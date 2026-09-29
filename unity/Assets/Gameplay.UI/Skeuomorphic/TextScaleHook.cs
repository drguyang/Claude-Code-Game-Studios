// 权威来源:design/gdd/skeuomorphic-ui.md 规则十 钩子②(文本缩放主题变量层)
//   · production/epics/skeuomorphic-ui/story-009-focus-visual-and-accessibility.md
//
// 设计说明:
//   · 字号走主题变量;textScale 变化 ⇒ 重新求值,不缓存最终字号。
//   · 归 49 无障碍定值,42 只消费。

namespace DaYiJingCheng.Gameplay.UI.Skeuomorphic
{
    /// <summary>文本缩放钩子(规则十钩子②)—— 重新求值,不缓存最终字号。</summary>
    public sealed class TextScaleHook
    {
        private float _scale = 1f;

        /// <summary>当前缩放系数(&gt;0;数值归 49)。</summary>
        public float Scale
        {
            get => _scale;
            set => _scale = value > 0 ? value : 1f;
        }

        /// <summary>按主题字号重新求值最终字号(每次调用都算,不读缓存)。</summary>
        public int EvaluateFontSize(int themeFontSize)
        {
            return (int)System.Math.Round(themeFontSize * _scale);
        }
    }
}
