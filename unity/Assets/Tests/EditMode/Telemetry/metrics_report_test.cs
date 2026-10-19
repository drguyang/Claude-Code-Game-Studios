// 权威来源:ADR-019 §五(导出 = 本地文件)· GDD R7(整数纪律:率 = (num,den) 对;标量才 Q16.16 + 显式舍入)·
//   E-2(分母 0 报「分母 0」,不报 0、不除零)· ADR-006 §五(ROUND_HALF_AWAY_FROM_ZERO 唯一舍入)。
//
// ⚠️ 本件是 TD-epic-01 的还债件(2026-10-19 第三片):ADR-019 §五 的「导出」半原无生产主语
//    (51 在 `unity/Assets/` 下零文件)。本件为 `Telemetry/MetricsReport.cs` 的夹具。

using System.Collections.Generic;
using DaYiJingCheng.Telemetry;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.Telemetry
{
    [TestFixture]
    internal sealed class MetricsReportTest
    {
        // ══════════════ 舍入:ROUND_HALF_AWAY_FROM_ZERO(ADR-006 唯一)══════════════

        /// <summary>半值偏离零(不是 ties-to-even)—— 这是 ADR-006 禁 `Math.Round` 默认的判据。</summary>
        [Test]
        public void test_roundHalfAway_halfGoesAwayFromZero()
        {
            Assert.That(MetricsReport.RoundHalfAwayFromZero(1, 2), Is.EqualTo(1), "0.5 ⇒ 1(远离零)");
            Assert.That(MetricsReport.RoundHalfAwayFromZero(-1, 2), Is.EqualTo(-1), "-0.5 ⇒ -1");
            Assert.That(MetricsReport.RoundHalfAwayFromZero(3, 2), Is.EqualTo(2), "1.5 ⇒ 2");
            Assert.That(MetricsReport.RoundHalfAwayFromZero(-3, 2), Is.EqualTo(-2), "-1.5 ⇒ -2");
            // ties-to-even 会把 0.5 判 0、2.5 判 2 —— 若实现退回 Math.Round,这两条红
            Assert.That(MetricsReport.RoundHalfAwayFromZero(5, 2), Is.EqualTo(3), "2.5 ⇒ 3(even 会判 2)");
        }

        /// <summary>分母 0 ⇒ 不除零,报标记(返回 0 由调用方走 DenominatorZero 分支)。</summary>
        [Test]
        public void test_roundHalfAway_denominatorZero_returnsZeroNotThrows()
        {
            Assert.That(MetricsReport.RoundHalfAwayFromZero(1, 0), Is.EqualTo(0), "分母 0 ⇒ 0,不抛");
            Assert.That(MetricsReport.RoundHalfAwayFromZero(0, 0), Is.EqualTo(0), "0/0 ⇒ 0,不抛");
        }

        // ══════════════ Q16.16 标量(显式舍入,非截断)══════════════

        /// <summary>(num << 16) / den 由显式舍入包住 —— 截断除法会把 0.5 判小一档。</summary>
        [Test]
        public void test_toQ1616_explicitRounding_notTruncation()
        {
            // 1/2 的 Q16.16 = 32768(0.5 × 65536)
            Assert.That(MetricsReport.ToQ1616(1, 2, out bool dz1), Is.EqualTo(32768));
            Assert.That(dz1, Is.False);
            // 1/3 = 21845.33… ⇒ 半远离零 ⇒ 21845
            Assert.That(MetricsReport.ToQ1616(1, 3, out bool dz2), Is.EqualTo(21845));
            Assert.That(dz2, Is.False);
            // 2/3 = 43690.67 ⇒ 43691(截断会判 43690)
            Assert.That(MetricsReport.ToQ1616(2, 3, out bool dz3), Is.EqualTo(43691),
                "2/3 须半远离零进位(截断除法会判 43690)");
            Assert.That(dz3, Is.False);
        }

        /// <summary>分母 0 ⇒ 标记 + 返 0,**不除零**(E-2)。</summary>
        [Test]
        public void test_toQ1616_denominatorZero_flagged()
        {
            long q = MetricsReport.ToQ1616(7, 0, out bool dz);
            Assert.That(dz, Is.True, "分母 0 须标记");
            Assert.That(q, Is.EqualTo(0), "分母 0 ⇒ 返 0,不除零");
        }

        /// <summary>负值:半远离零对负数同样有效(舍入模式不许因符号变卦)。</summary>
        [Test]
        public void test_toQ1616_negative_roundsAwayFromZero()
        {
            // -1/2 ⇒ -32768(恰整,无歧义)
            Assert.That(MetricsReport.ToQ1616(-1, 2, out bool dz), Is.EqualTo(-32768));
            Assert.That(dz, Is.False);
        }

        // ══════════════ 渲染:率 = (num,den) 对;分母 0 报「分母 0」══════════════

        /// <summary>率渲染成整数对,不折小数(R7:标量化只在显式调用时发生)。</summary>
        [Test]
        public void test_render_rateAsIntegerPair_notDecimal()
        {
            var lines = new List<MetricLine> { new MetricLine("F1a", 3, 4) };
            string text = MetricsReport.Render("t", lines);
            StringAssert.Contains("F1a = 3 / 4", text);
            Assert.That(text, Does.Not.Contain("0.75"), "不得自作主张折小数(R7)");
        }

        /// <summary>分母 0 ⇒ 渲染「分母 0」,**不渲染 0**(E-2 逐字:不报 0、不除零)。</summary>
        [Test]
        public void test_render_denominatorZero_saysSo_notZero()
        {
            var lines = new List<MetricLine> { new MetricLine("F1a", 0, 0) };
            string text = MetricsReport.Render("t", lines);
            StringAssert.Contains("F1a = 分母 0", text);
            Assert.That(text, Does.Not.Contain("F1a = 0 "), "分母 0 不得报成 0(E-2)");
        }

        /// <summary>空指标列表 ⇒ 显式「无指标」,不产出空文件冒充绿。</summary>
        [Test]
        public void test_render_emptySaysSo()
        {
            string text = MetricsReport.Render("t", new List<MetricLine>());
            StringAssert.Contains("无指标", text);
        }
    }
}
