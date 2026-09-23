// R-1(U0-b 残留)· 定界读端 —— ADR-010 §Key Interfaces 点名的 CodecReader 类型,
//   本批为其**首个定义件**(同 CodecWriter 头注:ISaveCodec 接口本体归 7a 轮)。
//
// 权威来源:ADR-010 §一 · ADR-006 §五(按字段名/tag,禁位置)。
//
// 严格性口径(损坏必被发现,不静默容错):
//   · 未知 tag / 重复 tag / 缺字段 / 尾随残留字节 ⇒ InvalidDataException;
//   · 数组 count 先对**剩余字节**复验再分配(防损坏 count 触发巨额分配);
//   · UTF-8 非法字节 ⇒ 抛(CodecPrimitives.StrictUtf8)。
//   文件级损坏恢复(校验和 + 自动回退)是 ADR-010 §四 的**外层**职责;本层只保证
//   「进了解码器的字节要么合法要么立刻炸」,两层不互相替代。

using System;
using System.IO;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.Codec
{
    /// <summary>定界字节读端(ref struct —— 持有 ReadOnlySpan,按 ADR-010 签名形按 ref 传递)。</summary>
    public ref struct CodecReader
    {
        private readonly ReadOnlySpan<byte> _src;
        private int _pos;

        public CodecReader(ReadOnlySpan<byte> src)
        {
            _src = src;
            _pos = 0;
        }

        public int Position => _pos;
        public int Length => _src.Length;
        public int Remaining => _src.Length - _pos;
        public bool HasMore => _pos < _src.Length;

        private void EnsureAvailable(int count)
        {
            if (count > Remaining)
                throw new InvalidDataException(
                    $"CodecReader:读 {count} 字节越界(剩 {Remaining})—— 段长与内容不符或已损坏");
        }

        // ── 原始小端读 ──

        public byte ReadByte()
        {
            EnsureAvailable(1);
            return _src[_pos++];
        }

        public int ReadInt32LittleEndian()
        {
            EnsureAvailable(4);
            int v = CodecPrimitives.ReadInt32LittleEndian(_src.Slice(_pos, 4));
            _pos += 4;
            return v;
        }

        public long ReadInt64LittleEndian()
        {
            EnsureAvailable(8);
            long v = CodecPrimitives.ReadInt64LittleEndian(_src.Slice(_pos, 8));
            _pos += 8;
            return v;
        }

        public ulong ReadUInt64LittleEndian()
        {
            EnsureAvailable(8);
            ulong v = CodecPrimitives.ReadUInt64LittleEndian(_src.Slice(_pos, 8));
            _pos += 8;
            return v;
        }

        // ── tag 字段读(规范路径)──

        public byte ReadTag() => ReadByte();

        public bool ReadFieldBoolean()
        {
            byte b = ReadByte();
            if (b > 1) throw new InvalidDataException($"bool 字段值 {b} 非 0/1 —— 已损坏");
            return b == 1;
        }

        /// <summary>Q16.16 定点字段 —— 走 FixCodec(D-21-18 唯一读入口)。</summary>
        public Fix ReadFieldFix() => FixCodec.Read(ref this);

        public WorldPos ReadFieldWorldPos()
        {
            int x = ReadInt32LittleEndian();
            int y = ReadInt32LittleEndian();
            int z = ReadInt32LittleEndian();
            return new WorldPos(x, y, z);
        }

        public CaseId ReadFieldCaseId()
        {
            long tick = ReadInt64LittleEndian();
            int patient = ReadInt32LittleEndian();
            long seq = ReadInt64LittleEndian();
            return new CaseId(tick, patient, seq);
        }

        public PayloadRef ReadFieldPayloadRef()
        {
            int blobId = ReadInt32LittleEndian();
            int offset = ReadInt32LittleEndian();
            int length = ReadInt32LittleEndian();
            return new PayloadRef(blobId, offset, length);
        }

        /// <summary>int 数组 = count + 元素。count&lt;0 或 count×4 &gt; 剩余字节 ⇒ 抛(分配前复验)。</summary>
        public int[] ReadFieldArrayInt32()
        {
            int count = ReadInt32LittleEndian();
            if (count < 0)
                throw new InvalidDataException($"数组 count={count} 为负 —— 已损坏");
            long needed = (long)count * 4;
            if (needed > Remaining)
                throw new InvalidDataException(
                    $"数组 count={count} 需 {needed} 字节,仅剩 {Remaining} —— 已损坏");
            var arr = new int[count];
            for (int i = 0; i < count; i++) arr[i] = ReadInt32LittleEndian();
            return arr;
        }

        /// <summary>long 数组 = count + 元素(count 复验同上)。</summary>
        public long[] ReadFieldArrayInt64()
        {
            int count = ReadInt32LittleEndian();
            if (count < 0)
                throw new InvalidDataException($"数组 count={count} 为负 —— 已损坏");
            long needed = (long)count * 8;
            if (needed > Remaining)
                throw new InvalidDataException(
                    $"数组 count={count} 需 {needed} 字节,仅剩 {Remaining} —— 已损坏");
            var arr = new long[count];
            for (int i = 0; i < count; i++) arr[i] = ReadInt64LittleEndian();
            return arr;
        }

        /// <summary>变长 UTF-8 = byteLen + 字节。byteLen 复验后经严格 UTF-8 解码。</summary>
        public string ReadFieldUtf8()
        {
            int byteLen = ReadInt32LittleEndian();
            if (byteLen < 0)
                throw new InvalidDataException($"UTF-8 段长 {byteLen} 为负 —— 已损坏");
            if (byteLen > Remaining)
                throw new InvalidDataException(
                    $"UTF-8 段长 {byteLen} 超剩余 {Remaining} —— 已损坏");
            if (byteLen == 0) return string.Empty;
            string s = CodecPrimitives.StrictUtf8.GetString(_src.Slice(_pos, byteLen));
            _pos += byteLen;
            return s;
        }

        /// <summary>段必须恰好读尽 —— 尾随字节 = 结构与段长不符(损坏或 schema 漂移)。</summary>
        public void EnsureFullyConsumed()
        {
            if (HasMore)
                throw new InvalidDataException(
                    $"尾随 {Remaining} 字节未被任何字段消费 —— 段长错误或未知结构");
        }
    }
}
