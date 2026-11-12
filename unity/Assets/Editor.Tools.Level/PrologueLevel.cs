// 序章三段关卡源 —— 城市废墟街区(A) / 七里路(B) / 城郊营地(C)(关卡工具的声明式数据)。
//
// 权威来源:
//   `design/levels/chapter-1-tent-clinic.md`(2026-11-12 Approved —— 空间清单 / 交互点表 / 七件数据改动)
//   `production/milestones/README.md` §七(P1 灰盒)· §7.5 三条裁定(2026-11-12:128×40 切片 · 不放开预摆 · 只做序章)
//   ADR-022 §二(唯一作者)· §三(导出契约)· §四(C1–C6)
//   ADR-015 §三(单一整数格)· §四(chunk 流式)
//
// ─────────────────────────────────────────────────────────────────────────────
// 本文件在 P1-a 的位置:**新增,不替换**
// ─────────────────────────────────────────────────────────────────────────────
// `P0ClinicLevel`(16×1×16)仍是 **canonical 关卡**(`LevelCatalog.CanonicalLevelId`),
// 其烘焙产物(`assets/data/world_*.json` 不带后缀)与 68 处字面坐标夹具**本批一律不动**。
// 本关卡以 **非 canonical id** 登记,产物带 `_p0_prologue` 后缀 —— 这是 P1-b 做「canonical 切换 +
// 夹具迁移」之前的**可编译、可跑 C1–C6、零运行期回归**形态。
//
// ⚠️ **不要把本文件登记成 canonical**:canonical 换绑 = 运行期装载面整体改(`WorldDataSets.FileNameFor`
//   决定后缀),须与夹具迁移**同批**,单做会得到「数据换了、夹具还在 16×16 上断言」的半截状态。
//
// ⚠️ **数值仍是占位**:`LatticeSizeMm` / `slopeLimit` / `stepOffset` 与 P0 医馆同源(用户数值轮);
//   地形三档的 `K_speed` / `K_accel` / `K_decel` 全取基准值 1 —— **地貌语义已声明,数值未裁**。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;   // WorldPos / Fix
using DaYiJingCheng.Sim.World;       // SlotType(唯一真源)

namespace DaYiJingCheng.EditorTools.Level
{
    /// <summary>序章三段关卡(128×1×40)的声明式源。</summary>
    public static class PrologueLevel
    {
        /// <summary>关卡 id(非 canonical ⇒ 产物文件带 `_p0_prologue` 后缀;见文件头)。</summary>
        public const string LevelId = "p0_prologue";

        // ─────────────────────────────────────────────────────────────────────
        // 尺寸与分区(单一出处 = 本节 + Define 的实参;不要在别处复制)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>世界宽(X;128 格 = 128 m)。</summary>
        private const int SizeX = 128;
        /// <summary>世界高(Y;单层)。</summary>
        private const int SizeY = 1;
        /// <summary>世界深(Z;40 格)。</summary>
        private const int SizeZ = 40;

        /// <summary>A 城市废墟街区:X 1–31 / Z 5–34。</summary>
        private const int AXMin = 1, AXMax = 31, AZMin = 5, AZMax = 34;
        /// <summary>B 七里路:X 32–95 / Z 14–25(走廊)。</summary>
        private const int BXMin = 32, BXMax = 95, BZMin = 14, BZMax = 25;
        /// <summary>C 城郊营地:X 96–126 / Z 5–34。</summary>
        private const int CXMin = 96, CXMax = 126, CZMin = 5, CZMax = 34;

        /// <summary>教堂主体(墙体范围);内部另判。</summary>
        private const int ChurchXMin = 4, ChurchXMax = 15, ChurchZMin = 14, ChurchZMax = 29;
        /// <summary>东墙塌口(唯一「可走出」的开口;`z` 区间,`x = ChurchXMax`)。</summary>
        private const int ChurchGapZMin = 20, ChurchGapZMax = 23;
        /// <summary>药铺废墟里塌成不可走的瓦砾块。</summary>
        private const int RubbleXMin = 26, RubbleXMax = 30, RubbleZMin = 5, RubbleZMax = 7;

