// 权威来源:production/epics/audio-system/story-009-source-lifecycle-handover.md
//   (AC-009-1 EndLoop 自评兜底 / AC-009-2 贴耳交接契约 / AC-009-3 重复过期句柄 / AC-009-4 咳嗽不停呼吸)
//   · EndLoop 兜底 = 呈现侧义务(ADR-001 §一之三 裁决二):从最新快照 Progress 自求值,
//     零网络依赖;时钟源 = sim tick(禁墙钟);有界时间 ≤ LOOP_EVAL_MAX_TICKS
//   · 贴耳交接 = 玩家自因;HANDOVER_MS 窗内交叉淡化;禁同刻双呼吸稳态超窗
//   · 句柄生命周期 = 44 本地持有(ADR-028 ⑤):重复 BeginLoop 以最后一次为准;过期 EndLoop 忽略
//   · 咳嗽不停呼吸 = AC-44-08(与 Story 004 联合断言)
// ADR-001 §一之三 裁决二(EndLoop 兜底)· ADR-028 ⑤(声源池生命周期)· ADR-018 §二(IAudioCueSink)
//
// ⚠️ 落点:故事 Test Evidence 登记路径 = tests/unit/audio_system/loop_lifecycle_test.cs;
//    Unity 只编译 unity/Assets/ 树 ⇒ **真身 = 本文件**(承 Story 001-008/014 同一先例);
//    账本侧由 tests/unit/audio_system/README.md 互链。
// ⚠️ 负向夹具落位:与 Story 007/008 同型 —— 违例 fixture 住**测试命名空间**。
// ⚠️ 扫描键单一出处 = AssemblyGates.AudioModuleNamespacePrefix(测试不重复定义字面量)。
// ⚠️ 测试纪律:arrange/act/assert · 无随机(确定性来源只有显式计数字段)·
//    无时间依赖(System.DateTime / Time 被 Cecil 扫描器判红)· 夹具读取前置 File.Exists。
// ⚠️ 与 Story 008 分工:本文件**不重复**空间化谓词面;只落句柄生命周期 + 交接 + 咳嗽不停呼吸。
//
// ⚠️ **2026-10-16 S14-3 实现轮 —— 自造 Fake 全数删除**。补评(评审原件
//    `production/qa/evidence/review-audio-epic-fake-green-2026-10-16.md` §2.3)判定
//    12 测中 🔴 假 9:被测对象(`EndLoopWatchdog` / `HandoverMux` / 句柄管理器)**不存在**,
//    测试以自造 `FakeLoopWatchdog` / `FakeHandoverFsm` / `FakeHandleManager` 顶替。
//    S14-3 落地 `HandoverMux`(交接 FSM)+ `LoopSelfTerminationDriver`(自评执行器);
//    兜底谓词与句柄生命周期**已由 S14-2 交付**(`EndLoopWatchdog` / `CueRenderSink`),
//    本文件改指那两个生产类,不重复造。
//
// ⚠️ **判据边界口径(与旧 Fake 的实质差异)**:旧 `FakeLoopWatchdog` 用
//    `stagnantFor >= _maxTicks`;生产 `EndLoopWatchdog` 用**严格 `>`**
//    (「等值不判,边界归下一 tick」)。本文件按生产口径断言 —— 这正是「换真」的意义:
//    旧 Fake 编码了一个**不同的**契约。

