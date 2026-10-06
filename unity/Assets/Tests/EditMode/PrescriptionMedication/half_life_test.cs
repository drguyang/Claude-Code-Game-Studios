// 权威来源:GDD F-11.2(半衰期 = F5 求值)· 规则六(11 在 F5 的求值点)
//          · ADR-006(Q16.16 整数域)· AC-11-08(唯一求值点 = 11)
//
// 测试 HalfLifeCalculator.Calculate / CalculateForDrug。
//
// NOT-RUN 声明(禁借绿):
// - AC-11-08 ②:21a 构建期断言 Axis_base + min(offset) ≥ MIN_USABLE_HALF_LIFE 不存在(BL-1)
// - AC-11-15:三格矩阵(ADR-012 未实跑)
// - TR-prescription-008:21a 半边

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
    public class HalfLifeCalculatorTest
    {
        // ── 辅助 ──────────────────────────────────────────────────────────

        private static Fix FixFromRaw(long raw) => new Fix(raw);

        private static Fix[] MakeOffsets(params long[] raws)
        {
            var result = new Fix[raws.Length];
            for (int i = 0; i < raws.Length; i++)
                result[i] = FixFromRaw(raws[i]);
            return result;
        }

        // ── F-11.2 基本求值 ──────────────────────────────────────────────

        [Test]
        public void test_halflife_quality1_usesFirstOffset()
        {
            // Axis_base = 1.0 (raw 65536), offsets = [+0.5, -0.25], quality = 1
            // Axis_effective = 65536 + 32768 = 98304
            var offsets = MakeOffsets(32768L, -16384L);
            var result = HalfLifeCalculator.Calculate(FixFromRaw(65536L), offsets, 1);
            Assert.AreEqual(98304L, result.AxisEffective.Raw);
            Assert.AreEqual(1, result.EffectiveQuality);
        }

        [Test]
        public void test_halflife_quality2_usesSecondOffset()
        {
            // Axis_base = 1.0 (raw 65536), offsets = [+0.5, -0.25], quality = 2
            // Axis_effective = 65536 + (-16384) = 49152
            var offsets = MakeOffsets(32768L, -16384L);
            var result = HalfLifeCalculator.Calculate(FixFromRaw(65536L), offsets, 2);
            Assert.AreEqual(49152L, result.AxisEffective.Raw);
            Assert.AreEqual(2, result.EffectiveQuality);
        }

        [Test]
        public void test_halflife_zeroOffset_returnsBase()
        {
            // Axis_base = 2.0 (raw 131072), offsets = [0, 0], quality = 1
            // Axis_effective = 131072 + 0 = 131072
            var offsets = MakeOffsets(0L, 0L);
            var result = HalfLifeCalculator.Calculate(FixFromRaw(131072L), offsets, 1);
            Assert.AreEqual(131072L, result.AxisEffective.Raw);
        }

        [Test]
        public void test_halflife_negativeOffset_reducesHalfLife()
        {
            // Axis_base = 1.0 (raw 65536), offsets = [-0.5, -0.5], quality = 1
            // Axis_effective = 65536 + (-32768) = 32768
            var offsets = MakeOffsets(-32768L, -32768L);
            var result = HalfLifeCalculator.Calculate(FixFromRaw(65536L), offsets, 1);
            Assert.AreEqual(32768L, result.AxisEffective.Raw);
        }

        [Test]
        public void test_halflife_positiveOffset_increasesHalfLife()
        {
            // Axis_base = 1.0 (raw 65536), offsets = [+1.0, +1.0], quality = 1
            // Axis_effective = 65536 + 65536 = 131072
            var offsets = MakeOffsets(65536L, 65536L);
            var result = HalfLifeCalculator.Calculate(FixFromRaw(65536L), offsets, 1);
            Assert.AreEqual(131072L, result.AxisEffective.Raw);
        }

        // ── 边界:quality 越界 ─────────────────────────────────────────────

        [Test]
        public void test_halflife_qualityZero_throws()
        {
            var offsets = MakeOffsets(32768L, -16384L);
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                HalfLifeCalculator.Calculate(FixFromRaw(65536L), offsets, 0));
        }

        [Test]
        public void test_halflife_qualityTooLarge_throws()
        {
            var offsets = MakeOffsets(32768L, -16384L);
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                HalfLifeCalculator.Calculate(FixFromRaw(65536L), offsets, 3));
        }

        [Test]
        public void test_halflife_emptyOffsets_throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                HalfLifeCalculator.Calculate(FixFromRaw(65536L), new Fix[0], 1));
        }

        [Test]
        public void test_halflife_nullOffsets_throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                HalfLifeCalculator.Calculate(FixFromRaw(65536L), null, 1));
        }

        // ── 边界:Axis_base = 0 ───────────────────────────────────────────

        [Test]
        public void test_halflife_zeroBase_returnsOffset()
        {
            // Axis_base = 0, offsets = [+0.5, -0.25], quality = 1
            // Axis_effective = 0 + 32768 = 32768
            var offsets = MakeOffsets(32768L, -16384L);
            var result = HalfLifeCalculator.Calculate(FixFromRaw(0L), offsets, 1);
            Assert.AreEqual(32768L, result.AxisEffective.Raw);
        }

        // ── 确定性:同输入同输出 ─────────────────────────────────────────────

        [Test]
        public void test_halflife_deterministic_sameInput()
        {
            var offsets = MakeOffsets(32768L, -16384L);
            var r1 = HalfLifeCalculator.Calculate(FixFromRaw(65536L), offsets, 1);
            var r2 = HalfLifeCalculator.Calculate(FixFromRaw(65536L), offsets, 1);
            Assert.AreEqual(r1.AxisEffective.Raw, r2.AxisEffective.Raw);
            Assert.AreEqual(r1.EffectiveQuality, r2.EffectiveQuality);
        }

        // ── 单调性:quality 越高,偏移越大(假设 offsets 单调递增)────────────

        [Test]
        public void test_halflife_monotonicIncreasing_quality()
        {
            // offsets = [0, +0.5, +1.0, +1.5], quality 1→4
            var offsets = MakeOffsets(0L, 32768L, 65536L, 98304L);
            var r1 = HalfLifeCalculator.Calculate(FixFromRaw(65536L), offsets, 1);
            var r2 = HalfLifeCalculator.Calculate(FixFromRaw(65536L), offsets, 2);
            var r3 = HalfLifeCalculator.Calculate(FixFromRaw(65536L), offsets, 3);
            var r4 = HalfLifeCalculator.Calculate(FixFromRaw(65536L), offsets, 4);
            Assert.Less(r1.AxisEffective.Raw, r2.AxisEffective.Raw);
            Assert.Less(r2.AxisEffective.Raw, r3.AxisEffective.Raw);
            Assert.Less(r3.AxisEffective.Raw, r4.AxisEffective.Raw);
        }

        // ── 零浮点:全整数域 ──────────────────────────────────────────────

        [Test]
        public void test_halflife_noFloat_allInteger()
        {
            // Axis_base = 3/4 (raw 49152), offsets = [+1/4, -1/4], quality = 1
            // Axis_effective = 49152 + 16384 = 65536
            var offsets = MakeOffsets(16384L, -16384L);
            var result = HalfLifeCalculator.Calculate(FixFromRaw(49152L), offsets, 1);
            Assert.AreEqual(65536L, result.AxisEffective.Raw);
        }

        private static string RepoRoot => ComputeRepoRoot();

        private static string ComputeRepoRoot([CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile) ?? ".",
                "..", "..", "..", "..", ".."));

        [Test]
        public void test_halflife_noFloat_staticScan()
        {
            // AC-11-11① 静态半:扫描 Sim/Prescription/ 源码,禁 float/double 类型与字面量
            // m-2(结构评审):本测**刻意**扫全目录而非仅 HalfLifeCalculator.cs —— AC-11-11①
            //   是全系统约束(11 的整个 sim 程序集零浮点),同目录 DoseCalculator 亦在约束内。
            //   若某文件将来引入浮点,本测**应当**失败(这是特性,非缺陷)。
            // 扫描实现共享于 PrescriptionFloatScan(与 story-002 同面,避免重复实现漂移)。
            string simDir = Path.Combine(RepoRoot, "unity", "Assets", "Sim", "Prescription");
            // M2(QA 评审):目录缺失 = 环境错误,须硬失败(原 Assert.Ignore 会静默跳过 ⇒ 借绿)
            Assert.IsTrue(Directory.Exists(simDir),
                $"Sim/Prescription/ 目录不存在(AC-11-11① 静态扫描无处可跑): {simDir}");

            var violations = PrescriptionFloatScan.Scan(simDir);

            Assert.IsEmpty(violations,
                $"Sim/Prescription/ 源码含浮点类型/字面量(AC-11-11① 零浮点):\n{string.Join("\n", violations)}");
        }

        // ── CalculateForDrug:读 DrugProfile(M-1 修正后为真实组合逻辑)─────────

        [Test]
        public void test_halflife_calculateForDrug_readsProfileFields()
        {
            var profile = new DrugProfile
            {
                HalfLife = FixFromRaw(65536L),
                AxisOffsetByQuality = MakeOffsets(32768L, -16384L),
            };
            var result = HalfLifeCalculator.CalculateForDrug(profile, 2);
            Assert.AreEqual(49152L, result.AxisEffective.Raw);
            Assert.AreEqual(2, result.EffectiveQuality);
        }

        [Test]
        public void test_halflife_calculateForDrug_nullHalfLife_throws()
        {
            var profile = new DrugProfile
            {
                HalfLife = null,
                AxisOffsetByQuality = MakeOffsets(32768L, -16384L),
            };
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                HalfLifeCalculator.CalculateForDrug(profile, 1));
        }

        [Test]
        public void test_halflife_calculateForDrug_nullOffsets_throws()
        {
            var profile = new DrugProfile
            {
                HalfLife = FixFromRaw(65536L),
                AxisOffsetByQuality = null,
            };
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                HalfLifeCalculator.CalculateForDrug(profile, 1));
        }

        // ── 大值:不溢出(int64 加法封闭)────────────────────────────────────

        [Test]
        public void test_halflife_largeValues_noOverflow()
        {
            // Axis_base = 2^40, offsets = [2^40, -2^40], quality = 1
            // Axis_effective = 2^40 + 2^40 = 2^41
            long large = 1L << 40;
            var offsets = MakeOffsets(large, -large);
            var result = HalfLifeCalculator.Calculate(FixFromRaw(large), offsets, 1);
            Assert.AreEqual(large * 2, result.AxisEffective.Raw);
        }

        // ── 负 Axis_base:合法(半衰期可为负?不,但加法封闭)────────────────

        [Test]
        public void test_halflife_negativeBase_valid()
        {
            // Axis_base = -1.0 (raw -65536), offsets = [+0.5, +0.5], quality = 1
            // Axis_effective = -65536 + 32768 = -32768
            var offsets = MakeOffsets(32768L, 32768L);
            var result = HalfLifeCalculator.Calculate(FixFromRaw(-65536L), offsets, 1);
            Assert.AreEqual(-32768L, result.AxisEffective.Raw);
        }

        // ── M1(QA 评审):int64 溢出行为 —— 显式抛,不依赖回绕(ADR-012 F7)──────

        [Test]
        public void test_halflife_positiveOverflow_throws()
        {
            // Axis_base = long.MaxValue, offset = +1 ⇒ 正向溢出
            // ADR-012 F7:IL2CPP 下有符号溢出为 UB ⇒ 须显式抛,不依赖回绕
            var offsets = MakeOffsets(1L, 1L);
            Assert.Throws<OverflowException>(() =>
                HalfLifeCalculator.Calculate(FixFromRaw(long.MaxValue), offsets, 1));
        }

        [Test]
        public void test_halflife_negativeOverflow_throws()
        {
            // Axis_base = long.MinValue, offset = -1 ⇒ 负向溢出
            var offsets = MakeOffsets(-1L, -1L);
            Assert.Throws<OverflowException>(() =>
                HalfLifeCalculator.Calculate(FixFromRaw(long.MinValue), offsets, 1));
        }

        [Test]
        public void test_halflife_noOverflowAtBoundary_valid()
        {
            // Axis_base = long.MaxValue - 1, offset = +1 ⇒ 恰达上界,合法
            var offsets = MakeOffsets(1L, 1L);
            var result = HalfLifeCalculator.Calculate(FixFromRaw(long.MaxValue - 1), offsets, 1);
            Assert.AreEqual(long.MaxValue, result.AxisEffective.Raw);
        }

        // ── m7(QA 评审):quality = offsets.Length = 上界合法值(显式)──────────

        [Test]
        public void test_halflife_qualityAtUpperBound_valid()
        {
            // offsets 长度 = 3,quality = 3 = 上界合法值(非越界)
            var offsets = MakeOffsets(0L, 16384L, 32768L);
            var result = HalfLifeCalculator.Calculate(FixFromRaw(65536L), offsets, 3);
            Assert.AreEqual(98304L, result.AxisEffective.Raw);
            Assert.AreEqual(3, result.EffectiveQuality);
        }

        // ── m8(QA 评审):单元素偏移表(最小合法输入)────────────────────────

        [Test]
        public void test_halflife_singleElementOffsets_valid()
        {
            // offsets 长度 = 1,quality = 1 ⇒ 唯一合法档
            var offsets = MakeOffsets(32768L);
            var result = HalfLifeCalculator.Calculate(FixFromRaw(65536L), offsets, 1);
            Assert.AreEqual(98304L, result.AxisEffective.Raw);
        }
    }
}
