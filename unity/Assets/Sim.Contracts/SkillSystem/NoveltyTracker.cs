// ============================================================================
// 新颖度追踪 —— 按 actor_id 独立维护 (skill_id, object_id) 冷却字典
// 权威来源: production/epics/skill-system/story-006-novelty-tracking.md
//   · AC-1 ~ AC-6(首次/冷却内/冷却外/actor 独立/联机不共享/对象粒度)
//   · ADR-026 §Decision 四(调参表 NOVELTY_COOLDOWN) + Implementation Guidelines
//   · ADR-005 确定性 sim(纯函数查询,无随机)
//   · ADR-009 §四(派生态重建三源不变量 —— lastSeenTick 来自 tick 序列,非墙钟)
// ============================================================================
// 全路径整数求值。lastSeenTick = tick 序号(非墙钟)。冷却期 = tick 数比较。
// 新颖度字典按 actor_id 独立,不进三流,不跨网络同步(联机各玩家各自维护)。
// 注意:NoveltyClass 枚举定义在 SkillRegistry.cs,本文件不再重复定义。
// ============================================================================

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.Contracts.SkillSystem
{
    /// <summary>
    /// 新颖度追踪纯服务。
    /// <para>按 actor_id 独立维护 (skill_id, object_id) → lastSeenTick 字典。</para>
    /// <para>冷却期判定 = 当前 tick - lastSeenTick ≥ NOVELTY_COOLDOWN(tick 数,非墙钟)。</para>
    /// <para>新颖度不进三流,不进存档,不跨网络同步 —— 纯运行期派生态(ADR-009)。</para>
    /// </summary>
    public sealed class NoveltyTracker
    {
        // ── 内部字典 ────────────────────────────────────────────────────────
        // 键 = (actorId, skillId, objectId) 三元组,值 = lastSeenTick
        // 使用 struct 键避免 GC 分配(NoveltyKey 是值类型)
        // 调用方保证 actorId / skillId / objectId ≥ 0

        private readonly Dictionary<NoveltyKey, int> _lastSeenTicks;
        private readonly int _cooldownTicks;

        /// <summary>新颖度冷却期(tick 数)。默认 24000 = 20 min @ 20 Hz。</summary>
        public int CooldownTicks => _cooldownTicks;

        // ── 构造器 ──────────────────────────────────────────────────────────

        /// <summary>构造新颖度追踪器。</summary>
        /// <param name="cooldownTicks">冷却期 tick 数(如 24000 = 20 min @ 20 Hz)。</param>
        public NoveltyTracker(int cooldownTicks)
        {
            if (cooldownTicks < 0)
                throw new ArgumentOutOfRangeException(nameof(cooldownTicks),
                    $"cooldownTicks={cooldownTicks} 不能为负");
            _cooldownTicks = cooldownTicks;
            _lastSeenTicks = new Dictionary<NoveltyKey, int>();
        }

        /// <summary>用默认冷却期(24000 tick)构造。</summary>
        public NoveltyTracker() : this(cooldownTicks: 24000) { }

        // ── 核心查询 ────────────────────────────────────────────────────────

        /// <summary>查询新颖度类别。
        /// 副作用:首次遇见或冷却期外会记录/更新 lastSeenTick;冷却期内不更新。
        /// </summary>
        /// <param name="actorId">玩家/实体 id(按 actor 独立追踪)。</param>
        /// <param name="skillId">技能 id。</param>
        /// <param name="objectId">对象 id(由调用方决定语义:病名/药材/对手类型)。</param>
        /// <param name="currentTick">当前 tick(来自 ITickProvider,非墙钟)。</param>
        /// <returns>NoveltyClass(First / Stale / Normal)。</returns>
        public NoveltyClass Query(int actorId, int skillId, int objectId, int currentTick)
        {
            if (actorId < 0)
                throw new ArgumentOutOfRangeException(nameof(actorId),
                    $"actorId={actorId} 不能为负");
            if (skillId < 0)
                throw new ArgumentOutOfRangeException(nameof(skillId),
                    $"skillId={skillId} 不能为负");
            if (objectId < 0)
                throw new ArgumentOutOfRangeException(nameof(objectId),
                    $"objectId={objectId} 不能为负");
            if (currentTick < 0)
                throw new ArgumentOutOfRangeException(nameof(currentTick),
                    $"currentTick={currentTick} 不能为负");

            var key = new NoveltyKey(actorId, skillId, objectId);

            if (!_lastSeenTicks.TryGetValue(key, out int lastSeenTick))
            {
                // 字典未命中 → First(首次遇见)
                _lastSeenTicks[key] = currentTick;
                return NoveltyClass.First;
            }

            int elapsed = currentTick - lastSeenTick;
            if (elapsed < 0)
                throw new ArgumentOutOfRangeException(nameof(currentTick),
                    $"currentTick={currentTick} 不能小于 lastSeenTick={lastSeenTick}(tick 必须单调递增)");
            if (elapsed >= _cooldownTicks)
            {
                // 冷却期外 → Normal,更新 lastSeenTick
                _lastSeenTicks[key] = currentTick;
                return NoveltyClass.Normal;
            }

            // 冷却期内 → Stale,不更新 lastSeenTick(冷却锁定)
            return NoveltyClass.Stale;
        }

        // ── 查询接口(只读,无副作用) ─────────────────────────────────────────

        /// <summary>只读查询:返回新颖度类别,不修改字典。</summary>
        public NoveltyClass Peek(int actorId, int skillId, int objectId, int currentTick)
        {
            if (actorId < 0) throw new ArgumentOutOfRangeException(nameof(actorId));
            if (skillId < 0) throw new ArgumentOutOfRangeException(nameof(skillId));
            if (objectId < 0) throw new ArgumentOutOfRangeException(nameof(objectId));
            if (currentTick < 0) throw new ArgumentOutOfRangeException(nameof(currentTick));

            var key = new NoveltyKey(actorId, skillId, objectId);

            if (!_lastSeenTicks.TryGetValue(key, out int lastSeenTick))
                return NoveltyClass.First;

            int elapsed = currentTick - lastSeenTick;
            if (elapsed < 0)
                throw new ArgumentOutOfRangeException(nameof(currentTick),
                    $"currentTick={currentTick} 不能小于 lastSeenTick={lastSeenTick}(tick 必须单调递增)");
            return elapsed >= _cooldownTicks ? NoveltyClass.Normal : NoveltyClass.Stale;
        }

        /// <summary>强制重置 (actorId, skillId, objectId) 的 lastSeenTick。</summary>
        public void Reset(int actorId, int skillId, int objectId, int newTick)
        {
            if (actorId < 0) throw new ArgumentOutOfRangeException(nameof(actorId));
            if (skillId < 0) throw new ArgumentOutOfRangeException(nameof(skillId));
            if (objectId < 0) throw new ArgumentOutOfRangeException(nameof(objectId));
            if (newTick < 0) throw new ArgumentOutOfRangeException(nameof(newTick));

            var key = new NoveltyKey(actorId, skillId, objectId);
            _lastSeenTicks[key] = newTick;
        }

        /// <summary>移除 (actorId, skillId, objectId) 条目(如角色销毁)。</summary>
        public bool Remove(int actorId, int skillId, int objectId)
        {
            if (actorId < 0) throw new ArgumentOutOfRangeException(nameof(actorId));
            if (skillId < 0) throw new ArgumentOutOfRangeException(nameof(skillId));
            if (objectId < 0) throw new ArgumentOutOfRangeException(nameof(objectId));

            return _lastSeenTicks.Remove(new NoveltyKey(actorId, skillId, objectId));
        }

        /// <summary>清空全部新颖度记录(如玩家重生/读档)。</summary>
        public void Clear() => _lastSeenTicks.Clear();

        /// <summary>当前字典条目数(调试/测试用)。</summary>
        public int Count => _lastSeenTicks.Count;
    }

    // ── 字典键(值类型,零 GC) ─────────────────────────────────────────────

    /// <summary>新颖度字典键 (actorId, skillId, objectId) —— 值类型,无 GC。</summary>
    internal readonly struct NoveltyKey : IEquatable<NoveltyKey>
    {
        public readonly int ActorId;
        public readonly int SkillId;
        public readonly int ObjectId;

        public NoveltyKey(int actorId, int skillId, int objectId)
        {
            ActorId = actorId;
            SkillId = skillId;
            ObjectId = objectId;
        }

        public bool Equals(NoveltyKey other) =>
            ActorId == other.ActorId && SkillId == other.SkillId && ObjectId == other.ObjectId;

        public override bool Equals(object obj) => obj is NoveltyKey other && Equals(other);

        public override int GetHashCode()
        {
            // 简单哈希组合:三个 int 异或+移位(确定性,跨平台一致)
            unchecked
            {
                int hash = ActorId;
                hash = (hash * 31) ^ SkillId;
                hash = (hash * 31) ^ ObjectId;
                return hash;
            }
        }

        public static bool operator ==(NoveltyKey left, NoveltyKey right) => left.Equals(right);
        public static bool operator !=(NoveltyKey left, NoveltyKey right) => !left.Equals(right);
    }
}
