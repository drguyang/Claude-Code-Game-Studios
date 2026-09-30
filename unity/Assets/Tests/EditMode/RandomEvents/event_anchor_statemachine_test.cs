// random-events Story 006 测试
//
// AC-52-22: 锚点确定性
// AC-52-40: 零轮询
// AC-52-41: 世界侧锚点数据源
// AC-52-42: 六态闭集迁移表
// AC-52-46: 迁移续跑

using System;
using DaYiJingCheng.Sim;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.RandomEvents
{
    public class EventAnchorStateMachineTest
    {
        // AC-52-22: 锚点确定性
        [Test]
        public void test_anchorResolution_deterministic()
        {
            var ctx = new AnchorContext(12345, 1000, -1, 17);

            int r1 = EventAnchor.Resolve(SpawnAnchor.GatherPoint, ctx);
            int r2 = EventAnchor.Resolve(SpawnAnchor.GatherPoint, ctx);

            Assert.AreEqual(r1, r2, "同输入应产生同输出");
        }

        // AC-52-22: GATHER_POINT 取点 = S mod count
        [Test]
        public void test_gatherPoint_modSemantics()
        {
            var ctx = new AnchorContext(12345, 1000, -1, 17);
            int result = EventAnchor.ResolveGatherPoint(ctx);

            Assert.GreaterOrEqual(result, 0, "取点应 >= 0");
            Assert.Less(result, 17, "取点应 < 17");
        }

        // AC-52-22: TRAVEL_PATH 回退 CLINIC_FRONT
        [Test]
        public void test_travelPath_fallbackToClinicFront()
        {
            var ctx = new AnchorContext(12345, 1000, -1, 17);
            int result = EventAnchor.Resolve(SpawnAnchor.TravelPath, ctx);
            int clinicFront = EventAnchor.Resolve(SpawnAnchor.ClinicFront, ctx);

            Assert.AreEqual(clinicFront, result, "出诊目标缺失应回退 CLINIC_FRONT");
        }

        // AC-52-42: 六态闭集迁移表
        [Test]
        public void test_transitionTable_closedSet()
        {
            Assert.IsTrue(EventStateMachine.ValidateClosedSet(), "迁移表应闭集");
        }

        // AC-52-42: 合法迁移
        [Test]
        public void test_validTransitions()
        {
            Assert.AreEqual(EventState.Preview, EventStateMachine.Transition(EventState.Pending, EventTrigger.Tick));
            Assert.AreEqual(EventState.Arrival, EventStateMachine.Transition(EventState.Preview, EventTrigger.PreviewDone));
            Assert.AreEqual(EventState.Resolution, EventStateMachine.Transition(EventState.Arrival, EventTrigger.PlayerStay));
            Assert.AreEqual(EventState.Ended, EventStateMachine.Transition(EventState.Resolution, EventTrigger.Resolve));
        }

        // AC-52-42: 非法迁移断言失败
        [Test]
        public void test_invalidTransition_throws()
        {
            Assert.Throws<InvalidOperationException>(() =>
                EventStateMachine.Transition(EventState.Ended, EventTrigger.Tick));
        }

        // AC-52-46: 迁移续跑
        [Test]
        public void test_migrationResume()
        {
            // 模拟迁移：从预告态续跑
            var state = EventState.Preview;
            state = EventStateMachine.Transition(state, EventTrigger.PreviewDone);
            Assert.AreEqual(EventState.Arrival, state, "迁移后应从预告态续跑");
        }

        // AC-52-40: 零轮询
        [Test]
        public void test_noPolling()
        {
            // 验证状态机不引用 Update / 协程
            var stateType = typeof(EventStateMachine);
            foreach (var method in stateType.GetMethods())
            {
                Assert.IsFalse(method.Name.Contains("Update"), "状态机不应有 Update 方法");
                Assert.IsFalse(method.Name.Contains("Coroutine"), "状态机不应有协程");
            }
        }
    }
}
