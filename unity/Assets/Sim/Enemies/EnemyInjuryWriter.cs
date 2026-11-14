// 敌人伤情发作写者(25)—— `EnemyInjuryOnset` 的唯一生产写入通道。
//
// 权威:
//   `design/registry/entities.yaml` —— EnemyInjuryOnset:stream = world · author = 25
//   ADR-016 §二(敌人复用 9 的伤情模型;伤情事件落**世界流**)
//   GDD combat-and-weapon-lines.md 规则二 / 三(Kind 与载荷 · F-25-7 `dose_seq`)
//   ADR-005(主机唯一 Append)· ADR-029 §③(`IPayloadEncoder`)
//
// ⚠️ **本写者只负责「一次命中 → 一条 onset」**,不判「该不该出手」:
//   出手意图的三类来源(玩家输入 / 27 的 Engage / 野兽)归各自的驱动;本类零判序。
// ⚠️ **`dose_seq` 从流前缀纯函数派生**(同 tick 同目标已有几条 ⇒ 本条序号),
//   不用可变计数器 —— 读档重放会得到同一条流(与 `CombatOnsetStream.ComputeDoseSeq` 同义,
//   但那条实现是「简化版按 Kind 计数」,会把不同目标的 onset 也算进来 ⇒ 本类不复用)。
// ⚠️ **Patient 头 = 被击中的目标 id**:高水位能从流里看见敌人 id(ADR-006 Amendment B)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary><see cref="EnemyInjuryWriter.TryWrite"/> 的结果码。</summary>
    public enum EnemyInjuryWriteResult
    {
        /// <summary>已写入一条 `EnemyInjuryOnset`。</summary>
        Applied = 0,
        /// <summary>入参非法 ⇒ 零写入。</summary>
        RejectInvalidArgument = 1,
    }

    /// <summary>敌人伤情发作写者(25)。</summary>
    public sealed class EnemyInjuryWriter
    {
        private readonly IEventSink _sink;
        private readonly IPayloadEncoder _encoder;
        private readonly Func<IReadOnlyList<SimEvent>> _eventsOf;
        private readonly IPayloadDecoder _decoder;

        /// <param name="sink">事件写入通道(主机唯一)。</param>
        /// <param name="encoder">载荷编码器。</param>
        /// <param name="eventsOf">事件流只读视图(`dose_seq` 派生)。</param>
        /// <param name="decoder">载荷解码器。</param>
        /// <exception cref="ArgumentNullException">任一依赖为 null。</exception>
        public EnemyInjuryWriter(IEventSink sink, IPayloadEncoder encoder,
                                 Func<IReadOnlyList<SimEvent>> eventsOf, IPayloadDecoder decoder)
        {
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
            _encoder = encoder ?? throw new ArgumentNullException(nameof(encoder));
            _eventsOf = eventsOf ?? throw new ArgumentNullException(nameof(eventsOf));
            _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
        }

        /// <summary>一次命中落一条 onset。</summary>
        /// <param name="actorId">出手者 id(玩家 / 敌人;≥ 0)。</param>
        /// <param name="targetId">被击中者 id(≥ 0)。</param>
        /// <param name="injuryId">伤情 id(9 的伤情注册表 ordinal;本批 = `INJ_BLUNT` = 15)。</param>
        /// <param name="magnitude">本次 PS 量(`(0,1]`;由 F-25-2 求值,本类不改)。</param>
        /// <param name="tick">当前逻辑 tick(≥ 0)。</param>
        public EnemyInjuryWriteResult TryWrite(int actorId, int targetId, int injuryId,
                                               Fix magnitude, long tick)
        {
            if (actorId < 0 || targetId < 0 || injuryId < 0 || tick < 0) return EnemyInjuryWriteResult.RejectInvalidArgument;
            if (magnitude.Raw <= 0) return EnemyInjuryWriteResult.RejectInvalidArgument;

            int doseSeq = NextDoseSeq(targetId, tick);
            var payload = new EnemyInjuryOnsetPayload(actorId, targetId, injuryId, magnitude, tick, doseSeq);
            _sink.Append(new SimEvent(tick, new PatientId(targetId), -1 /* 未发号哨兵 O-1 */, EventKind.EnemyInjuryOnset,
                                      _encoder.Encode(EventKind.EnemyInjuryOnset, in payload)));
            return EnemyInjuryWriteResult.Applied;
        }

        private int NextDoseSeq(int targetId, long tick)
        {
            IReadOnlyList<SimEvent> events = _eventsOf();
            if (events == null) return 0;
            int n = 0;
            for (int i = 0; i < events.Count; i++)
            {
                SimEvent e = events[i];
                if (e.Kind != EventKind.EnemyInjuryOnset || e.Tick != tick) continue;
                EnemyInjuryOnsetPayload p = _decoder.Decode<EnemyInjuryOnsetPayload>(e.Kind, e.Payload);
                if (p.TargetId == targetId) n++;
            }
            return n;
        }
    }
}
