// sprint-08 S1.1 件 F2/G · 脉案命令路由判据(2026-10-12)
//
// 判据(可证伪):
//   ① 指认一条词 ⇒ 病例流新增 JudgmentRecorded(lexiconId = 该词, confidence = 疑似);
//   ② 同词再 Submit ⇒ JudgmentRevised(confidence = 确定);三态循环的第二态;
//   ③ 第三次 ⇒ JudgmentRevised(lexicon_id = 0, confidence = 0)= 擦除回空
//      (case-system.md:241「空判断合法」—— 擦除是写空判断,不是删事件);
//   ④ 循环恰三态:再落第四笔 ⇒ 回到「空 → 首落笔」路径(零第四档);
//   ⑤ 档位可经 DTO 回读(PendingConfidence 0/1/2 —— 42 只读不持);
//   ⑥ 换词:空态指认新词 ⇒ Recorded;已落笔指认新词 ⇒ Revised(改判);
//   ⑦ 载荷可解码回读(编码通道真走通 —— F0 补的 EncodeJudgmentRevised);
//   ⑧ fail-loud:未绑病人 / 指认 id ≤ 0。
//
// 全生产路径:真组合根 + 真 PatientSpawner + 真 CaseOpenedDriver(开案)+ 真 JudgmentWriter。

