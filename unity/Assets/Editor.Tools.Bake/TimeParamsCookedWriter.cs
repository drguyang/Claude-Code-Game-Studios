// 权威来源:ADR-014 §二(确定性 *.cooked;同源两次烘焙逐位一致)· CookedFormat.cs(布局共参照)
//          · ADR-038 §D1(载荷 = 三个 i64,零 float)
//
// ⚠️ 本文件 = `5_time` 的**编码器**(阶段 2 烘焙的写面)。
// ⚠️ 布局与 `Gameplay.Presentation.TimeParamsCookedCodec` **严格镜像** ——
//    改一处必须改两处(与 DiagnosisReadFloor* 同纪律)。
//
// ⚠️ 载荷布局(全部 little-endian):
//    ticks_per_day : i64
//    night_start   : i64
//    night_span    : i64
// 固定 24 B,零变长字段 ⇒ 读方可用固定头断言。

using System.IO;
using System.Text;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>`5_time` 的 deterministic cooked 写入器。</summary>
    internal static class TimeParamsCookedWriter
    {
        /// <summary>载荷固定字节数(3 × i64)。读方据此做长度断言。</summary>
        public const int PayloadBytes = 24;

        /// <summary>编码(含 20 字节头)。</summary>
        public static byte[] Write(in TimeParamsBinder.BindResult bound, uint configVersion)
        {
            using (var payload = new MemoryStream())
            using (var w = new BinaryWriter(payload, Encoding.UTF8))
            {
                w.Write(bound.TicksPerDay);
                w.Write(bound.NightStart);
                w.Write(bound.NightSpan);
                w.Flush();
                return FinishHeader(payload.ToArray(), bound.SchemaVersion, configVersion);
            }
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
