// T22 批一 · `TD-building-05` 的躯干判据(2026-11-10)
//
// 权威:
//   ADR-015 §一(视觉层 float 任意、不进 sim)· ADR-023 §②(场景零预摆,运行期生成)
//   `TD-building-05` 解除动作(「可见 + 可辨识 + 与格对齐」)
//
// ─────────────────────────────────────────────────────────────────────────────
// 本件钉什么
// ─────────────────────────────────────────────────────────────────────────────
//   ① **一个槽一个体块**,位置 = 槽位格、贴地(中心上抬半个高度);
//   ② **空槽不画**(`BakedInitial == false`)—— 否则「看得到的」与「选得中的」分叉;
//   ③ **可辨识靠尺寸**,不靠颜色(类型之间缩放在 Y/Z 上确有差异 —— 灰度下也分得出);
//   ④ **幂等**(重复 `SpawnAll` 不叠出第二套)与**清得干净**(`DestroyAll` 归零);
//   ⑤ **真产物**上跑一遍:9 个 baked 行 ⇒ 9 个体块,且灶台在 `(14,0,14)`。
//
// ⚠️ **读外部夹具的具名声明**(`test-standards.md` 要求):本件读
//   `Assets/DataCooked/world_buildslots.cooked.bytes`(经 `DataCookedDisk.LoadBuildSlots()`)
//   与 `Assets/Gameplay.Presentation/Patients/Resources/PatientPlaceholderMaterial.mat`(经 Resources)。
//   理由:**必须测真表 / 真材质** —— 内联字面量会让「表改了夹具仍绿」,而材质的可用性
//   (`TD-build-03`:着色器被剥离 ⇒ 打包版找不到 URP Lit)正是本批要守的那道门。
// ⚠️ 零写盘、零随机、零时间依赖。GameObject 在 `[TearDown]` 显式销毁。

using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Presentation;
using DaYiJingCheng.Gameplay.Presentation.Building;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;
using NUnit.Framework;
using UnityEngine;

namespace DaYiJingCheng.Tests.ModularBuilding
{
    [TestFixture]
    public sealed class structure_visual_spawner_test
    {
        private Material _material;
        private StructureVisualFactory _factory;
        private StructureVisualSpawner _spawner;

        [SetUp]
        public void Setup()
        {
            _material = Resources.Load<Material>(StructureVisualFactory.SharedPlaceholderMaterialResourcePath);
            Assert.IsNotNull(_material,
                "占位材质取不到 —— 结构与病人的视觉会一起瞎(资产被移走 / 改名?)");
            _factory = new StructureVisualFactory(_material);
            _spawner = new StructureVisualSpawner(_factory);
        }

        [TearDown]
        public void TearDown()
        {
            _spawner?.DestroyAll();
            _spawner?.Dispose();
        }

        private static WorldBuildSlotsDataSet Data(params WorldBuildSlotRow[] rows)
            => new WorldBuildSlotsDataSet(rows, 16, 1, 16, schemaVersion: 2u, configVersion: 1u);

        private static int MaskOf(SlotType type) => 1 << (int)type;

        // ══════════ ① 位掩码 → 类型(最低置位) ══════════

        [Test]
        public void test_slotTypeOf_picksLowestSetBit()
        {
            Assert.AreEqual(SlotType.Shell, StructureVisualFactory.SlotTypeOf(MaskOf(SlotType.Shell)),
                "单位掩码须还原成它自己");
            Assert.AreEqual(SlotType.Shell,
                StructureVisualFactory.SlotTypeOf(MaskOf(SlotType.Utensil) | MaskOf(SlotType.Shell)),
                "多位掩码取**最低位**(Shell=6 < Utensil=9;视觉不是权威,权威是 4 的选择器)");
            Assert.AreEqual(SlotType.Utensil, StructureVisualFactory.SlotTypeOf(MaskOf(SlotType.Utensil)));
            Assert.AreEqual(SlotType.None, StructureVisualFactory.SlotTypeOf(0), "零掩码 ⇒ None,不抛");
        }

