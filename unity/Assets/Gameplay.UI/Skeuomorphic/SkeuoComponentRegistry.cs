namespace DaYiJingCheng.Gameplay.UI.Skeuomorphic
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

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
        /// <param name="isTextureContainer">是否为**贴图容器**(AC-42-C7 判据面)。默认 false —— 见
        /// <see cref="ComponentRegistration.IsTextureContainer"/>。</param>
        public static void Register(SkeuoElement kind, string ussClassName, int allocatedVariantSlots = 1,
            string variantSuffix = "", bool isTextureContainer = false)
        {
            if (_locked) throw new InvalidOperationException("[SkeuoComponentRegistry] 注册表已锁定,不能再注册。");
            if (_registry.Count >= MaxRegisteredComponents)
                throw new InvalidOperationException(
                    $"[SkeuoComponentRegistry] 元件总数 {_registry.Count} 已到配额上限 {MaxRegisteredComponents}。");
            if (_registry.ContainsKey(kind))
                throw new InvalidOperationException(
                    $"[SkeuoComponentRegistry] 元件「{kind}」重复注册。");

            var reg = new ComponentRegistration(kind, ussClassName, Math.Max(1, allocatedVariantSlots),
                variantSuffix, isTextureContainer);
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

        /// <summary>重置注册表为空闲状态(仅测试用;运行期不调用)。</summary>
        public static void Reset()
        {
            _locked = false;
            _registry.Clear();
        }

        /// <summary>获取元件注册项(只读)。</summary>
        public static IReadOnlyDictionary<SkeuoElement, ComponentRegistration> All => _registry;

        /// <summary>**贴图容器类名集**(`IsTextureContainer == true` 者)—— AC-42-C7 的判据面。
        /// <para>⚠️ **2026-10-05 用户裁定**:C7 判据由「全部已注册类」**收窄**为本集。
        /// 与 `All` 的类名集差集 = `ink-faded` / `seal-small`(仅字色 / 字号,无贴图)。</para>
        /// <para>⚠️ **变体遵循基类之外单列**:基类 `ink` 为 true,其变体 `ink-faded` 仍为 false
        /// (用户裁:仅基类改判,变体保持 —— 否则造出「`ink-faded` 容器无图」的新红)。</para></summary>
        public static IEnumerable<string> TextureContainerClassNames
            => _registry.Values.Where(v => v.IsTextureContainer).Select(v => v.UssClassName);

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

            // ⚠️ 2026-10-05 用户裁定:isTextureContainer 逐类判定(见 art-assets-required-for-019 §八·二)。
            //    paper / scroll / ink / seal 四基类 = true;变体 ink-faded / seal-small = false
            //    (裁:仅基类改判,变体保持)。
            // ⚠️ **`.paper-aged` 是 `.paper` 的变体类,不是独立注册项** —— 4 值枚举(`SkeuoElement`)
            //    不含 PaperAged,且其图 `paper_aged-final.png` 走 `paper` 类的变体槽(同 `-aged` 后缀)。
            Register(SkeuoElement.Paper, "paper", 2, "-aged", isTextureContainer: true);
            RegisterVariant(SkeuoElement.Paper, "-aged");

            Register(SkeuoElement.Scroll, "scroll", 1, "", isTextureContainer: true);

            Register(SkeuoElement.Ink, "ink", 2, "-faded", isTextureContainer: true);
            RegisterVariant(SkeuoElement.Ink, "-faded");

            Register(SkeuoElement.Seal, "seal", 2, "-small", isTextureContainer: true);
            RegisterVariant(SkeuoElement.Seal, "-small");

            Lock();
        }
    }
}
