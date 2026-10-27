// S20-11 · inventory story-003 —— 堆叠键 / 摆放序 / 折重 / P0 范围门 / DTO 零浮点
//
// 权威来源:
//   design/gdd/inventory-and-items.md 规则三(堆叠键 + 折重口径)· 规则十 1.(摆放函数)
//     · 规则四 F-20.1(载重)· AC-20-04 / AC-20-12 / AC-20-15 · BL-7③ / BL-28 · OQ-20-4
//   item-database.md §Formulas F4 · §Schema E(21a 唯一出处)
//   ADR-006 D-21-17(weight / stack_max = int)· ADR-005(确定性)· ADR-025 §①(SIm 引用集)
//   story production/epics/inventory-items/story-003-stacking-container-weight-fold.md
//
// ⚠️ **AC 记分(禁把「本体跑通」读成「五条全绿」)**:
//     ✅ AC-20-04  · 堆叠键唯一 = (item_key, quality)(分组谓词调 21a F4,20 侧零重定义)
//     ✅ AC-20-12  · 非网格 / 零排序(签名类型面 + 行为面双判)
//     ✅ AC-20-15  · 输出 DTO 双层零浮点(递归反射 + Cecil IL)
//     ✅ 折重不变量 · OQ-20-4 结清口径(空箱自重 + 子件逐件;可逆;无双重计数)
//     ⚠️ P0 范围门 · **部分** —— ③ `build_part` quality 恒 1 **已绿**;
//        ① 容器 qty 恒 1 · ② 容器 quality 恒 1 **结构性不可勾**:P0 无「这是容器吗」的判据载体
//        (21a `ItemCategory` 无 container 值 / `ItemInstance` 无 `is_container` /
//        数据零容器条目 / `StackingSolver.cs:72-76` 已自陈该洞并路由 Story 010)
//        ⇒ 挂 **BLOCKED-BY Story 010**,禁以 `Children.Length > 0` 顶替
//        (那会把空容器判成非容器 = 同一个洞)。
//
// ⚠️ **两个必须读清的近邻风险**:
//   ① 「`StacksOf` 归并出多实例栈」≠「流里发生过合并写入」—— 前者是**读取视图的派生**,
//      合并写入(消耗 / 转移原子性)归 Story 005。
//   ② 「`ContainerClosure` 拒嵌套」是 21a Story 010 的既有执行体,本件**只补夹具**,
//      不重复实现(它的存在不使 P0 范围门 ①② 可勾 —— 闭包校验需要先知道谁是容器)。
//
// ⚠️ 零随机 / 零时间依赖 / 零写盘到仓内(临时文件一律落 Path.GetTempPath());全部 id / 数值写死。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DaYiJingCheng.EditorTools.Gates;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using Mono.Cecil;
using NUnit.Framework;
using UnityEditor;

namespace DaYiJingCheng.Tests.EditMode.Inventory
{
    /// <summary>inventory story-003 的 EditMode 夹具(AC-20-04 / 12 / 15 / P0 范围门 / 折重)。</summary>
    [TestFixture]
    public class StackingContainerRulesTest
    {
        // ───────────────────────── 共享夹具 ─────────────────────────

        private const string WillowRaw = "willow_bark";
        private const string ReedRaw = "reed_stem";
        private const string PlankPart = "plank";
        private const string BoxPart = "medbox";

        /// <summary>物品表夹具:三件可堆叠 + 一件 <c>build_part</c>。
        /// <para>⚠️ 数值全是**夹具值**,不是 GDD 数值(数值归用户;此处只为可判)。</para></summary>
        private static ItemDef[] MakeDefs()
        {
            return new[]
            {
                Def(0, WillowRaw, ProcessingState.Raw, ItemCategory.Material, stackMax: 9, weight: 2),
                Def(1, ReedRaw, ProcessingState.Raw, ItemCategory.Material, stackMax: 4, weight: 3),
                Def(2, PlankPart, ProcessingState.Raw, ItemCategory.BuildPart, stackMax: 1, weight: 5),
                Def(3, BoxPart, ProcessingState.Raw, ItemCategory.BuildPart, stackMax: 1, weight: 7),
            };
        }

        private static ItemDef Def(int ordinal, string baseId, ProcessingState state,
                                   ItemCategory cat, int stackMax, int weight)
            => new ItemDef
            {
                Ordinal = ordinal,
                BaseId = baseId,
                ProcessingState = state,
                DisplayName = baseId,
                Category = cat,
                StackMax = stackMax,
                Weight = weight,
                Deprecated = false,
                LegalTransitions = Array.Empty<string>(),
            };

        /// <summary>查表函数(夹具专用;查不到 ⇒ 抛,照 <see cref="InstanceResolver"/> 的只增不删口径)。</summary>
        private static Func<ItemKey, ItemDef> Lookup(ItemDef[] defs)
            => key =>
            {
                for (int i = 0; i < defs.Length; i++)
                    if (defs[i].ProcessingState == key.State &&
                        string.Equals(defs[i].BaseId, key.BaseId, StringComparison.Ordinal))
                        return defs[i];
                throw new InvalidOperationException($"[夹具] 物品表缺条目 {key}");
            };

        private static ItemInstance Inst(long id, string baseId, int quality, int qty,
                                         long[] children = null)
            => new ItemInstance(id, new ItemKey(baseId, ProcessingState.Raw), quality, qty,
                                children ?? Array.Empty<long>());

        // ═══════════════════════ AC-20-04 · 堆叠键唯一 ═══════════════════════

