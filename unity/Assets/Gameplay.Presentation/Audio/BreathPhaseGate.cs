// 权威来源:production/epics/audio-system/story-004-breath-layers-precision-tiers.md(Implementation Notes)
//   · 附加音层**相位锁定**到基础循环,窗口 = 吸气段末 20%;clock_ref 运行期执行半
//   · 形态裁定(2026-09-26):**loop + 逐周期相位门控**(非 one-shot;002 规则 8 强制 loop: true)
//   · 相位旗复位跟着基础层 timeSamples(回绕**或** Seek 跳变都要复位)
// GDD:design/gdd/audio-system.md §Edge Cases 相位条(:576-588,F5 重钉)· AC-44-08 [A] · AC-44-14
// docs/engine-reference/unity/modules/audio.md(AudioLowPassFilter / ramp 依据;3a 补录)
// ADR-018 §四 · TR-audio-005
//
// ⚠️ **编码防线 ①(唯一乘法路径)**:分数 → 绝对量的换算**只准经**
//    InspirePhaseMapping.ToAbsoluteOffsetSeconds / ToAbsoluteJitterSeconds ——
//    本文件**零处**出现 `× INSPIRE_FRACTION`(窗口也从映射函数取,周期取 1 ⇒ 秒值 ≡ 周期分数)。
// ⚠️ **驱动源 = 基础层 `timeSamples / clip.frequency`,分母 = `clip.length`**(unity-specialist Q4):
//    pitch ≠ 1 时真实 T = T/pitch,旋钮 T 会错;`AudioSource.time` 在首个 DSP 回调前为 0,
//    **不得**用作首周期判据(本类只读 timeSamples)。
// ⚠️ **禁协程 / 定时器排附加层**(帧率与 DSP 缓冲不同源 ⇒ 啰音逐周期漂进吸气中段 =
//    医学反相,AC-44-08 直接红)—— 相位真值只来自 timeSamples,由 Tick 采样。

using UnityEngine;

namespace DaYiJingCheng.Gameplay.Presentation.Audio
{
    /// <summary>呼吸两层相位门控的**纯函数核心**(Story 004 运行期执行面 · AC-44-08 [A] / AC-44-14)。
    /// <para>无状态(状态由 <see cref="BreathLayerDriver"/> 持),可 EditMode 直测;
    /// 输入输出全为标量,零 <c>AudioSource</c> 依赖。</para></summary>
    public static class BreathPhaseGate
    {
        /// <summary>基础层**真实周期相位**(0..1,0 = 吸气起点)。
        /// <para>公式:<c>phase = timeSamples / (clip.frequency × clip.length)</c>
        /// —— **分母 = clip.length,不是旋钮 T**(pitch ≠ 1 时真实 T = T/pitch,Q4 裁定)。</para>
        /// <para>首个 DSP 回调前 <c>timeSamples = 0</c> ⇒ phase = 0(周期原点)——
        /// 不产生误触发(窗口 ⊆ 吸气段末,0 ∉ 窗口),**不读 <c>AudioSource.time</c>**。</para></summary>
        /// <param name="timeSamples">基础层 <c>AudioSource.timeSamples</c>(播放头采样位置)。</param>
        /// <param name="frequency">clip 采样率(Hz)。</param>
        /// <param name="clipLengthSeconds"><c>clip.length</c>(秒)—— 相位分母的一半。</param>
        /// <returns>周期相位;分母非法 ⇒ LogError + 0(安全值,不抛)。</returns>
        public static double CyclePhase(int timeSamples, int frequency, double clipLengthSeconds)
        {
            double totalSamples = (double)frequency * clipLengthSeconds;
            if (frequency <= 0 || !(clipLengthSeconds > 0.0) || timeSamples < 0 || totalSamples <= 0.0)
            {
                Debug.LogError("[BreathPhaseGate] 相位分母非法(clip 未就绪 / timeSamples < 0)" +
                               "—— 返回 0(周期原点)");
                return 0.0;
            }

            return timeSamples / totalSamples;
        }

        /// <summary>**相位旗复位判定**(形态裁定:回绕或 Seek 跳变都要复位)。
        /// <para>① <c>current &lt; previous</c> ⇒ 回绕或 Seek 后跳;
        /// ② <c>current − previous &gt; maxExpectedAdvance</c> ⇒ Seek 前跳(跳过窗口时须复位防漏发)。
        /// 正常推进(≤ 期望上限)不复位。</para></summary>
        /// <param name="previousPhase">上一 Tick 相位。</param>
        /// <param name="currentPhase">本 Tick 相位。</param>
        /// <param name="maxExpectedAdvance">单 Tick 允许的最大正向推进
        /// (调用方 = <c>2 × 帧墙钟 / clip.length</c>,余量 2 倍)。</param>
        /// <returns>true = 应复位本周期触发旗。</returns>
        public static bool ShouldResetCycleFlag(double previousPhase, double currentPhase, double maxExpectedAdvance)
        {
            if (currentPhase < previousPhase)
                return true;                                  // 回绕 / Seek 后跳
            double advance = currentPhase - previousPhase;
            if (maxExpectedAdvance < 0.0)
                maxExpectedAdvance = 0.0;
            return advance > maxExpectedAdvance;              // Seek 前跳
        }

