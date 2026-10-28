// 最小合成世界 —— **装配期**的世界实例工厂(S20-17 · 2026-10-28)。
//
// ═══════════════════════════════════════════════════════════════════════════
// 为什么需要这个文件
// ═══════════════════════════════════════════════════════════════════════════
// `CompositionRoot.cs:787-796` 逐字记着:23 的 `StructureInstanceRegistry` 在生产树
// **零构造**(`new World` 的构造同样零构造),并指名「`BootRoot` 的 World 场景装配轮」。
// `impl-s20-2a-building01-2026-10-25.md:176` 的 N5 条把这件事记为「本件未做」,
// 而 `TD-building-01` 已于 2026-10-25 结案 ⇒ 该缺口今日**零载体**。
// 本文件即该轮的第一块:让 `World` 在生产装配路径上**存在**。
//
// ═══════════════════════════════════════════════════════════════════════════
// 为什么是「合成」而不是「装载」—— 这一条必须读懂再改
// ═══════════════════════════════════════════════════════════════════════════
// 世界几何的真源 = ADR-022 关卡工具产出的 `world_geometry.json` → ADR-014 烘焙 →
// `world_*.cooked`。今日**三样全无**:`assets/data/` 零份 `world_*` JSON、
// `unity/Assets/DataCooked/` 的 25 份 cooked 产物里零份世界数据,
// 而关卡工具已被裁定「不立 epic、不排期」(`p0-remaining-roadmap-2026-10-20.md:1088`)。
// ⇒ 装配轮**没有可装载的输入**。用户 2026-10-28 裁定:边界取「最小合成世界」。
//
// ⚠️ **本工厂不是在给 `WorldGeometry` 开后门**。`WorldLattice.cs:295-298` 的
//    `WorldGeometry` 文档逐字写着「刻意**没有**『按尺寸造一个空白世界』的入口:
//    未装载的世界若默认可走,烘焙缺数据就从『启动期硬失败』退化成『全世界默认可走』
//    的静默失败」。那条约束**仍然成立,本文件不修改它** —— `WorldGeometry` 依旧只从
//    成形数组构造,依旧在装载期做量程 / 长度校验。
//    差别在于**谁在承担「我知道这是合成的」这件事**:
//      · `WorldGeometry` 的空白入口会让**任何**调用方都悄悄拿到一个可走世界;
//      · 本工厂只服务**装配期这一处**,且强制配一条响亮日志(见下)。
//
// ⚠️ **响亮登记(禁静默)**:`Create()` 每次都打一条 `LogWarning`。理由 ——
//    一个全 walkable 的 16×1×16 世界与「真世界」在 `IsEffectivelyWalkable` 上不可区分,
//    若不打日志,将来「世界变成空的 / 建造永远合法」这类症状会被误诊为逻辑缺陷,
//    而不是「世界数据从未产出」。这条日志就是那个诊断锚点。
//    `BootRoot` 另置 `IsSyntheticWorld` 可观测属性(PlayMode 可断言)。
//
// ═══════════════════════════════════════════════════════════════════════════
// 边界(本卡不解除的,逐条如实)
// ═══════════════════════════════════════════════════════════════════════════
// · ~~`BuildSlotCatalog` 是**空目录**(`RegisterSlot` 零调用)⇒ `IsInBuildSlotRegion`
//   恒 false~~(**2026-10-28 S20-20 起过期**,原文不删):槽位骨架现从
//   `world_buildslots.cooked.bytes` 装载并 `RegisterSlot`(产物由关卡工具导出 + 烘焙)。
//   ⚠️ 但仍**只**是 13 格骨架 —— `AC-23-08` 第三半边的覆盖面 = 那 13 格,不是「任意格」。
// · 地形全 walkable + POI / 资源格全 `-1` ⇒ `EcozoneOf` / 资源点查询在本世界上
//   只有「没有」这一个答案 —— 这不是「已实现」,是「没有数据」。
// · 本工厂**不**提供任何写入面:世界内容只能由后续关卡工具轮替换。

