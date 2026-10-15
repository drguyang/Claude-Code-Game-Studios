// case-system Story 004 测试 —— 同源检测与 PatternRecognized
//
// AC-37-07/08/09:长度 < 阈值不触发;恰达标发一次;第 4 例不重发
// AC-37-10:同 D 三例同 tick ⇒ 触发一次,成员取全序前 3
// AC-37-11:三互异病人、两空名一错名 ⇒ 照样触发
// AC-37-12/13:纯伤情案不进计数;共病一案进两组
// AC-37-22/34:A、B、A 三例同 D ⇒ FirstPerPatient 只留 A₁ ⇒ 长度 2 不触发
// AC-37-27:同 CaseClosed 令 D₁/D₂ 同时达标 ⇒ 按 salted_key 升序
// AC-37-14:载荷含 salted_key,不含裸 disease_id
// AC-37-29:脚本三案链 —— 链外同 D 病例不消耗名额 + 一次性(永不重发)
//
// 权威来源:
//   GDD F-37.1(同源检测 · FirstPerPatient · CandidateSeq · PatternFired)
//   GDD F-37.1b(ScriptedChain / ChainEligible 判定域 · 一次性)
//   ADR-008 §五(盐契约:逐 D 派生)
//   ADR-007 §二(WorldSeed 存档头)
//
// ⚠️ 本测试为纯数据层:不触引擎 API,不触 codec,不触持久化。
//    被测对象 = Sim 的 PatternDetector(生产代码)。
//
// ══════════════════════════════════════════════════════════════════════════════
// ⚠️⚠️ 元规则(TA-6,2026-10-15 补评后立)—— **定序类 AC 的夹具必须至少一处「输入序 ≠ 期望序」**
//
//   理由:若夹具的输入序恰等于期望输出序,则「删掉实现里的排序/去重」**不改变输出**
//   ⇒ 断言恒真(假绿)。判别力判据固定为:「把对应的实现删掉/改错 ⇒ 该测试转红」。
//
//   本文件满足该纪律的用例(逐条给出可捕获的变异):
//     · test_ac3727_..._saltedKeyOrderNotInsertionOrder —— 插入序 [2,1] ≠ 期望序 [1,2]
//                                                        变异 = 停用 SortBySaltedKey ⇒ 必红
//     · test_ac3727_saltedOrderIsNotOrdinalOrder       —— 期望序 [5,0,1] ≠ ordinal 序 [0,1,5]
//                                                        变异 = 改成按 ordinal 排序 ⇒ 必红
//                                                        (D=1/2 夹具**抓不到**该变异 —— 二者同序)
//     · test_ac3727_permutedInsertionOrder_identicalSequence —— 两流插入序相反,序列须**逐位相同**
//     · test_ac3713_comorbidCase_countedInBothGroups   —— 提取器集合迭代序 [2,1] ≠ 期望序 [1,2]
//     · test_ac3710_sameTickThreeCases_shuffledStreamOrder_membersInFullOrder
//                                                     —— 流内序 (3,1,2) ≠ 全序 (1,2,3)
//                                                        变异 = 删 ComputeCandidateSeq 的 candidates.Sort ⇒ 必红
//     · test_ac3710_sameTickFourCases_memberFreezeDropsFourth —— 流内序 (4,2,1,3) ≠ 全序前 3
//     · test_ac3709_..._membersFrozenAtFirstThree      —— 「前 3」{1,2,3} ≠ 「后 3」{2,3,4}
//                                                        变异 = 取 candidates[count-threshold..] ⇒ 必红
//     · test_ac3712_mixedInjuryAndReal_injuryCasesDoNotFillQuota —— 伤情案数(3)恰等于阈值
//                                                        变异 = 空 disease_set 也计入某组 ⇒ 必红
//
//   ⚠️ 逐 tick 严格递增的夹具**结构上不可能**满足该纪律(全序键含 `Tick` ⇒ 全序 ≡ 流内序)——
//      AC-37-34 / AC-37-29 属此类:其判别力在**去重(FirstPerPatient)/ 定义域收窄**这一维
//      (见各测注释),**不宣称钉住次序**。若要钉次序,须把夹具换成**同 tick**组。
// ══════════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.CaseSystem
{
    /// <summary>case-system Story 004 —— 同源检测与 PatternRecognized。</summary>
    [TestFixture]
    public sealed class PatternDetectionTest
    {
        private const long WorldSeed = 12345;
        private const int Threshold = 3;

        // ════════════════════════════════════════════════════════════════════
        // 辅助
        // ════════════════════════════════════════════════════════════════════

        private static SimEvent MakeEvent(long tick, PatientId patient, long seq, EventKind kind)
        {
            return new SimEvent(tick, patient, seq, kind, default(PayloadRef));
        }

        /// <summary>病种提取:所有事件归同一病种 ordinal 0。</summary>
        private static int[] ExtractDisease(SimEvent e) => new[] { 0 };

        /// <summary>病种提取:纯伤情案 —— 空病种集。</summary>
        private static int[] ExtractEmptyDisease(SimEvent e) => new int[0];

        /// <summary>
        /// 病种提取:共病 —— 同时归 ordinal 1 和 2。
        /// <para>⚠️ 数组序刻意写成 `[2,1]`(= `disease_set` 的**集合迭代序**的一种排列;
        /// 集合无「序」语义)⇒ 令 AC-37-13 的插入序 `[2,1]` ≠ 期望序 `[1,2]`,使该测**顺带**
        /// 也钉住定序(承文件头元规则 TA-6)。若把此序改回 `[1,2]`,该测对「有无排序」即失去判别力。</para>
        /// </summary>
        private static int[] ExtractComorbidDisease(SimEvent e) => new[] { 2, 1 };

        /// <summary>
        /// 发射决策层快捷入口:调用生产纯函数 <see cref="PatternDetector.NextEmissions"/>
        /// (含«一次性»门 + 逐 D 成员归属),阈值与盐提取器取本文件固定值。
        /// <para>成员集的**唯一**断言载体 —— 达标判定层的扁平 `MemberSet` 已删(补评 CB-3)。</para>
        /// </summary>
        private static IReadOnlyList<PatternEmission> Emit(
            IReadOnlyList<SimEvent> caseEvents,
            Func<SimEvent, int[]> diseaseExtractor,
            IReadOnlyList<ScriptedChain> scriptedChains = null)
        {
            return PatternDetector.NextEmissions(
                caseEvents, WorldSeed, Threshold, diseaseExtractor, ExtractSaltedKeyFromSeq, scriptedChains);
        }

        // ════════════════════════════════════════════════════════════════════
        // AC-37-07/08/09:长度 < 阈值不触发;恰达标发一次;第 4 例不重发
        // ════════════════════════════════════════════════════════════════════

        /// <summary>AC-37-07:长度 < 阈值不触发。</summary>
        [Test]
        public void test_ac3707_belowThreshold_noTrigger()
        {
            var caseEvents = new[]
            {
                MakeEvent(100, new PatientId(1), 0, EventKind.CaseClosed),
                MakeEvent(200, new PatientId(2), 0, EventKind.CaseClosed),
            };

            var result = PatternDetector.Evaluate(caseEvents, WorldSeed, Threshold, ExtractDisease);
            Assert.That(result.Fired, Is.False, "长度 < 阈值不触发");
        }

        /// <summary>AC-37-08:恰达标发一次。</summary>
        [Test]
        public void test_ac3708_atThreshold_firesOnce()
        {
            var caseEvents = new[]
            {
                MakeEvent(100, new PatientId(1), 0, EventKind.CaseClosed),
                MakeEvent(200, new PatientId(2), 0, EventKind.CaseClosed),
                MakeEvent(300, new PatientId(3), 0, EventKind.CaseClosed),
            };

            var result = PatternDetector.Evaluate(caseEvents, WorldSeed, Threshold, ExtractDisease);
            Assert.That(result.Fired, Is.True, "恰达标触发");
            Assert.That(result.FiredDiseases, Is.EqualTo(new[] { 0 }), "触发一次(恰一个病种)");
        }

        /// <summary>
        /// AC-37-09:第 4 例结案 ⇒ **不重发**,且成员集**仍是触发时刻的前 3 例**。
        /// <para>⚠️ 判别力来源(TB-2):夹具 4 例逐 tick 递增 ⇒「前 3 例」{(100,1,0),(200,2,0),(300,3,0)}
        /// **≠**「后 3 例」{(200,2,0),(300,3,0),(400,4,0)} —— 断言取**逐位内容 + 次序**
        /// (不是 `Count`),故变异「取 candidates[count-threshold..]」必红。</para>
        /// <para>⚠️ 两层语义(勿混):`Evaluate` = 达标判定层(第 4 例仍 `Fired=true`,这是前缀属性,
        /// **不是**缺陷 —— 见 PatternDetector.cs 文件头);「不重发」+「成员冻结」的载体是
        /// 发射决策层 <see cref="PatternDetector.NextEmissions"/>(GDD F-37.1b 一次性,AC-37-09)。</para>
        /// </summary>
        [Test]
        public void test_ac3709_fourthCase_noRefire_membersFrozenAtFirstThree()
        {
            var caseEvents = new List<SimEvent>
            {
                MakeEvent(100, new PatientId(1), 0, EventKind.CaseClosed),
                MakeEvent(200, new PatientId(2), 0, EventKind.CaseClosed),
                MakeEvent(300, new PatientId(3), 0, EventKind.CaseClosed),
                MakeEvent(400, new PatientId(4), 0, EventKind.CaseClosed),
            };

            // ① 发射决策层:成员 = **触发时刻的前 3 例**(逐位内容 + 次序)
            var emissions = Emit(caseEvents, ExtractDisease);
            Assert.That(emissions.Count, Is.EqualTo(1), "恰一条发射(单 D 单事件)");
            Assert.That(emissions[0].MemberSet, Is.EqualTo(new[]
            {
                new CaseId(100, 1, 0), new CaseId(200, 2, 0), new CaseId(300, 3, 0),
            }), "成员冻结于**前 3 例** —— 第 4 例 (400,4,0) 不在其中(「后 3 例」变异即红)");

            // ② 承生产契约:发射即 Append
            //    ⚠️ 2026-10-15 订正:原注「写者未落」已陈旧 —— `PatternRecognitionWriter` 已于
            //    sprint-11 落地(S11-13)。本测仍以夹具模拟同一契约,是为**隔离单测**
            //    (只测检测器,不拖装配);**真装配**载体 = `pattern_recognition_writer_test.cs`(9 测)。
            foreach (var emission in emissions)
                caseEvents.Add(MakePatternRecognized(400, emission.DiseaseOrdinal));

            // ③ 一次性门:流中已有该 salted_key ⇒ 第 4 例不重发
            var again = Emit(caseEvents, ExtractDisease);
            Assert.That(again.Count, Is.EqualTo(0), "第 4 例不重发(一次性,不可重现)");

            // ④ 对照:达标判定层不随一次性门变化(钉死两层边界)
            var prefix = PatternDetector.Evaluate(caseEvents, WorldSeed, Threshold, ExtractDisease);
            Assert.That(prefix.Fired, Is.True, "对照:Evaluate 是前缀属性(达标即恒真)");
        }

        // ════════════════════════════════════════════════════════════════════
        // AC-37-10:同 D 三例同 tick ⇒ 触发一次,成员取全序前 3
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// AC-37-10:同 `D` 三例**同一 tick** 结案 ⇒ 触发一次,成员集取**全序前 3**,无平局
        /// (全序键 `(Tick, Patient, Seq)` 含 `Patient`,ADR-008 §二)。
        /// <para>⚠️ 判别力来源(TA-6/TB-2):**流内发出序 (3,1,2) ≠ 全序序 (1,2,3)**
        /// —— 同 tick 下按 `Patient` 升序 ⇒ 删掉 `ComputeCandidateSeq` 的 `candidates.Sort(...)`
        /// 必红;「取后 3 例」类变异因 3 例全取亦在同测红(次序不同)。</para>
        /// </summary>
        [Test]
        public void test_ac3710_sameTickThreeCases_shuffledStreamOrder_membersInFullOrder()
        {
            var caseEvents = new[]
            {
                MakeEvent(100, new PatientId(3), 0, EventKind.CaseClosed), // 先发 = 全序第 3
                MakeEvent(100, new PatientId(1), 0, EventKind.CaseClosed), // 次发 = 全序第 1
                MakeEvent(100, new PatientId(2), 0, EventKind.CaseClosed), // 末发 = 全序第 2
            };

            var result = PatternDetector.Evaluate(caseEvents, WorldSeed, Threshold, ExtractDisease);
            Assert.That(result.Fired, Is.True, "同 tick 三例触发");
            Assert.That(result.FiredDiseases, Is.EqualTo(new[] { 0 }), "触发一次(恰一个病种)");

            var emissions = Emit(caseEvents, ExtractDisease);
            Assert.That(emissions.Count, Is.EqualTo(1), "恰一条发射");
            Assert.That(emissions[0].MemberSet, Is.EqualTo(new[]
            {
                new CaseId(100, 1, 0), new CaseId(100, 2, 0), new CaseId(100, 3, 0),
            }), "成员取全序前 3(按 Patient 升序 —— 不是流内发出序 3,1,2)");
        }

        /// <summary>
        /// AC-37-10 × AC-37-09 交叉:同 tick **4** 例 ⇒ 触发一次,成员 = 全序**前 3**(弃第 4 例)。
        /// <para>⚠️ 判别力来源(TB-2):「全序前 3」{1,2,3} ≠ 「全序后 3」{2,3,4}(4 例下二者不同)
        /// ⇒ 「取 candidates[count-threshold..]」变异必红;流内发出序 (4,2,1,3) ≠ 全序序
        /// ⇒ 「删 candidates.Sort」变异同时被抓。</para>
        /// </summary>
        [Test]
        public void test_ac3710_sameTickFourCases_memberFreezeDropsFourth()
        {
            var caseEvents = new[]
            {
                MakeEvent(100, new PatientId(4), 0, EventKind.CaseClosed),
                MakeEvent(100, new PatientId(2), 0, EventKind.CaseClosed),
                MakeEvent(100, new PatientId(1), 0, EventKind.CaseClosed),
                MakeEvent(100, new PatientId(3), 0, EventKind.CaseClosed),
            };

            var emissions = Emit(caseEvents, ExtractDisease);
            Assert.That(emissions.Count, Is.EqualTo(1), "恰一条发射(同 tick 多例仍单 D 单事件)");
            Assert.That(emissions[0].MemberSet, Is.EqualTo(new[]
            {
                new CaseId(100, 1, 0), new CaseId(100, 2, 0), new CaseId(100, 3, 0),
            }), "全序前 3 —— 第 4 例 (100,4,0) 不进成员集");
        }

        // ════════════════════════════════════════════════════════════════════
        // AC-37-11:三互异病人、两空名一错名 ⇒ 照样触发
        // ════════════════════════════════════════════════════════════════════

        /// <summary>AC-37-11:病名栏空/错不影响触发(公式不看病名)。</summary>
        [Test]
        public void test_ac3711_emptyOrWrongName_stillFires()
        {
            // 病名栏空/错 = disease_set 可能不同,但公式只看 state 与 disease_set
            // 用不同病种 ordinal 模拟病名差异(空名→ordinal 0,错名→ordinal 99)
            var caseEvents = new[]
            {
                MakeEvent(100, new PatientId(1), 0, EventKind.CaseClosed),
                MakeEvent(200, new PatientId(2), 0, EventKind.CaseClosed),
                MakeEvent(300, new PatientId(3), 0, EventKind.CaseClosed),
            };

            // 三个事件分别归不同病种,但都达阈值(各 3 例)
            // 这里用同一病种 ordinal 0 模拟「病名不同但公式不读」
            var result = PatternDetector.Evaluate(caseEvents, WorldSeed, Threshold, ExtractDisease);
            Assert.That(result.Fired, Is.True, "病名空/错照样触发");
        }

        // ════════════════════════════════════════════════════════════════════
        // AC-37-12/13:纯伤情案不进计数;共病一案进两组
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// AC-37-12:纯伤情案(`disease_set` 空)不进**任何**同源计数。
        /// <para>⚠️ 反真空(TB-3):原夹具只有 2 例,`Fired==false` 由「数据不足」蕴含(内层循环零迭代)
        /// ⇒ 无判别力。现用**恰达阈值数(3 例)**的纯伤情案:若伤情案被计入任一病种组,
        /// 计数 = 3 ⇒ 达标 ⇒ 必红。</para>
        /// </summary>
        [Test]
        public void test_ac3712_pureInjuryCases_neverCounted()
        {
            var caseEvents = new[]
            {
                MakeEvent(100, new PatientId(1), 0, EventKind.CaseClosed),
                MakeEvent(200, new PatientId(2), 0, EventKind.CaseClosed),
                MakeEvent(300, new PatientId(3), 0, EventKind.CaseClosed),
            };

            var result = PatternDetector.Evaluate(caseEvents, WorldSeed, Threshold, ExtractEmptyDisease);
            Assert.That(result.Fired, Is.False, "3 例纯伤情案(恰达阈值数)仍不触发");
            Assert.That(Emit(caseEvents, ExtractEmptyDisease), Is.Empty, "零发射");
        }

        /// <summary>
        /// AC-37-12(锐利夹具 · 混合):2 真病例 + 3 纯伤情案 ⇒ 仍不触发(伤情案不占名额、不进计数);
        /// 补第 3 真病例后 ⇒ 触发,且成员集**只含真病例**。
        /// <para>⚠️ 判别力来源(TB-3):伤情案数(3)恰等于阈值 —— 变异「空 disease_set 也计入某组」
        /// 会让第一段断言直接达标 ⇒ 必红;成员集断言同时排除「伤情案占名额」。</para>
        /// </summary>
        [Test]
        public void test_ac3712_mixedInjuryAndReal_injuryCasesDoNotFillQuota()
        {
            // patient ≥ 100 ⇒ 纯伤情(disease_set 空);patient < 100 ⇒ 真病例(D=0)
            int[] ExtractInjuryOrReal(SimEvent e) =>
                e.Patient.Value >= 100 ? new int[0] : new[] { 0 };

            var caseEvents = new List<SimEvent>
            {
                MakeEvent(100, new PatientId(1), 0, EventKind.CaseClosed),    // 真病例 ①
                MakeEvent(110, new PatientId(101), 0, EventKind.CaseClosed),  // 纯伤情
                MakeEvent(120, new PatientId(102), 0, EventKind.CaseClosed),  // 纯伤情
                MakeEvent(130, new PatientId(2), 0, EventKind.CaseClosed),    // 真病例 ②
                MakeEvent(140, new PatientId(103), 0, EventKind.CaseClosed),  // 纯伤情
            };

            var twoReal = PatternDetector.Evaluate(caseEvents, WorldSeed, Threshold, ExtractInjuryOrReal);
            Assert.That(twoReal.Fired, Is.False,
                "真病例仅 2 例(3 例伤情案不补名额)⇒ 不触发");

            // 正向对照:补第 3 真病例 ⇒ 恰触发
            caseEvents.Add(MakeEvent(150, new PatientId(3), 0, EventKind.CaseClosed));

            var threeReal = PatternDetector.Evaluate(caseEvents, WorldSeed, Threshold, ExtractInjuryOrReal);
            Assert.That(threeReal.Fired, Is.True, "第 3 真病例到位 ⇒ 触发");
            Assert.That(threeReal.FiredDiseases, Is.EqualTo(new[] { 0 }), "恰一个病种");

            var emissions = Emit(caseEvents, ExtractInjuryOrReal);
            Assert.That(emissions.Count, Is.EqualTo(1), "恰一条发射");
            Assert.That(emissions[0].MemberSet, Is.EqualTo(new[]
            {
                new CaseId(100, 1, 0), new CaseId(130, 2, 0), new CaseId(150, 3, 0),
            }), "成员集恰为 3 例真病例 —— (110,101,0)/(120,102,0)/(140,103,0) 一个都不在其中");
        }

        /// <summary>AC-37-13:共病一案进两组各计一次。</summary>
        [Test]
        public void test_ac3713_comorbidCase_countedInBothGroups()
        {
            // 共病一案 = disease_set 含两个病种,各计一次
            var caseEvents = new[]
            {
                MakeEvent(100, new PatientId(1), 0, EventKind.CaseClosed),
                MakeEvent(200, new PatientId(2), 0, EventKind.CaseClosed),
                MakeEvent(300, new PatientId(3), 0, EventKind.CaseClosed),
            };

            var result = PatternDetector.Evaluate(caseEvents, WorldSeed, Threshold, ExtractComorbidDisease);
            Assert.That(result.Fired, Is.True, "共病一案进两组");
            Assert.That(result.FiredDiseases, Is.EqualTo(new[] { 1, 2 }),
                "两个病种都触发,且按 salted 升序 [1,2] —— 提取器的集合迭代序是 [2,1](见 ExtractComorbidDisease)");
        }

        // ════════════════════════════════════════════════════════════════════
        // AC-37-22/34:A、B、A 三例同 D ⇒ FirstPerPatient 只留 A₁ ⇒ 长度 2 不触发
        // ════════════════════════════════════════════════════════════════════

        /// <summary>AC-37-22:A、B、A 三例同 D ⇒ FirstPerPatient 只留 A₁ ⇒ 长度 2 不触发。</summary>
        [Test]
        public void test_ac3722_ABAsameD_firstPerPatient_deduplicates()
        {
            var caseEvents = new[]
            {
                MakeEvent(100, new PatientId(1), 0, EventKind.CaseClosed), // A₁
                MakeEvent(200, new PatientId(2), 0, EventKind.CaseClosed), // B₁
                MakeEvent(300, new PatientId(1), 1, EventKind.CaseClosed), // A₂(复诊)
            };

            var result = PatternDetector.Evaluate(caseEvents, WorldSeed, Threshold, ExtractDisease);
            Assert.That(result.Fired, Is.False, "A、B、A 三例 ⇒ FirstPerPatient 只留 A₁ ⇒ 长度 2 不触发");
        }

        /// <summary>AC-37-34:补 C 例后触发且 MemberSet = {A₁,B₁,C₁}。</summary>
        [Test]
        public void test_ac3734_addC_firesWithCorrectMembers()
        {
            var caseEvents = new[]
            {
                MakeEvent(100, new PatientId(1), 0, EventKind.CaseClosed), // A₁
                MakeEvent(200, new PatientId(2), 0, EventKind.CaseClosed), // B₁
                MakeEvent(300, new PatientId(1), 1, EventKind.CaseClosed), // A₂(复诊)
                MakeEvent(400, new PatientId(3), 0, EventKind.CaseClosed), // C₁
            };

            var result = PatternDetector.Evaluate(caseEvents, WorldSeed, Threshold, ExtractDisease);
            Assert.That(result.Fired, Is.True, "补 C 例后触发");

            // ⚠️ 判别力来自 **FirstPerPatient 去重**:无去重 ⇒ 成员 = {A₁,B₁,A₂},与下面逐位期望不同
            // （⚠️ 本夹具逐 tick 递增 ⇒ 全序 ≡ 流内序,「排序有无」在此**不可捕获** ——
            //   次序判别力由同 tick 夹具承担,见文件头元规则。本测只钉去重,不宣称钉次序。）
            var emissions = Emit(caseEvents, ExtractDisease);
            Assert.That(emissions.Count, Is.EqualTo(1), "恰一条发射");
            Assert.That(emissions[0].MemberSet, Is.EqualTo(new[]
            {
                new CaseId(100, 1, 0),   // A₁
                new CaseId(200, 2, 0),   // B₁
                new CaseId(400, 3, 0),   // C₁（A₂ = (300,1,1) 被 FirstPerPatient 丢弃）
            }), "成员集 = {A₁,B₁,C₁}(而非未去重的 {A₁,B₁,A₂})");
        }

        // ════════════════════════════════════════════════════════════════════
        // AC-37-27:同 CaseClosed 令 D₁/D₂ 同时达标 ⇒ 按 salted_key 升序
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// AC-37-27:同一 `CaseClosed` 令 D₁/D₂ 同时达标 ⇒ 两条 `PatternRecognized` 按
        /// **`salted_key` 整数升序**依次写入(Seq = 0,1)—— **禁依赖 `disease_set` 迭代序**。
        /// <para>⚠️ 判别力来源(TB-1):**插入序 ≠ 期望序** —— 先入 `Dictionary` 的是 D=2(病人 1–3),
        /// 后入的是 D=1(病人 4–6);而 W=12345 下
        /// salted(2)=`0xD3F9ECB60053AFF4` &gt; salted(1)=`0x8EB85FEA2CCDBF7C`
        /// ⇒ 插入序 `[2,1]`、期望序 `[1,2]`;停用 `SortBySaltedKey` 即得 `[2,1]` ⇒ **必红**。</para>
        /// <para>(盐值由 `golden_hash_v1_test.cs` 的 `"case-salt"` 黄金向量钉住 —— 期望序不是臆想的。)</para>
        /// </summary>
        [Test]
        public void test_ac3727_sameTickMultipleDiseases_saltedKeyOrderNotInsertionOrder()
        {
            // 夹具前提自检:期望序必须 ≠ 插入序,否则本测对「有无排序」零判别力。
            // ⚠️ 若此断言失败 ⇒ 是**夹具过期信号**(盐函数或病种号动了),须重选病种号,
            //    绝不可把下面的 Is.EqualTo 改回 Is.EquivalentTo(那会退回 TB-1 的假绿形态)。
            Assert.That(PatternDetector.ComputeSaltedKey(WorldSeed, 2),
                Is.GreaterThan(PatternDetector.ComputeSaltedKey(WorldSeed, 1)),
                "夹具前提:salted(2) > salted(1) ⇒ 插入序 [2,1] ≠ 期望序 [1,2]");

            var caseEvents = new[]
            {
                MakeEvent(100, new PatientId(1), 0, EventKind.CaseClosed), // 先入 ⇒ D=2
                MakeEvent(100, new PatientId(2), 0, EventKind.CaseClosed),
                MakeEvent(100, new PatientId(3), 0, EventKind.CaseClosed),
                MakeEvent(100, new PatientId(4), 0, EventKind.CaseClosed), // 后入 ⇒ D=1
                MakeEvent(100, new PatientId(5), 0, EventKind.CaseClosed),
                MakeEvent(100, new PatientId(6), 0, EventKind.CaseClosed),
            };

            // 病人 1-3 → 病种 2,病人 4-6 → 病种 1
            int[] ExtractByPatientGroup(SimEvent e) => e.Patient.Value <= 3 ? new[] { 2 } : new[] { 1 };

            var result = PatternDetector.Evaluate(caseEvents, WorldSeed, Threshold, ExtractByPatientGroup);
            Assert.That(result.Fired, Is.True, "同 tick 多 D 触发");
            Assert.That(result.FiredDiseases, Is.EqualTo(new[] { 1, 2 }),
                "FiredDiseases 恰 [1,2](salted 升序)—— 删/停排序即得插入序 [2,1] ⇒ 红");

            var emissions = Emit(caseEvents, ExtractByPatientGroup);
            Assert.That(emissions.Select(x => x.DiseaseOrdinal), Is.EqualTo(new[] { 1, 2 }),
                "发射序 = salted 升序(写者按此序 Append ⇒ AC-37-27 的 Seq=0,1 由此得)");
        }

        /// <summary>
        /// AC-37-27 后半句:「**换一个插入序重跑,事件流逐位相同**」。
        /// <para>两个流:同一组 6 例同 tick 结案,但**病种首次出现序相反**(流 A 先 D=1;流 B 先 D=2)
        /// ⇒ `FiredDiseases` 与发射序都必须**逐位相同**(断「相同」,不是断「等价」)。</para>
        /// <para>⚠️ 变异:去掉排序 ⇒ 两流各自退化为自己的 `Dictionary` 迭代序 ⇒ 序列不等 ⇒ 本测红。</para>
        /// </summary>
        [Test]
        public void test_ac3727_permutedInsertionOrder_identicalSequence()
        {
            // 流 A:先入 D=1(病人 1–3),后入 D=2(病人 4–6)
            var streamA = new[]
            {
                MakeEvent(100, new PatientId(1), 0, EventKind.CaseClosed),
                MakeEvent(100, new PatientId(2), 0, EventKind.CaseClosed),
                MakeEvent(100, new PatientId(3), 0, EventKind.CaseClosed),
                MakeEvent(100, new PatientId(4), 0, EventKind.CaseClosed),
                MakeEvent(100, new PatientId(5), 0, EventKind.CaseClosed),
                MakeEvent(100, new PatientId(6), 0, EventKind.CaseClosed),
            };
            // 流 B:同一病例集,病种首次出现序相反(D=2 先入)
            var streamB = new[]
            {
                MakeEvent(100, new PatientId(4), 0, EventKind.CaseClosed),
                MakeEvent(100, new PatientId(5), 0, EventKind.CaseClosed),
                MakeEvent(100, new PatientId(6), 0, EventKind.CaseClosed),
                MakeEvent(100, new PatientId(1), 0, EventKind.CaseClosed),
                MakeEvent(100, new PatientId(2), 0, EventKind.CaseClosed),
                MakeEvent(100, new PatientId(3), 0, EventKind.CaseClosed),
            };

            int[] ExtractByGroup(SimEvent e) => e.Patient.Value <= 3 ? new[] { 1 } : new[] { 2 };

            var a = PatternDetector.Evaluate(streamA, WorldSeed, Threshold, ExtractByGroup);
            var b = PatternDetector.Evaluate(streamB, WorldSeed, Threshold, ExtractByGroup);

            Assert.That(a.FiredDiseases, Is.EqualTo(new[] { 1, 2 }), "流 A:插入序 = 期望序");
            Assert.That(b.FiredDiseases, Is.EqualTo(new[] { 1, 2 }),
                "流 B:插入序 [2,1] ≠ 期望序 ⇒ 只有真排序才能得到 [1,2]");
            Assert.That(a.FiredDiseases, Is.EqualTo(b.FiredDiseases),
                "换插入序重跑 ⇒ 事件流逐位相同(不依赖 disease_set 迭代序)");

            Assert.That(Emit(streamA, ExtractByGroup).Select(x => x.DiseaseOrdinal),
                Is.EqualTo(Emit(streamB, ExtractByGroup).Select(x => x.DiseaseOrdinal)),
                "发射序亦逐位相同(salted 升序 = 唯一定序源)");
        }

        /// <summary>
        /// AC-37-27(定序**键**):顺序是 **`salted_key` 整数升序**,**不是**病种 `ordinal` 升序。
        /// <para>⚠️ 判别力来源(补测,2026-10-15):夹具取 D ∈ {0,1,5} —— W=12345 下
        /// salted(5)=`0x405CC4FE863FCE06` &lt; salted(0)=`0x74180702D186D1D6` &lt; salted(1)=`0x8EB85FEA2CCDBF7C`
        /// ⇒ 期望序 `[5,0,1]` 与 `ordinal` 序 `[0,1,5]` **完全不同**
        /// ⇒ 变异「按 ordinal 排序(而非 salted_key)」必红。</para>
        /// <para>⚠️ 该变异在上一个夹具(D=1/2)**抓不到** —— 二者恰好同序(变异实测:无差异)。
        /// 故本测不是重复,是补上「定序键是哪一个」这半边。</para>
        /// </summary>
        [Test]
        public void test_ac3727_saltedOrderIsNotOrdinalOrder()
        {
            Assert.That(PatternDetector.ComputeSaltedKey(WorldSeed, 5),
                Is.LessThan(PatternDetector.ComputeSaltedKey(WorldSeed, 0)), "夹具前提:salted(5) < salted(0)");
            Assert.That(PatternDetector.ComputeSaltedKey(WorldSeed, 0),
                Is.LessThan(PatternDetector.ComputeSaltedKey(WorldSeed, 1)), "夹具前提:salted(0) < salted(1)");

            // 九例同 tick:病人 1-3 → D=0,4-6 → D=1,7-9 → D=5(插入序 = ordinal 序 = [0,1,5])
            var caseEvents = new[]
            {
                MakeEvent(100, new PatientId(1), 0, EventKind.CaseClosed),
                MakeEvent(100, new PatientId(2), 0, EventKind.CaseClosed),
                MakeEvent(100, new PatientId(3), 0, EventKind.CaseClosed),
                MakeEvent(100, new PatientId(4), 0, EventKind.CaseClosed),
                MakeEvent(100, new PatientId(5), 0, EventKind.CaseClosed),
                MakeEvent(100, new PatientId(6), 0, EventKind.CaseClosed),
                MakeEvent(100, new PatientId(7), 0, EventKind.CaseClosed),
                MakeEvent(100, new PatientId(8), 0, EventKind.CaseClosed),
                MakeEvent(100, new PatientId(9), 0, EventKind.CaseClosed),
            };

            int[] ExtractThreeGroups(SimEvent e)
            {
                int p = e.Patient.Value;
                return p <= 3 ? new[] { 0 } : (p <= 6 ? new[] { 1 } : new[] { 5 });
            }

            var result = PatternDetector.Evaluate(caseEvents, WorldSeed, Threshold, ExtractThreeGroups);
            Assert.That(result.FiredDiseases, Is.EqualTo(new[] { 5, 0, 1 }),
                "期望序 = salted 升序 [5,0,1](不是 ordinal 序 [0,1,5],也不是插入序)");

            Assert.That(Emit(caseEvents, ExtractThreeGroups).Select(x => x.DiseaseOrdinal),
                Is.EqualTo(new[] { 5, 0, 1 }), "发射序同源(单一定序源)");
        }

        // ════════════════════════════════════════════════════════════════════
        // AC-37-14:载荷含 salted_key,不含裸 disease_id
        // ════════════════════════════════════════════════════════════════════

        /// <summary>AC-37-14:PatternRecognizedPayload 含 SaltedKey。</summary>
        [Test]
        public void test_ac3714_patternRecognizedPayload_containsSaltedKey()
        {
            var patternType = typeof(PatternRecognizedPayload);
            var field = patternType.GetField("SaltedKey", BindingFlags.Public | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, "PatternRecognizedPayload 须含 SaltedKey");
            Assert.That(field.FieldType, Is.EqualTo(typeof(ulong)), "SaltedKey 类型须为 ulong");
        }

        /// <summary>AC-37-14:PatternRecognizedPayload 不含裸 disease_id。</summary>
        [Test]
        public void test_ac3714_patternRecognizedPayload_noDiseaseId()
        {
            var patternType = typeof(PatternRecognizedPayload);
            Assert.That(patternType.GetField("DiseaseId", BindingFlags.Public | BindingFlags.Instance),
                Is.Null, "PatternRecognizedPayload 不得含裸 DiseaseId");
            Assert.That(patternType.GetField("DiseaseName", BindingFlags.Public | BindingFlags.Instance),
                Is.Null, "PatternRecognizedPayload 不得含 DiseaseName");
        }

        // ════════════════════════════════════════════════════════════════════
        // 盐契约:salted_key 逐 D 派生
        // ════════════════════════════════════════════════════════════════════

        /// <summary>盐契约:salted_key 逐 D 派生(不同 D 不同 key)。</summary>
        [Test]
        public void test_saltedKey_perDisease_differentKeys()
        {
            ulong key1 = PatternDetector.ComputeSaltedKey(WorldSeed, 1);
            ulong key2 = PatternDetector.ComputeSaltedKey(WorldSeed, 2);
            Assert.That(key1, Is.Not.EqualTo(key2), "不同 D 不同 salted_key");
        }

        /// <summary>盐契约:salted_key 确定性(同输入同输出)。</summary>
        [Test]
        public void test_saltedKey_deterministic()
        {
            ulong key1 = PatternDetector.ComputeSaltedKey(WorldSeed, 1);
            ulong key2 = PatternDetector.ComputeSaltedKey(WorldSeed, 1);
            Assert.That(key1, Is.EqualTo(key2), "同输入同输出");
        }

        /// <summary>盐契约:salted_key 依赖 WorldSeed。</summary>
        [Test]
        public void test_saltedKey_dependsOnWorldSeed()
        {
            ulong key1 = PatternDetector.ComputeSaltedKey(WorldSeed, 1);
            ulong key2 = PatternDetector.ComputeSaltedKey(WorldSeed + 1, 1);
            Assert.That(key1, Is.Not.EqualTo(key2), "不同 WorldSeed 不同 salted_key");
        }

        // ════════════════════════════════════════════════════════════════════
        // 综合:完整触发流程
        // ════════════════════════════════════════════════════════════════════

        /// <summary>综合流程:三例结案 → 触发 → 第 4 例不重发。</summary>
        [Test]
        public void test_fullFlow_threeCases_fire_fourthNoRefire()
        {
            // 1. 三例结案
            var caseEvents = new List<SimEvent>
            {
                MakeEvent(100, new PatientId(1), 0, EventKind.CaseClosed),
                MakeEvent(200, new PatientId(2), 0, EventKind.CaseClosed),
                MakeEvent(300, new PatientId(3), 0, EventKind.CaseClosed),
            };

            var result1 = PatternDetector.Evaluate(caseEvents, WorldSeed, Threshold, ExtractDisease);
            Assert.That(result1.Fired, Is.True, "三例触发");
            Assert.That(result1.FiredDiseases, Is.EqualTo(new[] { 0 }), "FiredDiseases = {0}");

            var emit1 = Emit(caseEvents, ExtractDisease);
            Assert.That(emit1.Count, Is.EqualTo(1), "恰一条发射");
            Assert.That(emit1[0].MemberSet, Is.EqualTo(new[]
            {
                new CaseId(100, 1, 0), new CaseId(200, 2, 0), new CaseId(300, 3, 0),
            }), "成员 = 前三例");

            // 2. 第 4 例结案 + 承生产契约「发射即 Append」
            foreach (var emission in emit1)
                caseEvents.Add(MakePatternRecognized(300, emission.DiseaseOrdinal));
            caseEvents.Add(MakeEvent(400, new PatientId(4), 0, EventKind.CaseClosed));

            var result2 = PatternDetector.Evaluate(caseEvents, WorldSeed, Threshold, ExtractDisease);
            Assert.That(result2.Fired, Is.True, "第 4 例仍触发(达标判定层的前缀属性)");

            // 3. 发射决策层:第 4 例不重发(「成员集冻结于前 3 例」的逐位断言见 AC-37-09 用例)
            var emit2 = Emit(caseEvents, ExtractDisease);
            Assert.That(emit2, Is.Empty, "第 4 例不重发(一次性)");
        }

        /// <summary>综合流程 Edge:复诊污染(A、B、A)不触发。</summary>
        [Test]
        public void test_fullFlow_edge_revisitPollution_noTrigger()
        {
            var caseEvents = new[]
            {
                MakeEvent(100, new PatientId(1), 0, EventKind.CaseClosed), // A₁
                MakeEvent(200, new PatientId(2), 0, EventKind.CaseClosed), // B₁
                MakeEvent(300, new PatientId(1), 1, EventKind.CaseClosed), // A₂(复诊)
            };

            var result = PatternDetector.Evaluate(caseEvents, WorldSeed, Threshold, ExtractDisease);
            Assert.That(result.Fired, Is.False, "复诊污染不触发");
        }

        /// <summary>综合流程 Edge:无时间窗口下界(三例任意跨度)。</summary>
        [Test]
        public void test_fullFlow_edge_noTimeWindow_anySpan()
        {
            var caseEvents = new[]
            {
                MakeEvent(100, new PatientId(1), 0, EventKind.CaseClosed),
                MakeEvent(5000, new PatientId(2), 0, EventKind.CaseClosed),
                MakeEvent(9999, new PatientId(3), 0, EventKind.CaseClosed),
            };

            var result = PatternDetector.Evaluate(caseEvents, WorldSeed, Threshold, ExtractDisease);
            Assert.That(result.Fired, Is.True, "无时间窗口下界,任意跨度可触发");
        }

        // ════════════════════════════════════════════════════════════════════
        // IsFired 方法测试
        // ════════════════════════════════════════════════════════════════════

        /// <summary>IsFired:存在对应 PatternRecognized 事件时返回 true。</summary>
        [Test]
        public void test_isFired_existingPatternRecognized_returnsTrue()
        {
            ulong saltedKey = PatternDetector.ComputeSaltedKey(WorldSeed, 1);
            var caseEvents = new[]
            {
                MakeEvent(100, PatientId.None, 0, EventKind.PatternRecognized),
            };

            // 用 saltedKey 作为 Seq 的占位(测试用)
            var eventsWithKey = new[]
            {
                new SimEvent(100, PatientId.None, (long)saltedKey, EventKind.PatternRecognized, default(PayloadRef)),
            };

            bool result = PatternDetector.IsFired(eventsWithKey, 1, WorldSeed, e => (ulong)e.Seq);
            Assert.That(result, Is.True, "存在对应 PatternRecognized 事件");
        }

        /// <summary>IsFired:不存在对应 PatternRecognized 事件时返回 false。</summary>
        [Test]
        public void test_isFired_noPatternRecognized_returnsFalse()
        {
            var caseEvents = new[]
            {
                MakeEvent(100, new PatientId(1), 0, EventKind.CaseClosed),
            };

            bool result = PatternDetector.IsFired(caseEvents, 1, WorldSeed, e => (ulong)e.Seq);
            Assert.That(result, Is.False, "不存在对应 PatternRecognized 事件");
        }

        /// <summary>IsFired:salted_key 不匹配时返回 false。</summary>
        [Test]
        public void test_isFired_wrongSaltedKey_returnsFalse()
        {
            ulong wrongKey = PatternDetector.ComputeSaltedKey(WorldSeed, 999);
            var caseEvents = new[]
            {
                new SimEvent(100, PatientId.None, (long)wrongKey, EventKind.PatternRecognized, default(PayloadRef)),
            };

            bool result = PatternDetector.IsFired(caseEvents, 1, WorldSeed, e => (ulong)e.Seq);
            Assert.That(result, Is.False, "salted_key 不匹配");
        }

        // ════════════════════════════════════════════════════════════════════
        // 非 CaseClosed 事件过滤测试
        // ════════════════════════════════════════════════════════════════════

        /// <summary>非 CaseClosed 事件不参与计数。</summary>
        [Test]
        public void test_nonCaseClosedEvents_ignored()
        {
            var caseEvents = new[]
            {
                MakeEvent(100, new PatientId(1), 0, EventKind.CaseOpened),
                MakeEvent(200, new PatientId(2), 0, EventKind.PatternRecognized),
                MakeEvent(300, new PatientId(3), 0, EventKind.JudgmentRecorded),
            };

            var result = PatternDetector.Evaluate(caseEvents, WorldSeed, Threshold, ExtractDisease);
            Assert.That(result.Fired, Is.False, "非 CaseClosed 事件不参与计数");
        }

        // ════════════════════════════════════════════════════════════════════
        // 空输入测试
        // ════════════════════════════════════════════════════════════════════

        /// <summary>空事件列表不触发。</summary>
        [Test]
        public void test_emptyEvents_noTrigger()
        {
            var caseEvents = new SimEvent[0];
            var result = PatternDetector.Evaluate(caseEvents, WorldSeed, Threshold, ExtractDisease);
            Assert.That(result.Fired, Is.False, "空事件列表不触发");
        }

        // ════════════════════════════════════════════════════════════════════
        // AC-37-29:脚本三案链(GDD F-37.1b)
        //   ChainEligible 判定域 · 链外同 D 病例不消耗名额 · 一次性
        // ════════════════════════════════════════════════════════════════════

        /// <summary>构造脚本链(成员 id 由本层注入 —— 内容值归内容轮 / ADR-014 烘焙)。</summary>
        private static ScriptedChain MakeChain(int diseaseOrdinal, params int[] memberPatientIds)
        {
            return new ScriptedChain(diseaseOrdinal, memberPatientIds);
        }

        /// <summary>PatternRecognized 占位事件:salted_key 塞进 Seq(测试专用解码口径)。</summary>
        private static SimEvent MakePatternRecognized(long tick, int diseaseOrdinal)
        {
            return new SimEvent(
                tick, PatientId.None,
                (long)PatternDetector.ComputeSaltedKey(WorldSeed, diseaseOrdinal),
                EventKind.PatternRecognized, default(PayloadRef));
        }

        /// <summary>配合 <see cref="MakePatternRecognized"/> 的 salted_key 提取。</summary>
        private static ulong ExtractSaltedKeyFromSeq(SimEvent e) => (ulong)e.Seq;

        /// <summary>
        /// 病种提取(逐 D 二选一夹具专用):病人 1/2/3 → D=1(**无链**);其余 → D=0(**有链**)。
        /// ⚠️ D=0 的病人须**同时含链成员与链外病例** —— 否则收窄与否结果相同,夹具无判别力。
        /// </summary>
        private static int[] ExtractChainVsNatural(SimEvent e)
        {
            return new[] { (e.Patient.Value <= 3) ? 1 : 0 };
        }

        /// <summary>ChainEligible:链成员 ⇒ 合格(GDD F-37.1b 逐字定义)。</summary>
        [Test]
        public void test_chainEligible_memberIsEligible()
        {
            var chain = MakeChain(0, 10, 11, 12);
            Assert.That(ChainEligible.IsEligible(chain, 11), Is.True, "11 ∈ ScriptedChain(0)");
        }

        /// <summary>ChainEligible:链外 ⇒ 不合格。</summary>
        [Test]
        public void test_chainEligible_nonMemberIsNotEligible()
        {
            var chain = MakeChain(0, 10, 11, 12);
            Assert.That(ChainEligible.IsEligible(chain, 7), Is.False, "7 ∉ ScriptedChain(0)");
        }

        /// <summary>
        /// AC-37-29(名额不被消耗):链外同 D 病例**再多**也不推进脚本链。
        /// 末段附反向对照 —— 证上面的 Fired=false 出自定义域收窄,而非数据不足。
        /// </summary>
        [Test]
        public void test_ac3729_chainQuota_notConsumedByRandomCases()
        {
            var chains = new[] { MakeChain(0, 10, 11, 12) };

            // 链成员仅 1 例(甲 = 10);另有 4 例同 D 的**链外**病例已结案
            var caseEvents = new[]
            {
                MakeEvent(100, new PatientId(1), 0, EventKind.CaseClosed),  // 链外
                MakeEvent(110, new PatientId(2), 0, EventKind.CaseClosed),  // 链外
                MakeEvent(120, new PatientId(10), 0, EventKind.CaseClosed), // 链成员 甲
                MakeEvent(130, new PatientId(3), 0, EventKind.CaseClosed),  // 链外
                MakeEvent(140, new PatientId(4), 0, EventKind.CaseClosed),  // 链外
            };

            // 无脚本链:5 例同 D ⇒ 达标
            var natural = PatternDetector.Evaluate(caseEvents, WorldSeed, Threshold, ExtractDisease);
            Assert.That(natural.Fired, Is.True,
                "对照:同一流按自然检测(无链)确实达标");

            // 有脚本链:候选收窄到链成员 ⇒ 仅 1 例 ⇒ 不达标
            var chained = PatternDetector.Evaluate(caseEvents, WorldSeed, Threshold, ExtractDisease, chains);
            Assert.That(chained.Fired, Is.False,
                "链外同 D 病例不消耗脚本链名额 —— 链成员仅 1 例,不达标");
        }

        /// <summary>
        /// AC-37-29:指定三例全部结案 ⇒ 恰好触发一次,MemberSet = 指定的那三例
        /// (链外病例先结案亦不占名额)。
        /// </summary>
        [Test]
        public void test_ac3729_chainComplete_firesWithChainMembersOnly()
        {
            var chains = new[] { MakeChain(0, 10, 11, 12) };

            var caseEvents = new[]
            {
                MakeEvent(100, new PatientId(1), 0, EventKind.CaseClosed),  // 链外,先结
                MakeEvent(110, new PatientId(2), 0, EventKind.CaseClosed),  // 链外,先结
                MakeEvent(120, new PatientId(10), 0, EventKind.CaseClosed), // 甲
                MakeEvent(130, new PatientId(11), 0, EventKind.CaseClosed), // 乙
                MakeEvent(140, new PatientId(12), 0, EventKind.CaseClosed), // 丙
            };

            var result = PatternDetector.Evaluate(caseEvents, WorldSeed, Threshold, ExtractDisease, chains);

            Assert.That(result.Fired, Is.True, "指定三例全结 ⇒ 触发");
            Assert.That(result.FiredDiseases, Is.EqualTo(new[] { 0 }), "恰一个病种");

            // ⚠️ 判别力在**定义域收窄**(链外病例不进候选),不在次序(逐 tick 递增 ⇒ 全序 ≡ 流内序)
            var emissions = Emit(caseEvents, ExtractDisease, chains);
            Assert.That(emissions.Count, Is.EqualTo(1), "恰一条发射");
            Assert.That(emissions[0].MemberSet, Is.EqualTo(new[]
            {
                new CaseId(120, 10, 0),
                new CaseId(130, 11, 0),
                new CaseId(140, 12, 0),
            }), "成员 = 链成员三例(断逐位内容,非仅计数)");
            Assert.That(emissions[0].MemberSet, Has.No.Member(new CaseId(100, 1, 0)), "链外病例不占名额");
            Assert.That(emissions[0].MemberSet, Has.No.Member(new CaseId(110, 2, 0)), "链外病例不占名额");
        }

        /// <summary>
        /// AC-37-29(一次性):`PatternRecognized` 已发出 ⇒ 永不重发,不可重现。
        /// 末段对照 <see cref="PatternDetector.Evaluate"/> 仍返 true —— 钉死两层语义的边界。
        /// </summary>
        [Test]
        public void test_ac3729_firesOnce_neverRefires()
        {
            var chains = new[] { MakeChain(0, 10, 11, 12) };

            var caseEvents = new List<SimEvent>
            {
                MakeEvent(120, new PatientId(10), 0, EventKind.CaseClosed),
                MakeEvent(130, new PatientId(11), 0, EventKind.CaseClosed),
                MakeEvent(140, new PatientId(12), 0, EventKind.CaseClosed),
            };

            // 1. 首次:达标且流中无该 salted_key ⇒ 恰一条,逐 D 成员归属
            var first = PatternDetector.NextEmissions(
                caseEvents, WorldSeed, Threshold, ExtractDisease, ExtractSaltedKeyFromSeq, chains);
            Assert.That(first.Count, Is.EqualTo(1), "首次恰发一条");
            Assert.That(first[0].DiseaseOrdinal, Is.EqualTo(0), "D = 0");
            Assert.That(first[0].SaltedKey, Is.EqualTo(PatternDetector.ComputeSaltedKey(WorldSeed, 0)),
                "salted_key 逐 D 派生");
            Assert.That(first[0].MemberSet, Is.EquivalentTo(new[]
            {
                new CaseId(120, 10, 0), new CaseId(130, 11, 0), new CaseId(140, 12, 0),
            }), "成员逐 D 归属(非扁平)");

            // 2. 承生产契约:发射即 Append
            foreach (var emission in first)
                caseEvents.Add(MakePatternRecognized(150, emission.DiseaseOrdinal));

            // 3. 再让更多的同 D 病例结案 ⇒ 永不重发
            caseEvents.Add(MakeEvent(160, new PatientId(20), 0, EventKind.CaseClosed));

            var second = PatternDetector.NextEmissions(
                caseEvents, WorldSeed, Threshold, ExtractDisease, ExtractSaltedKeyFromSeq, chains);
            Assert.That(second.Count, Is.EqualTo(0), "永不重发(一次性,不可重现)");

            // 4. 对照:Evaluate 仍在达标判定层返 true(两层的边界)
            var still = PatternDetector.Evaluate(caseEvents, WorldSeed, Threshold, ExtractDisease, chains);
            Assert.That(still.Fired, Is.True,
                "对照:Evaluate 是达标判定层,「不重发」归 NextEmissions");
        }

        /// <summary>
        /// GDD F-37.1b「同一算式…不需要第二套公式」:链内**声明序**不被消费。
        /// </summary>
        [Test]
        public void test_ac3729_declarationOrder_notConsumed()
        {
            var caseEvents = new[]
            {
                MakeEvent(120, new PatientId(10), 0, EventKind.CaseClosed),
                MakeEvent(130, new PatientId(11), 0, EventKind.CaseClosed),
                MakeEvent(140, new PatientId(12), 0, EventKind.CaseClosed),
            };

            var ascending = PatternDetector.Evaluate(caseEvents, WorldSeed, Threshold, ExtractDisease,
                new[] { MakeChain(0, 10, 11, 12) });
            var shuffled = PatternDetector.Evaluate(caseEvents, WorldSeed, Threshold, ExtractDisease,
                new[] { MakeChain(0, 12, 10, 11) });

            Assert.That(ascending.Fired, Is.True, "声明序 [10,11,12] 触发");
            Assert.That(shuffled.Fired, Is.True, "声明序 [12,10,11] 同样触发");

            // 成员集(含**次序**)必须与链的声明序无关 —— 断「逐位相同」,不是「等价」
            var emitAscending = Emit(caseEvents, ExtractDisease, new[] { MakeChain(0, 10, 11, 12) });
            var emitShuffled = Emit(caseEvents, ExtractDisease, new[] { MakeChain(0, 12, 10, 11) });

            Assert.That(emitShuffled.Count, Is.EqualTo(emitAscending.Count), "发射条数相同");
            Assert.That(emitShuffled[0].MemberSet, Is.EqualTo(emitAscending[0].MemberSet),
                "成员集逐位相同 —— 与声明序无关");
            Assert.That(emitAscending[0].MemberSet, Is.EqualTo(new[]
            {
                new CaseId(120, 10, 0), new CaseId(130, 11, 0), new CaseId(140, 12, 0),
            }), "成员按全序键 (Tick,Patient,Seq) 定序,非声明序");
        }

        /// <summary>
        /// GDD F-37.1b「旧算法的行为保留给**非脚本**的同源检测」:逐 D 二选一,非全局模式。
        /// ⚠️ D=0 同时含链成员(10)与链外病例(20/21/22) —— 无收窄时 D=0 会以 4 例达标,
        /// 有收窄时仅 1 例不达标。这是本夹具判别力的来源(证伪轮实测:缺链外病例即假绿)。
        /// </summary>
        [Test]
        public void test_ac3729_chainAndNaturalCoexist_perDisease()
        {
            var chains = new[] { MakeChain(0, 10, 11, 12) };

            var caseEvents = new[]
            {
                MakeEvent(100, new PatientId(1), 0, EventKind.CaseClosed),  // D=1,无链
                MakeEvent(110, new PatientId(2), 0, EventKind.CaseClosed),  // D=1,无链
                MakeEvent(120, new PatientId(3), 0, EventKind.CaseClosed),  // D=1,无链
                MakeEvent(130, new PatientId(10), 0, EventKind.CaseClosed), // D=0,链成员(仅 1 例)
                MakeEvent(140, new PatientId(20), 0, EventKind.CaseClosed), // D=0,链外
                MakeEvent(150, new PatientId(21), 0, EventKind.CaseClosed), // D=0,链外
                MakeEvent(160, new PatientId(22), 0, EventKind.CaseClosed), // D=0,链外
            };

            var result = PatternDetector.Evaluate(
                caseEvents, WorldSeed, Threshold, ExtractChainVsNatural, chains);

            Assert.That(result.FiredDiseases, Is.EqualTo(new[] { 1 }),
                "只有无链的 D=1 达标;有链的 D=0 收窄后仅 1 例(链外 3 例既不计数也不占名额)");

            var emissions = Emit(caseEvents, ExtractChainVsNatural, chains);
            Assert.That(emissions.Count, Is.EqualTo(1), "恰一条发射(仅 D=1)");
            Assert.That(emissions[0].DiseaseOrdinal, Is.EqualTo(1), "发射的是无链的 D=1");
            Assert.That(emissions[0].MemberSet, Is.EqualTo(new[]
            {
                new CaseId(100, 1, 0), new CaseId(110, 2, 0), new CaseId(120, 3, 0),
            }), "D=1 的成员 = 该病自然检测的前三例(D=0 的任何人都不在其中)");
        }
    }
}
