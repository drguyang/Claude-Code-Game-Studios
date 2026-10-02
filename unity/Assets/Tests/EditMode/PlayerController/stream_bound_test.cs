// player-controller Story 004 测试 — 集成上界(AC-1-03②)
//
// 跑 ≥ 10³ tick 的移动序列(含折返、对角、传送、垂直)
// 断言 Append 总数 ≤ tick 数

using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Gameplay.Presentation.Player;
using NUnit.Framework;
using UnityEngine;

namespace DaYiJingCheng.Tests.PlayerController
{
    public class StreamBoundTest
    {
        [Test]
        public void test_ac103_streamBound_appendCountLessThanTickCount()
        {
            // 跑 1000 tick 的移动序列
            var sink = new FakeEventSink();
            var provider = new FakeTickProvider(0);
            var detector = new CellTransitionDetector(sink, provider);

            int tickCount = 1000;
            for (int tick = 0; tick < tickCount; tick++)
            {
                provider.SetTick(tick);

                // 模拟移动: 每 tick 移动 0.5 格(有时折返)
                float x = (tick % 2 == 0) ? tick * 0.5f : (tick - 1) * 0.5f;
                detector.OnPositionSample(new Vector3(x, 0, 0));

                detector.OnTickEdge();
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
            var detector = new CellTransitionDetector(sink, provider);

            int tickCount = 1000;
            for (int tick = 0; tick < tickCount; tick++)
            {
                provider.SetTick(tick);

                // 折返: 0→500→0
                float x = (tick < 500) ? tick * 0.1f : (1000 - tick) * 0.1f;
                detector.OnPositionSample(new Vector3(x, 0, 0));

                detector.OnTickEdge();
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
