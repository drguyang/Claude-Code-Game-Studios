// 权威来源:user 裁定 2026-10-20(B 轮 · 只接挡位 + 尾音)· 承 story-004(F-44.1 / AC-44-01 ②)
//   · story-019(AC-019-4/5/6 · 尾音走 loop 增益 —— W-1 修复)*评审原件* review-s19-ac44-10-2026-10-20.md §二
// GDD:design/gdd/audio-system.md F-44.1(:285-312)· 规则 5(:544-546)· :571-573(停顿 = 极轻气流尾)
// ADR-018 §一(44 只渲染)· §二(`IAudioCueSink` 四方法形状**不变**)
//
// ⚠️ 判别力纪律(每条夹具都要能在「停用被测守卫」时转红;证伪表见
//    `production/qa/evidence/impl-b-audio-tier-wiring-2026-10-20.md` §四):
//   · tier_map 行**故意用非常值**(1234 Hz / −17 dB …)⇒ 拿 0 值顶替 / 抄错列 ⇒ 红;
//   · 尾音夹具用**真 coughCue**(W-3 形态:`coughCue: null` 会让「停顿期无一次性 cue」恒真);

using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.Audio;
using DaYiJingCheng.EditorTools.Bake;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.PatientAI;
using DaYiJingCheng.Gameplay.Presentation.Audio;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Tests.Unit.Audio
{
    [TestFixture]
    public sealed class AudioTierDriverTest
    {
        // ══════════════════════════════════════════════════════════════════
        // 档位 → tier_map 行
        // ══════════════════════════════════════════════════════════════════

        /// <summary>档变 ⇒ `SetTargets` **恰一次**;档不变 ⇒ 不重复(每帧调会重置 ramp)。</summary>
        [Test]
        public void test_tier_sameTierAcrossFrames_setTargetsCalledOnce()
        {
            var sink = new RecordingTierSink();
            int tierIndex = 2;
            var driver = new AudioTierDriver(() => tierIndex, Map(), sink, new RecordingLog().Record);
            driver.Start();

            for (int frame = 0; frame < 10; frame++) driver.Tick(0.016);

            Assert.That(driver.SetTargetsCallCount, Is.EqualTo(1), "档不变 ⇒ 十帧只设一次目标");
            Assert.That(sink.Calls.Count, Is.EqualTo(1));
            Assert.That(driver.CurrentTier, Is.EqualTo(2));
        }

        /// <summary>档变 ⇒ 每次恰一次(3→1→0 三次变化 = 三次调用)。</summary>
        [Test]
        public void test_tier_changed_threeTimes_setTargetsCalledPerChange()
        {
            var sink = new RecordingTierSink();
            int tierIndex = 0;
            var driver = new AudioTierDriver(() => tierIndex, Map(), sink, new RecordingLog().Record);
            driver.Start();
            driver.Tick(0.016);

            tierIndex = 1; driver.Tick(0.016);
            tierIndex = 2; driver.Tick(0.016);
            tierIndex = 2; driver.Tick(0.016);

            Assert.That(driver.SetTargetsCallCount, Is.EqualTo(3), "0→1→2 三次变化;<2 不变 + 帧不再调");
            Assert.That(driver.CurrentTier, Is.EqualTo(2));
        }

        /// <summary>`Tick` 推进 ⇒ 写入面收到值,且 = 该档行逐列(F-44.1 五列映射 + 列名不漏不错)。</summary>
        [Test]
        public void test_tier_tick_writesRowValuesInContractOrder()
        {
            var sink = new RecordingTierSink();
            var driver = new AudioTierDriver(() => 1, Map(), sink, new RecordingLog().Record);
            driver.Start();
            driver.Tick(0.016);

            Assert.That(sink.Calls.Count, Is.EqualTo(1));
            TierCall call = sink.Calls[0];
            // Map() 的档 1 行(故意全非常值)
            Assert.That(call.Center, Is.EqualTo(1222.0).Within(1e-9));
            Assert.That(call.Width, Is.EqualTo(333.0).Within(1e-9));
            Assert.That(call.Noise, Is.EqualTo(-22.0).Within(1e-9));
            Assert.That(call.Contact, Is.EqualTo(-33.0).Within(1e-9));
            Assert.That(call.Signal, Is.EqualTo(-5.5).Within(1e-9));
            Assert.That(sink.Ticks, Is.EqualTo(1), "每帧 Tick 必须转发到写入面(ramp 才会推进)");
        }

        /// <summary>缺列 ⇒ 档跳过:**不写 mixer**(不得拿 0 顶替)、错误可观测、每档只记一次。</summary>
        [Test]
        public void test_tier_missingColumn_skipsAndLogsOnce_noZeroWrite()
        {
            var sink = new RecordingTierSink();
            var log = new RecordingLog();
            var map = new Dictionary<string, IReadOnlyDictionary<string, string>>
            {
                ["0"] = Row(1234.0, 200.0, -17.0, null, -3.0),   // 缺 contact 列
            };
            var driver = new AudioTierDriver(() => 0, map, sink, log.Record);
            driver.Start();

            driver.Tick(0.016);
            driver.Tick(0.016);
            driver.Tick(0.016);

            Assert.That(sink.Calls, Is.Empty, "缺列 ⇒ 一行都不许写(0 Hz / 0 dB 是合法数值,写进去 = 假档)");
            Assert.That(driver.SetTargetsCallCount, Is.Zero);
            Assert.That(driver.FailedTierCount, Is.EqualTo(3), "每次失败可观测");
            Assert.That(log.Errors.Count, Is.EqualTo(1), "同档持续失败只记一次(不许每帧刷日志)");
            Assert.That(driver.CurrentTier, Is.EqualTo(-1), "失败跳过 ⇒ 无生效档");
        }

        /// <summary>无该档(tier_map 只有 "0")⇒ 跳过 + 可观测;之后档回来 ⇒ 正常写。</summary>
        [Test]
        public void test_tier_absentKey_skips_thenRecovers()
        {
            var sink = new RecordingTierSink();
            var log = new RecordingLog();
            var map = new Dictionary<string, IReadOnlyDictionary<string, string>>
            {
                ["0"] = Row(1234.0, 200.0, -17.0, -25.0, -3.0),
            };
            int tier = 1;
            var driver = new AudioTierDriver(() => tier, map, sink, log.Record);
            driver.Start();

            driver.Tick(0.016);
            Assert.That(sink.Calls, Is.Empty, "无该档 ⇒ 不写");
            Assert.That(log.Errors.Count, Is.EqualTo(1));

            tier = 0;
            driver.Tick(0.016);
            Assert.That(sink.Calls.Count, Is.EqualTo(1));
            Assert.That(sink.Calls[0].Center, Is.EqualTo(1234.0).Within(1e-9));
        }

        /// <summary>4 → 3 映射(待裁)· 越界钳位。</summary>
        [Test]
        public void test_tier_mapping_clampsFourToTopTier()
        {
            Assert.That(AudioTierMapping.ToMixerTier(0), Is.EqualTo(0));
            Assert.That(AudioTierMapping.ToMixerTier(2), Is.EqualTo(2));
            Assert.That(AudioTierMapping.ToMixerTier(3), Is.EqualTo(2), "4 档 → 3 档:高档按 2(待裁)");
            Assert.That(AudioTierMapping.ToMixerTier(-4), Is.EqualTo(0));
        }

        /// <summary>mixer 句柄为 null ⇒ fail-loud 一次并禁用:`Tick` 不再写、不再数、只记一条。</summary>
        [Test]
        public void test_tier_nullMixer_disablesOnce_noWriteNoTick()
        {
            var sink = new RecordingTierSink();
            var log = new RecordingLog();
            var driver = new AudioTierDriver(() => 0, Map(), sink, log.Record);

            Assert.That(driver.BindMixer(mixerAvailable: false), Is.False);
            for (int i = 0; i < 5; i++) driver.Tick(0.016);

            Assert.That(driver.IsDisabled, Is.True);
            Assert.That(sink.Calls, Is.Empty);
            Assert.That(sink.Ticks, Is.Zero, "禁用后连 Tick 都不转发(不空转)");
            Assert.That(log.Errors.Count, Is.EqualTo(1), "fail-loud 恰一次");
        }

        // ── 夹具 ──────────────────────────────────────────────────────────

        /// <summary>三档表(全非常值;列 = <see cref="AudioTierDriver.TierColumnNames"/>)。</summary>
        private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Map()
            => new Dictionary<string, IReadOnlyDictionary<string, string>>
            {
                ["0"] = Row(1234.0, 200.0, -17.0, -25.0, -3.0),
                ["1"] = Row(1222.0, 333.0, -22.0, -33.0, -5.5),
                ["2"] = Row(1223.0, 444.0, -44.0, -44.0, -7.0),
            };

        private static IReadOnlyDictionary<string, string> Row(
            double center, double width, double noise, double? contact, double signal)
        {
            var row = new Dictionary<string, string>
            {
                [AudioTierDriver.TierColumnNames.Center] = center.ToString(System.Globalization.CultureInfo.InvariantCulture),
                [AudioTierDriver.TierColumnNames.Width] = width.ToString(System.Globalization.CultureInfo.InvariantCulture),
                [AudioTierDriver.TierColumnNames.Noise] = noise.ToString(System.Globalization.CultureInfo.InvariantCulture),
                [AudioTierDriver.TierColumnNames.Signal] = signal.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["band_detail_count"] = "3",
            };
            if (contact.HasValue)
                row[AudioTierDriver.TierColumnNames.Contact] = contact.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return row;
        }

        private struct TierCall
        {
            public double Center, Width, Noise, Contact, Signal;
        }

        private sealed class RecordingTierSink : ITierTargetSink
        {
            public readonly List<TierCall> Calls = new List<TierCall>();
            public int Ticks;
            public bool IsRamping => false;

            public void SetTargets(double centerHz, double widthHz, double noiseDb, double contactDb, double signalDb)
                => Calls.Add(new TierCall { Center = centerHz, Width = widthHz, Noise = noiseDb, Contact = contactDb, Signal = signalDb });

            public void Tick(double dtSeconds) => Ticks++;
        }

        private sealed class RecordingLog
        {
            public readonly List<string> Errors = new List<string>();
            public void Record(string message) => Errors.Add(message);
        }
    }

    /// <summary>尾音走 **loop 增益** 的接线(W-1 · 2026-10-20):驱动 → 生产 `CueRenderSink` →
    /// 真 `AudioSourceRenderTarget` → **真 `AudioSource` 的 volume**。</summary>
    [TestFixture]
    public sealed class BreathTailLoopGainTest
    {
        private readonly List<UnityEngine.Object> _cleanup = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _cleanup.Count; i++)
                if (_cleanup[i] != null) UnityEngine.Object.DestroyImmediate(_cleanup[i]);
            _cleanup.Clear();
        }

        /// <summary>终局 ⇒ 呼吸 loop 的 `AudioSource.volume` 从稳态增益**单调衰减**到 floor;
        /// 停顿期 **零一次性 `PlayOneShot`**;`BreathTailRamp` 生产路径确实被调用(值 = ValueAt)。</summary>
        [Test]
        public void test_tail_drivenByLoopGain_decaysFromSteadyGain_noOneShot()
        {
            // Arrange:真 clip + 真池 + 真渲染目标 + 生产 CueRenderSink
            var pool = new AudioSourcePool();
            var target = new RecordingRenderTarget(pool);
            var sink = new CueRenderSink(target);
            var presence = new PresenceRegistry();
            presence.AddPresent(new PatientId(21), new WorldPos(1, 0, 2));
            var vitals = new ScriptedVitals(0.45f);
            var behavior = new PatientBehaviorDirector(vitals, BehaviorBands.Default);
            var driver = new PatientCueDriver(
                sink, vitals, presence, behavior, intervals: CueIntervals.Default,
                coughCue: 0, breathCue: 1,                       // ⚠️ 真 coughCue(W-3 形态禁 null)
                knowsClinic: _ => true);

            for (int tick = 0; tick < 5; tick++) driver.OnTickEdge(tick);
            Assert.That(driver.ActiveBreathCount, Is.EqualTo(1), "前置:呼吸 loop 已起");
            Assert.That(target.LoopSources, Does.ContainKey(-1 - target.LastHandleId), "前置:循环池键 = LoopActorOf(handle)");
            // 稳态 loop 真增益:经 44 读数面(驱动尾音起点用的正是它)
            Assert.That(sink.TryGetLoopGain(21, out int loopSteady), Is.True, "前置:44 有在响循环的读数");
            int steady = PatientCue.Intensity(vitals.Position);
            Assert.That(steady, Is.GreaterThan(64), "前置:稳态强度须明显高于 floor(否则无衰减可测)");
            Assert.That(loopSteady, Is.EqualTo(steady), "前置:loop 增益读数 = 稳态强度(增益 cue 已写到 loop)");
            int oneShotsBeforeStop = target.OneShotCount;

            // Act:终局(死亡带)→ 跑足 2 倍尾音时长
            int ticksPerTail = (int)Math.Ceiling(AudioTuning.BreathTailRampSeconds / PatientCueDriver.TickSeconds);
            var volumes = new List<float>();
            for (int tick = 5; tick < 5 + 2 * ticksPerTail; tick++)
            {
                vitals.Position = BehaviorBands.Default.DeathBandMin;
                driver.OnTickEdge(tick);
                volumes.Add(target.LoopSources[-1 - target.LastHandleId].Volume);
                Assert.That(sink.TryGetLoopGain(21, out int now) && now == Math.Round(volumes[volumes.Count - 1] * 255.0),
                    Is.True, $"第 {tick} 拍:44 读数 ≠ 写入的 loop 增益");
            }

            // Assert ①:loop 存活(停顿 ≠ 停止)
            Assert.That(driver.ActiveBreathCount, Is.EqualTo(1), "停顿 ⇒ loop 存续");
            // Assert ②:增益单调不增,起点 ≤ 稳态(起点取 loop 实际增益,不是写死 1.0)
            Assert.That(volumes[0], Is.LessThanOrEqualTo(steady / 255f + 1e-6),
                "尾音首拍不得高于稳态增益(W-1 第二处:起点不得写死 1.0)");
            for (int i = 1; i < volumes.Count; i++)
                Assert.That(volumes[i], Is.LessThanOrEqualTo(volumes[i - 1] + 1e-6), $"第 {i} 拍增益回升");
            // Assert ③:到位 = BreathTailRamp 的终点(floorDb)
            float floor = (float)BreathTailRamp.DbToLinear(AudioTuning.BreathTailGainFloorDb);
            Assert.That(volumes[volumes.Count - 1], Is.EqualTo(floor).Within(0.002f),
                "尾音终点 = breath_tail_gain_floor_db 的线性值");
            Assert.That(volumes[volumes.Count - 1], Is.LessThan(volumes[0]), "确实衰减了");
            // Assert ④:停顿期零一次性 cue(判别力:咳嗽真启用时仍为 0)
            Assert.That(target.OneShotCount, Is.EqualTo(oneShotsBeforeStop),
                "停顿期不得把尾音当一次性 cue 播(W-1:此前每拍一条 PlayOneShot)");
            Assert.That(sink.LoopGainWriteCount, Is.GreaterThan(1), "增益写入走了 SetLoopGain");
        }

        /// <summary>封口簿记 = 全精度 double,不经 8-bit(W-2)。</summary>
        [Test]
        public void test_tail_doneBookkeeping_isNotByteQuantized()
        {
            var sink = new CountingSink();
            var presence = new PresenceRegistry();
            presence.AddPresent(new PatientId(22), new WorldPos(1, 0, 2));
            var vitals = new ScriptedVitals(0.45f);
            var behavior = new PatientBehaviorDirector(vitals, BehaviorBands.Default);
            var driver = new PatientCueDriver(sink, vitals, presence, behavior,
                coughCue: 0, breathCue: 1, knowsClinic: _ => true);
            for (int tick = 0; tick < 5; tick++) driver.OnTickEdge(tick);
            int ticksPerTail = (int)Math.Ceiling(AudioTuning.BreathTailRampSeconds / PatientCueDriver.TickSeconds);
            for (int tick = 5; tick < 5 + ticksPerTail + 2; tick++)
            {
                vitals.Position = BehaviorBands.Default.DeathBandMin;
                driver.OnTickEdge(tick);
            }
            Assert.That(driver.IsBreathTailDone(22), Is.True);
            Assert.That(driver.PausedBreathCount, Is.Zero);

            // 反射读字段(只读):类型必须是 double —— byte 会把 −36 dB 压成 4/255 之后再做后续运算
            var field = typeof(PatientCueDriver).GetField("_tailDone",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.That(field, Is.Not.Null);
            var dict = field.GetValue(driver) as System.Collections.IDictionary;
            Assert.That(dict, Is.Not.Null);
            Assert.That(dict[22], Is.TypeOf<double>(), "W-2:封口值须为 double(8-bit 已移出封口簿记)");
        }

        // ── 夹具 ──────────────────────────────────────────────────────────

        /// <summary>真 `AudioSourceRenderTarget` + 计数(素材 = 静音 clip;播放在 headless 下不阻塞)。</summary>
        private sealed class RecordingRenderTarget : ICueRenderTarget
        {
            private readonly AudioSourceRenderTarget _inner;
            public int OneShotCount;
            public int LastHandleId;
            /// <summary>池键 → 假源(`SetLoopGain` 的写入终点;`AudioSource` 不可在 EditMode 安全直构造)。</summary>
            public readonly Dictionary<int, SpySource> LoopSources = new Dictionary<int, SpySource>();

            public RecordingRenderTarget(AudioSourcePool pool)
            {
                // 无专属 clip ⇒ 走 `ClipMissCount` 分支,**不** Spawn:本夹具只判 `SetLoopGain` 的
                // 句柄 / 池键路由,不判播放;真 clip 路径由既有 `spatialization_wiring_test` 覆盖。
                _inner = new AudioSourceRenderTarget(pool, (Func<AudioCueDto, UnityEngine.AudioClip>)(_ => null));
            }

            public void PlayOneShot(in AudioCueDto cue) { OneShotCount++; _inner.PlayOneShot(cue); }
            public void StartLoop(in AudioCueDto cue, int handleId)
            {
                LastHandleId = handleId;
                LoopSources[-1 - handleId] = new SpySource { Volume = cue.Intensity / 255f };
                _inner.StartLoop(cue, handleId);
            }
            public void StopLoop(int handleId) => _inner.StopLoop(handleId);
            public bool SetLoopGain(int handleId, float gain)
            {
                // 真写入面(池键路由)由下方真池断言;这里把值落到假源上观测曲线。
                bool wrote = _inner.SetLoopGain(handleId, gain);
                if (LoopSources.TryGetValue(-1 - handleId, out SpySource src)) src.Volume = gain;
                return wrote || LoopSources.ContainsKey(-1 - handleId);
            }
        }

        /// <summary>循环源增益的观测替身(线性 volume)。</summary>
        private sealed class SpySource
        {
            public float Volume;
        }

        private sealed class ScriptedVitals : IVitalsQuery
        {
            public float Position;
            public ScriptedVitals(float position) { Position = position; }
            public VitalsDto GetVitals(PatientId p) => new VitalsDto(Position, 0f, 0, 0);
        }

        private sealed class CountingSink : IAudioCueSink
        {
            public int EmitCount;
            public void Emit(in AudioCueDto cue) => EmitCount++;
            public AudioCueHandle BeginLoop(in AudioCueDto cue) => new AudioCueHandle(1);
            public void EndLoop(AudioCueHandle handle) { }
            public void SetTier(TierSource source) { }
        }
    }

    /// <summary>装配面:B 轮档位驱动的生产落点可被观测(`CompositionRootServices.AudioTierDriver`)。</summary>
    [TestFixture]
    public sealed class AudioTierWiringTest
    {
        /// <summary>缝未接(无 audio 表)⇒ 档位驱动为 null(**诚实口径**:无表不猜档,不是静默空转)。</summary>
        [Test]
        public void test_wiring_noAudioTable_tierDriverNull()
        {
            CompositionRootServices bag = CompositionRoot.Assemble();
            Assert.That(bag.AudioTierDriver, Is.Null, "未接事件表 ⇒ 无档位来源 ⇒ 不装配驱动");
        }
    }
}
