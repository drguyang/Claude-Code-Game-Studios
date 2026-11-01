// S23-5 · story-005 复活落点与传送命令 —— AC 闭夹具(2026-11-01)
//
// ─────────────────────────────────────────────────────────────────────────────
// 读哪些仓库文件 / 写哪里(test-standards 具名声明义务)
// ─────────────────────────────────────────────────────────────────────────────
// 读(只读,零写盘):
//   ① `unity/Assets/Sim/RespawnCommandIssuer.cs`、`unity/Assets/Sim/DeathCandidateConsumer.cs`、
//      `unity/Assets/Sim.Contracts/Abstractions.cs` 的**源码**(剥注释后扫描)—— 为什么必须读真源:
//      「29 复活路径零 Append 点 / 零 ActorCellEntered」(AC-29-09)与「命令契约零 Vector3」
//      (ADR-025 QQ-01 引用面断言)是**跨文件的源码级契约**,类型面表达不了。
// 写:**零写盘**(流与装配全内存)。
//
// ── ⚠️ 桩面标注(卡面口径)────────────────────────────────────────────────────
// `ITeleportCommandSink` 的生产实装归 1(玩家控制器/表现侧,ADR-025 QQ-01 ①′)——
// 本件的 sink 全部是**测试桩**(记录命令的显式 TestStub,非生产 Fake 冒充生产类);
// 1 侧消费(发 ActorCellEntered)的联调维持 BLOCKED-BY-9/24 标注,本件**不**冒充全链绿。
//
// ── AC → 测试映射(story-005)────────────────────────────────────────────────
//   AC-29-09            → test_ac2909_zeroAppendPoint…(源码)+ test_issue_zeroStreamEvents(行为面)
//   AC-29-10(半边)     → test_boundedRevives_throughRealChain…(真冷却门链;全链绿随收口核)
//   落点两分支          → test_clinicBedAvailable… / test_noClinicBothForms_commandTargetsSpawnCell
//   命令形状            → test_commandShape_allInteger_zeroVector3(ADR-025 引用面)
//   副作用负面清单      → test_issue_zeroStreamEvents(零流事件 = 疾病/世界/掉落全不残不越)
//   联机共床            → test_twoPlayersSameBed_sameCellTwoCommands
//   结算段时序          → test_settlementSegment_reviveAfterSkillDown(掉落 → 掉级 → 复活)
//   未接线              → test_consumerUnwired_revive_003BehaviorPreserved

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;
using UnityEngine;

namespace DaYiJingCheng.Tests.DeathRespawn
{
    /// <summary>story-005 的 AC 闭:落点两分支、命令形状、零写流、有界、时序。</summary>
    [TestFixture]
    public sealed class DeathRespawnCommandTest
    {
        private CompositionRootServices _bag;

        private const int PlayerId = 7;
        private const long Tick = 100;
        private static readonly WorldPos SpawnCell = new WorldPos(0, 1, 0);
        private static readonly WorldPos BedCell = new WorldPos(3, 2, 9);

        [SetUp]
        public void Setup()
        {
            _bag = CompositionRoot.Assemble();
        }

        /// <summary>显式命名桩:1 侧传送 sink 的**测试替身**(只记录,零行为 ——
        /// 生产实装归 1,本桩面按卡面口径标注,不冒充生产装配)。</summary>
        private sealed class RecordingTeleportStub : ITeleportCommandSink
        {
            public readonly List<(int ActorId, WorldPos Cell)> Commands = new();
            public void RequestTeleport(int actorId, WorldPos cell) => Commands.Add((actorId, cell));
        }

        private sealed class FixedBed : IClinicReviveCell
        {
            private readonly WorldPos _cell;
            public FixedBed(WorldPos cell) => _cell = cell;
            public bool TryGet(int actorId, out WorldPos cell) { cell = _cell; return true; }
        }

        private sealed class NoBed : IClinicReviveCell
        {
            public bool TryGet(int actorId, out WorldPos cell) { cell = default; return false; }
        }

        private static RespawnCommandIssuer NewIssuer(ITeleportCommandSink sink, IClinicReviveCell bed)
            => new RespawnCommandIssuer(sink, bed, SpawnCell);

        private int CountOf(EventKind kind) => _bag.Stream.Events.Count(e => e.Kind == kind);

        private static string RepoFile(string relative) =>
            Path.Combine(Application.dataPath, "..", "..", relative.Replace('/', Path.DirectorySeparatorChar));

        private static string StripComments(string src)
        {
            string noBlock = Regex.Replace(src, @"/\*.*?\*/", " ", RegexOptions.Singleline);
            return Regex.Replace(noBlock, @"//[^\n]*", " ");
        }

        // ══════════════════ 落点两分支 ══════════════════

        [Test]
        public void test_clinicBedAvailable_commandTargetsBedCell()
        {
            var sink = new RecordingTeleportStub();

            NewIssuer(sink, new FixedBed(BedCell)).Issue(PlayerId);

            CollectionAssert.AreEqual(new[] { (PlayerId, BedCell) }, sink.Commands,
                "有医馆床格 ⇒ 命令格 = 选定床格(24 端口给格;选定规则本体归 24)");
        }

