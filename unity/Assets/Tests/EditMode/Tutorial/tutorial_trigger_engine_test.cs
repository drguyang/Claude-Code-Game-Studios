// S25-2 · tutorial-003 —— 48 两相触发引擎的判据(F-48.1 边沿 / F-48.2 独立 / AC-48-08 冷播种)
//
// 权威来源:
//   · `design/gdd/tutorial-and-onboarding.md` 规则四 / F-48.1 / F-48.2 / AC-48-08 / AC-48-09
//   · `docs/architecture/adr-009` §一 Q1(引导进度 = 派生态;闩 = 进程内易失)
//   · Story: production/epics/tutorial-onboarding/story-003-anchor-completion-trigger-engine.md
//
// ⚠️ **只读外部夹具的具名声明**(承 `.claude/rules/test-standards.md` 2026-10-30 条):
//    本件有一条端到端用例读 `unity/Assets/DataCooked/48_tutorial_content.cooked.bytes`。
//    为什么读真表:步 id 与 `predicateKind` 是**数据**,内联一份会让「表改坏了夹具仍绿」。
//
// ⚠️ 夹具构成:多数用例用**合成步表**(测边沿逻辑本身,不掺数据面);一条端到端用例用**真表 + 真投影**,
//    证明两条口接得上 S25-1 的 `TutorialProjection`。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using DaYiJingCheng.Gameplay.Presentation.Tutorial;
using DaYiJingCheng.Gameplay.UI.Tutorial;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.EditMode.Tutorial
{
    [TestFixture]
    public class tutorial_trigger_engine_test
    {
        private const int PlayerA = 41;
        private const int PlayerB = 42;

        private static readonly string RepoRoot = ComputeRepoRoot();

        private static string ComputeRepoRoot(
            [System.Runtime.CompilerServices.CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(thisFile) ?? ".", "..", "..", "..", "..", ".."));

        // ══════════════ 夹具 ══════════════

        private static readonly IReadOnlyList<TutorialStepRow> SixSteps = BuildSteps(
            "observe", "judge", "treat", "close", "archive", "manage");

        private static IReadOnlyList<TutorialStepRow> BuildSteps(params string[] ids)
            => ids.Select(id => new TutorialStepRow(id, id, id, null, null, null, null)).ToArray();

        /// <summary>合成视图:<paramref name="completed"/> 里的步为真,其余为假。</summary>
        private static TutorialProgressView View(
            int playerId, IReadOnlyList<TutorialStepRow> steps, params string[] completed)
        {
            var rows = new TutorialStepCompletion[steps.Count];
            for (int i = 0; i < steps.Count; i++)
                rows[i] = new TutorialStepCompletion(
                    steps[i].Id, steps[i].PredicateKind,
                    completed.Contains(steps[i].Id, StringComparer.Ordinal));
            return new TutorialProgressView(playerId, rows);
        }

        private static TutorialTrigger NewTrigger(
            int playerId = PlayerA, IReadOnlyList<TutorialStepRow> steps = null)
            => new TutorialTrigger(playerId, steps ?? SixSteps);

        // ══════════════ AC-48-08:冷播种不重燃 ══════════════

        [Test]
        public void test_ac48_08_seed_marksLatchWithoutFiring()
        {
            // Arrange:读档后的流已含 treat 的完成事件
            TutorialTrigger trigger = NewTrigger();
            TutorialProgressView view = View(PlayerA, SixSteps, "treat");

            // Act:播种(冷启动,读档完成 → 首帧前)
            trigger.Seed(view);

            // Assert:完成判据立真,但**一次都不发火**(媒体不重播)
            Assert.That(trigger.HasFired("treat"), Is.True, "播种后该步应已在闩上(视同已发生)");
            Assert.That(trigger.FireCount, Is.EqualTo(0),
                "播种口**不得**跑 Fire 路径 —— 否则老玩家读档会重播教过的内容(F-48.1 冷启动)");
        }

        [Test]
        public void test_ac48_08_afterSeed_newCompletionFiresOnlyItself()
        {
            TutorialTrigger trigger = NewTrigger();
            trigger.Seed(View(PlayerA, SixSteps, "treat"));

            // Act:读档后又结了一个案
            IReadOnlyList<string> fired = trigger.Evaluate(View(PlayerA, SixSteps, "treat", "close"));

            // Assert:只发 close;treat 不连带复燃
            CollectionAssert.AreEqual(new[] { "close" }, fired.ToArray());
            Assert.That(trigger.FireCount, Is.EqualTo(1));
        }

        [Test]
        public void test_ac48_08_seedTwice_isIdempotent_noFire()
        {
            TutorialTrigger trigger = NewTrigger();
            trigger.Seed(View(PlayerA, SixSteps, "treat"));
            trigger.Seed(View(PlayerA, SixSteps, "treat", "close"));

            Assert.That(trigger.FireCount, Is.EqualTo(0), "播种幂等 —— 无论调用几次都不发火");
        }

        // ══════════════ 边沿唯一性 ══════════════

        [Test]
        public void test_ac2_sameViewEvaluatedTwice_firesOnlyOnce()
        {
            TutorialTrigger trigger = NewTrigger();
            TutorialProgressView view = View(PlayerA, SixSteps, "judge");

            IReadOnlyList<string> first = trigger.Evaluate(view);
            IReadOnlyList<string> second = trigger.Evaluate(view);   // 事件回灌 / 重放

            CollectionAssert.AreEqual(new[] { "judge" }, first.ToArray());
            Assert.That(second, Is.Empty, "同一判据不得二次发火(边沿 = 首真那一刻)");
            Assert.That(trigger.FireCount, Is.EqualTo(1));
        }

        [Test]
        public void test_ac2_olderViewAfterNewer_doesNotRefireNorUnlatch()
        {
            TutorialTrigger trigger = NewTrigger();
            trigger.Evaluate(View(PlayerA, SixSteps, "judge", "close"));

            // 回灌一个更旧的视图(投影只会增;这里是防御性构造)
            IReadOnlyList<string> fired = trigger.Evaluate(View(PlayerA, SixSteps, "judge"));

            Assert.That(fired, Is.Empty, "视图回退不得再发火");
            Assert.That(trigger.HasFired("close"), Is.True, "闩只进不退(单调)");
        }

        // ══════════════ F-48.2:可跳步 / 可回头 / 不补播 ══════════════

        [Test]
        public void test_ac3_skipSteps_onlyCompletedOneFires()
        {
            TutorialTrigger trigger = NewTrigger();

            // 玩家直接建了房(manage),从没落笔
            IReadOnlyList<string> fired = trigger.Evaluate(View(PlayerA, SixSteps, "manage"));

            CollectionAssert.AreEqual(new[] { "manage" }, fired.ToArray(),
                "可跳步 —— 后步先完成合法,不报错不惩罚(F-48.2)");
        }

        [Test]
        public void test_ac3_comeBackLater_firesThatStepOnly()
        {
            TutorialTrigger trigger = NewTrigger();
            trigger.Evaluate(View(PlayerA, SixSteps, "manage"));

            // 回头补做落笔 ⇒ 只发 judge,不补播 manage
            IReadOnlyList<string> fired = trigger.Evaluate(View(PlayerA, SixSteps, "manage", "judge"));

            CollectionAssert.AreEqual(new[] { "judge" }, fired.ToArray(),
                "可回头 —— 补做的那一步照常发火,已完成的不重播");
        }

        [Test]
        public void test_ac3_noCompensationOutput_forSkippedSteps()
        {
            TutorialTrigger trigger = NewTrigger();
            trigger.Evaluate(View(PlayerA, SixSteps, "manage"));

            // 引擎面的唯一输出 = step id;不存在「请先做 X」这类文案/惩罚信号
            Assert.That(trigger.HasFired("judge"), Is.False);
            Assert.That(trigger.HasFired("observe"), Is.False);
            Assert.That(trigger.HasFired("treat"), Is.False);
            Assert.That(trigger.HasFired("close"), Is.False);
            Assert.That(trigger.HasFired("archive"), Is.False);
            Assert.That(trigger.FireCount, Is.EqualTo(1));
        }

        // ══════════════ 双玩家分路 ══════════════

        [Test]
        public void test_ac4_twoPlayers_haveIndependentLatches()
        {
            TutorialTrigger a = NewTrigger(PlayerA);
            TutorialTrigger b = NewTrigger(PlayerB);

            a.Evaluate(View(PlayerA, SixSteps, "judge"));
            b.Evaluate(View(PlayerB, SixSteps));            // B 什么都没做

            Assert.That(a.HasFired("judge"), Is.True);
            Assert.That(b.HasFired("judge"), Is.False);
            Assert.That(b.FireCount, Is.EqualTo(0));
        }

        [Test]
        public void test_guard_crossPlayerView_throws()
        {
            TutorialTrigger a = NewTrigger(PlayerA);
            var ex = Assert.Throws<ArgumentOutOfRangeException>(
                () => a.Evaluate(View(PlayerB, SixSteps, "judge")));
            Assert.That(ex.ParamName, Is.EqualTo("view"),
                "把 B 的投影喂给 A 的引擎 ⇒ 会把别人的进度当成自己的播出去,必须 fail-loud");
            Assert.That(a.FireCount, Is.EqualTo(0), "抛之前不得发火(副作用为零)");
        }

        // ══════════════ AC-5:闩不可序列化 + 两口分家 ══════════════

        [Test]
        public void test_ac5_typeCarriesNoSerializableLatch()
        {
            Type t = typeof(TutorialTrigger);

            Assert.That(t.IsSerializable, Is.False,
                "触发引擎**不得**标 [Serializable] —— 闩一旦可序列化就会跟着存档走(AC-48-03 的反面)");
            Assert.That(typeof(ISerializable).IsAssignableFrom(t), Is.False,
                "不得实现 ISerializable");

            foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public |
                                          BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                Assert.That(f.IsPublic, Is.False, $"公开字段「{f.Name}」暴露了闩状态");
                Assert.That(f.GetCustomAttributes(typeof(UnityEngine.SerializeField), true), Is.Empty,
                    $"字段「{f.Name}」带 [SerializeField] —— 闩会被编辑器序列化");
            }
        }

        [Test]
        public void test_ac5_seedAndEvaluate_areDistinctMethods()
        {
            MethodInfo seed = typeof(TutorialTrigger).GetMethod("Seed");
            MethodInfo evaluate = typeof(TutorialTrigger).GetMethod("Evaluate");

            Assert.That(seed, Is.Not.Null);
            Assert.That(evaluate, Is.Not.Null);
            Assert.That(seed, Is.Not.SameAs(evaluate), "播种口与增量口**不得**共用同一个方法");
            Assert.That(seed.ReturnType, Is.EqualTo(typeof(void)), "播种口不出意图(它只置饱和)");
            Assert.That(evaluate.ReturnType, Is.EqualTo(typeof(IReadOnlyList<string>)));
        }

        [Test]
        public void test_ac5_seedMouthNeverEmitsIntent()
        {
            TutorialTrigger trigger = NewTrigger();

            // 往播种口投一个「全部完成」的视图 —— 该口只置饱和,不产生任何一步的边沿
            trigger.Seed(View(PlayerA, SixSteps, SixSteps.Select(s => s.Id).ToArray()));

            Assert.That(trigger.FireCount, Is.EqualTo(0));
            // 之后再走增量口也不发火(全部已在闩上)
            Assert.That(trigger.Evaluate(View(PlayerA, SixSteps, SixSteps.Select(s => s.Id).ToArray())),
                Is.Empty);
        }

        [Test]
        public void test_ac5_publicSurface_hasNoGateMembers()
        {
            // 零设门的**引擎面**判据:公开成员恰为本类该有的那几个;多出「解锁 / 禁用 / 拦截」即红。
            // ⚠️ 设门守卫本体(反射扫 48 全 API 面)= Story 005;本条只管本类。
            var publicMembers = typeof(TutorialTrigger)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName)
                .Select(m => m.Name)
                .Concat(typeof(TutorialTrigger)
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Select(p => p.Name))
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToArray();

            CollectionAssert.AreEqual(
                new[] { "Evaluate", "FireCount", "HasFired", "PlayerId", "Seed", "StepCount" },
                publicMembers,
                "触发引擎的公开面变了 —— 若新增的是「解锁 / 禁用 / 拦截」类成员,那就是设门(规则五)");
        }

        // ══════════════ 入口守卫 ══════════════

        [Test]
        public void test_guard_nullSteps_throws()
        {
            var ex = Assert.Throws<ArgumentNullException>(() => new TutorialTrigger(PlayerA, null));
            Assert.That(ex.ParamName, Is.EqualTo("steps"));
        }

        [Test]
        public void test_guard_negativePlayerId_throws()
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(
                () => new TutorialTrigger(TutorialEventFact.NoAuthor, SixSteps));
            Assert.That(ex.ParamName, Is.EqualTo("playerId"));
        }

        [Test]
        public void test_emptyStepSet_neverFires()
        {
            TutorialTrigger trigger = NewTrigger(PlayerA, BuildSteps());
            Assert.That(trigger.Evaluate(View(PlayerA, BuildSteps())), Is.Empty);
            Assert.That(trigger.FireCount, Is.EqualTo(0));
        }

        // ══════════════ 端到端:真表 + 真投影 ══════════════

        [Test]
        public void test_e2e_realProjection_firesOnFirstTrueEdge()
        {
            IReadOnlyList<TutorialStepRow> realSteps = LoadRealSteps();

            var pool = new InMemoryBlobPool();
            var encoder = new PayloadEncoder(pool);
            var decoder = new PayloadDecoder(pool);

            // 空流 ⇒ 六步全假 ⇒ 一次都不发
            var emptyFeed = new TutorialStreamFeed(() => Array.Empty<SimEvent>(), decoder);
            TutorialProgressView empty = TutorialProjection.Project(emptyFeed, realSteps, PlayerA);

            var trigger = new TutorialTrigger(PlayerA, realSteps);
            trigger.Seed(empty);                       // 冷启动播种(空流,等价于全新局)
            Assert.That(trigger.FireCount, Is.EqualTo(0));

            // 之后来了一条落笔事件 ⇒ 只有 judge 的首真边沿发火
            SimEvent judgment = new SimEvent(10L, new PatientId(7), -1L, EventKind.JudgmentRecorded,
                encoder.EncodeJudgmentRecorded(
                    new JudgmentRecordedPayload(7, new CaseId(10L, 7, 0), PlayerA, 1, 1), string.Empty));
            var feed = new TutorialStreamFeed(() => new[] { judgment }, decoder);
            TutorialProgressView after = TutorialProjection.Project(feed, realSteps, PlayerA);

            CollectionAssert.AreEqual(new[] { "judge" }, trigger.Evaluate(after).ToArray(),
                "真表下 judge 步的 predicateKind 是 JudgmentRecorded —— 边沿应恰好落在它上面");
        }

        private static IReadOnlyList<TutorialStepRow> LoadRealSteps()
        {
            string path = Path.Combine(RepoRoot, "unity", "Assets", "DataCooked",
                TutorialContentTable.CookedFileName);
            Assert.That(File.Exists(path), Is.True, $"缺烘焙产物 `{path}`");
            Assert.That(TutorialContentTable.TryDecode(File.ReadAllBytes(path), out var table, out string err),
                Is.True, $"48 内容表解码失败:{err}");
            return table.Steps;
        }
    }
}
