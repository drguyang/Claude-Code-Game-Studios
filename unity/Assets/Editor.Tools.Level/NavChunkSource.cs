// 导航格的**切片器 + 切片完整性检查(C6)**(关卡工具 · 2026-10-29 · story-005)。
//
// 权威来源:
//   ADR-022 §五(**关卡工具是切片者**;`world_nav_{chunk}.json` 的 `{chunk}` 键 = ADR-015 §四 的同一 chunk)
//   ADR-022 §四 **C6 切片完整性 = 硬失败**(显式 `throw`,非告警)
//   ADR-015 §四(**chunk 是流式 / 脏块粒度,不是坐标原点** —— 禁 chunk 局部坐标系)
//   `production/epics/level-tool/story-005-world-nav-chunk-slicing.md` AC-2 / AC-3
//
// ═══════════════════════════════════════════════════════════════════════════
// §切片的形状
// ═══════════════════════════════════════════════════════════════════════════
//   · chunk 划分**只由** `ChunkTopology.ForWorld(...)` 给出(与几何侧同源,AC-2②)。
//   · 每块一份**稠密**数组,长度 = `chunkSizeX × chunkSizeY × chunkSizeZ`,
//     索引 = `(lx*chunkSizeY + ly)*chunkSizeZ + lz`(chunk 内局部坐标)。
//   · 落在**世界之外**的衬垫格一律写 `0`(不可走)。⚠️ 衬垫不是数据:
//     它只让所有块等长;C6 只数**世界格**,不数衬垫。
//
// ═══════════════════════════════════════════════════════════════════════════
// §C6 为什么是「事后校验」而不是「靠构造保证」
// ═══════════════════════════════════════════════════════════════════════════
//   若切片正确,`Slice()` 的产物**天然**每格覆盖一次 ⇒ C6 永不失败。那正是它该有的样子:
//   它防的不是「`Slice()` 写错」这一种,而是**整条产出链**上任何一处让「格 → 切片」
//   的映射失真 —— 手工改产物、换一个切片者、将来把切片挪到别处(ADR-022 §五 允许)、
//   甚至「两个 chunk 文件被写成同一号」。这些**都不经过 `Slice()`**。
//   所以 C6 的输入是**切片集合**(不论谁造的),不是「我刚造的那一份」。
//
//   ⚠️ 漏格与重复格是**两条独立判据**,不得合并成一条 —— 前者表现为「那格不可走」
//   (AI 绕远),后者表现为「取哪一份」(同格两个答案,先到者赢 ⇒ 非确定性)。
//   一个夹具同时验两条,会让停用任一分支时另一条仍红 ⇒ 两条都失去判别力(story AC-3 的警示)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;   // WorldPos
using DaYiJingCheng.Sim.World;       // ChunkTopology(chunk 划分的唯一函数)

namespace DaYiJingCheng.EditorTools.Level
{
    /// <summary>
    /// 一个 chunk 的导航格切片(稠密数组;chunk 内局部索引)。
    /// </summary>
    public readonly struct NavChunkSlice
    {
        /// <summary>chunk 一维索引(<see cref="ChunkTopology.ChunkToIndex"/>)。</summary>
        public readonly int ChunkIndex;
        /// <summary>chunk 坐标 x。</summary>
        public readonly int ChunkX;
        /// <summary>chunk 坐标 y。</summary>
        public readonly int ChunkY;
        /// <summary>chunk 坐标 z。</summary>
        public readonly int ChunkZ;

        /// <summary>稠密可走性(0/1;长度 = `chunkSizeX × chunkSizeY × chunkSizeZ`)。</summary>
        public readonly IReadOnlyList<int> Walkable;

        /// <summary>构造一个切片。</summary>
        public NavChunkSlice(int chunkIndex, int chunkX, int chunkY, int chunkZ, IReadOnlyList<int> walkable)
        {
            ChunkIndex = chunkIndex; ChunkX = chunkX; ChunkY = chunkY; ChunkZ = chunkZ;
            Walkable = walkable;
        }
    }

