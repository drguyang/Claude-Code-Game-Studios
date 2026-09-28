// ============================================================================
// SkillGrown 事件发射器 —— Story 007
// 权威来源: production/epics/skill-system/story-007-skillgrown-event-emit.md
//   · AC-1 ~ AC-6(EmitGrowth / 载荷结构 / Level 哨兵 / patient_id / 全序键 / 无 disease_id)
//   · ADR-007 §一(主机唯一 Append) · ADR-009 §三(三流全序键)
//   · ADR-024 SkillGrown 已在 entities.yaml 具名登记(stream: history, author: 30)
//   · ADR-005 确定性 sim(纯函数构造,无随机)
//   · ADR-025 Sim.Contracts 引用集恰 = BCL,不可引 Sim.Codec
// ============================================================================
// EmitGrowth 不直接调用 IEventSink.Append —— 它构造 SkillGrownPayload 交调用方(主机),
// 由主机负责:① PayloadCodec.Encode 编码 → blob 池;② 构造 SimEvent + 填充 PayloadRef;
// ③ Append 入病史流。30 不监听事件流,不主动触发成长,只提供发射接口。
// ============================================================================

using System;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.Contracts.SkillSystem
{
    /// <summary>
    /// SkillGrown 事件发射器。唯一对外入口 = <see cref="EmitGrowth"/>,
    /// 返回 <see cref="SkillGrownPayload"/>,由调用方(主机)编码 + 构造 SimEvent + Append。
    /// <para>载荷结构 = (actor_id, patient_id, skill_id, object_id, novelty_class, Level)。</para>
    /// <para>Level: 升级时 = 新等级(绝对等级); 未升级时 = 哨兵 -1(非 0)。</para>
    /// <para>patient_id: 无病例语境 = PatientId.None(-1); 8 诊断传入具体病例 id。</para>
    /// </summary>
    public static class SkillGrownEmitter
    {
        /// <summary>构造 SkillGrown 载荷(不编码、不构造 SimEvent、不 Append,由调用方完成)。</summary>
        /// <param name="actorId">玩家/实体 id。</param>
        /// <param name="skillId">技能 id(SkillId ordinal)。</param>
        /// <param name="objectId">对象 id(病种/品种/对手类型 ordinal)。</param>
        /// <param name="novelty">新颖度类别。</param>
        /// <param name="level">升级时 = 新等级; 未升级 = null(写入哨兵 -1)。</param>
        /// <param name="currentTick">当前 tick(来自 ITickProvider)。</param>
        /// <param name="patientId">成长所依附的受伤实体 id; 无病例语境 = PatientId.None。</param>
        /// <returns>SkillGrownPayload 结构体(调用方负责 PayloadCodec.Encode + SimEvent 构造 + Append)。</returns>
        /// <exception cref="ArgumentOutOfRangeException">任何 id 为负或 novelty 不在有效范围。</exception>
        public static SkillGrownPayload EmitGrowth(int actorId, int skillId, int objectId,
            NoveltyClass novelty, int? level, long currentTick, PatientId patientId)
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
            if ((int)novelty < 0 || (int)novelty > (int)NoveltyClass.Normal)
                throw new ArgumentOutOfRangeException(nameof(novelty),
                    $"NoveltyClass ordinal {(int)novelty} 不在有效范围 [0, {(int)NoveltyClass.Normal}]");
            if (currentTick < 0)
                throw new ArgumentOutOfRangeException(nameof(currentTick),
                    $"currentTick={currentTick} 不能为负");

            int resolvedLevel = level ?? -1; // 未升级 → 哨兵 -1
            int noveltyOrdinal = (int)novelty;

            return new SkillGrownPayload(actorId, patientId.Value, skillId, objectId,
                noveltyOrdinal, resolvedLevel);
        }

        /// <summary>便捷重载: patientId 默认 PatientId.None(无病例语境)。</summary>
        public static SkillGrownPayload EmitGrowth(int actorId, int skillId, int objectId,
            NoveltyClass novelty, int? level, long currentTick)
        {
            return EmitGrowth(actorId, skillId, objectId, novelty, level, currentTick,
                PatientId.None);
        }
    }
}
