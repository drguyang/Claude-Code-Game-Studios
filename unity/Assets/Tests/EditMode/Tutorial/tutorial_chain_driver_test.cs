// S25-4c · tutorial-004 —— 48 **生产链**的判据(事件流 → 投影 → 边沿 → 调度 → 投递)
//
// 权威来源:
//   · `design/gdd/tutorial-and-onboarding.md` F-48.1(Completion 读时 / Fire 边沿)· 规则四
//   · `unity/Assets/Gameplay.Boot/TutorialChainDriver.cs`(被策对象)
//
// ⚠️ **只读外部夹具的具名声明**(承 `.claude/rules/test-standards.md` 2026-10-30 条):
//    本件读 `unity/Assets/DataCooked/48_tutorial_content.cooked.bytes`。为什么必须读真表:
//    六步的 `predicateKind` 是**数据**(observe=CaseOpened / close=CaseClosed / archive=Checkpoint …),
//    内联一份会让「表里改了判据」在夹具里看不见。
//
// ⚠️ 本件**不打 Fake 替身**:`EventStream` / `TutorialChainDriver` / `TutorialProjection` /
//    `TutorialDispatcher` / `TutorialMediaDriver` 全是生产件;只有两个 sink 是记录器
//    (它们是**出口**,记录即判据)。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Presentation.Tutorial;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.EditMode.Tutorial
{
    [TestFixture]
    public class tutorial_chain_driver_test
    {
        private const int Player = 41;
        private const int Patient = 11;

        private static readonly string RepoRoot = ComputeRepoRoot();

        private InMemoryBlobPool _pool;
        private PayloadEncoder _encoder;
        private PayloadDecoder _decoder;
        private EventStream _stream;
        private TutorialContentTable _content;
        private TutorialChainSeams _seams;
        private RecordingAudio _audio;
        private RecordingSubtitles _subtitles;

        private static string ComputeRepoRoot(
            [System.Runtime.CompilerServices.CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(thisFile) ?? ".", "..", "..", "..", "..", ".."));

        [SetUp]
        public void SetUp()
        {
            _pool = new InMemoryBlobPool();
            _encoder = new PayloadEncoder(_pool);
            _decoder = new PayloadDecoder(_pool);
            _stream = new EventStream(new FakeIdAuthority(), new NoPresence());
            _content = LoadRealTable();

            _audio = new RecordingAudio();
            _subtitles = new RecordingSubtitles();
            _seams = BuildSeams(checkpointWritten: null);
        }

        // ══════════════ 夹具 ══════════════

        private static TutorialContentTable LoadRealTable()
        {
            string path = Path.Combine(RepoRoot, "unity", "Assets", "DataCooked",
                TutorialContentTable.CookedFileName);
            Assert.That(File.Exists(path), Is.True, $"缺烘焙产物 `{path}`");
            Assert.That(TutorialContentTable.TryDecode(File.ReadAllBytes(path), out var table, out string err),
                Is.True, $"48 内容表解码失败:{err}");
            Assert.That(table.Steps.Count, Is.EqualTo(6), "六步闭集");
            return table;
        }

        /// <summary>翻译层的解析缝:表里每条口述都解析得到,文本 = 表行的 text 加前缀(便于区分来源)。</summary>
        private TutorialChainSeams BuildSeams(Func<bool> checkpointWritten)
        {
            TutorialContentTable content = _content;
            var ordinalOf = new Dictionary<string, int>(StringComparer.Ordinal);
            var textOf = new Dictionary<int, string>();
            for (int i = 0; i < content.Narration.Count; i++)
            {
                ordinalOf[content.Narration[i].CueId] = 100 + i;
                textOf[100 + i] = "【字幕】" + content.Narration[i].Text;
            }

            var media = new TutorialMediaDriver(
                _audio, _subtitles,
                cue => ordinalOf.TryGetValue(cue, out int ord) ? ord : TutorialMediaDriver.NoOrdinal,
                ord => textOf.TryGetValue(ord, out string t) ? t : null);

            return new TutorialChainSeams(content, media, () => new Int3(2, 0, 3), checkpointWritten);
        }

        private SimEvent CaseOpened(long tick)
            => new SimEvent(tick, new PatientId(Patient), -1L, EventKind.CaseOpened,
                _encoder.Encode(EventKind.CaseOpened,
                    new CaseOpenedPayload(Patient, new DiseaseIdSet(1UL << 0))));

        private SimEvent CaseClosed(long tick)
            => new SimEvent(tick, new PatientId(Patient), -1L, EventKind.CaseClosed,
                _encoder.Encode(EventKind.CaseClosed,
                    new CaseClosedPayload(Patient, new CaseId(tick, Patient, 0),
                        new DiseaseIdSet(1UL << 0), true)));

        private void Tick(long tick) => _stream.Append(CaseOpened(tick));

        // ══════════════ 边沿:新事件 → 发火 → 真的投递 ══════════════

        [Test]
        public void test_caseOpened_firesObserveStep_andPlaysNarration()
        {
            var chain = _seams.Attach(_stream, _decoder, Player);
            Assert.That(chain.SeededCompletedSteps, Is.Zero, "空流播种应无已完成步");

            _stream.Append(CaseOpened(10));
            TutorialChainTick r = chain.OnTickEdge(10);

            CollectionAssert.Contains(r.FiredStepIds, "observe", "CaseOpened 对应 observe 步(真表 predicateKind)");
            Assert.That(r.NarrationsPlayed, Is.EqualTo(1), "observe 有一条口述 ⇒ 应发一条");
            Assert.That(_audio.Emitted.Count, Is.EqualTo(1));
            Assert.That(_subtitles.Shown.Count, Is.EqualTo(1), "口述必带字幕(AC-48-15)");
            Assert.That(_subtitles.Shown[0].Text, Does.StartWith("【字幕】"),
                "字幕文本须来自**事件表**解析缝,不是意图自带的 Text");
            Assert.That(_audio.Emitted[0].Cell.X, Is.EqualTo(2), "空间化锚点来自装配缝");
            Assert.That(_audio.Emitted[0].Cell.Z, Is.EqualTo(3));
        }

        [Test]
        public void test_sameCompletion_doesNotRefireOnLaterTicks()
        {
            var chain = _seams.Attach(_stream, _decoder, Player);
            _stream.Append(CaseOpened(10));
            chain.OnTickEdge(10);

            TutorialChainTick again = chain.OnTickEdge(11);
            TutorialChainTick third = chain.OnTickEdge(12);

            Assert.That(again.FiredStepIds, Is.Empty, "无新完成 ⇒ 不得重发(边沿闩)");
            Assert.That(third.FiredStepIds, Is.Empty);
            Assert.That(_audio.Emitted.Count, Is.EqualTo(1), "口述只发一次");
        }

        [Test]
        public void test_incremental_newEventFiresOnlyItsOwnStep()
        {
            var chain = _seams.Attach(_stream, _decoder, Player);

            _stream.Append(CaseOpened(10));
            TutorialChainTick first = chain.OnTickEdge(10);

            _stream.Append(CaseClosed(20));
            TutorialChainTick second = chain.OnTickEdge(20);

            CollectionAssert.Contains(first.FiredStepIds, "observe");
            CollectionAssert.Contains(second.FiredStepIds, "close", "CaseClosed 对应 close 步");
            CollectionAssert.DoesNotContain(second.FiredStepIds, "observe",
                "已发火的步不得在后续 tick 重发");
        }

        /// <summary>
        /// 增量游标:事实表 = 流的事件数,**不重不漏**。
        /// <para>⚠️ 没有这条,游标写错(每 tick 从头再解一遍)在行为上**看不出来** ——
        /// 投影是集合语义、发火有闩 ⇒ 重复的事实不改变结果,只白白膨胀内存。</para>
        /// </summary>
        [Test]
        public void test_incrementalCursor_doesNotDuplicateFacts()
        {
            var chain = _seams.Attach(_stream, _decoder, Player);

            _stream.Append(CaseOpened(10));
            chain.OnTickEdge(10);
            Assert.That(chain.FactCount, Is.EqualTo(_stream.Count), "首条事件须恰解一次");

            chain.OnTickEdge(11);
            chain.OnTickEdge(12);
            Assert.That(chain.FactCount, Is.EqualTo(_stream.Count), "无新事件的边沿不得重复累积");

            _stream.Append(CaseClosed(20));
            chain.OnTickEdge(20);
            Assert.That(chain.FactCount, Is.EqualTo(_stream.Count), "新事件只追加其自身");
        }

        // ══════════════ 播种:读档不重播 ══════════════

        [Test]
        public void test_seed_fromLoadedStream_doesNotReplay()
        {
            _stream.Append(CaseOpened(5));          // 模拟「存档里已有」

            var chain = _seams.Attach(_stream, _decoder, Player);
            Assert.That(chain.SeededCompletedSteps, Is.GreaterThanOrEqualTo(1),
                "构造期须按既有流播种(否则读档会把整段教学重播一遍)");

            TutorialChainTick r = chain.OnTickEdge(6);

            Assert.That(r.FiredStepIds, Is.Empty, "播种只置饱和、不 Fire");
            Assert.That(_audio.Emitted, Is.Empty, "读档后不得重播口述");
        }

        // ══════════════ Checkpoint 缝:null 与接线两种行为 ══════════════

        [Test]
        public void test_checkpointSeam_whenNull_archiveNeverCompletes()
        {
            var chain = _seams.Attach(_stream, _decoder, Player);

            for (long t = 1; t <= 3; t++) chain.OnTickEdge(t);

            Assert.That(chain.OnTickEdge(4).FiredStepIds, Is.Empty);
        }

        [Test]
        public void test_checkpointSeam_whenWired_archiveFiresOnce()
        {
            bool saved = false;
            var seams = BuildSeams(checkpointWritten: () => saved);
            var chain = seams.Attach(_stream, _decoder, Player);

            Assert.That(seams.CheckpointWritable, Is.True);
            Assert.That(chain.OnTickEdge(1).FiredStepIds, Is.Empty, "还没存过档");

            saved = true;
            TutorialChainTick r = chain.OnTickEdge(2);
            CollectionAssert.Contains(r.FiredStepIds, "archive", "Checkpoint 缝为真 ⇒ archive 步完成");
            Assert.That(r.NarrationsPlayed, Is.EqualTo(1));

            Assert.That(chain.OnTickEdge(3).FiredStepIds, Is.Empty, "只发一次");
        }

        // ══════════════ 流被整体替换 ══════════════

        [Test]
        public void test_streamReplacement_isDetectedAndReseeded()
        {
            var chain = _seams.Attach(_stream, _decoder, Player);
            _stream.Append(CaseOpened(10));
            chain.OnTickEdge(10);
            Assert.That(_audio.Emitted.Count, Is.EqualTo(1));

            // 换世界:流被清空并写入另一条(不同病人 ⇒ 指纹必然不同)
            _stream.Clear();
            _stream.Append(new SimEvent(100, new PatientId(Patient + 1), -1L, EventKind.CaseOpened,
                _encoder.Encode(EventKind.CaseOpened,
                    new CaseOpenedPayload(Patient + 1, new DiseaseIdSet(1UL << 0)))));

            TutorialChainTick r = chain.OnTickEdge(100);
            Assert.That(r.StreamWasReplaced, Is.True, "流被替换须被检出(否则旧世界进度串进新世界)");
            Assert.That(r.FiredStepIds, Is.Empty, "重建 + 重新播种的那一拍不发火");
            Assert.That(_audio.Emitted.Count, Is.EqualTo(1), "重播种不得重播");
        }

        // ══════════════ 守卫 ══════════════

        [Test]
        public void test_seams_emptyContent_isRejected()
        {
            var media = new TutorialMediaDriver(_audio, _subtitles, _ => 0, _ => "x");
            var ex = Assert.Throws<ArgumentException>(() =>
                new TutorialChainSeams(default, media, () => default(Int3)));
            Assert.That(ex.ParamName, Is.EqualTo("content"));
        }

        [Test]
        public void test_guard_nullArguments_throw()
        {
            var media = new TutorialMediaDriver(_audio, _subtitles, _ => 0, _ => "x");
            Assert.That(Assert.Throws<ArgumentNullException>(() =>
                new TutorialChainSeams(_content, null, () => default(Int3))).ParamName, Is.EqualTo("media"));
            Assert.That(Assert.Throws<ArgumentNullException>(() =>
                new TutorialChainSeams(_content, media, null)).ParamName, Is.EqualTo("sourceCell"));

            Assert.That(Assert.Throws<ArgumentNullException>(() =>
                new TutorialChainDriver(null, _decoder, _content, Player, media, () => default(Int3)))
                .ParamName, Is.EqualTo("stream"));
            Assert.That(Assert.Throws<ArgumentNullException>(() =>
                new TutorialChainDriver(_stream, null, _content, Player, media, () => default(Int3)))
                .ParamName, Is.EqualTo("decoder"));
            Assert.That(Assert.Throws<ArgumentNullException>(() =>
                new TutorialChainDriver(_stream, _decoder, _content, Player, null, () => default(Int3)))
                .ParamName, Is.EqualTo("media"));
            Assert.That(Assert.Throws<ArgumentNullException>(() =>
                new TutorialChainDriver(_stream, _decoder, _content, Player, media, null))
                .ParamName, Is.EqualTo("sourceCell"));
        }

        // ══════════════ 记录器 ══════════════

        private sealed class RecordingAudio : IAudioCueSink
        {
            public readonly List<AudioCueDto> Emitted = new List<AudioCueDto>();
            public void Emit(in AudioCueDto cue) => Emitted.Add(cue);
            public AudioCueHandle BeginLoop(in AudioCueDto cue) => throw new InvalidOperationException("口述不起循环");
            public void EndLoop(AudioCueHandle handle) => throw new InvalidOperationException("无循环");
            public void SetTier(TierSource source) { }
        }

        private sealed class RecordingSubtitles : ISubtitleSink
        {
            public readonly List<SubtitleLineData> Shown = new List<SubtitleLineData>();
            public void Show(in SubtitleLineData line) => Shown.Add(line);
            public void Hide() { }
        }

        private sealed class FakeIdAuthority : IIdAuthority
        {
            private int _patient;
            private long _item;
            public PatientId NextPatientId() => new PatientId(_patient++);
            public ItemInstanceId NextItemInstanceId() => new ItemInstanceId(_item++);
        }

        private sealed class NoPresence : IPresenceQuery
        {
            public bool IsPresent(PatientId patientId) => false;
            public int PresentCount => 0;
            public bool IsPresentAt(WorldPos cell) => false;
            public IReadOnlyCollection<int> PresentPatientIds() => Array.Empty<int>();
        }
    }
}
