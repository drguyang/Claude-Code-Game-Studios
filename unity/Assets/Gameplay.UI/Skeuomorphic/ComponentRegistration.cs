namespace DaYiJingCheng.Gameplay.UI.Skeuomorphic
{
    using System;
    using System.Collections.Generic;

    /// <summary>单条元件注册项(含变体配额)。</summary>
    public sealed class ComponentRegistration
    {
        public SkeuoElement Kind { get; }
        public string UssClassName { get; }
        public int VariantCount { get; private set; }
        public int AllocatedVariantSlots { get; }
        public string VariantSuffix { get; set; }

        /// <summary>
        /// 本类是否为**贴图容器**(AC-42-C7 判据面)。
        /// <para>⚠️ **2026-10-05 用户裁定新增** —— 取代原「已注册类全须有 `background-image`」判据。
        /// 该旧判据**不自洽**:`.ink` 整类无 `background-color`(纯字色 + `text-shadow`,
        /// 承 `art-bible §7.2`「字号差异走 USS 变量」)· `.seal-small` 仅 `font-size`
        /// ⇒ 「已注册」与「贴图容器」须**解耦**。</para>
        /// <para>**默认 false**(须显式传 true)—— 防新增类被静默拉进 C7 面。</para>
        /// </summary>
        public bool IsTextureContainer { get; }

        public ComponentRegistration(SkeuoElement kind, string ussClassName, int allocatedVariantSlots,
            string variantSuffix = "", bool isTextureContainer = false)
        {
            Kind = kind;
            UssClassName = ussClassName ?? throw new ArgumentNullException(nameof(ussClassName));
            AllocatedVariantSlots = allocatedVariantSlots;
            VariantSuffix = variantSuffix ?? string.Empty;
            IsTextureContainer = isTextureContainer;
        }

        public void RegisterVariant()
        {
            if (VariantCount >= AllocatedVariantSlots)
                throw new InvalidOperationException(
                    $"[SkeuoComponentRegistry] 元件「{UssClassName}」变体数 {VariantCount} 已到配额上限 {AllocatedVariantSlots}。");
            VariantCount++;
        }
    }
}
