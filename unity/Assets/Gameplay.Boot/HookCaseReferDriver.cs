// 转诊 / 送医的**装配层驱动** —— C3-a(2026-11-10 用户裁定「接进输入层(转诊键)」)
//
// 权威:
//   docs/architecture/adr-044-case-referred-kind.md §D7(**触发点本轮未接** —— 本类就是那条缝)
//   design/gdd/reviews/technique-unlock-decision-sheet-2026-11-10.md §3 C3(C3-a)
//   ADR-008 §一 病例流按 Kind 路由 · ADR-005 主机唯一 Append
//
// ── 本类只做一件 T5b 没做的事:把「按键」翻译成「哪一例」 ─────────────────
//   `HookCaseClosureWriter.TryRefer` 要 `CaseId`(三元组 Tick/Patient/Seq),
//   而按键只给出「最近在场病人」。本类从**病例流**取那个病人**当前开着的那一例**
//   (`CaseStreamQuery.OpenCaseOf` —— 全仓唯一判据,与 `CaseOpenWriter` / `JudgmentWriter` /
//   `FoldedPredicate` 条件③ 同一个读法)。
//   ⚠️ 这是读取,不是判据 —— 「这一例该不该转诊」仍归 37(本类只说「他现在有一例开着」)。
//   ⚠️ 取到的 caseId 只是**候选**:写者自己会对着真流再复核(`IsOpen` / `IsClosed` 按精确三元组),
//      所以本类不需要第二套「哪一案」判据。
//
// ⚠️ **不得**因为本类存在就声称「转诊已可用」:它接上了按键,但
//   ① 钩子病例的配额 / 可诊断性仍由 `HookCasePolicy` 判;
//   ② 本文只覆盖「转诊」,送医(`ReasonHospitalized`)今日**没有按键**。
//   证据里须逐条如实写。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>把一次「转诊键」翻译成一条 `CaseReferred`(病例流)。</summary>
    public sealed class HookCaseReferDriver
    {
        private readonly HookCaseClosureWriter _writer;
        private readonly Func<IReadOnlyList<SimEvent>> _events;
        private readonly Func<long> _tick;

        /// <param name="writer">37 的收场写者(唯一 Append 落点)。</param>
        /// <param name="events">事件流只读视图(取开着的病例)。</param>
        /// <param name="tick">当前 tick(<see cref="ITickProvider.CurrentTick"/>)。</param>
        /// <exception cref="ArgumentNullException">任一依赖为 null。</exception>
        /// <remarks>🔁 2026-11-10(T19):原第三参 <c>decoder</c> 已摘 —— 反查改用
        /// <c>CaseStreamQuery.OpenCaseOf</c>(它只读事件头,不解载荷),本类因此不再需要解码器。</remarks>
        public HookCaseReferDriver(HookCaseClosureWriter writer,
                                   Func<IReadOnlyList<SimEvent>> events,
                                   Func<long> tick)
        {
            _writer = writer ?? throw new ArgumentNullException(nameof(writer));
            _events = events ?? throw new ArgumentNullException(nameof(events));
            _tick = tick ?? throw new ArgumentNullException(nameof(tick));
        }

        /// <summary>
        /// 把该病人**当前开着的那一例**转诊出去。
        /// </summary>
        /// <param name="patient">病人(按键时的最近在场者;None ⇒ 直接 false)。</param>
        /// <param name="reason">理由闭集(见 <see cref="CaseReferredPayload"/>;缺省转诊)。</param>
        /// <returns><c>true</c> = 写出了一条;<c>false</c> = 没有开着的病例 / 已转诊过。</returns>
        public bool TryRefer(PatientId patient, int reason = CaseReferredPayload.ReasonTransferred)
        {
            if (patient.IsNone) return false;

            if (!TryFindOpenCase(patient, out CaseId caseId)) return false;
            return _writer.TryRefer(_tick(), patient, caseId, reason);
        }

        /// <summary>该病人**当前开着的那一例**(没有 ⇒ <c>false</c>)。</summary>
        /// <remarks>
        /// 🔁 **2026-11-10(T19)**:判据正文从本类搬走 —— 直接复用
        /// <see cref="CaseStreamQuery.OpenCaseOf"/>(全仓唯一读法:最后一条 <c>CaseOpened</c>
        /// 且其后无该病人的 <c>CaseClosed</c>)。本方法只是「同一判据的 bool 面」。
        /// </remarks>
        private bool TryFindOpenCase(PatientId patient, out CaseId caseId)
        {
            caseId = default;
            IReadOnlyList<SimEvent> events = _events();
            if (events == null) return false;

            CaseId? open = CaseStreamQuery.OpenCaseOf(events, patient.Value);
            if (!open.HasValue) return false;

            caseId = open.Value;
            return true;
        }
    }
}
