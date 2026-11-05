// ADR-041(存档槽覆盖语义)的**屏级判据**(42 侧)。
//
// 权威:`docs/architecture/adr-041-save-slot-overwrite.md` §D1 / §D5 与 §Validation V-1 / V-2 / V-3。
//
// 本件断四件:
//   ① **空槽行 Submit = 落笔到它自己的槽号**(V-1);
//   ② **已有档行第一次 Submit 只展开二段态,零动作**(V-2 的屏侧半边);
//   ③ **已锁行**:「启封」灰置且**退出焦点序**,「重写」仍可用(V-3 的屏侧半边);
//   ④ **重画 / 翻页 ⇒ 二段态作废**(行重建后不得残留两枚指向旧行的墨字)。
//
// ⚠️ 屏只**转达意图**:能不能写由 7a 判(`AC-7b-02` 不变)。本件断的是「发没发、发给谁」。

using System.Collections.Generic;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic.Screens;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace DaYiJingCheng.Tests.SkeuomorphicUI
{
    [TestFixture]
    public sealed class Adr041SaveSlotChoicesTest
    {
        private sealed class RecordingSink : ISaveSlotCommandSink
        {
            public readonly List<uint> Loads = new List<uint>();
            public readonly List<uint> SaveAts = new List<uint>();
            public int SaveNextCount;

            public bool TryLoad(uint slotSeq) { Loads.Add(slotSeq); return true; }
            public bool TrySave() { SaveNextCount++; return true; }
            public bool TrySaveAt(uint slotSeq) { SaveAts.Add(slotSeq); return true; }
        }

        private static SaveSlotsScreen NewScreen(out RecordingSink sink)
        {
            var screen = new SaveSlotsScreen(new VisualElement(), new SkeuoElementLibrary());
            sink = new RecordingSink();
            screen.BindCommandSink(sink);
            return screen;
        }

        /// <summary>空槽 / 有档 / 已锁 三种行各一条(槽 3 / 4 / 5)。</summary>
        private static IReadOnlyList<SaveSlotView> Mixed() => new[]
        {
            new SaveSlotView(3u, known: true, hasSave: false, tick: 0L),
            new SaveSlotView(4u, known: true, hasSave: true, tick: 1200L, dayNumber: 1L),
            new SaveSlotView(5u, known: true, hasSave: true, tick: 2400L, dayNumber: 1L, isLocked: true),
        };

        // ══════════════════════════════════════════════════════════════════
        // ① 空槽行 = 落笔到自己的槽号
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_emptyRow_submit_writesItsOwnSlot()
        {
            SaveSlotsScreen screen = NewScreen(out RecordingSink sink);
            screen.SetSlotViews(Mixed());

            screen.Activate(0);                        // 槽 3 = 空

            Assert.That(sink.SaveAts, Is.EqualTo(new[] { 3u }),
                "空槽行落笔到**它自己的槽号**(写台账高水位 = 按 3 号写到别处)");
            Assert.That(sink.Loads, Is.Empty, "空槽行不得再走载入(旧形态的死胡同)");
            Assert.That(screen.ArmedRowIndex, Is.EqualTo(-1), "空槽没有二段态");
        }

        // ══════════════════════════════════════════════════════════════════
        // ② 已有档行 = 二段态,第一次 Submit 零动作
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_occupiedRow_firstSubmit_onlyArms()
        {
            SaveSlotsScreen screen = NewScreen(out RecordingSink sink);
            screen.SetSlotViews(Mixed());

            screen.Activate(1);                        // 槽 4 = 有档

            Assert.That(sink.Loads, Is.Empty, "第一次 Submit 不得载入");
            Assert.That(sink.SaveAts, Is.Empty, "第一次 Submit 也不得落笔");
            Assert.That(screen.ArmedRowIndex, Is.EqualTo(1), "而是展开二段态");

            SaveSlotItem row = screen.RowAt(1);
            Assert.That(row.IsArmed, Is.True);
            Assert.That(row.OpenChoice, Is.Not.Null, "须出示「启封」");
            Assert.That(row.RewriteChoice, Is.Not.Null, "须出示「重写」");

            // 再按同一行 = 不重复展开(墨字已摊开,由它们接手)
            screen.Activate(1);
            Assert.That(sink.Loads, Is.Empty);
            Assert.That(sink.SaveAts, Is.Empty);

            // 选「启封」⇒ 载入该槽号
            row.ActivateOpen();
            Assert.That(sink.Loads, Is.EqualTo(new[] { 4u }));
            Assert.That(screen.ArmedRowIndex, Is.EqualTo(-1), "选了就合回");
        }

        [Test]
        public void test_occupiedRow_rewrite_writesSameSlot()
        {
            SaveSlotsScreen screen = NewScreen(out RecordingSink sink);
            screen.SetSlotViews(Mixed());

            screen.Activate(1);
            screen.RowAt(1).ActivateRewrite();

            Assert.That(sink.SaveAts, Is.EqualTo(new[] { 4u }),
                "「重写」= 覆写**这一行自己的槽号**");
            Assert.That(sink.Loads, Is.Empty);
        }

        // ══════════════════════════════════════════════════════════════════
        // ③ 已锁行:「启封」灰置并退出焦点序,「重写」仍可用
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_lockedRow_doesNotEnterTwoStage_andSaysWhy()
        {
            // 🔁 **2026-11-05 口径改判(用户实测驱动)**:本条原断「已锁行仍进二段态,
            //    「启封」灰置、「重写」可用」。实际后果是**灾难性的**:焦点被落在
            //    「重写」上(`SaveSlotsScreen.ArmRow`),玩家以为在「读取」,一次确认就**覆写存档**。
            //    用户实测逐字:「点已有存档,选择读取,没有重新进入游戏」—— 真因是这个。
            //    依据:`save-slots-7b.md:143` 本来就写「已锁行**仍可聚焦**,Submit 给**拒绝反馈**」
            //    ⇒ 原实现**偏离 spec**,本条随之改成 spec 的形状。
            //    ⚠️ 副作用如实登记:UI 不再提供「覆写已锁槽」的入口(台账侧 ADR-041 §D3 仍允许)。
            SaveSlotsScreen screen = NewScreen(out RecordingSink sink);
            screen.SetSlotViews(Mixed());

            screen.Activate(2);                        // 槽 5 = 有档且已锁
            SaveSlotItem row = screen.RowAt(2);

            Assert.That(screen.ArmedRowIndex, Is.EqualTo(-1), "已锁行**不得**进二段态");
            Assert.That(row.OpenChoice, Is.Null, "二段态的墨字一个都不该出现");
            // 🔁 2026-11-05 字号轮:「为什么不能」改挂**页角注**(7b §11 的原定落点)——
            //    行字 24px 后整行超宽,这句话拼在行上会被硬裁在页框外(1600×900 实拍)。
            Assert.That(screen.NoticeText, Does.Contain("推进一次剧情后可启封"),
                "必须说明原因 —— 不能只是「点了没反应」(落点 = 页角注,不是行文字)");
            Assert.That(row.MarkText, Does.Not.Contain("推进一次剧情后可启封"),
                "行文字只留状态「· 已锁」;理由在页角注 —— 否则 24px 下行宽装不下");
            Assert.That(sink.SaveAts, Is.Empty,
                "**一旦误触就是毁档**:已锁行点一下不得产生任何落笔");
            Assert.That(sink.Loads, Is.Empty, "也不得触发载入(锁着,载入必被 7a 拒)");
        }

        // ══════════════════════════════════════════════════════════════════
        // ④ 重画 ⇒ 二段态作废
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_rebuild_disarmsPreviousRow()
        {
            SaveSlotsScreen screen = NewScreen(out _);
            screen.SetSlotViews(Mixed());
            screen.Activate(1);
            Assert.That(screen.ArmedRowIndex, Is.EqualTo(1));

            screen.SetSlotViews(Mixed());              // 装配层回推(写完一档后的重探)

            Assert.That(screen.ArmedRowIndex, Is.EqualTo(-1), "行重建 ⇒ 二段态作废");
            Assert.That(screen.RowAt(1).IsArmed, Is.False, "旧行的两枚墨字必须随之消失");
        }

        /// <summary>「留档」新行仍走 `TrySave()`(追加语义未变)。</summary>
        [Test]
        public void test_writableRow_stillUsesAppendSemantics()
        {
            SaveSlotsScreen screen = NewScreen(out RecordingSink sink);
            screen.SetSlotViews(Mixed());

            screen.ActivateWritable();

            Assert.That(sink.SaveNextCount, Is.EqualTo(1), "「留档」= 追加到台账下一号");
            Assert.That(sink.SaveAts, Is.Empty, "它不是「写指定槽」的入口");
        }
    }
}
