// diagnosis-system Story 004 —— F-8.3 阴性把握度与不泄漏不变量(required evidence)。
//
// 登记落点: tests/unit/diagnosis_system/README.md(AC→测映射)
// 真身落点: unity/Assets/Tests/EditMode/DiagnosisSystem/confidence_leak_test.cs
//
// 权威来源:
//   GDD design/gdd/diagnosis-system.md —— §F-8.3(C_neg · W_j 分支 · 把握度 clamp ·
//     构成排除阈值 · L*_j 导出)/ §F-8.4(无随机三理由)/ §F-8.5(不泄漏不变式)/
//     C-3(L*_j ≤ SKILL_CAP)/ C-4(两族正交)/ C-6(NEG_WEIGHT_j > 0)/ C-7(L*_j ≥ tier_named_j)/
//     AC-8-10/11/12/14/15/16/17/18 · AC-8-F1/F2/F4/F5 · AC-8-50
//   ADR-012(跨平台逐位一致 = 三格矩阵判据)· ADR-006(舍入)· ADR-005(确定性纪律)
//   Story: production/epics/diagnosis-system/story-004-negative-confidence-and-no-leak.md
//
// ⚠️ 承重面 = **阴性族定表(编辑期)+ 求值器(运行期)的存在与形状**,以及**两族正交**:
//   ① C-3 = 会**静默坏掉**的约束 ⇒ 逐条算 L*_j、逐条报警(不报错,只是某个阴性永远不算数);
//   ② C-4 两族正交 = 改一族**不触**另一族(分表分文件,测试物化而非代码审查);
//   ③ C-7 = 说不出的话谈不上算数(L*_j ≥ tier_named_j);
//   ④ F-8.5 不泄漏 = 把握度**只是 (Skill, sign_id) 的函数** —— 实参表无 Sign_j;
//   ⑤ F-8.4 无随机 = 同输入 N ≥ 10⁴ 次逐位相同 + 程序集零 PRNG 调用点。
//
// ⚠️ 驱动**生产**接缝:DiagnosisNegativeConfidenceBaker.BakeFromRepo(菜单与测试共用同一台机器)、
//   DiagnosisNegativeConfidenceBinderProbe.Bake/TableOf(夹具直喂)、DiagnosisReadFloor* 同理
//   —— 不是测试侧的重实现。
//
// ⚠️ **数值轮占位(承 story-002 S-9 / story-004 Note 7)**:R-8.2 阴性条目的
//   `neg_weight = "1"` 与 `assets/data/diagnosis_negative_confidence.json` 的五个旋钮
//   均为**合成值**(GDD 原值 `*待裁*`,归用户数值轮)。本文件以合成旋钮判**形状**
//   (存在性 / 序关系 / 不等式 / 正交性),**不把 `1` 当终值**;数值轮落定后须重签相关向量。
//
// ═════════════════════════════════════════════════════════════════════════
// ⚠️ NOT-RUN 显式登记(禁借绿 —— story Test Evidence 要求):
//   · **AC-8-F5(表现层 float 跨平台逐位一致)NOT-RUN-BLOCKED-BY-ADR-012** —— 三格矩阵
//     (Linux-x64-Mono / Linux-x64-IL2CPP / Linux-ARM64-IL2CPP)未实跑 ⇒ 本文件只证
//     **Mono 侧自洽**(同进程双跑 + 定表哈希自洽);跨平台半边待矩阵,禁借绿。
//   · **AC-8-16 的「跨两个独立进程」半边** —— EditMode 无法起独立进程;本文件证
//     **同进程** N ≥ 10⁴ 次逐位相同 + 静态守门零 PRNG(跨进程确定性由 ADR-005 结构性保证,
//     其判据归 AC-8-3 / ADR-012 矩阵)。
//   · **AC-8-13(UC-8-F1 端到端)归 [I] 集成 + D-8-4** —— 依赖 9 侧 `Project(Sign_j)` 未落地
//     (disease-simulation story 004),本 story Out of Scope;公式级 AC-8-F1 在此判。
// ═════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using DaYiJingCheng.EditorTools.Bake;
using DaYiJingCheng.EditorTools.Gates;
using DaYiJingCheng.Gameplay.Presentation.Diagnosis;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.Contracts.SkillSystem;
using NUnit.Framework;
using SignChannel = DaYiJingCheng.Gameplay.Presentation.Diagnosis.SignChannel;

namespace DaYiJingCheng.Tests.DiagnosisSystem
{
    [TestFixture]
    internal sealed class ConfidenceLeakTest
    {
        // ═══════════════════════════════════════════════════════════
        //  仓根 / 夹具路径(承 read_floor_slots_test 同款回溯)
        // ═══════════════════════════════════════════════════════════

        private static string RepoRoot => ComputeRepoRoot();

        private static string ComputeRepoRoot([CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile) ?? ".",
                "..", "..", "..", "..", ".."));

        private static string FixtureDir =>
            Path.Combine(RepoRoot, "tests", "unit", "diagnosis_system", "fixtures");

        private static string ReadFixture(string name)
        {
            string path = Path.Combine(FixtureDir, name);
            Assert.IsTrue(File.Exists(path), $"夹具不存在:{path}");
            return File.ReadAllText(path);
        }

        private static string NegSeedJson() => File.ReadAllText(Path.Combine(
            RepoRoot, "assets", "data", DiagnosisNegativeConfidenceBaker.NegativeConfidenceFileName));

        private static string ReadFloorSeedJson() => File.ReadAllText(Path.Combine(
            RepoRoot, "assets", "data", DiagnosisReadFloorBaker.ReadFloorFileName));

        private static string SignsSeedJson() => File.ReadAllText(Path.Combine(
            RepoRoot, "assets", "data", DiagnosisSignBaker.SignsFileName));

        // ── 三条生产链(夹具 / 种子 → 定表;同一台机器)──

        private static DiagnosisNegativeConfidenceTable LoadNegTable(string json)
        {
            byte[] cooked = DiagnosisNegativeConfidenceBinderProbe.Bake(json);
            return DiagnosisNegativeConfidenceCookedCodec.Read(cooked).Table;
        }

        private static DiagnosisNegativeConfidenceTable NegSeedTable()
            => DiagnosisNegativeConfidenceCookedCodec
                .Read(DiagnosisNegativeConfidenceBaker.BakeFromRepo(RepoRoot).Cooked).Table;

        private static DiagnosisReadFloorTable LoadReadFloor(string json)
            => DiagnosisReadFloorCookedCodec
                .Read(DiagnosisReadFloorBinderProbe.Bake(json)).Table;

        private static DiagnosisReadFloorTable ReadFloorSeed()
            => DiagnosisReadFloorCookedCodec
                .Read(DiagnosisReadFloorBaker.BakeFromRepo(RepoRoot).Cooked).Table;

        private static List<SignLexemeRow> SeedRows()
            => DiagnosisSignCookedCodec.Read(DiagnosisSignBinderProbe.Bake(SignsSeedJson())).Rows;

        /// <summary>违例夹具驱动(端到端黑盒;承 sign_table_test BakeFails 同款):
        /// 必须抛 <see cref="BakeValidationException"/>、**恰一条错误**(排他)、错误文本触底规则 tag。</summary>
        private static BakeValidationException NegBakeFails(string json, params string[] tags)
        {
            var ex = Assert.Throws<BakeValidationException>(
                () => DiagnosisNegativeConfidenceBinderProbe.Bake(json),
                "违例源须聚合硬失败(绝不静默烘出)");
            Assert.AreEqual(1, ex.Errors.Count,
                "单违例夹具须恰一条错误(排他)—— 夹具串味 / 多错折叠即此抓\n实得:\n" +
                string.Join("\n", ex.Errors));
            string joined = string.Join("\n", ex.Errors);
            foreach (string tag in tags)
                Assert.IsTrue(joined.Contains(tag),
                    $"错误须触底到规则「{tag}」—— 实得:\n{joined}");
            return ex;
        }

