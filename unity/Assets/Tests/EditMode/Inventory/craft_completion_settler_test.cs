// `TD-processing-08` · 2026-11-03 —— 20 侧完成时点结算(起货溢出)的夹具
//
// 权威来源:
//   design/gdd/processing.md R-18-D[甲]②(:279-298)溢出物 = 完成 tick 当场本格 `DropSpawned`,
//     id **原样引用**点火已铸的 `output_instance_ids[]`(无第二铸造点);
//     规则八(:308)「完成 = 时间到达 + 出参已定」·「产出入库不另发事件」
//   design/gdd/inventory-and-items.md 规则六(原子单位 = 「炮制 = 一次结算」⇒ 整批拒绝同纪律)
//   `TD-processing-08` 的解除判据
//
// ─────────────────────────────────────────────────────────────────────────────
// 本文件断的两件事(缺一即「修了一半」)
// ─────────────────────────────────────────────────────────────────────────────
// ① **可见性**:`Craft` 的产出在 `AsOfTick < CompleteTick` 时**不在** `InventoryOf` 里
//    (修的是「点火即入包」)。
// ② **落地**:完成 tick 容量不足 ⇒ 本批**整批** `DropSpawned`,owner = NoOwner,
//    且 id 与点火已铸**逐位相同**(修的是「溢出路径没有宾语」)。
//
// ⚠️ 零随机 / 零时间依赖 / 零写盘;全部 id / 数值写死。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;
using DaYiJingCheng.Tests;                     // TestDropWriter
using DaYiJingCheng.Tests.DiseaseSimulation;     // FakeIdAuthority / FakePresenceQuery
using NUnit.Framework;

namespace DaYiJingCheng.Tests.EditMode.Inventory
{
    [TestFixture]
    public class CraftCompletionSettlerTest
    {
        // ── 夹具常量(与配方 / 物品表逐条对位)────────────────────────────
        private const int OrdWillowBark = 10;
        private const int OrdSalicylic = 12;
        private const int RecipeOrdinal = 7;
        private const int Actor = 7;
        private const long StartTick = 10L;
        private const long DurationTicks = 10L;      // ⇒ CompleteTick = 20
        private const long CompleteTick = StartTick + DurationTicks;
        private const long OutputId = 500L;
        private const int OutputQty = 3;
        private const int SalicylicWeight = 2;       // ⇒ 本批总重 = 2 × 3 = 6

        private InMemoryBlobPool _pool;
        private PayloadEncoder _encoder;
        private PayloadDecoder _decoder;

        [SetUp]
        public void Setup()
        {
            _pool = new InMemoryBlobPool();
            _encoder = new PayloadEncoder(_pool);
            _decoder = new PayloadDecoder(_pool);
        }

        // ── 表 ────────────────────────────────────────────────────────────

        private static ItemDef Def(int ordinal, string baseId, ProcessingState st, int weight)
            => new ItemDef
            {
                Ordinal = ordinal,
                BaseId = baseId,
                ProcessingState = st,
                DisplayName = baseId,
                Category = ItemCategory.Material,
                StackMax = 99,
                Weight = weight,
                Deprecated = false,
                LegalTransitions = Array.Empty<string>(),
            };

        private static ItemDataSet Items() => new ItemDataSet
        {
            Items = new[]
            {
                Def(OrdWillowBark, "willow_bark", ProcessingState.Dried, 1),
                Def(OrdSalicylic, "salicylic_acid", ProcessingState.Extracted, SalicylicWeight),
            },
            ConfigVersion = 1u,
        };

        private static RecipeDataSet Recipes() => new RecipeDataSet
        {
            Recipes = new[]
            {
                new Recipe
                {
                    Ordinal = RecipeOrdinal,
                    RecipeId = "extract_salicylic",
                    Owner = RecipeOwner.Process,
                    Inputs = new[] { new RecipeEntry(new ItemKey("willow_bark", ProcessingState.Dried), 1) },
                    Outputs = new[] { new RecipeEntry(new ItemKey("salicylic_acid", ProcessingState.Extracted), 1) },
                    DurationTicks = (int)DurationTicks,
                },
            },
        };

