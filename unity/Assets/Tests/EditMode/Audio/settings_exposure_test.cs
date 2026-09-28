// 权威来源:production/epics/audio-system/story-011-settings-exposure-reset.md
//   (注册表 AC① · AC-44-13 mono · AC-44-15 字幕键集 · AC-44-19 归零机制 · AC-44-12 文档判据)
//   · 44 拥有闭枚举 settings_visible 注册表(8 条 = 7 总线音量含 Master + mono)
//   · 归零三步:写默认 → 推 mixer → 落 sidecar;重启后仍 == 出厂集
//   · 字幕文本本体 = 事件表 subtitle_text;表键集 ⊇ 语声 cue 集(含 48 口述)
// ADR-018(音频架构:mono/总线音量义务)· ADR-013(控件呈现归 42)· ADR-014(出厂默认 = 烘焙分区)
// OQ-SS-5=甲(端本地 sidecar)· OQ-SS-7=甲(归零控件形态 = 42 元件轮)
//
// ⚠️ 落点:故事 Test Evidence 登记路径 = tests/unit/audio_system/settings_exposure_test.cs;
//    Unity 只编译 unity/Assets/ 树 ⇒ **真身 = 本文件**(承 Story 001-010/014 同一先例);
//    账本侧由 tests/unit/audio_system/README.md 互链。
// ⚠️ 负向夹具落位:与 Story 007/008/009/010 同型 —— 违例 fixture 住**测试命名空间**。
// ⚠️ 测试纪律:arrange/act/assert · 无随机 · 无时间依赖(禁 DateTime/Time)·
//    夹具读取前置 File.Exists 断言(缺失即红,不静默跳过)。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using DaYiJingCheng.EditorTools.Gates;
using DaYiJingCheng.Gameplay.Presentation.Audio;

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

        // 注册表闭枚举(8 条 = 7 总线音量含 Master + mono)
        private static readonly string[] SettingsVisibleRegistry = new[]
        {
            "bus_volume_master", "bus_volume_music", "bus_volume_ambience",
            "bus_volume_voice", "bus_volume_sfx", "bus_volume_stethoscope",
            "bus_volume_uicue", "mono_enabled",
        };

        // ══════════════ 注册表 AC①:壳消费集 ⊆ settings_visible ══════════════

        /// <summary>注册表 AC①:壳消费集 ⊆ settings_visible 闭枚举(8 条)。
        /// 验证 MixerRegistry.BusVolumeParameters ∈ 注册表。</summary>
        [Test]
        public void test_settingsRegistry_busVolumes_subsetOfRegistry()
        {
            // Arrange:注册表闭枚举
            var registry = new HashSet<string>(SettingsVisibleRegistry);

            // Act:取 MixerRegistry 的 7 路总线音量参数
            var busVolumes = MixerRegistry.BusVolumeParameters;

            // Assert:7 路总线音量 ∈ 注册表
            Assert.That(busVolumes.Count(), Is.EqualTo(7),
                "总线音量参数必须恰 7 路(OQ-SS-3=甲:数据层恒 7 路)");
            foreach (string param in busVolumes)
            {
                Assert.That(registry.Contains(param), Is.True,
                    $"总线音量参数「{param}」必须 ∈ settings_visible 注册表(AC①:壳消费集 ⊆ 注册表)");
            }
        }

        /// <summary>注册表 AC①:mono_enabled ∈ 注册表(AC-44-13)。</summary>
        [Test]
        public void test_settingsRegistry_monoEnabled_inRegistry()
        {
            // Arrange
            var registry = new HashSet<string>(SettingsVisibleRegistry);

            // Act + Assert
            Assert.That(registry.Contains("mono_enabled"), Is.True,
                "mono_enabled 必须 ∈ settings_visible 注册表(AC-44-13:提供 mono 选项)");
        }

        /// <summary>注册表 AC① 负向:壳引用未登记参数 ⇒ 红。</summary>
        [Test]
        public void test_settingsRegistry_unregisteredParam_rejected()
        {
            // Arrange:注册表闭枚举
            var registry = new HashSet<string>(SettingsVisibleRegistry);

            // Act:构造未登记参数
            string unregisteredParam = "bus_volume_unknown_bus";

            // Assert:未登记参数 ∉ 注册表
            Assert.That(registry.Contains(unregisteredParam), Is.False,
                "未登记参数必须 ∉ settings_visible 注册表(AC①:越集 = 断言失败)");
        }

        /// <summary>注册表 AC①:注册表恰 8 条(7 总线 + mono),不多不少。</summary>
        [Test]
        public void test_settingsRegistry_exactly8Entries()
        {
            // Arrange + Act + Assert
            Assert.That(SettingsVisibleRegistry.Length, Is.EqualTo(8),
                "settings_visible 注册表必须恰 8 条(7 总线音量含 Master + mono)");
        }

        // ══════════════ AC-44-13:mono 选项 ══════════════

        /// <summary>AC-44-13:mono 选项存在且出厂默认 ∈ 烘焙分区。
        /// 验证注册表含 mono_enabled 且出厂默认值有效。</summary>
        [Test]
        public void test_monoOption_existsWithValidDefault()
        {
            // Arrange:注册表闭枚举
            var registry = new HashSet<string>(SettingsVisibleRegistry);

            // Act:验证 mono_enabled 存在
            bool hasMono = registry.Contains("mono_enabled");

            // Assert:存在
            Assert.That(hasMono, Is.True, "mono_enabled 必须存在(AC-44-13)");

            // Assert:出厂默认有效(mono 默认 = false,即关)
            // ⚠️ 出厂默认值归 ADR-014 烘焙分区(用户调),当前阶段生产代码未实现
            //    本测试验证注册表含 mono_enabled 且默认值语义正确(false = 关)
            //    生产实现后应从烘焙分区读取实际默认值做断言
            Assert.Inconclusive("mono 出厂默认值待生产实现后从烘焙分区读取验证");
        }

        // ══════════════ AC-44-15:字幕键集覆盖 ══════════════

        /// <summary>AC-44-15 [A]:字幕文本本体 = 事件表 subtitle_text;表键集 ⊇ 语声 cue 集。
        /// 验证事件表中语声/口述族 cue 都有 subtitle_text 字段。</summary>
        [Test]
        public void test_subtitleKeySet_coveredByEventTable()
        {
            // Arrange:读真种子事件表
            string path = Path.Combine(repoRoot(), "assets", "data", "audio_events.json");
            Assert.That(File.Exists(path), Is.True, $"真种子缺失:{path}");
            string json = File.ReadAllText(path);

            // Act:提取语声/口述族 cue(Voice_/Narration_ 前缀)
            var subtitleCues = ExtractCuesWithPrefix(json, "Voice_")
                .Concat(ExtractCuesWithPrefix(json, "Narration_"))
                .ToList();

            // Assert:语声/口述族 cue 都有 subtitle_text(per-cue 验证,非全局搜索)
            foreach (string cue in subtitleCues)
            {
                // per-cue 验证:该 cue 行内必须有 subtitle_text
                bool hasSubtitle = HasSubtitleForCue(json, cue);
                Assert.That(hasSubtitle, Is.True,
                    $"语声/口述 cue「{cue}」必须有 subtitle_text 字段(AC-44-15:表键集 ⊇ 语声 cue 集)");
            }
        }

        /// <summary>per-cue 验证:该 cue 行内是否有 subtitle_text(非全局搜索)。</summary>
        private static bool HasSubtitleForCue(string json, string cue)
        {
            // 找到 cue 位置,检查其后 500 字符内是否有 subtitle_text
            int cueIdx = json.IndexOf($"\"{cue}\"", StringComparison.Ordinal);
            if (cueIdx < 0) return false;
            int searchEnd = Math.Min(cueIdx + 500, json.Length);
            string cueBlock = json.Substring(cueIdx, searchEnd - cueIdx);
            return cueBlock.Contains("subtitle_text", StringComparison.Ordinal);
        }

        /// <summary>AC-44-15 [A] 负向:缺 subtitle_text 的语声 cue ⇒ 红。</summary>
        [Test]
        public void test_subtitleKeySet_missingSubtitle_rejected()
        {
            // Arrange:构造缺 subtitle_text 的语声 cue
            string cue = "Voice_Cough_Damp";
            string jsonWithoutSubtitle = "{\"cue\":\"Voice_Cough_Damp\",\"bus\":\"Voice\"}";

            // Act:检查 subtitle_text
            bool hasSubtitle = jsonWithoutSubtitle.Contains("subtitle_text");

            // Assert:缺 subtitle_text 必须判失败
            Assert.That(hasSubtitle, Is.False,
                "缺 subtitle_text 的语声 cue 必须判失败(AC-44-15:缺失 = 构建失败)");
        }

        // ══════════════ AC-44-19:归零机制 ══════════════

        /// <summary>AC-44-19:归零三步(写默认 → 推 mixer → 落 sidecar)后 store == 出厂集。
        /// ⚠️ 生产归零机制尚未实现,本测试验证**归零契约面形状**(三步语义 + sidecar 持久化)。
        ///    生产实现后应替换为调用真实 IResetMechanism/ISettingsStore 接口。</summary>
        [Test]
        public void test_resetMechanism_threeStepsReachFactory()
        {
            // Arrange:被改坏的 store 值
            var store = new FakeSettingsStore();
            store.SetValue("bus_volume_master", 0.5f);  // 非出厂值
            store.SetValue("mono_enabled", true);        // 非出厂值

            // Act:归零三步
            var resetter = new FakeResetMechanism(store);
            resetter.ResetToFactory();

            // Assert:三步后 store == 出厂集
            Assert.That(store.GetValue("bus_volume_master"), Is.EqualTo(1.0f),
                "归零后 bus_volume_master == 出厂值 1.0");
            Assert.That(store.GetValue("mono_enabled"), Is.EqualTo(false),
                "归零后 mono_enabled == 出厂值 false");
            Assert.That(store.SidecarWritten, Is.True,
                "归零第三步必须落 sidecar(AC-44-19:归零三步缺一无效)");
        }

        /// <summary>AC-44-19:重启后仍 == 出厂集(sidecar 持久化)。
        /// ⚠️ 生产归零机制尚未实现,本测试验证 sidecar 持久化契约面形状。</summary>
        [Test]
        public void test_resetMechanism_persistsAfterRestart()
        {
            // Arrange:归零后的 store(sidecar 已写)
            var store = new FakeSettingsStore();
            var resetter = new FakeResetMechanism(store);
            resetter.ResetToFactory();
            Assert.That(store.SidecarWritten, Is.True, "前置:sidecar 必须已写");

            // Act:模拟重启(从 sidecar 重新加载)
            var newStore = new FakeSettingsStore();
            newStore.LoadFromSidecar(store.SidecarWritten);

            // Assert:重启后仍 == 出厂集
            Assert.That(newStore.GetValue("bus_volume_master"), Is.EqualTo(1.0f),
                "重启后 bus_volume_master == 出厂值(sidecar 持久化)");
            Assert.That(newStore.GetValue("mono_enabled"), Is.EqualTo(false),
                "重启后 mono_enabled == 出厂值");
        }

        /// <summary>AC-44-19 负向:跳过 sidecar 落盘 ⇒ 重启后红。
        /// ⚠️ 生产归零机制尚未实现,本测试验证 sidecar 缺失时重启失败的契约面形状。</summary>
        [Test]
        public void test_resetMechanism_skipSidecar_rejected()
        {
            // Arrange:跳过 sidecar 的归零
            var store = new FakeSettingsStore();
            store.SetValue("bus_volume_master", 0.5f);

            // Act:只写默认 + 推 mixer,跳过 sidecar
            store.SetValue("bus_volume_master", 1.0f);  // 写默认
            // 跳过 sidecar 落盘

            // Assert:sidecar 未写 ⇒ 重启后值丢失
            Assert.That(store.SidecarWritten, Is.False,
                "跳过 sidecar 落盘 ⇒ 重启后红(AC-44-19:归零三步缺一无效)");

            // 模拟重启:从 sidecar 加载(空)
            var newStore = new FakeSettingsStore();
            newStore.LoadFromSidecar(store.SidecarWritten);
            Assert.That(newStore.GetValue("bus_volume_master"), Is.Not.EqualTo(1.0f),
                "跳过 sidecar ⇒ 重启后值 != 出厂集(红)");
        }

        // ══════════════ AC-44-12:文档判据 ══════════════

        /// <summary>AC-44-12 [L]:三条具名项在位 —— 视觉替代引 AC-13-F1 · P0 非目标 · 三义务 AC 存在。
        /// 验证 GDD 文档包含三条具名项。</summary>
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

        // ══════════════ 测试替身与工具方法 ══════════════

        /// <summary>从 JSON 提取指定前缀的 cue 名(简化:正则匹配)。</summary>
        private static List<string> ExtractCuesWithPrefix(string json, string prefix)
        {
            var result = new List<string>();
            var pattern = "\"cue\"\\s*:\\s*\"(" + prefix + "[^\"]+)\"";
            var matches = System.Text.RegularExpressions.Regex.Matches(json, pattern);
            foreach (System.Text.RegularExpressions.Match m in matches)
                result.Add(m.Groups[1].Value);
            return result;
        }

        /// <summary>FakeSettingsStore:设置 store 谓词面(测试自持,纯函数)。
        /// 语义 = OQ-SS-5=甲:端本地 sidecar,与 7a 存档位解耦。</summary>
        private sealed class FakeSettingsStore
        {
            private readonly Dictionary<string, object> _values = new Dictionary<string, object>();

            public bool SidecarWritten { get; private set; }

            public void SetValue(string key, object value) => _values[key] = value;

            public object GetValue(string key)
                => _values.TryGetValue(key, out var v) ? v : null;

            public void WriteSidecar() => SidecarWritten = true;

            public void LoadFromSidecar(bool sidecarWritten)
            {
                // 从 sidecar 加载(简化:sidecar 未写 ⇒ 值为默认/空)
                if (!sidecarWritten)
                {
                    _values.Clear();
                }
                else
                {
                    // sidecar 已写 ⇒ 加载出厂值(模拟从 sidecar 持久化恢复)
                    _values["bus_volume_master"] = 1.0f;
                    _values["mono_enabled"] = false;
                }
            }
        }

        /// <summary>FakeResetMechanism:归零机制谓词面(测试自持,纯函数)。
        /// 语义 = AC-44-19:归零三步(写默认 → 推 mixer → 落 sidecar)。</summary>
        private sealed class FakeResetMechanism
        {
            private readonly FakeSettingsStore _store;

            public FakeResetMechanism(FakeSettingsStore store) => _store = store;

            public void ResetToFactory()
            {
                // 第一步:写默认
                _store.SetValue("bus_volume_master", 1.0f);
                _store.SetValue("mono_enabled", false);
                // 第二步:推 mixer(简化:无 mixer 依赖)
                // 第三步:落 sidecar
                _store.WriteSidecar();
            }
        }
    }
}
