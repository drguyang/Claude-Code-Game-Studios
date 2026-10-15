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
        {
            if (stream == null || stream.Count == 0) return false;

            // 有序扫描(置换不变 —— 与聚合器同口径,承 GDD 规则四按 Tick 不按到达序)。
            var ordered = new List<SimEvent>(stream.Count);
            for (int i = 0; i < stream.Count; i++) ordered.Add(stream[i]);
            ordered.Sort((a, b) => EventOrder.Compare(in a, in b));

            // D:逐病人 run 计数器 —— 病人 id ⇒ 该病人当前连续 Drug 条数。
            // 换病人不再重置他人计数器(旧实现的静默丢失)。
            var runByPatient = new Dictionary<int, int>();

            for (int i = 0; i < ordered.Count; i++)
            {
                SimEvent e = ordered[i];
                int patient = e.Patient.Value;

                if (e.Kind == EventKind.DrugTreatmentApplied)
                {
                    // 同病人延续 run;他人事件不触碰本计数器(「同一 patient 的连续序列」逐字)。
                    runByPatient.TryGetValue(patient, out int run);
                    run++;
                    runByPatient[patient] = run;
                    if (run >= TrialRunThreshold) return true;
                }
                else if (e.Kind == EventKind.JudgmentRecorded)
                {
                    // 支持性判断打断**该病人**的 run(「无 JudgmentRecorded 支撑」逐字)。
                    if (runByPatient.ContainsKey(patient))
                        runByPatient[patient] = MinConsecutiveDrugs;
                }
                // 其余 Kind 不打断也不延续(闭集外事件与谓词无关)。
            }
            return false;
        }
    }
}
