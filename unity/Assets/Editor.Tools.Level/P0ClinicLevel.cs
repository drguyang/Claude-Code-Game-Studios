// P0 医馆 —— 建造槽位骨架的**具体种子**(关卡工具的声明式数据)。
//
// 权威来源:
//   ADR-022 §二(唯一作者 = 关卡工具)· §三(导出契约含 `BakedInitial`)
//   ADR-015 §三(整数格)· GDD modular-building.md 规则一(槽位骨架)
//
// ⚠️ **规模刻意最小**:只铺 13 格(9 格的 Decor 3×3 区 + 4 格的 Shell 条),落在
//    `MinimalWorldFactory` 的 16×1×16 合成世界内,**其余 243 格一律非槽位** ——
//    「非槽位格」是既有夹具(`test_acB2_placeOnNonSlotCell_returnsSlotNotFoundWithZeroSlots` 类)
//    仍然有意义的必要条件。铺满 = 让「不在区域内」这条判据失去观察面。
//
// ⚠️ **`BakedInitial` 不是恒 `true`**(2026-10-28 收口轮订正,原稿恒 true —— 原文不删):
//    原稿把 13 格全标 `baked_initial = true`,读起来的语义是「开档时整间医馆**已建满**」
//    ⇒ 一旦运行期真的开始应用初始占用,玩家**没有一格可建**,这与本轮的目的
//    (「让玩家今天建得了东西」)直接相反。现种子按**语义**分:
//      · **Shell 4 格 = `true`** —— 外壳(墙 / 地基)在开档时**就存在**,这是外壳的定义;
//      · **Decor 9 格 = 1 格 `true` + 8 格 `false`** —— 一格里已摆着家具(证明 `true` 分支有真实取值),
//        其余 8 格是**空地**(证明 `false` 分支可建)。
//    ⚠️ ~~但 **运行期今日仍不应用初始占用**(`BuildSlotCatalog` 没有承载它的字段)——
//    `MinimalWorldFactory` 因此在遇到 `BakedInitial == true` 的行时**打一条 LogWarning**,
//    不让「数据声明了、运行期没落地」变成静默。该缺口登记在交付报告里。~~
//    **🔁 2026-10-28(S20-20 挂标 #2)该缺口已闭合,原文不删**:`BuildSlot` 现承载
//    `BakedInitial` + `ModuleId`(schema 2),`World.ApplyBakedInitial` 在生产装配路径上
//    应用基线(占足迹 + 进实例表 + 确定性发号)。`MinimalWorldFactory` 的那条 LogWarning
//    已随之删除(它报的正是这个已闭合的缺口)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;   // Fix(地形表的 K_accel / K_decel;ADR-014 §二)
using DaYiJingCheng.Sim.World;       // SlotType(唯一真源,2026-10-28 评审 A-21)

namespace DaYiJingCheng.EditorTools.Level
{
    /// <summary>P0 医馆的槽位骨架种子。</summary>
    public static class P0ClinicLevel
    {
        /// <summary>关卡 id(导出 JSON 的 `level_id`;**同时是** <see cref="LevelCatalog.CanonicalLevelId"/>)。</summary>
        public const string LevelId = "p0_clinic";

        // 🔁 **2026-10-29(story-001 AC-1)此处删除了三个尺寸常量,原文不删、保持删除。**
        //    删掉的:`public const int Width = 16;` / `Height = 1;` / `Depth = 16;`
        //    (各自带一句「与 `MinimalWorldFactory.SizeX/Y/Z` 同值」的注释)。
        //
        //    **为什么删而不是留着**:那三个常量是「**第二条读尺寸的路**」—— 任何调用方
        //    (或未来的导出器)写 `P0ClinicLevel.Width`,就会绕过 `LevelSource` 这唯一载体,
        //    而那正是 story-001 病灶 ① 的形态(「换一个关卡要改源码」)。留成常量等于留着那条路。
        //    ⇒ 尺寸现在**只有一个出处** = `Define()` 里构造 `LevelSource` 的那三个实参;
        //      夹具 `level_source_generalization_test::test_ac1_*` 用反射钉住「本类不再有这三个成员」。
        //
        //    ⚠️ 「与 `MinimalWorldFactory.SizeX/Y/Z` 同值」这条**约束本身仍然有效** ——
        //    它只是不再是「两个常量同值」,而是「运行期合成世界的尺寸 == 本关卡的声明尺寸」。
        //    该等式由 `MinimalWorldFactory` 侧的既有装载链与夹具覆盖;本工具**不引用**运行期装配
        //    (工具只产出数据,不读运行期状态)。

