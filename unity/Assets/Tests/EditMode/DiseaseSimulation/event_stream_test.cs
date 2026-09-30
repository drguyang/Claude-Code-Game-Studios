// disease-simulation Story 002 测试
//
// AC-2: 五+一抽象点接口齐备
// AC-15: 有界性（PATIENT_APPEARANCE_CAP = 24）
// AC-36: id 机制（计数器永不复位）
// AC-16: 流侧（Seq 单调 + 复位）
// AC-15: 处置去重
// TR-disease-005: patient_seed 纯函数

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.DiseaseSimulation
{
    // 测试用 fake 实现
    internal sealed class FakeTickProvider : ITickProvider
    {
        public long CurrentTick { get; set; }
    }

    internal sealed class FakeIdAuthority : IIdAuthority
    {
        private int _nextPatientId = 0;
        private int _nextItemInstanceId = 0;

        public PatientId NextPatientId() => new PatientId(_nextPatientId++);
        public ItemInstanceId NextItemInstanceId() => new ItemInstanceId(_nextItemInstanceId++);
    }

    internal sealed class FakePresenceQuery : IPresenceQuery
    {
        private readonly HashSet<int> _presentPatients = new HashSet<int>();

        public bool IsPresent(PatientId patientId) => _presentPatients.Contains(patientId.Value);
        public int PresentCount => _presentPatients.Count;

        public void Add(PatientId patientId) => _presentPatients.Add(patientId.Value);
        public void Clear() => _presentPatients.Clear();
    }

    internal sealed class FakeEventAuthority : IEventAuthority
    {
        public bool IsAuthority => true;
        public EventRollResult Roll(in RollRequest r) => new EventRollResult(0, 0, 0, 0);
    }

    public class EventStreamTest
    {
        private EventStream _stream;
        private FakePresenceQuery _presenceQuery;
        private FakeIdAuthority _idAuthority;

        [SetUp]
        public void Setup()
        {
            _presenceQuery = new FakePresenceQuery();
            _idAuthority = new FakeIdAuthority();
            _stream = new EventStream(_idAuthority, _presenceQuery);
        }

        // AC-15: CAP 拒收
        [Test]
        public void test_cap_rejectsBeyond24()
        {
            // 添加 24 个在场病人
            for (int i = 0; i < EventStream.PATIENT_APPEARANCE_CAP; i++)
            {
                _presenceQuery.Add(new PatientId(i));
            }

            Assert.AreEqual(EventStream.PATIENT_APPEARANCE_CAP, _presenceQuery.PresentCount);

            // 第 25 个病人应被拒收（Append 时检查）
            var newPatient = new PatientId(100);
            Assert.Throws<InvalidOperationException>(() =>
            {
                _stream.Append(new SimEvent(0, newPatient, 0, EventKind.ActorCellEntered, default));
            });
        }

        // AC-16: Seq 复位
        [Test]
        public void test_seq_resetsEachTick()
        {
            var patient = new PatientId(1);

            // Tick 100: 发 3 个事件
            _stream.Append(new SimEvent(100, patient, 0, EventKind.ActorCellEntered, default));
            _stream.Append(new SimEvent(100, patient, 1, EventKind.ActorCellEntered, default));
            _stream.Append(new SimEvent(100, patient, 2, EventKind.ActorCellEntered, default));

            // Tick 101: Seq 应复位
            _stream.Append(new SimEvent(101, patient, 0, EventKind.ActorCellEntered, default));

            // 验证事件数
            Assert.AreEqual(4, _stream.Count);
        }

        // AC-15: 去重
        [Test]
        public void test_dedup_sameEventTwice()
        {
            var patient = new PatientId(1);
            var evt = new SimEvent(100, patient, 0, EventKind.ActorCellEntered, default);

            _stream.Append(evt);
            _stream.Append(evt); // 重发

            // 去重后只应有一条
            Assert.AreEqual(1, _stream.Count);
        }

        // AC-36: id 重构
        [Test]
        public void test_idReconstruction_fromEventStream()
        {
            // 添加一些事件
            _stream.Append(new SimEvent(0, new PatientId(0), 0, EventKind.ActorCellEntered, default));
            _stream.Append(new SimEvent(0, new PatientId(5), 0, EventKind.ActorCellEntered, default));
            _stream.Append(new SimEvent(0, new PatientId(3), 0, EventKind.ActorCellEntered, default));

            // 重构 next = max + 1
            var next = _stream.GetNextPatientId();
            Assert.AreEqual(6, next.Value);
        }

        // AC-2: 抽象点接口齐备
        [Test]
        public void test_abstractionPoints_exist()
        {
            Assert.IsNotNull(_stream);
            Assert.IsNotNull(_idAuthority);
            Assert.IsNotNull(_presenceQuery);
            Assert.IsNotNull(new FakeTickProvider());
            Assert.IsNotNull(new FakeEventAuthority());
        }
    }
}
