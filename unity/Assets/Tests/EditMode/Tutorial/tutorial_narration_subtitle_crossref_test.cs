// S25-4 收口轮(2026-11-07)· `TD-tutorial-01` 的判据 —— 48 口述文案 ↔ 44 字幕文本的跨表一致。
//
// 权威来源:
//   `design/gdd/tutorial-and-onboarding.md` AC-48-15(教学口述零字幕是禁项;字幕走 AC-44-15 管线)
//   `TD-tutorial-01`(债册:四条全不一致;用户 2026-11-07 裁定甲案 = 对齐两表 + 补跨表校验)
//   门本体 = `unity/Assets/Editor.Tools.Gates/TutorialNarrationSubtitleCrossRefGate.cs`
//
// ⚠️ 本件读**真烘焙产物**(`Assets/DataCooked/48_tutorial_content.cooked.bytes` 与
//    `audio_events.cooked.bytes`),不是内联字面量 —— 因为要测的正是「两表在**真数据**上一致」,
//    内联数据会让夹具在真表分叉时仍绿(该判据的全部价值即在于此)。
//    只读,不写盘。

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using DaYiJingCheng.EditorTools.Gates;
using DaYiJingCheng.Gameplay.Presentation;
using DaYiJingCheng.Gameplay.Presentation.Audio;
using DaYiJingCheng.Gameplay.Presentation.Tutorial;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.EditMode.Tutorial
{
    [TestFixture]
    public class tutorial_narration_subtitle_crossref_test
    {
        private static readonly string RepoRoot = ComputeRepoRoot();

        /// <summary>向上找到**含 `unity/Assets/DataCooked` 的目录**作仓库根。
        /// <para>刻意不用「数 N 个 `..`」:那要求调用点与仓库根的层级固定,搬文件即静默失配
        /// (`ProjectSettings` 下已有先例)。找不到 ⇒ 直接失败,不返回一个猜的路径。</para></summary>
        private static string ComputeRepoRoot([CallerFilePath] string thisFile = "")
        {
            string dir = Path.GetDirectoryName(Path.GetFullPath(thisFile));
            while (!string.IsNullOrEmpty(dir))
            {
                if (Directory.Exists(Path.Combine(dir, "unity", "Assets", "DataCooked"))) return dir;
                dir = Path.GetDirectoryName(dir);
            }
            Assert.Fail($"找不到仓库根(自 `{thisFile}` 向上未见到 `unity/Assets/DataCooked`)");
            return null;
        }

        private static string CookedPath(string fileName)
            => Path.Combine(RepoRoot, "unity", "Assets", "DataCooked", fileName);

        private static TutorialContentTable LoadRealContent()
        {
            string path = CookedPath(TutorialContentTable.CookedFileName);
            Assert.That(File.Exists(path), Is.True, $"缺烘焙产物 `{path}` —— 先跑 DataBakeMenu");
            Assert.That(TutorialContentTable.TryDecode(File.ReadAllBytes(path), out var table, out string err),
                Is.True, $"48 内容表解码失败:{err}");
            return table;
        }

        private static AudioEventDataSet LoadRealAudio()
        {
            string path = CookedPath(AudioEventDataSet.CookedFileName);
            Assert.That(File.Exists(path), Is.True, $"缺烘焙产物 `{path}` —— 先跑 44 的烘焙");
            return AudioEventCookedCodec.Read(File.ReadAllBytes(path));
        }

        /// <summary>合成一行事件表数据(只填本判据关心的两列,其余保持「缺失」语义)。</summary>
        private static AudioEventRow Row(string cue, string subtitle)
            => new AudioEventRow(
                cue: cue, schemaVersion: 1, loop: false, bus: "Voice",
                whitelistCategory: "WorldPerceptible", triggerSource: "WorldPerceptible",
                clockRef: null, subtitleText: subtitle, xfadeRaw: null, assets: new[] { cue + ".wav" },
                tierParams: null, hasPolicy: false, policyTriggerPhaseRaw: null, policyJitterRaw: null);

        private static AudioEventDataSet Table(params AudioEventRow[] rows)
            => new AudioEventDataSet(rows, null, 1, 0);

        private static TutorialNarrationRow N(string id, string cue, string text)
            => new TutorialNarrationRow(id, cue, 1, text);

        // ══════════════ 真产物:两表一致 ══════════════

        [Test]
        public void test_realArtifacts_narrationAndSubtitleAreAligned()
        {
            TutorialContentTable content = LoadRealContent();
            AudioEventDataSet audio = LoadRealAudio();

            // 前置:两侧都非空 —— 否则下面的「零违例」是空集上的恒绿(F4 空洞正控)。
            Assert.That(content.Narration.Count, Is.GreaterThan(0), "48 零口述 ⇒ 本判据会空转通过");
            Assert.That(audio.RowCount, Is.GreaterThan(0), "44 事件表零行 ⇒ 本判据会空转通过");

            List<string> errs = TutorialNarrationSubtitleCrossRefGate.Check(content.Narration, audio);
            Assert.That(errs, Is.Empty, "两表文案不一致(或该 cue 无字幕):\n" + string.Join("\n", errs));

            // 双向确认:每条口述都真的在事件表里取到了同一个字符串(不是「都没检查」)。
            var subs = Enumerable.Range(0, audio.RowCount)
                .Select(i => audio.Row(i))
                .Where(r => !string.IsNullOrEmpty(r.Cue))
                .GroupBy(r => r.Cue)
                .ToDictionary(g => g.Key, g => g.First().SubtitleText);
            foreach (TutorialNarrationRow n in content.Narration)
            {
                Assert.That(subs.ContainsKey(n.CueId), Is.True, $"事件表缺 cue「{n.CueId}」");
                Assert.That(subs[n.CueId], Is.EqualTo(n.Text), $"cue「{n.CueId}」两表文案不一致");
            }
        }

        // ══════════════ 负向:每一类违例各一条 ══════════════

        [Test]
        public void test_detect_textMismatch_reportsBothSides()
        {
            var errs = TutorialNarrationSubtitleCrossRefGate.Check(
                new[] { N("n1", "Narration_TeachObserve_01", "先看看这个人。") },
                Table(Row("Narration_TeachObserve_01", "喘着气,气色、神态,都在脸上。")));

            Assert.That(errs, Has.Count.EqualTo(1));
            StringAssert.Contains("[C3]", errs[0]);
            StringAssert.Contains("先看看这个人。", errs[0]);            // 48 侧原文
            StringAssert.Contains("喘着气", errs[0]);                    // 事件表侧原文
        }

        [Test]
        public void test_detect_missingCue_reportsNoSubtitlePath()
        {
            var errs = TutorialNarrationSubtitleCrossRefGate.Check(
                new[] { N("n1", "Narration_TeachObserve_01", "x") },
                Table(Row("Some_Other_Cue", "y")));

            Assert.That(errs, Has.Count.EqualTo(1));
            StringAssert.Contains("[C1]", errs[0]);
        }

        [Test]
        public void test_detect_missingSubtitleField_isRed()
        {
            var errs = TutorialNarrationSubtitleCrossRefGate.Check(
                new[] { N("n1", "Narration_TeachObserve_01", "x") },
                Table(Row("Narration_TeachObserve_01", null)));

            Assert.That(errs, Has.Count.EqualTo(1));
            StringAssert.Contains("[C2]", errs[0]);
        }

        [Test]
        public void test_detect_emptySubtitle_isRed()
        {
            var errs = TutorialNarrationSubtitleCrossRefGate.Check(
                new[] { N("n1", "Narration_TeachObserve_01", "x") },
                Table(Row("Narration_TeachObserve_01", "")));

            Assert.That(errs, Has.Count.EqualTo(1));
            StringAssert.Contains("[C2]", errs[0]);
        }

        [Test]
        public void test_detect_duplicateCue_isRed()
        {
            var errs = TutorialNarrationSubtitleCrossRefGate.Check(
                new[] { N("n1", "Narration_TeachObserve_01", "x") },
                Table(
                    Row("Narration_TeachObserve_01", "x"),
                    Row("Narration_TeachObserve_01", "x")));

            Assert.That(errs.Any(e => e.Contains("[G3]")), Is.True, string.Join("\n", errs));
        }

        // ══════════════ 空集守卫(空洞正控的两侧)══════════════

        [Test]
        public void test_emptyNarration_isRed_notVacuousGreen()
        {
            var errs = TutorialNarrationSubtitleCrossRefGate.Check(
                new TutorialNarrationRow[0], Table(Row("Narration_TeachObserve_01", "x")));

            Assert.That(errs, Has.Count.EqualTo(1));
            StringAssert.Contains("[G1]", errs[0]);
        }

        [Test]
        public void test_emptyAudioTable_isRed_notVacuousGreen()
        {
            var errs = TutorialNarrationSubtitleCrossRefGate.Check(
                new[] { N("n1", "Narration_TeachObserve_01", "x") }, Table());

            Assert.That(errs, Has.Count.EqualTo(1));
            StringAssert.Contains("[G2]", errs[0]);
        }

        // ══════════════ 正控:一致的一对必须**零违例** ══════════════

        [Test]
        public void test_positiveControl_matchingPair_isGreen()
        {
            var errs = TutorialNarrationSubtitleCrossRefGate.Check(
                new[] { N("n1", "Narration_TeachObserve_01", "同一句话") },
                Table(Row("Narration_TeachObserve_01", "同一句话"), Row("Breath", "别的")));

            Assert.That(errs, Is.Empty,
                "一致的一对必须零违例 —— 否则上面所有负向都是「永远红」,判据失去判别力");
        }
    }
}
