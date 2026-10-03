// ADR-020 Amendment B —— CameraRig 实现。
//
// 权威来源:
//   ADR-2020 Amendment B —— YawBasis 构造式
//   GDD camera-and-viewpoint.md —— AC-2-07/08/09/10
//
// 构造式（逐字落地）：
//   f̂ := (sin yaw, 0, cos yaw)
//   r̂ := (cos yaw, 0, −sin yaw)
//
// 禁止：先取相机 forward 再水平化再归一（事后形态）

using System.Collections.Generic;
using UnityEngine;

namespace DaYiJingCheng.Gameplay.Presentation.Camera
{
    /// <summary>
    /// 相机机位 —— 第三人称越肩。
    /// </summary>
    public sealed class CameraRig : MonoBehaviour, ICameraRig
    {
        [Header("Yaw/Pitch")]
        [SerializeField] private float _yaw = 0f;
        [SerializeField] private float _pitch = 30f;

        [Header("Distance")]
        [SerializeField] private float _distance = 5f;

        // ── AC-2-06:档位与镜头效果归属(ADR-020 §六)────────────────
        // ⚠️ 效果**语义**归 8(数据表,经 ADR-014 烘焙);此处只持**渲染实现**的开关。
        //    `FirstPerson`(VR)⇒ 效果**全禁**(AC-20-08)。
        private CameraMode _mode = CameraMode.Explore;

        // AC-2-08: YAW_BASIS_EPS 唯一定义点
        public const float YAW_BASIS_EPS = 1e-5f;

        // AC-2-09: PITCH_MIN/MAX 装载期断言
        public const float PITCH_MIN = -45f;
        public const float PITCH_MAX = 60f;

        /// <inheritdoc />
        public float Yaw => _yaw;

        /// <inheritdoc />
        public float Pitch => _pitch;

        /// <inheritdoc />
        public YawBasis YawBasis
        {
            get
            {
                // 构造式（逐字落地）
                float sinYaw = Mathf.Sin(_yaw);
                float cosYaw = Mathf.Cos(_yaw);

                Vector3 fwd = new Vector3(sinYaw, 0f, cosYaw);
                Vector3 right = new Vector3(cosYaw, 0f, -sinYaw);

                return new YawBasis(fwd, right);
            }
        }

        /// <summary>
        /// 更新 yaw（弧度）。
        /// </summary>
        /// <summary>当前档(ADR-020 §Key Interfaces)。</summary>
        public CameraMode Mode => _mode;

        /// <summary>
        /// 当前生效的镜头效果清单(渲染实现侧)。
        /// ⚠️ 效果**不得用于报状态**(AC-20-09)—— 本清单只驱动渲染,不进任何流。
        /// </summary>
        public IReadOnlyList<string> ActivePostProcessEffectsForTest()
        {
            // VR(FirstPerson)⇒ 全禁(AC-20-08)
            if (_mode == CameraMode.FirstPerson) return System.Array.Empty<string>();
            // 平面档 ⇒ 效果可用(语义归 8,此处为渲染实现占位)
            return new[] { "ink_edge" };
        }

        /// <summary>
        /// 测试缝:强制设档(AC-2-06② 在 P0 无真 VR ⇒ 夹具注入)。
        /// ⚠️ 仅供测试;生产路径的档切换归 story 005 的档状态机。
        /// </summary>
        public void SetModeForTest(CameraMode mode) => _mode = mode;

        /// <summary>
        /// 应用一次 Look 增量(yaw 弧度 / pitch 度)。
        /// ⚠️ 测试缝:供 AC-2-01② 的差分重算注入输入序列。
        /// </summary>
        public void ApplyLook(float deltaYaw, float deltaPitch)
        {
            UpdateYaw(deltaYaw);
            UpdatePitch(deltaPitch);
        }

        public void UpdateYaw(float deltaYaw)
        {
            _yaw += deltaYaw;
            // 回绕到 [0, 2π)
            _yaw = Mathf.Repeat(_yaw, Mathf.PI * 2f);
        }

        /// <summary>
        /// 更新 pitch（度）。
        /// </summary>
        public void UpdatePitch(float deltaPitch)
        {
            _pitch = Mathf.Clamp(_pitch + deltaPitch, PITCH_MIN, PITCH_MAX);
        }

        /// <summary>
        /// 相机位置（绕点旋转）。
        /// </summary>
        public Vector3 GetCameraPosition(Vector3 target)
        {
            float pitchRad = _pitch * Mathf.Deg2Rad;
            float cosPitch = Mathf.Cos(pitchRad);
            float sinPitch = Mathf.Sin(pitchRad);

            Vector3 offset = new Vector3(
                Mathf.Sin(_yaw) * cosPitch,
                sinPitch,
                Mathf.Cos(_yaw) * cosPitch
            ) * _distance;

            return target + offset;
        }
    }
}
