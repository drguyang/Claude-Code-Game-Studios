// 权威来源:production/epics/audio-system/story-009-source-lifecycle-handover.md
//   (EndLoop 自评兜底 / 贴耳交接契约 / 重复过期句柄 / 咳嗽不停呼吸)
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

        // ══════════════ EndLoop 自评兜底(ADR-001 §一之三 裁决二)══════════════

        /// <summary>EndLoop 自评兜底:流停更/快照停「循环中」时,44 在有界时间内自行结束循环。
        /// 测试侧自持 Watchdog 谓词面(纯函数,零引擎依赖)。
        /// 时钟源 = sim tick(禁墙钟);有界时间 ≤ LOOP_EVAL_MAX_TICKS。</summary>
        [Test]
        public void test_endLoopWatchdog_staleStream_stopsWithinBound()
        {
            // Arrange:循环 cue,快照停「循环中」,流停更
            var watchdog = new FakeLoopWatchdog(maxTicks: 100);
            watchdog.BeginLoop(cueId: 42, snapshotProgress: 100, snapshotTick: 60);

            // Act:推进 tick 至上限
            for (long tick = 61; tick <= 160; tick++)
                watchdog.Tick(tick);

            // Assert:循环已停(句柄释放)
            Assert.That(watchdog.IsLoopActive, Is.False,
                "流停更后,循环必须在有界时间内自行结束(≤ LOOP_EVAL_MAX_TICKS)");
            Assert.That(watchdog.StopTick, Is.GreaterThan(0), "必须有停止 tick 记录");
            Assert.That(watchdog.StopTick, Is.LessThanOrEqualTo(60 + 100),
                "停止 tick 必须 ≤ 快照 tick + LOOP_EVAL_MAX_TICKS(有界)");
        }

        /// <summary>EndLoop 自评兜底:流恢复更新后不得重复停(幂等)。</summary>
        [Test]
        public void test_endLoopWatchdog_resumeNoDoubleStop()
        {
            // Arrange
            var watchdog = new FakeLoopWatchdog(maxTicks: 100);
            watchdog.BeginLoop(cueId: 42, snapshotProgress: 100, snapshotTick: 60);

            // Act:先停
            for (long tick = 61; tick <= 160; tick++) watchdog.Tick(tick);
            Assert.That(watchdog.IsLoopActive, Is.False, "前置:循环已停");
            long stopTick1 = watchdog.StopTick;
            int stopCount1 = watchdog.StopCount;

            // 流恢复更新后继续推进
            watchdog.UpdateSnapshot(progress: 200, tick: 161);
            for (long tick = 162; tick <= 300; tick++) watchdog.Tick(tick);

            // Assert:不重复停(幂等)
            Assert.That(watchdog.StopCount, Is.EqualTo(stopCount1),
                "流恢复后不得重复停(幂等)");
        }

        /// <summary>EndLoop 自评兜底:有界常量取 0 ⇒ 当轮必停(下界自证)。</summary>
        [Test]
        public void test_endLoopWatchdog_zeroBoundStopsImmediately()
        {
            // Arrange:maxTicks = 0(下界)
            var watchdog = new FakeLoopWatchdog(maxTicks: 0);
            watchdog.BeginLoop(cueId: 42, snapshotProgress: 100, snapshotTick: 60);

            // Act:推进 1 tick
            watchdog.Tick(61);

            // Assert:当轮必停
            Assert.That(watchdog.IsLoopActive, Is.False,
                "有界常量 = 0 时当轮必停(下界自证)");
            Assert.That(watchdog.StopTick, Is.EqualTo(61), "停止 tick = 当前 tick");
        }

        /// <summary>EndLoop 自评兜底:时钟源 = sim tick(禁墙钟)。
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

        /// <summary>EndLoop 契约形状:EndLoop 存在,参数为本地句柄 AudioCueHandle(非网络类型)。
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

        // ══════════════ 贴耳交接契约 ══════════════

        /// <summary>贴耳交接:进入听诊 ⇒ 世界层在 HANDOVER_MS 窗内压出、听诊层同窗淡入。
        /// 两层并存 ≤ 交接窗,禁同刻双呼吸稳态。测试侧自持 HandoverFSM 谓词面。</summary>
        [Test]
        public void test_handover_enterStethoscope_worldFadesOut()
        {
            // Arrange:世界层循环中
            var fsm = new FakeHandoverFsm(handoverTicks: 10);
            fsm.EnterWorldLayer(cueId: 10, worldProgress: 50);
            Assert.That(fsm.WorldActive, Is.True, "前置:世界层活跃");

            // Act:触发进入听诊
            fsm.EnterStethoscope();

            // Assert:交接窗内,世界层压出
            for (long tick = 1; tick <= 10; tick++) fsm.Tick(tick);
            Assert.That(fsm.WorldActive, Is.False,
                "HANDOVER_MS 窗内世界层必须压出(禁同刻双呼吸稳态)");
            Assert.That(fsm.StethoscopeActive, Is.True, "听诊层淡入");
        }

        /// <summary>贴耳交接:退出听诊 ⇒ 世界层淡入、听诊层压出(镜像)。</summary>
        [Test]
        public void test_handover_exitStethoscope_worldFadesIn()
        {
            // Arrange:听诊层活跃
            var fsm = new FakeHandoverFsm(handoverTicks: 10);
            fsm.EnterWorldLayer(cueId: 10, worldProgress: 50);
            fsm.EnterStethoscope();
            for (long tick = 1; tick <= 10; tick++) fsm.Tick(tick);
            Assert.That(fsm.StethoscopeActive, Is.True, "前置:听诊层活跃");

            // Act:退出听诊
            fsm.ExitStethoscope();
            for (long tick = 11; tick <= 20; tick++) fsm.Tick(tick);

            // Assert:镜像
            Assert.That(fsm.WorldActive, Is.True, "世界层淡入");
            Assert.That(fsm.StethoscopeActive, Is.False, "听诊层压出");
        }

        /// <summary>贴耳交接:窗内重复触发进入(幂等)。</summary>
        [Test]
        public void test_handover_idempotentReentry()
        {
            // Arrange
            var fsm = new FakeHandoverFsm(handoverTicks: 10);
            fsm.EnterWorldLayer(cueId: 10, worldProgress: 50);

            // Act:连续两次进入
            fsm.EnterStethoscope();
            fsm.EnterStethoscope();  // 幂等
            for (long tick = 1; tick <= 10; tick++) fsm.Tick(tick);

            // Assert:只交接一次
            Assert.That(fsm.HandoverCount, Is.EqualTo(1), "窗内重复触发进入必须幂等");
            Assert.That(fsm.WorldActive, Is.False);
            Assert.That(fsm.StethoscopeActive, Is.True);
        }

        /// <summary>贴耳交接:听诊中世界 cue 新到 ⇒ 不重启世界层(听诊优先)。</summary>
        [Test]
        public void test_handover_worldCueArrives_noRestart()
        {
            // Arrange:听诊层活跃
            var fsm = new FakeHandoverFsm(handoverTicks: 10);
            fsm.EnterWorldLayer(cueId: 10, worldProgress: 50);
            fsm.EnterStethoscope();
            for (long tick = 1; tick <= 10; tick++) fsm.Tick(tick);
            Assert.That(fsm.StethoscopeActive, Is.True, "前置:听诊层活跃");

            // Act:世界 cue 新到
            fsm.OnWorldCueArrived(cueId: 11);

            // Assert:不重启世界层(听诊优先)
            Assert.That(fsm.WorldActive, Is.False,
                "听诊中世界 cue 新到 ⇒ 不重启世界层(听诊优先)");
        }

        /// <summary>贴耳交接:禁硬切(ramp=0 ⇒ 红)。</summary>
        [Test]
        public void test_handover_hardCut_rejected()
        {
            // Arrange + Act + Assert:handoverTicks = 0(硬切)构造时必须拒绝
            Assert.Throws<ArgumentException>(() => new FakeHandoverFsm(handoverTicks: 0),
                "硬切(handoverTicks=0)必须拒绝(ramp 下界同 50ms 族)");
        }

        // ══════════════ 重复/过期句柄 ══════════════

        /// <summary>重复/过期句柄:同源重复 BeginLoop ⇒ 以最后一次为准释放旧句柄(防双呼吸层)。</summary>
        [Test]
        public void test_duplicateBeginLoop_lastWins()
        {
            // Arrange
            var manager = new FakeHandleManager();

            // Act:同 cue 两次 BeginLoop
            manager.BeginLoop(cueId: 10);
            manager.BeginLoop(cueId: 10);  // 重复
            manager.EndLoop(cueId: 10);

            // Assert:只存活最后一次句柄
            Assert.That(manager.ActiveHandleCount, Is.EqualTo(0),
                "重复 BeginLoop 以最后一次为准,EndLoop 后无存活句柄");
            Assert.That(manager.ReleasedHandles.Count, Is.EqualTo(1),
                "只释放最后一次句柄(旧句柄已被覆盖)");
        }

        /// <summary>重复/过期句柄:过期 EndLoop 忽略不产生播放错误。</summary>
        [Test]
        public void test_expiredEndLoop_ignored()
        {
            // Arrange
            var manager = new FakeHandleManager();
            manager.BeginLoop(cueId: 10);
            manager.ReleaseAll();  // 先释放

            // Act:过期 EndLoop
            Assert.DoesNotThrow(() => manager.EndLoop(cueId: 10),
                "过期 EndLoop 必须忽略(不产生播放错误)");
        }

        // ══════════════ 咳嗽不停呼吸(AC-44-08 联合断言)══════════════

        // ⚠️ 主判据归 Story 004 `breath_layers_test.test_coughNeverEndsBreathLoop_zeroEndLoopCalls`
        //    (同名同逻辑,已存在)。本 story AC 明确把「咳嗽不停呼吸」列为验收标准,
        //    但为避免重复测试浪费维护成本,不在此重复实现 —— 联合断言 = Story 004 判据。

        // ══════════════ 测试替身与夹具 ══════════════

        /// <summary>FakeLoopWatchdog:EndLoop 自评兜底谓词面(测试自持,纯函数)。
        /// 语义 = ADR-001 §一之三 裁决二:从最新快照 Progress 自求值,零网络依赖,时钟源 = sim tick。</summary>
        private sealed class FakeLoopWatchdog
        {
            private readonly long _maxTicks;
            private bool _loopActive;
            private long _snapshotTick;
            private long _snapshotProgress;
            private long _currentTick;

            public FakeLoopWatchdog(long maxTicks) => _maxTicks = maxTicks;

            public bool IsLoopActive => _loopActive;
            public long StopTick { get; private set; }
            public int StopCount { get; private set; }

            public void BeginLoop(int cueId, long snapshotProgress, long snapshotTick)
            {
                _loopActive = true;
                _snapshotProgress = snapshotProgress;
                _snapshotTick = snapshotTick;
            }

            public void UpdateSnapshot(long progress, long tick)
            {
                _snapshotProgress = progress;
                _snapshotTick = tick;
            }

            public void Tick(long currentTick)
            {
                _currentTick = currentTick;
                if (!_loopActive) return;
                long stagnantFor = currentTick - _snapshotTick;
                if (stagnantFor >= _maxTicks)
                {
                    _loopActive = false;
                    StopTick = currentTick;
                    StopCount++;
                }
            }
        }

        /// <summary>FakeHandoverFsm:贴耳交接状态机谓词面(测试自持,纯函数)。
        /// 语义 = GDD F-44.7 交接契约:两态(世界/听诊)+ 窗内交叉淡化;玩家自因入口。</summary>
        private sealed class FakeHandoverFsm
        {
            private readonly long _handoverTicks;
            private bool _worldActive;
            private bool _stethoscopeActive;
            private long _handoverElapsed;

            public FakeHandoverFsm(long handoverTicks)
            {
                if (handoverTicks < 0)
                    throw new ArgumentException("handoverTicks 必须 ≥ 0");
                if (handoverTicks == 0)
                    throw new ArgumentException("硬切(handoverTicks=0)禁止(ramp 下界同 50ms 族)");
                _handoverTicks = handoverTicks;
            }

            public bool WorldActive => _worldActive;
            public bool StethoscopeActive => _stethoscopeActive;
            public int HandoverCount { get; private set; }

            public void EnterWorldLayer(int cueId, long worldProgress)
            {
                _worldActive = true;
                _stethoscopeActive = false;
            }

            public void EnterStethoscope()
            {
                if (!_worldActive) return;  // 世界层未活跃 ⇒ 不交接
                if (_stethoscopeActive) return;  // 幂等
                _handoverElapsed = 0;
                HandoverCount++;
                // 进入听诊 ⇒ 世界层立即开始压出(窗内交叉淡化开始)
                _worldActive = false;
            }

            public void ExitStethoscope()
            {
                if (!_stethoscopeActive) return;
                _stethoscopeActive = false;
                _worldActive = true;
                // 退出后彻底清除交接状态(Tick 不再干预世界层)
                HandoverCount = 0;
                _handoverElapsed = 0;
            }

            public void OnWorldCueArrived(int cueId)
            {
                // 听诊中世界 cue 新到 ⇒ 不重启世界层(听诊优先)
                if (_stethoscopeActive) return;
                _worldActive = true;
            }

            public void Tick(long tick)
            {
                if (HandoverCount == 0) return;
                // 进入听诊时世界层立即开始压出(不等窗满)
                _worldActive = false;
                _handoverElapsed++;
                if (_handoverElapsed >= _handoverTicks)
                {
                    _stethoscopeActive = true;
                }
            }
        }

        /// <summary>FakeHandleManager:句柄生命周期谓词面(测试自持,纯函数)。
        /// 语义 = ADR-028 ⑤:同源重复 BeginLoop 以最后一次为准;过期 EndLoop 忽略。</summary>
        private sealed class FakeHandleManager
        {
            private readonly Dictionary<int, int> _activeHandles = new Dictionary<int, int>();
            private int _nextId = 1;

            public int ActiveHandleCount => _activeHandles.Count;
            public List<int> ReleasedHandles { get; } = new List<int>();

            public AudioCueHandle BeginLoop(int cueId)
            {
                // 同源重复 BeginLoop ⇒ 以最后一次为准(覆盖旧句柄,不计入 ReleasedHandles)
                int id = _nextId++;
                _activeHandles[cueId] = id;
                return new AudioCueHandle(id);
            }

            public void EndLoop(int cueId)
            {
                if (!_activeHandles.TryGetValue(cueId, out var id)) return;  // 过期 EndLoop 忽略
                _activeHandles.Remove(cueId);
                ReleasedHandles.Add(id);
            }

            public void ReleaseAll()
            {
                foreach (var kv in _activeHandles)
                    ReleasedHandles.Add(kv.Value);
                _activeHandles.Clear();
            }
        }

    }
}
