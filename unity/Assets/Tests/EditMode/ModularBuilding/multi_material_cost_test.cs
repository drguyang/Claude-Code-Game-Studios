// 多材料建造(schema 3)—— 帐篷五件组件的选料 / 缺口 / 真实产物(N5 实现轮 · 2026-11-14)
//
// 权威:
//   design/levels/prologue-beat-script.md §3.1(PA-01…PA-05 ⇒ 门板 9 / 钉子 10 / 房梁 11 /
//   布 12 / 油布 13,帐篷 `module_id = 10` 五件各 1)
//   docs/tech-debt-register.md `TD-building-07` 「丁」路径
//   ADR-014(build_modules schema 2→3)· ADR-005(整数域 / 流是唯一真源)
//
// ⚠️ 本件**分两半**:
//   ① **真实产物**(`DataCookedDisk.LoadBuildModules()`)—— 钉「帐篷真的是五件」;
//      这一半若用合成数据代替,表被改回单材料时它照样绿(自造断言顶替 AC 语义层,F1)。
//   ② **纯函数**(`BuildMaterialPlanner.PlanForCosts`)—— 钉逐件判定 / 整手拒绝 / 缺口点名。
//
// ⚠️ 零随机 / 零墙钟 / 零写盘;投影经**真实 fold** 造(不 `new InventoryProjection(...)`,
//    否则绕过「投影来自事件流」这条语义)。

