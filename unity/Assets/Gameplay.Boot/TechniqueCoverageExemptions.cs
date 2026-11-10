// 技法的**具名豁免** —— 「这门技法当前没有任何病种用它」的唯一合法出口(T12 · 2026-11-09)
//
// 权威:`design/gdd/reviews/technique-unlock-map-2026-11-09.md`
//       · §7 T7 的**镜像**:T7 建的 `technique_coverage_test` 断言「每一行病种都有技法归宿」,
//         本条断言「每一门技法都有病种用它」。**门此前只朝一个方向看** ——
//         这就是 T11 量出「技法 1 覆盖 0 行」却没有任何东西会响的直接原因。
//       · §5.1 可投放 ⇔ ∃ t : t 已解锁 ∧ t ∈ treatable_by(d)
//
// ── 为什么「豁免表」是生产代码而不是测试里的私有字典 ──────────────────────
// 它是**具名缺口清单**,不是测试夹具。放在生产装配面意味着:
//   · 「这门技法白开了」是一份**被声明**的待办,而不是一条测试里的注释;
//   · 补数据的人打开 `Gameplay.Boot` 就能看到它还欠着什么;
//   · 判据(集合相等,不是包含)由 `technique_side_gap_gate_test` 执行。
//
// ── 本表只许按裁定修改,不许为了转绿加行 ─────────────────────────────────
// 加一行 = 承认「这门技法当前无用」;删一行 = 补了数据。两种都是**事实变更**,
// 必须同批更新 `impl-t11-draw-surface-audit-2026-11-09.md` 的表的。

using System;
using System.Collections.Generic;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>覆盖 0 行的技法的具名豁免(技法 id → 理由)。</summary>
    public static class TechniqueCoverageExemptions
    {
        /// <summary>
        /// 今日**没有任何病种使用**的技法,逐条带理由。
        /// </summary>
        /// <remarks>
        /// <para>判据面由 <c>technique_side_gap_gate_test</c> 执行:
        /// 实际零覆盖集 **必须恰好等于** 本表键集(集合相等,防「悄悄加豁免」与「豁免陈旧」).</para>
        /// </remarks>
        public static readonly IReadOnlyDictionary<int, string> ByDesign =
            new Dictionary<int, string>
            {
                [1] =
                    "节奏型通气 —— P0 **没有任何病种**把它列进 treatable_by(T11 实测:覆盖 0 行)。" +
                    "🔁 2026-11-10(A3-b,用户裁定「从起步集移出」):它已于本批从 " +
                    "`StartingTechniques` 移出(现 = {0}),不再是「开局白给一个没用的动作」。" +
                    "⚠️ **但本行不能删** —— 本门的判据是「零覆盖集 == 豁免键集」," +
                    "与起步集无关;移出起步集**不改覆盖**,删了本条门当场转红。" +
                    "(原文「② 把它从 StartingTechniques 移出。任一落地后删本行」是**错的**,已订正。)" +
                    "唯一出口 = ① 给它挂上病种(改 disease_action_axis.json;A2 内容批)。",
            };

        /// <summary>某门技法是否已登记为具名豁免。</summary>
        /// <param name="techniqueId">技法 id(= 处置轴 action id)。</param>
        public static bool IsExempt(int techniqueId) => ByDesign.ContainsKey(techniqueId);
    }
}
