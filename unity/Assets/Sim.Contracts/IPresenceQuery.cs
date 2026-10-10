// ADR-005 —— IPresenceQuery 抽象点。
//
// 权威来源:
//   ADR-005 §Key Interfaces —— 五抽象点之一
//   ADR-008 §六 —— 在场才模拟（离屏病人不进 Step）
//   GDD disease-simulation.md —— AC-15 有界性
//
// 语义:
//   - 提供方 = 表现层注入（格/在场视图）
//   - sim 侧只读整数在场标志
//   - 禁接收连续位置（AC-20-03 同构纪律）

using System.Collections.Generic;

namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary>
    /// 在场查询 —— 判断病人是否在场（被模拟）。
    /// </summary>
    public interface IPresenceQuery
    {
        /// <summary>
        /// 病人是否在场。
        /// </summary>
        bool IsPresent(PatientId patientId);

        /// <summary>
        /// 当前在场病人数。
        /// </summary>
        int PresentCount { get; }

        /// <summary>
        /// 指定格上是否有实体（玩家/敌人）。
        /// </summary>
        bool IsPresentAt(WorldPos cell);

        /// <summary>
        /// 当前在场病人的 id 集合（只读快照）。
        /// <para>sprint-05 T1.2 新增:病例开账驱动需要遍历在场病人。</para>
        /// </summary>
        IReadOnlyCollection<int> PresentPatientIds();
    }
}
