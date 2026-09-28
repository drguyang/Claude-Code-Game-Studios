// Story 001: 拟物元件库基础 · 工厂实现(ISkeuoComponentLibrary)
//
// 权威来源:
//   · production/epics/skeuomorphic-ui/story-001-component-library.md
//   · ADR-013 §四 Key Interfaces
//
// 设计说明:
//   · 运行期工厂只负责把元件种类映射到 USS class name 并创建 VisualElement。
//   · 九宫格值绑定由美术 / 关卡工具在素材层面提供;Unity 6.3 运行时 -unity-slice-*
//     Play Mode 行为未经验证,此处仅绑定 USS class,不硬编码 slice 数值。
//   · 变体后缀由 SkeuoComponentRegistry 单一出处供给(registry-driven),不维护第二张表。

namespace DaYiJingCheng.Gameplay.UI.Skeuomorphic
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.UIElements;

    /// <summary>拟物元件库工厂实现(SkeuoElement → USS class + VisualElement)。</summary>
    public sealed class SkeuoElementLibrary : ISkeuoComponentLibrary
    {
        /// <summary>USS class 映射表(与 USS 文件内类名一一对应)。</summary>
        private static readonly string[] UssClassByKind = new string[]
        {
            "paper",   // Paper = 0
            "scroll",  // Scroll = 1
            "ink",     // Ink = 2
            "seal"     // Seal = 3
        };

        /// <inheritdoc/>
        public VisualElement Create(SkeuoElement kind)
        {
            int kindIndex = (int)kind;
            if (kindIndex < 0 || kindIndex >= UssClassByKind.Length)
                throw new ArgumentOutOfRangeException(
                    nameof(kind), kind,
                    $"[SkeuoElementLibrary] 元件种类「{kind}」超出注册范围。");

            string baseClass = UssClassByKind[kindIndex];
            if (string.IsNullOrEmpty(baseClass))
                throw new InvalidOperationException(
                    $"[SkeuoElementLibrary] 元件种类「{kind}」的 USS class 未登记。");

            VisualElement element = new VisualElement();
            element.AddToClassList(baseClass);
            return element;
        }

        /// <summary>
        /// 创建指定变体的元件(变体由 USS 类名后缀实现)。
        /// 变体后缀从 SkeuoComponentRegistry 读取,registry 是单一出处。
        /// </summary>
        public VisualElement CreateWithVariant(SkeuoElement kind, int variantIndex)
        {
            VisualElement element = Create(kind);
            string baseClass = UssClassByKind[(int)kind];

            if (!SkeuoComponentRegistry.All.TryGetValue(kind, out var reg))
                throw new KeyNotFoundException($"[SkeuoElementLibrary] 元件「{kind}」未注册。");

            if (variantIndex < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(variantIndex), variantIndex,
                    $"[SkeuoElementLibrary] 变体索引不能为负。");

            if (variantIndex > 0)
            {
                if (variantIndex > reg.VariantCount)
                    throw new ArgumentOutOfRangeException(
                        nameof(variantIndex), variantIndex,
                        $"[SkeuoElementLibrary] 元件「{kind}」变体索引 {variantIndex} 超出已登记变体数 {reg.VariantCount}。");
                if (!string.IsNullOrEmpty(reg.VariantSuffix))
                {
                    element.AddToClassList(baseClass + reg.VariantSuffix);
                }
            }

            return element;
        }

        /// <summary>获取元件的基础 USS class 名(只读,用于校验 / 序列化)。</summary>
        public static string GetUssClassName(SkeuoElement kind)
        {
            int kindIndex = (int)kind;
            if (kindIndex < 0 || kindIndex >= UssClassByKind.Length)
                throw new ArgumentOutOfRangeException(
                    nameof(kind), kind,
                    $"[SkeuoElementLibrary] 元件种类「{kind}」超出注册范围。");
            return UssClassByKind[kindIndex];
        }
    }
}
