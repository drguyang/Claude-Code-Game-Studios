// case-system Story 002 测试 —— 立案两路径 · 开案唯一 · 判断记录事件化
//
// AC-37-30:路径 A 与路径 B 各产生 CaseOpened 入病例流 —— 入流义务不因触发者不同而不同
// AC-37-26:同一病人同时至多一个开案;第二次就诊交互不产生新 CaseOpened
// 药材短缺注入 ⇒ 零 CaseOpened(不立案的负断言)
// AC-37-05:判断记录落病例流,无内存私有副本;存新值:J(c) = 全序最后一条判断事件
// 改写史全序列在病例流保留(AC-37-23 的事件面)
// Judgment 类型形状:lexicon_id/confidence/freehand_text 三字段,判定路径只读前两者
//
// 权威来源:
//   ADR-008(病例事件流;五 Kind 落病例流;case_id 三元组;跨流全序)
//   ADR-009 §二(模拟态判据 —— 入流义务与触发者无关)
//   GDD 规则二(立案 A/B 两路径 + 因果订正 + 开案唯一)
//   GDD 规则四(判断记录/存新值/Judgment 形状)
//
// ⚠️ 本测试为纯数据层:不触引擎 API,不触 codec,不触持久化。
//    被测对象 = Sim 的 CaseOpenDecider / CaseStreamQuery(生产代码)。

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.CaseSystem
{
    /// <summary>case-system Story 002 —— 立案两路径 · 开案唯一 · 判断记录事件化。</summary>
    [TestFixture]
    public sealed class CaseOpenAndJudgmentTest
    {
        // ════════════════════════════════════════════════════════════════════
        // 辅助:构造 SimEvent(header 层,PayloadRef 用零值 —— 本故事不测载荷编码)
        // ════════════════════════════════════════════════════════════════════

        private static SimEvent MakeEvent(long tick, PatientId patient, long seq, EventKind kind)
        {
            return new SimEvent(tick, patient, seq, kind, default(PayloadRef));
        }

        private static CaseId MakeCaseId(long tick, int patient, long seq)
        {
            return new CaseId(tick, patient, seq);
        }

        // ════════════════════════════════════════════════════════════════════
        // AC-37-30:路径 A 与路径 B 各产生 CaseOpened 入病例流
        // ════════════════════════════════════════════════════════════════════

        /// <summary>AC-37-30:路径 A(52 事件注入)与路径 B(自然就诊)各产生一次就诊交互
        /// ⇒ 二者均经 IEventSink 写入 CaseOpened 至病例流。</summary>
        [Test]
        public void test_ac3730_pathA_and_pathB_bothProduceCaseOpened()
        {
            // 路径 A:52 注入 + 抵达交互
            var decisionA = CaseOpenDecider.Decide(hasOpenCase: false, hasPatient: true, sourceKind: CaseOpenSource.A);
            Assert.That(decisionA, Is.EqualTo(CaseOpenDecision.OpenNew),
                "路径 A 须产生 CaseOpened(入流义务不因触发者不同而不同)");

            // 路径 B:13 自然就诊
            var decisionB = CaseOpenDecider.Decide(hasOpenCase: false, hasPatient: true, sourceKind: CaseOpenSource.B);
            Assert.That(decisionB, Is.EqualTo(CaseOpenDecision.OpenNew),
                "路径 B 须产生 CaseOpened(入流义务不因触发者不同而不同)");

            // 两条路径决策结果相同
            Assert.That(decisionA, Is.EqualTo(decisionB),
                "路径 A 与路径 B 的入流义务完全相同(ADR-009 §二)");
        }

        /// <summary>AC-37-30 Edge:同 tick 一 A 一 B(不同病人)→ 两条,case_id 不同。</summary>
        [Test]
        public void test_ac3730_edge_sameTick_pathA_pathB_differentPatients_caseIdsDiffer()
        {
            const long tick = 100;

            // 路径 A:病人 1
            var decisionA = CaseOpenDecider.Decide(hasOpenCase: false, hasPatient: true, sourceKind: CaseOpenSource.A);
            var caseIdA = MakeCaseId(tick, 1, 0);

            // 路径 B:病人 2
            var decisionB = CaseOpenDecider.Decide(hasOpenCase: false, hasPatient: true, sourceKind: CaseOpenSource.B);
            var caseIdB = MakeCaseId(tick, 2, 0);

            Assert.That(decisionA, Is.EqualTo(CaseOpenDecision.OpenNew));
            Assert.That(decisionB, Is.EqualTo(CaseOpenDecision.OpenNew));
            Assert.That(caseIdA, Is.Not.EqualTo(caseIdB),
                "同 tick 不同病人 ⇒ case_id 不同(承 story-001 AC-37-02)");
        }

        // ════════════════════════════════════════════════════════════════════
        // AC-37-26:同一病人同时至多一个开案
        // ════════════════════════════════════════════════════════════════════

        /// <summary>AC-37-26:病人 p 已有一个「开」的病例,玩家再次对 p 发起就诊交互
        /// ⇒ 不产生新的 CaseOpened,交互回到已开的那一例。</summary>
        [Test]
        public void test_ac3726_secondInteractionOnSamePatient_noNewCaseOpened()
        {
            // 第一次交互:开新案
            var first = CaseOpenDecider.Decide(hasOpenCase: false, hasPatient: true, sourceKind: CaseOpenSource.B);
            Assert.That(first, Is.EqualTo(CaseOpenDecision.OpenNew));

            // 第二次交互:回到已开案
            var second = CaseOpenDecider.Decide(hasOpenCase: true, hasPatient: true, sourceKind: CaseOpenSource.B);
            Assert.That(second, Is.EqualTo(CaseOpenDecision.ReturnExisting),
                "第二次就诊交互不产生新 CaseOpened(AC-37-26)");
        }

        /// <summary>AC-37-26:结案后再交互 → 新案(复诊,合法)。</summary>
        [Test]
        public void test_ac3726_edge_afterClose_newInteraction_opensNewCase()
        {
            // 结案后 hasOpenCase = false
            var afterClose = CaseOpenDecider.Decide(hasOpenCase: false, hasPatient: true, sourceKind: CaseOpenSource.B);
            Assert.That(afterClose, Is.EqualTo(CaseOpenDecision.OpenNew),
                "结案后再交互 ⇒ 新案(复诊,合法)");
        }

        /// <summary>AC-37-26:跨病人不受限 —— 病人 1 有开案不影响病人 2 开新案。</summary>
        [Test]
        public void test_ac3726_edge_crossPatient_noLimit()
        {
            // 病人 1 已有开案
            var p1 = CaseOpenDecider.Decide(hasOpenCase: true, hasPatient: true, sourceKind: CaseOpenSource.B);
            Assert.That(p1, Is.EqualTo(CaseOpenDecision.ReturnExisting));

            // 病人 2 无开案 —— 不受病人 1 影响
            var p2 = CaseOpenDecider.Decide(hasOpenCase: false, hasPatient: true, sourceKind: CaseOpenSource.B);
            Assert.That(p2, Is.EqualTo(CaseOpenDecision.OpenNew),
                "跨病人不受限 —— 病人 1 有开案不影响病人 2 开新案");
        }

        // ════════════════════════════════════════════════════════════════════
        // 药材短缺注入 ⇒ 零 CaseOpened
        // ════════════════════════════════════════════════════════════════════

        /// <summary>药材短缺注入 ⇒ 零 CaseOpened(不立案的负断言)。</summary>
        [Test]
        public void test_medicineShortage_noCaseOpened()
        {
            // 药材短缺 = 无病人
            var decision = CaseOpenDecider.Decide(hasOpenCase: false, hasPatient: false, sourceKind: CaseOpenSource.A);
            Assert.That(decision, Is.EqualTo(CaseOpenDecision.NoCase),
                "药材短缺不立案(无病人)");
        }

        // ════════════════════════════════════════════════════════════════════
        // AC-37-05:判断记录事件化 + 存新值
        // ════════════════════════════════════════════════════════════════════

        /// <summary>AC-37-05:落笔/改写各产生一条事件进病例流,无内存私有副本。</summary>
        [Test]
        public void test_ac3705_judgmentRecorded_and_revised_bothGoToCaseStream()
        {
            // 落笔 = JudgmentRecorded
            var recordedEvent = MakeEvent(200, new PatientId(1), 0, EventKind.JudgmentRecorded);
            Assert.That(StreamRouting.Of(recordedEvent.Kind), Is.EqualTo(StreamId.Case),
                "JudgmentRecorded 须落病例流(ADR-008 §三)");

            // 改写 = JudgmentRevised
            var revisedEvent = MakeEvent(250, new PatientId(1), 1, EventKind.JudgmentRevised);
            Assert.That(StreamRouting.Of(revisedEvent.Kind), Is.EqualTo(StreamId.Case),
                "JudgmentRevised 须落病例流(ADR-008 §三)");
        }

        /// <summary>AC-37-05 存新值:J(c) = 全序最后一条判断事件(改写两次后取末值,非差分合成)。</summary>
        [Test]
        public void test_ac3705_storeNewValue_judgmentIsLastEvent()
        {
            const long tick = 100;
            var patient = new PatientId(1);

            // 落笔(甲)
            var recorded = MakeEvent(tick, patient, 0, EventKind.JudgmentRecorded);
            // 改写(乙)
            var revised1 = MakeEvent(tick + 50, patient, 1, EventKind.JudgmentRevised);
            // 改写(丙)
            var revised2 = MakeEvent(tick + 100, patient, 2, EventKind.JudgmentRevised);

            var allEvents = new[] { recorded, revised1, revised2 };

            // J(c) = 全序最后一条判断事件
            var lastJudgment = allEvents
                .Where(e => e.Kind == EventKind.JudgmentRecorded || e.Kind == EventKind.JudgmentRevised)
                .OrderBy(e => EventOrder.KeyOf(e))
                .Last();

            Assert.That(lastJudgment.Kind, Is.EqualTo(EventKind.JudgmentRevised),
                "J(c) 须取全序最后一条判断事件");
            Assert.That(lastJudgment.Seq, Is.EqualTo(2),
                "改写两次后取末值(Seq=2),非差分合成");
            Assert.That(lastJudgment.Tick, Is.EqualTo(tick + 100),
                "末值 tick = 最后一次改写的 tick");
        }

        /// <summary>AC-37-05 Edge:空判断合法(lexicon_id=0 ∧ freehand_text="")。</summary>
        [Test]
        public void test_ac3705_edge_emptyJudgment_isLegal()
        {
            // 空判断 = lexicon_id = 0(合法终态 S-8.3)
            // 本测试验证空判断也能正常产生事件
            var emptyJudgment = MakeEvent(100, new PatientId(1), 0, EventKind.JudgmentRecorded);
            Assert.That(StreamRouting.Of(emptyJudgment.Kind), Is.EqualTo(StreamId.Case),
                "空判断合法 —— 照样落病例流");
        }

        // ════════════════════════════════════════════════════════════════════
        // AC-37-23 事件面:改写史全序列在病例流保留
        // ════════════════════════════════════════════════════════════════════

        /// <summary>AC-37-23 事件面:改写史全序列在病例流保留(病例流不折叠,ADR-008 §六)。</summary>
        [Test]
        public void test_ac3723_revisionHistory_allInCaseStream()
        {
            const long tick = 100;
            var patient = new PatientId(1);

            // 落笔 + 两次改写 = 三条事件
            var events = new[]
            {
                MakeEvent(tick, patient, 0, EventKind.JudgmentRecorded),
                MakeEvent(tick + 50, patient, 1, EventKind.JudgmentRevised),
                MakeEvent(tick + 100, patient, 2, EventKind.JudgmentRevised),
            };

            // 全部落病例流
            foreach (var e in events)
            {
                Assert.That(StreamRouting.Of(e.Kind), Is.EqualTo(StreamId.Case),
                    "改写史每条都须落病例流(AC-37-23)");
            }

            // 三条事件按全序排列
            var sorted = events.OrderBy(e => EventOrder.KeyOf(e)).ToList();
            Assert.That(sorted[0].Seq, Is.EqualTo(0));
            Assert.That(sorted[1].Seq, Is.EqualTo(1));
            Assert.That(sorted[2].Seq, Is.EqualTo(2));
        }

        // ════════════════════════════════════════════════════════════════════
        // Judgment 类型形状:lexicon_id/confidence/freehand_text 三字段
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Judgment 类型形状:lexicon_id/confidence/freehand_text 三字段,
        /// 判定路径只读前两者(引用扫描:freehand_text 零判定消费点)。</summary>
        [Test]
        public void test_judgmentShape_threeFields_exists()
        {
            // JudgmentRecordedPayload 含 LexiconId + Confidence
            var recordedType = typeof(JudgmentRecordedPayload);
            Assert.That(recordedType.GetField("LexiconId", BindingFlags.Public | BindingFlags.Instance),
                Is.Not.Null, "JudgmentRecordedPayload 须含 LexiconId");
            Assert.That(recordedType.GetField("Confidence", BindingFlags.Public | BindingFlags.Instance),
                Is.Not.Null, "JudgmentRecordedPayload 须含 Confidence");

            // JudgmentRevisedPayload 含 LexiconId + Confidence
            var revisedType = typeof(JudgmentRevisedPayload);
            Assert.That(revisedType.GetField("LexiconId", BindingFlags.Public | BindingFlags.Instance),
                Is.Not.Null, "JudgmentRevisedPayload 须含 LexiconId");
            Assert.That(revisedType.GetField("Confidence", BindingFlags.Public | BindingFlags.Instance),
                Is.Not.Null, "JudgmentRevisedPayload 须含 Confidence");
        }

        /// <summary>Judgment 类型形状:lexicon_id 是 u16 值域(int),confidence 是 u8 值域(byte)。</summary>
        [Test]
        public void test_judgmentShape_lexiconIdIsInt_confidenceIsByte()
        {
            var recordedType = typeof(JudgmentRecordedPayload);

            var lexiconField = recordedType.GetField("LexiconId", BindingFlags.Public | BindingFlags.Instance);
            Assert.That(lexiconField.FieldType, Is.EqualTo(typeof(int)),
                "LexiconId 类型须为 int(u16 值域,用 int 承载)");

            var confidenceField = recordedType.GetField("Confidence", BindingFlags.Public | BindingFlags.Instance);
            Assert.That(confidenceField.FieldType, Is.EqualTo(typeof(byte)),
                "Confidence 类型须为 byte(u8 值域)");
        }

        /// <summary>Judgment 类型形状:freehand_text 零判定消费点(引用扫描)。
        /// 判定路径只读 lexicon_id + confidence,不读 freehand_text。</summary>
        [Test]
        public void test_judgmentShape_freehandText_zeroJudgmentConsumption()
        {
            // freehand_text 在 sim 消费面不存在(只进呈现层)
            // JudgmentRecordedPayload / JudgmentRevisedPayload 不含 FreehandTextField
            var recordedType = typeof(JudgmentRecordedPayload);
            var revisedType = typeof(JudgmentRevisedPayload);

            Assert.That(recordedType.GetField("FreehandText", BindingFlags.Public | BindingFlags.Instance),
                Is.Null, "JudgmentRecordedPayload 不得含 FreehandText(freehand_text 只进呈现层)");
            Assert.That(revisedType.GetField("FreehandText", BindingFlags.Public | BindingFlags.Instance),
                Is.Null, "JudgmentRevisedPayload 不得含 FreehandText(freehand_text 只进呈现层)");
        }

        // ════════════════════════════════════════════════════════════════════
        // 综合:立案 → 判断 → 改写 完整流程
        // ════════════════════════════════════════════════════════════════════

        /// <summary>综合流程:立案 → 落笔 → 改写 → 再改写,全部事件落病例流,存新值取末条。</summary>
        [Test]
        public void test_fullFlow_open_record_revise_revise_allInCaseStream()
        {
            const long tick = 100;
            var patient = new PatientId(1);

            // 1. 立案
            var openDecision = CaseOpenDecider.Decide(hasOpenCase: false, hasPatient: true, sourceKind: CaseOpenSource.B);
            Assert.That(openDecision, Is.EqualTo(CaseOpenDecision.OpenNew));
            var caseOpened = MakeEvent(tick, patient, 0, EventKind.CaseOpened);

            // 2. 落笔
            var recorded = MakeEvent(tick + 10, patient, 1, EventKind.JudgmentRecorded);

            // 3. 改写
            var revised1 = MakeEvent(tick + 60, patient, 2, EventKind.JudgmentRevised);

            // 4. 再改写
            var revised2 = MakeEvent(tick + 110, patient, 3, EventKind.JudgmentRevised);

            var allEvents = new[] { caseOpened, recorded, revised1, revised2 };

            // 全部落病例流
            foreach (var e in allEvents)
            {
                Assert.That(StreamRouting.Of(e.Kind), Is.EqualTo(StreamId.Case),
                    $"{e.Kind} 须落病例流");
            }

            // 存新值 = 末条
            var lastJudgment = allEvents
                .Where(e => e.Kind == EventKind.JudgmentRecorded || e.Kind == EventKind.JudgmentRevised)
                .OrderBy(e => EventOrder.KeyOf(e))
                .Last();

            Assert.That(lastJudgment.Seq, Is.EqualTo(3), "存新值取末条(Seq=3)");
            Assert.That(lastJudgment.Tick, Is.EqualTo(tick + 110), "末值 tick = 最后一次改写");
        }

        /// <summary>综合流程 Edge:立案后第二次交互 → 回到已开案,不产生新 CaseOpened。</summary>
        [Test]
        public void test_fullFlow_edge_secondInteractionAfterOpen_returnsExisting()
        {
            const long tick = 100;
            var patient = new PatientId(1);

            // 1. 立案
            var first = CaseOpenDecider.Decide(hasOpenCase: false, hasPatient: true, sourceKind: CaseOpenSource.B);
            Assert.That(first, Is.EqualTo(CaseOpenDecision.OpenNew));

            // 2. 第二次交互
            var second = CaseOpenDecider.Decide(hasOpenCase: true, hasPatient: true, sourceKind: CaseOpenSource.B);
            Assert.That(second, Is.EqualTo(CaseOpenDecision.ReturnExisting),
                "第二次交互回到已开案,不产生新 CaseOpened");

            // 3. 判断记录仍然可以落(回到已开案后可以继续落笔)
            var recorded = MakeEvent(tick + 10, patient, 1, EventKind.JudgmentRecorded);
            Assert.That(StreamRouting.Of(recorded.Kind), Is.EqualTo(StreamId.Case),
                "回到已开案后判断记录照样落病例流");
        }

        // ════════════════════════════════════════════════════════════════════
        // 流前缀查询:OpenCaseOf(p) —— 生产代码 CaseStreamQuery
        // ════════════════════════════════════════════════════════════════════

        /// <summary>OpenCaseOf(p):有开案返回 case_id。</summary>
        [Test]
        public void test_openCaseOf_hasOpenCase_returnsCaseId()
        {
            var events = new[]
            {
                MakeEvent(100, new PatientId(1), 0, EventKind.CaseOpened),
            };

            var result = CaseStreamQuery.OpenCaseOf(events, 1);
            Assert.That(result.HasValue, Is.True, "有开案须返回 case_id");
            Assert.That(result.Value, Is.EqualTo(MakeCaseId(100, 1, 0)));
        }

        /// <summary>OpenCaseOf(p):无开案返回 null。</summary>
        [Test]
        public void test_openCaseOf_noOpenCase_returnsNull()
        {
            var events = new SimEvent[0];
            var result = CaseStreamQuery.OpenCaseOf(events, 1);
            Assert.That(result.HasValue, Is.False, "无开案须返回 null");
        }

        /// <summary>OpenCaseOf(p):结案后返回 null。</summary>
        [Test]
        public void test_openCaseOf_afterClose_returnsNull()
        {
            var events = new[]
            {
                MakeEvent(100, new PatientId(1), 0, EventKind.CaseOpened),
                MakeEvent(200, new PatientId(1), 1, EventKind.CaseClosed),
            };

            var result = CaseStreamQuery.OpenCaseOf(events, 1);
            Assert.That(result.HasValue, Is.False, "结案后须返回 null");
        }

        /// <summary>OpenCaseOf(p):复诊(结案后再立案)返回新 case_id。</summary>
        [Test]
        public void test_openCaseOf_revisit_returnsNewCaseId()
        {
            var events = new[]
            {
                MakeEvent(100, new PatientId(1), 0, EventKind.CaseOpened),
                MakeEvent(200, new PatientId(1), 1, EventKind.CaseClosed),
                MakeEvent(300, new PatientId(1), 2, EventKind.CaseOpened),
            };

            var result = CaseStreamQuery.OpenCaseOf(events, 1);
            Assert.That(result.HasValue, Is.True, "复诊须返回新 case_id");
            Assert.That(result.Value, Is.EqualTo(MakeCaseId(300, 1, 2)));
        }

        /// <summary>OpenCaseOf(p):跨病人不受限。</summary>
        [Test]
        public void test_openCaseOf_crossPatient_returnsCorrectCase()
        {
            var events = new[]
            {
                MakeEvent(100, new PatientId(1), 0, EventKind.CaseOpened),
                MakeEvent(100, new PatientId(2), 0, EventKind.CaseOpened),
            };

            var result1 = CaseStreamQuery.OpenCaseOf(events, 1);
            var result2 = CaseStreamQuery.OpenCaseOf(events, 2);

            Assert.That(result1.Value, Is.EqualTo(MakeCaseId(100, 1, 0)));
            Assert.That(result2.Value, Is.EqualTo(MakeCaseId(100, 2, 0)));
        }

        // ════════════════════════════════════════════════════════════════════
        // 存新值:LastJudgmentOf —— 生产代码 CaseStreamQuery
        // ════════════════════════════════════════════════════════════════════

        /// <summary>LastJudgmentOf:无判断记录返回 null。</summary>
        [Test]
        public void test_lastJudgmentOf_noJudgment_returnsNull()
        {
            var events = new[]
            {
                MakeEvent(100, new PatientId(1), 0, EventKind.CaseOpened),
            };

            var result = CaseStreamQuery.LastJudgmentOf(events, MakeCaseId(100, 1, 0));
            Assert.That(result.HasValue, Is.False, "无判断记录须返回 null");
        }

        /// <summary>LastJudgmentOf:有判断记录返回末条。</summary>
        [Test]
        public void test_lastJudgmentOf_hasJudgment_returnsLast()
        {
            var events = new[]
            {
                MakeEvent(100, new PatientId(1), 0, EventKind.JudgmentRecorded),
                MakeEvent(150, new PatientId(1), 1, EventKind.JudgmentRevised),
                MakeEvent(200, new PatientId(1), 2, EventKind.JudgmentRevised),
            };

            var result = CaseStreamQuery.LastJudgmentOf(events, MakeCaseId(100, 1, 0));
            Assert.That(result.HasValue, Is.True, "有判断记录须返回末条");
            Assert.That(result.Value.Seq, Is.EqualTo(2), "末条 Seq=2");
            Assert.That(result.Value.Tick, Is.EqualTo(200), "末条 tick=200");
        }

        /// <summary>LastJudgmentOf:单条判断记录返回该条。</summary>
        [Test]
        public void test_lastJudgmentOf_singleJudgment_returnsThat()
        {
            var events = new[]
            {
                MakeEvent(100, new PatientId(1), 0, EventKind.JudgmentRecorded),
            };

            var result = CaseStreamQuery.LastJudgmentOf(events, MakeCaseId(100, 1, 0));
            Assert.That(result.HasValue, Is.True, "单条判断记录须返回该条");
            Assert.That(result.Value.Seq, Is.EqualTo(0));
        }
    }
}
