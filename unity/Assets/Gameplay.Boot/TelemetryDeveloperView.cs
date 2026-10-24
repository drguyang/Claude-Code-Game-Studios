// 权威来源:ADR-019 §五(调试视图 = 开发者可见的本地汇总;不交付玩家可见统计 UI · AC-19-06)·
//   design/gdd/telemetry-analytics.md(`OQ-51-1` 2026-10-06 已裁:两处分立,51 侧 = **纯文本**)·
//   production/epics/telemetry-analytics/story-009-blank-and-degraded.md(AC-51-D4 / D8 / D9)
//
// ═══════════════════════════════════════════════════════════════════════════════
// ⚠️ **本文件整体包在 `#if UNITY_EDITOR || DEVELOPMENT_BUILD` 内**(AC-51-D4,承
//    `Gameplay.UI/Skeuomorphic/DevDebugView.cs` 先例):Release 玩家构建里
//    **代码路径不存在** —— 不是「有入口但点了没用」,是编译期剔除。
// ⚠️ **纯文本**:零 `.uxml` / `.uss` / Canvas / 场景资产(AC-51-A5 / E1);不经 42 渲染
//    (`OQ-51-1` 已裁两处分立;「经 42 渲染」是 P1a 的预留通道,不是 P0 形状)。
// ⚠️ **参数纪律**:`W` / `S` / `t_0` / `GAP_THRESHOLD` / `GROWTH_WINDOW` / `TELEMETRY_MIN_SAMPLE`
//    全部 `*待定*`(GDD Tuning Knobs,用户调)⇒ 本件**不填默认值**;请求里没给的,
//    正文里显式写「未给(未算)」—— 报告不假装算过。
// ═══════════════════════════════════════════════════════════════════════════════

#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System;
using System.Text;
using DaYiJingCheng.Telemetry;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>调试视图的预览口径(全部可空 = 调用方没给的旋钮;**本件不代填**)。</summary>
    public readonly struct TelemetryPreviewRequest
    {
        /// <summary>F4 技能筛键(技能表 ordinal 的字符串)。null ⇒ F4 未算。</summary>
        public readonly string Skill;

        /// <summary>F4 成长窗口(tick 闭区间)。null ⇒ 全流。</summary>
        public readonly (long from, long to)? GrowthWindow;

        /// <summary>F5 首窗起点 t_0。null ⇒ F5 未算。</summary>
        public readonly long? FirstWindowTick;

        /// <summary>F5 步长 S。null ⇒ F5 未算。</summary>
        public readonly int? Step;

        /// <summary>F5 窗宽 W(半开 `[t_k, t_k + W)`)。null ⇒ F5 未算。</summary>
        public readonly int? Window;

        /// <summary>F7 静默段阈值 GAP_THRESHOLD。null ⇒ F6 照算、F7 未算。</summary>
        public readonly int? GapThreshold;

        /// <summary>「样本不足」门槛 TELEMETRY_MIN_SAMPLE(AC-51-D9)。null ⇒ 不加该标注。</summary>
        public readonly int? MinSample;

        /// <summary>
        /// 存档头 `ConfigVersion` 与当前产物的比对(`AC-51-D7` / E-10)。null ⇒ **不做版本门**。
        /// <para>⚠️ <b>2026-10-24 `TD-epic-01-f` 修复新增</b>:此前本请求**没有**这个字段,
        /// 而 <see cref="Render"/> 调 <c>AllMetricsReport</c> 时也不传该形参 ⇒
        /// 版本门在生产路径上**从未执行**过(`ConfigVersionCheck` 生产侧零构造)。</para>
        /// </summary>
        public readonly ConfigVersionCheck? ConfigVersion;

        public TelemetryPreviewRequest(string skill, (long from, long to)? growthWindow,
            long? firstWindowTick, int? step, int? window, int? gapThreshold, int? minSample,
            ConfigVersionCheck? configVersion = null)
        {
            Skill = skill; GrowthWindow = growthWindow; FirstWindowTick = firstWindowTick;
            Step = step; Window = window; GapThreshold = gapThreshold; MinSample = minSample;
            ConfigVersion = configVersion;
        }
    }

    /// <summary>51 的开发者调试视图 —— **纯文本汇编**(ADR-019 §五 · `OQ-51-1` P0 形状)。
    /// <para>只调 <see cref="TelemetryReporter"/> 的报告方法;自身零 IO、零引擎、零资产。</para></summary>
    public static class TelemetryDeveloperView
    {
        private const string NotGiven = "未给(未算 —— 旋钮待用户调)";

        /// <summary>汇编预览正文:口径标注 + 可算指标 + 未算项逐条点名。</summary>
        public static string Render(TelemetryReporter reporter, in TelemetryPreviewRequest r)
        {
            if (reporter == null) throw new ArgumentNullException(nameof(reporter));

            var sb = new StringBuilder();
            sb.Append("# 51 调试视图(开发者本地 · DevBuild/Editor only · 纯文本)\n");
            // 完整性头由 `AllMetricsReport` 自带(空流 / 部分指标)—— 此处**不前置**,否则重复成两份
            // (第二份还会与「未给」参数口径脱节)。

            // 样本不足(AC-51-D9):只加标注,**数值不动** —— 报告照常产出
            if (r.MinSample.HasValue && reporter.ScorableCount < r.MinSample.Value)
                sb.AppendFormat("# ⚠️ 样本不足:Scorable = {0} < TELEMETRY_MIN_SAMPLE = {1}(数值照常,不做掩盖)\n",
                    reporter.ScorableCount, r.MinSample.Value);

            // F1–F3:参数无关,恒算
            // 🔁 2026-10-24 `TD-epic-01-f`:版本门**终于被传入**(此前恒不传 ⇒ 门从未执行)。
            //    不匹配时 `AllMetricsReport` 会早退、只出标注不出指标(AC-51-D7「不静默出数」)。
            sb.Append(reporter.AllMetricsReport(out int skipped, r.ConfigVersion));
            sb.AppendFormat("# 跳过条数(解码失败,可观测)= {0}\n", skipped);

            // F4
            if (!string.IsNullOrEmpty(r.Skill))
                sb.Append(reporter.SkillGrowthReport(r.Skill, r.GrowthWindow, out _));
            else
                sb.AppendFormat("# F4 未算:技能筛键 — {0}\n", NotGiven);

            // F5
            if (r.FirstWindowTick.HasValue && r.Step.HasValue && r.Window.HasValue)
                sb.Append(reporter.DifficultyCurveReport(r.FirstWindowTick.Value, r.Step.Value,
                    r.Window.Value, out _));
            else
                sb.AppendFormat("# F5 未算:t_0 / S / W — {0}\n", NotGiven);

            // F6 恒算(只读 tick);F7 要 GAP_THRESHOLD
            if (r.GapThreshold.HasValue)
                sb.Append(reporter.SessionRhythmReport(r.GapThreshold.Value, out _));
            else
                sb.AppendFormat("# F6/F7 未算:GAP_THRESHOLD — {0}\n", NotGiven);

            return sb.ToString();
        }
    }
}

#endif
