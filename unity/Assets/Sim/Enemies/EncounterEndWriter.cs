// 遭遇结束写者(27)—— `EncounterEnded` 的唯一生产写入通道。
//
// 权威:
//   `design/registry/entities.yaml` —— EncounterEnded:stream = world · author = 27
//   GDD enemy-ai.md 规则二十(`PR-27-4`:Started = 52 / Ended = **27**)
//   GDD enemy-ai.md 规则二十一(Ended ≠ 实体消失:倒地敌人仍可被救治 / 取材)
//   ADR-021 §三(StateChanged 命名先例)· ADR-005(主机唯一 Append)
//
// ⚠️ **一支遭遇至多一条 End**:去重按 `encounter_id`(`remaining` 的「脱离后再全倒地」
//   不再单写第二条 —— GDD「已结束即终态」)。
// ⚠️ 载荷 `reason` ∈ {0 脱离, 1 全倒地, 2 超时};越界 ⇒ 拒(`RejectInvalidArgument`)。
//   「Ended ≠ 实体消失」:本写者**不**发 `DropSpawned` / 不删实体 —— 那是下游各自的事。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>遭遇结束原因(与 `EncounterEndReason` 同值,写者侧再钉一次闭集)。</summary>
    public enum EncounterEndReasonKind
    {
        /// <summary>脱离。</summary>
        Disengaged = 0,
        /// <summary>全部成员倒地。</summary>
        AllDowned = 1,
        /// <summary>超时(`ENCOUNTER_TIMEOUT`)。</summary>
        Timeout = 2,
    }

    /// <summary><see cref="EncounterEndWriter.TryEnd"/> 的结果码。</summary>
    public enum EncounterEndResult
    {
        /// <summary>已写入一条 `EncounterEnded`。</summary>
        Applied = 0,
        /// <summary>该遭遇已结束过 ⇒ 零写入。</summary>
        AlreadyEnded = 1,
        /// <summary>入参非法 ⇒ 零写入。</summary>
        RejectInvalidArgument = 2,
    }

    /// <summary>遭遇结束写者(27)。</summary>
    public sealed class EncounterEndWriter
    {
        private readonly IEventSink _sink;
        private readonly IPayloadEncoder _encoder;
        private readonly Func<IReadOnlyList<SimEvent>> _eventsOf;
        private readonly IPayloadDecoder _decoder;

        /// <param name="sink">事件写入通道(主机唯一)。</param>
        /// <param name="encoder">载荷编码器。</param>
        /// <param name="eventsOf">事件流只读视图(去重判据)。</param>
        /// <param name="decoder">载荷解码器。</param>
        /// <exception cref="ArgumentNullException">任一依赖为 null。</exception>
        public EncounterEndWriter(IEventSink sink, IPayloadEncoder encoder,
                                  Func<IReadOnlyList<SimEvent>> eventsOf, IPayloadDecoder decoder)
        {
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
            _encoder = encoder ?? throw new ArgumentNullException(nameof(encoder));
            _eventsOf = eventsOf ?? throw new ArgumentNullException(nameof(eventsOf));
            _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
        }

        /// <summary>写一条遭遇结束(同遭遇已结束 ⇒ no-op)。</summary>
        /// <param name="encounterId">遭遇 id(≥ 0)。</param>
        /// <param name="reason">结束原因(0 / 1 / 2)。</param>
        /// <param name="tick">当前逻辑 tick(≥ 0)。</param>
        public EncounterEndResult TryEnd(int encounterId, EncounterEndReasonKind reason, long tick)
        {
            if (encounterId < 0 || tick < 0) return EncounterEndResult.RejectInvalidArgument;
            int r = (int)reason;
            if (r < 0 || r > 2) return EncounterEndResult.RejectInvalidArgument;
            if (Exists(encounterId)) return EncounterEndResult.AlreadyEnded;

            var payload = new EncounterEndedPayload(encounterId, r);
            _sink.Append(new SimEvent(tick, PatientId.None, -1 /* 未发号哨兵 O-1 */, EventKind.EncounterEnded,
                                      _encoder.Encode(EventKind.EncounterEnded, in payload)));
            return EncounterEndResult.Applied;
        }

        private bool Exists(int encounterId)
        {
            IReadOnlyList<SimEvent> events = _eventsOf();
            if (events == null) return false;
            for (int i = 0; i < events.Count; i++)
            {
                SimEvent e = events[i];
                if (e.Kind != EventKind.EncounterEnded) continue;
                EncounterEndedPayload p = _decoder.Decode<EncounterEndedPayload>(e.Kind, e.Payload);
                if (p.EncounterId == encounterId) return true;
            }
            return false;
        }
    }
}
