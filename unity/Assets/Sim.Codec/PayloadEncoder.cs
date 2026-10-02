// ADR-029 §①/§③ —— IPayloadEncoder 的实现(住 Sim.Codec)。
//
// 权威来源:
//   ADR-029 §① —— 乙案:编码 + 入池一体,返回 PayloadRef
//   ADR-029 §② —— IBlobSink 写面,经构造注入
//   ADR-029 §Implementation Guidelines —— 分派**镜像既有 DecodeBoxed**;
//     接受瞬时装箱(与既有解码路径同款);不走 kindgen
//   ADR-006 Amendment G-2 —— 载荷字节住不可变 blob 池
//   ADR-006 Amendment G-3 —— freehand_text 迁入 blob 变长段;sim 消费面不持有该值
//
// 分派完备性(ADR-029 §① 修正 ④,用户 2026-10-02 裁定):
//   34 支具名 Encode 重载中 **32 支**可经 Encode<T> 表达;余 2 支
//   (JudgmentRecorded / JudgmentRevised)多收一个 `string freehandText` ——
//   该值**不在 T 里**(struct 故意不含,承 G-3「sim 消费面不持有该值」),
//   故本实现对其**抛 NotSupportedException**(错误串指向具名重载)。
//   不给 Encode<T> 加 string 形参的理由:G-3 既已禁 sim **读** freehand_text,
//   sim 侧的通用编码面更不应**承载**它。

