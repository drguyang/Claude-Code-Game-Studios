// 关卡工具 story-005 —— **导航格切片 + C6 + C3**(AC-1 … AC-6)的夹具。
//
// 权威来源:
//   `production/epics/level-tool/story-005-world-nav-chunk-slicing.md`(AC-1 … AC-6)
//   ADR-022 §四(C3 = 告警 / C6 = 硬失败)· §五(按 chunk 切片)· ADR-015 §四(chunk 不是坐标原点)
//   `production/qa/evidence/spike-navmesh-6.3-2026-10-29.md`(C3 三态设计依据)
//
// ⚠️ **零写盘 / 零随机 / 零时间依赖**。C3 的采样器全部是**注入的合成采样器** ——
//    真实 NavMesh 在 P0 不存在(spike 实测),拿它当夹具输入等于「用无数据验判定」。
//
// ⚠️ **AC-3 的两条负向夹具刻意分开**(故事卡逐字要求):漏格与重复格是**不同的失败模式**
//    (前者「那格不可走」,后者「取哪一份」),合并会让停用任一分支时另一条仍红。
//
// ⚠️ 本件**不证**:AI 寻路是否真的走通(只出数据,寻路归 27)·
//    NavMesh 烘焙质量(C3 是告警级)· chunk 激活的运行时行为(归 6 / ADR-023 ⑥)·
//    `maxDistance` 的口径(spike 登记为 NOT-RUN,见 `NavMeshReachabilitySampler` 文件头)。

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using DaYiJingCheng.EditorTools.Bake;
using DaYiJingCheng.EditorTools.Level;
using DaYiJingCheng.Gameplay.Presentation;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.LevelTool
{
    /// <summary>story-005:导航格切片、C6 两条硬失败、C3 三态告警、chunk 划分同源。</summary>
    [TestFixture]
    public class nav_chunk_slicing_test
    {
        private static readonly string RepoRoot = ComputeRepoRoot();

        private static string ComputeRepoRoot(
            [System.Runtime.CompilerServices.CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(thisFile) ?? ".", "..", "..", "..", "..", ".."));

        private static string CookedDir => Path.Combine(RepoRoot, "unity", "Assets", "DataCooked");

        private static LevelSource P0() => LevelCatalog.Define(P0ClinicLevel.LevelId);

        // ══════════════════════════ AC-1:`INavLattice` ══════════════════════════

        /// <summary>AC-1①:`INavLattice` 承载类型存在,且 <see cref="NavLattice"/> 实现它。</summary>
        /// <remarks><b>判别力</b>:删掉接口或让实现类脱钩 ⇒ 编译期/断言期即红。</remarks>
        [Test]
        public void test_ac1_navLatticeType_exists_andImplementsContract()
        {
            Assert.That(typeof(INavLattice).IsInterface, Is.True,
                "story-005 AC-1 要求承载类型存在;`world-ecozones/EPIC.md` 逐字写「6 拥有 INavLattice」");
            Assert.That(typeof(INavLattice).IsAssignableFrom(typeof(NavLattice)), Is.True,
                "NavLattice 未实现 INavLattice ⇒ 契约与实现在类型上脱钩");

            MethodInfo m = typeof(INavLattice).GetMethod("IsWalkable");
            Assert.That(m, Is.Not.Null, "契约缺 IsWalkable");
            Assert.That(m.ReturnType, Is.EqualTo(typeof(bool)));
            Assert.That(m.GetParameters().Length, Is.EqualTo(1));
            Assert.That(m.GetParameters()[0].ParameterType, Is.EqualTo(typeof(WorldPos)),
                "格形参须是 WorldPos(ADR-015 §三 单一整数格),不得另起一套坐标");
        }

        /// <summary>AC-1②:**未驻留 chunk 查询回 `block`**(不是 `walkable`)。</summary>
        /// <remarks><b>判别力</b>:把 `_chunkCells[idx] == null` 分支改成「返回 true」或
        /// 「当全 0 数组」以外的任何宽容读法 ⇒ 本测红。</remarks>
        [Test]
        public void test_ac1_unresidentChunk_returnsBlock_residentChunkHonoursData()
        {
            const int size = 16, chunkSize = 8;          // 2×1×2 = 4 块
            var fullWalkable = new int[chunkSize * 1 * chunkSize];
            for (int i = 0; i < fullWalkable.Length; i++) fullWalkable[i] = 1;

            // 只驻留 chunk 0;chunk 1 / 2 / 3 缺席。
            var lattice = new NavLattice(size, 1, size, chunkSize, 1, chunkSize,
                new Dictionary<int, IReadOnlyList<int>> { { 0, fullWalkable } });

            Assert.That(lattice.IsWalkable(new WorldPos(1, 0, 1)), Is.True,
                "chunk 0 已驻留且那格标 1 ⇒ 须可走(否则下面的 block 断言可能是「恒 false」蒙对的)");
            Assert.That(lattice.IsWalkable(new WorldPos(9, 0, 1)), Is.False,
                "格 (9,0,1) 在 chunk 1 —— **未驻留 ⇒ block**。回 true = AC-1 的反例(未驻留默认可走)");
            Assert.That(lattice.IsWalkable(new WorldPos(1, 0, 9)), Is.False, "chunk 2 未驻留 ⇒ block");
            Assert.That(lattice.IsWalkable(new WorldPos(9, 0, 9)), Is.False, "chunk 3 未驻留 ⇒ block");
        }

        /// <summary>AC-1(边界):越界格 ⇒ block(不抛、不越界读)。</summary>
        [Test]
        public void test_ac1_outOfRangeCell_returnsBlock()
        {
            // 2×1×4 的世界、chunk 2×1×2 ⇒ 1×1×2 = 2 块,每块 2×1×2 = **4** 格。
            var allOne = new int[4];
            var lattice = new NavLattice(2, 1, 4, 2, 1, 2,
                new Dictionary<int, IReadOnlyList<int>> { { 0, allOne }, { 1, allOne } });

            Assert.That(lattice.IsWalkable(new WorldPos(-1, 0, 0)), Is.False, "负坐标 ⇒ block");
            Assert.That(lattice.IsWalkable(new WorldPos(2, 0, 0)), Is.False, "x 越界 ⇒ block");
            Assert.That(lattice.IsWalkable(new WorldPos(0, 1, 0)), Is.False, "y 越界 ⇒ block");
            Assert.That(lattice.IsWalkable(new WorldPos(0, 0, 4)), Is.False, "z 越界 ⇒ block");
        }

        /// <summary>AC-1(端到端):由**烘焙产物**装配的 NavLattice 与关卡的声明逐格相同。</summary>
        /// <remarks>⚠️ 这同时证明「未驻留 ⇒ block」不是靠「永远不装载」蒙对的:
        /// 四块全装载后,每一格都读得到真值。</remarks>
        [Test]
        public void test_ac1_latticeFromBakedChunks_reproducesDeclaredWalkable()
        {
            LevelSource src = P0();
            var resident = new Dictionary<int, IReadOnlyList<int>>();
            int chunkCount = src.GetChunkTopology().ChunkCount;
            for (int i = 0; i < chunkCount; i++)
            {
                WorldNavChunkDataSet ds = DataCookedDisk.LoadWorldNavChunk(i);
                resident.Add(ds.ChunkIndex, Array.AsReadOnly(ds.Walkable));
            }

            var lattice = new NavLattice(
                src.Width, src.Height, src.Depth,
                src.ChunkSizeX, src.ChunkSizeY, src.ChunkSizeZ, resident);

            Assert.That(lattice.ResidentChunkCount, Is.EqualTo(chunkCount), "四块须全驻留");

            int mismatches = 0;
            for (int x = 0; x < src.Width; x++)
                for (int y = 0; y < src.Height; y++)
                    for (int z = 0; z < src.Depth; z++)
                    {
                        bool declared = src.Walkable[(x * src.Height + y) * src.Depth + z] == 1;
                        if (lattice.IsWalkable(new WorldPos(x, y, z)) != declared) mismatches++;
                    }

            Assert.That(mismatches, Is.EqualTo(0),
                "由烘焙切片装配的导航格与关卡声明不一致 ⇒ 切片 / 块内索引 / 反序列化任一处错位");
        }

        // ═════════════════════ AC-2:按 chunk 切片 + 同源 ═════════════════════

        /// <summary>AC-2①:每个 chunk 一个文件,基准关卡的名字是 `world_nav_{chunk}.json`。</summary>
        [Test]
        public void test_ac2_oneFilePerChunk_canonicalNaming()
        {
            IReadOnlyList<DataSetProduct> products = LevelExporter.ExportNavChunks(P0());

            Assert.That(products.Count, Is.EqualTo(4),
                "P0 = 16×1×16、chunk 8×1×8 ⇒ 2×1×2 = 4 块(用户裁定 2026-10-29)");

            for (int i = 0; i < products.Count; i++)
                Assert.That(products[i].FileName, Is.EqualTo("world_nav_" + i + ".json"),
                    "基准关卡的切片名须是 ADR-022 §五 的契约字面 `world_nav_{chunk}.json`");
        }

        /// <summary>AC-2②:导航格切片的 chunk 键与**几何产物**的 chunk 划分同源。</summary>
        /// <remarks><b>这是本 story 的同源判据</b>:chunk 数取自**烘焙产物**的
        /// `world_size + chunk_size`,chunk 坐标由 `IndexToChunk` 给出;若导航格侧另写一遍公式
        /// (例如 `x >> 4` 或另一套边界),两者会在此分家。
        /// <b>判别力</b>:把 `NavChunkSlicer` 的划分改成 `ChunkTopology.ForWorld` 之外的另一套 ⇒ 红。</remarks>
        [Test]
        public void test_ac2_chunkKeys_agreeWithGeometryProduct()
        {
            WorldGeometryDataSet geom = DataCookedDisk.LoadWorldGeometry();
            LevelSource src = P0();

            Assert.That((geom.ChunkSizeX, geom.ChunkSizeY, geom.ChunkSizeZ),
                Is.EqualTo((src.ChunkSizeX, src.ChunkSizeY, src.ChunkSizeZ)),
                "几何产物声明的 chunk 尺寸与关卡源不一致 —— 「同源」在此已经断了");

            ChunkTopology topo = geom.Topology;
            IReadOnlyList<DataSetProduct> products = LevelExporter.ExportNavChunks(src);
            Assert.That(products.Count, Is.EqualTo(topo.ChunkCount),
                "切片文件数须等于**几何产物**算出的 chunk 数(不是导航格侧自算的一个数)");

            for (int i = 0; i < products.Count; i++)
            {
                string json = products[i].Json;
                Match idx = Regex.Match(json, "\"chunk_index\": (-?[0-9]+)");
                Match cell = Regex.Match(json, "\"chunk\": \\[(-?[0-9]+), (-?[0-9]+), (-?[0-9]+)\\]");
                Assert.That(idx.Success && cell.Success, Is.True, "切片缺 chunk_index / chunk 字段:\n" + json);

                // 切片**自己声明**的划分必须等于几何产物的声明 —— 「同源」在数据面上的直接判据。
                Match cs = Regex.Match(json, "\"chunk_size\": \\[(-?[0-9]+), (-?[0-9]+), (-?[0-9]+)\\]");
                Assert.That(cs.Success, Is.True, "切片缺 chunk_size 字段:\n" + json);
                Assert.That(
                    (int.Parse(cs.Groups[1].Value), int.Parse(cs.Groups[2].Value), int.Parse(cs.Groups[3].Value)),
                    Is.EqualTo((geom.ChunkSizeX, geom.ChunkSizeY, geom.ChunkSizeZ)),
                    "切片声明的 chunk 尺寸与**几何产物**不一致 —— 两侧各写了一遍划分(AC-2② 的反例)");

                int chunkIndex = int.Parse(idx.Groups[1].Value);
                var declared = (int.Parse(cell.Groups[1].Value),
                                int.Parse(cell.Groups[2].Value),
                                int.Parse(cell.Groups[3].Value));
                WorldPos expected = topo.IndexToChunk(chunkIndex);

                Assert.That(declared, Is.EqualTo((expected.X, expected.Y, expected.Z)),
                    $"第 {i} 片的 chunk 坐标与**几何产物**的划分不一致 —— " +
                    "导航格侧用了另一套公式(AC-2② 判的正是这件事)");
            }
        }

        /// <summary>AC-2(回归):同一关卡两次导出逐字节相同(确定性)。</summary>
        [Test]
        public void test_ac2_exportNavChunks_isDeterministic()
        {
            LevelSource src = P0();
            IReadOnlyList<DataSetProduct> a = LevelExporter.ExportNavChunks(src);
            IReadOnlyList<DataSetProduct> b = LevelExporter.ExportNavChunks(src);

            Assert.That(a.Count, Is.EqualTo(b.Count));
            for (int i = 0; i < a.Count; i++)
            {
                Assert.That(a[i].FileName, Is.EqualTo(b[i].FileName));
                Assert.That(a[i].Json, Is.EqualTo(b[i].Json), "第 " + i + " 片两次导出不逐字节相同");
            }
        }

        // ══════════════ AC-3:C6 切片完整性 —— **两条独立** ══════════════

        /// <summary>AC-3①:**漏格** —— 少给一块切片 ⇒ 恰报「漏格」,且不报「重复格」。</summary>
        /// <remarks><b>判别力</b>:停用「漏格」分支 ⇒ 本测红;两个分支合并成一条 ⇒
        /// 本测与 <see cref="test_ac3_duplicateCell_throwsNamingDuplicate"/> 会同时红/同时绿,失去区分。</remarks>
        [Test]
        public void test_ac3_missingChunk_reportsLeak_notDuplicate()
        {
            LevelSource src = P0();
            List<NavChunkSlice> slices = new List<NavChunkSlice>(
                NavChunkSlicer.Slice(src.Width, src.Height, src.Depth,
                    src.ChunkSizeX, src.ChunkSizeY, src.ChunkSizeZ, src.Walkable));
            slices.RemoveAt(slices.Count - 1);      // 整块消失 = 典型漏格

            bool ok = NavChunkSliceCheck.TryCheck(
                src.Width, src.Height, src.Depth,
                src.ChunkSizeX, src.ChunkSizeY, src.ChunkSizeZ, slices, out List<string> violations);

            Assert.That(ok, Is.False, "漏了一整块却判绿 —— C6 是硬失败(ADR-022 §四)");
            Assert.That(Report(violations), Does.Contain("漏格"), "报文须点名「漏格」这一失败模式");
            Assert.That(Report(violations), Does.Not.Contain("重复格"),
                "漏一块**不会**产生重复格;报文里出现「重复格」说明两个分支被合并了");
        }

        /// <summary>AC-3②:**重复格** —— 同一块给两遍 ⇒ 恰报「重复格」,且不报「漏格」。</summary>
        /// <remarks><b>判别力</b>:停用「重复格」分支 ⇒ 本测红。</remarks>
        [Test]
        public void test_ac3_duplicateChunk_reportsDuplicate_notLeak()
        {
            LevelSource src = P0();
            List<NavChunkSlice> slices = new List<NavChunkSlice>(
                NavChunkSlicer.Slice(src.Width, src.Height, src.Depth,
                    src.ChunkSizeX, src.ChunkSizeY, src.ChunkSizeZ, src.Walkable));

            // 把第 1 块**再给一遍**(同坐标、同内容、同索引)⇒ 其覆盖的每格被数两次。
            NavChunkSlice dup = slices[1];
            slices.Add(new NavChunkSlice(dup.ChunkIndex, dup.ChunkX, dup.ChunkY, dup.ChunkZ, dup.Walkable));

            bool ok = NavChunkSliceCheck.TryCheck(
                src.Width, src.Height, src.Depth,
                src.ChunkSizeX, src.ChunkSizeY, src.ChunkSizeZ, slices, out List<string> violations);

            Assert.That(ok, Is.False, "同一块出现两遍却判绿 —— C6 是硬失败");
            Assert.That(Report(violations), Does.Contain("重复格"), "报文须点名「重复格」这一失败模式");
            Assert.That(Report(violations), Does.Not.Contain("漏格"),
                "重复**不**导致漏格(每格仍被覆盖);出现「漏格」说明两个分支被合并了");
        }

        /// <summary>AC-3(正例):`ExportNavChunks` 自己产出的切片集合须过 C6。</summary>
        [Test]
        public void test_ac3_exportedSlices_passTheirOwnCheck()
        {
            LevelSource src = P0();
            IReadOnlyList<NavChunkSlice> slices = NavChunkSlicer.Slice(
                src.Width, src.Height, src.Depth,
                src.ChunkSizeX, src.ChunkSizeY, src.ChunkSizeZ, src.Walkable);

            bool ok = NavChunkSliceCheck.TryCheck(
                src.Width, src.Height, src.Depth,
                src.ChunkSizeX, src.ChunkSizeY, src.ChunkSizeZ, slices, out List<string> violations);

            Assert.That(ok, Is.True, "切片器自己的产物过不了 C6 —— 扫描键没起作用或切片器写错了:\n"
                + Report(violations));
        }

        // ═════════════════════ AC-4:C3 —— 三态告警 ═════════════════════

        /// <summary>AC-4①:逻辑可走 / NavMesh 不可达 ⇒ **报出**告警,且**不抛**。</summary>
        /// <remarks><b>判别力</b>:停用「可走→不可达」这一方向 ⇒ 本测红(告警为空)。
        /// ⚠️ 只验「不抛」是不够的 —— 一个永远不报的实现也满足它,那正是告警类检查的经典假绿。</remarks>
        [Test]
        public void test_ac4_logicalWalkable_navUnreachable_warnsWithoutThrowing()
        {
            LevelSource src = P0();
            Func<WorldPos, NavReachability> sampler = _ => NavReachability.Unreachable;

            CheckReport report = null;
            Assert.DoesNotThrow(() => report = LevelConsistencyCheck.Check(src, sampler),
                "C3 是告警级(ADR-022 §四)—— 矛盾不得使检查抛");

            Assert.That(report.IsGreen, Is.True, "告警不得进 Violations(否则就退化成硬失败了)");
            Assert.That(report.Warnings.Count, Is.GreaterThan(0), "矛盾存在却零告警 —— 检查没有在报");
            Assert.That(Report(report.Warnings), Does.Contain("逻辑可走"),
                "报文须说清是哪一侧的漂移");
        }

        /// <summary>AC-4(反方向):逻辑不可走 / NavMesh 可达 ⇒ **同样**报出告警。</summary>
        /// <remarks><b>判别力</b>:只实现一个方向 ⇒ 本测红。两个方向都漏掉一半会让漂移静默。</remarks>
        [Test]
        public void test_ac4_logicalBlocked_navReachable_warns()
        {
            LevelSource src = P0();
            Func<WorldPos, NavReachability> sampler = _ => NavReachability.Reachable;

            CheckReport report = LevelConsistencyCheck.Check(src, sampler);

            Assert.That(report.IsGreen, Is.True);
            Assert.That(report.Warnings.Count, Is.GreaterThan(0), "反方向的矛盾没有被报出");
            Assert.That(Report(report.Warnings), Does.Contain("逻辑不可走"),
                "报文须说清是反方向的漂移");
        }

        /// <summary>AC-4(三态):`Unknown`(本场景无 NavMesh 数据)⇒ **零告警**。</summary>
        /// <remarks><b>为什么这条是必需的</b>:P0 今日没有 baked NavMesh(spike 实测)。
        /// 若把 `Unknown` 当「不可达」,C3 会对每一个可走格报一次 ⇒ 上千条噪声把真告警淹掉,
        /// 而那种「一跑就满屏警告」的检查在实践中等于不存在。
        /// <b>判别力</b>:把 `continue` 那行删掉 ⇒ 本测红(告警数 = 可走格数)。</remarks>
        [Test]
        public void test_ac4_unknownNavMeshData_producesZeroWarnings()
        {
            LevelSource src = P0();
            Func<WorldPos, NavReachability> sampler = _ => NavReachability.Unknown;

            CheckReport report = LevelConsistencyCheck.Check(src, sampler);

            Assert.That(report.IsGreen, Is.True);
            Assert.That(report.Warnings, Is.Empty,
                "`Unknown` = 本场景没有 NavMesh 数据 ⇒ 不告警(spike 结论);" +
                "报了就会出现「每格一条」的噪声洪流");
        }

        /// <summary>AC-4(正例):两侧一致 ⇒ 零告警。</summary>
        [Test]
        public void test_ac4_consistentSides_produceZeroWarnings()
        {
            LevelSource src = P0();
            Func<WorldPos, NavReachability> sampler = cell =>
            {
                bool walkable = src.Walkable[
                    (cell.X * src.Height + cell.Y) * src.Depth + cell.Z] == 1;
                return walkable ? NavReachability.Reachable : NavReachability.Unreachable;
            };

            CheckReport report = LevelConsistencyCheck.Check(src, sampler);

            Assert.That(report.IsGreen, Is.True);
            Assert.That(report.Warnings, Is.Empty, "两侧逐格一致时不得有任何告警");
        }

        /// <summary>AC-4②:导出入口**不因 C3 失败** —— `ExportNavChunks` 在有矛盾时仍返回产物。</summary>
        /// <remarks><b>判别力</b>:把 C3 的告警改成 `throw` ⇒ 本测红(构造它的调用会抛)。
        /// ⚠️ 本测走的是**生产采样器**(无 baked NavMesh ⇒ 全 `Unknown` ⇒ 零告警),
        /// 故它证的是「C3 在导出链路上不阻断」,不证「有矛盾时的具体报文」(那由上面三条合成采样器覆盖)。</remarks>
        [Test]
        public void test_ac4_exportNavChunks_doesNotFailOnNavMeshMismatch()
        {
            IReadOnlyList<DataSetProduct> products = null;
            Assert.DoesNotThrow(() => products = LevelExporter.ExportNavChunks(P0()),
                "C3 只告警 —— 导出不得因它失败");
            Assert.That(products.Count, Is.EqualTo(4));
        }

        // ══════════════════ AC-5:烘焙 + 进组 ══════════════════

        /// <summary>AC-5:各 chunk 产物已烘焙、已进 `data-core`,且组条目数 = 盘上产物数。</summary>
        /// <remarks><b>判别力</b>:少烘一片 / 少登记一条 ⇒ 两条断言各红一条。</remarks>
        [Test]
        public void test_ac5_allChunksBaked_andRegisteredInDataCoreGroup()
        {
            LevelSource src = P0();
            int chunkCount = src.GetChunkTopology().ChunkCount;

            string groupPath = Path.Combine(
                RepoRoot, "unity", "Assets", "AddressableAssetsData", "AssetGroups", "data-core.asset");
            Assert.That(File.Exists(groupPath), Is.True, "data-core 组定义缺失(它随仓库入库)");
            string group = File.ReadAllText(groupPath, System.Text.Encoding.UTF8);

            for (int i = 0; i < chunkCount; i++)
            {
                string cookedName = WorldNavChunkDataSet.CookedFileNameFor(i);
                Assert.That(File.Exists(Path.Combine(CookedDir, cookedName)), Is.True,
                    $"盘上缺 {cookedName} —— 重烘:大医精诚/数据管线/烘焙 world_nav");
                Assert.That(group, Does.Contain(cookedName),
                    $"data-core 组未登记 {cookedName} ⇒ catalog 里它的 address 查不到 Location");
            }

            // 组条目数 == 盘上 *.cooked.bytes 件数(与 `BuildAddressablesContentCore` 的自检同一判据)。
            int onDisk = Directory.GetFiles(CookedDir, "*.cooked.bytes").Length;
            int inGroup = Regex.Matches(group, "- m_GUID:").Count;
            Assert.That(inGroup, Is.EqualTo(onDisk),
                $"组条目 {inGroup} ≠ 盘上产物 {onDisk} —— " +
                "「新产物必登记」靠人记是不成立的,这条判据是那件事的机械面");
        }

        /// <summary>AC-5:登记的四个出货产物名 == **参数化**名字函数的结果。</summary>
        /// <remarks><b>为什么需要这条</b>:产物名有两条算出路径 —— 烘焙侧的**字面量登记表**
        /// (`DataBakeMenu.WorldNavChunkCookedAssetNames`,给漂移门与组条目用)与读侧的**参数化**
        /// 名字(`WorldNavChunkDataSet.CookedFileNameFor`,给 `DataCookedDisk` 用)。
        /// 两者分家时,盘上写的名字与运行期查的名字不同 ⇒ **E-13 启动期硬失败**,而症状
        /// (「产物不存在」)离病根(「两个地方各拼了一遍」)极远。
        /// <b>判别力</b>:任一侧改拼接方式(多一个下划线 / 改扩展名)⇒ 红。</remarks>
        [Test]
        public void test_ac5_registeredNames_matchParameterizedName()
        {
            string[] registered = DataBakeMenu.WorldNavChunkCookedAssetNames;

            Assert.That(registered.Length, Is.EqualTo(P0().GetChunkTopology().ChunkCount),
                "登记表长度须等于 P0 的 chunk 数(4)");

            for (int i = 0; i < registered.Length; i++)
                Assert.That(WorldNavChunkDataSet.CookedFileNameFor(i), Is.EqualTo(registered[i]),
                    $"读侧参数化名字与烘焙侧登记表在 chunk {i} 上分家 —— " +
                    "落盘的名字与运行期查的名字不同 ⇒ E-13");
        }

        /// <summary>AC-5(边界):导航格切片**不进** `DataCoreAddresses`(「首次 Step 前常驻」不适用)。</summary>
        /// <remarks>ADR-022 §五 逐字补注:ADR-014 §五 的常驻约束「只约束小体量启动前置数据,
        /// 逻辑导航格除外」。⚠️ 本测钉的是**当下的口径**,不是「永远不许常驻」——
        /// 若将来把它并进预载,须同批改本测并说明理由。</remarks>
        [Test]
        public void test_ac5_navChunksAreNotInFirstStepPreloadList()
        {
            FieldInfo field = typeof(DataCorePreloader).GetField(
                "DataCoreAddresses", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.That(field, Is.Not.Null, "`DataCorePreloader.DataCoreAddresses` 不见了");
            var addresses = (string[])field.GetValue(null);

            for (int i = 0; i < 4; i++)
                Assert.That(addresses, Has.No.Member(WorldNavChunkDataSet.CookedFileNameFor(i)),
                    "导航格切片进了「首次 Step 前常驻」清单 —— ADR-022 §五 明确把它排除在外");
        }

        /// <summary>AC-5:切片烘焙**确定性** —— 同源两次烘焙逐字节相同。</summary>
        [Test]
        public void test_ac5_bakeTwice_bitIdentical()
        {
            string path = Path.Combine(RepoRoot, "assets", "data", "world_nav_0.json");
            Assert.That(File.Exists(path), Is.True, "作者态源缺失:" + path);
            string json = File.ReadAllText(path, System.Text.Encoding.UTF8);

            WorldNavChunkBake.BakeOutput a = WorldNavChunkBake.BakeFromSourceText(json, 0);
            WorldNavChunkBake.BakeOutput b = WorldNavChunkBake.BakeFromSourceText(json, 0);

            Assert.That(Convert.ToBase64String(a.Cooked), Is.EqualTo(Convert.ToBase64String(b.Cooked)),
                "两次烘焙字节不同 ⇒ 烘焙不确定(掺了 Guid / 字典序 / 时间)");
            Assert.That(a.ConfigVersion, Is.EqualTo(b.ConfigVersion));
        }

        /// <summary>AC-5(负例):把 A 片的字节装到 B 片的地址上 ⇒ 抛,报文点名「chunk 号」。</summary>
        [Test]
        public void test_ac5_codec_rejectsMismatchedChunkIndex()
        {
            string path = Path.Combine(RepoRoot, "assets", "data", "world_nav_0.json");
            string json = File.ReadAllText(path, System.Text.Encoding.UTF8);
            byte[] cooked = WorldNavChunkBake.BakeFromSourceText(json, 0).Cooked;

            var ex = Assert.Throws<InvalidDataException>(() => DataCookedDiskNavProbe(cooked, expectedChunkIndex: 1));
            Assert.That(ex.Message, Does.Contain("chunk"), "报文须点名 chunk(否则「装错块」会被误诊为「字节损坏」)");
        }

        /// <summary>调 codec 而不经磁盘(把「读盘」这一变量从判据里去掉)。</summary>
        private static WorldNavChunkDataSet DataCookedDiskNavProbe(byte[] cooked, int expectedChunkIndex)
            => WorldNavChunkCookedCodec.Read(cooked, expectedChunkIndex);

        // ══════════════════ AC-6:未实现项 5 → 3 ══════════════════

        /// <summary>AC-6:`UnimplementedChecks` 恰 3 条,且 C3 / C6 **已移出**。</summary>
        /// <remarks><b>判别力</b>:少删一条 ⇒ 长度不符;C3/C6 还留着 ⇒ 第二条断言红。
        /// <para>🔁 <b>2026-10-29(story-006 AC-5)原文保留 + 判据迁走</b>:C1 / C2 / C2' 已全部落地,
        /// 清单归零 ⇒ 本条「恰 3 条」的判据**已无宾语**。当前条数判据 = story-006 的
        /// `consistency_c1_c2_c2prime_test::test_ac5_unimplementedChecks_isEmpty`(判 0 条)。
        /// 本方法**不删**:它记录的是「C3 / C6 移出」那一刻的判据,删掉会让那一轮的证据消失。</para></remarks>
        [Test]
        public void test_ac6_unimplementedChecks_dropsC3AndC6_leavingThree()
        {
            Assert.Ignore("NOT-RUN: 本测的宾语已不存在 —— 2026-10-29 story-006 起 `UnimplementedChecks` 归零" +
                          "(C1 / C2 / C2' 全部落地)。**这不是通过,是无宾语**。" +
                          "当前判据见 `consistency_c1_c2_c2prime_test::test_ac5_unimplementedChecks_isEmpty`。");
        }

        // ══════════════════════════ 工具 ══════════════════════════

        private static string Report(IReadOnlyList<string> items)
            => items == null ? string.Empty : string.Join("\n", items);
    }
}
