// camera-viewpoint Story 005 —— 档位状态机 + 性能义务
//
// 权威来源:
//   GDD camera-and-viewpoint.md R-2-5(三档 · 优先级 · 转场规则)/ EC-2-8/9/10/11/14 · 组 5
//   ADR-020 §五(相机只读不持状态;档位是**呈现参数**不是游戏事实)
//   ADR-011(③ 显式相位,禁 Script Execution Order)
//   ADR-013(ModalId 闭集;O-12:39 是唯一请求方)
//
// 两处订正(2026-09-16 评审):
//   · EC-2-9 结算算法收敛为「每帧结算一次」四步(原「同优先级取最后」与「同档幂等」
//     互相纠缠 ⇒ **活锁观感**)。
//   · AC-2-20 原文「无任何计时字段」在转场 `t` 存在时**恒假** ⇒ 精确为
//     「无与**档位存续时间**相关的计时器 / 看门狗」。

using System;
using System.Collections.Generic;
using UnityEngine;

namespace DaYiJingCheng.Gameplay.Presentation.Camera
{
    /// <summary>
    /// 档位优先级(R-2-5 的语义序:模态 > 动作 > 默认)。
    /// ⚠️ 数值大 = 优先级高。
    /// 🔴 **2026-10-03 评审修复(A)**:初版把 `FirstPerson` 也塞进本格(= 3,与 `Casebook` 同值)
    /// ⇒ `Settle` 的同优先级「后到者胜」使 **VR 与脉案可互抢**,违 R-2-9 / EC-2-11。
    /// ⇒ 现 `FirstPerson` **不属于本格**(独立路径,见 <see cref="CameraModeMachine.Settle"/> 早退)。
    /// </summary>
    public static class CameraModePriority
    {
        public static int Of(CameraMode m) => m switch
        {
            CameraMode.Casebook => 3,
            CameraMode.Treatment => 2,
            CameraMode.Explore => 1,
            // ⚠️ FirstPerson **不经本格** —— R-2-9「VR 是另一条路径,不复用本链」。
            //    Settle 在优先级裁决**之前**对 FirstPerson 早退处理;此处返回 0 仅为兜底,
            //    正常路径永不消费该值。
            CameraMode.FirstPerson => 0,
            _ => 0,
        };
    }

    /// <summary>一次档位请求(意图制:档位**只能**由请求方设定)。</summary>
    public readonly struct CameraModeRequest
    {
        public readonly CameraMode Mode;
        public readonly int RequesterId;   // 请求方系统号(4/8/10/39…);仅审计用
        public CameraModeRequest(CameraMode mode, int requesterId)
        { Mode = mode; RequesterId = requesterId; }
    }

    /// <summary>
    /// 档位状态机 —— **意图制** + 每帧一结算 + 转场。
    /// </summary>
    /// <remarks>
    /// ⚠️ **AC-2-17**:`Mode` 的**唯一写入点**是本类的 `SetMode`/结算路径;
    /// 其方法体**不读**任何其它系统状态(无 8 界面栈 / 10 动作状态 / 39 模态查询)。
    /// ⚠️ **AC-2-20**:本类**无与档位存续时间相关的计时器 / 看门狗**;
    /// `TransitionT` 是**转场**进度(允许),不是档位存续计时。
    /// </remarks>
    public sealed class CameraModeMachine
    {
        private readonly List<CameraModeRequest> _pending = new List<CameraModeRequest>();

        /// <summary>当前档(唯一写入点 = 本类内部)。</summary>
        public CameraMode Mode { get; private set; } = CameraMode.Explore;

        /// <summary>转场进度 t ∈ [0,1](转场计时,**允许** —— AC-2-20 订正)。</summary>
        public float TransitionT { get; private set; } = 1f;

        /// <summary>转场起始值(**被打断时取当前插值值** —— AC-2-18③)。</summary>
        public float TransitionFrom { get; private set; }

        /// <summary>转场目标值。</summary>
        public float TransitionTo { get; private set; }

        /// <summary>转场重起算次数(供 AC-2-18②④ 判据)。</summary>
        public int TransitionRestarts { get; private set; }

        /// <summary>
        /// **唯一入口**(AC-2-17):请求方登记意图。
        /// ⚠️ 本方法体**不读**任何其它系统状态 —— 它只把请求入队。
        /// </summary>
        public void SetMode(CameraMode mode, int requesterId)
        {
            _pending.Add(new CameraModeRequest(mode, requesterId));
        }

