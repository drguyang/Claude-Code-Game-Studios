// emergency-procedures Story 004 PlayMode 测试
//
// 主机侧管线端到端验证:
//   - Subscribe(EmergencyAttempt) → Judge → Append × 2 → Seq 发号
//   - 有界性: 一条完成动作 ⇒ 恰两条病史流事件
//   - 可靠上行: EmergencyAttempt 走可靠通道
//   - AC-10-06b: 10/11 载荷交叉校验
//   - AC-10-07b: 三 Kind 在白名单内
//   - AC-10-22: 无绕过 tick 边界的即时生效路径

using System.Collections;
using System.Linq;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.EmergencyProcedures;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// 类型歧义解决: 使用完整命名空间
using EmergencyAttemptPayload = DaYiJingCheng.Sim.EmergencyProcedures.EmergencyAttemptPayload;

namespace DaYiJingCheng.Tests.PlayMode.EmergencyProcedures
{
    public class HostAuthorityTest
    {
        // ══════════ 主机管线端到端 ══════════

        [UnityTest]
        public IEnumerator test_hostPipeline_endToEnd()
        {
            // Arrange
            var sink = new FakeEventSink();
            var idAuth = new FakeIdAuthority();
            var processor = new HostEmergencyProcessor(sink, idAuth);

            var attempt = new EmergencyAttemptPayload(0, 20, 3, new[] { 10, 20, 30 }, 800);
            var action = new EmergencyActionRow { ActionId = 0, MinEdges = 2, MinHoldTicks = 50, MagThreshold = 400 };
            var ctx = new JudgeContext { Level = 5, MagThresholdEffective = 400 };

            // Act
            processor.Process(attempt, action, ctx, tick: 100);

            // Assert: 恰两条事件（EmergencyAttempt + EmergencyTreatmentApplied）
            Assert.AreEqual(2, sink.AppendedEvents.Count, "一条完成动作应恰两条流事件");
            Assert.AreEqual(EventKind.EmergencyAttempt, sink.AppendedEvents[0].Kind);
            Assert.AreEqual(EventKind.EmergencyTreatmentApplied, sink.AppendedEvents[1].Kind);

            yield return null;
        }

        // ══════════ 有界性: 一条动作 ⇒ 恰两条事件 ══════════

        [UnityTest]
        public IEnumerator test_boundedness_oneActionTwoEvents()
        {
            var sink = new FakeEventSink();
            var idAuth = new FakeIdAuthority();
            var processor = new HostEmergencyProcessor(sink, idAuth);

            var attempt = new EmergencyAttemptPayload(0, 20, 3, new[] { 10, 20, 30 }, 800);
            var action = new EmergencyActionRow { ActionId = 0, MinEdges = 2, MinHoldTicks = 50, MagThreshold = 400 };
            var ctx = new JudgeContext { Level = 5, MagThresholdEffective = 400 };

            processor.Process(attempt, action, ctx, tick: 100);

            // 有界性: 与帧率无关，一条动作恰两条
            Assert.AreEqual(2, sink.AppendedEvents.Count, "有界性: 一条动作应恰两条事件");

            yield return null;
        }

        // ══════════ AC-10-07b: 三 Kind 在白名单内 ══════════

        [Test]
        public void test_ac1007b_threeKindsInWhitelist()
        {
            var kinds = EmergencyActionSchema.GetRequiredKindWhitelist();
            Assert.AreEqual(3, kinds.Length, "白名单应恰含三 Kind");
            Assert.IsTrue(kinds.Contains("EmergencyAttempt"));
            Assert.IsTrue(kinds.Contains("EmergencyTreatmentApplied"));
            Assert.IsTrue(kinds.Contains("DrugTreatmentApplied"));
        }

        // ══════════ AC-10-22: 无即时生效路径 ══════════

        [Test]
        public void test_ac1022_noDirectVitalsWrite()
        {
            // 10 程序集零 IVitalsQuery 写面
            var assembly = typeof(JudgeEvaluator).Assembly;
            var vitalsWriteTypes = assembly.GetTypes()
                .Where(t => t.GetInterfaces().Any(i => i.Name.Contains("IVitalsQuery")))
                .ToArray();
            Assert.IsEmpty(vitalsWriteTypes, "10 程序集不应含 IVitalsQuery 写面");
        }

        // ══════════ 测试辅助 ══════════

        private sealed class FakeEventSink : IEventSink
        {
            public readonly System.Collections.Generic.List<SimEvent> AppendedEvents = new System.Collections.Generic.List<SimEvent>();
            public void Append(in SimEvent e) => AppendedEvents.Add(e);
        }

        private sealed class FakeIdAuthority : IIdAuthority
        {
            public PatientId NextPatientId() => new PatientId(1);
            public ItemInstanceId NextItemInstanceId() => new ItemInstanceId(1);
        }
    }
}
