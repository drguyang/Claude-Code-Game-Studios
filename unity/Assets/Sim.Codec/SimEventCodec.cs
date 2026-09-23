// R-1(U0-b 残留)· SimEvent 头编解码(header/blob 分家的 header 半边)。
//
// 权威来源:
//   ADR-006 §五 —— 「SimEvent 一类值 struct……**按字段名编码,不得按字段位置编码**」;
//     ADR-010 §一同款(proto 风格字段表,禁位置打包)。
//   ADR-006 Amendment G-1 —— 字段序抄本单一化:**唯一权威序 = {Tick, Patient, Seq,
//     Kind, Payload}**;本文件的 tag 分配即该序的 1..5(tag = 声明序,升序落笔)。
//   ADR-006 Amendment G-2 —— Payload 字段类型 = PayloadRef(BlobId, Offset, Length
//     全整数内联);载荷字节在池中,本 codec 只编解 header。
//   b1b 残留 R-1 行 —— 「解码器本体……PayloadRef 的 blob 池契约」。
//
// 位置编码的失效模式(ADR-006 §五 原文):同一 struct 两种抄本写出两种字节流,
//   长度对得上、值**静默错位** —— tag 化后字段重排不再改字节面。

using System;
using System.IO;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.Codec
{
    /// <summary>三流事件**头部**的编解码(载荷字节经 IBlobPool / PayloadCodec 另行寻址)。</summary>
    public static class SimEventCodec
    {
        // tag 表(Amendment G-1 权威序):1 Tick · 2 Patient · 3 Seq · 4 Kind · 5 Payload。
        private const byte TagTick = 1;
        private const byte TagPatient = 2;
        private const byte TagSeq = 3;
        private const byte TagKind = 4;
        private const byte TagPayload = 5;
        private const uint ExpectedMask = (1u << 5) - 1;   // 全五字段各一

        /// <summary>按 tag 升序(canonical)写出头字节段。载荷内容不在本段内。</summary>
        public static byte[] Encode(in SimEvent e)
        {
            var w = new CodecWriter();
            w.WriteFieldInt64(TagTick, e.Tick);
            w.WriteFieldInt32(TagPatient, e.Patient.Value);
            w.WriteFieldInt64(TagSeq, e.Seq);
            w.WriteFieldInt32(TagKind, (int)e.Kind);
            w.WriteFieldPayloadRef(TagPayload, e.Payload);
            return w.ToArray();
        }

        /// <summary>
        /// 严格解码:全五 tag 恰各一(缺 / 重 / 未知 ⇒ 抛)、Kind ∈ 枚举闭集、
        /// PayloadRef.Offset/Length ≥ 0(负值使池切片无意义)。段须恰好读尽。
        /// </summary>
        public static SimEvent Decode(ReadOnlySpan<byte> headerBytes)
        {
            var r = new CodecReader(headerBytes);

            long tick = 0, seq = 0;
            int patient = 0, kindRaw = 0;
            PayloadRef payload = default;
            uint seen = 0;

            while (r.HasMore)
            {
                byte tag = r.ReadTag();
                if (tag < 1 || tag > 5)
                    throw new InvalidDataException($"SimEvent 未知字段 tag={tag}");

                uint bit = 1u << (tag - 1);
                if ((seen & bit) != 0)
                    throw new InvalidDataException($"SimEvent 字段 tag={tag} 重复");
                seen |= bit;

                switch (tag)
                {
                    case TagTick:   tick = r.ReadInt64LittleEndian(); break;
                    case TagPatient: patient = r.ReadInt32LittleEndian(); break;
                    case TagSeq:    seq = r.ReadInt64LittleEndian(); break;
                    case TagKind:   kindRaw = r.ReadInt32LittleEndian(); break;
                    case TagPayload: payload = r.ReadFieldPayloadRef(); break;
                }
            }

            if (seen != ExpectedMask)
                throw new InvalidDataException(
                    $"SimEvent 缺字段(已见 mask=0x{seen:X},应为 0x{ExpectedMask:X})");
            r.EnsureFullyConsumed();

            if (!Enum.IsDefined(typeof(EventKind), kindRaw))
                throw new InvalidDataException($"SimEvent.Kind={kindRaw} 不在 34 支闭集内");
            if (payload.Offset < 0 || payload.Length < 0)
                throw new InvalidDataException(
                    $"PayloadRef 越界值(Offset={payload.Offset}, Length={payload.Length})");

            return new SimEvent(tick, new PatientId(patient), seq, (EventKind)kindRaw, payload);
        }
    }
}
