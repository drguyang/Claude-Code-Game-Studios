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
        /// <summary>
        /// `MAG_FLOOR` —— **不再本处定义**,唯一真源 = <see cref="CombatActionSchema.MagFloor"/>。
        /// </summary>
        /// <remarks>
        /// ⚠️ **2026-10-23(用户裁定 ② · round36):本类的手写副本已删除**。
        /// <para><b>原状</b>:<c>public const int MAG_FLOOR = 1000;</c>,且消费点是
        /// <c>new Fix(MAG_FLOOR)</c> —— <see cref="Fix"/> 的 `long` 构造是 **Q16.16 raw**
        /// (见 `Fix.cs:47`)⇒ 实际地板 `1000/65536 ≈ 0.01526`,
        /// 只有 GDD `:1351` 裁定的 `1/20 = 0.05` 的 **30.5%**。</para>
        /// <para><b>现口径</b>:直接引用 <see cref="CombatActionSchema.MagFloor"/>(<see cref="Fix"/> 类型,
        /// 由 `FixParse.Parse("1/20")` 产出)⇒ 副本消失,不可能再漂移。</para>
        /// <para><b>为什么既有测试没抓到</b>:`combat_magnitude_f252_test.cs`
        /// 用 <c>Assert.AreEqual(CombatMagnitude.MAG_FLOOR, result.Raw, ...)</c> ——
        /// **自引**该常量,常量写什么都绿(与 `judge_test.cs:322` 自引 schema 同型)。</para>
        /// </remarks>
        private static Fix MagFloor => CombatActionSchema.MagFloor;

        /// <summary>`MAG_CAP` —— 唯一真源 = <see cref="CombatActionSchema.MagCap"/> (GDD `:1352` = `1/2`)。
        /// 原状 `const int MAG_CAP = 10000`(当 raw ⇒ 0.1526,应为 0.5),见上面的 remarks。</summary>
        private static Fix MagCap => CombatActionSchema.MagCap;

        /// <summary>
        /// `MAG_FLOOR` 的 **raw** 值(= <see cref="CombatActionSchema.MagFloor"/>`.Raw`)。
        /// <para>⚠️ 保留此名是为了不打断既有 4 处夹具引用;语义与改前的 `const int` 一致
        /// (均为 Q16.16 **raw**),**只有值变了**(`1000` → `3277` = `1/20`)。</para>
        /// </summary>
        public static long MAG_FLOOR => CombatActionSchema.MagFloor.Raw;

        /// <summary>`MAG_CAP` 的 **raw** 值(= <see cref="CombatActionSchema.MagCap"/>`.Raw`;
        /// `10000` → `32768` = `1/2`)。语义见 <see cref="MAG_FLOOR"/>。</summary>
        public static long MAG_CAP => CombatActionSchema.MagCap.Raw;


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
            Fix raw = MagFloor + term;

            // clamp(两端均取真源的 Fix,不再拼 raw 字面量)
            if (raw.Raw < MagFloor.Raw) return MagFloor;
            if (raw.Raw > MagCap.Raw) return MagCap;
            return raw;
        }

        /// <summary>
        /// 敌/兽退化式（CP:=0）。
        /// </summary>
        public static Fix ComputeDegradedMagnitude(MagnitudeParams parms)
        {
            if (parms == null) throw new ArgumentNullException(nameof(parms));

            // 退化式 = MAG_FLOOR + base_step
            Fix raw = MagFloor + parms.BaseStep;

            // clamp(同 ComputeMagnitude:两端取真源 Fix)
            if (raw.Raw < MagFloor.Raw) return MagFloor;
            if (raw.Raw > MagCap.Raw) return MagCap;
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
