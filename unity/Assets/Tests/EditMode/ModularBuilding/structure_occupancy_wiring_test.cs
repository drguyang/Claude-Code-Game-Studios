// 评审 C1/C2 的判据 —— 占用格集同源 + Overlay 写路径接线(2026-10-03 补)
//
// 权威来源:
//   F-23-1  EffectiveWalkable(cell) := Nav[cell].walkable ∧ ¬slot_occupied(cell)
//   F-23-2b 整数旋转足迹(PlaceableChecker.ComputeOccupiedCells)
//   评审 C1:Register 的占用格集**恒 = {anchor}** ⇒ 多格模块非锚点足迹格不参与拆除检查
//   评审 C2:World.OccupyCells/FreeCells **零生产调用方** ⇒ Overlay 写路径未接线
//
// ⚠️ 本文件补的是**集成判据** —— 既有测试全为单元级(直调 World.OccupyCells 或
//    不传 moduleCatalog),**掩盖了**这两条缺陷。

using System;   // InvalidOperationException(N-r1 fail-closed 判据)
using System.Collections.Generic;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.ModularBuilding
{
    public class StructureOccupancyWiringTest
    {
        private InMemoryBlobPool _pool;
        private PayloadEncoder _encoder;
        private StructureInstanceRegistry _registry;
        private List<SimEvent> _events;

        /// <summary>占两格的模块:L 形(锚点 + 相邻一格)—— 使「非锚点足迹」可观测。</summary>
        private static readonly WorldPos[] LShapeLocal = { new WorldPos(0, 0, 0), new WorldPos(1, 0, 0) };

        private sealed class StubCatalog : IModuleCatalog
        {
            private readonly Dictionary<int, ModuleDefinition> _defs = new Dictionary<int, ModuleDefinition>();
            public void Add(int id, WorldPos[] local) =>
                _defs[id] = new ModuleDefinition(id, SlotType.Decor, local,
                                                 new[] { 0, 90, 180, 270 });
            public ModuleDefinition? GetModuleDefinition(int moduleId) =>
                _defs.TryGetValue(moduleId, out var d) ? d : (ModuleDefinition?)null;
            public bool ContainsModule(int moduleId) => _defs.ContainsKey(moduleId);
            public IReadOnlyCollection<int> GetAllModuleIds() => _defs.Keys;
        }

        [SetUp]
        public void Setup()
        {
            _pool = new InMemoryBlobPool();
            _encoder = new PayloadEncoder(_pool);
            _registry = new StructureInstanceRegistry();
            _events = new List<SimEvent>();
        }

        // ══════════ C1:占用格集 = 真足迹(与放置判定同源)══════════

        [Test]
        public void test_c1_register_usesFullFootprint_notAnchorOnly()
        {
            var catalog = new StubCatalog();
            catalog.Add(7, LShapeLocal);
            var writer = new StructureWriter(new CapturingSink(_events), _registry, _encoder,
                                             catalog, new NoopOccupancy());

            int sid = writer.Place(new WorldPos(10, 0, 10), moduleId: 7, orientation: 0, variant: 0, tick: 1);

            Assert.IsTrue(_registry.TryGet(sid, out var inst));
            Assert.AreEqual(2, inst.OccupiedCells.Count,
                "🔴 C1:两格模块的占用集须为 **2**(原实现恒 = {{anchor}} ⇒ 此断言红)");
            CollectionAssert.Contains(new List<WorldPos>(inst.OccupiedCells), new WorldPos(10, 0, 10));
            CollectionAssert.Contains(new List<WorldPos>(inst.OccupiedCells), new WorldPos(11, 0, 10));
        }

        [Test]
        public void test_c1_footprintMatchesPlaceableChecker()
        {
            // 同源性:Register 用的足迹须 == PlaceableChecker 对同一模块/朝向算出的足迹
            var catalog = new StubCatalog();
            catalog.Add(7, LShapeLocal);
            var writer = new StructureWriter(new CapturingSink(_events), _registry, _encoder,
                                             catalog, new NoopOccupancy());

            var anchor = new WorldPos(3, 0, 4);
            int sid = writer.Place(anchor, 7, orientation: 90, variant: 0, tick: 1);
            _registry.TryGet(sid, out var inst);

            var expected = PlaceableChecker.ComputeOccupiedCells(
                catalog.GetModuleDefinition(7).Value, anchor, 90);
            CollectionAssert.AreEquivalent(new List<WorldPos>(expected),
                new List<WorldPos>(inst.OccupiedCells),
                "C1:实例表足迹须与放置判定**同源**(同一函数)");
        }

        // ══════════ C2:Overlay 写路径接线 ══════════

        [Test]
        public void test_c2_place_writesOccupancy()
        {
            var catalog = new StubCatalog();
            catalog.Add(7, LShapeLocal);
            var occupancy = new SpyOccupancy();
            var writer = new StructureWriter(new CapturingSink(_events), _registry, _encoder,
                                             catalog, occupancy);

            writer.Place(new WorldPos(10, 0, 10), 7, 0, 0, tick: 1);

            Assert.AreEqual(1, occupancy.OccupyCalls,
                "🔴 C2:Place 须调用 OccupyCells(原实现**零调用** ⇒ Overlay 写路径未接线)");
            Assert.AreEqual(2, occupancy.LastOccupied.Count, "须占用**完整足迹**两格");
        }

        [Test]
        public void test_c2_remove_freesOccupancy()
        {
            var catalog = new StubCatalog();
            catalog.Add(7, LShapeLocal);
            var occupancy = new SpyOccupancy();
            var writer = new StructureWriter(new CapturingSink(_events), _registry, _encoder,
                                             catalog, occupancy);

            int sid = writer.Place(new WorldPos(10, 0, 10), 7, 0, 0, tick: 1);
            writer.Remove(sid, tick: 2);

            Assert.AreEqual(1, occupancy.FreeCalls, "C2:Remove 须调用 FreeCells");
            Assert.AreEqual(2, occupancy.LastFreed.Count, "须释放**完整足迹**");
        }

        // ══════════ N-r1:fail-closed(第二轮评审补)══════════

        [Test]
        public void test_nr1_noCatalog_throwsFailClosed()
        {
            // ⚠️ **2026-10-03 反转**:初版本测名为 `noCatalog_noOccupancy_noThrow`,
            //    断言「不抛」—— 那等于把 **C1/C2 的静默退化路径合法化**(第二轮评审 N-r1)。
            //    现改为 **fail-closed**:无目录 ⇒ 抛。
            var writer = new StructureWriter(new CapturingSink(_events), _registry, _encoder);
            var ex = Assert.Throws<InvalidOperationException>(
                () => writer.Place(new WorldPos(1, 0, 1), 7, 0, 0, tick: 1),
                "未注入 IModuleCatalog ⇒ 须 fail-closed(静默兜底 {anchor} = C1 复现)");
            StringAssert.Contains("ModuleCatalog", ex.Message);
        }

        [Test]
        public void test_nr1_unknownModuleId_throwsFailClosed()
        {
            // moduleId 不在目录 ⇒ 不得拿 {anchor} 冒充真足迹
            var catalog = new StubCatalog();   // 空目录
            var writer = new StructureWriter(new CapturingSink(_events), _registry, _encoder,
                                             catalog, new NoopOccupancy());
            Assert.Throws<InvalidOperationException>(
                () => writer.Place(new WorldPos(1, 0, 1), 999, 0, 0, tick: 1),
                "moduleId 不在目录 ⇒ fail-closed");
        }

        [Test]
        public void test_nr1_noOccupancy_throwsFailClosed()
        {
            // 有目录但无 occupancy ⇒ 不得静默跳过 Overlay 写(C2)
            var catalog = new StubCatalog();
            catalog.Add(7, LShapeLocal);
            // ⚠️ 本用例**刻意不传** occupancy(上一轮我的批量替换误加了 `NoopOccupancy`,
            //    致该测失去判据 —— 已修正)
            var writer = new StructureWriter(new CapturingSink(_events), _registry, _encoder,
                                             catalog);
            var ex = Assert.Throws<InvalidOperationException>(
                () => writer.Place(new WorldPos(1, 0, 1), 7, 0, 0, tick: 1),
                "未注入 IWorldOccupancy ⇒ 须 fail-closed(C2:Overlay 不写)");
            StringAssert.Contains("WorldOccupancy", ex.Message);
        }

        // ══════════ 辅助 ══════════

        private sealed class CapturingSink : IEventSink
        {
            private readonly List<SimEvent> _sink;
            public CapturingSink(List<SimEvent> sink) { _sink = sink; }
            public void Append(in SimEvent e) => _sink.Add(e);
        }

        private sealed class NoopOccupancy : IWorldOccupancy
        {
            public void OccupyCells(IEnumerable<WorldPos> cells) { }
            public void FreeCells(IEnumerable<WorldPos> cells) { }
        }

        private sealed class SpyOccupancy : IWorldOccupancy
        {
            public int OccupyCalls, FreeCalls;
            public IReadOnlyList<WorldPos> LastOccupied = new List<WorldPos>();
            public IReadOnlyList<WorldPos> LastFreed = new List<WorldPos>();
            public void OccupyCells(IEnumerable<WorldPos> cells)
            {
                OccupyCalls++;
                LastOccupied = new List<WorldPos>(cells);
            }
            public void FreeCells(IEnumerable<WorldPos> cells)
            {
                FreeCalls++;
                LastFreed = new List<WorldPos>(cells);
            }
        }
    }
}
