// 投放面复核 —— 判据夹具(T11 · 2026-11-09)
//
// 权威:`design/gdd/reviews/technique-unlock-map-2026-11-09.md`
//       · §5.1 可投放 ⇔ (∃ t 已解锁 ∧ t ∈ treatable_by(d)) ∧ (min_skill_level ≤ 等级)
//       · §8 **R1**:「技法门做得太严 ⇒ 玩家能治的病太少」,处置 =「复核可投放病种数 ≥ 3」
//
// ─────────────────────────────────────────────────────────────────────────────
// 读哪些仓库文件(测试标准 §只读外部夹具的具名声明义务)
// ─────────────────────────────────────────────────────────────────────────────
// 读 `unity/Assets/DataCooked/disease_registry.cooked.bytes` 与
//   `disease_action_axis.cooked.bytes`(经 `DataCookedDisk` + 生产 codec)。
// 为什么必须读真产物:R1 量的是**这一份作者态内容**的投放面;
//   内联一份假表只能证明本夹具自己写的谓词(假绿形态 F1)。
// ⚠️ 本文件零写盘。
//
// ─────────────────────────────────────────────────────────────────────────────
// 本文件要证明的六件事
// ─────────────────────────────────────────────────────────────────────────────
//   ① **分类穷尽** —— 每一行至少落在「空集即可投放 / 某门技法覆盖 / 全解锁仍拒」之一
//      (这条是**反空洞正控**:审计若静默丢行,①红);
//   ② **两个极端集**的语义 —— 无声明 ⇒ 空集也放行;声明为空 ⇒ 全解锁仍拒;
//   ③ **逐技法覆盖**与 `DrawableUnder({t})` 逐行一致(两条独立路径不许分叉);
//   ④ **多技法取并集**;
//   ⑤ **拒绝未接线**:index 为 null / 等级为负 ⇒ 抛(测出来的不是生产面);
//   ⑥ **真数据快照** —— 今天开局(起步集 `{0,1}`)能投放几行,钉在**实测值**上;
//      内容一变就红,逼人回来复核 R1 并更新证据件。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Presentation;
using DaYiJingCheng.Sim;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.DiseaseSimulation
{
    [TestFixture]
    public sealed class draw_surface_audit_test
    {
        // ════════════════════════════════════════════════════════════════
        //  夹具(合成)
        // ════════════════════════════════════════════════════════════════

        private static DiseaseRegistryEntry Row(int id, string key, int minSkill = 0)
            => new DiseaseRegistryEntry { DiseaseId = id, DiseaseKey = key, MinSkillLevel = minSkill };

        private static TechniqueDrawIndex Index(params TreatableByCookedRow[] rows)
            => new TechniqueDrawIndex(rows);

        private static TreatableByCookedRow Tb(string key, int action)
            => new TreatableByCookedRow(key, action, "symptomatic");

        // ════════════════════════════════════════════════════════════════
        //  ② 两个极端集
        // ════════════════════════════════════════════════════════════════

        /// <summary>索引里**没有**这个病种 ⇒ 判 `Undeclared` ⇒ 空技法集也放行。</summary>
        [Test]
        public void test_undeclaredRow_isAlwaysDrawable()
        {
            var rows = new[] { Row(1, "DIS_A") };
            var report = DrawSurfaceAudit.Compute(rows, Index(), playerLevel: 0);

            Assert.That(report.AlwaysDrawable, Is.EqualTo(new[] { "DIS_A" }),
                "无声明 = 数据没覆盖 ⇒ Undeclared 放行(不是「无处置可用」)");
            Assert.That(report.NeverDrawable, Is.Empty);
            Assert.That(report.Coverage, Is.Empty);
        }

        /// <summary>
        /// 索引里**没出现过**的 key ⇒ 未声明 ⇒ `AlwaysDrawable`。
        /// <para>⚠️ 本条**不能**证明「作者态空数组 `[]` 会怎样」——
        /// `TechniqueDrawIndex` 只按出现过的行建键,构造不出「已声明但空」的索引。
        /// 那个问题只能对**真产物**问,见
        /// <see cref="test_realData_emptyDeclaredRow_isUndeclaredNotBlocked"/>。</para>
        /// </summary>
        [Test]
        public void test_keyAbsentFromIndex_isUndeclaredNotBlocked()
        {
            var rows = new[] { Row(1, "DIS_A"), Row(2, "DIS_B") };
            var index = Index(Tb("DIS_B", 0));

            Assert.That(index.IsDeclared("DIS_A"), Is.False,
                "夹具前提:`TechniqueDrawIndex` 对未曾出现的 key 判未声明");

            var report = DrawSurfaceAudit.Compute(rows, index, playerLevel: 0);
            Assert.That(report.AlwaysDrawable, Is.EqualTo(new[] { "DIS_A" }),
                "未声明 ⇒ Undeclared 放行(与「声明了但空」是两件事)");
            Assert.That(report.Coverage[0].DiseaseKeys, Is.EqualTo(new[] { "DIS_B" }));
        }

        // ════════════════════════════════════════════════════════════════
        //  ③ 逐技法覆盖 == DrawableUnder
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// 不变式:`DrawableUnder({t}) == AlwaysDrawable ∪ Coverage(t)`。
        /// <para>为什么不写成「两者相等」:未声明的行在**任何**技法集下都放行,
        /// 它不在任何一门技法的覆盖里。相等写法会逼着把未声明行计进覆盖 ——
        /// 那样每门技法的价值都被虚高(实测不排除时 0 号技法「覆盖 6 行」,其中 2 行与它无关)。</para>
        /// </summary>
        [Test]
        public void test_coveragePlusAlwaysDrawable_equalsDrawableUnder()
        {
            var rows = new[] { Row(1, "DIS_A"), Row(2, "DIS_B"), Row(3, "DIS_C"), Row(4, "DIS_X") };
            var index = Index(Tb("DIS_A", 0), Tb("DIS_B", 0), Tb("DIS_C", 10));

            var report = DrawSurfaceAudit.Compute(rows, index, playerLevel: 0);
            Assert.That(report.AlwaysDrawable, Is.EqualTo(new[] { "DIS_X" }), "夹具前提:DIS_X 未声明");

            foreach (TechniqueCoverage c in report.Coverage)
            {
                var expected = new List<string>(report.AlwaysDrawable);
                expected.AddRange(c.DiseaseKeys);
                expected.Sort(StringComparer.Ordinal);

                var actual = new List<string>(report.DrawableUnder(new[] { c.TechniqueId }));
                actual.Sort(StringComparer.Ordinal);

                Assert.That(actual, Is.EqualTo(expected),
                    "技法 " + c.TechniqueId + ":DrawableUnder ≠ AlwaysDrawable ∪ Coverage —— 两条路径已分叉");
            }
        }

        /// <summary>逐技法覆盖的期望值(手算:0 号覆盖 A/B,10 号覆盖 C)。</summary>
        [Test]
        public void test_coverage_perTechnique()
        {
            var rows = new[] { Row(1, "DIS_A"), Row(2, "DIS_B"), Row(3, "DIS_C"), Row(4, "DIS_D") };
            var index = Index(Tb("DIS_A", 0), Tb("DIS_B", 0), Tb("DIS_C", 10));

            var report = DrawSurfaceAudit.Compute(rows, index, playerLevel: 0);

            Assert.That(report.Coverage.Count, Is.EqualTo(2), "技法 id 空间从数据取 ⇒ 只应有 0 与 10");
            Assert.That(report.Coverage[0].TechniqueId, Is.EqualTo(0));
            Assert.That(report.Coverage[0].DiseaseKeys, Is.EqualTo(new[] { "DIS_A", "DIS_B" }));
            Assert.That(report.Coverage[1].TechniqueId, Is.EqualTo(10));
            Assert.That(report.Coverage[1].DiseaseKeys, Is.EqualTo(new[] { "DIS_C" }));

            // DIS_D 未声明 ⇒ **不**计进任何技法覆盖(它不靠技法就能投放)
            Assert.That(report.AlwaysDrawable, Is.EqualTo(new[] { "DIS_D" }));
            foreach (TechniqueCoverage c in report.Coverage)
                Assert.That(c.DiseaseKeys, Has.No.Member("DIS_D"),
                    "未声明的行不得计进技法覆盖 —— 否则每门技法的价值虚高");
        }

        // ════════════════════════════════════════════════════════════════
        //  ④ 多技法取并集
        // ════════════════════════════════════════════════════════════════

        /// <summary>两门技法一起解锁 ⇒ 两个覆盖集的并集(不是交集)。</summary>
        [Test]
        public void test_drawableUnder_unionsTechniques()
        {
            var rows = new[] { Row(1, "DIS_A"), Row(2, "DIS_B"), Row(3, "DIS_C") };
            var index = Index(Tb("DIS_A", 0), Tb("DIS_B", 10), Tb("DIS_C", 11));

            var report = DrawSurfaceAudit.Compute(rows, index, playerLevel: 0);

            Assert.That(report.DrawableUnder(new[] { 0 }), Is.EqualTo(new[] { "DIS_A" }));
            Assert.That(report.DrawableUnder(new[] { 10 }), Is.EqualTo(new[] { "DIS_B" }));
            Assert.That(report.DrawableUnder(new[] { 0, 10 }), Is.EqualTo(new[] { "DIS_A", "DIS_B" }),
                "并集 —— 交集在这里会得到空");
            Assert.That(report.DrawableUnder(Array.Empty<int>()), Is.Empty,
                "一门都不会 ⇒ 三行全拒(三行都已声明)");
        }

        /// <summary>等级门:技法够但等级不足 ⇒ 仍拒(两道门不乘)。</summary>
        [Test]
        public void test_levelGate_blocksAfterTechniquePasses()
        {
            var rows = new[] { Row(1, "DIS_A", minSkill: 5) };
            var index = Index(Tb("DIS_A", 0));

            Assert.That(DrawSurfaceAudit.Compute(rows, index, playerLevel: 0)
                            .DrawableUnder(new[] { 0 }), Is.Empty, "等级不足 ⇒ 拒");
            Assert.That(DrawSurfaceAudit.Compute(rows, index, playerLevel: 5)
                            .DrawableUnder(new[] { 0 }), Is.EqualTo(new[] { "DIS_A" }), "等级够 ⇒ 放行");
        }

        // ════════════════════════════════════════════════════════════════
        //  ⑤ 拒绝未接线
        // ════════════════════════════════════════════════════════════════

        /// <summary>index 为 null ⇒ 抛(判据会恒返 NotWired,测出来的不是投放面)。</summary>
        [Test]
        public void test_nullIndex_throws()
        {
            var ex = Assert.Throws<ArgumentNullException>(
                () => DrawSurfaceAudit.Compute(new[] { Row(1, "DIS_A") }, null, 0));
            Assert.That(ex.ParamName, Is.EqualTo("index"));
        }

        /// <summary>负等级 ⇒ 抛。</summary>
        [Test]
        public void test_negativeLevel_throws()
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(
                () => DrawSurfaceAudit.Compute(new[] { Row(1, "DIS_A") }, Index(), -1));
            Assert.That(ex.ParamName, Is.EqualTo("playerLevel"));
        }

        /// <summary>`DrawableUnder(null)` ⇒ 抛(空集与 null 是两件事)。</summary>
        [Test]
        public void test_drawableUnder_null_throws()
        {
            var report = DrawSurfaceAudit.Compute(new[] { Row(1, "DIS_A") }, Index(), 0);
            Assert.Throws<ArgumentNullException>(() => report.DrawableUnder(null));
        }

        // ════════════════════════════════════════════════════════════════
        //  ⑥ 真数据:分类穷尽 + 快照
        // ════════════════════════════════════════════════════════════════

        private static DrawSurfaceReport RealReport()
        {
            DiseaseRegistryDataSet registry = DataCookedDisk.LoadDiseaseRegistry();
            DiseaseActionAxisDataSet axis = DiseaseActionAxisCookedCodec.Read(
                DataCookedDisk.ReadAll(AddressablesDataProvider.DiseaseActionAxisAddress));

            // 传入**全部** action id:否则「某技法零覆盖」与「该技法不存在」在报告里不可区分
            var actionIds = new List<int>(axis.Actions.Count);
            for (int i = 0; i < axis.Actions.Count; i++) actionIds.Add(axis.Actions[i].ActionId);

            return DrawSurfaceAudit.Compute(registry.Rows, new TechniqueDrawIndex(axis.TreatableBy),
                                            playerLevel: 0, actionIds);
        }

        /// <summary>
        /// **① 分类穷尽**(反空洞正控):每一行至少落在三类之一。
        /// <para>审计若静默丢行(比如 `TechniqueDrawIndex` 改了形状),这条当场红。</para>
        /// </summary>
        [Test]
        public void test_realData_everyRowIsClassified()
        {
            DrawSurfaceReport report = RealReport();

            var covered = new HashSet<string>(StringComparer.Ordinal);
            foreach (TechniqueCoverage c in report.Coverage)
                foreach (string key in c.DiseaseKeys) covered.Add(key);

            var classified = new HashSet<string>(StringComparer.Ordinal);
            foreach (string key in report.AlwaysDrawable) classified.Add(key);
            foreach (string key in report.NeverDrawable) classified.Add(key);
            foreach (string key in covered) classified.Add(key);

            Assert.That(report.TotalRows, Is.EqualTo(17),
                "注册表行数变了(10 病 + 7 伤)—— 须同批复核本夹具与 T11 证据件的表");

            Assert.That(classified.Count, Is.EqualTo(report.TotalRows),
                "有行没被任何一类覆盖 ⇒ 审计静默丢行(覆盖 " + classified.Count +
                " / 总 " + report.TotalRows + ")");
        }

        /// <summary>
        /// 真数据快照:**起步集**能投放几行。
        /// <para>⚠️ 这条钉的是**内容态**,不是门槛判断。它红了有两种可能:
        /// ① 有人改了 `treatable_by` 或起步集 —— 属正常内容变更,复核 R1 后更新本值;
        /// ② 判据被改坏了 —— 属缺陷。</para>
        /// </summary>
        [Test]
        public void test_realData_starterSetSnapshot()
        {
            DrawSurfaceReport report = RealReport();

            IReadOnlyList<string> starter =
                report.DrawableUnder(TechniqueUnlockBootstrap.StartingTechniques);

            TestContext.WriteLine("[T11] 起步集 = {" +
                string.Join(",", TechniqueUnlockBootstrap.StartingTechniques) + "} ⇒ 可投放 " +
                starter.Count + " 行:" + string.Join(", ", starter));

            // ⚠️ 6 = 4 件伤情(技法 0)+ 2 行**未声明**(`DIS_NEURASTHENIA` / `INJ_DRUG_HARM`,
            //    它们在空技法集下也放行)。**不含任何一种病** —— 10 个病种全要技法 2/3/10/11/12。
            Assert.That(starter.Count, Is.EqualTo(6),
                "开局面变了 —— 复核 R1(map §8)并更新 T11 证据件 §2 的表");
        }

        /// <summary>
        /// **实测裁定一处注释矛盾**:`DIS_NEURASTHENIA` 的 `treatable_by` 是 `[]`,
        /// 而它在运行期落进 `Undeclared` ⇒ **放行**。
        /// <para>`PatientDrawEligibility` ③ 说的就是这条(烘焙产物不保留空声明记录);
        /// ④ 那句「声明为空数组 ⇒ 恒假 ⇒ 拒收」**在本轮实测下不成立**。</para>
        /// <para>本条红了 ⇒ 要么内容改了(NEURASTHENIA 不再是空声明),要么产物形状变了
        /// (开始保留空声明)—— 两种都要回来复核 §5.1 的判据文字。</para>
        /// </summary>
        [Test]
        public void test_realData_emptyDeclaredRow_isUndeclaredNotBlocked()
        {
            DrawSurfaceReport report = RealReport();

            Assert.That(report.AlwaysDrawable, Does.Contain("DIS_NEURASTHENIA"),
                "NEURASTHENIA 的 `\"DIS_NEURASTHENIA\": []` 在产物里不留痕 ⇒ 判未声明 ⇒ 放行");
            Assert.That(report.NeverDrawable, Is.Empty,
                "今日没有任何一行是「全解锁仍拒」");
        }

        /// <summary>
        /// **内容缺口钉死**:技法 `1`(节奏型通气)覆盖 **0 行**。
        /// <para>它是起步集的一员(玩家开局就有),但没有任何病种把它列进 `treatable_by`
        /// ⇒ 这一门技法在投放面上**完全无效**。这是内容缺口,不是代码缺陷 ——
        /// 补一行 `treatable_by` 或给某个病种加它,本条的期望值才该改。</para>
        /// </summary>
        [Test]
        public void test_realData_techniqueOneCoversNothing()
        {
            DrawSurfaceReport report = RealReport();

            bool found = false;
            int rows = -1;
            foreach (TechniqueCoverage c in report.Coverage)
                if (c.TechniqueId == 1) { found = true; rows = c.RowCount; break; }

            Assert.IsTrue(found, "技法 1 必须出现在覆盖表里(传了 actionIds ⇒ 零覆盖也要列出)");
            Assert.That(rows, Is.EqualTo(0),
                "技法 1(节奏型通气)今日覆盖 0 行 —— 内容缺口,补数据后更新本值");
        }

        /// <summary>把整张表打进测试输出(证据件的复算来源)。</summary>
        [Test]
        public void test_realData_printFullTable()
        {
            DrawSurfaceReport report = RealReport();

            TestContext.WriteLine("[T11] 总行数 = " + report.TotalRows);
            TestContext.WriteLine("[T11] 空技法集即可投放 = " + report.AlwaysDrawable.Count +
                " [" + string.Join(", ", report.AlwaysDrawable) + "]");
            TestContext.WriteLine("[T11] 全解锁仍拒 = " + report.NeverDrawable.Count +
                " [" + string.Join(", ", report.NeverDrawable) + "]");
            foreach (TechniqueCoverage c in report.Coverage)
                TestContext.WriteLine("[T11] 技法 " + c.TechniqueId + " 覆盖 " + c.RowCount +
                    " 行:[" + string.Join(", ", c.DiseaseKeys) + "]");

            Assert.That(report.Coverage.Count, Is.GreaterThan(0), "逐技法覆盖为空 ⇒ 审计没读到数据");
        }
    }
}
