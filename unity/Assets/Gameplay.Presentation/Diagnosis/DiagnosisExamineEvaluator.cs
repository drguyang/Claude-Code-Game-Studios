// sprint-06 G1.1 · 查体求值门面(8 侧纯函数 · 2026-10-11)
//
// 权威来源:
//   GDD design/gdd/diagnosis-system.md —— 规则一(8 只读不写不持状态)/ 规则四(精度)/
//     §F-8.1(READ_FLOOR 双条件门)/ §F-8.2(TierIndex / 空白档回退)/ §S-8.2 四态/
//     AC-8-3(纯静态零字段)/ · §Visual/Audio 二(词表与阈值归 8)
//   disease-simulation.md §F2(9 只出 signs[] 词键 + 通道位;阈值归 8)
//   ADR-005(IVitalsQuery = 唯一浮点出口)· ADR-009 §一 Q1(revealed = 派生态,
//     「查体完成」不进三流 —— 对撞裁决乙案 2026-10-11)
//   DiagnosisChannelMaskMap(R-8.1 序数 ↔ AC-21 掩码位,唯一合法转换)
//   DiagnosisReadFloorEvaluator(row 级 Evaluate —— 本门面按行复用它,不重实现)
//
// ⚠️ 纯函数零字段(AC-8-3):同输入两次调用逐字相同;依赖全部显式入参。
// ⚠️ 住前缀命名空间(DiagnosisBoundaryGates 扫描键)→ 零 IEventSink(铁律②)、
//    零时钟/零 RNG(AC-8-3)、零持久化(D-PERSIST)、零 Fix 族(D-FIX)。
//
// ⚠️ 值域(P0 简化,登记非隐藏):`signValue01` 取该通道的投影量 ∈ [0,1]。
//    9 侧 F2 病种级 `Project(Sign_j)ⱼ` 未实现(ProgressionEvaluator.Signs 恒空,
//    tripwire 保留)⇒ P0 以 `VitalsDto.Position`(单病种简化)注入 —— 归 9 接线轮,
//    本门面形状不受影响(判据照跑)。

using System;
using System.Collections.Generic;

namespace DaYiJingCheng.Gameplay.Presentation.Diagnosis
{
    /// <summary>
    /// 一次查体完成后的呈现集(值语义;42 值区 + 39 分册快照的输入)。
    /// <para>由 <see cref="DiagnosisExamineEvaluator.Evaluate"/> 产出;零缓存、零回源。</para>
    /// </summary>
    public readonly struct ExaminedSet
    {
        /// <summary>已收录词条(仅 Positive / Negative —— 读不出按阴性形态,不进呈现词流)。</summary>
        public readonly IReadOnlyList<SignLexemeRow> Rows;

        /// <summary>通道位掩码(AC-21;5 体格位按位或;History 无位)。</summary>
        public readonly int SignChannelMask;

        /// <summary>已收录词条数(0 = 空集 —— 全读不出/无词 ⇒ 空行,禁假数据)。</summary>
        public readonly int SignCount;

        /// <summary>本次查体的精度档(0=粗…3=满;§F-8.2 SlotOfSkill)。</summary>
        public readonly int PrecisionSlot;

        public ExaminedSet(IReadOnlyList<SignLexemeRow> rows, int signChannelMask,
                           int signCount, int precisionSlot)
        {
            Rows = rows;
            SignChannelMask = signChannelMask;
            SignCount = signCount;
            PrecisionSlot = precisionSlot;
        }
    }

    /// <summary>
    /// 查体求值门面(8 侧纯函数)—— 查体完成 ⇒ 逐行按 <see cref="DiagnosisReadFloorEvaluator"/>
    /// 裁决,收录阳性/阴性词条并聚通道掩码 + 精度档。
    /// <para><b>revealed = 派生态</b>(对撞裁决乙案 2026-10-11):本门面**不写流、不记录**,
    /// 只算「本次查体该看到什么」;「查了没有」+ 快照归 39 分册(装配层的
    /// <c>ExaminationLedger</c>,本装配不引用装配层 —— 由外部注入)。</para>
    /// </summary>
    public static class DiagnosisExamineEvaluator
    {
        /// <summary>
        /// 求值一次查体(纯函数)。
        /// </summary>
        /// <param name="table">F-8.1 门槛定表(binary32 查表;依赖注入,非持状态)。</param>
        /// <param name="rows">8 词表(全部词条;按通道分组由 <see cref="SignChannel"/> 映掩码)。</param>
        /// <param name="skill">诊断熟练度(整数等级;§F-8.2 TierIndex 输入)。</param>
        /// <param name="signValue01">9 的投影量 <c>Project(Sign_j) ∈ [0,1]</c>(D-8-4;
        /// P0 简化 = 单病种 <c>VitalsDto.Position</c>,见文件头值域注)。</param>
        /// <returns>呈现集。空输入 ⇒ 空集(PassThrough,零收录)。</returns>
        /// <exception cref="ArgumentNullException">任一定表 / 行集为 null。</exception>
        public static ExaminedSet Evaluate(
            in DiagnosisReadFloorTable table,
            IReadOnlyList<SignLexemeRow> rows,
            int skill,
            float signValue01)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));

            var collected = new List<SignLexemeRow>();
            int channelMask = 0;
            int count = 0;

            for (int i = 0; i < rows.Count; i++)
            {
                SignLexemeRow row = rows[i];

                DiagnosisReadFloorEvaluator.Outcome o =
                    DiagnosisReadFloorEvaluator.Evaluate(in table, row, skill, signValue01);

                // 只收录既有词又可为证据的形态(阳性/阴性):
                //   UnreadableNegative = 已查但读不出 ⇒ 呈阴性形态但不构成条,
                //   AC-8-17 真值半边(不得参与排除)⇒ 不进呈现词流(值区空 = 阴性形态)。
                if (o.State != SignReadState.Positive && o.State != SignReadState.Negative)
                    continue;

                collected.Add(row);
                count++;

                // 通道掩码(R-8.1 序数 → AC-21 位;History 无位 ⇒ OR 0 不污染)
                int bit = DiagnosisChannelMaskMap.MaskOf(row.Channel);
                channelMask |= bit;
            }

            int slot = DiagnosisReadFloorEvaluator.TierIndex(skill);   // 0..3,S-8.2 档序
            return new ExaminedSet(collected.AsReadOnly(), channelMask, count, slot);
        }
    }
}