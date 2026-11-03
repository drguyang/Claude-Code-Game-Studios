// `TimeBase` 的 F-5.1 昼夜夹具(2026-11-03;同日两轮形态变化)。
//
// 权威来源:
//   design/gdd/time-and-weather.md §规则四 / §Tuning Knobs(`:516` = 36000,2026-11-03 用户裁定)
//   ADR-038 §D1 —— 三值**不在代码里**,本夹具因此**自带一份 `TimeParams` 字面量**。
//   `AC-5-05` —— 值的唯一定义点在**数据侧**(`assets/data/5_time.json`)。
//
// 🔁 2026-11-03 ADR-038:本夹具原写 `TimeBase.TICKS_PER_DAY`(代码常量)。常量已外部化
//    ⇒ 改为**构造 `TimeParams` 传入**。这**没有削弱**判据 —— 公式的正确性与值从哪来无关,
//    而「文件里到底是不是 36000」由 `time_params_bake_test` 对**真表**断言(职责分离)。
//
// 断三件事:
//   ① **公式与量纲**:dayNumber 的 1 起边界、20 Hz × 30 min = 36000 的自洽;
//   ② **昼夜边界**与**跨日环绕**(生产值不跨日 ⇒ 环绕只能靠注入参数守);
//   ③ 负 tick 的环绕语义。
//
// ⚠️ 本文件**不**断系统 5 的任何其它参数(`TICKS_PER_SEASON` / `SEASONS_PER_YEAR` /
//    `WEATHER_BLOCK_TICKS`):三者值未裁定、不在 `5_time.json` 的 schema 里(ADR-038 §D1)。

using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.World;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.TimeWeather
{
    [TestFixture]
    public class F51DayNightTest
    {
        /// <summary>
        /// 生产参数的**字面量副本**(2026-11-03 用户裁定)。
        /// <para>⚠️ 故意**不**从数据文件读 —— 本类测的是**公式**;数据文件的真值由烘焙侧夹具断言。
        /// 用字面量还能保证:若哪天公式实现改坏,这里与数据值无关地转红。</para>
        /// </summary>
        private static TimeParams Params(long tpd = 36000L, long start = 27000L, long span = 9000L)
            => new TimeParams { TICKS_PER_DAY = tpd, NIGHT_START = start, NIGHT_SPAN = span };

        // ⚠️ 原 `test_ticksPerDay_isTheAdjudicatedValue` / `..._matchesHalfHourAt20Hz` 两条
        //    **已迁往 `time_params_bake_test`**。理由:值不在代码里了(ADR-038 §D1),
        //    在本类里断言 `Params().TICKS_PER_DAY == 36000` 只是**断言自己传进去的字面量**
        //    (自指判据 · F3),对生产行为零判别力。真值必须对**真表**断言。

        [Test]
        public void test_dayNumber_isOneBased_atBoundaries()
        {
            TimeParams p = Params();
            Assert.That(TimeBase.DayNumberOf(0L, p), Is.EqualTo(1L), "tick 0 = 第 1 日(1 起)");
            Assert.That(TimeBase.DayNumberOf(35999L, p), Is.EqualTo(1L), "第 1 日最后一 tick");
            Assert.That(TimeBase.DayNumberOf(36000L, p), Is.EqualTo(2L), "整日边界进第 2 日");
            Assert.That(TimeBase.DayNumberOf(71999L, p), Is.EqualTo(2L));
            Assert.That(TimeBase.DayNumberOf(72000L, p), Is.EqualTo(3L));
        }

        [Test]
        public void test_dayNumber_negativeTick_doesNotThrow()
        {
            // 负 tick 不在 ADR-005 的合法域内;显示面按第 1 日处理,不得抛。
            TimeParams p = Params();
            Assert.DoesNotThrow(() => TimeBase.DayNumberOf(-1L, p));
            Assert.That(TimeBase.DayNumberOf(-1L, p), Is.EqualTo(1L));
        }

        // ══════════════════════════════════════════════════════════════════
        // F-5.1 昼夜
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_f51_productionValues_boundaries()
        {
            // 生产值 27000 / 9000:夜 = 一日之末四分之一(27000..35999)
            TimeParams p = Params();
            Assert.That(TimeBase.IsNight(26999L, p), Is.False, "夜前一 tick 仍是昼");
            Assert.That(TimeBase.IsNight(27000L, p), Is.True, "夜的起点含");
            Assert.That(TimeBase.IsNight(35999L, p), Is.True, "当日最后一 tick 仍在夜");
            Assert.That(TimeBase.IsNight(36000L, p), Is.False, "下一日首 tick 回到昼");
            Assert.That(TimeBase.IsNight(0L, p), Is.False, "纪元起点是昼");
        }

        [Test]
        public void test_f51_naiveRangeWouldBeWrong_acrossDayWrap()
        {
            // ⚠️ **本条是 FMod 式的唯一防线**。生产值 27000+9000 恰好不跨日,
            //    拿生产值写不出跨日环绕的判据 ⇒ 必须注入一组**会跨日**的配置。
            //    起始 30000 · 长 12000 ⇒ 夜 = [30000,36000) ∪ [0,6000)。
            //    朴素写法 `phase >= start && phase < start + span` 会漏掉后一段 ⇒ 本组红。
            const long tpd = 36000L, start = 30000L, span = 12000L;
            Assert.That(TimeBase.IsNightAt(30000L, start, span, tpd), Is.True, "夜的起点");
            Assert.That(TimeBase.IsNightAt(35999L, start, span, tpd), Is.True, "跨日之前");
            Assert.That(TimeBase.IsNightAt(0L, start, span, tpd), Is.True,
                "**跨日之后**那一段 —— 朴素区间比较正是在这里漏判");
            Assert.That(TimeBase.IsNightAt(5999L, start, span, tpd), Is.True, "跨日段的最后一 tick");
            Assert.That(TimeBase.IsNightAt(6000L, start, span, tpd), Is.False, "跨日段结束");
            Assert.That(TimeBase.IsNightAt(29999L, start, span, tpd), Is.False, "夜前一 tick");
        }

        [Test]
        public void test_f51_negativeTick_usesWrapSafeModulo()
        {
            // C# 的 `%` 是截断语义:若直接 `phase - start` 再用 `%`,负数会让
            // `< span` 恒真 ⇒ 正午被判成夜(GDD :288 点名的那条)。
            TimeParams p = Params();
            Assert.That(TimeBase.IsNight(-1L, p), Is.True,
                "tick -1 ⇒ phase 35999 ⇒ 夜(环绕,不是「负相位」)");
            Assert.That(TimeBase.IsNight(-27000L, p), Is.False,
                "tick -27000 ⇒ phase 9000 ⇒ 昼");
        }

        [Test]
        public void test_params_null_isRejected_notSilentDefault()
        {
            // ADR-038 §D1:参数没注入 ⇒ 抛,不得拿默认值静默算出一条假时间线。
            Assert.Throws<System.ArgumentNullException>(() => TimeBase.IsNight(0L, null));
            Assert.Throws<System.ArgumentNullException>(() => TimeBase.DayNumberOf(0L, null));
        }
    }
}
