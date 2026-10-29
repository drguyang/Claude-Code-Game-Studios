// S14-6 · story-010 —— 音频事件表的 deterministic cooked 写入器(阶段 2 烘焙的写面)。
//
// 权威来源:
//   ADR-014 §二(同源两次烘焙逐位一致:固定字段顺序 · 零字典迭代 · 零本地时间)
//   CookedFormat.cs(20 字节头布局的唯一共参照)· AudioEventTableBinder.BindResult(行集来源)
//   AudioEventTableGates.{TierMapKeys, TierMapColumns}(tier_map 定型序的单一出处)
//
// ⚠️ 载荷布局(全部 little-endian;固定字段顺序):
//   u32 schema_version
//   u32 row_count
//   row_count × {
//     string cue; u32 row_schema_version; u8 loop;
//     string bus; string whitelist_category; string trigger_source;
//     string clock_ref; string subtitle_text; string xfade_raw;
//     u32 asset_count; asset_count × string
//     u32 tier_param_count; tier_param_count × { string tierKey; u32 colCount; colCount × { string col; string val } }
//     u8 has_policy; if has_policy { string trigger_phase_raw; string jitter_raw }
//   }
//   u32 tier_map_count(= TierMapKeys.Count)
//   tier_map_count × { string tierKey; u32 colCount(= TierMapColumns.Count);
//                      colCount × { string col; u8 present; if present string value } }
//
// ⚠️ **行序 = cue ordinal**(本件钉死;理由见 AudioEventTableBinder 文件头)——
//   **不排序**。这与 DelayTableCookedWriter 相反:那张表的键是枚举序数、行序须规范化,
//   本表的「行序」本身就是语义(ordinal),排序 = 改语义。写方在此**保序直写**,
//   由测试钉住「行序即 ordinal」+「同源两次烘焙逐位一致」两条性质。
//
// ⚠️ **tier_map 走定型序**(TierMapKeys × TierMapColumns),不是字典迭代序 ——
//   与 DelayTableCookedWriter 的「零字典迭代」同一纪律。行内 tier_params 亦然(键序升序)。
//
// ⚠️ **schema_version 只住头槽**(offset 8,单一出处)。
//   承 DelayTableCookedWriter 的 story-002 R-1 教训:头槽硬写 0、真值编进载荷首字段 ⇒
//   ① 违 CookedFormat 头契约(读方只读头槽比对,不符 = 启动期硬失败);② 同值两处存 = 第二真源。
//   本件头槽写 <paramref name="schemaVersion"/>;载荷首字段是**行级** schema_version(另一个量,非重复)。

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DaYiJingCheng.EditorTools.Gates;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>音频事件表的 deterministic cooked 写入器。</summary>
    internal static class AudioEventTableCookedWriter
    {
        /// <summary>编码事件表(含 20 字节头)。</summary>
        /// <exception cref="InvalidOperationException">表为空 / 无行 —— 空表会让 44 一个 cue 都解析不出
        /// (静默播空),fail-loud(门侧 ValidateRequiredCues 应已拦;此处是纵深防御)。</exception>
        public static byte[] Write(AudioEventTableBinder.BindResult bound, uint configVersion)
        {
            AudioEventTable table = bound.Table;
            if (table == null || table.Rows == null || table.Rows.Count == 0)
                throw new InvalidOperationException(
                    "音频事件表绑定行集为空 ⇒ 拒绝落盘(门侧 ValidateRequiredCues 应已拦;" +
                    "空表会让 44 解析不出任何 cue = 静默失败)");

            using (var payload = new MemoryStream())
            using (var w = new BinaryWriter(payload, Encoding.UTF8))
            {
                w.Write(bound.SchemaVersion);
                w.Write(table.Rows.Count);
                for (int i = 0; i < table.Rows.Count; i++)
                    WriteRow(w, table.Rows[i]);

                WriteTierMap(w, table.TierMap);

                w.Flush();
                return FinishHeader(payload.ToArray(), bound.SchemaVersion, configVersion);
            }
        }

        // ══════════════ 行 ══════════════

        private static void WriteRow(BinaryWriter w, AudioEventTableRow row)
        {
            WriteString(w, row?.Cue);
            w.Write(ParseU32(row?.SchemaVersionRaw));
            w.Write((byte)(row != null && row.Loop ? 1 : 0));
            WriteString(w, row?.Bus);
            WriteString(w, row?.WhitelistCategory);
            WriteString(w, row?.TriggerSource);
            WriteString(w, row?.ClockRef);
            WriteString(w, row?.SubtitleText);
            WriteString(w, row?.XfadeRaw);

            WriteStringArray(w, row?.Assets);
            WriteNestedMap(w, row?.TierParams);

            bool hasPolicy = row?.Policy != null;
            w.Write((byte)(hasPolicy ? 1 : 0));
            if (hasPolicy)
            {
                WriteString(w, row.Policy.TriggerPhaseRaw);
                WriteString(w, row.Policy.JitterRaw);
            }

            // story-020 重裁 ①:变体轴声明(null = 非变体行)。**追加在末尾** ——
            // 既有字段偏移不变;读方 AudioEventCookedCodec.ReadRow 须同改。
            WriteString(w, row?.VariantAxisRaw);

            // 🔁 2026-10-29(`TD-audio-035` ③):空间化四列。**同样追加在末尾**(既有偏移不变),
            // 读方 `AudioEventCookedCodec.ReadRow` 须逐字同序同改。
            // ⚠️ 写**原文**而不是解析后的 int:`null` 与 `0` 必须可分(缺列 = 静默回落,
            // 而 `0` 是非法值 —— 若在此把 null 折成 0,读方就无法区分「没给」与「给了非法值」)。
            WriteString(w, row?.MinDistanceRaw);
            WriteString(w, row?.MaxDistanceRaw);
            WriteString(w, row?.OcclusionLowpassRaw);
            WriteString(w, row?.PriorityClassRaw);
        }

        // ══════════════ tier_map(定型序 = TierMapKeys × TierMapColumns)══════════════

        private static void WriteTierMap(
            BinaryWriter w, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> tierMap)
        {
            IReadOnlyList<string> keys = AudioEventTableGates.TierMapKeys;
            IReadOnlyList<string> cols = AudioEventTableGates.TierMapColumns;

            w.Write(keys.Count);
            for (int i = 0; i < keys.Count; i++)
            {
                WriteString(w, keys[i]);
                w.Write(cols.Count);

                IReadOnlyDictionary<string, string> row = null;
                tierMap?.TryGetValue(keys[i], out row);

                for (int c = 0; c < cols.Count; c++)
                {
                    WriteString(w, cols[c]);
                    string value = null;
                    bool present = row != null && row.TryGetValue(cols[c], out value) && value != null;
                    w.Write((byte)(present ? 1 : 0));
                    if (present) WriteString(w, value);
                }
            }
        }

        /// <summary>嵌套映射(键序**升序**,确定性 —— 不用字典迭代序)。</summary>
        private static void WriteNestedMap(
            BinaryWriter w,
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> map)
        {
            if (map == null)
            {
                w.Write(-1);
                return;
            }

            var outer = new List<string>(map.Keys);
            outer.Sort(StringComparer.Ordinal);

            w.Write(outer.Count);
            for (int i = 0; i < outer.Count; i++)
            {
                WriteString(w, outer[i]);
                IReadOnlyDictionary<string, string> inner = map[outer[i]];
                if (inner == null)
                {
                    w.Write(-1);
                    continue;
                }

                var cols = new List<string>(inner.Keys);
                cols.Sort(StringComparer.Ordinal);

                w.Write(cols.Count);
                for (int c = 0; c < cols.Count; c++)
                {
                    WriteString(w, cols[c]);
                    WriteString(w, inner[cols[c]]);
                }
            }
        }

        // ══════════════ 小件 ══════════════

        /// <summary>字符串:长度前缀(-1 = null,与 CookedWriter 同规格)。</summary>
        private static void WriteString(BinaryWriter w, string value)
        {
            if (value == null)
            {
                w.Write(-1);
                return;
            }
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            w.Write(bytes.Length);
            w.Write(bytes);
        }

        /// <summary>字符串数组:-1 = null,0 = 空。</summary>
        private static void WriteStringArray(BinaryWriter w, IReadOnlyList<string> values)
        {
            if (values == null)
            {
                w.Write(-1);
                return;
            }
            w.Write(values.Count);
            for (int i = 0; i < values.Count; i++) WriteString(w, values[i]);
        }

        /// <summary>原文 → u32(行级 schema_version;不可解析 ⇒ 0 —— 门已就此报错,写方不二次抛)。</summary>
        private static uint ParseU32(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return 0;
            return uint.TryParse(raw, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out uint v) ? v : 0;
        }

        /// <summary>头(magic + formatVersion + schemaVersion + configVersion + payloadLength)+ 载荷。</summary>
        private static byte[] FinishHeader(byte[] payload, uint schemaVersion, uint configVersion)
        {
            var output = new byte[CookedFormat.HeaderSize + payload.Length];
            string magic = CookedFormat.Magic;
            output[0] = (byte)magic[0];
            output[1] = (byte)magic[1];
            output[2] = (byte)magic[2];
            output[3] = (byte)magic[3];
            WriteU32Le(output, 4, CookedFormat.FormatVersion);
            WriteU32Le(output, 8, schemaVersion);   // R-1:头槽承载(非 0,非载荷首字段)
            WriteU32Le(output, 12, configVersion);
            WriteU32Le(output, 16, (uint)payload.Length);
            Buffer.BlockCopy(payload, 0, output, CookedFormat.HeaderSize, payload.Length);
            return output;
        }

        private static void WriteU32Le(byte[] buffer, int offset, uint value)
        {
            buffer[offset] = (byte)(value & 0xFF);
            buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
            buffer[offset + 2] = (byte)((value >> 16) & 0xFF);
            buffer[offset + 3] = (byte)((value >> 24) & 0xFF);
        }
    }
}
