// S20-19 · 42 建造发起方接线 —— **建造选料器**夹具(AC B-5)+ 库存门生产实装
//
// 权威来源:
//   design/gdd/inventory-and-items.md 规则六(:348-372)
//     · 原子单位「建造扣料 = 一次 `StructurePlaced` 的 `cost(m)`」
//     · **BL-23 兜底序**:玩家未点名实例 ⇒ 同 `item_key` 多栈按 `instance_id` **升序**逐栈扣
//     · 规则九 Edge Case:不足 ⇒ **整手拒绝**,不部分扣
//   design/gdd/modular-building.md 规则二 条件⑤(库存门)· 规则七(:124 `cost(m)`)
//   `production/sprints/sprint-20.md` §十 S20-19 的 AC B-5
//
// ─────────────────────────────────────────────────────────────────────────────
// ⚠️ 卡面 B-5 原文「跨栈 ⇒ **N 条相邻 `Seq`**」已作废(就地加注见 sprint-20 §十)
// ─────────────────────────────────────────────────────────────────────────────
// 原文假定「扣料 = 写 N 条消耗事件」。实测 `InventoryFold.ApplyBuildCost`
// (`InventoryFold.cs:929`)已按 `cost(m)` 从 `StructurePlaced` **反推**扣减
// (2026-10-27 R16 甲案)⇒ 再写 N 条消耗事件会**双重扣减**。
// ⇒ 本批的载体改为**纯函数产出物** `BuildMaterialPlan.Lines`:
//     · 「跨栈」= N 条 `ConsumeLine`;
//     · 「升序」= `Lines` 的**数组序**;
//     · 「不重复点名」= 同一 `InstanceId` 在 `Lines` 里**只出现一次**。
//   这三条属性的**可证伪性**与原文等价,只是观测对象从事件流换成了计划。
//
// ⚠️ 本件零随机 / 零时间依赖 / 零写盘;id 与数量全部写死字面量
//    (禁引用被测类的常量 —— 自指判据会让变异两边一起动)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Presentation.Building;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.ModularBuilding
{
    /// <summary>AC B-5:规则六 BL-23 兜底序的唯一实现点。</summary>
    [TestFixture]
    public class BuildMaterialPlannerTest
    {
        // 夹具值(非 GDD 数值):owner 42 是「唯一玩家」,43 是「另一个人」。
        private const int Owner = 42;
        private const int OtherOwner = 43;
        private const int Ordinal = 7;
        private const int OtherOrdinal = 8;

        private PayloadEncoder _encoder;
        private PayloadDecoder _decoder;

        [SetUp]
        public void Setup()
        {
            var pool = new InMemoryBlobPool();
            _encoder = new PayloadEncoder(pool);
            _decoder = new PayloadDecoder(pool);
        }

        // ══════════════════════════════════════════════════════════════════
        // 行表直测(核心重载 `Plan(owner, ordinal, need, rows)`)
        // ══════════════════════════════════════════════════════════════════

        private static InventoryRow Row(long id, int ordinal, int qty,
                                        int owner = Owner, int quality = 1)
            => new InventoryRow(id, ordinal, quality, qty, owner);

        private static IReadOnlyDictionary<long, InventoryRow> Rows(params InventoryRow[] rows)
        {
            var d = new Dictionary<long, InventoryRow>(rows.Length);
            foreach (InventoryRow r in rows) d[r.InstanceId] = r;
            return d;
        }

        /// <summary>把计划摊成可读串 —— 断言用它而不是逐字段比,失败信息才看得出「选了哪几栈」。</summary>
        private static string Show(BuildMaterialPlan p)
        {
            var parts = new List<string>(p.Lines.Length);
            foreach (ConsumeLine line in p.Lines)
                parts.Add(line.InstanceId + ":" + line.Qty);
            return string.Join(",", parts);
        }

        [Test]
        public void test_plan_rows_ascendingInstanceId_selectsLowestIdsFirst()
        {
            // Arrange:三栈同 ordinal,id 故意乱序放进字典(字典枚举序不是契约)
            var rows = Rows(Row(9, Ordinal, 3), Row(2, Ordinal, 3), Row(5, Ordinal, 3));

            // Act
            BuildMaterialPlan plan = BuildMaterialPlanner.Plan(Owner, Ordinal, need: 4, rows);

            // Assert:2 号栈吃满 3 份,5 号栈再补 1 份;9 号栈不动
            Assert.That(plan.IsComplete, Is.True);
            Assert.That(Show(plan), Is.EqualTo("2:3,5:1"),
                "BL-23 兜底序 = instance_id 升序;乱序字典不得影响选取序");
        }

        [Test]
        public void test_plan_rows_crossStacks_emitsOneLinePerStack()
        {
            // Arrange:三栈各 2 份,共 6 份;需要 5 份 ⇒ 必然跨 3 栈
            var rows = Rows(Row(1, Ordinal, 2), Row(2, Ordinal, 2), Row(3, Ordinal, 2));

            // Act
            BuildMaterialPlan plan = BuildMaterialPlanner.Plan(Owner, Ordinal, need: 5, rows);

            // Assert
            Assert.That(plan.Lines.Length, Is.EqualTo(3), "跨三栈 ⇒ 恰三条 ConsumeLine");
            Assert.That(Show(plan), Is.EqualTo("1:2,2:2,3:1"));
        }

        [Test]
        public void test_plan_rows_singleStack_isNamedOnceOnly()
        {
            // Arrange:一栈 10 份,需要 3 份
            var rows = Rows(Row(4, Ordinal, 10));

            // Act
            BuildMaterialPlan plan = BuildMaterialPlanner.Plan(Owner, Ordinal, need: 3, rows);

            // Assert:同一实例**不重复点名**(否则 fold 会按两次扣)
            Assert.That(plan.Lines.Length, Is.EqualTo(1));
            Assert.That(Show(plan), Is.EqualTo("4:3"));
        }

        [Test]
        public void test_plan_rows_insufficient_returnsEmptyLines_noPartialConsume()
        {
            // Arrange:只有 3 份,需要 4 份
            var rows = Rows(Row(1, Ordinal, 2), Row(2, Ordinal, 1));

            // Act
            BuildMaterialPlan plan = BuildMaterialPlanner.Plan(Owner, Ordinal, need: 4, rows);

            // Assert:规则九 —— 不足则**整手拒绝**,不返回半份计划
            Assert.That(plan.IsComplete, Is.False);
            Assert.That(plan.Lines.Length, Is.EqualTo(0), "不足时 Lines 必须为空(fail-closed)");
            Assert.That(plan.RequiredQty, Is.EqualTo(4), "缺口消息要能说清「要多少」");
            Assert.That(plan.AvailableQty, Is.EqualTo(3), "缺口消息要能说清「只有多少」");
        }

        [Test]
        public void test_plan_rows_exactAvailable_succeeds()
        {
            // Arrange:恰等界(阈值边界 —— `available == need` 必须通过,不得写成 `available > need`)
            var rows = Rows(Row(1, Ordinal, 2), Row(2, Ordinal, 2));

            // Act
            BuildMaterialPlan plan = BuildMaterialPlanner.Plan(Owner, Ordinal, need: 4, rows);

            // Assert
            Assert.That(plan.IsComplete, Is.True);
            Assert.That(Show(plan), Is.EqualTo("1:2,2:2"));
        }

        [Test]
        public void test_plan_rows_otherOwner_notCounted()
        {
            // Arrange:自己只有 1 份,别人有 10 份
            var rows = Rows(Row(1, Ordinal, 1, Owner), Row(2, Ordinal, 10, OtherOwner));

            // Act
            BuildMaterialPlan plan = BuildMaterialPlanner.Plan(Owner, Ordinal, need: 2, rows);

            // Assert:归属过滤先于数量判定 —— 别人的材料不能替自己建
            Assert.That(plan.IsComplete, Is.False);
            Assert.That(plan.AvailableQty, Is.EqualTo(1));
            Assert.That(plan.Lines.Length, Is.EqualTo(0));
        }

        [Test]
        public void test_plan_rows_otherItemOrdinal_notCounted()
        {
            // Arrange:同 owner,但 ordinal 不同
            var rows = Rows(Row(1, Ordinal, 1), Row(2, OtherOrdinal, 10));

            // Act
            BuildMaterialPlan plan = BuildMaterialPlanner.Plan(Owner, Ordinal, need: 2, rows);

            // Assert
            Assert.That(plan.IsComplete, Is.False);
            Assert.That(plan.AvailableQty, Is.EqualTo(1));
        }

        [Test]
        public void test_plan_rows_zeroOrNegativeQty_notCounted()
        {
            // Arrange:归零行 / 负值行(畸形输入)不得进候选集
            var rows = Rows(Row(1, Ordinal, 0), Row(2, Ordinal, -3), Row(3, Ordinal, 2));

            // Act
            BuildMaterialPlan plan = BuildMaterialPlanner.Plan(Owner, Ordinal, need: 2, rows);

            // Assert
            Assert.That(plan.IsComplete, Is.True);
            Assert.That(Show(plan), Is.EqualTo("3:2"), "qty ≤ 0 的行既不计数也不被点名");
            Assert.That(plan.AvailableQty, Is.EqualTo(2));
        }

        [Test]
        public void test_plan_rows_needZeroOrNegative_isCompleteWithEmptyLines()
        {
            // Arrange:不需要材料的模块
            var rows = Rows(Row(1, Ordinal, 5));

            // Act
            BuildMaterialPlan zero = BuildMaterialPlanner.Plan(Owner, Ordinal, need: 0, rows);
            BuildMaterialPlan negative = BuildMaterialPlanner.Plan(Owner, Ordinal, need: -1, rows);

            // Assert:不扣料是**合法结论**,不是「没算出来」
            Assert.That(zero.IsComplete, Is.True);
            Assert.That(zero.Lines.Length, Is.EqualTo(0));
            Assert.That(negative.IsComplete, Is.True);
            Assert.That(negative.Lines.Length, Is.EqualTo(0));
        }

        [Test]
        public void test_plan_rows_unboundOwner_isAlwaysInsufficient()
        {
            // Arrange:归属未绑定(`BuildOwnerSlot.Unbound` = -1),材料却在 42 名下
            var rows = Rows(Row(1, Ordinal, 10));

            // Act
            BuildMaterialPlan plan = BuildMaterialPlanner.Plan(-1, Ordinal, need: 1, rows);

            // Assert:fail-closed —— 不知道扣谁的 ⇒ 拒绝,不静默归到别人头上
            Assert.That(plan.IsComplete, Is.False);
            Assert.That(plan.AvailableQty, Is.EqualTo(0));
        }

        [Test]
        public void test_plan_rows_nullRows_throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => BuildMaterialPlanner.Plan(Owner, Ordinal, 1, (IReadOnlyDictionary<long, InventoryRow>)null));
        }

        // ══════════════════════════════════════════════════════════════════
        // 投影重载(写前路径)—— 与行表重载**同一算法**
        // ══════════════════════════════════════════════════════════════════

        private SimEvent Ev<T>(EventKind kind, T payload, long tick, long seq = 0L) where T : struct
            => new SimEvent(tick, PatientId.None, seq, kind, _encoder.Encode(kind, payload));

        /// <summary>
        /// 经**真实 fold** 造一个 owner 名下的投影(id 与数量由调用方写死)。
        /// 不直接 `new InventoryProjection(...)` —— 那样就绕过了「投影来自事件流」这条语义,
        /// 而本重载的判据正是「同一份 fold 行表」。
        /// </summary>
        private InventoryProjection Projection(int owner, params (long id, int ordinal, int qty)[] stacks)
        {
            var events = new List<SimEvent>();
            long tick = 1;
            for (int i = 0; i < stacks.Length; i++)
            {
                (long id, int ordinal, int qty) = stacks[i];
                events.Add(Ev(EventKind.DropSpawned,
                    new DropSpawnedPayload(id, new WorldPos(1, 2, 3), ordinal, qty), tick, seq: 0));
                events.Add(Ev(EventKind.ResourceHarvested,
                    new ResourceHarvestedPayload(id, nodeId: 100 + i, gatherSeq: 1, qty: qty, outQuality: 1),
                    tick, seq: 1));
                events.Add(Ev(EventKind.DropClaimed,
                    new DropClaimedPayload(id, owner, tick), tick + 1, seq: 0));
                tick += 2;
            }

            return InventoryFold.Fold(events, _decoder, InventoryFoldContext.None)
                                .RequireComplete();
        }

        [Test]
        public void test_plan_projectionAndRowsOverloads_yieldIdenticalPlan()
        {
            // Arrange:同一批材料,两条入口各算一次
            InventoryProjection projection = Projection(Owner, (9L, Ordinal, 3), (2L, Ordinal, 3), (5L, Ordinal, 3));

            var rows = new Dictionary<long, InventoryRow>();
            foreach (long id in new long[] { 2L, 5L, 9L })
            {
                Assert.That(projection.TryGet(id, out InventoryRow row), Is.True,
                    $"夹具前置失败:fold 投影里没有实例 {id}");
                rows[id] = row;
            }

            // Act
            BuildMaterialPlan viaProjection = BuildMaterialPlanner.Plan(Owner, Ordinal, need: 4, projection);
            BuildMaterialPlan viaRows = BuildMaterialPlanner.Plan(Owner, Ordinal, need: 4, (IReadOnlyDictionary<long, InventoryRow>)rows);

            // Assert:两条路径必须逐字一致 —— 写前路径与 fold 重建路径分歧 =
            //         「主机验过 X 份、fold 扣了 Y 份」,而两边测试各自全绿。
            Assert.That(Show(viaProjection), Is.EqualTo("2:3,5:1"));
            Assert.That(Show(viaProjection), Is.EqualTo(Show(viaRows)));
            Assert.That(viaProjection.RequiredQty, Is.EqualTo(viaRows.RequiredQty));
            Assert.That(viaProjection.AvailableQty, Is.EqualTo(viaRows.AvailableQty));
        }

        [Test]
        public void test_plan_projectionNull_throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => BuildMaterialPlanner.Plan(Owner, Ordinal, 1, (InventoryProjection)null));
        }

        // ══════════════════════════════════════════════════════════════════
        // 按模块选料(便捷入口:cost / build_item_key 的唯一真源 = 目录)
        // ══════════════════════════════════════════════════════════════════

        private static TestModuleCatalog CatalogWith(int moduleId, int cost, int buildItemKey)
        {
            var catalog = new TestModuleCatalog();
            catalog.RegisterModule(new ModuleDefinition(
                moduleId: moduleId,
                slotType: SlotType.Decor,
                localOccupancy: new List<WorldPos> { new WorldPos(0, 0, 0) },
                validOrientations: new[] { 0 },
                cost: cost,
                buildItemKey: buildItemKey,
                refundable: 1));
            return catalog;
        }

        [Test]
        public void test_planforModule_moduleNotInCatalog_returnsModuleNotFound()
        {
            // Arrange:目录里只有 7,查 99
            TestModuleCatalog catalog = CatalogWith(7, cost: 4, buildItemKey: Ordinal);
            InventoryProjection projection = Projection(Owner, (1L, Ordinal, 10));

            // Act
            BuildMaterialPlanResult r = BuildMaterialPlanner.PlanForModule(
                99, Owner, projection, catalog, out BuildMaterialPlan plan);

            // Assert:取不到 cost(m) ⇒ 不可判通过(fail-closed),且计划为空
            Assert.That(r, Is.EqualTo(BuildMaterialPlanResult.ModuleNotFound));
            Assert.That(plan.Lines.Length, Is.EqualTo(0));
        }

        [Test]
        public void test_planforModule_usesCatalogCostAndBuildItemKey()
        {
            // Arrange:模块 7 = cost 4 / item_key 7;材料恰 4 份(跨两栈)
            TestModuleCatalog catalog = CatalogWith(7, cost: 4, buildItemKey: Ordinal);
            InventoryProjection projection = Projection(Owner, (1L, Ordinal, 1), (2L, Ordinal, 3));

            // Act
            BuildMaterialPlanResult r = BuildMaterialPlanner.PlanForModule(
                7, Owner, projection, catalog, out BuildMaterialPlan plan);

            // Assert
            Assert.That(r, Is.EqualTo(BuildMaterialPlanResult.Success));
            Assert.That(plan.RequiredQty, Is.EqualTo(4), "扣减量取自目录的 cost(m),不是调用方自报");
            Assert.That(Show(plan), Is.EqualTo("1:1,2:3"));
        }

        [Test]
        public void test_planforModule_insufficient_returnsNotEnoughMaterial()
        {
            // Arrange:cost 4,只有 3 份
            TestModuleCatalog catalog = CatalogWith(7, cost: 4, buildItemKey: Ordinal);
            InventoryProjection projection = Projection(Owner, (1L, Ordinal, 3));

            // Act
            BuildMaterialPlanResult r = BuildMaterialPlanner.PlanForModule(
                7, Owner, projection, catalog, out BuildMaterialPlan plan);

            // Assert
            Assert.That(r, Is.EqualTo(BuildMaterialPlanResult.NotEnoughMaterial));
            Assert.That(plan.Lines.Length, Is.EqualTo(0));
            Assert.That(plan.AvailableQty, Is.EqualTo(3));
        }

        [Test]
        public void test_planforModule_nullModules_throws()
        {
            InventoryProjection projection = Projection(Owner, (1L, Ordinal, 10));
            Assert.Throws<ArgumentNullException>(() => BuildMaterialPlanner.PlanForModule(
                7, Owner, projection, null, out _));
        }
    }

    /// <summary>
    /// `BuildStockQuery` —— `IStockQuery` 的生产实装(条件⑤ 的第三个调用点)。
    /// </summary>
    /// <remarks>
    /// 本 fixture 钉的是**一条语义**:`HasStock(moduleId)` 的判据是「够 `cost(m)` 份」,
    /// 不是「有 1 件」。若实现退化成「存在 ≥ 1 件」,`PlaceableChecker` 会在材料只够半手时
    /// 放行,把「不足」推给后面的选料器 —— 两处判据语义不一致。
    /// </remarks>
    [TestFixture]
    public class BuildStockQueryTest
    {
        private const int Owner = 42;
        private const int Ordinal = 7;
        private const int Module = 7;

        private PayloadEncoder _encoder;
        private PayloadDecoder _decoder;
        private TestModuleCatalog _catalog;
        private BuildOwnerSlot _slot;

        [SetUp]
        public void Setup()
        {
            var pool = new InMemoryBlobPool();
            _encoder = new PayloadEncoder(pool);
            _decoder = new PayloadDecoder(pool);

            _catalog = new TestModuleCatalog();
            _catalog.RegisterModule(new ModuleDefinition(
                moduleId: Module,
                slotType: SlotType.Decor,
                localOccupancy: new List<WorldPos> { new WorldPos(0, 0, 0) },
                validOrientations: new[] { 0 },
                cost: 4,
                buildItemKey: Ordinal,
                refundable: 1));

            _slot = new BuildOwnerSlot();
        }

        private SimEvent Ev<T>(EventKind kind, T payload, long tick, long seq = 0L) where T : struct
            => new SimEvent(tick, PatientId.None, seq, kind, _encoder.Encode(kind, payload));

        private InventoryProjection Projection(int owner, long id, int qty)
        {
            var events = new List<SimEvent>
            {
                Ev(EventKind.DropSpawned, new DropSpawnedPayload(id, new WorldPos(1, 2, 3), Ordinal, qty), 1, 0),
                Ev(EventKind.ResourceHarvested, new ResourceHarvestedPayload(id, 100, 1, qty, 1), 1, 1),
                Ev(EventKind.DropClaimed, new DropClaimedPayload(id, owner, 1), 2, 0),
            };
            return InventoryFold.Fold(events, _decoder, InventoryFoldContext.None)
                                .RequireComplete();
        }

        private BuildStockQuery Query(InventoryProjection projection)
            => new BuildStockQuery(() => projection, _catalog, _slot);

        [Test]
        public void test_hasStock_oneOfFour_isFalse()
        {
            // Arrange:有 1 件,但造一个要 4 件
            _slot.Bind(Owner);
            BuildStockQuery q = Query(Projection(Owner, 1L, qty: 1));

            // Act + Assert:判据是「够 cost(m)」,不是「有 ≥ 1 件」
            Assert.That(q.HasStock(Module), Is.False);
        }

        [Test]
        public void test_hasStock_exactlyCost_isTrue()
        {
            _slot.Bind(Owner);
            BuildStockQuery q = Query(Projection(Owner, 1L, qty: 4));

            Assert.That(q.HasStock(Module), Is.True);
        }

        [Test]
        public void test_hasStock_unboundOwner_isFalse()
        {
            // Arrange:材料在 42 名下,但归属槽未绑定
            BuildStockQuery q = Query(Projection(Owner, 1L, qty: 10));

            // Act + Assert:未绑定 ⇒ 恒不足(fail-closed),不得静默放行
            Assert.That(q.HasStock(Module), Is.False);
        }

        [Test]
        public void test_hasStock_moduleNotInCatalog_isFalse()
        {
            _slot.Bind(Owner);
            BuildStockQuery q = Query(Projection(Owner, 1L, qty: 10));

            Assert.That(q.HasStock(moduleId: 99), Is.False, "无成本真源 ⇒ 不可判通过");
        }

        [Test]
        public void test_hasStock_zeroCostModule_isTrue()
        {
            // Arrange:cost = 0 的模块(不需要材料)
            _catalog.RegisterModule(new ModuleDefinition(
                moduleId: 8,
                slotType: SlotType.Decor,
                localOccupancy: new List<WorldPos> { new WorldPos(0, 0, 0) },
                validOrientations: new[] { 0 },
                cost: 0,
                buildItemKey: Ordinal,
                refundable: 1));
            _slot.Bind(Owner);
            BuildStockQuery q = Query(Projection(Owner, 1L, qty: 1));

            Assert.That(q.HasStock(8), Is.True, "不扣料 ⇒ 造得出来(与 BuildMaterialPlan.IsComplete 同口径)");
        }
    }
}
