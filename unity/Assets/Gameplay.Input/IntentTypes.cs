// Story 006 · 3 的意图边界与交出物(零 SimEvent · 全整数)
//
// 权威来源:
//   AC-3-A6(BLOCKING)—— 输入程序集 asmdef 引用集白名单:不引用声明 IEventSink / SimEvent 的
//     程序集(构建失败);3 只产出「意图」(InteractIntent / EmergencyIntent / FocusNavigationIntent)
//     与 EmergencyReading —— 本文件的四个类型即该闭集(白名单断言见 Editor.Tools.Gates / InputBoundaryGates)。
//   AC-3-B3(BLOCKING)—— 判定结果全整数、由 10 直接构造;3 只交全整数 EmergencyReading。
//   GDD input-system.md 规则八(直读浮点是手感层永不进流;交出的读数是全整数 EmergencyReading)·
//     规则九(联机 C 路:客户端聚合为一条全整数 EmergencyAttempt 上行,本地判定仅预表现)·
//     规则十(EmergencyIntent 是 10 预约直读通道的动作意图)· FocusNavigationIntent = 类型化只读视图
//     (不返回值、不驱动移动,方向真相在官方桥,归 42)。
//   ADR-011 §二 / Amendment A / Amendment B · ADR-006(整数域纪律)· ADR-009 §七(意图三段式第一段)
//
// ⚠️ 枚举纪律(支 1-b,Sim.Contracts/Payloads/PayloadCommon.cs):契约不造枚举 —— 枚举类字段一律
//   int + 注释标 ordinal 来源(防 ordinal 表第二真源;定义住各系统烘焙数据)。EmergencyAction 枚举
//   **尚未定义**(10 侧 OQ-10-6 open)⇒ EmergencyReading.EmergencyAction / EmergencyIntent.Action
//   一律 int ordinal,表归 10 / 21a 的烘焙数据。
//
// 命名空间:本文件四个类型住 **DaYiJingCheng.Gameplay.Input.Intents** 子命名空间 —— 该子命名空间
//   是 AC-3-A6 交出物闭集断言的扫描键(白名单外新增类型须过断言,Analog InputService / BindingsStore
//   等基础设施住根命名空间,不进闭集面)。
//
// 形状口径:
//   EmergencyReading = emergency-procedures.md 规则一(:108-135)五字段 —— action(枚举索引)· hold_ticks
//     (tick 计持时)· edges(press 沿计数)· edge_ticks[](单调不减 press 沿 tick)· magnitude(瞬时定点整数)。
//     3 交给 10 的**永远**是这个结构,判定(Judge / JudgeResult / SimEvent)全归 10(规则八 / 三轮评审
//     修正「生产者口径统一 —— 3 只出读数」)。
//   InteractIntent / FocusNavigationIntent = 帧事件标记(见面文档注释 —— 「意图即事实,无载荷」;
//     4 决定「这一帧的交互对象是谁」,42 决定「接受或忽略」,3 不携带任何他系统状态 —— I2)。

using System;

namespace DaYiJingCheng.Gameplay.Input.Intents
{
    /// <summary>3 → 4 的交互意图(帧事件标记;**意图,非 SimEvent**):「玩家按了交互」这一事实本身,
    /// 不携带任何他系统状态(I2「无外部状态意图源」)。4 决定「这一帧的交互对象是谁」(GDD §Interactions:
    /// 交互系统行 —— 按 A 指认病名时 4 同样只收到本意图)。</summary>
    /// <remarks>零载荷(规则:交互动作本帧是否发生 = 完整事实;目标判定需要的玩家位置等属 4 的读面)。
    /// 全整数约束对零字段平凡成立;闭集成员见 AC-3-A6。</remarks>
    public readonly struct InteractIntent
    {
    }

    /// <summary>3 → 10 的急救动作意图:10 预约直读通道时携带的「哪个急救动作被武装」
    /// (GDD §Interactions:急救动作行 = <c>IEmergencyInput</c> + <c>EmergencyIntent</c> | 直读通道)。
    /// 预约后逐帧读数是 <see cref="EmergencyReading"/>(本文件下);判定仍在 10。</summary>
    /// <remarks>
    /// <para><see cref="Action"/> = <c>EmergencyAction</c> 枚举索引。<b>OQ-10-6 未定 ⇒ 此字段是 int
    /// ordinal</b>(支 1-b 枚举纪律);ordinal 表定义住 10 / 21a 的烘焙数据,本类型不建枚举。</para>
    /// <para>P0 最小面:本结构只承载「动作身份」,不承载通道的 Armed / Reading 状态机(归 7 的直读通道,
    /// Story 007)。</para>
    /// </remarks>
    public readonly struct EmergencyIntent
    {
        /// <summary>动作枚举索引(int ordinal,非枚举 —— OQ-10-6;表归 10 / 21a 烘焙数据)。</summary>
        public readonly int Action;

        /// <summary>构造。</summary>
        /// <param name="action"><c>EmergencyAction</c> ordinal(10 / 21a 表)。</param>
        public EmergencyIntent(int action)
        {
            Action = action;
        }
    }

