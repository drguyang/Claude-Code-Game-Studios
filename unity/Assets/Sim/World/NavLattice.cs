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
// 🔁 **2026-10-29(story-005 收口后的接线轮)就地订正,原文不删**:
//    这里原先写「**本类不可变**:构造时给定驻留集合,之后不增不减 …… 可变会让
//    「同一格在两次查询之间换了答案」成为可能,而那种非确定性在任何重放判据下都无法归因」。
//
//    **那段理由对「数据」成立,对「驻留」不成立** —— ADR-022 §五 与 ADR-023 ⑥ 的语义
//    恰恰就是「同一格在不同时刻可能驻留 / 不驻留」;而它**仍然确定**,因为驻留集是
//    纯函数:`ChunkActivator.ComputeActiveChunks(玩家格)` ⇒ 同一事件流重放 ⇒ 同一驻留序列。
//    真正的非确定性来自「不经事件流的第二输入」(墙钟 / 表现态位置),那由 ADR-016 §三 禁掉。
//
//    ⇒ 现形状:**数据装载源 + 驻留集**两者分离 ——
//      · `_sourceChunks`:构造期一次性给定的全部切片(**不变**,装载期构造纪律不变);
//      · `_resident` :派生集,由 <see cref="SetResidency"/> 按激活器的纯函数结果重写。
//    `SetResidency` 只改驻留位、不碰数据 ⇒ 「未驻留 = block」在类型上始终可观测。

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
        private readonly int[][] _chunkCells;   // [chunkIndex] ⇒ 稠密可走性;null = **无数据**
        private readonly bool[] _resident;      // [chunkIndex] ⇒ 当前是否驻留(派生集)

        /// <summary>本导航格的世界尺寸 + chunk 划分。</summary>
        public ChunkTopology Topology => _topology;

        /// <summary>已驻留 chunk 数(诊断用;不参与判定)。</summary>
        public int ResidentChunkCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _chunkCells.Length; i++)
                    if (_resident[i] && _chunkCells[i] != null) n++;
                return n;
            }
        }

        /// <summary>某 chunk 当前是否驻留(诊断 / 夹具用;`IsWalkable` 不依赖它做二次判断)。</summary>
        /// <param name="chunkIndex">chunk 一维索引。</param>
        public bool IsResident(int chunkIndex)
        {
            if (chunkIndex < 0 || chunkIndex >= _resident.Length) return false;
            return _resident[chunkIndex] && _chunkCells[chunkIndex] != null;
        }

        /// <summary>
        /// **重写驻留集**(ADR-022 §五 / ADR-023 ⑥:系统 6 按 `ChunkActivator` 的纯函数结果调用)。
        /// <para>⚠️ 只改驻留位,**不碰数据** —— 「有数据但不驻留」与「根本无数据」在
        /// <see cref="IsWalkable"/> 上同回 `false`(都是 block),但 <see cref="IsResident"/> 分得开。</para>
        /// <para>⚠️ 索引集超出「有数据的 chunk」的部分被忽略(激活器可能给出整张网格,
        /// 而本世界只装载了一部分产物)。</para>
        /// </summary>
        /// <param name="activeChunks">按 <see cref="ChunkTopology.ChunkToIndex"/> 索引的激活位图。</param>
        /// <exception cref="ArgumentNullException"><paramref name="activeChunks"/> 为 null。</exception>
        /// <exception cref="ArgumentException">位图长度不等于 chunk 总数。</exception>
        public void SetResidency(IReadOnlyList<bool> activeChunks)
        {
            if (activeChunks == null) throw new ArgumentNullException(nameof(activeChunks));
            if (activeChunks.Count != _topology.ChunkCount)
                throw new ArgumentException(
                    $"激活位图长度 = {activeChunks.Count},须等于本世界的 chunk 数 {_topology.ChunkCount} —— " +
                    "长度不齐会让激活集整体错位(静默:驻留的块与玩家所在的块不是同一批)",
                    nameof(activeChunks));

            for (int i = 0; i < _resident.Length; i++)
                _resident[i] = activeChunks[i] && _chunkCells[i] != null;
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

            // 构造后**默认全驻留**:给了数据的块视为已在场。
            // ⚠️ 这让既有的「不在字典里 = block」夹具语义不变(那几块**没有数据**),
            //    而「有数据但不驻留」是新增的第二种 block 来源,由 `SetResidency` 产生。
            _resident = new bool[_topology.ChunkCount];
            for (int i = 0; i < _chunkCells.Length; i++) _resident[i] = _chunkCells[i] != null;
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
            // ⚠️ **未驻留 ⇒ block**(AC-1 的判据)。两种来源同回 false:
            //    ① 本世界没有这一块的数据(`cells == null`)② 有数据但当前不驻留。
            //    合并成一条 `if` 是**故意的**:对调用方而言两者语义相同(「现在查不到这一格」),
            //    区分它们只在诊断面(`IsResident` / `ResidentChunkCount`)。
            if (cells == null || !_resident[chunkIndex]) return false;

            int lx = cell.X - chunk.X * _topology.ChunkSizeX;
            int ly = cell.Y - chunk.Y * _topology.ChunkSizeY;
            int lz = cell.Z - chunk.Z * _topology.ChunkSizeZ;
            int local = (lx * _topology.ChunkSizeY + ly) * _topology.ChunkSizeZ + lz;
            return cells[local] == 1;
        }
    }
}
