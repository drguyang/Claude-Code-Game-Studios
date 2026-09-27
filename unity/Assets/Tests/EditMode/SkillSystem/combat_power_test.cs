// ============================================================================
// 战斗效能 EditMode 单测 —— Story 004 验收
// 权威来源: production/epics/skill-system/story-004-combat-power-and-medical-mod.md
//   · AC-1 ~ AC-6(CombatPower 定点求值 / 医术修正上限 / P0 武器线 / Fix 类型契约 /
//     昏迷阈值 / WeaponMultiplier 可配置)
//   · TR-skill-003(TR-registry.yaml) · TR-skill-004 · TR-skill-007
//   · ADR-026 §Decision 四(调参表 Fix 字段) + Implementation Guidelines
//   · ADR-006 §Decision 三(ROUND_HALF_AWAY_FROM_ZERO)
// ============================================================================
// 落点:unity/Assets/Tests/EditMode/SkillSystem/combat_power_test.cs
//   按 ADR-025 §⑤(2026-09-23 路径订正注)Unity 只编译 Assets/ 树,真身落此。
// ============================================================================
// 测试策略:全部用 Raw 值断言 Fix 相等(避免 ToFloat 浮点中转)。
// 零随机种子、零时间依赖、零外部 I/O。
// ============================================================================

using System;
using NUnit.Framework;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.Contracts.SkillSystem;

namespace DaYiJingCheng.Tests.Unit.SkillSystem
{
    /// <summary>Story 004 战斗效能与医术修正的 EditMode 单测。</summary>
    [TestFixture]
    internal sealed class CombatPowerTest
    {
        private const int SKILL_CAP = SkillRegistry.SKILL_CAP; // 60

        // ═══════════════════════════════════════════════════════════════════
        // AC-1: CombatPower 定点求值正确(徒手)
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>AC-1 主例: 徒手=60, 急救=60, WeaponMultiplier=0.9
        /// → 60 × 0.9 × (1 + 0.20) = 60 × 0.9 × 1.2 = Fix(64.8)。</summary>
        [Test]
        public void test_ac1_combatPower_徒手60_急救60_returns6480()
        {
            Fix result = CombatPower.Compute(
                combatSkillLevel: 60,
                medicalSkillLevel: 60,
                weaponLine: WeaponLine.徒手,
                tuning: SkillTuningTable.Default);

            // 60 × 0.9 × 1.20 经 Q16.16 链式乘法(每步舍入):
            // 60*0.9=54.0 raw=3538944; 3538944*1.2 的 raw=4246693
            Assert.That(result.Raw, Is.EqualTo(4246693L),
                "CombatPower(60徒手, 60急救) = 60 × 0.9 × 1.20 = 64.8(Q16.16 链式舍入)");
        }

        /// <summary>AC-1 边缘: 徒手=0, 急救=0 → CombatPower = 0。</summary>
        [Test]
        public void test_ac1_combatPower_skillZero_returnsZero()
        {
            Fix result = CombatPower.Compute(
                combatSkillLevel: 0,
                medicalSkillLevel: 0,
                weaponLine: WeaponLine.徒手,
                tuning: SkillTuningTable.Default);

            Assert.That(result.Raw, Is.EqualTo(0L),
                "CombatPower(0徒手, 0急救) = 0");
        }

        // ═══════════════════════════════════════════════════════════════════
        // AC-2: 医术修正硬上限 +20%
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>AC-2 主例: 徒手=1, 急救=60 → 医术修正 = 0.20(上限);
        /// CombatPower = 1 × 0.9 × 1.20 = 1.08。</summary>
        [Test]
        public void test_ac2_medicalModifier_cap_returns020()
        {
            Fix result = CombatPower.Compute(
                combatSkillLevel: 1,
                medicalSkillLevel: 60,
                weaponLine: WeaponLine.徒手,
                tuning: SkillTuningTable.Default);

            // 1 × 0.9 × 1.20 经 Q16.16 链式乘法(每步舍入) raw = 70778
            Assert.That(result.Raw, Is.EqualTo(70778L),
                "CombatPower(1徒手, 60急救) = 1 × 0.9 × 1.20 = 1.08(Q16.16 链式舍入)");
        }