    /// <summary>把一维可走性数组按 chunk 切成若干片(AC-2 的产出面)。</summary>
    public static class NavChunkSlicer
    {
        /// <summary>
        /// 切分。<see cref="ChunkTopology.ForWorld"/> 是划分的**唯一**出处(AC-2②)。
        /// </summary>
        /// <param name="sizeX">世界 X 格数(&gt; 0)。</param>
        /// <param name="sizeY">世界 Y 格数(&gt; 0)。</param>
        /// <param name="sizeZ">世界 Z 格数(&gt; 0)。</param>
        /// <param name="chunkSizeX">chunk X 边长(格,&gt; 0)。</param>
        /// <param name="chunkSizeY">chunk Y 边长(格,&gt; 0)。</param>
        /// <param name="chunkSizeZ">chunk Z 边长(格,&gt; 0)。</param>
        /// <param name="walkable">每格可走性(0/1;长度 = `sizeX×sizeY×sizeZ`;索引 = `(x*sizeY + y)*sizeZ + z`)。</param>
        /// <returns>切片列表,按 <see cref="NavChunkSlice.ChunkIndex"/> 升序。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="walkable"/> 为 null。</exception>
        /// <exception cref="ArgumentException">数组长度不等于世界格数 / 含非 0/1 值。</exception>
        public static IReadOnlyList<NavChunkSlice> Slice(
            int sizeX, int sizeY, int sizeZ,
            int chunkSizeX, int chunkSizeY, int chunkSizeZ,
            IReadOnlyList<int> walkable)
        {
            if (walkable == null) throw new ArgumentNullException(nameof(walkable));

            ChunkTopology topo = ChunkTopology.ForWorld(sizeX, sizeY, sizeZ, chunkSizeX, chunkSizeY, chunkSizeZ);

            long total = (long)sizeX * sizeY * sizeZ;
            if (walkable.Count != total)
                throw new ArgumentException(
                    $"可走性数组长度 = {walkable.Count},须等于世界格数 {total}", nameof(walkable));

            long chunkCells = (long)chunkSizeX * chunkSizeY * chunkSizeZ;
            if (chunkCells > int.MaxValue)
                throw new ArgumentException($"chunk 格数 {chunkCells} 超出 int 域", nameof(chunkSizeX));

            var result = new List<NavChunkSlice>(topo.ChunkCount);
            for (int ci = 0; ci < topo.ChunkCount; ci++)
            {
                WorldPos chunk = topo.IndexToChunk(ci);
                var cells = new int[(int)chunkCells];   // 衬垫格保持 0(世界之外 = 不可走)
                FillChunkCells(cells, chunk, sizeX, sizeY, sizeZ,
                    chunkSizeX, chunkSizeY, chunkSizeZ, walkable);
                result.Add(new NavChunkSlice(ci, chunk.X, chunk.Y, chunk.Z, Array.AsReadOnly(cells)));
            }

            return result;
        }

        /// <summary>把世界可走性搬进一个 chunk 的局部数组(越界处 `break`,衬垫保持 0)。</summary>
        /// <remarks>🔁 <b>2026-10-29 拆分</b>(原 <see cref="Slice"/> 50 行 &gt; 40)。
        /// ⚠️ 三层都用的 `break`(而不是 `continue`)是原样保留的 —— 轴越界后该轴之后的局部坐标
        /// 只会更越界,`break` 是**等价且更快**的写法,改成 `continue` 结果一样但慢一档。</remarks>
        private static void FillChunkCells(int[] cells, WorldPos chunk,
            int sizeX, int sizeY, int sizeZ,
            int chunkSizeX, int chunkSizeY, int chunkSizeZ, IReadOnlyList<int> walkable)
        {
            int baseX = chunk.X * chunkSizeX;
            int baseY = chunk.Y * chunkSizeY;
            int baseZ = chunk.Z * chunkSizeZ;

            for (int lx = 0; lx < chunkSizeX; lx++)
            {
                int x = baseX + lx;
                if (x >= sizeX) break;
                for (int ly = 0; ly < chunkSizeY; ly++)
                {
                    int y = baseY + ly;
                    if (y >= sizeY) break;
                    for (int lz = 0; lz < chunkSizeZ; lz++)
                    {
                        int z = baseZ + lz;
                        if (z >= sizeZ) break;
                        cells[(lx * chunkSizeY + ly) * chunkSizeZ + lz] =
                            walkable[(x * sizeY + y) * sizeZ + z];
                    }
                }
            }
        }
    }

    /// <summary>ADR-022 §四 **C6 切片完整性** = 每格**恰好覆盖一次**(硬失败)。</summary>
    public static class NavChunkSliceCheck
    {
        /// <summary>
        /// 校验切片集合覆盖了每一格且只覆盖一次。
        /// <para>⚠️ 两条违反**独立**收集(漏格 / 重复格),不合并 —— 见文件头 §C6 末段。</para>
        /// </summary>
        /// <param name="sizeX">世界 X 格数。</param>
        /// <param name="sizeY">世界 Y 格数。</param>
        /// <param name="sizeZ">世界 Z 格数。</param>
        /// <param name="chunkSizeX">chunk X 边长。</param>
        /// <param name="chunkSizeY">chunk Y 边长。</param>
        /// <param name="chunkSizeZ">chunk Z 边长。</param>
        /// <param name="slices">切片集合(**不论谁造的** —— 见文件头)。</param>
        /// <param name="violations">违反项(空 = 通过)。</param>
        /// <returns>是否全绿。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="slices"/> 为 null。</exception>
        public static bool TryCheck(
            int sizeX, int sizeY, int sizeZ,
            int chunkSizeX, int chunkSizeY, int chunkSizeZ,
            IReadOnlyList<NavChunkSlice> slices, out List<string> violations)
        {
            if (slices == null) throw new ArgumentNullException(nameof(slices));

            ChunkTopology topo = ChunkTopology.ForWorld(sizeX, sizeY, sizeZ, chunkSizeX, chunkSizeY, chunkSizeZ);
            violations = new List<string>();

            int total = sizeX * sizeY * sizeZ;
            var coverCount = new int[total];

            AccumulateCoverage(slices, topo, sizeX, sizeY, sizeZ,
                chunkSizeX, chunkSizeY, chunkSizeZ, coverCount, violations);

            ReportMissingCells(coverCount, sizeY, sizeZ, violations);
            ReportDuplicateCells(coverCount, sizeY, sizeZ, violations);

            return violations.Count == 0;
        }

