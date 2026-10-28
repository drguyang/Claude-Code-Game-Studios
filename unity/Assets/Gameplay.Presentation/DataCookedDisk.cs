// 烘焙产物的**磁盘兜底装载**(EditMode / 测试装配用)—— 24 匹配链接线所需的三份。
//
// 权威来源与先例:
//   `Gameplay.Boot/DiagnosisCookedDisk.cs`(sprint-07 S1.2 · 2026-10-11)是**同一形态的首例**:
//   Addressables 组在本机是 gitignored 的本地产物 ⇒ 生产走 `DataCorePreloader`;
//   测试 / EditMode 无 Addressables 运行时 ⇒ 从 `Assets/DataCooked/` 直读**同一份**字节,
//   用**同一个** codec 解码。本类是那条先例的**去诊断专用化**版本(先例自陈只服务诊断三份)。
//
// ⚠️ **本类不是生产装载路径** —— 生产唯一 = `AddressablesDataProvider`(经 `BootRoot` 的
//   `DataCorePreloader`)。本类只补「测试环境无 Addressables」的读路径:
//   · 读的是**同一份烘焙产物**(bake 菜单产物),零合成、零 Fake;
//   · 解码器是**同一批** `*CookedCodec`(与 bake 写方镜像);
//   ∴ 无第二真源。
//
// ⚠️ E-13 同款硬失败:任何缺失 ⇒ 显式 throw,零默认值、零静默降级。

using System;
using System.IO;
using DaYiJingCheng.Sim.Contracts;   // ItemDataSet(21a 物品种子表 · 2026-10-27)
using UnityEngine;

namespace DaYiJingCheng.Gameplay.Presentation
{
    /// <summary>烘焙产物的磁盘兜底读取(EditMode / 测试装配专用;生产走 Addressables)。</summary>
    public static class DataCookedDisk
    {
        /// <summary>产物目录名(相对 `Assets/`;与 `Editor.Tools.Bake.DataBakeMenu.CookedDirName` 同值)。</summary>
        private const string CookedDirName = "DataCooked";

        /// <summary>产物目录绝对路径。</summary>
        public static string CookedDirectory()
            => Path.Combine(Application.dataPath, CookedDirName);

        /// <summary>读产物字节(缺失 ⇒ 抛,不返回空数组)。</summary>
        /// <param name="assetName">产物文件名(如 <c>build_modules.cooked.bytes</c>)。</param>
        /// <exception cref="InvalidOperationException">文件不存在 —— 须先跑 bake。</exception>
        public static byte[] ReadAll(string assetName)
        {
            if (string.IsNullOrEmpty(assetName))
                throw new ArgumentException("产物名为空", nameof(assetName));

            string path = Path.Combine(CookedDirectory(), assetName);
            if (!File.Exists(path))
                throw new InvalidOperationException(
                    $"[E-13][disk] 烘焙产物不存在:{path} —— 须先跑 bake" +
                    "(DataBakeMenu / DiagnosisBakeCli.BakeAll)。拒绝返回空数据集。");
            return File.ReadAllBytes(path);
        }

        // ── 24 匹配链的三份(TD-building-01 读侧的装配输入)────────────────────
        // ⚠️ 产物名**引用 codec / provider 侧的常量**,不在此另抄字面量(ADR-024 §⑥ 同口径):
        //    `BuildModulesDataSet.CookedFileName` 是唯一声明处。

        /// <summary>23 模块目录(<c>build_modules.cooked.bytes</c>)。</summary>
        public static BuildModulesDataSet LoadBuildModules()
            => BuildModulesCookedCodec.Read(ReadAll(BuildModulesDataSet.CookedFileName));

        /// <summary>建造槽位骨架(<c>world_buildslots.cooked.bytes</c>;ADR-022 §三导出契约的烘焙产物)。
        /// <para>⚠️ 消费方 = <c>MinimalWorldFactory.Create()</c>(装配期世界工厂)。
        /// 产物缺失 / 损坏 ⇒ 本方法**抛**(E-13 口径);「降级为空目录」的判断在**调用方**
        /// (`MinimalWorldFactory` 显式 catch + LogError + 横幅),不在本方法里静默。</para></summary>
        public static WorldBuildSlotsDataSet LoadBuildSlots()
            => WorldBuildSlotsCookedCodec.Read(ReadAll(WorldBuildSlotsDataSet.CookedFileName));

        /// <summary>世界几何(<c>world_geometry.cooked.bytes</c>;ADR-022 §三导出契约的烘焙产物)。
        /// <para>⚠️ 消费方 = <c>MinimalWorldFactory.Create()</c>(装配期世界工厂)的**磁盘兜底**。
        /// 与槽位骨架不同:**本方法抛 ⇒ 装配期硬失败**,没有「降级为合成世界」的路径 ——
        /// 合成世界正是 story-002 要消灭的状态(它会以「兼容路径」的名义永久存活)。</para></summary>
        public static WorldGeometryDataSet LoadWorldGeometry()
            => WorldGeometryCookedCodec.Read(ReadAll(WorldGeometryDataSet.CookedFileName));