        /// <summary>AC-20-04:同 key 同 q 合一、异 q 分栈、不可堆叠条目各自独栈。</summary>
        [Test]
        public void test_ac2004_sameKeySameQuality_merges_othersStaySeparate()
        {
            ItemDef[] defs = MakeDefs();
            var instances = new List<ItemInstance>
            {
                Inst(100, WillowRaw, 1, 1),   // A:同 key 同 q
                Inst(101, WillowRaw, 1, 1),   // A:与 100 合并
                Inst(102, WillowRaw, 3, 1),   // 异 q ⇒ 独立栈
                Inst(103, ReedRaw,   1, 1),   // 异 key ⇒ 独立栈
                Inst(104, PlankPart, 1, 1),   // stack_max = 1 ⇒ 永不合并
                Inst(105, PlankPart, 1, 1),   // 同上,各自独栈
            };

            IReadOnlyList<InventoryStack> stacks = StackingRules.StacksOf(instances, Lookup(defs));

            Assert.AreEqual(5, stacks.Count,
                "期望 5 栈: willow(q1) 合一 → 1 · willow(q3) → 1 · reed(q1) → 1 · plank ×2 → 2");

            InventoryStack willowQ1 = stacks.Single(s => s.Key.BaseId == WillowRaw && s.Quality == 1);
            Assert.AreEqual(2, willowQ1.TotalQty, "同 key 同品级两件 ⇒ 合并后 qty = 2");
            CollectionAssert.AreEqual(new long[] { 100, 101 }, willowQ1.InstanceIds,
                "合并栈的实例 id 须升序,且恰为 {100, 101}");

            // 不同品级不合并 —— 21a 规则三(20 不得改写)
            Assert.AreEqual(1, stacks.Count(s => s.Key.BaseId == WillowRaw && s.Quality == 3),
                "异品级须各自成栈(StackKey = (item_key, quality))");
        }

        /// <summary>AC-20-04:`StackKey` 在 20 两棵树的**定义点恰一处**(`InventoryFold.cs` 的
        /// <c>InventoryRow.StackKey</c>)。本件不得新增第二份算式。
        /// <para>⚠️ 判据为**源文本计数**:`StackKey =&gt;` 形式恰 1 处;`StackingRules` 只用不定义。</para>
        /// </summary>
        [Test]
        public void test_ac2004_stackKeyExpression_definedExactlyOnce()
        {
            string root = DaYiJingCheng.EditorTools.Gates.AssemblyGates.ScriptAssemblyPath("Sim");
            string projectRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(root),
                                                               "..", ".."));

            var hits = new List<string>();
            foreach (string rel in new[] { "Assets/Sim/ItemDatabase", "Assets/Sim.Contracts/ItemDatabase" })
            {
                string dir = Path.Combine(projectRoot, rel);
                if (!Directory.Exists(dir)) continue;
                foreach (string f in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
                {
                    string[] lines = File.ReadAllLines(f);
                    for (int i = 0; i < lines.Length; i++)
                        // 只认「声明式」的表达式体定义(排除注释行)
                        if (lines[i].Contains("StackKey =>") && !lines[i].TrimStart().StartsWith("//"))
                            hits.Add($"{Path.GetFileName(f)}:{i + 1}");
                }
            }

            Assert.AreEqual(1, hits.Count,
                "20 侧 StackKey 的**算式定义**须恰一处(AC-20-04 的「唯一出处」要求;" +
                "21a 侧只有文档注释、无该类型实现,故唯一实现点在 InventoryFold.cs)。实际命中:\n  "
                + string.Join("\n  ", hits));
        }

        /// <summary>AC-20-04:可堆叠条目合计超 <c>stack_max</c> ⇒ 装满一栈再开下一栈
        /// (切分算术走 21a <see cref="StackingSolver.SplitStack"/>,本件不自写)。</summary>
        [Test]
        public void test_ac2004_overflow_spillsToNewStack_quantityConserved()
        {
            ItemDef[] defs = MakeDefs();      // willow stackMax = 9
            var instances = new List<ItemInstance>
            {
                Inst(200, WillowRaw, 1, 5),
                Inst(201, WillowRaw, 1, 4),   // 合计 9 = stackMax ⇒ 恰一栈
                Inst(202, WillowRaw, 1, 1),   // 再 +1 ⇒ 溢出到第二栈
            };

            IReadOnlyList<InventoryStack> stacks = StackingRules.StacksOf(instances, Lookup(defs));

            Assert.AreEqual(2, stacks.Count, "合计 10 > stackMax 9 ⇒ 期望 2 栈");
            Assert.AreEqual(9, stacks[0].TotalQty, "首栈须补满到 stackMax");
            Assert.AreEqual(1, stacks[1].TotalQty, "溢出量落第二栈");

            // 守恒:Σ TotalQty = Σ 输入 qty(切分不凭空增删 —— AC-21a-33 的口径)
            int sumIn = instances.Sum(i => i.Qty);
            int sumOut = stacks.Sum(s => s.TotalQty);
            Assert.AreEqual(sumIn, sumOut, "切分后总量须守恒");

            // 实例 id 不重复登记 ⇒ ⚠️ **2026-10-27 订正(评审 `review-s20-11-inventory-003` B-2)**:
            //   原断言是 `CollectionAssert.AllItemsAreUnique(allIds, "同一实例 id 不得在多个栈重复登记")`
            //   —— 它把「id 全局唯一」写成了契约,而 `InstanceIds` 的真实语义是「**本栈吸纳了**谁」。
            //   评审同时指出该断言对缺陷**零判别力**:漏记只会让集合更唯一,永远绿。
            //   订正为**覆盖率判据**:每个输入实例都至少在一个栈的 id 集里出现(漏记 ⇒ 红)。
            var allIds = stacks.SelectMany(s => s.InstanceIds).ToArray();
            foreach (ItemInstance inst in instances)
                CollectionAssert.Contains(allIds, inst.InstanceId,
                    $"实例 {inst.InstanceId} 的件数进了某些栈,其 id 却不在任何栈的 id 集里 —— " +
                    "下游按 id 集渲染会漏画它(B-2 的原缺陷)");
        }

