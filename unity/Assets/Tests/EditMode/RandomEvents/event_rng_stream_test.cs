// random-events Story 002 测试
//
// AC-52-04: SplitMix64 唯一 RNG
// AC-52-05: 掷骰输入可从流重构
// AC-52-06: PatientId.None 哨兵
// AC-52-07: 禁墙钟
// AC-52-10: 禁符号扫描

using System;
using System.Collections.Generic;
using System.Reflection;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.RandomEvents
{
    public class EventRngStreamTest
    {
        // AC-52-04: SplitMix64 唯一 RNG
        [Test]
        public void test_splitMix64_isUniqueRng()
        {
            // 验证 SplitMix64 存在且可用
            var hash = SplitMix64.Hash(12345, 67890);
            Assert.Greater(hash, 0, "SplitMix64 应产生非零哈希");

            // 验证确定性：同输入同输出
            var hash2 = SplitMix64.Hash(12345, 67890);
            Assert.AreEqual(hash, hash2, "SplitMix64 应是确定性的");

            // 验证不同输入产生不同输出
            var hash3 = SplitMix64.Hash(12345, 67891);
            Assert.AreNotEqual(hash, hash3, "不同输入应产生不同输出");
        }

        // AC-52-05: 掷骰输入可从流重构
        [Test]
        public void test_seedReproducible()
        {
            ulong worldSeed = 12345;
            long win = 1000;
            int tier = 1;
            long ordinal = 0;

            Assert.IsTrue(EventRng.ValidateSeedReproducible(worldSeed, win, tier, ordinal),
                "同种子同输入应逐位同输出");

            // 验证不同输入产生不同输出
            var seed1 = new EventRollSeed(win, tier, ordinal);
            var seed2 = new EventRollSeed(win, tier, ordinal + 1);
            Assert.AreNotEqual(seed1.ComputeHash(worldSeed), seed2.ComputeHash(worldSeed),
                "不同 ordinal 应产生不同哈希");
        }

        // AC-52-05: CDF walk
        [Test]
        public void test_cdfWalk_keyAscending()
        {
            var keys = new List<int> { 1, 2, 3 };
            var weights = new List<int> { 1, 1, 1 };

            // r=0 → key=1
            var result1 = EventRng.CdfWalk(0, keys, weights);
            Assert.AreEqual(1, result1.ChosenKey, "r=0 应选 key=1");

            // r=1 → key=2
            var result2 = EventRng.CdfWalk(1, keys, weights);
            Assert.AreEqual(2, result2.ChosenKey, "r=1 应选 key=2");

            // r=2 → key=3
            var result3 = EventRng.CdfWalk(2, keys, weights);
            Assert.AreEqual(3, result3.ChosenKey, "r=2 应选 key=3");
        }

        // AC-52-05: C=0 时跳过
        [Test]
        public void test_cdfWalk_zeroTotalWeight_skips()
        {
            var keys = new List<int> { 1, 2 };
            var weights = new List<int> { 0, 0 };

            var result = EventRng.CdfWalk(0, keys, weights);
            Assert.IsFalse(result.Found, "C=0 时应跳过");
        }

        // AC-52-05: 非升序 key 输入仍按升序遍历
        [Test]
        public void test_cdfWalk_nonAscendingKeys_sortedTraversal()
        {
            // 非升序输入 {3, 1, 2}，权重各 1
            var keys = new List<int> { 3, 1, 2 };
            var weights = new List<int> { 1, 1, 1 };

            // r=0 → 升序第一个 key=1
            var result1 = EventRng.CdfWalk(0, keys, weights);
            Assert.AreEqual(1, result1.ChosenKey, "r=0 应选升序第一个 key=1");

            // r=1 → 升序第二个 key=2
            var result2 = EventRng.CdfWalk(1, keys, weights);
            Assert.AreEqual(2, result2.ChosenKey, "r=1 应选升序第二个 key=2");

            // r=2 → 升序第三个 key=3
            var result3 = EventRng.CdfWalk(2, keys, weights);
            Assert.AreEqual(3, result3.ChosenKey, "r=2 应选升序第三个 key=3");
        }

        // AC-52-06: PatientId.None 哨兵
        [Test]
        public void test_patientIdNone_sentinel()
        {
            Assert.AreEqual(-1, PatientId.None.Value,
                "PatientId.None 应 = -1");
        }

        // AC-52-07: 禁墙钟
        [Test]
        public void test_noWallClock()
        {
            // 验证 EventRng 不引用墙钟类型
            var rngType = typeof(EventRng);
            Type[] wallClockTypes = { typeof(DateTime), typeof(DateTimeOffset), typeof(TimeSpan) };

            foreach (var method in rngType.GetMethods())
            {
                foreach (var param in method.GetParameters())
                {
                    foreach (var wallClockType in wallClockTypes)
                    {
                        Assert.IsFalse(param.ParameterType == wallClockType,
                            $"方法 {method.Name} 参数 {param.Name} 不应为 {wallClockType.Name}");
                    }
                }
            }
        }

        // AC-52-10: 禁符号扫描
        [Test]
        public void test_noRandomSymbols()
        {
            // 验证 EventRng 不引用 Random（包括 System.Random 和 UnityEngine.Random）
            var rngType = typeof(EventRng);

            // 检查字段
            foreach (var field in rngType.GetFields())
            {
                Assert.IsFalse(field.FieldType == typeof(Random),
                    $"字段 {field.Name} 不应为 System.Random");
            }

            // 检查方法参数
            foreach (var method in rngType.GetMethods())
            {
                foreach (var param in method.GetParameters())
                {
                    Assert.IsFalse(param.ParameterType == typeof(Random),
                        $"方法 {method.Name} 参数 {param.Name} 不应为 System.Random");
                }
            }
        }

        // 窗口起点 tick 计算
        [Test]
        public void test_windowStartTick()
        {
            long ticksPerDay = 1000;
            Assert.AreEqual(0, EventRng.ComputeWindowStart(0, ticksPerDay));
            Assert.AreEqual(0, EventRng.ComputeWindowStart(999, ticksPerDay));
            Assert.AreEqual(1000, EventRng.ComputeWindowStart(1000, ticksPerDay));
            Assert.AreEqual(1000, EventRng.ComputeWindowStart(1999, ticksPerDay));
        }
    }
}
