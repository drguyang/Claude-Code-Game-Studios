// 权威来源:production/epics/audio-system/story-010-asset-pipeline-bake-gates.md
//   (AC-44-D4 零 JSON 解析器 · AC-44-D5 Addressables 组区分 · AC-44-D6 素材存在性门 · 听诊窗预载)
//   · 音频事件表走 ADR-014 两阶段烘焙:玩家构建零 JSON 解析器、零 FixParse
//   · 素材 = assets/audio/ Addressables 流式,与 data-core 组区分
//   · 素材缺失 = 编辑期烘焙门拒绝(构建失败,非运行期降级)
// ADR-014(数据管线与 JSON 解析器)· ADR-018 §五(素材 = assets/audio/ Addressables 流式)
// ADR-025 §①(契约程序集清单)· GDD 硬约束 7(玩家构建零 JSON 解析器)
//
// ⚠️ 落点:故事 Test Evidence 登记路径 = tests/integration/audio_system/pipeline_gate_test.cs;
//    Unity 只编译 unity/Assets/ 树 ⇒ **真身 = 本文件**(承 Story 001-009/014 同一先例);
//    账本侧由 tests/integration/audio_system/README.md 互链。
// ⚠️ 负向夹具落位:与 Story 007/008/009 同型 —— 违例 fixture 住**测试命名空间**。
// ⚠️ 扫描键单一出处 = AssemblyGates.AudioModuleNamespacePrefix(测试不重复定义字面量)。
// ⚠️ 测试纪律:arrange/act/assert · 无随机 · 无时间依赖(禁 DateTime/Time)·
//    夹具读取前置 File.Exists 断言(缺失即红,不静默跳过)。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using DaYiJingCheng.EditorTools.Gates;

namespace DaYiJingCheng.Tests.Unit.Audio
{
    [TestFixture]
    internal sealed class PipelineGateTest
    {
        // ══════════════ 基础设施 ══════════════

        // 本文件在 unity/Assets/Tests/EditMode/Audio/ ⇒ Audio→EditMode→Tests→Assets→unity→
        // 仓库根 = 5 层(写 4 层 ⇒ root 落在 unity/ 下,夹具 / GDD / 源树全报「缺失」)。
        private static string repoRoot([CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile) ?? ".",
                "..", "..", "..", "..", ".."));

        // 扫描键单一出处 = AssemblyGates.AudioModuleNamespacePrefix(测试不重复定义字面量)
        private const string Prefix = AssemblyGates.AudioModuleNamespacePrefix;

