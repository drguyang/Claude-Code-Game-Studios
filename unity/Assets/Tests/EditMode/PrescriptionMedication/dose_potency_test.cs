// 权威来源:GDD F-11.1(剂量定点化)· AC-11-11(零浮点/舍入/中间积)· AC-11-17(空 dose_range)
//          · ADR-006(ROUND_HALF_AWAY_FROM_ZERO)· ADR-012 F7(128 位中间积)
//
// 测试 DoseCalculator.Calculate / CalculateForDrug / ResolveEffectiveDose。
//
// NOT-RUN 声明(禁借绿;补做评审 2026-10-06 后重列):
// - AC-11-11④ int64 分支:21a 未声明 drug_potency 域(BL-7),无条件走 128 位路径;
//   **且**「禁 Int128 / BigInteger」原为无判据 ⇒ 已补扫描路(见 PrescriptionFloatScan)
// - AC-11-19:NOISE_BAND_9 在 9 侧不存在(BL-2)⇒ 比较器骨架 + 差值序列导出器**已交付**,
//   正式断言仍 NOT-RUN(门槛无主;本件不含任何门槛字面量)
// - AC-11-15:三格矩阵(ADR-012 未实跑)
// - TR-prescription-007:21a 半边
// - AC-11-09:派生器 + 契约已落(PrescriptionDerivedBaker + BindResult.SingleDoseMaxRaw);
//   消费侧(9 的 F1 clamp)与「items 纳入 ConfigVersion 哈希」归 story-001 / 9 的域

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
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
        public void test_dose_exactDivision_positive()
        {
            // ⚠️ m4(评审):原名 `halfAway_positive` 名实不符 —— 本例是**整除**,根本不触舍入。
            // drug_potency = 1.0 (raw 65536), dose = 1, DOSE_BASE = 2
            // 65536 × 1 / 2 = 32768.0 → 32768(整除,无舍入)
            var result = DoseCalculator.Calculate(FixFromRaw(65536L), 1, 2);
            Assert.AreEqual(32768L, result.DosePotency.Raw);
        }

        [Test]
        public void test_dose_rounding_positiveNonHalf_truncates()
        {
            // drug_potency = 1.0 (raw 65536), dose = 1, DOSE_BASE = 3
            // 65536 × 1 / 3 = 21845.333... → 21845(舍去,非半值)
            var result = DoseCalculator.Calculate(FixFromRaw(65536L), 1, 3);
            Assert.AreEqual(21845L, result.DosePotency.Raw);
        }

        [Test]
        public void test_dose_rounding_negativeNonHalf_symmetric()
        {
            // ⚠️ m4(评审):原名 `halfAway_negative` 名实不符 —— 本例是**非半值**,
            // 守的是「负值域同样向零侧舍去」,不守舍入模式本身(守模式的 = exactHalf_negative)。
            // drug_potency = -1.0 (raw -65536), dose = 1, DOSE_BASE = 3
            // -65536 × 1 / 3 = -21845.333... → -21845(非半值)
            var result = DoseCalculator.Calculate(FixFromRaw(-65536L), 1, 3);
            Assert.AreEqual(-21845L, result.DosePotency.Raw);
        }

        [Test]
        public void test_dose_negativeDose_negativeResult()
        {
            // m1(评审):原稿 23 测全部 `dose ≥ 0` ⇒ `neg = (raw<0) ^ (dose<0)` 的 **dose 半边
            // 从未被验证**(去掉 dose 符号项的实现存活 23/23)。
            // drug_potency = +1.0, dose = -1 ⇒ neg 应为 true ⇒ 结果取负。
            var result = DoseCalculator.Calculate(FixFromRaw(65536L), -1, 2);
            Assert.AreEqual(-32768L, result.DosePotency.Raw,
                "负 dose × 正 potency 应得负值(neg 的 dose 半边未被消费 ⇒ 符号丢失)");
        }

        [Test]
        public void test_dose_negativeDoseNegativePotency_positiveResult()
        {
            // m1 补:两负 ⇒ 正(XOR 的完整真值表)。
            var result = DoseCalculator.Calculate(FixFromRaw(-65536L), -1, 2);
            Assert.AreEqual(32768L, result.DosePotency.Raw, "负 × 负应得正(XOR 两半边同时消费)");
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
        public void test_dose_integerArithmetic_exactQuotient()
        {
            // ⚠️ m4(评审):原名 `noFloat_allInteger` 名实不符 —— 本测是纯算术断言,
            // 对「零浮点」零判别力(把 Calculate 换成 double 实现本测照样绿;真正守零浮点的是静态扫描)。
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

        [Test]
        public void test_dose_floatScan_hasPositiveControl()
        {
            // ⚠️ 评审 4:本测是**扫描器自身的正控** —— 没有它,若 `Scan` 被改成恒 `return new List()`,
            //    本卡与 story-003 的零浮点断言会**全部真空通过**。
            // 同时验证 M-2(禁 Int128/BigInteger)与 m6(行尾注释不误报)两处修复:
            // 夹具里**故意**含行尾注释形式的浮点字面量 ⇒ 应**不**被误报。
            string tmp = Path.Combine(Path.GetTempPath(), "dp_scan_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tmp);
            try
            {
                File.WriteAllText(Path.Combine(tmp, "mutant.cs"),
                    "using System.Numerics;\n" +
                    "class X {\n" +
                    "    double d = 1.5;              // 行尾注释里的 2.5 与 BigInteger 不该误报\n" +
                    "    BigInteger big;              // Int128 同理\n" +
                    "}\n");

                var hits = PrescriptionFloatScan.Scan(tmp);

                Assert.IsNotEmpty(hits, "扫描器对已知浮点/大整数片段返回空 ⇒ 扫描逻辑失效(真空绿)");
                Assert.IsTrue(hits.Exists(h => h.Contains("type")), $"未命中浮点类型路: {string.Join(" | ", hits)}");
                Assert.IsTrue(hits.Exists(h => h.Contains("literal")), $"未命中浮点字面量路: {string.Join(" | ", hits)}");
                // M-2:大整数替身须被独立成路(不进浮点正则 —— 原稿两者皆漏)
                Assert.IsTrue(hits.Exists(h => h.Contains("big-int type")),
                    $"未命中大整数替身路(ADR-005 AmG 禁 Int128/BigInteger): {string.Join(" | ", hits)}");
                // m6:行尾注释里的 2.5 是**文档**,不得计入违例(否则生产件注释一引述就误红)
                Assert.IsFalse(hits.Exists(h => h.Contains("'2.5'")),
                    $"行尾注释内容被误报为违例(m6 未修): {string.Join(" | ", hits)}");
            }
            finally
            {
                Directory.Delete(tmp, recursive: true);
            }
        }

        // ── AC-11-19:可感知地板比较器骨架(BL-2 ⇒ 判据 NOT-RUN,机器可跑)────────

        [Test]
        public void test_floor_differenceSequence_isMonotoneAndPositive()
        {
            // 机器半边:差值序列导出器对合成表可运行(判据开关 gated-off —— 门槛无主)。
            // 单调增的 F-11.1 ⇒ 相邻档差恒 > 0。
            var range = new DoseRange(1, 5);
            long[] deltas = PerceptibleFloorComparator.DifferenceSequence(
                FixFromRaw(65536L), range, 2);

            Assert.AreEqual(4, deltas.Length, "差值序列长度应 = hi − lo");
            foreach (long d in deltas)
                Assert.Greater(d, 0L, "单调 F-11.1 的相邻档差应恒 > 0");
        }

        [Test]
        public void test_floor_singleDetentRange_yieldsEmptySequence()
        {
            // 单档域(hi == lo)⇒ 无相邻对 ⇒ 空序列(与 AC-11-17 的整剂路径同形,不是 0 档)。
            var range = new DoseRange(3, 3);
            long[] deltas = PerceptibleFloorComparator.DifferenceSequence(
                FixFromRaw(65536L), range, 2);
            Assert.IsEmpty(deltas);
        }

        [Test]
        public void test_floor_comparator_detectsViolation()
        {
            // 谓词骨架对合成门槛可判 —— 证「机器可用」,**不**证 AC-11-19 本身
            //(门槛 NOISE_BAND_9 在 9 侧不存在,BL-2 ⇒ 正式对拍 NOT-RUN,禁借绿)。
            long[] deltas = { 100L, 200L, 300L };
            Assert.IsTrue(PerceptibleFloorComparator.SatisfiesFloor(deltas, 100L));
            Assert.IsFalse(PerceptibleFloorComparator.SatisfiesFloor(deltas, 101L),
                "低于门槛的项须被判不达标(否则比较器恒真)");
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
        public void test_dose_intermediateProduct128_overflowAtIntMaxDose()
        {
            // ⚠️ n1(评审):原名 `_2` 无描述性;注释曾写「dose = 2^31」而实参是 `int.MaxValue`(= 2^31 − 1)。
            // drug_potency = 2^47, dose = int.MaxValue, DOSE_BASE = 1
            // 中间积 ≈ 2^78,超出 Q16.16 域 ⇒ OverflowException
            long raw = 1L << 47;
            Assert.Throws<OverflowException>(() =>
                DoseCalculator.Calculate(FixFromRaw(raw), int.MaxValue, 1));
        }

        [Test]
        public void test_dose_quotientAtSignBound_throws()
        {
            // ⚠️ m2(评审):`SignBound` 守卫(`result > 2^63 || (== 2^63 && !neg)`)原稿
            // **零覆盖** —— 删掉整段守卫,原稿 23 测全部存活。本测锁定它。
            //
            // 夹具:raw = 2^40,dose = 2^23,DOSE_BASE = 1 ⇒ 积 = 2^63 **恰在界上**。
            //   qHi = 0(积 < 2^64)⇒ 前一道 qHi 守卫不拦;result == SignBound 且 !neg
            //   ⇒ 须抛(2^63 作为**正** long 不可表示;作为负值才合法)。
            Assert.Throws<OverflowException>(() =>
                DoseCalculator.Calculate(FixFromRaw(1L << 40), 1 << 23, 1),
                "商恰 = 2^63 且为正 ⇒ 超 int64 定点域,须抛而非静默回绕");
        }

        [Test]
        public void test_dose_quotientAtSignBoundNegative_isValid()
        {
            // m2 的对照半边:同一界值取负 ⇒ 合法(−2^63 是 long.MinValue,可表示)⇒ **不**抛。
            // 没有这条,把守卫改成「result >= SignBound 一律抛」也会绿 ⇒ 判据不完整。
            var result = DoseCalculator.Calculate(FixFromRaw(-(1L << 40)), 1 << 23, 1);
            Assert.AreEqual(long.MinValue, result.DosePotency.Raw,
                "−2^63 是合法 long.MinValue ⇒ 不得抛(守卫须区分符号)");
        }

        [Test]
        public void test_dose_mul128_lowWordCarry_isConsumed()
        {
            // ⚠️ M1(评审):`Mul128` 的 `loCarry` 位**可达但零覆盖** —— 把该行改成恒 0,
            // 原稿 23 测**全部存活**。本测用逐位复算过的夹具锁定它。
            //
            // 夹具:raw = 96279238802687,dose = 652768597,DOSE_BASE = 65536
            //   精确积 = 62848063633457952820139(> 2^64 ⇒ 高字非零)
            //   精确商 = 958985345969512219,rem = 35755 ⇒ rem×2 ≥ 65536 ⇒ 舍入进位 ⇒ 958985345969512220
            //   丢弃 loCarry 的变异体给出 958703870992801564(差 281474976710656 = 2^48)
            var result = DoseCalculator.Calculate(FixFromRaw(96279238802687L), 652768597, 65536);
            Assert.AreEqual(958985345969512220L, result.DosePotency.Raw,
                "loCarry 位未被消费 ⇒ 128 位积的低字进位丢失(高字少 1)");
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

        // ── AC-11-08:F5 唯一求值点(11 = 唯一;9 / 10 零消费)──────────────────

        [Test]
        public void test_f5_singleEvaluationPoint_noConsumersInSimOutsideTwoOwners()
        {
            // ⚠️ B2(评审):AC-11-08 的**负判据**——「9 与 10 程序集零 axis_offset 消费点」——
            //    原稿全库**不存在**。本测补上。
            //
            // 判据形状 = 扫描 `Sim/`(9 与 10 两个程序集都在其下)全部 *.cs,
            // 除**两个合法 F5 实现体**外,任何文件出现 `AxisOffsetByQuality` 即违例:
            //   ① Sim/Prescription/HalfLifeCalculator.cs   —— 11 侧的 F-11.2 求值点
            //   ② Sim/ItemDatabase/QualityTimelineSolver.cs —— 21a 侧的 F5 求解器
            // ⚠️ 判据是**反射式源码扫描**(承 AC-20-03 先例:判据 = 断言不是 grep 习惯,
            //    但此处无程序集反射入口 —— 两程序集无独立 asmdef,故退为源码面扫描并**加正控**)。
            string simDir = Path.Combine(RepoRoot, "unity", "Assets", "Sim");
            Assert.IsTrue(Directory.Exists(simDir), $"Sim/ 目录不存在: {simDir}");

            string[] files = Directory.GetFiles(simDir, "*.cs", SearchOption.AllDirectories);
            Assert.IsNotEmpty(files, "扫描面为 0 文件 ⇒ 断言真空通过");
            Assert.IsTrue(Array.Exists(files, f => Path.GetFileName(f) == "HostEmergencyProcessor.cs"),
                "HostEmergencyProcessor.cs(10 侧)不在扫描集内 ⇒ 断言面错位");
            Assert.IsTrue(Array.Exists(files, f => Path.GetFileName(f) == "QualityTimelineSolver.cs"),
                "QualityTimelineSolver.cs(21a 侧)不在扫描集内 ⇒ 断言面错位");

            var allowed = new HashSet<string>(StringComparer.Ordinal)
            {
                "HalfLifeCalculator.cs",     // 11 侧 F-11.2 求值点(唯一)
                "QualityTimelineSolver.cs",  // 21a 侧 F5 求解器(唯一)
            };

            var offenders = new List<string>();
            foreach (string file in files)
            {
                if (allowed.Contains(Path.GetFileName(file))) continue;
                string src = StripCommentsForScan(File.ReadAllText(file));
                if (src.Contains("AxisOffsetByQuality") || src.Contains("axis_offset_by_quality"))
                    offenders.Add(Path.GetFileName(file));
            }

            Assert.IsEmpty(offenders,
                $"AC-11-08 违反:9 / 10 程序集出现 axis_offset 消费点 ⇒ F5 求值点不唯一。违例文件:\n" +
                string.Join("\n", offenders));
        }

        [Test]
        public void test_f5_uniqueEvaluationPoint_positiveControl()
        {
            // ⚠️ 正控(同 story-004 的 QA M3 纪律):没有它,上面的扫描若被改成恒空
            //    会**真空通过**。此处对一个**已知含消费点**的文件跑同一判据,须命中。
            string[] knownConsumers =
            {
                Path.Combine(RepoRoot, "unity", "Assets", "Sim", "ItemDatabase", "QualityTimelineSolver.cs"),
                Path.Combine(RepoRoot, "unity", "Assets", "Sim", "Prescription", "HalfLifeCalculator.cs"),
            };
            foreach (string f in knownConsumers)
            {
                Assert.IsTrue(File.Exists(f), $"正控夹具缺失: {f}");
                string src = StripCommentsForScan(File.ReadAllText(f));
                Assert.IsTrue(src.Contains("AxisOffsetByQuality"),
                    $"正控失败:已知消费点 {Path.GetFileName(f)} 未被判据命中 ⇒ 扫描逻辑失效");
            }
        }

        [Test]
        public void test_f5_halfLifeCalculator_matchesTimelineSolver_halflifeAxis()
        {
            // ⚠️ B2 的另一半:两份 F-11.2 实现(11 侧 / 21a 侧)必须**同输入同输出**。
            //    公式重复本身是既有事实(两文件各自自陈「单一实现」);本测把「同解」变成可证伪判据 ——
            //    任一侧改了公式(如偏移索引改成 quality 而非 quality−1)⇒ 本测红。
            var offsets = new[] { FixFromRaw(32768L), FixFromRaw(-16384L), FixFromRaw(65536L) };
            var profile = new DrugProfile
            {
                HalfLife = FixFromRaw(131072L),
                QualityAxis = QualityAxis.HalfLife,
                AxisOffsetByQuality = offsets,
            };

            for (int q = 1; q <= offsets.Length; q++)
            {
                Fix fromPrescription = HalfLifeCalculator.CalculateForDrug(profile, q).AxisEffective;
                Fix fromTimeline = QualityTimelineSolver.ApplyQualityTimeline(profile, q).HalfLife.Value;
                Assert.AreEqual(fromTimeline.Raw, fromPrescription.Raw,
                    $"quality = {q}:11 侧与 21a 侧的 F-11.2 输出不等 ⇒ 公式重复实现已漂移");
            }
        }

        // ── D-21-22:品级不调制 drug_potency ─────────────────────────────────

        [Test]
        public void test_dose_qualityDoesNotModulatePotency()
        {
            // ⚠️ M2/M-2(评审):D-21-22 原稿**零测试**。
            // 判据:F-11.1 的入参**不含 quality** —— 同 (potency, dose, doseBase) 在任何品级下
            // 必得同值;品级只经 F5 进**时间轴**(见上一条对拍)。
            var baseline = DoseCalculator.Calculate(FixFromRaw(49152L), 3, 4).DosePotency.Raw;

            // 品级扫 1…MAX_QUALITY:构造带全部品级偏移的 profile,逐一经 CalculateForDrug 求值 ——
            // 结果必须**逐位等于**不含品级信息时的值(偏移表只喂 F5,不进 F-11.1)。
            var offsets = new[] { FixFromRaw(0L), FixFromRaw(65536L), FixFromRaw(-32768L), FixFromRaw(131072L) };
            for (int q = 1; q <= offsets.Length; q++)
            {
                var profile = new DrugProfile
                {
                    DrugPotency = FixFromRaw(49152L),
                    DoseRange = new DoseRange(1, 5),
                    HalfLife = FixFromRaw(65536L),
                    QualityAxis = QualityAxis.HalfLife,
                    AxisOffsetByQuality = offsets,
                };
                long got = DoseCalculator.CalculateForDrug(
                    profile.DrugPotency.Value, profile.DoseRange, 3, 4).DosePotency.Raw;
                Assert.AreEqual(baseline, got,
                    $"quality = {q} 时 dose_potency 变了 ⇒ 品级非法调制 drug_potency(D-21-22)");

                // 对照:同一 profile 的 F5 输出**确实随品级变**(证明上面的「不变」不是因为表没生效)
                Fix halfLife = HalfLifeCalculator.CalculateForDrug(profile, q).AxisEffective;
                Assert.AreEqual(65536L + offsets[q - 1].Raw, halfLife.Raw,
                    "F5 未随品级变化 ⇒ 对照失效(上一条的「不变」不可信)");
            }
        }

        private static string StripCommentsForScan(string source)
        {
            // 与 PrescriptionFloatScan 同口径:注释里的提及是文档,不是消费点。
            var sb = new System.Text.StringBuilder(source.Length);
            int i = 0, n = source.Length;
            while (i < n)
            {
                char c = source[i];
                if (c == '/' && i + 1 < n && source[i + 1] == '/')
                {
                    while (i < n && source[i] != '\n') { sb.Append(' '); i++; }
                    continue;
                }
                if (c == '/' && i + 1 < n && source[i + 1] == '*')
                {
                    sb.Append("  "); i += 2;
                    while (i < n && !(source[i] == '*' && i + 1 < n && source[i + 1] == '/'))
                    {
                        sb.Append(source[i] == '\n' ? '\n' : ' '); i++;
                    }
                    if (i < n) { sb.Append("  "); i += 2; }
                    continue;
                }
                sb.Append(c); i++;
            }
            return sb.ToString();
        }
    }
}