        // ══════════ ② 可辨识靠尺寸 ══════════

        [Test]
        public void test_scale_differsAcrossSlotTypes()
        {
            Vector3 wall = StructureVisualFactory.ScaleOf(SlotType.Shell);
            Vector3 stove = StructureVisualFactory.ScaleOf(SlotType.Utensil);
            Vector3 panel = StructureVisualFactory.ScaleOf(SlotType.ClinicPanel);
            Vector3 knob = StructureVisualFactory.ScaleOf(SlotType.Switch);

            Assert.Less(wall.z, stove.z, "墙是薄板、灶台是方块 —— 厚度必须不同");
            Assert.Less(stove.y, panel.y, "灶台矮于立牌面板 —— 高度必须不同");
            Assert.Less(knob.y, stove.y, "开关是最小的那个");
            Assert.Greater(wall.y, stove.y, "墙高于灶台");
        }

        // ══════════ ③ 一个槽一个体块,位置贴地 ══════════

        [Test]
        public void test_spawnAll_createsOnePerBakedRow_atCellPosition()
        {
            var rows = new[]
            {
                new WorldBuildSlotRow(new WorldPos(14, 0, 14), MaskOf(SlotType.Utensil), true, 12),
                new WorldBuildSlotRow(new WorldPos(5, 0, 1),  MaskOf(SlotType.Door),    true, 11),
            };

            Assert.AreEqual(2, _spawner.SpawnAll(Data(rows)), "两个 baked 行 ⇒ 两个体块");

            GameObject stove = FindChild("StructureVisual_Utensil_14_0_14");
            Assert.IsNotNull(stove, "体块按 类型_格 命名(走查靠名字定位)");

            Vector3 scale = StructureVisualFactory.ScaleOf(SlotType.Utensil);
            Assert.AreEqual(14f, stove.transform.position.x, 1e-4f, "x = 格 x(1 m/格)");
            Assert.AreEqual(14f, stove.transform.position.z, 1e-4f, "z = 格 z");
            Assert.AreEqual(scale.y * 0.5f, stove.transform.position.y, 1e-4f,
                "y = 半个高度(底面贴地 y=0,不悬空也不陷进地板)");
        }

        // ══════════ ④ 空槽不画(负控) ══════════

        [Test]
        public void test_spawnAll_skipsUnbuiltSlot()
        {
            var rows = new[]
            {
                new WorldBuildSlotRow(new WorldPos(12, 0, 12), MaskOf(SlotType.Tent), false, -1),
            };

            Assert.AreEqual(0, _spawner.SpawnAll(Data(rows)),
                "空槽(`BakedInitial == false`)不画 —— 画了就是「这里已经有帐篷了」");
            Assert.IsNull(_spawner.Root,
                "一个体块都没有时,连根节点都不该建");
        }

        // ══════════ ⑤ 幂等 + 清得干净 ══════════

        [Test]
        public void test_spawnAll_isIdempotent_noDoubling()
        {
            var rows = new[]
            {
                new WorldBuildSlotRow(new WorldPos(1, 0, 1), MaskOf(SlotType.Shell), true, 8),
            };

            _spawner.SpawnAll(Data(rows));
            _spawner.SpawnAll(Data(rows));   // 世界切换会重装配 ⇒ 第二次不得叠出第二套

            Assert.AreEqual(1, _spawner.SpawnedCount, "重复 SpawnAll 后仍只一个体块");
            Assert.AreEqual(1, _spawner.Root.childCount, "场景里也只能有一个(簿记与现场须一致)");
        }

