// F4 求值接线 —— `DiseaseVitalsService.StepAll` → `F4OutcomeEvaluator`(2026-10-22 第三轮立)。
//
// 权威来源:
//   GDD disease-simulation.md:1080-1125 —— F4 判定顺序与各支条件
//   GDD disease-simulation.md:1126-1133 —— `last_intervention` 三通道
//   GDD disease-simulation.md:1135-1140 —— 照护杠杆适用集(破伤风不在内)
//   GDD Tuning :1743-1744 —— `W` / `TREND_EPS` 均 *待定*
//   ADR-005 —— 主机唯一 Step;病史流是唯一真源
//   ADR-016 §二 —— 病人侧不吃玩家的战伤破例
//
// ── 本文件钉的是什么 ──────────────────────────────────────────────────────────
// 上一轮交付的是「**可求值的纯函数**」,生产调用点 = 0 —— 即 `F4OutcomeEvaluator`
// 从未被任何 tick 循环调用过。本文件钉的是**接线本身**,判据是:
//   · 走到 Step 边沿后,`GetOutcome(p)` 返回**由 F1 读数算出的**九态;
//   · 且该值**随 position 变化**(不是常量、不是默认值);
//   · 且死亡支**受窗口约束**(接线把 `onsetTick` / `tick` / 处置通道真的传进去了,
//     不是传了 `0` 让窗口恒过)。
//
// ── 真装配,零 Fake ───────────────────────────────────────────────────────────
// 与 `vitals_chain_test` 同款:`CompositionRoot.Assemble` → 真 `EventStream` +
// 真 `PayloadEncoder` + 真 `PatientSpawner`。病人由 `PatientSpawner.SpawnNext` 建档。

