// 垂直切片测试 — 验证核心循环: 病人出现 → 诊断 → 治疗 → 体征可查
//
// 目标: 1 病人 + 1 诊断 + 1 治疗, 无美术, 验证核心循环
//
// 核心循环:
//   1. 病人出现 (DiseaseOnset → 病史流) — 驱动 PatientSpawner(9 的写者, ADR-030)
//   2. 诊断 (CaseOpened → 病例流) — CaseOpenWriter(批次 D 真写者, 37)
//   3. 治疗 (DrugTreatmentApplied → 病史流) — 11 PrescribeFlow 驱动
//   4. 病人状态更新 (VitalsDto via 生产 DiseaseVitalsService, 批次 C)
//
// ── 测试模式(批次 F · 2026-10-10「禁 Fake 换真复评」)─────────────────────────
//   真装配 `CompositionRoot.Assemble(registry)` —— 与 vitals_chain_test 同款:
//   零 FakeEventSink(真 EventStream:路由 / Seq 发号 / 去重 / AC-15 有界性全链路)·
//   零 FakeVitalsQuery(生产 `IVitalsQuery` = DiseaseVitalsService)·
//   零 FakeIdAuthority(生产 IdAuthority 机制 A)· 零 FakePresenceQuery(生产 PresenceRegistry)。
//   本文件此前的「SetVitals 自问自答」「手搓 SimEvent 假 Append」两处假绿已随本批消除。
//
//   ⚠️ 仍为 Fake 的三端口(生产实装不存在 —— 登记归属,非本批裁,非假绿):
//     FakeConversion / FakeStore / FakeSkills(PrescribeFlow 的药材换算 / 库存 / 技能端口)
//     —— 归 19 药材库存 · 21a 加工 · 30 技能 各自实现轮;端口替换后本文件同批换真。
//   ⚠️ 病种 fixture = 镜像 `Tests/EditMode/DiseaseSimulation/synthetic_disease_fixture.cs`
//     (两测试程序集互不可见 —— EditMode.asmdef 为 Editor-only,PlayMode 不得引用;
//      两处同源镜像,数值轮须同批替换;验收口径承裁定①:合成占位非最终数值)。
//
// 参考: vitals_chain_test.cs(真装配驱动范式)· case_open_writer_test.cs(写者前置)
// 权威: design/gdd/disease-simulation.md F1/F2 · ADR-030 · ADR-008 · GDD case-system 规则二

