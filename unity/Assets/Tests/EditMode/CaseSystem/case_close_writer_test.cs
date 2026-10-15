// M2 接线轮阶段 2 · 批次 D —— CaseClosed 写者(37)端到端测试(EditMode,真装配,禁 Fake)。
//
// 覆盖:
//   ① 真装配袋(真 EventStream + 真 PayloadEncoder)调用写者 ⇒ **病例流恰增 1 条**,
//      Kind / 流别 / 载荷 round-trip 四字段 / Patient / Seq 逐项断言;
//   ② 前置条件(GDD 规则六):已结案 ⇒ 幂等拒收;未开案 ⇒ 拒;未勾选 ⇒ 拒;窗口内无处置 ⇒ 拒;
//   ③ 处置证据窗口方向(ADR-008 §四 2026-09-17 订正):闭区间 [caseId.Tick, closeTick],
//      两端边界各一条断言,窗口外 ⇒ 拒;
//   ④ 复诊新案可结(同一病人,后案独立于前案 —— caseId 精确关联,S11-8 B-1 面);
//   ⑤ treated 快照由写者按真流自算(非调用方转述)。
//
// 测试夹具说明:处置事件(8 护理 / 10 急救 / 11 用药)的载荷**解码不归本件** ——
//   `CaseCloseQuery.HasTreatmentInWindow` 是纯 Kind / Patient / Tick 过滤,不碰载荷;
//   故本测试以池内一合法引用(非解码对象)注入处置事件。**被测量是写者**,处置事件是**输入夹具**,
//   不构成「Fake 被测对象」(与 case_open_writer_test 的禁 Fake 口径一致)。
//
// 权威:design/gdd/case-system.md 规则六 / F-37.2 / AC-37-03 / AC-37-04 / AC-37-21 / EC-37-1 ·
//      entities.yaml SimEvent.Kind.CaseClosed(stream: case · author: 37)·
//      ADR-008 §一 / §三 / §四 · ADR-029 §③ · ADR-005(Seq 发号)

