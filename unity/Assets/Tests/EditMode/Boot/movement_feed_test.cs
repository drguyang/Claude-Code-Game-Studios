// M2 接线轮阶段 1 尾 · 输入 → PlayerController 采样 → 跨格事件运行期可达(EditMode)
//
// 覆盖三条(全部经**生产喂入方法** <see cref="MovementFeed.PumpFrame"/> 驱动**真装配袋**
// <c>CompositionRoot.Assemble()</c> —— 照 composition_root_test 的装配模式):
//   ① 跨格 ⇒ 恰增 1 条 ActorCellEntered,载荷经 IPayloadEncoder 真入池、可解码,
//      actorId = 装配时 id、格与位移方向一致、tick = 提交时的 CurrentTick;
//   ② 原地 / 小位移同格 ⇒ 零新增(不产噪声);
//   ③ 斜向轴(‖·‖ = √2 > 1)喂入 ⇒ AC-1-09 不抛、且对角跨格**单事件**(AC-1-13)。
//
// 为何 EditMode 能跑真物理(2026-10-09 勘察实证,见任务报告):
//   · Time.deltaTime 在 batch EditMode ≈ 0.333 s(= maximumDeltaTime 钳位)⇒ Move 有位移;
//   · CharacterController.Move 在 EditMode 立即生效(实测位移逐帧变化);
//   · Awake 在 EditMode **不**自动跑 ⇒ PlayerController.Controller 懒绑定(2026-10-09)补足;
//   · 地面 = 测试自建 Cube + Physics.SyncTransforms ⇒ isGrounded 稳定,重力不制造假跨格。
//
// 确定性:tick 用**注入的 0.05 s/帧**(每帧恰 1 tick),不读墙钟;断言只比格 / 帧数关系,
//   不比绝对位移(位移幅度随 Time.deltaTime 而变,格关系对 0.0167 ≤ dt ≤ 0.333 均成立)。

using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Presentation.Player;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using PlayerControllerType = DaYiJingCheng.Gameplay.Presentation.Player.PlayerController;

namespace DaYiJingCheng.Tests.Boot
{
    /// <summary><see cref="MovementFeed"/> / <see cref="MovementInputReader"/> 生产喂入链测试。</summary>
    public class MovementFeedTest
    {
        /// <summary>每帧注入的逻辑时间 = TICK_SECONDS(OQ-25-8)⇒ 每帧恰 1 个 tick 边沿。</summary>
        private const double FrameSeconds = 0.05;

        /// <summary>跨格循环帧上限(≈ 3 s 逻辑时间;小 dt 环境 ~20 帧内必跨)。</summary>
        private const int MaxCrossFrames = 60;

        private GameObject _ground;
        private GameObject _playerGo;
        private PlayerControllerType _player;
        private CompositionRootServices _bag;
        private SimTickDriver _driver;
        private int _actorId;

        [SetUp]
        public void SetUp()
        {
            // 真装配袋(非 Fake):事件 → 真实 EventStream(Seq 发号 / 去重 / AC-15)、
            // 载荷 → 真 PayloadEncoder + InMemoryBlobPool
            _bag = CompositionRoot.Assemble();
            _driver = new SimTickDriver();
            // F4:先丢弃首个号 —— IdAuthority 初值 0,直接取号恒得 0 ⇒ 身份断言退化成
            // 「0 == 0」恒真(空转)。丢一个再取,actorId 恒 ≥ 1,断言才有区分力。
            _bag.IdAuthority.NextPatientId();
            _actorId = _bag.IdAuthority.NextPatientId().Value;   // 非零 id 防 0==0 恒真
            Assert.Greater(_actorId, 0,
                "F4:actorId 必须非零 —— 若有人去掉上面的丢号步,身份断言会退化成 0==0 恒真");

            // 地面:无它则 isGrounded 恒 false,重力把 y 拖出格 ⇒ 制造与移动无关的假跨格。
            // 顶面刻意取 y = 0.5(**非**整数格顶):玩家中心静置 ≈ 1.5,落在格 Y = 1 的中部 ——
            // 若顶面 = 0,静置中心 ≈ 1.0 恰骑在格边界上,浮点微动即可让格 Y 在 0/1 间翻转
            // ⇒ 偶发 ActorCellEntered 噪声(见任务报告「遗留」条)。
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.name = "MovementFeedTest_Ground";
            _ground.transform.position = new Vector3(0f, 0f, 0f);      // 顶面 y = 0.5
            _ground.transform.localScale = new Vector3(400f, 1f, 400f);
            Physics.SyncTransforms();

            _playerGo = new GameObject("MovementFeedTest_Player");
            // RequireComponent 自动补 CharacterController;EditMode 不跑 Awake ⇒ 懒绑定兜住
            _player = _playerGo.AddComponent<PlayerControllerType>();
            _player.Initialize(SimAuthorityMode.Host, _bag.EventSink, _driver, _bag.Encoder, _actorId);
            _player.Teleport(new Vector3(0.5f, 1.55f, 0.5f));   // 初始格 = (0, 1, 0)(底 0.55,离地 0.05)
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            ReleaseInjectedKeyboard();   // F8:清虚拟键盘状态,防污染其他测试
            if (_playerGo != null) Object.DestroyImmediate(_playerGo);
            if (_ground != null) Object.DestroyImmediate(_ground);
            _playerGo = null;
            _ground = null;
            _player = null;
            _bag = null;
            _driver = null;
        }

