// R1-34 / R1-35 —— GDD 校验 4b / 14 的注册表写入期下界(2026-10-22 立)。
//
// 权威来源:
//   GDD disease-simulation.md:337 —— 校验 4(急性保持型须 `A_peak/SCALE ≥ DEATH_THRESHOLD`)
//   GDD disease-simulation.md:344-347 —— 校验 14 三类下界(plateau∧lethal / 豁免类 / 急性保持型)
//   GDD disease-simulation.md:348 —— 校验 4b
//   `RegistrySchema.cs` 的 `R1_CHECK_22` 补注 —— 自陈「4b 全库零校验」的那条
//
// ── 立件缘由 ──────────────────────────────────────────────────────────────────
// `R1_CHECK_23` 校验的是 `Scale > 0 ∧ Scale ≥ A_peak` —— **完全不同的判据**。
// 此前全库没有任何东西校验「声明致死的病种能不能真打到死线」。
// 后果:注册表可以收进一条「lethal = true 但 A_peak/SCALE = 0.3」的行,
// 它**永远打不到 DEATH_THRESHOLD = 0.58** ⇒ 铁律一「不治就死」在该行上为假。
//
// ⚠️ 本文件与 `disease_registry_data_test` 的分工:
//   后者验**真数据**过校验;本文件验**校验器本身会拒**(用合成夹具构造违例)。

