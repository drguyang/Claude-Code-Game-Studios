// story-009 · 急救直读通道运行期装配驱动(2026-10-14)
//
// 权威来源:
//   production/epics/emergency-procedures/story-009(R-9.2 驱动权 / R-9.3 Idle 门 / 规则六之甲中止)
//   emergency-procedures.md 规则六之甲(Armed 内持续无新边沿 ≥ ABORT_IDLE_TICKS ⇒ 中止不发)
//   input-system.md 规则七(直读通道独立于 42 UI 栈)
//   ADR-011 §二(直读 = onAfterUpdate 回调,非轮询)· ADR-013 Amendment A(IModalState / IArmedState
//     模态开集只读契约)
//   2026-10-14 用户裁定:「F 键 = 按住 = 一次动作」—— 按下沿 Arm、松开沿 EndAction 聚合上行;
//     「顺带把 IArmedState 生产实现补上」—— 本类即该实现(结清 ModalGate 方向①的零实现者空挂;
//     ModalGate 实例自身的生产接线归 4 交互轮,不在本 story)
//
// 设计口径:
//   - 本驱动 = **相位搬运工**(R-9.2):EmergencyDirectReadChannel(3 的通道态状态机)的
//     Arm / End / Abort 转译 + 聚合交出物(AggregatedEmergency)到判域形状的边界映射
//     (落 EmergencyManualDriver.TryAggregated)+ Idle 门(候选列表可见性回调)。
//   - **不构造 SimEvent / 不写流** —— 写者恒 EmergencyManualDriver → HostEmergencyProcessor
//     (ADR-005 主机唯一 Append;本类零 IEventSink 依赖)。
//   - 中止判据(R-9.3 + 规则六之甲 · ⚠️ 2026-10-14 评审 F-1 修订):**采样活跃度看门狗** ——
//     Armed 期 `lastActiveTick` 随每次成功 Feed 推进;`tick - lastActiveTick >= AbortIdleTicks`
//     (<see cref="ModalPhaseEvaluator.AbortIdleTicks"/> = 50,合成值,真值归数值轮)⇒ 中止零事件。
//     ⚠️ 判据为何不能是「距 Arm 的时长」:按住语义(裁定②)下首 press 沿后**恒无新边沿**,
//     GDD :313「持续不产生任何 Emergency 边沿」对按住手势结构性失配 —— 原实现会把任何
//     ≥ 2.5 s 的合法长按在 tick 边沿误杀(零事件),松开沿 EndAction 已回 Idle ⇒ no-op,
//     玩家按住 3 秒 = 什么都没发生(评审 F-1 实测推演)。活跃度 = Feed 成功(接线模式
//     onAfterUpdate 每 Armed 帧喂 ⇒ 长按永不被误杀);看门狗只拦「采样真停 ≥ 50 tick」
//     (设备断连 / 回调死 —— Suspended 入口归 story-008 前的兜底)。
//     检查点 = tick 边沿(BootRoot 的 tick 边沿序列内 OnTickEdge),不由渲染帧驱动。
//     ⚠️ **2026-11-07 订正(原文不删)**:上一段说「接线模式 onAfterUpdate 每 Armed 帧喂」——
//     **喂的是通道自己,不是本类的 `Feed`** ⇒ `lastActiveTick` 在真机上从不推进,按住满
//     50 tick 必被中止(零事件)。现改为 `Attach` 订阅通道的 `Sampled` 事件推进基准
//     (夹具侧 `FeedForTest` 那条缝不变)。真启动序判据见
//     `Tests/PlayMode/world_gamepad_path_test.cs` 的 `test_wg_03`。
//   - Idle 门(R-9.3):Arm ⇒ 候选列表隐藏(onIdleGate(false));End / Abort ⇒ 复现
//     (onIdleGate(true))。回调由组合根注入指向 EmergencyCandidateRuntime.SetVisible
//     —— 本类不引 Gameplay.UI(可见性是回调,不是依赖)。
//   - 档位意图(story-010 · 2026-11-02):动作开始 ⇒ Treatment;结束(含零样本)/ 中止 / 跳过
//     ⇒ Explore。`requesterId` 恒 = RequesterId(10),经后置绑定的 Func<ICameraRig> 发出。
//     ⚠️ 只发**意图**,不读回 rig.Mode(ADR-020 §五)。

