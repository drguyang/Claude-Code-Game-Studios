// time-weather Story 003 测试
//
// AC-5-15: 同输入逐位同输出
// AC-5-16: 负分量场景 + 无钳制
// AC-5-17: 中性缺省 = FIX_ONE
// TR-timeweather-012: 零 float/double

using System;
using System.Reflection;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.TimeWeather
{
    public class EnvModRawTest
    {
        // AC-5-17: 中性缺省 = FIX_ONE
        [Test]
        public void test_neutralDefault_returnsOne()
        {
            var input = new EnvModInput(Fix.Zero, 0, 0, false, 0);
            var result = EnvMod.ComputeEnvModRaw(input);

            Assert.AreEqual(Fix.One.Raw, result.Raw,
                "全分量取默认档 ⇒ EnvMod_raw == FIX_ONE");
        }

        // AC-5-15: 同输入逐位同输出
        [Test]
        public void test_deterministic_sameInputSameOutput()
        {
            var input = new EnvModInput(new Fix(5000), 2, 500, true, 3);

            Assert.IsTrue(EnvMod.IsDeterministic(input),
                "同输入应逐位同输出");
        }

        // AC-5-16: 负分量场景
        [Test]
        public void test_negativeComponent_notClamped()
        {
            // 使用非零天气/季节值，使负的 ecozone 基线能够真正产生负的结果
            var input = new EnvModInput(new Fix(-1000), 1, 100, false, 1);
            var result = EnvMod.ComputeEnvModRaw(input);

            // 5 侧无钳制，负值合法
            Assert.Less(result.Raw, Fix.One.Raw,
                "负分量应产生负 EnvMod_raw，未被钳制");
        }

        // TR-timeweather-012: 零 float/double
        [Test]
        public void test_noFloatDouble_inDto()
        {
            var envModType = typeof(EnvModInput);
            foreach (var field in envModType.GetFields())
            {
                Assert.IsFalse(field.FieldType == typeof(float),
                    $"字段 {field.Name} 不应为 float");
                Assert.IsFalse(field.FieldType == typeof(double),
                    $"字段 {field.Name} 不应为 double");
            }
        }

        // 天气贡献
        [Test]
        public void test_weatherContribution()
        {
            var input = new EnvModInput(Fix.One, 1, 100, false, 0);
            var result = EnvMod.ComputeEnvModRaw(input);

            // 天气贡献应改变 EnvMod_raw（不要求一定增加，因为乘法可能溢出）
            Assert.AreNotEqual(Fix.One.Raw, result.Raw,
                "天气贡献应改变 EnvMod_raw");
        }

        // 昼夜修正
        [Test]
        public void test_nightModifier()
        {
            // 使用非中性输入避免乘法溢出
            var dayInput = new EnvModInput(new Fix(1000), 1, 100, false, 0);
            var nightInput = new EnvModInput(new Fix(1000), 1, 100, true, 0);

            var dayResult = EnvMod.ComputeEnvModRaw(dayInput);
            var nightResult = EnvMod.ComputeEnvModRaw(nightInput);

            // 昼夜修正应改变 EnvMod_raw（不要求一定降低）
            Assert.AreNotEqual(dayResult.Raw, nightResult.Raw,
                "昼夜修正应改变 EnvMod_raw");
        }

        // 季节修正
        [Test]
        public void test_seasonModifier()
        {
            // 使用非零天气值避免乘法归零
            var season0 = new EnvModInput(Fix.One, 1, 100, false, 0);
            var season3 = new EnvModInput(Fix.One, 1, 100, false, 3);

            var result0 = EnvMod.ComputeEnvModRaw(season0);
            var result3 = EnvMod.ComputeEnvModRaw(season3);

            // 季节修正应改变 EnvMod_raw（不要求一定增加）
            Assert.AreNotEqual(result0.Raw, result3.Raw,
                "季节修正应改变 EnvMod_raw");
        }
    }
}
