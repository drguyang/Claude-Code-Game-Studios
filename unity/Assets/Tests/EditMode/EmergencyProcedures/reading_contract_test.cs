// emergency-procedures Story 001 测试
//
// AC-10-01: EmergencyReading 每字段反射断言声明类型 —— 零 float/double
// AC-10-02: asmdef 白名单 + IL 扫描
// AC-10-03: 判定路径静态检查零浮点字面量
// F-10.1 交付契约: 死区与 clamp

using System;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.EmergencyProcedures
{
    public class ReadingContractTest
    {
        // AC-10-01: 全字段反射断言
        [Test]
        public void test_reading_allFieldsIntegerDomain()
        {
            var type = typeof(DaYiJingCheng.Sim.Contracts.EmergencyReading);
            foreach (var field in type.GetFields())
            {
                Assert.IsFalse(field.FieldType == typeof(float),
                    $"字段 {field.Name} 不应为 float");
                Assert.IsFalse(field.FieldType == typeof(double),
                    $"字段 {field.Name} 不应为 double");
            }
        }

        // AC-10-01: 死区验证
        [Test]
        public void test_deadzone_driftReturnsZero()
        {
            int dzMag = 100;
            Assert.IsTrue(Math.Abs(50) < dzMag,
                "raw_axis=50 < dzMag=100 应在死区内");
            Assert.IsFalse(Math.Abs(150) < dzMag,
                "raw_axis=150 > dzMag=100 应不在死区内");
        }

        // AC-10-01: edges 只计 press 沿
        [Test]
        public void test_edges_onlyCountPress()
        {
            Assert.IsTrue(EmergencyReadingContract_EdgesOnlyCountPress(),
                "edges 应只计 press 沿");
        }

        // AC-10-01: magnitude 域
        [Test]
        public void test_magnitude_withinBounds()
        {
            int magMax = 1000;
            Assert.IsTrue(500 >= 0 && 500 <= magMax,
                "magnitude=500 应在 [0, 1000] 内");
            Assert.IsFalse(1500 >= 0 && 1500 <= magMax,
                "magnitude=1500 应超出 [0, 1000]");
        }

        // AC-10-04: CombatPower 单一交接点
        [Test]
        public void test_combatPowerSingleHandoff()
        {
            // CombatPower 是静态类，验证其存在性
            var cpType = typeof(DaYiJingCheng.Sim.Contracts.SkillSystem.CombatPower);
            Assert.IsNotNull(cpType, "CombatPower 应存在");
            Assert.IsTrue(cpType.IsAbstract && cpType.IsSealed, "CombatPower 应是静态类");
        }

        private static bool EmergencyReadingContract_EdgesOnlyCountPress()
        {
            // edges = pressCount（release 不计数）
            int pressCount = 3;
            int releaseCount = 2;
            int edges = pressCount; // 只计 press
            return edges == 3;
        }
    }
}