        /// <summary>AC-2 边缘: 急救=0 → 医术修正 = 0;CombatPower = 1 × 0.9 × 1.0 = 0.9。</summary>
        [Test]
        public void test_ac2_medicalSkillZero_modifierIsZero()
        {
            Fix result = CombatPower.Compute(
                combatSkillLevel: 1,
                medicalSkillLevel: 0,
                weaponLine: WeaponLine.徒手,
                tuning: SkillTuningTable.Default);

            // 1 × 0.9 × (1 + 0) = 0.9
            Assert.That(result.Raw, Is.EqualTo(Fix.FromRational(9L, 10L).Raw),
                "CombatPower(1徒手, 0急救) = 1 × 0.9 × 1.0 = 0.9");
        }

        // ═══════════════════════════════════════════════════════════════════
        // AC-3: P0 仅 WeaponLine.徒手 吃到医术修正;其余 P0 线修正 = 0
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>AC-3 主例: 短兵=40, 手术=60 → 医术修正 = 0;
        /// CombatPower = 40 × 1.0 × 1.0 = 40(手术 P1a,P0 不生效)。</summary>
        [Test]
        public void test_ac3_shortBlade_medicalModIsZero()
        {
            Fix result = CombatPower.Compute(
                combatSkillLevel: 40,
                medicalSkillLevel: 60,
                weaponLine: WeaponLine.短兵,
                tuning: SkillTuningTable.Default);

            Assert.That(result.Raw, Is.EqualTo(40L * Fix.OneRaw),
                "CombatPower(40短兵, 60手术) = 40 × 1.0 × 1.0 = 40(手术未上线,P0 修正=0)");
        }

        /// <summary>AC-3 补充: 钝器=40, 医疗=60 → 无关联医术 → 修正 = 0;
        /// CombatPower = 40 × 1.0 × 1.0 = 40。</summary>
        [Test]
        public void test_ac3_blunt_medicalModIsZero()
        {
            Fix result = CombatPower.Compute(
                combatSkillLevel: 40,
                medicalSkillLevel: 60,
                weaponLine: WeaponLine.钝器,
                tuning: SkillTuningTable.Default);

            Assert.That(result.Raw, Is.EqualTo(40L * Fix.OneRaw),
                "CombatPower(40钝器, 60急救) = 40 × 1.0 × 1.0 = 40(钝器无关联医术)");
        }

        /// <summary>AC-3 补充: 长兵=40, 医疗=60 → 无关联医术 → 修正 = 0;
        /// CombatPower = 40 × 1.1 × 1.0 = 44(Q16.16 raw=2883600)。</summary>
        [Test]
        public void test_ac3_longBlade_medicalModIsZero()
        {
            Fix result = CombatPower.Compute(
                combatSkillLevel: 40,
                medicalSkillLevel: 60,
                weaponLine: WeaponLine.长兵,
                tuning: SkillTuningTable.Default);

            // 40 × 1.1(Q16.16 raw=72090) = raw=2883600
            Assert.That(result.Raw, Is.EqualTo(2883600L),
                "CombatPower(40长兵, 60急救) = 40 × 1.1 × 1.0 = 44(Q16.16 链式舍入)");
        }

        /// <summary>AC-3 补充: 暗器=40, 医疗=60 → 无关联医术 → 修正 = 0;
        /// CombatPower = 40 × 0.8 × 1.0 = 32(Q16.16 raw=2097160)。</summary>
        [Test]
        public void test_ac3_hidden_medicalModIsZero()
        {
            Fix result = CombatPower.Compute(
                combatSkillLevel: 40,
                medicalSkillLevel: 60,
                weaponLine: WeaponLine.暗器,
                tuning: SkillTuningTable.Default);

            // 40 × 0.8(Q16.16 raw=52429) = raw=2097160
            Assert.That(result.Raw, Is.EqualTo(2097160L),
                "CombatPower(40暗器, 60急救) = 40 × 0.8 × 1.0 = 32(Q16.16 链式舍入)");
        }