        /// <summary>**部分吸纳**:某实例的一截进了已有未满栈、剩余溢出到后续栈 ⇒
        /// 该实例 id 须进**两栈**的 id 集(2026-10-27 补 · 评审 `review-s20-11-inventory-003` B-2 路径 1)。
        /// <para>⚠️ 本条的存在缘由:原实现在溢出分支直接传 `open.InstanceIds`,把「已有一截进了
        /// 这一栈」的实例**漏记**掉;而既有夹具的序列 `5/4/1` 恰好在 `4` 处填满,从未发生部分吸纳
        /// ⇒ 对该缺陷零判别力。</para></summary>
        [Test]
        public void test_ac2004_partialAbsorption_recordsInstanceInBothStacks()
        {
            ItemDef[] defs = MakeDefs();      // willow stackMax = 9
            var instances = new List<ItemInstance>
            {
                Inst(300, WillowRaw, 1, 1),
                Inst(301, WillowRaw, 1, 1),   // 栈内 A+B = 2,未满
                Inst(302, WillowRaw, 1, 10),  // C:先填 7 进首栈(补满到 9),余 3 落新栈
            };

            IReadOnlyList<InventoryStack> stacks = StackingRules.StacksOf(instances, Lookup(defs));

            Assert.AreEqual(2, stacks.Count, "2 + 10 = 12 > stackMax 9 ⇒ 恰 2 栈");
            Assert.AreEqual(9, stacks[0].TotalQty, "首栈补满");
            Assert.AreEqual(3, stacks[1].TotalQty, "余量落第二栈");

            CollectionAssert.Contains(stacks[0].InstanceIds, 302L,
                "C 有 7 件进了首栈 ⇒ 首栈 id 集**必须含 C**(漏记 = 下游按 id 集渲染会漏画它)");
            CollectionAssert.Contains(stacks[1].InstanceIds, 302L,
                "C 的余 3 件进了第二栈 ⇒ 第二栈 id 集也含 C(跨栈实例是合法形态)");

            int sumIn = instances.Sum(i => i.Qty);
            int sumOut = stacks.Sum(s => s.TotalQty);
            Assert.AreEqual(sumIn, sumOut, "切分后总量须守恒");
        }

        /// <summary>**尾部部分栈须被登记为未满栈**:多栈切分后的末栈若未满,
        /// 下一个同键实例应去补满它,而不是另开新栈
        /// (2026-10-27 补 · 评审 `review-s20-11-inventory-003` B-2 路径 2)。</summary>
        [Test]
        public void test_ac2004_tailPartialStack_absorbsNextInstance()
        {
            ItemDef[] defs = MakeDefs();      // willow stackMax = 9
            var instances = new List<ItemInstance>
            {
                Inst(400, WillowRaw, 1, 12),  // 单实例跨栈:首栈 9 + 尾栈 3
                Inst(401, WillowRaw, 1, 5),   // 尾栈还有 6 空位 ⇒ 应被补满到 9
            };

            IReadOnlyList<InventoryStack> stacks = StackingRules.StacksOf(instances, Lookup(defs));

            Assert.AreEqual(2, stacks.Count,
                "尾栈(3)吸收 5 后 = 8 ≤ 9 ⇒ 不应另开第三栈;" +
                "原实现不登记尾部部分栈 ⇒ 会错误地开出第三栈");
            Assert.AreEqual(9, stacks[0].TotalQty, "首栈满");
            Assert.AreEqual(8, stacks[1].TotalQty, "3 + 5 = 8(尾栈被补,但未补满)");
            CollectionAssert.Contains(stacks[1].InstanceIds, 401L, "尾栈 id 集须含新实例");
        }

        /// <summary>AC-20-04:容器实例(<c>Children.Length &gt; 0</c>)**永不参与合并** ——
        /// 21a F4 的容器守卫。
        /// <para>⚠️ 本条只证「有子件的容器不合并」;「空容器」的不可判定性是 21a 已登记的 schema 洞
        /// (见 <see cref="StackingSolver"/> 文档),归 Story 010。</para>
        /// </summary>
        [Test]
        public void test_ac2004_containerWithChildren_neverMerges()
        {
            ItemDef[] defs = MakeDefs();
            var instances = new List<ItemInstance>
            {
                Inst(300, WillowRaw, 1, 1, children: new long[] { 900 }),
                Inst(301, WillowRaw, 1, 1, children: new long[] { 901 }),
            };

            IReadOnlyList<InventoryStack> stacks = StackingRules.StacksOf(instances, Lookup(defs));

            Assert.AreEqual(2, stacks.Count,
                "两个容器实例(同 key 同 q)也须各自独栈 —— 容器自身不堆叠(GDD §Schema E)");
        }

        /// <summary>AC-20-04:非 21a 来源的堆叠规则一律不得出现 —— 本件通过**直接调用**
        /// <see cref="StackingSolver.CanStack"/> 兑现,不以本地谓词替代。</summary>
        [Test]
        public void test_ac2004_stackMaxOfOne_neverMerges()
        {
            ItemDef[] defs = MakeDefs();      // plank stackMax = 1
            IReadOnlyList<InventoryStack> stacks = StackingRules.StacksOf(
                new List<ItemInstance> { Inst(400, PlankPart, 1, 1), Inst(401, PlankPart, 1, 1) },
                Lookup(defs));

            Assert.AreEqual(2, stacks.Count, "stack_max = 1 ⇒ 永不合并(21a F4 条件①)");
        }

        // ═══════════════════ AC-20-12 · 非网格 / 零排序 ═══════════════════

