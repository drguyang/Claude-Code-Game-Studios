// 权威来源:design/gdd/skeuomorphic-ui.md V-10(六态机映射)
//   · production/epics/skeuomorphic-ui/story-017-enemy-vitals-bar.md
//
// 设计说明:
//   · 六态 → 读数条渲染形态的映射;态归 27,42 只画。
//   · 禁硬编码映射表进组件 — 映射是纯函数,可测。

namespace DaYiJingCheng.Gameplay.UI.Skeuomorphic
{
    /// <summary>敌人六态 → 读数条渲染形态的映射(V-10)。</summary>
    public sealed class SixStateMapping
    {
        /// <summary>把敌人态映射为读数条的 USS 形态类名(纯函数,会话内稳定)。</summary>
        public static string MapToUssClass(EnemyState state)
        {
            switch (state)
            {
                case EnemyState.Healthy:     return "vitals-healthy";
                case EnemyState.Injured:     return "vitals-injured";
                case EnemyState.Staggered:   return "vitals-staggered";
                case EnemyState.Downed:      return "vitals-downed";
                case EnemyState.Unconscious: return "vitals-unconscious";
                case EnemyState.Dead:        return "vitals-dead";
                default:                     return "vitals-healthy";
            }
        }
    }
}
