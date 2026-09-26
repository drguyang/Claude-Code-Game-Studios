// 权威来源:production/epics/audio-system/story-003-mixer-topology-snapshots.md(4 条 AC)
//   · AC-44-C1 —— AudioMixerSnapshot.TransitionTo(+ AudioMixer.TransitionToSnapshots,2026-09-26
//     unity-specialist 裁定纳入同一白名单,防旁路)全部调用点(IL/Cecil 扫描)∈ 快照切换调用点
//     白名单;负向夹具 = 体征 handler 调 TransitionTo(DialogueFocus) ⇒ 红;
//     DialogueFocus 调用点仅来自玩家对话入口方法(按快照字段归属断言)
//   · AC-44-E3 —— .mixer 资产扫描:① 七总线组齐备 ② Aux/Reverb send 拓扑清单单一出处
//     (新增未登记 send = 构建失败)③ reverb preset 切换所有者 = 44(触发输入 = 房间格,
//     表现层派生,不进流)
//   · 注册表 AC② —— 快照参数集 ∩ 玩家 exposed 集 = ∅(两级组结构;Force Text YAML 断言)
//   · 注册表 AC③ —— 注册表总线部分 == 七总线(计数 = 7,含 Master)+ GDD 表对账(7 行 ↔ 7 员)
// TR-audio-003(混音拓扑单一出处)· TR-audio-004(DialogueFocus 非拟物例外通道)
// ADR-018 §三(七总线 · 快照禁播报 · Aux/Reverb send)· ADR-020 §七(AudioListener 引用)
//
// ⚠️ 落点:故事 Test Evidence 登记路径 = unity/Assets/Tests/EditMode/Audio/mixer_topology_test.cs
//    (本文件即真身;账本互链 = tests/integration/audio_system/README.md Story 003 段)。
// ⚠️ 读法纪律:.mixer 是 Force Text YAML ⇒ 测试**手写文本扫描**(逐行状态机,不引 Newtonsoft /
//    第三方解析器,承 Story 002 同一纪律);多文档格式 `--- !u!<classId> &<fileID>`,
//    层级断言**沿 fileID 树**(m_MasterGroup → m_Children),不用 m_Name 计数冒充层级
//    (2026-09-26 unity-specialist 裁定)。
// ⚠️ IL 扫描 = Mono.Cecil + ReadingMode.Deferred(承 AssemblyGates b5 实测:SRM 在本工程
//    编译不过,勿用 System.Reflection.Metadata);谓词 = declaring type =
//    UnityEngine.Audio.AudioMixerSnapshot + 方法名 TransitionTo,并显式扫
//    AudioMixer.TransitionToSnapshots。
// ⚠️ 负向夹具落位:文件末命名空间块(44 前缀下、住测试装配)—— 真编译产物真 IL,
//    不污染生产扫描面;YAML 负例住 tests/integration/audio_system/fixtures/。
// ⚠️ 纪律:test_* 命名 · arrange/act/assert · 无随机 · 无时间依赖 · 零网络 I/O;
//    夹具 / GDD / .mixer 读取前置 File.Exists 断言(缺失即红,不静默跳过)。
// ⚠️ 未决(登记在交付报告):.mixer 资产由 MixerAssetGenerator(编辑器 batch)生成,
//    生成前 test_realMixerAsset_* 与 YAML 正例依赖的真资产缺失 ⇒ **预期红**(缺失即红纪律)。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using DaYiJingCheng.EditorTools.Gates;
using DaYiJingCheng.Gameplay.Presentation.Audio;

namespace DaYiJingCheng.Tests.Unit.Audio
{
    /// <summary>Story 003 混音拓扑与快照纪律(AC-44-C1 / AC-44-E3 / 注册表 AC②③)的 EditMode 测试。</summary>
    [TestFixture]
    internal sealed class MixerTopologyTest
    {
        /// <summary>.mixer 资产落点(与 MixerAssetGenerator 输出路径同一常量面;缺失即红)。</summary>
        private const string MixerAssetPath = "Assets/Audio/DaYiJingCheng.mixer";

        // ══════════════ AC-44-C1 正例:当前树全部调用点 ∈ 白名单 ══════════════

