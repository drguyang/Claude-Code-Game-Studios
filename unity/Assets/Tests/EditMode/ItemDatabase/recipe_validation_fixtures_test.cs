// 权威来源:
//   Story 006(production/epics/item-database/story-006-recipe-validation-fixtures.md)
//     · AC-21a-7 / 9 / 10 / 11 / 12 / 16 / 17 / 18 / 19 / 20 / 66 —— 11 条构建期校验门
//     · §QA Test Cases = 本文件的测试规格(不发明 QA 之外的用例;每条 Given/When/Then/Edge 至少一测)
//   GDD:design/gdd/item-database.md §Edge Cases 写入期校验族(:755-830)· §Acceptance 组三(:1049-1075)
//   ADR-014 §Decision(负向夹具断言构建期硬失败;本文件经门的错误列表非空断言表达 ——
//     throw 聚合归 Story 008,门本身零 throw)
//
// ⚠️ 落点:故事头登记的账本路径 = tests/unit/item_database/recipe_validation_fixtures_test.cs;
//    Unity 只编译 unity/Assets/ 树 ⇒ 真身 = 本文件(与 Story 001/002/004/005 同一先例)。
//
// ⚠️ 数值纪律:以下常量**全部是测试夹具值,不是游戏平衡值**(GDD §Tuning Knobs「默认」列留空,
//    数值待用户)。边界断言用 FixtureSkillCap / FixtureMaxQuality 常量拼接,**不裸写 60 / 5**
//    (QA 明文:测试随常量表走)。
// ⚠️ 零 UnityEngine / 零外部 I/O(仅读 11 个负向夹具)/ 无随机种子 / 无时间依赖。
// ⚠️ 本工程未装 Newtonsoft(schema_types_primary_key_test.cs :745)—— 夹具一律手写正则抽取。

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using NUnit.Framework;
using DaYiJingCheng.EditorTools.Gates;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Tests.Unit.ItemDatabase
{
    [TestFixture]
    internal sealed class RecipeValidationFixturesTest
    {
        // ══════════════ 夹具小件(数值待用户 —— 此处为测试夹具值)══════════════

        private const int FixtureSkillCap = 60;
        private const int FixtureMaxQuality = 5;

        private static Fix fx(string literal) => FixParse.Parse(literal);

        /// <summary>构造常量表;未点名的字段取中性值(各门只判自己的域,互不串扰)。</summary>
        private static RecipeSettlementConstants constants(
            string qtyMultMin = "1", string qtyMultMax = "2",
            string skillModCap = "0", string qualModCap = "0", string equipModCap = "0",
            string envModMin = "-1/2", string envModMax = "0",
            string retainMin = "1/4", string retainMax = "1",
            string effMin = "1", string effMax = "1",
            int maxQuality = FixtureMaxQuality, int skillCap = FixtureSkillCap)
            => new RecipeSettlementConstants(
                qtyMultMin: fx(qtyMultMin),
                qtyMultMax: fx(qtyMultMax),
                skillModCap: fx(skillModCap),
                qualModCap: fx(qualModCap),
                equipModCap: fx(equipModCap),
                envModMin: fx(envModMin),
                envModMax: fx(envModMax),
                retainMin: fx(retainMin),
                retainMax: fx(retainMax),
                effMin: fx(effMin),
                effMax: fx(effMax),
                maxQuality: maxQuality,
                skillCap: skillCap);

        /// <summary>由夹具构造常量表(缺字段取中性默认;键名 = GDD 旋钮名 —— 本文件属 Tests 装配,扫描面排除)。</summary>
        private static RecipeSettlementConstants constantsFromFixture(string json)
            => constants(
                qtyMultMin: readStringFieldOrDefault(json, "qty_mult_min", "1"),
                qtyMultMax: readStringFieldOrDefault(json, "qty_mult_max", "2"),
                skillModCap: readStringFieldOrDefault(json, "skill_mod_cap", "0"),
                qualModCap: readStringFieldOrDefault(json, "qual_mod_cap", "0"),
                equipModCap: readStringFieldOrDefault(json, "equip_mod_cap", "0"),
                envModMin: readStringFieldOrDefault(json, "env_mod_min", "-1/2"),
                envModMax: readStringFieldOrDefault(json, "env_mod_max", "0"),
                retainMin: readStringFieldOrDefault(json, "retain_min", "1/4"),
                retainMax: readStringFieldOrDefault(json, "retain_max", "1"));

        /// <summary>由夹具的平行数组构造配方项(state 经 Story 002 的 TryParseProcessingState 解析)。</summary>
        private static RecipeEntry[] entries(string[] baseIds, string[] states, int[] qtys)
        {
            Assert.That(baseIds.Length, Is.EqualTo(qtys.Length), "夹具自证:base_ids 与 qty 长度须等");
            Assert.That(states.Length, Is.EqualTo(baseIds.Length), "夹具自证:states 与 base_ids 长度须等");
            var result = new RecipeEntry[qtys.Length];
            for (int i = 0; i < qtys.Length; i++)
            {
                Assert.That(ItemDbValidation.TryParseProcessingState(states[i], out ProcessingState state),
                    Is.True, $"夹具自证:state 闭合 {states[i]}");
                result[i] = new RecipeEntry(new ItemKey(baseIds[i], state), qtys[i]);
            }

            return result;
        }

        private static RecipeEntry entry(string baseId, ProcessingState state, int qty)
            => new RecipeEntry(new ItemKey(baseId, state), qty);

        // ══════════════ AC-21a-7:outputs 逐条 qty ══════════════

        [Test]
        public void test_outputQty_fixtureNonPositive_rejected()
        {
            // Given:QA 指定负向夹具 —— outputs[0].qty = 0(仅一条非法)
            string json = readFixture("invalid_base_qty.json");
            RecipeEntry[] outputs = entries(
                readStringArray(json, "output_base_ids"),
                readStringArray(json, "output_states"),
                readIntArray(json, "output_qty"));
            Assert.That(outputs[0].Qty, Is.EqualTo(0), "夹具自证:产出 qty = 0");

            // When
            IReadOnlyList<string> errors = RecipeValidationGates.ValidateOutputQty(
                outputs, "fixture_base_qty");

            // Then:硬失败(错误列表非空)+ AC 号点名
            Assert.That(errors.Count, Is.GreaterThanOrEqualTo(1), "qty = 0 ⇒ 拒");
            Assert.That(errors[0], Does.Contain("AC-21a-7"));
        }

        [Test]
        public void test_outputQty_singleInvalidNegativeEntry_rejected()
        {
            // QA Edge:仅一条非法(负值形)—— 单条 qty = −n
            RecipeEntry[] outputs = { entry("willow_bark", ProcessingState.Dried, -1) };

            Assert.That(RecipeValidationGates.ValidateOutputQty(outputs, "single_neg"),
                Is.Not.Empty, "单条负 qty ⇒ 拒");
        }

        [Test]
        public void test_outputQty_multipleInvalidEntries_eachNamed()
        {
            // QA Edge:多条非法 —— 0 与 −1 同表,逐条各产一条
            RecipeEntry[] outputs =
            {
                entry("a", ProcessingState.Raw, 0),
                entry("b", ProcessingState.Raw, -2),
            };

            IReadOnlyList<string> errors = RecipeValidationGates.ValidateOutputQty(
                outputs, "multi_bad");

            Assert.That(errors.Count, Is.EqualTo(2), "两条非法 ⇒ 两条错误(逐条判据)");
            Assert.That(errors[0], Does.Contain("outputs[0]"));
            Assert.That(errors[1], Does.Contain("outputs[1]"));
        }

        [Test]
        public void test_outputQty_allPositive_accepted()
        {
            // 正例对照(「只验合法值不算通过」铁律:正反都要有)—— 全部 qty ≥ 1
            RecipeEntry[] outputs =
            {
                entry("willow_bark", ProcessingState.Dried, 1),
                entry("willow_bark", ProcessingState.Dried, 3),
            };

            Assert.That(RecipeValidationGates.ValidateOutputQty(outputs, "all_pos"), Is.Empty,
                "全部 qty ≥ 1 ⇒ 过");
        }

        // ══════════════ AC-21a-9:cap 和(委托 IsSelfConsistent)══════════════

        [Test]
        public void test_capSum_fixtureEnvModPushesOverLimit_rejected()
        {
            // Given:QA Edge 回归点 —— cap 全小、仅 EnvModMax 单独把式子推过界
            string json = readFixture("invalid_cap_sum.json");
            RecipeSettlementConstants c = constantsFromFixture(json);
            Assert.That(c.SkillModCap.Raw + c.QualModCap.Raw + c.EquipModCap.Raw,
                Is.LessThanOrEqualTo(fx("1").Raw), "夹具自证:三 cap 和 = 3/4 ≤ 1(单独不超)");

            // When / Then
            IReadOnlyList<string> errors = RecipeValidationGates.ValidateConstantCapSum(
                c, "const_table_env_push");
            Assert.That(errors.Count, Is.EqualTo(1), "EnvModMax 计入后超限 ⇒ 拒");
            Assert.That(errors[0], Does.Contain("AC-21a-9"));
            Assert.That(errors[0], Does.Contain("EnvModMax"), "错误须点名 EnvModMax 分量");
            Assert.That(errors[0], Does.Contain("QtyMultMax"), "错误须点名右端分量");
        }

        [Test]
        public void test_capSum_exactBoundary_accepted()
        {
            // 正例:左 = 3×1/4 + 1/2 = 5/4;右 = 9/4 − 1 = 5/4 ⇒ 恰等(≤)过
            RecipeSettlementConstants c = constants(
                skillModCap: "1/4", qualModCap: "1/4", equipModCap: "1/4",
                envModMax: "1/2", qtyMultMax: "9/4");

            Assert.That(RecipeValidationGates.ValidateConstantCapSum(c, "boundary"), Is.Empty,
                "恰在边界(≤)⇒ 过(QA:≤ 边界过)");
        }

        [Test]
        public void test_capSum_nonPositiveEnvModCountedAsZero_accepted()
        {
            // QA Edge:EnvModMax ≤ 0 时该项按 0 计(IsSelfConsistent 内建 Positive(),门只信任它)
            RecipeSettlementConstants c = constants(
                skillModCap: "1/4", qualModCap: "1/4", equipModCap: "1/4",
                envModMax: "-1/2", qtyMultMax: "2");

            Assert.That(RecipeValidationGates.ValidateConstantCapSum(c, "env_neg"), Is.Empty,
                "EnvModMax 为负不计入 ⇒ 左 = 3/4 ≤ 1 ⇒ 过");
        }

        [Test]
        public void test_capSum_gateVerdict_equalsSelfConsistentPredicate_sameCriterionAsAc3()
        {
            // 与 AC-3 正反同判据:门空 ⇔ IsSelfConsistent true(同一谓词的两面,单一实现)
            string badJson = readFixture("invalid_cap_sum.json");
            RecipeSettlementConstants bad = constantsFromFixture(badJson);
            RecipeSettlementConstants good = constants(
                skillModCap: "1/4", qualModCap: "1/4", equipModCap: "1/4",
                envModMax: "1/2", qtyMultMax: "9/4");

            Assert.That(
                RecipeValidationGates.ValidateConstantCapSum(bad, "pair_bad").Count == 0,
                Is.EqualTo(RecipeSettlementConstantTableValidator.IsSelfConsistent(bad)),
                "反例:门与谓词同号(均判坏)");
            Assert.That(
                RecipeValidationGates.ValidateConstantCapSum(good, "pair_good").Count == 0,
                Is.EqualTo(RecipeSettlementConstantTableValidator.IsSelfConsistent(good)),
                "正例:门与谓词同号(均判好)");
            Assert.That(RecipeSettlementConstantTableValidator.IsSelfConsistent(bad), Is.False);
            Assert.That(RecipeSettlementConstantTableValidator.IsSelfConsistent(good), Is.True);
        }

        // ══════════════ AC-21a-10:QTY_MULT 区间 ══════════════

        [Test]
        public void test_qtyRange_fixtureEqualBounds_rejected()
        {
            // Given:QA Given 主案 —— MIN = MAX(**含等号**即拒)
            string json = readFixture("invalid_qty_range.json");
            RecipeSettlementConstants c = constantsFromFixture(json);
            Assert.That(c.QtyMultMin.Raw, Is.EqualTo(c.QtyMultMax.Raw), "夹具自证:相等");

            // When / Then
            IReadOnlyList<string> errors = RecipeValidationGates.ValidateQtyMultRange(
                c, "qty_equal");
            Assert.That(errors.Count, Is.EqualTo(1), "相等 ⇒ 拒(含等号!)");
            Assert.That(errors[0], Does.Contain("AC-21a-10"));
        }

        [Test]
        public void test_qtyRange_minAboveMax_rejected()
        {
            // QA Given 第二表 —— MIN > MAX(代码构造的兄弟案)
            RecipeSettlementConstants c = constants(qtyMultMin: "2", qtyMultMax: "1");

            IReadOnlyList<string> errors = RecipeValidationGates.ValidateQtyMultRange(
                c, "qty_inverted");
            Assert.That(errors.Count, Is.EqualTo(1), "MIN > MAX ⇒ 拒");
            Assert.That(errors[0], Does.Contain("AC-21a-10"));
        }

        [Test]
        public void test_qtyRange_differByOneRawUnit_accepted()
        {
            // QA Edge:差一个最小单位(Fix raw 差 1)⇒ 过
            long maxRaw = fx("2").Raw;
            var nearEqual = new RecipeSettlementConstants(
                qtyMultMin: new Fix(maxRaw - 1), qtyMultMax: fx("2"),
                skillModCap: fx("0"), qualModCap: fx("0"), equipModCap: fx("0"),
                envModMin: fx("-1/2"), envModMax: fx("0"),
                retainMin: fx("1/4"), retainMax: fx("1"),
                effMin: fx("1"), effMax: fx("1"),
                maxQuality: FixtureMaxQuality, skillCap: FixtureSkillCap);

            Assert.That(nearEqual.QtyMultMax.Raw - nearEqual.QtyMultMin.Raw, Is.EqualTo(1),
                "夹具自证:raw 差恰 1");
            Assert.That(RecipeValidationGates.ValidateQtyMultRange(nearEqual, "raw_diff_1"),
                Is.Empty, "差一个最小单位 ⇒ 过");
        }

        [Test]
        public void test_qtyRange_minBelowMax_accepted()
        {
            // 正例对照:常规区间
            RecipeSettlementConstants c = constants(qtyMultMin: "1", qtyMultMax: "2");
            Assert.That(RecipeValidationGates.ValidateQtyMultRange(c, "qty_ok"), Is.Empty,
                "MIN < MAX ⇒ 过");
        }

        // ══════════════ AC-21a-11:RETAIN 三条件 ══════════════

        [Test]
        public void test_retain_fixtureMinAboveMax_rejected()
        {
            // 条件一:RETAIN_MIN > RETAIN_MAX(夹具子记录 0)
            (RecipeSettlementConstants c, string caseName) = retainCase(0);
            Assert.That(caseName, Is.EqualTo("min_above_max"), "夹具自证:子记录 0 = 条件一");

            IReadOnlyList<string> errors = RecipeValidationGates.ValidateRetainRange(
                c, "retain_inverted");
            Assert.That(errors.Count, Is.EqualTo(1), "区间反向 ⇒ 拒(条件一,恰一条)");
            Assert.That(errors[0], Does.Contain("AC-21a-11 条件一"));
        }

        [Test]
        public void test_retain_fixtureMaxAboveOne_rejected()
        {
            // 条件二:RETAIN_MAX > 1(clamp 会掩盖,必须显式拒)—— 夹具子记录 1
            (RecipeSettlementConstants c, string caseName) = retainCase(1);
            Assert.That(caseName, Is.EqualTo("max_above_one"), "夹具自证:子记录 1 = 条件二");

            IReadOnlyList<string> errors = RecipeValidationGates.ValidateRetainRange(
                c, "retain_over_one");
            Assert.That(errors.Count, Is.EqualTo(1), "> 1 ⇒ 拒(条件二,恰一条)");
            Assert.That(errors[0], Does.Contain("AC-21a-11 条件二"));
        }

        [Test]
        public void test_retain_fixtureMinNonPositive_rejected()
        {
            // 条件三:RETAIN_MIN ≤ 0(QA Edge:恰 = 0 亦拒)—— 夹具子记录 2
            (RecipeSettlementConstants c, string caseName) = retainCase(2);
            Assert.That(caseName, Is.EqualTo("min_non_positive"), "夹具自证:子记录 2 = 条件三");

            IReadOnlyList<string> errors = RecipeValidationGates.ValidateRetainRange(
                c, "retain_zero");
            Assert.That(errors.Count, Is.EqualTo(1), "≤ 0 ⇒ 拒(条件三,恰一条)");
            Assert.That(errors[0], Does.Contain("AC-21a-11 条件三"));
        }

        [Test]
        public void test_retain_maxExactlyOne_accepted()
        {
            // QA Edge:RETAIN_MAX 恰 = 1(满技能全保)⇒ 过
            RecipeSettlementConstants c = constants(retainMin: "1/4", retainMax: "1");
            Assert.That(RecipeValidationGates.ValidateRetainRange(c, "retain_one"), Is.Empty,
                "0 < min ≤ max = 1 ⇒ 过");
        }

        [Test]
        public void test_retain_minEqualsMax_accepted()
        {
            // QA Edge:RETAIN_MIN = RETAIN_MAX(区间退化点,GDD 只拒 >,不收紧)⇒ 过
            RecipeSettlementConstants c = constants(retainMin: "1/2", retainMax: "1/2");
            Assert.That(RecipeValidationGates.ValidateRetainRange(c, "retain_point"), Is.Empty,
                "min = max > 0 ≤ 1 ⇒ 过");
        }

        // ══════════════ AC-21a-12:ENV_MOD 区间 ══════════════

        [Test]
        public void test_envRange_fixtureInverted_rejected()
        {
            // Given:QA 指定负向夹具 —— MIN > MAX 倒置
            string json = readFixture("invalid_env_range.json");
            RecipeSettlementConstants c = constantsFromFixture(json);

            // When / Then
            IReadOnlyList<string> errors = RecipeValidationGates.ValidateEnvModRange(
                c, "env_inverted");
            Assert.That(errors.Count, Is.EqualTo(1), "倒置 ⇒ 拒");
            Assert.That(errors[0], Does.Contain("AC-21a-12"));
        }

        [Test]
        public void test_envRange_equalBounds_accepted()
        {
            // QA Edge:相等**过**(GDD 只拒 >)
            RecipeSettlementConstants c = constants(envModMin: "-1/2", envModMax: "-1/2");
            Assert.That(RecipeValidationGates.ValidateEnvModRange(c, "env_equal"), Is.Empty,
                "MIN = MAX ⇒ 按现文过(不擅自收紧)");
        }

        [Test]
        public void test_envRange_allNegative_accepted()
        {
            // QA Edge:全负区间过(火候难控、背阴合法,由产出乘子下界兜底)
            RecipeSettlementConstants c = constants(envModMin: "-1", envModMax: "-1/2");
            Assert.That(RecipeValidationGates.ValidateEnvModRange(c, "env_neg"), Is.Empty,
                "全负且有序 ⇒ 过");
        }

        [Test]
        public void test_envRange_crossingZero_accepted()
        {
            // QA Edge:跨零过(负环境修正合法)
            RecipeSettlementConstants c = constants(envModMin: "-1/2", envModMax: "1/2");
            Assert.That(RecipeValidationGates.ValidateEnvModRange(c, "env_cross"), Is.Empty,
                "跨零且有序 ⇒ 过");
        }

        // ══════════════ AC-21a-16:配方项 qty 双侧 + 空侧 ══════════════

        [Test]
        public void test_recipeQty_fixtureInputsZero_rejected()
        {
            // Given:QA 主案 —— inputs[0].qty = 0
            string json = readFixture("invalid_recipe_qty.json");
            RecipeEntry[] inputs = entries(
                readStringArray(json, "input_base_ids"),
                readStringArray(json, "input_states"),
                readIntArray(json, "input_qty"));
            RecipeEntry[] outputs = entries(
                readStringArray(json, "output_base_ids"),
                readStringArray(json, "output_states"),
                readIntArray(json, "output_qty"));
            Assert.That(inputs[0].Qty, Is.EqualTo(0), "夹具自证:输入 qty = 0");

            // When / Then
            IReadOnlyList<string> errors = RecipeValidationGates.ValidateRecipeEntryQuantities(
                inputs, outputs, "fixture_recipe_qty");
            Assert.That(errors.Count, Is.EqualTo(1), "输入 qty = 0 ⇒ 拒(输出侧合法不产错)");
            Assert.That(errors[0], Does.Contain("AC-21a-16"));
        }

        [Test]
        public void test_recipeQty_outputsNegative_rejected()
        {
            // QA Given 轮换案 —— outputs[0].qty = −1(代码构造)
            RecipeEntry[] inputs = { entry("willow_bark", ProcessingState.Raw, 1) };
            RecipeEntry[] outputs = { entry("willow_bark", ProcessingState.Dried, -1) };

            IReadOnlyList<string> errors = RecipeValidationGates.ValidateRecipeEntryQuantities(
                inputs, outputs, "out_neg");
            Assert.That(errors.Count, Is.EqualTo(1), "输出 qty = −1 ⇒ 拒");
            Assert.That(errors[0], Does.Contain("AC-21a-16"));
            Assert.That(errors[0], Does.Contain("outputs[0]"));
        }

        [Test]
        public void test_recipeQty_emptyInputsOrEmptyOutputs_rejected()
        {
            // QA Edge:空 inputs / 空 outputs 独立拒(GDD §Edge Cases :759/:760:凭空造物 / 销毁不归配方表)
            RecipeEntry[] oneIn = { entry("willow_bark", ProcessingState.Raw, 1) };
            RecipeEntry[] oneOut = { entry("willow_bark", ProcessingState.Dried, 1) };

            IReadOnlyList<string> emptyInputs = RecipeValidationGates.ValidateRecipeEntryQuantities(
                Array.Empty<RecipeEntry>(), oneOut, "empty_in");
            Assert.That(emptyInputs.Count, Is.EqualTo(1), "空 inputs ⇒ 拒(凭空造物)");
            Assert.That(emptyInputs[0], Does.Contain("inputs 为空"));

            IReadOnlyList<string> emptyOutputs = RecipeValidationGates.ValidateRecipeEntryQuantities(
                oneIn, Array.Empty<RecipeEntry>(), "empty_out");
            Assert.That(emptyOutputs.Count, Is.EqualTo(1), "空 outputs ⇒ 拒(销毁不归配方表)");
            Assert.That(emptyOutputs[0], Does.Contain("outputs 为空"));

            IReadOnlyList<string> bothEmpty = RecipeValidationGates.ValidateRecipeEntryQuantities(
                Array.Empty<RecipeEntry>(), Array.Empty<RecipeEntry>(), "empty_both");
            Assert.That(bothEmpty.Count, Is.EqualTo(2), "双空 ⇒ 两条");
        }

        [Test]
        public void test_recipeQty_allPositiveBothSides_accepted()
        {
            // 正例对照:双侧全 ≥ 1
            RecipeEntry[] inputs = { entry("willow_bark", ProcessingState.Raw, 3) };
            RecipeEntry[] outputs = { entry("willow_bark", ProcessingState.Dried, 1) };

            Assert.That(RecipeValidationGates.ValidateRecipeEntryQuantities(
                inputs, outputs, "both_ok"), Is.Empty, "双侧非空且全 ≥ 1 ⇒ 过");
        }

        // ══════════════ AC-21a-17:item_key 外键悬空 ══════════════

        private static HashSet<ItemKey> knownKeysFromFixture(string json)
        {
            string[] baseIds = readStringArray(json, "known_base_ids");
            string[] states = readStringArray(json, "known_states");
            Assert.That(states.Length, Is.EqualTo(baseIds.Length), "夹具自证:known 平行数组等长");

            var known = new HashSet<ItemKey>();
            for (int i = 0; i < baseIds.Length; i++)
            {
                Assert.That(ItemDbValidation.TryParseProcessingState(states[i], out ProcessingState state),
                    Is.True, $"夹具自证:known state 闭合 {states[i]}");
                known.Add(new ItemKey(baseIds[i], state));
            }

            return known;
        }

        [Test]
        public void test_fk_fixtureBaseExistsButStateDangling_rejected()
        {
            // Given:QA 主案 —— base 存在但 state 不成条目(willow_bark/extracted 未登记)
            string json = readFixture("invalid_recipe_fk.json");
            HashSet<ItemKey> known = knownKeysFromFixture(json);
            RecipeEntry[] inputs = entries(
                readStringArray(json, "input_base_ids"),
                readStringArray(json, "input_states"),
                readIntArray(json, "input_qty"));
            RecipeEntry[] outputs = entries(
                readStringArray(json, "output_base_ids"),
                readStringArray(json, "output_states"),
                readIntArray(json, "output_qty"));
            Assert.That(known.Contains(new ItemKey("willow_bark", ProcessingState.Raw)), Is.True,
                "夹具自证:willow_bark/raw 在已知集合");
            Assert.That(known.Contains(new ItemKey("willow_bark", ProcessingState.Extracted)), Is.False,
                "夹具自证:willow_bark/extracted 不在已知集合");

            // When / Then:inputs 侧悬空;outputs 侧(salicylic/extracted)在集合内不产错
            IReadOnlyList<string> errors = RecipeValidationGates.ValidateRecipeForeignKeys(
                inputs, outputs, known, "fk_state");
            Assert.That(errors.Count, Is.EqualTo(1), "仅 state 错 ⇒ 恰一条(inputs 侧)");
            Assert.That(errors[0], Does.Contain("AC-21a-17"));
            Assert.That(errors[0], Does.Contain("inputs[0]"));
        }

        [Test]
        public void test_fk_danglingBaseId_rejected()
        {
            // QA Edge:仅 base 错(整个 base_id 未登记)
            HashSet<ItemKey> known = new HashSet<ItemKey>
            {
                new ItemKey("willow_bark", ProcessingState.Raw),
            };
            RecipeEntry[] inputs = { entry("unknown_herb", ProcessingState.Raw, 1) };
            RecipeEntry[] outputs = { entry("willow_bark", ProcessingState.Raw, 1) };

            IReadOnlyList<string> errors = RecipeValidationGates.ValidateRecipeForeignKeys(
                inputs, outputs, known, "fk_base");
            Assert.That(errors.Count, Is.EqualTo(1), "base 错 ⇒ 拒");
            Assert.That(errors[0], Does.Contain("unknown_herb"));
            Assert.That(errors[0], Does.Contain("AC-21a-17"));
        }

        [Test]
        public void test_fk_outputsSideDangling_rejected()
        {
            // QA Given:outputs 侧轮换(双向都查)
            HashSet<ItemKey> known = new HashSet<ItemKey>
            {
                new ItemKey("willow_bark", ProcessingState.Raw),
            };
            RecipeEntry[] inputs = { entry("willow_bark", ProcessingState.Raw, 1) };
            RecipeEntry[] outputs = { entry("willow_bark", ProcessingState.Extracted, 1) };

            IReadOnlyList<string> errors = RecipeValidationGates.ValidateRecipeForeignKeys(
                inputs, outputs, known, "fk_out");
            Assert.That(errors.Count, Is.EqualTo(1), "outputs 侧悬空 ⇒ 拒(双向都查)");
            Assert.That(errors[0], Does.Contain("outputs[0]"));
        }

        [Test]
        public void test_fk_crossBaseRecipe_bothSidesExist_accepted()
        {
            // QA Edge:跨 base 配方(柳树皮 → 水杨酸)本身合法 —— 外键各自独立存在即过
            HashSet<ItemKey> known = new HashSet<ItemKey>
            {
                new ItemKey("willow_bark", ProcessingState.Raw),
                new ItemKey("salicylic_acid", ProcessingState.Extracted),
            };
            RecipeEntry[] inputs = { entry("willow_bark", ProcessingState.Raw, 3) };
            RecipeEntry[] outputs = { entry("salicylic_acid", ProcessingState.Extracted, 1) };

            Assert.That(RecipeValidationGates.ValidateRecipeForeignKeys(
                inputs, outputs, known, "fk_cross"), Is.Empty, "跨 base 且两侧键均在集合 ⇒ 过");
        }

        [Test]
        public void test_fk_caseSensitiveBaseId_variantCasingDangling_rejected()
        {
            // QA Edge:大小写敏感(ItemKey 序数比较 —— 不同字面量 = 不同键)
            HashSet<ItemKey> known = new HashSet<ItemKey>
            {
                new ItemKey("willow_bark", ProcessingState.Raw),
            };
            RecipeEntry[] inputs = { entry("Willow_Bark", ProcessingState.Raw, 1) };
            RecipeEntry[] outputs = { entry("willow_bark", ProcessingState.Raw, 1) };

            IReadOnlyList<string> errors = RecipeValidationGates.ValidateRecipeForeignKeys(
                inputs, outputs, known, "fk_case");
            Assert.That(errors.Count, Is.EqualTo(1), "大小写不同字面量 = 不同键 ⇒ 悬空拒");
            Assert.That(errors[0], Does.Contain("AC-21a-17"));
        }

        // ══════════════ AC-21a-18:duration_ticks ══════════════

        [Test]
        public void test_duration_fixtureZero_rejected()
        {
            // Given:QA 主案 —— duration_ticks = 0
            string json = readFixture("invalid_duration.json");
            int duration = readIntField(json, "duration_ticks");
            Assert.That(duration, Is.EqualTo(0), "夹具自证:duration = 0");

            // When / Then
            IReadOnlyList<string> errors = RecipeValidationGates.ValidateDurationTicks(
                duration, "fixture_duration");
            Assert.That(errors.Count, Is.EqualTo(1), "0 ⇒ 拒");
            Assert.That(errors[0], Does.Contain("AC-21a-18"));
        }

        [Test]
        public void test_duration_negative_rejected()
        {
            // QA Given 轮换案 —— 负值
            IReadOnlyList<string> errors = RecipeValidationGates.ValidateDurationTicks(
                -5, "neg");
            Assert.That(errors.Count, Is.EqualTo(1), "负 tick ⇒ 拒");
            Assert.That(errors[0], Does.Contain("AC-21a-18"));
        }

        [Test]
        public void test_duration_singleTick_accepted()
        {
            // QA Then:= 1(过,单 tick 配方)
            Assert.That(RecipeValidationGates.ValidateDurationTicks(1, "one_tick"), Is.Empty,
                "= 1 ⇒ 过");
            Assert.That(RecipeValidationGates.ValidateDurationTicks(int.MaxValue, "max_tick"),
                Is.Empty, "极大正 tick ⇒ 过(溢出归 AC-21a-64 族,不在本门)");
        }

        // ══════════════ AC-21a-19:skill_gate 区间 ══════════════

        [Test]
        public void test_skillGate_fixtureNegative_rejected()
        {
            // Given:QA 主案 —— skill_gate = −1(夹具)
            string json = readFixture("invalid_skill_gate.json");
            int gate = readIntField(json, "skill_gate");
            Assert.That(gate, Is.EqualTo(-1), "夹具自证:skill_gate = −1");
            RecipeSettlementConstants c = constants(); // SkillCap = FixtureSkillCap(不写死 60)

            // When / Then
            IReadOnlyList<string> errors = RecipeValidationGates.ValidateSkillGate(
                gate, c, "fixture_skill_gate");
            Assert.That(errors.Count, Is.EqualTo(1), "−1 ⇒ 拒");
            Assert.That(errors[0], Does.Contain("AC-21a-19"));
        }

        [Test]
        public void test_skillGate_aboveSkillCap_rejected()
        {
            // QA Given 轮换案 —— SKILL_CAP+1(边界用 FixtureSkillCap 拼,随常量表走)
            RecipeSettlementConstants c = constants();

            IReadOnlyList<string> errors = RecipeValidationGates.ValidateSkillGate(
                FixtureSkillCap + 1, c, "gate_over");
            Assert.That(errors.Count, Is.EqualTo(1), "SkillCap+1 ⇒ 拒");
            Assert.That(errors[0], Does.Contain("AC-21a-19"));
            Assert.That(errors[0], Does.Contain(FixtureSkillCap.ToString()),
                "错误点名注入的 SkillCap 值");
        }

        [Test]
        public void test_skillGate_zero_accepted()
        {
            // QA Edge:skill_gate = 0(无门槛)⇒ 过
            RecipeSettlementConstants c = constants();
            Assert.That(RecipeValidationGates.ValidateSkillGate(0, c, "gate_zero"), Is.Empty,
                "= 0 ⇒ 过");
        }

        [Test]
        public void test_skillGate_atSkillCap_accepted()
        {
            // QA Then:恰在上界 ⇒ 过(边界含等号)
            RecipeSettlementConstants c = constants();
            Assert.That(RecipeValidationGates.ValidateSkillGate(
                FixtureSkillCap, c, "gate_at_cap"), Is.Empty, "= SkillCap ⇒ 过");
        }

        // ══════════════ AC-21a-20:min_quality 准入闸 ══════════════

        [Test]
        public void test_minQuality_fixtureZero_rejected()
        {
            // Given:QA 主案 —— min_quality = 0
            string json = readFixture("invalid_min_quality.json");
            int minQuality = readIntField(json, "min_quality");
            Assert.That(minQuality, Is.EqualTo(0), "夹具自证:min_quality = 0");
            RecipeSettlementConstants c = constants();

            // When / Then
            IReadOnlyList<string> errors = RecipeValidationGates.ValidateMinQuality(
                minQuality, c, "fixture_min_quality");
            Assert.That(errors.Count, Is.EqualTo(1), "< 1 ⇒ 拒");
            Assert.That(errors[0], Does.Contain("AC-21a-20"));
        }

        [Test]
        public void test_minQuality_aboveMaxQuality_rejected()
        {
            // QA Given 轮换案 —— MAX_QUALITY+1(边界用 FixtureMaxQuality 拼,数值待用户)
            RecipeSettlementConstants c = constants();

            IReadOnlyList<string> errors = RecipeValidationGates.ValidateMinQuality(
                FixtureMaxQuality + 1, c, "quality_over");
            Assert.That(errors.Count, Is.EqualTo(1), "MaxQuality+1 ⇒ 拒");
            Assert.That(errors[0], Does.Contain("AC-21a-20"));
        }

        [Test]
        public void test_minQuality_one_accepted()
        {
            // QA Then:= 1(过)—— 品级下限恒 ≥ 1
            RecipeSettlementConstants c = constants();
            Assert.That(RecipeValidationGates.ValidateMinQuality(1, c, "quality_one"), Is.Empty,
                "= 1 ⇒ 过");
        }

        [Test]
        public void test_minQuality_atMaxQuality_accepted()
        {
            // QA Then:= MAX_QUALITY(过,边界含等号)
            RecipeSettlementConstants c = constants();
            Assert.That(RecipeValidationGates.ValidateMinQuality(
                FixtureMaxQuality, c, "quality_at_max"), Is.Empty, "= MaxQuality ⇒ 过");
        }

        // ══════════════ AC-21a-66:owner 三子集划分 ══════════════

        [Test]
        public void test_ownerPartition_fixtureMissingNullAndOutOfSet_rejected()
        {
            // Given:QA 指定负向夹具 —— 两条 null(缺字段同罚)+ 一条枚举外 "Process"
            string json = readFixture("invalid_recipe_owner.json");
            string[] owners = readOwnerLiterals(json, "owners");
            Assert.That(owners.Length, Is.EqualTo(6), "夹具自证:6 条记录");
            Assert.That(owners[1], Is.Null, "夹具自证:记录 1 = JSON null");
            Assert.That(owners[4], Is.EqualTo("Process"), "夹具自证:记录 4 = 枚举外字面量");

            // When
            IReadOnlyList<string> errors = RecipeValidationGates.PartitionRecipeOwners(
                owners, out IReadOnlyList<int> process, out IReadOnlyList<int> craft,
                out IReadOnlyList<int> build);

            // Then:硬失败 —— 2 条 null + 1 条枚举外 = 3 条错误
            Assert.That(errors.Count, Is.EqualTo(3), "null×2 + 枚举外×1 ⇒ 3 条");
            Assert.That(errors[0], Does.Contain("AC-21a-66"));
            Assert.That(errors[1], Does.Contain("AC-21a-66"));
            Assert.That(errors[2], Does.Contain("AC-21a-66"));
            Assert.That(errors[2], Does.Contain("Process"), "枚举外字面量点名");
            // 合法三条仍完成划分(部分结果)
            Assert.That(process.Count + craft.Count + build.Count, Is.EqualTo(3),
                "三条合法记录照常入子集");
        }

        [Test]
        public void test_ownerPartition_nullLiteralOnly_rejected()
        {
            // QA Edge:owner 字段存在但为 null(缺字段同罚)
            IReadOnlyList<string> errors = RecipeValidationGates.PartitionRecipeOwners(
                new string[] { null }, out _, out _, out _);

            Assert.That(errors.Count, Is.EqualTo(1), "单条 null ⇒ 拒");
            Assert.That(errors[0], Does.Contain("AC-21a-66"));
        }

        [Test]
        public void test_ownerPartition_caseMisspelledLiteral_rejected()
        {
            // 枚举外字面量(序数精确 —— "Process" ≠ "process";同 AC-21a-22 闭合口径)
            IReadOnlyList<string> errors = RecipeValidationGates.PartitionRecipeOwners(
                new[] { "Process" }, out _, out _, out _);

            Assert.That(errors.Count, Is.EqualTo(1), "大小写不符 ⇒ 拒");
            Assert.That(errors[0], Does.Contain("AC-21a-66"));
        }

        [Test]
        public void test_ownerPartition_threeSubsets_unionEqualsFullTablePairwiseDisjoint()
        {
            // QA Then 三性质:并 = 全表、两两交 = ∅、每条恰归一个子集
            string[] owners = { "process", "craft", "build", "craft" };

            IReadOnlyList<string> errors = RecipeValidationGates.PartitionRecipeOwners(
                owners, out IReadOnlyList<int> process, out IReadOnlyList<int> craft,
                out IReadOnlyList<int> build);

            // When/Then
            Assert.That(errors, Is.Empty, "全合法 ⇒ 无错");

            var union = new HashSet<int>(process);
            union.UnionWith(craft);
            union.UnionWith(build);
            Assert.That(union.Count, Is.EqualTo(owners.Length), "并 = 全表(下标覆盖)");
            Assert.That(process.Count + craft.Count + build.Count, Is.EqualTo(owners.Length),
                "三子集计数和 = 全表(无重复计入 ⇒ 两两交 = ∅ 的计数形态)");

            // 两两交显式断言(QA 要求逐条断言,不靠计数平凡性)
            Assert.That(intersect(process, craft), Is.Empty, "process ∩ craft = ∅");
            Assert.That(intersect(process, build), Is.Empty, "process ∩ build = ∅");
            Assert.That(intersect(craft, build), Is.Empty, "craft ∩ build = ∅");

            // 具名抽查
            Assert.That(process, Does.Contain(0));
            Assert.That(craft, Does.Contain(1));
            Assert.That(build, Does.Contain(2));
        }

        [Test]
        public void test_ownerPartition_emptySubset_accepted()
        {
            // QA Edge:单子集空表合法 —— build 无记录,并仍等全表
            string[] owners = { "process", "process" };

            IReadOnlyList<string> errors = RecipeValidationGates.PartitionRecipeOwners(
                owners, out IReadOnlyList<int> process, out IReadOnlyList<int> craft,
                out IReadOnlyList<int> build);

            Assert.That(errors, Is.Empty, "空 build 子集 ⇒ 合法");
            Assert.That(build, Is.Empty, "build 子集空");
            Assert.That(craft, Is.Empty, "craft 子集空");
            Assert.That(process.Count, Is.EqualTo(2), "并 = 全表(2 条 process)");
        }

        [Test]
        public void test_ownerPartition_indexPartitionMutuallyExclusive_regardlessOfRecipeId()
        {
            // QA Edge:重复 recipe_id 跨子集 —— 唯一性归 recipe_id 校验(Story 002 AC-21a-21),
            // 本门只保证**划分互斥**:同 id 两记录分入不同子集时下标仍各归恰一个
            // (夹具 recipe_ids[0] 与 [4] 可视为同 id 的两条记录形态;此处按字面量序列断言互斥性本身)
            string[] owners = { "process", "craft", "process" };

            IReadOnlyList<string> errors = RecipeValidationGates.PartitionRecipeOwners(
                owners, out IReadOnlyList<int> process, out IReadOnlyList<int> craft,
                out IReadOnlyList<int> build);

            Assert.That(errors, Is.Empty);
            var seen = new HashSet<int>();
            foreach (int idx in process) Assert.That(seen.Add(idx), Is.True, "下标不重复");
            foreach (int idx in craft) Assert.That(seen.Add(idx), Is.True, "下标不重复");
            foreach (int idx in build) Assert.That(seen.Add(idx), Is.True, "下标不重复");
            Assert.That(seen.Count, Is.EqualTo(owners.Length), "每个下标恰入一个子集(互斥 + 全覆盖)");
        }

        // ══════════════ 夹具读取辅助(无 JSON 解析器 —— 手写抽取,承 Story 002/004/005 同型)══════════════

        /// <summary>读 QA 指定负向夹具(缺文件 = 断言红,不 skip)。</summary>
        private static string readFixture(string fileName)
        {
            string path = Path.Combine(
                repoRoot(), "tests", "unit", "item_database", "fixtures", fileName);
            Assert.That(File.Exists(path), Is.True, $"负向夹具缺失(Story 006 QA 指定):{path}");
            return File.ReadAllText(path);
        }

        private static string repoRoot([CallerFilePath] string thisFile = "")
        {
            string dir = Path.GetDirectoryName(thisFile) ?? ".";
            return Path.GetFullPath(Path.Combine(dir, "..", "..", "..", "..", ".."));
        }

        /// <summary>取夹具中首个 <c>"key": "value"</c> 的字符串值(去引号;Fix 字面量是字符串,ADR-014 §四)。</summary>
        private static string readStringField(string json, string key)
        {
            Match match = Regex.Match(json, "\"" + key + "\"\\s*:\\s*\"([^\"]*)\"");
            Assert.That(match.Success, Is.True, $"夹具缺字符串字段 {key}");
            return match.Groups[1].Value;
        }

        /// <summary>同 <see cref="readStringField"/>,缺字段时回中性默认(常量表夹具只点名被罚字段)。</summary>
        private static string readStringFieldOrDefault(string json, string key, string fallback)
        {
            Match match = Regex.Match(json, "\"" + key + "\"\\s*:\\s*\"([^\"]*)\"");
            return match.Success ? match.Groups[1].Value : fallback;
        }

        /// <summary>取夹具中标量整数字段(支持负号,如 skill_gate = −1)。</summary>
        private static int readIntField(string json, string key)
        {
            Match match = Regex.Match(json, "\"" + key + "\"\\s*:\\s*(-?\\d+)");
            Assert.That(match.Success, Is.True, $"夹具缺整数字段 {key}");
            return int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>取夹具中 <c>"key": [ 1, -2 ]</c> 的**带符号**整数数组(平行数组形)。</summary>
        private static int[] readIntArray(string json, string key)
        {
            Match match = Regex.Match(json, "\"" + key + "\"\\s*:\\s*\\[([^\\]]*)\\]");
            Assert.That(match.Success, Is.True, $"夹具缺数组字段 {key}");

            var values = new List<int>();
            foreach (Match m in Regex.Matches(match.Groups[1].Value, "(-?\\d+)"))
                values.Add(int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture));
            return values.ToArray();
        }

        /// <summary>取夹具中 <c>"key": [ "a", "b" ]</c> 的字符串数组(去引号)。</summary>
        private static string[] readStringArray(string json, string key)
        {
            Match match = Regex.Match(json, "\"" + key + "\"\\s*:\\s*\\[([^\\]]*)\\]");
            Assert.That(match.Success, Is.True, $"夹具缺字符串数组字段 {key}");

            var values = new List<string>();
            foreach (Match m in Regex.Matches(match.Groups[1].Value, "\"([^\"]*)\""))
                values.Add(m.Groups[1].Value);
            return values.ToArray();
        }

        /// <summary>取夹具中 owner 字面量数组 —— **保留 JSON null 为 C# null**(raw 两层缝的入参形)。</summary>
        private static string[] readOwnerLiterals(string json, string key)
        {
            Match match = Regex.Match(json, "\"" + key + "\"\\s*:\\s*\\[([^\\]]*)\\]");
            Assert.That(match.Success, Is.True, $"夹具缺 owner 数组字段 {key}");

            var values = new List<string>();
            foreach (string token in match.Groups[1].Value.Split(','))
            {
                string t = token.Trim();
                if (t == "null")
                {
                    values.Add(null);
                }
                else if (t.Length >= 2 && t[0] == '"' && t[t.Length - 1] == '"')
                {
                    values.Add(t.Substring(1, t.Length - 2));
                }
                else if (t.Length > 0)
                {
                    Assert.Fail($"owner 数组含意外 token:{t}");
                }
            }

            return values.ToArray();
        }

        /// <summary>从 invalid_retain.json 的三条子记录取第 index 条的常量表 + case 名。</summary>
        private static (RecipeSettlementConstants constants, string caseName) retainCase(int index)
        {
            string json = readFixture("invalid_retain.json");
            string[] mins = readAllStringFields(json, "retain_min");
            string[] maxs = readAllStringFields(json, "retain_max");
            string[] names = readAllStringFields(json, "case");
            Assert.That(mins.Length, Is.EqualTo(3), "夹具自证:三条子记录");
            Assert.That(maxs.Length, Is.EqualTo(3), "夹具自证:三条子记录");
            Assert.That(names.Length, Is.EqualTo(3), "夹具自证:三条子记录");

            return (constants(retainMin: mins[index], retainMax: maxs[index]), names[index]);
        }

        /// <summary>取夹具中某字符串字段的**全部**出现值(子记录轮换形)。</summary>
        private static string[] readAllStringFields(string json, string key)
        {
            var values = new List<string>();
            foreach (Match m in Regex.Matches(json, "\"" + key + "\"\\s*:\\s*\"([^\"]*)\""))
                values.Add(m.Groups[1].Value);
            return values.ToArray();
        }

        /// <summary>两个下标子集的交(测试侧断言两两交 = ∅ 用)。</summary>
        private static HashSet<int> intersect(IReadOnlyList<int> a, IReadOnlyList<int> b)
        {
            var set = new HashSet<int>(a);
            set.IntersectWith(b);
            return set;
        }
    }
}
