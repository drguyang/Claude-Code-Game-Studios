// R-1(U0-b 残留)· 可增长写字端 —— ADR-010 §Key Interfaces 点名的 CodecWriter 类型,
//   本批为其**首个定义件**(此前全库只有引用无定义,同 R-4「引用却无登记」的先行解法:
//   类型先落地,ISaveCodec 接口本体归 7a 轮)。
//
// 权威来源:ADR-010 §一(全二进制 · 显式小端 · 按字段名/tag 编码,禁位置打包)
//          · ADR-006 §五(tag 化即「按字段名」判据的实现形)
//
// 结构语义:承载**增量写出**的可变缓冲(byte[] + 游标,指数扩容)。
//   因含可变状态,按 ADR-010 签名形为普通 struct、**按 ref 传递**。
//   tag 字段方法 = 「1 字节 tag + 定宽小端值」;复合值(WorldPos / CaseId / PayloadRef)
//   = tag + 冻结内联布局(其字段序由 Sim.Contracts 的 struct 定义钉死,非本层自由)。

using System;
using System.IO;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.Codec
{
    /// <summary>增量写字端。用法:<c>var w = new CodecWriter(); w.WriteFieldInt32(1, v); ...; w.ToArray()</c>。</summary>
    public struct CodecWriter
    {
        private byte[] _buffer;
        private int _pos;

        public CodecWriter(int initialCapacity)
        {
            _buffer = new byte[initialCapacity < 16 ? 16 : initialCapacity];
            _pos = 0;
        }

        /// <summary>已写字节数(= 产物长度,见 <see cref="ToArray"/>)。</summary>
        public int Length => _pos;

        private void Ensure(int extra)
        {
            // 参数隐式无参构造 = 字段清零(_buffer = null)—— struct 的无参构造不可免,
            // 首写必须能从 null 起步(Array.Resize 对 null 源即新建)。
            if (_buffer != null && _pos + extra <= _buffer.Length) return;
            int newSize = _buffer == null ? 256 : _buffer.Length * 2;
            while (newSize < _pos + extra) newSize *= 2;
            Array.Resize(ref _buffer, newSize);
        }

        // ── 原始小端写(复合值内联段 / 已知定长布局用)──

        public void WriteByte(byte value)
        {
            Ensure(1);
            _buffer[_pos++] = value;
        }

        public void WriteInt32LittleEndian(int value)
        {
            Ensure(4);
            CodecPrimitives.WriteInt32LittleEndian(_buffer.AsSpan(_pos, 4), value);
            _pos += 4;
        }

        public void WriteInt64LittleEndian(long value)
        {
            Ensure(8);
            CodecPrimitives.WriteInt64LittleEndian(_buffer.AsSpan(_pos, 8), value);
            _pos += 8;
        }

        public void WriteUInt64LittleEndian(ulong value)
        {
            Ensure(8);
            CodecPrimitives.WriteUInt64LittleEndian(_buffer.AsSpan(_pos, 8), value);
            _pos += 8;
        }

        // ── tag 字段写(载荷 / SimEvent 头的规范路径:tag + 值原子落笔)──
        // tag 语义:1 起、按 struct 声明序分配、全字段必写(canonical = 升 tag 序)。

        public void WriteFieldByte(byte tag, byte value)
        {
            WriteByte(tag);
            WriteByte(value);
        }

        public void WriteFieldBoolean(byte tag, bool value)
        {
            WriteByte(tag);
            WriteByte(value ? (byte)1 : (byte)0);
        }

        public void WriteFieldInt32(byte tag, int value)
        {
            WriteByte(tag);
            WriteInt32LittleEndian(value);
        }

        public void WriteFieldInt64(byte tag, long value)
        {
            WriteByte(tag);
            WriteInt64LittleEndian(value);
        }

        public void WriteFieldUInt64(byte tag, ulong value)
        {
            WriteByte(tag);
            WriteUInt64LittleEndian(value);
        }

        /// <summary>Q16.16 定点字段 —— 走 FixCodec(D-21-18 命名自定义编码器,唯一落笔口)。</summary>
        public void WriteFieldFix(byte tag, Fix value)
        {
            WriteByte(tag);
            FixCodec.Write(ref this, value);
        }

        /// <summary>整数格(WorldPos 内联 = X, Y, Z 各 i32,序 = struct 声明序)。</summary>
        public void WriteFieldWorldPos(byte tag, WorldPos value)
        {
            WriteByte(tag);
            WriteInt32LittleEndian(value.X);
            WriteInt32LittleEndian(value.Y);
            WriteInt32LittleEndian(value.Z);
        }

        /// <summary>全序键三元组(CaseId 内联 = Tick i64, Patient i32, Seq i64,ADR-006 G-1 序)。</summary>
        public void WriteFieldCaseId(byte tag, CaseId value)
        {
            WriteByte(tag);
            WriteInt64LittleEndian(value.Tick);
            WriteInt32LittleEndian(value.Patient);
            WriteInt64LittleEndian(value.Seq);
        }

        /// <summary>载荷引用(PayloadRef 内联 = BlobId, Offset, Length 各 i32,ADR-006 G-2 形)。</summary>
        public void WriteFieldPayloadRef(byte tag, PayloadRef value)
        {
            WriteByte(tag);
            WriteInt32LittleEndian(value.BlobId);
            WriteInt32LittleEndian(value.Offset);
            WriteInt32LittleEndian(value.Length);
        }

        /// <summary>int 数组字段 = tag + count(i32)+ count×i32。count≥0 由写侧结构保证(读侧复验)。</summary>
        public void WriteFieldArrayInt32(byte tag, int[] values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            WriteByte(tag);
            WriteInt32LittleEndian(values.Length);
            foreach (var v in values) WriteInt32LittleEndian(v);
        }

        /// <summary>long 数组字段 = tag + count(i32)+ count×i64。</summary>
        public void WriteFieldArrayInt64(byte tag, long[] values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            WriteByte(tag);
            WriteInt32LittleEndian(values.Length);
            foreach (var v in values) WriteInt64LittleEndian(v);
        }

        /// <summary>变长 UTF-8 字段 = tag + byteLen(i32)+ 严格 UTF-8 字节(无 BOM)。
        /// 全案唯一使用者 = Judgment.freehand_text(ADR-006 G-3);null 视为空串(空栏合法)。</summary>
        public void WriteFieldUtf8(byte tag, string value)
        {
            byte[] utf8 = value == null
                ? Array.Empty<byte>()
                : CodecPrimitives.StrictUtf8.GetBytes(value);
            WriteByte(tag);
            WriteInt32LittleEndian(utf8.Length);
            Ensure(utf8.Length);
            utf8.CopyTo(_buffer.AsSpan(_pos));
            _pos += utf8.Length;
        }

        /// <summary>拷出已写字节(每次调用分配新数组;调用后 writer 可继续写或弃用)。</summary>
        public byte[] ToArray()
        {
            var result = new byte[_pos];
            Array.Copy(_buffer, result, _pos);
            return result;
        }
    }
}