using System.Collections;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.DiseaseSimulation;
using DaYiJingCheng.Sim.Prescription;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace DaYiJingCheng.Tests.PlayMode
{
    /// <summary>
    /// 垂直切片测试 — 验证核心游戏循环(真装配,禁 Fake 自洽环)。
    /// 驱动真生产路径: PatientSpawner (9) + CaseOpenWriter (37) + PrescribeFlow (11)
    /// + DiseaseVitalsService (体征链)。
    /// </summary>
    public class VerticalSliceTest
    {
        /// <summary>合成病种 id(镜像 synthetic_disease_fixture;1..8 P0 冻结清单之外)。</summary>
        private const int SyntheticDiseaseId = 9001;

        /// <summary>潜伏期刻度(镜像 fixture;给药前基线的取样窗口上界)。</summary>
        private const int IncubationTicks = 600;

        /// <summary>float 比较容差(承 vitals_chain_test 同款口径:吞两次 ToFloat 舍入,
        /// 远窄于任何公式性错误)。</summary>
        private const float Tol = 2e-4f;

        /// <summary>
        /// 体征回路断言的给药 tick = 潜伏期边界(τ = incubation)。
        /// <para>为何不能在潜伏期内给药:AC-26 潜伏期抑制**含处置**(<c>τ &lt; incubation ⇒
        /// Progress ≡ 0</c>,ProgressionEvaluator 提前返回)⇒ 潜伏期内治疗贡献恒不可见,
        /// Go/No-Go ⑤ 须在边界上验。</para>
        /// <para>为何恰取边界:τ = incubation 处自然曲线 = A_peak×(1−e⁰)/R_rise = **0**
        /// (Exp(0) 精确)⇒ 给药后 position 的全部增量归因于 DrugTreatmentApplied。</para>
        /// </summary>
        private const long DoseTick = IncubationTicks;

        // ══════════ 夹具(真装配)══════════

        private CompositionRootServices _bag;
        private PrescribePorts _prescribePorts;

        [SetUp]
        public void SetUp()
        {
            _bag = CompositionRoot.Assemble(new[] { CreateSyntheticEntry() });
            // doseBase = DOSE_BASE 合成占位:取 1 ⇒ dose_potency = potency × dose(直通)。
            // 取 100 会把药力 1/2 压成 0.005(0.5×1/100),对「位置变化 ≥ 0.4」的断言
            // 不可判 —— 裁定① 合成数值口径下 1 是本测试的自洽选择,数值轮不动本处。
            _prescribePorts = new PrescribePorts(
                new FakeConversion(), new FakeStore(), _bag.Presence, new FakeSkills(),
                doseBase: 1);
        }

        /// <summary>
        /// 生成病人并灌入在场(生产语义:9 创建后由装配层写面进在场视图;
        /// 该写面归属待裁,登记在 9 的实现轮 —— 本测试按其语义灌入,不裁归属)。
        /// </summary>
        private PatientId SpawnPresent(long tick)
        {
            var patient = _bag.PatientSpawner.SpawnNext(SyntheticDiseaseId, tick);
            _bag.Presence.AddPresent(patient, new WorldPos(0, 0, 0));
            return patient;
        }

        /// <summary>批次 D 真写者(每次新建:写者无状态,事件流从 bag 取)。</summary>
        private CaseOpenWriter MakeCaseWriter()
            => new CaseOpenWriter(_bag.EventSink, _bag.Presence, _bag.Encoder,
                                  _bag.Stream.Events);

        /// <summary>处方请求(剂量 / 药力 / 半衰期为合成占位,承裁定①)。</summary>
        private static PrescribeRequest BuildRequest(PatientId patient,
                                                     PrescriptionPolarity polarity)
            => new PrescribeRequest(
                itemKey: new ItemKey("willow_bark", ProcessingState.Raw),
                entry: new PrescriptionEntry(10, polarity, 0),
                actorId: 1,
                patientId: patient,
                selectedDose: 1,
                profile: new DrugProfile
                {
                    DoseRange = new DoseRange(1, 3),
                    DrugPotency = Fix.FromRational(1, 2),
                    HalfLife = Fix.FromRational(1, 1),
                    AxisOffsetByQuality = new[]
                    {
                        Fix.Zero, Fix.Zero, Fix.Zero, Fix.Zero, Fix.Zero
                    }
                },
                selectedQuality: 1,
                isHost: true);

        /// <summary>
        /// 镜像 <c>SyntheticDiseaseFixture.Create()</c>(EditMode 程序集 Editor-only,
        /// PlayMode 不可见)。数值 = 合成占位,数值轮两处同批替换。
        /// </summary>
        private static DiseaseRegistryEntry CreateSyntheticEntry()
        {
            return new DiseaseRegistryEntry
            {
                DiseaseId = SyntheticDiseaseId,
                DiseaseKey = "DIS_SYNTH_FIXTURE",
                Polarity = 1,           // 0=寒 1=热 2=平
                Severity = 3,
                Contagion = 1,
                Lethality = 1,
                TreatmentDifficulty = 5,
                RecoveryTime = 3600,
                RelapseChance = 50,
                ComorbidityFactor = 20,
                SeasonalMod = 30,
                AgeMod = 15,
                GenderMod = 10,
                OccupationMod = 10,
                RegionMod = 20,
                ClimateMod = 25,
                TreatableBy = null,      // 已拆独立轴文件(见 fixture 头注)
                Handle = null,
                Curve = new NaturalProgressCurve
                {
                    Incubation = IncubationTicks,
                    APeak = FixParse.Parse("1/2"),
                    TauRise = FixParse.Parse("300"),
                    TauPeak = 1800,
                    TauFall = FixParse.Parse("600"),
                    SelfLimit = true,
                    Plateau = false,
                    Sigma = FixParse.Parse("1/64"),
                    BoundaryMode = "monotone"
                },
                Relapse = new RelapseCurve
                {
                    ARel = FixParse.Parse("1/4"),
                    Interval = 2400,
                    TauFall = FixParse.Parse("300")
                },
                Scale = FixParse.Parse("1"),
                Signs = new[]
                {
                    new SignEntry("synth_pallor", SignChannels.Face),
                    new SignEntry("synth_tachypnea", SignChannels.Breath),
                    new SignEntry("synth_weak_voice", SignChannels.Voice | SignChannels.Posture)
                }
            };
        }

        // ══════════ 核心循环验证 ══════════

        /// <summary>
        /// 验证病人出现事件写入病史流 — 驱动 PatientSpawner(9 的 DiseaseOnset 写者)真生产路径。
        /// ADR-030 §③:写者 = 9 · §④:落病史流。真 EventStream 覆盖路由 / Seq 发号 / 有界性。
        /// </summary>
        [Test]
        public void test_diseaseOnset_writesToHistoryStream()
        {
            // Arrange(真装配已在 SetUp;病人未创建)

            // Act:驱动真生产路径
            var patientId = SpawnPresent(tick: 0);

            // Assert:事件确实写入病史流
            Assert.AreEqual(1, _bag.Stream.Count, "DiseaseOnset 应写入病史流");
            var e = _bag.Stream.Events[0];
            Assert.AreEqual(EventKind.DiseaseOnset, e.Kind);
            Assert.AreEqual(patientId, e.Patient, "事件的 Patient 应为新建病人");
            Assert.AreEqual(0L, e.Tick);
            Assert.AreEqual(StreamId.History, StreamRouting.Of(e.Kind),
                "DiseaseOnset 应路由到病史流");
            Assert.AreEqual(0L, e.Seq, "首个事件 header Seq 应由 EventStream 发号为 0");
        }

        /// <summary>
        /// 验证诊断记录事件写入病例流 — 驱动 CaseOpenWriter(批次 D 真写者)真生产路径。
        /// 前置复核(在场 + 未开案)在写者内对真流求值;Seq 仍由 EventStream 发号。
        /// </summary>
        [Test]
        public void test_caseOpened_writesToCaseStream()
        {
            // Arrange
            var patient = SpawnPresent(tick: 0);

            // Act:驱动真生产路径(写者内复核前置)
            var result = MakeCaseWriter().TryOpen(
                tick: 0, patient: patient, diseaseSnapshot: new DiseaseIdSet(0b1011UL));

            // Assert(SpawnPresent 已写一条 DiseaseOnset ⇒ 流内共 2 条)
            Assert.AreEqual(CaseOpenWriteResult.Opened, result,
                "在场 + 未开案 ⇒ 应立案并写入");
            Assert.AreEqual(2, _bag.Stream.Count,
                "DiseaseOnset(spawn)+ CaseOpened(立案)应共 2 条");
            Assert.AreEqual(EventKind.DiseaseOnset, _bag.Stream.Events[0].Kind,
                "首条应为 spawn 的 DiseaseOnset");
            var e = _bag.Stream.Events[1];
            Assert.AreEqual(EventKind.CaseOpened, e.Kind);
            Assert.AreEqual(patient, e.Patient);
            Assert.AreEqual(StreamId.Case, StreamRouting.Of(e.Kind),
                "CaseOpened 应路由到病例流");
            Assert.GreaterOrEqual(e.Seq, 0L, "Seq 应由 EventStream 发号(非 -1 哨兵)");
        }

        /// <summary>
        /// 验证治疗应用事件写入病史流 — 驱动 PrescribeFlow 真生产路径(真 EventStream)。
        /// </summary>
        [Test]
        public void test_drugTreatmentApplied_writesToHistoryStream()
        {
            // Arrange
            var patient = new PatientId(0);
            _bag.Presence.AddPresent(patient, new WorldPos(0, 0, 0));

            // Act:驱动 PrescribeFlow 真生产路径
            var outcome = PrescribeFlow.Prescribe(
                BuildRequest(patient, PrescriptionPolarity.Symptomatic),
                tick: 0, _prescribePorts, _bag.EventSink, _bag.Encoder);

            // Assert
            Assert.IsTrue(outcome.Applied, "处方应成功");
            Assert.AreEqual(1, _bag.Stream.Count, "治疗应用事件应写入病史流");
            var e = _bag.Stream.Events[0];
            Assert.AreEqual(EventKind.DrugTreatmentApplied, e.Kind);
            Assert.AreEqual(patient, e.Patient);
            Assert.AreEqual(StreamId.History, StreamRouting.Of(e.Kind),
                "DrugTreatmentApplied 应路由到病史流");
        }

        /// <summary>
        /// 验证体征回路:治疗事件后**生产** <c>IVitalsQuery</c> 返回值变化(Go/No-Go ⑤,路径禁 Fake)。
        /// GIVEN 驱动至给药前一 tick(τ &lt; incubation ⇒ 自然病程 0)WHEN PrescribeFlow 在
        /// 潜伏期边界写入对因药(Causal)后驱动同 tick 边沿 THEN 生产查询返回的位置 ≈ 药力
        /// (自然曲线在 τ = incubation 恰为 0,全部增量归因治疗 —— AC-26 潜伏期抑制含处置,
        /// 潜伏期内给药恒不可见,故边界为 Go/No-Go ⑤ 的最早可判点)。
        /// 此前本测试为 `SetVitals → GetVitals` 自问自答(Fake 自洽环),批次 F 换真。
        /// </summary>
        [Test]
        public void test_vitalsQuery_returnsAfterTreatment()
        {
            // Arrange:病人出现(真写者),驱动到给药前一 tick
            var patient = SpawnPresent(tick: 0);
            for (long t = 0; t < DoseTick; t++)
                _bag.VitalsService.OnTickEdge(t);

            // Act 0:给药前基线(τ = DoseTick−1 = 599 < Incubation ⇒ 自然病程 position = 0)
            float before = _bag.VitalsService.GetVitals(patient).Position;
            Assert.AreEqual(0f, before, Tol,
                $"给药前一 tick(τ={DoseTick - 1} < Incubation={IncubationTicks})内自然病程 position 应为 0");

            // Act 1:PrescribeFlow 真路径写入对因药(Causal ⇒ F1 药疗和式有贡献)
            var outcome = PrescribeFlow.Prescribe(
                BuildRequest(patient, PrescriptionPolarity.Causal),
                tick: DoseTick, _prescribePorts, _bag.EventSink, _bag.Encoder);
            Assert.IsTrue(outcome.Applied, "处方应成功");

            // Act 2:本边沿写入 → 本 tick 体征立即可见(DiseaseVitalsService 头注次序语义)
            _bag.VitalsService.OnTickEdge(DoseTick);
            float after = _bag.VitalsService.GetVitals(patient).Position;

            // Assert:治疗事件后返回值变化(生产查询,非 Fake 回写)
            Assert.Greater(after, before + 0.4f,
                $"治疗事件后体征位置须变化(合成药力 1/2 ⇒ 给药 tick 期望 ≈0.5;实测 before={before} after={after})");
        }

        /// <summary>
        /// 验证完整核心循环: 病人出现 → 诊断 → 治疗 → 生产体征查询。
        /// 三条事件全走真 EventStream(路由 / Seq / 有界性),体征走生产 DiseaseVitalsService。
        /// </summary>
        [UnityTest]
        public IEnumerator test_fullCoreLoop_patientToTreatment()
        {
            // Act:病人出现(9 的 DiseaseOnset 写者驱动)
            var patient = SpawnPresent(tick: 0);

            // Act:诊断(批次 D CaseOpenWriter 真写者驱动)
            var caseResult = MakeCaseWriter().TryOpen(
                0, patient, new DiseaseIdSet(0b1011UL));
            Assert.AreEqual(CaseOpenWriteResult.Opened, caseResult);

            // Act:驱动至给药前一 tick(τ = 599 < Incubation ⇒ 自然病程 0;
            // AC-26 潜伏期抑制含处置 ⇒ 给药须落在潜伏期边界才可判,见 DoseTick 头注)
            for (long t = 0; t < DoseTick; t++)
                _bag.VitalsService.OnTickEdge(t);
            float before = _bag.VitalsService.GetVitals(patient).Position;
            Assert.AreEqual(0f, before, Tol,
                $"给药前(τ={DoseTick - 1} < Incubation={IncubationTicks})自然病程 position 应为 0");

            // Act:治疗(11 PrescribeFlow 驱动;tick = 潜伏期边界,事件流 tick 单调 0,0,DoseTick)
            var outcome = PrescribeFlow.Prescribe(
                BuildRequest(patient, PrescriptionPolarity.Causal),
                tick: DoseTick, _prescribePorts, _bag.EventSink, _bag.Encoder);
            Assert.IsTrue(outcome.Applied, "处方应成功");

            // Assert:三条事件全部写入(病人出现 → 诊断 → 治疗)
            Assert.AreEqual(3, _bag.Stream.Count, "完整核心循环应产生三条事件");
            Assert.AreEqual(EventKind.DiseaseOnset, _bag.Stream.Events[0].Kind,
                "首条应为病人出现(9 的 DiseaseOnset)");
            Assert.AreEqual(EventKind.CaseOpened, _bag.Stream.Events[1].Kind);
            Assert.AreEqual(EventKind.DrugTreatmentApplied, _bag.Stream.Events[2].Kind);

            // Act + Assert:状态更新(生产查询;本边沿事件立刻可见)
            // τ = incubation 处自然曲线 = A_peak×(1−e⁰)/R_rise = 0 ⇒ 全部增量归因治疗
            _bag.VitalsService.OnTickEdge(DoseTick);
            float position = _bag.VitalsService.GetVitals(patient).Position;
            Assert.Greater(position, before + 0.4f,
                $"治疗后生产体征查询位置须变化(τ={DoseTick} 处自然曲线 0,药力 1/2 ⇒ 期望 ≈0.5;实测 before={before} after={position})");

            yield return null;
        }

        // ══════════ 边界情况 ══════════

        /// <summary>
        /// 验证无病人时诊断失败 — 驱动 CaseOpenWriter 真写者:前置复核返回具名结果码 + 零写入。
        /// </summary>
        [Test]
        public void test_caseOpened_withoutPatient_fails()
        {
            // Arrange:不创建任何病人(ghost 从未进在场登记簿)
            var writer = MakeCaseWriter();
            var ghost = new PatientId(99);

            // Act + Assert:不在场 ⇒ PatientAbsent,零写入
            var absentResult = writer.TryOpen(0, ghost, new DiseaseIdSet(1));
            Assert.AreEqual(CaseOpenWriteResult.PatientAbsent, absentResult,
                "无病人(不在场)⇒ 不立案");
            Assert.AreEqual(0, _bag.Stream.Count, "不立案 ⇒ 零写入");

            // Act + Assert:哨兵入参 ⇒ NoPatient,零写入(哨兵不得开出病例)
            var noneResult = writer.TryOpen(0, PatientId.None, new DiseaseIdSet(1));
            Assert.AreEqual(CaseOpenWriteResult.NoPatient, noneResult,
                "PatientId.None 属输入非法 ⇒ 不立案");
            Assert.AreEqual(0, _bag.Stream.Count, "两条不立案分支均须零写入");
        }

        /// <summary>
        /// 验证无诊断时 PrescribeFlow 不拦治疗 — 真前置检查归 CaseCloseDecider(未实现,登记)。
        /// 断言当前真实行为:无 CaseOpened 前置下治疗仍成功写入(非 GDD 终态,如实记录)。
        /// </summary>
        [Test]
        public void test_treatmentWithoutDiagnosis_prescribeFlowDoesNotGate()
        {
            // Arrange:病人出现但不诊断(无 CaseOpened)
            var patient = new PatientId(0);
            _bag.Presence.AddPresent(patient, new WorldPos(0, 0, 0));

            // Act:直接治疗(无前置诊断)— PrescribeFlow 不检查诊断前置
            var outcome = PrescribeFlow.Prescribe(
                BuildRequest(patient, PrescriptionPolarity.Symptomatic),
                tick: 0, _prescribePorts, _bag.EventSink, _bag.Encoder);

            // Assert:PrescribeFlow 不检查诊断前置,治疗仍成功
            Assert.IsTrue(outcome.Applied, "PrescribeFlow 不检查诊断前置");
            Assert.AreEqual(1, _bag.Stream.Count, "治疗事件仍写入");
        }

        // ══════════ 端口 Fake(生产实装缺位登记,见文件头)══════════

        private sealed class FakeConversion : IPortionsConversion
        {
            public int PortionsPerDose(ItemKey itemKey) => 2;
        }

        private sealed class FakeStore : IPortionsStore
        {
            public bool HasPortions(int playerId, ItemKey itemKey, int portions) => true;
            public int PeekLowestQuality(int playerId, ItemKey itemKey, int portions) => 1;
            public bool ConsumePortions(int playerId, ItemKey itemKey, int portions) => true;
        }

        private sealed class FakeSkills : ISkillGrowthPort
        {
            public int QueryLevel(int actorId, int skillId) => 5;
            public void EmitGrowth(int actorId, int skillId, int objectId, int noveltyHint,
                Fix kDifficulty, long tick, PatientId patientId) { }
        }
    }
}
