// 装配期世界工厂 —— **世界几何的唯一装配入口**(S20-17 起 · 2026-10-28;story-002 改为装载 · 2026-10-29)。
//
// ═══════════════════════════════════════════════════════════════════════════
// 本文件的历史与现状(两段,第一段原文保留)
// ═══════════════════════════════════════════════════════════════════════════
// **2026-10-28(S20-17)**:`CompositionRoot.cs` 逐字记着「23 的 `StructureInstanceRegistry`
// 在生产树**零构造**(`new World` 的构造同样零构造)」。本文件是那一轮的第一块 —— 让 `World`
// 在生产装配路径上**存在**。当时世界几何的三种来源(作者态 `world_*.json` /
// `DataCooked/world_*.cooked` / 关卡工具的产出)**一样都不存在**,所以只能造一个
// ⚠️ 本文件是**运行期**源码,`RuntimeSourceGuard`(AC-21a-48)会剥掉注释后扫**字符串字面量**里的
// 「作者态目录」字样。**注释里写路径名是安全的**(全仓大量注释如此),**异常报文里写就会红** ——
// 2026-10-29 实测:一条 `InvalidDataException` 报文的文案里带了路径写法,全量回归红了一条,
// 改成「作者态数据目录」才绿。**改本文件的报文时避开该字样。**
// **最小合成世界**(16×1×16 全 walkable),并强制打一条 `LogWarning` 作诊断锚点。
//
// **2026-10-29(关卡工具 story-002)**:那三样现在都有了 —— 关卡工具导出
// `world_geometry.json` / `world_terrain.json` → ADR-014 烘焙 → `world_geometry.cooked` /
// `world_terrain.cooked` → `data-core` 预载。⇒ **合成支被删除**,本类成为纯粹的**装载器**:
// 几何尺寸 / 每格可走性 / 每格速度档下标**全部**来自烘焙产物。
//
// ⚠️ **合成支为什么整体删掉,而不是留成「无产物则合成」的兼容路径**
//    (story-002 §Implementation Notes 的「乙」案是后者;本轮**取窄**,如实登记供用户裁定):
//    ① **它与 AC-5 在同一入口上产生两种语义** —— AC-5 要求「`TERRAIN_TABLE` 空表 ⇒ 装载**抛**」,
//       而「无产物则合成」要求「产物不在 ⇒ 静默造一个全可走世界」。两条判据在同一段
//       `try` 里只能二选一,而 AC-5 是 BLOCKING。
//    ② **判别「产物缺失」与「产物损坏」不可靠**:`AddressablesDataProvider` 把两种失败
//       都包成 `InvalidOperationException`(见其 `Ensure*` 的 `catch … throw new
//       InvalidOperationException`),按异常类型分不开 ⇒ 合成支会把「字节损坏」
//       当成「没有数据」吞下去。那正是本仓反复抓到的「假绿」形态。
//    ③ **合成支今日零可达**:两份产物随本 story 提交入库,`data-core` 组已登记。
//    ⇒ 终态(故事卡称「甲」)就是现在这个形态。**若将来确需合成支**(例如「开发者未烘培」的
//      友好降级),须**另裁**并同时解决 ② 的判别问题 —— 不得在 `catch` 里顺手加一条。
//
// ═══════════════════════════════════════════════════════════════════════════
// 边界(本卡不解除的,逐条如实)
// ═══════════════════════════════════════════════════════════════════════════
// · 🔁 **POI / 资源格:已由产物供给**(2026-10-31 `S21-8` 订正,原文不删)——
//   原文写「仍全 `-1`(无) —— 归 story S4」,**已过期**:`level-tool/story-002`
//   (2026-10-29)已产出两份数据集,本文件 `:147-172` 从 `IDataProvider` / `DataCookedDisk`
//   载入并填格。`-1` 今天的语义只剩「**该格没有 POI / 资源**」这一合法结果,不再是「还没接」。
//   ⚠️ 载入是**即时 + 硬失败**(缺任一份 ⇒ 抛),**不**静默降级成「全 -1」——
//   后者会让 `PoiStateMachine` 的 `_poiIdSet` 零成员而**看起来完全正常**(GDD EC-6-10),是本仓记过的假绿形态。
//   **载体**(可跑):`Tests/EditMode/LevelTool/poi_and_resource_load_test.cs` 的
//   `test_ac4_poiGrid_poiCellReturnsId_nonPoiCellReturnsMinusOne`(两个方向:POI 格 == id、
//   非 POI 格 == -1)与 `test_ac4_poiStateMachine_idsFromArtifact_canEmitPoiStateChanged`。
// · **`WorldLatticeParams` 仍不构造** —— 它的 6 个数值里只有 `LatticeSizeMm` 与 K_speed 表
//   来自产物,其余四个(`SPEED_MODE_MAX` / `K_CONTEXT_MAX` / `MAX_DT` / `SAFETY_MARGIN`)
//   **全归用户数值轮**。本文件**不**给它们填默认值:那会让 F-6-1 的不等式以「猜的数」成立。
// · **第一层之外的 y 只有 1 格**(P0 关卡声明 16×1×16);多层世界归关卡设计轮。
// · 本工厂**不**提供任何写入面:世界内容只能由关卡工具 + 烘焙链替换。

