// ADR-015 / ADR-016 / GDD combat-and-weapon-lines.md —— 命中判定 F-25-1。
//
// 权威来源:
//   ADR-015 §三 —— 单一整数格
//   ADR-016 §三 —— 感知输入 = 粗粒度整数格
//   GDD combat-and-weapon-lines.md F-25-1 —— 五合取项
//
// 核心机制:
//   - d2 ≤ RANGE² 命中（整数边界）
//   - 友伤白名单
//   - 解锁判定
//   - Down 查询
//   - 判定输入 = 本 tick 已吸收 ActorCellEntered 后的格快照

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>
    /// 命中判定输入。
    /// </summary>
    public readonly struct HitDeterminationInput
    {
        public readonly int ActorId;
        public readonly int TargetId;
        public readonly WorldPos ActorCell;
        public readonly WorldPos TargetCell;
        public readonly int RangeCells;
        public readonly int UnlockLevel;
        public readonly int CombatSkillLevel;
        public readonly bool IsTargetFriendly;
        public readonly bool IsActorDown;
        public readonly bool IsTargetDown;

        public HitDeterminationInput(
            int actorId, int targetId,
            WorldPos actorCell, WorldPos targetCell,
            int rangeCells, int unlockLevel, int combatSkillLevel,
            bool isTargetFriendly, bool isActorDown, bool isTargetDown)
        {
            ActorId = actorId;
            TargetId = targetId;
            ActorCell = actorCell;
            TargetCell = targetCell;
            RangeCells = rangeCells;
            UnlockLevel = unlockLevel;
            CombatSkillLevel = combatSkillLevel;
            IsTargetFriendly = isTargetFriendly;
            IsActorDown = isActorDown;
            IsTargetDown = isTargetDown;
        }
    }

    /// <summary>
    /// 命中判定结果。
    /// </summary>
    public readonly struct HitDeterminationResult
    {
        public readonly bool IsHit;
        public readonly bool IsUnlocked;
        public readonly bool IsInRange;
        public readonly bool IsTargetAllowed;
        public readonly bool IsActorNotDown;
        public readonly bool IsTargetNotDown;

        public HitDeterminationResult(bool isHit, bool isUnlocked, bool isInRange, bool isTargetAllowed, bool isActorNotDown, bool isTargetNotDown)
        {
            IsHit = isHit;
            IsUnlocked = isUnlocked;
            IsInRange = isInRange;
            IsTargetAllowed = isTargetAllowed;
            IsActorNotDown = isActorNotDown;
            IsTargetNotDown = isTargetNotDown;
        }
    }

    /// <summary>
    /// 命中判定 F-25-1 —— 五合取项。
    /// </summary>
    public static class CombatHitResolution
    {
        /// <summary>
        /// 计算整数格距离平方。
        /// </summary>
        public static long ComputeD2(WorldPos a, WorldPos b)
        {
            long dx = a.X - b.X;
            long dy = a.Y - b.Y;
            long dz = a.Z - b.Z;
            return dx * dx + dy * dy + dz * dz;
        }

        /// <summary>
        /// 执行命中判定。
        /// </summary>
        public static HitDeterminationResult Determine(HitDeterminationInput input)
        {
            // ① 解锁判定
            bool isUnlocked = input.CombatSkillLevel >= input.UnlockLevel;

            // ② 距离判定
            long d2 = ComputeD2(input.ActorCell, input.TargetCell);
            long range2 = (long)input.RangeCells * input.RangeCells;
            bool isInRange = d2 <= range2;

            // ③ 友伤白名单
            bool isTargetAllowed = !input.IsTargetFriendly;

            // ④ Down 查询
            bool isActorNotDown = !input.IsActorDown;
            bool isTargetNotDown = !input.IsTargetDown;

            // 五合取项
            bool isHit = isUnlocked && isInRange && isTargetAllowed && isActorNotDown && isTargetNotDown;

            return new HitDeterminationResult(isHit, isUnlocked, isInRange, isTargetAllowed, isActorNotDown, isTargetNotDown);
        }

        /// <summary>
        /// 验证判定输入类型 ∈ 整数域。
        /// </summary>
        public static bool ValidateIntegerDomain()
        {
            // 简化版：验证 HitDeterminationInput 无 float/double 字段
            var inputType = typeof(HitDeterminationInput);
            foreach (var field in inputType.GetFields())
            {
                if (field.FieldType == typeof(float) || field.FieldType == typeof(double))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
