// Story 006 · 3 侧聚合器(B4 C 路:客户端聚合为**一条**全整数上行载荷形状)
//
// 权威来源:
//   AC-3-B4(BLOCKING,承 ADR-011 Amendment B C 路)—— 客户端把意图**聚合为一条**全整数
//     `EmergencyAttempt` 上行 → 主机 `Judge` + `Append` + `Seq`;本地判定仅预表现;
//     「不逐帧同步输入」保留(单周期 0 动作 ⇒ 0 条上行;失败动作仍聚合 —— 判定在主机)。
//   F-10.5 Aggregate(emergency-procedures.md:652-683,唯一聚合点):
//     agg = {
//       action, hold_ticks = 最后一个边沿tick − 第一个边沿tick, edges = |edge_ticks|,
//       edge_ticks = [非递减 press 沿 tick], mag_peak = max(magnitude[t]),   // → 幅度门
//       mag_last = magnitude[t_end],                                          // → 预表现定格
//     }
//     聚合时刻 = 动作结束(或中止)时;中止按规则六之甲**不发** `EmergencyAttempt`。
//   F-10.5 附带口径:`edge_ticks[]` 单调性属 3(运行时断言);`max()` 平局取 tick 较小者归 10。
//
// ⚠️ A6 约束的落地形态:本文件住 **Gameplay.Input**(引用集 = {Unity.InputSystem} ∪ BCL),
//   **不能**引用 Sim.Contracts(声明 IEventSink / SimEvent 的程序集)—— 所以聚合结果用
//   本装配自己的 <see cref="AggregatedEmergency"/> 全整数记录,**不是**
//   <c>EmergencyAttemptPayload</c>(后者由 10 在其边界映射,补 Method / ActorId 字段)。
//   「恰一条 / 中止零条」的断言 = 本节制的边界事实,测试在 3 侧断言。
//
//   3 侧的「聚合产物」(AggregatedEmergency,住 Intents 交出物子命名空间)与「聚合器」
//   (EmergencyAggregator,住根命名空间 = 3 的自身基础设施)是两个类型,分住两面 ——
//   交出物闭集面(AC-3-A6 扫描键)只认 Intents 子命名空间,基础设施不进闭集。
//
// ⚠️ **交出物闭集是 5 件而非 3+1**:A6 原文点名 InteractIntent / EmergencyIntent /
//   FocusNavigationIntent + EmergencyReading 四件;但**同一故事**的 B4(BLOCKING)要求
//   「聚合为**一条**上行」—— 该上行产物的整数形状就是 <see cref="AggregatedEmergency"/>
//   (10 在其边界映射为 EmergencyAttemptPayload 并补 Method / ActorId)。只登记 A6 的
//   3+1 会让 B4 的上行形状**逃出闭集断言面**(第二真源);故本门登记 5 件并在
//   InputBoundaryGates.DeliveredIntentTypes 逐条注明出处(A6 四件 / B4 一件)。
//   **A6 与 B4 是本故事自身的两条 BLOCKING AC,不是两条裁决** —— 合并成一张白名单
//   是把本故事已裁定的交出物写全,不是扩权。
//
// 谁调用:
//   · <see cref="Sample"/> = 直读通道逐帧读数(Story 007 装配通道相位;每帧一次)。
//   · <see cref="EndAttempt"/> = 玩家完成的动作结束(松开/完成)—— 无论表现好坏都聚合,
//     优劣由主机判(判定输入语义;QA「失败动作(判定前中止)⇒ 仍聚合」)。
//   · <see cref="AbortAttempt"/> = 规则六之甲的**系统级中止**(Armed 内持续无 `Emergency`
//     边沿达 `ABORT_IDLE_TICKS` 之类)—— **不发**任何聚合结果(中止 ≠ 失败动作)。
//   ⚠️ 谁在什么时候决定「中止」不是本文件的事(判据归 10 / 通道状态机,Story 007 装配);
//   本文件只提供两条出口,保证 EndAttempt ≤ 恰一条 / AbortAttempt = 零条。

using System;
using System.Collections.Generic;

