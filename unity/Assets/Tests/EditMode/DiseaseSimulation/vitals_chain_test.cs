// M2 接线轮阶段 2 · 批次 C(体征链核心)—— 三断点的端到端可证伪链(EditMode,禁 Fake)。
//
// 覆盖(阶段 0 勘察认定的三断点 + 主循环):
//   ① apply 层    —— `DiseaseOnset` 经**真** `PatientSpawner` + `IPayloadEncoder` 入流,
//                     `DiseaseVitalsService.OnTickEdge` 解码并建档(此前病人状态无事件驱动);
//   ② 投影桥      —— `ProgressionResult → VitalsDto`(此前 `new VitalsDto` 只在 Tests);
//   ③ 查询面      —— `IVitalsQuery.GetVitals` **生产**实装(此前仅测试 Fake);
//   ④ 主循环接线与次序 —— 见 `Boot/disease_tick_pump_test.cs`(本文件直驱 Step 边沿)。
//
// 真装配:`CompositionRoot.Assemble(registry)` —— 真 `EventStream`(Seq 发号 / 去重 / AC-15)、
//   真 `PayloadEncoder` + `InMemoryBlobPool`、真 `PatientSpawner`。**零 FakeVitalsQuery /
//   零 FakeEventSink 自洽环**(既有 `vertical_slice_test` 的 Fake 断言本批不动,归批次 F 复评)。
//
// 确定性:零随机、零墙钟 —— tick 由本文件按序显式驱动;期望值在 Fix 域内用
//   `Fix.Exp` / `Fix` 四则独立算出,最后一步才转 float 比较(与生产投影同一出口)。
//
// 权威:design/gdd/disease-simulation.md F1(三分支 / R_rise / Relapse / Decay)·
//   F2(position = clamp(Progress/SCALE,0,1))· AC-26(潜伏抑制)· AC-6(连续)·
//   AC-8(max 非和)· AC-13(对症不改 Progress)· AC-27(Δ<0 归零)·
//   ADR-030(DiseaseOnset)· ADR-009 Amendment I(急救三 Kind)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.DiseaseSimulation;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.DiseaseSimulation
{
    /// <summary><see cref="DiseaseVitalsService"/> 体征链端到端测试。</summary>
    public class VitalsChainTest
    {
        /// <summary>float 比较容差(生产投影是 Q16.16 → binary32,量级 1 的值 eps ≈ 6e-8)。
        /// 取 2×10⁻⁴:足以吞掉两次 ToFloat 的舍入,远窄于任何公式性错误
        /// (漏 R_rise 归一化偏 ~0.3 · 半衰期差一倍偏 ~0.05 · max 误作和偏 ~0.18)。</summary>
        private const float Tol = 2e-4f;

        /// <summary>对症极性序数(载荷 `polarity`;相对
        /// <see cref="ProgressionEvaluator.PolarityCausal"/> = 1)。</summary>
        private const int PolaritySymptomatic = 0;

        /// <summary>任意处置 id(`<c>TreatableBy</c> == null` ⇒ 离牌门结构性缺位不拦)。</summary>
        private const int TreatmentId = 7;

        // ────────────────────────────────────────────────────────────────
        // 驱动脚手架(真装配,非 Fake)
        // ────────────────────────────────────────────────────────────────

        /// <summary>一条真装配的体征链(每个测试自建,互不共享可变状态)。</summary>
        private sealed class Chain
        {
            public CompositionRootServices Bag;
            public DiseaseVitalsService Svc;
            public PatientId Patient;
            public long Tick = -1;

            /// <summary>按序驱动到目标 tick(单调;与生产 PumpFrame 的逐边沿语义一致)。</summary>
            public void DriveTo(long target)
            {
                while (Tick < target)
                {
                    Tick++;
                    Svc.OnTickEdge(Tick);
                }
            }

            public float Position() => Svc.GetVitals(Patient).Position;
        }

        /// <summary>真装配 + 建档(onset tick = 0);尚未走过任何 tick 边沿。</summary>
        private static Chain OpenChain(DiseaseRegistryEntry entry)
        {
            var bag = CompositionRoot.Assemble(new[] { entry });
            var chain = new Chain { Bag = bag, Svc = bag.VitalsService };
            chain.Patient = bag.PatientSpawner.SpawnNext(entry.DiseaseId, tick: 0);
            return chain;
        }

        /// <summary>追加一条药疗处置(载荷经**真** `IPayloadEncoder` 入池、事件进真 `EventStream`)。</summary>
        private static void AppendDrug(Chain c, long tick, int polarity, Fix potency, long halfLife)
        {
            var payload = new DrugTreatmentAppliedPayload(
                tick: tick, treatmentId: TreatmentId, actorId: 1,
                polarity: polarity, drugPotency: potency, halfLife: halfLife, seq: 0);
            c.Bag.EventSink.Append(new SimEvent(
                tick, c.Patient, -1, EventKind.DrugTreatmentApplied,
                c.Bag.Encoder.Encode(EventKind.DrugTreatmentApplied, payload)));
        }

        /// <summary>追加一条急救处置(10 写;与 11 支同构地进同一条 F1 和式)。</summary>
        private static void AppendEmergencyDrug(Chain c, long tick, int polarity, Fix potency, long halfLife)
        {
            var payload = new EmergencyTreatmentAppliedPayload(
                tick: tick, treatmentId: TreatmentId, actorId: 1, polarity: polarity,
                drugPotency: potency, halfLife: halfLife, method: 0, cause: 0, seq: 0);
            c.Bag.EventSink.Append(new SimEvent(
                tick, c.Patient, -1, EventKind.EmergencyTreatmentApplied,
                c.Bag.Encoder.Encode(EventKind.EmergencyTreatmentApplied, payload)));
        }

        /// <summary>追加一条急救判定输入(10 产出 / 主机物化;只登记,不改病程)。</summary>
        private static void AppendAttempt(Chain c, long tick, int action)
        {
            var payload = new EmergencyAttemptPayload(
                action: action, holdTicks: 12, edges: 0, magPeak: 100, magLast: 80,
                method: 0, actorId: 1, edgeTicks: Array.Empty<int>());
            c.Bag.EventSink.Append(new SimEvent(
                tick, c.Patient, -1, EventKind.EmergencyAttempt,
                c.Bag.Encoder.Encode(EventKind.EmergencyAttempt, payload)));
        }

        // ────────────────────────────────────────────────────────────────
        // ① + ② + ③:DiseaseOnset → Step → GetVitals(非默认 + 随 F1 曲线可断言)
        // ────────────────────────────────────────────────────────────────

        [Test]
        public void test_vitalsChain_incubationThenRise_positionZeroThenMonotoneRise()
        {
            // Arrange:合成 fixture(批次 B),onset tick = 0
            DiseaseRegistryEntry entry = SyntheticDiseaseFixture.Create();
            Chain c = OpenChain(entry);

            // Act ①:潜伏段(τ < incubation = 600)—— 驱动到潜伏末 tick
            c.DriveTo(SyntheticDiseaseFixture.IncubationTicks - 1);
            float beforeIncubation = c.Position();

            // Act ②:上升段 [incubation, τ_peak] —— 每 100 tick 采样
            var samples = new List<float>();
            for (long t = SyntheticDiseaseFixture.IncubationTicks;
                 t <= SyntheticDiseaseFixture.TauPeakTicks; t += 100)
            {
                c.DriveTo(t);
                samples.Add(c.Position());
            }

            // Assert ①:潜伏期恒 0(AC-26:噪声不得使潜伏病人有读数;Noise 现亦为 tripwire 恒 0)
            Assert.AreEqual(0f, beforeIncubation,
                $"τ = {SyntheticDiseaseFixture.IncubationTicks - 1} < incubation ⇒ position 必须恰为 0");

            // Assert ②:上升段单调不降 · 有正读数 · 终点相接 A_peak
            for (int i = 1; i < samples.Count; i++)
                Assert.GreaterOrEqual(samples[i], samples[i - 1],
                    $"上升段须单调不降(τ = {SyntheticDiseaseFixture.IncubationTicks + i * 100})");
            Assert.Greater(samples[1], 0f, "τ > incubation 后必须出现正读数(否则曲线没接真)");
            Assert.AreEqual((entry.Curve.APeak / entry.Scale).ToFloat(),
                samples[samples.Count - 1], Tol,
                "τ = τ_peak 处 Base = A_peak(R_rise 归一化)⇒ position = A_peak / SCALE");

            // Assert ③:投影界(F2)
            foreach (float p in samples)
            {
                Assert.GreaterOrEqual(p, 0f, "position ≥ 0");
                Assert.LessOrEqual(p, 1f, "position ≤ 1");
            }
        }

        [Test]
        public void test_vitalsChain_selfLimitFall_positionMatchesFixExpAnchor()
        {
            // Arrange:关掉复发块 —— 否则 τ = τ_peak + τ_fall 恰逢首击(A_rel = 1/4 > Base),
            //          max(⊔) 会盖掉衰减段,锚点不可判。
            DiseaseRegistryEntry entry = SyntheticDiseaseFixture.Create();
            entry.Relapse = null;
            Chain c = OpenChain(entry);

            var curve = entry.Curve;
            long fallTicks = curve.TauFall.Round();
            long[] taus =
            {
                0L, curve.TauPeak, curve.TauPeak + fallTicks / 2,
                curve.TauPeak + fallTicks, curve.TauPeak + 2 * fallTicks
            };

            // Act:逐段驱动并采样
            var samples = new Dictionary<long, float>();
            foreach (long tau in taus)
            {
                c.DriveTo(tau);
                samples[tau] = c.Position();
            }

            // Assert ①:τ = τ_peak 起点 = A_peak / SCALE
            Assert.AreEqual((curve.APeak / entry.Scale).ToFloat(),
                samples[curve.TauPeak], Tol,
                "τ = τ_peak 处衰减段起点须 = A_peak(Exp(0) = 1)");

            // Assert ②:τ_peak + τ_fall ⇒ A_peak · e^(−1) / SCALE —— 半衰形状经 Fix.Exp
            Fix expectedOne = curve.APeak * Fix.Exp(Fix.Zero - Fix.One) / entry.Scale;
            Assert.AreEqual(expectedOne.ToFloat(),
                samples[curve.TauPeak + fallTicks], Tol,
                "τ_peak + τ_fall 处须 ≈ A_peak·e^(−1)(衰减段指数走 Fix.Exp,不是线性折半)");

            // Assert ③:τ_peak + 2τ_fall ⇒ A_peak · e^(−2) / SCALE
            Fix expectedTwo = curve.APeak * Fix.Exp(Fix.Zero - (Fix.One + Fix.One)) / entry.Scale;
            Assert.AreEqual(expectedTwo.ToFloat(),
                samples[curve.TauPeak + 2 * fallTicks], Tol,
                "τ_peak + 2τ_fall 处须 ≈ A_peak·e^(−2)");

            // Assert ④:衰减段(自 τ_peak 起)单调不增 —— τ = 0 属潜伏段,不进本判据
            float prev = samples[curve.TauPeak];
            for (int i = 2; i < taus.Length; i++)
            {
                long tau = taus[i];
                Assert.LessOrEqual(samples[tau], prev + Tol, $"衰减段须单调不增(τ = {tau})");
                prev = samples[tau];
            }
        }

        [Test]
        public void test_vitalsChain_relapseFirstStrike_positionIsMaxNotSum()
        {
            // Arrange:原 fixture(带复发块)—— 首击 τ = 2400 处 A_rel = 1/4 > Base = A_peak·e^(−1)
            DiseaseRegistryEntry entry = SyntheticDiseaseFixture.Create();
            Chain c = OpenChain(entry);
            var r = entry.Relapse;
            var curve = entry.Curve;

            // Act:驱动到首击点
            c.DriveTo(r.Interval);
            float atStrike = c.Position();

            // Assert ①:复发臂自身击发(τ = τ_rel ⇒ Exp(0) = 1 ⇒ 精确 = A_rel / SCALE)
            Assert.AreEqual((r.ARel / entry.Scale).ToFloat(), atStrike, Tol,
                "τ = relapse_interval 处复发击发 ⇒ position = A_rel / SCALE");

            // Assert ②:⊔ = 取大不是相加(AC-8)——
            //   Base(2400) = A_peak · e^(−(2400−1800)/600) = A_peak·e^(−1);τ_fall = 600 ⇒ 比值恰 1
            Fix baseAtStrike = curve.APeak * Fix.Exp(Fix.Zero - Fix.One);
            Assert.Greater(baseAtStrike.Raw, 0L, "sanity:Base 在该点仍为正(max 臂非退化)");
            float sumWouldBe = ((baseAtStrike + r.ARel) / entry.Scale).ToFloat();
            Assert.Less(atStrike, sumWouldBe - Tol,
                "⊔ 必须是取大而非相加(AC-8:括号是运算优先级 —— max(Base ⊔ Relapse) 而非 Base + Relapse)");
        }

        // ────────────────────────────────────────────────────────────────
        // DrugContribution 接真(Decay 经 Fix.Exp;半衰期锚点)
        // ────────────────────────────────────────────────────────────────

        [Test]
        public void test_vitalsChain_causalDrugDecay_positionShiftMatchesHalfLifeAnchor()
        {
            // Arrange 两条平行链(同一 fixture 数值,互不共享状态):
            //   A = 无处置基线;B = τ=1000 落一剂对因处置(potency = −1/4,half_life = 400)
            Fix potency = FixParse.Parse("-1/4");
            Fix scale = SyntheticDiseaseFixture.Create().Scale;
            long halfLife = 400;
            long doseTick = 1000;

            Chain a = OpenChain(SyntheticDiseaseFixture.Create());
            Chain b = OpenChain(SyntheticDiseaseFixture.Create());
            a.DriveTo(doseTick - 1);
            b.DriveTo(doseTick - 1);
            AppendDrug(b, doseTick, ProgressionEvaluator.PolarityCausal, potency, halfLife);

            // Δ = 0(处置当刻)⇒ Decay = 1 ⇒ 差 = drug_potency / SCALE
            a.DriveTo(doseTick);
            b.DriveTo(doseTick);
            Assert.AreEqual((potency / scale).ToFloat(), b.Position() - a.Position(), Tol,
                "Δ = 0 ⇒ Decay(0) = 1 ⇒ position 差 = drug_potency / SCALE");

            // Δ = half_life ⇒ e^(−1)
            a.DriveTo(doseTick + halfLife);
            b.DriveTo(doseTick + halfLife);
            Fix expectedOne = potency * Fix.Exp(Fix.Zero - Fix.One) / scale;
            Assert.AreEqual(expectedOne.ToFloat(), b.Position() - a.Position(), Tol,
                "Δ = half_life ⇒ Decay = e^(−1)(半衰期锚点,经 Fix.Exp)");

            // Δ = 2 × half_life ⇒ e^(−2)
            a.DriveTo(doseTick + 2 * halfLife);
            b.DriveTo(doseTick + 2 * halfLife);
            Fix expectedTwo = potency * Fix.Exp(Fix.Zero - (Fix.One + Fix.One)) / scale;
            Assert.AreEqual(expectedTwo.ToFloat(), b.Position() - a.Position(), Tol,
                "Δ = 2×half_life ⇒ Decay = e^(−2)");
        }

        [Test]
        public void test_vitalsChain_symptomaticPolarity_positionUnchanged()
        {
            // Arrange:对症处置(AC-13:只改 Signs,不改 Progress)
            Fix potency = FixParse.Parse("-1/4");
            Chain a = OpenChain(SyntheticDiseaseFixture.Create());
            Chain s = OpenChain(SyntheticDiseaseFixture.Create());
            a.DriveTo(999);
            s.DriveTo(999);
            AppendDrug(s, 1000, PolaritySymptomatic, potency, halfLife: 400);

            // Act + Assert:同 tick 序驱动,逐点比
            for (long t = 1000; t <= 1800; t += 100)
            {
                a.DriveTo(t);
                s.DriveTo(t);
                Assert.AreEqual(a.Position(), s.Position(),
                    $"对症处置不得改 Progress(τ = {t};AC-13 / F1 polarity 门)");
            }
        }

        [Test]
        public void test_vitalsChain_emergencyTreatment_contributesAndAttemptRegistered()
        {
            // Arrange:10 侧两支 —— EmergencyTreatmentApplied(结算,进和式)+
            //                        EmergencyAttempt(判定输入,只登记)
            Fix potency = FixParse.Parse("-1/4");
            Fix scale = SyntheticDiseaseFixture.Create().Scale;
            long halfLife = 400;
            long doseTick = 1000;

            Chain a = OpenChain(SyntheticDiseaseFixture.Create());
            Chain e = OpenChain(SyntheticDiseaseFixture.Create());
            a.DriveTo(doseTick - 1);
            e.DriveTo(doseTick - 1);
            AppendEmergencyDrug(e, doseTick, ProgressionEvaluator.PolarityCausal, potency, halfLife);
            AppendAttempt(e, doseTick, action: 3);

            a.DriveTo(doseTick + halfLife);
            e.DriveTo(doseTick + halfLife);

            // Assert ①:急救结算与药疗同构地进 F1 和式(批次 E 已给它病人归因)
            Fix expected = potency * Fix.Exp(Fix.Zero - Fix.One) / scale;
            Assert.AreEqual(expected.ToFloat(), e.Position() - a.Position(), Tol,
                "EmergencyTreatmentApplied 的 Decay 同样经 Fix.Exp(半衰期锚点)");

            // Assert ②:判定输入被登记(只计数,不改病程)
            Assert.AreEqual(1, e.Svc.Courses.Get(e.Patient).EmergencyAttemptCount,
                "EmergencyAttempt 须在 apply 层登记(三断点①的判定输入支)");
            Assert.AreEqual(3, e.Svc.Courses.Get(e.Patient).LastEmergencyActionId,
                "最近一次判定输入的 action id 须可读");
        }

        [Test]
        public void test_vitalsChain_doseNotVisibleBeforeItsTick()
        {
            // 处置事件的 Tick 晚于当前边沿 ⇒ 游标不推进它(cursor 只应用 `Tick ≤ tick`);
            // 等它自己的边沿到达 ⇒ 立刻生效(入流序 + tick 门,两段都可判)。
            Fix potency = FixParse.Parse("-1/4");
            Chain a = OpenChain(SyntheticDiseaseFixture.Create());
            Chain f = OpenChain(SyntheticDiseaseFixture.Create());
            a.DriveTo(1000);
            f.DriveTo(1000);
            AppendDrug(f, 2000, ProgressionEvaluator.PolarityCausal, potency, halfLife: 400);

            a.DriveTo(1500);
            f.DriveTo(1500);
            Assert.AreEqual(a.Position(), f.Position(),
                "Tick = 2000 的处置在 1500 边沿不得入和式(游标按 `Tick ≤ tick` 推进)");

            a.DriveTo(2000);
            f.DriveTo(2000);
            Assert.Less(f.Position(), a.Position(),
                "到达处置自身的 tick ⇒ 必须入和式(否则事件被永久漏读)");
        }

        /// <summary>
        /// 游标门的**独立判别面**(2026-10-10 合批评审 M13 变异实测发现):剂量面被
        /// Decay 的 `Δ &lt; 0` 归零门冗余吸收 —— 实删 `if (e.Tick &gt; tick) break` 后
        /// <c>doseNotVisibleBeforeItsTick</c> 仍绿(双层防护等价变异);唯一能红在删门上的
        /// 是「未来 <c>DiseaseOnset</c> 不得提前建档」—— 删门 ⇒ tick=2000 的 onset 在
        /// 1000 边沿被提前 apply ⇒ 未建档 fail-loud 失效。
        /// </summary>
        [Test]
        public void test_vitalsChain_futureOnset_notRegisteredBeforeItsTick()
        {
            // Arrange:第二个病人 onset 落在未来 tick(经真 SpawnNext 入流)
            DiseaseRegistryEntry entry = SyntheticDiseaseFixture.Create();
            CompositionRootServices bag = CompositionRoot.Assemble(new[] { entry });
            PatientId future = bag.PatientSpawner.SpawnNext(entry.DiseaseId, tick: 2000);
            var chain = new Chain { Bag = bag, Svc = bag.VitalsService, Patient = future, Tick = -1 };

            // Act:驱动到 onset 之前的边沿
            chain.DriveTo(1000);

            // Assert:未建档 fail-loud(游标未推进未来事件 ⇒ 病程不存在)
            Assert.Throws<PatientCourseNotFoundException>(
                () => bag.VitalsService.GetVitals(future),
                "Tick = 2000 的 DiseaseOnset 在 1000 边沿不得建档(游标按 `Tick ≤ tick` 推进)");

            // 到达 onset 自身边沿 ⇒ 建档可查询(否则事件被永久漏读)
            chain.DriveTo(2000);
            Assert.DoesNotThrow(() => bag.VitalsService.GetVitals(future),
                "到达 onset 自身 tick ⇒ 必须已建档(未读事件检测)");
        }

        // ────────────────────────────────────────────────────────────────
        // 查询面 fail-loud + 注册表门 + tick 单调
        // ────────────────────────────────────────────────────────────────

        [Test]
        public void test_vitalsChain_unknownPatient_throwsNamedException()
        {
            // Arrange:全新链,尚有任何建档
            Chain c = OpenChain(SyntheticDiseaseFixture.Create());

            // Act / Assert ①:从未建档 ⇒ 具名异常(不返回默认 VitalsDto)
            var ex1 = Assert.Throws<PatientCourseNotFoundException>(
                () => c.Svc.GetVitals(new PatientId(4242)));
            Assert.AreEqual(4242, ex1.RequestedPatient, "异常须带被请求的病人 id");

            // Act / Assert ②:DiseaseOnset 已入流但 Step 未驱动 ⇒ 同样具名异常
            Assert.Throws<PatientCourseNotFoundException>(() => c.Svc.GetVitals(c.Patient),
                "DiseaseOnset 已入流但 OnTickEdge 未驱动 ⇒ 拒绝返回默认 VitalsDto");

            // Act / Assert ③:驱动后可读(证 ①② 来自「未建档」而非实现残缺)
            c.DriveTo(0);
            Assert.DoesNotThrow(() => c.Svc.GetVitals(c.Patient), "建档并 Step 后必须可读");
        }

        [Test]
        public void test_vitalsChain_unregisteredDisease_failLoud()
        {
            // Arrange:registry 用 fixture,但 onset 声明一个不在表内的病种
            var bag = CompositionRoot.Assemble(new[] { SyntheticDiseaseFixture.Create() });
            bag.PatientSpawner.SpawnNext(SyntheticDiseaseFixture.DiseaseId + 1, tick: 0);

            // Act / Assert:Step 阶段 fail-loud(拒绝为未知病种求值)
            var ex = Assert.Throws<ArgumentException>(() => bag.VitalsService.OnTickEdge(0));
            StringAssert.Contains("不在注册表内", ex.Message);
            Assert.AreEqual("registry", ex.ParamName, "异常须点名缺失的装配依赖");
        }

        [Test]
        public void test_vitalsChain_tickRegress_failsLoud()
        {
            Chain c = OpenChain(SyntheticDiseaseFixture.Create());
            c.DriveTo(10);
            Assert.Throws<InvalidOperationException>(
                () => c.Svc.OnTickEdge(5),
                "tick 时间倒流 = 同一病人被旧时刻覆盖新求值,必须 fail-loud");
        }

        // ────────────────────────────────────────────────────────────────
        // F1 的 Σ 侧门(求值器直测 —— 链路测不到的分支)
        // ────────────────────────────────────────────────────────────────

        [Test]
        public void test_progressionEvaluator_futureDose_contributionZero()
        {
            // AC-27:Decay(Δ < 0) = 0 —— 时间反演必须归零,否则指数爆炸。
            // 链路侧(游标按 Tick ≤ tick 推进)结构上到不了这条分支 ⇒ 在求值器直测。
            DiseaseRegistryEntry entry = SyntheticDiseaseFixture.Create();
            var doses = new[]
            {
                new TreatmentDose(2000, TreatmentId, ProgressionEvaluator.PolarityCausal,
                                  FixParse.Parse("-1/2"), 400)
            };
            var none = new TreatmentDose[0];

            ProgressionResult withFuture = ProgressionEvaluator.Evaluate(
                entry, 0, 1000, doses, 0UL, new PatientId(1));
            ProgressionResult without = ProgressionEvaluator.Evaluate(
                entry, 0, 1000, none, 0UL, new PatientId(1));

            Assert.AreEqual(without.Position.Raw, withFuture.Position.Raw,
                "未来处置不得影响当前 Progress(Δ < 0 ⇒ Decay = 0)");
        }

        [Test]
        public void test_progressionEvaluator_doseClampBound_saturatesSum()
        {
            // F1 对称 clamp(四轮 K1):界只罩 Σ,不罩整式。
            // τ = 1000 处 Base ≈ 0.375;Σ = −1/4(Δ=0)⇒ 未夹 0.125 > 0(max(0,·) 不介入);
            //   界 = 1/16 ⇒ Σ 被抬到 −1/16 ⇒ Progress 抬高 (1/4 − 1/16) = 3/16。
            DiseaseRegistryEntry entry = SyntheticDiseaseFixture.Create();
            var doses = new[]
            {
                new TreatmentDose(1000, TreatmentId, ProgressionEvaluator.PolarityCausal,
                                  FixParse.Parse("-1/4"), 400)
            };
            Fix bound = FixParse.Parse("1/16");

            ProgressionResult unclamped = ProgressionEvaluator.Evaluate(
                entry, 0, 1000, doses, 0UL, new PatientId(1));
            ProgressionResult clamped = ProgressionEvaluator.Evaluate(
                entry, 0, 1000, doses, 0UL, new PatientId(1), bound);

            Assert.Greater(unclamped.Position.Raw, 0L,
                "前置:未夹结果须 > 0,否则 max(0,·) 介入、差值不再是 clamp 的纯量");

            long expectedDiff = (-doses[0].DrugPotency.Raw) - bound.Raw;  // |Σ| − bound
            Assert.AreEqual(expectedDiff, clamped.Position.Raw - unclamped.Position.Raw,
                "clamp 只作用于 Σ(四轮 K1):差 = |Σ| − bound");
        }
    }
}
