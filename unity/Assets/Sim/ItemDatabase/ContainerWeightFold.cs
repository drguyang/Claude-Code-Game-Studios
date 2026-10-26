// 权威来源:design/gdd/inventory-and-items.md 规则三 推荐 4(:255-260 容器折重结清口径)
//          · 规则四 / F-20.1(:435-450 载重整数求和)
//          · OQ-20-4(✅ 2026-09-19 结清:折重 = **空箱自重 + 子件逐件**)
//          · AC-20-04 折重不变量(story-003)· BL-7③ / BL-28(P0 范围门)
//          · 21a Schema E(:384 容器 qty/quality 恒 1)· ADR-006 §Decision 一 D-21-17(weight = int)
//          · ADR-025 §①(纯 sim 逻辑 → `Sim` 装配,引用集 {BCL, Sim.Contracts})
//
// ⚠️ **口径(非数值)**:`InstanceWeight(container) = ItemDef.weight(container.key) × 1`(**空箱自重**),
//    **子件逐件单独计入** —— `children` 只承载归属,**不承载重量二次折算**。
//    这正是 OQ-20-4 的结清口径,且与 P1a 放开嵌套后一致(逐件计不会双重计数)。
//
// ⚠️ **反口径**(本文件**不得**实现的另一种读法):「容器 weight 已含子件」——
//    若那样实现,子件会既在箱重里又被逐件累加 = **双计**。TR-inventory-015 的语义即在此划清。
//
// ⚠️ **整数域、先乘后加**:全程 `long` 中间量 + `checked` 溢出显式失败(禁静默回绕,
//    承 ADR-012 F7 的有符号溢出纪律)。禁 `float` / `double` / `decimal` / `BigInteger`
//    (门 A + ADR-006)。
//
// ⚠️ **零数值**:`unitWeight` / `qty` 全部参数化注入,本文件不承载任何具体数值(数值归用户)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>「某物品 × 若干件」的二元组 —— 折重与载重的求和项。
    /// <para><b>不是</b>流载荷:它是纯算术入参(`weight` + `qty` 已在调用方查表得到)。</para>
    /// </summary>
    public readonly struct ItemStackAmount
    {
        /// <summary>单件重量(<c>ItemDef.weight</c>,int &gt; 0 —— D-21-17 的整数最小单位个数)。</summary>
        public readonly int UnitWeight;

        /// <summary>件数(int ≥ 1)。</summary>
        public readonly int Qty;

        /// <summary>构造一个求和项。</summary>
        /// <param name="unitWeight">单件重量(int &gt; 0)。</param>
        /// <param name="qty">件数(int ≥ 1)。</param>
        public ItemStackAmount(int unitWeight, int qty)
        {
            UnitWeight = unitWeight; Qty = qty;
        }
    }

    /// <summary>容器折重(AC-20-04 折重不变量 · OQ-20-4 结清口径)。
    /// <para><b>纯算术核</b>:入参即全部输入,**不查物品表** —— 查表由调用方完成
    /// (与 <see cref="ConservationSolver"/> 的 <c>Func&lt;ItemKey, int&gt;</c> 取径同款:
    /// 算术核与数据源解耦,核本身可独立反证)。</para>
    /// <para><b>与 Story 004 共件</b>:<see cref="InstanceWeight"/> 就是
    /// <c>CarryLoad(p) = Σ InstanceWeight(inst)</c> 的被求和项 —— Story 004 直接调用本件,
    /// **不得**另写一份 `weight × qty`(卡面「与 Story 004 的 CarryLoad 共用加法件」)。</para>
    /// </summary>
    public static class ContainerWeightFold
    {
        /// <summary>单个实例的重量 = <c>unitWeight × qty</c>(GDD F-20.1,整数域)。
        /// <para>⚠️ 容器调用时 <paramref name="qty"/> 恒 1(§Schema E)⇒ 结果 = **空箱自重**;
        /// 子件**不在此处折算**(见 <see cref="FoldWeight"/>)。</para>
        /// </summary>
        /// <param name="unitWeight">单件重量(int &gt; 0)。</param>
        /// <param name="qty">件数(int ≥ 1)。</param>
        /// <returns>该实例的重量(≥ 1)。</returns>
        /// <exception cref="InvalidOperationException"><paramref name="unitWeight"/> &lt; 1 或
        /// <paramref name="qty"/> &lt; 1(域错误 —— 越界校验 = AC-21a-15);
        /// 或乘积超 <c>long</c>(真实数据被 AC-21a-64 上界前置拦下,此处为纯函数自保护的兜底)。</exception>
        /// <example><c>InstanceWeight(2, 5)</c> ⇒ <c>10</c>;<c>InstanceWeight(7, 1)</c> ⇒ <c>7</c>(空箱)。</example>
        public static long InstanceWeight(int unitWeight, int qty)
        {
            if (unitWeight < 1 || qty < 1)
                throw new InvalidOperationException(
                    $"[折重] weight / qty 均须 ≥ 1 —— 实际 ({unitWeight}, {qty});" +
                    "域错误归数值轮 / 校验层(AC-21a-15)");

            try
            {
                return checked((long)unitWeight * qty);   // int × int ≤ 2^62 ⇒ 落 long 必不溢出
            }
            catch (OverflowException)
            {
                throw new InvalidOperationException(
                    $"[折重] {unitWeight} × {qty} 超 long 承载 —— " +
                    "真实数据须被 AC-21a-64 上界前置拦下(请修正数值上界)");
            }
        }

        /// <summary>容器折重 = <b>空箱自重 + 子件逐件</b>(OQ-20-4 结清口径)。
        /// <para><b>公式</b>:<c>FoldWeight(w0, children) = w0 + Σ(children[i].UnitWeight × children[i].Qty)</c></para>
        /// <para>⚠️ 关键在**加法而不是乘法**:子件是**逐件并到总和上**,不是「容器重 × 件数」。
        /// 这就是与「容器 weight 已含子件」那种读法的分界 —— 后者会双计。</para>
        /// <para><b>可逆性**:拆出全部子件后再折回,结果与拆前逐位相等
        /// (加法结合律在整数域成立 —— 这正是 ADR-006 要求整数域求值的理由之一,
        /// <c>float</c> 域下「相加与顺序无关」是假的,见 <c>RecipeSettlementSolver.cs:226</c>)。</para>
        /// </summary>
        /// <param name="emptyContainerWeight">空箱自重(单件重量 × 1;≥ 1)。</param>
        /// <param name="children">子件求和项(**顺序不影响结果**;可为空 = 空箱)。</param>
        /// <returns>折重(≥ 1)。</returns>
        /// <exception cref="InvalidOperationException"><paramref name="emptyContainerWeight"/> &lt; 1;
        /// 任一子项域错误;或累加超 <c>long</c>(AC-21a-64 上界前置拦下,此处兜底)。</exception>
        /// <exception cref="ArgumentNullException"><paramref name="children"/> 为 null
        /// (拒以 null 冒充空箱 —— 空箱须显式传空集)。</exception>
        /// <example>空箱 7 + 子件 (2×3)、(5×1) ⇒ <c>7 + 6 + 5 = 18</c>;
        /// <c>FoldWeight(7, 空)</c> ⇒ <c>7</c>(空箱 = 自重)。</example>
        public static long FoldWeight(int emptyContainerWeight, IReadOnlyList<ItemStackAmount> children)
        {
            if (emptyContainerWeight < 1)
                throw new InvalidOperationException(
                    $"[折重] 空箱自重 = {emptyContainerWeight} < 1 —— 域错误(AC-21a-15)");
            if (children == null)
                throw new ArgumentNullException(nameof(children),
                    "子件集非空禁 null —— 空箱传 Array.Empty<ItemStackAmount>()(不静默当空箱)");

            long total = emptyContainerWeight;   // ① 空箱自重先入,不给「子件已含在自重里」留读法
            for (int i = 0; i < children.Count; i++)
            {
                // ② 子件逐件:先乘(qty 份)后加 —— 顺序与 ADR-006 的「先乘后加」一致。
                long childWeight = InstanceWeight(children[i].UnitWeight, children[i].Qty);
                try
                {
                    total = checked(total + childWeight);
                }
                catch (OverflowException)
                {
                    throw new InvalidOperationException(
                        $"[折重] 累加到第 {i} 个子件时超 long 承载 —— " +
                        "真实数据须被 AC-21a-64 上界前置拦下(请修正数值上界)");
                }
            }
            return total;
        }
    }
}
