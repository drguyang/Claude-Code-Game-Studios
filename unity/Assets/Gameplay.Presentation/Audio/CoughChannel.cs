// 权威来源:production/epics/audio-system/story-004-breath-layers-precision-tiers.md(AC-44-08 ③)
//   · 咳嗽 cue **不得**调用 EndLoop 停呼吸循环(否则「咳嗽 ⇒ 停呼吸 ⇒ 重启」=
//     临床「啰音被咳掉了」,直接违 AC-44-08「咳嗽后不消失」)
// GDD:design/gdd/audio-system.md §Edge Cases 听诊与呼吸层(:574-577,咳嗽走独立一次性通道)
// ADR-018 §四(需求① 医学准确性)· TR-audio-005
//
// ⚠️ **结构上无 EndLoop 路径**:本类唯一方法只调 <c>sink.Emit</c>(一次性通道)——
//    行为主判据 = `breath_layers_test.test_coughNeverEndsBreathLoop_zeroEndLoopCalls`
//    (记录型 IAudioCueSink 假件,unity-specialist Q5);IL 扫描仅作辅助粗 guard(未启用)。

using System;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Presentation.Audio
{
    /// <summary>咳嗽(一次性 cue)派发通道 —— AC-44-08 ③ 的执行体。
    /// <para>GDD §Edge Cases :574-577:附加音层播放中收到咳嗽 cue 时,**呼吸循环继续**;
    /// 咳嗽走独立一次性通道(Voice 总线)。本类是 44 侧咳嗽派发的**唯一入口**,
    /// 呼吸循环的句柄归唯一持有者(上游)收尾 —— 本类**碰不到** <c>EndLoop</c>。</para>
    /// <example>
    /// CoughChannel.Dispatch(sink, new AudioCueDto(coughCueId, 0, default, sourceId, false));
    /// </example></summary>
    public static class CoughChannel
    {
        /// <summary>派发一次咳嗽(经 <c>Emit</c> 一次性通道;**绝不**触碰任何循环句柄)。</summary>
        /// <param name="sink">音频 cue 界面(ADR-018 <c>IAudioCueSink</c>;null ⇒ ArgumentNullException)。</param>
        /// <param name="coughCue">咳嗽 cue(一次性;<c>Looped = false</c>)。</param>
        public static void Dispatch(IAudioCueSink sink, in AudioCueDto coughCue)
        {
            if (sink == null) throw new ArgumentNullException(nameof(sink));
            sink.Emit(coughCue);
        }
    }
}
