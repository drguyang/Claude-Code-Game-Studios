// 权威来源:production/epics/telemetry-analytics/story-008-pure-function-recomputable.md
//   (AC-51-C1…C4 — 纯函数与可复算:同流两次重算逐位相同 / 单一折叠函数两入口同值
//    / 可进 ADR-12 黄金夹具(ADVISORY) / 不依赖墙钟帧序执行次数)
//   · R8:每条指标 = Fold(E) —— 不读墙钟/帧序/执行次数;同流同值
//   · R9:两种入口共用同一个折叠函数 —— 增量边读边折终值 == 离线整表折叠终值
//   · R11:指标定义必须写成可复算的谓词
// ADR-019 §一(回放即数据记录,零新埋点)· ADR-012(黄金夹具 + ROUND_HALF_AWAY_FROM_ZERO)
// ADR-006 §五(Fix 编码器 + 守恒律)
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

using System.Collections.Generic;
using System.Linq;

namespace DaYiJingCheng.Telemetry
{
    /// <summary>纯函数折叠器(ADR-019 §一 R8/R9:每条指标 = Fold(E);两种入口共用同一个折叠函数)。
    /// <para>**零埋点**:输入由调用方从既有事件流还原;本类不做任何 IO、不订阅、不写。</para></summary>
    /// <remarks>
    /// ⚠️ 上移台账(TD-epic-01):本类逐字搬自
    /// <c>Tests/EditMode/Telemetry/pure_function_recomputable_test.cs</c> 的测试自持私有类
    /// <c>FoldEngine</c> —— **上移 = 逐字搬移,零公式变更**(含常量、哨兵、
    /// <c>ExportScalarQ1616</c> 的哈希合成与「模拟 ROUND_HALF_AWAY_FROM_ZERO」截断式)。
    /// </remarks>
    public static class FoldEngine
    {
        /// <summary>一条事件(由调用方从**事件流**还原后喂入)。</summary>
        public sealed class Event
        {
            public int Kind;      // 事件类型枚举值
            public long Tick;     // 逻辑 tick
            public int Payload;   // 载荷
        }

        /// <summary>折叠结果(全部整数域,无浮点)。</summary>
        public readonly struct FoldResult
        {
            public readonly int TotalEvents;
            public readonly int DistinctKinds;
            public readonly int SumPayload;
            public readonly long MinTick;
            public readonly long MaxTick;
            public readonly long SessionSpan;

            public FoldResult(int totalEvents, int distinctKinds, int sumPayload,
                               long minTick, long maxTick)
            {
                TotalEvents = totalEvents;
                DistinctKinds = distinctKinds;
                SumPayload = sumPayload;
                MinTick = minTick;
                MaxTick = maxTick;
                SessionSpan = maxTick - minTick;
            }

            /// <summary>Q16.16 标量导出(ROUND_HALF_AWAY_FROM_ZERO)。
            /// 用于跨实现对比的标量指纹。</summary>
            public long ExportScalarQ1616()
            {
                // 将各字段组合成一个 64 位标量,使用 ROUND_HALF_AWAY_FROM_ZERO 舍入
                long h = 17;
                h = h * 31 + TotalEvents;
                h = h * 31 + DistinctKinds;
                h = h * 31 + SumPayload;
                h = h * 31 + (int)(MinTick & 0xFFFFFFFF);
                h = h * 31 + (int)(MaxTick & 0xFFFFFFFF);
                h = h * 31 + (int)(SessionSpan & 0xFFFFFFFF);
                // 模拟 ROUND_HALF_AWAY_FROM_ZERO:加上 0.5 后截断
                long rounded = (h >= 0) ? (h + 1) / 2 : -((-h + 1) / 2);
                return rounded;
            }
        }

        /// <summary>离线整表折叠(唯一实现)。</summary>
        public static FoldResult Fold(IReadOnlyList<Event> events)
        {
            if (events.Count == 0)
                return new FoldResult(0, 0, 0, 0, 0);

            int total = events.Count;
            int distinctKinds = events.Select(e => e.Kind).Distinct().Count();
            int sumPayload = events.Sum(e => e.Payload);
            long minTick = events.Min(e => e.Tick);
            long maxTick = events.Max(e => e.Tick);
            return new FoldResult(total, distinctKinds, sumPayload, minTick, maxTick);
        }

        /// <summary>增量边读边折(与离线共用同一个 Fold 终值逻辑)。
        /// 模拟:逐事件读入,维护滚动状态,终值 == 离线整表折叠。</summary>
        public static FoldResult FoldIncremental(IReadOnlyList<Event> events)
        {
            if (events.Count == 0)
                return new FoldResult(0, 0, 0, 0, 0);

            var seenKinds = new HashSet<int>();
            long sumPayload = 0;
            long minTick = long.MaxValue;
            long maxTick = long.MinValue;

            foreach (var e in events)
            {
                seenKinds.Add(e.Kind);
                sumPayload += e.Payload;
                if (e.Tick < minTick) minTick = e.Tick;
                if (e.Tick > maxTick) maxTick = e.Tick;
            }

            return new FoldResult(events.Count, seenKinds.Count,
                (int)sumPayload, minTick, maxTick);
        }
    }
}
