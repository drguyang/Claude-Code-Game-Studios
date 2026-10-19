// 10 emergency-procedures 规则十二 + AC-10-21 · 3 侧的「模拟量通道有无」作证面(2026-10-19)
//
// 权威来源:
//   10 GDD emergency-procedures.md 规则十二(:467)「设备无模拟量通道(纯键鼠,OQ-10-3 的回退路径)
//     ⇒ cause = 降级」+ Edge Cases(:728)「magnitude 通道无输入设备(纯键鼠)⇒ 回退形状,cause = 降级」;
//     AC-10-21(BLOCKING):纯键鼠 ⇒ 幅度门恒过、节奏 + 稳度双门照评、cause = 降级。
//   10 GDD 规则一(R-5):「手不能抖」的幅度侧**实为手柄独占**,VR 推 P1b
//     ⇒ P0 上「有无模拟量」的判据 = **手柄 / XR 家族是否在场**。
//   ADR-011 §F(Emergency 动作引用在构造期解析一次,零每帧字符串查找)。
//
// ⚠️ 为什么住 3 而不住 Boot:本读数描述的是通道侧的设备事实(游戏手柄 / XR 是否给得出
//   模拟量),那是 3 的拥有物(「3 只出整数读数与意图」)。Boot 侧只经单方法接口
//   `IEmergencyDeviceReadiness` 转发一个 bool,不在此重写判据。
//
// ⚠️ 判据(最小、可测、不猜):
//   · 有 Pad(游戏手柄布局)或 Xr(XRController 布局)设备在场 ⇒ 有模拟量通道;
//   · 只有键鼠,或设备态未知(未 Attach / NoDevice)⇒ **无**模拟量通道(键鼠回退形状);
//   · 判据只读设备态,**不读 InputSystem.devices 第三份副本**(设备态是 3 的单一真源)。
//   · 纯键鼠时 Boot 侧【**未**】执行「幅度通道缺失 ⇒ 幅度按 MAG_MAX 回填」——
//     那是 `FeelLatencyEvaluator.CreateKeyboardFallbackReading()` 的键鼠回退形状,
//     其生产接线须先把判据落到通道读数层并补可证伪夹具;**本轮只落 cause 半边,如实登记**。

namespace DaYiJingCheng.Gameplay.Input
{
    /// <summary>「本次动作是否走在无模拟量通道的路径上」的单方法读数(10 GDD 规则十二判据的输入面)。
    /// <para>true ⇒ `cause = 降级`;false ⇒ `cause = 玩家选择`。消费方 = 10 的写侧
    /// (`EmergencyManualDriver`);本接口住 3 —— 判据归通道拥有者,免得两处各持一份设备判断而漂移。</para>
    /// <para>设备态缺失(`NoDevice` / 未 <c>Attach</c>)时实现回答 **true(降级)** ——
    /// 降级如实可见,不静默记成「玩家选择」把降级读数抹掉(见 <see cref="EmergencyDeviceReadiness"/> 类注)。</para></summary>
    public interface IEmergencyDeviceReadiness
    {
        /// <summary>当前动作是否走在无模拟量通道的路径上(⇒ `cause = 降级`,AC-10-21)。</summary>
        bool IsAnalogChannelAbsent { get; }
    }

    /// <summary>
    /// 3 侧「模拟量通道有无」的作证面 —— 实现 <see cref="IEmergencyDeviceReadiness"/>。
    /// </summary>
    /// <remarks>
    /// <para><b>读数来源</b>:注入的 <see cref="DeviceStateManager"/>(story-008;设备态五值的单一真源)。
    /// 未注入 / 未 <c>Attach</c>(设备态恒 <see cref="DeviceState.NoDevice"/>)⇒ 判「无模拟量」——
    /// 与纯键鼠同路(降级如实可见,而不是记成「玩家选择」把降级读数抹掉)。</para>
    /// <para><b>为什么不是「键鼠在场即降级」</b>:Mixed 下玩家用**手柄**做动作时幅度通道是通的,
    /// 记降级会把「有通道却没用」误报成「无通道」。判据按**能力**而非**最近输入来源**。</para>
    /// </remarks>
    public sealed class EmergencyDeviceReadiness : IEmergencyDeviceReadiness
    {
        private readonly DeviceStateManager _devices;

        /// <param name="devices">3 的设备态(可空;null ⇒ 恒「无模拟量」,见类注)。</param>
        public EmergencyDeviceReadiness(DeviceStateManager devices)
        {
            _devices = devices;
        }

        /// <summary>true ⇒ 本次动作无模拟量通道(键鼠回退形状,`cause = 降级`)。</summary>
        public bool IsAnalogChannelAbsent
        {
            get
            {
                if (_devices == null) return true;
                switch (_devices.State)
                {
                    case DeviceState.PadOnly:
                    case DeviceState.Mixed:     // 手柄在场 ⇒ 幅度通道可用(不论最近输入来自谁)
                    case DeviceState.XrActive:  // XR 抢占且带扳机模拟量
                        return false;
                    default:                    // KbmOnly / NoDevice(含未 Attach)⇒ 键鼠回退
                        return true;
                }
            }
        }
    }
}
