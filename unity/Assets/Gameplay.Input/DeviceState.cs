// Story 008 · 设备态(§States 一 五态 + Mixed「最近一次有效输入」+ D4 迟滞的装配方)
//
// 权威来源:
//   GDD input-system.md §States 一(五态表:KbmOnly / PadOnly / Mixed / XrActive / NoDevice;
//     「Mixed 以最近一次有效输入来源为准,不叠加不双活 —— 必裁口径,非实现自由」)·
//   §Tuning Knobs 一之二(DEVICE_SWITCH_THRESHOLD / DRIFT_TOLERANCE / DWELL_DURATION;
//     Ⓑ 比较域 = 径向 ‖Δ‖ 禁逐分量;Ⓑ.1 鼠标侧指针增量单列 / 轴设备侧轴幅度单列,
//     两列各自成表 —— 常量表按设备类分列登记)· AC-3-D4。
//   GDD §States 头部 I2 作用域注记:设备态(连接 / Mixed 迟滞计时)= 3 **自有域**,
//     由 AC-3-D4 守门,不是「无外部状态意图源」不变量的违反面。
//   Story 008 Implementation Notes:「态转移由直读通道观察到的有效输入驱动」—— 本类暴露
//     观察入口(Observe*),接线由装配层完成;本故事**不触碰** EmergencyDirectReadChannel。
//
// 数值纪律(项目铁律「数值用户自己调」):三旋钮的具体数值**不在本文件**拍定 ——
//   构造方显式注入 Q16.16 整数(承 Story 007 EmergencyDirectReadChannel 同型先例);
//   临时种子值住 assets/data/input_device_switch.json(两列 × 三旋钮,标「待数值轮」)。
//
// ⚠️ XrActive 进入条件原文 = 「OpenXR session 开始」;本机 P0 未安装 com.unity.xr.openxr
//   (Story 008 spike 结论,见 Completion Notes)⇒ 本类以 **XRController 布局设备在联**
//   作为可测近似(布局住 Unity.InputSystem 运行期,零 OpenXR 依赖)。真 session 事件接线
//   归 VR 轮(P1b),届时替换本近似 —— 不新增对外 API 形状之外的承诺。

