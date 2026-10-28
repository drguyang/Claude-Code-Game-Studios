// **脚手架关卡** —— 不为玩法服务,只为证明关卡工具**真的泛化**。
//
// 权威来源:
//   `production/epics/level-tool/story-001-level-source-generalization.md` AC-2
//     (「新增第二个关卡定义(尺寸与 `p0_clinic` 不同)」;反例明确写着
//      「把 `P0ClinicLevel` 复制一份改个名字**不算** —— 判据要求两关尺寸不同」)
//   ADR-022 §二(逻辑层数据只能由关卡工具导出 —— 本文件是该口径下的**合法**声明式源)
//
// ⚠️ **这不是设计产物,不要把它当第二间医馆。**
//   它的全部用途 = 让「加第二个关卡」这件事有一条**可跑的路径**,并让「尺寸来自数据」
//   有一个**与 P0 不同**的取值。取 `8×2×8` 而不是 `8×8×8`:非立方 ⇒ 任一轴被写死成另一个轴的
//   值,`world_size` 立刻对不上(AC-1 的边界:「三个轴各自独立,不是同一个值」)。
//   真实第二关卡(设计、布局、模块配置)归**关卡设计轮**;届时本定义**整个替换**,
//   不是「在它上面加槽位」。
//
// ⚠️ **`Y = 2` 是刻意的**:`p0_clinic` 是 `16×1×16`(单层)。第二关若同样是「单层 + 只改 X/Z」,
//    `Height` 会永远等于 1 ⇒ 「三维尺寸各自独立」这条**在数据上不可观测**。
//   本关卡的槽位因此分布在 `y = 0` 与 `y = 1` 两层。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;   // Fix(地形表的 K_accel / K_decel;ADR-014 §二)
using DaYiJingCheng.Sim.World;       // SlotType(唯一真源)

namespace DaYiJingCheng.EditorTools.Level
{
    /// <summary>第二关卡的**脚手架**声明式源(见文件头:不是设计产物)。</summary>
    public static class ScaffoldSecondLevel
    {
        /// <summary>关卡 id(导出 JSON 的 `level_id`;**不含**基准关卡的名字)。</summary>
        public const string LevelId = "scaffold_second";

        // ⚠️ 尺寸**只在这里出现一次**,且**只**作为 LevelSource 的构造实参 ——
        //    不留 `const int Width` 之类的「另一条路」(AC-1 的病灶正是那条路)。
        //    `8 / 2 / 8` 与 `p0_clinic` 的 `16 / 1 / 16` 在 Y 轴上直接分家(X/Z 亦不同值),
        //    且本关非立方 ⇒ 「尺寸来自数据」在**两个轴**上都有观察面。

        /// <summary>y = 0 层的 Decor 空地(可建)。</summary>
        public const int DecorFreeX = 2;
        /// <summary>见 <see cref="DecorFreeX"/>。</summary>
        public const int DecorFreeZ = 2;

        /// <summary>y = 1 层的 Decor 初始占用格。</summary>
        public const int DecorOccupiedX = 5;
        /// <summary>见 <see cref="DecorOccupiedX"/>。</summary>
        public const int DecorOccupiedZ = 5;

        /// <summary>y = 0 层的 Shell 初始占用格(外壳)。</summary>
        public const int ShellX = 6;
        /// <summary>见 <see cref="ShellX"/>。</summary>
        public const int ShellZ = 1;

        /// <summary>初始占用 Decor 用的模块 id(`build_modules.json` 的 7 = `chamber_pot`)。</summary>
        public const int DecorModuleId = 7;

        /// <summary>外壳用的模块 id(`build_modules.json` 的 8 = `refundable: false`)。</summary>
        public const int ShellModuleId = 8;

        /// <summary>
        /// 产出脚手架的声明式关卡源。
        /// <para>槽位集(4 条,横跨 y = 0 / y = 1 两层):
        /// ① `(2,0,2)` Decor 空地(`baked_initial = false`);
        /// ② `(5,1,5)` Decor 初始占用(`module_id = 7`);
        /// ③ `(6,0,1)` Shell 初始占用(`module_id = 8`);
        /// ④ `(7,1,7)` Decor 空地。</para>
        /// <para>⚠️ 「空地与初始占用都有、两层都有」不是凑数:第二个关卡的导出若只有
        /// 一种取值,`baked_initial` 的两条域门(④/⑤)在这一关上**没有观察面**。</para>
        /// </summary>
        public static LevelSource Define()
        {
            // ⚠️ 第四格原为 `(7, 1, 7)`。story-002 引入「外环不可走」之后,`x = 7` 落在
            //    8×2×8 的边界环上 ⇒ 那格不可通行,而「槽位落在走不到的格上」是数据自相矛盾
            //    (入口永远无法抵达)。改 `(6, 1, 6)`:仍在 `y = 1`(AC-2 的「第二层有槽位」
            //    判据不受影响),且落在内部。
            var slots = new List<SlotSpec>
            {
                new SlotSpec(DecorFreeX, 0, DecorFreeZ, SlotType.Decor, bakedInitial: false),
                new SlotSpec(DecorOccupiedX, 1, DecorOccupiedZ, SlotType.Decor,
                             bakedInitial: true, moduleId: DecorModuleId),
                new SlotSpec(ShellX, 0, ShellZ, SlotType.Shell,
                             bakedInitial: true, moduleId: ShellModuleId),
                new SlotSpec(6, 1, 6, SlotType.Decor, bakedInitial: false),
            };

            const int sizeX = 8;
            const int sizeY = 2;
            const int sizeZ = 8;

            return new LevelSource(
                LevelId, sizeX, sizeY, sizeZ, slots,
                walkable: BuildWalkable(sizeX, sizeY, sizeZ),
                terrain: BuildTerrain(sizeX, sizeY, sizeZ),
                latticeSizeMm: P0ClinicLevel.LatticeSizeMm,
                ecozones: BuildEcozones(),
                poi: BuildPoi(),
                resources: BuildResources());
        }

