// S23-1 · story-001 PlayerDied 登记与死亡结算入口 —— **AC 闭**夹具(2026-11-01)
//
// ─────────────────────────────────────────────────────────────────────────────
// 读哪些仓库文件 / 写哪里(test-standards 具名声明义务)
// ─────────────────────────────────────────────────────────────────────────────
// 读(只读,零写盘):
//   ① `Assets/Sim/**` 与 `Assets/Gameplay.Boot/**` 的**源码**(唯一 Append 点 / MotorSuppressed
//      负断言的扫描面)—— 为什么必须读真源:「生产码里只有一个 PlayerDied Append 点」是
//      **跨文件的调用约定**,类型面表达不了,只能源码断言(与 `test_driverSurface_neverTakesHarvestGate` 同法)。
//   ② `design/gdd/death-and-respawn.md`(移动禁用接线判据:依赖行存在性)—— 为什么读真 GDD:
//      「1 读 PlayerDied 的依赖行存在」是**文档面判据**,读内联副本会失去「测的就是真表」价值。
// 写:**零写盘**(流与装配全内存)。
//
// ── 与既有两件的分工(不重复;判「无证据」前已扫全树)────────────────────────────
//   `death_candidate_writers_test.cs`(S18-1):写者结果码 / 载荷 round-trip / 谓词闸 /
//       背包无关结构性断言 / 桩门下的候选消费 —— **桩 `deathAllowed` 恒 true**。
//   `death_cooldown_test.cs`(story-004):门谓词边界 / 零计数器 / 登记一致性 / 装配面。
//   `DiseaseSimulation/death_candidate_sink_test.cs`:sink 契约 + F4 端到端(含
//       `test_simAssembly_referencesZeroEngineAssemblies` —— 本文件**不重复**该门 A 断言)。
//   本件补的 = 既有件**没有**的面:①**真冷却门**下的候选链(幂等 / 有界 —— 桩恒真测不出);
//   ②流别路由(只进世界流);③构造依赖集形状(AC-29-13 读法 A);④唯一 Append 点 +
//   MotorSuppressed 负断言(源码面);⑤death_cell 出生格兜底(生产 lambda 镜像)。
//
// ── AC → 测试映射(story-001;⚠️ 桩面绿 = 「29 侧半边」,全链绿随 G-1 门行收口核)────
//   AC-29-01(半边)      → test_candidate_throughRealGate… / test_sameCandidateReinjected…
//   AC-29-03            → 既有 structural(背包无关)+ 本件链路测试全程无背包依赖(登记,不重复)
//   AC-29-13(读法 A)    → test_consumerCtorDependencySet…
//   路由断言            → test_playerDied_onlyInWorldStream…
//   主机唯一 Append     → test_noOtherProductionWriter…
//   移动禁用接线        → test_deathMovementDisable…
//   AC-29-08(004 全链半)→ test_secondCandidateWithinCooldown…
//   AC-29-10(004 全链半)→ test_boundedDeaths…
//   装配三分(Sim 侧)    → 既有 sink_test 门 A 断言(不重复)
//   death_cell 兜底     → test_deathCell_fallsBackToSpawnCell…

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;
using UnityEngine;

namespace DaYiJingCheng.Tests.DeathRespawn
{
    /// <summary>story-001 的 AC 闭:真冷却门下的候选链、路由、结构判据、源码负断言。</summary>
    [TestFixture]
    public sealed class PlayerDiedSettlementTest
    {
        private CompositionRootServices _bag;
        private PlayerDiedWriter _writer;
        private DeathCandidateConsumer _consumer;

        private const int PlayerId = 7;
        private const long FirstTick = 100;
        private static readonly WorldPos SpawnCell = new WorldPos(0, 1, 0);   // 镜像 CompositionRoot:356(生产 lambda 的兜底值)

        [SetUp]
        public void Setup()
        {
            _bag = CompositionRoot.Assemble();
            _writer = new PlayerDiedWriter(_bag.EventSink, _bag.Encoder);

            // 真冷却门 = 服务袋里那只(它读**装配的这条流** —— death_cooldown_test §⑦ 已钉)。
            // deathCellOf = 生产 lambda 同款(CompositionRoot:366-368):真查流 + 出生格兜底。
            _consumer = new DeathCandidateConsumer(
                _writer,
                deathAllowed: _bag.DeathCooldown.DeathAllowed,
                actorClassOf: _ => ActorClass.Player,
                entryOf: _ => LethalEntry(),
                deathCellOf: actorId =>
                    DeathCellQuery.TryLastCellOf(_bag.Stream.Events, _bag.Decoder, actorId, out WorldPos cell)
                        ? cell
                        : SpawnCell);
        }

        // ── 夹具(纯数据,非 Fake 被测对象)──────────────────────────────────

