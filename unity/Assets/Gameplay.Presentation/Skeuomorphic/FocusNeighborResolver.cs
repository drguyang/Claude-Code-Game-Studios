// 权威来源:ADR-013 §五 Focus Navigation Presentation Side + AC-42-B4
//   · production/epics/skeuomorphic-ui/story-002-focus-gate-and-bridge.md
//
// 设计说明:
//   · 降级路径的自实现焦点算法:邻居查找 + 方向投影。
//   · AC-42-B4 铁律:邻居枚举 / 方向投影 / 几何查询类必须标记为 internal/private,
//     不得从 42 的焦点契约导出面(IFocusNavigationPresenter 及所有 public/internal 类型)导出。
//     焦点移动不能被 42 以外的任何系统计算。
//   · 本类 = internal,只在 Gameplay.Presentation 程序集内可见。
//   · 完整降级实现须另开 ADR 登记(见 FocusNavigationBridge.ActivateDowngrade 的注释)。

namespace DaYiJingCheng.Gameplay.Presentation.Skeuomorphic
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// 焦点邻居查找与方向投影(降级路径 · 内部实现)。
    /// <para>AC-42-B4:本类型为 internal —— 焦点移动的计算不得被 42 以外的系统调用。</para>
    /// </summary>
    internal sealed class FocusNeighborResolver
    {
        private readonly IEnumerable<IFocusable> _controls;
        private int _currentRank;

        /// <summary>创建邻居查找器。</summary>
        /// <param name="controls">全部可聚焦控件(rank 数据完整性由 FocusBoundaryAssertions 装载时校验)。</param>
        /// <param name="currentRank">当前焦点 rank(-1 = 尚未设定)。</param>
        internal FocusNeighborResolver(IEnumerable<IFocusable> controls, int currentRank)
        {
            _controls = controls ?? throw new System.ArgumentNullException(nameof(controls));
            _currentRank = currentRank;
        }

        /// <summary>当前焦点 rank。</summary>
        internal int CurrentRank => _currentRank;

        /// <summary>
        /// 方向投影:查找指定方向上的下一个可聚焦控件。
        /// <para>降级路径仅在引擎焦点质量不达预期时启用;语义 = 按 rank 序的方向邻居。</para>
        /// </summary>
        /// <param name="direction">导航方向。</param>
        /// <returns>目标控件的 rank;无有效邻居返回 -1。</returns>
        internal int Resolve(NavigationDirection direction)
        {
            // 过滤出有效控件
            var valid = _controls
                .Where(c => c != null && c.IsFocusEnabled)
                .ToList();

            if (valid.Count == 0)
                return -1;

            // 当前无焦点 → 取首个有效控件
            if (_currentRank < 0)
                return valid[0].FocusRank;

            switch (direction)
            {
                case NavigationDirection.Next:
                case NavigationDirection.Down:
                case NavigationDirection.Right:
                    // 找 rank 大于当前的最小有效 rank(回绕到首个)
                    var next = valid
                        .Where(c => c.FocusRank > _currentRank)
                        .OrderBy(c => c.FocusRank)
                        .FirstOrDefault();
                    return next?.FocusRank ?? valid[0].FocusRank;

                case NavigationDirection.Previous:
                case NavigationDirection.Up:
                case NavigationDirection.Left:
                    // 找 rank 小于当前的最大有效 rank(回绕到末个)
                    var prev = valid
                        .Where(c => c.FocusRank < _currentRank)
                        .OrderByDescending(c => c.FocusRank)
                        .FirstOrDefault();
                    return prev?.FocusRank ?? valid[valid.Count - 1].FocusRank;

                default:
                    return -1;
            }
        }

        /// <summary>
        /// 更新当前焦点 rank(焦点移动后调用)。
        /// </summary>
        internal void SetCurrentRank(int rank)
        {
            _currentRank = rank;
        }
    }
}
