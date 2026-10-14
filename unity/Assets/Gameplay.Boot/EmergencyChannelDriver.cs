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
//   - 中止判据(R-9.3 + 规则六之甲):Armed 起点起 `tick - armedTick >= AbortIdleTicks`
//     (<see cref="ModalPhaseEvaluator.AbortIdleTicks"/> = 50,合成值,真值归数值轮)⇒
//     AbortAction 零事件。按住语义下 Armed 期内无新 press 沿 ⇒ 「按住超阈值 = 停手」。
//     检查点 = tick 边沿(BootRoot 的 tick 边沿序列内 OnTickEdge),不由渲染帧驱动。
//   - Idle 门(R-9.3):Arm ⇒ 候选列表隐藏(onIdleGate(false));End / Abort ⇒ 复现
//     (onIdleGate(true))。回调由组合根注入指向 EmergencyCandidateRuntime.SetVisible
//     —— 本类不引 Gameplay.UI(可见性是回调,不是依赖)。

using System;
using DaYiJingCheng.Gameplay.Input;
using DaYiJingCheng.Gameplay.Input.Intents;
using DaYiJingCheng.Gameplay.Interaction;   // IArmedState(ADR-013 Amendment A;命名空间住 Interaction)
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
        private readonly EmergencyDirectReadChannel _channel;
        private readonly EmergencyManualDriver _manual;
        private readonly Func<long> _currentTick;
        private readonly Action<bool> _onIdleGate;

        /// <summary>本次 Armed 的目标病人(Arm 时锁定;None = 未预约过)。</summary>
        private PatientId _target = PatientId.None;

        /// <summary>Armed 起点的逻辑 tick(中止判据基准;Idle 态无意义)。</summary>
        private long _armedTick;

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

        /// <summary>挂接通道 onAfterUpdate(透传;接线模式唯一,AC-9-01 fail-loud 由通道抛)。</summary>
        public void Attach() => _channel.Attach();

        /// <summary>卸下通道 onAfterUpdate(透传;幂等)。</summary>
        public void Detach() => _channel.Detach();

        /// <summary>
        /// 预约通道(Idle → Armed,候选列表隐藏)。非 Idle 态由通道自己抛(重复预约 = 编程错误)。
        /// </summary>
        /// <param name="actionOrdinal">急救动作 ordinal(OQ-10-6 表;真表归 21a/10 烘焙轮)。</param>
        /// <param name="target">本次动作的目标病人(Arm 时锁定)。</param>
        public void Arm(int actionOrdinal, PatientId target)
        {
            _channel.Arm(actionOrdinal);
            _target = target;
            _armedTick = _currentTick();
            _onIdleGate?.Invoke(false);
        }

        /// <summary>逻辑层 / 测试驱动缝:透传通道 Feed(Armed 外拒收语义由通道持有)。</summary>
        public bool Feed(EmergencyReading reading) => _channel.Feed(reading);

        /// <summary>测试驱动缝:透传通道 FeedForTest(纯逻辑驱动,不依赖输入相位)。</summary>
        public int FeedForTest(float axis, bool newlyPressed) => _channel.FeedForTest(axis, newlyPressed);

        /// <summary>
        /// 动作结束(松开沿,2026-10-14 用户裁定):聚合 → TryAggregated 上行 → Idle 门复现。
        /// </summary>
        /// <returns>true = 双事件已入流;false = 零样本签约(Idle / 无动作)或
        /// 在场前置未过 —— 均**零事件**(零提示纪律)。</returns>
        public bool EndAction(int actorId)
        {
            AggregatedEmergency? agg = _channel.EndAction();
            _onIdleGate?.Invoke(true);
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
            _onIdleGate?.Invoke(true);
            return wasArmed;
        }

        /// <summary>
        /// tick 边沿中止检查(规则六之甲;由 BootRoot 的 tick 边沿序列驱动,**非渲染帧**)。
        /// Armed 且距预约起点 ≥ <see cref="ModalPhaseEvaluator.AbortIdleTicks"/> ⇒ 中止。
        /// </summary>
        public void OnTickEdge(long tick)
        {
            if (_channel.State != DirectChannelState.Armed)
                return;
            if (tick - _armedTick >= ModalPhaseEvaluator.AbortIdleTicks)
                AbortAction();
        }
    }
}
