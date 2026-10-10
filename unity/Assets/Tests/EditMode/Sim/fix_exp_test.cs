// tests/unit/sim/fix_exp_test.cs
//
// 手写定点 Exp(M2 接线轮 · 阶段 2 · 批次 A)的 EditMode 夹具。
//
// 权威来源:
//   design/gdd/disease-simulation.md
//     · F0「⚠️ 定点 Exp 必须计入实现量 —— F1/F2 通篇依赖它」(Base / Relapse / Decay = e^(−τ/·))
//     · F1 病程求值 / F2 体征投影 —— 指数语义 = e^(−W/H)(W、H 为 Fix,指数经 Fix 除法得出)
//     · F0 误差预算行「待 src/ 实现后标定」—— 本文件即标定件
//   docs/architecture/adr-012-cross-platform-determinism-ci-gate.md —— 单元级黄金夹具先例
//   docs/architecture/adr-006-fixed-point-boundary-contract.md —— ROUND_HALF_AWAY_FROM_ZERO 单一舍入
//   docs/architecture/adr-026-skill-growth-fixed-point.md —— FixPow/FixSqrt 整数算法先例
//
// ⚠️ 黄金期望值(golden-fixexp-v1)由 **Fix.Exp 同算法的 Python 复刻**(ln2 规约 + 10 阶
//    Taylor + half-away,与 C# 零共享代码但**同构**)算出 —— 它防的是**实现漂移**
//    (C# 侧被改错即红),**不防算法级共模偏置**(实现与复刻同时错向同一偏置时夹具全绿;
//    实测 34 向量中 12 条与真值 e^x 有差,最大在域顶 ≈ 0.4 ulp,∈ ε 界内,属文档声明的
//    精度取舍)。**算法级共模防线 = 第 2 节分层 Math.Exp 神谕对拍(ε 界),不是黄金夹具**
//    (评审测-1 修正:原注「独立 Python 参考…任一条变红 = 实现漂移或参考漂移」过强)。
//    黄金向量任一条变红 = 实现漂移,**禁就地改期望值**(ADR-012 版本化刷新纪律:
//    刷新 = 新 golden-vN + 变更日志,不是改老数)。
// ⚠️ `Math.Exp` **仅在本测试文件内**作参照神谕(ADR-012 单元级对拍口径);
//    生产代码零浮点由 Sim 程序集门 B / b6 门扫描,不在本文件职责内。
// ⚠️ 本文件零随机、零墙钟 —— 网格与关键点全为字面量,逐次运行逐位一致。
//
// 误差界(与 Fix.Exp 头注同源):
//   ε = 4×2⁻¹⁶ 相对 + 3 LSB 绝对。
//   论证:① r 舍入 0.5 ulp + ② 级数 20 次舍入宽松计数 ≤ 16 ulp(粗界)+
//        ③ ln2 常量 2e-9 + ④ 截断 2.2e-13 + ⑤⑥ 出口移位(≥0 精确 / <0 ≤0.5 LSB);
//   实测:全域穷举 2,907,261 点(= 本域全部整数 raw)对 Math.Exp 神谕,误差三段口径
//         (与 Fix.cs 头注同源):值 ≥ 1.0 最大相对 3.12×2⁻¹⁶ · 小值区(oracle < 4096 LSB)
//         最大绝对 0.595 LSB · 中间带由绝对项 3 LSB 承担 ⇒ 断言取 4 / 3 留 ~25% 余量。

