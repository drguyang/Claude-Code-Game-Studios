// Checkpoint → Load → Restore 全链往返 —— TD-persist-03 的真重放端到端钉。
//
// 权威来源:
//   docs/tech-debt-register.md TD-persist-03 —— 债描述「列表 → sim 状态」段的兑现判据
//   AC-7a-01(persistence-service.md:597)—— 重放后「id 高水位 == 直接重放所得」的形状
//   ADR-006 Amendment B —— 折叠行必留 patient_id,高水位重构依赖
//
// 与单测的分野:单测钉各部件(restore 三关 / codec 布局 / 组版段),
// 本文件只钉**全链**:存档字节 → 读档解出 → Restore 进 sim → 状态可比。
//
// 写盘边界:全部落 `Path.GetTempPath()`(FsWriteBoundaryGate 允许形态;零仓库锚点)。

using System;
using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Tests.DiseaseSimulation;   // FakeIdAuthority / FakePresenceQuery
using NUnit.Framework;

namespace DaYiJingCheng.Tests.PersistenceService
{
    public class SaveReplayRoundtripTest
    {
        private const int PatientA = 7;
        private const int DiseaseSelfLimit = 21;   // 非 plateau、非 lethal ⇒ 可折
        private const long Seed = 0x5EEDL;

        private string _testDir;
        private InMemoryBlobPool _pool;
        private PayloadEncoder _encoder;
        private PayloadDecoder _decoder;
        private Dictionary<int, DiseaseRegistryEntry> _registry;

        [SetUp]
        public void SetUp()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "dyjq_td_persist_03_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDir);
            _pool = new InMemoryBlobPool();
            _encoder = new PayloadEncoder(_pool);
            _decoder = new PayloadDecoder(_pool);
            _registry = new Dictionary<int, DiseaseRegistryEntry>
            {
                [DiseaseSelfLimit] = new DiseaseRegistryEntry
                {
                    DiseaseId = DiseaseSelfLimit,
                    DiseaseKey = "D" + DiseaseSelfLimit,
                    Kind = DiseaseKind.Disease,
                    Lethality = 1,
                    Curve = new NaturalProgressCurve { Plateau = false },
                },
            };
        }

        [TearDown]
        public void Teardown()
        {
            if (Directory.Exists(_testDir))
            {
                try { Directory.Delete(_testDir, true); } catch { }
            }
        }

        private SimEvent Onset(long tick, int patient, int disease)
            => new SimEvent(tick, new PatientId(patient), 0L, EventKind.DiseaseOnset,
                _encoder.Encode(EventKind.DiseaseOnset,
                    new DiseaseOnsetPayload(tick, disease, patient, Seed, 0L)));

        private SimEvent Outcome(long tick, int patient, int disease, PatientOutcome outcome)
            => new SimEvent(tick, new PatientId(patient), 0L, EventKind.PatientOutcomeChanged,
                _encoder.Encode(EventKind.PatientOutcomeChanged,
                    new PatientOutcomeChangedPayload(patient, disease, (int)outcome, 0L)));

        private FoldedPredicate PredicateOf(EventStream stream) => new FoldedPredicate(
            historyStream: () => SaveComposer.FilterByStream(stream.Events, StreamId.History),
            caseStream: () => SaveComposer.FilterByStream(stream.Events, StreamId.Case),
            diseaseLookup: id => _registry.TryGetValue(id, out var e) ? e : null,
            decoder: _decoder);

        [Test]
        public void test_replay_roundtrip_restoredRowsAndHighWater_matchPreSaveState()
        {
            // ══ Arrange:可折病人(已终态)进流 ⇒ Checkpoint(折叠发生,行落盘)══
            var stream = new EventStream(new FakeIdAuthority(), new FakePresenceQuery());
            stream.Append(Onset(10L, PatientA, DiseaseSelfLimit));
            stream.Append(Outcome(20L, PatientA, DiseaseSelfLimit, PatientOutcome.Died));

            var svc = new SaveService(
                _testDir, new SaveCodec(),
                folded: PredicateOf(stream),
                narrativeContext: FuncNarrativeContextQuery.AlwaysIn,
                replaySink: _ => { },
                foldedRowSink: _ => { },
                // 🔁 2026-10-25 AC-7a-07:`compose` 契约 = **返回未签名**,校验和由 worker 补。
                // ⚠️ 此处**不得**再包 `AddChecksum` —— 那会二次签名 ⇒ 头错位
                // (`Load` 报「存档版本 1508257137 > 当前 1」,回归实证)。
                compose: _ => SaveComposer.Compose(
                    stream.Events, PredicateOf(stream), _decoder, new SaveCodec(),
                    worldSeed: 1UL, configVersion: 0U, tick: 20L,
                    existingFoldedRows: stream.FoldedRows),
                writeScheduler: action => action());
            svc.Checkpoint(new SaveSlot(0));

            // ══ Act:「进程重启」⇒ 新流 + 同目录 Load,恢复闭包 = Restore ══
            var fresh = new EventStream(new FakeIdAuthority(), new FakePresenceQuery());
            IReadOnlyList<SimEvent> pending = null;
            var svcLoad = new SaveService(
                _testDir, new SaveCodec(),
                folded: PredicateOf(fresh),
                narrativeContext: FuncNarrativeContextQuery.AlwaysIn,
                replaySink: events => pending = events,
                foldedRowSink: bytes => fresh.Restore(
                    pending ?? throw new InvalidOperationException("折叠段先于事件段到达"),
                    FoldRowCodec.DecodeSegment(bytes)),
                // 同上:`compose` 返回未签名,worker 补校验和。
                compose: _ => SaveComposer.Compose(
                    fresh.Events, PredicateOf(fresh), _decoder, new SaveCodec(),
                    worldSeed: 1UL, configVersion: 0U, tick: 20L,
                    existingFoldedRows: fresh.FoldedRows),
                writeScheduler: action => action());
            svcLoad.Load(new SaveSlot(0));

            // ══ Assert:恢复态 == 直接重放所得(AC-7a-01 形状的三点)══
            Assert.AreEqual(0, fresh.Count,
                "被折病人病史行已抹:恢复后事件段不含其任何行");
            Assert.AreEqual(1, fresh.FoldedRows.Count, "折叠行经折叠段字节回到 sim");
            Assert.AreEqual(PatientA, fresh.FoldedRows[0].PatientId,
                "patient_id 必留(ADR-006 Amendment B)");
            Assert.AreEqual(Seed, fresh.FoldedRows[0].PatientSeed,
                "patient_seed 从 onset 载荷往返,不得重算");

            // 高水位:被折病人 id 只活在折叠行 ⇒ 漏掉它 = 新病人复用已折叠 id。
            Assert.AreEqual(PatientA + 1, fresh.GetNextPatientId().Value,
                "恢复后高水位须从折叠行重构(== 直接重放世界流所得)");

            // 二次 Checkpoint 不丢折叠行(回写义务):
            svcLoad.Checkpoint(new SaveSlot(1));
            byte[] secondSave = File.ReadAllBytes(Path.Combine(_testDir, "save_1.bin"));
            var r = new CodecReader(secondSave.AsSpan(SaveHeaderCodec.HeaderSize));
            SimEventListCodec.ReadWithinBody(ref r, new SaveCodec());
            IReadOnlyList<FoldRow> carried = FoldRowCodec.DecodeSegment(r.ReadBytes(r.Remaining));
            Assert.AreEqual(1, carried.Count,
                "读档后的下一次 Checkpoint:折叠行经 existingFoldedRows 回写,不静默消失");
            Assert.AreEqual(PatientA, carried[0].PatientId);
        }
    }
}
