// 权威来源:production/epics/audio-system/story-008-spatialization-world-breath.md(AC-44-D8 ①)
// GDD:design/gdd/audio-system.md F-44.1(隔门 ≠ 静音,是变闷)· 事件表 `occlusion_lowpass` 参数
// docs/engine-reference/unity/modules/audio.md §AudioLowPassFilter Component(长期稳定 API)
// ADR-015 §一(遮挡输入 = 视觉几何 raycast,派生态不进流)· ADR-018 §四
//
// ⚠️ **「穿门 ≠ 静音」是判据的核心**(GDD / AC-44-D8 ① 逐字):遮挡改变的是**音色**(低通),
//    不是**存在性** —— 故 `cutoff ≤ 0` 必须**拒绝**(那是静音,不是变闷),而 cutoff > 0 时
//    滤波**启用**但声音**照常出**。
// ⚠️ **遮挡判定源 = 44 本地 raycast**(视觉几何;**不进流**,ADR-015 两层纪律)——
//    本类**只消费**判定结果(bool),不做 raycast(那需要物理场景,归装配层)。
// ⚠️ **与组级滤波分工**:mixer 组上的 LowPass = 全组共享(逐 cue 带档位会互踩);
//    `AudioLowPassFilter` **组件是逐源**独立滤波(GDD F-44.1 :306-310)—— 本类管后者。
// ⚠️ **组件共位是引擎强制**:`AudioLowPassFilter` 带 `[RequireComponent(typeof(AudioSource))]`
//    —— 空 GameObject 上加它会失败并返回 null(实测:Unity 报
//    「Add required component of type 'AudioListener' or 'AudioSource' … first」)。
//    故本类**以 `AudioSource` 为入参**、按需在其 GameObject 上补挂滤波 —— 共位由引擎保证,
//    不靠调用方纪律。

using System;
using UnityEngine;

namespace DaYiJingCheng.Gameplay.Presentation.Audio
{
    /// <summary>逐源遮挡低通驱动器(AC-44-D8 ①)。
    /// <para>输入 = 目标声源 + 遮挡判定(bool,44 本地 raycast 的结果)+ `occlusion_lowpass`
    /// 参数值;输出 = 该源上滤波的启用与截止频率。</para>
    /// <para>⚠️ **不做 ramp** —— 硬下界 ramp(≥ 50 ms)归 <see cref="FilterRamp"/> 族
    /// (GDD F-44.1 注);本类是**语义**层(遮挡 ⇒ 变闷),ramp 化是执行层的事。</para>
    /// <example>
    /// var driver = new OcclusionDriver();
    /// driver.Apply(source, cutoffHz: 1200f, occluded: true);   // 变闷,但听得见
    /// driver.Apply(source, cutoffHz: 1200f, occluded: false);  // 回全通
    /// </example></summary>
    public sealed class OcclusionDriver
    {
        /// <summary>按遮挡判定施加 / 撤销低通。</summary>
        /// <param name="source">目标声源(null ⇒ <see cref="ArgumentNullException"/>)。
        /// 滤波组件按需挂在其 GameObject 上(引擎 `[RequireComponent]` 要求共位)。</param>
        /// <param name="cutoffHz">`occlusion_lowpass` 参数值(Hz)。遮挡为真时须 &gt; 0 ——
        /// ≤ 0 是静音语义,违「穿门 ≠ 静音」⇒ <see cref="ArgumentException"/>。</param>
        /// <param name="occluded">遮挡判定(44 本地 raycast 结果)。</param>
        public void Apply(AudioSource source, float cutoffHz, bool occluded)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            // ⚠️ 非泛型 GetComponent:泛型版在缺组件时会抛 MissingComponentException(实测)
            var filter = source.GetComponent(typeof(AudioLowPassFilter)) as AudioLowPassFilter;

            if (occluded)
            {
                if (!(cutoffHz > 0f))
                {
                    throw new ArgumentException(
                        $"遮挡时 cutoff 必须 > 0(实得 {cutoffHz})—— " +
                        "「穿门 ≠ 静音」:遮挡改变音色,不改变存在性(AC-44-D8 ①)",
                        nameof(cutoffHz));
                }

                // 拒绝路径**先于**挂组件 ⇒ 校验失败不留下半施用的组件。
                if (filter == null) filter = source.gameObject.AddComponent<AudioLowPassFilter>();
                filter.cutoffFrequency = cutoffHz;
                filter.enabled = true;
                return;
            }

            // 无遮挡 ⇒ 撤销滤波(走全通)。**不**把 cutoff 设 0(那会静音)。
            if (filter != null) filter.enabled = false;
        }
    }
}
