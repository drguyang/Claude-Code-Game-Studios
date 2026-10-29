// 13 的 `CueKind` → 42 的 `PatientCueVisual`(`AC-13-F1` 的 42 侧达成面 · 2026-10-29 · 缺口 D)。
//
// ── 为什么这段映射住在 `Gameplay.Boot` ──────────────────────────────────────
// 42(`Gameplay.UI`)**不引用** 13(`Gameplay.Presentation/PatientAI`),13 也不引用 42 ——
// 承 ADR-013 §9 C3(呈现层只渲染)与 ADR-016(决策进 sim / 边界层)。两者只在本装配层相接,
// 所以「语义 id → 可见物」这一跳只能住这里。
//
// ⚠️ **映射按语义,不按素材**:依据是 `CueKind` 的语义(13 的枚举),不是音轨名 / ordinal。
//    ordinal 是事件表的行序(ADR-014 §五 登记的脆弱面),拿它选可见物会让
//    「重排 `audio_events.json` ⇒ 咳嗽显示成呻吟」成为静默故障。
//
// ⚠️ **P0 可达性(如实登记)**:13 的调度在 P0 恒传 `kind: CueKind.Cough`
//    (`PatientCueDriver` 文件头注 / `PatientCueSchedule.Decide` 的调用点),
//    故 `Groan` 这一支**今日无生产生产者** —— 它只由夹具直测。
//    **不得**把这一支读成「已端到端验证」。

using DaYiJingCheng.Gameplay.PatientAI;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>13 的 cue 语义 → 42 的可见对应物(装配层唯一的跨层翻译点)。</summary>
    public static class PatientCueVisualMap
    {
        /// <summary>把 13 的语义 id 翻成 42 的三态之一。
        /// <para>⚠️ **不设「默认落咳嗽」的兜底** —— 13 日后新增 `CueKind` 时,兜底会让
        /// 新语义**静默显示成咳嗽**(错标比崩溃更难发现)。未知值 ⇒ 具名抛出。</para></summary>
        /// <exception cref="System.ArgumentOutOfRangeException">`kind` 不在 13 今日的两值之内
        /// (说明 13 扩了枚举而本映射没跟上)。</exception>
        public static PatientCueVisual Of(CueKind kind)
        {
            if (kind == CueKind.Cough) return PatientCueVisual.Cough;
            if (kind == CueKind.Groan) return PatientCueVisual.Groan;
            throw new System.ArgumentOutOfRangeException(nameof(kind), kind,
                "[PatientCueVisualMap] 未知的 CueKind —— 13 扩了枚举而本映射没跟上。" +
                "不得回落到咳嗽形(会把新语义错标成咳嗽)。");
        }
    }
}
