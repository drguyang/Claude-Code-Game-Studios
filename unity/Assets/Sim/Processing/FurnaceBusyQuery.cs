// `ISingleFurnaceQuery` 的**生产实现** —— 单炉空闲(AC-18-10 的第三条件)
//
// ── 判据真源(不许有第二个)────────────────────────────────────────────────
//   `FurnaceState.IsBusy(events, decoder, actorId, tick)`
//     = 「该玩家此刻是否已有一炉在烧」(半开区间 `[start, start+duration)`,
//       见 `FurnaceState` 2026-10-26 评审 W2 注记)。
//
// ⚠️ **本类零判据、零状态**:只做「取流 + 取 tick + 转发」。
//    任何「完成当拍忙不忙」的口径写在这里 = 第二真源(`IsBusyCore` 是唯一定义点)。
//
// ── 为什么住 `Sim` 而不是 `Gameplay.Boot`
//   本类**只**转接 `FurnaceState.IsBusy`,两者都在 `Sim` —— 没有任何跨程序集需求。
//   ⚠️ 曾短暂放在 `Gameplay.Boot`,被 `test_ac1817b_saveComposerReferencesNoProcessingTypes`
//   判红(该门扫 `Gameplay.Boot` 全目录要求零 18 类型引用)。**移回 `Sim` 是正确落点**,
//   不是为了让门变绿而改门 —— 那条门至今未动。
//
// ── 为什么本类此前不存在(`ISingleFurnaceQuery` 全树零实现)
//   它是 AC-18-10 的接缝,而 AC-18-10 的**消费面**(42 的炮制面板)一直没建 ⇒
//   接口立了、实现没落。本轮(`O-18-R6` 接口半边)补上,使 `ProcessingAdmissionQuery`
//   有真源可转接 —— 否则它只能拿一个假 `false`(「炉永远空闲」),那正是静默失效。
//
// ── 依赖为什么是 `Func`
//   事件流与当前 tick 都随时间变化。持快照 = 炉早已空出来,面板却还判「忙」。
//   tick 走回调而非读 `ITickProvider` 的静态值,理由同:42 屏在**打开那一刻**求值。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.Processing
{
    /// <summary><see cref="ISingleFurnaceQuery"/> 的生产实现:转发到 <see cref="FurnaceState"/>。</summary>
    /// <remarks>
    /// <para>⚠️ <b>本类每次查询重扫全流</b>(`FurnaceState.TryGetLatest` 的既有形状)。
    /// P0 的炮制面板是玩家主动打开的屏,不是每 tick 热路径 ——
    /// 真要做候选集逐行求值(一屏 ≤12 条 × 全流扫描)时须另开 ADR 优化,
    /// 不得在此私自加缓存(缓存失效判据会是第三真源)。</para>
    /// </remarks>
    public sealed class FurnaceBusyQuery : ISingleFurnaceQuery
    {
        private readonly Func<IReadOnlyList<SimEvent>> _events;
        private readonly IPayloadDecoder _decoder;
        private readonly Func<long> _tick;

        /// <summary>构造查询面。</summary>
        /// <param name="events">事件流只读视图(每次调用重取)。</param>
        /// <param name="decoder">载荷解码器(ADR-031)。</param>
        /// <param name="tick">当前逻辑 tick(每次调用重取;**不得**用墙钟)。</param>
        /// <exception cref="ArgumentNullException">任一依赖为 null。</exception>
        public FurnaceBusyQuery(Func<IReadOnlyList<SimEvent>> events,
                                IPayloadDecoder decoder,
                                Func<long> tick)
        {
            _events = events ?? throw new ArgumentNullException(nameof(events),
                "未注入事件流视图 —— 拒绝返回「谁都不忙」");
            _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder),
                "未注入载荷解码器 —— 拒绝返回「谁都不忙」(那是进行中炉子的静默失效)");
            _tick = tick ?? throw new ArgumentNullException(nameof(tick),
                "未注入 tick 取法 —— 拒绝用墙钟(ADR-005:逻辑 tick 是全案唯一时间量纲)");
        }

        /// <inheritdoc/>
        public bool IsBusy(int actorId)
        {
            IReadOnlyList<SimEvent> events = _events();
            if (events == null) return false;   // 无流 ⇒ 无炉(装配早期,合法)
            return FurnaceState.IsBusy(events, _decoder, actorId, _tick());
        }
    }
}