        /// <summary>无床的两形态(端口未接线 = null;端口在但答无)⇒ 恒兜底世界出生点。</summary>
        [Test]
        public void test_noClinicBothForms_commandTargetsSpawnCell()
        {
            var sinkNull = new RecordingTeleportStub();
            var sinkFalse = new RecordingTeleportStub();

            NewIssuer(sinkNull, null).Issue(PlayerId);          // 形态①:端口未接线
            NewIssuer(sinkFalse, new NoBed()).Issue(PlayerId);  // 形态②:端口答无床

            CollectionAssert.AreEqual(new[] { (PlayerId, SpawnCell) }, sinkNull.Commands,
                "未接线 ⇒ 兜底出生点(ADR-015 固定格)");
            CollectionAssert.AreEqual(new[] { (PlayerId, SpawnCell) }, sinkFalse.Commands,
                "端口答无 ⇒ 兜底出生点(两分支全覆盖)");
        }

        /// <summary>联机共床:两玩家同床格 ⇒ 同格两条命令(1 侧串行定序执行;29 逐结算即发)。</summary>
        [Test]
        public void test_twoPlayersSameBed_sameCellTwoCommands()
        {
            var sink = new RecordingTeleportStub();
            RespawnCommandIssuer issuer = NewIssuer(sink, new FixedBed(BedCell));

            issuer.Issue(7);
            issuer.Issue(9);

            Assert.AreEqual(2, sink.Commands.Count, "两条命令(不批不并)");
            Assert.IsTrue(sink.Commands.All(c => c.Cell == BedCell), "同格落点");
            Assert.AreEqual(new[] { 7, 9 }, sink.Commands.Select(c => c.ActorId).ToArray(),
                "逐 actor 独立命令(串行定序在 1 侧消费)");
        }

        // ══════════════════ AC-29-09 —— 零 Append 点 + 零流事件 ══════════════════

        /// <summary>29 复活路径(发令面 + 消费者)剥注释后零 `.Append(` / 零 `ActorCellEntered`
        /// —— 29 只发整数命令,格事件由 1 发(AC-29-09 检索半)。</summary>
        [Test]
        public void test_ac2909_zeroAppendPoint_in29RespawnPath()
        {
            string issuer = StripComments(File.ReadAllText(RepoFile(
                "unity/Assets/Sim/RespawnCommandIssuer.cs")));
            string consumer = StripComments(File.ReadAllText(RepoFile(
                "unity/Assets/Sim/DeathCandidateConsumer.cs")));

            Assert.That(issuer.Contains(".Append(") || issuer.Contains("IEventSink")
                || issuer.Contains("ActorCellEntered"), Is.False,
                "发令面零 Append 点、零 ActorCellEntered 引用(AC-29-09)");
            Assert.That(consumer.Contains("ActorCellEntered"), Is.False,
                "消费者层同样零位置事件(29 零位置 Append —— ADR-020 §四)");
        }

        /// <summary>行为面:复活发令**零流事件** —— 过场位移零写流;疾病/世界/掉落
        /// 全不残不越(副作用负面清单;掉落物仍以原事件在流上,story-002 回捡动线对拍)。</summary>
        [Test]
        public void test_issue_zeroStreamEvents_sideEffectNegativeList()
        {
            int before = _bag.Stream.Events.Count;

            var sink = new RecordingTeleportStub();
            NewIssuer(sink, new FixedBed(BedCell)).Issue(PlayerId);

            Assert.AreEqual(1, sink.Commands.Count, "[前置] 命令已发");
            Assert.AreEqual(before, _bag.Stream.Events.Count,
                "复活发令零流事件(位移/落格归 1;29 零写流)");
            Assert.AreEqual(0, CountOf(EventKind.PlayerDied), "不重复触发死亡类事件");
        }

        // ══════════════════ AC-29-10(半边)—— 有界性走真冷却门链 ══════════════════

        /// <summary>候选每 tick 一个 ⇒ 死亡只在 {0, 2400}(S23-1 同款时序)⇒ 复活命令
        /// 恰 2 条 —— 复活来源上界 = 死亡上界(AC-29-10 半边;ActorCellEntered 实发数
        /// 归 1 侧联调,收口核)。</summary>
        [Test]
        public void test_boundedRevives_throughRealChain_commandsTrackDeaths()
        {
            var sink = new RecordingTeleportStub();
            var consumer = new DeathCandidateConsumer(
                new PlayerDiedWriter(_bag.EventSink, _bag.Encoder),
                deathAllowed: _bag.DeathCooldown.DeathAllowed,
                actorClassOf: _ => ActorClass.Player,
                entryOf: _ => LethalEntry(),
                deathCellOf: _ => SpawnCell,
                revive: (id, tick) => NewIssuer(sink, new FixedBed(BedCell)).Issue(id));

            const long horizon = 2600;
            for (long t = 0; t <= horizon; t++)
                consumer.OnDeathCandidate(PlayerId, t);

            int deaths = CountOf(EventKind.PlayerDied);
            Assert.AreEqual(2, deaths, "[前置] 死亡有界 = 2(S23-1 已证)");
            Assert.AreEqual(deaths, sink.Commands.Count,
                "复活命令数 == PlayerDied 数(每死恰一复活;有界性同源)");
        }

