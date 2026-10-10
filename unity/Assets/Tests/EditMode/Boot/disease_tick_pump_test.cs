// M2 接线轮阶段 2 · 批次 C(体征链核心)—— sim 主循环接线与**次序语义**(EditMode)。
//
// 断点④「无 sim 主循环」的可测面:疾病 Step 此前没有任何 tick 驱动
// (「病人出现后没有任何东西推进疾病」)。本文件证明三件事:
//   ① 接线 —— 帧泵把每个 tick 边沿转给 `DiseaseVitalsService.OnTickEdge`
//              (`BootRoot.StepVitals` 只是同一入口的薄转发);
//   ② 次序 —— 疾病 Step 排在同边沿 `PlayerController.OnTickEdge` **之后**
//              ⇒ 本边沿写入的病史事件在**本 tick** 的体征里可见(「体征在 tick 边沿后可见」);
//   ③ tick 回推 —— 一帧补 N 个 tick 时,回调拿到的是**各自的** tick(N 个不同值),
//              不是被 `SimTickDriver.CurrentTick` 污染成同一个末 tick。
//
// 三条各自的判别力(变异推演见任务报告):
//   · 把 onTickEdge 挪到 player.OnTickEdge **之前** ⇒ ② 的 `cellAlreadyCommitted` 断言红;
//   · 回退成 `onTickEdge(CurrentTick)` ⇒ ③ 断言红([3,3,3] ≠ [1,2,3]);
//   · 把 Step 挂到渲染帧而非 tick 边沿 ⇒ ③ 的零步断言红(0 s 帧不产生任何 Step)。
//
// 驱动:真装配袋(`CompositionRoot.Assemble(fixture)`)· 真 `EventStream` ·
//   真 `PayloadEncoder` · 真 PlayerController(EditMode 可跑真 CharacterController,
//   理由见 movement_feed_test 头注)。零 Fake。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Presentation.Player;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Tests.DiseaseSimulation;
using NUnit.Framework;
using UnityEngine;
using PlayerControllerType = DaYiJingCheng.Gameplay.Presentation.Player.PlayerController;

namespace DaYiJingCheng.Tests.Boot
{
    /// <summary><see cref="MovementFeed.PumpFrame"/> 的第二驱动口(体征链 Step)与次序契约。</summary>
    public class DiseaseTickPumpTest
    {
        /// <summary>单 tick 时长(OQ-25-8 = 0.05 s)—— 每帧恰 1 个 tick 边沿。</summary>
        private const double OneTick = 0.05;

        /// <summary>三 tick 帧 —— 用于「一帧补 N 个 tick」的回推断言。
        /// ⚠️ 取 0.16 而非 0.15:`0.15` 的 double 字面量比 `0.05×3` **略小**
        /// (0.1499999999999999944 &lt; 0.1500000000000000083),逐次累减后第三步差 5.6e-18
        /// ⇒ 只补 2 个 tick(实测)。0.16 留 0.01 s 余量,3 步对浮点不敏感。</summary>
        private const double ThreeTicks = 0.16;

        private GameObject _ground;
        private GameObject _playerGo;

        [SetUp]
        public void SetUp()
        {
            // 地面:无它则 isGrounded 恒 false,重力把 y 拖出格 ⇒ 制造与移动无关的假跨格。
            // (理由与顶面高度取值同 movement_feed_test。)
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.name = "DiseaseTickPumpTest_Ground";
            _ground.transform.position = new Vector3(0f, 0f, 0f);
            _ground.transform.localScale = new Vector3(400f, 1f, 400f);
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            if (_playerGo != null) UnityEngine.Object.DestroyImmediate(_playerGo);
            if (_ground != null) UnityEngine.Object.DestroyImmediate(_ground);
            _playerGo = null;
            _ground = null;
        }

        /// <summary>真装配 + 玩家(照 composition_root_test / movement_feed_test 的装配模式)。</summary>
        private CompositionRootServices AssembleWithPlayer(
            out PlayerControllerType player, out SimTickDriver driver)
        {
            var bag = CompositionRoot.Assemble(new[] { SyntheticDiseaseFixture.Create() });
            driver = new SimTickDriver();

            // 丢一个号:IdAuthority 初值 0 ⇒ 直接取号恒得 0,身份断言会退化成 0 == 0。
            bag.IdAuthority.NextPatientId();
            int actorId = bag.IdAuthority.NextPatientId().Value;
            Assert.Greater(actorId, 0, "actorId 必须非零(防身份断言空转)");

            _playerGo = new GameObject("DiseaseTickPumpTest_Player");
            player = _playerGo.AddComponent<PlayerControllerType>();
            player.Initialize(SimAuthorityMode.Host, bag.EventSink, driver, bag.Encoder, actorId);
            player.Teleport(new Vector3(0.5f, 1.55f, 0.5f));
            Physics.SyncTransforms();
            return bag;
        }

