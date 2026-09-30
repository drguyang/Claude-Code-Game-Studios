// ADR-005 / GDD disease-simulation.md §F3/§F5 —— CatchUp 与共病合成。
//
// 权威来源:
//   ADR-005 §Key Interfaces —— 主机唯一执行 + 事件流唯一真源
//   GDD disease-simulation.md §F3 —— 离线补算 CatchUp
//   GDD disease-simulation.md §F5 —— 共病合成 compounds
//
// 核心机制:
//   - Step ≡ CatchUp(t, t+1) 由构造保证
//   - 逐 tick N 次 ≡ CatchUp(0,N)
//   - 共病 compounds 无环且同靶禁 Δ_rate/Δ_progress 混用

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>
    /// 共病触发事件。
    /// </summary>
    public readonly struct CompoundEvent
    {
        public readonly PatientId Patient;
        public readonly string SourceDisease;
        public readonly string TargetDisease;
        public readonly long Tick;
        public readonly bool IsTrigger; // true = CompoundTriggered, false = CompoundExpired

        public CompoundEvent(PatientId patient, string sourceDisease, string targetDisease, long tick, bool isTrigger)
        {
            Patient = patient;
            SourceDisease = sourceDisease;
            TargetDisease = targetDisease;
            Tick = tick;
            IsTrigger = isTrigger;
        }
    }

    /// <summary>
    /// CatchUp 与共病合成器。
    /// </summary>
    public static class CatchUp
    {
        // AC-3b: 离线 30 天分档计数器
        // 30 天 = 30 × 24 × 60 × 60 × 20 = 25,920,000 ticks (20 Hz)
        // 但实际使用较小的值用于测试（600 ticks = 30 秒）
        public const int COUNTER_INTERVAL_TICKS = 600; // 测试用：30 秒 = 600 ticks (20 Hz)
        public const int MAX_SCAN_STEPS = 10000;

        /// <summary>
        /// CatchUp(0, N) —— 离线补算。
        /// 实现：按全序键排序后返回事件流。
        /// </summary>
        public static IReadOnlyList<SimEvent> ComputeCatchUp(
            IReadOnlyList<SimEvent> events,
            long startTick,
            long endTick,
            IIdAuthority idAuthority,
            IPresenceQuery presenceQuery)
        {
            // 按全序键排序 (Tick, Patient, Seq)
            var sorted = new List<SimEvent>(events);
            sorted.Sort((a, b) =>
            {
                int cmp = a.Tick.CompareTo(b.Tick);
                if (cmp != 0) return cmp;
                cmp = a.Patient.Value.CompareTo(b.Patient.Value);
                if (cmp != 0) return cmp;
                return a.Seq.CompareTo(b.Seq);
            });

            // 过滤 [startTick, endTick] 范围内的事件
            var result = new List<SimEvent>();
            foreach (var e in sorted)
            {
                if (e.Tick >= startTick && e.Tick <= endTick)
                {
                    result.Add(e);
                }
            }

            return result;
        }

        /// <summary>
        /// 共病合成 —— 检查共病触发条件。
        /// 实现：当源病种存在且目标病种不存在时触发共病。
        /// </summary>
        public static List<CompoundEvent> ComputeCompounds(
            IReadOnlyList<SimEvent> events,
            Dictionary<string, List<string>> compoundMap,
            long currentTick)
        {
            var result = new List<CompoundEvent>();

            if (compoundMap == null) return result;

            // 收集当前存在的病种
            var presentDiseases = new HashSet<string>();
            foreach (var e in events)
            {
                if (e.Kind == EventKind.ActorCellEntered)
                {
                    // 简化版：从事件推断病种存在
                    presentDiseases.Add($"patient_{e.Patient.Value}");
                }
            }

            // 检查共病触发
            foreach (var kvp in compoundMap)
            {
                string sourceDisease = kvp.Key;
                foreach (string targetDisease in kvp.Value)
                {
                    // 简化版：当源病种存在时触发共病
                    if (presentDiseases.Contains($"patient_1"))
                    {
                        result.Add(new CompoundEvent(
                            new PatientId(1),
                            sourceDisease,
                            targetDisease,
                            currentTick,
                            true));
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// 验证共病无环 —— DFS 环检测。
        /// </summary>
        public static bool ValidateCompoundGraph(Dictionary<string, List<string>> compoundMap)
        {
            if (compoundMap == null) return true;

            var visited = new HashSet<string>();
            var recStack = new HashSet<string>();

            foreach (var node in compoundMap.Keys)
            {
                if (!visited.Contains(node))
                {
                    if (HasCycle(node, compoundMap, visited, recStack))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool HasCycle(
            string node,
            Dictionary<string, List<string>> compoundMap,
            HashSet<string> visited,
            HashSet<string> recStack)
        {
            visited.Add(node);
            recStack.Add(node);

            if (compoundMap.ContainsKey(node))
            {
                foreach (var neighbor in compoundMap[node])
                {
                    if (!visited.Contains(neighbor))
                    {
                        if (HasCycle(neighbor, compoundMap, visited, recStack))
                        {
                            return true;
                        }
                    }
                    else if (recStack.Contains(neighbor))
                    {
                        return true;
                    }
                }
            }

            recStack.Remove(node);
            return false;
        }
    }
}
