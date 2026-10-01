// ADR-005 / GDD time-and-weather.md —— 时间基准与相位推导。
//
// 权威来源:
//   ADR-005 —— tick 唯一来源 = ITickProvider,5 只读 CurrentTick
//   GDD time-and-weather.md —— 昼夜相位 / 季节索引 / 天气纯函数
//
// 核心机制:
//   - TICKS_PER_DAY 单一定义点
//   - FMod 环绕安全取模(禁 C# % 负操作数)
//   - 天气 = (WorldSeed, block(tick), EcozoneOf(cell)) 纯函数

using System;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.World
{
    /// <summary>
    /// 时间基准 —— tick 只读与昼夜 / 季节相位推导。
    /// </summary>
    public static class TimeBase
    {
        public const long TICKS_PER_DAY = 480L; // 20 Hz × 24 h = 4800,但 GDD 裁定 480(见下)
        public const long NIGHT_START = 360L;  // 18:00(360 / 480 × 24h = 18h)
        public const long NIGHT_SPAN = 240L;   // 12 h 夜晚

        /// <summary>
        /// 环绕安全取模 —— 结果 ∈ [0, n)。
        /// </summary>
        public static long FMod(long a, long n)
        {
            if (n <= 0) throw new ArgumentException("n 必须 > 0", nameof(n));
            long r = a % n;
            return r < 0 ? r + n : r;
        }

        /// <summary>
        /// 计算昼夜相位。
        /// </summary>
        public static long ComputePhase(long tick)
        {
            return FMod(tick, TICKS_PER_DAY);
        }

        /// <summary>
        /// 判断是否夜晚。
        /// </summary>
        public static bool IsNight(long tick)
        {
            long phase = ComputePhase(tick);
            return FMod(phase - NIGHT_START, TICKS_PER_DAY) < NIGHT_SPAN;
        }

        /// <summary>
        /// 计算季节索引。
        /// </summary>
        public static long ComputeSeasonIndex(long tick)
        {
            long seasonLength = TICKS_PER_DAY * 30L; // 30 天 / 季节
            return FMod(FDiv(tick, seasonLength), 4L);
        }

        /// <summary>
        /// 整数除法(向零截断)。
        /// </summary>
        public static long FDiv(long a, long b)
        {
            if (b == 0) throw new DivideByZeroException();
            return a / b;
        }

        /// <summary>
        /// 计算完整时间状态(相位 + 是否夜晚 + 季节索引)。
        /// </summary>
        public static TimeState ComputeState(long tick)
        {
            return new TimeState(
                ComputePhase(tick),
                IsNight(tick),
                ComputeSeasonIndex(tick));
        }
    }

    /// <summary>
    /// 时间状态快照。
    /// </summary>
    public readonly struct TimeState
    {
        public readonly long Phase;
        public readonly bool IsNight;
        public readonly long SeasonIndex;

        public TimeState(long phase, bool isNight, long seasonIndex)
        {
            Phase = phase;
            IsNight = isNight;
            SeasonIndex = seasonIndex;
        }
    }
}
