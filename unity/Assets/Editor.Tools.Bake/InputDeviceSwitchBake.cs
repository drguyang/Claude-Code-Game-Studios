// TD-epic-01 第十一片 · 设备切换迟滞常量表的**仓根装载器**(阶段 1 词法 + 阶段 2 绑定 + 编码)。
//
// 权威来源:ADR-014 §三/§五(两阶段烘焙入口 · ConfigVersion = 源数据集内容哈希派生)
//          · InputDeviceSwitchBinder(唯一构建期校验点)
//          · InputDeviceSwitchCookedWriter(编码面)
//          · 先例 = AudioSettingsRegistryBake / DelayTable53Bake(同款「仓根装载器」形态)
//
// ⚠️ 本件刻意**不碰 UnityEngine / Addressables**:纯文件 I/O + 绑定 + 编码
//   (承 DelayTable53Bake 同纪律)⇒ EditMode 测试可直接驱动。
// ⚠️ **本件不落盘**:绑定 / 编码结果供测试与菜单消费;写盘归 `DataBakeMenu`。
// ⚠️ **本件是本 epic 的「临时种子」还债面**(2026-10-19):此前六旋钮只有 JSON + binder
//   + `CompositionRoot.EmergencyDeviceSwitchSeed*` 手抄常量,**零 cooked 制品、零装载路径**;
//   JSON 与常量表无机械对账 ⇒ 静默分叉。本件补齐烘焙侧,接线的运行期半边归
//   `AddressablesDataProvider` / `BootRoot`。