        private static InventoryFoldContext Ctx(long asOfTick, int? solePlayer = null)
            => new InventoryFoldContext(Items(), Recipes(), null, solePlayer, asOfTick);

        // ── 事件 ──────────────────────────────────────────────────────────

        /// <summary>一炉:点火 <c>StartTick</c>、时长 <c>DurationTicks</c>、
        /// 产出 1 行 × <paramref name="qty"/>、id = <see cref="OutputId"/>。</summary>
        private SimEvent CraftEvent(int qty = OutputQty)
            => new SimEvent(StartTick, PatientId.None, -1L, EventKind.Craft,
                _encoder.Encode(EventKind.Craft, new CraftPayload(
                    Actor, RecipeOrdinal, StartTick, DurationTicks,
                    new long[] { 1 }, new[] { 1 },
                    new[] { qty }, new[] { 3 }, new long[] { OutputId },
                    new WorldPos(4, 0, 7))));

        // ══════════════════════════════════════════════════════════════════
        // ① 可见性 —— 完成 tick 之前产出不在 InventoryOf 里
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_td08_foldHidesOutputsBeforeCompleteTick()
        {
            var stream = new EventStream(new FakeIdAuthority(), new FakePresenceQuery());
            AppendCraftWithInput(stream);

            InventoryFoldResult r = InventoryFold.Fold(stream.Events, _decoder, Ctx(CompleteTick - 1));

            Assert.That(r.Projection.TryGet(OutputId, out _), Is.False,
                "完成前 1 tick:产出**不得**出现在投影里(修「点火即入包」)");
            Assert.That(r.IsComplete, Is.True,
                "未到完成 tick 时不该报缺口 —— 切点门在查表**之前**返回(否则会误报配方缺项);" +
                "实际缺口 = " + string.Join(" | ", GapTexts(r)));
        }

        [Test]
        public void test_td08_foldShowsOutputsAtCompleteTick()
        {
            var stream = new EventStream(new FakeIdAuthority(), new FakePresenceQuery());
            AppendCraftWithInput(stream);

            InventoryFoldResult r = InventoryFold.Fold(stream.Events, _decoder, Ctx(CompleteTick));

            Assert.That(r.Projection.TryGet(OutputId, out InventoryRow row), Is.True,
                "完成当拍(等号)产出必须可见 —— 与 AC-18-21 的「当拍」同口径");
            Assert.That(row.Owner, Is.EqualTo(Actor), "装得下时归炮制者");
            Assert.That(row.Qty, Is.EqualTo(OutputQty));
        }

        // ══════════════════════════════════════════════════════════════════
        // ② 落地 —— 容量不足 ⇒ 整批 DropSpawned,id 原样引用
        // ══════════════════════════════════════════════════════════════════

        private CraftCompletionSettler Settler(EventStream stream, int cap,
                                               Func<InventoryProjection> projectionOf = null)
        {
            DropEventWriter writer = TestDropWriter.Create(stream, _pool);
            return new CraftCompletionSettler(
                stream.Events, _decoder, writer,
                projectionOf ?? (() => InventoryFold.Fold(stream.Events, _decoder, Ctx(CompleteTick))
                                          .Projection),
                new CarryCapacity(cap), Items(), Recipes());
        }

        [Test]
        public void test_td08_capacityInsufficient_emitsWholeBatchWithIgnitionIds()
        {
            var stream = new EventStream(new FakeIdAuthority(), new FakePresenceQuery());
            AppendCraftWithInput(stream);

            // 本批总重 = 2 × 3 = 6 > cap 5 ⇒ 装不下。
            CraftCompletionSettler settler = Settler(stream, cap: 5);
            int emitted = settler.OnTickEdge(CompleteTick);

            Assert.That(emitted, Is.EqualTo(1), "整批落地 = 1 条 DropSpawned(本炉只有 1 行产出)");
            Assert.That(settler.EmittedDropCount, Is.EqualTo(1));

            var spawned = DropsIn(stream);
            Assert.That(spawned.Count, Is.EqualTo(1));
            Assert.That(spawned[0].InstanceId, Is.EqualTo(OutputId),
                "**无第二铸造点**:落地 id 必须逐位等于点火已铸的那个");
            Assert.That(spawned[0].Qty, Is.EqualTo(OutputQty));
            Assert.That(spawned[0].ItemKey, Is.EqualTo(OrdSalicylic), "item_key 经配方表折回 ordinal");
            Assert.That(spawned[0].SpawnAnchor.Equals(new WorldPos(4, 0, 7)), Is.True,
                "锚点 = `Craft.ToolCell`(R-18-D:本格落地)");
        }

