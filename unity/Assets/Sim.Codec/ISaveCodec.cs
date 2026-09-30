// ADR-010 §一 —— ISaveCodec 接口定义。
//
// 权威来源:
//   ADR-010 §一 —— 全二进制 codec,按字段名/tag 编码,禁位置打包
//   ADR-006 §五 —— Fix 经自定义编码器,禁 Unity 内置序列化器
//   ADR-010 §三 —— 义务 3:三流序列化 + 禁 float

using System;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.Codec
{
    /// <summary>存档编码器接口。按字段名/tag 编码,禁位置打包。</summary>
    public interface ISaveCodec
    {
        /// <summary>按字段名/tag 编码 SimEvent。</summary>
        void WriteEvent(in SimEvent e, ref CodecWriter w);

        /// <summary>严格解码 SimEvent。</summary>
        SimEvent ReadEvent(ref CodecReader r);

        /// <summary>Fix 经自定义编码器写出(_raw 的 long,显式小端)。</summary>
        void WriteFix(Fix v, ref CodecWriter w);

        /// <summary>Fix 经自定义编码器读入。</summary>
        Fix ReadFix(ref CodecReader r);
    }
}
