// 权威来源:production/epics/audio-system/story-004-breath-layers-precision-tiers.md(Implementation Notes)
//   · 形态裁定(2026-09-26):**loop + 逐周期相位门控**(非 one-shot;002 规则 8 强制 loop: true)
//   · 相位旗复位跟着基础层 timeSamples(回绕**或** Seek 跳变都要复位)
//   · **禁协程 / 定时器**排附加层(帧率与 DSP 不同源 ⇒ 漂进吸气中段 = 医学反相,AC-44-08 红)
// GDD:design/gdd/audio-system.md §Edge Cases 相位条(:576-588)· AC-44-08 · AC-44-14
//   · :312「所有滤波/噪声底变化 ramp ≥ 50 ms」· :562 禁「啪」一声切换
// ADR-018 §四 · TR-audio-005
//
// ⚠️ **每帧 Tick 驱动**(调用方 = Update;禁 Coroutine / Invoke / 计时器)——
//    帧循环只**采样**相位真值(timeSamples = 音频线程真相),不**生成**时间。
// ⚠️ **Q6 裁定(2026-09-26,loop 源上禁用 PlayScheduled)**:原实现每周期
//    `PlayScheduled(windowStartDsp)` 已**删除** —— 对已 playing 的 loop 源调
//    `PlayScheduled` 通常是重新计划起播(等价重启),计划点已过则「尽快起播」无补偿
//    (**推断,未实测**);engine-reference 对 `PlayScheduled` / `SetScheduledStartTime` /
//    `dspTime` / `timeSamples` **零覆盖** ⇒ 登记 OQ-44-5 同族 spike。
//    可选改法①(独立 one-shot 源)与 002 规则 8 的 `loop: true` 冲突,**弃**;
//    取改法② = **timeSamples 相位检测 + 音量门控,不重启**,接受 ≤ 1 帧粒度。
//    形态裁定(loop + 逐周期相位门控)不变。
// ⚠️ **Q7 裁定(2026-09-26,门控音量必须 ramp)**:窗口进 / 出**各走一次**
//    `FilterRamp`(≥ `AudioTuning.RampSeconds`),**不裸切 0/1**(幅度不连续 = click,
//    违 GDD :312 / :562)。底座 = `FilterRamp`(与 006 共用),**消费与断言归 004**。

using UnityEngine;

namespace DaYiJingCheng.Gameplay.Presentation.Audio
{
    /// <summary>呼吸两层传输的**最小读写面**(基础层只读;附加层读 + 门控音量)。
    /// <para>生产实现 = <see cref="AudioSourceLayerTransport"/>(包装 <c>AudioSource</c>);
    /// EditMode 测试注入记录型假件(零 <c>AudioSource</c> 依赖)。</para>
    /// <para>⚠️ **无 <c>PlayScheduled</c> 成员**(Q6 裁定:loop 源上禁用,见文件头;
    /// 接口面**刻意不保留**未使用成员 —— 死接口面是误用磁铁,spike 结论落地时再按需增回)。</para></summary>
    public interface IBreathLayerTransport
    {
        /// <summary><c>AudioSource.timeSamples</c> —— 播放头采样位置(相位唯一真值来源)。</summary>
        int TimeSamples { get; }

        /// <summary>clip 采样率(Hz;<c>AudioClip.frequency</c>)。</summary>
        int Frequency { get; }

        /// <summary>clip 时长(秒;<c>AudioClip.length</c>)—— **相位分母,不是旋钮 T**。</summary>
        double ClipLengthSeconds { get; }

        /// <summary>音量(0..1)—— 相位门控的输出端(附加层 loop 的逐周期开关,经 ramp)。</summary>
        float Volume { get; set; }
    }

    /// <summary>呼吸两层的**相位门控驱动**(Story 004 运行期执行面)。
    /// <para>职责:① 采样基础层 <c>timeSamples</c> 求真实周期相位;② 回绕 / Seek 复位触发旗;
    /// ③ 窗口判定(<see cref="BreathPhaseGate"/>,单一映射路径);④ 附加层音量门控 ——
    /// 进 / 出窗口各走一次 <see cref="FilterRamp"/>(≥50 ms,Q7)。
    /// **不重启循环 · 不持有档位 · 不起协程**。</para>
    /// <example>
    /// // Update 里:
    /// driver.Tick(AudioSettings.dspTime, triggerPhase, jitter, AudioTuning.InspireFraction);
    /// </example></summary>
    public sealed class BreathLayerDriver
    {
        private readonly IBreathLayerTransport _baseTransport;
        private readonly IBreathLayerTransport _adventitiousTransport;

        private double _previousPhase;
        private bool _hasPreviousPhase;
        private double _lastTickDspSeconds;
        private bool _hasLastTick;
        private bool _firedThisCycle;

        private FilterRamp.State _gateRamp;
        private double _gateVolume;

        /// <summary>当前周期相位(最近一次 Tick 求得;0 = 尚未 Tick)。</summary>
        public double CurrentPhase { get; private set; }

        /// <summary>本周期是否已发过(回绕 / Seek 时复位)。</summary>
        public bool FiredThisCycle => _firedThisCycle;

        /// <summary>附加层是否处于相位窗内(门控 ramp 的目标态切换依据)。</summary>
        public bool AdventitiousInWindow { get; private set; }

        /// <summary>门控音量当前值(经 ramp;测试与调试读)。</summary>
        public float GateVolume => (float)_gateVolume;

        /// <summary>门控 ramp 已推进时长(秒)——「进 / 出窗各 ≥50 ms」断言读它(Q7 可测点)。</summary>
        public double GateRampElapsedSeconds => _gateRamp.ElapsedSeconds;

