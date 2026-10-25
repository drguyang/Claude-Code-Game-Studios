// TD-randomevents-02 —— 出诊启动写者 + 活跃出诊目标查询的行为钉子(52 规则十二)。
//
// 卡面判据(债册 TD-randomevents-02 ④「夹具 AC-52-56/57/58」):
//   · AC-52-56:跨出医馆 ⇒ 恰一条 HouseCallStarted(Patient = None,载荷 target_patient_id
//     与急召链接的病人一致);同 tick 重入 / 跨回再跨出 ⇒ 仍恰一条(一次性)。
//   · AC-52-57:派生定义三条件 —— ①立案后活跃转假;②无立案无窗口 ⇒ 持续活跃(P0 无窗口);
//     ③读档重建(同流再判读)逐位一致。链接断裂 fail-loud(2026-10-25 裁定①)。
//   · AC-52-58:医馆内移动不触发 / 无目标不触发 / 本 tick 无跨格不触发;
//     且 HouseCallStarted 落流的同一 Step 问 checkpoint 门 ⇒ 门答「安全」
//     (12.4:旅途段不构成 unsafe —— 锚点③ 不得自否决,防呆注的否证钉)。
//
// 权威:GDD random-events.md 规则十二(12.1–12.4)/ entities.yaml HouseCallStarted /
//   ADR-029 §③ + ADR-031 §②(编码解码抽象点)/ ADR-015 §三(整数格判据域)