        /// <summary>POI 定义(**单个占位**,与 `P0ClinicLevel` 的三个在数量上不同)。
        /// <para>⚠️ 为什么只有一个:本关卡是**脚手架**,它要证的是「POI 定义来自数据,
        /// 且第二关卡可以有自己的声明」—— 一个就够。数量 / 类型归关卡设计轮。</para></summary>
        private static PoiSource BuildPoi()
            => new PoiSource(new List<PoiSpec>
            {
                new PoiSpec(0, new WorldPos(4, 0, 4), type: 1, guardRef: 1),
            });

        /// <summary>资源点(**两条**;一条在生态区内、一条不带生态区倾向)。
        /// <para>⚠️ 第二条刻意用 `ecozone_id = -1` —— 它让「无倾向」这条取值在真产物上
        /// **有观察面**(全填 0 的实现会在这里露馅)。</para></summary>
        private static ResourceSource BuildResources()
            => new ResourceSource(new List<ResourceSpec>
            {
                new ResourceSpec(3, new WorldPos(2, 0, 4), ecozoneId: 0),
                new ResourceSpec(3, new WorldPos(2, 1, 5), ecozoneId: ResourceSpec.NoEcozone),
            });

        /// <summary>生态区声明(**单个占位区**;与 `P0ClinicLevel` 的两个区在**形状上不同**)。
        /// <para>⚠️ 为什么只有一个区:本关卡是**脚手架**,它要证的是「生态区来自数据,
        /// 且第二关卡可以有自己的声明」—— 一个区就够。数量 / 形状归关卡设计轮。</para></summary>
        private static EcozoneSource BuildEcozones()
        {
            static WorldPos P(int x, int z) => new WorldPos(x, 0, z);
            return new EcozoneSource(new List<EcozoneZoneSpec>
            {
                new EcozoneZoneSpec(0, "scaffold_zone", new[]
                {
                    P(1, 1), P(6, 1), P(6, 6), P(1, 6),
                }),
            });
        }

        // ⚠️ 以下两件与 `P0ClinicLevel` 的对应方法**形状相同、数值也相同**(外环 1 格不可走、
        //    单行地形表 `terrain_id = 0`)。**刻意不抽公用工具**:本关卡是**脚手架**,
        //    它的存在意义就是「与第一关卡不同」;把两者的地形声明合并成一个共用方法,
    //    会让「第二个关卡的地形可以独立声明」这件事在代码上不成立 ——
        //    而那正是本关卡要证的事(AC-2)。真实第二关卡来时本文件整个替换。

        /// <summary>每格可走性(外环 1 格不可走;索引 = `(x*H + y)*D + z`)。</summary>
        private static IReadOnlyList<int> BuildWalkable(int sizeX, int sizeY, int sizeZ)
        {
            var walkable = new int[sizeX * sizeY * sizeZ];
            for (int x = 0; x < sizeX; x++)
                for (int y = 0; y < sizeY; y++)
                    for (int z = 0; z < sizeZ; z++)
                    {
                        bool onRing = x == 0 || x == sizeX - 1 || z == 0 || z == sizeZ - 1;
                        walkable[(x * sizeY + y) * sizeZ + z] = onRing ? 0 : 1;
                    }
            return walkable;
        }

        /// <summary>本关卡的地形声明(单行表 `terrain_id = 0`,全格指向它)。</summary>
        /// <remarks>⚠️ 单行 / `K_speed = 1` 是**占位**,归用户数值轮(与 `P0ClinicLevel` 同口径)。</remarks>
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

            var cellTerrainId = new int[total];
            return new TerrainSource(
                rows, cellTerrainId, P0ClinicLevel.SlopeLimitDegX100, P0ClinicLevel.StepOffsetMm, total);
        }
    }
}
