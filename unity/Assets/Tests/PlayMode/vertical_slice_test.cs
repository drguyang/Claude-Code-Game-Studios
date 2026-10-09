// 垂直切片测试 — 验证核心循环: 病人出现 → 诊断 → 治疗
//
// 目标: 1 病人 + 1 诊断 + 1 治疗, 无美术, 验证核心循环
//
// 核心循环:
//   1. 病人出现 (DiseaseOnset → 病史流) — 驱动 PatientSpawner(9 的写者, ADR-030)
//   2. 诊断 (CaseOpened → 病例流) — 37 写者未实现, 用 CaseOpenDecider 驱动
//   3. 治疗 (DrugTreatmentApplied → 病史流) — 11 PrescribeFlow 驱动
//   4. 病人状态更新 (VitalsDto via IVitalsQuery)
//
// 测试模式: 纯 C# 对象 + FakeEventSink + 真生产路径
// 参考: host_authority_test.cs 的 FakeEventSink 模式

using System.Collections;
using System.Collections.Generic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.DiseaseSimulation;
using DaYiJingCheng.Sim.Prescription;
using DaYiJingCheng.Sim.Codec;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace DaYiJingCheng.Tests.PlayMode
{
    /// <summary>
    /// 垂直切片测试 — 验证核心游戏循环。
    /// 驱动真生产路径: PrescribeFlow (11) + CaseOpenDecider (37)。
    /// </summary>
    public class VerticalSliceTest
    {
        // ══════════ 夹具 ══════════

        private EventStream _stream;
        private FakeVitalsQuery _vitals;
        private FakeIdAuthority _idAuth;
        private FakePresenceQuery _presence;
        private FakeEventSink _sink;
        private PayloadEncoder _encoder;
        private PrescribePorts _prescribePorts;

        [SetUp]
        public void SetUp()
        {
            _idAuth = new FakeIdAuthority();
            _presence = new FakePresenceQuery();
            _stream = new EventStream(_idAuth, _presence);
            _sink = new FakeEventSink();
            _vitals = new FakeVitalsQuery();
            var pool = new InMemoryBlobPool();
            _encoder = new PayloadEncoder(pool);
            _prescribePorts = new PrescribePorts(
                new FakeConversion(), new FakeStore(), _presence, new FakeSkills(), doseBase: 100);
        }

        private sealed class FakeConversion : IPortionsConversion
        {
            public int PortionsPerDose(ItemKey itemKey) => 2;
        }

        private sealed class FakeStore : IPortionsStore
        {
            public bool HasPortions(int playerId, ItemKey itemKey, int portions) => true;
            public int PeekLowestQuality(int playerId, ItemKey itemKey, int portions) => 1;
            public bool ConsumePortions(int playerId, ItemKey itemKey, int portions) => true;
        }

        private sealed class FakeSkills : ISkillGrowthPort
        {
            public int QueryLevel(int actorId, int skillId) => 5;
            public void EmitGrowth(int actorId, int skillId, int objectId, int noveltyHint,
                Fix kDifficulty, long tick, PatientId patientId) { }
        }

        // ══════════ 核心循环验证 ══════════

        /// <summary>
        /// 验证病人出现事件写入病史流 — 驱动 PatientSpawner(9 的 DiseaseOnset 写者)真生产路径。
        /// ADR-030 §③:写者 = 9 · §④:落病史流。
        /// ⚠️ 用**真 EventStream**(非 FakeEventSink)以覆盖路由 / Seq 发号 / 有界性全链路。
        /// ⚠️ 登记缺口:病人创建后「进入在场视图」无写面(`IPresenceQuery` 只有读面),
        /// 测试侧以 FakePresenceQuery.AddPresent 代偿;该写面归属待裁(9 实现轮)。
        /// </summary>
        [Test]
        public void test_diseaseOnset_writesToHistoryStream()
        {
            // Arrange: 9 的写者,写向真 EventStream
            var spawner = new PatientSpawner(_idAuth, _stream, _encoder, worldSeed: 12345UL);

            // Act: 驱动真生产路径
            var patientId = spawner.SpawnNext(diseaseId: 1, tick: 0);

            // Assert: 事件确实写入病史流
            Assert.AreEqual(1, _stream.Count, "DiseaseOnset 应写入病史流");
            var e = _stream.Events[0];
            Assert.AreEqual(EventKind.DiseaseOnset, e.Kind);
            Assert.AreEqual(patientId, e.Patient, "事件的 Patient 应为新建病人");
            Assert.AreEqual(0L, e.Tick);
            Assert.AreEqual(StreamId.History, StreamRouting.Of(e.Kind),
                "DiseaseOnset 应路由到病史流");
            // 评审 A2: 补 header Seq 发号断言(首个事件 Seq = 0)。
            // 真 EventStream 在 Append 时发号(EventStream.cs:90-93),此断言覆盖 DiseaseOnset 路径。
            Assert.AreEqual(0L, e.Seq, "首个事件 header Seq 应由 EventStream 发号为 0");
        }

        /// <summary>
        /// 验证诊断记录事件写入病例流 — 驱动 CaseOpenDecider 真生产路径。
        /// </summary>
        [Test]
        public void test_caseOpened_writesToCaseStream()
        {
            // Arrange
            var patient = new PatientId(0);
            _presence.AddPresent(patient);

            // Act: 驱动 CaseOpenDecider 真生产路径
            var decision = CaseOpenDecider.Decide(
                hasOpenCase: false,
                hasPatient: _presence.IsPresent(patient),
                sourceKind: CaseOpenSource.B);

            // Assert: 决策 = OpenNew
            Assert.AreEqual(CaseOpenDecision.OpenNew, decision, "无开案 + 有病人 ⇒ 应开新案");

            // Act: 据决策写入事件
            if (decision == CaseOpenDecision.OpenNew)
            {
                var payload = new CaseOpenedPayload(patient.Value, default);
                var e = new SimEvent(0, patient, 0, EventKind.CaseOpened,
                    _encoder.Encode(EventKind.CaseOpened, payload));
                _sink.Append(e);
            }

            // Assert
            Assert.AreEqual(1, _sink.AppendedEvents.Count, "诊断记录事件应写入病例流");
            Assert.AreEqual(EventKind.CaseOpened, _sink.AppendedEvents[0].Kind);
            Assert.AreEqual(patient, _sink.AppendedEvents[0].Patient);
        }

        /// <summary>
        /// 验证治疗应用事件写入病史流 — 驱动 PrescribeFlow 真生产路径。
        /// </summary>
        [Test]
        public void test_drugTreatmentApplied_writesToHistoryStream()
        {
            // Arrange
            var patient = new PatientId(0);
            _presence.AddPresent(patient);

            var req = new PrescribeRequest(
                itemKey: new ItemKey("willow_bark", ProcessingState.Raw),
                entry: new PrescriptionEntry(10, PrescriptionPolarity.Symptomatic, 0),
                actorId: 1,
                patientId: patient,
                selectedDose: 1,
                profile: new DrugProfile { DoseRange = new DoseRange(1, 3), DrugPotency = Fix.FromRational(1, 2), HalfLife = Fix.FromRational(1, 1), AxisOffsetByQuality = new[] { Fix.Zero, Fix.Zero, Fix.Zero, Fix.Zero, Fix.Zero } },
                selectedQuality: 1,
                isHost: true);

            // Act: 驱动 PrescribeFlow 真生产路径
            var outcome = PrescribeFlow.Prescribe(req, tick: 0, _prescribePorts, _sink, _encoder);

            // Assert
            Assert.IsTrue(outcome.Applied, "处方应成功");
            Assert.AreEqual(1, _sink.AppendedEvents.Count, "治疗应用事件应写入病史流");
            Assert.AreEqual(EventKind.DrugTreatmentApplied, _sink.AppendedEvents[0].Kind);
            Assert.AreEqual(patient, _sink.AppendedEvents[0].Patient);
        }

        /// <summary>
        /// 验证治疗后病人状态可查。
        /// </summary>
        [Test]
        public void test_vitalsQuery_returnsAfterTreatment()
        {
            // Arrange
            var patient = new PatientId(0);
            _vitals.SetVitals(patient, new VitalsDto(0.5f, 0.1f, SignChannel.Breathing | SignChannel.Palpation, 2));

            // Act
            var v = _vitals.GetVitals(patient);

            // Assert
            Assert.AreEqual(0.5f, v.Position, "治疗后体征位置应可查");
            Assert.AreEqual(0.1f, v.Trend, "治疗后体征趋势应可查");
            Assert.AreEqual(SignChannel.Breathing | SignChannel.Palpation, v.SignChannelMask);
            Assert.AreEqual(2, v.SignCount);
        }

        // ══════════ 集成验证 ══════════

        /// <summary>
        /// 验证完整核心循环: 病人出现 → 诊断 → 治疗 → 状态更新。
        /// 病人出现腿驱动 PatientSpawner(9 的 DiseaseOnset 写者,ADR-030)。
        /// </summary>
        [UnityTest]
        public IEnumerator test_fullCoreLoop_patientToTreatment()
        {
            // Act: 病人出现 (9 的 DiseaseOnset 写者驱动)
            var spawner = new PatientSpawner(_idAuth, _sink, _encoder, worldSeed: 12345UL);
            var patient = spawner.SpawnNext(diseaseId: 1, tick: 0);
            _presence.AddPresent(patient);

            // Act: 诊断 (CaseOpenDecider 驱动)
            var decision = CaseOpenDecider.Decide(false, true, CaseOpenSource.B);
            Assert.AreEqual(CaseOpenDecision.OpenNew, decision);
            var casePayload = new CaseOpenedPayload(patient.Value, default);
            _sink.Append(new SimEvent(0, patient, 0, EventKind.CaseOpened,
                _encoder.Encode(EventKind.CaseOpened, casePayload)));

            // Act: 治疗 (PrescribeFlow 驱动)
            var req = new PrescribeRequest(
                new ItemKey("willow_bark", ProcessingState.Raw),
                new PrescriptionEntry(10, PrescriptionPolarity.Symptomatic, 0),
                1, patient, 1,
                new DrugProfile { DoseRange = new DoseRange(1, 3), DrugPotency = Fix.FromRational(1, 2), HalfLife = Fix.FromRational(1, 1), AxisOffsetByQuality = new[] { Fix.Zero, Fix.Zero, Fix.Zero, Fix.Zero, Fix.Zero } },
                1, true);
            var outcome = PrescribeFlow.Prescribe(req, 1, _prescribePorts, _sink, _encoder);

            // Act: 状态更新
            _vitals.SetVitals(patient, new VitalsDto(0.8f, -0.2f, SignChannel.Breathing, 1));

            // Assert: 三条事件全部写入(病人出现 → 诊断 → 治疗)
            Assert.AreEqual(3, _sink.AppendedEvents.Count, "完整核心循环应产生三条事件");
            Assert.AreEqual(EventKind.DiseaseOnset, _sink.AppendedEvents[0].Kind,
                "首条应为病人出现(9 的 DiseaseOnset)");
            Assert.AreEqual(EventKind.CaseOpened, _sink.AppendedEvents[1].Kind);
            Assert.AreEqual(EventKind.DrugTreatmentApplied, _sink.AppendedEvents[2].Kind);
            Assert.IsTrue(outcome.Applied, "处方应成功");

            // Assert: 状态可查
            var v = _vitals.GetVitals(patient);
            Assert.AreEqual(0.8f, v.Position, "治疗后体征位置应更新");
            Assert.AreEqual(-0.2f, v.Trend, "治疗后体征趋势应更新");

            yield return null;
        }

        // ══════════ 边界情况 ══════════

        /// <summary>
        /// 验证无病人时诊断失败 — 驱动 CaseOpenDecider 真生产路径。
        /// </summary>
        [Test]
        public void test_caseOpened_withoutPatient_fails()
        {
            // Arrange: 不添加任何在场病人
            var ghostPatient = new PatientId(99);

            // Act: 驱动 CaseOpenDecider 真生产路径
            var decision = CaseOpenDecider.Decide(
                hasOpenCase: false,
                hasPatient: _presence.IsPresent(ghostPatient),
                sourceKind: CaseOpenSource.B);

            // Assert: 决策 = NoCase
            Assert.AreEqual(CaseOpenDecision.NoCase, decision, "无病人 ⇒ 不立案");
        }

        /// <summary>
        /// 验证无诊断时治疗失败 — 驱动 PrescribeFlow 真生产路径。
        /// </summary>
        [Test]
        public void test_treatmentWithoutDiagnosis_fails()
        {
            // Arrange: 病人出现但不诊断
            var patient = new PatientId(0);
            _presence.AddPresent(patient);

            // Act: 直接治疗（无前置诊断）— PrescribeFlow 不检查诊断前置
            var req = new PrescribeRequest(
                new ItemKey("willow_bark", ProcessingState.Raw),
                new PrescriptionEntry(10, PrescriptionPolarity.Symptomatic, 0),
                1, patient, 1,
                new DrugProfile { DoseRange = new DoseRange(1, 3), DrugPotency = Fix.FromRational(1, 2), HalfLife = Fix.FromRational(1, 1), AxisOffsetByQuality = new[] { Fix.Zero, Fix.Zero, Fix.Zero, Fix.Zero, Fix.Zero } },
                1, true);
            var outcome = PrescribeFlow.Prescribe(req, 0, _prescribePorts, _sink, _encoder);

            // Assert: PrescribeFlow 不检查诊断前置，治疗仍成功
            // 真前置检查归 CaseCloseDecider（未实现）
            Assert.IsTrue(outcome.Applied, "PrescribeFlow 不检查诊断前置");
            Assert.AreEqual(1, _sink.AppendedEvents.Count, "治疗事件仍写入");
        }

        // ══════════ 夹具类 ══════════

        private sealed class FakeEventSink : IEventSink
        {
            public readonly List<SimEvent> AppendedEvents = new List<SimEvent>();
            public void Append(in SimEvent e) => AppendedEvents.Add(e);
        }

        private sealed class FakeIdAuthority : IIdAuthority
        {
            public PatientId NextPatientId() => new PatientId(1);
            public ItemInstanceId NextItemInstanceId() => new ItemInstanceId(1);
        }

        private sealed class FakePresenceQuery : IPresenceQuery
        {
            private readonly HashSet<int> _present = new HashSet<int>();

            public bool IsPresent(PatientId patientId) => _present.Contains(patientId.Value);
            public int PresentCount => _present.Count;
            public bool IsPresentAt(WorldPos cell) => false;

            public void AddPresent(PatientId patientId) => _present.Add(patientId.Value);
        }

        private sealed class FakeVitalsQuery : IVitalsQuery
        {
            private readonly Dictionary<int, VitalsDto> _data = new Dictionary<int, VitalsDto>();

            public VitalsDto GetVitals(PatientId p)
            {
                if (_data.TryGetValue(p.Value, out var v)) return v;
                return default;
            }

            public void SetVitals(PatientId p, VitalsDto v) => _data[p.Value] = v;
        }

    }
}
