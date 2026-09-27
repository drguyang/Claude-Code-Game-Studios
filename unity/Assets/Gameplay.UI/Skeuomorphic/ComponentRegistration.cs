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

        public ComponentRegistration(SkeuoElement kind, string ussClassName, int allocatedVariantSlots, string variantSuffix = "")
        {
            Kind = kind;
            UssClassName = ussClassName ?? throw new ArgumentNullException(nameof(ussClassName));
            AllocatedVariantSlots = allocatedVariantSlots;
            VariantSuffix = variantSuffix ?? string.Empty;
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
