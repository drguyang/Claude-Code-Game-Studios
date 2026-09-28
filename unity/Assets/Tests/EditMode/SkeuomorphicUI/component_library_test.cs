namespace DaYiJingCheng.Tests.Unit.SkeuomorphicUI
{
    using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
    using NUnit.Framework;
    using System;

    /// <summary>
    /// Story 001: 拟物元件库基础 · 单元测试。
    /// <para>覆盖 AC-42-C1 ~ C6。</para>
    /// </summary>
    [TestFixture]
    public class component_library_test
    {
        // ── AC-42-C1: 九宫格装载断言 ──

        [Test]
        public void test_nine_slice_positive_values_pass()
        {
            var errs = NineSliceBoundsValidator.Validate(
                elementName: "paper", left: 12, right: 12, top: 12, bottom: 12,
                width: 100, height: 100);

            Assert.IsEmpty(errs);
        }

        [Test]
        public void test_nine_slice_zero_throws_error()
        {
            var errs = NineSliceBoundsValidator.Validate(
                elementName: "paper", left: 0, right: 12, top: 12, bottom: 12,
                width: 100, height: 100);

            StringAssert.Contains("C1", errs[0]);
            StringAssert.Contains("left", errs[0]);
        }

        [Test]
        public void test_nine_slice_negative_throws_error()
        {
            var errs = NineSliceBoundsValidator.Validate(
                elementName: "seal", left: 8, right: 8, top: 8, bottom: -3,
                width: 60, height: 60);

            StringAssert.Contains("C1", errs[0]);
            StringAssert.Contains("bottom", errs[0]);
        }

        [Test]
        public void test_nine_slice_at_half_size_boundary_invalid()
        {
            // slice == halfSize should be invalid (slice must be strictly less than halfSize)
            bool valid = NineSliceBoundsValidator.IsValid(slice: 50, halfSize: 50);
            Assert.IsFalse(valid);
        }

        [Test]
        public void test_nine_slice_validate_error_message_contains_element_name()
        {
            var errs = NineSliceBoundsValidator.Validate(
                elementName: "seal", left: 0, right: 8, top: 8, bottom: 8,
                width: 60, height: 60);

            StringAssert.Contains("seal", errs[0]);
            StringAssert.Contains("left", errs[0]);
        }

        // ── AC-42-C2: 主题变量引用完整 ──

        [Test]
        public void test_theme_variable_reference_validator_registers_and_queries()
        {
            ThemeVariableReferenceValidator.Register("--skeuo-paper-bg");
            ThemeVariableReferenceValidator.Register("--skeuo-ink-fg");

            Assert.IsTrue(ThemeVariableReferenceValidator.IsRegistered("--skeuo-paper-bg"));
            Assert.IsTrue(ThemeVariableReferenceValidator.IsRegistered("--skeuo-ink-fg"));
            Assert.IsFalse(ThemeVariableReferenceValidator.IsRegistered("--skeuo-nonexistent"));
        }

        [Test]
        public void test_theme_variable_reference_validator_extracts_declared_names()
        {
            string themeUss = @":root {
    --skeuo-paper-bg: #f5f0e8;
    --skeuo-ink-fg: #2c2416;
}";
            var declared = new System.Collections.Generic.List<string>();
            foreach (var name in ThemeVariableReferenceValidator.ExtractDeclaredNames(themeUss))
                declared.Add(name);

            CollectionAssert.Contains(declared, "--skeuo-paper-bg");
            CollectionAssert.Contains(declared, "--skeuo-ink-fg");
            Assert.AreEqual(2, declared.Count);
        }

        [Test]
        public void test_theme_variable_reference_validator_case_insensitive()
        {
            ThemeVariableReferenceValidator.Register("--skeuo-paper-bg");
            ThemeVariableReferenceValidator.Register("--skeuo-ink-fg");

            Assert.IsTrue(ThemeVariableReferenceValidator.IsRegistered("--SKEUO-PAPER-BG"));
            Assert.IsTrue(ThemeVariableReferenceValidator.IsRegistered("--SKEUO-INK-FG"));
        }

        [Test]
        public void test_theme_variable_reference_validator_validates_missing()
        {
            // Arrange: 登记两个变量;构造一个"引用了未登记变量"的 USS 片段
            ThemeVariableReferenceValidator.Register("--skeuo-paper-bg");
            ThemeVariableReferenceValidator.Register("--skeuo-ink-fg");

            string uss = @".paper { background-color: var(--skeuo-paper-bg); color: var(--skeuo-missing-var); }";
            var used = new System.Collections.Generic.List<string>();
            foreach (var name in ThemeVariableReferenceValidator.ExtractReferencedNames(uss))
                used.Add(name);

            // Act
            var errs = ThemeVariableReferenceValidator.ValidateReferences(used);

            // Assert: --skeuo-missing-var 未登记,应报 1 条
            Assert.AreEqual(1, errs.Count);
            StringAssert.Contains("missing-var", errs[0]);
        }

        [Test]
        public void test_theme_variable_reference_validator_empty_input_no_errors()
        {
            var errs = ThemeVariableReferenceValidator.ValidateReferences(System.Array.Empty<string>());
            Assert.IsEmpty(errs);
        }

        [SetUp]
        public void SetUp()
        {
            ThemeVariableReferenceValidator.Register("--skeuo-paper-bg");
            ThemeVariableReferenceValidator.Register("--skeuo-ink-fg");
            SkeuoComponentRegistry.InitializeDefaults();
        }

        [Test]
        public void test_component_registry_quota_defaults_pass()
        {
            var errs = SkeuoComponentRegistry.ValidateQuotas();
            Assert.IsEmpty(errs);
        }

        [Test]
        public void test_component_registry_initialize_defaults_idempotent_after_lock()
        {
            SkeuoComponentRegistry.InitializeDefaults();
            SkeuoComponentRegistry.InitializeDefaults();

            Assert.AreEqual(4, SkeuoComponentRegistry.All.Count);
        }

        [Test]
        public void test_component_registry_register_after_lock_throws()
        {
            SkeuoComponentRegistry.InitializeDefaults();
            SkeuoComponentRegistry.Lock();

            Assert.Throws<InvalidOperationException>(() =>
                SkeuoComponentRegistry.Register(
                    SkeuoElement.Paper, "paper-test", 1));
        }

        [Test]
        public void test_component_registry_max_components_limit()
        {
            Assert.AreEqual(16, SkeuoComponentRegistry.MaxRegisteredComponents);
        }

        [Test]
        public void test_component_registry_max_variants_limit()
        {
            Assert.AreEqual(4, SkeuoComponentRegistry.MaxVariantsPerComponent);
        }

        // ── AC-42-C4/C5: 元件工厂 + 变体 ──

        [Test]
        public void test_element_library_create_returns_element_with_correct_class()
        {
            var lib = new SkeuoElementLibrary();
            var paper = lib.Create(SkeuoElement.Paper);
            Assert.IsTrue(paper.ClassListContains("paper"));
        }

        [Test]
        public void test_element_library_create_all_four_kinds()
        {
            var lib = new SkeuoElementLibrary();
            Assert.IsTrue(lib.Create(SkeuoElement.Paper).ClassListContains("paper"));
            Assert.IsTrue(lib.Create(SkeuoElement.Scroll).ClassListContains("scroll"));
            Assert.IsTrue(lib.Create(SkeuoElement.Ink).ClassListContains("ink"));
            Assert.IsTrue(lib.Create(SkeuoElement.Seal).ClassListContains("seal"));
        }

        [Test]
        public void test_element_library_invalid_kind_throws()
        {
            var lib = new SkeuoElementLibrary();
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                lib.Create((SkeuoElement)999));
        }

        [Test]
        public void test_element_library_create_with_variant_adds_suffix_class()
        {
            SkeuoComponentRegistry.InitializeDefaults();
            var lib = new SkeuoElementLibrary();
            var aged = lib.CreateWithVariant(SkeuoElement.Paper, variantIndex: 1);
            Assert.IsTrue(aged.ClassListContains("paper-aged"));
        }

        [Test]
        public void test_element_library_variant_index_zero_no_suffix()
        {
            SkeuoComponentRegistry.InitializeDefaults();
            var lib = new SkeuoElementLibrary();
            var paper = lib.CreateWithVariant(SkeuoElement.Paper, variantIndex: 0);
            Assert.IsTrue(paper.ClassListContains("paper"));
            Assert.IsFalse(paper.ClassListContains("paper-aged"));
        }

        [Test]
        public void test_element_library_get_uss_class_name()
        {
            Assert.AreEqual("paper", SkeuoElementLibrary.GetUssClassName(SkeuoElement.Paper));
            Assert.AreEqual("scroll", SkeuoElementLibrary.GetUssClassName(SkeuoElement.Scroll));
            Assert.AreEqual("ink", SkeuoElementLibrary.GetUssClassName(SkeuoElement.Ink));
            Assert.AreEqual("seal", SkeuoElementLibrary.GetUssClassName(SkeuoElement.Seal));
        }

        [Test]
        public void test_element_library_create_with_variant_quota_mismatch_throws()
        {
            // Paper has 1 registered variant (variantIndex valid: 0 or 1)
            // variantIndex=2 is out of range and must throw
            SkeuoComponentRegistry.InitializeDefaults();
            var lib = new SkeuoElementLibrary();

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                lib.CreateWithVariant(SkeuoElement.Paper, variantIndex: 2));
        }

        [Test]
        public void test_element_library_get_uss_class_name_invalid_kind_throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SkeuoElementLibrary.GetUssClassName((SkeuoElement)999));
        }

        // ── AC-42-C5: USS 硬编码字号 / 文本 lint ──

        [Test]
        public void test_hardcoded_font_size_regex_detects_violation()
        {
            string ussLine = "    font-size: 14px;";
            bool matches = System.Text.RegularExpressions.Regex.IsMatch(
                ussLine, @"font-size\s*:\s*\d+px",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            Assert.IsTrue(matches);
        }

        [Test]
        public void test_hardcoded_text_regex_detects_violation()
        {
            string ussLine = "content: \"保存\";";
            bool matches = System.Text.RegularExpressions.Regex.IsMatch(
                ussLine, @"(content|text)\s*:\s*[""'][^""']+[""']",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            Assert.IsTrue(matches);
        }

        [Test]
        public void test_theme_variable_reference_not_flagged_as_hardcoded()
        {
            string ussLine = "    font-size: var(--skeuo-font-size-md);";
            bool matches = System.Text.RegularExpressions.Regex.IsMatch(
                ussLine, @"font-size\s*:\s*\d+px",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            Assert.IsFalse(matches);
        }

        // ── AC-42-C6: fallback 字体位 ──

        [Test]
        public void test_fallback_font_registry_chinese_slot_exists()
        {
            string font = FallbackFontRegistry.Get("chinese");
            Assert.IsFalse(string.IsNullOrEmpty(font));
            StringAssert.Contains("Noto", font);
        }

        [Test]
        public void test_fallback_font_registry_english_slot_exists()
        {
            string font = FallbackFontRegistry.Get("english");
            Assert.IsFalse(string.IsNullOrEmpty(font));
        }

        [Test]
        public void test_fallback_font_registry_missing_slot_throws()
        {
            System.Collections.Generic.KeyNotFoundException ex2 = Assert.Throws<System.Collections.Generic.KeyNotFoundException>(() =>
                FallbackFontRegistry.Get("nonexistent-slot"));
        }

        [Test]
        public void test_fallback_font_registry_validate_fallbacks_no_errors()
        {
            var errs = FallbackFontRegistry.ValidateFallbacks();
            Assert.IsEmpty(errs);
        }

        // ── 综合联动 ──

        [Test]
        public void test_default_registry_has_all_four_elements()
        {
            SkeuoComponentRegistry.InitializeDefaults();
            Assert.IsTrue(SkeuoComponentRegistry.All.ContainsKey(SkeuoElement.Paper));
            Assert.IsTrue(SkeuoComponentRegistry.All.ContainsKey(SkeuoElement.Scroll));
            Assert.IsTrue(SkeuoComponentRegistry.All.ContainsKey(SkeuoElement.Ink));
            Assert.IsTrue(SkeuoComponentRegistry.All.ContainsKey(SkeuoElement.Seal));
        }

        [Test]
        public void test_default_registry_paper_has_aged_variant()
        {
            SkeuoComponentRegistry.InitializeDefaults();
            var paperReg = SkeuoComponentRegistry.All[SkeuoElement.Paper];
            Assert.AreEqual("paper", paperReg.UssClassName);
            Assert.AreEqual(2, paperReg.AllocatedVariantSlots);
            Assert.AreEqual(1, paperReg.VariantCount);
            Assert.AreEqual("-aged", paperReg.VariantSuffix);
        }

        [Test]
        public void test_default_registry_scroll_has_no_variant()
        {
            SkeuoComponentRegistry.InitializeDefaults();
            var scrollReg = SkeuoComponentRegistry.All[SkeuoElement.Scroll];
            Assert.AreEqual(1, scrollReg.AllocatedVariantSlots);
            Assert.AreEqual(0, scrollReg.VariantCount);
        }
    }
}
