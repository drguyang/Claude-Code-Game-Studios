// 导航格的**实现**(`Sim` 侧句柄;契约在 `Sim.Contracts/INavLattice.cs`)。
//
// 权威来源:
//   `production/epics/level-tool/story-005-world-nav-chunk-slicing.md` AC-1(BLOCKING)
//   · ADR-022 §五(**未驻留 chunk = 全 block**)· ADR-015 §四(chunk 是流式粒度,非坐标原点)
//   · ADR-016 §三(AI 读粗粒度整数格,不读表现态位置)
//
// ═══════════════════════════════════════════════════════════════════════════
// §形状与判据
// ═══════════════════════════════════════════════════════════════════════════
//   · 世界尺寸 + chunk 划分 = `ChunkTopology.ForWorld(...)`(**唯一**的划分函数,
//     与几何侧 / 切片器同源 —— 见该方法的注释)。
//   · 每个 chunk 一份**稠密**可走性数组,长度 = `chunkSizeX × chunkSizeY × chunkSizeZ`,
//     索引 = `(lx*chunkSizeY + ly)*chunkSizeZ + lz`(`lx/ly/lz` 是**chunk 内**局部坐标)。
//     落在世界之外的「衬垫格」一律写 0(不可走)—— 衬垫不是数据,是让每块等长。
//   · **未挂载的 chunk** ⇒ `IsWalkable` 回 `false`(block)。判定方式 = 该 chunk 索引处
//     没有数组(`_chunkCells[idx] == null`),**不是**「数组全 0」—— 两者语义不同:
//     前者是「没装载」,后者是「装载了、这格就是不可走」。合并两者会让
//     「数据缺失」与「数据说不可走」在类型上不可分。
//
// ⚠️ **本类不可变**:构造时给定驻留集合,之后不增不减。要变 = 造一个新的
//    (与 `PoiStateMachine` / `EcozoneRegistry` 同款的「装载期一次性构造」纪律)。
//    可变会让「同一格在两次查询之间换了答案」成为可能,而那种非确定性
//    在任何重放判据下都无法归因。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.World
{
    /// <summary>
    /// 基于 chunk 切片的导航格实现(<see cref="INavLattice"/>)。
    /// <para>未驻留的 chunk 一律回 <c>false</c>(block,ADR-022 §五 / story-005 AC-1)。</para>
    /// </summary>
    public sealed class NavLattice : INavLattice
    {
        private readonly int _sizeX;
        private readonly int _sizeY;
        private readonly int _sizeZ;
        private readonly ChunkTopology _topology;
        private readonly int[][] _chunkCells;   // [chunkIndex] ⇒ 稠密可走性;null = 未驻留

        /// <summary>本导航格的世界尺寸 + chunk 划分。</summary>
        public ChunkTopology Topology => _topology;

        /// <summary>已驻留 chunk 数(诊断用;不参与判定)。</summary>
        public int ResidentChunkCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _chunkCells.Length; i++) if (_chunkCells[i] != null) n++;
                return n;
            }
        }

        /// <summary>
        /// 构造导航格。
        /// </summary>
        /// <param name="sizeX">世界 X 格数(&gt; 0)。</param>
        /// <param name="sizeY">世界 Y 格数(&gt; 0)。</param>
        /// <param name="sizeZ">世界 Z 格数(&gt; 0)。</param>
        /// <param name="chunkSizeX">chunk X 边长(格,&gt; 0)。</param>
        /// <param name="chunkSizeY">chunk Y 边长(格,&gt; 0)。</param>
        /// <param name="chunkSizeZ">chunk Z 边长(格,&gt; 0)。</param>
        /// <param name="residentChunks">
        /// 已驻留的切片:`chunk 一维索引 → 稠密可走性`。缺席的索引 = **未驻留**。
        /// </param>
        /// <exception cref="ArgumentNullException"><paramref name="residentChunks"/> 为 null。</exception>
        /// <exception cref="ArgumentException">某切片的 chunk 索引越界 / 数组长度不等于 chunk 格数 / 含非 0/1 值。</exception>
        public NavLattice(
            int sizeX, int sizeY, int sizeZ,
            int chunkSizeX, int chunkSizeY, int chunkSizeZ,
            IReadOnlyDictionary<int, IReadOnlyList<int>> residentChunks)
        {
            if (residentChunks == null) throw new ArgumentNullException(nameof(residentChunks));

            _topology = ChunkTopology.ForWorld(sizeX, sizeY, sizeZ, chunkSizeX, chunkSizeY, chunkSizeZ);
            _sizeX = sizeX; _sizeY = sizeY; _sizeZ = sizeZ;

            long chunkCells = (long)chunkSizeX * chunkSizeY * chunkSizeZ;
            if (chunkCells > int.MaxValue)
                throw new ArgumentOutOfRangeException(
                    nameof(chunkSizeX), $"chunk 格数 {chunkCells} 超出 int 域 —— 块内索引不可表示");

            _chunkCells = new int[_topology.ChunkCount][];
            foreach (KeyValuePair<int, IReadOnlyList<int>> kv in residentChunks)
            {
                int idx = kv.Key;
                if (idx < 0 || idx >= _topology.ChunkCount)
                    throw new ArgumentException(
                        $"切片 chunk 索引 = {idx} 越界 —— 本世界共 {_topology.ChunkCount} 个 chunk " +
                        $"({_topology.ChunksX}×{_topology.ChunksY}×{_topology.ChunksZ})", nameof(residentChunks));

                IReadOnlyList<int> cells = kv.Value;
                if (cells == null)
                    throw new ArgumentException($"切片 chunk {idx} 的可走性数组为 null", nameof(residentChunks));
                if (cells.Count != (int)chunkCells)
                    throw new ArgumentException(
                        $"切片 chunk {idx} 的数组长度 = {cells.Count},须等于 chunk 格数 {chunkCells} —— " +
                        "长度不齐会让块内索引错位(静默:相邻 chunk 的格子互相顶替)", nameof(residentChunks));

                var copy = new int[cells.Count];
                for (int i = 0; i < cells.Count; i++)
                {
                    if (cells[i] != 0 && cells[i] != 1)
                        throw new ArgumentException(
                            $"切片 chunk {idx} 第 {i} 项 = {cells[i]} —— 只接受 0/1" +
                            "(非 0/1 会让读方猜「非零即真」,而 0b10 之类的位值是真值域里的假值)",
                            nameof(residentChunks));
                    copy[i] = cells[i];
                }
                _chunkCells[idx] = copy;
            }
        }

        /// <summary>该格是否可走;越界 / 未驻留 chunk ⇒ <c>false</c>(block)。</summary>
        public bool IsWalkable(WorldPos cell)
        {
            if (cell.X < 0 || cell.X >= _sizeX ||
                cell.Y < 0 || cell.Y >= _sizeY ||
                cell.Z < 0 || cell.Z >= _sizeZ)
                return false;

            WorldPos chunk = _topology.WorldToChunk(cell);
            int chunkIndex = _topology.ChunkToIndex(chunk);
            int[] cells = _chunkCells[chunkIndex];
            if (cells == null) return false;   // ⚠️ 未驻留 ⇒ block(AC-1 的判据)

            int lx = cell.X - chunk.X * _topology.ChunkSizeX;
            int ly = cell.Y - chunk.Y * _topology.ChunkSizeY;
            int lz = cell.Z - chunk.Z * _topology.ChunkSizeZ;
            int local = (lx * _topology.ChunkSizeY + ly) * _topology.ChunkSizeZ + lz;
            return cells[local] == 1;
        }
    }
}
