// S19-1 判据 ④ 位置半 + 委托半的接线测试 —— `SaveComposer` + `CompositionRoot` 装配。
//
// 权威来源:
//   production/sprints/sprint-19.md §S19-1 判据 ④ 位置半(i)(ii)
//   production/qa/evidence/s19-1-fold-adjudication-2026-10-24.md §二(甲-a)· §三(字节半六条实测)
//   ADR-010 §六(Checkpoint 唯一写入口)· ADR-025 §①(Sim / Sim.Codec 互不可见)
//
// ─────────────────────────────────────────────────────────────────────────────
// 🔁 2026-10-24 `TD-persist-03` 修复轮:字节半**已在本文件兑现**
// ─────────────────────────────────────────────────────────────────────────────
// S19-1 版本文件头曾写「本文件不测字节半」—— 当时折叠行编码器不存在,
// `test_compose_foldablePatient_*` 那条以「须抛」证明其缺失。
// 本轮编码器落地(`FoldRowCodec`)+ 布局定稿(v1 = 头部 + 事件段 + 折叠段),
// 该条**就地改写**为「行确实进字节」,并新增:既有行回写 / 空段 / 反向判据
// (谓词假 ⇒ 与不折叠逐位相同)/ 三流全进段。
// 旧断言改写缘由逐条见各测试内 🔁 注;原件 = 证据文件 §测试改写。
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

        /// <summary>v1 布局读回帮助:事件段 + 折叠段(与 SaveService.ParseBody 同口径)。</summary>
        private static void ParseBody(
            byte[] bytes, out IReadOnlyList<SimEvent> events, out IReadOnlyList<FoldRow> foldRows)
        {
            var r = new CodecReader(bytes.AsSpan(SaveHeaderCodec.HeaderSize));
            // 复合体:事件段用无读尽断言的读(其后还有折叠段),末段由折叠段读尽闭合。
            events = SimEventListCodec.ReadWithinBody(ref r, new SaveCodec());
            foldRows = FoldRowCodec.DecodeSegment(r.ReadBytes(r.Remaining));
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

            IReadOnlyList<SimEvent> back;
            IReadOnlyList<FoldRow> foldRows;
            ParseBody(bytes, out back, out foldRows);
            Assert.AreEqual(1, back.Count, "未折叠 ⇒ 事件原样保留");
            Assert.AreEqual(EventKind.DiseaseOnset, back[0].Kind);
            Assert.AreEqual(0, foldRows.Count, "未折叠 ⇒ 折叠段零行");
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
        // 判据 ④ 字节半(TD-persist-03 动作 ⑤ · 2026-10-24 修复轮兑现)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_compose_foldablePatient_writesFoldRowsIntoFoldedSegment()
        {
            // 病人 A:单病种、已终态、非豁免、无未结案 ⇒ 谓词为真 ⇒ 有折叠行落折叠段。
            // 🔁 本条改写自 S19-1 版的 `..._throwsInsteadOfWritingSaveWithoutFoldedRows`:
            //    当时编码器不存在 ⇒ fail-loud 是字节半 NOT-RUN 的**缺失证据**;
            //    本轮(TD-persist-03)编码器落地,断言**翻转**为「行确实进字节」——
            //    旧断言(须抛)在新实现下必红,按「同批改而非删」纪律就地改写。
            var events = new[]
            {
                Onset(10L, PatientA, DiseaseSelfLimit),
                Outcome(20L, PatientA, DiseaseSelfLimit, PatientOutcome.Died),
                CaseEvent(10L, PatientA, EventKind.CaseOpened),
                CaseEvent(30L, PatientA, EventKind.CaseClosed),
            };

            Assert.IsTrue(Predicate(events).Folded(PatientA), "前提:该病人须可折");

            // Act
            byte[] bytes = SaveComposer.Compose(
                events, Predicate(events), _decoder, new SaveCodec(),
                worldSeed: 1UL, configVersion: 0U, tick: 30L);

            // Assert —— 事件段:被折病人的**病史流**行已抹;折叠段:行在场。
            // ⚠️ 断言面收窄到病史流:夹具里的 CaseEvent 也带 PatientA,但那是病例流
            //    行 —— 折叠只抹病史流,病例行不是被折对象(ADR-008 §六 折叠不物理化)。
            var r = new CodecReader(bytes.AsSpan(SaveHeaderCodec.HeaderSize));
            IReadOnlyList<SimEvent> back = SimEventListCodec.ReadWithinBody(ref r, new SaveCodec());
            Assert.IsFalse(back.Any(e => e.Patient.Value == PatientA
                                      && StreamRouting.Of(e.Kind) == StreamId.History),
                "被折病人的病史行须已从事件段抹除(折叠丢弃流位置;病例流行不在折叠面)");
            Assert.IsFalse(back.Any(e => e.Kind == EventKind.DiseaseOnset),
                "被折病人的 onset 行不得残留在事件段");

            IReadOnlyList<FoldRow> rows = FoldRowCodec.DecodeSegment(r.ReadBytes(r.Remaining));
            Assert.AreEqual(1, rows.Count, "折叠段须恰有该病人一行");
            Assert.AreEqual(PatientA, rows[0].PatientId, "行须保留 patient_id(ADR-006 Amendment B)");
            Assert.AreEqual(DiseaseSelfLimit, rows[0].DiseaseId);
            Assert.AreEqual(Seed, rows[0].PatientSeed, "patient_seed = hash(world_seed, patient_id),取自 onset 载荷");
            Assert.AreEqual(10L, rows[0].Onset, "onset 取自 onset 事件载荷");
            Assert.AreEqual(20L, rows[0].TEnd, "t_end = 终态事件头部 Tick(不是载荷字段)");
            Assert.AreEqual((int)PatientOutcome.Died, rows[0].Outcome);
        }

        [Test]
        public void test_compose_existingFoldedRows_areCarriedIntoSegment()
        {
            // 回写义务(TD-persist-03 修复轮):读档恢复后,被折病人**不在流里**,
            // 折叠循环看不见他们 ⇒ 不回写 = 二次 Checkpoint 静默丢折叠行。
            // 本条 = 该义务的回归钉:既有行 + 本轮新折行都须在折叠段,既有行在前。
            var events = new[]
            {
                Onset(10L, PatientA, DiseaseSelfLimit),
                Outcome(20L, PatientA, DiseaseSelfLimit, PatientOutcome.Died),
                Onset(30L, PatientB, DiseaseSelfLimit),
                Outcome(40L, PatientB, DiseaseSelfLimit, PatientOutcome.Recovered),
            };

            var carried = new[] { new FoldRow(1L, 99, 3, 77L, (int)PatientOutcome.Died, 2L) };

            byte[] bytes = SaveComposer.Compose(
                events, Predicate(events), _decoder, new SaveCodec(),
                worldSeed: 1UL, configVersion: 0U, tick: 40L, existingFoldedRows: carried);

            var r = new CodecReader(bytes.AsSpan(SaveHeaderCodec.HeaderSize));
            SimEventListCodec.ReadWithinBody(ref r, new SaveCodec()); // 跳过事件段
            IReadOnlyList<FoldRow> rows = FoldRowCodec.DecodeSegment(r.ReadBytes(r.Remaining));

            Assert.AreEqual(3, rows.Count, "既有 1 行 + 新折 2 行 = 3 行");
            Assert.AreEqual(3, rows[0].PatientId, "既有行在前(行序确定性)");
            Assert.AreEqual(PatientA, rows[1].PatientId, "新折行按病人 id 升序跟在既有行后");
            Assert.AreEqual(PatientB, rows[2].PatientId);
        }

        [Test]
        public void test_compose_noFoldAtAll_foldedSegmentIsEmptyCountZero()
        {
            // 无折叠 ⇒ 折叠段仍**在场**(count=0,4 字节)—— v1 布局必备段,
            // 读侧(SaveService.ParseBody)据此判定「事件段后必见折叠段」的对称性。
            var events = new[] { Onset(10L, PatientA, DiseaseSelfLimit) };

            byte[] bytes = SaveComposer.Compose(
                events, Predicate(events), _decoder, new SaveCodec(),
                worldSeed: 1UL, configVersion: 0U, tick: 10L);

            var r = new CodecReader(bytes.AsSpan(SaveHeaderCodec.HeaderSize));
            SimEventListCodec.ReadWithinBody(ref r, new SaveCodec());
            Assert.GreaterOrEqual(r.Remaining, 4, "折叠段 count 前缀须在场(v1 必备段)");
            IReadOnlyList<FoldRow> rows = FoldRowCodec.DecodeSegment(r.ReadBytes(r.Remaining));
            Assert.AreEqual(0, rows.Count, "无折叠 ⇒ 零行,段 = count 前缀 4 字节");
        }

        [Test]
        public void test_compose_reverseCriterion_notFolded_bytesIdenticalWithoutPredicate()
        {
            // 反向判据(TD-persist-03 卡住判据的另一半):谓词恒假(无可折病人)⇒
            // 字节与「不跑折叠」逐位相同 —— 折叠路径的存在不得改变无折叠存档的形状。
            var events = new[]
            {
                Onset(10L, PatientA, DiseaseSelfLimit),
                CaseEvent(10L, PatientA, EventKind.CaseOpened), // 未结案 ⇒ 谓词假
            };

            byte[] withPredicate = SaveComposer.Compose(
                events, Predicate(events), _decoder, new SaveCodec(), 1UL, 0U, 10L);
            byte[] neverFold = SaveComposer.Compose(
                events, NeverFold, _decoder, new SaveCodec(), 1UL, 0U, 10L);

            CollectionAssert.AreEqual(withPredicate, neverFold,
                "谓词假 ⇒ 折叠路径零参与 ⇒ 字节逐位相同(折叠不得改变无折档)");
        }

        /// <summary>恒假谓词 —— 反向判据用:任何病人都不折。</summary>
        private sealed class NeverFoldedPredicate : IFoldedPredicate
        {
            public bool Folded(int patientId) => false;
        }

        private IFoldedPredicate NeverFold => new NeverFoldedPredicate();

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
        public void test_compose_threeStreams_allLandInEventSegment()
        {
            // 调用方把三流混在一坨里 ⇒ 组版器须自行按 StreamRouting 识别,且
            // **三流全进事件段**(v1 单列表布局)。
            //
            // 🔁 本条改写自 S19-1 版的 `..._routesByStream_notByCallerAssumption`:
            //    旧版断言「病例流 / 世界流不进段」= 当时静默丢弃缺陷的现状钉
            //    (其注释自陈「补布局的那一轮必须同时改本条」)。本轮
            //    (TD-persist-03 动作 ③,用户裁定「修 ADR-010 承认单列表」)布局落地,
            //    断言**翻转**为三流全在段内、且保持原全局序。
            var events = new[]
            {
                Onset(10L, PatientA, DiseaseSelfLimit),                      // 病史流
                CaseEvent(10L, PatientA, EventKind.CaseOpened),              // 病例流
                new SimEvent(11L, PatientId.None, 0L, EventKind.StructurePlaced, default), // 世界流
            };

            byte[] bytes = SaveComposer.Compose(events, Predicate(events), _decoder, new SaveCodec(),
                                                1UL, 0U, 11L);

            IReadOnlyList<SimEvent> back;
            IReadOnlyList<FoldRow> foldRows;
            ParseBody(bytes, out back, out foldRows);

            Assert.AreEqual(3, back.Count,
                "v1 单列表:三流全进事件段(静默丢弃已随 TD-persist-03 动作 ③ 消灭)");
            Assert.AreEqual(EventKind.DiseaseOnset, back[0].Kind, "原全局序保持");
            Assert.AreEqual(EventKind.CaseOpened, back[1].Kind);
            Assert.AreEqual(EventKind.StructurePlaced, back[2].Kind);
            Assert.AreEqual(0, foldRows.Count);
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
