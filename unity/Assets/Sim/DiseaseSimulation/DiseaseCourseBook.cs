// M2 接线轮阶段 2 · 批次 C(体征链核心)—— apply 层的**状态持有侧**。
//
// 权威来源:
//   三断点之①「apply 层无」—— `ProgressionEvaluator` 收 events 却不解码不用,
//     病人状态无事件驱动(阶段 0 勘察)。本文件 = 状态侧;驱动与解码侧 = 组合根的
//     `Gameplay.Boot.DiseaseVitalsService`(解码需要 `Sim.Codec`,Sim 够不着 —— 见 DiseaseCourse.cs 头注)。
//   ADR-005 —— 病史事件流是唯一真源;主机唯一执行 Step。
//   ADR-030 ③ —— `DiseaseOnset` 的 `Append` 调用者 = 9(经 `PatientSpawner`);本类只**读**它。
//   ADR-006 Amendment B —— `patient_id` 必留(高水位重构依赖)⇒ 载荷 `patient_id` 与事件头
//     `SimEvent.Patient` 须一致,本类对不一致 fail-loud(不静默择一)。
//   缺口 fail-loud 口径(承 CompositionRoot 头注 / RegistrySchema 具名异常惯例):
//     未建档取档 / 重复建档 / 无档落处置 ⇒ 具名异常,不返回半成品、不静默造档。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.DiseaseSimulation
{
    /// <summary>
    /// 病程档案簿 —— 按病人持有 <see cref="DiseaseCourse"/>,由病史流事件单调推进。
    /// </summary>
    /// <remarks>
    /// <para><b>不自己读流</b>:本类收**已解码**的强类型载荷(见文件头分工),
    /// 故它是纯整数域、零 IO、零 codec 依赖 —— 可在门 A 内被直接单测。</para>
    /// <para><b>入流序 = 插入序</b>:<see cref="InOrder"/> 是稳定的追加列表 ⇒ 步进遍历序确定
    /// (Dictionary 的遍历序不作承诺,刻意不用它驱动 Step)。</para>
    /// </remarks>
    public sealed class DiseaseCourseBook
    {
        private readonly Dictionary<int, DiseaseCourse> _byPatient =
            new Dictionary<int, DiseaseCourse>();
        private readonly List<DiseaseCourse> _inOrder = new List<DiseaseCourse>();

        /// <summary>已建档病人数。</summary>
        public int Count => _inOrder.Count;

        /// <summary>按建档顺序的只读视图(Step 的确定性遍历面)。</summary>
        public IReadOnlyList<DiseaseCourse> InOrder => _inOrder;

        /// <summary>
        /// 建档(消费 <c>DiseaseOnset</c>)。
        /// </summary>
        /// <param name="payload">解码后的载荷(ADR-030 五字段)。</param>
        /// <param name="headerPatient">事件头 <c>SimEvent.Patient</c> 归因。</param>
        /// <returns>新建的档案。</returns>
        /// <exception cref="InvalidOperationException">载荷 `patient_id` 与事件头不一致(ADR-006
        /// Amendment B:`patient_id` 必留 ⇒ 两处必须同指一人),或该病人已建档(重复 onset)。</exception>
        public DiseaseCourse ApplyOnset(in DiseaseOnsetPayload payload, PatientId headerPatient)
        {
            if (payload.PatientId != headerPatient.Value)
                throw new InvalidOperationException(
                    $"DiseaseOnset 载荷 patient_id={payload.PatientId} 与事件头 Patient={headerPatient.Value} " +
                    "不一致 —— ADR-006 Amendment B 要求两处同指一人,拒绝建档");

            if (_byPatient.ContainsKey(payload.PatientId))
                throw new InvalidOperationException(
                    $"patient_id={payload.PatientId} 已建档,重复 DiseaseOnset —— 病人出现是单次事件(ADR-030 ① 甲案)");

            var course = new DiseaseCourse(headerPatient, payload.DiseaseId,
                                           payload.OnsetTick, payload.PatientSeed);
            _byPatient[payload.PatientId] = course;
            _inOrder.Add(course);
            return course;
        }

        /// <summary>
        /// 追加一次处置剂量(消费 <c>DrugTreatmentApplied</c> / <c>EmergencyTreatmentApplied</c>)。
        /// </summary>
        /// <param name="headerPatient">事件头归因(两条 Kind 的载荷均**无** patient 字段 ⇒ 头是唯一病人来源)。</param>
        /// <param name="dose">解码并归一后的三元组。</param>
        /// <returns>所属档案。</returns>
        /// <exception cref="InvalidOperationException">事件无病人归因,或该病人未建档
        /// (处置早于 onset = 入流序违例,不静默造档)。</exception>
        public DiseaseCourse ApplyTreatment(PatientId headerPatient, in TreatmentDose dose)
        {
            DiseaseCourse course = Require(headerPatient, "处置事件");
            course.AddDose(dose);
            return course;
        }

        /// <summary>
        /// 登记一次急救判定输入(消费 <c>EmergencyAttempt</c>)。
        /// </summary>
        /// <remarks>
        /// <para><b>只计数,不求值</b>:`EmergencyAttempt` 是**判定输入**而非结算输出
        /// (ADR-009 Amendment I);结算结果由 <c>EmergencyTreatmentApplied</c> 落流。
        /// 9 侧不在本档案内复算判定(判定 = 10 的 `JudgeEvaluator`,主机职责)。</para>
        /// </remarks>
        /// <param name="headerPatient">事件头归因(批次 E 起急救两支已带真实病人)。</param>
        /// <param name="attempt">解码后的判定输入载荷。</param>
        /// <returns>所属档案。</returns>
        /// <exception cref="InvalidOperationException">无病人归因,或该病人未建档。</exception>
        public DiseaseCourse RegisterEmergencyAttempt(PatientId headerPatient, in EmergencyAttemptPayload attempt)
        {
            DiseaseCourse course = Require(headerPatient, "EmergencyAttempt");
            course.NoteEmergencyAttempt(attempt.Action);
            return course;
        }

        /// <summary>取档(不存在返回 false;不抛 —— 探测面)。</summary>
        public bool TryGet(PatientId patient, out DiseaseCourse course)
            => _byPatient.TryGetValue(patient.Value, out course);

        /// <summary>取档(不存在 = 具名异常 —— 查询面 fail-loud)。</summary>
        /// <exception cref="PatientCourseNotFoundException">该病人尚未建档。</exception>
        public DiseaseCourse Get(PatientId patient)
            => TryGet(patient, out var course)
                ? course
                : throw new PatientCourseNotFoundException(patient);

        private DiseaseCourse Require(PatientId patient, string what)
        {
            if (patient.Value < 0)
                throw new InvalidOperationException(
                    $"{what} 带 PatientId.None/负值({patient.Value})—— 急救三 Kind 属病人侧事件," +
                    "无归因则无法归档(ADR-009 Amendment I + ADR-006 Amendment B)");
            if (!_byPatient.TryGetValue(patient.Value, out var course))
                throw new InvalidOperationException(
                    $"{what} 指向未建档病人 patient_id={patient.Value} —— 入流序违例(DiseaseOnset 必须先于处置)," +
                    "拒绝静默造档");
            return course;
        }
    }

    /// <summary>
    /// 查询面 fail-loud:请求的病人尚未建档(未应用 <c>DiseaseOnset</c>,或 Step 从未驱动到该事件)。
    /// </summary>
    /// <remarks>
    /// 具名异常,照库内惯例(<see cref="RegistryValidationException"/> /
    /// <see cref="EventPoolValidationException"/> 同族)—— **不返回默认 `VitalsDto`**:
    /// 全零体征与「真的一点症状没有」不可区分,属静默失败面(AC-20「无条件真值」的前提)。
    /// </remarks>
    public sealed class PatientCourseNotFoundException : Exception
    {
        /// <summary>请求的病人 id(-1 = <c>PatientId.None</c>)。</summary>
        public int RequestedPatient { get; }

        public PatientCourseNotFoundException(PatientId patient)
            : base($"IVitalsQuery.GetVitals:patient_id={patient.Value} 尚未建档 —— " +
                   "病史流内无该病人的 DiseaseOnset,或体征链 Step 尚未驱动到它(拒绝返回默认 VitalsDto)")
        {
            RequestedPatient = patient.Value;
        }
    }
}
