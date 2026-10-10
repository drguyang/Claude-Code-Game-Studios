// sprint-05 T2.1 · 42 侧只读数源的**生产实装**(2026-10-10)
//
// 权威来源:
//   ADR-013 §9 C3 —— 42 只渲染不持状态;**本类是 42 与装配层之间的装配桥**,同样零缓存
//   ADR-025 §① —— `Gameplay.Boot` = 装配层(组合根案 A)
//   ADR-005 抽象点之四 `IVitalsQuery` / `VitalsDto`(全案唯一浮点出口)
//   story-024 边界①② —— 42 零自定义映射(通道/词/极性全来自 8 的表)
//   GDD disease-simulation.md F2 signs tripwire —— 当前 tick 下 9 侧恒产出
//     `SignCount = 0` / `mask = 0`(词表内容与阈值归 8)⇒ 本类暴露的
//     `RevealedRows` **恒空集**;接线目标是把 42 接到唯一浮点出口上,
//     **不是**先把尚未存在的词表画出来(禁假数据)。
//
// 设计说明:
//   · 住 `Gameplay.Boot`(**不**住 `Gameplay.UI`):42 装配不可反向引用装配层
//     (ADR-025 §① Gameplay.UI 引用集 = {Sim.Contracts, Gameplay.Presentation};
//      而 `ICasebookDtoSource` 住 Gameplay.UI)⇒ 实现必须在能同时看见
//     `Gameplay.UI` + `Sim` + `Sim.Contracts` 的装配 = 本装配(与案 A 同理由)。
//   · 每 tick 边沿刷新:调 `ChangePatient` 换绑定病人 ⇒ 下一 Property 读即新值
//     (零缓存字段 —— 缓存 = 第二份真相,破 C3)。
//   · 绑定病人在场性由调用方负责(不在场 = `GetVitals` fail-loud
//     `PatientCourseNotFoundException`,42 侧转为空行 —— 由 driver 的 try 兜住)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Presentation.Diagnosis;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.DiseaseSimulation;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>
    /// 脉案只读数源的生产实装 —— 把 42 接到 **唯一浮点出口** <see cref="IVitalsQuery"/>
    /// (sprint-05 T2.1)。每次读都穿过 query(零缓存副本)。
    /// </summary>
    public sealed class CasebookVitalsSource : ICasebookDtoSource
    {
        private readonly IVitalsQuery _query;
        private PatientId _patient = PatientId.None;

        /// <param name="query">体征查询门面(9 的 `IVitalsQuery` 生产实装 =
        /// <see cref="DiseaseVitalsService"/>;fail-loud 不接受 null)。</param>
        public CasebookVitalsSource(IVitalsQuery query)
        {
            _query = query ?? throw new ArgumentNullException(nameof(query));
        }

        /// <summary>当前绑定病人。</summary>
        public PatientId BoundPatient => _patient;

        /// <summary>换绑定病人(不持旧病人的任何量 —— 42 下次读即是新病人)。</summary>
        public void ChangePatient(PatientId patient) => _patient = patient;

        /// <inheritdoc/>
        /// <remarks>不在场 / 未建档 ⇒ <c>GetVitals</c> fail-loud;42 侧转空行
        /// (见 <see cref="SkeuoRuntimeDriver.OnTickEdge"/> 的兜底)。</remarks>
        public int ActiveSignCount
        {
            get
            {
                try { return _query.GetVitals(_patient).SignCount; }
                catch (PatientCourseNotFoundException) { return 0; }   // 未建档 ⇒ 空行
            }
        }

        /// <inheritdoc/>
        /// <remarks>
        /// **恒空集**(F2 signs tripwire):词表内容与阈值归 8,9 侧当前无阈值可判活跃词条
        /// ⇒ `DiseaseVitalsService.GetVitals` 恒返 `SignCount = 0`。接线目标是把 42
        /// 挂到真源上,**不是**编造读数 —— 空集经 <see cref="Gameplay.UI.Skeuomorphic.CasebookScreen"/>
        /// 走「未查 = 空行」(`:329`),正是接线期应有的诚实形态。
        /// </remarks>
        public IReadOnlyList<SignLexemeRow> RevealedRows => Array.Empty<SignLexemeRow>();

        /// <inheritdoc/>
        /// <remarks>精度档归读数侧(8 / 9);9 侧未接线前恒粗档(0)。</remarks>
        public int PrecisionSlot => 0;

        /// <inheritdoc/>
        /// <remarks>落笔 tick 归 37(判断记录落病例流,ADR-008);37 未接线 ⇒ 0 = 刚落笔。</remarks>
        public long InkAgeTicks => 0L;
    }
}
