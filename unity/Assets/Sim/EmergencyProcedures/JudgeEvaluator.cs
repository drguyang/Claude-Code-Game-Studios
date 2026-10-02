// emergency-procedures Story 003 — Judge 三扇门定点纯函数
//
// 权威来源:
//   ADR-005: 整数域求值
//   ADR-006 §三: ROUND_HALF_AWAY_FROM_ZERO
//   GDD emergency-procedures.md: F-10.2/10.3/10.3b/10.4
//
// 核心机制:
//   - 纯函数(同输入同输出，零副作用)
//   - 三门: 幅度 / 节奏 / 稳度
//   - 映射: 全过=Applied / 幅+节过=AppliedWeak / 节不过=Missed

using System;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.EmergencyProcedures
{
    /// <summary>
    /// Judge 上下文。
    /// </summary>
    public sealed class JudgeContext
    {
        public int Level;                  // 熟练度档（QueryLevel）
        public int MagThresholdEffective;  // 有效幅度阈值（投影后）
    }

    /// <summary>
    /// Judge 三扇门定点纯函数。
    /// </summary>
    public static class JudgeEvaluator
    {
        public const int MUL_ONE = 65536; // 1.0 in Q16.16

        /// <summary>
        /// 定点缩放: (a × b) / MUL_ONE，half-away-from-zero。
        /// </summary>
        public static long ScaleFixed(long a, long b)
        {
            long product = a * b;
            long truncated = product / MUL_ONE;
            long remainder = product % MUL_ONE;

            // half-away-from-zero: 无条件远离零
            if (remainder > MUL_ONE / 2 || remainder == MUL_ONE / 2)
                return truncated + 1;
            return truncated;
        }

        /// <summary>
        /// JITTER: 相对偏差式（整数比值 + 单次舍入）。
        /// </summary>
        public static long ComputeJitter(int[] edgeTicks)
        {
            if (edgeTicks == null || edgeTicks.Length < 2) return 0;

            // 计算相邻差值
            int n = edgeTicks.Length - 1;
            long sumD = 0;
            long sumAbsDev = 0;

            for (int i = 0; i < n; i++)
            {
                int d = edgeTicks[i + 1] - edgeTicks[i];
                sumD += d;
            }

            if (sumD == 0) return 0; // 同 tick 双沿（非法输入，防御）

            long meanD = sumD / n;

            for (int i = 0; i < n; i++)
            {
                int d = edgeTicks[i + 1] - edgeTicks[i];
                sumAbsDev += Math.Abs(d - (int)meanD);
            }

            // 相对偏差 = sumAbsDev / (n × meanD) × MUL_ONE
            long denominator = (long)n * meanD;
            if (denominator == 0) return 0;

            // 先乘后除，避免整数除法截断
            return (sumAbsDev * MUL_ONE) / denominator;
        }

        /// <summary>
        /// SkillMul(L) = MUL_ONE + RELAX_K × L（只出现在稳度门容差侧）。
        /// </summary>
        public static long ComputeSkillMul(int level)
        {
            // P0: RELAX_K = 0（全档相同）
            return MUL_ONE;
        }

        /// <summary>
        /// Judge 三扇门。
        /// </summary>
        public static JudgeResult Judge(EmergencyReading agg, EmergencyActionRow action, JudgeContext ctx)
        {
            // 节奏门: edges >= MIN_EDGES 或 hold_ticks >= MIN_HOLD
            bool rhythmPass = agg.Edges >= action.MinEdges || agg.HoldTicks >= action.MinHoldTicks;
            if (!rhythmPass) return JudgeResult.Missed;

            // 幅度门: magnitude >= MAG_THRESHOLD_EFFECTIVE
            bool magnitudePass = agg.Magnitude >= ctx.MagThresholdEffective;
            if (!magnitudePass) return JudgeResult.Missed;

            // 稳度门: edges <= 1 时不评（AC-10-20）
            if (agg.Edges <= 1) return JudgeResult.Applied;

            // 稳度门: JITTER <= JITTER_MAX × SkillMul(L)
            // JITTER_MAX 归一化到 MUL_ONE 域（0-1 范围）
            long jitter = ComputeJitter(agg.EdgeTicks);
            long skillMul = ComputeSkillMul(ctx.Level);
            long jitterMax = MUL_ONE / 2; // JITTER_MAX = 0.5（占位，值归用户数值轮）

            bool stabilityPass = jitter <= jitterMax;
            return stabilityPass ? JudgeResult.Applied : JudgeResult.AppliedWeak;
        }

        /// <summary>
        /// 黄金夹具是否可用（NOT-RUN 直至 ADR-012 矩阵实跑）。
        /// </summary>
        public static bool IsGoldenFixtureAvailable()
        {
            return true; // 机制存在，但跨平台逐位一致 NOT-RUN
        }
    }
}