        /// <summary>AC-3 补充: 手术=0 时短兵修正仍 = 0。</summary>
        [Test]
        public void test_ac3_shortBlade_zeroMedical_returnsBaseOnly()
        {
            Fix result = CombatPower.Compute(
                combatSkillLevel: 40,
                medicalSkillLevel: 0,
                weaponLine: WeaponLine.短兵,
                tuning: SkillTuningTable.Default);

            Assert.That(result.Raw, Is.EqualTo(40L * Fix.OneRaw),
                "CombatPower(40短兵, 0手术) = 40 × 1.0 × 1.0 = 40");
        }

        // ═══════════════════════════════════════════════════════════════════
        // AC-4: 各武器线 WeaponMultiplier 可配置(默认值验证)
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>AC-6 验证:五条武器线默认 WeaponMultiplier = [0.9, 1.0, 1.0, 1.1, 0.8]。</summary>
        [Test]
        public void test_ac6_weaponMultipliers_defaultValues()
        {
            Fix[] expected = new Fix[]
            {
                Fix.FromRational(9L, 10L),   // 徒手 = 0.9
                Fix.FromRational(10L, 10L),  // 短兵 = 1.0
                Fix.FromRational(10L, 10L),  // 钝器 = 1.0
                Fix.FromRational(11L, 10L),  // 长兵 = 1.1
                Fix.FromRational(8L, 10L),   // 暗器 = 0.8
            };

            for (int i = 0; i < expected.Length; i++)
            {
                Fix actual = SkillTuningTable.Default.GetWeaponMultiplier((WeaponLine)i);
                Assert.That(actual.Raw, Is.EqualTo(expected[i].Raw),
                    $"WeaponLine {(WeaponLine)i} 默认倍率 = {expected[i].ToFloat()}");
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // AC-5: 昏迷阈值常量(UNCONSCIOUS_AT = Fix(0))
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>AC-5 主例: UNCONSCIOUS_AT = Fix(0),生命归零 = 昏迷(非死亡)。</summary>
        [Test]
        public void test_ac5_unconsciousAt_isZero()
        {
            Assert.That(SkillTuningTable.UnconsciousAt.Raw, Is.EqualTo(0L),
                "UNCONSCIOUS_AT = Fix(0),生命归零即昏迷");
        }

        /// <summary>AC-5 边缘: 负生命也满足昏迷条件(life <= 0)。</summary>
        [Test]
        public void test_ac5_negativeLife_alsoUnconscious()
        {
            Fix negativeLife = new Fix(-5L * Fix.OneRaw);
            bool isUnconscious = negativeLife.Raw <= SkillTuningTable.UnconsciousAt.Raw;
            Assert.That(isUnconscious, Is.True,
                "life = -5 <= 0 → 昏迷(非死亡)");
        }

        // ═══════════════════════════════════════════════════════════════════
        // AC-6: 五条武器线全部可计算(非零 CombatPower)
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>AC-6 主例:五条武器线各 level=10,医疗=0 → 计算全部非零。</summary>
        [Test]
        public void test_ac6_allWeaponLines_computeNonZero()
        {
            int level = 10;
            int medical = 0;

            // Q16.16 链式乘法舍入结果(非理想浮点)
            Fix[] expectedBase = new Fix[]
            {
                new Fix(589820L),    // 徒手 10*0.9 ≈ 8.9999 raw=589820
                new Fix(655360L),    // 短兵 10*1.0 = 10.0 raw=655360
                new Fix(655360L),    // 钝器 10*1.0 = 10.0 raw=655360
                new Fix(720900L),    // 长兵 10*1.1 ≈ 11.0001 raw=720900
                new Fix(524290L),    // 暗器 10*0.8 ≈ 8.0000 raw=524290
            };

            for (int i = 0; i < expectedBase.Length; i++)
            {
                Fix result = CombatPower.Compute(level, medical, (WeaponLine)i, SkillTuningTable.Default);
                Assert.That(result.Raw, Is.EqualTo(expectedBase[i].Raw),
                    $"{(WeaponLine)i} CombatPower(10, 0) = {expectedBase[i].ToFloat()}");
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // Fix.Div 定点除法专项验证(CombatPower 依赖 Fix.Div 的医术修正)
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>Fix.Div 主例: 20/60 → Fix(0.333...) = floor(21845)。</summary>
        [Test]
        public void test_fixDiv_20div60_returnsThird()
        {
            Fix numerator = Fix.FromRational(20L, 1L);   // 20.0
            Fix denominator = Fix.FromRational(60L, 1L);  // 60.0
            Fix result = numerator / denominator;

            // 20/60 = 1/3 ≈ 0.33333; raw = floor(0.33333 * 65536) = 21845
            long expectedRaw = (20L * Fix.OneRaw) / 60L;
            Assert.That(result.Raw, Is.EqualTo(expectedRaw),
                "Fix.Div(20, 60) ≈ 0.333, raw = 21845");
        }

        /// <summary>Fix.Div 边缘: 60/60 → Fix(1.0)。</summary>
        [Test]
        public void test_fixDiv_60div60_returnsOne()
        {
            Fix numerator = Fix.FromRational(60L, 1L);
            Fix denominator = Fix.FromRational(60L, 1L);
            Fix result = numerator / denominator;

            Assert.That(result.Raw, Is.EqualTo(Fix.OneRaw),
                "Fix.Div(60, 60) = 1.0");
        }

        /// <summary>Fix.Div 边缘: 0/60 → Fix(0.0)。</summary>
        [Test]
        public void test_fixDiv_0div60_returnsZero()
        {
            Fix result = Fix.Zero / Fix.FromRational(60L, 1L);

            Assert.That(result.Raw, Is.EqualTo(0L),
                "Fix.Div(0, 60) = 0.0");
        }

        /// <summary>Fix.Div 零分母 → DivideByZeroException。</summary>
        [Test]
        public void test_fixDiv_zeroDenominator_throws()
        {
            Fix numerator = Fix.FromRational(20L, 1L);
            Assert.Throws<DivideByZeroException>(() =>
            {
                Fix _ = numerator / Fix.Zero;
            }, "Fix.Div 分母为零须抛 DivideByZeroException");
        }

        // ═══════════════════════════════════════════════════════════════════
        // 医术修正单独验证(MedicalModifier 独立测试)
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>医术修正 = FixDiv(MED_COMBAT_MOD × 60, 60) = Fix(0.20) 上限验证。</summary>
        [Test]
        public void test_medicalModifier_60level_returns020()
        {
            Fix numerator = SkillTuningTable.Default.MedicalCombatModifier * new Fix(60L * Fix.OneRaw);
            Fix denominator = new Fix(SKILL_CAP * Fix.OneRaw);
            Fix mod = numerator / denominator;

            Assert.That(mod.Raw, Is.EqualTo(SkillTuningTable.Default.MedicalCombatModifier.Raw),
                "医术修正(60级) = FixDiv(0.20 × 60, 60) = 0.20(上限)");
        }

        /// <summary>医术修正 = FixDiv(MED_COMBAT_MOD × 30, 60) = Fix(0.10) 半上限。</summary>
        [Test]
        public void test_medicalModifier_30level_returns010()
        {
            Fix numerator = SkillTuningTable.Default.MedicalCombatModifier * new Fix(30L * Fix.OneRaw);
            Fix denominator = new Fix(SKILL_CAP * Fix.OneRaw);
            Fix mod = numerator / denominator;

            // 0.20 * 30 = 6.0 raw=393216; /60 = 0.10 raw=6554
            Assert.That(mod.Raw, Is.EqualTo(6554L),
                "医术修正(30级) = FixDiv(0.20 × 30, 60) = 0.10");
        }

        // ═══════════════════════════════════════════════════════════════════
        // 数值边界与多级连升边界(CombatPower 极端值)
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>极端值: 长兵=60, 医疗=60 → 60 × 1.1 × 1.0 = 66(无医疗修正)。</summary>
        [Test]
        public void test_extreme_longBlade60_noMedical_returns66()
        {
            Fix result = CombatPower.Compute(60, 60, WeaponLine.长兵, SkillTuningTable.Default);

            // 60 × 1.1(Q16.16 raw=72090) = raw=4325400
            Assert.That(result.Raw, Is.EqualTo(4325400L),
                "CombatPower(60长兵, 60急救) = 60 × 1.1 × 1.0 = 66(长兵无医疗修正)");
        }

        /// <summary>极端值: 徒手=60, 急救=60 → 64.8(全局 CombatPower 上限验证)。</summary>
        [Test]
        public void test_extreme_upperBound_combatPower()
        {
            Fix result = CombatPower.Compute(60, 60, WeaponLine.徒手, SkillTuningTable.Default);

            // 60 × 0.9 × 1.20(Q16.16 链式舍入) raw = 4246693
            Assert.That(result.Raw, Is.EqualTo(4246693L),
                "CombatPower 上限 = 64.8(60徒手 + 60急救,Q16.16 链式舍入)");
        }

        /// <summary>边缘: CombatPower 返回 Fix 类型(非 int/float) — ADR-025 传送契约。</summary>
        [Test]
        public void test_ac4_combatPower_returnsFixType_adr025()
        {
            Fix result = CombatPower.Compute(60, 60, WeaponLine.徒手, SkillTuningTable.Default);

            Assert.That(result.GetType(), Is.EqualTo(typeof(Fix)),
                "CombatPower 返回 Fix 类型,非 int/float");
        }

        // ═══════════════════════════════════════════════════════════════════
        // Fix.Div 奇数分母舍入验证(Finding 2 修复回归)
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>Fix.Div 奇数分母: Fix(1) / Fix(3) ≈ 0.333 → raw = 21845。</summary>
        [Test]
        public void test_fixDiv_oddDenominator_1div3_returns0333()
        {
            Fix result = Fix.One / new Fix(3L * Fix.OneRaw);
            Assert.That(result.Raw, Is.EqualTo(21845L),
                "Fix.Div(1, 3) ≈ 0.333, raw = 21845(奇数分母舍入正确)");
        }

        /// <summary>Fix.Div 奇数分母: Fix(2) / Fix(3) ≈ 0.667 → raw = 43691。</summary>
        [Test]
        public void test_fixDiv_oddDenominator_2div3_returns0667()
        {
            Fix result = new Fix(2L * Fix.OneRaw) / new Fix(3L * Fix.OneRaw);
            Assert.That(result.Raw, Is.EqualTo(43691L),
                "Fix.Div(2, 3) ≈ 0.667, raw = 43691(奇数分母舍入正确)");
        }

        // ═══════════════════════════════════════════════════════════════════
        // Fix.Div 负值舍入验证(ROUND_HALF_AWAY_FROM_ZERO 对称性)
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>Fix.Div 负值舍入验证: Fix(-3) / Fix(2) = -1.5 → raw = -98304。</summary>
        [Test]
        public void test_fixDiv_negativeNumerator_exactResult()
        {
            Fix result = new Fix(-3L * Fix.OneRaw) / new Fix(2L * Fix.OneRaw);
            Assert.That(result.Raw, Is.EqualTo(-98304L),
                "Fix.Div(-3, 2) = -1.5, raw = -98304(恰好表示,无需舍入)");
        }

        /// <summary>Fix.Div 负值舍入验证: Fix(-2) / Fix(3) ≈ -0.667 → 舍入到 -43691。</summary>
        [Test]
        public void test_fixDiv_negativeNumerator_roundsCorrectly()
        {
            Fix result = new Fix(-2L * Fix.OneRaw) / new Fix(3L * Fix.OneRaw);
            Assert.That(result.Raw, Is.EqualTo(-43691L),
                "Fix.Div(-2, 3) ≈ -0.667, raw = -43691(ROUND_HALF_AWAY_FROM_ZERO)");
        }

        /// <summary>Fix.Div 双负值: Fix(-3) / Fix(-2) = 1.5 → raw = 98304。</summary>
        [Test]
        public void test_fixDiv_bothNegative_roundsAwayFromZero()
        {
            Fix result = new Fix(-3L * Fix.OneRaw) / new Fix(-2L * Fix.OneRaw);
            Assert.That(result.Raw, Is.EqualTo(98304L),
                "Fix.Div(-3, -2) = 1.5, raw = 98304(正结果)");
        }

        // ═══════════════════════════════════════════════════════════════════
        // Fix.Div 溢出边界验证(Finding 1 修复回归)
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>Fix.Div 溢出边界: raw = 2^48 + 1 > 2^48 → 触发 OverflowException。</summary>
        [Test]
        public void test_fixDiv_overflowBoundary_throws()
        {
            // Fix.Div 守卫: ua > (ulong.MaxValue >> 16) = 2^48 时抛异常
            // 直接用 2^48 + 1 的 long 值,绕过编译期 checked 算术
            Fix large = new Fix(281474976710657L); // 2^48 + 1
            Assert.Throws<OverflowException>(() =>
            {
                Fix _ = large / Fix.One;
            }, "Fix.Div 被除数 ua > 2^48 须抛 OverflowException");
        }

        // ═══════════════════════════════════════════════════════════════════
        // CombatPower 异常输入验证
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>CombatPower 非法武器线 → ArgumentOutOfRangeException。</summary>
        [Test]
        public void test_compute_invalidWeaponLine_throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                CombatPower.Compute(40, 60, (WeaponLine)999, SkillTuningTable.Default);
            }, "CombatPower 非法武器线须抛 ArgumentOutOfRangeException");
        }

        /// <summary>CombatPower 负 combatSkillLevel → ArgumentOutOfRangeException。</summary>
        [Test]
        public void test_compute_negativeCombatSkillLevel_throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                CombatPower.Compute(-1, 60, WeaponLine.徒手, SkillTuningTable.Default);
            }, "CombatPower 负 combatSkillLevel 须抛 ArgumentOutOfRangeException");
        }

        /// <summary>CombatPower 负 medicalSkillLevel → ArgumentOutOfRangeException。</summary>
        [Test]
        public void test_compute_negativeMedicalSkillLevel_throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                CombatPower.Compute(60, -1, WeaponLine.徒手, SkillTuningTable.Default);
            }, "CombatPower 负 medicalSkillLevel 须抛 ArgumentOutOfRangeException");
        }

        /// <summary>医术修正 60 级独立断言: mod.Raw == MED_COMBAT_MOD.Raw。</summary>
        [Test]
        public void test_medicalModifier_60level_rawEqualsMedicalCombatModifierRaw()
        {
            Fix numerator = SkillTuningTable.Default.MedicalCombatModifier * new Fix(60L * Fix.OneRaw);
            Fix denominator = new Fix(SKILL_CAP * Fix.OneRaw);
            Fix mod = numerator / denominator;

            Assert.That(mod.Raw, Is.EqualTo(SkillTuningTable.Default.MedicalCombatModifier.Raw),
                "医术修正 60 级 = MED_COMBAT_MOD.Raw,保证 0.20 边界舍入回归可检测");
        }

        // ═══════════════════════════════════════════════════════════════════
        // 医术修正超出 SKILL_CAP 的行为文档化(G-4 决策记录)
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>医术修正超出 SKILL_CAP: medicalSkillLevel=120 → 0.40(超出 AC-2 声明的 [0, 0.20])。
        /// <para>当前实现不做 clamp,调用方保证 medicalSkillLevel ∈ [0, SKILL_CAP]。</para></summary>
        [Test]
        public void test_medicalModifier_aboveCap_returnsUnclamped()
        {
            Fix numerator = SkillTuningTable.Default.MedicalCombatModifier * new Fix(120L * Fix.OneRaw);
            Fix denominator = new Fix(SKILL_CAP * Fix.OneRaw);
            Fix mod = numerator / denominator;

            // 0.20 * 120 / 60 = 0.40
            Assert.That(mod.Raw, Is.EqualTo(Fix.FromRational(40L, 100L).Raw),
                "医术修正(120级) = 0.40(超上限,调用方负责 clamp)");
        }
    }
}
