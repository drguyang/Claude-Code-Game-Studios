// ADR-010 §一 —— ISaveCodec 默认实现。
//
// 权威来源:
//   ADR-010 §一 —— 全二进制 codec,按字段名/tag 编码
//   ADR-006 §五 —— Fix 经自定义编码器
//   SimEventCodec —— 头部编解码
//   FixCodec —— Fix 编解码

using System;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.Codec
{
    /// <summary>ISaveCodec 默认实现。按字段名/tag 编码,禁位置打包。</summary>
    public sealed class SaveCodec : ISaveCodec
    {
        public void WriteEvent(in SimEvent e, ref CodecWriter w)
        {
            byte[] header = SimEventCodec.Encode(e);
            w.WriteInt32LittleEndian(header.Length);
            w.WriteBytes(header);
        }

        public SimEvent ReadEvent(ref CodecReader r)
        {
            // 读取长度前缀,然后解码 header 字节段
            int length = r.ReadInt32LittleEndian();
            byte[] headerBytes = r.ReadBytes(length);
            return SimEventCodec.Decode(headerBytes);
        }

        public void WriteFix(Fix v, ref CodecWriter w)
        {
            FixCodec.Write(ref w, v);
        }

        public Fix ReadFix(ref CodecReader r)
        {
            return FixCodec.Read(ref r);
        }
    }
}
