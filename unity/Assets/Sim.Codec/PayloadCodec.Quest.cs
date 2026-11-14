// R-4 · 任务流 2 支(ADR-045 §D2)。schema 源 = entities.yaml payload_schema。
//
// ⚠️ 两条载荷均**无 tick / 无 seq / 无 patient_id**:
//    前两者是事件头 `SimEvent.Tick` / `Seq` 的量(入载荷 = 第二真源);
//    后者因奇遇是玩家级事实(`Patient = PatientId.None`)。
//    同 `TechniqueUnlocked` / `ChapterAdvanced` 的口径。
//
// ⚠️ tag 值域 1..31(uint 掩码),每个 tag **恰好一次**(由 `RequireCompleteMask` 保证)。

using System;
using System.IO;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.Codec
{
    public static partial class PayloadCodec
    {
        // ── 20. AdventureAccepted(ADR-045 §D2)───────────────────────────
        // tag: 1 AdventureId · 2 ActorId —— 两个 i32,全部必填。

        public static byte[] Encode(in AdventureAcceptedPayload p)
        {
            var w = new CodecWriter();
            w.WriteFieldInt32(1, p.AdventureId);
            w.WriteFieldInt32(2, p.ActorId);
            return w.ToArray();
        }

        private static AdventureAcceptedPayload DecodeAdventureAccepted(ReadOnlySpan<byte> bytes)
        {
            var r = new CodecReader(bytes);
            uint seen = 0;
            int adventureId = 0, actorId = 0;
            while (r.HasMore)
            {
                switch (ReadTagChecked(ref r, nameof(EventKind.AdventureAccepted), ref seen))
                {
                    case 1: adventureId = r.ReadInt32LittleEndian(); break;
                    case 2: actorId = r.ReadInt32LittleEndian(); break;
                    default: throw new InvalidDataException("AdventureAccepted: 未知 tag");
                }
            }
            RequireCompleteMask(nameof(EventKind.AdventureAccepted), seen, (1u << 2) - 1);
            r.EnsureFullyConsumed();
            return new AdventureAcceptedPayload(adventureId, actorId);
        }

        // ── 21. AdventureCompleted(ADR-045 §D2)──────────────────────────
        // tag: 1 AdventureId · 2 ActorId · 3 ItemKey · 4 Qty —— 四个 i32,全部必填。
        // ⚠️ ItemKey / Qty 入载荷是刻意的:完成事件必须自带「给了什么」,
        //    只记 adventure_id 会让「给了几件」变成读作者态表推导(第二真源)。

        public static byte[] Encode(in AdventureCompletedPayload p)
        {
            var w = new CodecWriter();
            w.WriteFieldInt32(1, p.AdventureId);
            w.WriteFieldInt32(2, p.ActorId);
            w.WriteFieldInt32(3, p.ItemKey);
            w.WriteFieldInt32(4, p.Qty);
            return w.ToArray();
        }

        private static AdventureCompletedPayload DecodeAdventureCompleted(ReadOnlySpan<byte> bytes)
        {
            var r = new CodecReader(bytes);
            uint seen = 0;
            int adventureId = 0, actorId = 0, itemKey = 0, qty = 0;
            while (r.HasMore)
            {
                switch (ReadTagChecked(ref r, nameof(EventKind.AdventureCompleted), ref seen))
                {
                    case 1: adventureId = r.ReadInt32LittleEndian(); break;
                    case 2: actorId = r.ReadInt32LittleEndian(); break;
                    case 3: itemKey = r.ReadInt32LittleEndian(); break;
                    case 4: qty = r.ReadInt32LittleEndian(); break;
                    default: throw new InvalidDataException("AdventureCompleted: 未知 tag");
                }
            }
            RequireCompleteMask(nameof(EventKind.AdventureCompleted), seen, (1u << 4) - 1);
            r.EnsureFullyConsumed();
            return new AdventureCompletedPayload(adventureId, actorId, itemKey, qty);
        }
    }
}
