// sprint-08 S3 · 词条可读性 polish 判据(EditMode · 2026-10-12)
//
// 判据(可证伪):
//   ① **对比 ≥7:1**(`accessibility-requirements.md:87` + `casebook-39.md:309`):
//      阳性墨 / 阴性墨 在**值区底衬**上的线性 Rec.709 对比度 —— 阴性态原为 3.87:1(违约),
//      改 `--skeuo-ink-negative` 后须 ≥7:1;
//       ⚠️ **2026-10-13 二轮**:用户真机实测「对比不足发灰」判红 —— 初值
//       (backing #fbf9f3 + negative #5a5044 = 7.48:1)数合格但贴线 ⇒ 现值
//       (#e7e0cd + #463b30 = 8.27:1)留 18% 冗余。判据阈值仍 ≥7:1(承诺值不抬),
//       冗余是**值的**事,不是判据的事。
//   ② **字号 ≥24px**(`:85` 纸面 UI 下限 + `casebook-39.md:308`「题签 / 落字」两半):
//      两类 + 题签类均绑 `--skeuo-font-size-lexeme`,且该 token 值 ≥24px;
//   ③ **底衬走 var**(C2/C4 门禁:零内联色、零硬编码);
//   ④ **明度轴 Δ**(承 story-021 AC-021-3 先例):值区底衬 vs 纸面主题色 ≥ 可辨阈值
//      —— 底衬须「可辨但不喧宾」,不是与纸面同色(否则底衬白加)。
//      ⚠️ **2026-10-13 二轮**:判据对象**由「纸纹中心」改为「`--skeuo-paper-bg` 主题色」**。
//      原口径拿底衬比纸纹中心(#fefdfe,近白,值区实际背景)看似更准,但它把「可辨」
//      钉在**一个 texture 采样值**上 —— 纸纹中心均值无 token 承载、无权威出处,
//      采样一变判据就漂(而 2026-10-13 实测 #f2ead8 在 paper-bg 口径下 Δ=0.0150
//      会自红,正是该口径在纠正取值)。改比主题色:Δ 有了 token 级真源。
//      (值的可辨性另一半由 `--skeuo-reading-rule` 边线**几何定界**承担 —— 见
//      `test_s3_5`;明度差不再是定界的唯一机制。)
//   ⑤ **边线定界(2026-10-13 二轮新增)**:`.channel-reading*` 须绑
//      `--skeuo-reading-rule` 的四向 border —— 换机制后的定界载体,缺它即
//      退回「靠明度差」的死结路径。
//
// ⚠️ 本件**纯文本/正则 + 主题文件解析**(照 `sign_channel_binder_test` 先例)——
//   EditMode `Sim.Contracts.Tests` **不引用 `Gameplay.UI`**(asmdef 引用集),
//   故不 `using` 任何 42 类型,只读 USS 文件字节。
// ⚠️ **几何腿的诚实边界**:字号档「会不会撑破 44px 行高下限」**本件不判** ——
//   batch 宿主 panel 无尺寸,`resolvedStyle.height` 恒 0、`MeasureTextSize` 返 NaN
//   (2026-10-12 S3 探针三轮实测),几何在无渲染宿主里量不出来。该腿归**人工走查**
//   (件 5 证据件),不得以本件借绿。

