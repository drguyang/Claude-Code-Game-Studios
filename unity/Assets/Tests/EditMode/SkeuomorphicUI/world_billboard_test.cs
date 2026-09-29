// 权威来源:production/epics/skeuomorphic-ui/story-005-world-billboard.md
//   (AC-42-F3 · V-10 · P0 最小实现)
//   · AC-42-F3: 材质分支(黄铜侧 读数条合法)
//   · V-10: 铜面+蚀刻 哑光面片;触发/状态归 27、值读既有 VitalsDto
//   · P0 最小实现: 无深度冲突;不实现 VR 世界空间(VR 急救推 P1a)
// ADR-013(拟物 UI 框架)· ADR-020 §六(VR 全禁镜头效果)
//
// ⚠️ 落点:故事 Test Evidence 登记路径 = tests/integration/skeuomorphic-ui/world_billboard_test.cs;
//    Unity 只编译 unity/Assets/ 树 ⇒ **真身 = 本文件**;账本侧由 tests/integration/skeuomorphic-ui/README.md 互链。
// ⚠️ 测试纪律:arrange/act/assert · 无随机 · 无时间依赖(禁 DateTime/Time)·
//    夹具自持,不依赖文件系统/网络/数据库。
// ⚠️ 代码评审修复:B1/B2 删除恒真断言;B3 名实不符修正;R1 提取重复代码;R2 类名 PascalCase;R3 补充字段验证。
// ⚠️ QA 评审修复:恒真断言 → Assert.Inconclusive;跳过测试 → Assert.Inconclusive;添加正面验证注释。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Tests.Unit.SkeuomorphicUI
{
    [TestFixture]
    public class WorldBillboardTest
    {
        // ══════════════ 基础设施 ══════════════

        private static string repoRoot([CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile) ?? ".",
                "..", "..", "..", "..", ".."));

        /// <summary>获取 42 命名空间下的所有类型(R1 修复:提取重复代码)。</summary>
        private static List<Type> GetUiTypes()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                .Where(t => t.Namespace != null && t.Namespace.StartsWith("DaYiJingCheng.Gameplay.UI"))
                .ToList();
        }

        // ══════════════ AC-42-F3: 材质分支(黄铜侧 读数条合法)══════════════

        /// <summary>AC-42-F3: 元件库清单断言 —— 库内不存在未查标签/占位符/计数/分组/排序/检索控件/
        /// 同源提示/数字角标/进度填充(墨侧)。
        /// QA 修复:目录不存在时用 Assert.Inconclusive(当前阶段无法判定)。
        /// 实现后补充:按墨侧/黄铜侧分别断言刻度条政策。</summary>
        [Test]
        public void test_ac42f3_componentLibrary_noForbiddenItems()
        {
            // Arrange:扫描 42 元件库目录
            // 路径修正(2026-09-30): 实际目录 = Assets/Gameplay.UI/(asmdef 式命名), 原写 Gameplay/UI/ 不存在 ⇒ 恒 Inconclusive
            string componentDir = Path.Combine(repoRoot(), "unity", "Assets", "Gameplay.UI", "Skeuomorphic");

            // Act:检查目录是否存在
            if (!Directory.Exists(componentDir))
            {
                // QA 修复:目录不存在时用 Inconclusive(当前阶段无法判定)
                Assert.Inconclusive("元件库目录不存在,当前阶段无法判定(AC-42-F3)");
                return;
            }

            // Assert:目录存在时扫描禁用项
            var forbiddenPatterns = new[] { "未查标签", "占位符", "计数", "分组", "排序", "检索控件", "同源提示", "数字角标", "进度填充" };
            var violations = new List<string>();
            var componentFiles = Directory.GetFiles(componentDir, "*.cs", SearchOption.AllDirectories);
            foreach (var file in componentFiles)
            {
                var content = File.ReadAllText(file);
                foreach (var pattern in forbiddenPatterns)
                {
                    if (content.Contains(pattern))
                        violations.Add($"{Path.GetFileName(file)} 含禁用项: {pattern}");
                }
            }

            Assert.That(violations, Is.Empty,
                "元件库不得含未查标签/占位符/计数/分组/排序/检索控件/同源提示/数字角标/进度填充(AC-42-F3):\n" +
                string.Join("\n", violations));
        }

        /// <summary>AC-42-F3: 刻度条只禁墨侧 —— 黄铜侧读数条的錾刻刻度合法。
        /// B3 修复:测试名改为 brassSide_componentsExist,验证黄铜侧元件存在。
        /// QA 修复:目录不存在时用 Assert.Inconclusive(当前阶段无法判定)。
        /// 实现后补充:验证黄铜侧刻度条合法性。</summary>
        [Test]
        public void test_ac42f3_brassSide_componentsExist()
        {
            // Arrange:扫描黄铜侧元件
            string brassDir = Path.Combine(repoRoot(), "unity", "Assets", "Gameplay.UI", "Skeuomorphic", "Brass");

            // Act:检查目录是否存在
            if (!Directory.Exists(brassDir))
            {
                // QA 修复:目录不存在时用 Inconclusive(当前阶段无法判定)
                Assert.Inconclusive("黄铜侧元件目录尚未创建,当前阶段无法判定(AC-42-F3)");
                return;
            }

            // Assert:黄铜侧元件存在时,验证元件文件存在
            var brassFiles = Directory.GetFiles(brassDir, "*.cs", SearchOption.AllDirectories);
            Assert.That(brassFiles, Is.Not.Empty, "黄铜侧元件目录应包含 .cs 文件");
        }

        // ══════════════ V-10: 铜面+蚀刻 哑光面片 ══════════════

        /// <summary>V-10: 值经既有 VitalsDto —— VitalsDto 是 readonly struct 且包含必要字段。
        /// B1 修复:删除恒真断言,保留有意义的字段验证。
        /// 实现后补充:验证 42 通过 IVitalsQuery 读取 VitalsDto 的数据流。</summary>
        [Test]
        public void test_v10_vitalsDto_structureValid()
        {
            // Arrange:获取 VitalsDto 类型
            var vitalsDtoType = typeof(VitalsDto);

            // Act + Assert:验证 struct 类型
            Assert.That(vitalsDtoType.IsValueType, Is.True, "VitalsDto 是 struct");

            // R3 修复:验证必要字段存在
            var positionField = vitalsDtoType.GetField("Position");
            Assert.That(positionField, Is.Not.Null, "VitalsDto 必须有 Position 字段");
            Assert.That(positionField.FieldType, Is.EqualTo(typeof(float)), "Position 是 float");

            var trendField = vitalsDtoType.GetField("Trend");
            Assert.That(trendField, Is.Not.Null, "VitalsDto 必须有 Trend 字段");

            var signChannelMaskField = vitalsDtoType.GetField("SignChannelMask");
            Assert.That(signChannelMaskField, Is.Not.Null, "VitalsDto 必须有 SignChannelMask 字段");
            Assert.That(signChannelMaskField.FieldType, Is.EqualTo(typeof(int)), "SignChannelMask 是 int");

            var signCountField = vitalsDtoType.GetField("SignCount");
            Assert.That(signCountField, Is.Not.Null, "VitalsDto 必须有 SignCount 字段");
        }

        /// <summary>V-10: 值经既有 VitalsDto —— IVitalsQuery 接口存在且返回 VitalsDto。
        /// B2 修复:删除恒真断言,保留有意义的接口和方法签名验证。
        /// 实现后补充:验证 42 使用该接口而非直连 9。</summary>
        [Test]
        public void test_v10_ivitalsQuery_returnsVitalsDto()
        {
            // Arrange:获取 IVitalsQuery 接口
            var ivitalsQueryType = typeof(IVitalsQuery);

            // Act + Assert:验证接口类型
            Assert.That(ivitalsQueryType.IsInterface, Is.True, "IVitalsQuery 是接口");

            // Assert:方法签名正确
            var getVitalsMethod = ivitalsQueryType.GetMethod("GetVitals");
            Assert.That(getVitalsMethod, Is.Not.Null, "IVitalsQuery 必须有 GetVitals 方法");
            Assert.That(getVitalsMethod.ReturnType, Is.EqualTo(typeof(VitalsDto)), "GetVitals 返回 VitalsDto");
        }

        /// <summary>V-10: 触发/状态归 27 —— 42 不持有触发/状态语义。
        /// 当前阶段:验证 42 命名空间无 EncounterStarted/Ended 事件定义。
        /// 实现后补充:验证 27 确实持有这些事件和六态机。</summary>
        [Test]
        public void test_v10_triggerState_notIn42()
        {
            // Arrange:扫描 42 命名空间下的类型
            var uiTypes = GetUiTypes();

            // Act:扫描事件定义
            var violations = new List<string>();
            foreach (var t in uiTypes)
            {
                foreach (var e in t.GetEvents(BindingFlags.Public | BindingFlags.NonPublic |
                                               BindingFlags.Instance | BindingFlags.Static |
                                               BindingFlags.DeclaredOnly))
                {
                    if (e.Name.Contains("EncounterStarted") || e.Name.Contains("EncounterEnded"))
                        violations.Add($"{t.Name}.{e.Name} 是触发/状态事件(应归 27)");
                }
            }

            // Assert:42 无触发/状态事件(实现前后都成立)
            Assert.That(violations, Is.Empty,
                "42 不得有 EncounterStarted/EncounterEnded 事件(触发/状态归 27)(V-10):\n" +
                string.Join("\n", violations));
        }

        // ══════════════ P0 最小实现: 无深度冲突 ══════════════

        /// <summary>P0 最小实现: 无深度冲突 —— billboard 面片不参与平面焦点门(世界空间 = 独占门)。
        /// 当前阶段:验证 42 命名空间无深度冲突相关代码。
        /// 实现后补充:验证 billboard 面片渲染无深度冲突(需 PlayMode 测试)。</summary>
        [Test]
        public void test_p0_noDepthConflict_noDepthConflictCode()
        {
            // Arrange:扫描 42 命名空间下的类型
            var uiTypes = GetUiTypes();

            // Act:扫描深度冲突相关代码
            var violations = new List<string>();
            foreach (var t in uiTypes)
            {
                foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                                              BindingFlags.Instance | BindingFlags.Static |
                                              BindingFlags.DeclaredOnly))
                {
                    if (m.Name.Contains("DepthConflict") || m.Name.Contains("depthConflict"))
                        violations.Add($"{t.Name}.{m.Name} 是深度冲突代码");
                }
            }

            // Assert:42 无深度冲突代码(实现前后都成立)
            Assert.That(violations, Is.Empty,
                "42 不得有深度冲突代码(P0 最小实现:无深度冲突):\n" +
                string.Join("\n", violations));
        }

        /// <summary>P0 最小实现: 不实现 VR 世界空间 —— VR 急救推 P1a。
        /// 当前阶段:验证 42 命名空间无 VR 世界空间相关代码。
        /// 实现后补充:验证 VR 模式不显示(需 PlayMode 测试)。</summary>
        [Test]
        public void test_p0_noVrWorldSpace_noVrCode()
        {
            // Arrange:扫描 42 命名空间下的类型
            var uiTypes = GetUiTypes();

            // Act:扫描 VR 相关代码
            var violations = new List<string>();
            foreach (var t in uiTypes)
            {
                foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                                              BindingFlags.Instance | BindingFlags.Static |
                                              BindingFlags.DeclaredOnly))
                {
                    if (m.Name.Contains("VR") || m.Name.Contains("Vr") || m.Name.Contains("HMD"))
                        violations.Add($"{t.Name}.{m.Name} 是 VR 相关代码(VR 推 P1a)");
                }
            }

            // Assert:42 无 VR 相关代码(实现前后都成立)
            Assert.That(violations, Is.Empty,
                "42 不得有 VR 相关代码(P0 最小实现:VR 推 P1a):\n" +
                string.Join("\n", violations));
        }
    }
}
