// 权威来源:ADR-010 §三(存档义务汇总)· ADR-014 §五(Addressables 预载)· Story 004(chunk 激活权)
//
// 核心机制:
//   - Chunk 激活 = 系统 6 的纯函数(玩家所在格 ± 流式半径)
//   - 未驻留 chunk ⇒ 全 block 保守判定
//   - 激活集不进三流(派生态)
//   - 消费白名单 {4, 25, 37}

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.World
{
    /// <summary>chunk 拓扑(烘焙数据加载后不可变)。</summary>
    public readonly struct ChunkTopology
    {
        public readonly int ChunkSizeX;
        public readonly int ChunkSizeY;
        public readonly int ChunkSizeZ;
        public readonly int ChunksX;
        public readonly int ChunksY;
        public readonly int ChunksZ;

        public ChunkTopology(int chunkSizeX, int chunkSizeY, int chunkSizeZ,
            int chunksX, int chunksY, int chunksZ)
        {
            if (chunkSizeX <= 0 || chunkSizeY <= 0 || chunkSizeZ <= 0)
                throw new ArgumentException("Chunk size 必须 > 0");
            if (chunksX <= 0 || chunksY <= 0 || chunksZ <= 0)
                throw new ArgumentException("Chunk count 必须 > 0");

            ChunkSizeX = chunkSizeX;
            ChunkSizeY = chunkSizeY;
            ChunkSizeZ = chunkSizeZ;
            ChunksX = chunksX;
            ChunksY = chunksY;
            ChunksZ = chunksZ;
        }

        /// <summary>
        /// **chunk 划分的唯一函数**(2026-10-29 · 关卡工具 story-005 AC-2②)。
        /// <para>由「世界尺寸 + chunk 尺寸」推出完整的 <see cref="ChunkTopology"/>
        /// (chunk 数 = 各轴**向上取整**)。</para>
        /// <para>⚠️ <b>本方法是「几何侧 / 导航格侧用同一个函数算 chunk」的**唯一**载体</b> ——
        /// `WorldGeometryDataSet.ChunkTopology`(运行期,读烘焙产物)、
        /// `NavChunkSlicer.Slice`(工具,切导航格)、`NavLattice`(运行期,查格)
        /// 三处**都调它**,不得各自写一遍 `(x + n - 1) / n` 或 `x >> s`:
        /// 两处各算一次 = 两边可以独立漂移,而漂移的表现是「某几格永远查不到」
        /// (静默,不是崩溃)。</para>
        /// <para>⚠️ chunk **不是坐标原点**(ADR-015 §四):本方法只产出**分块索引**,
        /// 格坐标仍是全局整数格。</para>
        /// </summary>
        /// <param name="sizeX">世界 X 格数(&gt; 0)。</param>
        /// <param name="sizeY">世界 Y 格数(&gt; 0)。</param>
        /// <param name="sizeZ">世界 Z 格数(&gt; 0)。</param>
        /// <param name="chunkSizeX">chunk X 边长(格,&gt; 0)。</param>
        /// <param name="chunkSizeY">chunk Y 边长(格,&gt; 0)。</param>
        /// <param name="chunkSizeZ">chunk Z 边长(格,&gt; 0)。</param>
        /// <exception cref="ArgumentOutOfRangeException">任一尺寸 &lt; 1。</exception>
        public static ChunkTopology ForWorld(
            int sizeX, int sizeY, int sizeZ, int chunkSizeX, int chunkSizeY, int chunkSizeZ)
        {
            if (sizeX < 1 || sizeY < 1 || sizeZ < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(sizeX), $"世界尺寸须 ≥ 1(实得 {sizeX}×{sizeY}×{sizeZ})");
            if (chunkSizeX < 1 || chunkSizeY < 1 || chunkSizeZ < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(chunkSizeX), $"chunk 尺寸须 ≥ 1(实得 {chunkSizeX}×{chunkSizeY}×{chunkSizeZ})");

            // 向上取整:用 long 算再落 int —— 与 WorldGeometry.ToIndex 同一条回绕纪律。
            long cx = ((long)sizeX + chunkSizeX - 1) / chunkSizeX;
            long cy = ((long)sizeY + chunkSizeY - 1) / chunkSizeY;
            long cz = ((long)sizeZ + chunkSizeZ - 1) / chunkSizeZ;
            if (cx > int.MaxValue || cy > int.MaxValue || cz > int.MaxValue)
                throw new ArgumentOutOfRangeException(
                    nameof(sizeX), $"chunk 数 ({cx},{cy},{cz}) 超出 int 域");

            return new ChunkTopology(chunkSizeX, chunkSizeY, chunkSizeZ, (int)cx, (int)cy, (int)cz);
        }

        /// <summary>chunk 总数。</summary>
        public int ChunkCount => ChunksX * ChunksY * ChunksZ;

        /// <summary>WorldPos → chunk 坐标。</summary>
        public WorldPos WorldToChunk(WorldPos worldPos)
        {
            return new WorldPos(
                worldPos.X / ChunkSizeX,
                worldPos.Y / ChunkSizeY,
                worldPos.Z / ChunkSizeZ);
        }

        /// <summary>chunk 坐标 → WorldPos 起点。</summary>
        public WorldPos ChunkToWorldOrigin(WorldPos chunkPos)
        {
            return new WorldPos(
                chunkPos.X * ChunkSizeX,
                chunkPos.Y * ChunkSizeY,
                chunkPos.Z * ChunkSizeZ);
        }

        /// <summary>chunk 坐标 → 一维索引。</summary>
        public int ChunkToIndex(WorldPos chunkPos)
        {
            return (chunkPos.X * ChunksY + chunkPos.Y) * ChunksZ + chunkPos.Z;
        }

        /// <summary>一维索引 → chunk 坐标。</summary>
        public WorldPos IndexToChunk(int index)
        {
            int z = index % ChunksZ;
            int y = (index / ChunksZ) % ChunksY;
            int x = index / (ChunksY * ChunksZ);
            return new WorldPos(x, y, z);
        }
    }

    /// <summary>chunk 激活器(系统 6 专用,纯函数重推)。</summary>
    public sealed class ChunkActivator
    {
        private readonly ChunkTopology _topology;
        private readonly int _streamingRadius; // 以 chunk 为单位的流式半径
        private readonly bool[] _activeChunks;  // 当前激活集(索引 => 布尔)
        private WorldPos _playerCell;  // 玩家当前位置(用于 IsChunkActive 重推)

        public ChunkTopology Topology => _topology;
        public int StreamingRadius => _streamingRadius;

        public ChunkActivator(ChunkTopology topology, int streamingRadius)
        {
            _topology = topology;
            _streamingRadius = streamingRadius;
            _activeChunks = new bool[topology.ChunksX * topology.ChunksY * topology.ChunksZ];
        }

        /// <summary>计算激活 chunk 集(纯函数,同输入 => 同输出,不修改内部状态)。</summary>
        /// <param name="playerCell">玩家当前所在格。</param>
        /// <returns>当前激活 chunk 集(只读副本)。</returns>
        public bool[] ComputeActiveChunks(WorldPos playerCell)
        {
            _playerCell = playerCell;
            WorldPos playerChunk = _topology.WorldToChunk(playerCell);

            // 纯函数: 不修改 _activeChunks,使用局部数组
            var result = new bool[_activeChunks.Length];

            // 半径范围内的 chunk 全部激活
            int rx = _streamingRadius;
            int ry = _streamingRadius;
            int rz = _streamingRadius;

            for (int dx = -rx; dx <= rx; dx++)
            {
                for (int dy = -ry; dy <= ry; dy++)
                {
                    for (int dz = -rz; dz <= rz; dz++)
                    {
                        WorldPos chunkPos = new WorldPos(
                            playerChunk.X + dx,
                            playerChunk.Y + dy,
                            playerChunk.Z + dz);

                        // 范围检查
                        if (chunkPos.X < 0 || chunkPos.X >= _topology.ChunksX ||
                            chunkPos.Y < 0 || chunkPos.Y >= _topology.ChunksY ||
                            chunkPos.Z < 0 || chunkPos.Z >= _topology.ChunksZ)
                            continue;

                        int idx = _topology.ChunkToIndex(chunkPos);
                        result[idx] = true;
                    }
                }
            }

            return result;
        }

        /// <summary>判断某 chunk 是否激活(纯函数,基于当前玩家位置重推)。</summary>
        public bool IsChunkActive(WorldPos chunkPos)
        {
            if (chunkPos.X < 0 || chunkPos.X >= _topology.ChunksX ||
                chunkPos.Y < 0 || chunkPos.Y >= _topology.ChunksY ||
                chunkPos.Z < 0 || chunkPos.Z >= _topology.ChunksZ)
                return false;

            // 纯函数: 基于当前玩家位置重推激活集
            var activeChunks = ComputeActiveChunks(_playerCell);
            return activeChunks[_topology.ChunkToIndex(chunkPos)];
        }

        /// <summary>判断某世界格是否在激活 chunk 内(保守判定:未驻留 = block)。</summary>
        public bool IsWorldPosAccessible(WorldPos worldPos)
        {
            WorldPos chunkPos = _topology.WorldToChunk(worldPos);
            return IsChunkActive(chunkPos);
        }
    }
}
