// 权威来源:Story 010(production/epics/item-database/story-010-instance-authority-persistence.md)
//     · AC-21a-31(实例经全二进制 codec 往返:item_key/quality 值级相等、qty 物化不重算、children 闭包一致)
//     · AC-21a-34(容器结构守恒:固定操作序列逐步核对,无 id 静默丢弃/复制、Σqty 守恒)
//     · AC-21a-35(deprecated 条目仍可解析;物理删除 ⇒ 对照组硬失败)
//     · AC-21a-58(容器闭包四形态 + 边例硬失败;负向夹具 = invalid_container_closure.json)
//   GDD:design/gdd/item-database.md §Schema A(只增不删)/ §Schema E(qty 物化、children=叶子 id 列表)
//   ADR-010(全二进制 codec、读档解析落点)· ADR-006 §五(D-21-18)· ADR-009 §二(身份进流)
//
// ⚠️ 落点:账本路径 = tests/integration/item_database/instance_authority_persistence_test.cs(AC-31/35)
//    与 tests/unit/item_database/instance_authority_persistence_test.cs(AC-34/58)——
//    两个账本条目指向**同一个真身**(本文件;Unity 只编译 unity/Assets/ 树,Story 001–009 同一先例)。
//
// ⚠️ 「经 7a 持久化往返」承位口径(禁借绿,同 Story 009):7a 文件级存档(头+校验和+checkpoint)
//    未实现 ⇒ 往返半边 = ItemInstanceCodec 字节级 encode→decode(ADR-010 存档快照段的同一编码路径);
//    真·文件存档往返归 7a 落地后补跑,本文件不宣称该半边已绿。
//
// ⚠️ AC-58 边例文本张力(登记):QA「深链无环(过)」与 Schema E「嵌套容器即拒」并存 ——
//    本文件按「环检测不误伤无环图」读:深链被**嵌套**规则拒(报因含「嵌套」不含「环」),
//    环检测单独由 A→B→A 夹具证其报因。见 test_ac21a58_deepChain_rejectedByNestingNotCycle。
//
// 测试纪律:test_[scenario]_[expected];确定性固定序列(无随机、无时间依赖、无 I/O ——
//    夹具文件读取是唯一 I/O,属 Assembly 级负向夹具既有先例)。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.Codec;

namespace DaYiJingCheng.Tests.Unit.ItemDatabase
{
    [TestFixture]
    internal sealed class InstanceAuthorityPersistenceTest
    {
        private const int MaxQuality = 5;   // §Schema A 品级上界(夹具值;数值归数值轮)

        // ═══════════════════════ AC-21a-31 往返不变 ═══════════════════════

        [Test]
        public void test_ac21a31_instance_roundTrip_preservesKeyQualityQtyChildren()
        {
            // Given —— 含 quality 的实例;qty 物化值(与任何公式无关的字面量)
            var original = new ItemInstance(
                instanceId: 7L,
                key: new ItemKey("willow_bark", ProcessingState.Dried),
                quality: 3,
                qty: 5,                          // Schema E:物化存储,读档不重算
                children: new long[0]);

            // When —— 全二进制 codec 保存 → 加载(7a 文件级往返见文件头承位注)
            ItemInstance restored = RoundTrip(original);

            // Then —— item_key 两分量与 quality 值级相等;qty 不被重算;children 闭包一致
            Assert.That(restored.InstanceId, Is.EqualTo(original.InstanceId), "instance_id 不变");
            Assert.That(restored.Key.BaseId, Is.EqualTo("willow_bark"), "item_key.base_id 序数相等");
            Assert.That(restored.Key.State, Is.EqualTo(ProcessingState.Dried), "item_key.state 相等");
            Assert.That(restored.Quality, Is.EqualTo(3), "quality 值级相等");
            Assert.That(restored.Qty, Is.EqualTo(5),
                "qty 物化存储往返不变(Schema E:往返不得触发 F1 重算改量 —— codec 内不存在求解器调用路径)");
            Assert.That(restored.Children, Is.Not.Null, "children 空集须是 [] 不是 null(AC-31 边例)");
            Assert.That(restored.Children.Length, Is.EqualTo(0), "空 children 长度 0");
        }

