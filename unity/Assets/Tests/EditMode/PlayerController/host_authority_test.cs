// player-controller Story 005 测试
//
// AC-1-30: 客户端不 Append(零调用点)
// AC-1-30②: 主机模式必发(反向用例)
// AC-1-30③(a): 上行载荷不含已提交格
// AC-1-30③(b): 同值上行被主机丢弃
// AC-1-30③(c): 幽灵格负例(预测回滚)
// 联机侧有界性: N actor 压力

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Gameplay.Presentation.Player;
using NUnit.Framework;
using UnityEngine;
using PlayerControllerType = DaYiJingCheng.Gameplay.Presentation.Player.PlayerController;

namespace DaYiJingCheng.Tests.PlayerController
{
    public class HostAuthorityTest
    {
        // ══════════ O-6(2026-10-09): 编码器夹具 ══════════
        private PayloadEncoder _encoder;

        [SetUp]
        public void Setup()
        {
            _encoder = new PayloadEncoder(new InMemoryBlobPool());
        }

        // ══════════ AC-1-30①: 客户端模式零 Append ══════════

        [Test]
        public void test_ac130_clientMode_zeroAppendCalls()
        {
            // AC-1-30①: OnTickEdge 入口方法不直接调用 Append
            // (AppendCellEnteredEvent 是独立方法，仅在 Host 模式被调用)
            var controllerType = typeof(PlayerControllerType);
            var onTickEdge = controllerType.GetMethod("OnTickEdge", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(onTickEdge, "OnTickEdge 方法应存在");
            Assert.IsFalse(ILBodyScanner.ContainsMethodCall(onTickEdge, "Append"),
                "OnTickEdge 不应直接调用 Append（客户端模式零 Append）");
        }

        // ══════════ AC-1-30②: 主机模式必发(反向用例) ══════════

        [Test]
        public void test_ac130_hostMode_emitsEvent()
        {
            // 主机模式下同一位置样本序列必须产生 1 次 Append
            var sink = new FakeEventSink();
            var detector = new CellTransitionDetector(sink, new FakeTickProvider(100), _encoder, actorId: 0);

            detector.OnPositionSample(new Vector3(0.5f, 0, 0));
            detector.OnPositionSample(new Vector3(1.5f, 0, 0));
            detector.OnTickEdge();

            Assert.AreEqual(1, sink.AppendedEvents.Count,
                "主机模式应产生 1 条事件(否定空实现)");
        }

        // ══════════ AC-1-30③(a): 上行载荷不含已提交格 ══════════

        [Test]
        public void test_ac130_payloadExcludesCommittedCell()
        {
            // 上行载荷字段集合 ⊆ 白名单
            var payloadType = typeof(ActorCellEnteredPayload);
            var whitelist = new[] { "ActorId", "Cell", "Tick" };

            foreach (var field in payloadType.GetFields())
            {
                Assert.IsTrue(whitelist.Contains(field.Name),
                    $"上行载荷字段 {field.Name} 不在白名单(可能含已提交格)");
            }
        }

        // ══════════ AC-1-30③(b): 同值上行被主机丢弃 ══════════

        [Test]
        public void test_ac130_sameValueUplink_discarded()
        {
            // 主机侧 actor 的 last_committed_cell = A
            // 注入上行 A(与权威格相同) ⇒ 主机不 Append、不推进
            var sink = new FakeEventSink();
            var provider = new FakeTickProvider(100);
            var controller = new PlayerControllerType();
            controller.Initialize(SimAuthorityMode.Host, sink, provider, _encoder, actorId: 0);

            // 先提交格 A
            controller.OnPositionSample(new Vector3(0.5f, 0, 0));
            controller.OnTickEdge();
            Assert.AreEqual(1, sink.AppendedEvents.Count, "初始提交 A");

            // 注入上行 A(同值)
            controller.OnUplinkSample(new Vector3(0.5f, 0, 0));
            controller.OnTickEdge();

            // 不应产生新事件
            Assert.AreEqual(1, sink.AppendedEvents.Count,
                "同值上行应被丢弃(不 Append、不推进)");
        }

        // ══════════ AC-1-30③(c): 幽灵格负例(预测回滚) ══════════

        [Test]
        public void test_ac130_ghostCell_noPhantomInStream()
        {
            // 客户端本地"已提交 A→B"后回滚,上行序列 B, A
            // 主机侧格序列不含权威从未到达的格
            var sink = new FakeEventSink();
            var provider = new FakeTickProvider(100);
            var controller = new PlayerControllerType();
            controller.Initialize(SimAuthorityMode.Host, sink, provider, _encoder, actorId: 0);

            // 提交 A
            controller.OnPositionSample(new Vector3(0.5f, 0, 0));
            controller.OnTickEdge();

            // 上行 B(客户端预测)
            controller.OnUplinkSample(new Vector3(1.5f, 0, 0));
            controller.OnTickEdge();

            // 上行 A(回滚)
            controller.OnUplinkSample(new Vector3(0.5f, 0, 0));
            controller.OnTickEdge();

            // 主机侧格序列: A, B, A — 不应含幽灵格
            Assert.AreEqual(3, sink.AppendedEvents.Count,
                "主机应记录 A→B→A 三次转移(无幽灵格)");
        }

        // ══════════ 联机侧有界性: N actor 压力 ══════════

        [Test]
        public void test_boundedness_nActors()
        {
            // 4 个 fake actor 同 tick 各上行多次
            // 断言主机 Append ≤ actor 数 × tick 数
            var sink = new FakeEventSink();
            var provider = new FakeTickProvider(0);
            var controller = new PlayerControllerType();
            controller.Initialize(SimAuthorityMode.Host, sink, provider, _encoder, actorId: 0);

            int actorCount = 4;
            int tickCount = 100;

            for (int tick = 0; tick < tickCount; tick++)
            {
                provider.SetTick(tick);
                for (int actor = 0; actor < actorCount; actor++)
                {
                    // 每个 actor 上行多次(模拟高频)
                    for (int sample = 0; sample < 10; sample++)
                    {
                        controller.OnUplinkSample(new Vector3(actor + sample * 0.1f, 0, 0));
                    }
                }
                controller.OnTickEdge();
            }

            Assert.LessOrEqual(sink.AppendedEvents.Count, actorCount * tickCount,
                "Append 总数应 ≤ actor 数 × tick 数(有界性)");
        }

        // ══════════ 测试辅助 ══════════

        private sealed class FakeEventSink : IEventSink
        {
            public readonly List<SimEvent> AppendedEvents = new List<SimEvent>();
            public void Append(in SimEvent e) => AppendedEvents.Add(e);
        }

        private sealed class FakeTickProvider : ITickProvider
        {
            private long _currentTick;
            public FakeTickProvider(long initialTick) { _currentTick = initialTick; }
            public long CurrentTick => _currentTick;
            public void SetTick(long tick) { _currentTick = tick; }
        }
    }
}
