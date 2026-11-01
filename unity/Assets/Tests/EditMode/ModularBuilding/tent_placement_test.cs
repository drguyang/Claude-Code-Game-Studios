// S22-8 甲案 · smoke `#13` 的「帐篷可放置」半 —— P0 唯一新建例外
//
// ▍为什么这条判据此前没有(S22-8 勘察)
//   `game-concept.md:697-698`(2026-09-20 改判)与 `modular-building.md` 规则六把形态**写死**了:
//   「落关卡工具在 `world_buildslots.json` **骨架外**预置的 `tent` 槽;`StructurePlaced` 照常发,
//   载荷形状不变」。裁定与形态都齐,**缺的是承接者** —— 23 的 story-001…007 无一张认领,
//   代码 `grep -rniE "\btent\b"` 曾是 0。
//
// ▍本件钉什么(全部走**真烘焙产物**,不自己造数据)
//   A `shippedData_hasTentSlotAndModule`:cooked 里恰有 1 个 Tent 槽(且**只允许 Tent**)+ 1 个 Tent 模块
//   B `tentPlacement_writesExactlyOneStructurePlaced`:五条件通过 ⇒ 放置 ⇒ **恰一条** `StructurePlaced`,
//     `Patient = None`,**骨架外零新事件**(世界流里没有第二种 Kind)
//   C `tentModule_onDecorSlot_isRejected`:负例 —— 帐篷放到非 Tent 槽 ⇒ `TypeMismatch`,**零 Append**
//
// ⚠️ 本件**不测**:序章剧情节拍(归 37/内容件)· `tent` 的外观(美术轮)· 自由建造(P1a)。
//
// 📖 只读外部夹具声明(test-standards 具名声明义务)
// ─────────────────────────────────────────────────────────
// 读仓库内**烘焙产物**(只读,零写盘):`Assets/DataCooked/` 下的
// `world_buildslots.cooked.bytes` 与 `build_modules.cooked.bytes`(经 `DataCookedDisk` + 生产 codec)。
// **为什么必须读真产物**:本条判据的对象就是「出货数据里能不能放下帐篷」;
// 内联一份假槽位表 ⇒ 改坏数据时夹具仍绿,正是本卡要消灭的形态。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Presentation;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.ModularBuilding
{
    /// <summary>帐篷(P0 唯一新建例外)可放置 —— 走真烘焙产物。</summary>
    [TestFixture]
    public sealed class TentPlacementTest
    {
        private BuildSlotCatalog _catalog;
        private BuildModulesCatalog _modules;
        private WorldBuildSlotsDataSet _slotsData;
        private readonly List<SimEvent> _events = new List<SimEvent>();

        [SetUp]
        public void SetUp()
        {
            _events.Clear();
            _slotsData = DataCookedDisk.LoadBuildSlots();
            _catalog = new BuildSlotCatalog();
            foreach (WorldBuildSlotRow row in _slotsData.Rows)
                _catalog.RegisterSlot(new BuildSlot(row.Cell, new BitMask(row.AllowedMask),
                                                   row.BakedInitial, row.ModuleId));

            _modules = new BuildModulesCatalog(DataCookedDisk.LoadBuildModules());
        }

        // ══════════════════════════════════════════════════════════════════
        // A 出货数据里有帐篷
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_shippedData_hasTentSlotAndModule()
        {
            var tentSlots = new List<WorldBuildSlotRow>();
            foreach (WorldBuildSlotRow row in _slotsData.Rows)
                if ((new BitMask(row.AllowedMask) & BitMask.Single(SlotType.Tent)) != new BitMask(0))
                    tentSlots.Add(row);

            Assert.AreEqual(1, tentSlots.Count,
                $"出货数据里应恰有 1 个 Tent 槽(实际 {tentSlots.Count})—— " +
                "0 是「帐篷没做」,>1 是「例外被读成了自由建造」");

            Assert.AreEqual(1 << 7, tentSlots[0].AllowedMask,
                "Tent 槽的掩码须**恰为** `1 << 7` —— 扩员若顺带放开了别的类型," +
                "等于给「P0 唯一例外」开了一个任意对;实际掩码 = " + tentSlots[0].AllowedMask);

            Assert.IsFalse(tentSlots[0].BakedInitial,
                "帐篷槽须 `baked_initial = false` —— 开档既有家具会合成到规则三的 `Overlay(0)`," +
                "那是「骨架内改造」而不是规则六的「骨架外新建」");

            int tentModuleId = -1;
            int tentModules = 0;
            foreach (int id in _modules.GetAllModuleIds())
            {
                ModuleDefinition? def = _modules.GetModuleDefinition(id);
                if (def.HasValue && def.Value.SlotType == SlotType.Tent) { tentModules++; tentModuleId = id; }
            }

            Assert.AreEqual(1, tentModules, $"模块目录里应恰有 1 个 Tent 模块(实际 {tentModules})");
            Assert.IsTrue(tentModuleId > 0, "Tent 模块须有正整数 id");
        }

        // ══════════════════════════════════════════════════════════════════
        // B 放置 ⇒ 恰一条 StructurePlaced
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_tentPlacement_writesExactlyOneStructurePlaced()
        {
            WorldPos anchor = FindTentSlot();
            int tentModuleId = FindTentModuleId();

            var checker = new PlaceableChecker(_catalog, _modules,
                new AlwaysStock(), new NeverOccupied(), new NobodyPresent());
            Assert.AreEqual(PlaceableResult.Success,
                checker.Check(tentModuleId, anchor, orientation: 0),
                "[前置] 帐篷模块 + 帐篷槽 ⇒ 五条件必须全过");

            var writer = new StructureWriter(new CapturingSink(_events),
                new StructureInstanceRegistry(), new PayloadEncoder(new InMemoryBlobPool()),
                _modules, occupancy: new NoopOccupancy(),
                dropWriter: TestDropWriter.Create(new CapturingSink(_events)));

            long id = writer.Place(anchor, tentModuleId, orientation: 0, variant: 0, tick: 100);

            Assert.GreaterOrEqual(id, 1L, "放置应返回真实 structure_id");
            Assert.AreEqual(1, _events.Count,
                $"一次帐篷放置 = **恰一条**世界流事件(实际 {_events.Count})—— " +
                "骨架外多出任何一条都意味着「新建」被做成了新机制");
            Assert.AreEqual(EventKind.StructurePlaced, _events[0].Kind);
            Assert.AreEqual(PatientId.None, _events[0].Patient,
                "世界级事件用 None 哨兵(不污染 max(patient_id) 高水位)");
            Assert.AreEqual(100L, _events[0].Tick, "事件 tick 取自调用方传入的 tick");

            Assert.IsTrue(_catalog.TryGetSlot(anchor, out BuildSlot after), "[守卫] 放置后槽位仍在");
            Assert.AreEqual(anchor, after.Position, "[守卫] 放置不改骨架(槽位坐标不变)");
        }

        // ══════════════════════════════════════════════════════════════════
        // C 负例:帐篷放到非 Tent 槽 ⇒ 拒 + 零 Append
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_tentModule_onDecorSlot_isRejected()
        {
            WorldPos decorAnchor = default;
            bool found = false;
            foreach (WorldBuildSlotRow row in _slotsData.Rows)
            {
                var mask = new BitMask(row.AllowedMask);
                if ((mask & BitMask.Single(SlotType.Decor)) != new BitMask(0)
                    && (mask & BitMask.Single(SlotType.Tent)) == new BitMask(0))
                { decorAnchor = row.Cell; found = true; break; }
            }

            Assert.IsTrue(found, "[前置] 数据里应有只接受 Decor 的槽");

            var checker = new PlaceableChecker(_catalog, _modules,
                new AlwaysStock(), new NeverOccupied(), new NobodyPresent());

            Assert.AreEqual(PlaceableResult.TypeMismatch,
                checker.Check(FindTentModuleId(), decorAnchor, orientation: 0),
                "帐篷放到非 Tent 槽 ⇒ 五条件的 ① 支必须拒");
            Assert.AreEqual(0, _events.Count, "被拒 ⇒ **零 Append**(意图 + 当下判定 + 拒绝零事件)");
        }

        // ══════════════════════════════════════════════════════════════════

        private WorldPos FindTentSlot()
        {
            foreach (WorldBuildSlotRow row in _slotsData.Rows)
                if ((new BitMask(row.AllowedMask) & BitMask.Single(SlotType.Tent)) != new BitMask(0))
                    return row.Cell;
            Assert.Fail("出货数据里找不到 Tent 槽 —— 帐篷数据被删了?");
            return default;
        }

        private int FindTentModuleId()
        {
            foreach (int id in _modules.GetAllModuleIds())
            {
                ModuleDefinition? def = _modules.GetModuleDefinition(id);
                if (def.HasValue && def.Value.SlotType == SlotType.Tent) return id;
            }
            Assert.Fail("模块目录里找不到 Tent 模块");
            return -1;
        }

        // ── 替身(IStockQuery / IOccupancyQuery / IPresenceQuery / IWorldOccupancy)──

        private sealed class AlwaysStock : IStockQuery
        {
            public bool HasStock(int moduleId) => true;
        }

        private sealed class NeverOccupied : IOccupancyQuery
        {
            public bool IsOccupied(WorldPos cell) => false;
        }

        private sealed class NobodyPresent : IPresenceQuery
        {
            public bool IsPresent(PatientId patientId) => false;
            public int PresentCount => 0;
            public bool IsPresentAt(WorldPos cell) => false;
            public IReadOnlyCollection<int> PresentPatientIds() => Array.Empty<int>();
        }

        private sealed class NoopOccupancy : IWorldOccupancy
        {
            public void OccupyCells(IEnumerable<WorldPos> cells) { }
            public void FreeCells(IEnumerable<WorldPos> cells) { }
        }

        private sealed class CapturingSink : IEventSink
        {
            private readonly List<SimEvent> _sink;
            public CapturingSink(List<SimEvent> sink) { _sink = sink; }
            public void Append(in SimEvent e) => _sink.Add(e);
        }
    }
}