using System;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Interaction;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.RandomEvents
{
    public class HouseCallTest
    {
        private const int PlayerActorId = 1;
        private const int HouseCallEventKey = 7;
        private static readonly WorldPos ClinicCell = new WorldPos(5, 0, 0);
        private static readonly WorldPos OutsideCell = new WorldPos(9, 0, 0);

        private InMemoryBlobPool _pool;
        private PayloadEncoder _encoder;
        private PayloadDecoder _decoder;
        private RecordingSink _sink;
        private ClinicStub _clinic;
        private List<SimEvent> _stream;
        private HouseCallStartedWriter _writer;

        [SetUp]
        public void Setup()
        {
            _pool = new InMemoryBlobPool();
            _encoder = new PayloadEncoder(_pool);
            _decoder = new PayloadDecoder(_pool);
            _sink = new RecordingSink();
            _clinic = new ClinicStub();
            _clinic.Cells.Add(ClinicCell);
            _stream = new List<SimEvent>();
            _writer = new HouseCallStartedWriter(
                _sink, _encoder, _decoder, _clinic,
                new[] { HouseCallEventKey }, PlayerActorId);
        }

        private sealed class RecordingSink : IEventSink
        {
            public int AppendCount;
            public readonly List<SimEvent> Events = new List<SimEvent>();
            public void Append(in SimEvent e) { AppendCount++; Events.Add(e); }
        }

        /// <summary>医馆格判据替身(显式格集合;同源义务归 Boot 装配轮 —— stub 不构成「已同源」)。</summary>
        private sealed class ClinicStub : IClinicCellSource
        {
            public readonly HashSet<WorldPos> Cells = new HashSet<WorldPos>();
            public bool IsClinicCell(WorldPos cell) => Cells.Contains(cell);
        }

        private sealed class ArmedStub : IArmedState
        {
            public bool IsArmed { get; set; }
        }

        // ── 流构造辅助(直接物化流事件;急召 / onset / 立案的合法写者归 9 / 37 / 52)──

        private void AddSummons(long tick, int eventKey = HouseCallEventKey)
        {
            var p = _encoder.Encode(EventKind.EventArrived,
                new EventArrivedPayload(eventKey, 0, OutsideCell, -1));
            _stream.Add(new SimEvent(tick, PatientId.None, -1, EventKind.EventArrived, p));
        }

        private void AddOnset(long tick, int patientId)
        {
            var p = _encoder.Encode(EventKind.DiseaseOnset,
                new DiseaseOnsetPayload(tick, 3, patientId, tick * 31 + patientId, -1));
            _stream.Add(new SimEvent(tick, PatientId.None, -1, EventKind.DiseaseOnset, p));
        }

        private void AddCaseOpened(long tick, int patientId)
        {
            var p = _encoder.Encode(EventKind.CaseOpened,
                new CaseOpenedPayload(patientId, new DiseaseIdSet(1UL << 3)));
            _stream.Add(new SimEvent(tick, new PatientId(patientId), -1, EventKind.CaseOpened, p));
        }

        private void AddEntered(long tick, WorldPos cell, int actorId = PlayerActorId)
        {
            var p = _encoder.Encode(EventKind.ActorCellEntered,
                new ActorCellEnteredPayload(actorId, cell, tick));
            _stream.Add(new SimEvent(tick, PatientId.None, -1, EventKind.ActorCellEntered, p));
        }

        /// <summary>标准急召场景:急召@10 + 病人@10 链接 + 玩家先在医馆(entered@8)。</summary>
        private void BuildSummonedPatientInClinic()
        {
            AddSummons(10);
            AddOnset(10, 42);
            AddEntered(8, ClinicCell);
        }

        // ── AC-52-57:活跃出诊目标派生定义(12.2 三条件 + 链接 fail-loud)──

        /// <summary>无急召 ⇒ 无活跃目标;非出诊类 EventArrived 不算急召。</summary>
        [Test]
        public void test_ac5257_noHouseCallSummons_inactive()
        {
            Assert.IsFalse(ActiveHouseCallQuery.Evaluate(_stream, _decoder, new[] { HouseCallEventKey }).Active,
                "空流 ⇒ 无活跃目标");

            AddSummons(10, eventKey: 99);   // 非出诊类 event_key
            AddOnset(10, 42);
            Assert.IsFalse(ActiveHouseCallQuery.Evaluate(_stream, _decoder, new[] { HouseCallEventKey }).Active,
                "event_key 不在出诊闭集 ⇒ 不算急召");
        }

        /// <summary>急召 + 同 tick 病人链接 ⇒ 活跃,目标 id = 急召降临 tick 创建的那位。</summary>
        [Test]
        public void test_ac5257_summonsLinkedToSameTickOnset_active()
        {
            AddSummons(10);
            AddOnset(10, 42);

            var r = ActiveHouseCallQuery.Evaluate(_stream, _decoder, new[] { HouseCallEventKey });
            Assert.IsTrue(r.Active, "未立案 ⇒ 活跃");
            Assert.AreEqual(42, r.TargetPatientId, "目标 = 急召降临 tick 创建的病人(零发号,从流读)");
            Assert.AreEqual(10, r.SummonsTick, "急召 tick = EventArrived 的 Envelope Tick");
        }

        /// <summary>① 目标病人 CaseOpened 落流 ⇒ 活跃转假(立案 = 终点)。</summary>
        [Test]
        public void test_ac5257_caseOpenedTerminatesTarget()
        {
            BuildSummonedPatientInClinic();
            Assert.IsTrue(ActiveHouseCallQuery.Evaluate(_stream, _decoder, new[] { HouseCallEventKey }).Active);

            AddCaseOpened(15, 42);
            Assert.IsFalse(ActiveHouseCallQuery.Evaluate(_stream, _decoder, new[] { HouseCallEventKey }).Active,
                "目标病人已立案 ⇒ 无活跃目标(12.2 条件②)");
        }

        /// <summary>立案早于急召(旧例)不终止新急召;他例病人的立案不影响本目标。</summary>
        [Test]
        public void test_ac5257_caseOpenedOfOtherTimeOrPatient_ignored()
        {
            AddCaseOpened(5, 42);      // 早于急召的旧立案
            AddSummons(10);
            AddOnset(10, 42);
            Assert.IsTrue(ActiveHouseCallQuery.Evaluate(_stream, _decoder, new[] { HouseCallEventKey }).Active,
                "急召前的立案不终止本目标(e.Tick >= 急召 tick 才算)");

            AddCaseOpened(15, 99);     // 他例病人的立案
            Assert.IsTrue(ActiveHouseCallQuery.Evaluate(_stream, _decoder, new[] { HouseCallEventKey }).Active,
                "终止面只看目标病人");
        }

        /// <summary>② P0 无窗口:急召后流逝大量 tick 且无立案 ⇒ 活跃持续(搁置不超时)。</summary>
        [Test]
        public void test_ac5257_noWindow_activePersists()
        {
            BuildSummonedPatientInClinic();
            for (long t = 20; t <= 2000; t += 100)
                AddEntered(t, OutsideCell);   // 玩家在外面闲逛很久

            Assert.IsTrue(ActiveHouseCallQuery.Evaluate(_stream, _decoder, new[] { HouseCallEventKey }).Active,
                "P0 无窗口 ⇒ 永不过期,搁置不转假(窗口归 37,落地时改本查询)");
        }

        /// <summary>③ 读档重建 = 同流再判读:两次 Evaluate 结果逐位一致(流是唯一真源)。</summary>
        [Test]
        public void test_ac5257_replayEvaluate_bitwiseStable()
        {
            BuildSummonedPatientInClinic();
            var a = ActiveHouseCallQuery.Evaluate(_stream, _decoder, new[] { HouseCallEventKey });
            var b = ActiveHouseCallQuery.Evaluate(_stream, _decoder, new[] { HouseCallEventKey });
            Assert.AreEqual(a.Active, b.Active);
            Assert.AreEqual(a.TargetPatientId, b.TargetPatientId);
            Assert.AreEqual(a.SummonsTick, b.SummonsTick);

            AddCaseOpened(15, 42);
            var c = ActiveHouseCallQuery.Evaluate(_stream, _decoder, new[] { HouseCallEventKey });
            var d = ActiveHouseCallQuery.Evaluate(_stream, _decoder, new[] { HouseCallEventKey });
            Assert.AreEqual(c.Active, d.Active, "终止态同样重建稳定");
            Assert.IsFalse(c.Active);
        }

        /// <summary>裁定①:降临 tick 处 DiseaseOnset 为 0 条 ⇒ fail-loud(9 侧未接线 / 流已坏)。</summary>
        [Test]
        public void test_ac5257_linkMissingOnset_throws()
        {
            AddSummons(10);   // 无同 tick onset
            Assert.Throws<InvalidOperationException>(
                () => ActiveHouseCallQuery.Evaluate(_stream, _decoder, new[] { HouseCallEventKey }),
                "链接断裂须抛,不静默返回 inactive(静默 = 第二真源形态)");
        }

        /// <summary>裁定①:降临 tick 处 DiseaseOnset 多条 ⇒ fail-loud(链接目标不可判定,拒绝猜)。</summary>
        [Test]
        public void test_ac5257_linkDuplicateOnset_throws()
        {
            AddSummons(10);
            AddOnset(10, 42);
            AddOnset(10, 43);
            Assert.Throws<InvalidOperationException>(
                () => ActiveHouseCallQuery.Evaluate(_stream, _decoder, new[] { HouseCallEventKey }),
                "同 tick 双病人 = 链接歧义,fail-loud");
        }

        /// <summary>相邻 tick 的 onset(急召 tick ± 1)不构成链接 —— 必须**同 tick**。</summary>
        [Test]
        public void test_ac5257_onsetAtAdjacentTick_notALink()
        {
            AddSummons(10);
            AddOnset(9, 42);    // 早一 tick
            AddOnset(11, 43);   // 晚一 tick
            Assert.Throws<InvalidOperationException>(
                () => ActiveHouseCallQuery.Evaluate(_stream, _decoder, new[] { HouseCallEventKey }),
                "链接只认同 tick;两侧的 onset 都不算(0 条 ⇒ 抛)");
        }

        // ── AC-52-56:跨出医馆 ⇒ 恰一条 HouseCallStarted(一次性)──

        /// <summary>主形态:医馆 → 非医馆 ⇒ Applied,恰一条,载荷 = 链接病人,Patient = None。</summary>
        [Test]
        public void test_ac5256_crossOut_appendsExactlyOne()
        {
            BuildSummonedPatientInClinic();
            AddEntered(12, OutsideCell);

            Assert.AreEqual(HouseCallStartResult.Applied, _writer.TryStartAtTick(_stream, 12));
            Assert.AreEqual(1, _sink.AppendCount, "恰一条");

            SimEvent e = _sink.Events[0];
            Assert.AreEqual(EventKind.HouseCallStarted, e.Kind);
            Assert.AreEqual(12, e.Tick, "写入调用 tick");
            Assert.AreEqual(PatientId.None.Value, e.Patient.Value, "世界流事件 Patient = None(DC-4)");
            var payload = _decoder.Decode<HouseCallStartedPayload>(EventKind.HouseCallStarted, e.Payload);
            Assert.AreEqual(42, payload.TargetPatientId, "目标 = 急召链接的病人(AC-52-56 的 id 一致性)");
        }

        /// <summary>同 tick 第二次调用(导演循环重入)⇒ AlreadyStartedThisTick,零新增写入。</summary>
        [Test]
        public void test_ac5256_secondCallSameTick_refused()
        {
            BuildSummonedPatientInClinic();
            AddEntered(12, OutsideCell);
            Assert.AreEqual(HouseCallStartResult.Applied, _writer.TryStartAtTick(_stream, 12));

            // 生产语义:Append 后事件进权威流,调用方回填后再调 ⇒ 写者须看见自己刚写的。
            var streamAfter = new List<SimEvent>(_stream);
            streamAfter.AddRange(_sink.Events);

            Assert.AreEqual(HouseCallStartResult.AlreadyStartedThisTick,
                _writer.TryStartAtTick(streamAfter, 12));
            Assert.AreEqual(1, _sink.AppendCount, "同 tick 仍恰一条(一次性)");
        }

        /// <summary>跨出后立刻跨回再跨出(同 tick 覆盖语义,末条 entered 胜)⇒ 仍恰一条。</summary>
        [Test]
        public void test_ac5256_reenterAndOutSameTick_stillExactlyOne()
        {
            BuildSummonedPatientInClinic();
            AddEntered(12, OutsideCell);   // 第一次跨出
            AddEntered(12, ClinicCell);    // 跨回(覆盖语义:同 tick 后者胜)
            AddEntered(12, OutsideCell);   // 再跨出 —— 本 tick 的最终格

            Assert.AreEqual(HouseCallStartResult.Applied, _writer.TryStartAtTick(_stream, 12));
            Assert.AreEqual(1, _sink.AppendCount, "同 tick 首次跨出判据:仍恰一条(AC-52-56 后半)");
        }

        /// <summary>同一活跃目标期间**再次**跨出(后一 tick)⇒ 再写入(每次跨出 = 一次锚点,12.1 逐字;首定口径)。</summary>
        [Test]
        public void test_ac5256_recrossOutLaterTick_writesAgain()
        {
            BuildSummonedPatientInClinic();
            AddEntered(12, OutsideCell);
            Assert.AreEqual(HouseCallStartResult.Applied, _writer.TryStartAtTick(_stream, 12));

            var streamAfter = new List<SimEvent>(_stream);
            streamAfter.AddRange(_sink.Events);
            AddEntered(20, ClinicCell);
            AddEntered(21, OutsideCell);
            streamAfter.AddRange(_stream.GetRange(_stream.Count - 2, 2));

            Assert.AreEqual(HouseCallStartResult.Applied, _writer.TryStartAtTick(streamAfter, 21),
                "「每次跨出 = 一次锚点时刻」—— 不做 per-summons 去重(12.1 逐字)");
            Assert.AreEqual(2, _sink.AppendCount);
        }

        // ── AC-52-58:不触发面(医馆内 / 无目标 / 无跨格)+ checkpoint 门安全 ──

        /// <summary>医馆内移动(未跨出)⇒ NotCrossingOut,零写入。</summary>
        [Test]
        public void test_ac5258_moveInsideClinic_noStart()
        {
            _clinic.Cells.Add(new WorldPos(6, 0, 0));   // 医馆第二格
            BuildSummonedPatientInClinic();
            AddEntered(12, new WorldPos(6, 0, 0));

            Assert.AreEqual(HouseCallStartResult.NotCrossingOut, _writer.TryStartAtTick(_stream, 12));
            Assert.AreEqual(0, _sink.AppendCount, "医馆内不触发(AC-52-58 前半)");
        }

        /// <summary>无活跃目标时跨出医馆 ⇒ NoActiveTarget,零写入。</summary>
        [Test]
        public void test_ac5258_noTarget_noStart()
        {
            AddEntered(8, ClinicCell);
            AddEntered(12, OutsideCell);   // 跨出形态合法,但无急召

            Assert.AreEqual(HouseCallStartResult.NoActiveTarget, _writer.TryStartAtTick(_stream, 12));
            Assert.AreEqual(0, _sink.AppendCount, "无目标不触发(AC-52-58 前半)");
        }

        /// <summary>本 tick 无跨格事件 ⇒ NoCrossingThisTick,零写入。</summary>
        [Test]
        public void test_ac5258_noEnteredThisTick_noStart()
        {
            BuildSummonedPatientInClinic();   // 最后一条 entered @8
            Assert.AreEqual(HouseCallStartResult.NoCrossingThisTick, _writer.TryStartAtTick(_stream, 12));
            Assert.AreEqual(0, _sink.AppendCount, "本 tick 无跨格 ⇒ 无从谈「跨出」");
        }

        /// <summary>首格无前序(无从证明「从医馆跨出」)⇒ NotCrossingOut(fail-safe)。</summary>
        [Test]
        public void test_ac5258_firstCellNoPrevious_notCrossingOut()
        {
            AddSummons(10);
            AddOnset(10, 42);
            AddEntered(12, OutsideCell);   // 玩家的第一条 entered,无前序格

            Assert.AreEqual(HouseCallStartResult.NotCrossingOut, _writer.TryStartAtTick(_stream, 12));
            Assert.AreEqual(0, _sink.AppendCount);
        }

        /// <summary>他例 actor 的跨格事件不冒充玩家(按 ActorId 筛)。</summary>
        [Test]
        public void test_ac5258_otherActorCrossing_ignored()
        {
            BuildSummonedPatientInClinic();
            AddEntered(12, OutsideCell, actorId: 2);   // 别人跨出
            Assert.AreEqual(HouseCallStartResult.NoCrossingThisTick, _writer.TryStartAtTick(_stream, 12));
            Assert.AreEqual(0, _sink.AppendCount);
        }

        /// <summary>12.4 否证钉:HouseCallStarted 落流后问 checkpoint 门 ⇒ 门答「安全」
        /// (旅途段不构成 unsafe —— 锚点③ 不得自否决;NoPendingMedicalActionQuery 防呆注的运行时面)。</summary>
        [Test]
        public void test_ac5258_checkpointGateSafeAfterHouseCallStarted()
        {
            var stub = new ArmedStub { IsArmed = false };
            var gate = new NoPendingMedicalActionQuery();
            gate.Bind(stub);
            Assert.IsTrue(gate.InNarrativeContext(), "基线:Idle ⇒ 安全");

            BuildSummonedPatientInClinic();
            AddEntered(12, OutsideCell);
            Assert.AreEqual(HouseCallStartResult.Applied, _writer.TryStartAtTick(_stream, 12));

            Assert.IsTrue(gate.InNarrativeContext(),
                "12.4:出诊旅途段是安全段 —— 活跃出诊目标不并入 unsafe 集(否则锚点③ 诞生 tick 自否决)");
        }

        /// <summary>链接断裂由查询 fail-loud 传播到写者(写者不吞)。</summary>
        [Test]
        public void test_writer_brokenLink_propagatesFailLoud()
        {
            AddSummons(10);   // 急召无 onset
            AddEntered(8, ClinicCell);
            AddEntered(12, OutsideCell);
            Assert.Throws<InvalidOperationException>(() => _writer.TryStartAtTick(_stream, 12),
                "写者不吞链接异常 —— 流已坏须炸在调用点");
            Assert.AreEqual(0, _sink.AppendCount);
        }
    }
}