        /// <summary>门控 ramp 是否仍在推进。</summary>
        public bool GateRampActive => _gateRamp.Active;

        /// <summary>构造驱动(注入两个传输;null ⇒ ArgumentNullException)。</summary>
        /// <param name="baseTransport">基础气流层(只读相位)。</param>
        /// <param name="adventitiousTransport">附加音层(ramp 化音量门控)。</param>
        public BreathLayerDriver(IBreathLayerTransport baseTransport, IBreathLayerTransport adventitiousTransport)
        {
            _baseTransport = baseTransport ?? throw new System.ArgumentNullException(nameof(baseTransport));
            _adventitiousTransport = adventitiousTransport ??
                                     throw new System.ArgumentNullException(nameof(adventitiousTransport));
        }

        /// <summary>每帧驱动(**由 Update 调;禁协程 / 定时器**)。
        /// <para>流程:相位 → 复位判定 → 窗口判定 → 边沿则开新门控 ramp → 逐帧推进
        /// (dt = dspTime 墙钟差)→ 写附加层音量。</para></summary>
        /// <param name="nowDspSeconds"><c>AudioSettings.dspTime</c>(秒)。</param>
        /// <param name="triggerPhase">相对吸气段的分数(事件表 <c>adventitious_policy</c>)。</param>
        /// <param name="jitter">相位抖动(分数幅值)。</param>
        /// <param name="inspireFraction"><c>INSPIRE_FRACTION</c>(生产传
        /// <see cref="AudioTuning.InspireFraction"/>)。</param>
        public void Tick(double nowDspSeconds, double triggerPhase, double jitter, double inspireFraction)
        {
            double clipLength = _baseTransport.ClipLengthSeconds;
            CurrentPhase = BreathPhaseGate.CyclePhase(
                _baseTransport.TimeSamples, _baseTransport.Frequency, clipLength);

            if (_hasPreviousPhase)
            {
                // 期望推进 = 墙钟经过 / clip.length,余量 2 倍(帧抖动安全系数)
                double maxAdvance = double.MaxValue;
                if (_hasLastTick && clipLength > 0.0)
                {
                    double wall = nowDspSeconds - _lastTickDspSeconds;
                    maxAdvance = (wall > 0.0 ? wall : 0.0) / clipLength * 2.0;
                }

                if (BreathPhaseGate.ShouldResetCycleFlag(_previousPhase, CurrentPhase, maxAdvance))
                    _firedThisCycle = false;   // 回绕或 Seek 跳变都要复位(形态裁定)
            }

            if (BreathPhaseGate.ShouldFire(CurrentPhase, triggerPhase, jitter,
                    inspireFraction, _firedThisCycle))
                _firedThisCycle = true;

            BreathPhaseGate.WindowCycleFraction(triggerPhase, jitter, inspireFraction,
                out double windowStart, out double windowEnd);
            bool inWindow = CurrentPhase >= windowStart && CurrentPhase <= windowEnd;

            double dt = _hasLastTick ? nowDspSeconds - _lastTickDspSeconds : 0.0;
            if (dt < 0.0) dt = 0.0;

            if (inWindow != AdventitiousInWindow)
            {
                // 进 / 出窗口边沿:各开一次 ≥ RampSeconds 的门控 ramp(Q7:禁裸 0/1)
                AdventitiousInWindow = inWindow;
                _gateRamp = FilterRamp.Begin(_gateVolume, inWindow ? 1.0 : 0.0);
            }

            if (_gateRamp.Active)
                _gateVolume = FilterRamp.Step(ref _gateRamp, dt);

            _adventitiousTransport.Volume = (float)_gateVolume;

            _previousPhase = CurrentPhase;
            _hasPreviousPhase = true;
            _lastTickDspSeconds = nowDspSeconds;
            _hasLastTick = true;
        }

        /// <summary>停止 / 跳转后调用:清相位基线、触发旗与门控(下一次 Tick 重建;
        /// 与 timeSamples 复位语义一致)。</summary>
        public void ResetState()
        {
            _hasPreviousPhase = false;
            _hasLastTick = false;
            _firedThisCycle = false;
            AdventitiousInWindow = false;
            CurrentPhase = 0.0;
            _gateRamp = default;
            _gateVolume = 0.0;
            _adventitiousTransport.Volume = 0f;
        }
    }

    /// <summary><see cref="IBreathLayerTransport"/> 的 <c>AudioSource</c> 生产适配(薄壳)。
    /// <para>不读 <c>AudioSource.time</c>(首个 DSP 回调前为 0,不能作首周期判据,Q4)——
    /// 相位一律走 <c>timeSamples</c>。</para></summary>
    public sealed class AudioSourceLayerTransport : IBreathLayerTransport
    {
        private readonly AudioSource _source;

        /// <summary>包装一个 <c>AudioSource</c>(基础层或附加层)。</summary>
        /// <param name="source">目标源(须已挂 clip;null ⇒ ArgumentNullException)。</param>
        public AudioSourceLayerTransport(AudioSource source)
        {
            _source = source != null ? source : throw new System.ArgumentNullException(nameof(source));
        }

        /// <inheritdoc/>
        public int TimeSamples => _source.timeSamples;

        /// <inheritdoc/>
        public int Frequency => _source.clip != null ? _source.clip.frequency : 0;

        /// <inheritdoc/>
        public double ClipLengthSeconds => _source.clip != null ? _source.clip.length : 0.0;

        /// <inheritdoc/>
        public float Volume
        {
            get => _source.volume;
            set => _source.volume = value;
        }
    }
}
