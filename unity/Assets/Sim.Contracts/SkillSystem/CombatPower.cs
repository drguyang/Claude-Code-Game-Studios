// ============================================================================
// 战斗效能 CombatPower = (CombatSkillLevel × WeaponMultiplier) × (1 + 医术修正)
// 权威来源: production/epics/skill-system/story-004-combat-power-and-medical-mod.md
//   · TR-skill-003(CombatPower 定点求值) / TR-skill-004(医术修正 = 0.20 × 等级/60)
//   · ADR-026 §Decision 四(调参表 Fix 字段) + Implementation Guidelines
//   · ADR-006 §Decision 三(ROUND_HALF_AWAY_FROM_ZERO)
// ============================================================================
// 全路径 Q16.16 定点求值。医术修正 = FixDiv(MED_COMBAT_MOD × 关联医术等级, SKILL_CAP)。
// P0 耦合:仅 WeaponLine.徒手 的关联医术(急救=P0) 吃到修正;其余 P0 线修正 = 0。
// 本文件纯静态方法,零 Unity 依赖,住 Sim.Contracts。
// ============================================================================

using System;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.Contracts.SkillSystem
{
    /// <summary>
    /// 战斗效能计算(纯静态,零 Unity 依赖)。
    /// <para>公式:CombatPower = (CombatSkillLevel × WeaponMultiplier) × (1 + 医术修正)</para>
    /// <para>医术修正 = FixDiv(MED_COMBAT_MOD × 关联医术等级, SKILL_CAP),结果 ∈ [0, 0.20]。</para>
    /// <para>P0 仅 WeaponLine.徒手 吃到医术修正;其余线修正 = 0(手术 P1a 未上线)。</para>
    /// </summary>
    public static class CombatPower
    {
        /// <summary>
        /// 计算 CombatPower。
        /// </summary>
        /// <param name="combatSkillLevel">格斗技能等级(int,0–60)。</param>
        /// <param name="medicalSkillLevel">关联医术技能等级(int,0–60)。</param>
        /// <param name="weaponLine">武器线(决定 WeaponMultiplier 和关联医术)。</param>
        /// <param name="tuning">调参表(提供 MED_COMBAT_MOD / WeaponMultipliers)。</param>
        /// <returns>CombatPower(Fix, Q16.16)。</returns>
        /// <exception cref="ArgumentOutOfRangeException">weaponLine 不在 WeaponLine 枚举范围内。</exception>
        public static Fix Compute(int combatSkillLevel, int medicalSkillLevel, WeaponLine weaponLine, SkillTuningTable tuning)
        {
            if (combatSkillLevel < 0)
                throw new ArgumentOutOfRangeException(nameof(combatSkillLevel),
                    $"combatSkillLevel={combatSkillLevel} 不能为负");
            if (medicalSkillLevel < 0)
                throw new ArgumentOutOfRangeException(nameof(medicalSkillLevel),
                    $"medicalSkillLevel={medicalSkillLevel} 不能为负");
            if ((int)weaponLine < 0 || (int)weaponLine >= tuning.WeaponMultipliers.Length)
                throw new ArgumentOutOfRangeException(nameof(weaponLine),
                    $"WeaponLine {(int)weaponLine} 超出调参表 WeaponMultipliers 范围 [0, {tuning.WeaponMultipliers.Length - 1}]");

            // WeaponMultiplier[line]
            Fix multiplier = tuning.GetWeaponMultiplier(weaponLine);

            // 医术修正: P0 仅 WeaponLine.徒手 的关联医术 = 急救(P0);其余 P0 线修正 = 0
            Fix medicalMod = ComputeMedicalModifier(medicalSkillLevel, weaponLine, tuning);

            // (CombatSkillLevel × WeaponMultiplier) × (1 + 医术修正)
            Fix basePower = new Fix(combatSkillLevel * Fix.OneRaw) * multiplier;
            Fix totalPower = basePower * (Fix.One + medicalMod);

            return totalPower;
        }

        /// <summary>
        /// 计算医术修正分量。P0 仅 WeaponLine.徒手 生效;其余线 = 0。
        /// </summary>
        private static Fix ComputeMedicalModifier(int medicalSkillLevel, WeaponLine weaponLine, SkillTuningTable tuning)
        {
            // P0:仅 WeaponLine.徒手 的关联医术 = 急救(P0)
            // P1a:WeaponLine.短兵 的关联医术 = 手术(P1a,P0 不生效)
            // 其余武器线(P0 / P1a) 均无关联医术 → 修正 = 0
            bool hasLinkedMedical = weaponLine == WeaponLine.徒手 || weaponLine == WeaponLine.短兵;

            if (!hasLinkedMedical)
                return Fix.Zero;

            int linkedMedicalSkillId = SkillRegistry.GetLinkedMedicalSkill(weaponLine);
            if (linkedMedicalSkillId < 0)
                return Fix.Zero;

            // 检查该医术技能是否 P0 上线
            bool medicalIsP0 = SkillRegistry.IsP0(linkedMedicalSkillId);
            if (!medicalIsP0)
                return Fix.Zero;

            // 医术修正 = FixDiv(MED_COMBAT_MOD × 关联医术等级, SKILL_CAP)
            Fix numerator = tuning.MedicalCombatModifier * new Fix(medicalSkillLevel * Fix.OneRaw);
            return numerator / new Fix(SkillRegistry.SKILL_CAP * Fix.OneRaw);
        }
    }
}
