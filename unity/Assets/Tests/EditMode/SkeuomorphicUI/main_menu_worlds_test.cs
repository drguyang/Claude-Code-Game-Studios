// 主菜单的**固定行形态**(2026-11-04 用户裁定「无论多少世界,主菜单暂时固定三行,和世界多少无关」
// —— 落定为**四行**:继续诊治 / 新起一局 / 设置 / 退出游戏)。
//
// 权威:
//   · `design/ux/main-menu-42.md`(2026-11-04 修订:行集恒定,与世界数无关)
//   · ADR-040(**撤销注**:世界列表与「删一局」自 2026-11-04 起不存在)
//   · ADR-013 §9 C3 —— 42 只发意图
//
// 本件断五件:
//   ① 行集恒定 = 4,且**不随世界数 / 存档数变化**;
//   ② 第一行文案由「有没有档」决定(继续诊治 / 开始诊治),动作恒为 `Continue`;
//   ③ 「新起一局」「设置」「退出游戏」各路由到自己的意图(**不得串线**);
//   ④ 未注入 sink ⇒ 按了留空、不抛(未接线的诚实形态);
//   ⑤ 越界确认 = 无操作。

using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic.Screens;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace DaYiJingCheng.Tests.SkeuomorphicUI
{
    [TestFixture]
    public sealed class MainMenuWorldsTest
    {
        private sealed class RecordingSink : IMainMenuCommandSink
        {
            public int ContinueCount;
            public int NewWorldCount;
            public int OpenSettingsCount;
            public int QuitCount;

            public void Continue() => ContinueCount++;
            public void NewWorld() => NewWorldCount++;
            public void OpenSettings() => OpenSettingsCount++;
            public void Quit() => QuitCount++;
        }

        private static MainMenuScreen NewScreen(out RecordingSink sink)
        {
            var screen = new MainMenuScreen(new VisualElement(), new SkeuoElementLibrary());
            sink = new RecordingSink();
            screen.BindCommandSink(sink);
            return screen;
        }

        // ══════════════════════════════════════════════════════════════════
        // ① 行集恒定
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_rowSet_isFixedFourRows_regardlessOfSaveState()
        {
            MainMenuScreen screen = NewScreen(out _);

            Assert.That(screen.RowCount, Is.EqualTo(MainMenuScreen.FixedRowCount),
                "行数 = 契约常值,与世界数 / 存档数无关");
            Assert.That(screen.RowCount, Is.EqualTo(4));

            screen.SetHasContinueSave(false);
            Assert.That(screen.RowCount, Is.EqualTo(4), "无档不得改变行数");

            screen.SetHasContinueSave(true);
            Assert.That(screen.RowCount, Is.EqualTo(4), "有档不得改变行数");

            Assert.That(screen.RowAt(0).name, Is.EqualTo("main-menu-continue"));
            Assert.That(screen.RowAt(1).name, Is.EqualTo("main-menu-new"));
            Assert.That(screen.RowAt(2).name, Is.EqualTo("main-menu-settings"));
            Assert.That(screen.RowAt(3).name, Is.EqualTo("main-menu-quit"));
        }

        // ══════════════════════════════════════════════════════════════════
        // ② 第一行文案随「有没有档」切换,动作恒为 Continue
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_firstRow_textFollowsSaveState_actionIsAlwaysContinue()
        {
            MainMenuScreen screen = NewScreen(out RecordingSink sink);

            screen.SetHasContinueSave(false);
            Assert.That(screen.RowTextAt(0), Is.EqualTo(MainMenuScreen.StartText));
            screen.ActivateRow(0);
            Assert.That(sink.ContinueCount, Is.EqualTo(1),
                "无档时第一行也发 Continue —— 「开新局 vs 继续」由装配层判(spec §6)");
            Assert.That(sink.NewWorldCount, Is.EqualTo(0), "第一行**不得**串到「新起一局」");

            screen.SetHasContinueSave(true);
            Assert.That(screen.RowTextAt(0), Is.EqualTo(MainMenuScreen.ContinueText));
        }

        // ══════════════════════════════════════════════════════════════════
        // ③ 三行各走各的意图(串线是本屏最容易犯的错)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_rows_routeToDistinctIntents()
        {
            MainMenuScreen screen = NewScreen(out RecordingSink sink);
            screen.SetHasContinueSave(true);

            Assert.That(screen.RowTextAt(1), Is.EqualTo(MainMenuScreen.NewWorldText));
            Assert.That(screen.RowTextAt(2), Is.EqualTo(MainMenuScreen.ShellEntryText));
            Assert.That(screen.RowTextAt(3), Is.EqualTo(MainMenuScreen.QuitText));

            screen.ActivateRow(1);
            Assert.That(sink.NewWorldCount, Is.EqualTo(1));
            Assert.That(sink.ContinueCount, Is.EqualTo(0));

            screen.ActivateRow(2);
            Assert.That(sink.OpenSettingsCount, Is.EqualTo(1));
            Assert.That(sink.NewWorldCount, Is.EqualTo(1), "设置**不得**顺带开新局");

            screen.ActivateRow(3);
            Assert.That(sink.QuitCount, Is.EqualTo(1));
            Assert.That(sink.OpenSettingsCount, Is.EqualTo(1), "退出**不得**顺带开设置");
        }

        // ══════════════════════════════════════════════════════════════════
        // ④ 未接线 ⇒ 按了留空,不抛
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_noSink_activateIsNoOp()
        {
            var screen = new MainMenuScreen(new VisualElement(), new SkeuoElementLibrary());

            Assert.DoesNotThrow(() => screen.ActivateRow(0));
            Assert.DoesNotThrow(() => screen.ActivateRow(1));
            Assert.DoesNotThrow(() => screen.ActivateRow(2));
            Assert.DoesNotThrow(() => screen.ActivateRow(3));
        }

        // ══════════════════════════════════════════════════════════════════
        // ⑤ 越界确认 = 无操作(不抛)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_activateRow_outOfRange_isNoOp()
        {
            MainMenuScreen screen = NewScreen(out RecordingSink sink);

            Assert.DoesNotThrow(() => screen.ActivateRow(-1));
            Assert.DoesNotThrow(() => screen.ActivateRow(99));
            Assert.That(sink.ContinueCount + sink.NewWorldCount
                        + sink.OpenSettingsCount + sink.QuitCount, Is.EqualTo(0));
        }
    }
}
