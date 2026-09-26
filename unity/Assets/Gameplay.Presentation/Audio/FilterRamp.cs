// 权威来源:production/epics/audio-system/story-004-breath-layers-precision-tiers.md(Implementation Notes)
//   · ramp ≥ 50 ms 常量单处定义(= AudioTuning.RampSeconds,与 Story 006 共用底座)
//   · 可测点 = 单帧 Δ ≤ maxΔ(斜率上限)+ 到位计时 ≥ 50 ms(unity-specialist Q3 裁定)
// GDD:design/gdd/audio-system.md F-44.1 注(ramp ≥ 50 ms 硬下界 :312)· AC-44-04(无爆音)
// docs/engine-reference/unity/modules/audio.md「Ramp ≥ 50 ms 的可用依据」(3a 补录)
// ADR-018 §四 · TR-audio-005 / TR-audio-006(50ms ramp)
//
// ⚠️ **禁直 set 目标值**(如 <c>AudioLowPassFilter.cutoffFrequency</c> 一次性赋值 = 硬切):
//    允许的写法是**每帧把 <see cref="Step"/> 的输出**写进 exposed <c>SetFloat</c> / 逐源滤波组件。
// ⚠️ **禁拿 <c>TransitionTo(0.05f)</c> 当 ramp 达标** —— 曲线引擎内部、未文档化 = 假绿
//    (依据 = engine-reference 3a 补录的两条签名核对)。
// ⚠️ 轴语义:**频率走对数轴**(octave,经 HertzToOctaveAxis / OctaveAxisToHz);
//    **dB 本身就是对数域 ⇒ 直接用原值当轴**(禁再取一次 log)。

using UnityEngine;

namespace DaYiJingCheng.Gameplay.Presentation.Audio
{
    /// <summary>滤波 / 噪声底 / 淡化的 **ramp 纯函数底座**(Story 004 建,Story 006 复用)。
    /// <para>在**轴域**(octave 或 dB)线性推进 —— 轴的变换由调用方经
    /// <see cref="HertzToOctaveAxis"/> 等选好,本类不猜轴语义,保证:
    /// ① 单帧推进 ≤ <see cref="MaxStepForFrame"/>(斜率上限,可测);
    /// ② 到位计时 ≥ 时长下限(<c>State.ElapsedSeconds</c> 收口,可测)。</para>
    /// <example>
    /// // 频率 ramp:2000 Hz → 500 Hz,dt = 1/60 s
    /// FilterRamp.State s = FilterRamp.Begin(
    ///     FilterRamp.HertzToOctaveAxis(2000), FilterRamp.HertzToOctaveAxis(500));
    /// double axis = FilterRamp.Step(ref s, 1.0 / 60.0);
    /// float hz = (float)FilterRamp.OctaveAxisToHz(axis);   // 再 SetFloat / 赋给逐源滤波
    /// </example></summary>
    public static class FilterRamp
    {
        /// <summary>ramp 会话状态(轴域;由 <see cref="Begin"/> 开、<see cref="Step"/> 推进)。</summary>
        public struct State
        {
            /// <summary>起点(轴域)。</summary>
            public double StartAxis;

            /// <summary>终点(轴域)。</summary>
            public double TargetAxis;

            /// <summary>总时长(秒)。默认 = <see cref="AudioTuning.RampSeconds"/>。</summary>
            public double DurationSeconds;

            /// <summary>已推进时长(秒;到位后停表)——「到位计时 ≥ 50 ms」断言读它。</summary>
            public double ElapsedSeconds;

            /// <summary>是否仍在推进(到位 = false)。</summary>
            public bool Active;
        }

        /// <summary>以 <see cref="AudioTuning.RampSeconds"/> 为时长开一段 ramp。</summary>
        /// <param name="fromAxis">起点(轴域;频率须先经 <see cref="HertzToOctaveAxis"/>)。</param>
        /// <param name="toAxis">终点(轴域)。</param>
        /// <returns>初始状态(<c>Active = true</c>;起点 == 终点时 <c>Active = false</c>)。</returns>
        public static State Begin(double fromAxis, double toAxis)
            => Begin(fromAxis, toAxis, AudioTuning.RampSeconds);

