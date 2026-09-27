// ============================================================================
// 技能升级曲线 —— XP_to_next(n) = C × n^P 与 TryLevelUp
// 权威来源: production/epics/skill-system/story-003-level-curve.md
//   · ADR-026 §Decision 一(幂运算唯一整数实现 FixPow) + §Decision 二(定表记忆化)
//   · ADR-026 Implementation Guidelines 1–6
// ============================================================================
// XP_to_next 的全部数学在 Q16.16 Fix 域求值,指数 ∈ {整数, 整数 + 1/2}(G-1 限死)。
// TryLevelUp 可能连升多级;level >= SKILL_CAP(60) 时锁定,返回 false。
// 构建期定表(可选)归构建工具,运行期只读 Fix.Pow 输出。
// ============================================================================

using System;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.Contracts.SkillSystem;

namespace DaYiJingCheng.Sim.Contracts.SkillSystem
{
    /// <summary>
    /// 技能升级曲线与升级判定(纯静态,零 Unity 依赖,住 Sim.Contracts)。
    /// <para>XP_to_next(n) = C × n^P。指数 ∈ {整数, 整数+1/2}(G-1 限死);
    /// P=1.4 类非闭集指数会在 Fix.Pow 层抛 ArgumentOutOfRangeException。</para>
    /// </summary>
    public static class SkillProgression
    {
        /// <summary>获取从 level 到 level+1 所需的经验值。
        /// <para>level &lt; SKILL_CAP(60) → 返回 Fix(C × P^level);
        /// level >= SKILL_CAP → 返回 <see cref="Fix.PositiveInfinity"/>(满级锁定)。</para>
        /// </summary>
        /// <param name="level">当前等级(0–59 为有效计算域)。</param>
        /// <param name="tuning">调参表(承载 C 与 P)。</param>
        /// <returns>升级所需 XP(Fix);满级时返回 PositiveInfinity。</returns>
        /// <exception cref="ArgumentOutOfRangeException">当 tuning.P 不是整数或半整数时,
        /// 由 Fix.Pow 抛出(构建期校验,G-1 限死)。</exception>
        public static Fix XpToNext(int level, SkillTuningTable tuning)
        {
            if (level >= SkillRegistry.SKILL_CAP)
                return Fix.PositiveInfinity;

            // XP_to_next(n) = C × n^P,全程 Fix 域(n = level)
            return tuning.C * Fix.Pow(new Fix(level * Fix.OneRaw), tuning.P);
        }

        /// <summary>尝试用 xpGained 升级。可能连升多级(若 XP 足够)。
        /// <para>循环:while(currentXp >= XpToNext(currentLevel) && currentLevel < SKILL_CAP)
        /// → subtract cost → level++。level 被钳制在 SKILL_CAP 以内。</para>
        /// </summary>
        /// <param name="currentLevel">当前等级(in/out,升级后递增,上限 SKILL_CAP)。</param>
        /// <param name="currentXp">当前累计 XP(in/out,扣除升级消耗后剩余)。</param>
        /// <param name="xpGained">本次获得的 XP(Fix,非负)。</param>
        /// <param name="tuning">调参表。</param>
        /// <returns>true = 等级发生变化;false = 未升级(满级或 XP 不足)。</returns>
        /// <exception cref="ArgumentOutOfRangeException">当 tuning.P 非法时,
        /// 由 Fix.Pow 抛出(构建期校验)。</exception>
        public static bool TryLevelUp(ref int currentLevel, ref Fix currentXp, Fix xpGained, SkillTuningTable tuning)
        {
            if (xpGained.Raw <= 0)
                return false;                   // 零 / 负 XP 不触发升级

            if (currentLevel >= SkillRegistry.SKILL_CAP)
                return false;                   // 已满级,锁定

            // 累加 XP
            currentXp += xpGained;

            bool leveled = false;
            while (currentLevel < SkillRegistry.SKILL_CAP)
            {
                Fix cost = XpToNext(currentLevel, tuning);
                if (cost == Fix.PositiveInfinity)
                    break;                      // 不应发生(level < SKILL_CAP 保证),防御性

                if (currentXp.Raw < cost.Raw)
                    break;                      // XP 不足,退出循环

                // 扣除升级消耗
                currentXp = currentXp - cost;
                currentLevel++;
                leveled = true;
            }

            return leveled;
        }
    }
}
