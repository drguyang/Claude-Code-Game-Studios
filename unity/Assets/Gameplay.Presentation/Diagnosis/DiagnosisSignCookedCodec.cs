// 权威来源:ADR-014 §二/§五(运行期读烘焙产物,零 JSON 解析;E-13 启动期硬失败口径)
//          · CookedFormat.cs(布局共参照)· Story 002(词条表读面;与
//            Editor.Tools.Bake.DiagnosisSignCookedWriter **严格镜像** —— 改一处必须改两处)
//
// ⚠️ 任何字段缺失 / 魔数不符 / 长度不符 / 版本不匹配 ⇒ 显式 throw(启动期硬失败,E-13 口径),
//    绝不返回默认值,绝不静默降级。
//
// ⚠️ 住前缀命名空间 —— 受 DiagnosisBoundaryGates 源层谓词约束:
//    · **无 `System.IO` 词面**(D-PERSIST)⇒ 不用 `BinaryReader` / `InvalidDataException`
//      (后者恰住 System.IO 命名空间)⇒ 改**手动字节游标** + `InvalidOperationException`
//      带 `[E-13]` 上下文(与 `AddressablesDataProvider.FetchBytes` 同款硬失败形状);
//    · **零 `Fix` 构造 / 调用**(D-FIX IL 含 `.ctor`)⇒ `neg_weight` 读作 raw `long?`。
//
// ⚠️ 全部字段 = 整数 / 枚举序数 / 布尔 / 长度前缀 UTF-8 字符串,零解析器。

