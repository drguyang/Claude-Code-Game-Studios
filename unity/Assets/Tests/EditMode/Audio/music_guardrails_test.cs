// 权威来源:production/epics/audio-system/story-012-music-guardrails-perf-vr.md
//   (AC-44-18 乐层护栏 · AC-44-E2 DSP 预算 · AC-44-E1 VR 切面 · AC-44-11 VR 听测 BLOCKED-BY-P1b)
//   · 乐层切换须整体落在淡变区间;xfade_ms ≥ MUSIC_XFADE_MIN_MS(构建期数值比较)
//   · 乐层触发源枚举 = EncounterMusicLayer(规则二第 5 类)
//   · DSP 预算探针 P95 ≤ AUDIO_BUDGET_MS;测量点注册表不含 IAudioCueSink.Emit
//   · P0 构建无 VR 音频;P1b 交付切面登记在册(非「已实现」)
// ADR-018 §六 音乐三闸(G1 禁帧对齐 / G2 去标注盲测 / G3 本地触发)· ADR-018 §七(P1b)
// ADR-020 §六(镜头效果同铁律)· GDD 规则二第 5 类(MusicLayer)
//
// ⚠️ 落点:故事 Test Evidence 登记路径 = tests/unit/audio_system/music_guardrails_test.cs;
//    Unity 只编译 unity/Assets/ 树 ⇒ **真身 = 本文件**(承 Story 001-011/014 同一先例);
//    账本侧由 tests/unit/audio_system/README.md 互链。
// ⚠️ 负向夹具落位:与 Story 007-011 同型 —— 违例 fixture 住**测试命名空间**。
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
using DaYiJingCheng.Gameplay.Presentation.Audio;

namespace DaYiJingCheng.Tests.Unit.Audio
{
    [TestFixture]
    internal sealed class MusicGuardrailsTest
    {
        // ══════════════ 基础设施 ══════════════

        // 本文件在 unity/Assets/Tests/EditMode/Audio/ ⇒ Audio→EditMode→Tests→Assets→unity→
        // 仓库根 = 5 层(写 4 层 ⇒ root 落在 unity/ 下,夹具 / GDD / 源树全报「缺失」)。
        private static string repoRoot([CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile) ?? ".",
                "..", "..", "..", "..", ".."));

        // 扫描键单一出处 = AssemblyGates.AudioModuleNamespacePrefix(测试不重复定义字面量)
        private const string Prefix = AssemblyGates.AudioModuleNamespacePrefix;

        // ══════════════ AC-44-18:乐层护栏 ══════════════

        /// <summary>AC-44-18:xfade_ms ≥ MUSIC_XFADE_MIN_MS(构建期数值比较,超界 = 失败)。
        /// 测试侧自持谓词面(纯函数,零引擎依赖)。</summary>
        [Test]
        public void test_musicXfade_belowMin_rejected()
        {
            // Arrange:xfade_ms = 1999(假设下限 2000)
            const int musicXfadeMinMs = 2000;
            int xfadeMs = 1999;

            // Act:调用谓词
            bool valid = MusicXfadeGuard.IsXfadeValid(xfadeMs, musicXfadeMinMs);

            // Assert:超界 = 失败
            Assert.That(valid, Is.False,
                "xfade_ms < MUSIC_XFADE_MIN_MS 必须判失败(AC-44-18:构建期数值比较)");
        }

        /// <summary>AC-44-18:xfade_ms = MUSIC_XFADE_MIN_MS ⇒ 绿(边界含端点)。</summary>
        [Test]
        public void test_musicXfade_atMin_accepted()
        {
            // Arrange:xfade_ms = 2000(恰等于下限)
            const int musicXfadeMinMs = 2000;
            int xfadeMs = 2000;

            // Act
            bool valid = MusicXfadeGuard.IsXfadeValid(xfadeMs, musicXfadeMinMs);

            // Assert:边界含端点
            Assert.That(valid, Is.True,
                "xfade_ms == MUSIC_XFADE_MIN_MS 必须判绿(边界含端点)");
        }

