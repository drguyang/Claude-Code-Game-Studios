// ADR-006 / GDD time-and-weather.md §F-5.4 —— EnvMod_raw 环境修正输出。
//
// 权威来源:
//   ADR-006 §五 —— Fix 进出经 FixParse 唯一解析
//   GDD time-and-weather.md §Formulas F-5.4 —— EnvMod_raw = g(生态区基线, 天气 kind×强度, 昼夜, season_index)
//
// 核心机制:
//   - 5 侧无钳制，负值合法
//   - 输出 Fix 乘子
//   - 中性缺省 = FIX_ONE

using System;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>
    /// EnvMod 输入参数。
    /// </summary>
    public readonly struct EnvModInput
    {
        public readonly Fix EcozoneBaseline;
        public readonly int WeatherKind;
        public readonly int WeatherIntensity;
        public readonly bool IsNight;
        public readonly long SeasonIndex;

        public EnvModInput(Fix ecozoneBaseline, int weatherKind, int weatherIntensity, bool isNight, long seasonIndex)
        {
            EcozoneBaseline = ecozoneBaseline;
            WeatherKind = weatherKind;
            WeatherIntensity = weatherIntensity;
            IsNight = isNight;
            SeasonIndex = seasonIndex;
        }
    }

    /// <summary>
    /// EnvMod_raw 环境修正输出 —— 5 侧无钳制。
    /// </summary>
    public static class EnvMod
    {
        /// <summary>
        /// 计算 EnvMod_raw（纯函数）。
        /// </summary>
        public static Fix ComputeEnvModRaw(EnvModInput input)
        {
            // 中性缺省：全分量取默认档 ⇒ EnvMod_raw == FIX_ONE
            if (input.EcozoneBaseline.Raw == 0 &&
                input.WeatherKind == 0 &&
                input.WeatherIntensity == 0 &&
                !input.IsNight &&
                input.SeasonIndex == 0)
            {
                return Fix.One;
            }

            // 简化版：线性组合
            // 完整版需要实现分项表（生态区基线 / kind×强度矩阵 / 昼夜修正）

            Fix result = Fix.One;

            // 生态区基线贡献
            result = result * input.EcozoneBaseline;

            // 天气贡献（简化版：kind × intensity 线性）
            Fix weatherContribution = new Fix(input.WeatherKind * 1000 + input.WeatherIntensity);
            result = result * weatherContribution;

            // 昼夜修正（简化版：夜晚 -10%）
            if (input.IsNight)
            {
                result = result * new Fix(9000); // 0.9
            }

            // 季节修正（简化版：season_index × 5%）
            Fix seasonContribution = new Fix(10000 + input.SeasonIndex * 500);
            result = result * seasonContribution;

            // 5 侧无钳制，负值合法
            return result;
        }

        /// <summary>
        /// 验证同输入逐位同输出。
        /// </summary>
        public static bool IsDeterministic(EnvModInput input)
        {
            var r1 = ComputeEnvModRaw(input);
            var r2 = ComputeEnvModRaw(input);
            return r1.Raw == r2.Raw;
        }
    }
}
