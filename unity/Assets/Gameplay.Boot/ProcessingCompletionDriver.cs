// S24-1 · processing story-005 —— 炮制完成调度的**发射侧**(18 → 44 的 cue 边界)
//
// 权威来源:
//   design/gdd/processing.md
//     · :597-598 cue 命名 —— `sfx_process_light`(起炉)/ `sfx_process_complete`(起货)/
//       `sfx_process_work`(行为声床)
//     · AC-18-21 —— 「时间到达 `CompleteTick` ⇒ … + `sfx_process_complete` 发射;
//       禁以墙钟 / 帧对齐为发射条件」
//   ADR-018 §一 —— 44 只触发、只渲染,永不持有游戏状态;**18 是发射方**
//   ADR-005 —— 完成 tick 到达由 `ITickProvider` 驱动(本类**不**自己推进 tick)
//   ADR-013 / GDD BL-4 —— 42 不画进度;器物状态语义归 18(OQ-18-9 具名形态未裁)
//
// ─────────────────────────────────────────────────────────────────────────────
// 本类**只做一件事**:tick 边沿 → 问调度 → 发 `sfx_process_complete`
// ─────────────────────────────────────────────────────────────────────────────
// 调度判据(哪些炉到点了)住 `Sim` 的 `FurnaceCompletionSchedule` —— 那是纯函数面,
// 本类一行判据都不重写(否则两份「到点」判据迟早分叉,本项目已抓过多次的「第二真源」形态)。
// 本类是**装配层**:把 `Sim` 的答案接成 44 能吃的 `AudioCueDto`。
//
// ─────────────────────────────────────────────────────────────────────────────
// 🔁 2026-11-11(T22 批九):**表里已有该行,本类在生产上会出声**
// ─────────────────────────────────────────────────────────────────────────────
// 原文(保留,已不成立):「今日**生产上不会出声** —— `audio_events.json` 的 19 行 cue 里
// 没有 `sfx_process_*` 三行 ⇒ 装配期 `TryResolveCue` 解析不到 ⇒ 首行即返回」。
// 现状:`sfx_process_complete` 已作为**第 20 行**追加(素材 + 重烘 20 行 + Addressables),
// `RequiredCues` 同批加门(`AudioEventTableGates`)⇒ `CueResolved` 在生产上为 true。
// ⚠️ 仍**不得**把它读成「起货音已验收」:AC-18-09 / 22 的听测 `[L]` 仍 `NOT-RUN`,
//    素材「能接受」只等于人耳试听一次通过,不等于声学判据签核。
//
// ─────────────────────────────────────────────────────────────────────────────
// 为什么在本类里持有「上一 tick」而不是每次都算
// ─────────────────────────────────────────────────────────────────────────────
// AC-18-21 要求「恰 1 条」:同一 tick 边沿被重入(读档后重放 / 上游把本帧的 tick 序列
// 重发)时,若再算一遍就会**重复发射**。故本类只接受**严格递增**的 tick,相等或回退即
// fail-closed 抛出 —— 静默容忍会让「重复发射」这种可听辨缺陷变成无声的数据错误。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Presentation.Audio;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.Processing;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>炮制完成调度的发射侧驱动 —— 每 tick 边沿把「到点的炉」发成 `sfx_process_complete`。
    /// <para>⚠️ 本类**不持有落地写权**(`DropSpawned` 的发出者 = 20),**不写流**,
    /// **不改器物状态**;它只是 ADR-018 §一 意义上的「触发方」。</para>
    /// <para>⚠️ 判据唯一出处 = <see cref="FurnaceCompletionSchedule.CollectAt"/>;本类不重写。</para>
    /// </summary>
    public sealed class ProcessingCompletionDriver
    {
        /// <summary>起货 cue 的**名字**(GDD `processing.md:597-598` 逐字)。
        /// <para>⚠️ 本常量不是第二份白名单:表里没有该名 ⇒ <c>TryGetOrdinal</c> 返回 false,
        /// 装配层据此传 <see cref="AudioEventDataSet.NoOrdinal"/>,本类不发声(可观测)。</para>
        /// <para>⚠️ 名字现在**也**写进了 <c>AudioEventDataSet.RequiredCues</c> 的构建期断言
        /// (2026-11-11 T22 批九:加表行与加门同批)—— 原文「加表行的那一轮再同步加门」已兑现。</para>
        /// </summary>
        public const string CompleteCueName = "sfx_process_complete";

        /// <summary>起货 cue 的强度(0–255 归一)。**种子值** —— 数值调参归用户(项目铁律);
        /// 44 侧混音的增益归 ADR-018 §三 的总线,不在此处。</summary>
        public const byte CompleteCueIntensity = 255;

        private readonly IReadOnlyList<SimEvent> _events;
        private readonly IPayloadDecoder _decoder;
        private readonly IAudioCueSink _audio;
        private readonly int _completeCueOrdinal;
        private readonly List<FurnaceCompletion> _buffer = new List<FurnaceCompletion>();

        private long _lastTick = long.MinValue;

        /// <summary>构造。</summary>
        /// <param name="events">事件流只读视图(装配期传入**同一实例**;全流即可,调度自筛 `Kind`)。</param>
        /// <param name="decoder">载荷解码器(ADR-031)。</param>
        /// <param name="audio">44 的入向唯一界面(ADR-018 §一)。</param>
        /// <param name="completeCueOrdinal">装配期解析出的 cue ordinal;
        /// 未解析 = <see cref="AudioEventDataSet.NoOrdinal"/> ⇒ 本驱动不发声。</param>
        /// <exception cref="ArgumentNullException">任一引用入参为 null(fail-closed:
        /// 宽容处理会让「没有起货声」变成静默失效)。</exception>
        public ProcessingCompletionDriver(IReadOnlyList<SimEvent> events, IPayloadDecoder decoder,
                                          IAudioCueSink audio, int completeCueOrdinal)
        {
            _events = events ?? throw new ArgumentNullException(nameof(events),
                "未传入事件流 —— 完成调度无从求值");
            _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder),
                "未传入 IPayloadDecoder —— 拒绝静默「没有炉起货」");
            _audio = audio ?? throw new ArgumentNullException(nameof(audio),
                "未传入 IAudioCueSink —— 拒绝静默丢弃 cue");
            _completeCueOrdinal = completeCueOrdinal;
        }

        /// <summary>cue 是否已解析到事件表行。`false` ⇒ <see cref="OnTickEdge"/> 恒 no-op
        /// (表里没有 `sfx_process_complete` 行)。</summary>
        public bool CueResolved => _completeCueOrdinal != AudioEventDataSet.NoOrdinal;

        /// <summary>上次 <see cref="OnTickEdge"/> 收集到的完成记录(**下次调用即失效**,
        /// 缓冲复用;只读用途,调用方不得持有)。</summary>
        public IReadOnlyList<FurnaceCompletion> LastCompletions => _buffer;

        /// <summary>累计发出去的 cue 条数(可观测面:AC-18-21「恰 1 条」的计数判据)。</summary>
        public int EmittedCueCount { get; private set; }

        /// <summary>
        /// 读档重建:把单调门**挪到恢复出来的 tick**(2026-11-05 · `TD-sim-05` 同族)。
        /// <para>病因与 `DiseaseVitalsService.ResetAfterRestore` 同类:`_lastTick` 只进不退,
        /// 读一份更早的档后下一个边沿会抛「非递增 tick」,世界每 tick 抛一次。</para>
        /// <para>⚠️ 与体征链**处置不同**:体征链把状态清空、让下一个边沿重放(它必须从流重建);
        /// 本类**不能重放** —— 重放会为已经发过的炉**再发一次** cue。所以这里是把门移到存档时刻
        /// (`tick ≤ 存档时刻` 的一切都视为已处理),之后才继续。</para>
        /// </summary>
        public void ResumeAt(long tick)
        {
            _lastTick = tick;
            _buffer.Clear();
        }

        /// <summary>tick 边沿入口 —— 收集本 tick 到点的炉并按炉发 `sfx_process_complete`。</summary>
        /// <param name="tick">当前逻辑 tick(`ITickProvider.CurrentTick`)。</param>
        /// <returns>本 tick 到点的炉数(不是 cue 条数 —— cue 未解析时为 0)。</returns>
        /// <exception cref="InvalidOperationException"><paramref name="tick"/> 未严格递增
        /// —— 重入会让同一炉重复发射,故 fail-closed。</exception>
        public int OnTickEdge(long tick)
        {
            if (tick <= _lastTick)
                throw new InvalidOperationException(
                    $"[炮制] 完成调度收到非递增 tick({tick} ≤ 上次 {_lastTick})—— " +
                    "同一 tick 边沿重入会重复发起货 cue;拒绝静默双发");
            _lastTick = tick;

            // 表里没有这一行 ⇒ 不扫描、不发(今日的生产态;见文件头)。
            if (!CueResolved)
            {
                _buffer.Clear();
                return 0;
            }

            int n = FurnaceCompletionSchedule.CollectAt(_events, _decoder, tick, _buffer);
            for (int i = 0; i < n; i++)
            {
                FurnaceCompletion c = _buffer[i];
                var cue = new AudioCueDto(
                    cue: _completeCueOrdinal,
                    intensity: CompleteCueIntensity,
                    cell: new Int3(c.ToolCell.X, c.ToolCell.Y, c.ToolCell.Z),
                    source: c.ActorId,       // 声源实体 = 该炉的归属玩家
                    looped: false);          // 起货是一次性事件,不是声床
                _audio.Emit(in cue);
                EmittedCueCount++;
            }

            return n;
        }
    }
}