using System;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.Codec
{
    /// <summary>
    /// 载荷编码器 —— 编码 + 入池,返回 <see cref="PayloadRef"/>。
    /// </summary>
    /// <remarks>
    /// <para><b>瞬时装箱</b>:分派经 <c>object</c>(镜像 <c>PayloadCodec.DecodeBoxed</c>)。
    /// 事件率上界 = tick 频率(20 Hz,ADR-009 §六),每次一条 alloc 无实质影响
    /// —— ADR-029 §① 修正 ① 明载「接受瞬时装箱」。</para>
    /// </remarks>
    public sealed class PayloadEncoder : IPayloadEncoder
    {
        private readonly IBlobSink _blobSink;

        /// <summary>构造编码器。</summary>
        /// <param name="blobSink">blob 池写面(实现者 = 池的所有者:7a / 45)。</param>
        public PayloadEncoder(IBlobSink blobSink)
        {
            _blobSink = blobSink ?? throw new ArgumentNullException(nameof(blobSink));
        }

        /// <inheritdoc />
        public PayloadRef Encode<T>(EventKind kind, in T payload) where T : struct
        {
            byte[] bytes = EncodeBoxed(kind, payload);
            int blobId = _blobSink.Store(bytes);
            return new PayloadRef(blobId, 0, bytes.Length);
        }

        /// <summary>
        /// 按 Kind 分派到 <see cref="PayloadCodec"/> 的具名 <c>Encode</c> 重载。
        /// 镜像 <c>PayloadCodec.DecodeBoxed</c>(ADR-029 §Implementation Guidelines)。
        /// </summary>
        /// <exception cref="InvalidOperationException">载荷类型与 Kind 不匹配(编程错误)。</exception>
        /// <exception cref="NotSupportedException">Judgment 两支 —— 须经具名重载(见类头注)。</exception>
        private static byte[] EncodeBoxed(EventKind kind, object payload)
        {
            switch (kind)
            {
                // ── 病史流(13)────────────────────────────────────────────
                case EventKind.InjuryOnset:               return PayloadCodec.Encode((InjuryOnsetPayload)payload);
                case EventKind.CompoundTriggered:         return PayloadCodec.Encode((CompoundTriggeredPayload)payload);
                case EventKind.CompoundExpired:           return PayloadCodec.Encode((CompoundExpiredPayload)payload);
                case EventKind.CareApplied:               return PayloadCodec.Encode((CareAppliedPayload)payload);
                case EventKind.EmergencyAttempt:          return PayloadCodec.Encode((EmergencyAttemptPayload)payload);
                case EventKind.EmergencyTreatmentApplied: return PayloadCodec.Encode((EmergencyTreatmentAppliedPayload)payload);
                case EventKind.DrugTreatmentApplied:      return PayloadCodec.Encode((DrugTreatmentAppliedPayload)payload);
                case EventKind.SkillGrown:                return PayloadCodec.Encode((SkillGrownPayload)payload);
                case EventKind.EventRolled:               return PayloadCodec.Encode((EventRolledPayload)payload);
                case EventKind.EventArrived:              return PayloadCodec.Encode((EventArrivedPayload)payload);
                case EventKind.ThreatDeferred:            return PayloadCodec.Encode((ThreatDeferredPayload)payload);
                case EventKind.ThreatDeferralCleared:     return PayloadCodec.Encode((ThreatDeferralClearedPayload)payload);
                case EventKind.HistoryFlagChanged:        return PayloadCodec.Encode((HistoryFlagChangedPayload)payload);

                // ── 病例流(5 —— 其中 2 支为 freehand_text 例外,见下)────────
                case EventKind.CaseOpened:                return PayloadCodec.Encode((CaseOpenedPayload)payload);
                case EventKind.CaseClosed:                return PayloadCodec.Encode((CaseClosedPayload)payload);
                case EventKind.PatternRecognized:         return PayloadCodec.Encode((PatternRecognizedPayload)payload);

                // ── 世界流(16)────────────────────────────────────────────
                case EventKind.ActorCellEntered:          return PayloadCodec.Encode((ActorCellEnteredPayload)payload);
                case EventKind.ResourceHarvested:         return PayloadCodec.Encode((ResourceHarvestedPayload)payload);
                case EventKind.DropSpawned:               return PayloadCodec.Encode((DropSpawnedPayload)payload);
                case EventKind.DropClaimed:               return PayloadCodec.Encode((DropClaimedPayload)payload);
                case EventKind.DropDespawned:             return PayloadCodec.Encode((DropDespawnedPayload)payload);
                case EventKind.Craft:                     return PayloadCodec.Encode((CraftPayload)payload);
                case EventKind.StructurePlaced:           return PayloadCodec.Encode((StructurePlacedPayload)payload);
                case EventKind.StructureModified:         return PayloadCodec.Encode((StructureModifiedPayload)payload);
                case EventKind.StructureRemoved:          return PayloadCodec.Encode((StructureRemovedPayload)payload);
                case EventKind.PoiStateChanged:           return PayloadCodec.Encode((PoiStateChangedPayload)payload);
                case EventKind.EnemyInjuryOnset:          return PayloadCodec.Encode((EnemyInjuryOnsetPayload)payload);
                case EventKind.InjuryStateChanged:        return PayloadCodec.Encode((InjuryStateChangedPayload)payload);
                case EventKind.EncounterStarted:          return PayloadCodec.Encode((EncounterStartedPayload)payload);
                case EventKind.EncounterEnded:            return PayloadCodec.Encode((EncounterEndedPayload)payload);
                case EventKind.ConsequenceResolved:       return PayloadCodec.Encode((ConsequenceResolvedPayload)payload);
                case EventKind.PlayerDied:                return PayloadCodec.Encode((PlayerDiedPayload)payload);

                // ── 2 支显式不支持(ADR-029 §① 修正 ④)──────────────────────
                // 这两支的具名重载多收 `string freehandText`(ADR-006 G-3 的 blob 变长段),
                // 该值不在 T 里 ⇒ Encode<T> 无从表达。sim 侧不承载自由文本(G-3),
                // 故此处显式抛,而非静默 default。
                case EventKind.JudgmentRecorded:
                    throw new NotSupportedException(
                        "JudgmentRecorded 的载荷需 freehandText(ADR-006 G-3 的 blob 变长段)," +
                        "不在 T 里 ⇒ IPayloadEncoder 不承载。请经具名重载:" +
                        "PayloadCodec.Encode(in JudgmentRecordedPayload, string freehandText)。" +
                        "(写者归 37 case-system 的 GDD 轮另裁。)");
                case EventKind.JudgmentRevised:
                    throw new NotSupportedException(
                        "JudgmentRevised 的载荷需 freehandText(ADR-006 G-3 的 blob 变长段)," +
                        "不在 T 里 ⇒ IPayloadEncoder 不承载。请经具名重载:" +
                        "PayloadCodec.Encode(in JudgmentRevisedPayload, string freehandText)。" +
                        "(写者归 37 case-system 的 GDD 轮另裁。)");

                default:
                    throw new InvalidOperationException($"未知 EventKind={(int)kind} —— 不在 34 支闭集内");
            }
        }
    }
}
