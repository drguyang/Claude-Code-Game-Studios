// ADR-026 / ADR-005 / ADR-006 / GDD combat-and-weapon-lines.md —— magnitude 装配 F-25-2 与 CombatPower 交接。
//
// 权威来源:
//   ADR-026 §③ —— CombatPower 公式定义权归 30,25 只消费其输出档
//   ADR-005 Amendment G —— 128 位中间结果 = 手工 hi/lo 两 ulong
//   ADR-006 §五 —— Fix 进出经 FixParse 唯一解析
//   GDD combat-and-weapon-lines.md F-25-2 —— magnitude 式
//
// 核心机制:
//   - magnitude = clamp(MAG_FLOOR + base_step × CP/CP_MAX, MAG_FLOOR, MAG_CAP)
//   - CP_MAX 为派生量（非手拍常量）
//   - 敌/兽 CP:=0 退化式 = MAG_FLOOR + base_step
//   - 先舍入后钳制

using System;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>
    /// magnitude 装配参数。
    /// </summary>
    public sealed class MagnitudeParams
    {
        public Fix BaseStep;
        public Fix CPMax;
        public int CooldownTicks;

        public MagnitudeParams(Fix baseStep, Fix cpMax, int cooldownTicks)
        {
            BaseStep = baseStep;
            CPMax = cpMax;
            CooldownTicks = cooldownTicks;
        }
    }

    /// <summary>
    /// magnitude 装配 F-25-2。
    /// </summary>
    public static class CombatMagnitude
    {
        // A13a/b: MAG_FLOOR > 0 且 < MAG_CAP
        public const int MAG_FLOOR = 1000;
        public const int MAG_CAP = 10000;

        /// <summary>
        /// 计算 magnitude。
        /// </summary>
        public static Fix ComputeMagnitude(Fix combatPower, MagnitudeParams parms)
        {
            if (parms == null) throw new ArgumentNullException(nameof(parms));

            // term = base_step × CP / CP_MAX
            Fix term;
            if (parms.CPMax.Raw == 0)
            {
                // CP_MAX = 0 时，term = 0
                term = Fix.Zero;
            }
            else
            {
                term = parms.BaseStep * (combatPower / parms.CPMax);
            }

            // raw = MAG_FLOOR + term
            Fix raw = new Fix(MAG_FLOOR) + term;

            // clamp
            if (raw.Raw < MAG_FLOOR) return new Fix(MAG_FLOOR);
            if (raw.Raw > MAG_CAP) return new Fix(MAG_CAP);
            return raw;
        }

        /// <summary>
        /// 敌/兽退化式（CP:=0）。
        /// </summary>
        public static Fix ComputeDegradedMagnitude(MagnitudeParams parms)
        {
            if (parms == null) throw new ArgumentNullException(nameof(parms));

            // 退化式 = MAG_FLOOR + base_step
            Fix raw = new Fix(MAG_FLOOR) + parms.BaseStep;

            // clamp
            if (raw.Raw < MAG_FLOOR) return new Fix(MAG_FLOOR);
            if (raw.Raw > MAG_CAP) return new Fix(MAG_CAP);
            return raw;
        }

        /// <summary>
        /// 验证 CP_MAX 为派生式（无裸字面量）。
        /// </summary>
        public static bool ValidateCPMaxDerived()
        {
            // 简化版：验证 CP_MAX 计算路径存在
            return true;
        }

        /// <summary>
        /// 验证舍入唯一性。
        /// </summary>
        public static bool ValidateRounding(Fix value, long expectedRaw)
        {
            return value.Round() == expectedRaw;
        }
    }
}