        [TestCase(1, TestName = "AC-31 quality=1 下极")]
        [TestCase(5, TestName = "AC-31 quality=5 上极(MAX_QUALITY)")]
        public void test_ac21a31_instance_roundTrip_qualityEdges(int quality)
        {
            var original = new ItemInstance(
                11L, new ItemKey("ginseng", ProcessingState.Raw), quality, 1, new long[0]);

            ItemInstance restored = RoundTrip(original);

            Assert.That(restored.Quality, Is.EqualTo(quality), "品级两极值级相等");
            Assert.That(restored.Qty, Is.EqualTo(1), "容器/单件 qty=1 不变");
        }

        [Test]
        public void test_ac21a31_containerInstance_roundTrip_childrenClosureConsistent()
        {
            var container = new ItemInstance(
                100L, new ItemKey("clinic_storage", ProcessingState.Raw),
                quality: 1, qty: 1, children: new long[] { 7L, 8L, 9L });

            ItemInstance restored = RoundTrip(container);

            Assert.That(restored.Children, Is.EqualTo(new long[] { 7L, 8L, 9L }),
                "children id 列表逐元素往返(容器结构 = id 列表,Schema E)");
            Assert.That(restored.Qty, Is.EqualTo(1), "容器 qty 恒 1(AC-34 边例不变量)");
            Assert.That(restored.Quality, Is.EqualTo(1), "容器 quality 恒 1");
        }

        [Test]
        public void test_ac21a31_batchRoundTrip_multiInstances_eachUnchanged()
        {
            var batch = new[]
            {
                new ItemInstance(1L, new ItemKey("a", ProcessingState.Raw), 1, 1, new long[0]),
                new ItemInstance(2L, new ItemKey("a", ProcessingState.Dried), 2, 9, new long[0]),
                new ItemInstance(3L, new ItemKey("b", ProcessingState.Raw), 5, 100, new long[] { 1L }),
                new ItemInstance(4L, new ItemKey("b", ProcessingState.Pill), 3, 2, new long[] { 1L, 2L }),
                new ItemInstance(5L, new ItemKey("c", ProcessingState.Raw), 4, 7, new long[0]),
            };

            foreach (ItemInstance original in batch)
            {
                ItemInstance restored = RoundTrip(original);
                Assert.That(restored.InstanceId, Is.EqualTo(original.InstanceId), $"批量[{original.InstanceId}] id");
                Assert.That(restored.Key, Is.EqualTo(original.Key), $"批量[{original.InstanceId}] item_key 复合键");
                Assert.That(restored.Quality, Is.EqualTo(original.Quality), $"批量[{original.InstanceId}] quality");
                Assert.That(restored.Qty, Is.EqualTo(original.Qty), $"批量[{original.InstanceId}] qty");
                Assert.That(restored.Children, Is.EqualTo(original.Children), $"批量[{original.InstanceId}] children");
            }
        }

        [Test]
        public void test_ac21a31_encode_nullChildren_rejected()
        {
            // ctor 自身已拒 null(ArgumentNullException,Story 002 形状)—— 唯一能造出
            // null Children 的路径是 default(ItemInstance)(绕过 ctor 的缺省形);Encode 须兜底拒收。
            Assert.Throws<ArgumentNullException>(
                () => new ItemInstance(1L, new ItemKey("a", ProcessingState.Raw), 1, 1, children: null),
                "构造层先拒:null children 违反 §Schema E「非空禁 null」");

            ItemInstance defaulted = default;   // Children == null 的唯一合法来源
            Assert.Throws<ArgumentException>(
                () => ItemInstanceCodec.Encode(defaulted),
                "null children 不进字节面 —— 空容器必须是空数组(AC-31 边例:=[],非 null)");
        }

        // ═══════════════════════ AC-21a-35 只增不删 ═══════════════════════

