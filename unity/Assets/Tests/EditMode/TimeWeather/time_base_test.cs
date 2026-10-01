// Story 003: 时间基准 —— tick 只读与昼夜/季节相位推导
//
// AC-5-01: phase = FMod(tick, TICKS_PER_DAY)
// AC-5-02: isNight = FMod(phase - NIGHT_START, TICKS_PER_DAY) < NIGHT_SPAN
// AC-5-03: season_index = FMod(FDiv(tick, TICKS_PER_SEASON), SEASONS_PER_YEAR)
// AC-5-04: 同输入同输出（纯函数确定性）

using System;
using NUnit.Framework;
using DaYiJingCheng.Sim.World;

namespace DaYiJingCheng.Tests.TimeWeather
{
    public class TimeBaseTest
    {
        [Test]
        public void test_phase_equalsFMod()
        {
            long tick = 12345;
            long expected = TimeBase.FMod(tick, TimeBase.TICKS_PER_DAY);
            Assert.AreEqual(expected, TimeBase.ComputePhase(tick));
        }

        [Test]
        public void test_isNight_correctRange()
        {
            // NIGHT_START = 1800, NIGHT_SPAN = 1200
            // tick = 1800 → phase = 1800 → isNight = true
            Assert.IsTrue(TimeBase.IsNight(1800));
            // tick = 600 → phase = 600 → isNight = false
            Assert.IsFalse(TimeBase.IsNight(600));
            // tick = 3000 → phase = 3000 → isNight = true (3000 >= 1800 && 3000 < 3000)
            Assert.IsTrue(TimeBase.IsNight(3000));
        }

        [Test]
        public void test_seasonIndex_correctRange()
        {
            // TICKS_PER_SEASON = 10000, SEASONS_PER_YEAR = 4
            // tick = 0 → season = 0
            Assert.AreEqual(0, TimeBase.ComputeSeasonIndex(0));
            // tick = 10000 → season = 1
            Assert.AreEqual(1, TimeBase.ComputeSeasonIndex(10000));
            // tick = 40000 → season = 0 (wraps)
            Assert.AreEqual(0, TimeBase.ComputeSeasonIndex(40000));
        }

        [Test]
        public void test_deterministic_sameInputSameOutput()
        {
            long tick = 99999;
            var state1 = TimeBase.ComputeState(tick);
            var state2 = TimeBase.ComputeState(tick);
            Assert.AreEqual(state1.Phase, state2.Phase);
            Assert.AreEqual(state1.IsNight, state2.IsNight);
            Assert.AreEqual(state1.SeasonIndex, state2.SeasonIndex);
        }
    }
}