        /// <summary>地形表(<c>world_terrain.cooked.bytes</c>)。消费方 = 同上;失败语义同
        /// <see cref="LoadWorldGeometry"/>(硬失败,空表亦然 —— AC-5)。</summary>
        public static WorldTerrainDataSet LoadWorldTerrain()
            => WorldTerrainCookedCodec.Read(ReadAll(WorldTerrainDataSet.CookedFileName));

        /// <summary>21a 家具目录(<c>item_database_furniture.cooked.bytes</c>)。</summary>
        public static ClinicFurnitureDataSet LoadClinicFurniture()
            => ClinicFurnitureCookedCodec.Read(ReadAll(AddressablesDataProvider.FurnitureCatalogueAddress));

        /// <summary>24 ROOM_TABLE(<c>clinicmachine_room.cooked.bytes</c>)。</summary>
        public static ClinicRoomDataSet LoadClinicRoom()
            => ClinicMachineCookedCodec.ReadRoom(ReadAll(AddressablesDataProvider.ClinicRoomAddress));

        /// <summary>24 ROOM_NAMES 词表(<c>clinicmachine_room_names.cooked.bytes</c>;裁定 ①-C)。</summary>
        public static ClinicRoomNameDataSet LoadClinicRoomNames()
            => ClinicMachineCookedCodec.ReadRoomNames(
                ReadAll(AddressablesDataProvider.ClinicRoomNamesAddress));

        /// <summary>24 CONTEXT_NAMES 词表(<c>clinicmachine_context_names.cooked.bytes</c>;裁定 ②-C)。</summary>
        public static ClinicContextNameDataSet LoadClinicContextNames()
            => ClinicMachineCookedCodec.ReadContextNames(
                ReadAll(AddressablesDataProvider.ClinicContextNamesAddress));

        // ── 9 病种注册表(2026-10-22 B1 接真)────────────────────────────────
        // ⚠️ 与上三份不同:本条的**消费方是生产装配**(`CompositionRoot` 的 dual-path),
        //    不只是测试。它在这里的意义 = 「产物已烘但 Addressables catalog 未重建」时的
        //    同一份字节兜底(同 codec、同校验,非降级)。

        /// <summary>9 病种注册表(<c>disease_registry.cooked.bytes</c>;含全局 <c>W</c> / <c>TREND_EPS</c>)。</summary>
        public static DiseaseRegistryDataSet LoadDiseaseRegistry()
            => DiseaseRegistryCookedCodec.Read(ReadAll(AddressablesDataProvider.DiseaseRegistryAddress));

        // ── 21a 物品种子表(S20-10/11/12 修复轮 · 2026-10-27)────────────────
        // ⚠️ 同 `LoadDiseaseRegistry`:消费方是**生产装配**(`CompositionRoot` 的
        //    `InventoryLoadService` 惰性取数),不只是测试。它在这里的意义 =
        //    「provider 为 null」(EditMode / 未预载)时读**同一份**烘焙产物 + **同一个** codec。

        /// <summary>21a 物品种子表(<c>item_database_items.cooked.bytes</c>)。
        /// <para>⚠️ <b>惰性调用点</b>:生产走 <c>() =&gt; DataCookedDisk.LoadItems()</c>,
        /// 只在首次库存投影重建时求值 —— 装配期零 IO(见 `InventoryLoadService` 类头注)。</para></summary>
        public static ItemDataSet LoadItems()
            => CookedCodec.ReadItemDataSet(ReadAll(AddressablesDataProvider.ItemsAddress));

        /// <summary>21a 配方表 + 全局结算常量(<c>item_database_recipes.cooked.bytes</c>)。
        /// <para>⚠️ <b>惰性调用点</b>(2026-10-28 A-5 修复轮):生产走
        /// <c>() =&gt; DataCookedDisk.LoadRecipes()</c>,只在首次库存投影重建时求值 ——
        /// 与 <see cref="LoadItems"/> 同格。消费者 = `InventoryFoldContext.Recipes`
        /// (R15:`Craft.RecipeId` → `Recipe.Outputs`);在此之前 **fold 侧从无配方表**,
        /// 每条有产出的 `Craft` 都报 <c>CraftRecipeNotInTable</c>。</para></summary>
        public static DaYiJingCheng.Sim.RecipeDataSet LoadRecipes()
            => CookedCodec.ReadRecipeDataSet(ReadAll(AddressablesDataProvider.RecipesAddress));
    }
}
