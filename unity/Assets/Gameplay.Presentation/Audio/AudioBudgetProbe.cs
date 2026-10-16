// 权威来源:production/epics/audio-system/story-012-music-guardrails-perf-vr.md
//   (AC-44-E2 —— 60 s 采样窗 Audio.Process / DSP 回调 P95 ≤ AUDIO_BUDGET_MS)
// GDD:design/gdd/audio-system.md AC-44-E2 :1123-1128 · §Tuning Knobs :759(AUDIO_BUDGET_MS 行)
// ADR-018 §一(44 只触发 / 只渲染)· §六 G1(乐层护栏,与本类同 story 的另一半)
//
// ⚠️ **本类是 S14-4 的交付物之一,用户裁定 2026-10-16** —— 补评判定 AC-44-E2 的 P95 两条测试
//    打的是**测试自写的 `DspBudgetGuard`**(两行 `p95Ms <= audioBudgetMs`)+ 双端硬编码
//    (`2.5f` / `1.8f`),而生产树零落点(🔴 假)。现落生产谓词 + 生产分位算法。
// ⚠️ **本类只承载「怎么算 / 怎么判」,不承载「实测值」** —— 60 s 窗采样须真 GPU + 真音频回调
//    (EditMode 无 DSP 图),**实测 P95 归 `production/qa/evidence/`**,禁在此断言(禁借绿)。
// ⚠️ **本类不持游戏状态**(ADR-018 §一):纯函数 + 常量,无字段、无状态。

using System;
using System.Collections.Generic;

namespace DaYiJingCheng.Gameplay.Presentation.Audio
{
    /// <summary>DSP 预算探针的**纯谓词面**(AC-44-E2)。
    /// <para>职责:① 分位算法(与 <c>Gameplay.Input.InputLatencyProbe</c> 同款最近秩法,
    /// 承「同一项目内分位口径一致」);② 预算判定(<c>P95 ≤ AUDIO_BUDGET_MS</c>)。</para>
    /// <para>⚠️ **判据 `≤` 含端点** —— 与 `xfade_ms ≥ MUSIC_XFADE_MIN_MS` 的含端点边界同族
    /// (story-012 QA 场景「P95 = budget ⇒ 绿」)。</para></summary>
    public static class AudioBudgetProbe
    {
        /// <summary>按**最近秩法**取分位(与 <c>InputLatencyProbe.ComputePercentile</c> 同口径:
        /// 升序后取 <c>ceil(n × p / 100) - 1</c>,越界钳到 <c>[0, n-1]</c>)。
        /// <para>⚠️ 与 <c>InputLatencyProbe</c> 的**同一算法两份实现**是刻意的 —— 那个是
        /// <c>MonoBehaviour</c> 的私有方法,抽公共工具类会动到 3 的装配面;
        /// **两处口径一致由本注释 + 各自测试守**(若将来改口径,两处同改)。</para></summary>
        /// <param name="samples">样本(未排序;空集 ⇒ <c>-1</c>,与 `InputLatencyProbe` 同哨兵)。</param>
        /// <param name="percentile">分位(如 95)。</param>
        /// <returns>分位值;空集 = <c>-1</c>(**不是 0** —— 0 会被误判为「极快」)。</returns>
        public static double Percentile(IReadOnlyList<double> samples, double percentile)
        {
            if (samples == null || samples.Count == 0)
                return -1;

            var sorted = new List<double>(samples);
            sorted.Sort();

            int idx = (int)Math.Ceiling(sorted.Count * percentile / 100.0) - 1;
            if (idx < 0) idx = 0;
            if (idx > sorted.Count - 1) idx = sorted.Count - 1;
            return sorted[idx];
        }

        /// <summary>`P95 ≤ AUDIO_BUDGET_MS`(AC-44-E2 的判据;**含端点**)。
        /// <para>⚠️ **负样本不得冒充通过**:<c>p95Ms &lt; 0</c>(哨兵 = 样本不足)判**失败** ——
        /// 否则「探针没采到样」会被读成「预算达标」(与 `PresentationDtoGuard.Scan(null)` 同款:
        /// 拒以空集冒充绿)。</para></summary>
        /// <param name="p95Ms">实测 P95(ms);<c>&lt; 0</c> = 样本不足哨兵 ⇒ 判失败。</param>
        /// <param name="audioBudgetMs">预算上限(ms;<see cref="AudioTuning.AudioBudgetMs"/>)。</param>
        /// <returns>是否在预算内。</returns>
        public static bool IsP95WithinBudget(double p95Ms, double audioBudgetMs)
            => p95Ms >= 0 && p95Ms <= audioBudgetMs;
    }
}
