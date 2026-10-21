// 权威来源:production/epics/audio-system/story-011-settings-exposure-reset.md
//   (注册表 AC① · AC-44-13 mono · AC-44-15 字幕键集 · AC-44-19 归零机制 · AC-44-12 文档判据)
//   · 44 拥有闭枚举 settings_visible 注册表(8 条 = 7 总线音量含 Master + mono)
//   · 归零三步:写默认 → 推 mixer → 落 sidecar;重启后仍 == 出厂集
//   · 字幕文本本体 = 事件表 subtitle_text;表键集 ⊇ 语声 cue 集
// ADR-018(音频架构:mono/总线音量义务)· ADR-013(控件呈现归 42)· ADR-014(出厂默认 = 烘焙分区)
// OQ-SS-5=甲(端本地 sidecar)· OQ-SS-7=甲(归零三步 = 写默认 → 推 mixer → 落 sidecar)
//
// ══════════════ S14-5(2026-10-16)改造说明 ══════════════
// 本文件此前 11 测中 🟢 真 1 · 🟡 半真 3 · 🔴 假 7(S13-9 评审原件 §2.5)——
// **被测对象不存在**,测试以自造 Fake + 本地字面量数组顶替 ⇒ 断言在对象缺席时仍绿。
// 本轮改造的三条纪律:
//   ① **删掉全部测试自造替身**(`SettingsVisibleRegistry` 本地数组 / `FakeSettingsStore` /
//      `FakeResetMechanism` / `HasSubtitleForCue` 判据复刻)⇒ 一律打**生产类**;
//   ② **判据单一出处**:族判定走 `AudioEventTableGates.IsSubtitleCue`,键集判定走
//      `AudioEventTableGates.ValidateSubtitles`,注册表对账走 `AudioSettingsRegistryBinder`
//      —— 测试只做**抽取**(玩家构建零 JSON 解析器纪律的测试侧对应),不做判断;
//   ③ **AC-44-13 的「出厂默认 ∈ 烘焙分区」断言恢复**(此前被修复轮删空留注释)。
//
// ⚠️ 落点:故事 Test Evidence 登记路径 = tests/unit/audio_system/settings_exposure_test.cs;
//    Unity 只编译 unity/Assets/ 树 ⇒ **真身 = 本文件**(承 Story 001-010/014 同一先例);
//    账本侧由 tests/unit/audio_system/README.md 互链。
// ⚠️ 测试纪律:arrange/act/assert · 无随机 · 无时间依赖(禁 DateTime/Time)·
//    夹具 / 种子读取前置 File.Exists 断言(缺失即红,不静默跳过)。
//    sidecar 测试落 `Path.GetTempPath()` 下的唯一子目录(承 `overrides_sidecar_test.cs` 先例),
//    SetUp 建 / TearDown 删 —— 不碰仓库树。

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using DaYiJingCheng.EditorTools.Bake;
using DaYiJingCheng.EditorTools.Gates;
using DaYiJingCheng.Gameplay.Presentation.Audio;
using DaYiJingCheng.Gameplay.Presentation.Settings;

namespace DaYiJingCheng.Tests.Unit.Audio
{
    [TestFixture]
    internal sealed class SettingsExposureTest
    {
        // ══════════════ 基础设施 ══════════════

        // 本文件在 unity/Assets/Tests/EditMode/Audio/ ⇒ Audio→EditMode→Tests→Assets→unity→
        // 仓库根 = 5 层(写 4 层 ⇒ root 落在 unity/ 下,夹具 / GDD / 源树全报「缺失」)。
        private static string repoRoot([CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile) ?? ".",
                "..", "..", "..", "..", ".."));

