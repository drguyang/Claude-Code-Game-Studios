// #21 —— 24 病人 × 20 Hz 求值不超 tick 预算(L_eval ≤ 50 ms)
//
// 权威:
//   `technical-preferences.md`「tick 频率 = 20 Hz ⇒ TICK_SECONDS = 0.05」;
//   `OQ-8` 裁定(2026-09-20):模拟范围 = 在场才模拟 · `PATIENT_APPEARANCE_CAP = 24`;
//   `ADR-005`(Step 由 `ITickProvider` 驱动)· `disease-simulation` F1 求值。
//
// ── 本文件钉什么 ────────────────────────────────────────────────────────────
// 同场 **24 个病人**(CAP 满额)时,**单次 tick 求值**不超过 50 ms。
//
// ── 为什么这条必须自带「前置断言」 ──────────────────────────────────────────
// 纯计时断言有个致命形态:**如果病人根本没建起来 / 求值根本没跑,循环会快得离谱而转绿**
// (假绿形态 F4「空洞正控」)。⇒ 计时前先断「确实有 24 个不同病人」,
// 计时后把**实测毫秒数**写进断言报文 —— 绿的时候也看得见跑了多久。
//
// ── 关于阈值与机器差异 ─────────────────────────────────────────────────────
// 50 ms 是 `tick_period` 本身(20 Hz),即「求值必须在一个 tick 内跑完」。
// 本条**不是**跨机器的绝对性能门(那归 ADR-012 的三格矩阵 / `TD-ci-01`),
// 而是「本机今天没有把 tick 预算吃掉」的回归哨兵 —— 一旦某次改动把它顶破,
// 先看到的是这里,而不是实机上的卡顿。

using System.Collections.Generic;
using System.Diagnostics;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.DiseaseSimulation;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.DiseaseSimulation
{
    /// <summary>24 病人满额时的单 tick 求值预算(`L_eval ≤ 50 ms`)。</summary>
    [TestFixture]
    public sealed class PatientCapTickBudgetTest
    {
        /// <summary>tick 周期(20 Hz)。**唯一出处** = `technical-preferences.md` 的 OQ-25-8 裁定。</summary>
        private const double TickBudgetMs = 50.0;

        /// <summary>预热 tick 数 —— 把 JIT / 首帧分配排除在计时窗口外。</summary>
        private const int WarmupTicks = 20;

        /// <summary>计时 tick 数。</summary>
        private const int MeasuredTicks = 100;

        /// <summary>自限型病种(与 `medcons_disease_projection_test` 同形,便于对照)。</summary>
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

        [Test]
        public void test_cap24_singleTickEvaluation_under50ms()
        {
            DiseaseRegistryEntry entry = SelfLimited(91, "DIS_BUDGET_SELF_LIMIT");
            CompositionRootServices bag = CompositionRoot.Assemble(new[] { entry });

            var svc = new DiseaseVitalsService(
                bag.Stream, bag.BlobPool, new[] { entry }, 0UL,
                trendWindowTicks: 100, trendEpsRaw: 1);

            // ── 前置断言(防空洞正控):确实建起 24 个**不同**病人 ──
            var ids = new List<PatientId>(EventStream.PATIENT_APPEARANCE_CAP);
            for (int i = 0; i < EventStream.PATIENT_APPEARANCE_CAP; i++)
                ids.Add(bag.PatientSpawner.SpawnNext(diseaseId: entry.DiseaseId, tick: 0));

            var distinct = new HashSet<int>();
            foreach (PatientId p in ids) distinct.Add(p.Value);
            Assert.That(distinct.Count, Is.EqualTo(EventStream.PATIENT_APPEARANCE_CAP),
                $"前置不成立:期望 {EventStream.PATIENT_APPEARANCE_CAP} 个不同病人,实得 {distinct.Count} " +
                "—— 病人没建起来时本测的计时毫无意义(会以超快速度假绿)");

            Assert.That(EventStream.PATIENT_APPEARANCE_CAP, Is.EqualTo(24),
                "CAP 与 OQ-8 裁定不符(24)—— 该值若被改,本测的「满额」前提须同步复核");

            // ── 预热 ──
            for (int t = 1; t <= WarmupTicks; t++) svc.OnTickEdge(t);

            // ── 第二道前置断言:求值**真的发生了**(否则计时只量到一个空循环) ──
            //    「病人建在流里」≠「服务求值过他们」。用 D 路投影逐个问一遍:
            //    24 个都拿到投影 ⇒ 求值确实覆盖满额 CAP。
            var adapter = new DiseaseProjectionAdapter(svc);
            int projected = 0;
            foreach (PatientId p in ids)
                if (adapter.TryProject(p, WarmupTicks, out _)) projected++;
            Assert.That(projected, Is.EqualTo(EventStream.PATIENT_APPEARANCE_CAP),
                $"前置不成立:只有 {projected}/{EventStream.PATIENT_APPEARANCE_CAP} 个病人拿得到投影 " +
                "—— 求值没覆盖满额时,下面的计时测的是空转");

            // ── 计时:逐 tick 量,取**最大**单 tick(平均会把尖峰盖掉) ──
            var sw = new Stopwatch();
            double maxMs = 0.0, sumMs = 0.0;
            long worstTick = -1;

            for (int i = 0; i < MeasuredTicks; i++)
            {
                long tick = WarmupTicks + 1 + i;
                sw.Restart();
                svc.OnTickEdge(tick);
                sw.Stop();

                double ms = sw.Elapsed.TotalMilliseconds;
                sumMs += ms;
                if (ms > maxMs) { maxMs = ms; worstTick = tick; }
            }

            double avgMs = sumMs / MeasuredTicks;

            // 绿也要看得见数:把实测写进测试输出(结果件里可读),否则「通过」无法区分
            // 「真的跑了 0.2 ms」与「什么都没跑所以 0.000 ms」。
            TestContext.WriteLine(
                $"[#21] 24 病人 × {MeasuredTicks} tick:max {maxMs:F3} ms · avg {avgMs:F3} ms " +
                $"(预算 {TickBudgetMs:F1} ms)");

            Assert.That(maxMs, Is.LessThanOrEqualTo(TickBudgetMs),
                $"24 病人单 tick 求值超出预算:{maxMs:F3} ms(最差 tick={worstTick})," +
                $"均值 {avgMs:F3} ms,预算 {TickBudgetMs:F1} ms(= 20 Hz 的 tick 周期)。" +
                "≥ 预算 = 求值跑不完一个 tick ⇒ 须优化或另开 ADR 改 tick 频率(不得单系统自行调频)");
        }
    }
}
