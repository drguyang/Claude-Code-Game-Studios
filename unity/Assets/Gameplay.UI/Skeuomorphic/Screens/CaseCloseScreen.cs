// 结案页(系统 37 的结案形态 · `ModalId.CaseClose` 第十员)—— T20 第二批(2026-11-10)
//
// 权威来源:
//   · `design/ux/casebook-39.md:370` 的 **`OQ-CB-7`**(「已处置」勾选 = 交互形态,归 39 定形态 / 42 落地)
//   · `design/gdd/case-system.md:687` 规则八 —— `可结案(c) := 开 ∧ 已处置(c) ∧ 玩家已勾选「已处置」`
//   · `design/ux/case-close-42.md`(本屏的形态规格,与代码同批落盘)
//   · ADR-013 §9 C3(42 只渲染,永不持有游戏状态)—— 本屏只发**意图**,判定全在 37 的写者
//
// ⚠️ **形态裁定(OQ-CB-7 取「独立结案页 + 纸面墨勾」)**:
//   39 §5.3 明令本屏禁「复选框」,而 GDD 的字面是「勾选」。裁定的落点 = **纸面墨勾** ——
//   它与「折边」「铅笔小勾」同属墨侧允许手法(不新增控件族、不进 UI-8.4 的禁入清单),
//   因此「勾选」这条 GDD 要求在本屏**可判**而**不违反** §5.3。
//
// ⚠️ 本屏**不显示病种**(`disease_id` 禁入呈现层,AC-37-15 / `PresentationDtoGuard`),
//   也不显示任何数量类信息(禁词表 + 「37 沉默」纪律)。
//
// ⚠️ 拒绝时给**一行淡墨字**(2026-11-10 用户裁定):玩家要知道「按了为什么没生效」。
//   文案由装配层给(本屏不判因),屏只负责「显示 / 清除」。

namespace DaYiJingCheng.Gameplay.UI.Skeuomorphic.Screens
{
    using DaYiJingCheng.Gameplay.Presentation.Skeuomorphic;
    using UnityEngine.UIElements;

    /// <summary>结案页的意图出口(实现方住装配层;42 不反向引用 Boot)。</summary>
    public interface ICaseCloseCommandSink
    {
        /// <summary>墨勾切换(纯屏上态;不发任何流事件)。</summary>
        void ToggleInk();

        /// <summary>
        /// 提交结案。
        /// <para>返回 <c>false</c> = 被拒(屏须显示淡墨行);<c>true</c> = 受理(实现方负责合屏)。</para>
        /// <para>⚠️ 判定不在这里:四条前置(已结 / 未开 / 未勾 / 窗口内无处置)全在 37 的写者里。</para>
        /// </summary>
        bool SubmitClose();

        /// <summary>合上本页,不结案。</summary>
        void Cancel();
    }

    /// <summary>结案页(<c>ModalId.CaseClose</c>)—— 平面拟物 UI,手柄走查。</summary>
    public sealed class CaseCloseScreen : PresentationRoot, IModalState
    {
        private readonly VisualElement _root;
        private readonly SkeuoElementLibrary _library;
        private readonly Label _inkMark;
        private readonly Label _reject;
        private ICaseCloseCommandSink _sink;

        /// <summary>墨勾是否已落(纯屏上态,重开屏即复位)。</summary>
        public bool IsInkChecked { get; private set; }

        /// <summary>当前淡墨行文字(空 = 不显示)。</summary>
        public string RejectText => _reject.text;

        /// <summary>创建结案页。</summary>
        public CaseCloseScreen(VisualElement root, SkeuoElementLibrary library) : base(root)
        {
            _root = root;
            _library = library;
            _inkMark = new Label(string.Empty) { name = "case-close-ink-mark" };
            _reject = new Label(string.Empty) { name = "case-close-reject" };
            BuildUI();
            BindRows();
        }

        /// <inheritdoc/>
        public ModalId Modal => ModalId.CaseClose;

        /// <summary>绑定意图出口(装配层在开屏前调)。</summary>
        public void Bind(ICaseCloseCommandSink sink) => _sink = sink;

        /// <summary>开屏时复位:墨勾清空、淡墨行清空(每次结案都是新的一页)。</summary>
        public void ResetForOpen()
        {
            IsInkChecked = false;
            SetInkMark();
            ClearReject();
            _root.Q<VisualElement>("case-close-ink-row")?.Focus();
        }

        /// <summary>
        /// 墨勾行被激活(Submit)。
        /// <para>⚠️ **公开**是因为 UI 事件在 EditMode 里发不起来(无 PanelSettings ⇒ `panel == null`
        /// ⇒ `SendEvent` 不派发)。判据因此直调本方法;**事件绑定本身**只有真包走查能验 ——
        /// 这条限制写在 `case_close_screen_test` 的文件头,不当成已覆盖。</para>
        /// </summary>
        public void ActivateInkRow()
        {
            IsInkChecked = !IsInkChecked;
            SetInkMark();
            ClearReject();
            _sink?.ToggleInk();
        }

        /// <summary>「结案」行被激活(Submit)。语义同上一条的说明。</summary>
        public void ActivateSubmitRow()
        {
            ClearReject();
            _sink?.SubmitClose();
        }

        /// <summary>显示一行淡墨拒绝说明。</summary>
        public void ShowReject(string reason)
        {
            _reject.text = reason ?? string.Empty;
            _reject.style.display = string.IsNullOrEmpty(reason) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        /// <summary>清除淡墨行。</summary>
        public void ClearReject() => ShowReject(string.Empty);

        private void SetInkMark() => _inkMark.text = IsInkChecked ? "✓" : string.Empty;

        private void BuildUI()
        {
            var host = new VisualElement { name = "case-close-host" };
            host.AddToClassList("pause-column");      // 与其余模态同一列宽契约

            var page = _library.Create(SkeuoElement.Paper);
            page.name = "case-close-page";
            page.AddToClassList("paper-page");        // 页框只给页级容器(行不挂)

            page.Add(new Label("结案") { name = "case-close-title" });

            var inkRow = new VisualElement { name = "case-close-ink-row" };
            inkRow.AddToClassList("ruled");
            inkRow.Add(new Label("已处置") { name = "case-close-ink-label" });
            inkRow.Add(_inkMark);
            page.Add(inkRow);

            var submitRow = new VisualElement { name = "case-close-submit-row" };
            submitRow.AddToClassList("ruled");
            submitRow.Add(new Label("结案") { name = "case-close-submit-label" });
            page.Add(submitRow);

            _reject.AddToClassList("case-close-reject-line");
            _reject.style.display = DisplayStyle.None;
            page.Add(_reject);

            host.Add(page);
            _root.Add(host);
        }

        private void BindRows()
        {
            VisualElement inkRow = _root.Q<VisualElement>("case-close-ink-row");
            VisualElement submitRow = _root.Q<VisualElement>("case-close-submit-row");
            if (inkRow != null)
            {
                inkRow.focusable = true;
                inkRow.RegisterCallback<NavigationSubmitEvent>(_ => ActivateInkRow());
            }
            if (submitRow != null)
            {
                submitRow.focusable = true;
                submitRow.RegisterCallback<NavigationSubmitEvent>(_ => ActivateSubmitRow());
            }
        }
    }
}