        [Test]
        public void test_td08_capacitySufficient_emitsNothing()
        {
            var stream = new EventStream(new FakeIdAuthority(), new FakePresenceQuery());
            AppendCraftWithInput(stream);

            CraftCompletionSettler settler = Settler(stream, cap: 100);
            Assert.That(settler.OnTickEdge(CompleteTick), Is.EqualTo(0), "装得下 ⇒ 零 DropSpawned");
            Assert.That(DropsIn(stream), Is.Empty);

            InventoryFoldResult r = InventoryFold.Fold(stream.Events, _decoder, Ctx(CompleteTick));
            Assert.That(r.Projection.TryGet(OutputId, out InventoryRow row), Is.True);
            Assert.That(row.Owner, Is.EqualTo(Actor), "留在背包");
        }

        [Test]
        public void test_td08_capacityCountsExistingLoad()
        {
            // 玩家身上已有 4 重量的东西(cap 5)⇒ 本批 6 装不下;空背包时(同 cap)也装不下,
            // 故本测改用 cap 8:空背包 6 ≤ 8 装得下,带 4 ⇒ 10 > 8 装不下。
            var stream = new EventStream(new FakeIdAuthority(), new FakePresenceQuery());
            AppendCraftWithInput(stream);

            InventoryProjection WithExistingLoad() => InventoryFold
                .Fold(stream.Events, _decoder, Ctx(CompleteTick)).Projection;

            CraftCompletionSettler empty = Settler(stream, cap: 8, projectionOf: WithExistingLoad);
            Assert.That(empty.OnTickEdge(CompleteTick), Is.EqualTo(0),
                "空背包:本批 6 ≤ cap 8 ⇒ 不落地");

            var loaded = new EventStream(new FakeIdAuthority(), new FakePresenceQuery());
            AppendCraftWithInput(loaded, alsoOwnedId: 900L, alsoOwnedQty: 4);

            CraftCompletionSettler busy = Settler(loaded, cap: 8,
                () => InventoryFold.Fold(loaded.Events, _decoder, Ctx(CompleteTick)).Projection);
            Assert.That(busy.OnTickEdge(CompleteTick), Is.EqualTo(1),
                "已有 4 + 本批 6 = 10 > cap 8 ⇒ 必须落地(容量裁决用的是**入库前**载重)");
        }

        [Test]
        public void test_td08_afterDrop_foldPutsBatchOnGround_noGaps()
        {
            var stream = new EventStream(new FakeIdAuthority(), new FakePresenceQuery());
            AppendCraftWithInput(stream);

            Settler(stream, cap: 5).OnTickEdge(CompleteTick);

            InventoryFoldResult r = InventoryFold.Fold(stream.Events, _decoder, Ctx(CompleteTick));
            Assert.That(r.IsComplete, Is.True,
                "落地后投影必须**无缺口** —— Craft 不建持有行、DropSpawned 建地面行,不是重复出生;" +
                "实际缺口 = " + string.Join(" | ", GapTexts(r)));
            Assert.That(r.Projection.TryGet(OutputId, out InventoryRow row), Is.True);
            Assert.That(row.Owner, Is.EqualTo(InventoryProjection.NoOwner),
                "整批在地上(规则六:不做「一半入包一半落地」)");
            Assert.That(r.Projection.InstancesOf(Actor), Is.Empty, "炮制者名下不得同时有这批");
        }

