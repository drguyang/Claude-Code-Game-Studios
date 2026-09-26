// 权威来源:production/epics/audio-system/story-004-breath-layers-precision-tiers.md
//          (Acceptance Criteria AC-44-01 ② · Implementation Notes)
//   · tier 滤波 / 噪声底参数 = **exposed + 脚本驱动,禁入任何快照**(GDD :194-201 ——
//     否则 DialogueFocus → Default 回退会把档位拉回出厂);断言**复用** 003 的
//     MixerTopologyGates.ValidateSnapshotExposedDisjoint(不另写判据)
//   · RampSeconds = 0.05f 单处常量(dB/oct 对数轴自插值 + 逐帧 SetFloat;禁直 set 目标值)
// GDD:design/gdd/audio-system.md F-44.1(:278,组级共享 :306-310)· F-44.2(SNR 以 Stethoscope
//   总线增益实现)· :194-201(快照不碰档位)· AC-44-04(ramp ≥ 50 ms)· AC-44-05(参数闭集)
// docs/engine-reference/unity/modules/audio.md(SetFloat 两参 / TransitionTo 曲线不透明)
// ADR-018 §三/§四 · TR-audio-005
//
// ⚠️ **组级共享,逐 cue 带档位会互踩**(GDD :306-310)⇒ 档位是设备级状态,经 exposed 参数
//    全组一份;**逐源 AudioLowPassFilter 仅作例外**(基础层与附加层要不同通带)——
//    本驱动只管组级 exposed 参数面。
// ⚠️ **禁拿 TransitionTo(0.05f) 当 ramp 达标**(曲线引擎内部、未文档化 = 假绿)——
//    ramp 由 FilterRamp 对数轴自插值 + 逐帧 SetFloat 兑现(本文件是消费方)。
// ⚠️ 参数**值** = 事件表 tier_map 行(数据归用户调;本驱动只负责 ramp 化到位,不持档位语义)。

using System;
using UnityEngine;
using UnityEngine.Audio;

namespace DaYiJingCheng.Gameplay.Presentation.Audio
{
    /// <summary>混音参数写入的**最小面**(EditMode 测试注入记录型假件;
    /// 生产实现 = <see cref="AudioMixerParameterSink"/>)。</summary>
    public interface IMixerParameterSink
    {
        /// <summary>写一个 exposed 参数(<c>AudioMixer.SetFloat</c> 同义)。</summary>
        /// <param name="parameterName">exposed 参数名(∈ <see cref="MixerRegistry.TierFilterParameters"/>)。</param>
        /// <param name="value">目标值(Hz 或 dB,视参数而定)。</param>
        void SetFloat(string parameterName, float value);
    }

    /// <summary>tier 滤波 / 噪声底参数的 **ramp 化脚本驱动**(Story 004 · AC-44-01 ② · Implementation Notes)。
    /// <para>输入 = 目标值(<c>tier_map</c> 行,数据归用户调);输出 = 经
    /// <see cref="FilterRamp"/> 斜坡到位后的逐帧 <c>SetFloat</c>。
    /// 首次 <see cref="SetTargets"/> 直接落位(无历史值可斜坡),之后每次改目标 = 走
    /// <see cref="AudioTuning.RampSeconds"/> 斜坡。</para>
    /// <example>
    /// var sink = new AudioMixerParameterSink(audioMixer);
    /// var driver = new TierFilterDriver(sink);
    /// driver.SetTargets(centerHz, widthHz, noiseDb, contactDb, signalDb);  // 首次直落
    /// // Update:
    /// driver.SetTargets(newCenter, ...);  // 档位变化 → 自动 ramp ≥ 50 ms
    /// driver.Tick(Time.deltaTime);
    /// </example></summary>
    public sealed class TierFilterDriver
    {
        private readonly IMixerParameterSink _mixer;

        // 轴:频率 = octave(log2 Hz);dB 本身是对数域 ⇒ 直接用原值当轴
        private double _centerAxis;
        private double _widthAxis;
        private double _noiseAxis;
        private double _contactAxis;
        private double _signalAxis;

        private FilterRamp.State _centerRamp;
        private FilterRamp.State _widthRamp;
        private FilterRamp.State _noiseRamp;
        private FilterRamp.State _contactRamp;
        private FilterRamp.State _signalRamp;

        private bool _initialized;
        private bool _dirty;

        /// <summary>构造驱动(注入参数写入面;null ⇒ ArgumentNullException)。</summary>
        /// <param name="mixer">exposed 参数写入面(生产 = <see cref="AudioMixerParameterSink"/>)。</param>
        public TierFilterDriver(IMixerParameterSink mixer)
        {
            _mixer = mixer ?? throw new ArgumentNullException(nameof(mixer));
        }

        /// <summary>是否仍有 ramp 在推进(测试与收口用)。</summary>
        public bool IsRamping =>
            _centerRamp.Active || _widthRamp.Active || _noiseRamp.Active ||
            _contactRamp.Active || _signalRamp.Active;

