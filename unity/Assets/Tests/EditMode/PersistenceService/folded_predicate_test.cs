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
// ⚠️ **AC ⑧「折叠后重放等价」的判据面只覆盖 skill-growth 一维(2026-10-22 · QA 条件 C-6)**
//   QA plan S18-2 的「What to test」第 8 条要求:折叠 + 重放 ⇒ 与未折叠全量重放同结果。
//   本文件只有 `test_fold_skillGrownReplayIsBitIdentical` 一条,覆盖的是 **`Level(s)@t`**
//   (ADR-026 §⑦ 的原文判据)。**general 意义的「重放等价」本文件不成立**,原因是实现层面的:
//   `HistoryFolder` 抹除该病人**全部**病史行(减 `SkillGrown`)⇒ 被抹的 `CareApplied` /
//   `DrugTreatmentApplied` 等**无法从保留流重放**。这不是夹具偷懒,是折叠语义本身
//   (见 `HistoryFolder.cs` 头注 + `OQ-7a-10`)。
//   ⇒ **不得**把本文件读成「AC ⑧ 已全维验证」。补其余病史行等价判据须先裁 `OQ-7a-10`。
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

        // ══════════ ① 全称口径(OQ-7a-10 裁定 A · 2026-10-24)══════════
        //
        // 收紧前 = ∃(任一病种终态即可);收紧后 = ∀(每个病种皆终态)。
        // 多病种当下可达:D-8-6 共病合成已由用户提级 P0 阻塞,AC-31 原文即举
        // 「病人同时有 position = 3/10 与 4/5 两个病」。

        [Test]
        public void test_folded_multiDisease_partialTerminal_isNotTerminal()
        {
            // A 已终态、B 仍在演进 ⇒ 条件 ① 不成立。
            // 收紧前此例会**误判为终态**,随后 Fold 抹掉 B 的全部病程且不产 B 的折叠行。
            AddDisease(1, lethality: 0);
            AddDisease(2, lethality: 0);
            _history.Add(Onset(10, P, 1));
            _history.Add(Onset(12, P, 2));
            _history.Add(Outcome(40, P, 1, PatientOutcome.Recovered));

            Assert.IsFalse(Predicate().Terminal(P),
                "B 无 PatientOutcomeChanged ⇒ 不是「每个病种皆终态」⇒ 条件 ① 不成立");
            Assert.IsFalse(Predicate().Folded(P),
                "整体不可折 —— 否则 Erase 会静默抹掉 B 的病程");
        }

        [Test]
        public void test_folded_multiDisease_allTerminal_isTerminal()
        {
            // 两个病种都终态 ⇒ 条件 ① 成立(收紧不得把正当情形也否掉)。
            AddDisease(1, lethality: 0);
            AddDisease(2, lethality: 1);
            _history.Add(Onset(10, P, 1));
            _history.Add(Onset(12, P, 2));
            _history.Add(Outcome(40, P, 1, PatientOutcome.Recovered));
            _history.Add(Outcome(50, P, 2, PatientOutcome.Died));

            Assert.IsTrue(Predicate().Terminal(P), "两病种皆终态 ⇒ 条件 ① 成立");
        }

        [Test]
        public void test_folded_noOnsetAtAll_isNotTerminal_notVacuouslyTrue()
        {
            // 空集 D_p = ∅ **不**取真空为真 —— 否则 Fold 会产 0 行却抹掉全部病史行(不可逆)。
            // 构造:只有一条别的 Kind,没有任何 DiseaseOnset。
            _history.Add(SkillGrown(10, P, skill: 1, level: 2));

            Assert.IsFalse(Predicate().Terminal(P),
                "无 DiseaseOnset ⇒ D_p = ∅ ⇒ 条件 ① 必须为 false(宁可漏折,不可误折)");
            Assert.IsFalse(Predicate().Folded(P), "整体不可折");
        }

        [Test]
        public void test_folded_orphanOutcome_throwsInsteadOfBeingSwallowed()
        {
            // 孤儿终态(有 outcome、无对应 onset)= **流损坏**,必须 fail-loud。
            // ⚠️ 这条是 2026-10-24 收紧时**实测抓到的真缺陷**:空集短路会把它静默吞掉 ——
            //    该病人 D_p = ∅ ⇒ 若先判空集返回 false,Fold 就永远走不到 BuildRows 的抛错点。
            _history.Add(Outcome(50, P, DISEASE, PatientOutcome.Recovered));

            var ex = Assert.Throws<InvalidDataException>(() => Predicate().Terminal(P));
            StringAssert.Contains("DiseaseOnset", ex.Message, "报文须点名缺失的 onset 载体");
        }

        [Test]
        public void test_folded_orphanOutcomeAlongsideValidOnset_throws()
        {
            // 非空 D_p 下的孤儿:病种 1 有 onset+outcome,病种 2 只有 outcome。
            // 不得因为「病种 1 已终态」就放过病种 2 的损坏。
            AddDisease(1, lethality: 0);
            AddDisease(2, lethality: 0);
            _history.Add(Onset(10, P, 1));
            _history.Add(Outcome(40, P, 1, PatientOutcome.Recovered));
            _history.Add(Outcome(50, P, 2, PatientOutcome.Died));

            var ex = Assert.Throws<InvalidDataException>(() => Predicate().Terminal(P));
            StringAssert.Contains("DiseaseOnset", ex.Message);
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
        public void test_fold_multiDisease_partialTerminal_erasesNothing()
        {
            // ⚠️ 端到端补强(`OQ-7a-10` 裁定 A 的**危害面**,不只断言谓词返回值)。
            //
            // 上一条 `test_folded_multiDisease_partialTerminal_isNotTerminal` 只证
            // 「谓词说 false」;但危害是 **`Erase` 真把 B 的病程抹掉了**。
            // 谓词假 ⇒ `Fold` 在 `:88` 短路 ⇒ 流逐条不变 —— 这条把那个「⇒」钉住。
            // 缺了它,谓词与折叠动作之间的一致性只在推理里,不在夹具里。
            AddDisease(1, lethality: 0);
            AddDisease(2, lethality: 0);
            _history.Add(Onset(10, P, 1));
            _history.Add(Onset(12, P, 2));
            _history.Add(Outcome(40, P, 1, PatientOutcome.Recovered));   // 仅 A 终态

            FoldResult result = HistoryFolder.Fold(_history, P, Predicate(), _decoder);

            Assert.IsTrue(result.IsIdentity, "部分终态 ⇒ 不折 ⇒ Rows 空");
            CollectionAssert.AreEqual(_history.ToArray(), result.Retained.ToArray(),
                "B 仍在演进 ⇒ 其 onset 与全部病程行必须逐条保留(误折不可逆)");
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
            // 🔁 2026-10-24 S19-1:3 → 6(basePath, codec, folded, narrativeContext, replaySink, compose)。
            //    `folded` 保持第 3 位 ⇒ 下方 ps[2] 类型断言仍成立,只改计数。
            // 🔁 2026-10-24 TD-persist-03:6 → 7(replaySink 之后增 foldedRowSink ——
            //    折叠段原始字节交出委托;compose 顺移至第 7 位)。
            Assert.AreEqual(7, ps.Length,
                "形参 = (basePath, codec, folded, narrativeContext, replaySink, foldedRowSink, compose)");
            Assert.AreEqual(typeof(IFoldedPredicate), ps[2].ParameterType,
                "第三形参须是 IFoldedPredicate(住 Sim.Contracts)");
            Assert.IsTrue(ps[2].HasDefaultValue && ps[2].DefaultValue == null,
                "折叠尚未进存档路径 ⇒ 形参可选(不注入 = null)");
            Assert.AreEqual(typeof(Func<bool>), ps[3].ParameterType,
                "第四形参须是 Func<bool>(叙事上下文钩子,一个量)");
            Assert.AreEqual(typeof(Action<IReadOnlyList<SimEvent>>), ps[4].ParameterType,
                "第五形参须是 Action<IReadOnlyList<SimEvent>>(重放交出)");
            Assert.AreEqual(typeof(Action<byte[]>), ps[5].ParameterType,
                "第六形参须是 Action<byte[]>(折叠段原始字节交出 —— 本类看不见 FoldRow,TD-persist-03)");
            Assert.AreEqual(typeof(Func<SaveSlot, byte[]>), ps[6].ParameterType,
                "第七形参须是 Func<SaveSlot, byte[]>(组版委托,折叠接线落点)");
            for (int i = 3; i < 7; i++)
                Assert.IsTrue(ps[i].HasDefaultValue && ps[i].DefaultValue == null,
                    $"形参 {i} 须可选(不注入 = null ⇒ fail-closed)");
        }

        [Test]
        public void test_iSaveService_publicMethodSetIsFive()
        {
            // ADR-010 :331 的反射断言形态:S19-1 后公开方法集恰 = 五个。
            // 🔁 2026-10-24:原断言恰 = {LoadBytes, SaveBytes};本件加三方法后必红,已改为五。
            string[] methods = typeof(ISaveService).GetMethods()
                .Select(m => m.Name)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToArray();

            CollectionAssert.AreEqual(
                new[] { "Checkpoint", "Load", "LoadBytes", "SaveBytes", "SaveOnExit" }, methods,
                "ISaveService 的公开方法集 = {Checkpoint, Load, LoadBytes, SaveBytes, SaveOnExit}");
        }

        [Test]
        public void test_iSaveService_checkpointAndLoadTakeOnlySaveSlot()
        {
            // ADR-010 :331 逐字「反射断言:公开方法参数集恰等于 {SaveSlot}」
            // —— 不得给 Checkpoint / Load 加形参,外部输入一律走构造注入。
            foreach (var name in new[] { "Checkpoint", "Load" })
            {
                var m = typeof(ISaveService).GetMethod(name);
                Assert.IsNotNull(m, $"ISaveService 缺方法 {name}");
                var ps = m.GetParameters();
                Assert.AreEqual(1, ps.Length, $"{name} 须恰有一个形参");
                Assert.AreEqual(typeof(SaveSlot), ps[0].ParameterType,
                    $"{name} 的形参须是 SaveSlot(不得加形参)");
            }

            var exit = typeof(ISaveService).GetMethod("SaveOnExit");
            Assert.IsNotNull(exit, "ISaveService 缺 SaveOnExit");
            Assert.AreEqual(0, exit.GetParameters().Length, "SaveOnExit 不取形参");
        }
    }
}
