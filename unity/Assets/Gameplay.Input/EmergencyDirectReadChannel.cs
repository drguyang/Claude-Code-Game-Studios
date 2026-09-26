// Story 007 · 急救直读通道(系统 3 的通道态状态机 + 采样管线;story-006 聚合器的装配方)
//
// 权威来源:
//   GDD input-system.md 规则七(急救动作的输入值在 `InputSystem.onAfterUpdate` 期的 action
//     回调中取 —— **非轮询**;直读通道独立于 42 UI 栈)· §States 二(直读通道态:Idle / Armed /
//     Reading / Suspended —— Idle 期 `Emergency` action `Disable()`;`enabled` 翻转与回调
//     计数本身是 AC,归 story-010 的 AC-3-E4,本故事只装配通道相位)。
//   GDD emergency-procedures.md 规则一(:108-135)五字段读数形状 · F-10.1(magnitude 定点化,
//     承 OQ-10-7:AXIAL_SCALE / DZ_MAG / MAG_MAX 三个乘子**值未裁,由构造方显式注入** ——
//     本文件不拍值,只落形状与 Q16.16 量纲)+ F-10.5(聚合归 10 的 C 路上行;本通道只做
//     **逐帧读数 → 喂聚合器 Edge**,聚合出口 EndAction / AbortAction 供 10 消费)。
//   ADR-011 §二(直读 = `onAfterUpdate` 回调,非轮询;方差不是算术 · 绕开 42 UI 栈)·
//     §Risks-A S1 / S3(updateMode 钉死 + 相位实测 —— 见 story-007 Completion Notes /
//     Logs/story007_spike_results.txt:帧-采样对偶 8/8 恰 +1;spike 在未聚焦 batch 里测得
//     manual `Update()` 不触发 onAfterUpdate —— 该结论**环境依赖**,聚焦环境 / 玩家构建
//     路径下手动 Update 会再触发本回调 ⇒ 由 `_lastCallbackFrame` 同帧去重闸拦下,
//     即 B2③「manual 干跑不双计」的实现保证)。
//   AC-3-B1a(零硬件合成注入 → 读数可见性 ≤1 帧且同帧可见)· AC-3-B2③(帧 +1 ⇒ 采样恰 +1,
//     **不得以枚举成员名为主语**,性质判据 · 载体可换)。
//
// ⚠️ G9(2026-09-26 评审移交 ·story 文件 Implementation Notes 51-57):`EmergencyReading.EdgeTicks`
//    **无防御性拷贝** —— 本通道每帧用 `_edgeTicks.ToArray()` 产一个**独立副本**喂给
//    `EmergencyAggregator.Sample`,Sample 返回后就丢弃该引用,**不存进任何字段**(存字段 =
//    跨帧持有,下一帧聚合出的 reading 数组被策略覆写 = 静默数据损坏)。数组每帧 new ⇒
//    每帧一次分配;零分配约束归 story-010(AC-3-E5),本故事不正交它。
//
// ⚠️ Idle 期零成本:本通道在 Idle 态**不产生任何样本**(Feed 拒收),且**不触碰**
//    `Emergency` 动作的 `enabled` 翻转 —— 翻转动作归 story-010 / 10 预约(本故事只保证
//    「不采样」与「不轮询」:全部样例只在 onAfterUpdate 回调内产生,无一在外部轮询驱动)。
//
// 形状口径:采样计数 `SampleCount` 是本通道暴露的**行为仪表**(对偶断言 / 诊断用);
//   状态四态枚举住根命名空间 = 3 的自身基础设施(非 Intents 交出物 —— 不进 A6 闭集面)。
//   Reading 态的行为身 = `_sampledThisFrame`(Armed 且本帧已被采样);接线侧由 onAfterUpdate
//   回调在**帧边界**清零(帧间不可观察为 Reading —— 与「每帧恰一次」的对偶一致)。

using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using DaYiJingCheng.Gameplay.Input.Intents;   // EdgeTicks 数组元素类型(累计 press 沿 tick)

namespace DaYiJingCheng.Gameplay.Input
{
    /// <summary>3 的直读通道态(§States 二;本故事实现内位枚举 —— 符号非判据主语,
    /// B2③ 只断「帧 +1 ⇒ 采样 +1」的性质)。</summary>
    public enum DirectChannelState
    {
        /// <summary>默认态:Emergency 动作 Disable()(翻转归 AC-3-E4 / story-010);本态 Feed 拒收 ⇒ 零采样。</summary>
        Idle = 0,

