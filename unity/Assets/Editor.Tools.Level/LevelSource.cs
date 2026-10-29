// 关卡工具 —— **声明式关卡源**(ADR-022 §二:逻辑层整数数据的唯一作者面)。
//
// 权威来源:
//   ADR-022 §二(逻辑层「只能由关卡工具编写并导出」)· §三(导出契约 `assets/data/world_*.json`
//     含 **初始占用标记 `BakedInitial`**)· ADR-015 §三(全部坐标为整数格 `WorldPos(i32,i32,i32)`)
//
// ⚠️ **槽位类型 = `Sim.World.SlotType` 本身,不建镜像**(2026-10-28 代码评审 A-21 修复)
//    ~~本程序集刻意零引用(`references: []`)⇒ 下面那个 `SlotType` 是 Sim 侧枚举名的镜像,
//    镜像若与 Sim 漂移,表现为烘焙期硬失败,不是静默错配。~~
//    **那段自述对「名字漂移」成立,对「序数漂移」不成立** —— 烘焙期曾按「名字表下标 + 1」
//    自产序数,与运行期 `BitMask.Single(type) => 1 << (int)type` 之间**没有任何门**;
//    Sim 侧在枚举中间插值 ⇒ 掩码与类型位整体错位、五条域门全绿 ⇒ **静默错配**。
//    现改为**直接引用 `Sim`**(`Editor.Tools.Level.asmdef` 的 `references: ["Sim"]`),
//    全仓只剩**一个** `SlotType` 定义,序数由 CLR 枚举给出,漂移在**编译期**即不可能。
//    ⚠️ 依据:ADR-022 §①「关卡工具 `UnityEngine` / UnityEditor 引用**自由** —— 门 A 不约束,
//    因其**不进构建**」;工具族引用集变更按 ADR-025 §① 登记(同 `Editor.Tools.Gates` +
//    `Unity.Addressables` 的先例)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.World;   // SlotType(唯一真源;ADR-022 §① 允许编辑期工具引用)

namespace DaYiJingCheng.EditorTools.Level
{
    /// <summary>一条槽位声明(整数格 + 类型 + 初始占用标记 + 初始家具模块 id)。</summary>
    public readonly struct SlotSpec
    {
        /// <summary>格 x(i32)。</summary>
        public readonly int X;
        /// <summary>格 y(i32)。</summary>
        public readonly int Y;
        /// <summary>格 z(i32)。</summary>
        public readonly int Z;
        /// <summary>槽位类型(**不得**为 <see cref="SlotType.None"/>)。</summary>
        public readonly SlotType Type;

        /// <summary>初始占用标记(ADR-022 §三:`BakedInitial`;开档既有家具 / 外壳)。</summary>
        public readonly bool BakedInitial;

        /// <summary>
        /// 开档既有家具的模块 id(`BakedInitial == true` 时必填;否则须为
        /// <see cref="NoModule"/>)。
        /// <para>🔁 <b>2026-10-28(S20-20 挂标 #2)新增</b>(schema 1→2):此前只声明「这格开档
        /// 已占用」,不声明「被什么占用」⇒ 运行期即使应用基线也算不出足迹、更发不出
        /// `structure_id`(GDD 规则三要求初始家具进实例表)。</para>
        /// </summary>
        public readonly int ModuleId;

        /// <summary>「本槽位开档无既有家具」的哨兵值(与 `Sim.World.BuildSlot.NoModule` 同值)。</summary>
        public const int NoModule = -1;

        /// <summary>构造一条槽位声明。</summary>
        /// <param name="moduleId">`BakedInitial == true` 时须 ≥ 1;否则留默认
        /// <see cref="NoModule"/>(导出器与绑定器两侧都会拒「true 但无 module_id」)。</param>
        public SlotSpec(int x, int y, int z, SlotType type, bool bakedInitial, int moduleId = NoModule)
        {
            X = x; Y = y; Z = z; Type = type; BakedInitial = bakedInitial; ModuleId = moduleId;
        }

        /// <summary>格坐标(整数三元组)。</summary>
        public (int X, int Y, int Z) Cell => (X, Y, Z);
    }

