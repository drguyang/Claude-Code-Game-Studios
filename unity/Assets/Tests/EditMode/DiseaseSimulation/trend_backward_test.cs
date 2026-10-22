// `trend_backward` —— GDD disease-simulation.md:921 的状态转移用后向差分(2026-10-22 第四轮立)。
//
// 权威来源:
//   GDD disease-simulation.md:921 —— `trend_backward(t) = (Progress(t) − Progress(t−W)) / W`
//   GDD disease-simulation.md:922 —— `trend_backward_agg = max over d∈D`
//   GDD disease-simulation.md:926-928 —— 死区 `|trend| < TREND_EPS` ⇒ 判「持平」返回 0
//   GDD disease-simulation.md:933 —— 「差分在 Q32.32 中间域完成 + 引入死区」的缘由
//   GDD disease-simulation.md:936-937 —— 「状态转移不再用中心差分」(读未来 = 非因果)
//   GDD disease-simulation.md:1111 —— 痊愈支的非上升期门吃 `trend_backward_agg`
//   GDD disease-simulation.md:1743-1744 —— `W` / `TREND_EPS` 均 *待定*(归数值轮)
//
// ── 本文件钉的是什么 ──────────────────────────────────────────────────────────
// 上一轮(`impl-f4-round3`)把 F4 接进了 `StepAll`,但传的 `trendBackwardAgg` 实际是
// `result.Trend` —— 那是 **F2 的中心差分**(读 `t ± W/2`,读未来),名字与语义都对不上。
// 当时两者都是 tripwire 恒 0,所以看不出来。本轮:
//   ① 实现 `ComputeTrendBackward`(真后向差分,只读过去);
//   ② 把 `Progress` 的求值提为**唯一实现** `ComputeProgress`,两处共用;
//   ③ 接线改调它。
// 本文件钉 ①:定义、符号、死区、未配置行为、与中心差分的**语义分叉**。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.DiseaseSimulation;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.DiseaseSimulation
{
    /// <summary>`trend_backward` 的定义、符号、死区与未配置行为。</summary>
    public class TrendBackwardTest
    {
        private const long OnsetTick = 0;

        /// <summary>
        /// 自限型上升曲线 —— `Progress` 在 `τ &lt; τ_peak` 段**单调上升**,
        /// 便于把「趋势符号」做成曲线的函数而不是噪声的函数。
        /// </summary>
        private static DiseaseRegistryEntry Rising()
        {
            return new DiseaseRegistryEntry
            {
                DiseaseId = 71,
                DiseaseKey = "DIS_TREND_RISING",
                Kind = DiseaseKind.Disease,
                Lethality = 0,
                Scale = new Fix(100 * Fix.OneRaw),
                Curve = new NaturalProgressCurve
                {
                    Incubation = 0,
                    APeak = new Fix(80 * Fix.OneRaw),
                    TauRise = new Fix(1000 * Fix.OneRaw),
                    TauPeak = 100000,          // 远未到峰 ⇒ 全程上升
                    TauFall = new Fix(1000 * Fix.OneRaw),
                    Plateau = false,
                    SelfLimit = false,
                    BoundaryMode = "monotone",
                },
            };
        }

        /// <summary>同一曲线,但已过峰且自限 ⇒ 全程衰减(趋势应为负)。</summary>
        private static DiseaseRegistryEntry Falling()
        {
            DiseaseRegistryEntry e = Rising();
            e.DiseaseId = 72;
            e.DiseaseKey = "DIS_TREND_FALLING";
            e.Curve.TauPeak = 10;              // 很早过峰
            e.Curve.SelfLimit = true;          // 过峰后指数衰减
            return e;
        }

        private static Fix Trend(
            DiseaseRegistryEntry entry, long currentTick, long w, long eps = 0)
        {
            return ProgressionEvaluator.ComputeTrendBackward(
                entry, OnsetTick, currentTick,
                new List<TreatmentDose>(), worldSeed: 1, new PatientId(1),
                windowTicks: w, trendEpsRaw: eps);
        }

        // ══════════════════ ① 定义:trend = (P(t) − P(t−W)) / W ══════════════════

        /// <summary>
        /// **核心判据**:数值等于定义式逐字算出来的值。
        /// <para>手算口径:<c>P(t)</c> 与 <c>P(t−W)</c> 各调一次 <c>Evaluate</c> 取
        /// <c>Position</c>,按 <c>(P(t) − P(t−W)) / W</c> 在 Fix 域复算 —— 与实现独立。</para>
        /// <para>若实现写成中心差分(读 <c>t ± W/2</c>),在上升曲线上三者不等
        /// (上升段是凸的 ⇒ 中心差分 &gt; 后向差分),本条红。</para>
        /// </summary>
        [Test]
        public void test_trendBackward_matchesDefinitionExactly()
        {
            DiseaseRegistryEntry entry = Rising();
            const long t = 5000;
            const long w = 1000;

            Fix actual = Trend(entry, t, w);

            Fix atNow = ProgressionEvaluator.Evaluate(
                entry, OnsetTick, t, new List<SimEvent>(), 1, new PatientId(1)).Position;
            Fix atBefore = ProgressionEvaluator.Evaluate(
                entry, OnsetTick, t - w, new List<SimEvent>(), 1, new PatientId(1)).Position;

            Fix expected = (atNow - atBefore) / new Fix(w * Fix.OneRaw);

            Assert.AreEqual(expected.Raw, actual.Raw,
                "trend_backward 须逐位等于 (P(t) − P(t−W)) / W —— 不得是中心差分");
        }

        // ══════════════════ ② 符号:上升为正、衰减为负 ══════════════════

        /// <summary>
        /// 上升段 ⇒ 正;衰减段 ⇒ 负。
        /// <para>若实现把被减数与被减数写反(或漏了符号),本条两个方向同时红 ——
        /// 单测一个方向抓不到「整体取反」。</para>
        /// </summary>
        [Test]
        public void test_trendBackward_signFollowsDirection()
        {
            Fix rising = Trend(Rising(), 5000, 1000);
            Assert.Greater(rising.Raw, 0, "上升段 ⇒ trend_backward > 0");

            Fix falling = Trend(Falling(), 5000, 1000);
            Assert.Less(falling.Raw, 0, "衰减段 ⇒ trend_backward < 0");
        }

        // ══════════════════ ③ 与中心差分的语义分叉(本文件存在的理由) ══════════════════

        /// <summary>
        /// **后向差分 ≠ 中心差分** —— 在凸的上升段上,中心差分严格更大。
        ///
        /// <para>这条钉的是「为什么不能拿 <c>result.Trend</c> 顶替」:两者名字相近、
        /// 量纲相同,在 tripwire 恒 0 时完全不可区分。若将来有人把接线改回
        /// <c>result.Trend</c>,只要中心差分真被实现,本测试就能抓到。</para>
        ///
        /// <para>⚠️ **当前中心差分仍是 tripwire(恒 0)**,故本测试用**定义式**构造对照量,
        /// 不依赖 <c>ComputeTrend</c> 的实现 —— 否则本条会与它一起恒绿。</para>
        /// </summary>
        [Test]
        public void test_trendBackward_differsFromCentralDifference_onConvexRise()
        {
            DiseaseRegistryEntry entry = Rising();
            const long t = 5000;
            const long w = 1000;

            Fix backward = Trend(entry, t, w);

            // 中心差分定义式(独立复算,不经 ComputeTrend —— 它是 tripwire)
            Fix atFuture = ProgressionEvaluator.Evaluate(
                entry, OnsetTick, t + w / 2, new List<SimEvent>(), 1, new PatientId(1)).Position;
            Fix atPast = ProgressionEvaluator.Evaluate(
                entry, OnsetTick, t - w / 2, new List<SimEvent>(), 1, new PatientId(1)).Position;
            Fix central = (atFuture - atPast) / new Fix(w * Fix.OneRaw);

            Assert.AreNotEqual(central.Raw, backward.Raw,
                "凸的上升段上中心差分与后向差分必须不同 —— 若相同,说明本曲线太接近线性,"
                + "本测试失去分辨力,须换曲线而非放宽断言");
        }

        // ══════════════════ ④ 死区 ══════════════════

        /// <summary>
        /// `|trend| &lt; TREND_EPS` ⇒ 返回 0(判「持平」),不返回 ±1 LSB 抖动。
        /// <para>缺死区则 plateau / 迁延段的量化噪声会被读成「在恶化」——
        /// GDD `:933` 点名「恰在最需要趋势的心衰类病种上失真」。</para>
        /// </summary>
        [Test]
        public void test_trendBackward_withinDeadzone_returnsZero()
        {
            // ⚠️ 取点很讲究(第一版取 t=100000,实测趋势已是 0 ⇒ 前提断言当场转红,
            //    正是它该做的事 —— 若没有那条前提,本条会在「差本来就是 0」时恒绿):
            //    要的是**衰减中段**,趋势小但确实非零。τ_fall = 1000,取 τ = 5000
            //    ⇒ 相邻 W = 1000 的差在 Q16.16 上约 60 raw,是死区该吸收的量级;
            //    再往后(如 τ = 8000)差只剩个位数 raw,离 0 太近,做前提不够稳。
            DiseaseRegistryEntry entry = Falling();
            entry.Curve.TauPeak = 10;
            const long t = 5000;
            const long w = 1000;

            Fix raw = Trend(entry, t, w, eps: 0);
            Assert.AreNotEqual(0, raw.Raw,
                "前提:无死区时该点趋势非零(否则本条退化为恒真,钉不住死区)");

            long eps = Math.Abs(raw.Raw) + 1;
            Fix gated = Trend(entry, t, w, eps: eps);

            Assert.AreEqual(0, gated.Raw,
                "|trend| < TREND_EPS ⇒ 判「持平」返回 0");
        }

        /// <summary>
        /// 死区是**严格小于** —— 恰好等于 `TREND_EPS` 时不归零(否则「刚好在门槛上」
        /// 的判读会随实现细节漂移)。
        /// </summary>
        [Test]
        public void test_trendBackward_exactlyAtEps_isNotZeroed()
        {
            DiseaseRegistryEntry entry = Rising();
            const long t = 5000;
            const long w = 1000;

            Fix raw = Trend(entry, t, w, eps: 0);
            Assert.Greater(raw.Raw, 0, "前提:上升段趋势为正");

            Fix atEps = Trend(entry, t, w, eps: raw.Raw);

            Assert.AreEqual(raw.Raw, atEps.Raw,
                "|trend| == TREND_EPS 不满足「<」⇒ 不归零(死区边界是严格小于)");
        }

        // ══════════════════ ⑤ 未配置 ⇒ 不求值(不代拍数值轮的量) ══════════════════

        /// <summary>
        /// `W ≤ 0` ⇒ 返回 0,**不**做任何差分。
        /// <para>用 0 当真窗口会退化成 <c>(P(t) − P(t)) / 0</c> ⇒ 除零。
        /// GDD Tuning `:1743` 标 `*待定*` ⇒ 实现不得代拍一个值。</para>
        /// </summary>
        [Test]
        public void test_unconfiguredWindow_returnsZero_notDivideByZero()
        {
            DiseaseRegistryEntry entry = Rising();

            Assert.DoesNotThrow(() => Trend(entry, 5000, w: 0),
                "W = 0(未配置)⇒ 不得除零");

            Assert.AreEqual(0, Trend(entry, 5000, w: 0).Raw, "W = 0 ⇒ 不求值,返回 0");
            Assert.AreEqual(0, Trend(entry, 5000, w: -100).Raw, "W < 0 ⇒ 同上");
        }

        // ══════════════════ ⑥ 因果性:只读过去 ══════════════════

        /// <summary>
        /// **因果性判据**:`trend_backward(t)` 与**未来**的读数无关 ——
        /// 在 `t` 之后追加任何处置,都不改变 `t` 时刻的后向趋势。
        ///
        /// <para>这是 GDD `:936-937`「F4 在 tick t 上用未来时刻决定状态 = 非因果」的
        /// 可执行形式。若实现误用中心差分(读 <c>t + W/2</c>),而该处置落在
        /// <c>(t, t + W/2]</c> 区间内,本条红。</para>
        /// </summary>
        [Test]
        public void test_trendBackward_ignoresFutureTreatments()
        {
            DiseaseRegistryEntry entry = Rising();
            const long t = 5000;
            const long w = 1000;

            Fix before = Trend(entry, t, w);

            // 未来处置:t+1(落在 (t, t+W/2] 内)—— 中心差分会被它污染
            var futureDose = new TreatmentDose(
                t + 1, 1, ProgressionEvaluator.PolarityCausal,
                new Fix(-50 * Fix.OneRaw), 100000);

            Fix after = ProgressionEvaluator.ComputeTrendBackward(
                entry, OnsetTick, t, new List<TreatmentDose> { futureDose },
                worldSeed: 1, new PatientId(1), windowTicks: w, trendEpsRaw: 0);

            Assert.AreEqual(before.Raw, after.Raw,
                "t 之后才发生的处置不得影响 t 时刻的 trend_backward(只读过去)");
        }

        // ══════════════════ ⑦ 跨 onset 边界:过去的 tick 早于 onset ══════════════════

        /// <summary>
        /// `t − W` 早于 `onset_tick` 时,**病还没起** ⇒ `Progress(t−W) = 0`
        /// ⇒ 趋势 = `P(t) / W`。
        /// <para>若实现不做潜伏抑制(或在过去 tick 上崩),本条红。
        /// 这条同时钉住「传入过去的 tick 是合法输入」这个前提 —— 没有它,
        /// 病程开头 W 个 tick 的趋势都不可求。</para>
        /// </summary>
        [Test]
        public void test_trendBackward_beforeOnset_treatsProgressAsZero()
        {
            DiseaseRegistryEntry entry = Rising();
            const long onset = 10000;
            const long t = 10500;          // τ = 500
            const long w = 1000;           // t − W = 9500 < onset

            Fix actual = ProgressionEvaluator.ComputeTrendBackward(
                entry, onset, t, new List<TreatmentDose>(), 1, new PatientId(1),
                windowTicks: w, trendEpsRaw: 0);

            Fix atNow = ProgressionEvaluator.Evaluate(
                entry, onset, t, new List<SimEvent>(), 1, new PatientId(1)).Position;

            // P(t−W) 早于 onset ⇒ 0 ⇒ trend = (P(t) − 0) / W
            Fix expected = atNow / new Fix(w * Fix.OneRaw);

            Assert.AreEqual(expected.Raw, actual.Raw,
                "t−W 早于 onset ⇒ Progress(t−W) = 0(潜伏抑制)");
        }
    }
}