using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Presentation;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.ModularBuilding
{
    /// <summary>帐篷五件组件的多材料成本判据。</summary>
    public class multi_material_cost_test
    {
        private const int Owner = 42;
        private const int TentModuleId = 10;

        /// <summary>五件组件的 ordinal(`item_database_items.json` 9..13)。</summary>
        private static readonly int[] Components = { 9, 10, 11, 12, 13 };

        private PayloadEncoder _encoder;
        private PayloadDecoder _decoder;

        [SetUp]
        public void SetUp()
        {
            var pool = new InMemoryBlobPool();
            _encoder = new PayloadEncoder(pool);
            _decoder = new PayloadDecoder(pool);
        }

        // ══════════════════ ① 真实产物 ══════════════════

        /// <summary>帐篷在真实 cooked 产物里 = **五件各 1**,且 `BuildItemKey` 退化为 -1 哨兵。</summary>
        [Test]
        public void test_realCatalog_tent_hasFiveDistinctComponents()
        {
            var catalog = new BuildModulesCatalog(DataCookedDisk.LoadBuildModules());

            ModuleDefinition tent = catalog.GetModuleDefinition(TentModuleId).Value;

            Assert.AreEqual(5, tent.Costs.Count, "帐篷的成本行集应为 5 行(五件组件各 1)");
            Assert.AreEqual(5, tent.Cost, "cost(m) 总量仍 = 5(Σ qty),返还公式读它");

            var seen = new HashSet<int>();
            for (int i = 0; i < tent.Costs.Count; i++)
            {
                Assert.AreEqual(1, tent.Costs[i].Qty, $"costs[{i}] 应为 1 件");
                Assert.That(System.Array.IndexOf(Components, tent.Costs[i].ItemKey), Is.GreaterThanOrEqualTo(0),
                    $"costs[{i}].item_key={tent.Costs[i].ItemKey} 不在五件组件 9..13 内");
                Assert.That(seen.Add(tent.Costs[i].ItemKey), Is.True,
                    "五件必须**互不相同** —— 重复即退回「同一种物品 ×5」");
            }

            Assert.AreEqual(-1, tent.BuildItemKey,
                "多材料模块的 BuildItemKey 必须是 -1 哨兵 —— 它不是真源,真源是 Costs");
        }

        /// <summary>其余模块仍是**单行**成本(单材料路径没有被这次改动带偏)。</summary>
        [Test]
        public void test_realCatalog_otherModules_staySingleLine()
        {
            var catalog = new BuildModulesCatalog(DataCookedDisk.LoadBuildModules());

            for (int id = 1; id <= 14; id++)
            {
                if (id == TentModuleId) continue;
                ModuleDefinition def = catalog.GetModuleDefinition(id).Value;
                Assert.AreEqual(1, def.Costs.Count, $"module_id={id} 应为单材料(1 行)");
                Assert.AreEqual(def.Cost, def.Costs[0].Qty, $"module_id={id} 的 qty 应等于 cost");
                Assert.AreEqual(def.Costs[0].ItemKey, def.BuildItemKey,
                    $"单材料时 BuildItemKey 应等于那一行的 key(module_id={id})");
            }
        }

        // ══════════════════ ② 纯函数 ══════════════════

        /// <summary>五件齐备 ⇒ 完整计划,每件各扣 1 份。</summary>
        [Test]
        public void test_planForCosts_allPresent_consumesOneOfEach()
        {
            InventoryProjection projection = Projection(OneOfEach());

            BuildMaterialPlan plan = BuildMaterialPlanner.PlanForCosts(Owner, TentCosts(), projection);

            Assert.IsTrue(plan.IsComplete, "五件齐备 ⇒ 计划应完整");
            Assert.AreEqual(5, plan.RequiredQty);
            Assert.AreEqual(5, plan.Lines.Length, "每件各扣 1 份 ⇒ 5 条 ConsumeLine");
            Assert.AreEqual(0, plan.MissingByItem.Length, "完整时不得填缺口");
        }

        /// <summary>缺一件 ⇒ **整手拒绝**(Lines 为空)+ 缺口**点名**是哪一件。</summary>
        [Test]
        public void test_planForCosts_missingOne_rejectsWholeHand_andNamesGap()
        {
            // 只给五件里的前四件(缺 13 = 油布)
            InventoryProjection projection = Projection(
                (1001L, 9, 1), (1002L, 10, 1), (1003L, 11, 1), (1004L, 12, 1));

            BuildMaterialPlan plan = BuildMaterialPlanner.PlanForCosts(Owner, TentCosts(), projection);

            Assert.IsFalse(plan.IsComplete, "缺一件 ⇒ 整手拒绝(规则九:不部分扣)");
            Assert.AreEqual(0, plan.Lines.Length, "不完整时 Lines 必须为空 —— 「扣一半」不存在");
            Assert.AreEqual(1, plan.MissingByItem.Length, "缺口应恰有 1 条(油布)");
            Assert.AreEqual(13, plan.MissingByItem[0].ItemKey, "缺口须点名 item_key=13");
            Assert.AreEqual(1, plan.MissingByItem[0].Qty, "缺 1 份");
        }

        /// <summary>某件数量不足 ⇒ 缺口数量是**该件自己的**差额,不是总量差。</summary>
        /// <remarks><b>判别力</b>:若缺口按 `Required − Available` 求和算(旧写法),五件各 1 而
        /// 只有 3 件时会给「缺 2」但**指不出是哪两件**;逐件算才能说「油布缺 1、布缺 1」。</remarks>
        [Test]
        public void test_planForCosts_twoShort_reportsPerItemGaps()
        {
            InventoryProjection projection = Projection(
                (1001L, 9, 1), (1002L, 10, 1), (1003L, 11, 1));

            BuildMaterialPlan plan = BuildMaterialPlanner.PlanForCosts(Owner, TentCosts(), projection);

            Assert.IsFalse(plan.IsComplete);
            Assert.AreEqual(2, plan.MissingByItem.Length, "缺两件 ⇒ 两条缺口");
            Assert.AreEqual(12, plan.MissingByItem[0].ItemKey);
            Assert.AreEqual(13, plan.MissingByItem[1].ItemKey);
            Assert.AreEqual(1, plan.MissingByItem[0].Qty);
            Assert.AreEqual(1, plan.MissingByItem[1].Qty);
        }

        /// <summary>单材料模块经 `PlanForCosts` 与旧 `Plan` **逐字同结论**(兼容路径不分叉)。</summary>
        [Test]
        public void test_planForCosts_singleLine_matchesLegacyPlan()
        {
            InventoryProjection projection = Projection((2001L, 8, 2), (2002L, 8, 5));

            BuildMaterialPlan legacy = BuildMaterialPlanner.Plan(Owner, itemOrdinal: 8, need: 4, projection);
            BuildMaterialPlan viaCosts = BuildMaterialPlanner.PlanForCosts(
                Owner, new[] { new ModuleCostLine(8, 4) }, projection);

            Assert.AreEqual(legacy.IsComplete, viaCosts.IsComplete);
            Assert.AreEqual(legacy.RequiredQty, viaCosts.RequiredQty);
            Assert.AreEqual(legacy.AvailableQty, viaCosts.AvailableQty);
            Assert.AreEqual(legacy.Lines.Length, viaCosts.Lines.Length);
        }

        // ── 夹具辅助 ──────────────────────────────────────────────────────

        private static ModuleCostLine[] TentCosts()
        {
            var costs = new ModuleCostLine[5];
            for (int i = 0; i < costs.Length; i++) costs[i] = new ModuleCostLine(Components[i], 1);
            return costs;
        }

        private static (long, int, int)[] OneOfEach()
        {
            var stacks = new (long, int, int)[5];
            for (int i = 0; i < stacks.Length; i++) stacks[i] = (1000L + i, Components[i], 1);
            return stacks;
        }

        /// <summary>经**真实 fold** 造投影(不直接 `new InventoryProjection`)。
        /// <para>⚠️ 每个实例都必须有一条**品级来源**,否则 fold 会以
        /// `InstanceWithoutQualitySource` 缺口整体拒绝(禁静默降级)—— 本件照
        /// `build_material_planner_test.Projection` 的同款三连:`DropSpawned` +
        /// `ResourceHarvested` + `DropClaimed`。</para></summary>
        private InventoryProjection Projection(params (long id, int ordinal, int qty)[] stacks)
        {
            var events = new List<SimEvent>();
            long tick = 1;
            for (int i = 0; i < stacks.Length; i++)
            {
                (long id, int ordinal, int qty) = stacks[i];
                events.Add(Event(EventKind.DropSpawned,
                    new DropSpawnedPayload(id, new WorldPos(1, 2, 3), ordinal, qty), tick));
                events.Add(Event(EventKind.ResourceHarvested,
                    new ResourceHarvestedPayload(id, nodeId: 100 + i, gatherSeq: 1, qty: qty, outQuality: 1),
                    tick));
                events.Add(Event(EventKind.DropClaimed, new DropClaimedPayload(id, Owner, tick), tick));
                tick += 1;
            }
            return InventoryFold.Fold(events, _decoder, InventoryFoldContext.None).RequireComplete();
        }

        private SimEvent Event<T>(EventKind kind, T payload, long tick) where T : struct
            => new SimEvent(tick, PatientId.None, 0L, kind, _encoder.Encode(kind, payload));
    }
}
