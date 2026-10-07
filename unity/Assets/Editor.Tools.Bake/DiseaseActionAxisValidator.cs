// 权威来源:GDD design/gdd/disease-simulation.md R1.3(treatable_by 字段)+ R3.2/R3.3(极性表 + 病种表)
//          · Story 007(校验器的**唯一调用点** —— 本件之前它是死代码)
//
// ⚠️ 本件是 story-007 的**校验器**(阶段 2 的校验面)。
//    `assets/data/disease_action_axis.json` → 绑定 + 校验 → 行集。
//
// ⚠️ 全部错误**聚合**后一次抛出(与 ItemDatabaseBaker 同纪律):不首个错即停,
//    让一份表的所有问题一次可见(BakeValidationException.Errors)。

using System;
using System.Collections.Generic;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>`disease_action_axis.json` 的校验器 —— 9-DC 构建期校验。</summary>
    public static class DiseaseActionAxisValidator
    {
        // 9-DC 校验规则号
        public const int DC_1 = 1; // owner ∈ {"10", "11"}
        public const int DC_2 = 2; // polarity ∈ {causal, symptomatic}
        public const int DC_3 = 3; // action_id 唯一
        public const int DC_4 = 4; // treatable_by[].action ∈ 处置轴闭集
        public const int DC_5 = 5; // 病种 key 非空
        public const int DC_6 = 6; // 至少 1 个处置
        public const int DC_7 = 7; // 至少 1 个病种

        /// <summary>
        /// 校验处置轴 + treatable_by 关系 —— 9-DC 构建期校验。
        /// </summary>
        public static void Validate(List<ActionAxisRow> actions, List<TreatableByRow> treatableBy)
        {
            if (actions == null)
                throw new ArgumentNullException(nameof(actions));
            if (treatableBy == null)
                throw new ArgumentNullException(nameof(treatableBy));

            var errors = new List<string>();
            int firstRule = 0;   // 首个违规规则号(异常的 RuleNumber;消息聚合全部违规)

            // 9-DC-6: 至少 1 个处置
            if (actions.Count < 1)
            {
                if (firstRule == 0) firstRule = DC_6;
                errors.Add("9-DC-6 违反:至少需要 1 个处置");
            }

            // 9-DC-7: 至少 1 个病种
            if (treatableBy.Count < 1)
            {
                if (firstRule == 0) firstRule = DC_7;
                errors.Add("9-DC-7 违反:至少需要 1 个病种");
            }

            var actionIds = new HashSet<int>();

            foreach (var action in actions)
            {
                // 9-DC-3: action_id 唯一
                if (!actionIds.Add(action.ActionId))
                {
                    if (firstRule == 0) firstRule = DC_3;
                    errors.Add($"9-DC-3 违反:action_id 重复: {action.ActionId}");
                }
            }

            foreach (var row in treatableBy)
            {
                // 9-DC-5: 病种 key 非空
                if (string.IsNullOrEmpty(row.DiseaseKey))
                {
                    if (firstRule == 0) firstRule = DC_5;
                    errors.Add("9-DC-5 违反:病种 key 不能为空");
                }

                // 9-DC-4: treatable_by[].action ∈ 处置轴闭集
                if (!actionIds.Contains(row.ActionId))
                {
                    if (firstRule == 0) firstRule = DC_4;
                    errors.Add($"9-DC-4 违反:treatable_by[{row.DiseaseKey}].action = {row.ActionId} ∉ 处置轴闭集" +
                        $"({string.Join(", ", actionIds)})—— 9 的 treatable_by 引用到空动作 ⇒ " +
                        "该药任何病种都不对症,而 11 侧仍照发事件(静默失败)。");
                }
            }

            if (errors.Count > 0)
                throw new DiseaseActionAxisValidationException(firstRule, string.Join("; ", errors));
        }
    }

    /// <summary>
    /// 处置轴校验异常。
    /// </summary>
    public sealed class DiseaseActionAxisValidationException : Exception
    {
        public int RuleNumber { get; }

        public DiseaseActionAxisValidationException(int ruleNumber, string message)
            : base($"9-DC-{ruleNumber:D2}: {message}")
        {
            RuleNumber = ruleNumber;
        }
    }
}