        /// <summary>10 预约通道后在急救动作开场进入:允许逐帧直读吃到 sample。</summary>
        Armed = 1,

        /// <summary>每帧直读(仅 Armed 期):回调内取值;本通道不做显式校准 —— 视为
        /// Armed 期内「本帧被采样」的瞬时观察(见类头形状口径)。</summary>
        Reading = 2,

        /// <summary>系统级失焦(设备断连 / 失焦 -> 合成 release);事件驱动不轮询。
        /// 本故事不实现 Suspended 的入口裁定(合成 release 归 Story 008 设备态),
        /// 仅保留状态位与「Suspended 亦拒收 Sample」的守卫。</summary>
        Suspended = 3,
    }

    /// <summary>急救直读通道(AC-3-B1a / B2③ 的实现载体):挂 <see cref="InputSystem.onAfterUpdate"/>,
    /// 在回调内直读 Emergency 动作并逐帧喂 <see cref="EmergencyAggregator"/> —— **唯一驱动源,
    /// 不在任何 Update 里主动轮询**。构造注入动作资产与 F-10.1 三个乘子(数值值域归
    /// OQ-10-7 数值轮,本类不拍值)。</summary>
    /// <remarks>
    /// <para><b>接线纪律</b>:<see cref="Attach"/> 恰挂一次 <c>onAfterUpdate</c>,<see cref="Detach"/>
    /// 卸挂;回调内<b>不抛异常</b>(构造 EmergencyReading 的断言违例属编程错误,以
    /// <see cref="ArgumentException"/> 上抛让装配层在启动期炸,而不在每帧回调里反复炸)。</para>
    /// <para><b>G9 数组纪律</b>:每帧 <c>_edgeTicks.ToArray()</c> 产独立副本喂 Sample,
    /// Sample 返回后即弃;类内<b>无任何字段持有</b>该数组(详见类头 ⚠️)。</para>
    /// <para><b>判定边界</b>:本通道只产 <see cref="EmergencyReading"/> 并喂聚合器 ——
    /// 不做判定、不构造 SimEvent、不写流(AC-3-B3 / A6;判定全归 10)。</para>
    /// <para><b>四态最小面</b>:Idle = 默认拒收 · Armed = 允许采样 · Reading = Armed 内瞬时
    /// 观察 · Suspended = 保留位(入口裁定归 Story 008)。<see cref="SampleCount"/> 是行为仪表
    /// (B2③ 对偶断言主角);测试经 <see cref="FeedForTest"/> 驱动纯逻辑面(不点火不挂回调),
    /// 由 <see cref="SampleCount"/> 与 <see cref="State"/> 断言。</para>
    /// </remarks>
    public sealed class EmergencyDirectReadChannel
    {
        /// <summary>F-10.1 `raw_axis → 定点域` 的标定乘子(值归数值轮;构造必传,Q16.16 量纲)。</summary>
        private readonly int _axialScale;

        /// <summary>F-10.1 幅度死区(摇杆漂移入口;值归数值轮)。</summary>
        private readonly int _dzMag;

        /// <summary>F-10.1 幅度上界(`AXIAL_SCALE ≥ MAG_MAX` · `0 ≤ MAG_MAX ≤ 65536`,值归数值轮)。</summary>
        private readonly int _magMax;

        private readonly EmergencyAggregator _aggregator = new EmergencyAggregator();
        private readonly List<int> _edgeTicks = new List<int>();
        private int _holdTicks;
        private int _armedAction;
        private DirectChannelState _state = DirectChannelState.Idle;
        private bool _attached;
        private int _sampleCount;

        /// <summary>本帧已采样瞬时位(Reading 态的行为身):Feed 成功置位,
        /// 本帧**首次** onAfterUpdate 回调(帧边界;同帧被去重闸拦下的回调不清)清零 ——
        /// 帧间不可观察为 Reading。逻辑层驱动(FeedForTest)不清零(无回调边界),
        /// 每次驱动后观察 = 已采样。</summary>
        private bool _sampledThisFrame;

