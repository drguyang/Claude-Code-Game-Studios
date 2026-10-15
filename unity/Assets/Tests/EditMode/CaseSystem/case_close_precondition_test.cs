// case-system Story 003 测试 —— 结案前置:处置证据窗口与快照
//
// AC-37-04:未勾「已处置」⇒ 结案拒绝(无事件)
// AC-37-21:已勾选但窗口内无处置事件 ⇒ 拒绝(复诊后门正向封堵)
// AC-37-03:同一 case_id 第二条 CaseClosed 幂等拒收
// 窗口边界:闭区间 [CaseOpened.Tick, CloseTick]
// CaseClosed 载荷 = {disease_set(集合,可空), treated} 快照
// AC-37-18:病人死亡但案未结 ⇒ 病例仍「开」、可正常结案(判别半边 = 塞满非 CaseClosed 事件)
//           ⚠️ 「空病种集」半边 2026-10-15 迁往 case_close_writer_test.cs(决策层不可表达);
//              「病名」半边 = UI/词表概念,37 Sim 层 NOT-RUN —— 详见文件末注块
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
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.CaseSystem
{
    /// <summary>case-system Story 003 —— 结案前置:处置证据窗口与快照。</summary>
    [TestFixture]
    public sealed class CaseClosePreconditionTest
    {
        // ════════════════════════════════════════════════════════════════════
        // 辅助:构造 SimEvent(header 层)
        // ⚠️ 2026-10-15 接线轮(承 ADR-031 V-8 ①「退」):IsClosed 已改载荷解码口径
        //    (读 CaseClosedPayload.CaseId)⇒ 每条 CaseClosed 须经真编码器造载荷
        //    (CaseId 三元组与测试意图的归属一致),default(PayloadRef) 解码必抛。
        //    非 CaseClosed 的 Kind 无需载荷(按 Kind 过滤先于解码)。
        // ════════════════════════════════════════════════════════════════════

        private static readonly InMemoryBlobPool SharedPool = new InMemoryBlobPool();
        private static readonly PayloadEncoder Encoder = new PayloadEncoder(SharedPool);
        private static readonly PayloadDecoder Decoder = new PayloadDecoder(SharedPool);

        /// <summary>造一条**真载荷**的 CaseClosed(唯一被 IsClosed 解码消费的 Kind)。</summary>
        private static SimEvent MakeClosed(long tick, CaseId caseId, long seq,
                                           DiseaseIdSet diseaseSet = default,
                                           bool treated = true)
        {
            var payload = new CaseClosedPayload(caseId.Patient, caseId, diseaseSet, treated);
            var payloadRef = Encoder.Encode(EventKind.CaseClosed, payload);
            return new SimEvent(tick, new PatientId(caseId.Patient), seq, EventKind.CaseClosed, payloadRef);
        }

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

            // 病例流 —— 🔴 2026-10-15(S11-8):补上真流派生入参,不再硬编码 isClosed:false。
            // 复诊 = **新的一例**(GDD 规则六-4),故复诊案的「已结」须查**它自己**,不是首案的。
            var patient = new PatientId(1);
            var caseEvents = new[]
            {
                MakeEvent(t1, patient, 0, EventKind.CaseOpened),  // 首诊立案
                MakeClosed(t2, MakeCaseId(t1, 1, 0), 1),          // 首诊结案
                MakeEvent(t3, patient, 2, EventKind.CaseOpened),  // 复诊立案(新的一例)
            };
            var revisitCaseId = MakeCaseId(t3, 1, 2);

            // 复诊窗口 = [t3, now]
            var hasTreatment = CaseCloseQuery.HasTreatmentInWindow(historyEvents, 1, t3, now);
            Assert.That(hasTreatment, Is.False,
                "首诊处置@t₁ 不在复诊窗口 [t₃, now] 内(复诊后门正向封堵)");

            var isOpen = CaseCloseQuery.IsOpen(caseEvents, revisitCaseId, Decoder);
            var isClosed = CaseCloseQuery.IsClosed(caseEvents, revisitCaseId, Decoder);
            Assert.That(isOpen, Is.True, "复诊案开着");
            Assert.That(isClosed, Is.False,
                "复诊案**未结** —— 首案的 CaseClosed 不得外溢到复诊案(否则理由码退化为 RejectAlreadyClosed)");

            var decision = CaseCloseDecider.Decide(
                isOpen: isOpen, isClosed: isClosed, hasTreatmentInWindow: hasTreatment, isChecked: true);
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

            // 病例流 —— 🔴 2026-10-15(S11-8):本用例是 **B-1 在真实场景下的显影点**。
            // 修前 IsClosed 按 Patient 匹配 ⇒ 复诊案的 isClosed 被首案的 CaseClosed 带成 true,
            // 本用例的 CanClose 会退化为 RejectAlreadyClosed ⇒ **复诊案永远结不掉**。
            var patient = new PatientId(1);
            var caseEvents = new[]
            {
                MakeEvent(t1, patient, 0, EventKind.CaseOpened),
                MakeClosed(t2, MakeCaseId(t1, 1, 0), 1),
                MakeEvent(t3, patient, 2, EventKind.CaseOpened),
            };
            var revisitCaseId = MakeCaseId(t3, 1, 2);

            var hasTreatment = CaseCloseQuery.HasTreatmentInWindow(historyEvents, 1, t3, now);
            Assert.That(hasTreatment, Is.True,
                "复诊窗口 [t₃, now] 内有新处置@t₄ ⇒ 放行");

            var decision = CaseCloseDecider.Decide(
                isOpen: CaseCloseQuery.IsOpen(caseEvents, revisitCaseId, Decoder),
                isClosed: CaseCloseQuery.IsClosed(caseEvents, revisitCaseId, Decoder),
                hasTreatmentInWindow: hasTreatment, isChecked: true);
            Assert.That(decision, Is.EqualTo(CaseCloseDecision.CanClose),
                "复诊窗口内有新处置 ⇒ 可结案(首案的 CaseClosed 不得外溢)");
        }

        // ════════════════════════════════════════════════════════════════════
        // AC-37-03:同一 case_id 第二条 CaseClosed 幂等拒收
        // ════════════════════════════════════════════════════════════════════

        /// <summary>AC-37-03:案件已结 → 幂等拒收(第二条 CaseClosed 不产生新事件)。
        /// 🔴 2026-10-15(S11-8 · 补评件 A-6/A3):原版硬编码 `isOpen: true, isClosed: true`
        /// —— 该组合在流派生口径下**不可能出现**(isOpen = 开过 ∧ ¬isClosed ⇒ 已结时 isOpen 必假),
        /// 即「测的是不可达状态」。现两入参均由**真流**派生,测的才是生产路径。</summary>
        [Test]
        public void test_ac3703_alreadyClosed_idempotentReject()
        {
            var patient = new PatientId(1);
            var caseId = MakeCaseId(100, 1, 0);

            // 已结案的流:立案@100 → 结案@200
            var caseEvents = new[]
            {
                MakeEvent(100, patient, 0, EventKind.CaseOpened),
                MakeClosed(200, caseId, 1),
            };

            var isOpen = CaseCloseQuery.IsOpen(caseEvents, caseId, Decoder);
            var isClosed = CaseCloseQuery.IsClosed(caseEvents, caseId, Decoder);
            Assert.That(isClosed, Is.True, "该案确已结");
            Assert.That(isOpen, Is.False, "已结案不再处于「开」态(流派生口径的必然结果)");

            var decision = CaseCloseDecider.Decide(
                isOpen: isOpen, isClosed: isClosed, hasTreatmentInWindow: true, isChecked: true);
            Assert.That(decision, Is.EqualTo(CaseCloseDecision.RejectAlreadyClosed),
                "案件已结须幂等拒收(AC-37-03)");
        }

        /// <summary>AC-37-03 Edge:同 tick 双意图 → 恰一条 CaseClosed(幂等)。
        /// 🔴 2026-10-15(S11-8):第二次意图的两入参亦改为**由同一流派生**(原版又一次硬编码
        /// `isOpen: true, isClosed: true` = 不可达状态)。</summary>
        [Test]
        public void test_ac3703_edge_sameTick_doubleIntent_idempotent()
        {
            const long openTick = 100;
            const long closeTick = 200; // 同 tick 双意图
            var patient = new PatientId(1);
            var caseId = MakeCaseId(openTick, 1, 0);

            var caseEvents = new List<SimEvent>
            {
                MakeEvent(openTick, patient, 0, EventKind.CaseOpened),
            };

            // 第一次结案意图(流上尚无 CaseClosed)
            var first = CaseCloseDecider.Decide(
                isOpen: CaseCloseQuery.IsOpen(caseEvents, caseId, Decoder),
                isClosed: CaseCloseQuery.IsClosed(caseEvents, caseId, Decoder),
                hasTreatmentInWindow: true, isChecked: true);
            Assert.That(first, Is.EqualTo(CaseCloseDecision.CanClose));

            // 第一次意图落盘
            caseEvents.Add(MakeClosed(closeTick, caseId, 1));

            // 第二次结案意图(同 tick):两入参由**同一流派生**
            var second = CaseCloseDecider.Decide(
                isOpen: CaseCloseQuery.IsOpen(caseEvents, caseId, Decoder),
                isClosed: CaseCloseQuery.IsClosed(caseEvents, caseId, Decoder),
                hasTreatmentInWindow: true, isChecked: true);
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
        //
        // ⚠️ **权威 = ADR-008 §四 `:216-249`**(🟢 2026-10-15 补立)。本处此前**是全案唯一枚举处**
        //    (GDD F-37.2 / 规则六 / registry 三处皆不枚举)即 story-003 补评 **B5** 所指的
        //    「实施侧无权威可依 ⇒ 实现自行其是」。
        //    定义:`treatment event := e.Kind ∈ { EmergencyTreatmentApplied, DrugTreatmentApplied, CareApplied }`
        //    **闭环纪律**:增删成员须**同时**改 adr-008 §四 + `case-system.md` F-37.2 `:666`/`:679`
        //    + 规则六 `:254` + 规则八 `:280`,**四处口径一致,禁止任一处单独扩集**。
        //    本段期望值**逐字引自该枚举**,不以生产代码默认值为真源(S11-10 AC)。
        // ════════════════════════════════════════════════════════════════════

        /// <summary>权威处置集 —— **逐字引自 ADR-008 §四 `:229`**(不引自生产代码默认值)。</summary>
        private static readonly EventKind[] AuthorityTreatmentSet =
        {
            EventKind.EmergencyTreatmentApplied,  // 10 急救已施加(ADR-009 Amendment I)
            EventKind.DrugTreatmentApplied,       // 11 用药已施加
            EventKind.CareApplied,                // 8  护理动作(病史流)
        };

        /// <summary>处置集含 EmergencyTreatmentApplied(10 急救**已施加**)。</summary>
        [Test]
        public void test_treatmentKinds_containsEmergencyTreatment()
        {
            Assert.That(TreatmentKinds.Contains(EventKind.EmergencyTreatmentApplied), Is.True,
                "处置集须含 EmergencyTreatmentApplied(10 急救已施加)");
        }

        /// <summary>处置集含 DrugTreatmentApplied(11 用药**已施加**)。</summary>
        [Test]
        public void test_treatmentKinds_containsDrugTreatment()
        {
            Assert.That(TreatmentKinds.Contains(EventKind.DrugTreatmentApplied), Is.True,
                "处置集须含 DrugTreatmentApplied(11 用药已施加)");
        }

        /// <summary>🔴 S11-10 回归哨兵(补评件 story-003 §二 B5):处置集须含 <c>CareApplied</c>(8 护理)。
        /// 权威枚举第 3 员;修前实现**未收** —— 取舍方向与语义**完全相反**
        /// (错收战斗伤害,漏收护理)。</summary>
        [Test]
        public void test_treatmentKinds_containsCareApplied()
        {
            Assert.That(TreatmentKinds.Contains(EventKind.CareApplied), Is.True,
                "处置集须含 CareApplied(8 护理)——「照顾」是玩家对病人做的**处置**," +
                "代价最轻的一档,必须收(ADR-008 §四 :236)");
        }

        /// <summary>🔴 S11-10 回归哨兵(补评件 story-003 §二 B5):处置集**不得**含 <c>InjuryOnset</c>。
        /// 它是 **25 的战斗伤害结算** ⇒ 收它 = 「病人挨了打」被计入「已被处置」⇒
        /// 玩家把病人打到即可满足结案前置,**正面击穿** `case-system.md:567`/`:839`
        /// 「玩家空手连关三例 ⇒ **不可能**」。</summary>
        [Test]
        public void test_treatmentKinds_excludesInjuryOnset()
        {
            Assert.That(TreatmentKinds.Contains(EventKind.InjuryOnset), Is.False,
                "InjuryOnset 是 25 的战斗伤害结算 —— 玩家对病人的**伤害不构成治疗证据**" +
                "(ADR-008 §四 :241-242:「收它 = 把『打死』与『治好』等价」)");
        }

        /// <summary>明确排除:<c>EmergencyAttempt</c> 是 10 的**判定输入**而非结算
        /// (ADR-009 Amendment I 三段式第一步)。「试了」≠「处置了」——
        /// 修前此点**已正确**,本测守住不回归。</summary>
        [Test]
        public void test_treatmentKinds_excludesEmergencyAttempt()
        {
            Assert.That(TreatmentKinds.Contains(EventKind.EmergencyAttempt), Is.False,
                "EmergencyAttempt 是判定输入而非结算 ——「试了」≠「处置了」(ADR-009 Amendment I)");
        }

        /// <summary>🔴 S11-10 双向穷尽:生产集 **恰等于**权威枚举(不多不少)。
        /// 单向 <c>Contains</c> 断言**抓不到**「偷偷多收一员」—— 而 B5 正是**错收** `InjuryOnset`
        /// 这样逃过测试面的(旧测 `test_treatmentKinds_containsInjuryOnset` 反向**断言**了该缺陷)。
        /// 本测是「处置集 == 权威」的**充要**判据。</summary>
        [Test]
        public void test_treatmentKinds_equalsAuthorityExactly()
        {
            var actual = new HashSet<EventKind>(TreatmentKinds.MembersSnapshot());
            var authority = new HashSet<EventKind>(AuthorityTreatmentSet);

            Assert.That(authority.Count, Is.EqualTo(3),
                "权威枚举恰 3 员(ADR-008 §四 :229 —— 本断言防夹具自身被改宽)");
            Assert.That(actual.Count, Is.EqualTo(authority.Count),
                "处置集成员数须等于权威枚举数 —— " +
                $"实际 = {{{string.Join(", ", actual)}}}");
            Assert.That(actual.SetEquals(authority), Is.True,
                "处置集须**恰等于** ADR-008 §四 的权威枚举 —— " +
                $"实际 = {{{string.Join(", ", actual)}}};" +
                $"权威 = {{{string.Join(", ", authority)}}}");
        }

        /// <summary>🔴 A-1(顺批 · S11-10):`MembersSnapshot()` 返**副本** ⇒ 调用方改不动真源。
        /// 原实现暴露可变 `public static readonly HashSet` ⇒ 任一处 `Set.Add(...)` 即**静默扩集**。</summary>
        [Test]
        public void test_treatmentKinds_snapshotIsIsolatedFromSource()
        {
            var snapshot = TreatmentKinds.MembersSnapshot();
            Assert.That(snapshot.Length, Is.EqualTo(3), "快照含 3 员");

            // 试图污染快照 —— 真源须纹丝不动
            for (int i = 0; i < snapshot.Length; i++)
                snapshot[i] = EventKind.InjuryOnset;

            Assert.That(TreatmentKinds.Contains(EventKind.InjuryOnset), Is.False,
                "改动快照**不得**影响真源(副本语义)");
            Assert.That(new HashSet<EventKind>(TreatmentKinds.MembersSnapshot())
                        .SetEquals(AuthorityTreatmentSet), Is.True,
                "真源仍恰等于权威枚举");
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

        /// <summary>AC-37-18:案件开着(无 <c>CaseClosed</c>)⇒ 仍为「开」,且**只有 <c>CaseClosed</c> 能结案**。
        /// <para>⚠️ <b>2026-10-15 判据收窄</b>(用户裁定「裁『不进流』+ 改判据」):病人死亡
        /// <b>不进事件流</b> ⇒ 37 对它 <b>结构性不可见</b>,原「死亡输入」<b>不可表达</b>
        /// (该事件不存在)⇒ 本 AC 的可测内容 = <b>37 无任何自动结案路径</b>
        /// (结案唯一出口 = 玩家显式动作,GDD 规则六)。</para>
        /// <para><b>判别半边</b>:流里塞满各式<b>非 <c>CaseClosed</c></b> 事件
        /// (治疗 / 处置 / 伤情 / 判断),案件仍须为「开」—— 若日后有人加入任何
        /// 「某事件到达即自动结案」的路径,本断言转红。这正是原夹具
        /// (只造一条 <c>CaseOpened</c>)所缺的判别力。</para></summary>
        [Test]
        public void test_ac3718_caseOpen_canClose()
        {
            var caseEvents = new[]
            {
                MakeEvent(100, new PatientId(1), 0, EventKind.CaseOpened),
                MakeEvent(110, new PatientId(1), 1, EventKind.DrugTreatmentApplied),
                MakeEvent(120, new PatientId(1), 2, EventKind.EmergencyTreatmentApplied),
                MakeEvent(130, new PatientId(1), 3, EventKind.CareApplied),
                MakeEvent(140, new PatientId(1), 4, EventKind.InjuryOnset),
                MakeEvent(150, new PatientId(1), 5, EventKind.JudgmentRecorded),
            };
            var caseId = MakeCaseId(100, 1, 0);

            var isOpen = CaseCloseQuery.IsOpen(caseEvents, caseId, Decoder);
            Assert.That(isOpen, Is.True,
                "非 CaseClosed 事件(治疗 / 处置 / 伤情 / 判断)不得结案 ⇒ 案件仍开着(AC-37-18 判别半边)");

            var decision = CaseCloseDecider.Decide(
                isOpen: isOpen,
                isClosed: CaseCloseQuery.IsClosed(caseEvents, caseId, Decoder), // 2026-10-15(S11-8):流派生取代硬编码
                hasTreatmentInWindow: true, isChecked: true);
            Assert.That(decision, Is.EqualTo(CaseCloseDecision.CanClose),
                "病例未结 + 已处置 + 已勾选 ⇒ 可结案(结案唯一出口 = 玩家显式动作,非自动)");
        }

        // ════════════════════════════════════════════════════════════════════
        // AC-37-18「空病种集」半边 —— **不在本文件**(2026-10-15 订正)
        // ════════════════════════════════════════════════════════════════════
        //
        // 原稿在此有两条测试(`test_ac3718_emptyDiseaseName_canClose` /
        // `test_ac3718_pureInjuryCase_emptyDiseaseSet_canClose`),经 postfix 评审核出
        // **两条均恒真**,已**删除**:
        //
        //   ① 两条都只调 `CaseCloseDecider.Decide(isOpen:true, isClosed:false,
        //      hasTreatmentInWindow:true, isChecked:true)` —— 与 happy path **逐字相同**;
        //      `Decide` 的形参里**根本没有 disease_set**,该维度在决策层**结构性不可表达**
        //      ⇒ 两条断言在**任何**实现下都为真(可捕获性判定 = 不可捕获)。
        //   ② `emptyDiseaseSet` 构造后**从未传给任何东西**(只自断 `.Bits == 0UL`)。
        //   ③ 「病名」是**词表 / UI 概念**(37 的 Sim 层无病名,只有 `disease_set` 位集)
        //      ⇒ 该半边的**语义载体不在本层** —— 如实登记 **NOT-RUN**,不冒充覆盖。
        //
        // **真覆盖落点**(空 disease_set 走**生产结案路径** + 载荷快照回读逐位为空):
        //   `case_close_writer_test.cs` → `test_ac3718_pureInjuryCase_emptyDiseaseSet_closesWithEmptySnapshot`
        //   —— 该处有真装配(真流 + 真写者 + 真 blob 池),能真正把空集喂进 `TryClose`。
        //   ⚠️ 本文件自陈「纯数据层,不触 codec」,故**不在此**引入写者装配(否则该声明成假)。
        //
        // 本文件保留的 AC-37-18 半边 = 上方 `test_ac3718_caseOpen_canClose`
        // (判别半边:塞满非 `CaseClosed` 事件 ⇒ 案件仍须为「开」)。

        // ════════════════════════════════════════════════════════════════════

        // ════════════════════════════════════════════════════════════════════
        // 结案副作用序列:CaseClosed 入病例流
        // ════════════════════════════════════════════════════════════════════

        /// <summary>结案副作用:CaseClosed 落病例流(StreamRouting)。</summary>
        [Test]
        public void test_closeSideEffect_caseClosedGoesToCaseStream()
        {
            var closedEvent = MakeClosed(200, MakeCaseId(100, 1, 0), 1);
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
            Assert.That(CaseCloseQuery.IsOpen(caseEvents, caseId, Decoder), Is.True);
        }

        /// <summary>IsOpen:有 CaseOpened + CaseClosed → false。</summary>
        [Test]
        public void test_isOpen_openedThenClosed_returnsFalse()
        {
            var caseEvents = new[]
            {
                MakeEvent(100, new PatientId(1), 0, EventKind.CaseOpened),
                MakeClosed(200, MakeCaseId(100, 1, 0), 1),
            };
            var caseId = MakeCaseId(100, 1, 0);
            Assert.That(CaseCloseQuery.IsOpen(caseEvents, caseId, Decoder), Is.False);
        }

        /// <summary>IsClosed:有 CaseClosed → true。</summary>
        [Test]
        public void test_isClosed_hasClosed_returnsTrue()
        {
            var caseEvents = new[]
            {
                MakeEvent(100, new PatientId(1), 0, EventKind.CaseOpened),
                MakeClosed(200, MakeCaseId(100, 1, 0), 1),
            };
            var caseId = MakeCaseId(100, 1, 0);
            Assert.That(CaseCloseQuery.IsClosed(caseEvents, caseId, Decoder), Is.True);
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
            Assert.That(CaseCloseQuery.IsClosed(caseEvents, caseId, Decoder), Is.False);
        }

        /// <summary>IsOpen:复诊(结案后再立案)→ 新案开着。</summary>
        [Test]
        public void test_isOpen_revisit_newCaseIsOpen()
        {
            var caseEvents = new[]
            {
                MakeEvent(100, new PatientId(1), 0, EventKind.CaseOpened),
                MakeClosed(200, MakeCaseId(100, 1, 0), 1),
                MakeEvent(300, new PatientId(1), 2, EventKind.CaseOpened),
            };
            var newCaseId = MakeCaseId(300, 1, 2);
            Assert.That(CaseCloseQuery.IsOpen(caseEvents, newCaseId, Decoder), Is.True,
                "复诊新案开着");
        }

        /// <summary>🔴 B-1 回归哨兵(补评件 story-003 §二 B-1 · sprint-11 S11-8):
        /// 复诊新案不得因**首案**的 CaseClosed 被判「已结」。</summary>
        [Test]
        public void test_isClosed_revisitNewCase_notClosed()
        {
            // 流:[首诊立案@100] → [首诊结案@200] → [复诊立案@300]
            // 复诊是**新的一例**(GDD 规则六-4 / EC-37-1:case_id 三元组不同 ⇒ 幂等判据不适用)。
            var caseEvents = new[]
            {
                MakeEvent(100, new PatientId(1), 0, EventKind.CaseOpened),
                MakeClosed(200, MakeCaseId(100, 1, 0), 1),
                MakeEvent(300, new PatientId(1), 2, EventKind.CaseOpened),
            };

            Assert.That(CaseCloseQuery.IsClosed(caseEvents, MakeCaseId(300, 1, 2), Decoder), Is.False,
                "复诊新案(300,1,2)不得因首案的 CaseClosed(200,1,1) 被判为已结 —— " +
                "否则 Decide 走 RejectAlreadyClosed,复诊案永远无法结案" +
                "(GDD :250-256 幂等定义 = **同一** case_id;EC-37-1 复诊 = 新病例)");

            // 反向对照:同一流上首案本身**确实**已结 —— 防掉进「一律返回 false」的假修。
            Assert.That(CaseCloseQuery.IsClosed(caseEvents, MakeCaseId(100, 1, 0), Decoder), Is.True,
                "首案(100,1,0)确已被 CaseClosed(200,1,1) 结掉");
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
            var isOpen = CaseCloseQuery.IsOpen(caseEvents, caseId, Decoder);
            var isClosed = CaseCloseQuery.IsClosed(caseEvents, caseId, Decoder);
            var decision = CaseCloseDecider.Decide(isOpen, isClosed, hasTreatment, isChecked: true);
            Assert.That(decision, Is.EqualTo(CaseCloseDecision.CanClose), "可结案");

            // 4. 写入 CaseClosed
            caseEvents.Add(MakeClosed(t3, caseId, 1));

            // 5. 幂等拒收(2026-10-15 S11-8:两入参均流派生 —— 原版 isOpen 硬编码为 true)
            var isOpenAfter = CaseCloseQuery.IsOpen(caseEvents, caseId, Decoder);
            var isClosedAfter = CaseCloseQuery.IsClosed(caseEvents, caseId, Decoder);
            Assert.That(isClosedAfter, Is.True, "已结");
            var secondDecision = CaseCloseDecider.Decide(
                isOpen: isOpenAfter, isClosed: isClosedAfter, hasTreatmentInWindow: true, isChecked: true);
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
            var isOpen = CaseCloseQuery.IsOpen(caseEvents, caseId, Decoder);
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

        // ════════════════════════════════════════════════════════════════════
        // 端到端组装:病例流 + 病史流 → CaseCloseQuery → CaseCloseDecider.Decide
        //
        // 🔴 2026-10-15(S11-8 · 补评件 ADVISORY A3):本文件此前**无一条**把两条流
        //    组装进 Decide 的用例 —— 每条 Decide 用例都自带 bool 入参,于是 B-1
        //    (IsClosed 按 Patient 匹配)在测试面**结构性隐形**。以下用例全程不出现
        //    硬编码状态标志,是 B-1 的端到端回归守卫。
        // ════════════════════════════════════════════════════════════════════

        /// <summary>端到端:首诊「开→治→结」,复诊「开→(窗口内无处置)拒→补治→结→幂等拒」。
        /// 每一步的两个 bool 入参均由**两条流**派生。</summary>
        [Test]
        public void test_e2e_revisitClosePath_deriveBothFlagsFromStreams()
        {
            var patient = new PatientId(1);
            var historyEvents = new List<SimEvent>();   // 病史流(处置证据)
            var caseEvents = new List<SimEvent>();      // 病例流(开 / 结)

            // ── 首诊:立案 @100 ─────────────────────────────────────────────
            caseEvents.Add(MakeEvent(100, patient, 0, EventKind.CaseOpened));
            var firstCase = MakeCaseId(100, 1, 0);
            Assert.That(CaseCloseQuery.IsOpen(caseEvents, firstCase, Decoder), Is.True, "首诊案开着");

            // ── 首诊:处置 @150 → 结案尝试 @200 ───────────────────────────
            historyEvents.Add(MakeEvent(150, patient, 0, EventKind.EmergencyTreatmentApplied));
            const long firstCloseTick = 200;
            var firstHasTreatment = CaseCloseQuery.HasTreatmentInWindow(historyEvents, 1, 100, firstCloseTick);
            Assert.That(firstHasTreatment, Is.True, "首诊窗口 [100,200] 内有处置@150");

            Assert.That(
                CaseCloseDecider.Decide(
                    CaseCloseQuery.IsOpen(caseEvents, firstCase, Decoder),
                    CaseCloseQuery.IsClosed(caseEvents, firstCase, Decoder),
                    firstHasTreatment, isChecked: true),
                Is.EqualTo(CaseCloseDecision.CanClose), "首诊可结案");

            caseEvents.Add(MakeClosed(firstCloseTick, firstCase, 1));
            Assert.That(CaseCloseQuery.IsClosed(caseEvents, firstCase, Decoder), Is.True, "首诊案已结");
            Assert.That(CaseCloseQuery.IsOpen(caseEvents, firstCase, Decoder), Is.False, "首诊案不再开着");

            // ── 复诊:立案 @300(**新的一例** —— EC-37-1)──────────────────
            caseEvents.Add(MakeEvent(300, patient, 2, EventKind.CaseOpened));
            var revisitCase = MakeCaseId(300, 1, 2);
            Assert.That(CaseCloseQuery.IsOpen(caseEvents, revisitCase, Decoder), Is.True, "复诊新案开着");

            // 🔴 B-1 核心断言:首案的 CaseClosed(@200)不得外溢到复诊案
            Assert.That(CaseCloseQuery.IsClosed(caseEvents, revisitCase, Decoder), Is.False,
                "复诊案**未结** —— 修前按 Patient 匹配 ⇒ 首案 CaseClosed 外溢 ⇒ 此处为 true");
            // 反向对照:首案自身仍为已结(防「一律返回 false」的假修)
            Assert.That(CaseCloseQuery.IsClosed(caseEvents, firstCase, Decoder), Is.True,
                "首案自身仍为已结(反向对照)");

            // ── 复诊:结案尝试 @400,但窗口内无处置 ────────────────────────
            const long now = 400;
            var revisitHasTreatment = CaseCloseQuery.HasTreatmentInWindow(historyEvents, 1, 300, now);
            Assert.That(revisitHasTreatment, Is.False, "首诊处置@150 不在复诊窗口 [300,400] 内");

            Assert.That(
                CaseCloseDecider.Decide(
                    CaseCloseQuery.IsOpen(caseEvents, revisitCase, Decoder),
                    CaseCloseQuery.IsClosed(caseEvents, revisitCase, Decoder),
                    revisitHasTreatment, isChecked: true),
                Is.EqualTo(CaseCloseDecision.RejectNoTreatment),
                "复诊无新处置 ⇒ 拒绝(**理由码**须是 RejectNoTreatment,不得被 B-1 顶替成 RejectAlreadyClosed)");

            // ── 复诊:补处置 @350 → 可结案 ─────────────────────────────────
            historyEvents.Add(MakeEvent(350, patient, 1, EventKind.DrugTreatmentApplied));
            revisitHasTreatment = CaseCloseQuery.HasTreatmentInWindow(historyEvents, 1, 300, now);
            Assert.That(revisitHasTreatment, Is.True, "复诊窗口内有新处置@350");

            Assert.That(
                CaseCloseDecider.Decide(
                    CaseCloseQuery.IsOpen(caseEvents, revisitCase, Decoder),
                    CaseCloseQuery.IsClosed(caseEvents, revisitCase, Decoder),
                    revisitHasTreatment, isChecked: true),
                Is.EqualTo(CaseCloseDecision.CanClose),
                "🔴 复诊可结案 —— B-1 未修时此处恒为 RejectAlreadyClosed,复诊案**永远结不掉**");

            // ── 复诊结案 @400 ─────────────────────────────────────────────
            caseEvents.Add(MakeClosed(now, revisitCase, 3));
            Assert.That(CaseCloseQuery.IsClosed(caseEvents, revisitCase, Decoder), Is.True, "复诊案已结");
            Assert.That(CaseCloseQuery.IsOpen(caseEvents, revisitCase, Decoder), Is.False, "复诊案不再开着");

            // ── 幂等:复诊案的第二条结案意图 ───────────────────────────────
            Assert.That(
                CaseCloseDecider.Decide(
                    CaseCloseQuery.IsOpen(caseEvents, revisitCase, Decoder),
                    CaseCloseQuery.IsClosed(caseEvents, revisitCase, Decoder),
                    revisitHasTreatment, isChecked: true),
                Is.EqualTo(CaseCloseDecision.RejectAlreadyClosed),
                "复诊案的第二条 CaseClosed 幂等拒收(AC-37-03)");
        }
    }
}
