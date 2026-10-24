// S19-2 测试面 —— IPlayerMotor 契约 + PlayerController 实现(TD-player-02 兑现)。
//
// 权威来源:
//   ADR-020 §Key Interfaces:396-402 —— 四成员权威签名
//   sprint-19.md §S19-2 判据 ①②③④ + PRE-S19-C 三条实现约束(用户 2026-10-24)
//   docs/tech-debt-register.md TD-player-02 —— 解除动作与卡住的判据
//
// 判据 → 测试映射:
//   ① 四成员齐(含无出参 ConsumeCellChanged)→ test_motor_interface_members_areComplete
//   ③ 跨格恰好一次:不进格不触发 / 原地抖动不重复 / 连跨两格两条各一次 → 三条
//   约束① 锁存器与 _hasPending 不相交 → test_consume_doesNotSteal_pendingPath
//   约束② Cell 走 FloorToInt(负坐标判别)→ test_motor_cell_usesFloorToFloat_notGetCell
//   判据② 位移真源唯一(Move 仍唯一写入)→ test_tick_delegatesToMove_singleWritePath
//
// 测试命名: test_[system]_[scenario]_[expected](test-standards)。
// 夹具纪律:MonoBehaviour 用 AddComponent 真夹具(S11-3 惯例,禁 new);
// 全部零写盘、零外部状态。

using System;
using System.Collections.Generic;
using System.Reflection;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Gameplay.Presentation.Player;
using NUnit.Framework;
using UnityEngine;
using PlayerControllerType = DaYiJingCheng.Gameplay.Presentation.Player.PlayerController;

namespace DaYiJingCheng.Tests.PlayerController
{
    public class PlayerMotorTest
    {
        private PayloadEncoder _encoder;
        private InMemoryBlobPool _pool;
        private FakeEventSink _sink;
        private readonly List<GameObject> _spawned = new List<GameObject>();

        [SetUp]
        public void Setup()
        {
            _pool = new InMemoryBlobPool();
            _encoder = new PayloadEncoder(_pool);
            _sink = new FakeEventSink();
        }

        [TearDown]
        public void Teardown()
        {
            foreach (var go in _spawned)
            {
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
            }
            _spawned.Clear();
        }

        private PlayerControllerType CreateController(int actorId = 0)
        {
            var go = new GameObject("test_player_motor");
            _spawned.Add(go);
            go.AddComponent<CharacterController>();
            var pc = go.AddComponent<PlayerControllerType>();
            pc.Initialize(SimAuthorityMode.Host, _sink, new FakeTickProvider(100), _encoder, actorId);
            return pc;
        }

        // ══════════ 判据 ①:四成员齐(反射面,不依赖调用) ══════════

