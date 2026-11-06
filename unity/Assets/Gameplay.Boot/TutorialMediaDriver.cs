// S25-4b · tutorial-004 —— Boot 的**翻译层**:48 的媒体意图 → 44 的 `AudioCueDto` + 42 的 `SubtitleLineData`。
//
// 权威来源:
//   · `design/gdd/tutorial-and-onboarding.md` 规则二(三媒体)· 规则七(48 只调度)
//   · `design/gdd/audio-system.md` **AC-44-15**(「cue → 44 播音;文本 → 42 呈现;
//     文本本体 = 44 维护的事件表 `subtitle_text` 字段,**上游发射点解析**」)
//   · `adr-018` §一/§二(44 只入不出)· `adr-013` §9 C3(42 只渲染)
//
// ═══ 为什么这一层必须住在 Boot,而不是 48 自己发 ═════════════════════════════
// `AudioCueDto` / `IAudioCueSink` 声明在 `Sim.Contracts`,而 48 的装配
// (`Gameplay.UI.Tutorial`)引用集恰 = `{Gameplay.Presentation}`(`AC-48-11` 的编译期形态)
// ⇒ **48 写不出那个类型名**。48 产出 `TutorialMediaIntent`,翻译归本层 —— 这不是分层偏好,
// 是引用集决定的唯一可能路径。
//
// ═══ 字幕文本的**单一真源** = 音频事件表的 `subtitle_text` ══════════════════
// AC-44-15 逐字:「文本本体 = 44 维护的事件表 `subtitle_text` 字段,上游发射点解析」。
// ⇒ 本类**不用** `TutorialMediaIntent.Text` 投字幕,而是 `ordinal → 表行 → subtitle_text`。
//
// ⚠️ **2026-11-06 如实登记的既有分歧(不在本层修)**:同一 cue 在两张表里台词**不同** ——
//    `assets/data/48_tutorial_content.json` 的 `narration[].text` 与
//    `assets/data/audio_events.json` 的 `rows[].subtitle_text` 对同样四条 cue 给出了两种措辞
//    (例:`Narration_TeachObserve_01` =「先看看这个人。气色、神态,都在脸上。」
//     vs「喘着气,气色、神态,都在脸上。」)。`AC-48-15` 要的是「同一行、不得两处各写一份」,
//    而现在是**两份**。本层按 AC-44-15 取音频表为准;两表的**内容对账**与「同源」的机械门
//    归内容轮 / 构建期校验,**未做**,不得据本类推断已一致。
//
// ═══ 本类不做什么 ═══════════════════════════════════════════════════════════
//   · **不排序、不去重、不计时**:输入即意图(顺序由 `TutorialDispatcher` 给),
//     「同刻不叠播」是**播放节奏**,归用户手感轮;本类只按给定序**立即**投递。
//   · **不演示范、不摊纸**:`Demonstration` / `Paper` 两种意图本层**不消费** ——
//     前者归 13 经 42 的片段接口,后者归 6 的纸道具 + 42 的近景模态。本类只**如实计数**,
//     让「没接的那一半」在返回值里可见,而不是静默消失(见 `TutorialPlaybackResult`)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using DaYiJingCheng.Gameplay.UI.Tutorial;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>一次调度投递的**结果计数**(让「没消费的那一半」可见)。</summary>
    public readonly struct TutorialPlaybackResult
    {
        /// <summary>真的发出去的口述条数(每一条 = 一次 `Emit` + 一次 `Show`)。</summary>
        public readonly int NarrationsPlayed;

        /// <summary>交给 13 / 42 的示范意图条数(本层不消费)。</summary>
        public readonly int DemonstrationsLeftToOthers;

        /// <summary>交给 6 / 42 的纸意图条数(本层不消费)。</summary>
        public readonly int PapersLeftToOthers;

        public TutorialPlaybackResult(int narrationsPlayed, int demonstrationsLeftToOthers, int papersLeftToOthers)
        {
            NarrationsPlayed = narrationsPlayed;
            DemonstrationsLeftToOthers = demonstrationsLeftToOthers;
            PapersLeftToOthers = papersLeftToOthers;
        }

        /// <summary>三类意图总数。</summary>
        public int Total => NarrationsPlayed + DemonstrationsLeftToOthers + PapersLeftToOthers;
    }

    /// <summary>
    /// 48 媒体意图 → 44 / 42 的**翻译层**(一实例 = 一名玩家;无状态、可重复调用)。
    /// </summary>
    public sealed class TutorialMediaDriver
    {
        /// <summary>口述 cue 的归一强度(一次性语声,与 `ProcessingCompletionDriver` 同档)。</summary>
        public const byte NarrationIntensity = 255;

        /// <summary>`AudioCueDto.Source` 的「非实体声源」哨兵(契约:`None = -1`)。</summary>
        public const int NoSource = -1;

        /// <summary>口述者显示名(字幕「名字前置」,`accessibility-requirements.md:95`)。</summary>
        public const string MentorSpeakerName = "师父";

        /// <summary>事件表里查不到该 cue 时的 ordinal(= `AudioEventDataSet.NoOrdinal`,此处重复以免引 44 类型)。</summary>
        public const int NoOrdinal = -1;

        private readonly IAudioCueSink _audio;
        private readonly ISubtitleSink _subtitles;
        private readonly Func<string, int> _ordinalOf;
        private readonly Func<int, string> _subtitleTextOf;
        private readonly string _speaker;

        /// <summary>构造。</summary>
        /// <param name="audio">44 的入向口(ADR-018 §二)。</param>
        /// <param name="subtitles">42 的字幕接收口(AC-44-15)。</param>
        /// <param name="ordinalOf">cue 名 → 事件表 ordinal(装配期注入;查不到回 <see cref="NoOrdinal"/>)。</param>
        /// <param name="subtitleTextOf">ordinal → 事件表 `subtitle_text`(空/null = 该行没有字幕文本)。</param>
        /// <param name="speakerName">口述者显示名(空 ⇒ 退 <see cref="MentorSpeakerName"/>)。</param>
        public TutorialMediaDriver(
            IAudioCueSink audio,
            ISubtitleSink subtitles,
            Func<string, int> ordinalOf,
            Func<int, string> subtitleTextOf,
            string speakerName = MentorSpeakerName)
        {
            _audio = audio ?? throw new ArgumentNullException(nameof(audio),
                "未传入 IAudioCueSink —— 口述会静默无声(拒以 null 冒充「本局无口述」)");
            _subtitles = subtitles ?? throw new ArgumentNullException(nameof(subtitles),
                "未传入 ISubtitleSink —— 口述会静默无字幕(AC-48-15 的禁项)");
            _ordinalOf = ordinalOf ?? throw new ArgumentNullException(nameof(ordinalOf));
            _subtitleTextOf = subtitleTextOf ?? throw new ArgumentNullException(nameof(subtitleTextOf));
            _speaker = string.IsNullOrEmpty(speakerName) ? MentorSpeakerName : speakerName;
        }

        /// <summary>
        /// 按给定顺序投递一串媒体意图。
        /// </summary>
        /// <param name="intents"><see cref="TutorialDispatcher.Dispatch"/> 的返回(已排序)。</param>
        /// <param name="sourceCell">口述的**空间化锚点**(integer 格,ADR-015 §三)。
        /// P0 无师父实体 ⇒ 调用方给「口述发生的格」;不得在此处伪造听众位置。</param>
        /// <returns>各类意图的计数(见 <see cref="TutorialPlaybackResult"/>)。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="intents"/> 为 null。</exception>
        /// <exception cref="InvalidOperationException">
        /// 口述 cue 在音频事件表里解析不到,或该行没有 `subtitle_text`。
        /// <para>⚠️ **刻意抛而不是跳过**:跳过 = 玩家听到/看到的是「这一段没有口述」,
        /// 与「内容没接上」**不可区分**——而这正是 `AC-48-15` 「教学口述零字幕是禁项」要防的形态。</para>
        /// </exception>
        public TutorialPlaybackResult Play(IReadOnlyList<TutorialMediaIntent> intents, Int3 sourceCell)
        {
            if (intents == null) throw new ArgumentNullException(nameof(intents),
                "意图列表为 null —— 空调度请传空列表,不要传 null(两者在归因上不是一回事)");

            int narrations = 0, demonstrations = 0, papers = 0;
            for (int i = 0; i < intents.Count; i++)
            {
                switch (intents[i].Kind)
                {
                    case TutorialMediaKind.Narration:
                        PlayNarration(intents[i], sourceCell);
                        narrations++;
                        break;
                    case TutorialMediaKind.Demonstration:
                        demonstrations++;
                        break;
                    case TutorialMediaKind.Paper:
                        papers++;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(intents), intents[i].Kind,
                            "未知媒体种类 —— 新增成员必须在此显式处置(不得落入静默丢弃)");
                }
            }

            return new TutorialPlaybackResult(narrations, demonstrations, papers);
        }

        private void PlayNarration(in TutorialMediaIntent intent, Int3 sourceCell)
        {
            int ordinal = _ordinalOf(intent.CueId);
            if (ordinal < 0)
                throw new InvalidOperationException(
                    $"口述 cue「{intent.CueId}」(步 {intent.StepId})在音频事件表里解析不到 —— " +
                    "该 cue 未烘焙进 `audio_events.cooked`(静默无声比崩更糟)");

            // ⚠️ 文本取自**事件表**(AC-44-15 的单一真源),不取 intent.Text —— 见文件头 §既有分歧。
            string text = _subtitleTextOf(ordinal);
            if (string.IsNullOrEmpty(text))
                throw new InvalidOperationException(
                    $"口述 cue「{intent.CueId}」(ordinal {ordinal})的事件表行没有 `subtitle_text` —— " +
                    "教学口述零字幕是禁项(AC-48-15)");

            _audio.Emit(new AudioCueDto(
                cue: ordinal,
                intensity: NarrationIntensity,
                cell: sourceCell,
                source: NoSource,
                looped: false));

            // 走查仪表(默认零开销):口述投递时把**真正用的声源格**打出来 ——
            // 「音量不对」时第一个要分清的就是「格传错了」还是「别处衰减」。
            if (WalkthroughDiagnostics.Enabled)
                UnityEngine.Debug.Log(
                    $"[tutorialdiag] 口述投递 cue={ordinal} cell=({sourceCell.X},{sourceCell.Y},{sourceCell.Z})");

            _subtitles.Show(new SubtitleLineData(_speaker, text));
        }
    }
}
