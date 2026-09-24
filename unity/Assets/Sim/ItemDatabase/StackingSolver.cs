// 权威来源:design/gdd/item-database.md §Formulas F4(:657-688)+ §Core Rules 规则六(:185-190)
//          · AC-21a-32(堆叠键 = (item_key, quality))· AC-21a-33(溢出新实例,总量守恒)
//          · AC-21a-64(守恒律整数域求值的上界前提 —— 先证不溢出)
//          · ADR-005(确定性模拟 —— 纯函数、整数域、可重放)
//          · ADR-006(weight / stack_max = int 计数,非 Fix —— D-21-17 · 舍入整数域内完成)
//          · ADR-025 §①(纯 sim 逻辑 → `Sim` 装配,引用集 {BCL, Sim.Contracts})
//
// ⚠️ **范围锁**:本故事只覆盖 AC-33 的**溢出切分与总量守恒**(一个堆 + 一次加入的纯函数)。
//    容器实例的 children 闭包 / 容器守恒(AC-34/58)归 Story 010;堆叠槽位实体 / 库存状态归 20。
// ⚠️ **Container 实例不参与堆叠**(qty/quality 恒 1,GDD §Schema E)—— 由
//    <see cref="CanStack"/> 检查 <see cref="ItemInstance.Children"/> 判定。
// ⚠️ **零数值**:weight / stack_max / MAX_QUALITY 的取值全部参数化注入,本文件不承载任何
//    具体数值(数值待用户,AC-64 的上界值属数值旋钮 TR-itemdb-029)。
// ⚠️ **不写任何流**(AC-33 只管溢出后各堆 qty;堆叠动作进世界流归 20 / Story 010)。

