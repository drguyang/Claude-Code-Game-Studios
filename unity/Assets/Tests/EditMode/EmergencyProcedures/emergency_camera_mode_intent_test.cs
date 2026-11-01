// story-010 —— 档位意图的**意图层**(L1)夹具:`EmergencyChannelDriver` 对 `ICameraRig` 的请求序列。
//
// ─────────────────────────────────────────────────────────────────────────────
// 读哪些仓库文件 / 写哪里
// ─────────────────────────────────────────────────────────────────────────────
// 读:`unity/Assets/**` 全部生产 `.cs`(**仅** `test_s10_07_...` 一条,经
//   `CameraSetModeRequesterGate.RunAll(".")` —— 必须读真树,扫自己造的目录证不了生产码干净)。
// 写:**零写盘**。其余各测全在内存里跑(合成流 + 间谍相机),不造临时目录。
//
// ── 本文件替代了什么(以及为什么必须替代) ──────────────────────────────────
// `AC-10-11`「跳过路径执行完毕必发 Explore / 相机不卡在近景」此前由
// `modal_phase_test.cs` 的 4 条 `Assert.AreEqual(N, result.<计数>)` 顶替 —— 那断的是
// `ModalPhaseEvaluator` **自己返回的整数**,工厂方法里写死、断言必然成立,
// **改坏生产行为也不会红**(假绿形态 F1:自造断言顶替 AC 语义层)。
// 相关计数载体已于 2026-11-02 退役(`ModalPhaseEvaluator` 侧的注记点名本 story 的 R-10.5)。
//
// ── 本文件测什么 / 不测什么 ─────────────────────────────────────────────────
// ✅ 测:**请求发没发**、**发了几次**、**requesterId 是不是 10**、**哪条出口发的**。
// ❌ 不测:**相机认不认**(优先级裁决 / 转场是否完成)—— 那是 L2 真机层
//    (`Tests/PlayMode/EmergencyProcedures/emergency_camera_mode_intent_wiring_test.cs`)。
//    ⚠️ 间谍的 `Mode` **刻意恒为 Explore 且不随 `SetMode` 变化** —— 让「拿间谍的 Mode 当证据」
//       这件事在本文件里**写不出来**,而不是靠纪律守。
//
// 权威来源:
//   `design/gdd/emergency-procedures.md` 规则八 `:352-360` · 规则六之甲 `:724` · `AC-10-11` `:1044`
//   `docs/architecture/adr-020-player-controller-and-camera.md` §五 / Amendment C ②
//   `production/epics/emergency-procedures/story-010-camera-mode-intent-wiring.md` R-10.1…R-10.7