using System;
using System.Collections.Generic;
using NUnit.Framework;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Tests.Unit.Sim
{
    [TestFixture]
    internal sealed class FixExpTest
    {
        // ── 域常量(与 Fix.Exp 头注同源;此处独立写字面量,防止两端无谓耦合)──
        private const long XMaxRaw = 2135016L;      // ≈ floor(47·ln2·2^16) − 10
        private const long XZeroRaw = -772244L;     // = floor(−17·ln2·2^16)
        private const long EpsAbsLsb = 3L;          // 断言绝对界(LSB)
        private const double EpsRelUlp = 4.0;       // 断言相对界(× 2⁻¹⁶)

        // ════════ 1. 黄金夹具 golden-fixexp-v1(固定 Fix 输入 → 精确定点输出)════════

        // 期望值来源:独立 Python 参考实现(ln2 规约 + 10 阶 Taylor,逐字复刻 half-away 舍入)。
        [TestCase(-999999999L, 0L)]                  // 深负(域内)→ 0
        [TestCase(-6553600L, 0L)]                    // −100:e^−100·2^16 < 0.5 LSB
        [TestCase(-2000000L, 0L)]                    // −30.5
        [TestCase(-786432L, 0L)]                     // −12:e^−12·2^16 ≈ 0.402 → 0
        [TestCase(-772244L, 0L)]                     // 零点哨兵(恰在 0.5 LSB 下方)
        [TestCase(-772243L, 1L)]                     // 零点上一格(0.5 LSB 上方)→ 1
        [TestCase(-655360L, 3L)]                     // −10:e^−10·2^16 ≈ 2.975
        [TestCase(-163840L, 5380L)]                  // −2.5(GDD F1 典型衰减指数)
        [TestCase(-152917L, 6355L)]                  // ≈ −7/3(FromRational 形)
        [TestCase(-131072L, 8869L)]                  // −2
        [TestCase(-114000L, 11509L)]                 // −1.7395
        [TestCase(-81920L, 18777L)]                  // −1.25(FromRational −5/4)
        [TestCase(-65537L, 24109L)]                  // 略小于 −1
        [TestCase(-65536L, 24110L)]                  // −1:1/e·2^16
        [TestCase(-45426L, 32768L)]                  // ≈ −ln2 → 0.5(衰减半量语义)
        [TestCase(-32768L, 39750L)]                  // −0.5(GDD F1 典型衰减指数)
        [TestCase(-32767L, 39751L)]                  // 略大于 −0.5
        [TestCase(-6554L, 59299L)]                   // −0.1
        [TestCase(-3L, 65533L)]                      // −3 LSB(小负)
        [TestCase(-1L, 65535L)]                      // −1 LSB:1 − 1 LSB 级
        [TestCase(0L, 65536L)]                       // x = 0 → 恰 = 1(65536)
        [TestCase(1L, 65537L)]                       // +1 LSB
        [TestCase(3L, 65539L)]                       // +3 LSB
        [TestCase(6554L, 72429L)]                    // +0.1
        [TestCase(32767L, 108050L)]                  // 略小于 0.5
        [TestCase(32768L, 108052L)]                  // +0.5
        [TestCase(65536L, 178146L)]                  // +1:e·2^16
        [TestCase(65537L, 178148L)]                  // 略大于 +1
        [TestCase(114000L, 373200L)]                 // +1.7395
        [TestCase(278528L, 4594368L)]                // +4.25
        [TestCase(655360L, 1443528704L)]             // +10:e^10·2^16
        [TestCase(1048576L, 582362333184L)]          // +16
        [TestCase(2135015L, 9221823924482867200L)]   // 域顶 −1
        [TestCase(2135016L, 9221964661971222528L)]   // 域顶(实现上界)
        public void test_fixExp_goldenVector_matchesExactRaw(long inputRaw, long expectedRaw)
        {
            // Arrange
            var input = new Fix(inputRaw);

            // Act
            long actual = Fix.Exp(input).Raw;

            // Assert
            Assert.AreEqual(expectedRaw, actual,
                $"golden-fixexp-v1 漂移:Fix.Exp({inputRaw}) 应为 {expectedRaw},实得 {actual} —— " +
                "禁就地改期望值,须双向核对参考实现(ADR-012 刷新纪律)");
        }

        // ════════ 2. 误差对拍:Math.Exp 仅测试侧作神谕,全域分层采样 ════════

        [Test]
        public void test_fixExp_stratifiedDomainSample_withinEpsilonOfOracle()
        {
            // Arrange:确定性分层网格 —— 全域粗网 + 近零密网 + 极小邻域 + 两条边界带 + 关键点
            var points = new SortedSet<long>();

            for (long r = XZeroRaw; r <= XMaxRaw; r += 499) points.Add(r);   // 全域粗网 ≈5800 点
            for (long r = -400000; r <= 400000; r += 211) points.Add(r);    // 近零密网 ≈3800 点
            for (long r = -4000; r <= 4000; r += 7) points.Add(r);          // 零邻域 ≈1100 点
            for (long r = XZeroRaw; r <= XZeroRaw + 64; r++) points.Add(r); // 零点边界带(逐点)
            for (long r = XMaxRaw - 32; r <= XMaxRaw; r++) points.Add(r);   // 域顶边界带(逐点)
            foreach (long r in new long[]
            {
                0, 1, -1, 3, -3, 65536, -65536, 32768, -32768,
                -163840, -45426, -655360, 2135016, -772244, -772243,
            }) points.Add(r);

            double worstDiff = 0.0;
            long worstRaw = 0L;

            // Act + Assert:逐点断言 |FixExp(x) − e^x| ≤ 3 LSB + 4×2⁻¹⁶ × e^x
            foreach (long raw in points)
            {
                long actual = Fix.Exp(new Fix(raw)).Raw;
                double oracle = Math.Exp(raw / 65536.0) * 65536.0;   // 神谕:测试侧专用
                double diff = Math.Abs(actual - oracle);
                double bound = EpsAbsLsb + oracle * (EpsRelUlp / 65536.0);

                if (diff > worstDiff) { worstDiff = diff; worstRaw = raw; }

                Assert.LessOrEqual(diff, bound,
                    $"Fix.Exp 超出 ε 界:x_raw={raw} 实得={actual} 神谕={oracle:F3} " +
                    $"|diff|={diff:F3} LSB > bound={bound:F3} LSB(ε = {EpsAbsLsb} LSB + {EpsRelUlp}×2⁻¹⁶ 相对)");
            }

            Assert.Greater(points.Count, 10000, "前提:采样点数量足够(网格字面量被误删即红)");
            TestContext.Out.WriteLine($"[fixexp] 采样 {points.Count} 点,最差点 x_raw={worstRaw} diff={worstDiff:F3} LSB");
        }

        // ════════ 3. GDD F1/F2 语义面:衰减指数 e^(−W/H)(W、H 为 Fix,除法后喂 Exp)════════

        [TestCase(1L, 2L)]      // −W/H = −0.5
        [TestCase(1L, 1L)]      // −W/H = −1(半衰期一次)
        [TestCase(5L, 2L)]      // −W/H = −2.5
        [TestCase(1L, 1000L)]   // −W/H = −0.001(长半衰期慢衰减)
        [TestCase(30L, 7L)]     // −W/H ≈ −4.2857
        [TestCase(5000L, 13L)]  // −W/H ≈ −384.6(超长程:e^x 深归零)
        public void test_fixExp_gddF1DecayShape_withinEpsilonOfOracle(long w, long h)
        {
            // Arrange:F1 的 Decay(Δ) = e^(−Δ/half_life) 指数构造形 —— 求值侧只做 Fix 除法
            Fix exponent = Fix.FromRational(-w, h);   // −W/H,ROUND_HALF_AWAY_FROM_ZERO

            // Act
            long actual = Fix.Exp(exponent).Raw;

            // Assert:与 Math.Exp(−W/H) 神谕对拍(同一 ε 界)
            double oracle = Math.Exp(-(double)w / h) * 65536.0;
            double diff = Math.Abs(actual - oracle);
            double bound = EpsAbsLsb + oracle * (EpsRelUlp / 65536.0);
            Assert.LessOrEqual(diff, bound,
                $"F1 衰减指数 −{w}/{h}:实得={actual} 神谕={oracle:F3} |diff|={diff:F3} > {bound:F3}");
        }

        // ════════ 4. 定值语义:x = 0 恰 = 1;衰减指数不越 1;正指数不破 1 ════════

        [Test]
        public void test_fixExp_zeroInput_returnsExactlyOne()
        {
            Assert.AreEqual(Fix.OneRaw, Fix.Exp(Fix.Zero).Raw, "e^0 必须恰为 1(65536),非近似");
        }

        [Test]
        public void test_fixExp_negativeExponent_decayNeverExceedsOne()
        {
            // F1 用面:指数全 ≤ 0(Δ ≥ 0 门在求值侧),衰减因子 ∈ (0, 1]
            foreach (long raw in new long[] { -1, -7, -32768, -65536, -163840, -655360, -772243 })
            {
                long actual = Fix.Exp(new Fix(raw)).Raw;
                Assert.Greater(actual, 0L, $"衰减因子必须 > 0:x_raw={raw}");
                Assert.LessOrEqual(actual, Fix.OneRaw, $"衰减因子必须 ≤ 1:x_raw={raw} 实得 {actual}");
            }
        }

        // ════════ 5. 定义域与异常行为 ════════

        [TestCase(2135017L)]          // 域顶 + 1(数学上界 2135026 内但实现域外)
        [TestCase(2135026L)]          // 数学上界(floor(47·ln2·2^16))
        [TestCase(long.MaxValue)]     // Fix.PositiveInfinity 的 raw
        public void test_fixExp_beyondMaxRaw_throwsOverflow(long inputRaw)
        {
            Assert.Throws<OverflowException>(
                () => { var _ = Fix.Exp(new Fix(inputRaw)).Raw; },
                $"x_raw={inputRaw} 超可表示域应抛 OverflowException(与 FixMul「溢出即 bug」同口径)");
        }

        [Test]
        public void test_fixExp_extremeNegative_returnsZero()
        {
            // 长半衰期/大 Δ 的自然结局:e^x < 0.5 LSB ⇒ 舍入即 0(精确,非饱和近似)
            Assert.AreEqual(0L, Fix.Exp(new Fix(long.MinValue)).Raw, "long.MinValue 须落域检查归零而非抛错");
            Assert.AreEqual(0L, Fix.Exp(new Fix(-999999999L)).Raw);
            Assert.AreEqual(0L, Fix.Exp(new Fix(XZeroRaw)).Raw, "零点哨兵(0.5 LSB 下方)→ 0");
            Assert.AreEqual(1L, Fix.Exp(new Fix(XZeroRaw + 1L)).Raw, "零点上一格(0.5 LSB 上方)→ 1");
        }

        // ════════ 6. 确定性:纯函数,零随机零墙钟 ════════

        [Test]
        public void test_fixExp_repeatedEvaluation_identicalRaw()
        {
            // Arrange:固定输入序列(含域内全类型样本)
            long[] inputs = { 0, 1, -1, -32768, -65536, -163840, 65536, XMaxRaw, XZeroRaw + 1, 278528 };

            // Act + Assert:两次求值逐点逐位一致
            foreach (long raw in inputs)
            {
                long first = Fix.Exp(new Fix(raw)).Raw;
                long second = Fix.Exp(new Fix(raw)).Raw;
                Assert.AreEqual(first, second, $"Fix.Exp 非确定:x_raw={raw} 两次结果 {first} vs {second}");
            }
        }
    }
}
