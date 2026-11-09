// 权威来源:production/epics/skeuomorphic-ui/story-016-paper-closeup.md
//   · design/ux/paper-closeup-48.md(本屏的 per-screen spec,Status = Approved)
//   · ADR-013 §四 Key Interfaces · §十-B(闭集第七员 `ModalId.PaperCloseup48`)
//   · `design/gdd/tutorial-and-onboarding.md` 规则二(纸 = 世界内静态道具,走近摊开)
//
// 设计说明:
//   · 教学纸近景 = 世界内单张纸的**可读近景**(走近 → 摊开;不是 HUD,不是弹窗)
//   · 继承 PresentationRoot,实现 IModalState(闭集第七员)
//   · 使用 SkeuoElementLibrary 创建元件
//
// 2026-11-06(sprint-25 S25-4)—— 本屏此前是**空壳**(只画一张纸,不接内容)。本轮补上内容绑定:
//   `Bind(papers)` 喂 48 内容表的纸条目,渲染「图题 + 一句话说明 + 页码」;
//   `TurnPage(±1)` 翻页(≤2 行标题 / ≤170 字正文,承 paper-closeup-48.md §12 的长度约束)。
//
// ⚠️ **本屏不持有游戏状态**:纸条目是**开屏时喂入的呈现缓存**,屏只渲染(承 ADR-013 §9 C3)。
// ⚠️ **插图槽未接素材**:`illustrationKey` 由 48 内容表给出、素材归 art(S25-4 时点尚空),
//    故本屏只**暴露**该键(`CurrentIllustrationKey`)供 42 的元件库后续接图,不自造图形。
// 2026-11-09(T6)—— 缺页态:被判为缺页的那一张**只剩标题**,正文不摆出来,纸面压一道撕口。
//   钩子病例的载体(map §5.2):「看懂了却治不了」比「看不懂也治不了」有价值。
//   `SetMissingPaperIds` 按**纸条目 id** 收(不按页序 —— 页序随内容表增删漂移)。
//   ⚠️ 谁决定「这一页缺」不归本屏(屏只渲染,承 ADR-013 §9 C3)。
//
// ⚠️ **合上 / 焦点还原不归本屏**:模态开合与焦点单栈门住 `SkeuoRuntimeDriver`(42 的既有机制)。

namespace DaYiJingCheng.Gameplay.UI.Skeuomorphic.Screens
{
    using System;
    using System.Collections.Generic;
    using DaYiJingCheng.Gameplay.Presentation.Skeuomorphic;
    using DaYiJingCheng.Gameplay.Presentation.Tutorial;
    using DaYiJingCheng.Sim.Contracts;
    using UnityEngine.UIElements;

    /// <summary>
    /// 教学纸近景(48)——世界内单张纸近景。
    /// <para>ModalId.PaperCloseup48,走近摊纸触发。</para>
    /// </summary>
    public sealed class PaperCloseupScreen : PresentationRoot, IModalState
    {
        /// <summary>图题元素名(观测面)。</summary>
        public const string TitleElementName = "paper-title";

        /// <summary>正文元素名(观测面)。</summary>
        public const string BodyElementName = "paper-body";

        /// <summary>页码元素名(观测面;单页时不显示)。</summary>
        public const string PageHintElementName = "paper-page-hint";

        /// <summary>纸面元素名(观测面;T6 起用于判缺页类是否挂上)。</summary>
        public const string PaperElementName = "paper";

        /// <summary>缺页撕口元素名(观测面;T6)。</summary>
        public const string MissingMarkElementName = "paper-missing-mark";

        /// <summary>缺页态的类名(挂在纸面元素上)。</summary>
        public const string MissingClass = "paper-missing";

        private readonly VisualElement _root;
        private readonly SkeuoElementLibrary _library;

        private VisualElement _paper;
        private VisualElement _missingMark;
        private Label _title;
        private Label _body;
        private Label _pageHint;
        private IReadOnlyList<TutorialPaperRow> _papers;
        private HashSet<string> _missingPaperIds;
        private int _index = -1;

        /// <summary>创建教学纸近景。</summary>
        public PaperCloseupScreen(VisualElement root, SkeuoElementLibrary library) : base(root)
        {
            _root = root;
            _library = library;
            BuildUI();
        }

        /// <inheritdoc/>
        public ModalId Modal => ModalId.PaperCloseup48;

        /// <summary>纸条目数(未绑定 ⇒ 0)。</summary>
        public int PageCount => _papers == null ? 0 : _papers.Count;

        /// <summary>当前页序(未绑定 ⇒ −1)。</summary>
        public int CurrentPage => _index;

        /// <summary>当前页的图题(未绑定 ⇒ 空串)。</summary>
        public string CurrentTitle => _title == null ? string.Empty : _title.text;

        /// <summary>当前页的说明正文(未绑定 ⇒ 空串)。</summary>
        public string CurrentBody => _body == null ? string.Empty : _body.text;

        /// <summary>当前页的插图素材键(未绑定 ⇒ <c>null</c>;接图归 42 的元件库)。</summary>
        public string CurrentIllustrationKey
            => _index >= 0 && _papers != null && _index < _papers.Count
                ? _papers[_index].IllustrationKey
                : null;

