// 权威来源:production/epics/telemetry-analytics/story-007-f6-f7-session-rhythm.md
//   (AC-51-B15…B18 — F6 局内时长 + F7 节律:SessionSpan 可审计 + 上中位数 + MAD + 退化处理)
//   · F6: SessionSpan = Tick_max − Tick_min;同时报 Tick_min/Tick_max/|E|;单事件 ⇒ span=0;空流 ⇒ 0
//   · F7: Δ_i = Tick(e_{i+1}) − Tick(e_i);Δ_median = Δ_(⌈n/2⌉)(1-indexed,上中位数,禁插值);
//          Δ_MAD = median(|Δ_i − Δ_median|);GapCount = |{i : Δ_i > GAP_THRESHOLD}|
//   · 退化:n < 2 ⇒ 无 Δ 序列 ⇒ 全部字段报「不可定义」;n=1 时 Δ_MAD 不是 0,是「无意义」;
//          n=2 ⇒ Δ_MAD = 0(合法)
//   · 禁浮点:无方差/标准差/Math.Sqrt/浮点除法;节律字段类型全为 Tick/int
// ADR-019 §一(回放即数据记录)· ADR-006 §五(整数 (num, den) 对)
//
// ═══════════════════════════════════════════════════════════════════════════════
// ⚠️ **本程序集是 TD-epic-01 的还债件**(2026-10-19):普查实测 51 遥测在 `unity/Assets/` 下
//    零生产文件,而 10 个 story 全标 `Complete` —— AC 语义层由**测试自持谓词面**顶替
//    (测试文件自陈「测试自持谓词面,零生产依赖」)。本件把谓词面上移为生产类。
//
// ⚠️ **门槛:与 `Sim.Contracts` 同款 `noEngineReferences: true`**(ADR-019 ③ 51 住边界层、不进 sim)
//    —— 读事件流是纯 C# 计算,不碰 `UnityEngine`。51 是**只读消费者**(ADR-005 三流的读者),
//    不是第四个 `IEventSink` 写入者(ADR-019 §三 逐字)。
// ⚠️ 因此本程序集**不得**出现:网络调用 / `UnityEngine.*` / 写文件(导出归调用方,见上)。
// ═══════════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Linq;

namespace DaYiJingCheng.Telemetry
{
    /// <summary>F6 局内时长 + F7 会话节律指标 —— 纯函数重算面
    /// (ADR-019 §一「回放即数据记录」的落地)。
    /// <para>语义 = GDD Formulas F6/F7:SessionSpan 可审计 + 上中位数 + MAD + 退化处理。</para>
    /// <para>**零埋点**:输入由调用方从既有事件流还原;本类不做任何 IO、不订阅、不写。</para></summary>
    /// <remarks>
    /// ⚠️ 上移台账(TD-epic-01):本类逐字搬自
    /// <c>Tests/EditMode/Telemetry/f6_f7_session_rhythm_test.cs</c> 的测试自持私有类
    /// <c>F6F7Formula</c> —— **上移 = 逐字搬移,零公式变更**(含常量、哨兵、上中位数下标
    /// 公式与「n<2 ⇒ 不可定义 ≠ 0」口径)。
    /// </remarks>
    public static class SessionRhythmMetrics
    {
        /// <summary>一条事件(由调用方从**事件流**还原后喂入)。</summary>
        public sealed class SimEvent
        {
            public long Tick;
        }

        /// <summary>F6:计算 SessionSpan = Tick_max − Tick_min。
        /// 返回 (tickMin, tickMax, sessionSpan, eventCount)。
        /// 空流 ⇒ (0, 0, 0, 0);单事件 ⇒ span = 0(合法)。</summary>
        public static (long tickMin, long tickMax, long sessionSpan, int eventCount)
            ComputeSessionSpan(IReadOnlyList<SimEvent> events)
        {
            if (events.Count == 0) return (0, 0, 0, 0);
            long min = events.Min(e => e.Tick);
            long max = events.Max(e => e.Tick);
            return (min, max, max - min, events.Count);
        }

        /// <summary>F7:计算节律指标(上中位数 + MAD + Δ_min + Δ_max + GapCount)。
        /// 返回 (deltaMedian, deltaMad, deltaMin, deltaMax, gapCount, defined)。
        /// n &lt; 2 ⇒ 无 Δ 序列 ⇒ (0, 0, 0, 0, 0, false)——「不可定义」≠ 0。</summary>
        public static (int deltaMedian, int deltaMad, int deltaMin, int deltaMax, int gapCount, bool defined)
            ComputeRhythm(IReadOnlyList<SimEvent> events, int gapThreshold)
        {
            int n = events.Count;
            if (n < 2) return (0, 0, 0, 0, 0, false);   // 退化:无 Δ 序列

            // Δ_i = Tick(e_{i+1}) − Tick(e_i),i = 0…n−2
            var deltas = new List<int>();
            for (int i = 0; i < n - 1; i++)
                deltas.Add((int)(events[i + 1].Tick - events[i].Tick));

            // 上中位数:m = n-1 个 Δ,排序后第 ⌈m/2⌉ 项(1-indexed),禁插值
            var sorted = deltas.OrderBy(d => d).ToList();
            int median = sorted[(n - 1 + 1) / 2 - 1];   // ⌈(n-1)/2⌉ 1-indexed → 0-indexed

            // Δ_MAD = median(|Δ_i − Δ_median|)
            var absDeviations = deltas.Select(d => Math.Abs(d - median)).OrderBy(d => d).ToList();
            int mad = absDeviations[(n - 1 + 1) / 2 - 1];

            // Δ_min / Δ_max
            int deltaMin = sorted.First();
            int deltaMax = sorted.Last();

            // GapCount = |{i : Δ_i > GAP_THRESHOLD}|
            int gapCount = deltas.Count(d => d > gapThreshold);

            return (median, mad, deltaMin, deltaMax, gapCount, true);
        }
    }
}