using System;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary><c>SplitStack</c> 的一次切分结果(AC-21a-33 的出参形状)。
    /// <para>总量守恒陈述:<c>CurrentFinalQty + Δ 落到新堆的全部 = 原 c + 原 Δqty</c>
    /// (调用方由此物化新实例,Σqty 全容器守恒)。</para></summary>
    public readonly struct StackSplitResult
    {
        /// <summary>原堆填入后的 qty(∈ [1, stackMax];c + Δ ≤ stackMax 时 = c + Δ,否则 = stackMax)。</summary>
        public readonly int CurrentFinalQty;

        /// <summary>溢出总量(> 0 表示 c + Δ 超过 stackMax;= 0 表示未溢出)。</summary>
        public readonly int OverflowQty;

        /// <summary>溢出部分切出的**完整满堆**数(c + Δ − stackMax 元溢出中完整新堆个数)。</summary>
        public readonly int NewFullStacks;

        /// <summary>溢出部分切出后**余数堆**的 qty(0 = 整除,无余数堆)。</summary>
        public readonly int PartialStackQty;

        /// <summary>构造切分结果(求解器专用入口)。</summary>
        /// <param name="currentFinalQty">原堆填入后的 qty。</param>
        /// <param name="overflowQty">溢出总量。</param>
        /// <param name="newFullStacks">完整满堆数。</param>
        /// <param name="partialStackQty">余数堆 qty。</param>
        public StackSplitResult(
            int currentFinalQty, int overflowQty, int newFullStacks, int partialStackQty)
        {
            CurrentFinalQty = currentFinalQty;
            OverflowQty = overflowQty;
            NewFullStacks = newFullStacks;
            PartialStackQty = partialStackQty;
        }
    }

    /// <summary>F4 堆叠与重量求解器(AC-21a-32/33/64)。
    /// <para><b>纯函数</b>:入参即全部输入,不读运行时对象、不写事件流 —— 同参数集逐位产出
    /// 同结果(可重放)。</para>
    /// <para><b>单一实现</b>(承 GDD §Formulas 权威边界 Q1 裁决的推广):堆叠判定与溢出切分
    /// 公式体**仅存在于本文件**;20 库存调用本求解器,不得自建第二份切分逻辑。
    /// 扫描面一致性:若在别处复制 <c>SplitStack</c> / <c>CanStack</c> 的公式体,
    /// Story 004 的 EditMode 便利性检查调用同一谓词。</para></summary>
    public static class StackingSolver
    {
        /// <summary>F4:<c>StackKey = (item_key, quality)</c> 的堆叠可合并判定(AC-21a-32,规则六)。
        /// <para>可合并 ⇔ 全部满足:① <c>stackMax &gt; 1</c>(stackMax = 1 永不合并);
        /// ② 两实例均**非容器**(容器 qty/quality 恒 1,不参与堆叠,GDD §Schema E);
        /// ③ 品级相等;④ 复合主键相等(<see cref="ItemKey.Equals(ItemKey)"/>,序数)。</para>
        /// <para>两实例的 key 相等且 quality 相等 = 同一 <c>StackKey</c> ——
        /// 本判定是「堆叠键相等」的集中表达,不在调用方重复实现(AC-21a-32)。</para>
        /// </summary>
        /// <param name="a">实例甲。</param>
        /// <param name="b">实例乙。</param>
        /// <param name="stackMax">该 item_key 的堆叠上限(int ≥ 1;数值待用户,越界校验归 Story 006)。</param>
        /// <returns><c>true</c> = 可合并(同 StackKey 且双非容器且可堆叠)。</returns>
        /// <example><c>CanStack(a, b, 99)</c>(同 K 同 q)⇒ <c>true</c>;
        /// <c>CanStack(a, b, 1)</c> ⇒ <c>false</c>(stackMax = 1 永不合并)。</example>
        public static bool CanStack(in ItemInstance a, in ItemInstance b, int stackMax)
            => stackMax > 1
               && a.Children.Length == 0
               && b.Children.Length == 0
               && a.Quality == b.Quality
               && a.Key.Equals(b.Key);

        /// <summary>F4:<c>SplitStack</c> —— 把 <c>Δqty</c> 加入当前堆 qty <c>c</c>(上限
        /// <c>stack_max = M</c>),切分「原堆填入量 + 溢出部分成堆」(AC-21a-33)。
        /// <para>溢出规则:<c>c + Δ ≤ M</c> ⇒ 无溢出;<c>c + Δ &gt; M</c> ⇒ 原堆补满到 <c>M</c>,
        /// 溢出 <c>c + Δ − M</c> 每 <c>M</c> 一个完整新堆,余数一个部分堆(逐次可多段溢出;
        /// 整除则无余数堆)。<b>Σqty 恒守恒</b>:原堆最终 qty + 各新堆 qty 之和 = c + Δ。</para>
        /// <para>⚠️ 谓词先加法用 <c>long</c> 累计(<c>int</c> 加法可能溢出 —— <c>Δqty</c> 无上界保证);
        /// 溢出总量超 <c>int.MaxValue</c> 显式抛(真实数据被 AC-21a-64 上界前置拦下,此处为
        /// 纯函数自保护的兜底,不留静默回绕);断言结果全部落回 <c>int</c>(出参恒 ≤ M,
        /// <c>M</c> 是 int ⇒ 切分后各量安全)。</para>
        /// </summary>
        /// <param name="currentQty">当前堆 qty <c>c</c>(≥ 1)。</param>
        /// <param name="addQty">加入量 <c>Δqty</c>(≥ 1)。</param>
        /// <param name="stackMax">堆叠上限 <c>M</c>(≥ 1)。</param>
        /// <returns>切分结果(见 <see cref="StackSplitResult"/>)。</returns>
        /// <exception cref="InvalidOperationException">任一入参 &lt; 1(域外;上限校验归 Story 006)。</exception>
        /// <example><c>SplitStack(3, 5, 6)</c> ⇒ <c>{ CurrentFinalQty = 6, Overflow = 2,
        /// NewFullStacks = 0, PartialStackQty = 2 }</c>;
        /// <c>SplitStack(1, 13, 5)</c> ⇒ <c>{ 5, 9, 1, 4 }</c>(先 1 满堆 + 余 4)。</example>
        public static StackSplitResult SplitStack(int currentQty, int addQty, int stackMax)
        {
            if (currentQty < 1 || addQty < 1 || stackMax < 1)
                throw new InvalidOperationException(
                    $"SplitStack 入参域:currentQty/addQty/stackMax 均 ≥ 1 —— 实际 " +
                    $"({currentQty}, {addQty}, {stackMax});域错误归数值轮 / 校验层(Story 006)。");

            long total = (long)currentQty + addQty;          // int 加法可溢出的运行期证明点
            if (total <= stackMax)
                return new StackSplitResult((int)total, 0, 0, 0);

            long overflow = total - stackMax;                // > 0
            if (overflow > int.MaxValue)
                throw new InvalidOperationException(
                    $"SplitStack 溢出总量 {overflow} 超 int 承载(真实数据被 AC-21a-64 上界前置拦下;请修正配方上界数值)。");
            int newFullStacks = (int)(overflow / stackMax);
            int partial = (int)(overflow % stackMax);        // 0 = 整除无余数堆
            return new StackSplitResult(stackMax, (int)overflow, newFullStacks, partial);
        }

        /// <summary>AC-21a-64 的上界判据 —— 守恒律整数域求值的**前提证明**:
        /// 最坏组合(配方与实例的最大 <c>weight</c> / 最大 <c>stack_max</c> / 配方条目数上限同时取满)
        /// 求 <c>Σ(weight × ItemDef.weight × Qty)</c> 是否 ≤ <c>long.MaxValue</c>。
        /// <para>⚠️ <b>先证不溢出再比</b>:中间积是 <c>int × int ≤ (2³¹−1)² ≈ 2⁶² &lt; 2⁶³</c>
        /// ⇒ 先乘落 <c>long</c> 安全;再以「除法比较」替代乘法,比较步骤本身不溢出
        /// (<c>entries ≤ long.MaxValue / perStack</c>)。<b>不用 BigInteger / float</b>。</para>
        /// <para>⚠️ 数值旋钮:三个上界的具体值归 21a 数值轮(TR-itemdb-029);
        /// 本函数只提供判据算子,测试参数化注入、不断言具体定值。</para>
        /// </summary>
        /// <param name="maxItemWeight">最大单件 <c>weight</c>(≥ 1)。</param>
        /// <param name="maxStackMax">最大 <c>stack_max</c>(≥ 1)。</param>
        /// <param name="maxEntries">配方条目数上限(≥ 1;最坏 = 每条都满堆)。</param>
        /// <returns><c>true</c> = 最坏 Σ 不溢出 <c>long</c>;`false` = 超声明上界(构建期硬失败,AC-21a-64)。</returns>
        /// <example>三个上界取尽所有合理值 ⇒ <c>true</c>;某界抬超 ⇒ <c>false</c>(数值轮调整)。</example>
        public static bool WeightedTotalFitsInt64(int maxItemWeight, int maxStackMax, int maxEntries)
        {
            if (maxItemWeight < 1 || maxStackMax < 1 || maxEntries < 1)
                return false;   // 域外 = 非合法界(负 / 零 weight 已由 AC-21a-15 前置拒)

            long perStack = (long)maxItemWeight * maxStackMax;   // ≤ 2^62,不溢出
            return maxEntries <= long.MaxValue / perStack;       // 除法比较,替代乘法防溢出
        }
    }
}