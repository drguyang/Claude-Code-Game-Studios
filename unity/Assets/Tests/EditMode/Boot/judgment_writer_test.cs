// sprint-06 G2.1 · 落笔写者判据(EditMode · 2026-10-11)
//
// 判据(可证伪):
//   ① 有开案病人落笔 ⇒ JudgmentRecorded 入病例流(载荷:patient_id / case_id /
//      author_player_id / lexicon_id / confidence 五字段全);
//   ② 无开案病人落笔 ⇒ NoOpenCase 返回码,零写入(病例都没开无从落笔);
//   ③ 载荷 case_id = 开案的 CaseId(不是臆造词典);
//   ④ 落**病例**流(StreamId.Case),non disease(病史流) —— 流路由配合断言。
//
// 纯逻辑 EditMode:真 EventStream + 真 CaseOpenWriter(写开案)+ 真 PayloadEncoder,
// 全生产路径。照 CaseOpenWriter 测试先例。

using System;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.DiseaseSimulation;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Boot
{
    /// <summary><see cref="JudgmentWriter"/> 判据(G2.1)。</summary>
    internal sealed class JudgmentWriterTest
    {
        private CompositionRootServices _bag;
        private JudgmentWriter _writer;

        [SetUp]
        public void SetUp()
        {
            _bag = CompositionRoot.Assemble();
            _writer = new JudgmentWriter(_bag.EventSink, _bag.Encoder, _bag.Stream.Events,
                                         authorPlayerId: 9001);
        }

        [Test]
        public void test_record_onOpenCase_writesJudgment_caseStream()
        {
            // 建档 + 登记在场(SpawnNext 只写 DiseaseOnset;在场是开案的 AC-15 前置)+ 开案
            PatientId patient = _bag.PatientSpawner.SpawnNext(diseaseId: 1, tick: 0);
            _bag.Presence.AddPresent(patient, new WorldPos(0, 0, 0));
            _bag.VitalsService.OnTickEdge(0);
            var caseWriter = new CaseOpenWriter(_bag.EventSink, _bag.Presence, _bag.Encoder, _bag.Stream.Events);
            var openRes = caseWriter.TryOpen(1, patient, default);
            Assert.AreEqual(CaseOpenWriteResult.Opened, openRes, "开案须成功(在场前置齐)");

            int before = _bag.Stream.Count;

            // Act:落笔
            JudgmentWriteResult r = _writer.TryRecord(tick: 3, patient, lexiconId: 7, confidence: 2);

            // Assert ①:Recorded + 病例流多一条
            Assert.AreEqual(JudgmentWriteResult.Recorded, r);
            Assert.AreEqual(before + 1, _bag.Stream.Count);
            SimEvent tail = _bag.Stream.Events[_bag.Stream.Count - 1];
            Assert.AreEqual(EventKind.JudgmentRecorded, tail.Kind, "尾事件 = JudgmentRecorded");

            // Assert ②:落病例流(流路由纯函数:JudgmentRecorded → Case)
            Assert.AreEqual(StreamId.Case,
                StreamRouting.Of(tail.Kind), "JudgmentRecorded 落病例流(StreamRouting.g.cs 纯函数)");
        }

        [Test]
        public void test_record_payload_roundtripsFiveFields_caseIdIsOpenCase()
        {
            PatientId patient = _bag.PatientSpawner.SpawnNext(diseaseId: 1, tick: 0);
            _bag.Presence.AddPresent(patient, new WorldPos(0, 0, 0));
            _bag.VitalsService.OnTickEdge(0);
            var caseWriter = new CaseOpenWriter(_bag.EventSink, _bag.Presence, _bag.Encoder, _bag.Stream.Events);
            Assert.AreEqual(CaseOpenWriteResult.Opened,
                caseWriter.TryOpen(1, patient, default), "开案须成功");

            _writer.TryRecord(tick: 3, patient, lexiconId: 7, confidence: 2);

            SimEvent tail = _bag.Stream.Events[_bag.Stream.Count - 1];
            Assert.IsTrue(PayloadCodec.TryGetPayload(in tail, _bag.BlobPool,
                out JudgmentRecordedPayload p), "载荷解码成功(字段 roundtrip)");
            Assert.AreEqual(patient.Value, p.PatientId, "patient_id");
            Assert.AreEqual(7, p.LexiconId, "lexicon_id");
            Assert.AreEqual(2, p.Confidence, "confidence");
            Assert.AreEqual(9001, p.AuthorPlayerId, "author_player_id");
            // case_id = 开案的那个(载荷编码自 OpenCaseOf 查询结果,非臆造)
            CaseId expected = CaseStreamQuery.OpenCaseOf(_bag.Stream.Events, patient.Value).Value;
            Assert.AreEqual(expected, p.CaseId, "载荷 case_id = 开案 CaseId");
        }

        [Test]
        public void test_record_noOpenCase_returnsNoOpenCase_noWrite()
        {
            PatientId patient = _bag.PatientSpawner.SpawnNext(diseaseId: 1, tick: 0);
            _bag.VitalsService.OnTickEdge(0);
            // 不开案(直接落笔)

            int before = _bag.Stream.Count;
            JudgmentWriteResult r = _writer.TryRecord(tick: 2, patient, lexiconId: 1, confidence: 1);

            Assert.AreEqual(JudgmentWriteResult.NoOpenCase, r, "无开案 ⇒ NoOpenCase(具名不写)");
            Assert.AreEqual(before, _bag.Stream.Count, "零写入");
        }

        [Test]
        public void test_record_negativeTick_failsLoud()
        {
            PatientId patient = _bag.PatientSpawner.SpawnNext(diseaseId: 1, tick: 0);
            Assert.Throws<ArgumentOutOfRangeException>(
                () => _writer.TryRecord(tick: -1, patient, lexiconId: 1, confidence: 1),
                "tick<0 = 编程错误,fail-loud");
        }
    }
}