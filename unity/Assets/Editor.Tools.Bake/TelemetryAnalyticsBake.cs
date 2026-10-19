// TD-epic-01 第十二片 · 遥测参数旋钮表的**仓根装载器**(阶段 1 词法 + 阶段 2 绑定 + 编码)。
//
// 权威来源:ADR-014 §三/§五(两阶段烘焙入口 · ConfigVersion = 源数据集内容哈希派生)
//          · TelemetryAnalyticsBinder(唯一构建期校验点)
//          · TelemetryAnalyticsCookedWriter(编码面)
//          · 先例 = InputDeviceSwitchBake / AudioSettingsRegistryBake(同款「仓根装载器」形态)
//
// ⚠️ 本件刻意**不碰 UnityEngine / Addressables**:纯文件 I/O + 绑定 + 编码 ⇒ EditMode 可直驱。
// ⚠️ **本件不落盘**:写盘归 `DataBakeMenu`(菜单 → BakeFromRepo → Materialize 真往返 → 写字节)。

using System;
using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.Gameplay.Presentation.Telemetry;   // TelemetryAnalyticsTuning / DataSet / Codec
using DaYiJingCheng.Sim.Contracts;                     // CookedFormat(20 字节头布局的唯一共参照)

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary><c>telemetry_analytics.json</c> 的仓根装载器(Bind + cooked 编码 + 真往返)。</summary>
    public static class TelemetryAnalyticsBake
    {
        /// <summary>作者态源文件名(单一出处;测试与菜单共用)。</summary>
        public const string SourceFileName = TelemetryAnalyticsBinder.SourceFileName;

        /// <summary>源文件所在目录(相对仓根;承既有先例 <c>assets/data/</c>)。</summary>
        public const string DataDirName = "assets/data";

        /// <summary>cooked 产物文件名(= Addressables address;单一出处 = <see cref="TelemetryAnalyticsDataSet"/>)。</summary>
        public static string CookedFileName => TelemetryAnalyticsDataSet.CookedFileName;

        /// <summary>烘焙输出(cooked 字节 + ConfigVersion + 绑定结果)。</summary>
        public readonly struct BakeOutput
        {
            /// <summary>cooked 字节(含 20 字节头)。</summary>
            public readonly byte[] Cooked;

            /// <summary>源数据集内容哈希派生的 ConfigVersion(u32)。</summary>
            public readonly uint ConfigVersion;

            /// <summary>绑定结果(六旋钮;测试直用)。</summary>
            public readonly TelemetryAnalyticsTuning Bound;

            /// <summary>构造烘焙输出。</summary>
            public BakeOutput(byte[] cooked, uint configVersion, in TelemetryAnalyticsTuning bound)
            {
                Cooked = cooked; ConfigVersion = configVersion; Bound = bound;
            }
        }

        /// <summary>从仓根绑定(读源 → 阶段 1 词法 → 阶段 2 白名单 + schema + 范围断言)。</summary>
        /// <exception cref="BakeValidationException">词法 / 未知键 / 缺组 / 范围违例(聚合)。</exception>
        public static TelemetryAnalyticsTuning BindFromRepo(string repoRoot)
            => TelemetryAnalyticsBinder.BindFromSourceText(ReadSource(repoRoot));

        /// <summary>从仓根烘焙(读源 → 绑定 + 范围断言 → 编码)。</summary>
        /// <exception cref="BakeValidationException">任一构建期拒收(聚合)。</exception>
        public static BakeOutput BakeFromRepo(string repoRoot) => Bake(ReadSource(repoRoot));

        /// <summary>从 JSON 串烘焙(不落盘;**与 <see cref="BakeFromRepo"/> 同一编码路径**)。</summary>
        /// <exception cref="BakeValidationException">任一构建期拒收(聚合)。</exception>
        public static BakeOutput BakeFromJson(string json) => Bake(json);

        private static BakeOutput Bake(string json)
        {
            TelemetryAnalyticsTuning bound = TelemetryAnalyticsBinder.BindFromSourceText(json);
            uint configVersion = ConfigVersionUtility.DeriveConfigVersion(new[]
            {
                new KeyValuePair<string, string>(SourceFileName, json),
            });
            byte[] cooked = TelemetryAnalyticsCookedWriter.Write(bound, CookedFormat.SchemaVersion, configVersion);
            return new BakeOutput(cooked, configVersion, bound);
        }

        private static string ReadSource(string repoRoot)
        {
            if (repoRoot == null) throw new ArgumentNullException(nameof(repoRoot));

            string path = Path.Combine(repoRoot, DataDirName, SourceFileName);
            if (!File.Exists(path))
                throw new BakeValidationException(
                    new List<string> { $"源文件不存在:{path}(GDD §Tuning Knobs:旋钮的落地位置 = 本文件)" });

            return File.ReadAllText(path);
        }

        /// <summary>烘焙输出 → 运行期数据集(**走真往返**:字节经
        /// <see cref="TelemetryAnalyticsCookedCodec.Read"/> 解回,再做跨侧逐项等值断言)。</summary>
        /// <remarks>⚠️ 写方与读方是两份手写布局(跨程序集);只比 `Bound` 与内存结构**不会**发现布局分叉
        /// (承 story-002 R-1 / 第十一片 F6 的形态)。本入口是两侧布局的**唯一自动兜现**。</remarks>
        /// <exception cref="BakeValidationException">跨侧逐项等值失败(布局分叉 / 编码丢字段)。</exception>
        public static TelemetryAnalyticsDataSet Materialize(in BakeOutput output)
        {
            AssertHeaderContract(output.Cooked);
            TelemetryAnalyticsDataSet dataSet = TelemetryAnalyticsCookedCodec.Read(output.Cooked);

            if (dataSet.ConfigVersion != output.ConfigVersion)
                throw new BakeValidationException(new List<string>
                {
                    $"cooked 头槽 ConfigVersion 0x{dataSet.ConfigVersion:X8} ≠ 烘焙期派生值 " +
                    $"0x{output.ConfigVersion:X8} —— 编码路径与派生路径分叉",
                });

            TelemetryAnalyticsTuning expected = output.Bound;
            TelemetryAnalyticsTuning actual = dataSet.Tuning;
            var errors = new List<string>();
            Compare("difficulty_curve.window_ticks", expected.DifficultyCurve.WindowTicks, actual.DifficultyCurve.WindowTicks, errors);
            Compare("difficulty_curve.step_ticks", expected.DifficultyCurve.StepTicks, actual.DifficultyCurve.StepTicks, errors);
            Compare("difficulty_curve.first_window_tick", expected.DifficultyCurve.FirstWindowTick, actual.DifficultyCurve.FirstWindowTick, errors);
            Compare("session_rhythm.gap_threshold_ticks", expected.GapThresholdTicks, actual.GapThresholdTicks, errors);
            Compare("skill_growth.growth_window_ticks", expected.GrowthWindowTicks, actual.GrowthWindowTicks, errors);
            Compare("sample.min_sample", expected.MinSample, actual.MinSample, errors);
            if (errors.Count > 0) throw new BakeValidationException(errors);
            return dataSet;
        }

        private static void Compare(string knob, long expected, long actual, List<string> errors)
        {
            if (expected != actual)
                errors.Add($"{knob} 往返后 {actual} ≠ 绑定值 {expected}");
        }

        /// <summary>cooked 头契约断言(魔数 / formatVersion / schemaVersion / payloadLength)。</summary>
        /// <exception cref="BakeValidationException">任一头字段不符。</exception>
        private static void AssertHeaderContract(byte[] cooked)
        {
            var errors = new List<string>();
            if (cooked == null || cooked.Length < CookedFormat.HeaderSize)
            {
                errors.Add("cooked 短于头部 —— 编码路径损坏");
                throw new BakeValidationException(errors);
            }
            if (cooked[0] != 'D' || cooked[1] != 'Y' || cooked[2] != 'J' || cooked[3] != 'C')
                errors.Add("cooked 魔数不符(≠ DYJC)");
            uint formatVersion = ReadU32(cooked, 4);
            if (formatVersion != CookedFormat.FormatVersion)
                errors.Add($"cooked formatVersion={formatVersion} ≠ 契约 {CookedFormat.FormatVersion}");
            uint schemaVersion = ReadU32(cooked, 8);
            if (schemaVersion != CookedFormat.SchemaVersion)
                errors.Add($"cooked 头槽 schemaVersion={schemaVersion} ≠ 契约 {CookedFormat.SchemaVersion} " +
                           "(承 DelayTableCookedWriter 的 R-1 教训:头槽不得硬写 0)");
            uint payloadLength = ReadU32(cooked, 16);
            if (payloadLength != cooked.Length - CookedFormat.HeaderSize)
                errors.Add($"cooked payloadLength={payloadLength} ≠ 实际 {cooked.Length - CookedFormat.HeaderSize}");
            if (errors.Count > 0) throw new BakeValidationException(errors);
        }

        private static uint ReadU32(byte[] buffer, int offset)
            => (uint)(buffer[offset] | (buffer[offset + 1] << 8) | (buffer[offset + 2] << 16) | (buffer[offset + 3] << 24));
    }
}