        /// <summary>指定 tick 上,流内是否已有 `ActorCellEntered`(= 玩家边沿已提交)。</summary>
        private static bool HasCellEnteredAt(CompositionRootServices bag, long tick)
        {
            IReadOnlyList<SimEvent> events = bag.Stream.Events;
            for (int i = 0; i < events.Count; i++)
            {
                SimEvent e = events[i];
                if (e.Kind == EventKind.ActorCellEntered && e.Tick == tick) return true;
            }
            return false;
        }

        [Test]
        public void test_pumpFrame_diseaseStepRunsAfterPlayerEdge_sameTickSeesItsEvents()
        {
            // Arrange
            CompositionRootServices bag = AssembleWithPlayer(
                out PlayerControllerType player, out SimTickDriver driver);

            var entry = SyntheticDiseaseFixture.Create();
            PatientId patient = bag.PatientSpawner.SpawnNext(entry.DiseaseId, tick: 0);

            var seenTicks = new List<long>();
            var cellAlreadyCommitted = new List<bool>();
            Action<long> probe = t =>
            {
                seenTicks.Add(t);
                cellAlreadyCommitted.Add(HasCellEnteredAt(bag, t));
                bag.VitalsService.OnTickEdge(t);     // 生产同一入口(BootRoot.StepVitals 的被调方)
            };

            // Act:一帧恰 1 tick(注入 0.05 s ⇒ 不读墙钟)
            MovementFeed.PumpFrame(player, driver, Vector2.zero, OneTick, probe);

            // Assert ①:恰一次边沿回调,tick = 该边沿自身的逻辑 tick
            Assert.AreEqual(1, seenTicks.Count, "0.05 s 帧 ⇒ 恰 1 个 tick 边沿");
            Assert.AreEqual(1L, seenTicks[0], "首个边沿 tick = 1(驱动器从 0 起)");

            // Assert ②:**次序契约** —— 回调被调时,玩家的 ActorCellEntered 已在流内
            Assert.IsTrue(cellAlreadyCommitted[0],
                "疾病 Step 必须排在 PlayerController.OnTickEdge **之后** —— " +
                "若反了,本边沿提交的事件要到下一个 tick 才进求值(体征晚一 tick)");

            // Assert ③:同边沿写入的 DiseaseOnset 在**本 tick** 即可读(断点①+④ 闭合)
            Assert.DoesNotThrow(
                () => bag.VitalsService.GetVitals(patient),
                "本边沿驱动后必须已建档并可查询(体征在 tick 边沿后可见)");
        }

        [Test]
        public void test_pumpFrame_multiTickFrame_drivesOneStepPerEdgeWithOwnTick()
        {
            // Arrange:一帧注入 0.15 s ⇒ SimTickDriver 补 3 个 tick(maxStepsPerFrame = 5 内)
            CompositionRootServices bag = AssembleWithPlayer(
                out PlayerControllerType player, out SimTickDriver driver);

            PatientId patient = bag.PatientSpawner.SpawnNext(
                SyntheticDiseaseFixture.DiseaseId, tick: 0);

            var seenTicks = new List<long>();
            Action<long> probe = t =>
            {
                seenTicks.Add(t);
                bag.VitalsService.OnTickEdge(t);
            };

            // Act
            MovementFeed.PumpFrame(player, driver, Vector2.zero, ThreeTicks, probe);

            // Assert ①:三个边沿,各带**自己的** tick(回推 = CurrentTick − steps + 1 起)
            Assert.AreEqual(3L, driver.CurrentTick, "0.15 s ⇒ CurrentTick 推进 3");
            Assert.AreEqual(3, seenTicks.Count, "每 tick 恰一次 Step(不由渲染帧驱动)");
            CollectionAssert.AreEqual(new long[] { 1L, 2L, 3L }, seenTicks,
                "回调必须逐边沿回推各自 tick —— 若写成 onTickEdge(CurrentTick)," +
                "三次全给末 tick(= [3,3,3]),体征会被同一时刻覆盖三次");

            // Assert ②:三次驱动后仍可查询(未因重复 tick / 游标问题抛出)
            Assert.DoesNotThrow(() => bag.VitalsService.GetVitals(patient),
                "三次 Step 后必须可查询");
        }

        [Test]
        public void test_pumpFrame_zeroSteps_doesNotDriveAnyEdge()
        {
            // Arrange:0 s ⇒ 不满一个 tick ⇒ 零边沿
            CompositionRootServices bag = AssembleWithPlayer(
                out PlayerControllerType player, out SimTickDriver driver);
            bag.PatientSpawner.SpawnNext(SyntheticDiseaseFixture.DiseaseId, tick: 0);

            int calls = 0;
            Action<long> probe = _ => calls++;

            // Act
            MovementFeed.PumpFrame(player, driver, Vector2.zero, 0.0, probe);

            // Assert
            Assert.AreEqual(0, calls, "未满一个 tick 不得触发任何 Step");
            Assert.AreEqual(0L, driver.CurrentTick, "tick 不得凭空前进");
            Assert.AreEqual(0, bag.VitalsService.EvaluatedCount,
                "没有边沿 ⇒ 没有病人被求值(否则求值被渲染帧驱动,违 ADR-005)");
        }
    }
}
