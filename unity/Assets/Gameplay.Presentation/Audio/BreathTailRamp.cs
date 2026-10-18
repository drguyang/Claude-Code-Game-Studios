// 权威来源:production/epics/audio-system/story-019-ac44-10-no-spike-sfx.md(面 10-b · AC-019-4/5)
// GDD design/gdd/audio-system.md:408-411(Output Range:呼吸层**连续**变化,无突变音效 AC-44-10)·
//   :571-573(急促 → 停顿连续变慢变浅;**停顿 = 极轻背景气流尾,停止 = 完全无声**)· :878(负面声明)
// ADR-018 §六(无提示音铁律:终局**不被播报**,只**消失**)
//
// ⚠️ **本文件是「停顿 vs 停止」在音频侧的唯一分界载体**(OQ-44-7 spike 的结论形态):
//   · **停顿(昏迷 / 转危)**:呼吸 loop **存续**,增益**指数衰减**到 `breath_tail_gain_floor_db`
//     —— 一阵极轻的背景气流尾,衰减**无 onset 拐点**(拐点 = 可听辨的「换了个声音」= 变相报状态);
//   · **停止(死亡 / 痊愈)**:loop `EndLoop`,**完全无声**,不播放任何尾音 —— 尾音是留给**活着**的病人的。
//   ⚠️ 若两态落到同一个声音形态上,玩家就只剩「有没有声音」这一个线索 ⇒ 承 §Edge Cases 的分辨要求即失效。
//
// ⚠️ **禁新 clip、禁新引擎 API**:衰减 = 既有 `AudioSource.volume` 上的一条增益曲线,
//   与 `VolumeRamp` 同族(同一线性 volume 域、同一逐帧写入纪律),但**不同类**:
//   `VolumeRamp` 是**线性匀速**交叉淡化(到达时长是判据),本类是**指数**衰减(倍率是判据)——
//   两者的斜率语义相反,合并会把「匀速」与「指数」两个判据搅成一条(承 ADR-024 §⑥ 单一语义纪律)。
//
// ⚠️ **指数底数是常量、不是旋钮**:`Exp` 底数是「衰减形状」(整条曲线同一个 e),调它等于调音色;
//   时长与终点才是旋钮(`AudioTuning.BreathTailRampMs` / `BreathTailGainFloorDb`,**数值归用户**)。
//
// ⚠️ 本类**不持有任何状态**(进程安全的纯函数 + 调用方持有的 `State`),不进三流、不进 sim
//   (ADR-009 派生态;ADR-018 §一 44 只渲染)。

using System;

namespace DaYiJingCheng.Gameplay.Presentation.Audio
{
    /// <summary>呼吸尾音的**指数增益衰减**(OQ-44-7 spike 结论 · AC-019-5 的载体)。
    /// <para>与 <see cref="VolumeRamp"/> 的分工:`VolumeRamp` = 匀速交叉淡化(ramp ≥ 50 ms 判据);
    /// 本类 = 指数尾巴(无拐点判据)。<b>共同点</b>:都只产出「本帧该写进
    /// <c>AudioSource.volume</c> 的值」,**禁直 set 目标值**。</para>
    /// <para>保证(全部可测):① 序列**单调不增**;② 相邻步**倍率恒等**(`v[k] = v0 · r^k`,
    /// 差分比值全等 ⇒ 无 onset 拐点);③ 终点 = 线性幅度(<c>10^(floorDb/20)</c>),到位后停;</para></summary>
    public static class BreathTailRamp
    {
        /// <summary>尾音衰减会话状态(线性 volume 域;由 <see cref="Begin"/> 开、<see cref="Step"/> 推进)。</summary>
        public struct State
        {
            /// <summary>起点(线性 volume;须 &gt; 0 —— ≤ 0 者无从衰减)。</summary>
            public double StartVolume;

            /// <summary>终点(线性 volume;= 10^(floorDb/20))。</summary>
            public double TargetVolume;

            /// <summary>总时长(秒;≤ 0 ⇒ 直接到位)。</summary>
            public double DurationSeconds;

            /// <summary>已推进时长(秒;到位后停表)。</summary>
            public double ElapsedSeconds;

            /// <summary>是否仍在推进(到位 = false)。</summary>
            public bool Active;
        }

