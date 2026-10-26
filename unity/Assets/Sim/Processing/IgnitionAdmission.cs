// S20-9 · processing story-004 —— 点火准入判据(**时点拒绝**,AC-18-12 的前半)
//
// 权威来源:
//   design/gdd/processing.md
//     · 规则七(:255-277)点火事务五步 —— ④ 交 20 `Apply(consumed, produced)` 验容量,
//       失败整体拒绝(此时尚无事件);⑤ **20 接受后**才发 `Craft`
//     · 规则七附注(:270-277)「容量不足 ⇒ 原料不扣、药不出、`Craft` 不落流」
//     · R-18-D(:279-297)拒绝发生在**点火 tick**,流上不存在「中途回滚」
//     · AC-18-12(本判据的机器可断言面)
//   design/gdd/inventory-and-items.md
//     · F-20.2(:464)`CanCarry ⟺ CarryLoad + InstanceWeight ≤ CARRY_CAP`(等号侧合法)
//   ADR-005 —— 事件流唯一真源 ⇒ 「拒绝」= **零 Append**,不是「写一条拒绝事件」
//   ADR-025 §① —— `Sim` 引用集恰 = {BCL, Sim.Contracts}
//
// ─────────────────────────────────────────────────────────────────────────────
// 本类**不做**什么(边界,必须与「它做了什么」同读)
// ─────────────────────────────────────────────────────────────────────────────
// ① **不调用 20 的 `Apply`** —— 该入口归 inventory-005(S20-14),本件交付的是**谓词**。
//    调用方在 S20-14 落地后应把 `CanAdmit` 作为 `Apply` 的**前置**,而不是替代它:
//    `Apply` 还要扣原料 / 铸产出实例 / 落地溢出,那三件都超出「装不装得下」。
// ② **不写任何事件** —— 本类零 `IEventSink`、零 `IPayloadEncoder` 依赖。拒绝 = 调用方
//    拿到 `Admitted == false` 后**什么都不做**。
//    这不是「省略」,是 AC-18-12「零 `Craft` 事件落流」的结构性保证:
//    不持写通道 ⇒ 想写也写不出来。
// ③ **不判单炉** —— 单炉归 `FurnaceState.IsBusy`(规则八并发模型表)。
//    两个判据**次序**由调用方定(规则七:② 单炉检查 → ④ 容量检查),本类不重排。
//
// ─────────────────────────────────────────────────────────────────────────────
// 为什么按「逐行增量」而不是「总和一次比」
// ─────────────────────────────────────────────────────────────────────────────
// 一次比总和需要在**本文件**里再做一遍 `w × qty` 的求和 —— 那是 `CarryLoadSolver.CanCarry`
// 的第二实现(`CarryLoad.cs:195` 明写「唯一实现点」,Story 005 只可经由 `CanCarry` 取真值)。
// 逐行增量则每一行都走同一个 `CanCarry`,溢出域 / 负值域 / 等号侧的判据**全部继承**。
// 整数加法结合律在 long 域成立 ⇒ 逐行增量与总和一次比**结果等价**;
// 差别只在「哪一行被拒」这个可诊断信息上 —— 增量版能给出,总和版给不出。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.Processing
{
    /// <summary>一行产出的载重输入(= `unitWeight × qty`,单件重量由调用方从 21a 物品表解出)。
    /// <para>⚠️ 为什么是「重量 + 件数」而不是 `ItemOrdinal`:`Sim` 看不见物品表
    /// (门 A / ADR-025 §①),ordinal → weight 的权威必须留在 21a。
    /// 与 `CarryLoadSolver.Sum` 收 `Func&lt;int,int&gt;` 是同一纪律的两种形态。</para>
    /// </summary>
    public readonly struct OutputLoadLine
    {
        /// <summary>单件重量(≥ 1;域错误由 <see cref="CarryLoadSolver.CanCarry"/> 抛)。</summary>
        public readonly int UnitWeight;

        /// <summary>件数(≥ 1)。</summary>
        public readonly int Qty;

        /// <summary>构造。</summary>
        public OutputLoadLine(int unitWeight, int qty)
        {
            UnitWeight = unitWeight; Qty = qty;
        }
    }

    /// <summary>点火准入裁决结果 —— 可诊断形态(`Admitted == false` 时点名被拒行)。</summary>
    public readonly struct IgnitionAdmissionResult
    {
        /// <summary>能否装下全部产出。</summary>
        public readonly bool Admitted;

        /// <summary>被拒的产出行下标(<see cref="Admitted"/> 为 true 时 = -1)。
        /// <para>有它才能区分「第一行就装不下」与「最后一行才装不下」——
        /// 两者在玩家侧是不同处境(前者可能换个配方就成,后者得先腾背包)。</para>
        /// </summary>
        public readonly int RejectedLineIndex;

        /// <summary>构造。</summary>
        public IgnitionAdmissionResult(bool admitted, int rejectedLineIndex)
        {
            Admitted = admitted; RejectedLineIndex = rejectedLineIndex;
        }

        /// <inheritdoc />
        public override string ToString() =>
            Admitted ? "Admitted" : "Rejected@" + RejectedLineIndex;
    }

    /// <summary>
    /// 点火准入判据 —— 给定当前载重与本次产出,判「装不装得下」(AC-18-12 谓词面)。
    /// <para><b>纯函数</b>:零静态可变状态、零 I/O、零墙钟、零事件写入。</para>
    /// <para><b>语义</b>:逐行累加,每行都问 <see cref="CarryLoadSolver.CanCarry"/> ——
    /// 任一行装不下即整体拒绝(R-18-D:拒绝在时点,**不存在部分接受**;
    /// 部分接受 = 部分成功,破 ADR-005 的可序列化前提)。</para>
    /// </summary>
    public static class IgnitionAdmission
    {
        /// <summary>判本次点火的产出能否全部装入。</summary>
        /// <param name="currentLoad">点火前该玩家的载重(≥ 0;由 20 的 `CarryLoad` 求得)。</param>
        /// <param name="outputs">本次产出逐行的载重输入。<b>不得为 <c>null</c></b>
        /// (调用方漏传 ⇒ 抛,不视作「零产出」)。</param>
        /// <param name="capacity">容量上限(已校验 `&gt; 0` 的 <see cref="CarryCapacity"/>;
        /// 未定值在构造它时就已硬失败,本方法不做第二道校验)。</param>
        /// <returns>裁决结果;<c>Admitted == true</c> ⇒ 调用方可发 `Craft`。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="outputs"/> 为 null
        /// (fail-closed:见下方「为什么 null 必须抛」)。</exception>
        /// <exception cref="InvalidOperationException"><paramref name="currentLoad"/> &lt; 0,
        /// 或某行的 weight / qty 域错误 —— 本方法**不吞、不转译**(域错误是数据坏,不是「装不下」)。</exception>
        /// <remarks>
        /// <b>为什么 null 必须抛(2026-10-26 评审 B1 订正)</b>:本方法的前一版把
        /// <c>outputs == null</c> 与 <c>Count == 0</c> 合并成「零产出 ⇒ 恒装得下」的短路。
        /// 那是一个 **fail-open 捷径** —— 「真的没有产出」与「不知道有没有产出」被当成同一件事,
        /// 而本方法是 AC-18-12(BLOCKING)唯一的时点拒绝门。同文件的
        /// <see cref="FurnaceState.TryGetLatest"/> 对空解码器取 fail-closed(抛),
        /// 两处立场相反即说明前者是错的。
        /// <para><b>空列表(非 null)仍返回 <c>Admitted = true</c></b>,但那是 <c>for</c> 循环
        /// **零迭代**的自然结果,**不是**一条被显式保证的语义 —— 合法配方**不可能**零产出行
        /// (`item-database.md:399` 的烘焙期非空约束 + 逐条 `max(1, ·)` 的产出地板)。
        /// 保留它只为不把「21a 已排除的输入」变成第二个抛点。</para>
        /// </remarks>
        public static IgnitionAdmissionResult CanAdmit(
            long currentLoad,
            IReadOnlyList<OutputLoadLine> outputs,
            CarryCapacity capacity)
        {
            if (outputs == null)
                throw new ArgumentNullException(nameof(outputs),
                    "未传入产出行清单 —— 拒绝当作「零产出」放行(那是 AC-18-12 的静默失效)");

            // 负载重在这里判,而不是等每行进 `CanCarry`:
            // 空列表不进循环 ⇒ 否则 `CanAdmit(-5, 空)` 会静默放行。
            if (currentLoad < 0L)
                throw new InvalidOperationException(
                    $"[炮制] CarryLoad = {currentLoad} < 0 —— 载重不可能为负;投影或求和出了错");

            long load = currentLoad;
            for (int i = 0; i < outputs.Count; i++)
            {
                OutputLoadLine line = outputs[i];
                if (!CarryLoadSolver.CanCarry(load, line.UnitWeight, line.Qty, capacity))
                    return new IgnitionAdmissionResult(false, i);

                // 后态载重 = 前态 + 本行重量;同样只经 `ContainerWeightFold`(不另写乘法)。
                load = checked(load + ContainerWeightFold.InstanceWeight(line.UnitWeight, line.Qty));
            }

            return new IgnitionAdmissionResult(true, -1);
        }
    }
}
