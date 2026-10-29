// 字幕投递的**上游半边**(`AC-44-15` `[A]` 半 · 2026-10-29 · 缺口 B)。
//
// ── 病因(实测)──────────────────────────────────────────────────────────────
// `AudioEventRow.SubtitleText` 在 codec 里被读出、存进行对象,**生产侧零消费者**
// (`grep SubtitleText` 除 codec 外只命中构建期门与测试)。
// ⇒ `AC-44-15` 的「双投递」只成立**播音半边**,文本半边从未送达 42。
//
// ── 判据形状(为什么必须两路对照)──────────────────────────────────────────
// 单看「发了 cue 之后字幕出现了」**不充分** —— 也可能是别处塞的。
// 故每条判据都成对:
//   | 路 | 装配 | 期望 |
//   |---|---|---|
//   | 甲 | `subtitleSink` + `subtitleTextOf` 都给了 | 字幕出现且文本来自事件表 |
//   | 乙 | 两者都不给(62 处既有夹具的默认) | **零投递**(行为逐位不变) |
//
// ── 为什么文本**不经 44 转发** ─────────────────────────────────────────────
// `AC-44-15` 原文逐字:「cue → 44 播音(**维持「只入不出」,44 不新增出向口**),
// 文本 → 42 呈现(**文本本体 = 44 维护的事件表 `subtitle_text` 字段,上游发射点解析**)」。
// ⇒ 本驱动(`PatientCueDriver`)就是那个「上游发射点」;它同时投两个面。
//
// 权威来源:`design/gdd/audio-system.md` AC-44-15 · `skeuomorphic-ui.md` E-12b
//   · `accessibility-requirements.md:94-95` · story-026。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.PatientAI;
using DaYiJingCheng.Gameplay.Presentation.Audio;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Boot
{
    /// <summary>`PatientCueDriver` 的字幕投递判据。</summary>
    public sealed class SubtitleDeliveryTest
    {
        /// <summary>记录型字幕 sink(生产契约 = 两个方法)。</summary>
        private sealed class RecordingSubtitleSink : ISubtitleSink
        {
            public readonly List<SubtitleLineData> Shown = new List<SubtitleLineData>();
            public int HideCount;

            public void Show(in SubtitleLineData line) => Shown.Add(line);
            public void Hide() => HideCount++;
        }

        /// <summary>记录型音频 sink(cue 播音面)。</summary>
        private sealed class RecordingAudioSink : IAudioCueSink
        {
            public int EmitCount;
            public void Emit(in AudioCueDto cue) => EmitCount++;
            public AudioCueHandle BeginLoop(in AudioCueDto cue) => new AudioCueHandle(-1);
            public void EndLoop(AudioCueHandle handle) { }
            public void SetTier(TierSource source) { }
        }

        /// <summary>固定读数源(让调度判定「该发一次性 cue」)。</summary>
        private sealed class FixedVitals : IVitalsQuery
        {
            private readonly float _position;
            public FixedVitals(float position) { _position = position; }
            public VitalsDto GetVitals(PatientId p) => new VitalsDto(_position, 0f, 0, 0);
        }

        private const int CoughOrdinal = 0;
        private const string CoughText = "咳…咳咳…";

        /// <summary>造一个驱动(可选字幕缝)。</summary>
        /// <param name="presence">**由调用方提供** —— 驱动与测试必须共享同一登记簿
        /// (否则测试 `AddPresent` 加进的是另一个实例,驱动看不到 ⇒ 零 cue)。</param>
        private static (PatientCueDriver driver, RecordingAudioSink audio, RecordingSubtitleSink subtitle)
            MakeDriver(bool withSubtitle, PresenceRegistry presence)
        {
            var audio = new RecordingAudioSink();
            var subtitle = new RecordingSubtitleSink();
            var behavior = new PatientBehaviorDirector(new FixedVitals(0.55f), BehaviorBands.Default);   // 中间带(非终态)⇒ 该发 cue

            var driver = new PatientCueDriver(
                audio, new FixedVitals(0.55f), presence, behavior,
                coughCue: CoughOrdinal,
                knowsClinic: _ => true,
                hasCourse: _ => true,
                subtitleSink: withSubtitle ? subtitle : (ISubtitleSink)null,
                subtitleTextOf: withSubtitle ? (Func<int, string>)(_ => CoughText) : null);

            return (driver, audio, subtitle);
        }

        /// <summary>**甲路:两条缝都给 ⇒ 播音与字幕同时发生**(AC-44-15 的双投递)。
        /// <para>这是本债的核心判据。</para></summary>
        [Test]
        public void test_withSubtitleSink_bothAudioAndTextAreDelivered()
        {
            var presence = new PresenceRegistry();
            var (driver, audio, subtitle) = MakeDriver(withSubtitle: true, presence);
            presence.AddPresent(new PatientId(1), new WorldPos(0, 0, 0));

            // phase ∈ [0, interval)(severe 档 interval = 40)⇒ 推进 50 拍确保越过 firstDue。
            for (long t = 10; t <= 60; t++) driver.OnTickEdge(t);

            Assert.That(audio.EmitCount, Is.GreaterThan(0),
                "前置:该 tick 应发出一次性 cue(否则本测的判据面不存在)");
            Assert.That(subtitle.Shown.Count, Is.EqualTo(audio.EmitCount),
                $"字幕条数须与播音条数相等 —— 播音 {audio.EmitCount} 条 / 字幕 {subtitle.Shown.Count} 条。" +
                "少 = 文本半边没投递(本债原形态)");
            Assert.That(subtitle.Shown[0].Text, Is.EqualTo(CoughText),
                "字幕文本须来自 `subtitleTextOf`(即事件表的 subtitle_text 列)");
            Assert.That(subtitle.Shown[0].SpeakerName, Is.EqualTo("病人"),
                "说话人须非空(`accessibility-requirements.md:95`「名字前置」)");
        }

        /// <summary>**乙路:两条缝都不给 ⇒ 零投递**(62 处既有夹具的默认)。
        /// <para>⚠️ 这条钉的是「零行为变化」—— 不传缝的装配不得凭空产字幕。</para></summary>
        [Test]
        public void test_withoutSubtitleSink_noTextDelivered()
        {
            var presence = new PresenceRegistry();
            var (driver, audio, subtitle) = MakeDriver(withSubtitle: false, presence);
            presence.AddPresent(new PatientId(1), new WorldPos(0, 0, 0));

            // phase ∈ [0, interval)(severe 档 interval = 40)⇒ 推进 50 拍确保越过 firstDue。
            for (long t = 10; t <= 60; t++) driver.OnTickEdge(t);

            Assert.That(audio.EmitCount, Is.GreaterThan(0), "前置:播音仍须发生");
            Assert.That(subtitle.Shown, Is.Empty,
                "未给字幕缝 ⇒ 不得投递(62 处既有装配的行为须逐位不变)");
        }

        /// <summary>**文本为空 ⇒ 不 Show**(空字幕会盖掉上一条真实字幕)。
        /// <para>⚠️ `Show` 是「替换当前行」语义,不是追加 —— 空文本调用等于把屏幕清空。</para></summary>
        [Test]
        public void test_emptyText_doesNotShow()
        {
            var audio = new RecordingAudioSink();
            var subtitle = new RecordingSubtitleSink();
            var presence = new PresenceRegistry();
            var behavior = new PatientBehaviorDirector(new FixedVitals(0.55f), BehaviorBands.Default);

            var driver = new PatientCueDriver(
                audio, new FixedVitals(0.55f), presence, behavior,
                coughCue: CoughOrdinal,
                knowsClinic: _ => true,
                hasCourse: _ => true,
                subtitleSink: subtitle,
                subtitleTextOf: _ => null);          // 该 cue 无字幕

            presence.AddPresent(new PatientId(1), new WorldPos(0, 0, 0));
            // phase ∈ [0, interval)(severe 档 interval = 40)⇒ 推进 50 拍确保越过 firstDue。
            for (long t = 10; t <= 60; t++) driver.OnTickEdge(t);

            Assert.That(audio.EmitCount, Is.GreaterThan(0), "前置:播音仍须发生");
            Assert.That(subtitle.Shown, Is.Empty,
                "文本为 null ⇒ 不得 Show(空字幕会盖掉上一条真实字幕)");
        }

        /// <summary>**字幕不是播音的替代** —— 播音面必须仍然工作。
        /// <para>⚠️ 这条防的是「为了投字幕而跳过 `sink.Emit`」的退化形态。</para></summary>
        [Test]
        public void test_subtitleDelivery_doesNotReplaceAudio()
        {
            var presence = new PresenceRegistry();
            var (driver, audio, subtitle) = MakeDriver(withSubtitle: true, presence);
            presence.AddPresent(new PatientId(1), new WorldPos(0, 0, 0));

            // phase ∈ [0, interval)(severe 档 interval = 40)⇒ 推进 50 拍确保越过 firstDue。
            for (long t = 10; t <= 60; t++) driver.OnTickEdge(t);

            Assert.That(audio.EmitCount, Is.GreaterThan(0),
                "字幕投递**不得**替代播音 —— AC-44-15 要的是双投递,不是二选一");
        }
    }
}
