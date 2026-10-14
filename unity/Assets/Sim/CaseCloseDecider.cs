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
        /// <param name="isOpen">案件是否开着(**流派生**:开过 ∧ ¬已结)。</param>
        /// <param name="isClosed">案件是否已结(**流派生**)。</param>
        /// <param name="hasTreatmentInWindow">窗口内是否有处置事件。</param>
        /// <param name="isChecked">玩家是否勾选「已处置」。</param>
        /// <returns>决策结果。</returns>
        public static CaseCloseDecision Decide(bool isOpen, bool isClosed, bool hasTreatmentInWindow, bool isChecked)
        {
            // 🔴 2026-10-15 修复(sprint-11 S11-8 · 补评件 story-003 · AC-37-03 面):两判序**已对调**。
            //
            // 契约:两 bool 由**同一流**派生(见 CaseCloseQuery)⇒ isOpen = 开过 ∧ ¬isClosed。
            //   (false, true)  = 开过且已结 —— 🔴 修前此组合是**正常生产路径**(结案后玩家再点),
            //                    而旧序 `!isOpen` 在前 ⇒ 已结案被报成「未开」(错因码)。
            //   (false, false) = 从未开过。
            //   (true,  true)  = **不可能**(isOpen 已蕴含 ¬isClosed)⇒ 无需处理、不再可达。
            // 「已结」比「未开」更具体(已结蕴含曾开)⇒ 具体者先判。
            //
            // ⚠️ 结果面零变化:两分支都是**拒绝且不产生事件**(GDD 规则六-4 的唯一要求);
            //    改动只在**理由码**。既有全部调用点行为不变(唯一受影响的组合恰是上表 (false,true))。

            // 案件已结 → 幂等拒收(AC-37-03)
            if (isClosed)
                return CaseCloseDecision.RejectAlreadyClosed;

            // 案件未开 → 拒绝
            if (!isOpen)
                return CaseCloseDecision.RejectNotOpen;

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
        /// 案件是否已结(流前缀纯函数)。
        ///
        /// <para><b>关联口径 = 流序扫描</b>(与 <see cref="CaseStreamQuery.OpenCaseOf"/> 同口径):
        /// 病例流是**全序**的(ADR-008 §一),故「某条 <c>CaseClosed</c> 属于哪个案件」由
        /// **其前最近一条该病人的 <c>CaseOpened</c>** 确定 —— 与「全序前缀纯函数」的纪律一致。</para>
        ///
        /// <para>⚠️ <b>为何不解码载荷</b>:<c>CaseClosedPayload.CaseId</c> 是权威关联字段,
        /// 但 <c>PayloadCodec</c> 住 <c>Sim.Codec</c>,而本类住 <c>Sim</c> —— 引用集恰 =
        /// {BCL, <c>Sim.Contracts</c>}(ADR-025 §① / b2 门),<c>PayloadRef</c> 仅
        /// <c>BlobId/Offset/Length</c>,**物理上够不着**(同 <c>PoiStateMachine</c> 的处置:
        /// 解码归调用方)。故本处取流序关联。</para>
        ///
        /// <para>⚠️ <b>等价性前置</b>:本口径在**合法流**上与载荷解码等价;二者只在违反
        /// 「同一病人同时至多一个开案」(GDD 规则二)的非法流上分歧 ——
        /// 而该流下写者行为本身未定义 ⇒ 分歧面不在契约内。</para>
        ///
        /// <para>🔴 <b>2026-10-15 修复(sprint-11 S11-8 · 补评件 story-003 §二 B-1)</b>:
        /// 原实现 `if (e.Patient.Value == caseId.Patient) return true;` 把「该病人**曾有过**任何
        /// <c>CaseClosed</c>」当作「**本** case_id 已结」⇒ 复诊新案被判已结,
        /// <c>Decide</c> 走 `RejectAlreadyClosed` ⇒ **复诊案永远无法结案**
        /// (GDD `:250-256` 幂等定义 = **同一** case_id;EC-37-1 复诊 = 新病例)。</para>
        /// </summary>
        /// <param name="caseEvents">病例流事件列表(已按全序 <c>(Tick, Patient, Seq)</c> 排列)。</param>
        /// <param name="caseId">案件 case_id。</param>
        /// <returns>是否已结。</returns>
        public static bool IsClosed(IReadOnlyList<SimEvent> caseEvents, CaseId caseId)
        {
            bool hasCurrent = false;
            var current = default(CaseId);

            foreach (var e in caseEvents)
            {
                if (e.Patient.Value != caseId.Patient)
                    continue;

                if (e.Kind == EventKind.CaseOpened)
                {
                    current = new CaseId(e.Tick, e.Patient.Value, e.Seq);
                    hasCurrent = true;
                }
                else if (e.Kind == EventKind.CaseClosed)
                {
                    // 本条 CaseClosed 归属 current(流序关联);结案后该病人回到「无开案」
                    if (hasCurrent && current.Equals(caseId))
                        return true;
                    hasCurrent = false;
                }
            }
            return false;
        }

        /// <summary>
        /// 案件是否开着 := 该案**开过** ∧ **未结**(GDD 规则六-4:结案不可撤销、不重开)。
        ///
        /// <para>与 <see cref="IsClosed"/> **共用同一契约与同一前置**(入参 = 病例流全序前缀)。
        /// 🔴 2026-10-15 修复(S11-8 · 补评件 ADVISORY A-6):两函数此前对**同一形参**
        /// <c>caseEvents</c> 隐含**不同**输入契约 —— 一个的注释要求「该案子集」、另一个按整表三元组查找,
        /// 是 B-1 的土壤;现统一为「整条全序流」,并由同一扫描派生,结构上不可再分叉。</para>
        /// </summary>
        /// <param name="caseEvents">病例流事件列表(已按全序 <c>(Tick, Patient, Seq)</c> 排列)。</param>
        /// <param name="caseId">案件 case_id。</param>
        /// <returns>是否开着。</returns>
        public static bool IsOpen(IReadOnlyList<SimEvent> caseEvents, CaseId caseId)
        {
            return OpenedInStream(caseEvents, caseId) && !IsClosed(caseEvents, caseId);
        }

        /// <summary>该案是否在流中**开过**(精确三元组匹配 <c>CaseOpened</c>)。
        /// 守卫「从未开过的 caseId」不得被 <see cref="IsOpen"/> 判为真。</summary>
        private static bool OpenedInStream(IReadOnlyList<SimEvent> caseEvents, CaseId caseId)
        {
            foreach (var e in caseEvents)
            {
                if (e.Kind != EventKind.CaseOpened)
                    continue;
                if (e.Tick == caseId.Tick && e.Patient.Value == caseId.Patient && e.Seq == caseId.Seq)
                    return true;
            }
            return false;
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
