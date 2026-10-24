// S19-1 判据 ④ 位置半 + 委托半的接线测试 —— `SaveComposer` + `CompositionRoot` 装配。
//
// 权威来源:
//   production/sprints/sprint-19.md §S19-1 判据 ④ 位置半(i)(ii)
//   production/qa/evidence/s19-1-fold-adjudication-2026-10-24.md §二(甲-a)· §三(字节半六条实测)
//   ADR-010 §六(Checkpoint 唯一写入口)· ADR-025 §①(Sim / Sim.Codec 互不可见)
//
// ─────────────────────────────────────────────────────────────────────────────
// ⚠️ 本文件**不**测判据 ④ 的字节半
// ─────────────────────────────────────────────────────────────────────────────
// 字节半(「`Checkpoint` 产出的字节里折叠行出现」)= **NOT-RUN**,归 `TD-persist-03`
// (用户 2026-10-24 裁定)。本文件里 `test_compose_*` 那几条证明的是
// 「组版器在**有折叠行要落盘**时 fail-loud 而不是写坏档」——
// 那**不是**字节半的替代品,而是它的**缺失证据**:正因为字节布局不存在,
// 组版器只能抛。**不得**把这几条绿读成「字节半已满足」。
//
// 写盘边界:本文件零写盘(纯内存组版)。

using System;
using System.Collections.Generic;
using System.Linq;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Tests.DiseaseSimulation;   // SyntheticDiseaseFixture
using NUnit.Framework;

namespace DaYiJingCheng.Tests.PersistenceService
{
    public class SaveComposerWiringTest
    {
        private const int PatientA = 7;
        private const int PatientB = 8;
        private const int DiseaseSelfLimit = 21;     // 非 plateau、非 lethal ⇒ 不豁免,可折
        private const int DiseasePlateau = 22;       // plateau 且 non-lethal ⇒ 豁免,不折
        private const long Seed = 0x5EEDL;

        private InMemoryBlobPool _pool;
        private PayloadEncoder _encoder;
        private PayloadDecoder _decoder;
        private Dictionary<int, DiseaseRegistryEntry> _registry;

        [SetUp]
        public void SetUp()
        {
            _pool = new InMemoryBlobPool();
            _encoder = new PayloadEncoder(_pool);
            _decoder = new PayloadDecoder(_pool);
            _registry = new Dictionary<int, DiseaseRegistryEntry>();

            _registry[DiseaseSelfLimit] = Entry(DiseaseSelfLimit, lethality: 1, plateau: false);
            _registry[DiseasePlateau] = Entry(DiseasePlateau, lethality: 0, plateau: true);
        }

        private static DiseaseRegistryEntry Entry(int id, int lethality, bool plateau)
            => new DiseaseRegistryEntry
            {
                DiseaseId = id,
                DiseaseKey = "D" + id,
                Kind = DiseaseKind.Disease,
                Lethality = lethality,
                Curve = new NaturalProgressCurve { Plateau = plateau },
            };

        // ══════════════════ 夹具 ══════════════════

        private SimEvent Onset(long tick, int patient, int disease)
            => new SimEvent(tick, new PatientId(patient), 0L, EventKind.DiseaseOnset,
                _encoder.Encode(EventKind.DiseaseOnset,
                    new DiseaseOnsetPayload(tick, disease, patient, Seed, 0L)));

        private SimEvent Outcome(long tick, int patient, int disease, PatientOutcome outcome)
            => new SimEvent(tick, new PatientId(patient), 0L, EventKind.PatientOutcomeChanged,
                _encoder.Encode(EventKind.PatientOutcomeChanged,
                    new PatientOutcomeChangedPayload(patient, disease, (int)outcome, 0L)));

        private SimEvent SkillGrown(long tick, int patient)
            => new SimEvent(tick, new PatientId(patient), 0L, EventKind.SkillGrown,
                _encoder.Encode(EventKind.SkillGrown,
                    new SkillGrownPayload(actorId: 1, patientId: patient, skillId: 1,
                                          objectId: DiseaseSelfLimit, noveltyClass: 0, level: 2)));

