// patient-ai Story 001 —— 只读视图 `IPresentPatients` 的状态枚举 + `ViewState()`(GDD F-13.6 / §States 二)。
//
// 权威来源:
//   GDD design/gdd/patient-ai.md §Formulas F-13.6(ViewState 优先级)· §States 二(粗状态枚举表)
//     · §Core Rules 十(13 出只读视图供 37 立案;13 不引用 37)
//   ADR-016 §六(13 ↔ 37 单向无环)· ADR-013(呈现层 DTO 受 PresentationDtoGuard 递归)
//   TR-patient-012/020 · AC-13-C1/C5
//
// ⚠️ **枚举刻意粗** —— 不含病种、不含 `position` / `trend`、**不含 `signs[]`**(AC-13-C1)。
//    37 立案只需要「是谁 · 在哪 · 能不能碰」,不需要「是什么病」(那是 8 与 37 自己的事)。
// ⚠️ **本表与 F-13.6 公式必须逐字一致**(AC-13-C5) —— 二者是同一映射的散文与公式两种表述。
//    原稿的「公式读 BehaviorState,表写 Waiting」是同一处歧义的两个版本,是 R3 的根因。
// ⚠️ **`Departed`(已离场)不进枚举** —— 视图是快照,离场者直接不在视图里(GDD §States 二)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.PatientAI
{
    /// <summary>`IPresentPatients` 的粗状态载荷(GDD §States 二)。**不含 `disease_id`**
    /// (规则三 + AC-13-C1;受 `PresentationDtoGuard` 递归扫描)。</summary>
    public enum PresentPatientState
    {
        /// <summary>`BehaviorState = Idle`(或 `Seeking.EnRoute`)· `SessionState = None` —— 在场、可就诊。</summary>
        Present = 0,
        /// <summary>`Seeking.AtClinic` · `SessionState = None` —— 已在医馆等待接诊。</summary>
        AwaitingCare = 1,
        /// <summary>`BehaviorState = Bedridden` —— 倒地不起(**行为描述,不映射九态**)。</summary>
        Collapsed = 2,
        /// <summary>`SessionState = InTreatment`(任一 `BehaviorState`)—— 就诊进行中。</summary>
        InTreatment = 3
    }

    /// <summary>只读视图的一行 —— `PatientId` + `WorldPos` 格 + 粗状态枚举(GDD 规则十)。
    /// <para>⚠️ **零 `disease_id` / 零 `position` / 零 `trend` / 零 `signs[]`**(AC-13-C1)。
    /// 受 `PresentationDtoGuard.Scan` 递归扫描 —— 本类型是测试的扫描根之一。</para></summary>
    public readonly struct PresentPatient
    {
        /// <summary>病人(与敌人共 id 空间,ADR-006 §Amendment B)。</summary>
        public readonly PatientId Id;
        /// <summary>逻辑格(整数,ADR-015 §三)。</summary>
        public readonly WorldPos Cell;
        /// <summary>粗状态(刻意粗 —— 见 <see cref="PresentPatientState"/>)。</summary>
        public readonly PresentPatientState State;

        public PresentPatient(PatientId id, WorldPos cell, PresentPatientState state)
        {
            Id = id; Cell = cell; State = state;
        }
    }

    /// <summary>只读视图(13 出、37 读;**13 不引用 37**,ADR-016 §六 单向无环)。</summary>
    public interface IPresentPatients
    {
        /// <summary>当前在场病人视图快照(离场者不在其中 —— AC-13-C3)。</summary>
        IReadOnlyList<PresentPatient> Snapshot();
    }

    /// <summary>`ViewState()`(F-13.6)—— 与 §States 二 枚举表**逐字一致**的纯函数。
    /// <para><b>优先级顺序**(判定顺序固定,无歧义;AC-13-C5 锁死):
    /// `InTreatment` &gt; `Collapsed` &gt; `AwaitingCare` &gt; `Present`。</para>
    /// <para>⚠️ **不出现 `position` / `trend` / 病种** —— 输入恰 = `(BehaviorState, SessionState, SeekingPhase)`。</para></summary>
    public static class ViewStateMap
    {
        /// <summary>`ViewState(p)`(GDD F-13.6 逐字落地)。</summary>
        public static PresentPatientState ViewState(BehaviorState behavior, SessionState session, SeekingPhase phase)
        {
            // 优先级 1:会诊中(边界层赐予;任一 BehaviorState 都成立 —— 住床上接受查体的病人报 InTreatment)
            if (session == SessionState.InTreatment) return PresentPatientState.InTreatment;

            // 优先级 2:倒地不起
            if (behavior == BehaviorState.Bedridden) return PresentPatientState.Collapsed;

            // 优先级 3:已在医馆等待接诊
            if (behavior == BehaviorState.Seeking && phase == SeekingPhase.AtClinic)
                return PresentPatientState.AwaitingCare;

            // 优先级 4:在场、可就诊
            return PresentPatientState.Present;
        }
    }
}
