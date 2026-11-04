// 主菜单「上次会话未完整保存」提示的夹具(2026-11-04)。
//
// 权威:
//   · `design/gdd/skeuomorphic-ui.md` **AC-42-H1**(BLOCKING)—— 本条是 `AC-7a-17` 后半句
//     在 42 侧的承接载体(此前 42 / 7b 都没有 AC ⇒ `TD-skeuo-05`)。
//   · ADR-013 §9 C3 —— 42 只渲染,**不判断**;标志位的置位 / 清除全归 7a。
//
// 本文件断四件(对应 AC 的 ①②③④):
//   ① 置位时该行**可见**,且原「继续诊治 / 开始诊治」那行**照旧可见**(并集,不是替换);
//   ② 该行**不进焦点序**、**不参与拾取**,`FirstFocusable` 仍指向唯一那一行;
//   ③ 未置位时该行**完全不占位**(display:none);
//   ④ 42 侧**无**读标志文件 / 扫存档目录的代码 —— 见文末那条结构断言。
//
// ⚠️ **不**断「7a 会不会置位」—— 那是 `SaveIncompleteFlag` / `SaveService` 的面
//    (`TD-persist-14` 已结案)。本条只断 42 消费这个布尔的方式。

using System.IO;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic.Screens;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace DaYiJingCheng.Tests.SkeuomorphicUI
{
    [TestFixture]
    public class MainMenuSessionNoticeTest
    {
        private static MainMenuScreen NewScreen()
            => new MainMenuScreen(new VisualElement(), new SkeuoElementLibrary());

        [Test]
        public void test_notice_isHidden_byDefault()
        {
            MainMenuScreen screen = NewScreen();

            Assert.That(screen.SessionIncompleteNoticeShown, Is.False,
                "AC-42-H1 ③:标志位为假时该行完全不占位");
            // 比 display:none 更彻底 —— 常态下元件**根本不存在**。
            // 为什么必须是这个形态(而不是常驻一个隐藏 Label):spec §5「一张纸、一行」的判据
            // 按子树内**文本元素个数**数(PlayMode `test_bootWithMenu_showsSingleRow_andLandsFocus`),
            // 常驻隐藏 Label 会让**什么都没发生**的常态也变成 2 个 —— 实测该夹具因此转红。
            Assert.That(screen.SessionIncompleteNotice, Is.Null,
                "AC-42-H1 ③:未置位时纸面上不得多出文本元件(否则「一张纸、一行」不成立)");
        }

        [Test]
        public void test_notice_isShown_whenFlagRaised()
        {
            MainMenuScreen screen = NewScreen();
            screen.SetSessionIncompleteNotice(true);

            Assert.That(screen.SessionIncompleteNoticeShown, Is.True,
                "AC-42-H1 ①:置位时该行可见");
            Assert.That(screen.SessionIncompleteNotice.text,
                Is.EqualTo(MainMenuScreen.SessionIncompleteText),
                "文案须是 AC 的原文措辞,不另造词");
        }

        [Test]
        public void test_notice_doesNotReplaceTheMainRow()
        {
            // 并集不是替换:两行同时可见,主行文案不受影响
            MainMenuScreen screen = NewScreen();
            screen.SetHasContinueSave(true);
            screen.SetSessionIncompleteNotice(true);

            Assert.That(screen.RowText, Is.EqualTo(MainMenuScreen.ContinueText),
                "AC-42-H1 ①:提示不得替换主行文案");
            Assert.That(screen.SessionIncompleteNoticeShown, Is.True);

            screen.SetHasContinueSave(false);
            Assert.That(screen.RowText, Is.EqualTo(MainMenuScreen.StartText),
                "主行文案仍随「有没有档」切换,与提示互不影响");
        }

        [Test]
        public void test_notice_isNotFocusable_andNotPicking()
        {
            MainMenuScreen screen = NewScreen();
            screen.SetSessionIncompleteNotice(true);

            Assert.That(screen.SessionIncompleteNotice.focusable, Is.False,
                "AC-42-H1 ②:提示不进焦点序");
            Assert.That(screen.SessionIncompleteNotice.pickingMode, Is.EqualTo(PickingMode.Ignore),
                "AC-42-H1 ②:提示不参与拾取(点它不该挡住下面的行)");
        }

        [Test]
        public void test_firstFocusable_staysTheOnlyRow()
        {
            MainMenuScreen screen = NewScreen();
            VisualElement before = screen.FirstFocusable;

            screen.SetSessionIncompleteNotice(true);

            Assert.That(screen.FirstFocusable, Is.SameAs(before),
                "AC-42-H1 ②:默认焦点仍是唯一那一行 —— 提示可见也不改变落焦");
            Assert.That(screen.FirstFocusable.ClassListContains("ruled"), Is.True,
                "落焦对象仍是「行」形态,不是提示");
        }

        [Test]
        public void test_notice_canBeHiddenAgain()
        {
            // 负控:置位 → 复位必须回到「完全不占位」
            MainMenuScreen screen = NewScreen();
            screen.SetSessionIncompleteNotice(true);
            screen.SetSessionIncompleteNotice(false);

            Assert.That(screen.SessionIncompleteNoticeShown, Is.False,
                "AC-42-H1 ③ 的反向:复位后必须重新不占位(否则旧提示会一直挂着)");
        }

        // ── ④ 42 不判断:类型树内零「读标志文件 / 扫存档目录」的代码 ──
        [Test]
        public void test_screen_hasNoFileOrDirectoryAccess()
        {
            const string rel = "Assets/Gameplay.UI/Skeuomorphic/Screens/MainMenuScreen.cs";
            Assert.IsTrue(File.Exists(rel), $"[前置] 找不到 {rel}(EditMode 的 CWD 应为工程根)");
            string src = File.ReadAllText(rel);

            // 去掉注释再扫 —— 本文件的注释里**故意**提到这些类型名(解释为什么不用它们),
            // 不去注释会把这个夹具变成「谁写了说明谁红」。
            string code = System.Text.RegularExpressions.Regex.Replace(
                src, @"/\*.*?\*/", "", System.Text.RegularExpressions.RegexOptions.Singleline);
            code = System.Text.RegularExpressions.Regex.Replace(code, @"//[^\n]*", "");

            foreach (string banned in new[] { "System.IO", "File.", "Directory.", "SaveIncompleteFlag" })
                Assert.IsFalse(code.Contains(banned),
                    $"AC-42-H1 ④:42 侧不得出现 `{banned}` —— 标志位的读写全归 7a(ADR-013 §9 C3)");
        }
    }
}
