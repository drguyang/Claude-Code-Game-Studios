// 敌人花名册 —— 从世界流**纯投影**出「此刻场上有哪些敌人、各自在哪、伤到什么程度」。
//
// 权威:
//   ADR-016 §二(敌人复用 9 的伤情模型;id 经 IIdAuthority 与病人共空间)
//   ADR-016 §一(效果进流,决策不进流 —— 敌人格**不进流**,这里只读出生格)
//   GDD enemy-ai.md 规则十六–十八(遭遇生命周期 / 伤情真值路由)
//   ADR-009 §四(派生态:每次现算,不缓存、不存档)
//
// ═══ 它只读三支事件 ═══════════════════════════════════════════════════════
//   `EncounterStarted`(52)→ 遭遇的成员 / 原型 / 出生格
//   `EnemyInjuryOnset`(25)→ 每个目标的累计伤量 Σ magnitude
//   `InjuryStateChanged`(9)→ 目标是否已倒地(Coma / Deceased)
//   `EncounterEnded`(27)→ 全倒地收场时把成员一律视为倒地(防漏写状态转移)
//
// ⚠️ 本类是**只读**投影:不写流、不缓存、不持跨调用状态(与 `FoldedPredicate` 同格)。
//    「此刻」= 传入事件流的那一段;调用方决定传全流还是前缀。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>场上的一名敌人(投影行)。</summary>
    public readonly struct EnemyActor
    {
        /// <summary>受伤实体 id(与病人共用 `IIdAuthority` 空间)。</summary>
        public readonly int ActorId;

        /// <summary>遭遇原型(`EncounterStarted.proto_id`;0 = 兵痞 / 1 = 野狗)。</summary>
        public readonly int ProtoId;

        /// <summary>所属遭遇 id。</summary>
        public readonly int EncounterId;

        /// <summary>出生格(敌人格是派生态、不进流 —— 这是**唯一**可由流得知的位置)。</summary>
        public readonly WorldPos Cell;

        /// <summary>累计伤量(`Σ EnemyInjuryOnset.magnitude`,PS 单位)。</summary>
        public readonly Fix Hurt;

        /// <summary>是否已倒地(Coma / Deceased,或所属遭遇以「全倒地」收场)。</summary>
        public readonly bool Downed;

        /// <summary>构造一行。</summary>
        public EnemyActor(int actorId, int protoId, int encounterId, WorldPos cell, Fix hurt, bool downed)
        {
            ActorId = actorId; ProtoId = protoId; EncounterId = encounterId;
            Cell = cell; Hurt = hurt; Downed = downed;
        }
    }

    /// <summary>敌人花名册(纯投影;每次现算)。</summary>
    public static class EnemyRoster
    {
        /// <summary>`EncounterEnded.reason` = 全倒地(与 `EncounterEndReason.AllDowned` 同值)。</summary>
        public const int ReasonAllDowned = 1;

        /// <summary>
        /// 从事件流投影出全部敌人行(**按 `ActorId` 升序** —— 稳定序,供夹具与选目标)。
        /// </summary>
        /// <param name="events">事件流只读视图(可传全流;本方法按 `Kind` 自筛)。</param>
        /// <param name="decoder">载荷解码器(`Sim` 读载荷的唯一合法路径,ADR-031)。</param>
        /// <exception cref="ArgumentNullException">events 或 decoder 为 null。</exception>
        public static IReadOnlyList<EnemyActor> Build(IReadOnlyList<SimEvent> events, IPayloadDecoder decoder)
        {
            if (events == null) throw new ArgumentNullException(nameof(events));
            if (decoder == null) throw new ArgumentNullException(nameof(decoder));

            var meta = new Dictionary<int, (int proto, int encounter, WorldPos cell)>();
            var hurt = new Dictionary<int, long>();
            var downed = new HashSet<int>();
            var endedAllDowned = new HashSet<int>();

            for (int i = 0; i < events.Count; i++)
            {
                SimEvent e = events[i];
                switch (e.Kind)
                {
                    case EventKind.EncounterStarted:
                    {
                        EncounterStartedPayload p =
                            decoder.Decode<EncounterStartedPayload>(e.Kind, e.Payload);
                        if (p.ActorIds == null) break;
                        for (int a = 0; a < p.ActorIds.Length; a++)
                            meta[p.ActorIds[a]] = (p.ProtoId, p.EncounterId, p.SpawnCell);
                        break;
                    }
                    case EventKind.EnemyInjuryOnset:
                    {
                        EnemyInjuryOnsetPayload p =
                            decoder.Decode<EnemyInjuryOnsetPayload>(e.Kind, e.Payload);
                        hurt.TryGetValue(p.TargetId, out long acc);
                        hurt[p.TargetId] = acc + p.Magnitude.Raw;
                        break;
                    }
                    case EventKind.InjuryStateChanged:
                    {
                        InjuryStateChangedPayload p =
                            decoder.Decode<InjuryStateChangedPayload>(e.Kind, e.Payload);
                        if (p.NewState == (int)InjuryState.Coma || p.NewState == (int)InjuryState.Deceased)
                            downed.Add(p.ActorId);
                        break;
                    }
                    case EventKind.EncounterEnded:
                    {
                        EncounterEndedPayload p =
                            decoder.Decode<EncounterEndedPayload>(e.Kind, e.Payload);
                        if (p.Reason == ReasonAllDowned) endedAllDowned.Add(p.EncounterId);
                        break;
                    }
                }
            }

            var ids = new List<int>(meta.Keys);
            ids.Sort();
            var rows = new List<EnemyActor>(ids.Count);
            for (int i = 0; i < ids.Count; i++)
            {
                int id = ids[i];
                (int proto, int encounter, WorldPos cell) = meta[id];
                hurt.TryGetValue(id, out long h);
                bool isDown = downed.Contains(id) || endedAllDowned.Contains(encounter);
                rows.Add(new EnemyActor(id, proto, encounter, cell, new Fix(h), isDown));
            }
            return rows;
        }

        /// <summary>场上**未倒地**的敌人(按 `ActorId` 升序)。</summary>
        public static IReadOnlyList<EnemyActor> Alive(IReadOnlyList<EnemyActor> rows)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            var alive = new List<EnemyActor>(rows.Count);
            for (int i = 0; i < rows.Count; i++)
                if (!rows[i].Downed) alive.Add(rows[i]);
            return alive;
        }
    }
}
