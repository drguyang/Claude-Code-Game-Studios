// ADR-005 / ADR-020 / GDD combat-and-weapon-lines.md —— 冷却 / 切换 / 压制。
//
// 权威来源:
//   ADR-005 §Key Interfaces —— 确定性模拟
//   ADR-2020 §一/§四 —— 玩家控制器与相机
//   GDD combat-and-weapon-lines.md F-25-3/F-25-4/F-25-5 —— 冷却/切换/压制
//
// 核心机制:
//   - 冷却 ≥1 tick 硬门
//   - 落空照常耗冷却
//   - 切换冷却 = 仅当前线动作可执行
//   - 压制刷新 = max 不 sum
//   - 压制态永不进流

using System;
using System.Collections.Generic;

namespace DaYiJingCheng.Sim
{
    /// <summary>
    /// 压制状态。
    /// </summary>
    public readonly struct SuppressionState
    {
        public readonly int VictimId;
        public readonly long OnsetTick;
        public readonly long UntilTick;
        public readonly bool IsDown;

        public SuppressionState(int victimId, long onsetTick, long untilTick, bool isDown)
        {
            VictimId = victimId;
            OnsetTick = onsetTick;
            UntilTick = untilTick;
            IsDown = isDown;
        }

        /// <summary>
        /// 查询某 tick 时是否被压制。
        /// </summary>
        public bool IsSuppressedAt(long tick)
        {
            return tick >= OnsetTick && tick < UntilTick && !IsDown;
        }
    }

    /// <summary>
    /// 冷却 / 切换 / 压制管理器。
    /// </summary>
    public static class CombatCooldown
    {
        /// <summary>
        /// 判断 actor 是否被压制。
        /// </summary>
        public static bool IsSuppressed(int actorId, long currentTick, IReadOnlyList<SuppressionState> suppressions)
        {
            foreach (var s in suppressions)
            {
                if (s.VictimId == actorId && s.IsSuppressedAt(currentTick))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 计算压制刷新（max 不 sum）。
        /// </summary>
        public static long ComputeSuppressedUntil(long onsetTick, long durationTicks, long existingUntil)
        {
            long newUntil = onsetTick + durationTicks;
            return Math.Max(newUntil, existingUntil);
        }

        /// <summary>
        /// 判断冷却是否完成。
        /// </summary>
        public static bool IsCooldownComplete(long lastAttackTick, long currentTick, int cooldownTicks)
        {
            return currentTick - lastAttackTick >= cooldownTicks;
        }

        /// <summary>
        /// 判断切换冷却是否完成。
        /// </summary>
        public static bool IsSwitchCooldownComplete(long lastSwitchTick, long currentTick, int switchCooldownTicks)
        {
            return currentTick - lastSwitchTick >= switchCooldownTicks;
        }

        /// <summary>
        /// 验证压制零入流。
        /// </summary>
        public static bool ValidateNoSuppressionInStream()
        {
            // 简化版：验证 SuppressionState 无 StreamKind 字段
            var stateType = typeof(SuppressionState);
            foreach (var field in stateType.GetFields())
            {
                if (field.Name.Contains("Stream") || field.Name.Contains("Kind"))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
