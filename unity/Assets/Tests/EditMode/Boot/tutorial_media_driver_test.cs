// S25-4b · tutorial-004 —— 48 媒体意图 → 44 / 42 的**翻译层**判据(AC-48-15 · AC-44-15)
//
// 权威来源:
//   · `design/gdd/audio-system.md` **AC-44-15**(文本本体 = 事件表 `subtitle_text`,上游发射点解析)
//   · `design/gdd/tutorial-and-onboarding.md` 规则二 / 规则七 · `AC-48-15`
//   · `unity/Assets/Gameplay.Boot/TutorialMediaDriver.cs`(被策对象;文件头记了两表分歧)
//
// ⚠️ 本件**不读真表**:翻译层的判据是「形参给什么就翻成什么」,读真表会把内容轮的对账
//    混进本层的判据里(两表今天台词不一致 —— 见被策类文件头 §既有分歧)。真表对账归内容轮。
//
// ⚠️ 本件**不判时机**:播放节奏(同刻不叠播 / 延迟)归用户手感轮;本层只按给定序立即投递。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using DaYiJingCheng.Gameplay.UI.Tutorial;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Boot
{
    /// <summary>`TutorialMediaDriver` 的翻译判据。</summary>
    public sealed class TutorialMediaDriverTest
    {
        private const string CueA = "Narration_A";
        private const string CueB = "Narration_B";
        private const string TableTextA = "表里的甲句。";
        private const string TableTextB = "表里的乙句。";

        /// <summary>记录型音频 sink(生产契约 = 四个方法;本层只该用 <see cref="Emit"/>)。</summary>
        private sealed class RecordingAudioSink : IAudioCueSink
        {
            public readonly List<AudioCueDto> Emitted = new List<AudioCueDto>();
            public int TierCalls;
            public int LoopCalls;

            public void Emit(in AudioCueDto cue) => Emitted.Add(cue);

            public AudioCueHandle BeginLoop(in AudioCueDto cue)
            {
                LoopCalls++;
                throw new InvalidOperationException("口述是一次性 cue —— 本层不得起循环");
            }

            public void EndLoop(AudioCueHandle handle) => throw new InvalidOperationException("本层不起循环");

            public void SetTier(TierSource source) => TierCalls++;
        }

        /// <summary>记录型字幕 sink。</summary>
        private sealed class RecordingSubtitleSink : ISubtitleSink
        {
            public readonly List<SubtitleLineData> Shown = new List<SubtitleLineData>();
            public int HideCount;

            public void Show(in SubtitleLineData line) => Shown.Add(line);
            public void Hide() => HideCount++;
        }

        private static readonly Int3 Cell = new Int3(3, 0, 7);

        private RecordingAudioSink _audio;
        private RecordingSubtitleSink _subtitles;

        [SetUp]
        public void SetUp()
        {
            _audio = new RecordingAudioSink();
            _subtitles = new RecordingSubtitleSink();
        }

        /// <summary>按名解析:只认本夹具的两条 cue(其余回 NoOrdinal,模拟「没烘进表」)。</summary>
        private static Func<string, int> Ordinals() => cue => cue switch
        {
            CueA => 11,
            CueB => 12,
            _ => TutorialMediaDriver.NoOrdinal,
        };

        private static Func<int, string> Texts() => ordinal => ordinal switch
        {
            11 => TableTextA,
            12 => TableTextB,
            _ => null,
        };

        private TutorialMediaDriver NewDriver(Func<string, int> ordinalOf = null,
                                              Func<int, string> textOf = null)
            => new TutorialMediaDriver(_audio, _subtitles, ordinalOf ?? Ordinals(), textOf ?? Texts());

        // ══════════════ 口述:cue 与字幕成对 ══════════════

        [Test]
        public void test_narration_emitsCueWithResolvedOrdinal()
        {
            NewDriver().Play(new[] { TutorialMediaIntent.Narration("observe", CueA, "意图里的甲句。") }, Cell);

            Assert.That(_audio.Emitted.Count, Is.EqualTo(1), "口述须恰好发一次 cue");
            AudioCueDto cue = _audio.Emitted[0];
            Assert.That(cue.Cue, Is.EqualTo(11), "cue 必须是**解析后的 ordinal**,不是名字");
            Assert.That(cue.Intensity, Is.EqualTo(TutorialMediaDriver.NarrationIntensity));
            Assert.That(cue.Source, Is.EqualTo(TutorialMediaDriver.NoSource), "口述无实体声源");
            Assert.That(cue.Looped, Is.False, "口述是一次性的");
            Assert.That(cue.Cell.X, Is.EqualTo(3), "空间化锚点须原样透传");
            Assert.That(cue.Cell.Z, Is.EqualTo(7));
        }

        [Test]
        public void test_narration_showsTableText_notIntentText()
        {
            // 意图里的文本与表里的**刻意不同** —— 只有取了表里的才说明单一真源成立
            NewDriver().Play(new[] { TutorialMediaIntent.Narration("observe", CueA, "意图里的甲句。") }, Cell);

            Assert.That(_subtitles.Shown.Count, Is.EqualTo(1), "口述必须带字幕(AC-48-15 禁项)");
            Assert.That(_subtitles.Shown[0].Text, Is.EqualTo(TableTextA),
                "字幕文本须取自事件表 `subtitle_text`(AC-44-15)—— 取意图自带的文本 = 第二真源");
            Assert.That(_subtitles.Shown[0].Text, Is.Not.EqualTo("意图里的甲句。"),
                "上限反证:若本断言红,说明本夹具的两种文本恰好相同,上一条就不再判「取自哪一边」");
            Assert.That(_subtitles.Shown[0].SpeakerName, Is.EqualTo(TutorialMediaDriver.MentorSpeakerName),
                "名字前置(accessibility-requirements.md:95)");
        }

        [Test]
        public void test_narration_unknownCue_throws()
        {
            var ex = Assert.Throws<InvalidOperationException>(
                () => NewDriver().Play(new[] { TutorialMediaIntent.Narration("observe", "Narration_Missing", "x") }, Cell));

            Assert.That(ex.Message, Does.Contain("Narration_Missing"), "报文须点名是哪一条 cue");
            Assert.That(_audio.Emitted, Is.Empty, "抛之前不得已经发过任何 cue");
            Assert.That(_subtitles.Shown, Is.Empty);
        }

        [Test]
        public void test_narration_emptySubtitleText_throws()
        {
            var ex = Assert.Throws<InvalidOperationException>(
                () => NewDriver(textOf: _ => string.Empty)
                    .Play(new[] { TutorialMediaIntent.Narration("observe", CueA, "x") }, Cell));

            Assert.That(ex.Message, Does.Contain("subtitle_text"), "报文须指出缺的是哪一列");
            Assert.That(_subtitles.Shown, Is.Empty, "没有文本就不得显示空字幕冒充「已投递」");
        }

        // ══════════════ 另外两种媒体:本层不消费,但必须**可见** ══════════════

        [Test]
        public void test_demonstrationAndPaper_areCountedNotPlayed()
        {
            TutorialPlaybackResult r = NewDriver().Play(new[]
            {
                TutorialMediaIntent.Demonstration("observe", "mentor", "mentor_pulse_exam"),
                TutorialMediaIntent.Paper("observe", "p_wangzhen"),
            }, Cell);

            Assert.That(r.NarrationsPlayed, Is.Zero);
            Assert.That(r.DemonstrationsLeftToOthers, Is.EqualTo(1), "示范须计数,不得静默消失");
            Assert.That(r.PapersLeftToOthers, Is.EqualTo(1));
            Assert.That(r.Total, Is.EqualTo(2));
            Assert.That(_audio.Emitted, Is.Empty, "本层不发示范音");
            Assert.That(_subtitles.Shown, Is.Empty, "本层不显示纸字幕");
        }

        // ══════════════ 次序与无状态 ══════════════

        [Test]
        public void test_play_preservesGivenOrder()
        {
            NewDriver().Play(new[]
            {
                TutorialMediaIntent.Narration("judge", CueB, "b"),
                TutorialMediaIntent.Narration("observe", CueA, "a"),
            }, Cell);

            Assert.That(_audio.Emitted.Count, Is.EqualTo(2));
            Assert.That(_audio.Emitted[0].Cue, Is.EqualTo(12), "投递序 = 给定的意图序(排序归 48,不归本层)");
            Assert.That(_audio.Emitted[1].Cue, Is.EqualTo(11));
            Assert.That(_subtitles.Shown[0].Text, Is.EqualTo(TableTextB));
        }

        [Test]
        public void test_play_sameInputTwice_playsTwice()
        {
            TutorialMediaDriver d = NewDriver();
            var intents = new[] { TutorialMediaIntent.Narration("observe", CueA, "x") };

            d.Play(intents, Cell);
            d.Play(intents, Cell);

            Assert.That(_audio.Emitted.Count, Is.EqualTo(2),
                "本层**不去重**:「每步只发一次」是 48 的闩(TutorialTrigger)的职责;" +
                "在这里再去一次重,会让两条去重规则互相掩护,坏掉一条也看不出来");
        }

        [Test]
        public void test_play_empty_doesNothing()
        {
            TutorialPlaybackResult r = NewDriver().Play(Array.Empty<TutorialMediaIntent>(), Cell);

            Assert.That(r.Total, Is.Zero);
            Assert.That(_audio.Emitted, Is.Empty);
            Assert.That(_subtitles.Shown, Is.Empty);
        }

        // ══════════════ 入口守卫 ══════════════

        [Test]
        public void test_guard_nullIntents_throws()
        {
            var ex = Assert.Throws<ArgumentNullException>(() => NewDriver().Play(null, Cell));
            Assert.That(ex.ParamName, Is.EqualTo("intents"));
        }

        [Test]
        public void test_guard_nullSinks_throwAtConstruction()
        {
            Assert.That(Assert.Throws<ArgumentNullException>(
                () => new TutorialMediaDriver(null, _subtitles, Ordinals(), Texts())).ParamName,
                Is.EqualTo("audio"));
            Assert.That(Assert.Throws<ArgumentNullException>(
                () => new TutorialMediaDriver(_audio, null, Ordinals(), Texts())).ParamName,
                Is.EqualTo("subtitles"));
            Assert.That(Assert.Throws<ArgumentNullException>(
                () => new TutorialMediaDriver(_audio, _subtitles, null, Texts())).ParamName,
                Is.EqualTo("ordinalOf"));
            Assert.That(Assert.Throws<ArgumentNullException>(
                () => new TutorialMediaDriver(_audio, _subtitles, Ordinals(), null)).ParamName,
                Is.EqualTo("subtitleTextOf"));
        }
    }
}