        /// <summary>AC-44-C1 正例:扫描三个运行期 gameplay 装配的全部快照切换调用点 ⇒ 每点 ∈ 登记集;
        /// 且调用点非空(空集不豁免 —— 扫不到 = 判据空转)。</summary>
        [Test]
        public void test_transitionCallsites_currentTree_allWhitelisted()
        {
            // Arrange
            Assert.That(EditorUtility.scriptCompilationFailed, Is.False,
                "跑扫描前提:编译成功(拒扫陈旧产物 —— 假绿防护,承 b5 同格)");

            // Act
            IReadOnlyList<string> scanErrors = MixerTopologyGates.ScanProjectTransitionCallsites(
                out List<MixerTopologyGates.TransitionCallsite> callsites);
            IReadOnlyList<string> verdicts = MixerTopologyGates.ValidateTransitionCallsites(callsites);

            // Assert
            Assert.That(scanErrors, Is.Empty, () => string.Join("\n", scanErrors));
            Assert.That(verdicts, Is.Empty,
                "全部 TransitionTo / TransitionToSnapshots 调用点必须 ∈ 快照切换调用点白名单:\n" +
                string.Join("\n", verdicts));
            Assert.That(callsites.Count, Is.GreaterThanOrEqualTo(6),
                "调用点数下限(6 = SnapshotDirector 5 + ReverbPresetSwitcher 1)—— 0 命中 = 扫描键失配的假绿");
        }

        // ══════════════ AC-44-C1 负例:体征 handler 调用点必红 ══════════════

        /// <summary>AC-44-C1 负向夹具:对**测试装配**产物跑同一谓词 —— 装内
        /// <c>TransitionNegativeFixture.OnVitalsChanged</c>(体征/病程变更 handler 调
        /// <c>TransitionTo</c>)⇒ 断言失败(红),且错误点名夹具与 AC。</summary>
        [Test]
        public void test_transitionCallsites_stateHandlerFixture_red()
        {
            // Arrange
            string dll = AssemblyGates.ScriptAssemblyPath("Sim.Contracts.Tests");
            Assert.That(File.Exists(dll), Is.True, $"测试装配产物缺失:{dll}");

            // Act
            IReadOnlyList<string> scanErrors = MixerTopologyGates.ScanTransitionCallsites(
                dll, out List<MixerTopologyGates.TransitionCallsite> callsites);
            IReadOnlyList<string> verdicts = MixerTopologyGates.ValidateTransitionCallsites(callsites);

            // Assert
            Assert.That(scanErrors, Is.Empty, () => string.Join("\n", scanErrors));
            Assert.That(callsites.Any(c => c.Method == "OnVitalsChanged"), Is.True,
                "负例 fixture 必须落进扫描面(落空 = 假绿)");
            Assert.That(verdicts, Is.Not.Empty, "非拟物状态 handler 的调用点必须红(AC-44-C1)");
            Assert.That(verdicts.Any(v => v.Contains("OnVitalsChanged") && v.Contains("AC-44-C1")),
                Is.True, () => string.Join("\n", verdicts));
        }

        /// <summary>AC-44-C1 扫描面守卫:产物缺失 ⇒ 报错,不以空集冒充绿。</summary>
        [Test]
        public void test_transitionCallsites_missingAssembly_red()
        {
            // Act
            IReadOnlyList<string> scanErrors = MixerTopologyGates.ScanTransitionCallsites(
                "/nonexistent/NoSuchAssembly.dll",
                out List<MixerTopologyGates.TransitionCallsite> callsites);

            // Assert
            Assert.That(scanErrors, Is.Not.Empty, "产物缺失必须红(拒以空集冒充绿)");
            Assert.That(scanErrors[0], Does.Contain("产物缺失"));
            Assert.That(callsites, Is.Empty);
        }

