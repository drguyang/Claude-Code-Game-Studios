// emergency-procedures Story 004 — EmergencyAttempt 聚合与上行
//
// 权威来源:
//   ADR-011 Amendment B: 客户端聚合为一条 EmergencyAttempt 全整数意图事件
//   ADR-001 §一之三 裁决一: 判定输入类 → 可靠通道
//   ADR-009 Amendment I: 三新 Kind; 一条动作落两条流事件
//   GDD emergency-procedures.md: F-10.5 聚合

using System;
using System.Linq;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.EmergencyProcedures
{
    /// <summary>
    /// 聚合后的 EmergencyAttempt 载荷（全整数）。
    /// GDD 规则十一⑤: action / hold_ticks / edges / edge_ticks[] / mag_peak + method / cause / provider
    /// </summary>
    public readonly struct EmergencyAttemptPayload
    {
        public readonly int Action;
        public readonly int HoldTicks;
        public readonly int Edges;
        public readonly int[] EdgeTicks;
        public readonly int MagPeak;
        public readonly int Method;    // 0 = Manual, 1 = Skip
        public readonly int Cause;     // 原因标志
        public readonly int Provider;  // 施予者

        public EmergencyAttemptPayload(int action, int holdTicks, int edges, int[] edgeTicks, int magPeak,
            int method = 0, int cause = 0, int provider = 0)
        {
            Action = action;
            HoldTicks = holdTicks;
            Edges = edges;
            EdgeTicks = edgeTicks;
            MagPeak = magPeak;
            Method = method;
            Cause = cause;
            Provider = provider;
        }
    }

    /// <summary>
    /// EmergencyTreatmentApplied 载荷（七项齐备）。
    /// </summary>
    public readonly struct EmergencyTreatmentAppliedPayload
    {
        public readonly int Polarity;
        public readonly long DrugPotency;
        public readonly int HalfLifeTicks;
        public readonly int TreatmentId;
        public readonly int Provider;
        public readonly int Method;
        public readonly int Cause;

        public EmergencyTreatmentAppliedPayload(int polarity, long drugPotency, int halfLifeTicks,
            int treatmentId, int provider, int method, int cause)
        {
            Polarity = polarity;
            DrugPotency = drugPotency;
            HalfLifeTicks = halfLifeTicks;
            TreatmentId = treatmentId;
            Provider = provider;
            Method = method;
            Cause = cause;
        }
    }

    /// <summary>
    /// EmergencyAttempt 聚合器（F-10.5）。
    /// </summary>
    public static class EmergencyAttemptAggregator
    {
        /// <summary>
        /// 聚合读数序列为 EmergencyAttempt 载荷。
        /// </summary>
        public static EmergencyAttemptPayload Aggregate(int[] edgeTicks, int[] magnitudes)
        {
            if (edgeTicks == null || edgeTicks.Length == 0)
                return new EmergencyAttemptPayload(0, 0, 0, new int[0], 0);

            int holdTicks = edgeTicks[edgeTicks.Length - 1] - edgeTicks[0];
            int edges = edgeTicks.Length;
            int magPeak = magnitudes.Max();

            return new EmergencyAttemptPayload(0, holdTicks, edges, edgeTicks, magPeak);
        }
    }
}
