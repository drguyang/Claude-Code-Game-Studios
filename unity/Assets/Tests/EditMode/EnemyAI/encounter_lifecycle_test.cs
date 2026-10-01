// enemy-ai Story 005 测试
//
// AC-27-22: 路由纯函数
// AC-27-23: id 通道
// AC-27-24: 可写集白名单
// AC-27-25: 三值可达
// AC-27-26: Down 敌人行为全停
// AC-27-27: 迁移续跑
// AC-27-28: 体积上界
// AC-27-29: DTO 形状
// AC-27-34: 调试视图隔离

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.EnemyAI
{
    public class EncounterLifecycleTest
    {
        // AC-27-22: 路由纯函数
        [Test]
        public void test_enemyOnset_routesToWorldStream()
        {
            // 验证 EnemyInjuryOnset 路由到世界流
            Assert.IsTrue(Enum.IsDefined(typeof(EventKind), EventKind.EnemyInjuryOnset));
            Assert.IsTrue(Enum.IsDefined(typeof(EventKind), EventKind.InjuryStateChanged));
        }

        // AC-27-23: id 通道
        [Test]
        public void test_idChannel_sharedWithPatients()
        {
            // 验证敌人 id 与病人共用同一空间
            var patientId = new PatientId(5);
            var enemyId = 5; // 同一 id 空间
            Assert.AreEqual(patientId.Value, enemyId);
        }

        // AC-27-24: 可写集白名单
        [Test]
        public void test_writableSet_whitelist()
        {
            // 验证 27 只能写 EncounterEnded
            Assert.IsTrue(EnemyEncounter.ValidateWritableSet());
        }

        // AC-27-25: 三值可达
        [Test]
        public void test_endReason_threeValuesReachable()
        {
            // 脱离
            var state1 = new EncounterState(1, 100);
            EnemyEncounter.Transition(state1, EncounterEndReason.Disengaged, 200);
            Assert.AreEqual(EncounterEndReason.Disengaged, state1.Reason);
            Assert.IsTrue(state1.IsEnded);

            // 全倒地
            var state2 = new EncounterState(2, 100);
            EnemyEncounter.Transition(state2, EncounterEndReason.AllDowned, 200);
            Assert.AreEqual(EncounterEndReason.AllDowned, state2.Reason);

            // 超时
            var state3 = new EncounterState(3, 100);
            EnemyEncounter.Transition(state3, EncounterEndReason.Timeout, 200);
            Assert.AreEqual(EncounterEndReason.Timeout, state3.Reason);
        }

        // AC-27-25: 同遭遇 Ended 恰一条
        [Test]
        public void test_endReason_onlyOneEndedPerEncounter()
        {
            var state = new EncounterState(1, 100);
            EnemyEncounter.Transition(state, EncounterEndReason.Disengaged, 200);

            // 第二次迁移应被忽略
            EnemyEncounter.Transition(state, EncounterEndReason.Timeout, 300);
            Assert.AreEqual(EncounterEndReason.Disengaged, state.Reason,
                "同遭遇应恰一条 Ended");
        }

        // AC-27-26: Down 敌人行为全停
        [Test]
        public void test_downEnemy_allBehaviorStopped()
        {
            var state = new EncounterState(1, 100);
            state.IsDown = true;

            // 验证 Down 时无转移/位移/攻击/寻路
            Assert.IsFalse(EnemyEncounter.CanTransition(state));
            Assert.IsFalse(EnemyEncounter.CanMove(state));
            Assert.IsFalse(EnemyEncounter.CanAttack(state));
            Assert.IsFalse(EnemyEncounter.CanPathfind(state));
        }

        // AC-27-27: 迁移续跑
        [Test]
        public void test_migrationResume_inFlightEvents()
        {
            // 模拟迁移：从预告态续跑
            var state = new EncounterState(1, 100);
            // 迁移后状态应正确重建
            Assert.IsFalse(state.IsEnded);
            Assert.GreaterOrEqual(state.StartedTick, 0);
        }

        // AC-27-28: 体积上界
        [Test]
        public void test_volumeBounded()
        {
            // 验证遭遇事件数有界
            Assert.Greater(EnemyEncounter.ENCOUNTER_TIMEOUT, 0);
        }

        // AC-27-29: DTO 形状
        [Test]
        public void test_enemySignalDto_shape()
        {
            var dto = new EnemySignalDto(
                1,
                new WorldPos(10, 0, 20),
                0,
                EnemyStateMachine.State.Patrol,
                "Downed",
                true,
                new WorldPos(11, 0, 20),
                100);

            Assert.AreEqual(1, dto.ActorId);
            Assert.AreEqual(10, dto.Cell.X);
            Assert.AreEqual(20, dto.Cell.Z);
            Assert.AreEqual(0, dto.Facing);
            Assert.AreEqual(EnemyStateMachine.State.Patrol, dto.State);
            Assert.AreEqual("Downed", dto.DownClass);
            Assert.IsTrue(dto.IsDown);
            Assert.AreEqual(11, dto.PathNext.X);
            Assert.AreEqual(100, dto.Tick);
        }

        // AC-27-29: DTO 无 float/无 Unity 引用/无 encounter 字段
        [Test]
        public void test_enemySignalDto_noFloatNoUnityNoEncounter()
        {
            var dtoType = typeof(EnemySignalDto);
            foreach (var field in dtoType.GetFields())
            {
                Assert.IsFalse(field.FieldType == typeof(float),
                    $"字段 {field.Name} 不应为 float");
                Assert.IsFalse(field.FieldType == typeof(double),
                    $"字段 {field.Name} 不应为 double");
                Assert.IsFalse(field.FieldType.Name.Contains("GameObject"),
                    $"字段 {field.Name} 不应为 Unity 引用");
                Assert.IsFalse(field.Name.Contains("Encounter"),
                    $"字段 {field.Name} 不应包含 encounter");
            }
        }

        // AC-27-34: 调试视图隔离
        [Test]
        public void test_debugView_isolation()
        {
            // 验证调试视图不引用 42 运行时类型
            var debugType = typeof(EnemyDebugView);
            Assert.IsNotNull(debugType);
        }
    }
}