        private static DiseaseRegistryEntry LethalEntry()
            => new DiseaseRegistryEntry
            {
                DiseaseId = 1,
                DiseaseKey = "DIS_PLAGUE",
                Kind = DiseaseKind.Disease,
                Lethality = 3,
            };

        private int CountOf(EventKind kind) => _bag.Stream.Events.Count(e => e.Kind == kind);

        private static string RepoRoot() =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        private static string RepoFile(string relative) =>
            Path.Combine(RepoRoot(), relative.Replace('/', Path.DirectorySeparatorChar));

        private static string StripComments(string src)
        {
            string noBlock = System.Text.RegularExpressions.Regex.Replace(
                src, @"/\*.*?\*/", " ", System.Text.RegularExpressions.RegexOptions.Singleline);
            return System.Text.RegularExpressions.Regex.Replace(noBlock, @"//[^\n]*", " ");
        }

        // ══════════════════ AC-29-01(半边)—— 真门下的候选链 ══════════════════

        [Test]
        public void test_candidate_throughRealGate_writesOnePlayerDied()
        {
            int before = CountOf(EventKind.PlayerDied);

            _consumer.OnDeathCandidate(PlayerId, FirstTick);

            Assert.AreEqual(before + 1, CountOf(EventKind.PlayerDied),
                "谓词放行 + 冷却门开 ⇒ 恰 1 条(真门,非桩 —— 桩面见 death_candidate_writers_test)");
        }

        /// <summary>同 candidate 重注入 ⇒ 幂等:恰一条。</summary>
        /// <remarks><b>判别力</b>:把真门换成桩恒 true(S18-1 夹具形态)⇒ 本测红(会写两条)。
        /// 这就是「判据 = 流前缀纯函数」的落实 —— 去重不另立计数器,由 F-29-2 的
        /// `t − last_death_tick ≥ CD` 承载(AC-29-15 零计数器纪律)。</remarks>
        [Test]
        public void test_sameCandidateReinjected_sameTick_idempotent_oneEvent()
        {
            _consumer.OnDeathCandidate(PlayerId, FirstTick);
            _consumer.OnDeathCandidate(PlayerId, FirstTick);   // 同 tick 重注入

            Assert.AreEqual(1, CountOf(EventKind.PlayerDied),
                "同 candidate 二次注入 ⇒ 冷却门拒(刚死过)⇒ 幂等,恰 1 条");
        }

        // ══════════════════ AC-29-08(004 全链半)—— 冷却内再死零事件 ══════════════════

        /// <summary>冷却内再次致死 ⇒ 零事件(OQ-29-4 已裁:不结算、不传送、回合继续)。</summary>
        /// <remarks><b>负控独立于正例</b>(F4):第一条写入先发生并断言,再验第二条零事件 ——
        /// 不是「流本来就空」的空洞正控。</remarks>
        [Test]
        public void test_secondCandidateWithinCooldown_zeroEvents()
        {
            _consumer.OnDeathCandidate(PlayerId, FirstTick);
            Assert.AreEqual(1, CountOf(EventKind.PlayerDied), "[负控前置] 第一次死亡已入流");

            _consumer.OnDeathCandidate(PlayerId, FirstTick + 10);   // 冷却内(2400 tick)

            Assert.AreEqual(1, CountOf(EventKind.PlayerDied),
                "冷却内再死 ⇒ 零事件(不发 PlayerDied、不结算、不传送 —— OQ-29-4 裁定)");
        }

        // ══════════════════ AC-29-10(004 全链半)—— 有界性 ══════════════════

        /// <summary>每 tick 一个候选、跨 2600 tick ⇒ 恰 2 条(t=0 与 t=2400)。</summary>
        /// <remarks><b>判别力</b>:门短路为恒 true ⇒ 本测红(会写 2601 条)。
        /// 上界式:窗口 H 内每 actor 死亡数 ≤ ⌊H/CD⌋ + 1;实体间独立由 death_cooldown_test §② 钉。</remarks>
        [Test]
        public void test_boundedDeaths_overHorizon_playersDividedByCooldown()
        {
            const long horizon = 2600;
            for (long t = 0; t <= horizon; t++)
                _consumer.OnDeathCandidate(PlayerId, t);

            var deathTicks = _bag.Stream.Events
                .Where(e => e.Kind == EventKind.PlayerDied)
                .Select(e => e.Tick).ToArray();

            Assert.AreEqual(new long[] { 0, DeathCooldownGate.DEATH_COOLDOWN }, deathTicks,
                $"候选每 tick 一个 ⇒ 死亡只在 t=0 与 t=2400;{horizon} tick 窗口内 ≤ ⌊H/CD⌋+1 = 2(AC-29-10)");
        }

        // ══════════════════ 路由断言(PlayerDied 只进世界流)══════════════════

