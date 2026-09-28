// ============================================================================
// 技能状态持久化与流重构 —— Story 008
// 权威来源: production/epics/skill-system/story-008-persistence-and-stream-rebuild.md
//   · AC-1~AC-6(从头重建 / 未升级哨兵 / 掉级 / round-trip / 折叠豁免 / 新玩家)
//   · ADR-010(7a 持久化,不独立快照,从流重构) · ADR-009(三流不折叠 SkillGrown)
//   · ADR-024(SkillGrown 已登记 stream: history)
//   · ADR-026 §七(折叠豁免扩一类 Kind,结清 OQ-7a-9)
// ============================================================================
// RebuildLevels 是纯函数:输入 SkillGrown 事件序列 → 输出 {skillId: level} 字典。
// 重建只看最新 Level(绝对等级),哨兵 LevelNotGrown(-1) 被忽略。
// 7a 负责从二进制存档读出 SkillGrown 序列,本函数不涉及 I/O。
// 输入顺序约定:调用方须按 Tick 升序传入(7a 读出顺序已保证)。
// ============================================================================

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts.SkillSystem;

namespace DaYiJingCheng.Sim.Contracts.SkillSystem
{
    /// <summary>
    /// 技能状态持久化与流重构。提供从 SkillGrown 事件序列重建技能等级的纯函数。
    /// <para>不独立快照 —— 技能等级始终从病史流的 SkillGrown 事件重建。</para>
    /// <para>折叠豁免 —— SkillGrown 行不随病例折叠被抹除(ADR-010 / ADR-026 §七)。</para>
    /// </summary>
    public static class SkillStateRebuilder
    {
        /// <summary>
        /// 从 SkillGrown 事件序列重建各技能最新等级。
        /// </summary>
        /// <param name="events">SkillGrown 事件序列(**调用方须按 Tick 升序传入**,7a 读出顺序已保证)。</param>
        /// <returns>skillId → level 字典。无历史或全部为哨兵 → 空字典。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="events"/> 为 null。</exception>
        public static Dictionary<int, int> RebuildLevels(IEnumerable<SkillGrownPayload> events)
        {
            if (events == null) throw new ArgumentNullException(nameof(events));

            var result = new Dictionary<int, int>();

            foreach (var e in events)
            {
                if (e.Level == SkillGrownPayload.LevelNotGrown) continue; // 哨兵 = 未升级,跳过

                result[e.SkillId] = e.Level;
            }

            return result;
        }
    }
}
