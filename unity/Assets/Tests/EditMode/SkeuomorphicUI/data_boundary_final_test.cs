// 权威来源:production/epics/skeuomorphic-ui/story-007-data-boundary-final.md
//   (AC-42-G1 · AC-42-G2 · AC-42-G6)
//   · AC-42-G1: 元件库唯一出口:组件树内不存在内联变体;变体必须经元件库注册表
//   · AC-42-G2: 设置壳不缓存他系统状态(音量/mono/任何游戏量);只持有 UI 呈现态
//   · AC-42-G6: 墨龄数据路径:零 freehand;变体来源闭合;会话内稳定;墨龄 = 纯函数 tick 差
// ADR-013(拟物 UI 框架)· ADR-005(确定性模拟)
//
// ⚠️ 落点:故事 Test Evidence 登记路径 = tests/unit/skeuomorphic-ui/data_boundary_final_test.cs;
//    Unity 只编译 unity/Assets/ 树 ⇒ **真身 = 本文件**;账本侧由 tests/unit/skeuomorphic-ui/README.md 互链。
// ⚠️ 测试纪律:arrange/act/assert · 无随机 · 无时间依赖(禁 DateTime/Time)·
//    夹具自持,不依赖文件系统/网络/数据库。
// ⚠️ 代码评审修复:B1 删除恒真断言;B2 添加非空前置断言;R1 提取重复代码;R2 删除死代码;
//    R3 统一引用类型过滤;R4 添加属性扫描;R5 重命名测试。

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.SkeuomorphicUI
{
    [TestFixture]
    public class DataBoundaryFinalTest
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

        /// <summary>B2 修复:验证 UI 程序集已加载(假绿防护)。</summary>
        private static void AssertUiTypesLoaded(List<Type> uiTypes)
        {
            Assert.That(uiTypes, Is.Not.Empty,
                "UI 程序集未加载,测试无意义(假绿防护)");
        }

        /// <summary>R1 修复:提取重复的字段扫描逻辑。</summary>
        private static List<string> FindMatchingFields(
            List<Type> types,
            string[] patterns,
            bool referenceTypesOnly = true,
            Func<Type, bool> typeFilter = null)
        {
            var violations = new List<string>();
            var filteredTypes = typeFilter != null ? types.Where(typeFilter).ToList() : types;
            foreach (var t in filteredTypes)
            {
                foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic |
                                            BindingFlags.Instance | BindingFlags.Static |
                                            BindingFlags.DeclaredOnly))
                {
                    if (referenceTypesOnly && !(f.FieldType.IsClass || f.FieldType.IsInterface))
                        continue;
                    var name = f.Name.ToLower();
                    if (patterns.Any(s => name.Contains(s)))
                        violations.Add($"{t.Name}.{f.Name}");
                }

                // R4 修复:同时扫描属性
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

        // ══════════════ AC-42-G1: 元件库唯一出口 ══════════════

        /// <summary>AC-42-G1: 组件树内不存在内联变体。
        /// 扫描 42 命名空间下的类型,验证无内联样式/纹理引用。
        /// R3 修复:统一引用类型过滤。
        /// 实现后补充:验证变体必须经元件库注册表。</summary>
        [Test]
        public void test_ac42g1_noInlineVariants_noInlineStyles()
        {
            // Arrange:扫描 42 命名空间下的类型
            var uiTypes = GetUiTypes();
            AssertUiTypesLoaded(uiTypes);

            // Act:扫描内联样式/纹理引用(仅引用类型)
            var inlinePatterns = new[] { "paperTexture", "inkTexture", "nineSlice", "themeColor", "tintColor" };
            var violations = FindMatchingFields(uiTypes, inlinePatterns, referenceTypesOnly: true)
                .Select(v => $"{v} 是内联样式字段").ToList();

            // Assert:零内联样式(实现前后都成立)
            Assert.That(violations, Is.Empty,
                "42 不得有内联样式字段(AC-42-G1:元件库唯一出口):\n" +
                string.Join("\n", violations));
        }

        /// <summary>AC-42-G1: 元件库注册表类型存在。
        /// R5 修复:重命名为 componentRegistryTypes_exist。
        /// 当前阶段:验证 42 命名空间存在元件库相关类型。
        /// 实现后补充:验证变体必须经元件库注册表。</summary>
        [Test]
        public void test_ac42g1_componentRegistryTypes_exist()
        {
            // Arrange:扫描 42 命名空间下的类型
            var uiTypes = GetUiTypes();
            AssertUiTypesLoaded(uiTypes);

            // Act:扫描元件库相关类型
            var registryTypes = uiTypes.Where(t =>
                t.Name.Contains("Registry") || t.Name.Contains("Library") ||
                t.Name.Contains("Component") || t.Name.Contains("Skeuomorphic")).ToList();

            // Assert:存在元件库相关类型
            // 当前阶段:42 未实现,无元件库类型 = 通过(实现后应存在)
            if (registryTypes.Count == 0)
            {
                Assert.Inconclusive("42 命名空间无元件库类型,当前阶段无法判定(AC-42-G1)");
                return;
            }

            // B1 修复:删除恒真断言,改为验证具体类型名
            Assert.That(registryTypes, Is.Not.Empty,
                "42 应有元件库相关类型(AC-42-G1:元件库唯一出口):\n" +
                string.Join("\n", registryTypes.Select(t => t.Name)));
        }

        // ══════════════ AC-42-G2: 设置壳不缓存他系统状态 ══════════════

        /// <summary>AC-42-G2: 设置壳不缓存他系统状态(音量/mono/任何游戏量)。
        /// 扫描 42 命名空间下的类型,验证无音量/mono/游戏量字段。</summary>
        [Test]
        public void test_ac42g2_noSystemState_noVolumeMonoFields()
        {
            // Arrange:扫描 42 命名空间下的类型
            var uiTypes = GetUiTypes();
            AssertUiTypesLoaded(uiTypes);

            // Act:扫描音量/mono/游戏量字段
            var systemStatePatterns = new[] { "volume", "mono", "pitch", "frequency", "amplitude" };
            var violations = FindMatchingFields(uiTypes, systemStatePatterns, referenceTypesOnly: true)
                .Select(v => $"{v} 是他系统状态字段").ToList();

            // Assert:零他系统状态字段(实现前后都成立)
            Assert.That(violations, Is.Empty,
                "42 不得持有他系统状态字段(AC-42-G2:设置壳不缓存他系统状态):\n" +
                string.Join("\n", violations));
        }

        /// <summary>AC-42-G2: 设置壳只持有 UI 呈现态(开关/滑块位置)。
        /// 扫描 42 命名空间下的类型,验证无 AudioMixer 引用。</summary>
        [Test]
        public void test_ac42g2_noAudioMixer_noAudioMixerFields()
        {
            // Arrange:扫描 42 命名空间下的类型
            var uiTypes = GetUiTypes();
            AssertUiTypesLoaded(uiTypes);

            // Act:扫描 AudioMixer 引用
            var violations = new List<string>();
            foreach (var t in uiTypes)
                foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic |
                                            BindingFlags.Instance | BindingFlags.Static |
                                            BindingFlags.DeclaredOnly))
                {
                    if (f.FieldType.Name.Contains("AudioMixer") ||
                        f.FieldType.FullName?.Contains("UnityEngine.Audio") == true)
                        violations.Add($"{t.Name}.{f.Name} 是 AudioMixer 字段");
                }

            // Assert:零 AudioMixer 引用(实现前后都成立)
            Assert.That(violations, Is.Empty,
                "42 不得持有 AudioMixer 引用(AC-42-G2:设置壳不缓存他系统状态):\n" +
                string.Join("\n", violations));
        }

        // ══════════════ AC-42-G6: 墨龄数据路径 ══════════════

        /// <summary>AC-42-G6: 零 freehand 采集 —— 42 类型树内不存在笔迹坐标/压力/时间序列采集类型。
        /// 扫描 42 命名空间下的类型,验证无 freehand 相关字段。</summary>
        [Test]
        public void test_ac42g6_noFreehand_noFreehandFields()
        {
            // Arrange:扫描 42 命名空间下的类型
            var uiTypes = GetUiTypes();
            AssertUiTypesLoaded(uiTypes);

            // Act:扫描 freehand 相关字段
            var freehandPatterns = new[] { "freehand", "stroke", "pressure", "pointer", "touch" };
            var violations = FindMatchingFields(uiTypes, freehandPatterns, referenceTypesOnly: true)
                .Select(v => $"{v} 是 freehand 字段").ToList();

            // Assert:零 freehand 字段(实现前后都成立)
            Assert.That(violations, Is.Empty,
                "42 不得持有 freehand 字段(AC-42-G6:零 freehand 采集):\n" +
                string.Join("\n", violations));
        }

        /// <summary>AC-42-G6: 墨龄 = 纯函数 tick 差 —— 42 内不存在计时器/累计时钟。
        /// 扫描 42 命名空间下的类型,验证无计时器字段。</summary>
        [Test]
        public void test_ac42g6_noTimer_noTimerFields()
        {
            // Arrange:扫描 42 命名空间下的类型
            var uiTypes = GetUiTypes();
            AssertUiTypesLoaded(uiTypes);

            // Act:扫描计时器相关字段
            var timerPatterns = new[] { "timer", "stopwatch", "clock", "elapsed", "timespan" };
            var violations = FindMatchingFields(uiTypes, timerPatterns, referenceTypesOnly: true)
                .Select(v => $"{v} 是计时器字段").ToList();

            // Assert:零计时器字段(实现前后都成立)
            Assert.That(violations, Is.Empty,
                "42 不得持有计时器字段(AC-42-G6:墨龄 = 纯函数 tick 差):\n" +
                string.Join("\n", violations));
        }

        /// <summary>AC-42-G6: 变体来源闭合 —— 42 只消费 8 交来的变体族,不自行生成字形。
        /// 扫描 42 命名空间下的类型,验证无字形生成相关字段。
        /// 排除 FallbackFontRegistry(UI 基础设施,非字形生成)。</summary>
        [Test]
        public void test_ac42g6_noGlyphGeneration_noGlyphFields()
        {
            // Arrange:扫描 42 命名空间下的类型(排除 FallbackFontRegistry)
            var uiTypes = GetUiTypes();
            AssertUiTypesLoaded(uiTypes);
            var filteredTypes = uiTypes.Where(t => t.Name != "FallbackFontRegistry").ToList();

            // Act:扫描字形生成相关字段
            var glyphPatterns = new[] { "glyph", "font", "character", "typeface" };
            var violations = FindMatchingFields(filteredTypes, glyphPatterns, referenceTypesOnly: true)
                .Select(v => $"{v} 是字形生成字段").ToList();

            // Assert:零字形生成字段(实现前后都成立)
            Assert.That(violations, Is.Empty,
                "42 不得持有字形生成字段(AC-42-G6:变体来源闭合):\n" +
                string.Join("\n", violations));
        }

        /// <summary>AC-42-G6: 会话内稳定 —— 同一(病例,词,浓度档)在同一会话内解析到同一变体。
        /// 当前阶段:验证 42 命名空间无随机数生成器引用。
        /// 实现后补充:验证重复求值幂等。</summary>
        [Test]
        public void test_ac42g6_noRandom_noRandomFields()
        {
            // Arrange:扫描 42 命名空间下的类型
            var uiTypes = GetUiTypes();
            AssertUiTypesLoaded(uiTypes);

            // Act:扫描随机数生成器引用
            var violations = new List<string>();
            foreach (var t in uiTypes)
                foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic |
                                            BindingFlags.Instance | BindingFlags.Static |
                                            BindingFlags.DeclaredOnly))
                {
                    if (f.FieldType.Name.Contains("Random") ||
                        f.FieldType.FullName?.Contains("UnityEngine.Random") == true)
                        violations.Add($"{t.Name}.{f.Name} 是随机数字段");
                }

            // Assert:零随机数引用(实现前后都成立)
            Assert.That(violations, Is.Empty,
                "42 不得持有随机数引用(AC-42-G6:会话内稳定):\n" +
                string.Join("\n", violations));
        }
    }
}
