// 权威来源:design/gdd/skeuomorphic-ui.md §Visual 二 记号登记表
//   · 每个形状只有一个拥有者、一个语义;新增语义须新增形状,不得借用车已有的勾。
//   · production/epics/skeuomorphic-ui/story-008-no-healthbar-feedback.md
//
// 设计说明:
//   · 六种记号:勾(完成) / 点(陈旧) / 叠角(加工态) / 划改痕(改写留痕) / 印(计入·卸出) / 折角(不可写·已合上)。
//   · 形状族由 42 的主题变量提供;语义与时机归登记表所列拥有者 —— 42 只画。

namespace DaYiJingCheng.Gameplay.UI.Skeuomorphic
{
    /// <summary>记号形状(六种;每形状一语义,不复用)。</summary>
    public enum MarkShape
    {
        Check = 0,       // 勾(单笔) = 完成
        Dot = 1,         // 点(单点) = 陈旧
        FoldedCorner = 2,// 叠角(双线角) = 加工态
        StrikeThrough = 3,// 划改痕 = 改写留痕
        Seal = 4,        // 印(单方印) = 计入/卸出
        Fold = 5,        // 折角 = 不可写/已合上 Locked
    }

    /// <summary>记号登记表(形状族 USS 类名的单一出处)。</summary>
    public static class MarkRegistry
    {
        private static readonly string[] UssByShape =
        {
            "mark-check", "mark-dot", "mark-fold-double",
            "mark-strike", "mark-seal", "mark-fold-single",
        };

        /// <summary>登记的形状数(恒 6)。</summary>
        public const int ShapeCount = 6;

        /// <summary>取记号形状的 USS 类名(只读)。</summary>
        public static string GetUssClass(MarkShape shape)
        {
            int i = (int)shape;
            if (i < 0 || i >= UssByShape.Length)
                throw new System.ArgumentOutOfRangeException(nameof(shape), shape,
                    "[MarkRegistry] 记号形状超出登记范围。");
            return UssByShape[i];
        }
    }
}
