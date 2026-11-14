// 材料不足反馈的**文案**(N4 · 2026-11-14;P3-02a 逐件点名 · 2026-11-14)。
//
// 权威:`design/levels/prologue-beat-script.md` P3-02a ——
//   「站 Tent 槽 + `BuildPlace`,五件组件不齐 ⇒ 屏幕下方一行:**还缺:门板、钉子。**
//     (**点名缺哪几件**,**不**进演出)」
//
// ═══ 为什么单独成一个纯类 ═════════════════════════════════════════════════
//   文案是**判据**(「缺哪几件」这句话不能说错),而它唯一的输入是缺口数与明细。
//   写成装配层的内联字符串 ⇒ 没有人能单独测它,而它恰好是最容易被悄悄改坏的东西
//   (「还缺 3 件」说成「还缺 3 个」、负数照样播、0 也播、把「有」说成「缺」)。
//
// ═══ 两种口径(为什么都要)═══════════════════════════════════════════════
//   ① **逐件点名**(明细可用)⇒ `还缺:门板、钉子。` —— P3-02a 要的就是它。
//   ② **总数兜底**(明细为空 / 有未知 item_key)⇒ `还缺 N 件。` —— 旧口径。
//   ⚠️ 明细里**任一件**叫不出名字就整体回退到 ②:半截名单(「还缺:门板、item_key=99」)
//      比总数更坏 —— 它把**实现细节**播给了玩家。
//
// ⚠️ **0 / 负数 ⇒ 不播**(返回 `null`)。理由:那一行是给「按了没反应」的解释,
//   材料够的时候它不该出现 —— 出现了就是**假反馈**,比没有反馈更坏。

using System.Collections.Generic;
using DaYiJingCheng.Sim.World;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>「还缺 …。」的唯一文案出口。</summary>
    public static class BuildShortfallHint
    {
        /// <summary>缺口 &gt; 0 时给出提示文本;否则 <c>null</c>(不播)。</summary>
        /// <param name="missing">缺多少份(<see cref="Gameplay.Presentation.Building.BuildController.LastMissingStock"/>)。</param>
        /// <param name="items">逐件缺口(<see cref="Gameplay.Presentation.Building.BuildController.LastMissingByItem"/>);
        /// <c>null</c> / 空 = 只有总数可报。</param>
        /// <returns>提示文本,或 <c>null</c>。</returns>
        public static string Format(int missing, IReadOnlyList<ModuleCostLine> items = null)
        {
            if (missing <= 0) return null;

            string total = "还缺 " + missing + " 件。";
            if (items == null || items.Count == 0) return total;

            var names = new List<string>(items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Qty <= 0) continue;   // 理论上不会出现;出现了也不该点进名单
                string name = ItemNameOf(items[i].ItemKey);
                if (name == null) return total;    // 有叫不出名字的 ⇒ 整体回退(见文件头)
                if (!names.Contains(name)) names.Add(name);
            }

            return names.Count > 0 ? "还缺:" + string.Join("、", names) + "。" : total;
        }

        /// <summary>
        /// 建材的物品显示名(`item_key` → 中文名)。
        /// <para>⚠️ 表源 = `design/levels/prologue-beat-script.md` §3.1 的 PA-01…PA-05
        /// (门板 9 / 钉子 10 / 房梁 11 / 布 12 / 油布 13)。仓库里**没有**物品名数据表
        /// (物品表只有 ordinal),故此处是**唯一落点**;将来物品名进数据后本表应删掉、
        /// 改读数据 —— 那时「叫不出名字」才会真正等于「数据缺失」。</para>
        /// </summary>
        /// <returns>显示名;未知 key 返回 <c>null</c>。</returns>
        public static string ItemNameOf(int itemKey) => itemKey switch
        {
            9 => "门板",
            10 => "钉子",
            11 => "房梁",
            12 => "布",
            13 => "油布",
            _ => null,
        };
    }
}