        /// <summary>chunk 边长(承 `P0ClinicLevel` 的用户裁定值 8×1×8;见规格 §9.2 #3 待 P1 实测)。</summary>
        public const int ChunkSizeX = 8;
        /// <summary>见 <see cref="ChunkSizeX"/>。</summary>
        public const int ChunkSizeY = 1;
        /// <summary>见 <see cref="ChunkSizeX"/>。</summary>
        public const int ChunkSizeZ = 8;

        /// <summary>格边长(毫米;承 P0 医馆 1000 mm = 1 m/格;数值轮裁定)。</summary>
        public const int LatticeSizeMm = 1000;

        /// <summary>
        /// 构造序章三段的声明式关卡源。
        /// </summary>
        public static LevelSource Define()
        {
            return new LevelSource(
                LevelId, SizeX, SizeY, SizeZ, BuildSlots(),
                walkable: BuildWalkable(),
                terrain: BuildTerrain(),
                latticeSizeMm: LatticeSizeMm,
                ecozones: BuildEcozones(),
                poi: BuildPoi(),
                resources: BuildResources(),
                chunkSizeX: ChunkSizeX,
                chunkSizeY: ChunkSizeY,
                chunkSizeZ: ChunkSizeZ,
                containers: BuildContainers());
        }

        // ─────────────────────────────────────────────────────────────────────
        // 可走性
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>每格可走性(索引 = `(x*SizeY + y)*SizeZ + z`)。</summary>
        /// <remarks>
        /// ⚠️ **世界边界一圈不可走**(x ∈ {0,127} ∨ z ∈ {0,39})—— 承 2026-11-03 的实测教训:
        /// 边界格若可走,「用边界当 flood fill 种子」会把全图误判成内部。
        /// </remarks>
        private static IReadOnlyList<int> BuildWalkable()
        {
            var walkable = new int[SizeX * SizeY * SizeZ];
            for (int x = 0; x < SizeX; x++)
                for (int y = 0; y < SizeY; y++)
                    for (int z = 0; z < SizeZ; z++)
                        walkable[(x * SizeY + y) * SizeZ + z] = IsWalkable(x, z) ? 1 : 0;
            return walkable;
        }

        /// <summary>单格可走性(x/z 平面)。</summary>
        private static bool IsWalkable(int x, int z)
        {
            if (x == 0 || x == SizeX - 1 || z == 0 || z == SizeZ - 1) return false;   // 世界边界

            bool inA = x >= AXMin && x <= AXMax && z >= AZMin && z <= AZMax;
            bool inB = x >= BXMin && x <= BXMax && z >= BZMin && z <= BZMax;
            bool inC = x >= CXMin && x <= CXMax && z >= CZMin && z <= CZMax;
            if (!inA && !inB && !inC) return false;                                    // 三区之外 = 地界

            if (inA && IsChurchWall(x, z)) return false;
            if (inA && x >= RubbleXMin && x <= RubbleXMax && z >= RubbleZMin && z <= RubbleZMax) return false;
            return true;
        }

        /// <summary>教堂墙体(内部可走;<see cref="ChurchGapZMin"/>..<see cref="ChurchGapZMax"/> 是东墙塌口)。</summary>
        private static bool IsChurchWall(int x, int z)
        {
            if (x < ChurchXMin || x > ChurchXMax || z < ChurchZMin || z > ChurchZMax) return false;

            bool onEdge = x == ChurchXMin || x == ChurchXMax || z == ChurchZMin || z == ChurchZMax;
            if (!onEdge) return false;                       // 内部

            if (x == ChurchXMax && z >= ChurchGapZMin && z <= ChurchGapZMax) return false;   // 东墙塌口
            return true;
        }

