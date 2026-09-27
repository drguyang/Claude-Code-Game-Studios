// ============================================================================
// 死亡掉级 EditMode 单测 —— Story 005 验收
// 权威来源: production/epics/skill-system/story-005-death-penalty.md
//   · AC-1 ~ AC-5(定点求值 / 允许掉到 0 / SkillGrown 触发语义)
//   · ADR-026 §Decision 四(调参表 Fix 字段) + Implementation Guidelines
//   · ADR-006 §Decision 三(ROUND_HALF_AWAY_FROM_ZERO —— 乘法后 floor = 截断)
// ============================================================================
// 落点:unity/Assets/Tests/EditMode/SkillSystem/death_penalty_test.cs
// ============================================================================
// 测试策略:全部用 int 断言等级结果,辅以 Raw 值验证定点中间结果。
// 零随机种子、零时间依赖、零外部 I/O。
// ============================================================================

using System;
using NUnit.Framework;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.Contracts.SkillSystem;

namespace DaYiJingCheng.Tests.Unit.SkillSystem
{
    /// <summary>Story 005 死亡掉级的 EditMode 单测。</summary>
    [TestFixture]
    internal sealed class DeathPenaltyTest
    {
        /// <summary>AC-1/AC-2 主例: 60 × 0.95(Q16.16 raw=62259) = floor(56.999) = 56。</summary>
        [Test]
        public void test_ac1_level60_returns56()
        {
            int result = DeathPenalty.Apply(60, SkillTuningTable.Default.DeathLoss);
            Assert.That(result, Is.EqualTo(56),
                "Apply(60, 0.95) = floor(56.999) = 56(Q16.16 截断)");
        }

        /// <summary>AC-1/AC-2 边缘: 40 × 0.95(Q16.16 raw=62259) = floor(37.999) = 37。</summary>
        [Test]
        public void test_ac1_level40_returns37()
        {
            int result = DeathPenalty.Apply(40, SkillTuningTable.Default.DeathLoss);
            Assert.That(result, Is.EqualTo(37),
                "Apply(40, 0.95) = floor(37.999) = 37(Q16.16 截断)");
        }

        /// <summary>AC-1/AC-2 边缘: 20 × 0.95(Q16.16 raw=62259) = floor(18.999) = 18。</summary>
        [Test]
        public void test_ac1_level20_returns18()
        {
            int result = DeathPenalty.Apply(20, SkillTuningTable.Default.DeathLoss);
            Assert.That(result, Is.EqualTo(18),
                "Apply(20, 0.95) = floor(18.999) = 18(Q16.16 截断)");
        }

        // ═══════════════════════════════════════════════════════════════════
        // AC-3: 允许掉到 0(不设下限保护)
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>AC-3 主例: 1 × 0.95 = 0.95 → floor = 0(允许掉到 0)。</summary>
        [Test]
        public void test_ac3_level1_returns0()
        {
            int result = DeathPenalty.Apply(1, SkillTuningTable.Default.DeathLoss);
            Assert.That(result, Is.EqualTo(0),
                "Apply(1, 0.95) = floor(0.95) = 0(允许掉到 0)");
        }

        /// <summary>AC-3 边缘: 0 × 0.95 = 0.0 → floor = 0(0 级再死仍是 0)。</summary>
        [Test]
        public void test_ac3_level0_returns0()
        {
            int result = DeathPenalty.Apply(0, SkillTuningTable.Default.DeathLoss);
            Assert.That(result, Is.EqualTo(0),
                "Apply(0, 0.95) = floor(0.0) = 0");
        }

        // ═══════════════════════════════════════════════════════════════════
        // AC-4: 定点中间值验证(floor 截断,非舍入)
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>AC-4: 10 × 0.95 = 9.5 → floor = 9(截断,非 away-from-zero)。</summary>
        [Test]
        public void test_ac4_level10_returns9_floorTruncation()
        {
            int result = DeathPenalty.Apply(10, SkillTuningTable.Default.DeathLoss);
            Assert.That(result, Is.EqualTo(9),
                "Apply(10, 0.95) = floor(9.5) = 9(截断)");
        }

        /// <summary>AC-4: 19 × 0.95 = 18.05 → floor = 18。</summary>
        [Test]
        public void test_ac4_level19_returns18()
        {
            int result = DeathPenalty.Apply(19, SkillTuningTable.Default.DeathLoss);
            Assert.That(result, Is.EqualTo(18),
                "Apply(19, 0.95) = floor(18.05) = 18");
        }

        // ═══════════════════════════════════════════════════════════════════
        // 异常输入验证
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>负等级 → ArgumentOutOfRangeException。</summary>
        [Test]
        public void test_negativeLevel_throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                DeathPenalty.Apply(-1, SkillTuningTable.Default.DeathLoss);
            }, "currentLevel 为负须抛 ArgumentOutOfRangeException");
        }

        // ═══════════════════════════════════════════════════════════════════
        // DEATH_LOSS 常量验证(调参表默认值)
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>默认 DEATH_LOSS = FixParse("19/20") = Fix(0.95)。</summary>
        [Test]
        public void test_defaultDeathLoss_is095()
        {
            Fix expected = Fix.FromRational(19L, 20L);
            Assert.That(SkillTuningTable.Default.DeathLoss.Raw, Is.EqualTo(expected.Raw),
                "Default.DeathLoss = FixParse('19/20') = 0.95");
        }
    }
}
