// 权威来源:
//   Story 005(production/epics/item-database/story-005-conservation-law-gates.md)
//     · AC-21a-8  —— 构建期聚合守恒上界:QTY_MULT_MAX × Σ(w × 产出基数) > EFF_MAX × Σ(w × 输入基数)
//                    ⇒ 拒;产出侧必须含乘子上界;EFF_MAX > 1 亦拒(交叉 AC-21a-40)
//     · AC-21a-39 —— 运行期不变量:任意合法配方 / 任意 EFF ≤ EFF_MAX / 任意 QtyMultiplier
//                    ⇒ Σ(w × OutputQty) ≤ EFF_MAX × Σ(w × ActualConsumed);整数域先乘后比
//     · AC-21a-40 —— EFF_MAX > 1 的任何常量表 ⇒ 构建期硬失败
//     · AC-21a-56 —— EFF_MIN ≤ 0 或 EFF_MIN > EFF_MAX ⇒ 构建期硬失败(EFF 是 F1 除数)
//     · AC-21a-65 —— 逐条同形极值式(**唯一硬门**,D-21-32)
//   GDD:design/gdd/item-database.md §Core Rules 规则八(:202-228)· §Formulas F1 守恒块(:530-552)
//        · §Tuning Knobs(:897-957)
//   ADR-006(主):§Decision 四 守恒律整数域内求值 · §Decision 三 单一舍入 ROUND_HALF_AWAY_FROM_ZERO
//   ADR-005(次):确定性模拟 —— 谓词纯函数、同参数集逐位同判定
//   ADR-025 §①:纯 sim 逻辑 → 装配 `Sim`(引用集 {BCL, Sim.Contracts})
//
// ⚠️ 落点:故事头登记的账本路径 = tests/unit/item_database/conservation_law_gates_test.cs;
//    Unity 只编译 unity/Assets/ 树 ⇒ 真身 = 本文件(与 Story 001/002/003/004 同一先例)。
//
// ⚠️ 范围(Out of Scope 硬边界):AC-21a-65 的执法体落 Editor.Tools.Gates/ConservationGates.cs
//    (纯函数,错误列表;throw 由 008 聚合执行),算术原语落 Sim/ItemDatabase/ConservationSolver.cs。
//    QTY_MULT 下界 / cap 和(AC-21a-9/10/11/12/13)与其余负向夹具归 Story 006/007;
//    容器闭包(Story 010)与烘焙管线接线(Story 008)不在本文件。
//
// ⚠️ 数值纪律:以下常量**全部是测试夹具值,不是游戏平衡值**(GDD §Tuning Knobs「默认」列留空,
//    数值待用户)。两条文档反例的数字**照录 GDD**(反例一 GDD :535;反例二 GDD :540)——
//    是 GDD 自带规格,非本测试新造。
// ⚠️ D-21-32(落盘,勿当已解):聚合式保留为**必要非充分**,极值式为**唯一硬门**;
//    本文件专门断言「聚合过、极简式红」的那一格必须判红 —— 那是 D-21-32 的整个存在理由。
// ⚠️ 零 UnityEngine / 零外部 I/O(仅读三个夹具)/ 无随机种子(整数步进遍历,非 RNG)/ 无时间依赖。

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using DaYiJingCheng.EditorTools.Gates;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Tests.Unit.ItemDatabase
{
    [TestFixture]
    internal sealed class ConservationLawGatesTest
    {
        // ══════════════ 夹具小件(数值待用户 —— 此处为测试夹具值)══════════════

        private const int FixtureSkillCap = 60;
        private const int FixtureMaxQuality = 5;
        private const int SweepSteps = 8;          // QtyMultiplier 整数步进档数(非浮点插值)

        private static Fix fx(string literal) => FixParse.Parse(literal);

        /// <summary>夹具常量表:仅 EFF_* / QTY_MULT_* 由夹具给,其余取中性值
        /// (caps 全 0 ⇒ ΣM = 0;retain = 1;env 带 [-1, 0])—— 本条测试只关心守恒律与 EFF 范围门。</summary>
        private static RecipeSettlementConstants constants(
            string effMin, string effMax, string qtyMultMin, string qtyMultMax)
            => new RecipeSettlementConstants(
                qtyMultMin: fx(qtyMultMin),
                qtyMultMax: fx(qtyMultMax),
                skillModCap: fx("0"),
                qualModCap: fx("0"),
                equipModCap: fx("0"),
                envModMin: fx("-1"),
                envModMax: fx("0"),
                retainMin: fx("1"),
                retainMax: fx("1"),
                effMin: fx(effMin),
                effMax: fx(effMax),
                maxQuality: FixtureMaxQuality,
                skillCap: FixtureSkillCap);

        /// <summary>由夹具 JSON 构造常量表(键名即 GDD 旋钮名,但本文件属 Tests 装配 —— 扫描面排除)。</summary>
        private static RecipeSettlementConstants constantsFromFixture(string json)
            => constants(
                readStringField(json, "eff_min"),
                readStringField(json, "eff_max"),
                readStringField(json, "qty_mult_min"),
                readStringField(json, "qty_mult_max"));

        /// <summary>构造一侧条目并登记权重(权重表由调用方持有 —— 物品表归 21a 数据文件)。</summary>
        private static RecipeEntry[] side(
            string prefix, int[] qtys, int[] weights, Dictionary<string, int> weightsByBaseId)
        {
            Assert.That(weights.Length, Is.EqualTo(qtys.Length), $"{prefix} 夹具:qty 与 weight 长度须等");
            var entries = new RecipeEntry[qtys.Length];
            for (int i = 0; i < qtys.Length; i++)
            {
                var key = new ItemKey(prefix + i, ProcessingState.Raw);
                weightsByBaseId[key.BaseId] = weights[i];
                entries[i] = new RecipeEntry(key, qtys[i]);
            }

            return entries;
        }

        /// <summary>权重查表**纯函数**(按 base_id 查;调用方须提供纯函数 —— 求解器不缓存、不持有物品表)。</summary>
        private static Func<ItemKey, int> weightOf(Dictionary<string, int> weightsByBaseId)
            => key => weightsByBaseId[key.BaseId];

        // ══════════════ AC-21a-8:聚合守恒上界(必要非充分)══════════════

        [Test]
        public void test_aggregateConservation_gddCounterExampleOne_rejected()
        {
            // Given:GDD :535 反例一(w_in = 10 / w_out = 9 / EFF_MAX = 1 / QTY_MULT_MAX = 2)
            string json = readFixture("invalid_conservation.json");
            RecipeSettlementConstants c = constantsFromFixture(json);
            var weights = new Dictionary<string, int>();
            RecipeEntry[] outputs = side("out_", readIntArray(json, "output_qty"),
                readIntArray(json, "output_weight"), weights);
            RecipeEntry[] inputs = side("in_", readIntArray(json, "input_qty"),
                readIntArray(json, "input_weight"), weights);
            Assert.That(weights["in_0"], Is.EqualTo(10), "夹具自证:输入权重 10");
            Assert.That(weights["out_0"], Is.EqualTo(9), "夹具自证:输出权重 9");
            Assert.That(c.QtyMultMax.Raw, Is.EqualTo(fx("2").Raw), "夹具自证:乘子上限 2");
            Assert.That(c.EffMax.Raw, Is.EqualTo(fx("1").Raw), "夹具自证:EFF_MAX = 1");

            // When
            IReadOnlyList<string> errors = ConservationGates.ValidateAggregateConservation(
                outputs, inputs, weightOf(weights), c, "recipe_agg_breach");

            // Then:2 × 9 = 18 > 1 × 10 = 10 ⇒ 拒(原稿两侧都用基数 ⇒ 9 ≤ 10 假绿)
            Assert.That(errors.Count, Is.EqualTo(1), "聚合式击穿 ⇒ 恰一条错误");
            Assert.That(errors[0], Does.Contain("AC-21a-8"));
            Assert.That(errors[0], Does.Contain("D-21-21"));
            Assert.That(ConservationSolver.AggregateUpperBoundHolds(
                outputs, inputs, weightOf(weights), c.EffMax, c.QtyMultMax), Is.False,
                "谓词同判:18 > 10");
        }

        [Test]
        public void test_aggregateConservation_exactEquality_accepted()
        {
            // Given:两侧同权重 9/9、qty 1、乘子上限 1 ⇒ 1 × 9 = 1 × 9(恰好相等)
            RecipeSettlementConstants c = constants("1", "1", "1", "1");
            var weights = new Dictionary<string, int>();
            RecipeEntry[] outputs = side("out_", new[] { 1 }, new[] { 9 }, weights);
            RecipeEntry[] inputs = side("in_", new[] { 1 }, new[] { 9 }, weights);

            // When / Then:QA 明文「恰好相等 ⇒ 过」
            Assert.That(ConservationGates.ValidateAggregateConservation(
                outputs, inputs, weightOf(weights), c, "recipe_equal"), Is.Empty,
                "恰等 ⇒ 过(边界含等号)");
        }

        [Test]
        public void test_aggregateConservation_effMaxAboveOne_rejectedAndEffMaxGateMerged()
        {
            // Given:EFF_MAX = 3/2(AC-8 文本含「EFF_MAX > 1 亦拒」;交叉 AC-40)
            RecipeSettlementConstants c = constants("1", "3/2", "1", "1");
            var weights = new Dictionary<string, int>();
            RecipeEntry[] outputs = side("out_", new[] { 1 }, new[] { 1 }, weights);
            RecipeEntry[] inputs = side("in_", new[] { 1 }, new[] { 100 }, weights);

            // When:守恒不等式本身宽松(1 ≤ 150),但上限非法
            IReadOnlyList<string> errors = ConservationGates.ValidateAggregateConservation(
                outputs, inputs, weightOf(weights), c, "recipe_eff_high");

            // Then:委托 AC-40 门并合并其错误列表(单一实现,不重复写判据)
            Assert.That(errors.Count, Is.EqualTo(1), "上限 > 1 ⇒ 恰一条(不等式本身未违反)");
            Assert.That(errors[0], Does.Contain("AC-21a-40"), "错误由 AC-40 门贡献(交叉 AC)");
        }

        [Test]
        public void test_aggregateConservation_multiOutputMixedWeights_rejectedWhenSumExceeds()
        {
            // Given:两条产出(权重 5 + 5,各 qty 1)+ 单条投入(权重 12,qty 1),乘子上限 2
            // ⇒ 2 × (5 + 5) = 20 > 1 × 12 = 12 ⇒ 拒(weight 归一到同一最小单位)
            RecipeSettlementConstants c = constants("1", "1", "1", "2");
            var weights = new Dictionary<string, int>();
            RecipeEntry[] outputs = side("out_", new[] { 1, 1 }, new[] { 5, 5 }, weights);
            RecipeEntry[] inputs = side("in_", new[] { 1 }, new[] { 12 }, weights);

            Assert.That(ConservationGates.ValidateAggregateConservation(
                outputs, inputs, weightOf(weights), c, "recipe_multi").Count, Is.EqualTo(1),
                "多条产出求和后超界 ⇒ 拒(QA Edge:多条 outputs)");
        }

        [Test]
        public void test_aggregateConservation_effMaxBelowOne_shrinksRightSide_rejected()
        {
            // Given:EFF_MAX = 1/2 使右侧收缩 ⇒ 2 × 9 = 18 > (1/2) × 20 = 10 ⇒ 拒(QA Edge)
            RecipeSettlementConstants c = constants("1/4", "1/2", "1", "2");
            var weights = new Dictionary<string, int>();
            RecipeEntry[] outputs = side("out_", new[] { 1 }, new[] { 9 }, weights);
            RecipeEntry[] inputs = side("in_", new[] { 1 }, new[] { 20 }, weights);

            Assert.That(ConservationGates.ValidateAggregateConservation(
                outputs, inputs, weightOf(weights), c, "recipe_eff_low").Count, Is.EqualTo(1),
                "EFF_MAX < 1 使右端收缩 ⇒ 同一配方可被拒");
        }

        [Test]
        public void test_aggregateConservation_emptyOrNullSide_rejected()
        {
            // Given:域外(空投入 / 空产出 / null 权重函数)—— 空侧 = 凭空造物或凭空销毁,
            // 由配方校验前置拒(AC-21a-8 QA Edge);本门返回 false,不抛
            RecipeSettlementConstants c = constants("1", "1", "1", "1");
            var weights = new Dictionary<string, int>();
            RecipeEntry[] outputs = side("out_", new[] { 1 }, new[] { 9 }, weights);
            RecipeEntry[] inputs = side("in_", new[] { 1 }, new[] { 9 }, weights);

            Assert.That(ConservationSolver.AggregateUpperBoundHolds(
                outputs, Array.Empty<RecipeEntry>(), weightOf(weights), c.EffMax, c.QtyMultMax), Is.False,
                "空投入 ⇒ false(不抛)");
            Assert.That(ConservationSolver.AggregateUpperBoundHolds(
                Array.Empty<RecipeEntry>(), inputs, weightOf(weights), c.EffMax, c.QtyMultMax), Is.False,
                "空产出 ⇒ false");
            Assert.That(ConservationSolver.AggregateUpperBoundHolds(
                null, inputs, weightOf(weights), c.EffMax, c.QtyMultMax), Is.False, "null 产出 ⇒ false");
            Assert.That(ConservationSolver.AggregateUpperBoundHolds(
                outputs, inputs, null, c.EffMax, c.QtyMultMax), Is.False, "null 权重函数 ⇒ false");
        }

        [Test]
        public void test_aggregateConservation_weightLookupNonPositiveOrMultiplierNonPositive_rejected()
        {
            // Given:权重查表给出 0(未填)/ 乘子 ≤ 0 —— 量纲不可通约或前提不可用 ⇒ 一律 false
            RecipeSettlementConstants c = constants("1", "1", "1", "1");
            var weights = new Dictionary<string, int>();
            RecipeEntry[] outputs = side("out_", new[] { 1 }, new[] { 0 }, weights);
            RecipeEntry[] inputs = side("in_", new[] { 1 }, new[] { 10 }, weights);

            Assert.That(ConservationSolver.AggregateUpperBoundHolds(
                outputs, inputs, weightOf(weights), c.EffMax, c.QtyMultMax), Is.False,
                "权重 ≤ 0(未填)⇒ false,不得当 0 用");
            Assert.That(ConservationSolver.AggregateUpperBoundHolds(
                outputs, inputs, key => -1, c.EffMax, c.QtyMultMax), Is.False, "负权重 ⇒ false");

            var weightsOk = new Dictionary<string, int>();
            RecipeEntry[] outOk = side("out_", new[] { 1 }, new[] { 1 }, weightsOk);
            RecipeEntry[] inOk = side("in_", new[] { 1 }, new[] { 10 }, weightsOk);
            Assert.That(ConservationSolver.AggregateUpperBoundHolds(
                outOk, inOk, weightOf(weightsOk), new Fix(0L), c.QtyMultMax), Is.False,
                "EFF_MAX raw = 0 ⇒ 前提不可用 ⇒ false");
            Assert.That(ConservationSolver.AggregateUpperBoundHolds(
                outOk, inOk, weightOf(weightsOk), c.EffMax, new Fix(0L)), Is.False,
                "乘子上限 raw = 0 ⇒ false");
        }

        // ══════════════ AC-21a-40:EFF_MAX > 1 ══════════════

        [Test]
        public void test_effMax_aboveOne_rejected()
        {
            RecipeSettlementConstants c = constants("1", "3/2", "1", "2");
            IReadOnlyList<string> errors = ConservationGates.ValidateEffMaxRange(c, "const_table_high");

            Assert.That(errors.Count, Is.EqualTo(1), "上限 > 1 ⇒ 恰一条");
            Assert.That(errors[0], Does.Contain("AC-21a-40"));
            Assert.That(errors[0], Does.Contain("98304"),
                "点名 offending raw 值(3/2 × 65536 = 98304)");
        }

        [Test]
        public void test_effMax_exactlyOne_accepted_zeroLoss()
        {
            RecipeSettlementConstants c = constants("1/2", "1", "1", "2");
            Assert.That(ConservationGates.ValidateEffMaxRange(c, "const_table_one"), Is.Empty,
                "恰 = 1(零损耗档)⇒ 过 —— 它是满技能取等号的唯一合法上端");
        }

        [Test]
        public void test_effMax_belowOne_accepted()
        {
            RecipeSettlementConstants c = constants("1/4", "3/4", "1", "2");
            Assert.That(ConservationGates.ValidateEffMaxRange(c, "const_table_below"), Is.Empty,
                "上限 < 1 ⇒ 过(守恒律的常规工况)");
        }

        [Test]
        public void test_effMaxAndEffMin_bothIllegal_eachGateNamesItsOwnAc()
        {
            // QA Edge:EFF_MAX 与 EFF_MIN 同时非法 —— 两侧各判自己的域,AC 号不得串
            RecipeSettlementConstants c = constants("0", "3/2", "1", "2");

            IReadOnlyList<string> effMaxErrors = ConservationGates.ValidateEffMaxRange(c, "both_bad");
            IReadOnlyList<string> effMinErrors = ConservationGates.ValidateEffMinRange(c, "both_bad");

            Assert.That(effMaxErrors.Count, Is.EqualTo(1));
            Assert.That(effMinErrors.Count, Is.EqualTo(1), "EFF_MIN = 0 ⇒ 下端门一条");
            Assert.That(effMaxErrors[0], Does.Contain("AC-21a-40"));
            Assert.That(effMinErrors[0], Does.Contain("AC-21a-56"));
            Assert.That(effMaxErrors[0], Does.Not.Contain("AC-21a-56"), "AC 号不得串号");
            Assert.That(effMinErrors[0], Does.Not.Contain("AC-21a-40"), "AC 号不得串号");
        }

        // ══════════════ AC-21a-56:EFF_MIN 下端与序关系 ══════════════

        [Test]
        public void test_effMin_fixtureZero_rejected()
        {
            // Given:QA 指定负向夹具 —— eff_min = 0(EFF 是 F1 除数 ⇒ 运行期除零)
            string json = readFixture("invalid_eff_range.json");
            RecipeSettlementConstants c = constantsFromFixture(json);
            Assert.That(c.EffMin.Raw, Is.EqualTo(0L), "夹具自证:eff_min = 0");

            // When / Then
            IReadOnlyList<string> errors = ConservationGates.ValidateEffMinRange(c, "const_table_zero");
            Assert.That(errors.Count, Is.EqualTo(1), "EFF_MIN = 0 ⇒ 恰一条");
            Assert.That(errors[0], Does.Contain("AC-21a-56"));
            Assert.That(errors[0], Does.Contain("除数"), "错误须点明「EFF 是除数」这一根因");
        }

        [Test]
        public void test_effMin_negative_rejected()
        {
            RecipeSettlementConstants c = constants("-1/2", "1", "1", "2");
            IReadOnlyList<string> errors = ConservationGates.ValidateEffMinRange(c, "const_table_neg");

            Assert.That(errors.Count, Is.EqualTo(1), "EFF_MIN < 0 ⇒ 拒(实耗为负 = 凭空造料)");
            Assert.That(errors[0], Does.Contain("AC-21a-56"));
        }

        [Test]
        public void test_effMin_aboveEffMax_rejected_codeConstructedSiblingCase()
        {
            // 一份夹具只承载一个主案 ⇒ 「EFF_MIN > EFF_MAX」由代码构造的常量表驱动(夹具 _note 已记)
            RecipeSettlementConstants c = constants("3/4", "1/2", "1", "2");
            IReadOnlyList<string> errors = ConservationGates.ValidateEffMinRange(c, "const_table_inverted");

            Assert.That(errors.Count, Is.EqualTo(1), "区间反向 ⇒ 拒");
            Assert.That(errors[0], Does.Contain("AC-21a-56"));
        }

        [Test]
        public void test_effMin_equalEffMax_positive_accepted_notTightened()
        {
            // GDD 现文只拒 `>` ⇒ EFF_MIN = EFF_MAX > 0 退化为点集,按现文**过**(不擅自收紧)
            RecipeSettlementConstants c = constants("1", "1", "1", "2");
            Assert.That(ConservationGates.ValidateEffMinRange(c, "const_table_point"), Is.Empty,
                "EFF_MIN = EFF_MAX = 1 ⇒ 按现文过(收紧 = 改机制)");
        }

        [Test]
        public void test_effMin_zeroAndAboveMax_twoNamedErrors()
        {
            // 两种违反同时成立 ⇒ 各具名一条(可返回 2 条)
            RecipeSettlementConstants c = constants("0", "-1", "1", "2");
            IReadOnlyList<string> errors = ConservationGates.ValidateEffMinRange(c, "const_table_two");

            Assert.That(errors.Count, Is.EqualTo(2), "下端违反 + 序关系违反 ⇒ 2 条");
            Assert.That(errors[0], Does.Contain("AC-21a-56"));
            Assert.That(errors[1], Does.Contain("AC-21a-56"));
            Assert.That(errors[0], Does.Not.EqualTo(errors[1]), "两条须可分辨");
        }

        // ══════════════ AC-21a-65:逐条同形极值式(唯一硬门)══════════════

        [Test]
        public void test_perLineExtreme_fixtureAggregatePassesButPerLineRejected()
        {
            // Given:GDD :540 反例二 —— 聚合式**抓不住**的那一格(D-21-32 的整个存在理由)
            string json = readFixture("invalid_conservation_perline.json");
            RecipeSettlementConstants c = constantsFromFixture(json);
            var weights = new Dictionary<string, int>();
            RecipeEntry[] outputs = side("out_", readIntArray(json, "output_qty"),
                readIntArray(json, "output_weight"), weights);
            RecipeEntry[] inputs = side("in_", readIntArray(json, "input_qty"),
                readIntArray(json, "input_weight"), weights);

            Assert.That(weights["in_0"], Is.EqualTo(10), "夹具自证:输入权重 10");
            Assert.That(weights["out_0"], Is.EqualTo(6), "夹具自证:输出权重 6");
            Assert.That(c.QtyMultMax.Raw, Is.EqualTo(fx("3/2").Raw), "夹具自证:乘子上限 1.5");

            // When:两式各跑一次
            bool aggregateHolds = ConservationSolver.AggregateUpperBoundHolds(
                outputs, inputs, weightOf(weights), c.EffMax, c.QtyMultMax);
            IReadOnlyList<string> perLineErrors = ConservationGates.ValidatePerLineExtremeConservation(
                outputs, inputs, weightOf(weights), c, "recipe_perline_breach");

            // Then:聚合式 1.5 × 6 = 9 ≤ 10 **过**;运行期 Round(1 × 1.5) = 2 ⇒ 12 > 10 **击穿**
            Assert.That(aggregateHolds, Is.True,
                "聚合式必过 —— 否则本测不构成 D-21-32 的中间带(失去意义)");
            Assert.That(perLineErrors.Count, Is.EqualTo(1), "极值式击穿 ⇒ 恰一条错误(**必须红**)");
            Assert.That(perLineErrors[0], Does.Contain("AC-21a-65"));
            Assert.That(perLineErrors[0], Does.Contain("D-21-32"));
            Assert.That(ConservationSolver.PerLineExtremeBoundHolds(
                outputs, inputs, weightOf(weights), c.EffMax, c.QtyMultMax), Is.False,
                "缝在 Round:max(1, Round(1 × 1.5)) = 2");
        }

        [Test]
        public void test_perLineExtreme_midpointRounding_goesAwayFromZeroNotTiesToEven()
        {
            // Given:qty 1、乘子上限 5/2 ⇒ Round(2.5):HALF_AWAY ⇒ 3;ties-to-even ⇒ 2(被禁模式)
            RecipeSettlementConstants c = constants("1", "1", "1", "5/2");
            var weights = new Dictionary<string, int>();
            RecipeEntry[] outputs = side("out_", new[] { 1 }, new[] { 4 }, weights);
            RecipeEntry[] inputs = side("in_", new[] { 1 }, new[] { 10 }, weights);

            // When / Then:聚合 5/2 × 4 = 10 ≤ 10 过;极值 4 × 3 = 12 > 10 拒
            Assert.That(RecipeSettlementSolver.OutputQty(1, fx("5/2")), Is.EqualTo(3),
                "中点远离零 ⇒ 3(ties-to-even 会得 2 —— ADR-006 §Decision 三 禁该模式)");
            Assert.That(ConservationSolver.AggregateUpperBoundHolds(
                outputs, inputs, weightOf(weights), c.EffMax, c.QtyMultMax), Is.True,
                "聚合式恰相等 ⇒ 过");
            Assert.That(ConservationGates.ValidatePerLineExtremeConservation(
                outputs, inputs, weightOf(weights), c, "recipe_midpoint").Count, Is.EqualTo(1),
                "极值式 ⇒ 拒(同一中间带,第二例)");
        }

        [Test]
        public void test_perLineExtreme_exactEquality_accepted()
        {
            // 两侧权重 9/9、qty 1、乘子上限 1、EFF_MAX 1 ⇒ 1 × 9 = 1 × 9(恰等 ⇒ 过)
            RecipeSettlementConstants c = constants("1", "1", "1", "1");
            var weights = new Dictionary<string, int>();
            RecipeEntry[] outputs = side("out_", new[] { 1 }, new[] { 9 }, weights);
            RecipeEntry[] inputs = side("in_", new[] { 1 }, new[] { 9 }, weights);

            Assert.That(ConservationGates.ValidatePerLineExtremeConservation(
                outputs, inputs, weightOf(weights), c, "recipe_equal"), Is.Empty,
                "恰等 ⇒ 过(QA 明文)");
        }

        [Test]
        public void test_perLineExtreme_floorMaxOneSubdomain_stricterThanAggregate_passes()
        {
            // QA Edge:max(1,·) 地板起作用的子域(qty 小 + 乘子上限低)
            // 乘子上限 1/4 ⇒ Round(1 × 1/4) = 0 ⇒ 地板救回 1;聚合式只有 1/4 × 9 = 9/4(更松)
            RecipeSettlementConstants c = constants("1", "1", "1", "1/4");
            var weights = new Dictionary<string, int>();
            RecipeEntry[] outputs = side("out_", new[] { 1 }, new[] { 9 }, weights);
            RecipeEntry[] inputs = side("in_", new[] { 1 }, new[] { 10 }, weights);

            Assert.That(RecipeSettlementSolver.OutputQty(1, fx("1/4")), Is.EqualTo(1),
                "地板触发:Round(0.25) = 0 ⇒ max(1,·) 救回 1");
            Assert.That(ConservationGates.ValidatePerLineExtremeConservation(
                outputs, inputs, weightOf(weights), c, "recipe_floor"), Is.Empty,
                "极值式在本地板子域更严(9 > 9/4)但仍在界内 ⇒ 过");
        }

        [Test]
        public void test_perLineExtreme_multipleInputsCeilAccumulation_rejected()
        {
            // QA Edge:多条 inputs 的 Ceil 累加;EFF_MAX = 1/2 ⇒ 每条 Ceil(2 / 0.5) = 4
            // RHS = 1/2 × (3×4 + 3×4) = 12;LHS = 7 × max(1, Round(1 × 2)) = 14 ⇒ 14 > 12 ⇒ 拒
            RecipeSettlementConstants c = constants("1/4", "1/2", "1", "2");
            var weights = new Dictionary<string, int>();
            RecipeEntry[] outputs = side("out_", new[] { 1 }, new[] { 7 }, weights);
            RecipeEntry[] inputs = side("in_", new[] { 2, 2 }, new[] { 3, 3 }, weights);

            Assert.That(RecipeSettlementSolver.ActualConsumed(2, fx("1/2")), Is.EqualTo(4),
                "逐条 Ceil(2 / 0.5) = 4(实耗累加非基数累加)");
            Assert.That(ConservationGates.ValidatePerLineExtremeConservation(
                outputs, inputs, weightOf(weights), c, "recipe_ceil").Count, Is.EqualTo(1),
                "Ceil 累加后击穿 ⇒ 拒");
        }

        [Test]
        public void test_perLineExtreme_effMaxBelowOne_enlargesConsumedSide_passes()
        {
            // QA Edge:EFF_MAX < 1 使投入侧 Ceil 放大 ⇒ 同一配方转为宽裕
            // RHS = 1/2 × (40 × Ceil(1 / 0.5)) = 1/2 × 80 = 40;LHS = 9 × 2 = 18 ⇒ 过
            RecipeSettlementConstants c = constants("1/4", "1/2", "1", "2");
            var weights = new Dictionary<string, int>();
            RecipeEntry[] outputs = side("out_", new[] { 1 }, new[] { 9 }, weights);
            RecipeEntry[] inputs = side("in_", new[] { 1 }, new[] { 40 }, weights);

            Assert.That(ConservationGates.ValidatePerLineExtremeConservation(
                outputs, inputs, weightOf(weights), c, "recipe_eff_half"), Is.Empty,
                "EFF_MAX < 1 ⇒ 实耗放大 ⇒ 过");
        }

        [Test]
        public void test_perLineExtreme_effMaxNonPositive_premiseUnusableNamedErrorWithoutThrow()
        {
            // 前提不可用:EFF_MAX ≤ 0 会使投入侧上取整除零(运行期函数显式抛)
            // ⇒ 本门改为**具名前提错误**返回,不把异常漏出去,也不代判 AC-40/56 的域
            RecipeSettlementConstants c = constants("0", "0", "1", "2");
            var weights = new Dictionary<string, int>();
            RecipeEntry[] outputs = side("out_", new[] { 1 }, new[] { 1 }, weights);
            RecipeEntry[] inputs = side("in_", new[] { 1 }, new[] { 10 }, weights);

            IReadOnlyList<string> errors = null;
            Assert.DoesNotThrow(() =>
                errors = ConservationGates.ValidatePerLineExtremeConservation(
                    outputs, inputs, weightOf(weights), c, "recipe_bad_premise"),
                "前提不可用 ⇒ 返回具名错误,不让 ActualConsumed 的异常逃逸");
            Assert.That(errors.Count, Is.EqualTo(1));
            Assert.That(errors[0], Does.Contain("前提不可用"));
            Assert.That(errors[0], Does.Contain("AC-21a-40"), "点明域归属,不代判");
        }

        [Test]
        public void test_perLineExtreme_gateVerdict_equalsSolverPredicate()
        {
            // 单一实现自证:门的「空/非空」与谓词的真假**必须同号**(含域外一格)
            RecipeSettlementConstants ok = constants("1/2", "1", "1", "2");
            var weights = new Dictionary<string, int>();
            RecipeEntry[] outputs = side("out_", new[] { 1, 2 }, new[] { 9, 4 }, weights);
            RecipeEntry[] inputs = side("in_", new[] { 3, 1 }, new[] { 20, 5 }, weights);

            bool predicate = ConservationSolver.PerLineExtremeBoundHolds(
                outputs, inputs, weightOf(weights), ok.EffMax, ok.QtyMultMax);
            bool gatePassed = ConservationGates.ValidatePerLineExtremeConservation(
                outputs, inputs, weightOf(weights), ok, "recipe_single_impl").Count == 0;
            Assert.That(gatePassed, Is.EqualTo(predicate), "门与谓词同判(单一实现)");

            // 域外一格:空投入 ⇒ 谓词 false,门非空(同号)
            bool predicateEmpty = ConservationSolver.PerLineExtremeBoundHolds(
                outputs, Array.Empty<RecipeEntry>(), weightOf(weights), ok.EffMax, ok.QtyMultMax);
            bool gateEmptyPassed = ConservationGates.ValidatePerLineExtremeConservation(
                outputs, Array.Empty<RecipeEntry>(), weightOf(weights), ok, "recipe_single_impl").Count == 0;
            Assert.That(predicateEmpty, Is.False);
            Assert.That(gateEmptyPassed, Is.EqualTo(predicateEmpty), "域外亦同号");
        }

        // ══════════════ AC-21a-39:运行期不变量(正向扫描)══════════════

        /// <summary>AC-39 的合法夹具(夹具值,非游戏数值):w_in = 20 / w_out = 9 / qty 1 /
        /// EFF ∈ [1/2, 1] / 乘子 ∈ [1, 2]。聚合式 2 × 9 = 18 ≤ 1 × 20 = 20;
        /// 极值式 9 × 2 = 18 ≤ 1 × (20 × Ceil(1/1)) = 20 ⇒ 两侧都在界内。</summary>
        private static void legalSweepFixture(
            out RecipeEntry[] outputs, out RecipeEntry[] inputs, out Func<ItemKey, int> w,
            out RecipeSettlementConstants c)
        {
            var weights = new Dictionary<string, int>();
            outputs = side("out_", new[] { 1 }, new[] { 9 }, weights);
            inputs = side("in_", new[] { 1 }, new[] { 20 }, weights);
            w = weightOf(weights);
            c = constants("1/2", "1", "1", "2");
        }

        [Test]
        public void test_runtimeInvariant_effAndQtyMultiplierSweep_alwaysHolds()
        {
            // Given:合法夹具;EFF 经 30 技能系统的等级 0…SKILL_CAP 由求解器插值给出(整数域)
            legalSweepFixture(out RecipeEntry[] outputs, out RecipeEntry[] inputs,
                out Func<ItemKey, int> w, out RecipeSettlementConstants c);
            Fix effMin = c.EffMin, effMax = c.EffMax;
            int checkedPoints = 0;

            // When / Then:双层整数遍历(EFF × 乘子),全程断言不变量成立
            for (int level = 0; level <= c.SkillCap; level++)
            {
                Fix efficiency = RecipeSettlementSolver.Efficiency(c, level);
                Assert.That(efficiency.Raw, Is.InRange(effMin.Raw, effMax.Raw),
                    $"level = {level} ⇒ EFF ∈ [EFF_MIN, EFF_MAX]");

                for (int step = 0; step <= SweepSteps; step++)
                {
                    long raw = c.QtyMultMin.Raw
                        + (c.QtyMultMax.Raw - c.QtyMultMin.Raw) * step / SweepSteps;   // 整数步进
                    Fix multiplier = new Fix(raw);

                    Assert.That(ConservationSolver.RuntimeInvariantHolds(
                            outputs, inputs, w, effMax, efficiency, multiplier), Is.True,
                        $"level = {level}(EFF raw {efficiency.Raw})·乘子 raw {raw} ⇒ 不变量须恒成立");
                    checkedPoints++;
                }
            }

            Assert.That(checkedPoints, Is.EqualTo((c.SkillCap + 1) * (SweepSteps + 1)),
                "扫描点数自证(整数遍历,无浮点插值)");
        }

        [Test]
        public void test_runtimeInvariant_zeroLossPoint_effEqualsEffMax_takesEquality()
        {
            // QA Edge:EFF = EFF_MAX = 1 零损耗取等 —— 实耗 = 基数
            legalSweepFixture(out RecipeEntry[] outputs, out RecipeEntry[] inputs,
                out Func<ItemKey, int> w, out RecipeSettlementConstants c);
            Fix effMax = c.EffMax;

            Assert.That(effMax.Raw, Is.EqualTo(Fix.OneRaw), "夹具自证:EFF_MAX = 1(零损耗档)");
            Assert.That(RecipeSettlementSolver.ActualConsumed(1, effMax), Is.EqualTo(1),
                "满技能 EFF = 1 ⇒ 实耗取等基数(零损耗)");
            Assert.That(RecipeSettlementSolver.Efficiency(c, c.SkillCap).Raw, Is.EqualTo(effMax.Raw),
                "满技能 ⇒ EFF = EFF_MAX");

            // 恰等配方(权重 9/9、乘子 1)⇒ 不等式取等号且成立
            var weights = new Dictionary<string, int>();
            RecipeEntry[] outTight = side("out_", new[] { 1 }, new[] { 9 }, weights);
            RecipeEntry[] inTight = side("in_", new[] { 1 }, new[] { 9 }, weights);
            Assert.That(ConservationSolver.RuntimeInvariantHolds(
                outTight, inTight, weightOf(weights), effMax, effMax, fx("1")), Is.True,
                "恰等 ⇒ 成立(边界含等号)");
        }

        [Test]
        public void test_runtimeInvariant_actualMultiplierBreachesAggregateFixture_violated()
        {
            // 反方向(GDD :535 反例一的运行期侧):同一配方取**实际**乘子 2 ⇒ 9 × 2 = 18 > 1 × 10
            string json = readFixture("invalid_conservation.json");
            RecipeSettlementConstants c = constantsFromFixture(json);
            var weights = new Dictionary<string, int>();
            RecipeEntry[] outputs = side("out_", readIntArray(json, "output_qty"),
                readIntArray(json, "output_weight"), weights);
            RecipeEntry[] inputs = side("in_", readIntArray(json, "input_qty"),
                readIntArray(json, "input_weight"), weights);

            Assert.That(ConservationSolver.RuntimeInvariantHolds(
                outputs, inputs, weightOf(weights), c.EffMax, c.EffMax, fx("1")), Is.True,
                "乘子 1(基数式)⇒ 9 ≤ 10 成立 —— 原稿只看这一格");
            Assert.That(ConservationSolver.RuntimeInvariantHolds(
                outputs, inputs, weightOf(weights), c.EffMax, c.EffMax, fx("2")), Is.False,
                "乘子 2 ⇒ 18 > 10 击穿(三轮 blocking #1 的运行期形态)");
        }

        [Test]
        public void test_runtimeInvariant_effOrEffMaxNonPositive_rejectedWithoutThrow()
        {
            // 域外:EFF ≤ 0 会让 ActualConsumed 抛 ⇒ 本谓词先自检并返回 false,异常不外逸
            legalSweepFixture(out RecipeEntry[] outputs, out RecipeEntry[] inputs,
                out Func<ItemKey, int> w, out RecipeSettlementConstants c);

            bool zeroEff = true, zeroEffMax = true;
            Assert.DoesNotThrow(() =>
                zeroEff = ConservationSolver.RuntimeInvariantHolds(
                    outputs, inputs, w, c.EffMax, new Fix(0L), fx("1")),
                "EFF = 0 ⇒ false,不抛");
            Assert.DoesNotThrow(() =>
                zeroEffMax = ConservationSolver.RuntimeInvariantHolds(
                    outputs, inputs, w, new Fix(0L), c.EffMax, fx("1")),
                "EFF_MAX = 0 ⇒ false,不抛");

            Assert.That(zeroEff, Is.False, "EFF = 0 域外 ⇒ false");
            Assert.That(zeroEffMax, Is.False, "EFF_MAX = 0 域外 ⇒ false");
        }

        [Test]
        public void test_runtimeInvariant_nullWeightFunctionOrEmptySide_rejected()
        {
            legalSweepFixture(out RecipeEntry[] outputs, out RecipeEntry[] inputs,
                out Func<ItemKey, int> w, out RecipeSettlementConstants c);

            Assert.That(ConservationSolver.RuntimeInvariantHolds(
                outputs, inputs, null, c.EffMax, c.EffMax, fx("1")), Is.False, "null 权重函数 ⇒ false");
            Assert.That(ConservationSolver.RuntimeInvariantHolds(
                outputs, null, w, c.EffMax, c.EffMax, fx("1")), Is.False, "null 投入 ⇒ false");
            Assert.That(ConservationSolver.RuntimeInvariantHolds(
                outputs, Array.Empty<RecipeEntry>(), w, c.EffMax, c.EffMax, fx("1")), Is.False,
                "空投入 ⇒ false");
        }

        // ══════════════ AC-21a-64 前提:整数域乘法守卫 ══════════════

        [Test]
        public void test_tryMultiply_overflowingProduct_rejectedWithoutWrap()
        {
            Assert.That(ConservationSolver.TryMultiply(2L, 3L, out long product), Is.True);
            Assert.That(product, Is.EqualTo(6L), "小积精确");

            Assert.That(ConservationSolver.TryMultiply(long.MaxValue, 2L, out long wrapped), Is.False,
                "超 int64 ⇒ false(前提未证,不得回绕)");
            Assert.That(wrapped, Is.EqualTo(0L), "拒时 out 值不承载回绕结果");
        }

        [Test]
        public void test_tryMultiply_negativeOrZeroOperands_guard()
        {
            Assert.That(ConservationSolver.TryMultiply(-1L, 5L, out _), Is.False, "负因子域外 ⇒ false");
            Assert.That(ConservationSolver.TryMultiply(5L, -1L, out _), Is.False, "负因子域外 ⇒ false");
            Assert.That(ConservationSolver.TryMultiply(0L, long.MaxValue, out long zero), Is.True,
                "零因子 ⇒ 积恰 0,无溢出之虞");
            Assert.That(zero, Is.EqualTo(0L));
        }

        // ══════════════ 夹具读取辅助(无 JSON 解析器 —— 手写抽取,承 Story 002/004 同型)══════════════

        /// <summary>读 QA 指定负向夹具(缺文件 = 断言红,不 skip)。</summary>
        private static string readFixture(string fileName)
        {
            string path = Path.Combine(
                repoRoot(), "tests", "unit", "item_database", "fixtures", fileName);
            Assert.That(File.Exists(path), Is.True, $"负向夹具缺失(Story 005 QA 指定):{path}");
            return File.ReadAllText(path);
        }

        private static string repoRoot([CallerFilePath] string thisFile = "")
        {
            string dir = Path.GetDirectoryName(thisFile) ?? ".";
            return Path.GetFullPath(Path.Combine(dir, "..", "..", "..", "..", ".."));
        }

        /// <summary>取夹具中首个 <c>"key": "value"</c> 的字符串值(去引号;Q16.16 字面量在夹具里是字符串,
        /// ADR-014 §四 —— 本辅助不做浮点中转)。</summary>
        private static string readStringField(string json, string key)
        {
            System.Text.RegularExpressions.Match match =
                System.Text.RegularExpressions.Regex.Match(
                    json, "\"" + key + "\"\\s*:\\s*\"([^\"]*)\"");
            Assert.That(match.Success, Is.True, $"夹具缺字符串字段 {key}");
            return match.Groups[1].Value;
        }

        /// <summary>取夹具中 <c>"key": [ 1, 2 ]</c> 的整数数组(扁平平行数组形)。</summary>
        private static int[] readIntArray(string json, string key)
        {
            System.Text.RegularExpressions.Match match =
                System.Text.RegularExpressions.Regex.Match(
                    json, "\"" + key + "\"\\s*:\\s*\\[([^\\]]*)\\]");
            Assert.That(match.Success, Is.True, $"夹具缺数组字段 {key}");

            var values = new List<int>();
            foreach (System.Text.RegularExpressions.Match m in
                System.Text.RegularExpressions.Regex.Matches(match.Groups[1].Value, "(\\d+)"))
                values.Add(int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture));
            return values.ToArray();
        }
    }
}
