// Story 008 · 设备切换迟滞常量表(§Tuning Knobs 一之二 · Ⓑ.1 两列结构)
//
// 权威来源:GDD input-system.md §Tuning Knobs 一之二(三旋钮 × 两设备类列)·
//   AC-3-D4(「三参数按设备类分两列(ADR-014 常量表)」)。
//   落点注记(承 AxisTuning 同一先例,ADR-025 §①):Gameplay.Input 不引 Sim.Contracts
//   ⇒ 承载 Fix 的常量表住 Gameplay.Presentation;运行期机制(DeviceStateManager)只收
//   Q16.16 int(构造注入),与本表解耦。
// 数值纪律:本结构不拍值 —— 值住 assets/data/input_device_switch.json(临时种子,
//   标「待数值轮」);Validate 只断 GDD 明写的**安全范围**(不破设计的边界),非好玩值。
//   DZ_INNER 交叉建议(「DRIFT_TOLERANCE 建议 ≤ DZ_INNER」)= 建议非硬断言,
//   不在此执法(登记:跨表建议,数值轮同批看两表)。

using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Presentation
{
    /// <summary>一列迟滞参数(设备类维度的三旋钮;两列实例 = Pointer / Axis)。
    /// 构造不校验 —— 装载期断言机械门在 InputDeviceSwitchBinder(ADR-014 硬失败),
    /// 直接构造的合法内存路径 = 测试负例(失效签名)。</summary>
    public readonly struct DeviceSwitchColumn
    {
        /// <summary>DEVICE_SWITCH_THRESHOLD(径向 ‖Δ‖ 域)。</summary>
        public Fix DeviceSwitchThreshold { get; }

        /// <summary>DRIFT_TOLERANCE(同径向域)。</summary>
        public Fix DriftTolerance { get; }

        /// <summary>DWELL_DURATION(int 计数,20 Hz ⇒ 1 tick = 50 ms;int 属 ADR-006 D-21-17
        /// 计数口径,不走 Fix 解析)。</summary>
        public int DwellTicks { get; }

        /// <summary>直接构造(不做区间校验,见类型头)。</summary>
        public DeviceSwitchColumn(Fix deviceSwitchThreshold, Fix driftTolerance, int dwellTicks)
        {
            DeviceSwitchThreshold = deviceSwitchThreshold;
            DriftTolerance = driftTolerance;
            DwellTicks = dwellTicks;
        }

        /// <summary>单列安全范围断言(GDD 表「安全范围」列的机械形式;全比较整数域 Raw)。</summary>
        /// <param name="label">错误前缀(列名 / 文件名)。</param>
        /// <param name="checkAxisUpperBound">true = 轴列(threshold 须 &lt; 1,GDD「≥1 永锁首设备」);
        /// false = 指针列(量纲 = 像素位移,无 1 上界语义,该条跳过)。</param>
        public List<string> Validate(string label, bool checkAxisUpperBound)
        {
            var errors = new List<string>();
            string tag = string.IsNullOrEmpty(label) ? string.Empty : label + ":";

            // ① DEVICE_SWITCH_THRESHOLD > 0(两端不可取的左端;=0 ⇒ 迟滞消失漂移即夺源)
            if (DeviceSwitchThreshold.Raw <= 0)
                errors.Add($"{tag}DEVICE_SWITCH_THRESHOLD 必须 > 0(raw={DeviceSwitchThreshold.Raw})" +
                           "—— = 0 迟滞消失,漂移即夺源(GDD §一之二 Ⓐ)");

            // ② 轴列右端:< 1(≥1 任何轴到不了 ⇒ 永锁首个被看见的设备,Mixed 名存实亡)
            if (checkAxisUpperBound && DeviceSwitchThreshold.Raw >= Fix.OneRaw)
                errors.Add($"{tag}轴列 DEVICE_SWITCH_THRESHOLD 必须 < 1(raw={DeviceSwitchThreshold.Raw})" +
                           "—— ≥1 任何轴都到不了 ⇒ 永锁首设备(GDD §一之二)");

            // ③ DRIFT_TOLERANCE > 0
            if (DriftTolerance.Raw <= 0)
                errors.Add($"{tag}DRIFT_TOLERANCE 必须 > 0(raw={DriftTolerance.Raw})" +
                           "—— 太大轻推即夺源(GDD §一之二)");

            // ④ DWELL_DURATION ≥ 0(tick 计数非负;判据侧为严格「>」,0 值 = 最快档,仍合法)
            if (DwellTicks < 0)
                errors.Add($"{tag}DWELL_DURATION 必须 ≥ 0(ticks={DwellTicks})—— 负持时非物理");

            return errors;
        }
    }

    /// <summary>两列迟滞常量表(Ⓑ.1:两列各自成表,不得共用一行 —— 结构上以两个独立
    /// <see cref="DeviceSwitchColumn"/> 承载,无共享字段)。</summary>
    public readonly struct DeviceSwitchTuning
    {
        /// <summary>指针列(鼠标:逐帧 ‖pointer_delta‖ 量纲)。</summary>
        public DeviceSwitchColumn Pointer { get; }

        /// <summary>轴列(摇杆/触控板:归一化轴幅度量纲)。</summary>
        public DeviceSwitchColumn Axis { get; }

        /// <summary>以两列构造(不校验)。</summary>
        public DeviceSwitchTuning(DeviceSwitchColumn pointer, DeviceSwitchColumn axis)
        {
            Pointer = pointer;
            Axis = axis;
        }

        /// <summary>两列全断言(装载期;违例聚合返回,空 = 合法)。</summary>
        public List<string> Validate(string label = null)
        {
            var errors = new List<string>();
            string tag = string.IsNullOrEmpty(label) ? string.Empty : label + ":";
            errors.AddRange(Pointer.Validate(tag + "pointer", checkAxisUpperBound: false));
            errors.AddRange(Axis.Validate(tag + "axis", checkAxisUpperBound: true));
            return errors;
        }
    }
}
