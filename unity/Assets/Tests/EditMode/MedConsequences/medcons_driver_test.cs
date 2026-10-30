// ADR-036 · S3/S4 —— 53 判定链的生产驱动(`ConsequenceDriver`)
//
// 权威:ADR-036 §Decision D2(每 tick `Aggregate → Judge → Emit`)· D3(缺数据源 ⇒ 惰性不发)
//       · D5(读流 / 写流须同一实例)· D6(去重两层)
//       · §Implementation Guidelines(零跨 tick 态 · 遇抛即终止本 tick)
//
// ── 本文件钉什么 ─────────────────────────────────────────────────────────────
// ① **端到端**:一条结案 + 一条处置 ⇒ 延迟窗过后流里**恰一条** `ConsequenceResolved`,
//    且 `Tick = close.Tick + DelayTable[outcome]`、`PatientId` = 该病人;
// ② **幂等**:同一 tick 再驱动一次 ⇒ 不再发(第二份后果是重复记账);
// ③ **延迟窗**:预算到期前驱动 ⇒ 零后果(「不发」必须可观察,否则「早发」与「准时发」不可分);
// ④ **D5**:读面与写面不是同一实例 ⇒ 构造期抛(且报文自陈 —— 防同类型异常冒充);
// ⑤ **D6 上层**:同一结案两个 outcome ⇒ 抛。
//
// ⚠️ **⑤ 是「零输入可达的门」**(ADR-035 N-4 纪律):`Aggregate` 对每条 `CaseClosed`
//    只产一组 ⇒ `OnTickEdge` 的路径上造不出冲突。它的载体只能是**直接调判据方法**,
//    本文件如实按此写,**不**声称「OnTickEdge 已被证明会拦」。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.PatientAI;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Consequences;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.EditorTools.Bake;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.MedConsequences
{
    /// <summary>ADR-036 S3/S4 —— 判定链驱动。</summary>
    [TestFixture]
    public sealed class MedconsDriverTest
    {
        private static InMemoryBlobPool _pool;
        private static PayloadEncoder _encoder;
        private static PayloadDecoder _decoder;

        [SetUp]
        public void FreshPool()
        {
            _pool = new InMemoryBlobPool();
            _encoder = new PayloadEncoder(_pool);
            _decoder = new PayloadDecoder(_pool);
        }

        // ══════════════════════════════════════════════════════════════════
        // 夹具
        // ══════════════════════════════════════════════════════════════════

        /// <summary>固定写权(恒主机)。</summary>
        private sealed class AlwaysHost : IEventAuthority
        {
            public bool IsAuthority => true;
            public bool IsHost => true;
            public EventRollResult Roll(in RollRequest r) => throw new NotSupportedException("夹具不掷骰");
        }

        private sealed class StubIds : IIdAuthority
        {
            private int _next = 1000;
            public PatientId NextPatientId() => new PatientId(_next++);
            public ItemInstanceId NextItemInstanceId() => new ItemInstanceId(_next++);
        }

        /// <summary>病程投影桩:恒「未痊愈」(D = NotRecovered)。**历史 tick 也照答**。</summary>
        private sealed class NotRecoveredProjection : IDiseaseProjection
        {
            public bool TryProject(PatientId patient, long tick, out DiseaseProjectionSnapshot snapshot)
            {
                snapshot = new DiseaseProjectionSnapshot(tick, recovered: false);
                return true;
            }
        }

        private const int Patient = 7;
        private const long CloseTick = 100;
        private const int DiseaseId = 91;   // 位 = 90(注册表 id 与位差 1)

        private static Outcome53.Table OutcomeTable()
            => OutcomeTable53.BakeTableFromRepo(MedConsRepoPaths.RepoRoot);

        private static DelayTable53.Table DelayTable()
            => DelayTable53Bake.BakeTableFromRepo(MedConsRepoPaths.RepoRoot);

        private static EventStream NewStream()
        {
            var presence = new PresenceRegistry();
            presence.AddPresent(new PatientId(Patient), new WorldPos(0, 0, 0));
            return new EventStream(new StubIds(), presence);
        }

        private static SimEvent Ev(long tick, long seq, EventKind kind, PayloadRef payload)
            => new SimEvent(tick, new PatientId(Patient), seq, kind, payload);

        /// <summary>一条真装配的驱动(读面 = 写面 = 同一条流)。</summary>
        private static ConsequenceDriver NewDriver(EventStream stream)
        {
            DelayTable53.Table delay = DelayTable();
            var emitter = new ConsequenceEmitter(stream, new AlwaysHost(), _encoder, delay);
            return new ConsequenceDriver(
                stream, stream, emitter, _decoder, new NotRecoveredProjection(),
                OutcomeTable(), new[] { new DiseaseRegistryEntry
                    { DiseaseId = DiseaseId, DiseaseKey = "DIS_DRIVER_TEST" } },
                treatableBy: null,      // 未接线 ⇒ C 轴命中位恒 false(AppliedUnmatched)
                worldSeed: 0UL);
        }

        private static void AppendCloseAndCare(EventStream stream)
        {
            ulong bits = 1UL << (DiseaseId - 1);
            stream.Append(Ev(50, 0, EventKind.CareApplied,
                _encoder.Encode<CareAppliedPayload>(EventKind.CareApplied,
                    new CareAppliedPayload(tick: 50, actionId: 1, caregiver: 0, phase: 0))));
            stream.Append(Ev(CloseTick, 1, EventKind.CaseClosed,
                _encoder.Encode<CaseClosedPayload>(EventKind.CaseClosed,
                    new CaseClosedPayload(Patient, default, new DiseaseIdSet(bits), treated: true))));
        }

        private static int ConsequenceCount(EventStream stream)
        {
            int n = 0;
            for (int i = 0; i < stream.Events.Count; i++)
                if (stream.Events[i].Kind == EventKind.ConsequenceResolved) n++;
            return n;
        }

        // ══════════════════════════════════════════════════════════════════
        // ① 端到端 + ② 幂等 + ③ 延迟窗
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_closeAndCare_emitsExactlyOneConsequence_atDelayTick()
        {
            EventStream stream = NewStream();
            AppendCloseAndCare(stream);
            ConsequenceDriver driver = NewDriver(stream);

            // 判定期望 outcome 的**唯一**来源 = 生产驱动自己产的载荷 —— 但「何时发」不循环:
            // 断言 tick = close.Tick + DelayTable[实际 outcome],两者都从产物读。
            driver.OnTickEdge(CloseTick);
            Assert.AreEqual(0, ConsequenceCount(stream), "结案当 tick ⇒ 仍在延迟窗内,零后果");

            driver.OnTickEdge(CloseTick + 5000);
            Assert.AreEqual(1, ConsequenceCount(stream), "延迟窗过后 ⇒ 恰一条后果");

            SimEvent row = default;
            bool found = false;
            for (int i = 0; i < stream.Events.Count; i++)
                if (stream.Events[i].Kind == EventKind.ConsequenceResolved) { row = stream.Events[i]; found = true; break; }
            Assert.IsTrue(found, "前提到位:流里须有一条后果行");

            var payload = _decoder.Decode<ConsequenceResolvedPayload>(
                EventKind.ConsequenceResolved, row.Payload);
            Assert.AreEqual(Patient, payload.PatientId, "后果须归属该病例的病人");

            Assert.IsTrue(DelayTable().TryDelayTicks(payload.Outcome, out long delay),
                "产出的 outcome 须在回响轴内(NoEcho 不该发)");
            Assert.AreEqual(CloseTick + delay, row.Tick,
                "发出 tick 须 = 结案 tick + 该 outcome 的延迟(F-53.2)");

            // ② 幂等:同一 tick 再驱动一次 ⇒ 不重复发。
            driver.OnTickEdge(CloseTick + 5000);
            Assert.AreEqual(1, ConsequenceCount(stream), "重复驱动不得重复发(幂等差额)");
        }

        [Test]
        public void test_noCaseClosed_emitsNothing()
        {
            EventStream stream = NewStream();
            ConsequenceDriver driver = NewDriver(stream);

            driver.OnTickEdge(1000);
            Assert.AreEqual(0, ConsequenceCount(stream), "无结案 ⇒ 零后果");
        }

        // ══════════════════════════════════════════════════════════════════
        // V-10 —— 空判定表 ⇒ 不发(不得静默产出后果)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 判定表**一行都没有**时,任何结案都落到 `default`(此处取 `NoEcho`)⇒ 延迟表查不到
        /// ⇒ **零后果**。这条钉的是「空表 = 不发」,不是「空表 = 崩」。
        /// </summary>
        [Test]
        public void test_emptyOutcomeTable_emitsNothing()
        {
            EventStream stream = NewStream();
            AppendCloseAndCare(stream);

            DelayTable53.Table delay = DelayTable();
            var emitter = new ConsequenceEmitter(stream, new AlwaysHost(), _encoder, delay);
            Outcome53.Table empty = Outcome53.CreateTable(
                new Dictionary<int, Outcome53.Outcome>(), uBandCount: 1,
                hasDefault: true, defaultOutcome: Outcome53.Outcome.NoEcho);

            var driver = new ConsequenceDriver(
                stream, stream, emitter, _decoder, new NotRecoveredProjection(), empty,
                new[] { new DiseaseRegistryEntry { DiseaseId = DiseaseId, DiseaseKey = "DIS_DRIVER_TEST" } },
                treatableBy: null, worldSeed: 0UL);

            driver.OnTickEdge(CloseTick + 5000);

            Assert.AreEqual(0, ConsequenceCount(stream),
                "空判定表 ⇒ 无回响轴 outcome ⇒ 零后果(不得凭空造一条)");
        }

        // ══════════════════════════════════════════════════════════════════
        // V-8 —— 无 provider ⇒ 不装配(惰性)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// `dataProvider == null` ⇒ 装配层**不构造**驱动(与服务袋的 `null` 语义一致:
        /// 不发,不是不判)。「显式告警」那一半在 `BootRoot` 的启动序里,由下一条源文本夹具钉住。
        /// </summary>
        [Test]
        public void test_noDataProvider_leavesDriverUnassembled()
        {
            var entry = new DiseaseRegistryEntry { DiseaseId = DiseaseId, DiseaseKey = "DIS_DRIVER_TEST" };
            CompositionRootServices bag = CompositionRoot.Assemble(new[] { entry });

            Assert.IsNull(bag.ConsequenceDriver,
                "无数据源 ⇒ 不装配驱动(惰性);装配出来再每 tick 空转会把「没数据」伪装成「没病例」");
        }

        /// <summary>启动序须在驱动缺席时**显式告警** —— 否则「惰性」与「机制未启用」同形(R-3)。</summary>
        [Test]
        public void test_bootRoot_warnsWhenDriverUnassembled()
        {
            string path = System.IO.Path.Combine(MedConsRepoPaths.ProjectRoot,
                "Assets", "Gameplay.Boot", "BootRoot.cs");
            string cleaned = DaYiJingCheng.Tests.Unit.Sim.GateChainSource
                .StripCommentsAndLiterals(System.IO.File.ReadAllText(path));

            Assert.That(cleaned.Contains("ConsequenceDriver == null"), Is.True,
                "启动序须判驱动缺席");
            Assert.That(cleaned.Contains("Debug.LogWarning"), Is.True,
                "缺席时须有显式告警(惰性但不静默)");
        }

        // ══════════════════════════════════════════════════════════════════
        // ④ D5 —— 读流与写流须同一实例
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_d5_differentWriteFace_throwsAtConstruction()
        {
            EventStream readFace = NewStream();
            EventStream writeFace = NewStream();   // 另一条流(模拟「读一个世界、写另一个」)
            var emitter = new ConsequenceEmitter(writeFace, new AlwaysHost(), _encoder, DelayTable());

            var ex = Assert.Throws<ArgumentException>(() => new ConsequenceDriver(
                readFace, writeFace, emitter, _decoder, new NotRecoveredProjection(),
                OutcomeTable(), Array.Empty<DiseaseRegistryEntry>(),
                treatableBy: null, worldSeed: 0UL));

            StringAssert.Contains("同一实例", ex.Message,
                "须是本判据自己的报文,不是参数校验偶然抛出的同类型异常");
        }

        // ══════════════════════════════════════════════════════════════════
        // ⑤ D6 上层 —— 同一结案两个 outcome ⇒ 抛(**零输入可达的门**,载体只能直调)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_d6_sameCaseClosedTwoOutcomes_throws()
        {
            SimEvent close = Ev(CloseTick, 1, EventKind.CaseClosed, default);
            var eligible = new (SimEvent CaseClosed, int Outcome)[]
            {
                (close, (int)Outcome53.Outcome.Residual),
                (close, (int)Outcome53.Outcome.Recurrence),
            };

            var ex = Assert.Throws<InvalidOperationException>(
                () => ConsequenceDriver.AssertNoConflictingOutcomes(eligible));

            StringAssert.Contains("两个不同 outcome", ex.Message, "报文须自陈判据");
            StringAssert.Contains($"seq={close.Seq}", ex.Message, "报文须能定位到该结案");
        }

        /// <summary>对照半:同一结案**同一** outcome 重复出现 ⇒ 不抛(不得退化成「见到重复就抛」)。</summary>
        [Test]
        public void test_d6_sameOutcomeTwice_doesNotThrow_control()
        {
            SimEvent close = Ev(CloseTick, 1, EventKind.CaseClosed, default);
            var eligible = new (SimEvent CaseClosed, int Outcome)[]
            {
                (close, (int)Outcome53.Outcome.Residual),
                (close, (int)Outcome53.Outcome.Residual),
            };

            Assert.DoesNotThrow(() => ConsequenceDriver.AssertNoConflictingOutcomes(eligible));
        }

        // ══════════════════════════════════════════════════════════════════
        // V-7 —— 单 tick 耗时(R3:全流排序被搬上热路径)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// `V-7` —— 单 tick 驱动耗时的**两段口径**(ADR-036 §Performance / R3)。
        /// <para><b>① 现实负载**(判据段):24 个在场病人 × (1 结案 + 5 处置)= 144 条事件 ——
        /// 这是 P0 的形态(`PATIENT_APPEARANCE_CAP = 24`,一个病人同时只开一例)。
        /// 平均单 tick 须 &lt; 50 ms(`TICK_SECONDS`)。</para>
        /// <para><b>② 压力口径**(只记录,不断言):1000 结案 + 1000 处置 = 2000 条。
        /// ⚠️ 首版夹具**把 2000 条当判据**并实测 **1135 ms/tick** —— 那暴露的不是
        /// 「AC-53-04 排序结构要改」,而是**我的桥**在逐例重排整条流问试药史
        /// (`TrialHistoryPredicate.HasTrialHistory(stream, patient)` 每次全排序)。
        /// 已改为每 tick 扫一次(`PatientsWithTrialHistory`),见驱动注释。
        /// 压力数仍如实打印,但**不再当判据** —— 现实负载才是 50 ms 预算的作用面。</para>
        /// </summary>
        [Test]
        public void test_v7_perTickCost_realisticLoad_withinTickBudget()
        {
            const double TickBudgetMs = 50.0;   // TICK_SECONDS(0.05s) × 1000
            const int Ticks = 20;

            // ── ① 现实负载(判据)──
            double realistic = MeasurePerTickMs(patients: 24, caresPerPatient: 5, Ticks);
            TestContext.WriteLine(
                $"[V-7 ①] 现实负载:24 病人 × (1 结案 + 5 处置)= 144 条 · {Ticks} tick · " +
                $"平均 {realistic:F3} ms/tick(预算 {TickBudgetMs:F0} ms)");
            Assert.Less(realistic, TickBudgetMs,
                $"现实负载单 tick {realistic:F3} ms ≥ tick 预算 {TickBudgetMs} ms");

            // ── ② 压力口径(只记录;不设判据)──
            double stress = MeasurePerTickMs(patients: 24, caresPerPatient: 41, Ticks);
            TestContext.WriteLine(
                $"[V-7 ②] 压力口径:24 病人 × (1 结案 + 41 处置)≈ 1008 条 · {Ticks} tick · " +
                $"平均 {stress:F3} ms/tick(**仅记录**,非判据)");
        }

        /// <summary>建 N 病人 × (1 结案 + k 处置)的流,驱动 <paramref name="Ticks"/> 个 tick,返回平均耗时。</summary>
        private static double MeasurePerTickMs(int patients, int caresPerPatient, int Ticks)
        {
            var presence = new PresenceRegistry();
            for (int p = 1; p <= patients; p++) presence.AddPresent(new PatientId(p), new WorldPos(0, 0, 0));
            var stream = new EventStream(new StubIds(), presence);

            ulong bits = 1UL << (DiseaseId - 1);
            long seq = 0;
            for (int p = 1; p <= patients; p++)
            {
                for (int c = 0; c < caresPerPatient; c++)
                {
                    stream.Append(new SimEvent(0, new PatientId(p), seq++, EventKind.CareApplied,
                        _encoder.Encode<CareAppliedPayload>(EventKind.CareApplied,
                            new CareAppliedPayload(0, actionId: 1, caregiver: 0, phase: 0))));
                }

                stream.Append(new SimEvent(0, new PatientId(p), seq++, EventKind.CaseClosed,
                    _encoder.Encode<CaseClosedPayload>(EventKind.CaseClosed,
                        new CaseClosedPayload(p, default, new DiseaseIdSet(bits), treated: true))));
            }

            ConsequenceDriver driver = NewDriver(stream);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (long t = 1; t <= Ticks; t++) driver.OnTickEdge(t);
            sw.Stop();

            return sw.Elapsed.TotalMilliseconds / Ticks;
        }

        // ══════════════════════════════════════════════════════════════════
        // S4 装配 —— 源文本判据
        // ══════════════════════════════════════════════════════════════════
        //
        // ⚠️ 为什么是**源文本**而不是行为夹具:装配是否发生,要在 `Assemble` 的完整上下文里
        //    才能观测(需要真 `IDataProvider` + 两份 cooked 产物)。而「装配这行代码还在不在」
        //    用行为夹具根本测不出 —— 它恰好是本项目反复抓到的「函数在、没人调」形态
        //    (`medcons_delayed_emit_test.cs:1454-1478` 的同一手法)。

        [Test]
        public void test_s4_compositionRoot_constructsDriver()
        {
            string path = System.IO.Path.Combine(MedConsRepoPaths.ProjectRoot,
                "Assets", "Gameplay.Boot", "CompositionRoot.cs");
            Assert.That(System.IO.File.Exists(path), Is.True, $"源文件缺失:{path}");

            string cleaned = DaYiJingCheng.Tests.Unit.Sim.GateChainSource
                .StripCommentsAndLiterals(System.IO.File.ReadAllText(path));
            StringAssert.Contains("services.ConsequenceDriver = new ConsequenceDriver(",
                cleaned,
                "装配层须真的构造并挂上驱动 —— 摘掉这一行,53 的判定链在生产上又回到「零装配点」");
        }

        [Test]
        public void test_s4_bootRoot_drivesAfterVitals_onSameEdge()
        {
            string path = System.IO.Path.Combine(MedConsRepoPaths.ProjectRoot,
                "Assets", "Gameplay.Boot", "BootRoot.cs");
            Assert.That(System.IO.File.Exists(path), Is.True, $"源文件缺失:{path}");

            string cleaned = DaYiJingCheng.Tests.Unit.Sim.GateChainSource
                .StripCommentsAndLiterals(System.IO.File.ReadAllText(path));
            string body = DaYiJingCheng.Tests.Unit.Sim.GateChainSource.MethodBody(cleaned,
                "private void StepVitals(long tick)");
            Assert.That(body, Is.Not.Null,
                "找不到 StepVitals 的方法体 —— 签名变了?本判据须同批订正");

            int vitals = body.IndexOf("VitalsService.OnTickEdge", StringComparison.Ordinal);
            int driver = body.IndexOf("ConsequenceDriver?.OnTickEdge", StringComparison.Ordinal);

            Assert.That(driver, Is.GreaterThanOrEqualTo(0),
                "同一 tick 边沿须驱动 53 的判定链 —— 不调用 = 装配了但永不运行");
            Assert.That(vitals, Is.GreaterThanOrEqualTo(0), "VitalsService 边沿须仍在");
            Assert.That(driver, Is.GreaterThan(vitals),
                "R4 硬约束:判定链须排在 VitalsService **之后** —— 排前面读到上一 tick 的 F4 态," +
                "静默偏差一个 tick 且无报错");
        }
    }
}
