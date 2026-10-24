// 延迟威胁槽位写者 —— `ThreatDeferred` / `ThreatDeferralCleared` 的生产写入通道(52 随机事件导演)。
//
// 权威来源:
//   ADR-007 §三 —— 承重条款:DeferredThreatSlot 是**状态**,必须进流
//     (留在可变对象里 ⇒ 主机迁移后静默丢失 / 重复补发;AC-52-46 直接守这条)
//   ADR-007 §四 —— 本两 Kind 是**世界级事件**:`Patient = PatientId.None` 是**必须**
//     (与 `CareApplied` 的「None 拒收」方向相反 —— 那是病人侧事件,本两 Kind 是世界侧)
//   design/registry/entities.yaml ThreatDeferred / ThreatDeferralCleared constraint ——
//     落病史流 · Patient = None · 载荷 { slot_index, event_key } / { slot_index, reason }
//     ·「槽集状态由全序流重构(派生,不另存)」· 写者 = 52,主机唯一 Append
//   GDD random-events.md 规则六之三(:284-305)—— 入槽 / 当日内离开补发 / 跨日作废三语义
//   ADR-029 §③ —— 载荷唯一编码路径 = IPayloadEncoder(本类内禁 `new PayloadRef(`)
//   TD-randomevents-01 / S19-4 —— 本类的兑现轮(2026-10-25);WriterExistenceGate
//     两 Kind 豁免同批撤除(判据①「已存在写者的 Kind 仍挂豁免表 = 红」)
//
// 核心机制:
//   - 前置条件在**写者内**复核(不信任调用方转述),任一不满足 ⇒ **零写入**并返回
//     具名结果码(CareAppliedWriter 同格,不静默):
//       ① slotIndex ∈ [0, deferMax)(有界性:挂起槽有限集合;超界拒收)
//       ② eventKey ≥ 0(种类界下界;上界 = 事件表体,52 的表成文轮落)
//       ③ reason ∈ {0=补发, 1=作废}(ADR-007 §三 原注「reason = 补发 | 作废」的两值闭集)
//   - 「同 slot 已占用不得再挂 / 未挂起不得清」是**流层不变量**(一占一释配对),
//     由折叠器执行(`EventBudget.FoldDeferEvent` 的 fail-loud 防御)—— 写者不读流、
//     不做占用簿记(那是第二真源的形态,S19-4 判据①刚拆掉,不得从写者后门回来)。
//
// ⚠️ **reason 序数(0=补发 / 1=作废)是落地约定,本件首定**:ADR-007 §三 与 entities.yaml
//   均只写「补发 | 作废」两值,**未写序数**。本类以转发常量 `ReasonReissued` / `ReasonVoided`
//   承载(不散落裸字面量);若 registry 将来补出序数定义,须三处齐
//   (① registry ② 本类常量 ③ 机械断言 —— CareApplied/CarePhase 的三处权威先例)。
//
// ⚠️ 跨日作废(AC-52-48)= 52 的 tick 边界循环对本类 `TryClear` 的逐槽调用
//   (reason = `ReasonVoided`);内存清槽不是直接调用 —— 槽表由 `EventBudget.FoldDeferEvent`
//   折叠本写者产出的流而更新(第二真源已拆)。
//
// ⚠️ **有写者 ≠ 已接线**:生产触发点(52 的导演主循环:抽中威胁 ∧ 医馆 ∧ 同日 → TryDefer;
//   玩家离开医馆 → TryClear(补发);跨日 tick → 逐槽 TryClear(作废))归 52 的实现轮。
//   本类只保证「谁调用它,流里就有一条合规的 ThreatDeferred / ThreatDeferralCleared」。
//   `IEventAuthority.IsHost` 门不在本件:与既有写者同格 —— 主机唯一由「谁调用写者」保证(ADR-005)。

