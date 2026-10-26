// 权威来源:design/gdd/inventory-and-items.md 规则三(:228-260 堆叠键)· 规则十 1.(:552-570 摆放函数)
//          · AC-20-04(StackKey 唯一 = (item_key, quality))· AC-20-12(非网格 / 零排序 / 零自动整理)
//          · OQ-20-3(焦点顺序 = instance_id 升序)· BL-22(「入箱序」废止,统一 id 升序)
//          · 21a F4(item-database.md §Formulas F4)· StackingSolver(堆叠判定的唯一实现)
//          · ADR-005(确定性模拟 —— 纯函数、整数域、可重放)· ADR-006(D-21-17:weight / stack_max 是 int)
//          · ADR-025 §①(纯 sim 逻辑 → `Sim` 装配,引用集 {BCL, Sim.Contracts})
//
// ⚠️ **零本地堆叠规则**:本文件**不定义**「同不同堆叠键」的判据 —— 那归
//    <see cref="StackingSolver.CanStack"/>(21a F4 唯一出处)。本文件只做**分组与切分的编排**,
//    分组谓词与切分算术全部调 21a,不在此处复制公式体(AC-20-04 的「20 不得改写 StackKey」)。
//
// ⚠️ **StackKey 的唯一实现点** = `InventoryFold.InventoryRow.StackKey`(`(ordinal << 32) | quality`)。
//    本文件**不新增**第二个 StackKey 算式:取值一律构造 <see cref="InventoryRow"/> 并读其属性。
//    ⚠️ 现状登记(2026-10-26 实测):卡面称「`StackKey` 符号唯读自 21a、20 侧零本地定义」,
//    但 21a 侧**只有文档注释、没有该类型的实现**(全仓 `grep -rn "StackKey"` 的唯一实现点是
//    `InventoryFold.cs:73`)。故本件的判据订正为「20 程序集内 `StackKey` **定义恰一处**」——
//    可复算:`grep -rn "StackKey =>" unity/Assets/Sim unity/Assets/Sim.Contracts` 恰 1 行。
//
// ⚠️ **摆放序不是世界坐标**:`PlacementSlot.Row/Col` 是**界面二维落位**(药箱面板的格),
//    与 ADR-015 §三 的 `WorldPos` 整数格**无关**。`COLUMNS` / `ROWS_PER_PAGE` 归 42 布局
//    (卡面 Guardrail:本件只交付「给定 COLUMNS 的落位纯函数」+ BLOCKED-BY-R9 标注)。
//
// ⚠️ **零数值**:`stack_max` / `columns` 全部参数化注入,本文件不承载任何具体数值。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>一「栈」—— 同 <c>StackKey</c> 的实例归并后的一个可见单位。
    /// <para><b>派生只读视图</b>:由实例集确定性导出,不持有状态、不写流(ADR-009 §四 派生态)。</para>
    /// </summary>
    public readonly struct InventoryStack
    {
        /// <summary>堆叠键 = <c>(item_key, quality)</c> 的整数形。取值**复用**
        /// <see cref="InventoryRow.StackKey"/> 的同一属性(20 程序集内唯一实现点)。</summary>
        public readonly long StackKey;

        /// <summary>物品复合主键(与 <see cref="StackKey"/> 一一对应;<c>ordinal</c> 是其整数形)。</summary>
        public readonly ItemKey Key;

        /// <summary>品级档(<b>合并前提之一</b> —— 不同品级各占独立栈)。</summary>
        public readonly int Quality;

        /// <summary>本栈数量(∈ [1, <c>stack_max</c>];装满后的溢出去下一栈)。</summary>
        public readonly int TotalQty;

        /// <summary>本栈吸纳的实例 id(**升序**;单实例栈 = 长度 1)。
        /// <para>⚠️ 多实例栈只在「几个实例同键同品级且合计未超 <c>stack_max</c>」时出现 ——
        /// 是**读取视图的归并**,不代表流里发生过合并写入(合并写入归 Story 005 的事务模板)。</para></summary>
        public readonly long[] InstanceIds;

        /// <summary>构造一栈(求解器专用入口)。</summary>
        /// <param name="stackKey">堆叠键(<see cref="InventoryRow.StackKey"/> 同属性值)。</param>
        /// <param name="key">物品复合主键。</param>
        /// <param name="quality">品级档。</param>
        /// <param name="totalQty">本栈数量。</param>
        /// <param name="instanceIds">吸纳的实例 id(升序,非 null)。</param>
        public InventoryStack(long stackKey, ItemKey key, int quality, int totalQty, long[] instanceIds)
        {
            StackKey = stackKey; Key = key; Quality = quality;
            TotalQty = totalQty; InstanceIds = instanceIds;
        }
    }

    /// <summary>摆放落位 —— 序中第 <c>i</c> 件在界面二维格的位置。
    /// <para><c>col = i mod columns</c>,<c>row = i / columns</c>(规则十 1. 的 BL-22 订正口径)。</para>
    /// </summary>
    public readonly struct PlacementSlot
    {
        /// <summary>实例 id。</summary>
        public readonly long InstanceId;

        /// <summary>行(0 起;换页条件 <c>row &gt;= ROWS_PER_PAGE</c> —— 阈值归 42 布局)。</summary>
        public readonly int Row;

        /// <summary>列(0 起,<c>&lt; columns</c>)。</summary>
        public readonly int Col;

        /// <summary>构造一个落位(求解器专用入口)。</summary>
        /// <param name="instanceId">实例 id。</param>
        /// <param name="row">行。</param>
        /// <param name="col">列。</param>
        public PlacementSlot(long instanceId, int row, int col)
        {
            InstanceId = instanceId; Row = row; Col = col;
        }
    }

    /// <summary>20 的堆叠与摆放派生视图(AC-20-04 · AC-20-12)。
    /// <para><b>纯函数</b>:入参即全部输入;不读墙钟、不读表现态位置、不写事件流 ——
    /// 同参数集逐位产出同结果(可重放,ADR-005)。</para>
    /// <para><b>零本地堆叠规则</b>:分组谓词 = <see cref="StackingSolver.CanStack"/>,
    /// 切分算术 = <see cref="StackingSolver.SplitStack"/>,两者均**直接调用**,不在此复制公式体。</para>
    /// </summary>
    public static class StackingRules
    {
        /// <summary>把实例集归并成栈(AC-20-04)。
        /// <para><b>归并规则</b>(全部来自 21a F4,本方法只编排):</para>
        /// <list type="number">
        /// <item>容器(<c>Children.Length &gt; 0</c>)与 <c>stack_max == 1</c> 的条目**永不合并** ——
        ///   各实例自成独立栈(谓词见 <see cref="StackingSolver.CanStack"/>)。</item>
        /// <item>可合并的同键同品级实例,合计超 <c>stack_max</c> 时**装满一栈再开下一栈** ——
        ///   切分算术走 <see cref="StackingSolver.SplitStack"/>(AC-21a-33 的溢出口径)。</item>
        /// <item>输出栈序 = **首个实例 id 升序**;同栈内实例 id 升序。</item>
        /// </list>
        /// <para>⚠️ <b>为什么入参是 <see cref="ItemInstance"/> 而不是投影行</b>:投影行
        /// (<see cref="InventoryRow"/>)只携带 <c>ItemOrdinal</c>,既不携带 <see cref="ItemKey"/>
        /// 也不携带 <c>Children</c> ⇒ 用它喂 <see cref="StackingSolver.CanStack"/> 会使容器守卫
        /// **恒真**(<c>Children.Length == 0</c> 永远成立)= 把 21a 的判据调成空转。
        /// 故本方法的定义域是实例本体(含 <c>Children</c>),投影侧接驳归调用方。</para>
        /// </summary>
        /// <param name="instances">待归并的实例集(可为空;传入顺序**任意** —— 排序在方法内,
        /// 不是调用方义务)。</param>
        /// <param name="defOf">物品键 → 物品条目(非 null;取 <c>StackMax</c> 与 <c>Ordinal</c>)。
        /// 查不到 = 静态数据缺陷,须抛(见 <see cref="InstanceResolver"/> 的只增不删口径)。</param>
        /// <returns>栈清单(实例集为空 ⇒ 空清单,非 null)。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="instances"/> 或
        /// <paramref name="defOf"/> 为 null(拒以空集冒充绿)。</exception>
        /// <exception cref="InvalidOperationException">查表返回 <c>StackMax &lt; 1</c>
        /// (域错误 —— 静态数据缺陷,不静默当作不可堆叠)。</exception>
        /// <example>同键同品级两个 <c>qty = 1</c> 实例、<c>stackMax = 9</c> ⇒ **一栈**
        /// <c>TotalQty = 2</c>、<c>InstanceIds</c> 长度 2;容器实例 ⇒ 各自独栈。</example>
        public static IReadOnlyList<InventoryStack> StacksOf(
            IReadOnlyList<ItemInstance> instances, Func<ItemKey, ItemDef> defOf)
        {
            if (instances == null) throw new ArgumentNullException(nameof(instances));
            if (defOf == null) throw new ArgumentNullException(nameof(defOf));

            // 确定性起点:按 instance_id 升序(规则十 BL-22 —— 「入箱序」废止,统一 id 序)。
            var ordered = new List<ItemInstance>(instances);
            ordered.Sort(CompareByInstanceId);

            var stacks = new List<InventoryStack>();
            // 每个 StackKey 当前**未满**栈在结果清单里的下标(仅可堆叠键登记)。
            var openByKey = new Dictionary<long, int>();

            for (int i = 0; i < ordered.Count; i++)
            {
                ItemInstance inst = ordered[i];
                ItemDef def = defOf(inst.Key);
                if (def.StackMax < 1)
                    throw new InvalidOperationException(
                        $"[堆叠] 物品 {inst.Key} 的 stack_max = {def.StackMax} < 1 —— " +
                        "静态数据缺陷(越界校验 = AC-21a-15);不静默当作不可堆叠");

                long stackKey = StackKeyOf(def.Ordinal, inst.Quality);

                // 「同不同堆叠键」的判据**全部来自 21a**(AC-20-04:20 侧不得复制公式体)。
                // `CanStack(inst, inst)` = 「该条目自身可堆叠」⇔ 非容器 ∧ `stackMax > 1`
                // ∧ 品级自反 ∧ 主键自反 —— 后两条恒真,故它恰好只留下前两条,正是合并前提。
                // ⚠️ **不得回退成手写 `inst.Children.Length == 0 && def.StackMax > 1`**:
                //   那是把 21a 的条件②①抄了一份,21a 改口径时这里会静默分叉。
                bool mergeable = StackingSolver.CanStack(inst, inst, def.StackMax);

                int remaining = inst.Qty;   // 尚未落位的数量

                if (mergeable && openByKey.TryGetValue(stackKey, out int openIdx))
                {
                    InventoryStack open = stacks[openIdx];
                    StackSplitResult split = StackingSolver.SplitStack(
                        open.TotalQty, remaining, def.StackMax);

                    if (split.OverflowQty == 0)
                    {
                        stacks[openIdx] = Append(open, inst, split.CurrentFinalQty);
                        continue;
                    }

                    // 溢出:原栈补满到 stackMax(21a 给出的 CurrentFinalQty),溢出量继续落新栈。
                    stacks[openIdx] = new InventoryStack(
                        open.StackKey, open.Key, open.Quality,
                        split.CurrentFinalQty, open.InstanceIds);
                    remaining = split.OverflowQty;
                    openByKey.Remove(stackKey);          // 该键已无未满栈
                }

                // 落位:inst 可能被拆到多个栈(remaining 可 > stackMax)。
                // 首栈承接 inst 自己的 id;再往后的栈**不含新实例 id**(它们只承载数量),
                // ⇒ 用 inst.InstanceId 的重复登记是被禁止的,故后续栈的 InstanceIds 为空数组。
                bool first = true;
                while (remaining > 0)
                {
                    int take = remaining > def.StackMax ? def.StackMax : remaining;
                    long[] ids = first ? new[] { inst.InstanceId } : Array.Empty<long>();
                    stacks.Add(new InventoryStack(stackKey, inst.Key, inst.Quality, take, ids));
                    if (first) openByKey[stackKey] = stacks.Count - 1;
                    remaining -= take;
                    first = false;
                }

                // 末栈若已满,不能当作「未满栈」吸纳后续实例。
                if (mergeable && openByKey.TryGetValue(stackKey, out int lastIdx) &&
                    stacks[lastIdx].TotalQty >= def.StackMax)
                    openByKey.Remove(stackKey);
            }

            return stacks;
        }

        /// <summary>摆放序与二维落位(AC-20-12 · 规则十 1.)。
        /// <para><b>序 = <paramref name="instanceIds"/> 的升序</b>(<c>instance_id</c> 升序,
        /// 与入箱历史 / 参与顺序**无关** —— BL-22 订正口径);落位 <c>col = i mod columns</c>、
        /// <c>row = i / columns</c>。</para>
        /// <para><b>AC-20-12 的承载**:签名参数类型集恰 = <c>{IReadOnlyList&lt;long&gt;, int}</c> ——
        /// 没有 <c>weight</c> / 品级 / 时间戳入参(编译期可证:按属性重排需要额外输入,
        /// 而这里拿不到);排序键只有 <c>instance_id</c> ⇒ 「零排序 / 零自动整理」不是纪律,是**类型面事实**。</para>
        /// </summary>
        /// <param name="instanceIds">箱内实例 id 集(可为空;传入顺序任意)。</param>
        /// <param name="columns">每行列数(≥ 1;具体值归 42 布局 —— 卡面 Guardrail 的 BLOCKED-BY-R9)。</param>
        /// <returns>落位清单(与 <paramref name="instanceIds"/> 等长,id 升序)。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="instanceIds"/> 为 null。</exception>
        /// <exception cref="InvalidOperationException"><paramref name="columns"/> &lt; 1
        /// (静态布局配置缺陷 —— 不静默退化成单列)。</exception>
        /// <example><c>PlacementOrder([7, 3], 2)</c> ⇒ <c>[(3, row 0, col 0), (7, row 0, col 1)]</c>;
        /// <c>PlacementOrder([], 2)</c> ⇒ 空清单(空箱)。</example>
        public static IReadOnlyList<PlacementSlot> PlacementOrder(
            IReadOnlyList<long> instanceIds, int columns)
        {
            if (instanceIds == null) throw new ArgumentNullException(nameof(instanceIds));
            if (columns < 1)
                throw new InvalidOperationException(
                    $"[摆放] columns = {columns} < 1 —— 布局配置缺陷(值归 42);不静默退化成单列");

            var ids = new List<long>(instanceIds);
            ids.Sort();

            var slots = new List<PlacementSlot>(ids.Count);
            for (int i = 0; i < ids.Count; i++)
                slots.Add(new PlacementSlot(ids[i], i / columns, i % columns));
            return slots;
        }

        /// <summary>堆叠键 —— **读 <see cref="InventoryRow.StackKey"/> 的同一属性**,
        /// 不在此重写算式(AC-20-04 的「唯一出处」要求:算式只有 <c>InventoryFold.cs:73</c> 一处)。</summary>
        private static long StackKeyOf(int itemOrdinal, int quality)
            => new InventoryRow(0L, itemOrdinal, quality, 0, InventoryProjection.NoOwner).StackKey;

        /// <summary>实例 id 升序比较(全序;id 唯一,无 tiebreak 需求)。</summary>
        private static int CompareByInstanceId(ItemInstance a, ItemInstance b)
            => a.InstanceId.CompareTo(b.InstanceId);

        /// <summary>把实例并入栈(id 追加保升序 —— 传入序已升序,故尾插即升序)。
        /// <para>数量由 21a 的切分结果给出(<paramref name="finalQty"/>),本方法不自行相加。</para>
        /// </summary>
        private static InventoryStack Append(InventoryStack stack, in ItemInstance inst, int finalQty)
        {
            var ids = new long[stack.InstanceIds.Length + 1];
            Array.Copy(stack.InstanceIds, ids, stack.InstanceIds.Length);
            ids[stack.InstanceIds.Length] = inst.InstanceId;
            return new InventoryStack(stack.StackKey, stack.Key, stack.Quality, finalQty, ids);
        }
    }
}