namespace DaYiJingCheng.Gameplay.Input.Intents
{
    /// <summary>3 侧聚合出的全整数「一条」急救读数记录(F-10.5 agg 形状;
    /// **3 的交出物,非 SimEvent 载荷** —— 10 在其边界映射为
    /// <c>EmergencyAttemptPayload</c> 并补 Method / ActorId)。</summary>
    /// <remarks>
    /// <para>全整数:HoldTicks(末沿 − 首沿)· Edges(== EdgeTicks.Length)· EdgeTicks[]
    /// (非递减 press 沿 tick)· MagPeak(进幅度门,唯一判定量)· MagLast(仅预表现定格,
    /// <b>不参与判定、不进流</b>)。</para>
    /// <para>由 <see cref="EmergencyAggregator.Sample"/> 逐帧累计、<see cref="EmergencyAggregator.EndAttempt"/>
    /// 一次产出;属性全部来自样本的整数域,零四舍五入、零浮点。</para>
    /// </remarks>
    public readonly struct AggregatedEmergency
    {
        /// <summary>动作枚举索引(int ordinal,非枚举 —— OQ-10-6;表归 10 / 21a 烘焙数据)。</summary>
        public readonly int Action;

        /// <summary>持时(F-10.5:最后一个边沿 tick − 第一个边沿 tick;无沿 = 0)。</summary>
        public readonly int HoldTicks;

        /// <summary>press 沿计数(F-10.3b 计数口径;== <see cref="EdgeTicks"/>.Length)。</summary>
        public readonly int Edges;

        /// <summary>非递减 press 沿 tick(单调性在 <see cref="EmergencyAggregator.Sample"/> 内运行时断言)。</summary>
        public readonly int[] EdgeTicks;

        /// <summary>整个动作期的幅度峰值(F-10.5 max 算子 → 进幅度门)。</summary>
        public readonly int MagPeak;

        /// <summary>动作结束时(t_end)的瞬时幅度 —— **不进任何门**(仅预表现定格 / 教学回放)。</summary>
        public readonly int MagLast;

        /// <summary>构造(聚合器内部使用 + 测试夹具)。<paramref name="edgeTicks"/> 传入即持 ——
        /// 聚合器 <see cref="EndAttempt"/> 产出的已是 <c>ToArray()</c> 快照,不受此影响;
        /// 外部夹具须自行不持有可变别名。
        /// ⚠️ 与 <see cref="EmergencyReading"/> ctor **同款断言**(<c>edges == 沿数</c> +
        /// 非 null + 单调非递减)—— 载荷侧 <c>EmergencyAttemptPayload</c> 的 codec 在
        /// 解码时以 <c>InvalidDataException</c> 强判同一条不变量(且要求 <c>Edges=0</c>
        /// 时数组**非 null**)。3 侧若不前置拦,违规记录会一路走到**主机编码路径**才炸
        /// (运行期失败,而非构建期);两处各写一遍日后必漂移,故口径写在本处并由门与测试
        /// 双向对照(评审 S3)。</summary>
        /// <exception cref="ArgumentException"><paramref name="edgeTicks"/> null / <paramref name="edges"/>
        /// ≠ 数组长度 / 沿序列非单调非递减。</exception>
        public AggregatedEmergency(int action, int holdTicks, int edges, int[] edgeTicks,
            int magPeak, int magLast)
        {
            if (edgeTicks == null)
                throw new ArgumentException("AggregatedEmergency.EdgeTicks 不得为 null" +
                    "(载荷侧 EmergencyAttemptPayload 要求 Edges=0 时数组为**空数组非 null**)",
                    nameof(edgeTicks));
            if (edges != edgeTicks.Length)
                throw new ArgumentException(
                    $"AggregatedEmergency.Edges({edges}) ≠ EdgeTicks.Length({edgeTicks.Length})" +
                    "(载荷侧 codec 解码面强判同一条不变量)", nameof(edges));
            for (int i = 1; i < edgeTicks.Length; i++)
                if (edgeTicks[i] < edgeTicks[i - 1])
                    throw new ArgumentException(
                        $"AggregatedEmergency 沿 tick 非单调非递减(下标 {i}:{edgeTicks[i]} < {edgeTicks[i - 1]})" +
                        " —— F-10.5 附带口径,属 3 的运行时断言义务。", nameof(edgeTicks));
            Action = action;
            HoldTicks = holdTicks;
            Edges = edges;
            EdgeTicks = edgeTicks;
            MagPeak = magPeak;
            MagLast = magLast;
        }
    }
}

