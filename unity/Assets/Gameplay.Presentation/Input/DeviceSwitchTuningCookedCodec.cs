// TD-epic-01 第十一片 · 设备切换迟滞常量表 cooked 读方(与
// `Editor.Tools.Bake.InputDeviceSwitchCookedWriter` **严格镜像**)。
//
// 权威来源:
//   ADR-014 §五(cooked 装载 · E-13 失败语义 = 启动期硬失败,零默认值、零静默降级)
//   CookedFormat.cs(20 字节头 + 载荷小件规格的唯一共参照)
//   AC-3-D4(三旋钮 × 两设备类列 —— 载荷结构 = 两列各自成表)
//
// ⚠️ 载荷布局(全部 little-endian;固定字段顺序;无字典迭代):
//    u32 schema_version
//    u8  has_pointer      (1 = 有;0 = 无 —— 写方拒绝无列落盘,故运行期读到的 0 = 上游绕过写方)
//    u8  has_axis
//    [has_pointer == 1] pointer 列:i64 threshold_raw; i64 tolerance_raw; i32 dwell_ticks
//    [has_axis    == 1] axis    列:同上
//
// ⚠️ **Fix 一律以 i64 raw 承载**(ADR-006:定点 = raw long;禁 float 中转)。
//   `Fix` 的构造器从 raw 复原 —— 全程整数域,零精度损失、零平台差。
// ⚠️ **零 `System.IO` 词面** —— 本件不做文件 I/O,字节由 Addressables 侧喂入。
// ⚠️ 布局逐字镜像写方;**改任一侧必须同改另一侧**
//   (唯一自动兜现 = `InputDeviceSwitchBake.Materialize` 的真往返 + 跨侧等值断言)。

