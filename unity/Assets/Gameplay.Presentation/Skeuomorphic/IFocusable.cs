namespace DaYiJingCheng.Gameplay.Presentation.Skeuomorphic
{
    /// <summary>
    /// 可聚焦控件的最小契约。
    /// <para>住 Gameplay.Presentation 程序集 —— 避免 Gameplay.UI → Gameplay.Presentation 的循环引用。</para>
    /// <para>由焦点导航边界断言 (<see cref="FocusBoundaryAssertions"/>) 消费;</para>
    /// <para>由 <see cref="FocusNavigationBridge"/> 在焦点悬空回退时消费。</para>
    /// </summary>
    /// <remarks>
    /// 实现方:UI 控件 / 呈现层元素。
    /// <para>rank 数据的装载与校验由 <see cref="DaYiJingCheng.Gameplay.UI.Skeuomorphic.FocusBoundaryAssertions"/> 负责;</para>
    /// <para>本接口只声明 getter,不提供 setter —— rank 数据应来自控件自身的配置 / 烘焙数据。</para>
    /// </remarks>
    public interface IFocusable
    {
        /// <summary>
        /// 控件的焦点排序秩(越小越优先被聚焦)。
        /// <para>满射性:每控件必须有唯一 rank(由 AssertRankDataSurjective 校验)。</para>
        /// <para>单射性:全部控件的 rank 值必须唯一(由 AssertRankDataInjective 校验)。</para>
        /// </summary>
        int FocusRank { get; }

        /// <summary>
        /// 控件当前是否可聚焦。
        /// <para>false = 禁用 / 销毁 / 不可见 —— 焦点系统应跳过此类控件。</para>
        /// </summary>
        bool IsFocusEnabled { get; }
    }
}