        /// <summary>AC-44-C1 DialogueFocus 半边:生产扫描中 <c>_dialogueFocus</c> 字段被快照切换
        /// 读取的位置 ⊆ 玩家对话入口方法(<c>BeginDialogueFocus</c> / <c>EndDialogueFocus</c>);
        /// 且该字段至少被命中一次(判据空转守卫)。</summary>
        [Test]
        public void test_dialogueFocus_callsiteFieldOwnership_onlyPlayerEntry()
        {
            // Arrange
            IReadOnlyList<string> scanErrors = MixerTopologyGates.ScanProjectTransitionCallsites(
                out List<MixerTopologyGates.TransitionCallsite> callsites);
            Assert.That(scanErrors, Is.Empty, () => string.Join("\n", scanErrors));

            // Act
            List<MixerTopologyGates.TransitionCallsite> dialogueCallsites = callsites
                .Where(c => c.SnapshotField == MixerTopologyGates.DialogueSnapshotField)
                .ToList();
            IReadOnlyList<string> verdicts =
                MixerTopologyGates.ValidateSnapshotFieldOwnership(callsites);

            // Assert
            Assert.That(dialogueCallsites, Is.Not.Empty,
                $"{MixerTopologyGates.DialogueSnapshotField} 必须至少出现在一处调用点(判据空转 = 假绿)");
            Assert.That(verdicts, Is.Empty, () => string.Join("\n", verdicts));
            Assert.That(dialogueCallsites.All(c =>
                    c.Method == "BeginDialogueFocus" || c.Method == "EndDialogueFocus"),
                Is.True, "DialogueFocus 调用点仅来自玩家对话入口方法(病人自发呻吟/咳嗽不触发 —— 规则三)");
        }

        /// <summary>白名单反向守卫:登记集每条规则的 <c>Type::Method</c> 必须真实存在于
        /// Gameplay.Presentation(死条目 = 白名单腐化,两向都断)。</summary>
        [Test]
        public void test_whitelistRules_allExistInPresentationAssembly()
        {
            // Arrange
            var presentation = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == AssemblyGates.PresentationAssemblyName);
            Assert.That(presentation, Is.Not.Null, "Gameplay.Presentation 必须已加载");

            // Act & Assert:逐条规则解析 Type::Method 并反射取证
            foreach (MixerTopologyGates.SnapshotCallsiteRule rule in MixerTopologyGates.TransitionCallsiteRules)
            {
                string[] parts = rule.Callsite.Split(new[] { "::" }, StringSplitOptions.None);
                Assert.That(parts, Has.Length.EqualTo(2), $"规则格式须为 Type::Method:{rule.Callsite}");
                Type type = presentation.GetType(parts[0], throwOnError: false);
                Assert.That(type, Is.Not.Null, $"白名单死条目(类型不存在):{rule.Callsite}");
                System.Reflection.MethodInfo method = type.GetMethod(
                    parts[1],
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static);
                Assert.That(method, Is.Not.Null, $"白名单死条目(方法不存在):{rule.Callsite}");
            }
            Assert.That(MixerTopologyGates.TransitionCallsiteRules.Count, Is.GreaterThanOrEqualTo(6),
                "白名单条目数下限漂移监控(6 = 5 玩家行为 + 1 世界可感知声)");
        }

        /// <summary>AC-44-C1 源文本层:反射字符串形态的快照切换(绕过 IL 直调的旁路)= 红
        /// (承 b5①b 同格 —— 字符串在 Cecil 面只是 ldstr)。</summary>
        [Test]
        public void test_transitionReflectionStringLiteral_reportsRed()
        {
            // Arrange
            var lines = new[]
            {
                "var m = (AudioMixer)typeof(AudioMixer).GetMethod(\"TransitionToSnapshots\").Invoke(o, a);",
                "typeof(AudioMixerSnapshot).GetMethod(\"TransitionTo\");",
            };

            // Act
            IReadOnlyList<string> errors =
                MixerTopologyGates.ValidateTransitionReflectionStrings("reflect.cs", lines);

            // Assert
            Assert.That(errors.Count, Is.EqualTo(2), () => string.Join("\n", errors));
            Assert.That(errors.All(e => e.Contains("AC-44-C1")), Is.True);

            // 对照:普通方法调用(非字符串字面量)不误杀
            Assert.That(
                MixerTopologyGates.ValidateTransitionReflectionStrings("ok.cs",
                    new[] { "_dialogueFocus.TransitionTo(_transitionSeconds); " }),
                Is.Empty, "方法直调不进反射字符串面(IL 层由 Cecil 扫描覆盖)");
        }

        // ══════════════ AC-44-E3:.mixer 资产扫描 ══════════════

        /// <summary>AC-44-E3 正例:合法拓扑夹具(七总线 + 两级组 + send 清单 + 快照五员 +
        /// exposed = 注册表)⇒ 总门零错误。</summary>
        [Test]
        public void test_mixerYaml_validTopology_zeroErrors()
        {
            // Arrange
            string yaml = readFixture("valid_mixer_topology.yaml");

            // Act
            IReadOnlyList<string> errors = MixerTopologyGates.ValidateMixerTopology(yaml);

            // Assert
            Assert.That(errors, Is.Empty, "合法拓扑必须全绿:\n" + string.Join("\n", errors));
        }

