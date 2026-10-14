// story-009 · 急救直读通道运行期装配 —— 端到端 + Idle 门(2026-10-14)
//
// 判据(可证伪):
//   ① AC-9-04 端到端:Feed 采样 → EndAction ⇒ 病史流 EmergencyAttempt +
//      EmergencyTreatmentApplied 双事件,载荷字段 = 聚合形状(method = 0);
//   ② AC-9-04b 强度判别:带幅度沿的聚合 ⇒ F-10.4 得 Applied 档强度 = 65536
//      (BasePotency 1.0 × ResultMul[Applied] 1.0);
//   ③ 零样本签约:Arm 后直接 EndAction(未喂任何样本)⇒ 零事件(F-10.5);
//   ④ 中止(规则六之甲):Armed 持续 ≥ AbortIdleTicks(50)⇒ OnTickEdge 中止,
//      零事件 + 回 Idle(中止 ≠ 跳过 —— 零 method=1 载荷);
//   ⑤ 中止未达阈值:49 tick 边沿 ⇒ 不中止(动作继续);
//   ⑥ AC-9-06 Idle 门:Arm ⇒ 列表隐藏;End/Abort ⇒ 复现(Idle 门回调直驱);
//   ⑦ 中止后松开沿幂等:AbortAction 后 EndAction ⇒ 不重复上行(已回 Idle)。
//
// ⚠️ 逻辑层模式:通道构造传 actions = null(AC-3-A1 单实例纪律 —— EditMode/PlayMode
//   不经 InputSystem.actions / Addressables 装载第二实例);驱动经 FeedForTest 纯逻辑驱动,
//   与 story-007 phase spike 同款(不依赖输入相位)。

