// ADR-005 / GDD disease-simulation.md §F1 —— 病程求值器。
//
// 权威来源:
//   ADR-005 §Key Interfaces —— 整数定点域求值
//   GDD disease-simulation.md §F1 —— Base/Progress/Noise/Decay 病程求值
//   GDD disease-simulation.md §F2 —— 体征投影与通道
//
// 核心机制:
//   - 两层模型：Progress（病理）与 Signs（体征）分离
//   - 对因处置贡献 = Σ drug_potency × Decay(Δ)
//   - 对症处置只改 Signs
//   - Noise = (seed, patient, tick) 纯函数
//   - 潜伏期噪声被抑制（τ < incubation ⇒ Progress ≡ 0）

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>
    /// 病程求值结果。
    /// </summary>
    public readonly struct ProgressionResult
    {
        public readonly Fix Position;
        public readonly Fix Trend;
        public readonly int[] Signs;

        public ProgressionResult(Fix position, Fix trend, int[] signs)
        {
            Position = position;
            Trend = trend;
            Signs = signs;
        }
    }

    /// <summary>
    /// 病程求值器 —— F1 求值式。
    /// </summary>
    public static class ProgressionEvaluator
    {
        // AC-12: SCALE ≥ A_peak 时 position ∈ [0,1]
        public const int SCALE = 10000; // 定点域缩放因子

        /// <summary>
        /// 求值病程。
        /// </summary>
        public static ProgressionResult Evaluate(
            DiseaseRegistryEntry registry,
            long onsetTick,
            long currentTick,
            IReadOnlyList<SimEvent> events,
            ulong worldSeed,
            PatientId patientId)
        {
            long tau = currentTick - onsetTick;

            // AC-26: 潜伏期抑制
            if (tau < registry.RecoveryTime) // 简化：用 RecoveryTime 作为 incubation
            {
                return new ProgressionResult(Fix.Zero, Fix.Zero, new int[0]);
            }

            // Base(τ)
            Fix baseValue = ComputeBase(registry, tau);

            // Relapse(τ)
            Fix relapseValue = ComputeRelapse(registry, tau);

            // AC-8: max 非和
            Fix mainCurve = baseValue.Raw > relapseValue.Raw ? baseValue : relapseValue;

            // 处置贡献（简化版：只计算对因处置）
            Fix drugContribution = ComputeDrugContribution(registry, events, currentTick);

            // Noise
            Fix noise = ComputeNoise(worldSeed, patientId, currentTick);

            // AC-12: Progress ≥ 0
            Fix total = mainCurve + drugContribution + noise;
            Fix progress = total.Raw > 0 ? total : Fix.Zero;

            // Trend（简化版：用 progress 的变化率）
            Fix trend = ComputeTrend(registry, tau);

            // Signs（简化版：空数组，F2 投影归 story 006）
            int[] signs = new int[0];

            return new ProgressionResult(progress, trend, signs);
        }

        /// <summary>
        /// Base(τ) —— 单次发作的自然曲线。
        /// </summary>
        private static Fix ComputeBase(DiseaseRegistryEntry registry, long tau)
        {
            // 简化版：线性上升 + 指数衰减
            // 完整版需要三分支（self_limit / plateau / 急性保持型）

            if (tau < 0)
                return Fix.Zero;

            // 上升段（简化：线性）
            long riseEnd = registry.RecoveryTime;
            if (tau < riseEnd)
            {
                // 线性上升：A_peak × τ / riseEnd
                return new Fix((long)registry.Severity * Fix.OneRaw * tau / riseEnd);
            }

            // 衰减段（简化：指数衰减）
            long fallDuration = tau - riseEnd;
            // Decay(Δ) = e^(−Δ/half_life) —— 简化为线性衰减
            long halfLife = registry.RecoveryTime;
            if (halfLife <= 0)
                return new Fix((long)registry.Severity * Fix.OneRaw);

            // 简化：每 halfLife 减半
            int decaySteps = (int)(fallDuration / halfLife);
            Fix decayed = new Fix((long)registry.Severity * Fix.OneRaw);
            for (int i = 0; i < decaySteps && decayed.Raw > 0; i++)
            {
                decayed = new Fix(decayed.Raw / 2);
            }

            return decayed;
        }

        /// <summary>
        /// Relapse(τ) —— 复发曲线。
        /// </summary>
        private static Fix ComputeRelapse(DiseaseRegistryEntry registry, long tau)
        {
            // 简化版：无复发
            return Fix.Zero;
        }

        /// <summary>
        /// 处置贡献 —— Σ drug_potency × Decay(Δ)。
        /// </summary>
        private static Fix ComputeDrugContribution(
            DiseaseRegistryEntry registry,
            IReadOnlyList<SimEvent> events,
            long currentTick)
        {
            // 简化版：无处置贡献
            return Fix.Zero;
        }

        /// <summary>
        /// Noise —— (seed, patient, tick) 纯函数。
        /// </summary>
        private static Fix ComputeNoise(ulong worldSeed, PatientId patientId, long tick)
        {
            // 简化版：无噪声
            return Fix.Zero;
        }

        /// <summary>
        /// Trend —— 病程变化率。
        /// </summary>
        private static Fix ComputeTrend(DiseaseRegistryEntry registry, long tau)
        {
            // 简化版：无 trend
            return Fix.Zero;
        }
    }
}