        [Test]
        public void test_motor_interface_members_areComplete()
        {
            // ADR-020 :396-402 权威形状;PRE-S19-C:ConsumeCellChanged 无出参。
            var it = typeof(IPlayerMotor);
            Assert.IsTrue(it.IsInterface, "IPlayerMotor 须是接口");

            var tick = it.GetMethod("Tick");
            Assert.IsNotNull(tick, "成员 Tick 缺失");
            Assert.AreEqual(typeof(void), tick.ReturnType, "Tick 返回 void");
            var ps = tick.GetParameters();
            Assert.AreEqual(2, ps.Length, "Tick(float, in MoveInput) 两形参");
            Assert.AreEqual(typeof(float), ps[0].ParameterType, "Tick[0] = float");
            // `in` 形参的反射类型带 `&` 后缀(按引用传递标记)—— 剥掉再比。
            Assert.AreEqual(typeof(MoveInput), ps[1].ParameterType.GetElementType() ?? ps[1].ParameterType,
                "Tick[1] = in MoveInput");
            Assert.IsTrue(ps[1].ParameterType.IsByRef, "Tick[1] 须按引用传递(in 修饰符)");

            Assert.IsNotNull(it.GetProperty("Cell"), "成员 Cell 缺失");
            Assert.AreEqual(typeof(WorldPos), it.GetProperty("Cell").PropertyType,
                "Cell 类型 = WorldPos(整数格,非 Int3 / Vector3)");

            Assert.IsNotNull(it.GetProperty("Position"), "成员 Position 缺失");
            Assert.AreEqual(typeof(Vector3), it.GetProperty("Position").PropertyType,
                "Position 类型 = Vector3(表现态)");

            var consume = it.GetMethod("ConsumeCellChanged");
            Assert.IsNotNull(consume, "成员 ConsumeCellChanged 缺失");
            Assert.AreEqual(typeof(bool), consume.ReturnType, "ConsumeCellChanged 返回 bool");
            Assert.AreEqual(0, consume.GetParameters().Length,
                "ConsumeCellChanged 无出参(PRE-S19-C 用户裁定:只读边沿)");

            // 判据 ④:与 ICameraRig 同装配(ADR-025 §② 落位)。
            Assert.AreEqual(typeof(DaYiJingCheng.Gameplay.Presentation.Camera.ICameraRig).Assembly,
                it.Assembly, "IPlayerMotor 与 ICameraRig 同在 Gameplay.Presentation");

            // PlayerController 实现该接口(TD-player-02 解除动作)。
            Assert.IsTrue(typeof(IPlayerMotor).IsAssignableFrom(typeof(PlayerControllerType)),
                "PlayerController 须实现 IPlayerMotor");
        }

        // ══════════ 判据 ③:跨格恰好一次 —— 三场景 ══════════

        [Test]
        public void test_motor_noCellChange_doesNotLatch()
        {
            // 不进格 ⇒ 无提交边沿 ⇒ ConsumeCellChanged 恒 false。
            var pc = CreateController();

            // 先提交一格建立基线(首提交 = 从无效哨兵 → 格 0,本身就是一条跨格边沿 ——
            // CellTransitionDetector 既有语义「首个样本必触发转移」;消费掉它)。
            pc.OnPositionSample(new Vector3(0.5f, 0f, 0.5f));
            pc.OnTickEdge(10L);
            Assert.IsTrue(pc.ConsumeCellChanged(), "前提:基线建立(首提交 = 边沿)");

            // 同一格内反复采样 + 提交(0.x 段抖动,FloorToInt 后恒格 0)⇒ 零新事件、零新边沿。
            pc.OnPositionSample(new Vector3(0.2f, 0f, 0.2f));
            pc.OnTickEdge(11L);
            pc.OnPositionSample(new Vector3(0.8f, 0f, 0.8f));
            pc.OnTickEdge(12L);

            Assert.AreEqual(1, _sink.AppendedEvents.Count,
                "同格采样 + 提交 ⇒ 零新事件(FloorToInt(0.2)=FloorToInt(0.8)=0)");
            Assert.IsFalse(pc.ConsumeCellChanged(),
                "未跨格 ⇒ 边沿锁存器不置位");
        }

        [Test]
        public void test_motor_singleCross_latchesExactlyOnce_notTwice()
        {
            // 原地抖动不重复:一次提交边沿只产生一次可消费真值 ——
            // 不消费它,再多次 OnTickEdge(同格)也不重新置位。
            var pc = CreateController();

            pc.OnPositionSample(new Vector3(0.5f, 0f, 0.5f));   // 格 (0,0,0)
            pc.OnTickEdge(10L);                                  // 首提交 ⇒ 边沿 1
            Assert.IsTrue(pc.ConsumeCellChanged(), "跨格(首提交)⇒ 真值一次");

            // 不消费场景的反面:消费后再问 ⇒ false(锁存器已清,不重复发放)。
            Assert.IsFalse(pc.ConsumeCellChanged(),
                "同一边沿不得二次消费(消费即清锁存器)");

            // 同 tick 内原地抖动(跨出去又回来):折返 ⇒ pending 清除 ⇒ 无事件无边沿。
            pc.OnPositionSample(new Vector3(1.5f, 0f, 0.5f));   // 格 (1,0,0) → pending
            pc.OnPositionSample(new Vector3(0.5f, 0f, 0.5f));   // 折回 (0,0,0) = 已提交格
            pc.OnTickEdge(11L);
            Assert.AreEqual(1, _sink.AppendedEvents.Count,
                "折返不发(A→B→A 同 tick ⇒ 零事件)");
            Assert.IsFalse(pc.ConsumeCellChanged(),
                "折返无边沿可消费");
        }

