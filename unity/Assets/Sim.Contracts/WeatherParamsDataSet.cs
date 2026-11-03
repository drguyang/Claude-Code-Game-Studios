// 权威来源:ADR-038 Amendment A(5 的天气参数外部化)· ADR-014 §二/§五(*.cooked 取数形状)
//          · GDD design/gdd/time-and-weather.md §F-5.3 / §Tuning Knobs
//
// ⚠️ 天气参数是 5 的**另一张表**(单表单文件)⇒ 独立产物 `5_weather.cooked.bytes`,
//    不塞进 `5_time.json`。两值 2026-11-03 由用户裁定:
//    `WEATHER_BLOCK_TICKS = 6000`(= 5 分钟实时,且整除 36000)· `INTENSITY_MAX = 100`。
//
// ⚠️ 字段序与 `Editor.Tools.Bake.WeatherParamsCookedWriter` /
//    `Gameplay.Presentation.WeatherParamsCookedCodec` **严格镜像** —— 改一处必须改三处。

namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary>5 天气参数集(<c>5_weather.cooked.bytes</c> 的解码形)。</summary>
    public struct WeatherParamsDataSet
    {
        /// <summary>天气相干块长(tick;定义域 `≥ 1`)。块内天气恒定,跨块重掷。</summary>
        public long WeatherBlockTicks;

        /// <summary>天气强度上界(定义域 `&gt; 0`;强度轴按此归一)。</summary>
        public long IntensityMax;

        /// <summary>烘焙时从源 JSON 文本派生的内容版本号(u32,ADR-014 §五)。</summary>
        public uint ConfigVersion;

        /// <summary>烘焙时随产物落盘的 schema 版本。</summary>
        public uint SchemaVersion;
    }
}
