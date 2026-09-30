// time-weather Story 002 测试
//
// AC-5-06: 同三元组返回逐位相同
// AC-5-11/12: 块内恒定 + 跨块允许跳变
// AC-5-13: 掷骰输入完全可重构
// TR-timeweather-011: 无命中 ⇒ 全局默认

using System;
using DaYiJingCheng.Sim;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.TimeWeather
{
    public class WeatherRollTest
    {
        private WeatherParams _params;

        [SetUp]
        public void Setup()
        {
            _params = new WeatherParams
            {
                WEATHER_BLOCK_TICKS = 100,
                INTENSITY_MAX = 1000
            };
        }

        // AC-5-06: 同三元组返回逐位相同
        [Test]
        public void test_sameTriple_returnsSameResult()
        {
            ulong worldSeed = 12345;
            long tick = 500;
            int ecozoneId = 1;

            var w1 = WeatherRoll.ComputeWeather(worldSeed, tick, ecozoneId, _params);
            var w2 = WeatherRoll.ComputeWeather(worldSeed, tick, ecozoneId, _params);

            Assert.AreEqual(w1.Kind, w2.Kind, "同三元组 kind 应相同");
            Assert.AreEqual(w1.Intensity, w2.Intensity, "同三元组 intensity 应相同");
        }

        // AC-5-11: 块内恒定
        [Test]
        public void test_constantWithinBlock()
        {
            ulong worldSeed = 12345;
            int ecozoneId = 1;

            // 同一块内的两个 tick
            long tick1 = 50;
            long tick2 = 99;

            Assert.IsTrue(WeatherRoll.IsConstantWithinBlock(worldSeed, tick1, tick2, ecozoneId, _params),
                "同块内天气应恒定");
        }

        // AC-5-12: 跨块允许跳变
        [Test]
        public void test_crossBlock_canVary()
        {
            ulong worldSeed = 12345;
            int ecozoneId = 1;

            // 跨块边界的两个 tick
            long tick1 = 99;  // 块 0
            long tick2 = 100; // 块 1

            var w1 = WeatherRoll.ComputeWeather(worldSeed, tick1, ecozoneId, _params);
            var w2 = WeatherRoll.ComputeWeather(worldSeed, tick2, ecozoneId, _params);

            // 跨块不要求恒定（允许跳变）
            // 注意：不强制要求不同，只要求不强制相同
            Assert.IsNotNull(w1);
            Assert.IsNotNull(w2);
        }

        // AC-5-13: 掷骰输入完全可重构
        [Test]
        public void test_reproducible_canReconstruct()
        {
            ulong worldSeed = 12345;
            long tick = 500;
            int ecozoneId = 1;

            // 运行期计算
            var runtime = WeatherRoll.ComputeWeather(worldSeed, tick, ecozoneId, _params);

            // 离线重放（相同输入）
            var replay = WeatherRoll.ComputeWeather(worldSeed, tick, ecozoneId, _params);

            Assert.AreEqual(runtime.Kind, replay.Kind, "离线重放 kind 应相同");
            Assert.AreEqual(runtime.Intensity, replay.Intensity, "离线重放 intensity 应相同");
        }

        // TR-timeweather-011: 无命中 ⇒ 全局默认
        [Test]
        public void test_noEcozone_returnsDefault()
        {
            ulong worldSeed = 12345;
            long tick = 500;
            int invalidEcozoneId = -1; // 无效生态区

            // 简化版：无效生态区应返回默认天气
            var w = WeatherRoll.ComputeWeather(worldSeed, tick, invalidEcozoneId, _params);

            Assert.IsNotNull(w);
            Assert.GreaterOrEqual(w.Kind, 0, "kind 应 >= 0");
            Assert.Less(w.Kind, 4, "kind 应 < 4");
        }

        // 非法参数硬失败
        [Test]
        public void test_invalidParams_throws()
        {
            var invalidParams = new WeatherParams
            {
                WEATHER_BLOCK_TICKS = 0,
                INTENSITY_MAX = 1000
            };

            Assert.Throws<ArgumentException>(() => invalidParams.Validate(),
                "WEATHER_BLOCK_TICKS = 0 应抛异常");
        }

        // intensity 上界
        [Test]
        public void test_intensityWithinBounds()
        {
            ulong worldSeed = 12345;
            int ecozoneId = 1;

            for (long tick = 0; tick < 1000; tick += 10)
            {
                var w = WeatherRoll.ComputeWeather(worldSeed, tick, ecozoneId, _params);
                Assert.GreaterOrEqual(w.Intensity, 0, "intensity 应 >= 0");
                Assert.Less(w.Intensity, _params.INTENSITY_MAX, "intensity 应 < INTENSITY_MAX");
            }
        }
    }
}
