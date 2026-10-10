// M2 接线轮阶段 2 · 批次 D —— CaseOpened 写者(37)端到端测试(EditMode,真装配,禁 Fake)。
//
// 覆盖:
//   ① 真装配袋(真 EventStream + 真 PayloadEncoder + 真在场登记簿)调用写者 ⇒
//      **病例流恰增 1 条**,Kind / 流别 / 载荷 round-trip / Patient / Seq 发号逐项断言;
//   ② 前置条件(GDD 规则二):已开案 ⇒ 回到那一例零写入;病人不在场 / 无病人 ⇒ 零写入;
//   ③ 复诊(前案已结)⇒ 可再开新案(规则三串接的前提);
//   ④ case_id 三元组由事件头派生(载荷不落 opened_tick / case_id)。
//
// 权威:design/gdd/case-system.md 规则二 / 规则三 / AC-37-26 ·
//      design/registry/entities.yaml SimEvent.Kind.CaseOpened(stream: case · author: 37)·
//      ADR-008(病例流 / case_id 三元组)· ADR-024 ① · ADR-029 §③ · ADR-005(Seq 发号)

using System;
using System.Linq;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.CaseSystem
{
    /// <summary><see cref="CaseOpenWriter"/> 生产写入通道测试。</summary>
    [TestFixture]
    public sealed class CaseOpenWriterTest
    {
        private CompositionRootServices _bag;
        private CaseOpenWriter _writer;
        private PatientId _patient;
        private const long SpawnTick = 0;

        [SetUp]
        public void Setup()
        {
            _bag = CompositionRoot.Assemble();
            _writer = new CaseOpenWriter(_bag.EventSink, _bag.Presence, _bag.Encoder, _bag.Stream.Events);
            // 病人:9 创建 + 组合层灌在场(「病人存在」前置的真源)
            _patient = _bag.PatientSpawner.SpawnNext(diseaseId: 0, tick: SpawnTick);
            _bag.Presence.AddPresent(_patient, new WorldPos(0, 0, 0));
        }

        private int CaseStreamCount()
            => _bag.Stream.Events.Count(e => StreamRouting.Of(e.Kind) == StreamId.Case);

        // ══════════ ① 真装配:恰增 1 条 + 全字段 round-trip ══════════

        [Test]
        public void test_caseOpened_realBag_appendsExactlyOneCaseEvent_roundTrip()
        {
            const long openTick = 5;
            var snapshot = new DiseaseIdSet(0b1011UL); // 版本化整数 ordinal 位集(非 FixSet)
            int beforeAll = _bag.Stream.Count;
            int beforeCase = CaseStreamCount();

            var result = _writer.TryOpen(openTick, _patient, snapshot);

            // ── 结果码 + 恰增 1 条 ──
            Assert.AreEqual(CaseOpenWriteResult.Opened, result, "首次立案须真正写入");
            Assert.AreEqual(beforeAll + 1, _bag.Stream.Count, "真流总事件数恰增 1");
            Assert.AreEqual(beforeCase + 1, CaseStreamCount(), "病例流恰增 1 条");

            // ── 事件头 ──
            var evt = _bag.Stream.Events[_bag.Stream.Count - 1];
            Assert.AreEqual(EventKind.CaseOpened, evt.Kind, "Kind = CaseOpened");
            Assert.AreEqual(StreamId.Case, StreamRouting.Of(evt.Kind), "落病例流(ADR-008 §一)");
            Assert.AreEqual(_patient, evt.Patient,
                "事件头 Patient = 病例所属病人(非 None —— case_id 与高水位都依赖它)");
            Assert.AreEqual(openTick, evt.Tick, "立案 tick = 本事件自身 Tick(载荷不落 opened_tick)");
            Assert.AreEqual(0, evt.Seq, "Seq 由发号器给出(该 (Tick, Patient) 首号 = 0)");

            // ── 载荷 round-trip(字节真进池)──
            Assert.IsTrue(
                PayloadCodec.TryGetPayload<CaseOpenedPayload>(evt, _bag.BlobPool, out var decoded),
                "载荷须能经池取回(手搓伪引用会在此 false)");
            Assert.AreEqual(_patient.Value, decoded.PatientId, "payload.patient_id");
            Assert.AreEqual(snapshot, decoded.DiseaseSnapshot, "payload.disease_snapshot");

            // ── case_id 只读派生(registry: case_id 不落字段)──
            Assert.IsTrue(_bag.BlobPool.TryGetBlob(evt.Payload.BlobId, out var bytes),
                "BlobId 须指向池内真字节");
            // 载荷 = patient_id(i32)+ 位集(变长整数)—— 恒不含 (Tick, Patient, Seq) 三元组;
            // 长度上限远小于 16 字节 ⇒ 不可能藏下三个 64 位字段。
            Assert.Less(evt.Payload.Length, 16,
                "载荷只含两字段,不得把 case_id / opened_tick 重复塞进去");

            // 派生 case_id 与流查询一致
            var derived = CaseStreamQuery.OpenCaseOf(_bag.Stream.Events, _patient.Value);
            Assert.IsTrue(derived.HasValue, "流前缀查询须看得见这条开案");
            Assert.AreEqual(new CaseId(openTick, _patient.Value, 0), derived.Value,
                "case_id = (Tick, Patient, Seq) 三元组(ADR-008 §二)");
        }

        // ══════════ ② 前置:同一病人至多一个开案(AC-37-26)══════════

        [Test]
        public void test_caseOpened_secondOpen_returnsExisting_noWrite()
        {
            Assert.AreEqual(CaseOpenWriteResult.Opened,
                _writer.TryOpen(5, _patient, new DiseaseIdSet(1)), "首次须开案");
            int afterFirst = _bag.Stream.Count;
            int afterFirstCase = CaseStreamCount();

            var second = _writer.TryOpen(6, _patient, new DiseaseIdSet(1));

            Assert.AreEqual(CaseOpenWriteResult.ReturnedExisting, second,
                "第二次交互不是新案,是回到已开的那一例(GDD 规则二 / AC-37-26)");
            Assert.AreEqual(afterFirst, _bag.Stream.Count, "零写入");
            Assert.AreEqual(afterFirstCase, CaseStreamCount(), "病例流零写入");
        }

        [Test]
        public void test_caseOpened_absentPatient_noWrite()
        {
            // 未灌在场:「病人存在」前置不满足(药材短缺 / 离场同型)
            var absent = _bag.PatientSpawner.SpawnNext(diseaseId: 0, tick: SpawnTick);
            Assert.IsFalse(_bag.Presence.IsPresent(absent), "前置:该病人确不在场");

            int before = _bag.Stream.Count;
            var result = _writer.TryOpen(5, absent, new DiseaseIdSet(1));

            Assert.AreEqual(CaseOpenWriteResult.PatientAbsent, result);
            Assert.AreEqual(before, _bag.Stream.Count, "零写入");
        }

        [Test]
        public void test_caseOpened_noneSentinel_noWrite()
        {
            int before = _bag.Stream.Count;
            var result = _writer.TryOpen(5, PatientId.None, new DiseaseIdSet(1));

            Assert.AreEqual(CaseOpenWriteResult.NoPatient, result,
                "哨兵 / 负 id 不得开出病例(否则污染 case_id 与 max(patient_id))");
            Assert.AreEqual(before, _bag.Stream.Count, "零写入");
        }

        [Test]
        public void test_caseOpened_negativeTick_throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => _writer.TryOpen(-1, _patient, new DiseaseIdSet(1)),
                "tick 入参非法 = 编程错误,fail-loud(与 PatientSpawner 同口径)");
        }

        // ══════════ ③ 复诊:前案已结 ⇒ 可再开新案(规则三串接)══════════

        [Test]
        public void test_caseOpened_afterClose_opensSecondCase_samePatient()
        {
            Assert.AreEqual(CaseOpenWriteResult.Opened,
                _writer.TryOpen(5, _patient, new DiseaseIdSet(1)), "首案");

            // 结案事件:CaseClosed 写者不在本批(豁免表登记)⇒ 测试内按同一生产路径补一条
            var closePayload = new CaseClosedPayload(
                _patient.Value, new CaseId(5, _patient.Value, 0), new DiseaseIdSet(1), false);
            _bag.EventSink.Append(new SimEvent(
                9, _patient, -1, EventKind.CaseClosed,
                _bag.Encoder.Encode(EventKind.CaseClosed, closePayload)));

            int before = _bag.Stream.Count;
            var reopen = _writer.TryOpen(10, _patient, new DiseaseIdSet(1));

            Assert.AreEqual(CaseOpenWriteResult.Opened, reopen,
                "结案后复诊 = 新案(串接键 patient_id,各案独立 —— GDD 规则三)");
            Assert.AreEqual(before + 1, _bag.Stream.Count, "恰增 1 条");
            var derived = CaseStreamQuery.OpenCaseOf(_bag.Stream.Events, _patient.Value);
            Assert.IsTrue(derived.HasValue);
            Assert.AreEqual(10, derived.Value.Tick, "新案的 case_id 取新立案 tick");
        }

        // ══════════ 注入面 fail-loud ══════════

        [Test]
        public void test_caseOpenWriter_nullDependency_throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => new CaseOpenWriter(null, _bag.Presence, _bag.Encoder, _bag.Stream.Events));
            Assert.Throws<ArgumentNullException>(
                () => new CaseOpenWriter(_bag.EventSink, null, _bag.Encoder, _bag.Stream.Events));
            Assert.Throws<ArgumentNullException>(
                () => new CaseOpenWriter(_bag.EventSink, _bag.Presence, null, _bag.Stream.Events));
            Assert.Throws<ArgumentNullException>(
                () => new CaseOpenWriter(_bag.EventSink, _bag.Presence, _bag.Encoder, null));
        }
    }
}