        /// <summary>病例流事件 —— `HasOpenCase` 只读 Kind 与 Patient,载荷留 default。</summary>
        private SimEvent CaseEvent(long tick, int patient, EventKind kind)
            => new SimEvent(tick, new PatientId(patient), 0L, kind, default);

        private FoldedPredicate Predicate(IReadOnlyList<SimEvent> events)
        {
            IReadOnlyList<SimEvent> history = SaveComposer.FilterByStream(events, StreamId.History);
            IReadOnlyList<SimEvent> cases = SaveComposer.FilterByStream(events, StreamId.Case);
            return new FoldedPredicate(
                historyStream: () => history,
                caseStream: () => cases,
                diseaseLookup: id => _registry.TryGetValue(id, out var e) ? e : null,
                decoder: _decoder);
        }

        // ══════════════════════════════════════════════════════════════════
        // 判据 ④ 位置半 (i) —— `HistoryFolder.Fold` 有生产调用点
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_historyFolderFold_hasProductionCallSite_outsideSimCodec()
        {
            // 本测试是 (i) 的**可复算判据**在测试面的表达:
            // 扫全树找 `HistoryFolder.Fold` 的调用点,要求 ≥1 行且不在 Sim.Codec/ 内。
            // 与 sprint-19 的 grep 判据同源,但用文件系统直读而非 shell —— 两者须同结论。
            string repoRoot = RepoRoot();
            string assets = System.IO.Path.Combine(repoRoot, "unity", "Assets");

            var hits = new List<string>();
            foreach (string file in System.IO.Directory.GetFiles(assets, "*.cs",
                         System.IO.SearchOption.AllDirectories))
            {
                string rel = file.Replace('\\', '/');
                if (rel.Contains("/Tests/")) continue;              // 测试不算生产调用链

                string[] lines = System.IO.File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    // 跳过注释行 —— 注释不是调用点(`SaveService.cs:49` 就有一处纯注释提及)。
                    string trimmed = lines[i].TrimStart();
                    if (trimmed.StartsWith("//") || trimmed.StartsWith("*")) continue;

                    if (lines[i].Contains("HistoryFolder.Fold("))
                        hits.Add($"{rel}:{i + 1}");
                }
            }

            Assert.Greater(hits.Count, 0,
                "HistoryFolder.Fold 须有生产调用点(TD-persist-01 ① 的位置半);实测 0 行");
            foreach (string hit in hits)
                Assert.IsFalse(hit.Contains("/Sim.Codec/"),
                    $"调用点 {hit} 落在 Sim.Codec/ 内 —— 物理上不可能" +
                    "(Sim 与 Sim.Codec 的 asmdef references 互不相含,ADR-025 §①)");
        }

        // ══════════════════════════════════════════════════════════════════
        // 组版:无折叠行 ⇒ 正常产字节
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_compose_noFoldedPatient_producesValidSave()
        {
            // 病人 A 只有一个未终态的病程 ⇒ 谓词为假 ⇒ 零折叠行 ⇒ 正常组版。
            var events = new[] { Onset(10L, PatientA, DiseaseSelfLimit) };

            byte[] bytes = SaveComposer.Compose(
                events, Predicate(events), _decoder, new SaveCodec(),
                worldSeed: 0xABCDUL, configVersion: 3U, tick: 10L);

            Assert.IsNotNull(bytes);
            Assert.IsTrue(SaveService.ValidateChecksum(bytes), "产出的字节须自校验通过");

            SaveHeader h = SaveHeaderCodec.Read(bytes);
            Assert.AreEqual(0xABCDUL, h.WorldSeed);
            Assert.AreEqual(3U, h.ConfigVersion);
            Assert.AreEqual(10L, h.Tick);

            IReadOnlyList<SimEvent> back = SimEventListCodec.Decode(
                bytes.AsSpan(SaveHeaderCodec.HeaderSize).ToArray(), new SaveCodec());
            Assert.AreEqual(1, back.Count, "未折叠 ⇒ 事件原样保留");
            Assert.AreEqual(EventKind.DiseaseOnset, back[0].Kind);
        }

