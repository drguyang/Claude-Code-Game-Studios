// ============================================================================
// 技能状态持久化与流重构 —— Story 008
// 权威来源: production/epics/skill-system/story-008-persistence-and-stream-rebuild.md
//   · AC-1~AC-6(从头重建 / 未升级哨兵 / 掉级 / round-trip / 折叠豁免 / 新玩家)
//   · ADR-010(7a 持久化,不独立快照,从流重构) · ADR-009(三流不折叠 SkillGrown)
//   · ADR-024(SkillGrown 已登记 stream: history)
//   · ADR-026 §七(折叠豁免扩一类 Kind,结清 OQ-7a-9)
// ============================================================================
// RebuildLevels 是纯函数:输入 SkillGrown 事件序列 → 输出 {actorId: {skillId: level}}。
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
    /// <para><b>多玩家(2026-10-23 用户裁定:改签名)</b> —— 返回形状按 <c>actorId</c>
    /// 分组。<see cref="SkillGrownPayload.ActorId"/> 是施予者(玩家),与 <c>player_id</c>
    /// 共用 id 空间(ADR-006 Amendment B);本项目 1–4 人联机从 P0 预留
    /// (<c>technical-preferences.md:31</c>)⇒ 两个玩家练同一技能**必须各自独立**。</para>
    /// </summary>
    public static class SkillStateRebuilder
    {
        /// <summary>
        /// 从 SkillGrown 事件序列重建**每个玩家**的各技能最新等级。
        /// </summary>
        /// <param name="events">SkillGrown 事件序列(**调用方须按 Tick 升序传入**,7a 读出顺序已保证)。</param>
        /// <returns>
        /// <c>actorId → (skillId → level)</c> 双层字典。
        /// 无历史或全部为哨兵 → 空字典。**某 actor 无任何有效成长 ⇒ 该 actor 不出现**在结果中
        /// (消费方按「查无 = 0 级」处理,与 AC-6 一致)。
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="events"/> 为 null。</exception>
        /// <remarks>
        /// <para><b>为什么按 actor 分组而不是单层 <c>skillId → level</c></b>(2026-10-23 裁定):
        /// 单层形状下 <c>result[e.SkillId] = e.Level</c> 完全不看 <see cref="SkillGrownPayload.ActorId"/>
        /// ⇒ 两个玩家练同一技能会**互相覆盖**,且「谁赢」纯由流序决定(与 actor 身份无关)。
        /// 这是**语义错误**,不是性能取舍。</para>
        /// <para>本形状与生产契约 <c>ISkillGrowthPort.QueryLevel(int actorId, int skillId)</c>
        /// 的参数序**对齐**(actor 在前)⇒ 消费方 <c>levels[actorId][skillId]</c> 可直接喂查询。</para>
        /// <para>⚠️ <b>输入顺序约定不变</b>:本函数**不检测**逆序(见下方 Remarks 的债项登记)。
        /// 「取最新」的语义依赖调用方按 Tick 升序传入 —— 7a 读出顺序已保证。</para>
        /// </remarks>
        public static Dictionary<int, Dictionary<int, int>> RebuildLevels(
            IEnumerable<SkillGrownPayload> events)
        {
            if (events == null) throw new ArgumentNullException(nameof(events));

            var result = new Dictionary<int, Dictionary<int, int>>();

            foreach (var e in events)
            {
                if (e.Level == SkillGrownPayload.LevelNotGrown) continue; // 哨兵 = 未升级,跳过

                if (!result.TryGetValue(e.ActorId, out Dictionary<int, int> bySkill))
                {
                    bySkill = new Dictionary<int, int>();
                    result[e.ActorId] = bySkill;
                }

                bySkill[e.SkillId] = e.Level;
            }

            return result;
        }
    }
}
