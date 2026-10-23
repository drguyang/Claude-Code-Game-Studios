// story-008 —— 死亡候选产出侧(`IDeathCandidateSink`)与 F4 接线(2026-10-24)。
//
// 权威来源:
//   production/epics/disease-simulation/story-008-death-candidate-sink.md —— AC-9-21…28
//   design/gdd/death-and-respawn.md:103 —— 归属表「死亡候选的产出 = 9」
//   design/gdd/death-and-respawn.md:90  —— 「29 只消费一个布尔」
//   unity/Assets/Sim.Contracts/IDeathCandidateSource.cs:57-71 —— 接口本体
//   ADR-009 §二(候选不进流)· ADR-005(主机唯一 Step)
//
// ── 本文件钉的是什么 ──────────────────────────────────────────────────────────
// 本件之前:`IDeathCandidateSink` **零实现**、`Publish(` **全树零命中** —— 产出路径
// 在生产上从未存在过。本文件钉的是**它现在存在且三闸都对**:
//   · 单消费者(二次 Register 抛 / 未注册 Publish 静默);
//   · 转移闸(吸收态不重复发);
//   · 类别闸(病人死亡不产生候选 —— 这是防 bug,不是防回归)。
//
// ── 真装配,零 Fake 被测对象 ─────────────────────────────────────────────────
// 与 `f4_wiring_test` / `death_candidate_writers_test` 同款:
// `CompositionRoot.Assemble` → 真 `EventStream` + 真 `PayloadEncoder` + 真 `PatientSpawner`。
// 唯一的替身是**观察者**(`RecordingConsumer`),它记下被通知了什么 —— 它不是被测对象,
// 是被测对象**通知**的对象(与「Fake 被测对象」不是一回事)。
//
// ── 两个夹具,差一个形参 ─────────────────────────────────────────────────────
// `Rig.PlayerClass`(类别 = Player)与 `Rig.Assembled`(类别 = 装配默认 Patient)
// **病种 / 曲线 / 驱动 tick 数完全相同**,唯一差异是 `actorClassOf`。
// ⇒ 「Player 发、Patient 不发」这一对结论才不可被「路径根本是死的」满足(防假绿)。