        /// <summary>Decor 3×3 区的原点(x 起)。</summary>
        public const int DecorOriginX = 6;
        /// <summary>Decor 3×3 区的原点(z 起)。</summary>
        public const int DecorOriginZ = 6;

        /// <summary>Decor 区里**唯一一格初始占用**(开档已摆着家具);其余 8 格是空地。</summary>
        /// <remarks>⚠️ 语义见文件头:全 true = 医馆已建满 = 玩家无可建格,与本轮目的相反。</remarks>
        public const int DecorPreOccupiedDx = 0;
        /// <summary>见 <see cref="DecorPreOccupiedDx"/>。</summary>
        public const int DecorPreOccupiedDz = 0;

        /// <summary>
        /// 那格初始 Decor 的模块 id(`build_modules.json` 的 7 = `chamber_pot`)。
        /// <para>🔁 <b>2026-10-28(S20-20 挂标 #2)新增</b>(schema 1→2):`baked_initial = true`
        /// 现在**必须**指明是哪件家具 —— 运行期要按它算足迹、并给初始家具发 `structure_id`
        /// (GDD 规则三)。取 `chamber_pot` 的理由:它是单格足迹(`[[0,0,0]]`)、`Decor` 类型、
        /// `refundable = true` —— 玩家对它的第一次改动(第一条 `StructureRemoved`)能走通返还链,
        /// 是规则三「玩家对初始家具的第一次改动 = 第一条 StructureRemoved」的可达面。</para>
        /// </summary>
        public const int DecorPreOccupiedModuleId = 7;

        /// <summary>Shell 条带用的模块 id(`build_modules.json` 的 8 = `refundable: false`)。</summary>
        /// <remarks>⚠️ 外壳 **`refundable = false`** —— 与 GDD 规则三/§「P0 外壳不可拆」一致:
        /// `DemolishChecker` 会按该位拒拆,返还链根本不会启动。</remarks>
        public const int ShellModuleId = 8;

        /// <summary>Shell 条带的 x 起点。</summary>
        public const int ShellStripXStart = 1;
        /// <summary>Shell 条带的长度(格)。</summary>
        public const int ShellStripLength = 4;
        /// <summary>Shell 条带的 z(单行)。</summary>
        public const int ShellStripZ = 1;

