// S12-9 · story-002 —— 试药史谓词(OQ-53-9 形状)。
//
// 权威来源:
//   GDD medical-consequences.md 规则三(2026-09-19 用户裁定 [A]):**试药序 = 同一 patient 的
//     连续 `DrugTreatmentApplied` 序列、无 `JudgmentRecorded` 支撑**;数值归平衡轮。
//   卡面 story-002 AC(试药史形状):连续 run 计数 + 支持性判断打断的谓词实现,
//     阈值常量留槽(值归数值轮);极性真源 = 9 注册表(53 不判极性)。
//   ADR-031(解码行读取口径)/ 规则五(入向闭集 —— 只看 Drug / Judgment 两支)
//
// ⚠️ 形状(本件钉):谓词 = f(流) —— 按 tick 有序扫,**逐病人**维护连续 Drug run;
//   该病人出现 JudgmentRecorded ⇒ 打断其 run;run 长度 ≥ 阈值 ⇒ 有试药史。
//   **跨病人不连**:他人的 Drug 既不延续也不打断本病人的 run。
//   阈值是留槽公开常量。
//
// ⚠️ 2026-10-15 返工(D):原实现是单游标 run/runPatient —— 换病人即 `run = 1`,
//   A 病人在跑的 run 被 B 病人**静默丢弃**(GDD 规则三原文:「同一 patient 的连续序列」)。
//   改为逐病人 `Dictionary<int,int> runByPatient`。
//
// ⚠️ 2026-10-15 返工(D):删除未使用的 `IPayloadDecoder decoder` 形参 —— 原实现断言非空后
//   再未使用(死依赖),且 Drug / Judgment 两支的 Kind 判别只需事件头(ADR-031 解码口径
//   不涉本谓词)。调用方(测试 5 处)同步去参。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.Consequences
{
    /// <summary>试药史谓词(OQ-53-9):同病人连续 DrugTreatmentApplied、无 JudgmentRecorded 支撑。</summary>
    public static class TrialHistoryPredicate
    {
        /// <summary>run 判定阈值(留槽常量 —— 值归数值轮;卡面 Implementation Note 6)。
        /// <para>连续 Drug 条数 ≥ 本值且中途无支持性 Judgment ⇒ 试药史痕迹成立。</para></summary>
        public const int TrialRunThreshold = 3;

        /// <summary>run 计数下界(语义哨兵:空流 / 无 Drug ⇒ 0)。</summary>
        public const int MinConsecutiveDrugs = 0;

        /// <summary>
        /// 判定事件流中是否存在试药史痕迹。
        /// </summary>
        /// <param name="stream">事件流(**任意到达序** —— 本谓词先按全序键排序,承规则四)。</param>
        /// <returns>true = 存在某病人 ≥ <see cref="TrialRunThreshold"/> 的连续 Drug run
        /// (无该病人的支持性 Judgment 打断)。</returns>
        public static bool HasTrialHistory(IReadOnlyList<SimEvent> stream)
            => PatientsWithTrialHistory(stream).Count > 0;

        /// <summary>
        /// 判定**指定病人**是否有试药史痕迹(2026-10-30 · ADR-036 实现轮 · 用户裁定「逐病人」)。
        /// </summary>
        /// <param name="stream">事件流(任意到达序)。</param>
        /// <param name="patient">只看该病人的事件;他人事件不延续也不打断。</param>
        /// <returns>true = **该病人**存在 ≥ <see cref="TrialRunThreshold"/> 的连续 Drug run。</returns>
        /// <remarks>
        /// <b>为什么要有逐病人入口</b>:53 的判定是**逐病例**的(`Outcome = Judge(P,C,D,U)` 的
        /// 输入集以一条结案为单位),而流级 <see cref="HasTrialHistory(IReadOnlyList{SimEvent})"/>
        /// 的语义是「流里**有任一**病人满足」—— 用它当逐病例的 <c>HasTrialHistory</c>,
        /// 会让「甲连开三副药」把**乙**的每条结案都判成试药史。两条边界的差集是**语义**差集,
        /// 不是性能差集。
        /// <para>⚠️ 与流级版本的关系:流级 ≡ 「∃ 病人 逐病人版本为真」。二者在不同输入下可同时为真
        /// 但**不可互相推出** —— 流级为真不对任何具体病人成立。</para>
        /// </remarks>
        public static bool HasTrialHistory(IReadOnlyList<SimEvent> stream, PatientId patient)
            => PatientsWithTrialHistory(stream).Contains(patient.Value);

        /// <summary>
        /// 一次扫流,返回**全部**有试药史痕迹的病人 id(逐病人语义,与逐次查询同口径)。
        /// </summary>
        /// <param name="stream">事件流(任意到达序)。</param>
        /// <returns>满足 `run ≥ <see cref="TrialRunThreshold"/>` 的病人 id 集(空流 ⇒ 空集)。</returns>
        /// <remarks>
        /// 🔁 **2026-10-30(ADR-036 V-7 实测后)** —— 53 的 driver 每 tick 要对**每个已结案病例**
        /// 问一次试药史。逐例调用 `HasTrialHistory(stream, patient)` 每次都会**复制并全排序整条流**
        /// ⇒ 1000 例 × 2000 事件 = 1000 次全排序(实测把单 tick 推到 **1135 ms**,
        /// 而 tick 预算是 50 ms)。本入口把排序降为**每 tick 一次**。
        /// <para>语义与逐例版**逐位相同**:两者都是「该病人的连续 Drug run 是否 ≥ 阈值,
        /// 且未被该病人的 `JudgmentRecorded` 打断」,跨病人互不影响。</para>
        /// </remarks>
        public static HashSet<int> PatientsWithTrialHistory(IReadOnlyList<SimEvent> stream)
        {
            var hits = new HashSet<int>();
            if (stream == null || stream.Count == 0) return hits;

            var ordered = new List<SimEvent>(stream.Count);
            for (int i = 0; i < stream.Count; i++) ordered.Add(stream[i]);
            ordered.Sort((a, b) => EventOrder.Compare(in a, in b));

            var runByPatient = new Dictionary<int, int>();
            for (int i = 0; i < ordered.Count; i++)
            {
                SimEvent e = ordered[i];
                int patient = e.Patient.Value;

                if (e.Kind == EventKind.DrugTreatmentApplied)
                {
                    runByPatient.TryGetValue(patient, out int run);
                    run++;
                    runByPatient[patient] = run;
                    if (run >= TrialRunThreshold) hits.Add(patient);
                }
                else if (e.Kind == EventKind.JudgmentRecorded)
                {
                    if (runByPatient.ContainsKey(patient))
                        runByPatient[patient] = MinConsecutiveDrugs;
                }
            }
            return hits;
        }

    }
}
