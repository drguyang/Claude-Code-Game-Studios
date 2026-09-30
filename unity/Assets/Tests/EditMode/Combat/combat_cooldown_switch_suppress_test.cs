// combat-weapons Story 005 测试
//
// AC-25-3-02/03: 落空照常耗冷却
// AC-25-3-06/07: 切换冷却
// AC-25-4-01/02: 压制 max 刷新
// AC-25-4-03: 压制不锁攻击
// AC-25-4-04: 压制零入流

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Combat
{
    public class CombatCooldownSwitchSuppressTest
    {
        // AC-25-3-02/03: 落空照常耗冷却
        [Test]
        public void test_whiffStillConsumesCooldown()
        {
            long lastAttack = 100;
            int cooldown = 4;

            // cooldown 窗口内
            Assert.IsFalse(CombatCooldown.IsCooldownComplete(lastAttack, 101, cooldown), "cooldown 窗口内应未完成");
            // cooldown 释放
            Assert.IsTrue(CombatCooldown.IsCooldownComplete(lastAttack, 104, cooldown), "cooldown 释放后应完成");
        }

        // AC-25-3-06/07: 切换冷却
        [Test]
        public void test_switchCooldown()
        {
            long lastSwitch = 100;
            int switchCooldown = 2;

            Assert.IsFalse(CombatCooldown.IsSwitchCooldownComplete(lastSwitch, 101, switchCooldown));
            Assert.IsTrue(CombatCooldown.IsSwitchCooldownComplete(lastSwitch, 102, switchCooldown));
        }

        // AC-25-4-01/02: 压制 max 刷新
        [Test]
        public void test_suppressionMaxRefresh()
        {
            long onset1 = 100;
            long duration = 10;
            long existingUntil = 110;

            long newUntil = CombatCooldown.ComputeSuppressedUntil(onset1, duration, existingUntil);
            Assert.AreEqual(110, newUntil, "max(110, 110) = 110");

            // 第二次压制延长
            long onset2 = 103;
            long newUntil2 = CombatCooldown.ComputeSuppressedUntil(onset2, duration, newUntil);
            Assert.AreEqual(113, newUntil2, "max(113, 110) = 113");
        }

        // AC-25-4-03: 压制不锁攻击
        [Test]
        public void test_suppressionDoesNotLockAttack()
        {
            var suppressions = new List<SuppressionState>
            {
                new SuppressionState(1, 100, 110, false)
            };

            // 被压制者仍可出手（压制锁移动不锁攻击）
            bool isSuppressed = CombatCooldown.IsSuppressed(1, 105, suppressions);
            Assert.IsTrue(isSuppressed, "t=105 应被压制");
            // 但压制不锁攻击（由占用门拦）
        }

        // AC-25-4-04: 压制零入流
        [Test]
        public void test_suppressionZeroInStream()
        {
            Assert.IsTrue(CombatCooldown.ValidateNoSuppressionInStream(),
                "压制态不应有 Stream/Kind 字段");
        }

        // 压制窗口查询
        [Test]
        public void test_suppressionWindowQuery()
        {
            var suppressions = new List<SuppressionState>
            {
                new SuppressionState(1, 100, 110, false)
            };

            Assert.IsTrue(CombatCooldown.IsSuppressed(1, 100, suppressions), "t=100 应被压制");
            Assert.IsTrue(CombatCooldown.IsSuppressed(1, 109, suppressions), "t=109 应被压制");
            Assert.IsFalse(CombatCooldown.IsSuppressed(1, 110, suppressions), "t=110 应释放");
            Assert.IsFalse(CombatCooldown.IsSuppressed(2, 105, suppressions), "其他 actor 不应被压制");
        }

        // Down 提前释放
        [Test]
        public void test_downEarlyRelease()
        {
            var suppressions = new List<SuppressionState>
            {
                new SuppressionState(1, 100, 110, true) // IsDown = true
            };

            Assert.IsFalse(CombatCooldown.IsSuppressed(1, 105, suppressions),
                "Down 应提前释放压制");
        }
    }
}
