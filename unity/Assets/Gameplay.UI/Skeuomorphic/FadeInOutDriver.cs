// 权威来源:design/gdd/skeuomorphic-ui.md V-10(读数条出现/淡出)
//   · production/epics/skeuomorphic-ui/story-017-enemy-vitals-bar.md
//
// 设计说明:
//   · 淡入淡出时长走动效缩放钩子(规则十第四钩子),不写死时长(AC-42-G4)。
//   · 触发时机归 27(EncounterStarted/Ended);42 只执行渐变。

namespace DaYiJingCheng.Gameplay.UI.Skeuomorphic
{
    /// <summary>读数条淡入淡出驱动(V-10)—— 时长经动效缩放钩子,无硬编码。</summary>
    public sealed class FadeInOutDriver
    {
        /// <summary>动效缩放系数(第四钩子;0 = 无动效瞬切,界面仍完全可用)。</summary>
        public float MotionScale { get; set; } = 1f;

        /// <summary>当前透明度(0..1)。</summary>
        public float Opacity { get; private set; }

        /// <summary>
        /// 推进一步淡入。
        /// </summary>
        /// <param name="normalizedStep">归一化步长(由渲染帧时间 × 动效缩放得出)。</param>
        public void StepFadeIn(float normalizedStep)
        {
            float delta = normalizedStep * MotionScale;
            Opacity = System.Math.Clamp(Opacity + delta, 0f, 1f);
        }

        /// <summary>推进一步淡出。</summary>
        public void StepFadeOut(float normalizedStep)
        {
            float delta = normalizedStep * MotionScale;
            Opacity = System.Math.Clamp(Opacity - delta, 0f, 1f);
        }

        /// <summary>瞬切(动效缩放 = 0 的降级路径,界面仍可用)。</summary>
        public void Snap(bool visible) => Opacity = visible ? 1f : 0f;
    }
}
