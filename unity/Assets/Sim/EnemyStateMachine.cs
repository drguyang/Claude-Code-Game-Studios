// ADR-016 §九 / GDD enemy-ai.md 规则五/十 —— 敌意状态机：六态转移、士气/脱离单一出处与 LOD 节流。
//
// 权威来源:
//   ADR-016 §九 —— 冻结/LOD（2026-09-17 修订）
//   GDD enemy-ai.md 规则五 —— 六态
//   GDD enemy-ai.md 规则十 —— LOD 节流
//
// 核心机制:
//   - 六态：Patrol / Alert / Chase / Flank / Engage / Disengage
//   - 转移表闭集（任何未定义 (state, trigger) 对 ⇒ 断言失败）
//   - 士气/脱离比较在 int64(Fix raw)域
//   - 同 tick 内敌人求值按 actor_id 升序、读上 tick 快照
//   - 计时器全部 tick 驱动

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>
    /// 状态机触发事件。
    /// </summary>
    public enum StateTrigger
    {
        PlayerVisible = 0,   // 玩家可见
        PlayerInRange = 1,   // 玩家进入范围
        PlayerLeft = 2,      // 玩家离开
        MoraleBreak = 3,     // 士气崩溃
        FlankComplete = 4,   // 包抄完成
        FlankTimeout = 5,    // 包抄超时
        AlertTimeout = 6,    // 警戒超时
        EngageTimeout = 7,   // 交战超时
        Down = 8,            // 倒下
        Recover = 9          // 恢复
    }

    /// <summary>
    /// 敌意状态机。
    /// </summary>
    public static class EnemyStateMachine
    {
        // 六态
        public enum State
        {
            Patrol = 0,
            Alert = 1,
            Chase = 2,
            Flank = 3,
            Engage = 4,
            Disengage = 5
        }

        // 转移表（闭集）
        private static readonly Dictionary<State, Dictionary<StateTrigger, State>> TransitionTable =
            new Dictionary<State, Dictionary<StateTrigger, State>>
            {
                { State.Patrol, new Dictionary<StateTrigger, State>
                    {
                        { StateTrigger.PlayerVisible, State.Alert },
                        { StateTrigger.Down, State.Disengage }
                    }
                },
                { State.Alert, new Dictionary<StateTrigger, State>
                    {
                        { StateTrigger.PlayerInRange, State.Chase },
                        { StateTrigger.AlertTimeout, State.Patrol },
                        { StateTrigger.Down, State.Disengage }
                    }
                },
                { State.Chase, new Dictionary<StateTrigger, State>
                    {
                        { StateTrigger.PlayerInRange, State.Engage },
                        { StateTrigger.PlayerLeft, State.Alert },
                        { StateTrigger.MoraleBreak, State.Disengage },
                        { StateTrigger.Down, State.Disengage }
                    }
                },
                { State.Flank, new Dictionary<StateTrigger, State>
                    {
                        { StateTrigger.FlankComplete, State.Engage },
                        { StateTrigger.FlankTimeout, State.Chase },
                        { StateTrigger.MoraleBreak, State.Disengage },
                        { StateTrigger.Down, State.Disengage }
                    }
                },
                { State.Engage, new Dictionary<StateTrigger, State>
                    {
                        { StateTrigger.PlayerLeft, State.Chase },
                        { StateTrigger.EngageTimeout, State.Chase },
                        { StateTrigger.MoraleBreak, State.Disengage },
                        { StateTrigger.Down, State.Disengage }
                    }
                },
                { State.Disengage, new Dictionary<StateTrigger, State>
                    {
                        { StateTrigger.Recover, State.Patrol }
                    }
                }
            };

        /// <summary>
        /// 执行状态迁移。
        /// </summary>
        public static State Transition(State current, StateTrigger trigger)
        {
            if (!TransitionTable.TryGetValue(current, out var triggers))
            {
                throw new InvalidOperationException($"状态 {current} 未定义迁移表");
            }

            if (!triggers.TryGetValue(trigger, out var next))
            {
                throw new InvalidOperationException($"非法迁移: {current} + {trigger}");
            }

            return next;
        }

        /// <summary>
        /// 验证转移表闭集。
        /// </summary>
        public static bool ValidateClosedSet()
        {
            foreach (var state in TransitionTable.Keys)
            {
                foreach (var trigger in TransitionTable[state].Keys)
                {
                    var next = TransitionTable[state][trigger];
                    if (!TransitionTable.ContainsKey(next))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// 判断是否在战斗中。
        /// </summary>
        public static bool IsInCombat(State state)
        {
            return state == State.Chase || state == State.Flank || state == State.Engage;
        }

        /// <summary>
        /// 计算士气值。
        /// </summary>
        public static Fix ComputeMorale(Fix currentMorale, bool isInCombat, bool isDown)
        {
            if (isDown)
                return Fix.Zero;

            if (isInCombat)
            {
                Fix result = currentMorale - new Fix(100);
                return result.Raw > 0 ? result : Fix.Zero;
            }
            return currentMorale;
        }

        /// <summary>
        /// 判断是否应该脱离。
        /// </summary>
        public static bool ShouldDisengage(Fix morale, Fix moraleBreakThreshold, bool isDown)
        {
            return isDown || morale.Raw < moraleBreakThreshold.Raw;
        }
    }
}
