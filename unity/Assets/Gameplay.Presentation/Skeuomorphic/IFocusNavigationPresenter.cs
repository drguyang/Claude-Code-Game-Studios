namespace DaYiJingCheng.Gameplay.Presentation.Skeuomorphic
{
    /// <summary>
    /// 焦点导航呈现契约。
    /// <para>由 42(拟物 UI 框架)实现,供外部系统(如 4 交互意图)查询焦点导航状态。</para>
    /// </summary>
    /// <remarks>
    /// 导出面仅包含 <see cref="IsFocusActive"/> 一个属性。
    /// 不导出「邻居枚举 / 方向投影 / 几何查询」类入口(AC-42-B4)。
    /// 焦点移动由 Unity UI Toolkit 的 FocusController 自动完成(官方桥 NavigationMoveEvent);
    /// 本接口不重复实现焦点算法。
    /// 若 spike 判定引擎焦点质量不达预期,降级实现自实现焦点算法——接口签名不变,
    /// 代码住在 Gameplay.UI 程序集内,不新开程序集。
    /// </remarks>
    public interface IFocusNavigationPresenter
    {
        /// <summary>焦点导航当前是否处于活跃状态(有焦点可导航的界面摊开)。</summary>
        bool IsFocusActive { get; }
    }
}