        /// <summary>以指定时长开一段 ramp(时长参数仅供测试注入;生产走无参重载)。</summary>
        /// <param name="fromAxis">起点(轴域)。</param>
        /// <param name="toAxis">终点(轴域)。</param>
        /// <param name="durationSeconds">时长(秒);≤ 0 ⇒ LogError + 按 0 处理(直接到位)。</param>
        public static State Begin(double fromAxis, double toAxis, double durationSeconds)
        {
            if (durationSeconds <= 0.0)
            {
                Debug.LogError("[FilterRamp] ramp 时长须 > 0(硬下界 = AudioTuning.RampSeconds)" +
                               "—— 按 0 处理(直接到位,不产生斜坡)");
                durationSeconds = 0.0;
            }

            return new State
            {
                StartAxis = fromAxis,
                TargetAxis = toAxis,
                DurationSeconds = durationSeconds,
                ElapsedSeconds = 0.0,
                Active = durationSeconds > 0.0 && fromAxis != toAxis,
            };
        }

        /// <summary>推进一步并返回当前轴值(轴域线性;越界钳到终点)。
        /// <para>单帧推进量恒 ≤ <see cref="MaxStepForFrame"/>(斜率上限判据);
        /// <paramref name="dtSeconds"/> ≤ 0 ⇒ 不推进(返回当前值)。</para></summary>
        /// <param name="state">会话状态(ref 推进时间)。</param>
        /// <param name="dtSeconds">帧时长(秒)。</param>
        /// <returns>本帧轴值。</returns>
        public static double Step(ref State state, double dtSeconds)
        {
            if (dtSeconds < 0.0)
            {
                Debug.LogError("[FilterRamp] dt < 0 —— 忽略本帧推进");
                dtSeconds = 0.0;
            }

            if (!state.Active)
                return state.TargetAxis;

            state.ElapsedSeconds += dtSeconds;
            if (state.DurationSeconds <= 0.0 || state.ElapsedSeconds >= state.DurationSeconds)
            {
                state.ElapsedSeconds = state.DurationSeconds;
                state.Active = false;
                return state.TargetAxis;
            }

            double t = state.ElapsedSeconds / state.DurationSeconds;
            return state.StartAxis + (state.TargetAxis - state.StartAxis) * t;
        }

        /// <summary>**斜率上限**:时长匀速下任意 <paramref name="dtSeconds"/> 帧允许的最大轴推进量
        /// —— 测试断言「单帧 Δ ≤ maxΔ」的纯函数判据。</summary>
        /// <param name="state">会话状态。</param>
        /// <param name="dtSeconds">帧时长(秒)。</param>
        /// <returns>允许的最大轴推进量(绝对值;时长 0 ⇒ 返回全程差)。</returns>
        public static double MaxStepForFrame(in State state, double dtSeconds)
        {
            double total = System.Math.Abs(state.TargetAxis - state.StartAxis);
            if (state.DurationSeconds <= 0.0)
                return total;
            if (dtSeconds < 0.0)
                dtSeconds = 0.0;
            return total * (dtSeconds / state.DurationSeconds);
        }

        /// <summary>频率 → octave 轴(<c>log2(Hz)</c>)—— 频率 ramp **必须**走对数轴
        /// (等距推进 = 听感上低频慢高频快的错 ramp)。</summary>
        /// <param name="hertz">频率(Hz;须 > 0)。</param>
        /// <returns>octave 轴值;入参 ≤ 0 ⇒ LogError + 0 轴(安全值,不抛)。</returns>
        public static double HertzToOctaveAxis(double hertz)
        {
            if (!(hertz > 0.0))
            {
                Debug.LogError("[FilterRamp] 频率须 > 0 Hz —— 返回 0 轴(安全值)");
                return 0.0;
            }

            return System.Math.Log(hertz, 2.0);
        }

        /// <summary>octave 轴 → 频率(<c>2^axis</c>)。<see cref="HertzToOctaveAxis"/> 的逆)。</summary>
        /// <param name="octaveAxis">octave 轴值。</param>
        /// <returns>频率(Hz)。</returns>
        public static double OctaveAxisToHz(double octaveAxis)
            => System.Math.Pow(2.0, octaveAxis);
    }
}
