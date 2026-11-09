// 技法解锁的只读查询契约 —— ADR-043 §D7(2026-11-09 · T3)
//
// 权威:docs/architecture/adr-043-technique-unlock-kind.md §D7
//   · 住 `Sim.Contracts`(**门 A 内**)—— 9 / 10 若都要问「这招会不会」,必须够得着
//   · **不复用 `ISkillLevelQuery`**:布尔 vs 等级。合一会让「未学会」与「0 级」混为一谈
//     (`skill-system.md:518`:0 级技能**仍可执行**,只是无加成 —— 与本契约的 false 完全不同)
//   · 实装 = 病史流折叠(`Gameplay.Boot/TechniqueUnlockProjection`),同源可重建(ADR-016 §一)
//
// ⚠️ **零引擎引用**:本文件只依赖 BCL。

namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary>
    /// 「某人的某一技法是否已解锁」的只读查询。
    /// <para>语义 = **知识层**(想不想得到这一招);物品层(做不做得成)与熟练层(做得稳不稳)
    /// 各自有别的载体 —— 不要在此合并(`technique-unlock-map-2026-11-09.md` §1)。</para>
    /// </summary>
    public interface ITechniqueUnlockQuery
    {
        /// <summary>该技法是否已解锁。</summary>
        /// <param name="actorId">玩家 id(玩家 id 空间)。</param>
        /// <param name="techniqueId">技法 id(= 处置轴 action id)。</param>
        /// <returns><c>true</c> = 已解锁;<c>false</c> = 未解锁 **或** 从未写过相关事件。</returns>
        bool IsUnlocked(int actorId, int techniqueId);
    }
}