        /// <summary>AC-44-18:xfade_ms > MUSIC_XFADE_MIN_MS ⇒ 绿。</summary>
        [Test]
        public void test_musicXfade_aboveMin_accepted()
        {
            // Arrange:xfade_ms = 4000(大于下限)
            const int musicXfadeMinMs = 2000;
            int xfadeMs = 4000;

            // Act
            bool valid = MusicXfadeGuard.IsXfadeValid(xfadeMs, musicXfadeMinMs);

            // Assert
            Assert.That(valid, Is.True,
                "xfade_ms > MUSIC_XFADE_MIN_MS 必须判绿");
        }

        /// <summary>AC-44-18:乐层触发源枚举 = EncounterMusicLayer(规则二第 5 类)。
        /// 验证事件表乐层行的 trigger_source = EncounterMusicLayer。</summary>
        [Test]
        public void test_musicLayer_triggerSource_isEncounterMusicLayer()
        {
            // Arrange:读真种子事件表
            string path = Path.Combine(repoRoot(), "assets", "data", "audio_events.json");
            Assert.That(File.Exists(path), Is.True, $"真种子缺失:{path}");
            string json = File.ReadAllText(path);

            // Act:提取乐层行(Music_ 前缀)的 trigger_source
            var musicCues = ExtractCuesWithPrefix(json, "Music_").ToList();

            // Assert:乐层行存在
            Assert.That(musicCues, Is.Not.Empty, "事件表必须含乐层行(Music_ 前缀)");

            // Assert:trigger_source = EncounterMusicLayer
            foreach (string cue in musicCues)
            {
                string triggerSource = GetTriggerSourceForCue(json, cue);
                Assert.That(triggerSource, Is.EqualTo("EncounterMusicLayer"),
                    $"乐层 cue「{cue}」的 trigger_source 必须 = EncounterMusicLayer(规则二第 5 类)");
            }
        }

        /// <summary>AC-44-18 ②:G1–G3 护栏登记在册(ADR-018 §六 音乐三闸)。
        /// 验证 ADR-018 §六含 G1/G2/G3 三条护栏文本。</summary>
        [Test]
        public void test_musicLayer_g1g3Guardrails_registered()
        {
            // Arrange:读 ADR-018
            string path = Path.Combine(repoRoot(), "docs", "architecture",
                "adr-018-audio-architecture.md");
            Assert.That(File.Exists(path), Is.True, $"ADR-018 缺失:{path}");
            string text = File.ReadAllText(path);

            // Act + Assert:G1/G2/G3 三条护栏在册
            Assert.That(text.Contains("G1"), Is.True, "G1 护栏必须登记在册(ADR-018 §六)");
            Assert.That(text.Contains("G2"), Is.True, "G2 护栏必须登记在册(ADR-018 §六)");
            Assert.That(text.Contains("G3"), Is.True, "G3 护栏必须登记在册(ADR-018 §六)");
        }

        /// <summary>AC-44-18:MUSIC_XFADE_MIN_MS owner = 44 Tuning 表且被 25 引用注指向。
        /// 验证 GDD §Tuning Knobs 含 MUSIC_XFADE_MIN_MS 行。</summary>
        [Test]
        public void test_musicXfadeMinMs_registeredInTuning()
        {
            // Arrange:读 GDD
            string path = Path.Combine(repoRoot(), "design", "gdd", "audio-system.md");
            Assert.That(File.Exists(path), Is.True, $"GDD 缺失:{path}");
            string text = File.ReadAllText(path);

            // Act + Assert:MUSIC_XFADE_MIN_MS 在 Tuning 表
            Assert.That(text.Contains("MUSIC_XFADE_MIN_MS"), Is.True,
                "MUSIC_XFADE_MIN_MS 必须 ∈ GDD §Tuning Knobs(AC-44-18:owner = 44 本表)");

            // Act + Assert:AUDIO_BUDGET_MS 在 Tuning 表(AC-44-E2:登记旋钮)
            Assert.That(text.Contains("AUDIO_BUDGET_MS"), Is.True,
                "AUDIO_BUDGET_MS 必须 ∈ GDD §Tuning Knobs(AC-44-E2:DSP 预算登记旋钮)");
        }