        [Test]
        public void test_motor_twoConsecutiveCrosses_eachLatchesOnce()
        {
            // 连跨两格的两条边沿各一次:A→B(边沿 1)→ 消费 → B→C(边沿 2)→ 消费。
            var pc = CreateController();

            pc.OnPositionSample(new Vector3(0.5f, 0f, 0.5f));   // 格 (0,0,0)
            pc.OnTickEdge(10L);                                  // 提交 (0,0,0) ⇒ 边沿 1
            Assert.AreEqual(1, _sink.AppendedEvents.Count);
            Assert.IsTrue(pc.ConsumeCellChanged(), "第一条跨格边沿(A→B)");

            pc.OnPositionSample(new Vector3(1.5f, 0f, 0.5f));   // 格 (1,0,0)
            pc.OnTickEdge(11L);                                  // 提交 (1,0,0) ⇒ 边沿 2
            Assert.AreEqual(2, _sink.AppendedEvents.Count);
            Assert.IsTrue(pc.ConsumeCellChanged(), "第二条跨格边沿(B→C)各一次");
            Assert.IsFalse(pc.ConsumeCellChanged());
        }

        [Test]
        public void test_motor_unconsumedEdge_persistsAcrossTicks_untilConsumed()
        {
            // 边沿锁存器跨 tick 保持:提交后不消费,下一 tick 的同格提交(零事件)
            // 不得清除或重复它 —— 消费者晚一拍也能拿到该边沿。
            var pc = CreateController();

            pc.OnPositionSample(new Vector3(0.5f, 0f, 0.5f));
            pc.OnTickEdge(10L);                                  // 边沿 1(未消费)
            pc.OnTickEdge(11L);                                  // 同格,零事件
            pc.OnTickEdge(12L);

            Assert.AreEqual(1, _sink.AppendedEvents.Count, "同格重复提交不发事件");
            Assert.IsTrue(pc.ConsumeCellChanged(),
                "未消费的边沿跨 tick 保持,恰好一次可取");
            Assert.IsFalse(pc.ConsumeCellChanged());
        }

        // ══════════ 约束 ①:锁存器与 Append 主路径不相交 ══════════

        [Test]
        public void test_motor_consume_doesNotSteal_pendingPath()
        {
            // 约束 ①(可观测形态):**锁存器亮着且新 pending 待提交**的共存态下,
            // 消费旧边沿不得吞掉 Append 主路径 —— 若 ConsumeCellChanged 误清
            // _hasPending / _pendingCell,下一 tick 的跨格事件会**静默丢失**。
            // (注:消费发生在「OnTickEdge 已自清 pending 之后」时,变异不可观测 ——
            //  本测刻意构造共存态,两个状态变量都活着。)
            var pc = CreateController();

            pc.OnPositionSample(new Vector3(0.5f, 0f, 0.5f));   // 格 0
            pc.OnTickEdge(10L);                                  // 提交 ⇒ 边沿 1(未消费)
            Assert.AreEqual(1, _sink.AppendedEvents.Count);

            pc.OnPositionSample(new Vector3(1.5f, 0f, 0.5f));   // 新 pending(格 1)待提交
            Assert.IsTrue(pc.ConsumeCellChanged(), "共存态:旧边沿可消费");

            pc.OnTickEdge(11L);                                  // pending 须照常提交

            Assert.AreEqual(2, _sink.AppendedEvents.Count,
                "消费边沿不得吞掉待提交的 pending(第二条跨格事件不丢 —— 约束 ① 本体)");
            Assert.IsTrue(pc.ConsumeCellChanged(), "第二条边沿仍可消费");
            Assert.IsFalse(pc.ConsumeCellChanged());
        }