        private string _tempDir;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(),
                "dayj_audio_settings_" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
            }
            catch (IOException) { /* 清理失败不掩盖被测断言 */ }
            catch (UnauthorizedAccessException) { /* 同上 */ }
        }

        // ══════════════ 注册表 AC①:壳消费集 ⊆ settings_visible 闭枚举 ══════════════

        /// <summary>注册表 AC①:7 路总线音量 ⊆ 闭枚举(判据出处 = <see cref="MixerRegistry.SettingsVisibleParameters"/>,
        /// 非测试本地数组)。</summary>
        [Test]
        public void test_settingsRegistry_busVolumes_subsetOfRegistry()
        {
            // Arrange:闭枚举(生产常量)
            IReadOnlyList<string> registry = MixerRegistry.SettingsVisibleParameters;

            // Act:7 路总线音量参数(生产常量)
            IReadOnlyList<string> busVolumes = MixerRegistry.BusVolumeParameters;

            // Assert
            Assert.That(busVolumes.Count, Is.EqualTo(7),
                "总线音量参数必须恰 7 路(OQ-SS-3=甲:数据层恒 7 路)");
            for (int i = 0; i < busVolumes.Count; i++)
                Assert.That(registry, Does.Contain(busVolumes[i]),
                    $"总线音量参数「{busVolumes[i]}」必须 ∈ settings_visible 闭枚举(AC①:壳消费集 ⊆ 注册表)");
        }

        /// <summary>注册表 AC①:<c>mono_enabled</c> ∈ 闭枚举(AC-44-13)。</summary>
        [Test]
        public void test_settingsRegistry_monoEnabled_inRegistry()
        {
            // Arrange + Act + Assert
            Assert.That(MixerRegistry.SettingsVisibleParameters, Does.Contain(MixerRegistry.MonoParameter),
                "mono_enabled 必须 ∈ settings_visible 闭枚举(AC-44-13:提供 mono 选项)");
        }

        /// <summary>注册表 AC①:闭枚举恰 8 条(7 总线 + mono),不多不少。</summary>
        [Test]
        public void test_settingsRegistry_exactly8Entries()
        {
            // Arrange + Act + Assert
            Assert.That(MixerRegistry.SettingsVisibleParameters.Count, Is.EqualTo(8),
                "settings_visible 闭枚举必须恰 8 条(7 总线音量含 Master + mono)");
        }

        /// <summary>注册表 AC① 负向:未登记参数被**生产代码**拒收(两处:
        /// <see cref="AudioSettingsRegistry.TryGetRow"/> 返回 false;store 的 Set 抛
        /// <see cref="ArgumentOutOfRangeException"/>)。</summary>
        [Test]
        public void test_settingsRegistry_unregisteredParam_rejected()
        {
            // Arrange:未登记参数 + 真 store
            const string unregistered = "bus_volume_unknown_bus";
            var store = new AudioSettingsStore(_tempDir);

            // Act
            bool found = AudioSettingsRegistry.TryGetRow(unregistered, out _);

            // Assert
            Assert.That(found, Is.False,
                "未登记参数必须 ∉ 注册表(AC①:越集 = 断言失败)");
            Assert.Throws<ArgumentOutOfRangeException>(
                () => store.Set(unregistered, SettingsValue.OfFloat(0.5)),
                "壳直写非注册表参数 = 控制清单 Forbidden 项 ⇒ 生产 store 必须抛(不得静默接受)");
        }

        // ══════════════ 烘焙注册表:名字 / 值 双向对账(AC-44-13 的结构面)══════════════

        /// <summary>真种子过烘焙门:8 行绑定零错误,且行集**恰** = 闭枚举(名字对账的构建期执行体)。</summary>
        [Test]
        public void test_bakedRegistry_realSeed_bindsWithZeroErrors()
        {
            // Arrange
            string path = Path.Combine(repoRoot(), "assets", "data", "audio_settings_registry.json");
            Assert.That(File.Exists(path), Is.True,
                $"真种子缺失(story-011 交付物,AC-44-13):{path}");

            // Act
            AudioSettingsRegistryBinder.BindResult bound = AudioSettingsRegistryBake.BindFromRepo(repoRoot());

            // Assert
            Assert.That(bound.Rows.Count, Is.EqualTo(MixerRegistry.SettingsVisibleParameters.Count),
                "烘焙注册表行数须恰 = 闭枚举 8 员(名字对账)");
            for (int i = 0; i < MixerRegistry.SettingsVisibleParameters.Count; i++)
                Assert.That(ContainsRow(bound.Rows, MixerRegistry.SettingsVisibleParameters[i]), Is.True,
                    $"烘焙注册表须含闭枚举成员「{MixerRegistry.SettingsVisibleParameters[i]}」");
        }

        /// <summary>负向:烘焙注册表**缺**一个闭枚举成员 ⇒ 构建期红(名字对账)。</summary>
        [Test]
        public void test_bakedRegistry_missingParam_rejected()
        {
            // Arrange:删掉 mono 行
            string json = ReadRealRegistryJson();
            string sabotaged = RemoveRow(json, "mono_enabled");
            Assert.That(sabotaged, Is.Not.EqualTo(json), "前置:破坏必须生效(否则本测无判别力)");

            // Act
            BakeValidationException ex = Assert.Throws<BakeValidationException>(
                () => AudioSettingsRegistryBake.BindForTest(sabotaged));

            // Assert:须由**名字对账**分支点名 —— ⚠️ 只查「mono_enabled」会被**值对账**的报文
            // 满足(`运行期回退行「mono_enabled」在烘焙数据中不存在`),从而打不到名字对账本身
            // (2026-10-16 证伪轮例 ⑦ 抓到:停用缺员分支后本测仍绿 ⇒ 弱判别面)。
            Assert.That(AnyError(ex.Errors, "名字对账", "mono_enabled"), Is.True,
                "缺闭枚举成员 ⇒ **名字对账**分支必须点名「mono_enabled」:\n" + string.Join("\n", ex.Errors));
        }

        /// <summary>负向:烘焙注册表含闭枚举**外**条目 ⇒ 构建期红(越集)。</summary>
        [Test]
        public void test_bakedRegistry_extraParam_rejected()
        {
            // Arrange:插入一行闭枚举外条目(挂在 rows 尾)
            string json = ReadRealRegistryJson();
            string sabotaged = json.Replace("\n  ]\n}",
                ",\n    {\n      \"param\": \"bus_volume_bogus\",\n      \"type\": \"float\",\n" +
                "      \"min\": 0.0,\n      \"max\": 1.0,\n      \"factory_default\": 1.0,\n" +
                "      \"player_group\": \"bus\",\n      \"label_source\": \"x\"\n    }\n  ]\n}");
            Assert.That(sabotaged, Is.Not.EqualTo(json), "前置:破坏必须生效");

            // Act
            BakeValidationException ex = Assert.Throws<BakeValidationException>(
                () => AudioSettingsRegistryBake.BindForTest(sabotaged));

            // Assert
            Assert.That(AnyError(ex.Errors, "bus_volume_bogus"), Is.True,
                "闭枚举外条目 ⇒ 名字对账必须点名「bus_volume_bogus」:\n" + string.Join("\n", ex.Errors));
        }

        /// <summary>负向:烘焙默认值与运行期回退**不一致** ⇒ 构建期红(值对账)。
        /// <para>这条是「回退静默漂移」不可能发生的保证 —— 改一处不改另一处即红。</para></summary>
        [Test]
        public void test_bakedRegistry_defaultMismatchWithFallback_rejected()
        {
            // Arrange:把 master 出厂默认改成 0.5(回退仍 1.0)
            string json = ReadRealRegistryJson();
            string sabotaged = ReplaceFirstDefault(json, "bus_volume_master", "1.0", "0.5");
            Assert.That(sabotaged, Is.Not.EqualTo(json), "前置:破坏必须生效");

            // Act
            BakeValidationException ex = Assert.Throws<BakeValidationException>(
                () => AudioSettingsRegistryBake.BindForTest(sabotaged));

            // Assert
            Assert.That(AnyError(ex.Errors, "值对账"), Is.True,
                "烘焙默认 ≠ 运行期回退 ⇒ 值对账必须报:\n" + string.Join("\n", ex.Errors));
        }

        /// <summary>负向:行内未知键 ⇒ 构建期红(ADR-014 §三 未知键硬失败)。</summary>
        [Test]
        public void test_bakedRegistry_unknownRowKey_rejected()
        {
            // Arrange
            string json = ReadRealRegistryJson();
            string sabotaged = json.Replace("\"player_group\": \"bus\",",
                "\"player_group\": \"bus\",\n      \"typo_key\": 1,");
            Assert.That(sabotaged, Is.Not.EqualTo(json), "前置:破坏必须生效");

            // Act
            BakeValidationException ex = Assert.Throws<BakeValidationException>(
                () => AudioSettingsRegistryBake.BindForTest(sabotaged));

            // Assert
            Assert.That(AnyError(ex.Errors, "typo_key"), Is.True,
                "未知键 ⇒ 白名单硬失败(拼写错会静默丢字段):\n" + string.Join("\n", ex.Errors));
        }

        /// <summary>负向:bool 行带 <c>min</c>/<c>max</c> ⇒ 构建期红(域检查;GDD :908 mono 行无区间)。</summary>
        [Test]
        public void test_bakedRegistry_boolRowWithRange_rejected()
        {
            // Arrange:给 mono 行插 min/max
            string json = ReadRealRegistryJson();
            string sabotaged = json.Replace("\"param\": \"mono_enabled\",",
                "\"param\": \"mono_enabled\",\n      \"min\": 0.0,\n      \"max\": 1.0,");
            Assert.That(sabotaged, Is.Not.EqualTo(json), "前置:破坏必须生效");

            // Act
            BakeValidationException ex = Assert.Throws<BakeValidationException>(
                () => AudioSettingsRegistryBake.BindForTest(sabotaged));

            // Assert
            Assert.That(AnyError(ex.Errors, "mono_enabled"), Is.True,
                "bool 行不得带 min/max:\n" + string.Join("\n", ex.Errors));
        }

        /// <summary>负向:float 行的 <c>factory_default</c> 越出 <c>[min, max]</c> ⇒ 构建期红
        /// (阈值边界守卫;GDD :896-909 声明域 0–1)。</summary>
        [Test]
        public void test_bakedRegistry_defaultOutOfRange_rejected()
        {
            // Arrange:master 出厂默认改成 1.5(越出 [0,1])
            string json = ReadRealRegistryJson();
            string sabotaged = ReplaceFirstDefault(json, "bus_volume_master", "1.0", "1.5");
            Assert.That(sabotaged, Is.Not.EqualTo(json), "前置:破坏必须生效");

            // Act
            BakeValidationException ex = Assert.Throws<BakeValidationException>(
                () => AudioSettingsRegistryBake.BindForTest(sabotaged));

            // Assert
            Assert.That(AnyError(ex.Errors, "越出区间"), Is.True,
                "出厂默认越出 [min,max] ⇒ 必须报:\n" + string.Join("\n", ex.Errors));
        }

        /// <summary>负向:<c>type</c> ∉ {float, bool} ⇒ 构建期红(类型闭集)。</summary>
        [Test]
        public void test_bakedRegistry_unknownType_rejected()
        {
            // Arrange
            string json = ReadRealRegistryJson();
            string sabotaged = json.Replace("\"param\": \"mono_enabled\",\n      \"type\": \"bool\",",
                "\"param\": \"mono_enabled\",\n      \"type\": \"int\",");
            Assert.That(sabotaged, Is.Not.EqualTo(json), "前置:破坏必须生效");

            // Act
            BakeValidationException ex = Assert.Throws<BakeValidationException>(
                () => AudioSettingsRegistryBake.BindForTest(sabotaged));

            // Assert
            Assert.That(AnyError(ex.Errors, "闭集"), Is.True,
                "type ∉ {float, bool} ⇒ 必须报闭集违例:\n" + string.Join("\n", ex.Errors));
        }

        /// <summary>负向:缺 <c>factory_default</c> ⇒ 构建期红(AC-44-13 的字段级守卫 ——
        /// 没有这一条,「出厂默认入烘焙分区」可被「字段整个不写」绕过)。</summary>
        [Test]
        public void test_bakedRegistry_missingFactoryDefault_rejected()
        {
            // Arrange:删掉 master 行的 factory_default
            string json = ReadRealRegistryJson();
            string sabotaged = json.Replace(
                "\"max\": 1.0,\n      \"factory_default\": 1.0,\n      \"player_group\": \"bus\",",
                "\"max\": 1.0,\n      \"player_group\": \"bus\",");
            Assert.That(sabotaged, Is.Not.EqualTo(json), "前置:破坏必须生效");

            // Act
            BakeValidationException ex = Assert.Throws<BakeValidationException>(
                () => AudioSettingsRegistryBake.BindForTest(sabotaged));

            // Assert
            Assert.That(AnyError(ex.Errors, "factory_default"), Is.True,
                "缺 factory_default ⇒ AC-44-13 的字段级守卫必须报:\n" + string.Join("\n", ex.Errors));
        }

        // ══════════════ AC-44-13:mono 出厂默认 ∈ 烘焙分区(断言已恢复)══════════════

        /// <summary>AC-44-13:mono 选项存在,且其**出厂默认来自烘焙分区**(而非代码常量)。
        /// <para>⚠️ 本条即 S13-9 评审点名「被删空」的那条断言 —— 此前只剩注释块
        /// (修复轮 B2 删断言留注释,注释还自称「非假绿」)。现恢复为**真断言**。</para></summary>
        [Test]
        public void test_monoOption_factoryDefault_fromBakedPartition()
        {
            // Arrange:从**烘焙分区**取行(不是 AudioSettingsRegistry.FallbackRows)
            AudioSettingsRegistryBinder.BindResult bound = AudioSettingsRegistryBake.BindFromRepo(repoRoot());

            // Act
            bool found = TryGetRow(bound.Rows, MixerRegistry.MonoParameter, out SettingsRegistryRow mono);

            // Assert:① 存在 ② 类型 = Bool ③ 出厂默认 = false(关)
            Assert.That(found, Is.True,
                $"烘焙分区必须含「{MixerRegistry.MonoParameter}」行(AC-44-13:出厂默认入烘焙分区)");
            Assert.That(mono.Kind, Is.EqualTo(SettingsValueKind.Bool),
                "mono 行类型 = bool(GDD :908)");
            Assert.That(mono.FactoryDefault.Bool, Is.False,
                "mono 出厂默认 = false(关)");
        }

        /// <summary>AC-44-13 交叉面:7 路总线音量的**烘焙默认**经
        /// <see cref="VolumeUnitMapping"/> 换算后 == <see cref="PlayerBusVolumeInitializer.FactoryDefaults"/>
        /// 的 dB 值(混音器侧与烘焙分区**同一真源**,非两处各写一份数值)。</summary>
        [Test]
        public void test_busVolumeFactoryDefaults_matchBakedPartition()
        {
            // Arrange
            AudioSettingsRegistryBinder.BindResult bound = AudioSettingsRegistryBake.BindFromRepo(repoRoot());
            IReadOnlyList<KeyValuePair<string, float>> mixerDefaults = PlayerBusVolumeInitializer.FactoryDefaults;

            // Act + Assert:7 路逐一比对
            Assert.That(mixerDefaults.Count, Is.EqualTo(7), "混音器侧出厂默认须恰 7 路");
            for (int i = 0; i < mixerDefaults.Count; i++)
            {
                Assert.That(TryGetRow(bound.Rows, mixerDefaults[i].Key, out SettingsRegistryRow row), Is.True,
                    $"烘焙分区缺「{mixerDefaults[i].Key}」行");
                double expectedDb = VolumeUnitMapping.LinearToDb(row.FactoryDefault.Float);
                Assert.That(mixerDefaults[i].Value, Is.EqualTo((float)expectedDb),
                    $"「{mixerDefaults[i].Key}」混音器默认须 = 烘焙线性默认经换算的 dB" +
                    $"(线性 {row.FactoryDefault.Float} ⇒ {expectedDb} dB)");
                // 判别力自证:混音器侧**不得**是玩家面线性值的直通(种子默认 1.0 vs 0 dB 恰好不等)
                Assert.That(mixerDefaults[i].Value, Is.Not.EqualTo((float)row.FactoryDefault.Float),
                    $"「{mixerDefaults[i].Key}」混音器默认**不得**等于玩家面线性值 " +
                    $"{row.FactoryDefault.Float} —— 单位换算必须发生(VolumeUnitMapping 存在之由)");
            }
        }

        // ══════════════ 单位换算(玩家线性域 ↔ 混音器 dB 域)══════════════

        /// <summary>换算边界:线性 1.0 ⇒ 0 dB(中性,不放大);线性 0 ⇒ 钳到下界 −80(非 −Inf)。</summary>
        [Test]
        public void test_volumeUnitMapping_neutralAndFloor()
        {
            // Arrange + Act + Assert
            Assert.That(VolumeUnitMapping.LinearToDb(1.0), Is.EqualTo(0.0),
                "线性 1.0 = 0 dB(玩家滑杆不是增益级)");
            Assert.That(VolumeUnitMapping.LinearToDb(0.0), Is.EqualTo(VolumeUnitMapping.MinDb),
                "线性 0 须钳到 −80(不返回 −Inf:−Inf 不可落盘 / 不可比对)");
            Assert.That(VolumeUnitMapping.LinearToDb(2.0), Is.EqualTo(0.0),
                "线性 ≥ 1 一律钳到 0 dB(不放大)");
        }

        /// <summary>两域混用是**静默**失败:玩家滑杆的 0.5 若被当 dB 直推 mixer,读作 −0.5 dB ≈ 满音量。
        /// 本条钉住「换算确实发生了」。</summary>
        [Test]
        public void test_volumeUnitMapping_linearValue_notPassedThroughAsDb()
        {
            // Arrange
            const double linear = 0.5;

            // Act
            double db = VolumeUnitMapping.LinearToDb(linear);

            // Assert:约 −6.02 dB(≈ 半振幅),**不是** −0.5
            Assert.That(db, Is.Not.EqualTo(-0.5).Within(1e-9),
                "线性 0.5 不得原样当 dB 推 mixer(否则玩家以为推到一半,实际近乎满音量)");
            Assert.That(db, Is.EqualTo(-6.0206).Within(0.001),
                "线性 0.5 ⇒ 20·log10(0.5) ≈ −6.02 dB");
            Assert.That(VolumeUnitMapping.DbToLinear(db), Is.EqualTo(linear).Within(1e-9),
                "往返一致(换算单点的对称半)");
        }

        // ══════════════ AC-44-15 [A]:字幕键集覆盖 ══════════════

        /// <summary>AC-44-15 [A]:真种子里语声 / 口述族 cue 全部带非空 <c>subtitle_text</c>。
        /// <para>判据 = **生产门** <see cref="AudioEventTableGates.ValidateSubtitles"/>
        /// (族判定 = <see cref="AudioEventTableGates.IsSubtitleCue"/>);测试只做抽取。</para></summary>
        [Test]
        public void test_subtitleKeySet_realSeed_passesProductionGate()
        {
            // Arrange:读真种子事件表 → 抽取行
            string path = Path.Combine(repoRoot(), "assets", "data", "audio_events.json");
            Assert.That(File.Exists(path), Is.True, $"真种子缺失:{path}");
            IReadOnlyList<AudioEventTableRow> rows = ExtractRows(File.ReadAllText(path));

            // Act:生产门
            IReadOnlyList<string> errors = AudioEventTableGates.ValidateSubtitles(rows);

            // Assert:① 抽取面非空(否则本测空转)② 生产门零错误
            Assert.That(CountSubtitleCues(rows), Is.GreaterThan(0),
                "前置:真种子里必须至少有一条语声 / 口述族 cue(否则键集覆盖空转)");
            Assert.That(errors, Is.Empty,
                "assets/data/audio_events.json 的语声族 cue 须全部带 subtitle_text:\n" +
                string.Join("\n", errors));
        }

        /// <summary>AC-44-15 [A] 负向:缺 <c>subtitle_text</c> 的语声 cue ⇒ 生产门红
        /// (夹具 = <c>invalid_subtitle_missing.json</c>,与 Story 002 共用)。</summary>
        [Test]
        public void test_subtitleKeySet_missingSubtitle_rejected()
        {
            // Arrange:仓库夹具
            string path = Path.Combine(repoRoot(), "tests", "unit", "audio_system",
                "fixtures", "invalid_subtitle_missing.json");
            Assert.That(File.Exists(path), Is.True, $"负向夹具缺失:{path}");
            IReadOnlyList<AudioEventTableRow> rows = ExtractRows(File.ReadAllText(path));

            // Act
            IReadOnlyList<string> errors = AudioEventTableGates.ValidateSubtitles(rows);

            // Assert:必须点名 subtitle_text(且抽取面非空 —— 否则「零错误」是空转)
            Assert.That(CountSubtitleCues(rows), Is.GreaterThan(0),
                "前置:夹具必须含语声族 cue(否则本测无判别力)");
            Assert.That(errors, Is.Not.Empty,
                "缺 subtitle_text 的语声 cue 必须判失败(AC-44-15:缺失 = 构建失败)");
            Assert.That(AnyError(errors, "subtitle_text"), Is.True,
                "错误须点名 subtitle_text:\n" + string.Join("\n", errors));
        }

        /// <summary>AC-44-15 [A] 的「含 48 口述」半边 —— **已落地**(2026-10-21 C-1 改判)。
        /// <para><b>原判定(2026-10-16 → 2026-10-21 前)</b>:本条是**缺口哨兵**,钉住
        /// 「48 的 4 支教学口述既不在事件表、前缀也不匹配
        /// <see cref="AudioEventTableGates.SubtitleCuePrefixes"/>(<c>Voice_</c>/<c>Narration_</c>)
        /// ⇒ 生产门看不见它们」这一实测事实;当时断言两条 `Is.False`。</para>
        /// <para><b>为何改判</b>:C-1 把 4 条口述 cue 落进事件表,并按用户裁定
        /// (2026-10-21)改名为 <c>Narration_Teach*_01</c> —— 前缀落进 <c>Narration_</c> 族
        /// ⇒ `IsSubtitleCue()` 转真,门的字幕覆盖断言**首次真正覆盖到 48 口述**。
        /// 承本测原注:「48 侧契约落地后本条会转红 —— 届时须随之改判,**而不是把红当回归修掉**」。
        /// 这正是那次改判。</para>
        /// <para>⚠️ 判据未放宽:两条断言由 `Is.False` 翻成 `Is.True`,**同一条覆盖命题的两端**
        /// (落地前「扫不到」/ 落地后「扫得到且真在表里」)。不是删断言、不是换夹具。</para></summary>
        [Test]
        public void test_tutorialNarrationCues_areInEventTable_andAreSubtitleCues()
        {
            // Arrange:48 的教学口述 cueId(读真种子,非硬编码 —— 48 改内容时本测随之动)
            string tutorialPath = Path.Combine(repoRoot(), "assets", "data", "48_tutorial_content.json");
            Assert.That(File.Exists(tutorialPath), Is.True, $"48 教学种子缺失:{tutorialPath}");
            IReadOnlyList<string> narrationCueIds = ExtractNarrationCueIds(File.ReadAllText(tutorialPath));
            Assert.That(narrationCueIds.Count, Is.GreaterThan(0),
                "前置:48 教学种子须有 narration[].cueId(否则本条无判别力)");

            // Arrange:事件表 cue 集
            string eventsPath = Path.Combine(repoRoot(), "assets", "data", "audio_events.json");
            IReadOnlyList<AudioEventTableRow> rows = ExtractRows(File.ReadAllText(eventsPath));

            // Act + Assert:① 48 口述 cue 全部在事件表 ② 全部被生产门判为语声族(⇒ 字幕断言覆盖到)
            for (int i = 0; i < narrationCueIds.Count; i++)
            {
                string cueId = narrationCueIds[i];
                Assert.That(ContainsCue(rows, cueId), Is.True,
                    $"AC-44-15 [A]:48 口述 cue「{cueId}」须在事件表内(否则字幕文本无出处)");
                Assert.That(AudioEventTableGates.IsSubtitleCue(cueId), Is.True,
                    $"AC-44-15 [A]:48 口述 cue「{cueId}」须匹配字幕族前缀 —— " +
                    "否则生产门的「表键集 ⊇ 语声 cue 集」对本条恒真(覆盖不到 = 义务悬空)");
            }
        }

        // ══════════════ AC-44-19:归零机制(真 store + 真 sidecar)══════════════

        /// <summary>AC-44-19 ①:归零三步(写默认 → 推 mixer → 落 sidecar)后 store == 出厂集,
        /// 且 sidecar 两文件已落盘。</summary>
        [Test]
        public void test_resetMechanism_threeStepsReachFactory()
        {
            // Arrange:被改坏的 store 值 + 注入推 mixer 缝
            var store = new AudioSettingsStore(_tempDir);
            var pushed = new List<KeyValuePair<string, double>>();
            store.MixerPushSink = dbValues =>
            {
                pushed.AddRange(dbValues);
                return Array.Empty<string>();
            };
            store.Set("bus_volume_master", SettingsValue.OfFloat(0.25));
            store.Set(MixerRegistry.MonoParameter, SettingsValue.OfBool(true));
            Assert.That(store.EqualsFactoryDefaults(), Is.False, "前置:store 必须已被改坏");

            // Act
            ResetOutcome outcome = store.ResetToFactory();

            // Assert:① 三步全成 ② store == 出厂集 ③ 第二步真的推了(7 路,mono 非 mixer 参数不计)
            Assert.That(outcome.Succeeded, Is.True,
                "归零三步须全部成功:\n" + string.Join("\n", outcome.Errors));
            Assert.That(store.EqualsFactoryDefaults(), Is.True, "归零后 store == 出厂集");
            Assert.That(pushed.Count, Is.EqualTo(7),
                "第二步须推 7 路总线音量(mono 非 mixer 参数,不入推列表)");
            Assert.That(File.Exists(Path.Combine(_tempDir, AudioSettingsSidecar.PayloadFileName)), Is.True,
                "归零第三步须落 sidecar 载荷");
            Assert.That(File.Exists(Path.Combine(_tempDir, AudioSettingsSidecar.SchemaFileName)), Is.True,
                "归零第三步须落 sidecar 头部");
        }

        /// <summary>AC-44-19 ②:重启后仍 == 出厂集(新 store 从 sidecar 载入)。</summary>
        [Test]
        public void test_resetMechanism_persistsAfterRestart()
        {
            // Arrange:归零后的 store(sidecar 已写)
            var store = new AudioSettingsStore(_tempDir);
            store.MixerPushSink = _ => Array.Empty<string>();
            store.Set("bus_volume_music", SettingsValue.OfFloat(0.1));
            ResetOutcome outcome = store.ResetToFactory();
            Assert.That(outcome.Succeeded, Is.True, "前置:归零须成功");

            // Act:模拟重启(新 store 从 sidecar 载入)
            var restarted = new AudioSettingsStore(_tempDir);
            SettingsLoadResult result = restarted.LoadFromSidecar();

            // Assert
            Assert.That(result, Is.EqualTo(SettingsLoadResult.Applied),
                "重启后须从 sidecar 成功载入(归零已落盘)");
            Assert.That(restarted.EqualsFactoryDefaults(), Is.True,
                "重启后仍 == 出厂集(AC-44-19 ②)");
        }

        /// <summary>AC-44-19 负向:**跳过第三步(sidecar 落盘)⇒ 归源无效**。
        /// <para>证法 = 直接演示失败模式,而非只断言一个布尔:预置一个含**非出厂值**的 sidecar,
        /// 只做第一步(写默认)⇒ 内存虽归零,但重启后被 sidecar 的坏值推回。</para></summary>
        [Test]
        public void test_resetMechanism_skipSidecar_rejected()
        {
            // Arrange:预置「上次会话玩家改过音量」的 sidecar(非出厂值)
            var bad = new Dictionary<string, SettingsValue>(StringComparer.Ordinal);
            for (int i = 0; i < AudioSettingsRegistry.ParamNames.Count; i++)
            {
                string param = AudioSettingsRegistry.ParamNames[i];
                Assert.That(AudioSettingsRegistry.TryGetRow(param, out SettingsRegistryRow row), Is.True);
                bad[param] = string.Equals(param, "bus_volume_master", StringComparison.Ordinal)
                    ? SettingsValue.OfFloat(0.25)      // 非出厂值
                    : row.FactoryDefault;
            }
            var sidecar = new AudioSettingsSidecar(_tempDir);
            Assert.That(sidecar.Save(bad, AudioSettingsStore.RegistrySignature()), Is.True,
                "前置:预置 sidecar 须写成功");

            // Act:只做第一步(写默认)—— **跳过**第三步落盘
            var store = new AudioSettingsStore(_tempDir);
            Assert.That(store.LoadFromSidecar(), Is.EqualTo(SettingsLoadResult.Applied), "前置:坏值已载入");
            store.LoadFactoryDefaults();   // 第一步

            // Assert:内存归零了……
            Assert.That(store.EqualsFactoryDefaults(), Is.True, "第一步后内存态 == 出厂集");

            // ……但重启后坏值被 sidecar 推回 ⇒ 归源**无效**
            var restarted = new AudioSettingsStore(_tempDir);
            Assert.That(restarted.LoadFromSidecar(), Is.EqualTo(SettingsLoadResult.Applied),
                "跳过第三步 ⇒ sidecar 仍是旧的坏值,重启后照常载入");
            Assert.That(restarted.EqualsFactoryDefaults(), Is.False,
                "跳过 sidecar 落盘 ⇒ 重启后值 ≠ 出厂集(AC-44-19:归零三步缺一无效)");
            Assert.That(restarted.TryGet("bus_volume_master", out SettingsValue v), Is.True);
            Assert.That(v.Float, Is.EqualTo(0.25),
                "被推回的具体值 = sidecar 里的坏值 0.25");
        }

        /// <summary>AC-44-19 负向:第二步(推 mixer)不可执行 ⇒ 归零**判失败**(不以「跳过」冒充成功)。</summary>
        [Test]
        public void test_resetMechanism_missingPushSink_fails()
        {
            // Arrange:store 未注入推 mixer 缝(story-010 装载面接线前 = 常态)
            var store = new AudioSettingsStore(_tempDir);
            store.Set("bus_volume_sfx", SettingsValue.OfFloat(0.3));

            // Act
            ResetOutcome outcome = store.ResetToFactory();

            // Assert
            Assert.That(outcome.Succeeded, Is.False,
                "推 mixer 缝缺失 ⇒ 归零三步缺一 ⇒ 必须判失败");
            Assert.That(AnyError(outcome.Errors, "第二步"), Is.True,
                "错误须点名第二步:\n" + string.Join("\n", outcome.Errors));
        }

        /// <summary>AC-44-19 负向:第三步(sidecar 落盘)失败 ⇒ 归零**判失败**
        /// (写点不可写:目录路径被一个同名**文件**占住)。</summary>
        [Test]
        public void test_resetMechanism_sidecarWriteFailure_fails()
        {
            // Arrange:把「目录」位置占成一个文件 ⇒ Directory.CreateDirectory 必失败
            string blocked = Path.Combine(_tempDir, "blocked");
            Directory.CreateDirectory(_tempDir);
            File.WriteAllText(blocked, "occupied");
            var store = new AudioSettingsStore(blocked);
            store.MixerPushSink = _ => Array.Empty<string>();

            // Act
            ResetOutcome outcome = store.ResetToFactory();

            // Assert
            Assert.That(outcome.Succeeded, Is.False,
                "sidecar 落盘失败 ⇒ 归零三步缺一 ⇒ 必须判失败(内存态已归零但重启会被坏值推回)");
            Assert.That(AnyError(outcome.Errors, "第三步"), Is.True,
                "错误须点名第三步:\n" + string.Join("\n", outcome.Errors));
        }

        /// <summary>推 mixer 的**单位**:store 送出的必须是 dB(线性经换算),不是玩家面线性值。</summary>
        [Test]
        public void test_resetMechanism_pushSinkReceivesDbValues()
        {
            // Arrange
            var store = new AudioSettingsStore(_tempDir);
            IReadOnlyList<KeyValuePair<string, double>> captured = null;
            store.MixerPushSink = dbValues => { captured = dbValues; return Array.Empty<string>(); };

            // Act
            store.ResetToFactory();

            // Assert:出厂线性 1.0 ⇒ 0 dB;且 mono 不在列表内
            Assert.That(captured, Is.Not.Null, "第二步须真的被调用");
            Assert.That(captured.Count, Is.EqualTo(7), "7 路总线音量");
            for (int i = 0; i < captured.Count; i++)
            {
                Assert.That(captured[i].Key, Is.Not.EqualTo(MixerRegistry.MonoParameter),
                    "mono 非 mixer exposed 参数,不得进推列表");
                Assert.That(captured[i].Value, Is.EqualTo(0.0),
                    $"「{captured[i].Key}」出厂线性 1.0 ⇒ 推 mixer 的 dB = 0");
            }
        }

        // ══════════════ sidecar 读序(承 3 规则四)══════════════

        /// <summary>签名失配 ⇒ 不采用载荷、陈旧件改名备份(<c>.bak-001</c>)、维持出厂默认。</summary>
        [Test]
        public void test_sidecar_signatureMismatch_backsUpAndKeepsDefaults()
        {
            // Arrange:用**错误签名**写一份 sidecar
            var values = new Dictionary<string, SettingsValue>(StringComparer.Ordinal);
            for (int i = 0; i < AudioSettingsRegistry.ParamNames.Count; i++)
                values[AudioSettingsRegistry.ParamNames[i]] = AudioSettingsRegistry.Rows[i].FactoryDefault;
            values["bus_volume_master"] = SettingsValue.OfFloat(0.25);
            var sidecar = new AudioSettingsSidecar(_tempDir);
            Assert.That(sidecar.Save(values, "deadbeef"), Is.True, "前置:陈旧 sidecar 须写成功");

            // Act
            SettingsLoadResult result = sidecar.Load(AudioSettingsStore.RegistrySignature(), out var loaded);

            // Assert
            Assert.That(result, Is.EqualTo(SettingsLoadResult.Mismatch),
                "签名不符 ⇒ 判失配(不采用载荷)");
            Assert.That(loaded, Is.Empty, "失配出口不得交出任何值(不部分恢复)");
            Assert.That(File.Exists(sidecar.PayloadPath + ".bak-001"), Is.True,
                "陈旧载荷须改名备份为 .bak-001");
            Assert.That(File.Exists(sidecar.SchemaPath + ".bak-001"), Is.True,
                "陈旧头部须改名备份为 .bak-001");

            // 半截/失配后的 store 维持出厂默认
            var store = new AudioSettingsStore(_tempDir);
            Assert.That(store.LoadFromSidecar(), Is.EqualTo(SettingsLoadResult.NoSidecar),
                "备份后已无 sidecar ⇒ 首次启动路径(出厂默认即所载)");
            Assert.That(store.EqualsFactoryDefaults(), Is.True, "失配后维持出厂默认");
        }

        /// <summary>半截载荷(键集缺员)⇒ 判失配,不部分恢复(半截会让设备偏好静默错位)。</summary>
        [Test]
        public void test_sidecar_truncatedPayload_rejected()
        {
            // Arrange:写一份**完整**sidecar,再把载荷删掉最后一行(模拟半截写盘)
            var values = new Dictionary<string, SettingsValue>(StringComparer.Ordinal);
            for (int i = 0; i < AudioSettingsRegistry.ParamNames.Count; i++)
            {
                Assert.That(AudioSettingsRegistry.TryGetRow(AudioSettingsRegistry.ParamNames[i],
                    out SettingsRegistryRow row), Is.True);
                values[AudioSettingsRegistry.ParamNames[i]] = row.FactoryDefault;
            }
            var sidecar = new AudioSettingsSidecar(_tempDir);
            Assert.That(sidecar.Save(values, AudioSettingsStore.RegistrySignature()), Is.True,
                "前置:sidecar 须写成功");

            string[] lines = File.ReadAllLines(sidecar.PayloadPath);
            Assert.That(lines.Length, Is.EqualTo(AudioSettingsRegistry.ParamNames.Count),
                "前置:载荷行数 = 参数数");
            // 真删一行:截断到 N−1 行(模拟半截写盘)
            var truncated = new List<string>(lines);
            truncated.RemoveAt(truncated.Count - 1);
            File.WriteAllText(sidecar.PayloadPath, string.Join("\n", truncated) + "\n",
                new System.Text.UTF8Encoding(false));

            // Act:经 store 的载入路径(键集完整性检查在此)
            var store = new AudioSettingsStore(_tempDir);
            SettingsLoadResult result = store.LoadFromSidecar();

            // Assert:缺键 ⇒ 判失配,维持出厂默认(不部分恢复)
            Assert.That(result, Is.EqualTo(SettingsLoadResult.Mismatch),
                "载荷缺员 ⇒ 须判失配(半截 sidecar 不得被当作有效偏好)");
            Assert.That(store.EqualsFactoryDefaults(), Is.True,
                "失配后维持出厂默认");
        }

        /// <summary>签名**不含**出厂默认值:改默认值不该让玩家的设备偏好失效(改默认 ≠ 改结构)。</summary>
        [Test]
        public void test_sidecar_signature_excludesFactoryDefaults()
        {
            // Arrange:同一参数名序列 ⇒ 同一签名(签名输入只有名字)
            string a = AudioSettingsSidecar.ComputeRegistrySignature(
                new[] { "bus_volume_master", MixerRegistry.MonoParameter });
            string b = AudioSettingsSidecar.ComputeRegistrySignature(
                new[] { MixerRegistry.MonoParameter, "bus_volume_master" });

            // Act + Assert:序无关(内部排序)
            Assert.That(a, Is.EqualTo(b), "签名对参数序不敏感(内部按序数排序)");
            Assert.That(a, Is.Not.Empty, "签名非空");
        }

        /// <summary>签名**对参数集敏感** —— 这是签名存在的全部理由:参数集变了 ⇒ 签名必须变
        /// (否则旧 sidecar 会被当作有效偏好载入,把已删参数的值静默错位)。
        /// <para>⚠️ 2026-10-16 证伪轮例 ① 抓到:此前**全库无此断言** ——
        /// `test_sidecar_signatureMismatch_*` 用的是**字面量** `"deadbeef"` 作错签名,
        /// 把派生改成「恒返常量」结论不变 ⇒ 该测对派生本体零判别力。</para></summary>
        [Test]
        public void test_sidecar_signature_isSensitiveToParamSet()
        {
            // Arrange:两个**不同**参数集(同名序列只在顺序上不同,故取真子集 + 增员)
            IReadOnlyList<string> full = AudioSettingsRegistry.ParamNames;
            Assert.That(full.Count, Is.GreaterThan(1), "前置:参数集须多于 1 员(否则本测无判别力)");

            var missingOne = new List<string>(full);
            missingOne.RemoveAt(missingOne.Count - 1);          // 缺员
            var plusExtra = new List<string>(full) { "bus_volume_not_registered" };   // 多员

            // Act
            string sigFull = AudioSettingsSidecar.ComputeRegistrySignature(full);
            string sigMissing = AudioSettingsSidecar.ComputeRegistrySignature(missingOne);
            string sigExtra = AudioSettingsSidecar.ComputeRegistrySignature(plusExtra);

            // Assert:参数集一变,签名必须跟着变(缺员 / 多员两个方向都查)
            Assert.That(sigMissing, Is.Not.EqualTo(sigFull),
                "参数集**缺员** ⇒ 签名必须不同(否则旧 sidecar 会被误判为有效)");
            Assert.That(sigExtra, Is.Not.EqualTo(sigFull),
                "参数集**多员** ⇒ 签名必须不同(否则结构漂移被静默吸收)");
        }

        // ══════════════ AC-44-12:文档判据 ══════════════

        /// <summary>AC-44-12 [L]:三条具名项在位 —— 视觉替代引 AC-13-F1 · P0 非目标 · 三义务 AC 存在。</summary>
        [Test]
        public void test_a11yDocumentation_threeItemsPresent()
        {
            // Arrange:读 GDD
            string path = Path.Combine(repoRoot(), "design", "gdd", "audio-system.md");
            Assert.That(File.Exists(path), Is.True, $"GDD 缺失:{path}");
            string text = File.ReadAllText(path);

            // Act + Assert:三条具名项在位
            Assert.That(text.Contains("AC-13-F1"), Is.True,
                "视觉替代必须引 AC-13-F1(AC-44-12 三条具名项之一)");
            Assert.That(text.Contains("隔墙听觉"), Is.True,
                "P0 非目标「隔墙听觉无视觉等价」必须存在(AC-44-12 三条具名项之一)");
            Assert.That(text.Contains("mono") && text.Contains("总线音量") && text.Contains("字幕"), Is.True,
                "三义务(mono/总线音量/语声字幕)AC 必须存在(AC-44-12 三条具名项之一)");
        }

        // ══════════════ 抽取小件(测试侧唯一的手写面;判据全在生产)══════════════

        private static string ReadRealRegistryJson()
        {
            string path = Path.Combine(repoRoot(), "assets", "data", "audio_settings_registry.json");
            Assert.That(File.Exists(path), Is.True, $"真种子缺失:{path}");
            return File.ReadAllText(path);
        }

        private static bool ContainsRow(IReadOnlyList<SettingsRegistryRow> rows, string param)
            => TryGetRow(rows, param, out _);

        private static bool TryGetRow(IReadOnlyList<SettingsRegistryRow> rows, string param,
                                      out SettingsRegistryRow row)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (string.Equals(rows[i].Param, param, StringComparison.Ordinal))
                {
                    row = rows[i];
                    return true;
                }
            }
            row = default;
            return false;
        }

        private static bool ContainsCue(IReadOnlyList<AudioEventTableRow> rows, string cue)
        {
            for (int i = 0; i < rows.Count; i++)
                if (string.Equals(rows[i].Cue, cue, StringComparison.Ordinal)) return true;
            return false;
        }

        private static int CountSubtitleCues(IReadOnlyList<AudioEventTableRow> rows)
        {
            int n = 0;
            for (int i = 0; i < rows.Count; i++)
                if (AudioEventTableGates.IsSubtitleCue(rows[i].Cue)) n++;
            return n;
        }

        /// <summary>错误集里是否含指定片段(不依赖执行次序 —— 门按规则聚合,顺序不作契约)。</summary>
        private static bool AnyError(IReadOnlyList<string> errors, string fragment)
        {
            for (int i = 0; i < errors.Count; i++)
            {
                if (errors[i] != null && errors[i].IndexOf(fragment, StringComparison.Ordinal) >= 0)
                    return true;
            }
            return false;
        }

        /// <summary>两段式匹配(同一条报文须**同时**含两片段)—— 用于把断言钉在**特定分支**上:
        /// 单片段会被**另一分支**的同名报文满足 ⇒ 弱判别面(证伪轮例 ⑦)。</summary>
        private static bool AnyError(IReadOnlyList<string> errors, string fragmentA, string fragmentB)
        {
            for (int i = 0; i < errors.Count; i++)
            {
                if (errors[i] == null) continue;
                if (errors[i].IndexOf(fragmentA, StringComparison.Ordinal) >= 0
                    && errors[i].IndexOf(fragmentB, StringComparison.Ordinal) >= 0)
                    return true;
            }
            return false;
        }

        // ── 破坏器(负向夹具的构造面;只做文本手术,不做判断)──

        /// <summary>删掉 <c>param == name</c> 的那个行对象(含其前导逗号 / 换行)。</summary>
        private static string RemoveRow(string json, string name)
        {
            int at = json.IndexOf("\"param\": \"" + name + "\"", StringComparison.Ordinal);
            if (at < 0) return json;
            int open = json.LastIndexOf('{', at);
            int close = MatchBracket(json, open, '{', '}');
            if (open < 0 || close < 0) return json;
            int end = close + 1;
            if (end < json.Length && json[end] == ',') end++;           // 吃掉后继逗号
            else
            {
                int back = open - 1;
                while (back >= 0 && char.IsWhiteSpace(json[back])) back--;
                if (back >= 0 && json[back] == ',') open = back;        // 末行:吃掉前导逗号
            }
            return json.Substring(0, open) + json.Substring(end);
        }

        /// <summary>把指定 param 行的 <c>factory_default</c> 由 <paramref name="from"/> 改成
        /// <paramref name="to"/>。</summary>
        private static string ReplaceFirstDefault(string json, string param, string from, string to)
        {
            int at = json.IndexOf("\"param\": \"" + param + "\"", StringComparison.Ordinal);
            if (at < 0) return json;
            int key = json.IndexOf("\"factory_default\": " + from, at, StringComparison.Ordinal);
            if (key < 0) return json;
            return json.Substring(0, key)
                 + "\"factory_default\": " + to
                 + json.Substring(key + ("\"factory_default\": " + from).Length);
        }

        // ── JSON 抽取(手写引号 / 括号配对扫描;玩家构建零解析器纪律的测试侧对应)──

        /// <summary>事件表 <c>rows[]</c> → 绑定前行结构(只取 cue 与 subtitle_text 两字段 ——
        /// 本文件所需的最小面;判据全部交给生产门)。</summary>
        private static IReadOnlyList<AudioEventTableRow> ExtractRows(string json)
        {
            var rows = new List<AudioEventTableRow>();
            int rowsKey = json.IndexOf("\"rows\"", StringComparison.Ordinal);
            if (rowsKey < 0) return rows;
            int open = json.IndexOf('[', rowsKey);
            int close = MatchBracket(json, open, '[', ']');
            if (open < 0 || close < 0) return rows;
            string arrayRaw = json.Substring(open, close - open + 1);

            int i = 0;
            while (i < arrayRaw.Length)
            {
                if (arrayRaw[i] == '{')
                {
                    int end = MatchBracket(arrayRaw, i, '{', '}');
                    if (end < 0) break;
                    string obj = arrayRaw.Substring(i, end - i + 1);
                    rows.Add(new AudioEventTableRow
                    {
                        Cue = ReadStringMember(obj, "cue"),
                        SubtitleText = ReadStringMember(obj, "subtitle_text"),
                    });
                    i = end + 1;
                }
                else i++;
            }
            return rows;
        }

        /// <summary>48 教学种子的**顶层** <c>narration[].cueId</c> 列表。
        /// <para>⚠️ 键名 <c>"narration"</c> 在 48 种子里出现 7 次(<c>steps[].narration</c> 是
        /// **字符串 id 数组**,顶层 <c>narration</c> 才是**对象数组**)。故本抽取器按
        /// 「数组首元素是对象」筛选 —— 取顶层那一个,而非首个字面命中
        /// (首个命中会让本测静默抽空 ⇒ 缺口哨兵退化为空转)。</para></summary>
        private static IReadOnlyList<string> ExtractNarrationCueIds(string json)
        {
            var ids = new List<string>();
            int from = 0;
            while (true)
            {
                int arrKey = json.IndexOf("\"narration\"", from, StringComparison.Ordinal);
                if (arrKey < 0) return ids;
                from = arrKey + 11;

                int open = json.IndexOf('[', arrKey);
                int close = MatchBracket(json, open, '[', ']');
                if (open < 0 || close < 0) continue;

                int probe = open + 1;
                while (probe < close && char.IsWhiteSpace(json[probe])) probe++;
                if (probe >= close || json[probe] != '{') continue;   // 字符串 id 数组 ⇒ 非顶层,继续找

                string arrayRaw = json.Substring(open, close - open + 1);
                int i = 0;
                while (i < arrayRaw.Length)
                {
                    int at = arrayRaw.IndexOf("\"cueId\"", i, StringComparison.Ordinal);
                    if (at < 0) break;
                    string value = ReadStringMember(arrayRaw.Substring(at), "cueId");
                    if (!string.IsNullOrEmpty(value)) ids.Add(value);
                    i = at + 7;
                }
                return ids;
            }
        }

        /// <summary>取对象文本里 <c>"key": "value"</c> 的字符串值;缺失 ⇒ null。</summary>
        private static string ReadStringMember(string obj, string key)
        {
            int at = obj.IndexOf("\"" + key + "\"", StringComparison.Ordinal);
            if (at < 0) return null;
            int colon = obj.IndexOf(':', at + key.Length + 2);
            if (colon < 0) return null;
            int i = colon + 1;
            while (i < obj.Length && char.IsWhiteSpace(obj[i])) i++;
            if (i >= obj.Length || obj[i] != '"') return null;
            int j = i + 1;
            var sb = new System.Text.StringBuilder();
            while (j < obj.Length)
            {
                if (obj[j] == '\\' && j + 1 < obj.Length) { sb.Append(obj[j + 1]); j += 2; continue; }
                if (obj[j] == '"') break;
                sb.Append(obj[j]); j++;
            }
            return sb.ToString();
        }

        /// <summary>从 <paramref name="open"/> 处的开括号找到配对的闭括号下标(跳过字符串内的括号);
        /// 未闭合 ⇒ −1。</summary>
        private static int MatchBracket(string s, int open, char openCh, char closeCh)
        {
            if (open < 0 || open >= s.Length || s[open] != openCh) return -1;
            int depth = 0;
            bool inStr = false;
            for (int i = open; i < s.Length; i++)
            {
                char c = s[i];
                if (inStr)
                {
                    if (c == '\\') { i++; continue; }
                    if (c == '"') inStr = false;
                    continue;
                }
                if (c == '"') { inStr = true; continue; }
                if (c == openCh) depth++;
                else if (c == closeCh)
                {
                    depth--;
                    if (depth == 0) return i;
                }
            }
            return -1;
        }
    }
}
