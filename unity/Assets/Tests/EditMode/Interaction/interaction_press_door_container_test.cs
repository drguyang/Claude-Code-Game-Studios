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
        private const long SwitchId = 45L;
        private const long PanelId = 46L;
        private const long UtensilId = 47L;

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

        // ══════════════ ⑤ 开关 / 医馆面板 / 器具(2026-11-02 · TD-interaction-03 收口)══════════════

        [Test]
        public void test_switchOnCell_routesSwitch_withStructureId()
        {
            var toggled = new List<long>();
            var router = Build(switchCell: Player, toggleSwitch: toggled.Add);

            bool consumed = router.OnPress();

            Assert.AreEqual(InteractableKind.Switch, router.LastRoutedKind);
            Assert.IsTrue(consumed);
            Assert.AreEqual(SwitchId, toggled[0], "StableId = structure_id");
        }

        [Test]
        public void test_clinicPanelOnCell_routesClinicPanel_withStructureId()
        {
            var opened = new List<long>();
            var router = Build(panelCell: Player, openClinicPanel: opened.Add);

            bool consumed = router.OnPress();

            Assert.AreEqual(InteractableKind.ClinicPanel, router.LastRoutedKind);
            Assert.IsTrue(consumed);
            Assert.AreEqual(PanelId, opened[0]);
        }

        [Test]
        public void test_utensil_withoutConsumer_returnsFalse()
        {
            var router = Build(utensilCell: Player);
            Assert.IsFalse(router.OnPress(),
                "18 的炮制面板 BLOCKED-BY-42 ⇒ 选中器具后按键落空(不假装进了模式)");
        }

        [Test]
        public void test_utensil_withConsumer_routesUtensil()
        {
            var entered = new List<long>();
            var router = Build(utensilCell: Player,
                enterProcessing: id => { entered.Add(id); return true; });

            bool consumed = router.OnPress();

            Assert.AreEqual(InteractableKind.Utensil, router.LastRoutedKind);
            Assert.IsTrue(consumed);
            Assert.AreEqual(UtensilId, entered[0]);
        }

        // ══════════════════════════ 辅助 ══════════════════════════

        private static InteractionPressRouter Build(
            WorldPos? doorCell = null,
            System.Action<long> toggleDoor = null,
            WorldPos? containerCell = null,
            System.Action<long> openContainer = null,
            WorldPos? switchCell = null,
            System.Action<long> toggleSwitch = null,
            WorldPos? panelCell = null,
            System.Action<long> openClinicPanel = null,
            WorldPos? utensilCell = null,
            System.Func<long, bool> enterProcessing = null)
        {
            var loader = new CandidateSetLoader(new CandidateSources(
                drops: null,
                worldBaked: null,
                patients: null,
                bakedInitial: new FakeBakedInitial(
                    doorCell, containerCell, switchCell, panelCell, utensilCell)));

            var radius = new InteractionRadius(Radius);
            var selector = new InteractionSelector(UnwiredDiscoveryReporter.Instance, radius);

            return new InteractionPressRouter(
                () => Player, () => 1L, actorId: 3,
                loader, selector, radius,
                drops: null, harvest: null,
                visit: null,
                aimBuild: null,
                toggleDoor: toggleDoor != null ? new System.Func<long, bool>(id => { toggleDoor(id); return true; }) : null,
                openContainer: openContainer,
                toggleSwitch: toggleSwitch != null ? new System.Func<long, bool>(id => { toggleSwitch(id); return true; }) : null,
                openClinicPanel: openClinicPanel,
                enterProcessing: enterProcessing);
        }

        private sealed class FakeBakedInitial : IBakedInitialSource
        {
            private readonly DoorDto[] _doors;
            private readonly ContainerDto[] _containers;
            private readonly SwitchDto[] _switches;
            private readonly ClinicPanelDto[] _panels;
            private readonly UtensilDto[] _utensils;

            public FakeBakedInitial(WorldPos? doorCell, WorldPos? containerCell,
                                    WorldPos? switchCell = null, WorldPos? panelCell = null,
                                    WorldPos? utensilCell = null)
            {
                _doors = doorCell.HasValue
                    ? new[] { new DoorDto(doorCell.Value, DoorId) }
                    : System.Array.Empty<DoorDto>();
                _containers = containerCell.HasValue
                    ? new[] { new ContainerDto(containerCell.Value, ContainerId) }
                    : System.Array.Empty<ContainerDto>();
                _switches = switchCell.HasValue
                    ? new[] { new SwitchDto(switchCell.Value, SwitchId) }
                    : System.Array.Empty<SwitchDto>();
                _panels = panelCell.HasValue
                    ? new[] { new ClinicPanelDto(panelCell.Value, PanelId) }
                    : System.Array.Empty<ClinicPanelDto>();
                _utensils = utensilCell.HasValue
                    ? new[] { new UtensilDto(utensilCell.Value, UtensilId) }
                    : System.Array.Empty<UtensilDto>();
            }

            public IReadOnlyList<UtensilDto> Utensils => _utensils;
            public IReadOnlyList<ClinicPanelDto> ClinicPanels => _panels;
            public IReadOnlyList<DoorDto> Doors => _doors;
            public IReadOnlyList<SwitchDto> Switches => _switches;
            public IReadOnlyList<ContainerDto> Containers => _containers;
        }
    }
}
