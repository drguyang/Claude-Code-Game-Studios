// 权威来源:design/gdd/item-database.md
//   · §Core Rules 规则八(:202-228)—— 守恒律不变量 / 构建期上界 / 运行期整数域求值 / 损耗口径
//   · §Formulas F1 守恒上界块(:530-552)—— 两条文档反例的**唯一出处**(照录,不新造)
//     ⚠️ 反例一(:535):w_in = 10 / w_out = 9 / EFF_MAX = 1 / QTY_MULT_MAX = 2
//        —— 基数式 9 ≤ 10 过,含乘子运行期 18 > 10 击穿(三轮 blocking #1)
//     ⚠️ 反例二(:540):w_in = 10 / w_out = 6 / EFF_MAX = 1 / QTY_MULT_MAX = 1.5 / qty = 1
//        —— 聚合式 1.5 × 6 = 9 ≤ 10 过,运行期 Round(1 × 1.5) = 2 ⇒ 12 > 10 击穿(D-21-32)
//   · AC-21a-8(聚合上界)· AC-21a-39(运行期不变量)· AC-21a-65(逐条同形极值式 = 唯一硬门)
//   · ADR-006 §Decision 四(守恒律的域内表达)· §Decision 三(单一舍入模式)
//   · ADR-005(纯函数、整数定点域、可逐位重放)· ADR-025 §①(纯 sim 逻辑 → `Sim` 装配)
//
// ⚠️ **单一实现(本文件存在的理由)**:构建期门与运行期**共用同一份算术** ——
//    逐条极值式(AC-21a-65)直接调用 `RecipeSettlementSolver.OutputQty` /
//    `RecipeSettlementSolver.ActualConsumed`,即运行期那两个 `max(1, Round(qty × mult))` 与
//    `Ceil(qty / EFF)` 的**同一个函数体**。构建期门因此不可能与运行期「同形而不同实现」——
//    D-21-32 的缝(聚合式与逐条式不同形)在结构上被消除,而不是靠「两处小心抄一致」。
//
// ⚠️ **整数域**:全部比较在 `long` 原始整数域完成(ADR-006 §Decision 四)—— 零 float、零 double、
//    零 decimal、零 `BigInteger`、零 `System.Int128`。乘子与权重和一律**先乘后比**,不逐项舍入。
//
// ⚠️ **溢出即前提未证**:两侧乘积经 `TryMultiply` 守卫;溢出时返回 `false`(拒),
//    **不静默回绕**(AC-21a-64 / TR-itemdb-029 的先证不溢出前提由此显式化)。
//
// ⚠️ **域外一律返回 false,不抛**:本文件是**纯谓词**,供构建期门(错误列表)与运行期断言消费。
//    唯一的抛出来自被消费的 `ActualConsumed`(EFF ≤ 0)—— 故本文件在调用它**之前**先自检
//    `efficiencyMax.Raw > 0`,把该异常挡在门外(域外 ⇒ false,不让异常逃逸)。
//
// ⚠️ **零数值**:本文件不含任何调参字面量(AC-21a-48)—— 上界值一律经入参注入
//    (`RecipeSettlementConstants` 的 PascalCase 属性),或由调用方提供的纯函数给出。
//    权重查表函数由**调用方**提供(物品表归 21a 数据文件,本文件不持有物品表)。