using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using DaYiJingCheng.EditorTools.Gates;
using DaYiJingCheng.Gameplay.Presentation.Audio;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Tests.Unit.Audio
{
    [TestFixture]
    internal sealed class LoopLifecycleTest
    {
        private const string Prefix = AssemblyGates.AudioModuleNamespacePrefix;

        /// <summary>交接窗种子(测试用显式值,不读 `AudioTuning.HandoverTicks` ——
        /// 边界值测试须能注入,避免与种子值耦合)。</summary>
        private const long HandoverTicks = 10;

        /// <summary>停滞阈值种子(同上:显式注入)。</summary>
        private const int MaxStagnantTicks = 100;

        // ══════════════ AC-009-1 EndLoop 自评兜底(ADR-001 §一之三 裁决二)══════════════

        /// <summary>AC-009-1:流停更 / 快照停「循环中」时,44 在**有界时间**内自行结束循环。
        /// <para>被测物 = 生产 `LoopSelfTerminationDriver`(执行)+ 生产 `EndLoopWatchdog`(判据)。</para>
        /// <para>⚠️ **断言必须落到 sink 侧**(`target.Stopped`)—— 只断驱动器内部计数器
        /// (`ActiveLoopCount` / `SelfTerminatedCount`)时,「停用实际 `_sink.EndLoop` 调用」
        /// 仍全绿(证伪例 ② 红 0 条实测)。内部计数器是**驱动器的自述**,sink 收到 `StopLoop`
        /// 才是**可观测的副作用**。</para></summary>
        [Test]
        public void test_endLoopWatchdog_staleStream_stopsWithinBound()
        {
            // Arrange:循环 cue,快照停「循环中」,流停更
            var target = new RecordingRenderTarget();
            var sink = new CueRenderSink(target);
            var driver = new LoopSelfTerminationDriver(sink, MaxStagnantTicks);
            AudioCueHandle h = sink.BeginLoop(BreathCue(source: 1));
            driver.BeginLoop(h, cueId: 42);
            driver.UpdateSnapshot(cueId: 42, progress: 100, serverTick: 60);

            // Act:推进 tick 至上限之后
            int stopped = 0;
            for (long tick = 61; tick <= 200 && stopped == 0; tick++)
                stopped = driver.Tick(tick);

            // Assert:循环已停 —— **观测 sink 实际收到的 StopLoop**(不是驱动器的自述)
            Assert.That(stopped, Is.EqualTo(1), "流停更后循环必须在有界时间内自行结束");
            Assert.That(driver.ActiveLoopCount, Is.EqualTo(0), "停后不再跟踪");
            Assert.That(driver.SelfTerminatedCount, Is.EqualTo(1));
            Assert.That(target.Stopped, Has.Count.EqualTo(1),
                "44 必须对**自有句柄**实际执行 EndLoop(ADR-028 ⑤)—— 只看内部计数器会漏掉「不执行」");
            Assert.That(target.Stopped[0], Is.EqualTo(h.Id), "停的是那个句柄");
        }

        /// <summary>AC-009-1:流恢复更新后不得重复停(幂等)。
        /// <para>⚠️ 生产口径 = **严格 `>`**:停滞 `== maxStagnantTicks` **不判停**;
        /// 恢复快照后停滞归零,不误杀。</para></summary>
        [Test]
        public void test_endLoopWatchdog_resumeNoDoubleStop()
        {
            // Arrange
            var target = new RecordingRenderTarget();
            var sink = new CueRenderSink(target);
            var driver = new LoopSelfTerminationDriver(sink, MaxStagnantTicks);
            AudioCueHandle h = sink.BeginLoop(BreathCue(source: 1));
            driver.BeginLoop(h, cueId: 42);
            driver.UpdateSnapshot(cueId: 42, progress: 100, serverTick: 60);

            // Act:推进到阈值边界(停滞 == 阈值 ⇒ 生产口径不判停)
            for (long tick = 61; tick <= 60 + MaxStagnantTicks; tick++) driver.Tick(tick);
            Assert.That(driver.ActiveLoopCount, Is.EqualTo(1),
                "停滞 == 阈值不判停(严格 > 口径;边界归下一 tick)");
            Assert.That(target.Stopped, Is.Empty, "边界未越 ⇒ sink 未收到 StopLoop");

            // 再推 1 tick ⇒ 越过阈值,停
            int stopped = driver.Tick(60 + MaxStagnantTicks + 1);
            Assert.That(stopped, Is.EqualTo(1), "越过阈值 ⇒ 停");
            int stoppedAtSink = target.Stopped.Count;

            // 流恢复更新后继续推进 ⇒ 不重复停
            driver.UpdateSnapshot(cueId: 42, progress: 200, serverTick: 400);
            for (long tick = 402; tick <= 600; tick++) driver.Tick(tick);

            // Assert:不重复停(幂等)—— **观测 sink 侧**
            Assert.That(driver.SelfTerminatedCount, Is.EqualTo(stoppedAtSink),
                "流恢复后不得重复停(幂等)");
            Assert.That(target.Stopped, Has.Count.EqualTo(stoppedAtSink),
                "sink 侧亦不得重复收到 StopLoop");
        }

        /// <summary>AC-009-1:有界常量取 0 ⇒ **当轮必停**(下界自证)。
        /// <para>⚠️ 生产口径 = 严格 `>`:阈值 0 时,停滞 1 tick > 0 ⇒ 停。</para></summary>
        [Test]
        public void test_endLoopWatchdog_zeroBoundStopsImmediately()
        {
            // Arrange:maxTicks = 0(下界)
            var target = new RecordingRenderTarget();
            var sink = new CueRenderSink(target);
            var driver = new LoopSelfTerminationDriver(sink, maxStagnantTicks: 0);
            AudioCueHandle h = sink.BeginLoop(BreathCue(source: 1));
            driver.BeginLoop(h, cueId: 42);
            driver.UpdateSnapshot(cueId: 42, progress: 100, serverTick: 60);

            // Act:推进 1 tick(停滞 = 1 > 0)
            int stopped = driver.Tick(61);

            // Assert:当轮必停 —— **观测 sink 侧**
            Assert.That(stopped, Is.EqualTo(1), "有界常量 = 0 时当轮必停(下界自证)");
            Assert.That(driver.ActiveLoopCount, Is.EqualTo(0));
            Assert.That(target.Stopped, Has.Count.EqualTo(1),
                "44 必须实际执行 EndLoop(ADR-028 ⑤)");
            Assert.That(target.Stopped[0], Is.EqualTo(h.Id));
        }

        /// <summary>AC-009-1:从未收到快照 ⇒ 不判停(与「无 `Progress` 快照 ⇒ 不判」同口径)。
        /// <para>补评前无此测 —— 新增,守「快照是自评的**必要**输入」。</para></summary>
        [Test]
        public void test_endLoopWatchdog_noSnapshotEver_notTerminated()
        {
            // Arrange:登记句柄但从不推快照
            var sink = new CueRenderSink(new RecordingRenderTarget());
            var driver = new LoopSelfTerminationDriver(sink, MaxStagnantTicks);
            AudioCueHandle h = sink.BeginLoop(BreathCue(source: 1));
            driver.BeginLoop(h, cueId: 42);

            // Act:推进远超阈值
            for (long tick = 1; tick <= 10_000; tick++) driver.Tick(tick);

            // Assert:不判停(无从判)
            Assert.That(driver.ActiveLoopCount, Is.EqualTo(1),
                "从未收到快照 ⇒ 无从判停(快照是自评的必要输入)");
        }

        /// <summary>AC-009-1:上游正常 `EndLoop` ⇒ 退出跟踪,自评不重复停。</summary>
        [Test]
        public void test_endLoopWatchdog_normalEndLoop_noSelfTermination()
        {
            // Arrange
            var sink = new CueRenderSink(new RecordingRenderTarget());
            var driver = new LoopSelfTerminationDriver(sink, MaxStagnantTicks);
            AudioCueHandle h = sink.BeginLoop(BreathCue(source: 1));
            driver.BeginLoop(h, cueId: 42);
            driver.UpdateSnapshot(cueId: 42, progress: 100, serverTick: 60);

            // Act:上游正常收尾
            driver.EndLoop(h);
            for (long tick = 61; tick <= 400; tick++) driver.Tick(tick);

            // Assert:自评未介入
            Assert.That(driver.SelfTerminatedCount, Is.EqualTo(0),
                "上游正常 EndLoop ⇒ 自评兜底不应介入(兜底只处理『收尾信号没来』)");
        }

        /// <summary>AC-009-1:时钟源 = sim tick(禁墙钟)。
        /// 用 Cecil 扫描器验证 44 生产类型方法体 IL 不含 DateTime/Time 引用
        /// (与 Story 007 D7 时钟面同纪律,复用 AssemblyGates.CheckAudioClockTokens)。</summary>
        [Test]
        public void test_endLoopWatchdog_noWallClockRefs()
        {
            // Arrange:编译成功前提
            Assert.That(EditorUtility.scriptCompilationFailed, Is.False,
                "跑 Cecil 扫描前提:编译成功(scriptCompilationFailed ⇒ 读上一版 DLL = 假绿)");

            // Act:用 Cecil 扫描器扫 44 生产类型方法体 IL
            var dll = AssemblyGates.ScriptAssemblyPath(AssemblyGates.PresentationAssemblyName);
            var errs = AssemblyGates.CheckAudioClockTokens(dll, Prefix);

            // Assert
            Assert.That(errs, Is.Empty,
                "EndLoop 兜底时钟源 = sim tick,禁墙钟(DateTime/Time)(ADR-001 裁决二 + D7 时钟面):\n" +
                string.Join("\n", errs));
        }

        /// <summary>AC-009-1 契约形状:EndLoop 存在,参数为本地句柄 AudioCueHandle(非网络类型)。
        /// ⚠️ 本测试只验证契约形状(EndLoop 不住网络序列化路径的**契约面**);
        ///     「AudioCueDto 不进任何网络通道」的裁决(ADR-001 §一之二)由 45 网络层实现期验证。</summary>
        [Test]
        public void test_endLoop_contractShape_localHandleOnly()
        {
            // Arrange:验证 IAudioCueSink 契约面
            var methods = typeof(IAudioCueSink).GetMethods();
            Assert.That(methods, Has.Length.EqualTo(4),
                "IAudioCueSink 契约漂移监控:Emit/BeginLoop/EndLoop/SetTier 四方法");

            // Assert:EndLoop 存在(契约要求)
            var endLoop = Array.Find(methods, m => m.Name == "EndLoop");
            Assert.That(endLoop, Is.Not.Null, "EndLoop 必须存在(ADR-018 §二)");

            // Assert:EndLoop 参数为 AudioCueHandle(非网络类型)
            var param = endLoop.GetParameters()[0];
            Assert.That(param.ParameterType, Is.EqualTo(typeof(AudioCueHandle)),
                "EndLoop 参数 = AudioCueHandle(本地句柄,非网络类型)");
        }

        // ══════════════ AC-009-2 贴耳交接契约 ══════════════

        /// <summary>AC-009-2:进入听诊 ⇒ 世界层在交接窗内压出、听诊层同窗淡入。
        /// 两层并存 ≤ 交接窗,禁同刻双呼吸稳态。
        /// <para>被测物 = 生产 `HandoverMux`(补评前是自造 `FakeHandoverFsm`)。</para></summary>
        [Test]
        public void test_handover_enterStethoscope_worldFadesOut()
        {
            // Arrange:世界层循环中
            var target = new RecordingRenderTarget();
            var sink = new CueRenderSink(target);
            var mux = new HandoverMux(sink, HandoverTicks);
            mux.EnterWorldLayer(BreathCue(source: 1), currentTick: 0);
            Assert.That(mux.Phase, Is.EqualTo(HandoverPhase.WorldOnly), "前置:世界层活跃");
            Assert.That(mux.WorldVolume, Is.EqualTo(1.0).Within(1e-9));
            int worldHandleId = target.Started[0];

            // Act:触发进入听诊,推进满窗
            mux.EnterStethoscope(BreathCue(source: 2), currentTick: 0);
            for (long tick = 1; tick <= HandoverTicks; tick++) mux.Tick(tick);

            // Assert:窗满 ⇒ 世界层压出、听诊层淡入
            Assert.That(mux.Phase, Is.EqualTo(HandoverPhase.StethoscopeOnly));
            Assert.That(mux.WorldVolume, Is.EqualTo(0.0).Within(1e-9),
                "窗满后世界层音量归零(禁同刻双呼吸稳态)");
            Assert.That(mux.StethoscopeVolume, Is.EqualTo(1.0).Within(1e-9), "听诊层淡入");
            Assert.That(mux.WorldHandleReleased, Is.True,
                "AC-009-2 逐字:世界层停止由 44 对自有句柄执行(ADR-028 ⑤)");
            // ⚠️ **观测 sink 侧** —— `WorldHandleReleased` 是 mux 的**自述**,
            //    停用实际 `_sink.EndLoop` 调用时它仍为 true(证伪例 ④ 只靠标志即转红,
            //    但标志与「真调 sink」是两件事)。sink 收到 StopLoop 才是副作用。
            Assert.That(target.Stopped, Does.Contain(worldHandleId),
                "世界层句柄必须**真的**被 44 停止(不是只翻了个标志)");
        }

        /// <summary>AC-009-2:窗**中途**两层并存但**不同刻稳态** —— 交叉淡化在开区间内。</summary>
        [Test]
        public void test_handover_midWindow_crossfadeStrictlyBetween()
        {
            // Arrange
            var sink = new CueRenderSink(new RecordingRenderTarget());
            var mux = new HandoverMux(sink, HandoverTicks);
            mux.EnterWorldLayer(BreathCue(source: 1), currentTick: 0);
            mux.EnterStethoscope(BreathCue(source: 2), currentTick: 0);

            // Act:推进到窗中点
            mux.Tick(HandoverTicks / 2);

            // Assert:两层都在 (0,1) 开区间内(交叉中,非双稳态)
            Assert.That(mux.WorldVolume, Is.GreaterThan(0.0).And.LessThan(1.0));
            Assert.That(mux.StethoscopeVolume, Is.GreaterThan(0.0).And.LessThan(1.0));
            Assert.That(mux.WorldVolume + mux.StethoscopeVolume, Is.EqualTo(1.0).Within(1e-9),
                "交叉淡化:两层音量之和恒为 1(等功率线性交叉)");
        }

        /// <summary>AC-009-2:退出听诊 ⇒ 世界层淡入、听诊层压出(镜像)。</summary>
        [Test]
        public void test_handover_exitStethoscope_worldFadesIn()
        {
            // Arrange:先完成进入
            var target = new RecordingRenderTarget();
            var sink = new CueRenderSink(target);
            var mux = new HandoverMux(sink, HandoverTicks);
            mux.EnterWorldLayer(BreathCue(source: 1), currentTick: 0);
            mux.EnterStethoscope(BreathCue(source: 2), currentTick: 0);
            for (long tick = 1; tick <= HandoverTicks; tick++) mux.Tick(tick);
            Assert.That(mux.Phase, Is.EqualTo(HandoverPhase.StethoscopeOnly), "前置:听诊层活跃");
            int stethoscopeHandleId = target.Started[1];   // [0] = 世界层,[1] = 听诊层

            // Act:退出听诊
            mux.ExitStethoscope(currentTick: HandoverTicks);
            for (long tick = HandoverTicks + 1; tick <= HandoverTicks * 2; tick++) mux.Tick(tick);

            // Assert:镜像
            Assert.That(mux.Phase, Is.EqualTo(HandoverPhase.WorldOnly));
            Assert.That(mux.WorldVolume, Is.EqualTo(1.0).Within(1e-9), "世界层淡入");
            Assert.That(mux.StethoscopeVolume, Is.EqualTo(0.0).Within(1e-9), "听诊层压出");
            Assert.That(mux.StethoscopeHandleReleased, Is.True, "听诊层句柄由 44 自行释放");
            // ⚠️ **观测 sink 侧**(同 `enterStethoscope_worldFadesOut`):
            //    `StethoscopeHandleReleased` 是自述;`target.Stopped` 才是副作用。
            Assert.That(target.Stopped, Does.Contain(stethoscopeHandleId),
                "听诊层句柄必须**真的**被 44 停止(不是只翻了个标志)");
        }

        /// <summary>AC-009-2:窗内重复触发进入(幂等)。</summary>
        [Test]
        public void test_handover_idempotentReentry()
        {
            // Arrange
            var sink = new CueRenderSink(new RecordingRenderTarget());
            var mux = new HandoverMux(sink, HandoverTicks);
            mux.EnterWorldLayer(BreathCue(source: 1), currentTick: 0);

            // Act:连续两次进入
            mux.EnterStethoscope(BreathCue(source: 2), currentTick: 0);
            mux.EnterStethoscope(BreathCue(source: 3), currentTick: 3);   // 幂等
            for (long tick = 1; tick <= HandoverTicks; tick++) mux.Tick(tick);

            // Assert:只交接一次
            Assert.That(mux.HandoverCount, Is.EqualTo(1), "窗内重复触发进入必须幂等");
            Assert.That(mux.Phase, Is.EqualTo(HandoverPhase.StethoscopeOnly));
        }

        /// <summary>AC-009-2:听诊中世界 cue 新到 ⇒ 不重启世界层(听诊优先)。</summary>
        [Test]
        public void test_handover_worldCueArrives_noRestart()
        {
            // Arrange:听诊层活跃
            var sink = new CueRenderSink(new RecordingRenderTarget());
            var mux = new HandoverMux(sink, HandoverTicks);
            mux.EnterWorldLayer(BreathCue(source: 1), currentTick: 0);
            mux.EnterStethoscope(BreathCue(source: 2), currentTick: 0);
            for (long tick = 1; tick <= HandoverTicks; tick++) mux.Tick(tick);
            Assert.That(mux.Phase, Is.EqualTo(HandoverPhase.StethoscopeOnly), "前置:听诊层活跃");

            // Act:世界 cue 新到
            mux.EnterWorldLayer(BreathCue(source: 9), currentTick: HandoverTicks + 1);

            // Assert:不重启世界层(听诊优先)
            Assert.That(mux.Phase, Is.EqualTo(HandoverPhase.StethoscopeOnly),
                "听诊中世界 cue 新到 ⇒ 不重启世界层(听诊优先)");
            Assert.That(mux.WorldVolume, Is.EqualTo(0.0).Within(1e-9), "世界层仍压出");
        }

        /// <summary>AC-009-2:禁硬切(handoverTicks ≤ 0 ⇒ 构造期拒绝)。</summary>
        [Test]
        public void test_handover_hardCut_rejected()
        {
            // Arrange + Act + Assert:硬切构造必须拒绝
            var sink = new CueRenderSink(new RecordingRenderTarget());
            Assert.Throws<ArgumentException>(() => new HandoverMux(sink, handoverTicks: 0),
                "硬切(handoverTicks=0)必须拒绝(ramp 下界同 50ms 族)");
            Assert.Throws<ArgumentException>(() => new HandoverMux(sink, handoverTicks: -5),
                "负窗必须拒绝(旧 Fake 只测 ==0,本测补齐 <0 —— 见 story Known Risks)");
        }

        /// <summary>AC-009-2:null sink ⇒ 构造期抛异常(注入缝守卫)。</summary>
        [Test]
        public void test_handover_nullSink_throws()
        {
            Assert.Throws<ArgumentNullException>(() => new HandoverMux(null, HandoverTicks));
        }

        // ══════════════ AC-009-3 重复/过期句柄 ══════════════

        /// <summary>AC-009-3:同源重复 BeginLoop ⇒ 以最后一次为准释放旧句柄(防双呼吸层)。
        /// <para>被测物 = 生产 `CueRenderSink`(S14-2 交付;补评前是自造 `FakeHandleManager`)。</para></summary>
        [Test]
        public void test_duplicateBeginLoop_lastWins()
        {
            // Arrange
            var target = new RecordingRenderTarget();
            var sink = new CueRenderSink(target);

            // Act:同源两次 BeginLoop
            AudioCueHandle first = sink.BeginLoop(BreathCue(source: 7));
            AudioCueHandle second = sink.BeginLoop(BreathCue(source: 7));   // 同源重复

            // Assert:只存活最后一次句柄(旧句柄已释放)
            Assert.That(sink.ActiveLoopCount, Is.EqualTo(1), "同源重复 ⇒ 只存活一个句柄");
            Assert.That(target.Stopped, Has.Count.EqualTo(1), "旧句柄被释放(防双呼吸层)");
            Assert.That(target.Stopped[0], Is.EqualTo(first.Id));
            Assert.That(second.Id, Is.Not.EqualTo(first.Id), "新句柄是新 id");

            // 收尾
            sink.EndLoop(second);
            Assert.That(sink.ActiveLoopCount, Is.EqualTo(0));
        }

        /// <summary>AC-009-3:过期 EndLoop 忽略不产生播放错误。</summary>
        [Test]
        public void test_expiredEndLoop_ignored()
        {
            // Arrange
            var target = new RecordingRenderTarget();
            var sink = new CueRenderSink(target);
            AudioCueHandle h = sink.BeginLoop(BreathCue(source: 7));
            sink.EndLoop(h);   // 正常释放

            // Act + Assert:过期 EndLoop 不抛
            Assert.DoesNotThrow(() => sink.EndLoop(h),
                "过期 EndLoop 必须忽略(不产生播放错误)");
            Assert.DoesNotThrow(() => sink.EndLoop(new AudioCueHandle(9999)),
                "从未存在的句柄同样忽略");
            Assert.That(target.Stopped, Has.Count.EqualTo(1), "过期句柄不产生额外 Stop");
        }

        // ══════════════ AC-009-4 咳嗽不停呼吸(AC-44-08 联合断言)══════════════

        // ⚠️ 主判据归 Story 004 `breath_layers_test.test_coughNeverEndsBreathLoop_zeroEndLoopCalls`
        //    (同名同逻辑,已存在,且调**真生产类** `CoughChannel.Dispatch` —— 评审原件 §2.3 判 🟡)。
        //    本 story AC 明确把「咳嗽不停呼吸」列为验收标准,但为避免重复测试浪费维护成本,
        //    不在此重复实现 —— 联合断言 = Story 004 判据。
        // ⚠️ 本 story 侧**可增补**的结构面:咳嗽通道**结构上无 EndLoop 路径**(见
        //    `CoughChannel.cs` 文件头「结构上无 EndLoop 路径」)—— 已由 Story 004 的
        //    `test_coughNeverEndsBreathLoop_zeroEndLoopCalls` 覆盖,不重复。

        // ══════════════ 测试夹具(负向 / 记录型;住测试命名空间)══════════════

        /// <summary>记录型渲染目标:`CueRenderSink` 的注入 seam(`ICueRenderTarget`)。
        /// <para>⚠️ 这是**记录型假件**,不是被测对象 —— 被测对象是 `CueRenderSink` 的
        /// **契约语义**(句柄唯一性 / 过期忽略),它由本类的调用记录**观测**。</para></summary>
        private sealed class RecordingRenderTarget : ICueRenderTarget
        {
            public readonly List<int> Started = new List<int>();
            public readonly List<int> Stopped = new List<int>();
            public int OneShotCount { get; private set; }

            public void PlayOneShot(in AudioCueDto cue) => OneShotCount++;

            public void StartLoop(in AudioCueDto cue, int handleId) => Started.Add(handleId);

            public void StopLoop(int handleId) => Stopped.Add(handleId);
            public bool SetLoopGain(int handleId, float gain) => false;
        }

        private static AudioCueDto BreathCue(int source, bool looped = true)
            => new AudioCueDto(cue: 42, intensity: 128, cell: default, source: source, looped: looped);
    }
}
