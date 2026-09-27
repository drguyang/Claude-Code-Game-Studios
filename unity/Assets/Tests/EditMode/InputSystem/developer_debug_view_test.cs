// Story 012 · 开发者调试视图与构建剥离(AC-3-E2 · AC-3-UI-2)
//
// 权威来源:
//   GDD input-system.md §UI Requirements 二 · AC-3-E2 · AC-3-UI-2
//   ADR-013 §9 C3(不读焦点栈) · ADR-019 §五(开发者切面)
//   Story 012 设计文档:production/epics/input-system/story-012-developer-debug-view.md
//
// 测试策略(Visual/Feel 类型):
//   E2① 源侧断言(条件编译门) —— 读源码文本面断言(#if 包裹存在)
//   E2② 符号断言(player-symbol-check 工具本体自带;CI job 挂账另轮)
//   UI-2 三条件 = ② 的运行面 + ③ 内容断言(读源码) + ④ 不读焦点栈(读源码)
//
// 条件:
//   - 本测试为 EditMode 纯逻辑测试(读源码文本,不加载 player)
//   - IL2CPP/Mono 符号扫描 = player-symbol-check 工具的责任(本文件不重复)
//   - 运行期 OnGUI 行为 = 视觉/手感,挂 ADVISORY,本文件不自动化

