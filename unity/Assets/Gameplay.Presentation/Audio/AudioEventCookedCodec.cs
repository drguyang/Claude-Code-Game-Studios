// S14-6 · story-010 —— 音频事件表 cooked 读方(与 `Editor.Tools.Bake.AudioEventTableCookedWriter`
// **严格镜像**;两侧布局分叉由 AudioEventTableBake.Materialize 的跨侧等值断言钉住)。
//
// 权威来源:
//   ADR-014 §五(cooked 装载 · E-13 失败语义 = 启动期硬失败,零默认值、零静默降级)
//   CookedFormat.cs(20 字节头 + 载荷小件规格的唯一共参照)
//   AC-44-D4(玩家构建**零 JSON 解析器** —— 本件只解字节,不碰任何 JSON 类型)
//
// ⚠️ 手动 little-endian 字节游标;任何缺失 / 魔数不符 / 长度不符 / 版本不匹配 / 尾部不齐
//   ⇒ 显式 throw(E-13 口径;承 `JudgmentLexiconCookedCodec` / `DiagnosisSignCookedCodec` 同纪律)。
// ⚠️ **零 `System.IO` 词面**(同前两件)—— 本件不做文件 I/O,字节由 Addressables 侧喂入。
// ⚠️ 布局逐字镜像写方 `AudioEventTableCookedWriter` 的文件头注释;**改任一侧必须同改另一侧**
//   (唯一自动兜现 = `AudioEventTableBake.Materialize` 的真往返 + 跨侧等值断言)。

