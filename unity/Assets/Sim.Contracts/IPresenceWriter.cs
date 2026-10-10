// sprint-05 T1.1 · 在场登记写面(2026-10-10)
//
// 权威来源:
//   IPresenceQuery 头注(提供方 = 表现层注入;sim 侧只读整数在场标志)
//   ADR-008 §六(在场才模拟)· AC-15(EventStream 有界性读 PresentCount / IsPresent)
//   PresenceRegistry 头注(写面给组合层;读面给 sim)
//
// 为何拆写面(2026-10-10 实测驱动):
//   EventStream.Append 的 AC-15 在场检查拒收不在场病人的事件 ⇒ spawn 前必须先登记。
//   但登记写面(AddPresent)住在 PresenceRegistry(Gameplay.PatientAI),而驱动
//   (PatientAppearedDriver)住在 Gameplay.Boot —— 装配方向 Boot → PatientAI 反向,
//   驱动不能引用实装类。抽象点 = 解法:Sim.Contracts 声明写面接口,实装实现它,
//   组合根把实装**作为接口**注入驱动(与 IPresenceQuery 读面同型)。

namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary>
    /// 在场登记写面 —— 组合层(9 的出现 / 6 的 chunk 激活)灌入在场集。
    /// <para>与 <see cref="IPresenceQuery"/> 读面配对:读面给 sim(EventStream AC-15),
    /// 写面给组合层(本接口)。</para>
    /// </summary>
    public interface IPresenceWriter
    {
        /// <summary>
        /// 登记病人在场(同时维护占格集)。
        /// <para>幂等:重复登记同一病人不触发变更通知。</para>
        /// </summary>
        /// <param name="patientId">病人 id</param>
        /// <param name="cell">病人所在格(占格集用;接线期可为占位格)</param>
        void AddPresent(PatientId patientId, WorldPos cell);

        /// <summary>
        /// 移除病人在场(同时清理占格集)。
        /// </summary>
        /// <param name="patientId">病人 id</param>
        void RemovePresent(PatientId patientId);

        /// <summary>
        /// 移动病人到另一格(同时维护两集)。
        /// </summary>
        /// <param name="patientId">病人 id</param>
        /// <param name="toCell">目标格</param>
        void MovePresent(PatientId patientId, WorldPos toCell);
    }
}
