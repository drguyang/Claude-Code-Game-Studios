// 遭遇开启写者(52)—— `EncounterStarted` 的唯一生产写入通道。
//
// 权威:
//   `design/registry/entities.yaml` —— EncounterStarted:stream = world · author = 52
//   GDD enemy-ai.md 规则二十(两 Kind 写者分工)· `PR-27-4`(Started = 52 / Ended = 27)
//   ADR-016 §二(id 经 `IIdAuthority` 与病人共空间)· ADR-005(主机唯一 Append)
//   ADR-029 §③(载荷唯一编码路径 = `IPayloadEncoder`)
//
// ⚠️ **Patient 头 = 本次遭遇全部成员 id 的**最大值**:
//   `GetNextPatientId` 的高水位只扫 `SimEvent.Patient`(ADR-006 Amendment B),
//   而一支遭遇带 N 个 actor、事件头只有一个 Patient 位。取 max 保证「读档后发号器
//   不低于这批敌人的任何一个 id」—— 取第一个 actor 会让后面的成员号在读档后**被重发**。
//   (2026-11-14 首个敌人写者轮的口径,登记在 ADR-016 §二 的落地注。)
//
// ⚠️ **去重按 `encounter_id`**:同一遭遇重复 Start = no-op(读档重放 / 驱动重试都走这条)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary><see cref="EncounterStartWriter.TryStart"/> 的结果码(零写入分支亦具名)。</summary>
    public enum EncounterStartResult
    {
        /// <summary>已写入一条 `EncounterStarted`。</summary>
        Applied = 0,
        /// <summary>该 `encounter_id` 已存在于流中 ⇒ 零写入。</summary>
        AlreadyStarted = 1,
        /// <summary>入参非法(id 为负 / 成员为空 / tick 为负)⇒ 零写入。</summary>
        RejectInvalidArgument = 2,
    }

    /// <summary>遭遇开启写者(52)。</summary>
    public sealed class EncounterStartWriter
    {
        private readonly IEventSink _sink;
        private readonly IPayloadEncoder _encoder;
        private readonly Func<IReadOnlyList<SimEvent>> _eventsOf;
        private readonly IPayloadDecoder _decoder;

        /// <param name="sink">事件写入通道(主机唯一)。</param>
        /// <param name="encoder">载荷编码器(ADR-029 §③)。</param>
        /// <param name="eventsOf">事件流只读视图(去重判据)。</param>
        /// <param name="decoder">载荷解码器(读已写过的遭遇 id)。</param>
        /// <exception cref="ArgumentNullException">任一依赖为 null。</exception>
        public EncounterStartWriter(IEventSink sink, IPayloadEncoder encoder,
                                    Func<IReadOnlyList<SimEvent>> eventsOf, IPayloadDecoder decoder)
        {
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
            _encoder = encoder ?? throw new ArgumentNullException(nameof(encoder));
            _eventsOf = eventsOf ?? throw new ArgumentNullException(nameof(eventsOf));
            _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
        }

        /// <summary>写一条遭遇开启(同 `encounter_id` 已存在 ⇒ no-op)。</summary>
        /// <param name="encounterId">遭遇 id(≥ 0)。</param>
        /// <param name="protoId">原型 id(≥ 0;0 = 兵痞 / 1 = 野狗)。</param>
        /// <param name="spawnCell">出生格(全体成员共用)。</param>
        /// <param name="actorIds">成员 id(**非空**,全部 ≥ 0;由 `IIdAuthority` 铸造)。</param>
        /// <param name="tick">当前逻辑 tick(≥ 0)。</param>
        public EncounterStartResult TryStart(int encounterId, int protoId, WorldPos spawnCell,
                                             IReadOnlyList<int> actorIds, long tick)
        {
            if (encounterId < 0 || protoId < 0 || tick < 0
                || actorIds == null || actorIds.Count == 0)
                return EncounterStartResult.RejectInvalidArgument;
            for (int i = 0; i < actorIds.Count; i++)
                if (actorIds[i] < 0) return EncounterStartResult.RejectInvalidArgument;

            if (Exists(encounterId)) return EncounterStartResult.AlreadyStarted;

            int maxActor = actorIds[0];
            for (int i = 1; i < actorIds.Count; i++)
                if (actorIds[i] > maxActor) maxActor = actorIds[i];

            var copy = new int[actorIds.Count];
            for (int i = 0; i < actorIds.Count; i++) copy[i] = actorIds[i];

            var payload = new EncounterStartedPayload(encounterId, protoId, spawnCell, copy);
            _sink.Append(new SimEvent(tick, new PatientId(maxActor), -1 /* 未发号哨兵 O-1 */,
                                      EventKind.EncounterStarted,
                                      _encoder.Encode(EventKind.EncounterStarted, in payload)));
            return EncounterStartResult.Applied;
        }

        private bool Exists(int encounterId)
        {
            IReadOnlyList<SimEvent> events = _eventsOf();
            if (events == null) return false;
            for (int i = 0; i < events.Count; i++)
            {
                SimEvent e = events[i];
                if (e.Kind != EventKind.EncounterStarted) continue;
                EncounterStartedPayload p = _decoder.Decode<EncounterStartedPayload>(e.Kind, e.Payload);
                if (p.EncounterId == encounterId) return true;
            }
            return false;
        }
    }
}
