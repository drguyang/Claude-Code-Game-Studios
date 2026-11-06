// S25-4 · tutorial-004 —— 纸近景屏的内容绑定判据(规则二 ③ · AC-4 的「内容面」半边)
//
// 权威来源:
//   · `design/ux/paper-closeup-48.md`(本屏 per-screen spec)· ADR-013 §十-B(闭集第七员)
//   · `design/gdd/tutorial-and-onboarding.md` 规则二(纸 = 世界内静态道具,走近摊开)
//   · Story: production/epics/tutorial-onboarding/story-004-three-media-scheduling-paper-modal.md
//
// ⚠️ 本件只判**内容面**(文本从哪来、翻页边界、模态身份)。
//    「走近 → 摊开」的世界侧半边需要 6 的纸道具摆放 + 4 的交互判定,**不在本件**:
//    那半边记 `NOT-RUN`,理由见证据件。

using System;
using DaYiJingCheng.Gameplay.Presentation.Skeuomorphic;
using DaYiJingCheng.Gameplay.Presentation.Tutorial;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic.Screens;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace DaYiJingCheng.Tests.EditMode.Tutorial
{
    [TestFixture]
    public class paper_closeup48_screen_test
    {
        private static TutorialPaperRow Row(string id, string title, string body, string illustration = null)
            => new TutorialPaperRow(id, "observe", title, body, "4:3", illustration);

        private VisualElement _root;

        [SetUp]
        public void SetUp() => _root = new VisualElement();

        private PaperCloseupScreen NewScreen()
            => new PaperCloseupScreen(_root, new SkeuoElementLibrary());

        // ══════════════ 内容绑定 ══════════════

        [Test]
        public void test_bind_rendersTitleAndBodyFromRow()
        {
            PaperCloseupScreen screen = NewScreen();

            screen.Bind(new[] { Row("p1", "望闻问切", "先看气色,再听声息。", "paper_obs") });

            Assert.That(screen.CurrentTitle, Is.EqualTo("望闻问切"));
            Assert.That(screen.CurrentBody, Is.EqualTo("先看气色,再听声息。"));
            Assert.That(screen.CurrentIllustrationKey, Is.EqualTo("paper_obs"),
                "插图键必须原样透出(接图归 42 的元件库,屏不自造图形)");
        }

        [Test]
        public void test_bind_emptySet_keepsPaperWithoutText()
        {
            PaperCloseupScreen screen = NewScreen();

            screen.Bind(Array.Empty<TutorialPaperRow>());

            Assert.That(screen.PageCount, Is.EqualTo(0));
            Assert.That(screen.CurrentPage, Is.EqualTo(-1));
            Assert.That(screen.CurrentTitle, Is.Empty);
            Assert.That(screen.CurrentBody, Is.Empty);
            // 纸面仍在(空纸 ≠ 没绑定:屏不因数据空而消失)
            Assert.That(_root.Q<VisualElement>("paper-content"), Is.Not.Null);
        }

        [Test]
        public void test_guard_nullPapers_throws()
        {
            PaperCloseupScreen screen = NewScreen();
            var ex = Assert.Throws<ArgumentNullException>(() => screen.Bind(null));
            Assert.That(ex.ParamName, Is.EqualTo("papers"));
        }

        // ══════════════ 翻页 ══════════════

        [Test]
        public void test_turnPage_advancesThenStopsAtEnds()
        {
            PaperCloseupScreen screen = NewScreen();
            screen.Bind(new[]
            {
                Row("p1", "一", "第一张"),
                Row("p2", "二", "第二张"),
            });

            Assert.That(screen.CurrentPage, Is.EqualTo(0));
            Assert.That(screen.TurnPage(+1), Is.True);
            Assert.That(screen.CurrentTitle, Is.EqualTo("二"));
            Assert.That(screen.TurnPage(+1), Is.False, "到尾即停(不回绕 —— 纸是一张张翻的)");
            Assert.That(screen.CurrentTitle, Is.EqualTo("二"), "越界翻页不得改内容");

            Assert.That(screen.TurnPage(-1), Is.True);
            Assert.That(screen.CurrentTitle, Is.EqualTo("一"));
            Assert.That(screen.TurnPage(-1), Is.False, "到头即停");
        }

        [Test]
        public void test_turnPage_unboundOrZeroDelta_isNoOp()
        {
            PaperCloseupScreen screen = NewScreen();
            Assert.That(screen.TurnPage(+1), Is.False, "未绑定 ⇒ 翻页无操作");

            screen.Bind(new[] { Row("p1", "一", "第一张"), Row("p2", "二", "第二张") });
            Assert.That(screen.TurnPage(0), Is.False);
            Assert.That(screen.CurrentPage, Is.EqualTo(0));
        }

        [Test]
        public void test_bind_startIndex_outOfRange_isClamped()
        {
            PaperCloseupScreen screen = NewScreen();
            var papers = new[] { Row("p1", "一", "甲"), Row("p2", "二", "乙") };

            screen.Bind(papers, startIndex: 99);
            Assert.That(screen.CurrentPage, Is.EqualTo(1), "越界起始页夹到最后一页");

            screen.Bind(papers, startIndex: -5);
            Assert.That(screen.CurrentPage, Is.EqualTo(0), "越界起始页夹到第一页");
        }

        // ══════════════ 页码:单页不显示 ══════════════

        [Test]
        public void test_singlePage_showsNoPageHint()
        {
            PaperCloseupScreen screen = NewScreen();
            screen.Bind(new[] { Row("p1", "一", "唯一一张") });

            Label hint = _root.Q<Label>(PaperCloseupScreen.PageHintElementName);
            Assert.That(hint, Is.Not.Null);
            Assert.That(hint.text, Is.Empty, "一张纸没有页序可言 —— 显示页码反而像分页器");
        }

        [Test]
        public void test_multiPage_showsPageHint()
        {
            PaperCloseupScreen screen = NewScreen();
            screen.Bind(new[] { Row("p1", "一", "甲"), Row("p2", "二", "乙"), Row("p3", "三", "丙") });
            screen.TurnPage(+1);

            Label hint = _root.Q<Label>(PaperCloseupScreen.PageHintElementName);
            Assert.That(hint.text, Does.Contain("2").And.Contain("3"));
        }

        // ══════════════ 模态身份 ══════════════

        [Test]
        public void test_modalId_isPaperCloseup48()
        {
            Assert.That(NewScreen().Modal, Is.EqualTo(ModalId.PaperCloseup48),
                "纸近景必须是闭集第七员(ADR-013 §十-B)—— 身份错了,焦点单栈门就管不到它");
        }
    }
}
