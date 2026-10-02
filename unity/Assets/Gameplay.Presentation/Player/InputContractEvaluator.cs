// ADR-020 §一 —— 输入契约与相机相对方向（F-1-8 基投影）。
//
// 权威来源:
//   ADR-020 §一 —— MoveInput 输入面 + Amendment B ICameraRig.YawBasis
//   ADR-011 —— action-based 动作映射
//   GDD player-controller-and-movement.md —— F-1-8 / AC-1-09 / AC-1-31 / AC-1-35
//
// 核心机制:
//   - v̂_world := normalize(my × f̂ + mx × r̂)
//   - r̂ 从 f̂ 派生（不单独取）
//   - ‖MoveInput‖ ≤ 1 边界硬断言（不 clamp）
//   - ‖MoveInput‖ = 0 时不做投影（走 Idle）

using System;
using UnityEngine;
using DaYiJingCheng.Gameplay.Presentation.Camera;

namespace DaYiJingCheng.Gameplay.Presentation.Player
{
    /// <summary>
    /// 输入契约求值器 —— F-1-8 基投影 + 边界断言。
    /// </summary>
    public static class InputContractEvaluator
    {
        /// <summary>
        /// 投影 MoveInput 到世界方向（F-1-8）—— 支持 ICameraRig 注入（供 AC-1-35 取样纪律测试）。
        /// </summary>
        public static Vector3 ProjectToWorld(Vector3 moveInput, ICameraRig rig)
        {
            // AC-1-35②: YawBasis 每帧只取样一次（缓存在局部变量）
            var basis = rig.YawBasis;
            return ProjectToWorld(moveInput, basis);
        }

        /// <summary>
        /// 投影 MoveInput 到世界方向（F-1-8）。
        /// </summary>
        /// <param name="moveInput">二维移动输入（‖MoveInput‖ ≤ 1）。</param>
        /// <param name="basis">相机 yaw 基（只读）。</param>
        /// <returns>世界方向单位向量（水平面内）。</returns>
        public static Vector3 ProjectToWorld(Vector3 moveInput, YawBasis basis)
        {
            // AC-1-09: ‖MoveInput‖ ≤ 1 边界硬断言
            float magnitude = moveInput.magnitude;
            if (magnitude > 1f)
            {
                throw new ArgumentException(
                    $"‖MoveInput‖ = {magnitude} 超出上界 1 —— 水平隧穿风险");
            }

            // AC-1-31: ‖MoveInput‖ = 0 时不做投影（走 Idle）
            if (magnitude < 0.0001f)
            {
                return Vector3.zero;
            }

            // F-1-8: v̂_world := normalize(my × f̂ + mx × r̂)
            // r̂ 从 f̂ 派生（不单独取）—— 防止相机轻微 roll 时产生非正交基
            Vector3 f = basis.Fwd;
            Vector3 r = Vector3.Cross(Vector3.up, f).normalized;

            // 断言 r̂ 与 basis.Right 一致（容差内）
            if (Vector3.Distance(r, basis.Right) > 0.001f)
            {
                throw new System.ArgumentException(
                    $"r̂ 派生值与 basis.Right 不一致 —— 相机可能存在非预期 roll");
            }

            Vector3 worldDir = moveInput.y * f + moveInput.x * r;

            // 退化分支: ‖proj_h(f̂)‖ ≈ 0 时退化到世界 +Z
            if (worldDir.sqrMagnitude < 0.0001f)
            {
                return Vector3.forward;
            }

            // AC-1-31: v̂_world 单位性 + 水平面内
            return worldDir.normalized;
        }
    }
}