using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace DaYiJingCheng.Gameplay.Input
{
    /// <summary>设备态五值(GDD §States 一;3 拥有的自有域状态,I2 明确豁免 —— 见文件头)。</summary>
    public enum DeviceState
    {
        /// <summary>仅键鼠在联。</summary>
        KbmOnly = 0,

        /// <summary>仅手柄在联(QueryBinding 的 device 返回 Pad)。</summary>
        PadOnly = 1,

        /// <summary>键鼠与手柄皆在联 —— 以**最近一次有效输入**来源为准,不叠加不双活。</summary>
        Mixed = 2,

        /// <summary>XR 在联(P0 = XRController 设备在联的近似,见文件头 ⚠️)。</summary>
        XrActive = 3,

        /// <summary>全部断开(合成 release 归 <see cref="DeviceReleaseGuard"/>;不冻结帧)。</summary>
        NoDevice = 4,
    }

    /// <summary>Mixed 内的「来源家族」刻度(设备态五值的来源轴;Kbm = 键盘+鼠标合并家族,
    /// 与 §States 一 的键鼠/手柄两分一致)。</summary>
    public enum DeviceSourceKind
    {
        /// <summary>尚无任何有效输入(Mixed 刚建立,来源未定)。</summary>
        None = 0,

        /// <summary>键鼠家族(键盘 / 鼠标 / 指针)。</summary>
        Kbm = 1,

        /// <summary>手柄家族(Gamepad 布局族)。</summary>
        Pad = 2,

        /// <summary>XR 家族(XRController 布局;迟滞走轴列,见 Ⓑ.1 注)。</summary>
        Xr = 3,
    }

    /// <summary>设备态管理器(§States 一):连接簿记(Added / Removed / Disconnected / Reconnected)
    /// + Mixed 的「最近一次有效输入」来源跟踪 + D4 迟滞的装配。构造不拍值:迟滞三旋钮按
    /// 设备类两列由构造方注入(临时值与区间断言住常量表,见 Story 文件)。</summary>
    /// <remarks>
    /// <para><b>五态推导(纯函数 <see cref="DeriveState"/>)</b>:XrActive 优先(XR 在联)→
    /// Mixed(键鼠 ∧ 手柄)→ KbmOnly / PadOnly(单家族)→ NoDevice(全无)。
    /// XR 与键鼠/手柄同时在场时 XR 抢占(XrActive 行「绑 XRController 布局;进入站定式急救」
    /// 的语义),登记注记:此优先序 GDD 未逐字,取「进入 XR session 即以 XR 为准」读法。</para>
    /// <para><b>有效输入 → 迟滞(AC-3-D4)</b>:<see cref="ObserveAxis"/> /
    /// <see cref="ObservePointer"/> 每 tick 一次由观察方(直读通道侧)驱动;
    /// 三条件(幅度 &gt; DEVICE_SWITCH_THRESHOLD ∧ &gt; DRIFT_TOLERANCE ∧ 持续 &gt; DWELL_DURATION)
    /// 全部**严格大于**才夺源;比较域 = 径向 ‖Δ‖(实现方 <see cref="DeviceSwitchHysteresis"/>,
    /// 禁逐分量)。鼠标列与轴列各用各的旋钮值(Ⓑ.1 两列)。</para>
    /// <para><b>数字输入(键盘按键)</b>:<see cref="ObserveDigital"/> 无漂移问题(数字键不存在
    /// 静息漂移),但**仍走同一迟滞机制**(幅度按满幅 1.0 计,持按逐 tick 喂)—— 快速轻点不满
    /// DWELL 即不夺源,与 Ⓑ 的三条件原文一致;§一之二「有意轻推另一设备仍会切源」的登记后果
    /// 在持满 DWELL 后成立。</para>
    /// <para><b>来源迁移(AC-3-D3 Edge「断连的设备是当前 Mixed 源」)</b>:来源家族清空时,
    /// 迁移到**存活家族**(别无可选);迁移不是「有效输入夺源」,不经迟滞。</para>
    /// </remarks>
    public sealed class DeviceStateManager
    {
        private readonly DeviceSwitchHysteresis _hysteresis;
        private readonly HashSet<InputDevice> _present = new HashSet<InputDevice>();
        private bool _attached;

        /// <summary>按两列迟滞旋钮构造(数值显式注入,构造不拍值)。</summary>
        /// <param name="axisThresholdQ16">轴列 DEVICE_SWITCH_THRESHOLD(Q16.16;开区间 (0,1) 的值域断言归常量表装载门)。</param>
        /// <param name="axisDriftToleranceQ16">轴列 DRIFT_TOLERANCE(Q16.16,&gt; 0)。</param>
        /// <param name="axisDwellTicks">轴列 DWELL_DURATION(tick 计数,20 Hz 标定;严格大于语义见 <see cref="DeviceSwitchHysteresis"/>)。</param>
        /// <param name="pointerThresholdQ16">指针列 DEVICE_SWITCH_THRESHOLD(Q16.16 指针位移 &gt; 0)。</param>
        /// <param name="pointerDriftToleranceQ16">指针列 DRIFT_TOLERANCE(Q16.16 &gt; 0)。</param>
        /// <param name="pointerDwellTicks">指针列 DWELL_DURATION(tick 计数)。</param>
        public DeviceStateManager(int axisThresholdQ16, int axisDriftToleranceQ16, int axisDwellTicks,
                                  int pointerThresholdQ16, int pointerDriftToleranceQ16, int pointerDwellTicks)
        {
            _hysteresis = new DeviceSwitchHysteresis(
                axisThresholdQ16, axisDriftToleranceQ16, axisDwellTicks,
                pointerThresholdQ16, pointerDriftToleranceQ16, pointerDwellTicks);
        }

        /// <summary>当前设备态(五值之一;由在场家族推导)。</summary>
        public DeviceState State { get; private set; } = DeviceState.NoDevice;

        /// <summary>当前来源家族(Mixed 的「最近一次有效输入」;非 Mixed 时 = 在场家族或 None)。</summary>
        public DeviceSourceKind ActiveSource { get; private set; } = DeviceSourceKind.None;

        /// <summary>是否已挂 <c>InputSystem.onDeviceChange</c>(恰一次;重复挂 = 抛)。</summary>
        public bool IsAttached => _attached;

        /// <summary>挂接设备变更事件并按 <c>InputSystem.devices</c> 快照重建在场集。
        /// 装配层在动作资产启用后挂一次(与 <see cref="DeviceReleaseGuard"/> 各挂各的,互不知晓)。</summary>
        /// <exception cref="InvalidOperationException">重复挂接。</exception>
        public void Attach()
        {
            if (_attached)
                throw new InvalidOperationException("DeviceStateManager 已挂接 onDeviceChange,不可重复挂(恰一次)。");
            _attached = true;
            _present.Clear();
            foreach (var dev in InputSystem.devices)
                if (FamilyOf(dev) != DeviceSourceKind.None) _present.Add(dev);
            Recalculate();
            InputSystem.onDeviceChange += OnDeviceChange;
        }

        /// <summary>卸下事件(未挂 = 幂等空操作)。</summary>
        public void Detach()
        {
            if (!_attached) return;
            _attached = false;
            InputSystem.onDeviceChange -= OnDeviceChange;
        }

        /// <summary>轴列观察(摇杆 / XR  stick;dx/dy 为**径向判定前的分量**,类内部只取
        /// ‖Δ‖ 模长比较 —— 逐分量判据被 Ⓑ 明禁,故分量在这里就丢掉)。每 tick 至多一次。</summary>
        /// <param name="candidate">候选来源家族(Pad / Xr)。</param>
        /// <param name="dxQ16">轴 Δx(Q16.16,归一化域)。</param>
        /// <param name="dyQ16">轴 Δy(Q16.16)。</param>
        /// <returns>true = 本 tick 发生夺源(ActiveSource 已更新)。</returns>
        public bool ObserveAxis(DeviceSourceKind candidate, int dxQ16, int dyQ16)
            => Observe(candidate, dxQ16, dyQ16, axisColumn: true);

        /// <summary>指针列观察(鼠标逐帧增量 ‖Δpointer‖;量纲 = 指针位移 Q16.16,独立于轴
        /// 归一化域 —— Ⓑ.1 单列)。</summary>
        public bool ObservePointer(int dxQ16, int dyQ16)
            => Observe(DeviceSourceKind.Kbm, dxQ16, dyQ16, axisColumn: false);

        /// <summary>数字输入观察(键盘按键持按,每 tick 调用一次;幅度按满幅计,见类头 ⚠️ 数字输入段)。</summary>
        public bool ObserveDigital(DeviceSourceKind candidate)
            => Observe(candidate, DeviceSwitchHysteresis.FullAxisQ16, 0, axisColumn: true);

        private bool Observe(DeviceSourceKind candidate, int dxQ16, int dyQ16, bool axisColumn)
        {
            if (candidate == DeviceSourceKind.None) return false;
            if (!FamilyPresent(candidate)) return false;   // 候选家族不在场 ⇒ 无源可夺
            bool switched = _hysteresis.Observe(ActiveSource, candidate, dxQ16, dyQ16, axisColumn);
            if (switched) ActiveSource = candidate;
            return switched;
        }

        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            switch (change)
            {
                case InputDeviceChange.Added:
                case InputDeviceChange.Reconnected:
                    if (FamilyOf(device) != DeviceSourceKind.None) _present.Add(device);
                    Recalculate();
                    break;
                case InputDeviceChange.Removed:
                case InputDeviceChange.Disconnected:
                    _present.Remove(device);
                    Recalculate();
                    break;
                // 其余变更(UsageChanged / ConfigurationChanged / SoftReset / …)不动在场集。
            }
        }

        /// <summary>在场 → 五态 + 来源迁移(类头两段口径的执行处)。</summary>
        private void Recalculate()
        {
            bool kbm = CountOfFamily(DeviceSourceKind.Kbm) > 0;
            bool pad = CountOfFamily(DeviceSourceKind.Pad) > 0;
            bool xr = CountOfFamily(DeviceSourceKind.Xr) > 0;
            State = DeriveState(kbm, pad, xr);

            // 来源家族离场 ⇒ 迁移到存活家族(AC-3-D3 Edge;不经迟滞,见类头)。
            if (ActiveSource != DeviceSourceKind.None && !FamilyPresent(ActiveSource))
            {
                ActiveSource = FirstPresentFamily();
                _hysteresis.ResetStreaks();
            }
            if (State == DeviceState.NoDevice)
            {
                ActiveSource = DeviceSourceKind.None;
                _hysteresis.ResetStreaks();
            }
        }

        /// <summary>五态推导(纯函数,测试可直接驱动)。优先序:XR &gt; Mixed &gt; 单家族 &gt; 无。</summary>
        public static DeviceState DeriveState(bool kbmPresent, bool padPresent, bool xrPresent)
        {
            if (xrPresent) return DeviceState.XrActive;
            if (kbmPresent && padPresent) return DeviceState.Mixed;
            if (kbmPresent) return DeviceState.KbmOnly;
            if (padPresent) return DeviceState.PadOnly;
            return DeviceState.NoDevice;
        }

        /// <summary>设备 → 来源家族映射:Keyboard / Mouse / Pointer → Kbm;Gamepad 族 → Pad;
        /// 布局名含 XRController → Xr;其余(未知布局)→ None(不计入在场)。</summary>
        public static DeviceSourceKind FamilyOf(InputDevice device)
        {
            if (device == null) return DeviceSourceKind.None;
            if (device is Keyboard || device is Mouse || device is Pointer) return DeviceSourceKind.Kbm;
            if (device is Gamepad) return DeviceSourceKind.Pad;
            string layout = device.layout;
            if (layout != null && layout.IndexOf("XRController", StringComparison.Ordinal) >= 0)
                return DeviceSourceKind.Xr;
            return DeviceSourceKind.None;
        }

        private int CountOfFamily(DeviceSourceKind family)
        {
            int n = 0;
            foreach (var d in _present)
                if (FamilyOf(d) == family) n++;
            return n;
        }

        private bool FamilyPresent(DeviceSourceKind family) => CountOfFamily(family) > 0;

        private DeviceSourceKind FirstPresentFamily()
        {
            if (CountOfFamily(DeviceSourceKind.Kbm) > 0) return DeviceSourceKind.Kbm;
            if (CountOfFamily(DeviceSourceKind.Pad) > 0) return DeviceSourceKind.Pad;
            if (CountOfFamily(DeviceSourceKind.Xr) > 0) return DeviceSourceKind.Xr;
            return DeviceSourceKind.None;
        }
    }
}
