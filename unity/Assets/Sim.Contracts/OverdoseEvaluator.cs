// ADR-035 §⑥ —— **过量支**的定档判据(纯函数)。
//
// 权威来源:
//   ADR-035 §①B/④(53 的代价表键 = `(harm_kind, band)`)
//   ADR-035 §⑥ 的解除条件(`TD-sim-02` + `TD-prescription-09` 两条均结案)
//     ⇒ 2026-10-30:`TD-prescription-09` 已 closed;`TD-sim-02` 由 9 侧
//        `ProgressionEvaluator.ComputePeakDrugLoad` + `VitalsDto.PeakDrugLoad` 结清。
//   用户 2026-10-30 两项裁定:① 9 侧走**独立状态量**、不进 `Progress`;
//     ② 「过量」按**同一味药**(`处置_id`)累积 —— P0 无「槽」结构(12 药物槽是 P1a)。
//
// ── 分工(四层所有权,承 ADR-035 §①)────────────────────────────────────────
//   · **9** 出「同一味药的最大当前体内药量」(`PeakDrugLoad`,绝对值,同 `drug_potency` 量纲)
//     —— 那是**物质基础**,不是后果。
//   · **53** 定「多少算过量、分几档」(本文件的 `bandThresholds` 来自 53 的代价表)
//     并结算后果。9 不判档,53 不自算药量。
//
// ⚠️ **为什么阈值是绝对值而不是比值**:`MAX_ACTIVE_DOSE` / `single_dose_max` 两个 F1 旋钮
//   的取值**至今待定**(`ProgressionEvaluator` 的 `doseClampBound` 生产路径为 `null`),
//   拿它们归一化会让「过量门槛」依赖两个未裁定的数。改由 53 直接给绝对阈值,
//   与 `drug_potency` 同量纲 ⇒ 不引入对 9 内部旋钮的耦合。
//
// ⚠️ **阈值必须严格递增**:非递增会让某档结构性不可达(与 R1-37 的死词条同型),
//   构建期由 `CostTableBinder` 拒收,运行期本文件**不做排序**(排序会掩盖作者的笔误)。

namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary>过量定档的**唯一求值点**(纯函数,零状态,可重放)。</summary>
    public static class OverdoseEvaluator
    {
        /// <summary>「未达过量」的哨兵 —— 与禁忌侧 <c>NoContraindication</c> 同款(负数选法同理由:
        /// 档 0 是合法档,不能拿 0 当「无」)。</summary>
        public const int NoOverdose = -1;

        /// <summary>
        /// 求值:返回「已越过的最高档」;未达最低阈值返回 <see cref="NoOverdose"/>。
        /// </summary>
        /// <param name="peakDrugLoad">9 的 `VitalsDto.PeakDrugLoad`(同一味药的最大当前体内药量)。</param>
        /// <param name="bandThresholds">各档**下界**(升序;`band i` 的下界 = 第 i 项)。
        /// <c>null</c> / 空 ⇒ 过量支未启用,恒返回哨兵。</param>
        /// <remarks>
        /// <para>判据是 <c>load ≥ threshold[i]</c>(**闭区间下界**)—— 与 F1 的 `position ≥ appear_at`
        /// 同口径。取「已越过的最高档」而不是「第一个未越过的档 - 1」,是为了让
        /// 阈值即便乱序也能给出保守(偏轻)结果,而不是抛。</para>
        /// </remarks>
        public static int Evaluate(Fix peakDrugLoad, System.Collections.Generic.IReadOnlyList<Fix> bandThresholds)
        {
            if (bandThresholds == null || bandThresholds.Count == 0) return NoOverdose;

            int band = NoOverdose;
            for (int i = 0; i < bandThresholds.Count; i++)
            {
                if (peakDrugLoad.Raw >= bandThresholds[i].Raw) band = i;
            }
            return band;
        }
    }
}
