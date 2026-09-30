// ADR-005 / GDD time-and-weather.md —— 时间基准与相位推导。
//
// 权威来源:
//   ADR-005 §Key Interfaces —— tick 唯一来源 = ITickProvider
//   GDD time-and-weather.md §Detailed Rules F-5.1 昼夜相位 · F-5.2 季节索引
//
// 核心机制:
//   - phase = FMod(tick, TICKS_PER_DAY)
//   - isNight = FMod(phase - NIGHT_START, TICKS_PER_DAY) < NIGHT_SPAN
//   - season_index = FMod(FDiv(tick, TICKS_PER_SEASON), SEASONS_PER_YEAR)
//   - FMod(a, n) = a - n * floor(a / n) ∈ [0, n)

using System;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>
    /// 时间参数（从烘焙数据装载）。
    /// </summary>
    public sealed class TimeParams
    {
        public long TICKS_PER_DAY;
        public long TICKS_PER_SEASON;
        public long SEASONS_PER_YEAR;
        public long NIGHT_START;
        public long NIGHT_SPAN;

        public void Validate()
        {
            if (TICKS_PER_DAY <= 0)
                throw new ArgumentException("TICKS_PER_DAY 必须 > 0", nameof(TICKS_PER_DAY));
            if (TICKS_PER_SEASON <= 0)
                throw new ArgumentException("TICKS_PER_SEASON 必须 > 0", nameof(TICKS_PER_SEASON));
            if (SEASONS_PER_YEAR < 2)
                throw new ArgumentException("SEASONS_PER_YEAR 必须 >= 2", nameof(SEASONS_PER_YEAR));
            if (NIGHT_SPAN <= 0 || NIGHT_SPAN >= TICKS_PER_DAY)
                throw new ArgumentException("NIGHT_SPAN 必须 ∈ (0, TICKS_PER_DAY)", nameof(NIGHT_SPAN));
        }
    }

    /// <summary>
    /// 时间基准 —— 昼夜 / 季节相位推导。
    /// </summary>
    public static class TimeBase
    {
        /// <summary>
        /// FMod —— 环绕安全取模，结果 ∈ [0, n)。
        /// </summary>
        public static long FMod(long a, long n)
        {
            if (n <= 0)
                throw new ArgumentException("n 必须 > 0", nameof(n));

            long r = a % n;
            return r < 0 ? r + n : r;
        }

        /// <summary>
        /// FDiv —— 整除（向零截断）。
        /// </summary>
        public static long FDiv(long a, long b)
        {
            if (b == 0)
                throw new ArgumentException("b 不能为 0", nameof(b));
            return a / b;
        }

        /// <summary>
        /// 计算昼夜相位。
        /// </summary>
        public static long ComputePhase(long tick, TimeParams p)
        {
            return FMod(tick, p.TICKS_PER_DAY);
        }

        /// <summary>
        /// 判断是否夜晚。
        /// </summary>
        public static bool IsNight(long tick, TimeParams p)
        {
            long phase = ComputePhase(tick, p);
            return FMod(phase - p.NIGHT_START, p.TICKS_PER_DAY) < p.NIGHT_SPAN;
        }

        /// <summary>
        /// 计算季节索引。
        /// </summary>
        public static long ComputeSeasonIndex(long tick, TimeParams p)
        {
            return FMod(FDiv(tick, p.TICKS_PER_SEASON), p.SEASONS_PER_YEAR);
        }

        /// <summary>
        /// 计算完整时间状态。
        /// </summary>
        public static TimeState ComputeState(long tick, TimeParams p)
        {
            return new TimeState(
                tick,
                ComputePhase(tick, p),
                IsNight(tick, p),
                ComputeSeasonIndex(tick, p));
        }
    }

    /// <summary>
    /// 时间状态快照。
    /// </summary>
    public readonly struct TimeState
    {
        public readonly long Tick;
        public readonly long Phase;
        public readonly bool IsNight;
        public readonly long SeasonIndex;

        public TimeState(long tick, long phase, bool isNight, long seasonIndex)
        {
            Tick = tick;
            Phase = phase;
            IsNight = isNight;
            SeasonIndex = seasonIndex;
        }
    }
}
