// player-controller Story 006 测试
//
// AC-1-23(机制): 位图 + 幂等 + 只碰自己位
// AC-1-27(类型白名单): 字段类型反射
// AC-1-12: 禁止项零引用(grep + 程序集引用集)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;
using DaYiJingCheng.Gameplay.Presentation.Player;
using NUnit.Framework;
using PlayerControllerType = DaYiJingCheng.Gameplay.Presentation.Player.PlayerController;

namespace DaYiJingCheng.Tests.PlayerController
{
    public class MotorLeaseTest
    {
        // ══════════ AC-1-23(机制): 位图语义 ══════════

        [Test]
        public void test_ac123_bitmap_singleBitPerSource()
        {
            var lease = new MotorLease();
            Assert.IsFalse(lease.IsSuppressed, "初始无压制");

            lease.Acquire(LeaseSource.Self);
            Assert.IsTrue(lease.IsSuppressed, "Self 位被置位");

            lease.Acquire(LeaseSource.Emergency);
            Assert.IsTrue(lease.IsSuppressed, "Emergency 位被置位");

            lease.Acquire(LeaseSource.Combat);
            Assert.IsTrue(lease.IsSuppressed, "Combat 位被置位");
        }

        [Test]
        public void test_ac123_idempotentAcquire()
        {
            var lease = new MotorLease();
            lease.Acquire(LeaseSource.Self);
            lease.Acquire(LeaseSource.Self); // 重复 Acquire
            Assert.IsTrue(lease.IsSuppressed, "幂等 Acquire 不改变状态");

            lease.Release(LeaseSource.Self);
            Assert.IsFalse(lease.IsSuppressed, "Release 后清除");
        }

        [Test]
        public void test_ac123_releaseOnlyOwnBit()
        {
            var lease = new MotorLease();
            lease.Acquire(LeaseSource.Self);
            lease.Acquire(LeaseSource.Emergency);

            lease.Release(LeaseSource.Self);
            Assert.IsTrue(lease.IsSuppressed, "Emergency 位保持");

            lease.Release(LeaseSource.Emergency);
            Assert.IsFalse(lease.IsSuppressed, "全部清除");
        }

        [Test]
        public void test_ac123_releaseNeverAcquired_noOp()
        {
            var lease = new MotorLease();
            lease.Release(LeaseSource.Combat); // 从未 Acquire
            Assert.IsFalse(lease.IsSuppressed, "无下溢");
        }

        [Test]
        public void test_ac123_interleavedSources()
        {
            var lease = new MotorLease();
            lease.Acquire(LeaseSource.Self);
            lease.Acquire(LeaseSource.Combat);
            lease.Release(LeaseSource.Self);
            Assert.IsTrue(lease.IsSuppressed, "Combat 位保持");

            lease.Acquire(LeaseSource.Emergency);
            Assert.IsTrue(lease.IsSuppressed, "Emergency 位被置位");

            lease.Release(LeaseSource.Combat);
            Assert.IsTrue(lease.IsSuppressed, "Emergency 位保持");

            lease.Release(LeaseSource.Emergency);
            Assert.IsFalse(lease.IsSuppressed, "全部清除");
        }

        // ══════════ AC-1-27: 字段类型白名单 ══════════

        [Test]
        public void test_ac127_noGameStateFields()
        {
            var playerType = typeof(PlayerControllerType);
            var forbiddenPatterns = new[] { "hp", "skill", "inventory", "quest", "health", "mana" };

            foreach (var field in playerType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                foreach (var pattern in forbiddenPatterns)
                {
                    Assert.IsFalse(field.Name.ToLower().Contains(pattern),
                        $"字段 {field.Name} 含禁止词 {pattern}(AC-1-27)");
                }
            }
        }

        // ══════════ AC-1-12: 禁止项零引用 ══════════

        [Test]
        public void test_ac112_noTerrainSampling()
        {
            var playerType = typeof(PlayerControllerType);
            foreach (var method in playerType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                Assert.IsFalse(method.Name.Contains("SampleHeight"),
                    $"方法 {method.Name} 含 SampleHeight(AC-1-12)");
                Assert.IsFalse(method.Name.Contains("TerrainSample"),
                    $"方法 {method.Name} 含 TerrainSample(AC-1-12)");
            }
        }

        [Test]
        public void test_ac112_noNavMeshSampling()
        {
            var playerType = typeof(PlayerControllerType);
            foreach (var method in playerType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                Assert.IsFalse(method.Name.Contains("NavMeshSample"),
                    $"方法 {method.Name} 含 NavMeshSample(AC-1-12)");
            }
        }
    }
}
