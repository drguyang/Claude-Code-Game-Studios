// 权威来源:design/gdd/skeuomorphic-ui.md §7.9 戥子档位读数元件(黄铜侧)
//   · ADR-013 §十-B(戥子档位读数元件 = 黄铜侧)
//   · AC-42-F3 材质分支断言须把本元件归入黄铜分支。
//   · production/epics/skeuomorphic-ui/story-005-world-billboard.md
//
// 设计说明:
//   · 黄铜侧读数件 — 錾刻刻度合法(墨侧禁刻度条,黄铜侧允许)。
//   · 哑光金属,禁发光/泛光;读数档位可辨性归 11 的 AC-11-13。

namespace DaYiJingCheng.Gameplay.UI.Skeuomorphic.Brass
{
    using UnityEngine.UIElements;

    /// <summary>戥子档位读数元件(黄铜侧 · AC-42-F3 黄铜分支)。</summary>
    public sealed class BrassScaleElement : VisualElement
    {
        /// <summary>创建黄铜刻度元件。</summary>
        public BrassScaleElement()
        {
            AddToClassList("brass");
            AddToClassList("brass-scale");
        }

        /// <summary>档位刻度数(整形量;档位语义归 11)。</summary>
        public int TickCount { get; set; }
    }
}
