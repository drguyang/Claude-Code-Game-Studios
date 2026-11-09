// 钩子病例的**收场写者**(T5b · 2026-11-09 · ADR-044)
//
// 权威:docs/architecture/adr-044-case-referred-kind.md
//   §D2 写者 = 37(本类 = 37 在装配层的唯一 Append 落点)
//   §D3 载荷 (patient_id, case_id, reason),reason ∈ {0 转诊, 1 送医}
//   §D5 **不自动结案** —— 本类不写 CaseClosed
//   §D6 同一 case_id 至多一条(重复 = no-op)
//   §D7 真实调用点(玩家转诊交互)本轮不存在
//
// ⚠️ 与本文件同格的先例:`TechniqueUnlockWriter`(ADR-043 §D3/D6)——
//    同样「闭集校验 + 去重 + 唯一 Append」三件事,同样在宿主就位前先立好写者。
//    ⚠️ **不得**因为本类存在就声称「转诊可用」:没有调用方(ADR-044 §D7 如实登记)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>
    /// `CaseReferred` 的唯一写入通道(主机侧)。
    /// <para>宿主(42 的转诊交互 / 剧本)只调用本类,不直接 `IEventSink.Append`
    /// (ADR-005 主机唯一权威)。</para>
    /// </summary>
    public sealed class HookCaseClosureWriter
    {
        private readonly IEventSink _sink;
        private readonly IPayloadEncoder _encoder;
        private readonly Func<IReadOnlyList<SimEvent>> _events;
        private readonly IPayloadDecoder _decoder;

        /// <param name="sink">主机事件写入通道(唯一 Append 落点)。</param>
        /// <param name="encoder">载荷编码器(ADR-029 唯一路径)。</param>
        /// <param name="events">事件流只读视图(去重扫描;与既有写者同格)。</param>
        /// <param name="decoder">载荷解码器(ADR-031)。</param>
        /// <exception cref="ArgumentNullException">任一依赖为 null。</exception>
        public HookCaseClosureWriter(IEventSink sink, IPayloadEncoder encoder,
                                     Func<IReadOnlyList<SimEvent>> events, IPayloadDecoder decoder)
        {
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
            _encoder = encoder ?? throw new ArgumentNullException(nameof(encoder));
            _events = events ?? throw new ArgumentNullException(nameof(events));
            _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
        }

        /// <summary>
        /// 把一次「转诊 / 送医」写进病例流(同一 case 重复调用 = no-op)。
        /// </summary>
        /// <param name="tick">判定时刻(事件头部 Tick)。</param>
        /// <param name="patient">病人 id(非哨兵)。</param>
        /// <param name="caseId">被转诊的病例。</param>
        /// <param name="reason"><see cref="CaseReferredPayload.ReasonTransferred"/> 或
        /// <see cref="CaseReferredPayload.ReasonHospitalized"/>。</param>
        /// <returns><c>true</c> = 真的写了一条;<c>false</c> = 该病例已转诊过(no-op)。</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="tick"/> 为负,或
        /// <paramref name="reason"/> 不在闭集内(报文点名合法值)。</exception>
        /// <exception cref="ArgumentException"><paramref name="patient"/> 是哨兵。</exception>
        public bool TryRefer(long tick, PatientId patient, CaseId caseId, int reason)
        {
            if (tick < 0) throw new ArgumentOutOfRangeException(nameof(tick), tick, "tick 不得为负");
            if (patient.IsNone)
                throw new ArgumentException(
                    "转诊是病人级事实 —— PatientId.None(-1)是世界级事件哨兵,不得作转诊对象",
                    nameof(patient));
            if (reason != CaseReferredPayload.ReasonTransferred
                && reason != CaseReferredPayload.ReasonHospitalized)
            {
                throw new ArgumentOutOfRangeException(nameof(reason), reason,
                    $"reason 必须是 {CaseReferredPayload.ReasonTransferred}(转诊) 或 " +
                    $"{CaseReferredPayload.ReasonHospitalized}(送医) —— 闭集外理由会让转诊单不可归因");
            }

            if (AlreadyReferred(caseId)) return false;

            var payload = new CaseReferredPayload(patient.Value, caseId, reason);
            _sink.Append(new SimEvent(
                tick, patient, -1, EventKind.CaseReferred,
                _encoder.Encode(EventKind.CaseReferred, payload)));
            return true;
        }

        /// <summary>该病例是否已有一条 `CaseReferred`(ADR-044 §D6)。</summary>
        private bool AlreadyReferred(CaseId caseId)
        {
            IReadOnlyList<SimEvent> events = _events();
            if (events == null) return false;

            for (int i = 0; i < events.Count; i++)
            {
                SimEvent e = events[i];
                if (e.Kind != EventKind.CaseReferred) continue;

                CaseReferredPayload p = _decoder.Decode<CaseReferredPayload>(e.Kind, e.Payload);
                if (p.CaseId == caseId) return true;
            }
            return false;
        }
    }
}
