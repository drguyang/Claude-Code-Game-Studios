// 权威来源:ADR-038 §D1 / §D5(时间参数外部化 · 数据形住 Sim.Contracts)
//          · ADR-014 §二/§五(*.cooked 唯一的跨装配取数形状)
//          · CookedFormat.cs(布局共参照)
//
// ⚠️ **为什么这个 struct 住 Sim.Contracts 而不是 Sim**:
//    `IDataProvider.Load<T>() where T : struct` 的 `T` 必须由**调用方装配**(Gameplay.Boot)可见,
//    而 `Sim` 的引用集恰 = {BCL, Sim.Contracts}(门 A / ADR-025 §①)⇒ 放 `Sim` 会让 Boot 侧够不着。
//    `Validate()` 之类的**领域规则**不在这里(它们住 `Sim.TimeParams`),本件只承载**形状**。
//
// ⚠️ 本 struct = `<c>5_time.cooked.bytes</c>` 的解码形。字段序与
//    `Editor.Tools.Bake.TimeParamsCookedWriter` / `Gameplay.Presentation.TimeParamsCookedCodec`
//    **严格镜像** —— 改一处必须改三处(写方 / 读方 / 本件)。

namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary>5 时间天气的系统参数集(烘焙产物 <c>5_time.cooked.bytes</c> 的解码形)。</summary>
    /// <remarks>
    /// 🔁 **2026-11-03 用户裁定 + ADR-038 Amendment A**:季参数两值已裁定
    /// (`ticks_per_season = 252000` · `seasons_per_year = 5`)⇒ 追加进 schema(schema_version 2)。
    /// 天气两值拆到独立产物 `5_weather.cooked.bytes`(`WeatherParamsDataSet`),
    /// 因为天气与环境参数是 5 的另一张表(单表单文件)。
    /// <para>⚠️ 占位值与已裁定值在数据文件里同形 —— 值未裁前不得先塞占位值(ADR-038 §D1)。</para>
    /// </remarks>
    public struct TimeParamsDataSet
    {
        /// <summary>一个世界日的 tick 数(36000 = 20 Hz 下现实 30 分钟)。</summary>
        public long TicksPerDay;

        /// <summary>夜的起点(日内相位,定义域 `[0, TicksPerDay)`)。</summary>
        public long NightStart;

        /// <summary>夜长(定义域 `(0, TicksPerDay)`)。</summary>
        public long NightSpan;

        /// <summary>一季的 tick 数(定义域 `&gt; 0`;2026-11-03 裁定 = 252000 = 7 日)。</summary>
        public long TicksPerSeason;

        /// <summary>一年几季(定义域 `≥ 2`;2026-11-03 裁定 = 5,与 17 的 `SEASON_MULT` 表长相等)。</summary>
        public long SeasonsPerYear;

        /// <summary>烘焙时从源 JSON 文本派生的内容版本号(u32,ADR-014 §五)。</summary>
        public uint ConfigVersion;

        /// <summary>烘焙时随产物落盘的 schema 版本(源 <c>schema_version</c> 字段)。</summary>
        public uint SchemaVersion;
    }
}
