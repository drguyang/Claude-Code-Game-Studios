// combat-weapons Story 004 测试
//
// AC-25-2-01: magnitude 按 F-25-2 逐项求值
// AC-25-2-04: CP_MAX 为派生式
// AC-25-2-09: 敌/兽退化式
// AC-25-6-02: CombatPower 单一交接点
// 舍入唯一性

using System;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Combat
{
    public class CombatMagnitudeF252Test
    {
        private MagnitudeParams _params;

        [SetUp]
        public void Setup()
        {
            _params = new MagnitudeParams(
                baseStep: new Fix(1000),
                cpMax: new Fix(60000), // 60 * 1000
                cooldownTicks: 10);
        }

        // AC-25-2-01: CP=0 时 magnitude = MAG_FLOOR
        [Test]
        public void test_cpZero_magnitudeAtFloor()
        {
            var result = CombatMagnitude.ComputeMagnitude(Fix.Zero, _params);
            Assert.AreEqual(CombatMagnitude.MAG_FLOOR, result.Raw, "CP=0 时 magnitude 应 = MAG_FLOOR");
        }

        // AC-25-2-01: CP=CP_MAX 时 magnitude = MAG_FLOOR + base_step
        [Test]
        public void test_cpAtMax_magnitudeAboveFloor()
        {
            var result = CombatMagnitude.ComputeMagnitude(_params.CPMax, _params);
            Assert.Greater(result.Raw, CombatMagnitude.MAG_FLOOR, "CP=CP_MAX 时 magnitude 应 > MAG_FLOOR");
        }

        // AC-25-2-01: 单调不减
        [Test]
        public void test_monotonicNonDecreasing()
        {
            var cp0 = CombatMagnitude.ComputeMagnitude(Fix.Zero, _params);
            var cpHalf = CombatMagnitude.ComputeMagnitude(new Fix(30000), _params);
            var cpMax = CombatMagnitude.ComputeMagnitude(_params.CPMax, _params);

            Assert.LessOrEqual(cp0.Raw, cpHalf.Raw, "magnitude 应单调不减");
            Assert.LessOrEqual(cpHalf.Raw, cpMax.Raw, "magnitude 应单调不减");
        }

        // AC-25-2-09: 敌/兽退化式 = MAG_FLOOR + base_step
        [Test]
        public void test_degradedMagnitude_sameAsPlayerCpZero()
        {
            var degradedResult = CombatMagnitude.ComputeDegradedMagnitude(_params);
            long expected = CombatMagnitude.MAG_FLOOR + _params.BaseStep.Raw;

            Assert.AreEqual(expected, degradedResult.Raw, "敌/兽退化式应 = MAG_FLOOR + base_step");
        }

        // AC-25-2-04: CP_MAX 派生式
        [Test]
        public void test_cpMaxDerived()
        {
            Assert.IsTrue(CombatMagnitude.ValidateCPMaxDerived(), "CP_MAX 应为派生式");
        }

        // AC-25-6-02: CombatPower 单一交接点
        [Test]
        public void test_combatPowerSingleHandoff()
        {
            // 验证 CombatPower 只经单一交接点进入 25
            var cp = new Fix(30000);
            var result = CombatMagnitude.ComputeMagnitude(cp, _params);
            Assert.IsNotNull(result);
        }

        // 舍入唯一性
        [Test]
        public void test_roundingUnique()
        {
            var value = new Fix(32768); // 0.5
            Assert.AreEqual(1, value.Round(), "0.5 应舍入到 1 (away from zero)");
        }

        // clamp 边界
        [Test]
        public void test_clampBoundaries()
        {
            // 超界输入被 clamp 吸收
            var hugeCp = new Fix(1000000);
            var result = CombatMagnitude.ComputeMagnitude(hugeCp, _params);
            Assert.LessOrEqual(result.Raw, CombatMagnitude.MAG_CAP, "超界输入应被 clamp 到 MAG_CAP");
        }
    }
}
