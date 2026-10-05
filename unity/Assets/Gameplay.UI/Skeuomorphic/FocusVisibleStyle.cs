// 权威来源:design/gdd/skeuomorphic-ui.md 规则十 钩子③(焦点可见样式契约)
//   · production/epics/skeuomorphic-ui/story-009-focus-visual-and-accessibility.md
//
// 设计说明:
//   · 拟物焦点高亮不得是纯色 — 靠形状不靠颜色(8 的 UI-8.2)。
//     ⚠️ 2026-10-05 载体改判:墨色加深 → **黄铜 2px**(用户裁定;承 art-bible §7.4 Amendment、
//     GDD 规则十同批注记)。「不得是纯色」纪律不变 —— 黄铜边框不是字段填充色。
//   · 每次焦点转移施加/移除类;门关时不施加(无焦点 ⇒ 无高亮,不得残留)。

namespace DaYiJingCheng.Gameplay.UI.Skeuomorphic
{
    /// <summary>焦点可见样式契约(规则十钩子③)。</summary>
    public sealed class FocusVisibleStyle
    {
        /// <summary>焦点可见 USS 类名(载体 = 黄铜 2px,2026-10-05 改判;禁纯色填充)。</summary>
        public const string ClassName = "focus-visible";

        /// <summary>焦点态 USS 类名(门关时不施加)。</summary>
        public const string DisabledClassName = "focus-visible-disabled";
    }
}
