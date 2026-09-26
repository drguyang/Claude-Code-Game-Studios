// Story 008 · 震动触觉通道接口(TR-input-015 · OQ-3-4;附注无 AC —— P0 只落接口)
//
// 权威来源:
//   GDD input-system.md §Visual/Audio 二:3 拥有「把震动指令送到设备」的**通道**,
//   「什么时候震 / 震多强 / 什么波形」的**语义归 8/10**(disease-simulation.md:1184
//   触诊主通道)。⚠️ OpenXRInput haptics 须单独 spike(OQ-3-4):本机 com.unity.xr.openxr
//   **未安装**(包清单 / PackageCache 双查空,Story 008 实测)⇒ spike 判「不可用」=
//   story 预期回退路径:**接口保留、实现挂账**,不改其他 AC 判定,不是 blocker。
//   P0 不做 VR 触觉(GDD §Visual/Audio 二 明写;VR 功能实现推 P1b,game-concept.md:723)。
//   数值与波形归用户与后续轮(story 附注)⇒ 本接口只有强度(int)与通道枚举,零波形参数。
//
// 形状纪律:接口住 Gameplay.Input 根命名空间(3 的基础设施 API,非 Intents 交出物 ——
//   不进 AC-3-A6 闭集面)。签名只用 BCL 类型 + 本命名空间枚举:引擎的 InputDevice 不经
//   参数暴露(通道实现自己知道注册了哪些设备),避免调用方(10)持引擎对象越界。
//   本故事**不做**任何具体实现(Gamepad rumble 接线 / OpenXR haptics 均挂账 P1b);
//   Verify 面 = 接口存在(类型可加载)+ 消费点约定:10 的调用点持 IHaptics 引用
//   —— 10 的实现故事落地时经构造注入(DI,承 coding-standards「可测性」条)。

namespace DaYiJingCheng.Gameplay.Input
{
    /// <summary>触觉通道枚举(3 拥有的**通道**身份;非语义 —— 语义归 8/10,§Visual/Audio 二)。
    /// P0 只有手柄震动一条真实通道;XR 触觉随 P1b 增员(闭集增员须回写 GDD,同旋钮纪律)。</summary>
    public enum HapticsChannel
    {
        /// <summary>手柄震动(Gamepad rumble 通道;实现接线挂账 —— 见文件头)。</summary>
        GamepadRumble = 0,

        /// <summary>XR 控制器触觉(OpenXR haptics;spike 判不可用 ⇒ P0 仅占位,不实现)。</summary>
        XrController = 1,
    }
}
