// S18-2 / G-2 —— 折叠谓词 Folded(p) 四条件 + 折叠行产出 的验收面。
//
// 权威来源:
//   GDD persistence-service.md:314-330 —— F-7a-4 单一出处:Folded(p) = 终态 ∧ ¬豁免 ∧ ¬未结案
//   GDD persistence-service.md:321     —— ¬豁免 = 豁免类(lethal = false ∧ plateau)**或** relapse.* 块
//   ADR-008 §六                        —— 条件 ③ 无未结案病例
//   ADR-026 §⑦                         —— 条件 ④ SkillGrown 行豁免(案 1)
//   ADR-010 §二                        —— 折叠行 = (onset, 病种_id, patient_id, patient_seed, outcome, t_end)
//   ADR-032 ⑥                          —— 条件 ① = 存在性查询(不重放 F4)
//   ADR-025 §①:112                     —— 甲案:IFoldedPredicate 住 Contracts · 实现住 Sim
//   ADR-010 :331                       —— ISaveService 公开方法参数集不变(构造函数注入)
//
// ⚠️ 本文件的验收范围(如实登记,禁借绿):
//   覆盖 ① 四条件逐条(各一否例 + 正例)② 折叠行六字段 ③ SkillGrown 行存续
//        ④ 边界(空流 / 空病例流 / 负 id)⑤ fail-loud(未知病种 / 重复终态)
//       ⑥ SaveService 的构造函数注入形态
//   **不在本文件**(归各自实现轮,当前 NOT-RUN):
//     · 9 的终态写者(WriterExistenceGate 已具名挂债)
//     · 折叠进存档路径(SaveCodec 尚无三流序列化)⇒ FoldedPredicate 当前**零生产调用点**
//     · 字节级折叠行落盘(7a 存档往返轮)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.PersistenceService
{
    /// <summary>Folded(p) 四条件 + 折叠行的夹具。</summary>
    public class FoldedPredicateTest
    {
        private InMemoryBlobPool _pool;
        private PayloadEncoder _encoder;
        private PayloadDecoder _decoder;
        private List<SimEvent> _history;
        private List<SimEvent> _cases;
        private Dictionary<int, DiseaseRegistryEntry> _registry;

        private const int P = 7;      // 被测病人
        private const int OTHER = 8;  // 旁证病人
        private const int DISEASE = 3;
        private const long SEED = 0x5EEDL;

        [SetUp]
        public void SetUp()
        {
            _pool = new InMemoryBlobPool();
            _encoder = new PayloadEncoder(_pool);
            _decoder = new PayloadDecoder(_pool);
            _history = new List<SimEvent>();
            _cases = new List<SimEvent>();
            _registry = new Dictionary<int, DiseaseRegistryEntry>();
        }

        // ══════════════════ 夹具构造 ══════════════════

        private SimEvent Onset(long tick, int patient, int disease, long seed = SEED, long seq = 0)
            => new SimEvent(tick, new PatientId(patient), seq, EventKind.DiseaseOnset,
                _encoder.Encode(EventKind.DiseaseOnset,
                    new DiseaseOnsetPayload(tick, disease, patient, seed, seq)));

        private SimEvent Outcome(long tick, int patient, int disease, PatientOutcome outcome, long seq = 0)
            => new SimEvent(tick, new PatientId(patient), seq, EventKind.PatientOutcomeChanged,
                _encoder.Encode(EventKind.PatientOutcomeChanged,
                    new PatientOutcomeChangedPayload(patient, disease, (int)outcome, seq)));

        private SimEvent SkillGrown(long tick, int patient, int skill, int level, long seq = 0)
            => new SimEvent(tick, new PatientId(patient), seq, EventKind.SkillGrown,
                _encoder.Encode(EventKind.SkillGrown,
                    new SkillGrownPayload(1, patient, skill, DISEASE, 0, level)));

        /// <summary>病例流事件 —— HasOpenCase 只读 Kind 与 Patient,故载荷留 default。</summary>
        private SimEvent CaseEvent(long tick, int patient, EventKind kind, long seq = 0)
            => new SimEvent(tick, new PatientId(patient), seq, kind, default);

        /// <summary>
        /// 注册表条目。<paramref name="lethality"/> = 0 即 <c>lethal = false</c>
        /// (承 <c>R1_CHECK_22</c> / <c>EntityDeathGates.LethalityOf</c> 的既有取法)。
        /// </summary>
        private void AddDisease(int diseaseId, int lethality,
                                bool plateau = false, bool selfLimit = false, bool relapse = false)
        {
            _registry[diseaseId] = new DiseaseRegistryEntry
            {
                DiseaseId = diseaseId,
                DiseaseKey = "D" + diseaseId,
                Kind = DiseaseKind.Disease,
                Lethality = lethality,
                Curve = new NaturalProgressCurve { Plateau = plateau, SelfLimit = selfLimit },
                Relapse = relapse ? new RelapseCurve() : null,
            };
        }

        private FoldedPredicate Predicate()
            => new FoldedPredicate(() => _history, () => _cases,
                id => _registry.TryGetValue(id, out var entry) ? entry : null,
                _decoder);

        /// <summary>四条件全满足的标准场景:终态 + 非豁免 + 无未结案。</summary>
        private void ArrangeFoldable()
        {
            AddDisease(DISEASE, lethality: 0, plateau: false);   // 非豁免(急性自愈型)
            _history.Add(Onset(tick: 10, patient: P, disease: DISEASE, seed: SEED));
            _cases.Add(CaseEvent(11, P, EventKind.CaseOpened));
            _cases.Add(CaseEvent(20, P, EventKind.CaseClosed));
            _history.Add(Outcome(tick: 50, patient: P, disease: DISEASE, outcome: PatientOutcome.Recovered));
        }

        // ══════════════════ ① 终态 ══════════════════

        [Test]
        public void test_folded_allFourConditionsHold_returnsTrue()
        {
            ArrangeFoldable();
            Assert.IsTrue(Predicate().Folded(P), "终态 + 非豁免 + 无未结案 ⇒ 可折");
        }

        [Test]
        public void test_folded_noTerminalEvent_returnsFalse()
        {
            // 条件 ①唯一否例:D_p 在、未结案无、非豁免 —— 但没有终态事件。
            AddDisease(DISEASE, lethality: 0);
            _history.Add(Onset(10, P, DISEASE));

            Assert.IsFalse(Predicate().Folded(P), "无 PatientOutcomeChanged ⇒ 终态不成立 ⇒ 不可折");
        }

        [Test]
        public void test_folded_recoveredCountsAsTerminal()
        {
            ArrangeFoldable();
            Assert.IsTrue(Predicate().Terminal(P), "痊愈(Recovered)是终态(AC-36 两态并列)");
        }

        [Test]
        public void test_folded_diedCountsAsTerminal()
        {
            AddDisease(DISEASE, lethality: 1);
            _history.Add(Onset(10, P, DISEASE));
            _history.Add(Outcome(50, P, DISEASE, PatientOutcome.Died));

            Assert.IsTrue(Predicate().Terminal(P), "死亡(Died)是终态");
        }

        [Test]
        public void test_folded_terminalEventOfOtherPatient_doesNotCount()
        {
            // 存在性查询按**病人**过滤 —— 别人的终态不是我的。
            AddDisease(DISEASE, lethality: 0);
            _history.Add(Onset(10, OTHER, DISEASE));
            _history.Add(Outcome(50, OTHER, DISEASE, PatientOutcome.Recovered));

            Assert.IsFalse(Predicate().Folded(P), "其他病人的终态事件不得让本病人可折");
        }

        // ══════════════════ ② 豁免(口径对齐点)══════════════════

        [Test]
        public void test_folded_plateauNonLethal_isExempt_returnsFalse()
        {
            // 豁免类 = lethal = false ∧ plateau。
            AddDisease(DISEASE, lethality: 0, plateau: true);
            _history.Add(Onset(10, P, DISEASE));
            _history.Add(Outcome(50, P, DISEASE, PatientOutcome.Recovered));

            Assert.IsTrue(Predicate().Exempt(P), "plateau ∧ ¬lethal ⇒ 豁免类");
            Assert.IsFalse(Predicate().Folded(P), "豁免类 ⇒ 不可折");
        }

        [Test]
        public void test_folded_plateauAndLethal_isNotExempt_returnsTrue()
        {
            // ⚠️ 本件最易实现错的一条:把 plateau 一律当豁免。
            // GDD :321 明写豁免类**必须带 lethal = false** —— plateau ∧ lethal 可达终态、可折叠。
            AddDisease(DISEASE, lethality: 2, plateau: true);
            _history.Add(Onset(10, P, DISEASE));
            _history.Add(Outcome(50, P, DISEASE, PatientOutcome.Died));

            Assert.IsFalse(Predicate().Exempt(P), "plateau ∧ lethal **不是**豁免类(口径对齐点)");
            Assert.IsTrue(Predicate().Folded(P), "plateau ∧ lethal ⇒ 可折");
        }

        [Test]
        public void test_folded_relapseBlock_isExempt_returnsFalse()
        {
            // relapse.* 与 plateau 正交:回放需完整病程。
            AddDisease(DISEASE, lethality: 0, plateau: false, relapse: true);
            _history.Add(Onset(10, P, DISEASE));
            _history.Add(Outcome(50, P, DISEASE, PatientOutcome.Recovered));

            Assert.IsTrue(Predicate().Exempt(P), "携带 relapse.* 块 ⇒ 豁免");
            Assert.IsFalse(Predicate().Folded(P), "relapse.* ⇒ 不可折");
        }

        [Test]
        public void test_folded_exemptOnAnyOfMultipleDiseases_returnsFalse()
        {
            // 豁免按病人整体:任一病种豁免 ⇒ 整体不折(否则该病种病程回放缺环)。
            AddDisease(1, lethality: 0, plateau: false);
            AddDisease(2, lethality: 0, plateau: true);
            _history.Add(Onset(10, P, 1));
            _history.Add(Onset(12, P, 2));
            _history.Add(Outcome(50, P, 1, PatientOutcome.Recovered));

            Assert.IsFalse(Predicate().Folded(P), "病人的任一病种属于豁免类 ⇒ 整体不折");
        }

        // ══════════════════ ③ 无未结案 ══════════════════

        [Test]
        public void test_folded_openCase_returnsFalse()
        {
            AddDisease(DISEASE, lethality: 0);
            _history.Add(Onset(10, P, DISEASE));
            _history.Add(Outcome(50, P, DISEASE, PatientOutcome.Recovered));
            _cases.Add(CaseEvent(11, P, EventKind.CaseOpened));   // 开了没闭

            Assert.IsTrue(Predicate().HasOpenCase(P), "已开未闭 ⇒ 未结案");
            Assert.IsFalse(Predicate().Folded(P), "有未结案病例 ⇒ 不可折(ADR-008 §六)");
        }

        [Test]
        public void test_folded_closedCase_countsAsNoOpenCase()
        {
            // ③ 必须复用 CaseStreamQuery.HasOpenCase 的语义(开 + 闭 = 已结)。
            ArrangeFoldable();
            Assert.IsFalse(Predicate().HasOpenCase(P), "CaseOpened 后跟 CaseClosed ⇒ 无未结案");
            Assert.IsTrue(Predicate().Folded(P));
        }

        [Test]
        public void test_folded_emptyCaseStream_treatedAsNoOpenCase()
        {
            // 从未就诊 ⇒ 病例流为空 ⇒ 条件 ③ 满足。
            AddDisease(DISEASE, lethality: 0);
            _history.Add(Onset(10, P, DISEASE));
            _history.Add(Outcome(50, P, DISEASE, PatientOutcome.Recovered));

            Assert.AreEqual(0, _cases.Count);
            Assert.IsTrue(Predicate().Folded(P), "病例流为空 ⇒ 未结案 = false ⇒ 可折");
        }

        // ══════════════════ 边界 / fail-loud ══════════════════

        [Test]
        public void test_folded_emptyHistory_returnsFalseWithoutThrow()
        {
            Assert.IsFalse(Predicate().Folded(P), "空病史流 ⇒ 任意病人不可折(且不崩)");
        }

        [Test]
        public void test_folded_negativePatientId_throws()
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Predicate().Folded(-1));
            StringAssert.Contains("PatientId.None", ex.Message,
                "报文须点名 PatientId.None,便于排障");
        }

        [Test]
        public void test_folded_unknownDiseaseId_failsLoud()
        {
            // 未知病种 ⇒ 无法判豁免。当作「非豁免」会折掉没能力评估的病人,且不可逆。
            _history.Add(Onset(10, P, disease: 99));   // 注册表里没有 99
            _history.Add(Outcome(50, P, disease: 99, outcome: PatientOutcome.Recovered));

            var ex = Assert.Throws<InvalidOperationException>(() => Predicate().Folded(P));
            StringAssert.Contains("99", ex.Message, "报文须点名缺失的病种 id");
        }

        [Test]
        public void test_folded_curveMissing_treatedAsNotPlateau()
        {
            // Curve == null 的 legacy 占位条目:plateau 无定义 ⇒ 视为非豁免
            // (与 EntityDeathGates 对 Curve == null 的口径一致)。
            _registry[DISEASE] = new DiseaseRegistryEntry
            {
                DiseaseId = DISEASE, DiseaseKey = "LEGACY", Kind = DiseaseKind.Disease,
                Lethality = 0, Curve = null, Relapse = null,
            };
            _history.Add(Onset(10, P, DISEASE));
            _history.Add(Outcome(50, P, DISEASE, PatientOutcome.Recovered));

            Assert.IsFalse(Predicate().Exempt(P), "Curve == null ⇒ plateau 无定义 ⇒ 非豁免");
        }

        // ══════════════════ 折叠行 —— 六字段 ══════════════════

        [Test]
        public void test_fold_allSixFieldsMatchTheirSources()
        {
            ArrangeFoldable();
            FoldResult result = HistoryFolder.Fold(_history, P, Predicate(), _decoder);

            Assert.AreEqual(1, result.Rows.Count);
            FoldRow row = result.Rows[0];

            Assert.AreEqual(10L, row.Onset, "onset ← DiseaseOnsetPayload.OnsetTick");
            Assert.AreEqual(DISEASE, row.DiseaseId, "病种_id ← 终态事件的 disease_id");
            Assert.AreEqual(P, row.PatientId, "patient_id ← 被折叠的病人");
            Assert.AreEqual(SEED, row.PatientSeed,
                "patient_seed ← DiseaseOnsetPayload.PatientSeed(不重算)");
            Assert.AreEqual((int)PatientOutcome.Recovered, row.Outcome, "outcome ← 终态事件载荷");
            Assert.AreEqual(50L, row.TEnd, "t_end ← 终态事件**头部 Tick**(ADR-032 ④a)");
        }

        [Test]
        public void test_fold_rowShapeIsExactlySixFields_inAdrOrder()
        {
            // ADR-010 §二 的字段序是逐字契约 —— 反射扫公开实例字段。
            string[] names = typeof(FoldRow)
                .GetFields(System.Reflection.BindingFlags.Public
                           | System.Reflection.BindingFlags.Instance)
                .Select(f => f.Name)
                .ToArray();

            CollectionAssert.AreEqual(
                new[] { "Onset", "DiseaseId", "PatientId", "PatientSeed", "Outcome", "TEnd" },
                names,
                "折叠行恰为六字段且次序 = ADR-010 §二 逐字");
        }

        [Test]
        public void test_fold_rowKeepsPatientId_standaloneAssertion()
        {
            // 单列断言:不许被「字段数对」掩盖(ADR-006 Amendment B 第 3 条)。
            ArrangeFoldable();
            FoldResult result = HistoryFolder.Fold(_history, P, Predicate(), _decoder);

            Assert.AreEqual(P, result.Rows[0].PatientId,
                "折叠行必须保留 patient_id —— 折叠丢弃流位置,seed 是单向哈希");
        }

        [Test]
        public void test_fold_multiDisease_producesOneRowPerDisease()
        {
            AddDisease(1, lethality: 0);
            AddDisease(2, lethality: 1);
            _history.Add(Onset(10, P, 1));
            _history.Add(Onset(12, P, 2));
            _history.Add(Outcome(40, P, 2, PatientOutcome.Died));
            _history.Add(Outcome(50, P, 1, PatientOutcome.Recovered));

            FoldResult result = HistoryFolder.Fold(_history, P, Predicate(), _decoder);

            Assert.AreEqual(2, result.Rows.Count, "每病种一行");
            Assert.AreEqual(new[] { 1, 2 }, result.Rows.Select(r => r.DiseaseId).ToArray(),
                "按 (Onset, DiseaseId) 升序 ⇒ 确定性");
        }

        [Test]
        public void test_fold_rowsAreDeterministicallyOrdered()
        {
            // 同一输入两次求值逐字段相同(字节级夹具的前置)。
            AddDisease(1, lethality: 0);
            AddDisease(2, lethality: 0);
            _history.Add(Onset(30, P, 2));   // 故意乱序写入
            _history.Add(Onset(20, P, 1));
            _history.Add(Outcome(50, P, 2, PatientOutcome.Died));
            _history.Add(Outcome(60, P, 1, PatientOutcome.Recovered));

            var a = HistoryFolder.Fold(_history, P, Predicate(), _decoder);
            var b = HistoryFolder.Fold(_history, P, Predicate(), _decoder);

            CollectionAssert.AreEqual(a.Rows.ToArray(), b.Rows.ToArray(), "两次求值逐字段相同");
            Assert.AreEqual(new[] { 1, 2 }, a.Rows.Select(r => r.DiseaseId).ToArray(),
                "按 onset 升序,与写入序无关");
        }

        // ══════════════════ ④ SkillGrown 行豁免 ══════════════════

        [Test]
        public void test_fold_retainsSkillGrownRows()
        {
            ArrangeFoldable();
            _history.Add(SkillGrown(30, P, skill: 5, level: 3, seq: 1));

            FoldResult result = HistoryFolder.Fold(_history, P, Predicate(), _decoder);

            Assert.AreEqual(1, result.Retained.Count(e => e.Kind == EventKind.SkillGrown),
                "被折病人的 SkillGrown 行必须保留(ADR-026 §⑦ 条件 ④)");
        }

        [Test]
        public void test_fold_removesPatientRowsButKeepsOtherPatients()
        {
            ArrangeFoldable();
            _history.Add(Onset(11, OTHER, DISEASE));             // 别人的行
            _history.Add(Outcome(60, OTHER, DISEASE, PatientOutcome.Died));

            FoldResult result = HistoryFolder.Fold(_history, P, Predicate(), _decoder);

            Assert.AreEqual(0, result.Retained.Count(e => e.Patient.Value == P && e.Kind != EventKind.SkillGrown),
                "被折病人的非 SkillGrown 行全部抹除");
            Assert.AreEqual(2, result.Retained.Count(e => e.Patient.Value == OTHER),
                "其他病人的行不受影响");
        }

        [Test]
        public void test_fold_skillGrownReplayIsBitIdentical()
        {
            // ADR-026 §⑦ 的判据原话 = 「折叠之后 Level(s)@t 逐位一致」,不是「Folded 返回某值」。
            ArrangeFoldable();
            _history.Add(SkillGrown(30, P, skill: 5, level: 2, seq: 1));
            _history.Add(SkillGrown(35, P, skill: 5, level: 3, seq: 2));

            FoldResult result = HistoryFolder.Fold(_history, P, Predicate(), _decoder);

            Assert.AreEqual(ReplayLevel(_history, actor: 1, skill: 5),
                            ReplayLevel(result.Retained, actor: 1, skill: 5),
                            "折叠前后 Level(skill=5)@t 逐位一致");
            Assert.AreEqual(3, ReplayLevel(result.Retained, actor: 1, skill: 5),
                "最后一次升级值 = 3(哨兵 -1 不得被当成等级)");
        }

        /// <summary>从流里重放某 (actor, skill) 的当前等级 —— 只读 <c>SkillGrown</c> 行。</summary>
        private int ReplayLevel(IReadOnlyList<SimEvent> events, int actor, int skill)
        {
            int level = 0;
            foreach (SimEvent e in events)
            {
                if (e.Kind != EventKind.SkillGrown) continue;
                SkillGrownPayload p = _decoder.Decode<SkillGrownPayload>(EventKind.SkillGrown, e.Payload);
                if (p.ActorId == actor && p.SkillId == skill && p.Level != SkillGrownPayload.LevelNotGrown)
                    level = p.Level;
            }
            return level;
        }

        // ══════════════════ 未折 ⇒ 恒等 ══════════════════

        [Test]
        public void test_fold_notFolded_isIdentity()
        {
            AddDisease(DISEASE, lethality: 0);
            _history.Add(Onset(10, P, DISEASE));   // 无终态 ⇒ 谓词假

            FoldResult result = HistoryFolder.Fold(_history, P, Predicate(), _decoder);

            Assert.IsTrue(result.IsIdentity, "谓词假 ⇒ Rows 空");
            CollectionAssert.AreEqual(_history.ToArray(), result.Retained.ToArray(),
                "谓词假 ⇒ 流逐条不变(零改动)");
        }

        // ══════════════════ 流损坏 ⇒ fail-loud ══════════════════

        [Test]
        public void test_fold_duplicateOutcomeForSameDisease_throws()
        {
            ArrangeFoldable();
            _history.Add(Outcome(70, P, DISEASE, PatientOutcome.Died));   // 第二条

            var ex = Assert.Throws<InvalidDataException>(
                () => HistoryFolder.Fold(_history, P, Predicate(), _decoder));
            StringAssert.Contains("至多一条", ex.Message,
                "报文须点出 ADR-032 ⑤ 的有界性");
        }

        [Test]
        public void test_fold_outcomeWithoutOnset_throws()
        {
            // 有终态、无 onset ⇒ 折叠行 onset / patient_seed 无来源。不猜。
            AddDisease(DISEASE, lethality: 0);
            _history.Add(Outcome(50, P, DISEASE, PatientOutcome.Recovered));

            var ex = Assert.Throws<InvalidDataException>(
                () => HistoryFolder.Fold(_history, P, Predicate(), _decoder));
            StringAssert.Contains("DiseaseOnset", ex.Message, "报文须点名缺失的 onset 载体");
        }

        // ══════════════════ SaveService 构造函数注入形态 ══════════════════

        [Test]
        public void test_saveService_takesFoldedPredicateViaConstructor_only()
        {
            // ADR-025 §① 甲案裁定 ④:走构造函数注入,Checkpoint 签名不动。
            var ctors = typeof(SaveService).GetConstructors();
            Assert.AreEqual(1, ctors.Length, "SaveService 应恰有一个公开构造函数");
            var ps = ctors[0].GetParameters();
            Assert.AreEqual(3, ps.Length, "形参 = (basePath, codec, folded)");
            Assert.AreEqual(typeof(IFoldedPredicate), ps[2].ParameterType,
                "第三形参须是 IFoldedPredicate(住 Sim.Contracts)");
            Assert.IsTrue(ps[2].HasDefaultValue && ps[2].DefaultValue == null,
                "折叠尚未进存档路径 ⇒ 形参可选(不注入 = null)");
        }

        [Test]
        public void test_iSaveService_publicMethodSetUnchanged()
        {
            // ADR-010 :331 的反射断言形态:公开方法参数集不变 —— 本件不得给接口加形参。
            string[] methods = typeof(ISaveService).GetMethods()
                .Select(m => m.Name)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToArray();

            CollectionAssert.AreEqual(new[] { "LoadBytes", "SaveBytes" }, methods,
                "ISaveService 的公开方法集不得因折叠而变");
        }
    }
}