        [Test]
        public void test_compose_exemptPatient_isNotFolded()
        {
            // 病人 B 已终态,但病种是 plateau ∧ non-lethal ⇒ 豁免 ⇒ 不折 ⇒ 正常产字节。
            var events = new[]
            {
                Onset(10L, PatientB, DiseasePlateau),
                Outcome(20L, PatientB, DiseasePlateau, PatientOutcome.Recovered),
            };

            Assert.IsTrue(Predicate(events).Folded(PatientB) == false, "豁免病人谓词须为假");

            Assert.DoesNotThrow(() => SaveComposer.Compose(
                events, Predicate(events), _decoder, new SaveCodec(),
                worldSeed: 1UL, configVersion: 0U, tick: 20L),
                "豁免 ⇒ 无折叠行 ⇒ 组版不得抛");
        }

        [Test]
        public void test_compose_openCaseBlocksFold_producesValidSave()
        {
            // 终态已到,但病例未结 ⇒ 条件 ③ 拦下 ⇒ 不折 ⇒ 正常组版。
            var events = new[]
            {
                Onset(10L, PatientA, DiseaseSelfLimit),
                Outcome(20L, PatientA, DiseaseSelfLimit, PatientOutcome.Recovered),
                CaseEvent(10L, PatientA, EventKind.CaseOpened),
            };

            Assert.IsFalse(Predicate(events).Folded(PatientA), "未结案 ⇒ 谓词为假");

            Assert.DoesNotThrow(() => SaveComposer.Compose(
                events, Predicate(events), _decoder, new SaveCodec(),
                worldSeed: 1UL, configVersion: 0U, tick: 20L));
        }

        // ══════════════════════════════════════════════════════════════════
        // ⚠️ 折叠行要落盘 ⇒ fail-loud(这是**字节半 NOT-RUN 的证据**,不是替代品)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_compose_foldablePatient_throwsInsteadOfWritingSaveWithoutFoldedRows()
        {
            // 病人 A:单病种、已终态、非豁免、无未结案 ⇒ 谓词为真 ⇒ 有折叠行要落盘。
            // 折叠行编码器不存在 ⇒ 必须抛,不得写出一份「抹掉了病史行却没有折叠行」的存档。
            var events = new[]
            {
                Onset(10L, PatientA, DiseaseSelfLimit),
                Outcome(20L, PatientA, DiseaseSelfLimit, PatientOutcome.Died),
                CaseEvent(10L, PatientA, EventKind.CaseOpened),
                CaseEvent(30L, PatientA, EventKind.CaseClosed),
            };

            Assert.IsTrue(Predicate(events).Folded(PatientA), "前提:该病人须可折");

            var ex = Assert.Throws<NotSupportedException>(() => SaveComposer.Compose(
                events, Predicate(events), _decoder, new SaveCodec(),
                worldSeed: 1UL, configVersion: 0U, tick: 30L));

            StringAssert.Contains("折叠行", ex.Message);
            StringAssert.Contains("TD-persist-03", ex.Message,
                "报文须点名归属件 —— 否则读者会以为这是 bug 而不是已登记缺口");
        }

        [Test]
        public void test_compose_failureMessage_namesTheNotRunCriterion()
        {
            var events = new[]
            {
                Onset(10L, PatientA, DiseaseSelfLimit),
                Outcome(20L, PatientA, DiseaseSelfLimit, PatientOutcome.Died),
            };

            var ex = Assert.Throws<NotSupportedException>(() => SaveComposer.Compose(
                events, Predicate(events), _decoder, new SaveCodec(), 1UL, 0U, 20L));

            StringAssert.Contains("NOT-RUN", ex.Message,
                "报文须自陈判据 ④ 字节半 = NOT-RUN(禁借绿的同族纪律)");
        }

