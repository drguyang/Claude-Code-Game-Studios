// 权威来源:GDD F-11.1(剂量定点化)· AC-11-11(零浮点/舍入/中间积)· AC-11-17(空 dose_range)
//          · ADR-006(ROUND_HALF_AWAY_FROM_ZERO)· ADR-012 F7(128 位中间积)
//
// 测试 DoseCalculator.Calculate / CalculateForDrug / ResolveEffectiveDose。
//
// NOT-RUN 声明(禁借绿):
// - AC-11-11④ int64 分支:21a 未声明 drug_potency 域(BL-7),无条件走 128 位路径
// - AC-11-19:NOISE_BAND_9 在 9 侧不存在(BL-2)
// - AC-11-15:三格矩阵(ADR-012 未实跑)
// - TR-prescription-007:21a 半边

using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.PrescriptionMedication
{
    [TestFixture]
    public class DoseCalculatorTest
    {
        // ── 辅助 ──────────────────────────────────────────────────────────

        private static Fix FixFromRaw(long raw) => new Fix(raw);

        // ── AC-11-11 ②:舍入唯一 = ROUND_HALF_AWAY_FROM_ZERO ─────────────────

        [Test]
        public void test_dose_rounding_halfAway_positive()
        {
            // drug_potency = 1.0 (raw 65536), dose = 1, DOSE_BASE = 2
            // 65536 × 1 / 2 = 32768.0 → 32768(整除,无舍入)
            var result = DoseCalculator.Calculate(FixFromRaw(65536L), 1, 2);
            Assert.AreEqual(32768L, result.DosePotency.Raw);
        }

        [Test]
        public void test_dose_rounding_halfAway_nonHalf()
        {
            // drug_potency = 1.0 (raw 65536), dose = 1, DOSE_BASE = 3
            // 65536 × 1 / 3 = 21845.333... → 21845(舍去,非半值)
            var result = DoseCalculator.Calculate(FixFromRaw(65536L), 1, 3);
            Assert.AreEqual(21845L, result.DosePotency.Raw);
        }

        [Test]
        public void test_dose_rounding_halfAway_negative()
        {
            // drug_potency = -1.0 (raw -65536), dose = 1, DOSE_BASE = 3
            // -65536 × 1 / 3 = -21845.333... → -21845(远离零,非半值)
            var result = DoseCalculator.Calculate(FixFromRaw(-65536L), 1, 3);
            Assert.AreEqual(-21845L, result.DosePotency.Raw);
        }

        [Test]
        public void test_dose_rounding_halfAway_exactDivNegative()
        {
            // drug_potency = -1.0 (raw -65536), dose = 1, DOSE_BASE = 2
            // -65536 × 1 / 2 = -32768.0 → -32768(整除,无舍入)
            var result = DoseCalculator.Calculate(FixFromRaw(-65536L), 1, 2);
            Assert.AreEqual(-32768L, result.DosePotency.Raw);
        }

        // ── AC-11-11 ①:零浮点(全整数域)────────────────────────────────────

        [Test]
        public void test_dose_noFloat_allInteger()
        {
            // drug_potency = 3/4 (raw 49152), dose = 2, DOSE_BASE = 3
            // 49152 × 2 / 3 = 32768.0 → 32768
            var result = DoseCalculator.Calculate(FixFromRaw(49152L), 2, 3);
            Assert.AreEqual(32768L, result.DosePotency.Raw);
        }

        private static string RepoRoot => ComputeRepoRoot();

        private static string ComputeRepoRoot([CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile) ?? ".",
                "..", "..", "..", "..", ".."));

        [Test]
        public void test_dose_noFloat_staticScan()
        {
            // AC-11-11① 静态半:扫描 Sim/Prescription/ 源码,禁 float/double 类型与字面量
            // 扫描实现共享于 PrescriptionFloatScan(story-002 / story-003 同面,避免重复实现漂移)
            string simDir = Path.Combine(RepoRoot, "unity", "Assets", "Sim", "Prescription");
            // M2:目录缺失 = 环境错误,须硬失败(原 Assert.Ignore 会静默跳过 ⇒ 借绿)
            Assert.IsTrue(Directory.Exists(simDir),
                $"Sim/Prescription/ 目录不存在(AC-11-11① 静态扫描无处可跑): {simDir}");

            var violations = PrescriptionFloatScan.Scan(simDir);

            Assert.IsEmpty(violations,
                $"Sim/Prescription/ 源码含浮点类型/字面量(AC-11-11① 零浮点):\n{string.Join("\n", violations)}");
        }

        // ── AC-11-11 ③:舍入除须测 .5 例(半值落点)─────────────────────────

        [Test]
        public void test_dose_rounding_halfAway_exactHalf_positive()
        {
            // drug_potency = 0.5 (raw 32768), dose = 16385, DOSE_BASE = 65536
            // 32768 × 16385 / 65536 = 8192.5 → 8193(半值远离零)
            // 朴素向零截断得 8192,故此测可区分两种舍入模式
            var result = DoseCalculator.Calculate(FixFromRaw(32768L), 16385, 65536);
            Assert.AreEqual(8193L, result.DosePotency.Raw);
        }

        [Test]
        public void test_dose_rounding_halfAway_exactHalf_negative()
        {
            // drug_potency = -0.5 (raw -32768), dose = 16385, DOSE_BASE = 65536
            // -32768 × 16385 / 65536 = -8192.5 → -8193(半值远离零)
            var result = DoseCalculator.Calculate(FixFromRaw(-32768L), 16385, 65536);
            Assert.AreEqual(-8193L, result.DosePotency.Raw);
        }

        // ── AC-11-11 ④:中间积宽度(128 位)──────────────────────────────────

        [Test]
        public void test_dose_intermediateProduct128_overflow_throws()
        {
            // drug_potency = 2^47 - 1(Q16.16 最大合法值), dose = 2^31 - 1(int 最大值)
            // 中间积 = (2^47 - 1) × (2^31 - 1) ≈ 2^78,超出 Q16.16 域 ⇒ OverflowException
            long maxRaw = (1L << 47) - 1;
            int maxDose = int.MaxValue;
            Assert.Throws<OverflowException>(() =>
                DoseCalculator.Calculate(FixFromRaw(maxRaw), maxDose, 1));
        }

        [Test]
        public void test_dose_intermediateProduct128_overflow_throws_2()
        {
            // drug_potency = 2^47, dose = 2^31, DOSE_BASE = 1
            // 中间积 = 2^78,超出 Q16.16 域 ⇒ OverflowException
            long raw = 1L << 47;
            Assert.Throws<OverflowException>(() =>
                DoseCalculator.Calculate(FixFromRaw(raw), int.MaxValue, 1));
        }

        [Test]
        public void test_dose_intermediateProduct128_largeProductValidQuotient()
        {
            // drug_potency = 2^40, dose = 2^24, DOSE_BASE = 2^24
            // 中间积 = 2^64(hi ≠ 0),但商 = 2^40,完全落在 Q16.16 合法域
            // 此测验证溢出判据在"商"而非"积"上
            long raw = 1L << 40;
            int dose = 1 << 24;
            int doseBase = 1 << 24;
            var result = DoseCalculator.Calculate(FixFromRaw(raw), dose, doseBase);
            Assert.AreEqual(raw, result.DosePotency.Raw);  // 商 = 2^40 = raw
        }

        // ── AC-11-17:空 dose_range ⇒ 整剂给药 ─────────────────────────────

        [Test]
        public void test_dose_emptyRange_resolvesToOne()
        {
            int effective = DoseCalculator.ResolveEffectiveDose(null, 5);
            Assert.AreEqual(1, effective);
        }

        [Test]
        public void test_dose_emptyRange_potencyEqualsDrugPotency()
        {
            // AC-11-17:整剂给药时 dose_potency = drug_potency(旁路公式)
            var result = DoseCalculator.CalculateForDrug(FixFromRaw(49152L), null, 5, 999);
            Assert.AreEqual(49152L, result.DosePotency.Raw);
            Assert.AreEqual(1, result.EffectiveDose);
        }

        [Test]
        public void test_dose_emptyRange_potencyEqualsDrugPotency_negative()
        {
            // AC-11-17:负值同样旁路
            var result = DoseCalculator.CalculateForDrug(FixFromRaw(-49152L), null, 5, 999);
            Assert.AreEqual(-49152L, result.DosePotency.Raw);
            Assert.AreEqual(1, result.EffectiveDose);
        }

        [Test]
        public void test_dose_nonEmptyRange_usesSelected()
        {
            var range = new DoseRange(1, 5);
            int effective = DoseCalculator.ResolveEffectiveDose(range, 3);
            Assert.AreEqual(3, effective);
        }

        [Test]
        public void test_dose_nonEmptyRange_calculatesNormally()
        {
            // 非空域走正常 F-11.1 公式
            var range = new DoseRange(1, 5);
            var result = DoseCalculator.CalculateForDrug(FixFromRaw(65536L), range, 3, 2);
            // 65536 × 3 / 2 = 98304
            Assert.AreEqual(98304L, result.DosePotency.Raw);
            Assert.AreEqual(3, result.EffectiveDose);
        }

        // ── 边界:DOSE_BASE ≤ 0 ─────────────────────────────────────────────

        [Test]
        public void test_dose_doseBaseZero_throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                DoseCalculator.Calculate(FixFromRaw(65536L), 1, 0));
        }

        [Test]
        public void test_dose_doseBaseNegative_throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                DoseCalculator.Calculate(FixFromRaw(65536L), 1, -1));
        }

        // ── 边界:drug_potency = 0 ───────────────────────────────────────────

        [Test]
        public void test_dose_zeroPotency_returnsZero()
        {
            var result = DoseCalculator.Calculate(FixFromRaw(0L), 5, 10);
            Assert.AreEqual(0L, result.DosePotency.Raw);
        }

        // ── 边界:dose = 0 ───────────────────────────────────────────────────

        [Test]
        public void test_dose_zeroDose_returnsZero()
        {
            var result = DoseCalculator.Calculate(FixFromRaw(65536L), 0, 10);
            Assert.AreEqual(0L, result.DosePotency.Raw);
        }

        // ── 单调性:dose 越大,dose_potency 越大 ──────────────────────────────

        [Test]
        public void test_dose_monotonicIncreasing()
        {
            var r1 = DoseCalculator.Calculate(FixFromRaw(65536L), 1, 10);
            var r2 = DoseCalculator.Calculate(FixFromRaw(65536L), 2, 10);
            var r3 = DoseCalculator.Calculate(FixFromRaw(65536L), 3, 10);
            Assert.Less(r1.DosePotency.Raw, r2.DosePotency.Raw);
            Assert.Less(r2.DosePotency.Raw, r3.DosePotency.Raw);
        }

        // ── 确定性:同输入同输出 ─────────────────────────────────────────────

        [Test]
        public void test_dose_deterministic_sameInput()
        {
            var r1 = DoseCalculator.Calculate(FixFromRaw(49152L), 2, 3);
            var r2 = DoseCalculator.Calculate(FixFromRaw(49152L), 2, 3);
            Assert.AreEqual(r1.DosePotency.Raw, r2.DosePotency.Raw);
            Assert.AreEqual(r1.EffectiveDose, r2.EffectiveDose);
        }

        // ── EffectiveDose 透传 ──────────────────────────────────────────────

        [Test]
        public void test_dose_effectiveDose_passthrough()
        {
            var result = DoseCalculator.Calculate(FixFromRaw(65536L), 7, 10);
            Assert.AreEqual(7, result.EffectiveDose);
        }
    }
}
