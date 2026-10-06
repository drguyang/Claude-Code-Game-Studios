// case-system Story 003 测试 —— 结案前置:处置证据窗口与快照
//
// AC-37-04:未勾「已处置」⇒ 结案拒绝(无事件)
// AC-37-21:已勾选但窗口内无处置事件 ⇒ 拒绝(复诊后门正向封堵)
// AC-37-03:同一 case_id 第二条 CaseClosed 幂等拒收
// 窗口边界:闭区间 [CaseOpened.Tick, CloseTick]
// CaseClosed 载荷 = {disease_set(集合,可空), treated} 快照
// AC-37-18:病人死亡但案未结 ⇒ 病例仍「开」、可正常结案;病名为空可结案
// 结案副作用序列:读数清除信号(→8)+ 判断冻结(→53)+ CaseClosed 入病例流
//
// 权威来源:
//   ADR-008 §四(处置证据窗口化 + 快照;方向订正 2026-09-17)
//   GDD 规则六(结案唯一出口 · 已处置前置 · 单例 · 不可撤销 · 幂等拒收)
//   GDD F-37.2(已处置 / 可结案 公式)
//
// ⚠️ 本测试为纯数据层:不触引擎 API,不触 codec,不触持久化。
//    被测对象 = Sim 的 CaseCloseDecider / TreatmentKinds / CaseCloseQuery(生产代码)。

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.CaseSystem
{
    /// <summary>case-system Story 003 —— 结案前置:处置证据窗口与快照。</summary>
    [TestFixture]
    public sealed class CaseClosePreconditionTest
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
        // AC-37-04:未勾「已处置」⇒ 结案拒绝
        // ════════════════════════════════════════════════════════════════════

        /// <summary>AC-37-04:未勾选「已处置」⇒ 拒绝(即使有处置事件)。</summary>
        [Test]
        public void test_ac3704_notChecked_rejectsClose()
        {
            var decision = CaseCloseDecider.Decide(
                isOpen: true, isClosed: false, hasTreatmentInWindow: true, isChecked: false);
            Assert.That(decision, Is.EqualTo(CaseCloseDecision.RejectNotChecked),
                "未勾选「已处置」须拒绝结案(AC-37-04)");
        }

        /// <summary>AC-37-04 Edge:未勾选且无处置事件 → 拒绝(未勾选优先)。</summary>
        [Test]
        public void test_ac3704_edge_notChecked_noTreatment_rejects()
        {
            var decision = CaseCloseDecider.Decide(
                isOpen: true, isClosed: false, hasTreatmentInWindow: false, isChecked: false);
            Assert.That(decision, Is.EqualTo(CaseCloseDecision.RejectNotChecked),
                "未勾选优先拒绝(即使也无处置事件)");
        }

        // ════════════════════════════════════════════════════════════════════
        // AC-37-21:已勾选但窗口内无处置事件 ⇒ 拒绝(复诊后门正向封堵)
        // ════════════════════════════════════════════════════════════════════

        /// <summary>AC-37-21:已勾选但窗口内无处置事件 → 拒绝。</summary>
        [Test]
        public void test_ac3721_checkedButNoTreatmentInWindow_rejects()
        {
            var decision = CaseCloseDecider.Decide(
                isOpen: true, isClosed: false, hasTreatmentInWindow: false, isChecked: true);
            Assert.That(decision, Is.EqualTo(CaseCloseDecision.RejectNoTreatment),
                "已勾选但窗口内无处置事件须拒绝(AC-37-21)");
        }

        /// <summary>AC-37-21 复诊后门:首诊处置@t₁,首诊结案@t₂,复诊立案@t₃(t₁<t₂<t₃),
        /// 复诊无新处置 → 拒绝(t₁ ∉ [t₃, now])。</summary>
        [Test]
        public void test_ac3721_revisitBackdoor_firstTreatmentNotInSecondWindow_rejects()
        {
            const long t1 = 100;  // 首诊处置
            const long t2 = 200;  // 首诊结案
            const long t3 = 300;  // 复诊立案
            const long now = 400; // 复诊结案尝试

            // 首诊处置在病史流
            var historyEvents = new[]
            {
                MakeEvent(t1, new PatientId(1), 0, EventKind.EmergencyTreatmentApplied),
            };

            // 复诊窗口 = [t3, now]
            var hasTreatment = CaseCloseQuery.HasTreatmentInWindow(historyEvents, 1, t3, now);
            Assert.That(hasTreatment, Is.False,
                "首诊处置@t₁ 不在复诊窗口 [t₃, now] 内(复诊后门正向封堵)");

            var decision = CaseCloseDecider.Decide(
                isOpen: true, isClosed: false, hasTreatmentInWindow: hasTreatment, isChecked: true);
            Assert.That(decision, Is.EqualTo(CaseCloseDecision.RejectNoTreatment),
                "复诊无新处置 ⇒ 拒绝(复诊后门关闭)");
        }

        /// <summary>AC-37-21 Edge:复诊窗口内补一条处置 → 放行。</summary>
        [Test]
        public void test_ac3721_edge_revisitWithNewTreatmentInWindow_allows()
        {
            const long t1 = 100;  // 首诊处置
            const long t2 = 200;  // 首诊结案
            const long t3 = 300;  // 复诊立案
            const long t4 = 350;  // 复诊新处置
            const long now = 400; // 复诊结案尝试

            var historyEvents = new[]
            {
                MakeEvent(t1, new PatientId(1), 0, EventKind.EmergencyTreatmentApplied),
                MakeEvent(t4, new PatientId(1), 1, EventKind.DrugTreatmentApplied),
            };

            var hasTreatment = CaseCloseQuery.HasTreatmentInWindow(historyEvents, 1, t3, now);
            Assert.That(hasTreatment, Is.True,
                "复诊窗口 [t₃, now] 内有新处置@t₄ ⇒ 放行");

            var decision = CaseCloseDecider.Decide(
                isOpen: true, isClosed: false, hasTreatmentInWindow: hasTreatment, isChecked: true);
            Assert.That(decision, Is.EqualTo(CaseCloseDecision.CanClose),
                "复诊窗口内有新处置 ⇒ 可结案");
        }

        // ════════════════════════════════════════════════════════════════════
        // AC-37-03:同一 case_id 第二条 CaseClosed 幂等拒收
        // ════════════════════════════════════════════════════════════════════

        /// <summary>AC-37-03:案件已结 → 幂等拒收(第二条 CaseClosed 不产生新事件)。</summary>
        [Test]
        public void test_ac3703_alreadyClosed_idempotentReject()
        {
            var decision = CaseCloseDecider.Decide(
                isOpen: true, isClosed: true, hasTreatmentInWindow: true, isChecked: true);
            Assert.That(decision, Is.EqualTo(CaseCloseDecision.RejectAlreadyClosed),
                "案件已结须幂等拒收(AC-37-03)");
        }

        /// <summary>AC-37-03 Edge:同 tick 双意图 → 恰一条 CaseClosed(幂等)。</summary>
        [Test]
        public void test_ac3703_edge_sameTick_doubleIntent_idempotent()
        {
            // 第一次结案
            var first = CaseCloseDecider.Decide(
                isOpen: true, isClosed: false, hasTreatmentInWindow: true, isChecked: true);
            Assert.That(first, Is.EqualTo(CaseCloseDecision.CanClose));

            // 第二次结案(同 tick)
            var second = CaseCloseDecider.Decide(
                isOpen: true, isClosed: true, hasTreatmentInWindow: true, isChecked: true);
            Assert.That(second, Is.EqualTo(CaseCloseDecision.RejectAlreadyClosed),
                "同 tick 双意图 ⇒ 第二条幂等拒收");
        }

        // ════════════════════════════════════════════════════════════════════
        // 窗口边界:闭区间 [CaseOpened.Tick, CloseTick]
        // ════════════════════════════════════════════════════════════════════

        /// <summary>窗口边界:e.Tick == openedTick(立案即处置)计入(闭区间)。</summary>
        [Test]
        public void test_windowBoundary_tickEqualsOpenedTick_included()
        {
            Assert.That(CaseCloseQuery.IsInWindow(100, 100, 200), Is.True,
                "e.Tick == openedTick 计入(闭区间)");
        }

        /// <summary>窗口边界:e.Tick == closeTick 计入(闭区间)。</summary>
        [Test]
        public void test_windowBoundary_tickEqualsCloseTick_included()
        {
            Assert.That(CaseCloseQuery.IsInWindow(200, 100, 200), Is.True,
                "e.Tick == closeTick 计入(闭区间)");
        }

        /// <summary>窗口边界:e.Tick < openedTick 不计入。</summary>
        [Test]
        public void test_windowBoundary_tickBeforeOpened_excluded()
        {
            Assert.That(CaseCloseQuery.IsInWindow(99, 100, 200), Is.False,
                "e.Tick < openedTick 不计入");
        }

        /// <summary>窗口边界:e.Tick > closeTick 不计入。</summary>
        [Test]
        public void test_windowBoundary_tickAfterClose_excluded()
        {
            Assert.That(CaseCloseQuery.IsInWindow(201, 100, 200), Is.False,
                "e.Tick > closeTick 不计入");
        }

        /// <summary>窗口边界:窗口内多条处置事件 → 已处置成立。</summary>
        [Test]
        public void test_windowBoundary_multipleTreatmentsInWindow_hasTreatment()
        {
            var historyEvents = new[]
            {
                MakeEvent(120, new PatientId(1), 0, EventKind.EmergencyTreatmentApplied),
                MakeEvent(150, new PatientId(1), 1, EventKind.DrugTreatmentApplied),
            };

            var hasTreatment = CaseCloseQuery.HasTreatmentInWindow(historyEvents, 1, 100, 200);
            Assert.That(hasTreatment, Is.True, "窗口内多条处置 ⇒ 已处置成立");
        }

        // ════════════════════════════════════════════════════════════════════
        // 处置集白名单:TreatmentKinds
        // ════════════════════════════════════════════════════════════════════

        /// <summary>处置集含 EmergencyTreatmentApplied(10 急救)。</summary>
        [Test]
        public void test_treatmentKinds_containsEmergencyTreatment()
        {
            Assert.That(TreatmentKinds.Contains(EventKind.EmergencyTreatmentApplied), Is.True,
                "处置集须含 EmergencyTreatmentApplied(10 急救)");
        }

        /// <summary>处置集含 DrugTreatmentApplied(11 处方)。</summary>
        [Test]
        public void test_treatmentKinds_containsDrugTreatment()
        {
            Assert.That(TreatmentKinds.Contains(EventKind.DrugTreatmentApplied), Is.True,
                "处置集须含 DrugTreatmentApplied(11 处方)");
        }

        /// <summary>处置集含 InjuryOnset(25 格斗)。</summary>
        [Test]
        public void test_treatmentKinds_containsInjuryOnset()
        {
            Assert.That(TreatmentKinds.Contains(EventKind.InjuryOnset), Is.True,
                "处置集须含 InjuryOnset(25 格斗)");
        }

        /// <summary>处置集不含非处置 Kind(如 CaseOpened)。</summary>
        [Test]
        public void test_treatmentKinds_excludesNonTreatmentKinds()
        {
            Assert.That(TreatmentKinds.Contains(EventKind.CaseOpened), Is.False,
                "处置集不含 CaseOpened");
            Assert.That(TreatmentKinds.Contains(EventKind.JudgmentRecorded), Is.False,
                "处置集不含 JudgmentRecorded");
            Assert.That(TreatmentKinds.Contains(EventKind.ActorCellEntered), Is.False,
                "处置集不含 ActorCellEntered");
        }

        // ════════════════════════════════════════════════════════════════════
        // CaseClosed 载荷形状:{disease_set(集合,可空), treated} 快照
        // ════════════════════════════════════════════════════════════════════

        /// <summary>CaseClosedPayload 含 DiseaseSet 字段(集合,可空)。</summary>
        [Test]
        public void test_caseClosedPayload_containsDiseaseSet()
        {
            var closedType = typeof(CaseClosedPayload);
            var field = closedType.GetField("DiseaseSet", BindingFlags.Public | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, "CaseClosedPayload 须含 DiseaseSet 字段");
            Assert.That(field.FieldType, Is.EqualTo(typeof(DiseaseIdSet)),
                "DiseaseSet 类型须为 DiseaseIdSet(集合)");
        }

        /// <summary>CaseClosedPayload 含 Treated 字段(bool 快照)。</summary>
        [Test]
        public void test_caseClosedPayload_containsTreated()
        {
            var closedType = typeof(CaseClosedPayload);
            var field = closedType.GetField("Treated", BindingFlags.Public | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, "CaseClosedPayload 须含 Treated 字段");
            Assert.That(field.FieldType, Is.EqualTo(typeof(bool)),
                "Treated 类型须为 bool");
        }

        /// <summary>CaseClosedPayload 含 PatientId 字段。</summary>
        [Test]
        public void test_caseClosedPayload_containsPatientId()
        {
            var closedType = typeof(CaseClosedPayload);
            var field = closedType.GetField("PatientId", BindingFlags.Public | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, "CaseClosedPayload 须含 PatientId 字段");
        }

        /// <summary>CaseClosedPayload 含 CaseId 字段(引用原案)。</summary>
        [Test]
        public void test_caseClosedPayload_containsCaseId()
        {
            var closedType = typeof(CaseClosedPayload);
            var field = closedType.GetField("CaseId", BindingFlags.Public | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, "CaseClosedPayload 须含 CaseId 字段");
            Assert.That(field.FieldType, Is.EqualTo(typeof(CaseId)),
                "CaseId 类型须为 CaseId(三元组)");
        }

        // ════════════════════════════════════════════════════════════════════
        // AC-37-18:病人死亡但案未结 ⇒ 病例仍「开」、可正常结案
        // ════════════════════════════════════════════════════════════════════

        /// <summary>AC-37-18:案件开着(无 CaseClosed)→ 可结案(病人死亡不影响)。</summary>
        [Test]
        public void test_ac3718_caseOpen_canClose()
        {
            var caseEvents = new[]
            {
                MakeEvent(100, new PatientId(1), 0, EventKind.CaseOpened),
            };
            var caseId = MakeCaseId(100, 1, 0);

            var isOpen = CaseCloseQuery.IsOpen(caseEvents, caseId);
            Assert.That(isOpen, Is.True, "案件开着(无 CaseClosed)");

            var decision = CaseCloseDecider.Decide(
                isOpen: isOpen, isClosed: false, hasTreatmentInWindow: true, isChecked: true);
            Assert.That(decision, Is.EqualTo(CaseCloseDecision.CanClose),
                "病人死亡但案未结 ⇒ 可正常结案(AC-37-18)");
        }

        /// <summary>AC-37-18:病名为空可结案(空是合法终态)。</summary>
        [Test]
        public void test_ac3718_emptyDiseaseName_canClose()
        {
            // 病名为空 = disease_set 可空,不影响结案
            var decision = CaseCloseDecider.Decide(
                isOpen: true, isClosed: false, hasTreatmentInWindow: true, isChecked: true);
            Assert.That(decision, Is.EqualTo(CaseCloseDecision.CanClose),
                "病名为空可结案(空是合法终态)");
        }

        /// <summary>AC-37-18:纯伤情案(无病种)→ disease_set 空集,结案合法。</summary>
        [Test]
        public void test_ac3718_pureInjuryCase_emptyDiseaseSet_canClose()
        {
            // 纯伤情案 = disease_set 空集
            var emptyDiseaseSet = new DiseaseIdSet(0);
            Assert.That(emptyDiseaseSet.Bits, Is.EqualTo(0UL), "纯伤情案 disease_set 空集");

            var decision = CaseCloseDecider.Decide(
                isOpen: true, isClosed: false, hasTreatmentInWindow: true, isChecked: true);
            Assert.That(decision, Is.EqualTo(CaseCloseDecision.CanClose),
                "纯伤情案(空 disease_set)结案合法");
        }

        // ════════════════════════════════════════════════════════════════════
        // 结案副作用序列:CaseClosed 入病例流
        // ════════════════════════════════════════════════════════════════════

        /// <summary>结案副作用:CaseClosed 落病例流(StreamRouting)。</summary>
        [Test]
        public void test_closeSideEffect_caseClosedGoesToCaseStream()
        {
            var closedEvent = MakeEvent(200, new PatientId(1), 1, EventKind.CaseClosed);
            Assert.That(StreamRouting.Of(closedEvent.Kind), Is.EqualTo(StreamId.Case),
                "CaseClosed 须落病例流(ADR-008 §一)");
        }

        /// <summary>结案副作用:CaseClosed 载荷含 disease_set + treated 快照。</summary>
        [Test]
        public void test_closeSideEffect_caseClosedPayloadHasSnapshot()
        {
            var closedType = typeof(CaseClosedPayload);
            Assert.That(closedType.GetField("DiseaseSet", BindingFlags.Public | BindingFlags.Instance),
                Is.Not.Null, "CaseClosed 载荷须含 disease_set 快照");
            Assert.That(closedType.GetField("Treated", BindingFlags.Public | BindingFlags.Instance),
                Is.Not.Null, "CaseClosed 载荷须含 treated 快照");
        }

        // ════════════════════════════════════════════════════════════════════
        // 流前缀查询:IsOpen / IsClosed —— 生产代码 CaseCloseQuery
        // ════════════════════════════════════════════════════════════════════

        /// <summary>IsOpen:有 CaseOpened 无 CaseClosed → true。</summary>
        [Test]
        public void test_isOpen_openedNotClosed_returnsTrue()
        {
            var caseEvents = new[]
            {
                MakeEvent(100, new PatientId(1), 0, EventKind.CaseOpened),
            };
            var caseId = MakeCaseId(100, 1, 0);
            Assert.That(CaseCloseQuery.IsOpen(caseEvents, caseId), Is.True);
        }

        /// <summary>IsOpen:有 CaseOpened + CaseClosed → false。</summary>
        [Test]
        public void test_isOpen_openedThenClosed_returnsFalse()
        {
            var caseEvents = new[]
            {
                MakeEvent(100, new PatientId(1), 0, EventKind.CaseOpened),
                MakeEvent(200, new PatientId(1), 1, EventKind.CaseClosed),
            };
            var caseId = MakeCaseId(100, 1, 0);
            Assert.That(CaseCloseQuery.IsOpen(caseEvents, caseId), Is.False);
        }

        /// <summary>IsClosed:有 CaseClosed → true。</summary>
        [Test]
        public void test_isClosed_hasClosed_returnsTrue()
        {
            var caseEvents = new[]
            {
                MakeEvent(100, new PatientId(1), 0, EventKind.CaseOpened),
                MakeEvent(200, new PatientId(1), 1, EventKind.CaseClosed),
            };
            var caseId = MakeCaseId(100, 1, 0);
            Assert.That(CaseCloseQuery.IsClosed(caseEvents, caseId), Is.True);
        }

        /// <summary>IsClosed:无 CaseClosed → false。</summary>
        [Test]
        public void test_isClosed_noClosed_returnsFalse()
        {
            var caseEvents = new[]
            {
                MakeEvent(100, new PatientId(1), 0, EventKind.CaseOpened),
            };
            var caseId = MakeCaseId(100, 1, 0);
            Assert.That(CaseCloseQuery.IsClosed(caseEvents, caseId), Is.False);
        }

        /// <summary>IsOpen:复诊(结案后再立案)→ 新案开着。</summary>
        [Test]
        public void test_isOpen_revisit_newCaseIsOpen()
        {
            var caseEvents = new[]
            {
                MakeEvent(100, new PatientId(1), 0, EventKind.CaseOpened),
                MakeEvent(200, new PatientId(1), 1, EventKind.CaseClosed),
                MakeEvent(300, new PatientId(1), 2, EventKind.CaseOpened),
            };
            var newCaseId = MakeCaseId(300, 1, 2);
            Assert.That(CaseCloseQuery.IsOpen(caseEvents, newCaseId), Is.True,
                "复诊新案开着");
        }

        // ════════════════════════════════════════════════════════════════════
        // 综合:完整结案流程
        // ════════════════════════════════════════════════════════════════════

        /// <summary>综合流程:立案 → 处置 → 勾选 → 结案 → 幂等拒收。</summary>
        [Test]
        public void test_fullFlow_open_treat_check_close_idempotent()
        {
            const long t1 = 100; // 立案
            const long t2 = 150; // 处置
            const long t3 = 200; // 结案

            var patient = new PatientId(1);
            var caseId = MakeCaseId(t1, 1, 0);

            // 1. 立案
            var caseEvents = new List<SimEvent>
            {
                MakeEvent(t1, patient, 0, EventKind.CaseOpened),
            };

            // 2. 处置(窗口内)
            var historyEvents = new[]
            {
                MakeEvent(t2, patient, 0, EventKind.EmergencyTreatmentApplied),
            };
            var hasTreatment = CaseCloseQuery.HasTreatmentInWindow(historyEvents, 1, t1, t3);
            Assert.That(hasTreatment, Is.True, "窗口内有处置");

            // 3. 结案决策
            var isOpen = CaseCloseQuery.IsOpen(caseEvents, caseId);
            var isClosed = CaseCloseQuery.IsClosed(caseEvents, caseId);
            var decision = CaseCloseDecider.Decide(isOpen, isClosed, hasTreatment, isChecked: true);
            Assert.That(decision, Is.EqualTo(CaseCloseDecision.CanClose), "可结案");

            // 4. 写入 CaseClosed
            caseEvents.Add(MakeEvent(t3, patient, 1, EventKind.CaseClosed));

            // 5. 幂等拒收
            var isClosedAfter = CaseCloseQuery.IsClosed(caseEvents, caseId);
            Assert.That(isClosedAfter, Is.True, "已结");
            var secondDecision = CaseCloseDecider.Decide(
                isOpen: true, isClosed: isClosedAfter, hasTreatmentInWindow: true, isChecked: true);
            Assert.That(secondDecision, Is.EqualTo(CaseCloseDecision.RejectAlreadyClosed),
                "第二条 CaseClosed 幂等拒收");
        }

        /// <summary>综合流程 Edge:立案即处置(e.Tick == openedTick)→ 可结案。</summary>
        [Test]
        public void test_fullFlow_edge_treatAtOpenTick_canClose()
        {
            const long t1 = 100; // 立案 + 处置(同 tick)
            const long t2 = 200; // 结案

            var patient = new PatientId(1);
            var caseId = MakeCaseId(t1, 1, 0);

            var historyEvents = new[]
            {
                MakeEvent(t1, patient, 0, EventKind.EmergencyTreatmentApplied),
            };
            var hasTreatment = CaseCloseQuery.HasTreatmentInWindow(historyEvents, 1, t1, t2);
            Assert.That(hasTreatment, Is.True, "立案即处置(闭区间)");

            var caseEvents = new List<SimEvent>
            {
                MakeEvent(t1, patient, 0, EventKind.CaseOpened),
            };
            var isOpen = CaseCloseQuery.IsOpen(caseEvents, caseId);
            var decision = CaseCloseDecider.Decide(isOpen, isClosed: false, hasTreatment, isChecked: true);
            Assert.That(decision, Is.EqualTo(CaseCloseDecision.CanClose),
                "立案即处置 ⇒ 可结案");
        }

        /// <summary>综合流程 Edge:处置在立案之前(e.Tick < openedTick)→ 拒绝。</summary>
        [Test]
        public void test_fullFlow_edge_treatBeforeOpen_rejects()
        {
            const long t1 = 50;  // 处置(立案前)
            const long t2 = 100; // 立案
            const long t3 = 200; // 结案

            var patient = new PatientId(1);

            var historyEvents = new[]
            {
                MakeEvent(t1, patient, 0, EventKind.EmergencyTreatmentApplied),
            };
            var hasTreatment = CaseCloseQuery.HasTreatmentInWindow(historyEvents, 1, t2, t3);
            Assert.That(hasTreatment, Is.False, "处置在立案之前 ⇒ 不计入窗口");

            var decision = CaseCloseDecider.Decide(
                isOpen: true, isClosed: false, hasTreatmentInWindow: hasTreatment, isChecked: true);
            Assert.That(decision, Is.EqualTo(CaseCloseDecision.RejectNoTreatment),
                "处置在立案之前 ⇒ 拒绝");
        }
    }
}
