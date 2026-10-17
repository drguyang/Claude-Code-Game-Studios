// S14-6 补口(2026-10-17)—— 听诊预载门的**真值适配器**。
//
// 权威来源:
//   GDD design/gdd/audio-system.md §Edge Cases 素材段 :640
//     「一次性 cue 允许极小迟发,但**不得**在听诊中因加载迟发(听诊窗预载)」
//   engine-reference `docs/engine-reference/unity/modules/audio.md:224-234`
//     「Preload Audio:`clip.LoadAudioData()` → `clip.loadState == AudioDataLoadState.Loaded`」
//   ADR-018 §一(44 只渲染)—— 本类只**读**载入态,不改变任何声音状态
//
// ⚠️ **为什么这一层此前不存在**(缺口来源):
//   `StethoscopePreloadGate` 是**纯谓词**,刻意自持 `ClipLoadState` 闭集、
//   **不引 `UnityEngine.AudioDataLoadState`**(见其 `:33-34`),以便 EditMode 无引擎面直驱动。
//   代价是 `AudioClip.loadState` → `ClipLoadState` 的映射**全库无人承担** ——
//   `pipeline_gate_test.cs:660` 的哨兵自己点名了这件缺失(「未覆盖:Unity 侧 loadState 枚举
//   与 ClipLoadState 的适配器」)。本类即该件。
//
// ⚠️ **为什么映射是显式 switch 而不是 `(ClipLoadState)(int)state`**:
//   两者今日数值恰同(`Unloaded=0 / Loading=1 / Loaded=2 / Failed=3`),但那是**巧合**,不是契约。
//   显式 switch 把「同一性」写成可审的声明;Unity 侧若增删状态,这里**编译期**暴露。
//   未知状态的兜底 = `Failed`(**保守方向**:放行会把静音误当「病人没啰音」,阻断只是晚进听诊)。
//
// ⚠️ **本类不改任何状态**:不调 `LoadAudioData()`、不碰 `AudioSource`。
//   预载动作的发起方归装配轮(谁在何时 `LoadAudioData`)—— 本类只回答「现在载到哪了」。

using System;
using UnityEngine;

namespace DaYiJingCheng.Gameplay.Presentation.Audio
{
    /// <summary>
    /// `AudioClip.loadState`(<see cref="AudioDataLoadState"/>)→
    /// <see cref="StethoscopePreloadGate.ClipLoadState"/> 的**唯一适配点**。
    /// <para>承 <see cref="StethoscopePreloadGate"/> 的查询缝形态:门持纯谓词,本类供真值。</para>
    /// </summary>
    public static class AudioClipLoadStateAdapter
    {
        /// <summary>Unity 载入态 → 门自持闭集。未知态兜底 <see cref="StethoscopePreloadGate.ClipLoadState.Failed"/>
        /// (**保守方向** —— 绝不把未知态当「已载」放行)。</summary>
        /// <param name="state">`AudioClip.loadState` 的取值。</param>
        public static StethoscopePreloadGate.ClipLoadState Map(AudioDataLoadState state)
        {
            switch (state)
            {
                case AudioDataLoadState.Unloaded: return StethoscopePreloadGate.ClipLoadState.Unloaded;
                case AudioDataLoadState.Loading:  return StethoscopePreloadGate.ClipLoadState.Loading;
                case AudioDataLoadState.Loaded:   return StethoscopePreloadGate.ClipLoadState.Loaded;
                case AudioDataLoadState.Failed:   return StethoscopePreloadGate.ClipLoadState.Failed;
                default:                          return StethoscopePreloadGate.ClipLoadState.Failed;
            }
        }

        /// <summary>单个 clip 的当前载入态。<paramref name="clip"/> 为 <c>null</c> ⇒
        /// <see cref="StethoscopePreloadGate.ClipLoadState.Failed"/>(**没有 clip ≠ 已载**)。</summary>
        /// <param name="clip">目标 clip(可为 null)。</param>
        public static StethoscopePreloadGate.ClipLoadState StateOf(AudioClip clip) =>
            clip == null
                ? StethoscopePreloadGate.ClipLoadState.Failed
                : Map(clip.loadState);

        /// <summary>
        /// 构造 <see cref="StethoscopePreloadGate"/> 需要的查询缝 —— cue id → <see cref="AudioClip"/>。
        /// <para>⚠️ <paramref name="resolve"/> 返回 <c>null</c>(该 cue 无 clip / 解析不到)⇒
        /// <see cref="StethoscopePreloadGate.ClipLoadState.Failed"/>,门**阻断**。
        /// 「查不到」绝不能被读成「已就绪」。</para>
        /// </summary>
        /// <param name="resolve">cue id → clip 的解析器(非 null)。</param>
        /// <exception cref="ArgumentNullException"><paramref name="resolve"/> 为 null。</exception>
        public static StethoscopePreloadGate.LoadStateQuery Query(Func<string, AudioClip> resolve)
        {
            if (resolve == null) throw new ArgumentNullException(nameof(resolve));
            return cueId => StateOf(resolve(cueId));
        }
    }
}
