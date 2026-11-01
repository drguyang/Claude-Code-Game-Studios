// ============================================================================
// 死亡过渡视觉相位机(42 侧 · 表现态;2026-11-01)
// 权威来源: design/ux/death-transition-29.md §6(Φ0–Φ4)· §9(转场规格)
//           · production/epics/death-respawn/story-006(AC-29-06/11 形状面)
// ============================================================================
// ⚠️ **纯表现态**:本机只吃 sim 相位基准(`DeathTransitionDto.Phase` ∈ {0,1,2})与
//   自身 dt,**零写路径、零 sim 状态**;视觉时长 = 表现资产参数(常量,数值轮可调),
//   不是游戏状态(呈现层不自持死亡计时器 —— 计时只推进动画,不推进任何 sim 事实)。
// ⚠️ **相位触发语义**(spec §6):Φ3「醒来」由**复活落地**(simPhase = Reviving)触发,
//   **不**由静默段计满自动触发 —— sim 迟迟不复活 ⇒ 保持躺定(Φ2)不自行醒来。
//   `LieSeconds` 是静默段的**设计时长**(AC-UX-03「静默段最短」的载体),供数值轮与夹具钉。
// ============================================================================

using UnityEngine;

namespace DaYiJingCheng.Gameplay.Presentation.DeathTransition
{
    /// <summary>视觉相位(spec §6 Φ0–Φ4;表现态枚举,不落流)。</summary>
    public enum DeathTransitionVisualPhase
    {
        Inactive = 0,   // Φ0 零叠层
        Falling = 1,    // Φ1 水墨渐入(倒下·失支撑)
        Lying = 2,      // Φ2 躺定静默(墨色满屏;设计时长 = 最短段)
        Waking = 3,     // Φ3 醒来(满屏保持,复活落格后)
        Fading = 4,     // Φ4 消散(非对称:短于渐入,不拖节奏)
    }

    /// <summary>
    /// 相位推进器(可变驱动结构;调用方持有,逐帧 <see cref="Step"/>)。
    /// <para>显示面 = <see cref="Alpha"/>(叠层不透明度 0–1);计时面 = <see cref="Elapsed"/>(段内秒)。</para>
    /// </summary>
    public struct DeathTransitionDriver
    {
        public DeathTransitionVisualPhase Phase;
        public float Alpha;
        public float Elapsed;

        public static DeathTransitionDriver Inactive => new(DeathTransitionVisualPhase.Inactive, 0f, 0f);

        public DeathTransitionDriver(DeathTransitionVisualPhase phase, float alpha, float elapsed)
        {
            Phase = phase;
            Alpha = alpha;
            Elapsed = elapsed;
        }

        /// <summary>推进一步(sim 相位基准 0/1/2 = None/Downed/Reviving;dt ≥ 0)。</summary>
        public void Step(int simPhase, float dt)
        {
            switch (Phase)
            {
                case DeathTransitionVisualPhase.Inactive:
                    if (simPhase == 1) // Downed ⇒ 开始渐入(Φ1)
                    {
                        Phase = DeathTransitionVisualPhase.Falling;
                        Elapsed = 0f;
                        Alpha = 0f;
                        Step(simPhase, dt); // 同帧消费 dt(首帧不丢)
                    }
                    return;

                case DeathTransitionVisualPhase.Falling:
                    Elapsed += dt;
                    Alpha = Mathf.Clamp01(Elapsed / FallSeconds);
                    if (Elapsed >= FallSeconds)
                    {
                        Phase = DeathTransitionVisualPhase.Lying;
                        Elapsed = 0f;
                        Alpha = 1f;
                    }
                    return;

                case DeathTransitionVisualPhase.Lying:
                    Alpha = 1f;
                    // 醒来只由复活落地触发;sim 迟迟不复活 ⇒ 保持躺定。
                    if (simPhase == 2)
                    {
                        Phase = DeathTransitionVisualPhase.Waking;
                        Elapsed = 0f;
                    }
                    return;

                case DeathTransitionVisualPhase.Waking:
                    Alpha = 1f;
                    Elapsed += dt;
                    if (Elapsed >= WakeSeconds)
                    {
                        Phase = DeathTransitionVisualPhase.Fading;
                        Elapsed = 0f;
                    }
                    return;

                case DeathTransitionVisualPhase.Fading:
                    Elapsed += dt;
                    Alpha = 1f - Mathf.Clamp01(Elapsed / FadeSeconds);
                    if (Elapsed >= FadeSeconds)
                    {
                        Phase = DeathTransitionVisualPhase.Inactive;
                        Elapsed = 0f;
                        Alpha = 0f;
                    }
                    return;
            }
        }

        /// <summary>Φ1 渐入时长(秒;数值轮可调)。</summary>
        public const float FallSeconds = 1.2f;

        /// <summary>Φ2 静默**设计时长**(秒;最短段 —— AC-UX-03;实际停留以 sim 复活为准)。</summary>
        public const float LieSeconds = 0.8f;

        /// <summary>Φ3 醒来保持时长(秒)。</summary>
        public const float WakeSeconds = 1.5f;

        /// <summary>Φ4 消散时长(秒;非对称 —— 短于 Fall,不拖节奏)。</summary>
        public const float FadeSeconds = 1.0f;
    }
}
