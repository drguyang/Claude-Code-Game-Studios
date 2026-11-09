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

        // ══════════════ 缺页(T6 · 钩子病例的载体)══════════════

        /// <summary>
        /// 被判为缺页的那一张**只剩标题**:正文不摆出来。
        /// <para>反证:Render 里把 `missing ? string.Empty : …` 改成恒取正文 ⇒ 本条红。</para>
        /// </summary>
        [Test]
        public void test_missingPage_showsTitleOnly()
        {
            PaperCloseupScreen screen = NewScreen();
            screen.SetMissingPaperIds(new[] { "p1" });
            screen.Bind(new[] { Row("p1", "止血包扎", "以带压住上臂动脉。") });

            Assert.That(screen.IsCurrentPageMissing, Is.True);
            Assert.That(screen.CurrentTitle, Is.EqualTo("止血包扎"),
                "标题必须还在 —— 「我知道有这一招」是钩子病例的全部牵引力");
            Assert.That(screen.CurrentBody, Is.Empty, "缺页不摆正文");
        }

        /// <summary>缺页态在纸面挂类、并显出撕口;正文页两者都不出现。</summary>
        [Test]
        public void test_missingPage_togglesClassAndMark()
        {
            PaperCloseupScreen screen = NewScreen();
            screen.SetMissingPaperIds(new[] { "p1" });
            screen.Bind(new[] { Row("p1", "止血包扎", "正文"), Row("p2", "望诊", "正文二") });

            var paper = _root.Q<VisualElement>("paper");
            Assert.That(paper, Is.Not.Null, "前置:纸面元素须取得到(否则下面的类断言在 null 上静默)");
            Assert.That(paper.ClassListContains(PaperCloseupScreen.MissingClass), Is.True);

            VisualElement mark = _root.Q<VisualElement>(PaperCloseupScreen.MissingMarkElementName);
            Assert.That(mark, Is.Not.Null);
            Assert.That(mark.style.display.value, Is.EqualTo(DisplayStyle.Flex));

            screen.TurnPage(+1);   // 翻到非缺页
            Assert.That(screen.IsCurrentPageMissing, Is.False);
            Assert.That(paper.ClassListContains(PaperCloseupScreen.MissingClass), Is.False,
                "换页后缺页类必须撤掉,否则整本书都像被撕过");
            Assert.That(mark.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(screen.CurrentBody, Is.EqualTo("正文二"));
        }

        /// <summary>未声明任何缺页 ⇒ 全部照常(既有行为零变化)。</summary>
        [Test]
        public void test_noMissingDeclaration_rendersNormally()
        {
            PaperCloseupScreen screen = NewScreen();
            screen.Bind(new[] { Row("p1", "望诊", "先看气色。") });

            Assert.That(screen.IsCurrentPageMissing, Is.False);
            Assert.That(screen.CurrentBody, Is.EqualTo("先看气色。"));
        }

        /// <summary>`null` 等价于「无缺页」(不是「全部缺页」—— 方向错一本手册就全白)。</summary>
        [Test]
        public void test_nullMissingSet_meansNoneMissing()
        {
            PaperCloseupScreen screen = NewScreen();
            screen.Bind(new[] { Row("p1", "望诊", "先看气色。") });
            screen.SetMissingPaperIds(null);

            Assert.That(screen.IsCurrentPageMissing, Is.False);
            Assert.That(screen.CurrentBody, Is.EqualTo("先看气色。"));
        }

        /// <summary>缺页判定按 **id** 不按页序:换页序不影响哪一张缺。</summary>
        [Test]
        public void test_missingIsKeyedById_notByPageOrder()
        {
            PaperCloseupScreen screen = NewScreen();
            screen.SetMissingPaperIds(new[] { "p2" });
            screen.Bind(new[] { Row("p1", "甲", "一"), Row("p2", "乙", "二") });

            Assert.That(screen.IsCurrentPageMissing, Is.False, "第 1 页是 p1,不缺");
            screen.TurnPage(+1);
            Assert.That(screen.IsCurrentPageMissing, Is.True, "第 2 页是 p2,缺");
            Assert.That(screen.CurrentBody, Is.Empty);
        }
    }
}
