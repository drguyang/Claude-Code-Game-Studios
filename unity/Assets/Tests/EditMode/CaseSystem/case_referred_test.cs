// 钩子病例的收场载体 `CaseReferred` —— 判据夹具(2026-11-09 · T5b · ADR-044)
//
// 权威:docs/architecture/adr-044-case-referred-kind.md §7(V-1…V-6)
//
// 本文件要证明:
//   ① **路由** —— 落病例流(不是病史流、不是世界流);
//   ② **载荷往返** —— 三字段逐值相等;
//   ③ **写者** —— 唯一 Append、事件头正确;
//   ④ **同一 case 至多一条**(§D6)—— 重复 = no-op,流长度不变;
//   ⑤ **reason 闭集** —— 闭集外理由**写不进去也读不出来**;
//   ⑥ **哨兵病人拒收** —— PatientId.None 是世界级哨兵,不是转诊对象。
//
// ⚠️ 本文件**只读**仓库文件(写盘落 `Path.GetTempPath()` 之外的路径为零)⇒ 不触发 `FsWriteBoundaryGate`。
// ⚠️ 无自指判据(F3):期望值全为字面量。

using System;
using System.Collections.Generic;
using NUnit.Framework;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Tests.CaseSystem
{
    /// <summary>T5b:转诊 / 送医的载体与写者。</summary>
    [TestFixture]
    public sealed class CaseReferredTest
    {
        private sealed class Sink : IEventSink
        {
            public readonly List<SimEvent> Events = new List<SimEvent>();
            public void Append(in SimEvent e) => Events.Add(e);
        }

        private sealed class Harness
        {
            public readonly InMemoryBlobPool Pool = new InMemoryBlobPool();
            public readonly PayloadEncoder Encoder;
            public readonly Sink Sink = new Sink();
            public readonly HookCaseClosureWriter Writer;

            public Harness()
            {
                Encoder = new PayloadEncoder(Pool);
                var decoder = new PayloadDecoder(Pool);
                Writer = new HookCaseClosureWriter(Sink, Encoder, () => Sink.Events, decoder);
            }
        }

        private static CaseId Case(long tick = 10, int patient = 100, long seq = 1)
            => new CaseId(tick, patient, seq);

        // ── ① 路由 ────────────────────────────────────────────────────────────

        [Test]
        public void test_routing_caseReferred_goesToCaseStream()
        {
            Assert.That(StreamRouting.Of(EventKind.CaseReferred), Is.EqualTo(StreamId.Case),
                "ADR-044 §D1:转诊是病例侧结论,落病例流");
        }

        // ── ② 载荷往返 ────────────────────────────────────────────────────────

        [Test]
        public void test_payload_roundTrips()
        {
            var pool = new InMemoryBlobPool();
            var encoder = new PayloadEncoder(pool);
            var decoder = new PayloadDecoder(pool);

            var payload = new CaseReferredPayload(100, Case(), CaseReferredPayload.ReasonHospitalized);
            PayloadRef r = encoder.Encode(EventKind.CaseReferred, payload);
            CaseReferredPayload back = decoder.Decode<CaseReferredPayload>(EventKind.CaseReferred, r);

            Assert.That(back.PatientId, Is.EqualTo(100));
            Assert.That(back.CaseId, Is.EqualTo(Case()));
            Assert.That(back.Reason, Is.EqualTo(1));
        }

        // ── ③ 写者 ────────────────────────────────────────────────────────────

        [Test]
        public void test_writer_emitsOneEvent_withCorrectHeader()
        {
            var h = new Harness();
            Assert.That(h.Writer.TryRefer(123, new PatientId(100), Case(), 0), Is.True);

            Assert.That(h.Sink.Events.Count, Is.EqualTo(1));
            SimEvent e = h.Sink.Events[0];
            Assert.That(e.Kind, Is.EqualTo(EventKind.CaseReferred));
            Assert.That(e.Patient.Value, Is.EqualTo(100), "转诊是病人级事实 ⇒ 头部 Patient = 该病人");
            Assert.That(e.Tick, Is.EqualTo(123));

            CaseReferredPayload p = new PayloadDecoder(h.Pool)
                .Decode<CaseReferredPayload>(EventKind.CaseReferred, e.Payload);
            Assert.That(p.Reason, Is.EqualTo(0));
        }

        // ── ④ 同一 case 至多一条 ──────────────────────────────────────────────

        [Test]
        public void test_writer_duplicateReferral_isNoOp()
        {
            var h = new Harness();
            Assert.That(h.Writer.TryRefer(123, new PatientId(100), Case(), 0), Is.True);
            Assert.That(h.Writer.TryRefer(200, new PatientId(100), Case(), 1), Is.False,
                "同一病例第二次转诊须返回 false(§D6)");
            Assert.That(h.Sink.Events.Count, Is.EqualTo(1), "重复不得写第二条");
        }

        [Test]
        public void test_writer_differentCase_isNotDeduped()
        {
            var h = new Harness();
            Assert.That(h.Writer.TryRefer(123, new PatientId(100), Case(), 0), Is.True);
            Assert.That(h.Writer.TryRefer(124, new PatientId(101), Case(tick: 20, patient: 101, seq: 2), 0),
                Is.True, "去重键是 case_id,不是病人 —— 另一个病例照写");
            Assert.That(h.Sink.Events.Count, Is.EqualTo(2));
        }

        // ── ⑤ reason 闭集 ─────────────────────────────────────────────────────

        [Test]
        public void test_writer_unknownReason_failsLoudWithoutWriting()
        {
            var h = new Harness();
            var ex = Assert.Throws<ArgumentOutOfRangeException>(
                () => h.Writer.TryRefer(123, new PatientId(100), Case(), reason: 7));

            Assert.That(ex.Message, Does.Contain("闭集"));
            Assert.That(h.Sink.Events.Count, Is.EqualTo(0), "拒收时不得留下半条事件");
        }

        /// <summary>解码侧同为闭集:把合法字节的最后一字段(reason)改成 99 ⇒ 解码即拒。</summary>
        [Test]
        public void test_decoder_unknownReason_isRejected()
        {
            var pool = new InMemoryBlobPool();
            var encoder = new PayloadEncoder(pool);
            var decoder = new PayloadDecoder(pool);

            PayloadRef r = encoder.Encode(EventKind.CaseReferred,
                new CaseReferredPayload(100, Case(), CaseReferredPayload.ReasonTransferred));
            Assert.That(pool.TryGetBlob(r.BlobId, out ReadOnlyMemory<byte> raw), Is.True,
                "前置:须能取回载荷字节(否则下面的改写打在空气上)");
            byte[] bytes = raw.ToArray();

            // reason 是 Encode 的**最后一个**字段 ⇒ 末 4 字节即它。
            int tail = bytes.Length - 4;
            bytes[tail] = 99; bytes[tail + 1] = 0; bytes[tail + 2] = 0; bytes[tail + 3] = 0;

            int badBlobId = pool.Store(bytes);
            var bad = new PayloadRef(badBlobId, 0, bytes.Length);
            Assert.Throws<System.IO.InvalidDataException>(
                () => decoder.Decode<CaseReferredPayload>(EventKind.CaseReferred, bad),
                "闭集外 reason 不得被读成合法转诊单");
        }

        // ── ⑥ 哨兵病人 ────────────────────────────────────────────────────────

        [Test]
        public void test_writer_sentinelPatient_isRejected()
        {
            var h = new Harness();
            Assert.Throws<ArgumentException>(
                () => h.Writer.TryRefer(123, PatientId.None, Case(), 0));
            Assert.That(h.Sink.Events.Count, Is.EqualTo(0));
        }

        [Test]
        public void test_writer_negativeTick_isRejected()
        {
            var h = new Harness();
            Assert.Throws<ArgumentOutOfRangeException>(
                () => h.Writer.TryRefer(-1, new PatientId(100), Case(), 0));
        }
    }
}
