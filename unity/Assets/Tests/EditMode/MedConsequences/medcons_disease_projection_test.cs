// ADR-036 · S1 —— D 路真值:`DiseaseProjectionAdapter`(9 的九态 → 53 的痊愈位)
//
// 权威:ADR-036 §Decision D1(9 扩投影形状 · 实现住边界层)· §Risks R1(痊愈判据在,
//       生产被 `trend_window_ticks = 0` 挡住)· §Risks R4(次序陷阱:`GetOutcome` 只给当前态)
//       卡面:production/epics/medcons/story-*.md §V-1 / V-6
//
// ── 本文件钉什么 ─────────────────────────────────────────────────────────────
// ① 适配器**如实转发** 9 的九态:痊愈 ⇒ `Recovered = true`,其余八态 ⇒ `false`;
// ② 未建档病人 ⇒ `TryProject = false`(正常退化,**不是**错误);
// ③ 问**非当前 tick** ⇒ 抛(而不是把历史时刻静默答成「此刻」—— R4 的唯一防线);
// ④ 快照的 `Tick` 恒等于被问的 tick(即当前 tick)。
//
// ── 为什么 ③ 是**必需**的,而不是过度防御 ────────────────────────────────────
// `DiseaseVitalsService` 只保留 `_outcomes` 的**当前**值。若适配器接受任意 tick,
// 调用方拿 tick=0 去问「他当时是否已痊愈」会得到 `_lastTick` 的答案 —— **静默错答**,
// 且错的方向恰好是「把旧病例判成新病况」。故 fail-loud。
//
// ── 真装配,零 Fake 被测对象 ─────────────────────────────────────────────────
// 与 `death_candidate_sink_test` 同款:`CompositionRoot.Assemble` → 真 `EventStream` +
// 真 `PayloadEncoder` + 真 `PatientSpawner`。被测对象 = `DiseaseVitalsService`(9)
// 与 `DiseaseProjectionAdapter`(装配层),**两者都是真的**。