        [Test]
        public void test_destroyAll_removesEverything()
        {
            var rows = new[]
            {
                new WorldBuildSlotRow(new WorldPos(1, 0, 1), MaskOf(SlotType.Shell), true, 8),
                new WorldBuildSlotRow(new WorldPos(2, 0, 1), MaskOf(SlotType.Shell), true, 8),
            };
            _spawner.SpawnAll(Data(rows));

            _spawner.DestroyAll();

            Assert.AreEqual(0, _spawner.SpawnedCount);
            Assert.IsNull(_spawner.Root, "根节点须一并销毁(否则下次叠出第二套)");
        }

        // ══════════ ⑥ 真产物:9 个 baked 行 ══════════

        [Test]
        public void test_realArtifact_nineBakedSlots_spawnNine()
        {
            WorldBuildSlotsDataSet real = DataCookedDisk.LoadBuildSlots();
            Assert.AreEqual(18, real.Rows.Count, "真产物 18 行(数据变了就要重新看这条断言)");

            int baked = 0;
            for (int i = 0; i < real.Rows.Count; i++) if (real.Rows[i].BakedInitial) baked++;

            Assert.AreEqual(9, baked, "18 行里只有 9 行真站着东西(8 个空装饰位 + 1 个空帐篷位留给玩家)");
            Assert.AreEqual(baked, _spawner.SpawnAll(real), "baked 行数 == 体块数");

            Assert.IsNotNull(FindChild("StructureVisual_Utensil_14_0_14"),
                "灶台在 (14,0,14) —— 18 的入口必须看得见");
            Assert.IsNull(FindChild("StructureVisual_Tent_12_0_12"),
                "帐篷位是空槽 ⇒ 真产物这条腿同样不得画");
        }

        /// <summary>在**本 spawner 自己的根**下找体块(不用 `GameObject.Find` —— 见 `Root` 的注)。</summary>
        private GameObject FindChild(string name)
        {
            Transform root = _spawner.Root;
            if (root == null) return null;
            Transform child = root.Find(name);
            return child == null ? null : child.gameObject;
        }

        // ══════════ ⑦ 容器柜(T22 批二):与槽位**并存**,不是替代 ══════════

        private static WorldContainersDataSet Containers(params WorldContainerRow[] rows)
            => new WorldContainersDataSet(rows, schemaVersion: 1u, configVersion: 1u);

        [Test]
        public void test_spawnContainers_createsCabinetAtCell_nextToSlots()
        {
            _spawner.SpawnAll(Data(
                new WorldBuildSlotRow(new WorldPos(1, 0, 1), MaskOf(SlotType.Shell), true, 8)));
            int added = _spawner.SpawnContainers(Containers(
                new WorldContainerRow(1L, new WorldPos(5, 0, 5))));

            Assert.AreEqual(1, added, "一行容器 ⇒ 一个柜体");
            Assert.AreEqual(2, _spawner.SpawnedCount, "槽位与容器**并存**(不是替代)");

            GameObject cabinet = FindChild("ContainerVisual_1");
            Assert.IsNotNull(cabinet, "柜体按 instance_id 命名");
            Vector3 scale = StructureVisualFactory.ContainerScale;
            Assert.AreEqual(5f, cabinet.transform.position.x, 1e-4f);
            Assert.AreEqual(5f, cabinet.transform.position.z, 1e-4f);
            Assert.AreEqual(scale.y * 0.5f, cabinet.transform.position.y, 1e-4f, "贴地,同槽位体块口径");
            Assert.AreNotEqual(StructureVisualFactory.ScaleOf(SlotType.Decor), scale,
                "柜子不能长得和装饰件一样 —— 走查要靠它认出 20 的入口");
        }

        [Test]
        public void test_realArtifact_oneContainerCabinet()
        {
            WorldContainersDataSet real = DataCookedDisk.LoadWorldContainers();
            Assert.AreEqual(1, real.Containers.Count, "真产物 1 个容器(数据变了要重新看这条)");
            Assert.AreEqual(1, _spawner.SpawnContainers(real));
            Assert.IsNotNull(FindChild("ContainerVisual_1"),
                "容器柜在 (5,0,5) —— 20 的入口必须看得见");
        }
    }
}