        /// <summary>
        /// 产出 P0 医馆的声明式关卡源。
        /// <para>槽位集:① `x ∈ [6,8] × z ∈ [6,8]` 的 **9 格 Decor**(3×3,足以容纳一个 3×3 足迹);
        /// ② `x ∈ [1,4] × z = 1` 的 **4 格 Shell**(外壳分支可达)。</para>
        /// <para>开档基线 = 5 格(4 Shell + 1 Decor),各有真 `module_id`(schema 2)。</para>
        /// </summary>
        public static LevelSource Define()
        {
            var slots = new List<SlotSpec>();

            for (int dx = 0; dx < 3; dx++)
                for (int dz = 0; dz < 3; dz++)
                {
                    bool preOccupied = dx == DecorPreOccupiedDx && dz == DecorPreOccupiedDz;
                    slots.Add(new SlotSpec(
                        DecorOriginX + dx, 0, DecorOriginZ + dz, SlotType.Decor,
                        bakedInitial: preOccupied,
                        moduleId: preOccupied ? DecorPreOccupiedModuleId : SlotSpec.NoModule));
                }

            for (int i = 0; i < ShellStripLength; i++)
                slots.Add(new SlotSpec(
                    ShellStripXStart + i, 0, ShellStripZ, SlotType.Shell,
                    bakedInitial: true, moduleId: ShellModuleId));

            // 尺寸的**唯一出处**(16×1×16,单层平面)。原为 `Width` / `Height` / `Depth` 三个
            // 公共常量,2026-10-29 删除(理由见上方 🔁 注)—— 不要再把它们加回来。
            const int sizeX = 16;
            const int sizeY = 1;
            const int sizeZ = 16;

            return new LevelSource(
                LevelId, sizeX, sizeY, sizeZ, slots,
                walkable: BuildWalkable(sizeX, sizeY, sizeZ),
                terrain: BuildTerrain(sizeX, sizeY, sizeZ),
                latticeSizeMm: LatticeSizeMm,
                ecozones: BuildEcozones(),
                poi: BuildPoi(),
                resources: BuildResources(),
                chunkSizeX: ChunkSizeX,
                chunkSizeY: ChunkSizeY,
                chunkSizeZ: ChunkSizeZ);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 以下三件是 **story-002** 新增的几何 / 地形声明。
        // ⚠️ 全部是**占位形状**,不是设计产物:
        //    · 外墙环的可走性 = 「医馆是一间屋子」的最小表达;
        //    · 单行地形表 = 「暂时只有一种地面」;
        //    · LATTICE_SIZE / slopeLimit / stepOffset 的**具体数值**归**用户数值轮**
        //      (GDD F-6-1 的下界由 LATTICE_SIZE 推,ADR-022 §Out of Scope 逐字点名数值归用户)。
        //    关卡设计轮替换本文件时,这三件整个重写 —— 不是在上面加格子。
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// chunk 边长(格;story-005 AC-2② · ADR-015 §四)。
        /// <para>⚠️ **用户裁定值(2026-10-29)**:`8×1×8` ⇒ 16×1×16 的世界切成
        /// `2×1×2 = **4** 个 chunk`(索引 `0..3`),`data-core` 组条目 31 → 35。</para>
        /// <para>⚠️ 取 `1` 作 Y 轴边长不是偶然:本关卡是**单层**(`Height = 1`),
        /// 用 8 会在 Y 轴上切出一个 7/8 空衬垫的块 —— 那是「chunk 尺寸不随关卡形状」
        /// 的形态,而 chunk 划分的唯一要求是**每格恰好覆盖一次**。</para>
        /// <para>⚠️ 三个数值归**用户裁定**,不由本工具推导:`ChunkTopology.ForWorld` 只负责
        /// 由它们算划分,不替它们取值。</para>
        /// </summary>
        public const int ChunkSizeX = 8;
        /// <summary>见 <see cref="ChunkSizeX"/>。</summary>
        public const int ChunkSizeY = 1;
        /// <summary>见 <see cref="ChunkSizeX"/>。</summary>
        public const int ChunkSizeZ = 8;

        /// <summary>格边长 `LATTICE_SIZE`(整数毫米;GDD F-6-1 / ADR-015 §三)。</summary>
        /// <remarks>⚠️ **占位值,归用户数值轮**。取 1000 mm = 1 米/格:本轮它只需 `> 0`
        /// (`WorldLatticeParams` 的 F-6-1 守卫要求),真实取值须与 `SPEED_MODE_MAX` / `MAX_DT` /
        /// `SAFETY_MARGIN` 一起裁定 —— 那四个量同属数值轮。</remarks>
        public const int LatticeSizeMm = 1000;

        /// <summary>`slopeLimit` 的量化值(0.01 度;量子见 `LatticeParamQuantization`)。</summary>
        /// <remarks>⚠️ **占位值,归用户数值轮**。取 4500(= 45.00°)= **AC-0 spike 实测到的
        /// `CharacterController.slopeLimit` 引擎默认值**。这样 C2(S6)在今天的默认控制器下
        /// 偏差**恰为 0**;玩家控制器一旦改了这个脚本参数,C2 就该红 —— 那正是它要抓的漂移。</remarks>
        public const int SlopeLimitDegX100 = 4500;

        /// <summary>`stepOffset` 的量化值(毫米)。</summary>
        /// <remarks>⚠️ **占位值,归用户数值轮**。取 300(= 0.300 m)= **AC-0 spike 实测到的
        /// `CharacterController.stepOffset` 引擎默认值**,口径同上一条。</remarks>
        public const int StepOffsetMm = 300;

        /// <summary>外轮廓墙环的厚度(格)。</summary>
        /// <remarks>取 1:单层世界的一圈墙。> 1 会让「屋子」在 16×16 里只剩不到 12×12 的内部,
        /// 而本关卡的槽位区(见 <see cref="DecorOriginX"/> 起)不依赖更大的内部。</remarks>
        public const int WallRingThickness = 1;

        /// <summary>
        /// 每格可走性(0/1;索引 = `(x*H + y)*D + z`)。
        /// <para><b>外墙环不可走</b>(距任一边界 &lt; <see cref="WallRingThickness"/>),内部全可走。</para>
        /// <para>⚠️ **为什么这里必须有不可走的格**:story-002 AC-4 的判据是「可走性来自产物,
        /// **不是** 16×1×16 全 true」。若本关卡的声明与旧的合成世界**逐格相同**,那条判据
        /// 在**任何夹具**上都不可判别(同值变异 —— 见 `feedback_refactor_falsifiability_ceiling`)。
        /// 一圈墙让「读到的是产物」这件事在数值上可观测。</para>
        /// </summary>
        private static IReadOnlyList<int> BuildWalkable(int sizeX, int sizeY, int sizeZ)
        {
            var walkable = new int[sizeX * sizeY * sizeZ];
            for (int x = 0; x < sizeX; x++)
                for (int y = 0; y < sizeY; y++)
                    for (int z = 0; z < sizeZ; z++)
                    {
                        bool onRing = x < WallRingThickness || x >= sizeX - WallRingThickness
                                   || z < WallRingThickness || z >= sizeZ - WallRingThickness;
                        walkable[(x * sizeY + y) * sizeZ + z] = onRing ? 0 : 1;
                    }
            return walkable;
        }

        /// <summary>
        /// 本关卡的生态区声明(**两个占位区,共一条边**;story-003)。
        /// <para>⚠️ **不是设计产物**:名称与形状都是占位,`world-ecozones` 的叙事 / 关卡设计轮
        /// 会整个替换(`world-and-ecozones.md` 的生态区内容不在本工具边界内)。</para>
        /// <para>⚠️ **为什么刻意让两区共享一条边**:GDD F-6-3 §二 的「开集 + 裁决序」只有在
        /// **贴边相邻**时才被检验 —— 共享边上每格同属两区**边界**,由 `min(id)` 裁决。
        /// 若两个区各留一格缝,`NONE` 会吞掉这一整类情形,AC-5 的「区内 / 区外两方向」
        /// 也就退化成「有 / 无」两条互不相干的断言。</para>
        /// <para>⚠️ 两区都不覆盖**外环墙格**(`x ∈ {0,15} ∨ z ∈ {0,15}`)⇒ 那些格是
        /// `EcozoneId.None`(真空野地)。`NONE` 是**合法结果**(GDD EC-6-10),不是缺数据。</para>
        /// </summary>
        private static EcozoneSource BuildEcozones()
        {
            // 顶点序 = 逆时针闭合环(**首尾不重复**;重复会让 C4 ⑤ 直接拒)。
            // 只用 x / z;y 恒 0 且不参与判定(GDD F-6-3 §四)。
            static WorldPos P(int x, int z) => new WorldPos(x, 0, z);

            var zones = new List<EcozoneZoneSpec>
            {
                // 医馆前厅:x ∈ [1,8] × z ∈ [1,14]
                new EcozoneZoneSpec(0, "clinic_ward", new[]
                {
                    P(1, 1), P(8, 1), P(8, 14), P(1, 14),
                }),
                // 药圃:x ∈ [8,14] × z ∈ [1,14] —— 与上面**共享 x = 8 这条边**
                new EcozoneZoneSpec(1, "herb_courtyard", new[]
                {
                    P(8, 1), P(14, 1), P(14, 14), P(8, 14),
                }),
            };

            return new EcozoneSource(zones);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 以下两件是 **story-004** 新增的 POI 定义 / 资源点声明。
        // ⚠️ 全部是**占位形状**(承 §上文「数值归用户数值轮」的同一纪律):
        //    · POI 的 `type` 取值闭集归 **6 的 GDD**;本处 `1` / `2` 只是两个**不同的**整数,
        //      用来让「json 里读得回原值」这条判据有判别力(全填同一个数会让它恒真)。
        //    · `guard_ref` 取稳「乙案」= 整数**档位**(story-004 §Implementation Notes):
        //      27 的敌人定义集今日不存在,取甲案会引入未定义的跨系统引用。
        //    · 资源 id `1` / `2` 同理 —— 17 采集的资源类型表里尚无 id 词表。
        //    关卡设计轮 + 数值轮替换本文件时,这两件整个重写,不是在数组里加两条。
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>本关卡的 POI **定义**(三个,两个在前厅一个在药圃)。</summary>
        /// <remarks>
        /// <para>⚠️ 一格一条(`PoiSource` 构造即校验);格坐标必须可走且在两区之内
        /// —— 否则「走近 POI 被发现」的走查(world-ecozones/story-005)没有可站的位置。</para>
        /// <para>⚠️ 本方法**只产出定义**;`PoiState`(未发现 / 已发现 / 已解决)住在世界流,
        /// 唯一写者是系统 6。工具程序集里连这个类型名都不出现(story-004 AC-6)。</para>
        /// </remarks>
        private static PoiSource BuildPoi()
        {
            static WorldPos P(int x, int z) => new WorldPos(x, 0, z);

            var pois = new List<PoiSpec>
            {
                new PoiSpec(0, P(4, 4),  type: 1, guardRef: 2),   // 前厅 · 强守卫
                new PoiSpec(1, P(11, 7), type: 2, guardRef: 1),   // 药圃 · 中守卫
                new PoiSpec(2, P(3, 12), type: 1, guardRef: 0),   // 前厅 · 无守卫
            };
            return new PoiSource(pois);
        }

        /// <summary>本关卡的资源点声明(四个,两区各两个;格与 POI 格互不重叠)。</summary>
        /// <remarks>
        /// <para>⚠️ **一格一条**(与 POI 同款):运行期 `ResourceGrid` 每格只有一个 `int`,
        /// 同格两条会被静默丢弃 ⇒ `ResourceSource` 构造期即拒。</para>
        /// <para>⚠️ `ecozone_id` 只引用、不校验(存在性归 S6 的 C 系列,见 `ResourceSource.cs` 文件头)。</para>
        /// </remarks>
        private static ResourceSource BuildResources()
        {
            static WorldPos P(int x, int z) => new WorldPos(x, 0, z);

            var rows = new List<ResourceSpec>
            {
                new ResourceSpec(1, P(2, 2),  ecozoneId: 0),
                new ResourceSpec(2, P(10, 2), ecozoneId: 1),
                new ResourceSpec(2, P(12, 12), ecozoneId: 1),
                new ResourceSpec(1, P(5, 9),  ecozoneId: 0),
            };
            return new ResourceSource(rows);
        }

        /// <summary>本关卡的地形声明(单行表 `terrain_id = 0`;每格都指向它)。</summary>
        /// <remarks>
        /// <para>⚠️ **单行表 = 占位**。`K_speed = 1` 是 F-6-2 的**基准地貌**
        /// (AC-6-08 要求 `K_speed == 1` 恰好一行),`K_accel` / `K_decel` 取 `1`(恒等)。
        /// ⇒ `K_TERRAIN_MAX` 今天恒 = 1。这不是缺陷:它是「数值未裁」的如实表达,
        /// 首个真实地貌(木地板 / 泥土 / 石阶)归用户数值轮 + 关卡设计轮。</para>
        /// <para>⚠️ 每个人都指向 `terrain_id = 0`(含不可走的墙环格):**可走性与地貌正交**
        /// —— 墙也有地面材质,只是走不上去。用「墙的 terrain_id 也必须是 0」换取
        /// 「表只有一行」,比给墙编一个 `K_speed ≥ 2` 的假地貌诚实。</para>
        /// </remarks>
        private static TerrainSource BuildTerrain(int sizeX, int sizeY, int sizeZ)
        {
            int total = sizeX * sizeY * sizeZ;
            var rows = new List<TerrainRowSpec>
            {
                new TerrainRowSpec(
                    terrainId: 0,
                    name: "wood_floor",
                    kSpeed: 1,
                    kAccel: Fix.FromRational(1, 1),
                    kDecel: Fix.FromRational(1, 1)),
            };

            var cellTerrainId = new int[total];   // 全 0 = 全指向 terrain_id 0(见上方注释)
            return new TerrainSource(rows, cellTerrainId, SlopeLimitDegX100, StepOffsetMm, total);
        }
    }
}