    /// <summary>声明式关卡源:尺寸 + 槽位表(纯整数,零 float)。</summary>
    public sealed class LevelSource
    {
        /// <summary>关卡 id(导出 JSON 的 `level_id`;非空)。</summary>
        public readonly string LevelId;

        /// <summary>格数 X(≥ 1)。</summary>
        public readonly int Width;
        /// <summary>格数 Y(≥ 1)。</summary>
        public readonly int Height;
        /// <summary>格数 Z(≥ 1)。</summary>
        public readonly int Depth;

        /// <summary>槽位表(声明序;导出时按 (x,y,z) 升序重排)。</summary>
        public readonly IReadOnlyList<SlotSpec> Slots;

        /// <summary>
        /// 每格可走性(**整数布尔** 0/1;长度须 = `Width×Height×Depth`;索引 = `(x*H + y)*D + z`,
        /// 与运行期 <c>WorldGeometry.ToIndex</c> 同一公式)。
        /// <para>🔁 <b>2026-10-29(story-002 AC-1)新增</b>。`null` = **本关卡未声明几何**
        /// —— 此时 <see cref="LevelExporter.ExportGeometry"/> **硬失败**(不合成「全可走」:
        /// 那会让「可走性来自产物」在 AC-4 的判据上不可判别,承 ADR-022 §二「工具是唯一作者」)。</para>
        /// <para>⚠️ 用 <c>int</c> 而非 <c>bool[]</c>:导出面要求「零浮点 + 纯整数」,
        /// 而 `bool[]` 到 JSON 的映射需要读方猜约定;0/1 是自描述的。</para>
        /// </summary>
        public readonly IReadOnlyList<int> Walkable;

        /// <summary>
        /// 地形声明(`TERRAIN_TABLE` + 每格 `terrain_id` + 控制器参数量化值)。
        /// <para>🔁 <b>2026-10-29(story-002 AC-2)新增</b>。`null` = **本关卡未声明地形**
        /// —— 此时 <see cref="LevelExporter.ExportTerrain"/> **硬失败**(F-6-2:`max over` 空集无定义)。
        /// 否则其 <see cref="TerrainSource.CellTerrainId"/> 长度须与本关卡的格总数相等。</para>
        /// </summary>
        public readonly TerrainSource Terrain;

        /// <summary>
        /// 生态区多边形声明(ADR-022 §三 `world_ecozones.json`)。
        /// <para>🔁 <b>2026-10-29(story-003 AC-1)新增</b>。`null` = **本关卡未声明生态区**
        /// —— 此时 <see cref="LevelExporter.ExportEcozones"/> **硬失败**(不合成空表:
        /// 空表与「未声明」在产物上不可分,而 `EcozoneOf` 恒 `None` 正是本 story 要消灭的状态)。</para>
        /// </summary>
        public readonly EcozoneSource Ecozones;

        /// <summary>
        /// POI **定义**集(ADR-022 §三 `world_poi.json`;ADR-021 §①:定义 = 派生态)。
        /// <para>🔁 <b>2026-10-29(story-004 AC-1)新增</b>。`null` = **本关卡未声明 POI**
        /// —— 此时 <see cref="LevelExporter.ExportPoi"/> **硬失败**(不合成空表:
        /// 空表会让 `PoiStateMachine` 的 `_poiIdSet` 零成员,`PoiStateChanged` 永不发出,
        /// 而「零 POI」与「未声明」在产物上不可分)。</para>
        /// <para>⚠️ 只承载**定义**;**状态**(`PoiState`)住世界流,唯一写者 = 系统 6
        /// —— 本字段在类型层面就够不到状态(story-004 AC-6)。</para>
        /// </summary>
        public readonly PoiSource Poi;

        /// <summary>
        /// 资源点集(ADR-022 §三 `world_resources.json`)。
        /// <para>🔁 <b>2026-10-29(story-004 AC-2)新增</b>。`null` = **本关卡未声明资源**
        /// —— 此时 <see cref="LevelExporter.ExportResources"/> **硬失败**(理由同 <see cref="Poi"/>)。</para>
        /// </summary>
        public readonly ResourceSource Resources;

