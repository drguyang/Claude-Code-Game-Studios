// 权威来源:production/epics/audio-system/story-011-settings-exposure-reset.md
//   · 注册表 AC①(壳消费集 ⊆ settings_visible 闭枚举,8 条)· AC-44-13(mono)
// GDD:design/gdd/audio-system.md §UI Requirements 设置暴露面注册表(:896-909)
//   · 「**单一出处**:44 拥有闭枚举 settings_visible(壳 settings-shell-42.md 的行目录 = 本表**镜像**)」
//   · 行含:参数名 / 类型 / 区间(0–1,dB −80–0)/ 出厂默认 / player_group / 题签语义源
// ADR-014 §五(出厂默认 = 烘焙分区)· ADR-018 §一(44 只渲染不持值)· OQ-SS-3=甲(数据层恒 7 路)
//
// ⚠️ **名字半边 vs 值半边**(刻意的两处落点,勿混读):
//   · **名字**闭集 = <see cref="MixerRegistry.SettingsVisibleParameters"/>(代码常量;mixer
//     SetFloat / FindSnapshot 都要按名字查表 ⇒ 必须编译期可见)。本类**引用**它,禁另抄。
//   · **值**半边(出厂默认) = `assets/data/audio_settings_registry.json`(**烘焙分区**,
//     ADR-014;AC-44-13 逐字「出厂默认入烘焙分区」)⇒ 经 <see cref="Provider"/> 缝注入。
// ⚠️ <see cref="FallbackRows"/> 是**「烘焙数据不可用」时的回退**,不是真源 ——
//    它刻意与作者态 JSON 同值,由 AudioSettingsRegistryGates 在构建期对账(两处不一致 = 构建失败),
//    使「回退静默漂移」不可能发生。story-010 接线后 Provider 常驻,回退退居二线。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Presentation.Audio;   // MixerRegistry(名字闭集的单一出处)

namespace DaYiJingCheng.Gameplay.Presentation.Settings
{
    /// <summary><c>settings_visible</c> 注册表的一行(GDD :896-909 表的机器形)。
    /// <para>⚠️ 本类型**不是** <c>Fix</c> 域成员:设置值是端本地设备偏好(表现层),
    /// 不入 sim 定点域、不进三流、不进 ADR-010 存档头(OQ-SS-5=甲)。</para></summary>
    public readonly struct SettingsRegistryRow
    {
        /// <summary>exposed 参数名(∈ <see cref="MixerRegistry.SettingsVisibleParameters"/>)。</summary>
        public readonly string Param;

        /// <summary>类型闭集(注册表 <c>type</c> 字段)。</summary>
        public readonly SettingsValueKind Kind;

        /// <summary>区间下界(仅 float 行有效;GDD :896-909「0–1」)。</summary>
        public readonly double Min;

        /// <summary>区间上界(仅 float 行有效)。</summary>
        public readonly double Max;

        /// <summary>出厂默认(AC-44-13「出厂默认入烘焙分区」—— 本字段的值来自烘焙数据)。</summary>
        public readonly SettingsValue FactoryDefault;

        /// <summary><c>player_group</c>(OQ-SS-3=甲:数据层恒 7 路,**分组只影响壳呈现**,归 42 元件轮)。
        /// 本字段只承载,不解释语义。</summary>
        public readonly string PlayerGroup;

        /// <summary>题签 / 页背注语义源(壳「44 给语义」;文案归 narrative)。</summary>
        public readonly string LabelSource;

        /// <summary>构造一行。</summary>
        public SettingsRegistryRow(string param, SettingsValueKind kind, double min, double max,
            SettingsValue factoryDefault, string playerGroup, string labelSource)
        {
            Param = param; Kind = kind; Min = min; Max = max;
            FactoryDefault = factoryDefault; PlayerGroup = playerGroup; LabelSource = labelSource;
        }
    }

