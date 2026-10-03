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

        // ── ADR-020 §Key Interfaces 的四成员(2026-10-03 评审修复 B-4/③ 补齐)──────
        //  🔴 初版仅落 YawBasis/Yaw/Pitch ⇒ ADR-020 明列的 Mode/SetMode/Tick/Camera **四成员缺失**,
        //     `OQ-2-6` 的 doc comment 亦未落 —— 而 `OQ-2-6` 自陈「P0 改接口近乎零成本,
        //     推到 P1a 则要动 ADR-020 定义」⇒ 零成本窗口正在关闭。本批偿付。

        /// <summary>当前档(ADR-020 §Key Interfaces)。</summary>
        CameraMode Mode { get; }

        /// <summary>
        /// 档位**请求**入口(意图制,R-2-5):请求方(10 / 39)登记意图,相机决定是否接受与如何转场。
        /// ⚠️ 这是**请求**不是**控制** —— 相机不因它立即切档(结算在单一相位,见 <see cref="Tick"/>)。
        /// </summary>
        void SetMode(CameraMode mode);

        /// <summary>
        /// 每帧求值入口 —— **单一显式相位**(AC-2-27③;承 ADR-011 的固定相位,
        /// 禁 `Script Execution Order` 隐式排序)。跟随 / 越肩阻尼 / 档位结算都在此。
        /// </summary>
        void Tick(float deltaTime);

        /// <summary>
        /// 平面模式的主相机(44 的 `AudioListener` 挂点 —— ADR-020 §七 / AC-20-10)。
        /// <para>🔴 **`OQ-2-6`(VR 立体语义,P1a 前置)**:本属性暴露**单个** `Camera`;
        /// VR 立体的双眼需要一对(或一个 XR 渲染路径)。本属性在 `FirstPerson` 档
        /// **代表哪只眼 / 还是那个「主」相机** —— **P0 未定**,归 P1a 开工前裁定。
        /// P0 只需保证该接口的扩容**不破坏平面链**(TR-camera-002)。</para>
        /// </summary>
        UnityEngine.Camera Camera { get; }
    }
}
