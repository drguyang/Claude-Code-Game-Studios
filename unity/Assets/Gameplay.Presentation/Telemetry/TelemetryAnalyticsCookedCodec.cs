// TD-epic-01 第十二片 · 遥测参数旋钮表 cooked 读方(与
// `Editor.Tools.Bake.TelemetryAnalyticsCookedWriter` **严格镜像**)。
//
// 权威来源:
//   ADR-014 §五(cooked 装载 · E-13 失败语义 = 启动期硬失败,零默认值、零静默降级)
//   CookedFormat.cs(20 字节头 + 载荷小件规格的唯一共参照)
//   AC-51-B14(`W < 1` / `S < 1` ⇒ **加载期**硬失败 —— 本类是那一道加载期门)
//
// ⚠️ 载荷布局(全部 little-endian;固定字段顺序):
//    u32 schema_version
//    i32 window_ticks  · i32 step_ticks · i64 first_window_tick
//    i32 gap_threshold_ticks · i32 growth_window_ticks · i32 min_sample
//
// ⚠️ **加载期门在解码之后、返回之前**:即使上游绕过 binder 写出越界值,
//    read 也会经 `TelemetryAnalyticsTuning.Validate` 拒收 —— AC-51-B14 的两半
//    (构建期 / 加载期)各有一道,且**都**落在生产代码里(本件不依赖测试自持谓词)。
// ⚠️ 零 `System.IO` 词面 —— 本件不做文件 I/O,字节由 Addressables 侧喂入。
// ⚠️ 改任一侧布局必须同改另一侧(唯一自动兜现 = `TelemetryAnalyticsBake.Materialize` 真往返)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Presentation.Telemetry
{
    /// <summary>遥测参数旋钮表烘焙产物的读方(写方 = <c>Editor.Tools.Bake.TelemetryAnalyticsCookedWriter</c>)。</summary>
    public static class TelemetryAnalyticsCookedCodec
    {
        /// <summary>读方认领的 schema_version(不匹配 = E-13 拒收)。</summary>
        public const uint ExpectedSchemaVersion = 1u;

        /// <summary>载荷固定字节数(u32 + i32×5 + i64 = 32)。</summary>
        public const int PayloadBytes = 32;

        /// <summary>读旋钮表(六旋钮 + 双版本号 + 范围门)。</summary>
        /// <exception cref="InvalidOperationException">头部损坏 / 版本不匹配 / 载荷不齐 / 范围越界 —— E-13 硬失败。</exception>
        public static TelemetryAnalyticsDataSet Read(byte[] bytes)
        {
            int pos = ValidateHeader(bytes, out uint configVersion, out uint headerSchemaVersion);

            uint payloadSchemaVersion = ReadU32(bytes, ref pos);
            if (payloadSchemaVersion != headerSchemaVersion)
                throw E13($"载荷 schema_version={payloadSchemaVersion} ≠ 头槽 {headerSchemaVersion} " +
                          "—— 同一真值两处存且已分叉(写方契约:两者必须相等)");

            int window = ReadI32(bytes, ref pos);
            int step = ReadI32(bytes, ref pos);
            long first = ReadI64(bytes, ref pos);
            int gap = ReadI32(bytes, ref pos);
            int growth = ReadI32(bytes, ref pos);
            int minSample = ReadI32(bytes, ref pos);

            if (pos != bytes.Length)
                throw E13($"载荷尾部不齐(读至 {pos},共 {bytes.Length})");

            var tuning = new TelemetryAnalyticsTuning(
                new DifficultyCurveTuning(window, step, first), gap, growth, minSample);

            // ── 加载期门(AC-51-B14 第二半)──
            List<string> errors = tuning.Validate("[加载期][telemetry_analytics]");
            if (errors.Count > 0)
                throw E13("参数越界 —— 加载期硬失败,不得静默取默认值:\n  " + string.Join("\n  ", errors));

            return new TelemetryAnalyticsDataSet(tuning, headerSchemaVersion, configVersion);
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
            if (payloadLength != PayloadBytes)
                throw E13($"payloadLength={payloadLength} ≠ 契约 {PayloadBytes} B(六旋钮固定载荷)");
            return CookedFormat.HeaderSize;
        }

        // ══════════════ 字节小件(little-endian;手动游标,边界可判)══════════════

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
                "[E-13][telemetry_analytics] cooked 读取失败 —— 启动期硬失败,绝不降级:" + detail);
    }
}
