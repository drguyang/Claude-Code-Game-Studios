// 奇遇驱动(ADR-045 §D4)—— 每 tick 只读、增量游标,判「够不够」并交给唯一写者。
//
// 权威:`docs/architecture/adr-045-quest-stream.md` §D4 / §D5
//      `design/levels/prologue-beat-script.md` §3.1
//
// ═══ 判据的「在哪采集」用玩家格,不用资源点格 ═══════════════════════════════
//   `ResourceHarvestedPayload` **不含格**;而采集是持续交互(玩家必须站在点旁 1 s),
//   ⇒ 事件发生时玩家所在格就是「在哪采的」。用它既不需要 nodeId → cell 的第二张映射表,
//   也与「区域 = 那个废墟」的叙事一致。
//   ⚠️ 代价:若玩家在同一 tick 内先采后瞬移,归类会落在新格。P0 无瞬移路径(除读档重建)。
//
// ═══ 增量游标,禁止每 tick 全流扫描 ═══════════════════════════════════════
//   ADR-036 实测:逐例全扫整条流在 2000 条时 **1135 ms/tick**。本类只吃 `_scanned` 之后那一段。
//   tick 变小 = 读档 ⇒ 计数清零重扫(与 `PrologueFactCounter` / `RegionTriggerDriver` 同口径)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>序章奇遇的判定驱动(纯读;写入全在 <see cref="AdventureWriter"/>)。</summary>
    public sealed class AdventureDriver
    {
        private readonly Func<IReadOnlyList<SimEvent>> _events;
        private readonly IReadOnlyList<AdventureStep> _steps;
        private readonly AdventureWriter _writer;
        private readonly Func<WorldPos> _playerCell;
        private readonly int _actorId;

        private readonly int[] _counts;
        private readonly bool[] _accepted;
        private int _scanned;
        private long _lastTick = long.MinValue;

        /// <param name="events">事件流只读视图。</param>
        /// <param name="steps">奇遇步骤表(P0 = <see cref="AdventureSteps.PrologueFive"/>)。</param>
        /// <param name="writer">唯一写者。</param>
        /// <param name="playerCell">玩家**经流确立**的当前格。</param>
        /// <param name="actorId">本机玩家 id。</param>
        /// <exception cref="ArgumentNullException">任一依赖为 null。</exception>
        public AdventureDriver(Func<IReadOnlyList<SimEvent>> events, IReadOnlyList<AdventureStep> steps,
                               AdventureWriter writer, Func<WorldPos> playerCell, int actorId)
        {
            _events = events ?? throw new ArgumentNullException(nameof(events));
            _steps = steps ?? throw new ArgumentNullException(nameof(steps));
            _writer = writer ?? throw new ArgumentNullException(nameof(writer));
            _playerCell = playerCell ?? throw new ArgumentNullException(nameof(playerCell));
            if (actorId < 0) throw new ArgumentOutOfRangeException(nameof(actorId), actorId, "玩家 id 不得为负");
            _actorId = actorId;
            _counts = new int[steps.Count];
            _accepted = new bool[steps.Count];
        }

        /// <summary>某条奇遇的当前计数(诊断 / 夹具观测面)。</summary>
        public int CountOf(int adventureId) => _counts[adventureId];

        /// <summary>每 tick 调一次(tick 边沿)。</summary>
        /// <param name="tick">当前逻辑 tick。</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="tick"/> 为负。</exception>
        public void OnTickEdge(long tick)
        {
            if (tick < 0) throw new ArgumentOutOfRangeException(nameof(tick), tick, "tick 不得为负");
            if (tick == _lastTick) return;

            // 读档 ⇒ 事件流可能变短 / tick 回退:计数清零重扫(不做「时间倒流即抛」——
            // 这里没有权威态可守,与 RegionTriggerDriver / PrologueFactCounter 同口径)。
            if (tick < _lastTick)
            {
                Array.Clear(_counts, 0, _counts.Length);
                Array.Clear(_accepted, 0, _accepted.Length);
                _scanned = 0;
            }
            _lastTick = tick;

            IReadOnlyList<SimEvent> events = _events();
            if (events == null) return;

            if (events.Count < _scanned)
            {
                Array.Clear(_counts, 0, _counts.Length);
                Array.Clear(_accepted, 0, _accepted.Length);
                _scanned = 0;
            }

            for (int i = _scanned; i < events.Count; i++)
            {
                SimEvent e = events[i];
                if (e.Kind != EventKind.ResourceHarvested) continue;
                // 只认本机玩家的采集 —— 事件头无 actor,但 P0 单机(ADR-016 / 20 的归属面)。
                AttributeOneHarvest(_playerCell(), tick);
            }
            _scanned = events.Count;
        }

        /// <summary>把一次采集归到所有命中的采集型奇遇(同一次采集可以同时计入多条)。</summary>
        private void AttributeOneHarvest(WorldPos cell, long tick)
        {
            for (int s = 0; s < _steps.Count; s++)
            {
                AdventureStep step = _steps[s];
                if (step.Condition != AdventureCondition.GatherInRect) continue;
                if (_counts[s] >= step.Required) continue;         // 已完成,不再计数
                if (!step.Contains(cell.X, cell.Z)) continue;

                _counts[s]++;
                if (!_accepted[s])
                {
                    // 首次命中 ⇒ 写「接取」(ADR-045 §D2:接取 = 进入计数范围)
                    _accepted[s] = _writer.Accept(step.Id, _actorId, tick) || _accepted[s];
                }
                if (_counts[s] >= step.Required)
                    _writer.Complete(in step, _actorId, cell, tick);   // 去重在写者内
            }
        }
    }
}