        /// <summary>同帧去重:每个渲染帧只处理第一次 onAfterUpdate 回调。手动
        /// <c>InputSystem.Update()</c>(聚焦环境 / 玩家构建路径)会在同帧再触发本回调,
        /// 不拦则 SampleCount 跟调用计数走、破坏「帧 +1 ⇒ 采样 +1」(AC-3-B2③
        /// 「manual 干跑不双计」)。</summary>
        private int _lastCallbackFrame = -1;

        /// <summary>仅接线模式使用:注入的动作资产里解析出的 Emergency 动作(构造时缓存,
        /// **零每帧字符串查找** —— TR-input-012;null = 未 Attach 的逻辑层模式)。</summary>
        private readonly InputAction _emergencyAction;

        /// <summary>以动作资产与 F-10.1 三乘子构造通道。逻辑层模式(<paramref name="actions"/>
        /// 为 null,仅测试/纯逻辑使用)不解析动作,<see cref="Attach"/> 会抛。</summary>
        /// <param name="actions">全案唯一动作资产(规则一);null 允许但不接线下。</param>
        /// <param name="axialScale">AXIAL_SCALE:Q16.16 定点整数,须 ≥ <paramref name="magMax"/>。</param>
        /// <param name="dzMag">DZ_MAG ≥ 0:大幅死区入口(tick 尺度。</param>
        /// <param name="magMax">MAG_MAX ∈ [0, 65536]:幅度上界(Q16.16 计数)。</param>
        /// <exception cref="ArgumentException"><paramref name="axialScale"/> &lt; <paramref name="magMax"/>
        /// (F-10.1 结构性要求 AXIAL_SCALE ≥ MAG_MAX)或 <paramref name="dzMag"/> &lt; 0。</exception>
        public EmergencyDirectReadChannel(InputActionAsset actions, int axialScale, int dzMag, int magMax)
        {
            if (axialScale < magMax)
                throw new ArgumentException(
                    $"AXIAL_SCALE({axialScale}) < MAG_MAX({magMax}) —— F-10.1 结构性要求 AXIAL_SCALE ≥ MAG_MAX" +
                    "(否则幅度域被压扁,幅度门退化近恒真;emergency-procedures.md F-10.1 首轮评审 A9)。",
                    nameof(axialScale));
            if (dzMag < 0)
                throw new ArgumentException($"DZ_MAG({dzMag}) < 0 —— 死区非负(F-10.1)。", nameof(dzMag));
            _axialScale = axialScale;
            _dzMag = dzMag;
            _magMax = magMax;
            _emergencyAction = actions?.FindAction("Emergency", throwIfNotFound: false);
        }

        /// <summary>当前通道态(3 拥有;10 据此判断能否预约 / 是否仍在采样期)。</summary>
        public DirectChannelState State => _state;

        /// <summary>已累计的采样计数(**行为仪表** —— B2③ 对偶断言的「采样侧」;
        /// 只在 Armed 期 Feed 成功时 +1,Idle / Suspended 零增长)。</summary>
        public int SampleCount => _sampleCount;

        /// <summary>是否已挂 <c>InputSystem.onAfterUpdate</c>。</summary>
        public bool IsAttached => _attached;

        /// <summary>挂接 onAfterUpdate(恰一次;重复挂 = 抛)。要求动作资产非 null 且
        /// Emergency 动作存在(<see cref="Attach"/> 只在接线模式合法)。</summary>
        /// <exception cref="InvalidOperationException">已挂 / 逻辑层模式动作缺失。</exception>
        public void Attach()
        {
            if (_attached)
                throw new InvalidOperationException("EmergencyDirectReadChannel 已挂接 onAfterUpdate,不可重复挂(恰一次)。");
            if (_emergencyAction == null)
                throw new InvalidOperationException(
                    "接线模式要求构造传入非 null 动作资产且含 Emergency 动作 —— 逻辑层模式不能 Attach。");
            _attached = true;
            InputSystem.onAfterUpdate += OnAfterUpdate;
        }

        /// <summary>卸下 onAfterUpdate(未挂时无操作;幂等)。</summary>
        public void Detach()
        {
            if (!_attached) return;
            _attached = false;
            InputSystem.onAfterUpdate -= OnAfterUpdate;
        }

