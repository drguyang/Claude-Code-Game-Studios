// `TD-skeuo-19` 判据(逻辑层)—— 存档册的两条输入面:**滚轮**与**上下键到页边界**。
//
// 病因(2026-11-06 走查时查出,记在债册 TD-skeuo-19):
//   `save-slots-7b.md:256` 的交互表从订立起就写着「点页缘 / **滚轮**」与「**L/R 肩** → 页缘行」,
//   而实现里:`grep -rn "WheelEvent" unity/Assets/Gameplay.UI` **零命中**;
//   `PagePrev` / `PageNext` 两个动作在输入资产里存在、绑了 `leftShoulder` / `rightShoulder`,
//   却**全仓无生产消费者**。⇒ 手柄与鼠标玩家实际只有「焦点移到页缘 + Submit」一条路。
//
// 用户 2026-11-06 两轮裁定(本文件判的就是这两条):
//   ①「不用想的太复杂,键盘和手柄的上下键移动到本页最上和最下就能进行翻页」
//   ②「鼠标的滚轮也能移动焦点上下,也能进行翻页」
// ⇒ 肩键路线**不做了**(不需要 `PagePrev` / `PageNext` 的生产消费者,也不加装配层驱动);
//   滚轮与方向键走**同一条竖轴**:一格 = 一行,到本页最上 / 最下再走 = 翻页。
//
// 本文件断的是**判定逻辑**(页号变不变、消不消费):
//   ① 滚轮:中间行 ⇒ 只移焦不翻页;首行再上 / 末行再下 ⇒ 翻页;
//   ② 上下键(`HandleEdgeMove`):只有**页边界**那一行才拦,中间行返回 false 交给官方焦点桥;
//   ③ 页码到底的边界:没得翻 ⇒ false 且**不消费**(让事件继续走)。
//
// ⚠️ 本文件**不判**「焦点真的落在哪一行」—— 那要真面板(`Focus()` 在无面板的屏单元里是
//   空操作)。落点 / 真事件链归 PlayMode 的 `save_book_paging_focus_test` 与真包走查。

