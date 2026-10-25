// AC-42-F3 的「分支半边」分类器(清债轮 · TD-prescription-07 · 2026-10-25)
//
// 权威来源:design/gdd/skeuomorphic-ui.md §二 ②/③(三档材质语言:水墨侧 / 黄铜侧 / 器具)
//   · AC-025-6(形态载体须归黄铜分支)
//   · GDD :237「三档的分档断言归 AC-42-F3(『按元件所属材质分支』的既有形状,分支数 2 → 3)」
//
// ⚠️ 本件是 AC-42-F3 材质分支断言的**单一判定点** —— 此前库内无分类器,
//    story-025 夹具以自造的 ClassListContains("brass") 顶替了 AC 的宾语
//    (「AC 语义层被自造断言顶替」第 4 例,2026-10-18 落债)。本件还债:分类器落地后,
//    分支断言一律打本件,不再散落字面类名判断。
//
// ⚠️ 未知输入 fail-loud(抛形,禁静默归默认分支)—— 分支归属是 GDD 档位语义,
//    归错档 = 材质语言互相渗透(墨侧禁令被绕过的通道),宁可炸。

namespace DaYiJingCheng.Gameplay.UI.Skeuomorphic
{
    using System;
    using System.Collections.Generic;
    using UnityEngine.UIElements;

    /// <summary>拟物元件库的材质分支(AC-42-F3 分档断言三枚举;GDD §二 ②/③)。</summary>
    public enum SkeuoMaterialBranch
    {
        /// <summary>水墨侧(纸 / 卷轴 / 墨 / 印 —— 水墨材质语言全覆盖)。</summary>
        Ink = 0,

        /// <summary>黄铜侧(读数 + 控件子类;錾刻刻度合法)。</summary>
        Brass = 1,

        /// <summary>器具档(木 / 皮 / 铜;OQ-IC-4 立档)。</summary>
        Implement = 2
    }

    /// <summary>材质分支分类器 —— 元件(或其所属子树)归入三档之一。
    /// <para>判定沿**祖先链**上行:材质分支是元件子树的属性,档形态子件
    /// (<c>.brass-detent</c> 等)自身不带根材质类,由其宿主元件归属。</para></summary>
    public static class SkeuoMaterialBranchClassifier
    {
        /// <summary>根材质类 → 分支(单一出处;新档须同时扩 GDD 与本表,枚举随 AC-42-F3 扩)。</summary>
        private static readonly Dictionary<string, SkeuoMaterialBranch> RootClassToBranch =
            new Dictionary<string, SkeuoMaterialBranch>(StringComparer.Ordinal)
            {
                { "ink", SkeuoMaterialBranch.Ink },
                { "paper", SkeuoMaterialBranch.Ink },
                { "scroll", SkeuoMaterialBranch.Ink },
                { "seal", SkeuoMaterialBranch.Ink },
                { "brass", SkeuoMaterialBranch.Brass },
                { "implement", SkeuoMaterialBranch.Implement },
            };

        /// <summary>按 USS 根材质类分类;未知类 = 抛(fail-loud,禁静默归默认)。</summary>
        public static SkeuoMaterialBranch OfClass(string rootClass)
        {
            if (rootClass != null && RootClassToBranch.TryGetValue(rootClass, out SkeuoMaterialBranch branch))
                return branch;
            throw new ArgumentException(
                $"[SkeuoMaterialBranchClassifier] 「{rootClass}」不是已登记的根材质类" +
                "(ink/paper/scroll/seal/brass/implement)—— 材质分支归属不明,禁静默默认。");
        }

        /// <summary>按元件分类:沿自身 + 祖先链找第一处根材质类。
        /// <para>同一元件挂两个分支的根类 = 混材冲突,抛(分支语义不得含糊);
        /// 全链无根材质类 = 抛(未入库元件,禁静默默认)。</para></summary>
        public static SkeuoMaterialBranch Of(VisualElement element)
        {
            if (element == null) throw new ArgumentNullException(nameof(element));

            for (VisualElement node = element; node != null; node = node.parent)
            {
                SkeuoMaterialBranch? found = null;
                foreach (KeyValuePair<string, SkeuoMaterialBranch> root in RootClassToBranch)
                {
                    if (!node.ClassListContains(root.Key)) continue;
                    if (found.HasValue && found.Value != root.Value)
                    {
                        throw new InvalidOperationException(
                            "[SkeuoMaterialBranchClassifier] 元件同时携带两个材质分支的根类" +
                            "(材质语言互相渗透,AC-42-F3 分支归属不明)。");
                    }
                    found = root.Value;
                }
                if (found.HasValue) return found.Value;
            }

            throw new InvalidOperationException(
                "[SkeuoMaterialBranchClassifier] 元件及其祖先链无任何根材质类 —— 未入库元件," +
                "分支归属不明(禁静默默认)。");
        }
    }
}