        /// <summary>
        /// 提交时刻的 <c>transform.position</c> 判格 —— 与生产**同源**
        /// (<c>CellTransitionDetector.CellFromPosition</c>),不自造第二套格算法。
        /// <para>F5:事件载荷 Cell 必须等于它 —— 守文件头「Move → 采样 → 边沿」次序契约:
        /// 正确次序下事件格 == 当帧位置格恒等;次序颠倒(采样 → 位移)时事件格 = 上一帧位置格,
        /// 而当帧位置已再移一格 ⇒ 断言红。</para>
        /// </summary>
        private static WorldPos CommitTimeCell(PlayerControllerType player)
            => CellTransitionDetector.CellFromPosition(player.transform.position);

        /// <summary>
        /// 静置两帧(零输入):首个 tick 边沿按 <c>OnTickEdge</c> 契约提交**初始格**
        /// (首样本必发);此后同格再采样 = 折返清 pending,不再发。
        /// 由此把「初始格提交」与「跨格」分开计量 —— 跨格的判据是**新增**条数。
        /// </summary>
        private WorldPos SettleAndGetStartCell()
        {
            MovementFeed.PumpFrame(_player, _driver, Vector2.zero, FrameSeconds);
            MovementFeed.PumpFrame(_player, _driver, Vector2.zero, FrameSeconds);

            Assert.AreEqual(1, _bag.Stream.Count,
                "静置后流内恰 1 条(初始格提交;同格再采样不得追加)");
            SimEvent initial = _bag.Stream.Events[_bag.Stream.Count - 1];
            Assert.AreEqual(EventKind.ActorCellEntered, initial.Kind);
            Assert.IsTrue(PayloadCodec.TryGetPayload(initial, _bag.BlobPool,
                                                      out ActorCellEnteredPayload initialPayload),
                "初始提交的载荷须经池 + codec 可解(真编码,非手搓伪引用)");
            Assert.AreEqual(_actorId, initialPayload.ActorId,
                "初始提交的 actorId = 装配时分配的 id(非零 id 防 0==0 恒真 —— F4)");
            return initialPayload.Cell;
        }

        // ══════════ ① 跨格 ⇒ 恰增 1 条 ActorCellEntered ══════════

