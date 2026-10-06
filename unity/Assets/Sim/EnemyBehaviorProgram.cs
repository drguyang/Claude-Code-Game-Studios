// ADR-016 / GDD enemy-ai.md —— 行为程序载体与整数决策器。
//
// 权威来源:
//   ADR-016 §四 —— 行为载体 = 版本化烘焙数据
//   ADR-016 2026-09-17 修订 —— P0 手写 ai_enemy.json，零可视化工具
//   GDD enemy-ai.md 规则六 —— 单套程序 × 两类参数行
//   GDD enemy-ai.md 规则八 —— 禁 float
//
// 核心机制:
//   - 每参数行恰有 entity_kind / down_class / morale_enabled / flank_enabled / target_policy / default_attack
//   - Fix 字段 JSON 写字符串 → FixParse
//   - 决策器读表为纯整数/定点求值

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>
    /// 行为程序参数行（对应 ai_enemy.json 的一行）。
    /// </summary>
    public sealed class EnemyBehaviorRow
    {
        public int Id;
        public string EntityKind;
        public string DownClass;
        public bool MoraleEnabled;
        public bool FlankEnabled;
        public string TargetPolicy;
        public int DefaultAttack;
        public Fix Speed;
        public int RVis;
        public int RAlert;
        public int RChase;
        public int RContact;
    }

    /// <summary>
    /// 行为程序 schema 校验器。
    /// </summary>
    public static class EnemyBehaviorSchema
    {
        /// <summary>
        /// 校验参数行。
        /// </summary>
        public static void Validate(EnemyBehaviorRow row)
        {
            if (row == null)
                throw new ArgumentNullException(nameof(row));

            if (string.IsNullOrEmpty(row.EntityKind))
                throw new EnemyBehaviorValidationException("EntityKind 不能为空", row.Id);

            if (string.IsNullOrEmpty(row.DownClass))
                throw new EnemyBehaviorValidationException("DownClass 不能为空", row.Id);

            if (string.IsNullOrEmpty(row.TargetPolicy))
                throw new EnemyBehaviorValidationException("TargetPolicy 不能为空", row.Id);

            // target_policy ∈ {LowestVitality, LastAttacker}
            if (row.TargetPolicy != "LowestVitality" && row.TargetPolicy != "LastAttacker")
                throw new EnemyBehaviorValidationException(
                    $"TargetPolicy 不在闭集内: {row.TargetPolicy}", row.Id);

            // R_CONTACT ≤ R_CHASE ≤ R_ALERT ≤ R_VIS(GDD F-27-1 关系式)
            // ⚠️ 原稿把三条全部写反(R_VIS ≤ R_ALERT ≤ R_CHASE ≤ R_CONTACT),而测试与 fixture
            //    一同验证了反向 ⇒ 三重自洽对一份 GDD。反方向不自洽:Alert 的进入条件是
            //    `Visible ∧ d2 ≤ R_ALERT²`,而 `Visible ⟺ d2 ≤ R_VIS²`;若 R_ALERT > R_VIS,
            //    存在 `d2 > R_VIS² ∧ d2 ≤ R_ALERT²` 的格 ⇒ 看不见却停下转头。
            if (row.RContact > row.RChase)
                throw new EnemyBehaviorValidationException(
                    $"R_CONTACT ({row.RContact}) > R_CHASE ({row.RChase})", row.Id);
            if (row.RChase > row.RAlert)
                throw new EnemyBehaviorValidationException(
                    $"R_CHASE ({row.RChase}) > R_ALERT ({row.RAlert})", row.Id);
            if (row.RAlert > row.RVis)
                throw new EnemyBehaviorValidationException(
                    $"R_ALERT ({row.RAlert}) > R_VIS ({row.RVis})", row.Id);
        }

        /// <summary>
        /// 校验参数行集合。
        /// </summary>
        public static void ValidateAll(IEnumerable<EnemyBehaviorRow> rows)
        {
            if (rows == null)
                throw new ArgumentNullException(nameof(rows));

            var ids = new HashSet<int>();
            foreach (var row in rows)
            {
                Validate(row);
                if (!ids.Add(row.Id))
                    throw new EnemyBehaviorValidationException($"重复 Id: {row.Id}", row.Id);
            }
        }

        /// <summary>
        /// 验证 Fix 字段为字符串（构建期）。
        /// </summary>
        public static Fix ParseFixField(string fieldName, string value, int rowId)
        {
            try
            {
                return FixParse.Parse(value);
            }
            catch (Exception ex)
            {
                throw new EnemyBehaviorValidationException(
                    $"Fix 字段 {fieldName} 解析失败: {ex.Message}", rowId);
            }
        }
    }

    /// <summary>
    /// 敌人行为校验异常。
    /// </summary>
    public sealed class EnemyBehaviorValidationException : Exception
    {
        public int RowId { get; }

        public EnemyBehaviorValidationException(string message, int rowId)
            : base(message)
        {
            RowId = rowId;
        }
    }

    /// <summary>
    /// 整数决策器（读表为纯整数/定点求值）。
    /// </summary>
    public static class EnemyDecisionEvaluator
    {
        /// <summary>
        /// 评估目标选择。
        /// </summary>
        public static int EvaluateTargetSelection(
            EnemyBehaviorRow row,
            IReadOnlyList<(int id, Fix vitality)> candidates,
            int lastAttackerId)
        {
            if (candidates == null || candidates.Count == 0)
                return -1;

            switch (row.TargetPolicy)
            {
                case "LowestVitality":
                    int lowestId = -1;
                    long lowestVitalityRaw = long.MaxValue;
                    foreach (var c in candidates)
                    {
                        if (c.vitality.Raw < lowestVitalityRaw)
                        {
                            lowestVitalityRaw = c.vitality.Raw;
                            lowestId = c.id;
                        }
                    }
                    return lowestId;

                case "LastAttacker":
                    return lastAttackerId;

                default:
                    throw new EnemyBehaviorValidationException(
                        $"未知 TargetPolicy: {row.TargetPolicy}", row.Id);
            }
        }

        /// <summary>
        /// 评估是否在感知范围内。
        /// </summary>
        public static bool IsInRange(EnemyBehaviorRow row, long d2)
        {
            return d2 <= (long)row.RVis * row.RVis;
        }

        /// <summary>
        /// 评估是否在追击范围内。
        /// </summary>
        public static bool IsInChaseRange(EnemyBehaviorRow row, long d2)
        {
            return d2 <= (long)row.RChase * row.RChase;
        }

        /// <summary>
        /// 评估士气。
        /// </summary>
        public static Fix EvaluateMorale(EnemyBehaviorRow row, Fix currentMorale, bool isInCombat)
        {
            if (!row.MoraleEnabled)
                return Fix.Zero;

            // 简化版：战斗中士气下降
            if (isInCombat)
            {
                Fix result = currentMorale - new Fix(100);
                return result.Raw > 0 ? result : Fix.Zero;
            }
            return currentMorale;
        }
    }
}
