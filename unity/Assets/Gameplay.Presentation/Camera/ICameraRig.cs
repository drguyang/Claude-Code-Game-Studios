// ADR-020 Amendment B —— ICameraRig 接口。
//
// 权威来源:
//   ADR-020 Amendment B —— YawBasis = (Vector3 fwd, Vector3 right) 水平化正交基,只读
//   ADR-020 §五 —— 相机只读不持状态
//   GDD camera-and-viewpoint.md —— AC-2-07/08/09/10
//
// AC-2-10②: 相机对 1 零 public 写入面
//   - YawBasis 返回不可变 struct
//   - 不暴露相机 Transform / Camera 句柄

using UnityEngine;

namespace DaYiJingCheng.Gameplay.Presentation.Camera
{
    /// <summary>
    /// 水平化正交基（不可变）。
    /// </summary>
    public readonly struct YawBasis
    {
        public readonly Vector3 Fwd;
        public readonly Vector3 Right;

        public YawBasis(Vector3 fwd, Vector3 right)
        {
            Fwd = fwd;
            Right = right;
        }
    }

    /// <summary>
    /// 相机机位接口 —— 只读，供系统 1 投影 MoveInput。
    /// </summary>
    /// <summary>
    /// 相机档(ADR-020 §Key Interfaces 的权威形状)。
    /// ⚠️ `FirstPerson`(VR)= **P1a**;P0 只落枚举与接口(TR-camera-002)。
    /// </summary>
    public enum CameraMode
    {
        Explore,      // 平面探索(第三人称越肩)
        Treatment,    // 处置态(10 急救)
        Casebook,     // 脉案(39)
        FirstPerson,  // VR(P1a)—— 头显驱动,不经本链;镜头效果全禁
    }

    public interface ICameraRig
    {
        /// <summary>
        /// 水平化正交基。f̂.y == 0 ∧ r̂.y == 0，与 pitch 无关。
        /// 构造式：f̂ := (sin yaw, 0, cos yaw)；r̂ := (cos yaw, 0, −sin yaw)。
        /// </summary>
        YawBasis YawBasis { get; }

        /// <summary>
        /// 当前 yaw（弧度）。
        /// </summary>
        float Yaw { get; }

        /// <summary>
        /// 当前 pitch（度，正 = 俯）。
        /// </summary>
        float Pitch { get; }
    }
}