        /// <summary>AC-20-12:摆放序 = <c>instance_id</c> 升序,**与传入顺序无关**
        /// (「入箱序」已废止 —— BL-22)。</summary>
        [Test]
        public void test_ac2012_shuffledInput_yieldsIdenticalOrder()
        {
            long[] natural = { 5, 11, 7, 23, 3 };
            long[] shuffled = { 23, 3, 11, 5, 7 };

            IReadOnlyList<PlacementSlot> a = StackingRules.PlacementOrder(natural, columns: 3);
            IReadOnlyList<PlacementSlot> b = StackingRules.PlacementOrder(shuffled, columns: 3);

            CollectionAssert.AreEqual(
                a.Select(s => s.InstanceId).ToArray(),
                b.Select(s => s.InstanceId).ToArray(),
                "同一集合乱序喂入 ⇒ 同序(排序在函数内,不是调用方义务)");
            CollectionAssert.AreEqual(new long[] { 3, 5, 7, 11, 23 },
                a.Select(s => s.InstanceId).ToArray(), "序 = instance_id 升序");
        }

        /// <summary>AC-20-12:落位 <c>col = i mod columns</c>、<c>row = i / columns</c>
        /// (规则十 1. 的 BL-22 订正口径)。</summary>
        [Test]
        public void test_ac2012_layout_isRowMajorByColumns()
        {
            IReadOnlyList<PlacementSlot> slots =
                StackingRules.PlacementOrder(new long[] { 10, 20, 30, 40, 50 }, columns: 2);

            Assert.AreEqual(0, slots[0].Row); Assert.AreEqual(0, slots[0].Col);
            Assert.AreEqual(0, slots[1].Row); Assert.AreEqual(1, slots[1].Col);
            Assert.AreEqual(1, slots[2].Row); Assert.AreEqual(0, slots[2].Col);
            Assert.AreEqual(1, slots[3].Row); Assert.AreEqual(1, slots[3].Col);
            Assert.AreEqual(2, slots[4].Row); Assert.AreEqual(0, slots[4].Col);
            Assert.AreEqual(10, slots[0].InstanceId, "首件 = 最小 id(升序后下标 0)");
        }

