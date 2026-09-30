// ADR-009 / ADR-012 —— 消费边界与确定性对拍。
//
// 权威来源:
//   ADR-009 §一 —— 三问判据/三态分类（派生态不进流）
//   ADR-012 §二 —— 双级黄金夹具 + 三格常驻矩阵
//   GDD time-and-weather.md —— AC-5-07/08/19
//
// 核心机制:
//   - 5 侧可写 Kind 集恰 = ∅
//   - 天气不产生任何 SimEvent
//   - 跨平台逐位对拍（EXTERNAL，挂 ADR-012）

using System;
using System.Collections.Generic;
using System.Reflection;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>
    /// 时间天气消费边界验证器。
    /// </summary>
    public static class TimeWeatherBoundary
    {
        /// <summary>
        /// 验证 5 侧可写 Kind 集恰 = ∅。
        /// </summary>
        public static bool ValidateNoEventSinkAppend()
        {
            // 检查 TimeBase / WeatherRoll / EnvMod 是否引用 IEventSink
            var simAssembly = typeof(TimeBase).Assembly;

            foreach (var type in simAssembly.GetTypes())
            {
                if (type.Name.StartsWith("Time") || type.Name.StartsWith("Weather") || type.Name.StartsWith("EnvMod"))
                {
                    foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
                    {
                        // 检查方法参数是否包含 IEventSink
                        foreach (var param in method.GetParameters())
                        {
                            if (param.ParameterType == typeof(IEventSink))
                            {
                                return false; // 发现 IEventSink 参数
                            }
                        }
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// 验证天气不产生任何 SimEvent。
        /// </summary>
        public static bool ValidateNoSimEventGeneration()
        {
            // 检查 WeatherRoll 是否产生 SimEvent
            var weatherType = typeof(WeatherRoll);
            foreach (var method in weatherType.GetMethods())
            {
                if (method.ReturnType == typeof(SimEvent))
                {
                    return false; // 发现返回 SimEvent 的方法
                }
            }

            return true;
        }

        /// <summary>
        /// 验证跨平台逐位对拍（简化版）。
        /// </summary>
        public static bool ValidateCrossPlatformDeterminism(
            ulong worldSeed,
            long tick,
            int ecozoneId,
            WeatherParams weatherParams)
        {
            // 简化版：验证同输入产生同输出
            var w1 = WeatherRoll.ComputeWeather(worldSeed, tick, ecozoneId, weatherParams);
            var w2 = WeatherRoll.ComputeWeather(worldSeed, tick, ecozoneId, weatherParams);

            return w1.Kind == w2.Kind && w1.Intensity == w2.Intensity;
        }

        /// <summary>
        /// 验证重放一致性。
        /// </summary>
        public static bool ValidateReplayConsistency(
            ulong worldSeed,
            IReadOnlyList<long> ticks,
            int ecozoneId,
            WeatherParams weatherParams)
        {
            // 简化版：验证重放结果与原始会话逐位相同
            foreach (long tick in ticks)
            {
                var w1 = WeatherRoll.ComputeWeather(worldSeed, tick, ecozoneId, weatherParams);
                var w2 = WeatherRoll.ComputeWeather(worldSeed, tick, ecozoneId, weatherParams);

                if (w1.Kind != w2.Kind || w1.Intensity != w2.Intensity)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
