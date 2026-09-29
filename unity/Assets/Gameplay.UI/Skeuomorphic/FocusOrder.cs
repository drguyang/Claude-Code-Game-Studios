// 权威来源:design/gdd/skeuomorphic-ui.md F3(布局可达性与焦点 rank 契约)
//   · AC-42-B1: 装载断言 — 屏定义数据声明 K 个可聚焦元素,值域恰为 1..K(满射 + 单射)
//   · production/epics/skeuomorphic-ui/story-003-focus-boundary.md
//
// 设计说明:
//   · rank 契约的数据载体 — 42 只读它,不生成语义序(语义序归各内容系统)。
//   · 满射/单射断言由 FocusBoundaryAssertions 执行,本类型只持数据。

namespace DaYiJingCheng.Gameplay.UI.Skeuomorphic
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// 焦点 rank 契约(F3)—— 屏内可聚焦元素的语义序数据。
    /// <para>AC-42-B1:值域恰为 1..K(满射 + 单射);由 <see cref="FocusBoundaryAssertions"/> 装载时校验。</para>
    /// </summary>
    public sealed class FocusOrder
    {
        private readonly int[] _ranks;

        /// <summary>创建焦点序(rank 列表须 1..K 满射 + 单射)。</summary>
        public FocusOrder(IEnumerable<int> ranks)
        {
            _ranks = ranks?.ToArray() ?? throw new System.ArgumentNullException(nameof(ranks));
        }

        /// <summary>可聚焦元素数 K。</summary>
        public int Count => _ranks.Length;

        /// <summary>全部 rank(只读)。</summary>
        public IReadOnlyList<int> Ranks => _ranks;

        /// <summary>校验值域恰为 1..K(满射 + 单射);违例返回错误清单。</summary>
        public IReadOnlyList<string> Validate()
        {
            var errors = new List<string>();
            if (_ranks.Length == 0) return errors; // K=0 合法(AC-42-B2:门不开但纸照画)

            var distinct = _ranks.Distinct().Count();
            if (distinct != _ranks.Length)
                errors.Add("[FocusOrder] rank 重复(违反单射)。");
            if (_ranks.Min() != 1 || _ranks.Max() != _ranks.Length)
                errors.Add($"[FocusOrder] 值域非 1..{_ranks.Length}(违反满射)。");
            return errors;
        }
    }
}
