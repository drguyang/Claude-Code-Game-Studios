// 拾取面板(`ModalId.Loot` 第十一员 · 2026-11-14) —— **屏**的判据夹具
//
// 权威:
//   · `docs/architecture/adr-013-*.md` §十-H Amendment G(闭集第十一员)
//   · `design/ux/loot-42.md`(本屏 spec,与代码同批落盘,未经 `/ux-review`)
//   · 用户 2026-11-14 原话:「掉落捡起时有物品栏,点击单个进入背包,
//     物品栏下方有全部捡取可以批量进入背包」
//
// ⚠️ 本文件只测**屏**(纯呈现面)。生产出口 `DriverLootActionSink`(真流结算 / 合屏)
//   的判据在 `Tests/EditMode/Boot/driver_loot_action_sink_test.cs` —— 那条链要引
//   `Gameplay.Boot`,而本目录的 asmdef(`SkeuomorphicUI.Tests`)刻意只看得见
//   `Gameplay.UI` / `Gameplay.Presentation`(与 `dto_boundary_and_modal_test` 同一道边界)。
//
// ─────────────────────────────────────────────────────────────────────────────
// 本文件要证明的五件事
// ─────────────────────────────────────────────────────────────────────────────
//   ① 行 = 喂进来多少件画多少行,行文字原样(屏不查物品表、不拼文案);
//   ② 空集 ⇒ 空态一行 + 「全部拾取」不可落焦(不死界面);
//   ③ 逐行激活 ⇒ 只报**那一件**的 instance_id(不多报、不报错件);
//   ④ 「全部拾取」激活 ⇒ 只报一次 ClaimAll;空集时不报;
//   ⑤ 重建 ⇒ 行数随喂入收缩、剩余行保序。
//
// ⚠️ **未覆盖**:`RegisterCallback<NavigationSubmitEvent>` / `ClickEvent` 的**事件绑定本身**
//   —— EditMode 下无 `PanelSettings`(`panel == null`),`SendEvent` 不派发
//   (与 `case_close_screen_test` 同款实测)。本文件因此直调屏上的公开入口
//   (`ActivateRow` / `ActivateTakeAll`,与回调是同一实现体);
//   **真事件绑定 = 真包走查**,目前无走查件 ⇒ 不借绿。

using System.Collections.Generic;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic.Screens;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace DaYiJingCheng.Tests.SkeuomorphicUI
{
    [TestFixture]
    public sealed class loot_screen_test
    {
        private VisualElement _root;
        private LootScreen _screen;
        private RecordingLootSink _sink;

        private sealed class RecordingLootSink : ILootActionSink
        {
            public readonly List<long> Claimed = new List<long>();
            public int ClaimAllCount;

            public void Claim(long instanceId) => Claimed.Add(instanceId);
            public void ClaimAll() => ClaimAllCount++;
        }

        [SetUp]
        public void Setup()
        {
            _root = new VisualElement();
            _screen = new LootScreen(_root, new SkeuoElementLibrary());
            _sink = new RecordingLootSink();
            _screen.Bind(_sink);
        }

        [Test]
        public void test_modalId_isLoot()
        {
            Assert.AreEqual(ModalId.Loot, _screen.Modal);
            Assert.AreEqual(11, (int)ModalId.Loot, "第十一员接在末尾(ADR-013 §十-H)");
        }

        [Test]
        public void test_present_buildsOneRowPerItem_verbatimLabels()
        {
            _screen.Present(new[]
            {
                new LootRow(41, "门板 ×1"),
                new LootRow(42, "止血草 ×3"),
            });

            Assert.AreEqual(2, _screen.RowCount);
            Assert.AreEqual("门板 ×1", _screen.RowLabelAt(0), "行文字原样 —— 屏不拼文案");
            Assert.AreEqual("止血草 ×3", _screen.RowLabelAt(1));
            Assert.AreEqual(41L, _screen.InstanceIdAt(0));
            Assert.AreEqual(42L, _screen.InstanceIdAt(1));
            Assert.IsTrue(_screen.IsTakeAllActive, "有行 ⇒ 全部拾取可落焦");
            CollectionAssert.AreEqual(new long[] { 41, 42 }, _screen.ListedInstanceIds);
        }

        [Test]
        public void test_present_empty_showsEmptyState_andDisablesTakeAll()
        {
            _screen.Present(new LootRow[0]);

            Assert.AreEqual(0, _screen.RowCount);
            Assert.IsFalse(_screen.IsTakeAllActive, "空集 ⇒ 「全部拾取」不可落焦");
            Assert.IsNull(_screen.FirstFocusable, "空集无可落焦点");

            _screen.ActivateTakeAll();
            Assert.AreEqual(0, _sink.ClaimAllCount, "空集时「全部拾取」不得报意图");
        }

        [Test]
        public void test_activateRow_reportsThatInstanceOnly()
        {
            _screen.Present(new[]
            {
                new LootRow(41, "甲"),
                new LootRow(42, "乙"),
            });

            _screen.ActivateRow(1);

            CollectionAssert.AreEqual(new long[] { 42 }, _sink.Claimed,
                "逐行激活只报**那一件**");
            Assert.AreEqual(0, _sink.ClaimAllCount);
        }

        [Test]
        public void test_activateTakeAll_reportsOnce()
        {
            _screen.Present(new[]
            {
                new LootRow(41, "甲"),
                new LootRow(42, "乙"),
            });

            _screen.ActivateTakeAll();

            Assert.AreEqual(1, _sink.ClaimAllCount, "批量 = 一次意图(逐件在装配层)");
            Assert.AreEqual(0, _sink.Claimed.Count, "批量不走逐件回调");
        }

        [Test]
        public void test_present_rebuild_shrinksList_withoutLosingOtherRows()
        {
            _screen.Present(new[]
            {
                new LootRow(41, "甲"),
                new LootRow(42, "乙"),
                new LootRow(43, "丙"),
            });

            _screen.Present(new[]
            {
                new LootRow(42, "乙"),
                new LootRow(43, "丙"),
            });

            Assert.AreEqual(2, _screen.RowCount, "重建后行数随喂入收缩");
            CollectionAssert.AreEqual(new long[] { 42, 43 }, _screen.ListedInstanceIds,
                "剩余行保序(屏不定序,抹掉的只是拿走的那些)");
        }
    }
}
