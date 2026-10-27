// S20-10 · inventory story-001 —— `InventoryOf` 的世界流折叠重建(本体用例)
//
// 权威来源:
//   design/gdd/inventory-and-items.md 规则一 / 二 / 三 / 五 · §涟漪登记 R15–R18
//   ADR-009 §一 / §二(三态分类 · 流优先级)· ADR-005(唯一真源)· ADR-031(读载荷唯一路径)
//   story production/epics/inventory-items/story-001-inventoryof-fold-rebuild.md
//     AC-20-03 [A] · AC-20-16 [I] · AC-20-05 [A] · AC-20-17 [A]
//
// ⚠️ **AC 记分(禁把「本体跑通」读成「四条 AC 全绿」)**:
//     ✅ AC-20-05  · 20 侧零结算求值(源面 + IL 面双判)
//     ✅ AC-20-17  · 零 `ItemConsumed` / `ItemTransferred` 追加 Kind(枚举面 + 七谓词面)
//     ⚠️ AC-20-03  · **不勾** —— 重建机制已验(全序 / 幂等 / 逐位等 / 乱序无关),但前置未闭合:
//                   旧五件(R2 · R3 · R10 · R11 · OQ-20-5 —— 其中 R10 / R11 / OQ-20-5 已闭)
//                   + 本实现轮新登记四件(R15 / R16 / R17 / R18)。`Craft` 产出 / 建造扣 /
//                   建造返还 / 非 17 来源品级 **四个面结构性不可重建**。
//     ⚠️ AC-20-16  · **不勾** —— 用药消耗的**写者**在 20 侧尚未实现(`DropDespawned` 无生产端);
//                   本件只证「fold 侧经 `DropDespawned` 正确消化消耗」,不证「用药会发它」。
//
// ⚠️ **两个必须读清的近邻风险**:
//   ① 「`Drop*` 三支夹具跑通」≠「17 采集端到端跑通」—— 夹具**直接构造**世界流事件,
//      不经过 `HarvestOrchestrator`(其 `IDropWriter` 生产实现至今为 `NullDropWriter`)。
//   ② 「缺载体 ⇒ 报缺口」是**正向判据**,不是「暂时跳过」。R15–R18 闭合后这些断言**会转红**
//      —— 那是**期待**,届时改成逐位等。
//
// ⚠️ 零随机 / 零时间依赖 / 零写盘;全部 id / tick / seq 写死。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DaYiJingCheng.EditorTools.Gates;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
// ⚠️ 用别名而非 `using DaYiJingCheng.Sim.World` —— 本文件同时引 Mono.Cecil
//    (AC-20-05 的 IL 面判据),它也有一个 `ModuleDefinition`,裸名会 CS0104 撞车。
using SimModuleDefinition = DaYiJingCheng.Sim.World.ModuleDefinition;
using SimModuleCatalog = DaYiJingCheng.Sim.World.IModuleCatalog;
using SlotType = DaYiJingCheng.Sim.World.SlotType;
using Mono.Cecil;
using NUnit.Framework;
using UnityEditor;

namespace DaYiJingCheng.Tests.EditMode.Inventory
{
    /// <summary><see cref="InventoryFold"/> —— 七类 Kind 的纯函数转移与缺口判据。</summary>
    [TestFixture]
    public sealed class InventoryFoldTest
    {
        private PayloadEncoder _encoder;
        private PayloadDecoder _decoder;

        [SetUp]
        public void Setup()
        {
            // 真编码器 + 真池 + 真解码器 —— 不 mock 载荷层:
            // 一旦载荷 struct 与 codec 分派脱节,这里就红(而不是被 Fake 掩盖)。
            var pool = new InMemoryBlobPool();
            _encoder = new PayloadEncoder(pool);
            _decoder = new PayloadDecoder(pool);
        }

        // ══════════════════════════════════════════════════════════════════
        // 夹具
        // ══════════════════════════════════════════════════════════════════

        private SimEvent Ev<T>(EventKind kind, T payload, long tick, long seq = 0L) where T : struct
            => new SimEvent(tick, PatientId.None, seq, kind, _encoder.Encode(kind, payload));

        private SimEvent Spawn(long id, int itemKey, int qty, long tick, long seq = 0L)
            => Ev(EventKind.DropSpawned, new DropSpawnedPayload(id, new WorldPos(1, 2, 3), itemKey, qty), tick, seq);

        private SimEvent Claim(long id, int claimer, long tick, long seq = 0L)
            => Ev(EventKind.DropClaimed, new DropClaimedPayload(id, claimer, tick), tick, seq);

        private SimEvent Despawn(long id, int qty, long tick, long seq = 0L)
            => Ev(EventKind.DropDespawned, new DropDespawnedPayload(id, qty, 0), tick, seq);

        private SimEvent Harvest(long id, int nodeId, int qty, int outQuality, long tick, long seq = 0L)
            => Ev(EventKind.ResourceHarvested,
                  new ResourceHarvestedPayload(id, nodeId, 1, qty, outQuality), tick, seq);

        private SimEvent Craft(int actor, long[] inputIds, int[] consumed,
            int[] outQty, int[] outQuality, long[] outIds, long tick, long seq = 0L)
            => Ev(EventKind.Craft,
                  new CraftPayload(actor, 7, tick, 10L,
                      inputIds, consumed, outQty, outQuality, outIds, new WorldPos(0, 0, 0)),
                  tick, seq);

        private static bool TryRow(InventoryFoldResult r, long id, out InventoryRow row)
            => r.Projection.TryGet(id, out row);

        // ══════════════════════════════════════════════════════════════════
        // 查表源夹具(2026-10-27 R15–R18 闭合批)
        // ══════════════════════════════════════════════════════════════════
        //
        // ⚠️ **不用 `InventoryFoldContext.None` 蒙混**:本文件里有 Craft 产出 / StructurePlaced
        //    的夹具,用 None 会掩盖「查表链真的走通了」这件事。真表夹具写死最小行集,
        //    与 `inventory_load_service_test.cs:78 MakeItems()` 同款(自造行,不读烘焙产物)。

        // 物品表 ordinal 常量 —— 与夹具里 `Spawn(id, itemKey: N, …)` 的 N 对应。
        private const int OrdWillowBark = 10;      // material
        private const int OrdDriedBark = 11;       // material
        private const int OrdSalicylic = 12;       // drug
        private const int OrdWoodenPost = 20;      // **build_part**(R18 桶③ / R16 扣料)
        private const int OrdSplint = 21;          // tool

        private static ItemDef MakeDef(int ordinal, string baseId, ProcessingState st,
            ItemCategory cat, int stackMax = 99)
            => new ItemDef
            {
                Ordinal = ordinal,
                BaseId = baseId,
                ProcessingState = st,
                DisplayName = baseId,
                Category = cat,
                StackMax = stackMax,
                Weight = 1,
                Deprecated = false,
                LegalTransitions = Array.Empty<string>(),
            };

        private static ItemDataSet MakeItems() => new ItemDataSet
        {
            Items = new[]
            {
                MakeDef(OrdWillowBark, "willow_bark", ProcessingState.Raw, ItemCategory.Material),
                MakeDef(OrdDriedBark, "willow_bark", ProcessingState.Dried, ItemCategory.Material),
                MakeDef(OrdSalicylic, "salicylic_acid", ProcessingState.Extracted, ItemCategory.Drug),
                MakeDef(OrdWoodenPost, "wooden_post", ProcessingState.Raw, ItemCategory.BuildPart),
                MakeDef(OrdSplint, "wooden_splint", ProcessingState.Raw, ItemCategory.Tool),
            },
            ConfigVersion = 1u,
        };

