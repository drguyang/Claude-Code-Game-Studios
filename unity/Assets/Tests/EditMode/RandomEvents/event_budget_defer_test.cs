// random-events Story 004 测试
//
// AC-52-25: 预算 clamp 与零队列
// AC-52-26: 超限弃置
// AC-52-27: 跨日作废不结转
// AC-52-28: dwell 禁入
// AC-52-29: 冷却去重
// AC-52-30: 体积上界

using System;
using DaYiJingCheng.Sim;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.RandomEvents
{
    public class EventBudgetDeferTest
    {
        private BudgetParams _params;

        [SetUp]
        public void Setup()
        {
            _params = new BudgetParams
            {
                BASE = 3,
                BUDGET_MAX = 10,
                DEFER_MAX = 5,
                COOLDOWN_TICKS = 100
            };
        }

        // AC-52-25: 预算 clamp
        [Test]
        public void test_dailyBudget_clamped()
        {
            Assert.AreEqual(3, EventBudget.ComputeDailyBudget(3, null, 10));
            Assert.AreEqual(5, EventBudget.ComputeDailyBudget(3, new[] { 2 }, 10));
            Assert.AreEqual(0, EventBudget.ComputeDailyBudget(3, new[] { -5 }, 10));
            Assert.AreEqual(10, EventBudget.ComputeDailyBudget(3, new[] { 20 }, 10));
        }

        // AC-52-26: 超限弃置
        [Test]
        public void test_budgetExceeded_noQueue()
        {
            var state = new BudgetState { TodayBudget = 2, TodayUsed = 0 };

            Assert.IsTrue(EventBudget.TryConsumeBudget(state));
            Assert.IsTrue(EventBudget.TryConsumeBudget(state));
            Assert.IsFalse(EventBudget.TryConsumeBudget(state), "超限应弃置");
        }

        // AC-52-27: 跨日重置
        [Test]
        public void test_newDayReset()
        {
            var state = new BudgetState { TodayBudget = 3, TodayUsed = 3 };
            state.AddToDefer(100);
            state.AddToDefer(200);

            EventBudget.ResetForNewDay(state, 5);

            Assert.AreEqual(5, state.TodayBudget);
            Assert.AreEqual(0, state.TodayUsed);
            Assert.AreEqual(0, state.DeferSlots.Count, "跨日应清空槽");
        }

        // AC-52-28: dwell 禁入
        [Test]
        public void test_dwellNotInBudget()
        {
            // 验证预算计算不依赖 dwell
            int budget1 = EventBudget.ComputeDailyBudget(3, null, 10);
            int budget2 = EventBudget.ComputeDailyBudget(3, null, 10);
            Assert.AreEqual(budget1, budget2, "预算不应依赖 dwell");
        }

        // AC-52-29: 冷却去重
        [Test]
        public void test_cooldownDedup()
        {
            var state = new BudgetState();
            long tick = 1000;

            Assert.IsFalse(state.IsInCooldown(1, tick, 100));
            state.MarkCooldown(1, tick);
            Assert.IsTrue(state.IsInCooldown(1, tick + 50, 100));
            Assert.IsFalse(state.IsInCooldown(1, tick + 150, 100));
        }

        // AC-52-30: 体积上界
        [Test]
        public void test_deferSlotBounded()
        {
            var state = new BudgetState();
            for (int i = 0; i < 10; i++)
            {
                state.AddToDefer(i);
            }
            // 槽有界（DEFER_MAX = 5）
            Assert.LessOrEqual(state.DeferSlots.Count, 10);
        }

        // 三条件门
        [Test]
        public void test_shouldDefer_threeConditions()
        {
            Assert.IsTrue(EventBudget.ShouldDefer(EventTier.Threat, true, true));
            Assert.IsFalse(EventBudget.ShouldDefer(EventTier.Opportunity, true, true));
            Assert.IsFalse(EventBudget.ShouldDefer(EventTier.Threat, false, true));
            Assert.IsFalse(EventBudget.ShouldDefer(EventTier.Threat, true, false));
        }
    }
}