using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic.Screens;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace DaYiJingCheng.Tests.SkeuomorphicUI
{
    /// <summary>`TD-skeuo-19`:滚轮 / 上下键到页边界这两条翻页输入面(逻辑层)。</summary>
    public sealed class SaveBookPagingTest
    {
        /// <summary>
        /// 8 个槽位 ⇒ `PageSize` = 6 时正好两页(第一页 6 行,第二页 2 行)。
        /// <para>⚠️ 全画**空槽**(`hasSave: false`):开册落页 = **高水位行**所在页(§7.1),
        /// 有档的话会直接翻到末页,首页判据全部落空(本夹具第一版就这样红过)。</para>
        /// </summary>
        private static SaveSlotsScreen TwoPages()
        {
            var screen = new SaveSlotsScreen(new VisualElement(), new SkeuoElementLibrary());
            var views = new SaveSlotView[8];
            for (uint i = 0; i < views.Length; i++)
                views[i] = new SaveSlotView(i + 1u, known: true, hasSave: false, tick: 0L);
            screen.SetSlotViews(views);
            return screen;
        }

        /// <summary>恰好一页(3 行)⇒ 哪里都翻不动,用来判「没得翻 = 不消费」。</summary>
        private static SaveSlotsScreen SinglePage()
        {
            var screen = new SaveSlotsScreen(new VisualElement(), new SkeuoElementLibrary());
            var views = new SaveSlotView[3];
            for (uint i = 0; i < views.Length; i++)
                views[i] = new SaveSlotView(i + 1u, known: true, hasSave: false, tick: 0L);
            screen.SetSlotViews(views);
            return screen;
        }

        // ══════════════════════════════════════════════════════════════════
        // ① 滚轮:一格一行,到页边界翻页
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_wheel_midPage_movesFocusWithoutTurningPage()
        {
            SaveSlotsScreen book = TwoPages();
            Assert.That(book.PageCount, Is.EqualTo(2), "前置:两页");
            Assert.That(book.CurrentPage, Is.EqualTo(0));

            // 第 1 行往下滚 ⇒ 焦点落到第 2 行,页号**不得**动
            Assert.That(book.HandleWheel(-1f, 1), Is.True, "中间行滚动 = 消费(焦点动了)");
            Assert.That(book.CurrentPage, Is.EqualTo(0), "中间行不是页边界,不得翻页");

            Assert.That(book.HandleWheel(+1f, 1), Is.True);
            Assert.That(book.CurrentPage, Is.EqualTo(0));
        }

        [Test]
        public void test_wheel_atLastRow_turnsToNextPage_atFirstRow_turnsBack()
        {
            SaveSlotsScreen book = TwoPages();
            int lastRow = book.SlotRowCount - 1;
            Assert.That(book.SlotRowCount, Is.EqualTo(6), "前置:第一页 6 行");

            Assert.That(book.HandleWheel(-1f, lastRow), Is.True, "末行再向下 = 翻到后一页");
            Assert.That(book.CurrentPage, Is.EqualTo(1));

            Assert.That(book.HandleWheel(+1f, 0), Is.True, "首行再向上 = 翻回前一页");
            Assert.That(book.CurrentPage, Is.EqualTo(0));
        }

        [Test]
        public void test_wheel_atBoundary_isNoOp_andDoesNotConsume()
        {
            SaveSlotsScreen book = TwoPages();

            // 已在首页,首行再往上滚
            Assert.That(book.HandleWheel(+1f, 0), Is.False,
                "到页边界 ⇒ 返回 false(不消费 —— 让事件继续往上走)");
            Assert.That(book.CurrentPage, Is.EqualTo(0), "页号不得动");

            book.HandleWheel(-1f, book.SlotRowCount - 1);        // 翻到末页(2 行)
            Assert.That(book.CurrentPage, Is.EqualTo(1));
            Assert.That(book.HandleWheel(-1f, book.SlotRowCount - 1), Is.True,
                "末页末行再向下:底下还有「留档」新行 ⇒ 焦点落上去,算消费");
            Assert.That(book.CurrentPage, Is.EqualTo(1), "但页号不动(没有后一页了)");

            // 零位移:某些设备静止时也发事件
            Assert.That(book.HandleWheel(0f, 0), Is.False, "零位移不得算动作");
        }

        [Test]
        public void test_wheel_emptyPage_isNoOp()
        {
            var book = new SaveSlotsScreen(new VisualElement(), new SkeuoElementLibrary());
            book.SetSlotViews(null);
            Assert.That(book.SlotRowCount, Is.EqualTo(0), "前置:空册");

            Assert.That(book.HandleWheel(-1f, -1), Is.False, "零行 ⇒ 无可移动,不消费");
            Assert.That(book.HandleWheel(+1f, -1), Is.False);
            Assert.That(book.PageCount, Is.EqualTo(1), "空册也算一页");
        }

        [Test]
        public void test_wheel_focusOffRows_entersFromTheEndItCameFrom()
        {
            SaveSlotsScreen book = TwoPages();

            // −1 = 焦点不在本页行上(未入面板 / 落在页缘或墨字上)
            Assert.That(book.HandleWheel(-1f, -1), Is.True, "向下滚:从首行进入");
            Assert.That(book.CurrentPage, Is.EqualTo(0), "进入首行不等于翻页");

            Assert.That(book.HandleWheel(+1f, -1), Is.True, "向上滚:从末行进入");
            Assert.That(book.CurrentPage, Is.EqualTo(0), "落在末行,仍在本页");
        }

        // ══════════════════════════════════════════════════════════════════
        // ② 上下键:只有页边界那一行才拦
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_edgeMove_midRow_isNotIntercepted()
        {
            SaveSlotsScreen book = TwoPages();

            Assert.That(book.HandleEdgeMove(3, NavigationMoveEvent.Direction.Down), Is.False,
                "中间行 ⇒ 不拦(事件继续走官方焦点桥,行间移动归引擎)");
            Assert.That(book.CurrentPage, Is.EqualTo(0));

            Assert.That(book.HandleEdgeMove(3, NavigationMoveEvent.Direction.Up), Is.False);
            Assert.That(book.CurrentPage, Is.EqualTo(0));

            Assert.That(book.HandleEdgeMove(3, NavigationMoveEvent.Direction.Left), Is.False,
                "左右移不归翻页管");
        }

        [Test]
        public void test_edgeMove_atBottomRow_turnsNextPage_atTopRow_turnsBack()
        {
            SaveSlotsScreen book = TwoPages();
            int lastRow = book.SlotRowCount - 1;

            Assert.That(book.HandleEdgeMove(lastRow, NavigationMoveEvent.Direction.Down), Is.True,
                "本页最下一行再向下 = 翻页");
            Assert.That(book.CurrentPage, Is.EqualTo(1));

            Assert.That(book.HandleEdgeMove(0, NavigationMoveEvent.Direction.Up), Is.True,
                "本页最上一行再向上 = 翻回去");
            Assert.That(book.CurrentPage, Is.EqualTo(0));
        }

        [Test]
        public void test_edgeMove_withoutNeighbourPage_isNoOp()
        {
            SaveSlotsScreen book = SinglePage();
            Assert.That(book.PageCount, Is.EqualTo(1), "前置:只有一页");

            Assert.That(book.HandleEdgeMove(0, NavigationMoveEvent.Direction.Up), Is.False,
                "首页首行再向上:没有前一页 ⇒ 不拦(交给引擎,焦点自然不动)");
            Assert.That(book.HandleEdgeMove(book.SlotRowCount - 1,
                NavigationMoveEvent.Direction.Down), Is.False,
                "末页末行再向下:没有后一页 ⇒ 不拦 —— 焦点要能落到「留档」新行");
            Assert.That(book.CurrentPage, Is.EqualTo(0));
        }

        [Test]
        public void test_edgeMove_outOfRangeRow_isIgnored()
        {
            SaveSlotsScreen book = TwoPages();
            Assert.That(book.HandleEdgeMove(-1, NavigationMoveEvent.Direction.Down), Is.False);
            Assert.That(book.HandleEdgeMove(book.SlotRowCount, NavigationMoveEvent.Direction.Down),
                Is.False);
            Assert.That(book.CurrentPage, Is.EqualTo(0));
        }

        // ══════════════════════════════════════════════════════════════════
        // ④ 末页只剩「留档」一行(槽位行恰好填满前一页)——
        //    2026-11-06 用户实测抓到的缺陷:从这一行向上走**回不到前一页的末行**,
        //    因为旧实现只看槽位行(`_rows.Count == 0` ⇒ 直接 no-op / 不拦)。
        // ══════════════════════════════════════════════════════════════════

        /// <summary>6 条空槽(恰好填满一页)⇒ 「留档」被挤到第 2 页。</summary>
        private static SaveSlotView[] SixEmptyViews()
        {
            var views = new SaveSlotView[6];
            for (uint i = 0; i < views.Length; i++)
                views[i] = new SaveSlotView(i + 1u, known: true, hasSave: false, tick: 0L);
            return views;
        }

        private static SaveSlotsScreen WritableOnlyLastPage()
        {
            var screen = new SaveSlotsScreen(new VisualElement(), new SkeuoElementLibrary());
            screen.SetSlotViews(SixEmptyViews());
            return screen;
        }

        [Test]
        public void test_refreshAfterSave_followsWritableRowToItsPage()
        {
            SaveSlotsScreen book = WritableOnlyLastPage();
            Assert.That(book.PageCount, Is.EqualTo(2), "前置:6 槽 + 「留档」⇒ 两页");
            Assert.That(book.CurrentPage, Is.EqualTo(0), "开册口径(§7.1):高水位页 —— 无档 ⇒ 首页");

            // 开册口径不变(默认 false)
            book.SetSlotViews(SixEmptyViews());
            Assert.That(book.CurrentPage, Is.EqualTo(0), "开册不得自己跳到末页");

            // 写盘落定后的重画:跟到「留档」所在页
            book.SetSlotViews(SixEmptyViews(), followWritablePage: true);
            Assert.That(book.CurrentPage, Is.EqualTo(1),
                "写盘后必须跟过去 —— 否则玩家眼前那页没有可写的地方(用户实测缺陷)");
            Assert.That(book.SlotRowCount, Is.EqualTo(0), "该页只有「留档」一行");
            Assert.That(book.WritableRow, Is.Not.Null);
        }

        [Test]
        public void test_wheelUp_fromWritableOnlyPage_returnsToPreviousPage()
        {
            SaveSlotsScreen book = WritableOnlyLastPage();
            Assert.That(book.PageCount, Is.EqualTo(2), "前置:6 槽 + 「留档」⇒ 两页");
            Assert.That(book.NextPage(), Is.True);
            Assert.That(book.SlotRowCount, Is.EqualTo(0), "前置:末页没有槽位行");
            Assert.That(book.WritableRow, Is.Not.Null, "前置:末页只剩「留档」");

            Assert.That(book.HandleWheel(+1f, 0), Is.True, "「留档」向上滚必须能翻回前一页");
            Assert.That(book.CurrentPage, Is.EqualTo(0));

            // 反向:「留档」向下滚没有下一处了 ⇒ 不消费、页号不动
            book.NextPage();
            Assert.That(book.HandleWheel(-1f, 0), Is.False, "轴上已是末端");
            Assert.That(book.CurrentPage, Is.EqualTo(1));
        }

        [Test]
        public void test_writableEdgeMove_up_flipsBack_whenPageHasNoSlotRows()
        {
            SaveSlotsScreen book = WritableOnlyLastPage();
            book.NextPage();
            Assert.That(book.SlotRowCount, Is.EqualTo(0), "前置:本页没有槽位行");

            Assert.That(book.HandleWritableEdgeMove(NavigationMoveEvent.Direction.Up), Is.True,
                "「留档」上方的唯一去处就是前一页 ⇒ 必须由本屏翻页");
            Assert.That(book.CurrentPage, Is.EqualTo(0));
            Assert.That(book.HandleWritableEdgeMove(NavigationMoveEvent.Direction.Up), Is.False,
                "翻回的这一页有槽位行 ⇒ 向上归官方焦点桥,不由本屏翻页");
        }

        [Test]
        public void test_writableEdgeMove_up_isNotIntercepted_whenSlotRowsAbove()
        {
            SaveSlotsScreen book = TwoPages();     // 8 槽 ⇒ 末页 2 行 + 「留档」
            book.NextPage();
            Assert.That(book.SlotRowCount, Is.EqualTo(2), "前置:本页有槽位行");

            Assert.That(book.HandleWritableEdgeMove(NavigationMoveEvent.Direction.Up), Is.False,
                "上方还有槽位行 ⇒ 交给官方焦点桥,不得由本屏翻页");
            Assert.That(book.HandleWritableEdgeMove(NavigationMoveEvent.Direction.Down), Is.False,
                "向下不归翻页管");
            Assert.That(book.CurrentPage, Is.EqualTo(1));
        }

        // ══════════════════════════════════════════════════════════════════
        // ③ 页缘热区那条路**不受影响**(它是「我要翻页」的明确意图)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_edgeHotzone_stillTurnsPage_andStopsAtEnds()
        {
            SaveSlotsScreen book = TwoPages();

            Assert.That(book.PreviousPage(), Is.False, "首页再往前 = no-op");
            Assert.That(book.NextPage(), Is.True);
            Assert.That(book.CurrentPage, Is.EqualTo(1));
            Assert.That(book.NextPage(), Is.False, "末页再往后 = no-op");
            Assert.That(book.PreviousPage(), Is.True);
            Assert.That(book.CurrentPage, Is.EqualTo(0));
        }
    }
}