        /// <summary>
        /// 每帧**结算一次**(EC-2-9 的四步收敛;AC-2-18④)。
        /// </summary>
        /// <param name="dt">帧时长。</param>
        /// <param name="transitionDuration">转场时长(组 5;数值留白)。</param>
        /// <returns>本帧是否发生了档位变更。</returns>
        public bool Settle(float dt, float transitionDuration)
        {
            // ⚠️ **2026-10-03 评审修复(§3/§5 修复清单 #5)**:返回值语义。
            //    初版 `return has && TransitionRestarts > 0 && TransitionT == 0f;` ——
            //    `TransitionRestarts` 是**累积**计数,一旦发生过任何切换就恒 `> 0`
            //    ⇒ 此后每次带请求的 Settle 都返回 true(哪怕本帧没换档),与 XML 注释矛盾。
            //    现改为**本帧是否真的发生了档位变更**。
            bool changedThisFrame = false;

            // ── ⓪ VR 独立路径早退(R-2-9:FirstPerson **不复用本链**)────────────
            //    初版把 FirstPerson 塞进优先级格(= 3,与 Casebook 同值)⇒ 同优先级「后到者胜」
            //    使 VR 与脉案**可互抢**(GDD EC-2-11 明禁:「FirstPerson 生效 ⇒ 平面链全部冻结」)。
            //    ⇒ 现 FirstPerson **不经** `CameraModePriority`,在裁决前单独处理:
            //      · 平面态收到 FirstPerson 请求 ⇒ 进入 VR(平面链冻结);
            //      · VR 在位 ⇒ 丢弃**所有**平面档请求,唯一放行的是「退出 VR」的 Explore。
            bool hasVrRequest = false, hasExploreRequest = false;
            for (int i = 0; i < _pending.Count; i++)
            {
                if (_pending[i].Mode == CameraMode.FirstPerson) hasVrRequest = true;
                else if (_pending[i].Mode == CameraMode.Explore) hasExploreRequest = true;
            }

            if (Mode != CameraMode.FirstPerson && hasVrRequest)
            {
                ApplyMode(CameraMode.FirstPerson);
                changedThisFrame = true;
                _pending.Clear();
            }
            else if (Mode == CameraMode.FirstPerson)
            {
                // VR 在位:平面链冻结 ⇒ 只接受「退出 VR」(由 Explore 请求表达)
                if (hasExploreRequest)
                {
                    ApplyMode(CameraMode.Explore);
                    changedThisFrame = true;
                }
                _pending.Clear();
            }
            else
            {
                // ── ① 取本帧**最高优先级**的请求(同优先级 ⇒ 取**最后**入队者)────
                bool has = false;
                CameraModeRequest best = default;
                int bestPrio = int.MinValue;
                foreach (var r in _pending)
                {
                    int prio = CameraModePriority.Of(r.Mode);
                    if (prio > bestPrio || (prio == bestPrio))   // 同优先级 ⇒ 后者胜
                    {
                        bestPrio = prio;
                        best = r;
                        has = true;
                    }
                }
                _pending.Clear();

                // ── ② **先裁决**再推进转场计时 ──────────────────────────────
                // ⚠️ **2026-10-03 修正**:初版**先推进 `TransitionT` 再裁决** ⇒
                //    打断帧的 `from` 取的是**推进后**的插值值(比打断那一刻多一帧)
                //    ⇒ 画面会有**一帧的跳变**(恰是 AC-2-18③ 要防的)。
                //    ⇒ 顺序改为:裁决(用**当前** t 取 from)→ 再推进 t。
                if (has)
                {
                    int curPrio = CameraModePriority.Of(Mode);
                    bool canSwitch = bestPrio >= curPrio && best.Mode != Mode;
                    if (canSwitch)
                    {
                        // from 取**打断那一刻**的当前插值值(AC-2-18③)
                        ApplyMode(best.Mode);
                        changedThisFrame = true;
                    }
                }
            }

            // ── ③ 推进转场计时 ─────────────────────────────────────────
            if (TransitionT < 1f)
                TransitionT = Mathf.Min(1f, TransitionT + dt / Mathf.Max(transitionDuration, 1e-6f));

            return changedThisFrame;
        }

        /// <summary>
        /// 当前**插值值** —— 转场中被再次打断时,新 `from` 取它(而非源档值)。
        /// </summary>
        private float CurrentInterpolated()
            => TransitionFrom + (TransitionTo - TransitionFrom) * TransitionT;

        /// <summary>
        /// **`Mode` 的唯一写入点**(AC-2-17):起转场 + 置档 + 计次。
        /// 所有切档路径(VR 早退 / VR 退出 / 优先级裁决)都经此 —— 使类体内 `Mode =` 恰一处。
        /// </summary>
        private void ApplyMode(CameraMode mode)
        {
            TransitionFrom = CurrentInterpolated();   // 打断那一刻的插值值(AC-2-18③)
            TransitionTo = 1f;
            TransitionT = 0f;
            Mode = mode;                              // ← Mode 的唯一写入点
            TransitionRestarts++;
        }

