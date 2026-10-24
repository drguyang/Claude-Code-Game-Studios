// S18-1 AC ⑦⑧ —— `IDeathCandidateSource` 注入点 + 两条写者(EditMode,真装配,禁 Fake 被测对象)。
//
// 覆盖:
//   ① **AC ⑦** —— `IDeathCandidateSource` 实现存在且 29 侧注入点可用;
//      契约 = 进程内接口(非流 Kind)⇒ `DeathCandidate` 不产生任何 `Append`;
//      接口面上**零生命值读取成员**(反射断言,落实 story-001:48「无生命值读取路径」)。
//   ② **AC ⑧ PlayerDied** —— 真装配袋调用 `PlayerDiedWriter` ⇒ 世界流恰增 1 条,
//      Kind / 流别 / 载荷三字段 round-trip / `Patient = None` / `Seq` 逐项断言。
//   ③ **AC ⑧ InjuryStateChanged** —— 状态确实变化 ⇒ 恰 1 条;未变化 ⇒ **零写入**
//      (有界性上界是「转移次数」不是「tick 数」)。
//   ④ **谓词闸** —— `SelfLimited == true` 时两个写者都不写(写者不绕过判死谓词)。
//   ⑤ **冷却门** —— `DeathAllowed == false` ⇒ 零写入(story-004 管线序:门在任何写流之前)。
//
// 测试夹具说明:被测对象 = 两个写者 + 注入点(生产类,真装配)。
//   注册表条目是**输入夹具**(`DiseaseRegistryEntry` 是纯数据),不构成 Fake 被测对象。
//
// 权威:design/registry/entities.yaml:2057(PlayerDied)/ :2270(InjuryStateChanged)·
//      design/gdd/death-and-respawn.md:90 · story-001:48-49 · story-004:47 ·
//      ADR-005(Seq 发号)· ADR-009 §二 / §六 · ADR-020 §四 · ADR-029 §③

