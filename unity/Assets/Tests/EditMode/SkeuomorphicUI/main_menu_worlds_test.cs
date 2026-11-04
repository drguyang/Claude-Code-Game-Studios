// 主菜单的**世界列表**形态(ADR-040 D9 · 2026-11-04)。
//
// 权威:
//   · `design/ux/main-menu-42.md`(**2026-11-04 修订**:单行 → 世界列表 + 新起)
//   · ADR-040 D4 / D5 / D9 —— 选哪个世界是玩家的事;42 只发意图(ADR-013 §9 C3)
//
// 本件断四件:
//   ① **没有世界** ⇒ 单行「开始诊治」,确认 = `NewWorld`(不是 `Continue` —— 没有可继续的东西);
//   ② **有世界** ⇒ 每个世界一行 + 末行「新起一局」,**第一行**文案恒为「继续诊治」且动作 = `Continue`;
//   ③ 其余世界行按装配层给的文案渲染,确认 = `SelectWorld(seq)`(42 **不拼**文案);
//   ④ 重画**不留旧行**(SetWorlds 连续调用后行数与最新列表一致)。

using System.Collections.Generic;
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
            public readonly List<int> Selected = new List<int>();
            public readonly List<int> Deleted = new List<int>();

            public void Continue() => ContinueCount++;
            public void SelectWorld(int seq) => Selected.Add(seq);
            public void NewWorld() => NewWorldCount++;
            public void DeleteWorld(int seq) => Deleted.Add(seq);
        }

        private static MainMenuScreen NewScreen(out RecordingSink sink)
        {
            var screen = new MainMenuScreen(new VisualElement(), new SkeuoElementLibrary());
            sink = new RecordingSink();
            screen.BindCommandSink(sink);
            return screen;
        }

        // ══════════════════════════════════════════════════════════════════
        // ① 没有世界 ⇒ 单行「开始诊治」,动作 = 新起
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_noWorlds_singleRow_andConfirmStartsNewWorld()
        {
            MainMenuScreen screen = NewScreen(out RecordingSink sink);

            screen.SetHasContinueSave(false);
            screen.SetWorlds(new List<MainMenuWorldView>());

            Assert.That(screen.RowCount, Is.EqualTo(1), "没有世界 ⇒ 与旧形态一样只有一行");
            Assert.That(screen.RowTextAt(0), Is.EqualTo(MainMenuScreen.StartText));

            screen.ActivateRow(0);

            Assert.That(sink.NewWorldCount, Is.EqualTo(1),
                "没有世界时「继续」无从谈起 —— 确认必须走 NewWorld");
            Assert.That(sink.ContinueCount, Is.EqualTo(0));
        }

        // ══════════════════════════════════════════════════════════════════
        // ② / ③ 有世界 ⇒ 每个世界一行 + 新起一行
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_worlds_renderRows_andRouteToTheRightIntent()
        {
            MainMenuScreen screen = NewScreen(out RecordingSink sink);

            screen.SetHasContinueSave(true);
            screen.SetWorlds(new List<MainMenuWorldView>
            {
                new MainMenuWorldView(3, "第 3 局"),
                new MainMenuWorldView(1, "第 1 局"),
            });

            Assert.That(screen.RowCount, Is.EqualTo(4), "两个世界 + 「新起一局」+ 「删一局」");
            Assert.That(screen.RowTextAt(0), Is.EqualTo(MainMenuScreen.ContinueText),
                "第一行恒为「继续诊治」(最近游玩的世界),不用装配层给的文案");
            Assert.That(screen.RowTextAt(1), Is.EqualTo("第 1 局"),
                "其余世界行用装配层给的成品文案(42 不拼字符串)");
            Assert.That(screen.RowTextAt(2), Is.EqualTo(MainMenuScreen.NewWorldText));
            Assert.That(screen.RowAt(0).name, Is.EqualTo("main-menu-world-3"));
            Assert.That(screen.RowAt(1).name, Is.EqualTo("main-menu-world-1"));
            Assert.That(screen.RowAt(2).name, Is.EqualTo("main-menu-new"));

            screen.ActivateRow(0);
            Assert.That(sink.ContinueCount, Is.EqualTo(1), "第一行 = 继续(最近世界)");
            Assert.That(sink.Selected, Is.Empty, "第一行不是「选某个具体序号的另一个世界」");

            screen.ActivateRow(1);
            Assert.That(sink.Selected, Is.EqualTo(new[] { 1 }),
                "第二个世界行必须带自己的序号");

            screen.ActivateRow(2);
            Assert.That(sink.NewWorldCount, Is.EqualTo(1));
        }

        // ══════════════════════════════════════════════════════════════════
        // ④ 重画不留旧行
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_setWorldsTwice_doesNotLeakRows()
        {
            MainMenuScreen screen = NewScreen(out _);

            screen.SetWorlds(new List<MainMenuWorldView>
            {
                new MainMenuWorldView(1, "第 1 局"),
                new MainMenuWorldView(2, "第 2 局"),
                new MainMenuWorldView(3, "第 3 局"),
            });
            Assert.That(screen.RowCount, Is.EqualTo(5), "3 个世界 + 新起 + 删一局");

            screen.SetWorlds(new List<MainMenuWorldView> { new MainMenuWorldView(9, "第 9 局") });
            Assert.That(screen.RowCount, Is.EqualTo(3), "行数必须跟最新列表走,不累积");
            Assert.That(screen.RowTextAt(1), Is.EqualTo(MainMenuScreen.NewWorldText));
        }

        // ══════════════════════════════════════════════════════════════════
        // 删除世界(ADR-040 D6):两次都必须落在同一个世界上
        // ══════════════════════════════════════════════════════════════════

        private static List<MainMenuWorldView> TwoWorlds() => new List<MainMenuWorldView>
        {
            new MainMenuWorldView(3, "第 3 局"),
            new MainMenuWorldView(1, "第 1 局"),
        };

        [Test]
        public void test_deleteRow_onlyExistsWhenThereAreWorlds()
        {
            MainMenuScreen screen = NewScreen(out _);

            screen.SetWorlds(new List<MainMenuWorldView>());
            for (int i = 0; i < screen.RowCount; i++)
                Assert.That(screen.RowTextAt(i), Is.Not.EqualTo(MainMenuScreen.DeleteWorldText),
                    "没有世界 ⇒ 「删一局」没有宾语,不该出现");

            screen.SetWorlds(TwoWorlds());
            Assert.That(screen.RowAt(screen.RowCount - 1).name, Is.EqualTo("main-menu-delete"));
            Assert.That(screen.RowTextAt(screen.RowCount - 1), Is.EqualTo(MainMenuScreen.DeleteWorldText));
        }

        [Test]
        public void test_deleteFlow_requiresTwoPressesOnTheSameWorld()
        {
            MainMenuScreen screen = NewScreen(out RecordingSink sink);
            screen.SetWorlds(TwoWorlds());

            screen.ActivateRow(screen.RowCount - 1);          // 「删一局」
            Assert.That(screen.Mode, Is.EqualTo(MainMenuMode.DeleteSelect));
            Assert.That(screen.RowTextAt(0), Is.EqualTo("删除 第 3 局"));
            Assert.That(screen.RowTextAt(screen.RowCount - 1), Is.EqualTo(MainMenuScreen.BackText));

            screen.ActivateRow(0);                            // 第一次:选目标
            Assert.That(screen.Mode, Is.EqualTo(MainMenuMode.DeleteConfirm));
            Assert.That(screen.PendingDeleteSeq, Is.EqualTo(3));
            Assert.That(screen.RowTextAt(0), Does.StartWith("再按一次确认"));
            Assert.That(sink.Deleted, Is.Empty, "第一次按**不得**删除任何东西");

            screen.ActivateRow(0);                            // 第二次:同一行 ⇒ 真删
            Assert.That(sink.Deleted, Is.EqualTo(new[] { 3 }));
        }

        [Test]
        public void test_deleteFlow_pressingAnotherWorld_reArmsInsteadOfDeleting()
        {
            MainMenuScreen screen = NewScreen(out RecordingSink sink);
            screen.SetWorlds(TwoWorlds());

            screen.ActivateRow(screen.RowCount - 1);          // 删一局
            screen.ActivateRow(0);                            // 选第 3 局
            screen.ActivateRow(1);                            // 改成第 1 局

            Assert.That(sink.Deleted, Is.Empty, "换目标 = 重新进入待确认,不是删除");
            Assert.That(screen.PendingDeleteSeq, Is.EqualTo(1));
            Assert.That(screen.RowTextAt(1), Does.StartWith("再按一次确认"));
            Assert.That(screen.RowTextAt(0), Is.EqualTo("删除 第 3 局"),
                "被换掉的目标必须退回未确认文案");
        }

        [Test]
        public void test_deleteFlow_backRow_returnsToNormal()
        {
            MainMenuScreen screen = NewScreen(out RecordingSink sink);
            screen.SetWorlds(TwoWorlds());

            screen.ActivateRow(screen.RowCount - 1);
            screen.ActivateRow(screen.RowCount - 1);          // 「返回」

            Assert.That(screen.Mode, Is.EqualTo(MainMenuMode.Normal));
            Assert.That(screen.RowAt(0).name, Is.EqualTo("main-menu-world-3"));
            Assert.That(sink.Deleted, Is.Empty);
        }

        [Test]
        public void test_setWorlds_resetsDeleteMode()
        {
            MainMenuScreen screen = NewScreen(out _);
            screen.SetWorlds(TwoWorlds());
            screen.ActivateRow(screen.RowCount - 1);
            Assert.That(screen.Mode, Is.EqualTo(MainMenuMode.DeleteSelect));

            screen.SetWorlds(TwoWorlds());                    // 装配层回推新列表

            Assert.That(screen.Mode, Is.EqualTo(MainMenuMode.Normal), "新列表 ⇒ 删除态作废");
            Assert.That(screen.PendingDeleteSeq, Is.EqualTo(-1));
        }

        // ══════════════════════════════════════════════════════════════════
        // 越界确认 = 无操作(不抛)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_activateRow_outOfRange_isNoOp()
        {
            MainMenuScreen screen = NewScreen(out RecordingSink sink);

            Assert.DoesNotThrow(() => screen.ActivateRow(-1));
            Assert.DoesNotThrow(() => screen.ActivateRow(99));
            Assert.That(sink.ContinueCount + sink.NewWorldCount, Is.EqualTo(0));
        }
    }
}
