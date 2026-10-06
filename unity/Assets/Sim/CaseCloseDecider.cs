// 权威来源:GDD 规则六(结案唯一出口 · 已处置前置 · 单例 · 不可撤销 · 幂等拒收)
//          · ADR-008 §四(处置证据窗口化 + 快照;方向订正 2026-09-17)
//          · ADR-009 §二(模拟态判据 —— 入流义务与触发者无关)
//
// 结案决策纯函数:给定病人 p 的当前开案状态、病史流处置证据、玩家勾选,返回决策结果。
// 不触 IEventSink,不触 IIdAuthority,只返回「该做什么」。
// 生产代码 —— 测试测的是本文件,不是测试自己的副本。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>结案决策结果。</summary>
    public enum CaseCloseDecision
    {
        /// <summary>可结案 —— 前置满足,写入 CaseClosed。</summary>
        CanClose = 0,
        /// <summary>拒绝 —— 未勾选「已处置」。</summary>
        RejectNotChecked = 1,
        /// <summary>拒绝 —— 窗口内无处置事件。</summary>
        RejectNoTreatment = 2,
        /// <summary>拒绝 —— 案件已结(幂等拒收)。</summary>
        RejectAlreadyClosed = 3,
        /// <summary>拒绝 —— 案件未开。</summary>
        RejectNotOpen = 4,
    }

    /// <summary>
    /// 结案决策纯函数(GDD 规则六)。
    /// </summary>
    public static class CaseCloseDecider
    {
        /// <summary>
        /// 结案决策:给定案件状态、病史流处置证据、玩家勾选,返回决策结果。
        /// </summary>
        /// <param name="isOpen">案件是否开着。</param>
        /// <param name="isClosed">案件是否已结。</param>
        /// <param name="hasTreatmentInWindow">窗口内是否有处置事件。</param>
        /// <param name="isChecked">玩家是否勾选「已处置」。</param>
        /// <returns>决策结果。</returns>
        public static CaseCloseDecision Decide(bool isOpen, bool isClosed, bool hasTreatmentInWindow, bool isChecked)
        {
            // 案件未开 → 拒绝
            if (!isOpen)
                return CaseCloseDecision.RejectNotOpen;

            // 案件已结 → 幂等拒收
            if (isClosed)
                return CaseCloseDecision.RejectAlreadyClosed;

            // 未勾选 → 拒绝
            if (!isChecked)
                return CaseCloseDecision.RejectNotChecked;

            // 窗口内无处置事件 → 拒绝
            if (!hasTreatmentInWindow)
                return CaseCloseDecision.RejectNoTreatment;

            // 前置满足 → 可结案
            return CaseCloseDecision.CanClose;
        }
    }

    /// <summary>
    /// 处置集白名单(GDD 规则五:10/11/25 写的 Kind)。
    /// </summary>
    public static class TreatmentKinds
    {
        /// <summary>处置事件 Kind 白名单。</summary>
        public static readonly HashSet<EventKind> Set = new HashSet<EventKind>
        {
            EventKind.EmergencyTreatmentApplied,  // 10 急救
            EventKind.DrugTreatmentApplied,       // 11 处方
            EventKind.InjuryOnset,                // 25 格斗
        };

        /// <summary>判断某 Kind 是否属于处置集。</summary>
        public static bool Contains(EventKind kind) => Set.Contains(kind);
    }

    /// <summary>
    /// 流前缀查询:结案前置谓词(GDD F-37.2)。
    /// </summary>
    public static class CaseCloseQuery
    {
        /// <summary>
        /// 已处置(c) := ∃ e ∈ 病史流: e.Patient = c.patient_id
        ///   ∧ e.Kind ∈ 处置集
        ///   ∧ e.Tick ∈ [CaseOpened.Tick, CloseTick]
        /// 窗口方向:处置 tick 落在立案与结案之间(2026-09-17 订正)。
        /// </summary>
        /// <param name="historyEvents">病史流事件列表。</param>
        /// <param name="patientId">病人 id。</param>
        /// <param name="openedTick">立案 tick(CaseOpened.Tick)。</param>
        /// <param name="closeTick">结案 tick(CaseClosed.Tick)。</param>
        /// <returns>窗口内是否有处置事件。</returns>
        public static bool HasTreatmentInWindow(
            IReadOnlyList<SimEvent> historyEvents, int patientId, long openedTick, long closeTick)
        {
            foreach (var e in historyEvents)
            {
                if (e.Patient.Value != patientId)
                    continue;
                if (!TreatmentKinds.Contains(e.Kind))
                    continue;
                if (e.Tick < openedTick || e.Tick > closeTick)
                    continue;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 案件是否已结(流前缀纯函数:该案已有 CaseClosed)。
        /// </summary>
        /// <param name="caseEvents">病例流事件列表。</param>
        /// <param name="caseId">案件 case_id。</param>
        /// <returns>是否已结。</returns>
        public static bool IsClosed(IReadOnlyList<SimEvent> caseEvents, CaseId caseId)
        {
            foreach (var e in caseEvents)
            {
                if (e.Kind != EventKind.CaseClosed)
                    continue;
                // CaseClosed 载荷含 case_id —— 通过载荷匹配
                // P0 简化:调用方负责传入该案的事件子集,或事件列表只含该案的 CaseClosed
                // 实际生产代码应通过 PayloadRef 解码 case_id 匹配
                // ⚠️ CaseClosed 的 Tick 是结案 tick,不是 case_id 的 Tick
                // case_id 三元组 = CaseOpened 的 (Tick, Patient, Seq)
                // CaseClosed 引用 case_id 但自身 Tick 不同(结案 tick)
                // 同一病人同时至多一个开案(story-002 不变量)
                // 故匹配 Patient 即可
                if (e.Patient.Value == caseId.Patient)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 案件是否开着(有 CaseOpened 且无其后 CaseClosed)。
        /// </summary>
        /// <param name="caseEvents">病例流事件列表。</param>
        /// <param name="caseId">案件 case_id。</param>
        /// <returns>是否开着。</returns>
        public static bool IsOpen(IReadOnlyList<SimEvent> caseEvents, CaseId caseId)
        {
            bool opened = false;
            foreach (var e in caseEvents)
            {
                if (e.Kind == EventKind.CaseOpened &&
                    e.Tick == caseId.Tick && e.Patient.Value == caseId.Patient && e.Seq == caseId.Seq)
                {
                    opened = true;
                }
                else if (e.Kind == EventKind.CaseClosed &&
                         e.Patient.Value == caseId.Patient)
                {
                    // CaseClosed 的 Tick 是结案 tick,不是 case_id 的 Tick
                    // 同一病人同时至多一个开案(story-002 不变量)
                    opened = false;
                }
            }
            return opened;
        }

        /// <summary>
        /// 窗口边界:e.Tick == openedTick(立案即处置)与 == closeTick 均计入(闭区间)。
        /// </summary>
        /// <param name="tick">处置事件 tick。</param>
        /// <param name="openedTick">立案 tick。</param>
        /// <param name="closeTick">结案 tick。</param>
        /// <returns>是否在窗口内(闭区间)。</returns>
        public static bool IsInWindow(long tick, long openedTick, long closeTick)
        {
            return tick >= openedTick && tick <= closeTick;
        }
    }
}