namespace DaYiJingCheng.Gameplay.Input
{
    using DaYiJingCheng.Gameplay.Input.Intents;   // 交出物子命名空间(AggregatedEmergency / EmergencyReading)

    /// <summary>急救动作期 3 侧聚合器(C 路上行唯一出口;F-10.5 聚合公式的唯一实现点)。</summary>
    /// <remarks>
    /// <para><b>状态</b>:动作进行中累计 own(动作身份 · 首/末沿 tick · 沿数组 · 幅度峰值 · 幅度末值);
    /// 每次 <see cref="EndAttempt"/>(或 <see cref="AbortAttempt"/> / 再次 <see cref="Sample"/> 前)清空。
    /// 设备态 / 通道态豁免(I2)—— 本类持有的是**自有聚合状态**,非他系统状态。</para>
    /// <para><b>恰好一次</b>:<see cref="EndAttempt"/> 返回 0 或 1 条 —— 有样本时恰一条
    /// (含表现糟糕的动作;优劣由主机判),零样本返回 null(对应「单周期 0 动作 ⇒ 0 条上行」)。
    /// <see cref="AbortAttempt"/> = 规则六之甲中止 —— 丢弃累计、返回 void,**不发**。</para>
    /// <para><b>单调性(属 3 的运行时断言)</b>:新样本的新沿 tick ≥ 已累计的末沿(F-10.5 附带口径);
    /// 违例抛 <see cref="ArgumentException"/> —— 这是 3 侧的断言义务,不静默吸收。</para>
    /// <para><b>caller-must 契约(2026-09-26 评审 S5 显红化)</b>:<see cref="EmergencyReading.EdgeTicks"/>
    /// 是**动作内累计视图**而非每帧增量 —— 直读通道可复用同一数组实例、只增长前缀;
    /// 本类按「已累计长度」当游标做差值追加,故**跨样本不得回退长度**(回退 ⇒ 显红,
    /// 旧实现会静默丢沿 / 什么都不加 ⇒ 稳度门拿到残缺沿序列而全绿)。本类**不复制**
    /// 调用方的数组,故调用方同样**不得跨帧持有**该引用(会被下一帧覆写)。</para>
    /// <para>纯 C# 整数域,零 UnityEngine / 零随机 / 零时间依赖;确定性可从样本序列重建。</para>
    /// </remarks>
    public sealed class EmergencyAggregator
    {
        private bool _started;
        private int _action;
        private int? _firstEdge;
        private int? _lastEdge;
        private readonly List<int> _edgeTicks = new List<int>();
        private int _magPeak;
        private int _magLast;

