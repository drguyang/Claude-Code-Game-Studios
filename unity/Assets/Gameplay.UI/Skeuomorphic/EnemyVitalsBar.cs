// 权威来源:design/gdd/skeuomorphic-ui.md V-10(敌人读数条 · 黄铜侧)
//   · production/epics/skeuomorphic-ui/story-017-enemy-vitals-bar.md
//
// 设计说明:
//   · 触发/状态归 27(EncounterStarted/Ended + 六态机);值经既有 VitalsDto。
//   · 42 只画材质与位置,不持 DTO 副本(AC-42-D3)。
//   · 禁传统血条形态 — 黄铜侧读数条走蚀刻刻度(AC-42-F3 黄铜分支)。

namespace DaYiJingCheng.Gameplay.UI.Skeuomorphic
{
    /// <summary>敌人读数条(V-10)—— 黄铜侧哑光面片,只渲染不持状态。</summary>
    public sealed class EnemyVitalsBar
    {
        /// <summary>是否可见(由 27 的遭遇态驱动,42 不自主决定)。</summary>
        public bool Visible { get; set; }

        /// <summary>当前六态(映射自 27 的状态机;42 只读不推进)。</summary>
        public EnemyState CurrentState { get; set; } = EnemyState.Healthy;

        /// <summary>渲染一个读数刻度(值来自 VitalsDto,读取即弃,不缓存)。</summary>
        public void RenderReading(float position)
        {
            // 读取即用 —— 不落字段,避免 AC-42-D3 的 DTO 副本
            _ = position;
        }
    }

    /// <summary>敌人六态(与 27 的状态机语义对齐;42 侧只读呈现)。</summary>
    public enum EnemyState
    {
        Healthy = 0,
        Injured = 1,
        Staggered = 2,
        Downed = 3,
        Unconscious = 4,
        Dead = 5,
    }
}
