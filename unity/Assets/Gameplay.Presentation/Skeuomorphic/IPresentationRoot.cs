namespace DaYiJingCheng.Gameplay.Presentation.Skeuomorphic
{
    /// <summary>
    /// 呈现层根接口——两栈(UI Toolkit 平面拟物 / UGUI world-space)共用契约。
    /// <para>铁律:42 只渲染,永不持有游戏状态(systems-index §9 C3)。</para>
    /// </summary>
    public interface IPresentationRoot
    {
        /// <summary>
        /// 焦点单栈门开关。
        /// <para>active = true:当前栈接收焦点导航意图流;</para>
        /// <para>active = false:当前栈不接收焦点导航(过渡态 / 另一栈活跃)。</para>
        /// </summary>
        /// <remarks>
        /// 两栈的 IPresentationRoot 实现均经同一入口,无旁路开关。
        /// 同一时刻仅一栈的 active = true(焦点单栈门)。
        /// </remarks>
        void SetFocusGate(bool active);

        /// <summary>
        /// 绑定只读 DTO 源。
        /// <para>呈现层通过此接口读取 DTO,永不缓存副本(caching = 第二份真相)。</para>
        /// </summary>
        void Bind(DaYiJingCheng.Sim.Contracts.IDtoSource source);
    }
}