        /// <summary>
        /// 是否处于冻结档(锚冻结、Look 不驱动、无移动、**不发起物理查询** —— AC-2-27②)。
        /// 🔴 **2026-10-03 评审修复**:初版 `Mode == Casebook` 遗漏 `FirstPerson`
        /// (VR 在位 ⇒ 平面链**全部冻结**,EC-2-11)⇒ 补入。
        /// </summary>
        public bool IsFrozen => Mode == CameraMode.Casebook || Mode == CameraMode.FirstPerson;

        /// <summary>
        /// Casebook 的固定高俯角(逐档常量;AC-2-19②)。
        /// 🔴 **2026-10-03 评审修复(承 TD §1「裂缝」)**:初版 `{ get; set; }` —— **public 可写**
        /// 的第二档位写口(任何人可改,不经 `SetMode`),与 AC-2-17「唯一写入点」精神相抵。
        /// 现 `private set`,经 <see cref="ConfigureCasebookPitch"/> 的**注入缝**设定
        /// (归组 5 参数表,非档位状态)。
        /// </summary>
        public float CasebookPitch { get; private set; } = 45f;   // PITCH_CASEBOOK(须 < PITCH_MAX=60;数值留白)

        /// <summary>注入缝:组 5 参数表设定 Casebook 俯角(装载期;非档位状态写入)。</summary>
        public void ConfigureCasebookPitch(float pitchDeg) => CasebookPitch = pitchDeg;
    }

    /// <summary>
    /// **计数包装**的物理查询接口 —— 使「每帧恰一次」可断言(AC-2-27①)。
    /// ⚠️ `Physics.SphereCast` 是**静态 API 不可拦截** ⇒ 相机须经本抽象调用
    /// (而非直调 `Physics.*`)。
    /// </summary>
    public sealed class CountingArmQuery : IArmCollisionQuery
    {
        private readonly IArmCollisionQuery _inner;
        private int _frameCount;
        private int _lastFrameId = int.MinValue;

        public CountingArmQuery(IArmCollisionQuery inner)
            => _inner = inner ?? throw new ArgumentNullException(nameof(inner));

        /// <summary>本帧累计查询数。</summary>
        public int FrameCount => _frameCount;

        /// <summary>本帧序号(判定权在被测对象侧,而非调用方 —— TD §3)。</summary>
        public int FrameId => _lastFrameId;

        /// <summary>
        /// 帧边沿:以**外部帧号**清零计数(由单一 Tick 相位调用)。
        /// 🔴 **2026-10-03 评审修复(TD §3)**:初版无帧号 ⇒ 「帧边沿」纯靠调用方自律
        /// (不调就永累积、调两次就丢一半)—— 计数**证明不了**「每帧恰一次」。
        /// 现:帧号**单调递增**;`BeginFrame` 传入非递增帧号 ⇒ 抛(重复调用 / 漏调用即红)。
        /// </summary>
        public void BeginFrame(int frameId)
        {
            if (frameId <= _lastFrameId)
                throw new InvalidOperationException(
                    $"帧号须**严格递增**(实得 {frameId} ≤ 上一帧 {_lastFrameId})—— " +
                    "帧边沿被重复调用 / 漏调用;AC-2-27① 的计数前提不成立。");
            _lastFrameId = frameId;
            _frameCount = 0;
        }

        /// <summary>
        /// 无帧号的兼容重载(帧号自增)。
        /// ⚠️ 生产相位驱动须用 <see cref="BeginFrame(int)"/> 传外部帧号;此重载为测试/迁移便利。
        /// </summary>
        public void BeginFrame() => BeginFrame(_lastFrameId == int.MinValue ? 0 : _lastFrameId + 1);

        public (bool hit, float distance) Cast(Vector3 o, Vector3 d, float r, float maxDist)
        {
            _frameCount++;
            return _inner.Cast(o, d, r, maxDist);
        }
    }

    /// <summary>
    /// **真实**的 PhysX 碰撞查询(AC-2-27① 的**生产**实现;TD §1 / QA §5 要求的接线)。
    /// 🔴 **2026-10-03 新增**:此前相机缝只有测试桩,**生产零接线** ⇒
    /// 「每帧恰一次查询」无从在真实求值路径上被计数。本类即那个生产实现。
    /// ⚠️ `QueryTriggerInteraction.Ignore` **必须显式传**(默认随重载不同 ⇒ 静默差异;AC-2-15③)。
    /// </summary>
    public sealed class PhysicsArmQuery : IArmCollisionQuery
    {
        public (bool hit, float distance) Cast(Vector3 origin, Vector3 dir, float radius, float maxDist)
        {
            // ⚠️ 显式 QueryTriggerInteraction.Ignore —— 触发体不顶相机(AC-2-15③)
            if (Physics.SphereCast(origin, radius, dir, out RaycastHit hit, maxDist,
                                   ~0, QueryTriggerInteraction.Ignore))
            {
                return (true, hit.distance);
            }
            // 未命中 ⇒ (false, 0);调用方按 GDD F-2-4 主用例取 d_raw := ARM_LEN
            return (false, 0f);
        }
    }
}
