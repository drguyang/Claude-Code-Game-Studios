// 技法解锁事件发射器 —— ADR-043 §D3(2026-11-09 · T3)
//
// 权威:docs/architecture/adr-043-technique-unlock-kind.md
//   · D1 载体 = 病史流 Kind `TechniqueUnlocked`
//   · D3 入口 = 本类(纯载荷构造,**不 Append**,由调用方 / 主机完成)
//   · D4 载荷 = (actor_id, technique_id, source_id) 全整数域
//   · D5 技法 id = 处置轴 `disease_action_axis.json` 的 action id
//
// ⚠️ 与 `SkillGrownEmitter.EmitGrowth` **同构**:30 明写不 Append,
//    「编码 + 构造 SimEvent + Append」由主机侧端口完成(`TechniqueUnlockWriter`)。
// ⚠️ **闭集校验不在这里**:本件拿不到处置轴(Sim.Contracts 不读数据)。
//    技法 id 是否属于轴闭集由**写者侧**校验(它有轴)—— 见 `TechniqueUnlockWriter`。
//    本件只挡「负数」这类必然错误的输入(fail-loud)。

using System;

namespace DaYiJingCheng.Sim.Contracts.SkillSystem
{
    /// <summary>`TechniqueUnlocked` 的唯一对外入口。</summary>
    public static class TechniqueUnlockEmitter
    {
        /// <summary>构造解锁载荷(**不编码、不建事件、不 Append**,由调用方完成)。</summary>
        /// <param name="actorId">学会的人(玩家 id 空间)。</param>
        /// <param name="techniqueId">技法 id(= 处置轴 action id)。</param>
        /// <param name="sourceId">来源 id(手册页 / 奇遇;**不参与判定**)。</param>
        /// <returns>载荷结构体(调用方负责 `PayloadCodec.Encode` + SimEvent + Append)。</returns>
        /// <exception cref="ArgumentOutOfRangeException">任一 id 为负 —— 负 id 一定是调用方算错。</exception>
        public static TechniqueUnlockedPayload EmitUnlock(int actorId, int techniqueId, int sourceId)
        {
            if (actorId < 0)
                throw new ArgumentOutOfRangeException(nameof(actorId), $"actorId={actorId} 不能为负");
            if (techniqueId < 0)
                throw new ArgumentOutOfRangeException(nameof(techniqueId),
                    $"techniqueId={techniqueId} 不能为负(技法 id = 处置轴 action id)");
            if (sourceId < 0)
                throw new ArgumentOutOfRangeException(nameof(sourceId),
                    $"sourceId={sourceId} 不能为负(-1 哨兵也无定义:来源未知就不该写这条)");

            return new TechniqueUnlockedPayload(actorId, techniqueId, sourceId);
        }
    }
}