        /// <summary>配方 <c>ordinal = 7</c>(夹具的 <see cref="Craft"/> 恒用它):
        /// 输入 dried bark,输出 1 行 salicylic_acid。<c>outputs</c> 恒 1 行 ⇒ 与
        /// <c>outQty.Length == 1</c> 的夹具对位。</summary>
        private static RecipeDataSet MakeRecipes() => new RecipeDataSet
        {
            Recipes = new[]
            {
                new Recipe
                {
                    Ordinal = 7,
                    RecipeId = "extract_salicylic",
                    Owner = RecipeOwner.Process,
                    Inputs = new[] { new RecipeEntry(new ItemKey("willow_bark", ProcessingState.Dried), 2) },
                    Outputs = new[] { new RecipeEntry(new ItemKey("salicylic_acid", ProcessingState.Extracted), 1) },
                    DurationTicks = 10,
                    SkillGate = 0,
                    MinQuality = 1,
                    BoundaryState = Array.Empty<ProcessingTransition>(),
                },
            },
            ConfigVersion = 1u,
        };

        /// <summary>模块目录桩:moduleId 3 ⇒ cost 2、build_item_key = wooden_post(build_part)。</summary>
        private sealed class FoldModuleCatalog : SimModuleCatalog
        {
            private readonly Dictionary<int, SimModuleDefinition> _defs =
                new Dictionary<int, SimModuleDefinition>();

            public FoldModuleCatalog(int moduleId, int cost, int buildItemKey)
                => _defs[moduleId] = new SimModuleDefinition(moduleId, SlotType.Decor,
                    new[] { new WorldPos(0, 0, 0) }, new[] { 0, 90, 180, 270 },
                    cost: cost, buildItemKey: buildItemKey);

            public SimModuleDefinition? GetModuleDefinition(int moduleId)
                => _defs.TryGetValue(moduleId, out SimModuleDefinition d)
                    ? d : (SimModuleDefinition?)null;

            public bool ContainsModule(int moduleId) => _defs.ContainsKey(moduleId);
            public IReadOnlyCollection<int> GetAllModuleIds() => _defs.Keys;
        }

        /// <summary>真表上下文(P0 单机:唯一玩家 = <paramref name="solePlayer"/>)。
        /// 模块 3 = cost 2 × wooden_post。</summary>
        private static InventoryFoldContext Ctx(int? solePlayer = null)
            => new InventoryFoldContext(MakeItems(), MakeRecipes(),
                new FoldModuleCatalog(moduleId: 3, cost: 2, buildItemKey: OrdWoodenPost),
                solePlayer);

        /// <summary>三份表全空 —— 纯 <c>Drop*</c> 夹具用它,是一条**可 grep 的声明**。</summary>
        private static readonly InventoryFoldContext EmptyCtx = InventoryFoldContext.None;

        // ══════════════════════════════════════════════════════════════════
        // AC-20-03 —— 重建机制本体(可重建子域逐位等 / 幂等 / 乱序无关 / 空流)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_ac2003_dropLifecycle_foldsOwnerAndQty()
        {
            var events = new List<SimEvent>
            {
                Spawn(1, 10, 5, tick: 1, seq: 0),
                Harvest(1, nodeId: 99, qty: 5, outQuality: 3, tick: 1, seq: 1),
                Claim(1, claimer: 7, tick: 2, seq: 0),
                Despawn(1, qty: 2, tick: 3, seq: 0),
            };

            InventoryFoldResult r = InventoryFold.Fold(events, _decoder, EmptyCtx);

            Assert.That(TryRow(r, 1, out InventoryRow row), Is.True);
            Assert.That(row.ItemOrdinal, Is.EqualTo(10), "item_key = 载荷原值(不解析、不求值)");
            Assert.That(row.Quality, Is.EqualTo(3), "品级从 ResourceHarvested.out_quality 抄入");
            Assert.That(row.Qty, Is.EqualTo(3), "5 - 2");
            Assert.That(row.Owner, Is.EqualTo(7), "认领后 owner = claimer");
            Assert.That(r.Projection.InstancesOf(7), Is.EquivalentTo(new long[] { 1L }));
            Assert.That(r.IsComplete, Is.True, "本流全在可重建子域内");
        }

        [Test]
        public void test_ac2003_qualityArrivesAfterSpawn_stillBound()
        {
            // 17 实发序 = DropSpawned → ResourceHarvested ⇒ 品级**晚于**出生。
            // 若 fold 只在出生时查品级,这一条会得到 quality=0。
            var events = new List<SimEvent>
            {
                Spawn(42, itemKey: 5, qty: 1, tick: 10, seq: 0),
                Harvest(42, nodeId: 3, qty: 1, outQuality: 2, tick: 10, seq: 1),
            };

            InventoryFoldResult r = InventoryFold.Fold(events, _decoder, EmptyCtx);

            Assert.That(TryRow(r, 42, out InventoryRow row), Is.True);
            Assert.That(row.Quality, Is.EqualTo(2),
                "品级须与先后无关(预扫一遍)—— 顺序依赖会让 17 的真实发序恒得 0");
        }

        [Test]
        public void test_ac2003_unclaimedInstance_hasNoOwnerSentinel()
        {
            var events = new List<SimEvent>
            {
                Spawn(8, itemKey: 1, qty: 1, tick: 1, seq: 0),
                Harvest(8, nodeId: 1, qty: 1, outQuality: 0, tick: 1, seq: 1),
            };

            InventoryFoldResult r = InventoryFold.Fold(events, _decoder, EmptyCtx);

            Assert.That(TryRow(r, 8, out InventoryRow row), Is.True);
            Assert.That(row.Owner, Is.EqualTo(InventoryProjection.NoOwner),
                "未认领 ⇒ NoOwner 哨兵,不是 0(0 是合法 player_id)");
            Assert.That(r.Projection.InstancesOf(InventoryProjection.NoOwner),
                Is.EquivalentTo(new long[] { 8L }));
        }

        [Test]
        public void test_ac2003_despawnToZero_removesFromProjection()
        {
            var events = new List<SimEvent>
            {
                Spawn(9, itemKey: 1, qty: 2, tick: 1, seq: 0),
                Harvest(9, nodeId: 1, qty: 2, outQuality: 1, tick: 1, seq: 1),
                Despawn(9, qty: 2, tick: 2, seq: 0),
            };

            InventoryFoldResult r = InventoryFold.Fold(events, _decoder, EmptyCtx);

            Assert.That(TryRow(r, 9, out _), Is.False, "归零 ⇒ 出投影");
            Assert.That(r.Projection.InstanceCount, Is.EqualTo(0));
            Assert.That(r.Projection.CanonicalForm(), Is.Empty);
        }

        [Test]
        public void test_ac2003_multiPlayerMultiInstance_ownerViewIsPerPlayer()
        {
            var events = new List<SimEvent>
            {
                Spawn(1, 10, 1, tick: 1, seq: 0), Harvest(1, 1, 1, 0, tick: 1, seq: 1), Claim(1, 7, tick: 2, seq: 0),
                Spawn(2, 10, 1, tick: 1, seq: 2), Harvest(2, 1, 1, 0, tick: 1, seq: 3), Claim(2, 8, tick: 2, seq: 1),
                Spawn(3, 11, 1, tick: 1, seq: 4), Harvest(3, 2, 1, 0, tick: 1, seq: 5), Claim(3, 7, tick: 3, seq: 0),
            };

            InventoryFoldResult r = InventoryFold.Fold(events, _decoder, EmptyCtx);

            Assert.That(r.Projection.InstancesOf(7), Is.EqualTo(new long[] { 1L, 3L }),
                "按 instance_id 升序(稳定序,换页 / 摆放序读这一序)");
            Assert.That(r.Projection.InstancesOf(8), Is.EqualTo(new long[] { 2L }));
            Assert.That(r.Projection.InstancesOf(99), Is.Empty, "无实例的玩家 ⇒ 空数组,非 null");
        }

