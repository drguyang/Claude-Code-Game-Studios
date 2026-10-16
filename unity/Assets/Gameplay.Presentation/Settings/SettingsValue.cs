// 权威来源:production/epics/audio-system/story-011-settings-exposure-reset.md
//   · 注册表 AC①(壳消费集 ⊆ settings_visible 闭枚举)· AC-44-13(mono)· AC-44-19(归零三步)
// GDD:design/gdd/audio-system.md §UI Requirements 设置暴露面注册表(:896-909)
//   · 8 行 = 7 总线音量(float 线性 0–1)+ mono_enabled(bool)
// ADR-018 §一(44 只渲染不持值)· ADR-013(控件呈现归 42)· ADR-014(出厂默认 = 烘焙分区)
// OQ-SS-5=甲(端本地 sidecar,持有者 = 边界层设置 store)· OQ-SS-7=甲(归零三步)
//
// ⚠️ **本类住 `Gameplay.Presentation`(边界层),禁住 `Sim`** ——
//    设置 = 设备偏好,与 sim 真值无关(GDD :1204「持有者 = 边界层设置 store」)。
//    sim 侧引用本类 = b2/b5 门级违例。
// ⚠️ **单一出处**:本类的类型闭集与 <see cref="AudioSettingsRegistry"/> 的行集必须一致,
//    由 AudioSettingsRegistryGates 在构建期对账(两处执行 = 两处分叉,承 ADR-024 §⑥)。

using System;

namespace DaYiJingCheng.Gameplay.Presentation.Settings
{
    /// <summary>设置值的**类型闭集**(注册表 <c>type</c> 字段的枚举;GDD :896-909 只用这两型)。</summary>
    public enum SettingsValueKind
    {
        /// <summary>线性浮点(总线音量;<c>0–1</c>,dB 换算归 <see cref="VolumeUnitMapping"/>)。</summary>
        Float = 0,

        /// <summary>布尔(mono 开关)。</summary>
        Bool = 1,
    }

    /// <summary>单个设置项的值(类型 + 数值)—— **无装箱**(承 ADR-029 §① 对载荷字段的同一取向:
    /// 值载体不用 <c>object</c>,免类型混淆与 GC 抖动)。
    /// <para>⚠️ 本类型**不是** <c>Fix</c> 域成员:设置值是设备偏好(表现层),不入 sim 定点域,
    /// 也不进三流 / 存档头(OQ-SS-5=甲:与存档位解耦)。</para></summary>
    public readonly struct SettingsValue : IEquatable<SettingsValue>
    {
        /// <summary>类型(闭集;构造后不可变)。</summary>
        public readonly SettingsValueKind Kind;

        /// <summary>线性浮点值(<see cref="SettingsValueKind.Float"/> 时有效;bool 时恒 0)。</summary>
        public readonly double Float;

        /// <summary>布尔值(<see cref="SettingsValueKind.Bool"/> 时有效;float 时恒 false)。</summary>
        public readonly bool Bool;

        private SettingsValue(SettingsValueKind kind, double f, bool b)
        {
            Kind = kind; Float = f; Bool = b;
        }

        /// <summary>构造浮点值。</summary>
        public static SettingsValue OfFloat(double value) =>
            new SettingsValue(SettingsValueKind.Float, value, false);

        /// <summary>构造布尔值。</summary>
        public static SettingsValue OfBool(bool value) =>
            new SettingsValue(SettingsValueKind.Bool, 0.0, value);

        /// <summary>序数相等(类型 + 数值双比;浮点用**精确位相等** —— 设置值是端本地回读,
        /// 不经任何算术,不存在容差需求)。</summary>
        public bool Equals(SettingsValue other) =>
            Kind == other.Kind && Float.Equals(other.Float) && Bool == other.Bool;

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is SettingsValue v && Equals(v);

        /// <inheritdoc/>
        public override int GetHashCode() => ((int)Kind * 397) ^ Float.GetHashCode() ^ Bool.GetHashCode();

        /// <summary>诊断用文本(错误消息里的值形态;不参与任何判据)。</summary>
        public override string ToString() =>
            Kind == SettingsValueKind.Bool ? (Bool ? "true" : "false")
                                           : Float.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
    }
}