using System;
using System.Collections.Generic;
using System.Text;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Presentation.Diagnosis
{
    /// <summary>词条表烘焙产物的读方(与 <c>DiagnosisSignCookedWriter</c> 镜像)。
    /// <para>手动 little-endian 字节游标;失败即 throw(E-13)。</para>
    /// <example><code>var ds = DiagnosisSignCookedCodec.Read(bytes); // 失败即 throw</code></example>
    /// </summary>
    public static class DiagnosisSignCookedCodec
    {
        /// <summary>读方认领的 <c>schema_version</c>(源恰为此值,否则 = ADR-014 §五「版本不匹配
        /// 硬失败」⇒ E-13 拒收;升级 schema 须同步改本常量与绑定层)。</summary>
        public const uint ExpectedSchemaVersion = 1u;

        /// <summary>单行最小字节数(全部长度前缀取最小 4 B、零揭示法、零负权重:
        /// 4+1+12+1+1+4+1+1 = 25)—— <c>count</c> 钳制下界的量纲,防伪造计数触发巨量分配)。</summary>
        public const int MinRowBytes = 25;

        /// <summary>读词条行集(含 <c>ConfigVersion</c> / <c>SchemaVersion</c> 与行序)。</summary>
        /// <exception cref="InvalidOperationException">头部损坏 / 版本不匹配 / 载荷长度不符 ——
        /// 启动期硬失败(消息带 [E-13] 上下文)。</exception>
        public static DiagnosisSignDataSet Read(byte[] bytes)
        {
            uint configVersion;
            uint schemaVersion;
            int pos = ValidateHeader(bytes, out configVersion, out schemaVersion);

            int count = ReadI32(bytes, ref pos);
            if (count < 0)
                throw E13("计数为 null 标记 —— 产物损坏,启动期硬失败");
            // 钳制:计数 × 单行最小尺寸仍装不进剩余载荷 ⇒ 伪造 / 损坏计数,
            // 必须在 new List(count) 分配之前拒(否则先巨额分配、后逐行越界)。
            if ((long)count * MinRowBytes > bytes.Length - pos)
                throw E13($"计数={count} × 最小行 {MinRowBytes} B 超出剩余载荷 {bytes.Length - pos} B" +
                          " —— 伪造计数,产物损坏");

            var rows = new List<SignLexemeRow>(count);
            for (int i = 0; i < count; i++)
                rows.Add(ReadRow(bytes, ref pos));

            if (pos != bytes.Length)
                throw E13($"载荷尾部不齐(读至 {pos},共 {bytes.Length})—— 产物损坏");

            return new DiagnosisSignDataSet
            {
                Rows = rows,
                ConfigVersion = configVersion,
                SchemaVersion = schemaVersion,
            };
        }

        private static SignLexemeRow ReadRow(byte[] b, ref int pos)
        {
            string signId = ReadString(b, ref pos);

            byte displayCount = ReadU8(b, ref pos);
            if (displayCount != 3)
                throw E13($"display_词 槽位数={displayCount} ≠ 3 —— 产物损坏(R-8.1 三档齐全)");
            var words = new string[3];
            for (int i = 0; i < 3; i++)
                words[i] = ReadString(b, ref pos);

            // 枚举序数界(闭环集守卫:序数超上界 = 产物损坏 —— 不做就等于把闭集校验放行到呈现层)
            byte channelRaw = ReadU8(b, ref pos);
            if (channelRaw > (byte)SignChannel.History)
                throw E13($"channel 序数={channelRaw} > 闭集上界 {(byte)SignChannel.History} —— 产物损坏");
            var channel = (SignChannel)channelRaw;

            byte revealCount = ReadU8(b, ref pos);
            var reveal = new RevealMethod[revealCount];
            for (int i = 0; i < revealCount; i++)
            {
                byte revealRaw = ReadU8(b, ref pos);
                if (revealRaw > (byte)RevealMethod.Inquiry)
                    throw E13($"reveal_by[{i}] 序数={revealRaw} > 闭集上界 " +
                              $"{(byte)RevealMethod.Inquiry} —— 产物损坏");
                reveal[i] = (RevealMethod)revealRaw;
            }

            int tier = ReadI32(b, ref pos);
            byte polarityRaw = ReadU8(b, ref pos);
            if (polarityRaw > (byte)SignPolarity.Negative)
                throw E13($"polarity 序数={polarityRaw} > 闭集上界 {(byte)SignPolarity.Negative}" +
                          " —— 产物损坏");
            var polarity = (SignPolarity)polarityRaw;

            byte hasWeight = ReadU8(b, ref pos);
            long? negRaw = null;
            if (hasWeight == 1)
                negRaw = ReadI64(b, ref pos);
            else if (hasWeight != 0)
                throw E13($"neg_weight 有无标志={hasWeight} ∉ {{0,1}} —— 产物损坏");

            return new SignLexemeRow(signId, words, channel, reveal, tier, polarity, negRaw);
        }

        // ── 头部校验(20 B:DYJC + formatVersion + schemaVersion + configVersion + payloadLength)──

        private static int ValidateHeader(byte[] bytes, out uint configVersion, out uint schemaVersion)
        {
            configVersion = 0;
            schemaVersion = 0;

            if (bytes == null || bytes.Length < CookedFormat.HeaderSize)
                throw E13($"字节不足头部({CookedFormat.HeaderSize} B)—— 产物损坏");

            if (bytes[0] != (byte)'D' || bytes[1] != (byte)'Y' ||
                bytes[2] != (byte)'J' || bytes[3] != (byte)'C')
                throw E13("魔数不符(期待 \"DYJC\")—— 拒收");

            uint formatVersion = ReadU32At(bytes, 4);
            if (formatVersion > CookedFormat.FormatVersion)
                throw E13($"formatVersion={formatVersion} > 读方 {CookedFormat.FormatVersion}" +
                          " —— 读方过旧,拒收");

            schemaVersion = ReadU32At(bytes, 8);
            if (schemaVersion != ExpectedSchemaVersion)
                throw E13($"schemaVersion={schemaVersion} ≠ 读方认领 {ExpectedSchemaVersion}" +
                          " —— 版本不匹配,拒收(ADR-014 §五)");
            configVersion = ReadU32At(bytes, 12);
            uint payloadLength = ReadU32At(bytes, 16);
            if (bytes.Length - CookedFormat.HeaderSize != payloadLength)
                throw E13($"载荷长度不符(头声明 {payloadLength}," +
                          $"实际 {bytes.Length - CookedFormat.HeaderSize})—— 产物损坏");

            return CookedFormat.HeaderSize;
        }

        // ── 游标读件(手动 little-endian;越界 = E-13)──

        private static byte ReadU8(byte[] b, ref int pos)
        {
            if (pos + 1 > b.Length) throw E13("读 u8 越界 —— 产物损坏");
            return b[pos++];
        }

        private static int ReadI32(byte[] b, ref int pos)
        {
            if (pos + 4 > b.Length) throw E13("读 i32 越界 —— 产物损坏");
            int v = b[pos] | (b[pos + 1] << 8) | (b[pos + 2] << 16) | (b[pos + 3] << 24);
            pos += 4;
            return v;
        }

        private static long ReadI64(byte[] b, ref int pos)
        {
            if (pos + 8 > b.Length) throw E13("读 i64 越界 —— 产物损坏");
            ulong lo = (uint)(b[pos] | (b[pos + 1] << 8) | (b[pos + 2] << 16) | (b[pos + 3] << 24));
            int hiStart = pos + 4;
            ulong hi = (uint)(b[hiStart] | (b[hiStart + 1] << 8) |
                              (b[hiStart + 2] << 16) | (b[hiStart + 3] << 24));
            pos += 8;
            return (long)((hi << 32) | lo);
        }

        /// <summary>长度前缀 UTF-8 字符串(写方 <c>WriteString</c>:−1 = null)。</summary>
        private static string ReadString(byte[] b, ref int pos)
        {
            int len = ReadI32(b, ref pos);
            if (len == -1) return null;
            if (len < -1) throw E13($"字符串长度={len} 非法 —— 产物损坏");
            if (pos + len > b.Length) throw E13("读字符串越界 —— 产物损坏");
            string s = Encoding.UTF8.GetString(b, pos, len);
            pos += len;
            return s;
        }

        private static uint ReadU32At(byte[] b, int offset) =>
            (uint)(b[offset] | (b[offset + 1] << 8) | (b[offset + 2] << 16) | (b[offset + 3] << 24));

        private static InvalidOperationException E13(string what) =>
            new InvalidOperationException($"[E-13] diagnosis_signs cooked:{what}");
    }

    /// <summary>词条表烘焙产物的解码形(<c>diagnosis_signs.cooked.bytes</c>)。
    /// <para><see cref="ConfigVersion"/> = 源数据集内容哈希派生(u32,ADR-014 §五)。</para></summary>
    public struct DiagnosisSignDataSet
    {
        /// <summary>全量词条行(序 = 源 JSON 序;校验通过后的行集)。</summary>
        public List<SignLexemeRow> Rows;

        /// <summary>烘焙时从源 JSON 文本派生的内容版本号(u32)。</summary>
        public uint ConfigVersion;

        /// <summary>烘焙时随产物落盘的 schema 版本(源 <c>schema_version</c> 字段)。</summary>
        public uint SchemaVersion;
    }
}