using System.Collections.Generic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.Sim
{
    /// <summary>R1-34(4b / 14a)与 R1-35(14b)的判据。</summary>
    [TestFixture]
    internal sealed class RegistryCurveFloorTest
    {
        // ══════════════════ 夹具 ══════════════════

        /// <summary>
        /// 造一条曲线合法的条目。`aPeakPercent` / `scalePercent` 以「整 100 单位」表达,
        /// 便于直接读比值 —— 例如 `aPeak=60, scale=100` ⇒ 比值 0.60。
        /// </summary>
        private static DiseaseRegistryEntry Entry(
            int diseaseId, string key, int lethality,
            bool selfLimit, bool plateau,
            int aPeak, int scale)
        {
            return new DiseaseRegistryEntry
            {
                DiseaseId = diseaseId,
                DiseaseKey = key,
                Kind = DiseaseKind.Disease,
                Polarity = 2,
                Severity = 2,
                Contagion = 0,
                Lethality = lethality,
                TreatmentDifficulty = 1,
                RecoveryTime = 100,
                Scale = new Fix(scale * Fix.OneRaw),
                Curve = new NaturalProgressCurve
                {
                    Incubation = 0,
                    APeak = new Fix(aPeak * Fix.OneRaw),
                    TauRise = new Fix(10 * Fix.OneRaw),
                    TauPeak = 100,
                    TauFall = new Fix(100 * Fix.OneRaw),
                    Plateau = plateau,
                    SelfLimit = selfLimit,
                    BoundaryMode = "monotone",
                },
            };
        }

        /// <summary>DEATH_THRESHOLD 换算成百分比(38011/65536 ≈ 58.00%)—— 用于夹具注释。</summary>
        private static readonly int DeathPercent = (int)(StateMachine.DEATH_THRESHOLD * 100L / Fix.OneRaw);
        private static readonly int RecoverPercent = (int)(StateMachine.RECOVER_THRESHOLD * 100L / Fix.OneRaw);
        private static readonly int CriticalPercent = (int)(StateMachine.CRITICAL_THRESHOLD * 100L / Fix.OneRaw);

        // ══════════════════ ① R1-34:急性保持型 ══════════════════

        /// <summary>
        /// 急性保持型 + `A_peak/SCALE &lt; DEATH` ⇒ **R1-34 红**。
        /// <para>这是 4b 的核心判据:「声明致死却永远打不到死线」= 无限迁延。</para>
        /// </summary>
        [Test]
        public void test_r1_34_acuteHold_belowDeathThreshold_throws()
        {
            // a_peak = 30, scale = 100 ⇒ 0.30 < 0.58
            var rows = new List<DiseaseRegistryEntry>
            {
                Entry(2, "DIS_ACUTE_LOW", lethality: 2, selfLimit: false, plateau: false,
                      aPeak: 30, scale: 100),
            };

            var ex = Assert.Throws<RegistryValidationException>(
                () => RegistrySchemaValidator.Validate(rows),
                $"急性保持型 A_peak/SCALE = 0.30 < DEATH ≈ {DeathPercent}% 必须被拒(GDD 校验 4b)");
            Assert.AreEqual(RegistrySchemaValidator.R1_CHECK_34, ex.RuleNumber);
        }

        /// <summary>
        /// 急性保持型 + `A_peak/SCALE` **恰等于** DEATH ⇒ 合法(判据是 ≥,不是 &gt;)。
        /// <para>防「一刀切成严格大于」的过宽修法。</para>
        /// </summary>
        [Test]
        public void test_r1_34_acuteHold_exactlyAtDeathThreshold_passes()
        {
            // 用 raw 精确构造:A_peak/Scale = DEATH/ONE ⇒ A_peak = Scale × DEATH / ONE。
            // 取 Scale = ONE(即 1.0),则 A_peak = DEATH raw ⇒ 比值恰等于阈值。
            var e = Entry(2, "DIS_ACUTE_EXACT", lethality: 2, selfLimit: false, plateau: false,
                          aPeak: 1, scale: 1);
            e.Curve.APeak = new Fix(StateMachine.DEATH_THRESHOLD);   // 比值恰 = DEATH
            e.Scale = new Fix(Fix.OneRaw);

            Assert.DoesNotThrow(() => RegistrySchemaValidator.Validate(
                    new List<DiseaseRegistryEntry> { e }),
                "A_peak/SCALE 恰等于 DEATH 是合法边界(判据为 ≥)");
        }

        /// <summary>急性保持型 + `lethal = false` ⇒ 先被 R1-22 拒(不是 R1-34)。</summary>
        [Test]
        public void test_acuteHold_nonLethal_isCaughtByR1_22_notR1_34()
        {
            var rows = new List<DiseaseRegistryEntry>
            {
                Entry(2, "DIS_ACUTE_NONLETHAL", lethality: 0, selfLimit: false, plateau: false,
                      aPeak: 30, scale: 100),
            };

            var ex = Assert.Throws<RegistryValidationException>(
                () => RegistrySchemaValidator.Validate(rows));
            Assert.AreEqual(RegistrySchemaValidator.R1_CHECK_22, ex.RuleNumber,
                "急性保持型须 lethal —— 这条比 4b 更基础,先红");
        }

        // ══════════════════ ② R1-34:plateau ∧ lethal(校验 14a) ══════════════════

        /// <summary>
        /// `plateau ∧ lethal` + `A_peak/SCALE &lt; DEATH` ⇒ **R1-34 红**。
        /// <para>GDD 校验 14:「否则 plateau 致死病永不可达死亡,与铁律二直接冲突」。</para>
        /// </summary>
        [Test]
        public void test_r1_34_plateauLethal_belowDeathThreshold_throws()
        {
            var rows = new List<DiseaseRegistryEntry>
            {
                Entry(2, "DIS_PLATEAU_LOW", lethality: 2, selfLimit: false, plateau: true,
                      aPeak: 40, scale: 100),   // 0.40 < 0.58
            };

            var ex = Assert.Throws<RegistryValidationException>(
                () => RegistrySchemaValidator.Validate(rows));
            Assert.AreEqual(RegistrySchemaValidator.R1_CHECK_34, ex.RuleNumber);
        }

        /// <summary>
        /// `plateau ∧ lethal` + `A_peak/SCALE = 0.667`(真表心衰 / 伤寒的值)⇒ 合法。
        /// <para>这条用**真表实际值**做正例,防「新门把真数据判红」。</para>
        /// </summary>
        [Test]
        public void test_r1_34_plateauLethal_atRealTableValue_passes()
        {
            // 真表 DIS_HEART_FAILURE:a_peak/scale ≈ 0.667。
            // 用精确比值 2/3:scale = 3×ONE,a_peak = 2×ONE ⇒ 0.6667 ≥ 0.58 ✓
            var e = Entry(9, "DIS_HEART_FAILURE", lethality: 2, selfLimit: false, plateau: true,
                          aPeak: 1, scale: 1);
            e.Scale = new Fix(3 * Fix.OneRaw);
            e.Curve.APeak = new Fix(2 * Fix.OneRaw);

            Assert.DoesNotThrow(() => RegistrySchemaValidator.Validate(
                    new List<DiseaseRegistryEntry> { e }));
        }

        // ══════════════════ ③ R1-35:豁免类(校验 14b) ══════════════════

        /// <summary>
        /// 豁免类(`plateau ∧ ¬lethal`)+ `A_peak/SCALE ≤ RECOVER` ⇒ **R1-35 红**。
        /// <para>下界缺 ⇒ 神经衰弱落地即成「痊愈」,「他还在病着」为假(R3.6)。</para>
        /// </summary>
        [Test]
        public void test_r1_35_exempt_belowOrAtRecover_throws()
        {
            // a_peak = 10, scale = 100 ⇒ 0.10 < 0.12(RECOVER)
            var rows = new List<DiseaseRegistryEntry>
            {
                Entry(14, "DIS_EXEMPT_LOW", lethality: 0, selfLimit: false, plateau: true,
                      aPeak: 10, scale: 100),
            };

            var ex = Assert.Throws<RegistryValidationException>(
                () => RegistrySchemaValidator.Validate(rows),
                $"豁免类 A_peak/SCALE = 0.10 ≤ RECOVER ≈ {RecoverPercent}% 必须被拒(GDD 校验 14)");
            Assert.AreEqual(RegistrySchemaValidator.R1_CHECK_35, ex.RuleNumber);
        }

        /// <summary>豁免类 + `A_peak/SCALE ≥ CRITICAL` ⇒ **R1-35 红**(上界)。</summary>
        [Test]
        public void test_r1_35_exempt_atOrAboveCritical_throws()
        {
            // a_peak = 40, scale = 100 ⇒ 0.40 > 0.30(CRITICAL)
            var rows = new List<DiseaseRegistryEntry>
            {
                Entry(14, "DIS_EXEMPT_HIGH", lethality: 0, selfLimit: false, plateau: true,
                      aPeak: 40, scale: 100),
            };

            var ex = Assert.Throws<RegistryValidationException>(
                () => RegistrySchemaValidator.Validate(rows),
                $"豁免类 A_peak/SCALE = 0.40 ≥ CRITICAL ≈ {CriticalPercent}% 必须被拒(空体征语义)");
            Assert.AreEqual(RegistrySchemaValidator.R1_CHECK_35, ex.RuleNumber);
        }

        /// <summary>
        /// 豁免类 + `A_peak/SCALE` **恰等于** RECOVER ⇒ **R1-35 红**(判据是严格大于)。
        /// <para>构造:`scale = RECOVER_raw`、`a_peak = ONE` ⇒ 比值恰 = `RECOVER/ONE`。</para>
        /// </summary>
        [Test]
        public void test_r1_35_exempt_exactlyAtRecover_throws()
        {
            var e = Entry(14, "DIS_EXEMPT_AT_RECOVER", lethality: 0, selfLimit: false, plateau: true,
                          aPeak: 1, scale: 1);
            // 构造「恰等于」:scale = ONE,a_peak = RECOVER raw ⇒ 比值恰 = RECOVER/ONE。
            e.Curve.APeak = new Fix(StateMachine.RECOVER_THRESHOLD);
            e.Scale = new Fix(Fix.OneRaw);

            var ex = Assert.Throws<RegistryValidationException>(
                () => RegistrySchemaValidator.Validate(new List<DiseaseRegistryEntry> { e }),
                "豁免类 A_peak/SCALE 恰等于 RECOVER 必须被拒(GDD 校验 14 为严格大于)");
            Assert.AreEqual(RegistrySchemaValidator.R1_CHECK_35, ex.RuleNumber);
        }

        /// <summary>
        /// 豁免类 + `A_peak/SCALE = 0.12`(略高于 `RECOVER = 7864/65536 ≈ 0.119995`)⇒ **合法**。
        ///
        /// <para><b>这条是 2026-10-22 实现轮自查修正的回归夹具</b>:原写法
        /// `AtLeast(RECOVER + 1)` 等价于 `a_peak × ONE ≥ (RECOVER+1) × scale`,
        /// 在 `scale = 100` 时要求 `a_peak ≥ 13` ⇒ **误拒**本行(GDD 判合法)。
        /// 改为 `StrictlyAbove(RECOVER)` 后本行通过。</para>
        /// </summary>
        [Test]
        public void test_r1_35_exempt_justAboveRecover_passes()
        {
            // a_peak = 12, scale = 100 ⇒ 0.12 > RECOVER ≈ 0.119995 ⇒ 合法(GDD 严格大于)
            var rows = new List<DiseaseRegistryEntry>
            {
                Entry(14, "DIS_EXEMPT_JUST_ABOVE", lethality: 0, selfLimit: false, plateau: true,
                      aPeak: 12, scale: 100),
            };

            Assert.DoesNotThrow(() => RegistrySchemaValidator.Validate(rows),
                "A_peak/SCALE = 0.12 略高于 RECOVER ≈ 0.119995 ⇒ GDD 判合法;" +
                "若本行转红,说明下界被写成了 `RECOVER + 1` 的近似式");
        }

        /// <summary>
        /// 豁免类 + `A_peak/SCALE = 0.15`(真表神经衰弱的值)⇒ 合法。
        /// </summary>
        [Test]
        public void test_r1_35_exempt_atRealTableValue_passes()
        {
            // 真表 DIS_NEURASTHENIA:a_peak 15 / scale 100 ⇒ 0.15 ∈ (0.12, 0.30)
            var rows = new List<DiseaseRegistryEntry>
            {
                Entry(14, "DIS_NEURASTHENIA", lethality: 0, selfLimit: false, plateau: true,
                      aPeak: 15, scale: 100),
            };

            Assert.DoesNotThrow(() => RegistrySchemaValidator.Validate(rows));
        }

        // ══════════════════ ④ 不适用型:自限型不受 4b 约束 ══════════════════

        /// <summary>
        /// 自限型 + `A_peak/SCALE = 0.30` ⇒ **不**被 R1-34 拒。
        /// <para>自限型关死亡支(F4 要求 `self_limit = false`),故它**不需要**打到死线。
        /// 这条防「新门判据过宽」—— 把自限型也要求 ≥DEATH 会让疟疾 / 麻疹全部非法。</para>
        /// </summary>
        [Test]
        public void test_r1_34_selfLimitType_notConstrained()
        {
            var rows = new List<DiseaseRegistryEntry>
            {
                Entry(7, "DIS_MALARIA_LIKE", lethality: 1, selfLimit: true, plateau: false,
                      aPeak: 45, scale: 100),   // 0.45 < 0.58,但自限型不需要
            };

            Assert.DoesNotThrow(() => RegistrySchemaValidator.Validate(rows),
                "自限型关死亡支 ⇒ 不受 4b 约束(判据不得过宽)");
        }

        // ══════════════════ ⑤ CompareProducts:128 位比较本身 ══════════════════

        /// <summary>
        /// `CompareProducts` 在**乘积溢出 int64** 时仍正确。
        /// <para>取 `2^40 × 2^40 = 2^80` vs `2^41 × 2^39 = 2^80` ⇒ 相等;
        /// 若用 int64 直接乘会溢出得到错误结果。</para>
        /// </summary>
        [Test]
        public void test_compareProducts_handlesInt64Overflow()
        {
            long a = 1L << 40, b = 1L << 40;   // 乘积 = 2^80(远超 int64)
            long c = 1L << 41, d = 1L << 39;   // 乘积 = 2^80(相同)

            Assert.AreEqual(0, RegistrySchemaValidator.CompareProducts(a, b, c, d),
                "2^40×2^40 与 2^41×2^39 均 = 2^80 ⇒ 相等(直接用 int64 乘会溢出)");

            Assert.AreEqual(-1, RegistrySchemaValidator.CompareProducts(a, b, c, d + 1));
            Assert.AreEqual(1, RegistrySchemaValidator.CompareProducts(c, d + 1, a, b));
        }

        /// <summary>小值比较与普通乘法一致(交叉验证 hi/lo 拆分)。</summary>
        [Test]
        public void test_compareProducts_matchesPlainMultiplication_forSmallValues()
        {
            var rng = new System.Random(20261022);   // 固定种子 ⇒ 确定性(测试标准要求)
            for (int i = 0; i < 200; i++)
            {
                long a = rng.Next(1, 100000), b = rng.Next(1, 100000);
                long c = rng.Next(1, 100000), d = rng.Next(1, 100000);

                long left = a * b, right = c * d;   // 小值不溢出
                int expected = left < right ? -1 : (left > right ? 1 : 0);

                Assert.AreEqual(expected, RegistrySchemaValidator.CompareProducts(a, b, c, d),
                    $"a={a} b={b} c={c} d={d}");
            }
        }

        /// <summary>负入参 ⇒ 抛(不静默按无符号读成巨大正数)。</summary>
        [Test]
        public void test_compareProducts_rejectsNegative()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => RegistrySchemaValidator.CompareProducts(-1, 1, 1, 1));
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => RegistrySchemaValidator.CompareProducts(1, -1, 1, 1));
        }
    }
}