using System.Linq;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Input;
using DaYiJingCheng.Gameplay.Interaction;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.EmergencyProcedures;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.PlayMode
{
    /// <summary>story-009 通道装配判据(逻辑层;FeedForTest 驱动,不点火真实输入)。</summary>
    public sealed class emergency_channel_wiring_test
    {
        private CompositionRootServices _bag;
        private EmergencyChannelDriver _driver;

        [SetUp]
        public void SetUp()
        {
            _bag = CompositionRoot.Assemble();   // 默认:inputActions = null(逻辑层)
            _driver = _bag.EmergencyChannel;
        }

        [TearDown]
        public void TearDown()
        {
            _driver?.Detach();   // 逻辑层未挂,幂等;防未来模式改动后悬挂回调
        }

        private static EmergencyReading Reading(int hold, int edges, int[] edgeTicks, int mag)
            => new EmergencyReading(0, hold, edges, edgeTicks, mag);

        [Test]
        public void test_armFeedEnd_writesBothEvents_withAggregatedShape()
        {
            var pid = _bag.PatientSpawner.SpawnNext(diseaseId: 1, tick: 0);
            _bag.Presence.AddPresent(pid, new WorldPos(0, 0, 0));
            _bag.VitalsService.OnTickEdge(0);
            int before = _bag.Stream.Count;

            // AC-9-02 相位门:Idle 期动作 disabled → Arm ⇒ Armed + enabled(AC-3-E4 转发面)
            Assert.IsFalse(_driver.EmergencyActionEnabled, "AC-9-02:Idle ⇒ Emergency 动作 disabled");
            _driver.Arm(actionOrdinal: 0, pid);
            Assert.AreEqual(DirectChannelState.Armed, _driver.State, "前置:Armed");
            Assert.IsTrue(_driver.EmergencyActionEnabled, "AC-9-02:Armed ⇒ Emergency 动作 enabled");

            // AC-9-03 采样门:Armed 期 Feed ⇒ SampleCount 递增
            Assert.AreEqual(0, _driver.SampleCount, "前置:未采样");
            _driver.FeedForTest(axis: 0.8f, newlyPressed: true);   // 样本 1:press 沿 + 幅度
            _driver.FeedForTest(axis: 0.6f, newlyPressed: false);  // 样本 2:幅度下降
            Assert.AreEqual(2, _driver.SampleCount, "AC-9-03:Armed 期 Feed ⇒ 采样计数递增");

            bool applied = _driver.EndAction(actorId: 9001);
            Assert.IsTrue(applied, "有样本 End ⇒ 双事件入流");
            Assert.AreEqual(DirectChannelState.Idle, _driver.State, "AC-9-02:End ⇒ 回 Idle");
            Assert.IsFalse(_driver.EmergencyActionEnabled, "AC-9-02:Idle ⇒ 动作 disabled(AC-3-E4)");

            Assert.AreEqual(before + 2, _bag.Stream.Count, "恰两条流事件");
            Assert.AreEqual(EventKind.EmergencyAttempt, _bag.Stream.Events[before].Kind);
            Assert.AreEqual(EventKind.EmergencyTreatmentApplied, _bag.Stream.Events[before + 1].Kind);

            // 载荷形状 = 聚合交出物(八字段全载;method 0 = Manual)
            Assert.IsTrue(PayloadCodec.TryGetPayload(_bag.Stream.Events[before], _bag.BlobPool,
                out EmergencyAttemptPayload attempt));
            Assert.AreEqual(0, attempt.Action, "Action = 预约 ordinal(0 = 止血包扎)");
            Assert.AreEqual(1, attempt.Edges, "Edges = press 沿数(仅样本 1 newlyPressed)");
            Assert.AreEqual(0, attempt.Method, "直读通道恒 Manual(Skip 只经 TryManual)");
            Assert.AreEqual(9001, attempt.ActorId);
            Assert.AreEqual(0, attempt.HoldTicks,
                "HoldTicks = 末沿 − 首沿(F-10.5;单沿 ⇒ 0 —— 不是采样帧数)");

            // 施加对象 = Arm 时锁定的目标病人(批次 E 归因)
            Assert.AreEqual(pid, _bag.Stream.Events[before].Patient, "事件头归因 = 目标病人");
        }

        [Test]
        public void test_armFeedEnd_fullMagnitude_yieldsAppliedPotency()
        {
            var pid = _bag.PatientSpawner.SpawnNext(diseaseId: 1, tick: 0);
            _bag.Presence.AddPresent(pid, new WorldPos(0, 0, 0));
            _bag.VitalsService.OnTickEdge(0);
            int before = _bag.Stream.Count;

            _driver.Arm(0, pid);
            _driver.FeedForTest(0.8f, newlyPressed: true);   // magPeak = round(0.8 × 65536) ≈ 52429
            _driver.FeedForTest(1.0f, newlyPressed: false);  // magPeak = 65536(夹取上界)

            Assert.IsTrue(_driver.EndAction(actorId: 9001));

            // 强度判别:幅度 ≥ 门槛(50)⇒ Judge Applied ⇒ ResultMul[Applied] = 1.0
            // ⇒ F-10.4:BasePotency(1.0)× 1.0 = 65536。Skip 的 AppliedWeak 档是 32768,
            // 两值可判别 = AC-9-05 不与端到端路径混淆的形状前提。
            Assert.IsTrue(PayloadCodec.TryGetPayload(_bag.Stream.Events[before + 1], _bag.BlobPool,
                out EmergencyTreatmentAppliedPayload applied));
            Assert.AreEqual(65536L, applied.DrugPotency.Raw,
                "Applied 档强度 = 65536(F-10.4;≠ Skip 档 32768 —— 强度判别面)");
            Assert.AreEqual(0, applied.Method);
        }

        [Test]
        public void test_armImmediateEnd_zeroSamples_emitsNothing()
        {
            var pid = _bag.PatientSpawner.SpawnNext(diseaseId: 1, tick: 0);
            _bag.Presence.AddPresent(pid, new WorldPos(0, 0, 0));
            _bag.VitalsService.OnTickEdge(0);
            int before = _bag.Stream.Count;

            _driver.Arm(0, pid);
            bool applied = _driver.EndAction(actorId: 9001);   // 零样本:直接松开

            Assert.IsFalse(applied, "零样本签约 ⇒ 零上行(F-10.5)");
            Assert.AreEqual(before, _bag.Stream.Count, "零样本 ⇒ 零事件");
            Assert.AreEqual(DirectChannelState.Idle, _driver.State, "End 后回 Idle");
        }

        [Test]
        public void test_armedIdleBeyondAbortTicks_tickEdgeAborts_zeroEvents()
        {
            var pid = _bag.PatientSpawner.SpawnNext(diseaseId: 1, tick: 0);
            _bag.Presence.AddPresent(pid, new WorldPos(0, 0, 0));
            _bag.VitalsService.OnTickEdge(0);
            int before = _bag.Stream.Count;

            _driver.Arm(0, pid);   // armedTick = 0(全新世界 tick 0)
            _driver.FeedForTest(0.8f, newlyPressed: true);

            // 规则六之甲:Armed 内持续无新边沿 ≥ 50 tick ⇒ 中止(零事件)。
            // 按住语义:press 沿只在样本 1;49 边沿不达阈值,第 50 个中止。
            for (int t = 1; t < ModalPhaseEvaluator.AbortIdleTicks; t++)
                _driver.OnTickEdge(t);
            Assert.AreEqual(DirectChannelState.Armed, _driver.State, "49 tick 不中止(未达阈值)");
            Assert.AreEqual(before, _bag.Stream.Count, "阈值内零事件");

            _driver.OnTickEdge(ModalPhaseEvaluator.AbortIdleTicks);   // 第 50 tick ⇒ 中止
            Assert.AreEqual(DirectChannelState.Idle, _driver.State, "达阈值 ⇒ 中止回 Idle");
            Assert.AreEqual(before, _bag.Stream.Count, "中止零事件(中止 ≠ 跳过)");

            // 流上零 method=1 载荷(中止不发 Skip 事件 —— 与跳过判别)
            CollectionAssert.DoesNotContain(
                _bag.Stream.Events.Skip(before).Select(e => e.Kind),
                EventKind.EmergencyAttempt, "中止零 attempt");
        }

        [Test]
        public void test_longPressWithContinuousFeeding_notAborted()
        {
            // 评审 F-1 回归(2026-10-14):中止看门狗 = 采样活跃度,非「距 Arm 时长」。
            // 按住语义下首 press 沿后恒无新边沿 ⇒ 旧判据(tick - armedTick ≥ 50)会把
            // 任何 ≥ 2.5 s 的合法长按在 tick 边沿误杀(零事件)。正确形态:接线模式
            // 每 Armed 帧都有 Feed ⇒ lastActiveTick 恒新 ⇒ 长按永不被中止。
            var pid = _bag.PatientSpawner.SpawnNext(diseaseId: 1, tick: 0);
            _bag.Presence.AddPresent(pid, new WorldPos(0, 0, 0));
            _bag.VitalsService.OnTickEdge(0);
            int before = _bag.Stream.Count;

            _driver.Arm(0, pid);   // armedTick = lastActiveTick = 0

            // 120 tick 边沿(> 阈值 50 的 2 倍),每个边沿都伴随采样(接线模式形态)。
            // ⚠️ 须同步推 TickDriver:生产里 lastActiveTick 与 tick 泵同源推进;
            //   测试不推则 CurrentTick 恒 0,看门狗读数失真(模拟失真,非产品缺陷)。
            for (int t = 1; t <= 120; t++)
            {
                _bag.TickDriver.Advance(0.05d);
                _driver.FeedForTest(0.8f, newlyPressed: false);   // 长按中:无新沿,有采样
                _driver.OnTickEdge(_bag.TickDriver.CurrentTick);
                Assert.AreEqual(DirectChannelState.Armed, _driver.State,
                    $"持续采样 ⇒ 第 {t} tick 不中止(评审 F-1:看门狗只拦采样停)");
            }
            Assert.AreEqual(before, _bag.Stream.Count, "零事件(动作还在途中)");

            // 松开 ⇒ 正常聚合上行(误杀回归的最终判据:长按真的产出双事件)
            _driver.FeedForTest(0.8f, newlyPressed: false);
            Assert.IsTrue(_driver.EndAction(actorId: 9001), "长按后松开 ⇒ 双事件入流");
            Assert.AreEqual(before + 2, _bag.Stream.Count);
        }

        [Test]
        public void test_abortThenRelease_endActionIdempotent()
        {
            var pid = _bag.PatientSpawner.SpawnNext(diseaseId: 1, tick: 0);
            _bag.Presence.AddPresent(pid, new WorldPos(0, 0, 0));
            _bag.VitalsService.OnTickEdge(0);
            int before = _bag.Stream.Count;

            _driver.Arm(0, pid);
            _driver.FeedForTest(0.8f, newlyPressed: true);
            Assert.IsTrue(_driver.AbortAction(), "中止前有动作在途");

            // 中止后松开沿:通道已回 Idle ⇒ EndAction 零样本签约(no-op,不重复上行)
            bool applied = _driver.EndAction(actorId: 9001);
            Assert.IsFalse(applied, "中止后 End = 幂等 no-op(规则六之甲:中止不发)");
            Assert.AreEqual(before, _bag.Stream.Count, "零事件");
        }

        [Test]
        public void test_idleGate_hidesOnArm_restoresOnEnd()
        {
            var pid = _bag.PatientSpawner.SpawnNext(diseaseId: 1, tick: 0);
            _bag.Presence.AddPresent(pid, new WorldPos(0, 0, 0));
            _bag.VitalsService.OnTickEdge(0);

            bool gateVisible = false;
            bool? lastGate = null;
            var gateDriver = new EmergencyChannelDriver(
                new EmergencyDirectReadChannel(null, 65536, 0, 65536),
                _bag.EmergencyManualDriver, () => _bag.TickDriver.CurrentTick,
                onIdleGate: v => { gateVisible = v; lastGate = v; });

            gateDriver.Arm(0, pid);
            Assert.IsTrue(gateVisible == false && lastGate == false,
                "AC-9-06:Arm ⇒ 候选列表隐藏(Idle 门 false)");

            gateDriver.FeedForTest(0.8f, newlyPressed: true);
            gateDriver.EndAction(actorId: 9001);
            Assert.IsTrue(gateVisible == true && lastGate == true,
                "AC-9-06:End ⇒ 列表复现(Idle 门 true)");
        }

        [Test]
        public void test_idleGate_restoresOnAbort()
        {
            var pid = _bag.PatientSpawner.SpawnNext(diseaseId: 1, tick: 0);
            _bag.Presence.AddPresent(pid, new WorldPos(0, 0, 0));
            _bag.VitalsService.OnTickEdge(0);

            bool? lastGate = null;
            var gateDriver = new EmergencyChannelDriver(
                new EmergencyDirectReadChannel(null, 65536, 0, 65536),
                _bag.EmergencyManualDriver, () => _bag.TickDriver.CurrentTick,
                onIdleGate: v => lastGate = v);

            gateDriver.Arm(0, pid);
            Assert.IsTrue(lastGate == false, "前置:Arm ⇒ 隐藏");

            gateDriver.AbortAction();
            Assert.IsTrue(lastGate == true, "AC-9-06:中止 ⇒ 列表复现");
        }

        [Test]
        public void test_iarmedState_productionImplementor()
        {
            // story-009 用户裁定「一并实现 IArmedState」:结清 ModalGate 方向①零实现者空挂。
            IArmedState armed = _driver;   // 接口可达性(生产实现者存在)
            Assert.IsFalse(armed.IsArmed, "Idle ⇒ IsArmed = false");

            var pid = _bag.PatientSpawner.SpawnNext(diseaseId: 1, tick: 0);
            _bag.Presence.AddPresent(pid, new WorldPos(0, 0, 0));
            _bag.VitalsService.OnTickEdge(0);

            _driver.Arm(0, pid);
            Assert.IsTrue(armed.IsArmed, "Armed ⇒ IsArmed = true(IArmedState 视角)");

            _driver.FeedForTest(0.8f, newlyPressed: true);
            _driver.EndAction(actorId: 9001);
            Assert.IsFalse(armed.IsArmed, "End ⇒ 回 Idle");
        }
    }
}
