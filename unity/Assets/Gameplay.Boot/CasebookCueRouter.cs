// 2026-11-11 · T22 批十一 —— 脉案页「呈现反馈声」请求的**装配层翻译器**。
//
// 权威:
//   · design/gdd/casebook.md:394 ——「翻页声 / 压痕声 / 落笔声 ∈ **行为反馈白名单**」
//   · design/gdd/skeuomorphic-ui.md:1341 ——「42 侧 = 纸面**压痕声** + 行高亮」
//   · ADR-018 §六 —— 无提示音铁律:只报**行为**,不报状态
//   · ADR-013 §9 C3 —— 42 只渲染、不持状态,更不持声源(ADR-028 声源池归 44)
//
// 分工:42 只发**语义请求**(`ICasebookCueSink`),本类把请求翻成 `AudioCueDto`
// 交给 44 —— 与 `ProcessingCompletionDriver` / `PatientCueDriver` 同一形态。
//
// ⚠️ **空间化锚点 = 玩家当前格**(经流确立的 `LastCommittedCell`,ADR-020 §四),
//    不是 `(0,0,0)`:UI 事件发生时玩家可能离原点 15 格,锚在原点会被距离衰减吃光
//    (`TD-audio-110` 的同一教训)。42 不读玩家格(那是游戏状态),故由装配层补。

namespace DaYiJingCheng.Gameplay.Boot
{
    using System;
    using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
    using DaYiJingCheng.Sim.Contracts;

    /// <summary>把 42 的纸面反馈请求翻译成 44 的 `AudioCueDto`(2026-11-11 · `AC-39-12`)。</summary>
    /// <remarks>⚠️ 未解析到表行时**静默不发**(ordinal = `NoOrdinal`)—— 与
    /// 「表里没这一行」的既有降级面一致,不造死 cue。</remarks>
    public sealed class CasebookCueRouter : ICasebookCueSink
    {
        /// <summary>翻页 cue(表内既有行,`assets/data/audio_events.json`)。</summary>
        public const string PageTurnCueName = "Page_Turn";

        /// <summary>纸面压痕 cue(2026-11-11 新增行;素材只在该行烘焙后才发声)。</summary>
        public const string PaperIndentCueName = "UI_Paper_Indent";

        /// <summary>一次性 UI cue 的强度(0–255;纸面声不随距离 / 状态变化)。</summary>
        private const byte UiCueIntensity = 255;

        /// <summary>声源实体哨兵 = `None`(环境 / UI 声非实体声源,ADR-018 §四)。</summary>
        private const int NonEntitySource = -1;

        private readonly IAudioCueSink _audio;
        private readonly Func<Int3> _cellOf;
        private readonly int _pageTurnOrdinal;
        private readonly int _indentOrdinal;

        /// <param name="audio">44 的唯一入向面(`CueRenderSink`;不得为 null —— 拒以静默吞声)。</param>
        /// <param name="cellOf">玩家当前格的只读取值(装配层提供;42 不持该状态)。</param>
        /// <param name="pageTurnOrdinal">翻页 cue 的 ordinal;`NoOrdinal` = 不发。</param>
        /// <param name="indentOrdinal">压痕 cue 的 ordinal;`NoOrdinal` = 不发。</param>
        public CasebookCueRouter(IAudioCueSink audio, Func<Int3> cellOf,
                                 int pageTurnOrdinal, int indentOrdinal)
        {
            _audio = audio ?? throw new ArgumentNullException(nameof(audio));
            _cellOf = cellOf ?? throw new ArgumentNullException(nameof(cellOf));
            _pageTurnOrdinal = pageTurnOrdinal;
            _indentOrdinal = indentOrdinal;
        }

        /// <summary>本路由器实际会发的 cue 数(0–2;测试与装配判据的观测面)。</summary>
        public int ResolvedCueCount =>
            (_pageTurnOrdinal >= 0 ? 1 : 0) + (_indentOrdinal >= 0 ? 1 : 0);

        /// <inheritdoc/>
        public void RequestPaperIndent() => Emit(_indentOrdinal);

        /// <inheritdoc/>
        public void RequestPageTurn() => Emit(_pageTurnOrdinal);

        private void Emit(int ordinal)
        {
            if (ordinal < 0) return;   // 表里没有该行 ⇒ 不发(可观测:ResolvedCueCount)
            var cue = new AudioCueDto(
                cue: ordinal,
                intensity: UiCueIntensity,
                cell: _cellOf(),
                source: NonEntitySource,
                looped: false);
            _audio.Emit(in cue);
        }
    }
}