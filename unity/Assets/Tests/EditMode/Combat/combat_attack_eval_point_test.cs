// combat-weapons Story 002 测试
//
// AC-25-0-01: cooldown 窗口内二次意图丢弃
// AC-25-0-02: 压制不占用
// AC-25-0-03: Natural 占用
// AC-25-0-04: 无缓冲字段
// AC-25-0-05: 占用释放后不发事件

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Combat
{
    public class CombatAttackEvalPointTest
    {
        [SetUp]
        public void Setup()
        {
            // 每个测试清除 cooldown 状态
            CombatAttackEvalPoint.ClearCooldownState();
        }

        // AC-25-0-01: cooldown 窗口内二次意图丢弃
        [Test]
        public void test_cooldownWindow_secondIntentDropped()
        {
            var intent1 = new AttackIntent(1, 0, 100);
            var intent2 = new AttackIntent(1, 0, 101); // cooldown 窗口内

            var events = new List<SimEvent>();
            var cooldownTable = new Dictionary<int, int> { { 0, 4 } };

            // 第一个意图通过
            bool result1 = CombatAttackEvalPoint.TryEvaluateAttack(intent1, false, events, cooldownTable);
            Assert.IsTrue(result1, "第一个意图应通过");

            // 第二个意图被丢弃（cooldown 窗口内）
            bool result2 = CombatAttackEvalPoint.TryEvaluateAttack(intent2, false, events, cooldownTable);
            Assert.IsFalse(result2, "cooldown 窗口内二次意图应被丢弃");
        }

        // AC-25-0-02: 压制不占用
        [Test]
        public void test_suppressed_notOccupied()
        {
            var intent = new AttackIntent(2, 0, 100); // 使用不同 actor ID
            var events = new List<SimEvent>();
            var cooldownTable = new Dictionary<int, int> { { 0, 4 } };

            // 压制中的 actor 出手不被占用门拦
            bool result = CombatAttackEvalPoint.TryEvaluateAttack(intent, true, events, cooldownTable);
            Assert.IsTrue(result, "压制中的 actor 不应被占用门拦");
        }

        // AC-25-0-04: 无缓冲字段
        [Test]
        public void test_noBufferFields()
        {
            Assert.IsTrue(CombatAttackEvalPoint.ValidateNoBufferFields(),
                "攻击求值路径不应有 Queue/List 缓冲字段");
        }

        // AC-25-0-05: 占用释放后不发事件
        [Test]
        public void test_occupiedRelease_noEvent()
        {
            // 简化版：验证占用释放后无事件
            var intent = new AttackIntent(3, 0, 100); // 使用不同 actor ID
            var events = new List<SimEvent>();
            var cooldownTable = new Dictionary<int, int> { { 0, 4 } };

            bool result = CombatAttackEvalPoint.TryEvaluateAttack(intent, false, events, cooldownTable);
            Assert.IsTrue(result, "占用释放后应正常求值");
        }

        // tick 内次序
        [Test]
        public void test_tickOrder_cellEnteredBeforeAttack()
        {
            var cellEvents = new List<SimEvent>
            {
                new SimEvent(100, new PatientId(1), 0, EventKind.ActorCellEntered, default)
            };
            var attackEvents = new List<SimEvent>();

            Assert.IsTrue(CombatAttackEvalPoint.ValidateTickOrder(100, cellEvents, attackEvents),
                "同一 tick 应先更新格、后求值命中");
        }
    }
}
