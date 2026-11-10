// 转诊 / 送医的**装配层驱动** —— C3-a(2026-11-10 用户裁定「接进输入层(转诊键)」)
//
// 权威:
//   docs/architecture/adr-044-case-referred-kind.md §D7(**触发点本轮未接** —— 本类就是那条缝)
//   design/gdd/reviews/technique-unlock-decision-sheet-2026-11-10.md §3 C3(C3-a)
//   ADR-008 §一 病例流按 Kind 路由 · ADR-005 主机唯一 Append
//
// ── 本类只做一件 T5b 没做的事:把「按键」翻译成「哪一例」 ─────────────────
//   `HookCaseClosureWriter.TryRefer` 要 `CaseId`(三元组 Tick/Patient/Seq),
//   而按键只给出「最近在场病人」。本类从**病例流**反查那个病人**当前开着的那一例**:
//   扫一遍流,取该病人**最后一条 `CaseOpened`**,且该 case 没有对应的 `CaseClosed`。
//   ⚠️ 这是读取,不是判据 —— 「这一例该不该转诊」仍归 37(本类只说「他现在有一例开着」)。
//
// ⚠️ **不得**因为本类存在就声称「转诊已可用」:它接上了按键,但
//   ① 钩子病例的配额 / 可诊断性仍由 `HookCasePolicy` 判;
//   ② 本文只覆盖「转诊」,送医(`ReasonHospitalized`)今日**没有按键**。
//   证据里须逐条如实写。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>把一次「转诊键」翻译成一条 `CaseReferred`(病例流)。</summary>
    public sealed class HookCaseReferDriver
    {
        private readonly HookCaseClosureWriter _writer;
        private readonly Func<IReadOnlyList<SimEvent>> _events;
        private readonly IPayloadDecoder _decoder;
        private readonly Func<long> _tick;

        /// <param name="writer">37 的收场写者(唯一 Append 落点)。</param>
        /// <param name="events">事件流只读视图(反查开着的病例)。</param>
        /// <param name="decoder">载荷解码器(ADR-031)。</param>
        /// <param name="tick">当前 tick(<see cref="ITickProvider.CurrentTick"/>)。</param>
        /// <exception cref="ArgumentNullException">任一依赖为 null。</exception>
        public HookCaseReferDriver(HookCaseClosureWriter writer,
                                   Func<IReadOnlyList<SimEvent>> events,
                                   IPayloadDecoder decoder,
                                   Func<long> tick)
        {
            _writer = writer ?? throw new ArgumentNullException(nameof(writer));
            _events = events ?? throw new ArgumentNullException(nameof(events));
            _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
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
        /// <para>判据 = 「最后一条 `CaseOpened(patient)`」且「没有任何 `CaseClosed` 的
        /// `CaseId` 等于它」。用**最后一条**而不是第一条:同一病人可以先后立案多次,
        /// 只有最新那例是「现在开着的」。</para>
        /// <para>⚠️ `CaseId` 的三元组 = (立案事件 Tick, 病人, 立案事件 Seq) ——
        /// 它是**从事件头读出来的**,不是另一处存的(ADR-006 Amendment A 的全序键)。</para>
        /// </remarks>
        private bool TryFindOpenCase(PatientId patient, out CaseId caseId)
        {
            caseId = default;

            IReadOnlyList<SimEvent> events = _events();
            if (events == null) return false;

            var closed = new HashSet<CaseId>();
            SimEvent? lastOpened = null;

            for (int i = 0; i < events.Count; i++)
            {
                SimEvent e = events[i];

                if (e.Kind == EventKind.CaseOpened)
                {
                    CaseOpenedPayload p = _decoder.Decode<CaseOpenedPayload>(e.Kind, e.Payload);
                    if (p.PatientId == patient.Value) lastOpened = e;
                    continue;
                }

                if (e.Kind == EventKind.CaseClosed)
                {
                    CaseClosedPayload p = _decoder.Decode<CaseClosedPayload>(e.Kind, e.Payload);
                    closed.Add(p.CaseId);
                }
            }

            if (lastOpened == null) return false;

            SimEvent opened = lastOpened.Value;
            caseId = new CaseId(opened.Tick, opened.Patient.Value, opened.Seq);
            return !closed.Contains(caseId);
        }
    }
}