        [Test]
        public void test_processMovementFrame_crossesCell_emitsExactlyOneActorCellEntered()
        {
            // Arrange:静置取初始格(x = 0.5 → 格 0;z = 0.5 → 格 0;y 落地 → 格 1)
            WorldPos start = SettleAndGetStartCell();
            Assert.AreEqual(0, start.X, "初始 x = 0.5 ⇒ 格 X = 0");
            Assert.AreEqual(1, start.Y, "初始 y = 1.55(地面 0.5 上方)⇒ 格 Y = 1");
            Assert.AreEqual(0, start.Z, "初始 z = 0.5 ⇒ 格 Z = 0");

            // Act:连续帧喂 +X,直到流内出现第二条(= 跨格那条)
            int frames = 0;
            for (; frames < MaxCrossFrames && _bag.Stream.Count < 2; frames++)
            {
                MovementFeed.PumpFrame(_player, _driver, Vector2.right, FrameSeconds);
            }

            // Assert:恰新增 1 条(不多不少 —— 不许一帧跳两格、不许重力另发一条)
            Assert.AreEqual(2, _bag.Stream.Count,
                $"跨格须恰增 1 条 ActorCellEntered(实跑 {frames} 帧)");
            Assert.Less(frames, MaxCrossFrames, "60 帧内必须跨出初始格 —— 否则喂入链断了");

            SimEvent evt = _bag.Stream.Events[_bag.Stream.Count - 1];
            Assert.AreEqual(EventKind.ActorCellEntered, evt.Kind);
            Assert.GreaterOrEqual(evt.Seq, 0L, "Seq 由 EventStream 发号(O-1 哨兵 -1 已消费)");

            // 载荷:真经 IPayloadEncoder 入池(ADR-029 乙案),可解码 round-trip
            Assert.IsTrue(_bag.BlobPool.TryGetBlob(evt.Payload.BlobId, out var blob),
                "BlobId 须在池内命中(载荷真入池)");
            Assert.Greater(blob.Length, 0, "池内字节非空");
            Assert.IsTrue(PayloadCodec.TryGetPayload(evt, _bag.BlobPool,
                                                      out ActorCellEnteredPayload payload),
                "ActorCellEntered 载荷须能经池 + codec 解出");

            // 身份 / tick / 方向(非零 id 防 0==0 恒真 —— F4:初值 0 直接取号会让本断言空转)
            Assert.AreEqual(_actorId, payload.ActorId, "载荷 actorId = 装配时 id");
            Assert.GreaterOrEqual(payload.Tick, 0L, "载荷 tick ≥ 0");
            Assert.AreEqual(_driver.CurrentTick, payload.Tick,
                "载荷 tick = 提交那次边沿的 CurrentTick(循环已停,两者必然相等)");
            Assert.AreEqual(start.X + 1, payload.Cell.X, "+X 跨格 ⇒ 格 X 恰 +1(不跳格)");
            Assert.AreEqual(start.Y, payload.Cell.Y, "地面行走 ⇒ 格 Y 不变(重力不制造假跨格)");
            Assert.AreEqual(start.Z, payload.Cell.Z, "纯 +X ⇒ 格 Z 不变");

            // F5:事件格 = 提交时刻位置判出的格 —— 守文件头「Move → 采样 → 边沿」次序契约
            Assert.AreEqual(CommitTimeCell(_player), payload.Cell,
                "事件载荷 Cell 必须等于提交时刻 player.transform.position 判出的格 —— " +
                "次序若被改成「采样 → 位移」,事件格将停在上一帧位置格,当帧已再移一格 ⇒ 此断言红");
        }

        // ══════════ ② 原地 / 小位移 ⇒ 零新增 ══════════

        [Test]
        public void test_processMovementFrame_stayingInCell_emitsNoNewEvent()
        {
            // Arrange
            SettleAndGetStartCell();
            Assert.AreEqual(1, _bag.Stream.Count, "静置基线 = 1 条初始提交");

            // Act:原地 8 帧 + 小位移 8 帧(轴 0.02 ⇒ 目标速 0.1 m/s,行程远小于半格)
            for (int i = 0; i < 8; i++)
                MovementFeed.PumpFrame(_player, _driver, Vector2.zero, FrameSeconds);
            for (int i = 0; i < 8; i++)
                MovementFeed.PumpFrame(_player, _driver, new Vector2(0.02f, 0f), FrameSeconds);

            // Assert:零新增(同格反复采样不产噪声)
            Assert.AreEqual(1, _bag.Stream.Count,
                "同格内原地 / 小位移采样不得产生任何新事件(不产噪声)");
        }

        // ══════════ ③ 斜向轴归一 + 对角单事件(AC-1-09 / AC-1-13) ══════════

        [Test]
        public void test_processMovementFrame_diagonalAxis_normalizedAndSingleEvent()
        {
            // Arrange
            WorldPos start = SettleAndGetStartCell();

            // Act:喂**原始**对角轴(‖·‖ = √2 > 1)—— 若喂入层不归一,Move 的 AC-1-09 硬断言
            // 会在第一帧抛 ArgumentException,本测直接红
            int frames = 0;
            for (; frames < MaxCrossFrames && _bag.Stream.Count < 2; frames++)
            {
                MovementFeed.PumpFrame(_player, _driver, Vector2.one, FrameSeconds);
            }

            // Assert:恰增 1 条,且对角同时越界 ⇒ 一格事件载 X 与 Z 各 +1(AC-1-13)
            Assert.AreEqual(2, _bag.Stream.Count,
                $"对角跨格须恰增 1 条(实跑 {frames} 帧)");
            Assert.Less(frames, MaxCrossFrames, "60 帧内必须跨出初始格");

            SimEvent evt = _bag.Stream.Events[_bag.Stream.Count - 1];
            Assert.IsTrue(PayloadCodec.TryGetPayload(evt, _bag.BlobPool,
                                                      out ActorCellEnteredPayload payload));
            Assert.AreEqual(_actorId, payload.ActorId, "非零 id 防 0==0 恒真(F4)");
            Assert.AreEqual(start.X + 1, payload.Cell.X, "对角 ⇒ X 同帧 +1");
            Assert.AreEqual(start.Z + 1, payload.Cell.Z, "对角 ⇒ Z 同帧 +1(单事件含两轴)");
            Assert.AreEqual(start.Y, payload.Cell.Y, "地面行走 ⇒ Y 不变");
            Assert.AreEqual(_driver.CurrentTick, payload.Tick);

            // F5:事件格 = 提交时刻位置判出的格 —— 守文件头「Move → 采样 → 边沿」次序契约
            Assert.AreEqual(CommitTimeCell(_player), payload.Cell,
                "对角跨格事件的 Cell 同样必须等于提交时刻 transform.position 判出的格 —— " +
                "次序颠倒(采样 → 位移)时该格停在上一帧位置 ⇒ 此断言红");
        }

