// 最小战斗闭环(2026-11-14)—— 玩家出手意图 → 一次命中 → 伤情 → 倒地 → 遭遇收场。
//
// 权威:
//   GDD combat-and-weapon-lines.md F-25-1 / F-25-2(判定 → magnitude)· 规则二(命中 → onset)
//   ADR-016 §二(敌人复用 9 的伤情模型;「生命归零 ⇒ INJ_COMA」)
//   ADR-009 §七(意图事件 + 主机当下判距 —— 本类即「当下判距」的落点)
//
// ═══ 一次出手 = 一条 onset(判序全在这里,且只有这里)══════════════════════════
//   ① 冷怯:距上次出手 < `cooldownTicks` ⇒ 不出(零事件);
//   ② 目标:场上**未倒地**且与玩家格 Chebyshev ≤ `range` 的敌人,取 `ActorId` 最小者
//      (F-27-6 的「最近格距 + 最低 actor_id 决胜」同口径);
//   ③ magnitude:`CombatMagnitude.ComputeMagnitude(CP, params)`(**CP 今日 = 0** ——
//      30 的格斗等级投影尚未接到战斗侧;P0 玩家 G-1 起点 = 0,故 magnitude = MAG_FLOOR);
//   ④ 伤情:`EnemyInjuryWriter` 落一条 `EnemyInjuryOnset`(INJ_BLUNT = 15,非致命);
//   ⑤ 倒地:重投影花名册,累计伤量 ≥ `StateMachine.COMA_THRESHOLD` ⇒ 经
//      `InjuryStateChangedWriter` 写一次 Coma(写者自己算,不在这里复制判序);
//   ⑥ 收场:该遭遇成员**全部**倒地 ⇒ `EncounterEndWriter` 写 `EncounterEnded(AllDowned)`。
//
// ⚠️ **敌人不还手**:27 的 Engage 出手意图需要 27 的 tick 循环(六态机 / 感知 / 寻路),
//   本节不接。⇒ 本轮敌人是**被动靶子**;「别人可以杀你」仍无机制路径(25 §九 第 7 项)。
// ⚠️ **玩家不可能打死敌人**:本类只写 `INJ_BLUNT`(非致命)+ 倒地态,**永不写 Deceased**
//   —— 承 25 §三 的「你的攻击不可能产生死亡状态」。
// ⚠️ **CP = 0 是事实不是选择**:`CombatPower.Compute(...)` 需要 `SkillTuningTable` 与
//   30 的等级投影;两者接到战斗侧之前,幅度退化为地板值。接上后只改本类一处。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>战斗驱动(玩家出手 → 敌人倒地)。</summary>
    public sealed class CombatDriver
    {
        /// <summary>`INJ_BLUNT` 的注册表 id(9 的伤情闭集第 15 行;非致命 + 自限)。</summary>
        /// <remarks>
        /// ⚠️ 这是一个**字面量**,由 `enemy_combat_test` 对照 `assets/data/disease_registry.json`
        /// 钉住(id 15 的 `key` 必须恰为 `INJ_BLUNT`)。注册表接入解析面后本常量迁出。
        /// </remarks>
        public const int InjuryBlunt = 15;

        /// <summary>默认冷却(10 tick = 0.5 s;手感值,归数值轮)。</summary>
        public const int DefaultCooldownTicks = 10;

        private readonly EnemyInjuryWriter _injury;
        private readonly InjuryStateChangedWriter _states;
        private readonly EncounterEndWriter _ends;
        private readonly Func<IReadOnlyList<SimEvent>> _eventsOf;
        private readonly IPayloadDecoder _decoder;
        private readonly Func<WorldPos> _playerCell;
        private readonly Func<long> _tickNow;
        private readonly int _actorId;
        private readonly int _range;
        private readonly int _cooldownTicks;
        private readonly MagnitudeParams _magnitudeParams;

        private long _nextAllowedTick = long.MinValue;

        /// <param name="injury">`EnemyInjuryOnset` 写者(25)。</param>
        /// <param name="states">`InjuryStateChanged` 写者(9)—— 倒地态的唯一写入口。</param>
        /// <param name="ends">`EncounterEnded` 写者(27)。</param>
        /// <param name="eventsOf">事件流只读视图。</param>
        /// <param name="decoder">载荷解码器。</param>
        /// <param name="playerCell">玩家经流确立格。</param>
        /// <param name="tickNow">当前逻辑 tick。</param>
        /// <param name="actorId">玩家 id(出手者)。</param>
        /// <param name="range">手长(Chebyshev 格;默认 1)。</param>
        /// <param name="cooldownTicks">出手冷却(tick;默认 <see cref="DefaultCooldownTicks"/>)。</param>
        /// <exception cref="ArgumentNullException">任一依赖为 null。</exception>
        public CombatDriver(EnemyInjuryWriter injury, InjuryStateChangedWriter states,
                            EncounterEndWriter ends, Func<IReadOnlyList<SimEvent>> eventsOf,
                            IPayloadDecoder decoder, Func<WorldPos> playerCell, Func<long> tickNow,
                            int actorId, int range = 1, int cooldownTicks = DefaultCooldownTicks)
        {
            _injury = injury ?? throw new ArgumentNullException(nameof(injury));
            _states = states ?? throw new ArgumentNullException(nameof(states));
            _ends = ends ?? throw new ArgumentNullException(nameof(ends));
            _eventsOf = eventsOf ?? throw new ArgumentNullException(nameof(eventsOf));
            _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
            _playerCell = playerCell ?? throw new ArgumentNullException(nameof(playerCell));
            _tickNow = tickNow ?? throw new ArgumentNullException(nameof(tickNow));
            if (actorId < 0) throw new ArgumentOutOfRangeException(nameof(actorId), actorId, "玩家 id 不得为负");
            if (range < 0) throw new ArgumentOutOfRangeException(nameof(range), range, "手长不得为负");
            if (cooldownTicks < 0) throw new ArgumentOutOfRangeException(nameof(cooldownTicks));
            _actorId = actorId; _range = range; _cooldownTicks = cooldownTicks;

            // ⚠️ CP = 0 ⇒ `term = base_step × 0 = 0` ⇒ 实测幅度 = MAG_FLOOR,**与 base_step 无关**。
            //    这里填的 base_step / CP_MAX 只是形状占位(接 30 的等级投影后才有意义);
            //    真正决定今日手感的是 `CombatActionSchema.MagFloor`(1/20),不是本行。
            _magnitudeParams = new MagnitudeParams(
                Fix.FromRational(1L, 4L), Fix.One, cooldownTicks);
        }

        /// <summary>最近一次出手结果(观测面;null = 还没出过手)。</summary>
        public CombatAttackResult? LastResult { get; private set; }

        /// <summary>最近一次出手命中的目标 id(-1 = 未命中)。</summary>
        public int LastTargetId { get; private set; } = -1;

        /// <summary>一次出手(按下沿由调用方边沿化)。</summary>
        /// <returns>结果码;只有 <see cref="CombatAttackResult.Hit"/> 会写事件。</returns>
        public CombatAttackResult Attack()
        {
            long tick = _tickNow();
            if (tick < _nextAllowedTick)
            {
                LastResult = CombatAttackResult.OnCooldown;
                return CombatAttackResult.OnCooldown;
            }

            WorldPos cell = _playerCell();
            IReadOnlyList<EnemyActor> roster = EnemyRoster.Build(_eventsOf(), _decoder);
            EnemyActor? target = null;
            for (int i = 0; i < roster.Count; i++)
            {
                EnemyActor e = roster[i];
                if (e.Downed) continue;
                if (Chebyshev(cell, e.Cell) > _range) continue;
                if (target == null || e.ActorId < target.Value.ActorId) target = e;
            }
            if (target == null)
            {
                LastResult = CombatAttackResult.NoTarget;
                return CombatAttackResult.NoTarget;
            }

            // ③ magnitude(CP = 0 ⇒ MAG_FLOOR)
            Fix magnitude = CombatMagnitude.ComputeMagnitude(Fix.Zero, _magnitudeParams);

            // ④ onset
            _injury.TryWrite(_actorId, target.Value.ActorId, InjuryBlunt, magnitude, tick);
            _nextAllowedTick = tick + _cooldownTicks;
            LastTargetId = target.Value.ActorId;

            // ⑤ 倒地:重投影后按**写者自己**判序落 Coma
            IReadOnlyList<EnemyActor> after = EnemyRoster.Build(_eventsOf(), _decoder);
            EnemyActor hit = default;
            bool found = false;
            for (int i = 0; i < after.Count; i++)
                if (after[i].ActorId == target.Value.ActorId) { hit = after[i]; found = true; break; }

            if (found && !hit.Downed && hit.Hurt.Raw >= StateMachine.COMA_THRESHOLD)
            {
                _states.TryWrite(tick, hit.ActorId, InjuryState.Healthy, hit.Hurt, -1L, string.Empty);
                after = EnemyRoster.Build(_eventsOf(), _decoder);
            }

            // ⑥ 收场:该遭遇成员全部倒地
            if (found && AllDowned(after, hit.EncounterId))
                _ends.TryEnd(hit.EncounterId, EncounterEndReasonKind.AllDowned, tick);

            LastResult = CombatAttackResult.Hit;
            return CombatAttackResult.Hit;
        }

        private static bool AllDowned(IReadOnlyList<EnemyActor> roster, int encounterId)
        {
            bool any = false;
            for (int i = 0; i < roster.Count; i++)
            {
                EnemyActor e = roster[i];
                if (e.EncounterId != encounterId) continue;
                any = true;
                if (!e.Downed) return false;
            }
            return any;
        }

        private static long Chebyshev(in WorldPos a, in WorldPos b)
        {
            long dx = Math.Abs((long)a.X - b.X);
            long dy = Math.Abs((long)a.Y - b.Y);
            long dz = Math.Abs((long)a.Z - b.Z);
            long m = dx > dy ? dx : dy;
            return m > dz ? m : dz;
        }
    }

    /// <summary>一次出手的结果码。</summary>
    public enum CombatAttackResult
    {
        /// <summary>命中(已写 onset;可能同时写倒地 / 收场)。</summary>
        Hit = 0,
        /// <summary>手长内没有可打的敌人 ⇒ 零事件。</summary>
        NoTarget = 1,
        /// <summary>冷却未到 ⇒ 零事件。</summary>
        OnCooldown = 2,
    }
}
