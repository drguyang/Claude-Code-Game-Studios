// S14-6 · story-010 —— 听诊窗预载门(AC「听诊窗预载」的生产谓词)。
//
// 权威来源:
//   GDD design/gdd/audio-system.md §Edge Cases 素材段 :640
//     「一次性 cue 允许极小迟发,但**不得**在听诊中因加载迟发(听诊窗预载)」
//   AC「听诊窗预载」:进入 `StethoscopeFocus` 前听诊族 cue `loadState == Loaded`(未就绪不进听诊)
//   ADR-018 §一(44 只渲染)—— 本类只回答「能不能进」,不改变任何声音状态
//   engine-reference `audio.md`:AudioClip.LoadAudioData() / loadState
//
// ⚠️ **为什么要有查询缝(`LoadStateQuery`)**:
//   生产路径的真值是 `AudioClip.loadState`(UnityEngine.Audio)。但
//   ① 素材本体归 content 批(本 story Out of Scope ⇒ **全库零真 wav 之前,真 clip 半边不可跑**);
//   ② 本类是**纯谓词**(注入式依赖,承 `PlayerBusVolumeInitializer.MixerResolver` 同款模式),
//      可在 EditMode 无引擎面直驱动。
//   ⇒ 查询缝是**唯一**进入真值的方式;`AudioClip` 的适配器(一行 lambda)归装配轮。
//   **真 `AudioClip.loadState` 的 PlayMode 半边 = NOT-RUN**(见 evidence 原件 §7)。
//
// ⚠️ **听诊族 cue 集 = 表里 `bus == Stethoscope` 的行**(不是另立一份 cue 名清单 ——
//   ADR-024 §⑥ 单一出处)。`MixerRegistry.BusNames` 含 "Stethoscope";
//   `StethoscopeBus` 常量与之的一致性由测试钉住(`Does.Contain`),避免两处字面量分叉。
//
// ⚠️ **空集 = 构造期硬失败**(不是「恒真放行」):听诊族 cue 集为空 ⇒ 门对任何输入都返回
//   「就绪」= 一条永不转红的假绿。与 `AudioEventTableGates.ValidateRequiredCues` 同一纪律。

using System;
using System.Collections.Generic;

namespace DaYiJingCheng.Gameplay.Presentation.Audio
{
    /// <summary>听诊窗预载门 —— 「进听诊之前,听诊族 cue 是否全部已载」的唯一判据。</summary>
    public sealed class StethoscopePreloadGate
    {
        /// <summary>clip 载入态(本类自持的闭集 —— **不引 `UnityEngine.AudioDataLoadState`**,
        /// 以保持本类零引擎依赖、可在 EditMode 直驱动;适配器归装配轮)。</summary>
        public enum ClipLoadState
        {
            /// <summary>未载入(冷启动常态)。</summary>
            Unloaded = 0,

            /// <summary>载入中(`LoadAudioData()` 已发、未完成)。</summary>
            Loading = 1,

            /// <summary>已载入 —— **唯一**放行态。</summary>
            Loaded = 2,

            /// <summary>载入失败(不可重试成功;放行会把静音误当「病人没啰音」)。</summary>
            Failed = 3,
        }

        /// <summary>cue → 载入态 的查询缝(生产 = `AudioClip.loadState` 适配器;测试 = 注入表)。</summary>
        /// <param name="cueId">cue id(如 <c>BreathLayer_Base_Calm</c>)。</param>
        public delegate ClipLoadState LoadStateQuery(string cueId);

        /// <summary>听诊总线名(与 `MixerRegistry.BusNames` 的成员一致性由测试钉住)。</summary>
        public const string StethoscopeBus = "Stethoscope";

        private readonly string[] _requiredCues;
        private readonly LoadStateQuery _query;

        /// <summary>构造门。</summary>
        /// <param name="requiredCues">听诊族 cue 集(**非空**;见 <see cref="FromTable"/>)。</param>
        /// <param name="query">载入态查询缝(非 null —— 无查询 = 无法证明已载 = 不得放行)。</param>
        /// <exception cref="ArgumentNullException"><paramref name="requiredCues"/> / <paramref name="query"/> 为 null。</exception>
        /// <exception cref="ArgumentException"><paramref name="requiredCues"/> 为空集
        /// (空集会让 <see cref="IsReady"/> 恒真 —— 假绿)。</exception>
        public StethoscopePreloadGate(IReadOnlyList<string> requiredCues, LoadStateQuery query)
        {
            if (requiredCues == null) throw new ArgumentNullException(nameof(requiredCues));
            _query = query ?? throw new ArgumentNullException(nameof(query));

            var cues = new List<string>(requiredCues.Count);
            for (int i = 0; i < requiredCues.Count; i++)
            {
                if (string.IsNullOrEmpty(requiredCues[i])) continue;
                if (!cues.Contains(requiredCues[i])) cues.Add(requiredCues[i]);
            }
            if (cues.Count == 0)
                throw new ArgumentException(
                    "听诊族 cue 集为空 —— 空集会让门对任何输入都返回「就绪」(恒真假绿)。" +
                    "事件表须至少有一行 bus = " + StethoscopeBus +
                    "(AudioEventTableGates.RequiredCues 的两支呼吸层即此族)",
                    nameof(requiredCues));

            _requiredCues = cues.ToArray();
        }

        /// <summary>从**事件表**派生听诊族(表里 <c>bus == </c><see cref="StethoscopeBus"/> 的行,
        /// 按 ordinal 序)—— cue 集单一出处 = 表,不另立清单。</summary>
        /// <param name="table">运行期事件表(经 cooked 装载)。</param>
        /// <param name="query">载入态查询缝。</param>
        /// <exception cref="ArgumentException">表中无听诊族行(见构造器:空集硬失败)。</exception>
        public static StethoscopePreloadGate FromTable(in AudioEventDataSet table, LoadStateQuery query)
        {
            var cues = new List<string>();
            for (int i = 0; i < table.RowCount; i++)
            {
                AudioEventRow row = table.Row(i);
                if (string.Equals(row.Bus, StethoscopeBus, StringComparison.Ordinal) &&
                    !string.IsNullOrEmpty(row.Cue))
                    cues.Add(row.Cue);
            }
            return new StethoscopePreloadGate(cues, query);
        }

        /// <summary>听诊族 cue 集(去重保序;构造期已保证非空)。</summary>
        public IReadOnlyList<string> RequiredCues => _requiredCues;

        /// <summary>单个 cue 的当前载入态。</summary>
        public ClipLoadState StateOf(string cueId) => _query(cueId);

        /// <summary>未就绪的 cue 清单(**保序**;空 = 全部已载)。</summary>
        public IReadOnlyList<string> PendingCues()
        {
            var pending = new List<string>();
            for (int i = 0; i < _requiredCues.Length; i++)
            {
                ClipLoadState state = _query(_requiredCues[i]);
                if (state != ClipLoadState.Loaded) pending.Add(_requiredCues[i]);
            }
            return pending;
        }

        /// <summary>听诊族是否全部 <see cref="ClipLoadState.Loaded"/>(= 放行条件)。
        /// <para>⚠️ 判据是「全族就绪」,**不是**「要进的那一条就绪」——
        /// GDD :640 的「不得在听诊中因加载迟发」针对的是听诊**期间**任何一条迟到,
        /// 只查入场 cue 会让族内其余 cue 在贴耳后迟发。</para></summary>
        public bool IsReady()
        {
            for (int i = 0; i < _requiredCues.Length; i++)
                if (_query(_requiredCues[i]) != ClipLoadState.Loaded) return false;
            return true;
        }
    }
}
