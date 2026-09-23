// R-1(U0-b 残留)· Sim.Codec 字节原语 —— 显式小端。
//
// 权威来源:
//   ADR-010 §一 —— 「现行平台全小端……但须**显式声明小端**并写黄金夹具钉死(编辑器 vs
//     IL2CPP 产出相同字节)」—— BinaryPrimitives 的端序参数即该显式声明;
//     跨平台逐位等价本身是 ADR-012 矩阵的实测对象,**不在本文件借绿**。
//   ADR-006 §五(D-21-18)—— 自定义编码器按字段名,禁位置打包(tag 约定见 PayloadCodec)。
//   ADR-025 §① —— Sim.Codec 引用集 = BCL + Sim.Contracts;本文件仅用 System.*。
//
// 2026-09-23 · b5 的 8 字节小端 helper 迁移落点:种子测试(sim_fixedpoint_test.cs)
//   的本地手写 loop 自本批起改调本类(生产件);独立参考 loop 移入 sim_codec_roundtrip_test.cs
//   与本类对拍(等价性 = 单元级断言,跨平台格仍归 ADR-012)。

using System;
using System.Buffers.Binary;
using System.Text;

namespace DaYiJingCheng.Sim.Codec
{
    /// <summary>小端字节读写原语(internal —— 测试经 InternalsVisibleTo("Sim.Contracts.Tests") 访问)。</summary>
    internal static class CodecPrimitives
    {
        /// <summary>严格 UTF-8(禁 BOM · 非法字节抛异常)—— 变长段(freehand_text)的唯一编解码器。
        /// 替换字符式宽容解码会把损坏静默成不同文本,违背「损坏必被发现」取向。</summary>
        internal static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(
            encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

        internal static void WriteInt32LittleEndian(Span<byte> dst, int value)
            => BinaryPrimitives.WriteInt32LittleEndian(dst, value);

        internal static int ReadInt32LittleEndian(ReadOnlySpan<byte> src)
            => BinaryPrimitives.ReadInt32LittleEndian(src);

        internal static void WriteInt64LittleEndian(Span<byte> dst, long value)
            => BinaryPrimitives.WriteInt64LittleEndian(dst, value);

        internal static long ReadInt64LittleEndian(ReadOnlySpan<byte> src)
            => BinaryPrimitives.ReadInt64LittleEndian(src);

        internal static void WriteUInt64LittleEndian(Span<byte> dst, ulong value)
            => BinaryPrimitives.WriteUInt64LittleEndian(dst, value);

        internal static ulong ReadUInt64LittleEndian(ReadOnlySpan<byte> src)
            => BinaryPrimitives.ReadUInt64LittleEndian(src);
    }
}
