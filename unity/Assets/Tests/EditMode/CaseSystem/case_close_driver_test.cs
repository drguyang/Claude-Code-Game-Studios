// 「结案键」→ `CaseCloseWriter.TryClose` 的装配层驱动判据(EditMode,真装配,禁 Fake)
// —— T19 甲(2026-11-10)
//
// 权威:`design/gdd/case-system.md` 规则六(结案唯一出口 · 已处置前置 · 单例 · 幂等拒收)·
//       ADR-008 §四(处置证据窗口化 + 快照)· ADR-030(`DiseaseOnset` 是病种的唯一载体)·
//       ADR-006 Amendment A(`CaseId` 三元组从事件头读)· ADR-043 §D6(解锁去重)
//
// ─────────────────────────────────────────────────────────────────────────────
// 本件要证明的六件事(= 用户 T19 选的那条:让技法 2 / 3 / 10 真的能解锁、病例能收口)
// ─────────────────────────────────────────────────────────────────────────────
//   ① **按键 → 真的结案**:OpenCaseOf 取到的那一例被结掉,病例流恰增 1 条;
//   ② **快照来自流的事实**:`disease_set` = 该病人的全部 `DiseaseOnset`(位序 `DiseaseId − 1`),
//      不是调用方转述,也不是开案快照(后者生产上恒空集);
//   ③ **`isChecked = true` 不跳过证据那半边**:窗口内无处置 ⇒ 照拒 `RejectNoTreatment`;
//   ④ **没有开着的病例 ⇒ 按下去什么也不发生**(零写入),哨兵病人同理;
//   ⑤ **结案后再按 ⇒ 零写入**(不会写第二条 `CaseClosed`);
//   ⑥ **端到端**:一条真结案经**生产规则表** `TechniqueUnlockRules.Default` 解锁技法 2。
//
// ─────────────────────────────────────────────────────────────────────────────
// 读哪些仓库文件(测试标准 §只读外部夹具的具名声明义务)
// ─────────────────────────────────────────────────────────────────────────────
// 经 `CompositionRoot.Assemble()` 装配生产袋 —— 它会按 `assets/data/*.cooked` 装载数据表。
//   为什么必须读真表:本件测的是**生产装配形状**(真 `EventStream` / 真编码器 / 真在场登记),
//   手搓一个假袋只能证明夹具自己的谓词(假绿形态 F1 / F2)。
// ⚠️ **本文件零写盘**:所有事件写进内存 `EventStream` / `InMemoryBlobPool`。