using System;
using DaYiJingCheng.Gameplay.Input;
using DaYiJingCheng.Gameplay.Input.Intents;
using DaYiJingCheng.Gameplay.Interaction;   // IArmedState(ADR-013 Amendment A;命名空间住 Interaction)
using DaYiJingCheng.Gameplay.Presentation.Camera;   // ICameraRig / CameraMode(story-010 档位意图)
using DaYiJingCheng.Gameplay.Presentation.Player;   // MotorLease / LeaseSource(story-011 移动压制)
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.EmergencyProcedures;
using EmergencyReading = DaYiJingCheng.Gameplay.Input.Intents.EmergencyReading;   // 与 Sim.Contracts 同名 —— Feed 缝只收 3 侧读数

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>
    /// 急救直读通道运行期装配驱动 —— 通道相位(Idle/Armed)的搬运与聚合交出物的边界映射。
    /// <para>实现 <see cref="IArmedState"/>(ADR-013 Amendment A 模态开集只读契约;
    /// story-009 用户裁定一并实现,结清零生产实现者空挂)。</para>
    /// </summary>
    public sealed class EmergencyChannelDriver : IArmedState
    {
        /// <summary>
        /// **10 的请求方系统号**(档位意图的 `requesterId`;story-010 R-10.3)。
        /// <para>不是配置项,是**身份**:甲案「同一请求方可降档」的判据靠它认出「谁开的这个档」
        /// (`camera-and-viewpoint.md:256`「只能由请求方退出」)。传错号 ⇒ 退出语义**静默失效**
        /// (不崩、不报错,相机卡在近景)⇒ 因此三处调用点共用本常量,不各写字面量。</para>
        /// <para>出处:`systems-index.md` 的系统编号 10 = 急救动作。</para>
        /// </summary>
        public const int RequesterId = 10;

        private readonly EmergencyDirectReadChannel _channel;
        private readonly EmergencyManualDriver _manual;
        private readonly Func<long> _currentTick;
        private readonly Action<bool> _onIdleGate;

        /// <summary>
        /// 相机机位的**懒访问器**(story-010 R-10.2;后置绑定,null = 不发档位意图)。
        /// <para>为什么是 `Func` 而不是 `ICameraRig` 实例:本类在 `CompositionRoot.Assemble`
        /// 内构造,而相机在 `BootRoot` 启动序第 5 步才建 ⇒ 装配期拿不到;且拆卸段会把
        /// `BootRoot._cameraRig` 置 null,闭包可能在下一帧才求值 ⇒ 直取会 NRE
        /// (2026-11-01 已有 94 条 PlayMode 红的先例)。</para>
        /// </summary>
        private Func<ICameraRig> _cameraRig;

        /// <summary>
        /// 1 的移动压制租约(story-011 R-11.2;**直绑引用**,不包装成 `Func`)。
        /// <para>与 <see cref="BindCameraRig"/> 的形状**刻意不同**:机位会被重建 / 拆卸置 null ⇒ 必须懒求值;
        /// 而租约是 `PlayerController` 的字段,生命周期与玩家一致,不会被换掉。</para>
        /// <para>null = 未绑定 ⇒ 不置位/不清位(逻辑层 / EditMode),不抛。</para>
        /// </summary>
        private MotorLease _motorLease;

        /// <summary>本次 Armed 的目标病人(Arm 时锁定;None = 未预约过)。</summary>
        private PatientId _target = PatientId.None;

        /// <summary>Armed 起点的逻辑 tick(Idle 态无意义;观测面)。</summary>
        private long _armedTick;

        /// <summary>最近一次「采样活跃」的逻辑 tick(中止看门狗基准;评审 F-1 修订 ——
        /// 按住语义下「边沿活跃」不可用,「采样活跃」才是输入存活的正确代理)。
        /// <para>🔁 <b>2026-11-07(真机缺陷修)</b>:本字段此前只在 <see cref="Feed"/> /
        /// <see cref="FeedForTest"/> 里推进 —— 而**接线形态下采样根本不经过这两个方法**:
        /// 通道的 `OnAfterUpdate` 直接喂通道自身。<see cref="Attach"/> 由此改为订阅通道的
        /// <c>Sampled</c> 事件,每笔被接收的样本把基准推到这个 tick。夹具看不出来,是因为
        /// 既有两条夹具都走 `FeedForTest`(那条缝经过本类)。</para></summary>
        private long _lastActiveTick;

        /// <summary>通道采样回调(<see cref="Attach"/> 订阅 / <see cref="Detach"/> 退订)。</summary>
        private void OnChannelSampled()
        {
            // 接线形态下这是**唯一**的活跃来源:通道每接收一笔样本就推一次基准。
            // ⚠️ 回调内不抛(通道的 `Feed` 可能在 `InputSystem.onAfterUpdate` 里被调)。
            _lastActiveTick = _currentTick();
        }

        /// <param name="channel">3 的直读通道(组合根装配;逻辑层 / 接线模式均合法)。</param>
        /// <param name="manual">手动入口驱动(聚合上行与 Skip 共用的 Process 链宿主)。</param>
        /// <param name="currentTick">当前逻辑 tick 读数(armedTick 基准 + EndAction 事件 tick)。</param>
        /// <param name="onIdleGate">Idle 门回调(true = Idle 态 ⇒ 候选列表复现;
        /// false = 非 Idle ⇒ 隐藏)。null = 无门(测试 / 逻辑层)。</param>
        /// <exception cref="ArgumentNullException">channel / manual / currentTick 为 null。</exception>
        public EmergencyChannelDriver(EmergencyDirectReadChannel channel, EmergencyManualDriver manual,
                                      Func<long> currentTick, Action<bool> onIdleGate = null)
        {
            _channel = channel ?? throw new ArgumentNullException(nameof(channel));
            _manual = manual ?? throw new ArgumentNullException(nameof(manual));
            _currentTick = currentTick ?? throw new ArgumentNullException(nameof(currentTick));
            _onIdleGate = onIdleGate;
        }

        /// <summary>通道当前态(3 拥有;观测面)。</summary>
        public DirectChannelState State => _channel.State;

        /// <summary><see cref="IArmedState"/> 实现 —— 是否处于 Armed(急救动作进行中)。</summary>
        public bool IsArmed => _channel.State == DirectChannelState.Armed;

        /// <summary>已累计采样计数(行为仪表,透传通道)。</summary>
        public int SampleCount => _channel.SampleCount;

        /// <summary>Emergency 动作 enabled 状态(AC-3-E4 观测面,透传通道)。</summary>
        public bool EmergencyActionEnabled => _channel.EmergencyActionEnabled;

        /// <summary>是否已挂 onAfterUpdate(透传通道)。</summary>
        public bool IsAttached => _channel.IsAttached;

        /// <summary>本次(或最近一次)Armed 的目标病人(观测面 / 测试缝)。</summary>
        public PatientId Target => _target;

        /// <summary>
        /// 相机机位**是否已绑定**(观测面 / story-010 S10-06b 的守卫)。
        /// <para>⚠️ 「没绑」是一条**静默失败通道**:没人调 <see cref="BindCameraRig"/> 就永远不发
        /// `Treatment`,不崩也不报错 —— 正是本类要消灭的那种失败。生产启动序跑完后本属性恒 `true`
        /// 由 PlayMode 夹具断言(删掉 `BootRoot` 的绑定调用 ⇒ 那条断言红,其余不变)。</para>
        /// </summary>
        public bool HasCameraRig => _cameraRig != null;

        /// <summary>
        /// 移动压制租约**是否已绑定**(story-011 S11-06 的守卫)。
        /// <para>⚠️ 与 `HasCameraRig` 同义:漏绑不崩不报错,只是玩家压不住 —— 那正是本类失败的唯一形态,
        /// 只能靠生产启动序断言守。</para>
        /// </summary>
        public bool HasMotorLease => _motorLease != null;

        /// <summary>
        /// 后置绑定 1 的移动压制租约(story-011 R-11.2)。
        /// <para>⚠️ 必须是 <c>PlayerController.MotorLease</c> 那**一个**实例 —— 两份位图 = 两个真源。</para>
        /// </summary>
        /// <param name="lease">1 的租约;null = 解绑(之后不置位/不清位)。</param>
        public void BindMotorLease(MotorLease lease)
        {
            _motorLease = lease;
        }

        /// <summary>
        /// 置位 10 的压制(规则九「动作开始」;只碰 <c>LeaseSource.Emergency</c> 那一位)。
        /// <para>⚠️ 只允许在**动作真的开始之后**调用 —— 否则会留下一个没有动作的压制位,
        /// 而没有任何东西会去清它 ⇒ **玩家永远动不了**(S11-01 的负例专守这一点)。</para>
        /// </summary>
        private void AcquireMotorSuppression()
        {
            // ⚠️ 写面**不在本类** —— 必须经 `Gameplay.Presentation` 的写面(`AC-1-23` 的
            //    机械门按**调用点所在程序集**判白名单,而本类住装配层 `Gameplay.Boot`)。
            //    2026-11-02 实现轮实测:直接在这里调 `MotorLease.Acquire` 会被该门判红。
            if (_motorLease == null) return;
            EmergencyMotorSuppression.Set(_motorLease, acquire: true);
        }

        /// <summary>
        /// 清 10 的压制(规则九「结束清位」/ 规则六之甲「中止亦清」)。幂等:未持有 ⇒ no-op(位语义无下溢)。
        /// </summary>
        private void ReleaseMotorSuppression()
        {
            if (_motorLease == null) return;
            EmergencyMotorSuppression.Set(_motorLease, acquire: false);
        }

        /// <summary>
        /// 后置绑定相机机位访问器(story-010 R-10.2)。
        /// <para>与 <c>TeleportSink.BindPlayer</c> / <c>CasebookCommandRouter.SetAuthor</c> /
        /// <c>BuildController.BindOwner</c> **同一约定**:装配期拿不到的东西一律后置绑定,
        /// 不塞 `CompositionRoot.Assemble` 的参数表。</para>
        /// </summary>
        /// <param name="accessor">返回当前机位的访问器;null = 解绑(之后不发档位意图)。
        /// 允许返回 null(拆卸后 / 逻辑层)⇒ 该次请求静默跳过,不抛。</param>
        public void BindCameraRig(Func<ICameraRig> accessor)
        {
            _cameraRig = accessor;
        }

        /// <summary>
        /// 发一条档位意图(规则八;`requesterId` 恒 = <see cref="RequesterId"/>)。
        /// <para>⚠️ 这是**请求**不是**控制**:相机是否接受、如何转场由
        /// `CameraModeMachine.Settle` 决定(ADR-020 §五 / EC-2-9),本类**不读回** `Mode`。</para>
        /// <para>未绑定 / 访问器返回 null ⇒ 静默跳过(R-10.2)。</para>
        /// </summary>
        private void RequestCameraMode(CameraMode mode)
        {
            ICameraRig rig = _cameraRig?.Invoke();
            if (rig == null)
                return;
            // ⚠️ 必须双参且传真实系统号 —— 单参 / -1 会让甲案「同一请求方可降档」静默失效
            // (相机卡在近景),且机械门 CameraSetModeRequesterGate 判红。
            rig.SetMode(mode, RequesterId);
        }

        /// <summary>挂接通道 onAfterUpdate(透传;接线模式唯一,AC-9-01 fail-loud 由通道抛)。
        /// <para>🔁 2026-11-07:顺带订阅通道的 <c>Sampled</c> —— 接线形态下采样不经过本类的
        /// <see cref="Feed"/>,不订阅就推不动中止看门狗的基准(真机长按 50 tick 被误杀)。</para></summary>
        public void Attach()
        {
            _channel.Attach();                     // 先挂(重复挂会抛)⇒ 抛出的那一轮不留悬空订阅
            _channel.Sampled += OnChannelSampled;
        }

        /// <summary>卸下通道 onAfterUpdate(透传;幂等)。</summary>
        public void Detach()
        {
            _channel.Sampled -= OnChannelSampled;
            _channel.Detach();
        }

        /// <summary>
        /// 预约通道(Idle → Armed,候选列表隐藏)。非 Idle 态由通道自己抛(重复预约 = 编程错误)。
        /// </summary>
        /// <param name="actionOrdinal">急救动作 ordinal(OQ-10-6 表;真表归 21a/10 烘焙轮)。</param>
        /// <param name="target">本次动作的目标病人(Arm 时锁定)。</param>
        public void Arm(int actionOrdinal, PatientId target)
        {
            // ⚠️ 顺序 = 「先 Arm 成功、后发 Treatment」(story-010 R-10.1):`_channel.Arm` 在非 Idle 态
            //     会抛 `InvalidOperationException` ⇒ 抛出的那一轮**不得**留下一个没有动作的
            //     `Treatment` 请求(那会让相机进近景而动作根本不存在)。
            _channel.Arm(actionOrdinal);
            _target = target;
            _armedTick = _currentTick();
            _lastActiveTick = _armedTick;
            _onIdleGate?.Invoke(false);
            RequestCameraMode(CameraMode.Treatment);   // 规则八「动作开始」
            AcquireMotorSuppression();                 // 规则九「动作开始置位」(必须在 Arm 成功之后)
        }

        /// <summary>逻辑层 / 测试驱动缝:透传通道 Feed(成功 = 采样活跃,推看门狗基准)。</summary>
        public bool Feed(EmergencyReading reading)
        {
            bool accepted = _channel.Feed(reading);
            if (accepted)
                _lastActiveTick = _currentTick();
            return accepted;
        }

        /// <summary>测试驱动缝:透传通道 FeedForTest(成功 = 采样活跃,推看门狗基准)。</summary>
        public int FeedForTest(float axis, bool newlyPressed)
        {
            int count = _channel.FeedForTest(axis, newlyPressed);
            if (_channel.State == DirectChannelState.Armed)
                _lastActiveTick = _currentTick();
            return count;
        }

        /// <summary>
        /// 动作结束(松开沿,2026-10-14 用户裁定):聚合 → TryAggregated 上行 → Idle 门复现。
        /// </summary>
        /// <returns>true = 双事件已入流;false = 零样本签约(Idle / 无动作)或
        /// 在场前置未过 —— 均**零事件**(零提示纪律)。</returns>
        public bool EndAction(int actorId)
        {
            // ⚠️ `wasArmed` 必须在 `_channel.EndAction()` **之前**读 —— 它会把态置回 Idle。
            bool wasArmed = _channel.State != DirectChannelState.Idle;
            AggregatedEmergency? agg = _channel.EndAction();
            _onIdleGate?.Invoke(true);
            // ⚠️ story-010 R-10.1:**不得**以 `agg != null` 为条件 —— 零样本签约(`agg == null` ⇒
            //     零事件)是**合法**的一次动作起止(按住又松开但没产生幅度沿)。动作结束了,
            //     相机就必须退出近景。写成 `agg != null` 才是最常见的漏法(M4 变异专打这一点)。
            if (wasArmed)
                RequestCameraMode(CameraMode.Explore);   // 规则八「动作结束」
            if (wasArmed)
                ReleaseMotorSuppression();              // 规则九「结束清位」(零样本同样是结束)
            if (agg == null)
                return false;   // 零样本签约:一次按住周期零动作 ⇒ 零上行(F-10.5)
            return _manual.TryAggregated(agg.Value, actorId, _target, _currentTick()).Applied;
        }

        /// <summary>
        /// 动作中止(规则六之甲):丢弃累计、回 Idle、**零事件**(中止 ≠ 跳过)。
        /// </summary>
        /// <returns>true = 中止前确有动作在途;false = 本就 Idle(幂等 no-op)。</returns>
        public bool AbortAction()
        {
            bool wasArmed = _channel.State != DirectChannelState.Idle;
            _channel.AbortAction();
            // ⚠️ story-010 S10-04:`wasArmed` 此前**未用于门控** `_onIdleGate` —— Idle 态误触会
            //     无条件复现候选列表(无害但无意义)。本批把同一守卫补上,并让档位意图共用它:
            //     本就 Idle ⇒ 既不该动列表,也不该凭空发 `Explore`。
            if (wasArmed)
            {
                _onIdleGate?.Invoke(true);
                RequestCameraMode(CameraMode.Explore);   // 规则六之甲「中止亦发 Explore」
                ReleaseMotorSuppression();               // 规则六之甲「清 MotorSuppressed」
            }
            return wasArmed;
        }

        /// <summary>
        /// **跳过路径入口**(规则六;story-010 R-10.7 甲案 · 2026-11-02 用户裁定)。
        /// <para>⚠️ **为什么跳过要经过本类**:此前 `CasebookCommandRouter.SubmitEmergencySkip`
        /// 直连 `EmergencyManualDriver.TryManual(method: 1)`,**不经**本驱动 ⇒ 规则八
        /// 「跳过路径结束必发 `Explore`」在代码里**没有任何落点**。</para>
        /// <para>⚠️ **`Explore` 必须先于 `TryManual`**:反过来的话 `TryManual` 若抛 / 短路,
        /// 相机就留在近景 —— 正好制造本卡要修的病。</para>
        /// <para>⚠️ **诚实登记(乙案)**:现行 P0 的跳过入口只在 `Idle` 可达
        /// (`R-9.3` 的 Idle 门)⇒ 此处 `Explore` 是**防御性幂等请求**(相机本就在 `Explore`),
        /// **不构成 `AC-10-11` 的可失败判据**。见 `story-010` §R-10.7 与 GDD 规则八的注。</para>
        /// </summary>
        /// <param name="target">目标病人(由路由侧 `_boundPatient` 给出)。</param>
        /// <param name="actorId">玩家 actor id(由路由侧后置注入)。</param>
        /// <returns>与 <see cref="EmergencyManualDriver.TryManual"/> 同义:`true` = 双事件已入流;
        /// `false` = 不在场 / 前置未过 ⇒ 零事件零提示(零提示纪律)。</returns>
        public bool SkipAction(PatientId target, int actorId)
        {
            RequestCameraMode(CameraMode.Explore);   // 规则八「跳过路径结束亦发」
            ReleaseMotorSuppression();               // 规则九「结束(含跳过)清位」—— 幂等,未持有即 no-op
            return _manual.TryManual(target, method: 1, actorId, _currentTick()).Applied;
        }

        /// <summary>
        /// tick 边沿中止检查(规则六之甲 · 评审 F-1 修订后的**采样活跃度看门狗**;由
        /// BootRoot 的 tick 边沿序列驱动,**非渲染帧**)。Armed 且距**最近采样** ≥
        /// <see cref="ModalPhaseEvaluator.AbortIdleTicks"/> ⇒ 中止(采样停 = 输入存活
        /// 代理失效;接线模式下每 Armed 帧都有样本 ⇒ 合法长按恒不被误杀)。
        /// </summary>
        public void OnTickEdge(long tick)
        {
            if (_channel.State != DirectChannelState.Armed)
                return;
            if (tick - _lastActiveTick >= ModalPhaseEvaluator.AbortIdleTicks)
                AbortAction();
        }
    }
}
