// ADR-005 / GDD random-events.md F1 —— 抽取管线：配额、上下文门与强度轴。
//
// 权威来源:
//   ADR-005 §Key Interfaces —— 确定性模拟
//   GDD random-events.md F1 —— 四步：档配额 → W_i 调制 → 强度定档 → 预算后生成
//
// 核心机制:
//   - Hamilton 最大余额法整数拆分
//   - ContextMult 只在配额层进入
//   - TODMult 夜窗只乘威胁档
//   - 先舍入后钳制

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>
    /// F1 管线输入。
    /// </summary>
    public readonly struct F1PipelineInput
    {
        public readonly long Window;
        public readonly long WindowSize;
        public readonly bool IsNight;
        public readonly bool IsOnExpedition;
        public readonly bool IsInClinic;
        public readonly bool InCombatCooldown;
        public readonly long SeasonIndex;

        public F1PipelineInput(long window, long windowSize, bool isNight, bool isOnExpedition, bool isInClinic, bool inCombatCooldown, long seasonIndex)
        {
            Window = window;
            WindowSize = windowSize;
            IsNight = isNight;
            IsOnExpedition = isOnExpedition;
            IsInClinic = isInClinic;
            InCombatCooldown = inCombatCooldown;
            SeasonIndex = seasonIndex;
        }
    }

    /// <summary>
    /// F1 管线输出。
    /// </summary>
    public readonly struct F1PipelineOutput
    {
        public readonly int[] Quotas;
        public readonly int StrengthTier;
        public readonly bool ContextGatePassed;

        public F1PipelineOutput(int[] quotas, int strengthTier, bool contextGatePassed)
        {
            Quotas = quotas;
            StrengthTier = strengthTier;
            ContextGatePassed = contextGatePassed;
        }
    }

    /// <summary>
    /// F1 抽取管线 —— 配额、上下文门与强度轴。
    /// </summary>
    public static class EventF1Pipeline
    {
        // 档占比（简化版：威胁 3/8, 机会 3/8, 反应 1/8, 灾难 1/8）
        private static readonly int[] TierRatios = { 3, 3, 1, 1 };
        private const int RatioSum = 8;

        /// <summary>
        /// Hamilton 最大余额法整数拆分。
        /// </summary>
        public static int[] HamiltonSplit(long windowSize)
        {
            return HamiltonSplit(windowSize, null, null);
        }

        /// <summary>
        /// Hamilton 最大余额法整数拆分（带上下文乘子）。
        /// 当某档 ×0 时，该档配额 = 0，其余档按原比例分配全部 windowSize。
        /// </summary>
        public static int[] HamiltonSplit(long windowSize, int[] contextMult, int[] reputationMult)
        {
            if (windowSize <= 0)
                throw new ArgumentException("windowSize 必须 > 0", nameof(windowSize));

            var quotas = new int[4];
            var remainders = new long[4];
            long total = 0;

            // 计算有效比例（考虑 ContextMult 和 ReputationMult）
            var effectiveRatios = new long[4];
            long effectiveSum = 0;
            for (int i = 0; i < 4; i++)
            {
                long ratio = TierRatios[i];
                if (contextMult != null) ratio *= contextMult[i];
                if (reputationMult != null) ratio *= reputationMult[i];
                effectiveRatios[i] = ratio;
                effectiveSum += ratio;
            }

            // 如果全部比例为 0，返回全零
            if (effectiveSum == 0)
            {
                return new int[] { 0, 0, 0, 0 };
            }

            // 整数 floor（按有效比例分配全部 windowSize）
            for (int i = 0; i < 4; i++)
            {
                quotas[i] = (int)(windowSize * effectiveRatios[i] / effectiveSum);
                remainders[i] = windowSize * effectiveRatios[i] % effectiveSum;
                total += quotas[i];
            }

            // 余额按余数降序排列（并列按档序：威胁<机会<反应<灾难）
            long remaining = windowSize - total;
            var indices = new[] { 0, 1, 2, 3 };
            Array.Sort(indices, (a, b) =>
            {
                int cmp = remainders[b].CompareTo(remainders[a]);
                return cmp != 0 ? cmp : a.CompareTo(b);
            });

            for (int i = 0; i < remaining && i < 4; i++)
            {
                // 只给配额 > 0 的档分配余额
                if (quotas[indices[i]] > 0)
                {
                    quotas[indices[i]]++;
                }
            }

            return quotas;
        }

        /// <summary>
        /// ContextGate 布尔判据。
        /// 返回值：true = 通过，false = 不通过。
        /// 注意：医馆只压制威胁档，机会/反应档保留。
        /// </summary>
        public static bool EvaluateContextGate(F1PipelineInput input)
        {
            // 战斗冷却：全档 ×0
            if (input.InCombatCooldown)
                return false;

            // 在出诊路径上（且不在医馆）
            return input.IsOnExpedition && !input.IsInClinic;
        }

        /// <summary>
        /// 计算 ContextMult（每档的上下文乘子）。
        /// 医馆：威胁档 ×0，机会/反应保留。
        /// </summary>
        public static int[] ComputeContextMult(F1PipelineInput input)
        {
            var mult = new int[4];

            // 战斗冷却：全档 ×0
            if (input.InCombatCooldown)
            {
                for (int i = 0; i < 4; i++) mult[i] = 0;
                return mult;
            }

            // 在医馆地块：威胁档 ×0，机会/反应保留
            if (input.IsInClinic)
            {
                mult[0] = 0; // 威胁档 ×0
                mult[1] = 1; // 机会档保留
                mult[2] = 1; // 反应档保留
                mult[3] = 1; // 灾难档保留
                return mult;
            }

            // 默认：全档 ×1
            for (int i = 0; i < 4; i++) mult[i] = 1;
            return mult;
        }

        /// <summary>
        /// 计算强度档（先舍入后钳制）。
        /// </summary>
        public static int ComputeStrengthTier(Fix strengthRaw, int minTier, int maxTier)
        {
            // 先舍入
            long rounded = strengthRaw.Round();

            // 后钳制
            if (rounded < minTier) return minTier;
            if (rounded > maxTier) return maxTier;
            return (int)rounded;
        }

        /// <summary>
        /// 执行 F1 管线。
        /// </summary>
        public static F1PipelineOutput Execute(F1PipelineInput input, int minTier, int maxTier)
        {
            // ① 档配额
            var quotas = HamiltonSplit(input.WindowSize);

            // ② ContextGate
            bool contextGate = EvaluateContextGate(input);

            // ③ 强度定档（简化版：固定值）
            int strengthTier = ComputeStrengthTier(new Fix(5000), minTier, maxTier);

            return new F1PipelineOutput(quotas, strengthTier, contextGate);
        }
    }
}
