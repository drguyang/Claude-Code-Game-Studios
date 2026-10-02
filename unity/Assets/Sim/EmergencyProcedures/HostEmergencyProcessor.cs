// emergency-procedures Story 004 — 主机侧管线
//
// 权威来源:
//   GDD emergency-procedures.md 规则十一: Subscribe(EmergencyAttempt) → Judge → Append × 2 → Seq 发号
//   ADR-005: 主机唯一 Append
//   ADR-006: Seq 发号

using System;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.EmergencyProcedures
{
    /// <summary>
    /// 主机侧急救处理器（规则十一）。
    /// 订阅 EmergencyAttempt → Judge → Append(EmergencyAttempt) + Append(EmergencyTreatmentApplied) → Seq 发号
    /// </summary>
    public sealed class HostEmergencyProcessor
    {
        private readonly IEventSink _eventSink;
        private readonly IIdAuthority _idAuthority;

        public HostEmergencyProcessor(IEventSink eventSink, IIdAuthority idAuthority)
        {
            _eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
            _idAuthority = idAuthority ?? throw new ArgumentNullException(nameof(idAuthority));
        }

        /// <summary>
        /// 处理 EmergencyAttempt（主机唯一入口）。
        /// </summary>
        public void Process(EmergencyAttemptPayload attempt, EmergencyActionRow action, JudgeContext ctx, long tick)
        {
            // 构造 EmergencyReading 供 Judge 使用
            var reading = new EmergencyReading(
                attempt.Action,
                attempt.HoldTicks,
                attempt.Edges,
                attempt.EdgeTicks,
                attempt.MagPeak);

            // Judge
            var result = JudgeEvaluator.Judge(reading, action, ctx);

            // Append EmergencyAttempt（物化）
            var attemptEvent = new SimEvent(
                tick,
                PatientId.None,
                0, // Seq 由发号器给出
                EventKind.EmergencyAttempt,
                new PayloadRef(attempt.Action, attempt.HoldTicks, attempt.Edges));
            _eventSink.Append(attemptEvent);

            // Append EmergencyTreatmentApplied
            var appliedEvent = new SimEvent(
                tick,
                PatientId.None,
                0,
                EventKind.EmergencyTreatmentApplied,
                new PayloadRef(attempt.Action, (int)result, 0));
            _eventSink.Append(appliedEvent);
        }
    }
}