        /// <summary>AC-44-E3 ① 负例:总线缺失(Stethoscope 组改名)⇒ 红,且诊断点名该总线。</summary>
        [Test]
        public void test_mixerYaml_missingBusGroup_reportsRed()
        {
            // Arrange
            string yaml = readFixture("invalid_missing_bus.yaml");

            // Act
            IReadOnlyList<string> errors = MixerTopologyGates.ValidateMixerTopology(yaml);

            // Assert
            Assert.That(errors.Any(e => e.Contains("七总线") && e.Contains("Stethoscope")),
                Is.True, "七总线缺 Stethoscope 必红:\n" + string.Join("\n", errors));
        }

        /// <summary>AC-44-E3 ② 负例:新增未登记 send ⇒ 红(拓扑清单 = 单一出处)。</summary>
        [Test]
        public void test_mixerYaml_unregisteredSend_reportsRed()
        {
            // Arrange
            string yaml = readFixture("invalid_unregistered_send.yaml");

            // Act
            IReadOnlyList<string> errors = MixerTopologyGates.ValidateMixerTopology(yaml);

            // Assert(2026-09-26 样例校准:send 真格式无名称字段,诊断键 = 源组 → 目标组;
            //  夹具经 SFX 的 effect m_SendTarget 注入 SFX → Reverb 这一条未登记 send)
            Assert.That(errors.Any(e => e.Contains("SFX → Reverb") && e.Contains("未登记")),
                Is.True, "未登记 send 必红(AC-44-E3 ②):\n" + string.Join("\n", errors));
        }

        /// <summary>AC-44-E3 ③ 正例:reverb preset 切换所有者 = 44 命名空间(反射取证)。</summary>
        [Test]
        public void test_reverbSwitcher_ownerIsAudio44Namespace()
        {
            // Arrange
            Type switcher = typeof(ReverbPresetSwitcher);

            // Act
            IReadOnlyList<string> errors = MixerTopologyGates.ValidateReverbSwitchOwner(switcher.Namespace);

            // Assert
            Assert.That(errors, Is.Empty, () => string.Join("\n", errors));
            Assert.That(switcher.Namespace, Is.EqualTo(AssemblyGates.AudioModuleNamespacePrefix),
                "reverb preset 切换所有者 = 44(AC-44-E3 ③;触发输入 = 房间格,表现层派生,不进流)");
        }

        /// <summary>AC-44-E3 ③ 负例:非 44 命名空间持有 reverb 切换 ⇒ 红(纯函数面)。</summary>
        [Test]
        public void test_reverbSwitcher_foreignOwner_reportsRed()
        {
            // Act
            IReadOnlyList<string> errors =
                MixerTopologyGates.ValidateReverbSwitchOwner("DaYiJingCheng.Gameplay.UI");

            // Assert
            Assert.That(errors.Count, Is.EqualTo(1), () => string.Join("\n", errors));
            Assert.That(errors[0], Does.Contain("AC-44-E3"));
        }

        /// <summary>真资产(API 层 + YAML 层 + 初始化器):缺失即红 —— 由 MixerAssetGenerator
        /// batch 生成后转绿(2026-09-26 交付报告登记的预期红项)。</summary>
        [Test]
        public void test_realMixerAsset_presentApiLayerAndValid()
        {
            // Arrange:文件存在性(缺失即红,不静默跳过)
            string absolutePath = Path.Combine(Application.dataPath, "Audio", "DaYiJingCheng.mixer");
            Assert.That(File.Exists(absolutePath),
                $".mixer 资产缺失:{absolutePath}(跑 MixerAssetGenerator.Create batch 生成)");
            Assert.That(File.Exists(absolutePath), Is.True, $"Force Text YAML 文件不可读:{MixerAssetPath}");

            // Act(API 层):AssetDatabase 加载 + 组 / 快照枚举
            AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerAssetPath);
            Assert.That(mixer, Is.Not.Null, $"AssetDatabase 加载失败:{MixerAssetPath}");
            string[] groupNames = mixer.FindMatchingGroups("")
                .Where(g => g != null).Select(g => g.name).ToArray(); // UnityEngine.Object.name(小写)
            List<string> apiErrors = new List<string>();
            foreach (string bus in MixerRegistry.BusNames)
                if (!groupNames.Contains(bus))
                    apiErrors.Add($"API 层缺总线组「{bus}」");
            foreach (string bus in MixerRegistry.BusNames)
            {
                if (!groupNames.Contains(MixerRegistry.VolumeGroupForBus(bus)))
                    apiErrors.Add($"API 层缺玩家音量组「{MixerRegistry.VolumeGroupForBus(bus)}」");
                if (!groupNames.Contains(MixerRegistry.DuckGroupForBus(bus)))
                    apiErrors.Add($"API 层缺快照 duck 组「{MixerRegistry.DuckGroupForBus(bus)}」");
            }
            foreach (string snapshotName in MixerRegistry.SnapshotNames)
                if (mixer.FindSnapshot(snapshotName) == null)
                    apiErrors.Add($"API 层缺快照「{snapshotName}」");

