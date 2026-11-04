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

            public void Continue() => ContinueCount++;
            public void SelectWorld(int seq) => Selected.Add(seq);
            public void NewWorld() => NewWorldCount++;
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

            Assert.That(screen.RowCount, Is.EqualTo(3), "两个世界 + 「新起一局」");
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
            Assert.That(screen.RowCount, Is.EqualTo(4));

            screen.SetWorlds(new List<MainMenuWorldView> { new MainMenuWorldView(9, "第 9 局") });
            Assert.That(screen.RowCount, Is.EqualTo(2), "行数必须跟最新列表走,不累积");
            Assert.That(screen.RowTextAt(1), Is.EqualTo(MainMenuScreen.NewWorldText));
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