        [Test]
        public void test_ac2003_shuffledInput_yieldsIdenticalProjection()
        {
            // 乱序输入 ⇒ fold 内部按全序键重排 ⇒ 同结果(排序不是调用方义务)。
            var ordered = new List<SimEvent>
            {
                Spawn(1, 10, 5, tick: 1, seq: 0),
                Harvest(1, 1, 5, 2, tick: 1, seq: 1),
                Claim(1, 7, tick: 2, seq: 0),
                Despawn(1, qty: 1, tick: 3, seq: 0),
                Spawn(2, 10, 3, tick: 1, seq: 2),
                Harvest(2, 1, 3, 1, tick: 1, seq: 3),
                Claim(2, 8, tick: 2, seq: 1),
            };
            var shuffled = new List<SimEvent>(ordered);
            shuffled.Reverse();
            // 再来一次交错扰动(避免「反向也恰好有序」的巧合)
            SimEvent tmp = shuffled[1]; shuffled[1] = shuffled[5]; shuffled[5] = tmp;

            string a = InventoryFold.Fold(ordered, _decoder, EmptyCtx).Projection.CanonicalForm();
            string b = InventoryFold.Fold(shuffled, _decoder, EmptyCtx).Projection.CanonicalForm();

            Assert.That(a, Is.Not.Empty, "投影非空 —— 否则「两者都空」会伪装成通过");
            Assert.That(b, Is.EqualTo(a), "乱序输入须得同一投影");
        }

        [Test]
        public void test_ac2003_sameStreamTwice_isIdempotent()
        {
            var events = new List<SimEvent>
            {
                Spawn(1, 10, 2, tick: 1, seq: 0),
                Harvest(1, 1, 2, 4, tick: 1, seq: 1),
                Claim(1, 7, tick: 2, seq: 0),
            };

            InventoryFoldResult r1 = InventoryFold.Fold(events, _decoder, EmptyCtx);
            InventoryFoldResult r2 = InventoryFold.Fold(events, _decoder, EmptyCtx);

            Assert.That(r2.Projection.CanonicalForm(), Is.EqualTo(r1.Projection.CanonicalForm()));
            Assert.That(r1.IsComplete, Is.True);
            Assert.That(r1.Gaps, Is.Empty);
        }

        [Test]
        public void test_ac2003_emptyStream_emptyProjectionNotNull()
        {
            InventoryFoldResult r = InventoryFold.Fold(Array.Empty<SimEvent>(), _decoder, EmptyCtx);

            Assert.That(r.Projection, Is.Not.Null);
            Assert.That(r.Projection.InstanceCount, Is.EqualTo(0));
            Assert.That(r.IsComplete, Is.True);
        }

        // ══════════════════════════════════════════════════════════════════
        // 单流域纪律 —— 病史 / 病例流条目不改变投影(OQ-20-1 ②)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_ac2016_drugTreatmentAppliedInHistoryStream_doesNotTouchProjection()
        {
            // 病史流的处置事件**不是**库存载体。它进场而世界流无 DropDespawned
            // ⇒ 实例仍在(这证明载体是 DropDespawned,不是处置事件)。
            var events = new List<SimEvent>
            {
                Spawn(1, 10, 1, tick: 1, seq: 0),
                Harvest(1, 1, 1, 0, tick: 1, seq: 1),
                Claim(1, 7, tick: 2, seq: 0),
                DrugTreatment(tick: 3),
            };

            InventoryFoldResult r = InventoryFold.Fold(events, _decoder, EmptyCtx);

            Assert.That(StreamRouting.Of(EventKind.DrugTreatmentApplied), Is.Not.EqualTo(StreamId.World),
                "前提:该 Kind 确属域外流(否则本测试测不到「域外被跳过」)");
            Assert.That(TryRow(r, 1, out InventoryRow row), Is.True,
                "病史流条目在场,但世界流无 DropDespawned ⇒ 实例仍在");
            Assert.That(row.Qty, Is.EqualTo(1));
        }

        [Test]
        public void test_ac2016_dropDespawned_isTheOnlyCarrierOfConsumption()
        {
            // 同一条消耗,只把世界流那一条补上 ⇒ 实例消失。两半合起来证明载体正确。
            var without = new List<SimEvent>
            {
                Spawn(1, 10, 1, tick: 1, seq: 0),
                Harvest(1, 1, 1, 0, tick: 1, seq: 1),
                Claim(1, 7, tick: 2, seq: 0),
            };
            var with = new List<SimEvent>(without) { Despawn(1, qty: 1, tick: 3, seq: 0) };

            Assert.That(TryRow(InventoryFold.Fold(without, _decoder, EmptyCtx), 1, out _), Is.True,
                "无 DropDespawned ⇒ 仍在");
            Assert.That(TryRow(InventoryFold.Fold(with, _decoder, EmptyCtx), 1, out _), Is.False,
                "补上 DropDespawned ⇒ 消失(载体 = 世界流,不是处置事件)");
        }

        // ══════════════════════════════════════════════════════════════════
        // 完整性判据 —— 缺载体 ⇒ 报缺口(正向判据,非「跳过」)
        //
        // 🔁 **2026-10-27 闭合批:四条原断言已按新语义改写**(用户三项裁定)。
        //    改写**不是放宽**,而是把判据从「缺载体」搬到「查表链的每个失败出口」上 ——
        //    每条新测试都比原来多钉一段(旧版只证「报缺口」,新版还证「闭合后真的建了行」)。
        //    旧断言为何必须改:原 `test_r15_…` 断「产出**不得**建行」,而 R15 闭合后
        //    建行正是**正确行为**;不改就是让测试与实现对着干。
        // ══════════════════════════════════════════════════════════════════

        /// <summary>**R15 闭合**:<c>recipe_id</c> 查得到 ⇒ 逐 outputs 行**建行**,
        /// 且 <c>item_key</c> 来自配方表(不是猜的)。</summary>
        [Test]
        public void test_r15_craftOutputs_resolveItemKeyFromRecipeTable_andBuildRows()
        {
            var events = new List<SimEvent>
            {
                Spawn(1, OrdWillowBark, 3, tick: 1, seq: 0), Harvest(1, 1, 3, 0, tick: 1, seq: 1),
                Claim(1, 7, tick: 2, seq: 0),
                Craft(actor: 7, inputIds: new long[] { 1 }, consumed: new[] { 2 },
                      outQty: new[] { 1 }, outQuality: new[] { 4 }, outIds: new long[] { 2 },
                      tick: 3),
            };

            InventoryFoldResult r = InventoryFold.Fold(events, _decoder, Ctx());

            // ① 产出建了行,且 item_key 来自配方 outputs(salicylic_acid → OrdSalicylic)
            Assert.That(TryRow(r, 2, out InventoryRow output), Is.True,
                "R15 闭合后产出**必须**建行 —— 查得到配方就查得到 item_key");
            Assert.That(output.ItemOrdinal, Is.EqualTo(OrdSalicylic),
                "item_key 经 配方 outputs → 物品表 ordinal 折出,不是载荷位(载荷无位)");
            Assert.That(output.Quality, Is.EqualTo(4), "品级抄载荷 OutputQuality(不计算)");
            Assert.That(output.Qty, Is.EqualTo(1));
            Assert.That(output.Owner, Is.EqualTo(7), "炮制产出归炮制者");

            // ② 扣料侧仍生效
            Assert.That(TryRow(r, 1, out InventoryRow input), Is.True);
            Assert.That(input.Qty, Is.EqualTo(1), "扣料侧仍生效(3 - 2)");

            // ③ 全链路无缺口 —— 这是「闭合」的判据,不是「没崩」
            Assert.That(r.Gaps, Is.Empty, "R15 闭合后本条流应完全可重建");
            Assert.That(r.IsComplete, Is.True);
        }

