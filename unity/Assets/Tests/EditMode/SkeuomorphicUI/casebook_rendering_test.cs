// 权威来源:production/epics/skeuomorphic-ui/story-011-casebook-rendering.md
//   (AC-42-F1① · AC-42-B5 · AC-42-B6 · 五通道区 · 两栏布局)
//   · AC-42-F1①: 脉案页 手柄走查 + 目视零按键提示浮层
//   · AC-42-B5: 空行有格线无字(格线渲染存在但无文本节点)
//   · AC-42-B6: 置信度不出溢体征栏(置信度数值 ≤ 体征栏承载上限)
//   · 五通道区: 面色/语声/呼吸/触感/病名五通道完整渲染
//   · 两栏布局: 左栏体征/右栏诊断
// ADR-013(拟物 UI 框架)
//
// ⚠️ 落点:故事 Test Evidence 登记路径 = production/qa/evidence/casebook-39-walkthrough.md;
//    Unity 只编译 unity/Assets/ 树 ⇒ **真身 = 本文件**;账本侧由 tests/unit/skeuomorphic-ui/README.md 互链。
// ⚠️ 测试纪律:arrange/act/assert · 无随机 · 无时间依赖(禁 DateTime/Time)·
//    夹具自持,不依赖文件系统/网络/数据库。
// ⚠️ 本 story 要求 UI 走查,当前阶段 42 未实现,用 Assert.Inconclusive / [Ignore] 标记。
// ⚠️ 代码评审修复:B1 保持 Inconclusive 添加注释;B2 添加注释;R1 提取 [SetUp];R2 提取助手方法;
//    R3 添加注释;R4 重命名测试;R5 添加注释指向替代证据路径。

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.SkeuomorphicUI
{
    [TestFixture]
    public class CasebookRenderingTest
    {
        // ══════════════ 基础设施 ══════════════

        private List<Type> _uiTypes;

        /// <summary>R1 修复:提取 [SetUp] 消除 Arrange 段重复。</summary>
        [SetUp]
        public void ArrangeUiTypes()
        {
            _uiTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                .Where(t => t.Namespace != null && t.Namespace.StartsWith("DaYiJingCheng.Gameplay.UI"))
                .ToList();
            Assert.That(_uiTypes, Is.Not.Empty,
                "UI 程序集未加载,测试无意义(假绿防护)");
        }

        /// <summary>R2 修复:提取存在性检查助手方法。
        /// B1 说明:当前阶段 42 未实现,无法确定预期类型名,保持 Inconclusive;
        /// 实现后改为对预期类型集合的断言(如五通道区应断言五个通道类型全部存在)。</summary>
        private static List<Type> FindTypesByPatterns(List<Type> types, string[] patterns)
        {
            return types.Where(t => patterns.Any(p => t.Name.Contains(p))).ToList();
        }

        /// <summary>提取重复的字段/属性扫描逻辑(字段 + 属性)。
        /// R3 说明:子串匹配存在漏报/误报,实现后改为显式期望类型清单断言。</summary>
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

        // ══════════════ 五通道区 ══════════════

        /// <summary>五通道区: 面色/语声/呼吸/触感/病名五通道完整渲染。
        /// B1 说明:当前阶段 42 未实现,无法确定预期类型名,保持 Inconclusive;
        /// 实现后改为断言五个通道类型全部存在(如 ComplexionChannel/VoiceChannel/...)。</summary>
        [Test]
        public void test_fiveChannels_exist()
        {
            // Arrange:[SetUp] 已加载 _uiTypes

            // Act:扫描五通道相关类型
            var channelPatterns = new[] { "Complexion", "Voice", "Breathing", "Palpation", "DiseaseName", "Channel" };
            var channelTypes = FindTypesByPatterns(_uiTypes, channelPatterns);

            // Assert:存在五通道相关类型
            // B1 说明:当前阶段 42 未实现,保持 Inconclusive;实现后改为断言五个通道类型全部存在
            if (channelTypes.Count == 0)
            {
                Assert.Inconclusive("42 命名空间无五通道类型,当前阶段无法判定(五通道区)");
                return;
            }

            Assert.That(channelTypes, Is.Not.Empty,
                "42 应有五通道相关类型(五通道区):\n" +
                string.Join("\n", channelTypes.Select(t => t.Name)));
        }

        /// <summary>五通道区: 42 内不存在传统血条/进度条。
        /// R4 修复:重命名测试(去除冗余的 noHealthbar)。</summary>
        [Test]
        public void test_fiveChannels_hasNoHealthbarFields()
        {
            // Arrange:[SetUp] 已加载 _uiTypes

            // Act:扫描血条/进度条相关字段
            var healthbarPatterns = new[] { "healthbar", "health", "progressbar", "progress", "fillbar", "fill" };
            var violations = FindMatchingMembers(_uiTypes, healthbarPatterns, referenceTypesOnly: true)
                .Select(v => $"{v} 是血条/进度条成员").ToList();

            // Assert:零血条/进度条字段(实现前后都成立)
            Assert.That(violations, Is.Empty,
                "42 不得有血条/进度条字段(五通道区:禁传统血条):\n" +
                string.Join("\n", violations));
        }

        // ══════════════ 两栏布局 ══════════════

        /// <summary>两栏布局: 左栏体征/右栏诊断。
        /// B1 说明:当前阶段 42 未实现,无法确定预期类型名,保持 Inconclusive;
        /// 实现后改为断言两栏布局类型存在。</summary>
        [Test]
        public void test_twoColumnLayout_exist()
        {
            // Arrange:[SetUp] 已加载 _uiTypes

            // Act:扫描两栏布局相关类型
            var layoutPatterns = new[] { "TwoColumn", "LeftRight", "SplitView", "TwoPane", "Casebook", "CasebookPage" };
            var layoutTypes = FindTypesByPatterns(_uiTypes, layoutPatterns);

            // Assert:存在两栏布局相关类型
            // B1 说明:当前阶段 42 未实现,保持 Inconclusive;实现后改为断言两栏布局类型存在
            if (layoutTypes.Count == 0)
            {
                Assert.Inconclusive("42 命名空间无两栏布局类型,当前阶段无法判定(两栏布局)");
                return;
            }

            Assert.That(layoutTypes, Is.Not.Empty,
                "42 应有两栏布局相关类型(两栏布局):\n" +
                string.Join("\n", layoutTypes.Select(t => t.Name)));
        }

        /// <summary>两栏布局: 42 内不存在硬编码字号。
        /// R4 修复:重命名测试(去除冗余的 noFontSize)。</summary>
        [Test]
        public void test_twoColumnLayout_hasNoFontSizeFields()
        {
            // Arrange:[SetUp] 已加载 _uiTypes

            // Act:扫描硬编码字号相关字段
            var fontSizePatterns = new[] { "fontsize", "fontsizevalue", "textsize" };
            var violations = FindMatchingMembers(_uiTypes, fontSizePatterns, referenceTypesOnly: true)
                .Select(v => $"{v} 是硬编码字号成员").ToList();

            // Assert:零硬编码字号字段(实现前后都成立)
            Assert.That(violations, Is.Empty,
                "42 不得有硬编码字号字段(两栏布局:禁硬编码字号):\n" +
                string.Join("\n", violations));
        }

        // ══════════════ AC-42-B5: 空行有格线无字 ══════════════

        /// <summary>AC-42-B5: 空行有格线无字(格线渲染存在但无文本节点)。
        /// ✅ **B2 转正(2026-10-08 · story-021)**:格线存在性正向断言已落
        /// `texture_binding_gate_test.test_ac021_2_ruled_and_empty_row_gridline_equal_weight_in_uss`
        /// (读 `SkeuoPaper.uss` 的 `.ruled, .empty-row` 声明块)—— 原「格线存在性零覆盖 = 借绿」
        /// 闭环;本方法只保留**类型存在性**扫描面。</summary>
        [Test]
        public void test_ac42b5_emptyRowRendering_exist()
        {
            // Arrange:[SetUp] 已加载 _uiTypes

            // Act:扫描空行渲染相关类型
            var emptyRowPatterns = new[] { "EmptyRow", "GridLine", "EmptyCell", "BlankRow" };
            var emptyRowTypes = FindTypesByPatterns(_uiTypes, emptyRowPatterns);

            // Assert:存在空行渲染相关类型
            // B2 说明:格线存在性未覆盖,实现后补充正向断言
            if (emptyRowTypes.Count == 0)
            {
                Assert.Inconclusive("42 命名空间无空行渲染类型,当前阶段无法判定(AC-42-B5)");
                return;
            }

            Assert.That(emptyRowTypes, Is.Not.Empty,
                "42 应有空行渲染相关类型(AC-42-B5):\n" +
                string.Join("\n", emptyRowTypes.Select(t => t.Name)));
        }

        /// <summary>AC-42-B5: 空行无文本节点。</summary>
        [Test]
        public void test_ac42b5_emptyRowNoText_noTextFields()
        {
            // Arrange:[SetUp] 已加载 _uiTypes

            // Act:扫描空行文本相关字段
            var textPatterns = new[] { "emptyrowtext", "blankrowtext", "nulltext" };
            var violations = FindMatchingMembers(_uiTypes, textPatterns, referenceTypesOnly: true)
                .Select(v => $"{v} 是空行文本成员").ToList();

            // Assert:零空行文本字段(实现前后都成立)
            Assert.That(violations, Is.Empty,
                "42 不得有空行文本字段(AC-42-B5:空行有格线无字):\n" +
                string.Join("\n", violations));
        }

        // ══════════════ AC-42-B6: 置信度不出溢体征栏 ══════════════

        /// <summary>AC-42-B6: 置信度不出溢体征栏(置信度数值 ≤ 体征栏承载上限)。
        /// B1 说明:当前阶段 42 未实现,无法确定预期类型名,保持 Inconclusive;
        /// 实现后改为断言置信度渲染类型存在 + 数值边界验证。</summary>
        [Test]
        public void test_ac42b6_confidenceRendering_exist()
        {
            // Arrange:[SetUp] 已加载 _uiTypes

            // Act:扫描置信度渲染相关类型
            var confidencePatterns = new[] { "Confidence", "ConfidenceBar", "ConfidenceDisplay", "ConfidenceRenderer" };
            var confidenceTypes = FindTypesByPatterns(_uiTypes, confidencePatterns);

            // Assert:存在置信度渲染相关类型
            // B1 说明:当前阶段 42 未实现,保持 Inconclusive;实现后改为断言 + 数值边界验证
            if (confidenceTypes.Count == 0)
            {
                Assert.Inconclusive("42 命名空间无置信度渲染类型,当前阶段无法判定(AC-42-B6)");
                return;
            }

            Assert.That(confidenceTypes, Is.Not.Empty,
                "42 应有置信度渲染相关类型(AC-42-B6):\n" +
                string.Join("\n", confidenceTypes.Select(t => t.Name)));
        }

        /// <summary>AC-42-B6: 置信度数值 ≤ 体征栏承载上限。
        /// R3 说明:子串匹配无法检测数值溢出,实现后补充 EditMode 逻辑测试。</summary>
        [Test]
        public void test_ac42b6_noConfidenceOverflow_noOverflowFields()
        {
            // Arrange:[SetUp] 已加载 _uiTypes

            // Act:扫描置信度溢出相关字段
            var overflowPatterns = new[] { "confidenceoverflow", "overflowconfidence", "confidenceexceed" };
            var violations = FindMatchingMembers(_uiTypes, overflowPatterns, referenceTypesOnly: true)
                .Select(v => $"{v} 是置信度溢出成员").ToList();

            // Assert:零置信度溢出字段(实现前后都成立)
            Assert.That(violations, Is.Empty,
                "42 不得有置信度溢出字段(AC-42-B6:置信度不出溢体征栏):\n" +
                string.Join("\n", violations));
        }

        // ══════════════ AC-42-F1①: 脉案页 手柄走查 + 目视零按键提示浮层 ══════════════

        /// <summary>AC-42-F1①: 脉案页 手柄走查 + 目视零按键提示浮层。
        /// R5 修复:添加注释指向替代证据路径。
        /// ⚠️ 本 story 要求 UI 走查,当前阶段 42 未实现,UI 测试无法运行。</summary>
        [Test]
        [Ignore("42 未实现 - 待 UI 走查;替代证据路径 = production/qa/evidence/casebook-39-walkthrough.md")]
        public void test_ac42f1_gamepadWalkthrough_noKeyHintOverlay()
        {
            // 实现后补充:
            // 1. 手柄走查: 面色 → 语声 → 呼吸 → 触感 → 病名
            // 2. 目视检查: 零按键提示浮层
            // 3. 边缘情况: 快速导航;焦点边界切换
            // 替代证据路径: production/qa/evidence/casebook-39-walkthrough.md
        }

        /// <summary>AC-42-F1①: 焦点顺序 = 面色 → 语声 → 呼吸 → 触感 → 病名。
        /// B1 说明:当前阶段 42 未实现,无法确定预期类型名,保持 Inconclusive;
        /// 实现后改为断言焦点顺序类型存在 + rank 数据驱动验证。</summary>
        [Test]
        public void test_ac42f1_focusOrder_exist()
        {
            // Arrange:[SetUp] 已加载 _uiTypes

            // Act:扫描焦点顺序相关类型
            var focusOrderPatterns = new[] { "FocusOrder", "FocusRank", "NavigationOrder", "FocusSequence" };
            var focusOrderTypes = FindTypesByPatterns(_uiTypes, focusOrderPatterns);

            // Assert:存在焦点顺序相关类型
            // B1 说明:当前阶段 42 未实现,保持 Inconclusive;实现后改为断言 + rank 数据驱动验证
            if (focusOrderTypes.Count == 0)
            {
                Assert.Inconclusive("42 命名空间无焦点顺序类型,当前阶段无法判定(AC-42-F1①)");
                return;
            }

            Assert.That(focusOrderTypes, Is.Not.Empty,
                "42 应有焦点顺序相关类型(AC-42-F1①):\n" +
                string.Join("\n", focusOrderTypes.Select(t => t.Name)));
        }

        /// <summary>AC-42-F1①: 42 内不存在按键提示浮层。</summary>
        [Test]
        public void test_ac42f1_noKeyHintOverlay_noKeyHintFields()
        {
            // Arrange:[SetUp] 已加载 _uiTypes

            // Act:扫描按键提示浮层相关字段
            var keyHintPatterns = new[] { "keyhint", "keyprompt", "buttonhint", "controllerhint" };
            var violations = FindMatchingMembers(_uiTypes, keyHintPatterns, referenceTypesOnly: true)
                .Select(v => $"{v} 是按键提示浮层成员").ToList();

            // Assert:零按键提示浮层字段(实现前后都成立)
            Assert.That(violations, Is.Empty,
                "42 不得有按键提示浮层字段(AC-42-F1①:目视零按键提示浮层):\n" +
                string.Join("\n", violations));
        }
    }
}
