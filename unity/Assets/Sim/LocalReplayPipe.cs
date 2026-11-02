// ADR-001 §一 —— `IReplayPipe` 的 **P0 本地占位实现**(45 网络层 · P1b 才接 NGO / Fusion)。
//
// 权威来源(逐字):
//   docs/architecture/adr-001-networking-pipe-abstraction.md
//     · `:167`  「**P0 预埋**:接口与 `ReorderBuffer` 是纯 C# 逻辑,**P0 全实现为本地占位**」
//     · `:143-146`「发布 / 订阅语义:主机 `Publish`,客户端 `Subscribe`;事件已由 `IEventSink`
//                  按 Kind 路由落流,网络层**不再做语义判断**(纯管道)」
//     · `:148-150`「乱序注入 → 全序键吸收 → 无丢、无重放;缓冲有界(`MaxLag` 旋钮),
//                  超界丢包 + 告警(客户端重连 = 拉流重放)」
//
// ─────────────────────────────────────────────────────────────────────────────
// 为什么住 `Sim`(而非 `Sim.Contracts`)
// ─────────────────────────────────────────────────────────────────────────────
// 全序键的**权威计算** = `EventOrder.KeyOf(e)`(`Sim` 程序集,依赖 kindgen 的 `StreamRouting`)。
// 本实现要在 `Publish` 里算键 ⇒ 必须看见 `EventOrder` ⇒ 只能住 `Sim`。
// ⚠️ **接口住 `Sim.Contracts`、实现住 `Sim`** —— 与 ADR-029 / ADR-031 的
// 「契约支 + 实现支」同形,不新增程序集边(门 A / b2 / ADR-025 §① 一字不改)。
//
// ─────────────────────────────────────────────────────────────────────────────
// 本地占位的语义边界(**必须读清,否则会把占位当生产**)
// ─────────────────────────────────────────────────────────────────────────────
// 本实现 = **同进程内**的发布 / 订阅:
//   · `Publish` 立即算键、立即入 `ReorderBuffer`、**立即**回调全部订阅者;
//   · **零网络**、零线程、零延迟 —— 「传输顺序不依赖」在本地天然成立(顺序就是调用序);
//   · 订阅者是**回调列表**(多订阅 = 追加,不是替换)。
//
// ⚠️ **它证明的是「契约形状可被实现」,不是「网络层能工作」**:
//   · 乱序吸收面 = 只有 `ReorderBuffer`(本类按调用序入队 ⇒ **本地路径永远不乱序**);
//     真正的乱序只会在 P1b 接库之后出现。
//   · `Subscribe` 的「排队到主线程」义务(ADR-001 F3)在本地是 no-op(本来就在主线程)。
//   · P1b 换实现时,**接口一字不改**,只把 `Publish` 换成「写可靠消息通道」。
//
// ⚠️ **不做语义判断**(ADR-001 逐字):本类**不**校验 `SimEvent` 的 Kind 是否在白名单
// (那归 `EventOrder.KeyOf` 的 `StreamRouting`,它已构建期拒收列表外 Kind)。
// 本类也不去重、不排序(排序归 `ReorderBuffer`,去重归 `EventStream`)。
//
// ⚠️ **告警落点**:本类住 `Sim`(门 A:**零 `UnityEngine`**)⇒ 不能调 `Debug.LogWarning`。
// 告警经构造注入(生产装配接 `Debug.LogWarning`;测试接收集列表)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>
    /// <see cref="IReplayPipe"/> 的 **P0 本地占位**(同进程内发布 / 订阅,零网络)。
    /// <para>⚠️ 本类**不是**网络实现 —— P1b 的 swap 评审才把 `Publish` 接到真实通道
    /// (ADR-001 `:169-171`)。它存在的意义 = 「抽象点有落点」,使 P1b 的评审有可替换的接缝。</para>
    /// </summary>
    public sealed class LocalReplayPipe : IReplayPipe
    {
        private readonly ReorderBuffer _buffer;
        private readonly List<Action<SimEvent>> _subscribers;

        /// <summary>构造。</summary>
        /// <param name="maxLag">乱序容忍深度(<see cref="ReorderBuffer.MaxLag"/>;≥ 1)。</param>
        /// <param name="overflowWarningSink">超界告警落点(<c>null</c> = 不告警,丢弃照常)。</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxLag"/> &lt; 1。</exception>
        public LocalReplayPipe(int maxLag = 32, Action<string> overflowWarningSink = null)
        {
            _buffer = new ReorderBuffer(maxLag, overflowWarningSink);
            _subscribers = new List<Action<SimEvent>>();
        }

        /// <summary>本管道持有的重排缓冲(可观测:测试读 <see cref="ReorderBuffer.Dropped"/>)。</summary>
        public ReorderBuffer Buffer => _buffer;

        /// <summary>当前订阅者数(可观测:多订阅 = 追加的判据面)。</summary>
        public int SubscriberCount => _subscribers.Count;

        /// <inheritdoc/>
        /// <remarks>
        /// <b>本地占位的执行序</b>:算全序键 → 入缓冲(超界则丢弃并告警,本方法静默返回)→
        /// `Drain` → 逐条回调全部订阅者。
        /// <para>⚠️ 「入队即回吐」是本地占位的**取舍**:真正的网络实现会在 `Publish` 时只入队、
        /// 在收到对端确认后才 `Drain`(或由接收侧 `Drain`)。**契约不约束这个时序** ——
        /// ADR-001 只要求「有界 + 全序 + 无丢无重放」。</para>
        /// </remarks>
        public void Publish(in SimEvent e)
        {
            if (!_buffer.TryEnqueue(in e, EventOrder.KeyOf(in e)))
                return;   // 超界:已丢弃 + 已告警(ADR-001「超界丢包 + 告警」)

            IReadOnlyList<SimEvent> ready = _buffer.Drain();
            for (int i = 0; i < ready.Count; i++)
            {
                SimEvent ev = ready[i];
                // 快照订阅者数:回调里 Subscribe / Unsubscribe 不得改变本次派发的目标集
                // (否则「谁收到了」依赖回调内的副作用 —— 那是不可复现的)。
                int n = _subscribers.Count;
                for (int s = 0; s < n; s++)
                    _subscribers[s](ev);
            }
        }

        /// <inheritdoc/>
        /// <exception cref="ArgumentNullException"><paramref name="onEvent"/> 为 null。</exception>
        /// <remarks>多订阅 = **追加**(ADR-001 未定,本实现取追加 —— 见接口注的「归实现方」)。</remarks>
        public void Subscribe(Action<SimEvent> onEvent)
        {
            if (onEvent == null)
                throw new ArgumentNullException(nameof(onEvent),
                    "订阅 null 回调 —— 拒绝静默接受(否则「订阅了」与「没订阅」在外部不可分)");
            _subscribers.Add(onEvent);
        }
    }
}