        /// <summary>**R15 失败出口 ①**:<c>recipe_id</c> 在配方表里查无此号 ⇒ 显式缺口
        /// (不是静默丢产出 —— 那会让「配方表没装载」伪装成「炮制没产出」)。</summary>
        [Test]
        public void test_r15_recipeNotInTable_raisesExplicitGap()
        {
            var events = new List<SimEvent>
            {
                Spawn(1, OrdWillowBark, 3, tick: 1, seq: 0), Harvest(1, 1, 3, 0, tick: 1, seq: 1),
                Claim(1, 7, tick: 2, seq: 0),
                Craft(actor: 7, inputIds: new long[] { 1 }, consumed: new[] { 2 },
                      outQty: new[] { 1 }, outQuality: new[] { 0 }, outIds: new long[] { 2 },
                      tick: 3),
            };

            // 空配方表 —— 模拟「配方表未装载 / 版本不一致」
            var noRecipes = new InventoryFoldContext(MakeItems(), default,
                new FoldModuleCatalog(3, 2, OrdWoodenPost), null);
            InventoryFoldResult r = InventoryFold.Fold(events, _decoder, noRecipes);

            InventoryFoldGap gap = r.Gaps.Single(g => g.Kind == InventoryFoldGapKind.CraftRecipeNotInTable);
            Assert.That(gap.RegistryId, Is.EqualTo("R15"));
            Assert.That(gap.At.Tick, Is.EqualTo(3L), "缺口须定位到具体事件");
            Assert.That(TryRow(r, 2, out _), Is.False, "查不到配方 ⇒ **不建行**(不猜 item_key)");
            Assert.That(r.IsComplete, Is.False);
        }

        /// <summary>**R15 失败出口 ②**:配方 <c>outputs</c> 行数 ≠ 载荷三组行数
        /// ⇒ 显式缺口(逐行对位不可能,不猜、不截断、不补零)。</summary>
        [Test]
        public void test_r15_recipeOutputCountMismatch_raisesExplicitGap()
        {
            // 夹具配方 outputs 恒 1 行;载荷报 2 行 ⇒ 第二跳失败
            var events = new List<SimEvent>
            {
                Spawn(1, OrdWillowBark, 3, tick: 1, seq: 0), Harvest(1, 1, 3, 0, tick: 1, seq: 1),
                Claim(1, 7, tick: 2, seq: 0),
                Craft(actor: 7, inputIds: new long[] { 1 }, consumed: new[] { 2 },
                      outQty: new[] { 1, 1 }, outQuality: new[] { 4, 4 },
                      outIds: new long[] { 2, 3 }, tick: 3),
            };

            InventoryFoldResult r = InventoryFold.Fold(events, _decoder, Ctx());

            InventoryFoldGap gap = r.Gaps.Single(g => g.Kind == InventoryFoldGapKind.CraftOutputCountMismatch);
            Assert.That(gap.RegistryId, Is.EqualTo("R15"));
            Assert.That(gap.At.Tick, Is.EqualTo(3L), "缺口须定位到具体事件");
            Assert.That(gap.InstanceId, Is.EqualTo(2L), "锚到首个产出 id(载荷可定位点)");
            Assert.That(TryRow(r, 2, out _), Is.False, "行数对不齐 ⇒ **两行都不建**(不猜对位)");
            Assert.That(TryRow(r, 3, out _), Is.False);
            Assert.That(r.IsComplete, Is.False);
        }

        /// <summary>**R16 甲案**:唯一玩家已知 ⇒ 从该玩家库存扣 <c>cost(m)</c>,
        /// 按规则六 <c>instance_id</c> 升序兜底。</summary>
        [Test]
        public void test_r16_buildCost_deductedFromSolePlayer()
        {
            // 两栈 wooden_post(id 2 三份 / id 3 两份),玩家 7 持有;模块 3 cost = 2
            var events = new List<SimEvent>
            {
                Spawn(2, OrdWoodenPost, 3, tick: 1, seq: 0), Claim(2, 7, tick: 2, seq: 0),
                Spawn(3, OrdWoodenPost, 2, tick: 3, seq: 0), Claim(3, 7, tick: 4, seq: 0),
                Ev(EventKind.StructurePlaced,
                   new StructurePlacedPayload(100L, new WorldPos(1, 1, 1), 3, 0, 0), tick: 5),
            };

            InventoryFoldResult r = InventoryFold.Fold(events, _decoder, Ctx(solePlayer: 7));

            Assert.That(r.Gaps, Is.Empty, "R16 闭合后单机建造应完全可重建");
            Assert.That(TryRow(r, 2, out InventoryRow a), Is.True);
            Assert.That(a.Qty, Is.EqualTo(1), "id 升序兜底:先扣 id=2(3 → 1)");
            Assert.That(TryRow(r, 3, out InventoryRow b), Is.True);
            Assert.That(b.Qty, Is.EqualTo(2), "id=3 不动(cost=2 已由 id=2 满足)");
        }

        /// <summary>**R16 fail-closed**:<c>SolePlayer</c> 为空(多玩家 / 未接)⇒ 显式缺口。</summary>
        [Test]
        public void test_r16_noSolePlayer_raisesAttributionGap()
        {
            var events = new List<SimEvent>
            {
                Ev(EventKind.StructurePlaced,
                   new StructurePlacedPayload(100L, new WorldPos(1, 1, 1), 3, 0, 0), tick: 5),
            };

            InventoryFoldResult r = InventoryFold.Fold(events, _decoder, Ctx(solePlayer: null));

            InventoryFoldGap gap = r.Gaps.Single(g => g.Kind == InventoryFoldGapKind.BuildCostAttributionUnresolved);
            Assert.That(gap.RegistryId, Is.EqualTo("R16"));
            Assert.That(gap.EventKind, Is.EqualTo(EventKind.StructurePlaced));
            Assert.That(gap.At.Tick, Is.EqualTo(5L), "缺口须定位到具体事件");
            Assert.That(r.IsComplete, Is.False);
        }

        /// <summary>**R16 fail-closed ②**:唯一玩家已知但库存不足 ⇒ 显式缺口
        /// (不静默扣到负数,也不只扣一半)。</summary>
        [Test]
        public void test_r16_buildCostExceedsHolding_raisesExplicitGap()
        {
            // 模块 3 cost = 2,玩家 7 名下只有 1 份 wooden_post
            var events = new List<SimEvent>
            {
                Spawn(2, OrdWoodenPost, 1, tick: 1, seq: 0), Claim(2, 7, tick: 2, seq: 0),
                Ev(EventKind.StructurePlaced,
                   new StructurePlacedPayload(100L, new WorldPos(1, 1, 1), 3, 0, 0), tick: 5),
            };

            InventoryFoldResult r = InventoryFold.Fold(events, _decoder, Ctx(solePlayer: 7));

            InventoryFoldGap gap = r.Gaps.Single(g => g.Kind == InventoryFoldGapKind.BuildCostExceedsHolding);
            Assert.That(gap.RegistryId, Is.EqualTo("R16"));
            Assert.That(gap.At.Tick, Is.EqualTo(5L), "缺口须定位到具体事件");
            Assert.That(TryRow(r, 2, out InventoryRow a), Is.True);
            Assert.That(a.Qty, Is.EqualTo(1),
                "不足时**一份都不扣** —— 部分扣减会让「拒绝」与「扣了一半」两种状态无从区分");
            Assert.That(r.IsComplete, Is.False);
        }

