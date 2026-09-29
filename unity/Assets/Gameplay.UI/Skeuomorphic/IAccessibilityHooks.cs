// 权威来源:ADR-013 §六 无障碍四钩子(接口存在,不在此实现)
//   · design/gdd/skeuomorphic-ui.md 规则十
//   · design/accessibility-requirements.md
//
// 设计说明:
//   · P0 只留接口,具体要求归 49 无障碍(P2)。
//   · 四钩子:焦点元素命名挂点 / 文本缩放主题变量层 / 焦点可见样式契约 / 动效缩放挂点。
//   · 焦点可见样式 = USS class(墨色加深/纸面压痕),非纯色高亮 — 靠形状不靠颜色(色盲可读)。

using UnityEngine.UIElements;

namespace DaYiJingCheng.Gameplay.UI.Skeuomorphic
{
    /// <summary>
    /// 无障碍四钩子接口(规则十)— P0 只留接口,具体要求归 49。
    /// <para>四钩子:① 焦点元素命名挂点 ② 文本缩放主题变量层 ③ 焦点可见样式契约 ④ 动效缩放挂点。</para>
    /// </summary>
    public interface IAccessibilityHooks
    {
        /// <summary>钩子①:为焦点元素设置无障碍名(屏幕阅读器读取)。</summary>
        void SetAccessibleName(VisualElement e, string name);

        /// <summary>钩子②:应用文本缩放(主题变量层,不缓存最终字号)。</summary>
        void ApplyTextScale(float scale);

        /// <summary>钩子④:应用动效缩放(置 0 = 无动效,界面仍完全可用)。</summary>
        void ApplyMotionScale(float scale);
    }
}
