// 2026-11-02 · `AC-4-22` —— 第四源的生产实装(`BakedInitialSource`)。
//
// 判据(逐条可证伪):
//   ① 基线号段内、模块 `SlotType.Door` 的实例 ⇒ 进 `Doors`(格 + structure_id 原样);
//   ② 号段外的实例(玩家新建)⇒ **不进**(第四源 = 开档既有、无流事件);
//   ③ 非门的模块 ⇒ 不进(分类依据 = 模块目录的 SlotType,不是 "variant 看起来像门");
//   ④ 容器切片原样透传(instance_id 空间 —— 与门的 structure_id 空间**不同**);
//   ⑤ 依赖缺失 ⇒ 空集,不抛(诚实降级:未接世界时没有第四源可报)。

using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Interaction;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Interaction
{
    /// <summary>第四源的结构分类与容器透传。</summary>
    public sealed class baked_initial_source_test
    {
        private const int DoorModule = 11;
        private const int DecorModule = 7;
        private const int UtensilModule = 12;
        private const int ClinicPanelModule = 13;
        private const int SwitchModule = 14;

        private static IModuleCatalog Catalog() => new FakeCatalog(
            (DoorModule, SlotType.Door),
            (DecorModule, SlotType.Decor),
            (UtensilModule, SlotType.Utensil),
            (ClinicPanelModule, SlotType.ClinicPanel),
            (SwitchModule, SlotType.Switch));

        private static StructureInstanceRegistry Registry()
        {
            var reg = new StructureInstanceRegistry();
            var nullCells = new[] { new WorldPos(0, 0, 0) };

            // id 1..5 = 基线(开档自带);id 6 = 玩家新建(号段外)
            reg.Restore(1, new WorldPos(5, 0, 5), DoorModule, 0, 0, nullCells);
            reg.Restore(2, new WorldPos(6, 0, 6), DecorModule, 0, 0, nullCells);
            reg.Restore(3, new WorldPos(10, 0, 5), UtensilModule, 0, 0, nullCells);
            reg.Restore(4, new WorldPos(5, 0, 9), ClinicPanelModule, 0, 0, nullCells);
            reg.Restore(5, new WorldPos(2, 0, 4), SwitchModule, 0, 0, nullCells);
            reg.Restore(6, new WorldPos(7, 0, 7), DoorModule, 0, 0, nullCells);
            return reg;
        }

        private static BakedInitialSource Source(
            StructureInstanceRegistry reg, int baseline = 5,
            IReadOnlyList<ContainerDto> containers = null)
            => new BakedInitialSource(
                instancesOf: () => reg.EnumerateInstances(),
                modulesOf: Catalog,
                baselineCountOf: () => baseline,
                containersOf: () => containers ?? new List<ContainerDto>());

        // ══════════════ ① / ② 基线号段 ══════════════

        [Test]
        public void test_doors_baselineDoor_isIncluded_playerBuiltDoor_isNot()
        {
            IReadOnlyList<DoorDto> doors = Source(Registry()).Doors;

            Assert.AreEqual(1, doors.Count, "只有基线号段内的门");
            Assert.AreEqual(1L, doors[0].StructureId, "玩家新建的门(id 6)不属第四源");
            Assert.AreEqual(new WorldPos(5, 0, 5), doors[0].Cell);
        }

        // ══════════════ ③ 模块分类 ══════════════

        [Test]
        public void test_doors_nonDoorModule_isExcluded()
        {
            IReadOnlyList<DoorDto> doors = Source(Registry()).Doors;

            foreach (DoorDto d in doors)
                Assert.AreNotEqual(2L, d.StructureId, "Decor 模块不得被当成门");
        }

        // ══════════════ ④ 容器透传 ══════════════

        [Test]
        public void test_containers_passthroughWithInstanceId()
        {
            var containers = new List<ContainerDto>
            {
                new ContainerDto(new WorldPos(5, 0, 5), 1L),
            };

            IReadOnlyList<ContainerDto> got = Source(Registry(), containers: containers).Containers;

            Assert.AreEqual(1, got.Count);
            Assert.AreEqual(1L, got[0].InstanceId, "容器走 instance_id(不是 structure_id)");
            Assert.AreEqual(new WorldPos(5, 0, 5), got[0].Cell);
        }

        // ══════════════ ④之二 三种新切片(2026-11-02 · TD-interaction-03 收口)══════════════

        [Test]
        public void test_utensils_baselineUtensil_isIncluded()
        {
            IReadOnlyList<UtensilDto> got = Source(Registry()).Utensils;

            Assert.AreEqual(1, got.Count);
            Assert.AreEqual(3L, got[0].StructureId);
            Assert.AreEqual(new WorldPos(10, 0, 5), got[0].Cell);
        }

        [Test]
        public void test_clinicPanels_baselinePanel_isIncluded()
        {
            IReadOnlyList<ClinicPanelDto> got = Source(Registry()).ClinicPanels;

            Assert.AreEqual(1, got.Count);
            Assert.AreEqual(4L, got[0].StructureId);
            Assert.AreEqual(new WorldPos(5, 0, 9), got[0].Cell);
        }

        [Test]
        public void test_switches_baselineSwitch_isIncluded()
        {
            IReadOnlyList<SwitchDto> got = Source(Registry()).Switches;

            Assert.AreEqual(1, got.Count);
            Assert.AreEqual(5L, got[0].StructureId);
            Assert.AreEqual(new WorldPos(2, 0, 4), got[0].Cell);
        }

        [Test]
        public void test_eachSlice_isDisjoint_noModuleLeaksAcrossKinds()
        {
            BakedInitialSource src = Source(Registry());
            int total = src.Doors.Count + src.Utensils.Count + src.ClinicPanels.Count + src.Switches.Count;

            Assert.AreEqual(4, total, "基线 5 件里 1 件是 Decor ⇒ 其余四类各恰 1 件(互不串类)");
        }

        // ══════════════ ⑤ 缺依赖 ══════════════

        [Test]
        public void test_noDependencies_emptyNotThrow()
        {
            var src = new BakedInitialSource();

            Assert.AreEqual(0, src.Doors.Count);
            Assert.AreEqual(0, src.Containers.Count);
            Assert.AreEqual(0, src.Utensils.Count, "P0 无器具数据 ⇒ 恒空");
            Assert.AreEqual(0, src.ClinicPanels.Count);
            Assert.AreEqual(0, src.Switches.Count);
        }

        // ══════════════ 夹具 ══════════════

        private sealed class FakeCatalog : IModuleCatalog
        {
            private readonly Dictionary<int, ModuleDefinition> _byId =
                new Dictionary<int, ModuleDefinition>();

            public FakeCatalog(params (int ModuleId, SlotType Type)[] rows)
            {
                foreach ((int id, SlotType type) in rows)
                {
                    _byId[id] = new ModuleDefinition(
                        id, type, new[] { new WorldPos(0, 0, 0) }, new[] { 0 },
                        cost: 1, buildItemKey: 8);
                }
            }

            public ModuleDefinition? GetModuleDefinition(int moduleId)
                => _byId.TryGetValue(moduleId, out ModuleDefinition d) ? d : (ModuleDefinition?)null;

            public bool ContainsModule(int moduleId) => _byId.ContainsKey(moduleId);

            public IReadOnlyCollection<int> GetAllModuleIds() => new List<int>(_byId.Keys);
        }
    }
}
