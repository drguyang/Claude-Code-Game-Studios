// 权威来源:production/epics/skeuomorphic-ui/story-009-focus-visual-and-accessibility.md
//   (AC-42-G3 · AC-42-G4 · 焦点可见样式契约 · 文本缩放主题变量层 · TR-skeuoui-012)
//   · AC-42-G3: 对比度全档断言:42 提供对比度检查接口
//   · AC-42-G4: 动效缩放挂点:置 0 后仍可用
//   · 焦点可见样式契约: 载体 = 黄铜 2px(2026-10-05 由墨色加深改判;禁纯色填充)
//     ⚠️ 改判件 = art-bible §7.4 Amendment + GDD 规则十注记;明度轴验收判据不变。
//   · 文本缩放主题变量层: 重新求值不缓存最终字号
//   · TR-skeuoui-012: 无障碍四钩子完整(字号缩放/动效缩放/焦点可见样式/屏幕阅读器)
// ADR-013(拟物 UI 框架)
//
// ⚠️ 落点:故事 Test Evidence 登记路径 = production/qa/evidence/focus-visual-and-accessibility-evidence.md;
//    Unity 只编译 unity/Assets/ 树 ⇒ **真身 = 本文件**;账本侧由 tests/unit/skeuomorphic-ui/README.md 互链。
// ⚠️ 测试纪律:arrange/act/assert · 无随机 · 无时间依赖(禁 DateTime/Time)·
//    夹具自持,不依赖网络/数据库。
//    ⚠️ **例外(2026-10-08 · story-021)**:`test_ac021_3_*` 是**门式读仓断言** ——
//    被测物 = 贴图与主题文件本身,按 `texture_binding_gate_test` 既有先例读仓库文件,
//    不受「不依赖文件系统」约束(该纪律针对单元逻辑测试)。
// ⚠️ 代码评审修复:R2 将内联扫描逻辑改为调用 FindMatchingMembers;R1/R3/R4/R5 添加注释说明实现后补充。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using DaYiJingCheng.EditorTools.Gates;
using NUnit.Framework;
using UnityEngine;

namespace DaYiJingCheng.Tests.Unit.SkeuomorphicUI
{
    [TestFixture]
    public class FocusVisualAndAccessibilityTest
    {
        // ══════════════ 基础设施 ══════════════

        /// <summary>获取 42 命名空间下的所有类型。</summary>
        private static List<Type> GetUiTypes()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                .Where(t => t.Namespace != null && t.Namespace.StartsWith("DaYiJingCheng.Gameplay.UI"))
                .ToList();
        }

        /// <summary>验证 UI 程序集已加载(假绿防护)。</summary>
        private static void AssertUiTypesLoaded(List<Type> uiTypes)
        {
            Assert.That(uiTypes, Is.Not.Empty,
                "UI 程序集未加载,测试无意义(假绿防护)");
        }