            // Act(YAML 层):Force Text 原文沿 fileID 树断言
            string yaml = File.ReadAllText(absolutePath);
            IReadOnlyList<string> yamlErrors = MixerTopologyGates.ValidateMixerTopology(yaml);

            // Act(初始化器):7 个玩家参数各 SetFloat 一次的键覆盖校验
            IReadOnlyList<string> defaultKeys = MixerRegistry.BusVolumeParameters;
            IReadOnlyList<string> initErrors = MixerTopologyGates.ValidatePlayerVolumeDefaults(defaultKeys);

            // Assert
            Assert.That(apiErrors, Is.Empty, "API 层:\n" + string.Join("\n", apiErrors));
            Assert.That(yamlErrors, Is.Empty, "YAML 层:\n" + string.Join("\n", yamlErrors));
            Assert.That(initErrors, Is.Empty, () => string.Join("\n", initErrors));

            // AC② **非空转守卫**:捕获键必须真解析到组名,否则交集恒 ∅ = 换一种方式空转。
            // (真资产捕获键是组 m_Volume 哈希 —— 解析失败会静默放过,与「检查通过」不可区分;
            //  2026-09-26 QA 点名的假绿形态,此处钉死。)
            int resolved = MixerTopologyGates.CountResolvedCaptureKeys(yaml);
            Assert.That(resolved, Is.GreaterThan(0),
                "AC② 空转守卫:真资产的快照捕获键必须至少有一个能解析到组名 —— " +
                "解析为 0 意味着交集断言在对空集做检查(假绿)");
        }

        /// <summary>两级组结构 + 交集 ∅(注册表 AC② 正例):duck 组 / 玩家组两级且
        /// 快照捕获 ∩ 玩家 exposed = ∅(对合法夹具单跑该判据,归因独立于总门)。</summary>
        [Test]
        public void test_snapshotCaptures_disjointFromPlayerExposed_zeroErrors()
        {
            // Arrange
            string yaml = readFixture("valid_mixer_topology.yaml");

            // Act
            IReadOnlyList<string> errors = MixerTopologyGates.ValidateSnapshotExposedDisjoint(yaml);

            // Assert
            Assert.That(errors, Is.Empty, () => string.Join("\n", errors));
        }

        /// <summary>注册表 AC② 负例:把 <c>bus_volume_music</c> 加进 StethoscopeFocus 快照捕获
        /// ⇒ 红(快照不得触碰玩家音量组 —— 出入快照打回玩家值的结构性堵点)。</summary>
        [Test]
        public void test_snapshotCapture_playerVolume_reportsRed()
        {
            // Arrange
            string yaml = readFixture("invalid_snapshot_captures_player_volume.yaml");

            // Act
            IReadOnlyList<string> errors = MixerTopologyGates.ValidateSnapshotExposedDisjoint(yaml);

            // Assert
            Assert.That(errors, Is.Not.Empty, "快照捕获玩家 exposed 参数必红(注册表 AC②)");
            Assert.That(errors.All(e => e.Contains("bus_volume_music") && e.Contains("AC②")),
                Is.True, () => string.Join("\n", errors));
        }

        /// <summary>两级组结构负例:单级总线(缺 duck_music 组)⇒ 红。</summary>
        [Test]
        public void test_singleLevelBus_reportsRed()
        {
            // Arrange
            string yaml = readFixture("invalid_single_level_bus.yaml");

            // Act
            IReadOnlyList<string> errors = MixerTopologyGates.ValidateMixerTopology(yaml);

            // Assert
            Assert.That(errors.Any(e => e.Contains("duck_music") && e.Contains("两级")),
                Is.True, "每总线须拆 duck 组 + 玩家组两级:\n" + string.Join("\n", errors));
        }

        /// <summary>快照五员表负例:缺 <c>DialogueFocus</c> ⇒ 红(GDD §States 五员:Default /
        /// StethoscopeFocus / DialogueFocus / Paused / VRComfort)。</summary>
        [Test]
        public void test_snapshotRoster_missingDialogueFocus_reportsRed()
        {
            // Arrange:从合法夹具移除 DialogueFocus 快照文档(整段三行)
            string yaml = readFixture("valid_mixer_topology.yaml");
            int start = yaml.IndexOf("--- !u!245 &3002", StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), "夹具自证:DialogueFocus 文档在场");
            int end = yaml.IndexOf("--- !u!", start + 4, StringComparison.Ordinal);
            string sabotaged = yaml.Remove(start, (end < 0 ? yaml.Length : end) - start);
            Assert.That(sabotaged, Does.Not.Contain("m_Name: DialogueFocus"), "夹具自证:已移除");

            // Act
            IReadOnlyList<string> errors = MixerTopologyGates.ValidateMixerTopology(sabotaged);

            // Assert
            Assert.That(errors.Any(e => e.Contains("DialogueFocus") && e.Contains("快照")),
                Is.True, "缺 DialogueFocus 快照必红:\n" + string.Join("\n", errors));
        }

        // ══════════════ 44 源纪律:SetFloat 初始化面 / 禁 ClearFloat ══════════════

        /// <summary>两级组纪律负例:44 源出现 <c>ClearFloat</c>(把玩家参数交还快照控制)= 红;
        /// 正例半边 = 当前 44 源树零命中。</summary>
        [Test]
        public void test_clearFloat_playerParamsNeverCleared()
        {
            // Arrange:合成负例(免写坏生产源)
            IReadOnlyList<string> synthetic = MixerTopologyGates.ValidateNoClearFloat(
                "synth.cs", new[] { "mixer.ClearFloat(\"bus_volume_music\");" });
            Assert.That(synthetic.Count, Is.EqualTo(1), () => string.Join("\n", synthetic));
            Assert.That(synthetic[0], Does.Contain("ClearFloat"));

            // Act:当前 44 源树(存在即扫,缺失即红)
            string root = Path.Combine(repoRoot(), "unity", "Assets", "Gameplay.Presentation");
            Assert.That(Directory.Exists(root), Is.True, $"44 源树缺失:{root}");
            var violations = new List<string>();
            foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string text = File.ReadAllText(file);
                if (!text.Contains("namespace " + AssemblyGates.AudioModuleNamespacePrefix))
                    continue;
                violations.AddRange(MixerTopologyGates.ValidateNoClearFloat(
                    file, text.Replace("\r\n", "\n").Split('\n')));
            }

            // Assert
            Assert.That(violations, Is.Empty, () => string.Join("\n", violations));
        }

        // ══════════════ 注册表 AC③:总线注册表计数 + GDD 对账 ══════════════

        /// <summary>注册表 AC③ 正例:<c>bus_volume_*</c> 常量集计数 = 7(含 master),且与
        /// 七总线名 1:1。</summary>
        [Test]
        public void test_busVolumeRegistry_sevenIncludingMaster()
        {
            // Act
            IReadOnlyList<string> errors = MixerTopologyGates.ValidateBusRegistryCounts(
                MixerRegistry.BusVolumeParameters, MixerRegistry.BusNames);

            // Assert
            Assert.That(errors, Is.Empty, () => string.Join("\n", errors));
            Assert.That(MixerRegistry.BusVolumeParameters.Count, Is.EqualTo(7)); // 静态型 IReadOnlyList 但运行时是 string[] ⇒ NUnit Has.Count 反射不到 Count 属性
            Assert.That(MixerRegistry.BusNames.Count, Is.EqualTo(7));
            Assert.That(MixerRegistry.BusVolumeParameters, Does.Contain("bus_volume_master"));
            Assert.That(MixerRegistry.BusNames, Does.Contain("Master"));
        }

        /// <summary>注册表 AC③ 负例:喂 6 条(漏 Master)⇒ 红(纯函数面)。</summary>
        [Test]
        public void test_busVolumeRegistry_missingMasterRow_reportsRed()
        {
            // Arrange
            var sixParams = MixerRegistry.BusVolumeParameters.Where(p => p != "bus_volume_master")
                .ToList();
            var sixBuses = MixerRegistry.BusNames.Where(b => b != "Master").ToList();

            // Act
            IReadOnlyList<string> errors = MixerTopologyGates.ValidateBusRegistryCounts(sixParams, sixBuses);

            // Assert
            Assert.That(errors, Is.Not.Empty, "漏 Master 行必红(原 Tuning 行漏 Master 已补的守卫)");
            Assert.That(errors.Any(e => e.Contains("7")), Is.True, () => string.Join("\n", errors));
        }

        /// <summary>注册表 AC③ 对账正例:GDD §UI Requirements 表(<c>audio-system.md:896</c>
        /// 语义源)的 <c>bus_volume_*</c> 行集 == C# 常量 7 员。</summary>
        [Test]
        public void test_gddBusRegistry_reconcilesWithConstants()
        {
            // Arrange
            string gddPath = Path.Combine(repoRoot(), "design", "gdd", "audio-system.md");
            Assert.That(File.Exists(gddPath), Is.True, $"GDD 缺失:{gddPath}");
            string[] lines = File.ReadAllText(gddPath).Replace("\r\n", "\n").Split('\n');

            // Act
            IReadOnlyList<string> errors = MixerTopologyGates.ValidateGddBusRegistryReconciliation(
                lines, MixerRegistry.BusVolumeParameters);

            // Assert
            Assert.That(errors, Is.Empty,
                "GDD 7 行 ↔ 常量 7 员对账失败:\n" + string.Join("\n", errors));
        }

        /// <summary>注册表 AC③ 对账负例:GDD 表缺一行(合成行集)⇒ 红。</summary>
        [Test]
        public void test_gddBusRegistry_missingRow_reportsRed()
        {
            // Arrange
            var gddLines = new[]
            {
                "| **`bus_volume_master`** | 总音量 |",
                "| **`bus_volume_music`** | 音乐 |",
                "| **`bus_volume_ambience`** | 环境 |",
                "| **`bus_volume_voice`** | 语声 |",
                "| **`bus_volume_sfx`** | 音效 |",
                "| **`bus_volume_stethoscope`** | 听诊 |",
                // 缺 bus_volume_uicue 行
            };

            // Act
            IReadOnlyList<string> errors = MixerTopologyGates.ValidateGddBusRegistryReconciliation(
                gddLines, MixerRegistry.BusVolumeParameters);

            // Assert
            Assert.That(errors, Is.Not.Empty, "GDD 缺 bus_volume_uicue 行必红");
            Assert.That(errors.Any(e => e.Contains("bus_volume_uicue")), Is.True,
                () => string.Join("\n", errors));
        }

        // ══════════════ 共用小件 ══════════════

        private static string readFixture(string fileName)
        {
            string path = Path.Combine(repoRoot(), "tests", "integration", "audio_system",
                "fixtures", fileName);
            Assert.That(File.Exists(path), Is.True, $"夹具缺失:{path}");
            return File.ReadAllText(path);
        }

        // 本文件在 unity/Assets/Tests/EditMode/Audio/ ⇒ Audio→EditMode→Tests→Assets→unity→仓库根 = 5 层
        // (原写 4 层 ⇒ root 落在 unity/ 下,夹具 / GDD / 源树全部报「缺失」—— 一个 bug 拖红 9 条)
        private static string repoRoot([CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile) ?? ".", "..", "..", "..", "..", ".."));
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// AC-44-C1 端到端负例:体征 / 病程变更 handler 调 TransitionTo(DialogueFocus)。
// 住 44 前缀命名空间之下(对本装配 dll 跑同一 ScanTransitionCallsites 谓词 ⇒ 真 IL 必红);
// **刻意不在生产树注入**(会把工程语义搞红),也不新增 asmdef(b3 封闭性拒),
// 借测试装配 Sim.Contracts.Tests 编译(承 assembly_boundary_test IlScanNegativeFixture 同一先例)。
// ══════════════════════════════════════════════════════════════════════════════
namespace DaYiJingCheng.Gameplay.Presentation.Audio
{
    internal sealed class TransitionNegativeFixture
    {
        /// <summary>模拟「体征 / 病程变更 handler」直接切 DialogueFocus —— 非拟物状态播报,
        /// AC-44-C1 负例(必须被调用点白名单拒收)。</summary>
        internal void OnVitalsChanged(AudioMixerSnapshot dialogueFocus)
        {
            dialogueFocus.TransitionTo(0.25f);
        }
    }
}
