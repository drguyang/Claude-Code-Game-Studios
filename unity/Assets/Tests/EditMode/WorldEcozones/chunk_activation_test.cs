// Story 004 测试: chunk 激活与消费边界
//
// AC-6-23: 激活集 = 纯函数重推
// AC-6-23: 未驻留 chunk => 全 block
// AC-6-26a: 发现门由主机 Append
// 消费白名单 {4, 25, 37}

using System;
using NUnit.Framework;
using DaYiJingCheng.Sim.Codec;
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

        // ══════════════════════════════════════════════════════════════
        // AC-6-23 [B] —— **发现门集成**(2026-10-03 补,闭合评审 B2)
        //
        // ⚠️ 原 AC **已勾**并自陈「`TryDiscover` 已实现」,但本文件 7 例
        //    **全为 chunk 拓扑纯函数测试**,`TryDiscover` / `ActorCellEntered` /
        //    `PoiStateMachine` 的引用数 = **0** ⇒ **[B] AC 无集成取证**。
        // ══════════════════════════════════════════════════════════════

        [Test]
        public void test_ac623_discoverGate_drivenByRealCellEntry()
        {
            // ⚠️ **2026-10-03 重写(第二轮评审 N2-1)**:
            //    初版的「同 tick」断言**恒真** —— `Tick` 是测试**直传入参**,
            //    激活与门之间**无因果**;且全文 `ActorCellEntered` 只出现在注释里,
            //    **零真实跨格事件** ⇒ [B] AC-6-23 的核心判据从未真验。
            //    现改为**真驱动链**:`CellTransitionDetector`(表现层跨格检测器,已在库)
            //    喂位置样本 → 产**真 `ActorCellEntered`** → 测试从该事件的格驱动激活与门,
            //    且**tick 取自事件本身**(而非测试硬编码)⇒ 因果真实。

            var topology = new ChunkTopology(16, 1, 16, 4, 1, 4);
            var activator = new ChunkActivator(topology, streamingRadius: 1);
            var sink = new SpyEventSink();

            // ① **真驱动**:跨格检测器按位置样本产出 ActorCellEntered
            const long Tick = 100;
            var tickProvider = new FixedTickProvider(Tick);
            // O-6(2026-10-09):写者经构造注入 IPayloadEncoder ⇒ 真编码器 + 真池,
            // 下方格还原走「池 + codec」的合法读路径(原伪 ref 形态已失效)。
            var pool = new InMemoryBlobPool();
            var detector = new DaYiJingCheng.Gameplay.Presentation.Player.CellTransitionDetector(
                sink, tickProvider, new PayloadEncoder(pool), actorId: 0);

            detector.OnPositionSample(new UnityEngine.Vector3(20.5f, 0f, 20.5f));  // → 格 (20,0,20)
            detector.OnTickEdge();                                                  // tick 边沿提交

            // ② 断言**真事件**已产生(而非零跨格事件)
            var cellEntry = sink.AppendedEvents.Find(e => e.Kind == EventKind.ActorCellEntered);
            Assert.IsNotNull(cellEntry,
                "跨格检测器须产出**真 `ActorCellEntered`** —— 这是本测的驱动源(N2-1)");
            Assert.AreEqual(Tick, cellEntry.Tick, "事件的 tick 须来自 tick provider(非测试硬编码)");

            // ③ 从**事件的格**驱动激活判定(因果链:事件 → 格 → 激活)
            var enteredCell = CellFromActorCellEntered(cellEntry, pool);
            var chunk = topology.WorldToChunk(enteredCell);
            Assert.IsTrue(activator.IsChunkActive(chunk),
                $"玩家进入格 {enteredCell.X},{enteredCell.Z} 所在 chunk 须被激活(激活权 = 6)");

            // ④ 发现门在**同一 tick** 求值(tick 取自事件,非直传)
            var machine = new PoiStateMachine(sink, new AlwaysHostAuthority(),
                                            new[] { 1 }, new NoopEncoder());
            var result = machine.TryDiscover(1, cellEntry.Tick);

            Assert.AreEqual(PoiStateTransferResult.Success, result, "发现门须成功");
            Assert.AreEqual(PoiState.Discovered, machine.GetState(1));

            var poiEvent = sink.AppendedEvents.Find(e => e.Kind == EventKind.PoiStateChanged);
            Assert.IsNotNull(poiEvent, "须发 PoiStateChanged");
            Assert.AreEqual(cellEntry.Tick, poiEvent.Tick,
                "**同 tick 求值**:PoiStateChanged.tick 须 == 驱动它的 ActorCellEntered.tick" +
                "(因果真实 —— 前者由后者的 tick 驱动,非测试各传一个常量)");
        }

        /// <summary>
        /// 从 `ActorCellEntered` 事件还原格(经池 + codec 的合法读路径)。
        /// ⚠️ O-6(2026-10-09)订正:原实现读 `PayloadRef` 三整数字段 —— 那是手搓伪引用的
        /// 形态(格坐标塞引用三字段,零字节进池)。写者改走 `IPayloadEncoder` 后载荷真进池,
        /// 此处必须解码;解码失败 = 字节没进池 = 写者退回手搓,本断言即红。
        /// </summary>
        private static WorldPos CellFromActorCellEntered(SimEvent e, IBlobPool pool)
        {
            Assert.IsTrue(PayloadCodec.TryGetPayload(e, pool, out ActorCellEnteredPayload p),
                "ActorCellEntered 载荷须能经池 + codec 取回(字节真的进了池 —— O-6)");
            return p.Cell;
        }

        /// <summary>固定 tick 的 provider —— 使「同 tick」可被真实驱动。</summary>
        private sealed class FixedTickProvider : ITickProvider
        {
            private readonly long _tick;
            public FixedTickProvider(long tick) { _tick = tick; }
            public long CurrentTick => _tick;
        }

        [Test]
        public void test_ac623_discoverGate_idempotentOnSecondEntry()
        {
            var topology = new ChunkTopology(16, 1, 16, 4, 1, 4);
            var sink = new SpyEventSink();
            var machine = new PoiStateMachine(sink, new AlwaysHostAuthority(),
                                            new[] { 1 }, new NoopEncoder());

            machine.TryDiscover(1, tick: 100);
            var second = machine.TryDiscover(1, tick: 101);

            Assert.AreEqual(PoiStateTransferResult.AlreadyAtState, second,
                "二次进入 ⇒ AlreadyAtState(发现门幂等)");
            Assert.AreEqual(1, sink.AppendedEvents.Count, "不得重复发事件");
        }

        [Test]
        public void test_ac623_discoverGate_blockedOnUnloadedChunk()
        {
            var topology = new ChunkTopology(16, 1, 16, 4, 1, 4);
            var activator = new ChunkActivator(topology, streamingRadius: 1);

            var farCell = new WorldPos(60, 0, 60);
            var farChunk = topology.WorldToChunk(farCell);
            Assert.IsFalse(activator.IsChunkActive(farChunk),
                "未驻留 chunk 须为**未激活**(保守:不假设全图可达)");
            Assert.IsFalse(activator.IsWorldPosAccessible(farCell),
                "未激活 chunk 内的世界格 ⇒ 不可达(AC-6-23 保守侧)");
        }

        // ── 本文件专用测试辅助 ──────────────────────────────────────
        private sealed class SpyEventSink : IEventSink
        {
            public readonly System.Collections.Generic.List<SimEvent> AppendedEvents =
                new System.Collections.Generic.List<SimEvent>();
            public void Append(in SimEvent e) => AppendedEvents.Add(e);
        }

        private sealed class AlwaysHostAuthority : IEventAuthority
        {
            public bool IsAuthority => true;
            public bool IsHost => true;
            public EventRollResult Roll(in RollRequest r) => new EventRollResult(0, 0, 0, 0);
        }

        /// <summary>本文件不测编码路径,只需一个可用的 encoder 占位。</summary>
        private sealed class NoopEncoder : IPayloadEncoder
        {
            public PayloadRef Encode<T>(EventKind kind, in T payload) where T : struct
                => new PayloadRef(0, 0, 0);
        }
    }
}