using System;
using System.Collections.Generic;
using System.Linq;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.DiseaseSimulation;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.DiseaseSimulation
{
    /// <summary>story-008 —— 死亡候选产出侧 + F4 接线。</summary>
    [TestFixture]
    public sealed class DeathCandidateSinkTest
    {
        // ══════════════════════════════════════════════════════════════════
        // 夹具
        // ══════════════════════════════════════════════════════════════════

        /// <summary>观察者 —— 记下被通知的 (playerId, tick)。**不是被测对象**。</summary>
        private sealed class RecordingConsumer : IDeathCandidateSource
        {
            public readonly List<(int PlayerId, long Tick)> Received =
                new List<(int, long)>();

            public void OnDeathCandidate(int playerId, long tick)
                => Received.Add((playerId, tick));
        }

        /// <summary>
        /// 急性保持型致死病种 —— `position` 随 τ 上升后**恒定在 A_peak/SCALE**。
        /// 与 `f4_wiring_test.AcuteLethal` 同形(`A_peak/SCALE = 0.70 ≥ DEATH = 0.58`)。
        /// </summary>
        private static DiseaseRegistryEntry AcuteLethal(int diseaseId, string key)
        {
            return new DiseaseRegistryEntry
            {
                DiseaseId = diseaseId,
                DiseaseKey = key,
                Kind = DiseaseKind.Disease,
                Lethality = 2,
                Scale = new Fix(100 * Fix.OneRaw),
                Curve = new NaturalProgressCurve
                {
                    Incubation = 0,
                    APeak = new Fix(70 * Fix.OneRaw),
                    TauRise = new Fix(10 * Fix.OneRaw),
                    TauPeak = 100,
                    TauFall = new Fix(100 * Fix.OneRaw),
                    Plateau = false,
                    SelfLimit = false,        // 急性保持型 ⇒ Base 恒 = A_peak(τ ≥ τ_peak)
                    BoundaryMode = "monotone",
                },
            };
        }

        /// <summary>
        /// 驱动到「已死」所需的 tick 数。
        /// <para>⚠️ **不是「越阈就死」**:F4 死亡支还要求
        /// <c>t − last_intervention &gt; TREATMENT_WINDOW(3600)</c>(铁律一:不能一越线就死)。
        /// 取窗口 + 10,与 `f4_wiring_test.test_death_requiresWindowElapsed` 同款。</para>
        /// </summary>
        private static readonly long TicksToDeath = StateMachine.TREATMENT_WINDOW + 10;

        /// <summary>一条真装配的体征链 + 单病人 + 候选观察者。</summary>
        private sealed class Rig
        {
            public CompositionRootServices Bag;
            public DiseaseVitalsService Svc;
            public RecordingConsumer Consumer;
            public PatientId Patient;

            public void Drive(long ticks)
            {
                for (long t = 1; t <= ticks; t++) Svc.OnTickEdge(t);
            }

            public InjuryState Outcome() => Svc.GetOutcome(Patient);
        }

        /// <summary>
        /// 类别 = <see cref="ActorClass.Player"/> 的链。
        /// <para>只覆写**类别查询面**这一个形参;其余全部走真装配(`bag.Stream` /
        /// `bag.BlobPool` / 真 <see cref="PatientSpawner"/>)。</para>
        /// </summary>
        private static Rig OpenPlayerClass(DiseaseRegistryEntry entry, out RecordingConsumer consumer)
        {
            var bag = CompositionRoot.Assemble(new[] { entry });
            consumer = new RecordingConsumer();

            var sink = new DeathCandidateSink();
            sink.Register(consumer);

            var svc = new DiseaseVitalsService(
                bag.Stream, bag.BlobPool, new[] { entry }, 0UL,
                actorClassOf: _ => ActorClass.Player,
                deathCandidates: sink);

            return new Rig
            {
                Bag = bag,
                Svc = svc,
                Consumer = consumer,
                Patient = bag.PatientSpawner.SpawnNext(entry.DiseaseId, tick: 0),
            };
        }

        /// <summary>装配默认链(类别查询面 = `Patient`)—— 服务与 sink 都取自服务袋。</summary>
        private static Rig OpenAssembled(DiseaseRegistryEntry entry, out RecordingConsumer consumer)
        {
            var bag = CompositionRoot.Assemble(new[] { entry });
            consumer = new RecordingConsumer();
            bag.DeathCandidates.Register(consumer);

            return new Rig
            {
                Bag = bag,
                Svc = bag.VitalsService,
                Consumer = consumer,
                Patient = bag.PatientSpawner.SpawnNext(entry.DiseaseId, tick: 0),
            };
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-9-21 —— 实现存在 + 住 Sim + 零构造依赖
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_sink_implementsInterface_andLivesInSim()
        {
            Assert.IsTrue(typeof(IDeathCandidateSink).IsAssignableFrom(typeof(DeathCandidateSink)),
                "DeathCandidateSink 须实现 IDeathCandidateSink");
            Assert.AreEqual("DaYiJingCheng.Sim", typeof(DeathCandidateSink).Namespace,
                "产出侧须住 Sim(门 A 内侧)—— 装配层实现会让「谁产出候选」跨层");
        }

        [Test]
        public void test_sink_hasZeroConstructorDependencies()
        {
            // 门 A + 「零状态」的机械判据:无参构造必须可用。
            // 若将来有人给它塞 IEventSink / encoder,这条会红 —— 那是刻意的。
            Assert.DoesNotThrow(() => new DeathCandidateSink(),
                "产出侧构造依赖须 = 零(它不判死、不查表、不编码、不写流)");
        }

        [Test]
        public void test_simAssembly_referencesZeroEngineAssemblies()
        {
            // AC-9-21「零引擎引用」的**运行期**判据(与构建期 b2 门
            // `AssemblyGates.cs:189-220` 互补:那条在构建时红,这条在测试时红)。
            // 空集守卫是必须的 —— `GetReferencedAssemblies()` 返回空集会让 for 循环
            // 空洞为真(假绿),承 `pipeline_gate_test.cs:114` 的同款警告。
            var refs = typeof(DeathCandidateSink).Assembly.GetReferencedAssemblies()
                .Select(a => a.Name).ToList();
            Assert.IsNotEmpty(refs,
                "Sim 程序集引用集非空(空集会让下面的循环空洞为真 = 假绿)");

            foreach (string r in refs)
            {
                Assert.IsFalse(r.StartsWith("UnityEngine", StringComparison.Ordinal),
                    $"门 A:Sim 程序集不得引用引擎程序集,实测引用:{r}");
                Assert.IsFalse(r.StartsWith("UnityEditor", StringComparison.Ordinal),
                    $"门 A:Sim 程序集不得引用编辑器程序集,实测引用:{r}");
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-9-22 —— 单消费者
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_register_firstSucceeds()
        {
            var sink = new DeathCandidateSink();
            var consumer = new RecordingConsumer();

            Assert.DoesNotThrow(() => sink.Register(consumer));
            Assert.AreSame(consumer, sink.Consumer, "首注册后 Consumer 须指向它");
        }

        [Test]
        public void test_register_secondThrows()
        {
            var sink = new DeathCandidateSink();
            sink.Register(new RecordingConsumer());

            Assert.Throws<InvalidOperationException>(
                () => sink.Register(new RecordingConsumer()),
                "二次注册须**抛**,不得静默覆盖 —— 覆盖会让前一个消费者永远收不到候选");
        }

        [Test]
        public void test_register_nullThrows()
        {
            var sink = new DeathCandidateSink();
            Assert.Throws<ArgumentNullException>(() => sink.Register(null));
        }

        [Test]
        public void test_publish_withoutConsumer_isSilentAndDoesNotThrow()
        {
            var sink = new DeathCandidateSink();

            // 接口 :68 逐字:「未注册消费者时**静默丢弃**(P0 允许:29 未装配的构建里 9 照常跑)」。
            Assert.DoesNotThrow(() => sink.Publish(7, 100),
                "未注册时 Publish 须静默丢弃,不得抛 —— 29 未装配的构建里 9 要照常跑");
        }

        [Test]
        public void test_publish_forwardsToRegisteredConsumer()
        {
            var sink = new DeathCandidateSink();
            var consumer = new RecordingConsumer();
            sink.Register(consumer);

            sink.Publish(7, 100);

            Assert.AreEqual(1, consumer.Received.Count);
            Assert.AreEqual((7, 100L), consumer.Received[0]);
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-9-23 —— 生产调用点存在(接线)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_stepAll_publishesWhenEnteringDeceased()
        {
            var entry = AcuteLethal(70, "DIS_S8_LETHAL");
            Rig rig = OpenPlayerClass(entry, out RecordingConsumer consumer);

            rig.Drive(TicksToDeath);

            Assert.AreEqual(InjuryState.Deceased, rig.Outcome(),
                "前提:该病种在该 tick 数下须已判死(否则本测测的是别的东西)");
            Assert.AreEqual(1, consumer.Received.Count,
                "F4 转移进 Deceased 时须经生产调用点 Publish **一次**");
            Assert.AreEqual(rig.Patient.Value, consumer.Received[0].PlayerId,
                "候选的 actor_id 须是被判死的那个实体 id");
        }

        [Test]
        public void test_publishTickIsTheTransferTick()
        {
            var entry = AcuteLethal(71, "DIS_S8_LETHAL_TICK");
            Rig rig = OpenPlayerClass(entry, out RecordingConsumer consumer);

            long firstDeceased = -1;
            for (long t = 1; t <= TicksToDeath; t++)
            {
                rig.Svc.OnTickEdge(t);
                if (firstDeceased < 0 && rig.Outcome() == InjuryState.Deceased)
                    firstDeceased = t;
            }

            Assert.Greater(firstDeceased, 0, "前提:须在观测窗口内判死");
            Assert.AreEqual(1, consumer.Received.Count);
            Assert.AreEqual(firstDeceased, consumer.Received[0].Tick,
                "候选的 tick 须是**转移那一 tick**,不是首次求值 tick、也不是末 tick");
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-9-24 —— 类别闸(防 bug:病人死亡不得产生玩家候选)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_patientDeath_producesNoCandidate()
        {
            // ⚠️ 这是本件最关键的测试。装配层的 actorClassOf 恒 Patient(现状唯一真值),
            // 若产出点不设类别闸,病人死亡会铸出 actor_id = 病人 id 的 PlayerDied。
            var entry = AcuteLethal(72, "DIS_S8_PATIENT_NO_CANDIDATE");
            Rig rig = OpenAssembled(entry, out RecordingConsumer consumer);

            rig.Drive(TicksToDeath);

            Assert.AreEqual(InjuryState.Deceased, rig.Outcome(),
                "前提:病人确实死了(否则「零候选」是因为没死,不是因为类别闸)");
            Assert.AreEqual(0, consumer.Received.Count,
                "病人死亡**不得**产生死亡候选 —— 候选的语义是「**玩家**应死」");
        }

        [Test]
        public void test_playerClass_producesCandidate()
        {
            // 反向夹具:同一病种、同一 tick 数,仅类别改为 Player ⇒ 必须产生候选。
            // 缺它,上一条「病人零候选」可以被「候选路径根本是死的」满足 = 假绿。
            var entry = AcuteLethal(73, "DIS_S8_PLAYER_CANDIDATE");
            Rig rig = OpenPlayerClass(entry, out RecordingConsumer consumer);

            rig.Drive(TicksToDeath);

            Assert.AreEqual(InjuryState.Deceased, rig.Outcome(),
                "前提:类别改 Player 后仍须判死(否则两条测的不是同一件事)");
            Assert.AreEqual(1, consumer.Received.Count,
                "类别 = Player ⇒ 须产生候选(否则类别闸测的只是「全都不发」)");
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-9-25 —— 吸收态不重复发
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_deceasedIsAbsorbing_noRepeatedPublish()
        {
            var entry = AcuteLethal(74, "DIS_S8_NO_REPEAT");
            Rig rig = OpenPlayerClass(entry, out RecordingConsumer consumer);

            // 判死后继续驱动 50 tick —— Deceased 是吸收态,每 tick 都会再判出 Deceased。
            rig.Drive(TicksToDeath + 50);

            Assert.AreEqual(InjuryState.Deceased, rig.Outcome());
            Assert.AreEqual(1, consumer.Received.Count,
                "吸收态:判死后继续驱动 50 tick,候选数须**恒 = 1** —— " +
                "每 tick 发会让事件率退化成「tick 数 × 玩家数」");
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-9-26 / AC-9-27 —— 装配面
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_assemble_exposesSink()
        {
            var bag = CompositionRoot.Assemble(new[] { AcuteLethal(75, "DIS_S8_ASSEMBLE") });
            Assert.IsNotNull(bag.DeathCandidates,
                "CompositionRoot 须构造 DeathCandidateSink 并经服务袋暴露");
            Assert.IsInstanceOf<DeathCandidateSink>(bag.DeathCandidates);
        }

        [Test]
        public void test_assemble_sinkIsNotYetRegistered()
        {
            // ⚠️ 现状断言:29 的消费者依赖 story-004 的 DeathAllowed 门(仍 Ready)
            // ⇒ 装配层**不**注册。这条把「未注册」钉成**已知状态**而非疏忽。
            // 29 装配轮落地时本测须**同批**改为「已注册」—— 它是一道提醒,不是永久契约。
            var bag = CompositionRoot.Assemble(new[] { AcuteLethal(76, "DIS_S8_UNREGISTERED") });
            Assert.IsNull(((DeathCandidateSink)bag.DeathCandidates).Consumer,
                "P0 现状:消费者由 29 的装配轮注册;9 的装配层不代为注册");
        }

        // ══════════════════════════════════════════════════════════════════
        // 接线闸 —— 未接线时与接线前逐位一致
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_noSink_isZeroCandidate_andOutcomeUnchanged()
        {
            // `actorClassOf` / `deathCandidates` 都省 = 接线前的调用形态。
            // 这条钉住「退回原行为」:不抛、九态与接线前逐位一致。
            var entry = AcuteLethal(77, "DIS_S8_NO_SINK");
            var bag = CompositionRoot.Assemble(new[] { entry });
            var svc = new DiseaseVitalsService(
                bag.Stream, bag.BlobPool, new[] { entry }, 0UL);
            var patient = bag.PatientSpawner.SpawnNext(entry.DiseaseId, tick: 0);

            Assert.DoesNotThrow(() =>
            {
                for (long t = 1; t <= TicksToDeath; t++) svc.OnTickEdge(t);
            }, "未接线时 Step 不得因缺 sink 抛");

            Assert.AreEqual(InjuryState.Deceased, svc.GetOutcome(patient),
                "未接线时 F4 求值结论须与接线前逐位一致");
        }
    }
}
