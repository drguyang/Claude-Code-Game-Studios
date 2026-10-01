// build_slot_catalog_test.cs
//
// AC-23-01: 槽位合法性判定(位置 + 类型位掩码)
// AC-23-05: 未知位置 ⇒ 查询安全返回空/默认,不抛异常

using System;
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
            // 注册测试槽位:位置(0,0,0),允许类型 0b0011 (Bed=1, Table=2)
            _catalog.RegisterSlot(new BuildSlot(new WorldPos(0, 0, 0), 0b0011));
        }

        // AC-23-01: 已注册槽位 ⇒ IsValidSlot 返回 true
        [Test]
        public void test_registeredSlot_returnsTrue()
        {
            Assert.IsTrue(_catalog.IsValidSlot(new WorldPos(0, 0, 0)),
                "已注册槽位必须返回 true");
        }

        // AC-23-05: 未知位置 ⇒ IsValidSlot 返回 false
        [Test]
        public void test_unknownPosition_returnsFalse()
        {
            Assert.IsFalse(_catalog.IsValidSlot(new WorldPos(9999, 0, 9999)),
                "未知位置必须返回 false");
        }

        // AC-23-05: 未知位置 ⇒ GetAllowedTypes 返回 0
        [Test]
        public void test_unknownPosition_returnsZero()
        {
            int types = _catalog.GetAllowedTypes(new WorldPos(9999, 0, 9999));
            Assert.AreEqual(0, types, "未知位置必须返回 0");
        }

        // AC-23-01: 已注册槽位 ⇒ GetAllowedTypes 返回正确的位掩码
        [Test]
        public void test_registeredSlot_returnsCorrectMask()
        {
            int types = _catalog.GetAllowedTypes(new WorldPos(0, 0, 0));
            Assert.AreEqual(0b0011, types, "已注册槽位必须返回正确的类型位掩码");
        }

        // AC-23-01: TryGetSlot 对已注册槽位返回 true 并输出槽位
        [Test]
        public void test_tryGetSlot_registered_returnsTrue()
        {
            bool found = _catalog.TryGetSlot(new WorldPos(0, 0, 0), out var slot);
            Assert.IsTrue(found, "已注册槽位必须被找到");
            Assert.AreEqual(0b0011, slot.AllowedTypes, "槽位类型必须匹配");
        }

        // AC-23-05: TryGetSlot 对未知位置返回 false
        [Test]
        public void test_tryGetSlot_unknown_returnsFalse()
        {
            bool found = _catalog.TryGetSlot(new WorldPos(9999, 0, 9999), out _);
            Assert.IsFalse(found, "未知位置必须返回 false");
        }

        // AC-23-05: 未知位置 ⇒ IsInBuildSlotRegion 返回 false
        [Test]
        public void test_unknownPosition_notInRegion()
        {
            Assert.IsFalse(_catalog.IsInBuildSlotRegion(new WorldPos(9999, 0, 9999)),
                "未知位置不在建造区域内");
        }
    }
}