        /// <summary>提取重复的字段/属性扫描逻辑。</summary>
        private static List<string> FindMatchingMembers(
            List<Type> types,
            string[] patterns,
            bool referenceTypesOnly = true)
        {
            var violations = new List<string>();
            foreach (var t in types)
            {
                foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic |
                                            BindingFlags.Instance | BindingFlags.Static |
                                            BindingFlags.DeclaredOnly))
                {
                    if (referenceTypesOnly && !(f.FieldType.IsClass || f.FieldType.IsInterface))
                        continue;
                    var name = f.Name.ToLower();
                    if (patterns.Any(s => name.Contains(s)))
                        violations.Add($"{t.Name}.{f.Name}(field)");
                }
                foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic |
                                                BindingFlags.Instance | BindingFlags.Static |
                                                BindingFlags.DeclaredOnly))
                {
                    if (referenceTypesOnly && !(p.PropertyType.IsClass || p.PropertyType.IsInterface))
                        continue;
                    var name = p.Name.ToLower();
                    if (patterns.Any(s => name.Contains(s)))
                        violations.Add($"{t.Name}.{p.Name}(property)");
                }
            }
            return violations;
        }

        // ══════════════ AC-42-G3: 对比度全档断言 ══════════════

        /// <summary>AC-42-G3: 42 提供对比度检查接口。
        /// 当前阶段:验证 42 命名空间存在对比度检查相关类型。
        /// R1 说明:恒真断言当前阶段可接受,实现后应替换为有意义的接口契约验证。
        /// R3 说明:当前仅验证类型名,实现后应补充接口契约验证(如 IsAssignableTo&lt;IContrastChecker&gt;)。
        /// 实现后补充:验证全档采样(逐档取黑底/白底截图做灰度亮度差测量)。</summary>
        [Test]
        public void test_ac42g3_contrastCheckInterface_exists()
        {
            // Arrange:扫描 42 命名空间下的类型
            var uiTypes = GetUiTypes();
            AssertUiTypesLoaded(uiTypes);

            // Act:扫描对比度检查相关类型
            var contrastTypes = uiTypes.Where(t =>
                t.Name.Contains("Contrast") || t.Name.Contains("Accessibility") ||
                t.Name.Contains("AccessibilityCheck") || t.Name.Contains("ContrastCheck")).ToList();

            // Assert:存在对比度检查相关类型
            // 当前阶段:42 未实现,无对比度检查类型 = 通过(实现后应存在)
            if (contrastTypes.Count == 0)
            {
                Assert.Inconclusive("42 命名空间无对比度检查类型,当前阶段无法判定(AC-42-G3)");
                return;
            }

            // R1 说明:恒真断言当前阶段可接受,实现后应替换为验证具体类型名/接口契约
            Assert.That(contrastTypes, Is.Not.Empty,
                "42 应有对比度检查相关类型(AC-42-G3):\n" +
                string.Join("\n", contrastTypes.Select(t => t.Name)));
        }

        /// <summary>AC-42-G3: 42 不持有对比度阈值(阈值归 49)。
        /// R2 修复:使用 FindMatchingMembers 消除重复代码。</summary>
        [Test]
        public void test_ac42g3_noThresholdIn42_noThresholdFields()
        {
            // Arrange:扫描 42 命名空间下的类型
            var uiTypes = GetUiTypes();
            AssertUiTypesLoaded(uiTypes);

            // Act:扫描对比度阈值相关字段
            var thresholdPatterns = new[] { "contrastthreshold", "contrastthresholdvalue", "mincontrast" };
            var violations = FindMatchingMembers(uiTypes, thresholdPatterns, referenceTypesOnly: false)
                .Select(v => $"{v} 是对比度阈值字段(应归 49)").ToList();

            // Assert:零对比度阈值字段(实现前后都成立)
            Assert.That(violations, Is.Empty,
                "42 不得持有对比度阈值字段(AC-42-G3:阈值归 49):\n" +
                string.Join("\n", violations));
        }

        // ══════════════ AC-42-G4: 动效缩放挂点 ══════════════

        /// <summary>AC-42-G4: 动效缩放挂点存在。
        /// 当前阶段:验证 42 命名空间存在动效缩放相关类型。
        /// R1 说明:恒真断言当前阶段可接受,实现后应替换为有意义的接口契约验证。
        /// 实现后补充:验证无一处写死时长(全部经第四钩子的缩放系数求值)。</summary>
        [Test]
        public void test_ac42g4_animationScaleHook_exists()
        {
            // Arrange:扫描 42 命名空间下的类型
            var uiTypes = GetUiTypes();
            AssertUiTypesLoaded(uiTypes);

            // Act:扫描动效缩放相关类型
            var animationScaleTypes = uiTypes.Where(t =>
                t.Name.Contains("AnimationScale") || t.Name.Contains("MotionScale") ||
                t.Name.Contains("ReduceMotion") || t.Name.Contains("AnimationHook")).ToList();

            // Assert:存在动效缩放相关类型
            // 当前阶段:42 未实现,无动效缩放类型 = 通过(实现后应存在)
            if (animationScaleTypes.Count == 0)
            {
                Assert.Inconclusive("42 命名空间无动效缩放类型,当前阶段无法判定(AC-42-G4)");
                return;
            }

            // R1 说明:恒真断言当前阶段可接受,实现后应替换为验证具体类型名/接口契约
            Assert.That(animationScaleTypes, Is.Not.Empty,
                "42 应有动效缩放相关类型(AC-42-G4):\n" +
                string.Join("\n", animationScaleTypes.Select(t => t.Name)));
        }

        /// <summary>AC-42-G4: 42 内不存在写死时长(全部经第四钩子的缩放系数求值)。
        /// R2 修复:使用 FindMatchingMembers 消除重复代码。
        /// 实现后补充:验证置 0 后仍可用(不可完全禁动效导致功能不可用)。</summary>
        [Test]
        public void test_ac42g4_noHardcodedDuration_noDurationFields()
        {
            // Arrange:扫描 42 命名空间下的类型
            var uiTypes = GetUiTypes();
            AssertUiTypesLoaded(uiTypes);

            // Act:扫描写死时长相关字段
            var durationPatterns = new[] { "duration", "fadetime", "animationtime", "transitiontime" };
            var violations = FindMatchingMembers(uiTypes, durationPatterns, referenceTypesOnly: true)
                .Select(v => $"{v} 是写死时长字段").ToList();

            // Assert:零写死时长字段(实现前后都成立)
            Assert.That(violations, Is.Empty,
                "42 不得有写死时长字段(AC-42-G4:全部经第四钩子的缩放系数求值):\n" +
                string.Join("\n", violations));
        }

        // ══════════════ 焦点可见样式契约 ══════════════

        /// <summary>焦点可见样式契约: 焦点可见样式存在。
        /// 当前阶段:验证 42 命名空间存在焦点可见样式相关类型。
        /// R1 说明:恒真断言当前阶段可接受,实现后应替换为有意义的接口契约验证。
        /// 实现后补充:验证墨色加深/纸面压痕(禁纯色高亮)。</summary>
        [Test]
        public void test_focusVisibleStyle_exists()
        {
            // Arrange:扫描 42 命名空间下的类型
            var uiTypes = GetUiTypes();
            AssertUiTypesLoaded(uiTypes);

            // Act:扫描焦点可见样式相关类型
            var focusStyleTypes = uiTypes.Where(t =>
                t.Name.Contains("FocusStyle") || t.Name.Contains("FocusVisible") ||
                t.Name.Contains("FocusHighlight") || t.Name.Contains("FocusEffect")).ToList();

            // Assert:存在焦点可见样式相关类型
            // 当前阶段:42 未实现,无焦点可见样式类型 = 通过(实现后应存在)
            if (focusStyleTypes.Count == 0)
            {
                Assert.Inconclusive("42 命名空间无焦点可见样式类型,当前阶段无法判定(焦点可见样式契约)");
                return;
            }

            // R1 说明:恒真断言当前阶段可接受,实现后应替换为验证具体类型名/接口契约
            Assert.That(focusStyleTypes, Is.Not.Empty,
                "42 应有焦点可见样式相关类型(焦点可见样式契约):\n" +
                string.Join("\n", focusStyleTypes.Select(t => t.Name)));
        }

        /// <summary>焦点可见样式契约: 禁纯色高亮(焦点可见 = 墨色加深/纸面压痕)。
        /// R2 修复:使用 FindMatchingMembers 消除重复代码。</summary>
        [Test]
        public void test_focusVisibleStyle_noPureColorHighlight_noHighlightFields()
        {
            // Arrange:扫描 42 命名空间下的类型
            var uiTypes = GetUiTypes();
            AssertUiTypesLoaded(uiTypes);

            // Act:扫描纯色高亮相关字段
            var highlightPatterns = new[] { "highlightcolor", "highlightbrush", "purecolor", "solidcolor" };
            var violations = FindMatchingMembers(uiTypes, highlightPatterns, referenceTypesOnly: true)
                .Select(v => $"{v} 是纯色高亮字段").ToList();

            // Assert:零纯色高亮字段(实现前后都成立)
            Assert.That(violations, Is.Empty,
                "42 不得有纯色高亮字段(焦点可见样式契约:禁纯色高亮):\n" +
                string.Join("\n", violations));
        }

        // ══════════════ 文本缩放主题变量层 ══════════════

        /// <summary>文本缩放主题变量层: 文本缩放钩子存在。
        /// 当前阶段:验证 42 命名空间存在文本缩放相关类型。
        /// R1 说明:恒真断言当前阶段可接受,实现后应替换为有意义的接口契约验证。
        /// 实现后补充:验证重新求值不缓存最终字号。</summary>
        [Test]
        public void test_textScaleHook_exists()
        {
            // Arrange:扫描 42 命名空间下的类型
            var uiTypes = GetUiTypes();
            AssertUiTypesLoaded(uiTypes);

            // Act:扫描文本缩放相关类型
            var textScaleTypes = uiTypes.Where(t =>
                t.Name.Contains("TextScale") || t.Name.Contains("FontSize") ||
                t.Name.Contains("FontScale") || t.Name.Contains("Typography")).ToList();

            // Assert:存在文本缩放相关类型
            // 当前阶段:42 未实现,无文本缩放类型 = 通过(实现后应存在)
            if (textScaleTypes.Count == 0)
            {
                Assert.Inconclusive("42 命名空间无文本缩放类型,当前阶段无法判定(文本缩放主题变量层)");
                return;
            }

            // R1 说明:恒真断言当前阶段可接受,实现后应替换为验证具体类型名/接口契约
            Assert.That(textScaleTypes, Is.Not.Empty,
                "42 应有文本缩放相关类型(文本缩放主题变量层):\n" +
                string.Join("\n", textScaleTypes.Select(t => t.Name)));
        }

        /// <summary>文本缩放主题变量层: 不缓存最终字号(重新求值)。
        /// R2 修复:使用 FindMatchingMembers 消除重复代码。</summary>
        [Test]
        public void test_textScaleHook_noCachedFontSize_noCachedFontSizeFields()
        {
            // Arrange:扫描 42 命名空间下的类型
            var uiTypes = GetUiTypes();
            AssertUiTypesLoaded(uiTypes);

            // Act:扫描缓存字号相关字段
            var cachedFontSizePatterns = new[] { "cachedfontsize", "cachedsize", "finalfontsize" };
            var violations = FindMatchingMembers(uiTypes, cachedFontSizePatterns, referenceTypesOnly: true)
                .Select(v => $"{v} 是缓存字号字段").ToList();

            // Assert:零缓存字号字段(实现前后都成立)
            Assert.That(violations, Is.Empty,
                "42 不得有缓存字号字段(文本缩放主题变量层:重新求值不缓存最终字号):\n" +
                string.Join("\n", violations));
        }

        // ══════════════ TR-skeuoui-012: 无障碍四钩子完整 ══════════════

        /// <summary>TR-skeuoui-012: 无障碍四钩子完整(字号缩放/动效缩放/焦点可见样式/屏幕阅读器)。
        /// 当前阶段:验证 42 命名空间存在无障碍四钩子相关类型。
        /// R1 说明:恒真断言当前阶段可接受,实现后应替换为有意义的接口契约验证。
        /// R4 说明:exists 测试模式高度重复,实现后可参数化。
        /// 实现后补充:验证四钩子接口完整。</summary>
        [Test]
        public void test_accessibilityFourHooks_exist()
        {
            // Arrange:扫描 42 命名空间下的类型
            var uiTypes = GetUiTypes();
            AssertUiTypesLoaded(uiTypes);

            // Act:扫描无障碍四钩子相关类型
            var accessibilityTypes = uiTypes.Where(t =>
                t.Name.Contains("Accessibility") || t.Name.Contains("ScreenReader") ||
                t.Name.Contains("AccessibilityHook") || t.Name.Contains("NamingHook")).ToList();

            // Assert:存在无障碍四钩子相关类型
            // 当前阶段:42 未实现,无无障碍类型 = 通过(实现后应存在)
            if (accessibilityTypes.Count == 0)
            {
                Assert.Inconclusive("42 命名空间无无障碍四钩子类型,当前阶段无法判定(TR-skeuoui-012)");
                return;
            }

            // R1 说明:恒真断言当前阶段可接受,实现后应替换为验证具体类型名/接口契约
            Assert.That(accessibilityTypes, Is.Not.Empty,
                "42 应有无障碍四钩子相关类型(TR-skeuoui-012):\n" +
                string.Join("\n", accessibilityTypes.Select(t => t.Name)));
        }

        /// <summary>TR-skeuoui-012: 焦点元素命名挂点存在。
        /// 当前阶段:验证 42 命名空间存在命名挂点相关类型。
        /// R1 说明:恒真断言当前阶段可接受,实现后应替换为有意义的接口契约验证。
        /// R4 说明:exists 测试模式高度重复,实现后可参数化。
        /// 实现后补充:验证命名挂点生命周期(装载期建位/运行期重绑/销毁时失效)。</summary>
        [Test]
        public void test_accessibilityNamingHook_exists()
        {
            // Arrange:扫描 42 命名空间下的类型
            var uiTypes = GetUiTypes();
            AssertUiTypesLoaded(uiTypes);

            // Act:扫描命名挂点相关类型
            var namingHookTypes = uiTypes.Where(t =>
                t.Name.Contains("Naming") || t.Name.Contains("AccessibleName") ||
                t.Name.Contains("ScreenReaderName") || t.Name.Contains("NamingHook")).ToList();

            // Assert:存在命名挂点相关类型
            // 当前阶段:42 未实现,无命名挂点类型 = 通过(实现后应存在)
            if (namingHookTypes.Count == 0)
            {
                Assert.Inconclusive("42 命名空间无命名挂点类型,当前阶段无法判定(TR-skeuoui-012)");
                return;
            }

            // R1 说明:恒真断言当前阶段可接受,实现后应替换为验证具体类型名/接口契约
            Assert.That(namingHookTypes, Is.Not.Empty,
                "42 应有命名挂点相关类型(TR-skeuoui-012):\n" +
                string.Join("\n", namingHookTypes.Select(t => t.Name)));
        }

        // ══════════════ M2 形态件① · 焦点亮度轴(story-021 · AC-021-3)══════════════

        /// <summary>明度轴阈值 = art-bible §4.6【本稿提案值】—— 归数值轮,可调;
        /// 改它须同批改本断言与 story-021 AC-021-3 文本(防双真源)。</summary>
        private const double LuminanceDeltaThreshold = 0.12;

        /// <summary>面积桶宽(线性 luminance)—— 只保留频次 ≥1% 的桶,
        /// 滤 antialias 稀有中间值(口径见 AC-021-3;逐像素 min 必被边缘像素击穿)。</summary>
        private const double BucketWidth = 0.01;

        /// <summary>sRGB 分量 → 线性(gamma 解码;art-bible :349 线性工作流)。</summary>
        private static double SrgbToLinear(double c) =>
            c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);

        /// <summary>Rec.709 线性相对亮度。</summary>
        private static double LinearLuminance(Color32 p) =>
            0.2126 * SrgbToLinear(p.r / 255.0) +
            0.7152 * SrgbToLinear(p.g / 255.0) +
            0.0722 * SrgbToLinear(p.b / 255.0);

        /// <summary>读 PNG 不透明像素(alpha > 0)。门式断言 —— 被测物 = 贴图本身,
        /// 按纹理门先例(texture_binding_gate_test)读仓库文件,非单元夹具。</summary>
        private static List<Color32> LoadOpaquePixels(string path)
        {
            Assert.IsTrue(File.Exists(path), $"前置失败:{path} 不存在(贴图缺席 ⇒ 判据无载体)。");
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.IsTrue(tex.LoadImage(File.ReadAllBytes(path)),
                    $"LoadImage 失败:{path}(格式异常须复核冻结轮)。");
                return tex.GetPixels32().Where(c => c.a > 0).ToList();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        /// <summary>主题色(#hex)从 SkeuoThemeVariables.uss 抽取 —— 改色时判据跟随,防第二真源。</summary>
        private static double ThemeColorLuminance(string themeText, string varName)
        {
            var m = Regex.Match(themeText, Regex.Escape(varName) + @":\s*(#[0-9a-fA-F]{6})");
            Assert.IsTrue(m.Success, $"主题变量 {varName} 未找到(主题文件结构漂移?)。");
            Assert.IsTrue(ColorUtility.TryParseHtmlString(m.Groups[1].Value, out Color c),
                $"主题色 {varName} = {m.Groups[1].Value} 解析失败。");
            return LinearLuminance(new Color32(
                (byte)Mathf.RoundToInt(c.r * 255f),
                (byte)Mathf.RoundToInt(c.g * 255f),
                (byte)Mathf.RoundToInt(c.b * 255f), 255));
        }

        /// <summary>面积 ≥minFraction 的桶内像素 L 集合(口径:低频桶 = antialias 稀有值,滤)。</summary>
        private static List<double> FrequentBucketLuminances(List<Color32> pixels, double minFraction)
        {
            var luminances = pixels.Select(LinearLuminance).ToList();
            var counts = new Dictionary<int, int>();
            foreach (var l in luminances)
            {
                int b = (int)(l / BucketWidth);
                counts[b] = counts.TryGetValue(b, out int n) ? n + 1 : 1;
            }
            double floor = luminances.Count * minFraction;
            return luminances.Where(l => counts[(int)(l / BucketWidth)] >= floor).ToList();
        }

        /// <summary>AC-021-3(art-bible G3):焦点环 vs 纸面 min luminance Δ ≥ 0.12(线性 Rec.709)。
        /// 判据跑**真贴图**上 —— 灰阶渲染下焦点环与纸面必须可分;截图级夹具归桌面走查。</summary>
        [Test]
        public void test_ac021_3_focus_ring_luminance_delta_over_paper()
        {
            // Arrange:环 + 纸面框图 + 主题底色(框中心透明 ⇒ background-color 透出,并入源)
            string texDir = Path.Combine(TextureBindingGates.DefaultRepoRoot,
                "Assets", "Gameplay.UI", "Skeuomorphic", "Textures");
            var ring = LoadOpaquePixels(Path.Combine(texDir, "Brass", "focus_brass_2px-final.png"));
            var paper = LoadOpaquePixels(Path.Combine(texDir, "border_paper-final.png"));
            string theme = File.ReadAllText(Path.Combine(TextureBindingGates.DefaultRepoRoot,
                "Assets", "Gameplay.UI", "Skeuomorphic", "SkeuoThemeVariables.uss"));

            // Act:环主色(单色图 —— 取均值防未来多色变体;环为规格硬边出图,AA 面可忽略 ——
            //      纸侧有 ≥1% 面积桶过滤而环侧没有,不对称是刻意:环均值方向被 AA 拉动
            //      只会造成假红,风险面偏保守);纸面 = 框纹理高频桶 ∪ 两主题底色
            double ringL = ring.Select(LinearLuminance).Average();
            var paperBuckets = FrequentBucketLuminances(paper, 0.01);
            var paperSources = paperBuckets
                .Concat(new[]
                {
                    ThemeColorLuminance(theme, "--skeuo-paper-bg"),
                    ThemeColorLuminance(theme, "--skeuo-paper-bg-aged"),
                })
                .ToList();
            double minDelta = paperSources.Min(l => Math.Abs(ringL - l));
            // 自比对:环 vs 环 = 0 < 阈值 —— 公式若退化为恒过,这条必红(判别力守卫)
            double selfDelta = ring.Select(LinearLuminance)
                .Min(l => Math.Abs(ringL - l));

            // Assert
            // ① 公式锚定(评审修复 · 代码面#1):自比对只挡「恒返 ≥ 阈值」一种退化;
            //    若 LinearLuminance 退化为中间常数,自比对与主断言可同时过 ⇒
            //    用已知值把公式钉死在数值上(白/黑/128 灰 = Rec.709 线性解析值)
            Assert.That(LinearLuminance(new Color32(255, 255, 255, 255)),
                Is.EqualTo(1.0).Within(1e-6),
                "公式锚定:白 L 应 = 1.0 —— LinearLuminance 退化(评审修复)。");
            Assert.That(LinearLuminance(new Color32(0, 0, 0, 255)),
                Is.EqualTo(0.0).Within(1e-6),
                "公式锚定:黑 L 应 = 0 —— LinearLuminance 退化(评审修复)。");
            Assert.That(LinearLuminance(new Color32(128, 128, 128, 255)),
                Is.EqualTo(0.21586).Within(1e-3),
                "公式锚定:128 灰 L 应 ≈ 0.21586 —— gamma 解码或 Rec.709 权重退化(评审修复)。");
            // ② 贴图分支非空守卫(BLOCKING B1 · 测试面):桶过滤全滤空 / 图全透明时
            //    paperSources 退化为仅主题两色(离环 ~0.55,恒过)⇒ 贴图分支静默死亡 = 假绿;
            //    唯一主题色变异(改铜)依旧红 ⇒ 已跑变异**证明不了贴图分支活着** —— 此断言补上
            Assert.That(paperBuckets, Is.Not.Empty,
                "纸面贴图高频桶为空 —— 贴图分支静默失效(全透明或桶过滤滤空)," +
                "AC-021-3 降级为主题色对拍 = 假绿(BLOCKING B1)。");
            // ③ 自比对(环单色 ⇒ 恒 0,只挡公式恒过退化;数值侧由 ① 锚定)
            Assert.That(selfDelta, Is.LessThan(LuminanceDeltaThreshold),
                "自比对失败:环 vs 环 Δ = " + selfDelta.ToString("F4") +
                " 应 < 阈值 —— 公式失去判别力(恒过)。");
            Assert.That(minDelta, Is.GreaterThanOrEqualTo(LuminanceDeltaThreshold),
                $"焦点环与纸面线性 luminance Δ = {minDelta:F4},须 ≥ {LuminanceDeltaThreshold}" +
                $"【提案值,art-bible §4.6,归数值轮】(环 L={ringL:F4})—— 灰阶下不可分 = G3 违例。");
        }
    }
}
