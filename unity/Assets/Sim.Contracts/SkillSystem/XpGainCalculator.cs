// ============================================================================
// 单次经验增益计算器 —— Story 002
// 权威来源: Story 002 · ADR-026 §Decision 四(XP_gain = BASE × K_difficulty × K_novelty)
// ============================================================================
// 本类纯静态计算,零 Unity 依赖,住 Sim.Contracts(BCL only)。
// XP_gain 全部在 Q16.16 定点域求值,三次 Fix.Mul 乘法链。
// SKILL_CAP 上限检查由 caller 负责,本方法不做截断。
// ============================================================================

using System;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.Contracts.SkillSystem
{
    /// <summary>单次经验增益公式计算器。</summary>
    /// <remarks>
    /// <para>公式: <c>XP_gain = BASE × K_difficulty × K_novelty</c>(全 Q16.16)。</para>
    /// <para><c>BASE</c> 读自 <see cref="SkillRegistry"/>;<c>K_difficulty</c> 由调用方推入;
    /// <c>K_novelty</c> 由 <see cref="NoveltyClass"/> 查表。</para>
    /// <para>三次 Fix.Mul 链: BASE × K_difficulty → Q32.32 中间积 → 移位回 Q16.16 → × K_novelty。</para>
    /// <para>零分配:返回栈分配 <see cref="Fix"/> 值类型。</para>
    /// </remarks>
    public static class XpGainCalculator
    {
        // ── 新颖度系数表(硬编码,与 SkillTuningTable.Default 对齐) ────────────
        // First = 3.0, Normal = 1.0, Stale = 0.2

        private static readonly Fix[] NoveltyCoefficients = new Fix[(int)NoveltyClass.Normal + 1]
        {
            new Fix(3L * Fix.OneRaw / 1),    // First = ×3.0
            new Fix(2L * Fix.OneRaw / 10),   // Stale  = ×0.2
            new Fix(1L * Fix.OneRaw / 1),    // Normal = ×1.0
        };

        /// <summary>计算单次动作应得的经验值(Q16.16)。</summary>
        /// <param name="skillId">技能 id(SkillId ordinal, 0–18)。</param>
        /// <param name="noveltyClass">新颖度类别(First / Stale / Normal)。</param>
        /// <param name="difficulty">难度系数 Fix(典型范围 1.0–3.0;调用方负责合法性)。</param>
        /// <returns>该次事件应得的经验值(Fix, Q16.16)。</returns>
        /// <exception cref="ArgumentOutOfRangeException">noveltyClass 不在 {First, Stale, Normal}。</exception>
        public static Fix ComputeXpGain(int skillId, NoveltyClass noveltyClass, Fix difficulty)
        {
            // BASE 来自技能注册表
            Fix baseXp = SkillRegistry.GetDefinition(skillId).BaseXp;

            // 新颖度系数查表
            int noveltyIndex = (int)noveltyClass;
            if ((uint)noveltyIndex >= (uint)NoveltyCoefficients.Length)
                throw new ArgumentOutOfRangeException(
                    nameof(noveltyClass),
                    $"NoveltyClass ordinal {noveltyIndex} 超出有效范围 [0, {NoveltyCoefficients.Length - 1}]");
            Fix noveltyCoeff = NoveltyCoefficients[noveltyIndex];

            // XP_gain = BASE × K_difficulty × K_novelty (两次 Fix.Mul 链,三因子连乘)
            Fix intermediate = baseXp * difficulty;
            Fix xpGain = intermediate * noveltyCoeff;

            return xpGain;
        }
    }
}