        /// <summary>
        /// 格边长 `LATTICE_SIZE`(**整数毫米**;ADR-015 §三 / GDD F-6-1)。
        /// <para>🔁 <b>2026-10-29(story-002 AC-1)新增</b>。`0` = 未声明(几何导出硬失败)。</para>
        /// <para>⚠️ **数值归用户数值轮**(GDD 同时用它推 F-6-1 的隧穿下界)。本类只承载。</para>
        /// </summary>
        public readonly int LatticeSizeMm;

        /// <summary>
        /// chunk 边长 X(格)。
        /// <para>🔁 <b>2026-10-29(story-005 AC-2②)新增</b>。`0` = 未声明(几何 / 导航格导出均硬失败)。</para>
        /// <para>⚠️ 三个分量**必须同时**声明或**同时**为零 —— chunk 是三维划分,
        /// 单轴为 0 会让 `ChunkTopology.ForWorld` 抛,而那属于作者错误、不是可选状态。</para>
        /// </summary>
        public readonly int ChunkSizeX;
        /// <summary>chunk 边长 Y(格);见 <see cref="ChunkSizeX"/>。</summary>
        public readonly int ChunkSizeY;
        /// <summary>chunk 边长 Z(格);见 <see cref="ChunkSizeX"/>。</summary>
        public readonly int ChunkSizeZ;

        /// <summary>
        /// 视觉层逐格占用声明(C1 的比对对象)。
        /// <para>🔁 <b>2026-10-29(story-006 AC-3)新增</b>。`null` = **本关卡未声明视觉层**
        /// —— 此时 C1 进 `CheckReport.NotRun`,**不冒充绿**(今日全库零视觉层资产,
        /// `World.unity` 只有 `Directional Light` + `Ground`)。</para>
        /// </summary>
        public readonly VisualLayerSource VisualLayer;

        /// <summary>
        /// 模块模板的 collider 足迹声明(C2' 的比对对象)。
        /// <para>🔁 <b>2026-10-29(story-006 AC-2)新增</b>。`null` = **未声明足迹**
        /// —— 此时 C2' 进 `CheckReport.NotRun`。⚠️ 声明与**模块 prefab** 是否一致
        /// **本批未跑**(prefab 归 23 素材轮,今日全库零 prefab)。</para>
        /// </summary>
        public readonly IReadOnlyList<ModuleFootprintSpec> ModuleFootprints;

        /// <summary>世界总格数(`Width × Height × Depth`;越界即抛,见构造)。</summary>
        public readonly int TotalCells;

        /// <summary>
        /// 构造关卡源。尺寸非法 / 槽位表为 null ⇒ 抛(作者错误,不当默认值吞掉)。
        /// <para>几何三件(`walkable` / `terrain` / `latticeSizeMm`)为 `null`/`0` ⇒
        /// **允许构造但几何导出会硬失败** —— 只声明槽位的关卡(以及只测槽位子集的夹具)是合法用法。</para>
        /// </summary>
        /// <param name="walkable">每格可走性(0/1;长度须 = 世界格数;为 <c>null</c> = 未声明)。</param>
        /// <param name="terrain">地形声明(为 <c>null</c> = 未声明)。</param>
        /// <param name="latticeSizeMm">格边长(毫米;`0` = 未声明)。</param>
        /// <exception cref="ArgumentException">`walkable` 含非 0/1 值 / 长度不符 / 地形每格数组长度不符
        /// / `latticeSizeMm` 为负。</exception>
        public LevelSource(
            string levelId, int width, int height, int depth, IReadOnlyList<SlotSpec> slots,
            IReadOnlyList<int> walkable = null, TerrainSource terrain = null, int latticeSizeMm = 0,
            EcozoneSource ecozones = null, PoiSource poi = null, ResourceSource resources = null,
            int chunkSizeX = 0, int chunkSizeY = 0, int chunkSizeZ = 0,
            VisualLayerSource visualLayer = null, IReadOnlyList<ModuleFootprintSpec> moduleFootprints = null)
        {
            if (string.IsNullOrEmpty(levelId))
                throw new ArgumentException("level_id 不得为空", nameof(levelId));
            if (width < 1 || height < 1 || depth < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(width), $"世界尺寸须 ≥ 1(实得 {width}×{height}×{depth})");
            LevelId = levelId;
            Width = width; Height = height; Depth = depth;
            Slots = slots ?? throw new ArgumentNullException(nameof(slots));

            TotalCells = ComputeTotalCells(width, height, depth);
            ValidateLatticeSize(latticeSizeMm);

            Walkable = CopyWalkable(walkable, TotalCells);
            Terrain = ValidateTerrain(terrain, TotalCells);

            LatticeSizeMm = latticeSizeMm;
            Ecozones = ecozones;
            Poi = poi;
            Resources = resources;

            VisualLayer = ValidateVisualLayer(visualLayer, TotalCells);
            ModuleFootprints = moduleFootprints;

            // ⚠️ 这三行**不能**挪进 `ValidateChunkSizes` —— `ChunkSize*` 是 `readonly`,
            //    只能在构造器里赋值(CS0191)。校验与落值因此拆成两步。
            ValidateChunkSizes(chunkSizeX, chunkSizeY, chunkSizeZ);
            ChunkSizeX = chunkSizeX;
            ChunkSizeY = chunkSizeY;
            ChunkSizeZ = chunkSizeZ;
        }

