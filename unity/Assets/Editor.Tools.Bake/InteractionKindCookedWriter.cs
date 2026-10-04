// 权威来源:ADR-014 §二(确定性 *.cooked;同源两次烘焙逐位一致)· CookedFormat.cs(布局共参照)
//          · Story 007(NR-1:interaction_kinds.json 的产物写入方)
//
// ⚠️ 本文件 = `4-DC-1…6` 契约行的**编码器**(阶段 2 烘焙的写面)。
//    story-006 交付了校验器(`InteractionKindTableValidator`)与 C# 夹具,但**无烘焙路径调用它**
//    (结构侧评审 F-1 记为死代码)。本件与 InteractionKindBinder 一起,把校验器接进装载路径。
//
// ⚠️ 布局与 Gameplay.Presentation.InteractionKindCookedCodec **严格镜像** —— 改一处必须改两处
//    (与 CookedWriter / CookedCodec 同纪律)。

using System.Collections.Generic;
using System.IO;
using System.Text;
using DaYiJingCheng.Gameplay.Interaction;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>交互数据契约行集的 deterministic cooked 写入器。
    /// <para>同一批行两次 ⇒ 逐位一致字节流(固定字段顺序、零字典迭代、零本地时间)。</para>
    /// <example><code>byte[] bytes = InteractionKindCookedWriter.Write(rows, schemaVersion, configVersion);</code></example>
    /// </summary>
    internal static class InteractionKindCookedWriter
    {
        /// <summary>编码契约行集(含 20 字节头)。载荷 = 行数 + 世界维度四元 + 逐行 11 字段(固定序)。</summary>
        /// <param name="rInteract">4-DC-1 的半径(与 W/H/D 同批,供读方闭路校验)。</param>
        public static byte[] Write(IReadOnlyList<KindContractRow> rows, uint schemaVersion, uint configVersion,
                                   int rInteract, int worldW, int worldH, int worldD)
        {
            using (var payload = new MemoryStream())
            using (var w = new BinaryWriter(payload, Encoding.UTF8))
            {
                // ⚠️ 维度四元随产物落盘(QA F-7):读方据此对**被烘过的那组**维度闭路校验,
                //    不得在测试里写字面量 —— 否则改种子维度后测试仍绿,却校验一张从未烘过的表。
                w.Write(rInteract);
                w.Write(worldW);
                w.Write(worldH);
                w.Write(worldD);

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

        /// <summary>单行编码(字段顺序 = KindContractRow 构造序,与读方镜像)。</summary>
        private static void WriteRow(BinaryWriter w, in KindContractRow row)
        {
            w.Write((byte)row.Kind);                       // InteractableKind : byte
            w.Write(row.KindPriority);                     // int
            w.Write((byte)row.StableIdSource);             // StableIdSource : byte
            w.Write(row.RoutesTo);                         // int
            w.Write((byte)(row.SuppressesMotor ? 1 : 0));  // bool → u8
            w.Write((byte)row.DurationOwner);              // DurationOwnerKind : byte(枚举序数)
            w.Write(row.DurationOwnerSystemId);            // int
            w.Write(row.WorldW);                           // int(4-DC-4 ②)
            w.Write(row.WorldH);                           // int
            w.Write(row.WorldD);                           // int
            w.Write((byte)row.IntentUplink);               // IntentUplink : byte
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
