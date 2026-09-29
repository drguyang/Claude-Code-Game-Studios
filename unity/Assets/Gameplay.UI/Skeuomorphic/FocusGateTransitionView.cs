// 权威来源:design/gdd/skeuomorphic-ui.md 规则三(焦点单栈门) + F8 不变量
//   · 规则十钩子③:焦点可见类在每次焦点转移施加/移除;门关时不施加。
//   · production/epics/skeuomorphic-ui/story-010-focus-gate-transition.md
//
// 设计说明:
//   · UI 侧的门状态视图 — 只读状态机结论,驱动焦点类施加/移除。
//   · F8:任何采样点不得两门皆开(由 FocusGateStateMachine 保证,本视图只消费)。

namespace DaYiJingCheng.Gameplay.UI.Skeuomorphic
{
    using DaYiJingCheng.Gameplay.Presentation.Skeuomorphic;

    /// <summary>焦点门过渡的 UI 侧视图(F8)—— 消费状态机结论,不做状态推断。</summary>
    public sealed class FocusGateTransitionView
    {
        /// <summary>当前门态(只读镜像)。</summary>
        public FocusGateState CurrentState { get; private set; } = FocusGateState.FlatActive;

        /// <summary>是否处于过渡窗口(两门皆关 ⇒ 焦点类不施加)。</summary>
        public bool InTransition => CurrentState == FocusGateState.Transitioning;

        /// <summary>由状态机事件驱动(只读镜像,不自行切换)。</summary>
        public void OnStateChanged(FocusGateState newState)
        {
            CurrentState = newState;
        }

        /// <summary>当前是否应施加焦点可见类(过渡中 = 不施加,无残留)。</summary>
        public bool ShouldApplyFocusVisible => !InTransition;
    }
}