        /// <summary>
        /// 声明哪些纸**只剩标题**(缺页 · T6)。
        /// </summary>
        /// <param name="paperIds">缺页的纸条目 id(可空集 = 无缺页;传 null 等价于空集)。
        /// <para>⚠️ 按**id** 而不是页序:页序会随内容表增删漂移,id 是 6 侧布置引用的稳定键。</para></param>
        /// <remarks>
        /// 缺页态 = 标题照常显示、正文不显示、纸面压一道撕口。
        /// **为什么不画灰按钮**:承 `AC-8-44` / `campaign-arc.md:153` ——
        /// 锁着的东西必须是「不存在」,不是「点不动」。
        /// <para>⚠️ 谁决定「这一页缺」不归本屏 —— 屏只渲染(承 ADR-013 §9 C3)。</para>
        /// </remarks>
        public void SetMissingPaperIds(IEnumerable<string> paperIds)
        {
            if (paperIds == null)
            {
                _missingPaperIds = null;
            }
            else
            {
                _missingPaperIds = new HashSet<string>(paperIds);
            }
            Render();
        }

        /// <summary>当前页是否处于缺页态(未绑定 / 无此声明 ⇒ <c>false</c>)。</summary>
        public bool IsCurrentPageMissing
            => _missingPaperIds != null
               && _index >= 0 && _papers != null && _index < _papers.Count
               && _papers[_index].Id != null
               && _missingPaperIds.Contains(_papers[_index].Id);

        /// <summary>
        /// 喂纸条目并渲染第一页(打开本屏前调用)。
        /// </summary>
        /// <param name="papers">48 内容表的 `<c>papers[]</c>`(可空集 ⇒ 只留纸面,零文字)。</param>
        /// <param name="startIndex">起始页序(越界 ⇒ 夹到界内)。</param>
        /// <exception cref="ArgumentNullException"><paramref name="papers"/> 为 null。</exception>
        public void Bind(IReadOnlyList<TutorialPaperRow> papers, int startIndex = 0)
        {
            _papers = papers ?? throw new ArgumentNullException(nameof(papers),
                "未给出纸条目 —— 空纸不是「还没读到」,是**数据没接上**(拒以空集冒充已绑定)");

            if (papers.Count == 0) _index = -1;
            else _index = Math.Max(0, Math.Min(startIndex, papers.Count - 1));

            Render();
        }

        /// <summary>
        /// 翻页(<paramref name="delta"/> 为 ±1;越界即停,不回绕)。
        /// </summary>
        /// <returns>是否真的翻了(越界 / 未绑定 ⇒ <c>false</c>)。</returns>
        public bool TurnPage(int delta)
        {
            if (PageCount == 0 || delta == 0) return false;
            int next = _index + delta;
            if (next < 0 || next >= PageCount) return false;
            _index = next;
            Render();
            return true;
        }

        /// <summary>构建教学纸近景 UI(只建一次;内容由 <see cref="Bind"/> 填)。</summary>
        private void BuildUI()
        {
            var paperContent = new VisualElement { name = "paper-content" };
            _paper = _library.Create(SkeuoElement.Paper);
            _paper.name = PaperElementName;
            paperContent.Add(_paper);

            _title = new Label { name = TitleElementName };
            _title.AddToClassList("paper-title");
            _paper.Add(_title);

            _body = new Label { name = BodyElementName };
            _body.AddToClassList("paper-body");
            _paper.Add(_body);

            _missingMark = new VisualElement { name = MissingMarkElementName };
            _missingMark.AddToClassList("paper-missing-mark");
            _missingMark.style.display = DisplayStyle.None;
            _paper.Add(_missingMark);

            _pageHint = new Label { name = PageHintElementName };
            _pageHint.AddToClassList("paper-page-hint");
            _paper.Add(_pageHint);

            HideText();
            _root.Add(paperContent);
        }

        private void Render()
        {
            if (_index < 0 || _papers == null || _index >= _papers.Count)
            {
                HideText();
                return;
            }

            TutorialPaperRow row = _papers[_index];
            _title.text = row.Title ?? string.Empty;

            // 缺页(T6):只剩标题,正文不摆出来,纸面压一道撕口。
            bool missing = IsCurrentPageMissing;
            _body.text = missing ? string.Empty : (row.Body ?? string.Empty);
            _paper.EnableInClassList(MissingClass, missing);
            _missingMark.style.display = missing ? DisplayStyle.Flex : DisplayStyle.None;

            // 单页不显示页码:「一张纸」没有页序可言,多显示反而像分页器(拟物纪律)
            _pageHint.text = PageCount > 1 ? $"第 {_index + 1} 页 / 共 {PageCount} 页" : string.Empty;
        }

        private void HideText()
        {
            _title.text = string.Empty;
            _body.text = string.Empty;
            _pageHint.text = string.Empty;
            _paper.EnableInClassList(MissingClass, false);
            if (_missingMark != null) _missingMark.style.display = DisplayStyle.None;
        }
    }
}