        /// <summary>TR-death-002:不落病史流。判别力:`StreamRouting` 的 PlayerDied 分支改指
        /// History ⇒ 本测红(generated 文件亦可变异,还原纪律同常)。</summary>
        [Test]
        public void test_playerDied_onlyInWorldStream_neverInHistoryOrCase()
        {
            _consumer.OnDeathCandidate(PlayerId, FirstTick);

            var died = _bag.Stream.Events.Where(e => e.Kind == EventKind.PlayerDied).ToList();
            Assert.That(died, Is.Not.Empty, "[前置] 已有 PlayerDied 供路由检查");
            foreach (SimEvent e in died)
                Assert.AreEqual(StreamId.World, StreamRouting.Of(e.Kind),
                    "PlayerDied 必须落在世界流(registry:stream: world;TR-death-002)");

            int inHistory = _bag.Stream.Events.Count(e =>
                e.Kind == EventKind.PlayerDied && StreamRouting.Of(e.Kind) != StreamId.World);
            Assert.AreEqual(0, inHistory, "零条 PlayerDied 落在非世界流");
        }

        // ══════════════════ AC-29-13(读法 A)—— 构造依赖集形状 ══════════════════

        /// <summary>29 的输入源 = `IDeathCandidateSource`(构造依赖闭包里**没有**流订阅、
        /// 没有 `IEventSink`、没有 `PatientId` 病人身份谓词)。</summary>
        /// <remarks>读法 A(2026-10-21):「输入 Kind 集」按字面不可判(DeathCandidate 不是
        /// Kind)⇒ 判据 = 29 的**构造依赖集**;`PlayerDied` 是输出不是输入,不在本面内。</remarks>
        [Test]
        public void test_consumerCtorDependencySet_withinStoryDeclaredShape()
        {
            var ctor = typeof(DeathCandidateConsumer).GetConstructors()
                .Single(c => c.GetParameters().Length > 0);
            var paramTypes = ctor.GetParameters().Select(p => p.ParameterType).ToArray();

            CollectionAssert.AreEquivalent(
                new System.Type[]
                {
                    typeof(PlayerDiedWriter),
                    typeof(Func<int, long, bool>),          // 冷却门(值由调用方给,本类不实现冷却)
                    typeof(Func<int, ActorClass>),          // 实体类别(判死谓词入参)
                    typeof(Func<int, DiseaseRegistryEntry>),// 病种条目(判死谓词入参)
                    typeof(Func<int, WorldPos>),            // death_cell(装配层从流取)
                    typeof(Action<int, WorldPos, long>),    // 散落结算回调(S23-2;可 null = 未接线)
                    typeof(Action<int, long>),              // 掉级结算回调(S23-3;可 null = 未接线)
                    typeof(Action<int, long>),              // 复活发令回调(S23-5;可 null = 未接线)
                },
                paramTypes,
                "story-001:48 定形的构造依赖闭包(+S23-2 掉落回调位 +S23-3 掉级回调位 " +
                "+S23-5 复活回调位 —— 结算段 = PlayerDied → 掉落 → 掉级 → 复活;" +
                "多出任何依赖 = 输入源集扩大(AC-29-13)");

            Assert.That(paramTypes, Has.No.Member(typeof(IEventSink)),
                "消费者不持 IEventSink —— Append 唯一入口在写者(单写入口纪律)");
            Assert.That(paramTypes, Has.No.Member(typeof(PatientId)),
                "零病人身份谓词:玩家 id 走裸 int(ADR-016 §二 共用空间),29 不做病人流查询");
        }

        // ══════════════════ 主机唯一 Append(源码面)══════════════════