        // ── 构造期的六段校验(2026-10-29 拆分:原 ctor 88 行 > 40)──────────────
        // ⚠️ **各段的执行次序与报文原文逐字未变** —— 次序决定「同时有多处非法时报哪一条」。

        /// <summary>格总数 + `int` 域守卫。</summary>
        /// <remarks>格总数用 long 算再落 int:W×H×D 在 int 上可回绕,回绕值落回 `[0, int.MaxValue]` 后
        /// 与真实格数比较会**静默通过**(承 `WorldGeometry.ToIndex` 的同一条纪律)。</remarks>
        private static int ComputeTotalCells(int width, int height, int depth)
        {
            long total = (long)width * height * depth;
            if (total > int.MaxValue)
                throw new ArgumentOutOfRangeException(
                    nameof(width), $"世界格总数 {total} 超出 int 域 —— 一维索引公式 (x*H+y)*D+z 不可表示");
            return (int)total;
        }

        /// <summary>格边长量化值守卫。</summary>
        private static void ValidateLatticeSize(int latticeSizeMm)
        {
            if (latticeSizeMm < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(latticeSizeMm), $"LATTICE_SIZE 量化值 = {latticeSizeMm} mm 不得为负(0 = 未声明)");
        }

        /// <summary>可走性数组的**长度对账 + 0/1 值域 + 防御性拷贝**;`null` 入参 ⇒ `null` 出参。</summary>
        private static IReadOnlyList<int> CopyWalkable(IReadOnlyList<int> walkable, int totalCells)
        {
            if (walkable == null) return null;

            if (walkable.Count != totalCells)
                throw new ArgumentException(
                    $"每格可走性数组长度 = {walkable.Count},须等于世界格数 {totalCells} —— " +
                    "长度不齐 = 可走性与几何不是同一张图纸", nameof(walkable));

            var copy = new int[walkable.Count];
            for (int i = 0; i < walkable.Count; i++)
            {
                if (walkable[i] != 0 && walkable[i] != 1)
                    throw new ArgumentException(
                        $"每格可走性第 {i} 项 = {walkable[i]} —— 只接受 0/1(不可通行用 0 表达;" +
                        "非 0/1 会让读方猜「非零即真」,而 0b10 之类的位值是真值域里的假值)",
                        nameof(walkable));
                copy[i] = walkable[i];
            }

            return Array.AsReadOnly(copy);
        }

        /// <summary>地形声明的格数对账;`null` 入参 ⇒ `null` 出参。</summary>
        private static TerrainSource ValidateTerrain(TerrainSource terrain, int totalCells)
        {
            if (terrain == null) return null;

            if (terrain.CellTerrainId.Count != totalCells)
                throw new ArgumentException(
                    $"每格 terrain_id 数组长度 = {terrain.CellTerrainId.Count},真值来自 TerrainSource;" +
                    $"本关卡的世界格数 = {totalCells}", nameof(terrain));
            return terrain;
        }

