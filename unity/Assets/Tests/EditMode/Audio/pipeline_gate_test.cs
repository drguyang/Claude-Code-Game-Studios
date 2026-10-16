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
                () => DataBakeMenu.EnsureAudioGroupCore(null, "Assets/Audio"),
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
                () => DataBakeMenu.EnsureAudioGroupCore(settings, "Assets/__no_such_audio_dir__"),
                "空素材目录必须**抛异常**(LogWarning + return 同样让退出码保持 0)");

            Assert.That(ex.Message, Does.Contain("__no_such_audio_dir__"),
                "报文须点名出错的目录,否则无法定位");
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
            Assert.That(output.Bound.Table.Rows.Count, Is.EqualTo(5),
                "真种子 rows[] 5 行(改动种子须同改本断言)");
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
            string broken = json.Replace("vo_patient_cough_damp_m1.wav", "__gone__.wav");

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

            Assert.That(gate.RequiredCues, Is.EqualTo(new[]
            {
                "BreathLayer_Base_Calm", "BreathLayer_Adventitious_Fine",
            }), "听诊族 = 表里 bus == Stethoscope 的行,按 ordinal 序");
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

        /// <summary>听诊窗预载 —— **真 `AudioClip.loadState` 的 PlayMode 半边 = NOT-RUN**。
        /// <para>⚠️ 缺口哨兵(禁借绿):素材本体虽已落占位文件(2026-10-16 用户裁定 ①),
        /// 但**真 `AudioClip` 的 `loadState` 过渡**需要 PlayMode + 真导入 + 真 `LoadAudioData()`,
        /// 本 EditMode 夹具结构上跑不到。素材 Import Settings 定稿归 content 批(OQ-44-2);
        /// 该半边落地后本哨兵**转红** ⇒ 须随之补做真 PlayMode 断言,**不得当回归修掉**。</para></summary>
        [Test]
        public void test_preload_realAudioClipLoadState_pendingPlayModeHalf()
        {
            string assetRoot = AudioAssetExistence.ResolveRoot(RepoRoot());
            bool anyClipOnDisk = AudioAssetGroupGates.AudioAssetsOnDisk().Count > 0;

            Assert.That(anyClipOnDisk, Is.True,
                "占位素材已落(2026-10-16 裁定 ①)⇒ 本哨兵的前提成立。若素材消失,先修 D6 正向半边");
            Assert.That(assetRoot, Is.Not.Null.And.Not.Empty);

            Assert.Pass(
                "NOT-RUN(如实登记):真 AudioClip.loadState 的过渡(Unloaded→Loading→Loaded/Failed)" +
                "需要 PlayMode + 真导入 + 真 LoadAudioData(),EditMode 结构上不可达。" +
                "已覆盖:查询缝的双向语义(Unloaded/Loading/Loaded/Failed 四态)+ 族集派生 + 接线。" +
                "未覆盖:Unity 侧 loadState 枚举与 ClipLoadState 的适配器(归装配轮 + content 批)。");
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

        /// <summary>读仓库作者态种子(只读)。</summary>
        private static string ReadSeedJson(string fileName)
            => File.ReadAllText(Path.Combine(RepoRoot(), "assets", "data", fileName));

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
