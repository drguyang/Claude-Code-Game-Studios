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

            InventoryFoldResult r = InventoryFold.Fold(events, _decoder);

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

            InventoryFoldResult r = InventoryFold.Fold(events, _decoder);

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

            InventoryFoldResult r = InventoryFold.Fold(events, _decoder);

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

            InventoryFoldResult r = InventoryFold.Fold(events, _decoder);

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

            InventoryFoldResult r = InventoryFold.Fold(events, _decoder);

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

            string a = InventoryFold.Fold(ordered, _decoder).Projection.CanonicalForm();
            string b = InventoryFold.Fold(shuffled, _decoder).Projection.CanonicalForm();

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

            InventoryFoldResult r1 = InventoryFold.Fold(events, _decoder);
            InventoryFoldResult r2 = InventoryFold.Fold(events, _decoder);

            Assert.That(r2.Projection.CanonicalForm(), Is.EqualTo(r1.Projection.CanonicalForm()));
            Assert.That(r1.IsComplete, Is.True);
            Assert.That(r1.Gaps, Is.Empty);
        }

        [Test]
        public void test_ac2003_emptyStream_emptyProjectionNotNull()
        {
            InventoryFoldResult r = InventoryFold.Fold(Array.Empty<SimEvent>(), _decoder);

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

            InventoryFoldResult r = InventoryFold.Fold(events, _decoder);

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

            Assert.That(TryRow(InventoryFold.Fold(without, _decoder), 1, out _), Is.True,
                "无 DropDespawned ⇒ 仍在");
            Assert.That(TryRow(InventoryFold.Fold(with, _decoder), 1, out _), Is.False,
                "补上 DropDespawned ⇒ 消失(载体 = 世界流,不是处置事件)");
        }

        // ══════════════════════════════════════════════════════════════════
        // 完整性判据 —— 缺载体 ⇒ 报缺口(正向判据,非「跳过」)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_r15_craftOutputs_raiseGapAndDoNotInventItemKey()
        {
            var events = new List<SimEvent>
            {
                Spawn(1, 10, 3, tick: 1, seq: 0), Harvest(1, 1, 3, 0, tick: 1, seq: 1),
                Claim(1, 7, tick: 2, seq: 0),
                Craft(actor: 7, inputIds: new long[] { 1 }, consumed: new[] { 2 },
                      outQty: new[] { 1 }, outQuality: new[] { 0 }, outIds: new long[] { 2 },
                      tick: 3),
            };

            InventoryFoldResult r = InventoryFold.Fold(events, _decoder);

            InventoryFoldGap gap = r.Gaps.Single(g => g.Kind == InventoryFoldGapKind.CraftOutputWithoutItemKey);
            Assert.That(gap.RegistryId, Is.EqualTo("R15"));
            Assert.That(gap.At.Tick, Is.EqualTo(3L), "缺口须定位到具体事件");
            Assert.That(TryRow(r, 2, out _), Is.False,
                "产出**不得**建行 —— 造占位就是发明一个生产端永不发出的字段");

            Assert.That(TryRow(r, 1, out InventoryRow input), Is.True);
            Assert.That(input.Qty, Is.EqualTo(1), "扣料侧仍生效(3 - 2)");

            Assert.That(r.IsComplete, Is.False);
            Assert.Throws<InvalidOperationException>(() => r.RequireComplete());
        }

        [Test]
        public void test_r16_structurePlaced_raisesGapWithNoActor()
        {
            var events = new List<SimEvent>
            {
                Ev(EventKind.StructurePlaced,
                   new StructurePlacedPayload(100L, new WorldPos(1, 1, 1), 3, 0, 0), tick: 5),
            };

            InventoryFoldResult r = InventoryFold.Fold(events, _decoder);

            InventoryFoldGap gap = r.Gaps.Single(g => g.Kind == InventoryFoldGapKind.StructurePlacementWithoutActor);
            Assert.That(gap.RegistryId, Is.EqualTo("R16"));
            Assert.That(gap.EventKind, Is.EqualTo(EventKind.StructurePlaced));
            Assert.That(gap.At.Tick, Is.EqualTo(5L), "缺口须定位到具体事件");
            Assert.That(r.IsComplete, Is.False);
        }

        [Test]
        public void test_r17_structureRemoved_raisesGapWithNoRefundIdentity()
        {
            var events = new List<SimEvent>
            {
                Ev(EventKind.StructureRemoved,
                   new StructureRemovedPayload(100L, new WorldPos(1, 1, 1), 3), tick: 6),
            };

            InventoryFoldResult r = InventoryFold.Fold(events, _decoder);

            InventoryFoldGap gap = r.Gaps.Single(g => g.Kind == InventoryFoldGapKind.StructureRemovalWithoutRefundIdentity);
            Assert.That(gap.RegistryId, Is.EqualTo("R17"));
            Assert.That(r.IsComplete, Is.False);
        }

        [Test]
        public void test_r18_instanceWithNoQualitySource_raisesGapButKeepsRow()
        {
            // 只有出生,没有 ResourceHarvested(模拟 25 / 27 / 29 / 18 起货路径)
            var events = new List<SimEvent> { Spawn(5, itemKey: 2, qty: 1, tick: 1) };

            InventoryFoldResult r = InventoryFold.Fold(events, _decoder);

            InventoryFoldGap gap = r.Gaps.Single(g => g.Kind == InventoryFoldGapKind.InstanceWithoutQualitySource);
            Assert.That(gap.RegistryId, Is.EqualTo("R18"));
            Assert.That(gap.InstanceId, Is.EqualTo(5L));
            Assert.That(r.IsComplete, Is.False);

            Assert.That(TryRow(r, 5, out InventoryRow row), Is.True,
                "缺口**不阻止**建行(部分态仍可见,但 IsComplete=false 不许当 InventoryOf 用)");
            Assert.That(row.Quality, Is.EqualTo(0), "品级不可知 ⇒ 0,不是猜一个值");
        }

        [Test]
        public void test_harvestWithoutSpawn_isDangling_notSilentlyDropped()
        {
            // 今天 20 侧写者缺位时 17 的真实形态:ResourceHarvested 落了、DropSpawned 没落。
            var events = new List<SimEvent> { Harvest(77, nodeId: 4, qty: 1, outQuality: 1, tick: 1) };

            InventoryFoldResult r = InventoryFold.Fold(events, _decoder);

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

            InventoryFoldResult r = InventoryFold.Fold(events, _decoder);

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

            InventoryFoldResult r = InventoryFold.Fold(events, _decoder);

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

            InventoryFoldResult r = InventoryFold.Fold(events, _decoder);

            Assert.That(r.Gaps.Any(g => g.Kind == InventoryFoldGapKind.InvalidDespawnQuantity), Is.True);
            Assert.That(TryRow(r, 1, out InventoryRow row), Is.True);
            Assert.That(row.Qty, Is.EqualTo(2), "畸形扣减被拒 ⇒ qty 不动(不扣成负数)");
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

            InventoryFoldResult r = InventoryFold.Fold(events, _decoder);

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

            InventoryFoldResult r = InventoryFold.Fold(events, _decoder);

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
                () => InventoryFold.Fold(new List<SimEvent> { Spawn(1, 10, 1, tick: 1, seq: 0) }, null));
        }

        [Test]
        public void test_requireComplete_onCleanStream_returnsSameProjection()
        {
            var events = new List<SimEvent>
            {
                Spawn(1, 10, 1, tick: 1, seq: 0), Harvest(1, 1, 1, 0, tick: 1, seq: 1),
                Claim(1, 7, tick: 2, seq: 0),
            };

            InventoryFoldResult r = InventoryFold.Fold(events, _decoder);

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

            var dll = AssemblyGates.ScriptAssemblyPath("Sim");
            Assert.That(File.Exists(dll), Is.True, "Sim.dll 须已编译");

            var hits = new List<string>();
            int scanned = 0;
            using (var asm = AssemblyDefinition.ReadAssembly(dll, new ReaderParameters
            {
                ReadingMode = ReadingMode.Deferred, InMemory = true,
            }))
            {
                foreach (var type in AssemblyGates.AllTypes(asm.MainModule))
                {
                    if (type.FullName != "DaYiJingCheng.Sim.InventoryFold") continue;
                    foreach (var m in type.Methods)
                    {
                        Mono.Cecil.Cil.MethodBody body;
                        try { body = m.Body; } catch { continue; }
                        if (body == null) continue;
                        scanned++;
                        foreach (var ins in body.Instructions)
                        {
                            if (!(ins.Operand is MethodReference mr)) continue;
                            if (mr.DeclaringType != null && banned.Contains(mr.DeclaringType.Name))
                                hits.Add(m.FullName + " -> " + mr.FullName);
                        }
                    }
                }
            }

            Assert.That(scanned, Is.GreaterThan(0),
                "InventoryFold 内一个方法体都没扫到 —— 扫描面已失效(类型改名 / DLL 陈旧),拒以空集冒充绿");
            Assert.That(hits, Is.Empty, "fold 不得调用 21a 的求值器:" + string.Join(" | ", hits));
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

            string a = InventoryFold.Fold(baseline, _decoder).Projection.CanonicalForm();
            string b = InventoryFold.Fold(withExtra, _decoder).Projection.CanonicalForm();

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
