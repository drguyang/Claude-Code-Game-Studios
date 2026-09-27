// Story 001: 拟物元件库基础 · 六条 AC 的构建期门(AC-42-C1 … C6)。
//
// 权威来源:
//   · production/epics/skeuomorphic-ui/story-001-component-library.md
//   · ADR-013 §四 / §Implementation Guidelines 3 / §六 无障碍钩子
//   · control-manifest Presentation Layer Rules「UI 双栈」
//   · TR-skeuoui-011(图集配额护栏)
//
// 形态 = 错误列表(空 = 通过);本文件**零 throw** —— 聚合非空列表 => 构建失败由调用方决定。
// 触发面 = ① 菜单项「大医精诚/Validation/Run Skeuomorphic UI Gates」
//         ② 编译后自动刷新(ReloadAssemblyPostProcessor)
//         ③ 构建前 fail-fast(IPreprocessBuildWithReport)
//         ④ CI EditMode 测试直调 ValidateAll()。
//
// 依赖关系:Editor.Tools.Gates → Gameplay.UI(读注册表 / 扫描 USS)。
//           2026-09-26 先例:MixerTopologyGates 已引用 Gameplay.Presentation,
//           此处引用 Gameplay.UI 同构(门住 Editor 层,不污染运行期引用图)。

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace DaYiJingCheng.EditorTools.Gates
{
    /// <summary>拟物元件库构建期校验(Story 001 · AC-42-C1…C6)。</summary>
    public static class SkeuomorphicUiGates
    {
        // ── AC-42-C3: 图集配额上限(从注册表读取,不维护第二份) ──
        private static int DefaultMaxComponents => DaYiJingCheng.Gameplay.UI.Skeuomorphic.SkeuoComponentRegistry.MaxRegisteredComponents;
        private static int DefaultMaxVariants => DaYiJingCheng.Gameplay.UI.Skeuomorphic.SkeuoComponentRegistry.MaxVariantsPerComponent;

        // ── AC-42-C4: 内联变体 lint ──
        // 策略:同类型元件若用硬编码颜色/尺寸模拟变体,说明元件库未登记对应变体。

        // ── AC-42-C4/C5: USS lint 正则 ──
        // 硬编码 px 字号: 行内或属性值中的 font-size: <数字>px
        private static readonly Regex HardcodedFontSizeRegex =
            new Regex(@"font-size\s*:\s*\d+px", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // 硬编码颜色 rgb() / #hex(不拦截 CSS 变量引用)
        private static readonly Regex HardcodedColorRegex =
            new Regex(@"(background-color|color|border-color)\s*:\s*(rgb\(|#)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // 硬编码尺寸 px(非变量引用) — C4 尺寸侧
        private static readonly Regex HardcodedSizeRegex =
            new Regex(@"(padding|margin|width|height|border-top-width|border-right-width|border-bottom-width|border-left-width)\s*:\s*\d+px", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // 硬编码文本 content 属性(USSText / Label 的 text 属性或 content USS 属性)
        private static readonly Regex HardcodedTextRegex =
            new Regex(@"(content|text)\s*:\s*[""'][^""']+[""']", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // 主题变量引用模式: var(--skeuo-*)
        private static readonly Regex ThemeVariableRegex =
            new Regex(@"var\((--skeuo-[a-z0-9-]+)\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // USS 注释去除(避免 regex 误报注释内容)
        private static readonly Regex UssCommentRegex =
            new Regex(@"/\*.*?\*/", RegexOptions.Compiled | RegexOptions.Singleline);

        // ── AC-42-C6: fallback 字体位 ──
        private const string ChineseFontSlot = "chinese";
        private const string EnglishFontSlot = "english";

        // ═══ 公共入口 ═══

        [MenuItem("大医精诚/Validation/Run Skeuomorphic UI Gates")]
        public static void RunMenu()
        {
            InitializeDefaults();
            var errs = ValidateAll();
            foreach (var e in errs) Debug.LogError(e);
            Debug.Log(errs.Count == 0
                ? "[SkeuomorphicUiGates] AC-42-C1…C6 全过"
                : $"[SkeuomorphicUiGates] {errs.Count} 条失败(见红行)");
        }

        /// <summary>初始化默认注册表与主题变量(构建期 / 菜单调用各一次)。</summary>
        private static void InitializeDefaults()
        {
            DaYiJingCheng.Gameplay.UI.Skeuomorphic.SkeuoComponentRegistry.InitializeDefaults();

            var ussDir = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Gameplay.UI", "Skeuomorphic");
            if (!Directory.Exists(ussDir)) return;

            var themeFile = Path.Combine(ussDir, "SkeuoThemeVariables.uss");
            if (!File.Exists(themeFile)) return;

            var text = File.ReadAllText(themeFile);
            var declared = DaYiJingCheng.Gameplay.UI.Skeuomorphic.ThemeVariableReferenceValidator.ExtractDeclaredNames(text);
            int count = 0;
            foreach (var name in declared)
            {
                DaYiJingCheng.Gameplay.UI.Skeuomorphic.ThemeVariableReferenceValidator.Register(name);
                count++;
            }
            Debug.Log($"[SkeuomorphicUiGates] 主题变量登记: {count} 个(来自 SkeuoThemeVariables.uss)");
        }

        /// <summary>执行全部 6 条 AC 校验,返回错误列表(空 = 通过)。</summary>
        public static List<string> ValidateAll()
        {
            var errs = new List<string>();
            errs.AddRange(ValidateComponentQuotas());             // C3
            errs.AddRange(ValidateThemeVariableReferences());     // C2
            errs.AddRange(ValidateUssHardcodedColors());          // C4
            errs.AddRange(ValidateUssHardcodedSizes());           // C4
            errs.AddRange(ValidateUssHardcodedFontSize());        // C5
            errs.AddRange(ValidateUssHardcodedText());            // C5
            errs.AddRange(ValidateFallbackFonts());               // C6
            errs.AddRange(ValidateNineSliceBounds());             // C1
            return errs;
        }

        // ═══ AC-42-C3: 元件库配额断言 ═══

        private static List<string> ValidateComponentQuotas()
        {
            var errs = new List<string>();
            try
            {
                var all = DaYiJingCheng.Gameplay.UI.Skeuomorphic.SkeuoComponentRegistry.All;
                if (all.Count > DefaultMaxComponents)
                    errs.Add($"[C3] 元件总数 {all.Count} 超过上限 {DefaultMaxComponents}。");

                foreach (var kv in all)
                {
                    if (kv.Value.AllocatedVariantSlots > DefaultMaxVariants)
                        errs.Add($"[C3] 元件「{kv.Key}」登记配额 {kv.Value.AllocatedVariantSlots} 超过单件上限 {DefaultMaxVariants}。");
                    if (kv.Value.VariantCount > kv.Value.AllocatedVariantSlots)
                        errs.Add($"[C3] 元件「{kv.Key}」实际变体数 {kv.Value.VariantCount} 超过登记配额 {kv.Value.AllocatedVariantSlots}。");
                }
            }
            catch (Exception ex)
            {
                errs.Add($"[C3] 读取元件库注册表失败: {ex.Message}");
            }
            return errs;
        }

        // ═══ AC-42-C2: USS 主题变量引用完整性 ═══
        // 策略:扫描 USS 文件,找出所有 var(--skeuo-*) 引用,与注册表比对。

        private static List<string> ValidateThemeVariableReferences()
        {
            var errs = new List<string>();
            var ussDir = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Gameplay.UI", "Skeuomorphic");
            if (!Directory.Exists(ussDir)) return errs;

            foreach (var ussFile in Directory.GetFiles(ussDir, "*.uss", SearchOption.TopDirectoryOnly))
            {
                var text = File.ReadAllText(ussFile);
                var matches = ThemeVariableRegex.Matches(text);
                foreach (Match m in matches)
                {
                    string varName = m.Groups[1].Value; // already --skeuo-xxx from capture group
                    if (!DaYiJingCheng.Gameplay.UI.Skeuomorphic.ThemeVariableReferenceValidator.IsRegistered(varName))
                        errs.Add($"[C2] {Path.GetFileName(ussFile)} 引用了未登记主题变量「{varName}」。");
                }
            }
            return errs;
        }

        // ═══ AC-42-C4: 内联变体 lint ═══

        private static List<string> ValidateUssHardcodedColors()
        {
            var errs = new List<string>();
            var ussDir = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Gameplay.UI", "Skeuomorphic");
            if (!Directory.Exists(ussDir)) return errs;

            foreach (var ussFile in Directory.GetFiles(ussDir, "*.uss", SearchOption.TopDirectoryOnly))
            {
                if (Path.GetFileName(ussFile) == "SkeuoThemeVariables.uss") continue;
                string text = File.ReadAllText(ussFile);
                string clean = UssCommentRegex.Replace(text, "");
                var lines = clean.Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    if (HardcodedColorRegex.IsMatch(lines[i]))
                        errs.Add($"[C4] {Path.GetFileName(ussFile)}:{i + 1} 发现硬编码颜色「{lines[i].Trim()}」—— 应使用主题变量,不得内联模拟变体。");
                }
            }
            return errs;
        }

        private static List<string> ValidateUssHardcodedSizes()
        {
            var errs = new List<string>();
            var ussDir = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Gameplay.UI", "Skeuomorphic");
            if (!Directory.Exists(ussDir)) return errs;

            foreach (var ussFile in Directory.GetFiles(ussDir, "*.uss", SearchOption.TopDirectoryOnly))
            {
                if (Path.GetFileName(ussFile) == "SkeuoThemeVariables.uss") continue;
                string text = File.ReadAllText(ussFile);
                string clean = UssCommentRegex.Replace(text, "");
                var lines = clean.Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    if (HardcodedSizeRegex.IsMatch(lines[i]))
                        errs.Add($"[C4] {Path.GetFileName(ussFile)}:{i + 1} 发现硬编码尺寸「{lines[i].Trim()}」—— 应使用主题变量,不得内联模拟变体。");
                }
            }
            return errs;
        }

        // ═══ AC-42-C1: 九宫格边框值断言 ═══
        // 策略:从 USS 文本提取 -unity-slice-* 值,验证 >0;半尺寸断言(< half-size)需纹理元数据,暂留内容管线。

        private static List<string> ValidateNineSliceBounds()
        {
            var errs = new List<string>();
            var ussDir = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Gameplay.UI", "Skeuomorphic");
            if (!Directory.Exists(ussDir)) return errs;

            foreach (var ussFile in Directory.GetFiles(ussDir, "*.uss", SearchOption.TopDirectoryOnly))
            {
                string text = File.ReadAllText(ussFile);
                string clean = UssCommentRegex.Replace(text, "");
                var sliceMatches = Regex.Matches(clean, @"-unity-slice-(left|right|top|bottom)\s*:\s*(\d+)px", RegexOptions.IgnoreCase);
                if (sliceMatches.Count == 0) continue;

                foreach (Match m in sliceMatches)
                {
                    string side = m.Groups[1].Value.ToLowerInvariant();
                    if (int.TryParse(m.Groups[2].Value, out int val) && val <= 0)
                    {
                        errs.Add($"[C1] {Path.GetFileName(ussFile)} -unity-slice-{side}={val} 须 > 0。");
                    }
                }
            }
            return errs;
        }

        // ═══ AC-42-C5: USS 硬编码字号 / 文本 lint ═══

        private static List<string> ValidateUssHardcodedFontSize()
        {
            var errs = new List<string>();
            var ussDir = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Gameplay.UI", "Skeuomorphic");
            if (!Directory.Exists(ussDir)) return errs;

            foreach (var ussFile in Directory.GetFiles(ussDir, "*.uss", SearchOption.TopDirectoryOnly))
            {
                if (Path.GetFileName(ussFile) == "SkeuoThemeVariables.uss") continue;
                var lines = File.ReadAllLines(ussFile);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (HardcodedFontSizeRegex.IsMatch(lines[i]))
                        errs.Add($"[C5] {Path.GetFileName(ussFile)}:{i + 1} 发现硬编码 px 字号「{lines[i].Trim()}」—— 须引用主题变量。");
                }
            }
            return errs;
        }

        private static List<string> ValidateUssHardcodedText()
        {
            var errs = new List<string>();
            var ussDir = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Gameplay.UI", "Skeuomorphic");
            if (!Directory.Exists(ussDir)) return errs;

            foreach (var ussFile in Directory.GetFiles(ussDir, "*.uss", SearchOption.TopDirectoryOnly))
            {
                if (Path.GetFileName(ussFile) == "SkeuoThemeVariables.uss") continue;
                var lines = File.ReadAllLines(ussFile);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (HardcodedTextRegex.IsMatch(lines[i]))
                        errs.Add($"[C5] {Path.GetFileName(ussFile)}:{i + 1} 发现硬编码文本「{lines[i].Trim()}」—— 文本内容不得内嵌 USS。");
                }
            }
            return errs;
        }

        // ═══ AC-42-C6: fallback 字体位断言 ═══

        private static List<string> ValidateFallbackFonts()
        {
            var errs = new List<string>();
            try
            {
                DaYiJingCheng.Gameplay.UI.Skeuomorphic.FallbackFontRegistry.Get(ChineseFontSlot);
            }
            catch (KeyNotFoundException)
            {
                errs.Add($"[C6] fallback 字体槽位「{ChineseFontSlot}」未登记(中文字体位必须存在)。");
            }
            try
            {
                DaYiJingCheng.Gameplay.UI.Skeuomorphic.FallbackFontRegistry.Get(EnglishFontSlot);
            }
            catch (KeyNotFoundException)
            {
                errs.Add($"[C6] fallback 字体槽位「{EnglishFontSlot}」未登记(英文字体位必须存在)。");
            }
            return errs;
        }
    }
}
#endif
