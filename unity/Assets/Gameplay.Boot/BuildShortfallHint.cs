// 材料不足反馈的**文案**(N4 · 2026-11-14)。
//
// 权威:`design/levels/prologue-beat-script.md` P3-02a ——
//   「站 Tent 槽 + `BuildPlace`,材料 &lt; 5 ⇒ 屏幕下方一行:**还缺 N 件。**(**不**进演出)」
//
// ═══ 为什么单独成一个纯类 ═════════════════════════════════════════════════
//   文案是**判据**(「缺几件」这句话不能说错),而它唯一的输入是一个整数。
//   写成装配层的内联字符串 ⇒ 没有人能单独测它,而它恰好是最容易被悄悄改坏的东西
//   (「还缺 3 件」说成「还缺 3 个」、负数照样播、0 也播)。
//
// ⚠️ **0 / 负数 ⇒ 不播**(返回 `null`)。理由:那一行是给「按了没反应」的解释,
//   材料够的时候它不该出现 —— 出现了就是**假反馈**,比没有反馈更坏。

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>「还缺 N 件。」的唯一文案出口。</summary>
    public static class BuildShortfallHint
    {
        /// <summary>缺口 &gt; 0 时给出提示文本;否则 <c>null</c>(不播)。</summary>
        /// <param name="missing">缺多少份(<see cref="Gameplay.Presentation.Building.BuildController.LastMissingStock"/>)。</param>
        /// <returns>提示文本,或 <c>null</c>。</returns>
        public static string Format(int missing)
            => missing > 0 ? "还缺 " + missing + " 件。" : null;
    }
}
