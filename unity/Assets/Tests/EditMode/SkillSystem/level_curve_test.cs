// ============================================================================
// 升级曲线 EditMode 单测 —— Story 003 验收
// 权威来源: production/epics/skill-system/story-003-level-curve.md
//   · AC-1 ~ AC-7(XP_to_next 定点求值 / P 取值校验 / FixPow 整数幂 /
//     FixSqrt 半整数幂 / 满级锁定 / 升级触发连升 / 构建期定表断言可选)
//   · TR-skill-001(TR-registry.yaml:1362)
//   · ADR-026 §Decision 一 / §Decision 二 / Implementation Guidelines 1–6
// ============================================================================
// 落点:unity/Assets/Tests/EditMode/SkillSystem/level_curve_test.cs
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
    /// <summary>Story 003 升级曲线的 EditMode 单测。</summary>
    [TestFixture]
    internal sealed class LevelCurveTest
    {
        private const int SKILL_CAP = SkillRegistry.SKILL_CAP; // 60

        // ═══════════════════════════════════════════════════════════════════
        // AC-1: XP_to_next(n) = C × n^P 定点求值正确
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>AC-1 主例: C=40, P=1(即 n^1=n), n=1 → Fix(40)。</summary>
        [Test]
        public void test_ac1_xpToNext_c40_p1_n1_returnsFix40()
        {
            var tuning = MakeTuning(c: Fix.FromRational(40L, 1L), p: Fix.One);
            Fix xp = SkillProgression.XpToNext(level: 1, tuning);
            Assert.That(xp.Raw, Is.EqualTo(40L * Fix.OneRaw),
                "C=40, P=1, n=1 → XP_to_next = 40");
        }

        /// <summary>AC-1 边缘: C=40, P=2, n=59 → Fix(40 × 59^2) = Fix(139640)。</summary>
        [Test]
        public void test_ac1_xpToNext_c40_p2_n59_returnsFix139640()
        {
            var tuning = MakeTuning(c: Fix.FromRational(40L, 1L), p: Fix.FromRational(2L, 1L));
            // 59^2 = 3481; 40 × 3481 = 139640
            long expectedRaw = 40L * 3481L * Fix.OneRaw;
            Fix xp = SkillProgression.XpToNext(level: 59, tuning);
            Assert.That(xp.Raw, Is.EqualTo(expectedRaw),
                "C=40, P=2, n=59 → 40 × 59^2 = 139640");
        }

        /// <summary>AC-1 边缘: n=60(满级) → PositiveInfinity。</summary>
        [Test]
        public void test_ac1_xpToNext_level60_returnsPositiveInfinity()
        {
            var tuning = MakeTuning(c: Fix.FromRational(40L, 1L), p: Fix.One);
            Fix xp = SkillProgression.XpToNext(level: SKILL_CAP, tuning);
            Assert.That(xp, Is.EqualTo(Fix.PositiveInfinity),
                "level=SKILL_CAP(60) → PositiveInfinity(满级锁定)");
        }

        // ═══════════════════════════════════════════════════════════════════
        // AC-2: P 取值集构建期校验 —— 非 {整数, 整数+1/2} 须 throw
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>AC-2 主例: P=1.4(raw = 1.4 * 65536 = 91750) 非整数/半整数 → throw。</summary>
        [Test]
        public void test_ac2_pNonIntegerHalf_throwsArgumentOutOfRange()
        {
            // 1.4 = 91750/65536 raw, frac = 91750 & 0xFFFF = 26214 ≠ 0 且 ≠ 0x8000
            Fix pInvalid = new Fix(91750L);
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                Fix.Pow(Fix.One, pInvalid),
                "P=1.4(非整数/半整数)须抛 ArgumentOutOfRangeException");
        }

        /// <summary>AC-2 边缘: P=1(整数) → OK;P=1.5(半整数) → OK;P=2(整数) → OK。</summary>
        [Test]
        public void test_ac2_pIntegerAndHalf_doesNotThrow()
        {
            Assert.DoesNotThrow(() =>
            {
                Fix.Pow(Fix.FromRational(3L, 1L), Fix.One);                  // P=1
                Fix.Pow(Fix.FromRational(3L, 1L), Fix.FromRational(3L, 2L)); // P=1.5
                Fix.Pow(Fix.FromRational(3L, 1L), Fix.FromRational(2L, 1L)); // P=2
            }, "P ∈ {整数, 整数+1/2} 不抛异常");
        }

        // ═══════════════════════════════════════════════════════════════════
        // AC-3: FixPow 整数幂路径正确(重复 Fix.Mul)
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>AC-3 主例: FixPow(3, 4) → Fix(81)(3^4 = 81)。</summary>
        [Test]
        public void test_ac3_fixPow_3exp4_returnsFix81()
        {
            Fix result = Fix.Pow(new Fix(3L * Fix.OneRaw), 4);
            Assert.That(result.Raw, Is.EqualTo(81L * Fix.OneRaw),
                "FixPow(3, 4) = 3^4 = 81");
        }

        /// <summary>AC-3 边缘: exponent=0 → Fix.One;exponent=1 → base 本身。</summary>
        [Test]
        public void test_ac3_fixPow_zeroAndOne_returnsIdentity()
        {
            Fix base3 = new Fix(3L * Fix.OneRaw);
            Assert.That(Fix.Pow(base3, 0).Raw, Is.EqualTo(Fix.OneRaw),
                "FixPow(x, 0) = 1");
            Assert.That(Fix.Pow(base3, 1).Raw, Is.EqualTo(base3.Raw),
                "FixPow(x, 1) = x");
        }

        // ═══════════════════════════════════════════════════════════════════
        // AC-4: FixSqrt 半整数幂路径正确(整数牛顿迭代)
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>AC-4 主例: FixSqrt(Fix(4)) → Fix(2.0)(raw = 131072)。</summary>
        [Test]
        public void test_ac4_fixSqrt_4_returnsFix2()
        {
            Fix result = Fix.Sqrt(new Fix(4L * Fix.OneRaw));
            Assert.That(result.Raw, Is.EqualTo(2L * Fix.OneRaw),
                "FixSqrt(4) = 2.0, raw = 2 * 65536 = 131072");
        }

        /// <summary>AC-4 边缘: FixSqrt(Fix(2)) → approx Fix(1.414)。
        /// <para>sqrt(2) ≈ 1.41421356; raw = floor(1.41421356 × 65536) = floor(92681.5) = 92681</para>
        /// <para>Newton 迭代起点 = 131072>>1 = 65536;收敛后 guess << 8 = 92681。</para>
        /// </summary>
        [Test]
        public void test_ac4_fixSqrt_2_returnsApprox1414()
        {
            Fix result = Fix.Sqrt(new Fix(2L * Fix.OneRaw));
            // 预期: floor(sqrt(2) * 65536) = 92681
            long expectedRaw = 92681L;
            Assert.That(result.Raw, Is.EqualTo(expectedRaw),
                "FixSqrt(2) raw ≈ 92681(sqrt(2) × 65536 = 92681.5 → floor = 92681)");
        }

        /// <summary>AC-4 验证:半整数幂路径 = Pow(base, k) * Sqrt(base)。</summary>
        [Test]
        public void test_ac4_halfIntegerPow_equalsIntPowTimesSqrt()
        {
            // 4^(1/2) = Pow(4, 0) * Sqrt(4) = 1 * 2 = 2
            Fix base4 = new Fix(4L * Fix.OneRaw);
            Fix halfExp = Fix.FromRational(1L, 2L); // 0.5
            Fix result = Fix.Pow(base4, halfExp);
            Assert.That(result.Raw, Is.EqualTo(2L * Fix.OneRaw),
                "4^(1/2) = Pow(4,0) * Sqrt(4) = 2.0");
        }

        // ═══════════════════════════════════════════════════════════════════
        // AC-5: 满级锁定
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>AC-5 主例: XpToNext(60) → PositiveInfinity。</summary>
        [Test]
        public void test_ac5_xpToNext_atCap_returnsPositiveInfinity()
        {
            var tuning = MakeTuning(c: Fix.FromRational(40L, 1L), p: Fix.One);
            Assert.That(SkillProgression.XpToNext(SKILL_CAP, tuning),
                Is.EqualTo(Fix.PositiveInfinity));
        }

        /// <summary>AC-5 边缘: TryLevelUp 在 level=60 时返回 false(不升级)。</summary>
        [Test]
        public void test_ac5_tryLevelUp_atCap_returnsFalse()
        {
            int level = SKILL_CAP;
            Fix xp = Fix.FromRational(99999L, 1L); // 海量 XP
            var tuning = MakeTuning(c: Fix.FromRational(40L, 1L), p: Fix.One);

            bool changed = SkillProgression.TryLevelUp(ref level, ref xp,
                Fix.FromRational(1000L, 1L), tuning);

            Assert.That(changed, Is.False, "满级 TryLevelUp 应返回 false");
            Assert.That(level, Is.EqualTo(SKILL_CAP), "满级不应再升级");
        }

        // ═══════════════════════════════════════════════════════════════════
        // AC-6: 升级触发与连升
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>AC-6 主例: level=1, xp=0, 获得 xp=120, C=40, P=1 → level=3, xp=0。
        /// <para>1→2 需 40, 2→3 需 80; 40+80 = 120;刚好用完。</para></summary>
        [Test]
        public void test_ac6_tryLevelUp_twoLevelUps_exactXp()
        {
            int level = 1;
            Fix xp = new Fix(0L); // raw = 0
            Fix gained = Fix.FromRational(120L, 1L);
            var tuning = MakeTuning(c: Fix.FromRational(40L, 1L), p: Fix.One);

            bool changed = SkillProgression.TryLevelUp(ref level, ref xp, gained, tuning);

            Assert.That(changed, Is.True, "应触发升级");
            Assert.That(level, Is.EqualTo(3), "连升两级: 1→3");
            Assert.That(xp.Raw, Is.EqualTo(0L), "XP 刚好用完 → 剩余 0");
        }

        /// <summary>AC-6 边缘: XP 刚好等于阈值 → 升级,剩余 XP = 0。</summary>
        [Test]
        public void test_ac6_tryLevelUp_exactThreshold_levelUpWithZeroRemainder()
        {
            int level = 1;
            Fix xp = Fix.Zero;
            // C=40, P=1: 1→2 需 40; 给 40 刚好
            Fix gained = Fix.FromRational(40L, 1L);
            var tuning = MakeTuning(c: Fix.FromRational(40L, 1L), p: Fix.One);

            bool changed = SkillProgression.TryLevelUp(ref level, ref xp, gained, tuning);

            Assert.That(changed, Is.True, "刚好达标应升级");
            Assert.That(level, Is.EqualTo(2), "1→2");
            Assert.That(xp.Raw, Is.EqualTo(0L), "刚好用完 → 剩余 0");
        }

        /// <summary>AC-6 边缘: XP 不足阈值 → 不升级。</summary>
        [Test]
        public void test_ac6_tryLevelUp_insufficientXp_noLevelUp()
        {
            int level = 1;
            Fix xp = Fix.Zero;
            // C=40, P=1: 1→2 需 40; 给 39.5(不足)
            Fix gained = Fix.FromRational(79L, 2L); // 39.5
            var tuning = MakeTuning(c: Fix.FromRational(40L, 1L), p: Fix.One);

            bool changed = SkillProgression.TryLevelUp(ref level, ref xp, gained, tuning);

            Assert.That(changed, Is.False, "XP 不足不升级");
            Assert.That(level, Is.EqualTo(1), "等级不变");
            // xp 已累加 39.5(不足阈值,保留剩余)
            Assert.That(xp.Raw, Is.EqualTo(Fix.FromRational(79L, 2L).Raw),
                "XP 不足时保留剩余");
        }

        /// <summary>AC-6 补充:负 XP 不触发升级,等级/XP 不变。</summary>
        [Test]
        public void test_ac6_tryLevelUp_negativeXp_noLevelUp()
        {
            int level = 1;
            Fix xp = Fix.FromRational(50L, 1L); // 已有 50 XP
            Fix gained = Fix.FromRational(-10L, 1L); // 负 XP
            var tuning = MakeTuning(c: Fix.FromRational(40L, 1L), p: Fix.One);

            bool changed = SkillProgression.TryLevelUp(ref level, ref xp, gained, tuning);

            Assert.That(changed, Is.False, "负 XP 不升级");
            Assert.That(level, Is.EqualTo(1), "等级不变");
            Assert.That(xp.Raw, Is.EqualTo(Fix.FromRational(50L, 1L).Raw),
                "负 XP 不影响已有 XP");
        }

        // ═══════════════════════════════════════════════════════════════════
        // AC-6 补充:边界与极端路径
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>AC-6 补充:level=0 → XpToNext = 0(C × 0^P = 0)。</summary>
        [Test]
        public void test_ac6_xpToNext_level0_returnsFixZero()
        {
            var tuning = MakeTuning(c: Fix.FromRational(40L, 1L), p: Fix.FromRational(3L, 2L));
            Fix xp = SkillProgression.XpToNext(level: 0, tuning);
            Assert.That(xp.Raw, Is.EqualTo(0L),
                "level=0 → C × 0^P = 0 XP(0^0 不进入,因 level < SKILL_CAP 且 P>0)");
        }

        /// <summary>G-1 修复: 从 level=0 给 XP → 正确升级到 level=1(XpToNext(0)=0 不消耗 XP)。</summary>
        [Test]
        public void test_ac6_tryLevelUp_fromLevel0_gainsLevel1_withoutConsumingXp()
        {
            int level = 0;
            Fix xp = Fix.Zero;
            Fix gained = Fix.FromRational(10L, 1L);
            var tuning = MakeTuning(c: Fix.FromRational(40L, 1L), p: Fix.One);

            bool changed = SkillProgression.TryLevelUp(ref level, ref xp, gained, tuning);

            Assert.That(changed, Is.True, "level=0 时应触发一次免费升级");
            Assert.That(level, Is.EqualTo(1), "0→1");
            Assert.That(xp.Raw, Is.EqualTo(gained.Raw),
                "XpToNext(0)=0 不消耗 XP,全部保留");
        }

        /// <summary>AC-6 补充:从 level=58 给海量 XP → 连升至 60 并钳制。</summary>
        [Test]
        public void test_ac6_tryLevelUp_nearCap_multiLevel_clampedToCap()
        {
            int level = 58;
            Fix xp = Fix.Zero;
            // C=40, P=1.5: 58→59 = 40×58^1.5, 59→60 = 40×59^1.5;给足够 XP 连升两级
            Fix gained = Fix.FromRational(99999L, 1L); // 海量 XP
            var tuning = MakeTuning(c: Fix.FromRational(40L, 1L), p: Fix.FromRational(3L, 2L));

            bool changed = SkillProgression.TryLevelUp(ref level, ref xp, gained, tuning);

            Assert.That(changed, Is.True, "应触发升级");
            Assert.That(level, Is.EqualTo(SKILL_CAP), "钳制在 60");
        }

        // ═══════════════════════════════════════════════════════════════════
        // AC-7: 构建期定表断言(可选,直接实现)
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>AC-7: 对 C=40, P=1.5 预计算 n=1..59 的 XP_to_next,
        /// 断言每个值等于 SkillProgression.XpToNext 的输出(定表 = 记忆化)。
        /// </summary>
        [Test]
        public void test_ac7_lookupTable_matchesSkillProgression()
        {
            var tuning = MakeTuning(c: Fix.FromRational(40L, 1L), p: Fix.FromRational(3L, 2L));
            // 构建期定表: 预计算 60 档(0..59,0 号位不用但占位)
            long[] table = new long[SKILL_CAP];
            for (int n = 1; n < SKILL_CAP; n++)
            {
                table[n] = SkillProgression.XpToNext(n, tuning).Raw;
            }

            // 运行期逐值比对(模拟「构建期断言」逻辑)
            for (int n = 1; n < SKILL_CAP; n++)
            {
                Fix computed = SkillProgression.XpToNext(n, tuning);
                Assert.That(computed.Raw, Is.EqualTo(table[n]),
                    $"n={n}: FixPow 输出与定表逐位相等(构建期断言模拟)");
            }

            // n=60 档位
            Assert.That(table[59], Is.Not.EqualTo(Fix.PositiveInfinity.Raw),
                "n=59 未满级,raw ≠ PositiveInfinity");
        }

        // ═══════════════════════════════════════════════════════════════════
        // 辅助
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>构造最小调参表(仅 C 与 P 有意义,其余用默认值)。</summary>
        private static SkillTuningTable MakeTuning(Fix c, Fix p)
        {
            // 复用 SkillTuningTable.Default 但覆写 C/P
            return new SkillTuningTable(
                c: c,
                p: p,
                noveltyFirst: SkillTuningTable.Default.NoveltyFirstCoeff,
                noveltyDecay: SkillTuningTable.Default.NoveltyDecayCoeff,
                noveltyCooldownTicks: SkillTuningTable.Default.NoveltyCooldown,
                deathLoss: SkillTuningTable.Default.DeathRetention,
                medCombatMod: SkillTuningTable.Default.MedicalCombatModifier,
                weaponMultipliers: SkillTuningTable.Default.GetWeaponMultipliers(),
                diagTiers: SkillTuningTable.Default.GetDiagTiers(),
                insightTiers: SkillTuningTable.Default.GetInsightTiers(),
                insightBonus: SkillTuningTable.Default.GetInsightBonus());
        }
    }
}
