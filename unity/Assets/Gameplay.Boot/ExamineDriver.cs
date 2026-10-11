// sprint-06 G1.3 · 查体入口驱动(2026-10-11)
//
// 权威来源:
//   sprint-06 §2 G1(查体入口装配):玩家近病人 Interact → 查体完成 → 8 求值落 39 分册快照
//   对撞裁决乙案:查体意图**不写流**(非判定输入,不产事件);「查了没有」住 39 分册
//   ADR-013 §9 C3(呈现侧只渲染不持状态;本驱动 = 呈现侧装配件,零 sim 写入)
//   DiagnosisExamineEvaluator(8 纯函数门面 —— 本次查体看到什么)
//   ExaminationLedger(39 侧最小分册)
//
// ⚠️ 查体意图的「判定输入」归属(D-8-3):tech-director 报告 B 已将「已查/未查」列 39 侧,
//   「查体意图 / 完成 tick / 通道」进第二 QoS 上行归 10(D-8-3 已有登记)——
//   本驱动是**单机本地最小路径**(意图本地消费,不落流),第二 QoS 上行归联机轮;
//   多机场景再走 ADR-001 第二 QoS。登记,不隐藏。
//
// ⚠️ 本类不产 `EmergencyAttempt` / 不产任何 SimEvent —— 纯呈现侧装配件。
//    查体 = 玩家行为记录,非 sim 副作用(乙案可证伪判据「病史流哈希逐位相同」)。

using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Presentation.Diagnosis;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>
    /// 查体入口驱动 —— 把「玩家对在场病人查体」接进 8 求值 + 39 分册。
    /// <para>输入 = 玩家对病人的查体意图(Interact 近病人)+ 该病人体征(IVitalsQuery)
    /// + 技能档(QueryLevel);输出 = 分册快照记录。零 sim 写入。</para>
    /// </summary>
    public sealed class ExamineDriver
    {
        private readonly IVitalsQuery _vitals;
        private readonly ExaminationLedger _ledger;
        private readonly int _queryLevel;

        /// <param name="vitals">9 的体征查询门面(唯一浮点出口;fail-loud 不接受 null)。</param>
        /// <param name="ledger">39 侧分册(查体记录落点)。</param>
        /// <param name="queryLevel">诊断技能档(30 的 QueryLevel —— 8 单向读 30,D-8-1 依赖方向)。</param>
        public ExamineDriver(IVitalsQuery vitals, ExaminationLedger ledger, int queryLevel)
        {
            _vitals = vitals ?? throw new System.ArgumentNullException(nameof(vitals));
            _ledger = ledger ?? throw new System.ArgumentNullException(nameof(ledger));
            _queryLevel = queryLevel;
        }

        /// <summary>
        /// 一次查体意图的本地消费:读数 → 求值 → 落分册快照。
        /// </summary>
        /// <param name="patientId">被查病人。</param>
        /// <param name="tick">完成 tick(来自 ITickProvider —— 本类零时钟,只透传)。</param>
        /// <param name="table">F-8.1 门槛定表(依赖注入,8 不持状态)。</param>
        /// <param name="rows">8 词表(8 烘焙表行集)。</param>
        /// <exception cref="Sim.Contracts.PatientCourseNotFoundException">病人未建档 /
        /// 不在场 —— 查体无从谈起,由调用方(Interact 装配)先判在场;此处 fail-loud 兜底。</exception>
        public void OnExamine(PatientId patientId, long tick,
                              in DiagnosisReadFloorTable table,
                              IReadOnlyList<SignLexemeRow> rows)
        {
            // 读体征(唯一浮点出口的消费侧;不在场 ⇒ GetVitals fail-loud,不吞)
            VitalsDto dto = _vitals.GetVitals(patientId);

            // 8 纯函数求值(本次查体看到什么)P0 简化:signValue01 = dto.Position(单病种)
            ExaminedSet set = DiagnosisExamineEvaluator.Evaluate(
                in table, rows, _queryLevel, dto.Position);

            // 落分册快照(AC-8-25 冻结值;复查覆盖旧读 AC-8-46)
            _ledger.RecordExamined(patientId.Value, tick, set);
        }
    }
}