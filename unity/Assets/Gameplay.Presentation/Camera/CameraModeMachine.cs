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
    /// </summary>
    public static class CameraModePriority
    {
        public static int Of(CameraMode m) => m switch
        {
            CameraMode.Casebook => 3,
            CameraMode.Treatment => 2,
            CameraMode.Explore => 1,
            // ⚠️ 2026-10-03:FirstPerson 是 **P1a 独立路径**,与三档**并列**而非从属
            //    (初版 0 ⇒ 被 Explore(1) 的优先级门拒)
            CameraMode.FirstPerson => 3,
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
            // ── ① 取本帧**最高优先级**的请求(同优先级 ⇒ 取**最后**入队者)────────
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
                    TransitionFrom = CurrentInterpolated();
                    TransitionTo = 1f;
                    TransitionT = 0f;
                    Mode = best.Mode;                      // ← Mode 的唯一写入点
                    TransitionRestarts++;
                }
            }

            // ── ③ 推进转场计时 ─────────────────────────────────────────
            if (TransitionT < 1f)
                TransitionT = Mathf.Min(1f, TransitionT + dt / Mathf.Max(transitionDuration, 1e-6f));

            return has && TransitionRestarts > 0 && TransitionT == 0f;
        }

        /// <summary>
        /// 当前**插值值** —— 转场中被再次打断时,新 `from` 取它(而非源档值)。
        /// </summary>
        private float CurrentInterpolated()
            => TransitionFrom + (TransitionTo - TransitionFrom) * TransitionT;

        /// <summary>是否处于冻结档(锚冻结、Look 不驱动、无移动 —— AC-2-27②)。</summary>
        public bool IsFrozen => Mode == CameraMode.Casebook;

        /// <summary>Casebook 的固定高俯角(逐档常量;AC-2-19②)。</summary>
        public float CasebookPitch { get; set; } = 45f;   // PITCH_CASEBOOK(须 < PITCH_MAX=60;数值留白)
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

        public CountingArmQuery(IArmCollisionQuery inner)
            => _inner = inner ?? throw new ArgumentNullException(nameof(inner));

        /// <summary>本帧累计查询数。</summary>
        public int FrameCount => _frameCount;

        /// <summary>帧边沿:清零计数(由单一 Tick 相位调用)。</summary>
        public void BeginFrame() => _frameCount = 0;

        public (bool hit, float distance) Cast(Vector3 o, Vector3 d, float r, float maxDist)
        {
            _frameCount++;
            return _inner.Cast(o, d, r, maxDist);
        }
    }
}
