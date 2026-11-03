// 权威来源:ADR-014 §二(确定性 *.cooked)· CookedFormat.cs(布局共参照)
//          · ADR-038 Amendment A(载荷 = 两个 i64,零 float)
//
// ⚠️ 本文件 = `5_weather` 的编码器(写面)。
// ⚠️ 布局与 `Gameplay.Presentation.WeatherParamsCookedCodec` **严格镜像**。
//
// 载荷布局(全部 little-endian,固定 16 B):
//    weather_block_ticks : i64
//    intensity_max       : i64

using System.IO;
using System.Text;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>`5_weather` 的 deterministic cooked 写入器。</summary>
    internal static class WeatherParamsCookedWriter
    {
        /// <summary>载荷固定字节数(2 × i64)。</summary>
        public const int PayloadBytes = 16;

        /// <summary>编码(含 20 字节头)。</summary>
        public static byte[] Write(in WeatherParamsBinder.BindResult bound, uint configVersion)
        {
            using (var payload = new MemoryStream())
            using (var w = new BinaryWriter(payload, Encoding.UTF8))
            {
                w.Write(bound.WeatherBlockTicks);
                w.Write(bound.IntensityMax);
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
