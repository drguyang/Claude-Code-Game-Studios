// M2 接线轮阶段 2 · 批次 D —— ResourceHarvested 写者(17 采集)端到端测试(EditMode,真装配,禁 Fake)。
//
// 覆盖:
//   ① 真装配袋调用写者 ⇒ **世界流恰增 1 条**(采集事实那一跳;身份两条归 20,不在本件),
//      Kind / 流别 / 载荷五字段 round-trip / Patient = None / Seq 发号逐项断言;
//   ② 不污染高水位:写入前后 `GetNextPatientId()` 不变(ADR-007 §四,PatientId.None 哨兵);
//   ③ 同节点连采:gather_seq 递增、同 tick 双条不被去重吞(O-4 条件键);
//   ④ 入参非法 fail-loud(tick / instance_id / node_id / gather_seq / qty / out_quality)。
//
// 权威:design/gdd/foraging.md 规则二(Amendment K)· 规则三(铸造点 = DropSpawned)·
//      规则四(out_quality 须落流)· F-17-1(gather_seq 计源)
//      design/registry/entities.yaml SimEvent.Kind.ResourceHarvested(stream: world · author: 17)
//      ADR-009 §二 / §七 Guidelines 4 · ADR-007 §四 · ADR-015 §七(OQ-4-11)· ADR-029 §③

