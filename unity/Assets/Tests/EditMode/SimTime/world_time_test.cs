// `WorldTime` 的夹具(2026-11-03 · 甲方案第二层 · 同日按用户裁定从 Sim.Contracts 移入 Sim.Time)。
//
// 权威来源:
//   design/gdd/time-and-weather.md §规则四 / §Tuning Knobs(`:516` = 36000,2026-11-03 用户裁定)
//   `AC-5-05` —— `TICKS_PER_DAY` 在代码中**只有一个定义点**
//
// 断两件事:
//   ① **值** = 36000(用户裁定,写死在这里当哨兵 —— 不引用被测常量,防自指判据);
//   ② **1 起的边界**:tick 0 / 35999 / 36000 / 71999 / 72000 各归哪一日。
//      这条是「第零日 / 第一日」差一的唯一可观测面:UI 上只是少写或多写一个字,
//      不会崩,也不会被别的夹具照出来。
//
// ⚠️ 本文件**不**断系统 5 的任何其它常量(`TICKS_PER_SEASON` / `NIGHT_*` …)——
//    它们今日在代码里没有落点,登记在 `WorldTimeConstants.cs` 的文件头。

using DaYiJingCheng.Sim.Time;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.SimTime
{
    [TestFixture]
    public class WorldTimeTest
    {
        [Test]
        public void test_ticksPerDay_isTheAdjudicatedValue()
        {
            // 字面量哨兵:若有人改了常量,本条必须红(不得写成 `Is.EqualTo(WorldTime.TicksPerDay)`)
            Assert.That(WorldTime.TicksPerDay, Is.EqualTo(36000),
                "TICKS_PER_DAY = 36000(20 Hz ⇒ 30 分钟一日)是 2026-11-03 用户裁定;" +
                "改它 = 重裁级(所有历史事件时间戳与存档同时失效)");
        }

        [Test]
        public void test_ticksPerDay_matchesHalfHourAt20Hz()
        {
            // 量纲自检:20 Hz × 60 s × 30 min = 36000。若哪天 tick 频率被重裁,
            // 本条会把「日长还是旧值」这件事照出来。
            const int hz = 20, secondsPerMinute = 60, minutesPerDay = 30;
            Assert.That(WorldTime.TicksPerDay, Is.EqualTo(hz * secondsPerMinute * minutesPerDay),
                "日长必须与「20 Hz · 30 分钟一日」自洽");
        }

        [Test]
        public void test_dayNumber_isOneBased_atBoundaries()
        {
            Assert.That(WorldTime.DayNumberOf(0L), Is.EqualTo(1L), "tick 0 = 第 1 日(1 起)");
            Assert.That(WorldTime.DayNumberOf(35999L), Is.EqualTo(1L), "第 1 日最后一 tick");
            Assert.That(WorldTime.DayNumberOf(36000L), Is.EqualTo(2L), "整日边界进第 2 日");
            Assert.That(WorldTime.DayNumberOf(71999L), Is.EqualTo(2L));
            Assert.That(WorldTime.DayNumberOf(72000L), Is.EqualTo(3L));
        }

        [Test]
        public void test_dayNumber_negativeTick_doesNotThrow()
        {
            // 负 tick 不在 ADR-005 的合法域内;显示面按第 1 日处理,不得抛。
            Assert.DoesNotThrow(() => WorldTime.DayNumberOf(-1L));
            Assert.That(WorldTime.DayNumberOf(-1L), Is.EqualTo(1L));
        }

        // ══════════════════════════════════════════════════════════════════
        // F-5.1 昼夜
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_f51_values_areTheAdjudicatedOnes()
        {
            // 字面量哨兵(不得写成 Is.EqualTo(WorldTime.NightStart) —— 那是自指判据)
            Assert.That(WorldTime.NightStart, Is.EqualTo(27000L));
            Assert.That(WorldTime.NightSpan, Is.EqualTo(9000L));
        }

        [Test]
        public void test_f51_productionValues_boundaries()
        {
            // 生产值 27000 / 9000:夜 = 一日之末四分之一(27000..35999)
            Assert.That(WorldTime.IsNight(26999L), Is.False, "夜前一 tick 仍是昼");
            Assert.That(WorldTime.IsNight(27000L), Is.True, "夜的起点含");
            Assert.That(WorldTime.IsNight(35999L), Is.True, "当日最后一 tick 仍在夜");
            Assert.That(WorldTime.IsNight(36000L), Is.False, "下一日首 tick 回到昼");
            Assert.That(WorldTime.IsNight(0L), Is.False, "纪元起点是昼");
        }

        [Test]
        public void test_f51_naiveRangeWouldBeWrong_acrossDayWrap()
        {
            // ⚠️ **本条是 FMod 式的唯一防线**。生产值 27000+9000 恰好不跨日,
            //    拿生产值写不出跨日环绕的判据 ⇒ 必须注入一组**会跨日**的配置。
            //    起始 30000 · 长 12000 ⇒ 夜 = [30000,36000) ∪ [0,6000)。
            //    朴素写法 `phase >= start && phase < start + span` 会漏掉后一段 ⇒ 本组红。
            Assert.That(WorldTime.IsNightAt(30000L, 30000L, 12000L), Is.True, "夜的起点");
            Assert.That(WorldTime.IsNightAt(35999L, 30000L, 12000L), Is.True, "跨日之前");
            Assert.That(WorldTime.IsNightAt(0L, 30000L, 12000L), Is.True,
                "**跨日之后**那一段 —— 朴素区间比较正是在这里漏判");
            Assert.That(WorldTime.IsNightAt(5999L, 30000L, 12000L), Is.True, "跨日段的最后一 tick");
            Assert.That(WorldTime.IsNightAt(6000L, 30000L, 12000L), Is.False, "跨日段结束");
            Assert.That(WorldTime.IsNightAt(29999L, 30000L, 12000L), Is.False, "夜前一 tick");
        }

        [Test]
        public void test_f51_negativeTick_usesWrapSafeModulo()
        {
            // C# 的 `%` 是截断语义:若直接 `phase - start` 再用 `%`,负数会让
            // `< span` 恒真 ⇒ 正午被判成夜(GDD :288 点名的那条)。
            Assert.That(WorldTime.IsNight(-1L), Is.True,
                "tick -1 ⇒ phase 35999 ⇒ 夜(环绕,不是「负相位」)");
            Assert.That(WorldTime.IsNight(-27000L), Is.False,
                "tick -27000 ⇒ phase 9000 ⇒ 昼");
        }
    }
}
