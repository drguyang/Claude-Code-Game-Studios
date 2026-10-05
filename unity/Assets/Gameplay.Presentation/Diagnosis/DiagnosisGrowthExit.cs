// diagnosis-system Story 001 —— 成长事件唯一门控出口形状(边界铁律④)。
//
// 权威来源:
//   GDD design/gdd/diagnosis-system.md 边界铁律④(`EmitGrowth` 只在主机执一次;调用点
//     须被 `IIdAuthority` 门控)· 出向表「30 技能与熟练度」行(8 是调用方,`K_difficulty`
//     作入参推入;**8 不写流**,事件由主机编码 + Append)
//   ADR-005(主机唯一执行 Step / CatchUp;六个 P0 抽象点)· ADR-007 §一(主机唯一 Append)
//   TR-diag-005(`EmitGrowth` 仅主机侧由 `IIdAuthority` 门控 —— partial;调用语义归 story 005)
//   Sim.Contracts `SkillGrownEmitter`(契约侧唯一对外入口 —— **返回载荷、不 Append**,
//     编码 / 构造 SimEvent / Append 三步全归调用方(主机))
//
// ⚠️ **story-001 只锁形状,不落调用语义**:本方法是 8 装配内**唯一**允许出现
//    `SkillGrownEmitter.EmitGrowth` 调用点的位置(`DiagnosisBoundaryGates` IL 层
//    `[D-EXIT]` 断言调用点恰 = 1 且在本类型内);「何时发 / 客户端不发包 /
//    `IIdAuthority` 门控」归 story 005(状态机 + 主机门控接线)。
// ⚠️ **零字段静态纯函数**(AC-8-3):出口自身不持状态、不持时钟 —— `currentTick` 由
//    调用方传入(四类合法可变输入之一,AC-8-3 ④)。

using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.Contracts.SkillSystem;

namespace DaYiJingCheng.Gameplay.Presentation.Diagnosis
{
    /// <summary>8 的成长事件唯一门控出口(铁律④ / TR-diag-005 的「存在唯一出口形状」半边)。
    /// <para>签名与契约 <c>SkillGrownEmitter.EmitGrowth</c> 逐参对齐,纯转发 ——
    /// <b>返回载荷、不写流</b>(与契约同构;Append 归主机,8 零 <c>IEventSink</c> 引用,
    /// 铁律②)。</para></summary>
    public static class DiagnosisGrowthExit
    {
        /// <summary>构造技能成长载荷并经唯一出口转发(8 内唯一允许的 EmitGrowth 调用点)。</summary>
        /// <param name="actorId">施予者(玩家 / 实体)id —— ≥ 0,契约校验。</param>
        /// <param name="skillId">技能 id(SkillId ordinal)。</param>
        /// <param name="objectId">对象 id(病种 / 品种 / 对手类型 ordinal)。</param>
        /// <param name="novelty">新颖度类别(First / Stale / Normal)。</param>
        /// <param name="level">升级时 = 新等级;未升级 = <c>null</c>(契约写入哨兵
        /// <see cref="SkillGrownPayload.LevelNotGrown"/>)。</param>
        /// <param name="currentTick">当前逻辑 tick(调用方取自 <c>ITickProvider</c>;
        /// 出口不持时钟)。</param>
        /// <param name="patientId">成长所依附的受伤实体;无病例语境 = <see cref="PatientId.None"/>。</param>
        /// <returns>技能成长载荷(编码 / 构造 <c>SimEvent</c> / Append 三步归主机)。</returns>
        /// <exception cref="ArgumentOutOfRangeException">任何 id 为负、novelty 越界或
        /// currentTick 为负(契约校验,原样透传)。</exception>
        public static SkillGrownPayload EmitGrowth(
            int actorId, int skillId, int objectId,
            NoveltyClass novelty, int? level, long currentTick, PatientId patientId)
            => SkillGrownEmitter.EmitGrowth(actorId, skillId, objectId, novelty, level,
                                             currentTick, patientId);
    }
}