using System;
using System.Linq;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Foraging
{
    /// <summary><see cref="ResourceHarvestWriter"/> 生产写入通道测试。</summary>
    [TestFixture]
    public sealed class ResourceHarvestWriterTest
    {
        private CompositionRootServices _bag;
        private ResourceHarvestWriter _writer;

        [SetUp]
        public void Setup()
        {
            _bag = CompositionRoot.Assemble();
            _writer = new ResourceHarvestWriter(_bag.EventSink, _bag.Encoder);
        }

        private int WorldStreamCount()
            => _bag.Stream.Events.Count(e => StreamRouting.Of(e.Kind) == StreamId.World);

        // ══════════ ① 真装配:恰增 1 条 + 五字段 round-trip ══════════

        [Test]
        public void test_resourceHarvested_realBag_appendsExactlyOneWorldEvent_roundTrip()
        {
            const long tick = 7;
            var instance = new ItemInstanceId(42);   // 铸造点 = DropSpawned(规则三);本件只引用
            const int nodeId = 3;                    // ADR-015 逻辑层稳定标识(非格坐标)
            const int gatherSeq = 0;
            const int qty = 2;
            const int outQuality = 3;                // 已过 QualityCap 截断(规则四)

            int beforeAll = _bag.Stream.Count;
            int beforeWorld = WorldStreamCount();

            _writer.Record(tick, instance, nodeId, gatherSeq, qty, outQuality);

            // ── 恰增 1 条(采集事实那一跳;DropSpawned / DropClaimed 归 20,本件零涉)──
            Assert.AreEqual(beforeAll + 1, _bag.Stream.Count, "真流总事件数恰增 1");
            Assert.AreEqual(beforeWorld + 1, WorldStreamCount(), "世界流恰增 1 条");

            var evt = _bag.Stream.Events[_bag.Stream.Count - 1];
            Assert.AreEqual(EventKind.ResourceHarvested, evt.Kind, "Kind = ResourceHarvested");
            Assert.AreEqual(StreamId.World, StreamRouting.Of(evt.Kind), "落世界流(ADR-009 §二)");
            Assert.AreEqual(PatientId.None, evt.Patient,
                "世界流事件 Patient = None —— 不污染 max(patient_id) 高水位(ADR-007 §四)");
            Assert.AreEqual(tick, evt.Tick, "tick = 动作完成时刻");
            Assert.AreEqual(0, evt.Seq, "Seq 由发号器给出((Tick, Patient) 首号)");

            // ── 载荷 round-trip ──
            Assert.IsTrue(
                PayloadCodec.TryGetPayload<ResourceHarvestedPayload>(evt, _bag.BlobPool, out var decoded),
                "载荷须能经池取回(手搓伪引用会在此 false)");
            Assert.AreEqual(instance.Value, decoded.InstanceId, "payload.instance_id");
            Assert.AreEqual(nodeId, decoded.NodeId, "payload.node_id(稳定标识,非格坐标)");
            Assert.AreEqual(gatherSeq, decoded.GatherSeq, "payload.gather_seq");
            Assert.AreEqual(qty, decoded.Qty, "payload.qty");
            Assert.AreEqual(outQuality, decoded.OutQuality, "payload.out_quality(截断后物化落流)");
        }

        // ══════════ ② 高水位纯净 ══════════

        [Test]
        public void test_resourceHarvested_doesNotPollutePatientHighWaterMark()
        {
            // 预置一个真实病人(高水位基线 = 该 id + 1)
            var patient = _bag.PatientSpawner.SpawnNext(diseaseId: 0, tick: 0);
            var before = _bag.Stream.GetNextPatientId();

            _writer.Record(7, new ItemInstanceId(1), 0, 0, 1, 1);
            _writer.Record(8, new ItemInstanceId(2), 0, 1, 1, 1);

            var after = _bag.Stream.GetNextPatientId();
            Assert.AreEqual(before, after,
                "世界流事件带 None 哨兵,不得抬高 max(patient_id) 高水位(ADR-007 §四)");
            Assert.AreEqual(patient.Value + 1, after.Value, "高水位仍只由病人事件决定");
        }

        // ══════════ ③ 同节点连采:gather_seq 递增 + 双条不坍缩 ══════════

        [Test]
        public void test_resourceHarvested_sameNodeSequential_gatherSeqAdvances_bothKept()
        {
            // 同一 tick 两条(模拟同 tick 连发的多株采集):O-4 条件键须靠载荷身份区分
            _writer.Record(7, new ItemInstanceId(10), 5, 0, 1, 1);
            _writer.Record(7, new ItemInstanceId(11), 5, 1, 1, 2);

            var events = _bag.Stream.Events
                .Where(e => e.Kind == EventKind.ResourceHarvested).ToList();
            Assert.AreEqual(2, events.Count,
                "同 tick 同 Kind 两条不同采集事实都须在流上(O-4:载荷身份区分)");
            Assert.AreEqual(0, events[0].Seq, "首条 Seq = 0");
            Assert.AreEqual(1, events[1].Seq, "次条 Seq 递增(同 (Tick, Patient))");

            Assert.IsTrue(PayloadCodec.TryGetPayload<ResourceHarvestedPayload>(
                events[0], _bag.BlobPool, out var p0));
            Assert.IsTrue(PayloadCodec.TryGetPayload<ResourceHarvestedPayload>(
                events[1], _bag.BlobPool, out var p1));
            Assert.AreEqual(0, p0.GatherSeq, "F-17-1:首采 gather_seq = 0");
            Assert.AreEqual(1, p1.GatherSeq, "F-17-1:次采 gather_seq = 1(该节点计数)");
            Assert.AreEqual(5, p0.NodeId);
            Assert.AreEqual(5, p1.NodeId, "同节点");
            Assert.AreNotEqual(events[0].Payload, events[1].Payload,
                "两事件载荷引用必须不同(伪引用同值 ⇒ 被 O-4 吞成一条)");
        }

        // ══════════ ④ 入参非法 fail-loud ══════════

        [Test]
        public void test_resourceHarvested_invalidArgs_throw()
        {
            var ok = new ItemInstanceId(1);

            Assert.Throws<ArgumentOutOfRangeException>(
                () => _writer.Record(-1, ok, 0, 0, 1, 1), "tick < 0");
            Assert.Throws<ArgumentOutOfRangeException>(
                () => _writer.Record(1, new ItemInstanceId(-1), 0, 0, 1, 1), "instance_id < 0");
            Assert.Throws<ArgumentOutOfRangeException>(
                () => _writer.Record(1, ok, -1, 0, 1, 1), "node_id < 0");
            Assert.Throws<ArgumentOutOfRangeException>(
                () => _writer.Record(1, ok, 0, -1, 1, 1), "gather_seq < 0");
            Assert.Throws<ArgumentOutOfRangeException>(
                () => _writer.Record(1, ok, 0, 0, 0, 1), "qty ≥ 1(GDD foraging)");
            Assert.Throws<ArgumentOutOfRangeException>(
                () => _writer.Record(1, ok, 0, 0, 1, 0), "out_quality ≥ 1(GDD foraging)");

            Assert.AreEqual(0, _bag.Stream.Count, "非法入参不得写出半条事件");
        }

        [Test]
        public void test_resourceHarvestWriter_nullDependency_throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => new ResourceHarvestWriter(null, _bag.Encoder));
            Assert.Throws<ArgumentNullException>(
                () => new ResourceHarvestWriter(_bag.EventSink, null));
        }
    }
}
