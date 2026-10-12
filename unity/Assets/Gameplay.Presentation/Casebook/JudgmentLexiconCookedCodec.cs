// sprint-08 S1.0 · 病名册词表 cooked 读方(与 Editor.Tools.Bake.JudgmentLexiconCookedWriter 严格镜像)
//
// ⚠️ 手动 little-endian 字节游标;任何缺失/魔数不符/长度不符/版本不匹配 ⇒ 显式 throw
//   (E-13 口径:启动期硬失败,零默认值、零静默降级)。
// ⚠️ 零 System.IO 词面(与 DiagnosisSignCookedCodec 同纪律)。

using System;
using System.Collections.Generic;
using System.Text;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Presentation.Casebook
{
    /// <summary>病名册词表烘焙产物的读方(写方 = <c>Editor.Tools.Bake.JudgmentLexiconCookedWriter</c>)。
    /// <para>载荷布局:行数 i32 → 逐行(lexicon_id i32 · text 长度前缀 UTF-8 ·
    /// disease_count u8 + N × i32)。</para></summary>
    public static class JudgmentLexiconCookedCodec
    {
        /// <summary>读方认领的 schema_version(不匹配 = E-13 拒收)。</summary>
        public const uint ExpectedSchemaVersion = 1u;

        /// <summary>单行最小字节数(lexicon_id 4 + 空 text 4 + disease_count 1 = 9)。</summary>
        public const int MinRowBytes = 9;

        /// <summary>读词表数据集(行集 + ConfigVersion / SchemaVersion)。</summary>
        /// <exception cref="InvalidOperationException">头部损坏 / 版本不匹配 / 载荷不齐 —— E-13 硬失败。</exception>
        public static JudgmentLexiconDataSet Read(byte[] bytes)
        {
            int pos = ValidateHeader(bytes, out uint configVersion, out uint schemaVersion);

            int count = ReadI32(bytes, ref pos);
            if (count < 0)
                throw E13("计数为 null 标记 —— 产物损坏");
            if ((long)count * MinRowBytes > bytes.Length - pos)
                throw E13($"计数={count} × 最小行 {MinRowBytes} B 超出剩余载荷 —— 伪造计数");

            var rows = new List<JudgmentLexemeRow>(count);
            for (int i = 0; i < count; i++)
                rows.Add(ReadRow(bytes, ref pos));

            if (pos != bytes.Length)
                throw E13($"载荷尾部不齐(读至 {pos},共 {bytes.Length})");

            return new JudgmentLexiconDataSet
            {
                Rows = rows,
                ConfigVersion = configVersion,
                SchemaVersion = schemaVersion,
            };
        }

        private static JudgmentLexemeRow ReadRow(byte[] b, ref int pos)
        {
            int lexiconId = ReadI32(b, ref pos);
            string text = ReadString(b, ref pos);
            byte diseaseCount = ReadU8(b, ref pos);
            var diseaseIds = new int[diseaseCount];
            for (int i = 0; i < diseaseCount; i++)
                diseaseIds[i] = ReadI32(b, ref pos);
            return new JudgmentLexemeRow(lexiconId, text, diseaseIds);
        }

        private static int ValidateHeader(byte[] bytes, out uint configVersion, out uint schemaVersion)
        {
            configVersion = 0;
            schemaVersion = 0;
            if (bytes == null || bytes.Length < CookedFormat.HeaderSize)
                throw E13("产物短于头部 —— 损坏");
            if (bytes[0] != 'D' || bytes[1] != 'Y' || bytes[2] != 'J' || bytes[3] != 'C')
                throw E13("魔数不符(≠ DYJC)—— 损坏");
            uint formatVersion = ReadU32(bytes, 4);
            if (formatVersion > CookedFormat.FormatVersion)
                throw E13($"formatVersion={formatVersion} > 读方 {CookedFormat.FormatVersion} —— 读方过旧");
            schemaVersion = ReadU32(bytes, 8);
            if (schemaVersion != ExpectedSchemaVersion)
                throw E13($"schemaVersion={schemaVersion} ≠ {ExpectedSchemaVersion} —— 版本不匹配");
            configVersion = ReadU32(bytes, 12);
            uint payloadLength = ReadU32(bytes, 16);
            if (payloadLength != bytes.Length - CookedFormat.HeaderSize)
                throw E13($"payloadLength={payloadLength} ≠ 实际 {bytes.Length - CookedFormat.HeaderSize}");
            return CookedFormat.HeaderSize;
        }

        private static string ReadString(byte[] b, ref int pos)
        {
            int len = ReadI32(b, ref pos);
            if (len == -1) return null;
            if (len < 0 || pos + len > b.Length)
                throw E13($"字符串长度 {len} 越界 —— 损坏");
            string s = Encoding.UTF8.GetString(b, pos, len);
            pos += len;
            return s;
        }

        private static byte ReadU8(byte[] b, ref int pos)
        {
            if (pos + 1 > b.Length) throw E13("u8 越界");
            return b[pos++];
        }

        private static int ReadI32(byte[] b, ref int pos)
        {
            if (pos + 4 > b.Length) throw E13("i32 越界");
            int v = b[pos] | (b[pos + 1] << 8) | (b[pos + 2] << 16) | (b[pos + 3] << 24);
            pos += 4;
            return v;
        }

        private static uint ReadU32(byte[] b, int pos)
            => (uint)ReadI32(b, ref pos);

        private static InvalidOperationException E13(string detail)
            => new InvalidOperationException($"[E-13][judgment_lexicon] {detail}");
    }
}