        /// <summary>10 预约通道(Armed 进入):Idle → Armed。已在 Armed / Reading 时抛
        /// (重复预约 = 编程错误)。⚠️ 本方法**不翻转** `Emergency` 动作的 enabled
        /// (翻转归 story-010 AC-3-E4),只开放采样窗口。</summary>
        /// <param name="actionOrdinal"><c>EmergencyAction</c> ordinal(OQ-10-6 表;
        /// 10 / 21a 烘焙数据;非负)。本通道 Armed 期**唯一动作身份** —— 采样从本参数取。</param>
        /// <exception cref="InvalidOperationException">非 Idle 态预约。</exception>
        /// <exception cref="ArgumentException"><paramref name="actionOrdinal"/> &lt; 0。</exception>
        public void Arm(int actionOrdinal)
        {
            if (_state != DirectChannelState.Idle)
                throw new InvalidOperationException(
                    $"通道预约失败:当前态 {_state},须 Idle(动作进行中不可二次预约;10 须先 End/Abort)。");
            if (actionOrdinal < 0)
                throw new ArgumentException("Emergency action ordinal 非负(OQ-10-6 表;10 侧烘焙数据)。",
                    nameof(actionOrdinal));
            _armedAction = actionOrdinal;
            _state = DirectChannelState.Armed;
        }

        /// <summary>动作结束(10 在动作完成时调用):聚合为恰一条 <see cref="AggregatedEmergency"/>
        /// 并回 Idle。Idle 态调用 = 空操作返回 null(零样本签约 —— 单周期 0 动作 ⇒ 0 条上行)。
        /// <b>不发</b> 时返回 null;判定语义仍归 10(表现优劣主机判)。</summary>
        public AggregatedEmergency? EndAction()
        {
            if (_state == DirectChannelState.Idle)
                return null;
            var result = _aggregator.EndAttempt();
            ResetToIdle();
            return result;
        }

        /// <summary>动作中止(10 裁决规则六之甲):丢弃累计、回 Idle、**不发**(F-10.5 附注)。</summary>
        public void AbortAction()
        {
            if (_state == DirectChannelState.Idle)
                return;
            _aggregator.AbortAttempt();
            ResetToIdle();
        }

        /// <summary>逐帧读数的**瞬态观察接口**(10 预表现 / 调试):Armed 且**本帧已被采样**
        /// = Reading(读数由聚合器消化,不跨帧缓存,承 G9)。帧间不可观察为 Reading ——
        /// 观察语义只在采样帧内成立(见 <see cref="_sampledThisFrame"/>)。</summary>
        public bool IsReading => _state == DirectChannelState.Armed && _sampledThisFrame;

        /// <summary>纯逻辑驱动入口(测试与接线回调共用):仅 Armed 态产生样本(Idle / Suspended
        /// 拒收 ⇒ 零采样);样本喂聚合器并累计 <see cref="SampleCount"/>。每帧恰一次由调用方
        /// 保证(接线侧 = onAfterUpdate 回调;测试侧 = 显式驱动)。</summary>
        /// <param name="r">本帧全整数读数(EdgeTicks 为动作内**累计视图**;本方法不持有引用,
        /// Sample 返回后即弃 —— 承 G9)。</param>
        /// <returns>true = 样本被接收并计数;false = Idle / Suspended 拒收。</returns>
        /// <exception cref="ArgumentException">动作身份中途变更 / 沿序列非单调 / 累计视图违例
        /// (EmergencyAggregator 语义,见其文件头)。</exception>
        public bool Feed(EmergencyReading r)
        {
            if (_state != DirectChannelState.Armed)
                return false;
            _aggregator.Sample(r);
            _sampleCount++;
            _sampledThisFrame = true;
            return true;
        }

