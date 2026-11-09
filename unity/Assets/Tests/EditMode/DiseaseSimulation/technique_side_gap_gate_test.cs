// 技法侧缺口门 —— 两件「把已知缺口门化」的事(T12 · 2026-11-09)
//
// 权威:`design/gdd/reviews/technique-unlock-map-2026-11-09.md` §5.2 / §7
//       · **甲**:T7 的 `technique_coverage_test` 只朝一个方向看(每行病种有技法归宿);
//         本条看另一个方向 —— 每门技法有病种用它,否则具名豁免。
//       · **乙**:T9 的规则行有三类源 id,值域此前**无任何门**兜(见 T9 证据件 §6)。
//
// ─────────────────────────────────────────────────────────────────────────────
// 读哪些仓库文件(测试标准 §只读外部夹具的具名声明义务)
// ─────────────────────────────────────────────────────────────────────────────
// 读 `unity/Assets/DataCooked/` 下的 `disease_registry` / `disease_action_axis` /
//   `world_poi` / `52_event_pool` 四份**真产物**,经 `DataCookedDisk` + 生产 codec。
// 为什么必须读真产物:两件事判的都是「**代码里的声明**与**真表**是否一致」——
//   内联假表只能证明本夹具自己写的谓词(假绿形态 F1)。不写盘。
//
// ─────────────────────────────────────────────────────────────────────────────
// 本文件要证明的六件事
// ─────────────────────────────────────────────────────────────────────────────
//   ① 技法侧**无未豁免的零覆盖技法**(甲的主判据);
//   ② 豁免表**不陈旧**(补了数据就必须删豁免);
//   ③ 豁免理由**非空**、**不悬空**(id 必须 ∈ 处置轴闭集);
//   ④ 门的谓词**有牙**:无覆盖且无豁免 ⇒ 报它;有豁免 ⇒ 不报;有覆盖 ⇒ 报了才算陈旧(负控);
//   ⑤ 规则表**零违例**(乙的主判据;**今日空真**,由 ⑥ 的非空性反衬);
//   ⑥ 校验器**有牙**:三类坏 id 各报一条,好 id 不报(负控 + 正控)。

