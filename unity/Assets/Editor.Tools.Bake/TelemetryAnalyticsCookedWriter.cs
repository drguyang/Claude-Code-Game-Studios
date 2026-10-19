// TD-epic-01 第十二片 · 51 遥测 F4–F7 参数旋钮表的 deterministic cooked 写入器。
//
// 权威来源:ADR-014 §二(同源两次烘焙逐位一致:固定字段顺序 · 零字典迭代 · 零本地时间)
//          · §五(ConfigVersion = 源内容哈希派生;头槽承载 schema_version)
//          · CookedFormat.cs(20 字节头布局的唯一共参照)
//          · TelemetryAnalyticsBinder(唯一构建期校验点)
//
// ⚠️ 载荷布局(全部 little-endian;固定字段顺序):
//    u32 schema_version
//    i32 window_ticks          (W)
//    i32 step_ticks            (S)
//    i64 first_window_tick     (t_0)
//    i32 gap_threshold_ticks   (GAP_THRESHOLD)
//    i32 growth_window_ticks   (GROWTH_WINDOW)
//    i32 min_sample            (TELEMETRY_MIN_SAMPLE)
//    载荷 = 4 + 4 + 4 + 8 + 4 + 4 + 4 = 32 字节
//
// ⚠️ **列序固定** = 按 GDD §Tuning Knobs 表格的行序(W → S → t_0 → GAP_THRESHOLD →
//    GROWTH_WINDOW → MIN_SAMPLE),不依赖 JSON 迭代序。
// ⚠️ 全为 int / tick 计数,**不是 Fix**(ADR-006 D-21-17)⇒ 无 `FixParse`、无 Q16.16。
// ⚠️ 布局与 `Gameplay.Presentation.Telemetry.TelemetryAnalyticsCookedCodec` **严格镜像** ——
//    改一处必须改两处(唯一自动兜现 = `TelemetryAnalyticsBake.Materialize` 的真往返)。

using System.IO;
using System.Text;
using DaYiJingCheng.Gameplay.Presentation.Telemetry;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>遥测参数旋钮表的 deterministic cooked 写入器。</summary>
    internal static class TelemetryAnalyticsCookedWriter
    {
        /// <summary>载荷固定字节数(不含 20 字节头)。</summary>
        public const int PayloadBytes = 32;

        /// <summary>编码(含 20 字节头)。</summary>
        public static byte[] Write(in TelemetryAnalyticsTuning tuning, uint schemaVersion, uint configVersion)
        {
            using (var payload = new MemoryStream(PayloadBytes))
            using (var w = new BinaryWriter(payload, Encoding.UTF8))
            {
                w.Write(schemaVersion);                                 // u32
                w.Write(tuning.DifficultyCurve.WindowTicks);            // i32 W
                w.Write(tuning.DifficultyCurve.StepTicks);              // i32 S
                w.Write(tuning.DifficultyCurve.FirstWindowTick);        // i64 t_0
                w.Write(tuning.GapThresholdTicks);                      // i32
                w.Write(tuning.GrowthWindowTicks);                      // i32
                w.Write(tuning.MinSample);                              // i32
                w.Flush();
                return FinishHeader(payload.ToArray(), schemaVersion, configVersion);
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
