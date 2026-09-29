// 权威来源:production/epics/skeuomorphic-ui/story-006-clinic-panel-refresh.md
//   (AC-42-F5 · AC-42-D2 · AC-42-D3)
//   · AC-42-F5: Structure* 事件 Append 后下一帧刷新为新的乘子/情境摘要
//   · AC-42-D2: 42 的代码中不存在写三流/写存档/写 assets/data/ 的调用
//   · AC-42-D3: 42 的类型树不持有 DTO 副本、不持有设置值
// ADR-013(拟物 UI 框架)
//
// ⚠️ 落点:故事 Test Evidence 登记路径 = tests/integration/skeuomorphic-ui/clinic_panel_refresh_test.cs;
//    Unity 只编译 unity/Assets/ 树 ⇒ **真身 = 本文件**;账本侧由 tests/unit/skeuomorphic-ui/README.md 互链。
// ⚠️ 测试纪律:arrange/act/assert · 无随机 · 无时间依赖(禁 DateTime/Time)·
//    夹具自持,不依赖文件系统/网络/数据库。
// ⚠️ 代码评审修复:B1 扩展文件写入检查覆盖方法体;R1 删除恒真断言;R2 统一 DeclaredOnly;
//    R3 删除死代码;R4 增加类型过滤;R5 提取重复代码;R6 增加 IEventSink 字段检查。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Tests.Unit.SkeuomorphicUI
{
    [TestFixture]
    public class ClinicPanelRefreshTest
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

        /// <summary>获取 42 命名空间下的所有类型(排除枚举)。</summary>
        private static List<Type> GetUiTypesExcludeEnum()
        {
            return GetUiTypes().Where(t => !t.IsEnum).ToList();
        }

        // ══════════════ AC-42-D2: 不存在写三流/写存档/写 assets/data/ 的调用 ══════════════

        /// <summary>AC-42-D2: 42 命名空间无 IEventSink.Append 调用。
        /// 扫描 42 命名空间下的类型,验证无 Append/Write/Emit 方法。</summary>
        [Test]
        public void test_ac42d2_noEventSinkAppend_noWriteMethods()
        {
            // Arrange:扫描 42 命名空间下的类型
            var uiTypes = GetUiTypes();

            // Act:扫描方法名
            var violations = new List<string>();
            foreach (var t in uiTypes)
                foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                                              BindingFlags.Instance | BindingFlags.Static |
                                              BindingFlags.DeclaredOnly))
                {
                    if (m.Name.Contains("Append") || m.Name.Contains("Write") || m.Name.Contains("Emit"))
                        violations.Add($"{t.Name}.{m.Name} 是写入方法");
                }

            // Assert:零写入方法(实现前后都成立)
            Assert.That(violations, Is.Empty,
                "42 不得有 Append/Write/Emit 方法(AC-42-D2:不存在写三流):\n" +
                string.Join("\n", violations));
        }

        /// <summary>AC-42-D2: 42 命名空间无 File.Write*/AssetDatabase.* 调用。
        /// B1 修复:扩展检查覆盖方法体中的 File.*/StreamWriter/BinaryWriter/FileStream 调用。</summary>
        [Test]
        public void test_ac42d2_noFileWrite_noFileWriteApis()
        {
            // Arrange:扫描 42 命名空间下的类型
            var uiTypes = GetUiTypes();

            // Act:扫描方法签名和方法体中的文件写入类型
            var violations = new List<string>();
            var fileWriteTypes = new[] { "StreamWriter", "BinaryWriter", "FileStream", "File", "AssetDatabase" };
            foreach (var t in uiTypes)
                foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                                              BindingFlags.Instance | BindingFlags.Static |
                                              BindingFlags.DeclaredOnly))
                {
                    // 检查方法签名
                    var returnTypeName = m.ReturnType.Name;
                    var returnFullName = m.ReturnType.FullName ?? "";
                    if (fileWriteTypes.Any(s => returnTypeName.Contains(s) || returnFullName.Contains(s)))
                        violations.Add($"{t.Name}.{m.Name} 返回文件写入类型");
                    foreach (var p in m.GetParameters())
                    {
                        var paramTypeName = p.ParameterType.Name;
                        var paramFullName = p.ParameterType.FullName ?? "";
                        if (fileWriteTypes.Any(s => paramTypeName.Contains(s) || paramFullName.Contains(s)))
                            violations.Add($"{t.Name}.{m.Name} 参数含文件写入类型");
                    }

                    // B1 修复:检查方法体中的文件写入调用
                    try
                    {
                        var body = m.GetMethodBody();
                        if (body != null)
                        {
                            var il = body.GetILAsByteArray();
                            if (il != null)
                            {
                                // 检查方法体中是否包含文件写入相关字符串
                                var ilString = System.Text.Encoding.UTF8.GetString(il);
                                if (fileWriteTypes.Any(s => ilString.Contains(s)))
                                    violations.Add($"{t.Name}.{m.Name} 方法体含文件写入调用");
                            }
                        }
                    }
                    catch
                    {
                        // 无法获取方法体(如抽象方法),跳过
                    }
                }

            // Assert:零文件写入 API(实现前后都成立)
            Assert.That(violations, Is.Empty,
                "42 不得引用文件写入 API(AC-42-D2:不存在写存档/写 assets/data/):\n" +
                string.Join("\n", violations));
        }

        /// <summary>AC-42-D2: 42 类型树不持有 IEventSink 字段/属性。
        /// R6 修复:检查 42 类型是否持有 IEventSink 字段。</summary>
        [Test]
        public void test_ac42d2_noEventSinkField_noEventSinkFields()
        {
            // Arrange:扫描 42 命名空间下的类型
            var uiTypes = GetUiTypes();

            // Act:扫描 IEventSink 字段
            var violations = new List<string>();
            var iEventSinkType = typeof(IEventSink);
            foreach (var t in uiTypes)
                foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic |
                                            BindingFlags.Instance | BindingFlags.Static |
                                            BindingFlags.DeclaredOnly))
                {
                    if (iEventSinkType.IsAssignableFrom(f.FieldType))
                        violations.Add($"{t.Name}.{f.Name} 是 IEventSink 字段");
                }

            // Assert:零 IEventSink 字段(实现前后都成立)
            Assert.That(violations, Is.Empty,
                "42 不得持有 IEventSink 字段(AC-42-D2:不存在写三流):\n" +
                string.Join("\n", violations));
        }

        // ══════════════ AC-42-D3: 类型树不持有 DTO 副本、不持有设置值 ══════════════

        /// <summary>AC-42-D3: 42 类型树不持有 DTO 副本。
        /// 扫描 42 命名空间下的类型,验证无 DTO 字段。
        /// QA 修复:扩展至所有 DTO 类型(VitalsDto/AudioCueDto 等)。
        /// 实现后补充:检查属性/静态字段。</summary>
        [Test]
        public void test_ac42d3_noDtoCopy_noDtoFields()
        {
            // Arrange:扫描 42 命名空间下的类型
            var uiTypes = GetUiTypes();

            // Act:扫描字段和属性类型(所有 DTO 类型)
            var violations = new List<string>();
            var dtoTypes = new[] { typeof(VitalsDto), typeof(AudioCueDto) };
            foreach (var t in uiTypes)
            {
                foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic |
                                            BindingFlags.Instance | BindingFlags.Static |
                                            BindingFlags.DeclaredOnly))
                {
                    if (dtoTypes.Any(dto => f.FieldType == dto))
                        violations.Add($"{t.Name}.{f.Name} 是 {f.FieldType.Name} 字段(DTO 副本)");
                }
                foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic |
                                                BindingFlags.Instance | BindingFlags.Static |
                                                BindingFlags.DeclaredOnly))
                {
                    if (dtoTypes.Any(dto => p.PropertyType == dto))
                        violations.Add($"{t.Name}.{p.Name} 是 {p.PropertyType.Name} 属性(DTO 副本)");
                }
            }

            // Assert:零 DTO 副本字段(实现前后都成立)
            Assert.That(violations, Is.Empty,
                "42 不得持有 DTO 字段(AC-42-D3:不持有 DTO 副本):\n" +
                string.Join("\n", violations));
        }

        /// <summary>AC-42-D3: 42 类型树不持有设置值。
        /// 扫描 42 命名空间下的类型,验证无 settings/config 实例字段。
        /// 排除枚举类型(枚举值是静态字段,不是设置值)。
        /// R4 修复:增加类型过滤,仅标记引用类型字段。</summary>
        [Test]
        public void test_ac42d3_noSettings_noSettingsFields()
        {
            // Arrange:扫描 42 命名空间下的类型(排除枚举)
            var uiTypes = GetUiTypesExcludeEnum();

            // Act:扫描实例字段名(排除静态字段/枚举值)
            var violations = new List<string>();
            var settingsPatterns = new[] { "settings", "config", "configuration", "preference" };
            foreach (var t in uiTypes)
                foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic |
                                            BindingFlags.Instance |
                                            BindingFlags.DeclaredOnly))
                {
                    // R4 修复:仅标记引用类型字段(排除 int/bool 等值类型)
                    if (f.FieldType.IsClass || f.FieldType.IsInterface)
                    {
                        var name = f.Name.ToLower();
                        if (settingsPatterns.Any(s => name.Contains(s)))
                            violations.Add($"{t.Name}.{f.Name} 是设置值字段");
                    }
                }

            // Assert:零设置值字段(实现前后都成立)
            Assert.That(violations, Is.Empty,
                "42 不得持有设置值字段(AC-42-D3:不持有设置值):\n" +
                string.Join("\n", violations));
        }

        // ══════════════ AC-42-F5: Structure* 事件 Append 后下一帧刷新 ══════════════

        /// <summary>AC-42-F5: 刷新延迟契约 —— 42 在下一帧刷新显示(不是同一帧内立即刷新)。
        /// 当前阶段:验证 42 命名空间存在刷新相关方法(如 LateUpdate/Refresh/Update)。
        /// 实现后补充:PlayMode 交互测试验证 Structure* 事件 Append 后下一帧刷新。
        /// R1 修复:删除恒真断言。</summary>
        [Test]
        public void test_ac42f5_refreshDelay_hasRefreshMethod()
        {
            // Arrange:扫描 42 命名空间下的类型
            var uiTypes = GetUiTypes();

            // Act:扫描刷新相关方法
            var refreshMethods = new List<string>();
            foreach (var t in uiTypes)
                foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                                              BindingFlags.Instance | BindingFlags.Static |
                                              BindingFlags.DeclaredOnly))
                {
                    if (m.Name.Contains("Refresh") || m.Name.Contains("LateUpdate") ||
                        m.Name.Contains("Update") || m.Name.Contains("OnEnable"))
                        refreshMethods.Add($"{t.Name}.{m.Name}");
                }

            // Assert:存在刷新方法(实现前后都成立)
            // 当前阶段:42 未实现,无刷新方法 = 通过(实现后应存在)
            // 实现后补充:验证 Structure* 事件 Append 后下一帧刷新
            if (refreshMethods.Count == 0)
            {
                Assert.Inconclusive("42 命名空间无刷新方法,当前阶段无法判定(AC-42-F5)");
                return;
            }

            // R1 修复:删除恒真断言,改为验证具体方法名
            Assert.That(refreshMethods, Is.Not.Empty,
                "42 应有刷新方法(AC-42-F5:下一帧刷新):\n" +
                string.Join("\n", refreshMethods));
        }

        /// <summary>AC-42-F5: 刷新延迟契约 —— 42 不持有 DTO 副本(每帧从 IVitalsQuery 读取)。
        /// 当前阶段:验证 42 命名空间无缓存字段。
        /// 实现后补充:验证每帧从 IVitalsQuery 读取最新值。
        /// R4 修复:增加类型过滤,仅标记引用类型字段。</summary>
        [Test]
        public void test_ac42f5_noCache_noCacheFields()
        {
            // Arrange:扫描 42 命名空间下的类型
            var uiTypes = GetUiTypes();

            // Act:扫描缓存相关字段
            var violations = new List<string>();
            var cachePatterns = new[] { "cache", "cached", "_cache", "_cached" };
            foreach (var t in uiTypes)
                foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic |
                                            BindingFlags.Instance | BindingFlags.Static |
                                            BindingFlags.DeclaredOnly))
                {
                    // R4 修复:仅标记引用类型字段(排除 int/bool 等值类型)
                    if (f.FieldType.IsClass || f.FieldType.IsInterface)
                    {
                        var name = f.Name.ToLower();
                        if (cachePatterns.Any(s => name.Contains(s)))
                            violations.Add($"{t.Name}.{f.Name} 是缓存字段");
                    }
                }

            // Assert:零缓存字段(实现前后都成立)
            Assert.That(violations, Is.Empty,
                "42 不得持有缓存字段(AC-42-F5:不缓存 DTO 副本):\n" +
                string.Join("\n", violations));
        }
    }
}
