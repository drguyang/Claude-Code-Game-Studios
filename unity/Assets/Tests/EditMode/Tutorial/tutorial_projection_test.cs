// S25-1 · tutorial-002 —— 48 只读投影 `Proj` 的判据(AC-48-03 / AC-48-09 / F-48.1 六步映射)
//
// 权威来源:
//   · `design/gdd/tutorial-and-onboarding.md` 规则四 / F-48.1 / F-48.2 / AC-48-03 / AC-48-08 / AC-48-09
//   · `docs/architecture/adr-009` §一 Q1(引导进度 = 派生态,不进流)
//   · `docs/architecture/adr-019` §三(边界层只读消费者)
//   · Story: production/epics/tutorial-onboarding/story-002-…(AC-1…AC-5 的自动化半边)
//
// ⚠️ **只读外部夹具的具名声明**(承 `.claude/rules/test-standards.md` 2026-10-30 条):
//    本件**读一份仓库内产物**:`unity/Assets/DataCooked/48_tutorial_content.cooked.bytes`。
//    为什么必须读真表而不是内联一份六步:六步的 `predicateKind` 是**数据**,
//    内联会让「表里把 judge 写成 CaseOpened」这类错误在夹具里看不见(表改坏了夹具仍绿)。
//    判据因此钉在**真烘焙产物**上;表缺失 / 解码失败 ⇒ 红(不静默跳过)。
//
// ⚠️ 本件**不**造 Fake 顶替生产类:`TutorialSnapshotBuilder` / `TutorialStreamFeed` /
//    `TutorialProjection` 都是生产件,本件直接打它们(承假绿形态 F2 的教训)。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Presentation.Tutorial;
using DaYiJingCheng.Gameplay.UI.Tutorial;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.EditMode.Tutorial
{
    [TestFixture]
    public class tutorial_projection_test
    {
        private const int PatientA = 11;
        private const int PatientB = 12;
        private const int PlayerA = 31;
        private const int PlayerB = 32;

        private static readonly string RepoRoot = ComputeRepoRoot();

        private InMemoryBlobPool _pool;
        private PayloadEncoder _encoder;
        private PayloadDecoder _decoder;
        private IReadOnlyList<TutorialStepRow> _steps;

        private static string ComputeRepoRoot(
            [System.Runtime.CompilerServices.CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(thisFile) ?? ".", "..", "..", "..", "..", ".."));

        [SetUp]
        public void SetUp()
        {
            _pool = new InMemoryBlobPool();
            _encoder = new PayloadEncoder(_pool);
            _decoder = new PayloadDecoder(_pool);
            _steps = LoadRealSteps();
        }

        // ══════════════ 真内容表 ══════════════

        /// <summary>读真烘焙产物(<c>48_tutorial_content.cooked.bytes</c>)并断言六步齐备。</summary>
        private static IReadOnlyList<TutorialStepRow> LoadRealSteps()
        {
            string path = Path.Combine(RepoRoot, "unity", "Assets", "DataCooked",
                TutorialContentTable.CookedFileName);
            Assert.That(File.Exists(path), Is.True,
                $"缺烘焙产物 `{path}` —— 48 的内容表未烘?本判据拒绝在缺失时静默跳过(那会变成空转绿)");

            Assert.That(TutorialContentTable.TryDecode(File.ReadAllBytes(path), out var table, out string err),
                Is.True, $"48 内容表解码失败:{err}");

            Assert.That(table.Steps.Count, Is.EqualTo(6),
                "六步闭集(observe / judge / treat / close / archive / manage)—— 表行数变了,判据须同步");
            return table.Steps;
        }

        // ══════════════ 事件构造 ══════════════

        private SimEvent CaseOpened(long tick, int patient)
            => new SimEvent(tick, new PatientId(patient), -1L, EventKind.CaseOpened,
                _encoder.Encode(EventKind.CaseOpened,
                    new CaseOpenedPayload(patient, new DiseaseIdSet(1UL << 0))));

        private SimEvent CaseClosed(long tick, int patient)
            => new SimEvent(tick, new PatientId(patient), -1L, EventKind.CaseClosed,
                _encoder.Encode(EventKind.CaseClosed,
                    new CaseClosedPayload(patient, new CaseId(tick, patient, 0),
                        new DiseaseIdSet(1UL << 0), true)));

        /// <remarks>⚠️ `JudgmentRecorded` **不走**泛型 `Encode&lt;T&gt;` —— 它另有 `freehandText`
        /// 变长段(ADR-006 G-3),泛型路径**显式抛** `NotSupportedException`。
        /// 必须走具名重载 `EncodeJudgmentRecorded`(2026-11-06 首轮实测踩到)。</remarks>
        private SimEvent Judgment(long tick, int patient, int authorPlayer)
        {
            var payload = new JudgmentRecordedPayload(
                patient, new CaseId(tick, patient, 0), authorPlayer, 1, 1);
            return new SimEvent(tick, new PatientId(patient), -1L, EventKind.JudgmentRecorded,
                _encoder.EncodeJudgmentRecorded(in payload, string.Empty));
        }

        private SimEvent EmergencyAttempt(long tick, int patient, int actorPlayer)
            => new SimEvent(tick, new PatientId(patient), -1L, EventKind.EmergencyAttempt,
                _encoder.Encode(EventKind.EmergencyAttempt,
                    new EmergencyAttemptPayload(action: 0, holdTicks: 0, edges: 0, magPeak: 0,
                        magLast: 0, method: 0, actorId: actorPlayer, edgeTicks: Array.Empty<int>())));

        private SimEvent DrugTreatment(long tick, int patient, int actorPlayer)
            => new SimEvent(tick, new PatientId(patient), -1L, EventKind.DrugTreatmentApplied,
                _encoder.Encode(EventKind.DrugTreatmentApplied,
                    new DrugTreatmentAppliedPayload(tick, treatmentId: 0, actorId: actorPlayer,
                        polarity: 0, drugPotency: Fix.Zero, halfLife: 1L, seq: 0L,
                        contraindicationBand: DrugTreatmentAppliedPayload.NoContraindication)));

        private SimEvent StructurePlaced(long tick)
            => new SimEvent(tick, PatientId.None, -1L, EventKind.StructurePlaced,
                _encoder.Encode(EventKind.StructurePlaced,
                    new StructurePlacedPayload(1L, new WorldPos(1, 0, 1), moduleId: 0,
                        orientation: 0, variant: 0)));

        // ══════════════ 投影入口 ══════════════

        private TutorialProgressView Project(int playerId, bool checkpointWritten, params SimEvent[] events)
            => TutorialProjection.Project(
                new TutorialStreamFeed(() => events, _decoder, () => checkpointWritten),
                _steps, playerId);

        private static string[] CompletedSteps(TutorialProgressView view)
            => view.Steps.Where(s => s.Completed).Select(s => s.StepId).ToArray();

        // ══════════════ AC-5:六谓词的判据事件映射(逐条 iff)══════════════

        [Test]
        public void test_ac5_eachPredicateEvent_completesOnlyItsOwnStep()
        {
            // Arrange:六种「判据来源」各一条 —— 五种流事件 + 一种回执。
            var cases = new (string Label, Func<SimEvent[]> Make, bool Checkpoint, string Expected)[]
            {
                ("CaseOpened",           () => new[] { CaseOpened(10L, PatientA) },            false, "observe"),
                ("JudgmentRecorded(p)",  () => new[] { Judgment(11L, PatientA, PlayerA) },     false, "judge"),
                ("DrugTreatmentApplied", () => new[] { DrugTreatment(12L, PatientA, PlayerA) },false, "treat"),
                ("CaseClosed",           () => new[] { CaseClosed(13L, PatientA) },            false, "close"),
                ("Checkpoint 回执",       () => Array.Empty<SimEvent>(),                        true,  "archive"),
                ("StructurePlaced",      () => new[] { StructurePlaced(15L) },                 false, "manage"),
            };

            foreach (var c in cases)
            {
                // Act
                TutorialProgressView view = Project(PlayerA, c.Checkpoint, c.Make());

                // Assert:只有对应那一步为真(iff,不是「至少」)
                CollectionAssert.AreEqual(new[] { c.Expected }, CompletedSteps(view),
                    $"{c.Label} 应只推进「{c.Expected}」一步;实际 = " +
                    $"{(CompletedSteps(view).Length == 0 ? "（无）" : string.Join(",", CompletedSteps(view)))}");
            }
        }

        [Test]
        public void test_ac5_emergencyAttempt_alsoCompletesTreatStep()
        {
            // F-48.1 第 ③ 步的判据是**析取**(EmergencyAttempt / DrugTreatmentApplied),
            // 而内容表 predicateKind 是单值(种子取 DrugTreatmentApplied)⇒ 另一支必须仍然成立。
            TutorialProgressView view = Project(PlayerA, false, EmergencyAttempt(20L, PatientA, PlayerA));

            Assert.That(view.IsCompleted("treat"), Is.True,
                "F-48.1 ③ = 「EmergencyAttempt / DrugTreatmentApplied」析取;只做急救不做药也算学过节手段");
        }

        [Test]
        public void test_ac5_predicateKindOfEachStep_matchesTheContentTable()
        {
            // 数据面判据:表里的 predicateKind 与投影认的键是**同一个字符串来源**
            // (EventKind 成员名)。表若改名,本断言与 AC-5 那条同时转红。
            var actual = _steps.ToDictionary(s => s.Id, s => s.PredicateKind);
            Assert.That(actual["observe"], Is.EqualTo("CaseOpened"));
            Assert.That(actual["judge"], Is.EqualTo("JudgmentRecorded"));
            Assert.That(actual["treat"], Is.EqualTo("DrugTreatmentApplied"));
            Assert.That(actual["close"], Is.EqualTo("CaseClosed"));
            Assert.That(actual["archive"], Is.EqualTo(TutorialProjection.CheckpointPredicateKind));
            Assert.That(actual["manage"], Is.EqualTo("StructurePlaced"));
        }

        // ══════════════ AC-5 边界:作者过滤 ══════════════

        [Test]
        public void test_ac5_judgmentByOtherPlayer_doesNotCompleteMyStep()
        {
            SimEvent e = Judgment(30L, PatientA, PlayerB);

            Assert.That(Project(PlayerA, false, e).IsCompleted("judge"), Is.False,
                "别人落笔不能算我学会(按 author_player_id 过滤)");
            Assert.That(Project(PlayerB, false, e).IsCompleted("judge"), Is.True,
                "作者本人应看到该步完成");
        }

        [Test]
        public void test_ac5_worldLevelEvent_completesForEveryPlayer()
        {
            // F-48.1 的 ① / ④ / ⑥ 无作者轴(载荷里没有玩家 id)⇒ 对每名玩家都成立。
            // 这是**口径**,不是遗漏:AC-48-09 只要求带作者轴的谓词各自独立。
            SimEvent e = CaseOpened(31L, PatientA);

            Assert.That(Project(PlayerA, false, e).IsCompleted("observe"), Is.True);
            Assert.That(Project(PlayerB, false, e).IsCompleted("observe"), Is.True);
        }

        [Test]
        public void test_ac4_twoPlayersAuthoredEvents_stayIndependent()
        {
            // Arrange:同一流里 p1 落笔、p2 用药
            SimEvent[] stream = { Judgment(40L, PatientA, PlayerA), DrugTreatment(41L, PatientA, PlayerB) };

            // Act
            TutorialProgressView p1 = Project(PlayerA, false, stream);
            TutorialProgressView p2 = Project(PlayerB, false, stream);

            // Assert:各自只看自己的
            Assert.That(p1.IsCompleted("judge"), Is.True);
            Assert.That(p1.IsCompleted("treat"), Is.False);
            Assert.That(p2.IsCompleted("judge"), Is.False);
            Assert.That(p2.IsCompleted("treat"), Is.True);
        }

        // ══════════════ AC-1:纯函数性与单调性 ══════════════

        [Test]
        public void test_ac1_sameInput_evaluatesIdenticallyTwice()
        {
            SimEvent[] stream =
            {
                CaseOpened(50L, PatientA), Judgment(51L, PatientA, PlayerA),
                StructurePlaced(52L),
            };

            string first = Project(PlayerA, false, stream).Describe();
            string second = Project(PlayerA, false, stream).Describe();

            Assert.That(second, Is.EqualTo(first),
                "同一输入两次求值必须逐字一致(纯函数;任何隐藏状态都会在这里露头)");
        }

        [Test]
        public void test_ac1_prefixIsMonotoneSubsetOfFull()
        {
            // Arrange:A' = 前缀(只立案),A = A' + 落笔
            SimEvent opened = CaseOpened(60L, PatientA);
            SimEvent judgment = Judgment(61L, PatientA, PlayerA);

            TutorialProgressView prefix = Project(PlayerA, false, opened);
            TutorialProgressView full = Project(PlayerA, false, opened, judgment);

            // Assert:前缀的完成集 ⊆ 完整的完成集,且恰多一步(不回收)
            foreach (string step in CompletedSteps(prefix))
                Assert.That(full.IsCompleted(step), Is.True,
                    $"「{step}」在前缀里已成立,追加事件后不得回退(单调性)");
            Assert.That(full.CompletedCount, Is.EqualTo(prefix.CompletedCount + 1),
                "追加一条落笔事件应恰好多完成一步");
            Assert.That(full.IsCompleted("judge"), Is.True);
        }

        [Test]
        public void test_ac1_emptyStream_allStepsIncomplete_andNotAllCompleted()
        {
            TutorialProgressView view = Project(PlayerA, false);

            Assert.That(view.StepCount, Is.EqualTo(6), "空流不影响步数(步来自内容表,不是事件)");
            Assert.That(view.CompletedCount, Is.EqualTo(0));
            Assert.That(view.AllCompleted, Is.False,
                "空步 / 未完成 ⇒ AllCompleted 必须为 false(不取真空为真)");
        }

        [Test]
        public void test_ac1_defaultView_isFailClosed()
        {
            // F9(default(struct) 绕过构造器):default 视图不得冒充「全部完成」。
            var view = default(TutorialProgressView);
            Assert.That(view.StepCount, Is.EqualTo(0));
            Assert.That(view.AllCompleted, Is.False);
            Assert.That(view.IsCompleted("judge"), Is.False);
        }

        [Test]
        public void test_ac8_fullStream_allSixStepsCompleted()
        {
            SimEvent[] stream =
            {
                CaseOpened(70L, PatientA), Judgment(71L, PatientA, PlayerA),
                DrugTreatment(72L, PatientA, PlayerA), CaseClosed(73L, PatientA),
                StructurePlaced(74L),
            };

            TutorialProgressView view = Project(PlayerA, checkpointWritten: true, stream);

            Assert.That(view.AllCompleted, Is.True);
            Assert.That(view.IsCompleted("archive"), Is.True, "回执已置 ⇒ 册子步成立");
        }

        // ══════════════ AC-48-03:零落盘(读不改变流)══════════════

        [Test]
        public void test_ac4803_projection_doesNotMutateTheStream()
        {
            // Arrange:真 EventStream(主机 Append 发号),再快照其全部可观测面
            var stream = new EventStream(new FixedIdAuthority(), new OpenPresence());
            stream.Append(CaseOpened(80L, PatientA));
            stream.Append(Judgment(81L, PatientA, PlayerA));
            stream.Append(StructurePlaced(82L));

            string before = DescribeStream(stream);

            // Act:投影读两遍(含回执为真的一遍)
            var feed = new TutorialStreamFeed(() => stream.Events, _decoder, () => true);
            TutorialProjection.Project(feed, _steps, PlayerA);
            TutorialProjection.Project(feed, _steps, PlayerB);

            // Assert:流逐事件不变
            Assert.That(DescribeStream(stream), Is.EqualTo(before),
                "投影是只读消费者 —— 读了两遍之后流必须逐事件不变(AC-48-03 / ADR-019 §三)");
        }

        [Test]
        public void test_ac4803_streamCountUnchanged()
        {
            var stream = new EventStream(new FixedIdAuthority(), new OpenPresence());
            stream.Append(CaseOpened(90L, PatientA));

            var feed = new TutorialStreamFeed(() => stream.Events, _decoder);
            TutorialProjection.Project(feed, _steps, PlayerA);

            Assert.That(stream.Count, Is.EqualTo(1), "48 不是第四个 IEventSink 写入者 —— 读不得新增事件");
        }

        [Test]
        public void test_ac4803_saveBytes_areUnchangedByProjection()
        {
            // Arrange:真事件流 + 真组版路径(不是 Fake)—— 投影前后各存一次同一时刻的档
            var stream = new EventStream(new FixedIdAuthority(), new OpenPresence());
            stream.Append(CaseOpened(120L, PatientA));
            stream.Append(Judgment(121L, PatientA, PlayerA));
            stream.Append(CaseClosed(122L, PatientA));
            stream.Append(StructurePlaced(123L));

            byte[] before = ComposeSave(stream);

            // Act:跑两遍投影(含回执为真的一遍 —— 那是唯一可能「写点什么」的分支)
            var feed = new TutorialStreamFeed(() => stream.Events, _decoder, () => true);
            TutorialProjection.Project(feed, _steps, PlayerA);
            TutorialProjection.Project(feed, _steps, PlayerB);

            // Assert:存档体逐字节相同
            CollectionAssert.AreEqual(before, ComposeSave(stream),
                "投影前后组出的存档必须逐字节相同 —— 存档里不含任何 tutorial 进度字段(AC-48-03)");
        }

        /// <summary>同一时刻、同一流的存档体(不含校验和;组版参数全固定)。</summary>
        /// <remarks>⚠️ 折叠谓词恒 false:本夹具的流里没有病史事件,折叠不该发生 ——
        /// 若它真被调用,说明组版对它做了我们没预期的事,那时本夹具会以别的形式红。</remarks>
        private byte[] ComposeSave(EventStream stream)
            => SaveComposer.Compose(
                stream.Events, new NeverFolded(), _decoder, new SaveCodec(),
                worldSeed: 1UL, configVersion: 0U, tick: 130L,
                timeParams: new SaveTimeParams(36000L, 27000L, 9000L, 252000L, 5L, 6000L, 100L),
                existingFoldedRows: stream.FoldedRows, blobPool: _pool);

        /// <summary>组版用的折叠谓词替身 —— 本夹具里永不折叠。</summary>
        private sealed class NeverFolded : IFoldedPredicate
        {
            public bool Folded(int patientId) => false;
        }

        private static string DescribeStream(EventStream stream)
            => string.Join("\n", stream.Events.Select(e =>
                $"{e.Tick}/{e.Patient.Value}/{e.Seq}/{e.Kind}/{e.Payload.BlobId}:{e.Payload.Offset}:{e.Payload.Length}"));

        // ══════════════ 入口守卫 ══════════════

        [Test]
        public void test_guard_nullFeed_throws()
        {
            var ex = Assert.Throws<ArgumentNullException>(
                () => TutorialProjection.Project(null, _steps, PlayerA));
            Assert.That(ex.ParamName, Is.EqualTo("feed"));
        }

        [Test]
        public void test_guard_nullSteps_throws()
        {
            var feed = new TutorialStreamFeed(() => Array.Empty<SimEvent>(), _decoder);
            var ex = Assert.Throws<ArgumentNullException>(
                () => TutorialProjection.Project(feed, null, PlayerA));
            Assert.That(ex.ParamName, Is.EqualTo("steps"));
        }

        [Test]
        public void test_guard_negativePlayerId_throws()
        {
            var feed = new TutorialStreamFeed(() => new[] { Judgment(100L, PatientA, 0) }, _decoder);
            var ex = Assert.Throws<ArgumentOutOfRangeException>(
                () => TutorialProjection.Project(feed, _steps, TutorialEventFact.NoAuthor));
            Assert.That(ex.ParamName, Is.EqualTo("playerId"),
                "拿「无作者」哨兵求值会让每一次落笔都算命中 —— 必须 fail-loud");
        }

        [Test]
        public void test_guard_missingDecoder_throws()
        {
            var ex = Assert.Throws<ArgumentNullException>(
                () => TutorialSnapshotBuilder.Build(Array.Empty<SimEvent>(), null));
            Assert.That(ex.ParamName, Is.EqualTo("decoder"));
        }

        // ══════════════ 快照解码面 ══════════════

        [Test]
        public void test_builder_authorAxis_isDecodedFromPayloads()
        {
            SimEvent[] stream =
            {
                CaseOpened(110L, PatientA),                 // 无作者轴
                Judgment(111L, PatientA, PlayerA),          // AuthorPlayerId
                EmergencyAttempt(112L, PatientA, PlayerB),  // ActorId
                DrugTreatment(113L, PatientA, PlayerB),     // ActorId
            };

            var facts = TutorialSnapshotBuilder.Build(stream, _decoder);

            Assert.That(facts.Count, Is.EqualTo(4));
            Assert.That(facts[0].KindName, Is.EqualTo("CaseOpened"));
            Assert.That(facts[0].HasAuthor, Is.False);
            Assert.That(facts[0].AuthorPlayerId, Is.EqualTo(TutorialEventFact.NoAuthor));
            Assert.That(facts[1].AuthorPlayerId, Is.EqualTo(PlayerA));
            Assert.That(facts[2].AuthorPlayerId, Is.EqualTo(PlayerB));
            Assert.That(facts[3].AuthorPlayerId, Is.EqualTo(PlayerB));
            Assert.That(facts[1].Tick, Is.EqualTo(111L), "Tick 原样透传(仅观测,不作判据)");
        }

        [Test]
        public void test_builder_emptyStream_returnsEmptyNotThrow()
        {
            var facts = TutorialSnapshotBuilder.Build(Array.Empty<SimEvent>(), _decoder);
            Assert.That(facts, Is.Not.Null);
            Assert.That(facts.Count, Is.EqualTo(0));
        }

        // ══════════════ 夹具替身(仅两处:发号 / 在场)══════════════

        private sealed class FixedIdAuthority : IIdAuthority
        {
            private int _next = 1;
            public PatientId NextPatientId() => new PatientId(_next++);
            public ItemInstanceId NextItemInstanceId() => new ItemInstanceId(_next++);
        }

        private sealed class OpenPresence : IPresenceQuery
        {
            public bool IsPresent(PatientId patientId) => true;
            public int PresentCount => 0;
            public bool IsPresentAt(WorldPos cell) => false;
            public IReadOnlyCollection<int> PresentPatientIds() => Array.Empty<int>();
        }
    }
}