using System;
using System.Collections.Generic;
using System.Text;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Presentation.Audio
{
    /// <summary>音频事件表烘焙产物的读方(写方 = <c>Editor.Tools.Bake.AudioEventTableCookedWriter</c>)。</summary>
    public static class AudioEventCookedCodec
    {
        /// <summary>读方认领的 schema_version(不匹配 = E-13 拒收)。</summary>
        public const uint ExpectedSchemaVersion = 1u;

        /// <summary>读事件表(行集 + tier_map + ConfigVersion / SchemaVersion)。</summary>
        /// <exception cref="InvalidOperationException">头部损坏 / 版本不匹配 / 载荷不齐 —— E-13 硬失败。</exception>
        public static AudioEventDataSet Read(byte[] bytes)
        {
            int pos = ValidateHeader(bytes, out uint configVersion, out uint headerSchemaVersion);

            uint payloadSchemaVersion = ReadU32(bytes, ref pos);
            if (payloadSchemaVersion != headerSchemaVersion)
                throw E13($"载荷 schema_version={payloadSchemaVersion} ≠ 头槽 {headerSchemaVersion} " +
                          "—— 同一真值两处存且已分叉(写方契约:两者必须相等)");

            uint count = ReadU32(bytes, ref pos);
            if (count > bytes.Length / MinRowBytes)
                throw E13($"行数={count} 超出行下界 {MinRowBytes} B 反推的上界 —— 伪造计数");

            var rows = new AudioEventRow[count];
            for (uint i = 0; i < count; i++)
                rows[i] = ReadRow(bytes, ref pos);

            var tierMap = ReadTierMap(bytes, ref pos);

            if (pos != bytes.Length)
                throw E13($"载荷尾部不齐(读至 {pos},共 {bytes.Length})");

            return new AudioEventDataSet(rows, tierMap, headerSchemaVersion, configVersion);
        }

        /// <summary>单行最小字节数(cue 4 + schemaVersion 4 + loop 1 + 五字符串各 4 + 三计数各 4
        /// + hasPolicy 1 = 34)—— 仅用于「计数伪造」的下界反推。</summary>
        public const int MinRowBytes = 34;

        private static AudioEventRow ReadRow(byte[] b, ref int pos)
        {
            string cue = ReadString(b, ref pos);
            uint schemaVersion = ReadU32(b, ref pos);
            bool loop = ReadU8(b, ref pos) != 0;
            string bus = ReadString(b, ref pos);
            string whitelistCategory = ReadString(b, ref pos);
            string triggerSource = ReadString(b, ref pos);
            string clockRef = ReadString(b, ref pos);
            string subtitleText = ReadString(b, ref pos);
            string xfadeRaw = ReadString(b, ref pos);

            string[] assets = ReadStringArray(b, ref pos);
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> tierParams = ReadNestedMap(b, ref pos);

            bool hasPolicy = ReadU8(b, ref pos) != 0;
            string policyTrigger = hasPolicy ? ReadString(b, ref pos) : null;
            string policyJitter = hasPolicy ? ReadString(b, ref pos) : null;

            // story-020 重裁 ①:变体轴声明(null = 非变体行;写方同序追加)。
            string variantAxis = ReadString(b, ref pos);

            // 🔁 2026-10-29(`TD-audio-035` ③):空间化四列(写方同序追加,读方逐字对齐)。
            string minDistanceRaw = ReadString(b, ref pos);
            string maxDistanceRaw = ReadString(b, ref pos);
            string occlusionLowpassRaw = ReadString(b, ref pos);
            string priorityClassRaw = ReadString(b, ref pos);

            return new AudioEventRow(
                cue, schemaVersion, loop, bus, whitelistCategory, triggerSource, clockRef,
                subtitleText, xfadeRaw, assets, tierParams, hasPolicy, policyTrigger, policyJitter,
                variantAxis, minDistanceRaw, maxDistanceRaw, occlusionLowpassRaw, priorityClassRaw);
        }

        /// <summary>tier_map:定型序(TierMapKeys × TierMapColumns)—— 写方按序写,读方按序读。</summary>
        private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> ReadTierMap(
            byte[] b, ref int pos)
        {
            uint tierCount = ReadU32(b, ref pos);
            if (tierCount > b.Length)
                throw E13($"tier_map 档位数={tierCount} 超出载荷长 —— 伪造计数");

            var map = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
            for (uint i = 0; i < tierCount; i++)
            {
                string tierKey = ReadString(b, ref pos);
                uint colCount = ReadU32(b, ref pos);
                if (colCount > b.Length)
                    throw E13($"tier「{tierKey}」列数={colCount} 超出载荷长 —— 伪造计数");

                var cols = new Dictionary<string, string>(StringComparer.Ordinal);
                for (uint c = 0; c < colCount; c++)
                {
                    string col = ReadString(b, ref pos);
                    bool present = ReadU8(b, ref pos) != 0;
                    if (present) cols[col] = ReadString(b, ref pos);
                }
                map[tierKey] = cols;
            }
            return map;
        }

        private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> ReadNestedMap(
            byte[] b, ref int pos)
        {
            int outerCount = ReadI32(b, ref pos);
            if (outerCount == -1) return null;
            if (outerCount < 0 || outerCount > b.Length)
                throw E13($"tier_params 档位数={outerCount} 非法 —— 损坏");

            var map = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
            for (int i = 0; i < outerCount; i++)
            {
                string key = ReadString(b, ref pos);
                int innerCount = ReadI32(b, ref pos);
                if (innerCount == -1)
                {
                    map[key] = null;
                    continue;
                }
                if (innerCount < 0 || innerCount > b.Length)
                    throw E13($"tier_params「{key}」列数={innerCount} 非法 —— 损坏");

                var inner = new Dictionary<string, string>(StringComparer.Ordinal);
                for (int c = 0; c < innerCount; c++)
                {
                    string col = ReadString(b, ref pos);
                    inner[col] = ReadString(b, ref pos);
                }
                map[key] = inner;
            }
            return map;
        }

        private static string[] ReadStringArray(byte[] b, ref int pos)
        {
            int count = ReadI32(b, ref pos);
            if (count == -1) return null;
            if (count < 0 || count > b.Length)
                throw E13($"字符串数组元素数={count} 非法 —— 损坏");
            var arr = new string[count];
            for (int i = 0; i < count; i++) arr[i] = ReadString(b, ref pos);
            return arr;
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

        // ══════════════ 字节小件(little-endian;无 BinaryReader —— 手动游标,边界可判)══════════════

        private static byte ReadU8(byte[] b, ref int pos)
        {
            if (pos + 1 > b.Length) throw E13("读 u8 越界 —— 载荷截断");
            return b[pos++];
        }

        private static int ReadI32(byte[] b, ref int pos)
        {
            if (pos + 4 > b.Length) throw E13("读 i32 越界 —— 载荷截断");
            int v = b[pos] | (b[pos + 1] << 8) | (b[pos + 2] << 16) | (b[pos + 3] << 24);
            pos += 4;
            return v;
        }

        private static uint ReadU32(byte[] b, ref int pos)
        {
            if (pos + 4 > b.Length) throw E13("读 u32 越界 —— 载荷截断");
            uint v = (uint)(b[pos] | (b[pos + 1] << 8) | (b[pos + 2] << 16) | (b[pos + 3] << 24));
            pos += 4;
            return v;
        }

        private static uint ReadU32(byte[] b, int offset)
            => (uint)(b[offset] | (b[offset + 1] << 8) | (b[offset + 2] << 16) | (b[offset + 3] << 24));

        private static InvalidOperationException E13(string detail)
            => new InvalidOperationException("[E-13][audio_events] cooked 读取失败 —— 启动期硬失败,绝不降级:" + detail);
    }
}
