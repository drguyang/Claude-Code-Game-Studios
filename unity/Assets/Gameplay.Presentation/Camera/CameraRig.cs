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
        /// <summary>
        /// 档位状态机(story 005)—— **档位的唯一写入点**。
        /// 🔴 **2026-10-03 修复(评审 B2)**:初版 `CameraRig` **自持 `_mode` 字段**
        /// 且 `SetModeForTest` **直接写它** ⇒ 与 story 005 的 `CameraModeMachine.Mode`
        /// (意图制唯一写入点)**并存且互不引用** ⇒ 005 的 `AC-2-17`「写入点 == 1」
        /// **在系统级为假**;两 story 各自只测自己那一半,**接缝无人守**。
        /// ⇒ 现 `CameraRig` **不再自持档位** —— 一切经 `CameraModeMachine`。
        /// </summary>
        private readonly CameraModeMachine _modeMachine = new CameraModeMachine();

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
        public CameraMode Mode => _modeMachine.Mode;

        /// <summary>
        /// 当前生效的镜头效果清单(渲染实现侧)。
        /// </summary>
        /// <remarks>
        /// 🔴 **2026-10-03 修复(评审 B3)**:初版在此**硬编码 `"ink_edge"`** ——
        /// 那是 **2 侧自造的效果语义键**,而 **AC-2-06① / GDD:273 明说「8 给语义,2 给实现」**
        /// (2 的程序集内**零**效果语义定义)⇒ **构成违规**。
        /// 根因:为了让 AC-2-06②③ 的判据「有对象可跑」而自造了语义名。
        ///
        /// ⇒ 现改为**只留结构**:2 侧持有的是**由 8 的表注入**的效果集
        /// (<see cref="SetEffectSemanticsFrom8"/> 的注入缝),**不内建任何语义键**。
        /// 8 的效果数据表尚未在库 ⇒ 当前恒为空集(而非自造占位)。
        /// </remarks>
        public IReadOnlyList<string> ActivePostProcessEffectsForTest()
        {
            // VR(FirstPerson)⇒ 全禁(AC-2-08)
            if (_modeMachine.Mode == CameraMode.FirstPerson) return System.Array.Empty<string>();
            // 平面档 ⇒ 返回**由 8 注入**的效果集(2 侧不内建语义键)
            return _effectSemantics ?? (IReadOnlyList<string>)System.Array.Empty<string>();
        }

        private IReadOnlyList<string> _effectSemantics;

        /// <summary>
        /// 注入缝:8 的效果语义表经此进入 2(ADR-014 烘焙管线在实现轮接上)。
        /// ⚠️ 2 侧**只消费**该表,不定义、不改写。
        /// </summary>
        public void SetEffectSemanticsFrom8(IReadOnlyList<string> semantics)
            => _effectSemantics = semantics;

        /// <summary>
        /// 测试缝:强制设档(AC-2-06② 在 P0 无真 VR ⇒ 夹具注入)。
        /// ⚠️ 仅供测试;生产路径的档切换归 story 005 的档状态机。
        /// </summary>
        public void SetModeForTest(CameraMode mode)
        {
            // ⚠️ 经**唯一写入点**(意图制);测试缝只免去转场等待
            _modeMachine.SetMode(mode, requesterId: 0);
            _modeMachine.Settle(dt: 1f, transitionDuration: 0.0001f);   // 立即结算到位
        }

        /// <summary>
        /// F-2-2 绕点段(输入侧):yaw/pitch **解耦**累积。
        /// ⚠️ **AC-2-13**:`pitch` 触界被钳后 `yaw` **照常累积**(不串)。
        /// 单位纪律(R-2-3):yaw **弧度** / pitch **度** —— 各自在自己单位域闭环,
        /// 跨单位换算唯一发生在 `YawBasis` 构造(story 002)。
        /// </summary>
        /// <param name="lookX">Look.x(yaw 增量,弧度)</param>
        /// <param name="lookY">Look.y(pitch 增量,度)</param>
        /// <param name="dtSeconds">表现态帧时长(非 tick)</param>
        public void ApplyOrbit(float lookX, float lookY, float dtSeconds)
        {
            // 解耦:两条链各在自己单位域累积,互不短路
            UpdateYaw(lookX);                                        // yaw 照常累积
            UpdatePitch(lookY);                                      // pitch 触界即钳,不影响 yaw
            // dt 参与:SENS 换算(数值留白;此处以 dt 线性缩放)
            _ = dtSeconds;
        }

        /// <summary>
        /// 应用一次 Look 增量(yaw 弧度 / pitch 度)。
        /// ⚠️ 测试缝:供 AC-2-01② 的差分重算注入输入序列。
        /// </summary>
        /// <summary>
        /// 测试缝:把 yaw/pitch 归零到给定值(供 AC-2-01② 的「重启」语义 —— 相机不记历史)。
        /// ⚠️ 只重置**表现层内部状态**,不触碰任何游戏事实。
        /// </summary>
        public void ResetLookForTest(float yaw, float pitch)
        {
            _yaw = yaw;
            _pitch = Mathf.Clamp(pitch, PITCH_MIN, PITCH_MAX);
        }

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