        [Test]
        public void test_td08_alreadyDropped_noSecondDrop()
        {
            var stream = new EventStream(new FakeIdAuthority(), new FakePresenceQuery());
            AppendCraftWithInput(stream);
            Settler(stream, cap: 5).OnTickEdge(CompleteTick);
            int afterFirst = DropsIn(stream).Count;

            // 「读档重放 / 同一 tick 段重建」:新结算器在同一份流上跑到同一个完成 tick。
            CraftCompletionSettler replay = Settler(stream, cap: 5);
            Assert.That(replay.OnTickEdge(CompleteTick), Is.EqualTo(0),
                "已经落地过 ⇒ 幂等跳过(否则每次重建都多一条 DropSpawned)");
            Assert.That(DropsIn(stream).Count, Is.EqualTo(afterFirst));
        }

        [Test]
        public void test_td08_nonIncreasingTick_throws()
        {
            var stream = new EventStream(new FakeIdAuthority(), new FakePresenceQuery());
            AppendCraftWithInput(stream);
            CraftCompletionSettler settler = Settler(stream, cap: 5);

            settler.OnTickEdge(CompleteTick);
            Assert.Throws<InvalidOperationException>(() => settler.OnTickEdge(CompleteTick),
                "同一 tick 重入必须 fail-closed(静默再判一次 = 重复落地风险)");
            Assert.Throws<InvalidOperationException>(() => settler.OnTickEdge(CompleteTick - 1),
                "tick 回退同样 fail-closed");
        }

        // ══════════════════════════════════════════════════════════════════
        // 辅助
        // ══════════════════════════════════════════════════════════════════

        /// <summary>把一炉**完整合法**地放进流:先喂原料实例(被点名 ⇒ 归属炮制者),
        /// 再发 `Craft`(其 `ActualConsumed` 扣掉它)。
        /// <para>⚠️ 不喂原料会让 fold 报 `DanglingInstanceReference`(扣料点名了一个不存在的实例)——
        /// 那是**夹具**的缺陷,不是切点门的缺陷;本文件要断的是产出侧,故原料必须实存。</para></summary>
        private void AppendCraftWithInput(EventStream stream, int qty = OutputQty,
                                          long? alsoOwnedId = null, int alsoOwnedQty = 0)
        {
            AppendOwned(stream, tick: 1L, instanceId: 1L, itemKey: OrdWillowBark, qty: 1, owner: Actor);
            if (alsoOwnedId.HasValue)
                AppendOwned(stream, tick: 3L, instanceId: alsoOwnedId.Value,
                            itemKey: OrdWillowBark, qty: alsoOwnedQty, owner: Actor);
            stream.Append(CraftEvent(qty));
        }

        /// <summary>两条事件把 <paramref name="instanceId"/> 落到 <paramref name="owner"/> 名下。</summary>
        private void AppendOwned(EventStream stream, long tick, long instanceId,
                                 int itemKey, int qty, int owner)
        {
            stream.Append(new SimEvent(tick, PatientId.None, -1L, EventKind.DropSpawned,
                _encoder.Encode(EventKind.DropSpawned,
                    new DropSpawnedPayload(instanceId, new WorldPos(0, 0, 0), itemKey, qty))));
            stream.Append(new SimEvent(tick + 1L, PatientId.None, -1L, EventKind.DropClaimed,
                _encoder.Encode(EventKind.DropClaimed,
                    new DropClaimedPayload(instanceId, owner, tick + 1L))));
        }

        private static IEnumerable<string> GapTexts(InventoryFoldResult r)
        {
            foreach (InventoryFoldGap g in r.Gaps) yield return g.Kind + "@" + g.At.Tick;
        }

        /// <summary>流里**本炉产出**的落地点(按流序)。
        /// <para>⚠️ 刻意**排除**夹具自己喂的原料 / 已有实例 —— 那些也是 `DropSpawned`。
        /// 只按 `InstanceId == OutputId` 取,才不会把夹具的种子当成被测行为。</para></summary>
        private List<DropSpawnedPayload> DropsIn(EventStream stream)
        {
            var list = new List<DropSpawnedPayload>();
            foreach (SimEvent e in stream.Events)
            {
                if (e.Kind != EventKind.DropSpawned) continue;
                DropSpawnedPayload p = _decoder.Decode<DropSpawnedPayload>(e.Kind, e.Payload);
                if (p.InstanceId == OutputId) list.Add(p);
            }
            return list;
        }
    }
}