    /// <summary><c>settings_visible</c> 注册表(44 侧闭枚举的**运行期载体**)。
    /// <para>⚠️ **只读消费者** —— 本类不持值(值真源 = <see cref="AudioSettingsStore"/>)、
    /// 不写盘(归 <see cref="AudioSettingsSidecar"/>);它只回答「有哪些参数、各自默认多少」。</para></summary>
    public static class AudioSettingsRegistry
    {
        /// <summary>烘焙注册表的注入缝(承 <c>PlayerBusVolumeInitializer.MixerResolver</c> 同款模式)。
        /// <para>**S14-6(2026-10-16)已接线**:`BootRoot` 在 `DataCorePreloader.TryPreload()` 成功后
        /// 注入 <c>provider.Load&lt;AudioSettingsDataSet&gt;().Rows</c>(返回烘焙分区的 8 行)。
        /// **未注入 = 用 <see cref="FallbackRows"/>(不硬找资源、不读 JSON —— 玩家构建零解析器)。</para></summary>
        public static Func<IReadOnlyList<SettingsRegistryRow>> Provider;

        /// <summary>注册表 8 行的参数名(**单一出处** = <see cref="MixerRegistry.SettingsVisibleParameters"/>;
        /// 本属性只是转发,禁在此另抄一份字面量)。</summary>
        public static IReadOnlyList<string> ParamNames => MixerRegistry.SettingsVisibleParameters;

        /// <summary>注册表**当前生效**的行集(Provider 已注入 ⇒ 烘焙数据;否则回退)。</summary>
        public static IReadOnlyList<SettingsRegistryRow> Rows
        {
            get
            {
                Func<IReadOnlyList<SettingsRegistryRow>> provider = Provider;
                if (provider != null)
                {
                    IReadOnlyList<SettingsRegistryRow> rows = provider();
                    if (rows != null && rows.Count > 0) return rows;
                }
                return FallbackRows;
            }
        }

        /// <summary>「烘焙数据不可用」时的回退行集(**非真源**;与作者态 JSON 同值,由构建期门对账)。
        /// <para>总线 7 行:线性 <c>1.0</c>(= 0 dB 中性,种子值);mono 行:<c>false</c>(关)。</para></summary>
        public static readonly IReadOnlyList<SettingsRegistryRow> FallbackRows = BuildFallbackRows();

        private static IReadOnlyList<SettingsRegistryRow> BuildFallbackRows()
        {
            var rows = new List<SettingsRegistryRow>(MixerRegistry.BusVolumeParameters.Count + 1);
            foreach (string param in MixerRegistry.BusVolumeParameters)
            {
                rows.Add(new SettingsRegistryRow(
                    param, SettingsValueKind.Float, 0.0, 1.0,
                    SettingsValue.OfFloat(1.0), "bus", "44 注册表行(壳「44 给语义」)"));
            }
            rows.Add(new SettingsRegistryRow(
                MixerRegistry.MonoParameter, SettingsValueKind.Bool, 0.0, 0.0,
                SettingsValue.OfBool(false), "a11y", "44 注册表行(壳「44 给语义」);AC-44-13"));
            return rows;
        }

        /// <summary>取指定参数的行;未登记 ⇒ false(调用方据此判「越集」)。</summary>
        /// <param name="param">exposed 参数名。</param>
        /// <param name="row">出口:命中行。</param>
        public static bool TryGetRow(string param, out SettingsRegistryRow row)
        {
            IReadOnlyList<SettingsRegistryRow> rows = Rows;
            for (int i = 0; i < rows.Count; i++)
            {
                if (string.Equals(rows[i].Param, param, StringComparison.Ordinal))
                {
                    row = rows[i];
                    return true;
                }
            }
            row = default;
            return false;
        }

        /// <summary>出厂默认集(总线音量部分,供 mixer 单向推送)。
        /// <para>⚠️ 返回的**是线性值**(注册表域 <c>0–1</c>);推 mixer 前须经
        /// <see cref="VolumeUnitMapping"/> 换算成 dB —— 调用方不得直接 <c>SetFloat</c> 线性值。</para></summary>
        public static IReadOnlyList<KeyValuePair<string, double>> FactoryVolumeDefaults()
        {
            var result = new List<KeyValuePair<string, double>>(MixerRegistry.BusVolumeParameters.Count);
            IReadOnlyList<SettingsRegistryRow> rows = Rows;
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Kind != SettingsValueKind.Float) continue;
                result.Add(new KeyValuePair<string, double>(
                    rows[i].Param, rows[i].FactoryDefault.Float));
            }
            return result;
        }
    }
}
