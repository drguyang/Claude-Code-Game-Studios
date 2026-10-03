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
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;
using NUnit.Framework;

internal sealed class FakeEventAuthority : IEventAuthority
{
    public bool IsAuthority => true;
    public bool IsHost => true;
    public EventRollResult Roll(in RollRequest r) => new EventRollResult(0, 0, 0, 0);
}

/// <summary>
/// 可切换主机位的 authority —— 供 AC-6-26a 的 host gate 负向夹具用。
/// 原 <see cref="FakeEventAuthority"/> 的 <c>IsHost =&gt; true</c> 恒真,
/// 故 13 个既有 [Test] 中**无一**能走到拒写路径(缺口 ①c)。
/// </summary>
internal sealed class SwitchableEventAuthority : IEventAuthority
{
    public SwitchableEventAuthority(bool isHost) { IsHost = isHost; }

    public bool IsAuthority => true;

    /// <summary>主机位 —— 夹具可中途翻转(验「先主机后降级」序列)。</summary>
    public bool IsHost { get; set; }

    public EventRollResult Roll(in RollRequest r) => new EventRollResult(0, 0, 0, 0);
}

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
        private InMemoryBlobPool _pool;
        private PayloadEncoder _encoder;

        [SetUp]
        public void Setup()
        {
            _eventSink = new SpyEventSink();
            _pool = new InMemoryBlobPool();
            _encoder = new PayloadEncoder(_pool);
            var eventAuthority = new FakeEventAuthority();
            _machine = new PoiStateMachine(_eventSink, eventAuthority, new[] { 1, 2, 3 }, _encoder);
        }

        /// <summary>构造带独立池的机器(ADR-029:构造须注入 IPayloadEncoder)。</summary>
        private static (PoiStateMachine Machine, InMemoryBlobPool Pool) Make(
            IEventSink sink, IEventAuthority authority, IEnumerable<int> poiIds)
        {
            var pool = new InMemoryBlobPool();
            return (new PoiStateMachine(sink, authority, poiIds, new PayloadEncoder(pool)), pool);
        }

        /// <summary>
        /// ADR-029 甲案:解码归**调用方**(看得见 codec 的一侧)。
        /// 本 helper 即扮演该侧 —— 从池取字节 → `PayloadCodec` 解码 → 喂给
        /// `RebuildFromDecoded`。`PoiStateMachine` 自身零 codec 依赖。
        /// </summary>
        private static List<(int PoiId, PoiState State)> DecodePoiStates(
            IReadOnlyList<SimEvent> events, IBlobPool pool)
        {
            var result = new List<(int, PoiState)>();
            foreach (var evt in events)
            {
                if (evt.Kind != EventKind.PoiStateChanged) continue;
                Assert.IsTrue(PayloadCodec.TryGetPayload(evt, pool, out PoiStateChangedPayload p),
                    "PoiStateChanged 载荷须能经池 + codec 取回(字节真的进了池)");
                result.Add((p.PoiId, (PoiState)p.NewState));
            }
            return result;
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

            // 收集事件(主机侧池:_pool 持有其字节)
            var events = new List<SimEvent>(_eventSink.AppendedEvents);

            // 新机器重建 —— ADR-029 甲案:调用方(看得见 codec 的一侧)先解码,再喂进来
            var (rebuilt, _) = Make(_eventSink, new FakeEventAuthority(), new[] { 1, 2, 3 });
            rebuilt.RebuildFromDecoded(DecodePoiStates(events, _pool));

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

            var machine = Make(_eventSink, new FakeEventAuthority(), pois).Machine;

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

        // ══════════════════════════════════════════════════════════════
        // AC-6-26a [B] —— host-only write gate 的**负向夹具**(缺口 ①c 的闭合件)
        //
        // 原状态:测试夹具 `IsHost => true` 恒真,13 个 [Test] 无一注入 false
        // ⇒ 只有正路径,负向判据未执行(见 `reconciliation-world-ecozones-2026-10-02.md`)。
        // 判据原文:「Append 权 = 主机唯一:客户端调用写通道 ⇒ **断言失败/拒写**」。
        // ⚠️ 本组只断言**行为**(拒写),不把具体错误码钉死 —— 见文件末 §已知缺陷。
        // ══════════════════════════════════════════════════════════════

        [Test]
        public void test_ac626a_clientWriteChannel_rejected_noEventAppended()
        {
            var sink = new SpyEventSink();
            var client = Make(sink, new SwitchableEventAuthority(isHost: false), new[] { 1, 2, 3 }).Machine;

            var result = client.TryAdvance(1, PoiState.Discovered);

            // 拒写(AC-6-26a)
            Assert.AreNotEqual(PoiStateTransferResult.Success, result,
                "客户端调用写通道不得成功(AC-6-26a)");

            // ⚠️ 2026-10-03(评审 C3):此前**刻意不钉错误码**(因 gate 返回 `PoiNotFound`,
            //    与「POI 不存在」同码,钉死等于把缺陷固化为契约)。
            //    现 gate 已改返回专用码 `NotHost` ⇒ 可钉死,且须与 `PoiNotFound` **可区分**。
            Assert.AreEqual(PoiStateTransferResult.NotHost, result,
                "客户端拒写须返回专用码 `NotHost`(不得与 `PoiNotFound` 混同)");
            Assert.AreNotEqual(PoiStateTransferResult.PoiNotFound, result,
                "「我不是主机」与「该 POI 不存在」**须可区分**(C3)");

            // 对照:真·不存在的 poi_id 在**主机**身份下 ⇒ PoiNotFound
            // (⚠️ 不能在同一客户端上测 —— gate **先于** poi_id 检查 ⇒ 客户端下必得 NotHost,
            //  这正是「两码可分」的体现:先验主机权,再验 id 存在性)
            var hostSink2 = new SpyEventSink();
            var host2 = new PoiStateMachine(hostSink2, new SwitchableEventAuthority(isHost: true),
                                           new[] { 1, 2, 3 }, new PayloadEncoder(new InMemoryBlobPool()));
            var notFound = host2.TryAdvance(999, PoiState.Discovered);
            Assert.AreEqual(PoiStateTransferResult.PoiNotFound, notFound,
                "主机身份 + 未登记 poi_id ⇒ PoiNotFound(与 NotHost 是**两个不同的码**)");
            Assert.AreNotEqual(PoiStateTransferResult.NotHost, notFound,
                "主机身份**不得**返回 NotHost");
            Assert.AreEqual(0, sink.AppendedEvents.Count,
                "客户端调用写通道**不得产生任何 Append**(AC-6-26a:Append 权 = 主机唯一)");
        }

        [Test]
        public void test_ac626a_clientWriteChannel_doesNotMutateState()
        {
            var sink = new SpyEventSink();
            var client = Make(sink, new SwitchableEventAuthority(isHost: false), new[] { 1, 2, 3 }).Machine;

            client.TryAdvance(1, PoiState.Resolved);   // 即使跳级目标合法,也应被门挡

            Assert.AreEqual(PoiState.Undiscovered, client.GetState(1),
                "被拒的写不得改变内存态(否则客户端出现「本地已发现」的幽灵状态)");
        }

        [Test]
        public void test_ac626a_discoverGate_alsoRejectedOnClient()
        {
            // TryDiscover 是独立入口(发现门 AC-6-23),须同样受 host gate 覆盖。
            var sink = new SpyEventSink();
            var client = Make(sink, new SwitchableEventAuthority(isHost: false), new[] { 1, 2, 3 }).Machine;

            var result = client.TryDiscover(1);

            Assert.AreNotEqual(PoiStateTransferResult.Success, result,
                "发现门亦须受 host gate 覆盖(AC-6-26a)");
            Assert.AreEqual(0, sink.AppendedEvents.Count, "客户端发现门不得 Append");
            Assert.AreEqual(PoiState.Undiscovered, client.GetState(1), "客户端发现门不得改状态");
        }

        [Test]
        public void test_ac626a_rebuildFromEvents_allowedOnClient()
        {
            // 负向面的**边界**:门只挡「写通道」,**不挡只读重建**。
            // 客户端必须能从主机回播的世界流重建 POI 状态(EC-16:全员读到同一序列)——
            // 若门误挡重建,客户端将永远看不到任何 POI 状态,与 ADR-020 Amendment B ③ 冲突。
            var hostSink = new SpyEventSink();
            var (host, hostPool) = Make(hostSink, new SwitchableEventAuthority(isHost: true), new[] { 1, 2, 3 });
            host.TryAdvance(1, PoiState.Discovered);
            host.TryAdvance(2, PoiState.Resolved);
            var stream = new List<SimEvent>(hostSink.AppendedEvents);

            var client = Make(new SpyEventSink(), new SwitchableEventAuthority(isHost: false), new[] { 1, 2, 3 }).Machine;
            // ADR-029 甲案:客户端从主机回播的流重建 —— 解码归调用方
            client.RebuildFromDecoded(DecodePoiStates(stream, hostPool));

            Assert.AreEqual(PoiState.Discovered, client.GetState(1), "客户端须能从流重建");
            Assert.AreEqual(PoiState.Resolved, client.GetState(2), "客户端须能从流重建");
        }

        [Test]
        public void test_ac626a_hostToClientDemotion_blocksSubsequentWrites()
        {
            // 门须**每次调用**读取 IsHost(非构造期缓存一次)——
            // 主机降级为客户端后,后续写必须立刻被挡(ADR-020 Amendment B:权责随模式变)。
            var sink = new SpyEventSink();
            var authority = new SwitchableEventAuthority(isHost: true);
            var machine = Make(sink, authority, new[] { 1, 2, 3 }).Machine;

            machine.TryAdvance(1, PoiState.Discovered);
            Assert.AreEqual(1, sink.AppendedEvents.Count, "主机期应写成功");

            authority.IsHost = false;   // 降级
            var after = machine.TryAdvance(2, PoiState.Discovered);

            Assert.AreNotEqual(PoiStateTransferResult.Success, after, "降级后写须被拒");
            Assert.AreEqual(1, sink.AppendedEvents.Count, "降级后不得新增 Append");
            Assert.AreEqual(PoiState.Undiscovered, machine.GetState(2), "降级后不得改状态");
        }

        [Test]
        public void test_ac626a_hostBaseline_stillWrites()
        {
            // 反向用例(否定「两边都不写」的空实现)——
            // 与 clientWriteChannel_rejected 配对:门必须**只挡客户端**。
            var sink = new SpyEventSink();
            var host = Make(sink, new SwitchableEventAuthority(isHost: true), new[] { 1, 2, 3 }).Machine;

            var result = host.TryAdvance(1, PoiState.Discovered);

            Assert.AreEqual(PoiStateTransferResult.Success, result, "主机写须成功");
            Assert.AreEqual(1, sink.AppendedEvents.Count, "主机写须产生一条 Append");
        }

        // ══════════════════════════════════════════════════════════════
        // §历史缺陷(**已闭** 2026-10-03 —— 保留原文作闭环记录)
        //
        // 【原缺陷】host gate 对客户端曾返回 `PoiStateTransferResult.PoiNotFound`,
        //   与「poi_id 不存在」**混同** —— 调用方无法区分「我不是主机」与
        //   「这个 POI 不存在」,可能误走「POI 数据缺失 ⇒ 重载定义」的降级路径。
        //
        // 【原处置】当时的本组刻意**不**把 `PoiNotFound` 钉进断言
        //   (否则等于把缺陷固化为契约),只断言「非 Success」。
        //
        // 【已修】`PoiStateMachine.cs:48` 新增专用枚举成员 `NotHost`,`:110-111`
        //   gate 改返 `NotHost`(:113 的 id 检查在其**之后**,语义各得其码)。
        //   本组现已**双向钉死**:`test_ac626a_clientWriteChannel_rejected_noEventAppended`
        //   正面断言 `== NotHost`,`test_ac626a_hostBaseline_stillWrites` 反向断言
        //   主机身份**不得**返回 `NotHost`、未登记 id 返 `PoiNotFound`(见本文件上方对应 [Test])。
        // ══════════════════════════════════════════════════════════════
    }
}
