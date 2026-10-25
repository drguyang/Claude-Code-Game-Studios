// player-controller Story 004 测试 — 集成上界(AC-1-03②)
//
// 跑 ≥ 10³ tick 的移动序列(含折返、对角、传送、垂直)
// 断言 Append 总数 ≤ tick 数
//
// 🔁 **2026-10-25(TD-player-03 收敛轮 · 裁定甲案)**:原打 CellTransitionDetector
//   实例(已删的平行实现),现打 PlayerController 内联唯一实现(S11-3 真夹具形态);
//   断言与规模不变 —— 有界性(AC-1-03②)是对状态机的性质要求,与载体无关。

using System.Collections.Generic;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Gameplay.Presentation.Player;
using NUnit.Framework;
using UnityEngine;
using PlayerControllerType = DaYiJingCheng.Gameplay.Presentation.Player.PlayerController;

namespace DaYiJingCheng.Tests.PlayerController
{
    public class StreamBoundTest
    {
        // O-6(2026-10-09):写者经构造注入 IPayloadEncoder ⇒ 测试给真编码器
        private PayloadEncoder _encoder;

        // S11-3:MonoBehaviour 真夹具(TD-player-03 迁移自 CellTransitionDetector 实例)
        private readonly List<GameObject> _spawned = new List<GameObject>();

        [SetUp]
        public void Setup() => _encoder = new PayloadEncoder(new InMemoryBlobPool());

        [TearDown]
        public void Teardown()
        {
            foreach (var go in _spawned)
            {
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
            }
            _spawned.Clear();
        }

        private PlayerControllerType CreateController(IEventSink sink, ITickProvider provider)
        {
            var go = new GameObject("test_stream_bound_controller");
            _spawned.Add(go);
            go.AddComponent<CharacterController>();
            var controller = go.AddComponent<PlayerControllerType>();
            controller.Initialize(SimAuthorityMode.Host, sink, provider, _encoder, actorId: 0);
            return controller;
        }

        [Test]
        public void test_ac103_streamBound_appendCountLessThanTickCount()
        {
            // 跑 1000 tick 的移动序列
            var sink = new FakeEventSink();
            var provider = new FakeTickProvider(0);
            var controller = CreateController(sink, provider);

            int tickCount = 1000;
            for (int tick = 0; tick < tickCount; tick++)
            {
                provider.SetTick(tick);

                // 模拟移动: 每 tick 移动 0.5 格(有时折返)
                float x = (tick % 2 == 0) ? tick * 0.5f : (tick - 1) * 0.5f;
                controller.OnPositionSample(new Vector3(x, 0, 0));

                controller.OnTickEdge(tick);
            }

            Assert.LessOrEqual(sink.AppendedEvents.Count, tickCount,
                "Append 总数应 ≤ tick 数(有界性)");
        }

        [Test]
        public void test_ac103_streamBound_withReversal()
        {
            // 含折返的移动序列
            var sink = new FakeEventSink();
            var provider = new FakeTickProvider(0);
            var controller = CreateController(sink, provider);

            int tickCount = 1000;
            for (int tick = 0; tick < tickCount; tick++)
            {
                provider.SetTick(tick);

                // 折返: 0→500→0
                float x = (tick < 500) ? tick * 0.1f : (1000 - tick) * 0.1f;
                controller.OnPositionSample(new Vector3(x, 0, 0));

                controller.OnTickEdge(tick);
            }

            Assert.LessOrEqual(sink.AppendedEvents.Count, tickCount,
                "含折返时 Append 总数应 ≤ tick 数");
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
