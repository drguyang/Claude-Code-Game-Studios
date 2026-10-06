// 权威来源:GDD F-11.1(剂量定点化)· 规则七(域内整数 · 资源刻度)
//          · ADR-006(Q16.16 整数域 · ROUND_HALF_AWAY_FROM_ZERO)
//          · ADR-012 F7(int64 直乘 IL2CPP UB ⇒ 中间积 128 位)
//          · AC-11-11(零浮点 / 舍入唯一 / 中间积宽度)
//          · AC-11-17(空 dose_range ⇒ 整剂给药 dose := 1)
//
// 剂量计算纯函数:给定 drug_potency(Q16.16)、dose(整数档)、DOSE_BASE(整数),
// 返回 dose_potency(Q16.16)。不触 IEventSink,不触 IIdAuthority,只算值。
// 生产代码 —— 测试测的是本文件,不是测试自己的副本。

using System;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>
    /// 剂量计算结果。
    /// </summary>
    public readonly struct DoseResult
    {
        /// <summary>计算得到的 dose_potency(Q16.16)。</summary>
        public readonly Fix DosePotency;

        /// <summary>实际使用的 dose(域可空时 = 1)。</summary>
        public readonly int EffectiveDose;

        public DoseResult(Fix dosePotency, int effectiveDose)
        {
            DosePotency = dosePotency;
            EffectiveDose = effectiveDose;
        }
    }

    /// <summary>
    /// 剂量定点化计算器(GDD F-11.1)。
    /// </summary>
    public static class DoseCalculator
    {
        /// <summary>
        /// 计算 dose_potency = RoundFixHalfAway(drug_potency × dose / DOSE_BASE)。
        /// </summary>
        /// <param name="drugPotency">药效基础幅值(Q16.16,来自 21a drug_profile)。</param>
        /// <param name="dose">剂量整数档(域可空时传 1)。</param>
        /// <param name="doseBase">剂量基准(DOSE_BASE,整数 &gt; 0)。</param>
        /// <returns>计算结果。</returns>
        /// <exception cref="ArgumentOutOfRangeException">doseBase ≤ 0。</exception>
        public static DoseResult Calculate(Fix drugPotency, int dose, int doseBase)
        {
            if (doseBase <= 0)
                throw new ArgumentOutOfRangeException(nameof(doseBase),
                    $"DOSE_BASE 必须 > 0,实际 = {doseBase}");

            // F-11.1: dose_potency = RoundFixHalfAway(drug_potency × dose / DOSE_BASE)
            // 中间积 drug_potency × dose 用 128 位手工 hi/lo(ADR-012 F7:
            // int64 直乘在 IL2CPP 下有符号溢出为 UB)。
            // drug_potency 是 Q16.16(≤ 2^47),dose 是 int(≤ 2^31),
            // 但 21a 未声明 drug_potency 的域(BL-7),故无条件走 128 位路径。
            long rawPotency = drugPotency.Raw;

            // 计算 128 位积
            bool neg = (rawPotency < 0) ^ (dose < 0);
            ulong ua = rawPotency < 0 ? unchecked(0UL - (ulong)rawPotency) : (ulong)rawPotency;
            ulong ub = dose < 0 ? unchecked(0UL - (ulong)dose) : (ulong)dose;
            ulong hi, lo;
            Mul128(ua, ub, out hi, out lo);

            // 计算商 = 积 / doseBase
            ulong ubase = (ulong)doseBase;
            ulong qHi, qLo, rem;
            Div128(hi, lo, ubase, out qHi, out qLo, out rem);

            // 舍入 = ROUND_HALF_AWAY_FROM_ZERO
            if (rem * 2 >= ubase)
            {
                qLo++;
                if (qLo == 0UL) qHi++;  // 进位
            }

            // 检查是否超域
            if (qHi != 0UL)
                throw new OverflowException(
                    $"DoseCalculator:商超出 Q16.16 域(qHi = {qHi})—— drug_potency × dose / DOSE_BASE 超出 Q16.16 域");

            // 回符号
            ulong result = qLo;
            const ulong SignBound = 1UL << 63;
            if (result > SignBound || (result == SignBound && !neg))
                throw new OverflowException("DoseCalculator:结果超出 int64 定点域");

            return new DoseResult(new Fix(neg ? unchecked(-(long)result) : (long)result), dose);
        }

        /// <summary>
        /// 128 位无符号乘法:a × b = (hi, lo)。
        /// 权威:ADR-005 Amendment G(手工 hi/lo 两 ulong 带进位)。
        /// </summary>
        private static void Mul128(ulong a, ulong b, out ulong hi, out ulong lo)
        {
            ulong x0 = a & 0xFFFFFFFFUL, x1 = a >> 32;
            ulong y0 = b & 0xFFFFFFFFUL, y1 = b >> 32;
            ulong p00 = unchecked(x0 * y0);
            ulong p01 = unchecked(x0 * y1);
            ulong p10 = unchecked(x1 * y0);
            ulong p11 = unchecked(x1 * y1);
            ulong midSum = unchecked(p01 + p10);
            ulong midCarry = midSum < p01 ? 1UL : 0UL;
            lo = unchecked(p00 + unchecked(midSum << 32));
            ulong loCarry = lo < p00 ? 1UL : 0UL;
            hi = unchecked(p11 + (midSum >> 32) + (midCarry << 32) + loCarry);
        }

        /// <summary>
        /// 128 位 ÷ 64 位除法:(hi, lo) / divisor = (qHi, qLo),余数 = rem。
        /// </summary>
        private static void Div128(ulong hi, ulong lo, ulong divisor, out ulong qHi, out ulong qLo, out ulong rem)
        {
            qHi = 0UL;
            qLo = 0UL;
            rem = 0UL;

            // 高 64 位
            for (int i = 63; i >= 0; i--)
            {
                rem = (rem << 1) | ((hi >> i) & 1UL);
                if (rem >= divisor)
                {
                    rem -= divisor;
                    qHi |= (1UL << i);
                }
            }

            // 低 64 位
            for (int i = 63; i >= 0; i--)
            {
                rem = (rem << 1) | ((lo >> i) & 1UL);
                if (rem >= divisor)
                {
                    rem -= divisor;
                    qLo |= (1UL << i);
                }
            }
        }

        /// <summary>
        /// 解析有效 dose:域可空 ⇒ 1(整剂给药),否则取玩家选择的档。
        /// </summary>
        /// <param name="doseRange">剂量范围(可空)。</param>
        /// <param name="selectedDose">玩家选择的剂量档。</param>
        /// <returns>有效 dose。</returns>
        public static int ResolveEffectiveDose(DoseRange? doseRange, int selectedDose)
        {
            if (doseRange == null)
                return 1;  // AC-11-17:空 dose_range ⇒ 整剂给药
            return selectedDose;
        }

        /// <summary>
        /// 组合入口:给定药物档案与玩家选择,计算最终 dose_potency。
        /// AC-11-17:空 dose_range ⇒ 整剂给药,dose_potency = drug_potency(旁路公式)。
        /// </summary>
        /// <param name="drugPotency">药效基础幅值(Q16.16,来自 21a drug_profile)。</param>
        /// <param name="doseRange">剂量范围(可空)。</param>
        /// <param name="selectedDose">玩家选择的剂量档。</param>
        /// <param name="doseBase">剂量基准(DOSE_BASE,整数 &gt; 0)。</param>
        /// <returns>计算结果。</returns>
        public static DoseResult CalculateForDrug(Fix drugPotency, DoseRange? doseRange, int selectedDose, int doseBase)
        {
            int effectiveDose = ResolveEffectiveDose(doseRange, selectedDose);
            if (doseRange == null)
            {
                // AC-11-17:整剂给药 ⇒ dose_potency = drug_potency(旁路 F-11.1 公式)
                return new DoseResult(drugPotency, effectiveDose);
            }
            return Calculate(drugPotency, effectiveDose, doseBase);
        }
    }
}
