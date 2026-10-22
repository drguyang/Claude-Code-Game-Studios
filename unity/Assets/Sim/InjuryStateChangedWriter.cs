// S18-1 AC ⑧ 另半边 · design/registry/entities.yaml:2270 —— 9 的 `InjuryStateChanged` 写入通道。
//
// 权威来源:
//   design/registry/entities.yaml:2270 —— SimEvent.Kind.InjuryStateChanged:
//     stream: world · author: "9 疾病与伤情模拟(唯一:状态真值在 9;25 只出 onset,
//                                不声称自己不知道的输出)"
//     payload_schema: "actor_id: i32; new_state: 枚举 … emit 时机 = position_agg 越阈
//                      被 Step 求出的那一 tick,离线 CatchUp 须产生逐条相同序列(R18);
//                      上界 ≤ 转移次数 × 实体数"
//   ADR-016 §一(效果进流,决策不进流)· ADR-021 §三(StateChanged 命名先例)
//   ADR-005(主机唯一 Append;Seq = -1 未发号哨兵)
//   ADR-029 §③(载荷唯一编码路径 = IPayloadEncoder)
//
// 核心机制(三层分离,与 `PlayerDiedWriter` / `CaseCloseWriter` 同形):
//   - **判据** = `StateMachine.ComputeTransition`(纯函数:状态转移)—— 本类不复制判序。
//   - **写者** = 本类:调纯函数得 `newState`,**仅当状态确实变化时**写一条。
//   - 状态**未变化 ⇒ 零写入** —— registry 的「上界 ≤ 转移次数 × 实体数」是**转移次数**,
//     不是 tick 数;每 tick 都写会把这个上界撑成「tick 数 × 实体数」,违 ADR-009 §六 有界性。
//
// ⚠️ **本写者只写 `InjuryStateChanged`(受伤实体集 {敌人, 玩家})**:
//   病人侧的状态转移由 9 的病史流既有 Kind 承载(registry constraint 原文),
//   不归本件。本类不判 actorClass 的「是不是玩家」—— 那是 `IsCombatant` 的职责,
//   调用方按实体集决定喂谁(病人不进本写者)。
//
// ⚠️ 登记项(本批不裁):
//   - **生产触发点**(9 的 `Step` 每 tick 调本写者)尚未接线:归 9 的 Step 装配轮。
//     本类落地 = 「有写者」(`WriterExistenceGate` 判据① 可撤豁免表),不等于「已接线」。
//   - `ComputeTransition` 的签名 5 参中 4 个当前未用(`StateMachine.cs:75-79`)——
//     那是既存形态,不属本件;本件只负责让它**有生产调用点**。

using System;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary><see cref="InjuryStateChangedWriter.TryWrite"/> 的结果码(零写入分支亦具名)。</summary>
    public enum InjuryStateWriteResult
    {
        /// <summary>一条 <see cref="EventKind.InjuryStateChanged"/> 入世界流(状态确实变化)。</summary>
        Written = 0,

        /// <summary>状态未变化 ⇒ 零写入。**这不是失败**,是常态:绝大多数 tick 不越阈。</summary>
        NoChange = 1,

        /// <summary>拒绝 —— 入参非法(actorId 为负 / tick 为负);零写入。</summary>
        RejectInvalidArgument = 2,
    }

    /// <summary>
    /// `InjuryStateChanged` 写者 —— 9 的世界流生产写入通道(受伤实体集 {敌人, 玩家})。
    /// </summary>
    /// <remarks>
    /// <para><b>为什么不收「调用方算好的 newState」</b>:与 <see cref="CaseCloseWriter"/>
    /// 「前置条件在写者内复核,不信任调用方转述」同一条纪律。本类自己调
    /// <see cref="StateMachine.ComputeTransition"/>,判序真源仍只有一处。</para>
    /// <para><b>为什么「未变化不写」是硬要求</b>:registry 的有界性上界是
    /// 「转移次数 × 实体数」。若每 tick 都写,上界变成「tick 数 × 实体数」——
    /// 20 Hz × N 实体,与 ADR-009 §六 的有界性论证直接冲突。</para>
    /// </remarks>
    public sealed class InjuryStateChangedWriter
    {
        private readonly IEventSink _eventSink;
        private readonly IPayloadEncoder _encoder;

        /// <summary>
        /// 构造 `InjuryStateChanged` 写者。
        /// </summary>
        /// <param name="eventSink">事件写入通道(主机唯一;生产 = 真 <c>EventStream</c>)。</param>
        /// <param name="encoder">载荷编码器(ADR-029 §③ —— 唯一合法编码路径,
        /// 本类内不得出现 <c>new PayloadRef(</c>,由 b6 门强制)。</param>
        /// <exception cref="ArgumentNullException">任一依赖为 null。</exception>
        public InjuryStateChangedWriter(IEventSink eventSink, IPayloadEncoder encoder)
        {
            _eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
            _encoder = encoder ?? throw new ArgumentNullException(nameof(encoder));
        }

        /// <summary>
        /// 按 <paramref name="positionAgg"/> 求值状态,状态变化时写一条事件。
        /// </summary>
        /// <param name="tick">当前逻辑 tick(20 Hz 量纲)。</param>
        /// <param name="actorId">受伤实体 id(敌人或玩家;病人不进本写者)。</param>
        /// <param name="currentState">该实体的**当前**状态(上一 tick 的求值结果)。</param>
        /// <param name="positionAgg">病情累计量(定点)—— 判据的唯一数值入参。</param>
        /// <param name="lastInterventionTick">最后一次干预 tick(经
        /// <see cref="StateMachine.ComputeTransition"/> 透传;当前未用,见头注登记项)。</param>
        /// <param name="diseaseKey">病种 / 伤情键(同上,透传)。</param>
        /// <returns>具名结果码。</returns>
        public InjuryStateWriteResult TryWrite(
            long tick, int actorId, InjuryState currentState,
            Fix positionAgg, long lastInterventionTick, string diseaseKey)
        {
            if (tick < 0) return InjuryStateWriteResult.RejectInvalidArgument;
            if (actorId < 0) return InjuryStateWriteResult.RejectInvalidArgument;

            // ── 判据(纯函数,真源 = StateMachine;本类不复制判序)──
            InjuryState newState = StateMachine.ComputeTransition(
                currentState, positionAgg, lastInterventionTick, tick, diseaseKey);

            // ── 未变化 ⇒ 零写入(有界性:上界是「转移次数」,不是「tick 数」)──
            if (newState == currentState)
                return InjuryStateWriteResult.NoChange;

            // ── 载荷:registry payload_schema 两字段(均整数枚举)──
            var payload = new InjuryStateChangedPayload(actorId, (int)newState);
            var payloadRef = _encoder.Encode(EventKind.InjuryStateChanged, payload);

            // ── 事件头:Patient = None(受伤实体侧事件,ADR-007 §四 —— 不污染高水位);
            //            Seq = -1 未发号哨兵(O-1)──
            _eventSink.Append(new SimEvent(
                tick, PatientId.None, -1, EventKind.InjuryStateChanged, payloadRef));

            return InjuryStateWriteResult.Written;
        }
    }
}
