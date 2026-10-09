// ADR-030 —— 9 的 DiseaseOnset 写者测试。
//
// 验证:
//   - PatientSpawner.SpawnNext 分配 patient_id
//   - patient_seed = SplitMix64.Hash(worldSeed, patientId)
//   - DiseaseOnset 事件写入病史流
//   - 载荷编码/解码链路闭合

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.DiseaseSimulation;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.DiseaseSimulation
{
    public class PatientSpawnerTest
    {
        private FakeIdAuthority _idAuthority;
        private FakeEventSink _sink;
        private InMemoryBlobPool _pool;
        private PayloadEncoder _encoder;
        private PatientSpawner _spawner;

        private const ulong WorldSeed = 12345UL;

        [SetUp]
        public void Setup()
        {
            _idAuthority = new FakeIdAuthority();
            _sink = new FakeEventSink();
            _pool = new InMemoryBlobPool();
            _encoder = new PayloadEncoder(_pool);
            _spawner = new PatientSpawner(_idAuthority, _sink, _encoder, WorldSeed);
        }

        // ADR-030 §③: 写者 = 9
        [Test]
        public void test_spawnNext_writesDiseaseOnset()
        {
            var patientId = _spawner.SpawnNext(diseaseId: 1, tick: 100);

            Assert.AreEqual(1, _sink.AppendedEvents.Count, "应写入一条 DiseaseOnset 事件");
            Assert.AreEqual(EventKind.DiseaseOnset, _sink.AppendedEvents[0].Kind);
            Assert.AreEqual(patientId, _sink.AppendedEvents[0].Patient);
            Assert.AreEqual(100, _sink.AppendedEvents[0].Tick);
        }

        // ADR-030 §④: 落病史流
        [Test]
        public void test_spawnNext_routesToHistoryStream()
        {
            var patientId = _spawner.SpawnNext(diseaseId: 2, tick: 200);

            var e = _sink.AppendedEvents[0];
            Assert.AreEqual(EventKind.DiseaseOnset, e.Kind);
            Assert.AreEqual(StreamId.History, StreamRouting.Of(e.Kind),
                "DiseaseOnset 应路由到病史流");
        }

        // disease-simulation.md 规则六 :164: patient_seed = hash(world_seed, patient_id)
        [Test]
        public void test_spawnNext_patientSeedIsHashOfWorldSeedAndPatientId()
        {
            var patientId = _spawner.SpawnNext(diseaseId: 3, tick: 300);

            // 与生产侧同口径:哈希 64 位按位承载(不截断)。
            // 期望值独立重算(不复用生产代码结果),但复用同一表达式 ⇒ 判别力限于
            // 「是否调 Hash / 参数是否正确」,不覆盖「Hash 算法本身被换」(评审 A1)。
            var expectedSeed = unchecked((long)SplitMix64.Hash((long)WorldSeed, patientId.Value));
            var e = _sink.AppendedEvents[0];

            // 解码载荷验证 patient_seed
            Assert.IsTrue(_pool.TryGetBlob(e.Payload.BlobId, out var blob));
            var decoded = PayloadCodec.Decode<DiseaseOnsetPayload>(e.Kind, blob.Span);
            Assert.AreEqual(expectedSeed, decoded.PatientSeed,
                "patient_seed 应等于 SplitMix64.Hash(worldSeed, patientId)");
        }

        // ADR-030: 载荷字段逐字段验证
        [Test]
        public void test_spawnNext_payloadFields()
        {
            var patientId = _spawner.SpawnNext(diseaseId: 4, tick: 400);

            var e = _sink.AppendedEvents[0];
            Assert.IsTrue(_pool.TryGetBlob(e.Payload.BlobId, out var blob));
            var decoded = PayloadCodec.Decode<DiseaseOnsetPayload>(e.Kind, blob.Span);

            Assert.AreEqual(400, decoded.OnsetTick, "OnsetTick 应匹配");
            Assert.AreEqual(4, decoded.DiseaseId, "DiseaseId 应匹配");
            Assert.AreEqual(patientId.Value, decoded.PatientId, "PatientId 应匹配");
            Assert.AreEqual(0, decoded.Seq, "Seq 初始应为 0(由 EventStream 发放)");
        }

        // ADR-005: IIdAuthority 分配 patient_id
        [Test]
        public void test_spawnNext_incrementsPatientId()
        {
            var p1 = _spawner.SpawnNext(diseaseId: 1, tick: 100);
            var p2 = _spawner.SpawnNext(diseaseId: 1, tick: 101);

            Assert.AreEqual(0, p1.Value, "第一个病人 id = 0");
            Assert.AreEqual(1, p2.Value, "第二个病人 id = 1");
        }

        // A3(评审 ADVISORY): 入参校验 — 负值应抛 ArgumentOutOfRangeException
        [Test]
        public void test_spawnNext_rejectsNegativeDiseaseId()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => _spawner.SpawnNext(diseaseId: -1, tick: 100),
                "负 diseaseId 应抛 ArgumentOutOfRangeException");
        }

        [Test]
        public void test_spawnNext_rejectsNegativeTick()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => _spawner.SpawnNext(diseaseId: 1, tick: -1),
                "负 tick 应抛 ArgumentOutOfRangeException");
        }

        // 测试用 fake 实现
        private sealed class FakeIdAuthority : IIdAuthority
        {
            private int _nextPatientId = 0;
            public PatientId NextPatientId() => new PatientId(_nextPatientId++);
            public ItemInstanceId NextItemInstanceId() => new ItemInstanceId(0);
        }

        private sealed class FakeEventSink : IEventSink
        {
            public readonly List<SimEvent> AppendedEvents = new List<SimEvent>();
            public void Append(in SimEvent e) => AppendedEvents.Add(e);
        }
    }
}
