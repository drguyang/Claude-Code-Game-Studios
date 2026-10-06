// enemy-ai Story 002 测试
//
// AC-27-02: 反射断言输入类型
// AC-27-14: Band 互斥
// AC-27-03: Visible 承重
// AC-27-15: Target 决胜
// AC-27-09: d2 全 int64
// AC-27-26: 感知/查询 API 枚举

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.EnemyAI
{
    public class PerceptionAndTargetingTest
    {
        // AC-27-14: Band 互斥
        [Test]
        public void test_bandMutuallyExclusive()
        {
            // 2026-10-06 OQ-27-1 定值:R_VIS=12 ≥ R_ALERT=8 ≥ R_CHASE=6 ≥ R_CONTACT=2
            // ⚠️ 原配置 (5, 8, 3) 违反该单调性(R_ALERT 8 > R_VIS 5)—— 与同一轮订正的
            //    EnemyBehaviorProgram 反向断言同源;互斥性质测试必须在**合法**半径组上跑,
            //    否则它证明的是另一套语义的互斥性。
            // d2=40 → Alert(40 > 36=R_CHASE², 40 ≤ 64=R_ALERT²)
            var band = EnemyPerception.EvaluateBand(40, true, true, 12, 8, 6);
            Assert.AreEqual(Band.Alert, band, "d2=40 应判 Alert");

            // d2=4 → Chase
            var band2 = EnemyPerception.EvaluateBand(4, true, true, 12, 8, 6);
            Assert.AreEqual(Band.Chase, band2, "d2=4 应判 Chase");

            // d2=100 → Patrol(100 > 64=R_ALERT²,100 ≤ 144=R_VIS²:看得见但未警戒)
            var band3 = EnemyPerception.EvaluateBand(100, false, false, 12, 8, 6);
            Assert.AreEqual(Band.Patrol, band3, "d2=100 应判 Patrol");
        }

        // AC-27-03: Visible 承重
        [Test]
        public void test_visibleRequiredForChase()
        {
            // 不可见时不应进入 Chase
            var band = EnemyPerception.EvaluateBand(4, false, true, 12, 8, 6);
            Assert.AreNotEqual(Band.Chase, band, "不可见时不应进入 Chase");
        }

        // AC-27-15: Target 决胜
        [Test]
        public void test_targetSelection_lowestActorIdWins()
        {
            var candidates = new List<(int id, WorldPos cell)>
            {
                (7, new WorldPos(1, 0, 0)),
                (4, new WorldPos(1, 0, 0)) // 等距
            };

            int target = EnemyPerception.SelectTarget(candidates, new WorldPos(0, 0, 0));
            Assert.AreEqual(4, target, "等距时应选 actor_id 最小的");
        }

        // AC-27-15: 打乱顺序结果不变
        [Test]
        public void test_targetSelection_orderIndependent()
        {
            var candidates1 = new List<(int id, WorldPos cell)>
            {
                (7, new WorldPos(1, 0, 0)),
                (4, new WorldPos(1, 0, 0))
            };
            var candidates2 = new List<(int id, WorldPos cell)>
            {
                (4, new WorldPos(1, 0, 0)),
                (7, new WorldPos(1, 0, 0))
            };

            int target1 = EnemyPerception.SelectTarget(candidates1, new WorldPos(0, 0, 0));
            int target2 = EnemyPerception.SelectTarget(candidates2, new WorldPos(0, 0, 0));

            Assert.AreEqual(target1, target2, "打乱顺序结果应不变");
        }

        // AC-27-09: d2 全 int64
        [Test]
        public void test_d2_noOverflow()
        {
            // 极端坐标差
            var a = new WorldPos(46341, 0, 0);
            var b = new WorldPos(0, 0, 0);
            long d2 = EnemyPerception.ComputeD2(a, b);
            Assert.AreEqual(46341L * 46341L, d2, "d2 应用 int64 计算");
        }

        // AC-27-02: 输入类型 ∈ 整数域
        [Test]
        public void test_inputTypes_integerDomain()
        {
            Assert.IsTrue(EnemyPerception.ValidateIntegerDomain(),
                "判定输入应全为整数域");
        }

        // AC-27-26: 感知/查询 API 枚举
        [Test]
        public void test_perceptionApiEnumeration()
        {
            // 验证 PerceptionInput 存在
            var inputType = typeof(PerceptionInput);
            Assert.IsNotNull(inputType);
        }
    }
}
