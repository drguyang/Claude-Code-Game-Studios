// 权威来源:ADR-021 §三(PoiStateChanged 世界流 Kind)· ADR-009 §七(三段式)· Story 003(POI 状态机)
//             ADR-029(载荷编码唯一路径 = IPayloadEncoder)
//
// 核心机制:
//   - PoiState 三态枚举(Undiscovered=0, Discovered=1, Resolved=2),单调不可逆
//   - 允许跳级(Undiscovered→Resolved),禁逆转移
//   - 状态写入经 IEventSink.Append → PoiStateChanged,主机唯一
//   - PatientId.None = -1 哨兵,不污染高水位
//   - 无第二存储(状态 = 流重建的当前视图)
//   - **载荷编码经 IPayloadEncoder**(ADR-029 §③:零手搓 PayloadRef)
//
// ⚠️ 读侧(重建)的接缝 —— ADR-029 §Implementation Guidelines 的**甲案**:
//   解码器 `PayloadCodec` 住 `Sim.Codec`,而本类住 `Sim`(够不着,且该引用边由
//   ADR-025 §①:111 禁止 + b2 门强制)。故 `RebuildFromEvents` **不自行解码** ——
//   由**调用方**(看得见 codec 的一侧:7a 读档 / 45 网络 / 边界层)把已解码的
//   `(poiId, newState)` 序列喂进来。⇒ 与写侧**对称**:写侧交出已编码的 PayloadRef,
//   读侧收下已解码的字段。**Sim 侧零 codec 依赖。**

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.World
{
    /// <summary>POI 状态三态枚举(单调不可逆,允许跳级)。</summary>
    public enum PoiState : int
    {
        Undiscovered = 0,
        Discovered = 1,
        Resolved = 2
    }

    /// <summary>POI 状态转移结果。</summary>
    public enum PoiStateTransferResult
    {
        Success,           // 转移成功
        AlreadyAtState,    // 已在目标状态(幂等,不写事件)
        InvalidTransfer,   // 逆转移或不可达转移
        PoiNotFound,       // poi_id 不存在(定义侧未登记)

        /// <summary>
        /// **非主机** —— 客户端调用写通道被 host gate 拒。
        /// ⚠️ **2026-10-03 新增(评审 C3)**:此前该情形**与 `PoiNotFound` 同码**,
        /// 调用方**无法区分**「我不是主机」与「该 POI 不存在」⇒ 可能触发错误的降级路径
        /// (如误以为 POI 数据缺失而重载定义)。AC-6-26a 要求「客户端调用写通道 ⇒
        /// 断言失败/拒写」,**拒写已成立,但可诊断性此前不成立**。
        /// </summary>
        NotHost
    }

    /// <summary>POI 状态机(主机唯一写者)。</summary>
    public sealed class PoiStateMachine
    {
        private readonly IEventSink _eventSink;
        private readonly IEventAuthority _eventAuthority;
        private readonly IPayloadEncoder _encoder;
        private readonly Dictionary<int, PoiState> _stateMap = new Dictionary<int, PoiState>();
        private readonly HashSet<int> _poiIdSet = new HashSet<int>();

        /// <summary>构造 POI 状态机。</summary>
        /// <param name="eventSink">事件写入通道(主机唯一)。</param>
        /// <param name="eventAuthority">掷骰/发号权威(主机唯一)。</param>
        /// <param name="initialPoiIds">初始 POI id 集合(从烘焙数据加载的定义侧)。</param>
        /// <param name="encoder">
        /// 载荷编码器(ADR-029 §③)—— **唯一合法的载荷构造路径**。
        /// 本类内不得出现 <c>new PayloadRef(</c>(ADR-029 §③ 的门判据)。
        /// </param>
        public PoiStateMachine(IEventSink eventSink, IEventAuthority eventAuthority,
                               IEnumerable<int> initialPoiIds, IPayloadEncoder encoder)
        {
            _eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
            _eventAuthority = eventAuthority ?? throw new ArgumentNullException(nameof(eventAuthority));
            _encoder = encoder ?? throw new ArgumentNullException(nameof(encoder));

            if (initialPoiIds != null)
            {
                foreach (var id in initialPoiIds)
                {
                    _poiIdSet.Add(id);
                    _stateMap[id] = PoiState.Undiscovered;
                }
            }
        }

        /// <summary>查询 POI 当前状态(不存在返回 Undiscovered)。</summary>
        public PoiState GetState(int poiId)
        {
            return _stateMap.TryGetValue(poiId, out var state) ? state : PoiState.Undiscovered;
        }

        /// <summary>
        /// 发现门(AC-6-23): 玩家进入 POI 所在格时调用。
        /// 仅 Undiscovered → Discovered 合法;已发现/已解决返回 AlreadyAtState。
        /// </summary>
        public PoiStateTransferResult TryDiscover(int poiId, long tick = 0)
        {
            return TryAdvance(poiId, PoiState.Discovered, tick);
        }

        /// <summary>尝试推进 POI 状态(主机唯一入口)。</summary>
        /// <param name="poiId">POI id。</param>
        /// <param name="toState">目标状态。</param>
        /// <param name="tick">当前 tick(由调用方注入)。</param>
        /// <returns>转移结果。</returns>
        public PoiStateTransferResult TryAdvance(int poiId, PoiState toState, long tick = 0)
        {
            // AC-6-26a: host-only write gate
            // ⚠️ 2026-10-03(评审 C3):原返回 `PoiNotFound` —— 与「poi_id 不存在」**同码**,
            //    调用方不可区分。现返回专用码 `NotHost`(拒写行为不变,可诊断性修复)。
            if (!_eventAuthority.IsHost)
                return PoiStateTransferResult.NotHost;

            if (!_poiIdSet.Contains(poiId))
                return PoiStateTransferResult.PoiNotFound;

            if (!_stateMap.TryGetValue(poiId, out var current))
                current = PoiState.Undiscovered;

            // 已在目标状态(幂等)
            if (current == toState)
                return PoiStateTransferResult.AlreadyAtState;

            // 逆转移检查:必须单调不减
            if (toState <= current)
                return PoiStateTransferResult.InvalidTransfer;

            // 合法转移:更新状态 + 写世界流事件
            _stateMap[poiId] = toState;

            // ADR-029 §③:载荷经 IPayloadEncoder —— 两字段全载,零手搓。
            // ⚠️ 2026-10-02 修复:原实现 `new PayloadRef(blobId: poiId, offset: (int)toState, length: 8)`
            //    把 **poiId 当 blobId**、**newState 当字节偏移**用 —— 与 PayloadRef 契约
            //    (`Offset` = 字节偏移,非业务字段)冲突,且 BlobId 与 poi_id 语义混同。
            var payloadRef = _encoder.Encode(EventKind.PoiStateChanged,
                new PoiStateChangedPayload(poiId, (int)toState));

            var evt = new SimEvent(
                tick,
                PatientId.None,
                -1, // 未发号哨兵 O-1(由发号器给出)
                EventKind.PoiStateChanged,
                payloadRef);

            _eventSink.Append(evt);

            return PoiStateTransferResult.Success;
        }

        /// <summary>
        /// 从**已解码**的世界流重建状态(读档/重放用)。
        /// </summary>
        /// <param name="decodedStates">
        /// 已解码的 (poi_id, new_state) 序列,**按事件全序**排列。
        /// 解码由调用方完成 —— 见类头注的「甲案」:本类住 `Sim`,够不着 `PayloadCodec`,
        /// 故不自行解码(ADR-029 §Implementation Guidelines)。
        /// </param>
        /// <remarks>
        /// ⚠️ 本方法**不校验单调性** —— 它按序列顺序覆盖状态,与「流是唯一真源」一致:
        /// 流的写入侧(`TryAdvance`)已保证单调不减,重建只需忠实重放。
        /// </remarks>
        public void RebuildFromDecoded(IReadOnlyList<(int PoiId, PoiState State)> decodedStates)
        {
            if (decodedStates == null) throw new ArgumentNullException(nameof(decodedStates));

            _stateMap.Clear();
            foreach (var id in _poiIdSet)
                _stateMap[id] = PoiState.Undiscovered;

            foreach (var (poiId, state) in decodedStates)
            {
                if (!_poiIdSet.Contains(poiId))
                    continue;   // 定义侧未登记的 poi_id ⇒ 跳过(与旧行为一致)
                _stateMap[poiId] = state;
            }
        }

        /// <summary>获取所有已知 POI 的当前状态快照(防御性拷贝,防 aliasing)。</summary>
        public IReadOnlyDictionary<int, PoiState> Snapshot()
        {
            return new Dictionary<int, PoiState>(_stateMap);
        }

        /// <summary>已知 POI 数量。</summary>
        public int Count => _poiIdSet.Count;
    }
}
