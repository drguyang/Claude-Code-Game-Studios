namespace DaYiJingCheng.Gameplay.Presentation.Skeuomorphic
{
    /// <summary>焦点门三态枚举。</summary>
    /// <remarks>
    /// 三态覆盖全部合法配置:
    /// <list type="bullet">
    ///   <item><description><see cref="FlatActive"/> — 平面拟物 UI(UI Toolkit)独占焦点导航。</description></item>
    ///   <item><description><see cref="WorldActive"/> — 世界空间 UI(UGUI world canvas)独占焦点导航。</description></item>
    ///   <item><description><see cref="Transitioning"/> — 过渡态:两门皆关,防止过渡窗口内重入。</description></item>
    /// </list>
    /// 不存在「两门皆开」的合法状态;任何切换必须经过 <see cref="Transitioning"/>。
    /// </remarks>
    public enum FocusGateState : byte
    {
        /// <summary>平面拟物 UI(UI Toolkit)独占焦点导航。</summary>
        FlatActive = 0,

        /// <summary>世界空间 UI(UGUI world canvas)独占焦点导航。</summary>
        WorldActive = 1,

        /// <summary>过渡态:两门皆关,防止过渡窗口内重入。</summary>
        Transitioning = 2
    }
}
