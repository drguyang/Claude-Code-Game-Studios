// ============================================================================
// 死亡掉级 —— new_level = floor(current_level × DEATH_LOSS)
// 权威来源: production/epics/skill-system/story-005-death-penalty.md
//   · AC-1 ~ AC-5(定点求值 / 允许掉到 0 / SkillGrown 触发语义)
//   · ADR-026 §Decision 四(调参表 Fix 字段) + Implementation Guidelines
//   · ADR-005 确定性 sim(纯函数,无随机)
// ============================================================================
// 全路径 Q16.16 定点求值。DEATH_LOSS = SkillTuningTable.Default.DeathLoss(Fix)。
// floor = raw long 右移 16 位(截断,非舍入)。允许掉到 0。
// SkillGrown 事件触发由调用方负责(本文件只定义「掉级后触发」的语义,
// 具体批量/逐 tick 由 29 死亡检测编排层裁定)。
// ============================================================================

using System;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.Contracts.SkillSystem
{
    /// <summary>
    /// 死亡掉级纯函数。
    /// <para>公式: new_level = floor(current_level × DEATH_LOSS)</para>
    /// <para>DEATH_LOSS 来自调参表(默认 0.95 = FixParse("19/20"))。</para>
    /// <para>允许掉到 0(不设下限保护)。0 级技能仍可执行(无加成但不禁止)。</para>
    /// </summary>
    public static class DeathPenalty
    {
        /// <summary>应用死亡掉级惩罚,返回新等级(int)。
        /// 调用方负责在掉级后触发 SkillGrown 事件(Story 007 实现机制)。
        /// </summary>
        /// <param name="currentLevel">当前技能等级(0–60)。</param>
        /// <param name="deathLoss">死亡保留比例(Fix,默认 0.95)。</param>
        /// <returns>掉级后的新等级(int, floor 截断,允许为 0)。</returns>
        /// <exception cref="ArgumentOutOfRangeException">currentLevel 为负时抛出。</exception>
        public static int Apply(int currentLevel, Fix deathLoss)
        {
            if (currentLevel < 0)
                throw new ArgumentOutOfRangeException(nameof(currentLevel),
                    $"currentLevel={currentLevel} 不能为负");

            if (deathLoss.Raw <= 0)
                return 0; // 零或负保留比例 → 直接归零

            // Q16.16: currentLevel × deathLoss, floor = raw >> 16(截断)
            Fix result = new Fix(currentLevel * Fix.OneRaw) * deathLoss;
            return (int)(result.Raw >> Fix.FractionalBits);
        }
    }
}