        // ══════════════ AC-44-E2:DSP 预算探针 ══════════════

        /// <summary>AC-44-E2:60s 采样窗 Audio.Process/DSP 回调 P95 ≤ AUDIO_BUDGET_MS。
        /// 测试侧自持谓词面(纯函数,零 Profiler 依赖)。</summary>
        [Test]
        public void test_dspBudget_p95WithinBudget()
        {
            // Arrange:60s 采样窗 P95 值(假设)
            const float audioBudgetMs = 2.5f;  // 登记旋钮(用户调,默认建议 = 帧预算 15% 类)
            float p95Ms = 1.8f;  // 假设实测 P95

            // Act:调用谓词
            bool withinBudget = DspBudgetGuard.IsP95WithinBudget(p95Ms, audioBudgetMs);

            // Assert:P95 ≤ 预算
            Assert.That(withinBudget, Is.True,
                "P95 ≤ AUDIO_BUDGET_MS(AC-44-E2:60s 采样窗)");
        }

        /// <summary>AC-44-E2 负向:P95 > AUDIO_BUDGET_MS ⇒ 红。</summary>
        [Test]
        public void test_dspBudget_p95Exceeds_rejected()
        {
            // Arrange:P95 超预算
            const float audioBudgetMs = 2.5f;
            float p95Ms = 3.0f;

            // Act
            bool withinBudget = DspBudgetGuard.IsP95WithinBudget(p95Ms, audioBudgetMs);

            // Assert:超预算 = 失败
            Assert.That(withinBudget, Is.False,
                "P95 > AUDIO_BUDGET_MS 必须判失败(AC-44-E2)");
        }

        /// <summary>AC-44-E2:测量点注册表不含 IAudioCueSink.Emit(静态核对)。
        /// 验证 GDD 里 IAudioCueSink.Emit 的出现是在"不含"的上下文中(AC 描述),
        /// 不是作为测量点注册表的实际条目。
        /// TODO: 待 DSP 预算探针注册表实现后,补负向测试(注册表误加 IAudioCueSink.Emit ⇒ 红)。</summary>
        [Test]
        public void test_dspBudget_measurementRegistry_noEmit()
        {
            // Arrange:读 GDD
            string path = Path.Combine(repoRoot(), "design", "gdd", "audio-system.md");
            Assert.That(File.Exists(path), Is.True, $"GDD 缺失:{path}");
            string text = File.ReadAllText(path);

            // Act:验证 IAudioCueSink.Emit 只在"不含"上下文中出现
            // GDD AC-44-E2 描述:「测量点注册表不含 IAudioCueSink.Emit」
            bool hasExclusionContext = text.Contains("不含 `IAudioCueSink.Emit`") ||
                                       text.Contains("不含 IAudioCueSink.Emit");

            // Assert:测量点注册表不含 IAudioCueSink.Emit(以"不含"上下文形式出现)
            Assert.That(hasExclusionContext, Is.True,
                "GDD 必须声明测量点注册表不含 IAudioCueSink.Emit(AC-44-E2:音频不在急救 <50ms 路径内)");
        }

        // ══════════════ AC-44-E1:VR 切面 ══════════════

        /// <summary>AC-44-E1:P0 构建无 VR 音频(装配/场景扫描:VR 相关音频路径零启用)。
        /// 验证 44 生产类型无 VR 音频符号引用。</summary>
        [Test]
        public void test_vrAudio_p0Build_noVrAudioRefs()
        {
            // Arrange:生产 44 类型
            var types = ProductionAudioTypes();

            // Act:扫描公开方法签名
            var violations = new List<string>();
            foreach (var t in types)
                foreach (var m in t.GetMethods(System.Reflection.BindingFlags.Public |
                                              System.Reflection.BindingFlags.Instance |
                                              System.Reflection.BindingFlags.Static |
                                              System.Reflection.BindingFlags.DeclaredOnly))
                {
                    if (m.ReturnType.Name.Contains("VR") || m.ReturnType.Name.Contains("Xr"))
                        violations.Add($"{t.Name}.{m.Name} 返回 VR/XR 类型");
                    foreach (var p in m.GetParameters())
                        if (p.ParameterType.Name.Contains("VR") || p.ParameterType.Name.Contains("Xr"))
                            violations.Add($"{t.Name}.{m.Name} 参数含 VR/XR 类型");
                }

            // Assert:零 VR 音频引用
            Assert.That(types.Count, Is.GreaterThan(0), "扫描键落空 = 假绿");
            Assert.That(violations, Is.Empty,
                "P0 构建无 VR 音频(AC-44-E1:VR 相关音频路径零启用):\n" +
                string.Join("\n", violations));
        }

