// TD-epic-01 第十一片 · 设备切换迟滞常量表的**运行期载体**(烘焙产物的消费侧)。
//
// 权威来源:
//   ADR-014 §五(cooked 产物经 Addressables `data-core` 组装载 · 首次 Step 前预载)
//   GDD input-system.md §Tuning Knobs 一之二 · AC-3-D4(三旋钮 × 两设备类列)
//   `DeviceSwitchTuning`(绑定后的内存形 —— 本类只是它的**落盘/装载壳**)
//
// ⚠️ **本类补的是「临时种子」的还债面**(TD-epic-01 第十一片,2026-10-19):
//   在此之前,六旋钮**只有** `assets/data/input_device_switch.json` + `InputDeviceSwitchBinder`
//   (构建期校验)+ `CompositionRoot.EmergencyDeviceSwitchSeed*`(六个手抄常量)——
//   **零 cooked 制品、零运行期装载路径**。JSON 与常量表同值但**无任何机械对账**,
//   数值轮改一边不会改另一边 ⇒ 静默分叉。本类 = 那条缺失的装载面。
//
// ⚠️ **本类零 `UnityEngine` 引用**(承 `AudioSettingsDataSet` 同纪律)—— 纯数据载体,
//   EditMode 可直驱动;装载走 `AddressablesDataProvider`(那个类才碰 Addressables)。
//
// ⚠️ **数值纪律**:本类**不拍值**。值住 `assets/data/input_device_switch.json`
//   (临时种子,标「待数值轮」);`DeviceSwitchTuning.Validate` 只断 GDD 明写的**安全范围**。

using System;

namespace DaYiJingCheng.Gameplay.Presentation.Input
{
    /// <summary>设备切换迟滞常量表烘焙产物的运行期形(两列 + 双版本号)。</summary>
    public readonly struct DeviceSwitchTuningDataSet
    {
        /// <summary>cooked 产物文件名(= Addressables address;写方
        /// <c>Editor.Tools.Bake.DataBakeMenu</c> 与本常量共用 —— **单一出处**)。</summary>
        public const string CookedFileName = "input_device_switch.cooked.bytes";

        /// <summary>两列迟滞参数(指针列 / 轴列;Ⓑ.1 各自成表)。</summary>
        public readonly DeviceSwitchTuning Tuning;

        /// <summary>源 schema_version(头槽回声;读方已断言其 == 载荷首字段)。</summary>
        public readonly uint SchemaVersion;

        /// <summary>源数据集内容哈希派生的 ConfigVersion(ADR-014 §五)。</summary>
        public readonly uint ConfigVersion;

        /// <summary>构造(读方唯一调用点)。</summary>
        public DeviceSwitchTuningDataSet(DeviceSwitchTuning tuning, uint schemaVersion, uint configVersion)
        {
            Tuning = tuning;
            SchemaVersion = schemaVersion;
            ConfigVersion = configVersion;
        }
    }
}
