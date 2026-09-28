namespace DaYiJingCheng.Gameplay.Presentation.Skeuomorphic
{
    using System;

    /// <summary>
    /// 焦点门状态变更事件参数。
    /// </summary>
    public sealed class FocusGateStateChangedEventArgs : EventArgs
    {
        /// <summary>前一状态。</summary>
        public FocusGateState PreviousState { get; }

        /// <summary>当前状态。</summary>
        public FocusGateState CurrentState { get; }

        /// <summary>初始化事件参数。</summary>
        public FocusGateStateChangedEventArgs(FocusGateState previous, FocusGateState current)
        {
            PreviousState = previous;
            CurrentState = current;
        }
    }

    /// <summary>
    /// 焦点门状态机。
    /// <para>三态:平面独占 / 世界空间独占 / 过渡(两门皆关)。</para>
    /// <para>切换走先关后开:请求切换 → 进入 Transitioning(两门皆关) → 过渡窗口到期 → 进入目标态。</para>
    /// <para>过渡窗口内禁止重入;过渡窗口长度 0 ≤ W_trans ≤ 1 frame。</para>
    /// </summary>
    /// <remarks>
    /// 纯 C# 实现,不引用任何 Unity 引擎类型。由 <see cref="FocusNavigationBridge"/>
    /// (MonoBehaviour 实现侧)持有并驱动。
    /// </remarks>
    public sealed class FocusGateStateMachine
    {
        /// <summary>当前焦点门状态。</summary>
        public FocusGateState CurrentState { get; private set; }

        /// <summary>是否为过渡态(两门皆关窗口内)。</summary>
        public bool IsTransitioning => CurrentState == FocusGateState.Transitioning;

        /// <summary>目标状态(仅在过渡态期间有意义;非过渡态时等于 <see cref="CurrentState"/>)。</summary>
        public FocusGateState? TargetState { get; private set; }

        /// <summary>状态变更事件。</summary>
        public event EventHandler<FocusGateStateChangedEventArgs> StateChanged;

        /// <summary>上一活跃态(用于取消过渡时的保守回退)。</summary>
        private FocusGateState _previousActiveState = FocusGateState.FlatActive;

        /// <summary>初始化状态机,默认态 = 平面拟物 UI 独占。</summary>
        public FocusGateStateMachine()
        {
            CurrentState = FocusGateState.FlatActive;
            TargetState = null;
        }

        /// <summary>
        /// 请求切换到目标状态。
        /// <para>若已在目标态或正在过渡到目标态,忽略请求(幂等)。</para>
        /// <para>切换序列:当前态 → Transitioning(两门皆关) → 目标态。</para>
        /// </summary>
        /// <param name="target">目标状态(平面独占 / 世界空间独占)。过渡态不得作为目标传入。</param>
        /// <returns>若状态变更实际发生则 true;幂等忽略时 false。</returns>
        /// <exception cref="ArgumentException">target 为 Transitioning。</exception>
        public bool RequestTransition(FocusGateState target)
        {
            if (target == FocusGateState.Transitioning)
                throw new ArgumentException(
                    "[FocusGateStateMachine] Transitioning 不能作为 RequestTransition 的目标;它是内部过渡态。",
                    nameof(target));

            // 幂等:已在目标态或正在过渡到同一目标,忽略
            if (CurrentState == target)
                return false;

            if (IsTransitioning && TargetState == target)
                return false;

            _previousActiveState = CurrentState;
            FocusGateState previous = CurrentState;
            CurrentState = FocusGateState.Transitioning;
            TargetState = target;

            StateChanged?.Invoke(this, new FocusGateStateChangedEventArgs(previous, CurrentState));
            return true;
        }

        /// <summary>
        /// 完成过渡,从 Transitioning 进入目标态。
        /// <para>若当前不在过渡态,忽略(无操作)。</para>
        /// <para>过渡窗口长度由调用方控制(0 ≤ W_trans ≤ 1 frame)。</para>
        /// </summary>
        /// <returns>若完成过渡则 true;非过渡态时 false。</returns>
        public bool CompleteTransition()
        {
            if (!IsTransitioning || TargetState == null)
                return false;

            FocusGateState previous = CurrentState;
            FocusGateState target = TargetState.Value;
            CurrentState = target;
            TargetState = null;

            StateChanged?.Invoke(this, new FocusGateStateChangedEventArgs(previous, CurrentState));
            return true;
        }

        /// <summary>
        /// 取消过渡,从 Transitioning 恢复至当前活跃态(保守回退)。
        /// <para>若当前不在过渡态,无操作。</para>
        /// </summary>
        /// <returns>若取消过渡则 true;非过渡态时 false。</returns>
        public bool CancelTransition()
        {
            if (!IsTransitioning)
                return false;

            FocusGateState previous = CurrentState;
            CurrentState = _previousActiveState;
            TargetState = null;

            StateChanged?.Invoke(this, new FocusGateStateChangedEventArgs(previous, CurrentState));
            return true;
        }
    }
}
