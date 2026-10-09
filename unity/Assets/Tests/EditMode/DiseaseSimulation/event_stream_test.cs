// disease-simulation Story 002 测试
//
// AC-2: 五+一抽象点接口齐备
// AC-15: 有界性（PATIENT_APPEARANCE_CAP = 24）
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
        public bool IsPresentAt(WorldPos cell) => false;

        public void Add(PatientId patientId) => _presentPatients.Add(patientId.Value);
        public void Clear() => _presentPatients.Clear();
    }

    internal sealed class FakeEventAuthority : IEventAuthority
    {
        public bool IsAuthority => true;
        public bool IsHost => true;
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
                _stream.Append(new SimEvent(0, newPatient, -1, EventKind.ActorCellEntered, default)); // -1 = 未发号哨兵 O-1
            });
        }

        // AC-16: Seq 复位
        [Test]
        public void test_seq_resetsEachTick()
        {
            var patient = new PatientId(1);

            // Tick 100: 未发号事件(哨兵 -1,O-1)—— 得首号 0
            _stream.Append(new SimEvent(100, patient, -1, EventKind.ActorCellEntered, default));
            // 同 (kind, tick, patient) 的未发号重发 = 去重(承既有语义:入流键含入流 Seq,
            // 未发号键同为 -1 ⇒ 幂等重试不产生重复)
            _stream.Append(new SimEvent(100, patient, -1, EventKind.ActorCellEntered, default));
            // 显式 Seq 事件不受发号影响(入流键不同 ⇒ 不去重)
            _stream.Append(new SimEvent(100, patient, 1, EventKind.SkillGrown, default));

            // Tick 110: Seq 应复位 —— 再得首号 0
            _stream.Append(new SimEvent(110, patient, -1, EventKind.ActorCellEntered, default));

            // 验证事件数 + 发号值 + 复位
            Assert.AreEqual(3, _stream.Count, "第二条未发号重发被去重");
            Assert.AreEqual(0L, _stream.Events[0].Seq, "tick100 首号 = 0(合法已发号值,O-1)");
            Assert.AreEqual(1L, _stream.Events[1].Seq, "显式 Seq 原样保留");
            Assert.AreEqual(0L, _stream.Events[2].Seq, "tick110 复位后再得首号 0");
        }

        // AC-15: 去重
        [Test]
        public void test_dedup_sameEventTwice()
        {
            var patient = new PatientId(1);
            var evt = new SimEvent(100, patient, -1, EventKind.ActorCellEntered, default); // -1 = 未发号哨兵 O-1

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
            _stream.Append(new SimEvent(0, new PatientId(0), -1, EventKind.ActorCellEntered, default));
            _stream.Append(new SimEvent(0, new PatientId(5), -1, EventKind.ActorCellEntered, default));
            _stream.Append(new SimEvent(0, new PatientId(3), -1, EventKind.ActorCellEntered, default));

            // 重构 next = max + 1
            var next = _stream.GetNextPatientId();
            Assert.AreEqual(6, next.Value);
        }

        // O-1 修复(2026-10-09):哨兵 -1;0 是合法已发号值,不再被覆盖
        [Test]
        public void test_o1_seqSentinel_negativeIssued_zeroExplicitPreserved()
        {
            var patient = new PatientId(1);
            // 三条用不同 Kind —— 避开「同 (kind, tick, patient) 未发号重发去重」(承既有语义)

            // ① 未发号(-1)→ 发首号 0
            _stream.Append(new SimEvent(10, patient, -1, EventKind.ActorCellEntered, default));
            Assert.AreEqual(0L, _stream.Events[0].Seq, "未发号(-1)应发首号 0");

            // ② 显式 0 → 原样保留(不再被哨兵覆盖 —— O-1 修复点)
            _stream.Append(new SimEvent(10, patient, 0, EventKind.SkillGrown, default));
            Assert.AreEqual(0L, _stream.Events[1].Seq, "显式 0 应原样保留(0 是合法已发号值)");

            // ③ 未发号(-1)→ 继续发号
            // 注:计数器对每条同 (Tick, Patient) 事件都前进(含显式事件,承既有行为)
            // ⇒ ② 已把 _currentSeq 推到 1,③ 得 2。
            _stream.Append(new SimEvent(10, patient, -1, EventKind.InjuryOnset, default));
            Assert.AreEqual(2L, _stream.Events[2].Seq, "后续未发号应得递增值(计数器含显式事件前进)");
        }

        // 评审 A3:发号复位条件的 patient 分量 —— 同 tick 换患者必须复位 0(AC-16 另一半)
        [Test]
        public void test_ac16_seqResets_onPatientSwitch_sameTick()
        {
            // 同 tick 50,患者 1 两条(不同 Kind 避开去重)→ Seq 0, 1
            _stream.Append(new SimEvent(50, new PatientId(1), -1, EventKind.ActorCellEntered, default));
            _stream.Append(new SimEvent(50, new PatientId(1), -1, EventKind.InjuryOnset, default));
            // 同 tick 换患者 2 ⇒ 复位 → Seq 0, 1(而非续号 2, 3)
            _stream.Append(new SimEvent(50, new PatientId(2), -1, EventKind.ActorCellEntered, default));
            _stream.Append(new SimEvent(50, new PatientId(2), -1, EventKind.InjuryOnset, default));

            Assert.AreEqual(0L, _stream.Events[0].Seq, "患者1 首号 0");
            Assert.AreEqual(1L, _stream.Events[1].Seq, "患者1 续号 1");
            Assert.AreEqual(0L, _stream.Events[2].Seq, "同 tick 换患者必须复位 0");
            Assert.AreEqual(1L, _stream.Events[3].Seq, "患者2 续号 1");
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
