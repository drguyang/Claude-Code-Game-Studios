// `mono_enabled` 的**实际施加面**(AC-44-13 / AC-44-12 ③-a · 2026-10-29)。
//
// ── 病因(实测,2026-10-29)────────────────────────────────────────────────────
// `mono_enabled` 在设置注册表(`AudioSettingsRegistry:103`)有条目、在
// `MixerRegistry:52` 有常量、在 42 设置壳有 UI 行 —— **三处都对**。
// 但全库**零消费方**:`grep MonoParameter` 生产侧只命中那两处**声明**。
// ⇒ 玩家改 mono **不产生任何声学变化**。
//
// ── 为什么不能像总线音量那样 `mixer.SetFloat`(关键实测)──────────────────────
// 我最初按债册写的「`.mixer` 加 `mono_enabled` 暴露参数」去做,实测推翻了该假设:
//   · `DaYiJingCheng.mixer` 的 `m_ExposedParameters` 12 员**全是**音量/滤波类,
//     每个 guid 都指向某个 `AudioMixerGroupController.m_Volume`;
//   · `monodis` 反汇编 `UnityEngine.AudioModule.dll`(Unity 6.3.24f1):
//     `AudioListener` **无** forceMono 类成员(只有 volume / pause / velocityUpdateMode /
//     GetOutputData / GetSpectrumData);`AudioSettings.speakerMode` **已标 deprecated**,
//     官方修法 = `GetConfiguration()` → 改 `speakerMode` → `Reset(config)`。
// ⇒ **Unity 6.3 的 mono 是设备级 `speakerMode`,不是混音器参数。**
//    把它做成 `.mixer` 暴露参数会是**一个永远不生效的假开关**。
//
// ── 本件的形状 ──────────────────────────────────────────────────────────────
// 纯函数式施加器 + 注入缝(承 `PlayerBusVolumeInitializer.MixerResolver` 同款模式):
//   · 不持状态(值真源 = `AudioSettingsStore`);
//   · 设备查询 / 重置走**注入委托**,EditMode 可测而不必真改音频设备;
//   · 生产实现走 `AudioSettings.GetConfiguration` / `Reset`。
//
// 权威来源:`design/gdd/audio-system.md:782`(mono 开/关)· `:933`(注册表第 8 行)
//   · `AC-44-13`(mono 选项)· `AC-44-12` ③-a(44 自己的非视觉义务)
//   · `docs/engine-reference/unity/VERSION.md`(6.3 LTS,post-cutoff ⇒ 以上 API 均实测)

using System;
using UnityEngine;

namespace DaYiJingCheng.Gameplay.Presentation.Audio
{
    /// <summary><c>mono_enabled</c> → 音频输出设备 <c>speakerMode</c> 的施加器。
    /// <para><b>不持状态</b>:每次施加都从设备当前配置出发(读 → 改 → 写回),
    /// 与「值真源 = <see cref="Settings.AudioSettingsStore"/>」的单向推送模型一致。</para>
    /// <para>⚠️ <b>关 mono 时还原成什么</b>:还原成**设备当前报告的非 Mono 模式**
    /// (即 <c>GetConfiguration()</c> 读回的值)—— 不是硬编码 <c>Stereo</c>。
    /// 硬编码会把玩家的 5.1 / 7.1 设备悄悄降成立体声,那是**静默的设备降级**。</para></summary>
    public static class MonoOutputApplier
    {
        /// <summary>读当前 <c>speakerMode</c>(注入缝;null ⇒ 用 <see cref="DefaultGetSpeakerMode"/>)。</summary>
        public static Func<AudioSpeakerMode> GetSpeakerMode;

        /// <summary>写回 <c>speakerMode</c>(注入缝;返回错误串,null ⇒ 成功)。
        /// <para>null ⇒ 用 <see cref="DefaultSetSpeakerMode"/>。</para></summary>
        public static Func<AudioSpeakerMode, string> SetSpeakerMode;

        /// <summary>上次「开 mono 前」的非 Mono 模式(用于关 mono 时还原)。
        /// <para>⚠️ 进程级记忆:**这是设备态缓存,不是设置值**。设置值仍只有
        /// <c>mono_enabled</c> 一个真源(在 store 里);本字段只记「还原目标」。</para></summary>
        private static AudioSpeakerMode? _preMonoMode;

        /// <summary>施加 <c>mono_enabled</c>。</summary>
        /// <param name="enabled">true = 下混到单声道;false = 还原到 mono 前的设备模式。</param>
        /// <returns>错误串(空 = 已施加);不抛 —— 设备不可用属可降级面。</returns>
        public static string Apply(bool enabled)
        {
            Func<AudioSpeakerMode> getter = GetSpeakerMode ?? DefaultGetSpeakerMode;
            Func<AudioSpeakerMode, string> setter = SetSpeakerMode ?? DefaultSetSpeakerMode;

            if (enabled)
            {
                AudioSpeakerMode current = getter();
                // 已在 Mono ⇒ 无变化(幂等)。⚠️ 不覆盖 _preMonoMode —— 否则「开两次」
                // 会把还原目标记成 Mono 自身,关 mono 时还原到 Mono = 关不掉。
                if (current == AudioSpeakerMode.Mono) return string.Empty;

                _preMonoMode = current;
                return setter(AudioSpeakerMode.Mono) ?? string.Empty;
            }

            // 关 mono:还原到开之前的模式;从未开过 ⇒ 已是原生态,无变化。
            if (!_preMonoMode.HasValue) return string.Empty;
            AudioSpeakerMode target = _preMonoMode.Value;
            _preMonoMode = null;
            return setter(target) ?? string.Empty;
        }

        /// <summary>**仅供测试**清空还原记忆(防跨夹具残留 —— 它是静态态)。</summary>
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static void ResetForTest()
        {
            _preMonoMode = null;
            GetSpeakerMode = null;
            SetSpeakerMode = null;
        }

        /// <summary>生产实现:读设备当前 <c>speakerMode</c>。
        /// <para>⚠️ 6.3 已把 <c>AudioSettings.speakerMode</c> 标 deprecated,但**读**仍可用;
        /// 官方修法只针对**写**(走 <c>GetConfiguration</c> + <c>Reset</c>)。
        /// 本处读用它,写走 <see cref="DefaultSetSpeakerMode"/>。</para></summary>
        private static AudioSpeakerMode DefaultGetSpeakerMode() => AudioSettings.speakerMode;

        /// <summary>生产实现:改 <c>AudioConfiguration.speakerMode</c> 后 <c>AudioSettings.Reset</c>。
        /// <para>⚠️ <b>Reset 会造成主线程停顿</b>(官方文档逐字:「can cause main thread stalls if
        /// <c>AudioSettings.Reset</c> is called when objects are loading asynchronously」)⇒
        /// 只在玩家显式改 mono 时调一次,<b>不得</b>放进每帧 / 每 tick 路径。</para></summary>
        private static string DefaultSetSpeakerMode(AudioSpeakerMode mode)
        {
            try
            {
                AudioConfiguration config = AudioSettings.GetConfiguration();
                config.speakerMode = mode;
                AudioSettings.Reset(config);
                return null;
            }
            catch (Exception ex)
            {
                // 设备不支持该模式 ⇒ 可降级面,不 fail-hard(但**不静默**:返回错误串由调用方登记)。
                return $"[mono] 设置 speakerMode={mode} 失败:{ex.Message}";
            }
        }
    }
}
