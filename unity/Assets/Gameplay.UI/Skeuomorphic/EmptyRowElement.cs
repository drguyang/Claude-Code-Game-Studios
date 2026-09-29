// 权威来源:design/gdd/skeuomorphic-ui.md AC-42-B5(空行有格线无字)
//   · production/epics/skeuomorphic-ui/story-011-casebook-rendering.md
//
// 设计说明:
//   · 空行 = 可聚焦但零文本(无灰色字 / 无「未查」标签 / 无占位文字)。
//   · 焦点态仍施加焦点类(AC-42-B5 的正半边)。

namespace DaYiJingCheng.Gameplay.UI.Skeuomorphic
{
    using UnityEngine.UIElements;

    /// <summary>
    /// 空行元素(AC-42-B5)—— 格线渲染存在但无文本节点。
    /// <para>空位可聚焦但零文本;不得因「无内容」被布局塌缩(L4 空位不夺焦)。</para>
    /// </summary>
    public sealed class EmptyRowElement : VisualElement
    {
        /// <summary>创建空行(仅格线,零 TextElement)。</summary>
        public EmptyRowElement()
        {
            AddToClassList("empty-row");
            // 故意不添加任何文本子元素 —— 空行即答案,不写占位文字
        }
    }
}