using System;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary><see cref="DeferredThreatSlotWriter.TryDefer"/> / <see cref="DeferredThreatSlotWriter.TryClear"/>
    /// 的结果码(零写入分支亦具名,不静默)。</summary>
    public enum DeferredThreatWriteResult
    {
        /// <summary>已写入一条事件。</summary>
        Applied,
        /// <summary>slotIndex 越界 [0, deferMax) —— 拒收。</summary>
        SlotOutOfRange,
        /// <summary>eventKey &lt; 0 —— 拒收。</summary>
        InvalidEventKey,
        /// <summary>reason ∉ {0=补发, 1=作废} —— 拒收。</summary>
        InvalidReason
    }

    /// <summary>
    /// 延迟威胁槽位写者(52;两 Kind 共一状态机,故共一写者类)。
    /// </summary>
    public sealed class DeferredThreatSlotWriter
    {
        /// <summary>reason:补发(当日内玩家离开医馆 ⇒ 立即补发该威胁;规则六之三)。</summary>
        public const int ReasonReissued = 0;

        /// <summary>reason:作废(跨日未补发 ⇒ 该 slot 作废,不结转;规则六之三 / AC-52-48)。</summary>
        public const int ReasonVoided = 1;

        private readonly IEventSink _eventSink;
        private readonly IPayloadEncoder _encoder;

        /// <summary>构造(依赖注入;主机唯一 Append 由「谁调用本类」保证 —— ADR-005)。</summary>
        public DeferredThreatSlotWriter(IEventSink eventSink, IPayloadEncoder encoder)
        {
            _eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
            _encoder = encoder ?? throw new ArgumentNullException(nameof(encoder));
        }

        /// <summary>
        /// 挂起一条威胁(规则六之三:威胁档 ∧ 在医馆 ∧ 同日 ⇒ 记入挂起槽)。
        /// </summary>
        /// <param name="tick">压制发生的那一 tick(只进事件头;payload 无 tick 字段 ——
        /// registry payload_schema 仅 { slot_index, event_key })。</param>
        /// <param name="slotIndex">挂起槽位(registry `slot_index`)。</param>
        /// <param name="eventKey">事件表 ordinal(registry `event_key`)。</param>
        /// <param name="deferMax">挂起槽上界(DEFER_MAX,烘焙配置;越界拒收)。</param>
        public DeferredThreatWriteResult TryDefer(long tick, int slotIndex, int eventKey, int deferMax)
        {
            if (tick < 0) throw new ArgumentOutOfRangeException(nameof(tick), tick, "tick 不得为负");
            if (deferMax <= 0) throw new ArgumentOutOfRangeException(nameof(deferMax), deferMax,
                "DEFER_MAX 必须 > 0(GDD :1096 界校验)");

            if (slotIndex < 0 || slotIndex >= deferMax)
                return DeferredThreatWriteResult.SlotOutOfRange;
            if (eventKey < 0)
                return DeferredThreatWriteResult.InvalidEventKey;

            var payload = new ThreatDeferredPayload(slotIndex, eventKey);
            var payloadRef = _encoder.Encode(EventKind.ThreatDeferred, payload);

            // 事件头:Patient = PatientId.None(世界级事件,ADR-007 §四 —— 本两 Kind 的**必须**值);
            // Seq = -1 未发号哨兵,由 EventStream 在 Append 时发号。
            _eventSink.Append(new SimEvent(
                tick, PatientId.None, -1, EventKind.ThreatDeferred, payloadRef));

            return DeferredThreatWriteResult.Applied;
        }

        /// <summary>
        /// 清挂起槽(补发或作废;规则六之三的两支收尾)。
        /// </summary>
        /// <param name="tick">清槽发生的那一 tick(只进事件头;payload 仅 { slot_index, reason })。</param>
        /// <param name="slotIndex">挂起槽位。</param>
        /// <param name="reason"><see cref="ReasonReissued"/>(补发)或 <see cref="ReasonVoided"/>(作废)。</param>
        /// <param name="deferMax">挂起槽上界(与 TryDefer 同域校验)。</param>
        public DeferredThreatWriteResult TryClear(long tick, int slotIndex, int reason, int deferMax)
        {
            if (tick < 0) throw new ArgumentOutOfRangeException(nameof(tick), tick, "tick 不得为负");
            if (deferMax <= 0) throw new ArgumentOutOfRangeException(nameof(deferMax), deferMax,
                "DEFER_MAX 必须 > 0(GDD :1096 界校验)");

            if (slotIndex < 0 || slotIndex >= deferMax)
                return DeferredThreatWriteResult.SlotOutOfRange;
            if (reason != ReasonReissued && reason != ReasonVoided)
                return DeferredThreatWriteResult.InvalidReason;

            var payload = new ThreatDeferralClearedPayload(slotIndex, reason);
            var payloadRef = _encoder.Encode(EventKind.ThreatDeferralCleared, payload);

            _eventSink.Append(new SimEvent(
                tick, PatientId.None, -1, EventKind.ThreatDeferralCleared, payloadRef));

            return DeferredThreatWriteResult.Applied;
        }
    }
}
