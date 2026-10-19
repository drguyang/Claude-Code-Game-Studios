// TD-epic-01 第十一片 · 设备切换迟滞常量表的 deterministic cooked 写入器。
//
// 权威来源:ADR-014 §二(同源两次烘焙逐位一致:固定字段顺序 · 零字典迭代 · 零本地时间)
//          · §五(ConfigVersion = 源内容哈希派生;头槽承载 schema_version)
//          · CookedFormat.cs(20 字节头布局的唯一共参照)
//          · InputDeviceSwitchBinder(唯一构建期校验点)
//
// ⚠️ 载荷布局(全部 little-endian;固定字段顺序):
//    u32 schema_version
//    u8  has_pointer; u8 has_axis
//    pointer 列:i64 threshold_raw; i64 tolerance_raw; i32 dwell_ticks
//    axis    列:同上
//
// ⚠️ **列序固定 = pointer 在前、axis 在后**(不靠 JSON 迭代序;源里对调两列是语义无变化的编辑)。
// ⚠️ **Fix 一律写 i64 raw**(ADR-006:定点落盘 = raw long,禁 double 中转)。
// ⚠️ **schema_version 只住头槽**(offset 8);载荷首字段 = 源数据集版本(与头槽必须相等,
//   由读方 `DeviceSwitchTuningCookedCodec.Read` 断言)。
// ⚠️ 布局与 `Gameplay.Presentation.Input.DeviceSwitchTuningCookedCodec` **严格镜像** ——
//   改一处必须改两处(唯一自动兜现 = `InputDeviceSwitchBake.Materialize` 的真往返)。

using System.IO;
using System.Text;
using DaYiJingCheng.Gameplay.Presentation;          // DeviceSwitchTuning / DeviceSwitchColumn
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>设备切换常量表的 deterministic cooked 写入器。</summary>
    /// <example><code>byte[] bytes = InputDeviceSwitchCookedWriter.Write(tuning, schemaVersion, configVersion);</code></example>
    internal static class InputDeviceSwitchCookedWriter
    {
        /// <summary>编码(含 20 字节头)。</summary>
        public static byte[] Write(DeviceSwitchTuning tuning, uint schemaVersion, uint configVersion)
        {
            using (var payload = new MemoryStream())
            using (var w = new BinaryWriter(payload, Encoding.UTF8))
            {
                w.Write(schemaVersion);

                // Ⓑ.1 两列各自成表 —— 缺列拒绝落盘(空列会让运行期静默回退到常量兜底)
                w.Write((byte)1);   // has_pointer
                w.Write((byte)1);   // has_axis

                WriteColumn(w, tuning.Pointer);
                WriteColumn(w, tuning.Axis);

                w.Flush();
                return FinishHeader(payload.ToArray(), schemaVersion, configVersion);
            }
        }

        /// <summary>单列编码(字段顺序 = threshold / tolerance / dwell,与读方镜像)。</summary>
        private static void WriteColumn(BinaryWriter w, in DeviceSwitchColumn column)
        {
            w.Write(column.DeviceSwitchThreshold.Raw);   // i64 raw
            w.Write(column.DriftTolerance.Raw);          // i64 raw
            w.Write(column.DwellTicks);                  // i32
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