using System;
using System.Collections.Generic;
using System.Linq;
using DaYiJingCheng.EditorTools.Gates;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Input;
using DaYiJingCheng.Gameplay.Presentation.Camera;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.EmergencyProcedures;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.EmergencyProcedures
{
    /// <summary>
    /// story-010 L1:`EmergencyChannelDriver` 的档位意图请求序列(`Treatment` / `Explore`,
    /// `requesterId` 恒 = 10)。
    /// </summary>
    [TestFixture]
    internal sealed class EmergencyCameraModeIntentTest
    {
        private const int Actor = 7;
        private const int ActionOrdinal = 0;

        private InMemoryBlobPool _pool;
        private PayloadEncoder _encoder;
        private PayloadDecoder _decoder;
        private EventStream _stream;
        private SpyCameraRig _rig;
        private EmergencyChannelDriver _driver;
        private long _tick;

        [SetUp]
        public void SetUp()
        {
            _pool = new InMemoryBlobPool();
            _encoder = new PayloadEncoder(_pool);
            _decoder = new PayloadDecoder(_pool);
            _stream = new EventStream(new SeqIdAuthority(), new PresentPresence());
            _tick = 100;

            var processor = new HostEmergencyProcessor(_stream, new SeqIdAuthority(), _encoder);
            var manual = new EmergencyManualDriver(processor, new PresentPresence());
            // ⚠️ `inputActions: null` = 逻辑层模式(Attach 非法、FeedForTest 可用)——
            //    本文件不需要真输入设备,只驱动相位。
            var channel = new EmergencyDirectReadChannel(null, 65536, 0, 65536);

            _rig = new SpyCameraRig();
            _driver = new EmergencyChannelDriver(channel, manual, () => _tick);
            _driver.BindCameraRig(() => _rig);
        }

        // ══════════════ S10-01 动作开始 ⇒ Treatment ══════════════

        [Test]
        public void test_s10_01_arm_requestsTreatmentWithRequesterTen()
        {
            _driver.Arm(ActionOrdinal, new PatientId(1));

            Assert.That(_rig.Describe(), Is.EqualTo("Treatment:10"),
                "动作开始须向相机发恰一条 (Treatment, requesterId: 10)(规则八 / R-10.3)");
            Assert.AreEqual(10, EmergencyChannelDriver.RequesterId,
                "10 = 急救动作的系统号(systems-index.md);改它等于改身份,须同步 ADR-020 Amd C ②");
        }

        [Test]
        public void test_s10_01_armOnArmed_throws_andRequestsNothingExtra()
        {
            _driver.Arm(ActionOrdinal, new PatientId(1));

            Assert.Throws<InvalidOperationException>(
                () => _driver.Arm(ActionOrdinal, new PatientId(1)),
                "非 Idle 态二次 Arm 由通道抛(story-007 既有行为)");

            Assert.That(_rig.Describe(), Is.EqualTo("Treatment:10"),
                "抛出的那一轮**不得**留下第二个 Treatment 请求 —— 否则相机进近景而动作不存在(但 R-10.1)");
        }

        // ══════════════ S10-02 结束两出口 ⇒ Explore ══════════════

        [Test]
        public void test_s10_02_endAction_withSamples_requestsExplore()
        {
            _driver.Arm(ActionOrdinal, new PatientId(1));
            _tick++;
            _driver.FeedForTest(axis: 1f, newlyPressed: true);

            _driver.EndAction(Actor);

            Assert.That(_rig.Describe(), Is.EqualTo("Treatment:10,Explore:10"),
                "正常结束须发 Explore(规则八「动作结束」)");
        }

        /// <summary>
        /// ⚠️ **本 fixture 的核心判别力用例**(证伪变异 M4 专打它):
        /// 零样本签约是**合法**的一次动作起止 —— 流上零事件,但相机**必须**退出近景。
        /// 把 `Explore` 写成以「有事件」为条件,本条即红。
        /// </summary>
        [Test]
        public void test_s10_02_endAction_zeroSamples_stillRequestsExplore()
        {
            _driver.Arm(ActionOrdinal, new PatientId(1));

            bool applied = _driver.EndAction(Actor);   // 零样本 ⇒ 通道返回 null ⇒ 零事件

            Assert.IsFalse(applied, "零样本签约:一次按住周期零动作 ⇒ 零上行(F-10.5)");
            Assert.IsFalse(_stream.Events.Any(e => e.Kind == EventKind.EmergencyTreatmentApplied),
                "零样本 ⇒ 流上零处置事件(前置断言:本用例确实没走成功路径)");
            Assert.That(_rig.Describe(), Is.EqualTo("Treatment:10,Explore:10"),
                "**零事件 ≠ 零请求** —— 动作结束了,相机就必须退出近景(规则八 / R-10.1)");
        }

        // ══════════════ S10-03 中止两路 ⇒ Explore ══════════════

        [Test]
        public void test_s10_03_abortAction_requestsExploreOnce()
        {
            _driver.Arm(ActionOrdinal, new PatientId(1));

            bool wasArmed = _driver.AbortAction();

            Assert.IsTrue(wasArmed, "中止前确有动作在途");
            Assert.That(_rig.Describe(), Is.EqualTo("Treatment:10,Explore:10"),
                "中止亦发 Explore(规则六之甲 :724)");
            Assert.IsFalse(_stream.Events.Any(e => e.Kind == EventKind.EmergencyTreatmentApplied),
                "中止 ≠ 跳过:零处置事件(AC-10-18)");
        }

        [Test]
        public void test_s10_03_tickWatchdogAbort_requestsExploreOnce()
        {
            _driver.Arm(ActionOrdinal, new PatientId(1));

            // 看门狗:距最近采样 ≥ AbortIdleTicks ⇒ 转调 AbortAction
            _driver.OnTickEdge(_tick + ModalPhaseEvaluator.AbortIdleTicks);

            Assert.That(_rig.Describe(), Is.EqualTo("Treatment:10,Explore:10"),
                "看门狗中止须走**同一条** Explore 出口(不重复实现 / 不遗漏)");
        }

        // ══════════════ S10-04 Idle 态误触 ⇒ 零请求 ══════════════

        [Test]
        public void test_s10_04_endActionWhenIdle_requestsNothing()
        {
            bool applied = _driver.EndAction(Actor);

            Assert.IsFalse(applied, "Idle 态 EndAction = 零样本 null");
            Assert.That(_rig.Describe(), Is.Empty,
                "从未 Arm ⇒ 不得凭空发 Explore(相机本来就该在 Explore,发不发都不该有请求)");
        }

        [Test]
        public void test_s10_04_abortWhenIdle_requestsNothing()
        {
            bool wasArmed = _driver.AbortAction();

            Assert.IsFalse(wasArmed, "Idle 态 AbortAction = 幂等 no-op");
            Assert.That(_rig.Describe(), Is.Empty, "幂等 no-op 不得留下档位请求");
        }

        // ══════════════ S10-10 跳过入口(甲案)══════════════

        [Test]
        public void test_s10_10_skipAction_requestsExploreWithRequesterTen()
        {
            _driver.SkipAction(new PatientId(1), Actor);

            Assert.That(_rig.Describe(), Is.EqualTo("Explore:10"),
                "跳过入口须向相机发 (Explore, requesterId: 10)(规则八「含跳过路径」/ R-10.7 甲案)");
        }

        [Test]
        public void test_s10_10_skipAction_stillWritesSkipSettlement()
        {
            bool applied = _driver.SkipAction(new PatientId(1), Actor);

            Assert.IsTrue(applied, "在场病人 ⇒ 跳过须走完 Process 链(不能因加了相机请求而短路)");
            var evs = _stream.Events
                .Where(e => e.Kind == EventKind.EmergencyTreatmentApplied)
                .Select(e => _decoder.Decode<EmergencyTreatmentAppliedPayload>(
                    EventKind.EmergencyTreatmentApplied, e.Payload))
                .ToArray();
            Assert.AreEqual(1, evs.Length, "跳过硬结算:恰一条处置事件(story-009 AC-9-05)");
            Assert.AreEqual(1, evs[0].Method, "method = 1 = Skip(不被相机接线改坏)");
        }

        // ══════════════ R-10.2 后置绑定与判空 ══════════════

        [Test]
        public void test_r10_2_hasCameraRig_reflectsBinding()
        {
            var channel = new EmergencyDirectReadChannel(null, 65536, 0, 65536);
            var manual = new EmergencyManualDriver(
                new HostEmergencyProcessor(_stream, new SeqIdAuthority(), _encoder),
                new PresentPresence());
            var unbound = new EmergencyChannelDriver(channel, manual, () => _tick);

            Assert.IsFalse(unbound.HasCameraRig, "未绑定前 HasCameraRig 须为 false(生产漏绑的观测面)");

            unbound.BindCameraRig(() => _rig);
            Assert.IsTrue(unbound.HasCameraRig, "绑定后须为 true");
        }

        [Test]
        public void test_r10_2_unboundRig_zeroRequests_noThrow()
        {
            var channel = new EmergencyDirectReadChannel(null, 65536, 0, 65536);
            var manual = new EmergencyManualDriver(
                new HostEmergencyProcessor(_stream, new SeqIdAuthority(), _encoder),
                new PresentPresence());
            var unbound = new EmergencyChannelDriver(channel, manual, () => _tick);

            Assert.DoesNotThrow(() => unbound.Arm(ActionOrdinal, new PatientId(1)),
                "未绑相机时 Arm 不得抛 —— 逻辑层 / EditMode 是合法形态");
            Assert.DoesNotThrow(() => unbound.EndAction(Actor));
            Assert.That(_rig.Describe(), Is.Empty, "未绑定时零请求(不会被别的实例误收)");
        }

        [Test]
        public void test_r10_2_accessorReturnsNull_skipsSilently()
        {
            _driver.BindCameraRig(() => null);   // 拆卸段形态:访问器非 null 但返回 null

            Assert.DoesNotThrow(() => _driver.Arm(ActionOrdinal, new PatientId(1)),
                "机位被置 null(拆卸段)⇒ 静默跳过,不抛");
            Assert.IsTrue(_driver.HasCameraRig, "访问器本身仍在 ⇒ HasCameraRig 为 true(绑定没丢,是机位没了)");
        }

        // ══════════════ S10-05 计数载体已退役(结构判据,不点名旧字段)══════════════

        /// <summary>
        /// 断 `ModalPhaseResult` 的 **int 类型公开成员集合恰等于**白名单 ——
        /// 任何人往结构里再加一个「计数」都会被这条钉住。
        /// <para>⚠️ **刻意用集合相等而不是「不含某名字」**:后者要在源码里写出那个名字,
        /// 而 story-010 S10-05 的机械判据是 `grep` 零命中(注释也算命中)。</para>
        /// </summary>
        [Test]
        public void test_s10_05_modalPhaseResult_intMembers_areExactlyWhitelisted()
        {
            var intMembers = typeof(ModalPhaseResult)
                .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                .Where(f => f.FieldType == typeof(int))
                .Select(f => f.Name)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToArray();

            Assert.That(intMembers, Is.EqualTo(new[]
                {
                    "AttemptCount", "Edges", "EventCount", "HoldTicks", "Method",
                }),
                "ModalPhaseResult 的 int 成员集合变了 —— 若是**加回了「必发 Explore」的计数载体**, " +
                "那不构成 AC-10-11 的证据(它没有生产读者,断它等于断求值器自己写的字面量 = 假绿 F1)。" +
                "档位意图的真载体是本文件的请求序列断言 + PlayMode 真机断言。");
        }

        // ══════════════ S10-07 机械门:新调用点合规 ══════════════

        [Test]
        public void test_s10_07_cameraSetModeGate_zeroViolationOnProductionTree()
        {
            var errs = CameraSetModeRequesterGate.RunAll(".");

            Assert.That(errs, Is.Empty,
                "生产码出现单参 `SetMode` 或 `requesterId: -1` —— 甲案的「同一请求方可降档」判据" +
                "会恒假并**静默失效**(ADR-020 Amendment C ② / AC-20-16)。\n" +
                string.Join("\n", errs));
        }

        // ══════════════ 夹具内辅助类型 ══════════════

        /// <summary>
        /// 间谍 `ICameraRig` —— **只记录**请求序列,不重实现任何相机行为。
        /// <para>⚠️ `Mode` 恒为 `Explore` 且**不随 `SetMode` 变化**:本文件不能拿它当「相机认了」的证据。</para>
        /// </summary>
        private sealed class SpyCameraRig : ICameraRig
        {
            private readonly List<(CameraMode Mode, int Requester)> _requests =
                new List<(CameraMode, int)>();

            public YawBasis YawBasis => new YawBasis(UnityEngine.Vector3.forward, UnityEngine.Vector3.right);
            public float Yaw => 0f;
            public float Pitch => 0f;

            /// <summary>恒定值,刻意不随请求变化(见类注)。</summary>
            public CameraMode Mode => CameraMode.Explore;

            /// <summary>单参重载 —— 生产**不得**走这里(门判红);记成哨兵值以便断言意外调用。</summary>
            public void SetMode(CameraMode mode) => _requests.Add((mode, int.MinValue));

            public void SetMode(CameraMode mode, int requesterId) => _requests.Add((mode, requesterId));

            public void Tick(float deltaTime) { }

            public UnityEngine.Camera Camera => null;

            /// <summary>请求序列的紧凑文本(`Mode:Requester` 逗号分隔),便于断言与失败时直读。</summary>
            public string Describe() =>
                string.Join(",", _requests.Select(r =>
                    r.Requester == int.MinValue ? $"{r.Mode}:SINGLE_ARG" : $"{r.Mode}:{r.Requester}"));
        }

        private sealed class SeqIdAuthority : IIdAuthority
        {
            private int _next;
            public PatientId NextPatientId() => new PatientId(_next++);
            public ItemInstanceId NextItemInstanceId() => new ItemInstanceId(_next++);
        }

        private sealed class PresentPresence : IPresenceQuery
        {
            public bool IsPresent(PatientId patientId) => true;
            public int PresentCount => 1;
            public bool IsPresentAt(WorldPos cell) => true;
            public IReadOnlyCollection<int> PresentPatientIds() => new[] { 1 };
        }
    }
}