        /// <summary>测试专用纯逻辑面:**不挂 onAfterUpdate、不依赖任何 Unity 输入相位**,
        /// 每调用等价于「接线侧回调触发一次」。**按帧语意喂动作相位参数**(axis ∈ [0,1]
        /// 归一化手感量 / newlyPressed —— 等价于接线侧 <see cref="ReadEmergency"/> 该帧的
        /// 直读结果);沿 tick 由通道内部单调计数器 +1 分配(单调 by construction)。
        /// 帧边界清 <see cref="_sampledThisFrame"/>(与接线侧回调一致:Reading 只在采样帧内
        /// 可观察)。返回采样后的 <see cref="SampleCount"/>。</summary>
        /// <param name="axis">本帧动作幅度(归一化 [0,1] 手感量,仅本帧;**不进流**;
        /// 经 F-10.1 形状在本通道内定点化为 magnitude)。</param>
        /// <param name="newlyPressed">本帧是否发生 press 沿(沿 tick = 通道内单调计数器 +1)。</param>
        public int FeedForTest(float axis, bool newlyPressed)
        {
            if (_state != DirectChannelState.Armed)
                return _sampleCount;
            int action = _armedAction;
            if (newlyPressed)
                _edgeTicks.Add(_edgeTicks.Count);
            _holdTicks++;
            int magnitude = MagnitudeFromAxis(axis);
            int[] edges = _edgeTicks.ToArray();
            var reading = new EmergencyReading(action, _holdTicks, edges.Length, edges, magnitude);
            _sampledThisFrame = true;
            _aggregator.Sample(reading);
            return ++_sampleCount;
        }

        /// <summary>onAfterUpdate 回调(接线回调 —— **唯一**样例驱动点)。同帧只处理第一次
        /// 回调:聚焦环境 / 玩家构建下手动 <c>InputSystem.Update()</c> 会在同帧再触发本回调,
        /// 不拦则采样数跟调用计数走(AC-3-B2③「manual 干跑不双计」)。Armed 期逐帧:读动作 →
        /// 构造 EmergencyReading → Feed。回调内不抛(见类头)。</summary>
        private void OnAfterUpdate()
        {
            if (UnityEngine.Time.frameCount == _lastCallbackFrame)
                return;                       // 同帧第二+次回调(手动 Update 等):不双计
            _lastCallbackFrame = UnityEngine.Time.frameCount;
            _sampledThisFrame = false;   // 帧边界:每帧采样窗口起点
            if (_state != DirectChannelState.Armed)
                return;
            var reading = ReadEmergency();
            Feed(reading);
        }

        /// <summary>从动作直读当前帧构成一条全整数读数(F-10.1 magnitude 定点化)。
        /// 「每帧新数组」的 ToArray 副本即 G9 的交付形态(见类头)。</summary>
        private EmergencyReading ReadEmergency()
        {
            // press 沿检测(&amp; 累计的沿数组 = 动作内累计视图,ToArray 后即弃)
            bool newlyPressed = _emergencyAction.WasPressedThisFrame();
            if (newlyPressed)
                _edgeTicks.Add(_edgeTicks.Count);   // 沿 tick = 动作内第几个样本(单调非递减 ✓)

            _holdTicks++;

            int magnitude = MagnitudeFromAxis(_emergencyAction.ReadValue<float>());

            int[] edges = _edgeTicks.ToArray();
            return new EmergencyReading(_armedAction, _holdTicks, edges.Length, edges, magnitude);
        }

        /// <summary>F-10.1 形状:raw → 定点 magnitude(round_half_away(ax · AXIAL_SCALE),
        /// 落 DZ 死区 / MAG_MAX 夹取)。输入为手感层 float raw(3 内部,不进流);
        /// 接线侧读动作值 ∈ [−1,1](模拟轴),测试侧经 <see cref="FeedForTest"/> 传归一化
        /// [0,1](该路径无负轴,形状同一定点化)。</summary>
        private int MagnitudeFromAxis(float rawAxis)
        {
            int magnitude = RoundHalfAwayFromZero(rawAxis * _axialScale);
            if (magnitude < _dzMag) magnitude = 0;          // 静息漂移入口吃掉(死区)
            if (magnitude > _magMax) magnitude = _magMax;   // 域界(不恒等 MAG_MAX = 域)
            return magnitude;
        }

        private void ResetToIdle()
        {
            _state = DirectChannelState.Idle;
            _armedAction = 0;
            _sampledThisFrame = false;
            _edgeTicks.Clear();
            _holdTicks = 0;
        }

        /// <summary>F-10.1 舍入:`ROUND_HALF_AWAY_FROM_ZERO`(承 ADR-006 —— 禁 Math.Round 默认
        /// ties-to-even)。输入为手感层 float raw(3 内部,不进流)。</summary>
        private static int RoundHalfAwayFromZero(float v)
        {
            if (v >= 0f) return (int)Math.Floor(v + 0.5f);
            return (int)Math.Ceiling(v - 0.5f);
        }
    }
}