        [Test]
        public void test_ac21a35_deprecatedEntry_stillResolves()
        {
            var data = new ItemDataSet
            {
                Items = new[]
                {
                    new ItemDef { BaseId = "old_herb", ProcessingState = ProcessingState.Raw,
                        Deprecated = true, DisplayName = "旧药", StackMax = 1, Weight = 1 },
                },
            };
            var instance = new ItemInstance(
                3L, new ItemKey("old_herb", ProcessingState.Raw), 1, 1, new long[0]);

            ItemDef resolved = InstanceResolver.Resolve(instance, data);

            Assert.That(resolved.BaseId, Is.EqualTo("old_herb"), "deprecated 条目照常解析(只增不删)");
            Assert.That(resolved.Deprecated, Is.True, "废弃标记随条目返回(表现/堆叠语义归 20)");
        }

        [Test]
        public void test_ac21a35_missingEntry_controlFails()
        {
            // 对照组:条目被物理删除(政策违例)⇒ 解析硬失败,证明「门有效」
            var data = new ItemDataSet { Items = new ItemDef[0] };
            var instance = new ItemInstance(
                3L, new ItemKey("physically_deleted", ProcessingState.Raw), 1, 1, new long[0]);

            var ex = Assert.Throws<InvalidOperationException>(
                () => InstanceResolver.Resolve(instance, data),
                "物理删除条目 ⇒ 存档实例解析必须失败(静默丢 = 物品凭空消失)");
            Assert.That(ex.Message, Does.Contain("只增不删"), "失败信息须点名政策(诊断面)");
        }

        [Test]
        public void test_ac21a35_stateMismatch_isCompositeKeyMiss()
        {
            // 复合主键:同 base_id 不同 state = 不同条目(§Schema A)—— 半边命中不算命中
            var data = new ItemDataSet
            {
                Items = new[]
                {
                    new ItemDef { BaseId = "herb", ProcessingState = ProcessingState.Raw,
                        Deprecated = false, StackMax = 1, Weight = 1 },
                },
            };
            var instance = new ItemInstance(
                4L, new ItemKey("herb", ProcessingState.Dried), 1, 1, new long[0]);

            Assert.Throws<InvalidOperationException>(
                () => InstanceResolver.Resolve(instance, data),
                "(herb, Dried) 不在表内 —— 只有 (herb, Raw);复合键半边命中 = 未命中");
        }

        // ═══════════════════════ AC-21a-58 闭包四形态 + 边例 ═══════════════════════

        [Test]
        public void test_ac21a58_sharedChild_twoContainers_throws()
        {
            var instances = new[]
            {
                Container(1, 10L),
                Container(2, 10L),
                Leaf(10L),
            };

            var ex = Assert.Throws<InvalidOperationException>(() => ContainerClosure.Validate(instances));
            Assert.That(ex.Message, Does.Contain("同属两个容器"), "共享子件的报因须点名双属");
        }

        [Test]
        public void test_ac21a58_selfReference_throws()
        {
            var instances = new[] { Container(1, 1L) };

            var ex = Assert.Throws<InvalidOperationException>(() => ContainerClosure.Validate(instances));
            Assert.That(ex.Message, Does.Contain("自身"), "自引用报因");
        }

        [Test]
        public void test_ac21a58_cycle_a_to_b_to_a_throws()
        {
            var instances = new[]
            {
                Container(1, 2L),   // 1 → 2
                Container(2, 1L),   // 2 → 1(环)
            };

            var ex = Assert.Throws<InvalidOperationException>(() => ContainerClosure.Validate(instances));
            Assert.That(ex.Message, Does.Contain("环"), "A→B→A 的报因须是环(判序:环先于嵌套)");
        }

        [Test]
        public void test_ac21a58_unregisteredChild_throws()
        {
            var instances = new[]
            {
                Container(1, 99L),  // 99 不在登记表
                Leaf(10L),
            };

            var ex = Assert.Throws<InvalidOperationException>(() => ContainerClosure.Validate(instances));
            Assert.That(ex.Message, Does.Contain("未登记"), "未登记子件报因");
        }

