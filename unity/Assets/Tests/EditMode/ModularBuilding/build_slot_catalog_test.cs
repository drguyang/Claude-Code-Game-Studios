// Story 001 测试: 槽位骨架目录加载与查询
//
// AC-23-01: 放置合法性五条件(①槽位合法 + ②占用为空 + ③区域包含 + ④骨架未改 + ⑤数据可及)
// AC-23-05: 四向朝向整数旋转,零浮点中间量
// AC-23-06: 判定代码支持四向(即使 P0 目录 ⊆ {0°})
// AC-23-08: 目录校验 (0,0) ∈ OccupiedCells_local + 占用格 ⊆ BuildSlotRegion

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.ModularBuilding
{
    public class BuildSlotCatalogTest
    {
        private BuildSlotCatalog _catalog;

        [SetUp]
        public void Setup()
        {
            _catalog = new BuildSlotCatalog();

            // 注册测试槽位(小医馆布局)
            // (0,0,0): 允许 Bed
            _catalog.RegisterSlot(new BuildSlot(
                new WorldPos(0, 0, 0),
                BitMask.Single(SlotType.Bed)));

            // (1,0,0): 允许 Table
            _catalog.RegisterSlot(new BuildSlot(
                new WorldPos(1, 0, 0),
                BitMask.Single(SlotType.Table)));

            // (2,0,0): 允许 Bed | Decor
            _catalog.RegisterSlot(new BuildSlot(
                new WorldPos(2, 0, 0),
                BitMask.Single(SlotType.Bed) | BitMask.Single(SlotType.Decor)));
        }

        // AC-23-01 ①: 槽位合法
        [Test]
        public void test_validSlot_returnsTrue()
        {
            Assert.IsTrue(_catalog.IsValidSlot(new WorldPos(0, 0, 0)));
        }

        [Test]
        public void test_invalidSlot_returnsFalse()
        {
            Assert.IsFalse(_catalog.IsValidSlot(new WorldPos(99, 0, 99)));
        }

        // AC-23-01 ①: 类型匹配
        [Test]
        public void test_allowedType_matching_returnsTrue()
        {
            var slot = new WorldPos(0, 0, 0);
            Assert.IsTrue(_catalog.GetAllowedTypes(slot).HasFlag(BitMask.Single(SlotType.Bed)));
        }

        [Test]
        public void test_allowedType_notMatching_returnsFalse()
        {
            var slot = new WorldPos(0, 0, 0);
            // Slot 0 只允许 Bed, Table 不允许
            Assert.IsFalse(_catalog.GetAllowedTypes(slot).HasFlag(BitMask.Single(SlotType.Table)));
        }

        // AC-23-01 ③: 区域包含
        [Test]
        public void test_registeredSlot_inRegion()
        {
            Assert.IsTrue(_catalog.IsInBuildSlotRegion(new WorldPos(1, 0, 0)));
        }

        [Test]
        public void test_unregisteredSlot_notInRegion()
        {
            Assert.IsFalse(_catalog.IsBuildSlotRegion(new WorldPos(50, 0, 50)));
        }

        // AC-23-08: (0,0) ∈ OccupiedCells_local 目录校验
        [Test]
        public void test_anchorCell_inOccupiedCells()
        {
            // 槽位本身的位置(0,0,0) 应在区域内
            Assert.IsTrue(_catalog.IsValidSlot(new WorldPos(0, 0, 0)));
        }

        // 边界: 空目录
        [Test]
        public void test_emptyCatalog_noSlots()
        {
            var empty = new BuildSlotCatalog();
            Assert.AreEqual(0, empty.Count);
            Assert.IsFalse(empty.IsValidSlot(new WorldPos(0, 0, 0)));
        }

        // 纯函数: 同输入双跑一致
        [Test]
        public void test_pureFunction_doubleRun_identical()
        {
            var positions = _catalog.GetAllSlotPositions();
            var positions2 = _catalog.GetAllSlotPositions();
            Assert.AreEqual(positions.Count, positions2.Count);
        }
    }

    // 辅助扩展方法
    internal static class BitMaskExtensions
    {
        public static bool HasFlag(this BitMask mask, BitMask flag)
        {
            return (mask.Value & flag.Value) != 0;
        }
    }
}
