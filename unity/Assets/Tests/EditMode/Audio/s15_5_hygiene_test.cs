// S15-5 —— 「TD-audio-029 + TD-audio-032 合并卫生批」的验收夹具。
//   权威依据:sprint-15.md S15-5 六条判据 · docs/tech-debt-register.md 的 TD-audio-029 / TD-audio-032
//   ADR-028 ①⑤(声源池唯一所有者 · cue 驱动生命周期)· ADR-005(tick 抽象点 = long)
//
// ⚠️ **被测对象全部是真生产件**:`AudioSourcePool` / `AudioSourceRenderTarget` /
//    `OneShotReclaimPump` / `PatientCueDriver` / `PatientCueSchedule` / `PresenceRegistry`。
//    注入的假件只有生产代码自己声明的外部缝:`IAudioCueSink` / `IVitalsQuery` / `Func<AudioClip>`。
//
// ⚠️ **「播完」如何被观测**:生产 = `OneShotReclaimPump` 每 tick 边沿问 `AudioSource.isPlaying`。
//    真 `isPlaying` 的时序(播放态 vs 编辑器非播放态)EditMode 不可证 ⇒
//    本夹具把「播放器说完了」这一**外部事实**显式注入(直接停源 / 传探针),
//    测的是池与渲染目标在**收到该事实之后**是否真的归还 —— 那才是 TD-audio-029 的缺陷面。
//    `isPlaying` 本身的 PlayMode 半边 = NOT-RUN(如实登记,禁借绿)。
//
// 测试纪律:arrange/act/assert · 无随机 · 无时间依赖 · 零文件系统**写**(源码行数度为只读)。

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using UnityEngine;
using DaYiJingCheng.Gameplay.Boot;                  // OneShotReclaimPump / PatientCueDriver
using DaYiJingCheng.Gameplay.PatientAI;             // PresenceRegistry / PatientBehaviorDirector / CueIntervals
using DaYiJingCheng.Gameplay.Presentation.Audio;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Tests.Unit.Audio
{
    [TestFixture]
    internal sealed class S15_5HygieneTest
    {
        private readonly List<UnityEngine.Object> _cleanup = new List<UnityEngine.Object>();

        // 本文件在 unity/Assets/Tests/EditMode/Audio/ ⇒ 仓库根 = 5 层
        private static string RepoRoot([CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile) ?? ".",
                "..", "..", "..", "..", ".."));

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _cleanup.Count; i++)
                if (_cleanup[i] != null) UnityEngine.Object.DestroyImmediate(_cleanup[i]);
            _cleanup.Clear();
            foreach (GameObject root in Resources.FindObjectsOfTypeAll<GameObject>()
                         .Where(g => g != null && g.name == AudioSourcePool.RootName && g.scene.IsValid()))
                UnityEngine.Object.DestroyImmediate(root);
        }

        private AudioSourcePool MakePool()
        {
            var pool = new AudioSourcePool();
            _cleanup.Add(pool.Root);
            return pool;
        }

        private AudioClip MakeClip(string name = "s15-5-clip")
        {
            AudioClip clip = AudioClip.Create(name, 64, 1, 44100, false);
            _cleanup.Add(clip);
            return clip;
        }

        private AudioSourceRenderTarget MakeTarget(AudioSourcePool pool, AudioClip clip = null)
        {
            var spatial = new SpatializationDirector(latticeSize: 1f, pool: pool);
            AudioClip c = clip ?? MakeClip();
            return new AudioSourceRenderTarget(pool, spatial, 1f, _ => c, busForCue: null);
        }

        private static AudioCueDto Cue(int source) => new AudioCueDto(cue: 3, intensity: 200,
            cell: new Int3(0, 0, 0), source: source, looped: false);

        // ══════════════════════════════════════════════════════════════════
        // ① TD-audio-029 —— 一次性 cue 播完 ⇒ 归还(闲置栈回流 · 在响计数回落)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_td029_oneShot_pending_afterPlay_notYetReleased_sameTick()
        {
            // Arrange
            AudioSourcePool pool = MakePool();
            AudioSourceRenderTarget target = MakeTarget(pool);

            // Act
            target.PlayOneShot(Cue(source: 7));
            pool.ReclaimExpired();   // 同 tick 的回收:**源刚开播,不得被掐**

            // Assert:①「在响」语义 —— 开播即计入;同帧回收不得把它提前摘掉
            Assert.That(pool.PendingOneShotCount, Is.EqualTo(1));
            Assert.That(pool.ActiveCount, Is.EqualTo(1), "刚开播的源被同帧回收摘掉了(JustQueued 保护失效)");

            // ② **回收回调口径**:同帧回收把「刚排队」标记消耗掉(⇒ 下一轮起可归还)。
            //    ⚠️ 若标记只跳过、从不清除 ⇒ 源**永不**归还 ⇒ 下面这一断言红(F3 反证)。
            pool.SourceForActor(7).Stop();
            Assert.That(OneShotReclaimPump.Pump(pool), Is.EqualTo(1),
                "下一次回收未归还已停的源 —— 「刚排队」标记没人清(TD-audio-029 的回流路径断了)");
            Assert.That(pool.ActiveCount, Is.EqualTo(0));
        }

        [Test]
        public void test_td029_oneShot_markFinished_returnsSourceToFreeStack()
        {
            // Arrange
            AudioSourcePool pool = MakePool();
            AudioSourceRenderTarget target = MakeTarget(pool);
            target.PlayOneShot(Cue(source: 7));
            AudioSource first = pool.SourceForActor(7);

            // Act:外部观测面报「播完了」
            bool marked = pool.MarkFinished(7);
            int reclaimed = pool.ReclaimExpired();

            // Assert
            Assert.That(marked, Is.True, "待归还的源未被 MarkFinished 认领");
            Assert.That(pool.SourceForActor(7), Is.Null, "已播完的源仍在「在响」集合里(ActiveCount 失真)");
            Assert.That(pool.ActiveCount, Is.EqualTo(0));
            Assert.That(pool.PendingOneShotCount, Is.EqualTo(0));

            // 闲置栈真回流:下一次 spawn(另一 actor)复用**同一条实例**
            AudioSource again = pool.Spawn(actorId: 8, 0, 0, 0, cueId: 3);
            Assert.That(again, Is.SameAs(first), "闲置栈未回流 —— 每次 one-shot 都会新建 AudioSource");
            Assert.That(reclaimed, Is.EqualTo(0), "MarkFinished 已当场归还 ⇒ ReclaimExpired 不再重复计");
        }

        [Test]
        public void test_td029_pump_liveSourceStays_finishedSourceIsReclaimed()
        {
            // Arrange:两条一次性源;一条「还在播」,一条「播放器说完了」
            AudioSourcePool pool = MakePool();
            AudioSourceRenderTarget target = MakeTarget(pool);
            target.PlayOneShot(Cue(source: 7));
            target.PlayOneShot(Cue(source: 8));
            pool.ReclaimExpired();                                   // 落「非新排队」态
            pool.SourceForActor(8).Stop();                           // 外部事实:8 播完了

            // Act
            int reclaimed = OneShotReclaimPump.Pump(pool);

            // Assert
            Assert.That(reclaimed, Is.EqualTo(1), "归还数 ≠ 1");
            Assert.That(pool.SourceForActor(8), Is.Null, "确认播完的源未归还");
            Assert.That(pool.SourceForActor(7), Is.Not.Null, "未确认播完的源被误归还(会掐断正在响的 cue)");
        }

        [Test]
        public void test_td029_loopSource_neverReclaimedByPump()
        {
            // Arrange:循环源 + 一条一次性源
            AudioSourcePool pool = MakePool();
            AudioSourceRenderTarget target = MakeTarget(pool);
            target.StartLoop(new AudioCueDto(cue: 4, intensity: 100, cell: new Int3(0, 0, 0),
                source: 9, looped: true), handleId: 1);
            target.PlayOneShot(Cue(source: 7));
            pool.ReclaimExpired();
            pool.SourceForActor(7).Stop();

            // Act
            OneShotReclaimPump.Pump(pool);
            OneShotReclaimPump.Pump(pool);

            // Assert:循环源(负键)不受一次性回收影响
            Assert.That(pool.SourceForActor(-2), Is.Not.Null, "循环源被一次性回收路径归还了(呼吸层会断)");
            Assert.That(pool.PendingOneShotCount, Is.EqualTo(0));
        }

        [Test]
        public void test_td029_probeFace_releasesWhenOwnerHandedItOver()
        {
            // Arrange:探针面(池外持有者的判活面)—— 探针被销毁 ⇒ 池视为播完
            AudioSourcePool pool = MakePool();
            AudioSource source = pool.Spawn(actorId: 12, 0, 0, 0, cueId: 3);
            var probe = new GameObject("s15-5-probe");
            try
            {
                pool.EnqueueOneShot(12, source, standaloneProbe: probe);
                pool.ReclaimExpired();
                Assert.That(pool.SourceForActor(12), Is.Not.Null, "探针尚活却被判完");

                // Act
                UnityEngine.Object.DestroyImmediate(probe);
                int reclaimed = pool.ReclaimExpired();

                // Assert
                Assert.That(reclaimed, Is.EqualTo(1), "探针已失活 ⇒ 应视为播完并归还");
            }
            finally
            {
                if (probe != null) UnityEngine.Object.DestroyImmediate(probe);
            }
        }

        [Test]
        public void test_td029_nullProbe_isNotJudgedFinished()
        {
            // Arrange:无池外持有者 ⇒ 探针面不可判(不可判 ≠ 已归还)
            AudioSourcePool pool = MakePool();
            AudioSource source = pool.Spawn(actorId: 13, 0, 0, 0, cueId: 3);
            pool.EnqueueOneShot(13, source);
            pool.ReclaimExpired();

            // Assert
            Assert.That(pool.SourceForActor(13), Is.Not.Null, "无探针的源被探针面误判为播完");
            Assert.That(pool.PendingOneShotCount, Is.EqualTo(1));
        }

        [Test]
        public void test_td029_enqueueWithForeignSource_throws()
        {
            // Arrange:池键错位(把别的源排到本 actor 名下)⇒ 必须 fail-loud
            AudioSourcePool pool = MakePool();
            AudioSource mine = pool.Spawn(actorId: 7, 0, 0, 0, cueId: 3);
            _ = mine;
            pool.Spawn(actorId: 8, 0, 0, 0, cueId: 3);

            // Act + Assert
            Assert.Throws<InvalidOperationException>(() => pool.EnqueueOneShot(7, pool.SourceForActor(8)),
                "归还落到别的 actor 身上时须抛,不得静默");
        }

        // ══════════════════════════════════════════════════════════════════
        // ② TD-audio-032 ② —— tick 簿记不再截成 int(2^31 之后仍正常调度)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_td032_2_decide_afterIntMaxValue_stillDue()
        {
            // Arrange:lastEmit = int.MaxValue(截成 int 回绕后的边界值)
            const long lastEmit = (long)int.MaxValue;

            // Act:一跳之后
            CueDispatch next = PatientCueSchedule.Decide(
                BehaviorState.Idle, TerminalFlag.None, lastEmit + 40, lastEmit,
                new PatientId(7), SymptomTier.Severe, 0.5f, CueIntervals.Default,
                breathAlive: true, kind: CueKind.Cough, entryTick: lastEmit);

            // Assert:间隔已过 ⇒ 必发(若把 tick 截成 int,int.MaxValue+40 回绕成负数 ⇒ 恒不 due)
            Assert.That(next.EmitOneShot, Is.True, "2^31 相邻一拍后调度器不再判 due —— tick 在别处被截成了 int");
        }

        [Test]
        public void test_td032_2_driver_keepsTickPastIntMax()
        {
            // Arrange:把 tick 推到 2^31 边界**两侧**。间隔取 3 tick(合法:0 < 3 < 90 ≤ 120)
            //   ⇒ 边界前后各至少一拍 due,截断与否在**是否 due** 上分叉。
            var intervals = new CueIntervals(severe: 3, mild: 90, recover: 120);
            var presence = PresenceWith(patientId: 7);
            var driver = new PatientCueDriver(new CountingSink(), new FakeVitals(0.5f), presence,
                new PatientBehaviorDirector(new FakeVitals(0.5f), BehaviorBands.Default),
                intervals: intervals, coughCue: 0, knowsClinic: _ => true);

            const long entry = (long)int.MaxValue - 5;
            int phase = PatientCue.Phase(new PatientId(7), SymptomTier.Severe, intervals);
            Assert.That(phase, Is.LessThan(3), "前置:相位须 < 间隔(分段常函数的定义)");

            // Act:从 entry 连跑到边界之后 3 拍
            for (long t = entry; t <= (long)int.MaxValue + 3; t++) driver.OnTickEdge(t);

            // Assert:边界**之后**仍按同一间隔发咳。
            //   若 tick 在簿记 / 调度处被截成 int:`int.MaxValue + 1` 回绕成负数 ⇒ `tick >= firstDue` 恒假
            //   ⇒ 边界后一拍都发不出。故这里必须 > 边界前那一拍的计数。
            int before = 0;
            for (long t = entry; t <= (long)int.MaxValue; t++) if (t >= entry + phase && (t - (entry + phase)) % 3 == 0) before++;
            Assert.That(driver.EmitCount, Is.GreaterThan(before),
                "2^31 边界之后一次性 cue 停发了 —— tick 在别处被截成了 int(TD-audio-032 ②)");
            Assert.That(driver.LastDrivenTick, Is.EqualTo((long)int.MaxValue + 3));
        }

        // ══════════════════════════════════════════════════════════════════
        // ④ TD-audio-032 ④ —— 在场快照复用 + 清扫面 = 五本簿记并集
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_td032_4_presentIds_snapshotReused_untilMembershipChanges()
        {
            // Arrange
            var presence = new PresenceRegistry();
            presence.AddPresent(new PatientId(1), new WorldPos(0, 0, 0));
            presence.AddPresent(new PatientId(2), new WorldPos(1, 0, 0));

            // Act
            IReadOnlyCollection<int> a = presence.PresentPatientIds();
            IReadOnlyCollection<int> b = presence.PresentPatientIds();
            presence.AddPresent(new PatientId(3), new WorldPos(2, 0, 0));
            IReadOnlyCollection<int> c = presence.PresentPatientIds();

            // Assert:成员未变 ⇒ 同一实例(零每拍分配);成员变了 ⇒ 新建且含新成员
            Assert.That(b, Is.SameAs(a), "成员未变却重建了快照 —— 每 tick 边沿仍在分配数组(TD-audio-032 ④)");
            Assert.That(c, Is.Not.SameAs(a), "成员变了仍返回旧快照 —— 会漏掉新出现的病人");
            Assert.That(c.Count, Is.EqualTo(3));
        }

        [Test]
        public void test_td032_4_pruneAbsent_clearsEmitOnlyPatientBookkeeping()
        {
            // Arrange:病人发过一次咳(无呼吸句柄),随后离场
            var sink = new CountingSink();
            var presence = PresenceWith(patientId: 7);
            var driver = new PatientCueDriver(sink, new FakeVitals(0.5f), presence,
                new PatientBehaviorDirector(new FakeVitals(0.5f), BehaviorBands.Default),
                coughCue: 0, knowsClinic: _ => true);
            for (long t = 0; t <= 60; t++) driver.OnTickEdge(t);
            Assert.That(driver.EmitCount, Is.GreaterThan(0), "前置:病人至少发过一次咳");

            // Act:离场 ⇒ 清扫
            presence.RemovePresent(new PatientId(7));
            driver.PruneAbsent(61);

            // Assert ①(判别面):清扫后簿记**归零** —— 该病人只有发咳簿记(无呼吸句柄),
            //   此前只跟 `_breathHandles` 走 ⇒ 他的 `_entryTick` / `_lastEmitTick` 行永不清。
            Assert.That(driver.TrackedPatientCount, Is.EqualTo(0),
                "离场病人的发咳簿记仍在 —— 清扫面只跟呼吸句柄走(TD-audio-032 ④)");

            // Assert ②:旧尾音簿记不得跨「停止」存活
            Assert.That(driver.PausedBreathCount, Is.EqualTo(0));

            // Assert ③:重新出现后照常建账(清扫不得把「还在场的」也清掉)
            presence.AddPresent(new PatientId(7), new WorldPos(1, 0, 2));
            driver.OnTickEdge(62);
            Assert.That(driver.TrackedPatientCount, Is.EqualTo(1), "重新出现的病人未建账");
        }

        // ══════════════════════════════════════════════════════════════════
        // ③ TD-audio-032 ③ —— 方法体 ≤ 40 物理行(源码物理行,由真源文件量出)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_td032_3_splitMethods_withinLineBudget()
        {
            // Arrange
            string src = StripComments(File.ReadAllText(Path.Combine(RepoRoot(), "unity", "Assets",
                "Gameplay.Boot", "PatientCueDriver.cs")));

            // Assert:主方法只许编排;每个拆分出来的私有方法各自 ≤ 40 物理行
            foreach (string sig in new[]
                     {
                         "public void OnTickEdge(long tick)",
                         "private void StepOnePatient(int patientId, long tick)",
                         "private CueDispatch DecideFor(",
                         "private void EmitOneShotIfDue(",
                         "private void PruneOneAbsent(",
                     })
            {
                int lines = ExtractMethodText(src, sig).Split('\n').Length;
                Assert.That(lines, Is.LessThanOrEqualTo(40),
                    $"「{sig}」实得 {lines} 物理行 > 40 —— 拆分不得把超标搬进私有方法");
            }
        }

        /// <summary>`TD-audio-032` ③ 的**第二半边**(S15-7 起登记未修):`BootRoot.BuildAudioSeams`
        /// 原 45 物理行 &gt; 40。</summary>
        /// <remarks><b>判别力</b>:往 `BuildAudioSeams` 里垫满 41 行 ⇒ 本测红(见反证记录)。
        /// <para>⚠️ 本测**不**复用 `PatientCueDriver.cs` 那次扫描:两者是不同文件、不同方法集,
        /// 合成一条会让「哪一半红了」不可分辨(S15-7 记的正是「拆分动作只兑现一半 = 未证」)。</para></remarks>
        [Test]
        public void test_td032_3_buildAudioSeams_withinLineBudget()
        {
            // Arrange
            string src = StripComments(File.ReadAllText(Path.Combine(RepoRoot(), "unity", "Assets",
                "Gameplay.Boot", "BootRoot.cs")));

            // Assert:主方法只许编排;两个抽出来的私有方法各自 ≤ 40 物理行
            foreach (string sig in new[]
                     {
                         "public static AudioAssemblySeams BuildAudioSeams(",
                         "private static AudioMixer ResolveMixerOrFail(",
                         "private static IReadOnlyList<string> ApplyPlayerBusDefaultsOrFail(",
                     })
            {
                int lines = ExtractMethodText(src, sig).Split('\n').Length;
                Assert.That(lines, Is.LessThanOrEqualTo(40),
                    $"「{sig}」实得 {lines} 物理行 > 40 —— `TD-audio-032` ③ 的 BuildAudioSeams 半边" +
                    "(S15-7 实测 45 行)回潮,或拆分把超标搬进了新的私有方法");
            }
        }

        [Test]
        public void test_td032_3_budgetMeasuresThePreSplitBody()
        {
            // 判别面自校准:把**拆分前**的整段体(原 `OnTickEdge` 的内容)喂给同一个计数器,
            // 它必须判超界。否则「计数器恒小」—— 上一条断言对任何实现都放行(假绿)。
            string preSplit =
                "public void OnTickEdge(long tick)\n{\n"
                + string.Join("\n", Enumerable.Range(0, 57).Select(i => "    var x" + i + " = 0;"))
                + "\n}\n";
            int lines = ExtractMethodText(preSplit, "public void OnTickEdge(long tick)").Split('\n').Length;
            Assert.That(lines, Is.GreaterThan(40),
                $"计数器对 60 行体给出 {lines} ⇒ 恒小,行数界无判别力");
        }

        // ══════════════════════════════════════════════════════════════════
        // 基础设施
        // ══════════════════════════════════════════════════════════════════

        private static PresenceRegistry PresenceWith(int patientId)
        {
            var presence = new PresenceRegistry();
            presence.AddPresent(new PatientId(patientId), new WorldPos(1, 0, 2));
            return presence;
        }

        private sealed class FakeVitals : IVitalsQuery
        {
            private readonly float _position;
            public FakeVitals(float position) => _position = position;
            public VitalsDto GetVitals(PatientId p) => new VitalsDto(_position, 0f, 0, 0);
        }

        private sealed class CountingSink : IAudioCueSink
        {
            public int EmitCount;
            public bool LastLooped;
            public void Emit(in AudioCueDto cue) { EmitCount++; }
            public AudioCueHandle BeginLoop(in AudioCueDto cue) { LastLooped = true; return new AudioCueHandle(1); }
            public void EndLoop(AudioCueHandle handle) { }
            public void SetTier(TierSource source) { }
        }

        private static string StripComments(string src)
        {
            var sb = new System.Text.StringBuilder(src.Length);
            for (int i = 0; i < src.Length; i++)
            {
                char c = src[i];
                if (c == '"' || c == '\'')
                {
                    char quote = c; bool verbatim = i > 0 && src[i - 1] == '@';
                    sb.Append(c);
                    for (i++; i < src.Length; i++)
                    {
                        sb.Append(src[i]);
                        if (!verbatim && src[i] == '\\' && i + 1 < src.Length) { sb.Append(src[i + 1]); i++; continue; }
                        if (src[i] == quote) break;
                    }
                    continue;
                }
                if (c == '/' && i + 1 < src.Length && src[i + 1] == '/') { while (i < src.Length && src[i] != '\n') i++; sb.Append('\n'); continue; }
                if (c == '/' && i + 1 < src.Length && src[i + 1] == '*')
                {
                    i += 2; while (i + 1 < src.Length && !(src[i] == '*' && src[i + 1] == '/')) i++;
                    i++; sb.Append(' '); continue;
                }
                sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>切出方法的**整物理行区间**(签名行首 → 闭括号行尾)—— 与 pipeline_gate_test 同口径,禁换口径。</summary>
        private static string ExtractMethodText(string src, string signatureFragment)
        {
            int sig = src.IndexOf(signatureFragment, StringComparison.Ordinal);
            Assert.That(sig, Is.GreaterThanOrEqualTo(0),
                $"源内未找到「{signatureFragment}」—— 夹具过期(方法被改名 / 搬家)");
            int lineStart = src.LastIndexOf('\n', sig) + 1;
            int open = src.IndexOf('{', sig);
            Assert.That(open, Is.GreaterThanOrEqualTo(0), "签名后无方法体 `{`");
            int depth = 0, close = -1;
            for (int i = open; i < src.Length; i++)
            {
                if (src[i] == '{') depth++;
                else if (src[i] == '}' && --depth == 0) { close = i; break; }
            }
            Assert.That(close, Is.GreaterThan(open), "方法体未闭合");
            int lineEnd = src.IndexOf('\n', close);
            if (lineEnd < 0) lineEnd = src.Length;
            return src.Substring(lineStart, lineEnd - lineStart);
        }
    }
}
