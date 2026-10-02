// emergency-procedures Story 004 / 007 — EmergencyAttempt 聚合与上行
//
// 权威来源:
//   ADR-011 Amendment B: 客户端聚合为一条 EmergencyAttempt 全整数意图事件
//   ADR-001 §一之三 裁决一: 判定输入类 → 可靠通道
//   ADR-009 Amendment I: 三新 Kind; 一条动作落两条流事件
//   GDD emergency-procedures.md: F-10.5 聚合(权威八字段形状)
//   entities.yaml:2253: EmergencyAttempt.payload_schema(八字段真源)
//   ADR-029 §③: 载荷编码唯一路径 = IPayloadEncoder
//
// ⚠️ 2026-10-03(Story 007 结构性收口)删除本文件内两个 payload struct 副本:
//   EmergencyAttemptPayload / EmergencyTreatmentAppliedPayload
//   曾**重复定义**于本文件(Sim.EmergencyProcedures)与
//   Sim.Contracts/Payloads/HistoryPayloads.cs(Sim.Contracts)。**契约版才是权威**
//   (PayloadCodec 按它编码;与 entities.yaml:2253/2269 的 payload_schema 一致)。
//
//   两份**字段集合不同**,非仅类型差异:
//     · attempt:Sim 版 8 字段含自造的 `Cause` / `Provider`,
//       **丢弃了 3 侧已交出的 `MagLast`**,且 `Provider` 与权威 `ActorId` 异名;
//     · applied:Sim 版**仅 7 字段**,缺 `Tick` 与 `Seq`(权威九字段)。
//   ⇒ 已删,统一用契约版(本文件经 using DaYiJingCheng.Sim.Contracts 可见)。
//   删自造字段的依据:entities.yaml:2253 的 payload_schema **不含 `cause`**
//   —— `cause` 属 applied(承 GDD:464「Skipped 须带 cause」在处置事件上下文)。

using System;
using System.Linq;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.EmergencyProcedures
{
    /// <summary>
    /// EmergencyAttempt 聚合器(F-10.5)。
    /// </summary>
    /// <remarks>
    /// ⚠️ **与 3 侧 <c>Gameplay.Input.EmergencyAggregator</c> 的分工**:
    /// 3 侧产出 <c>AggregatedEmergency</c>(六字段:action / hold_ticks / edges /
    /// edge_ticks / mag_peak / **mag_last**),10 在其**边界**映射为
    /// <see cref="EmergencyAttemptPayload"/> 并**补 `Method` / `ActorId`**
    /// (承 `Gameplay.Input/EmergencyAggregator.cs:18-19` 的设计意图)。
    /// ⇒ **`MagLast` 由 3 交出,本聚合器须透传,不得丢弃。**
    /// </remarks>
    public static class EmergencyAttemptAggregator
    {
        /// <summary>
        /// 聚合读数序列为 <see cref="EmergencyAttemptPayload"/>(权威八字段)。
        /// </summary>
        /// <param name="action">动作枚举索引(F-10.5:`action` 不变)。</param>
        /// <param name="edgeTicks">非递减的 press 沿 tick(长度 = `Edges`)。</param>
        /// <param name="magnitudes">逐样本幅度(定点整数)。</param>
        /// <param name="magLast">
        /// 末样本幅度(3 侧交出;GDD F-10.5 `mag_last = magnitude[t_end]`)。
        /// **仅供预表现定格与 48 的回放,不进任何门。**
        /// </param>
        /// <param name="method">结算路径枚举(Manual / Skip)。</param>
        /// <param name="actorId">施予者 id(10 的边界补;3 侧无此量)。</param>
        public static EmergencyAttemptPayload Aggregate(
            int action, int[] edgeTicks, int[] magnitudes,
            int magLast, int method, int actorId)
        {
            if (edgeTicks == null || edgeTicks.Length == 0)
                return new EmergencyAttemptPayload(
                    action, 0, 0, 0, magLast, method, actorId, new int[0]);

            int holdTicks = edgeTicks[edgeTicks.Length - 1] - edgeTicks[0];
            int edges = edgeTicks.Length;
            // F-10.5 附带口径:`max()` 平局取 tick **较小**者;禁依赖数组迭代序。
            int magPeak = Magnitudes.MaxWithEarlierTickTieBreak(edgeTicks, magnitudes);

            return new EmergencyAttemptPayload(
                action, holdTicks, edges, magPeak, magLast, method, actorId, edgeTicks);
        }
    }

    /// <summary>
    /// 幅度序列的 F-10.5 算子(平局口径 = 10 的附带义务)。
    /// </summary>
    internal static class Magnitudes
    {
        /// <summary>
        /// `mag_peak = max(magnitude[t]) over t`;**平局取 tick 较小者**
        /// (GDD F-10.5 附带口径表:「`max()` 的平局口径」归 10;
        /// 禁依赖数组迭代序 —— 承 ADR-008 `PatternRecognized` 同款纪律)。
        /// </summary>
        internal static int MaxWithEarlierTickTieBreak(int[] edgeTicks, int[] magnitudes)
        {
            if (magnitudes == null || magnitudes.Length == 0) return 0;

            int best = magnitudes[0];
            int bestTick = edgeTicks != null && edgeTicks.Length > 0 ? edgeTicks[0] : int.MaxValue;

            for (int i = 1; i < magnitudes.Length; i++)
            {
                int tick = edgeTicks != null && i < edgeTicks.Length ? edgeTicks[i] : int.MaxValue;
                // 严格大于 ⇒ 替换;等于时**只在 tick 更小**才替换(平局取较早沿)
                if (magnitudes[i] > best || (magnitudes[i] == best && tick < bestTick))
                {
                    best = magnitudes[i];
                    bestTick = tick;
                }
            }
            return best;
        }
    }
}