        /// <summary>**R17 退出谓词集**(用户 2026-10-27 裁定「地面掉落」):
        /// <c>StructureRemoved</c> **不再**产生任何缺口 —— 返还物由 23 另发普通
        /// <c>DropSpawned</c>,fold 照常当掉落处理。</summary>
        [Test]
        public void test_r17_structureRemoved_noLongerInInventoryPredicateSet()
        {
            var events = new List<SimEvent>
            {
                Ev(EventKind.StructureRemoved,
                   new StructureRemovedPayload(100L, new WorldPos(1, 1, 1), 3), tick: 6),
            };

            InventoryFoldResult r = InventoryFold.Fold(events, _decoder, Ctx(solePlayer: 7));

            Assert.That(r.Gaps, Is.Empty,
                "R17 甲-1 裁定后 StructureRemoved **退出**库存谓词集 —— " +
                "它既不改投影也不报缺口(返还物是另发的 DropSpawned)");
            Assert.That(r.IsComplete, Is.True);
            Assert.That(r.Projection.InstanceCount, Is.EqualTo(0), "拆除本身不产生库存行");
        }

        /// <summary>**R17 闭环的正向形态**:拆除后 23 发的普通 <c>DropSpawned</c>
        /// 被 fold 当掉落处理(建行、在地上、品级按 BL-28 建材桶恒 1)。</summary>
        [Test]
        public void test_r17_refundDrop_isFoldedAsOrdinaryDrop()
        {
            var events = new List<SimEvent>
            {
                Ev(EventKind.StructureRemoved,
                   new StructureRemovedPayload(100L, new WorldPos(1, 1, 1), 3), tick: 6),
                // 23 的返还事件:新铸 id=9,qty = Refund(1),锚点 = 拆除格
                Spawn(9, OrdWoodenPost, 1, tick: 6, seq: 1),
            };

            InventoryFoldResult r = InventoryFold.Fold(events, _decoder, Ctx(solePlayer: 7));

            Assert.That(TryRow(r, 9, out InventoryRow refund), Is.True, "返还款被当普通掉落建行");
            Assert.That(refund.ItemOrdinal, Is.EqualTo(OrdWoodenPost));
            Assert.That(refund.Qty, Is.EqualTo(1));
            Assert.That(refund.Owner, Is.EqualTo(InventoryProjection.NoOwner), "在地上(未被认领)");
            Assert.That(refund.Quality, Is.EqualTo(BuildPartQualityGate.P0BuildPartQuality),
                "建材品级按 BL-28 口径常量恒 1(R18 桶③)");
            Assert.That(r.Gaps, Is.Empty, "R17 + R18 建材桶闭合后,返还链路完全可重建");
        }

        /// <summary>**R18 桶外仍报缺口**:非 17 来源、非 Craft 产出、非建材的实例
        /// (25 / 27 / 29 掉落)依然没有品级载体。</summary>
        [Test]
        public void test_r18_nonBuildPartWithoutQualitySource_stillRaisesGap()
        {
            // OrdSplint = tool,不是 build_part;且无 ResourceHarvested / 无 Craft 产出
            var events = new List<SimEvent> { Spawn(5, itemKey: OrdSplint, qty: 1, tick: 1) };

            InventoryFoldResult r = InventoryFold.Fold(events, _decoder, Ctx());

            InventoryFoldGap gap = r.Gaps.Single(g => g.Kind == InventoryFoldGapKind.InstanceWithoutQualitySource);
            Assert.That(gap.RegistryId, Is.EqualTo("R18"));
            Assert.That(gap.InstanceId, Is.EqualTo(5L));
            Assert.That(r.IsComplete, Is.False);

            Assert.That(TryRow(r, 5, out InventoryRow row), Is.True,
                "缺口**不阻止**建行(部分态仍可见,但 IsComplete=false 不许当 InventoryOf 用)");
            Assert.That(row.Quality, Is.EqualTo(0), "品级不可知 ⇒ 0,不是猜一个值");
        }

        [Test]
        public void test_conflictingQualitySource_raisesGapAndFirstSeenWins()
        {
            // 评审 `review-s20-10-inventory-001-2026-10-27.md` B-2:
            //   `InventoryFoldGapKind.ConflictingQualitySource`(:115)的**唯一产出点**
            //   (InventoryFold.cs:521)在此前**零夹具** —— 该分支从未被执行过,
            //   「同一实例两处矛盾会报缺口」这句话没有任何东西在钉它。
            //   本夹具补上:两采同实例不同档 ⇒ 报缺口 **且** 品级取首条。
            var events = new List<SimEvent>
            {
                Spawn(9, itemKey: 3, qty: 1, tick: 1, seq: 0),
                Harvest(9, nodeId: 1, qty: 1, outQuality: 2, tick: 2, seq: 0),
                Harvest(9, nodeId: 1, qty: 1, outQuality: 5, tick: 3, seq: 0),
            };

            InventoryFoldResult r = InventoryFold.Fold(events, _decoder, EmptyCtx);

            Assert.That(
                r.Gaps.Any(g => g.Kind == InventoryFoldGapKind.ConflictingQualitySource
                                && g.InstanceId == 9L), Is.True,
                "同一实例两处给出不同 out_quality ⇒ 必须报缺口 —— 静默取其一会让"
                + "「品级只从载荷抄」变成「品级按扫描顺序抽签」");
            Assert.That(r.IsComplete, Is.False,
                "有矛盾来源 ⇒ 部分态,不得当 InventoryOf 用");

            Assert.That(TryRow(r, 9, out InventoryRow row), Is.True);
            Assert.That(row.Quality, Is.EqualTo(2),
                "**首条胜出**(InventoryFold.cs:528 的 `continue` 语义):"
                + "后到的 5 不得覆盖已记的 2");
        }

        [Test]
        public void test_harvestWithoutSpawn_isDangling_notSilentlyDropped()
        {
            // 今天 20 侧写者缺位时 17 的真实形态:ResourceHarvested 落了、DropSpawned 没落。
            var events = new List<SimEvent> { Harvest(77, nodeId: 4, qty: 1, outQuality: 1, tick: 1) };

            InventoryFoldResult r = InventoryFold.Fold(events, _decoder, EmptyCtx);

            Assert.That(r.Gaps.Any(g => g.Kind == InventoryFoldGapKind.DanglingInstanceReference
                                        && g.InstanceId == 77L), Is.True,
                "有采集无出生 ⇒ 必须报缺口(静默丢弃会让「库存全空」看起来像正常)");
            Assert.That(r.IsComplete, Is.False);
        }

        [Test]
        public void test_duplicateSpawnOnLiveInstance_raisesDuplicateInstanceId()
        {
            var events = new List<SimEvent>
            {
                Spawn(1, 10, 1, tick: 1, seq: 0),
                Harvest(1, 1, 1, 0, tick: 1, seq: 1),
                Spawn(1, 10, 1, tick: 2, seq: 0),
            };

            InventoryFoldResult r = InventoryFold.Fold(events, _decoder, EmptyCtx);

            Assert.That(r.Gaps.Any(g => g.Kind == InventoryFoldGapKind.DuplicateInstanceId), Is.True,
                "号永不回收(ADR-006 机制 A 不变量②)");
        }