        // ══════════════════════════════════════════════════════════════════
        // 组版的纯函数性 / 确定性
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_compose_isDeterministic_sameInputSameBytes()
        {
            var events = new[] { Onset(10L, PatientA, DiseaseSelfLimit) };

            byte[] a = SaveComposer.Compose(events, Predicate(events), _decoder, new SaveCodec(),
                                            0x1234UL, 5U, 10L);
            byte[] b = SaveComposer.Compose(events, Predicate(events), _decoder, new SaveCodec(),
                                            0x1234UL, 5U, 10L);

            CollectionAssert.AreEqual(a, b, "同样输入 ⇒ 逐字节相同(ADR-012 黄金夹具前提)");
        }

        [Test]
        public void test_compose_routesByStream_notByCallerAssumption()
        {
            // 调用方把三流混在一坨里 ⇒ 组版器须自行按 StreamRouting 分流,
            // 病例流事件**不进**事件列表段(今日布局 = 头部 + 病史流段)。
            var events = new[]
            {
                Onset(10L, PatientA, DiseaseSelfLimit),
                CaseEvent(10L, PatientA, EventKind.CaseOpened),
                new SimEvent(11L, PatientId.None, 0L, EventKind.StructurePlaced, default),
            };

            byte[] bytes = SaveComposer.Compose(events, Predicate(events), _decoder, new SaveCodec(),
                                                1UL, 0U, 11L);

            IReadOnlyList<SimEvent> back = SimEventListCodec.Decode(
                bytes.AsSpan(SaveHeaderCodec.HeaderSize).ToArray(), new SaveCodec());

            Assert.AreEqual(1, back.Count, "只有病史流事件进段(病例流 / 世界流今日不进)");
            Assert.AreEqual(EventKind.DiseaseOnset, back[0].Kind);
        }

        [Test]
        public void test_filterByStream_matchesStreamRouting()
        {
            // FilterByStream 的口径必须就是 StreamRouting.Of —— 不得自抄白名单。
            var events = new[]
            {
                Onset(1L, PatientA, DiseaseSelfLimit),
                CaseEvent(2L, PatientA, EventKind.CaseOpened),
                new SimEvent(3L, PatientId.None, 0L, EventKind.Craft, default),
            };

            Assert.AreEqual(1, SaveComposer.FilterByStream(events, StreamId.History).Count);
            Assert.AreEqual(1, SaveComposer.FilterByStream(events, StreamId.Case).Count);
            Assert.AreEqual(1, SaveComposer.FilterByStream(events, StreamId.World).Count);

            var all = SaveComposer.FilterByStream(events, StreamId.History)
                .Concat(SaveComposer.FilterByStream(events, StreamId.Case))
                .Concat(SaveComposer.FilterByStream(events, StreamId.World));
            Assert.AreEqual(3, all.Count(), "三流并集须恰等于全集(无漏无重)");
        }

        // ══════════════════════════════════════════════════════════════════
        // 判据 ④ 位置半 (ii) —— 装配根接了 compose,且 SaveService 可被取出
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_assemble_withoutSaveBasePath_leavesSaveServiceNull()
        {
            // 109 处既有 Assemble 调用方(多为 EditMode 夹具)不传目录 ⇒ 不构造 SaveService。
            var bag = CompositionRoot.Assemble(new[] { SyntheticDiseaseFixture.Create() });

            Assert.IsNull(bag.SaveService,
                "未指定存档目录 ⇒ SaveService 为 null(合法状态,不是半装配)");
        }

