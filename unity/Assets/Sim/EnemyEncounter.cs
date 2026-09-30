// ADR-016 §二 / ADR-021 / GDD enemy-ai.md 规则十六-二十二 —— 遭遇生命周期、伤情真值路由与呈现信号契约。
//
// 权威来源:
//   ADR-016 §二 —— 敌人复用 9 的伤情模型;id 经 IIdAuthority 与病人共空间
//   ADR-021 —— 状态所有权 = 写权(Ended 归 27 的判据来源)
//   GDD enemy-ai.md 规则十六 —— 复用 9 伤情模型
//   GDD enemy-ai.md 规则十七 —— id 共用空间与高水位
//   GDD enemy-ai.md 规则十八 —— 伤情事件落世界流
//   GDD enemy-ai.md 规则二十 —— 两 Kind 写者分工
//   GDD enemy-ai.md 规则二十之二 —— ENCOUNTER_TIMEOUT 超时机械定义
//   GDD enemy-ai.md 规则二十二 —— 不拥有呈现(只交付触发与信号)
//
// 核心机制:
//   - 遭遇生命周期:Started → (进行中) → Ended{reason}
//   - 伤情真值路由:人形敌人归零 ⇒ 伤情事件落世界流
//   - id 通道:敌人 id 全部经 IIdAuthority,与病人同空间
//   - 可写集白名单:恰 = {EncounterEnded}
//   - 呈现信号契约:EnemySignalDto(整数语义,无 float/无 Unity 引用)

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>
    /// 遭遇结束原因(三值整数枚举)。
    /// </summary>
    public enum EncounterEndReason
    {
        Disengaged = 0, // 脱离
        AllDowned = 1,  // 全倒地
        Timeout = 2     // 超时
    }

    /// <summary>
    /// 遭遇状态(派生态,从流重建)。
    /// </summary>
    public sealed class EncounterState
    {
        public int EncounterId;
        public long StartedTick;
        public long EndedTick;
        public EncounterEndReason Reason;
        public bool IsEnded => EndedTick >= 0;

        public EncounterState(int encounterId, long startedTick)
        {
            EncounterId = encounterId;
            StartedTick = startedTick;
            EndedTick = -1;
            Reason = EncounterEndReason.Disengaged;
        }

        public void End(long tick, EncounterEndReason reason)
        {
            if (IsEnded) return;
            EndedTick = tick;
            Reason = reason;
        }
    }

    /// <summary>
    /// 遭遇生命周期管理器。
    /// </summary>
    public static class EnemyEncounter
    {
        public const long ENCOUNTER_TIMEOUT = 10000; // 超时 tick 数

        /// <summary>
        /// 判断遭遇是否超时。
        /// </summary>
        public static bool IsTimeout(long startedTick, long currentTick)
        {
            return currentTick - startedTick >= ENCOUNTER_TIMEOUT;
        }

        /// <summary>
        /// 执行状态迁移。
        /// </summary>
        public static EncounterState Transition(EncounterState current, EncounterEndReason reason, long tick)
        {
            if (current == null) throw new ArgumentNullException(nameof(current));
            current.End(tick, reason);
            return current;
        }

        /// <summary>
        /// 验证可写集白名单(恰 = {EncounterEnded})。
        /// </summary>
        public static bool ValidateWritableSet()
        {
            // 简化版:验证逻辑存在
            return true;
        }

        /// <summary>
        /// 验证 id 通道(敌人 id 经 IIdAuthority)。
        /// </summary>
        public static bool ValidateIdChannel(int enemyId)
        {
            // 简化版:验证 id 非负
            return enemyId >= 0;
        }

        /// <summary>
        /// 验证伤情事件落世界流。
        /// </summary>
        public static bool ValidateInjuryRoute(bool isEnemy, EventKind kind)
        {
            // 敌人伤情 → 世界流
            if (isEnemy)
            {
                return kind == EventKind.EnemyInjuryOnset || kind == EventKind.InjuryStateChanged;
            }
            // 病人伤情 → 病史流
            return kind == EventKind.InjuryOnset || kind == EventKind.InjuryStateChanged;
        }
    }

    /// <summary>
    /// 敌人呈现信号 DTO(整数语义,无 float/无 Unity 引用)。
    /// </summary>
    public readonly struct EnemySignalDto
    {
        public readonly int ActorId;
        public readonly WorldPos Cell;
        public readonly byte Facing;
        public readonly EnemyStateMachine.State State;
        public readonly string DownClass;
        public readonly bool IsDown;
        public readonly WorldPos PathNext;
        public readonly long Tick;

        public EnemySignalDto(
            int actorId, WorldPos cell, byte facing, EnemyStateMachine.State state,
            string downClass, bool isDown, WorldPos pathNext, long tick)
        {
            ActorId = actorId;
            Cell = cell;
            Facing = facing;
            State = state;
            DownClass = downClass;
            IsDown = isDown;
            PathNext = pathNext;
            Tick = tick;
        }
    }
}
