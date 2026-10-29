// 病人 cue 可见物的**上游半边**(`AC-13-F1` 的 42 侧达成面 · 2026-10-29 · 缺口 D)。
//
// ── 病因(实测)──────────────────────────────────────────────────────────────
// `AC-13-F1` 的三类可见物此前**零承载**:13 侧只判结构前提(cue 语义是独立枚举 id /
// 呼吸层默认在),其夹具自陈「达成面在 42 的冗余通道」;而 42 侧一寸可见物都没有。
// ⇒ 听障玩家在 P0 拿不到任何替代通道。
//
// ── 判据形状(为什么必须两路对照)──────────────────────────────────────────
// 单看「发了 cue 之后字形出现了」**不充分** —— 也可能是别处塞的。
// 故每条判据都成对:
//   | 路 | 装配 | 期望 |
//   |---|---|---|
//   | 甲 | `cueMarkSink` 给了 | 播音与字形**同时**发生,且类别取自 13 的决策 |
//   | 乙 | 不给(既有夹具的默认) | **零投递**,播音行为逐位不变 |
//
// 权威来源:`design/gdd/patient-ai.md` AC-13-F1 · `audio-system.md` AC-44-12 ①
//   · `accessibility-requirements.md:95`(色-only 铁律)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.PatientAI;
using DaYiJingCheng.Gameplay.Presentation.Audio;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Boot
{
    /// <summary>`PatientCueDriver` 的可见物投递判据。</summary>
    public sealed class PatientCueMarkDeliveryTest
    {
        /// <summary>记录型可见物 sink(生产契约 = 三个方法)。</summary>
        private sealed class RecordingMarkSink : IPatientCueMarkSink
        {
            public readonly List<PatientCueMarkData> Marks = new List<PatientCueMarkData>();
            public readonly List<int> ClearedPatients = new List<int>();
            public int ClearAllCount;

            public void Mark(in PatientCueMarkData mark) => Marks.Add(mark);
            public void ClearPatient(int patientId) => ClearedPatients.Add(patientId);
            public void ClearAll() => ClearAllCount++;
        }

        /// <summary>记录型音频 sink(播音面)。</summary>
        private sealed class RecordingAudioSink : IAudioCueSink
        {
            public int EmitCount;
            public int BeginLoopCount;
            public void Emit(in AudioCueDto cue) => EmitCount++;
            public AudioCueHandle BeginLoop(in AudioCueDto cue) { BeginLoopCount++; return new AudioCueHandle(-1); }
            public void EndLoop(AudioCueHandle handle) { }
            public void SetTier(TierSource source) { }
        }

        /// <summary>固定读数源(中间带 = 非终态 ⇒ 该发 cue / 该起呼吸层)。</summary>
        private sealed class FixedVitals : IVitalsQuery
        {
            private readonly float _position;
            public FixedVitals(float position) { _position = position; }
            public VitalsDto GetVitals(PatientId p) => new VitalsDto(_position, 0f, 0, 0);
        }

        private const int CoughOrdinal = 0;
        private const int BreathOrdinal = 1;
        private const int PatientId1 = 1;

        private static (PatientCueDriver driver, RecordingAudioSink audio, RecordingMarkSink marks)
            MakeDriver(bool withMarks, PresenceRegistry presence)
        {
            var audio = new RecordingAudioSink();
            var marks = new RecordingMarkSink();
            var vitals = new FixedVitals(0.55f);   // 中间带(非终态)⇒ 该发 cue / 该起呼吸层
            var behavior = new PatientBehaviorDirector(vitals, BehaviorBands.Default);

            var driver = new PatientCueDriver(
                audio, vitals, presence, behavior,
                coughCue: CoughOrdinal,
                breathCue: BreathOrdinal,
                knowsClinic: _ => true,
                hasCourse: _ => true,
                cueMarkSink: withMarks ? marks : (IPatientCueMarkSink)null);

            return (driver, audio, marks);
        }

        /// <summary>推进到一次性 cue 的到期点(phase ∈ [0, interval),severe interval = 40)。</summary>
        private static void AdvanceToFirstCough(PatientCueDriver driver)
        {
            for (long t = 10; t <= 60; t++) driver.OnTickEdge(t);
        }

        /// <summary>**甲路:咳嗽的可见物真的投递,类别 = 咳嗽**(不是「有东西就算」)。</summary>
        [Test]
        public void test_cough_markDelivered()
        {
            var presence = new PresenceRegistry();
            var (driver, audio, marks) = MakeDriver(withMarks: true, presence);
            presence.AddPresent(new PatientId(PatientId1), new WorldPos(0, 0, 0));

            AdvanceToFirstCough(driver);

            Assert.That(audio.EmitCount, Is.GreaterThan(0),
                "前置:该 tick 应发出一次性 cue(否则本测的判据面不存在)");

            bool hasCough = false;
            foreach (PatientCueMarkData m in marks.Marks)
                if (m.Kind == PatientCueVisual.Cough && m.PatientId == PatientId1) hasCough = true;

            Assert.That(hasCough, Is.True,
                $"咳嗽须有可见对应物 —— 实得 [{string.Join(", ", marks.Marks)}]");
        }

        /// <summary>**呼吸的可见物在起呼吸层时投递**(第三类)。</summary>
        [Test]
        public void test_breath_markDeliveredOnBegin()
        {
            var presence = new PresenceRegistry();
            var (driver, audio, marks) = MakeDriver(withMarks: true, presence);
            presence.AddPresent(new PatientId(PatientId1), new WorldPos(0, 0, 0));

            driver.OnTickEdge(10);   // 首拍:13 判呼吸层应起

            Assert.That(audio.BeginLoopCount, Is.GreaterThan(0),
                "前置:首拍应起呼吸层(否则本测的判据面不存在)");

            bool hasBreath = false;
            foreach (PatientCueMarkData m in marks.Marks)
                if (m.Kind == PatientCueVisual.Breath && m.PatientId == PatientId1) hasBreath = true;

            Assert.That(hasBreath, Is.True,
                $"呼吸须有可见对应物(AC-13-F1 第三类)—— 实得 [{string.Join(", ", marks.Marks)}]");
        }

        /// <summary>呼吸的可见物**不逐拍重复投递**(起一次 = 一个形)。</summary>
        [Test]
        public void test_breath_notRedeliveredEveryTick()
        {
            var presence = new PresenceRegistry();
            var (driver, _, marks) = MakeDriver(withMarks: true, presence);
            presence.AddPresent(new PatientId(PatientId1), new WorldPos(0, 0, 0));

            for (long t = 10; t <= 60; t++) driver.OnTickEdge(t);

            int breathMarks = 0;
            foreach (PatientCueMarkData m in marks.Marks)
                if (m.Kind == PatientCueVisual.Breath) breathMarks++;

            Assert.That(breathMarks, Is.EqualTo(1),
                "呼吸层只起一次 ⇒ 形也只投一次(逐拍重投会让容器被同一病人刷穿)");
        }

        /// <summary>**乙路:不给缝 ⇒ 零投递,播音逐位不变**(既有夹具的默认)。</summary>
        [Test]
        public void test_withoutSink_audioUnaffected()
        {
            var presence = new PresenceRegistry();
            var (driver, audio, marks) = MakeDriver(withMarks: false, presence);
            presence.AddPresent(new PatientId(PatientId1), new WorldPos(0, 0, 0));

            AdvanceToFirstCough(driver);

            Assert.That(audio.EmitCount, Is.GreaterThan(0), "不给可见物缝不得影响播音");
            Assert.That(audio.BeginLoopCount, Is.GreaterThan(0), "不给可见物缝不得影响呼吸层");
            Assert.That(marks.Marks, Is.Empty, "未给缝 ⇒ 不得投递(既有装配行为逐位不变)");
        }

        /// <summary>**离场摘除** —— 病人离开在场册 ⇒ 该病人的可见物一并摘掉(不留残影)。</summary>
        [Test]
        public void test_pruneAbsent_clearsThatPatient()
        {
            var presence = new PresenceRegistry();
            var (driver, _, marks) = MakeDriver(withMarks: true, presence);
            presence.AddPresent(new PatientId(PatientId1), new WorldPos(0, 0, 0));
            driver.OnTickEdge(10);

            presence.RemovePresent(new PatientId(PatientId1));
            driver.PruneAbsent(11);

            Assert.That(marks.ClearedPatients, Does.Contain(PatientId1),
                "离场须摘除该病人的可见物 —— 否则屏上留一个不存在的病人的形");
        }

        /// <summary>**全量收尾**(场景卸载 / 读档前)。</summary>
        [Test]
        public void test_stopAll_clearsEverything()
        {
            var presence = new PresenceRegistry();
            var (driver, _, marks) = MakeDriver(withMarks: true, presence);
            presence.AddPresent(new PatientId(PatientId1), new WorldPos(0, 0, 0));
            driver.OnTickEdge(10);

            driver.StopAll();

            Assert.That(marks.ClearAllCount, Is.EqualTo(1), "StopAll 须清空可见物(不留残影)");
        }

        /// <summary>**语义映射三分支**:咳嗽 / 呻吟各自对应,未知值**具名抛出**。
        /// <para>⚠️ `Groan` 这一支**今日无生产生产者**(13 的调度在 P0 恒传 `CueKind.Cough`)——
        /// 本测直测映射,不主张端到端可达(见证据件「本件不主张」)。</para></summary>
        [Test]
        public void test_visualMap_coversBothCueKinds_andRejectsUnknown()
        {
            Assert.That(PatientCueVisualMap.Of(CueKind.Cough), Is.EqualTo(PatientCueVisual.Cough));
            Assert.That(PatientCueVisualMap.Of(CueKind.Groan), Is.EqualTo(PatientCueVisual.Groan));

            Assert.Throws<ArgumentOutOfRangeException>(
                () => PatientCueVisualMap.Of((CueKind)99),
                "未知语义不得静默落进咳嗽形(错标比崩溃更难发现)");
        }
    }
}
