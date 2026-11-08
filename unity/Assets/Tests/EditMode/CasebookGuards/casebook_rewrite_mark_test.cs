// 39 · 改判留痕的**读时投影**判据(2026-11-08 · S26-4 补件)
//
// 权威:
//   design/gdd/casebook.md 规则六(改判留痕 · 痕**不计分**)
//   系统 39 `AC-39-12`「「旧」态灰度仍可辨(**非色单载**)」—— 屏上要有一个**形状**承载它
//   系统 37 `AC-37-05`(J(c) = 该案全序最后一条判断事件,存新值)
//   `AC-39-13`(痕 = 病例流**读时**投影,不得做成写侧状态位)
//
// 被测对象 = `CaseStreamQuery.IsCurrentJudgmentRewritten`(**生产函数**,不是测试自造谓词)。
// ⚠️ 反证设计见每条 test 的注释;另:本文件**不**断言「屏怎么画」——
//    那是 PlayMode `casebook_rewrite_mark_test`,两件事不合并(否则一处坏两边一起红,定位差)。

using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.CasebookGuards
{
    /// <summary>`CaseStreamQuery.IsCurrentJudgmentRewritten` 的五种形态(2026-11-08)。</summary>
    [TestFixture]
    public sealed class CasebookRewriteMarkQueryTest
    {
        private static readonly InMemoryBlobPool SharedPool = new InMemoryBlobPool();
        private static readonly PayloadEncoder Encoder = new PayloadEncoder(SharedPool);
        private static readonly PayloadDecoder Decoder = new PayloadDecoder(SharedPool);

        private static readonly PatientId Patient = new PatientId(7);

        /// <summary>本案的 case_id(由一条 CaseOpened 的 Tick/Seq 决定,测试内固定)。</summary>
        private static readonly CaseId Case =
            new CaseId(100, Patient.Value, 0);

        private static SimEvent Recorded(long tick, long seq, int lexiconId, CaseId? at = null)
        {
            CaseId c = at ?? Case;
            var p = new JudgmentRecordedPayload(c.Patient, c, authorPlayerId: 1,
                                               lexiconId: lexiconId, confidence: 1);
            return new SimEvent(tick, Patient, seq, EventKind.JudgmentRecorded,
                                Encoder.EncodeJudgmentRecorded(p, string.Empty));
        }

        private static SimEvent Revised(long tick, long seq, int lexiconId, CaseId? at = null)
        {
            CaseId c = at ?? Case;
            var p = new JudgmentRevisedPayload(c.Patient, c, authorPlayerId: 1,
                                              lexiconId: lexiconId, confidence: 2);
            return new SimEvent(tick, Patient, seq, EventKind.JudgmentRevised,
                                Encoder.EncodeJudgmentRevised(p, string.Empty));
        }

        private static bool Marked(params SimEvent[] events) =>
            CaseStreamQuery.IsCurrentJudgmentRewritten(
                (IReadOnlyList<SimEvent>)events, Case, Decoder);

        /// <summary>空流 ⇒ 无痕(负控;若实现写成「没判断也返回 true」⇒ 红)。</summary>
        [Test]
        public void test_noJudgment_noMark()
        {
            Assert.That(Marked(), Is.False);
        }

        /// <summary>只有一次落笔(Recorded)⇒ 无痕 —— 「新起一笔」不标。</summary>
        [Test]
        public void test_recordedOnly_noMark()
        {
            Assert.That(Marked(Recorded(200, 0, lexiconId: 5)), Is.False);
        }

        /// <summary>
        /// 落笔后改判(非空)⇒ **有痕**。
        /// <para>反证:把 `marked = p.LexiconId != 0` 改成 `marked = false` ⇒ 本条红。</para>
        /// </summary>
        [Test]
        public void test_revisedNonEmpty_marks()
        {
            Assert.That(Marked(Recorded(200, 0, lexiconId: 5),
                               Revised(260, 1, lexiconId: 9)), Is.True);
        }

        /// <summary>
        /// 改判后又**新起一笔**(Recorded)⇒ 痕清除。
        /// <para>反证:删掉 `marked = false` 那行(Recorded 不清痕)⇒ 本条红。</para>
        /// </summary>
        [Test]
        public void test_revisedThenRecorded_clearsMark()
        {
            Assert.That(Marked(Recorded(200, 0, lexiconId: 5),
                               Revised(260, 1, lexiconId: 9),
                               Recorded(320, 2, lexiconId: 11)), Is.False);
        }

        /// <summary>
        /// **擦除**(改判成空判断 `lexicon_id = 0`)⇒ 无痕 —— 病名栏已空,没有可标的「旧」。
        /// <para>反证:把 `p.LexiconId != 0` 改成 `true`(任何改判都标)⇒ 本条红。</para>
        /// </summary>
        [Test]
        public void test_revisedToEmpty_eraseNoMark()
        {
            Assert.That(Marked(Recorded(200, 0, lexiconId: 5),
                               Revised(260, 1, lexiconId: 0)), Is.False);
        }

        /// <summary>
        /// **别的案**的改判不标本案(归属按载荷自带的 `CaseId`,不是流序)。
        /// <para>反证:把两处 `if (!...CaseId.Equals(caseId)) continue;` 去掉(退回流序口径)
        /// ⇒ 本条红。</para>
        /// </summary>
        [Test]
        public void test_otherCaseRevised_doesNotMarkThisCase()
        {
            var other = new CaseId(150, Patient.Value, 3);
            Assert.That(Marked(Recorded(200, 0, lexiconId: 5),
                               Revised(260, 1, lexiconId: 9, at: other)), Is.False);
        }
    }
    /// <summary>
    /// **接线判据**(全生产装配路径):`CasebookVitalsSource` 真的实现了能力接口,
    /// 且其读数随病例流变化 —— 防「接口写了、装配层没接」这类 F2 假绿。
    /// <para>与 `InkAgeTest` 同一装配源(`CompositionRoot.Assemble()`),不另造依赖。</para>
    /// </summary>
    [TestFixture]
    public sealed class CasebookRewriteMarkWiringTest
    {
        private CompositionRootServices _bag;
        private CasebookVitalsSource _source;
        private JudgmentWriter _writer;
        private PatientId _patient;

        [SetUp]
        public void SetUp()
        {
            _bag = CompositionRoot.Assemble();
            _source = _bag.CasebookSource;
            _writer = new JudgmentWriter(_bag.EventSink, _bag.Encoder, _bag.Stream.Events,
                                         authorPlayerId: 9001);

            _patient = _bag.PatientSpawner.SpawnNext(diseaseId: 1, tick: 0);
            _bag.Presence.AddPresent(_patient, new WorldPos(0, 0, 0));
            _bag.VitalsService.OnTickEdge(0);
            var caseWriter = new CaseOpenWriter(_bag.EventSink, _bag.Presence, _bag.Encoder,
                                               _bag.Stream.Events);
            Assert.AreEqual(CaseOpenWriteResult.Opened, caseWriter.TryOpen(1, _patient, default),
                "落笔前置:开案须成功");
            _source.ChangePatient(_patient);
        }

        [Test]
        public void test_productionSource_implementsRewriteMarkCapability()
        {
            Assert.That(_source, Is.InstanceOf<ICasebookRewriteMarkSource>(),
                "42 侧靠能力接口取痕 —— 装配层不实现 ⇒ 屏永远无痕(且不报错,静默)");
        }

        [Test]
        public void test_recordOnly_noMark_thenRevise_marks_thenErase_clears()
        {
            Assert.AreEqual(JudgmentWriteResult.Recorded,
                _writer.TryRecord(tick: 1, patient: _patient, lexiconId: 7, confidence: 2));
            Assert.That(_source.HasRewriteMark, Is.False, "只落笔一次 ⇒ 无痕");

            Assert.AreEqual(JudgmentWriteResult.Recorded,
                _writer.TryRevise(tick: 2, patient: _patient, lexiconId: 9, confidence: 2));
            Assert.That(_source.HasRewriteMark, Is.True, "改判(非空)⇒ 有痕");

            Assert.AreEqual(JudgmentWriteResult.Recorded,
                _writer.TryRevise(tick: 3, patient: _patient, lexiconId: 0, confidence: 0));
            Assert.That(_source.HasRewriteMark, Is.False, "擦除 ⇒ 痕清除(病名栏已空,无可标的「旧」)");
        }

        [Test]
        public void test_unboundPatient_noMark()
        {
            _source.ChangePatient(PatientId.None);
            Assert.That(_source.HasRewriteMark, Is.False,
                "无绑定病人 ⇒ 无痕(零假数据;不因别的病人改过口而亮)");
        }
    }
}