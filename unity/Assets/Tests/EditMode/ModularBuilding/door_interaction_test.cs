// 2026-11-02 · `AC-4-22` 兑现轮 —— 门的开 / 合(4 选中 → 23 改状态 → 世界流)。
//
// 判据(逐条可证伪):
//   ① 关着的门挡住格子(`IsEffectivelyWalkable == false`);
//   ② 一次 toggle ⇒ **恰一条** `StructureModified`,`NewVariant = 1`,格子变可走;
//   ③ 再 toggle ⇒ 回到关(两条事件),格子再挡住 —— 证明不是「开门单向开关」;
//   ④ 非门结构(Decor)不得被 toggle 改掉(模块分类守卫);
//   ⑤ 不存在的 id ⇒ false 且零事件。
//
// ⚠️ 本件**不重跑** `StructureRestorer` —— 「`StructureModified` 读档重放恢复 variant」
//    已有夹具(`world_load_service_test` / `structure_restore_test`),本件只证「写出来的
//    就是那条 Kind + 载荷正确」,不复制第二份重放判据。

using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.ModularBuilding
{
    /// <summary>`StructureToggleDriver`(门档)+ `World.IsEffectivelyWalkable` 的门格分支。
    /// <para>⚠️ 类于 2026-11-02 由 `DoorInteractionDriver` 泛化并改名 —— 本行原写旧名,同批订正。</para></summary>
    public sealed class door_interaction_test
    {
        private const int DoorModuleId = 11;
        private const int DecorModuleId = 7;
        private static readonly WorldPos DoorCell = new WorldPos(2, 0, 2);

        private List<SimEvent> _events;
        private InMemoryBlobPool _pool;
        private World _world;
        private StructureWriter _writer;
        private StructureToggleDriver _driver;
        private long _tick;

        [SetUp]
        public void SetUp()
        {
            _events = new List<SimEvent>();
            _pool = new InMemoryBlobPool();

            var terrain = new TerrainCell[8, 1, 8];
            for (int x = 0; x < 8; x++)
                for (int z = 0; z < 8; z++)
                    terrain[x, 0, z] = new TerrainCell(true, 0);

            var poi = new int[64];
            var resource = new int[64];
            for (int i = 0; i < 64; i++) { poi[i] = -1; resource[i] = -1; }

            var catalog = new BuildSlotCatalog();
            catalog.RegisterSlot(new BuildSlot(
                DoorCell, BitMask.Single(SlotType.Door), bakedInitial: true, moduleId: DoorModuleId));

            _world = new World(new WorldGeometry(terrain, poi, resource), catalog);

            var modules = new FakeModuleCatalog(
                (DoorModuleId, SlotType.Door),
                (DecorModuleId, SlotType.Decor));

            _world.ApplyBakedInitial(modules);

            var sink = new Sink(_events);
            _writer = new StructureWriter(sink, _world.Structures, new PayloadEncoder(_pool),
                                          modules, _world, TestDropWriter.Create(sink, _pool));

            _tick = 10;
            _driver = new StructureToggleDriver(
                SlotType.Door,
                () => _world.Structures,
                () => _world.BaselineModuleCatalog,
                () => _writer,
                () => _tick);
        }

        private T PayloadOf<T>(SimEvent evt) where T : struct
        {
            Assert.IsTrue(PayloadCodec.TryGetPayload(evt, _pool, out T p),
                $"{evt.Kind}: 载荷须能经池 + codec 取回(字节真的进了池)");
            return p;
        }

        // ══════════════ ① 关着的门挡路 ══════════════

        [Test]
        public void test_closedDoor_blocksCell()
        {
            Assert.AreEqual(1, _world.BakedInitialStructureCount, "前置:基线须落 1 扇门");
            Assert.IsFalse(_world.IsEffectivelyWalkable(DoorCell),
                "关着的门(基线 variant = 0)须挡住该格");
        }

        // ══════════════ ② 开 ══════════════

        [Test]
        public void test_toggle_opensDoor_writesExactlyOneModify()
        {
            bool ok = _driver.Toggle(1L);

            Assert.IsTrue(ok, "门存在 ⇒ toggle 须成功");
            Assert.AreEqual(1, _events.Count, "恰一条事件");
            Assert.AreEqual(EventKind.StructureModified, _events[0].Kind,
                "门的开合 = `StructureModified`(不新开 Kind)");

            StructureModifiedPayload p = PayloadOf<StructureModifiedPayload>(_events[0]);
            Assert.AreEqual(1L, p.StructureId);
            Assert.AreEqual(1, p.NewVariant, "0 → 1(开)");
            Assert.AreEqual(2, p.ModifiedFields, "modified_fields = 变体位(0b10)");
            Assert.IsTrue(_world.IsEffectivelyWalkable(DoorCell), "开了 ⇒ 可走");
        }

        // ══════════════ ③ 再合 ══════════════

        [Test]
        public void test_toggleTwice_closesAgain()
        {
            Assert.IsTrue(_driver.Toggle(1L));
            _tick = 11;
            Assert.IsTrue(_driver.Toggle(1L));

            Assert.AreEqual(2, _events.Count, "两次 toggle ⇒ 两条事件");
            StructureModifiedPayload second = PayloadOf<StructureModifiedPayload>(_events[1]);
            Assert.AreEqual(0, second.NewVariant, "1 → 0(关)");
            Assert.IsFalse(_world.IsEffectivelyWalkable(DoorCell), "合上 ⇒ 又挡住");
        }

        // ══════════════ ④ 非门不得被改 ══════════════

        [Test]
        public void test_toggle_nonDoorStructure_isRejected()
        {
            // 玩家放一件 Decor(id = 基线数 + 1)
            var decorCatalog = new FakeModuleCatalog(
                (DoorModuleId, SlotType.Door), (DecorModuleId, SlotType.Decor));
            long decorId = PlaceDecor(decorCatalog);
            _events.Clear();

            Assert.IsFalse(_driver.Toggle(decorId), "非门结构不得被 toggle 改掉");
            Assert.AreEqual(0, _events.Count, "零事件(不得写出无关的 StructureModified)");
        }

        // ══════════════ ⑤ 类型守卫(门 / 开关共用驱动器后新增)══════════════

        /// <summary>开关驱动器**不得**切一扇门 —— 反之亦然(`expectedType` 的判别力)。</summary>
        /// <remarks>🔁 2026-11-02(`TD-interaction-03` 收口轮):`DoorInteractionDriver` 泛化为
        /// `StructureToggleDriver(expectedType)` 后引入的性质;缺本条 ⇒ 两类结构可互相改写。</remarks>
        [Test]
        public void test_switchDriver_rejectsDoor()
        {
            var switchDriver = new StructureToggleDriver(
                SlotType.Switch,
                () => _world.Structures,
                () => _world.BaselineModuleCatalog,
                () => _writer,
                () => _tick);

            Assert.IsFalse(switchDriver.Toggle(1L), "开关驱动器不得改门的 variant");
            Assert.AreEqual(0, _events.Count, "零事件");
            Assert.IsFalse(_world.IsEffectivelyWalkable(DoorCell), "门仍关着(未被误改)");
        }

        // ══════════════ ⑥ 不存在的 id ══════════════

        [Test]
        public void test_toggle_unknownId_returnsFalse_noEvent()
        {
            Assert.IsFalse(_driver.Toggle(999L));
            Assert.AreEqual(0, _events.Count);
        }

        // ══════════════ 夹具 ══════════════

        /// <summary>在槽位外放一件 Decor(走 23 的写者,拿到一个非门 id)。</summary>
        private long PlaceDecor(IModuleCatalog modules)
        {
            var sink = new Sink(_events);
            var writer = new StructureWriter(sink, _world.Structures, new PayloadEncoder(_pool),
                                             modules, _world, TestDropWriter.Create(sink, _pool));
            return writer.Place(new WorldPos(5, 0, 5), DecorModuleId, orientation: 0, variant: 0, tick: 9);
        }

        private sealed class Sink : IEventSink
        {
            private readonly List<SimEvent> _list;
            public Sink(List<SimEvent> list) { _list = list; }
            public void Append(in SimEvent e) => _list.Add(e);
        }

        private sealed class FakeModuleCatalog : IModuleCatalog
        {
            private readonly Dictionary<int, ModuleDefinition> _byId =
                new Dictionary<int, ModuleDefinition>();

            public FakeModuleCatalog(params (int ModuleId, SlotType Type)[] rows)
            {
                foreach ((int id, SlotType type) in rows)
                    _byId[id] = new ModuleDefinition(
                        id, type, new[] { new WorldPos(0, 0, 0) }, new[] { 0 },
                        cost: 1, buildItemKey: 8);
            }

            public ModuleDefinition? GetModuleDefinition(int moduleId)
                => _byId.TryGetValue(moduleId, out ModuleDefinition d) ? d : (ModuleDefinition?)null;

            public bool ContainsModule(int moduleId) => _byId.ContainsKey(moduleId);

            public IReadOnlyCollection<int> GetAllModuleIds() => new List<int>(_byId.Keys);
        }
    }
}
