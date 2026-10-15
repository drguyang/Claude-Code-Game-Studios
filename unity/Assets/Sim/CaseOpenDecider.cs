// 权威来源:GDD 规则二(立案 A/B 两路径 + 因果订正 + 开案唯一)
//          · ADR-009 §二(模拟态判据 —— 入流义务与触发者无关)
//          · ADR-008 §一(病例流路由口径)
//
// 立案决策纯函数:给定病人 p 的当前开案状态与一次就诊交互,返回决策结果。
// 不触 IEventSink,不触 IIdAuthority,只返回「该做什么」。
// 生产代码 —— 测试测的是本文件,不是测试自己的副本。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>立案触发路径。</summary>
    public enum CaseOpenSource
    {
        /// <summary>路径 A:52 事件注入(急召出诊 / 原型疫情)。</summary>
        A = 0,
        /// <summary>路径 B:13 自然就诊。</summary>
        B = 1
    }

    /// <summary>立案决策结果。</summary>
    public enum CaseOpenDecision
    {
        /// <summary>开新案 —— 病人当前无开案。</summary>
        OpenNew = 0,
        /// <summary>回到已开案 —— 病人已有开案,第二次交互不产生新案。</summary>
        ReturnExisting = 1,
        /// <summary>不立案 —— 药材短缺等无病人场景。</summary>
        NoCase = 2
    }

    /// <summary>
    /// 立案决策纯函数(GDD 规则二)。
    /// </summary>
    public static class CaseOpenDecider
    {
        /// <summary>
        /// 立案决策:给定病人 p 的当前开案状态与一次就诊交互,返回决策结果。
        /// </summary>
        /// <param name="hasOpenCase">病人当前是否有开案。</param>
        /// <param name="hasPatient">是否有病人(药材短缺 = false)。</param>
        /// <param name="sourceKind">触发路径(A=事件注入 / B=自然就诊)—— 只作记录,不改变决策。</param>
        /// <returns>决策结果。</returns>
        public static CaseOpenDecision Decide(bool hasOpenCase, bool hasPatient, CaseOpenSource sourceKind)
        {
            // 药材短缺不立案(无病人)
            if (!hasPatient)
                return CaseOpenDecision.NoCase;

            // 同一病人同时至多一个开案(GDD 规则二 2026-09-17 新增)
            if (hasOpenCase)
                return CaseOpenDecision.ReturnExisting;

            // 两条路径入流义务完全相同(ADR-009 §二 模拟态判据)
            return CaseOpenDecision.OpenNew;
        }
    }

    /// <summary>
    /// 流前缀查询:OpenCaseOf(p) := 最近 CaseOpened 且无其后 CaseClosed。
    /// GDD 规则二 Implementation Notes 第 2 条。
    /// </summary>
    public static class CaseStreamQuery
    {
        /// <summary>
        /// 查询病人 p 当前的开案 case_id。
        /// </summary>
        /// <param name="events">病例流事件列表(已按全序排列)。</param>
        /// <param name="patientId">病人 id。</param>
        /// <returns>开案 case_id;无开案返回 null。</returns>
        public static CaseId? OpenCaseOf(IReadOnlyList<SimEvent> events, int patientId)
        {
            CaseId? lastOpened = null;
            foreach (var e in events)
            {
                if (e.Patient.Value != patientId)
                    continue;

                if (e.Kind == EventKind.CaseOpened)
                {
                    lastOpened = new CaseId(e.Tick, e.Patient.Value, e.Seq);
                }
                else if (e.Kind == EventKind.CaseClosed)
                {
                    // 结案后 lastOpened 失效
                    lastOpened = null;
                }
            }
            return lastOpened;
        }

        /// <summary>
        /// 判断病人 p 当前是否有开案。
        /// </summary>
        public static bool HasOpenCase(IReadOnlyList<SimEvent> events, int patientId)
        {
            return OpenCaseOf(events, patientId).HasValue;
        }

        /// <summary>
        /// 计算 J(c) = 病例流中**该案**最后一条判断事件的 judgment(GDD 规则四①)。
        /// 存新值:全序最后一条胜,非差分合成。
        ///
        /// <para><b>归属判据 = 流序关联</b>(与 <see cref="OpenCaseOf"/> 同范式):逐条扫描,
        /// 以该病人的 <c>CaseOpened</c> 三元组确定「当下开案」,其后至 <c>CaseClosed</c> 之前的
        /// 判断事件即归属该案。**<paramref name="caseId"/> 是本案的唯一判据** ——
        /// 传**整条病例流**即可,不需要调用方预先切片。</para>
        ///
        /// <para>🔴 <b>2026-10-15 修复(sprint-11 S11-7 · 补评件 story-002 §二 BLK-8)</b>:
        /// 原实现的 <c>caseId</c> 形参**体内零引用** —— 只取「传入列表里最后一条判断事件」,
        /// 契约押在「调用方传入该案事件子集」上。而生产唯一调用方
        /// (<c>CompositionRoot</c> → <c>CasebookVitalsSource.InkAgeTicks</c>)传的是
        /// <c>EventStream.Events</c> = **全部混合流** ⇒ 墨龄**取到别的病人的判断事件**。
        /// 该契约在生产路径上**不可能被满足**(见下条),故**不是「调用方传错」而是接口本身失守**。</para>
        ///
        /// <para>⚠️ <b>为何不读载荷里的 <c>CaseId</c></b>:<c>JudgmentRecordedPayload.CaseId</c> 确实
        /// 存在,但 <c>SimEvent.Payload</c> 是 <see cref="PayloadRef"/>(BlobId/Offset/Length),
        /// **解码归 <c>Sim.Codec</c>** —— 本类住 <c>Sim</c>,引用集**恰 = {BCL, Sim.Contracts}**
        /// (ADR-025 §① / b2 门),**物理上够不着**。故走流序关联(承 S11-8 同款口径)。</para>
        ///
        /// <para><b>代价(与 S11-8 同款,如实登记)</b>:该口径下判断事件对案件的归属是**推断**而非
        /// **读取**;与载荷解码**在合法流上等价**,仅在违反「同一病人同时至多一个开案」
        /// (GDD 规则二)的**非法流**上分歧 —— 该流下写者行为本身未定义。</para>
        /// </summary>
        /// <param name="events">病例流事件列表(已按全序排列)。**传整条流**,本函数自按
        /// <paramref name="caseId"/> 过滤。</param>
        /// <param name="caseId">案件 case_id。</param>
        /// <returns>该案最后一条判断事件;该案无判断记录返回 null。</returns>
        public static SimEvent? LastJudgmentOf(IReadOnlyList<SimEvent> events, CaseId caseId)
        {
            SimEvent? last = null;
            bool inCase = false;

            foreach (var e in events)
            {
                // 只看该病人的事件(Patient 已是三元组的一维)
                if (e.Patient.Value != caseId.Patient)
                    continue;

                if (e.Kind == EventKind.CaseOpened)
                {
                    // 案件身份 = CaseOpened 的三元组(Patient 已匹配 ⇒ 比 Tick/Seq 即可)
                    inCase = e.Tick == caseId.Tick && e.Seq == caseId.Seq;
                    continue;
                }

                if (e.Kind == EventKind.CaseClosed)
                {
                    // 结案即离开本案 —— 其后的判断不再属于 caseId
                    inCase = false;
                    continue;
                }

                if (!inCase)
                    continue;

                if (e.Kind == EventKind.JudgmentRecorded || e.Kind == EventKind.JudgmentRevised)
                    last = e;
            }

            return last;
        }
    }
}
