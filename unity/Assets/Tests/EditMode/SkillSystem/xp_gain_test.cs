// ============================================================================
// 单次经验增益公式测试 —— Story 002 验收
// 权威来源: production/epics/skill-system/story-002-xp-gain-formula.md
// ============================================================================
// 落点:unity/Assets/Tests/EditMode/SkillSystem/xp_gain_test.cs
//    按 ADR-025 §⑤(2026-09-23 路径订正注)Unity 只编译 Assets/ 树,真身落此。
// ============================================================================

using System;
using System.Linq;
using NUnit.Framework;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.Contracts.SkillSystem;

namespace DaYiJingCheng.Tests.Unit.SkillSystem
{
    [TestFixture]
    internal sealed class XpGainTest
    {
        // ════════════════ AC-1: XP_gain 定点求值正确 ════════════════

        [Test]
        public void test_xpGain_diagnostic_firstEncounter_returns36()
        {
            // BASE(诊断)=8, K_difficulty=1.5, K_novelty=First(3.0)
            // 8 × 1.5 × 3.0 = 36.0
            Fix difficulty = new Fix(3L * Fix.OneRaw / 2); // 1.5
            Fix xpGain = XpGainCalculator.ComputeXpGain(
                (int)SkillId.诊断, NoveltyClass.First, difficulty);

            Assert.That(xpGain.Raw, Is.EqualTo(36L * Fix.OneRaw),
                "诊断首次遇见 ×1.5 难度 = 36.0 XP");
        }

        [Test]
        public void test_xpGain_smallBase_returnsCorrectValue()
        {
            // 奔跑 BASE = 0.2 ×1.0 ×1.0 = 0.2
            Fix difficulty = new Fix(Fix.OneRaw); // 1.0
            Fix xpGain = XpGainCalculator.ComputeXpGain(
                (int)SkillId.奔跑, NoveltyClass.Normal, difficulty);

            Assert.That(xpGain.Raw, Is.EqualTo(Fix.OneRaw / 5),
                "奔跑 BASE=0.2 ×1.0 ×1.0 = 0.2");
        }

        [Test]
        public void test_xpGain_difficultyZero_returnsZero()
        {
            // K_difficulty = 0 时 XP_gain = 0
            Fix difficulty = new Fix(0L); // 0.0
            Fix xpGain = XpGainCalculator.ComputeXpGain(
                (int)SkillId.诊断, NoveltyClass.First, difficulty);

            Assert.That(xpGain.Raw, Is.EqualTo(0L),
                "难度系数 0 ⇒ XP_gain = 0");
        }

        // ════════════════ AC-2: BASE 默认值来自注册表 ════════════════

        [Test]
        public void test_xpGain_baseFromRegistry_diagnostic_is8()
        {
            // 仅 K_novelty=1.0, K_difficulty=1.0 → XP_gain = BASE
            Fix xpGain = XpGainCalculator.ComputeXpGain(
                (int)SkillId.诊断, NoveltyClass.Normal, new Fix(Fix.OneRaw));

            Assert.That(xpGain.Raw, Is.EqualTo(8L * Fix.OneRaw),
                "诊断 BASE = 8, Normal ×1.0 ⇒ 8.0 XP");
        }

        [Test]
        public void test_xpGain_baseFromRegistry_gathering_is1()
        {
            Fix xpGain = XpGainCalculator.ComputeXpGain(
                (int)SkillId.采集, NoveltyClass.Normal, new Fix(Fix.OneRaw));

            Assert.That(xpGain.Raw, Is.EqualTo(1L * Fix.OneRaw),
                "采集 BASE = 1, Normal ×1.0 ⇒ 1.0 XP");
        }

        // ════════════════ AC-3: K_novelty 三档系数正确 ════════════════

        [Test]
        public void test_xpGain_noveltyFirst_coefficient3()
        {
            Fix xpGain = XpGainCalculator.ComputeXpGain(
                (int)SkillId.诊断, NoveltyClass.First, new Fix(Fix.OneRaw));

            // 8 × 1.0 × 3.0 = 24
            Assert.That(xpGain.Raw, Is.EqualTo(24L * Fix.OneRaw),
                "首次遇见系数 = 3.0 ⇒ 24 XP");
        }

