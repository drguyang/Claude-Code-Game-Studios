namespace DaYiJingCheng.Gameplay.Presentation.Skeuomorphic
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// 焦点悬空回退处理器。
    /// <para>当当前焦点控件被销毁 / 禁用时,计算最近有效控件的 rank 作为回退目标。</para>
    /// </summary>
    /// <remarks>
    /// 策略:在全部有效控件中,按 |rank_current - rank_candidate| 最小距离选取;
    /// 若全部控件均无效,返回 -1(焦点进入悬空态,由调用方处理视觉反馈)。
    /// <para>不绑定 Unity 运行时 —— 纯 C# 逻辑,可在 EditMode 测试。</para>
    /// </remarks>
    public static class FocusOrphanHandler
    {
        /// <summary>
        /// 处理焦点悬空回退。
        /// </summary>
        /// <param name="validTargets">全部有效可聚焦控件(已过滤禁用/销毁)。</param>
        /// <param name="currentRank">当前焦点 rank(可能已失效)。</param>
        /// <returns>回退 rank(>= 1 有效) 或 -1(无有效回退)。</returns>
        public static int HandleOrphan(IEnumerable<IFocusable> validTargets, int currentRank)
        {
            if (validTargets == null)
                throw new ArgumentNullException(nameof(validTargets));

            var validList = new List<IFocusable>();
            foreach (var c in validTargets)
            {
                if (c != null && c.IsFocusEnabled)
                    validList.Add(c);
            }

            if (validList.Count == 0)
                return -1;

            if (currentRank < 0)
                return validList.Min(c => c.FocusRank);

            int bestRank = -1;
            int bestDistance = int.MaxValue;

            foreach (var c in validList)
            {
                int distance = Math.Abs(c.FocusRank - currentRank);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestRank = c.FocusRank;
                }
            }

            return bestRank;
        }
    }
}
