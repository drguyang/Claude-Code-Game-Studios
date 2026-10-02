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

using UnityEngine;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Presentation.Player
{
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
        /// </summary>
        public void Move(Vector3 moveInput)
        {
            if (_controller == null) return;

            // AC-1-09: ‖MoveInput‖ ≤ 1 边界硬断言
            ValidateMoveInput(moveInput);

            // 应用重力
            if (_controller.isGrounded)
            {
                _velocity.y = -0.5f;
            }
            else
            {
                _velocity.y += _gravity * Time.deltaTime;
            }

            // 合成位移
            Vector3 delta = moveInput * _moveSpeed * Time.deltaTime;
            delta.y = _velocity.y * Time.deltaTime;

            // AC-1-01②: CharacterController.Move 是唯一位移写入点
            _controller.Move(delta);
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