        /// <summary>谓词否决 / 冷却拒 ⇒ 零命令(结算段只在 Written 后跑)。</summary>
        [Test]
        public void test_rejectOrCooldown_zeroCommands()
        {
            var sink = new RecordingTeleportStub();
            var consumer = new DeathCandidateConsumer(
                new PlayerDiedWriter(_bag.EventSink, _bag.Encoder),
                deathAllowed: _bag.DeathCooldown.DeathAllowed,
                actorClassOf: _ => ActorClass.Player,
                entryOf: _ => LethalEntry(),
                deathCellOf: _ => SpawnCell,
                revive: (id, tick) => NewIssuer(sink, new FixedBed(BedCell)).Issue(id));

            consumer.OnDeathCandidate(PlayerId, Tick);
            consumer.OnDeathCandidate(PlayerId, Tick);   // 同 tick 重注入 = 冷却拒

            Assert.AreEqual(1, sink.Commands.Count, "同 candidate 幂等 ⇒ 恰一复活命令");
        }

        // ══════════════════ 结算段时序 / 未接线 ══════════════════

        [Test]
        public void test_settlementSegment_reviveAfterSkillDown()
        {
            var calls = new List<string>();
            var consumer = new DeathCandidateConsumer(
                new PlayerDiedWriter(_bag.EventSink, _bag.Encoder),
                _bag.DeathCooldown.DeathAllowed,
                _ => ActorClass.Player, _ => LethalEntry(), _ => SpawnCell,
                dropSettlement: (id, cell, tick) => calls.Add("drop"),
                skillDown: (id, tick) => calls.Add("skill"),
                revive: (id, tick) => calls.Add("revive"));

            consumer.OnDeathCandidate(PlayerId, Tick);

            CollectionAssert.AreEqual(new[] { "drop", "skill", "revive" }, calls,
                "结算段全序 = 掉落 → 掉级 → 复活(story-005:46 末步)");
        }

        [Test]
        public void test_consumerUnwired_revive_003BehaviorPreserved()
        {
            var consumer = new DeathCandidateConsumer(
                new PlayerDiedWriter(_bag.EventSink, _bag.Encoder),
                _bag.DeathCooldown.DeathAllowed,
                _ => ActorClass.Player, _ => LethalEntry(), _ => SpawnCell);

            consumer.OnDeathCandidate(PlayerId, Tick);

            Assert.AreEqual(1, CountOf(EventKind.PlayerDied), "001–003 行为不变");
            // 未接线 ⇒ 零复活命令(零流事件本就成立 —— 命令面在 sink,不接线即无处可去)
        }

        // ══════════════════ 命令形状(ADR-025 QQ-01 引用面)══════════════════

        /// <summary>整数命令半:契约与本类源码零 Vector3 / 零 float(连续半物理上
        /// 进不了门 A 程序集;引用面断言与门 A 互补)。</summary>
        [Test]
        public void test_commandShape_allInteger_zeroVector3()
        {
            string contract = StripComments(File.ReadAllText(RepoFile(
                "unity/Assets/Sim.Contracts/Abstractions.cs")));
            string issuer = StripComments(File.ReadAllText(RepoFile(
                "unity/Assets/Sim/RespawnCommandIssuer.cs")));

            Assert.That(Regex.IsMatch(contract, @"\bVector3\b"), Is.False,
                "ADR-025 ①′:整数命令半零 Vector3(连续半住 Gameplay.Presentation)");
            Assert.That(Regex.IsMatch(issuer, @"\bVector3\b|\bfloat\b|\d+\.\d+"), Is.False,
                "发令面零连续坐标(ADR-020 §四 / 门 A 同法)");
            Assert.That(contract, Does.Contain("RequestTeleport(int actorId, WorldPos cell)"),
                "命令形状 = (int, WorldPos) 全整数(正判)");
        }

        [Test]
        public void test_issue_invalidActor_throws()
        {
            var sink = new RecordingTeleportStub();
            Assert.Throws<ArgumentOutOfRangeException>(
                () => NewIssuer(sink, new FixedBed(BedCell)).Issue(-1));
            Assert.Throws<ArgumentNullException>(() => new RespawnCommandIssuer(null, null, SpawnCell));
        }

        // ── 夹具(纯数据)──────────────────────────────────────────────────

        private static DiseaseRegistryEntry LethalEntry()
            => new DiseaseRegistryEntry
            {
                DiseaseId = 1,
                DiseaseKey = "DIS_PLAGUE",
                Kind = DiseaseKind.Disease,
                Lethality = 3,
            };
    }
}
