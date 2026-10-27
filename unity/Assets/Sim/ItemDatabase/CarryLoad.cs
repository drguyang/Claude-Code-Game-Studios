// 权威来源:design/gdd/inventory-and-items.md
//   · 规则四 F-20.1(:273-290 载重整数求和 / `CarryLoad` 定义)
//   · 规则四 F-20.2(:464 `CanCarry ⟺ CarryLoad + InstanceWeight ≤ CARRY_CAP`)
//   · AC-20-08(零浮点 · weight 参与整数乘加)· AC-20-09(硬不变量)
//   · AC-20-13(`CARRY_CAP` 未定值 ⇒ 装载期硬失败,禁 null 兜底 / 默认容量)
//   · AC-20-18(`CarryRatioDto = {carry_load, cap}` 整数对;满档 ⇔ `carry_load > cap − W_MIN`)
//   · AC-20-24(只存在一个容量真值;零 `RemainingSlack` 类派生字段)
//   · AC-20-21(存在性门 = 自指谓词,门本体见 `CarryCapExistenceGate.cs`)
//   · 规则三 推荐 4(:255-265 容器折重 = 空箱自重 + 子件逐件;OQ-20-4 结清)
//   · ADR-006 §Decision 一 D-21-17(`weight` / `stack_max` 是 int 计数,移出 `Fix` 解析集)
//   · ADR-014 §五(装载期硬失败纪律:E-13 同款 —— 抛异常,不 null 兜底)
//   · ADR-025 §①(`Sim` 引用集恰 = {BCL, Sim.Contracts})
//
// ⚠️ **零数值**:本文件不承载任何具体重量 / 容量数值。`CARRY_CAP`(GDD :283 已裁 60,值可调)
//    与 `W_MIN`(= `min{ItemDef.weight}`)全部由调用方注入 —— 与 `RecipeSettlementConstants`
//    同款纪律(AC-21a-48:代码里零字面量 ⇒ 不存在「改了 GDD 忘了改代码」的漂移面)。
//
// ⚠️ **为什么入参是 `Func<int,int> weightOfOrdinal` 而不是 `Func<ItemKey,int>`**:
//    `InventoryRow`(S20-10 的 fold 投影)只携 `ItemOrdinal`,**不携** `ItemKey` 的
//    `base_id` / `state` 两段。若本件签名收 `ItemKey`,调用方就必须在每次求和时把
//    ordinal 反解成 key —— 那是第二个映射真源。直接收 ordinal 的 weight 查询,
//    把「ordinal → ItemDef」的权威留在**物品表**里(21a),本文件不猜。
//
// ⚠️ **零隐式状态**:本类**没有**任何静态可变字段 / `[ThreadStatic]` / 单例。
//    全部输入经参数传入(项目标准「dependency injection over singletons」)。
//
// ⚠️ **容器折重的两条路(2026-10-26 用户裁定「CarryLoad 接受可选子件表,双路都有夹具」)**:
//    GDD 推荐 4 写「容器 = 空箱自重,**子件逐件单独计入 `CarryLoad`**」。而 `InventoryRow`
//    **不携 `children`** —— 因为 fold 只认七类世界流 Kind,而 P0 **没有任何「进 / 出容器」
//    事件载体**(`inventory-and-items.md:562` 已登记为「形名失配第六次实例」)。
//    ⇒ 实现分两路,且**两路都有夹具**:
//      ① **单层求和**(`childrenByContainer == null`)—— 每个投影行各自计重。
//         若子件**本来就有自己的投影行**,它已被计入 ⇒ 本路**天然就是**
//         「空箱自重 + 子件逐件」,**不会双计**。
//      ② **显式子件表**(传 `childrenByContainer`)—— 当调用方手上有实例本体
//         (`ItemInstance.Children`)时,把子件逐件并进来。
//    ⚠️ **② 的双计风险是显式的**,判据见 <see cref="CarryLoadBreakdown.DoubleCountedChildWeight"/>。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>`CARRY_CAP` 未定值 / 非法时抛出的具名异常(AC-20-13 · ADR-014 §五 硬失败纪律)。
    /// <para>⚠️ **不是** `ArgumentException` 的子类:该异常表示「数据未就绪 / 缺载体」,
    /// 而非「调用方传错了参数」—— 两者在装配期日志里必须可区分。</para>
    /// </summary>
    public sealed class CarryCapUndefinedException : Exception
    {
        /// <summary>构造。</summary>
        /// <param name="message">诊断信息(须含实际收到的值与拒收理由)。</param>
        public CarryCapUndefinedException(string message) : base(message) { }
    }

    /// <summary>某实例的单件重量查不到 / 非法时抛出的具名异常(AC-20-13 邻域 · 缺载体纪律)。
    /// <para><b>为什么与 <see cref="CarryCapUndefinedException"/> 分开</b>:两者是**不同数据源**
    /// 的缺位(容量表 vs 物品表),装配期排障时必须一眼分得开。合成一个类型会把
    /// 「物品表缺条目」误诊成「容量未注入」。</para>
    /// </summary>
    public sealed class ItemWeightUndefinedException : Exception
    {
        /// <summary>构造。</summary>
        /// <param name="message">诊断信息(须含 ordinal / 实得值)。</param>
        public ItemWeightUndefinedException(string message) : base(message) { }
    }

    /// <summary>载重读数的整数对 —— 20 对 42 / 52 的**唯一**容量出口(AC-20-18 · AC-20-24)。
    /// <para><b>恰两个字段</b>,零 `float`、零百分比、零分档枚举、**零 `RemainingSlack`**
    /// (AC-20-24:slack 由 42 自行 <c>cap − carry_load</c> 相减;20 侧多给一个字段
    /// 就等于多一个可以与 `CARRY_CAP` 脱节的**第二容量真值**)。</para>
    /// <para>住 `Assets/Sim/ItemDatabase` ⇒ 已被 `InventoryDtoFloatGate`(AC-20-15)的
    /// `ScanDirsRelative` 覆盖,浮点字段会在构建门被抓。</para>
    /// <para>⚠️ <b>`CarryLoad` 字段类型 = `long`,与 GDD `:302` 的 `carry_load: int` 不一致</b>
    /// —— 详见 story-004 §Completion Notes 的 ADVISORY 偏差登记。理由:F-20.1 的**求和结果**
    /// 定义域是 **int64**(GDD `:449` 的溢出界改判),若 DTO 收窄到 int32,则「载重超 int32」
    /// 这一异常状态会**静默回绕**成一个小正数(本项目已抓过五次的静默失败形态)。
    /// 收窄只省 4 字节,代价是新增一条无守卫的回绕路径 ⇒ 取 long。</para>
    /// </summary>
    public readonly struct CarryRatioDto
    {
        /// <summary>当前载重(GDD F-20.1 的 `CarryLoad`;整数最小单位个数;int64 承 `:449` 改判)。</summary>
        public readonly long CarryLoad;

        /// <summary>容量上限(`CARRY_CAP`;**唯一**容量真值,AC-20-24)。</summary>
        public readonly int Cap;

        /// <summary>构造读数对。</summary>
        /// <param name="carryLoad">当前载重(≥ 0)。</param>
        /// <param name="cap">容量上限(&gt; 0;非法值在构造 <see cref="CarryCapacity"/> 时已被拒)。</param>
        public CarryRatioDto(long carryLoad, int cap)
        {
            CarryLoad = carryLoad; Cap = cap;
        }
    }

    /// <summary>载重求和的**可诊断**结果 —— 除总量外把两条静默失败模式显式报出来。
    /// <para>两条都是**静默**失败:① 子件既在投影行里、又被子件表重复并入(**双计** ⇒ 总量偏大
    /// ⇒ 玩家被无理由挡住);② 容器行在、子件在投影里查不到(**漏计** ⇒ 总量偏小
    /// ⇒ 玩家多带东西)。故做成可断言字段,而不是静默求和。</para>
    /// </summary>
    public readonly struct CarryLoadBreakdown
    {
        /// <summary>总载重(= 各行 `InstanceWeight` 之和 + 子件表并入量)。</summary>
        public readonly long Total;

        /// <summary>由**子件表**并入的重量(单层求和路径下恒 0)。</summary>
        public readonly long ChildWeightAdded;

        /// <summary>同一子件**已有真实持有者**、又被子件表并入 ⇒ 其重量在全局载重里被算两遍的量。
        /// <para>⚠️ <b>非零即调用方误用</b>:子件既已被某玩家 `DropClaimed`(有投影行),
        /// 就不该再作为「容器内的子件」并进来 —— 那说明子件表与本玩家的投影面重叠了。
        /// 本字段存在是为了让误用**可见**,而不是让核算出一个「看起来还行」的数。</para>
        /// <para>判定用 <c>Owner != InventoryProjection.NoOwner</c>(**不是** <c>Owner == owner</c>):
        /// 归属**别人**的子件同样已被计入**那个人的**载重,并进来同样是双计 —— 只是偏的是全局账
        /// 而非本次求和。两种都算。</para>
        /// </summary>
        public readonly long DoubleCountedChildWeight;

        /// <summary>子件在投影里**查不到** ⇒ 其重量**无从得知**;本字段记录此类子件 id 的个数。
        /// <para>⚠️ <b>非零 ⇒ 总量偏小</b>(漏计)。</para>
        /// </summary>
        public readonly int OrphanChildCount;

        /// <summary>构造。</summary>
        /// <param name="total">总量。</param>
        /// <param name="childWeightAdded">子件表并入量。</param>
        /// <param name="doubleCountedChildWeight">被双计的量。</param>
        /// <param name="orphanChildCount">孤儿子件个数。</param>
        public CarryLoadBreakdown(long total, long childWeightAdded,
                                  long doubleCountedChildWeight, int orphanChildCount)
        {
            Total = total;
            ChildWeightAdded = childWeightAdded;
            DoubleCountedChildWeight = doubleCountedChildWeight;
            OrphanChildCount = orphanChildCount;
        }

        /// <summary>两条风险都为零 ⇒ 结果可直接用于裁决(供夹具与调用方断言)。</summary>
        public bool IsClean => DoubleCountedChildWeight == 0L && OrphanChildCount == 0;
    }

    /// <summary>容量上限 —— **唯一**容量真值 `CARRY_CAP` 的装载期校验体(AC-20-13 · AC-20-24)。
    /// <para><b>为什么需要一个类型而不是裸 `int`</b>:AC-20-13 要求「未定值 ⇒ 硬失败,
    /// 禁 null 兜底 / 默认容量」,而裸 `int` 的 `default` 是 `0` —— 一个未初始化的字段
    /// 会静默变成「容量 0」而非「未定」。把校验收进构造器,使「未定」**在类型上不可表达**
    /// (`new CarryCapacity(null)` 抛,而不是变成 0)。</para>
    /// <para>⚠️ 本类型**不**持有默认值:全仓零 `const int CARRY_CAP = ...`
    /// (AC-20-24 的「零第二容量常量」;真实值来自 `assets/data` 烘焙产物 → `IDataProvider`)。</para>
    /// </summary>
    public readonly struct CarryCapacity
    {
        /// <summary>容量上限(构造时已校验 `> 0`)。</summary>
        public readonly int Cap;

        /// <summary>构造并校验容量上限。</summary>
        /// <param name="cap">容量上限。须 &gt; 0;`null` = 未注入。</param>
        /// <exception cref="CarryCapUndefinedException"><paramref name="cap"/> 为 null(未注入)
        /// 或非正 —— 即「容量未定值」的两种真实形态(E-13 同款硬失败)。</exception>
        public CarryCapacity(int? cap)
        {
            if (cap == null)
                throw new CarryCapUndefinedException(
                    "[载重] CARRY_CAP 未注入 —— 容量未定值时装载必须硬失败," +
                    "不得以默认容量兜底(AC-20-13 · ADR-014 §五)");
            if (cap.Value <= 0)
                throw new CarryCapUndefinedException(
                    $"[载重] CARRY_CAP = {cap.Value} 非正 —— 容量未定值 / 数据损坏;" +
                    "装载必须硬失败,不得以 0 或负数继续(AC-20-13)");

            Cap = cap.Value;
        }

        /// <summary>
        /// 从**定点**形态拒绝装载 —— `CARRY_CAP` 是 int 计数口径,**不是 `Fix`**
        /// (承 ADR-006 D-21-17 邻域纪律:`weight` / `stack_max` 已移出 `Fix` 解析集,
        /// 容量与它们同族)。
        /// <para>本重载的价值 = 让「把 `Fix` 字面量当 cap」**选错重载**:
        /// 调用方写 <c>new CarryCapacity(someFix)</c> 会落到这里并抛,
        /// 而不是静默经某个转换变成 int(`Fix` 刻意无 <c>implicit operator int</c>,
        /// 故不存在静默通道 —— 本重载是给**误传**一个明确的失败点)。</para>
        /// </summary>
        /// <param name="cap">误传的定点值(恒拒)。</param>
        /// <exception cref="CarryCapUndefinedException">恒抛 —— `Fix` 是错误口径。</exception>
        public CarryCapacity(Fix cap)
        {
            throw new CarryCapUndefinedException(
                "[载重] CARRY_CAP 收到 Fix —— 它是 int 计数口径(与 weight / stack_max 同族," +
                "D-21-17),不是定点量;请检查数据源是否把容量与 weight 混了口径(AC-20-13 边例)");
        }
    }

    /// <summary>载重裁决(F-20.1 / F-20.2 · AC-20-08/09/13/18/24)。
    /// <para><b>唯一实现点</b>:全仓「重多少 / 装不装得下」的算术只在这里。
    /// Story 005 的原子装卸只可经由 <see cref="CanCarry"/> 取真值,禁第二实现。</para>
    /// <para>全部方法为**纯函数**:入参即全部输入,零静态可变状态、零 I/O。</para>
    /// </summary>
    public static class CarryLoadSolver
    {
        /// <summary>按玩家求载重 —— 单层求和(容器 = 空箱自重;子件的计入见 <see cref="SumDetailed"/>)。</summary>
        /// <param name="projection">S20-10 的库存投影(唯一实例真源)。</param>
        /// <param name="weightOfOrdinal">按 `ItemOrdinal` 取单件重量(int &gt; 0)。</param>
        /// <param name="owner">按玩家求和(<see cref="InventoryProjection.NoOwner"/> 表示在地上)。</param>
        /// <returns>该玩家的总载重(≥ 0)。</returns>
        /// <exception cref="ArgumentNullException">任一入参为 null。</exception>
        /// <exception cref="CarryCapUndefinedException">投影自相矛盾(声明持有却取不到行)。</exception>
        /// <exception cref="ItemWeightUndefinedException">某实例的 weight 查不到或非法(缺载体 ⇒ fail-loud)。</exception>
        public static long Sum(InventoryProjection projection,
                               Func<int, int> weightOfOrdinal,
                               int owner)
        {
            return SumDetailed(projection, weightOfOrdinal, owner, null).Total;
        }

        /// <summary>按玩家求载重的可诊断形态(见 <see cref="CarryLoadBreakdown"/>)。</summary>
        /// <param name="projection">库存投影。</param>
        /// <param name="weightOfOrdinal">按 `ItemOrdinal` 取单件重量。</param>
        /// <param name="owner">按玩家求和。</param>
        /// <param name="childrenByContainer">容器 id → 子件 id 列表。
        /// **null = 不并入子件表**(单层求和;此时若调用方另有子件来源而未提供,总量会偏小 ——
        /// 由调用方自己保证口径一致)。</param>
        /// <returns>可诊断结果。</returns>
        /// <exception cref="ArgumentNullException">projection / weightOfOrdinal 为 null。</exception>
        /// <exception cref="CarryCapUndefinedException">投影自相矛盾。</exception>
        /// <exception cref="ItemWeightUndefinedException">某实例或子件的 weight 查不到 / 非法。</exception>
        public static CarryLoadBreakdown SumDetailed(
            InventoryProjection projection,
            Func<int, int> weightOfOrdinal,
            int owner,
            IReadOnlyDictionary<long, IReadOnlyList<long>> childrenByContainer)
        {
            if (projection == null) throw new ArgumentNullException(nameof(projection));
            if (weightOfOrdinal == null) throw new ArgumentNullException(nameof(weightOfOrdinal));

            IReadOnlyList<long> ids = projection.InstancesOf(owner);
            long total = 0L;
            long childAdded = 0L;
            long doubleCounted = 0L;
            int orphan = 0;

            for (int i = 0; i < ids.Count; i++)
            {
                if (!projection.TryGet(ids[i], out InventoryRow row))
                    throw new CarryCapUndefinedException(
                        $"[载重] 投影声明持有实例 {ids[i]} 却取不到行 —— 投影内部不一致");

                // ① 本行自己的重量(= 容器的「空箱自重」;21a §Schema E:容器 qty 恒 1)。
                total = checked(total + InstanceWeightOf(row, weightOfOrdinal));

                // ② 调用方给了显式子件表 ⇒ 把子件逐件并进来(推荐 4 的精确口径)。
                if (childrenByContainer == null) continue;
                if (!childrenByContainer.TryGetValue(ids[i], out IReadOnlyList<long> childIds)) continue;
                if (childIds == null) continue;

                for (int c = 0; c < childIds.Count; c++)
                {
                    long childId = childIds[c];
                    if (!projection.TryGet(childId, out InventoryRow childRow))
                    {
                        // 投影里查不到 ⇒ 「不在场」的子件,重量无从得知。
                        orphan++;
                        continue;
                    }

                    long w = InstanceWeightOf(childRow, weightOfOrdinal);

                    // ⚠️ 子件**已有真实持有者** ⇒ 它已在某人(可能是本玩家,也可能是别人)的
                    //    载重里被计过。再并入就是双计 —— 显式报出,**不静默求和**。
                    if (childRow.Owner != InventoryProjection.NoOwner)
                    {
                        doubleCounted = checked(doubleCounted + w);
                        continue;
                    }

                    // 未归属(在地上)⇒ 谁都没计过,并入是正确的。
                    total = checked(total + w);
                    childAdded = checked(childAdded + w);
                }
            }

            return new CarryLoadBreakdown(total, childAdded, doubleCounted, orphan);
        }

        /// <summary>单条实例的重量(= `weight × qty`,经 S20-11 的共用加法件 —— 不另写一份)。</summary>
        private static long InstanceWeightOf(InventoryRow row, Func<int, int> weightOfOrdinal)
        {
            int unitWeight = weightOfOrdinal(row.ItemOrdinal);
            if (unitWeight < 1)
                throw new ItemWeightUndefinedException(
                    $"[载重] 实例 {row.InstanceId}(ordinal={row.ItemOrdinal})的 weight 查得 " +
                    $"{unitWeight} < 1 —— 物品表缺该条目或数值非法;" +
                    "缺载体不得静默当 0(AC-20-13 邻域)");

            return ContainerWeightFold.InstanceWeight(unitWeight, row.Qty);
        }

        /// <summary>容量判定(F-20.2)——
        /// <c>CanCarry(player, inst) ⟺ CarryLoad(player) + InstanceWeight(inst) ≤ CARRY_CAP</c>。
        /// <para>⚠️ **等号侧合法**:恰好装满**可以装**(`≤` 而非 `<`)。</para>
        /// </summary>
        /// <param name="currentLoad">当前载重(≥ 0)。</param>
        /// <param name="candidateUnitWeight">候选实例的单件重量(int &gt; 0)。</param>
        /// <param name="candidateQty">候选实例的件数(int ≥ 1)。</param>
        /// <param name="capacity">容量上限(已校验)。</param>
        /// <returns>能否装下。</returns>
        /// <exception cref="InvalidOperationException"><paramref name="currentLoad"/> &lt; 0,
        /// 或候选的 weight / qty 域错误;
        /// 或 <c>currentLoad + candidate</c> 超 <c>long</c>(见下)。</exception>
        public static bool CanCarry(long currentLoad, int candidateUnitWeight, int candidateQty,
                                    CarryCapacity capacity)
        {
            if (currentLoad < 0L)
                throw new InvalidOperationException(
                    $"[载重] CarryLoad = {currentLoad} < 0 —— 载重不可能为负;投影或求和出了错");

            long candidate = ContainerWeightFold.InstanceWeight(candidateUnitWeight, candidateQty);

            // ⚠️ **2026-10-27 订正(评审 `review-s20-12-inventory-004` W-3)**:
            //   原实现是裸的 `currentLoad + candidate <= capacity.Cap` —— 比较点**无 `checked`**。
            //   两入参都够大时(`currentLoad` 是**公开的裸 `long`**,`candidate` 可达 ≈ 2^62),
            //   加法在 unchecked 上下文里**静默回绕为负** ⇒ `负 <= Cap` 恒真 ⇒ **静默放行**
            //   (超载的候选被判成装得下)。这与 ADR-012 F7 的有符号溢出纪律直接冲突。
            //   当下生产路径经 `Sum` 产出的 `load` 因四站 `checked` 不可逼近 2^63,故**当时不可达**;
            //   但 `CanCarry` 是公开谓词,任何绕开 `Sum` 的调用方(或损坏的投影)都能触达 ⇒ 必须自己守。
            //   修法 = 与 `ContainerWeightFold.InstanceWeight` **同款**:`checked` + 转具名异常,
            //   不是返回 false(「算不出来」≠「装不下」,静默给 false 同样是谎报)。
            long projected;
            try
            {
                projected = checked(currentLoad + candidate);
            }
            catch (OverflowException)
            {
                throw new InvalidOperationException(
                    $"[载重] {currentLoad} + {candidate} 超 long 承载 —— 载重求和溢出即输入已不可信" +
                    "(真实数据须被 AC-21a-64 上界前置拦下);不得静默判 false 或回绕后放行");
            }

            return projected <= capacity.Cap;
        }

        /// <summary>「新增**任何**物品都一律放不进」的谓词 —— 满档呈现的机制半边(AC-20-18)。
        /// <para><b>公式</b>:<c>IsFullForAnyAddition(load, cap, wMin) ⟺ load &gt; cap − wMin</c></para>
        /// <para>⚠️ <b>禁简化成 <c>load == cap</c></b>(BL-26 明令):若 `wMin &gt; 1`,
        /// 在 `cap − 1` 处也**已经**放不进任何东西,两侧只有以 `wMin` 为界才可比对。
        /// 简化会在「剩余空间不够最小物品」那段**谎报「还能装」**。</para>
        /// <para>⚠️ `wMin &gt; cap` 时右端为负 ⇒ 恒真(确实一件都放不进)—— 这是正确行为,
        /// 不是边例漏判。</para>
        /// </summary>
        /// <param name="currentLoad">当前载重(≥ 0)。</param>
        /// <param name="capacity">容量上限。</param>
        /// <param name="wMin">物品表最小单件重量(`min{ItemDef.weight}`;≥ 1)。</param>
        /// <returns>是否「新增一律放不进」。</returns>
        /// <exception cref="InvalidOperationException"><paramref name="currentLoad"/> &lt; 0,
        /// 或 <paramref name="wMin"/> &lt; 1(域错误)。</exception>
        public static bool IsFullForAnyAddition(long currentLoad, CarryCapacity capacity, int wMin)
        {
            if (currentLoad < 0L)
                throw new InvalidOperationException(
                    $"[载重] CarryLoad = {currentLoad} < 0 —— 载重不可能为负;投影或求和出了错");
            if (wMin < 1)
                throw new InvalidOperationException(
                    $"[载重] wMin = {wMin} < 1 —— weight ∈ int>0 ⇒ min 必 ≥ 1;域错误(AC-21a-15)");

            // 整数减法:cap ≥ 1 且 wMin ≥ 1 ⇒ 差可负,long 比较仍正确。
            return currentLoad > (long)capacity.Cap - wMin;
        }

        /// <summary>构建读数对(<see cref="CarryRatioDto"/>)—— 20 对 42 / 52 的唯一容量出口。</summary>
        /// <param name="currentLoad">当前载重(≥ 0)。</param>
        /// <param name="capacity">容量上限。</param>
        /// <returns>恰两字段的整数读数对(AC-20-18 · AC-20-24)。</returns>
        /// <exception cref="InvalidOperationException"><paramref name="currentLoad"/> &lt; 0。</exception>
        public static CarryRatioDto Ratio(long currentLoad, CarryCapacity capacity)
        {
            if (currentLoad < 0L)
                throw new InvalidOperationException(
                    $"[载重] CarryLoad = {currentLoad} < 0 —— 载重不可能为负;投影或求和出了错");

            return new CarryRatioDto(currentLoad, capacity.Cap);
        }

        /// <summary>`W_MIN` = 物品表**全表**最小单件重量(烘焙期常量,AC-20-18)。
        /// <para>本函数只做 `min` 运算,**不含任何数值**;真实值来自
        /// `assets/data/item_database_items.json` 经 ADR-014 烘焙后的产物。</para>
        /// </summary>
        /// <param name="weights">全表单件重量。</param>
        /// <returns>最小重量(≥ 1)。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="weights"/> 为 null。</exception>
        /// <exception cref="InvalidOperationException">表为空,或含 &lt; 1 的非法重量
        /// (空表 ⇒ `W_MIN` 无定义 ⇒ 满档谓词无输入;不得返回哨兵值冒充)。</exception>
        public static int MinWeight(IReadOnlyList<int> weights)
        {
            if (weights == null) throw new ArgumentNullException(nameof(weights));
            if (weights.Count == 0)
                throw new InvalidOperationException(
                    "[载重] 物品表为空 —— W_MIN 无定义;空表不得返回哨兵值冒充(满档谓词会失去输入)");

            int min = int.MaxValue;
            for (int i = 0; i < weights.Count; i++)
            {
                if (weights[i] < 1)
                    throw new InvalidOperationException(
                        $"[载重] 物品表第 {i} 条 weight = {weights[i]} < 1 —— 域错误(AC-21a-15)");
                if (weights[i] < min) min = weights[i];
            }
            return min;
        }
    }
}
