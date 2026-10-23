// 真表「终态 → 折叠链」端到端判据(2026-10-23 round29)。
//
// ── 为什么需要本文件 ──────────────────────────────────────────────────────────
// round27 落了 `PatientOutcomeChangedWriter`(写者),round28 证了「真表下病人确实会死」
// (F4 判得出 Deceased)。但**写者产出的事件是否真的被折叠链消费**,从未被任何测试观测过:
//   · `folded_predicate_test` 的病史流是**手搓 SimEvent**(直接 `_encoder.Encode` 构造),
//     不经过写者 ⇒ 它证「谓词本身对」,**不证**「写者写出的字节形状能被谓词读回」;
//   · round27 的写者测试断言了 Kind / 流别 / 载荷 round-trip,**没接谓词**;
//   · `HistoryFolder` 的行 / 抹除判据同样只在合成流上测过。
// ⇒ 「真表 + 真装配 + 真写者 → FoldedPredicate → HistoryFolder」这一整跳是空白。
//   本文件补这一跳。
//
// ── 判据形态(判别力的归属分开写清) ─────────────────────────────────────────
//
//   | # | 场景 | 期望 | 判别力归谁 |
//   |---|---|---|---|
//   | ① | 真表豁免面前提(lethal / plateau / relapse 三列) | 豁免类恰 = {14} | 数值轮改列会**点名转红** |
//   | ② | 破伤风:驱动判死 → 写者 → 谓词 → 折叠 | 可折,产 1 行,六字段逐项对得上两条真事件 | **本文件独有**(链贯通) |
//   | ③ | 心衰(plateau ∧ lethal):写者写终态 → 折叠 | **不豁免、可折** | 真表行**,不是**合成(对照 GDD :321) |
//   | ④ | 神经衰弱(plateau ∧ ¬lethal):写者写终态 → 折叠 | Terminal 真但**不可折**(豁免) | 真表行 |
//   | ⑤ | 开案挡住 → 结案放行 | 未结案不可折,结案后可折 | 条件③ 的**次序**依赖 |
//   | ⑥ | 同病种两条终态 | 折叠**当场抛**(不静默取一条) | `ADR-032 ⑤` 的下游执行体 |
//
// ⚠️ **② 的「死亡」归 F4(round28 已证),本文件不重复证**;本文件只从「判死那一刻」接管:
//    手动在判死 tick 调写者 —— 这**模拟**生产触发点,而生产触发点**仍未接线**
//    (`DiseaseVitalsService` 类头「不写流」与终态落流的冲突未裁,见写者类头)。
//    ⇒ 本文件证的是「**接线之后**链是通的」,**不证**「玩家能看见病人死」。
//
// ⚠️ **③④ 的终态是调用方直给的,不是 F4 判出的** —— `W = 0`(**数值轮未裁**)⇒ 痊愈支在
//    生产里不求值,真表的 plateau 型(SelfLimit / 迁延)也不会被判 Recovered。本文件用
//    `Recovered` 只是把「取一个合法终态」这件事冻结成输入,**验的是消费侧**(谓词 + 折叠),
//    **不声称**生产会产这个终态。
//    ⚠️ 2026-10-23 订正:原文把 `W = 0` 的原因写成「`OQ-9-1` 未裁」。**两件事已分家** ——
//    `OQ-9-1`(潜伏期起算点)已于 2026-10-23 裁为乙案;`W` 仍为 0 是**数值轮未裁**,与 OQ-9-1 无关。
//
// ── 覆盖不到(如实登记,禁借绿) ──────────────────────────────────────────────
// ① 生产触发点未接线(同上)—— 本文件手动调用写者;
// ② `relapse.*` 豁免分支在**真表上不可达**(全表零 relapse 块,见 ① 的前提断言)⇒ 该分支
//    只由 `folded_predicate_test` 的合成行覆盖,本文件不重复;
// ③ 条件③ 的病例流事件是**手工 Append 的最小事件**(`CaseStreamQuery` 只读 Kind + Patient
//    头部,不读载荷)⇒ 证的是「次序依赖」,不证 37 的写者路径;
// ④ 本文件是 EditMode,不是 PlayMode / 真启动序。
//
// 权威来源:
//   `design/gdd/persistence-service.md:316`(Folded 三条件)· `:321`(豁免类 = lethal=false ∧ plateau)
//   ADR-032 ③⑤⑥(写者 = 9 · 每病人每病种至多一条 · 条件① 只做存在性查询)
//   ADR-026 §⑦(折叠豁免 SkillGrown)· ADR-030(`DiseaseOnset` = 病种载体)
//   ADR-008 §六(病例流不折叠)· ADR-010(病史流折叠)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Interaction;
using DaYiJingCheng.Gameplay.Presentation;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.DiseaseSimulation
{
    /// <summary>
    /// 真表终态 → 折叠链(写者 → 谓词 → 折叠器)端到端判据。
    /// </summary>
    [TestFixture]
    public sealed class RealRegistryPatientOutcomeFoldTest
    {
        // ── 真表 id(前提断言逐行点名,缺行/改值都当场说清) ──
        private const int TetanusId = 13;        // lethal ∧ ¬plateau ⇒ 非豁免
        private const int HeartFailureId = 9;    // lethal ∧ plateau  ⇒ **非豁免**(GDD :321 的判别面)
        private const int NeurastheniaId = 14;   // ¬lethal ∧ plateau ⇒ 真表唯一豁免类
        private const int DysenteryId = 12;      // 旁观病人(用以证「折叠只动目标病人」)

        /// <summary>与 round28 同值:破伤风的首次判死落在这个窗口内(实测)。</summary>
        private const long DriveUntil = 10000;

        /// <summary>③④ 直给终态的 tick(无驱动 ⇒ 不需要过窗口)。</summary>
        private const long ManualWriteTick = 2000;

        // ══════════════════════════════════════════════════════════════════════
        // 盘上真产物(本文件自己不写盘)
        // ══════════════════════════════════════════════════════════════════════

        private static readonly string RepoRoot = ComputeRepoRoot();

        private static string ComputeRepoRoot(
            [System.Runtime.CompilerServices.CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(thisFile) ?? ".", "..", "..", "..", "..", ".."));

        private static string CookedDir => Path.Combine(RepoRoot, "unity", "Assets", "DataCooked");

        private static byte[] ReadCommitted(string fileName)
        {
            string path = Path.Combine(CookedDir, fileName);
            Assert.That(File.Exists(path), Is.True, $"盘上产物缺失:{path}(须先跑烘焙菜单)");
            return File.ReadAllBytes(path);
        }

        /// <summary>⚠️ 静态注入缝跨测试残留 ⇒ 每测前后复原(承 `reader_wiring_gate_test` 同款)。</summary>
        [SetUp]
        [TearDown]
        public void ResetInjectionSeam() => KindPriorityTable.Provider = null;

        // ══════════════════════════════════════════════════════════════════════
        // 装配 / 视图 / 驱动
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>真装配:<c>dataProvider</c> 给**盘上真产物** ⇒ 走 ② 级(真注册表 + 真 W/eps)。</summary>
        private static CompositionRootServices AssembleReal()
            => CompositionRoot.Assemble(
                registry: null,
                dataProvider: new MultiTableProvider()
                    .Add(DataCookedDisk.LoadDiseaseRegistry())
                    .Add(DiseaseActionAxisCookedCodec.Read(
                        ReadCommitted("disease_action_axis.cooked.bytes")))
                    .Add(PrescriptionActionsCookedCodec.Read(
                        ReadCommitted("prescription_actions.cooked.bytes")))
                    .Add(InteractionKindCookedCodec.Read(
                        ReadCommitted("interaction_kinds.cooked.bytes")))
                    .Add(DiagnosisCookedDisk.LoadSigns())
                    .Add(DiagnosisCookedDisk.LoadReadFloor())
                    .Add(DiagnosisCookedDisk.LoadJudgmentLexicon()));

        private static DiseaseRegistryDataSet Table() => DataCookedDisk.LoadDiseaseRegistry();

        private static DiseaseRegistryEntry Row(DiseaseRegistryDataSet table, int diseaseId)
        {
            for (int i = 0; i < table.Rows.Count; i++)
                if (table.Rows[i].DiseaseId == diseaseId) return table.Rows[i];
            Assert.Fail($"真表里没有 disease_id={diseaseId} —— 本判据的前提不成立");
            return null;
        }

        /// <summary>
        /// 按 <see cref="StreamRouting"/> 把**单条物理流**切成逻辑流视图 —— 与生产消费端的读法一致
        /// (`CompositionRoot` 只建一个 `EventStream`,三逻辑流靠 Kind 路由区分)。
        /// </summary>
        private static IReadOnlyList<SimEvent> View(CompositionRootServices bag, StreamId streamId)
            => bag.Stream.Events.Where(e => StreamRouting.Of(e.Kind) == streamId).ToList();

        /// <summary>谓词:历史 / 病例两视图 + 真表查表 + 真解码器(与生产装配同形)。</summary>
        private static FoldedPredicate Predicate(CompositionRootServices bag, DiseaseRegistryDataSet table)
            => new FoldedPredicate(
                () => View(bag, StreamId.History),
                () => View(bag, StreamId.Case),
                id =>
                {
                    for (int i = 0; i < table.Rows.Count; i++)
                        if (table.Rows[i].DiseaseId == id) return table.Rows[i];
                    return null;
                },
                bag.Decoder);

        /// <summary>驱动单个病人直到首次判死;<c>-1</c> = 窗口内未判死。</summary>
        private static long DriveToDeath(CompositionRootServices bag, PatientId patient)
        {
            DiseaseVitalsService svc = bag.VitalsService;
            for (long tick = 0; tick <= DriveUntil; tick++)
            {
                svc.OnTickEdge(tick);
                if (svc.GetOutcome(patient) == InjuryState.Deceased) return tick;
            }
            return -1;
        }

        /// <summary>病史流里第一个匹配 (病人, Kind) 的事件;缺则当场 fail。</summary>
        private static SimEvent FirstOf(IReadOnlyList<SimEvent> history, PatientId patient, EventKind kind)
        {
            foreach (SimEvent e in history)
                if (e.Patient.Value == patient.Value && e.Kind == kind) return e;
            Assert.Fail($"病史流里没有 {kind}(病人 {patient.Value})—— 前提不成立");
            return default;
        }

        // ══════════════════════════════════════════════════════════════════════
        // ① 前提 —— 真表的豁免面就是本判据假设的那样
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 真表三列前提。<b>为什么单列一条</b>:③④ 的绿全靠这三行字段,
        /// 数值轮一旦改 `lethal` / `plateau`,下面几条会**转红但归因不明**
        /// (可能是数据改了,也可能是谓词坏了)。本测把归因钉死在数据侧。
        /// </summary>
        [Test]
        public void test_realRegistry_exemptionFace_premise()
        {
            DiseaseRegistryDataSet table = Table();

            DiseaseRegistryEntry tetanus = Row(table, TetanusId);
            Assert.That(tetanus.DiseaseKey, Is.EqualTo("DIS_TETANUS"), "id 13 应是破伤风");
            Assert.That(tetanus.Lethality, Is.GreaterThan(0), "破伤风须 lethal(⇒ 不进豁免)");
            Assert.That(tetanus.Curve, Is.Not.Null, "破伤风须带曲线");
            Assert.That(tetanus.Curve.Plateau, Is.False, "破伤风须 ¬plateau");

            DiseaseRegistryEntry heartFailure = Row(table, HeartFailureId);
            Assert.That(heartFailure.DiseaseKey, Is.EqualTo("DIS_HEART_FAILURE"), "id 9 应是心衰");
            Assert.That(heartFailure.Lethality, Is.GreaterThan(0), "心衰须 lethal");
            Assert.That(heartFailure.Curve, Is.Not.Null, "心衰须带曲线");
            Assert.That(heartFailure.Curve.Plateau, Is.True,
                "心衰须 plateau ⇒ 是 `plateau ∧ lethal` 型(豁免类**不含**这一型,GDD :321)");

            DiseaseRegistryEntry neurasthenia = Row(table, NeurastheniaId);
            Assert.That(neurasthenia.DiseaseKey, Is.EqualTo("DIS_NEURASTHENIA"), "id 14 应是神经衰弱");
            Assert.That(neurasthenia.Lethality, Is.EqualTo(0), "神经衰弱须 ¬lethal");
            Assert.That(neurasthenia.Curve, Is.Not.Null, "神经衰弱须带曲线");
            Assert.That(neurasthenia.Curve.Plateau, Is.True, "神经衰弱须 plateau");

            // ── 全表豁免面:恰好 {14} 一行 ──
            List<int> exempt = table.Rows
                .Where(r => r.Lethality <= 0 && r.Curve != null && r.Curve.Plateau)
                .Select(r => r.DiseaseId)
                .ToList();
            Assert.That(exempt, Is.EquivalentTo(new[] { NeurastheniaId }),
                "真表豁免类(lethal=false ∧ plateau)须恰为 {14} —— 数值轮改列时本判据点名");

            // ── relapse 分支在真表上不可达(如实登记,不假装覆盖) ──
            Assert.That(table.Rows.All(r => r.Relapse == null), Is.True,
                "真表零 relapse 块 ⇒ 该豁免分支只由 folded_predicate_test 的合成行覆盖");
        }

        // ══════════════════════════════════════════════════════════════════════
        // ② 破伤风:判死 → 写者 → 谓词 → 折叠(整链贯通)
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// <b>本文件的主判据。</b>四段全用生产实现,零合成:
        /// 9 的 `PatientSpawner`(写真 `DiseaseOnset`)→ F4 判死(round28 已证,此处只取时刻)→
        /// `PatientOutcomeChangedWriter`(写真终态)→ `FoldedPredicate` → `HistoryFolder`。
        /// </summary>
        [Test]
        public void test_realRegistry_deathThenWriter_foldsIntoRow_endToEnd()
        {
            CompositionRootServices bag = AssembleReal();
            DiseaseRegistryDataSet table = Table();

            PatientId tetanusPatient = bag.PatientSpawner.SpawnNext(TetanusId, tick: 0);
            PatientId bystander = bag.PatientSpawner.SpawnNext(DysenteryId, tick: 0);

            // 同一病人一条 SkillGrown(真编码器 + 真流)—— 证折叠抹除豁免它(ADR-026 §⑦)
            bag.EventSink.Append(new SimEvent(5, tetanusPatient, -1, EventKind.SkillGrown,
                bag.Encoder.Encode(EventKind.SkillGrown,
                    new SkillGrownPayload(1, tetanusPatient.Value, 0, TetanusId, 0, level: 3))));

            long deathTick = DriveToDeath(bag, tetanusPatient);
            Assert.That(deathTick, Is.GreaterThanOrEqualTo(0),
                $"{DriveUntil} tick 内未判死 —— 前提不成立(round28 已证破伤风必死;此处红 = 求值链回归)");

            // ── 判死那一刻:写者落终态(模拟生产触发点;生产接线仍未做,见头注) ──
            var writer = new PatientOutcomeChangedWriter(bag.EventSink, bag.Encoder);
            Assert.That(writer.TryWrite(deathTick, tetanusPatient, TetanusId, InjuryState.Deceased),
                Is.EqualTo(PatientOutcomeWriteResult.Written), "判死须真正写成终态事件");

            // ── 谓词三条件逐条 ──
            FoldedPredicate predicate = Predicate(bag, table);
            Assert.IsTrue(predicate.Terminal(tetanusPatient.Value),
                "条件①:写者落的终态事件被谓词**经真解码器**读到(这一条就是本文件要补的那跳)");
            Assert.IsFalse(predicate.Exempt(tetanusPatient.Value),
                "条件②:破伤风 lethal ⇒ 非豁免");
            Assert.IsFalse(predicate.HasOpenCase(tetanusPatient.Value),
                "条件③:本测未开案");
            Assert.IsTrue(predicate.Folded(tetanusPatient.Value), "三条件成立 ⇒ 可折");

            // ── 折叠:产行 + 抹除 ──
            IReadOnlyList<SimEvent> history = View(bag, StreamId.History);
            int historyCount = history.Count;
            FoldResult result = HistoryFolder.Fold(history, tetanusPatient.Value, predicate, bag.Decoder);

            Assert.That(result.Rows.Count, Is.EqualTo(1), "单病种 ⇒ 恰 1 行");
            FoldRow row = result.Rows[0];

            // 行内容逐项 == 两条**真事件**的内容(期望值从流里解出来,不硬编码)
            DiseaseOnsetPayload onset = bag.Decoder.Decode<DiseaseOnsetPayload>(
                EventKind.DiseaseOnset,
                FirstOf(history, tetanusPatient, EventKind.DiseaseOnset).Payload);

            Assert.That(row.Onset, Is.EqualTo(onset.OnsetTick), "onset 取自 DiseaseOnset 载荷(ADR-030)");
            Assert.That(row.Onset, Is.EqualTo(0), "病人在 spawn 的 tick 0 出现");
            Assert.That(row.DiseaseId, Is.EqualTo(TetanusId), "病种 id = 终态载荷的 disease_id");
            Assert.That(row.PatientId, Is.EqualTo(tetanusPatient.Value), "patient_id 必留(ADR-006 Amd B)");
            Assert.That(row.PatientSeed, Is.EqualTo(onset.PatientSeed),
                "patient_seed 取自 onset 载荷(规则六:不重算)");
            Assert.That(row.Outcome, Is.EqualTo((int)PatientOutcome.Died), "九态 Deceased ⇒ 载荷 Died(2)");
            Assert.That(row.TEnd, Is.EqualTo(deathTick), "t_end = 终态事件**头部** Tick(不进载荷)");

            // 抹除面:该病人非 SkillGrown 全走,旁观病人一行不动
            Assert.That(result.Retained.Any(e => e.Patient.Value == tetanusPatient.Value
                    && e.Kind == EventKind.PatientOutcomeChanged), Is.False, "终态行已折进行");
            Assert.That(result.Retained.Any(e => e.Patient.Value == tetanusPatient.Value
                    && e.Kind == EventKind.DiseaseOnset), Is.False, "onset 行已折进行");
            Assert.That(result.Retained.Count(e => e.Patient.Value == tetanusPatient.Value
                    && e.Kind == EventKind.SkillGrown), Is.EqualTo(1),
                "SkillGrown 豁免抹除(ADR-026 §⑦ 条件④)");
            Assert.That(result.Retained.Any(e => e.Patient.Value == bystander.Value
                    && e.Kind == EventKind.DiseaseOnset), Is.True,
                "折叠只动目标病人(旁观病人的 onset 须原样留下)");

            Assert.That(bag.Stream.Count, Is.EqualTo(historyCount),
                "折叠是纯函数 —— 产新表,不回写真流");
        }

        // ══════════════════════════════════════════════════════════════════════
        // ③ 心衰(plateau ∧ lethal):真表上**不**豁免
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// GDD <c>:321</c> 的判别面落在**真行**上:「全 <c>plateau</c> 免豁」是错的 ——
        /// <c>plateau ∧ lethal</c> 型可折。合成夹具(`folded_predicate_test:184`)已测过同形,
        /// 本测证的是**真表的这一行真的走这条分支**。
        /// </summary>
        [Test]
        public void test_realRegistry_plateauAndLethal_notExempt_folds()
        {
            CompositionRootServices bag = AssembleReal();
            DiseaseRegistryDataSet table = Table();

            PatientId patient = bag.PatientSpawner.SpawnNext(HeartFailureId, tick: 0);

            // 终态直给(见头注 ⚠️:W=0 ⇒ 生产不求值痊愈支;此处只冻结输入,验消费侧)
            var writer = new PatientOutcomeChangedWriter(bag.EventSink, bag.Encoder);
            Assert.That(writer.TryWrite(ManualWriteTick, patient, HeartFailureId, InjuryState.Recovered),
                Is.EqualTo(PatientOutcomeWriteResult.Written), "写者须接受 Recovered(ADR-032 两终态并列)");

            FoldedPredicate predicate = Predicate(bag, table);
            Assert.IsTrue(predicate.Terminal(patient.Value), "条件①:终态事件在流里");
            Assert.IsFalse(predicate.Exempt(patient.Value),
                "条件②:心衰 lethal ⇒ **不**豁免(把 plateau 单独当豁免条件就会在这里红)");
            Assert.IsTrue(predicate.Folded(patient.Value), "⇒ 可折");

            FoldResult result = HistoryFolder.Fold(View(bag, StreamId.History), patient.Value,
                predicate, bag.Decoder);

            Assert.That(result.Rows.Count, Is.EqualTo(1), "单病种 ⇒ 恰 1 行");
            Assert.That(result.Rows[0].DiseaseId, Is.EqualTo(HeartFailureId));
            Assert.That(result.Rows[0].Outcome, Is.EqualTo((int)PatientOutcome.Recovered),
                "InjuryState.Recovered ⇒ 载荷 Recovered(1)");
            Assert.That(result.Rows[0].TEnd, Is.EqualTo(ManualWriteTick));
        }

        // ══════════════════════════════════════════════════════════════════════
        // ④ 神经衰弱(plateau ∧ ¬lethal):真表唯一豁免类,永不折叠
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 判别力:即使**终态事件已落流**(条件①成立),豁免类也**不得折叠** ——
        /// 若谓词把豁免判反(或漏判 plateau),本测会在 `Folded` 那一步红。
        /// </summary>
        [Test]
        public void test_realRegistry_plateauNonLethal_exempt_neverFolds()
        {
            CompositionRootServices bag = AssembleReal();
            DiseaseRegistryDataSet table = Table();

            PatientId patient = bag.PatientSpawner.SpawnNext(NeurastheniaId, tick: 0);

            var writer = new PatientOutcomeChangedWriter(bag.EventSink, bag.Encoder);
            Assert.That(writer.TryWrite(ManualWriteTick, patient, NeurastheniaId, InjuryState.Recovered),
                Is.EqualTo(PatientOutcomeWriteResult.Written), "终态**确实**写进去了(条件①成立的前提)");

            FoldedPredicate predicate = Predicate(bag, table);
            Assert.IsTrue(predicate.Terminal(patient.Value), "条件①成立 —— 豁免不是靠「没有终态」蒙对的");
            Assert.IsTrue(predicate.Exempt(patient.Value),
                "条件②:神经衰弱 ¬lethal ∧ plateau ⇒ 豁免");
            Assert.IsFalse(predicate.Folded(patient.Value), "⇒ 不可折(豁免挡住,与条件①无关)");

            IReadOnlyList<SimEvent> history = View(bag, StreamId.History);
            FoldResult result = HistoryFolder.Fold(history, patient.Value, predicate, bag.Decoder);

            Assert.IsTrue(result.IsIdentity, "谓词假 ⇒ 恒等");
            Assert.That(result.Rows.Count, Is.EqualTo(0), "零行");
            CollectionAssert.AreEqual(history, result.Retained, "Retained 与入参逐条相同");
        }

        // ══════════════════════════════════════════════════════════════════════
        // ⑤ 病例流次序:开案挡住,结案放行
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 条件③ 是**次序**判据(`OpenCaseOf` 前缀扫描),不是集合判据:
        /// 同一对开/结案事件,结案在开案**之后**才算「无未结案」。
        /// 病例事件用最小形态(`CaseStreamQuery` 只读 Kind + Patient 头部,不读载荷)。
        /// </summary>
        [Test]
        public void test_realRegistry_openCaseBlocksFold_closingUnblocks()
        {
            CompositionRootServices bag = AssembleReal();
            DiseaseRegistryDataSet table = Table();

            PatientId patient = bag.PatientSpawner.SpawnNext(TetanusId, tick: 0);
            long deathTick = DriveToDeath(bag, patient);
            Assert.That(deathTick, Is.GreaterThanOrEqualTo(0), "前提:破伤风须判死");

            var writer = new PatientOutcomeChangedWriter(bag.EventSink, bag.Encoder);
            Assert.That(writer.TryWrite(deathTick, patient, TetanusId, InjuryState.Deceased),
                Is.EqualTo(PatientOutcomeWriteResult.Written));

            FoldedPredicate predicate = Predicate(bag, table);
            Assert.IsTrue(predicate.Folded(patient.Value), "基线:无开案 ⇒ 可折");

            // ── 开案 ──
            bag.EventSink.Append(new SimEvent(deathTick + 1, patient, -1, EventKind.CaseOpened, default));
            Assert.IsTrue(predicate.HasOpenCase(patient.Value), "开案后须见未结案");
            Assert.IsFalse(predicate.Folded(patient.Value), "未结案 ⇒ 条件③ 挡住");

            // ── 结案(在开案之后) ──
            bag.EventSink.Append(new SimEvent(deathTick + 2, patient, -1, EventKind.CaseClosed, default));
            Assert.IsFalse(predicate.HasOpenCase(patient.Value), "结案后无未结案");
            Assert.IsTrue(predicate.Folded(patient.Value), "⇒ 恢复可折(次序依赖,不是集合)");
        }

        // ══════════════════════════════════════════════════════════════════════
        // ⑥ 同病种两条终态:折叠当场抛(ADR-032 ⑤ 的下游执行体)
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// round27 已登记「写者**不**做幂等」(终态吸收 ⇒ 重复 = 上游缺陷)。
        /// <c>EventStream</c> 的去重也接不住它:未发号事件的键含**载荷身份**,
        /// 写者每次 `Encode` 出新的 blob ⇒ 两条都进流。⇒ 这条规则的下游执行体就是
        /// <see cref="HistoryFolder"/> 的「同病种第二条终态 ⇒ `InvalidDataException`」。
        /// 本测证它**真的会抛**,而不是静默取一条(健康的数据形状前提被破坏时不得猜)。
        /// </summary>
        [Test]
        public void test_realRegistry_duplicateTerminal_forSameDisease_failsLoud()
        {
            CompositionRootServices bag = AssembleReal();
            DiseaseRegistryDataSet table = Table();

            PatientId patient = bag.PatientSpawner.SpawnNext(HeartFailureId, tick: 0);
            var writer = new PatientOutcomeChangedWriter(bag.EventSink, bag.Encoder);

            Assert.That(writer.TryWrite(ManualWriteTick, patient, HeartFailureId, InjuryState.Recovered),
                Is.EqualTo(PatientOutcomeWriteResult.Written));
            Assert.That(writer.TryWrite(ManualWriteTick + 100, patient, HeartFailureId, InjuryState.Recovered),
                Is.EqualTo(PatientOutcomeWriteResult.Written),
                "写者无幂等(round27 已登记)—— 两条都进流,由折叠器拦");
            Assert.That(View(bag, StreamId.History)
                    .Count(e => e.Patient.Value == patient.Value
                                && e.Kind == EventKind.PatientOutcomeChanged),
                Is.EqualTo(2), "两条终态事件**都**在流里(EventStream 去重键含载荷身份,接不住它)");

            FoldedPredicate predicate = Predicate(bag, table);
            Assert.IsTrue(predicate.Folded(patient.Value), "谓词面看到终态即真(存在性查询,ADR-032 ⑥)");

            int streamCountBefore = bag.Stream.Count;
            var ex = Assert.Throws<InvalidDataException>(() =>
                HistoryFolder.Fold(View(bag, StreamId.History), patient.Value, predicate, bag.Decoder));
            StringAssert.Contains("第二条终态事件", ex.Message,
                "须点名流损坏原因(而不是笼统的 InvalidDataException)");
            Assert.That(bag.Stream.Count, Is.EqualTo(streamCountBefore), "折叠失败不回写流");
        }

        // ══════════════════════════════════════════════════════════════════════
        // 夹具 plumbing
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 只装本文件用到的表;缺表 ⇒ <b>抛</b>(「装配问到了没准备的表」本身就是接线错的信号)。
        /// </summary>
        private sealed class MultiTableProvider : IDataProvider
        {
            private readonly Dictionary<Type, object> _byType = new Dictionary<Type, object>();

            public MultiTableProvider Add<TDataSet>(in TDataSet dataSet) where TDataSet : struct
            {
                _byType[typeof(TDataSet)] = dataSet;
                return this;
            }

            public TDataSet Load<TDataSet>() where TDataSet : struct
            {
                if (_byType.TryGetValue(typeof(TDataSet), out object boxed))
                    return (TDataSet)boxed;
                throw new InvalidOperationException(
                    $"MultiTableProvider 无 {typeof(TDataSet).Name} —— 本测不该问到它(真问 = 接线错)");
            }
        }
    }
}
