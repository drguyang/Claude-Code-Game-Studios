// 权威来源:ADR-038 §D3(存档 v4 记时间参数)· ADR-038 Amendment A(存档 v5 追加季/天气四值)
//
// ⚠️ 本类型是**写档时的入参**,不是时间系统的载体(`Sim.TimeParams` / `Sim.WeatherParams` 才是)。
//    分开的理由:`Sim.Codec` 不引用 `Sim`(它只依赖 `Sim.Contracts`)⇒
//    不能把领域类型拖进来;而且写档只需要七个整数值。
//
// 🔁 **2026-11-03 Amendment A(用户裁定「三个时间参数全记」扩展到「同时进存档 v5」)**:
//    v4 只记 `TICKS_PER_DAY / NIGHT_START / NIGHT_SPAN`;v5 追加
//    `TICKS_PER_SEASON / SEASONS_PER_YEAR / WEATHER_BLOCK_TICKS / INTENSITY_MAX`。
//    理由与 D3 同一条:这四个量都是**模拟 / 掷骰的输入**(季节长度 → 17 的采集;
//    天气块长 → 5 的天气掷骰),不随档走 ⇒ 旧档重放出的世界与玩家经历的不同,
//    而 `ConfigVersion` 不匹配按 ADR-010 §七 **非致命**。

namespace DaYiJingCheng.Sim.Codec
{
    /// <summary>写入存档头 v5 的七个参数(时间三值 + 季二值 + 天气二值)。</summary>
    /// <remarks>
    /// ⚠️ **刻意不提供「默认值 / Legacy 常量」** —— 本仓历史上的
    /// `36000/27000/9000` 只允许出现在**迁移脚本**(`SaveMigrationV3ToV4`,记录事实)与夹具里。
    /// 生产写档若拿不到真值,应当**抛**,而不是回落到一个恰好正确的常数 ——
    /// 那会让「配置改错」与「一切正常」不可分。
    /// </remarks>
    public readonly struct SaveTimeParams
    {
        /// <summary>一个世界日的 tick 数(写档时须 &gt; 0)。</summary>
        public readonly long TicksPerDay;

        /// <summary>夜的起点(日内相位)。</summary>
        public readonly long NightStart;

        /// <summary>夜长。</summary>
        public readonly long NightSpan;

        /// <summary>一季的 tick 数(须 &gt; 0;2026-11-03 裁定 = 252000)。</summary>
        public readonly long TicksPerSeason;

        /// <summary>一年几季(须 ≥ 2;2026-11-03 裁定 = 5,与 17 的 `SEASON_MULT` 表长相等)。</summary>
        public readonly long SeasonsPerYear;

        /// <summary>天气相干块长(须 &gt; 0;2026-11-03 裁定 = 6000)。</summary>
        public readonly long WeatherBlockTicks;

        /// <summary>天气强度上界(须 &gt; 0;2026-11-03 裁定 = 100)。</summary>
        public readonly long IntensityMax;

        /// <summary>构造(七值齐;缺一不可 —— 见 <see cref="IsSpecified"/>)。</summary>
        public SaveTimeParams(
            long ticksPerDay, long nightStart, long nightSpan,
            long ticksPerSeason, long seasonsPerYear,
            long weatherBlockTicks, long intensityMax)
        {
            TicksPerDay = ticksPerDay;
            NightStart = nightStart;
            NightSpan = nightSpan;
            TicksPerSeason = ticksPerSeason;
            SeasonsPerYear = seasonsPerYear;
            WeatherBlockTicks = weatherBlockTicks;
            IntensityMax = intensityMax;
        }

        /// <summary>
        /// 是否已给出**非零**值(七个都 &gt; 0 才为真)。
        /// <para>⚠️ 本判据**只判「填了没有」**,不判「填得对不对」——
        /// `NIGHT_SPAN &lt; TICKS_PER_DAY` / `SEASONS_PER_YEAR ≥ 2` 这类**关系**归
        /// `Sim.TimeParams.Validate()` / `ValidateSeason()` / `Sim.WeatherParams.Validate()`。
        /// 两层分开的理由:写档侧要的是「缺值 ⇒ 明确报缺值」;把关系塞进这里会让
        /// 「值非法」伪装成「没给值」(`InvalidOperationException` 冒充 `ArgumentException`,
        /// 归因指向错误的修复方向 —— 实测踩到过)。</para>
        /// </summary>
        public bool IsSpecified =>
            TicksPerDay > 0L
            && NightSpan > 0L
            && TicksPerSeason > 0L
            && SeasonsPerYear > 0L
            && WeatherBlockTicks > 0L
            && IntensityMax > 0L;
    }
}
