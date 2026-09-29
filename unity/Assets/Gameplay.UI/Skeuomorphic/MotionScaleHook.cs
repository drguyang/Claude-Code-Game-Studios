// 权威来源:design/gdd/skeuomorphic-ui.md 规则十 钩子④(动效缩放挂点)
//   · production/epics/skeuomorphic-ui/story-009-focus-visual-and-accessibility.md
//
// 设计说明:
//   · 翻页 / 淡出 / 墨迹渗开 / 焦点移动等时长型观感走本缩放系数,无写死时长(AC-42-G4)。
//   · 置 0 = 无动效且界面仍完全可用(AC-42-G4 后半)。

namespace DaYiJingCheng.Gameplay.UI.Skeuomorphic
{
    /// <summary>动效缩放钩子(规则十钩子④)。</summary>
    public sealed class MotionScaleHook
    {
        /// <summary>缩放系数(0 = 无动效;数值归 49)。</summary>
        public float Scale { get; set; } = 1f;

        /// <summary>按基础时长求实际动效时长(秒);Scale=0 ⇒ 0(瞬时,界面仍可用)。</summary>
        public float EvaluateDuration(float baseSeconds)
        {
            if (baseSeconds <= 0) return 0f;
            return baseSeconds * (Scale < 0 ? 0 : Scale);
        }
    }
}
