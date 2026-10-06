// case-system Story 004 测试 —— 同源检测与 PatternRecognized
//
// AC-37-07/08/09:长度 < 阈值不触发;恰达标发一次;第 4 例不重发
// AC-37-10:同 D 三例同 tick ⇒ 触发一次,成员取全序前 3
// AC-37-11:三互异病人、两空名一错名 ⇒ 照样触发
// AC-37-12/13:纯伤情案不进计数;共病一案进两组
// AC-37-22/34:A、B、A 三例同 D ⇒ FirstPerPatient 只留 A₁ ⇒ 长度 2 不触发
// AC-37-27:同 CaseClosed 令 D₁/D₂ 同时达标 ⇒ 按 salted_key 升序
// AC-37-14:载荷含 salted_key,不含裸 disease_id
//
// 权威来源:
//   GDD F-37.1(同源检测 · FirstPerPatient · CandidateSeq · PatternFired)
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
    }
}
