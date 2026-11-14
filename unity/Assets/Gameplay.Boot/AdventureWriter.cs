// 奇遇事件的**唯一**写者(ADR-045 §D3)—— 任务流的 Append 单点。
//
// 权威:
//   docs/architecture/adr-045-quest-stream.md §D2(两 Kind)· §D3(唯一写者)
//   · §D5(同一 (actor, adventure) 至多一条)· §D6(完成时 DropSpawned 落脚下)
//   design/levels/prologue-beat-script.md §3.1
//
// ⚠️ **任何第二处 `Append(AdventureAccepted / AdventureCompleted)` 都算违规** ——
//    与 `TechniqueUnlockWriter` / `ChapterAdvanceWriter` 同一条口径:多写者 = 多真源。
//
// ⚠️ 去重**在这里做,不在调用方**:驱动每 tick 都会重算「够不够」,
//    若靠调用方纪律去重,读档重放/同 tick 二次推进就会**二次发放组件**。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>奇遇两支事件的唯一写入通道(任务流 · ADR-045)。</summary>
    public sealed class AdventureWriter
    {
        private readonly IEventSink _sink;
        private readonly IPayloadEncoder _encoder;
        private readonly Func<IReadOnlyList<SimEvent>> _events;
        private readonly IPayloadDecoder _decoder;
        private readonly DropEventWriter _drops;

        /// <param name="sink">主机事件写入通道(唯一 Append 落点)。</param>
        /// <param name="encoder">载荷编码器(ADR-029 唯一路径)。</param>
        /// <param name="events">事件流只读视图(去重判据)。</param>
        /// <param name="decoder">载荷解码器(读已写过的奇遇 id)。</param>
        /// <param name="drops">掉落写者(**组件入包的唯一路径** —— ADR-045 §D6)。</param>
        /// <exception cref="ArgumentNullException">任一依赖为 null。</exception>
        public AdventureWriter(IEventSink sink, IPayloadEncoder encoder,
                               Func<IReadOnlyList<SimEvent>> events, IPayloadDecoder decoder,
                               DropEventWriter drops)
        {
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
            _encoder = encoder ?? throw new ArgumentNullException(nameof(encoder));
            _events = events ?? throw new ArgumentNullException(nameof(events));
            _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
            _drops = drops ?? throw new ArgumentNullException(nameof(drops));
        }

        /// <summary>写「接取」(已写过 ⇒ no-op)。</summary>
        /// <returns>`true` = 本次真的写了一条。</returns>
        public bool Accept(int adventureId, int actorId, long tick)
        {
            if (adventureId < 0) throw new ArgumentOutOfRangeException(nameof(adventureId));
            if (actorId < 0) throw new ArgumentOutOfRangeException(nameof(actorId));
            if (tick < 0) throw new ArgumentOutOfRangeException(nameof(tick));
            if (Exists(EventKind.AdventureAccepted, adventureId, actorId)) return false;

            var payload = new AdventureAcceptedPayload(adventureId, actorId);
            _sink.Append(new SimEvent(tick, PatientId.None, 0L, EventKind.AdventureAccepted,
                                      _encoder.Encode(EventKind.AdventureAccepted, in payload)));
            return true;
        }

        /// <summary>写「完成」,并在**同 tick** 于 <paramref name="dropCell"/> 发一件组件掉落。</summary>
        /// <returns>`true` = 本次真的写了一条(含掉落)。</returns>
        /// <remarks>⚠️ 已完成 ⇒ **整段 no-op**,不重复发掉落 —— 这是「组件只给一次」的机制保证(§D5)。</remarks>
        public bool Complete(in AdventureStep step, int actorId, WorldPos dropCell, long tick)
        {
            if (actorId < 0) throw new ArgumentOutOfRangeException(nameof(actorId));
            if (tick < 0) throw new ArgumentOutOfRangeException(nameof(tick));
            if (Exists(EventKind.AdventureCompleted, step.Id, actorId)) return false;

            var payload = new AdventureCompletedPayload(step.Id, actorId, step.ItemKey, qty: 1);
            _sink.Append(new SimEvent(tick, PatientId.None, 0L, EventKind.AdventureCompleted,
                                      _encoder.Encode(EventKind.AdventureCompleted, in payload)));

            // §D6:组件经**既有掉落路径**入世界(玩家自行 DropClaimed)—— 不新增库存谓词。
            _drops.RecordSpawned(tick, dropCell, step.ItemKey, qty: 1);
            return true;
        }

        /// <summary>是否已有该 `(kind, adventure, actor)` 的事实。</summary>
        public bool Exists(EventKind kind, int adventureId, int actorId)
        {
            IReadOnlyList<SimEvent> events = _events();
            if (events == null) return false;
            for (int i = 0; i < events.Count; i++)
            {
                SimEvent e = events[i];
                if (e.Kind != kind) continue;
                if (kind == EventKind.AdventureAccepted)
                {
                    AdventureAcceptedPayload p = _decoder.Decode<AdventureAcceptedPayload>(kind, e.Payload);
                    if (p.AdventureId == adventureId && p.ActorId == actorId) return true;
                }
                else if (kind == EventKind.AdventureCompleted)
                {
                    AdventureCompletedPayload p = _decoder.Decode<AdventureCompletedPayload>(kind, e.Payload);
                    if (p.AdventureId == adventureId && p.ActorId == actorId) return true;
                }
            }
            return false;
        }
    }
}
