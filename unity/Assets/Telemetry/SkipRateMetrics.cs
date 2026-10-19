// 权威来源:production/epics/telemetry-analytics/story-004-f3-skip-rate.md
//   (AC-51-B8…B9 — F3 跳过率:Skip/Opportunity/SkipByChoice + NotOffered 不入分母)
//   · F3 = Skip / Opportunity;SkipByChoice 与降级强制分开报
//   · 分母 = 可跳过机会数(提供小游戏的实例数);NotOffered 不入分母
//   · Opportunity = 0 ⇒ 不可定义(报 (0,0));不抛除零
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

using System.Collections.Generic;
using System.Linq;

namespace DaYiJingCheng.Telemetry
{
    /// <summary>F3 跳过率指标 —— 纯函数重算面(ADR-019 §一「回放即数据记录」的落地)。
    /// <para>语义 = GDD Formulas F3:SkipRate = Skip / Opportunity;SkipByChoice 分开报。</para>
    /// <para>**零埋点**:输入由调用方从既有事件流还原;本类不做任何 IO、不订阅、不写。</para></summary>
    /// <remarks>
    /// ⚠️ 上移台账(TD-epic-01):本类逐字搬自
    /// <c>Tests/EditMode/Telemetry/f3_skip_rate_test.cs</c> 的测试自持私有类
    /// <c>F3Formula</c> —— **上移 = 逐字搬移,零公式变更**(含常量、哨兵与分支顺序)。
    /// </remarks>
    public static class SkipRateMetrics
    {
        /// <summary>`cause = 玩家选择` 的显示名(GDD 规则十二的枚举名;序数 0 归 10)。</summary>
        public const string CauseNamePlayerSelection = "玩家选择";

        /// <summary>`cause = 降级` 的显示名(GDD 规则十二;序数 1 归 10)。</summary>
        public const string CauseNameDegraded = "降级";

        /// <summary>cause 序数 → 显示名(序数承 10:`0 = 玩家选择` · `1 = 降级`,见 `EmergencyTreatmentApplied.cause`)。
        /// <para>闭集外序数 ⇒ null(**不猜**)—— 该实例的 cause 计为「未知」,不进 SkipByChoice。</para></summary>
        public static string CauseNameOf(int ordinal)
        {
            switch (ordinal)
            {
                case 0: return CauseNamePlayerSelection;
                case 1: return CauseNameDegraded;
                default: return null;
            }
        }

        /// <summary>一次动作实例(由调用方从**事件流**还原后喂入)。</summary>
        public sealed class ActionInstance
        {
            public int InstanceId;
            public string Resolution;   // Played / Skipped / NotOffered
            public string Cause;        // 玩家选择 / 降级 / null
        }

        /// <summary>计算 F3 指标。
        /// 返回 (skipNum, skipDen, skipByChoiceNum, skipByChoiceDen, denominatorZero)。
        /// Opportunity = 提供小游戏的实例数(不含 NotOffered);分母 0 ⇒ 不可定义。</summary>
        public static (int skipNum, int skipDen, int choiceNum, int choiceDen, bool denominatorZero)
            Compute(IReadOnlyList<ActionInstance> instances)
        {
            int opportunity = instances.Count(i => i.Resolution != "NotOffered");
            if (opportunity == 0)
                return (0, 0, 0, 0, true);   // 分母 0 ⇒ 不可定义

            int skip = instances.Count(i => i.Resolution == "Skipped");
            int choice = instances.Count(i => i.Resolution == "Skipped" && i.Cause == CauseNamePlayerSelection);
            return (skip, opportunity, choice, opportunity, false);
        }

        /// <summary>降级跳过数(`Skipped ∧ cause = 降级`)—— AC-51-B8「两个独立分子」的第二分子。
        /// <para>与 <see cref="Compute"/> 的 SkipByChoice **分开报**,不并入 <c>Skip</c> 单一形状。</para></summary>
        public static int CountDegradedSkips(IReadOnlyList<ActionInstance> instances)
            => instances.Count(i => i.Resolution == "Skipped" && i.Cause == CauseNameDegraded);
    }
}
