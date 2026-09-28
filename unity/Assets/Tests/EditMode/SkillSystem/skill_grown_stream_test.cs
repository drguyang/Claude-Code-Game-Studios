// ============================================================================
// SkillGrown 事件流集成 EditMode 测试 —— Story 007 验收补充
// 权威来源: production/epics/skill-system/story-007-skillgrown-event-emit.md
//   · AC-5(三流全序键路由) · 主机唯一 Append(ADR-007 §一)
//   · ADR-009 §三(跨流全序键) · ADR-024(Kind → StreamId 路由)
// ============================================================================
// 本文件验证 EmitGrowth 产出的 SkillGrownPayload 经编码 → SimEvent → Append →
// StreamRouting.Of 路由 → PayloadCodec 解码全链路。因项目无独立 integration
// 测试目录且 IEventSink 仅有接口无实现,本测试住 EditMode,以 mock sink
// 替代真实 Append,覆盖跨组件契约。
// ============================================================================

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.Contracts.SkillSystem;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.SkillSystem
{
    /// <summary>Story 007 SkillGrown 事件流集成测试。</summary>
    [TestFixture]
    internal sealed class SkillGrownStreamTest
    {
        // ═══════════════════════════════════════════════════════════════════
        // 集成链路: EmitGrowth → PayloadCodec.Encode → SimEvent 构造 → StreamRouting
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>SkillGrown 经 StreamRouting 路由到 History 流。</summary>
        [Test]
        public void test_streamRouting_skillGrown_routesToHistory()
        {
            StreamId stream = StreamRouting.Of(EventKind.SkillGrown);
            Assert.That(stream, Is.EqualTo(StreamId.History),
                "SkillGrown 路由到 History 流(ADR-024)");
        }

        /// <summary>全链路往返: EmitGrowth → encode → SimEvent → decode → 字段一致。</summary>
        [Test]
        public void test_fullPipeline_emitEncodeDecode_fieldsPreserved()
        {
            // Arrange: EmitGrowth 产出载荷
            SkillGrownPayload payload = SkillGrownEmitter.EmitGrowth(
                actorId: 7, skillId: (int)SkillId.急救, objectId: 3,
                novelty: NoveltyClass.Stale, level: 5, currentTick: 200,
                patientId: new PatientId(12));

            // Act: 编码 → 构造 SimEvent → 解码
            byte[] encoded = PayloadCodec.Encode(payload);
            var simEvent = new SimEvent(
                tick: 200,
                patient: new PatientId(12),
                seq: 0,
                kind: EventKind.SkillGrown,
                payload: default);

            var decoded = PayloadCodec.Decode<SkillGrownPayload>(EventKind.SkillGrown, encoded);

            // Assert: 解码字段 = 原始载荷
            Assert.That(decoded.ActorId, Is.EqualTo(payload.ActorId));
            Assert.That(decoded.PatientId, Is.EqualTo(payload.PatientId));
            Assert.That(decoded.SkillId, Is.EqualTo(payload.SkillId));
            Assert.That(decoded.ObjectId, Is.EqualTo(payload.ObjectId));
            Assert.That(decoded.NoveltyClass, Is.EqualTo(payload.NoveltyClass));
            Assert.That(decoded.Level, Is.EqualTo(payload.Level));

            // SimEvent 头部字段正确
            Assert.That(simEvent.Kind, Is.EqualTo(EventKind.SkillGrown));
            Assert.That(simEvent.Tick, Is.EqualTo(200L));
            Assert.That(simEvent.Patient, Is.EqualTo(new PatientId(12)));
            Assert.That(simEvent.Seq, Is.EqualTo(0L));
        }

        /// <summary>同一 tick 多条 SkillGrown 共享同一 (Tick, Patient) 空间,Seq 由主机区分。</summary>
        [Test]
        public void test_multipleEvents_sameTickPatient_distinctBySeq()
        {
            // 两条成长事件,同一 tick 同一 actor
            var p1 = SkillGrownEmitter.EmitGrowth(
                actorId: 1, skillId: (int)SkillId.诊断, objectId: 1,
                novelty: NoveltyClass.First, level: 2, currentTick: 100,
                patientId: PatientId.None);

            var p2 = SkillGrownEmitter.EmitGrowth(
                actorId: 1, skillId: (int)SkillId.采集, objectId: 5,
                novelty: NoveltyClass.Normal, level: null, currentTick: 100,
                patientId: PatientId.None);

            byte[] e1 = PayloadCodec.Encode(p1);
            byte[] e2 = PayloadCodec.Encode(p2);

            var d1 = PayloadCodec.Decode<SkillGrownPayload>(EventKind.SkillGrown, e1);
            var d2 = PayloadCodec.Decode<SkillGrownPayload>(EventKind.SkillGrown, e2);

            Assert.That(d1.SkillId, Is.EqualTo((int)SkillId.诊断));
            Assert.That(d2.SkillId, Is.EqualTo((int)SkillId.采集));
            Assert.That(d1.Level, Is.EqualTo(2));
            Assert.That(d2.Level, Is.EqualTo(-1));
        }

        /// <summary>模拟 Append 链路: mock sink 接收 SimEvent 并记录 Kind。</summary>
        [Test]
        public void test_mockAppend_receivesSkillGrownEvent()
        {
            var sink = new MockEventSink();

            SkillGrownPayload payload = SkillGrownEmitter.EmitGrowth(
                actorId: 1, skillId: (int)SkillId.诊断, objectId: 1,
                novelty: NoveltyClass.First, level: null, currentTick: 100,
                patientId: PatientId.None);

            byte[] encoded = PayloadCodec.Encode(payload);
            var evt = new SimEvent(
                tick: 100,
                patient: PatientId.None,
                seq: 0,
                kind: EventKind.SkillGrown,
                payload: default);

            sink.Append(in evt);

            Assert.That(sink.AppendedCount, Is.EqualTo(1));
            Assert.That(sink.LastKind, Is.EqualTo(EventKind.SkillGrown));
            Assert.That(sink.LastTick, Is.EqualTo(100L));
            Assert.That(sink.LastPatient, Is.EqualTo(PatientId.None));
        }

        // ═══════════════════════════════════════════════════════════════════
        // Mock IEventSink 实现
        // ═══════════════════════════════════════════════════════════════════

        private sealed class MockEventSink : IEventSink
        {
            public int AppendedCount { get; private set; }
            public EventKind LastKind { get; private set; }
            public long LastTick { get; private set; }
            public PatientId LastPatient { get; private set; }

            public void Append(in SimEvent e)
            {
                AppendedCount++;
                LastKind = e.Kind;
                LastTick = e.Tick;
                LastPatient = e.Patient;
            }
        }
    }
}
