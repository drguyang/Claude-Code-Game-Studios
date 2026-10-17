// 权威来源:production/epics/audio-system/story-010-asset-pipeline-bake-gates.md
//   (AC-44-D4 零 JSON 解析器 · AC-44-D5 Addressables 组区分 · AC-44-D6 素材存在性门 · 听诊窗预载)
//   · 音频事件表 / 设置注册表走 ADR-014 两阶段烘焙:玩家构建零 JSON 解析器、零 FixParse
//   · 素材 = Addressables 流式,与 data-core 组区分
//   · 素材缺失 = 编辑期烘焙门拒绝(构建失败,非运行期降级)
// ADR-014(数据管线与 JSON 解析器)· ADR-018 §五(素材流式)· ADR-024 §⑥(单一出处)
// ADR-025 §①(契约程序集清单)· GDD 硬约束 7(玩家构建零 JSON 解析器)
//
// ⚠️ 落点:故事 Test Evidence 登记路径 = tests/integration/audio_system/pipeline_gate_test.cs;
//    Unity 只编译 unity/Assets/ 树 ⇒ **真身 = 本文件**(承 Story 001-009/014 同一先例);
//    账本侧由 tests/integration/audio_system/README.md 互链。
//
// ⚠️ **S14-6 改造:四个自造替身全部删除**(评审原件 §2.4 判 🔴)——
//    · `FakePreloadGate`        → 真生产件 `StethoscopePreloadGate`(+ `HandoverMux` 接线)
//    · `GroupSeparabilityPredicate` → 真生产件 `AudioAssetGroupGates`
//    · 内联 `File.Exists` 谓词   → 真生产件 `AudioAssetExistence`
//    · `ExtractFieldName`(YAML 扫描)→ 真设置读取 `AudioAssetGroupGates.ValidateSettings()`
//    · **副作用整段删除**:原 `:212-228` 的 `Directory.CreateDirectory(repoRoot/assets/audio)`
//      + `File.WriteAllText` + `File.Delete` —— 违 `.claude/rules/test-standards.md`
//      「Unit tests must not depend on external state (filesystem)」。
//      `assets/audio/` 这个目录此前存在,唯一原因就是这个副作用。
//
// ⚠️ **本夹具的 I/O 面是「读」,不是「写」**:被测对象**就是**仓库作者态数据
//    (`assets/data/audio_events.json` 等)与烘焙链路,故读源文件是本夹具的**题面**。
//    判据:本文件零 `File.WriteAllText` / `Directory.CreateDirectory` / `File.Delete`
//    (由 `test_discipline_noFilesystemWrites` 自证 —— 反证见 evidence 原件 §证伪轮)。
//
// ⚠️ 测试纪律:arrange/act/assert · 无随机 · 无时间依赖(禁 DateTime/Time)。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using NUnit.Framework;
using UnityEditor;                            // EditorUtility / AssetDatabase(夹具失败路径的标签清理)
using UnityEngine;                            // AudioDataLoadState(AudioClipLoadStateAdapter 适配面)
using UnityEditor.AddressableAssets;          // AddressableAssetSettingsDefaultObject(TD-audio-01e② 夹具注入真设置)
using UnityEditor.AddressableAssets.Settings; // AddressableAssetSettings(同上)
using DaYiJingCheng.EditorTools.Bake;
using DaYiJingCheng.EditorTools.Gates;
using DaYiJingCheng.Gameplay.Boot;          // BootRoot.ApplyAudioSettingsProvider(接线真身)
using DaYiJingCheng.Gameplay.Presentation;   // AddressablesDataProvider(接线 address 断言)
using DaYiJingCheng.Gameplay.Presentation.Audio;
using DaYiJingCheng.Gameplay.Presentation.Settings;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Tests.Unit.Audio
{
    [TestFixture]
    internal sealed class PipelineGateTest
    {
        // ══════════════ 基础设施 ══════════════

        // 本文件在 unity/Assets/Tests/EditMode/Audio/ ⇒ Audio→EditMode→Tests→Assets→unity→
        // 仓库根 = 5 层(写 4 层 ⇒ root 落在 unity/ 下,夹具 / GDD / 源树全报「缺失」)。
        private static string RepoRoot([CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile) ?? ".",
                "..", "..", "..", "..", ".."));

        // 本文件自身路径。⚠️ 必须经**无参 helper** 取:把 `[CallerFilePath]` 参数直接挂在
        // `[Test]` 方法上会被 NUnit 当成 test-case 参数(`TargetParameterCountException`)。
        private static string SourcePath([CallerFilePath] string thisFile = "") => thisFile;

        // 扫描键单一出处 = AssemblyGates.AudioModuleNamespacePrefix(测试不重复定义字面量)
        private const string Prefix = AssemblyGates.AudioModuleNamespacePrefix;

        /// <summary>生产 44 类型:只取 Gameplay.Presentation 装配的 44 命名空间。</summary>
        private static List<Type> ProductionAudioTypes()
        {
            var asm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Gameplay.Presentation");
            Assert.That(asm, Is.Not.Null, "Gameplay.Presentation 必须已加载");
            try
            {
                return asm.GetTypes().Where(t => t.Namespace != null &&
                    t.Namespace.StartsWith(Prefix)).ToList();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(t => t != null && t.Namespace != null &&
                    t.Namespace.StartsWith(Prefix)).ToList();
            }
        }

        [TearDown]
        public void TearDown()
        {
            // 静态注入缝:测试改过就必须还原(否则污染同程序集内的其他夹具)
            AudioSettingsRegistry.Provider = null;
        }

        // ══════════════ AC-44-D4:玩家构建零 JSON 解析器 ══════════════

        /// <summary>AC-44-D4:玩家构建产物无 Newtonsoft JSON 解析路径 —— **装配引用面**断言。
        /// <para>⚠️ 先例 `CameraViewpoint/camera_presentation_discipline_test.cs:284`;
        /// 并承 `presentation_projection_test.cs:412` 的警告:`GetReferencedAssemblies()`
        /// 可能空洞为真 ⇒ **必须先断言扫描键非空**。</para></summary>
        [Test]
        public void test_d4_presentationAssembly_noNewtonsoftRef()
        {
            // Arrange
            Assembly asm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Gameplay.Presentation");
            Assert.That(asm, Is.Not.Null, "Gameplay.Presentation 必须已加载");

            // Act
            AssemblyName[] refs = asm.GetReferencedAssemblies();

            // Assert(扫描键非空守卫先行 —— 空集冒充绿是本项目已登记的失效模式)
            Assert.That(refs.Length, Is.GreaterThan(0),
                "GetReferencedAssemblies() 返回空集 ⇒ 本断言空洞为真 = 假绿");
            var newtonsoft = refs.Where(r => r.Name != null &&
                r.Name.IndexOf("Newtonsoft", StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            Assert.That(newtonsoft, Is.Empty,
                "玩家构建零 JSON 解析器(AC-44-D4):Gameplay.Presentation 装配不得引用 Newtonsoft,实得:" +
                string.Join(", ", newtonsoft.Select(r => r.Name)));
        }

        /// <summary>AC-44-D4:44 类型无 JsonTextReader/JsonReader 引用(解析器零存在)。</summary>
        [Test]
        public void test_d4_noJsonReaderRefs_inProductionAudioTypes()
        {
            var types = ProductionAudioTypes();
            var violations = new List<string>();
            foreach (var t in types)
                foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance |
                                              BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    if (m.ReturnType.Name.Contains("JsonReader") || m.ReturnType.Name.Contains("JsonTextReader"))
                        violations.Add($"{t.Name}.{m.Name} 返回 JsonReader 类型");
                    foreach (var p in m.GetParameters())
                        if (p.ParameterType.Name.Contains("JsonReader") ||
                            p.ParameterType.Name.Contains("JsonTextReader"))
                            violations.Add($"{t.Name}.{m.Name} 参数含 JsonReader 类型");
                }

            Assert.That(types.Count, Is.GreaterThan(0), "扫描键落空 = 假绿");
            Assert.That(violations, Is.Empty,
                "玩家构建零 JSON 解析器(AC-44-D4):无 JsonTextReader/JsonReader 引用:\n" +
                string.Join("\n", violations));
        }

        /// <summary>AC-44-D4:44 类型无 FixParse 引用(ADR-014 烘焙后玩家构建零 FixParse)。</summary>
        [Test]
        public void test_d4_noFixParseRefs_inProductionAudioTypes()
        {
            var types = ProductionAudioTypes();
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

            Assert.That(types.Count, Is.GreaterThan(0), "扫描键落空 = 假绿");
            Assert.That(violations, Is.Empty,
                "玩家构建零 FixParse(AC-44-D4 / ADR-014):44 类型不得引用 FixParse:\n" +
                string.Join("\n", violations));
        }

        /// <summary>AC-44-D4:运行期**只解字节** —— 真种子烘焙产物的字节经运行期 codec 解出真表。
        /// <para>这是 D4 的**正向半边**:没有它,上面三条「不得引用 X」可以由「什么都不做」满足。</para></summary>
        [Test]
        public void test_d4_runtimeCodec_decodesBakedBytesOnly()
        {
            // Arrange
            AudioEventTableBake.BakeOutput output = AudioEventTableBake.BakeFromRepo(RepoRoot());

            // Act
            AudioEventDataSet table = AudioEventCookedCodec.Read(output.Cooked);

            // Assert
            Assert.That(table.RowCount, Is.EqualTo(output.Bound.Table.Rows.Count),
                "运行期表行数必须等于烘焙行数(运行期只解字节,不重解析 JSON)");
            Assert.That(table.ConfigVersion, Is.EqualTo(output.ConfigVersion),
                "运行期表 ConfigVersion 必须等于烘焙期派生值");
        }

        /// <summary>AC-44-D4:设置注册表同款 —— 真种子烘焙产物经运行期 codec 解出真行集。</summary>
        [Test]
        public void test_d4_settingsCodec_decodesBakedBytesOnly()
        {
            AudioSettingsRegistryBake.BakeOutput output = AudioSettingsRegistryBake.BakeFromRepo(RepoRoot());
            AudioSettingsDataSet dataSet = AudioSettingsRegistryCookedCodec.Read(output.Cooked);

            Assert.That(dataSet.RowCount, Is.EqualTo(output.Bound.Rows.Count));
            Assert.That(dataSet.ConfigVersion, Is.EqualTo(output.ConfigVersion));
        }

        /// <summary>cooked 读方:载荷 schema_version ≠ 头槽 ⇒ 拒(同一真值两处存且已分叉)。
        /// <para>⚠️ 断言**钉报文**而不只是「抛了」:不给报文时,后续越界读同样会抛 ⇒
        /// 本测对「读方是否真的比对了这两处」零判别力(承 S14-5 证伪轮抓出的同型缺陷 ——
        /// 报文被别的错误满足)。</para></summary>
        [Test]
        public void test_d4_runtimeCodec_payloadSchemaDivergedFromHeader_rejected()
        {
            byte[] tampered = TamperPayloadSchema(AudioEventTableBake.BakeFromRepo(RepoRoot()).Cooked);

            var ex = Assert.Throws<InvalidOperationException>(
                () => AudioEventCookedCodec.Read(tampered));
            Assert.That(ex.Message, Does.Contain("载荷 schema_version"),
                "读方必须报「载荷 schema_version 与头槽分叉」—— 否则本测接不住布局分叉(别的错也会抛)");
        }

        /// <summary>设置注册表 codec:同一分叉守卫的对称覆盖。</summary>
        [Test]
        public void test_d4_settingsCodec_payloadSchemaDivergedFromHeader_rejected()
        {
            byte[] tampered =
                TamperPayloadSchema(AudioSettingsRegistryBake.BakeFromRepo(RepoRoot()).Cooked);

            var ex = Assert.Throws<InvalidOperationException>(
                () => AudioSettingsRegistryCookedCodec.Read(tampered));
            Assert.That(ex.Message, Does.Contain("载荷 schema_version"));
        }

        /// <summary>把载荷首 u32(源 schema_version)改成 0,头槽保持原值 ⇒ 制造两处分叉。</summary>
        private static byte[] TamperPayloadSchema(byte[] cooked)
        {
            byte[] t = (byte[])cooked.Clone();
            for (int i = 0; i < 4; i++) t[CookedFormat.HeaderSize + i] = 0;
            return t;
        }

        // ══════════════ AC-44-D5:Addressables 组区分 ══════════════

        /// <summary>AC-44-D5(正向半边,打**真** AddressableAssetSettings):
        /// 工程内音频素材各自登记在**非 data-core** 组,且 data-core 组零音频目录条目。
        /// <para>⚠️ 机器前置:AddressableAssetsData 是 gitignored 的机器生成物 ⇒
        /// 首次须跑菜单「确保 data-core / audio Addressables 组」。设置缺席时本断言**报错**
        /// (不是跳过 —— 跳过 = 可选门 = 原稿 `Assert.Ignore` 假绿的翻版)。</para></summary>
        [Test]
        public void test_d5_realSettings_audioAssetsSeparateFromDataCore()
        {
            IReadOnlyList<string> errors = AudioAssetGroupGates.ValidateSettings();
            Assert.That(errors, Is.Empty,
                "AC-44-D5:素材须与 data-core 组区分。实得:\n" + string.Join("\n", errors));
        }

        /// <summary>AC-44-D5:磁盘上的音频素材集**非空** —— 扫描键非空守卫
        /// (空集会让 `ValidateEntries` 的 (a) 半边对任何输入都通过)。</summary>
        [Test]
        public void test_d5_audioAssetsOnDisk_nonEmpty()
        {
            IReadOnlyList<string> onDisk = AudioAssetGroupGates.AudioAssetsOnDisk();
            Assert.That(onDisk.Count, Is.GreaterThan(0),
                "Assets/Audio/ 下无 .wav ⇒ D5 的 (a) 半边与 D6 的正向半边同时空洞为真 = 假绿");
            Assert.That(onDisk.All(p => p.EndsWith(
                    AudioAssetGroupGates.AudioAssetExtension, StringComparison.Ordinal)), Is.True,
                "扫盘结果必须全是音频素材扩展名");
        }

        /// <summary>AC-44-D5 负向(纯谓词):音频素材登记进 data-core 组 ⇒ 拒。</summary>
        [Test]
        public void test_d5_pure_audioAssetInDataCoreGroup_rejected()
        {
            var entries = new List<AudioGroupEntry>
            {
                new AudioGroupEntry("data-core", "Assets/Audio/sfx_breath_base_loop_small.wav"),
            };
            var expected = new List<string> { "Assets/Audio/sfx_breath_base_loop_small.wav" };

            IReadOnlyList<string> errors = AudioAssetGroupGates.ValidateEntries(entries, expected);

            Assert.That(errors, Is.Not.Empty, "混组必须被拒(AC-44-D5)");
            Assert.That(string.Join("\n", errors), Does.Contain("data-core"));
        }

        /// <summary>AC-44-D5 负向(纯谓词):音频素材**未登记**进任何组 ⇒ 拒。</summary>
        [Test]
        public void test_d5_pure_audioAssetUnregistered_rejected()
        {
            var entries = new List<AudioGroupEntry>
            {
                new AudioGroupEntry("data-core", "Assets/DataCooked/audio_events.cooked.bytes"),
            };
            var expected = new List<string> { "Assets/Audio/vo_patient_cough_damp_m1.wav" };

            IReadOnlyList<string> errors = AudioAssetGroupGates.ValidateEntries(entries, expected);

            Assert.That(errors, Is.Not.Empty, "未登记素材必须被拒(AC-44-D5:须走 Addressables 流式)");
            Assert.That(string.Join("\n", errors), Does.Contain("未登记"));
        }

        /// <summary>AC-44-D5 负向(纯谓词):data-core 组含**期望集之外**的音频目录条目 ⇒ 仍拒
        /// (覆盖「不在期望集里、但确实躺在 Assets/Audio/ 下并被塞进 data-core」的漏网条目)。
        /// <para>⚠️ 夹具形态是**判别力的一部分**(S14-6 证伪轮 C4 实测):若期望集成员**不合法登记**,
        /// (a) 半边会先报「未登记进任何组」⇒ 断言被**别的**错误满足,停用 (b) 判据仍绿 = 假绿。
        /// 故此处显式让期望集成员合法登记在 `audio` 组,并把漏网条目设为**另一个**文件 ——
        /// 只有 (b) 能报出它。</para>
        /// <para>报文断言**钉住是哪条判据报的**(同上:仅断「非空」接不住误报)。</para></summary>
        [Test]
        public void test_d5_pure_strayAudioEntryInDataCore_rejected()
        {
            var entries = new List<AudioGroupEntry>
            {
                // 期望集成员:合法登记在非 data-core 组 ⇒ (a) 半边通过,判据面只剩 (b)
                new AudioGroupEntry("audio", "Assets/Audio/sfx_breath_base_loop_small.wav"),
                // 漏网条目:不在期望集内,却躺在 Assets/Audio/ 下并被塞进 data-core
                new AudioGroupEntry("data-core", "Assets/Audio/orphan.wav"),
            };
            var expected = new List<string> { "Assets/Audio/sfx_breath_base_loop_small.wav" };

            IReadOnlyList<string> errors = AudioAssetGroupGates.ValidateEntries(entries, expected);
            string joined = string.Join("\n", errors);

            Assert.That(errors, Is.Not.Empty, "data-core 组内任何音频目录条目都违 AC-44-D5");
            Assert.That(joined, Does.Contain("orphan.wav"),
                "(b) 判据必须点名漏网条目 —— 不点名 ⇒ 本测可能被 (a) 半边的误报满足(证伪轮 C4 实证)");
            Assert.That(joined, Does.Not.Contain("sfx_breath_base_loop_small"),
                "合法登记的期望集成员不得被报(否则本测对 (b) 无判别力)");
        }

        /// <summary>AC-44-D5 正向(纯谓词):干净条目集 ⇒ 零错误(负向夹具的对照面)。</summary>
        [Test]
        public void test_d5_pure_cleanEntries_pass()
        {
            var entries = new List<AudioGroupEntry>
            {
                new AudioGroupEntry("data-core", "Assets/DataCooked/audio_events.cooked.bytes"),
                new AudioGroupEntry("audio", "Assets/Audio/sfx_breath_base_loop_small.wav"),
            };
            var expected = new List<string> { "Assets/Audio/sfx_breath_base_loop_small.wav" };

            Assert.That(AudioAssetGroupGates.ValidateEntries(entries, expected), Is.Empty,
                "干净条目集必须零错误(否则负向夹具的「非空」无判别力)");
        }

        /// <summary>AC-44-D5:null 条目集 ⇒ 拒(空集冒充绿守卫)。</summary>
        [Test]
        public void test_d5_pure_nullEntries_rejected()
        {
            IReadOnlyList<string> errors =
                AudioAssetGroupGates.ValidateEntries(null, new List<string> { "Assets/Audio/x.wav" });
            Assert.That(errors, Is.Not.Empty, "null 条目集不得静默通过");
        }

        /// <summary>AC-44-D5:组名常量自检 —— 素材组名必须**不是** data-core
        /// (判据本身不依赖名字,但常量分叉会让菜单建错组)。</summary>
        [Test]
        public void test_d5_audioGroupName_isNotDataCore()
        {
            Assert.That(AudioAssetGroupGates.AudioGroupName,
                Is.Not.EqualTo(AudioAssetGroupGates.DataCoreGroupName),
                "素材组名与数据组名必须互异(AC-44-D5 的菜单侧前提)");
        }

        // ══════════════ story-017 门加固(W-2 / W-3)══════════════
        //   AC-44-D5b 扩展名过滤跨平台一致 · AC-44-D5c 判据方法 ≤40 行 / CC<10
        //   · AC-44-D5d 错误集逐字不变(纯重构)

        /// <summary>AC-44-D5b:音频素材扩展名判定**跨平台大小写一致**。
        /// <para>病根(W-2):`Directory.GetFiles(dir, "*.wav")` 的 pattern **在 Linux 上区分大小写**
        /// (独立 net8.0 探针实测:含 `lower.wav` / `UPPER.WAV` / `Mixed.Wav` 的目录**只返回 `lower.wav`**)
        /// ⇒ CI 平台正是 Linux(ADR-012),素材批交付 `.WAV` 时门**静默少报**。</para>
        /// <para>判据真源 = `AudioAssetGroupGates.IsAudioAssetName`(**单一出处**);
        /// `AudioImportSettingsGate.IsWavName` 已改为**委派**到它 ⇒ 本测末尾同时钉住两侧不脱钩。</para>
        /// <para>⚠️ 本测**只覆盖谓词**;「调用点真的用了谓词」由下一条
        /// (`test_d5b_audioAssetsOnDisk_filtersViaPredicateNotGlob`)补 —— 两者合起来才封住 W-2。</para></summary>
        [Test]
        public void test_d5b_audioAssetName_extensionCaseInsensitive()
        {
            // ── 正向:三形态 + 带目录路径 ──
            Assert.That(AudioAssetGroupGates.IsAudioAssetName("lower.wav"), Is.True, "全小写须识别");
            Assert.That(AudioAssetGroupGates.IsAudioAssetName("UPPER.WAV"), Is.True,
                "全大写须识别 —— Linux 下 `GetFiles(\"*.wav\")` 会漏掉它(W-2 病根)");
            Assert.That(AudioAssetGroupGates.IsAudioAssetName("Mixed.Wav"), Is.True, "混写须识别");
            Assert.That(AudioAssetGroupGates.IsAudioAssetName("Assets/Audio/vo_x.WAV"), Is.True,
                "带目录路径须识别");

            // ── 负向 ──
            Assert.That(AudioAssetGroupGates.IsAudioAssetName("note.txt"), Is.False);
            Assert.That(AudioAssetGroupGates.IsAudioAssetName("noext"), Is.False);
            Assert.That(AudioAssetGroupGates.IsAudioAssetName("x.wav.bak"), Is.False, "伪后缀不得命中");
            Assert.That(AudioAssetGroupGates.IsAudioAssetName(""), Is.False);
            Assert.That(AudioAssetGroupGates.IsAudioAssetName(null), Is.False);

            // ── 委派不脱钩:两处判据对同一探针必须同结论 ──
            foreach (string probe in new[] { "a.wav", "A.WAV", "b.txt", "", "c.Wav", null })
                Assert.That(AudioImportSettingsGate.IsWavName(probe),
                    Is.EqualTo(AudioAssetGroupGates.IsAudioAssetName(probe)),
                    $"两处扩展名判据分叉(「单一出处」名存实亡):{probe ?? "(null)"}");
        }

        /// <summary>AC-44-D5b(**映射结果半边**,QA 用例逐字):磁盘路径集 → 工程路径集,
        /// `.wav` / `.WAV` / `.Wav` **三形态均须返回**,非 wav / 伪后缀 / 编辑器临时文件须剔除。
        /// <para>⚠️ 为什么是**纯函数**而不是往盘上造副本:副本会同时进 D5 的期望集与 D6 的
        /// 存在性判据(假红),且违夹具「零文件写入」纪律。抽纯函数后夹具可注入路径直断 ——
        /// 这才是 QA 用例「两者均被返回」的**直接**证据(而非「谓词对 + 调用点用了谓词」的复合论证)。</para>
        /// <para>排序按 `Ordinal`:大写字母码位小于小写(`'M'`=77 < `'U'`=85 < `'l'`=108)⇒
        /// 期望次序 = `Mixed.Wav` / `UPPER.WAV` / `lower.wav`。本断言**同时钉住顺序**。</para></summary>
        [Test]
        public void test_d5b_filterAudioNames_threeCaseFormsAllKept()
        {
            IReadOnlyList<string> got = AudioAssetGroupGates.FilterAudioNames(new List<string>
            {
                "/root/Audio/lower.wav",       // 全小写
                "/root/Audio/UPPER.WAV",       // 全大写 —— Linux 下 glob 会漏掉它(W-2 病根)
                "/root/Audio/Mixed.Wav",       // 混写
                "/root/Audio/note.txt",        // 非 wav
                "/root/Audio/noext",           // 无扩展名
                "/root/Audio/x.wav.bak",       // 伪后缀
                "/root/Audio/.hidden.wav",     // 编辑器临时文件(须剔除)
            });

            Assert.That(got, Is.EqualTo(new[]
            {
                "Assets/Audio/Mixed.Wav",
                "Assets/Audio/UPPER.WAV",
                "Assets/Audio/lower.wav",
            }), "三形态均须返回且按 Ordinal 排序;非 wav / 伪后缀 / 临时文件须剔除");

            Assert.That(AudioAssetGroupGates.FilterAudioNames(null), Is.Empty,
                "null 入参 ⇒ 空集(不得 NRE)");
            Assert.That(AudioAssetGroupGates.FilterAudioNames(new List<string>()), Is.Empty);
        }

        /// <summary>AC-44-D5b(**调用点半边**):`AudioAssetsOnDisk` 必须把枚举结果交给纯函数,
        /// 且该纯函数必须用判据真源 `IsAudioAssetName` —— 不得回到
        /// `GetFiles(dir, "*" + AudioAssetExtension)` 形态,也不得自己另写一份扩展名比较。
        /// <para>⚠️ 为什么单立一条:谓词本身正确**不蕴含**调用点用它 —— 把枚举改回 glob,
        /// 上两条断言仍**全绿**(素材目录恰好只有小写 `.wav`)⇒ 它们对「调用点回退」零判别力。
        /// 本测是那两条的**判别面补丁**(证伪轮 F6 反证)。</para>
        /// <para>扫描面 = 生产件源码文本(**注释已剥离**)—— 文件头注**逐字记载**了被禁的写法,
        /// 不剥离则扫描命中自己的说明文字 ⇒ 恒红、零判别力(同 `test_discipline_noFilesystemWrites`)。</para></summary>
        [Test]
        public void test_d5b_audioAssetsOnDisk_filtersViaPredicateNotGlob()
        {
            string src = StripComments(File.ReadAllText(GatesSourcePath()));

            // ① 真环境半边:枚举结果须交给纯函数
            string reader = ExtractMethodText(src, "public static IReadOnlyList<string> AudioAssetsOnDisk(");
            Assert.That(reader, Does.Contain("FilterAudioNames("),
                "枚举必须经 FilterAudioNames 过滤(W-2 修法)");

            // ② 映射半边:须用判据真源,不得另写一份扩展名比较(「禁另抄」)
            string mapper = ExtractMethodText(src, "public static IReadOnlyList<string> FilterAudioNames(");
            Assert.That(mapper, Does.Contain("IsAudioAssetName("),
                "映射必须经 IsAudioAssetName(单一出处),不得另写扩展名比较");

            // ③ 全文不得再出现 glob 形态
            // ⚠️ 令牌**拼接构造**:整字面量会让本断言自身命中(注释虽已剥离,仍保持与
            //    `test_discipline_noFilesystemWrites` 同款纪律),且避免将来误伤本文件。
            Assert.That(src, Does.Not.Contain("\"*\"" + " + AudioAssetExtension"),
                "全文不得再出现 glob 形态 —— W-2 病根是 pattern 区分大小写,"
                + "⛔ 改成 \"*.WAV\" 只是把分叉挪到另一边");
        }

        /// <summary>AC-44-D5c:`ValidateEntries` 须 ≤ 40 **物理行**(签名行 → 闭括号行,含注释与空行)
        /// 且圈复杂度 &lt; 10 —— 由拆分(编排 + 两个私有判据半)兑现。
        /// <para>⚠️ **度量口径 = 物理行,不是「非空非括号行」**(W-3 原稿实测 45 行 = 物理行)。
        /// 用错口径:原实现只有 ~22 个非空非括号行 ⇒ **原实现也通过** ⇒ 夹具零判别力(假绿)。
        /// 本测的阈值 40 正是按物理行口径能区分「拆分前 45 / 拆分后 10」而设。</para>
        /// <para>⚠️ 本测自带**计数器自校准**:计数器若写坏成「恒小」,两个断言都会蒙混过关 ⇒
        /// 校准断言是计数器本身的判别面(证伪轮 F8 反证)。</para></summary>
        [Test]
        public void test_d5c_validateEntries_withinLineAndComplexityBudget()
        {
            // ── 自校准:计数器对已知片段必须给出确定值(否则「恒小」= 假绿)──
            const string probe = "{ if (a) { for (int i = 0; i < 1; i++) { } } else if (b && c) { } }";
            Assert.That(CountDecisionPoints(probe), Is.EqualTo(4),
                "决策点计数器自校准失败(期望 if + for + if + && = 4)—— 计数器坏了则本测无判别力");

            string src = StripComments(File.ReadAllText(GatesSourcePath()));
            string body = ExtractMethodText(src, "public static IReadOnlyList<string> ValidateEntries(");

            int lines = body.Split('\n').Length;
            Assert.That(lines, Is.LessThanOrEqualTo(40),
                $"ValidateEntries 须 ≤ 40 物理行(W-3:拆分前 45 行),实得 {lines} 行 —— "
                + "两个判据半须留在私有方法里,编排方法不得内联判据");

            int cc = 1 + CountDecisionPoints(body);
            Assert.That(cc, Is.LessThan(10),
                $"ValidateEntries 圈复杂度须 < 10(W-3:拆分前 15),实得 {cc} —— 编排方法不得内联判据");

            // ── QA 用例的 Edge case 逐字:「拆分后主方法只剩编排;**私有方法各自 ≤ 40 行**」──
            //    ⚠️ 只在主方法上钉行数会被「把判据挪进一个 60 行的私有方法」绕过 —— 阈值要
            //    落在**每一段**上,否则拆分只是把超标搬了家。
            foreach (string sig in new[]
                     {
                         "private static void CheckExpectedAssetsAreGrouped(",
                         "private static void CheckDataCoreHasNoAudioEntries(",
                     })
            {
                int halfLines = ExtractMethodText(src, sig).Split('\n').Length;
                Assert.That(halfLines, Is.LessThanOrEqualTo(40),
                    $"{sig} 亦须 ≤ 40 物理行,实得 {halfLines} 行 —— "
                    + "否则「拆分」只是把超标搬到了私有方法里");
            }
        }

        /// <summary>AC-44-D5d:拆分**不得改变判定语义** —— 同一输入下错误集**逐字相同**。
        /// <para>做法 = **黄金串表**:每例断言**完整错误列表**(条数 + 逐字文本 + 顺序),
        /// 而不是「非空」。仅断非空接不住三种回归:① 报文改了字 ② 多报/漏报一条
        /// ③ 两半次序颠倒 —— 而这三种正是「顺手重构」的典型副作用。</para>
        /// <para>⚠️ 本表的期望串**逐字复制**生产件里的报文字面量。生产件改文案 ⇒ 本表须同步;
        /// 不同步 = 红。这正是「纯重构」的可证伪形态,不是脆性(改文案本就该同步账本)。</para></summary>
        [Test]
        public void test_d5d_errorSetIsByteIdentical()
        {
            const string A = "Assets/Audio/a.wav";
            const string B = "Assets/Audio/b.wav";
            const string C = "Assets/Audio/c.wav";

            // 期望报文构造器(逐字对齐生产件;改任一处 ⇒ 本表红)
            static string InDataCore(string asset) =>
                "[D5] 音频素材「" + asset + "」登记在 `data-core` 组 —— " +
                "AC-44-D5:素材与 `data-core` 数据烘焙组**必须区分**(混组 = 流式素材被打进数据包)";
            static string Unregistered(string asset) =>
                "[D5] 音频素材「" + asset + "」未登记进任何 Addressables 组 —— " +
                "AC-44-D5:素材须走 Addressables 流式(未登记 = 打进主包 / 不可寻址)。" +
                "跑菜单「确保 audio Addressables 组」";
            static string StrayInDataCore(string asset) =>
                "[D5] `data-core` 组含音频目录条目「" + asset + "」—— " +
                "AC-44-D5:组区分(该文件不在期望集内,仍是混组)";

            // ① null 条目集 ⇒ 恰一条(空集冒充绿守卫)
            Assert.That(AudioAssetGroupGates.ValidateEntries(null, new List<string> { A }),
                Is.EqualTo(new[] { "[D5] 条目集为 null —— 空集冒充绿" }),
                "① null 条目集须恰一条");

            // ② 期望素材登记进 data-core ⇒ 恰一条 (a) 判据报文
            Assert.That(
                AudioAssetGroupGates.ValidateEntries(
                    new List<AudioGroupEntry> { new AudioGroupEntry("data-core", A) },
                    new List<string> { A }),
                Is.EqualTo(new[] { InDataCore(A) }), "② 混组须恰一条");

            // ③ 期望素材未登记 ⇒ 恰一条 (a) 判据报文
            Assert.That(
                AudioAssetGroupGates.ValidateEntries(
                    new List<AudioGroupEntry> { new AudioGroupEntry("data-core", "Assets/DataCooked/x.bytes") },
                    new List<string> { A }),
                Is.EqualTo(new[] { Unregistered(A) }), "③ 未登记须恰一条");

            // ④ 两半**同时**触发 ⇒ (a) 的两条在前、(b) 的一条在后(拆分不得颠倒次序)
            var mixed = new List<AudioGroupEntry>
            {
                new AudioGroupEntry("data-core", A),   // (a) 命中:混组
                new AudioGroupEntry("data-core", C),   // (b) 命中:在 Audio 下但不在期望集
            };
            Assert.That(AudioAssetGroupGates.ValidateEntries(mixed, new List<string> { A, B }),
                Is.EqualTo(new[] { InDataCore(A), Unregistered(B), StrayInDataCore(C) }),
                "④ (a) 半的报文须先于 (b) 半 —— 次序是判据语义的一部分");

            // ⑤ 干净条目集 ⇒ 空
            Assert.That(
                AudioAssetGroupGates.ValidateEntries(
                    new List<AudioGroupEntry> { new AudioGroupEntry("audio", A) },
                    new List<string> { A }),
                Is.Empty, "⑤ 干净条目集须零错误");

            // ⑥ 期望集为 null ⇒ (a) 半整段跳过;(b) 半仍在(其 `expectedAudioAssets != null` 守卫
            //    不成立 ⇒ 不 continue ⇒ 报)。**行为原样钉住**(纯重构 = 语义不动,不是语义变好)。
            Assert.That(
                AudioAssetGroupGates.ValidateEntries(
                    new List<AudioGroupEntry> { new AudioGroupEntry("data-core", A) }, null),
                Is.EqualTo(new[] { StrayInDataCore(A) }),
                "⑥ 期望集为 null 时的行为须与拆分前一致");
        }


        // ══════════════ TD-audio-01b / 01e:菜单硬失败 ══════════════
        // 病根(2026-10-17 实测,`s17-freshclone-20261017-051600.log`):菜单项在
        // `settings == null` 时只 `Debug.LogError` 就 `return` ⇒ **Unity 退出码 = 0**
        // ⇒ 以 `-executeMethod` 作 CI 前置步的无人值守路径会**报绿而组实际缺失** = 假绿。
        // 夹具经可注入重载 `EnsureAudioGroupCore(settings, dir)` 命中两条分支。

        /// <summary>TD-audio-01b + 01e①:设置缺失 ⇒ **硬失败**(不是 `LogError` + `return`)。
        /// <para>笔 1 之后组定义已入库 ⇒ 「设置缺失」的含义由「没初始化过」变为
        /// 「入库文件被删/损坏」,恢复动作是 <c>git checkout</c> —— 报文须指向它。</para></summary>
        [Test]
        public void test_d5_menu_settingsMissing_throwsHard()
        {
            var ex = Assert.Throws<InvalidOperationException>(
                () => DataBakeMenu.EnsureAudioGroupCore(null, "Assets/Audio", "audio"),
                "设置缺失必须**抛异常**(LogError + return 会让进程退出码保持 0 = 假绿)");

            Assert.That(ex.Message, Does.Contain("git checkout"),
                "报文须指向恢复动作 —— 组定义已入库,缺设置 = 入库文件被删/损坏");
            Assert.That(ex.Message, Does.Not.Contain("Window > Asset Management"),
                "不得再引导用户手建设置 —— 新建的只含默认组,与入库定义分叉");
        }

        /// <summary>TD-audio-01e②:素材目录零素材 ⇒ **硬失败**(原为 `LogWarning` + `return`,
        /// 同款进程级静默)。
        /// <para>⚠️ 本测注入**真设置** —— 承「先验后改」,该分支在任何写入之前判定
        /// ⇒ 失败时设置零改动,不污染工程 Addressables 状态。若日后有人把校验挪到写入之后,
        /// 本测会开始改动工程状态(届时须重新设计夹具)。</para></summary>
        [Test]
        public void test_d5_menu_noAssets_throwsHard()
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            Assert.That(settings, Is.Not.Null,
                "本机 Addressables 设置缺失 —— 正向半边 test_d5_realSettings_… 也会红(机器前置)");

            var ex = Assert.Throws<InvalidOperationException>(
                () => DataBakeMenu.EnsureAudioGroupCore(settings, "Assets/__no_such_audio_dir__", "audio"),
                "空素材目录必须**抛异常**(LogWarning + return 同样让退出码保持 0)");

            Assert.That(ex.Message, Does.Contain("__no_such_audio_dir__"),
                "报文须点名出错的目录,否则无法定位");
        }

        /// <summary>跑一段注入**假组名**的菜单代码:断言其**抛异常**,并保证**不留痕** —— 无论成败。</summary>
        /// <remarks>⚠️ <b>为什么需要它(2026-10-17 证伪轮实测,不是预防性设计)</b>:变异 M1/M3 停用
        /// 「组缺失 ⇒ throw」后,生产码**落入分支 ④**,先 <c>settings.AddLabel(假组名)</c> 再
        /// <c>AssetDatabase.SaveAssets()</c> ⇒ **入库的 `AddressableAssetSettings.asset` 被写入两个假标签**
        /// (实测 `git diff` 命中 `audio__no_such_group__` / `data-core__no_such_group__`)。
        /// 即:**守卫一破,失败的测试会污染已提交的资产**。`.claude/rules/test-standards.md` 要求
        /// 「Integration tests must clean up after themselves」⇒ 本包装器即该清理,
        /// 并把「无残留」判据从「只查**组**」扩到「**查组 + 查标签**」。</remarks>
        private static InvalidOperationException AssertThrowsAndLeaveNoTrace(
            AddressableAssetSettings settings, string fakeGroupName, TestDelegate code, string why)
        {
            InvalidOperationException ex = null;
            try
            {
                ex = Assert.Throws<InvalidOperationException>(code, why);
            }
            finally
            {
                // ⚠️ 清理必须落 finally —— **失败路径**才是会留下标签的那一条。
                if (settings.GetLabels().Contains(fakeGroupName))
                {
                    settings.RemoveLabel(fakeGroupName);
                    EditorUtility.SetDirty(settings);
                    AssetDatabase.SaveAssets();
                }
            }

            Assert.That(settings.FindGroup(fakeGroupName), Is.Null,
                "抛异常后不得留下任何新建的组(先验后改:失败时零改动)");
            Assert.That(settings.GetLabels(), Does.Not.Contain(fakeGroupName),
                "抛异常后不得留下标签 —— 守卫一破,生产码会先 AddLabel 再 SaveAssets");
            return ex;
        }

        /// <summary>TD-audio-01 W-5②(2026-10-17):**组缺失 ⇒ 硬失败**,不得 `CreateGroup` 重建。
        /// <para>组定义已随仓库入库(01a)⇒ 组缺失 = 入库文件被删/损坏,恢复靠 `git checkout`。
        /// 原 `CreateGroup(schemasToCopy: null)` 建出的 schema 是 Addressables **默认字段值**,
        /// 而入库的 <c>audio_BundledAssetGroupSchema</c> 是 <c>m_UseDefaultSchemaSettings: 0</c>
        /// 的脱离默认态 ⇒ 重建 = 与入库定义**分叉**(看着绿,组是错的)。</para>
        /// <para>注入一个不存在的组名命中分支 ③(注入真设置 —— 承「先验后改」,失败时零改动)。</para></summary>
        [Test]
        public void test_d5_menu_groupMissing_throwsHard()
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            Assert.That(settings, Is.Not.Null, "本机 Addressables 设置缺失(机器前置)");

            const string absent = "audio__no_such_group__";
            Assert.That(settings.FindGroup(absent), Is.Null, "本测要求该组名确不存在");

            InvalidOperationException ex = AssertThrowsAndLeaveNoTrace(
                settings, absent,
                () => DataBakeMenu.EnsureAudioGroupCore(settings, "Assets/Audio", absent),
                "组缺失必须**抛异常**,不得静默 CreateGroup(重建 schema 与入库定义分叉)");

            Assert.That(ex.Message, Does.Contain("git checkout"),
                "报文须指向恢复动作 —— 组定义已入库");
        }

        /// <summary>TD-audio-01 W-5② 的 <c>data-core</c> 侧 + 两静默分支加固(2026-10-17)。
        /// <para>`EnsureDataCoreGroup` 原与 `EnsureAudioGroup` 同款三处静默返回,现已同批加固
        /// (用户裁定 01e 的窄范围已扩)。三支逐条钉住。</para></summary>
        [Test]
        public void test_dcore_menu_settingsMissing_throwsHard()
        {
            var ex = Assert.Throws<InvalidOperationException>(
                () => DataBakeMenu.EnsureDataCoreGroupCore(null, "Assets/DataCooked", "data-core"),
                "设置缺失必须抛异常(与 audio 侧同款进程级静默)");

            Assert.That(ex.Message, Does.Contain("git checkout"));
        }

        /// <summary><c>data-core</c> 侧:产物目录零 <c>*.cooked.bytes</c> ⇒ 硬失败。
        /// <para>⚠️ 原实现在此处**已先建组、先打标签**才 `LogWarning` —— 现已把扫描提到写入之前
        /// (先验后改)。本测注入真设置,故该性质可被观测:抛异常后组与标签零改动。</para></summary>
        [Test]
        public void test_dcore_menu_noCooked_throwsHard()
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            Assert.That(settings, Is.Not.Null, "本机 Addressables 设置缺失(机器前置)");

            const string emptyDir = "Assets/__no_such_cooked_dir__";
            var ex = Assert.Throws<InvalidOperationException>(
                () => DataBakeMenu.EnsureDataCoreGroupCore(settings, emptyDir, "data-core"),
                "空产物目录必须抛异常");

            Assert.That(ex.Message, Does.Contain(emptyDir), "报文须点名出错的目录");
        }

        /// <summary><c>data-core</c> 侧:组缺失 ⇒ 硬失败,不得 <c>CreateGroup</c>(同 W-5②)。</summary>
        [Test]
        public void test_dcore_menu_groupMissing_throwsHard()
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            Assert.That(settings, Is.Not.Null, "本机 Addressables 设置缺失(机器前置)");

            const string absent = "data-core__no_such_group__";
            Assert.That(settings.FindGroup(absent), Is.Null, "本测要求该组名确不存在");

            InvalidOperationException ex = AssertThrowsAndLeaveNoTrace(
                settings, absent,
                () => DataBakeMenu.EnsureDataCoreGroupCore(
                    settings, $"Assets/{DataBakeMenu.CookedDirName}", absent),
                "组缺失必须抛异常,不得静默 CreateGroup");

            Assert.That(ex.Message, Does.Contain("git checkout"));
        }

        // ══════════════ AC-44-D6:素材存在性门 ══════════════

        /// <summary>AC-44-D6(正向半边,打**真种子**):`assets/data/audio_events.json`
        /// 引用的 5 个素材**全部存在** ⇒ 烘焙零错误。
        /// <para>这是本 story 的核心判据 —— 原稿的两个「存在性测试」都自建文件,
        /// 生产链路一次都没跑过存在性检查。</para></summary>
        [Test]
        public void test_d6_realSeed_bakesWithNoMissingAsset()
        {
            AudioEventTableBake.BakeOutput output = AudioEventTableBake.BakeFromRepo(RepoRoot());

            Assert.That(output.Cooked, Is.Not.Null.And.Not.Empty);
            // ⚠️ 2026-10-17:原断言 5 行 —— 被**内容批**打破(34 个真素材接入后种子扩到 15 行)。
            //    该断言的**判别力在「烘焙不 throw」**,不在「行数恰为 5」;行数钉死会让每次
            //    合法扩表都变红(测的是「没改过数据」,不是「门能过」)⇒ 改为下界守卫:
            //    至少覆盖内容批已交付的行数,且行数 == 表模型行数(自洽)。
            Assert.That(output.Bound.Table.Rows.Count, Is.GreaterThanOrEqualTo(15),
                "真种子至少 15 行(内容批 2026-10-17 落地后的下界;扩表须同改本下界)");
            Assert.That(output.Bound.Table.Rows.Count,
                Is.EqualTo(ReadSeedRowCount()), "烘焙行数须与源 JSON rows[] 行数一致");
        }

        /// <summary>AC-44-D6 负向:把真种子里一个素材名换成不存在的名字 ⇒ **聚合 throw**。</summary>
        [Test]
        public void test_d6_missingAsset_rejectedWithAggregateThrow()
        {
            string json = ReadSeedJson("audio_events.json");
            const string present = "sfx_breath_adventitious_fine.wav";
            Assert.That(json, Does.Contain(present), "负向夹具前提:种子里必须含该素材名(否则替换空转 = 假绿)");

            string broken = json.Replace(present, "__missing_cue__.wav");
            Assert.That(broken, Is.Not.EqualTo(json), "负向夹具必须真的改动了源文本");

            // Act + Assert:门强制 —— 缺失素材 = 构建失败,不是警告
            var ex = Assert.Throws<BakeValidationException>(
                () => AudioEventTableBake.BakeFromJson(broken, AudioAssetExistence.ResolveRoot(RepoRoot())));
            Assert.That(ex.Errors, Is.Not.Empty, "聚合 throw 必须携带错误列表");
        }

        /// <summary>AC-44-D6:缺失错误报文须**点名文件 + cue**(可证伪,不是「有一处错误」)。</summary>
        [Test]
        public void test_d6_missingAssetError_namesFileAndCue()
        {
            string json = ReadSeedJson("audio_events.json");
            // ⚠️ 2026-10-17:原用 `vo_patient_cough_damp_m1.wav` —— 该名已按命名裁定**退役并删文件**,
            //    种子里不再出现 ⇒ Replace 空转 ⇒ 烘焙照常成功 ⇒ 本测假红(实测)。
            //    改为**先取自种子实际引用的第一个素材名**(断言前提非空),再替换。
            string present = FirstAssetNameInSeed(json);
            Assert.That(present, Is.Not.Null.And.Not.Empty,
                "负向夹具前提:种子必须至少引用一个素材名(否则替换空转 = 假绿)");
            Assert.That(json, Does.Contain(present));
            string broken = json.Replace(present, "__gone__.wav");
            Assert.That(broken, Is.Not.EqualTo(json), "负向夹具必须真的改动了源文本");

            var ex = Assert.Throws<BakeValidationException>(
                () => AudioEventTableBake.BakeFromJson(broken, AudioAssetExistence.ResolveRoot(RepoRoot())));

            string joined = string.Join("\n", ex.Errors);
            Assert.That(joined, Does.Contain("__gone__.wav"), "错误须点名缺失的文件");
            Assert.That(joined, Does.Contain("Voice_Cough_Damp"), "错误须点名引用了它的 cue");
            Assert.That(joined, Does.Contain("[D6"), "错误须带 D6 标记(可追溯 AC)");
        }

        /// <summary>AC-44-D6:素材根为 null ⇒ `ArgumentNullException`
        /// (**刻意无「根可空 ⇒ 跳过」形态** —— 可选门正是原稿假绿的翻版)。</summary>
        [Test]
        public void test_d6_nullAssetRoot_isHardFailure()
        {
            Assert.Throws<ArgumentNullException>(
                () => AudioAssetExistence.Missing(new[] { "a.wav" }, null));
            Assert.Throws<ArgumentNullException>(
                () => AudioEventTableBake.BakeFromJson("{}", null));
        }

        /// <summary>AC-44-D6:存在性谓词本体(存在 ⇒ true / 缺失 ⇒ false),打**真**工程素材根。</summary>
        [Test]
        public void test_d6_existencePredicate_existingAndMissing()
        {
            string root = AudioAssetExistence.ResolveRoot(RepoRoot());

            Assert.That(AudioAssetExistence.Exists("sfx_breath_base_loop_small.wav", root), Is.True,
                "真种子引用的素材必须在工程内存在(D6 正向半边)");
            Assert.That(AudioAssetExistence.Exists("__definitely_absent__.wav", root), Is.False,
                "不存在的素材必须判缺失(D6 的判别力来源)");
        }

        /// <summary>AC-44-D6:缺失集**保序去重**(错误顺序确定,便于逐条比对)。</summary>
        [Test]
        public void test_d6_missing_isOrderPreservingAndDeduped()
        {
            string root = AudioAssetExistence.ResolveRoot(RepoRoot());
            IReadOnlyList<string> missing = AudioAssetExistence.Missing(
                new[] { "b.wav", "a.wav", "b.wav", null, "" }, root);

            Assert.That(missing, Is.EqualTo(new[] { "b.wav", "a.wav" }),
                "保序去重;null / 空串跳过(字段缺失归 schema 门,不归本谓词)");
        }

        /// <summary>AC-44-D6:字段缺失(assets[] = null)不归本谓词(归 schema 门)⇒ 空缺失集。</summary>
        [Test]
        public void test_d6_nullAssetList_yieldsNoMissing()
        {
            Assert.That(
                AudioAssetExistence.Missing(null, AudioAssetExistence.ResolveRoot(RepoRoot())),
                Is.Empty);
        }

        // ══════════════ 听诊窗预载(真生产谓词)══════════════

        /// <summary>听诊族 cue 集 = 表里 `bus == Stethoscope` 的行(真种子派生)。</summary>
        private static StethoscopePreloadGate GateFromRealSeed(
            Func<string, StethoscopePreloadGate.ClipLoadState> query)
        {
            AudioEventDataSet table =
                AudioEventTableBake.Materialize(AudioEventTableBake.BakeFromRepo(RepoRoot()));
            return StethoscopePreloadGate.FromTable(table, (cue) => query(cue));
        }

        /// <summary>听诊窗预载:未载入 ⇒ 不进听诊(AC:「进入 StethoscopeFocus 前 loadState == Loaded」)。</summary>
        [Test]
        public void test_preload_notLoaded_blocks()
        {
            StethoscopePreloadGate gate =
                GateFromRealSeed(_ => StethoscopePreloadGate.ClipLoadState.Unloaded);

            Assert.That(gate.IsReady(), Is.False, "未载入不得放行");
            Assert.That(gate.PendingCues(), Is.Not.Empty);
        }

        /// <summary>听诊窗预载:全族已载 ⇒ 放行。</summary>
        [Test]
        public void test_preload_allLoaded_allows()
        {
            StethoscopePreloadGate gate =
                GateFromRealSeed(_ => StethoscopePreloadGate.ClipLoadState.Loaded);

            Assert.That(gate.IsReady(), Is.True, "全族已载必须放行(否则门恒闭 = 功能死)");
            Assert.That(gate.PendingCues(), Is.Empty);
        }

        /// <summary>听诊窗预载:族内**只差一条** ⇒ 仍阻断。
        /// <para>判据是「全族就绪」,**不是**「要进的那一条就绪」—— GDD :640 的
        /// 「不得在听诊中因加载迟发」针对的是听诊**期间**任何一条迟到。</para></summary>
        [Test]
        public void test_preload_partialLoaded_blocks()
        {
            AudioEventDataSet table =
                AudioEventTableBake.Materialize(AudioEventTableBake.BakeFromRepo(RepoRoot()));
            StethoscopePreloadGate reference =
                StethoscopePreloadGate.FromTable(table, _ => StethoscopePreloadGate.ClipLoadState.Loaded);
            Assert.That(reference.RequiredCues.Count, Is.GreaterThan(1),
                "本断言要求听诊族 ≥ 2 条(真种子实为 2 条:两支呼吸层);族变小则本测失去判别力");

            string last = reference.RequiredCues[reference.RequiredCues.Count - 1];
            StethoscopePreloadGate gate = StethoscopePreloadGate.FromTable(
                table,
                cue => string.Equals(cue, last, StringComparison.Ordinal)
                    ? StethoscopePreloadGate.ClipLoadState.Loading
                    : StethoscopePreloadGate.ClipLoadState.Loaded);

            Assert.That(gate.IsReady(), Is.False, "族内任一条未 Loaded ⇒ 不得放行");
            Assert.That(gate.PendingCues(), Is.EqualTo(new[] { last }));
        }

        /// <summary>听诊窗预载:**载入失败** ≠ 就绪(放行会把静音误当「病人没啰音」)。</summary>
        [Test]
        public void test_preload_failedState_blocks()
        {
            StethoscopePreloadGate gate =
                GateFromRealSeed(_ => StethoscopePreloadGate.ClipLoadState.Failed);

            Assert.That(gate.IsReady(), Is.False, "Failed 不是放行态(静音 ≠ 无啰音)");
        }

        /// <summary>听诊窗预载:空 cue 集 ⇒ **构造期硬失败**(空集会让门恒真 = 假绿)。</summary>
        [Test]
        public void test_preload_emptyCueSet_isHardFailure()
        {
            Assert.Throws<ArgumentException>(() => new StethoscopePreloadGate(
                new List<string>(), _ => StethoscopePreloadGate.ClipLoadState.Loaded));
        }

        /// <summary>听诊窗预载:查询缝为 null ⇒ 硬失败(无查询 = 无法证明已载 = 不得放行)。</summary>
        [Test]
        public void test_preload_nullQuery_isHardFailure()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new StethoscopePreloadGate(new[] { "BreathLayer_Base_Calm" }, null));
        }

        /// <summary>听诊窗预载:族集从**真种子表**派生(不是另立清单 —— ADR-024 §⑥ 单一出处)。</summary>
        [Test]
        public void test_preload_fromTable_derivesStethoscopeFamily()
        {
            StethoscopePreloadGate gate =
                GateFromRealSeed(_ => StethoscopePreloadGate.ClipLoadState.Loaded);

            // ⚠️ 2026-10-17:内容批为 `Pulse_Contact` / `Steth_Contact` 也选了 Stethoscope 总线
            //    (承 GDD `:810`「把脉与听诊同层」)⇒ 听诊族由 2 条扩到 4 条。
            //    本测的判别力在「**只取** bus == Stethoscope 的行」,**不在「恰好 2 条」** ⇒
            //    改为与「从真种子独立推出的期望集」逐项比,条数不钉死。
            Assert.That(gate.RequiredCues, Is.EquivalentTo(ExpectedStethoscopeCues()),
                "听诊族 = 表里 bus == Stethoscope 的行,按 ordinal 序(期望集从真种子独立推出)");
        }

        /// <summary>听诊窗预载:总线名与 `MixerRegistry.BusNames` 一致(两处字面量不得分叉)。</summary>
        [Test]
        public void test_preload_stethoscopeBusName_matchesMixerRegistry()
        {
            Assert.That(MixerRegistry.BusNames, Does.Contain(StethoscopePreloadGate.StethoscopeBus),
                "StethoscopePreloadGate.StethoscopeBus 必须 ∈ MixerRegistry.BusNames");
        }

        /// <summary>听诊窗预载**接线**:门挂在贴耳的真实入口 `HandoverMux` 上 ——
        /// 未就绪 ⇒ `TryEnterStethoscope` 返回 false 且**相位不变 / 句柄不建 / 计数不增**。</summary>
        [Test]
        public void test_preload_handoverMux_gateBlocksEntry()
        {
            var sink = new ProbeSink();
            StethoscopePreloadGate gate =
                GateFromRealSeed(_ => StethoscopePreloadGate.ClipLoadState.Unloaded);
            var mux = new HandoverMux(sink, handoverTicks: 10, preloadGate: gate);
            mux.EnterWorldLayer(Cue(3), 0);

            bool entered = mux.TryEnterStethoscope(Cue(1), 0);

            Assert.That(entered, Is.False, "预载未就绪 ⇒ 不进听诊");
            Assert.That(mux.Phase, Is.EqualTo(HandoverPhase.WorldOnly), "相位不得变");
            Assert.That(mux.HandoverCount, Is.Zero, "交接计数不得增(未发生交接)");
            Assert.That(sink.BeginLoopCount, Is.EqualTo(1), "只应有世界层那一次 BeginLoop");
        }

        /// <summary>听诊窗预载**接线**:就绪 ⇒ 放行,且相位 / 计数按 AC-009-2 推进。</summary>
        [Test]
        public void test_preload_handoverMux_gateAllowsEntry()
        {
            var sink = new ProbeSink();
            StethoscopePreloadGate gate =
                GateFromRealSeed(_ => StethoscopePreloadGate.ClipLoadState.Loaded);
            var mux = new HandoverMux(sink, handoverTicks: 10, preloadGate: gate);
            mux.EnterWorldLayer(Cue(3), 0);

            bool entered = mux.TryEnterStethoscope(Cue(1), 0);

            Assert.That(entered, Is.True, "全族已载 ⇒ 放行");
            Assert.That(mux.Phase, Is.EqualTo(HandoverPhase.HandingToStethoscope));
            Assert.That(mux.HandoverCount, Is.EqualTo(1));
        }

        /// <summary>听诊窗预载:不设门(旧构造)⇒ 行为不变 —— 门是**可选**接线,不改既有语义。</summary>
        [Test]
        public void test_preload_handoverMux_noGate_legacyBehaviourUnchanged()
        {
            var sink = new ProbeSink();
            var mux = new HandoverMux(sink, handoverTicks: 10);
            mux.EnterWorldLayer(Cue(3), 0);

            Assert.That(mux.TryEnterStethoscope(Cue(1), 0), Is.True);
            Assert.That(mux.PreloadGate, Is.Null);
        }

        /// <summary>听诊窗预载 —— 真 `AudioClip.loadState` 的**适配器契约**(2026-10-17 补口)。
        /// <para>⚠️ <b>本测前身是「NOT-RUN 缺口哨兵」</b>(<c>test_preload_realAudioClipLoadState_pendingPlayModeHalf</c>),
        /// 它自陈的未覆盖面 = 「Unity 侧 loadState 枚举与 ClipLoadState 的适配器」。
        /// 该件已于 2026-10-17 落地(<see cref="AudioClipLoadStateAdapter"/>)⇒ 哨兵按自身口径
        /// **转成真断言**(不是被当回归修掉)。</para>
        /// <para>本测覆盖**纯映射**;真 clip 的 `Unloaded→Loaded` 过渡由 PlayMode 件承载
        /// (<c>unity/Assets/Tests/PlayMode/Audio/stethoscope_preload_loadstate_test.cs</c>)。</para></summary>
        [Test]
        public void test_preload_adapter_mapsAllFourUnityLoadStates()
        {
            Assert.That(AudioClipLoadStateAdapter.Map(AudioDataLoadState.Unloaded),
                Is.EqualTo(StethoscopePreloadGate.ClipLoadState.Unloaded));
            Assert.That(AudioClipLoadStateAdapter.Map(AudioDataLoadState.Loading),
                Is.EqualTo(StethoscopePreloadGate.ClipLoadState.Loading));
            Assert.That(AudioClipLoadStateAdapter.Map(AudioDataLoadState.Loaded),
                Is.EqualTo(StethoscopePreloadGate.ClipLoadState.Loaded));
            Assert.That(AudioClipLoadStateAdapter.Map(AudioDataLoadState.Failed),
                Is.EqualTo(StethoscopePreloadGate.ClipLoadState.Failed));
        }

        /// <summary>适配器:**没有 clip ≠ 已载**(null ⇒ Failed ⇒ 门阻断)。
        /// <para>若映射成 <c>Loaded</c>,「素材没接上」会被读成「就绪」⇒ 进听诊听静音 ⇒
        /// 把静音误当「病人没啰音」。</para></summary>
        [Test]
        public void test_preload_adapter_nullClip_isFailedNotLoaded()
        {
            Assert.That(AudioClipLoadStateAdapter.StateOf(null),
                Is.EqualTo(StethoscopePreloadGate.ClipLoadState.Failed),
                "null clip 必须是 Failed —— 放行会把静音误当「病人没啰音」");
        }

        /// <summary>适配器:解析器为 null ⇒ 构造期硬失败(与 <see cref="StethoscopePreloadGate"/>
        /// 的 null 查询缝同款)。</summary>
        [Test]
        public void test_preload_adapter_nullResolver_isHardFailure()
        {
            Assert.Throws<ArgumentNullException>(() => AudioClipLoadStateAdapter.Query(null));
        }

        /// <summary>适配器接上**真门**:查不到 clip 的 cue ⇒ 门阻断(端到端方向一致)。</summary>
        [Test]
        public void test_preload_adapter_unresolvableCue_blocksGate()
        {
            StethoscopePreloadGate gate = StethoscopePreloadGate.FromTable(
                AudioEventTableBake.Materialize(AudioEventTableBake.BakeFromRepo(RepoRoot())),
                AudioClipLoadStateAdapter.Query(_ => null));

            Assert.That(gate.IsReady(), Is.False,
                "解析不到 clip ⇒ 全族 Failed ⇒ 门必须阻断");
            Assert.That(gate.PendingCues(), Is.Not.Empty);
        }

        // ══════════════ 烘焙确定性 / 往返 / 头契约 ══════════════

        /// <summary>ADR-014 §二:同源两次烘焙**逐位一致**。</summary>
        [Test]
        public void test_bake_sameSeedTwice_bitIdentical()
        {
            byte[] a = AudioEventTableBake.BakeFromRepo(RepoRoot()).Cooked;
            byte[] b = AudioEventTableBake.BakeFromRepo(RepoRoot()).Cooked;

            Assert.That(a, Is.EqualTo(b), "同源两次烘焙必须逐位一致(ADR-014 §二)");
        }

        /// <summary>设置注册表同款确定性。</summary>
        [Test]
        public void test_bake_settings_sameSeedTwice_bitIdentical()
        {
            byte[] a = AudioSettingsRegistryBake.BakeFromRepo(RepoRoot()).Cooked;
            byte[] b = AudioSettingsRegistryBake.BakeFromRepo(RepoRoot()).Cooked;

            Assert.That(a, Is.EqualTo(b), "同源两次烘焙必须逐位一致(ADR-014 §二)");
        }

        /// <summary>行序 = cue ordinal(**语义**,不是实现细节):真种子的 5 行按序解出。</summary>
        [Test]
        public void test_bake_rowOrderIsCueOrdinal()
        {
            AudioEventDataSet table =
                AudioEventTableBake.Materialize(AudioEventTableBake.BakeFromRepo(RepoRoot()));

            Assert.That(table.CueAt(0), Is.EqualTo("Voice_Cough_Damp"));
            Assert.That(table.CueAt(4), Is.EqualTo("Music_Encounter_Layer"));
            Assert.That(table.TryGetOrdinal("BreathLayer_Base_Calm", out int ord), Is.True);
            Assert.That(ord, Is.EqualTo(1), "ordinal = rows[] 行序");
            Assert.That(table.TryGetOrdinal("__absent__", out int none), Is.False);
            Assert.That(none, Is.EqualTo(AudioEventDataSet.NoOrdinal));
        }

        /// <summary>越界 ordinal ⇒ 抛(静默返回 default 会藏 bug)。</summary>
        [Test]
        public void test_bake_rowOutOfRange_throws()
        {
            AudioEventDataSet table =
                AudioEventTableBake.Materialize(AudioEventTableBake.BakeFromRepo(RepoRoot()));
            Assert.Throws<ArgumentOutOfRangeException>(() => table.Row(table.RowCount));
            Assert.Throws<ArgumentOutOfRangeException>(() => table.Row(-1));
        }

        /// <summary>cooked 头契约(魔数 / formatVersion / schemaVersion 非 0 / payloadLength)。</summary>
        [Test]
        public void test_bake_headerContract_audioAndSettings()
        {
            byte[] evt = AudioEventTableBake.BakeFromRepo(RepoRoot()).Cooked;
            byte[] set = AudioSettingsRegistryBake.BakeFromRepo(RepoRoot()).Cooked;

            Assert.DoesNotThrow(() => AudioEventTableBake.AssertHeaderContract(evt));
            Assert.DoesNotThrow(() => AudioSettingsRegistryBake.AssertHeaderContract(set));
        }

        /// <summary>头契约自检**有判别力**:坏头必须被拒(否则 `DoesNotThrow` 空洞为真)。</summary>
        [Test]
        public void test_bake_headerContract_rejectsBrokenHeaders()
        {
            byte[] good = AudioSettingsRegistryBake.BakeFromRepo(RepoRoot()).Cooked;

            byte[] badMagic = (byte[])good.Clone();
            badMagic[0] = (byte)'X';
            Assert.Throws<BakeValidationException>(
                () => AudioSettingsRegistryBake.AssertHeaderContract(badMagic));

            byte[] badSchema = (byte[])good.Clone();
            badSchema[8] = 0; badSchema[9] = 0; badSchema[10] = 0; badSchema[11] = 0;
            Assert.Throws<BakeValidationException>(
                () => AudioSettingsRegistryBake.AssertHeaderContract(badSchema));

            Assert.Throws<BakeValidationException>(
                () => AudioSettingsRegistryBake.AssertHeaderContract(null));
        }

        /// <summary>跨侧等值断言:往返物化**逐项**比对(布局分叉 = 红)。
        /// <para>本测的判别力由「改坏写方 ⇒ 本测红」的证伪轮证明(见 evidence 原件)。</para></summary>
        [Test]
        public void test_bake_materialize_crossSideMatches()
        {
            Assert.DoesNotThrow(() =>
                AudioEventTableBake.Materialize(AudioEventTableBake.BakeFromRepo(RepoRoot())));
            Assert.DoesNotThrow(() =>
                AudioSettingsRegistryBake.Materialize(AudioSettingsRegistryBake.BakeFromRepo(RepoRoot())));
        }

        /// <summary>设置注册表:源**行序变化**不改载荷字节(行无 ordinal 语义 ⇒ 无意义的编辑
        /// 不得假红 ConfigVersion 之外的东西)。
        /// <para>⚠️ 头槽 `ConfigVersion` 是**源文本**哈希 ⇒ 行序变化**会**改它(这是设计:
        /// 源变了就该变)。本断言钉的是**载荷**(头后字节)不受行序影响。</para></summary>
        [Test]
        public void test_bake_settings_payloadIndependentOfSourceRowOrder()
        {
            byte[] forward = AudioSettingsRegistryBake.BakeFromJson(SettingsJson(reverse: false)).Cooked;
            byte[] reverse = AudioSettingsRegistryBake.BakeFromJson(SettingsJson(reverse: true)).Cooked;

            Assert.That(reverse, Is.Not.EqualTo(forward),
                "前提:行序确实不同(否则本测空转)—— 头槽 ConfigVersion 随源文本变");
            Assert.That(Payload(reverse), Is.EqualTo(Payload(forward)),
                "载荷必须与源行序无关(编码按 param Ordinal 排序)");
        }

        /// <summary>设置注册表:从 `FallbackRows` 生成的 8 行 JSON 可过门(名字/值双向对账)。</summary>
        [Test]
        public void test_bake_settings_fixtureFromFallbackPassesGate()
        {
            AudioSettingsRegistryBake.BakeOutput output =
                AudioSettingsRegistryBake.BakeFromJson(SettingsJson(reverse: false));

            Assert.That(output.Bound.Rows.Count, Is.EqualTo(MixerRegistry.SettingsVisibleParameters.Count));
            AudioSettingsDataSet dataSet = AudioSettingsRegistryBake.Materialize(output);
            Assert.That(dataSet.RowCount, Is.EqualTo(output.Bound.Rows.Count));
        }

        // ══════════════ 接线(裁定 ④:最小接线)══════════════

        /// <summary>接线:注入 Provider ⇒ `Rows` = 烘焙行(非 FallbackRows)。</summary>
        [Test]
        public void test_wiring_providerInjection_overridesFallback()
        {
            AudioSettingsDataSet dataSet =
                AudioSettingsRegistryBake.Materialize(
                    AudioSettingsRegistryBake.BakeFromRepo(RepoRoot()));

            AudioSettingsRegistry.Provider = () => dataSet.Rows;

            Assert.That(AudioSettingsRegistry.Rows.Count, Is.EqualTo(dataSet.RowCount));
            Assert.That(AudioSettingsRegistry.TryGetRow(MixerRegistry.MonoParameter, out var row), Is.True);
            Assert.That(row.Kind, Is.EqualTo(SettingsValueKind.Bool));
        }

        /// <summary>接线:未注入 / 注入返回空 ⇒ 回退到 `FallbackRows`(设计内的降级面)。</summary>
        [Test]
        public void test_wiring_providerAbsentOrEmpty_usesFallback()
        {
            AudioSettingsRegistry.Provider = null;
            IReadOnlyList<SettingsRegistryRow> fallback = AudioSettingsRegistry.Rows;
            Assert.That(fallback.Count, Is.EqualTo(MixerRegistry.SettingsVisibleParameters.Count));

            AudioSettingsRegistry.Provider = () => new List<SettingsRegistryRow>();
            Assert.That(AudioSettingsRegistry.Rows.Count, Is.EqualTo(fallback.Count),
                "空集注入不得让 Rows 变空(否则设置壳拿不到任何行)");
        }

        /// <summary>接线:烘焙分区与回退集**逐键同值**(AC-44-13 的运行期观测面;
        /// 构建期已由 `AudioSettingsRegistryBinder.ValidateValueReconciliation` 双向对账)。</summary>
        [Test]
        public void test_wiring_bakedRows_matchFallbackValues()
        {
            AudioSettingsDataSet dataSet =
                AudioSettingsRegistryBake.Materialize(
                    AudioSettingsRegistryBake.BakeFromRepo(RepoRoot()));

            foreach (SettingsRegistryRow baked in dataSet.Rows)
            {
                Assert.That(TryFind(AudioSettingsRegistry.FallbackRows, baked.Param, out SettingsRegistryRow fb),
                    Is.True, $"回退集缺「{baked.Param}」");
                Assert.That(baked.FactoryDefault, Is.EqualTo(fb.FactoryDefault),
                    $"「{baked.Param}」出厂默认烘焙 vs 回退不一致");
            }
        }

        /// <summary>按名线性查行(回退集的只读面;`AudioSettingsRegistry.TryGetRow` 走的是
        /// **当前生效**行集,本断言要的恰是**回退集本身**)。</summary>
        private static bool TryFind(
            IReadOnlyList<SettingsRegistryRow> rows, string param, out SettingsRegistryRow row)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (!string.Equals(rows[i].Param, param, StringComparison.Ordinal)) continue;
                row = rows[i];
                return true;
            }
            row = default;
            return false;
        }

        /// <summary>接线:Addressables address 常量与产物文件名**同一出处**(禁两处字面量)。</summary>
        [Test]
        public void test_wiring_addresses_singleSourceOfTruth()
        {
            Assert.That(AddressablesDataProvider.AudioEventsAddress,
                Is.EqualTo(AudioEventDataSet.CookedFileName));
            Assert.That(AddressablesDataProvider.AudioSettingsAddress,
                Is.EqualTo(AudioSettingsDataSet.CookedFileName));
            Assert.That(AudioEventTableBake.CookedFileName, Is.EqualTo(AudioEventDataSet.CookedFileName));
            Assert.That(AudioSettingsRegistryBake.CookedFileName, Is.EqualTo(AudioSettingsDataSet.CookedFileName));
        }

        /// <summary>接线:菜单是 binder 的**唯一调用点链路**存在性自检 ——
        /// 两个烘焙菜单项与两个组菜单项必须存在(否则门永不执行)。</summary>
        [Test]
        public void test_wiring_bakeMenuEntriesExist()
        {
            Type menu = typeof(AudioEventTableBake).Assembly.GetTypes()
                .FirstOrDefault(t => t.Name == "DataBakeMenu");
            Assert.That(menu, Is.Not.Null, "DataBakeMenu 必须存在(Editor.Tools.Bake)");

            string[] expected =
            {
                "BakeAudioEvents", "BakeAudioSettingsRegistry",
                "EnsureDataCoreGroup", "EnsureAudioGroup",
            };
            foreach (string name in expected)
                Assert.That(menu.GetMethod(name, BindingFlags.Public | BindingFlags.Static), Is.Not.Null,
                    $"菜单方法 {name} 缺失 ⇒ 该链路永不执行(binder 悬空)");
        }

        // ══════════════ 接线:BootRoot 真身(裁定 ④ · 兑清 story-011 §登记项)══════════════
        // ⚠️ 上四条 `test_wiring_*` 打的是**缝**(`AudioSettingsRegistry.Provider` 本身);
        //    本组打的是**注入方真身**(`BootRoot.ApplyAudioSettingsProvider`)——
        //    只有缝而没有注入方 ⇒ 「接线存在」是不可证伪的口头承诺(本 sprint 的病根)。

        /// <summary>预载成功 ⇒ 注入生效,`Rows` 取自 provider(**非** `FallbackRows`)。
        /// <para>⚠️ 供给数据集**刻意只有 1 行**且 param 为合成名:若与 `FallbackRows` 同值,
        /// 「注入了」与「没注入」在断言上不可分 ⇒ 该夹具零判别力。</para></summary>
        [Test]
        public void test_bootroot_injection_whenPreloaded_rowsComeFromProvider()
        {
            const string probeParam = "__s14_6_probe__";
            var oneRow = new AudioSettingsDataSet(
                new[]
                {
                    new SettingsRegistryRow(probeParam, SettingsValueKind.Bool, 0.0, 0.0,
                        SettingsValue.OfBool(false), "probe", "probe"),
                },
                schemaVersion: 1u, configVersion: 0u);

            AudioSettingsRegistry.Provider = null;

            BootRoot.ApplyAudioSettingsProvider(preloaded: true, new StubDataProvider(oneRow));

            Assert.That(AudioSettingsRegistry.Provider, Is.Not.Null,
                "预载成功却未注入 ⇒ 接线断裂(设置壳恒走回退集,AC-44-13 真源永不生效)");
            Assert.That(AudioSettingsRegistry.Rows.Count, Is.EqualTo(1),
                "Rows 未取自 provider(仍为 FallbackRows ⇒ 注入是空转)");
            Assert.That(AudioSettingsRegistry.Rows[0].Param, Is.EqualTo(probeParam));
        }

        /// <summary>预载**失败** ⇒ 不注入,`Rows` 走回退集(设计内的降级面)。</summary>
        [Test]
        public void test_bootroot_noInjection_whenNotPreloaded_rowsStayFallback()
        {
            AudioSettingsRegistry.Provider = null;
            int fallbackCount = AudioSettingsRegistry.FallbackRows.Count;

            BootRoot.ApplyAudioSettingsProvider(preloaded: false, new StubDataProvider(default));

            Assert.That(AudioSettingsRegistry.Provider, Is.Null,
                "预载失败却注入了 ⇒ 启动期硬失败被静默降级成「半装配」");
            Assert.That(AudioSettingsRegistry.Rows.Count, Is.EqualTo(fallbackCount));
        }

        /// <summary>provider 为 null ⇒ 不注入(与预载失败同一条早退路径)。</summary>
        [Test]
        public void test_bootroot_noInjection_whenProviderNull_rowsStayFallback()
        {
            AudioSettingsRegistry.Provider = null;
            int fallbackCount = AudioSettingsRegistry.FallbackRows.Count;

            BootRoot.ApplyAudioSettingsProvider(preloaded: true, null);

            Assert.That(AudioSettingsRegistry.Provider, Is.Null);
            Assert.That(AudioSettingsRegistry.Rows.Count, Is.EqualTo(fallbackCount));
        }

        // ══════════════ Play Mode Script 门(TD-audio-01f 的机器侧漂移)══════════════

        /// <summary>Play Mode Script 必须指向「Use Asset Database」。
        /// <para>⚠️ <b>这是机器前置断言,不是纯逻辑断言</b> —— 被测对象是
        /// <c>ProjectConfigData.ActivePlayModeIndex</c>,它住
        /// <c>unity/Library/AddressablesConfig.dat</c>(**机器本地、gitignored**),
        /// <b>不在</b>入库的 <c>AddressableAssetSettings.asset</c> 里
        /// (包源码 <c>ProjectConfigData.cs:390</c>)。</para>
        /// <para><b>为什么值得一条测试</b>:留在「Use Existing Build」时,PlayMode 解析的是
        /// <c>Library/com.unity.addressables/aa/&lt;platform&gt;/catalog.bin</c> 这份预建 catalog;
        /// 组条目一变就陈旧 ⇒ <c>InvalidKeyException: No Location found for Key=…</c>,
        /// 启动序整条挂掉。**2026-09-24 与 2026-10-17 两次同症状**,两次都花了排查成本
        /// (第二次被误诊成「新检出须重建 catalog」)。本测把该漂移变成**一条具名红**。</para>
        /// <para><b>如果你是有意切到 Existing Build 做 bundle 观测</b>:本测会红,这是设计。
        /// 观测完跑菜单「大医精诚/数据管线/Play Mode 归位(Use Asset Database)」即可。</para></summary>
        [Test]
        public void test_playModeScript_isAssetDatabase()
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            Assert.That(settings, Is.Not.Null, "本机 Addressables 设置缺失(机器前置)");

            int expected = DataBakeMenu.FindAssetDatabaseBuilderIndex(settings);
            Assert.That(expected, Is.GreaterThanOrEqualTo(0),
                "DataBuilders 名单里找不到名含「Asset Database」的 builder —— 名单被改过?");

            int active = settings.ActivePlayModeDataBuilderIndex;
            string activeName = settings.GetDataBuilder(active)?.Name ?? "<null>";

            Assert.That(active, Is.EqualTo(expected),
                $"Play Mode Script 现为「{activeName}」(DataBuilder[{active}]),应为「Use Asset Database」。\n" +
                "后果:PlayMode 去解析 Library 下的**预建 catalog**,组条目一变就陈旧 ⇒\n" +
                "      InvalidKeyException: No Location found for Key=… ⇒ 启动序整条红。\n" +
                "修:菜单「大医精诚/数据管线/Play Mode 归位(Use Asset Database)」。\n" +
                "⚠️ 该设置住 unity/Library/AddressablesConfig.dat(机器本地、gitignored)" +
                "⇒ 不入库、**不可** git checkout 恢复。");
        }

        /// <summary>谓词负向半边:null 设置 / 无匹配名 ⇒ <c>-1</c>(不得抛,不得返回 0 冒充命中)。</summary>
        [Test]
        public void test_playModeScript_predicate_nullOrMiss_returnsMinusOne()
        {
            Assert.That(DataBakeMenu.FindAssetDatabaseBuilderIndex(null), Is.EqualTo(-1),
                "null 设置必须回 -1 —— 回 0 会让调用方把「没找到」当成「就是第 0 个」");
        }

        // ══════════════ U1 Spike Teardown 的 Play Mode 复原动作(TD-audio-01f 残留② · 2026-10-17)══════════════

        /// <summary>Teardown 的复原动作<b>本身</b>在漂移态下确实把它拉回「Use Asset Database」。</summary>
        /// <remarks>
        /// <para><b>被测对象</b>:<c>U1SpikeSetup.RestorePlayModeCore</c>(2026-10-17 从 <c>Teardown</c>
        /// 的 <c>finally</c> 抽出 —— 抽出前该动作无任何夹具面)。</para>
        /// <para><b>判别力</b>:把 <c>RestorePlayModeCore</c> 改成空体 ⇒ 本测<b>必红</b>
        /// (夹具先把索引切到 Existing Build,空体不会把它拉回)。</para>
        /// <para>⚠️ <b>本测不证明「Teardown 调用了它」</b> —— 接线半边仍无执行判据(见
        /// <c>test_u1teardown_restorePlayMode_isWiredIntoTeardown</c>)。</para>
        /// <para>⚠️ <b>会短暂改动本机的真实 Play Mode 设置</b>(<c>Library/AddressablesConfig.dat</c>,
        /// 机器本地)。<c>finally</c> 无条件写回原值;写回失败会 <c>Assert.Inconclusive</c>,
        /// 不静默留痕 —— 静默留痕正是 <c>TD-audio-01f</c> 本身。</para>
        /// </remarks>
        [Test]
        public void test_u1teardown_restorePlayMode_corePullsDriftedStateBack()
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            Assert.That(settings, Is.Not.Null, "本机 Addressables 设置缺失(机器前置)");

            int target = DataBakeMenu.FindAssetDatabaseBuilderIndex(settings);
            Assert.That(target, Is.GreaterThanOrEqualTo(0),
                "DataBuilders 名单里找不到名含「Asset Database」的 builder —— 名单被改过?");
            Assert.That(settings.DataBuilders.Count, Is.GreaterThan(1),
                "前提:DataBuilders 至少两个,否则无法构造漂移态(全库只有一个 builder 时本测无意义)");

            int original = settings.ActivePlayModeDataBuilderIndex;
            int drifted = -1;
            for (int i = 0; i < settings.DataBuilders.Count; i++)
            {
                if (i != target) { drifted = i; break; }
            }
            Assert.That(drifted, Is.GreaterThanOrEqualTo(0), "前提:存在一个非 Asset Database 的 builder");

            string driftedName = settings.GetDataBuilder(drifted)?.Name ?? "<null>";
            try
            {
                // ── Arrange:构造漂移态(等价于 ConfigurePlayMode 留下的状态)
                settings.ActivePlayModeDataBuilderIndex = drifted;
                Assert.That(settings.ActivePlayModeDataBuilderIndex, Is.EqualTo(drifted),
                    $"夹具前提:设置可写(实测切到「{driftedName}」未生效)");

                // ── Act
                DaYiJingCheng.EditorTools.Spike.U1SpikeSetup.RestorePlayModeCore(settings);

                // ── Assert
                int after = settings.ActivePlayModeDataBuilderIndex;
                string afterName = settings.GetDataBuilder(after)?.Name ?? "<null>";
                Assert.That(after, Is.EqualTo(target),
                    $"RestorePlayModeCore 未把 Play Mode 拉回「Use Asset Database」——\n" +
                    $"  漂移态 = [{drifted}]「{driftedName}」· 期望 = [{target}] · 实得 = [{after}]「{afterName}」\n" +
                    "后果:PlayMode 解析预建 catalog,组条目一变即 InvalidKeyException(TD-audio-01f)。");
            }
            finally
            {
                // ── 无条件写回原值(本测不得在本机留下副作用)
                settings.ActivePlayModeDataBuilderIndex = original;
                if (settings.ActivePlayModeDataBuilderIndex != original)
                {
                    Assert.Inconclusive(
                        $"⚠️ 本测的 finally 未能把 Play Mode 写回原值 [{original}] ——\n" +
                        "   本机设置现处于非预期态。手动跑菜单「大医精诚/数据管线/Play Mode 归位」。");
                }
            }
        }

        /// <summary>null 设置 ⇒ 不抛(<c>Teardown</c> 的 <c>finally</c> 不得因设置缺失而炸)。</summary>
        [Test]
        public void test_u1teardown_restorePlayMode_nullSettings_doesNotThrow()
        {
            Assert.DoesNotThrow(
                () => DaYiJingCheng.EditorTools.Spike.U1SpikeSetup.RestorePlayModeCore(null),
                "设置缺失时 Teardown 的复原步骤必须静默返回 —— 抛异常会盖掉 Teardown 主路径的日志");
        }

        /// <summary>接线:<c>Teardown</c> 的 <c>finally</c> 确实调用了 <c>RestorePlayModeCore</c>。</summary>
        /// <remarks>
        /// ⚠️ <b>这是弱判据(源码扫描),不是执行判据</b> —— 如实登记,不升格为「已证」。
        /// 它只抓「有人删掉这行调用」这一类回归;抓不住「调用位置从 finally 挪走」「抛出被吞」。
        /// 补法归 PlayMode 手跑或将来把 Teardown 改为可脚本驱动的形式。
        /// <para>本测断言的是<b>词法存在性</b>(源码里含该调用),不是行为等价 ——
        /// 因此即便本测绿,<c>接线</c>半边仍标 <c>Proven = 弱</c>。</para>
        /// </remarks>
        [Test]
        public void test_u1teardown_restorePlayMode_isWiredIntoTeardown()
        {
            string path = Path.Combine(RepoRoot(), "unity/Assets/Editor.Tools.Spike/U1SpikeSetup.cs");
            Assert.That(File.Exists(path), Is.True, $"源文件不存在:{path}");
            string src = File.ReadAllText(path);

            int teardown = src.IndexOf("static void Teardown()", StringComparison.Ordinal);
            Assert.That(teardown, Is.GreaterThanOrEqualTo(0), "找不到 Teardown 方法定义 —— 源码结构变了?");

            int finallyIdx = src.IndexOf("finally", teardown, StringComparison.Ordinal);
            Assert.That(finallyIdx, Is.GreaterThanOrEqualTo(0),
                "Teardown 内找不到 finally —— 复原已不在异常路径上?");

            // ⚠️ 扫描窗必须止于 Teardown 的方法体边界 —— 不能一路扫到文件尾:
            //    本文件的**文档注释**与方法**定义**里都出现 `RestorePlayModeCore`,
            //    首版 `src.Substring(finallyIdx)` 因此对「删掉调用」的变异**照样命中** ⇒ 假绿
            //    (2026-10-17 证伪轮实测:删调用后本测仍绿,已就地修)。
            //    边界 = finally 之后第一个「与 finally 行同缩进的闭合花括号」所在行。
            string finallyLine = LineContaining(src, finallyIdx);
            int indent = IndentOf(finallyLine);
            int windowEnd = EndOfBlockAfter(src, finallyIdx, indent);
            Assert.That(windowEnd, Is.GreaterThan(finallyIdx), "无法确定 Teardown 的方法体边界");

            string window = src.Substring(finallyIdx, windowEnd - finallyIdx);
            int call = window.IndexOf("RestorePlayModeCore(", StringComparison.Ordinal);
            Assert.That(call, Is.GreaterThanOrEqualTo(0),
                "Teardown 的 finally 段里找不到 RestorePlayModeCore 调用 ——\n" +
                "Play Mode 复原接线被删了?后果见 TD-audio-01f 残留②。\n" +
                $"扫描窗 = [{finallyIdx}, {windowEnd}),长度 {windowEnd - finallyIdx} 字符。");
        }

        private static string LineContaining(string src, int idx)
        {
            int start = src.LastIndexOf('\n', Math.Max(0, idx - 1)) + 1;
            int end = src.IndexOf('\n', idx);
            return end < 0 ? src.Substring(start) : src.Substring(start, end - start);
        }

        private static int IndentOf(string line)
        {
            int i = 0;
            while (i < line.Length && (line[i] == ' ' || line[i] == '\t')) i++;
            return i;
        }

        /// <summary>从 <paramref name="from"/> 之后找第一个「行首缩进 ≤ <paramref name="indent"/> 且含 '}'」的行尾。</summary>
        private static int EndOfBlockAfter(string src, int from, int indent)
        {
            int pos = src.IndexOf('\n', from);
            while (pos >= 0 && pos < src.Length - 1)
            {
                int lineStart = pos + 1;
                string line = LineContaining(src, lineStart);
                if (line.TrimStart().StartsWith("}", StringComparison.Ordinal) && IndentOf(line) <= indent)
                {
                    return lineStart;
                }
                pos = src.IndexOf('\n', lineStart);
            }
            return -1;
        }

        // ══════════════ R-2 归属闭合(Story 016 · AC-44-E1/E3/E4 · 三处结合)══════════════

        /// <summary>A 锁:**经总门入口**驱动 —— C 改名且 <b>事件表也是改后的名</b> ⇒ 总门必须点名 A 锁。</summary>
        /// <remarks>
        /// ⚠️ 本条与 <see cref="test_e1_dataTableCueRenamed_lockedByConstant"/> 的差别是**关键**:
        /// 那条只调内层 <c>ValidateRequirementTableMatches</c>,测不到「总门是否真的接上这把锁」。
        /// 2026-10-17 证伪轮实测:停用总门里的 A 锁,全套 87 条**一条不红** ⇒ 那条是假绿面。
        /// 本条把 cue 在**数据件与事件表两侧同时改名**,使下层覆盖检查全部通过,
        /// **唯一**能报错的就是 A 锁 ⇒ 判别力归 A 锁。
        /// </remarks>
        [Test]
        public void test_e1_gateEntry_pointNamesALockWhenOnlyLockCanCatch()
        {
            AudioRequirementsData data = AudioRequirementsBake.BindFromJson(
                ReadSeedJson(AudioRequirementsBake.SourceFileName)
                    .Replace("\"cue\": \"Clinic_Room_Tone\"", "\"cue\": \"Clinic_Room_ToneV2\""));

            // 事件表侧同步改成 V2 ⇒ E1 覆盖检查通过,只剩 A 锁可报
            AudioEventTable full = AudioEventTableBake.BakeFromRepo(RepoRoot()).Bound.Table;
            var rows = full.Rows.Select(r => new AudioEventTableRow
            {
                Cue = r.Cue == "Clinic_Room_Tone" ? "Clinic_Room_ToneV2" : r.Cue,
                Assets = r.Assets,
            }).ToList();

            var errors = AudioRequirementsBake.ValidateAtAssetRoot(
                data, rows, AudioAssetExistence.ResolveRoot(RepoRoot()));

            Assert.That(errors, Has.Some.Contains("A 锁").And.Some.Contains("Clinic_Room_ToneV2"),
                "总门未接上 A 锁(C 与事件表同步改名,唯一能报的只有 A 锁):\n" + string.Join("\n", errors));
        }

        /// <summary>A 锁:数据件多出的未登记项 —— **经总门入口**同样必须被点名。</summary>
        [Test]
        public void test_e1_gateEntry_pointRejectsExtraRequirement()
        {
            string json = ReadSeedJson(AudioRequirementsBake.SourceFileName);
            int at = json.IndexOf("\"world_cue_split\"", StringComparison.Ordinal);
            Assert.That(at, Is.GreaterThan(0), "夹具前提:数据件含 world_cue_split");
            int reqEnd = json.LastIndexOf(']', at - 1);   // ⚠️ at 指向键名引号 ⇒ 从 at-1 往回找
            string injected = json.Insert(reqEnd,
                ",\n    {\"id\": \"SFX-99\", \"name\": \"未登记项\", \"cue\": \"Pulse_Contact\", \"files\": [\"sfx_pulse_contact_01.wav\"]}");
            AudioRequirementsData data = AudioRequirementsBake.BindFromJson(injected);

            AudioEventTable full = AudioEventTableBake.BakeFromRepo(RepoRoot()).Bound.Table;
            var errors = AudioRequirementsBake.ValidateAtAssetRoot(
                data, full.Rows, AudioAssetExistence.ResolveRoot(RepoRoot()));

            Assert.That(errors, Has.Some.Contains("SFX-99"),
                "总门未拦下多出的需求项:\n" + string.Join("\n", errors));
        }

        /// <summary>正向:真数据件 + 真事件表 + 真素材库 ⇒ 零错误(E1 / E4 全绿)。</summary>
        [Test]
        public void test_e1_realRequirements_gatePassesWithNoError()
        {
            AudioRequirementsData data = AudioRequirementsBake.BindFromRepo(RepoRoot());
            AudioEventTable table = AudioEventTableBake.BakeFromRepo(RepoRoot()).Bound.Table;

            var errors = AudioRequirementsBake.ValidateAtAssetRoot(
                data, table.Rows, AudioAssetExistence.ResolveRoot(RepoRoot()));

            Assert.That(errors, Is.Empty,
                "R-2 归属门在真种子上报错:\n" + string.Join("\n", errors));
        }

        /// <summary>A 锁:C 改了而 A 没改 ⇒ 具名差集红(即使 C 与 B 同步改了)。</summary>
        [Test]
        public void test_e1_dataTableCueRenamed_lockedByConstant()
        {
            AudioRequirementsData data = AudioRequirementsBake.BindFromJson(
                ReadSeedJson(AudioRequirementsBake.SourceFileName)
                    .Replace("\"cue\": \"Clinic_Room_Tone\"", "\"cue\": \"Clinic_Room_ToneV2\""));

            var errors = AudioEventTableGates.ValidateRequirementTableMatches(data);
            Assert.That(errors, Has.Some.Contains("AMB-1").And.Some.Contains("Clinic_Room_ToneV2"),
                "A 锁未拦下「数据件改名而常量未改」:\n" + string.Join("\n", errors));
        }

        /// <summary>E1:删掉事件表里的一项 cue ⇒ 该 R-2 项报「不在事件表内」。</summary>
        [Test]
        public void test_e1_eventTableMissingCue_reportsThatRequirement()
        {
            AudioRequirementsData data = AudioRequirementsBake.BindFromRepo(RepoRoot());
            AudioEventTable full = AudioEventTableBake.BakeFromRepo(RepoRoot()).Bound.Table;
            var rows = full.Rows.Where(r => r.Cue != "Pulse_Contact").ToList();
            Assert.That(rows.Count, Is.EqualTo(full.Rows.Count - 1), "夹具前提:确实删掉了一行");

            var errors = AudioEventTableGates.ValidateEventTableCoverage(data, rows);
            Assert.That(errors, Has.Some.Contains("SFX-1").And.Some.Contains("Pulse_Contact"),
                "删 cue 后 E1 未点名该 R-2 项:\n" + string.Join("\n", errors));
        }

        /// <summary>E1:世界声 4 档位 cue 少一条 ⇒ 红(9 项拆 4 cue 的归属缺一)。</summary>
        [Test]
        public void test_e1_worldSoundCueMissing_reportsSplitGap()
        {
            AudioRequirementsData data = AudioRequirementsBake.BindFromRepo(RepoRoot());
            AudioEventTable full = AudioEventTableBake.BakeFromRepo(RepoRoot()).Bound.Table;
            var rows = full.Rows.Where(r => r.Cue != "World_Disaster").ToList();

            var errors = AudioEventTableGates.ValidateEventTableCoverage(data, rows);
            Assert.That(errors, Has.Some.Contains("World_Disaster"),
                "世界声缺一档未报:\n" + string.Join("\n", errors));
        }

        /// <summary>E4:素材库多出一个无归属文件 ⇒ 红(有素材无归属)。</summary>
        [Test]
        public void test_e4_libraryExtraUnregisteredFile_rejected()
        {
            AudioRequirementsData data = AudioRequirementsBake.BindFromRepo(RepoRoot());
            var files = new List<string>(
                AudioRequirementsBake.ListWavFiles(AudioAssetExistence.ResolveRoot(RepoRoot())));
            files.Add("sfx_mystery_01.wav");

            var errors = AudioEventTableGates.ValidateLibraryCoverage(data, files);
            Assert.That(errors, Has.Some.Contains("sfx_mystery_01.wav"),
                "无归属素材未报:\n" + string.Join("\n", errors));
        }

        /// <summary>E4:需求文件不在库里 ⇒ 红(归属已裁而文件缺失)。</summary>
        [Test]
        public void test_e4_requiredFileAbsent_rejected()
        {
            AudioRequirementsData data = AudioRequirementsBake.BindFromRepo(RepoRoot());
            var files = new List<string>(
                AudioRequirementsBake.ListWavFiles(AudioAssetExistence.ResolveRoot(RepoRoot())));
            files.Remove("amb_clinic_room_loop.wav");

            var errors = AudioEventTableGates.ValidateLibraryCoverage(data, files);
            Assert.That(errors, Has.Some.Contains("amb_clinic_room_loop.wav"),
                "需求文件缺失未报:\n" + string.Join("\n", errors));
        }

        /// <summary>E3:删掉一个被引 <c>.wav</c> ⇒ <b>构建期 throw</b>(不是告警、不是判过)。</summary>
        /// <remarks>
        /// ⚠️ 本测**不碰文件系统**(<c>test_discipline_noFilesystemWrites</c> 钉死这一条)——
        /// 它把素材根指向一个**不存在的路径**,效果等价于「库是空的」,断言
        /// <see cref="BakeValidationException"/> 且报文点名该文件。不 <c>CreateDirectory</c>,
        /// 也就没有 <c>finally</c> 清理;「真删盘上文件」那半边由 story-010 的 D6 夹具覆盖。
        /// </remarks>
        [Test]
        public void test_e3_missingRequiredFile_throwsAtBakeTime()
        {
            string emptyRoot = Path.Combine(Path.GetTempPath(), "e3-absent-root-" + Guid.NewGuid().ToString("N"));

            var ex = Assert.Throws<BakeValidationException>(
                () => AudioEventTableBake.BakeFromJson(ReadSeedJson("audio_events.json"), emptyRoot));
            Assert.That(ex.Message, Does.Contain("sfx_pulse_contact_01.wav"),
                "E3 抛了但未点名缺失文件:\n" + ex.Message);
        }

        /// <summary>B 支:真权威件 ↔ 常量 逐项对账 = 零错误(出处与代码一致)。</summary>
        [Test]
        public void test_e1_manifestAgreesWithConstants_realDoc()
        {
            var errors = AudioRequirementsBake.ValidateManifestAgrees(RepoRoot());
            Assert.That(errors, Is.Empty,
                "权威件与代码常量不一致:\n" + string.Join("\n", errors));
        }

        /// <summary>B 支:权威件删掉一个冻结文件名 ⇒ 红(文档滞后 / 代码超前都会被拦)。</summary>
        [Test]
        public void test_e1_manifestMissingFile_rejected()
        {
            string path = Path.Combine(RepoRoot(), AudioRequirementsBake.ManifestRelative);
            string text = File.ReadAllText(path);
            const string needle = "amb_clinic_room_loop.wav";
            Assert.That(text, Does.Contain(needle), "夹具前提:权威件含该冻结文件");
            int first = text.IndexOf(needle, StringComparison.Ordinal);
            string mutated = text.Remove(first, needle.Length).Insert(first, "amb_clinic_room_loopX.wav");

            var errors = AudioRequirementsBake.ValidateManifestText(mutated);
            Assert.That(errors, Has.Some.Contains(needle),
                "权威件缺文件未报:\n" + string.Join("\n", errors));
        }

        /// <summary>B 支:花括号复合写法的展开(权威件用 <c>{a,b}</c> 记脚步材质)。</summary>
        [Test]
        public void test_e1_expandBraces_expandsSingleLevel()
        {
            var got = AudioRequirementsBake.ExpandBraces("sfx_footstep_{stone,dirt,humus,snow}_01.wav");
            Assert.That(got, Is.EqualTo(new[]
            {
                "sfx_footstep_stone_01.wav", "sfx_footstep_dirt_01.wav",
                "sfx_footstep_humus_01.wav", "sfx_footstep_snow_01.wav",
            }), "花括号展开结果不符");
        }

        // ══════════════ Import Load Type 门(OQ-44-2 连带裁定 · GDD §OQ-44-2 附)══════════════

        /// <summary>GDD §OQ-44-2 附 的实测表逐行复算(判据 = 该表,非记忆)。
        /// <para>⚠️ 本测**硬编码**时长 / 驻留 —— 属 `.claude/rules/test-standards.md` 的
        /// 「boundary value tests where the exact number IS the point」豁免:
        /// 这里断言的**就是** GDD 表格本身,换成常量文件反而看不出「门与 GDD 是否一致」。</para></summary>
        [Test]
        public void test_importSettings_requiredLoadType_matchesGddTable()
        {
            // 行 = GDD §OQ-44-2 附「本次实测套用」表(5 行)
            Assert.That(AudioImportSettingsGate.RequiredLoadType(
                "music_encounter_layer_loop.wav", 23.3160, 2056512L), Is.EqualTo(2), "music_* → Streaming");
            Assert.That(AudioImportSettingsGate.RequiredLoadType(
                "sfx_breath_base_loop_small.wav", 1.9500, 171990L), Is.EqualTo(0), "呼吸底层 → DecompressOnLoad");
            Assert.That(AudioImportSettingsGate.RequiredLoadType(
                "sfx_breath_adventitious_fine.wav", 1.9500, 171990L), Is.EqualTo(0), "附加音 → DecompressOnLoad");
            Assert.That(AudioImportSettingsGate.RequiredLoadType(
                "vo_patient_cough_damp_f1.wav", 1.5840, 139734L), Is.EqualTo(0), "语声 f1 → DecompressOnLoad");
            Assert.That(AudioImportSettingsGate.RequiredLoadType(
                "vo_patient_cough_damp_m1.wav", 1.2480, 110124L), Is.EqualTo(0), "语声 m1 → DecompressOnLoad");
        }

        /// <summary>阈值边界(命中形态 = <b>阈值边界类</b>,S12-4 强制证伪)。
        /// <para>判据 = GDD §附 表:「≤ 512 KB <b>且</b> &lt; 5 s」小档 ·「&gt; 4 MB <b>或</b> ≥ 30 s」大档。
        /// 两条都是**同时/或**结构 ⇒ 边界两侧各取一点,钉住「且 / 或」不被改成「或 / 且」。</para></summary>
        [Test]
        public void test_importSettings_boundaries_areInclusiveExclusiveAsSpecified()
        {
            const long kb512 = AudioImportSettingsGate.SmallResidentBytes;   // 512 * 1024
            const long mb4 = AudioImportSettingsGate.LargeResidentBytes;     // 4 * 1024 * 1024

            // ── 小档:「≤ 512 KB」含等号 ·「< 5 s」不含等号 ⇒ 两条都须成立 ──
            Assert.That(AudioImportSettingsGate.RequiredLoadType("x.wav", 4.999, kb512), Is.EqualTo(0),
                "512 KB 整 + 4.999 s ⇒ 小档(≤ 是闭区间)");
            Assert.That(AudioImportSettingsGate.RequiredLoadType("x.wav", 4.999, kb512 + 1), Is.EqualTo(1),
                "512 KB + 1 字节 ⇒ 跌出小档 ⇒ 中档");
            Assert.That(AudioImportSettingsGate.RequiredLoadType("x.wav", 5.0, 1024), Is.EqualTo(1),
                "恰 5.0 s ⇒ 跌出小档(< 是开区间)⇒ 中档");
            Assert.That(AudioImportSettingsGate.RequiredLoadType("x.wav", 0.0, 0), Is.EqualTo(0),
                "零时长零字节 ⇒ 小档(下界不退化成别的档)");

            // ── 大档:「> 4 MB」不含等号 ·「≥ 30 s」含等号 ⇒ 任一成立即升 ──
            Assert.That(AudioImportSettingsGate.RequiredLoadType("x.wav", 1.0, mb4), Is.EqualTo(1),
                "恰 4 MB ⇒ 不升档(> 是开区间)");
            Assert.That(AudioImportSettingsGate.RequiredLoadType("x.wav", 1.0, mb4 + 1), Is.EqualTo(2),
                "4 MB + 1 字节 ⇒ 大档");
            Assert.That(AudioImportSettingsGate.RequiredLoadType("x.wav", 30.0, 1024), Is.EqualTo(2),
                "恰 30 s ⇒ 大档(≥ 是闭区间)—— 与「恰 5.0 s」开闭相反,是本测的主要判别点");
            Assert.That(AudioImportSettingsGate.RequiredLoadType("x.wav", 29.999, 1024), Is.EqualTo(1),
                "29.999 s ⇒ 中档");
        }

        /// <summary>`music_*` 的**语义档覆盖**优先于数值判据(GDD §附 登记的「口径张力」)。
        /// <para>⚠️ 这是 §附 里唯一一处**判据重叠**的裁定(23.3 s / 2008 KB 按数值落中档,
        /// 按语义取 Streaming)。本测把它钉死 ⇒ 若有人「顺手」把阈值改成
        /// 「> 512 KB 即 Streaming」,本测**仍会绿**(覆盖规则未动),但
        /// <see cref="test_importSettings_boundaries_areInclusiveExclusiveAsSpecified"/> 会红 ——
        /// 两条互补,合起来才锁住口径。</para></summary>
        [Test]
        public void test_importSettings_musicPrefix_overridesNumericTier()
        {
            // 完全落在小档的数值 ⇒ 数值判据会说 0,语义档必须说 2
            Assert.That(AudioImportSettingsGate.RequiredLoadType("music_x.wav", 1.0, 1024), Is.EqualTo(2),
                "music_* 无视数值判据,恒 Streaming(§附:audio.md:217 语义档 + loop 长驻)");

            // 仅前缀区分:同数值、不同前缀 ⇒ 不同档
            Assert.That(AudioImportSettingsGate.RequiredLoadType("sfx_x.wav", 1.0, 1024), Is.EqualTo(0),
                "同数值非 music_* ⇒ 走数值判据(小档)—— 证明差异来自前缀,不是数值");

            // 前缀须**从头**匹配,不得中缀命中
            Assert.That(AudioImportSettingsGate.RequiredLoadType("vo_music_x.wav", 1.0, 1024), Is.EqualTo(0),
                "「music_」出现在中缀不算 —— 必须是 StartsWith,不是 Contains");
        }

        /// <summary><b>真环境扫描</b>:素材根下 5 个 wav 的 `.meta` 现值须等于应然档位。
        /// <para>本测即 GDD §附「验证命令」的可执行版 —— 后续 12 个 `vo_` 变体落地时**自动纳入**,零改门。</para></summary>
        [Test]
        public void test_importSettings_realMetas_matchRequiredTier()
        {
            string root = AudioImportSettingsGate.ResolveRoot(RepoRoot());
            Assert.That(Directory.Exists(root), Is.True, $"素材根不存在:{root}(机器前置)");

            string[] wavs = Directory.GetFiles(root, "*.wav", SearchOption.TopDirectoryOnly);
            Assert.That(wavs.Length, Is.GreaterThanOrEqualTo(5),
                $"素材根下应至少有 S14-6 落的 5 个 wav,实测 {wavs.Length} 个 —— 素材被删?");

            IReadOnlyList<string> violations = AudioImportSettingsGate.Validate(root);
            Assert.That(violations, Is.Empty,
                "Import Load Type 与 GDD §OQ-44-2 附 判据不符:\n  " + string.Join("\n  ", violations));
        }

        /// <summary>负向半边:wav 头不可读 / meta 缺失 ⇒ **报违例**,不得静默跳过。
        /// <para>⚠️ 与 `AudioAssetExistence.Missing` 同款纪律 —— 「读不到 ⇒ 跳过」正是
        /// story-010 原稿 `Assert.Ignore` 假绿的翻版。本测用**不存在**的路径触发,
        /// 零文件系统写入(承本夹具的 I-读-不-写 纪律)。</para></summary>
        [Test]
        public void test_importSettings_unreadableInput_isViolationNotSilentSkip()
        {
            string bogus = Path.Combine(Path.GetTempPath(), "__dyjc_no_such_dir__", "nope.wav");

            Assert.That(AudioImportSettingsGate.TryReadWaveInfo(
                bogus, out double dur, out long dec, out string err), Is.False,
                "不存在的 wav 必须回 false");
            Assert.That(err, Is.Not.Null.And.Not.Empty, "失败必须带原因,不得静默");
            Assert.That(dur, Is.EqualTo(0.0), "失败时时长归零");
            Assert.That(dec, Is.EqualTo(0L), "失败时驻留归零");

            Assert.That(AudioImportSettingsGate.TryReadLoadType(
                bogus + ".meta", out int lt, out string mErr), Is.False, "不存在的 meta 必须回 false");
            Assert.That(lt, Is.EqualTo(-1), "失败时档位归 -1(不得回 0 冒充 DecompressOnLoad)");
            Assert.That(mErr, Is.Not.Null.And.Not.Empty);

            // 根不存在 ⇒ Validate 回**一条**违例,不是空列表(空列表 = 合规)
            IReadOnlyList<string> v = AudioImportSettingsGate.Validate(
                Path.Combine(Path.GetTempPath(), "__dyjc_no_such_dir__"));
            Assert.That(v.Count, Is.EqualTo(1), "根不存在须报 1 条违例");
            Assert.That(v[0], Does.Contain("根不存在"));
        }

        /// <summary>wav 扩展名判定**跨平台大小写一致**(承 story-017 W-2 同型缺陷)。
        /// <para>⚠️ 立此测的实测依据:2026-10-17 探针(net8.0 · Linux)证明
        /// `Directory.GetFiles(dir, "*.wav")` **区分大小写** —— 目录含
        /// `lower.wav` / `UPPER.WAV` / `Mixed.Wav` 时**只返回 `lower.wav`**,另两个静默漏扫。
        /// 而 CI 平台正是 Linux(ADR-012)⇒ 素材批若交付 `.WAV`,门会**少报违例**。</para>
        /// <para>夹具形态 = **纯谓词直断**,不碰文件系统(承本文件「零文件写入」纪律)。</para></summary>
        [Test]
        public void test_importSettings_wavExtension_isCaseInsensitiveAcrossPlatforms()
        {
            // 三形态均须被识别(判别力:停用 OrdinalIgnoreCase ⇒ 后两条转红)
            Assert.That(AudioImportSettingsGate.IsWavName("lower.wav"), Is.True, "全小写须识别");
            Assert.That(AudioImportSettingsGate.IsWavName("UPPER.WAV"), Is.True,
                "全大写须识别 —— Linux 下 GetFiles(\"*.wav\") 会漏掉它");
            Assert.That(AudioImportSettingsGate.IsWavName("Mixed.Wav"), Is.True, "混写须识别");

            // 路径形态(带目录)也须成立 —— Validate 收到的是 GetFiles 回的完整路径
            Assert.That(AudioImportSettingsGate.IsWavName("/a/b/c/vo_x.WAV"), Is.True, "带目录路径须识别");

            // 反面:非 wav / 空 / 无扩展名 / 伪后缀 一律 false(否则会把 .wav.bak 也扫进来)
            Assert.That(AudioImportSettingsGate.IsWavName("note.txt"), Is.False);
            Assert.That(AudioImportSettingsGate.IsWavName("noext"), Is.False);
            Assert.That(AudioImportSettingsGate.IsWavName("x.wav.bak"), Is.False, "伪后缀不得命中");
            Assert.That(AudioImportSettingsGate.IsWavName(""), Is.False);
            Assert.That(AudioImportSettingsGate.IsWavName(null), Is.False);
        }

        // ══════════════ 夹具纪律自证 ══════════════

        /// <summary>本夹具**零文件写入**(S14-6 删除的副作用段的结构性守卫)。
        /// <para>扫描面 = 本文件自身源码文本(经 `[CallerFilePath]` 定位),**注释已剥离**。</para>
        /// <para>⚠️ 剥离注释是必需的,不是宽松化:本文件头注**逐字记载**了被删掉的那三个调用
        /// (那正是本测要守的东西),不剥离则扫描命中自己的说明文字 ⇒ 恒红、零判别力。</para></summary>
        [Test]
        public void test_discipline_noFilesystemWrites()
        {
            string thisFile = SourcePath();
            Assert.That(File.Exists(thisFile), Is.True, $"夹具源文件定位失败:{thisFile}");
            string src = StripComments(File.ReadAllText(thisFile));

            // ⚠️ 令牌**拼接构造**:整字面量会让本数组自身命中扫描 ⇒ 本测恒红 = 无判别力。
            //    拼接后源码里不存在整串,扫描才只捞真调用点。
            string[] forbidden =
            {
                "File.Write" + "AllText", "File.Write" + "AllBytes",
                "Directory.Create" + "Directory", "File.De" + "lete", "Directory.De" + "lete",
            };
            var hits = forbidden.Where(f => src.Contains(f)).ToList();

            Assert.That(hits, Is.Empty,
                "本夹具不得写文件系统(.claude/rules/test-standards.md)—— 命中:" + string.Join(", ", hits));
        }

        /// <summary>剥掉 `//` 行注释与 `/* */` 块注释(字符串字面量内的 `//` 不误伤)。
        /// <para>只用于上述纪律自证:注释里写「我们不再调用 X」不是调用。</para></summary>
        private static string StripComments(string src)
        {
            var sb = new StringBuilder(src.Length);
            for (int i = 0; i < src.Length; i++)
            {
                char c = src[i];

                // 字符串字面量(含逐字串 / 字符字面量)整段照抄,内部 `//` 不当注释。
                if (c == '"' || c == '\'')
                {
                    char quote = c;
                    bool verbatim = i > 0 && src[i - 1] == '@';
                    sb.Append(c);
                    for (i++; i < src.Length; i++)
                    {
                        sb.Append(src[i]);
                        if (!verbatim && src[i] == '\\' && i + 1 < src.Length)
                        {
                            sb.Append(src[i + 1]);
                            i++;
                            continue;
                        }
                        if (src[i] == quote) break;
                    }
                    continue;
                }

                if (c == '/' && i + 1 < src.Length && src[i + 1] == '/')
                {
                    while (i < src.Length && src[i] != '\n') i++;
                    sb.Append('\n');
                    continue;
                }

                if (c == '/' && i + 1 < src.Length && src[i + 1] == '*')
                {
                    i += 2;
                    while (i + 1 < src.Length && !(src[i] == '*' && src[i + 1] == '/')) i++;
                    i++;                       // 跳过 `*/` 的 `/`(外层 for 再 +1)
                    sb.Append(' ');
                    continue;
                }

                sb.Append(c);
            }
            return sb.ToString();
        }

        // ══════════════ 测试替身与工具方法 ══════════════

        /// <summary>组区分门生产件源路径(story-017 的源码扫描面)。</summary>
        private static string GatesSourcePath()
            => Path.Combine(RepoRoot(), "unity", "Assets", "Editor.Tools.Gates", "AudioAssetGroupGates.cs");

        /// <summary>从源码文本里切出某方法的**整物理行区间**(签名所在行首 → 闭括号所在行尾)。
        /// <para>⚠️ **刻意按物理行切**(含注释 / 空行 / 花括号),因为 AC-44-D5c 的 40 行口径
        /// 就是物理行 —— 换口径会让夹具对原实现也放行(假绿)。</para>
        /// <para>⚠️ 括号配平**逐字符**做,故调用方须先 <see cref="StripComments"/>:
        /// 注释里的 `{` / `}` 会让配平跑偏。字符串字面量里的花括号本判据面内不出现。</para></summary>
        private static string ExtractMethodText(string src, string signatureFragment)
        {
            int sig = src.IndexOf(signatureFragment, StringComparison.Ordinal);
            Assert.That(sig, Is.GreaterThanOrEqualTo(0),
                $"源内未找到「{signatureFragment}」—— 夹具过期(方法被改名 / 搬家),不是判别力缺口");

            int lineStart = src.LastIndexOf('\n', sig) + 1;
            int open = src.IndexOf('{', sig);
            Assert.That(open, Is.GreaterThanOrEqualTo(0), "签名后无方法体 `{`");

            int depth = 0, close = -1;
            for (int i = open; i < src.Length; i++)
            {
                if (src[i] == '{') depth++;
                else if (src[i] == '}' && --depth == 0) { close = i; break; }
            }
            Assert.That(close, Is.GreaterThan(open), "方法体未闭合");

            int lineEnd = src.IndexOf('\n', close);
            if (lineEnd < 0) lineEnd = src.Length;
            return src.Substring(lineStart, lineEnd - lineStart);
        }

        /// <summary>数方法体里的**决策点**(if / for / foreach / while / case / catch / `&amp;&amp;` / `||`)。
        /// <para>⚠️ 三元 `?` **刻意不计** —— 与 `?.` / `??` / 可空类型 `?` 在不做词法分析的前提下
        /// 无法区分,硬计会引入假阳性。本判据面内不出现三元(若将来出现,本计数器须一并升级)。</para>
        /// <para>调用方按 `1 + CountDecisionPoints(body)` 得圈复杂度。</para></summary>
        private static int CountDecisionPoints(string body)
        {
            if (string.IsNullOrEmpty(body)) return 0;

            int n = 0;
            foreach (string word in new[] { "if", "for", "foreach", "while", "case", "catch" })
                n += CountWholeWord(body, word);
            n += CountLiteral(body, "&&");
            n += CountLiteral(body, "||");
            return n;
        }

        /// <summary>按**词界**计数(前后须非标识符字符)——
        /// `foreach` 不误计 `for`,`notify` 不误计 `if`。</summary>
        private static int CountWholeWord(string src, string word)
        {
            int n = 0, i = 0;
            while ((i = src.IndexOf(word, i, StringComparison.Ordinal)) >= 0)
            {
                int end = i + word.Length;
                bool leftOk = i == 0 || !IsIdentChar(src[i - 1]);
                bool rightOk = end >= src.Length || !IsIdentChar(src[end]);
                if (leftOk && rightOk) n++;
                i = end;
            }
            return n;
        }

        /// <summary>按整串计数(用于 `&amp;&amp;` / `||` 这类非词法记号)。</summary>
        private static int CountLiteral(string src, string token)
        {
            int n = 0, i = 0;
            while ((i = src.IndexOf(token, i, StringComparison.Ordinal)) >= 0) { n++; i += token.Length; }
            return n;
        }

        private static bool IsIdentChar(char c) => char.IsLetterOrDigit(c) || c == '_';

        /// <summary>读仓库作者态种子(只读)。</summary>
        private static string ReadSeedJson(string fileName)
            => File.ReadAllText(Path.Combine(RepoRoot(), "assets", "data", fileName));

        /// <summary>真种子的 `rows[]` 行数(独立于烘焙模型 —— 用于「烘焙行数 == 源行数」的自洽断言)。</summary>
        private static int ReadSeedRowCount()
        {
            string json = ReadSeedJson("audio_events.json");
            var m = System.Text.RegularExpressions.Regex.Match(json, "\"rows\"\\s*:\\s*\\[");
            Assert.That(m.Success, "真种子必须有 rows[] 数组");
            // 行对象以 "{"cue"" 起头 —— 与 AudioEventTableGates.RowKeys 的必填首键一致。
            return System.Text.RegularExpressions.Regex.Matches(json, "\"cue\"\\s*:").Count;
        }

        /// <summary>真种子里**第一个**被引用的素材名(`assets[]` 首项)。负向夹具据此构造「真名 → 假名」。</summary>
        private static string FirstAssetNameInSeed(string json)
        {
            var m = System.Text.RegularExpressions.Regex.Match(json, "\"assets\"\\s*:\\s*\\[\\s*\"([^\"]+\\.wav)\""); 
            return m.Success ? m.Groups[1].Value : null;
        }

        /// <summary>听诊族期望集 —— 从真种子**独立**推出(`bus == Stethoscope` 的行,按 ordinal 序)。
        /// <para>刻意不复用 `StethoscopePreloadGate` 的解析:夹具要与被测量**不同源**,否则恒真。</para></summary>
        private static string[] ExpectedStethoscopeCues()
        {
            // ⚠️ 2026-10-17 三次修正 —— 为什么不用正则切块:
            //    ① `[^{}]*"cue"` 在 `assets[]`(嵌套数组)前就断;
            //    ② `[^{}]*` 又会命中 `tier_map` 的内层对象 / 附加音层的嵌套对象
            //      (实测:`BreathLayer_Adventitious_Fine` 的 `adventitious_policy.{trigger_phase,jitter}`
            //       块在行之后出现,正则块把「行 cue」与「子块 bus」配了对 —— 漏行)。
            //    正解 = **顶层花括号扫描**:只在 depth 返回 0 时切出一个完整对象,
            //    再在完整对象里取 `"cue"` 与 `"bus"` —— 不依赖键序、不依赖嵌套形态。
            var cues = new List<string>();
            foreach (string obj in TopLevelObjectsInRows(ReadSeedJson("audio_events.json")))
            {
                var bus = System.Text.RegularExpressions.Regex.Match(obj, "\"bus\"\\s*:\\s*\"(Stethoscope)\"");
                var cue = System.Text.RegularExpressions.Regex.Match(obj, "\"cue\"\\s*:\\s*\"([^\"]+)\"");
                if (bus.Success && cue.Success) cues.Add(cue.Groups[1].Value);
            }
            Assert.That(cues, Is.Not.Empty, "真种子必须至少有一行 bus == Stethoscope(否则本断言空转)");
            // 行序 = 表内 ordinal,但预载门可能对族内重排(基础层优先)
            // ⇒ 夹具只钉**集合**(哪些 cue ∈ 听诊族),序的归属在门的实现里,不在本 AC 面内。
            return cues.ToArray();
        }

        /// <summary>`rows[]` 下的**顶层**对象文本(花括号深度扫描;嵌套对象不单独返回)。</summary>
        private static List<string> TopLevelObjectsInRows(string json)
        {
            int start = json.IndexOf("\"rows\"", System.StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThan(0), "真种子必须有 rows[]");
            int open = json.IndexOf('[', start);
            Assert.That(open, Is.GreaterThan(0), "rows 后必须有数组");

            var objects = new List<string>();
            int depth = 0, objStart = -1;
            for (int i = open + 1; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '"')
                {
                    // 跳过字符串字面量(含转义)
                    for (i++; i < json.Length; i++)
                    {
                        if (json[i] == '\\') { i++; continue; }
                        if (json[i] == '"') break;
                    }
                    continue;
                }
                if (c == '{')
                {
                    if (depth == 0) objStart = i;
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0 && objStart >= 0)
                    {
                        objects.Add(json.Substring(objStart, i - objStart + 1));
                        objStart = -1;
                    }
                }
                else if (c == ']' && depth == 0) break;
            }
            Assert.That(objects, Is.Not.Empty, "rows[] 必须至少有一个行对象");
            return objects;
        }

        /// <summary>从 `AudioSettingsRegistry.FallbackRows` 生成注册表 JSON(不另抄数值 —— 单一出处)。
        /// <para>⚠️ 生成物必须能过 `AudioSettingsRegistryBinder` 的名字 / 值双向对账,
        /// 否则本夹具会在「门」上先红(那才是它该红的地方)。</para></summary>
        private static string SettingsJson(bool reverse)
        {
            var rows = new List<SettingsRegistryRow>(AudioSettingsRegistry.FallbackRows);
            if (reverse) rows.Reverse();

            var sb = new StringBuilder();
            sb.Append("{\"schema_version\":1,\"_note\":\"fixture(S14-6 pipeline_gate_test)\",\"rows\":[");
            for (int i = 0; i < rows.Count; i++)
            {
                SettingsRegistryRow r = rows[i];
                if (i > 0) sb.Append(',');
                sb.Append("{\"param\":\"").Append(r.Param).Append("\",\"type\":\"")
                  .Append(r.Kind == SettingsValueKind.Bool ? "bool" : "float").Append('"');
                if (r.Kind == SettingsValueKind.Float)
                    sb.Append(",\"min\":").Append(Num(r.Min)).Append(",\"max\":").Append(Num(r.Max));
                sb.Append(",\"factory_default\":");
                sb.Append(r.Kind == SettingsValueKind.Bool
                    ? (r.FactoryDefault.Bool ? "true" : "false")
                    : Num(r.FactoryDefault.Float));
                sb.Append(",\"player_group\":\"").Append(r.PlayerGroup)
                  .Append("\",\"label_source\":\"").Append(r.LabelSource).Append("\"}");
            }
            sb.Append("]}");
            return sb.ToString();
        }

        private static string Num(double v)
            => v.ToString("R", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>cooked 载荷(剥 20 字节头)。</summary>
        private static byte[] Payload(byte[] cooked)
        {
            var payload = new byte[cooked.Length - CookedFormat.HeaderSize];
            Buffer.BlockCopy(cooked, CookedFormat.HeaderSize, payload, 0, payload.Length);
            return payload;
        }

        private static AudioCueDto Cue(int ordinal)
            => new AudioCueDto(ordinal, 0, default, -1, true);

        /// <summary>`IDataProvider` 替身:只回答 `AudioSettingsDataSet`(BootRoot 注入测专用)。
        /// <para>替身由本夹具自持是**正当**的 —— 被测对象是「注入动作是否发生」,不是「装载是否正确」;
        /// 装载正确性由 `AddressablesDataProvider` 自己的测与 codec 往返测覆盖。</para></summary>
        private sealed class StubDataProvider : IDataProvider
        {
            private readonly AudioSettingsDataSet _settings;

            public StubDataProvider(AudioSettingsDataSet settings) { _settings = settings; }

            public TDataSet Load<TDataSet>() where TDataSet : struct
                => typeof(TDataSet) == typeof(AudioSettingsDataSet)
                    ? (TDataSet)(object)_settings
                    : throw new InvalidOperationException(
                        $"StubDataProvider:未预期类型 {typeof(TDataSet).Name}(本替身只承载 AudioSettingsDataSet)");
        }

        /// <summary>句柄计数探针(只记 BeginLoop 次数;本夹具只关心「有没有建句柄」)。</summary>
        private sealed class ProbeSink : IAudioCueSink
        {
            public int BeginLoopCount;

            public void Emit(in AudioCueDto cue) { }

            public AudioCueHandle BeginLoop(in AudioCueDto cue)
            {
                BeginLoopCount++;
                return new AudioCueHandle(BeginLoopCount);
            }

            public void EndLoop(AudioCueHandle handle) { }

            public void SetTier(TierSource source) { }
        }
    }
}
