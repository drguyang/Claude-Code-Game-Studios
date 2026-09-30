// Story 004 测试: World 合成 EffectiveWalkable + 占用变更
//
// AC-23-02: 重放重建 slot_occupied / structure_at / Overlay 与事件序列逐位一致
// AC-23-03: EffectiveWalkable = Nav.walkable ∧ ¬Overlay.blocked
// AC-23-04: slot_occupied 与 Overlay.blocked 恒一致(单一真相,无漂移)
// AC-23-10: 载荷全整数

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.ModularBuilding
{
    public class WorldTest
    {
        private WorldGeometry _geometry;
        private BuildSlotCatalog _catalog;
        private World _world;

        [SetUp]
        public void Setup()
        {
            // 创建 5×1×5 小世界
            _geometry = new WorldGeometry(5, 1, 5);
            _catalog = new BuildSlotCatalog();

            // 注册槽位: (0,0,0) Bed, (1,0,0) Table, (2,0,0) Bed
            _catalog.RegisterSlot(new BuildSlot(new WorldPos(0, 0, 0), BitMask.Single(SlotType.Bed)));
            _catalog.RegisterSlot(new BuildSlot(new WorldPos(1, 0, 0), BitMask.Single(SlotType.Table)));
            _catalog.RegisterSlot(new BuildSlot(new WorldPos(2, 0, 0), BitMask.Single(SlotType.Bed)));

            _world = new World(_geometry, _catalog);
        }

        // AC-23-03: EffectiveWalkable 合成
        [Test]
        public void test_effectiveWalkable_walkableAndFree()
        {
            // (3,0,3): Nav 默认 walkable=true, 未被占用
            Assert.IsTrue(_world.IsEffectivelyWalkable(new WorldPos(3, 0, 3)));
        }

        [Test]
        public void test_effectiveWalkable_occupied()
        {
            // 占用 (0,0,0)
            _world.OccupyCell(new WorldPos(0, 0, 0));

            // 被占用 => blocked => 不可走
            Assert.IsFalse(_world.IsEffectivelyWalkable(new WorldPos(0, 0, 0)));
        }

        [Test]
        public void test_effectiveWalkable_unwalkableNav()
        {
            // (999,0,999): 越界 => TerrainCell 默认 Walkable=false
            Assert.IsFalse(_world.IsEffectivelyWalkable(new WorldPos(999, 0, 999)));
        }

        // AC-23-04: 单一占用表双访问器一致
        [Test]
        public void test_singleOccupancySource_consistent()
        {
            _world.OccupyCell(new WorldPos(1, 0, 0));

            // slot_occupied 视图
            bool slotOccupied = _world.IsSlotOccupied(new WorldPos(1, 0, 0));

            // EffectiveWalkable 视图
            bool walkable = _world.IsEffectivelyWalkable(new WorldPos(1, 0, 0));

            // slot_occupied = true => blocked => walkable = false
            Assert.IsTrue(slotOccupied);
            Assert.IsFalse(walkable);
        }

        // 边界: 越界坐标安全返回
        [Test]
        public void test_outOfBounds_returnsFalse()
        {
            Assert.IsFalse(_world.IsSlotOccupied(new WorldPos(999, 0, 999)));
            Assert.IsFalse(_world.IsEffectivelyWalkable(new WorldPos(999, 0, 999)));
        }

        // 边界: 空占用表全部可走
        [Test]
        public void test_emptyOccupancy_allWalkable()
        {
            for (int x = 0; x < 5; x++)
            {
                for (int z = 0; z < 5; z++)
                {
                    Assert.IsTrue(_world.IsEffectivelyWalkable(new WorldPos(x, 0, z)),
                        $"({x},0,{z}) should be walkable when empty");
                }
            }
        }

        // 占用/释放原子性
        [Test]
        public void test_occupyThenFree_restoresWalkable()
        {
            var cell = new WorldPos(2, 0, 2);

            Assert.IsTrue(_world.IsEffectivelyWalkable(cell));

            _world.OccupyCell(cell);
            Assert.IsFalse(_world.IsEffectivelyWalkable(cell));

            _world.FreeCell(cell);
            Assert.IsTrue(_world.IsEffectivelyWalkable(cell));
        }

        // GetModuleAt: 无结构返回 -1
        [Test]
        public void test_getModuleAt_empty_returnsMinusOne()
        {
            Assert.AreEqual(-1, _world.GetModuleAt(new WorldPos(0, 0, 0)));
        }
    }
}
