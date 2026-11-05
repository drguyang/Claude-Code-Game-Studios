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
        public void test_rows_backupOnlyAndDamaged_areTextVisible_andDistinct()
        {
            // 🔁 2026-11-05(`TD-persist-22` 册子半边):两种**异态**必须在册子上看得见,
            //    且彼此可分 —— 改前它们与「未绑定」在纸面上同形(零字)。
            SaveSlotsScreen screen = NewScreen(out _);
            screen.SetSlotViews(new[]
            {
                // 只剩备份:探测报的是**备份那一份**的 tick/日子
                new SaveSlotView(1u, known: true, hasSave: true, tick: 1200L, dayNumber: 1L,
                                 fromBackup: true),
                // 已损:探到了、但两级都读不出
                new SaveSlotView(2u, known: true, hasSave: false, tick: 0L, damaged: true),
                // 对照:同一 tick 的**正常**档,不带尾缀
                new SaveSlotView(3u, known: true, hasSave: true, tick: 1200L, dayNumber: 1L),
            });

            Assert.That(screen.RowAt(0).MarkText, Is.EqualTo("壹 · 第壹日 昼 · 前版"),
                "只剩备份 ⇒ 行上写明,否则玩家不知道按下去读到的是旧的那一份");
            Assert.That(screen.RowAt(1).MarkText, Is.EqualTo("贰 · 此页已损"),
                "已损 ⇒ 有字(它不是空槽,也不是「我不知道」)");
            Assert.That(screen.RowAt(2).MarkText, Is.EqualTo("叁 · 第壹日 昼"),
                "对照:正常档不带任何尾缀 —— 否则尾缀没有判别力");

            // 墨色通道:已损行**不是**空行(空行 = 只有格线没有字)
            Assert.That(screen.RowAt(1).ClassListContains("empty-row"), Is.False,
                "已损行须走有档的墨色通道");
            Assert.That(screen.RowAt(1).ClassListContains("save-slot-item"), Is.True);
        }

        [Test]
        public void test_row_lockedSlot_showsLockSuffix()
        {
            // 🔁 2026-11-05(用户裁定甲案 · `smoke-2026-11-05.md` 的「没法读档」):
            //    读档即锁此前**只有行为面**(「启封」灰置)、**零文字面** ⇒ 玩家点了没反应,
            //    屏上什么都不说,只有日志里有半句。锁是**会自解**的(下一次叙事锚点),
            //    所以文案只陈述状态,不写「永不可读」。
            SaveSlotsScreen screen = NewScreen(out _);
            screen.SetSlotViews(new[]
            {
                new SaveSlotView(1u, known: true, hasSave: true, tick: 1200L, dayNumber: 1L,
                                 isLocked: true),
                new SaveSlotView(2u, known: true, hasSave: true, tick: 1200L, dayNumber: 1L),
            });

            Assert.That(screen.RowAt(0).MarkText, Is.EqualTo("壹 · 第壹日 昼 · 已锁"),
                "已锁 ⇒ 行上写明(否则「点了没反应」无法与「界面坏了」区分)");
            Assert.That(screen.RowAt(1).MarkText, Is.EqualTo("贰 · 第壹日 昼"),
                "对照:未锁不带尾缀 —— 否则尾缀没有判别力");
        }

        [Test]
        public void test_lockedRow_activate_doesNotEnterTwoStage_andExplainsWhy()
        {
            // 🔁 2026-11-05(用户实测:「点已有存档 → 选读取 → 没有重新进入游戏」)。
            //    真因更坏:已锁槽的「启封」灰置且退出焦点序 ⇒ 二段态焦点落到**「重写」**
            //    ⇒ 一次确认就**覆写**这个槽。⇒ 已锁行不再进二段态,改为行上说明。
            SaveSlotsScreen screen = NewScreen(out _);
            screen.SetSlotViews(new[]
            {
                new SaveSlotView(1u, known: true, hasSave: true, tick: 1200L, dayNumber: 1L,
                                 isLocked: true),
            });

            screen.Activate(0);

            Assert.That(screen.ArmedRowIndex, Is.EqualTo(-1),
                "已锁行**不得**进入二段态 —— 否则默认焦点在「重写」上,一次确认就覆写存档");
            // 🔁 2026-11-05:理由移出**行文字**、落到**页角注**(7b §11)——
            //    行字 24px 后行宽装不下这句(实测硬裁在页框外)。
            Assert.That(screen.NoticeText, Does.Contain("推进一次剧情后可启封"),
                "必须说明为什么点不动,不能只是「点了没反应」");
            Assert.That(screen.RowAt(0).MarkText, Does.Contain("已锁"),
                "行上仍须有**状态**字样(理由归页角注,各行其位)");
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