        [Test]
        public void test_assemble_withSaveBasePath_constructsSaveServiceWithFoldedPredicate()
        {
            string dir = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), "dyjq_s19_1_compose_" + Guid.NewGuid().ToString("N"));

            try
            {
                var bag = CompositionRoot.Assemble(
                    new[] { SyntheticDiseaseFixture.Create() }, saveBasePath: dir);

                Assert.IsNotNull(bag.SaveService, "指定目录 ⇒ 须构造 SaveService");
                Assert.IsNotNull(bag.SaveService.FoldedPredicate,
                    "折叠谓词须已注入(位置半 (ii) 的另一半:装配根真的接了)");
                Assert.IsTrue(bag.SaveService.Ledger.HasNarrativeContextSource == false,
                    "叙事上下文今日刻意未接(fail-closed)—— 不得因「测试要绿」而填一个恒真委托");
            }
            finally
            {
                if (System.IO.Directory.Exists(dir))
                {
                    try { System.IO.Directory.Delete(dir, true); } catch { }
                }
            }
        }

        [Test]
        public void test_assemble_saveService_checkpointFailsClosed_withoutNarrativeContext()
        {
            // 端到端证:装配出的 SaveService 在生产路径上**拒绝**存档。
            // 这不是缺陷,是刻意(fail-closed)—— 本测试防的是「有人为了让存档能用而
            // 塞一个恒真委托进来」,那会让 save-scum 通道默开而全绿。
            string dir = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), "dyjq_s19_1_failclosed_" + Guid.NewGuid().ToString("N"));

            try
            {
                var bag = CompositionRoot.Assemble(
                    new[] { SyntheticDiseaseFixture.Create() }, saveBasePath: dir);

                var ex = Assert.Throws<InvalidOperationException>(
                    () => bag.SaveService.Checkpoint(new SaveSlot(0)),
                    "未注入叙事上下文 ⇒ Checkpoint 须抛(fail-closed)");
                StringAssert.Contains("narrativeContext", ex.Message);

                Assert.Throws<InvalidOperationException>(
                    () => bag.SaveService.SaveOnExit(),
                    "SaveOnExit 同门,亦须 fail-closed");
            }
            finally
            {
                if (System.IO.Directory.Exists(dir))
                {
                    try { System.IO.Directory.Delete(dir, true); } catch { }
                }
            }
        }

        [Test]
        public void test_assemble_isNotCalledWithPersistentDataPathByAnyTest()
        {
            // 反向守卫:全树测试不得把 persistentDataPath 当 saveBasePath 传 ——
            // 那会让测试污染用户数据目录,而 FsWriteBoundaryGate 只扫字面量、抓不到派生路径。
            string repoRoot = RepoRoot();
            string testsDir = System.IO.Path.Combine(repoRoot, "unity", "Assets", "Tests");

            // ⚠️ 自指排除:本文件自己就同时含这两个串(本测试的判据文本 + 上一条测试的注释)
            //    ⇒ 不排除则本测试恒红,且红的是「它自己」而非被测对象。
            //    两重判据(全路径 / 文件名):CallerFilePath 与 Application.dataPath 派生的路径
            //    可能只在大小写或符号链接上不同,只比全路径会让本测试在换机器后翻红。
            string selfPath = System.IO.Path.GetFullPath(ThisFile());
            string selfName = System.IO.Path.GetFileName(selfPath);

            var offenders = new List<string>();
            foreach (string file in System.IO.Directory.GetFiles(testsDir, "*.cs",
                         System.IO.SearchOption.AllDirectories))
            {
                if (System.IO.Path.GetFullPath(file) == selfPath) continue;
                if (System.IO.Path.GetFileName(file) == selfName) continue;

                string text = System.IO.File.ReadAllText(file);
                if (text.Contains("saveBasePath:") && text.Contains("persistentDataPath"))
                    offenders.Add(file.Replace('\\', '/'));
            }

            CollectionAssert.IsEmpty(offenders,
                "测试不得用 persistentDataPath 作存档目录(会污染用户数据目录)");
        }

        /// <summary>本测试文件自身的绝对路径(自指排除用)。</summary>
        private static string ThisFile(
            [System.Runtime.CompilerServices.CallerFilePath] string path = null) => path;

        /// <summary>仓库根(`Assets/` 的父目录的父目录)。</summary>
        private static string RepoRoot()
        {
            // UnityEngine.Application.dataPath 在 EditMode 下 = <repo>/unity/Assets
            string dataPath = UnityEngine.Application.dataPath.Replace('\\', '/');
            return System.IO.Path.GetDirectoryName(System.IO.Path.GetDirectoryName(dataPath));
        }
    }
}
