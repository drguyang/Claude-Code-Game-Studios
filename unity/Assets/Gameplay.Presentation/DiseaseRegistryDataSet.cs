// 9 病种注册表的 cooked 产物数据集(**运行期装载形**)—— 2026-10-22 B1 补立。
//
// 权威来源:
//   ADR-014 §二/§五(两阶段烘焙 · 运行期读烘焙产物,零 JSON 解析器)
//   ADR-006 §四(`Fix` 在 `.cooked` 里 = raw long,零 `FixParse`)
//   `CookedFormat.cs`(20 字节头布局)
//   写方镜像 = `Editor.Tools.Bake/DiseaseRegistryCookedWriter.cs`
//
// ⚠️ **住 `Gameplay.Presentation` 的理由**(与 `DeviceSwitchTuningDataSet` /
//   `TelemetryAnalyticsDataSet` 同款纪律):烘焙器(`Editor.Tools.Bake`)与运行期装载面
//   (`AddressablesDataProvider`)都引得到它。行类型 `DiseaseRegistryEntry` 住 `Sim`
//   —— 那正是本数据集要喂的下游(`DiseaseVitalsService` 构造参数),故不另立镜像行类型
//   (与 `DiseaseActionAxisDataSet` 的镜像行类型不同:那里的行类型只服务装配层过滤,
//   这里的行类型本身就是 sim 的输入形状)。
//
// ⚠️ 本件零 `UnityEngine` 引用 —— 纯数据载体,EditMode 可直驱。

using System.Collections.Generic;
using DaYiJingCheng.Sim;

namespace DaYiJingCheng.Gameplay.Presentation
{
    /// <summary>
    /// 病种注册表烘焙产物(<c>disease_registry.cooked.bytes</c>)的运行期数据集。
    /// <para>载荷 = 全局趋势窗口(<c>W</c> / <c>TREND_EPS</c>)+ 逐行病种条目。</para>
    /// </summary>
    public readonly struct DiseaseRegistryDataSet
    {
        /// <summary>产物文件名(= Addressables address;写盘名与装载名共用本常量)。</summary>
        public const string CookedFileName = "disease_registry.cooked.bytes";

        /// <summary>病种条目(入流序 = 作者态数组序;装配层据此建 id → 条目字典)。</summary>
        public readonly IReadOnlyList<DiseaseRegistryEntry> Rows;

        /// <summary>
        /// 全局 <c>W</c>(趋势差分窗口,tick;GDD Tuning §四 <c>:1743</c>)。
        /// <para><c>0</c> = <b>未配置</b>(数值轮未裁)⇒ F4 的痊愈支 / 迁延支不求值
        /// (<see cref="DaYiJingCheng.Sim.F4OutcomeEvaluator.RecoveryWindowInput.IsConfigured"/>)。
        /// <c>0</c> 是**合法值**,不是错误 —— 装配层不得代拍默认值。</para>
        /// </summary>
        public readonly long TrendWindowTicks;

        /// <summary>全局 <c>TREND_EPS</c>(趋势死区,Q16.16 raw;GDD Tuning §四 <c>:1744</c>)。
        /// <para><c>0</c> = 未配置。</para></summary>
        public readonly long TrendEpsRaw;

        /// <summary>头槽 offset 8 的 schema_version(作者态回声)。</summary>
        public readonly uint SchemaVersion;

        /// <summary>头槽 offset 12 的 configVersion(源内容哈希派生;与存档头比对走 ADR-010 §七)。</summary>
        public readonly uint ConfigVersion;

        /// <summary>构造(不校验 —— 校验在 <c>DiseaseRegistryCookedCodec.Read</c> 内完成)。</summary>
        public DiseaseRegistryDataSet(IReadOnlyList<DiseaseRegistryEntry> rows,
                                      long trendWindowTicks, long trendEpsRaw,
                                      uint schemaVersion, uint configVersion)
        {
            Rows = rows;
            TrendWindowTicks = trendWindowTicks;
            TrendEpsRaw = trendEpsRaw;
            SchemaVersion = schemaVersion;
            ConfigVersion = configVersion;
        }
    }
}
