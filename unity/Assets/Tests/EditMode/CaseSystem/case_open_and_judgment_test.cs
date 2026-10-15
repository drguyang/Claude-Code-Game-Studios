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
// ⚠️ 本测试**以纯数据层为主**:被测对象 = Sim 的 CaseOpenDecider / CaseStreamQuery(生产代码),
//    不触引擎 API,不触持久化。
//
// ⚠️ 2026-10-15 断言形态订正批(补评件 review-case-system-story-002-retro-2026-10-15.md
//    §七·三 / §七·四 净保留的 5 条):本批修 BLK-1 恒真半边 · BLK-2 常量比较 ·
//    BLK-3 自造 J(c) · BLK-5 重复断言 + S-8.3 未验 · BLK-9 负断言未按契约构造。
//    其中**两条**要的是「病例流」上的断言(药材短缺 ⇒ 零 `CaseOpened`;S-8.3 空判断)**——纯数据层
//    做不到**:自己造的 `List<SimEvent>` 断言「零 CaseOpened」是空跑(测试自己没放就没放)。
//    故这两条按仓内既有先例(CaseSystem/case_open_writer_test.cs「真装配,禁 Fake」)走
//    CompositionRoot.Assemble() + 真 EventStream + 真 PayloadEncoder ⇒ 本文件自本批起
//    **触 codec**(仅为载荷回读),此为可证伪的必要代价,不是无谓扩大依赖面。

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
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

        /// <summary>病例流内 <see cref="EventKind.CaseOpened"/> 条数。
        /// <para><b>负存在断言的扫描闭集</b>(GDD 负存在断言纪律:点名单一出处,不得写「所有」)
        /// = **病例流全部事件**;载体 = 生产写者 <see cref="CaseOpenWriter"/>。</para></summary>
        private static int CountCaseOpened(IReadOnlyList<SimEvent> events)
        {
            int n = 0;
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Kind == EventKind.CaseOpened)
                    n++;
            }
            return n;
        }

        // ════════════════════════════════════════════════════════════════════
        // AC-37-30:路径 A 与路径 B 各产生 CaseOpened 入病例流
        // ════════════════════════════════════════════════════════════════════

        /// <summary>AC-37-30:路径 A(52 事件注入)与路径 B(自然就诊)的**立案决策同义务**。
        ///
        /// <para>⚠️ <b>2026-10-15 断言形态订正(补评件 §六·二 BLK-1 的恒真半边)</b>:原第三条断言
        /// <c>decisionA == decisionB</c> **已删** —— 它由上面两条断言**逻辑蕴含**
        /// (两者皆 <c>OpenNew</c> ⇒ 必相等),且在 <see cref="CaseOpenDecider.Decide"/>
        /// **从不读 <c>sourceKind</c>**(「只作记录,不改变决策」= 因果订正的执行面)的前提下,
        /// 该断言**构造上不可能为假** ⇒ 判别力 0。**不**以加大输入 / 注释重新包装它。</para>
        ///
        /// <para>AC-37-30 的**流级**义务(「各产生 <c>CaseOpened</c> **入病例流**」)在纯决策函数层
        /// **不可验**(本层压根没有 `IEventSink`)。其真载体(本 story §Test Evidence 引用面):
        /// <c>CaseSystem/case_open_writer_test.cs</c>(真组合根 + 真流,断言病例流恰增 1 条)
        /// 与 <c>PlayMode/case_opened_wiring_test.cs</c>(真启动序,BootRoot → CaseOpenedDriver → TryOpen)。</para></summary>
        [Test]
        public void test_ac3730_pathA_and_pathB_bothProduceCaseOpened()
        {
            // 路径 A:52 注入 + 抵达交互
            var decisionA = CaseOpenDecider.Decide(hasOpenCase: false, hasPatient: true, sourceKind: CaseOpenSource.A);
            Assert.That(decisionA, Is.EqualTo(CaseOpenDecision.OpenNew),
                "路径 A 须立案(入流义务不因触发者不同而不同)");

            // 路径 B:13 自然就诊
            var decisionB = CaseOpenDecider.Decide(hasOpenCase: false, hasPatient: true, sourceKind: CaseOpenSource.B);
            Assert.That(decisionB, Is.EqualTo(CaseOpenDecision.OpenNew),
                "路径 B 须立案(入流义务不因触发者不同而不同)");
        }

        /// <summary>AC-37-30 Edge / AC-37-02:同 tick 一 A 一 B(不同病人)→ 两条 <c>CaseOpened</c>,
        /// 两个 <c>case_id</c> 不同。
        ///
        /// <para>⚠️ <b>2026-10-15 断言形态订正(补评件 §六·二 BLK-2)</b>:原稿的
        /// <c>MakeCaseId(100,1,0) != MakeCaseId(100,2,0)</c> 由测试**字面量**构造 ⇒ <c>1 ≠ 2</c> 恒真,
        /// 与生产毫无关系。改为**由流派生**:两个 <c>case_id</c> 各自取自生产查询
        /// <see cref="CaseStreamQuery.OpenCaseOf"/>(case_id = 流中 <c>CaseOpened</c> 的三元组,
        /// ADR-008 §二)⇒ 测试不再自钉期望值。</para></summary>
        [Test]
        public void test_ac3730_edge_sameTick_pathA_pathB_differentPatients_caseIdsDiffer()
        {
            const long tick = 100;

            // 同 tick 一 A(病人 1)一 B(病人 2)各立案一次 ⇒ 两条 CaseOpened 入流
            var stream = new[]
            {
                MakeEvent(tick, new PatientId(1), 0, EventKind.CaseOpened),
                MakeEvent(tick, new PatientId(2), 0, EventKind.CaseOpened),
            };

            // 期望值由**生产查询 / 流派生**,不由测试字面钉死
            var caseIdA = CaseStreamQuery.OpenCaseOf(stream, 1);
            var caseIdB = CaseStreamQuery.OpenCaseOf(stream, 2);

            Assert.That(caseIdA.HasValue, Is.True, "病人 1 的开案须在流中可见");
            Assert.That(caseIdB.HasValue, Is.True, "病人 2 的开案须在流中可见");
            Assert.That(caseIdA.Value, Is.Not.EqualTo(caseIdB.Value),
                "同 tick 不同病人 ⇒ case_id 不同(三元组含 Patient,ADR-008 §二 / 承 story-001 AC-37-02)");
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

        /// <summary><b>点名 fixture</b>:52 注入的「药材短缺」医疗内容条目。
        /// <para>定义特征 = **无病人**(出处 = `case-system.md:199` GDD 规则二注:「37 注入的三个
        /// 条目里,只有两个立案。药材短缺不立案 —— 它是缺资源的**环境事件**,没有病人」)。
        /// 生产侧对它的编码 = <see cref="PatientId.None"/> 入参;</para>
        /// <para>载体 = 生产写者 <see cref="CaseOpenWriter"/> 前置 ①(「病人存在」);
        /// 扫描闭集 = **病例流全部事件**(见 <see cref="CountCaseOpened"/>)。</para></summary>
        private static readonly PatientId MedicineShortageFixture = PatientId.None;

        /// <summary>药材短缺注入 ⇒ 零 CaseOpened(不立案的负断言,点名 fixture)。
        ///
        /// <para>⚠️ <b>2026-10-15 断言形态订正(补评件 §六·二 BLK-9)</b>:原稿只把
        /// <c>hasPatient: false</c> 当布尔喂进决策函数 —— **未注入药材短缺条目、无点名 fixture、
        /// 也无「零 CaseOpened」的流断言**(「零写入」这件事根本没碰过流)。现按 AC 契约补齐:
        /// 走**真装配 + 真 <c>EventStream</c> + 真写者**,断言病例流**零** <c>CaseOpened</c>。</para>
        ///
        /// <para><b>反证设计(把实现改错 ⇒ 本测转红)</b>:删掉 <see cref="CaseOpenWriter.TryOpen"/>
        /// 的 <c>if (patient.IsNone) return NoPatient;</c> 前置 —— 入参随即落到
        /// <c>!_presence.IsPresent(...)</c> 分支,返回码从 <c>NoPatient</c> 变成 <c>PatientAbsent</c>,
        /// 第一条断言红;若该分支也一并删掉 ⇒ 真流里多出一条 <c>CaseOpened</c>,第二条断言红。</para></summary>
        [Test]
        public void test_medicineShortage_noCaseOpened()
        {
            // Arrange:真装配(真流 / 真在场登记簿 / 真编码器)——「零写入」只有在真流上才有意义
            var bag = CompositionRoot.Assemble();
            var writer = new CaseOpenWriter(bag.EventSink, bag.Presence, bag.Encoder, bag.Stream.Events);
            int streamBefore = bag.Stream.Count;
            int caseBefore = CountCaseOpened(bag.Stream.Events);
            Assert.That(caseBefore, Is.EqualTo(0),
                "前置(防断言空跑):装配后尚无任何病人 ⇒ 病例流本无 CaseOpened");

            // Act:药材短缺注入下的立案尝试(无病人条目)
            var result = writer.TryOpen(tick: 100, patient: MedicineShortageFixture,
                                        diseaseSnapshot: new DiseaseIdSet(0b1UL));

            // Assert ①:具名不立案(零写入分支不静默)
            Assert.That(result, Is.EqualTo(CaseOpenWriteResult.NoPatient),
                "药材短缺不立案(无病人 —— GDD 规则二注;哨兵 / 负 id 同此路径)");

            // Assert ②:AC 字面 —— 病例流**零 CaseOpened**(闭集 = 病例流全部事件)
            Assert.That(bag.Stream.Count, Is.EqualTo(streamBefore), "药材短缺 ⇒ 流总事件数不变(零写入)");
            Assert.That(CountCaseOpened(bag.Stream.Events), Is.EqualTo(caseBefore),
                "药材短缺 ⇒ 零 CaseOpened(不得为短缺硬造病人 / 硬开病例)");
        }

        // ════════════════════════════════════════════════════════════════════
        // AC-37-05:判断记录事件化 + 存新值
        // ════════════════════════════════════════════════════════════════════

        /// <summary>AC-37-05:落笔/改写各产生一条事件进病例流,无内存私有副本。
        /// <para>⚠️ 本层只及**路由**(kindgen 依 `entities.yaml` 生成,见 `StreamRouting.g.cs`);
        /// 「经 `IEventSink` 真的多出一条事件」的真载体 = `Boot/judgment_writer_test.cs`
        /// (`test_record_onOpenCase_writesJudgment_caseStream`:病例流 `before + 1`、尾事件
        /// `Kind == JudgmentRecorded`)—— 见 story-002 §Test Evidence 的 AC→载体映射。</para></summary>
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

        /// <summary>AC-37-05 存新值:J(c) = **该案**全序最后一条判断事件(改写两次后取末值,非差分合成)。
        ///
        /// <para>⚠️ <b>2026-10-15 断言形态订正(补评件 §六·二 BLK-3 / §六·三 B3)</b>:原稿在测试内
        /// **自造 <c>J(c)</c>**(<c>.Where(...).OrderBy(EventOrder.KeyOf).Last()</c>),
        /// 一次也没调过生产函数 <see cref="CaseStreamQuery.LastJudgmentOf"/> ⇒ 测的是「LINQ 的
        /// <c>Last</c> 会取末条」,不是 37 的取法。现改调生产函数;期望值同样**由流派生**
        /// (<c>case_id</c> 取自生产查询,不由测试字面钉死)。</para>
        ///
        /// <para><b>反证设计(改错 ⇒ 转红)</b>:把 <c>LastJudgmentOf</c> 改成「取该案**第一条**判断」
        /// (<c>if (last == null) last = e;</c>)⇒ Tick / Seq 两条断言红;改成 <c>return null</c> ⇒
        /// HasValue 红;把 <c>inCase</c> 归属跟踪整段删掉(= S11-7 前的旧实现)⇒ **本测仍绿**
        /// (单案夹具不施压 <c>caseId</c>)——「按 <c>case_id</c> 归属」的反证由同文件两条跨案夹具
        /// (<c>test_lastJudgmentOf_crossPatientInterleaved_*</c> /
        /// <c>test_lastJudgmentOf_samePatientSecondCase_*</c>)与 `Boot/ink_age_test.cs` 承担。</para></summary>
        [Test]
        public void test_ac3705_storeNewValue_judgmentIsLastEvent()
        {
            const long tick = 100;
            var patient = new PatientId(1);

            // 立案(甲)+ 落笔(甲)+ 改写(乙)+ 改写(丙)
            var events = new[]
            {
                MakeEvent(tick, patient, 0, EventKind.CaseOpened),
                MakeEvent(tick + 10, patient, 1, EventKind.JudgmentRecorded),
                MakeEvent(tick + 50, patient, 2, EventKind.JudgmentRevised),
                MakeEvent(tick + 100, patient, 3, EventKind.JudgmentRevised),
            };

            // case_id 由**生产查询**从流派生(ADR-008 §二:case_id = CaseOpened 的三元组)
            var caseId = CaseStreamQuery.OpenCaseOf(events, patient.Value);
            Assert.That(caseId.HasValue, Is.True, "前置:该病人有开案");

            // J(c) —— 调**生产函数**,不自造
            var lastJudgment = CaseStreamQuery.LastJudgmentOf(events, caseId.Value);

            Assert.That(lastJudgment.HasValue, Is.True, "该案有判断记录 ⇒ 须返回末条");
            Assert.That(lastJudgment.Value.Kind, Is.EqualTo(EventKind.JudgmentRevised),
                "J(c) 须取全序最后一条判断事件");
            Assert.That(lastJudgment.Value.Tick, Is.EqualTo(tick + 100),
                "末值 tick = 最后一次改写的 tick(非首条、非差分合成)");
            Assert.That(lastJudgment.Value.Seq, Is.EqualTo(3),
                "改写两次后取末值(Seq=3),非差分合成");
        }

        /// <summary>AC-37-05 Edge / S-8.3:空判断**合法** —— `lexicon_id = 0` ∧ `freehand_text = ''`
        /// (`case-system.md:241`;`diagnosis-system.md` S-8.3「空是合法终态」)。
        ///
        /// <para>⚠️ <b>2026-10-15 断言形态订正(补评件 §六·二 BLK-5)</b>:原稿构造一条
        /// <c>default(PayloadRef)</c> 的事件后断言 <c>StreamRouting.Of(...) == StreamId.Case</c> ——
        /// 与 <see cref="test_ac3705_judgmentRecorded_and_revised_bothGoToCaseStream"/> 是
        /// **同一事实**的重复断言,而 S-8.3 的「空判断 = <c>lexicon_id 0</c> ∧
        /// <c>freehand_text ''</c>」**一字未验**。现删去重复的路由断言(保留判别力更强的那条 ——
        /// 它同时覆盖 Recorded / Revised 两支),改验 S-8.3 本体:擦除(写一条空判断)
        /// **不被拒收**、**真的落进病例流**、**载荷确为空**、且 **J(c) 取到它**(回到空态)。</para>
        ///
        /// <para><b>反证设计(改错 ⇒ 转红)</b>:给 <see cref="JudgmentWriter.TryRevise"/> 加
        /// 「<c>lexiconId &lt;= 0</c> ⇒ 拒收(具名返回码)」的校验 ⇒ 结果码与流增量两条断言红;
        /// 让擦除走成「不 Append」⇒ 流增量红;把载荷写成 <c>lexicon_id = 1</c> ⇒ 载荷断言红。</para>
        ///
        /// <para>⚠️ 本测只覆盖 **sim 可见的两半**(<c>lexicon_id = 0</c> + 事件真的入流)。
        /// 「<c>freehand_text = ''</c>」半不在此层:`CasePayloads.cs:3` 已定该值住呈现装配
        /// (ADR-006 Amendment A 的自由文本例外),P0 由写者固定传 <c>string.Empty</c>。</para></summary>
        [Test]
        public void test_ac3705_edge_emptyJudgment_isLegal()
        {
            // Arrange:真装配 + 真写者 + 该病人已开案(落笔 / 改判的前置)
            var bag = CompositionRoot.Assemble();
            var patient = bag.PatientSpawner.SpawnNext(diseaseId: 1, tick: 0);
            bag.Presence.AddPresent(patient, new WorldPos(0, 0, 0));
            bag.VitalsService.OnTickEdge(0);
            var caseWriter = new CaseOpenWriter(bag.EventSink, bag.Presence, bag.Encoder, bag.Stream.Events);
            Assert.That(caseWriter.TryOpen(tick: 1, patient: patient, diseaseSnapshot: default(DiseaseIdSet)),
                Is.EqualTo(CaseOpenWriteResult.Opened), "开案须成功(改判前置)");

            var writer = new JudgmentWriter(bag.EventSink, bag.Encoder, bag.Stream.Events,
                                            authorPlayerId: 9001);
            int before = bag.Stream.Count;

            // Act:擦除 = 写一条空判断(lexicon_id = 0 ∧ confidence = 0)—— **不是删事件**
            var result = writer.TryRevise(tick: 3, patient: patient, lexiconId: 0, confidence: 0);

            // Assert ①:空判断合法 —— 不得被拒收(S-8.3「空是合法终态」)
            Assert.That(result, Is.EqualTo(JudgmentWriteResult.Recorded),
                "空判断合法(S-8.3)—— 擦除不得被拒收");

            // Assert ②:擦除是**写一条新值**,不是删事件
            Assert.That(bag.Stream.Count, Is.EqualTo(before + 1), "擦除 ⇒ 病例流恰增 1 条(非删事件)");
            var tail = bag.Stream.Events[bag.Stream.Count - 1];
            Assert.That(tail.Kind, Is.EqualTo(EventKind.JudgmentRevised), "擦除 = JudgmentRevised");
            Assert.That(StreamRouting.Of(tail.Kind), Is.EqualTo(StreamId.Case), "擦除落病例流");

            // Assert ③:载荷确为「空」(lexicon_id = 0 ∧ confidence = 0)
            Assert.That(PayloadCodec.TryGetPayload(in tail, bag.BlobPool, out JudgmentRevisedPayload empty),
                Is.True, "空判断载荷须可解码回读");
            Assert.That(empty.LexiconId, Is.EqualTo(0), "空判断 lexicon_id = 0(case-system.md:241)");
            Assert.That(empty.Confidence, Is.EqualTo(0), "空判断 confidence = 0");

            // Assert ④:存新值 —— J(c) = 全序末条 ⇒ 取到擦除那条(回到空态,而非「无判断」)
            var openCase = CaseStreamQuery.OpenCaseOf(bag.Stream.Events, patient.Value);
            Assert.That(openCase.HasValue, Is.True, "该案仍开着");
            var j = CaseStreamQuery.LastJudgmentOf(bag.Stream.Events, openCase.Value);
            Assert.That(j.HasValue, Is.True, "空判断**是**判断事件(不是「无判断」)");
            Assert.That(j.Value.Tick, Is.EqualTo(3), "J(c) = 擦除那条(全序末条胜 ⇒ 回到空态)");
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
            // ⚠️ **插入序刻意打乱**([2,0,1])(2026-10-15 ADV-5)—— 原稿按 0/1/2 插入,
            //    而 `OrderBy` 对**已排序输入**是**恒等映射** ⇒ 该断言对「有无排序」零判别力。
            var events = new[]
            {
                MakeEvent(tick + 100, patient, 2, EventKind.JudgmentRevised),
                MakeEvent(tick, patient, 0, EventKind.JudgmentRecorded),
                MakeEvent(tick + 50, patient, 1, EventKind.JudgmentRevised),
            };

            // 全部落病例流
            foreach (var e in events)
            {
                Assert.That(StreamRouting.Of(e.Kind), Is.EqualTo(StreamId.Case),
                    "改写史每条都须落病例流(AC-37-23)");
            }

            // 三条事件按全序排列:输入序 [2,0,1] ⇒ 期望序 [0,1,2](非恒等)
            // 变异 = 去掉 OrderBy(直接按流内序取)⇒ sorted[0].Seq == 2 ⇒ 红
            var sorted = events.OrderBy(e => EventOrder.KeyOf(e)).ToList();
            Assert.That(sorted[0].Seq, Is.EqualTo(0), "全序首条 = Seq 0(输入序首条是 Seq 2)");
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

        /// <summary>综合流程:立案 → 落笔 → 改写 → 再改写,全部事件落病例流,存新值取末条。
        /// <para>⚠️ 2026-10-15:本测原亦**自造 <c>J(c)</c>**(<c>.Where().OrderBy().Last()</c>),
        /// 是补评件 §六·二 BLK-3 点名的第二处(与
        /// <see cref="test_ac3705_storeNewValue_judgmentIsLastEvent"/> 同批订正)——
        /// 现改调生产 <see cref="CaseStreamQuery.LastJudgmentOf"/>。</para></summary>
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

            // 存新值 = 末条 —— 调**生产函数**,不自造(见 summary 的 BLK-3 订正)
            var caseId = CaseStreamQuery.OpenCaseOf(allEvents, patient.Value);
            Assert.That(caseId.HasValue, Is.True, "前置:该病人有开案");
            var lastJudgment = CaseStreamQuery.LastJudgmentOf(allEvents, caseId.Value);

            Assert.That(lastJudgment.HasValue, Is.True, "该案有判断记录 ⇒ 须返回末条");
            Assert.That(lastJudgment.Value.Seq, Is.EqualTo(3), "存新值取末条(Seq=3)");
            Assert.That(lastJudgment.Value.Tick, Is.EqualTo(tick + 110), "末值 tick = 最后一次改写");
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
        //
        // ⚠️ 2026-10-15 S11-7 修复附注(story-002 §二 BLK-8):
        //   下列三条夹具**原本不含 CaseOpened** —— 它们编码的是旧契约
        //   「调用方传入该案事件子集」。但该契约在生产路径上**不可能被满足**
        //   (唯一调用方传 EventStream.Events = 全部混合流)⇒ 正是这三条让
        //   「caseId 死参」逃过测试面(同 S11-10 那条反向断言缺陷的旧测)。
        //   现按新契约(传整条流)重写,并补两条**跨案判别**夹具 —— 旧实现下必红。
        // ════════════════════════════════════════════════════════════════════

        /// <summary>LastJudgmentOf:该案无判断记录返回 null。</summary>
        [Test]
        public void test_lastJudgmentOf_noJudgment_returnsNull()
        {
            var events = new[]
            {
                MakeEvent(100, new PatientId(1), 0, EventKind.CaseOpened),
            };

            var result = CaseStreamQuery.LastJudgmentOf(events, MakeCaseId(100, 1, 0));
            Assert.That(result.HasValue, Is.False, "该案无判断记录须返回 null");
        }

        /// <summary>LastJudgmentOf:该案有判断记录返回末条。</summary>
        [Test]
        public void test_lastJudgmentOf_hasJudgment_returnsLast()
        {
            var events = new[]
            {
                MakeEvent(100, new PatientId(1), 0, EventKind.CaseOpened),
                MakeEvent(100, new PatientId(1), 1, EventKind.JudgmentRecorded),
                MakeEvent(150, new PatientId(1), 2, EventKind.JudgmentRevised),
                MakeEvent(200, new PatientId(1), 3, EventKind.JudgmentRevised),
            };

            var result = CaseStreamQuery.LastJudgmentOf(events, MakeCaseId(100, 1, 0));
            Assert.That(result.HasValue, Is.True, "有判断记录须返回末条");
            Assert.That(result.Value.Seq, Is.EqualTo(3), "末条 Seq=3");
            Assert.That(result.Value.Tick, Is.EqualTo(200), "末条 tick=200");
        }

        /// <summary>LastJudgmentOf:单条判断记录返回该条。</summary>
        [Test]
        public void test_lastJudgmentOf_singleJudgment_returnsThat()
        {
            var events = new[]
            {
                MakeEvent(100, new PatientId(1), 0, EventKind.CaseOpened),
                MakeEvent(100, new PatientId(1), 1, EventKind.JudgmentRecorded),
            };

            var result = CaseStreamQuery.LastJudgmentOf(events, MakeCaseId(100, 1, 0));
            Assert.That(result.HasValue, Is.True, "单条判断记录须返回该条");
            Assert.That(result.Value.Seq, Is.EqualTo(1));
        }

        /// <summary>🔴 S11-7 判别夹具:跨病人交错 —— 病人 2 的判断**全流更晚**,
        /// 但**不得**被算进病人 1 的案。
        /// <para>旧实现(取传入列表末条判断)在此**必错** —— 会返回病人 2 的 tick=140。
        /// 这正是 BLK-8 的实害:墨龄取错病人。</para></summary>
        [Test]
        public void test_lastJudgmentOf_crossPatientInterleaved_ignoresOtherPatientsJudgments()
        {
            var events = new[]
            {
                MakeEvent(100, new PatientId(1), 0, EventKind.CaseOpened),
                MakeEvent(100, new PatientId(2), 0, EventKind.CaseOpened),
                MakeEvent(110, new PatientId(1), 1, EventKind.JudgmentRecorded),
                MakeEvent(120, new PatientId(2), 1, EventKind.JudgmentRecorded),
                MakeEvent(130, new PatientId(1), 2, EventKind.JudgmentRevised),
                MakeEvent(140, new PatientId(2), 2, EventKind.JudgmentRevised),  // 全流末条 —— 病人 2 的
            };

            var result = CaseStreamQuery.LastJudgmentOf(events, MakeCaseId(100, 1, 0));

            Assert.That(result.HasValue, Is.True, "病人 1 本案有两笔判断,须返回其末条");
            Assert.That(result.Value.Patient.Value, Is.EqualTo(1), "须是病人 1 的事件");
            Assert.That(result.Value.Tick, Is.EqualTo(130),
                "病人 1 末条 tick=130;旧实现返回 140(病人 2 的)⇒ 墨龄取错病人");
        }

        /// <summary>🔴 S11-7 判别夹具:同一病人**复诊** —— 第二案的判断不得污染第一案。
        /// <para>这是「按 Patient 匹配」与「按 case_id 匹配」的分界:两者 Patient **相同**,
        /// 只有 CaseOpened 三元组不同。故本测**不能被跨病人夹具替代**。</para></summary>
        [Test]
        public void test_lastJudgmentOf_samePatientSecondCase_ignoresFirstCaseJudgments()
        {
            var events = new[]
            {
                MakeEvent(100, new PatientId(1), 0, EventKind.CaseOpened),   // 案 A
                MakeEvent(110, new PatientId(1), 1, EventKind.JudgmentRecorded),
                MakeEvent(200, new PatientId(1), 2, EventKind.CaseClosed),   // 案 A 结
                MakeEvent(300, new PatientId(1), 3, EventKind.CaseOpened),   // 案 B(复诊)
                MakeEvent(310, new PatientId(1), 4, EventKind.JudgmentRecorded),
            };

            var caseA = CaseStreamQuery.LastJudgmentOf(events, MakeCaseId(100, 1, 0));
            var caseB = CaseStreamQuery.LastJudgmentOf(events, MakeCaseId(300, 1, 3));

            Assert.That(caseA.HasValue, Is.True, "案 A 有判断记录");
            Assert.That(caseA.Value.Tick, Is.EqualTo(110),
                "案 A 只认 [100,200) 窗内的判断;旧实现返回 310(案 B 的)");
            Assert.That(caseB.HasValue, Is.True, "案 B 有判断记录");
            Assert.That(caseB.Value.Tick, Is.EqualTo(310), "案 B 只认 300 之后的判断");
        }
    }
}
