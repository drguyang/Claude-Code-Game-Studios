// 权威来源:design/gdd/skeuomorphic-ui.md §7.9 戥子档位读数元件(黄铜侧)
//   · ADR-013 §十-B(戥子档位读数元件 = 黄铜侧)
//   · AC-42-F3 材质分支断言须把本元件归入黄铜分支。
//   · production/epics/skeuomorphic-ui/story-005-world-billboard.md
//   · production/epics/skeuomorphic-ui/story-025-dentch-dose-form-element.md(AC-025-1…6)
//
// 设计说明:
//   · 黄铜侧读数件 — 錾刻刻度合法(墨侧禁刻度条,黄铜侧允许)。
//   · 哑光金属,禁发光/泛光;读数档位可辨性归 11 的 AC-11-13。
//   · AC-025-1:本元件是**逐档形态的载体** —— 档位读数由「N 个形态子件 + 其中一档
//     几何放大」表达,不经任何数字文本(AC-025-5 零数字铁律)。形态差异落在 USS
//     (`.brass-detent` / `.brass-detent--focused`),本类只决定「有几个 / 哪个落焦」。
//   · AC-025-2:**元件不持剂量状态** —— 无 `CurrentDose` / `SelectedOrdinal` 一类
//     持久字段;形态完全由 `SetDetents` 的入参决定,同一入参重复调用 ⇒ 同一形态。

namespace DaYiJingCheng.Gameplay.UI.Skeuomorphic.Brass
{
    using System;
    using UnityEngine.UIElements;

    /// <summary>戥子档位读数元件(黄铜侧 · AC-42-F3 黄铜分支)。</summary>
    public sealed class BrassScaleElement : VisualElement
    {
        /// <summary>常态档形态类名(AC-025-3)。</summary>
        public const string DetentClass = "brass-detent";

        /// <summary>落焦档形态类名 —— 几何放大,颜色不变(AC-025-1 ②「非颜色」)。</summary>
        public const string FocusedDetentClass = "brass-detent--focused";

        /// <summary>档形态带的类名。</summary>
        public const string DetentBandClass = "brass-detent-band";

        /// <summary>档形态带(逐档形态子件的宿主)。</summary>
        private readonly VisualElement _detentBand;

        /// <summary>创建黄铜刻度元件。</summary>
        public BrassScaleElement()
        {
            AddToClassList("brass");
            AddToClassList("brass-scale");

            _detentBand = new VisualElement();
            _detentBand.AddToClassList(DetentBandClass);
            // ⚠️ 2026-10-18 评审 R-9:`Add` 而非 `hierarchy.Add` —— 二者对 VisualElement
            //    今日等价(`contentContainer == this`),但 `Add` 是全库惯用写法,
            //    且基类若改为容器型时不会绕开 `contentContainer`。
            Add(_detentBand);
        }

        // ⚠️ **2026-10-30 删除 `TickCount`(TD-prescription-08)** —— 该自动属性此前
        //    **生产侧零读**:唯一写入点 `CasebookScreen.cs:529` 只写不读,而本元件内部
        //    一切档位判据走的都是 `DetentCount`(= `_detentBand.childCount`,由**已生成的形态**
        //    反查),**从不回看那个入参**。
        //    ⇒ 它是纯死状态,且与 AC-025-2「元件不得持可变的剂量状态」**同向**
        //    (保留一个只写不读的 `int` 只会诱使后来者以为它承载语义)。
        //    ⚠️ **易混读**:`PrescribeRequestDriver.DoseDetentCount` 读的是
        //    **`DentchDoseSelector.TickCount`**(同名的**另一个**属性,不是本元件的)——
        //    两处同名不同物,删本处**不影响**那一条。

        /// <summary>当前已生成的档形态数(0 = 未装载)。</summary>
        public int DetentCount => _detentBand.childCount;

        /// <summary>落焦档的档序(1 起;0 = 无落焦档)。
        /// <para>**由已生成的形态反查**,不是缓存的入参 —— 缓存入参会把「上一次的落焦档」
        /// 留成第二真源(AC-025-2 禁)。</para></summary>
        public int FocusedDetentOrdinal
        {
            get
            {
                for (int i = 0; i < _detentBand.childCount; i++)
                {
                    if (_detentBand[i].ClassListContains(FocusedDetentClass)) return i + 1;
                }

                return 0;
            }
        }

        /// <summary>按档数生成逐档形态,并把第 <paramref name="focusedOrdinal"/> 档标为落焦形态。</summary>
        /// <param name="count">档数。**须 ≥ 1**。</param>
        /// <param name="focusedOrdinal">落焦档序(1 起),须落在 <c>[1, count]</c> 内。</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="count"/> ≤ 0,或 <paramref name="focusedOrdinal"/> 越界。
        /// ⚠️ **越界一律抛,禁钳位** —— 钳位会把越界入参静默画成一个看似合法的形态,
        /// 使上游的档序错位在呈现层无从察觉。</exception>
        public void SetDetents(int count, int focusedOrdinal)
        {
            if (count <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(count), count, "[BrassScaleElement] 档数须 ≥ 1。");
            }

            if (focusedOrdinal < 1 || focusedOrdinal > count)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(focusedOrdinal), focusedOrdinal,
                    $"[BrassScaleElement] 落焦档序须落在 [1, {count}] 内(不钳位)。");
            }

            _detentBand.Clear();
            for (int ordinal = 1; ordinal <= count; ordinal++)
            {
                var detent = new VisualElement();
                detent.AddToClassList(DetentClass);
                if (ordinal == focusedOrdinal) detent.AddToClassList(FocusedDetentClass);
                _detentBand.Add(detent);
            }
        }
    }
}
