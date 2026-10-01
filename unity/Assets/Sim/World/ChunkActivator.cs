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

        /// <summary>判断某 chunk 是否激活。</summary>
        public bool IsChunkActive(WorldPos chunkPos)
        {
            if (chunkPos.X < 0 || chunkPos.X >= _topology.ChunksX ||
                chunkPos.Y < 0 || chunkPos.Y >= _topology.ChunksY ||
                chunkPos.Z < 0 || chunkPos.Z >= _topology.ChunksZ)
                return false;

            return _activeChunks[_topology.ChunkToIndex(chunkPos)];
        }

        /// <summary>判断某世界格是否在激活 chunk 内(保守判定:未驻留 = block)。</summary>
        public bool IsWorldPosAccessible(WorldPos worldPos)
        {
            WorldPos chunkPos = _topology.WorldToChunk(worldPos);
            return IsChunkActive(chunkPos);
        }
    }
}