        /// <summary>**相位窗(周期分数域)**:<c>[trigger_phase × F − jitter, … + jitter]</c>。
        /// 换算**只准经 <see cref="InspirePhaseMapping"/>**(周期取 1 ⇒ 返回的「秒」≡ 周期分数;
        /// 禁在此自乘 INSPIRE_FRACTION —— 编码防线 ①)。</summary>
        /// <param name="triggerPhase">相对吸气段的分数(事件表原文;门已断言 ∈ [0.8,1.0])。</param>
        /// <param name="jitter">相位抖动(相对吸气段的分数幅值;≥ 0)。</param>
        /// <param name="inspireFraction"><c>INSPIRE_FRACTION</c>(注入;生产传
        /// <see cref="AudioTuning.InspireFraction"/>)。</param>
        /// <param name="windowStart">窗起点(周期分数)。</param>
        /// <param name="windowEnd">窗终点(周期分数;恒 ≤ F ⊆ 吸气段)。</param>
        public static void WindowCycleFraction(
            double triggerPhase, double jitter, double inspireFraction,
            out double windowStart, out double windowEnd)
        {
            double center = InspirePhaseMapping.ToAbsoluteOffsetSeconds(
                triggerPhase, inspireFraction, 1.0);
            double jitterAmount = InspirePhaseMapping.ToAbsoluteJitterSeconds(
                jitter, inspireFraction, 1.0);

            windowStart = Clamp01(center - jitterAmount);
            windowEnd = Clamp01(center + jitterAmount);
        }

        /// <summary>**本周期是否该发**(附加层相位门控):相位 ∈ 窗口 且 本周期未发过。
        /// 窗口经 <see cref="WindowCycleFraction"/>(单一映射路径)。</summary>
        /// <param name="cyclePhase">当前周期相位(<see cref="CyclePhase"/>)。</param>
        /// <param name="triggerPhase">相对吸气段的分数(事件表)。</param>
        /// <param name="jitter">相位抖动(分数幅值)。</param>
        /// <param name="inspireFraction"><c>INSPIRE_FRACTION</c>。</param>
        /// <param name="firedThisCycle">本周期已发旗(回绕 / Seek 复位由驱动侧管)。</param>
        /// <returns>true = 本 Tick 应发(驱动侧置旗并开门控)。</returns>
        public static bool ShouldFire(
            double cyclePhase, double triggerPhase, double jitter,
            double inspireFraction, bool firedThisCycle)
        {
            if (firedThisCycle)
                return false;
            if (!(inspireFraction > 0.0 && inspireFraction < 1.0))
            {
                Debug.LogError("[BreathPhaseGate] INSPIRE_FRACTION 须 ∈ (0,1) —— 本周期不触发" +
                               "(GDD §Tuning Knobs 安全范围)");
                return false;
            }

            WindowCycleFraction(triggerPhase, jitter, inspireFraction,
                out double start, out double end);
            return cyclePhase >= start && cyclePhase <= end;
        }

        /// <summary>**附加层调度时刻(候选,当前无生产调用方)**:窗口起 / 终点的 dspTime ——
        /// 换算只准经 <see cref="InspirePhaseMapping"/>(唯一乘法路径)。
        /// <para>⚠️ **Q6 裁定(2026-09-26)**:loop 源上禁用 <c>PlayScheduled</c>(见
        /// <c>BreathLayerDriver.cs</c> 文件头;OQ-44-5 spike 未结)⇒ 本函数暂无调用方,
        /// **保留为唯一换算路径**并由测试钉住(单乘法路径断言),spike 结论落地时直接复用。</para></summary>
        /// <param name="cycleStartDspSeconds">本周期起点的 dspTime
        /// (调用方记录;帧量化 ≤ 1 帧,已登记)。</param>
        /// <param name="triggerPhase">相对吸气段的分数。</param>
        /// <param name="jitter">相位抖动(分数幅值)。</param>
        /// <param name="inspireFraction"><c>INSPIRE_FRACTION</c>。</param>
        /// <param name="cycleSeconds">周期时长(秒;**= clip.length**,非旋钮 T)。</param>
        /// <param name="windowStartDsp">窗口起点 dspTime。</param>
        /// <param name="windowEndDsp">窗口终点 dspTime。</param>
        public static void AdventitiousWindowDsp(
            double cycleStartDspSeconds, double triggerPhase, double jitter,
            double inspireFraction, double cycleSeconds,
            out double windowStartDsp, out double windowEndDsp)
        {
            double center = InspirePhaseMapping.ToAbsoluteOffsetSeconds(
                triggerPhase, inspireFraction, cycleSeconds);
            double jitterAmount = InspirePhaseMapping.ToAbsoluteJitterSeconds(
                jitter, inspireFraction, cycleSeconds);

            windowStartDsp = cycleStartDspSeconds + Clamp01Seconds(center - jitterAmount, cycleSeconds);
            windowEndDsp = cycleStartDspSeconds + Clamp01Seconds(center + jitterAmount, cycleSeconds);
        }

        /// <summary>钳到 [0, max](max ≤ 0 时返回 0)。</summary>
        private static double Clamp01Seconds(double value, double max)
        {
            if (max < 0.0) max = 0.0;
            if (value < 0.0) return 0.0;
            return value > max ? max : value;
        }

        /// <summary>钳到 [0,1]。</summary>
        private static double Clamp01(double value)
        {
            if (value < 0.0) return 0.0;
            return value > 1.0 ? 1.0 : value;
        }
    }
}
