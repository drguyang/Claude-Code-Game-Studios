// TD-epic-01 第十二片 · 51 遥测 F4–F7 参数旋钮的**内存形**(绑定结果 / 装载载体共用)。
//
// 权威来源:
//   `design/gdd/telemetry-analytics.md` §Tuning Knobs(六旋钮的存在、位置与副作用;
//     「数值由用户自己调」—— 本类**不拍值**,只执法 GDD 明写的安全范围)
//   ADR-014 §四(Fix 字段必须字符串;本表全为 int 计数值 —— 承 ADR-006 D-21-17
//     「int 计数移出 Fix 解析集」,故 .json 里用合法整数 token)
//   AC-51-B14(F5 参数退化 ⇒ 构建期 / 加载期硬失败,非运行期退化、非静默取默认值)
//
// ⚠️ **落点**:住 `Gameplay.Presentation`(与 `DeviceSwitchTuning` 同款纪律 —— 烘焙器的
//   `Editor.Tools.Bake` 与运行期的装载面都引得到它);`Telemetry` 程序集是
//   `noEngineReferences` 的纯计算面,**不**持有参数真源。
// ⚠️ 本类零 `UnityEngine` 引用 —— 纯数据 + 校验谓词,EditMode 可直驱。

using System;
using System.Collections.Generic;

namespace DaYiJingCheng.Gameplay.Presentation.Telemetry
{
    /// <summary>F5 难度曲线滑窗三旋钮(`W` / `S` / `t_0`;GDD §Tuning Knobs)。
    /// <para>`W` = 窗宽(tick,`≥ 1`)· `S` = 步长(tick,`≥ 1`;可 &gt; `W`,窗间留空由覆盖记账承载)·
    /// `t_0` = 首窗起点(tick,`≥ 0`)。</para></summary>
    public readonly struct DifficultyCurveTuning
    {
        /// <summary>窗宽 `W`(tick)。</summary>
        public readonly int WindowTicks;

        /// <summary>步长 `S`(tick)。</summary>
        public readonly int StepTicks;

        /// <summary>首窗起点 `t_0`(tick)。</summary>
        public readonly long FirstWindowTick;

        /// <summary>构造(不校验 —— 校验归 <see cref="TelemetryAnalyticsTuning.Validate"/>)。</summary>
        public DifficultyCurveTuning(int windowTicks, int stepTicks, long firstWindowTick)
        {
            WindowTicks = windowTicks;
            StepTicks = stepTicks;
            FirstWindowTick = firstWindowTick;
        }
    }

    /// <summary>六旋钮(四组)+ 安全范围校验谓词。</summary>
    public readonly struct TelemetryAnalyticsTuning
    {
        /// <summary>F5 滑窗三旋钮。</summary>
        public readonly DifficultyCurveTuning DifficultyCurve;

        /// <summary>F7 静默段阈值 `GAP_THRESHOLD`(tick,`≥ 0`)。</summary>
        public readonly int GapThresholdTicks;

        /// <summary>F4 成长统计窗 `GROWTH_WINDOW`(tick,`≥ 1`)。</summary>
        public readonly int GrowthWindowTicks;

        /// <summary>E-3 样本不足标注门槛 `TELEMETRY_MIN_SAMPLE`(计数,`≥ 1`)。</summary>
        public readonly int MinSample;

        /// <summary>构造(不校验)。</summary>
        public TelemetryAnalyticsTuning(DifficultyCurveTuning difficultyCurve, int gapThresholdTicks,
                                        int growthWindowTicks, int minSample)
        {
            DifficultyCurve = difficultyCurve;
            GapThresholdTicks = gapThresholdTicks;
            GrowthWindowTicks = growthWindowTicks;
            MinSample = minSample;
        }

        /// <summary>安全范围断言(GDD §Tuning Knobs 的「安全范围」列逐条;返回错误列表,空 = 合法)。
        /// <para>⚠️ 只执法 GDD 明写的**安全范围**,**不**拍值 —— 值的调整归用户数值轮。</para>
        /// <para>`W &lt; 1` / `S &lt; 1` ⇒ 退化,构建期 / 加载期硬失败(AC-51-B14)。</para></summary>
        /// <param name="fileLabel">错误前缀(文件名或「加载期」)。</param>
        public List<string> Validate(string fileLabel)
        {
            var errors = new List<string>();
            if (DifficultyCurve.WindowTicks < 1)
                errors.Add($"{fileLabel}:difficulty_curve.window_ticks = {DifficultyCurve.WindowTicks} " +
                           "—— W < 1 ⇒ F5 退化,须 ≥ 1(AC-51-B14:构建期 / 加载期硬失败,不得静默取默认值)");
            if (DifficultyCurve.StepTicks < 1)
                errors.Add($"{fileLabel}:difficulty_curve.step_ticks = {DifficultyCurve.StepTicks} " +
                           "—— S < 1 ⇒ 步长退化,须 ≥ 1(S > W 合法,窗间留空由 WindowCoverage 记账)");
            if (DifficultyCurve.FirstWindowTick < 0)
                errors.Add($"{fileLabel}:difficulty_curve.first_window_tick = {DifficultyCurve.FirstWindowTick} " +
                           "—— t_0 须 ≥ 0(Tick 非负)");
            if (GapThresholdTicks < 0)
                errors.Add($"{fileLabel}:session_rhythm.gap_threshold_ticks = {GapThresholdTicks} " +
                           "—— GAP_THRESHOLD 须 ≥ 0");
            if (GrowthWindowTicks < 1)
                errors.Add($"{fileLabel}:skill_growth.growth_window_ticks = {GrowthWindowTicks} " +
                           "—— GROWTH_WINDOW 须 ≥ 1");
            if (MinSample < 1)
                errors.Add($"{fileLabel}:sample.min_sample = {MinSample} " +
                           "—— TELEMETRY_MIN_SAMPLE 须 ≥ 1(0 会让「样本不足」标注恒不触发)");
            return errors;
        }
    }
}
