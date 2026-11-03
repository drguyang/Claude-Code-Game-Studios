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
    /// ⚠️ 「三值均在」是**今日**的形状:<c>ticks_per_season</c> / <c>seasons_per_year</c> /
    /// <c>weather_block_ticks</c> 三值**尚未裁定**,故**刻意不在 schema 里**
    /// (ADR-038 §D1:占位值与已裁定值在数据文件里同形,一旦混入就再也分不清
    /// 「这个数裁过没有」—— <c>480</c> 那次事故正是这个形态)。
    /// 它们落地时**追加字段 + 升 schema_version**,不新建产物。
    /// </remarks>
    public struct TimeParamsDataSet
    {
        /// <summary>一个世界日的 tick 数(36000 = 20 Hz 下现实 30 分钟)。</summary>
        public long TicksPerDay;

        /// <summary>夜的起点(日内相位,定义域 `[0, TicksPerDay)`)。</summary>
        public long NightStart;

        /// <summary>夜长(定义域 `(0, TicksPerDay)`)。</summary>
        public long NightSpan;

        /// <summary>烘焙时从源 JSON 文本派生的内容版本号(u32,ADR-014 §五)。</summary>
        public uint ConfigVersion;

        /// <summary>烘焙时随产物落盘的 schema 版本(源 <c>schema_version</c> 字段)。</summary>
        public uint SchemaVersion;
    }
}
