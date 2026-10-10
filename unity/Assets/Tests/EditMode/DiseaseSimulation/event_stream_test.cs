// disease-simulation Story 002 测试
//
// AC-2: 五+一抽象点接口齐备
// AC-15: 有界性（PATIENT_APPEARANCE_CAP = 24）
// AC-16: 流侧（Seq 单调 + 复位）
// AC-15: 处置去重
// TR-disease-005: patient_seed 纯函数

using System;
using System.Collections.Generic;
using System.Linq;
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
        public IReadOnlyCollection<int> PresentPatientIds() => _presentPatients.ToArray();

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
            // 同一未发号事件重发 = 去重(O-4 后口径:未发号键含 PayloadRef 身份,
            // 同一 SimEvent 重发 ⇒ 同 PayloadRef ⇒ 同键 ⇒ 幂等重试不产生重复)
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

        // O-4 修复(2026-10-09):未发号事件键补载荷身份 —— 不同载荷不得坍缩
        [Test]
        public void test_o4_distinctPayloads_sameKindTickPatient_bothKept()
        {
            // 现实触发:同 tick 放置两个结构(均 None 世界事件 + 不同 structureId 载荷)
            _stream.Append(new SimEvent(10, PatientId.None, -1, EventKind.StructurePlaced, new PayloadRef(1, 0, 8)));
            _stream.Append(new SimEvent(10, PatientId.None, -1, EventKind.StructurePlaced, new PayloadRef(2, 0, 8)));

            Assert.AreEqual(2, _stream.Count, "不同载荷 = 不同事件,均应入流(同 tick 双结构不再坍缩)");
            Assert.AreEqual(0L, _stream.Events[0].Seq, "首条得首号");
            Assert.AreEqual(1L, _stream.Events[1].Seq, "次条续号");
        }

        // O-4 反向守卫:载荷身份入键不得破坏「同一事件重发」的幂等去重
        [Test]
        public void test_o4_resendSameUnreleasedEvent_deduped()
        {
            var evt = new SimEvent(10, PatientId.None, -1, EventKind.StructurePlaced, new PayloadRef(7, 0, 8));

            _stream.Append(evt);
            _stream.Append(evt); // 同一 SimEvent 重发(同 PayloadRef)

            Assert.AreEqual(1, _stream.Count, "重发仍须幂等拒收(载荷入键 ≠ 放弃去重)");
        }

        // O-5 修复(2026-10-09):PatientId.None 世界事件不受 CAP 拒收
        [Test]
        public void test_o5_worldEvents_nonePatient_bypassesCap()
        {
            for (int i = 0; i < EventStream.PATIENT_APPEARANCE_CAP; i++)
                _presenceQuery.Add(new PatientId(i)); // 灌满 CAP
            Assert.AreEqual(EventStream.PATIENT_APPEARANCE_CAP, _presenceQuery.PresentCount);

            // 三种 None 世界事件在 CAP 满时均应入流(结构 / POI / 玩家跨格)
            _stream.Append(new SimEvent(0, PatientId.None, -1, EventKind.StructurePlaced, new PayloadRef(1, 0, 8)));
            _stream.Append(new SimEvent(0, PatientId.None, -1, EventKind.PoiStateChanged, new PayloadRef(2, 0, 8)));
            _stream.Append(new SimEvent(0, PatientId.None, -1, EventKind.ActorCellEntered, new PayloadRef(3, 0, 8)));

            Assert.AreEqual(3, _stream.Count, "CAP 满不得拒收世界事件(None 非病人)");

            // 测试面 F-2:CAP 满 × **在场**真实病人 —— 稳态满员不得冻结
            // (杀「删 !IsPresent 条件」变异:否则 24 个在场病人的后续事件全抛 AC-15)
            _stream.Append(new SimEvent(1, new PatientId(0), -1, EventKind.DiseaseOnset, new PayloadRef(4, 0, 8)));
            Assert.AreEqual(4, _stream.Count, "在场病人(0..23)的事件在 CAP 满稳态下仍可入流");
            // 非 None 且**不在场**的病人在 CAP 满时仍须拒收 —— 由 test_cap_rejectsBeyond24 守另一半
        }

        // 代码面 F3 + 测试面 F-1(合并):条件键第二分支(已发号/显式 Seq)—— 一测两面
        [Test]
        public void test_dedup_explicitSeq_payloadBlind_andSeqDistinguished()
        {
            var patient = new PatientId(1);

            // 面①:同 (Kind, Patient, Tick)、显式 Seq=5、**不同 PayloadRef** ⇒ 去重
            // (已发号键不补载荷 ⇒ 重编码/重映射的重传命中同键;杀「条件改恒真」变异)
            _stream.Append(new SimEvent(20, patient, 5, EventKind.SkillGrown, new PayloadRef(1, 0, 8)));
            _stream.Append(new SimEvent(20, patient, 5, EventKind.SkillGrown, new PayloadRef(2, 0, 8)));
            Assert.AreEqual(1, _stream.Count, "显式同 Seq = 同一事件,载荷差异不得造成假阴性去重");

            // 面②:同参但 Seq=5 / Seq=6(同 PayloadRef)⇒ 各自入流
            // (已发号键含 Seq 分量;杀「issued 键丢 Seq」变异)
            _stream.Append(new SimEvent(20, patient, 6, EventKind.SkillGrown, new PayloadRef(1, 0, 8)));
            Assert.AreEqual(2, _stream.Count, "不同显式 Seq = 不同事件,须都入流");

            // Seq 发号器不受显式 Seq 写入影响的既有语义:计数器对同 (Tick, Patient) 每条事件
            // 都前进(含显式)—— 首条 reset 至 0,显式 6 推进至 1,本条未发号得 2。
            _stream.Append(new SimEvent(20, patient, -1, EventKind.InjuryOnset, new PayloadRef(9, 0, 8)));
            Assert.AreEqual(3, _stream.Count);
            Assert.AreEqual(2L, _stream.Events[2].Seq, "未发号续发(计数器含显式事件前进,承既有行为)");
        }

        // 测试面 F-3:Clear 清键 + 发号复位(test-only API 的键重建)
        [Test]
        public void test_clear_resetsDedupKeysAndSeq()
        {
            var patient = new PatientId(1);
            var evt = new SimEvent(30, patient, -1, EventKind.SkillGrown, new PayloadRef(5, 0, 8));
            _stream.Append(evt);
            Assert.AreEqual(1, _stream.Count);

            _stream.Clear();

            Assert.AreEqual(0, _stream.Count, "Clear 清事件");
            _stream.Append(evt); // 同一事件:键已清 ⇒ 须可再入
            Assert.AreEqual(1, _stream.Count, "Clear 须同时清去重键(否则同事件永远进不来)");
            Assert.AreEqual(0L, _stream.Events[0].Seq, "Clear 须复位发号器(再入得首号 0)");
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

        // BCD-码-3(2026-10-10):流按 Tick 非降 —— 回退 tick 的新事件入流期 fail-loud
        [Test]
        public void test_monotonic_tickRegression_throwsBeforeWrite()
        {
            var patient = new PatientId(1);
            _stream.Append(new SimEvent(100, patient, -1, EventKind.ActorCellEntered, default));
            _stream.Append(new SimEvent(110, patient, -1, EventKind.InjuryOnset, default));

            // 不同 Kind ⇒ 不走去重短路,撞单调断言(游标 break 门的写入期护栏)
            Assert.Throws<InvalidOperationException>(() =>
                _stream.Append(new SimEvent(50, patient, -1, EventKind.DiseaseOnset, default)),
                "tick 回退(110 → 50)的新事件须 fail-loud(BCD-码-3)");

            Assert.AreEqual(2, _stream.Count,
                "断言在入列表之前抛出 ⇒ 流不被污染(去重键 / 发号器均未写)");
        }

        // BCD-码-3 配套:旧 tick **重发**走去重短路,不得误触断言(45 重传语义保留)
        [Test]
        public void test_monotonic_resendOldTick_dedupedNotThrown()
        {
            var patient = new PatientId(1);
            var old = new SimEvent(100, patient, -1, EventKind.ActorCellEntered, default);
            _stream.Append(old);
            _stream.Append(new SimEvent(120, patient, -1, EventKind.InjuryOnset, default));

            Assert.DoesNotThrow(() => _stream.Append(old),
                "旧 tick 同一事件重发 = 去重短路,先于单调断言(重传不因新事件入流而炸)");
            Assert.AreEqual(2, _stream.Count, "重发仍被去重拒收");
        }

        // BCD-码-3 配套:Clear 复位单调基线(与发号器 / 去重键同格)
        [Test]
        public void test_monotonic_clearResetsBaseline()
        {
            var patient = new PatientId(1);
            _stream.Append(new SimEvent(100, patient, -1, EventKind.ActorCellEntered, default));
            _stream.Clear();

            Assert.DoesNotThrow(
                () => _stream.Append(new SimEvent(50, patient, -1, EventKind.InjuryOnset, default)),
                "Clear 须一并清单调基线(否则清空后旧基线拦住合法的低 tick 重建)");
            Assert.AreEqual(1, _stream.Count);
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
