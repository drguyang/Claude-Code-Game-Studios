// Story 008 · 震动触觉通道接口(TR-input-015 附注 · OQ-3-4;P0 只落接口,无实现)
//
// 权威来源:GDD input-system.md §Visual/Audio 二(3 拥有通道,8/10 拥有语义)·
// story-008 附注(无 AC):「IHaptics 接口存在」是 Verify 项;OpenXRInput spike 判
// 不可用(com.unity.xr.openxr 未安装)⇒ 接口保留、实现挂账 P1b(预期回退,非 blocker)。
// 数值与波形归用户与后续轮 ⇒ 参数只有通道枚举 + 强度(int,Q16.16),零波形 / 零时长承诺。

namespace DaYiJingCheng.Gameplay.Input
{
    /// <summary>震动触觉**通道**接口(3 拥有通道,不拥有语义 —— §Visual/Audio 二)。
    /// P0 交付面 = 接口本身(Verify:类型存在);实现(Gamepad rumble 接线 / OpenXR haptics)
    /// 挂账:OpenXR spike 不可用 ⇒ VR 触觉 P0 不做;手柄 rumble 接线归后续故事轮。
    /// 调用方 = 8/10(语义决策者),经构造注入获取实例(DI,承 coding-standards)。</summary>
    public interface IHaptics
    {
        /// <summary>在指定通道发一次震动指令(火-and-forget,无回执 —— 回执语义不存在于 P0)。
        /// 实现缺席时(挂账期)调用方应持 no-op 实现(由装配层提供,接口自身不定义默认实现)。</summary>
        /// <param name="channel">通道身份(3 拥有;GamepadRumble / XrController)。</param>
        /// <param name="intensityQ16">强度,Q16.16 定点整数(0 = 静默,65536 = 满幅)。
        /// 「震多强」的**取值语义归 8/10**,本参数只承诺量纲与闭区间值域。</param>
        void Pulse(HapticsChannel channel, int intensityQ16);
    }
}