        // ══════════ ④ 喂入轴恒在 AC-1-09 前提内(生产读取面) ══════════

        [Test]
        public void test_toMoveInput_andReader_axisAlwaysWithinUnitBound()
        {
            // 归一形状:对角压到单位圆内、轻推保留、零 / 非有限归零、恒在水平面
            Vector3 diag = MovementFeed.ToMoveInput(new Vector2(1f, 1f));
            Assert.LessOrEqual(diag.magnitude, 1f + 1e-5f, "对角须径向归一(否则斜走更快)");
            PlayerControllerType.ValidateMoveInput(diag);   // AC-1-09 不抛即过
            Assert.AreEqual(0f, diag.y, "移动入参恒在水平面(重力归控制器)");

            Vector3 light = MovementFeed.ToMoveInput(new Vector2(0.3f, 0f));
            Assert.AreEqual(0.3f, light.x, 1e-6f, "模长 ≤ 1 的轻推原样通过(不放大不削平)");

            Assert.AreEqual(Vector3.zero, MovementFeed.ToMoveInput(Vector2.zero));
            Assert.AreEqual(Vector3.zero, MovementFeed.ToMoveInput(new Vector2(float.NaN, 1f)),
                "NaN 轴归零(F-3.1 ④ 同口径 —— 不许污染速度链)");
            Assert.AreEqual(Vector3.zero,
                MovementFeed.ToMoveInput(new Vector2(float.PositiveInfinity, 1f)),
                "∞ 轴归零(F6 · 与 NaN 同口径 —— 不许污染速度链)");

            // 生产读取面(无键入面:读到什么都是合法值):恒有限、恒 ≤ 1
            Vector2 read = MovementInputReader.ReadMoveAxis();
            Assert.IsFalse(float.IsNaN(read.x) || float.IsNaN(read.y)
                           || float.IsInfinity(read.x) || float.IsInfinity(read.y),
                "读取面不得返回非有限轴");
            Assert.LessOrEqual(read.magnitude, 1f + 1e-5f, "读取面输出须在单位圆内");
            PlayerControllerType.ValidateMoveInput(MovementFeed.ToMoveInput(read));

            // F8:虚拟设备注入 —— 队列 W 按下 + 手动 Update,使读取面断言非空转
            InjectKeyboardW();
            Vector2 readW = MovementInputReader.ReadMoveAxis();
            Assert.IsFalse(float.IsNaN(readW.x) || float.IsNaN(readW.y)
                           || float.IsInfinity(readW.x) || float.IsInfinity(readW.y),
                "注入按键后读取面仍不得返回非有限轴");
            Assert.Greater(readW.magnitude, 0f,
                "虚拟 W 按下 ⇒ ReadMoveAxis 必须读到非零轴(读取面断言非空转)");
            Assert.LessOrEqual(readW.magnitude, 1f + 1e-5f,
                "注入 W(单键)输出仍在单位圆内");
            PlayerControllerType.ValidateMoveInput(MovementFeed.ToMoveInput(readW));
        }

        // ══════════ ⑤ 落地后水平位移受配置约束(F7 速度修复回归) ══════════

