// 权威来源:ADR-014 §二(确定性 *.cooked;同源两次烘焙逐位一致)· CookedFormat.cs(布局共参照)
//          · Story 004(F-8.3 阴性定表的产物写入方)
//
// ⚠️ 本文件 = F-8.3 定表的**编码器**(阶段 2 烘焙的写面)。
// ⚠️ 布局与 Gameplay.Presentation.Diagnosis.DiagnosisNegativeConfidenceCookedCodec **严格镜像** ——
//    改一处必须改两处(与 DiagnosisReadFloor* 同纪律)。
//
// ⚠️ 载荷布局(全部 little-endian):
//    skill_cap            : i32
//    neg_conf_0           : i64 raw Q16.16
//    neg_conf_cap         : i64 raw Q16.16
//    neg_gamma            : i64 raw Q16.16
//    neg_weight_fallback  : i64 raw Q16.16
//    exclude_conf_min     : i64 raw Q16.16
//    表长 count           : i32(= skill_cap + 1)
//    count × float32(IEEE-754 binary32;G-4 位宽钉死 —— **禁 double**)
//
// ⚠️ 旋钮以 raw i64 落盘(非 float):运行期若要复核端点锚,拿的是**整数域**的值,
//    不引入第二处浮点舍入路径(承 ADR-005/006 定点边界纪律)。

using System.IO;
using System.Text;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>F-8.3 阴性定表的 deterministic cooked 写入器。
    /// <para>同一批旋钮两次 ⇒ 逐位一致字节流(固定字段顺序、零字典迭代、零本地时间)。</para>
    /// </summary>
    internal static class DiagnosisNegativeConfidenceCookedWriter
    {
        /// <summary>编码定表(含 20 字节头)。</summary>
        public static byte[] Write(in DiagnosisNegativeConfidenceBinder.BindResult bound, uint configVersion)
        {
            using (var payload = new MemoryStream())
            using (var w = new BinaryWriter(payload, Encoding.UTF8))
            {
                w.Write(bound.SkillCap);
                w.Write(bound.NegConf0Raw);
                w.Write(bound.NegConfCapRaw);
                w.Write(bound.NegGammaRaw);
                w.Write(bound.NegWeightFallbackRaw);
                w.Write(bound.ExcludeMinRaw);

                float[] table = bound.Table;
                w.Write(table.Length);
                for (int i = 0; i < table.Length; i++)
                    w.Write(table[i]);          // BinaryWriter.Write(float) = 4 B little-endian

                w.Flush();
                return FinishHeader(payload.ToArray(), bound.SchemaVersion, configVersion);
            }
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
