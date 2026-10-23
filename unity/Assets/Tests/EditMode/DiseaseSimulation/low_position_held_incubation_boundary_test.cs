// OQ-9-1 的**裁定后判据**(乙案)—— 2026-10-23 用户裁定,round40 落地。
//
// ── 裁定内容 ──────────────────────────────────────────────────────────────────
// `design/gdd/disease-simulation.md:2152` 的 **OQ-9-1** 问:`LowPositionHeldTicks` 的
// 起算点该是 `onset`(甲案)还是 `max(onset, onset + incubation)`(乙案)?
//
// **用户 2026-10-23 裁定:乙案。** 起算点 = `onset + incubation`。
//
// 裁定理由(裁定前实测所得,见本文件 §①–§③ 的判据):
//   潜伏期内 `τ < incubation` ⇒ `ComputeProgress` 恒返 0 ⇒ `position = 0 < RECOVER_THRESHOLD`
//   ⇒ 甲案下 `held` 会在**病一个体征都没显出来**时就攒满 `W`;而条件 ⑤(非上升期门)
//   在 `trend = 0` 时放行(`trend ≤ 0`)⇒ 判出「潜伏期痊愈」。
//   乙案消除这条路径。
//
// ── ⚠️ 乙案的具名代价(裁定当时已计入,不是事后发现)──────────────────────────
// GDD `:544` / `:567` 把「**潜伏 → 痊愈**」明列为**合法边**(「不经发病直接痊愈 ——
// 有些病自己会好」)。该边的触发条件 `self_limit = true ∧ 无 relapse.* 块`
// 与痊愈支条件 ②③④ **完全同源**,而潜伏期内 `trend` 恒 0、`held` 恒 0 < W
// ⇒ **乙案把这条边也一并关掉了**。
// ⇒ 今天**没有任何病种**能从潜伏期直接痊愈。若将来要恢复该边,须**另裁** ——
//   它同时受条件 ⑤ 与 ⑥ 约束,不是改 `ComputeLowPositionHeldTicks` 一处即可。
// 本文件 §⑤ 把这一点钉成判据(不是写在注释里就算)。
//
// ── 本文件的判据清单 ──────────────────────────────────────────────────────────
//   ① 起算点 = onset + incubation(潜伏期内恒 0,过 incubation 才开始计数)
//   ② 端到端:潜伏期内**不判愈**(甲案下会判愈的那个时刻)
//   ③ 对照:只差 incubation ⇒ 有潜伏期的不判愈(隔离出致因)
//   ④ W = 0 ⇒ 两支不求值(「今天不出事」的机制;边界本身仍在)
//   ⑤ 真表影响面 + **乙案关掉「潜伏 → 痊愈」边的具名证据**
//
// ── 覆盖不到(如实登记,禁借绿)─────────────────────────────────────────────────
// ① 本文件用**合成夹具**演示机制,并用**真表**点名影响面;它**不**证明
//    「生产里曾经判错过」—— 裁定前 `W = 0`(`assets/data/disease_registry.json:11`),
//    `IsConfigured == false` ⇒ 两支不求值 ⇒ 边界在**生产里从未触发过**。
//    这一点是 OQ-9-1 的「当下可观测性」,§④ 钉住它。
// ② 本文件**不**覆盖 `W` 的数值本身 —— 那归数值轮(GDD Tuning `:1743` 标 `*待定*`)。
// ③ 本文件**不**覆盖「潜伏 → 痊愈」边的恢复 —— 见上「具名代价」,须另裁。
//
// 权威来源:
//   `design/gdd/disease-simulation.md:2152-2184`(OQ-9-1 正文 + 甲/乙对照表)
//   `design/gdd/disease-simulation.md:1113-1114`(痊愈支条件 ⑥「低位须稳定 ≥ W」)
//   `design/gdd/disease-simulation.md:544` / `:567`(转移表:潜伏 → 痊愈,乙案关掉的那条边)
//   `design/gdd/disease-simulation.md:1743`(`W` *待定*,归数值轮)
//   `unity/Assets/Sim/ProgressionEvaluator.cs:302-320`(乙案的实现与代价注记)
//   `unity/Assets/Sim/F4OutcomeEvaluator.cs:204-209`(两支的 `IsConfigured` 门)