        /// <summary>AC-20-12 的**类型面判据**:<c>PlacementOrder</c> 的入参类型集恰 =
        /// <c>{IReadOnlyList&lt;long&gt;, int}</c> —— 拿不到 weight / 品级 / 时间戳
        /// ⇒ 「零排序 / 零自动整理」是**编译期事实**,不是纪律。
        /// </summary>
        [Test]
        public void test_ac2012_signature_cannotSeeAttributesOrTime()
        {
            System.Reflection.MethodInfo m = typeof(StackingRules).GetMethod(
                nameof(StackingRules.PlacementOrder),
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

            Assert.IsNotNull(m, "PlacementOrder 须为公开静态方法");
            System.Reflection.ParameterInfo[] ps = m.GetParameters();
            Assert.AreEqual(2, ps.Length, "恰好两个入参(id 集 + columns)");

            Assert.AreEqual(typeof(IReadOnlyList<long>), ps[0].ParameterType,
                "① 入参须恰为 IReadOnlyList<long> —— 若是实例对象则能读到 weight / 品级 ⇒ 可重排");
            Assert.AreEqual(typeof(int), ps[1].ParameterType,
                "② 第二个入参须恰为 int(布局列数)");
        }

        /// <summary>AC-20-12:布局列数非法 ⇒ 显式抛,**不静默退化成单列**。</summary>
        [Test]
        public void test_ac2012_nonPositiveColumns_throwsInsteadOfDegrading()
        {
            Assert.Throws<InvalidOperationException>(
                () => StackingRules.PlacementOrder(new long[] { 1 }, columns: 0),
                "columns = 0 = 布局配置缺陷,须显式失败而非静默退化");
        }

        /// <summary>AC-20-12:空箱 ⇒ 空序(非 null)。</summary>
        [Test]
        public void test_ac2012_emptyBox_yieldsEmptyOrderNotNull()
        {
            IReadOnlyList<PlacementSlot> slots =
                StackingRules.PlacementOrder(Array.Empty<long>(), columns: 4);

            Assert.IsNotNull(slots, "空箱返回空清单,不是 null");
            Assert.AreEqual(0, slots.Count);
        }

        // ═══════════════════ 折重不变量 · OQ-20-4 ═══════════════════

        /// <summary>折重 = <b>空箱自重 + 子件逐件</b>(OQ-20-4 结清口径)。</summary>
        [Test]
        public void test_foldWeight_isEmptyContainerPlusChildrenSummed()
        {
            var children = new List<ItemStackAmount>
            {
                new ItemStackAmount(unitWeight: 2, qty: 3),   // 6
                new ItemStackAmount(unitWeight: 5, qty: 1),   // 5
            };

            long folded = ContainerWeightFold.FoldWeight(emptyContainerWeight: 7, children);

            Assert.AreEqual(18L, folded, "7 + (2×3) + (5×1) = 18,整数逐位");
        }

        /// <summary>折重:空箱(零子件)⇒ 恰 = 自重。</summary>
        [Test]
        public void test_foldWeight_emptyChildren_equalsSelfWeight()
        {
            Assert.AreEqual(7L,
                ContainerWeightFold.FoldWeight(7, Array.Empty<ItemStackAmount>()),
                "空箱 = 自重(Σ = 0)");
        }

        /// <summary>折重**可逆性**:拆出全部子件再折回,结果逐位相等
        /// (整数域加法结合律 —— ADR-006 要求整数域求值的原因之一)。
        /// <para>⚠️ <b>必须有字面量锚</b>(2026-10-26 证伪轮 M5/M6 实测):本夹具原版只写
        /// <c>Assert.AreEqual(before, after)</c>,而 <c>before</c> / <c>after</c> **都来自
        /// <see cref="ContainerWeightFold.FoldWeight"/>** —— 变异让两次调用同向变化时两者仍相等
        /// ⇒ 两轮变异(子件被吞 / 自重按件放大)**双双全绿逃逸**。这就是
        /// 「夹具自指」(断言引用被測对象自身,变异两边一起动)。
        /// 修法 = 加<b>独立算出</b>的字面量 <c>40</c>,把「可逆」与「值对」两件事分开钉。</para>
        /// </summary>
        [Test]
        public void test_foldWeight_removeThenRefold_isBitIdentical()
        {
            var full = new List<ItemStackAmount>
            {
                new ItemStackAmount(2, 3), new ItemStackAmount(5, 1), new ItemStackAmount(11, 2),
            };
            long before = ContainerWeightFold.FoldWeight(7, full);

            // 字面量锚:7 + (2×3) + (5×1) + (11×2) = 7 + 6 + 5 + 22 = 40
            Assert.AreEqual(40L, before,
                "折前的**绝对值**须由字面量钉住 —— 只跟自身比会让变异两边同动而逃逸");

            // 拆出(逐个移走)后重新折回 —— 顺序也打乱,验「与顺序无关」
            var refolded = new List<ItemStackAmount> { full[2], full[0], full[1] };
            long after = ContainerWeightFold.FoldWeight(7, refolded);

            Assert.AreEqual(40L, after,
                "折后的**绝对值**同样由字面量钉住(与 before 独立成立,不靠互相比)");
            Assert.AreEqual(before, after, "拆出再折回须逐位等值;顺序不影响结果(整数加法交换)");
        }

        /// <summary>折重**无双重计数**:容器自重不随子件数放大。
        /// <para>判据 = 加一件子件只增加该子件的重量,容器自重那一份不变。</para>
        /// </summary>
        [Test]
        public void test_foldWeight_addingChild_addsOnlyChildWeight()
        {
            long one = ContainerWeightFold.FoldWeight(7, new[] { new ItemStackAmount(2, 1) });
            long two = ContainerWeightFold.FoldWeight(
                7, new[] { new ItemStackAmount(2, 1), new ItemStackAmount(3, 4) });

            Assert.AreEqual(9L, one, "7 + 2 = 9");
            Assert.AreEqual(21L, two, "7 + 2 + 12 = 21 —— 自重仍是 7,未被 ×2");
            Assert.AreEqual(12L, two - one, "增量恰 = 新子件重量(3×4),不含自重");
        }

        /// <summary>折重:容器实例自身重量 = <c>weight × 1</c>(§Schema E 的 qty 恒 1 口径)。</summary>
        [Test]
        public void test_instanceWeight_containerQtyOne_isSelfWeight()
        {
            Assert.AreEqual(7L, ContainerWeightFold.InstanceWeight(unitWeight: 7, qty: 1),
                "容器 qty 恒 1 ⇒ InstanceWeight = 空箱自重");
        }

        /// <summary>折重域错:null 子件集须抛 —— 不静默当空箱(空箱须显式传空集)。</summary>
        [Test]
        public void test_foldWeight_nullChildren_throwsInsteadOfTreatedAsEmpty()
        {
            Assert.Throws<ArgumentNullException>(
                () => ContainerWeightFold.FoldWeight(7, null),
                "null ≠ 空箱;静默当空箱 = 把「忘了传子件」读成「没有子件」");
        }

        /// <summary>折重域错:零 / 负 weight 须抛(越界校验归 21a AC-21a-15,此处为纯函数自保护)。</summary>
        [Test]
        public void test_instanceWeight_nonPositiveInputs_throw()
        {
            Assert.Throws<InvalidOperationException>(() => ContainerWeightFold.InstanceWeight(0, 1));
            Assert.Throws<InvalidOperationException>(() => ContainerWeightFold.InstanceWeight(1, 0));
            Assert.Throws<InvalidOperationException>(() => ContainerWeightFold.InstanceWeight(-1, 5));
        }

        // ═══════════════ P0 范围门 · ③ build_part quality 恒 1 ═══════════════

        /// <summary>P0 范围门 ③:非 <c>build_part</c> 实例任意品级放行 —— 本条只管建材那一轴。</summary>
        [Test]
        public void test_p0Gate_nonBuildPart_anyQualityPasses()
        {
            ItemDef[] defs = MakeDefs();
            IReadOnlyList<string> errs = BuildPartQualityGate.Validate(
                new List<ItemInstance> { Inst(500, WillowRaw, quality: 5, qty: 1) },
                Lookup(defs));

            Assert.IsEmpty(errs, "原料的品级合法性归 21a 的 MAX_QUALITY 校验,不在本条范围内");
        }

        /// <summary>P0 范围门 ③:<c>build_part</c> 实例 <c>quality != 1</c> ⇒ 违例(点名 id 与品级)。</summary>
        [Test]
        public void test_p0Gate_buildPartQualityNotOne_reportsViolation()
        {
            ItemDef[] defs = MakeDefs();
            IReadOnlyList<string> errs = BuildPartQualityGate.Validate(
                new List<ItemInstance> { Inst(501, PlankPart, quality: 3, qty: 1) },
                Lookup(defs));

            Assert.AreEqual(1, errs.Count, "一条违例");
            StringAssert.Contains("501", errs[0], "须点名 instance_id 便于定位");
            StringAssert.Contains("BL-7③", errs[0], "须点名范围门出处");
        }

        /// <summary>P0 范围门 ③:<c>build_part</c> 实例 <c>quality == 1</c> ⇒ 通过。</summary>
        [Test]
        public void test_p0Gate_buildPartQualityOne_passes()
        {
            ItemDef[] defs = MakeDefs();
            IReadOnlyList<string> errs = BuildPartQualityGate.Validate(
                new List<ItemInstance> { Inst(502, PlankPart, quality: 1, qty: 1) },
                Lookup(defs));

            Assert.IsEmpty(errs, "P0 期间 build_part 的合法品级恰为 1");
        }

        /// <summary>P0 范围门 ③:<c>RequireValid</c> 违例即抛,消息含全部违例。</summary>
        [Test]
        public void test_p0Gate_requireValid_throwsOnViolation()
        {
            ItemDef[] defs = MakeDefs();
            var ex = Assert.Throws<InvalidOperationException>(() => BuildPartQualityGate.RequireValid(
                new List<ItemInstance>
                {
                    Inst(503, PlankPart, quality: 2, qty: 1),
                    Inst(504, BoxPart, quality: 4, qty: 1),
                },
                Lookup(defs)));

            StringAssert.Contains("2 处违例", ex.Message, "消息须一次列全,不做逐条试错");
        }

        /// <summary>P0 范围门 ①②的**不可勾登记**:本件不提供容器 qty / quality 的断言
        /// —— 因为 P0 无「这是容器吗」的判据载体。
        /// <para>⚠️ 本条是**反向夹具**:它钉住「本件**没有**假装能判容器」这件事。
        /// 若将来有人在 <see cref="BuildPartQualityGate"/> 里加了以
        /// <c>Children.Length &gt; 0</c> 判容器的逻辑 ⇒ 本条转红 = **期待**(那时应改判据,不是删本条)。</para>
        /// </summary>
        [Test]
        public void test_p0Gate_containerInvariants_areNotFakedViaChildrenLength()
        {
            // 空容器(children = [])与普通实例在 `ItemInstance` 上**数据不可区分**;
            // 若某实现以 Children.Length > 0 判容器,它必然放过空容器 = 判据是假的。
            var emptyContainerShaped = Inst(600, BoxPart, quality: 1, qty: 1, children: Array.Empty<long>());
            var plainInstance = Inst(601, BoxPart, quality: 1, qty: 1);

            Assert.AreEqual(emptyContainerShaped.Children.Length, plainInstance.Children.Length,
                "空容器与非容器在 Children 面逐位相同 ⇒ 「以 children 非空判容器」在本型上不可实现");
            Assert.AreEqual(emptyContainerShaped.Qty, plainInstance.Qty,
                "同理 qty 面也不可分 —— 容器 qty 恒 1 的守卫需 is_container 标志(归 Story 010)");

            // 类型面反证:20 侧不得出现 is_container 的「私下约定」(如按 base_id 字面量硬编码)。
            // 判据 = BuildPartQualityGate 的公开面里没有「容器」判定入口。
            var publicMethods = typeof(BuildPartQualityGate)
                .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .Select(m => m.Name).ToArray();
            CollectionAssert.DoesNotContain(publicMethods, "IsContainer",
                "20 侧不得自造容器判定入口 —— 那会绕过 Story 010 的 schema 裁决");
        }

        // ═══════════════ 21a Story 010 既有执行体 · 补夹具(不重复实现) ═══════════════

        /// <summary>P0 禁嵌套容器(BL-7③ / BL-28)的执法体是 21a Story 010 的
        /// <see cref="ContainerClosure"/>(第六形态:child 自身有 children ⇒ 拒)。
        /// 本件**只补夹具**,不重复实现。</summary>
        [Test]
        public void test_nestedContainer_rejectedByExistingClosureValidator()
        {
            var instances = new List<ItemInstance>
            {
                Inst(700, BoxPart, 1, 1, children: new long[] { 701 }),   // 外层箱
                Inst(701, BoxPart, 1, 1, children: new long[] { 702 }),   // 内层箱 = 嵌套
                Inst(702, WillowRaw, 1, 1),                                // 叶子
            };

            var ex = Assert.Throws<InvalidOperationException>(
                () => ContainerClosure.Validate(instances));
            StringAssert.Contains("嵌套", ex.Message,
                "P0 禁嵌套容器 —— 执法体是 Story 010 的 ContainerClosure(AC-21a-58 第六形态)");
        }

        /// <summary>合法单层容器(容器 → 全部叶子)通过闭包校验 —— 对照组,证上一条不是恒红。</summary>
        [Test]
        public void test_singleLevelContainer_passesClosureValidation()
        {
            var instances = new List<ItemInstance>
            {
                Inst(800, BoxPart, 1, 1, children: new long[] { 801, 802 }),
                Inst(801, WillowRaw, 1, 1),
                Inst(802, WillowRaw, 1, 2),
            };

            Assert.DoesNotThrow(() => ContainerClosure.Validate(instances),
                "单层容器(children 全是叶子)合法 —— 对照组");
        }

        // ═══════════════ AC-20-15 · 输出 DTO 双层零浮点 ═══════════════

        /// <summary>AC-20-15 层 1:20 的全部输出 DTO 递归反射零浮点字段。</summary>
        [Test]
        public void test_ac2015_layer1_recursiveReflection_noFloatFields()
        {
            string unityProjectRoot = UnityProjectRoot();
            List<string> typeNames = InventoryDtoFloatGate.EnumerateTypeFullNames(unityProjectRoot);

            Assert.IsNotEmpty(typeNames,
                "扫描面须至少收到一个类型 —— 收到 0 个 = 门静默变绿");

            var roots = new List<Type>();
            foreach (string fullName in typeNames)
            {
                Type t = typeof(InventoryFold).Assembly.GetType(fullName)
                      ?? typeof(ItemInstance).Assembly.GetType(fullName);
                if (t != null) roots.Add(t);
            }
            Assert.IsNotEmpty(roots,
                "至少有一个类型可在装配里解析 —— 全部解析不到说明扫描面与装配分叉");

            var allErrs = new List<string>();
            foreach (Type root in roots) allErrs.AddRange(InventoryDtoFloatGate.ScanTypes(root));

            Assert.IsEmpty(allErrs,
                "20 的输出 DTO 须递归零浮点(AC-20-15 层 1;门 A / ADR-006):\n  "
                + string.Join("\n  ", allErrs));
        }

        /// <summary>AC-20-15 层 1 的**判别力**:内含 float 字段的假 DTO 必须被抓
        /// (否则门在扫但什么都不抓 = 空转)。</summary>
        [Test]
        public void test_ac2015_layer1_negativeFixture_floatFieldIsReported()
        {
            IReadOnlyList<string> errs = InventoryDtoFloatGate.ScanTypes(typeof(FixtureDtoWithFloat));

            Assert.IsNotEmpty(errs, "含 float 字段的 DTO 必须被抓 —— 否则 AC-20-15 的门恒绿");
            Assert.IsTrue(errs.Any(e => e.Contains("Ratio")),
                "报错须点名具体字段路径(Ratio):\n  " + string.Join("\n  ", errs));
        }

        /// <summary>AC-20-15 层 1 的**深挖判别力**:float 藏在 <c>List&lt;T&gt;</c> 泛参里也必须被抓
        /// (泛参展开是递归纪律里最容易漏的一条)。</summary>
        [Test]
        public void test_ac2015_layer1_negativeFixture_floatInsideGenericIsReported()
        {
            IReadOnlyList<string> errs = InventoryDtoFloatGate.ScanTypes(typeof(FixtureDtoWithNestedFloat));

            Assert.IsNotEmpty(errs, "float 藏在 List<T> 的 T 里也必须被抓(泛参展开)");
        }

        /// <summary>AC-20-15 层 1 的**假阳性守卫**:含枚举字段的 DTO 必须**干净**
        /// (枚举基类 <c>System.Enum</c> 带 <c>IConvertible.ToSingle/ToDouble/ToDecimal</c>,
        /// 展开它会把每个枚举字段都误判成浮点)。
        /// <para>⚠️ 这条钉的是 <c>if (t.IsEnum) return;</c> —— 2026-10-26 证伪轮 M14 实测:
        /// 拆掉该守卫后**生产面仍全绿**(基类链的 <c>IsNonProjectType</c> break 兜住了
        /// <c>System.Enum</c>),说明生产夹具对这个守卫**零判别力**。本夹具补上那一面。</para>
        /// </summary>
        [Test]
        public void test_ac2015_layer1_enumField_isNotReportedAsFloat()
        {
            IReadOnlyList<string> errs = InventoryDtoFloatGate.ScanTypes(typeof(FixtureDtoWithEnumField));

            Assert.IsEmpty(errs,
                "枚举字段不是浮点违例 —— 报出来就是假阳性(全项目每个枚举字段都会中招):\n  "
                + string.Join("\n  ", errs));
        }

        /// <summary>AC-20-15 层 2:20 两侧装配的 Cecil IL 扫描零浮点
        /// (指令 / 局部变量 / 字段 / 签名四面)。</summary>
        [Test]
        public void test_ac2015_layer2_ilScan_noFloatOpcodesOrTypes()
        {
            Assert.That(EditorUtility.scriptCompilationFailed, Is.False,
                "跑 Cecil 扫描前提:编译成功(扫到上一版产物 = 假绿)");

            string unityProjectRoot = UnityProjectRoot();
            List<string> typeNames = InventoryDtoFloatGate.EnumerateTypeFullNames(unityProjectRoot);

            string simDll = AssemblyGates.ScriptAssemblyPath("Sim");
            string contractsDll = AssemblyGates.ScriptAssemblyPath("Sim.Contracts");

            var errs = new List<string>();
            errs.AddRange(InventoryDtoFloatGate.ScanIl(simDll, typeNames, out int n1));
            errs.AddRange(InventoryDtoFloatGate.ScanIl(contractsDll, typeNames, out int n2));

            Assert.Greater(n1 + n2, 0, "两装配合起来须至少扫到 1 个类型 —— 0 = 面空了");

            // 两层共面:源目录声明的类型必须都能在编译产物里找到
            errs.AddRange(InventoryDtoFloatGate.MissingTypeNames(typeNames, simDll, contractsDll));

            Assert.IsEmpty(errs,
                "20 两侧装配须零浮点(AC-20-15 层 2;门 A):\n  " + string.Join("\n  ", errs));
        }

        /// <summary>AC-20-15 层 2 的判别力:对**测试装配内**的假类型跑同一谓词必须红
        /// (与 <c>ecozone_query_test</c> 的负面夹具同款 —— 谓词按类型全名解析,与生产侧同一条路径)。</summary>
        [Test]
        public void test_ac2015_layer2_negativeFixture_reportsRed()
        {
            Assert.That(EditorUtility.scriptCompilationFailed, Is.False,
                "跑 Cecil 扫描前提:编译成功");

            string testDll = AssemblyGates.ScriptAssemblyPath("Sim.Contracts.Tests");
            var errs = InventoryDtoFloatGate.ScanIl(
                testDll,
                new[] { typeof(FixtureIlWithFloat).FullName },
                out int scanned);

            Assert.AreEqual(1, scanned, "负面夹具类型须被扫到");
            Assert.IsNotEmpty(errs, "含 conv.r4 / float 局部的假类型必须被同一谓词抓住");
            Assert.IsTrue(errs.Any(e => e.Contains("浮点指令")),
                "须报出指令面(conv.r4):\n  " + string.Join("\n  ", errs));
        }

        /// <summary>AC-20-15 层 2:**入参**清单为空 ⇒ 红(拒以空集冒充绿)。
        /// <para>⚠️ 本夹具只钉 <c>ScanIl</c> 的**入参早守卫**(<c>typeFullNames == null ||
        /// Count == 0</c>)。它**不覆盖**「清单非空但该装配里一个都匹配不到」那条
        /// —— 那条由下一条夹具单独钉。2026-10-26 证伪轮 M13 原先把两者混为一谈,
        /// 打的是后一条的守卫,被这一条的早退抢先 ⇒ 全绿逃逸。</para>
        /// </summary>
        [Test]
        public void test_ac2015_layer2_emptyTypeList_isRed()
        {
            string simDll = AssemblyGates.ScriptAssemblyPath("Sim");
            List<string> errs = InventoryDtoFloatGate.ScanIl(simDll, Array.Empty<string>(), out _);

            Assert.IsNotEmpty(errs, "空清单 = 关了门 = 永远绿 ⇒ 必须红");
        }

        /// <summary>AC-20-15 层 2:**清单非空但该装配零命中** ⇒ 红。
        /// <para>这是与上一条**不同**的属性:上一条判「调用方传了空集」,
        /// 本条判「清单与装配分叉」(分派错 / 编译未成功 / 扫错 dll)。
        /// 守卫 = <c>scannedTypeCount == 0</c>,即 <see cref="InventoryDtoFloatGate.ScanIl"/>
        /// 的收尾判定。</para>
        /// </summary>
        [Test]
        public void test_ac2015_layer2_nonEmptyListButZeroHitInAssembly_isRed()
        {
            string contractsDll = AssemblyGates.ScriptAssemblyPath("Sim.Contracts");

            // 清单非空(过得了入参早守卫),但**不含 Sim.Contracts 里的任何类型** ⇒ 零命中。
            List<string> errs = InventoryDtoFloatGate.ScanIl(
                contractsDll, new[] { "DaYiJingCheng.Sim.ItemDatabase.NoSuchTypeForGateTest" }, out int scanned);

            Assert.AreEqual(0, scanned, "该装配须一个都匹配不到 —— 否则夹具失效");
            Assert.IsNotEmpty(errs,
                "清单非空却零命中 = 扫描面与装配分叉 ⇒ 必须红,不得静默返回「没问题」");
            Assert.IsTrue(errs.Any(e => e.Contains("零类型命中")),
                "须报出来的是「零命中」这条,而不是别的:\n  " + string.Join("\n  ", errs));
        }

        /// <summary>扫描目录守卫:目录被搬走 ⇒ 红(而不是静默变绿)。</summary>
        [Test]
        public void test_ac2015_scanDirGuard_missingDirIsRed()
        {
            List<string> errs = InventoryDtoFloatGate.CheckScanDirsExist(
                Path.Combine(Path.GetTempPath(), "no-such-unity-root-" + Guid.NewGuid().ToString("N")));

            Assert.AreEqual(InventoryDtoFloatGate.ScanDirsRelative.Length, errs.Count,
                "两个扫描目录都不存在 ⇒ 两条红;拒绝静默变绿");
        }

        // ───────────────────────── 辅助 ─────────────────────────

        /// <summary>工程根(<c>unity/</c>)。由 <c>Assembly.Location</c> 反推,不是 cwd
        /// (cwd 在 batch 模式下是工程根上一级,见既有测试的同一注记)。</summary>
        private static string UnityProjectRoot()
        {
            // .../unity/Library/ScriptAssemblies/Sim.Contracts.Tests.dll → .../unity
            string testDll = AssemblyGates.ScriptAssemblyPath("Sim.Contracts.Tests");
            string scriptAssemblies = Path.GetDirectoryName(testDll);
            return Path.GetFullPath(Path.Combine(scriptAssemblies, "..", ".."));
        }
    }

