// AC-5-11 —— 装载期校验器,**逐参数一条用例**(不得合并成「检查配置合法」一条)。
//
// 权威来源:
//   design/gdd/time-and-weather.md:671(AC-5-11 全文)
//   ADR-038 §D1(五条里哪三条由装载路径触发、哪两条只到「夹具注入」层 · R4)
//
// ⚠️ **判据归属(必须如实区分,不得混记成「五条全绿」)**:
//   ① `TICKS_PER_DAY = 0`          —— 装载路径可达(`TimeParams.Validate`,数据文件可写坏)
//   ② `NIGHT_SPAN ≥ TICKS_PER_DAY` —— 同上
//   ③ `SEASONS_PER_YEAR < 2`       —— **只到夹具注入层**(两个季参数值未裁定、不在 schema 里)
//   ④ `TICKS_PER_SEASON = 0`       —— 同上
//   ⑤ `WEATHER_BLOCK_TICKS = 0`    —— `WeatherParams.Validate` 已有(C-5 路径),装载路径同上
//   AC-5-11 的 GIVEN 原文就是「**注入**各非法配置」⇒ ①–⑤ 的夹具注入面均成立;
//   但「由装载路径触发」这一层,③④⑤ 今日 **NOT-RUN**(ADR-038 §D1 / R4)。
//
// ⚠️ 每条**独立**一个 `[Test]`(AC 明令「不得合并」),且都带**正控**(合法值不抛)——
//    否则「一律抛」也能骗过全部负控(F4 空洞正控)。

using System;
using DaYiJingCheng.Sim;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.TimeWeather
{
    /// <summary>AC-5-11:逐参数注入非法配置,每一种都硬失败(异常,不静默钳正)。</summary>
    [TestFixture]
    public class TimeParamsValidateTest
    {
        private static TimeParams Valid() => new TimeParams
        {
            TICKS_PER_DAY = 36000L,
            NIGHT_START = 27000L,
            NIGHT_SPAN = 9000L,
            TICKS_PER_SEASON = 1080000L,
            SEASONS_PER_YEAR = 4L,
        };

        private static WeatherParams ValidWeather() => new WeatherParams
        {
            WEATHER_BLOCK_TICKS = 6000L,
            // INTENSITY_MAX 也是 `Validate()` 的守卫项(天气强度上限)。
            // 正控只给 0 会撞上第二条守卫 —— 那样正控是在证明「有一部分字段没填」,
            // 而不是证明「合法配置能过」。
            INTENSITY_MAX = 100L,
        };

        [Test]
        public void test_validConfig_doesNotThrow_positiveControl()
        {
            // 正控:没有它,下面五条「必须抛」都可以被「一律抛」满足
            Assert.DoesNotThrow(() => Valid().Validate());
            Assert.DoesNotThrow(() => Valid().ValidateSeason());
            Assert.DoesNotThrow(() => ValidWeather().Validate());
        }

        [Test]
        public void test_ticksPerDayZero_throws()
        {
            TimeParams p = Valid();
            p.TICKS_PER_DAY = 0L;
            var ex = Assert.Throws<ArgumentException>(() => p.Validate());
            Assert.That(ex.Message, Does.Contain("TICKS_PER_DAY"));
        }

        [Test]
        public void test_nightSpanNotLessThanTicksPerDay_throws()
        {
            TimeParams p = Valid();
            p.NIGHT_SPAN = p.TICKS_PER_DAY;   // ≥ ⇒ 恒夜
            var ex = Assert.Throws<ArgumentException>(() => p.Validate());
            Assert.That(ex.Message, Does.Contain("NIGHT_SPAN"));
        }

        [Test]
        public void test_nightStartOutsideDay_throws()
        {
            // GDD `:519` 的 `START ∈ [0, TICKS_PER_DAY)` —— AC-5-11 未列,但同一条规则的另一半
            TimeParams p = Valid();
            p.NIGHT_START = p.TICKS_PER_DAY;
            var ex = Assert.Throws<ArgumentException>(() => p.Validate());
            Assert.That(ex.Message, Does.Contain("NIGHT_START"));
        }

        [Test]
        public void test_seasonsPerYearLessThanTwo_throws()
        {
            // ⚠️ 只到夹具注入层(ADR-038 §D1):季参数值未裁定,不在 5_time 的 schema 里
            TimeParams p = Valid();
            p.SEASONS_PER_YEAR = 1L;
            var ex = Assert.Throws<ArgumentException>(() => p.ValidateSeason());
            Assert.That(ex.Message, Does.Contain("SEASONS_PER_YEAR"));
        }

        [Test]
        public void test_ticksPerSeasonZero_throws()
        {
            // ⚠️ 同上:只到夹具注入层
            TimeParams p = Valid();
            p.TICKS_PER_SEASON = 0L;
            var ex = Assert.Throws<ArgumentException>(() => p.ValidateSeason());
            Assert.That(ex.Message, Does.Contain("TICKS_PER_SEASON"));
        }

        [Test]
        public void test_weatherBlockTicksZero_throws()
        {
            // ⚠️ 同上:只到夹具注入层
            WeatherParams p = ValidWeather();
            p.WEATHER_BLOCK_TICKS = 0L;
            var ex = Assert.Throws<ArgumentException>(() => p.Validate());
            Assert.That(ex.Message, Does.Contain("WEATHER_BLOCK_TICKS"));
        }
    }
}
