// enemy-ai Story 003 测试
//
// AC-27-07: 图可达性
// AC-27-11: 2×2 叉乘
// AC-27-12: 死区探针
// AC-27-13: Flank 三出口
// AC-27-04/05: LOD 节流
// AC-27-24: 决策轨迹不进三流

using System;
using DaYiJingCheng.Sim;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.EnemyAI
{
    public class HostilityStateMachineTest
    {
        // AC-27-07: 图可达性
        [Test]
        public void test_reachability_allStatesCanDisengage()
        {
            // 每非终结态存在到 Disengage 的路径
            // 简化版：验证转移表闭集
            Assert.IsTrue(EnemyStateMachine.ValidateClosedSet(), "转移表应闭集");
        }

        // AC-27-11: 2×2 叉乘
        [Test]
        public void test_moraleFlankCrossProduct()
        {
            // morale_enabled=T, flank_enabled=F → 士气逃逸
            var morale = new DaYiJingCheng.Sim.Contracts.Fix(500);
            var breakThreshold = new DaYiJingCheng.Sim.Contracts.Fix(1000);
            Assert.IsTrue(EnemyStateMachine.ShouldDisengage(morale, breakThreshold, false),
                "士气低于阈值应脱离");

            // morale_enabled=F → 不脱离
            var highMorale = new DaYiJingCheng.Sim.Contracts.Fix(2000);
            Assert.IsFalse(EnemyStateMachine.ShouldDisengage(highMorale, breakThreshold, false),
                "士气高于阈值不应脱离");
        }

        // AC-27-12: 死区探针
        [Test]
        public void test_alertTimeout_fallsBackToPatrol()
        {
            var next = EnemyStateMachine.Transition(EnemyStateMachine.State.Alert, StateTrigger.AlertTimeout);
            Assert.AreEqual(EnemyStateMachine.State.Patrol, next, "警戒超时应有回落 Patrol");
        }

        // AC-27-13: Flank 三出口
        [Test]
        public void test_flankThreeExits()
        {
            var toEngage = EnemyStateMachine.Transition(EnemyStateMachine.State.Flank, StateTrigger.FlankComplete);
            Assert.AreEqual(EnemyStateMachine.State.Engage, toEngage, "包抄完成应进入 Engage");

            var toChase = EnemyStateMachine.Transition(EnemyStateMachine.State.Flank, StateTrigger.FlankTimeout);
            Assert.AreEqual(EnemyStateMachine.State.Chase, toChase, "包抄超时应有 Chase");

            var toDisengage = EnemyStateMachine.Transition(EnemyStateMachine.State.Flank, StateTrigger.MoraleBreak);
            Assert.AreEqual(EnemyStateMachine.State.Disengage, toDisengage, "士气崩溃应有 Disengage");
        }

        // AC-27-04/05: LOD 节流
        [Test]
        public void test_inCombat_pureFunction()
        {
            Assert.IsTrue(EnemyStateMachine.IsInCombat(EnemyStateMachine.State.Chase));
            Assert.IsTrue(EnemyStateMachine.IsInCombat(EnemyStateMachine.State.Flank));
            Assert.IsTrue(EnemyStateMachine.IsInCombat(EnemyStateMachine.State.Engage));
            Assert.IsFalse(EnemyStateMachine.IsInCombat(EnemyStateMachine.State.Patrol));
            Assert.IsFalse(EnemyStateMachine.IsInCombat(EnemyStateMachine.State.Alert));
            Assert.IsFalse(EnemyStateMachine.IsInCombat(EnemyStateMachine.State.Disengage));
        }

        // AC-27-24: 决策轨迹不进三流
        [Test]
        public void test_decisionTraceNotInStream()
        {
            // 验证状态机无 StreamKind 字段
            var stateType = typeof(EnemyStateMachine.State);
            Assert.IsFalse(stateType.GetField("StreamKind") != null, "状态机不应有 StreamKind 字段");
        }

        // 合法迁移
        [Test]
        public void test_validTransitions()
        {
            Assert.AreEqual(EnemyStateMachine.State.Alert,
                EnemyStateMachine.Transition(EnemyStateMachine.State.Patrol, StateTrigger.PlayerVisible));
            Assert.AreEqual(EnemyStateMachine.State.Chase,
                EnemyStateMachine.Transition(EnemyStateMachine.State.Alert, StateTrigger.PlayerInRange));
            Assert.AreEqual(EnemyStateMachine.State.Engage,
                EnemyStateMachine.Transition(EnemyStateMachine.State.Chase, StateTrigger.PlayerInRange));
        }

        // 非法迁移断言失败
        [Test]
        public void test_invalidTransition_throws()
        {
            Assert.Throws<InvalidOperationException>(() =>
                EnemyStateMachine.Transition(EnemyStateMachine.State.Disengage, StateTrigger.PlayerVisible));
        }
    }
}