        // ══════════ 约束 ②:Cell 走 FloorToInt(与事件格同源) ══════════

        [Test]
        public void test_motor_cell_usesFloorToFloat_notGetCell()
        {
            // 负坐标判别:pos = -0.5 ⇒ FloorToInt = -1(事件路径),RoundToInt = 0(GetCell)。
            // 契约 Cell 若与 ActorCellEnteredPayload.Cell 不同源,此处必取错值。
            var pc = CreateController();
            pc.Teleport(new Vector3(-0.5f, 0f, -0.5f));

            WorldPos contractCell = pc.Cell;

            // 提交一次同位置采样,取事件载荷的 Cell 做同源对照。
            pc.OnPositionSample(new Vector3(-0.5f, 0f, -0.5f));
            pc.OnTickEdge(10L);
            Assert.AreEqual(1, _sink.AppendedEvents.Count);
            var payload = new PayloadDecoder(_pool).Decode<ActorCellEnteredPayload>(
                EventKind.ActorCellEntered, _sink.AppendedEvents[0].Payload);

            Assert.AreEqual(new WorldPos(-1, 0, -1), contractCell,
                "契约 Cell = FloorToInt(-0.5) = -1(禁 GetCell 的 RoundToInt = 0)");
            Assert.AreEqual(payload.Cell, contractCell,
                "契约 Cell 与事件载荷 Cell 同源(约束 ② 的判据本体)");
        }

        // ══════════ 判据 ②:位移真源唯一(Tick 委托 Move,零第二写入点) ══════════

        [Test]
        public void test_tick_delegatesToMove_singleWritePath()
        {
            // 判据 ②「不新增第二个位移真源」的机械面:Tick 的 IL 体对 Move 的调用是
            // 唯一通路(委托,不是旁路重写);ILBodyScanner 惯例承 input-system story-001
            // 的先例(本目录既有扫描器,非事后 grep)。
            var tick = typeof(PlayerControllerType).GetMethod("Tick");
            Assert.IsNotNull(tick, "前提:PlayerController.Tick 存在");
            Assert.IsTrue(ILBodyScanner.ContainsMethodCall(tick, "Move"),
                "Tick 体内须调用 Move(委托同一位移真源 —— 禁旁路重写 controller.Move)");

            // 行为面(同对象):Tick 段继续产生位移 —— 若 Tick 是 no-op 或旁路写入,
            // 表现会不同;委托路径下 Tick 与 Move 对同一 CharacterController 逐段累进。
            var pc = CreateController();
            pc.Move(new Vector3(1f, 0f, 0f));
            float afterMove = pc.transform.position.x;
            pc.Tick(0.016f, new MoveInput(new Vector3(1f, 0f, 0f)));
            float afterTick = pc.transform.position.x;

            Assert.Greater(afterMove, 0f, "前提:Move 路径产生位移");
            Assert.Greater(afterTick, afterMove, "Tick 段继续产生位移(委托 Move 的行为证据)");
        }

        [Test]
        public void test_motor_position_readonlyView_matchesTransform()
        {
            // Position = 表现态只读视图(ADR-020 §四:连续位置永不写流)。
            var pc = CreateController();
            pc.Teleport(new Vector3(2f, 1f, 3f));

            Assert.AreEqual(pc.transform.position, pc.Position,
                "Position 就是 transform.position 的只读投影");
        }

        // ══════════ 夹具(本文件自带 —— 各测试文件的 Fake 均为 private 嵌套,不共享) ══════════

        private sealed class FakeEventSink : IEventSink
        {
            public readonly List<SimEvent> AppendedEvents = new List<SimEvent>();
            public void Append(in SimEvent e) => AppendedEvents.Add(e);
        }

        private sealed class FakeTickProvider : ITickProvider
        {
            private readonly long _currentTick;
            public FakeTickProvider(long initialTick) { _currentTick = initialTick; }
            public long CurrentTick => _currentTick;
        }
    }
}
