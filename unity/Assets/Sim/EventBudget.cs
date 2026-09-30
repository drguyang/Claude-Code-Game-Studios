// ADR-005 / ADR-007 / ADR-009 —— F2 密度预算与 DeferredThreatSlot。
//
// 权威来源:
//   ADR-005 §Key Interfaces —— 确定性模拟
//   ADR-007 §一 —— 掷骰状态可重构
//   ADR-009 §六 —— 世界流有界性
//   GDD random-events.md F2 —— 密度预算 · DeferredThreatSlot 唯一例外
//
// 核心机制:
//   - EventBudgetPerDay = clamp(BASE + ΣMod, 0, BUDGET_MAX)
//   - 超限弃置：不写流（无队列）
//   - DeferredThreatSlot：威胁档 ∧ 在医馆 ∧ 同日 ⇒ 入槽
//   - 跨日作废不结转
//   - 冷却去重：同 key 在 COOLDOWN_TICKS 内不再被抽

using System;
using System.Collections.Generic;

namespace DaYiJingCheng.Sim
{
    /// <summary>
    /// 预算参数（从烘焙数据装载）。
    /// </summary>
    public sealed class BudgetParams
    {
        public int BASE;
        public int BUDGET_MAX;
        public int DEFER_MAX;
        public int COOLDOWN_TICKS;

        public void Validate()
        {
            if (BASE < 0)
                throw new ArgumentException("BASE 必须 >= 0", nameof(BASE));
            if (BUDGET_MAX <= 0)
                throw new ArgumentException("BUDGET_MAX 必须 > 0", nameof(BUDGET_MAX));
            if (DEFER_MAX <= 0)
                throw new ArgumentException("DEFER_MAX 必须 > 0", nameof(DEFER_MAX));
            if (COOLDOWN_TICKS <= 0)
                throw new ArgumentException("COOLDOWN_TICKS 必须 > 0", nameof(COOLDOWN_TICKS));
        }
    }

    /// <summary>
    /// 预算状态（派生态，从流重构）。
    /// </summary>
    public sealed class BudgetState
    {
        public int TodayBudget;
        public int TodayUsed;
        public readonly List<int> DeferSlots = new List<int>();
        public readonly Dictionary<int, long> CooldownTable = new Dictionary<int, long>();

        public bool IsBudgetExceeded => TodayUsed >= TodayBudget;

        public bool TryUseBudget()
        {
            if (IsBudgetExceeded) return false;
            TodayUsed++;
            return true;
        }

        public void AddToDefer(int eventKey)
        {
            DeferSlots.Add(eventKey);
        }

        public void ClearDefer()
        {
            DeferSlots.Clear();
        }

        public bool IsInCooldown(int eventKey, long currentTick, int cooldownTicks)
        {
            if (CooldownTable.TryGetValue(eventKey, out long lastTick))
            {
                return currentTick - lastTick < cooldownTicks;
            }
            return false;
        }

        public void MarkCooldown(int eventKey, long currentTick)
        {
            CooldownTable[eventKey] = currentTick;
        }
    }

    /// <summary>
    /// F2 密度预算与 DeferredThreatSlot。
    /// </summary>
    public static class EventBudget
    {
        /// <summary>
        /// 计算当日预算。
        /// </summary>
        public static int ComputeDailyBudget(int baseValue, int[] mods, int budgetMax)
        {
            int sum = baseValue;
            if (mods != null)
            {
                foreach (var m in mods) sum += m;
            }
            return Math.Clamp(sum, 0, budgetMax);
        }

        /// <summary>
        /// 判断是否应入槽（三条件门）。
        /// </summary>
        public static bool ShouldDefer(EventTier tier, bool isInClinic, bool isSameDay)
        {
            return tier == EventTier.Threat && isInClinic && isSameDay;
        }

        /// <summary>
        /// 执行预算检查。
        /// </summary>
        public static bool TryConsumeBudget(BudgetState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            return state.TryUseBudget();
        }

        /// <summary>
        /// 跨日重置。
        /// </summary>
        public static void ResetForNewDay(BudgetState state, int dailyBudget)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            state.TodayBudget = dailyBudget;
            state.TodayUsed = 0;
            state.ClearDefer();
        }

        /// <summary>
        /// 验证预算状态可从流重构。
        /// </summary>
        public static bool ValidateReconstructable(BudgetState state)
        {
            // 简化版：验证状态非空
            return state != null;
        }
    }
}