using System;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>规则八守恒律的**构建期与运行期共用算术**(AC-21a-8 / 39 / 65)。
    /// <para><b>纯函数</b>:入参即全部输入 —— 两个条目数组 + 权重查表函数 + 上界值。
    /// 不读运行时对象、不写任何流、无静态可变态 ⇒ 同参数集逐位同判定。</para>
    /// <para><b>三种形态</b>(规则八 / §F1):</para>
    /// <list type="bullet">
    /// <item><see cref="AggregateUpperBoundHolds"/> —— **聚合式**(AC-21a-8)。两侧都用**基数**,
    /// 产出侧乘上界乘子。它是**必要非充分**条件(D-21-32),只作诊断/交叉检查,不作唯一硬门。</item>
    /// <item><see cref="PerLineExtremeBoundHolds"/> —— **逐条同形极值式**(AC-21a-65)。
    /// **唯一硬门** —— 与运行期同形(消费运行期的取整/上取整函数),故不可能是「必要非充分」。</item>
    /// <item><see cref="RuntimeInvariantHolds"/> —— **运行期不变量**(AC-21a-39)。
    /// 取**实际** EFF 与**实际** QtyMultiplier,断言规则八的不等式恒成立。</item>
    /// </list>
    /// <para>⚠️ <b>量纲</b>:两侧均为「重量」(D-21-19)—— 逐条乘 <c>weight</c> 最小单位个数,
    /// 否则「一粒丹 = 一斤药材」,不等式恒真且不约束任何东西(GDD 规则八 ⚠️ 单位口径)。</para>
    /// </summary>
    public static class ConservationSolver
    {
        // ══════════ 整数域原语 ══════════

        /// <summary>非负整数乘法守卫 —— AC-21a-64 的「先证不溢出」前提在**比较点**上的落地。
        /// <para>域外(任一因子为负)⇒ <c>false</c>(本判据只服务非负量:重量和与 raw 乘子);
        /// 乘积超 <c>long</c> ⇒ <c>false</c>,**不静默回绕**(回绕会让守恒判定给出假绿)。</para>
        /// <para>比较式 <c>a &gt; long.MaxValue / b</c> 本身不溢出(b &gt; 0 已由前置排除)。</para>
        /// </summary>
        /// <param name="a">因子甲(≥ 0)。</param>
        /// <param name="b">因子乙(≥ 0)。</param>
        /// <param name="product">乘积(仅当返回 <c>true</c> 时有意义)。</param>
        /// <returns><c>true</c> = 乘积可精确表示且无回绕。</returns>
        /// <example><c>TryMultiply(2, 3, out long p)</c> ⇒ <c>true</c>,<c>p = 6</c>;
        /// <c>TryMultiply(long.MaxValue, 2, out _)</c> ⇒ <c>false</c>(前提未证,不得回绕)。</example>
        public static bool TryMultiply(long a, long b, out long product)
        {
            product = 0L;
            if (a < 0L || b < 0L)
                return false;                        // 域外:非负量之外的输入不进本判据
            if (a == 0L || b == 0L)
                return true;                         // 积恰 0,无溢出之虞

            if (a > long.MaxValue / b)
                return false;                        // 超 int64 ⇒ 前提未证,显式拒

            product = a * b;
            return true;
        }

        // ══════════ AC-21a-8:聚合上界(必要非充分)══════════

        /// <summary>构建期**聚合**守恒上界(AC-21a-8 · D-21-21)。
        /// <para>判据(§F1 :530-533):</para>
        /// <code>
        /// QTY_MULT_MAX × Σ( weight × outputs_i.qty )  ≤  EFF_MAX × Σ( weight × inputs_j.qty )
        /// </code>
        /// <para>产出侧**必须含** <c>QtyMultiplier</c> 上界 —— 原稿两侧都用基数、漏了产出侧乘子,
        /// 于是构建期 <c>9 ≤ 10</c> 通过、运行期 <c>18 &gt; 10</c> 击穿(GDD :535 反例一)。</para>
        /// <para>⚠️ <b>本式是必要非充分</b>(D-21-32):它与运行期的**逐条** <c>max(1, Round(·))</c>
        /// 不同形,GDD :540 的反例二可击穿它 ⇒ 唯一硬门是
        /// <see cref="PerLineExtremeBoundHolds"/>,本式只作诊断与交叉检查。</para>
        /// </summary>
        /// <param name="outputs">产出项(基数;非 null、非空 —— 空产出由配方校验前置拒)。</param>
        /// <param name="inputs">投入项(基数;非 null、非空)。</param>
        /// <param name="itemWeight">权重查表函数 <c>item_key → weight</c>(int ≥ 1)。
        /// <b>调用方须提供纯函数</b>(物品表归 21a 数据文件;本文件不持有物品表,也不缓存)。</param>
        /// <param name="efficiencyMax">转化效率上限(其 raw 乘子;须 &gt; 0)。</param>
        /// <param name="qtyMultiplierMax">产出乘子上限(其 raw 乘子;须 &gt; 0)。</param>
        /// <returns><c>true</c> = 不等式成立(过);<c>false</c> = 违反**或**域外**或**前提未证。</returns>
        /// <example>反例一(<c>w_in = 10, w_out = 9, EFF_MAX = 1, QTY_MULT_MAX = 2</c>)
        /// ⇒ <c>2 × 9 = 18 &gt; 1 × 10 = 10</c> ⇒ <c>false</c>(GDD :535 照录)。</example>
        public static bool AggregateUpperBoundHolds(
            RecipeEntry[] outputs,
            RecipeEntry[] inputs,
            Func<ItemKey, int> itemWeight,
            Fix efficiencyMax,
            Fix qtyMultiplierMax)
        {
            if (itemWeight == null)
                return false;
            if (efficiencyMax.Raw <= 0L || qtyMultiplierMax.Raw <= 0L)
                return false;                        // 乘子域外 ⇒ 前提不可用(域门归 AC-21a-40/56)

            if (!TrySumBaseMass(outputs, itemWeight, out long producedBase))
                return false;
            if (!TrySumBaseMass(inputs, itemWeight, out long consumedBase))
                return false;

            if (!TryMultiply(qtyMultiplierMax.Raw, producedBase, out long left))
                return false;                        // 前提未证(AC-21a-64)⇒ 拒,不回绕
            if (!TryMultiply(efficiencyMax.Raw, consumedBase, out long right))
                return false;

            return left <= right;
        }

        // ══════════ AC-21a-65:逐条同形极值式(唯一硬门)══════════

        /// <summary><b>唯一硬门</b> —— 构建期**逐条同形极值式**(AC-21a-65 · D-21-32 落盘)。
        /// <para>判据(§F1 :545-548):</para>
        /// <code>
        /// Σ( weight_out × max(1, Round(outputs_i.qty × QTY_MULT_MAX)) )
        ///     ≤ EFF_MAX × Σ( weight_in × Ceil(inputs_j.qty / EFF_MAX) )
        /// </code>
        /// <para>⚠️ <b>同形的意义</b>:两侧的 <c>max(1, Round(·))</c> 与 <c>Ceil(· / EFF)</c>
        /// **不是重写的**,而是直接调用运行期那两个函数
        /// (<see cref="RecipeSettlementSolver.OutputQty"/> /
        /// <see cref="RecipeSettlementSolver.ActualConsumed"/>)——
        /// 构建期门与运行期**逐位同形**,「两式结论不一致」在结构上不可能发生。
        /// 聚合式(<see cref="AggregateUpperBoundHolds"/>)保留为**必要非充分**。</para>
        /// <para>⚠️ 产出侧的 <c>Round</c> 走 <c>ROUND_HALF_AWAY_FROM_ZERO</c>(ADR-006 §Decision 三)
        /// —— 缝恰在 <c>Round</c> 不在 <c>max(1,·)</c>(GDD :543)。</para>
        /// </summary>
        /// <param name="outputs">产出项(基数)。</param>
        /// <param name="inputs">投入项(基数)。</param>
        /// <param name="itemWeight">权重查表纯函数(int ≥ 1;非 null)。</param>
        /// <param name="efficiencyMax">转化效率上限(须 raw &gt; 0 —— 它是投入侧上取整的除数)。</param>
        /// <param name="qtyMultiplierMax">产出乘子上限(须 raw &gt; 0)。</param>
        /// <returns><c>true</c> = 过;<c>false</c> = 击穿**或**域外**或**前提未证。</returns>
        /// <example>反例二(<c>w_in = 10, w_out = 6, EFF_MAX = 1, QTY_MULT_MAX = 1.5, qty = 1</c>)
        /// ⇒ <c>Round(1 × 1.5) = 2</c> ⇒ <c>2 × 6 = 12 &gt; 1 × 10 = 10</c> ⇒ <c>false</c>(GDD :540 照录)。</example>
        public static bool PerLineExtremeBoundHolds(
            RecipeEntry[] outputs,
            RecipeEntry[] inputs,
            Func<ItemKey, int> itemWeight,
            Fix efficiencyMax,
            Fix qtyMultiplierMax)
        {
            if (itemWeight == null)
                return false;
            if (efficiencyMax.Raw <= 0L || qtyMultiplierMax.Raw <= 0L)
                return false;                        // EFF ≤ 0 是除数域外 —— 先挡在 ActualConsumed 之外

            if (!TrySumProducedMass(outputs, itemWeight, qtyMultiplierMax, out long produced))
                return false;
            if (!TrySumConsumedMass(inputs, itemWeight, efficiencyMax, out long consumed))
                return false;

            if (!TryMultiply(produced, Fix.OneRaw, out long leftScaled))
                return false;
            if (!TryMultiply(efficiencyMax.Raw, consumed, out long rightScaled))
                return false;

            return leftScaled <= rightScaled;
        }

        // ══════════ AC-21a-39:运行期不变量 ══════════

        /// <summary>运行期守恒不变量(AC-21a-39 · 规则八 :203-207):
        /// 对**任意合法配方、任意 EFF ≤ EFF_MAX、任意 QtyMultiplier**
        /// <code>
        /// Σ( weight × OutputQty_i )  ≤  EFF_MAX × Σ( weight × ActualConsumed_j )
        /// </code>
        /// <para>⚠️ <b>RHS 的乘子恒取 <paramref name="efficiencyMax"/></b> —— 律的右端是
        /// <c>EFF_MAX × Σ(...)</c>,对 EFF 的扫描**只落在左端**(<c>ActualConsumed</c> 的除数)。
        /// 不得把 RHS 换成实际 <c>EFF</c>:那会把「EFF 越小越宽裕」的回报方向弄反
        /// (<c>EFF ↓ ⇒ 实耗 ↑ ⇒ RHS ↑</c>,恒成立才有意义)。</para>
        /// <para>两侧同量纲(重量):产出侧含 QtyMultiplier、投入侧取**实耗**非基数。
        /// 整数域先乘后比、禁逐项舍入(GDD :215-217:逐项舍入会让同一配方在主机/客户端
        /// **合法性与否翻转**)。</para>
        /// </summary>
        /// <param name="outputs">产出项(基数)。</param>
        /// <param name="inputs">投入项(基数)。</param>
        /// <param name="itemWeight">权重查表纯函数(int ≥ 1;非 null)。</param>
        /// <param name="efficiencyMax">转化效率上限(律的右端乘子;须 raw &gt; 0)。</param>
        /// <param name="efficiency">本次结算的**实际** EFF —— 须 raw &gt; 0 且 ≤ <c>EFF_MAX</c>。</param>
        /// <param name="qtyMultiplier">本次结算的**实际** QtyMultiplier(钳制后)。</param>
        /// <returns><c>true</c> = 不变量成立;<c>false</c> = 违反**或**域外**或**前提未证。</returns>
        /// <example>零损耗点(<c>EFF = EFF_MAX = 1</c>,基数式配方)⇒ <c>true</c>,且取等号。</example>
        public static bool RuntimeInvariantHolds(
            RecipeEntry[] outputs,
            RecipeEntry[] inputs,
            Func<ItemKey, int> itemWeight,
            Fix efficiencyMax,
            Fix efficiency,
            Fix qtyMultiplier)
        {
            if (itemWeight == null)
                return false;
            if (efficiencyMax.Raw <= 0L || efficiency.Raw <= 0L)
                return false;                        // EFF ≤ 0 域外:ActualConsumed 会抛,此处先拒

            if (!TrySumProducedMass(outputs, itemWeight, qtyMultiplier, out long produced))
                return false;
            if (!TrySumConsumedMass(inputs, itemWeight, efficiency, out long consumed))
                return false;

            if (!TryMultiply(produced, Fix.OneRaw, out long leftScaled))
                return false;
            if (!TryMultiply(efficiencyMax.Raw, consumed, out long rightScaled))
                return false;

            return leftScaled <= rightScaled;
        }

        // ══════════ 私有:三条重量和(全部整数域,溢出即 false)══════════

        /// <summary>Σ( weight × qty ) —— **基数**侧(聚合式用)。</summary>
        private static bool TrySumBaseMass(
            RecipeEntry[] entries, Func<ItemKey, int> itemWeight, out long total)
            => TrySumMass(entries, itemWeight, out total, null, new Fix(0L));

        /// <summary>Σ( weight × OutputQty_i ) —— 产出侧(含乘子,逐条 <c>max(1, Round(·))</c>)。</summary>
        private static bool TrySumProducedMass(
            RecipeEntry[] entries, Func<ItemKey, int> itemWeight, Fix qtyMultiplier, out long total)
            => TrySumMass(entries, itemWeight, out total, qtyMultiplier, new Fix(0L));

        /// <summary>Σ( weight × ActualConsumed_j ) —— 投入侧(取**实耗**非基数)。</summary>
        private static bool TrySumConsumedMass(
            RecipeEntry[] entries, Func<ItemKey, int> itemWeight, Fix efficiency, out long total)
            => TrySumMass(entries, itemWeight, out total, null, efficiency);

        /// <summary>三条和的共同实现:逐条求该条的**量**(基数 / 产出 / 实耗),乘权重后累加。
        /// <para>域外一律 <c>false</c>(不抛):数组 null / 空、权重 ≤ 0、基数 ≤ 0、
        /// 逐项积或累加超 <c>long</c>。</para>
        /// <para>⚠️ <c>producedMultiplier</c> 与 <c>consumedDivisor</c> 二者**恰有一个**被使用
        /// (<c>consumedDivisor.Raw &gt; 0</c> 表示本侧取实耗)—— 调用方已在上游自检 EFF &gt; 0,
        /// 故此处 <c>ActualConsumed</c> 不会抛。</para>
        /// </summary>
        private static bool TrySumMass(
            RecipeEntry[] entries,
            Func<ItemKey, int> itemWeight,
            out long total,
            Fix? producedMultiplier,
            Fix consumedDivisor)
        {
            total = 0L;
            if (entries == null || entries.Length == 0)
                return false;                        // 空侧 = 凭空造物 / 凭空销毁,由配方校验前置拒

            long running = 0L;
            for (int i = 0; i < entries.Length; i++)
            {
                RecipeEntry entry = entries[i];
                if (entry.Qty <= 0)
                    return false;                    // 基数 ≤ 0 域外(AC-21a-7/16 前置拒)

                int unitMass = itemWeight(entry.Key);
                if (unitMass <= 0)
                    return false;                    // 权重未填 / 域外 ⇒ 量纲不可通约,不得当 0 用

                long quantity;
                if (producedMultiplier.HasValue)
                {
                    // 逐条 max(1, Round(qty × mult)) —— 运行期同一函数体(同形,非重写)
                    quantity = RecipeSettlementSolver.OutputQty(entry.Qty, producedMultiplier.Value);
                }
                else if (consumedDivisor.Raw > 0L)
                {
                    // 逐条 Ceil(qty / EFF) —— 运行期同一函数体(同形,非重写)
                    quantity = RecipeSettlementSolver.ActualConsumed(entry.Qty, consumedDivisor);
                }
                else
                {
                    quantity = entry.Qty;            // 基数侧
                }

                if (!TryMultiply(unitMass, quantity, out long lineMass))
                    return false;
                if (!TryAccumulate(running, lineMass, out long next))
                    return false;                    // 累加溢出 ⇒ 前提未证(AC-21a-64),不回绕
                running = next;
            }

            total = running;
            return true;
        }

        /// <summary>非负累加守卫(超 <c>long</c> ⇒ <c>false</c>,不回绕)。</summary>
        private static bool TryAccumulate(long total, long term, out long sum)
        {
            sum = 0L;
            if (term < 0L || total < 0L)
                return false;
            if (total > long.MaxValue - term)
                return false;
            sum = total + term;
            return true;
        }
    }
}
