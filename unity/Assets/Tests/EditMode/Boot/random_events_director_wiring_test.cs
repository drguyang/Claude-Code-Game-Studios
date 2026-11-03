// 52 出诊启动接线轮 —— RandomEventsDirectorDriver + ClinicCellSource 的行为钉子
// (TD-randomevents-02 二十九订 ⚠ 接线义务①的兑现件,2026-10-25 三裁全取推荐)。
//
// 钉的安全属性:
//   ① **fail-safe 双空集**:生产形态(空 event_key 集 / 空医馆格集)下 HouseCallStarted
//      恒零写入 —— 「通电待信」态的运行时面(驱动类头注;两处数据回填前不许假触发);
//   ② **晚绑定一次**:未绑定 = no-op;BindPlayer 只许一次(装配错误即抛);
//   ③ **同源单点发放**:driver.ClinicCells == 服务袋实例(组合根同源断言的观测面)。
//
// 权威:GDD random-events.md 规则十二(12.1/12.2)· TD-randomevents-02 ·
//   PatientAppearedDriver(边沿驱动范式)· NoPendingMedicalActionQuery(晚绑定先例)

using System;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Boot
{
    public class RandomEventsDirectorWiringTest
    {
        private const int PlayerActorId = 1;
        private const int HouseCallEventKey = 7;
        private static readonly WorldPos ClinicCell = new WorldPos(5, 0, 0);
        private static readonly WorldPos OutsideCell = new WorldPos(9, 0, 0);

        private InMemoryBlobPool _pool;
        private PayloadEncoder _encoder;
        private PayloadDecoder _decoder;
        private RecordingSink _sink;
        private ClinicCellSource _clinicCells;
        private DaYiJingCheng.Sim.World.StructureInstanceRegistry _structures;
        private List<SimEvent> _stream;

        [SetUp]
        public void Setup()
        {
            _pool = new InMemoryBlobPool();
            _encoder = new PayloadEncoder(_pool);
            _decoder = new PayloadDecoder(_pool);
            _sink = new RecordingSink();
            _clinicCells = new ClinicCellSource();
            _structures = new DaYiJingCheng.Sim.World.StructureInstanceRegistry();
            _clinicCells.Bind(_structures, FakeBuildingCatalog.IsBuilding);
            _stream = new List<SimEvent>();
        }

        private sealed class RecordingSink : IEventSink
        {
            public int AppendCount;
            public readonly List<SimEvent> Events = new List<SimEvent>();
            public void Append(in SimEvent e) { AppendCount++; Events.Add(e); }
        }

        /// <summary>标准急召流:急召@10(出诊类 key7)+ 同 tick 病人 42 + 玩家在医馆(entered@8)。</summary>
        private void BuildSummonedPatientInClinic()
        {
            var summons = _encoder.Encode(EventKind.EventArrived,
                new EventArrivedPayload(HouseCallEventKey, 0, OutsideCell, -1));
            _stream.Add(new SimEvent(10, PatientId.None, -1, EventKind.EventArrived, summons));
            var onset = _encoder.Encode(EventKind.DiseaseOnset,
                new DiseaseOnsetPayload(10, 3, 42, 420, -1));
            _stream.Add(new SimEvent(10, PatientId.None, -1, EventKind.DiseaseOnset, onset));
            var entered = _encoder.Encode(EventKind.ActorCellEntered,
                new ActorCellEnteredPayload(PlayerActorId, ClinicCell, 8));
            _stream.Add(new SimEvent(8, PatientId.None, -1, EventKind.ActorCellEntered, entered));
        }

        private RandomEventsDirectorDriver NewDriver(int[] keys)
        {
            var d = new RandomEventsDirectorDriver(
                () => _stream, _sink, _encoder, _decoder, _clinicCells, keys);
            d.BindPlayer(PlayerActorId);
            return d;
        }

        /// <summary>派生源:结构表为空 ⇒ 恒 false;登记一条建筑件后命中(2026-11-03 契约改造)。</summary>
        [Test]
        public void test_clinicCellSource_emptyStructures_isFalse_thenRegisterHits()
        {
            Assert.AreEqual(0, _clinicCells.DerivedCellCount, "结构表为空 ⇒ 0 格");
            Assert.IsFalse(_clinicCells.IsClinicCell(ClinicCell), "空 ⇒ 医馆格判据恒 false(fail-safe)");

            RegisterBuildingAt(ClinicCell);
            Assert.AreEqual(1, _clinicCells.DerivedCellCount, "登记 1 条建筑件 ⇒ 1 格");
            Assert.IsTrue(_clinicCells.IsClinicCell(ClinicCell), "登记后命中");
            Assert.IsFalse(_clinicCells.IsClinicCell(OutsideCell), "集外仍 false");
        }

        /// <summary>⚠️ 家具**不算**建筑件(判据 = `furn_type == null`,不是 `SlotType`)。</summary>
        [Test]
        public void test_clinicCellSource_furniture_isNotClinicCell()
        {
            _structures.Register(ClinicCell, FakeBuildingCatalog.FurnitureModuleId, 0, 0);
            Assert.AreEqual(0, _clinicCells.DerivedCellCount,
                "家具件不算医馆地块 —— `SlotType` 粒度过粗(`Decor` 混装家具与非家具)");
            Assert.IsFalse(_clinicCells.IsClinicCell(ClinicCell));
        }

        /// <summary>登记一条建筑件到指定格(测试缝)。</summary>
        private void RegisterBuildingAt(WorldPos cell)
            => _structures.Register(cell, FakeBuildingCatalog.BuildingModuleId, 0, 0);

        /// <summary>最小模块目录:1 = 建筑件(`furn_type` null),2 = 家具件。</summary>
        private sealed class FakeBuildingCatalog : DaYiJingCheng.Sim.World.IModuleCatalog
        {
            public const int BuildingModuleId = 1;
            public const int FurnitureModuleId = 2;

            /// <summary>判据本体(`furn_type == null` 即建筑件)。</summary>
            public static bool IsBuilding(int moduleId) => moduleId == BuildingModuleId;

            public DaYiJingCheng.Sim.World.ModuleDefinition? GetModuleDefinition(int moduleId)
                => moduleId == BuildingModuleId || moduleId == FurnitureModuleId
                    ? new DaYiJingCheng.Sim.World.ModuleDefinition(
                        moduleId, DaYiJingCheng.Sim.World.SlotType.Decor,
                        new[] { new WorldPos(0, 0, 0) }, new[] { 0 }, refundable: 0, cost: 1,
                        buildItemKey: 1)
                    : (DaYiJingCheng.Sim.World.ModuleDefinition?)null;

            public bool ContainsModule(int moduleId)
                => moduleId == BuildingModuleId || moduleId == FurnitureModuleId;

            public System.Collections.Generic.IReadOnlyCollection<int> GetAllModuleIds()
                => new[] { BuildingModuleId, FurnitureModuleId };
        }

        /// <summary>未绑定玩家 ⇒ 边沿 no-op(fail-safe:装配未完成 = 不驱动,不是错误态)。</summary>
        [Test]
        public void test_director_unbound_onTickEdge_noop()
        {
            var d = new RandomEventsDirectorDriver(
                () => _stream, _sink, _encoder, _decoder, _clinicCells, new[] { HouseCallEventKey });
            Assert.IsFalse(d.IsPlayerBound);

            BuildSummonedPatientInClinic();
            var entered = _encoder.Encode(EventKind.ActorCellEntered,
                new ActorCellEnteredPayload(PlayerActorId, OutsideCell, 12));
            _stream.Add(new SimEvent(12, PatientId.None, -1, EventKind.ActorCellEntered, entered));

            Assert.DoesNotThrow(() => d.OnTickEdge(12));
            Assert.AreEqual(0, _sink.AppendCount, "未绑定 ⇒ 零写入");
            Assert.AreEqual(0, d.StartedCount);
        }

        /// <summary>BindPlayer:负 id 抛;重复绑定抛(只许一次 —— 玩家 id 单调铸造)。</summary>
        [Test]
        public void test_director_bindPlayer_guards()
        {
            var d = new RandomEventsDirectorDriver(
                () => _stream, _sink, _encoder, _decoder, _clinicCells, new[] { HouseCallEventKey });
            Assert.Throws<ArgumentOutOfRangeException>(() => d.BindPlayer(-1));
            d.BindPlayer(PlayerActorId);
            Assert.Throws<InvalidOperationException>(() => d.BindPlayer(PlayerActorId),
                "二次绑定 = 装配错误,须抛(NoPendingMedicalActionQuery.Bind 同格)");
            Assert.IsTrue(d.IsPlayerBound);
        }

        /// <summary>正路径:绑定 + 活跃目标 + 跨出 ⇒ 恰一条,StartedCount/LastResult 可观测。</summary>
        [Test]
        public void test_director_crossingWithActiveTarget_appliesOnce()
        {
            var d = NewDriver(new[] { HouseCallEventKey });
            RegisterBuildingAt(ClinicCell);   // 隔离地块变量(占位源默认空集)
            BuildSummonedPatientInClinic();
            var entered = _encoder.Encode(EventKind.ActorCellEntered,
                new ActorCellEnteredPayload(PlayerActorId, OutsideCell, 12));
            _stream.Add(new SimEvent(12, PatientId.None, -1, EventKind.ActorCellEntered, entered));

            d.OnTickEdge(12);
            Assert.AreEqual(1, d.StartedCount, "恰一条");
            Assert.AreEqual(HouseCallStartResult.Applied, d.LastResult);
            Assert.AreEqual(1, _sink.AppendCount);
            Assert.AreEqual(EventKind.HouseCallStarted, _sink.Events[0].Kind);
            Assert.AreEqual(12, _sink.Events[0].Tick);
        }

        /// <summary>同 tick 二次边沿调用 ⇒ 只驱动一次(驱动的 _lastDrivenTick 防御)。</summary>
        [Test]
        public void test_director_sameTickEdge_drivenOnce()
        {
            var d = NewDriver(new[] { HouseCallEventKey });
            RegisterBuildingAt(ClinicCell);   // 隔离地块变量(占位源默认空集)
            BuildSummonedPatientInClinic();
            var entered = _encoder.Encode(EventKind.ActorCellEntered,
                new ActorCellEnteredPayload(PlayerActorId, OutsideCell, 12));
            _stream.Add(new SimEvent(12, PatientId.None, -1, EventKind.ActorCellEntered, entered));

            d.OnTickEdge(12);
            d.OnTickEdge(12);
            Assert.AreEqual(1, d.StartedCount, "同 tick 边沿只驱动一次");
            Assert.AreEqual(1, _sink.AppendCount);
        }

        /// <summary>① 生产形态(空 event_key 集):有跨格也恒 NoActiveTarget、恒零写入 ——
        /// 「通电待信」态的运行时钉(37 回填 key 前不许假触发)。</summary>
        [Test]
        public void test_director_productionShape_emptyKeys_zeroWrites()
        {
            var d = NewDriver(System.Array.Empty<int>());
            BuildSummonedPatientInClinic();
            var entered = _encoder.Encode(EventKind.ActorCellEntered,
                new ActorCellEnteredPayload(PlayerActorId, OutsideCell, 12));
            _stream.Add(new SimEvent(12, PatientId.None, -1, EventKind.ActorCellEntered, entered));

            for (long t = 11; t <= 15; t++) d.OnTickEdge(t);
            Assert.AreEqual(0, _sink.AppendCount, "空 key 集 ⇒ 恒零写入(裁定③)");
            Assert.AreEqual(HouseCallStartResult.NoActiveTarget, d.LastResult);
        }

        /// <summary>① 生产形态(空医馆地块集):即便 key 集非空,「跨出」判定恒不满足 ——
        /// 空集 fail-safe 的第二面(23/24 回填地块前不许假触发)。</summary>
        [Test]
        public void test_director_emptyClinicCells_notCrossingOut_zeroWrites()
        {
            var d = NewDriver(new[] { HouseCallEventKey });   // key 集非空,隔离地块变量
            BuildSummonedPatientInClinic();
            var entered = _encoder.Encode(EventKind.ActorCellEntered,
                new ActorCellEnteredPayload(PlayerActorId, OutsideCell, 12));
            _stream.Add(new SimEvent(12, PatientId.None, -1, EventKind.ActorCellEntered, entered));

            d.OnTickEdge(12);
            Assert.AreEqual(0, _sink.AppendCount, "空地块集 ⇒ 前格非医馆 ⇒ NotCrossingOut(裁定②)");
            Assert.AreEqual(HouseCallStartResult.NotCrossingOut, d.LastResult);
        }

        /// <summary>③ 同源钉的观测面:driver 持有的医馆格源 == 装配注入的同一实例。</summary>
        [Test]
        public void test_director_clinicCells_sameInstanceAsInjected()
        {
            var d = new RandomEventsDirectorDriver(
                () => _stream, _sink, _encoder, _decoder, _clinicCells, new[] { HouseCallEventKey });
            Assert.AreSame(_clinicCells, d.ClinicCells,
                "同源义务:消费点必须持服务袋同一实例(IClinicCellSource 接口头注禁令)");
        }
    }
}