using System;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Presentation.Diagnosis;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Boot
{
    /// <summary><see cref="CasebookCommandRouter"/> 判据(sprint-08 S1.1 件 F2/G · OQ-CB-5 甲)。</summary>
    internal sealed class casebook_command_router_test
    {
        private const int PlayerId = 9001;
        private const int LexiconA = 7;
        private const int LexiconB = 9;

        private CompositionRootServices _bag;
        private PatientId _patient;

        /// <summary>建档 + 在场 + 开案(落笔前置:病人须有开案)。</summary>
        [SetUp]
        public void SetUp()
        {
            _bag = CompositionRoot.Assemble();
            _patient = _bag.PatientSpawner.SpawnNext(diseaseId: 1, tick: 0);
            _bag.Presence.AddPresent(_patient, new WorldPos(0, 0, 0));
            _bag.VitalsService.OnTickEdge(0);
            _bag.TickDriver.Advance(0.05d);                   // → tick 1(开案于 1)
            _bag.CaseOpenedDriver.OnTickEdge(_bag.TickDriver.CurrentTick);   // 开案(CaseOpened 入流)

            _bag.PrescriptionPage.SetAuthor(PlayerId);        // 作者(命令路由复用同一 Writer)
            _bag.CasebookCommands.BindPatient(_patient);
        }

        /// <summary>病例流内 Judgment* 事件条数 + 末条解码。</summary>
        private int JudgmentCount(out SimEvent last, out int lexiconId, out byte confidence)
        {
            int n = 0;
            last = default;
            lexiconId = 0;
            confidence = 0;
            foreach (var e in _bag.Stream.Events)
            {
                if (e.Kind != EventKind.JudgmentRecorded && e.Kind != EventKind.JudgmentRevised)
                    continue;
                n++;
                last = e;
                if (e.Kind == EventKind.JudgmentRecorded)
                {
                    Assert.IsTrue(PayloadCodec.TryGetPayload(in e, _bag.BlobPool,
                        out JudgmentRecordedPayload p), "Recorded 载荷可解码");
                    lexiconId = p.LexiconId; confidence = p.Confidence;
                }
                else
                {
                    Assert.IsTrue(PayloadCodec.TryGetPayload(in e, _bag.BlobPool,
                        out JudgmentRevisedPayload p), "Revised 载荷可解码(F0 编码通道)");
                    lexiconId = p.LexiconId; confidence = p.Confidence;
                }
            }
            return n;
        }

        // ══════════════════════════════════════════════════════════════════════
        // ① 首落笔 ⇒ JudgmentRecorded(疑似)
        // ══════════════════════════════════════════════════════════════════════
        [Test]
        public void test_firstPoint_writesJudgmentRecordedSuspected()
        {
            _bag.CasebookCommands.SubmitDiseaseName(LexiconA);

            int n = JudgmentCount(out var last, out int lex, out byte conf);
            Assert.AreEqual(1, n, "首落笔 ⇒ 恰一条判断事件");
            Assert.AreEqual(EventKind.JudgmentRecorded, last.Kind, "首落笔 = JudgmentRecorded");
            Assert.AreEqual(LexiconA, lex, "lexiconId = 指认的词(显式 id,非显示索引)");
            Assert.AreEqual((byte)JudgmentState.Suspected, conf, "首落 = 疑似(OQ-CB-5 甲)");
            Assert.AreEqual((byte)JudgmentState.Suspected, _bag.CasebookCommands.PendingConfidence,
                "档位推进到疑似");
        }

        // ══════════════════════════════════════════════════════════════════════
        // ② 同词再 Submit ⇒ JudgmentRevised(确定)
        // ══════════════════════════════════════════════════════════════════════
        [Test]
        public void test_secondSubmit_writesJudgmentRevisedConfirmed()
        {
            _bag.CasebookCommands.SubmitDiseaseName(LexiconA);
            _bag.CasebookCommands.SubmitConfidence();

            int n = JudgmentCount(out var last, out int lex, out byte conf);
            Assert.AreEqual(2, n, "第二次 ⇒ 共两条判断事件(Recorded + Revised)");
            Assert.AreEqual(EventKind.JudgmentRevised, last.Kind, "改判 = JudgmentRevised");
            Assert.AreEqual(LexiconA, lex, "改判仍指同一条词");
            Assert.AreEqual((byte)JudgmentState.Confirmed, conf, "第二档 = 确定");
        }

        // ══════════════════════════════════════════════════════════════════════
        // ③ 第三次 ⇒ 擦除(写空判断 lexicon_id = 0)
        // ══════════════════════════════════════════════════════════════════════
        [Test]
        public void test_thirdSubmit_erasesViaEmptyJudgment()
        {
            _bag.CasebookCommands.SubmitDiseaseName(LexiconA);
            _bag.CasebookCommands.SubmitConfidence();
            _bag.CasebookCommands.SubmitConfidence();

            int n = JudgmentCount(out var last, out int lex, out byte conf);
            Assert.AreEqual(3, n, "第三次 ⇒ 共三条(擦除也是写一条新值,非删事件)");
            Assert.AreEqual(EventKind.JudgmentRevised, last.Kind, "擦除 = JudgmentRevised");
            Assert.AreEqual(0, lex, "擦除载荷 lexicon_id = 0(case-system.md:241 空判断)");
            Assert.AreEqual((byte)JudgmentState.Empty, conf, "擦除 confidence = 0");
            Assert.AreEqual((byte)JudgmentState.Empty, _bag.CasebookCommands.PendingConfidence,
                "档位回空");
        }

        // ══════════════════════════════════════════════════════════════════════
        // ④ 恰三态:第四笔回到首落笔路径
        // ══════════════════════════════════════════════════════════════════════
        [Test]
        public void test_fourthSubmit_wrapsToFirstPoint_noFourthState()
        {
            _bag.CasebookCommands.SubmitDiseaseName(LexiconA);
            _bag.CasebookCommands.SubmitConfidence();
            _bag.CasebookCommands.SubmitConfidence();
            _bag.CasebookCommands.SubmitDiseaseName(LexiconA);   // 擦除后 = 空 ⇒ 首落笔

            int n = JudgmentCount(out var last, out int lex, out byte conf);
            Assert.AreEqual(4, n, "第四笔 ⇒ 第四条");
            Assert.AreEqual(EventKind.JudgmentRecorded, last.Kind, "回到首落笔(Recorded)");
            Assert.AreEqual(LexiconA, lex, "指认同一条词");
            Assert.AreEqual((byte)JudgmentState.Suspected, conf, "档位回疑似(零第四档)");
        }

        // ══════════════════════════════════════════════════════════════════════
        // ⑤ 档位经 DTO 回读(42 只读不持)
        // ══════════════════════════════════════════════════════════════════════
        [Test]
        public void test_pendingConfidence_readableThroughDtoSource()
        {
            _bag.CasebookSource.ChangePatient(_patient);   // 两处病人同步(含路由)
            Assert.AreEqual(0, _bag.CasebookSource.PendingConfidence, "未落笔 ⇒ 档位 0");

            _bag.CasebookCommands.SubmitDiseaseName(LexiconA);
            Assert.AreEqual((byte)JudgmentState.Suspected, _bag.CasebookSource.PendingConfidence,
                "落笔后 DTO 回读 = 疑似(42 经此读,不持档位)");
        }

        // ══════════════════════════════════════════════════════════════════════
        // ⑥ 换词:空态指认新词 ⇒ Recorded;已落笔指认新词 ⇒ Revised
        // ══════════════════════════════════════════════════════════════════════
        [Test]
        public void test_switchLexicon_emptyIsRecorded_afterPointIsRevised()
        {
            _bag.CasebookCommands.SubmitDiseaseName(LexiconA);   // 首落笔
            _bag.CasebookCommands.SubmitDiseaseName(LexiconB);   // 换词(已落笔 ⇒ 改判)

            int n = JudgmentCount(out var last, out int lex, out byte conf);
            Assert.AreEqual(2, n, "换词 ⇒ 第二条事件");
            Assert.AreEqual(EventKind.JudgmentRevised, last.Kind, "已落笔换词 = 改判");
            Assert.AreEqual(LexiconB, lex, "改判到新词");
            Assert.AreEqual((byte)JudgmentState.Suspected, conf, "新词第一笔 = 疑似");

            // 擦除后再指认新词 ⇒ 从空起笔(Recorded)
            _bag.CasebookCommands.SubmitConfidence();            // 疑似 ⇒ 确定
            _bag.CasebookCommands.SubmitConfidence();            // 确定 ⇒ 擦除
            _bag.CasebookCommands.SubmitDiseaseName(LexiconA);
            JudgmentCount(out last, out lex, out conf);
            Assert.AreEqual(EventKind.JudgmentRecorded, last.Kind,
                "擦除后(空态)指认 ⇒ 从空起笔(Recorded,非改判)");
        }

        // ══════════════════════════════════════════════════════════════════════
        // ⑦ 病人换绑 ⇒ 档位复位(上一病人的档不串到新病人)
        // ══════════════════════════════════════════════════════════════════════
        [Test]
        public void test_rebindPatient_resetsPendingState()
        {
            _bag.CasebookCommands.SubmitDiseaseName(LexiconA);
            Assert.AreEqual((byte)JudgmentState.Suspected, _bag.CasebookCommands.PendingConfidence);

            var other = _bag.PatientSpawner.SpawnNext(diseaseId: 1, tick: 2);
            _bag.CasebookCommands.BindPatient(other);

            Assert.AreEqual(0, _bag.CasebookCommands.PendingConfidence,
                "换病人 ⇒ 档位复位(不串病人)");
            Assert.AreEqual(0, _bag.CasebookCommands.CurrentLexiconId, "当前词复位");
        }

        // ══════════════════════════════════════════════════════════════════════
        // ⑧ fail-loud
        // ══════════════════════════════════════════════════════════════════════
        [Test]
        public void test_unboundPatient_failLoud()
        {
            var fresh = CompositionRoot.Assemble();
            fresh.PrescriptionPage.SetAuthor(PlayerId);
            Assert.Throws<InvalidOperationException>(
                () => fresh.CasebookCommands.SubmitDiseaseName(LexiconA),
                "未绑病人 ⇒ fail-loud(无对象无从落笔)");
        }

        [Test]
        public void test_nonPositiveLexiconId_failLoud()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => _bag.CasebookCommands.SubmitDiseaseName(0),
                "指认 id = 0(空栏哨兵)⇒ fail-loud");
        }
    }
}
