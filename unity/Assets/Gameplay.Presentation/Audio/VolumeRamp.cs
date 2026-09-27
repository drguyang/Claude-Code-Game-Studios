// 权威来源:production/epics/audio-system/story-006-voice-variants-intensity-bucket.md
//   · AC-44-04:交叉淡化无爆音(spike)、无相位抵消;滤波/噪声底变化 ramp ≥ 50 ms
//   · 交叉淡化 ramp ≥ 50ms,常量单一出处(与 Story 004 共用);禁直 set cutoffFrequency 硬切
// GDD:design/gdd/audio-system.md F-44.1 注(ramp ≥ 50 ms 硬下界 :312)· AC-44-04
// ADR-018 §四 需求②(变体库混合 + 交叉淡化)· TR-audio-006(50ms ramp 防爆音)
//
// ⚠️ 执行体不可复用 FilterRamp:FilterRamp 的 sink 是 SetFloat,变体淡化的 sink 是
//    AudioSource.volume ⇒ 执行体必须独立;常量(AudioTuning.RampSeconds)与 Step 节奏可共用。
// ⚠️ 线性 volume 插值有感知 dip 风险(DANGER 提醒)—— AC-44-04 [L] 听测必须覆盖变体切换路径。
// ⚠️ 禁直 set 目标值(如 AudioSource.volume 一次性赋值 = 硬切):
//    允许的写法是**每帧把 Step 的输出**写进 AudioSource.volume。
// ⚠️ 50ms ramp 常量与 Story 004 共用单处定义(AudioTuning.RampSeconds),禁在别处另抄 0.05。

namespace DaYiJingCheng.Gameplay.Presentation.Audio
{
    /// <summary>交叉淡化 ramp 纯函数底座(Story 006 建,结构对齐 FilterRamp 但不继承不包装)。
    /// <para>在**线性 volume 域**推进 —— AudioSource.volume 是线性幅度,直接线性插值。
    /// ⚠️ 线性 volume 插值有感知 dip 风险(DANGER 提醒)—— AC-44-04 [L] 听测必须覆盖变体切换路径。</para>
    /// <para>保证:① 单帧推进 ≤ <see cref="MaxStepForFrame"/>(斜率上限,可测);
    /// ② 到位计时 ≥ 时长下限(<c>State.ElapsedSeconds</c> 收口,可测)。</para>
    /// <example>
    /// // 交叉淡化:volume 0.8 → 0.2,dt = 1/60 s
    /// VolumeRamp.State s = VolumeRamp.Begin(0.8, 0.2);
    /// double vol = VolumeRamp.Step(ref s, 1.0 / 60.0);
    /// audioSource.volume = (float)vol;   // 每帧写入,禁直 set
    /// </example></summary>
    public static class VolumeRamp
    {
        /// <summary>ramp 会话状态(线性 volume 域;由 <see cref="Begin"/> 开、<see cref="Step"/> 推进)。</summary>
        public struct State
        {
            /// <summary>起点(线性 volume)。</summary>
            public double StartVolume;

            /// <summary>终点(线性 volume)。</summary>
            public double TargetVolume;

            /// <summary>总时长(秒)。默认 = <see cref="AudioTuning.RampSeconds"/>。</summary>
            public double DurationSeconds;

            /// <summary>已推进时长(秒;到位后停表)——「到位计时 ≥ 50 ms」断言读它。</summary>
            public double ElapsedSeconds;

            /// <summary>是否仍在推进(到位 = false)。</summary>
            public bool Active;
        }

        /// <summary>以 <see cref="AudioTuning.RampSeconds"/> 为时长开一段 ramp。</summary>
        /// <param name="fromVolume">起点(线性 volume)。</param>
        /// <param name="toVolume">终点(线性 volume)。</param>
        /// <returns>初始状态(<c>Active = true</c>;起点 == 终点时 <c>Active = false</c>)。</returns>
        public static State Begin(double fromVolume, double toVolume)
            => Begin(fromVolume, toVolume, AudioTuning.RampSeconds);

        /// <summary>以指定时长开一段 ramp(时长参数仅供测试注入;生产走无参重载)。</summary>
        /// <param name="fromVolume">起点(线性 volume)。</param>
        /// <param name="toVolume">终点(线性 volume)。</param>
        /// <param name="durationSeconds">时长(秒);≤ 0 ⇒ 按 0 处理(直接到位)。</param>
        /// <returns>初始状态。</returns>
        public static State Begin(double fromVolume, double toVolume, double durationSeconds)
        {
            if (durationSeconds <= 0.0)
            {
                durationSeconds = 0.0;
            }

            return new State
            {
                StartVolume = fromVolume,
                TargetVolume = toVolume,
                DurationSeconds = durationSeconds,
                ElapsedSeconds = 0.0,
                Active = durationSeconds > 0.0 && fromVolume != toVolume,
            };
        }

        /// <summary>推进一步并返回当前 volume(线性;越界钳到终点)。
        /// <para>单帧推进量恒 ≤ <see cref="MaxStepForFrame"/>(斜率上限判据);
        /// <paramref name="dtSeconds"/> ≤ 0 ⇒ 不推进(返回当前值)。</para></summary>
        /// <param name="state">会话状态(ref 推进时间)。</param>
        /// <param name="dtSeconds">帧时长(秒)。</param>
        /// <returns>本帧 volume。</returns>
        public static double Step(ref State state, double dtSeconds)
        {
            if (dtSeconds < 0.0)
            {
                dtSeconds = 0.0;
            }

            if (!state.Active)
                return state.TargetVolume;

            state.ElapsedSeconds += dtSeconds;
            if (state.DurationSeconds <= 0.0 || state.ElapsedSeconds >= state.DurationSeconds)
            {
                state.ElapsedSeconds = state.DurationSeconds;
                state.Active = false;
                return state.TargetVolume;
            }

            double t = state.ElapsedSeconds / state.DurationSeconds;
            return state.StartVolume + (state.TargetVolume - state.StartVolume) * t;
        }

        /// <summary>**斜率上限**:时长匀速下任意 <paramref name="dtSeconds"/> 帧允许的最大 volume 推进量
        /// —— 测试断言「单帧 Δ ≤ maxΔ」的纯函数判据。</summary>
        /// <param name="state">会话状态。</param>
        /// <param name="dtSeconds">帧时长(秒)。</param>
        /// <returns>允许的最大 volume 推进量(绝对值;时长 0 ⇒ 返回全程差)。</returns>
        public static double MaxStepForFrame(in State state, double dtSeconds)
        {
            double total = System.Math.Abs(state.TargetVolume - state.StartVolume);
            if (state.DurationSeconds <= 0.0)
                return total;
            if (dtSeconds < 0.0)
                dtSeconds = 0.0;
            return total * (dtSeconds / state.DurationSeconds);
        }
    }
}
