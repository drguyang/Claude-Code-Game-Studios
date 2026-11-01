// ============================================================================
// 死亡/复活过渡 DTO(S23 · story-006 · 2026-11-01)
// 权威来源:
//   · design/ux/death-transition-29.md §10(数据要求 —— 全整数、零 float、零代价数值)
//   · production/epics/death-respawn/story-006-presentation-whitelist.md(AC-29-07)
//   · ADR-013 §9 C3(呈现只读 DTO;PresentationDtoGuard 递归扫描覆盖)
// ============================================================================
// ⚠️ **本 DTO 是「呈现三件套同构纪律」的只读投影面**:只渲染,永不持有游戏状态。
//   全部字段 **int**;**零 float**、**零代价数值**(掉落数/掉级数/死亡次数不得出现 ——
//   代价要玩家自己发现,`death-and-respawn.md:258`);受 `PresentationDtoGuard.Scan`
//   递归扫描(与 VitalsDto 同格,AC-29-07)。
// ⚠️ **Phase 是 sim 事实的投影基准,不是动画相位**:呈现层的 Φ1–Φ4 视觉相位
//   (spec §6)由本 DTO + 表现态动画时长推导;动画时长**不在本 DTO**(那是表现资产
//   参数,不是游戏状态 —— 呈现层不自持死亡计时器)。
// ============================================================================

namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary>
    /// 死亡/复活过渡的只读投影 DTO(全整数;零代价数值)。
    /// <para>由 <c>DeathTransitionQuery.Project</c>(Sim 侧纯函数)从三流投影;
    /// 呈现层只读,重开呈现层不改变任何 sim 事实(零写路径)。</para>
    /// </summary>
    public readonly struct DeathTransitionDto
    {
        /// <summary>相位基准:无死亡事实(零叠层,spec Φ0)。</summary>
        public const int PhaseNone = 0;

        /// <summary>相位基准:该 actor 的 `PlayerDied` 真写且此后无其 `ActorCellEntered`
        /// (倒下状态,spec Φ1/Φ2 的 sim 面)。</summary>
        public const int PhaseDowned = 1;

        /// <summary>相位基准:死亡之后已有该 actor 的 `ActorCellEntered`
        /// (复活落格已发生,spec Φ3「醒来」的 sim 面)。</summary>
        public const int PhaseReviving = 2;

        /// <summary>投影相位(取值 ∈ {0, 1, 2},见上三常量)。</summary>
        public readonly int Phase;

        /// <summary>本地性:1 = 本地玩家(全屏过渡),0 = 远端(局部水墨标识,第二 QoS 面)。
        /// 由装配层传入(本地性不是流事实,是设备事实)。</summary>
        public readonly int IsLocal;

        /// <summary>构造只读投影。</summary>
        public DeathTransitionDto(int phase, int isLocal)
        {
            Phase = phase;
            IsLocal = isLocal;
        }
    }
}