        /// <summary>逐帧喂入读数(直读通道每帧一次)。首样本定动作身份,
        /// 此后动作身份若变 = 编程错误(抛异常,不静默换动作)。</summary>
        /// <param name="r">单帧全整数读数(通道 Armed/Reading 期产出)。</param>
        /// <exception cref="ArgumentException">动作身份变更 / 沿序列非单调 / **累计视图契约违例**。</exception>
        public void Sample(EmergencyReading r)
        {
            if (!_started)            {
                _started = true;
                _action = r.Action;
                _firstEdge = null;
                _lastEdge = null;
                _edgeTicks.Clear();
                AppendEdges(r);
                _magPeak = r.Magnitude;
                _magLast = r.Magnitude;
                return;
            }

            if (r.Action != _action)
                throw new ArgumentException(
                    $"EmergencyAggregator 动作身份中途变更(先 {_action} 后 {r.Action})——" +
                    "直读通道一个 Armed 期只应武装一个动作;换动作须先 EndAttempt/AbortAttempt。",
                    nameof(r));
            // 「累计视图」是**caller-must 契约**,不满足即显红(评审 S5):若通道改喂每帧
            // 增量,旧实现会静默丢沿(`from` 越过长度 ⇒ 循环空转)或什么都不加 ⇒ 稳度门
            // 拿到残缺沿序列、Edges 偏小,而**全绿**。这是最难查的静默失真面,必须前置断。
            if (r.EdgeTicks.Length < _edgeTicks.Count)
                throw new ArgumentException(
                    $"EmergencyAggregator 累计视图契约违例(本帧沿数 {r.EdgeTicks.Length} < " +
                    $"已累计 {_edgeTicks.Count})—— EdgeTicks 是**动作内累计视图**(Story 007 " +
                    "通道每帧可复用同一数组、只增长前缀),不是每帧增量;改喂增量须先 EndAttempt。",
                    nameof(r));
            AppendEdges(r);
            _magPeak = Math.Max(_magPeak, r.Magnitude);
            _magLast = r.Magnitude;
        }

        /// <summary>把样本的累计沿视图并入聚合器(新沿 tick 单调断言;
        /// 沿数组 = 累计视图,按差值追加,不依赖数组是否为同一实例)。</summary>
        private void AppendEdges(EmergencyReading r)
        {
            var edges = r.EdgeTicks;
            int from = _edgeTicks.Count;
            for (int i = from; i < edges.Length; i++)
            {
                int tick = edges[i];
                if (_lastEdge.HasValue && tick < _lastEdge.Value)
                    throw new ArgumentException(
                        $"EmergencyAggregator 沿 tick 单调性违例(新沿 {tick} < 前沿 {_lastEdge.Value})" +
                        " —— F-10.5 附带口径:边沿时刻非递减(属 3 的运行时断言)。",
                        nameof(r));
                _edgeTicks.Add(tick);
                if (!_firstEdge.HasValue) _firstEdge = tick;
                _lastEdge = tick;
            }
        }

        /// <summary>动作结束(松开 / 完成):聚合为**恰一条**全整数记录并重置累计。
        /// 表现优劣**不**在此判断(判定输入语义 —— 主机才有权判失败)。零样本返回 null
        /// (对应「单周期 0 动作 ⇒ 0 条上行」)。</summary>
        /// <returns>聚合记录;从未有样本时为 null(0 条上行)。</returns>
        public AggregatedEmergency? EndAttempt()
        {
            if (!_started)
                return null;

            int hold = 0;
            if (_firstEdge.HasValue && _lastEdge.HasValue)
                hold = _lastEdge.Value - _firstEdge.Value;   // F-10.5:末沿 − 首沿
            int[] edgeSnapshot = _edgeTicks.ToArray();
            var result = new AggregatedEmergency(_action, hold, edgeSnapshot.Length,
                edgeSnapshot, _magPeak, _magLast);
            Reset();
            return result;
        }

        /// <summary>规则六之甲的系统级中止(如持续无边沿达 <c>ABORT_IDLE_TICKS</c>):
        /// 丢弃累计,返回 void —— 中止**不发** <c>EmergencyAttempt</c>(F-10.5 附注)。</summary>
        public void AbortAttempt()
        {
            Reset();
        }

        /// <summary>重置累计态(动作身份 / 沿 / 幅度);由 EndAttempt / AbortAttempt 调用。
        /// <b>private</b>(2026-09-26 评审 S11):本类**不向外部开放中途清空的入口** ——
        /// 外部可在动作进行中静默丢弃已累计的沿与幅度(与 caller-must 契约违例叠加
        /// 构成难查的失真路径)。测试侧走 <see cref="EndAttempt"/> / <see cref="AbortAttempt"/>
        /// 两条**真出口**(它们内部调本方法),不需要也不该有第三条。</summary>
        private void Reset()
        {
            _started = false;
            _firstEdge = null;
            _lastEdge = null;
            _edgeTicks.Clear();
            _magPeak = 0;
            _magLast = 0;
        }
    }
}