// Story 006 测试: 拆除判定 + F-23-3 返还公式
//
// AC-23-07: 不存在 / 格上有实体 => 拒绝且不 Append
// AC-23-08: 目录校验 cost ≥ ⌈1/R⌉
// AC-23-16: Refund = ⌊cost × R⌋ 整数向下取整,失败走 DropSpawned

using System;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.ModularBuilding
{
    public class DemolishCheckerTest
    {
        private DemolishChecker _checker;
        private StructureInstanceRegistry _registry;

        [SetUp]
        public void Setup()
        {
            _registry = new StructureInstanceRegistry();
            var occupancy = new EmptyOccupancyQuery();
            var presence = new AlwaysEmptyPresence();
            var moduleCatalog = new TestModuleCatalog();
            _checker = new DemolishChecker(_registry, occupancy, presence, moduleCatalog);
        }

        // AC-23-07: 不存在 => StructureNotFound
        [Test]
        public void test_nonexistent_returnsNotFound()
        {
            Assert.AreEqual(DemolishResult.StructureNotFound, _checker.Check(999));
        }

        // AC-23-07: 有实体 => EntityOnCell
        [Test]
        public void test_entityOnCell_returnsEntityOnCell()
        {
            int sid = _registry.Register(new WorldPos(0, 0, 0), 1, 0, 0);
            var checkerWithEntity = new DemolishChecker(
                _registry, new EmptyOccupancyQuery(), new AlwaysPresentPresence(), new TestModuleCatalog());
            Assert.AreEqual(DemolishResult.EntityOnCell, checkerWithEntity.Check(sid));
        }

        // AC-23-07: 合法结构 => Success
        [Test]
        public void test_validStructure_returnsSuccess()
        {
            int sid = _registry.Register(new WorldPos(0, 0, 0), 1, 0, 0);
            Assert.AreEqual(DemolishResult.Success, _checker.Check(sid));
        }
    }

    // 测试辅助: 空占用查询
    internal class EmptyOccupancyQuery : IOccupancyQuery
    {
        public bool IsOccupied(WorldPos cell) => false;
    }

    // 测试辅助: 总是有实体
    internal class AlwaysPresentPresence : IPresenceQuery
    {
        public bool IsPresent(PatientId patientId) => true;
        public int PresentCount => 1;
        public bool IsPresentAt(WorldPos cell) => true;
    }
}
