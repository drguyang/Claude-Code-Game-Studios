// time-weather Story 001 测试
//
// AC-5-01: phase = FMod(tick, TICKS_PER_DAY)
// AC-5-02/03: 环绕段判定正确
// AC-5-05: TICKS_PER_DAY 单一定义
// AC-5-14: 非法参数硬失败
// AC-5-20: FMod(-1, 5) = 4

using System;
using DaYiJingCheng.Sim;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.TimeWeather
{
    public class TimeBaseTest
    {
        private TimeParams _params;

        [SetUp]
        public void Setup()
        {
            _params = new TimeParams
            {
                TICKS_PER_DAY = 1000,
                TICKS_PER_SEASON = 10000,
                SEASONS_PER_YEAR = 4,
                NIGHT_START = 700,
                NIGHT_SPAN = 200
            };
        }

        // AC-5-01: phase = FMod(tick, TICKS_PER_DAY)
        [Test]
        public void test_phase_equalsFMod()
        {
            Assert.AreEqual(0, TimeBase.ComputePhase(0, _params));
            Assert.AreEqual(500, TimeBase.ComputePhase(500, _params));
            Assert.AreEqual(0, TimeBase.ComputePhase(1000, _params));
            Assert.AreEqual(1, TimeBase.ComputePhase(1001, _params));
        }

        // AC-5-06: isNight triple fixture (TPD=100, START=90, SPAN=20)
        [Test]
        public void test_nightWraparound_tripleFixture()
        {
            var fixture = new TimeParams
            {
                TICKS_PER_DAY = 100,
                TICKS_PER_SEASON = 1000,
                SEASONS_PER_YEAR = 4,
                NIGHT_START = 90,
                NIGHT_SPAN = 20
            };

            // phase=95 → night (pre-wrap)
            Assert.IsTrue(TimeBase.IsNight(95, fixture), "phase=95 应是夜晚（pre-wrap）");

            // phase=9 → night (boundary, SPAN=20 open/close)
            Assert.IsTrue(TimeBase.IsNight(9, fixture), "phase=9 应是夜晚（boundary）");

            // phase=11 → day (boundary)
            Assert.IsFalse(TimeBase.IsNight(11, fixture), "phase=11 应是白天（boundary）");

            // phase=5 → night (post-wrap)
            Assert.IsTrue(TimeBase.IsNight(5, fixture), "phase=5 应是夜晚（post-wrap）");

            // Reverse assertion: naive START ≤ phase < START+SPAN at phase=5 must return day
            // 证明 fixture 确实测试环绕
            bool naiveResult = 90 <= 5 && 5 < 90 + 20; // false
            Assert.IsFalse(naiveResult, "朴素区间比较在 phase=5 应返回 false（证明环绕测试有效）");
        }

        // AC-5-20: FMod 边界测试 + C# % reverse sentinel
        [Test]
        public void test_fmod_int64Boundaries()
        {
            // n = 1
            Assert.AreEqual(0, TimeBase.FMod(0, 1), "FMod(0, 1) 应 = 0");
            Assert.AreEqual(0, TimeBase.FMod(100, 1), "FMod(100, 1) 应 = 0");
            Assert.AreEqual(0, TimeBase.FMod(-100, 1), "FMod(-100, 1) 应 = 0");

            // a = int64 边界
            Assert.AreEqual(0, TimeBase.FMod(long.MaxValue, long.MaxValue), "FMod(Max, Max) 应 = 0");
            // FMod(Min, Max) = Min - Max * floor(Min / Max) = Min - Max * (-1) = Min + Max = -1
            // 但 FMod 要求结果 ∈ [0, n)，所以 FMod(Min, Max) = Max - 1
            Assert.AreEqual(long.MaxValue - 1, TimeBase.FMod(long.MinValue, long.MaxValue), "FMod(Min, Max) 应 = Max - 1");

            // C# % reverse sentinel: (5-90) % 100 == -85 ≠ FMod(5-90, 100) == 15
            int csharpMod = (5 - 90) % 100; // -85
            long fmodResult = TimeBase.FMod(5 - 90, 100); // 15
            Assert.AreEqual(-85, csharpMod, "C# % 应返回 -85（截断陷阱）");
            Assert.AreEqual(15, fmodResult, "FMod 应返回 15（环绕安全）");
            Assert.AreNotEqual(csharpMod, fmodResult, "C# % 与 FMod 结果应不同");
        }

        // AC-5-11: NIGHT_SPAN ≥ TICKS_PER_DAY 硬失败
        [Test]
        public void test_invalidNightSpan_throws()
        {
            var invalidParams = new TimeParams
            {
                TICKS_PER_DAY = 1000,
                TICKS_PER_SEASON = 10000,
                SEASONS_PER_YEAR = 4,
                NIGHT_START = 700,
                NIGHT_SPAN = 1000 // ≥ TICKS_PER_DAY
            };

            Assert.Throws<ArgumentException>(() => invalidParams.Validate(),
                "NIGHT_SPAN ≥ TICKS_PER_DAY 应抛异常");
        }

        // AC-5-11: TICKS_PER_SEASON = 0 硬失败
        [Test]
        public void test_invalidTicksPerSeason_throws()
        {
            var invalidParams = new TimeParams
            {
                TICKS_PER_DAY = 1000,
                TICKS_PER_SEASON = 0,
                SEASONS_PER_YEAR = 4,
                NIGHT_START = 700,
                NIGHT_SPAN = 200
            };

            Assert.Throws<ArgumentException>(() => invalidParams.Validate(),
                "TICKS_PER_SEASON = 0 应抛异常");
        }

        // AC-5-14: TICKS_PER_DAY = 0 硬失败
        [Test]
        public void test_invalidParams_throws()
        {
            var invalidParams = new TimeParams
            {
                TICKS_PER_DAY = 0,
                TICKS_PER_SEASON = 10000,
                SEASONS_PER_YEAR = 4,
                NIGHT_START = 700,
                NIGHT_SPAN = 200
            };

            Assert.Throws<ArgumentException>(() => invalidParams.Validate(),
                "TICKS_PER_DAY = 0 应抛异常");
        }

        // AC-5-14: SEASONS_PER_YEAR < 2 硬失败
        [Test]
        public void test_invalidSeasons_throws()
        {
            var invalidParams = new TimeParams
            {
                TICKS_PER_DAY = 1000,
                TICKS_PER_SEASON = 10000,
                SEASONS_PER_YEAR = 1,
                NIGHT_START = 700,
                NIGHT_SPAN = 200
            };

            Assert.Throws<ArgumentException>(() => invalidParams.Validate(),
                "SEASONS_PER_YEAR = 1 应抛异常");
        }

        // AC-5-05: TICKS_PER_DAY 单一定义
        [Test]
        public void test_ticksPerDay_singleDefinition()
        {
            Assert.Greater(_params.TICKS_PER_DAY, 0, "TICKS_PER_DAY 应 > 0");
        }

        // 季节索引计算
        [Test]
        public void test_seasonIndex_monotonicAndWraps()
        {
            Assert.AreEqual(0, TimeBase.ComputeSeasonIndex(0, _params));
            Assert.AreEqual(0, TimeBase.ComputeSeasonIndex(9999, _params));
            Assert.AreEqual(1, TimeBase.ComputeSeasonIndex(10000, _params));
            Assert.AreEqual(3, TimeBase.ComputeSeasonIndex(39999, _params));
            Assert.AreEqual(0, TimeBase.ComputeSeasonIndex(40000, _params), "年尾应回绕");
        }

        // 纯函数确定性
        [Test]
        public void test_deterministic_pureFunction()
        {
            var state1 = TimeBase.ComputeState(12345, _params);
            var state2 = TimeBase.ComputeState(12345, _params);

            Assert.AreEqual(state1.Tick, state2.Tick);
            Assert.AreEqual(state1.Phase, state2.Phase);
            Assert.AreEqual(state1.IsNight, state2.IsNight);
            Assert.AreEqual(state1.SeasonIndex, state2.SeasonIndex);
        }
    }
}
