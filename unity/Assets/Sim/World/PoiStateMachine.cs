// 权威来源:ADR-021 §三(PoiStateChanged 世界流 Kind)· ADR-009 §七(三段式)· Story 003(POI 状态机)
//
// 核心机制:
//   - PoiState 三态枚举(Undiscovered=0, Discovered=1, Resolved=2),单调不可逆
//   - 允许跳级(Undiscovered→Resolved),禁逆转移
//   - 状态写入经 IEventSink.Append → PoiStateChanged,主机唯一
//   - PatientId.None = -1 哨兵,不污染高水位
//   - 无第二存储(状态 = 流重建的当前视图)

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
        PoiNotFound        // poi_id 不存在
    }

    /// <summary>POI 状态机(主机唯一写者)。</summary>
    public sealed class PoiStateMachine
    {
        private readonly IEventSink _eventSink;
        private readonly IEventAuthority _eventAuthority;
        private readonly Dictionary<int, PoiState> _stateMap = new Dictionary<int, PoiState>();
        private readonly HashSet<int> _poiIdSet = new HashSet<int>();

        /// <summary>构造 POI 状态机。</summary>
        /// <param name="eventSink">事件写入通道(主机唯一)。</param>
        /// <param name="eventAuthority">掷骰/发号权威(主机唯一)。</param>
        /// <param name="initialPoiIds">初始 POI id 集合(从烘焙数据加载的定义侧)。</param>
        public PoiStateMachine(IEventSink eventSink, IEventAuthority eventAuthority, IEnumerable<int> initialPoiIds)
        {
            _eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
            _eventAuthority = eventAuthority ?? throw new ArgumentNullException(nameof(eventAuthority));

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
            if (!_eventAuthority.IsHost)
                return PoiStateTransferResult.PoiNotFound;

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

            // PatientId.None = -1 哨兵(ADR-021 裁定④)
            // 载荷编码: BlobId = poiId, Offset = (int)toState, Length = 8 (两个 int32)
            // TODO: 接入 Sim.Codec 真实 payload_schema(需 IBlobPool 支持)
            var payloadRef = new PayloadRef(blobId: poiId, offset: (int)toState, length: 8);

            var evt = new SimEvent(
                tick,
                PatientId.None,
                0, // seq 由发号器给出
                EventKind.PoiStateChanged,
                payloadRef);

            _eventSink.Append(evt);

            return PoiStateTransferResult.Success;
        }

        /// <summary>从世界流重建状态(读档/重放用)。</summary>
        public void RebuildFromEvents(IReadOnlyList<SimEvent> worldEvents)
        {
            _stateMap.Clear();

            foreach (var evt in worldEvents)
            {
                if (evt.Kind != EventKind.PoiStateChanged)
                    continue;

                // 从 PayloadRef 解码: BlobId = poiId, Offset = new_state
                // TODO: 接入 Sim.Codec 真实 payload_schema(需 IBlobPool 支持)
                int poiId = evt.Payload.BlobId;
                PoiState newState = (PoiState)evt.Payload.Offset;

                if (!_poiIdSet.Contains(poiId))
                    continue;

                _stateMap[poiId] = newState;
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
