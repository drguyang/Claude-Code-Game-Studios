// 导航格 **接生产装配** 的夹具(story-005 收口后的接线轮 · 2026-10-29)。
//
// 权威来源:
//   ADR-023 ⑥(运行期 chunk 激活权 = 系统 6;**只读 `ActorCellEntered` 的格**,
//              不读表现态位置 ⇒ 激活是派生态不进流)
//   ADR-022 §五(未驻留 chunk = 全 `block`,保守判定)
//   `production/epics/level-tool/story-005-world-nav-chunk-slicing.md` AC-1 / AC-5
//
// ⚠️ 本件判的是**接线**,不是「寻路」—— 27 的 `EnemyPathing.FindPath` 今天仍零生产调用方,
//    本件不声称它在跑。判据面 = 「`World.IsEffectivelyWalkable` 的可走性来自 `INavLattice`」
//    「驻留集由 `ActorCellEntered` 的格驱动」「P0 的生产装配全驻留 ⇒ 与未接线逐格等价」。

using System;
using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Presentation;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.LevelTool
{
    /// <summary>导航格接线:驻留集、World 的来源切换、装配期全驻留等价。</summary>
    [TestFixture]
    public class nav_streaming_wiring_test
    {
        private static readonly string RepoRoot = ComputeRepoRoot();

        private static string ComputeRepoRoot(
            [System.Runtime.CompilerServices.CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(thisFile) ?? ".", "..", "..", "..", "..", ".."));

        // ── 小世界:2×1×2,chunk 2×1×2 ⇒ 1 块 4 格 ─────────────────────────────
        private static WorldGeometry MakeGeometry(bool walkable, int sizeX, int sizeY, int sizeZ)
        {
            var terrain = new TerrainCell[sizeX, sizeY, sizeZ];
            for (int x = 0; x < sizeX; x++)
                for (int y = 0; y < sizeY; y++)
                    for (int z = 0; z < sizeZ; z++)
                        terrain[x, y, z] = new TerrainCell(walkable, 0);

            int total = sizeX * sizeY * sizeZ;
            var poi = new int[total];
            var res = new int[total];
            for (int i = 0; i < total; i++) { poi[i] = -1; res[i] = -1; }
            return new WorldGeometry(terrain, poi, res);
        }

        private static NavLattice MakeLattice(int sizeX, int sizeY, int sizeZ,
            int csX, int csY, int csZ, params (int Chunk, bool Walkable)[] chunks)
        {
            var dict = new Dictionary<int, IReadOnlyList<int>>();
            int chunkCells = csX * csY * csZ;
            foreach ((int chunk, bool walkable) in chunks)
            {
                var cells = new int[chunkCells];
                for (int i = 0; i < chunkCells; i++) cells[i] = walkable ? 1 : 0;
                dict.Add(chunk, Array.AsReadOnly(cells));
            }
            return new NavLattice(sizeX, sizeY, sizeZ, csX, csY, csZ, dict);
        }

        // ══════════════ ① 驻留集:有数据但未驻留 ⇒ block ══════════════

        /// <summary>`SetResidency` 把「有数据」的块卸下 ⇒ 那几格回 `block`,但 `IsResident` 说得出区别。</summary>
        /// <remarks><b>判别力</b>:把 `IsWalkable` 的 `!_resident[...]` 那一半删掉 ⇒ 本测红。</remarks>
        [Test]
        public void test_setResidency_unmountedChunk_returnsBlock_dataStillPresent()
        {
            // 16×1×16、chunk 8×1×8 ⇒ 4 块;四块都有数据。
            var lattice = MakeLattice(16, 1, 16, 8, 1, 8,
                (0, true), (1, true), (2, true), (3, true));

            Assert.That(lattice.IsWalkable(new WorldPos(9, 0, 1)), Is.True,
                "前置:全驻留时 chunk 1 的那格应可走(否则下面的 block 断言可能是「恒 false」蒙对的)");

            // 只留 chunk 0。
            lattice.SetResidency(new[] { true, false, false, false });

            Assert.That(lattice.IsWalkable(new WorldPos(1, 0, 1)), Is.True, "chunk 0 仍驻留 ⇒ 可走");
            Assert.That(lattice.IsWalkable(new WorldPos(9, 0, 1)), Is.False,
                "chunk 1 已卸下 ⇒ **有数据也不可走**(ADR-022 §五 未驻留 = block)");
            Assert.That(lattice.IsResident(1), Is.False, "IsResident 须说得出「卸下了」");
            Assert.That(lattice.ResidentChunkCount, Is.EqualTo(1));
        }

        /// <summary>`SetResidency` 的位图长度与 chunk 数不符 ⇒ 抛(错位是静默的)。</summary>
        [Test]
        public void test_setResidency_wrongLength_throws()
        {
            var lattice = MakeLattice(4, 1, 4, 2, 1, 2, (0, true), (1, true), (2, true), (3, true));
            var ex = Assert.Throws<ArgumentException>(() => lattice.SetResidency(new[] { true, false }));
            Assert.That(ex.Message, Does.Contain("chunk"), "报文须点名 chunk 数");
        }

        // ══════════════ ② World 的可走性来自 NavLattice ══════════════

        /// <summary>接了导航格 ⇒ `IsEffectivelyWalkable` 读它,**不再读几何产物的可走性**。</summary>
        /// <remarks><b>判别力</b>:把 `IsEffectivelyWalkable` 改回只读 `_geometry` ⇒ 本测红
        /// (几何说可走、导航格说不可走,回 true 就是没换来源)。</remarks>
        [Test]
        public void test_world_isEffectivelyWalkable_readsNavLattice_notGeometry()
        {
            WorldGeometry geometry = MakeGeometry(walkable: true, 2, 1, 2);
            NavLattice allBlocked = MakeLattice(2, 1, 2, 2, 1, 2, (0, false));

            var world = new World(geometry, new BuildSlotCatalog(), allBlocked);

            Assert.That(world.IsEffectivelyWalkable(new WorldPos(0, 0, 0)), Is.False,
                "几何说可走、导航格说不可走 —— 接了导航格就必须以导航格为准");
            Assert.That(world.Nav, Is.SameAs(allBlocked), "World 须持有传进来的导航格");
        }

        /// <summary>未接导航格 ⇒ 回落几何产物(**既有行为**,测试与无产物场景用)。</summary>
        [Test]
        public void test_world_withoutNav_fallsBackToGeometryWalkable()
        {
            WorldGeometry geometry = MakeGeometry(walkable: true, 2, 1, 2);
            var world = new World(geometry, new BuildSlotCatalog());

            Assert.That(world.Nav, Is.Null);
            Assert.That(world.NavStreaming, Is.Null);
            Assert.That(world.IsEffectivelyWalkable(new WorldPos(0, 0, 0)), Is.True,
                "未接导航格时须回落到几何产物的可走性(否则所有既有夹具都会红)");
        }

        // ══════════════ ③ 驻留由 `ActorCellEntered` 的格驱动 ══════════════

        /// <summary>`OnActorCellEntered` 用格重推激活集;半径 0 ⇒ 只驻留玩家所在块。</summary>
        /// <remarks><b>判别力</b>:把 `World.OnActorCellEntered` 改成空操作 ⇒ 本测红
        /// (驻留恒为构造默认的全驻留)。</remarks>
        [Test]
        public void test_world_onActorCellEntered_narrowsResidencyByRadiusZero()
        {
            NavLattice lattice = MakeLattice(16, 1, 16, 8, 1, 8,
                (0, true), (1, true), (2, true), (3, true));
            var world = new World(MakeGeometry(true, 16, 1, 16), new BuildSlotCatalog(),
                                  lattice, navStreamingRadius: 0);

            Assert.That(lattice.ResidentChunkCount, Is.EqualTo(4), "前置:构造后默认全驻留");

            world.OnActorCellEntered(new WorldPos(1, 0, 1));   // chunk 0

            Assert.That(world.NavStreaming, Is.Not.Null);
            Assert.That(lattice.ResidentChunkCount, Is.EqualTo(1),
                "半径 0 ⇒ 只驻留玩家所在的 chunk");
            Assert.That(world.IsEffectivelyWalkable(new WorldPos(1, 0, 1)), Is.True);
            Assert.That(world.IsEffectivelyWalkable(new WorldPos(9, 0, 9)), Is.False,
                "chunk 3 未驻留 ⇒ block");
        }

        /// <summary>同一格喂两次 ⇒ 驻留集相同(纯函数;ADR-023 ⑥ 的「派生态可重推」)。</summary>
        [Test]
        public void test_navStreaming_sameCellTwice_sameResidency()
        {
            NavLattice lattice = MakeLattice(16, 1, 16, 8, 1, 8,
                (0, true), (1, true), (2, true), (3, true));
            var streaming = new NavChunkStreaming(lattice, streamingRadius: 1);

            var cell = new WorldPos(12, 0, 12);   // chunk 3
            streaming.OnActorCellEntered(cell);
            bool[] first = new bool[4];
            for (int i = 0; i < 4; i++) first[i] = lattice.IsResident(i);

            streaming.OnActorCellEntered(cell);
            for (int i = 0; i < 4; i++)
                Assert.That(lattice.IsResident(i), Is.EqualTo(first[i]),
                    $"第 {i} 块的驻留位在两次同格喂入之间变了 —— 驱动不是纯函数");
        }

        // ══════════════ ④ 生产装配:P0 全驻留 ⇒ 与几何逐格等价 ══════════════

        /// <summary>`MinimalWorldFactory.Create` 产出带导航格的 `World`,且 P0 逐格可走性与几何产物一致。</summary>
        /// <remarks><b>为什么这条是接线轮的核心判据</b>:P0 的 chunk = 2×1×2,半径 1 ⇒ 恒全驻留
        /// ⇒ **必须**与「未接导航格」逐格等价;不等 = 装载 / 索引 / 尺寸对账有一处错位。
        /// <b>判别力</b>:把 `NavStreamingRadius` 改成 `-1`(或装载少一片)⇒ 某些格变 block ⇒ 红。</remarks>
        [Test]
        public void test_factory_worldHasNavLattice_andMatchesGeometryCellByCell()
        {
            World world = MinimalWorldFactory.Create(null);   // 磁盘兜底(EditMode 下 DataCooked 在盘上)

            Assert.That(world.Nav, Is.Not.Null, "生产装配须接上导航格");
            Assert.That(world.NavStreaming, Is.Not.Null, "接了 NavLattice 就须有驻留驱动");

            WorldGeometryDataSet geo = DataCookedDisk.LoadWorldGeometry();

            // ⚠️ **必须先喂一次格再判**(2026-10-29 证伪轮 W4 抓到的缺口):
            //    `NavLattice` 构造后默认全驻留 ⇒ 不喂格的话 `NavStreamingRadius` 这个旋钮
            //    **从未被求值**,把它改成 -1(什么都不驻留)本测仍绿。喂一格之后半径才真正生效。
            world.OnActorCellEntered(new WorldPos(1, 0, 1));
            Assert.That(((NavLattice)world.Nav).ResidentChunkCount,
                Is.EqualTo(geo.Topology.ChunkCount),
                $"P0 = 2×1×2 共 {geo.Topology.ChunkCount} 块,半径 {MinimalWorldFactory.NavStreamingRadius} " +
                "须覆盖全部 ⇒ 喂一格后仍全驻留(否则「与未接线逐格等价」不成立)");

            int mismatches = 0;
            for (int x = 0; x < geo.SizeX; x++)
                for (int y = 0; y < geo.SizeY; y++)
                    for (int z = 0; z < geo.SizeZ; z++)
                    {
                        bool fromGeometry = geo.Walkable[(x * geo.SizeY + y) * geo.SizeZ + z] != 0;
                        if (world.Nav.IsWalkable(new WorldPos(x, y, z)) != fromGeometry) mismatches++;
                    }

            Assert.That(mismatches, Is.EqualTo(0),
                "全驻留下导航格与几何产物须逐格相同 —— 「两份产物同源」在生产装载面上的直接判据");
        }

        /// <summary>产物与几何对不上 ⇒ 装载**抛**(不静默用错尺寸)。</summary>
        /// <remarks>本测直接打 codec 的「装错块」门(装配侧的对账门形状相同):
        /// 把 chunk 0 的字节交给 chunk 1 的地址 ⇒ 抛且报文点名 chunk 号。</remarks>
        [Test]
        public void test_codec_rejectsChunkIndexMismatch()
        {
            byte[] cooked = File.ReadAllBytes(
                Path.Combine(RepoRoot, "unity", "Assets", "DataCooked", "world_nav_0.cooked.bytes"));
            var ex = Assert.Throws<InvalidDataException>(
                () => WorldNavChunkCookedCodec.Read(cooked, expectedChunkIndex: 1));
            Assert.That(ex.Message, Does.Contain("chunk"));
        }
    }
}