        [Test]
        public void test_xpGain_noveltyStale_coefficient02()
        {
            Fix xpGain = XpGainCalculator.ComputeXpGain(
                (int)SkillId.诊断, NoveltyClass.Stale, new Fix(Fix.OneRaw));

            // 8 × 1.0 × 0.2 = 1.6; 0.2 在 Q16.16 = 2*OneRaw/10 = 13107(近似)
            // 实际 Fix.Mul 结果: 8 * 13107 = 104856(非 16*OneRaw/10=104857)
            Assert.That(xpGain.Raw, Is.EqualTo(8L * (2L * Fix.OneRaw / 10)),
                "冷却期内系数 = 0.2 ⇒ 1.6 XP,raw=104856");
        }

        [Test]
        public void test_xpGain_noveltyNormal_coefficient1()
        {
            Fix xpGain = XpGainCalculator.ComputeXpGain(
                (int)SkillId.诊断, NoveltyClass.Normal, new Fix(Fix.OneRaw));

            // 8 × 1.0 × 1.0 = 8
            Assert.That(xpGain.Raw, Is.EqualTo(8L * Fix.OneRaw),
                "普通系数 = 1.0 ⇒ 8 XP");
        }

        // ════════════════ AC-4: K_difficulty 外部入参(诊断错误 = 0.5) ════════════════

        [Test]
        public void test_xpGain_difficulty05_halfXp()
        {
            // 诊断错误: K_difficulty = 0.5
            Fix difficulty = new Fix(5L * Fix.OneRaw / 10); // 0.5
            Fix xpGain = XpGainCalculator.ComputeXpGain(
                (int)SkillId.诊断, NoveltyClass.Normal, difficulty);

            // 8 × 0.5 × 1.0 = 4
            Assert.That(xpGain.Raw, Is.EqualTo(4L * Fix.OneRaw),
                "诊断错误 K_difficulty=0.5 ⇒ 半值 4 XP");
        }

        [Test]
        public void test_xpGain_difficulty30_upperBound()
        {
            // 难度上限 K_difficulty = 3.0
            Fix difficulty = new Fix(3L * Fix.OneRaw / 1); // 3.0
            Fix xpGain = XpGainCalculator.ComputeXpGain(
                (int)SkillId.诊断, NoveltyClass.Normal, difficulty);

            // 8 × 3.0 × 1.0 = 24
            Assert.That(xpGain.Raw, Is.EqualTo(24L * Fix.OneRaw),
                "难度上限 3.0 ⇒ 24 XP");
        }

        // ════════════════ AC-5: 不做 SKILL_CAP 截断(caller 负责) ════════════════

        [Test]
        public void test_xpGain_noTruncation_returnsFullValue()
        {
            // XP_gain 可以超过 SKILL_CAP,本方法不做截断
            Fix hugeDifficulty = new Fix(100L * Fix.OneRaw / 1); // 100.0
            Fix xpGain = XpGainCalculator.ComputeXpGain(
                (int)SkillId.诊断, NoveltyClass.First, hugeDifficulty);

            // 8 × 100 × 3 = 2400 —— 远 > 60,但本方法只返回计算值
            Assert.That(xpGain.Raw, Is.EqualTo(2400L * Fix.OneRaw),
                "大难度系数不截断,返回完整 2400 XP");
        }

        // ════════════════ AC-6: 定点求值逐位一致(边界值) ════════════════

        [Test]
        public void test_xpGain_bitExact_runningNormal()
        {
            // 奔跑 BASE=0.2, K_difficulty=1.0, K_novelty=Normal=1.0
            // 期望 = 0.2 × 1.0 × 1.0 = 0.2
            // raw = 0.2 × 65536 = 13107
            Fix xpGain = XpGainCalculator.ComputeXpGain(
                (int)SkillId.奔跑, NoveltyClass.Normal, new Fix(Fix.OneRaw));

            Assert.That(xpGain.Raw, Is.EqualTo(Fix.OneRaw / 5),
                "奔跑 Normal = 0.2, raw = 65536/5 = 13107 逐位一致");
        }