        /// <summary>
        /// F7 变异回归:自由落体落地后的**一帧小推杆**,水平位移必须
        /// = min(vTarget, Accel · dt) · dt(当前水平速 = 0 的新速)。
        /// <para>旧式速度记账(以含 v.y 的 ‖_velocity‖ 当当前速 + 水平分量不回写 +
        /// 落地不清负 y)会拿自由落体累积的 |v.y| 残值(≈ 20+)当当前速 ⇒ 一帧水平冲出
        /// 数米,远超本断言上限 ⇒ 本测恰红。新式:当前速 = 水平分量(0)⇒ 按 F-1-3 从 0 起加速。</para>
        /// </summary>
        [Test]
        public void test_move_afterFreeFall_horizontalDisplacementBoundedByConfig()
        {
            // Arrange:先落地位于格中(SetUp 已是),再抬到高空自由落体一次,
            // 让 _velocity.y 累积出大残值(旧式 bug 的触发前提)。
            SettleAndGetStartCell();
            _player.Teleport(new Vector3(0.5f, 30f, 0.5f));
            Physics.SyncTransforms();

            bool nearGround = false;
            float lastY = float.MaxValue;
            int guard = 0;
            while (guard++ < 400)
            {
                MovementFeed.PumpFrame(_player, _driver, Vector2.zero, FrameSeconds);
                float y = _player.transform.position.y;
                if (!nearGround)
                {
                    if (y < 3f) nearGround = true;
                    lastY = y;
                    continue;
                }
                if (y >= lastY - 1e-4f) break;   // 不再下降 = 已落地静置
                lastY = y;
            }
            Assert.Less(guard, 400, "400 帧内必须落回地面并静置");
            Assert.IsTrue(nearGround && _player.transform.position.y < 3f,
                "落地位于地面附近(顶面 0.5 ⇒ 静置中心 ≈ 1.5)");

            // Act:落地后一帧小推杆(轴 0.5),量**水平位移差分**
            float dt = Time.deltaTime;
            Assert.Greater(dt, 0f, "Time.deltaTime 须为正(否则本测无意义)");
            Vector3 before = _player.transform.position;
            MovementFeed.PumpFrame(_player, _driver, new Vector2(0.5f, 0f), FrameSeconds);
            Vector3 after = _player.transform.position;

            // Assert:预期 = 当前水平速 0 起,按 F-1-3 线性趋近一帧后的速度 × dt
            float vTarget = 5f * 0.5f;                              // F-1-2: SPEED_MODE × ‖MoveInput‖
            float expectedSpeed = Mathf.Min(vTarget, 10f * dt);     // F-1-3: Accel = 10,当前速 = 0
            float expectedDx = expectedSpeed * dt;
            float dx = after.x - before.x;

            Assert.Greater(expectedDx, 0f, "预期有正向位移");
            Assert.AreEqual(expectedDx, dx, expectedDx * 0.1f + 1e-4f,
                $"落地后一帧水平位移须 ≈ 新速·dt = {expectedDx:F4}(dt = {dt:F4})—— " +
                "旧式拿 |v.y| 残值当当前速会数倍于此(落地冲) ⇒ 红");
            Assert.AreEqual(before.z, after.z, 1e-3f, "纯 +X 推杆 ⇒ Z 不动");
        }

        // ══════════ F8 · 读取面虚拟设备注入 ══════════

        private Keyboard _injectedKeyboard;
        private bool _injectedKeyboardAdded;

        /// <summary>
        /// 队列一次「W 按下」的键盘状态并手动 <c>InputSystem.Update()</c>,
        /// 使 <c>Keyboard.current</c> 能读到按下 —— 读取面断言就此非空转。
        /// </summary>
        private void InjectKeyboardW()
        {
            _injectedKeyboard = Keyboard.current;
            if (_injectedKeyboard == null)
            {
                _injectedKeyboard = InputSystem.AddDevice<Keyboard>();
                _injectedKeyboardAdded = true;
            }
            InputSystem.QueueStateEvent(_injectedKeyboard, new KeyboardState(Key.W));
            InputSystem.Update();
        }

        /// <summary>清虚拟键盘状态并(若由本测试创建)移除设备 —— 防污染其他测试。</summary>
        private void ReleaseInjectedKeyboard()
        {
            if (_injectedKeyboard == null) return;
            try
            {
                if (_injectedKeyboard.added)
                {
                    InputSystem.QueueStateEvent(_injectedKeyboard, new KeyboardState());
                    InputSystem.Update();
                }
                if (_injectedKeyboardAdded)
                    InputSystem.RemoveDevice(_injectedKeyboard);
            }
            catch (System.Exception)
            {
                // 清理失败不得遮蔽测试本体的判定(设备残留只会让读取面多读到一个空键盘)
            }
            _injectedKeyboard = null;
            _injectedKeyboardAdded = false;
        }
    }
}
