// combat-weapons Story 003 测试
//
// AC-25-1-01: 整数边界
// AC-25-1-02: 友伤白名单
// AC-25-1-03: 解锁判定
// AC-25-1-04: Down 查询
// AC-25-1-08: 次序半边

using System;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Combat
{
    public class CombatHitResolutionTest
    {
        // AC-25-1-01: 整数边界
        [Test]
        public void test_d2AtRangeBoundary_hits()
        {
            var input = new HitDeterminationInput(
                1, 2,
                new WorldPos(0, 0, 0), new WorldPos(3, 0, 0),
                3, 1, 10,
                false, false, false);

            var result = CombatHitResolution.Determine(input);
            Assert.IsTrue(result.IsInRange, "d2 = RANGE² 应命中");
            Assert.IsTrue(result.IsHit, "界内应命中");
        }

        // AC-25-1-01: 界外
        [Test]
        public void test_d2BeyondRange_misses()
        {
            var input = new HitDeterminationInput(
                1, 2,
                new WorldPos(0, 0, 0), new WorldPos(4, 0, 0),
                3, 1, 10,
                false, false, false);

            var result = CombatHitResolution.Determine(input);
            Assert.IsFalse(result.IsInRange, "d2 > RANGE² 应落空");
            Assert.IsFalse(result.IsHit, "界外应落空");
        }

        // AC-25-1-02: 友伤白名单
        [Test]
        public void test_friendlyTarget_blocked()
        {
            var input = new HitDeterminationInput(
                1, 2,
                new WorldPos(0, 0, 0), new WorldPos(1, 0, 0),
                3, 1, 10,
                true, // 友方
                false, false);

            var result = CombatHitResolution.Determine(input);
            Assert.IsFalse(result.IsTargetAllowed, "友方目标应被白名单拦截");
            Assert.IsFalse(result.IsHit, "友方目标不应命中");
        }

        // AC-25-1-03: 解锁判定
        [Test]
        public void test_unlockLevelNotMet_rejected()
        {
            var input = new HitDeterminationInput(
                1, 2,
                new WorldPos(0, 0, 0), new WorldPos(1, 0, 0),
                3, 10, 5, // unlock_level=10 > skill=5
                false, false, false);

            var result = CombatHitResolution.Determine(input);
            Assert.IsFalse(result.IsUnlocked, "解锁未达应拒绝");
            Assert.IsFalse(result.IsHit, "解锁未达不应命中");
        }

        // AC-25-1-03: 恰解锁
        [Test]
        public void test_unlockLevelExactlyMet_allowed()
        {
            var input = new HitDeterminationInput(
                1, 2,
                new WorldPos(0, 0, 0), new WorldPos(1, 0, 0),
                3, 5, 5, // unlock_level=5 == skill=5
                false, false, false);

            var result = CombatHitResolution.Determine(input);
            Assert.IsTrue(result.IsUnlocked, "恰解锁应允许");
        }

        // AC-25-1-04: Down 查询
        [Test]
        public void test_actorDown_cannotAttack()
        {
            var input = new HitDeterminationInput(
                1, 2,
                new WorldPos(0, 0, 0), new WorldPos(1, 0, 0),
                3, 1, 10,
                false, true, false); // actor Down

            var result = CombatHitResolution.Determine(input);
            Assert.IsFalse(result.IsActorNotDown, "Down 的 actor 不应出手");
            Assert.IsFalse(result.IsHit, "Down 不应命中");
        }

        // AC-25-1-04: 目标 Down
        [Test]
        public void test_targetDown_cannotBeHit()
        {
            var input = new HitDeterminationInput(
                1, 2,
                new WorldPos(0, 0, 0), new WorldPos(1, 0, 0),
                3, 1, 10,
                false, false, true); // target Down

            var result = CombatHitResolution.Determine(input);
            Assert.IsFalse(result.IsTargetNotDown, "Down 的目标不应被命中");
            Assert.IsFalse(result.IsHit, "Down 目标不应命中");
        }

        // AC-25-1-08: 次序半边
        [Test]
        public void test_integerDomain_noFloat()
        {
            Assert.IsTrue(CombatHitResolution.ValidateIntegerDomain(),
                "判定输入应全为整数域");
        }
    }
}
