// 存档册「转册」的夹具(2026-11-04)。
//
// 权威:`design/ux/save-slots-7b.md`
//   · §5.1 —— 每页 6 行(OQ-SB-2 外推),超出即续页;页码在封面区鱼尾;
//   · §5.2 —— 翻页热区 = 页缘(指针点页缘 / 手柄把焦点移到页缘行再 Submit);零按钮元件;
//   · §7.1 —— 翻页后页内焦点重置为该页首行;
//   · 「留档」新行 = 末行之后 ⇒ 跨页时只属于**末页**。
//
// ✅ **2026-11-04 补两条**(上一版按 NOT-RUN 记的两项已实现):
//    · 150% 缩放 ⇒ 页容量 6 → 4(`TextScale` / `PageCapacity`);
//    · 开册首焦点 = 高水位行**所在页**的首行(`HighWaterPage`)。
//    ⚠️ 缩放**数值归 49**:本文件断的是「屏消费这个输入」,不是「49 会不会给这个值」。

using System.Collections.Generic;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic.Screens;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace DaYiJingCheng.Tests.SkeuomorphicUI
{
    [TestFixture]
    public class SaveSlotsPagingTest
    {
        private sealed class RecordingSink : ISaveSlotCommandSink
        {
            public uint LastLoad;
            public int LoadCount;
            public bool TryLoad(uint slotSeq) { LastLoad = slotSeq; LoadCount++; return true; }
            public bool TrySave() => true;
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

        /// <summary>造 n 行「未绑定」视图,槽号从 1 起(与册上行的身份无关,只借它当序号)。</summary>
        private static IReadOnlyList<SaveSlotView> Views(int n)
        {
            var list = new List<SaveSlotView>(n);
            for (int i = 0; i < n; i++) list.Add(SaveSlotView.Unbound((uint)(i + 1)));
            return list;
        }

        [Test]
        public void test_paging_sixRowsPerPage_thenSpillToNextPage()
        {
            SaveSlotsScreen screen = NewScreen(out _);
            screen.SetSlotViews(Views(13));

            Assert.That(screen.PageCount, Is.EqualTo(3), "13 行 ÷ 每页 6 ⇒ 3 页");
            Assert.That(screen.CurrentPage, Is.EqualTo(0), "开册停在首页");
            Assert.That(screen.SlotRowCount, Is.EqualTo(6), "首页满 6 行");

            Assert.That(screen.NextPage(), Is.True);
            Assert.That(screen.SlotRowCount, Is.EqualTo(6), "第二页同样满 6 行");

            Assert.That(screen.NextPage(), Is.True);
            Assert.That(screen.SlotRowCount, Is.EqualTo(1), "末页只剩 1 行(13 − 12)");
        }

        [Test]
        public void test_paging_writableRowBelongsToLastPageOnly()
        {
            SaveSlotsScreen screen = NewScreen(out _);
            screen.SetSlotViews(Views(8));

            Assert.That(screen.WritableRow, Is.Null,
                "「留档」新行 = 末行之后 ⇒ 非末页**不得**出现(否则中途也能落笔,与册页序矛盾)");

            screen.NextPage();
            Assert.That(screen.WritableRow, Is.Not.Null, "末页 = 新行的位置");
        }

        [Test]
        public void test_paging_activateIsPageLocal()
        {
            SaveSlotsScreen screen = NewScreen(out RecordingSink sink);
            screen.SetSlotViews(Views(8));      // 槽 1..8 ⇒ 第二页是槽 7、8

            screen.NextPage();
            screen.Activate(0);

            Assert.That(sink.LastLoad, Is.EqualTo(7u),
                "行号是**页内**的:第二页第 0 行 = 全表第 6 行 = 槽 7。" +
                "不换算 ⇒ 按第二页会读到第一页的档,最坏的静默错读");
        }

        [Test]
        public void test_paging_edgesOnlyShownWhenTurnable()
        {
            SaveSlotsScreen screen = NewScreen(out _);
            screen.SetSlotViews(Views(8));

            Assert.That(screen.PreviousEdge.style.display.value, Is.EqualTo(DisplayStyle.None),
                "首页没有上一页 ⇒ 页缘不占纸面");
            Assert.That(screen.PreviousEdge.focusable, Is.False,
                "且不进焦点遍历 —— 焦点面由 `focusable` 表达,不靠 `display` 推断");
            Assert.That(screen.NextEdge.style.display.value, Is.EqualTo(DisplayStyle.Flex),
                "还有下一页 ⇒ 下一页的页缘在");
            Assert.That(screen.NextEdge.focusable, Is.True);

            screen.NextPage();

            Assert.That(screen.PreviousEdge.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(screen.PreviousEdge.focusable, Is.True);
            Assert.That(screen.NextEdge.style.display.value, Is.EqualTo(DisplayStyle.None),
                "末页没有下一页 ⇒ 页缘收起来");
            Assert.That(screen.NextEdge.focusable, Is.False);
        }

        [Test]
        public void test_paging_pageMarkShowsPositionAndTotal()
        {
            SaveSlotsScreen screen = NewScreen(out _);
            screen.SetSlotViews(Views(8));

            Assert.That(screen.PageMark.text, Is.EqualTo("1 / 2"));
            screen.NextPage();
            Assert.That(screen.PageMark.text, Is.EqualTo("2 / 2"));
        }

        [Test]
        public void test_paging_turningPastEitherEnd_isRefusedNotThrown()
        {
            SaveSlotsScreen screen = NewScreen(out _);
            screen.SetSlotViews(Views(7));

            Assert.That(screen.PreviousPage(), Is.False, "首页再往前 ⇒ false,不抛");
            Assert.That(screen.CurrentPage, Is.EqualTo(0));

            Assert.That(screen.NextPage(), Is.True);
            Assert.That(screen.NextPage(), Is.False, "末页再往后 ⇒ false,不抛");
            Assert.That(screen.CurrentPage, Is.EqualTo(1));
        }

        [Test]
        public void test_paging_emptyBook_stillHasOnePageAndWritableRow()
        {
            SaveSlotsScreen screen = NewScreen(out _);
            screen.SetSlotViews(new SaveSlotView[0]);

            Assert.That(screen.PageCount, Is.EqualTo(1), "空册也算一页");
            Assert.That(screen.SlotRowCount, Is.EqualTo(0));
            Assert.That(screen.WritableRow, Is.Not.Null,
                "空册的末页就是首页 ⇒ 新行必须在(否则玩家一开局无处落笔)");
        }

        [Test]
        public void test_paging_resettingViews_returnsToFirstPage()
        {
            SaveSlotsScreen screen = NewScreen(out _);
            screen.SetSlotViews(Views(13));
            screen.NextPage();
            Assert.That(screen.CurrentPage, Is.EqualTo(1));

            screen.SetSlotViews(Views(13));

            Assert.That(screen.CurrentPage, Is.EqualTo(0),
                "重取清单 ⇒ 回到首页(否则清单变短时页号可能越界)");
        }

        // ══════════════════════════════════════════════════════════════════
        // §5.1 文本缩放降档 · §7.1 开册首焦点(2026-11-04 补)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>造 n 行,前 <paramref name="saved"/> 行**有档**(其余空槽)。</summary>
        private static IReadOnlyList<SaveSlotView> ViewsWithSaves(int n, int saved)
        {
            var list = new List<SaveSlotView>(n);
            for (int i = 0; i < n; i++)
            {
                bool has = i < saved;
                list.Add(new SaveSlotView((uint)(i + 1), known: true, hasSave: has,
                                          tick: has ? 36000L * (i + 1) : 0L,
                                          dayNumber: has ? i + 1 : 0, isNight: false));
            }
            return list;
        }

        [Test]
        public void test_zoom100_capacityStaysSix()
        {
            SaveSlotsScreen screen = NewScreen(out _);
            screen.TextScale = 1f;
            screen.SetSlotViews(Views(13));

            Assert.That(screen.PageCapacity, Is.EqualTo(6), "100% 缩放 ⇒ 每页 6 行");
            Assert.That(screen.SlotRowCount, Is.EqualTo(6));
            Assert.That(screen.PageCount, Is.EqualTo(3), "13 行 / 每页 6 ⇒ 3 页");
        }

        [Test]
        public void test_zoom150_capacityDropsToFour()
        {
            SaveSlotsScreen screen = NewScreen(out _);
            screen.TextScale = 1.5f;
            screen.SetSlotViews(Views(13));

            Assert.That(screen.PageCapacity, Is.EqualTo(4),
                "§5.1:文本缩放 150% ⇒ 页容量 6 行降为 4 行");
            Assert.That(screen.SlotRowCount, Is.EqualTo(4), "本页只画 4 行");
            Assert.That(screen.PageCount, Is.EqualTo(4), "13 行 / 每页 4 ⇒ 4 页(溢出转册,不滚动)");
        }

        [Test]
        public void test_zoomBelowThreshold_capacityStaysSix()
        {
            // 负控:阈值是 150%,不是「只要不等于 1 就降档」
            SaveSlotsScreen screen = NewScreen(out _);
            screen.TextScale = 1.49f;
            screen.SetSlotViews(Views(13));

            Assert.That(screen.PageCapacity, Is.EqualTo(6), "149% 仍在 6 行档");
        }

        [Test]
        public void test_openBook_startsOnHighWaterPage()
        {
            // 13 行、前 8 行有档 ⇒ 高水位行 = 第 8 行(index 7)⇒ 每页 6 行时在第 2 页(index 1)
            SaveSlotsScreen screen = NewScreen(out _);
            screen.TextScale = 1f;
            screen.SetSlotViews(ViewsWithSaves(13, saved: 8));

            Assert.That(screen.CurrentPage, Is.EqualTo(1),
                "§7.1:开册首焦点 = 高水位行**所在页**,不是首页");
            Assert.That(screen.SlotRowCount, Is.EqualTo(6));
            Assert.That(screen.RowAt(0), Is.SameAs(screen.FirstFocusable),
                "装配方取 `FirstFocusable` ⇒ 即该页首行(焦点本身归装配层)");
        }

        [Test]
        public void test_openBook_highWaterOnLastPage_whenZoomed()
        {
            // 同一份清单,150% 下每页 4 行 ⇒ 第 8 行(index 7)落在第 2 页(index 1)
            SaveSlotsScreen screen = NewScreen(out _);
            screen.TextScale = 1.5f;
            screen.SetSlotViews(ViewsWithSaves(13, saved: 8));

            Assert.That(screen.PageCapacity, Is.EqualTo(4));
            Assert.That(screen.CurrentPage, Is.EqualTo(1),
                "降档后同一高水位行落在不同页 ⇒ 页码必须按**当前容量**算");
        }

        [Test]
        public void test_openBook_emptyBook_staysOnFirstPage()
        {
            // 正控:册里一个档都没有 ⇒ 不得跑到末页或越界页
            SaveSlotsScreen screen = NewScreen(out _);
            screen.SetSlotViews(ViewsWithSaves(13, saved: 0));

            Assert.That(screen.CurrentPage, Is.EqualTo(0), "空册 = 首页");
        }
    }
}
