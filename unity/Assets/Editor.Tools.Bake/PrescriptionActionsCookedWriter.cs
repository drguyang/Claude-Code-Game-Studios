// 权威来源:ADR-014 §二(确定性 *.cooked;同源两次烘焙逐位一致)· CookedFormat.cs(布局共参照)
//          · Story 001(prescription_actions.json 的产物写入方)
//
// ⚠️ 本文件 = 处方表契约行的**编码器**(阶段 2 烘焙的写面)。
//    布局与运行期 CookedCodec **严格镜像** —— 改一处必须改两处。
//
// ⚠️ 载荷布局:
//    [0..3]   DOSE_BASE (i32)
//    [4..7]   MAX_DOSE_DETENTS (i32)
//    [8..11]  row_count (i32)
//    [12..]   逐行:item_key (i32 len + UTF-8) + action_id (i32) + polarity (i32 len + UTF-8)

using System.Collections.Generic;
using System.IO;
using System.Text;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>处方表行集的 deterministic cooked 写入器。
    /// <para>同一批行两次 ⇒ 逐位一致字节流(固定字段顺序、零字典迭代、零本地时间)。</para>
    /// </summary>
    internal static class PrescriptionActionsCookedWriter
    {
        /// <summary>编码处方表行集(含 20 字节头)。</summary>
        public static byte[] Write(IReadOnlyList<PrescriptionActionRow> rows, uint schemaVersion, uint configVersion,
                                   int doseBase, int maxDoseDetents)
        {
            using (var payload = new MemoryStream())
            using (var w = new BinaryWriter(payload, Encoding.UTF8))
            {
                w.Write(doseBase);
                w.Write(maxDoseDetents);
                w.Write(rows?.Count ?? -1);
                if (rows != null)
                {
                    for (int i = 0; i < rows.Count; i++)
                        WriteRow(w, rows[i]);
                }
                w.Flush();
                return FinishHeader(payload.ToArray(), schemaVersion, configVersion);
            }
        }

        /// <summary>单行编码(字段顺序 = PrescriptionActionRow 构造序,与读方镜像)。</summary>
        private static void WriteRow(BinaryWriter w, in PrescriptionActionRow row)
        {
            WriteString(w, row.ItemKey);
            w.Write(row.ActionId);
            WriteString(w, row.Polarity);
        }

        /// <summary>字符串编码:i32 字节长 + UTF-8 字节(无终止符)。</summary>
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

        /// <summary>头(magic + formatVersion + schemaVersion + configVersion + payloadLength)+ 载荷。</summary>
        private static byte[] FinishHeader(byte[] payload, uint schemaVersion, uint configVersion)
        {
            var output = new byte[CookedFormat.HeaderSize + payload.Length];
            output[0] = (byte)'D';
            output[1] = (byte)'Y';
            output[2] = (byte)'J';
            output[3] = (byte)'C';
            WriteU32Le(output, 4, CookedFormat.FormatVersion);
            WriteU32Le(output, 8, schemaVersion);
            WriteU32Le(output, 12, configVersion);
            WriteU32Le(output, 16, (uint)payload.Length);
            System.Buffer.BlockCopy(payload, 0, output, CookedFormat.HeaderSize, payload.Length);
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
