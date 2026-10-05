// 权威来源:ADR-014 §二(确定性 *.cooked;同源两次烘焙逐位一致)· CookedFormat.cs(布局共参照)
//          · Story 002(`diagnosis_signs.json` 的产物写入方)
//
// ⚠️ 本文件 = R-8.1 七字段行集的**编码器**(阶段 2 烘焙的写面)。
// ⚠️ 布局与 Gameplay.Presentation.Diagnosis.DiagnosisSignCookedCodec **严格镜像** ——
//    改一处必须改两处(与 CookedWriter / CookedCodec 同纪律)。
//
// ⚠️ 载荷布局(全部 little-endian):
//    行数 i32 → 逐行:
//      sign_id:长度前缀 UTF-8(i32 len,−1 = null)
//      display_词:u8 槽位数(=3)+ 3 × 长度前缀字符串
//      channel:u8(枚举序数)
//      reveal_by:u8 方法数 + N × u8(枚举序数)
//      tier_named:i32
//      polarity:u8(枚举序数)
//      neg_weight:u8 有无标志 + [i64 raw] 仅当有

using System.Collections.Generic;
using System.IO;
using System.Text;
using DaYiJingCheng.Gameplay.Presentation.Diagnosis;
using DaYiJingCheng.Sim.Contracts;
// ⚠️ CS0104 消歧(结构侧评审 S-6):`Sim.Contracts.SignChannel` 是 AC-21 的通道**位掩码静态类**,
//    与本表 R-8.1 的通道**枚举**同名不同物 —— 本文件的 `SignChannel` 一律指枚举。
using SignChannel = DaYiJingCheng.Gameplay.Presentation.Diagnosis.SignChannel;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>词条行集的 deterministic cooked 写入器。
    /// <para>同一批行两次 ⇒ 逐位一致字节流(固定字段顺序、零字典迭代、零本地时间)。</para>
    /// </summary>
    internal static class DiagnosisSignCookedWriter
    {
        /// <summary>编码词条行集(含 20 字节头)。</summary>
        public static byte[] Write(IReadOnlyList<SignLexemeRow> rows, uint schemaVersion, uint configVersion)
        {
            using (var payload = new MemoryStream())
            using (var w = new BinaryWriter(payload, Encoding.UTF8))
            {
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

        /// <summary>单行编码(字段顺序 = SignLexemeRow 构造序,与读方镜像)。</summary>
        private static void WriteRow(BinaryWriter w, in SignLexemeRow row)
        {
            WriteString(w, row.SignId);

            string[] words = row.DisplayWords ?? new string[0];
            w.Write((byte)words.Length);            // 读方断言 == 3(校验器已保证)
            for (int i = 0; i < words.Length; i++)
                WriteString(w, words[i]);

            w.Write((byte)row.Channel);             // SignChannel : byte

            RevealMethod[] reveal = row.RevealBy ?? new RevealMethod[0];
            w.Write((byte)reveal.Length);
            for (int i = 0; i < reveal.Length; i++)
                w.Write((byte)reveal[i]);           // RevealMethod : byte

            w.Write(row.TierNamed);                 // int
            w.Write((byte)row.Polarity);            // SignPolarity : byte

            if (row.NegWeightRaw.HasValue)
            {
                w.Write((byte)1);
                w.Write(row.NegWeightRaw.Value);    // int64 raw Q16.16
            }
            else
            {
                w.Write((byte)0);
            }
        }

        /// <summary>长度前缀字符串(−1 = null;承 CookedWriter.WriteString 口径)。</summary>
        private static void WriteString(BinaryWriter w, string s)
        {
            if (s == null)
            {
                w.Write(-1);
                return;
            }
            byte[] utf8 = Encoding.UTF8.GetBytes(s);
            w.Write(utf8.Length);
            w.Write(utf8);
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
