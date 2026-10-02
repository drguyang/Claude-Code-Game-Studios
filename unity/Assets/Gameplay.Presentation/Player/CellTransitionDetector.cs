// ADR-020 §四 —— 跨格检测与归并算子。
//
// 权威来源:
//   ADR-020 §四 —— 玩家位移 = 纯表现态,唯一投影 = ActorCellEntered
//   ADR-009 —— 世界流有界性论证(事件率上界 = tick 频率)
//   ADR-005 —— ITickProvider / IEventSink 抽象点
//   GDD player-controller-and-movement.md —— F-1-1b / EC-1…EC-8
//
// 核心机制:
//   - 归并算子: 同 tick 内 N 个位置样本 ⇒ 至多 1 条事件
//   - 折返不发: 同 tick 内 A→B→A ⇒ 零事件
//   - 回访再发: 跨 tick 的 A→B→A ⇒ 两条事件
//   - 传送只发落点: 零中间格补发
//   - dt 钳位不累积: 超出 MAX_DT 的部分丢弃
//   - 载荷 tick 来源唯一: ITickProvider.CurrentTick

using System;
using UnityEngine;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;

namespace DaYiJingCheng.Gameplay.Presentation.Player
{
    /// <summary>
    /// 跨格检测器 —— 归并算子 + tick 边沿提交。
    /// </summary>
    public sealed class CellTransitionDetector
    {
        private readonly IEventSink _eventSink;
        private readonly ITickProvider _tickProvider;

        private WorldPos _lastCommittedCell = new WorldPos(-1, -1, -1); // (-1,-1,-1) = 无效, 首个样本必触发转移
        private WorldPos _pendingCell = new WorldPos(-1, -1, -1);
        private bool _hasPending;

        public CellTransitionDetector(IEventSink eventSink, ITickProvider tickProvider)
        {
            _eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
            _tickProvider = tickProvider ?? throw new ArgumentNullException(nameof(tickProvider));
        }

        /// <summary>
        /// 位置样本输入(每帧可多次调用)。
        /// </summary>
        public void OnPositionSample(Vector3 position)
        {
            WorldPos cell = CellFromPosition(position);

            if (!_hasPending)
            {
                _pendingCell = cell;
                _hasPending = true;
                return;
            }

            // 折返检测: 新样本 == last_committed ⇒ 清除 pending
            if (cell.X == _lastCommittedCell.X && cell.Y == _lastCommittedCell.Y && cell.Z == _lastCommittedCell.Z)
            {
                _hasPending = false;
                _pendingCell = new WorldPos(-1, -1, -1);
                return;
            }

            // 更新 pending(取最新样本)
            _pendingCell = cell;
        }

        /// <summary>
        /// tick 边沿提交(每 tick 调用一次)。
        /// </summary>
        public void OnTickEdge()
        {
            if (!_hasPending) return;

            WorldPos cell = _pendingCell;
            _hasPending = false;
            _pendingCell = new WorldPos(-1, -1, -1);

            // 首个样本(_lastCommittedCell == invalid)必触发转移
            // 与 last_committed 相同 ⇒ 不发
            bool isInvalid = _lastCommittedCell.X < 0 && _lastCommittedCell.Y < 0 && _lastCommittedCell.Z < 0;
            if (!isInvalid && cell.X == _lastCommittedCell.X && cell.Y == _lastCommittedCell.Y && cell.Z == _lastCommittedCell.Z) return;

            // 提交事件 — 载荷编码由 IEventSink 实现负责(ADR-024)
            // PayloadRef.BlobId 存 cell.X, Offset 存 cell.Y, Length 存 cell.Z
            var evt = new SimEvent(
                _tickProvider.CurrentTick,
                PatientId.None,
                0,
                EventKind.ActorCellEntered,
                new PayloadRef(cell.X, cell.Y, cell.Z));
            _eventSink.Append(evt);

            _lastCommittedCell = cell;
        }

        /// <summary>
        /// 位置 → 整数格(AC-1-07: 使用 Mathf.FloorToInt,三维)。
        /// </summary>
        public static WorldPos CellFromPosition(Vector3 position)
        {
            return new WorldPos(
                Mathf.FloorToInt(position.x),
                Mathf.FloorToInt(position.y),
                Mathf.FloorToInt(position.z));
        }

        /// <summary>
        /// 计算位移(AC-1-16: dt 钳位不累积)。
        /// </summary>
        public static float ComputeDisplacement(float speed, float dt, float maxDt)
        {
            float clampedDt = Mathf.Min(dt, maxDt);
            return speed * clampedDt;
        }
    }
}
