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

        /// <summary>
        /// 绕点灵敏度(R-2-3 组 1 的 `SENS`;数值留白,归用户数值轮)。
        /// <para>GDD F-2-2 的输入侧式:`yaw += Look.x × SENS × dt`(yaw **弧度**);
        /// `pitch := clamp(pitch + Look.y × SENS × dt, PITCH_MIN, PITCH_MAX)`(pitch **度**)。
        /// ⇒ **两根轴各自单位域一根旋钮**(弧度域 / 度域),跨单位换算唯一发生在
        /// `YawBasis` 构造(story 002)。</para>
        /// 🔴 **2026-10-03 评审修复(A1)**:`ApplyOrbit` 初版**丢弃 `dtSeconds` 与 `SENS`**
        /// (`_ = dtSeconds;`,注释却写「以 dt 线性缩放」)—— 与 GDD 式不符,且带 `dt` 依赖的
        /// 期望值会被钉死。现按 GDD 式逐字落地。
        /// </summary>
        public const float LOOK_SENS_X = 0.1f;   // 弧度 / (单位 × 秒)
        public const float LOOK_SENS_Y = 0.1f;   // 度 / (单位 × 秒)

        /// <summary>
        /// 档位转场的**位置跳变**容差(组 5b;`TRANSITION_JUMP_EPS`,数值留白)。
        /// 🔴 **2026-10-03 评审修复(F)**:初版全仓**零命中**该常量实体,`AC-2-18③` 只断言
        /// 「标量前后相等」,从未采样相机变换。现落为**唯一定义实体**,
        /// 并让 <see cref="CameraModeMachine"/> 的转场采样经此判据(承 story 002 的「同一常量实体」纪律)。
        /// </summary>
        public const float TRANSITION_JUMP_EPS = 1e-3f;

        /// <summary>
        /// AC-2-09 装载期校验(PITCH_MAX &lt; 90° 等俯角界)。
        /// ⚠️ **2026-10-03 补(评审 F3)**:原 AC-2-09① 无校验体 —— const 写在那里
        /// 但从未有任何断言/校验消费它,判据挂空。
        /// Guardrail 口径(「取值一旦存在即被守住」):值已存在 ⇒ 装载期即校验;
        /// **参数化**是为让负向夹具注入违例值(不违反「不在空值上跑断言」—— 校验的是具体值)。
        /// </summary>
        /// <exception cref="System.ArgumentErrorException">违反俯角界时抛,错误串点名双值。</exception>
        public static void ValidatePitchLimits(float min, float max)
        {
            if (!(max < 90f))
                throw new System.ArgumentException(
                    $"AC-2-09①:PITCH_MAX({max}) 须 < 90° —— 过大俯角使相机翻转穿地", nameof(max));
            if (!(min > -90f))
                throw new System.ArgumentException(
                    $"PITCH_MIN({min}) 须 > -90°(同族下界,防翻仰)", nameof(min));
            if (min >= max)
                throw new System.ArgumentException(
                    $"PITCH_MIN({min}) 须 < PITCH_MAX({max}) —— 空区间使 pitch 恒被钳死", nameof(min));
            if (!(min < 0f))
                throw new System.ArgumentException(
                    $"PITCH_MIN({min}) 须 < 0 —— 仰视允许是 R-2-3 符号约定的直接判据(AC-2-09①)", nameof(min));
        }

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

        /// <summary>档位**请求**入口(意图制,R-2-5;ADR-020 §Key Interfaces)。</summary>
        public void SetMode(CameraMode mode) => _modeMachine.SetMode(mode, requesterId: 0);

        /// <summary>
        /// 每帧求值入口(单一显式相位;AC-2-27③)。结算档位 + 同步档位姿态。
        /// </summary>
        /// <param name="deltaTime">表现态帧时长(非 tick)。</param>
        public void Tick(float deltaTime)
        {
            _modeMachine.Settle(deltaTime, TransitionDuration);
            SyncPoseFromMode(_modeMachine);
        }

        /// <summary>档位转场时长(组 5;数值留白,归用户数值轮)。</summary>
        public float TransitionDuration = 0.3f;

        /// <summary>
        /// 平面模式的主相机(44 的 `AudioListener` 挂点;`OQ-2-6` 的 VR 语义见接口注释)。
        /// ⚠️ 惰性解析**本 GameObject 上**的 `Camera` 组件(Boot 常驻场景)。
        /// </summary>
        public UnityEngine.Camera Camera
        {
            get
            {
                if (_camera == null) _camera = GetComponent<UnityEngine.Camera>();
                return _camera;
            }
        }

        private UnityEngine.Camera _camera;

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
        /// 把档位姿态同步到相机(AC-2-19② 的**承载物**)。
        /// <para>进入 `Casebook` ⇒ `pitch` **收敛到该档的约定固定高俯角** `PITCH_CASEBOOK`
        /// (而非玩家进来时的值)。</para>
        /// 🔴 **2026-10-03 评审修复(TD §3.3 / QA §3.1)**:此前 `CasebookPitch` 是
        /// **无消费者的孤立常量**,「pitch 收敛」无承载物可验。现补此生产路径。
        /// </summary>
        public void SyncPoseFromMode(CameraModeMachine machine)
        {
            if (machine == null) throw new System.ArgumentNullException(nameof(machine));
            if (machine.Mode == CameraMode.Casebook)
                _pitch = Mathf.Clamp(machine.CasebookPitch, PITCH_MIN, PITCH_MAX);
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
            // GDD F-2-2 输入侧式(逐字):yaw += Look.x × SENS × dt; pitch := clamp(pitch + Look.y × SENS × dt)
            // 解耦:两条链各在自己单位域累积,互不短路
            UpdateYaw(lookX * LOOK_SENS_X * dtSeconds);               // yaw 照常累积(弧度域)
            UpdatePitch(lookY * LOOK_SENS_Y * dtSeconds);             // pitch 触界即钳,不影响 yaw(度域)
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

        /// <summary>
        /// 更新 yaw(绕点段)。
        /// </summary>
        /// <remarks>
        /// **AC-2-10① 相位落点决策(2026-10-03 实现期定死,承 EC-2-14 原文要求)**:
        /// 选 **B 路 = `ICameraRig.UpdateYaw()` 显式驱动** —— 调用方在自身固定相位
        /// (如 `InputSystem.onAfterUpdate`,ADR-011 先例)调入本方法;
        /// **禁止**依赖 `Script Execution Order` 面板的隐式排序
        /// (场景资产,跨 prefab 不传递 ⇒ 静默失效)。
        /// 注:此前该决策**只写在 story 文档里、代码零注释**(评审 F2)⇒ 现落码。
        /// </remarks>
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
