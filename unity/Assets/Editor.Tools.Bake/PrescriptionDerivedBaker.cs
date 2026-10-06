// 权威来源:AC-11-09(single_dose_max = 烘焙期派生常量,零手填)
//          · ADR-014 §三/§五(两阶段烘焙 · ConfigVersion = 源数据集内容哈希派生)
//          · ADR-005 Amendment G / ADR-012 F7(中间积 128 位,经 DoseCalculator 唯一实现)
//          · GDD design/gdd/prescription-and-medication.md §F-11.1
//
// ⚠️ 本件是 `single_dose_max` 的**唯一派生点**。派生式(AC-11-09 逐字):
//      single_dose_max = max over(全部药 × dose_range.hi) of |dose_potency|
//    其中 `dose_potency` 一律经 **DoseCalculator**(F-11.1 的唯一实现)求值 ——
//    本件**不重写** F-11.1 公式(AC-11-02「11 不发明结算」同样约束派生层)。
//
// ⚠️ **零手填**:本文件不含任何剂量字面量;改 `assets/data/` 任一源字段 ⇒ 派生值自动重算,
//    且随 ConfigVersion(内容哈希)一起变(AC-11-09 的联动判据)。
//
// ⚠️ 消费方 = 9 的 F1 clamp 上界 `MAX_ACTIVE_DOSE × single_dose_max`
//    (design/registry/entities.yaml §F1);本件只派生,不做 clamp(归 9)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>`single_dose_max` 的烘焙期派生器(AC-11-09)。
    /// <para><b>纯函数</b>:入参 = 已词法化的 items / 处方表常量 —— 同输入逐位同输出。</para>
    /// <para>作者态源 = `item_database_items.json`(`category = drug` 行的 `drug_profile`)
    /// + `prescription_actions.json` 的 `DOSE_BASE`。</para>
    /// </summary>
    public static class PrescriptionDerivedBaker
    {
        /// <summary>派生量的落点名(供日志 / 测试点名;值本身住 cooked 载荷,不落独立文件)。</summary>
        public const string SingleDoseMaxName = "single_dose_max";

        /// <summary>
        /// 从已词法化的源派生 `single_dose_max`(raw Q16.16)。
        /// </summary>
        /// <param name="itemsRoot">`item_database_items.json` 根节点。</param>
        /// <param name="doseBase">`prescription_actions.json` 的 `DOSE_BASE`(须 &gt; 0)。</param>
        /// <returns>`max |dose_potency|` 的 raw 值(恒 ≥ 0)。</returns>
        /// <exception cref="BakeValidationException">
        /// DOSE_BASE ≤ 0 · 药行缺 `base_id` · `drug_potency` 非 Fix 字符串 ·
        /// `dose_range` 形态非法 · 处方表有行而该药无 `drug_potency`(clamp 上界会退化为 0)。</exception>
        /// <remarks>`internal`:`JsonNode` 是装配内类型(阶段 1 词法器产物);测试经
        /// <see cref="PrescriptionActionsBinderProbe"/> 的 public 转发驱动同一台机器。</remarks>
        internal static long DeriveSingleDoseMaxRaw(JsonNode itemsRoot, int doseBase)
        {
            var errors = new List<string>();
            if (doseBase <= 0)
                errors.Add($"{SingleDoseMaxName} 派生:DOSE_BASE = {doseBase} 必须 > 0");

            if (itemsRoot.Kind != JsonNodeKind.Object ||
                !itemsRoot.TryGet("items", out JsonNode itemsArr) ||
                itemsArr.Kind != JsonNodeKind.Array)
            {
                errors.Add($"{SingleDoseMaxName} 派生:item_database_items.json 缺 items 数组");
                throw new BakeValidationException(errors);
            }

            long maxAbs = 0L;
            int drugCount = 0;

            for (int i = 0; i < itemsArr.Items.Count; i++)
            {
                JsonNode item = itemsArr.Items[i];
                if (item.Kind != JsonNodeKind.Object) continue;
                if (!item.TryGet("category", out JsonNode cat) ||
                    cat.Kind != JsonNodeKind.String || cat.Str != "drug") continue;

                string label = item.TryGet("base_id", out JsonNode bid) && bid.Kind == JsonNodeKind.String
                    ? bid.Str
                    : $"items[{i}]";
                drugCount++;

                if (!item.TryGet("drug_profile", out JsonNode dp) || dp.Kind != JsonNodeKind.Object)
                {
                    errors.Add($"{SingleDoseMaxName} 派生:药 '{label}' 无 drug_profile");
                    continue;
                }

                if (!TryReadPotency(dp, label, errors, out Fix potency))
                    continue;

                // dose_range 为空 / 缺 ⇒ 整剂路径(DoseCalculator.CalculateForDrug 同口径):
                // dose_potency = drug_potency,不乘不除。
                DoseResult? r = TryDoseAtHi(dp, potency, doseBase, label, errors);
                if (!r.HasValue) continue;

                long abs = r.Value.DosePotency.Raw;
                if (abs < 0) abs = unchecked(-abs);
                if (abs > maxAbs) maxAbs = abs;
            }

            if (errors.Count > 0)
                throw new BakeValidationException(errors);

            // 有药行却派生为 0 ⇒ 9 的 clamp 上界退化为 0(全药效被截断)= 数据错误,硬失败。
            if (drugCount > 0 && maxAbs == 0L)
                errors.Add($"{SingleDoseMaxName} 派生:有 {drugCount} 味药但派生值 = 0 —— " +
                           "9 的 F1 clamp 上界(MAX_ACTIVE_DOSE × single_dose_max)会退化为 0,硬失败");
            if (errors.Count > 0)
                throw new BakeValidationException(errors);

            return maxAbs;
        }

        /// <summary>读 `drug_profile.drug_potency`(Fix 字符串,ADR-014 §四:禁 JSON 数字承载 Fix)。</summary>
        private static bool TryReadPotency(JsonNode dp, string label, List<string> errors, out Fix potency)
        {
            potency = default;
            if (!dp.TryGet("drug_potency", out JsonNode node))
            {
                errors.Add($"{SingleDoseMaxName} 派生:药 '{label}' 缺 drug_potency");
                return false;
            }
            if (node.Kind != JsonNodeKind.String)
            {
                errors.Add($"{SingleDoseMaxName} 派生:药 '{label}' 的 drug_potency 须为 Fix 字符串" +
                           $"(实得 {node.Kind};ADR-014 §四)");
                return false;
            }
            try
            {
                potency = FixParse.Parse(node.Str);
                return true;
            }
            catch (Exception ex)
            {
                errors.Add($"{SingleDoseMaxName} 派生:药 '{label}' drug_potency FixParse 失败:{ex.Message}");
                return false;
            }
        }

        /// <summary>取该药在 `dose_range.hi` 档的 `dose_potency`(无域 ⇒ 整剂路径)。</summary>
        private static DoseResult? TryDoseAtHi(
            JsonNode dp, Fix potency, int doseBase, string label, List<string> errors)
        {
            DoseRange? range = null;
            if (dp.TryGet("dose_range", out JsonNode dr) && dr.Kind != JsonNodeKind.Null)
            {
                if (dr.Kind != JsonNodeKind.Array || dr.Items.Count != 2)
                {
                    errors.Add($"{SingleDoseMaxName} 派生:药 '{label}' 的 dose_range 须为 [lo, hi] 两元数组或 null");
                    return null;
                }
                range = new DoseRange((int)dr.Items[0].Int, (int)dr.Items[1].Int);
            }

            try
            {
                // hi = 域上界;无域 ⇒ CalculateForDrug 的整剂旁路(dose := 1,potency 原样)。
                int hi = range.HasValue ? range.Value.Max : 1;
                return DoseCalculator.CalculateForDrug(potency, range, hi, doseBase);
            }
            catch (Exception ex)
            {
                errors.Add($"{SingleDoseMaxName} 派生:药 '{label}' 在 dose_range.hi 求值失败:{ex.Message}");
                return null;
            }
        }
    }
}
