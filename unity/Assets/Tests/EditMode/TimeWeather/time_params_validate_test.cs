// AC-5-11 —— 装载期校验器,**逐参数一条用例**(不得合并成「检查配置合法」一条)。
//
// 权威来源:
//   design/gdd/time-and-weather.md:671(AC-5-11 全文)
//   ADR-038 §D1(五条里哪三条由装载路径触发、哪两条只到「夹具注入」层 · R4)
//
// 🔁 **2026-11-03(ADR-038 Amendment A)**:五条**全部**由装载路径触发 ——
//   ① `TICKS_PER_DAY = 0` ② `NIGHT_SPAN ≥ TICKS_PER_DAY` ③ `SEASONS_PER_YEAR < 2`
//   ④ `TICKS_PER_SEASON = 0` ⑤ `WEATHER_BLOCK_TICKS = 0`
//   ①②③④ 走 `BootRoot.ResolveTimeParams(provider)`;⑤ 走 `BootRoot.ResolveWeatherParams(provider)`。
//   ⇒ 每一条都同时有两条夹具:**直接注入** 与 **经装载路径**(假 provider 喂坏值)。
//   ⚠️ 「值未裁定」这一层已消失(2026-11-03 用户裁定 252000 / 5 / 6000 / 100)。
//
// ⚠️ 每条**独立**一个 `[Test]`(AC 明令「不得合并」),且都带**正控**(合法值不抛)——
//    否则「一律抛」也能骗过全部负控(F4 空洞正控)。

using System;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
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

        // ══════════════ 装载路径(Amendment A:五条全部可达)══════════════

        [Test]
        public void test_loadPath_positiveControl()
        {
            Assert.DoesNotThrow(() => BootRoot.ResolveTimeParams(
                new FakeProvider(ValidTimeData())));
            Assert.DoesNotThrow(() => BootRoot.ResolveWeatherParams(
                new FakeProvider(weather: new WeatherParamsDataSet
                {
                    WeatherBlockTicks = 6000L,
                    IntensityMax = 100L,
                })));
        }

        [Test]
        public void test_loadPath_ticksPerDayZero_throws()
        {
            TimeParamsDataSet d = ValidTimeData();
            d.TicksPerDay = 0L;
            var ex = Assert.Throws<ArgumentException>(
                () => BootRoot.ResolveTimeParams(new FakeProvider(d)));
            Assert.That(ex.Message, Does.Contain("TICKS_PER_DAY"));
        }

        [Test]
        public void test_loadPath_nightSpanNotLessThanDay_throws()
        {
            TimeParamsDataSet d = ValidTimeData();
            d.NightSpan = d.TicksPerDay;
            var ex = Assert.Throws<ArgumentException>(
                () => BootRoot.ResolveTimeParams(new FakeProvider(d)));
            Assert.That(ex.Message, Does.Contain("NIGHT_SPAN"));
        }

        [Test]
        public void test_loadPath_seasonsPerYearLessThanTwo_throws()
        {
            TimeParamsDataSet d = ValidTimeData();
            d.SeasonsPerYear = 1L;
            var ex = Assert.Throws<ArgumentException>(
                () => BootRoot.ResolveTimeParams(new FakeProvider(d)));
            Assert.That(ex.Message, Does.Contain("SEASONS_PER_YEAR"));
        }

        [Test]
        public void test_loadPath_ticksPerSeasonZero_throws()
        {
            TimeParamsDataSet d = ValidTimeData();
            d.TicksPerSeason = 0L;
            var ex = Assert.Throws<ArgumentException>(
                () => BootRoot.ResolveTimeParams(new FakeProvider(d)));
            Assert.That(ex.Message, Does.Contain("TICKS_PER_SEASON"));
        }

        [Test]
        public void test_loadPath_weatherBlockTicksZero_throws()
        {
            var ex = Assert.Throws<ArgumentException>(
                () => BootRoot.ResolveWeatherParams(new FakeProvider(
                    weather: new WeatherParamsDataSet
                    {
                        WeatherBlockTicks = 0L,
                        IntensityMax = 100L,
                    })));
            Assert.That(ex.Message, Does.Contain("WEATHER_BLOCK_TICKS"));
        }

        [Test]
        public void test_loadPath_nullProvider_returnsNull_notAValidatedConfig()
        {
            // 「未预载」不是「配置合法」—— 返回 null,由调用方决定是否启动。
            Assert.That(BootRoot.ResolveTimeParams(null), Is.Null);
            Assert.That(BootRoot.ResolveWeatherParams(null), Is.Null);
        }

        // ══════════════ 夹具 ══════════════

        private static TimeParamsDataSet ValidTimeData() => new TimeParamsDataSet
        {
            TicksPerDay = 36000L,
            NightStart = 27000L,
            NightSpan = 9000L,
            TicksPerSeason = 252000L,
            SeasonsPerYear = 5L,
        };

        /// <summary>只提供 5 的两份数据集的假 provider。</summary>
        private sealed class FakeProvider : IDataProvider
        {
            private readonly TimeParamsDataSet? _time;
            private readonly WeatherParamsDataSet? _weather;

            public FakeProvider(TimeParamsDataSet time) => _time = time;

            public FakeProvider(WeatherParamsDataSet weather) => _weather = weather;

            public TDataSet Load<TDataSet>() where TDataSet : struct
            {
                if (typeof(TDataSet) == typeof(TimeParamsDataSet) && _time.HasValue)
                    return (TDataSet)(object)_time.Value;
                if (typeof(TDataSet) == typeof(WeatherParamsDataSet) && _weather.HasValue)
                    return (TDataSet)(object)_weather.Value;
                throw new NotSupportedException($"夹具未提供 {typeof(TDataSet).Name}");
            }
        }
    }
}
