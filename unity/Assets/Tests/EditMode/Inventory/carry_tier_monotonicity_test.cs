// 载重四档的分档函数判据(2026-11-10 · T22 批二 · `inventory-006` 的 Logic 半边)
//
// 权威:
//   `design/gdd/inventory-and-items.md` AC-20-10(禁数字 / 禁条)· AC-20-24(`CARRY_CAP` 唯一来源)
//   `design/ux/inventory-container-20.md` §6(四档 = `slack = cap − carry_load` 的单调不增函数,
//     **阈值归 42**)· BL-27 分工(20 出谓词、42 出分档)
//   `production/qa/qa-plan-sprint-26-2026-11-08.md` §S26-5(本文件的派生来源)
//
// ─────────────────────────────────────────────────────────────────────────────
// 本件钉什么(以及**没有**钉什么)
// ─────────────────────────────────────────────────────────────────────────────
//   ① 对 `slack` **单调不增**:载重往上走,档位只往「更满」走,绝不回退;
//   ② 边界值**字面写死**(不照抄实现里的 `ceil(cap/4)` / `ceil(cap/2)` ——
//      照抄就是 F3「自指判据」:变异常量时两边一起动,零判别力);
//   ③ **满档只由 20 的谓词给**:`isFull == false` 时,本屏**任何**载重都不得自报 `Full`
//      (BL-27 的分工判据;也是 F8「行为等价掩盖结构分叉」的守门);
//   ④ 非法输入(负载重 / 负容量)必须**抛**,不静默夹取。
//
// ⚠️ **本件不钉「档位只由 slack 决定」** —— QA 计划 §S26-5 曾写这一条,但实测现行实现
//   **不是** slack 单独的函数:阈值是**相对 cap** 的(`ceil(cap/4)` / `ceil(cap/2)`),
//   ⇒ 同一 `slack` 在不同 `cap` 下可以落到不同档。本件按**现行实现 + GDD 口径**
//   (「单调不增 + 满档具名谓词」)写判据,并把这条口径差异登记在证据件里,
//   **不**拿一条恒真的断言把计划稿的原话糊过去。

