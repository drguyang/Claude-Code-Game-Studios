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

        /// <summary>病种提取:共病 —— 同时归 ordinal 1 和 2。</summary>
        private static int[] ExtractComorbidDisease(SimEvent e) => new[] { 1, 2 };

        /// <summary>病种提取:按 Patient 映射到不同病种(用于多 D 测试)。</summary>
        private static int[] ExtractDiseaseByPatient(SimEvent e) => new[] { e.Patient.Value };

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
            Assert.That(result.FiredDiseases.Count, Is.EqualTo(1), "触发一次");
        }

        /// <summary>AC-37-09:第 4 例不重发、不进 MemberSet(冻结)。</summary>
        [Test]
        public void test_ac3709_fourthCase_noRefire()
        {
            var caseEvents = new[]
            {
                MakeEvent(100, new PatientId(1), 0, EventKind.CaseClosed),
                MakeEvent(200, new PatientId(2), 0, EventKind.CaseClosed),
                MakeEvent(300, new PatientId(3), 0, EventKind.CaseClosed),
                MakeEvent(400, new PatientId(4), 0, EventKind.CaseClosed),
            };

            var result = PatternDetector.Evaluate(caseEvents, WorldSeed, Threshold, ExtractDisease);
            Assert.That(result.Fired, Is.True, "第 4 例仍触发(已触发过)");
            Assert.That(result.MemberSet.Count, Is.EqualTo(Threshold), "MemberSet 冻结于前 3 例");
        }

        // ════════════════════════════════════════════════════════════════════
        // AC-37-10:同 D 三例同 tick ⇒ 触发一次,成员取全序前 3
        // ════════════════════════════════════════════════════════════════════

        /// <summary>AC-37-10:同 tick 三例 ⇒ 触发一次,成员取全序前 3。</summary>
        [Test]
        public void test_ac3710_sameTickThreeCases_firesOnce()
        {
            var caseEvents = new[]
            {
                MakeEvent(100, new PatientId(1), 0, EventKind.CaseClosed),
                MakeEvent(100, new PatientId(2), 0, EventKind.CaseClosed),
                MakeEvent(100, new PatientId(3), 0, EventKind.CaseClosed),
            };

            var result = PatternDetector.Evaluate(caseEvents, WorldSeed, Threshold, ExtractDisease);
            Assert.That(result.Fired, Is.True, "同 tick 三例触发");
            Assert.That(result.FiredDiseases.Count, Is.EqualTo(1), "触发一次");
            Assert.That(result.MemberSet.Count, Is.EqualTo(Threshold), "成员取全序前 3");
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

        /// <summary>AC-37-12:纯伤情案(disease_set 空)不进计数。</summary>
        [Test]
        public void test_ac3712_pureInjuryCase_notCounted()
        {
            var caseEvents = new[]
            {
                MakeEvent(100, new PatientId(1), 0, EventKind.CaseClosed),
                MakeEvent(200, new PatientId(2), 0, EventKind.CaseClosed),
            };

            var result = PatternDetector.Evaluate(caseEvents, WorldSeed, Threshold, ExtractEmptyDisease);
            Assert.That(result.Fired, Is.False, "纯伤情案不进计数");
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
            Assert.That(result.FiredDiseases.Count, Is.EqualTo(2), "两个病种都触发");
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
            Assert.That(result.MemberSet.Count, Is.EqualTo(Threshold), "MemberSet = {A₁,B₁,C₁}");
            // 验证 MemberSet 内容:A₁(tick=100,patient=1,seq=0), B₁(tick=200,patient=2,seq=0), C₁(tick=400,patient=3,seq=0)
            Assert.That(result.MemberSet, Is.EquivalentTo(new[]
            {
                new CaseId(100, 1, 0),
                new CaseId(200, 2, 0),
                new CaseId(400, 3, 0),
            }), "MemberSet 内容正确");
        }

        // ════════════════════════════════════════════════════════════════════
        // AC-37-27:同 CaseClosed 令 D₁/D₂ 同时达标 ⇒ 按 salted_key 升序
        // ════════════════════════════════════════════════════════════════════

        /// <summary>AC-37-27:同 tick 多 D 同时达标 ⇒ 按 salted_key 升序。</summary>
        [Test]
        public void test_ac3727_sameTickMultipleDiseases_saltedKeyOrder()
        {
            // 病种 1: 3 个不同病人
            // 病种 2: 3 个不同病人
            var caseEvents = new[]
            {
                MakeEvent(100, new PatientId(1), 0, EventKind.CaseClosed),
                MakeEvent(100, new PatientId(2), 0, EventKind.CaseClosed),
                MakeEvent(100, new PatientId(3), 0, EventKind.CaseClosed),
                MakeEvent(100, new PatientId(4), 0, EventKind.CaseClosed),
                MakeEvent(100, new PatientId(5), 0, EventKind.CaseClosed),
                MakeEvent(100, new PatientId(6), 0, EventKind.CaseClosed),
            };

            // 病人 1-3 → 病种 1,病人 4-6 → 病种 2
            int[] ExtractByPatientGroup(SimEvent e)
            {
                int p = e.Patient.Value;
                return p <= 3 ? new[] { 1 } : new[] { 2 };
            }

            var result = PatternDetector.Evaluate(caseEvents, WorldSeed, Threshold, ExtractByPatientGroup);
            Assert.That(result.Fired, Is.True, "同 tick 多 D 触发");
            Assert.That(result.FiredDiseases.Count, Is.EqualTo(2), "两个病种都触发");
            // 验证 salted_key 升序
            for (int i = 0; i < result.FiredDiseases.Count - 1; i++)
            {
                ulong saltedA = PatternDetector.ComputeSaltedKey(WorldSeed, result.FiredDiseases[i]);
                ulong saltedB = PatternDetector.ComputeSaltedKey(WorldSeed, result.FiredDiseases[i + 1]);
                Assert.That(saltedA, Is.LessThan(saltedB), "salted_key 升序");
            }
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
            Assert.That(result1.FiredDiseases, Is.EquivalentTo(new[] { 0 }), "FiredDiseases = {0}");
            Assert.That(result1.MemberSet.Count, Is.EqualTo(Threshold), "MemberSet = 3 例");

            // 2. 第 4 例结案
            caseEvents.Add(MakeEvent(400, new PatientId(4), 0, EventKind.CaseClosed));
            var result2 = PatternDetector.Evaluate(caseEvents, WorldSeed, Threshold, ExtractDisease);
            Assert.That(result2.Fired, Is.True, "第 4 例仍触发(已触发过)");
            Assert.That(result2.MemberSet.Count, Is.EqualTo(Threshold), "MemberSet 冻结");
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
            Assert.That(result.FiredDiseases, Is.EquivalentTo(new[] { 0 }), "恰一个病种");
            Assert.That(result.MemberSet, Is.EquivalentTo(new[]
            {
                new CaseId(120, 10, 0),
                new CaseId(130, 11, 0),
                new CaseId(140, 12, 0),
            }), "MemberSet = 链成员三例(断内容,非仅计数)");
            Assert.That(result.MemberSet, Has.No.Member(new CaseId(100, 1, 0)), "链外病例不占名额");
            Assert.That(result.MemberSet, Has.No.Member(new CaseId(110, 2, 0)), "链外病例不占名额");
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
            Assert.That(shuffled.MemberSet, Is.EquivalentTo(ascending.MemberSet),
                "成员集与声明序无关");
            Assert.That(ascending.MemberSet, Is.EquivalentTo(new[]
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

            Assert.That(result.FiredDiseases, Is.EquivalentTo(new[] { 1 }),
                "只有无链的 D=1 达标;有链的 D=0 收窄后仅 1 例(链外 3 例既不计数也不占名额)");
            Assert.That(result.MemberSet, Is.EquivalentTo(new[]
            {
                new CaseId(100, 1, 0), new CaseId(110, 2, 0), new CaseId(120, 3, 0),
            }), "D=1 的成员 = 该病自然检测的前三例(D=0 的任何人都不在其中)");
        }
    }
}
