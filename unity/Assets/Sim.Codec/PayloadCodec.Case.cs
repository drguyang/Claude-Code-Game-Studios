// R-1 · 病例流 5 支。schema 源 = entities.yaml payload_schema(ADR-008 §三 抄本 2026-09-17 订正版)。
// Judgment 两支的 tag 6 = freehand_text(ADR-006 G-3):Encode 强制携带(无 text 形参重载不提供),
// Decode<T> 读出即弃(sim 面无该字段),呈现读取走 PayloadCodec.TryGetFreehandText。

using System;
using System.IO;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.Codec
{
    public static partial class PayloadCodec
    {
        // ── 14. CaseOpened(ADR-008 §三)───────────────────────────────────
        // tag: 1 PatientId · 2 DiseaseSnapshot(u64 bitmask)

        public static byte[] Encode(in CaseOpenedPayload p)
        {
            var w = new CodecWriter();
            w.WriteFieldInt32(1, p.PatientId);
            w.WriteFieldUInt64(2, p.DiseaseSnapshot.Bits);
            return w.ToArray();
        }

        private static CaseOpenedPayload DecodeCaseOpened(ReadOnlySpan<byte> bytes)
        {
            var r = new CodecReader(bytes);
            uint seen = 0;
            int patientId = 0;
            ulong bits = 0;
            while (r.HasMore)
            {
                switch (ReadTagChecked(ref r, nameof(EventKind.CaseOpened), ref seen))
                {
                    case 1: patientId = r.ReadInt32LittleEndian(); break;
                    case 2: bits = r.ReadUInt64LittleEndian(); break;
                    default: throw new InvalidDataException($"CaseOpened: 未知 tag");
                }
            }
            RequireCompleteMask(nameof(EventKind.CaseOpened), seen, (1u << 2) - 1);
            r.EnsureFullyConsumed();
            return new CaseOpenedPayload(patientId, new DiseaseIdSet(bits));
        }

        // ── 15. CaseClosed(ADR-008 §三 / §四)─────────────────────────────
        // tag: 1 PatientId · 2 CaseId · 3 DiseaseSet(u64)· 4 Treated(bool=1B)

        public static byte[] Encode(in CaseClosedPayload p)
        {
            var w = new CodecWriter();
            w.WriteFieldInt32(1, p.PatientId);
            w.WriteFieldCaseId(2, p.CaseId);
            w.WriteFieldUInt64(3, p.DiseaseSet.Bits);
            w.WriteFieldBoolean(4, p.Treated);
            return w.ToArray();
        }

        private static CaseClosedPayload DecodeCaseClosed(ReadOnlySpan<byte> bytes)
        {
            var r = new CodecReader(bytes);
            uint seen = 0;
            int patientId = 0;
            CaseId caseId = default;
            ulong bits = 0;
            bool treated = false;
            while (r.HasMore)
            {
                switch (ReadTagChecked(ref r, nameof(EventKind.CaseClosed), ref seen))
                {
                    case 1: patientId = r.ReadInt32LittleEndian(); break;
                    case 2: caseId = r.ReadFieldCaseId(); break;
                    case 3: bits = r.ReadUInt64LittleEndian(); break;
                    case 4: treated = r.ReadFieldBoolean(); break;
                    default: throw new InvalidDataException($"CaseClosed: 未知 tag");
                }
            }
            RequireCompleteMask(nameof(EventKind.CaseClosed), seen, (1u << 4) - 1);
            r.EnsureFullyConsumed();
            return new CaseClosedPayload(patientId, caseId, new DiseaseIdSet(bits), treated);
        }

        // ── 16. PatternRecognized(冻结三元组)─────────────────────────────
        // tag: 1 PatientId · 2 AnchorCase · 3 SaltedKey(u64)· 4 MemberDiseaseSet(u64)· 5 Version

        public static byte[] Encode(in PatternRecognizedPayload p)
        {
            var w = new CodecWriter();
            w.WriteFieldInt32(1, p.PatientId);
            w.WriteFieldCaseId(2, p.AnchorCase);
            w.WriteFieldUInt64(3, p.SaltedKey);
            w.WriteFieldUInt64(4, p.MemberDiseaseSet.Bits);
            w.WriteFieldInt32(5, p.Version);
            return w.ToArray();
        }

        private static PatternRecognizedPayload DecodePatternRecognized(ReadOnlySpan<byte> bytes)
        {
            var r = new CodecReader(bytes);
            uint seen = 0;
            int patientId = 0, version = 0;
            CaseId anchorCase = default;
            ulong saltedKey = 0, memberBits = 0;
            while (r.HasMore)
            {
                switch (ReadTagChecked(ref r, nameof(EventKind.PatternRecognized), ref seen))
                {
                    case 1: patientId = r.ReadInt32LittleEndian(); break;
                    case 2: anchorCase = r.ReadFieldCaseId(); break;
                    case 3: saltedKey = r.ReadUInt64LittleEndian(); break;
                    case 4: memberBits = r.ReadUInt64LittleEndian(); break;
                    case 5: version = r.ReadInt32LittleEndian(); break;
                    default: throw new InvalidDataException($"PatternRecognized: 未知 tag");
                }
            }
            RequireCompleteMask(nameof(EventKind.PatternRecognized), seen, (1u << 5) - 1);
            r.EnsureFullyConsumed();
            return new PatternRecognizedPayload(patientId, anchorCase, saltedKey, new DiseaseIdSet(memberBits), version);
        }

        // ── 17. JudgmentRecorded ──────────────────────────────────────────
        // tag: 1 PatientId · 2 CaseId · 3 AuthorPlayerId · 4 LexiconId · 5 Confidence(u8)
        //      6 freehand_text(变长 UTF-8,G-3)

        public static byte[] Encode(in JudgmentRecordedPayload p, string freehandText)
        {
            var w = new CodecWriter();
            w.WriteFieldInt32(1, p.PatientId);
            w.WriteFieldCaseId(2, p.CaseId);
            w.WriteFieldInt32(3, p.AuthorPlayerId);
            w.WriteFieldInt32(4, p.LexiconId);
            w.WriteFieldByte(5, p.Confidence);
            w.WriteFieldUtf8(6, freehandText);
            return w.ToArray();
        }

        private static JudgmentRecordedPayload DecodeJudgmentRecorded(ReadOnlySpan<byte> bytes)
        {
            var r = new CodecReader(bytes);
            uint seen = 0;
            int patientId = 0, authorPlayerId = 0, lexiconId = 0;
            CaseId caseId = default;
            byte confidence = 0;
            while (r.HasMore)
            {
                switch (ReadTagChecked(ref r, nameof(EventKind.JudgmentRecorded), ref seen))
                {
                    case 1: patientId = r.ReadInt32LittleEndian(); break;
                    case 2: caseId = r.ReadFieldCaseId(); break;
                    case 3: authorPlayerId = r.ReadInt32LittleEndian(); break;
                    case 4: lexiconId = r.ReadInt32LittleEndian(); break;
                    case 5: confidence = r.ReadByte(); break;
                    case 6: r.ReadFieldUtf8(); break;   // 读出即弃 —— sim 面无字段(违例面在消费)
                    default: throw new InvalidDataException($"JudgmentRecorded: 未知 tag");
                }
            }
            RequireCompleteMask(nameof(EventKind.JudgmentRecorded), seen, (1u << 6) - 1);
            r.EnsureFullyConsumed();
            return new JudgmentRecordedPayload(patientId, caseId, authorPlayerId, lexiconId, confidence);
        }

        // ── 18. JudgmentRevised ───────────────────────────────────────────
        // tag 序同 JudgmentRecorded。

        public static byte[] Encode(in JudgmentRevisedPayload p, string freehandText)
        {
            var w = new CodecWriter();
            w.WriteFieldInt32(1, p.PatientId);
            w.WriteFieldCaseId(2, p.CaseId);
            w.WriteFieldInt32(3, p.AuthorPlayerId);
            w.WriteFieldInt32(4, p.LexiconId);
            w.WriteFieldByte(5, p.Confidence);
            w.WriteFieldUtf8(6, freehandText);
            return w.ToArray();
        }

        private static JudgmentRevisedPayload DecodeJudgmentRevised(ReadOnlySpan<byte> bytes)
        {
            var r = new CodecReader(bytes);
            uint seen = 0;
            int patientId = 0, authorPlayerId = 0, lexiconId = 0;
            CaseId caseId = default;
            byte confidence = 0;
            while (r.HasMore)
            {
                switch (ReadTagChecked(ref r, nameof(EventKind.JudgmentRevised), ref seen))
                {
                    case 1: patientId = r.ReadInt32LittleEndian(); break;
                    case 2: caseId = r.ReadFieldCaseId(); break;
                    case 3: authorPlayerId = r.ReadInt32LittleEndian(); break;
                    case 4: lexiconId = r.ReadInt32LittleEndian(); break;
                    case 5: confidence = r.ReadByte(); break;
                    case 6: r.ReadFieldUtf8(); break;   // 读出即弃 —— sim 面无字段
                    default: throw new InvalidDataException($"JudgmentRevised: 未知 tag");
                }
            }
            RequireCompleteMask(nameof(EventKind.JudgmentRevised), seen, (1u << 6) - 1);
            r.EnsureFullyConsumed();
            return new JudgmentRevisedPayload(patientId, caseId, authorPlayerId, lexiconId, confidence);
        }
    }
}
