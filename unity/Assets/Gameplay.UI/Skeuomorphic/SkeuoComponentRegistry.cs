namespace DaYiJingCheng.Gameplay.UI.Skeuomorphic
{
    using System;
    using System.Collections.Generic;

    /// <summary>拟物元件库注册表:负责元件注册、变体配额门控与 atlas 配额断言。</summary>
    public static class SkeuoComponentRegistry
    {
        // AC-42-C3 图集配额(用户可调;触发行为 = 构建期构建失败,此处为运行期门控镜像)
        public const int MaxRegisteredComponents = 16;
        public const int MaxVariantsPerComponent = 4;

        private static readonly Dictionary<SkeuoElement, ComponentRegistration> _registry
            = new Dictionary<SkeuoElement, ComponentRegistration>();

        private static bool _locked;

        /// <summary>注册一个元件种类(构建期完成,运行期只读)。</summary>
        public static void Register(SkeuoElement kind, string ussClassName, int allocatedVariantSlots = 1, string variantSuffix = "")
        {
            if (_locked) throw new InvalidOperationException("[SkeuoComponentRegistry] 注册表已锁定,不能再注册。");
            if (_registry.Count >= MaxRegisteredComponents)
                throw new InvalidOperationException(
                    $"[SkeuoComponentRegistry] 元件总数 {_registry.Count} 已到配额上限 {MaxRegisteredComponents}。");
            if (_registry.ContainsKey(kind))
                throw new InvalidOperationException(
                    $"[SkeuoComponentRegistry] 元件「{kind}」重复注册。");

            var reg = new ComponentRegistration(kind, ussClassName, Math.Max(1, allocatedVariantSlots), variantSuffix);
            _registry[kind] = reg;
        }

        /// <summary>注册一个变体(须先 Register 再 RegisterVariant)。</summary>
        public static void RegisterVariant(SkeuoElement kind, string variantSuffix = "")
        {
            if (!_registry.TryGetValue(kind, out var reg))
                throw new KeyNotFoundException($"[SkeuoComponentRegistry] 元件「{kind}」未注册。");
            if (!string.IsNullOrEmpty(variantSuffix))
            {
                reg.VariantSuffix = variantSuffix;
            }
            reg.RegisterVariant();
        }

        /// <summary>完成注册阶段,进入只读锁定状态。</summary>
        public static void Lock()
        {
            _locked = true;
        }

        /// <summary>获取元件注册项(只读)。</summary>
        public static IReadOnlyDictionary<SkeuoElement, ComponentRegistration> All => _registry;

        /// <summary>构建期配额断言:全部注册项合法则返回空列表。</summary>
        public static IReadOnlyList<string> ValidateQuotas()
        {
            var errs = new List<string>();
            if (_registry.Count > MaxRegisteredComponents)
                errs.Add($"[C3] 元件总数 {_registry.Count} 超过上限 {MaxRegisteredComponents}。");
            foreach (var kv in _registry)
            {
                if (kv.Value.VariantCount > kv.Value.AllocatedVariantSlots)
                    errs.Add($"[C3] 元件「{kv.Key}」实际变体数 {kv.Value.VariantCount}" +
                             $" 超过登记配额 {kv.Value.AllocatedVariantSlots}。");
                if (kv.Value.AllocatedVariantSlots > MaxVariantsPerComponent)
                    errs.Add($"[C3] 元件「{kv.Key}」登记配额 {kv.Value.AllocatedVariantSlots}" +
                             $" 超过单件上限 {MaxVariantsPerComponent}。");
            }
            return errs;
        }

        /// <summary>注册 Story 001 默认元件(构建期调用一次)。</summary>
        public static void InitializeDefaults()
        {
            if (_locked) return;

            Register(SkeuoElement.Paper, "paper", 2, "-aged");
            Register(SkeuoElement.Scroll, "scroll", 1);
            Register(SkeuoElement.Ink, "ink", 3, "-faded");
            Register(SkeuoElement.Seal, "seal", 2, "-small");

            Lock();
        }
    }
}
