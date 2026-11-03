// Story 003: 时间基准 —— tick 只读与昼夜/季节相位推导
//
// AC-5-01: phase = FMod(tick, TICKS_PER_DAY)
// AC-5-02: isNight = FMod(phase - NIGHT_START, TICKS_PER_DAY) < NIGHT_SPAN
// AC-5-03: season_index = FMod(FDiv(tick, TICKS_PER_SEASON), SEASONS_PER_YEAR)
// AC-5-04: 同输入同输出(纯函数确定性)
//
// 🔁 2026-11-03 两轮形态变化:
//   ① 收编轮:旧断言钉的是**占位值**(480 / 360 / 240),与已裁定值不符 ⇒ 已随值更新。
//   ② ADR-038:值**已外部化到 `assets/data/5_time.json`** ⇒ 本夹具改为**注入 `TimeParams`**,
//      不再引用任何代码常量。

using NUnit.Framework;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.World;

namespace DaYiJingCheng.Tests.TimeWeather
{
    public class TimeBaseTest
    {
        /// <summary>已裁定值(2026-11-03)的字面量副本 —— 本类测公式,真值由烘焙侧夹具断言。</summary>
        private static TimeParams P() => new TimeParams
        {
            TICKS_PER_DAY = 36000L,
            NIGHT_START = 27000L,
            NIGHT_SPAN = 9000L,
        };

        [Test]
        public void test_phase_equalsFMod()
        {
            long tick = 12345;
            long expected = TimeBase.FMod(tick, P().TICKS_PER_DAY);
            Assert.AreEqual(expected, TimeBase.ComputePhase(tick, P()));
        }

        [Test]
        public void test_isNight_correctRange()
        {
            // TICKS_PER_DAY = 36000, NIGHT_START = 27000, NIGHT_SPAN = 9000
            // 夜 = [27000, 36000)(恰好不跨日)
            TimeParams p = P();
            Assert.IsTrue(TimeBase.IsNight(27000, p), "夜的起点");
            Assert.IsFalse(TimeBase.IsNight(26999, p), "夜前一 tick 仍是昼");
            Assert.IsFalse(TimeBase.IsNight(0, p), "纪元起点是昼(旧值下曾是「夜」)");
            Assert.IsTrue(TimeBase.IsNight(35999, p), "当日最后一 tick 仍在夜");
        }

        [Test]
        public void test_seasonIndex_correctRange()
        {
            // seasonLength = TICKS_PER_DAY * 30 = 1,080,000(⚠️ 该式是**占位**,
            // 与 F-5.2 的 `TICKS_PER_SEASON` 不等价 —— 见 TimeBase 文件头)。
            TimeParams p = P();
            Assert.AreEqual(0, TimeBase.ComputeSeasonIndex(0, p));
            Assert.AreEqual(1, TimeBase.ComputeSeasonIndex(1080000, p));
            // 4 季 × 1,080,000 = 4,320,000 ⇒ 回绕到 0
            Assert.AreEqual(0, TimeBase.ComputeSeasonIndex(4320000, p));
        }

        [Test]
        public void test_deterministic_sameInputSameOutput()
        {
            long tick = 99999;
            var state1 = TimeBase.ComputeState(tick, P());
            var state2 = TimeBase.ComputeState(tick, P());
            Assert.AreEqual(state1.Phase, state2.Phase);
            Assert.AreEqual(state1.IsNight, state2.IsNight);
            Assert.AreEqual(state1.SeasonIndex, state2.SeasonIndex);
        }
    }
}
