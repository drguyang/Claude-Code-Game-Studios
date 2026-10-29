// 权威来源:ADR-014 §二(确定性 *.cooked;同源两次烘焙逐位一致)· CookedFormat.cs(布局共参照)
//          · `docs/tech-debt-register.md` `TD-audio-032` ①
//
// ⚠️ 本文件 = 13 调参表的**编码器**(阶段 2 烘焙的写面)。
// ⚠️ 布局与 Gameplay.Presentation.PatientAI.PatientAiCookedCodec **严格镜像** ——
//    改一处必须改两处(与 DiagnosisReadFloor* 同纪律)。

using System.IO;
using System.Text;
using DaYiJingCheng.Gameplay.PatientAI;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>13 调参表的 deterministic cooked 写入器。
    /// <para>同一批系数两次 ⇒ 逐位一致字节流(固定字段顺序、零字典迭代、零本地时间)。</para>
    /// </summary>
    internal static class PatientAiCookedWriter
    {
        /// <summary>编码调参表(含 20 字节头)。</summary>
        public static byte[] Write(in PatientAiBinder.BindResult bound, uint configVersion)
        {
            using (var payload = new MemoryStream())
            using (var w = new BinaryWriter(payload, Encoding.UTF8))
            {
                CueIntervals cue = bound.CueIntervals;
                w.Write(cue.Severe);
                w.Write(cue.Mild);
                w.Write(cue.Recover);

                // 阈值以 **float32** 落盘(G-4 位宽钉死 —— 禁 double;承
                // `DiagnosisReadFloorCookedWriter` 的定表同款)。
                // ⚠️ 定点化已在 binder 的 `FixParse` 那步发生(GDD `patient-ai.md` 规则五:
                // 源表禁 float 字段,须写定点串)⇒ 此处拿到的 float **已是定点化的结果**,
                // 原样落盘即可,不再二次量化。
                BehaviorBands bands = bound.BehaviorBands;
                w.Write(bands.MildMin);
                w.Write(bands.SeekMin);
                w.Write(bands.CollapseMin);
                w.Write(bands.RecoverMax);
                w.Write(bands.Hyst);
                w.Write(bands.DeathBandMin);

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