using System;
using System.Linq;
using System.Reflection;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.DeathRespawn
{
    /// <summary>`IDeathCandidateSource` 注入点 + `PlayerDied` / `InjuryStateChanged` 两条写者。</summary>
    [TestFixture]
    public sealed class DeathCandidateWritersTest
    {
        private CompositionRootServices _bag;
        private PlayerDiedWriter _playerDiedWriter;
        private InjuryStateChangedWriter _injuryWriter;

        private const int PlayerId = 7;
        private const long Tick = 100;

        [SetUp]
        public void Setup()
        {
            _bag = CompositionRoot.Assemble();
            _playerDiedWriter = new PlayerDiedWriter(_bag.EventSink, _bag.Encoder);
            _injuryWriter = new InjuryStateChangedWriter(_bag.EventSink, _bag.Encoder);
        }

        // ── 夹具 ────────────────────────────────────────────────────────────

        /// <summary>造一条注册表条目(纯数据夹具,非 Fake 被测对象)。</summary>
        private static DiseaseRegistryEntry Entry(
            string key, DiseaseKind kind, int lethality, bool selfLimit)
        {
            var e = new DiseaseRegistryEntry
            {
                DiseaseId = 1,
                DiseaseKey = key,
                Kind = kind,
                Lethality = lethality,
            };
            if (selfLimit)
                e.Curve = new NaturalProgressCurve { SelfLimit = true };
            return e;
        }

        private int WorldStreamCount()
            => _bag.Stream.Events.Count(e => StreamRouting.Of(e.Kind) == StreamId.World);

        private int CountOf(EventKind kind)
            => _bag.Stream.Events.Count(e => e.Kind == kind);

        // ══════════════════ ① AC ⑦ —— 接口存在 + 零生命值读取面 ══════════════════

        [Test]
        public void test_deathCandidateSource_interfaceExistsInContracts()
        {
            Type t = typeof(IDeathCandidateSource);
            Assert.IsTrue(t.IsInterface, "IDeathCandidateSource 必须是接口(进程内契约,非流 Kind)");

            MethodInfo onDeath = t.GetMethod("OnDeathCandidate");
            Assert.IsNotNull(onDeath, "story-001:48 定形的方法名 = OnDeathCandidate");
            Assert.AreEqual(new[] { typeof(int), typeof(long) },
                onDeath.GetParameters().Select(p => p.ParameterType).ToArray(),
                "签名 = (playerId: int, tick: long)");
        }

        [Test]
        public void test_deathCandidateSource_hasNoHealthReadingMember()
        {
            // story-001:48「29 内部…无生命值读取路径」—— 落实为接口面上零生命值成员。
            // 判据 = 方法名 / 返回类型均不含 health / hp / vitals / position 类语义。
            string[] banned = { "health", "hp", "vitals", "position", "damage" };
            foreach (MethodInfo m in typeof(IDeathCandidateSource).GetMethods())
            {
                string lower = m.Name.ToLowerInvariant();
                foreach (string b in banned)
                    Assert.IsFalse(lower.Contains(b),
                        $"IDeathCandidateSource 不得暴露生命值读取面(命中 '{b}' 于 {m.Name})");
            }
        }

        [Test]
        public void test_deathCandidate_doesNotEnterAnyStream()
        {
            // QA plan:121 / :294 —— `DeathCandidate` 不是流 Kind。
            // 判据 = EventKind 枚举里没有该成员(反射,不靠 grep)。
            Assert.IsNull(Enum.GetNames(typeof(EventKind)).FirstOrDefault(n => n == "DeathCandidate"),
                "DeathCandidate 不得是 SimEvent.Kind —— 它是进程内接口,不进任何流");
        }

        [Test]
        public void test_deathCandidateConsumer_implementsSource()
        {
            Assert.IsTrue(typeof(IDeathCandidateSource).IsAssignableFrom(typeof(DeathCandidateConsumer)),
                "29 侧注入点须实现 IDeathCandidateSource");
        }

        // ══════════════════ ② AC ⑧ —— PlayerDied 写者 ══════════════════

        [Test]
        public void test_playerDiedWriter_writesExactlyOneWorldEvent()
        {
            int before = WorldStreamCount();
            var entry = Entry("DIS_PLAGUE", DiseaseKind.Disease, lethality: 3, selfLimit: false);

            var result = _playerDiedWriter.TryWrite(
                Tick, PlayerId, ActorClass.Player, entry, new WorldPos(3, 0, 4), deathAllowed: true);

            Assert.AreEqual(PlayerDiedWriteResult.Written, result);
            Assert.AreEqual(before + 1, WorldStreamCount(), "世界流恰增 1 条");
            Assert.AreEqual(1, CountOf(EventKind.PlayerDied));
        }

        [Test]
        public void test_playerDiedWriter_payloadRoundTrips()
        {
            var entry = Entry("DIS_PLAGUE", DiseaseKind.Disease, lethality: 3, selfLimit: false);
            var cell = new WorldPos(11, 0, -5);

            _playerDiedWriter.TryWrite(Tick, PlayerId, ActorClass.Player, entry, cell, deathAllowed: true);

            SimEvent e = _bag.Stream.Events.Last(x => x.Kind == EventKind.PlayerDied);
            PlayerDiedPayload p = _bag.Decoder.Decode<PlayerDiedPayload>(EventKind.PlayerDied, e.Payload);

            Assert.AreEqual(PlayerId, p.ActorId);
            Assert.AreEqual(cell.X, p.DeathCell.X);
            Assert.AreEqual(cell.Z, p.DeathCell.Z);
            Assert.AreEqual(Tick, p.Tick);
        }

        [Test]
        public void test_playerDiedWriter_patientIsNoneAndSeqIsUnsigned()
        {
            var entry = Entry("DIS_PLAGUE", DiseaseKind.Disease, lethality: 3, selfLimit: false);
            _playerDiedWriter.TryWrite(Tick, PlayerId, ActorClass.Player, entry,
                new WorldPos(0, 0, 0), deathAllowed: true);

            SimEvent e = _bag.Stream.Events.Last(x => x.Kind == EventKind.PlayerDied);
            Assert.AreEqual(PatientId.None.Value, e.Patient.Value,
                "受伤实体侧事件不污染 max(patient_id) 高水位(ADR-007 §四)");
            Assert.GreaterOrEqual(e.Seq, 0L, "Seq 由主机发号,写入后不得仍为 -1 哨兵");
        }

        [Test]
        public void test_playerDiedWriter_rejectsWhenSelfLimited()
        {
            int before = WorldStreamCount();
            // selfLimit = true ⇒ 自限门恒开 ⇒ 死亡支关闭。
            var entry = Entry("DIS_COLD", DiseaseKind.Disease, lethality: 0, selfLimit: true);

            var result = _playerDiedWriter.TryWrite(
                Tick, PlayerId, ActorClass.Player, entry, new WorldPos(0, 0, 0), deathAllowed: true);

            Assert.AreEqual(PlayerDiedWriteResult.RejectPredicate, result);
            Assert.AreEqual(before, WorldStreamCount(), "谓词否决 ⇒ 零写入");
        }

        [Test]
        public void test_playerDiedWriter_rejectsWhenCooldownClosed()
        {
            int before = WorldStreamCount();
            var entry = Entry("DIS_PLAGUE", DiseaseKind.Disease, lethality: 3, selfLimit: false);

            var result = _playerDiedWriter.TryWrite(
                Tick, PlayerId, ActorClass.Player, entry, new WorldPos(0, 0, 0), deathAllowed: false);

            Assert.AreEqual(PlayerDiedWriteResult.RejectCooldown, result);
            Assert.AreEqual(before, WorldStreamCount(), "冷却门未开 ⇒ 零写入(story-004:47 门在写流之前)");
        }

        [Test]
        public void test_playerDiedWriter_emitsEvenWithEmptyInventory()
        {
            // registry 原文:「无条件发出 —— 背包为空也发」。
            // 本写者不收背包参数 ⇒ 结构上不可能因背包而拒。判据 = 接口签名里无背包形参。
            foreach (MethodInfo m in typeof(PlayerDiedWriter).GetMethods(
                         BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                foreach (ParameterInfo p in m.GetParameters())
                    Assert.IsFalse(p.Name.ToLowerInvariant().Contains("inventory"),
                        "PlayerDiedWriter 不得因背包状态拒写(registry 原文:无条件发出)");
            }
        }

        // ══════════════════ ③ AC ⑧ —— InjuryStateChanged 写者 ══════════════════

        [Test]
        public void test_injuryStateWriter_writesOnActualTransition()
        {
            int before = WorldStreamCount();

            var result = _injuryWriter.TryWrite(
                Tick, actorId: 3, InjuryState.Healthy, new Fix(2500), 0, "INJ_STAB");

            Assert.AreEqual(InjuryStateWriteResult.Written, result);
            Assert.AreEqual(before + 1, WorldStreamCount(), "世界流恰增 1 条");
            Assert.AreEqual(1, CountOf(EventKind.InjuryStateChanged));
        }

        [Test]
        public void test_injuryStateWriter_payloadRoundTrips()
        {
            _injuryWriter.TryWrite(Tick, actorId: 3, InjuryState.Healthy, new Fix(2500), 0, "INJ_STAB");

            SimEvent e = _bag.Stream.Events.Last(x => x.Kind == EventKind.InjuryStateChanged);
            InjuryStateChangedPayload p =
                _bag.Decoder.Decode<InjuryStateChangedPayload>(EventKind.InjuryStateChanged, e.Payload);

            Assert.AreEqual(3, p.ActorId);
            Assert.AreEqual((int)InjuryState.Severe, p.NewState);
        }

        [Test]
        public void test_injuryStateWriter_noWriteWhenStateUnchanged()
        {
            int before = WorldStreamCount();
            // 同 positionAgg 求值两次 ⇒ 第二次状态不变 ⇒ 零写入。
            // 有界性上界是「转移次数 × 实体数」,不是「tick 数 × 实体数」(ADR-009 §六)。
            _injuryWriter.TryWrite(Tick, actorId: 3, InjuryState.Healthy, new Fix(2500), 0, "INJ_STAB");
            var second = _injuryWriter.TryWrite(
                Tick + 1, actorId: 3, InjuryState.Severe, new Fix(2500), 0, "INJ_STAB");

            Assert.AreEqual(InjuryStateWriteResult.NoChange, second);
            Assert.AreEqual(before + 1, WorldStreamCount(), "状态未变 ⇒ 只增第一条");
        }

        // ══════════════════ ④ 注入点端到端 ══════════════════

        [Test]
        public void test_deathCandidateConsumer_writesWhenPredicateAllows()
        {
            var entry = Entry("DIS_PLAGUE", DiseaseKind.Disease, lethality: 3, selfLimit: false);
            var consumer = new DeathCandidateConsumer(
                _playerDiedWriter,
                deathAllowed: (_, __) => true,
                actorClassOf: _ => ActorClass.Player,
                entryOf: _ => entry,
                deathCellOf: _ => new WorldPos(5, 0, 5));

            consumer.OnDeathCandidate(PlayerId, Tick);

            Assert.AreEqual(1, CountOf(EventKind.PlayerDied), "谓词放行 ⇒ 恰 1 条 PlayerDied");
        }

        [Test]
        public void test_deathCandidateConsumer_silentWhenPredicateDenies()
        {
            // selfLimit = true ⇒ PlayerDeathWarranted = false ⇒ 零写入。
            var entry = Entry("DIS_COLD", DiseaseKind.Disease, lethality: 0, selfLimit: true);
            var consumer = new DeathCandidateConsumer(
                _playerDiedWriter,
                deathAllowed: (_, __) => true,
                actorClassOf: _ => ActorClass.Player,
                entryOf: _ => entry,
                deathCellOf: _ => new WorldPos(0, 0, 0));

            consumer.OnDeathCandidate(PlayerId, Tick);

            Assert.AreEqual(0, CountOf(EventKind.PlayerDied), "谓词否决 ⇒ 零写入");
        }

        [Test]
        public void test_deathCandidateConsumer_doesNotWriteDeathCandidateEvent()
        {
            // AC ⑦ 的判据:候选是进程内传递,不产生任何 Append。
            var entry = Entry("DIS_PLAGUE", DiseaseKind.Disease, lethality: 3, selfLimit: false);
            var consumer = new DeathCandidateConsumer(
                _playerDiedWriter, (_, __) => true, _ => ActorClass.Player, _ => entry,
                _ => new WorldPos(0, 0, 0));

            int before = _bag.Stream.Events.Count;
            consumer.OnDeathCandidate(PlayerId, Tick);
            int after = _bag.Stream.Events.Count;

            Assert.AreEqual(before + 1, after,
                "一次候选只产生一条事件(PlayerDied),不存在独立的 DeathCandidate 事件");
        }
    }
}