        /// <summary>设置目标值(<c>tier_map</c> 行;数值归用户调)。
        /// 首次调用直接落位;其后走 <see cref="AudioTuning.RampSeconds"/> 斜坡(轴域见文件头)。</summary>
        /// <param name="passbandCenterHz">通带中心(Hz;须 &gt; 0)。</param>
        /// <param name="passbandWidthHz">通带宽(Hz;须 &gt; 0)。</param>
        /// <param name="noiseFloorDb">噪声底(dB)。</param>
        /// <param name="contactNoiseFloorDb">接触噪声底(dB)。</param>
        /// <param name="signalDb">信号基准(dB;TierMap 第 6 列)。</param>
        public void SetTargets(
            double passbandCenterHz, double passbandWidthHz,
            double noiseFloorDb, double contactNoiseFloorDb, double signalDb)
        {
            if (!(passbandCenterHz > 0.0) || !(passbandWidthHz > 0.0))
            {
                Debug.LogError("[TierFilterDriver] 通带中心 / 宽须 > 0 Hz —— 本次目标忽略" +
                               "(不启动会退到 1 Hz 的假 ramp)");
                return;
            }

            double centerAxis = FilterRamp.HertzToOctaveAxis(passbandCenterHz);
            double widthAxis = FilterRamp.HertzToOctaveAxis(passbandWidthHz);

            if (!_initialized)
            {
                _centerAxis = centerAxis;
                _widthAxis = widthAxis;
                _noiseAxis = noiseFloorDb;
                _contactAxis = contactNoiseFloorDb;
                _signalAxis = signalDb;
                _initialized = true;
                WriteCurrent();
                return;
            }

            _centerRamp = FilterRamp.Begin(_centerAxis, centerAxis);
            _widthRamp = FilterRamp.Begin(_widthAxis, widthAxis);
            _noiseRamp = FilterRamp.Begin(_noiseAxis, noiseFloorDb);
            _contactRamp = FilterRamp.Begin(_contactAxis, contactNoiseFloorDb);
            _signalRamp = FilterRamp.Begin(_signalAxis, signalDb);
            _dirty = true;
        }

        /// <summary>每帧推进(由 Update 调;dt ≤ 0 忽略本帧)。全部到位后停止写入。</summary>
        /// <param name="dtSeconds">帧时长(秒;<c>Time.deltaTime</c>)。</param>
        public void Tick(double dtSeconds)
        {
            if (!_initialized)
                return;
            if (dtSeconds < 0.0)
            {
                Debug.LogError("[TierFilterDriver] dt < 0 —— 忽略本帧");
                return;
            }

            if (!_dirty)
                return;

            if (_centerRamp.Active) _centerAxis = FilterRamp.Step(ref _centerRamp, dtSeconds);
            if (_widthRamp.Active) _widthAxis = FilterRamp.Step(ref _widthRamp, dtSeconds);
            if (_noiseRamp.Active) _noiseAxis = FilterRamp.Step(ref _noiseRamp, dtSeconds);
            if (_contactRamp.Active) _contactAxis = FilterRamp.Step(ref _contactRamp, dtSeconds);
            if (_signalRamp.Active) _signalAxis = FilterRamp.Step(ref _signalRamp, dtSeconds);

            WriteCurrent();
            _dirty = IsRamping;
        }

        /// <summary>把当前轴值写进 exposed 参数(轴 → 物理量变换的**唯一落点**)。</summary>
        private void WriteCurrent()
        {
            // 索引 = MixerRegistry.TierFilterParameters 的登记序(单一出处,禁另抄名字)
            _mixer.SetFloat(MixerRegistry.TierFilterParameters[0],
                (float)FilterRamp.OctaveAxisToHz(_centerAxis));
            _mixer.SetFloat(MixerRegistry.TierFilterParameters[1],
                (float)FilterRamp.OctaveAxisToHz(_widthAxis));
            _mixer.SetFloat(MixerRegistry.TierFilterParameters[2], (float)_noiseAxis);
            _mixer.SetFloat(MixerRegistry.TierFilterParameters[3], (float)_contactAxis);
            _mixer.SetFloat(MixerRegistry.TierFilterParameters[4], (float)_signalAxis);
        }
    }

    /// <summary><see cref="IMixerParameterSink"/> 的 <c>AudioMixer</c> 生产适配(薄壳)。
    /// <para><c>SetFloat(string, float)</c> 两参、无 transitionTime(engine-reference 3a 补录)——
    /// 斜坡只由 <see cref="FilterRamp"/> 提供。</para></summary>
    public sealed class AudioMixerParameterSink : IMixerParameterSink
    {
        private readonly AudioMixer _mixer;

        /// <summary>包装一个 <c>AudioMixer</c>(null ⇒ ArgumentNullException)。</summary>
        /// <param name="mixer">目标混音器。</param>
        public AudioMixerParameterSink(AudioMixer mixer)
        {
            _mixer = mixer != null ? mixer : throw new ArgumentNullException(nameof(mixer));
        }

        /// <inheritdoc/>
        public void SetFloat(string parameterName, float value)
            => _mixer.SetFloat(parameterName, value);
    }
}