        /// <summary>造一条最小词条行(极性 / 权重可控)。</summary>
        private static SignLexemeRow Row(
            string id, SignPolarity polarity, int tier, long? negWeightRaw = null)
            => new SignLexemeRow(
                id, new[] { "粗", "中", "细" }, SignChannel.Touch,
                new[] { RevealMethod.Palpation }, tier, polarity, negWeightRaw);

        /// <summary>阴性权重占位(raw Q16.16 的 1.0 —— 与种子 `neg_weight = "1"` 同值)。</summary>
        private const long WeightOneRaw = 65536L;

        // ═══════════════════════════════════════════════════════════
        //  AC-8-10 · C-3 阴性不得是死内容(逐条算 L*_j,逐条报警)
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac810_seedNegatives_noDeadContent()
        {
            DiagnosisNegativeConfidenceTable table = NegSeedTable();
            List<SignLexemeRow> negatives = SeedRows()
                .Where(r => r.Polarity == SignPolarity.Negative).ToList();

            // 反空转:扫描面须非空(空过滤跑假绿即此抓)
            Assert.IsNotEmpty(negatives, "R-8.2 阴性条目集不得为空(C-3 的扫描面)");
            Assert.AreEqual(4, negatives.Count, "P0 阴性组 = 4 行(R-8.2)");

            var dead = new List<string>();
            foreach (SignLexemeRow row in negatives)
            {
                int lStar = DiagnosisNegativeConfidenceEvaluator.ExclusionLevel(table, row);
                // C-3 字面 = `L*_j ≤ SKILL_CAP`;哨兵 NeverExcludes(-1) 是该式的唯一违反形态
                // (ExclusionLevel 只可能返回哨兵或 [0, SkillCap] 内的值 ⇒ 无 `> SkillCap` 分支)。
                if (lStar == DiagnosisNegativeConfidenceEvaluator.NeverExcludes)
                    dead.Add($"{row.SignId}(L*=∅)");
                else
                    Assert.LessOrEqual(lStar, table.SkillCap,
                        $"{row.SignId}:L*={lStar} 须 ≤ SKILL_CAP={table.SkillCap}(C-3)");
            }
            Assert.IsEmpty(dead,
                "C-3:阴性体征是**死内容** —— 满技能也说不响「没有」,永远不构成排除。" +
                "加阴性体征时必须算一次 L*_j。点名:\n" + string.Join("\n", dead));
        }

        [Test]
        public void test_ac810_deadContentAlarm_namesRow()
        {
            // 合成「死内容」定表:C_neg ≡ 0.5 且 EXCLUDE_CONF_MIN = 1.0 ⇒ 把握度 ≤ 0.5 < 1.0
            // ⇒ **每条阴性**都永不构成排除。证明报警面真实(不是恒绿),且点名到条目。
            var curve = new float[61];
            for (int i = 0; i < curve.Length; i++) curve[i] = 0.5f;
            var deadTable = new DiagnosisNegativeConfidenceTable(
                60, 32768L, 32768L, 65536L, 32768L, 65536L /*EXCLUDE_CONF_MIN = 1.0*/, curve);

            var deadRows = new List<SignLexemeRow>();
            foreach (SignLexemeRow row in SeedRows().Where(r => r.Polarity == SignPolarity.Negative))
                if (DiagnosisNegativeConfidenceEvaluator.ExclusionLevel(deadTable, row) ==
                    DiagnosisNegativeConfidenceEvaluator.NeverExcludes)
                    deadRows.Add(row);

            Assert.IsNotEmpty(deadRows,
                "阈值 = 1.0 而 C_neg ≤ 0.5 ⇒ L* 须为空集(报警面真实 —— 否则 AC-8-10 恒绿)");
            Assert.AreEqual(4, deadRows.Count, "四条阴性条目全部落入死内容");
            // 报警须**点名** —— 与 AC-8-10 同一条判据的失败路径(证明它承重,非恒真)
            Assert.IsTrue(deadRows.All(r => !string.IsNullOrEmpty(r.SignId)),
                "报警输出须携带 sign_id(点名到条目)");
        }

