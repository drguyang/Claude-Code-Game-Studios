// R-1 · 病史流 13 支(事件表序 = EventKind 声明序)。schema 源 = entities.yaml payload_schema。
// tag = struct 声明序(见 PayloadCodec.cs 头注)。

using System;
using System.IO;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.Codec
{
    public static partial class PayloadCodec
    {
        // ── 1. InjuryOnset(25 直登)──────────────────────────────────────
        // tag: 1 ActorId · 2 TargetId · 3 InjuryId · 4 Magnitude(Fix)· 5 Tick · 6 DoseSeq

        public static byte[] Encode(in InjuryOnsetPayload p)
        {
            var w = new CodecWriter();
            w.WriteFieldInt32(1, p.ActorId);
            w.WriteFieldInt32(2, p.TargetId);
            w.WriteFieldInt32(3, p.InjuryId);
            w.WriteFieldFix(4, p.Magnitude);
            w.WriteFieldInt64(5, p.Tick);
            w.WriteFieldInt32(6, p.DoseSeq);
            return w.ToArray();
        }

        private static InjuryOnsetPayload DecodeInjuryOnset(ReadOnlySpan<byte> bytes)
        {
            var r = new CodecReader(bytes);
            uint seen = 0;
            int actorId = 0, targetId = 0, injuryId = 0, doseSeq = 0;
            Fix magnitude = default;
            long tick = 0;
            while (r.HasMore)
            {
                switch (ReadTagChecked(r, nameof(InjuryOnset), ref seen))
                {
                    case 1: actorId = r.ReadInt32LittleEndian(); break;
                    case 2: targetId = r.ReadInt32LittleEndian(); break;
                    case 3: injuryId = r.ReadInt32LittleEndian(); break;
                    case 4: magnitude = r.ReadFieldFix(); break;
                    case 5: tick = r.ReadInt64LittleEndian(); break;
                    case 6: doseSeq = r.ReadInt32LittleEndian(); break;
                    default: throw new InvalidDataException($"InjuryOnset: 未知 tag");
                }
            }
            RequireCompleteMask(nameof(InjuryOnset), seen, (1u << 6) - 1);
            r.EnsureFullyConsumed();
            return new InjuryOnsetPayload(actorId, targetId, injuryId, magnitude, tick, doseSeq);
        }

        // ── 2. CompoundTriggered(9)───────────────────────────────────────
        // tag: 1 Tick · 2 TargetId · 3 RuleId · 4 ComponentId

        public static byte[] Encode(in CompoundTriggeredPayload p)
        {
            var w = new CodecWriter();
            w.WriteFieldInt64(1, p.Tick);
            w.WriteFieldInt32(2, p.TargetId);
            w.WriteFieldInt32(3, p.RuleId);
            w.WriteFieldInt32(4, p.ComponentId);
            return w.ToArray();
        }

        private static CompoundTriggeredPayload DecodeCompoundTriggered(ReadOnlySpan<byte> bytes)
        {
            var r = new CodecReader(bytes);
            uint seen = 0;
            long tick = 0;
            int targetId = 0, ruleId = 0, componentId = 0;
            while (r.HasMore)
            {
                switch (ReadTagChecked(r, nameof(CompoundTriggered), ref seen))
                {
                    case 1: tick = r.ReadInt64LittleEndian(); break;
                    case 2: targetId = r.ReadInt32LittleEndian(); break;
                    case 3: ruleId = r.ReadInt32LittleEndian(); break;
                    case 4: componentId = r.ReadInt32LittleEndian(); break;
                    default: throw new InvalidDataException($"CompoundTriggered: 未知 tag");
                }
            }
            RequireCompleteMask(nameof(CompoundTriggered), seen, (1u << 4) - 1);
            r.EnsureFullyConsumed();
            return new CompoundTriggeredPayload(tick, targetId, ruleId, componentId);
        }

        // ── 3. CompoundExpired(9)─────────────────────────────────────────
        // tag: 1 Tick · 2 TargetId · 3 RuleId

        public static byte[] Encode(in CompoundExpiredPayload p)
        {
            var w = new CodecWriter();
            w.WriteFieldInt64(1, p.Tick);
            w.WriteFieldInt32(2, p.TargetId);
            w.WriteFieldInt32(3, p.RuleId);
            return w.ToArray();
        }

        private static CompoundExpiredPayload DecodeCompoundExpired(ReadOnlySpan<byte> bytes)
        {
            var r = new CodecReader(bytes);
            uint seen = 0;
            long tick = 0;
            int targetId = 0, ruleId = 0;
            while (r.HasMore)
            {
                switch (ReadTagChecked(r, nameof(CompoundExpired), ref seen))
                {
                    case 1: tick = r.ReadInt64LittleEndian(); break;
                    case 2: targetId = r.ReadInt32LittleEndian(); break;
                    case 3: ruleId = r.ReadInt32LittleEndian(); break;
                    default: throw new InvalidDataException($"CompoundExpired: 未知 tag");
                }
            }
            RequireCompleteMask(nameof(CompoundExpired), seen, (1u << 3) - 1);
            r.EnsureFullyConsumed();
            return new CompoundExpiredPayload(tick, targetId, ruleId);
        }

        // ── 4. CareApplied(8)─────────────────────────────────────────────
        // tag: 1 Tick · 2 ActionId · 3 Caregiver · 4 Phase

        public static byte[] Encode(in CareAppliedPayload p)
        {
            var w = new CodecWriter();
            w.WriteFieldInt64(1, p.Tick);
            w.WriteFieldInt32(2, p.ActionId);
            w.WriteFieldInt32(3, p.Caregiver);
            w.WriteFieldInt32(4, p.Phase);
            return w.ToArray();
        }

        private static CareAppliedPayload DecodeCareApplied(ReadOnlySpan<byte> bytes)
        {
            var r = new CodecReader(bytes);
            uint seen = 0;
            long tick = 0;
            int actionId = 0, caregiver = 0, phase = 0;
            while (r.HasMore)
            {
                switch (ReadTagChecked(r, nameof(CareApplied), ref seen))
                {
                    case 1: tick = r.ReadInt64LittleEndian(); break;
                    case 2: actionId = r.ReadInt32LittleEndian(); break;
                    case 3: caregiver = r.ReadInt32LittleEndian(); break;
                    case 4: phase = r.ReadInt32LittleEndian(); break;
                    default: throw new InvalidDataException($"CareApplied: 未知 tag");
                }
            }
            RequireCompleteMask(nameof(CareApplied), seen, (1u << 4) - 1);
            r.EnsureFullyConsumed();
            return new CareAppliedPayload(tick, actionId, caregiver, phase);
        }

        // ── 5. EmergencyAttempt(ADR-009 Amendment I)──────────────────────
        // tag: 1 Action · 2 HoldTicks · 3 Edges · 4 MagPeak · 5 MagLast · 6 Method · 7 ActorId · 8 EdgeTicks[]
        // 跨字段约束:Edges == EdgeTicks.Length(写侧拒 / 读侧拒,双侧同源)。

        public static byte[] Encode(in EmergencyAttemptPayload p)
        {
            if (p.EdgeTicks == null)
                throw new ArgumentException("EdgeTicks 不得为 null(=0 边时为空数组)", nameof(p));
            if (p.EdgeTicks.Length != p.Edges)
                throw new ArgumentException(
                    $"Edges={p.Edges} 与 EdgeTicks.Length={p.EdgeTicks.Length} 不符 —— 坏数据不进字节面",
                    nameof(p));
            var w = new CodecWriter();
            w.WriteFieldInt32(1, p.Action);
            w.WriteFieldInt32(2, p.HoldTicks);
            w.WriteFieldInt32(3, p.Edges);
            w.WriteFieldInt32(4, p.MagPeak);
            w.WriteFieldInt32(5, p.MagLast);
            w.WriteFieldInt32(6, p.Method);
            w.WriteFieldInt32(7, p.ActorId);
            w.WriteFieldArrayInt32(8, p.EdgeTicks);
            return w.ToArray();
        }

        private static EmergencyAttemptPayload DecodeEmergencyAttempt(ReadOnlySpan<byte> bytes)
        {
            var r = new CodecReader(bytes);
            uint seen = 0;
            int action = 0, holdTicks = 0, edges = 0, magPeak = 0, magLast = 0, method = 0, actorId = 0;
            int[] edgeTicks = null;
            while (r.HasMore)
            {
                switch (ReadTagChecked(r, nameof(EmergencyAttempt), ref seen))
                {
                    case 1: action = r.ReadInt32LittleEndian(); break;
                    case 2: holdTicks = r.ReadInt32LittleEndian(); break;
                    case 3: edges = r.ReadInt32LittleEndian(); break;
                    case 4: magPeak = r.ReadInt32LittleEndian(); break;
                    case 5: magLast = r.ReadInt32LittleEndian(); break;
                    case 6: method = r.ReadInt32LittleEndian(); break;
                    case 7: actorId = r.ReadInt32LittleEndian(); break;
                    case 8: edgeTicks = r.ReadFieldArrayInt32(); break;
                    default: throw new InvalidDataException($"EmergencyAttempt: 未知 tag");
                }
            }
            RequireCompleteMask(nameof(EmergencyAttempt), seen, (1u << 8) - 1);
            r.EnsureFullyConsumed();
            if (edgeTicks.Length != edges)
                throw new InvalidDataException(
                    $"EmergencyAttempt: Edges={edges} 与 EdgeTicks.Length={edgeTicks.Length} 不符 —— 已损坏");
            return new EmergencyAttemptPayload(action, holdTicks, edges, magPeak, magLast, method, actorId, edgeTicks);
        }

        // ── 6. EmergencyTreatmentApplied(Amendment I · 10 写)─────────────
        // tag: 1 Tick · 2 TreatmentId · 3 ActorId · 4 Polarity · 5 DrugPotency(Fix)
        //      6 HalfLife · 7 Method · 8 Cause · 9 Seq

        public static byte[] Encode(in EmergencyTreatmentAppliedPayload p)
        {
            var w = new CodecWriter();
            w.WriteFieldInt64(1, p.Tick);
            w.WriteFieldInt32(2, p.TreatmentId);
            w.WriteFieldInt32(3, p.ActorId);
            w.WriteFieldInt32(4, p.Polarity);
            w.WriteFieldFix(5, p.DrugPotency);
            w.WriteFieldInt64(6, p.HalfLife);
            w.WriteFieldInt32(7, p.Method);
            w.WriteFieldInt32(8, p.Cause);
            w.WriteFieldInt64(9, p.Seq);
            return w.ToArray();
        }

        private static EmergencyTreatmentAppliedPayload DecodeEmergencyTreatmentApplied(ReadOnlySpan<byte> bytes)
        {
            var r = new CodecReader(bytes);
            uint seen = 0;
            long tick = 0, halfLife = 0, seq = 0;
            int treatmentId = 0, actorId = 0, polarity = 0, method = 0, cause = 0;
            Fix drugPotency = default;
            while (r.HasMore)
            {
                switch (ReadTagChecked(r, nameof(EmergencyTreatmentApplied), ref seen))
                {
                    case 1: tick = r.ReadInt64LittleEndian(); break;
                    case 2: treatmentId = r.ReadInt32LittleEndian(); break;
                    case 3: actorId = r.ReadInt32LittleEndian(); break;
                    case 4: polarity = r.ReadInt32LittleEndian(); break;
                    case 5: drugPotency = r.ReadFieldFix(); break;
                    case 6: halfLife = r.ReadInt64LittleEndian(); break;
                    case 7: method = r.ReadInt32LittleEndian(); break;
                    case 8: cause = r.ReadInt32LittleEndian(); break;
                    case 9: seq = r.ReadInt64LittleEndian(); break;
                    default: throw new InvalidDataException($"EmergencyTreatmentApplied: 未知 tag");
                }
            }
            RequireCompleteMask(nameof(EmergencyTreatmentApplied), seen, (1u << 9) - 1);
            r.EnsureFullyConsumed();
            return new EmergencyTreatmentAppliedPayload(tick, treatmentId, actorId, polarity, drugPotency, halfLife, method, cause, seq);
        }

        // ── 7. DrugTreatmentApplied(Amendment I · 11 写)──────────────────
        // tag: 1 Tick · 2 TreatmentId · 3 ActorId · 4 Polarity · 5 DrugPotency(Fix)
        //      6 HalfLife · 7 Seq

        public static byte[] Encode(in DrugTreatmentAppliedPayload p)
        {
            var w = new CodecWriter();
            w.WriteFieldInt64(1, p.Tick);
            w.WriteFieldInt32(2, p.TreatmentId);
            w.WriteFieldInt32(3, p.ActorId);
            w.WriteFieldInt32(4, p.Polarity);
            w.WriteFieldFix(5, p.DrugPotency);
            w.WriteFieldInt64(6, p.HalfLife);
            w.WriteFieldInt64(7, p.Seq);
            return w.ToArray();
        }

        private static DrugTreatmentAppliedPayload DecodeDrugTreatmentApplied(ReadOnlySpan<byte> bytes)
        {
            var r = new CodecReader(bytes);
            uint seen = 0;
            long tick = 0, halfLife = 0, seq = 0;
            int treatmentId = 0, actorId = 0, polarity = 0;
            Fix drugPotency = default;
            while (r.HasMore)
            {
                switch (ReadTagChecked(r, nameof(DrugTreatmentApplied), ref seen))
                {
                    case 1: tick = r.ReadInt64LittleEndian(); break;
                    case 2: treatmentId = r.ReadInt32LittleEndian(); break;
                    case 3: actorId = r.ReadInt32LittleEndian(); break;
                    case 4: polarity = r.ReadInt32LittleEndian(); break;
                    case 5: drugPotency = r.ReadFieldFix(); break;
                    case 6: halfLife = r.ReadInt64LittleEndian(); break;
                    case 7: seq = r.ReadInt64LittleEndian(); break;
                    default: throw new InvalidDataException($"DrugTreatmentApplied: 未知 tag");
                }
            }
            RequireCompleteMask(nameof(DrugTreatmentApplied), seen, (1u << 7) - 1);
            r.EnsureFullyConsumed();
            return new DrugTreatmentAppliedPayload(tick, treatmentId, actorId, polarity, drugPotency, halfLife, seq);
        }

        // ── 8. SkillGrown(ADR-024 补齐轮)─────────────────────────────────
        // tag: 1 ActorId · 2 PatientId · 3 SkillId · 4 ObjectId · 5 NoveltyClass · 6 Level

        public static byte[] Encode(in SkillGrownPayload p)
        {
            var w = new CodecWriter();
            w.WriteFieldInt32(1, p.ActorId);
            w.WriteFieldInt32(2, p.PatientId);
            w.WriteFieldInt32(3, p.SkillId);
            w.WriteFieldInt32(4, p.ObjectId);
            w.WriteFieldInt32(5, p.NoveltyClass);
            w.WriteFieldInt32(6, p.Level);
            return w.ToArray();
        }

        private static SkillGrownPayload DecodeSkillGrown(ReadOnlySpan<byte> bytes)
        {
            var r = new CodecReader(bytes);
            uint seen = 0;
            int actorId = 0, patientId = 0, skillId = 0, objectId = 0, noveltyClass = 0, level = 0;
            while (r.HasMore)
            {
                switch (ReadTagChecked(r, nameof(SkillGrown), ref seen))
                {
                    case 1: actorId = r.ReadInt32LittleEndian(); break;
                    case 2: patientId = r.ReadInt32LittleEndian(); break;
                    case 3: skillId = r.ReadInt32LittleEndian(); break;
                    case 4: objectId = r.ReadInt32LittleEndian(); break;
                    case 5: noveltyClass = r.ReadInt32LittleEndian(); break;
                    case 6: level = r.ReadInt32LittleEndian(); break;
                    default: throw new InvalidDataException($"SkillGrown: 未知 tag");
                }
            }
            RequireCompleteMask(nameof(SkillGrown), seen, (1u << 6) - 1);
            r.EnsureFullyConsumed();
            return new SkillGrownPayload(actorId, patientId, skillId, objectId, noveltyClass, level);
        }

        // ── 9. EventRolled(ADR-007 §三)───────────────────────────────────
        // tag: 1 Win · 2 Tier · 3 Ordinal · 4 ChosenKey

        public static byte[] Encode(in EventRolledPayload p)
        {
            var w = new CodecWriter();
            w.WriteFieldInt32(1, p.Win);
            w.WriteFieldInt32(2, p.Tier);
            w.WriteFieldInt64(3, p.Ordinal);
            w.WriteFieldInt32(4, p.ChosenKey);
            return w.ToArray();
        }

        private static EventRolledPayload DecodeEventRolled(ReadOnlySpan<byte> bytes)
        {
            var r = new CodecReader(bytes);
            uint seen = 0;
            int win = 0, tier = 0, chosenKey = 0;
            long ordinal = 0;
            while (r.HasMore)
            {
                switch (ReadTagChecked(r, nameof(EventRolled), ref seen))
                {
                    case 1: win = r.ReadInt32LittleEndian(); break;
                    case 2: tier = r.ReadInt32LittleEndian(); break;
                    case 3: ordinal = r.ReadInt64LittleEndian(); break;
                    case 4: chosenKey = r.ReadInt32LittleEndian(); break;
                    default: throw new InvalidDataException($"EventRolled: 未知 tag");
                }
            }
            RequireCompleteMask(nameof(EventRolled), seen, (1u << 4) - 1);
            r.EnsureFullyConsumed();
            return new EventRolledPayload(win, tier, ordinal, chosenKey);
        }

        // ── 10. EventArrived(ADR-007 §三)─────────────────────────────────
        // tag: 1 EventKey · 2 Tier · 3 SpawnAnchor(WorldPos)· 4 CauseClueKey

        public static byte[] Encode(in EventArrivedPayload p)
        {
            var w = new CodecWriter();
            w.WriteFieldInt32(1, p.EventKey);
            w.WriteFieldInt32(2, p.Tier);
            w.WriteFieldWorldPos(3, p.SpawnAnchor);
            w.WriteFieldInt32(4, p.CauseClueKey);
            return w.ToArray();
        }

        private static EventArrivedPayload DecodeEventArrived(ReadOnlySpan<byte> bytes)
        {
            var r = new CodecReader(bytes);
            uint seen = 0;
            int eventKey = 0, tier = 0, causeClueKey = 0;
            WorldPos spawnAnchor = default;
            while (r.HasMore)
            {
                switch (ReadTagChecked(r, nameof(EventArrived), ref seen))
                {
                    case 1: eventKey = r.ReadInt32LittleEndian(); break;
                    case 2: tier = r.ReadInt32LittleEndian(); break;
                    case 3: spawnAnchor = r.ReadFieldWorldPos(); break;
                    case 4: causeClueKey = r.ReadInt32LittleEndian(); break;
                    default: throw new InvalidDataException($"EventArrived: 未知 tag");
                }
            }
            RequireCompleteMask(nameof(EventArrived), seen, (1u << 4) - 1);
            r.EnsureFullyConsumed();
            return new EventArrivedPayload(eventKey, tier, spawnAnchor, causeClueKey);
        }

        // ── 11. ThreatDeferred(ADR-007 §三)───────────────────────────────
        // tag: 1 SlotIndex · 2 EventKey

        public static byte[] Encode(in ThreatDeferredPayload p)
        {
            var w = new CodecWriter();
            w.WriteFieldInt32(1, p.SlotIndex);
            w.WriteFieldInt32(2, p.EventKey);
            return w.ToArray();
        }

        private static ThreatDeferredPayload DecodeThreatDeferred(ReadOnlySpan<byte> bytes)
        {
            var r = new CodecReader(bytes);
            uint seen = 0;
            int slotIndex = 0, eventKey = 0;
            while (r.HasMore)
            {
                switch (ReadTagChecked(r, nameof(ThreatDeferred), ref seen))
                {
                    case 1: slotIndex = r.ReadInt32LittleEndian(); break;
                    case 2: eventKey = r.ReadInt32LittleEndian(); break;
                    default: throw new InvalidDataException($"ThreatDeferred: 未知 tag");
                }
            }
            RequireCompleteMask(nameof(ThreatDeferred), seen, (1u << 2) - 1);
            r.EnsureFullyConsumed();
            return new ThreatDeferredPayload(slotIndex, eventKey);
        }

        // ── 12. ThreatDeferralCleared(ADR-007 §三)────────────────────────
        // tag: 1 SlotIndex · 2 Reason

        public static byte[] Encode(in ThreatDeferralClearedPayload p)
        {
            var w = new CodecWriter();
            w.WriteFieldInt32(1, p.SlotIndex);
            w.WriteFieldInt32(2, p.Reason);
            return w.ToArray();
        }

        private static ThreatDeferralClearedPayload DecodeThreatDeferralCleared(ReadOnlySpan<byte> bytes)
        {
            var r = new CodecReader(bytes);
            uint seen = 0;
            int slotIndex = 0, reason = 0;
            while (r.HasMore)
            {
                switch (ReadTagChecked(r, nameof(ThreatDeferralCleared), ref seen))
                {
                    case 1: slotIndex = r.ReadInt32LittleEndian(); break;
                    case 2: reason = r.ReadInt32LittleEndian(); break;
                    default: throw new InvalidDataException($"ThreatDeferralCleared: 未知 tag");
                }
            }
            RequireCompleteMask(nameof(ThreatDeferralCleared), seen, (1u << 2) - 1);
            r.EnsureFullyConsumed();
            return new ThreatDeferralClearedPayload(slotIndex, reason);
        }

        // ── 13. HistoryFlagChanged(ADR-007 §三)───────────────────────────
        // tag: 1 FlagId · 2 NewValue

        public static byte[] Encode(in HistoryFlagChangedPayload p)
        {
            var w = new CodecWriter();
            w.WriteFieldInt32(1, p.FlagId);
            w.WriteFieldInt32(2, p.NewValue);
            return w.ToArray();
        }

        private static HistoryFlagChangedPayload DecodeHistoryFlagChanged(ReadOnlySpan<byte> bytes)
        {
            var r = new CodecReader(bytes);
            uint seen = 0;
            int flagId = 0, newValue = 0;
            while (r.HasMore)
            {
                switch (ReadTagChecked(r, nameof(HistoryFlagChanged), ref seen))
                {
                    case 1: flagId = r.ReadInt32LittleEndian(); break;
                    case 2: newValue = r.ReadInt32LittleEndian(); break;
                    default: throw new InvalidDataException($"HistoryFlagChanged: 未知 tag");
                }
            }
            RequireCompleteMask(nameof(HistoryFlagChanged), seen, (1u << 2) - 1);
            r.EnsureFullyConsumed();
            return new HistoryFlagChangedPayload(flagId, newValue);
        }
    }
}
