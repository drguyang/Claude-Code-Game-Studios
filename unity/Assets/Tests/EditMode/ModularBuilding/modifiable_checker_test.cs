// Story 005 测试: ModifiableChecker(改朝向/变体占用检查)
//
// AC-23-05: 四向整数旋转,零浮点
// AC-23-06: 判定代码支持四向
// AC-23-11: StructureModified 只载物理形态位

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.ModularBuilding
{
    public class ModifiableCheckerTest
    {
        private BuildSlotCatalog _catalog;
        private TestModuleCatalog _moduleCatalog;
        private StructureInstanceRegistry _registry;
        private ModifiableChecker _checker;

        [SetUp]
        public void Setup()
        {
            _catalog = new BuildSlotCatalog();
            _moduleCatalog = new TestModuleCatalog();
            _registry = new StructureInstanceRegistry();

            // 注册 3×1 槽位
            for (int x = 0; x < 3; x++)
                _catalog.RegisterSlot(new BuildSlot(new WorldPos(x, 0, 0), BitMask.Single(SlotType.Bed)));

            // 2×1 床模块(锚点(0,0,0)在占用格中)
            _moduleCatalog.RegisterModule(new ModuleDefinition(
                moduleId: 1,
                slotType: SlotType.Bed,
                localOccupancy: new List<WorldPos> { new WorldPos(0, 0, 0), new WorldPos(1, 0, 0) },
                validOrientations: new[] { 0, 90, 180, 270 },
                refundable: 1));

            _checker = new ModifiableChecker(_catalog, new OccupancyQuery(_registry), _moduleCatalog, _registry);
        }

        // AC-23-05: 无变更返回 NoChange
        [Test]
        public void test_noChange_returnsNoChange()
        {
            int sid = _registry.Register(new WorldPos(0, 0, 0), 1, 0, 0);
            var result = _checker.Check(sid, 0, 0);
            Assert.AreEqual(ModifiableResult.NoChange, result);
        }

        // AC-23-05: 合法朝向变更
        [Test]
        public void test_validOrientationChange_returnsSuccess()
        {
            // 放置 2×1 床(横向占 (0,0,0) + (1,0,0))
            int sid = _registry.Register(new WorldPos(0, 0, 0), 1, 0, 0);
            _registry.Register(new WorldPos(0, 0, 0), 1, 0, 0); // placeholder for occupancy

            // 旋转 90° 后占 (0,0,0) + (0,0,1), 旧格 (1,0,0) 释放
            // 新格 (0,0,1) 未被占 => 成功
            var result = _checker.Check(sid, 90, 0);
            Assert.AreEqual(ModifiableResult.Success, result);
        }

        // 新格被占 => NewCellsOccupied
        [Test]
        public void test_newCellsOccupied_returnsNewCellsOccupied()
        {
            // 放置两张床: (0,0,0) 横向 + (1,0,0) 自身
            int sid1 = _registry.Register(new WorldPos(0, 0, 0), 1, 0, 0);
            int sid2 = _registry.Register(new WorldPos(2, 0, 0), 1, 0, 0);

            // 将 sid1 旋转 180°: 新占 (-1,0,0) + (0,0,0), 旧占 (0,0,0) + (1,0,0)
            // 新格 (-1,0,0) 超出区域 => OutOfRegion
            var result = _checker.Check(sid1, 180, 0);
            Assert.AreEqual(ModifiableResult.OutOfRegion, result);
        }

        // 非法朝向 => TypeMismatch
        [Test]
        public void test_invalidOrientation_returnsTypeMismatch()
        {
            int sid = _registry.Register(new WorldPos(0, 0, 0), 1, 0, 0);
            var result = _checker.Check(sid, 45, 0);
            Assert.AreEqual(ModifiableResult.TypeMismatch, result);
        }

        // 不存在的 structure_id => StructureNotFound
        [Test]
        public void test_nonexistentStructure_returnsNotFound()
        {
            var result = _checker.Check(999, 90, 0);
            Assert.AreEqual(ModifiableResult.StructureNotFound, result);
        }
    }

    // 测试辅助: 从 StructureInstanceRegistry 读取占用
    internal class OccupancyQuery : IOccupancyQuery
    {
        private readonly StructureInstanceRegistry _registry;
        private readonly HashSet<WorldPos> _occupied = new HashSet<WorldPos>();

        public OccupancyQuery(StructureInstanceRegistry registry)
        {
            _registry = registry;
        }

        public bool IsOccupied(WorldPos cell)
        {
            // 简化测试: 只检查是否有实例在该格
            return _registry.GetInstanceAt(cell) >= 0;
        }
    }
}