        /// <summary>以 <see cref="AudioTuning.BreathTailRampSeconds"/> / <see cref="AudioTuning.BreathTailGainFloorDb"/>
        /// 开一段尾音(生产路径)。</summary>
        /// <param name="fromVolume">起点(线性 volume)。</param>
        /// <returns>初始状态。</returns>
        public static State Begin(double fromVolume)
            => Begin(fromVolume, AudioTuning.BreathTailRampSeconds, AudioTuning.BreathTailGainFloorDb);

        /// <summary>以指定时长 / 增益终值 dB 开一段尾音(参数注入仅供测试)。</summary>
        /// <param name="fromVolume">起点(线性 volume;≤ 0 或 NaN ⇒ 直接到位,不产生衰减)。</param>
        /// <param name="durationSeconds">总时长(秒;≤ 0 ⇒ 直接到位)。</param>
        /// <param name="floorDb">增益终值(dB;≤ 本值时直接到位)。</param>
        /// <returns>初始状态。</returns>
        public static State Begin(double fromVolume, double durationSeconds, double floorDb)
        {
            double target = DbToLinear(floorDb);
            bool degenerate = durationSeconds <= 0.0
                              || double.IsNaN(fromVolume) || fromVolume <= 0.0
                              || fromVolume <= target;

            return new State
            {
                StartVolume = degenerate ? target : fromVolume,
                TargetVolume = target,
                DurationSeconds = degenerate ? 0.0 : durationSeconds,
                ElapsedSeconds = 0.0,
                Active = !degenerate,
            };
        }

        /// <summary>推进一步并返回本帧该写进 <c>AudioSource.volume</c> 的值(线性)。
        /// <para>曲线见 <see cref="ValueAt"/>(单一出处);等 dt 推进时相邻步**倍率恒等**
        /// ⇒ 差分比值全等(「无拐点」的机器判据)。</para></summary>
        /// <param name="state">会话状态(ref 推进时间)。</param>
        /// <param name="dtSeconds">帧时长(秒;≤ 0 ⇒ 不推进,返回当前值)。</param>
        /// <returns>本帧 volume(线性)。</returns>
        public static double Step(ref State state, double dtSeconds)
        {
            if (dtSeconds < 0.0)
                dtSeconds = 0.0;

            if (!state.Active)
                return state.TargetVolume;

            state.ElapsedSeconds += dtSeconds;
            if (state.DurationSeconds <= 0.0 || state.ElapsedSeconds >= state.DurationSeconds)
            {
                state.ElapsedSeconds = state.DurationSeconds;
                state.Active = false;
                return state.TargetVolume;
            }

            return ValueAt(state.StartVolume, state.TargetVolume, state.ElapsedSeconds, state.DurationSeconds);
        }

        /// <summary>尾音曲线 = <c>v(t) = v0 · (floor/v0)^(t/T)</c> —— 等**对数时距**的几何衰减;
        /// 等 dt 推进时相邻步的**倍率恒等** ⇒ 差分比值全等(「无拐点」的机器判据)。
        /// <para>⚠️ 与 <see cref="Step"/> 共用同一曲线公式(单一出处),仅作用域不同:
        /// <see cref="Step"/> 推进并返回**衰减中**的值;本函数按绝对时刻取点(供装配层逐帧采样,不推状态)。</para></summary>
        /// <param name="startVolume">起点(线性 volume)。</param>
        /// <param name="targetVolume">终点(线性 volume)。</param>
        /// <param name="t">已推进时长(秒;clamp 到 [0,T])。</param>
        /// <param name="durationSeconds">总时长(秒)。</param>
        /// <returns>t 时刻的 volume(线性)。</returns>
        public static double ValueAt(double startVolume, double targetVolume, double t, double durationSeconds)
        {
            if (durationSeconds <= 0.0 || t >= durationSeconds) return targetVolume;
            if (t <= 0.0) return startVolume;
            return startVolume * Math.Pow(targetVolume / startVolume, t / durationSeconds);
        }

        /// <summary>dB → 线性幅度(`10^(db/20)`;纯函数,单一出处)。</summary>
        /// <param name="db">增益(dB)。</param>
        /// <returns>线性幅度。</returns>
        public static double DbToLinear(double db) => Math.Pow(10.0, db / 20.0);
    }
}
