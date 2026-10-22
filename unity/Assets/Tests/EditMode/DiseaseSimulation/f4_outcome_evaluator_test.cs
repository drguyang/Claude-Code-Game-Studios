// F4 求值体 + `last_intervention` 聚合器 —— 三件缺口的机械 pin(2026-10-22 立)。
//
// 权威来源:
//   GDD disease-simulation.md:1080-1112 —— F4 判定顺序(严重度降序)
//   GDD disease-simulation.md:1126-1140 —— `last_intervention` 三通道 + 哨兵
//   GDD 规则六 · 集群 2 —— 照护支派生式 `max(最后止, 最后动作 + CARE_GAP)`
//   GDD 校验 4b / 14 —— 注册表写入期下界(R1-34 / R1-35)
//   ADR-016 §一 —— 三源不变量(纯函数)
//
// ── 立件缘由 ──────────────────────────────────────────────────────────────────
// 2026-10-22 实测:「值已裁 ≠ F4 可求值」。六个阈值常量有裁定值并落在
// `entities.yaml` + GDD §五,但:
//   ① F4 求值体不存在(`F4OutcomeEvaluator` 只在注释里出现一次,作为「裁定名」);
//   ② `last_intervention` 聚合器不存在(只有形参在传);
//   ③ 注册表侧 4b 零校验(`A_peak/SCALE ≥ DEATH_THRESHOLD`)。
// ⇒ 本文件为三件各出机械判据。
//
// ── 为什么每条都必须是「可红」的 ────────────────────────────────────────────
// 本项目已抓过五次「用一句自述顶替一个从未存在的门」。故每条测试都对准
// **一个具体行为**,删掉被测代码即转红(证伪轮见证据件)。