    // ───────── 负面夹具(层 1:递归反射;放在测试装配里,不进生产面) ─────────

    /// <summary>层 1 负面夹具:直接带 float 字段。</summary>
    public sealed class FixtureDtoWithFloat
    {
        /// <summary>违禁字段(AC-20-15 层 1 必须抓到)。</summary>
        public readonly float Ratio;
    }

    /// <summary>层 1 负面夹具:float 藏在泛型实参里(泛参展开的判别力)。</summary>
    public sealed class FixtureDtoWithNestedFloat
    {
        /// <summary>浮点藏在 <c>List&lt;float&gt;</c> 的实参里。</summary>
        public readonly List<float> Samples;
    }

    /// <summary>层 1 **正向**夹具:<b>枚举字段不得被判成浮点</b>。
    /// <para>判别力说明:枚举的基类是 <c>System.Enum</c>,它带
    /// <c>IConvertible.ToSingle/ToDouble/ToDecimal</c> 三个返回浮点的方法 ——
    /// 若展开枚举的成员,<b>每一个枚举字段</b>都会被判成浮点违例(全假阳性)。
    /// 承重守卫 = <c>InventoryDtoFloatGate:238</c> 的 <c>if (t.IsEnum) return;</c>。</para>
    /// <para>⚠️ 2026-10-26 证伪轮首跑实测:该守卫原本**没有专属夹具**,
    /// 拆掉它后生产面全绿(因为基类链那条 break 兜住了 <c>System.Enum</c>)⇒ 全绿逃逸。</para>
    /// </summary>
    public sealed class FixtureDtoWithEnumField
    {
        /// <summary>枚举字段 —— 须被**放行**,不是违例。</summary>
        public readonly ItemCategory Category;
    }

    /// <summary>层 2 负面夹具:方法体含 <c>conv.r4</c> + float 局部。
    /// <para>⚠️ 本型只被 Cecil 扫描,不被调用 —— 它的存在即为「谓词真的能抓」的证据。</para>
    /// </summary>
    public sealed class FixtureIlWithFloat
    {
        /// <summary>把 int 转 float 返回 —— IL 上是 <c>conv.r4</c>。</summary>
        public float ToFloat(int raw) => (float)raw;
    }
}
