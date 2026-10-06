// 权威来源:AC-11-19(可感知地板 dose_potency(d+1) − dose_potency(d) ≥ NOISE_BAND_9)
//          · TR-prescription-019(量纲一致 —— 不得挪用 9 的 σ 或 21a 品级地板)
//          · GDD design/gdd/prescription-and-medication.md §Tuning Knobs
//
// ⚠️ **NOT-RUN(禁借绿)**:本件交付的是**比较器骨架 + 差值序列导出器**,不是判据本身。
//    成因 = BL-2(O-11→9):`NOISE_BAND_9` 在 9 侧**不存在**(无该常量)。
//    ⇒ 断言的**门槛值无主**,整条判据现不可执行;本件只把「数值轮一到即可判」的机器备好。
//
// ⚠️ **量纲纪律(TR-prescription-019)**:本件的比较在**药效幅值域**(Q16.16 raw)内完成。
//    数值轮填 `NOISE_BAND_9` 时,**不得**把 9 的 σ(Progress 域)或 21a 品级地板(tick 域)
//    的数值直接搬来 —— 三个域的量纲不同,搬来即静默错判。
//
// ⚠️ 本件**不判对错、不抛错、不改任何值** —— 只出序列与布尔谓词(消费方 = 测试 / 数值轮)。

using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>AC-11-19 可感知地板的比较器骨架(BL-2 未闭 ⇒ 判据 NOT-RUN)。
    /// <para>职责:① 从 F-11.1 导出**相邻档差值序列**;② 对给定门槛做逐项比较。
    /// 门槛值(`NOISE_BAND_9`)由调用方注入 —— 本件不含任何数值常量(数值归数值轮)。</para>
    /// </summary>
    public static class PerceptibleFloorComparator
    {
        /// <summary>
        /// 导出相邻档差值序列:`Δ(d) = dose_potency(d+1) − dose_potency(d)`,`d ∈ [lo, hi−1]`。
        /// <para>每项都经 <see cref="DoseCalculator.Calculate"/>(F-11.1 的唯一实现)求值 ——
        /// 本件**不重写** F-11.1(AC-11-02)。</para>
        /// </summary>
        /// <param name="drugPotency">药效基础幅值(Q16.16)。</param>
        /// <param name="range">剂量域(须 <c>Max &gt; Min</c>;单档域 ⇒ 空序列)。</param>
        /// <param name="doseBase">DOSE_BASE(&gt; 0)。</param>
        /// <returns>长度 = <c>Max − Min</c> 的差值序列(单档域 = 空数组)。</returns>
        public static long[] DifferenceSequence(Fix drugPotency, DoseRange range, int doseBase)
        {
            int count = range.Max - range.Min;
            if (count <= 0) return new long[0];

            var deltas = new long[count];
            for (int i = 0; i < count; i++)
            {
                int d = range.Min + i;
                long lo = DoseCalculator.Calculate(drugPotency, d, doseBase).DosePotency.Raw;
                long hi = DoseCalculator.Calculate(drugPotency, d + 1, doseBase).DosePotency.Raw;
                deltas[i] = hi - lo;
            }
            return deltas;
        }

        /// <summary>判据骨架:全部相邻档差值 ≥ <paramref name="floorRaw"/>(AC-11-19)。
        /// <para>⚠️ **门槛无主**(BL-2)⇒ 现无生产调用点;测试以合成门槛驱动本谓词,
        /// 证明机器可用。正式对拍 BLOCKED-BY-O-11→9。</para></summary>
        /// <param name="deltas">差值序列(见 <see cref="DifferenceSequence"/>)。</param>
        /// <param name="floorRaw">可感知地板 `NOISE_BAND_9`(raw;量纲 = 药效幅值域)。</param>
        /// <returns>true = 全部项达标(含空序列 —— 单档域真空真)。</returns>
        public static bool SatisfiesFloor(long[] deltas, long floorRaw)
        {
            if (deltas == null || deltas.Length == 0) return true;
            for (int i = 0; i < deltas.Length; i++)
            {
                if (deltas[i] < floorRaw) return false;
            }
            return true;
        }
    }
}
