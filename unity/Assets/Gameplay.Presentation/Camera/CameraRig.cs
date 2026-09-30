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
