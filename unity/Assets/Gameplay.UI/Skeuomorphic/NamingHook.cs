// 权威来源:design/gdd/skeuomorphic-ui.md 规则十 钩子①(焦点元素的命名挂点)
//   · production/epics/skeuomorphic-ui/story-009-focus-visual-and-accessibility.md
//
// 设计说明:
//   · 每个可聚焦元素有一个可挂无障碍名的位置。
//   · 生命周期:装载期建位;运行期重绑不重建挂点;元素销毁时挂点失效。

namespace DaYiJingCheng.Gameplay.UI.Skeuomorphic
{
    /// <summary>焦点元素命名挂点(规则十钩子①)。</summary>
    public sealed class NamingHook
    {
        private string _accessibleName = string.Empty;

        /// <summary>无障碍名(运行期重绑只改名,不重建挂点)。</summary>
        public string AccessibleName
        {
            get => _accessibleName;
            set => _accessibleName = value ?? string.Empty;
        }

        /// <summary>挂点是否有效(元素销毁 = 失效)。</summary>
        public bool IsValid { get; private set; } = true;

        /// <summary>元素销毁时调用(挂点随之失效)。</summary>
        public void Invalidate() => IsValid = false;
    }
}
