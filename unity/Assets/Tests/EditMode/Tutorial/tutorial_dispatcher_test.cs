// S25-4 · tutorial-004 —— 三媒体调度的判据(AC-1 键空间 / AC-5 口述必带字幕 / AC-3 37 零教学分支)
//
// 权威来源:
//   · `design/gdd/tutorial-and-onboarding.md` 规则二(三种媒体)· 规则七(48 只调度)
//     · **AC-48-15**(教学口述零字幕 = 禁项)· **AC-48-10**(37 零教学分支)
//   · Story: production/epics/tutorial-onboarding/story-004-three-media-scheduling-paper-modal.md
//
// ⚠️ **只读外部夹具的具名声明**(承 `.claude/rules/test-standards.md` 2026-10-30 条):
//    本件读 `unity/Assets/DataCooked/48_tutorial_content.cooked.bytes`。为什么必须读真表:
//    「键全部来自 `.cooked`、无硬编码」这条 AC **只有对着真表才能判** —— 内联一份键表
//    会让「表改了夹具仍绿」,正好是这条 AC 要防的形态。
//
// ⚠️ 本件**不测时序**:延迟 / 并发数归用户手感轮;调度器只保证**顺序**可复现(见类注)。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DaYiJingCheng.Gameplay.Presentation.Tutorial;
using DaYiJingCheng.Gameplay.UI.Tutorial;
using DaYiJingCheng.Sim;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.EditMode.Tutorial
{
    [TestFixture]
    public class tutorial_dispatcher_test
    {
        private const int PlayerA = 51;
        private const int PlayerB = 52;

        private static readonly string RepoRoot = ComputeRepoRoot();

        private TutorialContentTable _content;

        private static string ComputeRepoRoot(
            [System.Runtime.CompilerServices.CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(thisFile) ?? ".", "..", "..", "..", "..", ".."));

        [SetUp]
        public void SetUp() => _content = LoadRealTable();

        private static TutorialContentTable LoadRealTable()
        {
            string path = Path.Combine(RepoRoot, "unity", "Assets", "DataCooked",
                TutorialContentTable.CookedFileName);
            Assert.That(File.Exists(path), Is.True, $"缺烘焙产物 `{path}`");
            Assert.That(TutorialContentTable.TryDecode(File.ReadAllBytes(path), out var table, out string err),
                Is.True, $"48 内容表解码失败:{err}");
            Assert.That(table.Narration.Count, Is.GreaterThan(0), "表里零口述 ⇒ 本判据会空转通过(假绿)");
            return table;
        }

        private TutorialDispatcher NewDispatcher(int playerId = PlayerA)
            => new TutorialDispatcher(playerId, _content);

        // ══════════════ AC-1:键全部来自 .cooked,零硬编码 ══════════════

        [Test]
        public void test_ac1_eachStepsNarrationCue_comesFromTheTable()
        {
            TutorialDispatcher dispatcher = NewDispatcher();

            foreach (TutorialStepRow step in _content.Steps)
            {
                IReadOnlyList<TutorialMediaIntent> intents = dispatcher.Dispatch(new[] { step.Id });
                var narrations = intents.Where(i => i.Kind == TutorialMediaKind.Narration).ToList();

                // 期望值直接从表算,不写字面量 —— 表改了这里跟着变
                var expectedCues = step.NarrationIds
                    .Select(id => _content.Narration.FirstOrDefault(r => r.Id == id))
                    .Where(r => r.Id != null && !string.IsNullOrEmpty(r.CueId))
                    .Select(r => r.CueId)
                    .OrderBy(c => c, StringComparer.Ordinal)
                    .ToList();

                CollectionAssert.AreEquivalent(expectedCues,
                    narrations.Select(n => n.CueId).OrderBy(c => c, StringComparer.Ordinal).ToList(),
                    $"步 {step.Id} 的口述 cue 应与内容表一行不差(无硬编码、无自造键)");
            }
        }

        [Test]
        public void test_ac1_allEmittedKeysExistInTheTable()
        {
            // 六步全发火 —— 一次把三类键都逼出来
            var allSteps = _content.Steps.Select(s => s.Id).ToArray();
            IReadOnlyList<TutorialMediaIntent> intents = NewDispatcher().Dispatch(allSteps);

            Assert.That(intents, Is.Not.Empty, "六步全发火却零意图 —— 判据会空转通过");

            var narrationIds = new HashSet<string>(_content.Narration.Select(r => r.CueId), StringComparer.Ordinal);
            var clipKeys = new HashSet<string>(_content.Demonstrations.Select(r => r.ClipKey), StringComparer.Ordinal);
            var paperIds = new HashSet<string>(_content.Papers.Select(r => r.Id), StringComparer.Ordinal);

            foreach (TutorialMediaIntent i in intents)
            {
                switch (i.Kind)
                {
                    case TutorialMediaKind.Narration:
                        Assert.That(narrationIds.Contains(i.CueId), Is.True, $"cue「{i.CueId}」不在表里");
                        break;
                    case TutorialMediaKind.Demonstration:
                        Assert.That(clipKeys.Contains(i.SegmentKey), Is.True, $"clip「{i.SegmentKey}」不在表里");
                        break;
                    case TutorialMediaKind.Paper:
                        Assert.That(paperIds.Contains(i.PaperId), Is.True, $"纸「{i.PaperId}」不在表里");
                        break;
                }
            }
        }

        [Test]
        public void test_ac1_fireOrderDoesNotAffectOutputOrder()
        {
            var forward = _content.Steps.Select(s => s.Id).ToArray();
            var reversed = forward.Reverse().ToArray();

            var a = NewDispatcher().Dispatch(forward).Select(Describe).ToArray();
            var b = NewDispatcher().Dispatch(reversed).Select(Describe).ToArray();

            CollectionAssert.AreEqual(a, b,
                "输出按**内容表步序**排列,与调用方给的 Fire 次序无关(纯函数;事件到达次序不得渗进呈现)");
        }

        /// <summary>
        /// 绝对次序:输出按**内容表步序**排,不是任何别的序。
        /// <para>⚠️ 上一条只断言「换个 Fire 次序输出不变」—— 那是**不变性**,遍历方向反一下两边
        /// 一起反,判据恒真(2026-11-06 证伪轮 m1 **全绿逃逸**)。本条钉**绝对次序**。</para>
        /// </summary>
        [Test]
        public void test_ac1_stepOrder_isTheContentTableOrder()
        {
            var allSteps = _content.Steps.Select(s => s.Id).ToArray();

            // 故意逆序喂 —— 输出仍须是表序
            IReadOnlyList<TutorialMediaIntent> intents = NewDispatcher().Dispatch(allSteps.Reverse().ToArray());
            Assert.That(intents, Is.Not.Empty, "零意图 —— 判据会空转通过");

            var emitted = new List<string>();
            foreach (TutorialMediaIntent i in intents)
                if (emitted.Count == 0 || emitted[emitted.Count - 1] != i.StepId) emitted.Add(i.StepId);

            // 期望 = 表序里「真的产出了媒体」的那些步(零媒体的步不出现,不是错误)
            var expected = _content.Steps.Select(s => s.Id).Where(emitted.Contains).ToArray();

            Assert.That(emitted.Count, Is.GreaterThan(1), "只出现一步 —— 步间抗不了序,判据会空转通过");
            CollectionAssert.AreEqual(expected, emitted,
                "步序不是内容表序 —— 绝对次序必须与表一致(不只「对输入次序不变」)");
        }

        /// <summary>
        /// 步内口述次序:**`Order` 升序,同 `Order` 按 id 序**。
        /// <para>⚠️ 合成行是必需的:真表**每步至多 1 条口述** ⇒ 走调度器永远碰不到排序分支
        /// (2026-11-06 证伪轮 m2 **全绿逃逸**,原因是「靶场里没有靶」而非断言写松)。</para>
        /// <para>合成 id 刻意让 **id 序与 order 序相反** —— 否则「只按 id 排」的变异照样绿。</para>
        /// </summary>
        [Test]
        public void test_ac1_narrationWithinStep_orderedByOrderThenId()
        {
            var rows = new[]
            {
                new TutorialNarrationRow("m_tie", "cue_M", 1, "同 order,按 id 排在 b 之后"),
                new TutorialNarrationRow("a_first_byId", "cue_A", 2, "order 最大,id 最小"),
                new TutorialNarrationRow("z_last_byId", "cue_Z", 1, "order 最小,id 最大"),
                new TutorialNarrationRow("b_tie", "cue_B", 1, "同 order,按 id 排在最前"),
            };

            IReadOnlyList<TutorialNarrationRow> ordered = TutorialDispatcher.OrderNarrations(rows);

            CollectionAssert.AreEqual(
                new[] { "cue_B", "cue_M", "cue_Z", "cue_A" },
                ordered.Select(r => r.CueId).ToArray(),
                "步内次序 = Order 升序 + 同 Order 按 id 序;只按 id 排会让 order 字段形同虚设");

            Assert.That(rows[0].Id, Is.EqualTo("m_tie"), "入参顺序不得被就地打乱(调用方的表序是只读的)");
            Assert.That(TutorialDispatcher.OrderNarrations(null), Is.Empty);
        }

        [Test]
        public void test_ac1_duplicateFiredIds_doNotDuplicateIntents()
        {
            var once = NewDispatcher().Dispatch(new[] { "judge" }).Select(Describe).ToArray();
            var twice = NewDispatcher().Dispatch(new[] { "judge", "judge" }).Select(Describe).ToArray();

            CollectionAssert.AreEqual(once, twice, "同一步在一次调用里重复出现,不得重复出意图");
        }

        [Test]
        public void test_ac1_emptyOrNullFired_returnsEmpty()
        {
            Assert.That(NewDispatcher().Dispatch(Array.Empty<string>()), Is.Empty);
            Assert.That(NewDispatcher().Dispatch(null), Is.Empty);
        }

        // ══════════════ AC-5:口述必带字幕(零「只出声不出字」路径)══════════════

        [Test]
        public void test_ac5_everyNarrationIntent_carriesSubtitleText()
        {
            var allSteps = _content.Steps.Select(s => s.Id).ToArray();
            IReadOnlyList<TutorialMediaIntent> intents = NewDispatcher().Dispatch(allSteps);
            var narrations = intents.Where(i => i.Kind == TutorialMediaKind.Narration).ToList();

            Assert.That(narrations, Is.Not.Empty, "零口述 ⇒ 判据空转");

            foreach (TutorialMediaIntent n in narrations)
                Assert.That(string.IsNullOrWhiteSpace(n.Text), Is.False,
                    $"cue「{n.CueId}」没有字幕文本 —— AC-48-15 的禁项(教学口述零字幕)");
        }

        [Test]
        public void test_ac5_subtitleText_comesFromTheSameRowAsTheCue()
        {
            var allSteps = _content.Steps.Select(s => s.Id).ToArray();
            var narrations = NewDispatcher().Dispatch(allSteps)
                .Where(i => i.Kind == TutorialMediaKind.Narration).ToList();

            foreach (TutorialMediaIntent n in narrations)
            {
                TutorialNarrationRow row = _content.Narration.FirstOrDefault(r => r.CueId == n.CueId);
                Assert.That(row.Id, Is.Not.Null, $"cue「{n.CueId}」在表里没有行");
                Assert.That(n.Text, Is.EqualTo(row.Text),
                    "字幕文本必须与 cue **同一行**(不得两处各写一份 —— 两处即第二真源)");
            }
        }

        [Test]
        public void test_ac5_narrationCount_matchesTableRows()
        {
            // 「只出声不出字」的另一面:也不能漏发 —— 表里有几条带 cue 的口述,就应出几条
            var allSteps = _content.Steps.Select(s => s.Id).ToArray();
            int emitted = NewDispatcher().Dispatch(allSteps)
                .Count(i => i.Kind == TutorialMediaKind.Narration);

            int expected = 0;
            foreach (TutorialStepRow step in _content.Steps)
                foreach (string id in step.NarrationIds)
                {
                    TutorialNarrationRow r = _content.Narration.FirstOrDefault(x => x.Id == id);
                    if (r.Id != null && !string.IsNullOrEmpty(r.CueId)) expected++;
                }

            Assert.That(emitted, Is.EqualTo(expected),
                "口述意图数应等于「表里带 cue 的口述行」数 —— 多发是重复,漏发是静音");
        }

        // ══════════════ AC-4:双路独立(单机夹具)══════════════

        [Test]
        public void test_ac4_twoPlayers_dispatchIndependently()
        {
            IReadOnlyList<TutorialMediaIntent> a =
                new TutorialDispatcher(PlayerA, _content).Dispatch(new[] { "judge" });
            IReadOnlyList<TutorialMediaIntent> b =
                new TutorialDispatcher(PlayerB, _content).Dispatch(new[] { "manage" });

            Assert.That(a.All(i => i.StepId == "judge"), Is.True, "A 的意图只应来自 A 发火的步");
            Assert.That(b.All(i => i.StepId == "manage"), Is.True, "B 的意图只应来自 B 发火的步");
        }

        // ══════════════ 入口守卫 ══════════════

        [Test]
        public void test_guard_negativePlayerId_throws()
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(
                () => new TutorialDispatcher(TutorialEventFact.NoAuthor, _content));
            Assert.That(ex.ParamName, Is.EqualTo("playerId"));
        }

        [Test]
        public void test_guard_defaultTable_throws()
        {
            // F9(default(struct) 绕过构造器):`default` 的 `Steps` 是**空列表**不是 null,
            // 所以守卫必须判「零步」而不是判 null。
            var ex = Assert.Throws<ArgumentException>(() => new TutorialDispatcher(PlayerA, default));
            Assert.That(ex.ParamName, Is.EqualTo("content"));
        }

        // ══════════════ AC-48-10:37 代码面零教学分支 ══════════════

        [Test]
        public void test_ac48_10_simAssembly_hasNoTutorialSymbols()
        {
            // 37(病例系统)住 Sim 装配。若它长出「教学模式」分支,符号名会带这些词族。
            // ⚠️ 这是**符号面**判据,不是 grep 注释 —— 与本项目的「门不认自述」同口径。
            Assembly sim = typeof(EventStream).Assembly;
            Type[] types = sim.GetExportedTypes();
            Assert.That(types.Length, Is.GreaterThan(0), "扫描面为空 —— 判据会空转通过");

            string[] tokens = { "tutorial", "onboarding", "guide" };
            var hits = new List<string>();

            foreach (Type t in types)
            {
                Check(t.Name, t.FullName);
                foreach (var m in t.GetMembers(BindingFlags.Public | BindingFlags.NonPublic |
                                               BindingFlags.Instance | BindingFlags.Static |
                                               BindingFlags.DeclaredOnly))
                    Check(m.Name, t.Name + "." + m.Name);
            }

            Assert.That(hits, Is.Empty,
                "Sim 装配里出现教学语义词族 —— AC-48-10(37 不知道 48 存在):\n" +
                string.Join("\n", hits.Take(20)));

            void Check(string name, string where)
            {
                string lower = name.ToLowerInvariant();
                foreach (string token in tokens)
                    if (lower.Contains(token)) hits.Add($"{where}(命中「{token}」)");
            }
        }

        private static string Describe(TutorialMediaIntent i)
            => $"{i.Kind}|{i.StepId}|{i.CueId}|{i.Actor}|{i.SegmentKey}|{i.PaperId}";
    }
}