using System;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Consequences;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.DiseaseSimulation;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.MedConsequences
{
    /// <summary>ADR-036 S1 —— D 路真值适配器。</summary>
    [TestFixture]
    public sealed class MedConsDiseaseProjectionTest
    {
        // ══════════════════════════════════════════════════════════════════
        // 夹具
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 自限型病种 —— 与 `care_lever_and_aggregator_test.SelfLimited` 同形。
        /// <para>`SelfLimit = true` ⇒ F4 死亡支的实体门 ② 直接关闭 ⇒ 不会在痊愈前死掉,
        /// 本条链的终态**只能**是痊愈或迁延/危殆。</para>
        /// </summary>
        private static DiseaseRegistryEntry SelfLimited(int diseaseId, string key)
        {
            return new DiseaseRegistryEntry
            {
                DiseaseId = diseaseId,
                DiseaseKey = key,
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

        /// <summary>稳定窗 W = 100 tick · TREND_EPS = 1 LSB(与 9 侧夹具同值)。</summary>
        private const long WindowTicks = 100;
        private const long TrendEpsRaw = 1;

        /// <summary>一条真装配的体征链(稳定窗**已配置**)+ 单病人 + 其适配器。</summary>
        private sealed class Rig
        {
            public DiseaseVitalsService Svc;
            public DiseaseProjectionAdapter Adapter;
            public PatientId Patient;
        }

        /// <summary>
        /// 建链。`configuredWindow = false` ⇒ 传 0/0,**复刻生产现状**(`disease_registry.json`
        /// 的 `trend_window_ticks = 0`)⇒ 痊愈支不求值。
        /// </summary>
        private static Rig Open(long diseaseId = 91, bool configuredWindow = true)
        {
            DiseaseRegistryEntry entry = SelfLimited((int)diseaseId, "DIS_PROJ_SELF_LIMIT");
            CompositionRootServices bag = CompositionRoot.Assemble(new[] { entry });

            var svc = new DiseaseVitalsService(
                bag.Stream, bag.BlobPool, new[] { entry }, 0UL,
                trendWindowTicks: configuredWindow ? WindowTicks : 0,
                trendEpsRaw: configuredWindow ? TrendEpsRaw : 0);

            return new Rig
            {
                Svc = svc,
                Adapter = new DiseaseProjectionAdapter(svc),
                Patient = bag.PatientSpawner.SpawnNext(entry.DiseaseId, tick: 0),
            };
        }

        // ══════════════════════════════════════════════════════════════════
        // ② 未建档 ⇒ false(正常退化)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 病人**已建在流里**但服务尚未驱动过一次 ⇒ `_outcomes` 无该病人 ⇒ `false`。
        /// <para>为什么不用「随便一个 id」:那测的是「id 不存在」,而这里测的是
        /// 「**已知病人但无投影**」—— 后者才是 53 在 tick 边沿首次拿到新病人时会遇到的形态。</para>
        /// </summary>
        [Test]
        public void test_knownPatientNotYetDriven_returnsFalse()
        {
            Rig rig = Open();

            bool ok = rig.Adapter.TryProject(rig.Patient, rig.Svc.LastTick, out DiseaseProjectionSnapshot snap);

            Assert.IsFalse(ok, "未驱动 ⇒ 无投影 ⇒ false(正常退化,不得抛)");
            Assert.AreEqual(0L, snap.Tick, "退化路径写出的快照为 default");
            Assert.IsFalse(snap.Recovered, "default 的痊愈位为 false");
        }

        /// <summary>驱动之后**另一个**未知 id 仍为 false —— 「有投影」不得退化成「恒有」。</summary>
        [Test]
        public void test_unknownPatient_afterDriving_stillReturnsFalse()
        {
            Rig rig = Open();
            Drive(rig, 5);

            Assert.IsFalse(
                rig.Adapter.TryProject(new PatientId(424242), rig.Svc.LastTick,
                    out DiseaseProjectionSnapshot snap),
                "未知病人恒 false");
            Assert.IsFalse(snap.Recovered);
        }

        // ══════════════════════════════════════════════════════════════════
        // ③ 未来 tick ⇒ 抛(历史 tick ⇒ 按转移历史作答)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 问**未来** tick 必须抛,且抛的是 9 侧自己的报文 —— 只断「抛了某种异常」会被
        /// 任何更深的同类型异常冒充(<c>.claude/rules</c> 的假绿形态之一)。
        /// <para>🔁 **2026-10-30 二轮(用户裁定「9 加状态转移历史」)**:初版本条断言的是
        /// 「**任何**非当前 tick 都抛」。实现 driver 时实测该口径接不上
        /// (`Aggregate` 按 `close.Tick` 取 D,延迟窗内恒早于当前)⇒ 现收窄为**只对未来抛**。</para>
        /// </summary>
        [Test]
        public void test_futureTick_throwsWithOwnMessage()
        {
            Rig rig = Open();
            Drive(rig, 7);   // LastTick = 7

            var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
                rig.Adapter.TryProject(rig.Patient, 8, out _));

            StringAssert.Contains("未来时刻", ex.Message,
                "须是 9 侧的 fail-loud 报文,不是下游偶然抛出的同类型异常");
            StringAssert.Contains("8", ex.Message, "报文须带上被问的 tick");
            StringAssert.Contains("7", ex.Message, "报文须带上当前 tick");
        }

        /// <summary>
        /// **历史** tick 不抛,且答案是**当时**的态 —— 三条一起证明「回溯」不是「恒给此刻」:
        /// ① 结案前(未发病那段)⇒ 无投影(<c>false</c>);
        /// ② 病程中 ⇒ 有投影且 <c>Recovered = false</c>;
        /// ③ 快照的 <c>Tick</c> = 被问的 tick(**不是**当前 tick)。
        /// </summary>
        [Test]
        public void test_historicalTick_answersThatTick_notCurrent()
        {
            Rig rig = Open();
            Drive(rig, 7);
            long now = rig.Svc.LastTick;

            // ① 病人出现于 tick 0,首次求值在 tick 1 ⇒ 问 tick 0 无历史可答。
            Assert.IsFalse(rig.Adapter.TryProject(rig.Patient, 0, out _),
                "该时刻尚未求值过 ⇒ false(正常退化)");

            // ② 问 tick 3(病程中)⇒ 有投影,且 tick 字段 = 3 而非 now。
            bool ok = rig.Adapter.TryProject(rig.Patient, 3, out DiseaseProjectionSnapshot snap);
            Assert.IsTrue(ok, "历史时刻可答(结案 tick 回溯的唯一路径)");
            Assert.AreEqual(3L, snap.Tick, "快照 tick 须是被问的时刻,不是当前 tick");
            Assert.AreNotEqual(now, snap.Tick, "回溯不得退化成「此刻」");
            Assert.IsFalse(snap.Recovered, "病程中 ⇒ 未痊愈");
        }

        // ══════════════════════════════════════════════════════════════════
        // ① 如实转发九态 —— 两个方向各一条(防「恒 false」的假绿)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>病程进行中 ⇒ `Recovered = false`,且 `Tick` = 当前 tick。</summary>
        [Test]
        public void test_notYetRecovered_mapsToFalse()
        {
            Rig rig = Open();
            Drive(rig, 5);

            Assert.AreNotEqual(InjuryState.Recovered, rig.Svc.GetOutcome(rig.Patient),
                "前提到位:5 tick 时病程尚未走完");

            bool ok = rig.Adapter.TryProject(rig.Patient, rig.Svc.LastTick, out DiseaseProjectionSnapshot snap);
            Assert.IsTrue(ok);
            Assert.IsFalse(snap.Recovered, "非痊愈态须映射为 false");
            Assert.AreEqual(rig.Svc.LastTick, snap.Tick, "快照 tick 须 = 当前 tick");
        }

        /// <summary>
        /// 痊愈态 ⇒ `Recovered = true`。
        /// <para>这条**反证上一条不是恒真**:同一个适配器、同一条链,只差驱动时长。
        /// 若适配器被写成「恒 false」,本条红;被写成「恒 true」,上条红。</para>
        /// </summary>
        [Test]
        public void test_recovered_mapsToTrue()
        {
            Rig rig = Open();
            long recoveredAt = DriveUntilRecovered(rig, cap: 4000);

            Assert.Greater(recoveredAt, 0, "前提到位:该病种 + 已配置稳定窗 ⇒ 必须能走到痊愈(否则本测无输入)");

            bool ok = rig.Adapter.TryProject(rig.Patient, rig.Svc.LastTick, out DiseaseProjectionSnapshot snap);
            Assert.IsTrue(ok);
            Assert.IsTrue(snap.Recovered, "痊愈态须映射为 true");
            Assert.AreEqual(rig.Svc.LastTick, snap.Tick);
        }

        /// <summary>
        /// **生产现状**:稳定窗未配置(`0/0`)⇒ 痊愈支不求值 ⇒ 同一条链驱动同样多 tick,
        /// 终态**不是**痊愈,适配器如实给 `false`。
        /// <para>这条钉的是 ADR-036 §Risks R1 的**可达性边界** ——「S1 已落」≠「`Recovered`
        /// 6 格已可用」。若哪天数值轮把 `trend_window_ticks` 配置上,本条会**转红**,
        /// 而那正是应当转红的信号(届时须同批更新 R1 与判定表的可达性说明)。</para>
        /// </summary>
        [Test]
        public void test_productionWindowUnconfigured_recoveryBranchIsNotEvaluated()
        {
            Rig rig = Open(configuredWindow: false);
            Drive(rig, 800);   // 与上一条的驱动量同量级(上条在 800 内已痊愈)

            Assert.AreNotEqual(InjuryState.Recovered, rig.Svc.GetOutcome(rig.Patient),
                "窗口未配置 ⇒ 痊愈支不判 ⇒ 同一条链走不到痊愈(ADR-036 §Risks R1 的可达性边界)");

            bool ok = rig.Adapter.TryProject(rig.Patient, rig.Svc.LastTick, out DiseaseProjectionSnapshot snap);
            Assert.IsTrue(ok, "仍有投影 —— 「不痊愈」不等于「无投影」");
            Assert.IsFalse(snap.Recovered, "如实转发:未求值即未痊愈(接口只有两态)");
        }

        // ══════════════════════════════════════════════════════════════════
        // 有界性 —— 转移历史按「状态变化」增长,不按 tick
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 驱动 60 tick 后,转移历史条数仍须 ≤ 九态数(**按变化计,不按 tick 计**)。
        /// <para><b>钉什么</b>:若有人把 `RecordOutcomeTransition` 退回「每 tick 追加一条」,
        /// 条数会 ≈ 60 ⇒ 红。`TryGetOutcomeAt` 的**返回值**在那样的实现下依然正确
        /// (取最后一条),唯一症状是内存 ⇒ 只能靠 <see cref="DiseaseVitalsService.OutcomeTransitionCount"/> 观测。</para>
        /// <para>⚠️ **同 tick 覆盖守卫的另一半(多病种病人)今日零输入可达**:
        /// `DiseaseCourseBook` 拒绝同一病人第二条 `DiseaseOnset`(ADR-030 ① 甲案「病人出现是
        /// 单次事件」)⇒ `StepAll` 不会在同一 tick 对同一病人记两次。
        /// 该守卫按「将来多病种落地」保留,**如实登记为零输入可达**,不宣称已被夹具钉住。</para>
        /// </summary>
        [Test]
        public void test_transitionHistory_growsByStateChange_notByTick()
        {
            Rig rig = Open();
            for (long t = 1; t <= 60; t++) rig.Svc.OnTickEdge(t);

            Assert.LessOrEqual(rig.Svc.OutcomeTransitionCount(rig.Patient), 9,
                "转移历史须按状态变化增长(≤ 九态数)—— 超过说明退回了「每 tick 一条」");
            Assert.GreaterOrEqual(rig.Svc.OutcomeTransitionCount(rig.Patient), 1,
                "至少留下首次求值那一条(否则历史面根本没在工作)");
        }

        // ══════════════════════════════════════════════════════════════════
        // 构造闸
        // ══════════════════════════════════════════════════════════════════

        /// <summary>null 服务 ⇒ 构造期抛(而不是在第一次 `TryProject` 时空引用)。</summary>
        [Test]
        public void test_nullVitals_throwsAtConstruction()
        {
            Assert.Throws<System.ArgumentNullException>(() => new DiseaseProjectionAdapter(null));
        }

        // ══════════════════════════════════════════════════════════════════
        // 驱动器
        // ══════════════════════════════════════════════════════════════════

        private static void Drive(Rig rig, long ticks)
        {
            for (long t = 1; t <= ticks; t++) rig.Svc.OnTickEdge(t);
        }

        /// <summary>逐 tick 驱动到痊愈(或到上限)。返回首次观测到痊愈的 tick,未达则 0。</summary>
        private static long DriveUntilRecovered(Rig rig, long cap)
        {
            for (long t = 1; t <= cap; t++)
            {
                rig.Svc.OnTickEdge(t);
                if (rig.Svc.GetOutcome(rig.Patient) == InjuryState.Recovered) return t;
            }

            return 0;
        }
    }
}
