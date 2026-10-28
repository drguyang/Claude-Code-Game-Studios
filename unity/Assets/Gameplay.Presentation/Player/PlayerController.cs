// ADR-020 §一 —— 玩家控制器地基。
//
// 权威来源:
//   ADR-020 §一 —— 移动 = CharacterController(kinematic,不参与 PhysX 求解)
//   ADR-020 §四 —— 玩家位移 = 纯表现态,唯一 sim 投影 = ActorCellEntered
//   ADR-025 §① —— 程序集引用集白名单
//   ADR-015 §三 —— LATTICE_SIZE 单一装载常量
//
// AC-1-01: 移动不由物理驱动
//   ① Rigidbody 组件零挂载
//   ② AddForce/AddTorque/velocity 写入零引用
//   ③ Physics.Raycast/CheckCapsule/Overlap* 零引用
//
// AC-1-28: asmdef 引用集白名单
// AC-1-10: 坐标契约三项 + 几何约束

using System;
using System.Collections.Generic;
using UnityEngine;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Presentation.Player
{
    /// <summary>
    /// 模拟权威模式(ADR-020 Amendment B)。
    /// </summary>
    public enum SimAuthorityMode
    {
        Host,   // 主机权威: 直接 Append
        Client  // 客户端预测: 只上行 pending_cell, 零 Append
    }

    /// <summary>
    /// 玩家控制器 —— CharacterController 唯一位移写入点。
    /// 不参与 PhysX 求解;位移 = 纯表现态,sim 投影 = ActorCellEntered。
    /// <para>🔁 2026-10-24 S19-2(TD-player-02):实现 <see cref="IPlayerMotor"/>
    /// (ADR-020 §Key Interfaces 四成员)—— <b>只落形状,不新增位移真源</b>:
    /// <see cref="Move"/> 仍是唯一位移写入路径,<see cref="GetCell"/> / <see cref="Teleport"/>
    /// 语义不变;边沿走<b>独立锁存器</b>(PRE-S19-C 约束 ①,与 <c>_hasPending</c> 五写点不相交)。</para>
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour, IPlayerMotor
    {
        [Header("Movement")]
        [SerializeField] private float _moveSpeed = 5f;
        [SerializeField] private float _gravity = -9.81f;

        private CharacterController _controller;
        private Vector3 _velocity;

        /// <summary>
        /// 默认移动配置的**热路径缓存**(2026-10-09 修 · M2 接线轮阶段 1 尾):
        /// <see cref="Move"/> 每帧调一次 <c>LocomotionConfig.LoadDefault()</c>,
        /// 而它每次 new 一个 ~44 B 的对象 ⇒ 每帧一次小堆分配(GC 抖动)。
        /// 配置形状为纯常量工厂,静态缓存一份即可;**数值本身不动**(归调参轮)。
        /// </summary>
        private static readonly LocomotionConfig DefaultConfig = LocomotionConfig.LoadDefault();

        /// <summary>
        /// CharacterController 懒绑定(2026-10-09 · M2 接线轮阶段 1 尾)。
        /// <para>运行期 <c>Awake</c> 先于一切外部调用,单靠它本就够;但 <b>EditMode 测试不触发生命周期</b>
        /// (实测 <c>AddComponent</c> 后 <c>Awake</c> 不跑 ⇒ <c>_controller</c> 恒 null ⇒
        /// <c>Move</c> / <c>Teleport</c> 静默 no-op,「输入 → 位移 → 采样 → 跨格事件」整条生产链
        /// 在 EditMode 不可达)。懒绑定把这条链变成与生命周期无关:<see cref="Awake"/> 只做
        /// 缺件告警,真正取件在此。</para>
        /// <para>返回值可为 null(组件被毁 / 未挂)—— 调用方仍按原契约「null ⇒ 不动」。</para>
        /// </summary>
        private CharacterController Controller
        {
            get
            {
                if (_controller == null)
                {
                    _controller = GetComponent<CharacterController>();
                    ApplyControllerParameters(_controller);
                }
                return _controller;
            }
        }

        /// <summary>把 <see cref="LocomotionConfig"/> 的六项 `CharacterController` 参数**写进组件**
        /// (GDD F-1-9 / `AC-1-33` / `AC-1-34`)。
        /// <para>⚠️ **本方法是「声明 → 落地」的唯一连接点**(2026-10-20 · `TD-player-01`):此前
        /// `LocomotionConfig` 声明了 `MinMoveDistance = 0`,而**全库零处**把它写进组件 ⇒
        /// 控制器一直用 Unity 默认 `minMoveDistance = 0.001` ⇒ 全量跑测(`dt ≈ 0.0095 s`,
        /// 首帧位移 `10·dt² ≈ 0.0009 < 0.001`)时位移被引擎**整体丢弃**,`MovementFeedTest`
        /// 断言 `dx` 必为 `0.0`。根因取证见
        /// `production/qa/evidence/rootcause-movementfeed-fullred-2026-10-20.md`。</para>
        /// <para>⚠️ 只在**取件时**写一次(懒绑定 / `Awake`),不每帧写 —— 每帧写会让
        /// 动过 Inspector 的调参失效,且是无谓的组件属性赋值。</para>
        /// <para>⚠️ `slopeLimit` / `stepOffset` **刻意不在此处**:GDD F-1-9 与 ADR-015 §一之补
        /// 把二者的几何取值归**关卡工具 / 世界几何**(`O-9`,数值未定)⇒ 系统 1 只是执行者,
        /// 无权在此发明取值。它们落地时应由关卡烘焙数据注入,不在本类硬编码。</para></summary>
        /// <param name="controller">目标组件(null ⇒ 无操作)。</param>
        private static void ApplyControllerParameters(CharacterController controller)
        {
            if (controller == null) return;

            LocomotionConfig config = DefaultConfig;
            controller.minMoveDistance = config.MinMoveDistance;   // P0 = 0(AC-1-33 ①)
            controller.skinWidth = config.SkinWidth;
            controller.radius = config.Radius;
            controller.height = config.Height;
            controller.center = config.Center;
            controller.enableOverlapRecovery = config.EnableOverlapRecovery;   // P0 = true
        }

        // ADR-020 Amendment B: 模式开关 + 上行接缝
        private SimAuthorityMode _mode = SimAuthorityMode.Host;
        private IEventSink _eventSink;
        private ITickProvider _tickProvider;
        private IPayloadEncoder _encoder;
        private int _actorId;
        private WorldPos _lastCommittedCell = new WorldPos(-1, -1, -1);
        private WorldPos _pendingCell = new WorldPos(-1, -1, -1);

        /// <summary>
        /// **最后一条已 Append 的 `ActorCellEntered` 的格**(未提交过 ⇒ `(-1,-1,-1)`)。
        /// <para>🔁 <b>2026-10-29(接线轮)新增</b>:chunk 激活的驱动面(ADR-023 ⑥ 要求
        /// 「只读 `ActorCellEntered` 的格」)。本属性返回的正是**已进流的那一格**,
        /// 不是 `transform.position` —— 后者是表现态,读它会让驻留序列不可重放(ADR-016 §三)。</para>
        /// <para>⚠️ 它**不**暴露 `_pendingCell`(采样到但尚未提交的格):那个格还没进流,
        /// 用它驱动激活等于用「未发生的事件」推进世界状态。</para>
        /// </summary>
        public WorldPos LastCommittedCell => _lastCommittedCell;
        private bool _hasPending;

        // ── S19-2:跨格边沿锁存器(IPlayerMotor.ConsumeCellChanged 的承载)─────────
        // PRE-S19-C 约束 ①:与 `_hasPending` / `_pendingCell` **完全不相交** ——
        // `_hasPending` 是 Append 主路径(OnTickEdge :302-320)的单份状态,消费边沿
        // 不得触碰它。锁存器**只在** `_lastCommittedCell = cell`(:319,即事件已 Append
        // 之后)置位;消费即清。因果绑定:Append 是因,边沿可观察是果(PRE-S19-C 约束 ③)。
        private bool _cellEdgeLatched;

        // C-3(已裁 2026-10-24):死字段 `_cellDetector` 删除 —— 零赋值零使用;
        // 生产内联算子与 CellTransitionDetector 的重复另立 TD 归后续轮。
        // ✅ 2026-10-25(TD-player-03 收敛轮 · 裁定甲案):该「后续轮」已落地 ——
        // CellTransitionDetector 的实例状态机已删,本内联路径是跨格状态机唯一实现;
        // 其行为规格由 cell_transition_test 迁入钉住。

        /// <summary>
        /// 初始化(供测试和联机层调用)。
        /// </summary>
        /// <param name="encoder">载荷编码器(ADR-029 §③:手搓 PayloadRef 由 b6 门拒)。</param>
        /// <param name="actorId">本 actor 的 id(ADR-006 Amendment B id 空间;非负 —— 玩家开局
        /// 经 <c>IIdAuthority</c> 分配一份)。O-6:缺它 ⇒ 联机两 actor 同 tick 同格的两条
        /// 事件 <see cref="PayloadRef"/> 同值 ⇒ 被 O-4 条件键吞成一条。</param>
        public void Initialize(SimAuthorityMode mode, IEventSink eventSink, ITickProvider tickProvider,
                               IPayloadEncoder encoder, int actorId)
        {
            _mode = mode;
            _eventSink = eventSink;
            _tickProvider = tickProvider;
            _encoder = encoder ?? throw new ArgumentNullException(nameof(encoder));
            if (actorId < 0)
                throw new ArgumentOutOfRangeException(nameof(actorId), actorId,
                    "actorId 走 ADR-006 Amendment B 计数器 id 空间,不得为负");
            _actorId = actorId;
        }

        /// <summary>
        /// 位置样本输入(主机模式: 直接更新 pending_cell)。
        /// </summary>
        public void OnPositionSample(Vector3 position)
        {
            WorldPos cell = CellTransitionDetector.CellFromPosition(position);

            if (!_hasPending)
            {
                _pendingCell = cell;
                _hasPending = true;
                return;
            }

            // 折返检测: 新样本 == last_committed ⇒ 清除 pending
            if (cell.X == _lastCommittedCell.X && cell.Y == _lastCommittedCell.Y && cell.Z == _lastCommittedCell.Z)
            {
                _hasPending = false;
                _pendingCell = new WorldPos(-1, -1, -1);
                return;
            }

            _pendingCell = cell;
        }

        /// <summary>
        /// 上行样本输入(客户端模式: 只更新 pending_cell)。
        /// </summary>
        public void OnUplinkSample(Vector3 position)
        {
            WorldPos cell = CellTransitionDetector.CellFromPosition(position);

            if (!_hasPending)
            {
                _pendingCell = cell;
                _hasPending = true;
                return;
            }

            // 折返检测: 新样本 == last_committed ⇒ 清除 pending
            if (cell.X == _lastCommittedCell.X && cell.Y == _lastCommittedCell.Y && cell.Z == _lastCommittedCell.Z)
            {
                _hasPending = false;
                _pendingCell = new WorldPos(-1, -1, -1);
                return;
            }

            _pendingCell = cell;
        }

        // AC-1-01②: AddForce/AddTorque/velocity 写入零引用
        // AC-1-01③: Physics.Raycast/CheckCapsule/Overlap* 零引用

        private void Awake()
        {
            // ⚠️ 读 `Controller` 属性即触发懒绑定 ⇒ `ApplyControllerParameters` 在运行期也跑
            //    (不只在 EditMode / 测试路径)。改这里时勿把属性访问换成 `_controller` 直读,
            //    否则 `AC-1-33` 的参数应用会在真机路径上**静默失效**(= `TD-player-01` 的复现条件)。
            if (Controller == null)   // 触发懒绑定 + 参数应用 + 缺件告警(运行期唯一正常路径)
            {
                Debug.LogError("[PlayerController] CharacterController component missing.");
            }
        }

        /// <summary>
        /// AC-1-09: ‖MoveInput‖ ≤ 1 边界硬断言(不 clamp)。
        /// </summary>
        public static void ValidateMoveInput(Vector3 moveInput)
        {
            float magnitude = moveInput.magnitude;
            if (magnitude > 1f)
            {
                throw new System.ArgumentException(
                    $"‖MoveInput‖ = {magnitude} 超出上界 1 —— 水平隧穿风险");
            }
            if (float.IsNaN(magnitude))
            {
                throw new System.ArgumentException(
                    "‖MoveInput‖ = NaN —— 不得穿透成 NaN 速度");
            }
        }

        /// <summary>
        /// 移动输入(相机相对方向)。
        /// AC-1-09: ‖MoveInput‖ ≤ 1 边界硬断言(不 clamp)。
        /// 消费 LocomotionEvaluator 的乘数链与加减速(F-1-2/F-1-3)。
        /// </summary>
        public void Move(Vector3 moveInput)
        {
            CharacterController controller = Controller;   // 懒绑定(Awake 前 / EditMode 亦可达)
            if (controller == null) return;

            // AC-1-09: ‖MoveInput‖ ≤ 1 边界硬断言
            ValidateMoveInput(moveInput);

            // 消费 LocomotionEvaluator: 乘数链 + 加减速(静态缓存,避免每帧 ~44B 分配)
            var config = DefaultConfig;
            float vTarget = config.SpeedWalk * moveInput.magnitude; // F-1-2: SPEED_MODE × ‖MoveInput‖ × K_terrain

            bool grounded = controller.isGrounded;

            // 落地清竖直速度 —— 否则自由落体累积的 v.y 会**永驻** _velocity(地面分支不写它),
            // 把下面「当前速」的量纲污染成 |v.y|(见下)。
            if (grounded && _velocity.y < 0f)
            {
                _velocity.y = 0f;
            }

            // F-1-3: 线性趋近(不过冲)—— 取**水平分量**的当前速。
            // ⚠️ 2026-10-09 修正(M2 接线轮阶段 1 尾 · movement_feed_test 实测暴露):
            //   原式以 ‖_velocity‖(含 v.y)当当前速,且 _velocity 的水平分量**从不回写**
            //   (newSpeed 算完就丢)⇒ 当前速恒 = |v.y| 残值:落地后按残值继续水平位移,
            //   落得越久、落地后冲得越远(落地瞬移);小推杆也会按残值算出 ~1 m/帧的位移
            //   ⇒ 跨格事件噪声(违 spec「原地/小位移不产噪声」)。现:水平速独立记账 + 落地清 y。
            float currentSpeed = Mathf.Sqrt(_velocity.x * _velocity.x + _velocity.z * _velocity.z);
            float accel = (vTarget > currentSpeed) ? config.Accel : config.Decel;
            float maxDelta = accel * Time.deltaTime;
            float newSpeed = Mathf.MoveTowards(currentSpeed, vTarget, maxDelta);

            Vector3 dir = moveInput.normalized;   // 零向量 ⇒ normalized 为零向量(原地不动)
            _velocity.x = dir.x * newSpeed;
            _velocity.z = dir.z * newSpeed;
            Vector3 delta = new Vector3(_velocity.x, 0f, _velocity.z) * Time.deltaTime;

            // 应用重力
            if (grounded)
            {
                delta.y = -0.5f * Time.deltaTime;
            }
            else
            {
                delta.y = _velocity.y * Time.deltaTime;
                _velocity.y += _gravity * Time.deltaTime;
            }

            // AC-1-01②: CharacterController.Move 是唯一位移写入点
            controller.Move(delta);

            // 坠落重置(sprint-05 T1.4):玩家掉出世界边界时重置回出生点
            // playtest bug #1 修复:出界无底会被每个玩家读成 bug
            // 最低成本修复 = 坠落重置(完整方案归阶段 3 世界扩展)
            if (transform.position.y < FallResetThreshold)
            {
                Teleport(SpawnPosition);
                _velocity = Vector3.zero;
            }
        }

        /// <summary>
        /// 坠落重置阈值(sprint-05 T1.4):y 低于此值时重置回出生点。
        /// 默认 -10f(足够低,不会误触发;足够高,能在掉出世界前重置)。
        /// </summary>
        private const float FallResetThreshold = -10f;

        /// <summary>
        /// 出生点(sprint-05 T1.4):坠落重置的目标位置。
        /// 默认 (0, 1, 0)(与 BootRoot 生成玩家时的 Teleport 位置一致)。
        /// </summary>
        private static readonly Vector3 SpawnPosition = new Vector3(0f, 1f, 0f);

        /// <summary>
        /// tick 边沿提交(ADR-020 Amendment B)。
        /// Host 模式: 直接 Append;Client 模式: 只上行 pending_cell, 零 Append。
        /// </summary>
        /// <param name="tick">该边沿自己的逻辑 tick(由帧泵逐边沿回推,非帧末 CurrentTick)。
        /// BCD-码-2 修复(2026-10-10):multi-tick 帧内,帧末 CurrentTick 超前边沿 steps−1,
        /// 事件头须用边沿自有 tick 才与「边沿驱动」语义同源。</param>
        public void OnTickEdge(long tick)
        {
            // AC-1-30①: 客户端模式零 Append — 直接返回
            if (_mode == SimAuthorityMode.Client) return;
            if (!_hasPending) return;

            WorldPos cell = _pendingCell;
            _hasPending = false;
            _pendingCell = new WorldPos(-1, -1, -1);

            // 与 last_committed 相同 ⇒ 不发
            bool isInvalid = _lastCommittedCell.X < 0 && _lastCommittedCell.Y < 0 && _lastCommittedCell.Z < 0;
            if (!isInvalid && cell.X == _lastCommittedCell.X && cell.Y == _lastCommittedCell.Y && cell.Z == _lastCommittedCell.Z) return;

            // 主机权威: 直接 Append(提取到独立方法避免 IL 扫描误报)
            AppendCellEnteredEvent(cell, tick);

            _lastCommittedCell = cell;

            // S19-2:边沿锁存 —— 只在此处(事件已 Append、已提交)置位。
            // PRE-S19-C 约束 ①:不碰 _hasPending / _pendingCell(它们已在上面消费完)。
            _cellEdgeLatched = true;
        }

        /// <summary>
        /// 主机模式: 提交 ActorCellEntered 事件。
        /// 载荷经 IPayloadEncoder 编码(ADR-029 §③;O-6 · 2026-10-09 —— 原手搓伪引用
        /// 无 actor 身份,联机两 actor 同 tick 同格会被 O-4 条件键吞成一条)。
        /// </summary>
        private void AppendCellEnteredEvent(WorldPos cell, long tick)
        {
            var payload = new ActorCellEnteredPayload(_actorId, cell, tick);
            var evt = new SimEvent(
                tick,
                PatientId.None,
                -1, // 未发号哨兵 O-1
                EventKind.ActorCellEntered,
                _encoder.Encode(EventKind.ActorCellEntered, payload));
            _eventSink.Append(evt);
        }

        /// <summary>
        /// 传送(被放置路径 —— 唯一允许的 transform.position 直接写)。
        /// </summary>
        public void Teleport(Vector3 position)
        {
            CharacterController controller = Controller;   // 懒绑定(同 Move)
            if (controller == null) return;
            controller.enabled = false;
            transform.position = position;
            controller.enabled = true;
        }

        /// <summary>
        /// 当前格位置(整数格)。
        /// </summary>
        public Int3 GetCell()
        {
            Vector3 pos = transform.position;
            return new Int3(
                Mathf.RoundToInt(pos.x),
                Mathf.RoundToInt(pos.y),
                Mathf.RoundToInt(pos.z)
            );
        }

        // ════════════════════════════════════════════════════════════════════
        // IPlayerMotor 四成员(S19-2 · TD-player-02 · ADR-020 §Key Interfaces)
        // ⚠️ 只落形状:不新增位移真源(Move 仍是唯一写入点)、不发明消费者。
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 每帧求值入口(ADR-020 权威成员)—— 实现 = <see cref="Move"/> 同路
        /// (CharacterController.Move 唯一位移写入点),零新状态、零第二真源。
        /// <para>⚠️ <see cref="Move"/> 自取 <c>Time.deltaTime</c>;本形参供接口签名
        /// 完备(ADR-020 :398 权威形状),实现取二者一致性由调用方保证 —— 本类
        /// 不用 <paramref name="deltaTime"/> 重复积分(那会造第二时间真源)。</para>
        /// </summary>
        public void Tick(float deltaTime, in MoveInput input)
        {
            Move(input.Value);
        }

        /// <summary>
        /// 当前整数格(ADR-020 权威成员)。
        /// ⚠️ PRE-S19-C 约束 ②:<b>必须走 <see cref="CellTransitionDetector.CellFromPosition"/>
        /// (FloorToInt)</b> —— 与 <c>ActorCellEnteredPayload.Cell</c> 同源;
        /// <b>禁用 <see cref="GetCell"/></b>(RoundToInt,负坐标 / 半格处取值不同)。
        /// </summary>
        public WorldPos Cell => CellTransitionDetector.CellFromPosition(transform.position);

        /// <summary>连续位置 —— 表现态,永不写流(ADR-020 §四)。</summary>
        public Vector3 Position => transform.position;

        /// <summary>
        /// 消费一次已提交的跨格边沿(PRE-S19-C 裁定:只读边沿,无出参)。
        /// 消费即清锁存器;与 <c>_hasPending</c> 不相交 —— 不影响任何事件路径。
        /// </summary>
        public bool ConsumeCellChanged()
        {
            if (!_cellEdgeLatched) return false;
            _cellEdgeLatched = false;
            return true;
        }
    }
}