using System;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.Sim
{
    /// <summary>F4 求值体 + `last_intervention` 聚合器。</summary>
    [TestFixture]
    internal sealed class F4OutcomeEvaluatorTest
    {
        // ══════════════════ 夹具 ══════════════════

        private const long Onset = 1000;
        private const long Tick = 5000;

        /// <summary>
        /// 造一条病种条目。默认 = **致死型 plateau 病**(`lethal=true ∧ plateau=true`),
        /// 即 F4 死亡支**可成立**的形态。
        /// </summary>
        private static DiseaseRegistryEntry LethalPlateau(int lethality = 2)
            => new DiseaseRegistryEntry
            {
                DiseaseId = 9,
                DiseaseKey = "DIS_TEST_LETHAL",
                Kind = DiseaseKind.Disease,
                Lethality = lethality,
                Scale = new Fix(100 * Fix.OneRaw),
                Curve = new NaturalProgressCurve
                {
                    APeak = new Fix(70 * Fix.OneRaw),
                    Plateau = true,
                    SelfLimit = false,
                    Incubation = 0,
                    TauPeak = 100,
                    TauRise = new Fix(10 * Fix.OneRaw),
                    TauFall = new Fix(100 * Fix.OneRaw),
                    BoundaryMode = "monotone",
                },
            };

        /// <summary>自限型(关死亡支)。</summary>
        private static DiseaseRegistryEntry SelfLimited()
        {
            var e = LethalPlateau();
            e.DiseaseKey = "DIS_TEST_SELFLIMIT";
            e.Curve.Plateau = false;
            e.Curve.SelfLimit = true;
            return e;
        }

        /// <summary>非致死型(关死亡支)。</summary>
        private static DiseaseRegistryEntry NonLethal()
        {
            var e = LethalPlateau();
            e.DiseaseKey = "DIS_TEST_NONLETHAL";
            e.Lethality = 0;
            return e;
        }

        private static Fix Raw(int raw) => new Fix(raw);

        // ══════════════════ ① 死亡支:四条件合取 ══════════════════

        /// <summary>
        /// 四条件**全满足** ⇒ Deceased。这是死亡支的正例;下面四条各去掉一个条件。
        /// </summary>
        [Test]
        public void test_death_allFourConditions_returnsDeceased()
        {
            var state = F4OutcomeEvaluator.Evaluate(
                ActorClass.Player, LethalPlateau(), Raw(StateMachine.DEATH_THRESHOLD),
                onsetTick: Onset, currentTick: Onset + StateMachine.TREATMENT_WINDOW + 1);

            Assert.AreEqual(InjuryState.Deceased, state);
        }

        /// <summary>
        /// 条件③失败(未越死线)⇒ **不** Deceased。positionAgg 恰在死线下 1 raw。
        /// </summary>
        [Test]
        public void test_death_belowThreshold_isNotDeceased()
        {
            var state = F4OutcomeEvaluator.Evaluate(
                ActorClass.Player, LethalPlateau(), Raw(StateMachine.DEATH_THRESHOLD - 1),
                onsetTick: Onset, currentTick: Onset + StateMachine.TREATMENT_WINDOW + 1);

            Assert.AreNotEqual(InjuryState.Deceased, state,
                "未越死线不得判死 —— 阈值是四条件之一");
        }

        /// <summary>
        /// 条件④失败(恰在窗口边界上)⇒ **不** Deceased。
        /// <para>窗口语义是**严格大于**:`(t − last) > TREATMENT_WINDOW`。
        /// 恰等于 ⇒ 仍在窗口内 ⇒ 不判死。</para>
        /// </summary>
        [Test]
        public void test_death_exactlyAtWindowBoundary_isNotDeceased()
        {
            var state = F4OutcomeEvaluator.Evaluate(
                ActorClass.Player, LethalPlateau(), Raw(StateMachine.DEATH_THRESHOLD),
                onsetTick: Onset,
                currentTick: Onset + StateMachine.TREATMENT_WINDOW); // 恰等于

            Assert.AreNotEqual(InjuryState.Deceased, state,
                "窗口条件是严格大于 —— 恰在边界上仍算「未过窗口」");
        }

        /// <summary>
        /// 条件②失败(`self_limit = true`)⇒ **不** Deceased,且落到昏迷(若 ≥ COMA)。
        /// <para>这条同时验证「昏迷支**无谓词门**」:自限型在 ≥COMA 时进昏迷,不是死。</para>
        /// </summary>
        [Test]
        public void test_death_selfLimited_isNotDeceased_butComa()
        {
            var state = F4OutcomeEvaluator.Evaluate(
                ActorClass.Player, SelfLimited(), Raw(StateMachine.DEATH_THRESHOLD),
                onsetTick: Onset, currentTick: Onset + StateMachine.TREATMENT_WINDOW + 1);

            Assert.AreEqual(InjuryState.Coma, state,
                "自限型关死亡支;≥COMA ⇒ 昏迷(昏迷支无谓词门)");
        }

        /// <summary>
        /// 条件①失败(combatant)⇒ **不** Deceased,且落到昏迷(ADR-016 §二)。
        /// <para>敌人 / 兽归零 = 昏迷,永不判死 —— 这是 ADR-016 §二 的硬要求。</para>
        /// </summary>
        [Test]
        public void test_death_combatant_isNotDeceased_butComa()
        {
            foreach (ActorClass cls in new[] { ActorClass.HumanEnemy, ActorClass.Beast })
            {
                var state = F4OutcomeEvaluator.Evaluate(
                    cls, LethalPlateau(), Raw(StateMachine.DEATH_THRESHOLD),
                    onsetTick: Onset, currentTick: Onset + StateMachine.TREATMENT_WINDOW + 1);

                Assert.AreEqual(InjuryState.Coma, state,
                    $"{cls}:combatant 归零 = 昏迷,不得判死(ADR-016 §二)");
            }
        }

        /// <summary>条件①失败(非致死型)⇒ 不 Deceased。</summary>
        [Test]
        public void test_death_nonLethal_isNotDeceased()
        {
            var state = F4OutcomeEvaluator.Evaluate(
                ActorClass.Player, NonLethal(), Raw(StateMachine.DEATH_THRESHOLD),
                onsetTick: Onset, currentTick: Onset + StateMachine.TREATMENT_WINDOW + 1);

            Assert.AreNotEqual(InjuryState.Deceased, state);
        }

        // ══════════════════ ② 死亡支 × last_intervention 的接线 ══════════════════

        /// <summary>
        /// **有处置 ⇒ 窗口重新计时** —— 这是 `last_intervention` 真正被 F4 消费的判据。
        /// <para>构造:onset 早得远过窗口,但处置发生在 `currentTick - 100`(窗口内)
        /// ⇒ **不**判死。若 F4 没接聚合器(直接用 onset),这条会红。</para>
        /// </summary>
        [Test]
        public void test_death_recentTreatment_resetsWindow_notDeceased()
        {
            long current = Onset + StateMachine.TREATMENT_WINDOW * 10; // 远超窗口
            var treatment = new LastInterventionAggregator.TreatmentChannelInput(
                new[] { current - 100 });                              // 但刚被治过

            var state = F4OutcomeEvaluator.Evaluate(
                ActorClass.Player, LethalPlateau(), Raw(StateMachine.DEATH_THRESHOLD),
                onsetTick: Onset, currentTick: current, treatment,
                LastInterventionAggregator.CareChannelInput.None);

            Assert.AreNotEqual(InjuryState.Deceased, state,
                "窗口内有过处置 ⇒ 倒计时重置 ⇒ 不判死。这是 last_intervention 被 F4 消费的证据");
        }

        /// <summary>
        /// **持续照护 ⇒ 窗口重新计时** —— 照护支的派生式被 F4 消费。
        /// <para>最后护理动作在 `currentTick - 200`(< CARE_GAP = 600)⇒ 派生中止时刻 =
        /// `动作 + CARE_GAP` = `currentTick + 400` > currentTick ⇒ 仍在照护中 ⇒ 不判死。</para>
        /// </summary>
        [Test]
        public void test_death_activeCare_resetsWindow_notDeceased()
        {
            long current = Onset + StateMachine.TREATMENT_WINDOW * 10;
            var care = new LastInterventionAggregator.CareChannelInput(
                lastCareStopTick: null,
                lastCareActionTick: current - 200);   // < CARE_GAP ⇒ 仍算连续照护

            var state = F4OutcomeEvaluator.Evaluate(
                ActorClass.Player, LethalPlateau(), Raw(StateMachine.DEATH_THRESHOLD),
                onsetTick: Onset, currentTick: current,
                LastInterventionAggregator.TreatmentChannelInput.None, care);

            Assert.AreNotEqual(InjuryState.Deceased, state,
                "照护支派生式 max(止, 动作+CARE_GAP) 须被 F4 消费");
        }

        // ══════════════════ ③ 昏迷支:无谓词门 ══════════════════

        /// <summary>
        /// ≥COMA 但 &lt; DEATH ⇒ Coma,**与致死性 / 自限性 / 实体类别全无关**。
        /// <para>这条是 GDD `:1091` 三次订正的机械 pin。加回任一门都会让它转红。</para>
        /// </summary>
        [Test]
        public void test_coma_hasNoPredicateGate()
        {
            var cases = new (string name, ActorClass cls, DiseaseRegistryEntry entry)[]
            {
                ("致死型 + 玩家",   ActorClass.Player,     LethalPlateau()),
                ("自限型 + 玩家",   ActorClass.Player,     SelfLimited()),
                ("非致死型 + 玩家", ActorClass.Player,     NonLethal()),
                ("致死型 + 敌人",   ActorClass.HumanEnemy, LethalPlateau()),
                ("致死型 + 兽",     ActorClass.Beast,      LethalPlateau()),
            };

            foreach (var c in cases)
            {
                var state = F4OutcomeEvaluator.Evaluate(
                    c.cls, c.entry, Raw(StateMachine.COMA_THRESHOLD),
                    onsetTick: Onset, currentTick: Onset);

                Assert.AreEqual(InjuryState.Coma, state,
                    $"{c.name}:≥COMA 即昏迷 —— 昏迷支无谓词门(GDD :1091)");
            }
        }

        /// <summary>恰在 COMA 边界 ⇒ Coma;下 1 raw ⇒ Critical。</summary>
        [Test]
        public void test_coma_boundaryIsInclusive()
        {
            Assert.AreEqual(InjuryState.Coma, F4OutcomeEvaluator.Evaluate(
                ActorClass.Player, NonLethal(), Raw(StateMachine.COMA_THRESHOLD), Onset, Onset));
            Assert.AreEqual(InjuryState.Critical, F4OutcomeEvaluator.Evaluate(
                ActorClass.Player, NonLethal(), Raw(StateMachine.COMA_THRESHOLD - 1), Onset, Onset));
        }

        // ══════════════════ ④ 低区间:纯阈值分级(与 ComputeTransition 同口径) ══════════════════

        /// <summary>
        /// `[0, CRITICAL)` 的分级须与 `StateMachine.ComputeTransition` **逐档一致**。
        /// <para>⚠️ 这不是「痊愈/迁延已实现」—— 见 `F4OutcomeEvaluator` 的未实现登记。</para>
        /// </summary>
        [Test]
        public void test_lowBands_matchComputeTransition()
        {
            int[] raws =
            {
                0, 1, StateMachine.MODERATE_THRESHOLD - 1, StateMachine.MODERATE_THRESHOLD,
                StateMachine.SEVERE_THRESHOLD - 1, StateMachine.SEVERE_THRESHOLD,
                StateMachine.CRITICAL_THRESHOLD - 1,
            };

            foreach (int raw in raws)
            {
                var viaF4 = F4OutcomeEvaluator.Evaluate(
                    ActorClass.Player, NonLethal(), Raw(raw), Onset, Onset);
                var viaTransition = StateMachine.ComputeTransition(
                    InjuryState.Healthy, Raw(raw), 0, 0, "x");

                Assert.AreEqual(viaTransition, viaF4,
                    $"raw={raw}:F4 与 ComputeTransition 在低区间须同口径");
            }
        }

        // ══════════════════ ⑤ last_intervention 聚合器 ══════════════════

        /// <summary>
        /// **哨兵**:三条通道全空 ⇒ 返回 `onset_tick`。
        /// <para>GDD `:1131-1134`「哨兵必须显式。缺了它…死亡判定整条短路」。
        /// 这是 F4 最需要覆盖的情形(从没被治过)。</para>
        /// </summary>
        [Test]
        public void test_lastIntervention_allChannelsEmpty_returnsOnsetTick()
        {
            long r = LastInterventionAggregator.Evaluate(
                onsetTick: Onset,
                LastInterventionAggregator.TreatmentChannelInput.None,
                LastInterventionAggregator.CareChannelInput.None);

            Assert.AreEqual(Onset, r, "三通道全空 ⇒ onset_tick 哨兵");
        }

        /// <summary>处置通道取 max(多条取最大 tick)。</summary>
        [Test]
        public void test_lastIntervention_treatmentChannel_takesMax()
        {
            long r = LastInterventionAggregator.Evaluate(
                onsetTick: Onset,
                new LastInterventionAggregator.TreatmentChannelInput(new long[] { 2000, 5000, 3000 }),
                LastInterventionAggregator.CareChannelInput.None);

            Assert.AreEqual(5000, r);
        }

        /// <summary>
        /// **照护支派生式(2026-10-22 第六轮裁定后)**:最后动作是「起」/ 走开 ⇒
        /// `最后动作 + CARE_GAP`。
        /// <para>🔁 原文(第五轮)写「`max(最后止, 最后动作 + CARE_GAP)`」—— 已作废,
        /// 该式使「止」项恒冗余(原文不删,见 `LastInterventionAggregator.Evaluate` 的 remarks)。</para>
        /// </summary>
        [Test]
        public void test_lastIntervention_careDerived_isActionPlusCareGap()
        {
            long r = LastInterventionAggregator.Evaluate(
                onsetTick: Onset,
                LastInterventionAggregator.TreatmentChannelInput.None,
                new LastInterventionAggregator.CareChannelInput(null, lastCareActionTick: 4000));

            Assert.AreEqual(4000 + StateMachine.CARE_GAP, r,
                "只有「起」、无止 ⇒ 宽限 = 最后动作 + CARE_GAP");
        }

        /// <summary>
        /// **最后动作是「止」⇒ 立刻起算,不再送 CARE_GAP**。
        /// <para>🔁 本条前身 = `test_lastIntervention_careStopLaterThanDerived_wins`
        /// (夹具 `止 9999 / 动作 4000`,断言取 9999)。该夹具在**新口径下结构性不可能** ——
        /// 「止」本身是一条护理动作 ⇒ `最后动作 ≥ 最后止` 恒成立,`止 9999 > 动作 4000`
        /// 描述的是一个不存在的状态。旧断言之所以过,恰恰是因为旧 `max` 式让「止」项恒被
        /// 第二项压过以外的分支永远走不到(即本测试当时**没在测它声称的东西**)。
        /// 改写为可构造的真实情形:最后一条动作就是「止」。</para>
        /// </summary>
        [Test]
        public void test_lastIntervention_careStop_endsWindowImmediately()
        {
            long r = LastInterventionAggregator.Evaluate(
                onsetTick: Onset,
                LastInterventionAggregator.TreatmentChannelInput.None,
                new LastInterventionAggregator.CareChannelInput(
                    lastCareStopTick: 9999, lastCareActionTick: 9999));

            Assert.AreEqual(9999, r,
                "最后动作是「止」(两值相等)⇒ last_care = 止本身,不得再送 CARE_GAP");
        }

        /// <summary>
        /// 最后动作是「起」(止更早)⇒ 从「起」宽限,不得回退到旧「止」。
        /// <para>🔁 本条前身 = `test_lastIntervention_careDerivedLaterThanStop_wins`
        /// (夹具 `止 4100 / 动作 4000`,断言 4600)。**数值恰好不变,但理由变了** ——
        /// 旧理由「派生值 4600 &gt; 止 4100 故取派生值」在新口径下无意义(不存在两个候选比较),
        /// 新理由 = 「最后动作是起 ⇒ 一律从它 + CARE_GAP」。保留本条是因为它钉住了
        /// **「停后再起」不得回退到旧止** 这一反例(散文原意做不到的那半)。</para>
        /// </summary>
        [Test]
        public void test_lastIntervention_careRestartAfterStop_usesNewAction()
        {
            long r = LastInterventionAggregator.Evaluate(
                onsetTick: Onset,
                LastInterventionAggregator.TreatmentChannelInput.None,
                new LastInterventionAggregator.CareChannelInput(
                    lastCareStopTick: 4100, lastCareActionTick: 4000));

            Assert.AreEqual(4000 + StateMachine.CARE_GAP, r,
                "最后动作是「起」4000(旧止 4100 更早)⇒ 从 4000 宽限;不得回退到旧止");
        }

        /// <summary>跨通道取 max(处置 vs 照护)。</summary>
        [Test]
        public void test_lastIntervention_crossChannel_takesMax()
        {
            long r = LastInterventionAggregator.Evaluate(
                onsetTick: Onset,
                new LastInterventionAggregator.TreatmentChannelInput(new long[] { 8000 }),
                new LastInterventionAggregator.CareChannelInput(null, lastCareActionTick: 4000));

            Assert.AreEqual(8000, r, "处置 8000 > 照护派生 4600 ⇒ 取 8000");
        }

        // ══════════════════ ⑥ 常量存在性 + 排序(校验 17) ══════════════════

        /// <summary>
        /// 三个此前**不存在**的常量现在存在且值正确。
        /// <para>⚠️ 这条防「常量被删掉但测试仍绿」—— 若删 `TREATMENT_WINDOW`,
        /// 本测试编译失败(引用它),这是**编译期**判据,比运行期断言更强。</para>
        /// </summary>
        [Test]
        public void test_constants_existWithAdjudicatedValues()
        {
            Assert.AreEqual(3600, StateMachine.TREATMENT_WINDOW, "TREATMENT_WINDOW 裁定值");
            Assert.AreEqual(600, StateMachine.CARE_GAP, "CARE_GAP 裁定值");
            Assert.AreEqual(7864, StateMachine.RECOVER_THRESHOLD, "RECOVER_THRESHOLD 裁定值");
        }

        /// <summary>GDD 校验 17 的排序约束 `TREATMENT_WINDOW ≥ CARE_GAP`。</summary>
        [Test]
        public void test_r1_17_treatmentWindowNotBelowCareGap()
        {
            Assert.IsTrue(F4OutcomeEvaluator.TreatmentWindowNotBelowCareGap,
                $"TREATMENT_WINDOW({StateMachine.TREATMENT_WINDOW}) 须 ≥ " +
                $"CARE_GAP({StateMachine.CARE_GAP}) —— 否则连续照护在窗口内被误判中止");
        }

        /// <summary>四阈值严格单调(校验 9 + 17 的联合)。</summary>
        [Test]
        public void test_thresholds_strictlyMonotonic()
        {
            Assert.Less(StateMachine.RECOVER_THRESHOLD, StateMachine.CRITICAL_THRESHOLD);
            Assert.Less(StateMachine.CRITICAL_THRESHOLD, StateMachine.COMA_THRESHOLD);
            Assert.Less(StateMachine.COMA_THRESHOLD, StateMachine.DEATH_THRESHOLD);
            Assert.Less(StateMachine.DEATH_THRESHOLD, Fix.OneRaw, "DEATH < 1.0(否则死亡不可达)");
        }

        // ══════════════════ ⑦ 入参守卫 ══════════════════

        /// <summary>`entry = null` ⇒ 抛(不静默返回某状态)。</summary>
        [Test]
        public void test_nullEntry_throws()
        {
            Assert.Throws<ArgumentNullException>(() => F4OutcomeEvaluator.Evaluate(
                ActorClass.Player, null, Raw(0), Onset, Onset));
        }

        /// <summary>
        /// `ActorClass.Unspecified` ⇒ 抛(fail-loud,不猜)。
        /// <para>⚠️ 本条在**低 position**下也须抛 —— 这是短路次序的判据:
        /// 若把阈值判定放在两门之前,Unspecified 会在低 position 静默返回 Mild。</para>
        /// </summary>
        [Test]
        public void test_unspecifiedActorClass_throwsEvenAtLowPosition()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => F4OutcomeEvaluator.Evaluate(
                ActorClass.Unspecified, LethalPlateau(), Raw(0), Onset, Onset),
                "Unspecified 须 fail-loud,不得因 position 低而静默通过");
        }
    }
}
