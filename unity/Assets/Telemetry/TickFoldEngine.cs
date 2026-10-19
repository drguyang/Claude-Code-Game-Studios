// 权威来源:production/epics/telemetry-analytics/story-009-blank-and-degraded.md
//   (AC-51-D1…D9 — 空白与退化处理:空输入零值 / 分母 0 报 (0,0)+标记 /
//    Faulted 不产出半份 / 规模不改变数值 / 部分流显式标注)
//   · D1:三流皆空 ⇒ 状态 = Computed(非 Faulted)、全部指标零值、不抛异常
//   · D2:分母 0 ⇒ 输出整数对 (0,0) + 显式「分母 0/未定义」标记;不报 0/1、不报标量 0、不报 NaN
//   · D3:Faulted 不产出半份 —— 状态 = Faulted 且中止;ITelemetrySink 零调用;
//          本地文件未生成/未覆写(旧文件字节保持);无部分指标;不可自愈(后续 Compute() 从 Idle 重入)
//         ⚠️ 生产主语在 `StreamIntegrity.cs`(`StreamIntegrity` + `ComputeSession`)与
//            `Gameplay.Boot/TelemetryReporter.ComputeAndSink`;本类的 `Fold(IReadOnlyList<int>)` 仍是
//            D1/D5/D6 的折叠面,D3 不在本类内承担。
//   · D5:R7 整数纪律 —— 无 float/double;率以 (num, den) 承载;
//          标量导出 = ROUND_HALF_AWAY_FROM_ZERO((num << 16) / den)
//   · D6:规模不改变数值 —— 同一超长流以 FOLD_MEMORY_MODE 两值各重算,全部指标逐位相同
//   · D7:ConfigVersion 不匹配 —— 报告含显式「版本不匹配」标注,不静默出数
//   · D8:部分流 —— 要么拒绝要么产出并显式标注「部分指标」;禁止把部分流当完整流静默出数
//   · D9:样本不足 —— 报告照常产出数值且含「样本不足」标注;不隐藏、不四舍五入掩盖
// ADR-019 §一(回放即数据记录,零新埋点)· ADR-006 §五(Fix 编码器 + 守恒律)
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
    /// <summary>D6 单一折叠器(空白与退化场景):恰有一个折叠实现;空流 ⇒ 全 0;无跨调用持久状态。
    /// <para>**零埋点**:输入由调用方从既有事件流还原;本类不做任何 IO、不订阅、不写。</para></summary>
    /// <remarks>
    /// ⚠️ 上移台账(TD-epic-01):本类逐字搬自
    /// <c>Tests/EditMode/Telemetry/blank_and_degraded_test.cs</c> 的测试自持私有类
    /// <c>FoldEngine</c> —— **上移 = 逐字搬移,零公式变更**(含常量、哨兵与空流口径)。
    /// <para>⚠️ **2026-10-19 第十六片订正**:story-008 上移的同类折叠器 `FoldEngine`
    /// 经实测**全仓零生产调用点**(只被自己的夹具引用)⇒ 已删除;其 C1/C2 夹具改挂
    /// **真实报告链**(`TelemetryReporter.JudgmentReport` → `JudgmentStreamReducer`
    /// → `JudgmentMetrics.Compute` → `MetricsReport.Render`),不再另立第二折叠面。
    /// 本类因此是全程序集**唯一**的「纯计数折叠」谓词面。</para>
    /// <para>⚠️ 上移时实测:测试内对原私有类**零调用点**(该文件 D1–D9 的断言走本地替身
    /// <c>FoldOutcome</c> / <c>ComputeEmpty</c> / <c>ComputeFaulted</c> / <c>ComputeTwoModes</c>)。
    /// 本件仍按 TD-epic-01 口径**逐字上移**(不因无调用点而丢弃谓词面),是否进一步改为
    /// 由测试驱动本类,归 51 的实现轮,不在本还债件内自行扩大。</para>
    /// </remarks>
    public static class TickFoldEngine
    {
        /// <summary>计算五字段折叠结果(全部整数域)。
        /// 空流 ⇒ (0,0,0,0)(D1);无 float(D5);无外部状态(D6)。</summary>
        public static (int totalEvents, int computedCases, int sumPayload, int minTick, int maxTick)
            Fold(IReadOnlyList<int> ticks)
        {
            if (ticks.Count == 0) return (0, 0, 0, 0, 0);
            return (ticks.Count, ticks.Count,
                ticks.Sum(), ticks.Min(), ticks.Max());
        }
    }
}