using System;
using System.Collections.Generic;   // Dictionary(导航格按 chunk 装载)
using System.IO;   // InvalidDataException(装载期硬失败的异常类型)
using DaYiJingCheng.Gameplay.Presentation;   // DataCookedDisk / 三份世界数据集
using DaYiJingCheng.Sim.Contracts;           // IDataProvider(生产装载面)
using DaYiJingCheng.Sim.World;
using UnityEngine;   // Debug(本文件属装配层,门 A 不约束)

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>
    /// 装配期世界工厂:从烘焙产物装载世界几何 + 地形表,并注册建造槽位。
    /// <para>⚠️ <b>本类不做任何合成</b> —— 几何三件(尺寸 / 可走性 / 速度档下标)全部来自
    /// <c>world_geometry.cooked</c> 与 <c>world_terrain.cooked</c>。产物缺失或损坏 =
    /// **启动期硬失败**(E-13),没有「退回全可走世界」的路径。详见文件头 ①②③。</para>
    /// <para>⚠️ <b>槽位骨架是唯一的例外</b>:它装载失败仍降级为**空目录** + `LogError` +
    /// 横幅(今日行为,S20-20 起)。理由见 <see cref="IsRecoverableSlotLoadFailure"/>:
    /// 空槽位目录的症状是「玩家建不了东西」,是可诊断的显式降级;而空几何的症状是
    /// 「世界不存在」,没有可继续运行的下游。</para>
    /// </summary>
    public static class MinimalWorldFactory
    {
        /// <summary>建造一个世界(从烘焙产物装载)。</summary>
        /// <param name="dataProvider">**生产**装载面(data-core Addressables);
        /// <c>null</c> ⇒ 退回 <see cref="DataCookedDisk"/> 的磁盘路径(**仅编辑器 / 夹具** ——
        /// 玩家构建里 <c>Assets/DataCooked</c> 不随包出货,该路径必失败)。
        /// <para>生产调用点 = `BootRoot` 启动序(传 `DataCorePreloader.Provider`)。</para></param>
        /// <returns>新的 <see cref="World"/> 实例(每次调用都是独立实例 —— 两份注册表
        /// 就是两份真源,调用方须自己保证只建一次)。
        /// <para>⚠️ <b>开档基线不由本方法应用</b>:应用基线需要**模块目录**,而目录由
        /// <c>CompositionRoot</c> 在装配段才构造。工厂只负责把 `BakedInitial` / `ModuleId`
        /// **装进目录**;基线由装配段调 <see cref="World.ApplyBakedInitial"/> 落地。</para></returns>
        /// <exception cref="InvalidDataException">产物损坏 / 尺寸不符 / 空地形表 / 越界下标 —— 启动期硬失败。</exception>
        public static World Create(IDataProvider dataProvider = null) => Create(dataProvider, out _);

        /// <summary>同上,并把**几何产物声明的格边长**带出来(`LATTICE_SIZE`,整数毫米)。
        /// <para>🔁 <b>2026-10-29(`TD-audio-035` ① 换源轮)新增</b>:该量此前**没有任何运行期出口** ——
        /// 产物里有、日志里打过,但装配层取不到 ⇒ 44 的空间化换算只能读自己的占位常量。</para>
        /// <para>⚠️ **它仍是 6 的值,不是 44 的值**:`P0ClinicLevel.LatticeSizeMm` 自陈「占位值,
        /// 归用户数值轮」⇒ 本出口解决的是**换源**(单一真源),不是**定值**。数值轮改关卡侧一处,
        /// 44 的米制尺度随之变 —— 那正是 `TD-audio-035` ① 要求的性质。</para></summary>
        /// <param name="dataProvider">同 <see cref="Create(IDataProvider)"/>。</param>
        /// <param name="latticeSizeMm">出口:格边长(整数毫米;由 codec 校验 `&gt; 0`,见
        /// <c>WorldGeometryCookedCodec</c> 的装载断言)。</param>
        /// <returns>同 <see cref="Create(IDataProvider)"/>。</returns>
        /// <exception cref="InvalidDataException">同 <see cref="Create(IDataProvider)"/>。</exception>
        public static World Create(IDataProvider dataProvider, out int latticeSizeMm)
        {
            WorldGeometryDataSet geo = dataProvider != null
                ? dataProvider.Load<WorldGeometryDataSet>()
                : DataCookedDisk.LoadWorldGeometry();
            WorldTerrainDataSet ter = dataProvider != null
                ? dataProvider.Load<WorldTerrainDataSet>()
                : DataCookedDisk.LoadWorldTerrain();

            // 格边长的唯一运行期出口(`TD-audio-035` ①)—— 与下面那条 `[世界装载]` 日志同一变量
            // (日志印的与会话拿到的**不可能分叉**)。codec 已在读产物时断言 `> 0`。
            latticeSizeMm = geo.LatticeSizeMm;

            // ── 两份产物的尺寸对账 ────────────────────────────────────────────────
            // 两份产物各自带 `world_size`(刻意冗余)。不相等 = 「地形是从另一版关卡烘的」,
            // 那会让每格的下标含义整体错位 —— 而那在运行期**看不出来**(只是地面材质错格)。
            if (geo.SizeX != ter.SizeX || geo.SizeY != ter.SizeY || geo.SizeZ != ter.SizeZ)
                throw new InvalidDataException(
                    $"世界几何与地形表的声明尺寸不一致:几何 {geo.SizeX}×{geo.SizeY}×{geo.SizeZ}," +
                    $"地形 {ter.SizeX}×{ter.SizeY}×{ter.SizeZ} —— 两份产物不是同一次导出的结果," +
                    "重跑「导出 p0_clinic 到作者态数据目录」+ 两个烘焙菜单");

            int sizeX = geo.SizeX, sizeY = geo.SizeY, sizeZ = geo.SizeZ;
            int total = geo.TotalCells;

            if (ter.CellSpeedIndex.Length != total)
                throw new InvalidDataException(
                    $"地形表每格数组长度 = {ter.CellSpeedIndex.Length},与几何格总数 {total} 不符");

            // ── 地形格:可走性来自几何产物,速度档下标来自地形产物 ────────────────
            var terrain = new TerrainCell[sizeX, sizeY, sizeZ];
            int walkableCount = 0;
            for (int x = 0; x < sizeX; x++)
                for (int y = 0; y < sizeY; y++)
                    for (int z = 0; z < sizeZ; z++)
                    {
                        // ⚠️ 索引公式与 `WorldGeometry.ToIndex` / `LevelSource.CellIndex` 逐字相同:
                        //    `(x*SizeY + y)*SizeZ + z`。三处若漂移,错格是**静默**的。
                        int idx = (x * sizeY + y) * sizeZ + z;

                        bool walkable = geo.Walkable[idx] != 0;
                        if (walkable) walkableCount++;
                        terrain[x, y, z] = new TerrainCell(walkable, ter.CellSpeedIndex[idx]);
                    }

            // ★ **`TerrainCell.KSpeedIdx` 的首个消费者**(story-002 AC-6)。
            //   此前它的上界只有「ADR-014 阶段 2 白名单」这一条**纸面**约定 —— 类自身的 doc
            //   逐字写着「当前**全仓零消费者**」。越界下标不会抛,只会让归 1 的速度求值
            //   取出另一行的 K_accel / K_decel(错格、无异常)。
            ValidateTerrainSpeedIndices(terrain, ter.Rows.Count);

            // ── POI **定义** / 资源点:从 cooked 产物填格(story-004 AC-4 / AC-5)──────────
            // ⚠️ 两份产物都是**即时** Load(与几何 / 地形同款):缺任一份 = 启动期硬失败。
            //    静默降级成「全 -1」会让 `PoiStateMachine` 的 `_poiIdSet` 零成员、
            //    `ResourceGrid` 恒无 —— 那在运行期**看起来完全正常**(GDD EC-6-10 的「无」是合法结果),
            //    正是本仓反复记的假绿形态。
            WorldPoiDataSet poiData = dataProvider != null
                ? dataProvider.Load<WorldPoiDataSet>()
                : DataCookedDisk.LoadWorldPoi();
            WorldResourcesDataSet resourceData = dataProvider != null
                ? dataProvider.Load<WorldResourcesDataSet>()
                : DataCookedDisk.LoadWorldResources();

            var poiGrid = new int[total];
            var resourceGrid = new int[total];
            for (int i = 0; i < total; i++)
            {
                poiGrid[i] = -1;
                resourceGrid[i] = -1;
            }

            // ⚠️ 索引公式与上面地形格 / `WorldGeometry.ToIndex` / `LevelSource.CellIndex` **逐字相同**:
            //    `(x*SizeY + y)*SizeZ + z`。四处若漂移,错格是**静默**的。
            // ⚠️ 越界在这里是**抛**而不是钳位:尺寸对账已过,越界只可能是产物被手工改过
            //    (工具侧 `ValidateAgainstLevel` 在导出 / 烘焙两处都拒过)。
            for (int i = 0; i < poiData.Pois.Count; i++)
            {
                WorldPoiRow row = poiData.Pois[i];
                int idx = CellIndexChecked(row.Cell, sizeX, sizeY, sizeZ, "world_poi", i);
                poiGrid[idx] = row.PoiId;
            }
            for (int i = 0; i < resourceData.Resources.Count; i++)
            {
                WorldResourceRow row = resourceData.Resources[i];
                int idx = CellIndexChecked(row.Cell, sizeX, sizeY, sizeZ, "world_resources", i);
                resourceGrid[idx] = row.ResourceId;
            }

            var geometry = new WorldGeometry(terrain, poiGrid, resourceGrid);

            // ── 槽位骨架:从 cooked 产物装载(S20-20 · 2026-10-28)─────────────────
            BuildSlotCatalog slotCatalog;
            bool slotsLoaded;
            string failureSummary = null;
            try
            {
                slotCatalog = LoadSlotCatalog(dataProvider, sizeX, sizeY, sizeZ, out failureSummary);
                slotsLoaded = true;
            }
            // ⚠️ **`ArgumentException` 须排除其两个子类**(2026-10-28 代码评审 A-23):
            //    `ArgumentNullException` / `ArgumentOutOfRangeException` 是**编码缺陷**的信号
            //    (某次重构把 null 传进装载链),被本 catch 吞掉后会打出一条
            //    「槽位产物缺失或损坏…修法:先跑关卡工具导出 JSON」—— **把开发者引向重烘产物,
            //    而真因在代码**。上面那句「编程错误照常冒泡」此前**只对 NRE 成立**。
            catch (Exception ex) when (IsRecoverableSlotLoadFailure(ex))
            {
                slotCatalog = new BuildSlotCatalog();   // 空目录(与今日行为一致)
                slotsLoaded = false;
                failureSummary = $"{ex.GetType().Name}: {ex.Message}";
                Debug.LogError(
                    "[S20-20][世界装载] 槽位骨架装载失败 ⇒ 槽位目录为空," +
                    "PlaceableChecker 将恒 SlotNotFound(玩家建不了任何东西)。"
                    + "装载源 —— " + SlotSourceLabel(dataProvider) + "。"
                    + "异常摘要 —— " + failureSummary);
            }

            string slotNote = slotsLoaded
                ? $"槽位 {slotCatalog.Count} 格"
                : "槽位目录为空(见上一条 LogError)";

            // ⚠️ **本条是 `Log` 而非 `LogWarning`**(story-002 起):合成世界已不存在,
            //    「几何来自产物」是**正常状态**,不是需要诊断锚点的异常。
            //    只有槽位装载失败那条是真的异常(它仍是 `LogError`)。
            Debug.Log(
                $"[世界装载] 几何来自烘焙产物 —— {sizeX}×{sizeY}×{sizeZ}," +
                $"可走 {walkableCount}/{total} 格,TERRAIN_TABLE {ter.Rows.Count} 行" +
                $"(K_TERRAIN_MAX = {ter.KTerrainMax}),LATTICE_SIZE = {geo.LatticeSizeMm} mm;" +
                $"{slotNote};POI {poiData.Pois.Count} 个,资源点 {resourceData.Resources.Count} 个;" +
                $"装载源 = {SlotSourceLabel(dataProvider)}。");

            // ── 导航格切片:装载 + 建 NavLattice(story-005 接线轮,2026-10-29)──────
            // ⚠️ 与上面几份**不同,这里不是 `Load<T>()`**:导航格是「一个类型 N 个产物」,
            //    类型分派表达不了 chunk 号 ⇒ 走 codec 的按 chunk 入口。
            // ⚠️ **「世界对不上」⇒ 本世界没有导航格数据**(`nav = null`,回落到几何产物的可走性)。
            //    这不是降级,是**语义**:导航格切片描述的是**某一个**世界(尺寸 + chunk 划分都写在
            //    产物头里);尺寸对不上就说明「这些切片不是这个世界产出的」,该世界本就无导航格。
            //    夹具用 4×1×4 之类的**合成世界**直驱本工厂时正走这条 ⇒ 它们不该被迫烘导航格。
            //    ⚠️ 真正损坏(长度不符 / 魔数错)仍会从 codec 抛 `InvalidDataException`,
            //    与「尺寸不符」同类型 ⇒ 一并落这条分支;P0 生产侧的「必须接上」由夹具
            //    `nav_streaming_wiring_test::test_factory_worldHasNavLattice_…` 单独钉住
            //    (它拿**真产物**跑,若真损坏那条会红)。
            NavLattice navLattice = null;
            string navNote;
            try
            {
                navLattice = LoadNavLattice(geo, dataProvider, out int navChunkCount);
                navNote = $"导航格 {navChunkCount} 片已装载并全驻留;" +
                          $"chunk = {geo.ChunkSizeX}×{geo.ChunkSizeY}×{geo.ChunkSizeZ}," +
                          $"流式半径 = {NavStreamingRadius}";
            }
            catch (Exception ex) when (ex is InvalidDataException || ex is FileNotFoundException)
            {
                navNote = $"导航格**未接**(本世界的尺寸与盘上切片不符 ⇒ 这些切片不属于本世界):{ex.Message}";
            }

            Debug.Log($"[世界装载] {navNote}。");

            return new World(geometry, slotCatalog, navLattice, NavStreamingRadius);
        }

        /// <summary>
        /// P0 的 chunk 流式半径(chunk 单位)。
        /// <para>⚠️ **`1` 是刻意的**:P0 的 chunk 划分 = 2×1×2,半径 1 ⇒ 任何玩家格都覆盖全部 4 块
        /// ⇒ 与「未接导航格」逐格等价,**零行为变更**。流式真正生效要等大世界的关卡设计轮 ——
        /// 那时调这个数(或改成本关卡的声明字段)才会看到「未驻留 = block」的生产面。</para>
        /// </summary>
        public const int NavStreamingRadius = 1;

        /// <summary>
        /// 按 chunk 装载导航格切片并建 <see cref="NavLattice"/>。
        /// <para>⚠️ **chunk 数由几何产物推出**(`geo.Topology`),不写死 —— 与切片器 / 运行期 codec
        /// 同一个函数(`ChunkTopology.ForWorld`)。</para>
        /// <para>⚠️ 每片的 `world_size` / `chunk_size` 与几何产物**逐项对账**:对不上 = 两份产物
        /// 来自不同版本的关卡,而症状(某些格永远查不到)离病根极远。</para>
        /// </summary>
        /// <exception cref="InvalidDataException">某片自报的尺寸与几何产物不符。</exception>
        private static NavLattice LoadNavLattice(
            WorldGeometryDataSet geo, IDataProvider dataProvider, out int chunkCount)
        {
            ChunkTopology topo = geo.Topology;
            chunkCount = topo.ChunkCount;

            var chunks = new Dictionary<int, IReadOnlyList<int>>(chunkCount);
            for (int i = 0; i < chunkCount; i++)
            {
                WorldNavChunkDataSet nav = dataProvider is AddressablesDataProvider ap
                    ? ap.LoadNavChunk(i)
                    : DataCookedDisk.LoadWorldNavChunk(i);

                if (nav.WorldSizeX != geo.SizeX || nav.WorldSizeY != geo.SizeY || nav.WorldSizeZ != geo.SizeZ)
                    throw new InvalidDataException(
                        $"[世界装载] 导航格第 {i} 片自报世界尺寸 " +
                        $"{nav.WorldSizeX}×{nav.WorldSizeY}×{nav.WorldSizeZ},而几何产物是 " +
                        $"{geo.SizeX}×{geo.SizeY}×{geo.SizeZ} —— 两份产物来自不同版本的关卡");

                if (nav.ChunkSizeX != geo.ChunkSizeX || nav.ChunkSizeY != geo.ChunkSizeY ||
                    nav.ChunkSizeZ != geo.ChunkSizeZ)
                    throw new InvalidDataException(
                        $"[世界装载] 导航格第 {i} 片自报 chunk 尺寸 " +
                        $"{nav.ChunkSizeX}×{nav.ChunkSizeY}×{nav.ChunkSizeZ},而几何产物是 " +
                        $"{geo.ChunkSizeX}×{geo.ChunkSizeY}×{geo.ChunkSizeZ} —— " +
                        "两侧的 chunk 划分不是同一个函数算出来的");

                chunks.Add(nav.ChunkIndex, System.Array.AsReadOnly(nav.Walkable));
            }

            return new NavLattice(
                geo.SizeX, geo.SizeY, geo.SizeZ,
                geo.ChunkSizeX, geo.ChunkSizeY, geo.ChunkSizeZ, chunks);
        }

        /// <summary>
        /// **`TerrainCell.KSpeedIdx` 的量程校验**(story-002 AC-6 的兑现面)。
        /// <para>逐格读回 `KSpeedIdx` 并断言 `0 ≤ idx &lt; terrainTableRowCount`。</para>
        /// <para>⚠️ <b>这是 `TerrainCell.KSpeedIdx` 在全仓的第一个、也是当前唯一的读者</b> ——
        /// 该字段此前只有「ADR-014 阶段 2 白名单」这条纸面约定守着,类自身的 doc 逐字写着
        /// 「当前**全仓零消费者**⇒ 无现役失效面」。AC-6 只要求「有消费者」,
        /// **不要求「消费得对」** —— 真正的速度求值归系统 1 的实现轮。</para>
        /// <para>⚠️ <b>可见性 = `public`</b>:读侧 codec 已经拒过越界下标,所以生产数据**打不出**
        /// 这条失败分支 —— 若把它藏在 `private`,那份判据就是一条**无人能证伪**的断言。
        /// 公开它,夹具可以用手工构造的 <see cref="TerrainCell"/> 数组直接命中
        /// (承 `IsRecoverableSlotLoadFailure` 的同款理由)。</para>
        /// </summary>
        /// <param name="terrain">已构造的地形格(三维)。</param>
        /// <param name="terrainTableRowCount">`TERRAIN_TABLE` 的行数(合法下标的上界,不含)。</param>
        /// <exception cref="InvalidDataException">存在越界 / 负的 `KSpeedIdx`。</exception>
        /// <summary>
        /// 格坐标 → 一维索引,越界即抛(为 POI / 资源点填格用)。
        /// <para>⚠️ 公式与 `WorldGeometry.ToIndex` / `LevelSource.CellIndex` **逐字相同**;
        /// 三处若漂移,错格是**静默**的(打到别的格,无异常)。</para>
        /// <para>⚠️ **不做钳位**:越界只可能是产物被手工改过 —— 工具侧与烘焙侧都已拒过,
        /// 钳位会把「产物坏了」变成「布局看起来对但有格子丢了」。</para>
        /// </summary>
        /// <exception cref="InvalidDataException">格越出声明尺寸。</exception>
        private static int CellIndexChecked(
            WorldPos cell, int sizeX, int sizeY, int sizeZ, string product, int rowIndex)
        {
            if (cell.X < 0 || cell.X >= sizeX || cell.Y < 0 || cell.Y >= sizeY ||
                cell.Z < 0 || cell.Z >= sizeZ)
                throw new InvalidDataException(
                    $"[cooked] {product} 第 {rowIndex} 行的格 ({cell.X},{cell.Y},{cell.Z}) " +
                    $"超出世界尺寸 {sizeX}×{sizeY}×{sizeZ} —— 产物被手工改过?" +
                    "(工具侧与烘焙侧都已拒过越界)");
            return (cell.X * sizeY + cell.Y) * sizeZ + cell.Z;
        }

        public static void ValidateTerrainSpeedIndices(TerrainCell[,,] terrain, int terrainTableRowCount)
        {
            if (terrain == null) throw new ArgumentNullException(nameof(terrain));
            if (terrainTableRowCount < 1)
                throw new InvalidDataException(
                    $"TERRAIN_TABLE 行数 = {terrainTableRowCount} —— 须 ≥ 1(GDD F-6-2)");

            int sizeX = terrain.GetLength(0), sizeY = terrain.GetLength(1), sizeZ = terrain.GetLength(2);
            for (int x = 0; x < sizeX; x++)
                for (int y = 0; y < sizeY; y++)
                    for (int z = 0; z < sizeZ; z++)
                    {
                        int kSpeedIdx = terrain[x, y, z].KSpeedIdx;
                        if (kSpeedIdx < 0 || kSpeedIdx >= terrainTableRowCount)
                            throw new InvalidDataException(
                                $"地形速度档下标越界:格 ({x},{y},{z}) 的 KSpeedIdx = {kSpeedIdx}," +
                                $"TERRAIN_TABLE 行数 = {terrainTableRowCount} —— 查表会取出越界行");
                    }
        }

        /// <summary>
        /// 「装载体失败是否可降级」。**这是那条 `catch` 判据的唯一出处**(不内联在 catch 里)。
        ///
        /// <para>⚠️ 存在的理由(2026-10-28 代码评审 **A-23**):判据内联时它**无法被夹具钉住** ——
        /// 「`ArgumentNullException` 会不会被吞」只能靠读代码回答。抽成具名谓词后,
        /// 每条分支各有一条夹具(`minimal_world_slot_load_failure_test.cs`),
        /// 停用任一分支的反证都能转红。</para>
        ///
        /// <para><b>可降级</b> = 装载链路会抛的「环境类」异常。</para>
        /// <para><b>必须冒泡</b> = 一切 `ArgumentNullException` / `ArgumentOutOfRangeException`
        /// —— 它们是**编码缺陷**的信号,吞掉后会打成「产物缺失或损坏…修法:先跑关卡工具导出」,
        /// **把开发者引向重烘产物,而真因在代码**。</para>
        /// <para>⚠️ <b>可见性 = `public`</b>:EditMode 夹具经 public 面直调按真值表逐条钉
        /// (承 `AssemblyGates` 的先例)。若改回 `internal`,夹具将编译失败而非静默跳过。</para>
        /// </summary>
        public static bool IsRecoverableSlotLoadFailure(Exception ex)
        {
            if (ex == null) return false;
            if (ex is ArgumentNullException) return false;          // 编码缺陷
            if (ex is ArgumentOutOfRangeException) return false;    // 编码缺陷
            return ex is InvalidOperationException
                || ex is InvalidDataException
                || ex is IOException
                || ex is UnauthorizedAccessException
                || ex is ArgumentException;                          // 裸基类:装载链路自抛(空产物名)
        }

        /// <summary>
        /// 从烘焙产物装载槽位骨架并注册进 <see cref="BuildSlotCatalog"/>。
        /// <para>⚠️ 任何不合格(空表 / 行越出世界几何)⇒ 显式抛,由 <see cref="Create"/>
        /// 决定「降级为空目录 + 响亮登记」—— 本方法**不**吞异常。</para>
        /// </summary>
        /// <param name="dataProvider">生产装载面;<c>null</c> ⇒ 磁盘兜底(见 <see cref="Create"/>)。</param>
        /// <param name="sizeX">世界 X 格数(**来自几何产物**,不是常量 —— 2026-10-29 起)。</param>
        /// <param name="sizeY">世界 Y 格数。</param>
        /// <param name="sizeZ">世界 Z 格数。</param>
        /// <param name="note">成功时的人读摘要(失败时经异常承载,本参数为 null)。</param>
        /// <exception cref="InvalidOperationException">产物缺失(E-13 口径 —— provider / 磁盘同款)。</exception>
        /// <exception cref="InvalidDataException">产物损坏 / 空表 / 行越出世界几何。</exception>
        private static BuildSlotCatalog LoadSlotCatalog(
            IDataProvider dataProvider, int sizeX, int sizeY, int sizeZ, out string note)
        {
            note = null;
            // ⚠️ 生产优先:provider 走 `data-core`(玩家构建里唯一存在的路径);
            //    磁盘兜底只服务编辑器 / 夹具 —— 两条路读的是**同一份 cooked 字节 + 同一 codec**。
            WorldBuildSlotsDataSet data = dataProvider != null
                ? dataProvider.Load<WorldBuildSlotsDataSet>()
                : DataCookedDisk.LoadBuildSlots();

            if (data.Rows.Count == 0)
                throw new InvalidDataException(
                    $"`{WorldBuildSlotsDataSet.CookedFileName}` 行数为 0 —— 空骨架等于没装(codec 应已拦)");

            var catalog = new BuildSlotCatalog();
            int bakedInitialCount = 0;
            for (int i = 0; i < data.Rows.Count; i++)
            {
                WorldBuildSlotRow row = data.Rows[i];

                // 与**几何产物**对账(2026-10-29 起:对账对象从「工厂造的常量尺寸」换成
                // 「世界实际尺寸」)。越界槽位在运行期永远不可达(玩家走不到)。
                if (row.Cell.X < 0 || row.Cell.X >= sizeX ||
                    row.Cell.Y < 0 || row.Cell.Y >= sizeY ||
                    row.Cell.Z < 0 || row.Cell.Z >= sizeZ)
                    throw new InvalidDataException(
                        $"槽位格 ({row.Cell.X},{row.Cell.Y},{row.Cell.Z}) 越出世界几何 " +
                        $"{sizeX}×{sizeY}×{sizeZ} —— 槽位数据与几何产物不是同一张图纸");

                catalog.RegisterSlot(new BuildSlot(
                    row.Cell, new BitMask(row.AllowedMask),
                    bakedInitial: row.BakedInitial, moduleId: row.ModuleId));
                if (row.BakedInitial) bakedInitialCount++;
            }

            note = $"槽位 {catalog.Count} 格(baked_initial {bakedInitialCount} 格,待装配期应用)," +
                   $"声明范围 {data.Width}×{data.Height}×{data.Depth}," +
                   $"ConfigVersion = 0x{data.ConfigVersion:X8}";
            return catalog;
        }

        /// <summary>装载源的人读标签(横幅 / LogError 用)。
        /// <para>⚠️ 存在的理由:两条装载路**都会**成功,「槽位 13 格」这句话在两条路上同形 ——
        /// 不标源就无法从日志判断生产路径(provider)到底有没有被走到。</para></summary>
        private static string SlotSourceLabel(IDataProvider dataProvider)
            => dataProvider != null ? "data-core Addressables" : "DataCooked 磁盘兜底(仅编辑器 / 夹具)";
    }
}