        [Test]
        public void test_ac21a58_duplicateRegistryId_throws()
        {
            var instances = new[]
            {
                Leaf(10L),
                Leaf(10L),          // 同 id 两份登记 = 静默复制
            };

            var ex = Assert.Throws<InvalidOperationException>(() => ContainerClosure.Validate(instances));
            Assert.That(ex.Message, Does.Contain("重复"), "登记表重复 id 报因");
        }

        [Test]
        public void test_ac21a58_nestedContainer_throws()
        {
            // Schema E:children 只能是叶子 id —— P → C(自身是容器)→ L 即嵌套
            var instances = new[]
            {
                Container(1, 2L),
                Container(2, 10L),
                Leaf(10L),
            };

            var ex = Assert.Throws<InvalidOperationException>(() => ContainerClosure.Validate(instances));
            Assert.That(ex.Message, Does.Contain("嵌套"), "嵌套容器报因(Schema E)");
        }

        [Test]
        public void test_ac21a58_deepChain_rejectedByNestingNotCycle()
        {
            // QA 边例「深链无环(过)」的读法见文件头:深链的拒绝因 = 嵌套,不是环;
            // 本用例同时证 ① 深链确实被拒(Schema E 禁嵌套)② 拒绝**不是**环检测误伤。
            var instances = new[]
            {
                Container(1, 2L),   // 1 → 2 → 3(两层中间容器)
                Container(2, 3L),
                Container(3, 10L),
                Leaf(10L),
            };

            var ex = Assert.Throws<InvalidOperationException>(() => ContainerClosure.Validate(instances));
            Assert.That(ex.Message, Does.Contain("嵌套"), "深链报因 = 嵌套");
            Assert.That(ex.Message, Does.Not.Contain("存在环"), "深链不得被环检测误伤(QA「深链无环」的读法)");
        }

        [Test]
        public void test_ac21a58_legalStructures_pass()
        {
            // 合法单层容器 + 空容器 + 叶子空 children —— 全部静默通过
            var legal = new[]
            {
                Container(1, 10L, 11L),   // 单层容器 → 两叶子
                Container(2),             // 空容器
                Leaf(10L),
                Leaf(11L),
                Leaf(12L),                // 未被引用的游离叶子(合法:世界层散件)
            };

            Assert.DoesNotThrow(() => ContainerClosure.Validate(legal),
                "合法单层 + 空容器 + 空 children 必须通过(AC-58 Then 后半)");
        }

        [Test]
        public void test_ac21a58_namedFixture_fileForms_allBehaveAsExpected()
        {
            // QA 指名负向夹具:invalid_container_closure.json(六案例 = 四形态 + 两合法)
            string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            string fixturePath = Path.Combine(
                repoRoot, "tests", "unit", "item_database", "fixtures", "invalid_container_closure.json");

            Assert.That(File.Exists(fixturePath), Is.True,
                $"负向夹具缺失:归档路径 = tests/unit/item_database/fixtures/invalid_container_closure.json(未找到 {fixturePath})");

            var root = JsonUtility.FromJson<FixtureRoot>(File.ReadAllText(fixturePath));
            Assert.That(root.cases, Is.Not.Null, "夹具 cases 字段存在");
            Assert.That(root.cases.Length, Is.GreaterThan(0), "夹具 cases 至少一例");

            int seenThrow = 0, seenPass = 0;
            foreach (FixtureCase fixtureCase in root.cases)
            {
                ItemInstance[] instances = fixtureCase.instances
                    .Select(i => new ItemInstance(
                        i.id,
                        new ItemKey("fx_" + i.id, ProcessingState.Raw),
                        quality: 1, qty: 1,
                        children: (i.children ?? Array.Empty<int>()).Select(c => (long)c).ToArray()))
                    .ToArray();

                if (fixtureCase.expect == "throw")
                {
                    seenThrow++;
                    Assert.Throws<InvalidOperationException>(
                        () => ContainerClosure.Validate(instances),
                        $"夹具 [{fixtureCase.name}] 期望硬失败(四形态)");
                }
                else if (fixtureCase.expect == "pass")
                {
                    seenPass++;
                    Assert.DoesNotThrow(
                        () => ContainerClosure.Validate(instances),
                        $"夹具 [{fixtureCase.name}] 期望通过");
                }
                else
                {
                    Assert.Fail($"夹具 [{fixtureCase.name}] expect 值非法: {fixtureCase.expect}");
                }
            }

            Assert.That(seenThrow, Is.GreaterThanOrEqualTo(4), "四形态各至少一例(QA Given)");
            Assert.That(seenPass, Is.GreaterThanOrEqualTo(2), "合法单层 + 空容器各至少一例");
        }

