// ADR-020 §一 —— 移动手感链求值器。
//
// 权威来源:
//   ADR-020 §一 —— 全部数值留白(AC-20-11),本文件只给形状与判据
//   GDD player-controller-and-movement.md —— F-1-1a/b/c · F-1-2…F-1-7
//
// 核心机制:
//   - 乘数链: v_target := SPEED_MODE × ‖MoveInput‖ × K_terrain × K_context × K_suppressed
//   - 加减速: 线性趋近(不过冲)
//   - 转向: 自动面向移动方向
//   - 求值次序: 读格 → v_target → 加速 → 转向 → Move

using System.Collections.Generic;
using UnityEngine;
using DaYiJingCheng.Gameplay.Presentation.Camera;

namespace DaYiJingCheng.Gameplay.Presentation.Player
{
    /// <summary>
    /// 移动配置(数值留白,归用户)。
    /// <para>⚠️ 本类是 <c>CharacterController</c> 参数的**唯一声明出处**(GDD F-1-9 / `AC-1-33`)——
    /// 但**声明不等于落地**:这六项须由 <see cref="PlayerController"/> 在取件时**写进组件**,
    /// 否则控制器取 Unity 默认值(2026-10-20 `TD-player-01`:声明了却从不写入 ⇒
    /// `minMoveDistance` 停在引擎默认 `0.001` ⇒ 吞掉亚阈值位移)。</para>
    /// </summary>
    public class LocomotionConfig
    {
        public static LocomotionConfig LoadDefault() => new LocomotionConfig
        {
            SpeedWalk = 5f,
            Accel = 10f,
            Decel = 10f,
            TurnRate = 180f,
            AirControl = 1f,
            JumpHeightMin = 1f,
            JumpHeightMax = 2f,
            GravityFallMult = 2f,
            MinMoveDistance = 0f,
            SkinWidth = 0.01f,
            Radius = 0.5f,
            Height = 1.8f,
            Center = Vector3.zero,
            EnableOverlapRecovery = true,
        };

        public float SpeedWalk;
        public float Accel;
        public float Decel;
        public float TurnRate;
        public float AirControl;
        public float JumpHeightMin;
        public float JumpHeightMax;
        public float GravityFallMult;

        // ── CharacterController 六项(F-1-9 / AC-1-33;`PlayerController` 负责写进组件)──
        /// <summary>位移抖动抑制下限 —— **P0 = 0**(GDD F-1-9:`≠ 0` 会吞掉起步微小位移)。
        /// <para>⚠️ 这是 `TD-player-01` 的直接病灶:声明为 0 但从未写进组件 ⇒ 引擎默认 0.001 生效。</para></summary>
        public float MinMoveDistance;
        /// <summary>碰撞皮肤厚度 —— 须 &lt; `DOWNWARD_PRESSURE`(`AC-1-34`)。</summary>
        public float SkinWidth;
        /// <summary>胶囊半径 —— 与 `LATTICE_SIZE` 有几何约束(EC-12:`LATTICE_SIZE ≥ radius × 2`)。</summary>
        public float Radius;
        /// <summary>胶囊总高。</summary>
        public float Height;
        /// <summary>胶囊中心偏移 —— **须为原点**:R6 的「用中心点取 floor」以 `transform.position`
        /// 为取样点(GDD F-1-9),偏移非零会让格判定与胶囊实际位置错开。</summary>
        public Vector3 Center;
        /// <summary>叠加时的自动分离 —— P0 保持默认 `true`(GDD F-1-9)。</summary>
        public bool EnableOverlapRecovery;
    }

    /// <summary>
    /// 移动手感链求值器。
    /// </summary>
    public sealed class LocomotionEvaluator
    {
        private readonly List<string> _evaluationOrder = new List<string>();

        /// <summary>
        /// 稳态速度(AC-1-19)。
        /// </summary>
        public float SteadyStateSpeed(Vector3 moveInput, YawBasis basis, float kTerrain)
        {
            float inputMagnitude = moveInput.magnitude;
            return LocomotionConfig.LoadDefault().SpeedWalk * inputMagnitude * kTerrain;
        }

        /// <summary>
        /// 更新 yaw(AC-1-20a)。
        /// </summary>
        public float UpdateYaw(Vector3 velocity, float currentYaw, float dt)
        {
            // v_horiz ≈ 0 时 yaw 保持
            if (velocity.sqrMagnitude < 0.0001f)
                {
                    _evaluationOrder.Add("turn");
                    return currentYaw;
                }

            // 自动面向移动方向
            float yawTarget = Mathf.Atan2(velocity.x, velocity.z) * Mathf.Rad2Deg;
            float delta = Mathf.DeltaAngle(currentYaw, yawTarget);
            float maxDelta = LocomotionConfig.LoadDefault().TurnRate * dt;
            float newYaw = currentYaw + Mathf.Clamp(delta, -maxDelta, maxDelta);

            _evaluationOrder.Add("turn");
            return newYaw;
        }

        /// <summary>
        /// 计算 v_target(AC-1-18)。
        /// </summary>
        public float ComputeVTarget(float inputMagnitude, float[] kTerrainSpeeds, int currentCellIndex, YawBasis basis)
        {
            _evaluationOrder.Add("read_cell");
            _evaluationOrder.Add("v_target");

            float kTerrain = kTerrainSpeeds[currentCellIndex];
            return LocomotionConfig.LoadDefault().SpeedWalk * inputMagnitude * kTerrain;
        }

        /// <summary>
        /// 获取最近一次求值次序。
        /// </summary>
        public string[] GetLastEvaluationOrder()
        {
            return _evaluationOrder.ToArray();
        }
    }
}