using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Presentation;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.DiseaseSimulation
{
    [TestFixture]
    public sealed class technique_side_gap_gate_test
    {
        // ════════════════════════════════════════════════════════════════
        //  真产物装载(四份,各一次)
        // ════════════════════════════════════════════════════════════════

        private static DrawSurfaceReport RealReport()
        {
            DiseaseRegistryDataSet registry = DataCookedDisk.LoadDiseaseRegistry();
            DiseaseActionAxisDataSet axis = DiseaseActionAxisCookedCodec.Read(
                DataCookedDisk.ReadAll(AddressablesDataProvider.DiseaseActionAxisAddress));

            var actionIds = new List<int>(axis.Actions.Count);
            for (int i = 0; i < axis.Actions.Count; i++) actionIds.Add(axis.Actions[i].ActionId);

            return DrawSurfaceAudit.Compute(registry.Rows, new TechniqueDrawIndex(axis.TreatableBy),
                                            playerLevel: 0, actionIds);
        }

        private static HashSet<int> AxisActionIds()
        {
            DiseaseActionAxisDataSet axis = DiseaseActionAxisCookedCodec.Read(
                DataCookedDisk.ReadAll(AddressablesDataProvider.DiseaseActionAxisAddress));
            var set = new HashSet<int>();
            for (int i = 0; i < axis.Actions.Count; i++) set.Add(axis.Actions[i].ActionId);
            return set;
        }

        private static HashSet<int> PoiIds()
        {
            WorldPoiDataSet poi = DataCookedDisk.LoadWorldPoi();
            var set = new HashSet<int>();
            for (int i = 0; i < poi.Pois.Count; i++) set.Add(poi.Pois[i].PoiId);
            return set;
        }

        private static HashSet<int> EventKeys()
        {
            EventPoolDataSet pool = DataCookedDisk.LoadEventPool();
            var set = new HashSet<int>();
            for (int i = 0; i < pool.Entries.Count; i++) set.Add(pool.Entries[i].Key);
            return set;
        }

        private static HashSet<int> RegistryDiseaseIds()
        {
            DiseaseRegistryDataSet registry = DataCookedDisk.LoadDiseaseRegistry();
            var set = new HashSet<int>();
            for (int i = 0; i < registry.Rows.Count; i++) set.Add(registry.Rows[i].DiseaseId);
            return set;
        }

        // ════════════════════════════════════════════════════════════════
        //  甲 · ① 技法侧无未豁免的零覆盖技法
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// 零覆盖技法集 **恰好等于** 具名豁免集(两个方向都要空)。
        /// <para>这一条就是 T11 那句「技法 1 覆盖 0 行」的回退护栏 ——
        /// 此前它可以悄悄烂着,现在它要么被补数据,要么挂着名字。</para>
        /// </summary>
        [Test]
        public void test_techniqueCoverageGate_passes()
        {
            DrawSurfaceReport report = RealReport();
            CoverageGateResult gate = DrawSurfaceAudit.EvaluateCoverageGate(
                report.Coverage, TechniqueCoverageExemptions.ByDesign);

            Assert.That(report.Coverage.Count, Is.GreaterThan(0),
                "逐技法覆盖为空 ⇒ 审计没读到数据,本条会空真通过(反空洞正控)");

            Assert.That(gate.UnexcusedGaps, Is.Empty,
                "有技法覆盖 0 行却**没有登记豁免** —— 它白开了且无人知道。" +
                "补数据,或写进 TechniqueCoverageExemptions.ByDesign 并说明理由");

            Assert.That(gate.StaleExemptions, Is.Empty,
                "豁免表陈旧 —— 这些技法已经有覆盖了,豁免行必须删(否则门在掩护一个已消失的缺口)");

            Assert.IsTrue(gate.Passed);
        }

        // ════════════════════════════════════════════════════════════════
        //  甲 · ② 豁免理由非空
        // ════════════════════════════════════════════════════════════════

        /// <summary>每条豁免必须带非空理由(空白理由 = 悄悄把门堵上)。</summary>
        [Test]
        public void test_exemptionReasons_areNonEmpty()
        {
            foreach (KeyValuePair<int, string> kv in TechniqueCoverageExemptions.ByDesign)
                Assert.That(string.IsNullOrWhiteSpace(kv.Value), Is.False,
                    "技法 " + kv.Key + " 的豁免理由为空 —— 空白理由等于把门堵上");
        }

        // ════════════════════════════════════════════════════════════════
        //  甲 · ③ 豁免不悬空
        // ════════════════════════════════════════════════════════════════

        /// <summary>豁免里的技法 id 必须真的存在于处置轴闭集(悬空 id = 拼写错误的豁免)。</summary>
        [Test]
        public void test_exemptions_areWithinActionClosedSet()
        {
            HashSet<int> actions = AxisActionIds();

            foreach (int techniqueId in TechniqueCoverageExemptions.ByDesign.Keys)
                Assert.That(actions.Contains(techniqueId), Is.True,
                    "豁免里的技法 id " + techniqueId + " 不在处置轴 actions[] 里 —— 悬空豁免");
        }

        // ════════════════════════════════════════════════════════════════
        //  甲 · ④ 门的谓词有牙(负控 —— 不依赖真表)
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// 纯谓词负控:零覆盖 + 无豁免 ⇒ 报 gap;零覆盖 + 有豁免 ⇒ 不报;有覆盖 + 有豁免 ⇒ 报陈旧。
        /// <para>没有这一条,① 在豁免表恰好正确时是绿的,而谓词写坏也看不出来。</para>
        /// </summary>
        [Test]
        public void test_gatePredicate_hasTeeth()
        {
            var coverage = new[]
            {
                new TechniqueCoverage(0, new[] { "DIS_A" }),   // 有覆盖,无豁免
                new TechniqueCoverage(1, new string[0]),        // 零覆盖,有豁免
                new TechniqueCoverage(2, new string[0]),        // 零覆盖,无豁免
                new TechniqueCoverage(3, new[] { "DIS_B" }),   // 有覆盖,有豁免(陈旧)
            };
            var exempt = new Dictionary<int, string> { [1] = "理由", [3] = "陈旧的理由" };

            CoverageGateResult gate = DrawSurfaceAudit.EvaluateCoverageGate(coverage, exempt);

            Assert.That(gate.UnexcusedGaps, Is.EqualTo(new[] { 2 }),
                "只有「零覆盖且无豁免」的 2 号该被报出来");
            Assert.That(gate.StaleExemptions, Is.EqualTo(new[] { 3 }),
                "3 号已有覆盖却还挂着豁免 ⇒ 陈旧");
            Assert.That(gate.Passed, Is.False);
        }

        // ════════════════════════════════════════════════════════════════
        //  乙 · ⑤ 规则表零违例
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// 生产规则表对三张真表零违例。
        /// <para>⚠️ **今日是空真**(`Default` 为空 ⇒ 零规则 ⇒ 零违例)。故本条显式断言
        /// 「表为空」,把空真写在明面上,并由 ⑥ 证明校验器有牙。</para>
        /// </summary>
        [Test]
        public void test_productionRules_haveNoDanglingSourceIds()
        {
            Assert.That(TechniqueUnlockRules.Default.Count, Is.EqualTo(0),
                "规则表今日为空 ⇒ 下面这条是**空真**,不是「查过没问题」。" +
                "填了第一行之后,请删掉本条断言,让下一条真正起作用");

            IReadOnlyList<string> violations = TechniqueUnlockRuleValidator.Validate(
                TechniqueUnlockRules.Default, PoiIds(), EventKeys(), RegistryDiseaseIds());

            Assert.That(violations, Is.Empty);
        }

        // ════════════════════════════════════════════════════════════════
        //  乙 · ⑥ 校验器有牙(三类坏 id 各一条 + 好 id 不报)
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// 三类源 id 各喂一个**不存在的**值 ⇒ 各报一条;再喂真表里确实存在的 ⇒ 零违例。
        /// <para>后半段(正控)不能省:只写负控的话,一个「永远报违例」的实现也会绿。</para>
        /// </summary>
        [Test]
        public void test_validator_hasTeeth()
        {
            var poi = new HashSet<int> { 100 };
            var events = new HashSet<int> { 200 };
            var diseases = new HashSet<int> { 300 };

            var bad = new[]
            {
                TechniqueUnlockRule.ForPoi(poiId: 999, requiredState: 0, techniqueId: 0),
                TechniqueUnlockRule.ForEvent(eventKey: 998, minTier: -1, techniqueId: 0),
                TechniqueUnlockRule.ForCase(diseaseId: 997, techniqueId: 0),
            };

            IReadOnlyList<string> badViolations =
                TechniqueUnlockRuleValidator.Validate(bad, poi, events, diseases);

            Assert.That(badViolations.Count, Is.EqualTo(3), "三类坏 id 各须报一条");
            Assert.That(string.Join(";", badViolations), Does.Contain("999"));
            Assert.That(string.Join(";", badViolations), Does.Contain("998"));
            Assert.That(string.Join(";", badViolations), Does.Contain("997"));

            // ── 正控:真表里存在的 id ⇒ 零违例 ──
            var good = new[]
            {
                TechniqueUnlockRule.ForPoi(poiId: 100, requiredState: 0, techniqueId: 0),
                TechniqueUnlockRule.ForEvent(eventKey: 200, minTier: -1, techniqueId: 0),
                TechniqueUnlockRule.ForCase(diseaseId: 300, techniqueId: 0),
            };
            Assert.That(TechniqueUnlockRuleValidator.Validate(good, poi, events, diseases), Is.Empty,
                "存在的 id 不得被报 —— 否则校验器是「永远报违例」的空实现");
        }

        /// <summary>null 词表 ⇒ 抛(空集与「没给表」是两件事)。</summary>
        [Test]
        public void test_validator_rejectsNullWordLists()
        {
            var rules = new[] { TechniqueUnlockRule.ForCase(1, 0) };
            Assert.Throws<System.ArgumentNullException>(
                () => TechniqueUnlockRuleValidator.Validate(rules, null, new HashSet<int>(), new HashSet<int>()));
            Assert.Throws<System.ArgumentNullException>(
                () => TechniqueUnlockRuleValidator.Validate(null, new HashSet<int>(), new HashSet<int>(), new HashSet<int>()));
        }
    }
}