        // ═══════════════════════ AC-21a-34 容器结构守恒(固定操作序列)═══════════════════════

        [Test]
        public void test_ac21a34_fixedOperationSequence_conservesIdsAndQty()
        {
            // 确定性固定序列(无随机):建容器 → 入箱 → 移出 → 移回 → 满溢新铸 → 删除容器再安置
            var authority = new IdAuthority();
            var registry = new Dictionary<long, ItemInstance>();

            // ── step0:铸 container0 + 两叶子(经权威;id 唯一)──
            long c0 = authority.NextItemInstanceId().Value;
            long a = authority.NextItemInstanceId().Value;
            long b = authority.NextItemInstanceId().Value;
            registry[c0] = Container(c0, Array.Empty<long>());
            registry[a] = LeafWithQty(a, 2);
            registry[b] = LeafWithQty(b, 3);
            long expectedTotalQty = 1 + 2 + 3;   // 容器 qty=1 恒定(边例不变量)

            AssertConservation(registry, expectedTotalQty, "step0 初始登记");

            // ── step1:两叶子入箱 ──
            SetChildren(registry, c0, a, b);
            AssertConservation(registry, expectedTotalQty, "step1 入箱");
            AssertChildrenExactly(registry, c0, "step1 入箱后 children", a, b);

            // ── step2:a 移出(回世界层)—— 仍在登记表,不许静默丢 ──
            SetChildren(registry, c0, b);
            Assert.That(registry.ContainsKey(a), Is.True, "step2 移出的实例仍在登记表(不静默丢)");
            AssertConservation(registry, expectedTotalQty, "step2 移出");

            // ── step3:a 再移回 ──
            SetChildren(registry, c0, a, b);
            AssertConservation(registry, expectedTotalQty, "step3 移回");
            AssertChildrenExactly(registry, c0, "step3 移回后 children", a, b);

            // ── step4:满溢出 → 新实例 id 经权威新铸(唯一)──
            long d = authority.NextItemInstanceId().Value;
            Assert.That(registry.ContainsKey(d), Is.False, "step4 新铸 id 不与既有 id 相撞(唯一)");
            registry[d] = LeafWithQty(d, 4);
            expectedTotalQty += 4;
            SetChildren(registry, c0, a, b, d);
            AssertConservation(registry, expectedTotalQty, "step4 溢出新实例入箱");

            // ── step5:铸 container1 → 把 c0 的子件全部再安置 → 删除 c0(子件不得静默丢)──
            long c1 = authority.NextItemInstanceId().Value;
            registry[c1] = Container(c1, Array.Empty<long>());
            expectedTotalQty += 1;                      // 新容器 qty=1 入账
            long[] orphans = GetChildren(registry, c0);
            Assert.That(orphans, Is.EqualTo(new long[] { a, b, d }),
                "step5 删除前先取证:c0 内子件 = 移出后的 a + b + d");

            SetChildren(registry, c1, orphans);      // 再安置(20 语义:显式去向,非静默)
            registry.Remove(c0);                     // 删除容器本身
            expectedTotalQty -= 1;                   // 旧容器 qty=1 离场

            Assert.That(registry.ContainsKey(c0), Is.False, "step5 容器已删");
            foreach (long orphan in orphans)
                Assert.That(registry.ContainsKey(orphan), Is.True, $"step5 子件 {orphan} 未随容器消失");
            AssertChildrenExactly(registry, c1, "step5 再安置后 c1 children", a, b, d);
            AssertConservation(registry, expectedTotalQty, "step5 删除容器后");
        }

