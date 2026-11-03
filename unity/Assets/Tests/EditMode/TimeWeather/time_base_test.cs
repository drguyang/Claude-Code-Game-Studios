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
            // 🔁 2026-11-03 收编轮:旧断言钉的是**占位值**(480 / 360 / 240),
            //    与已裁定值(36000 / 27000 / 9000)不符 ⇒ 必须随值更新。
            //    ⚠️ 这不是「为让红变绿而改测试」:值由用户 2026-11-03 裁定,
            //    旧值在 GDD 定值之前是占位,其注释「GDD 裁定 480」为假。
            // TICKS_PER_DAY = 36000, NIGHT_START = 27000, NIGHT_SPAN = 9000
            // 夜 = [27000, 36000)(恰好不跨日)
            Assert.IsTrue(TimeBase.IsNight(27000), "夜的起点");
            Assert.IsFalse(TimeBase.IsNight(26999), "夜前一 tick 仍是昼");
            Assert.IsFalse(TimeBase.IsNight(0), "纪元起点是昼(旧值下曾是「夜」)");
            Assert.IsTrue(TimeBase.IsNight(35999), "当日最后一 tick 仍在夜");
        }

        [Test]
        public void test_seasonIndex_correctRange()
        {
            // seasonLength = TICKS_PER_DAY * 30 = 1,080,000(⚠️ 该式是**占位**,
            // 与 F-5.2 的 `TICKS_PER_SEASON` 不等价 —— 见 TimeBase 文件头)。
            Assert.AreEqual(0, TimeBase.ComputeSeasonIndex(0));
            Assert.AreEqual(1, TimeBase.ComputeSeasonIndex(1080000));
            // 4 季 × 1,080,000 = 4,320,000 ⇒ 回绕到 0
            Assert.AreEqual(0, TimeBase.ComputeSeasonIndex(4320000));
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