using System;
using System.Linq;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.CaseSystem
{
    /// <summary><see cref="CaseCloseWriter"/> 生产写入通道测试。</summary>
    [TestFixture]
    public sealed class CaseCloseWriterTest
    {
        private CompositionRootServices _bag;
        private CaseOpenWriter _openWriter;
        private CaseCloseWriter _closeWriter;
        private PatientId _patient;
        private const long SpawnTick = 0;

        [SetUp]
        public void Setup()
        {
            _bag = CompositionRoot.Assemble();
            _openWriter = new CaseOpenWriter(_bag.EventSink, _bag.Presence, _bag.Encoder, _bag.Stream.Events);
            _closeWriter = new CaseCloseWriter(_bag.EventSink, _bag.Encoder, _bag.Decoder, _bag.Stream.Events);
            // 病人:9 创建 + 组合层灌在场(「病人存在」前置的真源)
            _patient = _bag.PatientSpawner.SpawnNext(diseaseId: 0, tick: SpawnTick);
            _bag.Presence.AddPresent(_patient, new WorldPos(0, 0, 0));
        }

        private int CaseStreamCount()
            => _bag.Stream.Events.Count(e => StreamRouting.Of(e.Kind) == StreamId.Case);

        /// <summary>开一案并返回其 <c>case_id</c> 三元组(取流前缀查询,与生产调用方同路径)。</summary>
        private CaseId OpenCase(long openTick)
        {
            Assert.AreEqual(CaseOpenWriteResult.Opened,
                _openWriter.TryOpen(openTick, _patient, new DiseaseIdSet(1)), "夹具:立案须成功");
            var derived = CaseStreamQuery.OpenCaseOf(_bag.Stream.Events, _patient.Value);
            Assert.IsTrue(derived.HasValue, "夹具:流前缀查询须看得见新开案");
            return derived.Value;
        }

        /// <summary>
        /// 注入一条处置事件(病史流)。载荷不参与本件断言(写者只按 Kind/Patient/Tick 过滤)
        /// ⇒ 挂一池内合法引用即可;用编码器取得该引用,避免手搓伪引用(round-trip 路径不适用)。
        /// </summary>
        private void AppendTreatment(long tick, EventKind kind)
        {
            var refInPool = _bag.Encoder.Encode(EventKind.CaseOpened,
                new CaseOpenedPayload(_patient.Value, new DiseaseIdSet(0)));
            _bag.EventSink.Append(new SimEvent(tick, _patient, -1, kind, refInPool));
        }

        // ══════════ ① 真装配:恰增 1 条 + 全字段 round-trip ══════════

        [Test]
        public void test_caseClosed_realBag_appendsExactlyOneCaseEvent_roundTrip()
        {
            var caseId = OpenCase(5);
            AppendTreatment(6, EventKind.CareApplied); // 窗口内(5 ≤ 6 ≤ 10)
            var snapshot = new DiseaseIdSet(0b1011UL); // 结案快照(调用方给出)
            int beforeAll = _bag.Stream.Count;
            int beforeCase = CaseStreamCount();

            var result = _closeWriter.TryClose(10, caseId, snapshot, isChecked: true);

            // ── 结果码 + 恰增 1 条 ──
            Assert.AreEqual(CaseCloseWriteResult.Closed, result, "前置满足须真正写入");
            Assert.AreEqual(beforeAll + 1, _bag.Stream.Count, "真流总事件数恰增 1");
            Assert.AreEqual(beforeCase + 1, CaseStreamCount(), "病例流恰增 1 条");

            // ── 事件头 ──
            var evt = _bag.Stream.Events[_bag.Stream.Count - 1];
            Assert.AreEqual(EventKind.CaseClosed, evt.Kind, "Kind = CaseClosed");
            Assert.AreEqual(StreamId.Case, StreamRouting.Of(evt.Kind), "落病例流(ADR-008 §一)");
            Assert.AreEqual(_patient, evt.Patient,
                "事件头 Patient = 病例所属病人(非 None —— 排序键与高水位都依赖它)");
            Assert.AreEqual(10, evt.Tick, "结案 tick = 本事件自身 Tick");
            Assert.AreEqual(0, evt.Seq, "Seq 由发号器给出(该 (Tick, Patient) 首号 = 0)");

            // ── 载荷 round-trip(四字段;字节真进池)──
            Assert.IsTrue(
                PayloadCodec.TryGetPayload<CaseClosedPayload>(evt, _bag.BlobPool, out var decoded),
                "载荷须能经池取回(手搓伪引用会在此 false)");
            Assert.AreEqual(_patient.Value, decoded.PatientId, "payload.patient_id");
            Assert.AreEqual(caseId, decoded.CaseId, "payload.case_id = 被结案件的精确三元组");
            Assert.AreEqual(snapshot, decoded.DiseaseSet, "payload.disease_set = 结案快照(调用方给定)");

            // ⚠️ **原此处有 `Assert.IsTrue(decoded.Treated, …)`,已删(2026-10-15 · 003 评审 §6.2)**
            //    —— 该断言**结构性恒真**,判别力为零:`CaseCloseWriter.cs` 自陈「走到写入分支
            //    `hasTreatmentInWindow` 恒 true」(无窗口内处置 ⇒ `Decide` 返 `RejectNoTreatment`
            //    ⇒ 根本走不到写)。⇒ 它**无法区分**「写者自算」与「硬编码 `true`」,
            //    而硬编码 `true` 在此路径上**行为等价** ⇒ 无缺陷可捕。
            //    `treated` 字段的真实信息量为 0(写入路径上恒真),它是给下游读者(7a/53)的
            //    **快照字段**,不是判据。窗口逻辑的判别力载体 = `case_close_precondition_test.cs`
            //    (含「窗口外处置 ⇒ 拒结案」的负例)。
        }

        // ══════════ ①b AC-37-18 空病种集(纯伤情案)真装配 ══════════

        /// <summary>AC-37-18:纯伤情案(无病种)⇒ 结案快照 <c>disease_set</c> 为空集,结案合法且**逐位为空**。
        /// <para>⚠️ <b>2026-10-15 从 <c>case_close_precondition_test.cs</c> 迁入</b> —— 该处的同名两条
        /// 只喂字面量给 <c>CaseCloseDecider.Decide</c>(其形参**无** <c>disease_set</c> ⇒ 恒真),
        /// 且 <c>emptyDiseaseSet</c> **从未进入任何调用**。空集这一维度在**决策层结构性不可表达**,
        /// 只有**写者层**(真装配)能把空集真正喂进生产结案路径。</para>
        /// <para><b>可捕获的变异</b>:<c>TryClose</c> 加「空 disease_set 拒收」守卫 ⇒ 结果码红;
        /// codec 对空位集编码错(如写成非零)⇒ 快照回读红。</para></summary>
        [Test]
        public void test_ac3718_pureInjuryCase_emptyDiseaseSet_closesWithEmptySnapshot()
        {
            var caseId = OpenCase(5);
            AppendTreatment(6, EventKind.CareApplied); // 窗口内处置(须 ∈ TreatmentKinds 权威集,否则结案被拒)
            int beforeCase = CaseStreamCount();

            var result = _closeWriter.TryClose(10, caseId, new DiseaseIdSet(0), isChecked: true);

            Assert.AreEqual(CaseCloseWriteResult.Closed, result,
                "空 disease_set 是合法终态(纯伤情案)⇒ 结案须成功");
            Assert.AreEqual(beforeCase + 1, CaseStreamCount(), "病例流恰增 1 条");

            var evt = _bag.Stream.Events[_bag.Stream.Count - 1];
            Assert.IsTrue(
                PayloadCodec.TryGetPayload<CaseClosedPayload>(evt, _bag.BlobPool, out var decoded),
                "载荷须能经池取回");
            Assert.AreEqual(0UL, decoded.DiseaseSet.Bits,
                "结案快照 disease_set 逐位为空(纯伤情案无病种)");
        }

        // ══════════ ② 前置:幂等拒收(AC-37-03)══════════

        [Test]
        public void test_caseClosed_secondClose_sameCaseId_alreadyClosed_noWrite()
        {
            var caseId = OpenCase(5);
            AppendTreatment(6, EventKind.EmergencyTreatmentApplied);
            Assert.AreEqual(CaseCloseWriteResult.Closed,
                _closeWriter.TryClose(10, caseId, new DiseaseIdSet(1), isChecked: true), "首次须结案");
            int afterAll = _bag.Stream.Count;
            int afterCase = CaseStreamCount();

            var second = _closeWriter.TryClose(11, caseId, new DiseaseIdSet(1), isChecked: true);

            Assert.AreEqual(CaseCloseWriteResult.RejectAlreadyClosed, second,
                "同 caseId 的第二条 CaseClosed 不产生(GDD 规则六-4 幂等拒收 / AC-37-03)");
            Assert.AreEqual(afterAll, _bag.Stream.Count, "零写入");
            Assert.AreEqual(afterCase, CaseStreamCount(), "病例流零写入");
        }

        // ══════════ ② 前置:未开案 ══════════

        [Test]
        public void test_caseClosed_neverOpened_notOpen_noWrite()
        {
            var ghost = new CaseId(5, _patient.Value, 0); // 该三元组从无开案
            int before = _bag.Stream.Count;

            var result = _closeWriter.TryClose(10, ghost, new DiseaseIdSet(1), isChecked: true);

            Assert.AreEqual(CaseCloseWriteResult.RejectNotOpen, result, "未开案不得结(亦覆盖三元组不匹配)");
            Assert.AreEqual(before, _bag.Stream.Count, "零写入");
        }

        [Test]
        public void test_caseClosed_noneSentinelPatient_notOpen_noWrite()
        {
            var bad = new CaseId(5, PatientId.None.Value, 0); // 哨兵 / 负 id
            int before = _bag.Stream.Count;

            var result = _closeWriter.TryClose(10, bad, new DiseaseIdSet(1), isChecked: true);

            Assert.AreEqual(CaseCloseWriteResult.RejectNotOpen, result,
                "哨兵 / 负 id 不得结案(污染 case_id 与 max(patient_id));该形参下归 RejectNotOpen");
            Assert.AreEqual(before, _bag.Stream.Count, "零写入");
        }

        // ══════════ ② 前置:未勾选 / 窗口内无处置 ══════════

        [Test]
        public void test_caseClosed_notChecked_noWrite()
        {
            var caseId = OpenCase(5);
            AppendTreatment(6, EventKind.CareApplied);
            int before = _bag.Stream.Count;

            var result = _closeWriter.TryClose(10, caseId, new DiseaseIdSet(1), isChecked: false);

            Assert.AreEqual(CaseCloseWriteResult.RejectNotChecked, result,
                "未勾选「已处置」⇒ 拒(GDD 规则六-4 / AC-37-04)");
            Assert.AreEqual(before, _bag.Stream.Count, "零写入");
        }

        [Test]
        public void test_caseClosed_noTreatmentInWindow_noWrite()
        {
            var caseId = OpenCase(5); // 无任何处置事件
            int before = _bag.Stream.Count;

            var result = _closeWriter.TryClose(10, caseId, new DiseaseIdSet(1), isChecked: true);

            Assert.AreEqual(CaseCloseWriteResult.RejectNoTreatment, result,
                "窗口内无处置事件 ⇒ 拒(GDD F-37.2 / AC-37-21 复诊后门)");
            Assert.AreEqual(before, _bag.Stream.Count, "零写入");
        }

        // ══════════ ③ 窗口方向:闭区间 + 窗口外 ══════════

        [Test]
        public void test_caseClosed_treatmentOnOpenTick_inclusiveBoundary_closes()
        {
            var caseId = OpenCase(5);
            AppendTreatment(5, EventKind.CareApplied); // 恰在立案 tick(闭区间下界)

            var result = _closeWriter.TryClose(10, caseId, new DiseaseIdSet(1), isChecked: true);

            Assert.AreEqual(CaseCloseWriteResult.Closed, result,
                "窗口闭区间:立案 tick 当刻的处置计入(e.Tick == CaseOpened.Tick)");
        }

        [Test]
        public void test_caseClosed_treatmentOnCloseTick_inclusiveBoundary_closes()
        {
            var caseId = OpenCase(5);
            AppendTreatment(10, EventKind.CareApplied); // 恰在结案 tick(闭区间上界)

            var result = _closeWriter.TryClose(10, caseId, new DiseaseIdSet(1), isChecked: true);

            Assert.AreEqual(CaseCloseWriteResult.Closed, result,
                "窗口闭区间:结案 tick 当刻的处置计入(e.Tick == closeTick)");
        }

        [Test]
        public void test_caseClosed_treatmentBeforeOpen_treatedAsOutsideWindow_noWrite()
        {
            // 按 tick 升序铺流(EventStream 硬不变量:入流 tick 必须非降)。
            // 处置 tick 3 < 开案 tick 5 —— 合法的非降序,且恰是「窗口外」的有效负例。
            AppendTreatment(3, EventKind.CareApplied); // 立案之前 ⇒ 窗口下界外
            var caseId = OpenCase(5);
            int before = _bag.Stream.Count;

            var result = _closeWriter.TryClose(10, caseId, new DiseaseIdSet(1), isChecked: true);

            Assert.AreEqual(CaseCloseWriteResult.RejectNoTreatment, result,
                "窗口 [openedTick, closeTick] 外的处置不计(ADR-008 §四 2026-09-17 方向订正)");
            Assert.AreEqual(before, _bag.Stream.Count, "零写入");
        }

        // ══════════ ④ 复诊:新案独立于前案(S11-8 B-1 面)══════════

        [Test]
        public void test_caseClosed_revisitNewCase_closesIndependently_ofPriorClosedCase()
        {
            // 首案:开 + 结
            var first = OpenCase(5);
            AppendTreatment(6, EventKind.CareApplied);
            Assert.AreEqual(CaseCloseWriteResult.Closed,
                _closeWriter.TryClose(10, first, new DiseaseIdSet(1), isChecked: true), "首案结");

            // 复诊新案(GDD 规则三;EC-37-1 复诊 = 新病例)
            var second = OpenCase(20);
            Assert.AreNotEqual(first, second, "复诊新案 = 新 case_id");
            AppendTreatment(21, EventKind.CareApplied);

            var result = _closeWriter.TryClose(30, second, new DiseaseIdSet(2), isChecked: true);

            Assert.AreEqual(CaseCloseWriteResult.Closed, result,
                "新案独立于前案 —— 前案已结不得使新案被判「已结」(S11-8 B-1 修复面)");
        }

        // ══════════ 注入面 / 入参 fail-loud ══════════

        [Test]
        public void test_caseClosed_negativeTick_throws()
        {
            var caseId = OpenCase(5);
            Assert.Throws<ArgumentOutOfRangeException>(
                () => _closeWriter.TryClose(-1, caseId, new DiseaseIdSet(1), isChecked: true),
                "tick 入参非法 = 编程错误,fail-loud(与 CaseOpenWriter 同口径)");
        }

        [Test]
        public void test_caseCloseWriter_nullDependency_throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => new CaseCloseWriter(null, _bag.Encoder, _bag.Decoder, _bag.Stream.Events));
            Assert.Throws<ArgumentNullException>(
                () => new CaseCloseWriter(_bag.EventSink, null, _bag.Decoder, _bag.Stream.Events));
            Assert.Throws<ArgumentNullException>(
                () => new CaseCloseWriter(_bag.EventSink, _bag.Encoder, null, _bag.Stream.Events));
        }
    }
}
