// 权威来源:ADR-014 §二(确定性 *.cooked;同源两次烘焙逐位一致)· CookedFormat.cs(布局共参照)
//          · Story 007(NR-1:disease_action_axis.json 的产物写入方)
//
// ⚠️ 本文件 = `9-DC-1…7` 契约行的**编码器**(阶段 2 烘焙的写面)。
//    story-007 交付了校验器(`DiseaseActionAxisValidator`)与 C# 夹具,但**无烘焙路径调用它**
//    (结构侧评审 F-1 记为死代码)。本件与 DiseaseActionAxisBinder 一起,把校验器接进装载路径。
//
// ⚠️ 布局与 Gameplay.Presentation.DiseaseActionAxisCookedCodec **严格镜像** —— 改一处必须改两处
//    (与 CookedWriter / CookedCodec 同纪律)。

using System.Collections.Generic;
using System.IO;
using System.Text;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>处置轴 + treatable_by 关系集的 deterministic cooked 写入器。
    /// <para>同一批行两次 ⇒ 逐位一致字节流(固定字段顺序、零字典迭代、零本地时间)。</para>
    /// <example><code>byte[] bytes = DiseaseActionAxisCookedWriter.Write(actions, treatableBy, schemaVersion, configVersion);</code></example>
    /// </summary>
    internal static class DiseaseActionAxisCookedWriter
    {
        /// <summary>编码契约行集(含 20 字节头)。载荷 = 处置轴行数 + 逐行 3 字段(固定序)+ treatable_by 行数 + 逐行 3 字段(固定序)。</summary>
        public static byte[] Write(IReadOnlyList<ActionAxisRow> actions, IReadOnlyList<TreatableByRow> treatableBy,
                                   uint schemaVersion, uint configVersion)
        {
            using (var payload = new MemoryStream())
            using (var w = new BinaryWriter(payload, Encoding.UTF8))
            {
                // 处置轴行集
                w.Write(actions?.Count ?? -1);
                if (actions != null)
                {
                    for (int i = 0; i < actions.Count; i++)
                        WriteActionRow(w, actions[i]);
                }

                // treatable_by 行集
                w.Write(treatableBy?.Count ?? -1);
                if (treatableBy != null)
                {
                    for (int i = 0; i < treatableBy.Count; i++)
                        WriteTreatableByRow(w, treatableBy[i]);
                }

                w.Flush();
                return FinishHeader(payload.ToArray(), schemaVersion, configVersion);
            }
        }

        /// <summary>单行编码(字段顺序 = ActionAxisRow 构造序,与读方镜像)。</summary>
        private static void WriteActionRow(BinaryWriter w, in ActionAxisRow row)
        {
            w.Write(row.ActionId);                     // int
            w.Write(row.Name ?? "");                   // string
            w.Write(row.Owner ?? "");                  // string
        }

        /// <summary>单行编码(字段顺序 = TreatableByRow 构造序,与读方镜像)。</summary>
        private static void WriteTreatableByRow(BinaryWriter w, in TreatableByRow row)
        {
            w.Write(row.DiseaseKey ?? "");             // string
            w.Write(row.ActionId);                     // int
            w.Write(row.Polarity ?? "");               // string
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
