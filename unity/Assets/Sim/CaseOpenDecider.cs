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
        /// 计算 J(c) = 病例流中该案最后一条判断事件的 judgment(GDD 规则四①)。
        /// 存新值:全序最后一条胜,非差分合成。
        /// </summary>
        /// <param name="events">病例流事件列表(已按全序排列)。</param>
        /// <param name="caseId">案件 case_id。</param>
        /// <returns>最后一条判断事件;无判断记录返回 null。</returns>
        public static SimEvent? LastJudgmentOf(IReadOnlyList<SimEvent> events, CaseId caseId)
        {
            SimEvent? last = null;
            foreach (var e in events)
            {
                if (e.Kind != EventKind.JudgmentRecorded && e.Kind != EventKind.JudgmentRevised)
                    continue;

                // 判断记录引用了 case_id —— 通过载荷匹配
                // 本函数假设调用方已按 case_id 过滤,或事件列表只含该案的判断记录
                // 实际生产代码应通过 PayloadRef 解码 case_id 匹配
                // P0 简化:调用方负责传入该案的事件子集
                last = e;
            }
            return last;
        }
    }
}
