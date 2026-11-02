// 2026-11-02 · `AC-4-22` —— 第四源(门 / 柜)接进路由后的判据。
//
// 判据(逐条可证伪):
//   ① 站在门格 ⇒ 种类 = `Door`,消费面收到**那个 structure_id**,按键被消费;
//   ② 站在柜格 ⇒ 种类 = `Container`,消费面收到**那个 instance_id**,按键被消费;
//   ③ 未接消费面 ⇒ 返回 false(按键落空,**不**静默吞掉);
//   ④ 门与柜同格 ⇒ `KindPriority` 定胜负(Container=3 < Door=4,见 `interaction_kinds.json`)。
//
// ⚠️ 本件只断「4 该分的分了」;门的**状态效果**(变体 / 可通行性 / 世界流)归
//    `door_interaction_test`(真 `World` + 真 `StructureWriter`)。

using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Interaction;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Interaction
{
    /// <summary>第四源(门 / 容器)的路由判据。</summary>
    public sealed class interaction_press_door_container_test
    {
        private static readonly WorldPos Player = new WorldPos(5, 0, 5);
        private const int Radius = 3;
        private const long DoorId = 41L;
        private const long ContainerId = 44L;

        // ══════════════ ① 门 ══════════════

        [Test]
        public void test_doorOnCell_routesDoor_withStructureId()
        {
            var toggled = new List<long>();
            var router = Build(doorCell: Player, toggleDoor: toggled.Add);

            bool consumed = router.OnPress();

            Assert.AreEqual(InteractableKind.Door, router.LastRoutedKind);
            Assert.IsTrue(consumed, "门有消费面 ⇒ 消费");
            Assert.AreEqual(1, toggled.Count);
            Assert.AreEqual(DoorId, toggled[0], "StableId = structure_id(不是 instance_id)");
        }

        // ══════════════ ② 柜 ══════════════

        [Test]
        public void test_containerOnCell_routesContainer_withInstanceId()
        {
            var opened = new List<long>();
            var router = Build(containerCell: Player, openContainer: opened.Add);

            bool consumed = router.OnPress();

            Assert.AreEqual(InteractableKind.Container, router.LastRoutedKind);
            Assert.IsTrue(consumed);
            Assert.AreEqual(1, opened.Count);
            Assert.AreEqual(ContainerId, opened[0], "StableId = instance_id(容器是一件物品实例)");
        }

        // ══════════════ ③ 未接消费面 ══════════════

        [Test]
        public void test_door_withoutConsumer_returnsFalse()
        {
            var router = Build(doorCell: Player);
            Assert.IsFalse(router.OnPress(), "没有消费面 ⇒ 按键落空,不得伪装成已处理");
        }

        [Test]
        public void test_container_withoutConsumer_returnsFalse()
        {
            var router = Build(containerCell: Player);
            Assert.IsFalse(router.OnPress(), "没有消费面 ⇒ 按键落空,不得伪装成已处理");
        }

        // ══════════════ ④ 同格优先级 ══════════════

        [Test]
        public void test_sameCell_containerBeatsDoor_byKindPriority()
        {
            var opened = new List<long>();
            var toggled = new List<long>();
            var router = Build(doorCell: Player, toggleDoor: toggled.Add,
                               containerCell: Player, openContainer: opened.Add);

            router.OnPress();

            Assert.AreEqual(InteractableKind.Container, router.LastRoutedKind,
                "同一格:Container(3)先于 Door(4)(`interaction_kinds.json` 的 kind_priority)");
            Assert.AreEqual(1, opened.Count);
            Assert.AreEqual(0, toggled.Count, "门**不得**被同时触发");
        }

        // ══════════════════════════ 辅助 ══════════════════════════

        private static InteractionPressRouter Build(
            WorldPos? doorCell = null,
            System.Action<long> toggleDoor = null,
            WorldPos? containerCell = null,
            System.Action<long> openContainer = null)
        {
            var loader = new CandidateSetLoader(new CandidateSources(
                drops: null,
                worldBaked: null,
                patients: null,
                bakedInitial: new FakeBakedInitial(doorCell, containerCell)));

            var radius = new InteractionRadius(Radius);
            var selector = new InteractionSelector(UnwiredDiscoveryReporter.Instance, radius);

            return new InteractionPressRouter(
                () => Player, () => 1L, actorId: 3,
                loader, selector, radius,
                drops: null, harvest: null,
                visit: null,
                aimBuild: null,
                toggleDoor: toggleDoor != null ? new System.Func<long, bool>(id => { toggleDoor(id); return true; }) : null,
                openContainer: openContainer);
        }

        private sealed class FakeBakedInitial : IBakedInitialSource
        {
            private readonly DoorDto[] _doors;
            private readonly ContainerDto[] _containers;

            public FakeBakedInitial(WorldPos? doorCell, WorldPos? containerCell)
            {
                _doors = doorCell.HasValue
                    ? new[] { new DoorDto(doorCell.Value, DoorId) }
                    : System.Array.Empty<DoorDto>();
                _containers = containerCell.HasValue
                    ? new[] { new ContainerDto(containerCell.Value, ContainerId) }
                    : System.Array.Empty<ContainerDto>();
            }

            public IReadOnlyList<UtensilDto> Utensils => System.Array.Empty<UtensilDto>();
            public IReadOnlyList<ClinicPanelDto> ClinicPanels => System.Array.Empty<ClinicPanelDto>();
            public IReadOnlyList<DoorDto> Doors => _doors;
            public IReadOnlyList<SwitchDto> Switches => System.Array.Empty<SwitchDto>();
            public IReadOnlyList<ContainerDto> Containers => _containers;
        }
    }
}