        // ═══════════════════════════════════════════════════════════
        //  AC-8-11 · C-7 L*_j ≥ tier_named_j(说不出的话谈不上算数)
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac811_negativeGroup_lStarEqualsTierNamed()
        {
            DiagnosisNegativeConfidenceTable table = NegSeedTable();
            var violations = new List<string>();
            foreach (SignLexemeRow row in SeedRows().Where(r => r.Polarity == SignPolarity.Negative))
            {
                int lStar = DiagnosisNegativeConfidenceEvaluator.ExclusionLevel(table, row);
                if (lStar < row.TierNamed)
                    violations.Add(
                        $"{row.SignId}:L*={lStar} < tier_named={row.TierNamed}" +
                        "(还叫不出名字,却已拿它当排除证据)");
            }
            Assert.IsEmpty(violations, "C-7 违反:\n" + string.Join("\n", violations));

            // 阴性组 tier_named = Lv20 与 AC-8-F1 的 L* ∈ (15,20] 联立 ⇒ L* = 20(四行同锚)
            foreach (SignLexemeRow row in SeedRows().Where(r => r.Polarity == SignPolarity.Negative))
            {
                Assert.AreEqual(20, row.TierNamed, $"阴性组 tier_named 须 = Lv20 —— {row.SignId}");
                Assert.AreEqual(20, DiagnosisNegativeConfidenceEvaluator.ExclusionLevel(table, row),
                    $"阴性组全四行同锚 L* = 20 —— {row.SignId}(C-7 ∧ AC-8-F1 联立)");
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  AC-8-12 / AC-8-F2 · C-4 两族正交(双向隔离)
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac812_readGamma_doesNotMoveNegativeFamily()
        {
            // AC-8-12 (a) 字面形:仅改 READ_GAMMA ⇒ 全部阴性把握度**逐位不变**。
            // 反空转 = **同一次改动的另一半**:同一对表下**阳性可读性确实变了**
            // (若阳性侧也不变,说明这对夹具根本没改到东西 ⇒ 本测恒真)。
            DiagnosisReadFloorTable rfA = LoadReadFloor(ReadFixture("read_floor_strict_monotonic.json"));
            DiagnosisReadFloorTable rfB = LoadReadFloor(ReadFixture("read_floor_half_gamma.json"));

            List<SignLexemeRow> rows = SeedRows();
            var pos = rows.First(r => r.Polarity == SignPolarity.Positive);
            var neg = rows.First(r => r.Polarity == SignPolarity.Negative);

            // 前置 ①:这对夹具**确实**改了 READ_GAMMA(否则下面两条断言恒真)
            Assert.AreNotEqual(rfA.ReadGammaRaw, rfB.ReadGammaRaw,
                "两夹具的 READ_GAMMA 须确已分叉(否则本测恒真 —— 假绿)");

            // 前置 ②:该旋钮**确实到达阳性族**(可读性随它变)—— 这是本测的反空转半边
            int posDiff = 0;
            for (int skill = 0; skill <= rfA.SkillCap; skill++)
                if (DiagnosisReadFloorEvaluator.Evaluate(rfA, pos, skill, 0.3f).IsReadable !=
                    DiagnosisReadFloorEvaluator.Evaluate(rfB, pos, skill, 0.3f).IsReadable)
                    posDiff++;
            Assert.Greater(posDiff, 0,
                "READ_GAMMA 须**确实**驱动阳性可读性(否则本测是空转)");

            // 主张:阴性族**完全不响应** READ_GAMMA。⚠️ 字面的「换掉 READ_GAMMA 重算阴性」
            // **按构造不可表达**(阴性烘焙的唯一入参是阴性源文本 —— 那正是 C-4 的物化面)。
            // 故此处证两条**可证伪**的等价命题:
            //   ① 阴性烘焙接缝的实参表**无**读地板入口(改 READ_GAMMA 物理上碰不到它);
            //   ② 阴性族对**自己的**旋钮**必须**响应(否则「正交」退化为「两族皆死」)。
            foreach (MethodInfo m in typeof(DiagnosisNegativeConfidenceBinderProbe).GetMethods(
                BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
                foreach (ParameterInfo p in m.GetParameters())
                    Assert.AreNotEqual(typeof(DiagnosisReadFloorTable), p.ParameterType,
                        $"{m.Name}({p.Name}):阴性烘焙接缝不得有读地板入口(C-4)");

            DiagnosisNegativeConfidenceTable negTable = NegSeedTable();
            DiagnosisNegativeConfidenceTable negAlt =
                LoadNegTable(ReadFixture("neg_conf_strict_monotonic.json"));
            int negDiff = 0;
            for (int skill = 0; skill <= negTable.SkillCap; skill++)
                if (BitConverter.SingleToInt32Bits(
                        DiagnosisNegativeConfidenceEvaluator.Confidence(negTable, neg, skill)) !=
                    BitConverter.SingleToInt32Bits(
                        DiagnosisNegativeConfidenceEvaluator.Confidence(negAlt, neg, skill)))
                    negDiff++;
            Assert.Greater(negDiff, 0,
                "阴性族须响应**自己的** NEG_GAMMA(否则 C-4 退化为两族皆死)");
        }

        [Test]
        public void test_ac812_negGamma_doesNotMovePositiveFamily()
        {
            // AC-8-12 (b) 字面形:仅改 NEG_GAMMA ⇒ 全部阳性可读性**逐位不变**。
            DiagnosisNegativeConfidenceTable negA = NegSeedTable();
            DiagnosisNegativeConfidenceTable negB = LoadNegTable(ReadFixture("neg_conf_half_gamma.json"));
            Assert.AreNotEqual(negA.NegGammaRaw, negB.NegGammaRaw,
                "两夹具的 NEG_GAMMA 须确已分叉(否则本测恒真 —— 假绿)");

            // 前置:NEG_GAMMA **确实**驱动阴性族(把握度随它变)—— 反空转半边
            var neg = SeedRows().First(r => r.Polarity == SignPolarity.Negative);
            int negDiff = 0;
            for (int skill = 0; skill <= negA.SkillCap; skill++)
                if (BitConverter.SingleToInt32Bits(
                        DiagnosisNegativeConfidenceEvaluator.Confidence(negA, neg, skill)) !=
                    BitConverter.SingleToInt32Bits(
                        DiagnosisNegativeConfidenceEvaluator.Confidence(negB, neg, skill)))
                    negDiff++;
            Assert.Greater(negDiff, 0, "NEG_GAMMA 须**确实**驱动阴性把握度(否则本测是空转)");

            // 主张:阳性族**完全不响应** NEG_GAMMA。⚠️ 字面的「换掉 NEG_GAMMA 重算阳性」
            // **按构造不可表达**(阳性烘焙的唯一入参是阳性源文本 —— 那正是 C-4 的物化面)。
            // 故此处证两条**可证伪**的等价命题(与 (a) 半镜像):
            //   ① 阳性烘焙接缝的实参表**无**阴性定表入口(改 NEG_GAMMA 物理上碰不到它);
            //   ② 阳性族**确实**响应自己的旋钮(否则「正交」退化为「两族皆死」)。
            foreach (MethodInfo m in typeof(DiagnosisReadFloorBinderProbe).GetMethods(
                BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
                foreach (ParameterInfo p in m.GetParameters())
                    Assert.AreNotEqual(typeof(DiagnosisNegativeConfidenceTable), p.ParameterType,
                        $"{m.Name}({p.Name}):阳性烘焙接缝不得有阴性定表入口(C-4)");

            DiagnosisReadFloorTable rfA = LoadReadFloor(ReadFixture("read_floor_strict_monotonic.json"));
            DiagnosisReadFloorTable rfB = LoadReadFloor(ReadFixture("read_floor_half_gamma.json"));
            Assert.AreNotEqual(rfA.ReadGammaRaw, rfB.ReadGammaRaw,
                "两读地板夹具的 READ_GAMMA 须确已分叉(否则下一断言恒真)");
            var pos = SeedRows().First(r => r.Polarity == SignPolarity.Positive);
            int posDiff = 0;
            for (int skill = 0; skill <= rfA.SkillCap; skill++)
                if (DiagnosisReadFloorEvaluator.Evaluate(rfA, pos, skill, 0.3f).IsReadable !=
                    DiagnosisReadFloorEvaluator.Evaluate(rfB, pos, skill, 0.3f).IsReadable)
                    posDiff++;
            Assert.Greater(posDiff, 0,
                "阳性族须响应**自己的** READ_GAMMA(否则 C-4 退化为两族皆死)");
        }

        [Test]
        public void test_ac812_noCrossFamilyParameter()
        {
            // C-4 正交的**可证伪**形态 = 两族求值器的实参表**互不相认**
            // (谁若把 `DiagnosisReadFloorTable` 塞进阴性求值、或反之,此测即红)。
            AssertNoParamOfType(typeof(DiagnosisNegativeConfidenceEvaluator),
                typeof(DiagnosisReadFloorTable), "阴性求值器");
            AssertNoParamOfType(typeof(DiagnosisReadFloorEvaluator),
                typeof(DiagnosisNegativeConfidenceTable), "阳性求值器");

            // 表类型侧同款:阴性表不得有读地板语义字段,反之亦然。
            AssertNoFieldToken(typeof(DiagnosisNegativeConfidenceTable),
                new[] { "readfloor", "baseread", "readgamma", "slot" }, "阴性表");
            AssertNoFieldToken(typeof(DiagnosisReadFloorTable),
                new[] { "neg", "exclude", "weight" }, "读地板表");
        }

        [Test]
        public void test_ac812_separateFiles_crossFeedRejected()
        {
            // 分表分文件物化:阴性源喂阳性绑定器 / 阳性源喂阴性绑定器 —— **两个方向都硬失败**。
            // 这是「改一族不触另一族」的端到端判据(不是代码审查):两族键集若重叠,此测即红。
            Assert.Throws<BakeValidationException>(
                () => DiagnosisReadFloorBinderProbe.Bake(NegSeedJson()),
                "阴性源不得被读地板绑定器接受(分表分文件)");
            Assert.Throws<BakeValidationException>(
                () => DiagnosisNegativeConfidenceBinderProbe.Bake(ReadFloorSeedJson()),
                "读地板源不得被阴性绑定器接受(分表分文件)");

            // 反向自证:各自的真源都能过(否则上面两条恒真 —— 假绿)。
            Assert.DoesNotThrow(() => DiagnosisReadFloorBinderProbe.Bake(ReadFloorSeedJson()));
            Assert.DoesNotThrow(() => DiagnosisNegativeConfidenceBinderProbe.Bake(NegSeedJson()));
        }

        // ═══════════════════════════════════════════════════════════
        //  AC-8-14 · 多阴性证据不合并(无聚合量 / 无置信度条)
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac814_multiEvidence_notMerged()
        {
            DiagnosisNegativeConfidenceTable table = NegSeedTable();
            // 三条:两条达标 + 第三条不够格(权重极小 ⇒ L* 极大)
            SignLexemeRow strongA = Row("n_strong_a", SignPolarity.Negative, 20, WeightOneRaw);
            SignLexemeRow strongB = Row("n_strong_b", SignPolarity.Negative, 20, WeightOneRaw);
            SignLexemeRow weak = Row("n_weak", SignPolarity.Negative, 20, 1L);  // 1/65536 ≈ 0

            int lA = DiagnosisNegativeConfidenceEvaluator.ExclusionLevel(table, strongA);
            int lB = DiagnosisNegativeConfidenceEvaluator.ExclusionLevel(table, strongB);
            int lW = DiagnosisNegativeConfidenceEvaluator.ExclusionLevel(table, weak);

            Assert.AreEqual(20, lA, "强证据 A 须在 Lv20 达标");
            Assert.AreEqual(20, lB, "强证据 B 须在 Lv20 达标");
            Assert.AreEqual(DiagnosisNegativeConfidenceEvaluator.NeverExcludes, lW,
                "弱证据在权重 ≈ 0 时不得达标(合成夹具证明第三条独立)");

            // ── 调低第三条 ⇒ 前两条判定**逐位不变**(AC-8-14 字面:不相乘不相加)──
            // 全表逐档取把握度 / 排除位,作为「调低前」快照;再用一条权重更低的第三条重算。
            var before = new List<(int conf, bool excl)>();
            for (int skill = 0; skill <= table.SkillCap; skill++)
                before.Add((
                    BitConverter.SingleToInt32Bits(
                        DiagnosisNegativeConfidenceEvaluator.Confidence(table, strongA, skill)),
                    DiagnosisNegativeConfidenceEvaluator.ConstitutesExclusion(table, strongA, skill)));

            // 「调低第三条」:把它压到 0 权重(若系统存在聚合量,前两条必然随之变)
            SignLexemeRow weaker = Row("n_weak", SignPolarity.Negative, 20, 0L);
            Assert.AreEqual(DiagnosisNegativeConfidenceEvaluator.NeverExcludes,
                DiagnosisNegativeConfidenceEvaluator.ExclusionLevel(table, weaker),
                "第三条压到 0 权重后仍不得达标");

            for (int skill = 0; skill <= table.SkillCap; skill++)
            {
                Assert.AreEqual(before[skill].conf,
                    BitConverter.SingleToInt32Bits(
                        DiagnosisNegativeConfidenceEvaluator.Confidence(table, strongA, skill)),
                    $"档 {skill}:调低第三条后强证据 A 的把握度变了 ⇒ 存在聚合量(AC-8-14 违)");
                Assert.AreEqual(before[skill].excl,
                    DiagnosisNegativeConfidenceEvaluator.ConstitutesExclusion(table, strongA, skill),
                    $"档 {skill}:调低第三条后强证据 A 的排除位变了 ⇒ 存在聚合量");
                Assert.AreEqual(20, DiagnosisNegativeConfidenceEvaluator.ExclusionLevel(table, strongB),
                    $"档 {skill}:强证据 B 的 L* 不得随第三条变");
            }

            // ⚠️ 承重面在**接口形状**上:上面两条比对按构造恒等(三条行是彼此独立的入参),
            //    真正可证伪的是「不存在吃多行的聚合入口」—— 有则此测红。
            foreach (MethodInfo m in typeof(DiagnosisNegativeConfidenceEvaluator).GetMethods(
                BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
                foreach (ParameterInfo p in m.GetParameters())
                {
                    Type pt = p.ParameterType;
                    Assert.IsFalse(pt.IsArray || (pt != typeof(string) &&
                            typeof(System.Collections.IEnumerable).IsAssignableFrom(pt)),
                        $"{m.Name}({p.Name}:{pt.Name}):排除判定不得有聚合多行的入口(AC-8-14)");
                }
        }

        [Test]
        public void test_ac814_noAggregateConfidenceType()
        {
            var asm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Gameplay.Presentation");
            Assert.That(asm, Is.Not.Null, "Gameplay.Presentation 装配须已加载");
            Type[] all;
            try { all = asm.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { all = ex.Types.Where(t => t != null).ToArray(); }

            var tokens = new[] { "confidence", "aggregate", "combined", "overall", "confidencebar" };
            // ⚠️ 类型名只查**聚合** token —— 裸 `confidence` 是阴性族自己的名词
            //    (`DiagnosisNegativeConfidenceTable` 等合法类型名即含它),不能当禁词。
            var typeTokens = new[] { "aggregate", "combined", "overall", "confidencebar" };
            const BindingFlags fb = BindingFlags.DeclaredOnly | BindingFlags.Public |
                                    BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            var hits = new List<string>();
            int scannedTypes = 0;
            foreach (Type t in all)
            {
                if (t == null || !InPrefix(t)) continue;
                scannedTypes++;
                // 扫描面含**类型名 / 属性名**,不只字段名 —— 名为 `ConfidenceBar` 而无匹配字段的
                // 类型同样违 AC-8-14(只扫字段会漏判)。
                string tnorm = t.Name.Replace("_", "").ToLowerInvariant();
                foreach (string tok in typeTokens)
                    if (tnorm.Contains(tok))
                        hits.Add($"{t.FullName} ← 类型名「{tok}」");
                foreach (FieldInfo f in t.GetFields(fb))
                {
                    string norm = f.Name.Replace("_", "").ToLowerInvariant();
                    foreach (string tok in tokens)
                        if (norm.Contains(tok))
                            hits.Add($"{t.FullName}.{f.Name} ← 「{tok}」");
                }
                foreach (PropertyInfo p in t.GetProperties(fb))
                {
                    string norm = p.Name.Replace("_", "").ToLowerInvariant();
                    foreach (string tok in tokens)
                        if (norm.Contains(tok))
                            hits.Add($"{t.FullName}.{p.Name} ← 属性「{tok}」");
                }
            }
            // 反空转:扫描面须非空 —— 命名空间漂移 / 装配未加载时 hits 恒空即假绿
            Assert.Greater(scannedTypes, 0,
                "扫描面为空(Gameplay.Presentation 前缀 ns 未命中)⇒ 本测恒绿,AC-8-14 无证据");
            Assert.IsEmpty(hits,
                "构成排除是**阈值判定不是连续值** —— 8 不得存在任何聚合置信度字段(置信度条):\n" +
                string.Join("\n", hits));
        }

        // ═══════════════════════════════════════════════════════════
        //  把握度 clamp(F-8.3 · Implementation Note 3)
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_confidence_clampedToUnitRange()
        {
            // ⚠️ 承重面:C-6 只钉 `neg_weight > 0`,**无上界** ⇒ 合法的 `W_j > 1` 可让
            //    `C_neg × W_j > 1` —— 此时 clamp 是唯一防线(MUT:去 clamp ⇒ 本测须红)。
            DiagnosisNegativeConfidenceTable table = NegSeedTable();
            Assert.AreEqual(1.0f, table.CurveAt(60), 1e-6f, "前置:C_neg(SKILL_CAP) = 1");

            // 权重 4.0(合法:> 0)⇒ 乘积 4.0 须被钳到 1.0
            SignLexemeRow heavy = Row("n_heavy", SignPolarity.Negative, 20, 4L * WeightOneRaw);
            float c = DiagnosisNegativeConfidenceEvaluator.Confidence(table, heavy, 60);
            Assert.AreEqual(1.0f, c, "把握度须被钳到 1(上界)—— 去 clamp 即此抓");
            Assert.LessOrEqual(c, 1.0f, "把握度恒 ≤ 1");

            // 负权重(raw < 0;运行期入参未过构建期门)⇒ 须钳到 0,不得为负
            SignLexemeRow negative = Row("n_negative", SignPolarity.Negative, 20, -2L * WeightOneRaw);
            Assert.AreEqual(0.0f,
                DiagnosisNegativeConfidenceEvaluator.Confidence(table, negative, 60),
                "把握度须被钳到 0(下界)—— 负值即此抓");

            // 全档扫描:任何档位的把握度都落在 [0,1]
            foreach (SignLexemeRow row in new[] { heavy, negative })
                for (int skill = 0; skill <= table.SkillCap; skill++)
                {
                    float v = DiagnosisNegativeConfidenceEvaluator.Confidence(table, row, skill);
                    Assert.GreaterOrEqual(v, 0f, $"{row.SignId}@{skill}:把握度不得 < 0");
                    Assert.LessOrEqual(v, 1f, $"{row.SignId}@{skill}:把握度不得 > 1");
                }
        }

        // ═══════════════════════════════════════════════════════════
        //  Q16 常量锚定(D-FIX 禁 Fix.OneRaw ⇒ 本地落值的正确性须可证伪)
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_q16one_matchesFixCanonical()
        {
            // ⚠️ 前缀 ns 内 D-FIX 源层谓词禁 `Fix.OneRaw` ⇒ `DiagnosisNegativeConfidenceTable`
            //    本地落 `Q16One = 65536f`。本地值**不得**是孤儿魔法数:此处把它逐位锚到
            //    `Fix` 的正典(`ToFloat()` 与 `FractionalBits`)—— 改错一位即红。
            Assert.AreEqual(16, Fix.FractionalBits,
                "Q16.16 的小数位须为 16(本地 Q16One 的语义前提)");
            Assert.AreEqual((float)Fix.OneRaw, 65536f,
                "本地 Q16One 须等于 Fix.OneRaw 的 float 形(否则 raw→float 全族偏位)");
            // 逐位等价:raw=1 的换算 == Fix 的 ToFloat()(唯一换算出口 RawToFloat 的锚)
            foreach (long raw in new[] { 0L, 1L, 32768L, 65536L, -65536L, 4L * 65536L + 1L })
                Assert.AreEqual(
                    BitConverter.SingleToInt32Bits(new Fix(raw).ToFloat()),
                    BitConverter.SingleToInt32Bits(DiagnosisNegativeConfidenceTable.RawToFloat(raw)),
                    $"raw={raw}:RawToFloat 须与 Fix.ToFloat() 逐位相同");
        }

        // ═══════════════════════════════════════════════════════════
        //  退化表消费 = 硬失败(禁静默 0 —— C-3 死内容报警不得被伪装)
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_curveat_unloadedTable_throws()
        {
            // `default(DiagnosisNegativeConfidenceTable)` 若静默返回 0f,则 C_neg ≡ 0
            // ⇒ 每条阴性都「永不构成排除」,死内容报警被伪装成正常值(E-13 口径所禁)。
            var unloaded = default(DiagnosisNegativeConfidenceTable);
            Assert.Throws<InvalidOperationException>(() => unloaded.CurveAt(0),
                "未装载的定表不得被消费 —— 须硬失败(E-13),不得静默返回 0");
        }

        // ═══════════════════════════════════════════════════════════
        //  AC-8-15 · neg_weight 只影响排除路径(阳性误填无副作用)
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac815_positiveWeight_hasNoEffectOnReadabilityOrExclusion()
        {
            DiagnosisNegativeConfidenceTable table = NegSeedTable();
            DiagnosisReadFloorTable rf = ReadFloorSeed();

            // 阳性行**误填** neg_weight(绕过构建期校验 —— 那半边归 story 002)
            SignLexemeRow bare = Row("pos_bare", SignPolarity.Positive, 20, null);
            SignLexemeRow filled = Row("pos_filled", SignPolarity.Positive, 20, 4L * WeightOneRaw);

            // ① 可读性只走 F-8.1,与 neg_weight 无关
            for (int skill = 0; skill <= rf.SkillCap; skill++)
            {
                var a = DiagnosisReadFloorEvaluator.Evaluate(rf, bare, skill, 1f);
                var b = DiagnosisReadFloorEvaluator.Evaluate(rf, filled, skill, 1f);
                Assert.AreEqual(a.IsReadable, b.IsReadable, $"{skill}:阳性可读性不得受 neg_weight 影响");
                Assert.AreEqual(a.State, b.State, $"{skill}:阳性四态不得受 neg_weight 影响");
            }

            // ② 不进任何排除判定(阳性恒 false)
            for (int skill = 0; skill <= table.SkillCap; skill++)
            {
                Assert.IsFalse(
                    DiagnosisNegativeConfidenceEvaluator.ConstitutesExclusion(table, bare, skill));
                Assert.IsFalse(
                    DiagnosisNegativeConfidenceEvaluator.ConstitutesExclusion(table, filled, skill));
            }

            // ③ 把握度走兜底权重(不是行内误填值)—— 证明误填不进公式
            float confBare = DiagnosisNegativeConfidenceEvaluator.Confidence(table, bare, 60);
            float confFilled = DiagnosisNegativeConfidenceEvaluator.Confidence(table, filled, 60);
            Assert.AreEqual(confBare, confFilled,
                "阳性行内误填的 neg_weight 不得进入把握度(须走 NEG_WEIGHT_FALLBACK)");
            Assert.AreEqual(table.FallbackWeight, confBare,
                "阳性把握度 = C_neg(SKILL_CAP) × NEG_WEIGHT_FALLBACK");
        }

        [Test]
        public void test_ac815_positivePolarityGate_isLoadBearing()
        {
            // ⚠️ 上面那条用的是种子兜底权重 1/2 —— 它使阳性把握度(≤0.5)**恰好低于**
            //    阈值 0.66,于是「阳性恒不构成排除」被阈值**顺带**满足,极性门被掩盖
            //    (MUT:去极性门 ⇒ 种子下仍绿)。本测用**高兜底**夹具(合法:>0 无上界)
            //    让阳性把握度达到 1.0 —— 此时只有极性门能挡住它。
            DiagnosisNegativeConfidenceTable hi =
                LoadNegTable(ReadFixture("neg_conf_high_fallback.json"));
            Assert.AreEqual(1.0f, hi.FallbackWeight, 1e-6f,
                "前置:高兜底夹具的 fallback 须 = 1.0(否则本测仍被阈值掩盖)");

            SignLexemeRow pos = SeedRows().First(r => r.Polarity == SignPolarity.Positive);
            float conf = DiagnosisNegativeConfidenceEvaluator.Confidence(hi, pos, 60);
            Assert.GreaterOrEqual(conf, hi.ExcludeMin,
                "前置:阳性把握度须**已达阈值**(否则极性门不是唯一防线)");

            // 主张:即便把握度达标,阳性也**不得**构成排除(极性门是唯一防线)
            Assert.IsFalse(
                DiagnosisNegativeConfidenceEvaluator.ConstitutesExclusion(hi, pos, 60),
                "阳性把握度 ≥ 阈值仍不得构成排除 —— 去极性门即此抓(F-8.3 修补 (c))");
            Assert.AreEqual(DiagnosisNegativeConfidenceEvaluator.NeverExcludes,
                DiagnosisNegativeConfidenceEvaluator.ExclusionLevel(hi, pos),
                "阳性行的 L* 恒为 NeverExcludes");

            // 对照:同一表下**阴性**行(权重 1)确实构成排除 —— 证明差异只来自极性
            SignLexemeRow neg = Row("n_same", SignPolarity.Negative, 20, WeightOneRaw);
            Assert.IsTrue(
                DiagnosisNegativeConfidenceEvaluator.ConstitutesExclusion(hi, neg, 60),
                "同表同权重下阴性行须构成排除(差异只来自极性)");
        }

        // ═══════════════════════════════════════════════════════════
        //  AC-8-16 · F-8.4 无随机(N ≥ 10⁴ 逐位相同 + 零 PRNG)
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac816_tenThousandReplays_bitIdentical()
        {
            // F-8.4 无随机的**可证伪**形态:同一 (Skill, sign_id) 反复求值,
            // 且**交错不同行 / 不同档**驱动 —— 任何跨调用状态或 RNG 都会在交错下暴露。
            DiagnosisReadFloorTable rf = ReadFloorSeed();
            List<SignLexemeRow> rows = SeedRows();
            int[] skills = { 0, 7, 15, 20, 35, 60 };

            // 基线 = 独立重算表(不经求值器):把握度 = C_neg(Skill) × W_j
            DiagnosisNegativeConfidenceTable table = NegSeedTable();
            var expected = new Dictionary<(int, int), (int state, int conf, bool excl)>();
            for (int r = 0; r < rows.Count; r++)
                foreach (int skill in skills)
                {
                    float w = ExpectedWeight(table, rows[r]);
                    float c = table.CurveAt(skill) * w;
                    if (c < 0f) c = 0f;
                    if (c > 1f) c = 1f;
                    // 四态**独立重算**(F-8.1 三合取门 —— 不复用求值器,防同源假绿)
                    string word = DiagnosisReadFloorEvaluator.DisplayWord(rows[r], skill);
                    bool readable = skill >= rows[r].TierNamed && 1f >= rf.ReadFloor(skill) && word != null;
                    int state = readable
                        ? (rows[r].Polarity == SignPolarity.Negative ? 2 : 1)
                        : 3;
                    expected[(r, skill)] = (state, BitConverter.SingleToInt32Bits(c),
                        rows[r].Polarity == SignPolarity.Negative && c >= table.ExcludeMin);
                }

            const int N = 10000;
            for (int i = 0; i < N; i++)
            {
                // 交错序:行序 × 档序按 i 轮转 —— 顺序本身不是常量
                int r = (i * 7 + 3) % rows.Count;
                int skill = skills[(i * 5 + 1) % skills.Length];

                var o = DiagnosisReadFloorEvaluator.Evaluate(rf, rows[r], skill, 1f);
                float c = DiagnosisNegativeConfidenceEvaluator.Confidence(table, rows[r], skill);
                bool excl = DiagnosisNegativeConfidenceEvaluator.ConstitutesExclusion(table, rows[r], skill);
                var want = expected[(r, skill)];

                Assert.AreEqual(want.state, (int)o.State,
                    $"第 {i} 次(行 {r} 档 {skill})四态分叉 —— F-8.4 禁随机");
                Assert.AreEqual(want.conf, BitConverter.SingleToInt32Bits(c),
                    $"第 {i} 次(行 {r} 档 {skill})把握度逐位分叉 —— 8 引入随机项或跨调用状态");
                Assert.AreEqual(want.excl, excl,
                    $"第 {i} 次(行 {r} 档 {skill})排除判定分叉");
            }
        }

        [Test]
        public void test_ac816_productionAssembly_zeroPrngCallSites()
        {
            // 静态守门:8 程序集零 PRNG 调用点(源层 + IL 层);本故事新增件须在扫描面内。
            List<string> errs = DiagnosisBoundaryGates.RunAll(out _);
            Assert.IsEmpty(errs,
                "8 前缀下须零 PRNG / 零 libm / 零持久化 / 零时钟:\n" + string.Join("\n", errs));

            // 反空转:扫描面须非空(产物缺失 ⇒ matched = 0 ⇒ 假绿)
            string dll = AssemblyGates.ScriptAssemblyPath(AssemblyGates.PresentationAssemblyName);
            List<string> ilErrs = DiagnosisBoundaryGates.CheckDiagnosisIl(
                dll, DiagnosisBoundaryGates.DiagnosisModuleNamespacePrefix,
                out int matched, out _);
            Assert.IsEmpty(ilErrs);
            Assert.Greater(matched, 0, "扫描面为空 ⇒ 拒以空集冒充绿(AC-8-2)");
        }

        // ═══════════════════════════════════════════════════════════
        //  AC-8-17 · F-8.4 语义边界(低熟练度 = 读得粗,不是读错)
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac817_polarityNeverFlips()
        {
            DiagnosisReadFloorTable rf = ReadFloorSeed();
            int[] skills = { 0, 1, 10, 20, 50, 60 };

            foreach (SignLexemeRow row in SeedRows())
            {
                var seen = new HashSet<SignReadState>();
                foreach (int skill in skills)
                    seen.Add(DiagnosisReadFloorEvaluator.Evaluate(rf, row, skill, 1f).State);

                if (row.Polarity == SignPolarity.Positive)
                    Assert.IsFalse(seen.Contains(SignReadState.Negative),
                        $"{row.SignId}:阳性体征的极性不得被熟练度翻成阴性");
                else
                    Assert.IsFalse(seen.Contains(SignReadState.Positive),
                        $"{row.SignId}:阴性体征的极性不得被熟练度翻成阳性");

                // 低档唯一退化方向 = 读不出(UnreadableNegative),不是反向
                Assert.IsTrue(seen.IsSubsetOf(new[]
                {
                    SignReadState.Positive, SignReadState.Negative, SignReadState.UnreadableNegative,
                }), $"{row.SignId}:六档输出须 ⊆ 字母表(无假阳性 / 无真值改写)");
            }

            // ── 反空转:极性须**忠实呈现**(否则「从不翻转」可由「永远是读不出」白拿)──
            // 满技能 + Sign 满值 ⇒ 阳性行必呈 Positive、阴性行必呈 Negative。
            foreach (SignLexemeRow row in SeedRows())
            {
                var top = DiagnosisReadFloorEvaluator.Evaluate(rf, row, SkillRegistry.SKILL_CAP, 1f);
                SignReadState want = row.Polarity == SignPolarity.Positive
                    ? SignReadState.Positive : SignReadState.Negative;
                Assert.AreEqual(want, top.State,
                    $"{row.SignId}:满技能 + Sign=1 须忠实呈 {want}(否则「不翻转」是空转)");
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  AC-8-18 / AC-8-F4 · F-8.5 不泄漏(公式层 + 数值层)
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac818_confidenceArgTable_lacksSignValue()
        {
            // 实现级:把握度 / 排除 / L* 的实参表**不含任何 Sign_j 载体**
            // (Sign_j 是 [0,1] 的 float —— 排除路径若收 float,泄漏即成可能)。
            var t = typeof(DiagnosisNegativeConfidenceEvaluator);
            var offenders = new List<string>();
            foreach (MethodInfo m in t.GetMethods(
                BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                foreach (ParameterInfo p in m.GetParameters())
                {
                    Type pt = p.ParameterType;
                    if (pt.IsByRef) pt = pt.GetElementType();
                    if (pt == typeof(float) || pt == typeof(double))
                        offenders.Add($"{m.Name}({p.Name}:{pt.Name})");
                    if (pt != null && pt.Name.IndexOf("Vitals", StringComparison.Ordinal) >= 0)
                        offenders.Add($"{m.Name}({p.Name}:{pt.Name})");
                }
            }
            Assert.IsEmpty(offenders,
                "F-8.5 不泄漏:C_neg 与把握度的实参表**永不含 Sign_j**(排除路径零 float 形参):\n" +
                string.Join("\n", offenders));

            // 更强:C_neg 本体只吃整数等级。
            // ⚠️ 求值器**根本没有** CurveAt(曲线住定表) —— 故显式断言两件独立事实,
            //    禁 `??` 回退(那会让「日后给求值器加 CurveAt(float) 重载」静默逃过)。
            Assert.IsNull(t.GetMethod("CurveAt",
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly),
                "求值器不得自持 CurveAt —— 曲线唯一住在定表里(否则 Sign_j 有新入口面)");
            MethodInfo curve = typeof(DiagnosisNegativeConfidenceTable).GetMethod("CurveAt");
            Assert.That(curve, Is.Not.Null, "C_neg 查表须存在");
            ParameterInfo[] ps = curve.GetParameters();
            Assert.AreEqual(1, ps.Length, "C_neg 实参表须恰一个形参");
            Assert.AreEqual(typeof(int), ps[0].ParameterType,
                "C_neg 只吃整数等级 —— Sign_j 无入口");
        }

        [Test]
        public void test_ac818_ac8f4_noLeak_bothPolarities()
        {
            DiagnosisNegativeConfidenceTable table = NegSeedTable();
            DiagnosisReadFloorTable rf = ReadFloorSeed();

            // 阳性 / 阴性各一条(F4 要求两类各测一遍 —— 证 W_j 的 polarity 分支不引入 Sign_j)
            SignLexemeRow pos = SeedRows().First(r => r.Polarity == SignPolarity.Positive);
            SignLexemeRow neg = SeedRows().First(r => r.Polarity == SignPolarity.Negative);

            // ⚠️ 档位须 ≥ tier_named(两族行 tier_named 均为 Lv20)—— 否则 `named` 门先挡,
            //    两病人**都**读不出,分叉前置不成立(那是 F-8.1 命名门,不是 Sign_j 门)。
            int[] skills = { 20, 35, 50, 60 };

            foreach (SignLexemeRow row in new[] { pos, neg })
                foreach (int skill in skills)
                {
                    // ── 前置:F-8.1 侧**确实随 Sign_j 分叉**(否则本测恒真 —— 假绿)──
                    var a = DiagnosisReadFloorEvaluator.Evaluate(rf, row, skill, 1f);
                    var b = DiagnosisReadFloorEvaluator.Evaluate(rf, row, skill, 0f);
                    Assert.AreNotEqual(a.IsReadable, b.IsReadable,
                        $"{row.SignId}@{skill}:F-8.1 侧须随 Sign_j 分叉(否则本测恒真)");

                    // ── F-8.3 侧:把握度 == C_neg(skill) × W_j,**独立重算**(不经求值器)──
                    // 病人 A / B 的 Sign_j 截然不同,但下面这个数只由 (Skill, sign_id) 决定。
                    float expected = table.CurveAt(skill) * ExpectedWeight(table, row);
                    if (expected < 0f) expected = 0f;
                    if (expected > 1f) expected = 1f;

                    Assert.AreEqual(
                        BitConverter.SingleToInt32Bits(expected),
                        BitConverter.SingleToInt32Bits(
                            DiagnosisNegativeConfidenceEvaluator.Confidence(table, row, skill)),
                        $"{row.SignId}@{skill}:把握度须恰 = C_neg(Skill) × W_j —— 不得掺入 Sign_j(F-8.5)");

                    // 构成排除:阳性恒 false;阴性 = 与阈值比
                    bool excl = DiagnosisNegativeConfidenceEvaluator.ConstitutesExclusion(table, row, skill);
                    if (row.Polarity == SignPolarity.Positive)
                        Assert.IsFalse(excl, $"{row.SignId}:阳性不得构成排除证据");
                    else
                        Assert.AreEqual(expected >= table.ExcludeMin, excl,
                            $"{row.SignId}@{skill}:构成排除须仅由把握度与阈值决定");
                }
        }

        /// <summary>独立重算 <c>W_j</c>(阴性取行内权重,阳性取兜底)—— 不复用求值器,防同源假绿。</summary>
        private static float ExpectedWeight(in DiagnosisNegativeConfidenceTable table, SignLexemeRow row)
        {
            if (row.Polarity == SignPolarity.Negative && row.NegWeightRaw.HasValue)
                return row.NegWeightRaw.Value / 65536f;
            return table.FallbackWeight;
        }

        // ═══════════════════════════════════════════════════════════
        //  AC-8-F1 · 回归锚(伤寒 / 痢疾 Lv15 / Lv20;sign_abd_soft)
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac8f1_signAbdSoft_lStarInHalfOpenRange()
        {
            DiagnosisNegativeConfidenceTable table = NegSeedTable();
            SignLexemeRow row = SeedRows().First(r => r.SignId == "sign_abd_soft");
            Assert.AreEqual(SignPolarity.Negative, row.Polarity, "sign_abd_soft 须为阴性");

            int lStar = DiagnosisNegativeConfidenceEvaluator.ExclusionLevel(table, row);

            // L* ∈ (15, 20]:Lv15 不达标、Lv20 达标
            Assert.LessOrEqual(lStar, 20, "L* 须 ≤ 20(AC-8-F1 上界)");
            Assert.Greater(lStar, 15, "L* 须 > 15(AC-8-F1 下界)");
            Assert.AreEqual(20, lStar, "AC-8-F1 ∧ C-7(tier_named = 20)⇒ L* = 20");

            // 等价式逐档核对(AC-8-F1 原文:C_neg(15)×W < MIN ≤ C_neg(20)×W)
            float min = table.ExcludeMin;
            float c15 = DiagnosisNegativeConfidenceEvaluator.Confidence(table, row, 15);
            float c20 = DiagnosisNegativeConfidenceEvaluator.Confidence(table, row, 20);
            Assert.Less(c15, min, "Lv15 把握度须 < EXCLUDE_CONF_MIN");
            Assert.GreaterOrEqual(c20, min, "Lv20 把握度须 ≥ EXCLUDE_CONF_MIN");
        }

        [Test]
        public void test_ac8f1_lv15AndLv20_readAsNegativeNotPositive()
        {
            // UC-8-F1 公式级:两档**均非阳性**,Lv15 不构成排除、Lv20 构成
            DiagnosisNegativeConfidenceTable table = NegSeedTable();
            DiagnosisReadFloorTable rf = ReadFloorSeed();
            SignLexemeRow row = SeedRows().First(r => r.SignId == "sign_abd_soft");

            foreach (int skill in new[] { 15, 20 })
            {
                var o = DiagnosisReadFloorEvaluator.Evaluate(rf, row, skill, 1f);
                Assert.AreNotEqual(SignReadState.Positive, o.State,
                    $"Lv{skill}:阴性体征呈阳性即极性翻转(AC-8-17)");
            }
            Assert.IsFalse(DiagnosisNegativeConfidenceEvaluator.ConstitutesExclusion(table, row, 15),
                "Lv15:阴性形态 + 把握不足,**不构成排除**");
            Assert.IsTrue(DiagnosisNegativeConfidenceEvaluator.ConstitutesExclusion(table, row, 20),
                "Lv20:构成排除证据");
        }

        // ═══════════════════════════════════════════════════════════
        //  AC-8-50 · 回归锚存在性(F1/F2/F4/F5 四条套件入口)
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac850_anchorSuiteResident()
        {
            var t = typeof(ConfidenceLeakTest);
            var names = new HashSet<string>(
                t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Select(m => m.Name), StringComparer.Ordinal);

            foreach (string anchor in new[]
            {
                "test_ac8f1_signAbdSoft_lStarInHalfOpenRange",   // F1
                "test_ac812_readGamma_doesNotMoveNegativeFamily",  // F2(阴性族侧:C-4 正交)
                "test_ac818_ac8f4_noLeak_bothPolarities",        // F4
                "test_ac8f5_monoSideSelfConsistent",             // F5(Mono 侧自洽;跨平台 NOT-RUN)
            })
                Assert.IsTrue(names.Contains(anchor),
                    $"AC-8-50:回归锚「{anchor}」须常驻套件(公式级判据不得被删)");
        }

        [Test]
        public void test_ac8f5_monoSideSelfConsistent()
        {
            // ⚠️ NOT-RUN 半边:跨平台三格矩阵(ADR-012)未实跑。此处只证 **Mono 侧自洽** ——
            //    同源两次烘焙逐位一致 + 定表哈希自洽(定表值本身即 binary32,跨平台一致性
            //    待矩阵;禁借绿)。
            byte[] a = DiagnosisNegativeConfidenceBinderProbe.Bake(NegSeedJson());
            byte[] b = DiagnosisNegativeConfidenceBinderProbe.Bake(NegSeedJson());
            Assert.AreEqual(a.Length, b.Length);
            for (int i = 0; i < a.Length; i++)
                Assert.AreEqual(a[i], b[i], $"字节 {i} 双跑分叉 —— 同进程确定性破(F-8.4)");

            DiagnosisNegativeConfidenceTable t1 = LoadNegTable(NegSeedJson());
            DiagnosisNegativeConfidenceTable t2 = NegSeedTable();
            for (int s = 0; s <= t1.SkillCap; s++)
                Assert.AreEqual(
                    BitConverter.SingleToInt32Bits(t1.CurveAt(s)),
                    BitConverter.SingleToInt32Bits(t2.CurveAt(s)),
                    $"档 {s} 定表值分叉");
        }

        // ═══════════════════════════════════════════════════════════
        //  构建期校验(值域 / C-5 / C-6;各违例各红在己)
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_bind_seedAndLegalBaseline_pass()
        {
            // 反空转:仓库真种子 + 合法夹具须**真通过**(证路径非恒拒)
            Assert.DoesNotThrow(() => DiagnosisNegativeConfidenceBaker.BakeFromRepo(RepoRoot));
            Assert.DoesNotThrow(() => DiagnosisNegativeConfidenceBinderProbe.Bake(
                ReadFixture("neg_conf_legal_baseline.json")));
            DiagnosisNegativeConfidenceTable t = NegSeedTable();
            Assert.AreEqual(61, t.Count, "定表覆盖 [0, SKILL_CAP] = 61 档");
            Assert.AreEqual(SkillRegistry.SKILL_CAP, t.SkillCap, "skill_cap 须与 30 同源");
        }

        [Test]
        public void test_bind_halfGammaAndPlateau_pass()
        {
            Assert.DoesNotThrow(() => DiagnosisNegativeConfidenceBinderProbe.Bake(
                ReadFixture("neg_conf_half_gamma.json")), "NEG_GAMMA = 1/2 须合法(C-5 闭集)");
            Assert.DoesNotThrow(() => DiagnosisNegativeConfidenceBinderProbe.Bake(
                ReadFixture("neg_conf_plateau_cap_eq_zero.json")),
                "NEG_CONF_CAP = NEG_CONF_0 须合法(平段;序关系非严格)");
        }

        [Test]
        public void test_bind_gammaThird_red() =>
            NegBakeFails(ReadFixture("neg_conf_gamma_third_red.json"), "C-5");

        [Test]
        public void test_bind_gammaFloatToken_red() =>
            NegBakeFails(ReadFixture("neg_conf_gamma_float_token_red.json"), "ADR-014");

        [Test]
        public void test_bind_zeroWeight_red() =>
            NegBakeFails(ReadFixture("neg_conf_zero_weight_red.json"), "C-6");

        [Test]
        public void test_bind_capBelowZero_red() =>
            NegBakeFails(ReadFixture("neg_conf_cap_below_zero_red.json"), "F-8.3");

        [Test]
        public void test_bind_excludeAboveCap_red() =>
            NegBakeFails(ReadFixture("neg_conf_exclude_above_cap_red.json"), "F-8.3");

        [Test]
        public void test_bind_conf0OutOfRange_red() =>
            NegBakeFails(ReadFixture("neg_conf_conf0_out_of_range_red.json"), "F-8.3");

        [Test]
        public void test_bind_skillCapDrift_red() =>
            NegBakeFails(ReadFixture("neg_conf_skillcap_drift_red.json"), "SKILL_CAP");

        [Test]
        public void test_bind_unknownKey_red() =>
            NegBakeFails(ReadFixture("neg_conf_unknown_key_red.json"), "unknown-key");

        [Test]
        public void test_bind_schemaVersionMismatch_red() =>
            // ADR-014 §五:版本不匹配须在**构建期**硬失败 —— 不得漏到运行期装载才抛 E-13。
            NegBakeFails(ReadFixture("neg_conf_schema_version_red.json"), "schema_version");

        // ═══════════════════════════════════════════════════════════
        //  定表单调 / 端点(C_neg 形状)
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_curve_monotonicAndEndpoints()
        {
            DiagnosisNegativeConfidenceTable t = NegSeedTable();
            Assert.AreEqual(0.5f, t.CurveAt(0), 1e-6f, "C_neg(0) = NEG_CONF_0");
            Assert.AreEqual(1.0f, t.CurveAt(60), 1e-6f, "C_neg(SKILL_CAP) = NEG_CONF_CAP");
            for (int s = 1; s <= t.SkillCap; s++)
                Assert.GreaterOrEqual(t.CurveAt(s), t.CurveAt(s - 1),
                    $"C_neg 须单调不减(档 {s})");
        }

        // ═══════════════════════════════════════════════════════════
        //  codec 硬失败(E-13)
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_codec_badMagic_red()
        {
            byte[] good = DiagnosisNegativeConfidenceBinderProbe.Bake(NegSeedJson());
            byte[] bad = (byte[])good.Clone();
            bad[0] = (byte)'X';
            Assert.Throws<InvalidOperationException>(
                () => DiagnosisNegativeConfidenceCookedCodec.Read(bad));
        }

        [Test]
        public void test_codec_truncatedPayload_red()
        {
            byte[] good = DiagnosisNegativeConfidenceBinderProbe.Bake(NegSeedJson());
            var short_ = new byte[good.Length - 4];
            Array.Copy(good, short_, short_.Length);
            Assert.Throws<InvalidOperationException>(
                () => DiagnosisNegativeConfidenceCookedCodec.Read(short_));
        }

        [Test]
        public void test_codec_schemaMismatch_red()
        {
            byte[] good = DiagnosisNegativeConfidenceBinderProbe.Bake(NegSeedJson());
            byte[] bad = (byte[])good.Clone();
            bad[8] = 9;  // schemaVersion 低字节 → 9
            Assert.Throws<InvalidOperationException>(
                () => DiagnosisNegativeConfidenceCookedCodec.Read(bad));
        }

        [Test]
        public void test_configVersion_deterministic()
        {
            Assert.AreEqual(
                DiagnosisNegativeConfidenceBinderProbe.ConfigVersionOf(NegSeedJson()),
                DiagnosisNegativeConfidenceBinderProbe.ConfigVersionOf(NegSeedJson()),
                "ConfigVersion = 源文本内容哈希(ADR-014 §五)须确定");
        }

        // ═══════════════════════════════════════════════════════════
        //  helpers
        // ═══════════════════════════════════════════════════════════

        private const string Prefix = DiagnosisBoundaryGates.DiagnosisModuleNamespacePrefix;

        private static bool InPrefix(Type t)
        {
            while (t != null && string.IsNullOrEmpty(t.Namespace)) t = t.DeclaringType;
            if (t == null) return false;
            string ns = t.Namespace;
            return ns == Prefix || ns.StartsWith(Prefix + ".", StringComparison.Ordinal);
        }

        /// <summary>断言某静态求值器的**任何**实参表都不含给定类型(C-4 正交的实现级判据)。</summary>
        private static void AssertNoParamOfType(Type owner, Type forbidden, string what)
        {
            var offenders = new List<string>();
            foreach (MethodInfo m in owner.GetMethods(
                BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
                foreach (ParameterInfo p in m.GetParameters())
                {
                    Type pt = p.ParameterType;
                    if (pt.IsByRef) pt = pt.GetElementType();
                    if (pt == forbidden)
                        offenders.Add($"{m.Name}({p.Name} : {pt.Name})");
                }
            Assert.IsEmpty(offenders,
                $"{what}不得以另一族的表类型为实参(C-4 两族正交;分表分文件即此判):\n" +
                string.Join("\n", offenders));
        }

        /// <summary>断言某表的**字段名**不含给定语义 token(C-4 的字段面判据)。</summary>
        private static void AssertNoFieldToken(Type owner, string[] tokens, string what)
        {
            const BindingFlags fb = BindingFlags.DeclaredOnly | BindingFlags.Public |
                                    BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            var hits = new List<string>();
            foreach (FieldInfo f in owner.GetFields(fb))
            {
                string norm = f.Name.Replace("_", "").ToLowerInvariant();
                foreach (string tok in tokens)
                    if (norm.Contains(tok))
                        hits.Add($"{owner.Name}.{f.Name} ← 「{tok}」");
            }
            Assert.IsEmpty(hits,
                $"{what}的字段不得携带另一族语义(C-4):\n" + string.Join("\n", hits));
        }
    }
}
