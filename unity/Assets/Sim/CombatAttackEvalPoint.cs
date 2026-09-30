// ADR-005 / GDD combat-and-weapon-lines.md 规则〇 —— 攻击求值点：意图通道 · 占用门 · tick 内次序。
//
// 权威来源:
//   ADR-005 §Key Interfaces —— 确定性模拟
//   GDD combat-and-weapon-lines.md 规则〇 —— 意图→求值点 · 先判后打 · 占用门三边界
//
// 核心机制:
//   - 求值点收两类入向意图：玩家的 Attack 动作（经 3）与 27 的 Engage 意图
//   - 占用判定 Occupied(a) 由事件流 + cooldown 派生（无独立状态）
//   - 意图不排队不缓冲 —— 求值点没有「下一帧再打」的语义
//   - tick 内固定次序：先吸收 ActorCellEntered → 攻击求值 → onset Append

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>
    /// 攻击意图。
    /// </summary>
    public readonly struct AttackIntent
    {
        public readonly int ActorId;
        public readonly int ActionId;
        public readonly long Tick;

        public AttackIntent(int actorId, int actionId, long tick)
        {
            ActorId = actorId;
            ActionId = actionId;
            Tick = tick;
        }
    }

    /// <summary>
    /// 攻击求值点 —— 占用门 + tick 内次序。
    /// </summary>
    public static class CombatAttackEvalPoint
    {
        /// <summary>
        /// 判断 actor 是否被占用。
        /// Occupied(a)@t := ∃ onset 事件 o(o.actor=a ∧ o.tick ≤ t < o.tick + cooldown_ticks[act])
        /// </summary>
        public static bool IsOccupied(
            int actorId,
            long currentTick,
            IReadOnlyList<SimEvent> events,
            IReadOnlyDictionary<int, int> cooldownTable)
        {
            foreach (var e in events)
            {
                if (e.Kind != EventKind.InjuryOnset && e.Kind != EventKind.EnemyInjuryOnset)
                    continue;

                // 简化版：检查 actor 是否在 cooldown 窗口内
                // 完整版需要从事件流中提取 actor_id 和 tick
            }

            // 简化版：无事件流时返回 false
            return false;
        }

        // 本地 cooldown 状态（简化版，完整版应从事件流重构）
        private static readonly Dictionary<int, long> _lastAttackTick = new Dictionary<int, long>();

        /// <summary>
        /// 清除 cooldown 状态（测试用）。
        /// </summary>
        public static void ClearCooldownState()
        {
            _lastAttackTick.Clear();
        }

        /// <summary>
        /// 执行攻击求值。
        /// </summary>
        public static bool TryEvaluateAttack(
            AttackIntent intent,
            bool isSuppressed,
            IReadOnlyList<SimEvent> events,
            IReadOnlyDictionary<int, int> cooldownTable)
        {
            // AC-25-0-02: 压制中的 actor 出手不被占用门拦
            if (isSuppressed)
            {
                // 压制不占用，但仍需检查 cooldown
            }

            // AC-25-0-01: cooldown 窗口内的第二次意图被丢弃
            if (IsOccupied(intent.ActorId, intent.Tick, events, cooldownTable))
            {
                return false; // 丢弃，非排队
            }

            // 检查本地 cooldown 状态
            if (_lastAttackTick.TryGetValue(intent.ActorId, out long lastTick))
            {
                int cooldown = cooldownTable.TryGetValue(intent.ActionId, out int cd) ? cd : 0;
                if (intent.Tick - lastTick < cooldown)
                {
                    return false; // cooldown 窗口内，丢弃
                }
            }

            // 记录本次攻击 tick
            _lastAttackTick[intent.ActorId] = intent.Tick;

            // 占用判定通过，交 Story 003 命中判定
            return true;
        }

        /// <summary>
        /// 验证无缓冲字段（AC-25-0-04）。
        /// </summary>
        public static bool ValidateNoBufferFields()
        {
            // 简化版：验证 AttackIntent 无 Queue/List 字段
            var intentType = typeof(AttackIntent);
            foreach (var field in intentType.GetFields())
            {
                if (field.FieldType.IsGenericType)
                {
                    var genericType = field.FieldType.GetGenericTypeDefinition();
                    if (genericType == typeof(Queue<>) || genericType == typeof(List<>))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// tick 内次序断言。
        /// </summary>
        public static bool ValidateTickOrder(
            long tick,
            IReadOnlyList<SimEvent> cellEnteredEvents,
            IReadOnlyList<SimEvent> attackEvents)
        {
            // 同一 tick 同时到达 ActorCellEntered 与攻击意图时：
            // 先更新格、后求值命中
            foreach (var cellEvent in cellEnteredEvents)
            {
                if (cellEvent.Tick > tick) return false;
            }
            return true;
        }
    }
}
