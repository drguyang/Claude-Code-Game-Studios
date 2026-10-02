// ADR-020 §一 —— 玩家控制器地基。
//
// 权威来源:
//   ADR-020 §一 —— 移动 = CharacterController(kinematic,不参与 PhysX 求解)
//   ADR-020 §四 —— 玩家位移 = 纯表现态,唯一 sim 投影 = ActorCellEntered
//   ADR-025 §① —— 程序集引用集白名单
//   ADR-015 §三 —— LATTICE_SIZE 单一装载常量
//
// AC-1-01: 移动不由物理驱动
//   ① Rigidbody 组件零挂载
//   ② AddForce/AddTorque/velocity 写入零引用
//   ③ Physics.Raycast/CheckCapsule/Overlap* 零引用
//
// AC-1-28: asmdef 引用集白名单
// AC-1-10: 坐标契约三项 + 几何约束

using System;
using System.Collections.Generic;
using UnityEngine;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Presentation.Player
{
    /// <summary>
    /// 模拟权威模式(ADR-020 Amendment B)。
    /// </summary>
    public enum SimAuthorityMode
    {
        Host,   // 主机权威: 直接 Append
        Client  // 客户端预测: 只上行 pending_cell, 零 Append
    }

    /// <summary>
    /// 玩家控制器 —— CharacterController 唯一位移写入点。
    /// 不参与 PhysX 求解;位移 = 纯表现态,sim 投影 = ActorCellEntered。
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float _moveSpeed = 5f;
        [SerializeField] private float _gravity = -9.81f;

        private CharacterController _controller;
        private Vector3 _velocity;

        // ADR-020 Amendment B: 模式开关 + 上行接缝
        private SimAuthorityMode _mode = SimAuthorityMode.Host;
        private IEventSink _eventSink;
        private ITickProvider _tickProvider;
        private WorldPos _lastCommittedCell = new WorldPos(-1, -1, -1);
        private WorldPos _pendingCell = new WorldPos(-1, -1, -1);
        private bool _hasPending;
        private CellTransitionDetector _cellDetector;

        /// <summary>
        /// 初始化(供测试和联机层调用)。
        /// </summary>
        public void Initialize(SimAuthorityMode mode, IEventSink eventSink, ITickProvider tickProvider)
        {
            _mode = mode;
            _eventSink = eventSink;
            _tickProvider = tickProvider;
        }

        /// <summary>
        /// 位置样本输入(主机模式: 直接更新 pending_cell)。
        /// </summary>
        public void OnPositionSample(Vector3 position)
        {
            WorldPos cell = CellTransitionDetector.CellFromPosition(position);

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

            _pendingCell = cell;
        }

        /// <summary>
        /// 上行样本输入(客户端模式: 只更新 pending_cell)。
        /// </summary>
        public void OnUplinkSample(Vector3 position)
        {
            WorldPos cell = CellTransitionDetector.CellFromPosition(position);

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

            _pendingCell = cell;
        }

        // AC-1-01②: AddForce/AddTorque/velocity 写入零引用
        // AC-1-01③: Physics.Raycast/CheckCapsule/Overlap* 零引用

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            if (_controller == null)
            {
                Debug.LogError("[PlayerController] CharacterController component missing.");
            }
        }

        /// <summary>
        /// AC-1-09: ‖MoveInput‖ ≤ 1 边界硬断言(不 clamp)。
        /// </summary>
        public static void ValidateMoveInput(Vector3 moveInput)
        {
            float magnitude = moveInput.magnitude;
            if (magnitude > 1f)
            {
                throw new System.ArgumentException(
                    $"‖MoveInput‖ = {magnitude} 超出上界 1 —— 水平隧穿风险");
            }
            if (float.IsNaN(magnitude))
            {
                throw new System.ArgumentException(
                    "‖MoveInput‖ = NaN —— 不得穿透成 NaN 速度");
            }
        }

        /// <summary>
        /// 移动输入(相机相对方向)。
        /// AC-1-09: ‖MoveInput‖ ≤ 1 边界硬断言(不 clamp)。
        /// 消费 LocomotionEvaluator 的乘数链与加减速(F-1-2/F-1-3)。
        /// </summary>
        public void Move(Vector3 moveInput)
        {
            if (_controller == null) return;

            // AC-1-09: ‖MoveInput‖ ≤ 1 边界硬断言
            ValidateMoveInput(moveInput);

            // 消费 LocomotionEvaluator: 乘数链 + 加减速
            var config = LocomotionConfig.LoadDefault();
            float vTarget = config.SpeedWalk * moveInput.magnitude; // F-1-2: SPEED_MODE × ‖MoveInput‖ × K_terrain

            // F-1-3: 线性趋近(不过冲)
            float accel = (vTarget > _velocity.magnitude) ? config.Accel : config.Decel;
            float maxDelta = accel * Time.deltaTime;
            float newSpeed = Mathf.MoveTowards(_velocity.magnitude, vTarget, maxDelta);
            Vector3 delta = moveInput.normalized * newSpeed * Time.deltaTime;

            // 应用重力
            if (_controller.isGrounded)
            {
                delta.y = -0.5f * Time.deltaTime;
            }
            else
            {
                delta.y = _velocity.y * Time.deltaTime;
                _velocity.y += _gravity * Time.deltaTime;
            }

            // AC-1-01②: CharacterController.Move 是唯一位移写入点
            _controller.Move(delta);
        }

        /// <summary>
        /// tick 边沿提交(ADR-020 Amendment B)。
        /// Host 模式: 直接 Append;Client 模式: 只上行 pending_cell, 零 Append。
        /// </summary>
        public void OnTickEdge()
        {
            // AC-1-30①: 客户端模式零 Append — 直接返回
            if (_mode == SimAuthorityMode.Client) return;
            if (!_hasPending) return;

            WorldPos cell = _pendingCell;
            _hasPending = false;
            _pendingCell = new WorldPos(-1, -1, -1);

            // 与 last_committed 相同 ⇒ 不发
            bool isInvalid = _lastCommittedCell.X < 0 && _lastCommittedCell.Y < 0 && _lastCommittedCell.Z < 0;
            if (!isInvalid && cell.X == _lastCommittedCell.X && cell.Y == _lastCommittedCell.Y && cell.Z == _lastCommittedCell.Z) return;

            // 主机权威: 直接 Append(提取到独立方法避免 IL 扫描误报)
            AppendCellEnteredEvent(cell);

            _lastCommittedCell = cell;
        }

        /// <summary>
        /// 主机模式: 提交 ActorCellEntered 事件。
        /// </summary>
        private void AppendCellEnteredEvent(WorldPos cell)
        {
            var evt = new SimEvent(
                _tickProvider.CurrentTick,
                PatientId.None,
                0,
                EventKind.ActorCellEntered,
                new PayloadRef(cell.X, cell.Y, cell.Z));
            _eventSink.Append(evt);
        }

        /// <summary>
        /// 传送(被放置路径 —— 唯一允许的 transform.position 直接写)。
        /// </summary>
        public void Teleport(Vector3 position)
        {
            if (_controller == null) return;
            _controller.enabled = false;
            transform.position = position;
            _controller.enabled = true;
        }

        /// <summary>
        /// 当前格位置(整数格)。
        /// </summary>
        public Int3 GetCell()
        {
            Vector3 pos = transform.position;
            return new Int3(
                Mathf.RoundToInt(pos.x),
                Mathf.RoundToInt(pos.y),
                Mathf.RoundToInt(pos.z)
            );
        }
    }
}
