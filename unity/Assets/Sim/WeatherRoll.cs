// ADR-007 / GDD time-and-weather.md §F-5.3 —— 天气纯函数与块哈希掷骰。
//
// 权威来源:
//   ADR-007 §一 —— 掷骰走 IEventAuthority
//   GDD time-and-weather.md §Formulas F-5.3 —— Weather(t,cell) = Roll(WorldSeed, FDiv(t,WEATHER_BLOCK_TICKS), EcozoneOf(cell))
//
// 核心机制:
//   - 块哈希保证同块内天气恒定
//   - 无时间积分（B-8）
//   - 掷骰输入完全可重构（三源不变量）

using System;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>
    /// 天气参数（从烘焙数据装载）。
    /// </summary>
    public sealed class WeatherParams
    {
        public long WEATHER_BLOCK_TICKS;
        public long INTENSITY_MAX;

        public void Validate()
        {
            if (WEATHER_BLOCK_TICKS <= 0)
                throw new ArgumentException("WEATHER_BLOCK_TICKS 必须 > 0", nameof(WEATHER_BLOCK_TICKS));
            if (INTENSITY_MAX <= 0)
                throw new ArgumentException("INTENSITY_MAX 必须 > 0", nameof(INTENSITY_MAX));
        }
    }

    /// <summary>
    /// 天气状态。
    /// </summary>
    public readonly struct WeatherState
    {
        public readonly int Kind;
        public readonly int Intensity;

        public WeatherState(int kind, int intensity)
        {
            Kind = kind;
            Intensity = intensity;
        }
    }

    /// <summary>
    /// 天气纯函数 —— 块哈希掷骰。
    /// </summary>
    public static class WeatherRoll
    {
        /// <summary>
        /// 计算块索引。
        /// </summary>
        public static long ComputeBlockIndex(long tick, WeatherParams p)
        {
            return TimeBase.FDiv(tick, p.WEATHER_BLOCK_TICKS);
        }

        /// <summary>
        /// 计算天气（块哈希掷骰）。
        /// </summary>
        public static WeatherState ComputeWeather(
            ulong worldSeed,
            long tick,
            int ecozoneId,
            WeatherParams p)
        {
            long blockIndex = ComputeBlockIndex(tick, p);

            // 块哈希掷骰（简化版：使用 SplitMix64 哈希）
            ulong hash = SplitMix64.Hash((long)worldSeed, blockIndex, ecozoneId);

            // 从哈希提取 kind 和 intensity
            int kind = (int)(hash % 4); // 4 种天气类型
            int intensity = (int)((hash >> 32) % (ulong)p.INTENSITY_MAX);

            return new WeatherState(kind, intensity);
        }

        /// <summary>
        /// 验证同块内天气恒定。
        /// </summary>
        public static bool IsConstantWithinBlock(
            ulong worldSeed,
            long tick1,
            long tick2,
            int ecozoneId,
            WeatherParams p)
        {
            var w1 = ComputeWeather(worldSeed, tick1, ecozoneId, p);
            var w2 = ComputeWeather(worldSeed, tick2, ecozoneId, p);

            // 同块内天气应恒定
            if (ComputeBlockIndex(tick1, p) != ComputeBlockIndex(tick2, p))
            {
                return false; // 跨块不要求恒定
            }

            return w1.Kind == w2.Kind && w1.Intensity == w2.Intensity;
        }
    }
}
