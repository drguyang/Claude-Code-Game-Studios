// 2026-11-02 · `TD-interaction-03` —— 发现上报的生产实装(4 → 6)
//
// 判据(逐条可证伪):
//   ① 一次请求 ⇒ 6 的状态机把该 POI 从 `Undiscovered` 推到 `Discovered`,
//      且**恰一条** `PoiStateChanged` 入流(写者是 6,不是 4 —— ADR-021 §②);
//   ② 未登记的 poi_id ⇒ `NotRegistered` 是**合法结果**:不抛、零事件、状态不变;
//   ③ 状态机未装配 ⇒ **抛**(不静默丢弃 —— 空实现正是本债的成因)。

using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Interaction;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Boot
{
    /// <summary>`PoiDiscoveryReporter` 与 `PoiStateMachine` 的接缝。</summary>
    public sealed class poi_discovery_reporter_test
    {
        private CompositionRootServices _bag;

        [SetUp]
        public void SetUp() => _bag = CompositionRoot.Assemble();

        [TearDown]
        public void TearDown() => _bag?.Dispose();

        private PoiStateMachine Machine(params int[] ids)
            => new PoiStateMachine(_bag.EventSink, new P0HostEventAuthority(), ids, _bag.Encoder);

        private int CountOf(EventKind kind)
        {
            int n = 0;
            foreach (SimEvent e in _bag.Stream.Events) if (e.Kind == kind) n++;
            return n;
        }

        // ══════════════ ① 请求 ⇒ 状态推进 + 恰一条事件 ══════════════

        [Test]
        public void test_request_discoverState_andWritesExactlyOneEvent()
        {
            PoiStateMachine machine = Machine(0, 1);
            var reporter = new PoiDiscoveryReporter(() => machine);
            Assert.AreEqual(PoiState.Undiscovered, machine.GetState(1), "[前置] 初始未发现");

            reporter.Request(new DiscoveryRequest(poiId: 1, evidenceCell: new WorldPos(2, 0, 2), tick: 5));

            Assert.AreEqual(PoiState.Discovered, machine.GetState(1),
                "请求 ⇒ 6 的状态机推进(4 只发请求,不写状态)");
            Assert.AreEqual(1, CountOf(EventKind.PoiStateChanged), "恰一条事件");
        }

        [Test]
        public void test_secondRequest_samePoi_idempotent_noSecondEvent()
        {
            PoiStateMachine machine = Machine(1);
            var reporter = new PoiDiscoveryReporter(() => machine);

            reporter.Request(new DiscoveryRequest(1, new WorldPos(2, 0, 2), 5));
            reporter.Request(new DiscoveryRequest(1, new WorldPos(2, 0, 2), 6));

            Assert.AreEqual(1, CountOf(EventKind.PoiStateChanged),
                "二次发现 = `AlreadyAtState`(幂等),不写第二条");
        }

        // ══════════════ ② 未登记 id 是合法结果 ══════════════

        [Test]
        public void test_unknownPoi_noThrow_noEvent()
        {
            PoiStateMachine machine = Machine(0);
            var reporter = new PoiDiscoveryReporter(() => machine);

            Assert.DoesNotThrow(() =>
                reporter.Request(new DiscoveryRequest(poiId: 99, evidenceCell: default, tick: 5)),
                "未登记的 poi_id ⇒ `NotRegistered`,是合法返回值,不是异常");

            Assert.AreEqual(0, CountOf(EventKind.PoiStateChanged), "零事件");
            Assert.AreEqual(PoiState.Undiscovered, machine.GetState(99), "状态不变");
        }

        // ══════════════ ③ 未装配 ⇒ 抛 ══════════════

        [Test]
        public void test_unboundMachine_throws_notSilentlyDropped()
        {
            var reporter = new PoiDiscoveryReporter(() => null);

            var ex = Assert.Throws<System.InvalidOperationException>(() =>
                reporter.Request(new DiscoveryRequest(0, default, 1)));
            StringAssert.Contains("未装配", ex.Message,
                "报文须点名装配缺陷 —— 静默丢弃会把同一个假绿换个位置");
        }
    }
}