        [Test]
        public void test_secondClaim_raisesDuplicateClaim_andKeepsFirstOwner()
        {
            var events = new List<SimEvent>
            {
                Spawn(1, 10, 1, tick: 1, seq: 0), Harvest(1, 1, 1, 0, tick: 1, seq: 1),
                Claim(1, 7, tick: 2, seq: 0), Claim(1, 8, tick: 3, seq: 0),
            };

            InventoryFoldResult r = InventoryFold.Fold(events, _decoder, EmptyCtx);

            Assert.That(r.Gaps.Any(g => g.Kind == InventoryFoldGapKind.DuplicateClaim), Is.True);
            Assert.That(TryRow(r, 1, out InventoryRow row), Is.True);
            Assert.That(row.Owner, Is.EqualTo(7), "二次 claim 被拒 ⇒ owner 保持第一个 claimer");
        }

        [Test]
        public void test_despawnMoreThanHeld_raisesInvalidQuantity_andKeepsRow()
        {
            var events = new List<SimEvent>
            {
                Spawn(1, 10, 2, tick: 1, seq: 0), Harvest(1, 1, 2, 0, tick: 1, seq: 1),
                Despawn(1, qty: 5, tick: 2, seq: 0),
            };

            InventoryFoldResult r = InventoryFold.Fold(events, _decoder, EmptyCtx);

            Assert.That(r.Gaps.Any(g => g.Kind == InventoryFoldGapKind.InvalidDespawnQuantity), Is.True);
            Assert.That(TryRow(r, 1, out InventoryRow row), Is.True);
            Assert.That(row.Qty, Is.EqualTo(2), "畸形扣减被拒 ⇒ qty 不动(不扣成负数)");
        }

        /// <summary>`InvalidDespawnQuantity` 的**另一半**:`qty ≤ 0`。
        /// (2026-10-27 补 · 评审 `review-s20-10-inventory-001` W-3 ——
        ///  上一条只覆盖 `qty > 持有量` 那一支,而 `InventoryFold.cs:384` 的 `p.Qty &lt;= 0`
        ///  是**独立分支**,`Branch` 覆盖率 1/2。) 两条各喂 zero 与负,验证都不扣。</summary>
        [Test]
        public void test_despawnNonPositiveQty_raisesInvalidQuantity_andKeepsRow()
        {
            foreach (int bad in new[] { 0, -3 })
            {
                var events = new List<SimEvent>
                {
                    Spawn(1, 10, 2, tick: 1, seq: 0), Harvest(1, 1, 2, 0, tick: 1, seq: 1),
                    Despawn(1, qty: bad, tick: 2, seq: 0),
                };

                InventoryFoldResult r = InventoryFold.Fold(events, _decoder, EmptyCtx);

                Assert.That(
                    r.Gaps.Any(g => g.Kind == InventoryFoldGapKind.InvalidDespawnQuantity
                                    && g.InstanceId == 1L), Is.True,
                    $"qty={bad} 属畸形输入(扣减量须 > 0)⇒ 必须报缺口,不得静默当成「扣 0」");
                Assert.That(TryRow(r, 1, out InventoryRow row), Is.True);
                Assert.That(row.Qty, Is.EqualTo(2), $"qty={bad} 被拒 ⇒ 持有量不动");
            }
        }

        [Test]
        public void test_craftConsumeOtherPlayersItem_raisesConsumptionExceedsHolding()
        {
            var events = new List<SimEvent>
            {
                Spawn(1, 10, 2, tick: 1, seq: 0), Harvest(1, 1, 2, 0, tick: 1, seq: 1),
                Claim(1, 7, tick: 2, seq: 0),
                Craft(actor: 9, inputIds: new long[] { 1 }, consumed: new[] { 1 },
                      outQty: Array.Empty<int>(), outQuality: Array.Empty<int>(),
                      outIds: Array.Empty<long>(), tick: 3),
            };

            InventoryFoldResult r = InventoryFold.Fold(events, _decoder, EmptyCtx);

            Assert.That(r.Gaps.Any(g => g.Kind == InventoryFoldGapKind.ConsumptionExceedsHolding), Is.True,
                "投料只能扣自己箱里的");
            Assert.That(TryRow(r, 1, out InventoryRow row), Is.True);
            Assert.That(row.Qty, Is.EqualTo(2), "跨人投料被拒 ⇒ 不扣");
        }

        [Test]
        public void test_craftZeroOutput_raisesNoCraftOutputGap()
        {
            // 只扣不出 ⇒ 不应报 R15(判据是「有产出行才缺 item_key 位」)
            var events = new List<SimEvent>
            {
                Spawn(1, 10, 2, tick: 1, seq: 0), Harvest(1, 1, 2, 0, tick: 1, seq: 1),
                Claim(1, 7, tick: 2, seq: 0),
                Craft(actor: 7, inputIds: new long[] { 1 }, consumed: new[] { 1 },
                      outQty: Array.Empty<int>(), outQuality: Array.Empty<int>(),
                      outIds: Array.Empty<long>(), tick: 3),
            };

            InventoryFoldResult r = InventoryFold.Fold(events, _decoder, EmptyCtx);

            Assert.That(r.Gaps.Any(g => g.Kind == InventoryFoldGapKind.CraftOutputWithoutItemKey), Is.False);
            Assert.That(r.IsComplete, Is.True, "只扣不出的 Craft 落在全可重建子域内");
        }

        [Test]
        public void test_craftUnevenArrays_areRejectedAtCodecBoundary_beforeFoldEverSeesThem()
        {
            // ⚠️ **本条是实测改判,不是原设想**。原稿想「构造不等长 Craft 载荷 ⇒ 断言 fold 报
            //    MalformedPayloadPairing」,首跑即红:codec **写侧**就抛了
            //    (`PayloadCodec.World.cs:200-208`「坏数据不进字节面」)—— 该形态**根本到不了 fold**。
            //
            // ⇒ 判据改成能真跑的第一道门:codec 拒收。fold 侧的 `MalformedPayloadPairing` 分支
            //    **保留为纵深防御**(读侧可能收到别端写坏的数据 / 生产池实现变更),
            //    但**本件未能用真 codec 构造出该形态 ⇒ 该分支的 Proven = ✗**(禁借绿)。
            var unevenOutput = new CraftPayload(7, 7, 3L, 10L,
                new long[] { 1 }, new[] { 1 },
                new[] { 1 }, new[] { 0 }, new long[] { 2, 3 },
                new WorldPos(0, 0, 0));
            Assert.Throws<ArgumentException>(
                () => _encoder.Encode(EventKind.Craft, unevenOutput),
                "codec 须在写侧拒收产出组不等长(坏数据不进字节面)");

            var unevenInput = new CraftPayload(7, 7, 3L, 10L,
                new long[] { 1, 2 }, new[] { 1 },
                Array.Empty<int>(), Array.Empty<int>(), Array.Empty<long>(),
                new WorldPos(0, 0, 0));
            Assert.Throws<ArgumentException>(
                () => _encoder.Encode(EventKind.Craft, unevenInput),
                "codec 须在写侧拒收输入组不等长");
        }

        [Test]
        public void test_nullDecoder_throwsInsteadOfReturningEmptyProjection()
        {
            // fail-closed:空解码器 ⇒ 若返回空投影,「库存全空」会成为静默失败。
            Assert.Throws<ArgumentNullException>(
                () => InventoryFold.Fold(new List<SimEvent> { Spawn(1, 10, 1, tick: 1, seq: 0) }, null, EmptyCtx));
        }

