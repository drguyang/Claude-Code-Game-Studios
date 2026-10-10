// M2 阶段 2 · 批次 B —— 合成 fixture 病种数据面测试。
//
// 验收口径(2026-10-09 用户裁定①):fixture 数值 = 占位非最终,数值轮整表替换;
//    本文件的验收面 = **数据形状与管线可证伪**(形状合法 · Fix 作者态往返 ·
//    F1/F2 公式形状抽样),不是数值好玩性。
//
// 覆盖:
//   1. 形状合法 —— fixture 过 RegistrySchemaValidator 全量校验(正向);必填字段非默认。
//   2. Fix 作者态口径 —— 全部 Fix 字段经 FixParse 字符串往返逐位一致(ADR-014 §四)。
//   3. 负夹具 —— 曲线面 R1-20…30 每条至少一个「只违该条」的最小反例(判别力)。
//   4. 曲线形状抽样 —— F1 Base 潜伏/上升/衰减段 + Relapse 脉冲 + Decay(Δ) 经 Fix.Exp
//      的因子量级 + F2 position ∈ [0,1]。求值用**测试侧 oracle**(照 GDD F1 式逐字实现),
//      不触 ProgressionEvaluator(批次 C 才接真;AC-6/8/26 的求值器测试归批次 C)。
//
// 确定性:零随机、零墙钟 —— 全部纯函数求值;fixture 每次 Create() 返回全新对象。
// 权威:design/gdd/disease-simulation.md R1.3 / F1 / F2 / §Visual/Audio 二 · ADR-014 §四。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.DiseaseSimulation
{
    public class CurveFixtureTest
    {
        /// <summary>峰值连续性/界内容差(单位 = Q16.16 LSB)。Exp ε = 4×2⁻¹⁶ 相对 + 3 LSB 绝对
        /// (fix_exp_test 标定),再留舍入余量;8 raw ≈ 1.2×10⁻⁴ —— 远小于任何公式性错误
        /// (如漏 R_rise 归一化偏 ~640 raw)。</summary>
        private const long TolTight = 8;

        /// <summary>e^(−1) / e^(−2) 因子量级容差(LSB)。64 raw ≈ 10⁻³ —— 半衰期用错一倍
        /// (e^(−0.5)=0.606 vs e^(−1)=0.368)偏差 ~1.5 万 raw,必红。</summary>
        private const long TolExp = 64;

        // ────────────────────────────────────────────────────────────────
        // 一、形状合法(正向)
        // ────────────────────────────────────────────────────────────────

        [Test]
        public void test_registry_syntheticFixture_passesValidation()
        {
            // Arrange
            var entries = new List<DiseaseRegistryEntry> { SyntheticDiseaseFixture.Create() };

            // Act / Assert —— 全量 19 条既有 + 11 条曲线面(R1-20…30)校验不抛
            Assert.DoesNotThrow(() => RegistrySchemaValidator.Validate(entries),
                "合成 fixture 应通过注册表全部构建期校验(形状合法)");
        }

        [Test]
        public void test_registry_syntheticFixture_requiredFieldsNonDefault()
        {
            // Arrange
            var e = SyntheticDiseaseFixture.Create();

            // Assert —— 必填字段非 C# 默认(GDD R1.3「必填」逐条;默认 0/false/null = 漏填)
            Assert.AreNotEqual(0, e.DiseaseId, "DiseaseId 非默认");
            Assert.IsFalse(string.IsNullOrEmpty(e.DiseaseKey), "DiseaseKey 非空");

            Assert.IsNotNull(e.Curve, "Curve 块必填(R1.3 natural_progress)");
            Assert.Greater(e.Curve.Incubation, 0, "curve.incubation 非默认(必填)");
            Assert.Greater(e.Curve.APeak.Raw, 0L, "curve.a_peak 非默认(必填 > 0)");
            Assert.Greater(e.Curve.TauRise.Raw, 0L, "curve.tau_rise 非默认(必填 > 0)");
            Assert.Greater(e.Curve.TauPeak, e.Curve.Incubation, "curve.tau_peak > incubation(必填)");
            Assert.Greater(e.Curve.TauFall.Raw, 0L, "curve.tau_fall 非默认(self_limit 必填 > 0)");
            Assert.IsTrue(e.Curve.SelfLimit, "curve.self_limit 被显式声明");
            Assert.IsFalse(e.Curve.Plateau, "curve.plateau 与 self_limit 互斥");
            Assert.IsTrue(e.Curve.Sigma.HasValue, "curve.sigma 逐病种覆盖被显式声明");
            Assert.Greater(e.Curve.Sigma.Value.Raw, 0L, "sigma ≥ 0 且非零占位");
            Assert.IsFalse(string.IsNullOrEmpty(e.Curve.BoundaryMode), "boundary_mode 必填(R1.3)");

            Assert.Greater(e.Scale.Raw, 0L, "scale 非默认(必填 > 0)");
            Assert.GreaterOrEqual(e.Scale.Raw, e.Curve.APeak.Raw, "scale ≥ a_peak(R1.3 校验 5)");

            Assert.IsNotNull(e.Relapse, "relapse 块被显式声明(fixture 覆盖复发段)");
            Assert.Greater(e.Relapse.ARel.Raw, 0L, "relapse.a_rel 非默认(必填 > 0)");
            Assert.Greater(e.Relapse.Interval, 0, "relapse.interval 非默认(必填 > 0)");
            Assert.Greater(e.Relapse.TauFall.Raw, 0L, "relapse.tau_fall 非默认(必填 > 0)");

            Assert.IsNotNull(e.Signs, "signs[] 被显式声明");
            Assert.Greater(e.Signs.Length, 0, "fixture signs[] 非空(体征投影链需要词键 + 通道位)");
            foreach (var s in e.Signs)
                Assert.IsFalse(string.IsNullOrEmpty(s.SignKey), "每个 sign 词键非空");

            Assert.IsNull(e.TreatableBy, "treatable_by 已拆独立轴文件,条目内恒 null(R1-19 不可达分支)");
        }

        [Test]
        public void test_registry_syntheticFixture_fixFieldsRoundTripBitExact()
        {
            // Arrange —— 全部 Fix 字段清单(作者态字符串 → FixParse → Fix → 再回字符串 → FixParse)
            var e = SyntheticDiseaseFixture.Create();
            Fix[] fixFields =
            {
                e.Curve.APeak, e.Curve.TauRise, e.Curve.TauFall, e.Curve.Sigma.Value,
                e.Scale, e.Relapse.ARel, e.Relapse.TauFall
            };

            // Act / Assert —— 往返逐位一致:raw → "raw/65536" → FixParse → 同 raw
            for (int i = 0; i < fixFields.Length; i++)
            {
                long raw = fixFields[i].Raw;
                var roundTripped = FixParse.Parse($"{raw}/{Fix.OneRaw}");
                Assert.AreEqual(raw, roundTripped.Raw, $"Fix 字段 #{i} 经 FixParse 往返须逐位一致");
            }

            // 确定性:两次 Create() 的 Fix 字段逐位相同(零随机、零墙钟)
            var again = SyntheticDiseaseFixture.Create();
            Fix[] second =
            {
                again.Curve.APeak, again.Curve.TauRise, again.Curve.TauFall, again.Curve.Sigma.Value,
                again.Scale, again.Relapse.ARel, again.Relapse.TauFall
            };
            for (int i = 0; i < fixFields.Length; i++)
                Assert.AreEqual(fixFields[i].Raw, second[i].Raw, $"两次 Create() 的 Fix 字段 #{i} 须逐位相同");
        }

        // ────────────────────────────────────────────────────────────────
        // 二、负夹具 —— 曲线面 R1-20…29,每条一个「只违该条」的最小反例
        // ────────────────────────────────────────────────────────────────

        private static RegistryValidationException AssertRejects(DiseaseRegistryEntry mutated)
        {
            var ex = Assert.Throws<RegistryValidationException>(() =>
                RegistrySchemaValidator.Validate(new List<DiseaseRegistryEntry> { mutated }));
            Assert.IsNotNull(ex);
            return ex;
        }

        [Test]
        public void test_registry_curveSelfLimitAndPlateauExclusive_throwsRule20()
        {
            // Arrange —— 违 GDD R1.3 校验 1:一个说会降、一个说会停
            var e = SyntheticDiseaseFixture.Create();
            e.Curve.Plateau = true; // SelfLimit 已为 true

            // Act / Assert
            Assert.AreEqual(20, AssertRejects(e).RuleNumber, "self_limit ∧ plateau 同真须报 R1-20");
        }

        [Test]
        public void test_registry_tauPeakNotGreaterThanIncubation_throwsRule21()
        {
            // Arrange ① —— 违 GDD R1.3 校验 2a:τ_peak = incubation ⇒ 上升段区间倒置
            var e1 = SyntheticDiseaseFixture.Create();
            e1.Curve.TauPeak = e1.Curve.Incubation;
            Assert.AreEqual(21, AssertRejects(e1).RuleNumber, "τ_peak ≤ incubation 须报 R1-21");

            // Arrange ② —— 违校验 2b:τ_rise = 0(负指数/除零)
            var e2 = SyntheticDiseaseFixture.Create();
            e2.Curve.TauRise = Fix.Zero;
            Assert.AreEqual(21, AssertRejects(e2).RuleNumber, "τ_rise ≤ 0 须报 R1-21");

            // Arrange ③ —— 违 Tuning §二:incubation < 0
            var e3 = SyntheticDiseaseFixture.Create();
            e3.Curve.Incubation = -1;
            Assert.AreEqual(21, AssertRejects(e3).RuleNumber, "incubation < 0 须报 R1-21");
        }

        [Test]
        public void test_registry_acuteHoldNonLethal_throwsRule22()
        {
            // Arrange —— 违 GDD R1.3 校验 4 前半:急性保持型(双 false)但 Lethality = 0
            var e = SyntheticDiseaseFixture.Create();
            e.Curve.SelfLimit = false;
            e.Curve.Plateau = false;
            e.Lethality = 0;

            // Act / Assert
            Assert.AreEqual(22, AssertRejects(e).RuleNumber, "急性保持型 + 非 lethal 须报 R1-22");
        }

        [Test]
        public void test_registry_scaleBelowAPeak_throwsRule23()
        {
            // Arrange ① —— 违 GDD R1.3 校验 5a:scale(1/4) < a_peak(1/2) ⇒ position 永久钉 1.0
            var e1 = SyntheticDiseaseFixture.Create();
            e1.Scale = FixParse.Parse("1/4");
            Assert.AreEqual(23, AssertRejects(e1).RuleNumber, "scale < a_peak 须报 R1-23");

            // Arrange ② —— 违校验 5b:scale = 0(除零 / 必填缺省)
            var e2 = SyntheticDiseaseFixture.Create();
            e2.Scale = Fix.Zero;
            Assert.AreEqual(23, AssertRejects(e2).RuleNumber, "scale ≤ 0 须报 R1-23");
        }

        [Test]
        public void test_registry_aPeakNonPositive_throwsRule24()
        {
            // Arrange —— 违 GDD R1.3 校验 6:a_peak = 0
            var e = SyntheticDiseaseFixture.Create();
            e.Curve.APeak = FixParse.Parse("0");

            // Act / Assert
            Assert.AreEqual(24, AssertRejects(e).RuleNumber, "a_peak ≤ 0 须报 R1-24");
        }

        [Test]
        public void test_registry_selfLimitWithoutTauFall_throwsRule25()
        {
            // Arrange —— 违 GDD R1.3 校验 7:self_limit ⇒ tau_fall > 0(e^(−τ/0) = 0/0)
            var e = SyntheticDiseaseFixture.Create();
            e.Curve.TauFall = Fix.Zero;

            // Act / Assert
            Assert.AreEqual(25, AssertRejects(e).RuleNumber, "self_limit ∧ tau_fall=0 须报 R1-25");
        }

        [Test]
        public void test_registry_relapseIncompleteValues_throwsRule26()
        {
            // Arrange ① —— 违 GDD R1.3 校验 3+8:interval = 0(半截块)
            var e1 = SyntheticDiseaseFixture.Create();
            e1.Relapse.Interval = 0;
            Assert.AreEqual(26, AssertRejects(e1).RuleNumber, "relapse.interval ≤ 0 须报 R1-26");

            // Arrange ② —— a_rel = 0
            var e2 = SyntheticDiseaseFixture.Create();
            e2.Relapse.ARel = Fix.Zero;
            Assert.AreEqual(26, AssertRejects(e2).RuleNumber, "relapse.a_rel ≤ 0 须报 R1-26");

            // Arrange ③ —— tau_rel_fall = 0
            var e3 = SyntheticDiseaseFixture.Create();
            e3.Relapse.TauFall = Fix.Zero;
            Assert.AreEqual(26, AssertRejects(e3).RuleNumber, "relapse.tau_rel_fall ≤ 0 须报 R1-26");
        }

        [Test]
        public void test_registry_boundaryModeOutsideWhitelist_throwsRule27()
        {
            // Arrange —— 违 R1.3 必填白名单:monotone / scan 之外
            var e = SyntheticDiseaseFixture.Create();
            e.Curve.BoundaryMode = "sweep";

            // Act / Assert
            Assert.AreEqual(27, AssertRejects(e).RuleNumber, "boundary_mode ∉ {monotone, scan} 须报 R1-27");
        }

        [Test]
        public void test_registry_negativeSigmaOverride_throwsRule28()
        {
            // Arrange —— 违 Tuning §四:σ 覆盖值为负
            var e = SyntheticDiseaseFixture.Create();
            e.Curve.Sigma = new Fix(-1);

            // Act / Assert
            Assert.AreEqual(28, AssertRejects(e).RuleNumber, "sigma < 0 须报 R1-28");
        }

        [Test]
        public void test_registry_signChannelOutsideClosedSet_throwsRule29()
        {
            // Arrange ① —— 第 6 位之外的未定义位(bit 6 = 64 ∉ 六通道闭集)
            var e1 = SyntheticDiseaseFixture.Create();
            e1.Signs[0] = new SignEntry("synth_bogus", 1 << 6);
            Assert.AreEqual(29, AssertRejects(e1).RuleNumber, "channel_mask 含未定义位须报 R1-29");

            // Arrange ② —— 零掩码(非零要求)
            var e2 = SyntheticDiseaseFixture.Create();
            e2.Signs[0] = new SignEntry("synth_bogus", 0);
            Assert.AreEqual(29, AssertRejects(e2).RuleNumber, "channel_mask = 0 须报 R1-29");
        }

        [Test]
        public void test_registry_signKeyEmpty_throwsRule29()
        {
            // Arrange —— 违 R1 表 signs[] 词键非空
            var e = SyntheticDiseaseFixture.Create();
            e.Signs[0] = new SignEntry("", SignChannels.Face);

            // Act / Assert
            Assert.AreEqual(29, AssertRejects(e).RuleNumber, "空 SignKey 须报 R1-29");
        }

        // ────────────────────────────────────────────────────────────────
        // 二之二、R1-30 —— R_rise > ε_MIN(BCD 码-1 收口,2026-10-10 定值落地)
        // ────────────────────────────────────────────────────────────────

        [Test]
        public void test_registry_rRiseAtEpsilonMin_throwsRule30()
        {
            // Arrange —— 违 GDD 校验 12 + F3 全局不等式:span = 1 tick,τ_rise = 200000 tick
            // ⇒ 比值 raw = round(65536/200000) = 0 ⇒ Exp(0) = 1(精确)⇒ R_rise = 0 ≤ ε_MIN ⇒ 拒收。
            // ⚠️ 刻意取「比值舍入为 0」而非「R_rise ≈ 1 LSB」:后者落在 Exp 逐步舍入误差带内,
            //    判据不稳;比值 = 0 时 Exp(0) = 1 无舍入,负测判据精确。
            // (R1-21 仍过:τ_peak = incubation + 1 > incubation;其余曲线面校验不受影响)
            var e = SyntheticDiseaseFixture.Create();
            e.Curve.TauPeak = e.Curve.Incubation + 1;   // span = 1 tick
            e.Curve.TauRise = FixParse.Parse("200000");

            // Act / Assert
            Assert.AreEqual(30, AssertRejects(e).RuleNumber,
                "R_rise ≤ ε_MIN(1 LSB)须报 R1-30(GDD 校验 12 —— 写入期门,2026-10-10 落地)");
        }

        [Test]
        public void test_registry_rRiseAboveEpsilonMin_passes()
        {
            // Arrange —— 边界内侧:span = 1 tick,τ_rise = 10000 tick ⇒ R_rise ≈ 6.5 LSB(> 1 ⇒ 合法)。
            // 与上测同构、唯 τ_rise 小一个量级 —— 证 R1-30 在**比 R_rise**,不是「一律拒小 span」。
            var e = SyntheticDiseaseFixture.Create();
            e.Curve.TauPeak = e.Curve.Incubation + 1;
            e.Curve.TauRise = FixParse.Parse("10000");

            // Act / Assert
            Assert.DoesNotThrow(() =>
                    RegistrySchemaValidator.Validate(new List<DiseaseRegistryEntry> { e }),
                "R_rise ≈ 6.5 LSB > ε_MIN(1 LSB)的配置必须合法(R1-30 不得误拒合法曲线)");
        }

        // ────────────────────────────────────────────────────────────────
        // 三、曲线形状抽样(F1 Base / Relapse / Decay + F2 position)—— 测试侧 oracle
        // ────────────────────────────────────────────────────────────────

        /// <summary>oracle:e^(−delta/tau),delta 单位 tick,tau 为 Fix 化时间常数。
        /// 照 GDD F1 的指数形状,经 Fix.Exp(批次 A)—— 全整数域,无浮点。</summary>
        private static Fix OracleExpNeg(long deltaTicks, Fix tau)
        {
            var ratio = new Fix(deltaTicks * Fix.OneRaw) / tau; // delta/tau(维度:tick/tick ⇒ 无量纲)
            return Fix.Exp(Fix.Zero - ratio);
        }

        /// <summary>oracle:上升段表达式(可越界求值,供 τ = τ_peak 连续性抽样)。</summary>
        private static Fix OracleRise(NaturalProgressCurve c, long tau)
        {
            if (tau < c.Incubation) return Fix.Zero;
            Fix e = OracleExpNeg(tau - c.Incubation, c.TauRise);
            // R_rise = 1 − e^(−(τ_peak − incubation)/τ_rise)(GDD F1:上升段归一化因子,常数)
            Fix rRise = Fix.One - OracleExpNeg(c.TauPeak - c.Incubation, c.TauRise);
            return c.APeak * (Fix.One - e) / rRise;
        }

        /// <summary>oracle:衰减段表达式(self_limit 分支)。</summary>
        private static Fix OracleFall(NaturalProgressCurve c, long tau)
            => c.APeak * OracleExpNeg(tau - c.TauPeak, c.TauFall);

        /// <summary>oracle:Base(τ) 三分支(GDD F1;fixture 为 self_limit 型,plateau/急性分支仅兜底)。</summary>
        private static Fix OracleBase(NaturalProgressCurve c, long tau)
        {
            if (tau < c.Incubation) return Fix.Zero;
            if (tau < c.TauPeak) return OracleRise(c, tau);
            if (c.SelfLimit) return OracleFall(c, tau);
            return c.APeak;
        }

        /// <summary>oracle:Relapse(τ),首击 k = 0 ⇒ τ_rel = interval × 1(GDD F1 / AC-9)。</summary>
        private static Fix OracleRelapse(RelapseCurve r, long tau)
        {
            if (r == null || tau < r.Interval) return Fix.Zero;
            return r.ARel * OracleExpNeg(tau - r.Interval, r.TauFall);
        }

        /// <summary>oracle:Decay(Δ) = e^(−Δ/half_life)·[Δ ≥ 0](GDD F1;Δ &lt; 0 归零门)。
        /// half_life 语义 = 处置事件载荷字段(AC-28,11/21a drug_profile 产出)——
        /// **不是**病种注册表字段(GDD R1.3 注:「病种定义病怎么走,药定义药怎么退」),
        /// 故此处以局部字面量代之,恰证其外部性。</summary>
        private static Fix OracleDecay(long deltaTicks, Fix halfLife)
        {
            if (deltaTicks < 0) return Fix.Zero;
            return OracleExpNeg(deltaTicks, halfLife);
        }

        [Test]
        public void test_curveShape_baseBeforeIncubation_isZero()
        {
            // Arrange
            var c = SyntheticDiseaseFixture.Create().Curve;

            // Act / Assert —— AC-26 形状:τ < incubation ⇒ Base ≡ 0(噪声不得使潜伏病人有读数)
            for (long tau = 0; tau < c.Incubation; tau += 50)
                Assert.AreEqual(Fix.Zero.Raw, OracleBase(c, tau).Raw, $"τ={tau} < incubation 须恒 0");
            Assert.AreEqual(Fix.Zero.Raw, OracleBase(c, c.Incubation - 1).Raw, "潜伏期末一 tick 仍须 0");
            Assert.AreEqual(Fix.Zero.Raw, OracleBase(c, c.Incubation).Raw, "τ = incubation 处 Base = 0(AC-6 连续起点)");
        }

        [Test]
        public void test_curveShape_baseRise_monotoneAndBounded()
        {
            // Arrange
            var c = SyntheticDiseaseFixture.Create().Curve;
            long samples = 0;
            Fix prev = Fix.Zero;

            // Act / Assert —— 上升段 [incubation, τ_peak):单调不降 · ≤ A_peak · 有样本
            for (long tau = c.Incubation; tau < c.TauPeak; tau += 60)
            {
                Fix b = OracleBase(c, tau);
                Assert.GreaterOrEqual(b.Raw, prev.Raw, $"上升段须单调不降(τ={tau})");
                Assert.LessOrEqual(b.Raw, c.APeak.Raw + TolTight, $"上升段须 ≤ A_peak(τ={tau})");
                prev = b;
                samples++;
            }
            Assert.Greater(samples, 0L, "上升段须抽到样本(τ_peak > incubation 由校验 2 保证)");

            // AC-6:τ = τ_peak 处上升段表达式 ≡ A_peak(R_rise 归一化生效),
            //       衰减段起点 = A_peak 精确(Exp(0) = 1)—— 两段连续相接
            Fix riseAtPeak = OracleRise(c, c.TauPeak);
            Assert.LessOrEqual(Math.Abs(riseAtPeak.Raw - c.APeak.Raw), TolTight,
                "上升段在 τ_peak 须相接于 A_peak(R_rise 归一化)");
            Assert.AreEqual(c.APeak.Raw, OracleFall(c, c.TauPeak).Raw, "衰减段起点须精确 = A_peak");
            Assert.LessOrEqual(Math.Abs(riseAtPeak.Raw - OracleFall(c, c.TauPeak).Raw), TolTight,
                "上升/衰减两段在 τ_peak 须连续(AC-6)");
        }

        [Test]
        public void test_curveShape_baseFall_selfLimitDecaysBounded()
        {
            // Arrange —— fixture 为 self_limit 型:τ ≥ τ_peak 后按 τ_fall 指数衰减
            var c = SyntheticDiseaseFixture.Create().Curve;
            Assert.IsTrue(c.SelfLimit, "fixture 形状前置:self_limit = true");

            // Act / Assert —— 衰减段:单调不降反向(不增)· ≤ A_peak
            long fallTicks = c.TauFall.Round(); // Fix 化时间常数 → tick 数(抽样步用)
            Fix prev = OracleBase(c, c.TauPeak);
            Assert.AreEqual(c.APeak.Raw, prev.Raw, "τ = τ_peak 起点 = A_peak");
            for (long tau = c.TauPeak + 60; tau <= c.TauPeak + 3 * fallTicks; tau += 60)
            {
                Fix b = OracleBase(c, tau);
                Assert.LessOrEqual(b.Raw, prev.Raw, $"衰减段须单调不增(τ={tau})");
                Assert.LessOrEqual(b.Raw, c.APeak.Raw + TolTight, $"衰减段须 ≤ A_peak(τ={tau})");
                prev = b;
            }

            // 量级锚点:τ = τ_peak + τ_fall ⇒ Base = A_peak × e^(−1)
            Fix atOneTauFall = OracleBase(c, c.TauPeak + fallTicks);
            long expected = (c.APeak * Fix.Exp(Fix.Zero - Fix.One)).Raw;
            Assert.LessOrEqual(Math.Abs(atOneTauFall.Raw - expected), TolExp,
                "τ_peak + τ_fall 处须 ≈ A_peak·e^(−1)(半衰形状经 Fix.Exp)");
        }

        [Test]
        public void test_curveShape_relapsePulse_shape()
        {
            // Arrange
            var e = SyntheticDiseaseFixture.Create();
            var r = e.Relapse;

            // Act / Assert —— 击发前恒 0(τ_rel = interval×1,AC-9:k=0)
            for (long tau = 0; tau < r.Interval; tau += 200)
                Assert.AreEqual(Fix.Zero.Raw, OracleRelapse(r, tau).Raw, $"τ={tau} < interval 须恒 0(复发未击发)");
            Assert.AreEqual(Fix.Zero.Raw, OracleRelapse(r, r.Interval - 1).Raw, "首击前一 tick 仍须 0");

            // 击发点精确 = A_rel(Exp(0) = 1)
            Assert.AreEqual(r.ARel.Raw, OracleRelapse(r, r.Interval).Raw, "τ = interval 处复发须精确 = A_rel");

            // 击发后按 τ_rel_fall 衰减:τ = interval + τ_rel_fall ⇒ A_rel × e^(−1);且单调不增
            long relFallTicks = r.TauFall.Round();
            long atOneTauFall = (r.ARel * Fix.Exp(Fix.Zero - Fix.One)).Raw;
            Assert.LessOrEqual(Math.Abs(OracleRelapse(r, r.Interval + relFallTicks).Raw - atOneTauFall), TolExp,
                "复发首击后一个 τ_rel_fall 须 ≈ A_rel·e^(−1)");
            Fix prev = OracleRelapse(r, r.Interval);
            for (long tau = r.Interval + 100; tau <= r.Interval + 5 * relFallTicks; tau += 100)
            {
                Fix v = OracleRelapse(r, tau);
                Assert.LessOrEqual(v.Raw, prev.Raw, $"复发脉冲须单调不增(τ={tau})");
                prev = v;
            }

            // ⊔ 语义前提(AC-8 形状):复发峰值不超主曲线峰值 ⇒ 取大不取和时不会把 fixture 读爆
            Assert.LessOrEqual(r.ARel.Raw, e.Curve.APeak.Raw, "fixture 自洽:A_rel ≤ A_peak(余烬型)");
        }

        [Test]
        public void test_curveShape_decayFactor_matchesExpMagnitude()
        {
            // Arrange —— half_life 走处置事件载荷语义(AC-28;非病种字段,见 OracleDecay 注)
            Fix halfLife = FixParse.Parse("400");

            // Act / Assert —— Δ = 0 ⇒ 精确 1(无衰减)
            Assert.AreEqual(Fix.OneRaw, OracleDecay(0, halfLife).Raw, "Decay(0) 须精确 = 1");

            // Δ = half_life ⇒ e^(−1) ≈ 0.3679;Δ = 2×half_life ⇒ e^(−2) ≈ 0.1353
            long e1 = Fix.Exp(Fix.Zero - Fix.One).Raw;
            long e2 = Fix.Exp(Fix.Zero - (Fix.One + Fix.One)).Raw;
            Assert.LessOrEqual(Math.Abs(OracleDecay(400, halfLife).Raw - e1), TolExp, "Decay(half_life) ≈ e^(−1)");
            Assert.LessOrEqual(Math.Abs(OracleDecay(800, halfLife).Raw - e2), TolExp, "Decay(2×half_life) ≈ e^(−2)");

            // 单调不增(0 → 6×half_life,步长 = half_life;尾部太小处不抽,防 3 LSB ε 掩盖)
            Fix prev = OracleDecay(0, halfLife);
            for (long delta = 400; delta <= 2400; delta += 400)
            {
                Fix f = OracleDecay(delta, halfLife);
                Assert.LessOrEqual(f.Raw, prev.Raw, $"Decay 须单调不增(Δ={delta})");
                prev = f;
            }

            // Δ < 0 归零门(时间反演必须归零,防指数爆炸)
            Assert.AreEqual(Fix.Zero.Raw, OracleDecay(-1, halfLife).Raw, "Decay(Δ<0) 须 = 0");
        }

        [Test]
        public void test_curveShape_position_withinUnitInterval()
        {
            // Arrange —— F2:position = clamp(Progress / SCALE, 0, 1);fixture 无处置/噪声,
            // Progress = Base ⊔ Relapse ⇒ 以两曲线包络代之
            var e = SyntheticDiseaseFixture.Create();

            // Act / Assert —— 全程抽样:position ≤ 1(由校验 5 scale ≥ a_peak 与曲线 ≤ a_peak 结构保证,
            // 本测在 Fix 除法的实际舍入下复核)
            long envelopeTicks = e.Curve.TauPeak + 3 * e.Curve.TauFall.Round();
            for (long tau = 0; tau <= envelopeTicks; tau += 60)
            {
                Fix progress = OracleBase(e.Curve, tau);
                Fix relapse = OracleRelapse(e.Relapse, tau);
                if (relapse.Raw > progress.Raw) progress = relapse;

                Fix position = progress / e.Scale;
                Assert.GreaterOrEqual(position.Raw, 0L, $"position ≥ 0(τ={tau})");
                Assert.LessOrEqual(position.Raw, Fix.OneRaw, $"position ≤ 1(τ={tau})");
            }
        }
    }
}
