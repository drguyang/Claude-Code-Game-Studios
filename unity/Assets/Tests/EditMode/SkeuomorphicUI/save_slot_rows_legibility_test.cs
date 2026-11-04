// 存档册「行内文字」的夹具(2026-11-03 甲方案第一层)。
//
// 病因(用户 smoke 2026-11-03):「存档和读档看不出差异」——
//   实现侧此前是裸 `VisualElement`,一个字都没有 ⇒ 四条行长得一样。
//
// 本文件断的是**同一屏上三种行互不相同**:
//   有档行 → 行号(§5.4 字段一,汉字序)
//   空槽行 → 零字(`AC-42-B5`「可聚焦但零文本」)
//   未绑定行 → 零字(与空槽同形,语义不同)
//   「留档」新行 → 自带字样 + 独立 class
//
// ⚠️ **不**断「装配层把真状态探出来了」—— 那是 `save_slot_probe_test.cs` 与
//    `BootRoot.BuildSaveSlotViews` 的面,后者今日只有走查,不在这里假装已证。

using System.Collections.Generic;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic.Screens;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace DaYiJingCheng.Tests.SkeuomorphicUI
{
    [TestFixture]
    public class SaveSlotRowsLegibilityTest
    {
        private sealed class RecordingSink : ISaveSlotCommandSink
        {
            public uint LastLoad;
            public int LoadCount;
            public int SaveCount;
            public bool TryLoad(uint slotSeq) { LastLoad = slotSeq; LoadCount++; return true; }
            public bool TrySave() { SaveCount++; return true; }
            public bool TrySaveAt(uint slotSeq) { LastSaveAt = slotSeq; SaveAtCount++; return true; }
            public uint LastSaveAt;
            public int SaveAtCount;
        }

        private static SaveSlotsScreen NewScreen(out RecordingSink sink)
        {
            var screen = new SaveSlotsScreen(new VisualElement(), new SkeuoElementLibrary());
            sink = new RecordingSink();
            screen.BindCommandSink(sink);
            return screen;
        }

        private static IReadOnlyList<SaveSlotView> Mixed() => new[]
        {
            // tick 1200 ⇒ 第 1 日 · 昼;tick 99000 ⇒ 第 3 日 · 夜(相位 27000 = 夜起点)
            new SaveSlotView(1u, known: true, hasSave: true,  tick: 1200L,  dayNumber: 1L),
            new SaveSlotView(2u, known: true, hasSave: true,  tick: 99000L, dayNumber: 3L,
                             isNight: true),
            new SaveSlotView(3u, known: true, hasSave: false, tick: 0L),
            SaveSlotView.Unbound(4u),
        };

        [Test]
        public void test_rows_occupiedShowsNumeralDayAndTimeOfDay_emptyAndUnboundShowNothing()
        {
            SaveSlotsScreen screen = NewScreen(out _);
            screen.SetSlotViews(Mixed());

            Assert.That(screen.SlotRowCount, Is.EqualTo(4), "「留档」新行不计入槽位行数");
            Assert.That(screen.RowAt(0).MarkText, Is.EqualTo("壹 · 第壹日 昼"),
                "有档 ⇒ 行号 + 世界日 + 时段(第二层 + F-5.1)");
            Assert.That(screen.RowAt(1).MarkText, Is.EqualTo("贰 · 第叁日 夜"),
                "两条有档行必须因**日子与时段不同**而可分 —— 只写行号的话它们仍然一样");
            Assert.That(screen.RowAt(2).MarkText, Is.Empty, "空槽 ⇒ 零字(AC-42-B5)");
            Assert.That(screen.RowAt(3).MarkText, Is.Empty, "未绑定 ⇒ 零字(与空槽同形)");
        }

        [Test]
        public void test_rows_timeOfDay_omittedWhenDayUnknown()
        {
            // `DayNumber == 0` = 时间信息未就绪。此时 **不得** 写「昼」——
            // `IsNight` 的默认值是 false,那正好等于「昼」,写出来就是**静默的错误断言**。
            SaveSlotsScreen screen = NewScreen(out _);
            screen.SetSlotViews(new[]
            {
                new SaveSlotView(1u, known: true, hasSave: true, tick: 1200L),
            });

            Assert.That(screen.RowAt(0).MarkText, Is.EqualTo("壹"),
                "日子未知 ⇒ 时段也不画(不能拿默认值冒充「昼」)");
        }

        [Test]
        public void test_rows_writableRowIsTextDistinctFromSlots()
        {
            SaveSlotsScreen screen = NewScreen(out _);
            screen.SetSlotViews(Mixed());

            Assert.That(screen.WritableRow, Is.Not.Null);
            Assert.That(screen.WritableRow.MarkText, Is.EqualTo("留档"),
                "玩家要能看出这一行是「再写一行」,而不是第四个槽");
            Assert.That(screen.WritableRow.ClassListContains("save-slot-writable"), Is.True);
            Assert.That(screen.RowAt(0).ClassListContains("save-slot-writable"), Is.False,
                "槽位行**不得**带留档行的 class —— 两道形态通道要分开");
        }

        [Test]
        public void test_rows_occupiedAndEmptyDifferOnInkChannelToo()
        {
            SaveSlotsScreen screen = NewScreen(out _);
            screen.SetSlotViews(Mixed());

            // 文字通道之外的第二条通道:空行格线 vs 有档墨迹。
            // 只有一条通道时,低对比度设置下「有/无」会单独失效。
            Assert.That(screen.RowAt(0).ClassListContains("save-slot-item"), Is.True);
            Assert.That(screen.RowAt(0).ClassListContains("empty-row"), Is.False);

            Assert.That(screen.RowAt(2).ClassListContains("empty-row"), Is.True,
                "第 3 行(索引 2)是空槽");
            Assert.That(screen.RowAt(2).ClassListContains("save-slot-item"), Is.False);
        }

        [Test]
        public void test_rows_activateStillRoutesToSinkBySlotSeq()
        {
            SaveSlotsScreen screen = NewScreen(out RecordingSink sink);
            screen.SetSlotViews(Mixed());

            // 🔁 2026-11-04(ADR-041 §D1/§D5):行号 → 槽号的映射有三条出口,逐条断。
            //    ① 空槽行(= 槽 3,行序 2)⇒ 落笔到**它自己的槽号**
            screen.Activate(2);
            Assert.That(sink.SaveAtCount, Is.EqualTo(1));
            Assert.That(sink.LastSaveAt, Is.EqualTo(3u),
                "空槽行落笔必须写它自己的槽号(写台账高水位 = 按 3 号写到别处)");

            //    ② 未绑定行(= 槽 4,行序 3)⇒ 维持载入路径
            screen.Activate(3);
            Assert.That(sink.LoadCount, Is.EqualTo(1));
            Assert.That(sink.LastLoad, Is.EqualTo(4u),
                "行序号 → 槽号的映射不因行内文字改动而漂移");

            //    ③ 已有档行(= 槽 2,行序 1)⇒ **第一下不得动作**,只展开二段态
            screen.Activate(1);
            Assert.That(sink.LoadCount, Is.EqualTo(1), "已有档行第一次 Submit 不得载入");
            Assert.That(sink.SaveAtCount, Is.EqualTo(1), "也不得落笔");
            Assert.That(screen.ArmedRowIndex, Is.EqualTo(1), "而是展开二段态");
        }

        [Test]
        public void test_rows_unboundListViaSaveSlots_rendersNoText()
        {
            // 装配层只给槽号(未注入状态源)⇒ 全部未绑定 ⇒ 全体零字。
            // 这是「我不知道」的诚实形态,**不得**画成有档。
            SaveSlotsScreen screen = NewScreen(out _);
            screen.SetSlots(new List<uint> { 1u, 2u, 3u });

            Assert.That(screen.SlotRowCount, Is.EqualTo(3));
            for (int i = 0; i < 3; i++)
            {
                Assert.That(screen.RowAt(i).MarkText, Is.Empty,
                    "未绑定行不得凭空写出槽号 —— 那会让玩家以为这里有档");
                Assert.That(screen.RowAt(i).focusable, Is.True,
                    "零文本不等于不可聚焦(AC-42-B5:空位可聚焦但零文本)");
            }
        }

        [Test]
        public void test_rows_numeralMapping_coversTenAndFallsBack()
        {
            Assert.That(SaveSlotItem.Numeral(1u), Is.EqualTo("壹"));
            Assert.That(SaveSlotItem.Numeral(10u), Is.EqualTo("拾"));
            Assert.That(SaveSlotItem.Numeral(11u), Is.EqualTo("11"),
                "十位写法是本地化素材问题 ⇒ 超过拾退回十进制串,不擅自发明");
        }
    }
}