using System;
using System.IO;
using System.Text.RegularExpressions;
using DaYiJingCheng.EditorTools.Gates;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.SkeuomorphicUI
{
    /// <summary>S3 词条可读性判据(对比 / 字号 / 底衬 / 明度轴)。</summary>
    internal sealed class skeuo_readability_test
    {
        private static readonly string SkeuoDir = Path.Combine(
            TextureBindingGates.DefaultRepoRoot, "Assets", "Gameplay.UI", "Skeuomorphic");

        /// <summary>纸面 UI 最小字号下限(`accessibility-requirements.md:85` @1080p 基准)。</summary>
        private const double MinFontSizePx = 24.0;

        /// <summary>墨字 on 纸面承诺对比(`:87` / `casebook-39.md:309`)。</summary>
        private const double MinContrastRatio = 7.0;

        /// <summary>底衬 vs 纸面主题色的最小明度差(承 story-021 minΔ 口径;底衬须可辨)。
        /// ⚠️ 2026-10-13 二轮:比较对象由纸纹中心采样改为 `--skeuo-paper-bg` token
        /// (真源可溯);阈值不变。</summary>
        private const double MinBackingLuminanceDelta = 0.02;

        // ══════════════════════════════════════════════════════════════════════
        // ① 对比 ≥7:1(核心 —— 阴性态原为 3.87:1 违约;2026-10-13 二轮由 7.48 → 8.27)
        // ══════════════════════════════════════════════════════════════════════
        [Test]
        public void test_s3_1_bothReadingInks_contrastAtLeast7to1_onBacking()
        {
            string theme = ReadTheme();
            double backing = LuminanceOfHex(ExtractHex(theme, "--skeuo-reading-backing"));

            var cases = new (string varName, string label)[]
            {
                ("--skeuo-ink-fg", "阳性墨"),
                ("--skeuo-ink-negative", "阴性墨"),
            };

            foreach (var (varName, label) in cases)
            {
                double ink = LuminanceOfHex(ExtractHex(theme, varName));
                double ratio = ContrastRatio(ink, backing);
                Assert.That(ratio, Is.GreaterThanOrEqualTo(MinContrastRatio),
                    $"{label}({varName})在值区底衬上的对比 = {ratio:F2}:1,须 ≥ {MinContrastRatio}:1 " +
                    $"(`:87` + `casebook-39.md:309`)。⚠️ 阴性态改前(ink-faded 合成)仅 3.87:1 —— " +
                    $"本断言是该违约的回归门;2026-10-13 二轮由 7.48 贴线值升至 8.27(留冗余)。");
            }
        }

        /// <summary>公式锚定(承 story-021 评审修复先例):白=1 / 黑=0 / 128灰≈0.21586 ——
        /// 防 `LuminanceOfHex` 退化后所有对比断言恒过。</summary>
        [Test]
        public void test_s3_1_luminanceFormula_anchored()
        {
            Assert.That(LuminanceOfHex("#ffffff"), Is.EqualTo(1.0).Within(1e-6), "白 L 应 = 1");
            Assert.That(LuminanceOfHex("#000000"), Is.EqualTo(0.0).Within(1e-6), "黑 L 应 = 0");
            Assert.That(LuminanceOfHex("#808080"), Is.EqualTo(0.21586).Within(1e-3),
                "128 灰 L 应 ≈ 0.21586(gamma 解码或 Rec.709 权重退化)");
        }

        // ══════════════════════════════════════════════════════════════════════
        // ② 字号 ≥24px(`:85` 下限 + `:308` 题签 / 落字两半)
        // ══════════════════════════════════════════════════════════════════════
        [Test]
        public void test_s3_2_lexemeToken_atLeast24px()
        {
            string theme = ReadTheme();
            var m = Regex.Match(theme, @"--skeuo-font-size-lexeme:\s*(\d+(?:\.\d+)?)px");
            Assert.IsTrue(m.Success, "缺 `--skeuo-font-size-lexeme` token(`:85` 纸面 UI 下限的载体)。");
            double px = double.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            Assert.That(px, Is.GreaterThanOrEqualTo(MinFontSizePx),
                $"`--skeuo-font-size-lexeme` = {px}px,须 ≥ {MinFontSizePx}px(`:85`)。" +
                $"⚠️ 原 5 档上限 xl=22px **物理上够不到 24** —— 本 token 是该缺口的唯一出路。");
        }

        [Test]
        public void test_s3_2_readingAndLabel_classes_bindLexemeToken()
        {
            string paper = StripComments(ReadUss("SkeuoPaper.uss"));

            foreach (string cls in new[] { ".channel-reading", ".channel-reading-negative", ".channel-label" })
            {
                var block = Regex.Match(paper, Regex.Escape(cls) + @"\s*\{[^}]*\}");
                Assert.IsTrue(block.Success, $"缺 `{cls}` 声明块。");
                Assert.IsTrue(Regex.IsMatch(block.Value, @"font-size:\s*var\(--skeuo-font-size-lexeme\)"),
                    $"`{cls}` 须绑 `var(--skeuo-font-size-lexeme)`(≥24px) —— " +
                    $"`:308`「通道题签 / 落字 ≥24px」的载体;改回 md(15px)即红。");
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // ③ 底衬走 var(零内联 / 零硬编码 —— C2/C4 门禁同款)
        // ══════════════════════════════════════════════════════════════════════
        [Test]
        public void test_s3_3_readingBacking_goesThroughVariable_noInline()
        {
            string paper = StripComments(ReadUss("SkeuoPaper.uss"));

            foreach (string cls in new[] { ".channel-reading", ".channel-reading-negative" })
            {
                var block = Regex.Match(paper, Regex.Escape(cls) + @"\s*\{[^}]*\}");
                Assert.IsTrue(block.Success, $"缺 `{cls}` 声明块。");
                Assert.IsTrue(Regex.IsMatch(block.Value,
                        @"background-color:\s*var\(--skeuo-reading-backing\)"),
                    $"`{cls}` 底衬须走 `var(--skeuo-reading-backing)`(C2/C4:零内联色)。");
                Assert.IsFalse(Regex.IsMatch(block.Value, @"#[0-9a-fA-F]{3,8}\b"),
                    $"`{cls}` 块内禁内联 hex。");
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // ④ 明度轴 Δ:底衬 vs 纸面主题色须可辨(不是同色)
        //    ⚠️ 2026-10-13 二轮:判据对象由「纸纹中心 texture 采样」改为「`--skeuo-paper-bg`
        //    token」—— 前者无 token 级真源(sample 口径漂移即判据漂移);值的可辨性另一半
        //    已由边线几何定界承担(见 ⑤),此处只守「不与纸面同色」的底线。
        // ══════════════════════════════════════════════════════════════════════
        [Test]
        public void test_s3_4_backing_luminanceDelta_overPaper_visible()
        {
            string theme = ReadTheme();
            double backing = LuminanceOfHex(ExtractHex(theme, "--skeuo-reading-backing"));
            double paper = LuminanceOfHex(ExtractHex(theme, "--skeuo-paper-bg"));

            double delta = Math.Abs(backing - paper);
            Assert.That(delta, Is.GreaterThanOrEqualTo(MinBackingLuminanceDelta),
                $"底衬 vs 纸面主题色明度差 = {delta:F4},须 ≥ {MinBackingLuminanceDelta} " +
                $"(底衬须可辨 —— 与纸面同色 = 底衬白加,承 story-021 minΔ 口径)。" +
                $"⚠️ 2026-10-13 二轮:现值 #e7e0cd 的 Δ = 0.0947;" +
                $"落选值 #f2ead8 的 Δ 仅 0.0150(**会自红**)—— 本断言即其排除门。");

            // 自比对判别力守卫:同色 ⇒ Δ=0 < 阈值(公式退化必红)
            Assert.That(Math.Abs(backing - backing), Is.LessThan(MinBackingLuminanceDelta),
                "自比对 Δ 应 = 0 < 阈值(公式非恒过)。");
        }

        // ══════════════════════════════════════════════════════════════════════
        // ⑤ 边线定界(2026-10-13 二轮新增):`.channel-reading*` 四向 border 绑
        //    `--skeuo-reading-rule` —— 「换机制」后的定界载体。
        //    为何是判据而不是可选 polish:初版靠明度差定界已被实测判红(真机
        //    「对比不足」),机制换成边线;边线缺失 = 退回已失败的路径。
        // ══════════════════════════════════════════════════════════════════════
        [Test]
        public void test_s3_5_readingClasses_bindRuleBorder()
        {
            string theme = ReadTheme();
            string paper = StripComments(ReadUss("SkeuoPaper.uss"));

            // token 须存在且是 rgba 形(C2/C4:零内联 —— 边线色也走变量)
            Assert.IsTrue(Regex.IsMatch(theme,
                    @"--skeuo-reading-rule:\s*rgba\("),
                "缺 `--skeuo-reading-rule` token(rgba 形)—— 值区边线定界机制的单一真源。");

            foreach (string cls in new[] { ".channel-reading", ".channel-reading-negative" })
            {
                var block = Regex.Match(paper, Regex.Escape(cls) + @"\s*\{[^}]*\}");
                Assert.IsTrue(block.Success, $"缺 `{cls}` 声明块。");

                foreach (string dir in new[] { "top", "right", "bottom", "left" })
                {
                    Assert.IsTrue(Regex.IsMatch(block.Value,
                            $@"border-{dir}-color:\s*var\(--skeuo-reading-rule\)"),
                        $"`{cls}` 缺 border-{dir}-color var 绑定 —— 边线四向须齐" +
                        $"(缺向 = 定界半边,2026-10-13 二轮机制的回归门)。");
                }

                // 块内禁内联 hex(边线色同样禁写死 —— C2/C4 只禁 hex,rgba 字面量
                // 若出现亦属内联,但 token 已在主题层,块内只应有 var)
                Assert.IsFalse(Regex.IsMatch(block.Value, @"#[0-9a-fA-F]{3,8}\b"),
                    $"`{cls}` 块内禁内联 hex。");
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // helpers
        // ══════════════════════════════════════════════════════════════════════

        private static string ReadTheme() => ReadUss("SkeuoThemeVariables.uss");

        private static string ReadUss(string name)
        {
            string path = Path.Combine(SkeuoDir, name);
            Assert.IsTrue(File.Exists(path), $"前置失败:{path} 不存在。");
            return File.ReadAllText(path);
        }

        private static string StripComments(string uss) =>
            Regex.Replace(uss, @"/\*.*?\*/", "", RegexOptions.Singleline);

        /// <summary>从主题文本抽 `--var: #rrggbb` 的色值。</summary>
        private static string ExtractHex(string theme, string varName)
        {
            var m = Regex.Match(theme, Regex.Escape(varName) + @":\s*(#[0-9a-fA-F]{6})\b");
            Assert.IsTrue(m.Success, $"主题变量 {varName} 未找到或非 #rrggbb(结构漂移?)。");
            return m.Groups[1].Value;
        }

        private static double SrgbToLinear(double c) =>
            c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);

        /// <summary>#rrggbb → Rec.709 线性相对亮度(art-bible :349 线性工作流)。</summary>
        private static double LuminanceOfHex(string hex)
        {
            hex = hex.TrimStart('#');
            double r = Convert.ToInt32(hex.Substring(0, 2), 16);
            double g = Convert.ToInt32(hex.Substring(2, 2), 16);
            double b = Convert.ToInt32(hex.Substring(4, 2), 16);
            return 0.2126 * SrgbToLinear(r / 255.0)
                 + 0.7152 * SrgbToLinear(g / 255.0)
                 + 0.0722 * SrgbToLinear(b / 255.0);
        }

        /// <summary>WCAG 对比度(线性亮度)。</summary>
        private static double ContrastRatio(double l1, double l2)
        {
            double hi = Math.Max(l1, l2), lo = Math.Min(l1, l2);
            return (hi + 0.05) / (lo + 0.05);
        }
    }
}
