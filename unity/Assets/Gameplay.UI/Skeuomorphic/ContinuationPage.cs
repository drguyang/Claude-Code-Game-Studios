// 权威来源:design/gdd/skeuomorphic-ui.md V-11(形态画不出 → 唯一降级 = 续页)
//   · AC-42-F6: 运行期降级路径 ⊆ 封闭白名单 {续页};静默裁切/缩字号/破纸/省略内容全禁。
//   · production/epics/skeuomorphic-ui/story-008-no-healthbar-feedback.md
//
// 设计说明:
//   · 42 只提供「再多一页」这个能力;切在哪、哪页放什么归内容拥有者(8/37/39)。

namespace DaYiJingCheng.Gameplay.UI.Skeuomorphic
{
    /// <summary>
    /// 续页降级路径(V-11 / AC-42-F6)—— 运行期唯一合法降级。
    /// </summary>
    public sealed class ContinuationPage
    {
        /// <summary>是否需要续页(内容超限时置 true;由布局测量得出)。</summary>
        public bool NeedsContinuation { get; set; }

        /// <summary>续页次数(观测值;切分策略归内容拥有者)。</summary>
        public int PageCount { get; private set; } = 1;

        /// <summary>
        /// 请求续页 —— V-11 的唯一降级动作。
        /// <para>禁:静默裁切 / 缩字号 / 破纸 / 拉伸 / 省略内容(AC-42-F6 白名单仅 {续页})。</para>
        /// </summary>
        public void RequestContinuation()
        {
            NeedsContinuation = true;
            PageCount++;
        }

        /// <summary>内容就位后复位(新页承载完毕)。</summary>
        public void ResetContinuation()
        {
            NeedsContinuation = false;
        }
    }
}
