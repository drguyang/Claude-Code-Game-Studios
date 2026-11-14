// 事件事实的**增量**计数器(2026-11-14 · 第二段节拍表的读面)。
//
// 权威:`design/levels/prologue-beat-script.md` §第二段(判据形如「`ResourceHarvested` 计数 = 5」
//       「`EmergencyTreatmentApplied` 落流」)
//
// ═══ 为什么不每 tick 扫全流 ═══════════════════════════════════════════════
//   ADR-036 实现轮实测过这个坑:逐例问一次历史会变成「每 tick 全排序整条流」,
//   2000 条时 **1135 ms/tick**。本类只吃**新增**的那一段(`_scanned` 之后),
//   于是每 tick 的成本 = 本 tick 新写入的事件数(通常是 0)。
//
// ═══ 边界 ═════════════════════════════════════════════════════════════════
//   · **只读**:不改流、不写流(承 ADR-019 §三)。
//   · **不持权威**:它可以整份丢掉重建 —— 计数全部来自流本身,重建结果逐位相同。
//   · **流变短 = 读档**:`Sync` 发现 `events.Count < _scanned` ⇒ 清零重扫。
//     ⚠️ 与 `RegionTriggerDriver` 同一口径(tick 变小是读档,不是错误)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>按 `EventKind` 增量计数的只读事实表。</summary>
    public sealed class PrologueFactCounter
    {
        private readonly Dictionary<EventKind, int> _counts = new Dictionary<EventKind, int>();
        private int _scanned;

        /// <summary>已扫描到的事件条数(诊断 / 夹具用)。</summary>
        public int Scanned => _scanned;

        /// <summary>把这批事件里**新增**的部分计入。</summary>
        /// <param name="events">当前流的全量事件(按写入序)。</param>
        /// <exception cref="ArgumentNullException"><paramref name="events"/> 为 null。</exception>
        public void Sync(IReadOnlyList<SimEvent> events)
        {
            if (events == null) throw new ArgumentNullException(nameof(events));

            // 流变短 ⇒ 读档 / 清空。清零重扫,不保留跨档计数。
            if (events.Count < _scanned)
            {
                _counts.Clear();
                _scanned = 0;
            }

            for (int i = _scanned; i < events.Count; i++)
            {
                EventKind kind = events[i].Kind;
                _counts.TryGetValue(kind, out int n);
                _counts[kind] = n + 1;
            }
            _scanned = events.Count;
        }

        /// <summary>某类事件已出现几次(未出现 = 0)。</summary>
        public int Of(EventKind kind) => _counts.TryGetValue(kind, out int n) ? n : 0;

        /// <summary>诊断串:`Kind=次数` 若干。</summary>
        public string Summary
        {
            get
            {
                if (_counts.Count == 0) return "无事实";
                var sb = new System.Text.StringBuilder();
                foreach (KeyValuePair<EventKind, int> kv in _counts)
                {
                    if (sb.Length > 0) sb.Append(' ');
                    sb.Append(kv.Key).Append('=').Append(kv.Value);
                }
                return sb.ToString();
            }
        }
    }
}
