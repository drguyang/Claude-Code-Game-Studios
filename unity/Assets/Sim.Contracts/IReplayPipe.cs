// ADR-001 §一 —— pipe 抽象(45 网络层 · **P0 预埋** · P1b 实现)。
//
// 权威来源(逐字):
//   docs/architecture/adr-001-networking-pipe-abstraction.md
//     · `:137-141`  `IReplayPipe` 两个方法(`Publish` / `Subscribe`)
//     · `:167`      「**P0 预埋**:接口与 `ReorderBuffer` 是纯 C# 逻辑,**P0 全实现为本地占位**」
//     · `:448`      Implementation Plan 第 1 条「**P0**:写 `IReplayPipe` + `ReorderBuffer`
//                   (纯 C# 逻辑,零依赖)」
//     · `:121-123`  「pipe 契约 = **可靠即可,保序非必需**(有界重排缓冲吸收乱序,全序键排序)」
//     · `:151-157`  「传输层只保证每条消息**可靠到达**;顺序由流合并排序吸收」+
//                   「『保序非必需』是**安全的下界契约**,不是两库的真实行为;
//                    契约措辞以『**传输顺序不依赖**』为准」
//
// ─────────────────────────────────────────────────────────────────────────────
// 为什么住 `Sim.Contracts`(而非新开程序集)
// ─────────────────────────────────────────────────────────────────────────────
// ADR-025 §① 的装配清单是**封闭**的(未登记 asmdef = 构建失败)。本契约的消费者是 `Sim`
// (`IEventSink` 的对称面)与未来的 45 实现;而 `Sim` 的引用集**恰 = {BCL, Sim.Contracts}**
// ⇒ 只有住 `Sim.Contracts` 才能被 `Sim` 看见,且**不开新的程序集边**(门 A / b2 一字不改)。
//
// ─────────────────────────────────────────────────────────────────────────────
// 为什么**不**在这里放「第二 QoS 通道」
// ─────────────────────────────────────────────────────────────────────────────
// ADR-001 §一之二 的 `IPositionalChannel` / `WorldPosLatest` 是**表现态位置**通道
// (unreliable latest-value 表),与三流可靠 pipe **刻意分离**(防 HOL 阻塞)。
// 两者语义、QoS、消费者全不同 ⇒ 合并进一个文件会让「谁走哪条通道」在类型面不可分。
// ⚠️ 本件**只**落 ADR-001 §一 的两个类型(`IReplayPipe` / `ReorderBuffer`)——
// `IPositionalChannel` 的落点归 45 的 P1b 轮(它的消费者 = 44 的远端空间化,
// 而 44 今日走 `AudioCueDto.Cell` 的本地路径,见 ADR-028 §③)。
//
// ⚠️ **零引擎引用**:本文件只依赖 BCL + `Sim.Contracts` 自身的 `SimEvent` / `EventOrderKey`。
// 不得 import 任何 NGO / Fusion 类型(P1b 的 swap 评审才有权引入库)。

using System;
using System.Collections.Generic;

namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary>
    /// 三流可靠 pipe 的**抽象点**(ADR-001 §一)。**P0 本地占位,零依赖**。
    /// <para><b>语义</b>:主机 <see cref="Publish"/>,客户端 <see cref="Subscribe"/>。
    /// 事件已由 <see cref="IEventSink"/> 按 Kind 路由落流,**网络层不做语义判断**(纯管道)。</para>
    /// <para><b>契约强度 = 「传输顺序不依赖」,不是「保序」</b>(ADR-001 `:151-157`):
    /// 传输层只保证每条消息可靠到达;顺序由有界重排缓冲 + 三流全序键吸收。
    /// ⇒ 实现**不得**依赖传输顺序(本地占位的实现天然有序,但那不是契约的一部分)。</para>
    /// <para><b>线程模型</b>(ADR-001 引擎复核 F3):<see cref="Publish"/> 在主机 Step 边界(主线程);
    /// 传输线程零 Unity API;`Subscribe` 回调排队到主线程 PlayerLoop 注入;
    /// `Drain` 在主线程消费。⚠️ 本接口**不表达**线程归属 —— 那归 P1b 的实现件。</para>
    /// </summary>
    public interface IReplayPipe
    {
        /// <summary>主机:发出一条事件(事件已按三流路由落流)。</summary>
        /// <param name="e">要发出的事件。实现方**不得**修改其载荷引用(ADR-006 G-2 不可变池)。</param>
        void Publish(in SimEvent e);

        /// <summary>客户端:注册接收回调(重排缓冲吸收后回调)。</summary>
        /// <param name="onEvent">接收回调。<c>null</c> ⇒ 实现方须抛
        /// (静默接受 null 会让「订阅了」与「没订阅」在外部不可分)。</param>
        /// <para>⚠️ **多订阅者语义归实现方**:ADR-001 原文未定「第二个 Subscribe 是追加还是替换」。
        /// 本件不预设 —— P1b 的 swap 评审时按库能力裁定。</para>
        void Subscribe(Action<SimEvent> onEvent);
    }

    /// <summary>
    /// 有界重排缓冲(ADR-001 §一)。**P0 本地占位,零依赖**。
    /// <para><b>语义</b>:乱序注入 → 按 <see cref="EventOrderKey"/> 吸收 → 输出**已就绪前缀**,
    /// 无丢、无重放。缓冲**有界**(<see cref="MaxLag"/> 旋钮),超界 ⇒ **丢弃 + 告警**
    /// (客户端重连 = 拉流重放,ADR-010 三流是重放源)。</para>
    /// <para><b>「已就绪前缀」的判据</b>:按全序键排序后,取**从最小键开始的连续段** ——
    /// 但「连续」在全序键上无定义(键不是整数序列)。⇒ 实现采用**滞后窗**口径:
    /// 只有当新到的键 ≤ 「已排出最大键」时才可能乱序;窗口内按全序键排序,窗口外(超
    /// <see cref="MaxLag"/> 条)丢弃。**这是本实现的取舍,ADR-001 只要求「有界 + 无丢无重放」**,
    /// 具体算法归 P1b(本件只给一个满足契约的本地占位)。</para>
    /// </summary>
    /// <remarks>
    /// <para><b>为什么是 <c>sealed</b> 而非 interface</b>:ADR-001 原文写的是
    /// <c>sealed class ReorderBuffer</c> —— 它是**算法**不是接缝。P1b 若要换实现,
    /// 换的是整个类(而非按接口注入)。</para>
    /// <para><b>告警落点</b>:本类住 <c>Sim.Contracts</c>(门 A:零 <c>UnityEngine</c>)⇒
    /// **不能**调 <c>Debug.LogWarning</c>。告警经 <see cref="OverflowWarningSink"/> 注入
    /// (生产 = 装配层接 <c>Debug.LogWarning</c>;测试 = 收集列表)。
    /// <c>null</c> ⇒ 不告警(合法:纯逻辑夹具不需要)。⚠️ **但「超界丢弃」本身照常发生** ——
    /// 不告警 ≠ 不丢弃。</para>
    /// </remarks>
    public sealed class ReorderBuffer
    {
        private readonly List<SimEvent> _pending;
        private readonly List<EventOrderKey> _keys;
        private readonly Action<string> _warn;
        private long _dropped;
        private long _enqueued;

        /// <summary>乱序容忍深度(旋钮,ADR-001 `:145`)。必须 ≥ 1。</summary>
        public int MaxLag { get; }

        /// <summary>构造。</summary>
        /// <param name="maxLag">乱序容忍深度(≥ 1)。</param>
        /// <param name="overflowWarningSink">超界告警落点(<c>null</c> = 不告警,丢弃照常)。</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxLag"/> &lt; 1。</exception>
        public ReorderBuffer(int maxLag, Action<string> overflowWarningSink = null)
        {
            if (maxLag < 1)
                throw new ArgumentOutOfRangeException(nameof(maxLag), maxLag,
                    "MaxLag 必须 ≥ 1 —— 0 会让缓冲退化为「按到达序直通」," +
                    "即放弃 ADR-001「全序键吸收」的全部意义");
            MaxLag = maxLag;
            _pending = new List<SimEvent>(maxLag);
            _keys = new List<EventOrderKey>(maxLag);
            _warn = overflowWarningSink;
        }

        /// <summary>当前在缓冲里等待排出的条数。</summary>
        public int Count => _pending.Count;

        /// <summary>累计入队条数(含随后被丢弃的)。</summary>
        public long Enqueued => _enqueued;

        /// <summary>累计因超界被丢弃的条数(可观测:测试与 51 的读数面)。</summary>
        public long Dropped => _dropped;

        /// <summary>
        /// 按全序键入队。**超界 ⇒ 丢弃 + 告警**,不抛(抛会让乱序变成崩溃 ——
        /// ADR-001 要求的是「丢弃 + 告警」,重连拉流兜底)。
        /// </summary>
        /// <param name="e">待入队事件。</param>
        /// <param name="key">该事件的三流全序键(ADR-008 §跨流全序键)。
        /// <para>⚠️ <b>由调用方给,不由本类从 <paramref name="e"/> 现算</b>:全序键的
        /// 权威计算在 `EventOrder.KeyOf(e)`(`Sim` 程序集),而本类住 `Sim.Contracts`
        /// —— 两程序集方向不可倒(ADR-025 §①)。若本类自己拼一份 `EventOrderKey`,
        /// 就成了**第二真源**(两份「怎么算全序键」迟早在某次修订里分叉)。</para></param>
        /// <returns><c>true</c> = 已入队;<c>false</c> = 超界被丢弃。</returns>
        public bool TryEnqueue(in SimEvent e, EventOrderKey key)
        {
            _enqueued++;

            if (_pending.Count >= MaxLag)
            {
                _dropped++;
                _warn?.Invoke(
                    $"[ADR-001 ReorderBuffer] 超界丢弃:缓冲已满({MaxLag} 条)," +
                    $"丢弃 (tick={key.Tick}, seq={key.Seq})。客户端重连 = 拉流重放(ADR-010 三流是重放源)。");
                return false;
            }

            // 按全序键插入(保持 _pending / _keys 同步升序)。
            int at = _keys.Count;
            for (int i = 0; i < _keys.Count; i++)
            {
                if (key.CompareTo(_keys[i]) < 0) { at = i; break; }
            }
            _keys.Insert(at, key);
            _pending.Insert(at, e);
            return true;
        }

        /// <summary>
        /// 输出**已就绪前缀**(全序)。调用方按需反复 Drain。
        /// <para><b>「就绪」的口径(本实现的取舍,见类注)</b>:缓冲里**按全序键已升序**,
        /// 故全部都是「就绪」—— 输出全部并清空。⚠️ 这**不是**最优算法(理想实现会在
        /// 已知窗口边界时提前输出),但它满足 ADR-001 的三条契约:
        /// **有界**(<see cref="MaxLag"/>)、**全序**(排序后输出)、**无丢无重放**
        /// (每入队事件至多被 Drain 一次)。</para>
        /// <returns>按全序键升序的已就绪事件(每次调用清空缓冲)。</returns>
        public IReadOnlyList<SimEvent> Drain()
        {
            if (_pending.Count == 0) return Array.Empty<SimEvent>();

            var ready = new List<SimEvent>(_pending.Count);
            for (int i = 0; i < _pending.Count; i++) ready.Add(_pending[i]);
            _pending.Clear();
            _keys.Clear();
            return ready;
        }
    }
}