        // ─────────────────────────────────────────────────────────────────────
        // 地形(三档地貌;数值全取基准 1,归数值轮)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>地形声明:0 = 石(A)/ 1 = 土(B)/ 2 = 腐殖土(C);边界格按石铺。</summary>
        /// <remarks>
        /// ⚠️ **`K_speed` 的「方向」未裁,本表只满足构造守卫** —— `TerrainSource` 要求
        /// 「`K_speed == 1` 恰好一行」(基准地貌唯一),而三行全填 1 会被当场拒。
        /// ⇒ 石 = 1(基准)· 土 / 腐殖土 = 2(**同一档**)。⚠️ **2 是「更快」还是「更慢」,
        /// GDD F-6-2 只定义 `K_TERRAIN_MAX = max(表)`,没钉方向** —— 数值与方向归**数值轮**,
        /// 本批只为让地形表可构造、可通过 C 系列检查,**不得**被读成「土路比石路快/慢」的裁定。
        /// </remarks>
        private static TerrainSource BuildTerrain()
        {
            var rows = new List<TerrainRowSpec>
            {
                new TerrainRowSpec(0, "stone", kSpeed: 1, kAccel: Fix.One, kDecel: Fix.One),
                new TerrainRowSpec(1, "dirt",  kSpeed: 2, kAccel: Fix.One, kDecel: Fix.One),
                new TerrainRowSpec(2, "humus", kSpeed: 2, kAccel: Fix.One, kDecel: Fix.One),
            };

            int total = SizeX * SizeY * SizeZ;
            var cellTerrainId = new int[total];
            for (int x = 0; x < SizeX; x++)
                for (int y = 0; y < SizeY; y++)
                    for (int z = 0; z < SizeZ; z++)
                    {
                        int id = 0;                                    // 缺省 = 石
                        if (x >= BXMin && x <= BXMax && z >= BZMin && z <= BZMax) id = 1;
                        else if (x >= CXMin && z >= CZMin && z <= CZMax) id = 2;
                        cellTerrainId[(x * SizeY + y) * SizeZ + z] = id;
                    }

            return new TerrainSource(rows, cellTerrainId,
                slopeLimitDegX100: P0ClinicLevel.SlopeLimitDegX100,
                stepOffsetMm: P0ClinicLevel.StepOffsetMm,
                totalCells: total);
        }

        // ─────────────────────────────────────────────────────────────────────
        // 生态区(两区,共享 x = 95 那条边;场景 ≠ 生态区)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>生态区:0 = `urban_ruins`(A+B)/ 1 = `field_camp`(C)。</summary>
        /// <remarks>
        /// ⚠️ **三场景 ≠ 三生态区**(用户 2026-11-12 裁定):生态区是 52 / 17 的**判定输入**,
        /// 场景是载入切分。故这里只有两区,且 B 与 A 同区 —— 七里路不是「另一种地貌」。
        /// ⚠️ 两区**共享一条边**(x = 95):F-6-3 的「开集 + `min(id)` 裁决序」只有在贴边时被检验。
        /// </remarks>
        private static EcozoneSource BuildEcozones()
        {
            static WorldPos P(int x, int z) => new WorldPos(x, 0, z);

            var zones = new List<EcozoneZoneSpec>
            {
                new EcozoneZoneSpec(0, "urban_ruins", new[]
                {
                    P(AXMin, AZMin), P(95, AZMin), P(95, AZMax), P(AXMin, AZMax),
                }, Fix.Zero),
                new EcozoneZoneSpec(1, "field_camp", new[]
                {
                    P(95, AZMin), P(SizeX - 1, AZMin), P(SizeX - 1, AZMax), P(95, AZMax),
                }, Fix.Zero),
            };
            return new EcozoneSource(zones, Fix.Zero);
        }

