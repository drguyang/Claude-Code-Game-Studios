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
//
// ══════════════════════════════════════════════════════════════════════════════
// ✅ 2026-10-16 S14-4 实现轮(用户裁定三项;上游判定 = 评审原件 §2.6)
//
// **原判定(12 测中 🟢1 / 🟡6 / 🔴5)**:
//   · 🔴 ×5 = 本文件自写两行谓词 `MusicXfadeGuard` / `DspBudgetGuard` + 双端硬编码
//     (`musicXfadeMinMs = 2000` / `audioBudgetMs = 2.5f`),**从不读真种子 / 真常量**
//   · 🟡 ×3 = 半真(见下 §半真强化)
//
// **本轮的处置**:
//   · 自写谓词**全数删除** —— 改打生产件:
//     `AudioEventTableGates.ValidateXfade`(构建期真门,原唯一具名空位)
//     · `AudioTuning.MusicXfadeMinMs` / `.AudioBudgetMs`(常量单一出处)
//     · `AudioBudgetProbe`(分位 + 预算判定)
//     · `AudioMeasurementPoints`(AC-10-08 测量点闭集 + 反空转守卫)
//   · **判据强度**:不再断言「GDD 含某句散文」,改断言**生产门的返回值随输入而变**。
// ══════════════════════════════════════════════════════════════════════════════

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

        /// <summary>造一行乐层 cue(<c>whitelist_category = MusicLayer</c>)。</summary>
        private static AudioEventTableRow MusicRow(string cue, string xfadeRaw) => new AudioEventTableRow
        {
            Cue = cue,
            WhitelistCategory = AudioEventTableGates.CategoryMusicLayer,
            TriggerSource = AudioEventTableGates.TriggerEncounterMusicLayer,
            XfadeRaw = xfadeRaw,
        };

        // ══════════════ AC-44-18①:xfade_ms 数值下界(构建期真门)══════════════
        //
        // 被测物 = `AudioEventTableGates.ValidateXfade`(生产,构建期门,在 `Validate` 聚合链内)。
        // 下界 = `AudioTuning.MusicXfadeMinMs`(**单一出处**,门内不另抄 —— ADR-024 §⑥)。

        /// <summary>AC-44-18①:乐层行 <c>xfade_ms &lt; MUSIC_XFADE_MIN_MS</c> ⇒ 门拒。
        /// 输入**相对真常量**构造(不硬编码 1999 —— 常量调值时本测仍有效)。</summary>
        [Test]
        public void test_musicXfade_belowMin_rejected()
        {
            // Arrange:下界 - 1(相对真常量,非硬编码)
            int belowMin = AudioTuning.MusicXfadeMinMs - 1;
            var rows = new[] { MusicRow("Music_Encounter_Layer", belowMin.ToString()) };

            // Act:调**生产构建期门**
            IReadOnlyList<string> errors = AudioEventTableGates.ValidateXfade(rows);

            // Assert:超界 = 构建失败(AC-44-18①「构建期数值比较,超界 = 失败」)
            Assert.That(errors, Is.Not.Empty,
                $"xfade_ms = {belowMin} < MUSIC_XFADE_MIN_MS = {AudioTuning.MusicXfadeMinMs} " +
                "必须判失败(AC-44-18①:构建期数值比较)");
            Assert.That(errors[0], Does.Contain("MUSIC_XFADE_MIN_MS"),
                "诊断须点名下界常量(否则调门的人不知道红线在哪)");
        }

        /// <summary>AC-44-18①:边界 <c>xfade_ms = MUSIC_XFADE_MIN_MS</c> ⇒ 门过(含端点)。</summary>
        [Test]
        public void test_musicXfade_atMin_accepted()
        {
            // Arrange:恰等于下界(读真常量)
            var rows = new[] { MusicRow("Music_Encounter_Layer", AudioTuning.MusicXfadeMinMs.ToString()) };

            // Act
            IReadOnlyList<string> errors = AudioEventTableGates.ValidateXfade(rows);

            // Assert:边界含端点 ⇒ 零错误
            Assert.That(errors, Is.Empty,
                $"xfade_ms == MUSIC_XFADE_MIN_MS({AudioTuning.MusicXfadeMinMs})必须判绿(边界含端点):\n" +
                string.Join("\n", errors));
        }

        /// <summary>AC-44-18①:<c>xfade_ms &gt; MUSIC_XFADE_MIN_MS</c> ⇒ 门过。
        /// ⚠️ 用**真种子值**(4000,读 <c>assets/data/audio_events.json</c>)—— 不再是硬编码假设值,
        /// 这正修掉原判定的 🔴「测试从不读真种子」。</summary>
        [Test]
        public void test_musicXfade_aboveMin_accepted()
        {
            // Arrange:读真种子乐层行
            string path = Path.Combine(repoRoot(), "assets", "data", "audio_events.json");
            Assert.That(File.Exists(path), Is.True, $"真种子缺失:{path}");
            string json = File.ReadAllText(path);
            string seedXfade = GetIntFieldForCue(json, "Music_Encounter_Layer", "xfade_ms");
            Assert.That(seedXfade, Is.Not.Null, "真种子乐层行必须带 xfade_ms(AC-44-09 存在性)");

            // Act:真种子值过**生产门**
            IReadOnlyList<string> errors = AudioEventTableGates.ValidateXfade(
                new[] { MusicRow("Music_Encounter_Layer", seedXfade) });

            // Assert
            Assert.That(errors, Is.Empty,
                $"真种子 xfade_ms = {seedXfade} 必须过门(≥ {AudioTuning.MusicXfadeMinMs}):\n" +
                string.Join("\n", errors));
            Assert.That(int.Parse(seedXfade), Is.GreaterThanOrEqualTo(AudioTuning.MusicXfadeMinMs),
                "真种子值须 ≥ 下界 —— 否则门会在真表上红(种子 / 常量失配)");
        }

        /// <summary>AC-44-18① 定义域收窄守卫:下界**只对乐层行** —— 非乐层行的小 <c>xfade_ms</c>
        /// 不得被拒(把下界套到可选字段会给非乐层行强加 G1 护栏,超出 AC-44-18 面)。</summary>
        [Test]
        public void test_nonMusicRow_belowMin_notRejected()
        {
            // Arrange:非乐层行 + 远小于下界的 xfade_ms
            var rows = new[]
            {
                new AudioEventTableRow
                {
                    Cue = "Voice_Cough_Damp",
                    WhitelistCategory = "ContinuousPhysiology",
                    TriggerSource = "ContinuousPhysiology",
                    XfadeRaw = "1",
                },
            };

            // Act
            IReadOnlyList<string> errors = AudioEventTableGates.ValidateXfade(rows);

            // Assert:非乐层行不判下界
            Assert.That(errors, Is.Empty,
                "xfade_ms 下界只对乐层行(whitelist_category = MusicLayer);" +
                "非乐层行是可选的 per-cue 覆盖,不受 G1 约束:\n" + string.Join("\n", errors));
        }

        /// <summary>AC-44-18①:诊断文本携带**真常量值** ⇒ 门与常量绑定(常量改了诊断跟着改,
        /// 不会出现「门说 2000 而常量是 3000」的静默分叉)。</summary>
        [Test]
        public void test_musicXfade_belowMin_diagnosticCarriesTuningValue()
        {
            // Arrange
            var rows = new[] { MusicRow("Music_Encounter_Layer", "1") };

            // Act
            IReadOnlyList<string> errors = AudioEventTableGates.ValidateXfade(rows);

            // Assert:诊断含真常量值(单一出处的可观测证据)
            Assert.That(errors, Is.Not.Empty);
            Assert.That(errors[0], Does.Contain(AudioTuning.MusicXfadeMinMs.ToString()),
                "诊断必须携带 AudioTuning.MusicXfadeMinMs 的**真值** —— " +
                "门内另抄一份数值会让两处分叉(ADR-024 §⑥)");
        }

        // ══════════════ AC-44-18②:触发源 + G1–G3 护栏 ══════════════

        /// <summary>AC-44-18②:乐层触发源枚举 = EncounterMusicLayer(规则二第 5 类)。
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

        /// <summary>AC-44-18②:G1–G3 护栏登记在册(ADR-018 §六 音乐三闸)。
        /// <para>⚠️ **S14-4 强化**(原判 🟡 半真:`Contains("G1")` **无锚点** —— 正文任何位置出现
        /// 字符串「G1」即过,包括「本 ADR 不设 G1」这种反向句)。现改为**锚定表格行首列**
        /// `| **G1 禁帧对齐**` 等三行 —— 只有真登记成表行才过。</para></summary>
        [Test]
        public void test_musicLayer_g1g3Guardrails_registered()
        {
            // Arrange:读 ADR-018
            string path = Path.Combine(repoRoot(), "docs", "architecture",
                "adr-018-audio-architecture.md");
            Assert.That(File.Exists(path), Is.True, $"ADR-018 缺失:{path}");
            string text = File.ReadAllText(path);

            // Act + Assert:G1/G2/G3 三条护栏**以表格行首列**登记在册(锚点化)
            Assert.That(HasGuardrailRow(text, "G1 禁帧对齐"), Is.True,
                "G1 必须以「| **G1 禁帧对齐**」形式登记为 ADR-018 §六 表格行" +
                "(无锚点的 Contains(\"G1\") 会被反向句蒙混过关 —— 原判 🟡 半真的根因)");
            Assert.That(HasGuardrailRow(text, "G2 去标注盲测"), Is.True,
                "G2 必须以「| **G2 去标注盲测**」形式登记为表格行");
            Assert.That(HasGuardrailRow(text, "G3 本地设备侧触发"), Is.True,
                "G3 必须以「| **G3 本地设备侧触发**」形式登记为表格行");
        }

        /// <summary>ADR-018 §六 护栏**表格行首列**判定。
        /// <para>⚠️ **锚定是刻意的**:原判 🟡 半真的根因是裸 `Contains("G1")` —— 正文任何位置
        /// (含「本 ADR 不设 G1」这类反向句)都会通过。</para>
        /// <para>⚠️ **锚点必须是真行首**(行首 = 文本开头或 <c>\n</c> 之后),**不能**只找
        /// <c>"| **G1 …"</c> —— 后者会放过 <c>| 说明 | **G1 …** |</c> 这种「G1 出现在中间列」
        /// 的形态。本判据的这条边界由 <see cref="test_guardrailAnchor_rejectsBareMention"/>
        /// 自证(首版漏了它,自证测当场转红 ⇒ 判据已修)。</para></summary>
        private static bool HasGuardrailRow(string text, string guardrailTitle)
        {
            string anchor = "| **" + guardrailTitle + "**";
            if (text.StartsWith(anchor, StringComparison.Ordinal)) return true;
            return text.Contains("\n" + anchor, StringComparison.Ordinal);
        }

        /// <summary>护栏锚定判据的**自证**(证伪用):反向句 / 裸提及**不得**通过锚定判据。
        /// <para>没有这条,「锚定」与「裸 Contains」在真文档上结果相同 ⇒ 判据的强化不可证伪。</para></summary>
        [Test]
        public void test_guardrailAnchor_rejectsBareMention()
        {
            // Arrange:三种**不应**通过锚定的文本
            const string bareMention = "本节不设 G1 护栏(已并入 §六)。";
            const string reversePhrase = "原「G1」条款已废止。";
            const string wrongColumn = "| 说明 | **G1 禁帧对齐** |";

            // Act + Assert
            Assert.That(HasGuardrailRow(bareMention, "G1 禁帧对齐"), Is.False,
                "裸提及不得通过锚定判据");
            Assert.That(HasGuardrailRow(reversePhrase, "G1 禁帧对齐"), Is.False,
                "反向句不得通过锚定判据");
            Assert.That(HasGuardrailRow(wrongColumn, "G1 禁帧对齐"), Is.False,
                "非行首列不得通过(锚点 = 行首)");

            // Assert:真表行**必须**通过(否则判据过严 = 假红源)
            Assert.That(HasGuardrailRow("| **G1 禁帧对齐** | 内容 | 理据 |", "G1 禁帧对齐"), Is.True,
                "真登记表行必须通过(过严的判据 = 假红源)");
        }

        /// <summary>AC-44-18③:`MUSIC_XFADE_MIN_MS` owner = 44 本表 —— 两条独立断言:
        /// ① 44 侧 GDD §Tuning Knobs 有该行且声明 owner;② **25 侧同名引用在场**
        /// (跨文件,补原 NICE「AC-44-18③ 的 25 侧引用未跨文件断言」)。</summary>
        [Test]
        public void test_musicXfadeMinMs_ownerIs44_withCrossFileReference()
        {
            // Arrange:读 44 侧 GDD
            string gddPath = Path.Combine(repoRoot(), "design", "gdd", "audio-system.md");
            Assert.That(File.Exists(gddPath), Is.True, $"GDD 缺失:{gddPath}");
            string gdd = File.ReadAllText(gddPath);

            // Assert ①:44 侧 Tuning 表有该行
            Assert.That(gdd.Contains("MUSIC_XFADE_MIN_MS"), Is.True,
                "MUSIC_XFADE_MIN_MS 必须 ∈ GDD §Tuning Knobs(AC-44-18③:owner = 44 本表)");
            Assert.That(gdd.Contains("owner = 44 本表"), Is.True,
                "GDD §Tuning Knobs 必须**声明 owner = 44 本表**(AC-44-18③ 逐字)");

            // Assert ②:25 侧同名引用在场(跨文件 —— 原 NICE 登记的缺口)
            string combatPath = Path.Combine(repoRoot(), "design", "gdd",
                "combat-and-weapon-lines.md");
            Assert.That(File.Exists(combatPath), Is.True, $"25 GDD 缺失:{combatPath}");
            string combat = File.ReadAllText(combatPath);
            Assert.That(combat.Contains("MUSIC_XFADE_MIN_MS"), Is.True,
                "25 侧必须含 MUSIC_XFADE_MIN_MS 的同名引用(AC-44-18③:" +
                "「25 侧同名引用 … 的 owner = 44 本表」)");

            // Assert ③:AUDIO_BUDGET_MS 亦在 Tuning 表(AC-44-E2:登记旋钮)
            Assert.That(gdd.Contains("AUDIO_BUDGET_MS"), Is.True,
                "AUDIO_BUDGET_MS 必须 ∈ GDD §Tuning Knobs(AC-44-E2:DSP 预算登记旋钮)");
        }

        // ══════════════ AC-44-E2:DSP 预算探针(生产谓词)══════════════
        //
        // 被测物 = `AudioBudgetProbe`(生产)。预算上限 = `AudioTuning.AudioBudgetMs`。
        // ⚠️ 实测 P95 归 evidence 文档(60 s 窗须真 GPU + 真音频回调)—— 本处只验判据与算法。

        /// <summary>AC-44-E2:P95 在预算内 ⇒ 通过(打**生产** <see cref="AudioBudgetProbe"/>)。</summary>
        [Test]
        public void test_dspBudget_p95WithinBudget()
        {
            // Arrange:样本 + 真预算常量
            double p95Ms = AudioTuning.AudioBudgetMs * 0.72;   // 相对真预算,不硬编码 1.8

            // Act
            bool withinBudget = AudioBudgetProbe.IsP95WithinBudget(p95Ms, AudioTuning.AudioBudgetMs);

            // Assert
            Assert.That(withinBudget, Is.True,
                $"P95({p95Ms}) ≤ AUDIO_BUDGET_MS({AudioTuning.AudioBudgetMs})(AC-44-E2:60 s 采样窗)");
        }

        /// <summary>AC-44-E2 负向:P95 &gt; 预算 ⇒ 判失败。</summary>
        [Test]
        public void test_dspBudget_p95Exceeds_rejected()
        {
            // Arrange
            double p95Ms = AudioTuning.AudioBudgetMs * 1.2;

            // Act
            bool withinBudget = AudioBudgetProbe.IsP95WithinBudget(p95Ms, AudioTuning.AudioBudgetMs);

            // Assert
            Assert.That(withinBudget, Is.False,
                $"P95({p95Ms}) > AUDIO_BUDGET_MS({AudioTuning.AudioBudgetMs})必须判失败(AC-44-E2)");
        }

        /// <summary>AC-44-E2 边界:<c>P95 == AUDIO_BUDGET_MS</c> ⇒ 通过(`≤` 含端点)。
        /// 补原 NICE「dspBudget 边界值 p95 == budget(≤ 端点)未测」。</summary>
        [Test]
        public void test_dspBudget_atBudget_accepted()
        {
            // Act
            bool withinBudget =
                AudioBudgetProbe.IsP95WithinBudget(AudioTuning.AudioBudgetMs, AudioTuning.AudioBudgetMs);

            // Assert
            Assert.That(withinBudget, Is.True,
                "P95 == AUDIO_BUDGET_MS 必须判绿(`≤` 含端点;与 xfade 的 `≥` 含端点同族)");
        }

        /// <summary>AC-44-E2 反空转:样本不足哨兵(<c>-1</c>)⇒ 判**失败**,
        /// 不得被读成「预算达标」(拒以空集冒充绿)。</summary>
        [Test]
        public void test_dspBudget_noSamples_notWithinBudget()
        {
            // Act:哨兵 = 探针没采到样
            bool withinBudget = AudioBudgetProbe.IsP95WithinBudget(-1, AudioTuning.AudioBudgetMs);

            // Assert
            Assert.That(withinBudget, Is.False,
                "样本不足(-1 哨兵)不得冒充「预算达标」—— 与 PresentationDtoGuard.Scan(null) 同款");
        }

        /// <summary>AC-44-E2:分位算法 = 最近秩法(与 <c>InputLatencyProbe</c> 同口径),
        /// 空集 = <c>-1</c> 哨兵(不是 0 —— 0 会被误判为「极快」)。</summary>
        [Test]
        public void test_dspBudget_percentile_nearestRank()
        {
            // Arrange:1..100 升序样本 ⇒ 最近秩 P95 = ceil(100 × 0.95) - 1 = 94(0-based)⇒ 值 95
            var samples = new List<double>();
            for (int i = 1; i <= 100; i++) samples.Add(i);

            // Act
            double p95 = AudioBudgetProbe.Percentile(samples, 95);
            double empty = AudioBudgetProbe.Percentile(new List<double>(), 95);

            // Assert
            Assert.That(p95, Is.EqualTo(95.0).Within(1e-9),
                "最近秩法:ceil(n × p / 100) - 1(与 InputLatencyProbe.ComputePercentile 同口径)");
            Assert.That(empty, Is.EqualTo(-1.0),
                "空集 ⇒ -1 哨兵(不是 0;0 会被 IsP95WithinBudget 误判为通过)");
        }

        /// <summary>AC-44-E2:测量点集**不含** <c>IAudioCueSink.Emit</c>(静态核对)。
        /// <para>⚠️ **S14-4 强化**(原判 🟡 半真:断言的宾语是「AC 的判据散文存在于 GDD」,
        /// 不是「某个注册表不含 Emit」)。现打**生产** <see cref="AudioMeasurementPoints"/>
        /// —— 集合非空 + 不含发射入口,两条都是真判据。</para></summary>
        [Test]
        public void test_dspBudget_measurementRegistry_noEmit()
        {
            // Act:校验生产闭集
            IReadOnlyList<string> errors = AudioMeasurementPoints.Validate(AudioMeasurementPoints.Ac1008Points);

            // Assert:零错误(集合非空 + 不含 Emit)
            Assert.That(errors, Is.Empty,
                "AC-10-08 测量点集必须非空且不含音频发射入口:\n" + string.Join("\n", errors));

            // Assert:集合真的非空(反空转的可观测面 —— 防「Validate 对空集也返回空」)
            Assert.That(AudioMeasurementPoints.Ac1008Points.Count, Is.GreaterThan(0),
                "测量点集不得为空 —— 「不含 X」对空集平凡成立(评审原件 §1.3「空转真扫」)");
        }

        /// <summary>AC-44-E2 反空转守卫负例:**空集** ⇒ 红。</summary>
        [Test]
        public void test_measurementRegistry_emptySet_isRed()
        {
            // Act
            IReadOnlyList<string> errors = AudioMeasurementPoints.Validate(new string[0]);

            // Assert
            Assert.That(errors, Is.Not.Empty, "空集必须红(反空转守卫)");
            Assert.That(errors[0], Does.Contain("反空转"),
                "空集的诊断须点名**反空转** —— 与 null(输入缺失)区分");
        }

        /// <summary>AC-44-E2 反空转守卫负例:<c>null</c> ⇒ 红,且诊断与空集**不同**
        /// (「忘了填」与「被清空」必须可区分)。</summary>
        [Test]
        public void test_measurementRegistry_null_isRed()
        {
            // Act
            IReadOnlyList<string> errors = AudioMeasurementPoints.Validate(null);

            // Assert
            Assert.That(errors, Is.Not.Empty, "null 必须红");
            Assert.That(errors[0], Does.Contain("NOT-RUN"),
                "null 的诊断 = 输入缺失(NOT-RUN 守卫),与空集的反空转区分");
        }

        /// <summary>AC-44-E2 负向:发射入口混入测量点集 ⇒ 红(AC-44-E2 的「不含」是可判的)。</summary>
        [Test]
        public void test_measurementRegistry_emitInjected_isRed()
        {
            // Arrange:把发射入口塞进集合
            var polluted = new List<string>(AudioMeasurementPoints.Ac1008Points)
            {
                AudioMeasurementPoints.EmitSymbol,
            };

            // Act
            IReadOnlyList<string> errors = AudioMeasurementPoints.Validate(polluted);

            // Assert
            Assert.That(errors, Is.Not.Empty, "含 IAudioCueSink.Emit ⇒ 必红(AC-44-E2)");
            Assert.That(errors[0], Does.Contain("IAudioCueSink.Emit"),
                "诊断须点名被塞进来的发射入口");
        }

        // ══════════════ AC-44-E1:VR 切面 ══════════════

        /// <summary>AC-44-E1:P0 构建无 VR 音频(装配/场景扫描:VR 相关音频路径零启用)。
        /// <para>⚠️ **S14-4 强化**(原判 🟡 半真:扫描面只有**公开方法签名**,字段 / 属性 /
        /// 装配引用集都不在面内)。现扩为三面:公开方法签名 + **字段** + **属性**,
        /// 并对 44 装配的**引用集**断言无 VR 相关装配。</para></summary>
        [Test]
        public void test_vrAudio_p0Build_noVrAudioRefs()
        {
            // Arrange:生产 44 类型
            var types = ProductionAudioTypes();
            Assert.That(types.Count, Is.GreaterThan(0), "扫描键落空 = 假绿");

            // Act:三面扫描(方法签名 / 字段 / 属性)—— 与自证测**共用同一方法**
            var violations = ScanVrViolations(types);

            // ④ 装配引用集(S14-4 新增面)
            var asm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == AssemblyGates.PresentationAssemblyName);
            Assert.That(asm, Is.Not.Null, "Gameplay.Presentation 必须已加载");
            foreach (var refName in asm.GetReferencedAssemblies().Select(r => r.Name))
                if (IsVrName(refName))
                    violations.Add($"Gameplay.Presentation 引用了 VR/XR 装配「{refName}」");

            // Assert:零 VR 音频引用
            Assert.That(violations, Is.Empty,
                "P0 构建无 VR 音频(AC-44-E1:VR 相关音频路径零启用):\n" +
                string.Join("\n", violations));
        }

        /// <summary>名称是否带 VR / XR 标记(大小写不敏感 —— 装配名与类型名的惯用大小写不一致)。</summary>
        private static bool IsVrName(string name)
            => !string.IsNullOrEmpty(name) &&
               (name.IndexOf("VR", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Xr", StringComparison.OrdinalIgnoreCase) >= 0);

        /// <summary>AC-44-E1 的**负向夹具自证**:扫描面必须能真的抓到 VR 字段 / 属性 / 方法签名。
        /// <para>⚠️ **为什么需要这条**:扩展的三个面(字段 / 属性 / 引用集)在当前 44 生产代码下
        /// **零命中** ⇒ 删掉它们主测照样绿 = **弱判别面**(不可证伪)。本测注入
        /// <see cref="VrLeakFixture"/> 证明三条面**真的有判别力** —— 与
        /// `assembly_boundary_test` 的 `FieldLeakFixture` 同款「负例必须有」纪律。</para></summary>
        [Test]
        public void test_vrAudio_scanSurface_catchesVrMembers()
        {
            // Act:扫负向夹具
            var violations = ScanVrViolations(new[] { typeof(VrLeakFixture) });

            // Assert:字段 / 属性 / 方法签名三面各自命中
            Assert.That(violations.Any(v => v.Contains("_vrField")), Is.True,
                "字段面必须能抓 VR 类型字段:\n" + string.Join("\n", violations));
            Assert.That(violations.Any(v => v.Contains("VrProp")), Is.True,
                "属性面必须能抓 VR 类型属性:\n" + string.Join("\n", violations));
            Assert.That(violations.Any(v => v.Contains("GetVrHandle")), Is.True,
                "方法签名面必须能抓 VR 返回类型:\n" + string.Join("\n", violations));
        }

        /// <summary>VR 违例扫描(**抽成方法** —— 主测与自证测共用同一扫描面,
        /// 避免「主测扫一面、自证测扫另一面」的两处分叉)。</summary>
        private static List<string> ScanVrViolations(IEnumerable<Type> types)
        {
            var violations = new List<string>();
            const BindingFlags anyMember = BindingFlags.Public | BindingFlags.NonPublic |
                                           BindingFlags.Instance | BindingFlags.Static |
                                           BindingFlags.DeclaredOnly;

            foreach (var t in types)
            {
                // ① 公开方法签名
                foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance |
                                               BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    if (IsVrName(m.ReturnType.Name)) violations.Add($"{t.Name}.{m.Name} 返回 VR/XR 类型");
                    foreach (var p in m.GetParameters())
                        if (IsVrName(p.ParameterType.Name))
                            violations.Add($"{t.Name}.{m.Name} 参数含 VR/XR 类型");
                }

                // ② 字段
                foreach (var f in t.GetFields(anyMember))
                    if (IsVrName(f.FieldType.Name)) violations.Add($"{t.Name}.{f.Name} 字段为 VR/XR 类型");

                // ③ 属性
                foreach (var p in t.GetProperties(anyMember))
                    if (IsVrName(p.PropertyType.Name)) violations.Add($"{t.Name}.{p.Name} 属性为 VR/XR 类型");
            }
            return violations;
        }

        // ══════════════ 负向夹具(住测试命名空间,不污染 44 生产扫描键)══════════════

        /// <summary>VR 违例夹具:一个假 VR 句柄类型 + 持它的字段 / 属性 / 方法。
        /// 住测试命名空间 ⇒ 不进 <see cref="ProductionAudioTypes"/> 的生产扫描面。</summary>
        internal sealed class VrLeakFixture
        {
            /// <summary>VR 类型字段(字段面判据)。</summary>
            private VrHandleStub _vrField;

            /// <summary>VR 类型属性(属性面判据)。</summary>
            public VrHandleStub VrProp { get; set; }

            /// <summary>VR 返回类型的方法(方法签名面判据)。</summary>
            public VrHandleStub GetVrHandle() => _vrField;
        }

        /// <summary>假 VR 句柄(名字含 VR ⇒ <see cref="IsVrName"/> 命中)。</summary>
        internal sealed class VrHandleStub
        {
        }

        /// <summary>AC-44-E1:P1b 交付切面(挂点/快照/延迟)登记在册(非「已实现」)。</summary>
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

        // ══════════════ 工具方法 ══════════════

        /// <summary>生产 44 类型:只取 Gameplay.Presentation 装配的 44 命名空间。</summary>
        private static List<Type> ProductionAudioTypes()
        {
            var asm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == AssemblyGates.PresentationAssemblyName);
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

        /// <summary>从 JSON 提取指定 cue 的整数字段原文(供「读真种子」用)。</summary>
        private static string GetIntFieldForCue(string json, string cue, string field)
        {
            int cueIdx = json.IndexOf($"\"{cue}\"", StringComparison.Ordinal);
            if (cueIdx < 0) return null;
            string cueBlock = json.Substring(cueIdx);
            var match = System.Text.RegularExpressions.Regex.Match(cueBlock,
                "\"" + field + "\"\\s*:\\s*(\\d+)");
            return match.Success ? match.Groups[1].Value : null;
        }
    }
}