using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Presentation;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.DiseaseSimulation;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.DiseaseSimulation
{
    /// <summary>
    /// OQ-9-1 **乙案**的判据:潜伏期内 <see cref="ProgressionEvaluator.ComputeLowPositionHeldTicks"/>
    /// 不计数,故病发出来之前不会判出痊愈。
    /// </summary>
    public class LowPositionHeldIncubationBoundaryTest
    {
        private const long OnsetTick = 0;

        /// <summary>判据用的 <c>W</c>(tick)—— **不是设计值**,只须为偶数且 &lt; 夹具的 incubation。</summary>
        private const long W = 100;

        /// <summary>夹具的潜伏期(tick)—— 刻意远大于 W,使甲/乙的差别可观测。</summary>
        private const int Incubation = 600;

        // ══════════════════════════════════════════════════════════════════════
        // 夹具:自限型 + 有潜伏期(形状对齐真表的 DIS_MEASLES)
        // ══════════════════════════════════════════════════════════════════════
        //
        // 真表麻疹(`assets/data/disease_registry.json` id=11):scale=100 · a_peak=35 ·
        //   tau_rise=1200 · tau_peak=9000 · tau_fall=2000 · incubation=600 ·
        //   self_limit=true · plateau=false · lethal=false
        //   ⇒ 它是真表里**唯一**同时满足「有潜伏期」与「痊愈候选」的行(见 §⑤)。
        //
        // 本夹具用同一组形状参数,只把 incubation 参数化,以便 §③ 做「只差 incubation」的对照。

        /// <summary>
        /// 自限型 + 可配潜伏期。<paramref name="incubation"/> = 0 时与既有
        /// <c>low_position_held_test.cs</c> 的夹具同形,便于对照。
        /// </summary>
        private static DiseaseRegistryEntry IncubatingSelfLimit(int incubation)
        {
            return new DiseaseRegistryEntry
            {
                DiseaseId = 91,
                DiseaseKey = "DIS_OQ91_INCUBATION",
                Kind = DiseaseKind.Disease,
                Lethality = 0,                          // 非致死 ⇒ 不触发死亡支,判据面只剩痊愈支
                Scale = new Fix(100 * Fix.OneRaw),
                Curve = new NaturalProgressCurve
                {
                    Incubation = incubation,
                    APeak = new Fix(35 * Fix.OneRaw),
                    TauRise = new Fix(1200 * Fix.OneRaw),
                    TauPeak = 9000,
                    TauFall = new Fix(2000 * Fix.OneRaw),
                    Plateau = false,                    // 非豁免类(豁免类 = plateau ∧ ¬lethal)
                    SelfLimit = true,                   // 条件 ④ 可愈性谓词
                    BoundaryMode = "monotone",
                },
            };
        }

        private static long Held(DiseaseRegistryEntry entry, long currentTick, long w)
            => ProgressionEvaluator.ComputeLowPositionHeldTicks(
                entry, OnsetTick, currentTick,
                new List<TreatmentDose>(), worldSeed: 1, new PatientId(1),
                windowTicks: w);

        private static ProgressionResult Progress(DiseaseRegistryEntry entry, long tick)
            => ProgressionEvaluator.Evaluate(
                entry, OnsetTick, tick, new List<TreatmentDose>(), worldSeed: 1, new PatientId(1));

        /// <summary>跑一次完整 F4 —— 与生产同一条调用面(<c>DiseaseVitalsService</c> 的形态)。</summary>
        private static InjuryState Outcome(DiseaseRegistryEntry entry, long tick, long w)
        {
            ProgressionResult r = Progress(entry, tick);

            Fix position = r.Position / entry.Scale;
            if (position.Raw < Fix.ZeroRaw) position = Fix.Zero;
            if (position.Raw > Fix.OneRaw) position = Fix.One;

            // 后向趋势(与生产同一条调用:W 为入参,eps 取 0 = 无死区,让判据不依赖 eps)
            Fix trendBackward = ProgressionEvaluator.ComputeTrendBackward(
                entry, OnsetTick, tick, new List<TreatmentDose>(), worldSeed: 1, new PatientId(1),
                windowTicks: w, trendEpsRaw: 0);

            var window = new RecoveryWindowInput(w, 0, Held(entry, tick, w));

            return F4OutcomeEvaluator.Evaluate(
                ActorClass.Patient, entry, position, trendBackward, OnsetTick, tick,
                LastInterventionAggregator.TreatmentChannelInput.None,
                LastInterventionAggregator.CareChannelInput.None,
                window);
        }

        // ══════════════════════════════════════════════════════════════════════
        // ① 起算点 = onset + incubation(乙案,精确到 tick)
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// <b>核心判据</b>:潜伏期内 <c>held</c> **恒 0**;到 <c>onset + incubation + W − 1</c>
        /// 才**恰好**攒满 <c>W</c>。
        /// </summary>
        /// <remarks>
        /// <para><b>修前红</b>:甲案(起算点 = onset)下,<c>t = onset + W − 1</c> 处 held 已 = W
        /// ⇒ 第一条断言红。这是乙案落地的直接判据。</para>
        /// <para>判别力已由证伪轮实测:把扫描下限改回 <c>onsetTick</c> ⇒ 本测红。</para>
        /// </remarks>
        [Test]
        public void test_held_countsFromIncubationEnd_notFromOnset()
        {
            DiseaseRegistryEntry entry = IncubatingSelfLimit(Incubation);

            // 潜伏期内一律 0 —— 这是乙案的定义
            Assert.AreEqual(0L, Held(entry, OnsetTick, W),
                "t = onset(仍在潜伏期)⇒ held = 0");
            Assert.AreEqual(0L, Held(entry, OnsetTick + W - 1, W),
                $"t = onset + W − 1 = {W - 1}(τ = {W - 1} < incubation = {Incubation})⇒ held = 0。\n" +
                "甲案下此处 held 已 = W ⇒ 本断言就是甲/乙的分界线");
            Assert.AreEqual(0L, Held(entry, OnsetTick + Incubation - 1, W),
                "潜伏期最后一 tick ⇒ held 仍 = 0(边界精确:incubation − 1 仍算潜伏)");

            // 过 incubation 才开始计数,且精确到 tick
            Assert.AreEqual(1L, Held(entry, OnsetTick + Incubation, W),
                "t = onset + incubation ⇒ 恰有 1 个可计 tick(起点本身)");
            Assert.AreEqual(W, Held(entry, OnsetTick + Incubation + W - 1, W),
                $"t = onset + incubation + W − 1 ⇒ 恰好攒满 W = {W}");
            Assert.AreEqual(W - 1, Held(entry, OnsetTick + Incubation + W - 2, W),
                "再往前一 tick 少一个 —— 证明是「恰好」攒满,不是提前就满(排除 off-by-one 误读)");
        }

        // ══════════════════════════════════════════════════════════════════════
        // ② 端到端:潜伏期内**不判愈**(甲案下会判愈的那个时刻)
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 甲案会判出「潜伏期痊愈」的那个时刻(<c>t = onset + W − 1</c>,τ = 99 ≪ 600),
        /// 乙案下**不判愈**。
        /// </summary>
        /// <remarks>
        /// <b>修前红</b>:甲案下此处返回 <see cref="InjuryState.Recovered"/>(六条件全过)⇒ 本测红。
        /// </remarks>
        [Test]
        public void test_incubationLongerThanWindow_notRecoveredDuringIncubation()
        {
            DiseaseRegistryEntry entry = IncubatingSelfLimit(Incubation);
            const long t = OnsetTick + W - 1;      // τ = 99

            Assert.Less(t - OnsetTick, entry.Curve.Incubation,
                "前提:求值时刻仍在潜伏期内(τ < incubation)");

            Assert.AreNotEqual(InjuryState.Recovered, Outcome(entry, t, W),
                $"τ = {t - OnsetTick} < incubation = {Incubation} 时**不得**判出 Recovered。\n" +
                "甲案下此处六条件全过 ⇒ 判愈(OQ-9-1 描述的「潜伏期痊愈」)。乙案消除该路径");
        }

        // ══════════════════════════════════════════════════════════════════════
        // ③ 对照:只差 incubation ⇒ 隔离出真正的致因
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 同一曲线、同一时刻,**只把 <c>incubation</c> 从 600 改成 0** ⇒ 两者都不判愈,
        /// 但**原因不同** —— 有潜伏期是被 ⑥ 挡住(<c>held = 0</c>),无潜伏期是被 ⑤ 挡住
        /// (<c>trend &gt; 0</c>)。
        /// </summary>
        /// <remarks>
        /// <para>这一条是 ② 的**归因判据**:它把「乙案挡住它」与「曲线参数凑巧挡住它」
        /// 分开 —— 两条路径的机制在断言里各自具名。</para>
        /// </remarks>
        [Test]
        public void test_control_sameCurveWithoutIncubation_blockedByDifferentCondition()
        {
            DiseaseRegistryEntry withIncubation = IncubatingSelfLimit(Incubation);
            DiseaseRegistryEntry withoutIncubation = IncubatingSelfLimit(incubation: 0);
            const long t = OnsetTick + W - 1;

            // 有潜伏期:held = 0 ⇒ 条件 ⑥ 挡住(这是乙案的机制)
            Assert.AreEqual(0L, Held(withIncubation, t, W),
                "有潜伏期 ⇒ 潜伏期内 held = 0 ⇒ 条件 ⑥(`held < W`)挡住 —— 乙案的机制");

            // 无潜伏期:held 攒得起来,但 trend > 0 ⇒ 条件 ⑤ 挡住
            Assert.Greater(Held(withoutIncubation, t, W), 0L,
                "无潜伏期 ⇒ 同刻 held > 0(已开始计数)");
            Fix trendWithout = ProgressionEvaluator.ComputeTrendBackward(
                withoutIncubation, OnsetTick, t, new List<TreatmentDose>(), 1, new PatientId(1),
                windowTicks: W, trendEpsRaw: 0);
            Assert.Greater(trendWithout.Raw, 0L,
                "无潜伏期 ⇒ 同刻已在上升段 ⇒ trend > 0 ⇒ 条件 ⑤ 挡住");

            // 结论:两者都不判愈,但挡它们的是不同的条件
            Assert.AreNotEqual(InjuryState.Recovered, Outcome(withIncubation, t, W),
                "有潜伏期 ⇒ 不判愈(被 ⑥ 挡住)");
            Assert.AreNotEqual(InjuryState.Recovered, Outcome(withoutIncubation, t, W),
                "无潜伏期 ⇒ 不判愈(被 ⑤ 挡住)—— 归因因此唯一");
        }

        // ══════════════════════════════════════════════════════════════════════
        // ④ W = 0 ⇒ 两支不求值(「裁定前生产里从未触发过」的机制)
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// <c>W = 0</c> ⇒ <see cref="RecoveryWindowInput.IsConfigured"/> 为 <c>false</c>
        /// ⇒ 两支根本不求值。
        /// </summary>
        /// <remarks>
        /// <para><b>这条钉的是「裁定前不出事」的机制</b>:OQ-9-1 原文说本边界
        /// 「在生产里不可触发」,靠的就是 <c>W = 0</c>。它与 ①② 合起来才是准确状态:
        /// **边界是真的,只是当时被 `W = 0` 关着**。</para>
        /// <para>⚠️ 现在边界已按乙案修好,**但 `W` 仍是 0**(归数值轮)⇒ 两支依然不求值。
        /// 「修好了」与「生效了」是两件事,本条钉住后者尚未发生。</para>
        /// <para>⚠️ <b>取时刻的坑(实测踩过)</b>:反向对照**不能**取潜伏刚过的时刻
        /// (<c>onset + incubation + W − 1</c>)—— 那里 <c>trend &gt; 0</c> 且
        /// <c>τ ≤ τ_peak</c> ⇒ 条件 ⑤ **本来就该拦住它**,与 <c>W</c> 无关。
        /// 用它做对照会得到「W 打开也不判愈」,从而**误判成实现坏了**。
        /// ⇒ 必须取**过峰之后的衰减段**:那里 position &lt; RECOVER 且 trend ≤ 0,
        /// 六条件里只有 ⑥ 受 <c>W</c> 控制。</para>
        /// </remarks>
        [Test]
        public void test_windowZero_bothBranchesNotEvaluated()
        {
            DiseaseRegistryEntry entry = IncubatingSelfLimit(Incubation);

            // 过峰之后的衰减段:τ = 12000 ≫ τ_peak = 9000,position 已衰减到 RECOVER 之下
            const long t = 12000;

            // 前提:此刻确实处于「低位 + 非上升」—— 六条件里只剩 ⑥ 受 W 控制
            ProgressionResult r = Progress(entry, t);
            Fix position = r.Position / entry.Scale;
            Assert.Less(position.Raw, StateMachine.RECOVER_THRESHOLD,
                "前提:过峰衰减段 position < RECOVER(条件 ① 成立)");
            Fix trend = ProgressionEvaluator.ComputeTrendBackward(
                entry, OnsetTick, t, new List<TreatmentDose>(), 1, new PatientId(1),
                windowTicks: W, trendEpsRaw: 0);
            Assert.LessOrEqual(trend.Raw, 0L,
                "前提:衰减段 trend ≤ 0(条件 ⑤ 放行)—— 否则本对照测的是 ⑤ 不是 ⑥");

            var window = new RecoveryWindowInput(windowTicks: 0, trendEpsRaw: 0,
                                                lowPositionHeldTicks: Held(entry, t, 0));
            Assert.IsFalse(window.IsConfigured, "W = 0 ⇒ 未配置");

            Assert.AreNotEqual(InjuryState.Recovered, Outcome(entry, t, 0),
                "W = 0 ⇒ 两支不求值 ⇒ 不判愈。\n" +
                "⚠️ 这不是「边界不存在」—— 是「W 未配置」。W 填上即生效");

            // 反向对照:同一时刻把 W 打开 ⇒ 立刻判愈(证明关它的只有 W,不是别的)
            Assert.AreEqual(InjuryState.Recovered, Outcome(entry, t, W),
                "同一夹具、同一时刻,只把 W 从 0 改成 100 ⇒ 立刻判愈 —— 边界本身是活的");
        }

        // ══════════════════════════════════════════════════════════════════════
        // ⑤ 真表影响面 + 乙案关掉「潜伏 → 痊愈」边的具名证据
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 从**盘上真产物**读出「痊愈候选 ∧ 有潜伏期」的行 —— 这些就是乙案影响到的行。
        /// </summary>
        /// <remarks>
        /// <para><b>痊愈候选</b> = 痊愈支条件 ②③④ 全过 = 无 <c>relapse</c>
        /// ∧ ¬(plateau ∧ ¬lethal) ∧ <c>self_limit</c>。</para>
        /// <para><b>为什么断言麻疹在集合里</b>:它是当前真表里**唯一**同时满足「有潜伏期」
        /// 与「痊愈候选」的行。这条是**绊线** —— 若将来给别的行加 <c>incubation</c>,本条会红,
        /// 提醒「OQ-9-1 的影响面变了」,须重新评估。**它不是「麻疹必须永远这样」的断言**。</para>
        /// </remarks>
        [Test]
        public void test_premise_realRegistry_namesRowsAffectedByYiCase()
        {
            DiseaseRegistryDataSet table = DataCookedDisk.LoadDiseaseRegistry();

            var atRisk = new List<string>();
            for (int i = 0; i < table.Rows.Count; i++)
            {
                DiseaseRegistryEntry e = table.Rows[i];
                if (e.Curve == null || e.Curve.Incubation <= 0) continue;

                bool recoverable = e.Relapse == null
                                   && !(e.Curve.Plateau && e.Lethality == 0)   // 非豁免类
                                   && e.Curve.SelfLimit;                       // 可愈性谓词
                if (recoverable) atRisk.Add($"{e.DiseaseKey}(id={e.DiseaseId}, incubation={e.Curve.Incubation})");
            }

            string atRiskJoined = string.Join(" / ", atRisk);
            Assert.That(atRisk, Is.Not.Empty,
                "真表里若**一行都没有**,OQ-9-1 的影响面 = 0(纯理论问题);" +
                $"实际有行 ⇒ 它是**具体**风险。当前扫描:{table.Rows.Count} 行,命中 {atRisk.Count} 行");

            Assert.That(atRiskJoined, Does.Contain("DIS_MEASLES"),
                "DIS_MEASLES 是当前真表里唯一的「有潜伏期 ∧ 痊愈候选」行 —— " +
                "若它不在此列,说明数据变了,须重新评估 OQ-9-1 的影响面。" +
                $"实际命中:{atRiskJoined}");
        }

        /// <summary>
        /// <b>乙案具名代价的判据</b>:GDD <c>:567</c> 的「潜伏 → 痊愈」边**当前不可达**。
        /// </summary>
        /// <remarks>
        /// <para>该边的触发条件(<c>self_limit ∧ ¬relapse</c>)与痊愈支条件 ②③④ 同源,
        /// 故本条用**真表麻疹**直接跑:它在 <c>t = onset + incubation</c>(潜伏刚结束、
        /// position 仍 &lt; RECOVER)处,**不会**被判愈 —— 因为 <c>held = 1 &lt; W</c>。</para>
        /// <para>⚠️ 本条钉的是**代价**,不是**期望**。若将来裁定恢复该边,本条须改写为
        /// 「该时刻应判愈」,不是删掉(与 round31 → round39 的缺口文件同一处置)。</para>
        /// </remarks>
        [Test]
        public void test_cost_latentToRecoveredEdgeIsCurrentlyUnreachable()
        {
            DiseaseRegistryEntry entry = IncubatingSelfLimit(Incubation);

            // 潜伏刚结束的那一刻:position 仍 < RECOVER(曲线刚从 0 起步)
            const long t = OnsetTick + Incubation;
            ProgressionResult r = Progress(entry, t);
            Fix position = r.Position / entry.Scale;

            Assert.Less(position.Raw, StateMachine.RECOVER_THRESHOLD,
                "前提:潜伏刚结束时 position 仍 < RECOVER —— 这条边的前提成立");
            Assert.AreEqual(1L, Held(entry, t, W),
                "held = 1(只有起点这一个 tick)");
            Assert.Less(1L, W,
                "held < W ⇒ 条件 ⑥ 不成立");

            Assert.AreNotEqual(InjuryState.Recovered, Outcome(entry, t, W),
                "⇒ 「潜伏 → 痊愈」边(GDD :567)**当前不可达**。\n" +
                "这是乙案的具名代价,裁定当时已计入;恢复该边须另裁(它同时受条件 ⑤ 与 ⑥ 约束)");
        }
    }
}