        // ─────────────────────────────────────────────────────────────────────
        // POI(四件;`type` 取值闭集归 6 的 GDD,本处沿用现有行的 1 = 废墟类)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>POI:教堂塌墙 / 药铺废墟 / 坍塌救助站 / 营地火堆。</summary>
        /// <remarks>⚠️ `guard_ref` 只声明档位(0 = 无守卫);具体敌人由 52 抽池时决定。</remarks>
        private static PoiSource BuildPoi()
        {
            static WorldPos P(int x, int z) => new WorldPos(x, 0, z);
            var pois = new List<PoiSpec>
            {
                new PoiSpec(0, P(ChurchXMax, 21), type: 1, guardRef: 0),   // 教堂东墙塌口
                new PoiSpec(1, P(24, 8),          type: 1, guardRef: 2),   // 药铺废墟(地痞)
                new PoiSpec(2, P(8, 8),           type: 1, guardRef: 0),   // 坍塌救助站(手册)
                new PoiSpec(3, P(116, 14),        type: 1, guardRef: 0),   // 营地火堆
            };
            return new PoiSource(pois);
        }

        // ─────────────────────────────────────────────────────────────────────
        // 资源(止血草 ×3;建材 / 草木灰待 21a 词条)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>资源点:营地止血草 ×3。</summary>
        /// <remarks>
        /// ⚠️ **本批只声明止血草**:`item_key` = 21a 物品表 ordinal,**止血草 = 4**(`_ordinal_baseline.json`)。
        /// 规格 §5 列的「建材堆 ×3(B1)· 草木灰 ×1(C3)」**暂缺 21a 词条**
        /// (门板 / 房梁 / 布 / 草木灰 均无 `item_key`),归 21a 实现轮补行 ——
        /// **不得在此臆造 ordinal**(臆造的 key 会让采前闸算错 `qty`,且是第二真源)。
        /// </remarks>
        private static ResourceSource BuildResources()
        {
            static WorldPos P(int x, int z) => new WorldPos(x, 0, z);
            var rows = new List<ResourceSpec>
            {
                new ResourceSpec(1, P(116, 20), itemKey: 4, ecozoneId: 1),
                new ResourceSpec(2, P(118, 24), itemKey: 4, ecozoneId: 1),
                new ResourceSpec(3, P(120, 22), itemKey: 4, ecozoneId: 1),
            };
            return new ResourceSource(rows);
        }

        // ─────────────────────────────────────────────────────────────────────
        // 建造槽(帐篷 = P0 唯一新建;Shell = P1a 自由建造面)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>槽位:帐篷槽 1(C2)+ Shell 4(P1a 自由建造的占位面)。</summary>
        /// <remarks>
        /// ⚠️ 帐篷槽**承现有语义**:`bakedInitial = false` / `moduleId = NoModule`
        /// —— 「序章那一次新建」的落点,不是开档既有件。
        /// ⚠️ Shell 四格同样 `bakedInitial = false`:P1a 才建;P0 它们只是「这里能建」的声明,
        /// 运行期不占格、不产 `structure_id`。
        /// </remarks>
        private static IReadOnlyList<SlotSpec> BuildSlots()
        {
            var slots = new List<SlotSpec>
            {
                new SlotSpec(108, 0, 16, SlotType.Tent, bakedInitial: false, moduleId: SlotSpec.NoModule),

                new SlotSpec(106, 0, 14, SlotType.Shell, bakedInitial: false, moduleId: SlotSpec.NoModule),
                new SlotSpec(110, 0, 14, SlotType.Shell, bakedInitial: false, moduleId: SlotSpec.NoModule),
                new SlotSpec(106, 0, 18, SlotType.Shell, bakedInitial: false, moduleId: SlotSpec.NoModule),
                new SlotSpec(110, 0, 18, SlotType.Shell, bakedInitial: false, moduleId: SlotSpec.NoModule),
            };
            return slots;
        }

        // ─────────────────────────────────────────────────────────────────────
        // 开档容器(药铺木箱 ×2;救助站书箱 ×1)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>开档自带容器(物品实例;`instance_id` 与掉落共用 `IIdAuthority` 空间)。</summary>
        private static ContainerSource BuildContainers()
        {
            static WorldPos P(int x, int z) => new WorldPos(x, 0, z);
            return new ContainerSource(new[]
            {
                new ContainerSpec(1L, P(24, 6)),
                new ContainerSpec(2L, P(28, 9)),
                new ContainerSpec(3L, P(7, 8)),
            });
        }
    }
}
