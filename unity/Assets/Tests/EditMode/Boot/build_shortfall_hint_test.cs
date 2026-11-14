// 材料不足反馈的**文案**夹具(N4 · 2026-11-14;P3-02a 逐件点名 · 2026-11-14)。
//
// 权威:`design/levels/prologue-beat-script.md` P3-02a
//   (「点名缺哪几件」:`还缺:门板、钉子。`)
//
// ⚠️ 本件钉**文案规则**:
//    · 缺口 > 0 才播、0 / 负数不播;
//    · **明细可用 ⇒ 点名**,明细缺失 / 有未知 key ⇒ 回退总数口径;
//    · 名单**去重**(同一件缺 2 份不该说两遍);
//    · `Qty ≤ 0` 的行不进名单(防御:计划里理论不会出现)。
//   「控制器算出的缺口对不对」归 `build_controller_test` 的
//   `test_shortfall_driverForwardsMissingByItem_andClearsOnSuccess`(生产面直测);
//   「提示在屏上停多久」归装配层,今日无自动化夹具 —— 如实登记,不借绿。

using System;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim.World;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Boot
{
    /// <summary>「还缺 …。」的唯一文案出口。</summary>
    [TestFixture]
    public class build_shortfall_hint_test
    {
        private static ModuleCostLine Line(int itemKey, int qty) => new ModuleCostLine(itemKey, qty);

        /// <summary>缺口为正、无明细 ⇒ 原样报数(旧口径不回归)。</summary>
        [Test]
        public void test_positiveGap_withoutItems_reportsNumber()
        {
            Assert.AreEqual("还缺 1 件。", BuildShortfallHint.Format(1));
            Assert.AreEqual("还缺 3 件。", BuildShortfallHint.Format(3));
            Assert.AreEqual("还缺 12 件。", BuildShortfallHint.Format(12));
        }

        /// <summary>缺口为 0 ⇒ **不播**(材料够的时候这一行不该出现)。</summary>
        [Test]
        public void test_zeroGap_isSilent()
        {
            Assert.IsNull(BuildShortfallHint.Format(0, new[] { Line(9, 1) }),
                "材料够时不得出现提示 —— 那是假反馈");
        }

        /// <summary>负数(理论上不该出现)⇒ 同样不播,不得渲染成「还缺 -2 件」。</summary>
        [Test]
        public void test_negativeGap_isSilent()
        {
            Assert.IsNull(BuildShortfallHint.Format(-2, new[] { Line(9, 1) }));
        }

        /// <summary>P3-02a 的原文例:缺门板与钉子 ⇒ 点名两件。</summary>
        [Test]
        public void test_namedItems_areListed()
        {
            Assert.AreEqual("还缺:门板、钉子。",
                BuildShortfallHint.Format(2, new[] { Line(9, 1), Line(10, 1) }));
        }

        /// <summary>帐篷五件:**顺序 = 明细顺序**,名字全出。</summary>
        [Test]
        public void test_fiveComponents_allNamed()
        {
            Assert.AreEqual("还缺:门板、钉子、房梁、布、油布。",
                BuildShortfallHint.Format(5, new[]
                {
                    Line(9, 1), Line(10, 1), Line(11, 1), Line(12, 1), Line(13, 1),
                }));
        }

        /// <summary>同一件缺 2 份 ⇒ 名字只说一遍(说两遍会读成「两种材料」)。</summary>
        [Test]
        public void test_duplicateNames_deduped()
        {
            Assert.AreEqual("还缺:门板。",
                BuildShortfallHint.Format(2, new[] { Line(9, 1), Line(9, 1) }));
        }

        /// <summary>有叫不出名字的 key ⇒ **整体回退**到总数 —— 不得把 `item_key=99` 播给玩家。</summary>
        [Test]
        public void test_unknownKey_fallsBackToTotal()
        {
            Assert.AreEqual("还缺 2 件。",
                BuildShortfallHint.Format(2, new[] { Line(9, 1), Line(99, 1) }));
        }

        /// <summary>`Qty ≤ 0` 的行不进名单(防御面)。</summary>
        [Test]
        public void test_zeroQtyLine_isSkipped()
        {
            Assert.AreEqual("还缺:门板。",
                BuildShortfallHint.Format(1, new[] { Line(9, 1), Line(10, 0) }));
        }

        /// <summary>空明细 / `null` 明细 ⇒ 总数口径。</summary>
        [Test]
        public void test_emptyOrNullItems_fallBackToTotal()
        {
            Assert.AreEqual("还缺 2 件。", BuildShortfallHint.Format(2, Array.Empty<ModuleCostLine>()));
            Assert.AreEqual("还缺 2 件。", BuildShortfallHint.Format(2, null));
        }

        /// <summary>名字表自身:五件全有名字;表外 key 返回 `null`(触发回退)。</summary>
        [Test]
        public void test_itemNameTable()
        {
            Assert.AreEqual("门板", BuildShortfallHint.ItemNameOf(9));
            Assert.AreEqual("钉子", BuildShortfallHint.ItemNameOf(10));
            Assert.AreEqual("房梁", BuildShortfallHint.ItemNameOf(11));
            Assert.AreEqual("布", BuildShortfallHint.ItemNameOf(12));
            Assert.AreEqual("油布", BuildShortfallHint.ItemNameOf(13));
            Assert.IsNull(BuildShortfallHint.ItemNameOf(0));
            Assert.IsNull(BuildShortfallHint.ItemNameOf(-1));
        }
    }
}
