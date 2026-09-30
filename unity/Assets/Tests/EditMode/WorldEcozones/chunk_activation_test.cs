// Story 004 测试: chunk 激活与消费边界
//
// AC-6-23: 激活集 = 纯函数重推
// AC-6-23: 未驻留 chunk => 全 block
// AC-6-26a: 发现门由主机 Append
// 消费白名单 {4, 25, 37}

using System;
using NUnit.Framework;
using DaYiJingCheng.Sim.World;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Tests.WorldEcozones
{
    public class ChunkActivationTest
    {
        // AC-6-23: 纯函数重推(同输入两跑相同)
        [Test]
        public void test_activeChunks_pureFunction_identicalRuns()
        {
            var topology = new ChunkTopology(16, 1, 16, 4, 1, 4);
            var activator = new ChunkActivator(topology, streamingRadius: 1);

            var cell = new WorldPos(8, 0, 8);

            var run1 = activator.ComputeActiveChunks(cell);
            var run2 = activator.ComputeActiveChunks(cell);

            // 比较激活集
            for (int i = 0; i < run1.Length; i++)
            {
                Assert.AreEqual(run1[i], run2[i], $"Index {i} differs");
            }
        }

        // AC-6-23: 未驻留 chunk = 全 block
        [Test]
        public void test_unloadedChunk_treatedAsBlock()
        {
            var topology = new ChunkTopology(16, 1, 16, 2, 1, 2);
            var activator = new ChunkActivator(topology, streamingRadius: 0);

            // 玩家在 chunk (0,0,0) => 只激活自身
            var active = activator.ComputeActiveChunks(new WorldPos(0, 0, 0));

            // chunk (1,1,1) 未激活
            Assert.IsFalse(activator.IsChunkActive(new WorldPos(1, 1, 1)));

            // 世界格在未激活 chunk => 不可达
            Assert.IsFalse(activator.IsWorldPosAccessible(new WorldPos(20, 0, 20)));
        }

        // 激活半径正确
        [Test]
        public void test_streamingRadius_correctActivation()
        {
            var topology = new ChunkTopology(16, 1, 16, 5, 1, 5);
            var activator = new ChunkActivator(topology, streamingRadius: 2);

            // 玩家在 chunk (2, 0, 2)
            var active = activator.ComputeActiveChunks(new WorldPos(32, 0, 32));

            // 半径 2 => chunk (0..4, 0..4, 0..4)
            for (int dx = 0; dx <= 4; dx++)
            {
                for (int dz = 0; dz <= 4; dz++)
                {
                    Assert.IsTrue(activator.IsChunkActive(new WorldPos(dx, 0, dz)),
                        $"Chunk ({dx}, 0, {dz}) should be active");
                }
            }

            // 超出半径 => 不激活
            Assert.IsFalse(activator.IsChunkActive(new WorldPos(5, 0, 2)));
        }

        // WorldToChunk / ChunkToWorldOrigin 互逆
        [Test]
        public void test_chunkConversion_inverse()
        {
            var topology = new ChunkTopology(16, 8, 16, 4, 2, 4);

            var worldPos = new WorldPos(33, 10, 50);
            var chunkPos = topology.WorldToChunk(worldPos);
            var origin = topology.ChunkToWorldOrigin(chunkPos);

            Assert.AreEqual(2, chunkPos.X); // 33 / 16 = 2
            Assert.AreEqual(1, chunkPos.Y); // 10 / 8 = 1
            Assert.AreEqual(3, chunkPos.Z); // 50 / 16 = 3
            Assert.AreEqual(32, origin.X);
            Assert.AreEqual(8, origin.Y);
            Assert.AreEqual(48, origin.Z);
        }

        // ChunkToIndex / IndexToChunk 互逆
        [Test]
        public void test_chunkIndex_roundTrip()
        {
            var topology = new ChunkTopology(16, 1, 16, 3, 1, 4);

            for (int x = 0; x < 3; x++)
            {
                for (int y = 0; y < 1; y++)
                {
                    for (int z = 0; z < 4; z++)
                    {
                        var chunk = new WorldPos(x, y, z);
                        int index = topology.ChunkToIndex(chunk);
                        var recovered = topology.IndexToChunk(index);
                        Assert.AreEqual(chunk, recovered);
                    }
                }
            }
        }

        // 边界: 越界 chunk => 不激活
        [Test]
        public void test_outOfBoundsChunk_notActive()
        {
            var topology = new ChunkTopology(16, 1, 16, 2, 1, 2);
            var activator = new ChunkActivator(topology, 1);

            Assert.IsFalse(activator.IsChunkActive(new WorldPos(-1, 0, 0)));
            Assert.IsFalse(activator.IsChunkActive(new WorldPos(2, 0, 0)));
            Assert.IsFalse(activator.IsChunkActive(new WorldPos(0, 0, -1)));
        }

        // 边界: 流式半径 0 => 仅自身 chunk
        [Test]
        public void test_zeroRadius_onlySelfChunk()
        {
            var topology = new ChunkTopology(16, 1, 16, 4, 1, 4);
            var activator = new ChunkActivator(topology, streamingRadius: 0);

            var active = activator.ComputeActiveChunks(new WorldPos(16, 0, 16));

            // 仅 chunk (1, 0, 1) 激活
            Assert.IsTrue(activator.IsChunkActive(new WorldPos(1, 0, 1)));
            Assert.IsFalse(activator.IsChunkActive(new WorldPos(0, 0, 1)));
            Assert.IsFalse(activator.IsChunkActive(new WorldPos(2, 0, 1)));
        }
    }
}