        /// <summary>统计每格被多少个切片覆盖(形状非法的切片先把报文收进 `violations`)。</summary>
        /// <remarks>🔁 <b>2026-10-29 拆分</b>(原 <see cref="TryCheck"/> 69 行 &gt; 40)。</remarks>
        private static void AccumulateCoverage(IReadOnlyList<NavChunkSlice> slices, ChunkTopology topo,
            int sizeX, int sizeY, int sizeZ, int chunkSizeX, int chunkSizeY, int chunkSizeZ,
            int[] coverCount, List<string> violations)
        {
            for (int s = 0; s < slices.Count; s++)
            {
                NavChunkSlice slice = slices[s];
                int[] cells = ToArray(slice, topo, "slices[" + s + "]", out List<string> shapeErr);
                if (cells == null)
                {
                    violations.AddRange(shapeErr);
                    continue;
                }

                int baseX = slice.ChunkX * chunkSizeX;
                int baseY = slice.ChunkY * chunkSizeY;
                int baseZ = slice.ChunkZ * chunkSizeZ;

                for (int lx = 0; lx < chunkSizeX; lx++)
                {
                    int x = baseX + lx;
                    if (x < 0 || x >= sizeX) continue;
                    for (int ly = 0; ly < chunkSizeY; ly++)
                    {
                        int y = baseY + ly;
                        if (y < 0 || y >= sizeY) continue;
                        for (int lz = 0; lz < chunkSizeZ; lz++)
                        {
                            int z = baseZ + lz;
                            if (z < 0 || z >= sizeZ) continue;
                            coverCount[(x * sizeY + y) * sizeZ + z]++;
                        }
                    }
                }
            }
        }

        /// <summary>① 漏格:某世界格不在任何切片里。</summary>
        private static void ReportMissingCells(int[] coverCount, int sizeY, int sizeZ,
            List<string> violations)
        {
            int total = coverCount.Length;
            for (int i = 0; i < total && violations.Count < MaxReported; i++)
            {
                if (coverCount[i] != 0) continue;
                int z = i % sizeZ, y = (i / sizeZ) % sizeY, x = i / (sizeY * sizeZ);
                violations.Add(
                    $"[关卡工具] C6 切片**漏格**:格 ({x},{y},{z}) 不在任何 chunk 切片里 —— " +
                    "该格在运行期永远查不到导航数据(表现为「那格不可走」)," +
                    "AI 会绕远路而不是报错;切片集合必须覆盖每一格恰一次");
            }
        }

        /// <summary>② 重复格:某世界格出现在两个以上切片里。</summary>
        private static void ReportDuplicateCells(int[] coverCount, int sizeY, int sizeZ,
            List<string> violations)
        {
            int total = coverCount.Length;
            for (int i = 0; i < total && violations.Count < MaxReported; i++)
            {
                if (coverCount[i] <= 1) continue;
                int z = i % sizeZ, y = (i / sizeZ) % sizeY, x = i / (sizeY * sizeZ);
                violations.Add(
                    $"[关卡工具] C6 切片**重复格**:格 ({x},{y},{z}) 被 {coverCount[i]} 个切片覆盖 —— " +
                    "同格两个答案 ⇒ 「取哪一份」取决于遍历序(非确定性)," +
                    "而两份数据若不同,重放不再逐位一致");
            }
        }

        /// <summary>违反项的报出上限(防一整张世界全漏时刷屏;超出即截断,不静默)。</summary>
        private const int MaxReported = 20;

        /// <summary>把切片的可走性取出并做形状校验(失败返回 null 并写 <paramref name="violations"/>)。</summary>
        private static int[] ToArray(
            NavChunkSlice slice, ChunkTopology topo, string where, out List<string> violations)
        {
            violations = null;
            if (slice.Walkable == null)
            {
                violations = new List<string> { $"[关卡工具] C6 {where} 的可走性数组为 null" };
                return null;
            }
            int expected = topo.ChunkSizeX * topo.ChunkSizeY * topo.ChunkSizeZ;
            if (slice.Walkable.Count != expected)
            {
                violations = new List<string>
                {
                    $"[关卡工具] C6 {where} 的数组长度 = {slice.Walkable.Count},须等于 chunk 格数 {expected}",
                };
                return null;
            }
            var cells = new int[expected];
            for (int i = 0; i < expected; i++) cells[i] = slice.Walkable[i];
            return cells;
        }
    }
}