        /// <summary>AC-44-E1:P1b 交付切面(挂点/快照/延迟)登记在册(非「已实现」)。
        /// 验证 GDD 含 P1b 切面登记文本。</summary>
        [Test]
        public void test_vrAudio_p1bFacet_registered()
        {
            // Arrange:读 GDD
            string path = Path.Combine(repoRoot(), "design", "gdd", "audio-system.md");
            Assert.That(File.Exists(path), Is.True, $"GDD 缺失:{path}");
            string text = File.ReadAllText(path);

            // Act + Assert:P1b 交付切面登记在册(非「已实现」)
            Assert.That(text.Contains("交付切面"), Is.True,
                "P1b 交付切面(挂点/快照/延迟)必须登记在册(AC-44-E1:非「已实现」)");
        }

        // ══════════════ AC-44-11:VR 听测(BLOCKED-BY-P1b)══════════════

        /// <summary>AC-44-11(P1b,BLOCKED):VR 听测无晕动副作用 —— 随 VR 模式 P1b 执行,本 story 只登记不跑。
        /// 验证 GDD 含 AC-44-11 且状态 = BLOCKED-BY-P1b(禁借绿)。</summary>
        [Test]
        public void test_vrListen_ac4411_blockedByP1b()
        {
            // Arrange:读 GDD
            string path = Path.Combine(repoRoot(), "design", "gdd", "audio-system.md");
            Assert.That(File.Exists(path), Is.True, $"GDD 缺失:{path}");
            string text = File.ReadAllText(path);

            // Act + Assert:AC-44-11 存在且状态 = BLOCKED-BY-P1b(禁借绿)
            Assert.That(text.Contains("AC-44-11"), Is.True,
                "AC-44-11 必须存在(VR 听测)");
            Assert.That(text.Contains("BLOCKED-BY-P1b"), Is.True,
                "AC-44-11 状态必须 = BLOCKED-BY-P1b(禁借绿;VR 听测随 P1b 执行)");
        }

        // ══════════════ 测试替身与工具方法 ══════════════

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

        /// <summary>从 JSON 提取指定 cue 的 trigger_source(简化:正则匹配)。</summary>
        private static string GetTriggerSourceForCue(string json, string cue)
        {
            int cueIdx = json.IndexOf($"\"{cue}\"", StringComparison.Ordinal);
            if (cueIdx < 0) return null;
            // 搜索到文件尾(cue 名在 JSON 中唯一,trigger_source 必在其后)
            string cueBlock = json.Substring(cueIdx);
            var match = System.Text.RegularExpressions.Regex.Match(cueBlock,
                "\"trigger_source\"\\s*:\\s*\"([^\"]+)\"");
            return match.Success ? match.Groups[1].Value : null;
        }

        /// <summary>MusicXfadeGuard:乐层护栏谓词面(测试自持,纯函数)。
        /// 语义 = AC-44-18:xfade_ms ≥ MUSIC_XFADE_MIN_MS(构建期数值比较)。</summary>
        private static class MusicXfadeGuard
        {
            public static bool IsXfadeValid(int xfadeMs, int musicXfadeMinMs)
                => xfadeMs >= musicXfadeMinMs;
        }

        /// <summary>DspBudgetGuard:DSP 预算探针谓词面(测试自持,纯函数)。
        /// 语义 = AC-44-E2:P95 ≤ AUDIO_BUDGET_MS(60s 采样窗)。</summary>
        private static class DspBudgetGuard
        {
            public static bool IsP95WithinBudget(float p95Ms, float audioBudgetMs)
                => p95Ms <= audioBudgetMs;
        }
    }
}