        /// <summary>生产码里 `EventKind.PlayerDied` 的引用点收敛于**已知允许集**,且其中
        /// **唯一**含 Append 调用的是写者。</summary>
        /// <remarks>读真源的理由见文件头;判定面 = `Assets/Sim` + `Assets/Gameplay.Boot`
        /// (排除 `/Tests/`)。两层判据:
        /// ① 引用点 ⊆ 允许集 {写者(唯一 Append)· kindgen 路由(非 Append)·
        ///    冷却门(读侧重算 `last_death_tick`,AC-29-15 零计数器)} —— 新增引用 = 须**有意识地**
        ///    进允许集,不得静默扩散;
        /// ② 允许集内「引用 ∧ `Append(new SimEvent`」共现者**恰为写者** —— 读侧文件长出
        ///    Append = 第二写入通道(ADR-005 主机唯一 Append)。
        /// 判别力:任一层被破坏 ⇒ 本测红(首版只断①的精确等集,被冷却门的合法读面打红过一次)。</remarks>
        [Test]
        public void test_noOtherProductionWriter_appendsPlayerDied()
        {
            var hits = new List<string>();
            foreach (string dir in new[] { "unity/Assets/Sim", "unity/Assets/Gameplay.Boot" })
            {
                string root = Path.Combine(RepoRoot(), dir);
                Assert.IsTrue(Directory.Exists(root), $"扫描面缺失:{root}");
                foreach (string f in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
                {
                    string norm = f.Replace('\\', '/');
                    if (norm.Contains("/Tests/")) continue;
                    if (StripComments(File.ReadAllText(f)).Contains("EventKind.PlayerDied"))
                        hits.Add(norm.Substring(norm.IndexOf("unity/Assets")));
                }
            }

            // ① 允许集(带理由 —— 每个 read-side 成员的合法性在此登记,不靠沉默):
            var allowReadSide = new Dictionary<string, string>
            {
                ["unity/Assets/Sim/PlayerDiedWriter.cs"] = "唯一 Append 点(写者)",
                ["unity/Assets/Sim/StreamRouting.g.cs"] = "kindgen 路由生成物(非 Append)",
                ["unity/Assets/Sim/DeathCooldownGate.cs"] = "读侧重算 last_death_tick(AC-29-15 零计数器)",
            };

            var unknown = hits.Where(h => !allowReadSide.ContainsKey(h)).ToList();
            Assert.That(unknown, Is.Empty,
                "EventKind.PlayerDied 的生产引用点越出允许集 —— 新增写入/读取面须先在此登记理由:\n  " +
                string.Join("\n  ", unknown));

            // ② 允许集内 Append 共现恰为写者:
            var appenders = hits.Where(h => StripComments(File.ReadAllText(RepoFile(h)))
                                          .Contains("Append(new SimEvent")).ToList();
            Assert.That(appenders, Is.EquivalentTo(new[] { "unity/Assets/Sim/PlayerDiedWriter.cs" }),
                "读侧文件长出 Append = 第二写入通道(ADR-005 主机唯一 Append)");
        }

        // ══════════════════ 移动禁用接线(GDD 依赖行 + 源码负断言)══════════════════

        /// <summary>① 29 的结算面零 `MotorSuppressed` 调用(死亡禁动 = 1 读 PlayerDied 自身决策,
        /// 29 不在白名单 {4,10,25});② GDD 的依赖行存在(1 的 `TeleportTo` + MotorSuppressed
        /// 误用修订记录)。</summary>
        [Test]
        public void test_deathMovementDisable_zeroMotorSuppressedIn29Surface()
        {
            foreach (string file in new[]
                     {
                         "unity/Assets/Sim/DeathCandidateConsumer.cs",
                         "unity/Assets/Sim/PlayerDiedWriter.cs",
                         "unity/Assets/Sim/DeathCooldownGate.cs",
                         "unity/Assets/Sim/DeathCellQuery.cs",
                         "unity/Assets/Sim/EntityDeathGates.cs",
                     })
            {
                string src = StripComments(File.ReadAllText(RepoFile(file)));
                Assert.IsFalse(src.Contains("MotorSuppressed"),
                    $"{file} 出现 MotorSuppressed —— 29 不得直接锁移动(GDD 首轮阻断 B6 修订)");
            }

            string gdd = File.ReadAllText(RepoFile("design/gdd/death-and-respawn.md"));
            Assert.That(gdd, Does.Contain("1 玩家控制器").And.Contain("TeleportTo"),
                "GDD 上游表须有 1 的依赖行(TeleportTo / 1 发 ActorCellEntered)—— 依赖行存在性判据");
            Assert.That(gdd, Does.Contain("MotorSuppressed"),
                "GDD 须保留 MotorSuppressed 误用的修订记录(2026-09-19 · 阻断 B6)—— 那是本负断言的出处");
        }

        // ══════════════════ death_cell 出生格兜底 ══════════════════

        /// <summary>玩家从未跨格(零 `ActorCellEntered`)⇒ death_cell = 世界出生点固定格
        /// (生产 lambda 镜像:`CompositionRoot` 的 `WorldSpawnCell = (0,1,0)`)。</summary>
        [Test]
        public void test_deathCell_fallsBackToSpawnCell_whenNeverCrossed()
        {
            _consumer.OnDeathCandidate(PlayerId, FirstTick);   // 空流 ⇒ TryLastCellOf false ⇒ 兜底

            SimEvent e = _bag.Stream.Events.Last(x => x.Kind == EventKind.PlayerDied);
            PlayerDiedPayload p = _bag.Decoder.Decode<PlayerDiedPayload>(EventKind.PlayerDied, e.Payload);

            Assert.AreEqual(SpawnCell.X, p.DeathCell.X);
            Assert.AreEqual(SpawnCell.Y, p.DeathCell.Y);
            Assert.AreEqual(SpawnCell.Z, p.DeathCell.Z,
                "从未跨格 ⇒ death_cell = 世界出生点固定格(EC 分支;story-002 的 spawn_anchor 同源)");
        }
    }
}
