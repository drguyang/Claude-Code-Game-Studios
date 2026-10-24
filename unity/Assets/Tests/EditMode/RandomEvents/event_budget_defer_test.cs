// random-events Story 004 测试
//
// AC 映射(2026-10-25 S19-4 订正 —— 原头注多条张冠李戴,原文见 git 历史,原文不删):
//   test_dailyBudget_clamped         → AC-52-25(预算 clamp 与零队列)+ AC-52-27
//                                      (BASE=0 ∧ ΣMod<0 ⇒ 下钳 0,不为负)
//   test_budgetExceeded_noQueue      → AC-52-26(超限弃置;规则六之三「医馆内压制」为唯一例外)
//   test_newDayReset                 → AC-52-48 跨日半(预算两数重置、由 tick 边界驱动)+
//                                      槽清空经流(⚠️ 本件改写:原 `AddToDefer` 直改 API 已删
//                                      —— S19-4 判据①「不保留第二真源」;跨日作废 = 对每个
//                                      挂起槽发 `ThreatDeferralCleared(reason=作废)` → 折叠清)
//   test_dwellNotInBudget            → AC-52-28(dwell 禁入:抽取/预算的输入签名不含 dwell time)
//                                      ⚠️ 原实现「同参调两次比相等」恒真,系假绿,本件换签名判据
//   test_cooldownDedup               → F2 冷却机制(GDD 冷却去重;原记 AC-52-29 系张冠李戴
//                                      —— AC-52-29 真身 = 医馆压制/补发/跨日作废)
//   test_deferSlotBounded            → GDD :300「DeferredThreatSlot 当日有界(至多 DEFER_MAX 条)」
//                                      (挂 AC-52-26 例外有界 + AC-52-29 作废语义;原记 AC-52-30
//                                      系张冠李戴 —— AC-52-30 真身 = 三案链脚本触发、不参与预算)
//                                      ⚠️ 原实现「加 10 断 ≤10」空断言,系假绿,本件换越界拒收判据
//   test_shouldDefer_threeConditions → 规则六之三三条件门(挂 AC-52-29)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.RandomEvents
{
    public class EventBudgetDeferTest
    {
        private BudgetParams _params;
        private InMemoryBlobPool _pool;
        private PayloadEncoder _encoder;
        private PayloadDecoder _decoder;

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
            _pool = new InMemoryBlobPool();
            _encoder = new PayloadEncoder(_pool);
            _decoder = new PayloadDecoder(_pool);
        }

        private sealed class RecordingSink : IEventSink
        {
            public int AppendCount;
            public readonly List<SimEvent> Events = new List<SimEvent>();
            public void Append(in SimEvent e) { AppendCount++; Events.Add(e); }
        }

        // AC-52-25 + AC-52-27: 预算 clamp(含下钳 0)
        [Test]
        public void test_dailyBudget_clamped()
        {
            Assert.AreEqual(3, EventBudget.ComputeDailyBudget(3, null, 10));
            Assert.AreEqual(5, EventBudget.ComputeDailyBudget(3, new[] { 2 }, 10));
            Assert.AreEqual(0, EventBudget.ComputeDailyBudget(0, new[] { -5 }, 10), "AC-52-27:下钳 0,不为负");
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

        // AC-52-48 跨日半:预算两数重置;槽清空经流(2026-10-25 改写,原 AddToDefer 直改面已删)
        [Test]
        public void test_newDayReset()
        {
            var state = new BudgetState { TodayBudget = 3, TodayUsed = 3 };
            var sink = new RecordingSink();
            var writer = new DeferredThreatSlotWriter(sink, _encoder);

            // 当日:两威胁入槽(经流)
            Assert.AreEqual(DeferredThreatWriteResult.Applied, writer.TryDefer(10, 0, 100, _params.DEFER_MAX));
            Assert.AreEqual(DeferredThreatWriteResult.Applied, writer.TryDefer(11, 1, 200, _params.DEFER_MAX));
            foreach (var e in sink.Events)
                EventBudget.FoldDeferEvent(state, e, _decoder, _params.DEFER_MAX);
            Assert.AreEqual(2, state.DeferSlots.Count, "跨日前:两槽挂起");

            // 跨日(AC-52-48):52 生产侧对每个挂起槽发作废 → 折叠自动清,不结转
            const long ticksPerDay = 1200;   // 夹具值;TICKS_PER_DAY 的权威值归 52 配置轮
            Assert.AreEqual(DeferredThreatWriteResult.Applied,
                writer.TryClear(ticksPerDay, 0, DeferredThreatSlotWriter.ReasonVoided, _params.DEFER_MAX));
            Assert.AreEqual(DeferredThreatWriteResult.Applied,
                writer.TryClear(ticksPerDay, 1, DeferredThreatSlotWriter.ReasonVoided, _params.DEFER_MAX));
            foreach (var e in sink.Events.Skip(2))
                EventBudget.FoldDeferEvent(state, e, _decoder, _params.DEFER_MAX);
            Assert.AreEqual(0, state.DeferSlots.Count, "跨日应经作废事件清空槽(不结转)");

            // 预算两数重置(ResetForNewDay 只管预算,不碰槽 —— 清槽已由上面的作废流承载)
            EventBudget.ResetForNewDay(state, 5);

            Assert.AreEqual(5, state.TodayBudget);
            Assert.AreEqual(0, state.TodayUsed);
        }

        // AC-52-28: dwell 禁入 —— 输入签名不含 dwell time(2026-10-25 换真判据,原恒真假绿)
        [Test]
        public void test_dwellNotInBudget()
        {
            var fields = typeof(BudgetParams).GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Select(f => f.Name).ToArray();
            CollectionAssert.AreEquivalent(
                new[] { "BASE", "BUDGET_MAX", "DEFER_MAX", "COOLDOWN_TICKS" },
                fields,
                "预算参数面恰四字段 —— 无 dwell / 停留时长可进权重(「停留」在数学上进不了输入)");

            var method = typeof(EventBudget).GetMethod(nameof(EventBudget.ComputeDailyBudget));
            Assert.IsNotNull(method);
            Assert.IsFalse(method.GetParameters().Any(p => p.Name.ToLowerInvariant().Contains("dwell")),
                "ComputeDailyBudget 签名不含 dwell 参数");
        }

        // F2 冷却去重(原记 AC-52-29 系张冠李戴,见头注订正)
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

        // GDD :300:DEFER_MAX 槽上界(2026-10-25 换真判据;原「加 10 断 ≤10」空断言系假绿)
        [Test]
        public void test_deferSlotBounded()
        {
            var state = new BudgetState();
            var sink = new RecordingSink();
            var writer = new DeferredThreatSlotWriter(sink, _encoder);

            // slot ∈ [0, DEFER_MAX) 全部可挂(写者侧具名 Applied)
            for (int i = 0; i < _params.DEFER_MAX; i++)
                Assert.AreEqual(DeferredThreatWriteResult.Applied,
                    writer.TryDefer(10 + i, i, 100 + i, _params.DEFER_MAX),
                    $"slot {i} 在上界之内应可挂");

            // 写者侧:上界之外拒收(具名结果码,零增量 —— 拒收前后 sink 仍只含成功那 5 条)
            Assert.AreEqual(DeferredThreatWriteResult.SlotOutOfRange,
                writer.TryDefer(99, _params.DEFER_MAX, 199, _params.DEFER_MAX),
                "slot == DEFER_MAX 越界 ⇒ 拒收");
            Assert.AreEqual(_params.DEFER_MAX, sink.Events.Count, "拒收 ⇒ 零增量");

            foreach (var e in sink.Events)
                EventBudget.FoldDeferEvent(state, e, _decoder, _params.DEFER_MAX);
            Assert.AreEqual(_params.DEFER_MAX, state.DeferSlots.Count, "恰 DEFER_MAX 槽挂满");

            // 折叠侧 fail-loud:绕过写者域校验构造的越界事件 ⇒ 抛,不静默
            var badRef = _encoder.Encode(EventKind.ThreatDeferred,
                new ThreatDeferredPayload(_params.DEFER_MAX, 999));
            var bad = new SimEvent(999, PatientId.None, -1, EventKind.ThreatDeferred, badRef);
            Assert.Throws<InvalidDataException>(
                () => EventBudget.FoldDeferEvent(state, bad, _decoder, _params.DEFER_MAX),
                "流内越界事件 = 流损坏 ⇒ fail-loud(静默 = 迁移后槽集漂移)");
        }

        // 规则六之三:三条件门
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
