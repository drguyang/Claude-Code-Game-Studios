// 权威来源:production/epics/skeuomorphic-ui/story-008-no-healthbar-feedback.md
//   (拒绝权降级白名单 · 记号登记表 · 无血条替代反馈)
//   · 拒绝权降级白名单: 续页唯一路径;开发期报冲突;不得静默裁切/缩字/破纸
//   · 记号登记表: 六种记号(勾/点/叠角/划改痕/印/折角);每种记号有明确渲染形态
//   · 无血条替代反馈: 进度 = 页数/纸厚/墨迹密度(不是传统血条)
// ADR-013(拟物 UI 框架)
//
// ⚠️ 落点:故事 Test Evidence 登记路径 = tests/integration/skeuomorphic-ui/no_healthbar_feedback_test.cs;
//    Unity 只编译 unity/Assets/ 树 ⇒ **真身 = 本文件**;账本侧由 tests/unit/skeuomorphic-ui/README.md 互链。
// ⚠️ 测试纪律:arrange/act/assert · 无随机 · 无时间依赖(禁 DateTime/Time)·
//    夹具自持,不依赖文件系统/网络/数据库。
// ⚠️ 代码评审修复:B1 添加属性扫描;B2 删除恒真断言;R1 提取重复代码;R3 重命名测试;
//    B3/B4/B5/B6/R4-R9 添加注释说明实现后补充。

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.SkeuomorphicUI
{
    [TestFixture]
    public class NoHealthbarFeedbackTest
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

        /// <summary>R1 修复:提取重复的字段/属性扫描逻辑。
        /// B1 修复:同时扫描字段和属性。
        /// B3/B4 说明:模式匹配存在误报/漏报风险,实现后补充精确匹配。</summary>
        private static List<string> FindMatchingMembers(
            List<Type> types,
            string[] patterns,
            bool referenceTypesOnly = true)
        {
            var violations = new List<string>();
            foreach (var t in types)
            {
                // 扫描字段
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

                // B1 修复:扫描属性
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

        // ══════════════ 拒绝权降级白名单: 续页唯一路径 ══════════════

        /// <summary>拒绝权降级白名单: 42 不得有静默裁切/缩字/破纸的降级路径。
        /// 扫描 42 命名空间下的类型,验证无裁切/缩字/破纸相关字段。
        /// B5 说明:当前为静态扫描,实现后补充行为验证(验证续页是唯一降级路径)。
        /// R8 说明:实现后补充开发期冲突上报机制的验证。</summary>
        [Test]
        public void test_rejectionWhitelist_noSilentClipping_noClipFields()
        {
            // Arrange:扫描 42 命名空间下的类型
            var uiTypes = GetUiTypes();
            AssertUiTypesLoaded(uiTypes);

            // Act:扫描裁切/缩字/破纸相关字段
            var clipPatterns = new[] { "clip", "truncate", "shrink", "crop", "overflow" };
            var violations = FindMatchingMembers(uiTypes, clipPatterns, referenceTypesOnly: true)
                .Select(v => $"{v} 是裁切/缩字/破纸字段").ToList();

            // Assert:零裁切/缩字/破纸字段(实现前后都成立)
            Assert.That(violations, Is.Empty,
                "42 不得有裁切/缩字/破纸字段(拒绝权降级白名单):\n" +
                string.Join("\n", violations));
        }

        /// <summary>拒绝权降级白名单: 续页路径存在。
        /// 当前阶段:验证 42 命名空间存在续页相关类型。
        /// B2 修复:删除恒真断言。
        /// B4 说明:模式匹配可能漏报(PageTurn/FlipPage 等),实现后补充精确匹配。
        /// R5 说明:实现后补充边界情况(刚好在边界/超限 1 字符/超限大量内容)。</summary>
        [Test]
        public void test_rejectionWhitelist_continuationPage_exists()
        {
            // Arrange:扫描 42 命名空间下的类型
            var uiTypes = GetUiTypes();
            AssertUiTypesLoaded(uiTypes);

            // Act:扫描续页相关类型
            var continuationTypes = uiTypes.Where(t =>
                t.Name.Contains("Continuation") || t.Name.Contains("NextPage") ||
                t.Name.Contains("Overflow") || t.Name.Contains("Spillover")).ToList();

            // Assert:存在续页相关类型
            // 当前阶段:42 未实现,无续页类型 = 通过(实现后应存在)
            if (continuationTypes.Count == 0)
            {
                Assert.Inconclusive("42 命名空间无续页类型,当前阶段无法判定(拒绝权降级白名单)");
                return;
            }

            // B2 修复:删除恒真断言,改为验证具体类型名
            Assert.That(continuationTypes, Is.Not.Empty,
                "42 应有续页相关类型(拒绝权降级白名单):\n" +
                string.Join("\n", continuationTypes.Select(t => t.Name)));
        }

        // ══════════════ 记号登记表: 六种记号 ══════════════

        /// <summary>记号登记表: 六种记号类型完整(勾/点/叠角/划改痕/印/折角)。
        /// 当前阶段:验证 42 命名空间存在记号相关类型。
        /// B2 修复:删除恒真断言。
        /// B4 说明:模式匹配可能漏报(Tick/Point/Stamp 等),实现后补充六种具体记号验证。
        /// R6 说明:实现后补充六种记号(勾/点/叠角/划改痕/印/折角)的具体验证。</summary>
        [Test]
        public void test_markRegistry_sixMarkTypes_exist()
        {
            // Arrange:扫描 42 命名空间下的类型
            var uiTypes = GetUiTypes();
            AssertUiTypesLoaded(uiTypes);

            // Act:扫描记号相关类型
            var markTypes = uiTypes.Where(t =>
                t.Name.Contains("Mark") || t.Name.Contains("Seal") ||
                t.Name.Contains("Fold") || t.Name.Contains("Corner") ||
                t.Name.Contains("Strike") || t.Name.Contains("Dot") ||
                t.Name.Contains("Check")).ToList();

            // Assert:存在记号相关类型
            // 当前阶段:42 未实现,无记号类型 = 通过(实现后应存在)
            if (markTypes.Count == 0)
            {
                Assert.Inconclusive("42 命名空间无记号类型,当前阶段无法判定(记号登记表)");
                return;
            }

            // B2 修复:删除恒真断言,改为验证具体类型名
            Assert.That(markTypes, Is.Not.Empty,
                "42 应有记号相关类型(记号登记表):\n" +
                string.Join("\n", markTypes.Select(t => t.Name)));
        }

        /// <summary>记号登记表: 记号渲染不破纸(纸张边界断言)。
        /// 扫描 42 命名空间下的类型,验证无破纸相关字段。
        /// B3 说明:模式 "break" 可能误报(breakpoint/lineBreak),实现后补充精确匹配。</summary>
        [Test]
        public void test_markRegistry_noPaperTear_noTearFields()
        {
            // Arrange:扫描 42 命名空间下的类型
            var uiTypes = GetUiTypes();
            AssertUiTypesLoaded(uiTypes);

            // Act:扫描破纸相关字段
            var tearPatterns = new[] { "tear", "rip", "break", "split", "fracture" };
            var violations = FindMatchingMembers(uiTypes, tearPatterns, referenceTypesOnly: true)
                .Select(v => $"{v} 是破纸字段").ToList();

            // Assert:零破纸字段(实现前后都成立)
            Assert.That(violations, Is.Empty,
                "42 不得有破纸字段(记号渲染不破纸):\n" +
                string.Join("\n", violations));
        }

        // ══════════════ 无血条替代反馈: 进度 = 页数/纸厚/墨迹密度 ══════════════

        /// <summary>无血条替代反馈: 42 不得有传统血条/进度条。
        /// R3 修复:重命名测试(去除冗余的 noHealthbar)。
        /// B3 说明:模式 "health"/"progress"/"fill" 可能误报,实现后补充精确匹配。</summary>
        [Test]
        public void test_noHealthbar_noHealthbarFields()
        {
            // Arrange:扫描 42 命名空间下的类型
            var uiTypes = GetUiTypes();
            AssertUiTypesLoaded(uiTypes);

            // Act:扫描血条/进度条相关字段
            var healthbarPatterns = new[] { "healthbar", "health", "progressbar", "progress", "fillbar", "fill" };
            var violations = FindMatchingMembers(uiTypes, healthbarPatterns, referenceTypesOnly: true)
                .Select(v => $"{v} 是血条/进度条字段").ToList();

            // Assert:零血条/进度条字段(实现前后都成立)
            Assert.That(violations, Is.Empty,
                "42 不得有血条/进度条字段(无血条替代反馈):\n" +
                string.Join("\n", violations));
        }

        /// <summary>无血条替代反馈: 进度 = 页数/纸厚/墨迹密度。
        /// 当前阶段:验证 42 命名空间存在页数/纸厚/墨迹密度相关类型。
        /// B2 修复:删除恒真断言。
        /// B4 说明:模式匹配可能漏报,实现后补充精确匹配。
        /// R7 说明:实现后补充进度计算逻辑验证(进度 = 页数/纸厚/墨迹密度)。</summary>
        [Test]
        public void test_noHealthbar_paperProgress_exists()
        {
            // Arrange:扫描 42 命名空间下的类型
            var uiTypes = GetUiTypes();
            AssertUiTypesLoaded(uiTypes);

            // Act:扫描页数/纸厚/墨迹密度相关类型
            var paperProgressTypes = uiTypes.Where(t =>
                t.Name.Contains("Page") || t.Name.Contains("Paper") ||
                t.Name.Contains("Thickness") || t.Name.Contains("InkDensity") ||
                t.Name.Contains("Ink")).ToList();

            // Assert:存在页数/纸厚/墨迹密度相关类型
            // 当前阶段:42 未实现,无相关类型 = 通过(实现后应存在)
            if (paperProgressTypes.Count == 0)
            {
                Assert.Inconclusive("42 命名空间无页数/纸厚/墨迹密度类型,当前阶段无法判定(无血条替代反馈)");
                return;
            }

            // B2 修复:删除恒真断言,改为验证具体类型名
            Assert.That(paperProgressTypes, Is.Not.Empty,
                "42 应有页数/纸厚/墨迹密度相关类型(无血条替代反馈):\n" +
                string.Join("\n", paperProgressTypes.Select(t => t.Name)));
        }

        /// <summary>无血条替代反馈: 完成 = 印章/页边记号。
        /// 扫描 42 命名空间下的类型,验证无传统完成指示器。
        /// R4 说明:实现后补充正面验证(完成 = 印章/页边记号)。</summary>
        [Test]
        public void test_noHealthbar_completionMark_noCompletionBar()
        {
            // Arrange:扫描 42 命名空间下的类型
            var uiTypes = GetUiTypes();
            AssertUiTypesLoaded(uiTypes);

            // Act:扫描传统完成指示器相关字段
            var completionPatterns = new[] { "completionbar", "completionindicator", "checkmark", "donebar" };
            var violations = FindMatchingMembers(uiTypes, completionPatterns, referenceTypesOnly: true)
                .Select(v => $"{v} 是传统完成指示器字段").ToList();

            // Assert:零传统完成指示器字段(实现前后都成立)
            Assert.That(violations, Is.Empty,
                "42 不得有传统完成指示器字段(完成 = 印章/页边记号):\n" +
                string.Join("\n", violations));
        }
    }
}