using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic.Screens;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace DaYiJingCheng.Tests.EditMode.Inventory
{
    [TestFixture]
    public sealed class carry_tier_monotonicity_test
    {
        // 夹具值(非 GDD 数值):12 = 4×3 一屏容量,取它的倍数便于手算阈值;
        // 3 = 小箱(踩过 `/4` 整除归零的那个坑)。
        private const int Cap = 12;
        private const int SmallCap = 3;

        private static CarryBand Band(long load, int cap, bool isFull)
            => InventoryContainerScreen.BandOf(load, cap, isFull);

        // ══════════ ① 单调不增(载重 ↑ ⇒ 档位只往更满走) ══════════

        [Test]
        public void test_band_isMonotonicNonIncreasing_inSlack()
        {
            int previous = -1;
            for (long load = 0; load <= Cap; load++)
            {
                int band = (int)Band(load, Cap, isFull: false);
                Assert.GreaterOrEqual(band, previous,
                    $"载重 {load} 的档位({(CarryBand)band})比上一格({(CarryBand)previous})更空 —— 回退了");
                previous = band;
            }

            // 正控:这一趟扫描**真的跨过**了至少两个档,否则「单调」是空真
            Assert.Greater(previous, (int)CarryBand.Empty,
                "扫描到头仍是 Empty ⇒ 上面那条单调断言没有判别力(夹具构造错了)");
        }

        // ══════════ ② 边界值字面写死(cap = 12) ══════════

        [Test]
        public void test_band_boundaries_withCapTwelve_areLiteral()
        {
            // §6 的整数式:`2 × load ≤ cap` ⇒ 半满,否则近满(手算,不引用实现的任何常量)
            Assert.AreEqual(CarryBand.Empty,    Band(0,  Cap, false), "空箱:箱底可见");
            Assert.AreEqual(CarryBand.Half,     Band(1,  Cap, false), "2×1 = 2 ≤ 12 ⇒ 半满(仍留空隙)");
            Assert.AreEqual(CarryBand.Half,     Band(6,  Cap, false), "2×6 = 12 ≤ 12 ⇒ 半满(恰在界上)");
            Assert.AreEqual(CarryBand.NearFull, Band(7,  Cap, false), "2×7 = 14 > 12 ⇒ 近满(挤到沿口)");
            Assert.AreEqual(CarryBand.NearFull, Band(11, Cap, false), "更满仍是近满");
            Assert.AreEqual(CarryBand.NearFull, Band(12, Cap, false),
                "载重到顶但谓词说没满 ⇒ **不得**自报 Full(见③)");
        }

        // ══════════ ③ 满档只由 20 的谓词给 ══════════

        [Test]
        public void test_band_full_comesOnlyFromPredicate()
        {
            Assert.AreEqual(CarryBand.Full, Band(Cap, Cap, isFull: true), "谓词说满 ⇒ Full");
            Assert.AreEqual(CarryBand.Full, Band(0, Cap, isFull: true),
                "谓词优先于「空箱」早退 —— 满档是 20 的结论,42 不重判");
            Assert.AreNotEqual(CarryBand.Full, Band(Cap, Cap, isFull: false),
                "谓词说没满 ⇒ 42 不得自己宣布 Full(BL-27 分工)");
        }

        // ══════════ ④ 小箱也能到近满(÷4 归零那个坑的回归) ══════════

        [Test]
        public void test_band_smallCap_reachesNearFull_notStuckAtHalf()
        {
            Assert.AreEqual(CarryBand.NearFull, Band(2, SmallCap, false),
                "cap=3 载 2:2×2 = 4 > 3 ⇒ 近满(任何「cap/4 整除」写法会得到 0 ⇒ 小箱永远判半满)");
            Assert.AreEqual(CarryBand.Half, Band(1, SmallCap, false),
                "cap=3 载 1:2×1 = 2 ≤ 3 ⇒ 半满");
        }

        // ══════════ ⑤ 未装配降级:cap = 0 恒 Empty ══════════

        [Test]
        public void test_band_capZero_isEmpty_degradationNotFull()
        {
            Assert.AreEqual(CarryBand.Empty, Band(0, 0, false), "容量真值缺席 ⇒ 空档(AC-20-13 禁默认容量)");
            Assert.AreEqual(CarryBand.Empty, Band(5, 0, false),
                "cap=0 而载重 5:slack 为负,仍归空档 —— 不得溢出成 Full/NearFull");
        }

        // ══════════ ⑤′ 四档在屏上**看得出区别**(AC-20-10 的几何腿) ══════════

        private static InventoryContainerScreen ScreenWith(long load, int cap, bool isFull)
        {
            var root = new VisualElement();
            var screen = new InventoryContainerScreen(root, new SkeuoElementLibrary());
            // 两件器物:让「格的竖直间隙」这条几何有对象可量
            var items = new[]
            {
                new ContainerItemDto(1L, itemOrdinal: 1, quality: 1, qty: 1),
                new ContainerItemDto(2L, itemOrdinal: 2, quality: 1, qty: 1),
            };
            screen.Bind(new InventoryContainerDto(1L, items, load, cap, isFull));
            return screen;
        }

        /// <summary>格上**真的被应用**的竖直间隙(读元素样式,不读缓存字段 —— 否则是自指判据)。</summary>
        private static float RenderedGap(InventoryContainerScreen screen)
            => screen.CellAt(0).style.marginTop.value.value;

        [Test]
        public void test_bandGeometry_fourBandsDifferInGeometry_notInColour()
        {
            var empty = ScreenWith(0, Cap, false);
            var half = ScreenWith(1, Cap, false);
            var near = ScreenWith(Cap, Cap, false);
            var full = ScreenWith(Cap, Cap, true);

            Assert.AreEqual(CarryBand.Empty, empty.Band);
            Assert.AreEqual(CarryBand.Half, half.Band);
            Assert.AreEqual(CarryBand.NearFull, near.Band);
            Assert.AreEqual(CarryBand.Full, full.Band);

            // 间隙单调不增 = 「越装越挤」的几何表达。**读渲染值**(元素样式),
            // 不读 `CellGapPx` —— 那个字段只证明「算过」,不证明「画上了」(F3 自指判据)。
            Assert.Greater(RenderedGap(empty), RenderedGap(half), "半满比空箱挤");
            Assert.Greater(RenderedGap(half), RenderedGap(near), "近满比半满挤");
            Assert.Greater(RenderedGap(near), RenderedGap(full), "满档高出沿口(间隙为负)");

            // 沿口 / 半盖只在对应档出现(不是四档都画一条)
            Assert.IsFalse(empty.IsRimVisible, "空箱没有沿口");
            Assert.IsFalse(half.IsRimVisible, "半满还没到沿口");
            Assert.IsTrue(near.IsRimVisible, "近满堆到沿口");
            Assert.IsFalse(near.IsLidVisible, "近满不挡盖");
            Assert.IsTrue(full.IsLidVisible, "满档半盖顶住(§6 的满档语汇)");
        }

        /// <summary>喂 <paramref name="count"/> 件器物(>12 才有第二页)。</summary>
        private static InventoryContainerScreen ScreenWithManyItems(int count, long load, int cap, bool isFull)
        {
            var root = new VisualElement();
            var screen = new InventoryContainerScreen(root, new SkeuoElementLibrary());
            var items = new ContainerItemDto[count];
            for (int i = 0; i < count; i++)
                items[i] = new ContainerItemDto(i + 1L, itemOrdinal: i + 1, quality: 1, qty: 1);
            screen.Bind(new InventoryContainerDto(1L, items, load, cap, isFull));
            return screen;
        }

        [Test]
        public void test_bandGeometry_survivesPageTurn()
        {
            // ⚠️ 必须**真的翻得动**:页数 = 1 时 `TurnPage` 早退,RebuildPage 根本不跑 ⇒
            //    这条会变成空洞正控(F4)。2026-11-10 证伪轮 M4 当场抓到过一次。
            var screen = ScreenWithManyItems(count: 13, load: Cap, cap: Cap, isFull: true);
            Assert.AreEqual(2, screen.PageCount, "前提:13 件 ⇒ 两页(否则下面的翻页是空动作)");
            float before = RenderedGap(screen);   // 翻页会**重造格** ⇒ 必须看新格上的值

            screen.TurnPage(+1);

            Assert.AreEqual(1, screen.Page, "前提:真的翻到了第二页");
            Assert.AreEqual(before, RenderedGap(screen), "翻页不得把档位几何丢掉");
            Assert.IsTrue(screen.IsLidVisible, "半盖在翻页后仍应在");
        }

        // ══════════ ⑥ 非法输入必须抛 ══════════

        [Test]
        public void test_band_negativeInputs_throwLoud()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => Band(-1, Cap, false),
                "负载重 = 投影或求和出错,不得静默夹到 0");
            Assert.Throws<System.ArgumentOutOfRangeException>(() => Band(0, -1, false),
                "负容量 = 契约被破坏,不得静默夹到 0");
        }
    }
}