using System;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Presentation.Input
{
    /// <summary>设备切换常量表烘焙产物的读方(写方 = <c>Editor.Tools.Bake.InputDeviceSwitchCookedWriter</c>)。</summary>
    public static class DeviceSwitchTuningCookedCodec
    {
        /// <summary>读方认领的 schema_version(不匹配 = E-13 拒收)。</summary>
        public const uint ExpectedSchemaVersion = 1u;

        /// <summary>单列固定字节数(i64 × 2 + i32 = 20)。</summary>
        public const int ColumnBytes = 20;

        /// <summary>读常量表(两列 + 双版本号)。</summary>
        /// <exception cref="InvalidOperationException">头部损坏 / 版本不匹配 / 载荷不齐 / 列缺失 —— E-13 硬失败。</exception>
        public static DeviceSwitchTuningDataSet Read(byte[] bytes)
        {
            int pos = ValidateHeader(bytes, out uint configVersion, out uint headerSchemaVersion);

            uint payloadSchemaVersion = ReadU32(bytes, ref pos);
            if (payloadSchemaVersion != headerSchemaVersion)
                throw E13($"载荷 schema_version={payloadSchemaVersion} ≠ 头槽 {headerSchemaVersion} " +
                          "—— 同一真值两处存且已分叉(写方契约:两者必须相等)");

            bool hasPointer = ReadU8(bytes, ref pos) != 0;
            bool hasAxis = ReadU8(bytes, ref pos) != 0;
            if (!hasPointer || !hasAxis)
                throw E13($"列缺失(pointer={hasPointer}, axis={hasAxis})—— Ⓑ.1 两列各自成表," +
                          "写方拒绝缺列落盘;读到缺列 = 上游绕过了写方(空列会让运行期静默回退到常量兜底," +
                          "使「烘焙表是真源」结构上不可证)");

            DeviceSwitchColumn pointer = ReadColumn(bytes, ref pos, "pointer");
            DeviceSwitchColumn axis = ReadColumn(bytes, ref pos, "axis");

            if (pos != bytes.Length)
                throw E13($"载荷尾部不齐(读至 {pos},共 {bytes.Length})");

            return new DeviceSwitchTuningDataSet(new DeviceSwitchTuning(pointer, axis),
                headerSchemaVersion, configVersion);
        }

        private static DeviceSwitchColumn ReadColumn(byte[] b, ref int pos, string label)
        {
            if (pos + ColumnBytes > b.Length)
                throw E13($"{label} 列越界(需 {ColumnBytes} B,余 {b.Length - pos} B)—— 载荷截断");
            long thresholdRaw = ReadI64(b, ref pos);
            long toleranceRaw = ReadI64(b, ref pos);
            int dwellTicks = ReadI32(b, ref pos);
            return new DeviceSwitchColumn(new Fix(thresholdRaw), new Fix(toleranceRaw), dwellTicks);
        }

        private static int ValidateHeader(byte[] bytes, out uint configVersion, out uint schemaVersion)
        {
            configVersion = 0;
            schemaVersion = 0;
            if (bytes == null || bytes.Length < CookedFormat.HeaderSize)
                throw E13("产物短于头部 —— 损坏");
            if (bytes[0] != 'D' || bytes[1] != 'Y' || bytes[2] != 'J' || bytes[3] != 'C')
                throw E13("魔数不符(≠ DYJC)—— 损坏");
            uint formatVersion = ReadU32(bytes, 4);
            if (formatVersion > CookedFormat.FormatVersion)
                throw E13($"formatVersion={formatVersion} > 读方 {CookedFormat.FormatVersion} —— 读方过旧");
            schemaVersion = ReadU32(bytes, 8);
            if (schemaVersion != ExpectedSchemaVersion)
                throw E13($"schemaVersion={schemaVersion} ≠ {ExpectedSchemaVersion} —— 版本不匹配");
            configVersion = ReadU32(bytes, 12);
            uint payloadLength = ReadU32(bytes, 16);
            if (payloadLength != bytes.Length - CookedFormat.HeaderSize)
                throw E13($"payloadLength={payloadLength} ≠ 实际 {bytes.Length - CookedFormat.HeaderSize}");
            return CookedFormat.HeaderSize;
        }

        // ══════════════ 字节小件(little-endian;手动游标,边界可判)══════════════

        private static byte ReadU8(byte[] b, ref int pos)
        {
            if (pos + 1 > b.Length) throw E13("读 u8 越界 —— 载荷截断");
            return b[pos++];
        }

        private static int ReadI32(byte[] b, ref int pos)
        {
            if (pos + 4 > b.Length) throw E13("读 i32 越界 —— 载荷截断");
            int v = b[pos] | (b[pos + 1] << 8) | (b[pos + 2] << 16) | (b[pos + 3] << 24);
            pos += 4;
            return v;
        }

        private static uint ReadU32(byte[] b, ref int pos)
        {
            if (pos + 4 > b.Length) throw E13("读 u32 越界 —— 载荷截断");
            uint v = (uint)(b[pos] | (b[pos + 1] << 8) | (b[pos + 2] << 16) | (b[pos + 3] << 24));
            pos += 4;
            return v;
        }

        private static uint ReadU32(byte[] b, int offset)
            => (uint)(b[offset] | (b[offset + 1] << 8) | (b[offset + 2] << 16) | (b[offset + 3] << 24));

        private static long ReadI64(byte[] b, ref int pos)
        {
            if (pos + 8 > b.Length) throw E13("读 i64 越界 —— 载荷截断");
            long v = (long)(uint)(b[pos] | (b[pos + 1] << 8) | (b[pos + 2] << 16) | (b[pos + 3] << 24))
                   | ((long)(uint)(b[pos + 4] | (b[pos + 5] << 8) | (b[pos + 6] << 16) | (b[pos + 7] << 24)) << 32);
            pos += 8;
            return v;
        }

        private static InvalidOperationException E13(string detail)
            => new InvalidOperationException(
                "[E-13][input_device_switch] cooked 读取失败 —— 启动期硬失败,绝不降级:" + detail);
    }
}