        /// <summary>视觉层声明的格数对账(C1 的比对对象);`null` 入参 ⇒ `null` 出参。</summary>
        /// <remarks>长度对账由 `VisualLayerSource` 自己判(单一出处),这里只比「是不是同一张图纸」。</remarks>
        private static VisualLayerSource ValidateVisualLayer(VisualLayerSource visualLayer, int totalCells)
        {
            if (visualLayer != null && visualLayer.TotalCells != totalCells)
                throw new ArgumentException(
                    $"视觉层声明的世界格数 = {visualLayer.TotalCells},本关卡 = {totalCells} —— " +
                    "两者不是同一张图纸(C1 会比到别人的格上)", nameof(visualLayer));
            return visualLayer;
        }

        /// <summary>chunk 三轴的**全声明或全为零**守卫(落值仍在构造器里,`readonly` 所限)。</summary>
        /// <remarks>⚠️ 半声明(如 X=8 而 Y=0)在 `ChunkTopology.ForWorld` 必抛,但那时报文指向
        /// 「数值非法」而不是「你漏了一个轴」—— 在构造期分开判,报文才指得准。</remarks>
        private static void ValidateChunkSizes(int chunkSizeX, int chunkSizeY, int chunkSizeZ)
        {
            if (chunkSizeX < 0 || chunkSizeY < 0 || chunkSizeZ < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(chunkSizeX),
                    $"chunk 尺寸不得为负(实得 {chunkSizeX}×{chunkSizeY}×{chunkSizeZ};0 = 未声明)");

            bool allZero = chunkSizeX == 0 && chunkSizeY == 0 && chunkSizeZ == 0;
            bool allPositive = chunkSizeX >= 1 && chunkSizeY >= 1 && chunkSizeZ >= 1;
            if (!allZero && !allPositive)
                throw new ArgumentException(
                    $"chunk 尺寸必须三轴**同时**声明或同时为零(实得 {chunkSizeX}×{chunkSizeY}×{chunkSizeZ})" +
                    " —— 半声明会让「chunk 划分」在某一轴上没有定义", nameof(chunkSizeX));
        }

        /// <summary>
        /// 本关卡的 chunk 划分(`ChunkTopology.ForWorld` —— 几何侧 / 导航格侧**同源**)。
        /// <para>⚠️ 方法名**不得**叫 `ChunkTopology`:那会让方法名遮蔽类型名,
        /// 方法体里的 `ChunkTopology.ForWorld(...)` 被解析成「对方法取成员」⇒ CS0119。</para>
        /// </summary>
        /// <exception cref="InvalidOperationException">未声明 chunk 尺寸。</exception>
        public ChunkTopology GetChunkTopology()
        {
            if (ChunkSizeX <= 0 || ChunkSizeY <= 0 || ChunkSizeZ <= 0)
                throw new InvalidOperationException(
                    $"[关卡工具] {LevelId} 未声明 chunk 尺寸 ⇒ 无法划分 chunk" +
                    "(story-005 AC-2②:几何侧与导航格侧须用**同一个**划分函数)");
            return ChunkTopology.ForWorld(Width, Height, Depth, ChunkSizeX, ChunkSizeY, ChunkSizeZ);
        }

        /// <summary>
        /// 格坐标 → 一维索引(`(x*H + y)*D + z`);与 <see cref="Walkable"/> /
        /// <see cref="TerrainSource.CellTerrainId"/> / 运行期 <c>WorldGeometry.ToIndex</c> **同一公式**。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">坐标越界(不做静默钳位 —— 越界即作者错误)。</exception>
        public int CellIndex(int x, int y, int z)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height || z < 0 || z >= Depth)
                throw new ArgumentOutOfRangeException(
                    nameof(x), $"格 ({x},{y},{z}) 超出 {Width}×{Height}×{Depth}");
            return (x * Height + y) * Depth + z;
        }
    }
}