using System;
using System.IO;   // InvalidDataException(装载期硬失败的异常类型)
using DaYiJingCheng.Gameplay.Presentation;   // DataCookedDisk / WorldBuildSlotsDataSet
using DaYiJingCheng.Sim.Contracts;           // IDataProvider(槽位骨架的生产装载面)
using DaYiJingCheng.Sim.World;
using UnityEngine;   // Debug.LogWarning(本文件属装配层,门 A 不约束)

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>
    /// 装配期最小合成世界工厂(S20-17)。
    /// <para>产出 <see cref="World"/>:16×1×16 全 walkable 地形 + 空槽位目录 +
    /// 空结构实例表。用地取代「世界烘焙数据」—— 后者今日零产出,唯一作者
    /// ADR-022 关卡工具已被裁定不排期。</para>
    /// <para>⚠️ <b>每次调用都打一条 <see cref="Debug.LogWarning(string)"/></b>:合成世界
    /// 与真世界在可走性查询上不可区分,不登记就会被误诊为逻辑缺陷。详见文件头。</para>
    /// </summary>
    public static class MinimalWorldFactory
    {
        /// <summary>合成世界的 X 轴格数。</summary>
        /// <remarks>选 16 而非 1:单格世界会让「足迹格 / 非锚点格」类判据退化成
        /// 同义反复(`place` 与 `OCCUPY` 落在同一格)。16×16 容得下一个 2×2 模组的整足迹。</remarks>
        public const int SizeX = 16;

        /// <summary>合成世界的 Y 轴格数。</summary>
        /// <remarks>1 = 单层平面。P0 的医馆是平面为主形态(`technical-preferences.md` §XR
        /// 「平面为主形态」),不做多层世界。</remarks>
        public const int SizeY = 1;

        /// <summary>合成世界的 Z 轴格数。</summary>
        public const int SizeZ = 16;

        /// <summary>建造一个最小合成世界。</summary>
        /// <param name="dataProvider">槽位骨架的**生产**装载面(data-core Addressables);
        /// <c>null</c> ⇒ 退回 <see cref="DataCookedDisk"/> 的磁盘路径(**仅编辑器 / 夹具**——
        /// 玩家构建里 <c>Assets/DataCooked</c> 不随包出货,该路径必失败并走「空目录 + 响亮登记」)。
        /// <para>⚠️ 2026-10-28(S20-20 装载面补接):本参数此前**不存在**,于是生产构建**唯一**
        /// 的槽位来源是磁盘兜底 ⇒ 槽位目录恒空、`PlaceableChecker` 恒 `SlotNotFound`。
        /// 生产调用点 = `BootRoot` 启动序(传 `DataCorePreloader.Provider`)。</para></param>
        /// <returns>新的 <see cref="World"/> 实例(每次调用都是独立实例 —— 两份注册表
        /// 就是两份真源,调用方须自己保证只建一次)。</returns>
        public static World Create(IDataProvider dataProvider = null)
        {
            int total = SizeX * SizeY * SizeZ;

            // 全 walkable 地形。kSpeedIdx 取 0(合法下标;本世界的 TerrainSpeedRow 表
            // 不参与任何判据 —— 没有消费者读它,见文件头「边界」)。
            var terrain = new TerrainCell[SizeX, SizeY, SizeZ];
            for (int x = 0; x < SizeX; x++)
                for (int y = 0; y < SizeY; y++)
                    for (int z = 0; z < SizeZ; z++)
                        terrain[x, y, z] = new TerrainCell(true, 0);

            // POI / 资源格全 -1(= 无)。长度须与 terrain 齐,否则 WorldGeometry 装载期抛。
            var poiGrid = new int[total];
            var resourceGrid = new int[total];
            for (int i = 0; i < total; i++)
            {
                poiGrid[i] = -1;
                resourceGrid[i] = -1;
            }

            var geometry = new WorldGeometry(terrain, poiGrid, resourceGrid);

            // ── 槽位骨架:从 cooked 产物装载(S20-20 · 2026-10-28)─────────────────────────
            // 病因:`RegisterSlot` 此前**零生产调用点** ⇒ `PlaceableChecker.Check` 恒
            //   `SlotNotFound` ⇒ 玩家建不了任何东西。本段是那唯一的生产装载点。
            //
            // ⚠️ **失败时保留今日行为(空目录),但**不静默**:异常摘要走 `LogError`,
            //   横幅走 `LogWarning` 并**如实**说明「槽位产物缺失或损坏 ⇒ 目录为空」。
            //   `catch` 面刻意收窄到「装载链路会抛的那几类」—— 编程错误(如 NRE)照常冒泡。
            BuildSlotCatalog slotCatalog;
            bool slotsLoaded;
            string failureSummary = null;
            try
            {
                slotCatalog = LoadSlotCatalog(dataProvider, out failureSummary);
                slotsLoaded = true;
            }
            // ⚠️ **`ArgumentException` 须排除其两个子类**(2026-10-28 代码评审 A-23):
            //    `ArgumentNullException` / `ArgumentOutOfRangeException` 是**编码缺陷**的信号
            //    (某次重构把 null 传进装载链),被本 catch 吞掉后会打出一条
            //    「槽位产物缺失或损坏…修法:先跑关卡工具导出 JSON」—— **把开发者引向重烘产物,
            //    而真因在代码**。上面那句「编程错误照常冒泡」此前**只对 NRE 成立**。
            //    仍保留裸 `ArgumentException`:装载链路自己会抛它(`DataCookedDisk` 的空产物名)。
            //    `NotSupportedException` 已删 —— 全链路无已证实的抛出源,留着只是扩面。
            catch (Exception ex) when (IsRecoverableSlotLoadFailure(ex))
            {
                slotCatalog = new BuildSlotCatalog();   // 空目录(与今日行为一致)
                slotsLoaded = false;
                failureSummary = $"{ex.GetType().Name}: {ex.Message}";
                Debug.LogError(
                    "[S20-20][合成世界] 槽位骨架装载失败 ⇒ 槽位目录为空," +
                    "PlaceableChecker 将恒 SlotNotFound(玩家建不了任何东西)。"
                    + "装载源 —— " + SlotSourceLabel(dataProvider) + "。"
                    + "异常摘要 —— " + failureSummary);
            }

            if (slotsLoaded)
            {
                Debug.LogWarning(
                    "[S20-20][合成世界] World 的**几何仍是合成的最小几何** —— "
                    + $"{SizeX}×{SizeY}×{SizeZ} 全 walkable,POI / 资源格全无。"
                    + $"但**建造槽位骨架来自烘焙产物** `{WorldBuildSlotsDataSet.CookedFileName}`"
                    + $"(槽位 {slotCatalog.Count} 格;装载源 = {SlotSourceLabel(dataProvider)};"
                    + "唯一作者 = ADR-022 关卡工具)。"
                    + "⇒ 「槽位判定」这一面有真数据;「地形 / 生态区 / 资源点」仍无数据。"
                    + "横幅出现属**预期**,不是故障;世界几何数据落地后本工厂应被装载路径替换。");
            }
            else
            {
                Debug.LogWarning(
                    "[S20-20][合成世界] World 用**最小合成几何**构造 —— "
                    + $"{SizeX}×{SizeY}×{SizeZ} 全 walkable;"
                    + "**槽位产物缺失或损坏 ⇒ 槽位目录为空**(原因见上一条 LogError)。"
                    + "⇒ 建造槽位判定恒为「不在区域内」,AC-23-08 第三半边无载体。"
                    // ⚠️ 本节**刻意不写作者态数据目录的字面路径**(2026-10-28 全量回归抓出):
                    //    `RuntimeSourceGuard`(AC-21a-48)扫运行期源码的**字符串字面量**里有没有
                    //    `assets` + `/data` 连写 —— 它是「玩家构建零 JSON 直读」的判据,
                    //    注释会被剥离、字面量不会。诊断文案里提路径同样算命中 ⇒ 改写指向文件名。
                    + "修法:先跑关卡工具导出 `world_buildslots.json`(作者态数据目录),"
                    + "再跑「烘焙 world_buildslots」菜单"
                    // ⚠️ 2026-10-28(S20-20 装载面):走生产装载面时,**组条目**是第二个独立失败源
                    //    —— 产物在盘上但没进 `data-core` 组 ⇒ `FetchBytes` 查不到 ⇒ 同一条 LogError。
                    //    不写这句,开发者会去重烘一份本来就在的产物。
                    + (dataProvider != null
                        ? ",最后跑「确保 data-core Addressables 组」(新产物在盘上 ≠ 在组里)"
                        : "")
                    + "。横幅出现属**预期**,不是故障。");
            }

            return new World(geometry, slotCatalog);
        }

        /// <summary>
        /// 「装载失败是否可降级」。**这是那条 `catch` 判据的唯一出处**(不内联在 catch 里)。
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
        /// (承 `AssemblyGates` 的先例 —— 「EditMode 测试经 public 面直调检查器,
        /// 免 `InternalsVisibleTo`」)。若改回 `internal`,夹具将编译失败而非静默跳过。</para>
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
        /// <para>⚠️ 任何不合格(空表 / 行落在合成世界几何之外)⇒ 显式抛,由 <see cref="Create"/>
        /// 决定「降级为空目录 + 响亮登记」—— 本方法**不**吞异常。</para>
        /// </summary>
        /// <param name="dataProvider">生产装载面;<c>null</c> ⇒ 磁盘兜底(见 <see cref="Create"/>)。</param>
        /// <param name="note">成功时的人读摘要(失败时经异常承载,本参数为 null)。</param>
        /// <exception cref="InvalidOperationException">产物缺失(E-13 口径 —— provider / 磁盘同款)。</exception>
        /// <exception cref="InvalidDataException">产物损坏 / 空表 / 行越出合成世界几何。</exception>
        private static BuildSlotCatalog LoadSlotCatalog(IDataProvider dataProvider, out string note)
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

                // 与**本工厂**的合成几何对账:产物声明 16×1×16,工厂也造 16×1×16。
                // 越界槽位在运行期永远不可达(玩家走不到),那是数据与世界的静默错配。
                if (row.Cell.X < 0 || row.Cell.X >= SizeX ||
                    row.Cell.Y < 0 || row.Cell.Y >= SizeY ||
                    row.Cell.Z < 0 || row.Cell.Z >= SizeZ)
                    throw new InvalidDataException(
                        $"槽位格 ({row.Cell.X},{row.Cell.Y},{row.Cell.Z}) 越出合成世界 " +
                        $"{SizeX}×{SizeY}×{SizeZ} —— 槽位数据与工厂几何不是同一张图纸");

                catalog.RegisterSlot(new BuildSlot(row.Cell, new BitMask(row.AllowedMask)));
                if (row.BakedInitial) bakedInitialCount++;
            }

            // ⚠️ `BuildSlotCatalog` / `BuildSlot` **今日无承载 `BakedInitial` 的字段**
            //   ⇒ 数据声明了初始占用,运行期**不应用**。这里响亮登记,不让它静默。
            if (bakedInitialCount > 0)
                Debug.LogWarning(
                    $"[S20-20][合成世界] 槽位骨架里有 {bakedInitialCount} 格标了 `baked_initial = true`," +
                    "但 `BuildSlotCatalog` 尚无承载该标记的字段 ⇒ **运行期不应用初始占用**" +
                    "(开档不会自动生成既有家具 / 外壳)。该缺口已登记,不在本工厂静默吞掉。");

            note = $"槽位 {catalog.Count} 格(baked_initial {bakedInitialCount} 格未应用)," +
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