        [Test]
        public void test_xpGain_bitExact_combatFirstEncounter()
        {
            // 徒手 BASE=2, K_difficulty=1.5, K_novelty=First=3.0
            // 期望 = 2 × 1.5 × 3.0 = 9
            Fix difficulty = new Fix(3L * Fix.OneRaw / 2);
            Fix xpGain = XpGainCalculator.ComputeXpGain(
                (int)SkillId.徒手, NoveltyClass.First, difficulty);

            Assert.That(xpGain.Raw, Is.EqualTo(9L * Fix.OneRaw),
                "徒手首次 ×1.5 = 9, raw = 9 × 65536 = 589824 逐位一致");
        }

        [Test]
        public void test_xpGain_bitExact_staleMultiplied()
        {
            // 处方用药 BASE=6, K_difficulty=1.0, K_novelty=Stale=0.2
            // 期望 = 6 × 1.0 × 0.2 = 1.2
            // 0.2 raw = 2*OneRaw/10 = 13107, 6 * 13107 // OneRaw = 78642
            Assert.That(6L * (2L * Fix.OneRaw / 10), Is.EqualTo(78642L),
                "1.2 raw 预校验: 6*13107//65536 = 78642");

            Fix xpGain = XpGainCalculator.ComputeXpGain(
                (int)SkillId.处方用药, NoveltyClass.Stale, new Fix(Fix.OneRaw));

            Assert.That(xpGain.Raw, Is.EqualTo(6L * (2L * Fix.OneRaw / 10)),
                "处方用药 Stale = 6 × 0.2 = 1.2, raw = 78642 逐位一致");
        }

        // ════════════════ 异常输入 ════════════════

        [Test]
        public void test_xpGain_invalidNoveltyClass_throws()
        {
            Fix difficulty = new Fix(Fix.OneRaw);
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                XpGainCalculator.ComputeXpGain(
                    (int)SkillId.诊断, (NoveltyClass)99, difficulty));
        }

        // ════════════════ GAP 补充: 边界与防御性 ════════════════

        /// <summary>GAP: 同一技能三档新颖度系数一次性验证(只变 noveltyCoeff)。</summary>
        [Test]
        public void test_xpGain_sameSkill_allNoveltyTiers_correctCoefficients()
        {
            Fix difficulty = new Fix(Fix.OneRaw); // 1.0

            // 诊断 BASE=8 × 1.0 × First(3.0) = 24
            Fix first = XpGainCalculator.ComputeXpGain(
                (int)SkillId.诊断, NoveltyClass.First, difficulty);
            // 诊断 BASE=8 × 1.0 × Normal(1.0) = 8
            Fix normal = XpGainCalculator.ComputeXpGain(
                (int)SkillId.诊断, NoveltyClass.Normal, difficulty);
            // 诊断 BASE=8 × 1.0 × Stale(0.2) = 1.6
            Fix stale = XpGainCalculator.ComputeXpGain(
                (int)SkillId.诊断, NoveltyClass.Stale, difficulty);

            Assert.That(first.Raw, Is.EqualTo(24L * Fix.OneRaw), "First = 24");
            Assert.That(normal.Raw, Is.EqualTo(8L * Fix.OneRaw), "Normal = 8");
            Assert.That(stale.Raw, Is.EqualTo(8L * (2L * Fix.OneRaw / 10)), "Stale = 1.6");
        }

        /// <summary>GAP: 双系数同时取最大值(BASE × 3.0 × 3.0)。</summary>
        [Test]
        public void test_xpGain_maxFactors_difficulty3First3_returnsMaxXp()
        {
            Fix difficulty = new Fix(3L * Fix.OneRaw / 1); // 3.0
            Fix xpGain = XpGainCalculator.ComputeXpGain(
                (int)SkillId.诊断, NoveltyClass.First, difficulty);

            // 8 × 3.0 × 3.0 = 72
            Assert.That(xpGain.Raw, Is.EqualTo(72L * Fix.OneRaw),
                "诊断 ×3.0 难度 ×3.0 首次 = 72 XP");
        }

        /// <summary>GAP: NoveltyClass ordinal = -1(负值) → 抛 ArgumentOutOfRangeException。</summary>
        [Test]
        public void test_xpGain_noveltyClassMinusOne_throws()
        {
            Fix difficulty = new Fix(Fix.OneRaw);
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                XpGainCalculator.ComputeXpGain(
                    (int)SkillId.诊断, (NoveltyClass)(-1), difficulty));
        }
    }
}
