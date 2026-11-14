// 材料不足反馈的**文案**夹具(N4 · 2026-11-14)。
//
// 权威:`design/levels/prologue-beat-script.md` P3-02a(「还缺 N 件。」)
//
// ⚠️ 本件只钉**文案规则**(缺口 > 0 才播、数字原样、复数不分)。
//    「控制器算出的缺口对不对」归 `BuildMaterialPlanner`(它已有自己的夹具);
//    「提示在屏上停多久」归装配层,今日无自动化夹具 —— 如实登记,不借绿。

using DaYiJingCheng.Gameplay.Boot;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Boot
{
    /// <summary>「还缺 N 件。」的唯一文案出口。</summary>
    [TestFixture]
    public class build_shortfall_hint_test
    {
        /// <summary>缺口为正 ⇒ 原样报数。</summary>
        [Test]
        public void test_positiveGap_reportsNumber()
        {
            Assert.AreEqual("还缺 1 件。", BuildShortfallHint.Format(1));
            Assert.AreEqual("还缺 3 件。", BuildShortfallHint.Format(3));
            Assert.AreEqual("还缺 12 件。", BuildShortfallHint.Format(12));
        }

        /// <summary>缺口为 0 ⇒ **不播**(材料够的时候这一行不该出现)。</summary>
        [Test]
        public void test_zeroGap_isSilent()
        {
            Assert.IsNull(BuildShortfallHint.Format(0), "材料够时不得出现提示 —— 那是假反馈");
        }

        /// <summary>负数(理论上不该出现)⇒ 同样不播,不得渲染成「还缺 -2 件」。</summary>
        [Test]
        public void test_negativeGap_isSilent()
        {
            Assert.IsNull(BuildShortfallHint.Format(-2));
        }
    }
}
