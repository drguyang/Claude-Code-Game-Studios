// 权威来源:production/epics/telemetry-analytics/story-005-f4-skill-growth.md
//   (AC-51-B10…B11 — F4 熟练度成长:Practice/NoveltyMix/ΔLevel;新颖度三分守恒;不重算 XP)
//   · Practice(skill, W) = |{g : g.skill = skill ∧ Tick(g) ∈ W}|
//   · NoveltyMix(skill, W) = (N_new, N_repeat, N_stale) 按新颖度枚举计数
//   · ΔLevel = Level_end − Level_start;Level_end = 读流终点携带值,绝不读玩家当前/存档现值
//   · 51 不重算 XP:那是 30 的定点域算术;重算 = 破 R7 + 造第二真源
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
    /// <summary>F4 熟练度成长指标 —— 纯函数重算面(ADR-019 §一「回放即数据记录」的落地)。
    /// <para>语义 = GDD Formulas F4:新颖度三分守恒 + 不重算 XP。</para>
    /// <para>**零埋点**:输入由调用方从既有事件流还原;本类不做任何 IO、不订阅、不写。</para></summary>
    /// <remarks>
    /// ⚠️ 上移台账(TD-epic-01):本类逐字搬自
    /// <c>Tests/EditMode/Telemetry/f4_skill_growth_test.cs</c> 的测试自持私有类
    /// <c>F4Formula</c> —— **上移 = 逐字搬移,零公式变更**(含常量、哨兵与边界口径)。
    /// </remarks>
    public static class SkillGrowthMetrics
    {
        /// <summary>一次成长事件(由调用方从**事件流**还原后喂入)。</summary>
        public sealed class GrowthEvent
        {
            public string Skill;
            public string NoveltyClass;  // new / repeat / stale
            public long? Level;          // null = 未携带
        }

        /// <summary>计算 Practice 与 NoveltyMix。
        /// 返回 (practice, nNew, nRepeat, nStale)。</summary>
        public static (int practice, int nNew, int nRepeat, int nStale)
            Compute(IReadOnlyList<GrowthEvent> events, string skill)
        {
            var inWindow = events.Where(e => e.Skill == skill).ToList();
            int practice = inWindow.Count;
            int nNew = inWindow.Count(e => e.NoveltyClass == "new");
            int nRepeat = inWindow.Count(e => e.NoveltyClass == "repeat");
            int nStale = inWindow.Count(e => e.NoveltyClass == "stale");
            return (practice, nNew, nRepeat, nStale);
        }

        /// <summary>尝试计算 ΔLevel(Level_end − Level_start)。
        /// 返回 (success, delta):所有事件携带 Level ⇒ (true, delta);
        /// 任一事件未携带 Level ⇒ (false, null)——「不可得」与 0 可区分。</summary>
        public static (bool success, int? delta)
            TryComputeDeltaLevel(IReadOnlyList<GrowthEvent> events, string skill)
        {
            var inWindow = events.Where(e => e.Skill == skill).ToList();
            if (inWindow.Count == 0) return (true, 0);
            if (inWindow.Any(e => e.Level == null)) return (false, null);
            long start = inWindow.Where(e => e.Level.HasValue).Min(e => e.Level.Value);
            long end = inWindow.Where(e => e.Level.HasValue).Max(e => e.Level.Value);
            return (true, (int)(end - start));
        }

        /// <summary>新颖度三分守恒:N_new + N_repeat + N_stale = Practice。</summary>
        public static bool NoveltyConserves(int practice, int nNew, int nRepeat, int nStale)
            => nNew + nRepeat + nStale == practice;
    }
}
