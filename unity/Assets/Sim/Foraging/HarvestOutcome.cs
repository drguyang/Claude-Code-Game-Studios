// story-005 —— 采集裁决的**结果形状**:三类失败各自具名 + 闸门输入。
//
// 权威来源:
//   design/gdd/foraging.md
//     · §Edge Cases「失败收敛」`:429-450` —— 三类失败**各自具名、各有载体**;
//       「库存满载 / 余量不足 / 超时未裁决」,原稿把三者塞进一条可选反馈 = 手-wave
//     · 规则六 —— 采集是**输入动作**,打断 = 零效果
//     · F-17-3 求值口径 ② —— 采前闸 = `remaining ≥ Fix(qty)`
//   story production/epics/foraging/story-005-action-atomicity-failure-convergence.md
//     AC-17-07(满载零消耗)· AC-17-07c(三类具名分支)· AC-17-08(打断零效果)
//
// ── 为什么失败必须是一个**枚举**而不是 bool ──────────────────────────────
// 三类失败给玩家的**正确响应相反**(清箱子 / 换一株 / 重试)。一个 `bool` 成功位
// 把它们压成同一件事 ⇒ 呈现层只能给一条笼统反馈 ⇒ 玩家无法判断该做什么。
// AC-17-07c 因此要求「各有具名枚举/分支,非单一可选反馈」。
//
// ── 「超时未裁决」为什么有枚举位、却没有生产入口 ──────────────────────────
// 它依赖采集意图的**上行通道**(联机),而 `TR-foraging-008` 仍是 `gap`
// (`OQ-17-6` 未裁,ADR-001 窄修订未做)⇒ P0 单机**结构上不可达**。
// 按 story 的护栏:**只立枚举与分支,不伪造网络时序**。伪造一个「超时」夹具
// 测的会是测试自己的计时器,不是生产行为。

using System;

namespace DaYiJingCheng.Sim.Foraging
{
    /// <summary>
    /// 采集裁决的失败类 —— **三类各自具名**(GDD §失败收敛;AC-17-07c)。
    /// </summary>
    public enum HarvestFailureKind
    {
        /// <summary>未失败(成功)。</summary>
        None = 0,

        /// <summary>库存满载 —— 正确响应:清箱子(归 20 的 `CanCarry`)。</summary>
        FullInventory = 1,

        /// <summary>节点余量不足 —— 正确响应:换一株(归 17 的 `NodeRemaining.CanHarvest`)。</summary>
        InsufficientRemaining = 2,

        /// <summary>超时未裁决 —— 正确响应:重试。**P0 单机不可达**(`OQ-17-6` 未裁 / 上行通道未定)。</summary>
        TimeoutUnresolved = 3,
    }

    /// <summary>
    /// 裁决前的**闸门输入** —— 由调用方在裁决时求值(GDD:采前闸由主机在裁决时判)。
    /// </summary>
    /// <remarks>
    /// <para><b>为什么是「输入」不是「在这里求值」</b>:满载归 20(`CanCarry` 要看载重与
    /// 候选件重量),余量归 17(`CanHarvest` 要看流前缀)。两边各有真源,本结构只承载
    /// **两个已求好的布尔**,不重算任何一边 —— 否则就是第二份实现。</para>
    /// </remarks>
    public readonly struct HarvestGate
    {
        /// <summary>20 的 `CanCarry` 结果:能否整件容纳本次采集量。</summary>
        public readonly bool HasCapacity;

        /// <summary>17 的 `CanHarvest` 结果:`remaining(node,t) ≥ Fix(qty)`。</summary>
        public readonly bool HasRemaining;

        /// <summary>构造闸门输入。</summary>
        public HarvestGate(bool hasCapacity, bool hasRemaining)
        {
            HasCapacity = hasCapacity;
            HasRemaining = hasRemaining;
        }

        /// <summary>
        /// **全放行** —— 仅供「已由别处判过」的装配点与既有夹具使用。
        /// <para>⚠️ 生产裁决路径**不得**直接用它:那等于跳过采前闸(AC-17-06 / 07)。
        /// 生产应传由 `CarryLoadSolver.CanCarry` 与 `NodeRemaining.CanHarvest` 求出的真值。</para>
        /// </summary>
        public static HarvestGate Allow => new HarvestGate(hasCapacity: true, hasRemaining: true);
    }

    /// <summary>
    /// 一次采集裁决的**结果**:成功带 id 与序号,失败带具名类别。
    /// </summary>
    public readonly struct HarvestOutcome
    {
        /// <summary>是否成功。<c>false</c> ⇒ <see cref="Result"/> 不可用(默认值)。</summary>
        public readonly bool Succeeded;

        /// <summary>失败类(成功时为 <see cref="HarvestFailureKind.None"/>)。</summary>
        public readonly HarvestFailureKind Failure;

        /// <summary>成功时的 id 与 <c>gather_seq</c>;失败时为默认值,**调用方不得读取**。</summary>
        public readonly HarvestResult Result;

        private HarvestOutcome(bool succeeded, HarvestFailureKind failure, in HarvestResult result)
        {
            Succeeded = succeeded; Failure = failure; Result = result;
        }

        /// <summary>构造成功结果。</summary>
        public static HarvestOutcome Success(in HarvestResult result)
            => new HarvestOutcome(true, HarvestFailureKind.None, in result);

        /// <summary>构造失败结果(<paramref name="kind"/> 不得为 <see cref="HarvestFailureKind.None"/>)。</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> 为 <c>None</c>。</exception>
        public static HarvestOutcome Reject(HarvestFailureKind kind)
        {
            if (kind == HarvestFailureKind.None)
                throw new ArgumentOutOfRangeException(nameof(kind), kind,
                    "「失败」的类别不得是 None —— 那会让调用方把失败读成成功");
            return new HarvestOutcome(false, kind, default);
        }
    }
}
