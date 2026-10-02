// emergency-procedures Story 004 / 007 — 主机侧管线
//
// 权威来源:
//   GDD emergency-procedures.md 规则十一: Subscribe(EmergencyAttempt) → Judge → Append × 2 → Seq 发号
//   GDD 规则五(:195-224): EmergencyTreatmentApplied 九字段载荷定义
//   GDD F-10.4(:604): drug_potency = RoundFix(BASE_POTENCY × ResultMul) / MUL_ONE(单一舍入)
//   ADR-005: 主机唯一 Append
//   ADR-006: Seq 发号
//   ADR-029 §③: 载荷编码唯一路径 = IPayloadEncoder(零手搓 PayloadRef)
//
// ⚠️ 2026-10-03(Story 007)修复两处手搓载荷(由 b6 门首次查出,前两轮评审与对账件均未登记):
//   :50  new PayloadRef(attempt.Action, attempt.HoldTicks, attempt.Edges)   —— 8 字段只写 3 个
//   :59  new PayloadRef(attempt.Action, (int)result, 0)                     —— 9 字段只写 1 个有意义
//        且把 `attempt.Action` 当 applied 首字段(`TreatmentId ≠ Action`)、`result` 当施予者 id。
//   ⇒ 现全部经 IPayloadEncoder,九字段/八字段逐项填充。

using System;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.EmergencyProcedures
{
    /// <summary>
    /// 主机侧急救处理器（规则十一）。
    /// 订阅 EmergencyAttempt → Judge → Append(EmergencyAttempt) + Append(EmergencyTreatmentApplied)
    /// </summary>
    /// <remarks>
    /// <para><b>`Seq` 的现状(Story 007 裁定 A=丙,如实登记)</b>:两处载荷的 `Seq` 字段
    /// **置 0 占位** —— 其真源是「主机在 `Append` 时发号」(GDD 规则五),
    /// 而发号发生在 <see cref="IEventSink.Append"/> 内部(`SimEvent.Seq`),
    /// **载荷构造在其之前** ⇒ 本处理器拿不到该值。
    /// 载荷 `Seq` 与 header `Seq` 的语义关系须由**上行链**落地时裁定(45 / P1b 范围)。
    /// ⚠️ **本处置不构成「九字段齐备」的借绿** —— 该字段在 AC-10-39 的判据面内被显式标注为占位。</para>
    /// </remarks>
    public sealed class HostEmergencyProcessor
    {
        private readonly IEventSink _eventSink;
        private readonly IIdAuthority _idAuthority;
        private readonly IPayloadEncoder _encoder;

        /// <param name="eventSink">事件写入通道(主机唯一)。</param>
        /// <param name="idAuthority">发号权威。</param>
        /// <param name="encoder">
        /// 载荷编码器(ADR-029 §③)—— **唯一合法的载荷构造路径**。
        /// 本类内不得出现 <c>new PayloadRef(</c>(ADR-029 §③ 的门判据,由 b6 门强制)。
        /// </param>
        public HostEmergencyProcessor(IEventSink eventSink, IIdAuthority idAuthority,
                                      IPayloadEncoder encoder)
        {
            _eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
            _idAuthority = idAuthority ?? throw new ArgumentNullException(nameof(idAuthority));
            _encoder = encoder ?? throw new ArgumentNullException(nameof(encoder));
        }

        /// <summary>
        /// 处理 EmergencyAttempt（主机唯一入口）。
        /// </summary>
        /// <param name="attempt">判定输入载荷(八字段,含 `Method` / `ActorId`)。</param>
        /// <param name="action">动作表行(含 `Polarity` / `BasePotency` / `HalfLifeTicks` / `ResultMul`)。</param>
        /// <param name="ctx">判定上下文。</param>
        /// <param name="tick">当前 tick。</param>
        /// <param name="cause">
        /// 处置原因枚举(GDD `:467-469` 的判据:设备无模拟量通道 ⇒ **降级**;
        /// 玩家在设备具备时主动选跳过 ⇒ **玩家选择**)。
        /// ⚠️ 该判据的信息源在**输入层(3)**,本处理器收不到 ⇒ 由调用方传入。
        /// **真实调用方 = 主机上行链,归 45 / P1b;当前仅测试调用。**
        /// </param>
        public void Process(EmergencyAttemptPayload attempt, EmergencyActionRow action,
                            JudgeContext ctx, long tick, int cause)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            // 构造 EmergencyReading 供 Judge 使用
            var reading = new EmergencyReading(
                attempt.Action,
                attempt.HoldTicks,
                attempt.Edges,
                attempt.EdgeTicks,
                attempt.MagPeak);

            // Judge
            var result = JudgeEvaluator.Judge(reading, action, ctx);

            // ── Append ① EmergencyAttempt(物化;八字段全载)────────────────
            var attemptEvent = new SimEvent(
                tick,
                PatientId.None,
                0, // Seq 由发号器给出(header)
                EventKind.EmergencyAttempt,
                _encoder.Encode(EventKind.EmergencyAttempt, attempt));
            _eventSink.Append(attemptEvent);

            // ── Append ② EmergencyTreatmentApplied(九字段全载)─────────────
            // F-10.4: drug_potency = RoundFix(BASE_POTENCY × ResultMul[result]) / MUL_ONE
            //   中间积落 Q32.32,全程**只做一次舍入**(ROUND_HALF_AWAY_FROM_ZERO)
            //   —— 承首轮评审 R-2/A8:`ScaleFixed` 即该实现,禁 `a * b / c` 朴素写法。
            long drugPotency = JudgeEvaluator.ScaleFixed(
                action.BasePotency,
                action.ResultMul[(int)result]);

            var applied = new EmergencyTreatmentAppliedPayload(
                tick:          tick,
                treatmentId:   action.ActionId,      // 处置_id = EmergencyAction 枚举
                actorId:       attempt.ActorId,      // 施予者(不是 JudgeResult!)
                polarity:      action.Polarity,      // 处置词表查得(不是恒 0)
                drugPotency:   new Fix(drugPotency), // F-10.4 单一舍入
                halfLife:      action.HalfLifeTicks, // 动作数据表(不是恒 0 ⇒ 不触发 9 的 Decay 除零)
                method:        attempt.Method,       // Manual / Skip —— 使跳过在流上可判别
                cause:         cause,                // 玩家选择 / 降级
                seq:           0);                   // ⚠️ 占位,见类头注

            var appliedEvent = new SimEvent(
                tick,
                PatientId.None,
                0,
                EventKind.EmergencyTreatmentApplied,
                _encoder.Encode(EventKind.EmergencyTreatmentApplied, applied));
            _eventSink.Append(appliedEvent);
        }
    }
}