using System;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.DiseaseSimulation;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.DiseaseSimulation
{
    /// <summary>F4 求值接线 —— Step 边沿后九态可查、随读数变化、受窗口约束。</summary>
    public class F4WiringTest
    {
        /// <summary>处置 id(`TreatableBy == null` ⇒ 离牌门不拦)。</summary>
        private const int TreatmentId = 7;

        /// <summary>一条真装配的体征链 + 单病人。</summary>
        private sealed class Chain
        {
            public CompositionRootServices Bag;
            public DiseaseVitalsService Svc;
            public PatientId Patient;
            public long Tick = -1;

            public void DriveTo(long target)
            {
                while (Tick < target)
                {
                    Tick++;
                    Svc.OnTickEdge(Tick);
                }
            }

            public InjuryState Outcome() => Svc.GetOutcome(Patient);
        }

        private static Chain OpenChain(DiseaseRegistryEntry entry)
        {
            var bag = CompositionRoot.Assemble(new[] { entry });
            var chain = new Chain { Bag = bag, Svc = bag.VitalsService };
            chain.Patient = bag.PatientSpawner.SpawnNext(entry.DiseaseId, tick: 0);
            return chain;
        }

        /// <summary>
        /// 急性保持型致死病种 —— `position` 随 τ 上升后**恒定在 A_peak/SCALE**,
        /// 便于把「越阈」做成 tick 的函数而不是曲线的函数。
        /// </summary>
        private static DiseaseRegistryEntry AcuteLethal(int aPeak = 70, int scale = 100)
        {
            return new DiseaseRegistryEntry
            {
                DiseaseId = 60,
                DiseaseKey = "DIS_F4_WIRING_LETHAL",
                Kind = DiseaseKind.Disease,
                Lethality = 2,
                Scale = new Fix(scale * Fix.OneRaw),
                Curve = new NaturalProgressCurve
                {
                    Incubation = 0,
                    APeak = new Fix(aPeak * Fix.OneRaw),
                    TauRise = new Fix(10 * Fix.OneRaw),
                    TauPeak = 100,
                    TauFall = new Fix(100 * Fix.OneRaw),
                    Plateau = false,
                    SelfLimit = false,        // 急性保持型 ⇒ Base 恒 = A_peak(τ ≥ τ_peak)
                    BoundaryMode = "monotone",
                },
            };
        }

        private static void AppendCausalDrug(Chain c, long tick, Fix potency, long halfLife)
        {
            var payload = new DrugTreatmentAppliedPayload(
                tick: tick, treatmentId: TreatmentId, actorId: 1,
                polarity: ProgressionEvaluator.PolarityCausal,
                drugPotency: potency, halfLife: halfLife, seq: 0,
                contraindicationBand: DrugTreatmentAppliedPayload.NoContraindication);
            c.Bag.EventSink.Append(new SimEvent(
                tick, c.Patient, -1, EventKind.DrugTreatmentApplied,
                c.Bag.Encoder.Encode(EventKind.DrugTreatmentApplied, payload)));
        }

        /// <summary>同一急性保持型曲线,但换成可指定病种键(照护适用集按 <c>DiseaseKey</c> 判)。</summary>
        private static DiseaseRegistryEntry AcuteLethalWithKey(string diseaseKey)
        {
            DiseaseRegistryEntry entry = AcuteLethal();
            entry.DiseaseKey = diseaseKey;
            return entry;
        }

        /// <summary>追加一条护理动作事件(起 = phase 0 · 止 = phase 1)。</summary>
        private static void AppendCare(Chain c, long tick, int phase)
        {
            var payload = new CareAppliedPayload(tick, actionId: 1, caregiver: 1, phase: phase);
            c.Bag.EventSink.Append(new SimEvent(
                tick, c.Patient, -1, EventKind.CareApplied,
                c.Bag.Encoder.Encode(EventKind.CareApplied, payload)));
        }

        // ══════════════════ ① 接线存在:Step 后九态可查 ══════════════════

        /// <summary>
        /// **核心判据**:`GetOutcome` 在 Step 之前抛、之后返回 —— 证明接线真的在 StepAll 里。
        /// <para>若 `StepAll` 不写 `_outcomes`,本测试在第二段抛
        /// <see cref="PatientCourseNotFoundException"/>。</para>
        /// </summary>
        [Test]
        public void test_outcome_unavailableBeforeStep_availableAfter()
        {
            Chain c = OpenChain(AcuteLethal());

            Assert.Throws<PatientCourseNotFoundException>(() => c.Outcome(),
                "未走过任何 tick 边沿 ⇒ 无九态(拒绝返回默认值)");

            c.DriveTo(0);

            Assert.DoesNotThrow(() => c.Outcome(),
                "Step 边沿后必须有九态 —— 否则 F4 仍未接线");
        }

        // ══════════════════ ② 值由 F1 读数算出(随 position 变) ══════════════════

        /// <summary>
        /// 九态**随曲线变化** —— 不是常量。
        /// <para>急性保持型 + `A_peak/SCALE = 0.70` ⇒ τ ≥ τ_peak 后 position 恒 0.70,
        /// 落在 `[DEATH=0.58, 1)` 区间。故:</para>
        /// <list type="bullet">
        /// <item>τ = 0(position = 0)⇒ 非 Deceased;</item>
        /// <item>τ = τ_peak(越过 DEATH)⇒ 越阈,但窗口未过 ⇒ 不判死,落 <c>Coma</c>
        /// (≥ COMA = 0.45)。</item>
        /// </list>
        /// <para>若接线把 `position` 传错量纲(如把 raw Progress 当 position),
        /// 第二条会是 Deceased(0.70 raw 远小于 DEATH raw,反而不越阈)——
        /// 两种错法都会让本条红。</para>
        /// </summary>
        [Test]
        public void test_outcome_tracksPosition_notConstant()
        {
            Chain c = OpenChain(AcuteLethal(aPeak: 70, scale: 100));

            c.DriveTo(0);
            InjuryState atOnset = c.Outcome();
            Assert.AreNotEqual(InjuryState.Deceased, atOnset,
                "τ = 0 时 position = 0,不可能已死");

            c.DriveTo(200);   // τ = 200 > τ_peak = 100 ⇒ position = 0.70 ≥ DEATH(0.58)
            InjuryState afterPeak = c.Outcome();

            Assert.AreEqual(InjuryState.Coma, afterPeak,
                "position = 0.70 ≥ COMA(0.45)但窗口未过 ⇒ 昏迷(死亡支被窗口挡住)");
        }

        // ══════════════════ ③ 窗口真的在起作用 ══════════════════

        /// <summary>
        /// **窗口约束接线判据**:同一 position 下,「刚过阈」不死、「过阈 + 过窗口」才死。
        /// <para>若接线把 `currentTick` / `onsetTick` 传成常量(如都传 0),
        /// `t − last_intervention` 恒 0 ⇒ **永不判死** ⇒ 第二条红。</para>
        /// </summary>
        [Test]
        public void test_death_requiresWindowElapsed()
        {
            Chain c = OpenChain(AcuteLethal());

            c.DriveTo(200);      // position 已越 DEATH,但 t − onset = 200 < 3600
            Assert.AreEqual(InjuryState.Coma, c.Outcome(),
                "越阈但窗口未过 ⇒ 昏迷,不判死(铁律一:不能一越线就死)");

            c.DriveTo(StateMachine.TREATMENT_WINDOW + 10);
            Assert.AreEqual(InjuryState.Deceased, c.Outcome(),
                "越阈 + 窗口已过(从未被治过 ⇒ last_intervention = onset_tick)⇒ 死亡");
        }

        // ══════════════════ ④ 有效对因处置刷新窗口(通道 ① 接通) ══════════════════

        /// <summary>
        /// **处置通道真的接进去了**:一条有效对因处置把死亡时刻**推后**。
        /// <para>判据构造:两链同时驱动到 `TREATMENT_WINDOW + 10` —— 无处置者已死;
        /// 有处置者(处置在 τ = 3000)的 `last_intervention = 3000`,
        /// 故 `t − last_intervention = 3610 − 3000 = 610 < 3600` ⇒ **仍昏迷**。</para>
        /// <para>若接线不传处置通道(恒 <c>None</c>),有处置者也会死 ⇒ 本条红。</para>
        /// </summary>
        [Test]
        public void test_causalTreatment_refreshesDeathWindow()
        {
            Chain untreated = OpenChain(AcuteLethal());
            Chain treated = OpenChain(AcuteLethal());

            // 处置在 τ = 3000:potency 为负(与 Base ≥ 0 异号)、半衰期足够长
            const long treatAt = 3000;
            AppendCausalDrug(treated, treatAt, new Fix(-10 * Fix.OneRaw), halfLife: 100000);

            long probe = StateMachine.TREATMENT_WINDOW + 10;   // 3610
            untreated.DriveTo(probe);
            treated.DriveTo(probe);

            Assert.AreEqual(InjuryState.Deceased, untreated.Outcome(),
                "无处置 ⇒ last_intervention = onset_tick ⇒ 窗口已过 ⇒ 死亡");

            Assert.AreEqual(InjuryState.Coma, treated.Outcome(),
                "τ=3000 的有效对因处置把 last_intervention 推到 3000 ⇒ 3610−3000=610 < 3600 ⇒ 仍昏迷");
        }

        /// <summary>
        /// **对症处置不刷新窗口**(铁律二 —— 伪治疗不续命)。
        /// <para>若 `IsEffectiveCausalDose` 的 `polarity = causal` 那一半被删,
        /// 本条红:对症处置会把死亡推迟,玩家可以靠「乱给药」无限续命。</para>
        /// </summary>
        [Test]
        public void test_symptomaticTreatment_doesNotRefreshWindow()
        {
            Chain c = OpenChain(AcuteLethal());

            var payload = new DrugTreatmentAppliedPayload(
                tick: 3000, treatmentId: TreatmentId, actorId: 1,
                polarity: 0,                                   // Symptomatic
                drugPotency: new Fix(-10 * Fix.OneRaw), halfLife: 100000, seq: 0,
                contraindicationBand: DrugTreatmentAppliedPayload.NoContraindication);
            c.Bag.EventSink.Append(new SimEvent(
                3000, c.Patient, -1, EventKind.DrugTreatmentApplied,
                c.Bag.Encoder.Encode(EventKind.DrugTreatmentApplied, payload)));

            c.DriveTo(StateMachine.TREATMENT_WINDOW + 10);

            Assert.AreEqual(InjuryState.Deceased, c.Outcome(),
                "对症处置**不得**刷新 last_intervention ⇒ 仍按 onset_tick 计时 ⇒ 死亡");
        }

        /// <summary>
        /// **正 potency 的处置也不算有效对因**(「与 Base 异号」那一半)。
        /// <para>若该判据被删,一条把病情推高的处置反而会续命 —— 语义反向。</para>
        /// </summary>
        [Test]
        public void test_positivePotencyTreatment_doesNotRefreshWindow()
        {
            Chain c = OpenChain(AcuteLethal());

            // causal 极性,但 potency 为正(与 Base ≥ 0 同号 ⇒ 不是「治疗」)
            AppendCausalDrug(c, 3000, new Fix(10 * Fix.OneRaw), halfLife: 100000);

            c.DriveTo(StateMachine.TREATMENT_WINDOW + 10);

            Assert.AreEqual(InjuryState.Deceased, c.Outcome(),
                "potency 与 Base 同号 ⇒ 不是有效对因处置 ⇒ 不刷新窗口");
        }

        // ══════════════════ ④b 照护通道(2026-10-22 第五轮接真) ══════════════════

        /// <summary>
        /// **照护通道真的接进去了**:一条护理动作把死亡时刻推后。
        /// <para>判据构造:两链同时驱动到 `TREATMENT_WINDOW + 10` —— 无护理者已死;
        /// 有护理者(动作在 τ = 3000,无「止」)的照护支 =
        /// `max(null, 3000 + CARE_GAP=600) = 3600`,
        /// 故 `t − last_intervention = 3610 − 3600 = 10 ≤ 3600` ⇒ **仍昏迷**。</para>
        /// <para>若接线不传照护通道(恒 <c>None</c>),有护理者也会死 ⇒ 本条红。</para>
        /// <para>⚠️ 病种取 <c>typhoid</c>(适用集内)—— 适用集外的病种见下一条。</para>
        /// </summary>
        [Test]
        public void test_care_refreshesDeathWindow()
        {
            Chain uncared = OpenChain(AcuteLethalWithKey("typhoid"));
            Chain cared = OpenChain(AcuteLethalWithKey("typhoid"));

            const long careAt = 3000;
            AppendCare(cared, careAt, phase: 0);      // 起,无「止」⇒ 派生式生效

            long probe = StateMachine.TREATMENT_WINDOW + 10;   // 3610
            uncared.DriveTo(probe);
            cared.DriveTo(probe);

            Assert.AreEqual(InjuryState.Deceased, uncared.Outcome(),
                "无护理 ⇒ last_intervention = onset_tick ⇒ 窗口已过 ⇒ 死亡");

            Assert.AreEqual(InjuryState.Coma, cared.Outcome(),
                "τ=3000 的护理动作 ⇒ 照护支 = 3000+600 = 3600 ⇒ 3610−3600 = 10 < 3600 ⇒ 仍昏迷");
        }

        /// <summary>
        /// **适用集外的病种,护理不刷新窗口** —— 破伤风(K8)。
        /// <para>同样 fixture、同样护理动作,唯一差别是 <c>DiseaseKey = "tetanus"</c>。
        /// 若接线漏掉 <see cref="CareLeverApplicability.AppliesTo"/> 这道门,
        /// 破伤风会被护理无限续命,「发病前清创预防」的手柄失去意义 ⇒ 本条红。</para>
        /// <para>⚠️ 这条与上一条构成**双向判据**:只测「伤寒不死」会漏掉「破伤风也不死」的
        /// 漏门实现(两者同效);只测「破伤风死」会漏掉「谁都不死」的恒 None 实现。
        /// 两条同时成立才排除这两种错法。</para>
        /// </summary>
        [Test]
        public void test_care_nonApplicableDisease_doesNotRefreshWindow()
        {
            Chain c = OpenChain(AcuteLethalWithKey("tetanus"));

            AppendCare(c, 3000, phase: 0);
            c.DriveTo(StateMachine.TREATMENT_WINDOW + 10);

            Assert.AreEqual(InjuryState.Deceased, c.Outcome(),
                "破伤风不在照护适用集(K8)⇒ 护理不刷新窗口 ⇒ 越阈 + 过窗口即死");
        }

        /// <summary>
        /// **最后动作是「止」⇒ 立刻起算,不再送 CARE_GAP**(2026-10-22 第六轮裁定后)。
        ///
        /// <para><b>🔁 本条前身 <c>test_careStop_isRedundantUnderDerivedFormula</c> 已作废</b>
        /// (原文不删):它钉的是旧 <c>max(最后止, 最后动作 + CARE_GAP)</c> 式下
        /// 「『止』项恒冗余」这一**事实**。该事实本身没错 —— 它正是冲突的证据 ——
        /// 但裁定已把该式作废,故断言方向反转。</para>
        ///
        /// <para><b>现算式</b>:最后动作是止 ⇒ <c>last_care = 止 tick</c>;
        /// 否则(起 / 走开)⇒ <c>最后动作 + CARE_GAP</c>。判据落在已有两字段上:
        /// 「最后动作是止」⟺ <c>LastCareStopTick == LastCareActionTick</c>。</para>
        ///
        /// <para><b>为什么必须早于无止情形</b>:裁定前那条 <c>max</c> 式让两值**恒等**
        /// ⇒ 玩家点「止」在数值上零作用。本条用 <c>Greater</c> 把这条差异钉住 ——
        /// 若有人把实现改回 <c>max</c>,它当场红。</para>
        ///
        /// <para><b>对实现的影响</b>:<see cref="DiseaseCourse.NoteCareAction"/> 仍**照记**两个字段
        /// (字段级判据见下一条)—— 裁定后两个字段都真的被 F4 消费了,
        /// 而不再是一个有用一个恒冗余。</para>
        /// </summary>
        [Test]
        public void test_careStop_endsWindowImmediately()
        {
            // 2026-10-22 第六轮裁定:最后动作是「止」⇒ 立刻起算,不再送 CARE_GAP。
            long withStop = LastInterventionAggregator.Evaluate(
                onsetTick: 0,
                LastInterventionAggregator.TreatmentChannelInput.None,
                new LastInterventionAggregator.CareChannelInput(
                    lastCareStopTick: 6500, lastCareActionTick: 6500));

            long withoutStop = LastInterventionAggregator.Evaluate(
                onsetTick: 0,
                LastInterventionAggregator.TreatmentChannelInput.None,
                new LastInterventionAggregator.CareChannelInput(
                    lastCareStopTick: null, lastCareActionTick: 6500));

            Assert.AreEqual(6500L, withStop,
                "最后动作是「止」⇒ last_care = 止本身 6500,不得再送 CARE_GAP(送了 = 玩家点了停还在续命 30 秒)");
            Assert.AreEqual(6500 + StateMachine.CARE_GAP, withoutStop,
                "只有「起」后走开(无止)⇒ 宽限 = 最后动作 + CARE_GAP");
            Assert.Greater(withoutStop, withStop,
                "有「止」必须**早于**无止情形结束 —— 这是裁定前那条 max 式做不到的(它让两值恒等)");
        }

        /// <summary>
        /// **停后再起:吃新动作,不吃旧止** —— 裁定前两案在这里各错一次。
        ///
        /// <para>夹具:起 1000 → 止 2000 → 起 3000(第二次护理正在进行)。</para>
        /// <list type="bullet">
        /// <item>旧 max 式:`max(2000, 3600) = 3600` ✓(这一格它是对的)</item>
        /// <item>散文**字面**读「『止』存在则从止起计时」:`2000` ✗ —— 玩家正在护理却从 2000 起算</item>
        /// <item>本裁定:最后动作 = 起 3000 ⇒ `3000 + 600 = 3600` ✓</item>
        /// </list>
        /// </summary>
        [Test]
        public void test_careRestartAfterStop_usesNewActionNotOldStop()
        {
            long restart = LastInterventionAggregator.Evaluate(
                onsetTick: 0,
                LastInterventionAggregator.TreatmentChannelInput.None,
                new LastInterventionAggregator.CareChannelInput(
                    lastCareStopTick: 2000, lastCareActionTick: 3000));

            Assert.AreEqual(3000L + StateMachine.CARE_GAP, restart,
                "最后动作是「起」(3000),不是「止」(2000)⇒ 从 3000 宽限,不得回退到旧止 2000");
        }

        /// <summary>
        /// **两个字段各自独立记录** —— 判据落在字段值上,不从九态反推。
        ///
        /// <para><b>为什么判据落在字段而不是九态上</b>:上一条已证「止」在派生式下恒冗余
        /// ⇒ 从九态**不可能**观测到 <c>LastCareStopTick</c> 是否被写。要钉「phase 被读了」
        /// 只能直接读字段。</para>
        ///
        /// <para><b>🔁 上段理由在 2026-10-22 第六轮裁定后已部分失效(原文不删)</b>:
        /// 裁定作废了 <c>max</c> 式 ⇒ 「止」现在**真的**影响 <c>last_intervention</c>
        /// ⇒ 九态**原则上**可观测到它了。但**本条仍用字段级判据**,理由换成更弱也更稳的一条:
        /// 九态是**多输入折叠后的单值**(阈值 + 窗口 + 三通道),从一个态反推「某一个字段写没写」
        /// 需要固定其余全部输入且恰好在边界上 —— 那是**脆弱的**判据(第四轮 N6 的假存活即此类)。
        /// 字段级判据直接、单义、不依赖别的量。⇒ **结论不变,理由更新**。</para>
        ///
        /// <para><b>⚠️ 夹具的关键 = 「止」之后还有一次「起」</b>(第二次护理)。
        /// 若只造「起 3000 → 止 6500」,两种读法**同效**:</para>
        /// <list type="bullet">
        /// <item>正确(区分 phase):<c>StopTick = 6500</c>,<c>ActionTick = 6500</c>;</item>
        /// <item>忽略 phase(止也当普通动作):<c>StopTick = max(3000, 6500) = 6500</c> —— **一样**。</item>
        /// </list>
        /// <para>⇒ 那种夹具对「忽略 phase」**假存活**(本轮实测确认)。必须让止**早于**某个起:</para>
        /// <list type="bullet">
        /// <item>正确:<c>StopTick = 3000</c>(止只记止),<c>ActionTick = 5000</c>(起更晚);</item>
        /// <item>忽略 phase:<c>StopTick = max(3000, 5000) = 5000</c> ⇒ **分叉**。</item>
        /// </list>
        /// </summary>
        [Test]
        public void test_careStop_recordedSeparatelyFromAction()
        {
            var book = new DiseaseCourseBook();
            var patient = new PatientId(11);
            book.ApplyOnset(new DiseaseOnsetPayload(
                onsetTick: 0, diseaseId: 60, patientId: 11, patientSeed: 123, seq: 0), patient);

            book.ApplyCare(patient, tick: 3000, phase: DiseaseCourse.CarePhaseStart);  // 第一次护理:起
            book.ApplyCare(patient, tick: 4000, phase: DiseaseCourse.CarePhaseStop);   // 第一次护理:止
            book.ApplyCare(patient, tick: 5000, phase: DiseaseCourse.CarePhaseStart);  // 第二次护理:起

            DiseaseCourse course = book.Get(patient);

            Assert.AreEqual(4000L, course.LastCareStopTick,
                "LastCareStopTick 只由 phase=1 写 ⇒ 仍是第一次的止 4000," +
                "不得被后到的 5000「起」顶上去(顶上去 = 忽略 phase)");
            Assert.AreEqual(5000L, course.LastCareActionTick,
                "LastCareActionTick 记录任意 phase 的最后一条 ⇒ 5000");
        }

        /// <summary>
        /// **「取大而非覆盖」** —— 乱序重放(AC-16)下直接赋值会让「最后一条」
        /// 退化成「**最后到达**的一条」。
        ///
        /// <para>本测试**直接调档案簿**(不经 <c>EventStream</c> —— 流本身有 BCD-码-3 的
        /// tick 非降断言,构造不出乱序入流):先送 6500 的止、再送 3000 的起,
        /// 两个字段都必须**留在 6500**。若改成直接赋值,第二个调用会把两字段都写成 3000。</para>
        /// </summary>
        [Test]
        public void test_careTimes_takeMax_notOverwrite()
        {
            var book = new DiseaseCourseBook();
            var patient = new PatientId(12);
            book.ApplyOnset(new DiseaseOnsetPayload(
                onsetTick: 0, diseaseId: 60, patientId: 12, patientSeed: 123, seq: 0), patient);

            book.ApplyCare(patient, tick: 6500, phase: DiseaseCourse.CarePhaseStop);  // 止(先到)
            book.ApplyCare(patient, tick: 3000, phase: DiseaseCourse.CarePhaseStart); // 起(后到 —— 乱序)

            DiseaseCourse course = book.Get(patient);

            Assert.AreEqual(6500L, course.LastCareStopTick,
                "后到的 3000 起不得把 6500 的止降回去");
            Assert.AreEqual(6500L, course.LastCareActionTick,
                "LastCareActionTick 取大 ⇒ 仍是 6500");
        }

        /// <summary>
        /// **越界 phase 当场抛,不静默当「起」**。
        ///
        /// <para><b>为什么这条必要</b>:越界值若被静默取值(例如当成「起」),会让
        /// <c>LastCareStopTick</c> 永远为空 ⇒ 照护支退化成
        /// <c>最后动作 + CARE_GAP</c> 单项式,而**没有任何测试会红**。
        /// 序数本身现已三处齐备(<c>entities.yaml:2186</c> / <c>Sim.Contracts.CarePhase</c> /
        /// <c>DiseaseCourse</c> 转发常量),但**序数有权威 ≠ 调用方不会传越界值** ——
        /// 写者(24)仍可能传三值枚举或 <c>1 = 起 / 0 = 止</c> 的旧编码,故值域门照旧必要。</para>
        ///
        /// <para>🔁 原文写「<c>entities.yaml</c> 的 <c>CareApplied</c> 条目只写『phase: 枚举(起 止)』,
        /// **全库没有序数定义** —— <c>0 = 起 / 1 = 止</c> 是本实现的落地约定」——
        /// **已失效**(原文不删):2026-10-22 用户裁定「甲+乙 都做」后 registry 已补序数。</para>
        ///
        /// <para>⇒ 越界值必须 fail-loud。本测试送 <c>2</c> 与 <c>-1</c> 两种。</para>
        /// </summary>
        [Test]
        public void test_carePhase_outOfRange_throws()
        {
            var book = new DiseaseCourseBook();
            var patient = new PatientId(12);
            book.ApplyOnset(new DiseaseOnsetPayload(
                onsetTick: 0, diseaseId: 60, patientId: 12, patientSeed: 1, seq: 0), patient);

            Assert.Throws<InvalidOperationException>(
                () => book.ApplyCare(patient, tick: 100, phase: 2),
                "phase = 2 越界 ⇒ 必须抛(否则静默当成「起」)");

            Assert.Throws<InvalidOperationException>(
                () => book.ApplyCare(patient, tick: 100, phase: -1),
                "phase = -1 越界 ⇒ 必须抛");
        }

        // ══════════════════ ⑤ 痊愈支/迁延支未求值(W/TREND_EPS 未裁定) ══════════════════

        /// <summary>
        /// **W 未裁定 ⇒ 接线不得代拍**:自限型病人曲线衰减到低位后,
        /// 九态**仍是阈值分级**(Severe/Moderate/Mild),**不是** <c>Recovered</c>。
        /// <para>这是「结构先落、数值作入参」那条裁定的接线侧判据 ——
        /// 若接线擅自传一个 W 值(哪怕 1),本条会转 <c>Recovered</c> 而红。</para>
        /// </summary>
        [Test]
        public void test_selfLimitedLowPosition_doesNotReportRecovered()
        {
            DiseaseRegistryEntry entry = AcuteLethal(aPeak: 20, scale: 100);
            entry.Curve.SelfLimit = true;      // 自限型:τ ≥ τ_peak 后指数衰减
            entry.Curve.TauFall = new Fix(20 * Fix.OneRaw);   // 快速衰减到低位

            Chain c = OpenChain(entry);
            c.DriveTo(1000);                   // 远过 τ_peak,position 已衰减到 ~0

            InjuryState state = c.Outcome();
            Assert.AreNotEqual(InjuryState.Recovered, state,
                "W / TREND_EPS 未裁定 ⇒ 接线传 default(RecoveryWindowInput) ⇒ 痊愈支不求值");
            Assert.AreNotEqual(InjuryState.Chronic, state,
                "同上:迁延支亦不求值");
        }

        // ══════════════════ ⑥ 病人侧不吃玩家的战伤破例 ══════════════════

        /// <summary>
        /// 接线传的实体类别是 <see cref="ActorClass.Patient"/> —— 不是 <c>Player</c>。
        ///
        /// <para><b>⚠️ 靶子的构造是本条的全部难点(第一版打错了,已订正)</b>:
        /// 第一版用「白名单伤情 + 急性保持型(`self_limit = false`)」——
        /// 但那样两种类别**都给 `SelfLimited = false`**:
        /// <c>Patient</c> 走注册表原值(<c>false</c>),<c>Player</c> 走战伤破例(<c>false</c>)
        /// ⇒ 二者同效 ⇒ 变异 <c>Patient → Player</c> **假存活**(实测 N6:`4584/4548/0`,零红)。</para>
        ///
        /// <para><b>现行构造 = 让两条路径真的分叉</b>:注册表 <c>self_limit = true</c> ⇒</para>
        /// <list type="bullet">
        /// <item><c>Patient</c> ⇒ 取注册表原值 <c>true</c> ⇒ **死亡支关闭** ⇒ 不死(昏迷);</item>
        /// <item><c>Player</c> ⇒ 战伤破例把 <c>true</c> 翻成 <c>false</c> ⇒ **死亡支开启** ⇒ 死。</item>
        /// </list>
        /// <para>⇒ 判据 = **不死**。若接线误传 <c>Player</c>,本条转 <c>Deceased</c> 而红。</para>
        ///
        /// <para><b>为什么要 <c>TauFall</c> 很大</b>:<c>self_limit = true</c> 会让曲线在
        /// <c>τ ≥ τ_peak</c> 后指数衰减。取 <c>τ_fall = 100000</c> tick ⇒ 到
        /// <c>τ = 3610</c> 时衰减 ≈ <c>e^(−0.035) ≈ 0.966</c>,<c>position ≈ 0.676 ≥ DEATH(0.58)</c>
        /// ⇒ 死亡支的**阈值条件仍成立**,分叉只发生在「门」那一层。
        /// 若 <c>τ_fall</c> 取小值,position 会先掉到阈值下 ⇒ 两种类别都不死 ⇒ **又是一次假存活**。</para>
        /// </summary>
        [Test]
        public void test_wiringUsesPatientClass_notPlayer()
        {
            DiseaseRegistryEntry entry = AcuteLethal();
            entry.Kind = DiseaseKind.Injury;
            entry.DiseaseKey = "INJ_STAB";          // 战伤白名单内
            entry.Curve.SelfLimit = true;           // ⇒ 与 Player 的破例分叉的关键
            entry.Curve.TauFall = new Fix(100000 * Fix.OneRaw);   // 衰减极慢,position 仍在死线之上

            Chain c = OpenChain(entry);
            c.DriveTo(StateMachine.TREATMENT_WINDOW + 10);

            Assert.AreNotEqual(InjuryState.Deceased, c.Outcome(),
                "病人不吃玩家的战伤破例 ⇒ SelfLimited 取注册表原值 true ⇒ 死亡支关闭 ⇒ 不得判死;" +
                "若本条返回 Deceased,说明接线误传了 ActorClass.Player(破例把 true 翻成了 false)");
        }
    }
}
