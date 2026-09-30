// Story 006 续: F-23-3 返还公式测试
//
// AC-23-08: 目录校验 cost ≥ ⌈1/R⌉
// AC-23-16: Refund = ⌊cost × R⌋ 整数向下取整

using NUnit.Framework;
using DaYiJingCheng.Sim.World;

namespace DaYiJingCheng.Tests.ModularBuilding
{
    public class RefundCalculatorTest
    {
        // P0 R = 1 (65536 Q16.16) => 全额返还
        [Test]
        public void test_refundRatioOne_returnsFullCost()
        {
            Assert.AreEqual(5, RefundCalculator.CalculateRefund(5, 65536));
            Assert.AreEqual(1, RefundCalculator.CalculateRefund(1, 65536));
        }

        // 整数向下取整
        [Test]
        public void test_refund_roundDown()
        {
            // R = 0.5 = 32768 Q16.16
            // ⌊3 × 0.5⌋ = ⌊1.5⌋ = 1
            Assert.AreEqual(1, RefundCalculator.CalculateRefund(3, 32768));

            // ⌊5 × 0.5⌋ = ⌊2.5⌋ = 2
            Assert.AreEqual(2, RefundCalculator.CalculateRefund(5, 32768));
        }

        // 零成本 => 零返还
        [Test]
        public void test_zeroCost_returnsZero()
        {
            Assert.AreEqual(0, RefundCalculator.CalculateRefund(0, 65536));
        }

        // AC-23-08: 目录校验
        [Test]
        public void test_validateRefundable_R1_alwaysPass()
        {
            // P0 R = 1 => ⌈1/1⌉ = 1, 任何 cost ≥ 1 通过
            Assert.IsTrue(RefundCalculator.ValidateRefundable(1, 65536));
            Assert.IsTrue(RefundCalculator.ValidateRefundable(100, 65536));
        }

        [Test]
        public void test_validateRefundable_RHalf_costThreshold()
        {
            // R = 0.5 = 32768 => ⌈1/0.5⌉ = ⌈2⌉ = 2
            Assert.IsFalse(RefundCalculator.ValidateRefundable(1, 32768));
            Assert.IsTrue(RefundCalculator.ValidateRefundable(2, 32768));
        }
    }
}
