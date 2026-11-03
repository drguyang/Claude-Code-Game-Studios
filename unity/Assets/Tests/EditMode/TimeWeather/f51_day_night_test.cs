// `TimeBase` 的 F-5.1 昼夜夹具(2026-11-03)。
// 🔁 同日收编:`Sim/Time/WorldTime` 已删除,常量与公式全部并回 `Sim.World.TimeBase`
//    —— 全仓此前同时存在两套时间实现(480 vs 36000),那正是 AC-5-05 要防的形态。
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
//    它们今日仍无落点,登记在 `Sim/World/TimeBase.cs` 的文件头。

using DaYiJingCheng.Sim.World;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.TimeWeather
{
    [TestFixture]
    public class F51DayNightTest
    {
        [Test]
        public void test_ticksPerDay_isTheAdjudicatedValue()
        {
            // 字面量哨兵:若有人改了常量,本条必须红(不得写成 `Is.EqualTo(TimeBase.TICKS_PER_DAY)`)
            Assert.That(TimeBase.TICKS_PER_DAY, Is.EqualTo(36000),
                "TICKS_PER_DAY = 36000(20 Hz ⇒ 30 分钟一日)是 2026-11-03 用户裁定;" +
                "改它 = 重裁级(所有历史事件时间戳与存档同时失效)");
        }

        [Test]
        public void test_ticksPerDay_matchesHalfHourAt20Hz()
        {
            // 量纲自检:20 Hz × 60 s × 30 min = 36000。若哪天 tick 频率被重裁,
            // 本条会把「日长还是旧值」这件事照出来。
            const int hz = 20, secondsPerMinute = 60, minutesPerDay = 30;
            Assert.That(TimeBase.TICKS_PER_DAY, Is.EqualTo(hz * secondsPerMinute * minutesPerDay),
                "日长必须与「20 Hz · 30 分钟一日」自洽");
        }

        [Test]
        public void test_dayNumber_isOneBased_atBoundaries()
        {
            Assert.That(TimeBase.DayNumberOf(0L), Is.EqualTo(1L), "tick 0 = 第 1 日(1 起)");
            Assert.That(TimeBase.DayNumberOf(35999L), Is.EqualTo(1L), "第 1 日最后一 tick");
            Assert.That(TimeBase.DayNumberOf(36000L), Is.EqualTo(2L), "整日边界进第 2 日");
            Assert.That(TimeBase.DayNumberOf(71999L), Is.EqualTo(2L));
            Assert.That(TimeBase.DayNumberOf(72000L), Is.EqualTo(3L));
        }

        [Test]
        public void test_dayNumber_negativeTick_doesNotThrow()
        {
            // 负 tick 不在 ADR-005 的合法域内;显示面按第 1 日处理,不得抛。
            Assert.DoesNotThrow(() => TimeBase.DayNumberOf(-1L));
            Assert.That(TimeBase.DayNumberOf(-1L), Is.EqualTo(1L));
        }

        // ══════════════════════════════════════════════════════════════════
        // F-5.1 昼夜
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_f51_values_areTheAdjudicatedOnes()
        {
            // 字面量哨兵(不得写成 Is.EqualTo(TimeBase.NIGHT_START) —— 那是自指判据)
            Assert.That(TimeBase.NIGHT_START, Is.EqualTo(27000L));
            Assert.That(TimeBase.NIGHT_SPAN, Is.EqualTo(9000L));
        }

        [Test]
        public void test_f51_productionValues_boundaries()
        {
            // 生产值 27000 / 9000:夜 = 一日之末四分之一(27000..35999)
            Assert.That(TimeBase.IsNight(26999L), Is.False, "夜前一 tick 仍是昼");
            Assert.That(TimeBase.IsNight(27000L), Is.True, "夜的起点含");
            Assert.That(TimeBase.IsNight(35999L), Is.True, "当日最后一 tick 仍在夜");
            Assert.That(TimeBase.IsNight(36000L), Is.False, "下一日首 tick 回到昼");
            Assert.That(TimeBase.IsNight(0L), Is.False, "纪元起点是昼");
        }

        [Test]
        public void test_f51_naiveRangeWouldBeWrong_acrossDayWrap()
        {
            // ⚠️ **本条是 FMod 式的唯一防线**。生产值 27000+9000 恰好不跨日,
            //    拿生产值写不出跨日环绕的判据 ⇒ 必须注入一组**会跨日**的配置。
            //    起始 30000 · 长 12000 ⇒ 夜 = [30000,36000) ∪ [0,6000)。
            //    朴素写法 `phase >= start && phase < start + span` 会漏掉后一段 ⇒ 本组红。
            Assert.That(TimeBase.IsNightAt(30000L, 30000L, 12000L), Is.True, "夜的起点");
            Assert.That(TimeBase.IsNightAt(35999L, 30000L, 12000L), Is.True, "跨日之前");
            Assert.That(TimeBase.IsNightAt(0L, 30000L, 12000L), Is.True,
                "**跨日之后**那一段 —— 朴素区间比较正是在这里漏判");
            Assert.That(TimeBase.IsNightAt(5999L, 30000L, 12000L), Is.True, "跨日段的最后一 tick");
            Assert.That(TimeBase.IsNightAt(6000L, 30000L, 12000L), Is.False, "跨日段结束");
            Assert.That(TimeBase.IsNightAt(29999L, 30000L, 12000L), Is.False, "夜前一 tick");
        }

        [Test]
        public void test_f51_negativeTick_usesWrapSafeModulo()
        {
            // C# 的 `%` 是截断语义:若直接 `phase - start` 再用 `%`,负数会让
            // `< span` 恒真 ⇒ 正午被判成夜(GDD :288 点名的那条)。
            Assert.That(TimeBase.IsNight(-1L), Is.True,
                "tick -1 ⇒ phase 35999 ⇒ 夜(环绕,不是「负相位」)");
            Assert.That(TimeBase.IsNight(-27000L), Is.False,
                "tick -27000 ⇒ phase 9000 ⇒ 昼");
        }
    }
}
