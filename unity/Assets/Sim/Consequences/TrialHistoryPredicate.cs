// S12-9 · story-002 —— 试药史谓词(OQ-53-9 形状)。
//
// 权威来源:
//   GDD medical-consequences.md 规则三(2026-09-19 用户裁定 [A]):**试药序 = 同一 patient 的
//     连续 `DrugTreatmentApplied` 序列、无 `JudgmentRecorded` 支撑**;数值归平衡轮。
//   卡面 story-002 AC(试药史形状):连续 run 计数 + 支持性判断打断的谓词实现,
//     阈值常量留槽(值归数值轮);极性真源 = 9 注册表(53 不判极性)。
//   ADR-031(解码行读取口径)/ 规则五(入向闭集 —— 只看 Drug / Judgment 两支)
//
// ⚠️ 形状(本件钉):谓词 = f(流, 解码器) —— 按 tick 有序扫,遇 JudgmentRecorded 打断
//   当前 run;run 长度 ≥ 阈值 ⇒ 有试药史。**跨病人不连**;阈值是留槽公开常量。
//
// ⚠️ 数值(阈值 3 等)归数值轮 —— 本件只钉「run 计数 + 打断」形状。

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
        /// <param name="decoder">载荷解码器(ADR-031;Drug 行的 Kind 判别不需要载荷内容,
        /// 但 Judgment 打断同样只需事件头 ⇒ 本参保留为解码口径的形状位,断言非 null)。</param>
        /// <returns>true = 存在 ≥ <see cref="TrialRunThreshold"/> 的连续 Drug run(无判断打断)。</returns>
        public static bool HasTrialHistory(IReadOnlyList<SimEvent> stream, IPayloadDecoder decoder)
        {
            if (decoder == null) throw new ArgumentNullException(nameof(decoder));
            if (stream == null || stream.Count == 0) return false;

            // 有序扫描(置换不变 —— 与聚合器同口径,承 GDD 规则四按 Tick 不按到达序)。
            var ordered = new List<SimEvent>(stream.Count);
            for (int i = 0; i < stream.Count; i++) ordered.Add(stream[i]);
            ordered.Sort((a, b) => EventOrder.Compare(in a, in b));

            int run = MinConsecutiveDrugs;
            int runPatient = int.MinValue;   // 当前 run 的病人(跨病人不连)

            for (int i = 0; i < ordered.Count; i++)
            {
                SimEvent e = ordered[i];

                if (e.Kind == EventKind.DrugTreatmentApplied)
                {
                    // 同病人延续 run;换病人 ⇒ 新 run(「同一 patient 的连续序列」逐字)。
                    if (e.Patient.Value == runPatient)
                        run++;
                    else
                    {
                        runPatient = e.Patient.Value;
                        run = 1;
                    }
                    if (run >= TrialRunThreshold) return true;
                }
                else if (e.Kind == EventKind.JudgmentRecorded && e.Patient.Value == runPatient)
                {
                    // 支持性判断打断该病人的 run(「无 JudgmentRecorded 支撑」逐字)。
                    run = MinConsecutiveDrugs;
                    runPatient = int.MinValue;
                }
                // 其余 Kind 不打断也不延续(闭集外事件与谓词无关)。
            }
            return false;
        }
    }
}