using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace DaYiJingCheng.Tests.Unit.InputSystem
{
    /// <summary>Story 012 开发者调试视图与构建剥离(AC-3-E2 · AC-3-UI-2)的 EditMode 测试。</summary>
    [TestFixture]
    internal sealed class DeveloperDebugViewTest
    {
        private const string DebugViewPath = "Assets/Gameplay.Input/InputDebugView.cs";

        // ══════════ AC-3-E2① · 条件编译门(源侧断言) ═══════════

        /// <summary>E2① 端到端:InputDebugView.cs 存在且包含 #if UNITY_EDITOR || DEVELOPMENT_BUILD 包裹。</summary>
        [Test]
        public void test_e2a_conditional_compile_wrapper_exists()
        {
            string fullPath = Path.Combine(Application.dataPath, "..", DebugViewPath).Replace("\\", "/");
            fullPath = Path.GetFullPath(fullPath);
            Assert.That(File.Exists(fullPath), Is.True,
                $"InputDebugView.cs 不存在于 {DebugViewPath}");

            string src = File.ReadAllText(fullPath);
            Assert.That(src, Does.Contain("#if UNITY_EDITOR"),
                "E2①:源文件必须含 #if UNITY_EDITOR 条件编译块");
            Assert.That(src, Does.Contain("DEVELOPMENT_BUILD"),
                "E2①:源文件必须含 DEVELOPMENT_BUILD 条件编译块");
        }

        /// <summary>E2① 类整体被条件编译包裹:整个 InputDebugView 类型(含命名空间)在 #if 块内。</summary>
        [Test]
        public void test_e2a_class_wrapped_in_conditional_block()
        {
            string src = GetSource(DebugViewPath);

            // 验证源码包含组合条件编译块(#if UNITY_EDITOR || DEVELOPMENT_BUILD)
            var ifMatch = Regex.Match(src,
                @"#if\s+(UNITY_EDITOR\s*\|\|\s*DEVELOPMENT_BUILD)");
            Assert.That(ifMatch.Success, Is.True,
                "E2①:源文件必须含 #if UNITY_EDITOR || DEVELOPMENT_BUILD 包裹");

            // 验证 public sealed class InputDebugView 出现在该块内
            int ifPos = ifMatch.Index;
            int classPos = src.IndexOf("public sealed class InputDebugView");
            Assert.That(classPos, Is.GreaterThan(ifPos),
                "E2①:InputDebugView 类定义必须在 #if 条件编译块内(Release 构建零代码路径)");
        }

        /// <summary>E2① OnGUI 方法体被条件编译包裹(玩家构建中零代码路径)。</summary>
        [Test]
        public void test_e2a_ongui_method_body_conditionally_compiled()
        {
            string src = GetSource(DebugViewPath);

            // 定位 OnGUI 方法
            var onGuiMatch = Regex.Match(src, @"private void OnGUI\(\)\s*\{");
            Assert.That(onGuiMatch.Success, Is.True,
                "E2①:InputDebugView 必须含 OnGUI() 方法");

            // OnGUI 方法体内不得出现 #if(类已包裹,方法体本身不再重复包裹)
            int pos = onGuiMatch.Index + onGuiMatch.Length;
            string afterDecl = src.Substring(pos);

            var ifMatch = Regex.Match(afterDecl, @"#if\s+(UNITY_EDITOR\s*\|\|\s*DEVELOPMENT_BUILD)");
            Assert.That(ifMatch.Success, Is.False,
                "E2①:OnGUI 方法体内不应再出现条件编译块(类级包裹已覆盖)");
        }

        /// <summary>E2① 玩家构建路径不存在:OnGUI 内无 #if 时内容应被完全跳过。
        /// 本测试验证「#if 块内有实质内容」—— 防止写空壳 #if 满足 grep 但不保护玩家构建。</summary>
        [Test]
        public void test_e2a_conditional_block_has_substantive_content()
        {
            string src = GetSource(DebugViewPath);

            // 提取 #if UNITY_EDITOR || DEVELOPMENT_BUILD 块内的内容
            var blockMatch = Regex.Match(src,
                @"#if\s+(UNITY_EDITOR\s*\|\|\s*DEVELOPMENT_BUILD)(.*?)#endif",
                RegexOptions.Singleline);

            Assert.That(blockMatch.Success, Is.True,
                "E2①:必须含完整的 #if...#endif 块");
            string block = blockMatch.Groups[2].Value;

            // 块内须包含实际绘制代码(不能是空壳 #if)
            Assert.That(block.Contains("GUI.Label") || block.Contains("GUI.Box"),
                Is.True,
                "E2①:#if 块内必须包含实际绘制代码(不能是空壳注释)");
        }

        // ══════════ AC-3-UI-2 三条件 ═══════════

        /// <summary>UI-2 条件二(运行面):输入调试视图类型存在且为 sealed class。</summary>
        [Test]
        public void test_ui2_debug_view_type_exists_and_sealed()
        {
            string src = GetSource(DebugViewPath);

            var classMatch = Regex.Match(src,
                @"public\s+sealed\s+class\s+InputDebugView\s*:");
            Assert.That(classMatch.Success, Is.True,
                "UI-2:InputDebugView 必须是 public sealed class(MonoBehaviour 子类)");
        }

        /// <summary>UI-2 条件三(内容断言):调试视图不显示 raw 轴值数值 —— 源码内无 rawAxis / ReadValue<float>
        /// 出现在显示字符串中。</summary>
        [Test]
        public void test_ui2_no_raw_axis_values_in_display_strings()
        {
            string src = GetSource(DebugViewPath);

            // 排除注释行后搜索 raw axis 值暴露
            string noComments = Regex.Replace(src, @"//.*$", "", RegexOptions.Multiline);
            noComments = Regex.Replace(noComments, @"/\*.*?\*/", "", RegexOptions.Singleline);

            Assert.That(noComments.Contains("ReadValue<float>"), Is.False,
                "UI-2:调试视图源码不得直接显示 ReadValue<float> 原始值");
            Assert.That(noComments.Contains("rawAxis"), Is.False,
                "UI-2:调试视图源码不得显示 rawAxis 数值字段");
            Assert.That(noComments.Contains("ReadValue<Vector2>"), Is.False,
                "UI-2:调试视图源码不得直接显示 Vector2 原始值");
        }

        /// <summary>UI-2 条件四(不读焦点栈):输入调试视图不引用焦点栈相关类型。</summary>
        [Test]
        public void test_ui2_debug_view_does_not_reference_focus_stack()
        {
            string src = GetSource(DebugViewPath);

            // 排除注释行
            string noComments = Regex.Replace(src, @"//.*$", "", RegexOptions.Multiline);
            noComments = Regex.Replace(noComments, @"/\*.*?\*/", "", RegexOptions.Singleline);

            // 禁止引用焦点控制器 / 焦点栈类型(ADR-013 §9 C3)
            Assert.That(noComments.Contains("FocusController"), Is.False,
                "UI-2:InputDebugView 不得引用 FocusController(焦点栈不归 3 读)");
            Assert.That(noComments.Contains("FocusGroup"), Is.False,
                "UI-2:InputDebugView 不得引用 FocusGroup(焦点栈不归 3 读)");
            Assert.That(noComments.Contains("EventSystem.current"), Is.False,
                "UI-2:InputDebugView 不得引用 EventSystem.current(焦点栈不归 3 读)");
        }

        // ══════════ 工具函数 ═══════════

        /// <summary>读取 Assets/ 下的源码文件(绝对路径解析)。</summary>
        private static string GetSource(string assetsRelativePath)
        {
            string fullPath = Path.Combine(Application.dataPath, "..", assetsRelativePath);
            fullPath = Path.GetFullPath(fullPath);
            Assert.That(File.Exists(fullPath), Is.True, $"源码文件不存在: {fullPath}");
            return File.ReadAllText(fullPath);
        }
    }
}
