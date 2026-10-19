// TD-epic-01 第十二片 · 51 遥测 F4–F7 参数旋钮表的**运行期载体**(烘焙产物的消费侧)。
//
// 权威来源:
//   ADR-014 §五(cooked 产物经 Addressables `data-core` 组装载 · 首次 Step 前预载)
//   GDD `telemetry-analytics.md` §Tuning Knobs(「旋钮的落地位置 = assets/data/telemetry_analytics.json」)
//   AC-51-B14(`W < 1` / `S < 1` ⇒ **加载期**硬失败 —— 本表在 codec 里再执法一次)
//
// ⚠️ **本类补的是「F4–F7 参数待定」的还债面**(TD-epic-01 第十二片,2026-10-19):
//   GDD 声明的落地位置 `assets/data/telemetry_analytics.json` 此前**从未存在** ⇒
//   AC-51-B14 的「配置加载」半边**没有对象**,AC-51-E5 只能靠「文件不存在 ⇒ 无开关」恒真通过。
// ⚠️ **本类零 `UnityEngine` 引用**(承 `AudioSettingsDataSet` 同纪律);装载走 `AddressablesDataProvider`。

namespace DaYiJingCheng.Gameplay.Presentation.Telemetry
{
    /// <summary>遥测参数旋钮表烘焙产物的运行期形(六旋钮 + 双版本号)。</summary>
    public readonly struct TelemetryAnalyticsDataSet
    {
        /// <summary>cooked 产物文件名(= Addressables address;写方
        /// <c>Editor.Tools.Bake.DataBakeMenu</c> 与本常量共用 —— **单一出处**)。</summary>
        public const string CookedFileName = "telemetry_analytics.cooked.bytes";

        /// <summary>六旋钮(四组)。</summary>
        public readonly TelemetryAnalyticsTuning Tuning;

        /// <summary>源 schema_version(头槽回声;读方已断言其 == 载荷首字段)。</summary>
        public readonly uint SchemaVersion;

        /// <summary>源数据集内容哈希派生的 ConfigVersion(ADR-014 §五)。</summary>
        public readonly uint ConfigVersion;

        /// <summary>构造(读方唯一调用点)。</summary>
        public TelemetryAnalyticsDataSet(in TelemetryAnalyticsTuning tuning, uint schemaVersion, uint configVersion)
        {
            Tuning = tuning;
            SchemaVersion = schemaVersion;
            ConfigVersion = configVersion;
        }
    }
}
