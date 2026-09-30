// Story 002 测试: 放置五条件判定 + 四向整数旋转
//
// AC-23-01: 五条件 Placeable(m, anchor)
// AC-23-05: 四向整数旋转,零浮点
// AC-23-06: 判定代码支持四向(即使 P0 目录 ⊆ {0°})
// AC-23-08: 目录校验 (0,0) ∈ OccupiedCells_local

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.ModularBuilding
{
    public class PlaceableCheckerTest
    {
        private BuildSlotCatalog _catalog;
        private TestModuleCatalog _moduleCatalog;
        private PlaceableChecker _checker;

        [SetUp]
        public void Setup()
        {
            _catalog = new BuildSlotCatalog();
            _moduleCatalog = new TestModuleCatalog();

            // 注册槽位
            _catalog.RegisterSlot(new BuildSlot(new WorldPos(0, 0, 0), BitMask.Single(SlotType.Bed)));
            _catalog.RegisterSlot(new BuildSlot(new WorldPos(1, 0, 0), BitMask.Single(SlotType.Table)));
            _catalog.RegisterSlot(new BuildSlot(new WorldPos(2, 0, 0), BitMask.Single(SlotType.Bed) | BitMask.Single(SlotType.Decor)));

            // 注册模块(2×1 床,锚点(0,0,0)在占用格中)
            _moduleCatalog.RegisterModule(new ModuleDefinition(
                moduleId: 1,
                slotType: SlotType.Bed,
                localOccupancy: new List<WorldPos> { new WorldPos(0, 0, 0), new WorldPos(1, 0, 0) },
                validOrientations: new[] { 0, 90, 180, 270 },
                refundable: 1));

            // 注册模块(1×1 桌)
            _moduleCatalog.RegisterModule(new ModuleDefinition(
                moduleId: 2,
                slotType: SlotType.Table,
                localOccupancy: new List<WorldPos> { new WorldPos(0, 0, 0) },
                validOrientations: new[] { 0 },
                refundable: 1));

            _checker = new PlaceableChecker(_catalog, _moduleCatalog, new AlwaysHasStock(), new AlwaysEmptyPresence());
        }

        // AC-23-01 ①: 槽位合法 + 类型匹配
        [Test]
        public void test_validSlotAndType_returnsSuccess()
        {
            var result = _checker.Check(1, new WorldPos(0, 0, 0));
            Assert.AreEqual(PlaceableResult.Success, result);
        }

        [Test]
        public void test_invalidSlot_returnsSlotNotFound()
        {
            var result = _checker.Check(1, new WorldPos(99, 0, 99));
            Assert.AreEqual(PlaceableResult.SlotNotFound, result);
        }

        [Test]
        public void test_typeMismatch_returnsTypeMismatch()
        {
            // Slot(0,0,0) 只允许 Bed, module 2 是 Table
            var result = _checker.Check(2, new WorldPos(0, 0, 0));
            Assert.AreEqual(PlaceableResult.TypeMismatch, result);
        }

        // AC-23-01 ⑤: module_id 存在
        [Test]
        public void test_unknownModule_returnsModuleNotFound()
        {
            var result = _checker.Check(999, new WorldPos(0, 0, 0));
            Assert.AreEqual(PlaceableResult.ModuleNotFound, result);
        }

        // AC-23-05: 四向整数旋转
        [Test]
        public void test_rotation_0_identity()
        {
            var cells = PlaceableChecker.ComputeOccupiedCells(
                _moduleCatalog.GetModuleDefinition(1),
                new WorldPos(5, 0, 5),
                0);
            Assert.AreEqual(2, cells.Count);
            Assert.AreEqual(new WorldPos(5, 0, 5), cells[0]);
            Assert.AreEqual(new WorldPos(6, 0, 5), cells[1]);
        }

        [Test]
        public void test_rotation_90_swapsXY()
        {
            // (1,0,0) → (0,0,1) in XZ
            var cells = PlaceableChecker.ComputeOccupiedCells(
                _moduleCatalog.GetModuleDefinition(1),
                new WorldPos(5, 0, 5),
                90);
            Assert.AreEqual(2, cells.Count);
            Assert.AreEqual(new WorldPos(5, 0, 5), cells[0]); // anchor
            Assert.AreEqual(new WorldPos(5, 0, 6), cells[1]); // rotated (1,0) → (0,1)
        }

        [Test]
        public void test_rotation_180_inverts()
        {
            var cells = PlaceableChecker.ComputeOccupiedCells(
                _moduleCatalog.GetModuleDefinition(1),
                new WorldPos(5, 0, 5),
                180);
            Assert.AreEqual(new WorldPos(5, 0, 5), cells[0]);
            Assert.AreEqual(new WorldPos(4, 0, 5), cells[1]);
        }

        [Test]
        public void test_rotation_270_swapsBack()
        {
            var cells = PlaceableChecker.ComputeOccupiedCells(
                _moduleCatalog.GetModuleDefinition(1),
                new WorldPos(5, 0, 5),
                270);
            Assert.AreEqual(new WorldPos(5, 0, 5), cells[0]);
            Assert.AreEqual(new WorldPos(5, 0, 4), cells[1]);
        }

        // AC-23-06: 非法朝向抛异常
        [Test]
        public void test_invalidOrientation_throws()
        {
            Assert.Throws<ArgumentException>(() =>
                PlaceableChecker.ComputeOccupiedCells(
                    _moduleCatalog.GetModuleDefinition(1),
                    new WorldPos(0, 0, 0),
                    45));
        }

        // AC-23-08: (0,0) ∈ OccupiedCells_local
        [Test]
        public void test_anchorInLocalOccupancy()
        {
            var def = _moduleCatalog.GetModuleDefinition(1);
            Assert.IsTrue(def.ContainsAnchor());
        }
    }

    // 测试辅助: 总是有库存
    internal class AlwaysHasStock : IStockQuery
    {
        public bool HasStock(int moduleId) => true;
    }

    // 测试辅助: 总是无实体
    internal class AlwaysEmptyPresence : IPresenceQuery
    {
        public bool IsPresent(int actorId) => false;
        public int PresentCount => 0;
    }

    // 测试辅助: 模块目录
    internal class TestModuleCatalog : IModuleCatalog
    {
        private readonly Dictionary<int, ModuleDefinition> _modules = new Dictionary<int, ModuleDefinition>();

        public void RegisterModule(ModuleDefinition def)
        {
            _modules[def.ModuleId] = def;
        }

        public ModuleDefinition? GetModuleDefinition(int moduleId)
        {
            return _modules.TryGetValue(moduleId, out var def) ? def : null;
        }

        public bool ContainsModule(int moduleId) => _modules.ContainsKey(moduleId);
        public IReadOnlyCollection<int> GetAllModuleIds() => _modules.Keys;
    }
}
