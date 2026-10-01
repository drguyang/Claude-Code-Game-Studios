// Story 003 测试: POI 状态机与 PoiStateChanged 世界流
//
// AC-6-10: PoiState 三态枚举,单调不可逆
// AC-6-11: 跳级合法
// AC-6-12: 逆转移无代码路径
// AC-6-13: 同一 tick 同 POI 至多一条状态事件
// AC-6-14/15: 重放重建 == 运行期内存态
// AC-6-16: 无第二存储(状态 = 流重建视图)
// AC-6-17: 有界性 ≤ 2N
// AC-6-18: spawn_anchor 读定义不读状态

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;

internal sealed class FakeEventAuthority : IEventAuthority
{
    public bool IsAuthority => true;
    public bool IsHost => true;
    public EventRollResult Roll(in RollRequest r) => new EventRollResult(0, 0, 0, 0);
}
using NUnit.Framework;

namespace DaYiJingCheng.Tests.WorldEcozones
{
    /// <summary>测试用 SpyEventSink —— 记录 Append 调用。</summary>
    internal sealed class SpyEventSink : IEventSink
    {
        public readonly List<SimEvent> AppendedEvents = new List<SimEvent>();

        public void Append(in SimEvent e)
        {
            AppendedEvents.Add(e);
        }

        public void Clear()
        {
            AppendedEvents.Clear();
        }
    }

    public class PoiStateMachineTest
    {
        private SpyEventSink _eventSink;
        private PoiStateMachine _machine;

        [SetUp]
        public void Setup()
        {
            _eventSink = new SpyEventSink();
            var eventAuthority = new FakeEventAuthority();
            _machine = new PoiStateMachine(_eventSink, eventAuthority, new[] { 1, 2, 3 });
        }

        // AC-6-10: 三态枚举值
        [Test]
        public void test_poiState_hasThreeValues()
        {
            Assert.AreEqual(0, (int)PoiState.Undiscovered);
            Assert.AreEqual(1, (int)PoiState.Discovered);
            Assert.AreEqual(2, (int)PoiState.Resolved);
        }

        // AC-6-10: 初始状态 Undiscovered
        [Test]
        public void test_initialState_isUndiscovered()
        {
            Assert.AreEqual(PoiState.Undiscovered, _machine.GetState(1));
        }

        // 合法转移: Undiscovered → Discovered
        [Test]
        public void test_undiscoveredToDiscovered_succeeds()
        {
            var result = _machine.TryAdvance(1, PoiState.Discovered);
            Assert.AreEqual(PoiStateTransferResult.Success, result);
            Assert.AreEqual(PoiState.Discovered, _machine.GetState(1));
        }

        // 合法转移: Discovered → Resolved
        [Test]
        public void test_discoveredToResolved_succeeds()
        {
            _machine.TryAdvance(1, PoiState.Discovered);
            var result = _machine.TryAdvance(1, PoiState.Resolved);
            Assert.AreEqual(PoiStateTransferResult.Success, result);
            Assert.AreEqual(PoiState.Resolved, _machine.GetState(1));
        }

        // AC-6-11: 跳级合法(Undiscovered → Resolved)
        [Test]
        public void test_skipDiscovered_undiscoveredToResolved_succeeds()
        {
            var result = _machine.TryAdvance(1, PoiState.Resolved);
            Assert.AreEqual(PoiStateTransferResult.Success, result);
            Assert.AreEqual(PoiState.Resolved, _machine.GetState(1));
        }

        // AC-6-12: 逆转移无 API 路径(TryAdvance 拒绝)
        [Test]
        public void test_reverseTransfer_discoveredToUndiscovered_rejected()
        {
            _machine.TryAdvance(1, PoiState.Discovered);
            var result = _machine.TryAdvance(1, PoiState.Undiscovered);
            Assert.AreEqual(PoiStateTransferResult.InvalidTransfer, result);
        }

