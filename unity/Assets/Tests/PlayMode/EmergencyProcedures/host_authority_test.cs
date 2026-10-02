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
using DaYiJingCheng.Sim.Codec;

// ⚠️ 2026-10-03(Story 007):原别名指向 Sim.EmergencyProcedures 版 ——
// 该重复 struct 已删(权威版在 Sim.Contracts)。现直接用权威版(经 using 可见)。

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
            var pool = new InMemoryBlobPool();
            var processor = new HostEmergencyProcessor(sink, idAuth, new PayloadEncoder(pool));

            // 权威八字段:action / holdTicks / edges / magPeak / magLast / method / actorId / edgeTicks
            var attempt = new EmergencyAttemptPayload(
                action: 0, holdTicks: 20, edges: 3, magPeak: 800, magLast: 800,
                method: 0, actorId: 7, edgeTicks: new[] { 10, 20, 30 });
            // ⚠️ 2026-10-03(story-007):须显式设 ResultMul —— F-10.4 读 action.ResultMul[result];
            // 原夹具未设(为 null)⇒ Process 会 NRE。
            // ⚠️ 该字段是**全局常量**(EmergencyActionSchema.GetResultMulTiers),
            // 却挂在 per-row 上且**schema 无校验** ⇒ 已登记缺口(见文件末 §已知问题)。
            var action = new EmergencyActionRow {
                ActionId = 0, MinEdges = 2, MinHoldTicks = 50, MagThreshold = 400,
                HalfLifeTicks = 120, Polarity = 1, BasePotency = 100000L,
                ResultMul = EmergencyActionSchema.GetResultMulTiers(),
            };
            var ctx = new JudgeContext { Level = 5, MagThresholdEffective = 400 };

            // Act
            processor.Process(attempt, action, ctx, tick: 100, cause: 0);

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
            var pool = new InMemoryBlobPool();
            var processor = new HostEmergencyProcessor(sink, idAuth, new PayloadEncoder(pool));

            // 权威八字段:action / holdTicks / edges / magPeak / magLast / method / actorId / edgeTicks
            var attempt = new EmergencyAttemptPayload(
                action: 0, holdTicks: 20, edges: 3, magPeak: 800, magLast: 800,
                method: 0, actorId: 7, edgeTicks: new[] { 10, 20, 30 });
            // ⚠️ 2026-10-03(story-007):须显式设 ResultMul —— F-10.4 读 action.ResultMul[result];
            // 原夹具未设(为 null)⇒ Process 会 NRE。
            // ⚠️ 该字段是**全局常量**(EmergencyActionSchema.GetResultMulTiers),
            // 却挂在 per-row 上且**schema 无校验** ⇒ 已登记缺口(见文件末 §已知问题)。
            var action = new EmergencyActionRow {
                ActionId = 0, MinEdges = 2, MinHoldTicks = 50, MagThreshold = 400,
                HalfLifeTicks = 120, Polarity = 1, BasePotency = 100000L,
                ResultMul = EmergencyActionSchema.GetResultMulTiers(),
            };
            var ctx = new JudgeContext { Level = 5, MagThresholdEffective = 400 };

            processor.Process(attempt, action, ctx, tick: 100, cause: 0);

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

        // ══════════════════════════════════════════════════════════════
        // §已修(2026-10-03 当日补齐)
        //
        // `EmergencyActionRow.ResultMul` 是 **per-row 字段**,但 F-10.4 的取值
        // (`{16384, 32768, 65536}` = 0.25 / 0.5 / 1.0)是 **GDD 全局常量**,
        // 由 `EmergencyActionSchema.GetResultMulTiers()` 提供 —— 与具体动作无关。
        //
        // 两个后果:
        //  ① **schema 无校验** —— `EmergencyActionSchema` 的 DC-1…DC-4 只覆盖
        //     `HalfLifeTicks` / `MagThreshold` / `JitterRelaxMul` / `ActionId`,
        //     **不含 `ResultMul`** ⇒ 一个未设 `ResultMul` 的 row 能通过全部烘焙门,
        //     直到 `HostEmergencyProcessor.Process` **NRE** 才暴露(本测即此形态)。
        //  ② **冗余** —— 全局常量挂在 per-row 上,每个动作都要重复填同一份。
        //
        // 修法(择一,须裁):甲 = 加 DC-5 校验 `ResultMul != null && Length == 3`;
        // 乙 = 把 `ResultMul` 从 row 移除,`Process` 直读 `GetResultMulTiers()`。
        // ✅ **已裁并落盘(2026-10-03)**:GDD 10-DC 补 **DC-6**(`result_mul` 恰三档),
//    `EmergencyActionSchema.ValidateResultMul` 已实现 + 3 例测试。
//    ⚠️ **「从 row 移除」一路已排除** —— GDD 10-DC 明写 `result_mul` 是
//    **per-action 数据表字段**(内联于 `emergency_action.json`,ADR-014 烘焙),
//    不是全局常量;故保留在 row 上是对的,缺的只是**校验**。
        // ══════════════════════════════════════════════════════════════

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
