// S14-6 · story-010 —— 设置暴露面注册表的**运行期载体**(烘焙产物的消费侧)。
//
// 权威来源:
//   AC-44-D4(注册表走 ADR-014 烘焙管线;玩家构建**零 JSON 解析器** —— 本类只吃 cooked 字节)
//   ADR-014 §五(cooked 产物经 Addressables `data-core` 组装载)
//   AC-44-13(出厂默认入烘焙分区)· OQ-SS-7=甲(默认值真源 = 烘焙分区)
//   `AudioSettingsRegistry.Provider` 缝(story-011 立;本类 = 该缝的运行期供给面)
//
// ⚠️ **本类与 `AudioSettingsRegistry.FallbackRows` 的关系**(勿混读):
//   本类 = **烘焙分区**(真源,AC-44-13);`FallbackRows` = 「烘焙数据不可用」时的回退。
//   两者同值由**构建期**对账(`AudioSettingsRegistryBinder.ValidateValueReconciliation`,
//   双向逐键)钉住 ⇒ 「回退静默漂移」不可能发生。运行期 `Provider` 常驻后回退退居二线。
//
// ⚠️ **本类零 `UnityEngine` 引用**(承 `AudioSettingsRegistry` / `AudioEventTable` 同纪律)——
//   纯数据载体,EditMode 可直驱动;装载走 `AddressablesDataProvider`(那个类才碰 Addressables)。
// ⚠️ **本类不含 `AudioCueDto` / `IAudioCueSink` / `AudioCueHandle` / `TierSource` 四个 token**
//   (b5 逃逸段的扫描面 —— 不含即无「声明了 44 契约却住别处」的形态)。

using System;
using System.Collections.Generic;

namespace DaYiJingCheng.Gameplay.Presentation.Settings
{
    /// <summary><c>settings_visible</c> 注册表烘焙产物的运行期形(行集 + 双版本号)。</summary>
    public readonly struct AudioSettingsDataSet
    {
        /// <summary>cooked 产物文件名(= Addressables address;写方
        /// <c>Editor.Tools.Bake.DataBakeMenu</c> 与本常量共用 —— **单一出处**)。</summary>
        public const string CookedFileName = "audio_settings_registry.cooked.bytes";

        private readonly SettingsRegistryRow[] _rows;

        /// <summary>源 schema_version(头槽回声;读方已断言其 == 载荷首字段)。</summary>
        public readonly uint SchemaVersion;

        /// <summary>源数据集内容哈希派生的 ConfigVersion(ADR-014 §五)。</summary>
        public readonly uint ConfigVersion;

        /// <summary>构造(读方唯一调用点)。</summary>
        public AudioSettingsDataSet(SettingsRegistryRow[] rows, uint schemaVersion, uint configVersion)
        {
            _rows = rows; SchemaVersion = schemaVersion; ConfigVersion = configVersion;
        }

        /// <summary>行集(按 <c>param</c> 的 Ordinal 升序 —— 编码序,见写方文件头)。
        /// <para>⚠️ 返回的列表**不保证非空**:空集是写方显式拒绝的形态(见
        /// `AudioSettingsRegistryCookedWriter.Write` 的空集硬失败),故运行期出现空集 = 上游绕过了写方。</para></summary>
        public IReadOnlyList<SettingsRegistryRow> Rows =>
            _rows ?? (IReadOnlyList<SettingsRegistryRow>)Array.Empty<SettingsRegistryRow>();

        /// <summary>行数。</summary>
        public int RowCount => _rows?.Length ?? 0;

        /// <summary>按编码序取行。</summary>
        /// <exception cref="ArgumentOutOfRangeException">index 越界(调用方须按名查 —— 本索引仅为遍历面)。</exception>
        public SettingsRegistryRow Row(int index)
        {
            if (_rows == null || index < 0 || index >= _rows.Length)
                throw new ArgumentOutOfRangeException(nameof(index), index, $"注册表行索引越界(共 {RowCount} 行)");
            return _rows[index];
        }
    }
}
