// 出诊启动写者 —— `HouseCallStarted` 的生产写入通道(52 随机事件导演)。
//
// 权威来源:
//   GDD random-events.md 规则十二(2026-10-25 52 GDD 轮)——
//     12.1 触发 = 活跃出诊目标 ∧ 玩家本 tick **从医馆格跨出到非医馆格**(52 只读流);
//     12.3 载荷 = 仅 { target_patient_id };DC-4 表:`Patient = None`。
//   design/registry/entities.yaml HouseCallStarted constraint ——
//     落**世界流** · Patient = None · 写者 = 52,主机唯一 Append。
//   ADR-005 —— 主机唯一 Append 由「谁调用本类」保证(与既有写者同格)。
//   ADR-029 §③ —— 载荷唯一编码路径 = IPayloadEncoder(本类内禁 `new PayloadRef(`)。
//   ADR-015 §三 —— 空间判据域 = 整数格;医馆格判据 = IClinicCellSource 注入
//     (与规则十「无人」、F1PipelineInput.IsInClinic 同源 —— 三处绑同一实现,
//     装配归 Boot 接线轮;本类只消费谓词,不装配)。
//   TD-randomevents-02 —— 本类的兑现轮(2026-10-25);WriterExistenceGate
//     豁免同批撤除(判据①「已存在写者的 Kind 仍挂豁免表 = 红」)。
//
// 核心机制:
//   - 前置条件在**写者内**复核(不信任调用方转述),任一不满足 ⇒ **零写入**并返回
//     具名结果码(DeferredThreatSlotWriter 同格,不静默):
//       ① 活跃出诊目标(ActiveHouseCallQuery.Evaluate,链接断裂由查询 fail-loud 传播);
//       ② 玩家本 tick 有一条 ActorCellEntered(无 ⇒ NoCrossingThisTick);
//       ③ 前一格 = 此前最后一条 entered 的格(**无此前格 ⇒ NotCrossingOut** ——
//         首格即在医馆外时无从构成「跨出」,fail-safe 拒收);
//         prev ∈ 医馆 ∧ cur ∉ 医馆(不满足 ⇒ NotCrossingOut);
//       ④ 本 tick 已有 HouseCallStarted(写者可能被导演循环同 tick 多次调用,
//         每 tick 恰一条 ⇒ AlreadyStartedThisTick)。
//   - **「每次跨出 = 一次锚点时刻」(12.1 逐字)**:不做 per-summons 去重 ——
//     同一活跃目标期间玩家再次跨出医馆 ⇒ 再次写入(重锚点)。本解读为首定口径,
//     若 37 后续裁定「重锚点须拒」,改本类一处 + 夹具一条,不改调用方。
//   - 「有写者 ≠ 已接线」:生产触发点(52 导演主循环逐 tick 调 TryStartAtTick)
//     归 52 的接线轮。本类只保证「谁调用它,流里就有一条合规的 HouseCallStarted」。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary><see cref="HouseCallStartedWriter.TryStartAtTick"/> 的结果码(零写入分支亦具名,不静默)。</summary>
    public enum HouseCallStartResult
    {
        /// <summary>已写入一条 HouseCallStarted。</summary>
        Applied,
        /// <summary>无活跃出诊目标(12.2 三条件不满足;窗口/拒访 P0 零检查)。</summary>
        NoActiveTarget,
        /// <summary>玩家本 tick 无跨格事件 —— 无从谈「跨出」。</summary>
        NoCrossingThisTick,
        /// <summary>有跨格事件但不构成「从医馆格跨出」(首格无前序 / 前格非医馆 / 新格仍在医馆)。</summary>
        NotCrossingOut,
        /// <summary>本 tick 已写入过一条 HouseCallStarted(每 tick 恰一条)。</summary>
        AlreadyStartedThisTick
    }

    /// <summary>
    /// 出诊启动写者(52;玩家离开医馆地块时把活跃出诊目标物化为流事件)。
    /// </summary>
    public sealed class HouseCallStartedWriter
    {
        private readonly IEventSink _eventSink;
        private readonly IPayloadEncoder _encoder;
        private readonly IPayloadDecoder _decoder;
        private readonly IClinicCellSource _clinicCells;
        private readonly IReadOnlyCollection<int> _houseCallEventKeys;
        private readonly int _playerActorId;

        /// <summary>构造(依赖注入;主机唯一 Append 由「谁调用本类」保证 —— ADR-005)。</summary>
        /// <param name="eventSink">事件写入通道(唯一 <c>Append</c> 出口)。</param>
        /// <param name="encoder">载荷编码器(ADR-029 第七抽象点)。</param>
        /// <param name="decoder">载荷解码器(读流复核前置 —— ADR-031 第八抽象点)。</param>
        /// <param name="clinicCells">医馆格判据源(12.1 的注入面;同源义务见接口头注)。</param>
        /// <param name="houseCallEventKeys">出诊类 event_key 闭集(转发给 <see cref="ActiveHouseCallQuery"/>)。</param>
        /// <param name="playerActorId">玩家实体 id(跨格事件按此筛)。</param>
        public HouseCallStartedWriter(
            IEventSink eventSink,
            IPayloadEncoder encoder,
            IPayloadDecoder decoder,
            IClinicCellSource clinicCells,
            IReadOnlyCollection<int> houseCallEventKeys,
            int playerActorId)
        {
            _eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
            _encoder = encoder ?? throw new ArgumentNullException(nameof(encoder));
            _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
            _clinicCells = clinicCells ?? throw new ArgumentNullException(nameof(clinicCells));
            _houseCallEventKeys = houseCallEventKeys ?? throw new ArgumentNullException(nameof(houseCallEventKeys));
            if (playerActorId < 0)
                throw new ArgumentOutOfRangeException(nameof(playerActorId), playerActorId,
                    "玩家实体 id 不得为负 —— PatientId.None(-1)是世界级事件哨兵,不是玩家");
            _playerActorId = playerActorId;
        }

        /// <summary>
        /// 尝试在指定 tick 启动出诊(规则十二 12.1;52 导演主循环逐 tick 调用)。
        /// </summary>
        /// <param name="events">事件流只读视图(前置复核全部在流上做,不信任调用方转述)。</param>
        /// <param name="tick">本 tick(写入事件头;payload 无 tick 字段 ——
        /// registry payload_schema 仅 { target_patient_id })。</param>
        /// <returns>具名结果码;仅 <see cref="HouseCallStartResult.Applied"/> 有写入。</returns>
        public HouseCallStartResult TryStartAtTick(IReadOnlyList<SimEvent> events, long tick)
        {
            if (events == null) throw new ArgumentNullException(nameof(events));
            if (tick < 0) throw new ArgumentOutOfRangeException(nameof(tick), tick, "tick 不得为负");

            // ① 活跃出诊目标(12.2;链接断裂由查询 fail-loud 传播,不在此吞)。
            ActiveHouseCall target = ActiveHouseCallQuery.Evaluate(
                events, _decoder, _houseCallEventKeys);
            if (!target.Active)
                return HouseCallStartResult.NoActiveTarget;

            // ② 玩家本 tick 的跨格事件(恰好覆盖语义:每 tick 每玩家至多一条)。
            WorldPos currentCell = default;
            bool enteredThisTick = false;
            for (int i = 0; i < events.Count; i++)
            {
                SimEvent e = events[i];
                if (e.Kind != EventKind.ActorCellEntered || e.Tick != tick)
                    continue;

                ActorCellEnteredPayload payload =
                    _decoder.Decode<ActorCellEnteredPayload>(EventKind.ActorCellEntered, e.Payload);

                if (payload.ActorId != _playerActorId)
                    continue;

                currentCell = payload.Cell;
                enteredThisTick = true;
            }

            if (!enteredThisTick)
                return HouseCallStartResult.NoCrossingThisTick;

            // ③ 前一格 = 此前(流序)最后一条 entered 的格;无前序格 ⇒ 无从构成「跨出」。
            WorldPos previousCell = default;
            bool hasPrevious = false;
            for (int i = 0; i < events.Count; i++)
            {
                SimEvent e = events[i];
                if (e.Kind != EventKind.ActorCellEntered || e.Tick >= tick)
                    continue;

                ActorCellEnteredPayload payload =
                    _decoder.Decode<ActorCellEnteredPayload>(EventKind.ActorCellEntered, e.Payload);

                if (payload.ActorId != _playerActorId)
                    continue;

                previousCell = payload.Cell;   // 流序最后一条胜(流按 Tick 非降)
                hasPrevious = true;
            }

            // 12.1 逐字:从**医馆格**跨出到**非医馆格**。
            if (!hasPrevious
                || !_clinicCells.IsClinicCell(previousCell)
                || _clinicCells.IsClinicCell(currentCell))
                return HouseCallStartResult.NotCrossingOut;

            // ④ 本 tick 已写入过 ⇒ 拒(每 tick 恰一条;写者可能被导演循环同 tick 重入)。
            for (int i = 0; i < events.Count; i++)
            {
                SimEvent e = events[i];
                if (e.Kind == EventKind.HouseCallStarted && e.Tick == tick)
                    return HouseCallStartResult.AlreadyStartedThisTick;
            }

            // ⑤ 写入:世界流 · Patient = None(DC-4)· Seq = -1 未发号哨兵(EventStream 发真号)。
            var payloadRef = _encoder.Encode(
                EventKind.HouseCallStarted, new HouseCallStartedPayload(target.TargetPatientId));
            _eventSink.Append(new SimEvent(
                tick, PatientId.None, -1, EventKind.HouseCallStarted, payloadRef));

            return HouseCallStartResult.Applied;
        }
    }
}
