// 投放面复核 —— 把 map §8 的 R1 从「风险条目」变成**可跑的数字**(T11 · 2026-11-09)
//
// 权威:`design/gdd/reviews/technique-unlock-map-2026-11-09.md`
//       · §5.1 可投放 ⇔ (∃ t : t 已解锁 ∧ t ∈ treatable_by(d)) ∧ (d.min_skill_level ≤ 等级)
//       · §8 **R1**「技法门做得太严 ⇒ 玩家能治的病太少,日常循环枯竭」
//             处置 =「每章解锁后复核『可投放病种数 ≥ 3』」
//
// ── 为什么需要它 ──────────────────────────────────────────────────────────
// T1–T10 十步全绿,但**没有任何一步量过「玩家到底能遇到几种病」**。
// 技法门(`PatientDrawEligibility` ④)是硬门:一门都不解锁 ⇒ 整行拒收。
// 而 `treatable_by` 的覆盖面是**作者态数据**,不经实测看不出来 —— 手读 JSON 也不够,
// 因为「声明的空数组」在烘焙产物里是否留痕,决定了那一行是**放行**还是**永拒**
// (见 `PatientDrawEligibility` ③/④ 两条注释的矛盾;以本篇的运行结果为准)。
//
// ── 本类只测量,不判定 ────────────────────────────────────────────────────
// 它**不**决定「≥3 才算合格」—— 那是 R1 的门槛,归设计;它只把每个技法阶段能投放几行算清楚。
// 门槛判据由夹具钉在当前值上(变了就红,逼人回来复核),而不是由本类自己褒贬。
//
// ── 为什么住 `Gameplay.Boot` ──────────────────────────────────────────────
// 它消费 `PatientDrawEligibility` / `TechniqueDrawIndex`(都住这里),是**同一个判定面**的
// 观测器。若另开一处重写判据,测出来的就不是生产行为。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>单门技法**单独**已解锁时,能投放的病种。</summary>
    public readonly struct TechniqueCoverage
    {
        /// <summary>技法 id(= 处置轴 action id)。</summary>
        public readonly int TechniqueId;

        /// <summary>该技法单独解锁时可投放的病种 key(按注册表序)。</summary>
        public readonly IReadOnlyList<string> DiseaseKeys;

        /// <summary>构造。</summary>
        public TechniqueCoverage(int techniqueId, IReadOnlyList<string> diseaseKeys)
        {
            TechniqueId = techniqueId;
            DiseaseKeys = diseaseKeys ?? throw new ArgumentNullException(nameof(diseaseKeys));
        }

        /// <summary>覆盖行数。</summary>
        public int RowCount => DiseaseKeys.Count;
    }

    /// <summary>投放面报告。</summary>
    public sealed class DrawSurfaceReport
    {
        private readonly IReadOnlyList<DiseaseRegistryEntry> _rows;
        private readonly TechniqueDrawIndex _index;
        private readonly int _level;

        /// <summary>注册表总行数。</summary>
        public int TotalRows => _rows.Count;

        /// <summary>**空技法集**下仍可投放的行(= 无声明 ⇒ `Undeclared` 放行)。</summary>
        public IReadOnlyList<string> AlwaysDrawable { get; }

        /// <summary>**全技法解锁**仍不可投放的行 —— 没有任何一门技法能救它们。</summary>
        public IReadOnlyList<string> NeverDrawable { get; }

        /// <summary>逐技法覆盖(按技法 id 升序)。</summary>
        public IReadOnlyList<TechniqueCoverage> Coverage { get; }

        internal DrawSurfaceReport(IReadOnlyList<DiseaseRegistryEntry> rows, TechniqueDrawIndex index,
                                   int level, IReadOnlyList<string> alwaysDrawable,
                                   IReadOnlyList<string> neverDrawable,
                                   IReadOnlyList<TechniqueCoverage> coverage)
        {
            _rows = rows;
            _index = index;
            _level = level;
            AlwaysDrawable = alwaysDrawable;
            NeverDrawable = neverDrawable;
            Coverage = coverage;
        }

        /// <summary>
        /// 给定**已解锁技法集**时的可投放病种(按注册表序)。
        /// </summary>
        /// <param name="unlocked">已解锁技法 id 集(空集合法)。</param>
        /// <exception cref="ArgumentNullException"><paramref name="unlocked"/> 为 null。</exception>
        public IReadOnlyList<string> DrawableUnder(IReadOnlyCollection<int> unlocked)
        {
            if (unlocked == null) throw new ArgumentNullException(nameof(unlocked));

            var result = new List<string>();
            for (int i = 0; i < _rows.Count; i++)
            {
                DiseaseRegistryEntry row = _rows[i];
                PatientDrawDecision decision = PatientDrawEligibility.Decide(
                    row, _index, _level, techniqueId => Contains(unlocked, techniqueId),
                    playerBound: true);
                if (decision.Admitted) result.Add(row.DiseaseKey);
            }
            return result;
        }

        private static bool Contains(IReadOnlyCollection<int> set, int id)
        {
            // `IReadOnlyCollection<int>` 没有 Contains ⇒ 手写一遍(不 `new HashSet` 复制:
            // 本方法每行调一次,复制会把 O(rows×|set|) 变成 O(rows×|set|)+分配)。
            foreach (int value in set)
                if (value == id) return true;
            return false;
        }
    }

    /// <summary>投放面复核:逐技法覆盖 + 两个极端集。</summary>
    public static class DrawSurfaceAudit
    {
        /// <summary>
        /// 测量一次投放面。
        /// </summary>
        /// <param name="rows">注册表行(病 + 伤,全表)。</param>
        /// <param name="index">`treatable_by` 反向索引(**非 null** —— 未接线时测的不是生产面)。</param>
        /// <param name="playerLevel">「处方用药」等级(等级门的输入)。</param>
        /// <param name="actionIds">处置轴的全部 action id(可选)。
        /// <para>给了 ⇒ 逐技法覆盖**逐门列出**,包括**零覆盖**的技法(它才是「这门技法白开了」
        /// 的证据);不给 ⇒ 只列数据里出现过的技法 —— 那样「零覆盖」与「不存在」不可区分。</para></param>
        /// <returns>报告。</returns>
        /// <exception cref="ArgumentNullException">任一依赖为 null。</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="playerLevel"/> 为负。</exception>
        public static DrawSurfaceReport Compute(IReadOnlyList<DiseaseRegistryEntry> rows,
                                                TechniqueDrawIndex index, int playerLevel,
                                                IReadOnlyList<int> actionIds = null)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            if (index == null)
                throw new ArgumentNullException(nameof(index),
                    "索引未接线 —— `Decide` 在 index == null 时恒返回 NotWired,测出来的不是投放面");
            if (playerLevel < 0)
                throw new ArgumentOutOfRangeException(nameof(playerLevel), playerLevel, "等级不得为负");

            // ── ① 空集:谁在「一门都不会」时仍可投放 ──
            var always = new List<string>();
            var techniquesSeen = new SortedSet<int>();
            for (int i = 0; i < rows.Count; i++)
            {
                DiseaseRegistryEntry row = rows[i];
                if (PatientDrawEligibility.Decide(row, index, playerLevel, _ => false,
                                                  playerBound: true).Admitted)
                    always.Add(row.DiseaseKey);

                // 技法 id 空间从**数据**取:索引里出现过的全部技法。
                IReadOnlyList<int> of = index.TechniquesOf(row.DiseaseKey);
                for (int t = 0; t < of.Count; t++) techniquesSeen.Add(of[t]);
            }

            // ── ② 逐技法:该技法**真正解锁**了哪些行 ──
            //    ⚠️ 必须排除未声明的行:它在**任何**技法集下都 `Undeclared` 放行,
            //       计进覆盖会把每门技法的价值全部虚高(实测:不排除时 0 号技法「覆盖 6 行」,
            //       其中 2 行其实跟它没关系)。
            if (actionIds != null)
                for (int a = 0; a < actionIds.Count; a++) techniquesSeen.Add(actionIds[a]);

            var coverage = new List<TechniqueCoverage>(techniquesSeen.Count);
            foreach (int techniqueId in techniquesSeen)
            {
                var keys = new List<string>();
                for (int i = 0; i < rows.Count; i++)
                {
                    DiseaseRegistryEntry row = rows[i];
                    if (!index.IsDeclared(row.DiseaseKey)) continue;
                    if (PatientDrawEligibility.Decide(row, index, playerLevel,
                                                      t => t == techniqueId, playerBound: true).Admitted)
                        keys.Add(row.DiseaseKey);
                }
                coverage.Add(new TechniqueCoverage(techniqueId, keys));
            }

            // ── ③ 全集:全解锁仍不可投放的行(没有任何技法能救) ──
            var never = new List<string>();
            for (int i = 0; i < rows.Count; i++)
            {
                DiseaseRegistryEntry row = rows[i];
                if (!PatientDrawEligibility.Decide(row, index, playerLevel, _ => true,
                                                   playerBound: true).Admitted)
                    never.Add(row.DiseaseKey);
            }

            return new DrawSurfaceReport(rows, index, playerLevel, always, never, coverage);
        }

        /// <summary>
        /// 技法覆盖门(T12 · 2026-11-09):零覆盖技法集 **必须恰好等于**具名豁免集。
        /// </summary>
        /// <param name="coverage">逐技法覆盖(须由 <see cref="Compute"/> 传入全部 action id 得出 ——
        /// 否则「零覆盖」与「不存在」不可区分)。</param>
        /// <param name="exemptions">具名豁免(技法 id → 理由)。</param>
        /// <returns>两个方向的差集。</returns>
        /// <exception cref="ArgumentNullException">任一入参为 null。</exception>
        /// <remarks>
        /// ⚠️ 写成**集合相等**(两个方向的差集都要空),不是「包含」:
        /// 只查「零覆盖 ⊆ 豁免」会让豁免表无限长大;只查「豁免 ⊆ 零覆盖」会让新出现的
        /// 零覆盖技法无人处理。两个方向都是静默失效面。
        /// </remarks>
        public static CoverageGateResult EvaluateCoverageGate(
            IReadOnlyList<TechniqueCoverage> coverage,
            IReadOnlyDictionary<int, string> exemptions)
        {
            if (coverage == null) throw new ArgumentNullException(nameof(coverage));
            if (exemptions == null) throw new ArgumentNullException(nameof(exemptions));

            var gaps = new List<int>();
            var covered = new HashSet<int>();
            for (int i = 0; i < coverage.Count; i++)
            {
                TechniqueCoverage c = coverage[i];
                if (c.RowCount > 0) { covered.Add(c.TechniqueId); continue; }
                if (!exemptions.ContainsKey(c.TechniqueId)) gaps.Add(c.TechniqueId);
            }

            var stale = new List<int>();
            foreach (int techniqueId in exemptions.Keys)
                if (covered.Contains(techniqueId)) stale.Add(techniqueId);

            return new CoverageGateResult(gaps, stale);
        }
    }

    /// <summary>技法覆盖门的判定结果(T12)。</summary>
    public readonly struct CoverageGateResult
    {
        /// <summary>覆盖 0 行**且**未登记豁免的技法 —— 必为空(否则有技法白开了还没人知道)。</summary>
        public readonly IReadOnlyList<int> UnexcusedGaps;

        /// <summary>登记了豁免、但**已经有覆盖**的技法 —— 必为空(豁免陈旧 ⇒ 门在掩护一个已消失的缺口)。</summary>
        public readonly IReadOnlyList<int> StaleExemptions;

        internal CoverageGateResult(IReadOnlyList<int> unexcusedGaps, IReadOnlyList<int> staleExemptions)
        {
            UnexcusedGaps = unexcusedGaps;
            StaleExemptions = staleExemptions;
        }

        /// <summary>两侧都空 ⇒ 通过。</summary>
        public bool Passed => UnexcusedGaps.Count == 0 && StaleExemptions.Count == 0;
    }
}
