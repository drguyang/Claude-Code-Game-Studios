// 权威来源:GDD F-11.2(半衰期 = F5 求值)· 规则六(11 在 F5 的求值点)
//          · ADR-006(Q16.16 整数域)· D-21-11(品级 → 时间轴接通)
//          · AC-11-08(唯一求值点 = 11)
//
// 半衰期计算纯函数:给定 Axis_base(Q16.16)、axis_offset_by_quality[](Q16.16 数组)、
// quality(1-based int),返回 Axis_effective(Q16.16)。
// 不触 IEventSink,不触 IIdAuthority,只算值。
// 生产代码 —— 测试测的是本文件,不是测试自己的副本。

using System;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>
    /// 半衰期计算结果。
    /// </summary>
    public readonly struct HalfLifeResult
    {
        /// <summary>计算得到的 Axis_effective(Q16.16)。</summary>
        public readonly Fix AxisEffective;

        /// <summary>实际使用的 quality(1-based)。
        /// <para>m-3(结构评审):当前为**透传**(quality 无 clamp / 无映射 —— 11 侧不重复 clamp,
        /// 越界由 <see cref="HalfLifeCalculator.Calculate"/> 抛异常拦截);预留扩展位。</para></summary>
        public readonly int EffectiveQuality;

        public HalfLifeResult(Fix axisEffective, int effectiveQuality)
        {
            AxisEffective = axisEffective;
            EffectiveQuality = effectiveQuality;
        }
    }

    /// <summary>
    /// 半衰期定点化计算器(GDD F-11.2 = F5 求值)。
    /// </summary>
    public static class HalfLifeCalculator
    {
        /// <summary>
        /// 计算 Axis_effective = Axis_base + axis_offset_by_quality[quality - 1]。
        /// </summary>
        /// <param name="axisBase">半衰期基础值(Q16.16,来自 21a drug_profile.half_life)。</param>
        /// <param name="axisOffsetByQuality">品级偏移表(Q16.16 数组,长度 = MAX_QUALITY)。</param>
        /// <param name="quality">品级档位(1-based,∈ [1, MAX_QUALITY])。</param>
        /// <returns>计算结果。</returns>
        /// <exception cref="ArgumentOutOfRangeException">quality 越界或偏移表为空。</exception>
        public static HalfLifeResult Calculate(Fix axisBase, Fix[] axisOffsetByQuality, int quality)
        {
            if (axisOffsetByQuality == null || axisOffsetByQuality.Length == 0)
                throw new ArgumentOutOfRangeException(nameof(axisOffsetByQuality),
                    "axis_offset_by_quality 不能为空(长度 = MAX_QUALITY ≥ 2)");
            if (quality < 1 || quality > axisOffsetByQuality.Length)
                throw new ArgumentOutOfRangeException(nameof(quality),
                    $"quality ∈ [1, {axisOffsetByQuality.Length}],实际 = {quality}");

            // F-11.2: Axis_effective = Axis_base + axis_offset_by_quality[quality - 1]
            // 纯整数加法(Q16.16 域),无浮点,无舍入。
            // ⚠️ 11 侧不重复 clamp(Axis_effective ≥ MIN_USABLE_HALF_LIFE 由 21a 构建期断言保证)。
            Fix offset = axisOffsetByQuality[quality - 1];

            // B-1(结构评审):走 `Fix.operator+`(checked,溢出抛 OverflowException),
            // **不**直接操作 `.Raw` 做 long 加法 —— 后者在 unchecked 上下文静默回绕,
            // 且 IL2CPP 下有符号溢出为 UB(ADR-012 F7)。溢出 = bug ⇒ 硬失败,非静默。
            Fix effective = axisBase + offset;

            return new HalfLifeResult(effective, quality);
        }

        /// <summary>
        /// 组合入口:给定 21a 药物档案与品级,读取字段并计算 Axis_effective。
        /// <para>M-1(结构评审):本入口有**真实组合逻辑** —— 从 <see cref="DrugProfile"/> 取
        /// <c>HalfLife</c> / <c>AxisOffsetByQuality</c> 两字段并做可空校验(与
        /// <c>DoseCalculator.CalculateForDrug</c> 处理 <c>DoseRange?</c> 同构),非纯透传。</para>
        /// </summary>
        /// <param name="profile">21a 药物档案(须含 HalfLife 与 AxisOffsetByQuality)。</param>
        /// <param name="quality">品级档位(1-based,∈ [1, MAX_QUALITY])。</param>
        /// <returns>计算结果。</returns>
        /// <exception cref="ArgumentOutOfRangeException">HalfLife 为空或 AxisOffsetByQuality 为空。</exception>
        public static HalfLifeResult CalculateForDrug(DrugProfile profile, int quality)
        {
            if (!profile.HalfLife.HasValue)
                throw new ArgumentOutOfRangeException(nameof(profile),
                    "drug_profile.half_life 为空 —— F-11.2 的 Axis_base 缺失(21a 数据错误)");
            if (profile.AxisOffsetByQuality == null)
                throw new ArgumentOutOfRangeException(nameof(profile),
                    "drug_profile.axis_offset_by_quality 为空 —— F5 的偏移表缺失(21a 数据错误)");

            return Calculate(profile.HalfLife.Value, profile.AxisOffsetByQuality, quality);
        }
    }
}