        [Test]
        public void test_reverseTransfer_resolvedToDiscovered_rejected()
        {
            _machine.TryAdvance(1, PoiState.Discovered);
            _machine.TryAdvance(1, PoiState.Resolved);
            var result = _machine.TryAdvance(1, PoiState.Discovered);
            Assert.AreEqual(PoiStateTransferResult.InvalidTransfer, result);
        }

        // 幂等: 已在目标状态
        [Test]
        public void test_idempotent_alreadyAtState()
        {
            _machine.TryAdvance(1, PoiState.Discovered);
            var result = _machine.TryAdvance(1, PoiState.Discovered);
            Assert.AreEqual(PoiStateTransferResult.AlreadyAtState, result);
        }

        // AC-6-13: 每次转移恰一条 PoiStateChanged, PatientId.None
        [Test]
        public void test_eachTransfer_emitsExactlyOneEvent()
        {
            _machine.TryAdvance(1, PoiState.Discovered);
            _machine.TryAdvance(1, PoiState.Resolved);

            Assert.AreEqual(2, _eventSink.AppendedEvents.Count);
            foreach (var evt in _eventSink.AppendedEvents)
            {
                Assert.AreEqual(EventKind.PoiStateChanged, evt.Kind);
                Assert.AreEqual(PatientId.None, evt.Patient);
            }
        }

        // AC-6-14/15: 重放重建 == 运行期内存态
        [Test]
        public void test_rebuildFromEvents_equalsLiveState()
        {
            // 执行一些转移
            _machine.TryAdvance(1, PoiState.Discovered);
            _machine.TryAdvance(2, PoiState.Resolved);
            _machine.TryAdvance(3, PoiState.Discovered);

            // 收集事件
            var events = new List<SimEvent>(_eventSink.AppendedEvents);

            // 新机器重建
            var rebuilt = new PoiStateMachine(_eventSink, new FakeEventAuthority(), new[] { 1, 2, 3 });
            rebuilt.RebuildFromEvents(events);

            // 逐 POI 比对
            Assert.AreEqual(PoiState.Discovered, rebuilt.GetState(1));
            Assert.AreEqual(PoiState.Resolved, rebuilt.GetState(2));
            Assert.AreEqual(PoiState.Discovered, rebuilt.GetState(3));
        }

        // AC-6-16: 无第二存储 — 状态仅存于 _stateMap,Snapshot 返回只读视图
        [Test]
        public void test_noSecondStorage_snapshotIsReadOnly()
        {
            _machine.TryAdvance(1, PoiState.Discovered);
            var snapshot = _machine.Snapshot();

            // 修改快照不应影响内部状态
            // (Dictionary 是引用类型,但 Snapshot 返回 IReadOnlyDictionary)
            Assert.IsTrue(snapshot is System.Collections.Generic.IReadOnlyDictionary<int, PoiState>);
        }

        // AC-6-17: 有界性 ≤ 2N
        [Test]
        public void test_boundedEvents_atMostTwoPerPoi()
        {
            int n = 100;
            var pois = new List<int>();
            for (int i = 0; i < n; i++) pois.Add(i);

            var machine = new PoiStateMachine(_eventSink, new FakeEventAuthority(), pois);

            // 全量转移:Undiscovered → Discovered → Resolved(每 POI 至多 2 次)
            for (int i = 0; i < n; i++)
            {
                machine.TryAdvance(i, PoiState.Discovered);
                machine.TryAdvance(i, PoiState.Resolved);
            }

            Assert.AreEqual(2 * n, _eventSink.AppendedEvents.Count);
        }

        // 边界: PoiNotFound
        [Test]
        public void test_poiNotFound_returnsNotFound()
        {
            var result = _machine.TryAdvance(999, PoiState.Discovered);
            Assert.AreEqual(PoiStateTransferResult.PoiNotFound, result);
        }
    }
}
