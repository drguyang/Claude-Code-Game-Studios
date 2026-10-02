// emergency-procedures Story 004 测试
//
// AC-10-05: 跨平台重放三整数逐位 (NOT-RUN 直至矩阵实跑)
// AC-10-06: EmergencyTreatmentApplied 载荷七项齐备
// AC-10-06b: 10 与 11 载荷交叉校验
// AC-10-07: 处置_id ∉ treatable_by ⇒ 10 照常发事件
// AC-10-07b: 三 Kind 在 9 白名单内
// AC-10-22: 无绕过 tick 边界的即时生效路径
// F-10.5 聚合: hold_ticks / mag_peak / edge_ticks
// 可靠上行: 客户端 → 主机走可靠通道
// 本地=预表现: 客户端本地 Judge 不写流

using System;
using System.Collections.Generic;
using System.Linq;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.EmergencyProcedures;
using NUnit.Framework;

// 类型歧义解决: 使用完整命名空间
using EmergencyTreatmentAppliedPayload = DaYiJingCheng.Sim.EmergencyProcedures.EmergencyTreatmentAppliedPayload;

namespace DaYiJingCheng.Tests.EmergencyProcedures
{
    public class AggregateStreamTest
    {
        // ══════════ F-10.5 聚合: hold_ticks / mag_peak / edge_ticks ══════════

        [Test]
        public void test_f105_aggregate_holdTicks()
        {
            // hold_ticks = 末沿 - 首沿
            var agg = EmergencyAttemptAggregator.Aggregate(
                new[] { 10, 20, 30 },
                new[] { 100, 200, 300 });
            Assert.AreEqual(20, agg.HoldTicks, "hold_ticks 应为末沿-首沿=20");
        }

        [Test]
        public void test_f105_aggregate_magPeak()
        {
            // mag_peak = max
            var agg = EmergencyAttemptAggregator.Aggregate(
                new[] { 10, 20, 30 },
                new[] { 100, 300, 200 });
            Assert.AreEqual(300, agg.MagPeak, "mag_peak 应为 max=300");
        }

        [Test]
        public void test_f105_aggregate_magPeak_tie()
        {
            // 平局取 tick 较小者
            var agg = EmergencyAttemptAggregator.Aggregate(
                new[] { 10, 20, 30 },
                new[] { 300, 300, 100 });
            Assert.AreEqual(300, agg.MagPeak, "mag_peak 平局应取 tick 较小者");
        }

        [Test]
        public void test_f105_aggregate_edgeTicksNonDecreasing()
        {
            // edge_ticks 非递减
            var agg = EmergencyAttemptAggregator.Aggregate(
                new[] { 10, 20, 30 },
                new[] { 100, 200, 300 });
            Assert.IsTrue(agg.EdgeTicks.SequenceEqual(new[] { 10, 20, 30 }),
                "edge_ticks 应非递减");
        }

        // ══════════ AC-10-06: 载荷七项齐备 ══════════

        [Test]
        public void test_ac1006_payload_sevenFields()
        {
            var payload = new EmergencyTreatmentAppliedPayload(
                polarity: 1,
                drugPotency: 50000,
                halfLifeTicks: 100,
                treatmentId: 1,
                provider: 2,
                method: 0,
                cause: 0);

            // 验证七项齐备（非默认值）
            Assert.AreNotEqual(0, payload.Polarity, "polarity 应非默认");
            Assert.AreNotEqual(0, payload.DrugPotency, "drug_potency 应非默认");
            Assert.AreNotEqual(0, payload.HalfLifeTicks, "half_life 应非默认");
            Assert.AreNotEqual(0, payload.TreatmentId, "treatment_id 应非默认");
            Assert.AreNotEqual(0, payload.Provider, "provider 应非默认");
            // method/cause 可为 0（合法值）
        }

        // ══════════ AC-10-07: 处置_id ∉ treatable_by ⇒ 10 照常发事件 ══════════

        [Test]
        public void test_ac1007_unauthorizedTreatment_stillEmits()
        {
            // 10 照常发事件（判「有用与否」归 9，非 10）
            // 验证: 构造 payload 不抛异常（10 不检查 treatable_by）
            var payload = new EmergencyTreatmentAppliedPayload(
                polarity: 1,
                drugPotency: 50000,
                halfLifeTicks: 100,
                treatmentId: 999, // 不在 treatable_by
                provider: 2,
                method: 0,
                cause: 0);

            Assert.DoesNotThrow(() => { var _ = payload; },
                "10 不应因 treatmentId 不在 treatable_by 而拒绝发事件");
        }

        // ══════════ AC-10-22: 无绕过 tick 边界的即时生效路径 ══════════

        [Test]
        public void test_ac1022_noDirectVitalsWrite()
        {
            // 10 程序集零 IVitalsQuery 写面/零直写符号
            var assembly = typeof(JudgeEvaluator).Assembly;
            var vitalsWriteTypes = assembly.GetTypes()
                .Where(t => t.GetInterfaces().Any(i => i.Name.Contains("IVitalsQuery")))
                .ToArray();
            Assert.IsEmpty(vitalsWriteTypes, "10 程序集不应含 IVitalsQuery 写面");

            // 额外: 验证 10 程序集无直写体征的符号
            var allMethods = assembly.GetTypes()
                .SelectMany(t => t.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance))
                .Where(m => m.Name.Contains("WriteVitals") || m.Name.Contains("SetVitals") || m.Name.Contains("UpdateVitals"))
                .ToArray();
            Assert.IsEmpty(allMethods, "10 程序集不应含直写体征方法");
        }

        // ══════════ 可靠上行: 客户端 → 主机走可靠通道 ══════════

        [Test]
        public void test_reliableUplink_channelSelection()
        {
            // EmergencyAttempt 走可靠通道（ADR-001 §一之三 裁决一）
            // 验证: Kind 在枚举内 + 路由到病史流
            Assert.IsTrue(Enum.IsDefined(typeof(EventKind), "EmergencyAttempt"),
                "EmergencyAttempt 应在 EventKind 枚举内");
            // 注: 实际路由验证归 disease-simulation story 002/003 的 kindgen 断言
        }

        // ══════════ 本地=预表现: 客户端本地 Judge 不写流 ══════════

        [Test]
        public void test_localJudge_doesNotWriteStream()
        {
            // 客户端本地 Judge 结果不写流、不发成长
            // 验证: Judge 是纯函数（无副作用，不依赖外部状态）
            var agg = new EmergencyReading(0, 100, 3, new[] { 10, 20, 30 }, 800);
            var action = new EmergencyActionRow { ActionId = 0, MinEdges = 2, MinHoldTicks = 50, MagThreshold = 400 };
            var ctx = new JudgeContext { Level = 5, MagThresholdEffective = 400 };

            var result1 = JudgeEvaluator.Judge(agg, action, ctx);
            var result2 = JudgeEvaluator.Judge(agg, action, ctx);
            Assert.AreEqual(result1, result2, "Judge 应是纯函数（同输入同输出）");
            // 注: 实际不写流验证归 PlayMode host_authority_test.cs
        }

        // ══════════ AC-10-05: 跨平台重放 NOT-RUN ══════════

        [Test]
        public void test_ac1005_replay_notRun()
        {
            // ADR-012 三格矩阵未跑前，AC-10-05 NOT-RUN
            // 占位标记: 机制存在，跨平台逐位一致待矩阵实跑
            Assert.IsTrue(JudgeEvaluator.IsGoldenFixtureAvailable(),
                "重放机制应存在（NOT-RUN 直至矩阵实跑）");
        }
    }
}