using System;
using System.Collections.Generic;
using System.Linq;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.CaseSystem
{
    /// <summary><see cref="CaseCloseDriver"/> —— 「结案键」到写者的那一段。</summary>
    [TestFixture]
    public sealed class CaseCloseDriverTest
    {
        private const int PatientDiseaseId = 11;          // 任意合法病种(只验位集透传)
        private const int KnownTechnique = 2;             // 生产规则表里 INJ_STAB 解锁的那一条

        private CompositionRootServices _bag;
        private CaseOpenWriter _openWriter;
        private CaseCloseWriter _closeWriter;
        private CaseCloseDriver _driver;
        private PatientId _patient;
        private long _tick;

        [SetUp]
        public void Setup()
        {
            _bag = CompositionRoot.Assemble();
            _openWriter = new CaseOpenWriter(_bag.EventSink, _bag.Presence, _bag.Encoder, _bag.Stream.Events);
            _closeWriter = new CaseCloseWriter(_bag.EventSink, _bag.Encoder, _bag.Decoder, _bag.Stream.Events);
            _tick = 0;
            _driver = new CaseCloseDriver(_closeWriter, () => _bag.Stream.Events, _bag.Decoder, () => _tick);

            // 病人:9 创建(写一条 DiseaseOnset)+ 组合层灌在场(立案前置的真源)
            _patient = _bag.PatientSpawner.SpawnNext(diseaseId: PatientDiseaseId, tick: 0);
            _bag.Presence.AddPresent(_patient, new WorldPos(0, 0, 0));
        }

        private int CaseStreamCount()
            => _bag.Stream.Events.Count(e => StreamRouting.Of(e.Kind) == StreamId.Case);

        /// <summary>开一案并返回其 <c>case_id</c>(与生产同路径:流前缀查询)。</summary>
        private CaseId OpenCase(long openTick)
        {
            Assert.AreEqual(CaseOpenWriteResult.Opened,
                _openWriter.TryOpen(openTick, _patient, new DiseaseIdSet(1)),
                "夹具:立案须成功");
            CaseId? derived = CaseStreamQuery.OpenCaseOf(_bag.Stream.Events, _patient.Value);
            Assert.IsTrue(derived.HasValue, "夹具:流前缀查询须看得见新开案");
            return derived.Value;
        }

        /// <summary>注入一条处置事件(病史流)。写者只按 Kind / Patient / Tick 过滤,不解载荷。</summary>
        private void AppendTreatment(long tick)
        {
            PayloadRef refInPool = _bag.Encoder.Encode(
                EventKind.CareApplied, new CareAppliedPayload(tick, actionId: 0, caregiver: 7, phase: 0));
            _bag.EventSink.Append(new SimEvent(tick, _patient, -1, EventKind.CareApplied, refInPool));
        }

        /// <summary>给**同一病人**补一条 <c>DiseaseOnset</c>(病人出现之外的后续病种)。</summary>
        private void AppendOnset(long tick, int diseaseId)
        {
            var payload = new DiseaseOnsetPayload(
                onsetTick: tick, diseaseId: diseaseId, patientId: _patient.Value,
                patientSeed: 0L, seq: 0);
            _bag.EventSink.Append(new SimEvent(
                tick, _patient, -1, EventKind.DiseaseOnset,
                _bag.Encoder.Encode(EventKind.DiseaseOnset, payload)));
        }

        private CaseClosedPayload LastClosed()
        {
            SimEvent evt = _bag.Stream.Events.Last(e => e.Kind == EventKind.CaseClosed);
            Assert.IsTrue(PayloadCodec.TryGetPayload<CaseClosedPayload>(evt, _bag.BlobPool, out var p),
                "载荷须能经池取回(手搓伪引用会在此 false)");
            return p;
        }

        // ══════════ ① + ② 结案成功:快照 = 流里的病种 ══════════

        [Test]
        public void test_tryClose_treatedCase_closesWithDiseaseSetFromOnsets()
        {
            CaseId caseId = OpenCase(5);
            AppendTreatment(6);                 // 窗口内(5 ≤ 6 ≤ 10)
            AppendOnset(7, diseaseId: 3);       // 第二个病种(仍在窗口内,但快照不看窗口)
            int beforeCase = CaseStreamCount();
            _tick = 10;

            CaseCloseWriteResult result = _driver.TryClose(_patient);

            Assert.AreEqual(CaseCloseWriteResult.Closed, result, "前置满足 ⇒ 真写入");
            Assert.AreEqual(beforeCase + 1, CaseStreamCount(), "病例流恰增 1 条");

            CaseClosedPayload payload = LastClosed();
            Assert.AreEqual(_patient.Value, payload.PatientId, "payload.patient_id");
            Assert.AreEqual(caseId, payload.CaseId, "payload.case_id = 开着的**那一例**的三元组");
            Assert.IsTrue(payload.Treated, "treated 由写者按真流自算(窗口内确有处置)");

            // 位序契约:bit index = DiseaseId − 1(DiseaseIdSet)
            ulong expected = (1UL << (PatientDiseaseId - 1)) | (1UL << (3 - 1));
            Assert.AreEqual(expected, payload.DiseaseSet.Bits,
                "结案快照 = 该病人全部 DiseaseOnset 的位集(不是调用方转述,也不是开案快照)");
        }

        // ══════════ ③ isChecked 只提供「自述」那一半 ══════════

        [Test]
        public void test_tryClose_treatmentMissing_rejectsNoTreatment_evenWhenChecked()
        {
            OpenCase(5);                        // 开案,但窗口内没有任何处置
            int before = _bag.Stream.Count;
            _tick = 10;

            CaseCloseWriteResult result = _driver.TryClose(_patient);

            Assert.AreEqual(CaseCloseWriteResult.RejectNoTreatment, result,
                "驱动传的 isChecked = true 只是玩家的自述;证据那半边(流里的处置)由写者查");
            Assert.AreEqual(before, _bag.Stream.Count, "零写入");
        }

        // ══════════ ④ 没有开着的病例 / 哨兵病人 ══════════

        [Test]
        public void test_tryClose_noOpenCase_rejectsNotOpen_noWrite()
        {
            int before = _bag.Stream.Count;
            _tick = 10;

            CaseCloseWriteResult result = _driver.TryClose(_patient);

            Assert.AreEqual(CaseCloseWriteResult.RejectNotOpen, result, "从没立案 ⇒ 按下去什么也不发生");
            Assert.AreEqual(before, _bag.Stream.Count, "零写入");
        }

        [Test]
        public void test_tryClose_noneSentinel_rejectsNotOpen_noWrite()
        {
            OpenCase(5);                        // 有病人在案,但按键路由给了哨兵
            int before = _bag.Stream.Count;
            _tick = 10;

            CaseCloseWriteResult result = _driver.TryClose(PatientId.None);

            Assert.AreEqual(CaseCloseWriteResult.RejectNotOpen, result,
                "PatientId.None(-1)会污染 case_id 与 max(patient_id) ⇒ 直接拒");
            Assert.AreEqual(before, _bag.Stream.Count, "零写入");
        }

        // ══════════ ⑤ 结案后再按:零写入 ══════════

        [Test]
        public void test_tryClose_secondPressAfterClose_writesNothing()
        {
            OpenCase(5);
            AppendTreatment(6);
            _tick = 10;
            Assert.AreEqual(CaseCloseWriteResult.Closed, _driver.TryClose(_patient), "首次须结案");
            int afterFirst = _bag.Stream.Count;

            _tick = 11;
            CaseCloseWriteResult second = _driver.TryClose(_patient);

            // ⚠️ 归因:驱动先按 `OpenCaseOf` 短路 ⇒ 走到的是 RejectNotOpen,不是写者的
            //    RejectAlreadyClosed。两者对玩家是同一件事(按下去没反应),但**码不同** ——
            //    本断言钉住的是驱动这一层的行为(写者层的幂等拒收由 case_close_writer_test 覆盖)。
            Assert.AreEqual(CaseCloseWriteResult.RejectNotOpen, second,
                "已无开着的一例 ⇒ 驱动在到达写者前就拒");
            Assert.AreEqual(afterFirst, _bag.Stream.Count, "不得写出第二条 CaseClosed");
        }

        // ══════════ ②′ DiseaseSetOf 本身(位序 / 越界 / 跨病人)══════════

        [Test]
        public void test_diseaseSetOf_multipleOnsets_unionOfIds()
        {
            AppendOnset(1, 3);
            AppendOnset(2, 64);                 // 边界:id 64 ⇒ bit 63

            DiseaseIdSet set = CaseStreamQuery.DiseaseSetOf(_bag.Stream.Events, _bag.Decoder, _patient);

            ulong expected = (1UL << (PatientDiseaseId - 1)) | (1UL << 2) | (1UL << 63);
            Assert.AreEqual(expected, set.Bits, "并集,且 id 64 落在 bit 63(位序 = DiseaseId − 1)");
        }

        [Test]
        public void test_diseaseSetOf_ignoresOtherPatients()
        {
            PatientId other = _bag.PatientSpawner.SpawnNext(diseaseId: 5, tick: 1);
            Assert.AreNotEqual(_patient, other, "夹具:须是两个不同的病人");

            DiseaseIdSet set = CaseStreamQuery.DiseaseSetOf(_bag.Stream.Events, _bag.Decoder, _patient);

            Assert.AreEqual(1UL << (PatientDiseaseId - 1), set.Bits,
                "另一个病人的病种不得混进来(否则结案快照会把别人的病算在他头上)");
        }

        [Test]
        public void test_diseaseSetOf_outOfRangeIds_areSkipped()
        {
            AppendOnset(1, diseaseId: 0);       // bit = -1(表达不了)
            AppendOnset(2, diseaseId: 65);      // bit = 64(ulong 表达不了)

            DiseaseIdSet set = CaseStreamQuery.DiseaseSetOf(_bag.Stream.Events, _bag.Decoder, _patient);

            Assert.AreEqual(1UL << (PatientDiseaseId - 1), set.Bits,
                "越界 id 跳过 —— 塞进相邻位等于凭空发明一个病种");
        }

        [Test]
        public void test_diseaseSetOf_noOnset_isEmpty()
        {
            // 未经 9 创建的病人 id(流里没有它的任何 onset)
            DiseaseIdSet set = CaseStreamQuery.DiseaseSetOf(_bag.Stream.Events, _bag.Decoder, new PatientId(999));

            Assert.AreEqual(0UL, set.Bits, "无 onset ⇒ 空集(纯伤情案的形态)");
        }

        // ══════════ ⑥ 端到端:真结案 ⇒ 生产规则表解锁技法 ══════════

        [Test]
        public void test_caseClosedUnlocksTechnique_endToEndWithProductionRuleTable()
        {
            // 生产规则表里 `ForCase(diseaseId: 2 → techniqueId: 2)` 是 INJ_STAB(刀伤)那一条。
            // 用二号病种的病人走完整条:出现 → 立案 → 处置 → **按键结案** → 扫流解锁。
            PatientId stabPatient = _bag.PatientSpawner.SpawnNext(diseaseId: 2, tick: 20);
            _bag.Presence.AddPresent(stabPatient, new WorldPos(1, 0, 1));
            Assert.AreEqual(CaseOpenWriteResult.Opened,
                _openWriter.TryOpen(25, stabPatient, new DiseaseIdSet(1UL << 1)), "夹具:立案");

            PayloadRef care = _bag.Encoder.Encode(
                EventKind.CareApplied, new CareAppliedPayload(26, actionId: 0, caregiver: 7, phase: 0));
            _bag.EventSink.Append(new SimEvent(26, stabPatient, -1, EventKind.CareApplied, care));

            _tick = 30;
            Assert.AreEqual(CaseCloseWriteResult.Closed, _driver.TryClose(stabPatient), "夹具:结案");

            var writer = new TechniqueUnlockWriter(
                _bag.Stream, _bag.Encoder,
                new TechniqueUnlockProjection(() => _bag.Stream.Events, _bag.Decoder),
                new HashSet<int> { KnownTechnique });
            var unlocker = new TechniqueUnlockDriver(
                _bag.Stream, _bag.Decoder, TechniqueUnlockRules.Default, writer, actorId: 7);

            int written = unlocker.OnTickEdge(tick: 30);

            Assert.AreEqual(1, written, "刀伤结案 ⇒ 生产规则表里恰一条命中(技法 2 清创缝合)");
            SimEvent unlock = _bag.Stream.Events.Last(e => e.Kind == EventKind.TechniqueUnlocked);
            var payload = _bag.Decoder.Decode<TechniqueUnlockedPayload>(unlock.Kind, unlock.Payload);
            Assert.AreEqual(7, payload.ActorId, "解锁跟人走");
            Assert.AreEqual(KnownTechnique, payload.TechniqueId);
            Assert.AreEqual(2, payload.SourceId, "sourceId = disease_id(一条事实一个来源)");
        }

        // ══════════ 构造门 ══════════

        [Test]
        public void test_nullDependency_throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => new CaseCloseDriver(null, () => _bag.Stream.Events, _bag.Decoder, () => 0));
            Assert.Throws<ArgumentNullException>(
                () => new CaseCloseDriver(_closeWriter, null, _bag.Decoder, () => 0));
            Assert.Throws<ArgumentNullException>(
                () => new CaseCloseDriver(_closeWriter, () => _bag.Stream.Events, null, () => 0));
            Assert.Throws<ArgumentNullException>(
                () => new CaseCloseDriver(_closeWriter, () => _bag.Stream.Events, _bag.Decoder, null));
        }
    }
}