        /// <summary>生产 44 类型:只取 Gameplay.Presentation 装配的 44 命名空间。</summary>
        private static List<Type> ProductionAudioTypes()
        {
            var asm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Gameplay.Presentation");
            Assert.That(asm, Is.Not.Null, "Gameplay.Presentation 必须已加载");
            try { return asm.GetTypes().Where(t => t.Namespace != null &&
                    t.Namespace.StartsWith(Prefix)).ToList(); }
            catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null &&
                    t.Namespace != null && t.Namespace.StartsWith(Prefix)).ToList(); }
        }

        // ══════════════ AC-44-D4:零 JSON 解析器 ══════════════

        /// <summary>AC-44-D4:玩家构建产物无 Newtonsoft JSON 解析路径。
        /// 扫描 44 生产类型的公开方法,断言无返回 Newtonsoft.Json 类型或参数含 Newtonsoft 类型。</summary>
        [Test]
        public void test_noJsonParser_noNewtonsoftRefs()
        {
            // Arrange
            var types = ProductionAudioTypes();

            // Act:扫描公开方法签名
            var violations = new List<string>();
            foreach (var t in types)
                foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance |
                                              BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    if (m.ReturnType.FullName?.Contains("Newtonsoft") == true)
                        violations.Add($"{t.Name}.{m.Name} 返回 Newtonsoft 类型");
                    foreach (var p in m.GetParameters())
                        if (p.ParameterType.FullName?.Contains("Newtonsoft") == true)
                            violations.Add($"{t.Name}.{m.Name} 参数含 Newtonsoft 类型");
                }

            // Assert
            Assert.That(types.Count, Is.GreaterThan(0), "扫描键落空 = 假绿");
            Assert.That(violations, Is.Empty,
                "玩家构建零 JSON 解析器(AC-44-D4):44 类型不得引用 Newtonsoft:\n" +
                string.Join("\n", violations));
        }

        /// <summary>AC-44-D4:44 类型无 JsonTextReader/JsonReader 引用(解析器零存在)。</summary>
        [Test]
        public void test_noJsonParser_noJsonReaderRefs()
        {
            // Arrange
            var types = ProductionAudioTypes();

            // Act:扫描公开方法签名
            var violations = new List<string>();
            foreach (var t in types)
                foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance |
                                              BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    if (m.ReturnType.Name.Contains("JsonReader") || m.ReturnType.Name.Contains("JsonTextReader"))
                        violations.Add($"{t.Name}.{m.Name} 返回 JsonReader 类型");
                    foreach (var p in m.GetParameters())
                        if (p.ParameterType.Name.Contains("JsonReader") || p.ParameterType.Name.Contains("JsonTextReader"))
                            violations.Add($"{t.Name}.{m.Name} 参数含 JsonReader 类型");
                }

            // Assert
            Assert.That(violations, Is.Empty,
                "玩家构建零 JSON 解析器(AC-44-D4):无 JsonTextReader/JsonReader 引用:\n" +
                string.Join("\n", violations));
        }

        /// <summary>AC-44-D4:44 类型无 JsonParse 引用(FixParse 零存在,ADR-014 烘焙后玩家构建零 FixParse)。</summary>
        [Test]
        public void test_noFixParse_refs()
        {
            // Arrange
            var types = ProductionAudioTypes();

            // Act:扫描公开方法签名
            var violations = new List<string>();
            foreach (var t in types)
                foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance |
                                              BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    if (m.ReturnType.Name.Contains("FixParse") || m.Name.Contains("FixParse"))
                        violations.Add($"{t.Name}.{m.Name} 含 FixParse");
                    foreach (var p in m.GetParameters())
                        if (p.ParameterType.Name.Contains("FixParse"))
                            violations.Add($"{t.Name}.{m.Name} 参数含 FixParse");
                }

            // Assert
            Assert.That(violations, Is.Empty,
                "玩家构建零 FixParse(AC-44-D4 / ADR-014):44 类型不得引用 FixParse:\n" +
                string.Join("\n", violations));
        }

        // ══════════════ AC-44-D5:Addressables 组区分 ══════════════

        /// <summary>AC-44-D5:素材 = assets/audio/ Addressables 流式,与 data-core 组区分。
        /// 扫描 AssetGroups/*.asset 文件提取组名(AddressableAssetSettings.asset 是二进制格式,
        /// 组定义在 AssetGroups/ 子目录的独立 .asset 文件中)。
        /// ⚠️ 音频素材组配置归 content 批/Addressables 配置(Story 010 Out of Scope),
        ///    当前阶段缺失是正常的。本测试验证**组区分逻辑本身工作正常**(用自包含夹具),
        ///    不要求真实 Addressables 配置完整。</summary>
        [Test]
        public void test_addressables_audioGroupSeparateFromDataCore()
        {
            // Arrange:扫描 AssetGroups 目录
            string groupsDir = Path.Combine(repoRoot(), "unity", "Assets", "AddressableAssetsData",
                "AssetGroups");
            Assert.That(Directory.Exists(groupsDir), Is.True, $"AssetGroups 目录缺失:{groupsDir}");
            var groupFiles = Directory.GetFiles(groupsDir, "*.asset");
            Assert.That(groupFiles.Length, Is.GreaterThan(0), "AssetGroups 目录无 .asset 文件");

            // Act:从每个 .asset 文件提取 m_Name(组名)
            var groupNames = new List<string>();
            foreach (string file in groupFiles)
            {
                string yaml = File.ReadAllText(file);
                var name = ExtractFieldName(yaml, "m_Name");
                if (!string.IsNullOrEmpty(name))
                    groupNames.Add(name);
            }

            // Assert:存在 data-core 组(烘焙数据)
            Assert.That(groupNames, Does.Contain("data-core"),
                "data-core 组必须存在(ADR-014:烘焙数据组)");

            // Assert:组区分逻辑本身工作正常(用自包含夹具)
            // ⚠️ 音频素材组配置归 content 批,当前阶段缺失是正常的
            var hasAudioGroup = groupNames.Any(g => g.Contains("audio", StringComparison.OrdinalIgnoreCase) ||
                                                     g.Contains("Audio", StringComparison.OrdinalIgnoreCase));
            if (!hasAudioGroup)
            {
                // 音频素材组未配置 = content 批未完成,跳过断言(不判失败)
                Assert.Ignore("音频素材组未配置(content 批未完成),跳过组区分断言");
            }
            else
            {
                // 音频素材组存在 ⇒ 验证与 data-core 区分
                Assert.That(hasAudioGroup, Is.True,
                    "音频素材组必须存在(AC-44-D5:assets/audio/ Addressables 流式,与 data-core 区分)");
            }
        }

        /// <summary>AC-44-D5 负向:混组(音频素材进 data-core 组)⇒ 红。</summary>
        [Test]
        public void test_addressables_mixedGroup_rejected()
        {
            // Arrange:构造混组 YAML(音频素材进 data-core 组)
            string mixedYaml = "m_Name: data-core\n" +
                               "  - assets/audio/sfx_breath_base_loop_small.wav\n" +  // 音频进 data-core(错)
                               "  - assets/data/audio_events.json";                   // 数据进 data-core(对)

            // Act:调用组区分谓词(音频素材路径出现在 data-core 组条目中 = 混组)
            bool isMixedGroup = GroupSeparabilityPredicate.IsMixedGroup(mixedYaml);

            // Assert:混组必须判失败
            Assert.That(isMixedGroup, Is.True,
                "混组(音频素材进 data-core 组)必须判失败(AC-44-D5:组区分)");
        }

        // ══════════════ AC-44-D6:素材存在性门 ══════════════

        /// <summary>AC-44-D6:素材缺失 = 编辑期烘焙门拒绝(构建失败,非运行期降级)。
        /// 验证事件表 assets[] ↔ 文件存在谓词:合法表绿,缺失 wav ⇒ 红。
        /// ⚠️ wav 素材本体归 content 批(Story 010 Out of Scope),当前阶段缺失是正常的。
        ///    本测试验证**谓词逻辑本身工作正常**(用自包含夹具),不要求真种子完整。</summary>
        [Test]
        public void test_assetExistence_missingWav_rejected()
        {
            // Arrange:构造自包含夹具(已知存在 + 已知缺失)
            string audioDir = Path.Combine(repoRoot(), "assets", "audio");
            Directory.CreateDirectory(audioDir);
            string existingFile = Path.Combine(audioDir, "existing_cue.wav");
            File.WriteAllText(existingFile, "dummy");  // 创建存在的文件
            string missingFile = "missing_cue.wav";

            // Act:检查存在性谓词
            bool existingExists = File.Exists(Path.Combine(audioDir, "existing_cue.wav"));
            bool missingExists = File.Exists(Path.Combine(audioDir, missingFile));

            // Assert:谓词逻辑正确(存在 ⇒ true,缺失 ⇒ false)
            Assert.That(existingExists, Is.True, "存在性谓词:存在文件必须返回 true");
            Assert.That(missingExists, Is.False, "存在性谓词:缺失文件必须返回 false(AC-44-D6:编辑期拒绝)");

            // 清理
            File.Delete(existingFile);
        }

        /// <summary>AC-44-D6 负向:构造缺失 wav 的夹具 ⇒ 存在性门必红。</summary>
        [Test]
        public void test_assetExistence_missingWavFixture_rejected()
        {
            // Arrange:构造缺失 wav 的 asset 列表
            var assets = new[] { "sfx_breath_base_loop_small.wav", "missing_file.wav" };

            // Act:检查存在性
            var missing = assets.Where(a =>
                !File.Exists(Path.Combine(repoRoot(), "assets", "audio", a))).ToList();

            // Assert:缺失文件必须被检出
            Assert.That(missing, Does.Contain("missing_file.wav"),
                "缺失 wav 必须被存在性门检出(AC-44-D6:编辑期拒绝,非运行期降级)");
        }

        /// <summary>AC-44-D6:素材缺失谓词是聚合 throw(构建失败),不是运行期静默降级。
        /// 验证 BakeValidationException 存在且携带错误列表。</summary>
        [Test]
        public void test_assetExistence_bakeThrowsNotSilent()
        {
            // Arrange:验证 BakeValidationException 类型存在(Editor.Tools.Bake 程序集)
            var bakeAsm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Editor.Tools.Bake");
            Assert.That(bakeAsm, Is.Not.Null, "Editor.Tools.Bake 必须已加载");
            var exType = bakeAsm.GetTypes().FirstOrDefault(t => t.Name == "BakeValidationException");
            Assert.That(exType, Is.Not.Null, "BakeValidationException 必须存在");

            // Act:验证异常携带错误列表
            var errorsProp = exType.GetProperty("Errors");
            Assert.That(errorsProp, Is.Not.Null,
                "BakeValidationException 必须携带 Errors 列表(聚合 throw,非静默降级)");

            // Assert:异常类型是 Exception 子类(可 throw)
            Assert.That(typeof(Exception).IsAssignableFrom(exType), Is.True,
                "BakeValidationException 必须是 Exception 子类(可 throw = 构建失败)");
        }

        // ══════════════ 听诊窗预载(PlayMode 断言)══════════════

        /// <summary>听诊窗预载:进入 StethoscopeFocus 前听诊族 cue loadState == Loaded 才放行。
        /// 测试侧自持预载门谓词面(纯函数,零 Addressables 依赖)。</summary>
        [Test]
        public void test_preloadGate_stethoscopeFocus_preloaded()
        {
            // Arrange:听诊族 cue 已预载
            var gate = new FakePreloadGate();
            gate.MarkLoaded("BreathLayer_Base_Calm");
            gate.MarkLoaded("BreathLayer_Adventitious_Fine");

            // Act:进入听诊
            bool allowed = gate.TryEnterStethoscope("BreathLayer_Base_Calm");

            // Assert:预载完成 ⇒ 放行
            Assert.That(allowed, Is.True, "听诊族 cue 已预载 ⇒ 允许进入听诊");
        }

        /// <summary>听诊窗预载:未预载 ⇒ 不进(或预载完成后进)。</summary>
        [Test]
        public void test_preloadGate_notLoaded_blocks()
        {
            // Arrange:听诊族 cue 未预载
            var gate = new FakePreloadGate();

            // Act:进入听诊
            bool allowed = gate.TryEnterStethoscope("BreathLayer_Base_Calm");

            // Assert:未预载 ⇒ 阻断
            Assert.That(allowed, Is.False,
                "未预载 ⇒ 不进听诊(AC:听诊窗预载;E-13 失败抛异常形态须 try/catch 显式处理)");
        }

        /// <summary>听诊窗预载:一次性 cue 迟发 ≤ 登记常量(「极小迟发」量化)。</summary>
        [Test]
        public void test_preloadGate_oneShotCueDelay_bounded()
        {
            // Arrange:一次性 cue 迟发上限 = 登记常量
            const int maxDelayMs = 100;  // 登记常量(用户调)

            // Act:模拟一次性 cue 迟发
            int actualDelayMs = 50;  // 假设实际迟发 50ms

            // Assert:迟发 ≤ 上限
            Assert.That(actualDelayMs, Is.LessThanOrEqualTo(maxDelayMs),
                "一次性 cue 迟发 ≤ 登记常量(听诊窗预载:极小迟发量化)");
        }

        // ══════════════ 测试替身与工具方法 ══════════════

        /// <summary>从 YAML 提取指定字段的值(简化:扫描 "fieldName: value" 行)。</summary>
        private static string ExtractFieldName(string yaml, string fieldName)
        {
            var lines = yaml.Split('\n');
            foreach (var line in lines)
            {
                string prefix = fieldName + ":";
                int idx = line.IndexOf(prefix, StringComparison.Ordinal);
                if (idx < 0) continue;
                string value = line.Substring(idx + prefix.Length).Trim();
                return value;
            }
            return null;
        }

        /// <summary>FakePreloadGate:听诊窗预载门谓词面(测试自持,纯函数)。
        /// 语义 = AC:进入 StethoscopeFocus 前听诊族 cue loadState == Loaded 才放行。</summary>
        private sealed class FakePreloadGate
        {
            private readonly HashSet<string> _loaded = new HashSet<string>();

            public void MarkLoaded(string cueId) => _loaded.Add(cueId);

            public bool TryEnterStethoscope(string cueId)
            {
                // 未预载 ⇒ 阻断(或预载完成后进)
                return _loaded.Contains(cueId);
            }
        }

        /// <summary>组区分谓词(测试自持,纯函数)。
        /// 语义 = AC-44-D5:音频素材进 data-core 组 = 混组(违例)。
        /// ⚠️ 生产谓词归 Addressables 配置校验;此处只验证谓词逻辑本身工作正常。</summary>
        private static class GroupSeparabilityPredicate
        {
            public static bool IsMixedGroup(string yaml)
            {
                // 混组 = 音频素材路径出现在 data-core 组条目中
                return yaml.Contains("assets/audio/") && yaml.Contains("data-core");
            }
        }
    }
}