    /// <summary>3 → 42 的导航动作**类型化只读视图**(规则十):不返回值、不驱动焦点移动
    /// (焦点移动唯一真源 = 官方桥,归 42 / R-6);由 42 决定接受或忽略(焦点单栈门)。
    /// 帧事件标记 —— 方向 / 步进语义由官方桥(<c>NavigationMoveEvent</c>)提供,3 不持有焦点状态
    /// (AC-3-C1 / AC-3-C4 的承诺面;本文件不负 C 组判据 —— C 组归 Story 009)。</summary>
    /// <remarks>零载荷:导航动作本帧是否发生 = 3 的交易事实;连续 <c>Vector2</c> → 离散步进归官方桥 / 42
    /// (GDD 规则十连带闭合,F-3.2:连发改给 42)。「3 内无焦点状态字段」由 AC-3-C1 / AC-3-C4 断。</remarks>
    public readonly struct FocusNavigationIntent
    {
    }

    /// <summary>3 → 10 的全整数急救读数(GDD 规则八 / emergency-procedures.md 规则一):**3 交给 10 的
    /// 唯一读数形状**。每一个字段都是整数语义;直读浮点值(手感层)永不进流(规则八)。
    /// 判定(<c>Judge</c> / <c>JudgeResult</c> / <c>SimEvent</c> 构造)全归 10。</summary>
    /// <remarks>
    /// <para>本结构是**单帧样本** —— 逐帧直读期(通道 <c>Armed</c> / <c>Reading</c>)每帧产生一个;
    /// 10 侧(或 3 侧聚合器 <c>EmergencyAggregator</c>)按 F-10.5 聚合为一条 <see cref="AggregatedEmergency"/>
    /// 后走 C 路上行。</para>
    /// <para>字段齐义(emergency-procedures.md 规则一 :108-135):<c>action</c> · <c>hold_ticks</c>(tick 计
    /// 持时)· <c>edges</c>(press 沿计数)· <c>edge_ticks</c>[](单调不减 press 沿 tick)· <c>magnitude</c>
    /// (该样本瞬时幅度,定点整数)。<c>edge_ticks</c> 单调非递减是 **3 侧的运行时断言义务**(F-10.5 附带口径)。</para>
    /// <para>【AC-3-B3 断言对象】—— 字段类型 ∈ {int, long, 枚举, Fix} + int[] 数组;零 float/double
    /// (AC-3-A7 载荷可达闭包零浮点同此覆盖)。由 Editor.Tools.Gates / InputBoundaryGates 反射断言。</para>
    /// </remarks>
    public readonly struct EmergencyReading
    {
        /// <summary>动作枚举索引(int ordinal,非枚举 —— OQ-10-6;表归 10 / 21a 烘焙数据)。</summary>
        public readonly int Action;

        /// <summary>至该样本的累计持时(tick 计,整数;即「持按时长以 tick 计」)。</summary>
        public readonly int HoldTicks;

        /// <summary>至该样本的累计 press 沿计数(== <see cref="EdgeTicks"/>.Length,运行时断言)。</summary>
        public readonly int Edges;

        /// <summary>单调非递减的 press 沿 tick(F-10.3b / F-10.5 的输入前提;单调性属 3 的运行时断言)。
        /// 每帧直读期该数组是**动作内累计视图**(实现侧可复用同一数组,读取方不得持有跨帧引用)。</summary>
        public readonly int[] EdgeTicks;

        /// <summary>该样本瞬时幅度(F-10.1 <c>round_fixed(raw_axis)</c>,定点整数 —— Q16.16 域内,
        /// 经 3 的整数乘法刻度;绝无 float 出口)。</summary>
        public readonly int Magnitude;

        /// <summary>构造。</summary>
        /// <param name="action"><c>EmergencyAction</c> ordinal。</param>
        /// <param name="holdTicks">该样本时刻的累计持时(tick)。</param>
        /// <param name="edges">累计 press 沿计数。</param>
        /// <param name="edgeTicks">单调非递减 press 沿 tick 数组;长度须 == <paramref name="edges"/>。</param>
        /// <param name="magnitude">该样本瞬时幅度(定点整数)。</param>
        /// <exception cref="ArgumentException"><paramref name="edgeTicks"/> null,
        /// 或 <paramref name="edges"/> ≠ <paramref name="edgeTicks"/>.Length,
        /// 或沿序列非单调非递减(F-10.5 附带口径:属 3 的断言义务)。</exception>
        public EmergencyReading(int action, int holdTicks, int edges, int[] edgeTicks, int magnitude)
        {
            if (edgeTicks == null)
                throw new ArgumentException("EmergencyReading.EdgeTicks 不得为 null", nameof(edgeTicks));
            if (edges != (edgeTicks?.Length ?? -1))
                throw new ArgumentException(
                    $"EmergencyReading.Edges({edges}) ≠ EdgeTicks.Length({edgeTicks?.Length})(规则一:沿计数 = 数组长度)",
                    nameof(edges));
            for (int i = 1; i < edgeTicks.Length; i++)
            {
                if (edgeTicks[i] < edgeTicks[i - 1])
                    throw new ArgumentException(
                        $"EmergencyReading 沿 tick 非单调非递减(下标 {i}:{edgeTicks[i]} < {edgeTicks[i - 1]})" +
                        " —— F-10.5 附带口径:边沿时刻非递减,属 3 的运行时断言义务。",
                        nameof(edgeTicks));
            }
            Action = action;
            HoldTicks = holdTicks;
            Edges = edges;
            EdgeTicks = edgeTicks;
            Magnitude = magnitude;
        }
    }
}