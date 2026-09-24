// 权威来源:
//   Story 007(production/epics/item-database/story-007-item-validation-fixtures.md)
//     · AC-21a-13 / 14 / 15 / 23 / 24 / 25 —— 6 条构建期校验门(AC-25 拆 a/b 两门)
//     · §QA Test Cases = 本文件的测试规格(不发明 QA 之外的用例;每条 Given/When/Then/Edge 至少一测)
//   GDD:design/gdd/item-database.md §Edge Cases 写入期校验族(:755-830)· §Acceptance 夹具表(:1049-1074)
//   ADR-014 §Decision(负向夹具断言构建期硬失败;本文件经门的错误列表非空断言表达 ——
//     throw 聚合归 Story 008,门本身零 throw)
//
// ⚠️ 落点:故事头登记的账本路径 = tests/unit/item_database/item_validation_fixtures_test.cs;
//    Unity 只编译 unity/Assets/ 树 ⇒ 真身 = 本文件(与 Story 001/002/004/005/006 同一先例)。
//
// ⚠️ 数值纪律:以下常量**全部是测试夹具值,不是游戏平衡值**(GDD §Tuning Knobs「默认」列留空,
//    数值待用户)。边界断言用 FixtureMaxQuality 常量拼接,**不裸写 5**。
//    known_injury_ids / maps_to_injury 是 9 与 25 侧的夹具替身(两系统枚举/动作表未建,注入值)。
// ⚠️ 零 UnityEngine / 零外部 I/O(仅读 7 个负向夹具)/ 无随机种子 / 无时间依赖。
// ⚠️ 本工程未装 Newtonsoft(schema_types_primary_key_test.cs :745)—— 夹具一律手写正则抽取。
// ⚠️ 每条 AC 既有负向断言也有**合法对照通过**断言(负向夹具 + 合法对照 = Control Manifest Required)。

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
    internal sealed class ItemValidationFixturesTest
    {
        // ══════════════ 夹具小件(数值待用户 —— 此处为测试夹具值)══════════════

        private const int FixtureMaxQuality = 5;

        // ══════════════ AC-21a-13:MAX_QUALITY ≥ 2 且整数 ══════════════

        [Test]
        public void test_maxQuality_fixtureBelowTwo_rejected()
        {
            // Given:QA —— MAX_QUALITY = 1(below_two 子记录)
            string body = extractRecordBody(readFixture("invalid_max_quality.json"), "below_two");
            string raw = readRawToken(body, "max_quality");
            Assert.That(raw, Is.EqualTo("1"), "夹具自证:below_two.max_quality = 1");

            // When
            IReadOnlyList<string> errors = ItemValidationGates.ValidateMaxQuality(raw, "below_two");

            // Then:硬失败
            Assert.That(errors.Count, Is.GreaterThanOrEqualTo(1), "MAX = 1 ⇒ 拒");
            Assert.That(errors[0], Does.Contain("AC-21a-13"));
        }

        [Test]
        public void test_maxQuality_fixtureZero_rejected()
        {
            // Given:QA —— MAX_QUALITY = 0(zero 子记录)
            string body = extractRecordBody(readFixture("invalid_max_quality.json"), "zero");
            string raw = readRawToken(body, "max_quality");
            Assert.That(raw, Is.EqualTo("0"), "夹具自证:zero.max_quality = 0");

            // When
            IReadOnlyList<string> errors = ItemValidationGates.ValidateMaxQuality(raw, "zero");

            // Then
            Assert.That(errors.Count, Is.GreaterThanOrEqualTo(1), "MAX = 0 ⇒ 拒");
            Assert.That(errors[0], Does.Contain("AC-21a-13"));
        }

        [Test]
        public void test_maxQuality_fixtureFloatString_rejected()
        {
            // Given:QA —— MAX_QUALITY = "3.5"(float_string 子记录)
            string body = extractRecordBody(readFixture("invalid_max_quality.json"), "float_string");
            string raw = readRawToken(body, "max_quality");
            Assert.That(raw, Is.EqualTo("3.5"), "夹具自证:float_string.max_quality = 3.5");

            // When
            IReadOnlyList<string> errors = ItemValidationGates.ValidateMaxQuality(raw, "float_string");

            // Then:浮点串拒
            Assert.That(errors.Count, Is.GreaterThanOrEqualTo(1), "\"3.5\" ⇒ 拒(非整数)");
            Assert.That(errors[0], Does.Contain("AC-21a-13"));
        }

        [Test]
        public void test_maxQuality_stringFormInteger_accepted()
        {
            // Given:QA Edge —— 字符串 "4" 须按整数解析成功,**非因是字符串被拒**
            // When
            IReadOnlyList<string> errors = ItemValidationGates.ValidateMaxQuality("4", "string_form");

            // Then:合法对照通过
            Assert.That(errors, Is.Empty, "\"4\" 是合法整数写法 ⇒ 过");
        }

        [Test]
        public void test_maxQuality_exactlyTwo_accepted()
        {
            // Given:QA Then —— =2 及以上整数过(具体档数 = 数值待用户,此处只测边界 2)
            // When
            IReadOnlyList<string> errors = ItemValidationGates.ValidateMaxQuality("2", "boundary_two");

            // Then:合法对照通过
            Assert.That(errors, Is.Empty, "MAX = 2 ⇒ 过(品级维度存在)");
        }

        // ══════════════ AC-21a-14:quality_distribution 支撑 ⊆ [1, MAX] ══════════════

        [Test]
        public void test_qualityDist_fixturePointZero_rejected()
        {
            // Given:QA —— 分布含支撑点 0(point_below_one 子记录:[1,0,3])
            string body = extractRecordBody(readFixture("invalid_quality_dist.json"), "point_below_one");
            int[] support = readIntArray(body, "support_points");
            Assert.That(support, Does.Contain(0), "夹具自证:含支撑点 0");

            // When
            IReadOnlyList<string> errors = ItemValidationGates.ValidateQualityDistribution(
                support, FixtureMaxQuality, "point_below_one");

            // Then:硬失败
            Assert.That(errors.Count, Is.GreaterThanOrEqualTo(1), "点 0 ⇒ 拒(<1)");
            Assert.That(errors[0], Does.Contain("AC-21a-14"));
        }

        [Test]
        public void test_qualityDist_pointAboveMax_rejected()
        {
            // Given:QA —— 支撑点 MAX_QUALITY+1(代码构造,避免夹具耦合常量 —— Story 006 惯例)
            int[] support = { FixtureMaxQuality + 1 };

            // When
            IReadOnlyList<string> errors = ItemValidationGates.ValidateQualityDistribution(
                support, FixtureMaxQuality, "above_max");

            // Then:硬失败
            Assert.That(errors.Count, Is.GreaterThanOrEqualTo(1), $"点 {FixtureMaxQuality + 1} ⇒ 拒(> MAX)");
            Assert.That(errors[0], Does.Contain("AC-21a-14"));
        }

        [Test]
        public void test_qualityDist_singleOutlierAmongValid_rejected()
        {
            // Given:QA Edge —— 仅一档越界,其余合法([1,2,0,4,5] 中点 0)
            string body = extractRecordBody(readFixture("invalid_quality_dist.json"), "single_outlier_among_valid");
            int[] support = readIntArray(body, "support_points");

            // When
            IReadOnlyList<string> errors = ItemValidationGates.ValidateQualityDistribution(
                support, FixtureMaxQuality, "single_outlier");

            // Then:单条越界即拒
            Assert.That(errors.Count, Is.EqualTo(1), "仅一档越界 ⇒ 恰一条错");
            Assert.That(errors[0], Does.Contain("AC-21a-14"));
        }

        [Test]
        public void test_qualityDist_zeroWeightSlotStillCounts_rejected()
        {
            // Given:QA Edge —— 权重为 0 的档仍计支撑(zero_weight_slot:[1,0]/[1,0])
            string body = extractRecordBody(readFixture("invalid_quality_dist.json"), "zero_weight_slot");
            int[] support = readIntArray(body, "support_points");
            int[] weights = readIntArray(body, "weights");
            Assert.That(weights[1], Is.EqualTo(0), "夹具自证:第二档权重 = 0");
            Assert.That(support[1], Is.EqualTo(0), "夹具自证:第二档支撑点 = 0(越界)");

            // When:门不豁免权重 0 的档 —— 调用方须把该档索引照传
            IReadOnlyList<string> errors = ItemValidationGates.ValidateQualityDistribution(
                support, FixtureMaxQuality, "zero_weight_slot");

            // Then:越界档仍拒
            Assert.That(errors.Count, Is.GreaterThanOrEqualTo(1), "权重 0 的档越界 ⇒ 仍拒(仍计支撑)");
            Assert.That(errors[0], Does.Contain("AC-21a-14"));
        }

        [Test]
        public void test_qualityDist_boundaryInclusive_accepted()
        {
            // Given:QA Then —— 支撑恰在 [1, MAX] 边界内(两端点都取)
            int[] support = { 1, FixtureMaxQuality };

            // When
            IReadOnlyList<string> errors = ItemValidationGates.ValidateQualityDistribution(
                support, FixtureMaxQuality, "boundary");

            // Then:合法对照通过
            Assert.That(errors, Is.Empty, "[1, MAX] 闭区间内 ⇒ 过");
        }

        [Test]
        public void test_qualityDist_maxQualityLowered_rerunRejects()
        {
            // Given:QA Edge —— 支撑全在界内但 MAX_QUALITY 后下调 ⇒ 重跑校验拒(静默失效回归点,D-21-7)
            int[] support = { 1, 2, 3 };

            // When/Then:原常量下通过
            IReadOnlyList<string> passAtThree = ItemValidationGates.ValidateQualityDistribution(
                support, 3, "rerun_at_3");
            Assert.That(passAtThree, Is.Empty, "max = 3 时 [1,2,3] 合法");

            // When:MAX 下调到 2 后**重跑同一校验**
            IReadOnlyList<string> rejectedAtTwo = ItemValidationGates.ValidateQualityDistribution(
                support, 2, "rerun_at_2");

            // Then:点 3 越出新界 ⇒ 拒(D-21-7 重跑机制归 Story 008 编排,门只判当下)
            Assert.That(rejectedAtTwo.Count, Is.GreaterThanOrEqualTo(1), "MAX 下调后重跑 ⇒ 拒");
            Assert.That(rejectedAtTwo[0], Does.Contain("AC-21a-14"));
        }

        // ══════════════ AC-21a-15:stack_max ≥ 1、weight ≥ 1(int 非 Fix)═════════════

        [Test]
        public void test_stackWeight_fixtureStackMaxZero_rejected()
        {
            // Given:QA —— stack_max = 0
            string body = extractRecordBody(readFixture("invalid_stack_weight.json"), "stack_max_zero");
            string stackMax = readRawToken(body, "stack_max");
            string weight = readRawToken(body, "weight");

            // When
            IReadOnlyList<string> errors = ItemValidationGates.ValidateStackWeight(
                stackMax, weight, "stack_max_zero");

            // Then
            Assert.That(errors.Count, Is.GreaterThanOrEqualTo(1), "stack_max = 0 ⇒ 拒");
            Assert.That(errors[0], Does.Contain("AC-21a-15"));
        }

        [Test]
        public void test_stackWeight_fixtureWeightZero_rejected()
        {
            // Given:QA —— weight = 0
            string body = extractRecordBody(readFixture("invalid_stack_weight.json"), "weight_zero");
            string stackMax = readRawToken(body, "stack_max");
            string weight = readRawToken(body, "weight");

            // When
            IReadOnlyList<string> errors = ItemValidationGates.ValidateStackWeight(
                stackMax, weight, "weight_zero");

            // Then
            Assert.That(errors.Count, Is.GreaterThanOrEqualTo(1), "weight = 0 ⇒ 拒");
            Assert.That(errors[0], Does.Contain("AC-21a-15"));
        }

        [Test]
        public void test_stackWeight_fixtureWeightNegative_rejected()
        {
            // Given:QA —— weight = −n 轮换(-3)
            string body = extractRecordBody(readFixture("invalid_stack_weight.json"), "weight_negative");
            string stackMax = readRawToken(body, "stack_max");
            string weight = readRawToken(body, "weight");
            Assert.That(weight, Is.EqualTo("-3"), "夹具自证:weight = -3");

            // When
            IReadOnlyList<string> errors = ItemValidationGates.ValidateStackWeight(
                stackMax, weight, "weight_negative");

            // Then
            Assert.That(errors.Count, Is.GreaterThanOrEqualTo(1), "weight = -3 ⇒ 拒");
            Assert.That(errors[0], Does.Contain("AC-21a-15"));
        }

        [Test]
        public void test_stackWeight_fixtureWeightFixForm_rejected()
        {
            // Given:Control Manifest Guardrail + QA Edge —— weight Fix 形式 "1/2" 拒(int 非 Fix,D-21-17)
            string body = extractRecordBody(readFixture("invalid_stack_weight.json"), "weight_fix_form");
            string stackMax = readRawToken(body, "stack_max");
            string weight = readRawToken(body, "weight");
            Assert.That(weight, Is.EqualTo("1/2"), "夹具自证:weight 是 Fix 形式字面量");

            // When
            IReadOnlyList<string> errors = ItemValidationGates.ValidateStackWeight(
                stackMax, weight, "weight_fix_form");

            // Then:类型错误拒
            Assert.That(errors.Count, Is.GreaterThanOrEqualTo(1), "weight = \"1/2\" ⇒ 拒(Fix 形式)");
            Assert.That(errors[0], Does.Contain("AC-21a-15"));
        }

        [Test]
        public void test_stackWeight_fixtureWeightFloat_rejected()
        {
            // Given:QA Edge —— weight 浮点拒
            string body = extractRecordBody(readFixture("invalid_stack_weight.json"), "weight_float");
            string stackMax = readRawToken(body, "stack_max");
            string weight = readRawToken(body, "weight");
            Assert.That(weight, Is.EqualTo("1.5"), "夹具自证:weight = 1.5");

            // When
            IReadOnlyList<string> errors = ItemValidationGates.ValidateStackWeight(
                stackMax, weight, "weight_float");

            // Then
            Assert.That(errors.Count, Is.GreaterThanOrEqualTo(1), "weight = 1.5 ⇒ 拒(浮点)");
            Assert.That(errors[0], Does.Contain("AC-21a-15"));
        }

        [Test]
        public void test_stackWeight_stackMaxOne_accepted()
        {
            // Given:QA Then —— stack_max = 1 过(不可堆叠合法)
            // When
            IReadOnlyList<string> errors = ItemValidationGates.ValidateStackWeight(
                "1", "1", "stack_one");

            // Then:合法对照通过
            Assert.That(errors, Is.Empty, "stack_max = 1 且 weight = 1 ⇒ 过");
        }

        [Test]
        public void test_stackWeight_weightOne_accepted()
        {
            // Given:QA Then —— weight = 1 过(最小正单位);stack_max 取合法值 3 互不串扰
            // When
            IReadOnlyList<string> errors = ItemValidationGates.ValidateStackWeight(
                "3", "1", "weight_one");

            // Then:合法对照通过
            Assert.That(errors, Is.Empty, "weight = 1 ⇒ 过(最小正单位)");
        }

        // ══════════════ AC-21a-23:P0 收窄(honey_fried / dry_fried / 非空 tcm_profile)═════════════

        [Test]
        public void test_p0Narrowing_fixtureHoneyFried_rejected()
        {
            // Given:QA —— processing_state = honey_fried(直接调 AC-23 门,不依赖 AC-22 先拒)
            (string state, string tcmBody) = p0Record(readFixture("invalid_p1a_leak.json"), "honey_fried_record");

            // When:本门独立判(两条门不互相替代)
            IReadOnlyList<string> errors = ItemValidationGates.ValidateP0Narrowing(
                state, tcmBody, "honey_fried_record");

            // Then:硬失败
            Assert.That(errors.Count, Is.GreaterThanOrEqualTo(1), "honey_fried ⇒ AC-23 自身拒");
            Assert.That(errors[0], Does.Contain("AC-21a-23"));
        }

        [Test]
        public void test_p0Narrowing_fixtureDryFried_rejected()
        {
            // Given:QA —— processing_state = dry_fried(三形态之一,直接调本门)
            (string state, string tcmBody) = p0Record(readFixture("invalid_p1a_leak.json"), "dry_fried_record");

            // When
            IReadOnlyList<string> errors = ItemValidationGates.ValidateP0Narrowing(
                state, tcmBody, "dry_fried_record");

            // Then
            Assert.That(errors.Count, Is.GreaterThanOrEqualTo(1), "dry_fried ⇒ 拒");
            Assert.That(errors[0], Does.Contain("AC-21a-23"));
        }

        [Test]
        public void test_p0Narrowing_fixtureTcmProfileNonNull_rejected()
        {
            // Given:QA —— tcm_profile 含任一非 null 值(tcm_nonempty_record)
            (string state, string tcmBody) = p0Record(readFixture("invalid_p1a_leak.json"), "tcm_nonempty_record");
            Assert.That(state, Is.EqualTo("raw"), "夹具自证:state 是 P0 合法值(单变量:只罚 tcm)");

            // When
            IReadOnlyList<string> errors = ItemValidationGates.ValidateP0Narrowing(
                state, tcmBody, "tcm_nonempty_record");

            // Then:三形态之一 —— 非空 tcm_profile 拒
            Assert.That(errors.Count, Is.GreaterThanOrEqualTo(1), "tcm_profile 非 null 值 ⇒ 拒");
            Assert.That(errors[0], Does.Contain("AC-21a-23"));
        }

        [Test]
        public void test_p0Narrowing_tcmPlaceholderAllNull_accepted()
        {
            // Given:QA Edge + D-21-6 —— 占位块全字段 null(tcm_placeholder_record)
            (string state, string tcmBody) = p0Record(readFixture("invalid_p1a_leak.json"), "tcm_placeholder_record");

            // When
            IReadOnlyList<string> errors = ItemValidationGates.ValidateP0Narrowing(
                state, tcmBody, "tcm_placeholder_record");

            // Then:合法对照通过(字段在、值全 null)
            Assert.That(errors, Is.Empty, "占位块全 null ⇒ 过(D-21-6)");
        }

        [Test]
        public void test_p0Narrowing_p0EnumStateTcmAbsent_accepted()
        {
            // Given:QA Then —— P0 枚举内 state + tcm_profile 缺省(null)(p0_legal_record)
            (string state, string tcmBody) = p0Record(readFixture("invalid_p1a_leak.json"), "p0_legal_record");
            Assert.That(state, Is.EqualTo("pill"), "夹具自证:P0 枚举内 state");

            // When
            IReadOnlyList<string> errors = ItemValidationGates.ValidateP0Narrowing(
                state, tcmBody, "p0_legal_record");

            // Then:合法对照通过
            Assert.That(errors, Is.Empty, "P0 枚举内 state + tcm_profile 空 ⇒ 过");
        }

        // ══════════════ AC-21a-24:配方 state 对 ∈ 源物品 legal_transitions ══════════════

        [Test]
        public void test_transition_fixtureUndeclaredPair_rejected()
        {
            // Given:QA —— 源条目 legal_transitions 不含某转换,而配方 boundary_state 声明了它
            // 夹具:willow_bark 声明 [raw>dried, dried>extracted],配方声明 raw→extracted(未声明)
            string json = readFixture("invalid_transition.json");
            string[] declarations = readStringArray(json, "source_legal_transitions");
            ProcessingTransition[] boundary = boundaryOf(
                readStringField(json, "boundary_from"), readStringField(json, "boundary_to"));
            Assert.That(declarations, Does.Not.Contain("raw>extracted"), "夹具自证:该对未声明");

            // When
            IReadOnlyList<string> errors = ItemValidationGates.ValidateTransition(
                declarations, boundary, "invalid_transition");

            // Then:硬失败
            Assert.That(errors.Count, Is.GreaterThanOrEqualTo(1), "未声明的对 ⇒ 拒");
            Assert.That(errors[0], Does.Contain("AC-21a-24"));
        }

        [Test]
        public void test_transition_nonAdjacentUndeclared_rejected()
        {
            // Given:QA Edge —— 图上非相邻对(raw→pill 未经中间态)未显式声明 ⇒ 拒
            string[] declarations = { "raw>dried", "dried>extracted" };
            ProcessingTransition[] boundary = boundaryOf("raw", "pill");

            // When
            IReadOnlyList<string> errors = ItemValidationGates.ValidateTransition(
                declarations, boundary, "non_adjacent_undeclared");

            // Then
            Assert.That(errors.Count, Is.GreaterThanOrEqualTo(1), "raw→pill 未声明 ⇒ 拒");
            Assert.That(errors[0], Does.Contain("AC-21a-24"));
        }

        [Test]
        public void test_transition_emptyLegalTransitions_rejected()
        {
            // Given:QA Then —— legal_transitions 空数组 + 任何转换 ⇒ 拒
            string[] declarations = Array.Empty<string>();
            ProcessingTransition[] boundary = boundaryOf("raw", "dried");

            // When
            IReadOnlyList<string> errors = ItemValidationGates.ValidateTransition(
                declarations, boundary, "empty_declarations");

            // Then
            Assert.That(errors.Count, Is.GreaterThanOrEqualTo(1), "空声明 + 任意转换 ⇒ 拒");
            Assert.That(errors[0], Does.Contain("AC-21a-24"));
        }

        [Test]
        public void test_transition_crossBase_checksSourceItemDeclarations_rejected()
        {
            // Given:QA Edge —— 跨 base 配方:通路查**源(输入侧)条目**的声明,输出条目另按其自身规则。
            // 输出侧条目(other_pill_base)声明了 raw>extracted,但源条目(willow_bark)没有 ——
            // 门只接收源条目声明(签名结构性只收一个列表)⇒ 输出侧声明不参与判定。
            string json = readFixture("invalid_transition.json");
            string[] sourceDeclarations = readStringArray(json, "source_legal_transitions");
            string[] outputSideDeclarations = { "raw>extracted" }; // 输出侧声明(不传给门)
            ProcessingTransition[] boundary = boundaryOf("raw", "extracted");
            Assert.That(outputSideDeclarations, Does.Contain("raw>extracted"), "对照:输出侧确实声明了");

            // When:只传源条目声明
            IReadOnlyList<string> errors = ItemValidationGates.ValidateTransition(
                sourceDeclarations, boundary, "cross_base_source");

            // Then:输出侧声明救不了源侧 —— 拒
            Assert.That(errors.Count, Is.GreaterThanOrEqualTo(1), "跨 base 查源条目声明 ⇒ 源侧未声明拒");
            Assert.That(errors[0], Does.Contain("AC-21a-24"));
        }

        [Test]
        public void test_transition_declaredPair_accepted()
        {
            // Given:QA Then —— 声明内的对(过):willow_bark 声明 raw>dried,配方用 raw→dried
            string json = readFixture("invalid_transition.json");
            string[] declarations = readStringArray(json, "source_legal_transitions");
            ProcessingTransition[] boundary = boundaryOf("raw", "dried");

            // When
            IReadOnlyList<string> errors = ItemValidationGates.ValidateTransition(
                declarations, boundary, "declared_pair");

            // Then:合法对照通过
            Assert.That(errors, Is.Empty, "声明内的对 ⇒ 过");
        }

        [Test]
        public void test_transition_declaredButUnused_accepted()
        {
            // Given:QA Edge —— 声明了但配方未用(声明 = 超集许可,单遍只查 boundary→declaration)
            string json = readFixture("invalid_transition.json");
            string[] declarations = readStringArray(json, "source_legal_transitions");
            ProcessingTransition[] boundary = boundaryOf("raw", "dried"); // 未用 dried>extracted

            // When
            IReadOnlyList<string> errors = ItemValidationGates.ValidateTransition(
                declarations, boundary, "declared_unused");

            // Then:合法对照通过
            Assert.That(errors, Is.Empty, "声明但未用 ⇒ 过(声明是超集)");
        }

        [Test]
        public void test_transition_nonAdjacentExplicitlyDeclared_accepted()
        {
            // Given:QA Edge —— 非相邻对(raw→pill)须**显式声明**才过
            string[] declarations = { "raw>dried", "dried>extracted", "raw>pill" };
            ProcessingTransition[] boundary = boundaryOf("raw", "pill");

            // When
            IReadOnlyList<string> errors = ItemValidationGates.ValidateTransition(
                declarations, boundary, "non_adjacent_declared");

            // Then:合法对照通过
            Assert.That(errors, Is.Empty, "非相邻对已显式声明 ⇒ 过");
        }

        // ══════════════ AC-21a-25:类别门 + 外键门 + 集合漂移门 ══════════════

        [Test]
        public void test_injuryBinding_fixtureWeaponMissingField_rejected()
        {
            // Given:QA —— weapon 记录缺 inflicts_injury 字段(weapon_missing 子记录)
            string json = readFixture("invalid_injury_fk.json");
            HashSet<string> known = new HashSet<string>(readStringArray(json, "known_injury_ids"));
            string body = extractRecordBody(json, "weapon_missing");
            ItemCategory category = categoryOf(body);
            string[] inflicts = readInjuryRef(body);
            Assert.That(category, Is.EqualTo(ItemCategory.Weapon), "夹具自证:category = weapon");
            Assert.That(inflicts, Is.Null.Or.Empty, "夹具自证:字段缺省");

            // When
            IReadOnlyList<string> errors = ItemValidationGates.ValidateInjuryBinding(
                category, inflicts, known, "weapon_missing");

            // Then:四形态之一 —— 缺字段拒
            Assert.That(errors.Count, Is.GreaterThanOrEqualTo(1), "weapon 缺字段 ⇒ 拒");
            Assert.That(errors[0], Does.Contain("AC-21a-25"));
        }

        [Test]
        public void test_injuryBinding_materialWithField_rejected()
        {
            // Given:QA —— material 记录带 inflicts_injury 字段(material_with 子记录)
            string json = readFixture("invalid_injury_fk.json");
            HashSet<string> known = new HashSet<string>(readStringArray(json, "known_injury_ids"));
            string body = extractRecordBody(json, "material_with");
            ItemCategory category = categoryOf(body);
            string[] inflicts = readInjuryRef(body);
            Assert.That(category, Is.EqualTo(ItemCategory.Material), "夹具自证:category = material");

            // When
            IReadOnlyList<string> errors = ItemValidationGates.ValidateInjuryBinding(
                category, inflicts, known, "material_with");

            // Then:四形态之二 —— 非 weapon 带字段拒
            Assert.That(errors.Count, Is.GreaterThanOrEqualTo(1), "material 带字段 ⇒ 拒");
            Assert.That(errors[0], Does.Contain("AC-21a-25"));
        }

        [Test]
        public void test_injuryBinding_fixtureDanglingInjuryId_rejected()
        {
            // Given:QA —— weapon 记录 injury_id 在 9 的枚举不存在(ghost_injury)
            string json = readFixture("invalid_injury_fk.json");
            HashSet<string> known = new HashSet<string>(readStringArray(json, "known_injury_ids"));
            string body = extractRecordBody(json, "weapon_dangling");
            ItemCategory category = categoryOf(body);
            string[] inflicts = readInjuryRef(body);
            Assert.That(inflicts, Does.Contain("ghost_injury"), "夹具自证:引用了不存在的 injury_id");

            // When
            IReadOnlyList<string> errors = ItemValidationGates.ValidateInjuryBinding(
                category, inflicts, known, "weapon_dangling");

            // Then:四形态之三 —— 外键悬空拒
            Assert.That(errors.Count, Is.GreaterThanOrEqualTo(1), "悬空 injury_id ⇒ 拒");
            Assert.That(errors[0], Does.Contain("AC-21a-25"));
        }

        [Test]
        public void test_injurySetDrift_fixtureMapsValueOutsideInflicts_rejected()
        {
            // Given:QA —— 25 某动作 maps_to_injury 值不在 21a 集合内(puncture_wound 漂移)
            string json = readFixture("injury_set_drift.json");
            var inflicts = new HashSet<string>(readStringArray(json, "inflicts_injury"));
            var maps = new HashSet<string>(readStringArray(json, "maps_to_injury"));
            Assert.That(maps, Does.Contain("puncture_wound"), "夹具自证:maps 含集合外值");
            Assert.That(inflicts, Does.Not.Contain("puncture_wound"), "夹具自证:inflicts 不含该值");

            // When
            IReadOnlyList<string> errors = ItemValidationGates.ValidateInjurySetSuperset(
                inflicts, maps, "injury_set_drift");

            // Then:四形态之四 —— 集合漂移拒
            Assert.That(errors.Count, Is.GreaterThanOrEqualTo(1), "maps 值不在 inflicts ⇒ 拒(漂移)");
            Assert.That(errors[0], Does.Contain("AC-21a-25"));
        }

        [Test]
        public void test_injuryBinding_weaponValidArray_accepted()
        {
            // Given:QA Then —— 合法 weapon 带有效集(列表形态)
            string json = readFixture("invalid_injury_fk.json");
            HashSet<string> known = new HashSet<string>(readStringArray(json, "known_injury_ids"));
            string body = extractRecordBody(json, "weapon_legal_array");
            ItemCategory category = categoryOf(body);
            string[] inflicts = readInjuryRef(body);

            // When
            IReadOnlyList<string> errors = ItemValidationGates.ValidateInjuryBinding(
                category, inflicts, known, "weapon_legal_array");

            // Then:合法对照通过
            Assert.That(errors, Is.Empty, "weapon + 有效数组 ⇒ 过");
        }

        [Test]
        public void test_injuryBinding_weaponSingleValueForm_accepted()
        {
            // Given:QA Edge —— 单值 vs 数组两种承载都过存在性门(OQ 归数值/数据轮)
            string json = readFixture("invalid_injury_fk.json");
            HashSet<string> known = new HashSet<string>(readStringArray(json, "known_injury_ids"));
            string body = extractRecordBody(json, "weapon_legal_single");
            ItemCategory category = categoryOf(body);
            string[] inflicts = readInjuryRef(body);
            Assert.That(inflicts, Has.Length.EqualTo(1), "夹具自证:单值形态归一为 1 元素");

            // When
            IReadOnlyList<string> errors = ItemValidationGates.ValidateInjuryBinding(
                category, inflicts, known, "weapon_legal_single");

            // Then:合法对照通过
            Assert.That(errors, Is.Empty, "weapon + 单值形态 ⇒ 过(形态不判)");
        }

        [Test]
        public void test_injuryBinding_materialWithoutField_accepted()
        {
            // Given:QA Then —— 非 weapon 无字段(过):material + 字段缺省
            string json = readFixture("invalid_injury_fk.json");
            HashSet<string> known = new HashSet<string>(readStringArray(json, "known_injury_ids"));
            var body = ItemCategory.Material;
            string[] inflicts = null;

            // When
            IReadOnlyList<string> errors = ItemValidationGates.ValidateInjuryBinding(
                body, inflicts, known, "material_without_field");

            // Then:合法对照通过
            Assert.That(errors, Is.Empty, "material 无字段 ⇒ 过");
        }

        [Test]
        public void test_injurySetDrift_setsExactlyEqual_accepted()
        {
            // Given:QA Edge —— 集合恰相等(⊇ 取等,过)
            var inflicts = new HashSet<string> { "cut_wound", "contusion" };
            var maps = new HashSet<string> { "cut_wound", "contusion" };

            // When
            IReadOnlyList<string> errors = ItemValidationGates.ValidateInjurySetSuperset(
                inflicts, maps, "sets_equal");

            // Then:合法对照通过
            Assert.That(errors, Is.Empty, "inflicts = maps ⇒ 过(⊇ 取等)");
            // 注:反向漂移(25 侧删动作后 inflicts 悬空)不在本 AC —— QA Edge 注记归 25,不测。
        }

        // ══════════════ 夹具抽取辅助(正则手写 —— 零 Newtonsoft,承 Story 006)═════════════

        private static string readFixture(string fileName)
        {
            string path = Path.Combine(
                repoRoot(), "tests", "unit", "item_database", "fixtures", fileName);
            Assert.That(File.Exists(path), Is.True, $"负向夹具缺失(Story 007 QA 指定):{path}");
            return File.ReadAllText(path);
        }

        private static string repoRoot([CallerFilePath] string thisFile = "")
        {
            string dir = Path.GetDirectoryName(thisFile) ?? ".";
            return Path.GetFullPath(Path.Combine(dir, "..", "..", "..", "..", ".."));
        }

        /// <summary>取夹具中首个 <c>"key": "value"</c> 的字符串值(去引号)。</summary>
        private static string readStringField(string json, string key)
        {
            Match match = Regex.Match(json, "\"" + key + "\"\\s*:\\s*\"([^\"]*)\"");
            Assert.That(match.Success, Is.True, $"夹具缺字符串字段 {key}");
            return match.Groups[1].Value;
        }

        /// <summary>取夹具中 <c>"key": [ 1, -2 ]</c> 的带符号整数数组。</summary>
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

        /// <summary>
        /// 取标量原始 token:带引号取引号内文,不带引号取裸 token(数字 / 浮点 / Fix 形式 "1/2")。
        /// AC-13 / AC-15 的字段混合 number 与 string 形态 —— 单一读法覆盖两类。
        /// </summary>
        private static string readRawToken(string json, string key)
        {
            Match match = Regex.Match(
                json, "\"" + key + "\"\\s*:\\s*(?:\"([^\"]*)\"|([^,\\s}\\r\\n]+))");
            Assert.That(match.Success, Is.True, $"夹具缺标量字段 {key}");
            return match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
        }

        /// <summary>
        /// 抽取具名子记录体(支持一层嵌套花括号 —— tcm_profile 块)。
        /// 返回外层花括号**之内**的原文。
        /// </summary>
        private static string extractRecordBody(string json, string recordName)
        {
            Match match = Regex.Match(
                json,
                "\"" + Regex.Escape(recordName) + "\"\\s*:\\s*\\{((?:[^{}]|\\{[^{}]*\\})*)\\}");
            Assert.That(match.Success, Is.True, $"夹具缺具名记录 {recordName}");
            return match.Groups[1].Value;
        }

        /// <summary>
        /// 从 invalid_p1a_leak.json 的具名记录抽取 (processing_state, tcm_profile 块体)。
        /// tcm_profile = JSON null / 缺省 ⇒ 块体 null;占位块 ⇒ 返回花括号**之内**的原文。
        /// </summary>
        private static (string state, string tcmBody) p0Record(string json, string recordName)
        {
            string body = extractRecordBody(json, recordName);
            string state = readStringField(body, "processing_state");

            Match nullMatch = Regex.Match(body, "\"tcm_profile\"\\s*:\\s*null");
            if (nullMatch.Success)
            {
                return (state, null);
            }

            Match blockMatch = Regex.Match(body, "\"tcm_profile\"\\s*:\\s*\\{([^{}]*)\\}");
            Assert.That(blockMatch.Success, Is.True, $"夹具自证:{recordName} 的 tcm_profile 缺 null 或块");
            return (state, blockMatch.Groups[1].Value);
        }

        /// <summary>按夹具 category 字面量解析(Story 002 单一实现;枚举闭合归 AC-22 不在本测)。</summary>
        private static ItemCategory categoryOf(string recordBody)
        {
            string literal = readStringField(recordBody, "category");
            Assert.That(ItemDbValidation.TryParseItemCategory(literal, out ItemCategory category), Is.True,
                $"夹具自证:category 闭合 {literal}");
            return category;
        }

        /// <summary>
        /// 读 inflicts_injury:缺省 / null ⇒ null;单值字符串 ⇒ 1 元素;数组 ⇒ 数组。
        /// 单值 vs 数组的形态归 OQ / 数据轮 —— 本测只验两种承载都过存在性门。
        /// </summary>
        private static string[] readInjuryRef(string recordBody)
        {
            Match arrayMatch = Regex.Match(
                recordBody, "\"inflicts_injury\"\\s*:\\s*\\[([^\\]]*)\\]");
            if (arrayMatch.Success)
            {
                var values = new List<string>();
                foreach (Match m in Regex.Matches(arrayMatch.Groups[1].Value, "\"([^\"]*)\""))
                    values.Add(m.Groups[1].Value);
                return values.ToArray();
            }

            Match singleMatch = Regex.Match(
                recordBody, "\"inflicts_injury\"\\s*:\\s*\"([^\"]*)\"");
            if (singleMatch.Success)
            {
                return new[] { singleMatch.Groups[1].Value };
            }

            return null; // 字段缺省 / null
        }

        /// <summary>按 boundary_from / boundary_to 构造状态对(state 经 Story 002 单一实现解析)。</summary>
        private static ProcessingTransition[] boundaryOf(string fromLiteral, string toLiteral)
        {
            Assert.That(ItemDbValidation.TryParseProcessingState(fromLiteral, out ProcessingState from), Is.True,
                $"夹具自证:from 闭合 {fromLiteral}");
            Assert.That(ItemDbValidation.TryParseProcessingState(toLiteral, out ProcessingState to), Is.True,
                $"夹具自证:to 闭合 {toLiteral}");
            return new[] { new ProcessingTransition(from, to) };
        }
    }
}
