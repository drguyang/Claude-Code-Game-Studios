// sprint-08 S1.0 · 病名册词表 cooked 写方(与 Gameplay.Presentation.Casebook.JudgmentLexiconCookedCodec 严格镜像)
//
// ⚠️ 载荷布局(全部 little-endian):行数 i32 → 逐行:
//   lexicon_id:i32 · text:长度前缀 UTF-8(i32 len,−1 = null)·
//   disease_count:u8 + N × i32

using System.Collections.Generic;
using System.IO;
using System.Text;
using DaYiJingCheng.Gameplay.Presentation.Casebook;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>病名册词表行集的 deterministic cooked 写入器(同源两次烘焙逐位一致)。</summary>
    internal static class JudgmentLexiconCookedWriter
    {
        public static byte[] Write(IReadOnlyList<JudgmentLexemeRow> rows, uint schemaVersion, uint configVersion)
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

        private static void WriteRow(BinaryWriter w, in JudgmentLexemeRow row)
        {
            w.Write(row.LexiconId);
            WriteString(w, row.Text);
            int[] diseaseIds = row.DiseaseIds ?? System.Array.Empty<int>();
            w.Write((byte)diseaseIds.Length);   // 校验器保证 ≤ MaxDiseaseIdsPerRow(255)[lexicon-disease-count]
            for (int i = 0; i < diseaseIds.Length; i++)
                w.Write(diseaseIds[i]);
        }

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
