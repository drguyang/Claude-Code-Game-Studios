// 权威来源:production/epics/telemetry-analytics/story-006-f5-difficulty-curve.md
//   (AC-51-B12…B14 — F5 难度曲线:滑窗密度 Dens_s(t_k) + 半开窗不双计 + 三流分列 + 参数退化硬失败)
//   · Dens_s(t_k) = |{e ∈ E : Kind(e) ∈ s ∧ t_k ≤ Tick(e) < t_k + W}|,t_k = t_0 + k·S
//   · 窗口半开 [t_k, t_k + W) —— 边界只取一次,防同 Tick 事件双计
//   · 三流各一条序列,不合并为单一标量(语义不同)
//   · W < 1 ⇒ 退化,构建期/加载期硬失败(非运行期退化、非静默取默认值)
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
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Telemetry
{
    /// <summary>F5 难度曲线指标 —— 纯函数重算面(ADR-019 §一「回放即数据记录」的落地)。
    /// <para>语义 = GDD Formulas F5:滑窗密度 + 半开窗不双计 + 三流分列 + 覆盖记账。</para>
    /// <para>**零埋点**:输入由调用方从既有事件流还原;本类不做任何 IO、不订阅、不写。</para></summary>
    /// <remarks>
    /// ⚠️ 上移台账(TD-epic-01):本类逐字搬自
    /// <c>Tests/EditMode/Telemetry/f5_difficulty_curve_test.cs</c> 的测试自持私有类
    /// <c>F5Formula</c> —— **上移 = 逐字搬移,零公式变更**(含常量、哨兵、覆盖记账循环与
    /// 退化硬失败口径)。测试自持的那支 <c>StreamId</c> 是 <c>Sim.Contracts.StreamId</c>
    /// 的重复声明;本类直接复用**既有** <see cref="DaYiJingCheng.Sim.Contracts.StreamId"/>
    /// (ADR-008 §Key Interfaces / ADR-009 §三,成员序 = 流优先级序),故不再自带副本 ——
    /// 这是**去重复**,不是改判据:两处成员名与语义逐字一致(Case / History / World)。
    /// </remarks>
    public static class DifficultyCurveMetrics
    {
        /// <summary>一条事件(由调用方从**事件流**还原后喂入)。</summary>
        public sealed class SimEvent
        {
            public StreamId Stream;
            public long Tick;
        }

        /// <summary>计算某流在窗口 t_k 的密度(半开 [t_k, t_k + W))。</summary>
        public static int Density(IReadOnlyList<SimEvent> events, StreamId stream,
                                   long t_k, int window)
        {
            if (window < 1) throw new ArgumentException("W < 1 ⇒ 退化,硬失败");
            return events.Count(e => e.Stream == stream &&
                                     e.Tick >= t_k && e.Tick < t_k + window);
        }

        /// <summary>计算所有窗口密度序列 + 覆盖记账。
        /// 返回 (densities, coveredTicks, spanTicks)。</summary>
        public static (List<int> densities, int coveredTicks, int spanTicks)
            ComputeSeries(IReadOnlyList<SimEvent> events, StreamId stream,
                          long t0, int step, int window, long tickMax)
        {
            if (window < 1) throw new ArgumentException("W < 1 ⇒ 退化,硬失败");
            if (step < 1) throw new ArgumentException("S < 1 ⇒ 退化,硬失败");

            var densities = new List<int>();
            var covered = new HashSet<long>();
            long k = 0;
            for (long t_k = t0; t_k <= tickMax; t_k = t0 + (++k) * step)
            {
                int d = Density(events, stream, t_k, window);
                densities.Add(d);
                for (long t = t_k; t < t_k + window; t++) covered.Add(t);
                if (t_k + window > tickMax && t_k >= tickMax) break;
            }
            int span = (int)(tickMax - t0);
            return (densities, covered.Count, Math.Max(0, span));
        }
    }
}
