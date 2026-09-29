// 权威来源:design/gdd/skeuomorphic-ui.md AC-42-B6(置信度不出溢体征栏)
//   · production/epics/skeuomorphic-ui/story-011-casebook-rendering.md
//
// 设计说明:
//   · 置信度符号只在置信度栏子树内出现,零溢出到体征栏。
//   · 承载上限由布局参数配置;42 不持置信度数值(游戏量归 8,AC-42-D4)。

namespace DaYiJingCheng.Gameplay.UI.Skeuomorphic
{
    using UnityEngine.UIElements;

    /// <summary>置信度栏(AC-42-B6)—— 置信度渲染的唯一子树边界。</summary>
    public sealed class ConfidenceColumn : VisualElement
    {
        /// <summary>创建置信度栏。</summary>
        public ConfidenceColumn()
        {
            AddToClassList("confidence-column");
        }

        /// <summary>栏承载上限(布局参数;溢出须续页而非裁切,承 V-11)。</summary>
        public int CapacityLimit { get; set; } = 6;
    }
}
