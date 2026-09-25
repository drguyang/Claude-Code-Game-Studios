// Story 001 · 动作资产 · 单实例纪律与 Legacy 零引用门(三条 AC 的全部测试真身)
//
// 权威来源:production/epics/input-system/story-001-action-asset-identity.md(判据权威,AC 原文)
//   · AC-3-A1(BLOCKING)单引用同一性 —— GetInstanceID(),非文件路径/文件计数(反例 = 同文件二次实例化)
//   · AC-3-A4①(BLOCKING)activeInputHandler == 1 —— 载体 = SerializedObject(PlayerSettings)
//       .FindProperty("activeInputHandler").intValue(2026-09-25 裁定换轨:GetPropertyInt 实测恒垃圾值,
//       且已 obsolete;见 GDD AC-3-A4① 就地修订注),EditMode 断言、Editor-only 程序集;
//       **禁**写 PlayerSettings 的同名属性访问与对应枚举成员名
//       (属性经反射核验在 6.3 不存在;枚举成员存在性仍未核验 ⇒ 均不进 BLOCKING 判据)
//   · AC-3-A4②(BLOCKING)Roslyn 分析器编译期拒绝 UnityEngine.Input 符号(编译失败,非 grep)
//   · TR-input-001 / TR-input-002(docs/architecture/tr-registry.yaml 的 requirement 字段为真源)
//   · GDD input-system.md 规则一(全案恰一个 .inputactions)/ 规则二(P0 动作清单表)/
//     规则三(三套绑重同表共存;OpenXR 绑通用 XRController;Gamepad Look 与 Navigate 不共享控件)
//   · ADR-011 §Risks-A S2(判据层只断「实例同一性」性质,不依赖 Instantiate/Clone 符号形状)
//
// 落点注记:故事 Test Evidence 登记口径为 tests/integration/input_system/action_asset_identity_test.cs;
//   Unity 只编译 unity/Assets/ 树 ⇒ 真身落本路径(unity/Assets/Tests/EditMode/InputSystem/)。
//
// 纪律:NUnit · test_* 命名 · 无随机/无时间依赖(确定性);子进程 60s 超时兜底;
//   反例夹具销毁用 DestroyImmediate(编辑态 Destroy 报错且留脏实例 ⇒ 同一性断言假绿);
//   GetInstanceID 现取现比、不跨会话持久化;每测试例自建服务实例。

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using DaYiJingCheng.Gameplay.Input;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DaYiJingCheng.Tests.Unit.InputSystem
{
    /// <summary>Story 001 动作资产同一性 + Legacy 零引用双门(AC-3-A1 / A4① / A4②)的 EditMode 测试。</summary>
    [TestFixture]
    internal sealed class ActionAssetIdentityTest
    {
        private const string ActionAssetPath = "Assets/InputSystem_Actions.inputactions";
        private const string AnalyzerAssetPath = "Assets/Editor.Tools.Analyzers/LegacyInputAnalyzer.dll";
        private const string RoslynLabel = "RoslynAnalyzer";

        private static readonly string RepoRoot = ComputeRepoRoot();
        private static readonly string AnalyzerDllOnDisk =
            Path.Combine(RepoRoot, "unity", "Assets", "Editor.Tools.Analyzers", "LegacyInputAnalyzer.dll");

        private static readonly string[] ExpectedBindingGroups = { "Keyboard&Mouse", "Gamepad", "XR" };

        private static string ComputeRepoRoot([CallerFilePath] string callerPath = "")
            => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(callerPath), "..", "..", "..", "..", ".."));

        private static InputActionAsset LoadActionAsset()
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionAssetPath);
            Assert.That(asset, Is.Not.Null, "动作资产未找到:" + ActionAssetPath);
            return asset;
        }

        private static IEnumerable<InputBinding> EffectiveBindings(InputAction action)
            => action.bindings.Where(b => !b.isComposite); // 复合父绑定 groups 恒空,判覆盖时跳过

        private static HashSet<string> GroupsOf(InputAction action)
        {
            var seen = new HashSet<string>();
            foreach (var b in EffectiveBindings(action))
                foreach (var g in (b.groups ?? string.Empty).Split(';'))
                    if (!string.IsNullOrEmpty(g))
                        seen.Add(g);
            return seen;
        }

        private static HashSet<string> PathsInGroup(InputActionMap map, string actionName, string group)
        {
            var outPaths = new HashSet<string>();
            var action = map.actions.First(a => a.name == actionName);
            foreach (var b in EffectiveBindings(action))
                if ((b.groups ?? string.Empty).Split(';').Contains(group))
                    outPaths.Add(b.path);
            return outPaths;
        }

        // ══════════ AC-3-A1 · 单引用同一性(GetInstanceID,非路径/非文件计数)══════════

        /// <summary>AC-3-A1 正例:输入服务初始化后取两次实例,GetInstanceID 相等(同一实例)。</summary>
        [Test]
        public void test_input_asset_identity_service_two_reads_return_same_instance_id()
        {
            // Arrange
            var asset = LoadActionAsset();
            var service = new InputService(asset);

            // Act
            int first = service.Actions.GetInstanceID();
            int second = service.Actions.GetInstanceID();

            // Assert
            Assert.That(second, Is.EqualTo(first), "服务两次读取必须返回同一实例(AC-3-A1 前半条)");
            Assert.That(InputService.IsSameAssetInstance(service.Actions, service.Actions), Is.True);
            Assert.That(service.ActionsInstanceId, Is.EqualTo(first));
        }

        /// <summary>AC-3-A1 反例夹具(AC 明文点名的失效形态):同文件二次实例化 ⇒ 同一性断言必须失败。</summary>
        [Test]
        public void test_input_asset_identity_second_instantiation_of_same_file_fails_assertion()
        {
            // Arrange
            var asset = LoadActionAsset();
            var service = new InputService(asset);
            var clone = UnityEngine.Object.Instantiate(asset); // 同一 .inputactions 的第二份实例
            try
            {
                // Act + Assert
                Assert.That(clone.GetInstanceID(), Is.Not.EqualTo(asset.GetInstanceID()),
                    "克隆体必须是不同实例(否则夹具本身失效)");
                Assert.That(InputService.IsSameAssetInstance(asset, clone), Is.False,
                    "同文件的两份实例化必须判「非同一」—— 判据若退化为路径/文件计数,本断言即红(AC-3-A1)");
                Assert.Throws<InvalidOperationException>(() => service.EnsureSameAssetInstance(clone),
                    "单实例纪律断言应对克隆体抛出(反例夹具)");
            }
            finally
            {
                // 编辑态销毁必须 DestroyImmediate —— Destroy 会留脏实例,污染后续 GetInstanceID 比较
                UnityEngine.Object.DestroyImmediate(clone);
            }
        }

        /// <summary>AC-3-A1 边界:同文件路径的两次解析返回同一实例(编辑器资产缓存);
        /// 「路径相同但实例不同」的分叉由上面的克隆反例覆盖 —— 二者合起来证明判据不是路径比较。</summary>
        [Test]
        public void test_input_asset_identity_same_path_two_loads_yield_one_instance()
        {
            // Arrange
            var a = LoadActionAsset();

            // Act
            var b = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionAssetPath);

            // Assert
            Assert.That(b, Is.Not.Null);
            Assert.That(b.GetInstanceID(), Is.EqualTo(a.GetInstanceID()),
                "同路径两次解析在编辑器内必须是同一实例缓存(非重新实例化)");
        }

        // ══════════ TR-input-001 / TR-input-002 · 资产结构(规则一 / 规则二 / 规则三)══════════

        /// <summary>规则一:全案恰一个 .inputactions(文件级;实例级同一性见 AC-A1 组)。</summary>
        [Test]
        public void test_input_asset_structure_project_contains_exactly_one_input_actions_file()
        {
            // Arrange
            string assetsDir = Path.Combine(RepoRoot, "unity", "Assets");

            // Act
            string[] files = Directory.GetFiles(assetsDir, "*.inputactions", SearchOption.AllDirectories);

            // Assert
            Assert.That(files.Length, Is.EqualTo(1),
                () => "规则一:全案恰一个 .inputactions,实际:\n" + string.Join("\n", files));
            StringAssert.Contains(ActionAssetPath, files[0].Replace('\\', '/'));
        }

        /// <summary>规则二 + 规则三(结构面):两张 map 的 P0 动作清单恰如 GDD 表,
        /// 且每个动作在三套绑重组(K&M / Gamepad / XR)下各有 ≥1 条绑定(同表共存)。</summary>
        [Test]
        public void test_input_asset_structure_p0_actions_each_cover_three_binding_groups()
        {
            // Arrange
            var asset = LoadActionAsset();

            // Act
            string[] mapNames = asset.actionMaps.Select(m => m.name).ToArray();

            // Assert —— map 集合
            Assert.That(mapNames, Is.EqualTo(new[] { "Player", "UI" }), "规则一:恰两张 action map");

            var player = asset.actionMaps.First(m => m.name == "Player");
            var ui = asset.actionMaps.First(m => m.name == "UI");

            Assert.That(player.actions.Select(a => a.name).ToArray(),
                Is.EqualTo(new[] { "Move", "Look", "Sprint", "Jump", "Interact", "Emergency", "Attack", "OpenInventory" }),
                "规则二:Player map 动作清单(GDD 表)");
            Assert.That(ui.actions.Select(a => a.name).ToArray(),
                Is.EqualTo(new[] { "Navigate", "Submit", "Cancel", "PagePrev", "PageNext" }),
                "规则二:UI map 动作清单(GDD 表;规则十:3 的 Navigate 即 UI map 的这一个实例)");

            // Assert —— 每动作 × 三组覆盖
            foreach (var map in asset.actionMaps)
                foreach (var action in map.actions)
                    Assert.That(GroupsOf(action), Is.EquivalentTo(ExpectedBindingGroups),
                        $"{map.name}/{action.name} 的绑重组必须三套同表共存(缺组即缺设备端)");

            // Assert —— controlSchemes 三套,且 bindingGroup 与 binding.groups 一致(方案名↔组名同集)
            string[] schemeGroups = asset.controlSchemes.Select(s => s.bindingGroup).ToArray();
            Assert.That(schemeGroups, Is.EquivalentTo(ExpectedBindingGroups),
                "controlSchemes 必须恰为 K&M / Gamepad / XR 三套,且 bindingGroup 与 groups 同集");
        }

        /// <summary>规则三(OpenXR 半条 · TR-input-002):XR 组绑重一律通用 XRController 布局,
        /// 不绑 OculusTouch / Index 等专属设备;usage 形态(*/{…})为设备无关,放行。</summary>
        [Test]
        public void test_input_asset_structure_openxr_bindings_use_generic_xrcontroller_layout()
        {
            // Arrange
            var asset = LoadActionAsset();
            string[] forbiddenDevices = { "OculusTouch", "IndexController", "HTCVive", "WindowsMR", "Knuckles", "Quest", "Vive" };

            // Act
            int xrBindingCount = 0;
            var violations = new List<string>();
            foreach (var map in asset.actionMaps)
                foreach (var action in map.actions)
                    foreach (var b in EffectiveBindings(action))
                    {
                        var groups = (b.groups ?? string.Empty).Split(';');
                        if (!groups.Contains("XR"))
                            continue;
                        xrBindingCount++;
                        bool generic = b.path.StartsWith("<XRController>", StringComparison.Ordinal)
                                       || b.path.StartsWith("*/", StringComparison.Ordinal);
                        if (!generic)
                            violations.Add($"{map.name}/{action.name}: {b.path}");
                        foreach (var dev in forbiddenDevices)
                            if (b.path.Contains(dev, StringComparison.Ordinal))
                                violations.Add($"{map.name}/{action.name}: 绑了专属设备 {b.path}");
                    }

            // Assert
            Assert.That(xrBindingCount, Is.GreaterThan(0), "XR 组必须有绑重(三套共存)");
            Assert.That(violations, Is.Empty, () => "OpenXR 绑重必须通用 XRController 布局:\n" + string.Join("\n", violations));
        }

        /// <summary>规则三(Gamepad 半条):Look 与 Navigate 不得共享同一物理控件
        /// (共享 = 移动镜头的同时焦点乱跳;GDD 规则三 / AC-2-22⑥ 的 3 侧落点)。</summary>
        [Test]
        public void test_input_asset_structure_gamepad_look_and_navigate_do_not_share_control()
        {
            // Arrange
            var asset = LoadActionAsset();
            var player = asset.actionMaps.First(m => m.name == "Player");
            var ui = asset.actionMaps.First(m => m.name == "UI");

            // Act
            var lookPaths = PathsInGroup(player, "Look", "Gamepad");
            var navigatePaths = PathsInGroup(ui, "Navigate", "Gamepad");
            var shared = lookPaths.Intersect(navigatePaths).ToArray();

            // Assert
            Assert.That(lookPaths, Is.Not.Empty, "Look 必须有 Gamepad 绑重");
            Assert.That(navigatePaths, Is.Not.Empty, "Navigate 必须有 Gamepad 绑重");
            Assert.That(shared, Is.Empty, "规则三:Gamepad 的 Look 与 Navigate 不得共享物理控件:" + string.Join(", ", shared));
        }

        // ══════════ AC-3-A4① · activeInputHandler == 1(EditMode · Editor-only)══════════

        /// <summary>AC-3-A4①:工程 Active Input Handling = Input System Package(整数编码 1)。
        /// 载体 = SerializedObject 读序列化字段(2026-09-25 裁定:原字面 GetPropertyInt 在 6.3 实测恒返垃圾值
        /// 且已 obsolete —— 三轮探针见 GDD AC-3-A4① 就地修订注);禁用符号形态见下一测的 grep 级自扫。</summary>
        [Test]
        public void test_legacy_input_gate_active_input_handler_is_input_system_package_only()
        {
            // Arrange:PlayerSettings 对象经 ProjectSettings/ProjectSettings.asset 序列化对象取得
            UnityEngine.Object[] settingsAssets =
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            Assert.That(settingsAssets, Is.Not.Null.And.Not.Empty,
                "ProjectSettings/ProjectSettings.asset 未加载到任何对象");
            Assert.That(settingsAssets.Length, Is.EqualTo(1),
                "ProjectSettings.asset 应恰载入一个子对象(2026-09-25 复核 N2:把「第 0 个」的自证升为明证)");
            Assert.That(settingsAssets[0].GetType().Name, Is.EqualTo("PlayerSettings"),
                "第 0 个对象必须是 PlayerSettings —— 类型不符即引擎资产布局变更,判据需重估");

            // Act:1 = Input System Package · 0 = Old · 2 = Both(AC 的整数编码)
            var settings = new SerializedObject(settingsAssets[0]);
            SerializedProperty handler = settings.FindProperty("activeInputHandler");
            Assert.That(handler, Is.Not.Null,
                "序列化字段 activeInputHandler 不存在 —— 引擎序列化布局变更,判据需重估");
            int activeInputHandler = handler.intValue;

            // Assert:值为 0(Old)或 2(Both)即红,覆盖「回退 Both」的失效形态
            Assert.That(activeInputHandler, Is.EqualTo(1),
                "Active Input Handling 必须是 Input System Package only(1);0=Old、2=Both 均违规");
        }

        /// <summary>AC-3-A4① 的 Editor-only 要求:断言所在程序集 = includePlatforms 含 Editor 的 asmdef。</summary>
        [Test]
        public void test_legacy_input_gate_assertion_lives_in_editor_only_assembly()
        {
            // Arrange
            string asmdefPath = Path.Combine(RepoRoot, "unity", "Assets", "Tests", "EditMode", "EditMode.asmdef");

            // Act
            Assert.That(File.Exists(asmdefPath), Is.True, "EditMode.asmdef 缺失:" + asmdefPath);
            string json = File.ReadAllText(asmdefPath);

            // Assert:正则容忍 JSON 空白排版变化(2026-09-25 复核 F5:字面比对与排版耦合)
            Assert.That(json,
                Does.Match("\"includePlatforms\"\\s*:\\s*\\[\\s*\"Editor\"\\s*\\]"),
                "本测试程序集必须是 Editor-only(AC-3-A4①:EditMode 断言且位于 Editor-only 程序集)");
        }

        /// <summary>AC-3-A4① 的 grep 级辅助:全 Assets 树(2026-09-25 复核 F2:自扫作用域由
        /// 「仅本文件」扩为 AC 字面的全工程)不得出现已废弃的 GetPropertyInt 读法、
        /// 同名属性访问或未核验的枚举成员名(写进 BLOCKING 判据的不可用符号 = 一条不可能通过的验收);
        /// 另断本文件走 SerializedObject 载体。</summary>
        [Test]
        public void test_legacy_input_gate_source_uses_serialized_property_carrier_without_banned_symbols()
        {
            // Arrange:本文件路径经 [CallerFilePath] 上溯(编译期烙入,与运行机路径无关)
            string src = File.ReadAllText(SelfSourcePath());

            // 被禁 token 用字符串拼接构造 —— 否则断言字面量本身就让自扫恒红(自己写进自己文件里)
            string bannedProperty = "PlayerSettings" + ".activeInputHandler";
            string bannedEnumToken = "Input" + "SystemPackage";
            string obsoleteCarrier = "GetPropertyInt" + "(\"activeInputHandler\")";

            // Act:正向(本文件走新载体)
            StringAssert.Contains("FindProperty(\"activeInputHandler\")", src);

            // Act:全 Assets 树扫描(所有 .cs;命中文件列进失败消息)
            string assetsRoot = Path.Combine(RepoRoot, "unity", "Assets");
            var offenders = new List<string>();
            foreach (string file in Directory.GetFiles(assetsRoot, "*.cs", SearchOption.AllDirectories))
            {
                string text = File.ReadAllText(file);
                if (text.Contains(bannedProperty, StringComparison.Ordinal))
                    offenders.Add(file + " → " + bannedProperty);
                if (text.Contains(bannedEnumToken, StringComparison.Ordinal))
                    offenders.Add(file + " → " + bannedEnumToken);
                if (text.Contains(obsoleteCarrier, StringComparison.Ordinal))
                    offenders.Add(file + " → " + obsoleteCarrier);
            }

            // Assert
            Assert.That(offenders, Is.Empty,
                () => "AC-3-A4①:全 Assets 树出现禁用符号形态:\n" + string.Join("\n", offenders));
        }

        // ══════════ AC-3-A4② · Roslyn 分析器(编译期拒 UnityEngine.Input 符号)══════════

        /// <summary>AC-3-A4② 前置:分析器 DLL 已产出,且带 RoslynAnalyzer label(label 缺失 ⇒ 门静默失效)。</summary>
        [Test]
        public void test_legacy_input_gate_analyzer_dll_present_and_labelled()
        {
            // Arrange
            Assert.That(File.Exists(AnalyzerDllOnDisk), Is.True,
                "分析器 DLL 不存在 —— 先跑 tools/analyzers/build.sh:" + AnalyzerDllOnDisk);

            // Act
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(AnalyzerAssetPath);
            Assert.That(asset, Is.Not.Null, "AssetDatabase 未加载到分析器 DLL:" + AnalyzerAssetPath);
            string[] labels = AssetDatabase.GetLabels(asset);

            // Assert
            Assert.That(labels, Does.Contain(RoslynLabel),
                $"缺少 {RoslynLabel} label ⇒ Unity 不会把它当分析器,AC-3-A4② 静默失效" +
                "(先 batch 执行 DaYiJingCheng.EditorTools.Gates.RoslynAnalyzerLabel.SetLabel)");
        }

        /// <summary>平台位全关回归(2026-09-25 B 实验改判):Any=off 且 Editor=off。
        /// Editor=on 会令每次域重载把 net6.0 分析器当普通插件装载 ⇒
        /// 21× scripting_class_is_subclass_of 警告 + Unloading broken assembly(ADR-012 挂账注);
        /// 实证 label 消费不依赖平台位(DY0001 编译期照报)。翻回任一位即红。</summary>
        [Test]
        public void test_legacy_input_gate_analyzer_platform_all_off()
        {
            // Arrange
            var importer = AssetImporter.GetAtPath(AnalyzerAssetPath) as PluginImporter;
            Assert.That(importer, Is.Not.Null, "PluginImporter 获取失败:" + AnalyzerAssetPath);

            // Assert
            Assert.That(importer.GetCompatibleWithAnyPlatform(), Is.False,
                "分析器 DLL 不得 Any=on ⇒ 会卷进 player 根程序集,其 net6.0 产物直引 " +
                "System.Private.CoreLib 6.0.0.0 ⇒ UnityLinker AssemblyResolutionException(W2 实证):" +
                AnalyzerAssetPath);
            Assert.That(importer.GetCompatibleWithEditor(), Is.False,
                "分析器 DLL 不得 Editor=on ⇒ 域重载会当普通插件装载,产生 " +
                "scripting_class_is_subclass_of ×21 + Unloading broken assembly 噪声(B 实验改判):" +
                AnalyzerAssetPath +
                "(回滚入口:DaYiJingCheng.EditorTools.Gates.RoslynAnalyzerLabel.SetLabel)");
        }

        /// <summary>AC-3-A4② 新鲜度(2026-09-25 复核 W2):DLL ↔ 分析器源码经 build.sh 产出的
        /// sidecar(双 hash:源 + DLL)绑定 —— 改源不重跑 build.sh ⇒ 红(防门静默跑旧逻辑)。</summary>
        [Test]
        public void test_legacy_input_gate_analyzer_dll_matches_source_freshness_sidecar()
        {
            // Arrange
            string srcPath = Path.Combine(RepoRoot, "tools", "analyzers",
                "LegacyInputAnalyzer", "LegacyInputAnalyzer.cs");
            string sidecarPath = Path.Combine(RepoRoot, "tools", "analyzers", "LegacyInputAnalyzer.dll.sha256");
            Assert.That(File.Exists(srcPath), Is.True, "分析器源码缺失:" + srcPath);
            Assert.That(File.Exists(sidecarPath), Is.True,
                "新鲜度 sidecar 缺失 —— 重跑 bash tools/analyzers/build.sh 产出:" + sidecarPath);
            Assert.That(File.Exists(AnalyzerDllOnDisk), Is.True,
                "分析器 DLL 不存在 —— 先跑 tools/analyzers/build.sh:" + AnalyzerDllOnDisk);

            // Act:sidecar 一行两列(源 hash · DLL hash)
            string[] parts = File.ReadAllText(sidecarPath)
                .Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            Assert.That(parts.Length, Is.EqualTo(2),
                "sidecar 格式应为「<源 sha256> <DLL sha256>」:" + sidecarPath);
            string srcHash = Sha256Hex(srcPath);
            string dllHash = Sha256Hex(AnalyzerDllOnDisk);

            // Assert
            Assert.That(srcHash, Is.EqualTo(parts[0]),
                "分析器源码已改动但 sidecar 未更新 ⇒ 改了源没重跑 build.sh(门会跑旧逻辑):" + srcPath);
            Assert.That(dllHash, Is.EqualTo(parts[1]),
                "DLL 与 sidecar 记录不符 ⇒ 重跑 build.sh 以刷新:" + AnalyzerDllOnDisk);
        }

        private static string Sha256Hex(string path)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
            using (var stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>AC-3-A4② 正向拒绝(全限定形态):样例含 UnityEngine.Input 符号引用 ⇒ 子进程 csc 编译失败且报 DY0001。</summary>
        [Test]
        public void test_legacy_input_gate_analyzer_rejects_fully_qualified_legacy_input()
        {
            // Arrange
            const string sample =
                "public static class BadFullyQualified\n" +
                "{\n" +
                "    public static bool Run() => UnityEngine.Input.GetKey(UnityEngine.KeyCode.Space);\n" +
                "    public static float Axis() => UnityEngine.Input.GetAxis(\"Horizontal\");\n" +
                "}\n";

            // Act
            var (exitCode, output) = RunCscWithAnalyzer("bad_fully_qualified.cs", sample);

            // Assert:编译失败(非 0)+ 诊断含 DY0001(不是任何随手的错误)+ 诊断指向引用位置(QA Then「指向该引用」,2026-09-25 补 F7)
            Assert.That(exitCode, Is.Not.EqualTo(0), "含 Legacy Input 引用的样例必须编译失败:\n" + output);
            Assert.That(output, Does.Contain("DY0001"), "失败必须由 DY0001 报出:\n" + output);
            Assert.That(output, Does.Match(@"bad_fully_qualified\.cs\(\d+,\d+\): error DY0001"),
                "诊断必须带行列位置、指向该引用(而非无位置的裸诊断):\n" + output);
        }

        /// <summary>AC-3-A4② 方法组形态(2026-09-25 复核 W3 补漏):<c>Func&lt;string,float&gt; f = Input.GetAxis;</c>
        /// 是符号引用但非调用 —— 补注册 MethodReference 后必须拒。</summary>
        [Test]
        public void test_legacy_input_gate_analyzer_rejects_method_group_legacy_input()
        {
            // Arrange
            const string sample =
                "using System;\n" +
                "using UnityEngine;\n" +
                "public static class BadMethodGroup\n" +
                "{\n" +
                "    public static float Run()\n" +
                "    {\n" +
                "        Func<string, float> axis = Input.GetAxis;\n" +
                "        return axis(\"Horizontal\");\n" +
                "    }\n" +
                "}\n";

            // Act
            var (exitCode, output) = RunCscWithAnalyzer("bad_method_group.cs", sample);

            // Assert
            Assert.That(exitCode, Is.Not.EqualTo(0), "方法组形态的 Legacy Input 引用必须编译失败:\n" + output);
            Assert.That(output, Does.Contain("DY0001"), "失败必须由 DY0001 报出:\n" + output);
        }

        /// <summary>AC-3-A4② typeof 形态(2026-09-25 复核补漏):typeof(UnityEngine.Input)
        /// 是类型符号引用,成员判定不覆盖,须单独拒。</summary>
        [Test]
        public void test_legacy_input_gate_analyzer_rejects_typeof_legacy_input()
        {
            // Arrange
            const string sample =
                "using System;\n" +
                "public static class BadTypeOf\n" +
                "{\n" +
                "    public static Type T() { return typeof(UnityEngine.Input); }\n" +
                "}\n";

            // Act
            var (exitCode, output) = RunCscWithAnalyzer("bad_typeof.cs", sample);

            // Assert
            Assert.That(exitCode, Is.Not.EqualTo(0), "typeof 形态的 Legacy Input 引用必须编译失败:\n" + output);
            Assert.That(output, Does.Contain("DY0001"), "失败必须由 DY0001 报出:\n" + output);
        }

        /// <summary>AC-3-A4② 别名形态:using 导入 + using 别名两种换名写法均须拒(语义符号判定,非文本前缀)。</summary>
        [Test]
        public void test_legacy_input_gate_analyzer_rejects_using_and_alias_legacy_input()
        {
            // Arrange
            const string sample =
                "using UnityEngine;\n" +
                "using LegacyInput = UnityEngine.Input;\n" +
                "public static class BadUsingAndAlias\n" +
                "{\n" +
                "    public static float Run() => Input.GetAxis(\"Vertical\") + LegacyInput.mousePosition.x;\n" +
                "}\n";

            // Act
            var (exitCode, output) = RunCscWithAnalyzer("bad_using_and_alias.cs", sample);

            // Assert
            Assert.That(exitCode, Is.Not.EqualTo(0), "using/别名形态同样必须编译失败:\n" + output);
            Assert.That(output, Does.Contain("DY0001"), "失败必须由 DY0001 报出:\n" + output);
        }

        /// <summary>AC-3-A4② 零误报(负向夹具):只 using UnityEngine 但不触 Input、
        /// 以及合法 Input System 写法(InputAction.ReadValue)⇒ 编译通过、无 DY0001。</summary>
        [Test]
        public void test_legacy_input_gate_analyzer_allows_input_action_usage()
        {
            // Arrange
            const string sample =
                "using UnityEngine;\n" +
                "using UnityEngine.InputSystem;\n" +
                "public static class GoodInputActionUsage\n" +
                "{\n" +
                "    public static float Run(InputAction action)\n" +
                "    {\n" +
                "        Vector3 offset = new Vector3();\n" +
                "        return action.ReadValue<float>() + offset.x;\n" +
                "    }\n" +
                "}\n";

            // Act
            var (exitCode, output) = RunCscWithAnalyzer("good_input_action.cs", sample);

            // Assert
            Assert.That(exitCode, Is.EqualTo(0), "合法 Input System 写法不得被误报:\n" + output);
            Assert.That(output, Does.Not.Contain("DY0001"), "合法样例不得出现 DY0001:\n" + output);
        }

        // ══════════ 子进程编译装置(AC-3-A4② 的执行体;不破坏工程编译)══════════

        /// <summary>桩程序集源:自备 UnityEngine.Input(Legacy)/ KeyCode / Vector3 与
        /// UnityEngine.InputSystem.InputAction —— 样例编译无需真 UnityEngine,
        /// 分析器按「UnityEngine.Input 类型符号」判定(语义),桩与真类型同名同命名空间即等价生效。</summary>
        private const string StubSource =
            "namespace UnityEngine\n" +
            "{\n" +
            "    public enum KeyCode { Space = 32 }\n" +
            "    public struct Vector3 { public float x; public float y; public float z; }\n" +
            "    public static class Input\n" +
            "    {\n" +
            "        public static bool GetKey(KeyCode key) { return false; }\n" +
            "        public static bool GetKeyDown(KeyCode key) { return false; }\n" +
            "        public static float GetAxis(string axisName) { return 0f; }\n" +
            "        public static bool anyKeyDown { get { return false; } }\n" +
            "        public static Vector3 mousePosition { get { return default(Vector3); } }\n" +
            "    }\n" +
            "}\n" +
            "namespace UnityEngine.InputSystem\n" +
            "{\n" +
            "    public class InputAction\n" +
            "    {\n" +
            "        public TValue ReadValue<TValue>() { return default(TValue); }\n" +
            "    }\n" +
            "}\n";

        private static string SelfSourcePath([CallerFilePath] string callerPath = "") => callerPath;

        /// <summary>从 EditorApplication.applicationPath 上溯定位捆绑 dotnet 与 csc
        /// (逐级试 Data/、Tools/ 两候选,兼容 Linux / Windows / macOS 安装布局 —— 跨机器可移植)。</summary>
        private static (string dotnet, string cscDll, string editorData) LocateUnityToolchain()
        {
            string dir = Path.GetDirectoryName(EditorApplication.applicationPath);
            while (!string.IsNullOrEmpty(dir))
            {
                foreach (string sub in new[] { "Data", "Tools" })
                {
                    string data = Path.Combine(dir, sub);
                    string csc = Path.Combine(data, "DotNetSdkRoslyn", "csc.dll");
                    if (!File.Exists(csc))
                        continue;
                    string dotnetUnix = Path.Combine(data, "NetCoreRuntime", "dotnet");
                    string dotnetWin = dotnetUnix + ".exe";
                    string dotnet = File.Exists(dotnetUnix) ? dotnetUnix
                        : File.Exists(dotnetWin) ? dotnetWin : null;
                    if (dotnet != null)
                        return (dotnet, csc, data);
                }
                dir = Path.GetDirectoryName(dir);
            }
            Assert.Fail("找不到捆绑工具链(以 applicationPath 上溯,试 Data/ 与 DotNetSdkRoslyn/):" +
                         EditorApplication.applicationPath);
            return default;
        }

        /// <summary>用捆绑 csc 编译样例(引用 NetCoreRuntime 框架程序集 + -analyzer 挂分析器 DLL)。
        /// 返回 (退出码, 合并输出)。工作目录固定于临时目录,每测先清后建(隔离 + 自清理)。</summary>
        private static (int exitCode, string output) RunCscWithAnalyzer(string sampleFileName, string sampleSource)
        {
            var (dotnet, cscDll, editorData) = LocateUnityToolchain();

            // 隔离:测试全名 + 进程 id(2026-09-25 复核 W6/F3:常量目录在并行/多实例下互删;
            // 用测试名而非 Guid,保日志可复现)
            string safeName = TestContext.CurrentContext.Test.FullName;
            foreach (char c in Path.GetInvalidFileNameChars())
                safeName = safeName.Replace(c, '_');
            string workDir = Path.Combine(Path.GetTempPath(),
                "dyc_story001_analyzer_" + Process.GetCurrentProcess().Id + "_" + safeName);
            if (Directory.Exists(workDir))
                Directory.Delete(workDir, true);
            Directory.CreateDirectory(workDir);
            try
            {
                // 框架引用集:NetCoreRuntime/shared/Microsoft.NETCore.App/<version>/*.dll(与 csc 宿主同运行时)
                string sharedRoot = Path.Combine(editorData, "NetCoreRuntime", "shared", "Microsoft.NETCore.App");
                Assert.That(Directory.Exists(sharedRoot), Is.True, "缺 shared/Microsoft.NETCore.App:" + sharedRoot);
                // 取最高版本(与 build.sh 的 sort -V | tail -1 同策略 —— 2026-09-25 复核 N4:原为 Ordinal 取最低,两侧相反)
                string frameworkDir = Directory.GetDirectories(sharedRoot)
                    .OrderBy(d => System.Version.TryParse(
                            Path.GetFileName(d.TrimEnd(Path.DirectorySeparatorChar, '/')), out var v)
                        ? v : new Version(0, 0))
                    .Last();
                string[] frameworkRefs = Directory.GetFiles(frameworkDir, "*.dll", SearchOption.TopDirectoryOnly);

                string rsp = Path.Combine(workDir, "refs.rsp");
                var rspLines = new List<string>();
                foreach (string dll in frameworkRefs)
                    rspLines.Add("-r:\"" + dll + "\"");
                File.WriteAllLines(rsp, rspLines);

                string stubPath = Path.Combine(workDir, "stub.cs");
                string samplePath = Path.Combine(workDir, sampleFileName);
                string outDll = Path.Combine(workDir, "sample_out.dll");
                File.WriteAllText(stubPath, StubSource);
                File.WriteAllText(samplePath, sampleSource);

                var psi = new ProcessStartInfo
                {
                    FileName = dotnet,
                    Arguments = "exec \"" + cscDll + "\" -nologo -target:library -nostdlib+" +
                                " -out:\"" + outDll + "\"" +
                                " -analyzer:\"" + AnalyzerDllOnDisk + "\"" +
                                " @\"" + rsp + "\" \"" + stubPath + "\" \"" + samplePath + "\"",
                    WorkingDirectory = workDir,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };

                using (var proc = Process.Start(psi))
                {
                    Assert.That(proc, Is.Not.Null, "Process.Start 失败:" + dotnet);
                    var stdoutTask = proc.StandardOutput.ReadToEndAsync();
                    var stderrTask = proc.StandardError.ReadToEndAsync();
                    bool exited = proc.WaitForExit(60_000);
                    if (!exited)
                    {
                        try { proc.Kill(); } catch { /* 已退出则忽略 */ }
                        Assert.Fail("csc 子进程超时(60s):" + psi.Arguments);
                    }
                    string output = stdoutTask.GetAwaiter().GetResult() + stderrTask.GetAwaiter().GetResult();
                    return (proc.ExitCode, output);
                }
            }
            finally
            {
                try { Directory.Delete(workDir, true); } catch { /* 清理失败不掩盖断言 */ }
            }
        }
    }
}