using System;
using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.Gameplay.Presentation;          // DeviceSwitchTuning / DeviceSwitchColumn
using DaYiJingCheng.Gameplay.Presentation.Input;    // DeviceSwitchTuningDataSet / Codec
using DaYiJingCheng.Sim.Contracts;   // CookedFormat(20 字节头布局的唯一共参照)

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary><c>input_device_switch.json</c> 的仓根装载器(Bind + cooked 编码 + 真往返)。</summary>
    public static class InputDeviceSwitchBake
    {
        /// <summary>作者态源文件名(单一出处;测试与菜单共用)。</summary>
        public const string SourceFileName = InputDeviceSwitchBinder.SourceFileName;

        /// <summary>源文件所在目录(相对仓根;承既有先例 <c>assets/data/</c>)。</summary>
        public const string DataDirName = "assets/data";

        /// <summary>cooked 产物文件名(= Addressables address;单一出处 = <see cref="DeviceSwitchTuningDataSet"/>)。</summary>
        public static string CookedFileName => DeviceSwitchTuningDataSet.CookedFileName;

        /// <summary>烘焙输出(cooked 字节 + ConfigVersion + 绑定结果)。</summary>
        public readonly struct BakeOutput
        {
            /// <summary>cooked 字节(含 20 字节头)。</summary>
            public readonly byte[] Cooked;

            /// <summary>源数据集内容哈希派生的 ConfigVersion(u32)。</summary>
            public readonly uint ConfigVersion;

            /// <summary>绑定结果(两列参数;测试直用)。</summary>
            public readonly DeviceSwitchTuning Bound;

            /// <summary>构造烘焙输出(写方 / 测试直用)。</summary>
            public BakeOutput(byte[] cooked, uint configVersion, DeviceSwitchTuning bound)
            {
                Cooked = cooked; ConfigVersion = configVersion; Bound = bound;
            }
        }

        /// <summary>从仓根绑定(读源 → 阶段 1 词法 → 阶段 2 两类校验 + 区间断言)。</summary>
        /// <exception cref="BakeValidationException">词法 / 未知键 / 缺列 / 区间违例(聚合)。</exception>
        public static DeviceSwitchTuning BindFromRepo(string repoRoot)
            => InputDeviceSwitchBinder.BindFromSourceText(ReadSource(repoRoot));

        /// <summary>从仓根烘焙(读源 → 绑定 + 区间断言 → 编码)。</summary>
        /// <exception cref="BakeValidationException">任一构建期拒收(聚合)。</exception>
        public static BakeOutput BakeFromRepo(string repoRoot)
            => Bake(ReadSource(repoRoot));

        /// <summary>从 JSON 串烘焙(不落盘;**与 <see cref="BakeFromRepo"/> 同一编码路径**)——
        /// 供测试驱动写侧性质(两列对调逐位一致 / 同源两次逐位一致)。</summary>
        /// <exception cref="BakeValidationException">任一构建期拒收(聚合)。</exception>
        public static BakeOutput BakeFromJson(string json) => Bake(json);

        private static BakeOutput Bake(string json)
        {
            DeviceSwitchTuning bound = InputDeviceSwitchBinder.BindFromSourceText(json);
            uint configVersion = ConfigVersionUtility.DeriveConfigVersion(new[]
            {
                new KeyValuePair<string, string>(SourceFileName, json),
            });
            byte[] cooked = InputDeviceSwitchCookedWriter.Write(bound, CookedFormat.SchemaVersion, configVersion);
            return new BakeOutput(cooked, configVersion, bound);
        }

        private static string ReadSource(string repoRoot)
        {
            if (repoRoot == null) throw new ArgumentNullException(nameof(repoRoot));

            string path = Path.Combine(repoRoot, DataDirName, SourceFileName);
            if (!File.Exists(path))
                throw new BakeValidationException(
                    new List<string> { $"源文件不存在:{path}(AC-3-D4:设备切换旋钮须有烘焙真源)" });

            return File.ReadAllText(path);
        }

        /// <summary>烘焙输出 → 运行期数据集(**走真往返**:字节经
        /// <see cref="DeviceSwitchTuningCookedCodec.Read"/> 解回,再做跨侧等值断言)。</summary>
        /// <remarks>⚠️ **为什么必须走真往返**:写方与读方是两份手写布局(跨程序集),
        /// 只比 `Bound` 与内存结构**不会**发现布局分叉 —— 那正是 story-002 R-1 的形态。
        /// 本入口是两侧布局的**唯一自动兜现**;逐项比对两列的三旋钮 raw 值。</remarks>
        /// <exception cref="BakeValidationException">跨侧逐项等值失败(布局分叉 / 编码丢字段)。</exception>
        public static DeviceSwitchTuningDataSet Materialize(in BakeOutput output)
        {
            AssertHeaderContract(output.Cooked);
            DeviceSwitchTuningDataSet dataSet = DeviceSwitchTuningCookedCodec.Read(output.Cooked);

            if (dataSet.ConfigVersion != output.ConfigVersion)
                throw new BakeValidationException(new List<string>
                {
                    $"cooked 头槽 ConfigVersion 0x{dataSet.ConfigVersion:X8} ≠ 烘焙期派生值 " +
                    $"0x{output.ConfigVersion:X8} —— 编码路径与派生路径分叉",
                });

            var errors = new List<string>();
            CompareColumn("pointer", output.Bound.Pointer, dataSet.Tuning.Pointer, errors);
            CompareColumn("axis", output.Bound.Axis, dataSet.Tuning.Axis, errors);
            if (errors.Count > 0) throw new BakeValidationException(errors);
            return dataSet;
        }

        /// <summary>单列跨侧逐项等值(固定字段序比对;任何一项不符即记错误)。</summary>
        private static void CompareColumn(string label, DeviceSwitchColumn expected, DeviceSwitchColumn actual,
                                          List<string> errors)
        {
            if (expected.DeviceSwitchThreshold.Raw != actual.DeviceSwitchThreshold.Raw)
                errors.Add($"{label}.device_switch_threshold 往返后 raw {actual.DeviceSwitchThreshold.Raw} " +
                           $"≠ 绑定值 {expected.DeviceSwitchThreshold.Raw}");
            if (expected.DriftTolerance.Raw != actual.DriftTolerance.Raw)
                errors.Add($"{label}.drift_tolerance 往返后 raw {actual.DriftTolerance.Raw} " +
                           $"≠ 绑定值 {expected.DriftTolerance.Raw}");
            if (expected.DwellTicks != actual.DwellTicks)
                errors.Add($"{label}.dwell_ticks 往返后 {actual.DwellTicks} ≠ 绑定值 {expected.DwellTicks}");
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