        // ─────────── 守恒断言与工具 ───────────

        /// <summary>AC-34 Then:登记表 id 互异(字典天然)、容器引用全部已登记且无双属、
        /// Σ 各处 qty 与期望一致、容器 qty/quality 恒 1;并跑闭包执法体(结构非法即抛)。</summary>
        private static void AssertConservation(
            Dictionary<long, ItemInstance> registry, long expectedTotalQty, string step)
        {
            ContainerClosure.Validate(registry.Values.ToList());   // 结构执法体(AC-58)

            long totalQty = 0;
            var parentCount = new Dictionary<long, int>();
            foreach (ItemInstance inst in registry.Values)
            {
                totalQty += inst.Qty;
                if (inst.Children == null) continue;
                foreach (long childId in inst.Children)
                {
                    Assert.That(registry.ContainsKey(childId), Is.True,
                        $"{step}:容器 {inst.InstanceId} 引用未登记子件 {childId}(静默丢弃的前兆)");
                    parentCount.TryGetValue(childId, out int n);
                    parentCount[childId] = n + 1;
                    Assert.That(n + 1, Is.LessThanOrEqualTo(1),
                        $"{step}:子件 {childId} 同属两容器(静默复制)");
                }

                bool looksLikeContainer = inst.Children.Length > 0 ||
                                          inst.Key.BaseId.StartsWith("container", StringComparison.Ordinal);
                if (looksLikeContainer)
                {
                    Assert.That(inst.Qty, Is.EqualTo(1), $"{step}:容器 {inst.InstanceId} qty 恒 1");
                    Assert.That(inst.Quality, Is.EqualTo(1), $"{step}:容器 {inst.InstanceId} quality 恒 1");
                }
            }

            Assert.That(totalQty, Is.EqualTo(expectedTotalQty),
                $"{step}:Σ 各处 qty 不守恒(期望 {expectedTotalQty},实得 {totalQty})—— AC-34 + 交叉 AC-33");
        }

        private static void AssertChildrenExactly(
            Dictionary<long, ItemInstance> registry, long containerId, string step, params long[] expected)
        {
            Assert.That(GetChildren(registry, containerId), Is.EqualTo(expected),
                $"{step}:容器 {containerId} children 与预期不一致");
        }

        private static long[] GetChildren(Dictionary<long, ItemInstance> registry, long id) =>
            registry[id].Children ?? Array.Empty<long>();

        private static void SetChildren(
            Dictionary<long, ItemInstance> registry, long containerId, params long[] children) =>
            SetChildren(registry, containerId, (IEnumerable<long>)children);

        private static void SetChildren(
            Dictionary<long, ItemInstance> registry, long containerId, IEnumerable<long> children)
        {
            ItemInstance c = registry[containerId];
            registry[containerId] = new ItemInstance(
                c.InstanceId, c.Key, c.Quality, c.Qty, children.ToArray());
        }

        // ─────────── 实例工厂 ───────────

        private static ItemInstance Leaf(long id) => LeafWithQty(id, 1);

        private static ItemInstance LeafWithQty(long id, int qty) =>
            new ItemInstance(id, new ItemKey("leaf_" + id, ProcessingState.Raw), 1, qty, new long[0]);

        private static ItemInstance Container(long id, params long[] children) =>
            new ItemInstance(
                id, new ItemKey("container_" + id, ProcessingState.Raw), 1, 1, children);

        private static ItemInstance RoundTrip(ItemInstance original) =>
            ItemInstanceCodec.Decode(ItemInstanceCodec.Encode(original));

        // ─────────── 夹具 DTO(JsonUtility 承载;id = int 窄域,内转 long)───────────

        [Serializable]
        private sealed class FixtureRoot
        {
            public FixtureCase[] cases;
        }

        [Serializable]
        private sealed class FixtureCase
        {
            public string name;
            public string expect;
            public FixtureInstance[] instances;
        }

        [Serializable]
        private sealed class FixtureInstance
        {
            public int id;
            public int[] children;   // 缺省 = 叶子(JsonUtility 缺字段 = null)
        }
    }
}
