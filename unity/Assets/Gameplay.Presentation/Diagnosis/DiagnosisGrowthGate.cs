// diagnosis-system Story 005 —— EmitGrowth 主机门控(铁律④ · TR-diag-005)。
//
// 权威来源:
//   GDD design/gdd/diagnosis-system.md 边界铁律④(EmitGrowth 只在主机执一次;
//     客户端不调)· AC「铁律④ EmitGrowth 门控」(静态守门 = 出口唯一 + 门控断言)
//   TR-diag-005(EmitGrowth 仅主机侧门控 —— story 001 落形状,调用语义归 story 005)
//   ADR-007 §一(IEventAuthority = 主机权威 / 掷骰权语义 —— 第六个 P0 抽象点)
//
// ⚠️ **登记订正(卡面措辞 vs 代码事实,评审可见)**:story-005 卡 Note 5 与 TR-diag-005
//    写「门控 = `IIdAuthority.IsHost(actorId)` 式判定」,但**实测 `IIdAuthority` 无 IsHost
//    成员**(只有 NextPatientId / NextItemInstanceId);主机权威成员在
//    **`IEventAuthority.IsHost`**(Abstractions.cs:61,ADR-007 §一)。本实现按代码事实取
//    `IEventAuthority.IsHost`,卡面措辞订正归本 story 的评审登记(GDD 背离/登记不修须显式)。
// ⚠️ **S-7 登记**:`IEventAuthority.IsHost` 在 P0 **恒 true**(Abstractions.cs:60 自陈)——
//    客户端拒绝分支在 P0 生产装配**不可达**,其判别力仅 fake 权威可证(测试侧已覆盖);
//    真客户端拦截的端到端回归 **NOT-RUN,归 45 / P1b 族**(与登记点 5 并列)。
// ⚠️ **出口唯一不被本文件破坏**:`DiagnosisBoundaryGates` [D-EXIT] 断言
//    `SkillGrownEmitter.EmitGrowth` 调用点恰 = 1 且在 DiagnosisGrowthExit 内 ——
//    本文件只调 DiagnosisGrowthExit 的转发,**不新增契约调用点**。

using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.Contracts.SkillSystem;

namespace DaYiJingCheng.Gameplay.Presentation.Diagnosis
{
    /// <summary>EmitGrowth 主机门控(铁律④ 的调用语义半边)。</summary>
    public static class DiagnosisGrowthGate
    {
        /// <summary>
        /// 门控式成长出口:客户端(非主机)⇒ **拒绝,零副作用**;主机 ⇒ 经唯一出口转发一次。
        /// </summary>
        /// <param name="authority">主机权威(ADR-007 第六抽象点;<c>null</c> 拒)。</param>
        /// <param name="actorId">施予者 id。</param>
        /// <param name="skillId">技能 id。</param>
        /// <param name="objectId">对象 id。</param>
        /// <param name="novelty">新颖度类别。</param>
        /// <param name="level">升级等级;<c>null</c> = 未升级。</param>
        /// <param name="currentTick">当前逻辑 tick。</param>
        /// <param name="patientId">依附的受伤实体;<c>null</c> 病例语境 = <see cref="PatientId.None"/>。</param>
        /// <param name="payload">放行时 = 出口返回的载荷;拒绝时 = <c>default</c>(零副作用)。</param>
        /// <returns><c>true</c> = 主机放行(恰一次出口调用);<c>false</c> = 拒绝。</returns>
        public static bool TryEmitGrowth(
            IEventAuthority authority,
            int actorId, int skillId, int objectId,
            NoveltyClass novelty, int? level, long currentTick, PatientId patientId,
            out SkillGrownPayload payload)
        {
            if (authority == null || !authority.IsHost)
            {
                payload = default; // 客户端:不调出口,零副作用
                return false;
            }

            payload = DiagnosisGrowthExit.EmitGrowth(
                actorId, skillId, objectId, novelty, level, currentTick, patientId);
            return true;
        }
    }
}
