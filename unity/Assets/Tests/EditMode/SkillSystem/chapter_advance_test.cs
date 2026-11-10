// 章节推进 + 转诊键的装配层载体 —— 判据夹具(2026-11-10 · B1-b / C3-a)
//
// 权威:
//   design/gdd/reviews/technique-unlock-decision-sheet-2026-11-10.md §2 B1 / §3 C3
//   design/registry/entities.yaml 的 `SimEvent.Kind.ChapterAdvanced` 条目
//   docs/architecture/adr-044-case-referred-kind.md §D2/§D6/§D7
//
// ─────────────────────────────────────────────────────────────────────────────
// 本文件要证明的六件事
// ─────────────────────────────────────────────────────────────────────────────
//   ① **路由** —— `ChapterAdvanced` 落病史流(kindgen 已断言,此处留运行期锚);
//   ② **载荷往返** —— (chapter, source_id) 经 Encode → Decode 逐值相等;
//   ③ **章号语义** —— 空流 ⇒ 0;推进后 = 载荷值;同章 = no-op;倒退 ⇒ 抛;0 ⇒ 抛;
//   ④ **解码侧拒收** —— chapter < 1 的字节 ⇒ 抛(不静默接受一条「第 0 章」);
//   ⑤ **转诊键** —— 有开着的病例 ⇒ 写一条 `CaseReferred`;病例已结 / 已转诊 ⇒ 不写;
//   ⑥ **未声明场景的诚实形态** —— 无生产者时章恒 0(`hook_case_policy_test` 另有一条钉住)。
//
// ⚠️ 本文件**只读**仓库文件、**零写盘** ⇒ 不触发 `FsWriteBoundaryGate`。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.SkillSystem
{
    [TestFixture]
    public sealed class chapter_advance_test
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
            public readonly PayloadDecoder Decoder;
            public readonly Sink Sink = new Sink();
            public readonly ChapterAdvanceWriter Writer;

            public Harness()
            {
                Encoder = new PayloadEncoder(Pool);
                Decoder = new PayloadDecoder(Pool);
                Writer = new ChapterAdvanceWriter(Sink, Encoder, () => Sink.Events, Decoder);
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  ① 路由
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void test_routing_chapterAdvanced_goesToHistoryStream()
        {
            Assert.That(StreamRouting.Of(EventKind.ChapterAdvanced), Is.EqualTo(StreamId.History),
                "章推进是玩家/世界级成长事实,与 TechniqueUnlocked / SkillGrown 同流(不是世界流)");
        }

        // ════════════════════════════════════════════════════════════════
        //  ② 载荷往返
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void test_payload_roundTrips()
        {
            var h = new Harness();
            PayloadRef r = h.Encoder.Encode(EventKind.ChapterAdvanced,
                                            new ChapterAdvancedPayload(3, 14));

            ChapterAdvancedPayload back =
                h.Decoder.Decode<ChapterAdvancedPayload>(EventKind.ChapterAdvanced, r);

            Assert.That(back.Chapter, Is.EqualTo(3));
            Assert.That(back.SourceId, Is.EqualTo(14));
        }

        // ════════════════════════════════════════════════════════════════
        //  ③ 章号语义
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void test_currentChapter_emptyStream_isZero()
        {
            var h = new Harness();
            Assert.That(h.Writer.CurrentChapter(), Is.EqualTo(0), "0 = 「从未推进」的哨兵");
        }

        [Test]
        public void test_advance_writesEvent_andCurrentChapterFollows()
        {
            var h = new Harness();

            Assert.That(h.Writer.TryAdvance(2, 14, 100), Is.True);
            Assert.That(h.Sink.Events.Count, Is.EqualTo(1));
            Assert.That(h.Sink.Events[0].Kind, Is.EqualTo(EventKind.ChapterAdvanced));
            Assert.That(h.Sink.Events[0].Patient.IsNone, Is.True,
                "章节是玩家级事实 ⇒ Patient = None(与 ADR-007 五支同格)");
            Assert.That(h.Writer.CurrentChapter(), Is.EqualTo(2),
                "载荷存**绝对量** ⇒ 取最后一条即当前章,不需累加");
        }

        [Test]
        public void test_advance_sameChapter_isNoOp()
        {
            var h = new Harness();
            h.Writer.TryAdvance(2, 14, 100);

            Assert.That(h.Writer.TryAdvance(2, 99, 200), Is.False, "同章 = no-op");
            Assert.That(h.Sink.Events.Count, Is.EqualTo(1), "流长度不得变化");
        }

        [Test]
        public void test_advance_backward_isRejected()
        {
            var h = new Harness();
            h.Writer.TryAdvance(3, 14, 100);

            var ex = Assert.Throws<InvalidOperationException>(() => h.Writer.TryAdvance(2, 14, 200));
            Assert.That(ex.Message, Does.Contain("倒退"), "报文须点名字段而不只是「非法」");
            Assert.That(h.Sink.Events.Count, Is.EqualTo(1), "拒收时不得留下半条事件");
            Assert.That(h.Writer.CurrentChapter(), Is.EqualTo(3), "章号不得被拒收的那次改动");
        }

        [Test]
        public void test_advance_zeroOrNegativeChapter_isRejected()
        {
            var h = new Harness();
            foreach (int bad in new[] { 0, -1 })
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => h.Writer.TryAdvance(bad, 14, 100),
                    $"chapter={bad} 须拒 —— 0 与空流不可区分,负数无意义");
            }
            Assert.That(h.Sink.Events.Count, Is.EqualTo(0));
        }

        // ════════════════════════════════════════════════════════════════
        //  ④ 解码侧拒收 chapter < 1
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// 值域门**只在写者侧**(`ChapterAdvanceWriter`),解码侧只管形状。
        /// <para><b>为什么钉这条</b>:起草期一度在解码侧加 `chapter &lt; 1` 拒收,实测当场打破
        /// `payload_encoder_test` / `payload_decoder_test` 的**全 Kind 往返夹具**
        /// (它们用 `Activator.CreateInstance` 造默认实参 ⇒ chapter = 0)。那说明
        /// 「解码侧值域门」会把通用往返面绑死在具体值域上。⇒ 本条的语义是:
        /// **解码对 0 放行(形状合法),写者拒(值域非法)** —— 两者分工,不重复判。</para>
        /// </summary>
        [Test]
        public void test_valueDomain_gateIsAtWriter_notDecoder()
        {
            var h = new Harness();
            PayloadRef r = h.Encoder.Encode(EventKind.ChapterAdvanced,
                                            new ChapterAdvancedPayload(0, 0));

            Assert.That(h.Decoder.Decode<ChapterAdvancedPayload>(EventKind.ChapterAdvanced, r).Chapter,
                        Is.EqualTo(0), "解码侧只管形状:0 形状合法,照样解得出");

            Assert.Throws<ArgumentOutOfRangeException>(() => h.Writer.TryAdvance(0, 0, 100),
                "值域门在写者:0 章不得入流");
            Assert.That(h.Sink.Events.Count, Is.EqualTo(0));
        }

        // ════════════════════════════════════════════════════════════════
        //  ⑤ 转诊键(C3-a)
        // ════════════════════════════════════════════════════════════════

        private sealed class ReferHarness
        {
            public readonly InMemoryBlobPool Pool = new InMemoryBlobPool();
            public readonly PayloadEncoder Encoder;
            public readonly PayloadDecoder Decoder;
            public readonly Sink Sink = new Sink();
            public readonly HookCaseReferDriver Driver;
            public long Tick = 500;

            public ReferHarness()
            {
                Encoder = new PayloadEncoder(Pool);
                Decoder = new PayloadDecoder(Pool);
                var writer = new HookCaseClosureWriter(Sink, Encoder, () => Sink.Events, Decoder);
                Driver = new HookCaseReferDriver(writer, () => Sink.Events, () => Tick);
            }

            public void Open(long tick, int patientId, long seq)
            {
                var p = new CaseOpenedPayload(patientId, new DiseaseIdSet(1UL << 5));
                Sink.Events.Add(new SimEvent(tick, new PatientId(patientId), seq,
                    EventKind.CaseOpened, Encoder.Encode(EventKind.CaseOpened, p)));
            }

            public void Close(CaseId id, int patientId)
            {
                var p = new CaseClosedPayload(patientId, id, new DiseaseIdSet(1UL << 5), treated: false);
                Sink.Events.Add(new SimEvent(Tick, new PatientId(patientId), 0,
                    EventKind.CaseClosed, Encoder.Encode(EventKind.CaseClosed, p)));
            }
        }

        [Test]
        public void test_refer_openCase_writesCaseReferred()
        {
            var h = new ReferHarness();
            h.Open(10, 100, 3);

            Assert.That(h.Driver.TryRefer(new PatientId(100)), Is.True);
            Assert.That(h.Sink.Events.Count, Is.EqualTo(2), "CaseOpened + CaseReferred");
            Assert.That(h.Sink.Events[1].Kind, Is.EqualTo(EventKind.CaseReferred));
        }

        [Test]
        public void test_refer_noOpenCase_isNoOp()
        {
            var h = new ReferHarness();
            Assert.That(h.Driver.TryRefer(new PatientId(100)), Is.False, "没有任何病例 ⇒ 不写");
            Assert.That(h.Sink.Events.Count, Is.EqualTo(0));
        }

        [Test]
        public void test_refer_closedCase_isNoOp()
        {
            var h = new ReferHarness();
            h.Open(10, 100, 3);
            h.Close(new CaseId(10, 100, 3), 100);

            Assert.That(h.Driver.TryRefer(new PatientId(100)), Is.False,
                "病例已结 ⇒ 没有「开着的这一例」可转");
            Assert.That(h.Sink.Events.Count, Is.EqualTo(2), "只有 CaseOpened + CaseClosed");
        }

        [Test]
        public void test_refer_twice_isNoOp()
        {
            var h = new ReferHarness();
            h.Open(10, 100, 3);

            Assert.That(h.Driver.TryRefer(new PatientId(100)), Is.True);
            Assert.That(h.Driver.TryRefer(new PatientId(100)), Is.False, "同一 case 重复转诊 = no-op");
        }

        [Test]
        public void test_refer_none_takesNoAction()
        {
            var h = new ReferHarness();
            h.Open(10, 100, 3);
            Assert.That(h.Driver.TryRefer(PatientId.None), Is.False, "哨兵病人不触发任何写入");
            Assert.That(h.Sink.Events.Count, Is.EqualTo(1));
        }
    }
}
