// 照护杠杆适用集 + 多病聚合 + 痊愈支/迁延支 —— 2026-10-22 第三轮。
//
// 权威来源:
//   GDD disease-simulation.md:1135-1140 —— 照护杠杆适用 = {伤寒 / 痢疾 / 心衰};破伤风不在内
//   GDD disease-simulation.md:294 —— handle ∈ {causal, symptomatic_only, care, none}
//   GDD disease-simulation.md:1300-1303 —— F5 合成式(position_agg / trend_backward_agg 各自 max)
//   GDD disease-simulation.md:1119-1125 —— argmax 的 d,并列取 DiseaseId 最小者
//   GDD disease-simulation.md:1113-1120 —— 痊愈支六条件合取
//   GDD disease-simulation.md:1125 —— 迁延支「由 sign(trend_backward_agg) 分段」
//   GDD Tuning :1743-1744 —— W / TREND_EPS 均 *待定* ⇒ 本批作入参,不写死
//
// ⚠️ 本文件与 f4_outcome_evaluator_test 的分工:
//   后者验死亡支 / 昏迷支(第一轮);本文件验第三轮新增的四件:
//     ① CareLeverApplicability  ② DiseaseAggregator  ③ 痊愈支  ④ 迁延支

using System.Collections.Generic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.Sim
{
    /// <summary>照护杠杆适用集(GDD :1135-1140)。</summary>
    [TestFixture]
    internal sealed class CareLeverApplicabilityTest
    {
        private static DiseaseRegistryEntry Entry(string key, string handle = null)
        {
            return new DiseaseRegistryEntry
            {
                DiseaseId = 1,
                DiseaseKey = key,
                Kind = DiseaseKind.Disease,
                Handle = handle,
                Lethality = 1,
                Scale = new Fix(Fix.OneRaw),
                Curve = new NaturalProgressCurve
                {
                    APeak = new Fix(Fix.OneRaw),
                    TauRise = new Fix(Fix.OneRaw),
                    TauPeak = 100,
                    TauFall = new Fix(Fix.OneRaw),
                    BoundaryMode = "monotone",
                },
            };
        }

        /// <summary>适用集三员(伤寒 / 痢疾 / 心衰)⇒ 全部 true。</summary>
        [Test]
        public void test_careApplicable_threeMembers_allTrue()
        {
            Assert.IsTrue(CareLeverApplicability.AppliesTo(Entry("typhoid")), "伤寒在适用集内");
            Assert.IsTrue(CareLeverApplicability.AppliesTo(Entry("dysentery")), "痢疾在适用集内");
            Assert.IsTrue(CareLeverApplicability.AppliesTo(Entry("heart_failure")), "心衰在适用集内");
        }

        /// <summary>
        /// **破伤风不在适用集** —— GDD K8 明写「破伤风护理**不刷新**死亡窗口」。
        /// <para>这是本类最重要的单条判据:若破伤风被误纳入,「越阈 + 过窗口即死」
        /// 会被护理无限推迟,破伤风的「发病前清创预防」手柄失去意义。</para>
        /// </summary>
        [Test]
        public void test_tetanus_isNotCareApplicable()
        {
            Assert.IsFalse(CareLeverApplicability.AppliesTo(Entry("tetanus")),
                "破伤风护理不刷新死亡窗口(K8)—— 不得纳入适用集");
        }

        /// <summary>其它病种(如疟疾)不在适用集。</summary>
        [Test]
        public void test_otherDisease_notCareApplicable()
        {
            Assert.IsFalse(CareLeverApplicability.AppliesTo(Entry("malaria")));
            Assert.IsFalse(CareLeverApplicability.AppliesTo(Entry("measles")));
        }

        /// <summary>null 键 ⇒ false(不抛)。</summary>
        [Test]
        public void test_nullKey_returnsFalse()
        {
            Assert.IsFalse(CareLeverApplicability.AppliesToKey(null));
            Assert.IsFalse(CareLeverApplicability.AppliesTo(Entry(null)));
        }

        /// <summary>null 条目 ⇒ 抛(不是静默 false)。</summary>
        [Test]
        public void test_nullEntry_throws()
        {
            Assert.Throws<System.ArgumentNullException>(
                () => CareLeverApplicability.AppliesTo(null));
        }

        /// <summary>`handle == "care"` ⇔ 在适用集内(派生关系一致)。</summary>
        [Test]
        public void test_handleCare_consistentWithApplicability()
        {
            Assert.IsTrue(CareLeverApplicability.CareApplicableConsistent(Entry("typhoid", "care")),
                "伤寒 + handle=care ⇒ 一致");
            Assert.IsFalse(CareLeverApplicability.CareApplicableConsistent(Entry("typhoid", "causal")),
                "伤寒 + handle=causal ⇒ 不一致(在适用集却不是 care)");
            Assert.IsFalse(CareLeverApplicability.CareApplicableConsistent(Entry("tetanus", "care")),
                "破伤风 + handle=care ⇒ 不一致(不在适用集却声称 care)");
        }

        /// <summary>`Handle == null`(未烘焙)⇒ 一致(不制造假红)。</summary>
        [Test]
        public void test_unbakedHandle_isConsistent()
        {
            Assert.IsTrue(CareLeverApplicability.CareApplicableConsistent(Entry("typhoid", null)),
                "未烘焙条目无从断言,不得判红");
        }
    }

    /// <summary>F5 多病聚合 + argmax 的 d(GDD :1119-1125 / :1300-1303)。</summary>
    [TestFixture]
    internal sealed class DiseaseAggregatorTest
    {
        private static DiseasePosition P(int id, double position, double trend = 0)
        {
            return new DiseasePosition(
                id,
                new Fix((long)(position * Fix.OneRaw)),
                new Fix((long)(trend * Fix.OneRaw)));
        }

        /// <summary>取大 —— position_agg = max。</summary>
        [Test]
        public void test_aggregate_takesMax()
        {
            var r = DiseaseAggregator.Aggregate(new[] { P(1, 0.2), P(2, 0.9), P(3, 0.5) });
            Assert.AreEqual(new Fix((long)(0.9 * Fix.OneRaw)).Raw, r.PositionAgg.Raw);
            Assert.AreEqual(2, r.DominantDiseaseId);
        }

        /// <summary>
        /// **AC-29(c) 的判据**:病人患麻疹(position 0.9,非致死)+ 轻刺伤(position 0.2,lethal)
        /// ⇒ argmax 的 d = 麻疹,不是刺伤 ⇒ F4 不会误判死。
        /// <para>这是 GDD `:1119-1125` 点名的假绿反例,本条把「d 选对了」钉死。</para>
        /// </summary>
        [Test]
        public void test_argmax_picksHighestPosition_notLethalOne()
        {
            // id 1 = 麻疹(0.9,非致死);id 2 = 刺伤(0.2,lethal 急性保持型)
            var r = DiseaseAggregator.Aggregate(new[] { P(1, 0.9), P(2, 0.2) });
            Assert.AreEqual(1, r.DominantDiseaseId,
                "argmax 必须取 position 最大的那个 d(麻疹),不是 lethal 的那个(刺伤)");
        }

        /// <summary>
        /// **并列取 DiseaseId 最小者**(GDD :1120)—— 确定性要求,不是优化。
        /// <para>两种遍历序必须给出**同一个** d。若写成「先到者胜」,下面两条会分叉。</para>
        /// </summary>
        [Test]
        public void test_tie_picksSmallestDiseaseId_bothOrders()
        {
            var forward = DiseaseAggregator.Aggregate(new[] { P(7, 0.5), P(3, 0.5) });
            var backward = DiseaseAggregator.Aggregate(new[] { P(3, 0.5), P(7, 0.5) });

            Assert.AreEqual(3, forward.DominantDiseaseId, "并列 ⇒ 取 id 最小者");
            Assert.AreEqual(3, backward.DominantDiseaseId,
                "遍历序颠倒后仍须取 id 最小者 —— 否则结果依赖入流序,破逐位重放");
        }

        /// <summary>三项全并列 ⇒ 仍取最小 id。</summary>
        [Test]
        public void test_threeWayTie_picksSmallest()
        {
            var r = DiseaseAggregator.Aggregate(new[] { P(9, 0.5), P(4, 0.5), P(6, 0.5) });
            Assert.AreEqual(4, r.DominantDiseaseId);
        }

        /// <summary>
        /// **两个 max 是独立求的** —— position_agg 与 trend_backward_agg 各自取大,
        /// 不保证来自同一个 d(GDD :1301-1303)。
        /// </summary>
        [Test]
        public void test_trendAgg_isIndependentMax_notFromDominant()
        {
            // d=1:position 最高但趋势为负;d=2:position 低但趋势最高
            var r = DiseaseAggregator.Aggregate(new[] { P(1, 0.9, -0.5), P(2, 0.2, 0.3) });

            Assert.AreEqual(1, r.DominantDiseaseId, "position 的 argmax = d1");
            Assert.AreEqual(new Fix((long)(0.3 * Fix.OneRaw)).Raw, r.TrendBackwardAgg.Raw,
                "trend_backward_agg 取 d2 的值(与 dominant d 不同源)—— GDD 明写两者各自 max");
        }

        /// <summary>负趋势取大 —— 全负时取最大的(最接近 0 的)。</summary>
        [Test]
        public void test_trendAgg_allNegative_takesLargest()
        {
            var r = DiseaseAggregator.Aggregate(new[] { P(1, 0.5, -0.3), P(2, 0.4, -0.1) });
            Assert.AreEqual(new Fix((long)(-0.1 * Fix.OneRaw)).Raw, r.TrendBackwardAgg.Raw);
        }

        /// <summary>空集合 ⇒ HasAny = false,哨兵 id = -1。</summary>
        [Test]
        public void test_empty_hasNoDisease()
        {
            var r = DiseaseAggregator.Aggregate(new DiseasePosition[0]);
            Assert.IsFalse(r.HasAny);
            Assert.AreEqual(DiseaseAggregator.NoDisease, r.DominantDiseaseId);
            Assert.AreEqual(0, r.PositionAgg.Raw);
        }

        /// <summary>null 集合 ⇒ 同空集合(不抛)。</summary>
        [Test]
        public void test_null_treatedAsEmpty()
        {
            var r = DiseaseAggregator.Aggregate(null);
            Assert.IsFalse(r.HasAny);
            Assert.AreEqual(DiseaseAggregator.NoDisease, r.DominantDiseaseId);
        }

        /// <summary>单元素 ⇒ 就是它自己。</summary>
        [Test]
        public void test_singleElement()
        {
            var r = DiseaseAggregator.Aggregate(new[] { P(5, 0.42, 0.1) });
            Assert.AreEqual(5, r.DominantDiseaseId);
            Assert.AreEqual(new Fix((long)(0.42 * Fix.OneRaw)).Raw, r.PositionAgg.Raw);
            Assert.IsTrue(r.HasAny);
        }
    }

    /// <summary>痊愈支(GDD :1113-1120)与迁延支(:1125)。</summary>
    [TestFixture]
    internal sealed class F4RecoveredAndChronicTest
    {
        private const long Onset = 1000;

        private static Fix Raw(long raw) => new Fix(raw);

        /// <summary>可愈型:self_limit = true(会自己好)。</summary>
        private static DiseaseRegistryEntry SelfLimited()
        {
            return new DiseaseRegistryEntry
            {
                DiseaseId = 10,
                DiseaseKey = "DIS_SELF_LIMIT",
                Kind = DiseaseKind.Disease,
                Lethality = 1,
                Scale = new Fix(Fix.OneRaw),
                Curve = new NaturalProgressCurve
                {
                    Incubation = 0,
                    APeak = new Fix(Fix.OneRaw),
                    TauRise = new Fix(10 * Fix.OneRaw),
                    TauPeak = 500,
                    TauFall = new Fix(100 * Fix.OneRaw),
                    Plateau = false,
                    SelfLimit = true,
                    BoundaryMode = "monotone",
                },
            };
        }

        /// <summary>带复发块 ⇒ 永不入痊愈。</summary>
        private static DiseaseRegistryEntry WithRelapse()
        {
            DiseaseRegistryEntry e = SelfLimited();
            e.DiseaseKey = "DIS_RELAPSE";
            e.Relapse = new RelapseCurve
            {
                ARel = new Fix(Fix.OneRaw),
                Interval = 100,
                TauFall = new Fix(50 * Fix.OneRaw),
            };
            return e;
        }

        /// <summary>豁免类:plateau ∧ ¬lethal ⇒ 永不入痊愈。</summary>
        private static DiseaseRegistryEntry Exempt()
        {
            DiseaseRegistryEntry e = SelfLimited();
            e.DiseaseKey = "DIS_EXEMPT";
            e.Lethality = 0;
            e.Curve.Plateau = true;
            e.Curve.SelfLimit = false;
            return e;
        }

        /// <summary>不可愈型:self_limit = false 且非豁免(急性保持型)。</summary>
        private static DiseaseRegistryEntry NotCurable()
        {
            DiseaseRegistryEntry e = SelfLimited();
            e.DiseaseKey = "DIS_ACUTE_HOLD";
            e.Lethality = 2;
            e.Curve.SelfLimit = false;
            e.Curve.Plateau = false;
            return e;
        }

        /// <summary>已配置的稳定窗:W = 100 tick,TREND_EPS = 1 LSB,低位已稳定 200 tick。</summary>
        private static RecoveryWindowInput Window(long heldTicks = 200)
            => new RecoveryWindowInput(windowTicks: 100, trendEpsRaw: 1, lowPositionHeldTicks: heldTicks);

        // ══════════════════ ① 六条件全成立 ⇒ Recovered ══════════════════

        /// <summary>自限型 + 低位 + 非上升 + 稳定够久 ⇒ 痊愈。</summary>
        [Test]
        public void test_recovered_allSixConditions_returnsRecovered()
        {
            var state = F4OutcomeEvaluator.Evaluate(
                ActorClass.Patient, SelfLimited(),
                Raw(StateMachine.RECOVER_THRESHOLD - 100),   // < RECOVER
                trendBackwardAgg: Fix.Zero,                   // 非上升
                onsetTick: Onset, currentTick: Onset + 2000,
                Window());

            Assert.AreEqual(InjuryState.Recovered, state);
        }

        // ══════════════════ ② 逐条去掉一个条件 ⇒ 不判痊愈 ══════════════════

        /// <summary>条件 ① 不成立(position 未跌破痊愈门)⇒ 不痊愈。</summary>
        [Test]
        public void test_recovered_condition1_positionAboveRecover_notRecovered()
        {
            var state = F4OutcomeEvaluator.Evaluate(
                ActorClass.Patient, SelfLimited(),
                Raw(StateMachine.RECOVER_THRESHOLD + 1),      // ≥ RECOVER
                Fix.Zero, Onset, Onset + 2000, Window());

            Assert.AreNotEqual(InjuryState.Recovered, state);
        }

        /// <summary>条件 ② 不成立(有 relapse 块)⇒ 不痊愈(GDD :1119 / R3.3)。</summary>
        [Test]
        public void test_recovered_condition2_hasRelapse_notRecovered()
        {
            var state = F4OutcomeEvaluator.Evaluate(
                ActorClass.Patient, WithRelapse(),
                Raw(StateMachine.RECOVER_THRESHOLD - 100),
                Fix.Zero, Onset, Onset + 2000, Window());

            Assert.AreNotEqual(InjuryState.Recovered, state,
                "带复发块的病种永不入痊愈 —— 否则「好转 → 复发」的医学链路消失");
        }

        /// <summary>条件 ③ 不成立(豁免类)⇒ 不痊愈(R3.6)。</summary>
        [Test]
        public void test_recovered_condition3_exemptClass_notRecovered()
        {
            var state = F4OutcomeEvaluator.Evaluate(
                ActorClass.Patient, Exempt(),
                Raw(StateMachine.RECOVER_THRESHOLD - 100),
                Fix.Zero, Onset, Onset + 2000, Window());

            Assert.AreNotEqual(InjuryState.Recovered, state,
                "豁免类永不入痊愈 —— 「他还在病着」须为真(R3.6)");
        }

        /// <summary>
        /// 条件 ④ 不成立(不可愈型)⇒ 不痊愈 —— 校验 16 的核心。
        /// <para>防的是「噪声 / 负 bonus 把不可愈病种压进痊愈」:position 低 ≠ 病好了。</para>
        /// </summary>
        [Test]
        public void test_recovered_condition4_notCurable_notRecovered()
        {
            var state = F4OutcomeEvaluator.Evaluate(
                ActorClass.Patient, NotCurable(),
                Raw(StateMachine.RECOVER_THRESHOLD - 100),
                Fix.Zero, Onset, Onset + 2000, Window());

            Assert.AreNotEqual(InjuryState.Recovered, state,
                "self_limit=false 的病种由型本身保证不达痊愈门 —— 校验 16 的可愈性结构门");
        }

        /// <summary>
        /// 条件 ⑤ 不成立(上升期且 τ ≤ τ_peak)⇒ 不痊愈 —— 四轮 K5。
        /// <para>缺门则「病刚发(τ&lt;incubation)就判愈」:自限型上升段 position 仍低。</para>
        /// </summary>
        [Test]
        public void test_recovered_condition5_risingBeforeTauPeak_notRecovered()
        {
            var state = F4OutcomeEvaluator.Evaluate(
                ActorClass.Patient, SelfLimited(),
                Raw(StateMachine.RECOVER_THRESHOLD - 100),
                trendBackwardAgg: Raw(Fix.OneRaw / 10),        // 明确上升(> TREND_EPS)
                onsetTick: Onset,
                currentTick: Onset + 100,                      // τ = 100 < τ_peak = 500
                Window());

            Assert.AreNotEqual(InjuryState.Recovered, state,
                "上升期且未过 τ_peak ⇒ 不判愈(否则病刚发就判愈)");
        }

        /// <summary>条件 ⑤ 的例外分支:τ &gt; τ_peak 时即使趋势为正也放行。</summary>
        [Test]
        public void test_recovered_condition5_risingButPastTauPeak_recovered()
        {
            var state = F4OutcomeEvaluator.Evaluate(
                ActorClass.Patient, SelfLimited(),
                Raw(StateMachine.RECOVER_THRESHOLD - 100),
                trendBackwardAgg: Raw(Fix.OneRaw / 10),        // 仍为正
                onsetTick: Onset,
                currentTick: Onset + 1000,                     // τ = 1000 > τ_peak = 500
                Window());

            Assert.AreEqual(InjuryState.Recovered, state,
                "τ > τ_peak 时趋势为正也放行 —— GDD 的「或」分支");
        }

        /// <summary>条件 ⑥ 不成立(低位稳定时长不足 W)⇒ 不痊愈。</summary>
        [Test]
        public void test_recovered_condition6_notStableEnough_notRecovered()
        {
            var state = F4OutcomeEvaluator.Evaluate(
                ActorClass.Patient, SelfLimited(),
                Raw(StateMachine.RECOVER_THRESHOLD - 100),
                Fix.Zero, Onset, Onset + 2000,
                Window(heldTicks: 99));                        // < W = 100

            Assert.AreNotEqual(InjuryState.Recovered, state,
                "低位稳定 < W ⇒ 不判愈(防 ±σ 抖动把终态判成随机)");
        }

        /// <summary>条件 ⑥ 边界:恰好 = W ⇒ 痊愈(判据是 ≥)。</summary>
        [Test]
        public void test_recovered_condition6_exactlyAtW_recovered()
        {
            var state = F4OutcomeEvaluator.Evaluate(
                ActorClass.Patient, SelfLimited(),
                Raw(StateMachine.RECOVER_THRESHOLD - 100),
                Fix.Zero, Onset, Onset + 2000,
                Window(heldTicks: 100));                       // 恰 = W

            Assert.AreEqual(InjuryState.Recovered, state, "稳定时长恰等于 W 是合法边界");
        }

        // ══════════════════ ③ 未配置窗口 ⇒ 两支都不求值 ══════════════════

        /// <summary>
        /// **W / TREND_EPS 未定(default)⇒ 痊愈支不求值** —— 退回纯阈值分级。
        /// <para>这是本批「数值待定不代拍」的判据:若把 W 当 0 用,「稳定 0 tick」恒成立
        /// ⇒ 痊愈支被无条件放行 ⇒ 静默假绿。default 必须让整支**不判**。</para>
        /// </summary>
        [Test]
        public void test_unconfiguredWindow_recoveredNotEvaluated()
        {
            var state = F4OutcomeEvaluator.Evaluate(
                ActorClass.Patient, SelfLimited(),
                Raw(StateMachine.RECOVER_THRESHOLD - 100),
                Onset, Onset + 2000);                          // 无 window 重载(5 参)

            Assert.AreNotEqual(InjuryState.Recovered, state,
                "W 未配置 ⇒ 痊愈支不求值(不得用 0 当窗口)");
            Assert.AreEqual(InjuryState.Severe, state,
                "退回纯阈值分级:RECOVER+500−100 = 7764 ≥ SEVERE(2000) ⇒ Severe");
        }

        // ══════════════════ ④ 迁延支 ══════════════════

        /// <summary>未达痊愈门 + 趋势非上升 ⇒ 迁延。</summary>
        [Test]
        public void test_chronic_notRecoveredAndFlat_returnsChronic()
        {
            // 不可愈型(痊愈支被 ④ 挡掉),position 在 RECOVER 与 CRITICAL 之间,趋势平
            var state = F4OutcomeEvaluator.Evaluate(
                ActorClass.Patient, NotCurable(),
                Raw(StateMachine.RECOVER_THRESHOLD + 500),
                trendBackwardAgg: Fix.Zero,
                onsetTick: Onset, currentTick: Onset + 2000,
                Window());

            Assert.AreEqual(InjuryState.Chronic, state);
        }

        /// <summary>趋势仍在上升 ⇒ 不是迁延(落回轻症分级)。</summary>
        [Test]
        public void test_chronic_rising_notChronic()
        {
            var state = F4OutcomeEvaluator.Evaluate(
                ActorClass.Patient, NotCurable(),
                Raw(StateMachine.RECOVER_THRESHOLD + 500),
                trendBackwardAgg: Raw(Fix.OneRaw),             // > TREND_EPS
                onsetTick: Onset, currentTick: Onset + 2000,
                Window());

            Assert.AreNotEqual(InjuryState.Chronic, state,
                "趋势上升 ⇒ 仍在进展,不是迁延");
        }

        /// <summary>迁延不越过危殆 —— position ≥ CRITICAL 时归 Critical。</summary>
        [Test]
        public void test_chronic_aboveCritical_returnsCritical()
        {
            var state = F4OutcomeEvaluator.Evaluate(
                ActorClass.Patient, NotCurable(),
                Raw(StateMachine.CRITICAL_THRESHOLD),          // 恰 = CRITICAL
                Fix.Zero, Onset, Onset + 2000, Window());

            Assert.AreEqual(InjuryState.Critical, state,
                "危殆先于迁延判定(严重度降序)");
        }

        /// <summary>未配置窗口 ⇒ 迁延支也不求值。</summary>
        [Test]
        public void test_unconfiguredWindow_chronicNotEvaluated()
        {
            var state = F4OutcomeEvaluator.Evaluate(
                ActorClass.Patient, NotCurable(),
                Raw(StateMachine.RECOVER_THRESHOLD + 500),
                Onset, Onset + 2000);                          // 无 window 重载

            Assert.AreNotEqual(InjuryState.Chronic, state,
                "W 未配置 ⇒ 迁延支不求值");
        }

        // ══════════════════ ⑤ 死亡支 / 昏迷支不受影响(回归) ══════════════════

        /// <summary>配了窗口后死亡支仍优先于痊愈支(严重度降序)。</summary>
        [Test]
        public void test_deathStillWinsOverRecovered()
        {
            // 急性保持型(不可愈)且越 DEATH、窗口已过 ⇒ Deceased
            var state = F4OutcomeEvaluator.Evaluate(
                ActorClass.Patient, NotCurable(),
                Raw(StateMachine.DEATH_THRESHOLD),
                Fix.Zero, Onset, Onset + StateMachine.TREATMENT_WINDOW + 1,
                Window());

            Assert.AreEqual(InjuryState.Deceased, state, "死亡支优先于一切");
        }

        /// <summary>配了窗口后昏迷支仍优先于痊愈支。</summary>
        [Test]
        public void test_comaStillWinsOverRecovered()
        {
            var state = F4OutcomeEvaluator.Evaluate(
                ActorClass.Patient, SelfLimited(),             // 自限型 ⇒ 死亡支关闭
                Raw(StateMachine.COMA_THRESHOLD),
                Fix.Zero, Onset, Onset + 2000, Window());

            Assert.AreEqual(InjuryState.Coma, state, "昏迷支优先于痊愈支");
        }

        // ══════════════════ ⑥ ActorClass.Patient 的行为 ══════════════════

        /// <summary>
        /// **`ActorClass.Patient` 不走玩家专属的战伤破例** —— 这是新增该值的核心判据。
        /// <para>破例三条件是 <c>Player ∧ Kind=Injury ∧ ∈白名单</c>;病人不满足第一项
        /// ⇒ <c>SelfLimited</c> 取注册表原值。若把病人当 <c>Player</c> 传,白名单伤情
        /// 会错误地关闭其自限门。</para>
        /// </summary>
        [Test]
        public void test_patientClass_doesNotGetPlayerBattleInjuryException()
        {
            // 白名单内的伤情 + 自限型曲线 ⇒ Patient 的 SelfLimited = self_limit = true
            var e = new DiseaseRegistryEntry
            {
                DiseaseId = 2,
                DiseaseKey = "INJ_STAB",                       // 白名单内
                Kind = DiseaseKind.Injury,
                Lethality = 2,
                Scale = new Fix(Fix.OneRaw),
                Curve = new NaturalProgressCurve
                {
                    APeak = new Fix(Fix.OneRaw),
                    TauRise = new Fix(Fix.OneRaw),
                    TauPeak = 300,
                    TauFall = new Fix(600 * Fix.OneRaw),
                    Plateau = false,
                    SelfLimit = true,                          // 自限
                    BoundaryMode = "monotone",
                },
            };

            Assert.IsTrue(EntityDeathGates.SelfLimited(ActorClass.Patient, e),
                "病人的 SelfLimited 取注册表原值(true),不吃 Player 的白名单破例");
            Assert.IsFalse(EntityDeathGates.SelfLimited(ActorClass.Player, e),
                "玩家 + 白名单伤情 + lethal ⇒ 破例 ⇒ SelfLimited = false(对照)");
        }

        /// <summary>病人不是 combatant。</summary>
        [Test]
        public void test_patientClass_isNotCombatant()
        {
            Assert.IsFalse(EntityDeathGates.IsCombatant(ActorClass.Patient),
                "GDD 真值表:13 病人 / 友方 NPC ⇒ IsCombatant = false");
        }

        /// <summary>病人可被疾病致死(LethalFor 对非 combatant 开放)。</summary>
        [Test]
        public void test_patientClass_lethalForIsTrue()
        {
            Assert.IsTrue(EntityDeathGates.LethalFor(ActorClass.Patient, NotCurable()),
                "病人侧 LethalFor = lethal(d) 原语义");
        }
    }
}