        [Test]
        public void test_requireComplete_onCleanStream_returnsSameProjection()
        {
            var events = new List<SimEvent>
            {
                Spawn(1, 10, 1, tick: 1, seq: 0), Harvest(1, 1, 1, 0, tick: 1, seq: 1),
                Claim(1, 7, tick: 2, seq: 0),
            };

            InventoryFoldResult r = InventoryFold.Fold(events, _decoder, EmptyCtx);

            Assert.That(r.RequireComplete().CanonicalForm(), Is.EqualTo(r.Projection.CanonicalForm()));
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-20-05 —— 20 侧零结算求值(源面 + IL 面)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_ac2005_foldSource_neverEvaluatesEfficiencyOrQualityMod()
        {
            // 源面判据:代码行里不得出现求值符号(注释剥掉 —— 类头注解释了 EFF 的存在理由,
            // 那是**说明**不是**调用**;不剥会把正当注释判成红,剥了才不会既误伤又借绿)。
            string path = Path.Combine(UnityEngine.Application.dataPath,
                "Sim", "ItemDatabase", "InventoryFold.cs");
            Assert.That(File.Exists(path), Is.True, "源文件须存在:@" + path);

            string code = StripComments(File.ReadAllText(path));
            foreach (string banned in new[]
                     { "EFF", "QualityMod", "quality_distribution", "ConservationSolver",
                       "RecipeSettlementSolver", "QualityTimelineSolver", "StackingSolver", "FixPow" })
            {
                Assert.That(code.Contains(banned), Is.False,
                    "AC-20-05:fold 内不得出现 " + banned + " —— 20 不求值,只搬运载荷里的整数");
            }
            Assert.That(code, Does.Contain("DropSpawned"),
                "下界:剥离后仍须看到真实代码(否则整文件被误剥 ⇒ 上列断言全部空转)");
        }

        [Test]
        public void test_ac2005_foldIL_neverCallsQualitySolvers()
        {
            // 类型面:fold 的 IL 里不得出现对 21a 求值器的调用 ——
            // 这是**能力断言**(加一层包装类 / 改方法名都逃不掉),不是 grep。
            Assert.That(EditorUtility.scriptCompilationFailed, Is.False,
                "读 IL 的前提 = 编译成功(否则读到的是上一版 DLL = 假绿)");

            var banned = new HashSet<string>(StringComparer.Ordinal)
            {
                "QualityTimelineSolver", "RecipeSettlementSolver", "ConservationSolver", "StackingSolver",
                "RecipeSettlementConstants", "GatherQualityRoll", "QualityDistributionShape",
            };

            // fold 代码路径的类型全集 —— **须与 `InventoryFold.cs` 的声明逐一对应**。
            // ⚠️ 本集合若与文件脱节(删/添类型未同步),下面的 `scannedTypes` 下界断言会红。
            var FoldCodePathTypes = new HashSet<string>(StringComparer.Ordinal)
            {
                "DaYiJingCheng.Sim.InventoryFold",          // static class —— Fold 本体
                "DaYiJingCheng.Sim.InventoryFoldResult",    // 结果 + IsComplete / RequireComplete
                "DaYiJingCheng.Sim.InventoryProjection",    // 投影(按持有者视图)
                "DaYiJingCheng.Sim.InventoryRow",           // 行 + StackKey
                "DaYiJingCheng.Sim.InventoryFoldGap",       // 缺口结构 + RegistryId
                "DaYiJingCheng.Sim.InventoryFoldGapKind",   // 缺口枚举
                // 2026-10-27 R15–R18 闭合批新增 —— 本集合的「须与文件声明逐一对应」判据
                // 正是靠这类同步义务生效:加类型不同步 ⇒ 判据面静默变窄。
                "DaYiJingCheng.Sim.InventoryFoldContext",   // 查表源 + 单机归属快照
                "DaYiJingCheng.Sim.ItemLookup",             // 物品表双向索引(嵌套类型,
                                                            //   反射名按 AllTypes 的口径展开)
            };

            var dll = AssemblyGates.ScriptAssemblyPath("Sim");
            Assert.That(File.Exists(dll), Is.True, "Sim.dll 须已编译");

            var hits = new List<string>();
            int scanned = 0;
            var scannedTypes = new List<string>();
            bool sawFold = false;
            using (var asm = AssemblyDefinition.ReadAssembly(dll, new ReaderParameters
            {
                ReadingMode = ReadingMode.Deferred, InMemory = true,
            }))
            {
                foreach (var type in AssemblyGates.AllTypes(asm.MainModule))
                {
                    // ⚠️ **2026-10-27 订正(评审 `review-s20-10-inventory-001` W-1 / G-5)**:
                    //   原判据是 `if (type.FullName != "DaYiJingCheng.Sim.InventoryFold") continue;`
                    //   ⇒ 扫描面 = **单个类型**,而 AC-20-05 的原文是「**20 的代码路径**」。
                    //   `Sim/ItemDatabase/` 下另有 16 个类型(共 17 文件),嵌套/lambda 生成的
                    //   闭包类型也全在扫描面外 ⇒ 对「20 不求值」这句话只证了一小片,`✅` 有借绿成分。
                    //   订正 = 扫描面放宽到**命名空间前缀** `DaYiJingCheng.Sim`,并断言扫到的类型数
                    //   与「InventoryFold 在册」两个下界,防扫描面再次静默缩回一个类。
                    // ⚠️ **判据面 = fold 的整个代码路径**(2026-10-27 订正 · 评审 W-1 / G-5):
                    //   原判据 `type.FullName != "...InventoryFold" ⇒ continue` 只扫 1 个类型,
                    //   而 `InventoryFold.cs` 实声明 **6 个类型**(Fold / Result / Projection /
                    //   Row / Gap / GapKind)—— 缺口构造、行构造、投影构建全在扫描面外。
                    //   本条把判据面收到「**该文件的全部类型**」,既补上 G-5 的窄面,
                    //   又不越界到「整个 Sim 命名空间」。
                    //
                    // ⚠️ **为什么不放宽到命名空间前缀**(实测否定):那样会命中
                    //   ① 别的系统的合法代码(`ProcessingSettlementEntry` / `CraftingSettlementEntry`
                    //      属 18,它们**本来就该**调 `RecipeSettlementSolver`);
                    //   ② 20 自己**必须**的委托(`StacksOf → StackingSolver.CanStack` 是 AC-20-04
                    //      明文要求「不得复制公式体」)。
                    //   两者都不是违规 ⇒ 命名空间面会假红,且真违规会被噪声淹没。
                    //   **AC-20-05 的「20 的代码路径」= fold 这条路径,不是 20 的全部代码**
                    //   (堆叠/折重/CanCarry 各有自己的 AC)。
                    if (type.FullName == null || !FoldCodePathTypes.Contains(type.FullName))
                        continue;

                    // 被禁类型自身不算「调用方」—— 那是定义体的自引用,不是「有人调它」。
                    if (banned.Contains(type.Name)) continue;

                    bool isFold = type.FullName == "DaYiJingCheng.Sim.InventoryFold";
                    if (isFold) sawFold = true;

                    foreach (var m in type.Methods)
                    {
                        Mono.Cecil.Cil.MethodBody body;
                        try { body = m.Body; } catch { continue; }
                        if (body == null) continue;
                        scanned++;
                        if (isFold) scannedTypes.Add(m.FullName);
                        foreach (var ins in body.Instructions)
                        {
                            if (!(ins.Operand is MethodReference mr)) continue;
                            if (mr.DeclaringType != null && banned.Contains(mr.DeclaringType.Name))
                                hits.Add(m.FullName + " -> " + mr.FullName);
                        }
                    }
                }
            }

            Assert.That(sawFold, Is.True,
                "扫描面里没有 InventoryFold —— 命名空间前缀判据已失配(类型搬家 / 汇编改名),拒以空集冒充绿");
            Assert.That(scanned, Is.GreaterThan(0),
                "Sim 内一个方法体都没扫到 —— 扫描面已失效(类型改名 / DLL 陈旧),拒以空集冒充绿");
            Assert.That(scannedTypes, Is.Not.Empty,
                "InventoryFold 自身零方法体被扫到 —— 判据面与文件脱节(回退到 AC 语义层之外)");

            // ⚠️ **扫描面 ↔ 文件声明 的机械对账**(2026-10-27 立):
            //   上面的 `FoldCodePathTypes` 是手写的,文件里新增一个类型就会**静默地**落在
            //   扫描面外 —— 那正是本夹具 W-1 要消灭的失败模式(判据面比 AC 语义层窄)。
            //   故这里从**源码**数一遍声明,与集合逐一对账。
            string foldSrc = StripComments(File.ReadAllText(Path.Combine(
                UnityEngine.Application.dataPath, "Sim", "ItemDatabase", "InventoryFold.cs")));
            var declared = new HashSet<string>(StringComparer.Ordinal);
            foreach (System.Text.RegularExpressions.Match mm in
                     System.Text.RegularExpressions.Regex.Matches(
                         foldSrc, @"\b(?:class|struct|enum)\s+(\w+)"))
                declared.Add("DaYiJingCheng.Sim." + mm.Groups[1].Value);

            CollectionAssert.AreEquivalent(declared, FoldCodePathTypes,
                "扫描面须与 InventoryFold.cs 的类型声明**逐一对应** —— " +
                "文件里加/删类型而不同步本集合,判据面就会静默变窄(或宽到误伤别的系统)");
            Assert.That(hits, Is.Empty, "20 的代码路径不得调用 21a 的求值器:" + string.Join(" | ", hits));
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-20-17 —— 零 ItemConsumed / ItemTransferred 追加 Kind
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_ac2017_registryHasNoItemConsumedOrTransferredKind()
        {
            // 判据 = **枚举面**:若日后有人加了这两个 Kind,IsDefined 会命中 ⇒ 本条红。
            // 这是能力断言 —— 文档里写「不追加」没有用,枚举里有就是有。
            Assert.That(Enum.IsDefined(typeof(EventKind), "ItemConsumed"), Is.False,
                "AC-20-17:扣减由 Craft.ActualConsumed 唯一推出,不得追加 ItemConsumed Kind");
            Assert.That(Enum.IsDefined(typeof(EventKind), "ItemTransferred"), Is.False,
                "AC-20-17:转移走 Drop* 三支,不得追加 ItemTransferred Kind");
        }

        [Test]
        public void test_ac2017_foldIgnoresWorldKindsOutsideTheSevenPredicates()
        {
            // 判据 = 七谓词是 fold 的全部输入面。世界流内非七谓词的事件(ActorCellEntered)
            // ⇒ 投影零变化 —— 证明 fold 没偷偷扩到第八支。
            var baseline = new List<SimEvent>
            {
                Spawn(1, 10, 1, tick: 1, seq: 0), Harvest(1, 1, 1, 0, tick: 1, seq: 1),
                Claim(1, 7, tick: 2, seq: 0),
            };
            var withExtra = new List<SimEvent>(baseline)
            {
                Ev(EventKind.ActorCellEntered,
                   new ActorCellEnteredPayload(7, new WorldPos(0, 0, 0), 2L), tick: 2, seq: 9),
            };

            string a = InventoryFold.Fold(baseline, _decoder, EmptyCtx).Projection.CanonicalForm();
            string b = InventoryFold.Fold(withExtra, _decoder, EmptyCtx).Projection.CanonicalForm();

            Assert.That(StreamRouting.Of(EventKind.ActorCellEntered), Is.EqualTo(StreamId.World),
                "前提:该 Kind 属世界流(否则测不到「域内但非七谓词」)");
            Assert.That(b, Is.EqualTo(a), "世界流内非七谓词 Kind 不得改变投影");
        }

        // ══════════════════════════════════════════════════════════════════
        // 门 A —— 本件零 UnityEngine / 零 float
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_gateA_simAssemblyHasNoEngineReferenceAndFoldHasNoFloat()
        {
            Assert.That(EditorUtility.scriptCompilationFailed, Is.False, "读 IL 的前提 = 编译成功");

            var dll = AssemblyGates.ScriptAssemblyPath("Sim");
            var hits = new List<string>();

            using (var asm = AssemblyDefinition.ReadAssembly(dll, new ReaderParameters
            {
                ReadingMode = ReadingMode.Deferred, InMemory = true,
            }))
            {
                foreach (var r in asm.MainModule.AssemblyReferences)
                    if (r.Name.StartsWith("UnityEngine", StringComparison.Ordinal)
                        || r.Name.StartsWith("UnityEditor", StringComparison.Ordinal))
                        hits.Add("asmref:" + r.Name);

                foreach (var type in AssemblyGates.AllTypes(asm.MainModule))
                {
                    if (type.FullName != "DaYiJingCheng.Sim.InventoryFold") continue;
                    foreach (var m in type.Methods)
                    {
                        if (m.ReturnType.MetadataType == MetadataType.Single
                            || m.ReturnType.MetadataType == MetadataType.Double)
                            hits.Add(m.FullName + " -> return " + m.ReturnType.Name);
                        foreach (var pm in m.Parameters)
                            if (pm.ParameterType.MetadataType == MetadataType.Single
                                || pm.ParameterType.MetadataType == MetadataType.Double)
                                hits.Add(m.FullName + " -> param " + pm.Name);
                    }
                }
            }

            Assert.That(hits, Is.Empty,
                "门 A:Sim 不得引用 UnityEngine / fold 不得出现浮点:" + string.Join(" | ", hits));
        }

        // ══════════════════════════════════════════════════════════════════
        // 私有辅助
        // ══════════════════════════════════════════════════════════════════

        /// <summary>病史流用药处置事件(11 写)。载荷形状与本测试无关,只要它真的落病史流。</summary>
        private SimEvent DrugTreatment(long tick)
        {
            var payload = new DrugTreatmentAppliedPayload(
                tick, treatmentId: 1, actorId: 7, polarity: 0,
                drugPotency: new Fix(0L), halfLife: 20L, seq: 0L);
            return new SimEvent(tick, new PatientId(1), -1, EventKind.DrugTreatmentApplied,
                _encoder.Encode(EventKind.DrugTreatmentApplied, payload));
        }

        /// <summary>把 C# 注释剥掉(源面判据既不得被注释误伤,也不得被注释借绿)。</summary>
        private static string StripComments(string src)
        {
            var sb = new System.Text.StringBuilder(src.Length);
            bool line = false, block = false, str = false;
            for (int i = 0; i < src.Length; i++)
            {
                char c = src[i];
                char n = i + 1 < src.Length ? src[i + 1] : '\0';
                if (line) { if (c == '\n') { line = false; sb.Append(c); } continue; }
                if (block) { if (c == '*' && n == '/') { block = false; i++; } continue; }
                if (str)
                {
                    sb.Append(c);
                    if (c == '\\') { if (n != '\0') { sb.Append(n); i++; } }
                    else if (c == '"') str = false;
                    continue;
                }
                if (c == '/' && n == '/') { line = true; i++; continue; }
                if (c == '/' && n == '*') { block = true; i++; continue; }
                if (c == '"') { str = true; }
                sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
