// sprint-06 G4.1 · 急救手动化 —— 自动触发移除后的**零输入观察窗**判据(EditMode · 2026-10-11)
//
// 判据(可证伪 · sprint-06 §2 G4.1):
//   ① 零输入观察窗:驱动在**无玩家急救意图**下对在场病人跑若干 tick 边沿
//      ⇒ 病史流**零 EmergencyAttempt**(自动触发分支必须已删除);
//   ② 手动入口:调用 TryManual ⇒ EmergencyAttempt + EmergencyTreatmentApplied 双事件入病史流
//      (真 HostEmergencyProcessor · 病人归因 · 流路由 History);
//   ③ Skip 路径:method = 1 ⇒ 载荷 method = Skip(跳过在流上可判别,R-6);
//   ④ 越界 / 负值 fail-loud(method 闭集 / tick / actorId)。
//
// ⚠️ 红线(sprint-06 §2 G4.3 qa-lead 注):**旧 `emergency_attempt_wiring_test` 断言
//   「启动即自动两事件」= 假绿** —— 本测与其反向:自动触发已移除,零输入必须零事件。

using System;
using System.Linq;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Boot
{
    /// <summary><see cref="EmergencyManualDriver"/> 判据(G4.1/G4.2)。</summary>
    internal sealed class EmergencyManualDriverTest
    {
        private CompositionRootServices _bag;
        private EmergencyManualDriver _driver;
        private PatientId _patient;

        [SetUp]
        public void SetUp()
        {
            _bag = CompositionRoot.Assemble();
            _driver = _bag.EmergencyManualDriver;

            _patient = _bag.PatientSpawner.SpawnNext(diseaseId: 1, tick: 0);
            _bag.Presence.AddPresent(_patient, new WorldPos(0, 0, 0));
            _bag.VitalsService.OnTickEdge(0);
        }

        [Test]
        public void test_zeroInput_window_emitsNoEmergencyAttempt()
        {
            // 跑若干 tick 边沿,但不调用 TryManual —— 自动触发分支不得存在
            for (int i = 0; i < 20; i++)
                _bag.TickDriver.Advance(0.05d);

            CollectionAssert.DoesNotContain(_bag.Stream.Events.Select(e => e.Kind),
                EventKind.EmergencyAttempt,
                "零输入观察窗 ⇒ 病史流零 EmergencyAttempt(自动触发已移除 —— G4.1 判据)");
            CollectionAssert.DoesNotContain(_bag.Stream.Events.Select(e => e.Kind),
                EventKind.EmergencyTreatmentApplied,
                "零输入 ⇒ 零处置事件(双事件都不发)");
        }

        [Test]
        public void test_manualEntry_writesBothEvents_intoHistory()
        {
            int before = _bag.Stream.Count;

            var result = _driver.TryManual(_patient, method: 0, actorId: 9001, tick: 5);

            Assert.IsTrue(result.Applied, "手动入口走完 Process(主机上下文)");
            Assert.AreEqual(before + 2, _bag.Stream.Count, "一次手动 ⇒ 恰两条流事件");
            Assert.AreEqual(EventKind.EmergencyAttempt, _bag.Stream.Events[before].Kind, "① EmergencyAttempt");
            Assert.AreEqual(EventKind.EmergencyTreatmentApplied, _bag.Stream.Events[before + 1].Kind,
                "② EmergencyTreatmentApplied");

            // 流路由(纯函数):急救 Kind 落病史流
            Assert.AreEqual(StreamId.History, StreamRouting.Of(EventKind.EmergencyAttempt));
            Assert.AreEqual(StreamId.History, StreamRouting.Of(EventKind.EmergencyTreatmentApplied));

            // 病人归因(批次 E):两事件头 Patient 须 = 被施救病人
            Assert.AreEqual(_patient, _bag.Stream.Events[before].Patient, "attempt 归属病人");
            Assert.AreEqual(_patient, _bag.Stream.Events[before + 1].Patient, "applied 归属病人");
        }

        [Test]
        public void test_manualSkip_methodSurvivesIntoPayload()
        {
            _driver.TryManual(_patient, method: 1, actorId: 9001, tick: 6);

            SimEvent applied = _bag.Stream.Events.Last(e => e.Kind == EventKind.EmergencyTreatmentApplied);
            Assert.IsTrue(PayloadCodec.TryGetPayload(applied, _bag.BlobPool,
                out EmergencyTreatmentAppliedPayload p), "applied 载荷解码");
            Assert.AreEqual(1, p.Method, "method = Skip(跳过在流上可判别 —— R-6 / ADR-019)");

            SimEvent attempt = _bag.Stream.Events.Last(e => e.Kind == EventKind.EmergencyAttempt);
            Assert.IsTrue(PayloadCodec.TryGetPayload(attempt, _bag.BlobPool,
                out EmergencyAttemptPayload ap), "attempt 载荷解码");
            Assert.AreEqual(1, ap.Method, "attempt 载荷 method = Skip");
            Assert.AreEqual(9001, ap.ActorId, "actor_id = 施予者");
        }

        [Test]
        public void test_invalidMethod_throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => _driver.TryManual(_patient, method: 2, actorId: 9001, tick: 7),
                "method 闭集 {0,1},越界 = 编程错误 fail-loud");
            Assert.Throws<ArgumentOutOfRangeException>(
                () => _driver.TryManual(_patient, method: -1, actorId: 9001, tick: 7),
                "method 负值 fail-loud");
        }

        [Test]
        public void test_invalidTickAndActor_throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => _driver.TryManual(_patient, method: 0, actorId: 9001, tick: -1),
                "tick<0 fail-loud");
            Assert.Throws<ArgumentOutOfRangeException>(
                () => _driver.TryManual(_patient, method: 0, actorId: -1, tick: 7),
                "actorId 负值 fail-loud");
        }
    }
}
