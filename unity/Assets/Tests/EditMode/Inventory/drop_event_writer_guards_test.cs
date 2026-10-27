// S20-14 · inventory story-005 —— 三支掉落写者 + 统一事务模板的**守卫 / 事务面**测试
// (Logic · EditMode)
//
// 权威来源:
//   design/gdd/inventory-and-items.md
//     规则二(消耗 / 归属路径表 · BL-1:铸造点 = `DropSpawned`,`DropClaimed` 只改归属不改发号)
//     规则六(:348-372)存取原子性(原子单位四行表)· 规则九(:529-560)跨栈部分消耗
//     Edge Cases(:613-621)超容量整件拒 / 中途失败整体回滚 / `qty` 畸形 ⇒ 零事件
//   story production/epics/inventory-items/story-005-atomicity-consumption-recovery-no-ack.md
//     AC-20-06(超容量整件拒)· AC-20-07(中途失败整体回滚)
//     AC-20-17(零 `ItemConsumed`/`ItemTransferred` 追加 Kind)· AC-20-25(无负确认信令)
//   ADR-005(主机唯一 Append / 发号)· ADR-006(载荷 ⊆ 整数域)· ADR-009 §七(拾取三段式)
//   ADR-024(Kind 单一真源)· ADR-029 §③(唯一编码路径 = `IPayloadEncoder`)
//
// ⚠️ **本文件不测什么**(划清边界,避免与别处重复记绿):
//   · 三支事件的**载荷形状 / 编码往返** —— 归 `Tests/EditMode/ItemDatabase/` 与 `Sim.Codec` 既有件;
//     本文件只在需要时**解码回读**做值面断言。
//   · `InventoryOf` fold 的重建语义 —— 归 `Tests/EditMode/Inventory/inventory_fold_test.cs`。
//   · 端到端真装配(23 / 29 接线)—— 归 `Tests/PlayMode/inventory_atomicity_test.cs`。
//
// ⚠️ **AC 记分(禁把「守卫面跑通」读成「四条 AC 全绿」)**:
//     ✅ AC-20-06  · 容量判据整件拒(本件 · 边界两侧各一例)
//     ✅ AC-20-07  · 校验是纯函数 ⇒ 任一不成立即返回 `None` 之外的裁决,**零写入**(本件)
//     ✅ AC-20-17  · 枚举面零追加 Kind(本件)
//     ✅ AC-20-25  · 拒绝路径零事件 + 枚举面零否定信令(本件)
//     ⚠️ 「整体回滚」的**端到端**半边(炮制真结算路径)归 PlayMode 件;本件只证模板本身的
//        两阶段语义(先全验,后写)。
//
// ⚠️ 零随机 / 零时间依赖 / 零写盘(不触 `FsWriteBoundaryGate`)。夹具数值全部标注「夹具值」。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.EditMode.Inventory
{
    /// <summary>掉落写者 + 事务模板的拒收 / 原子性守卫。</summary>
    [TestFixture]
    public sealed class DropEventWriterGuardsTest
    {
        // ── 夹具值(全部写死,零随机)──────────────────────────────────────
        private const long Tick = 700L;
        private const int Actor = 11;
        private const int OtherActor = 12;
        private const int ItemOrdinal = 5;
        private const int InitialQty = 10;

        private CountingSink _sink;
        private CountingIdAuthority _ids;
        private InMemoryBlobPool _pool;
        private PayloadEncoder _encoder;
        private PayloadDecoder _decoder;
        private DropEventWriter _writer;

        [SetUp]
        public void Setup()
        {
            _sink = new CountingSink();
            _ids = new CountingIdAuthority();
            _pool = new InMemoryBlobPool();
            _encoder = new PayloadEncoder(_pool);
            _decoder = new PayloadDecoder(_pool);
            _writer = new DropEventWriter(_sink, _encoder, _ids);
        }

        // ══════════════════════════════════════════════════════════════════
        // ① RecordSpawned —— 铸造点 + 入参域(规则二 BL-1)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_spawned_mintsExactlyOneId_andWritesOneEvent()
        {
            ItemInstanceId id = _writer.RecordSpawned(Tick, Cell(3, 0, -2), ItemOrdinal, InitialQty);

            Assert.AreEqual(1, _ids.ItemMints, "出生是铸造点 ⇒ 恰铸 1 个 id(规则二 BL-1)");
            Assert.AreEqual(1, _sink.Count, "恰 1 条 `DropSpawned`");
            Assert.AreEqual(id.Value, _ids.LastMinted, "返回的 id = 本次铸造的 id(不是凭空的)");

            SimEvent e = _sink.Events[0];
            Assert.AreEqual(EventKind.DropSpawned, e.Kind);
            Assert.AreEqual(PatientId.None.Value, e.Patient.Value,
                "世界流事件带 None 哨兵(ADR-007 §四)");

            DropSpawnedPayload p = _decoder.Decode<DropSpawnedPayload>(e.Kind, e.Payload);
            Assert.AreEqual(id.Value, p.InstanceId, "载荷的 instance_id = 铸造结果");
            Assert.AreEqual(ItemOrdinal, p.ItemKey);
            Assert.AreEqual(InitialQty, p.Qty);
            Assert.AreEqual(Cell(3, 0, -2), p.SpawnAnchor, "锚点是整数格,原样搬运");
        }

        [Test]
        public void test_spawned_negativeTick_throwsWithoutSideEffect()
        {
            // ⚠️ 双判据(异常类型 + 副作用零):只断异常类型无法区分「入口拒」与
            //    「跑到铸造之后才炸」—— 后者会留下一个空洞号(S20-13 的 G5/G6/G7 教训)。
            Assert.Throws<ArgumentOutOfRangeException>(
                () => _writer.RecordSpawned(-1L, Cell(0, 0, 0), ItemOrdinal, InitialQty));
            Assert.AreEqual(0, _sink.Count, "入参非法 ⇒ 零事件");
            Assert.AreEqual(0, _ids.ItemMints, "更要紧:不铸 id(铸了就是空洞号)");
        }

        [Test]
        public void test_spawned_negativeItemKey_throwsWithoutSideEffect()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => _writer.RecordSpawned(Tick, Cell(0, 0, 0), -1, InitialQty));
            Assert.AreEqual(0, _sink.Count);
            Assert.AreEqual(0, _ids.ItemMints);
        }

        [Test]
        public void test_spawned_zeroQty_throwsWithoutSideEffect()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => _writer.RecordSpawned(Tick, Cell(0, 0, 0), ItemOrdinal, 0));
            Assert.AreEqual(0, _sink.Count, "零份实例不该存在 ⇒ 零事件");
            Assert.AreEqual(0, _ids.ItemMints, "零份 ⇒ 更不该铸 id");
        }

        // ══════════════════════════════════════════════════════════════════
        // ② RecordClaimed —— 只改归属,不发号(规则二 BL-1)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_claimed_doesNotMint_andDoesNotRespawn()
        {
            ItemInstanceId id = _writer.RecordSpawned(Tick, Cell(1, 0, 1), ItemOrdinal, InitialQty);
            int mintsAfterSpawn = _ids.ItemMints;

            _writer.RecordClaimed(Tick + 1, id, Actor);

            Assert.AreEqual(mintsAfterSpawn, _ids.ItemMints,
                "`DropClaimed` **只改归属不改发号** —— 零新增铸造");
            Assert.AreEqual(2, _sink.Count, "出生 + 归属 = 2 条;归属**不**再发一条出生");
            Assert.AreEqual(EventKind.DropClaimed, _sink.Events[1].Kind);

            DropClaimedPayload p = _decoder.Decode<DropClaimedPayload>(
                _sink.Events[1].Kind, _sink.Events[1].Payload);
            Assert.AreEqual(id.Value, p.InstanceId, "归属事件指向**同一个**实例 id");
            Assert.AreEqual(Actor, p.Claimer);
            Assert.AreEqual(Tick + 1, p.Tick);
        }

        [Test]
        public void test_claimed_negativeClaimer_throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => _writer.RecordClaimed(Tick, new ItemInstanceId(0L), -1));
            Assert.AreEqual(0, _sink.Count, "负 claimer 是畸形输入 ⇒ 零事件");
        }

        // ══════════════════════════════════════════════════════════════════
        // ③ RecordDespawned —— 份数 / 理由域
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_despawned_zeroQty_throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => _writer.RecordDespawned(Tick, new ItemInstanceId(0L), 0, reason: 1));
            Assert.AreEqual(0, _sink.Count, "零份「减量」是无意义事件 ⇒ 不写比写更好");
        }

        [Test]
        public void test_despawned_negativeReason_throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => _writer.RecordDespawned(Tick, new ItemInstanceId(0L), 1, reason: -1));
            Assert.AreEqual(0, _sink.Count, "reason 是枚举序数 ⇒ 负值属畸形输入");
        }

        [Test]
        public void test_despawned_carriesQtyAndReasonVerbatim()
        {
            _writer.RecordDespawned(Tick, new ItemInstanceId(42L), qty: 3, reason: 1);

            Assert.AreEqual(1, _sink.Count);
            DropDespawnedPayload p = _decoder.Decode<DropDespawnedPayload>(
                _sink.Events[0].Kind, _sink.Events[0].Payload);
            Assert.AreEqual(42L, p.InstanceId);
            Assert.AreEqual(3, p.Qty, "份数原样搬运(20 不做 dose→portions 的换算)");
            Assert.AreEqual(1, p.Reason, "reason 原样搬运(档表归 R-2,本件不定义值)");
        }

        // ══════════════════════════════════════════════════════════════════
        // ④ 事务模板 —— 消耗前置五条(规则九 · AC-20-07 / AC-20-25)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_consume_happyPath_passes()
        {
            InventoryProjection proj = Project(
                new Row(1L, ItemOrdinal, 3, InitialQty, Actor));

            Assert.AreEqual(InventoryRejection.None,
                InventoryTransaction.ValidateConsumption(Lines((1L, 4)), Actor, proj));
        }

        [Test]
        public void test_consume_quantityNotPositive_rejected()
        {
            InventoryProjection proj = Project(new Row(1L, ItemOrdinal, 3, InitialQty, Actor));
            Assert.AreEqual(InventoryRejection.QuantityNotPositive,
                InventoryTransaction.ValidateConsumption(Lines((1L, 0)), Actor, proj));
        }

        [Test]
        public void test_consume_duplicateInstanceInOneRequest_rejected()
        {
            // ⚠️ 这条是「逐条校验」的结构性盲点:两条各自的 qty 都 ≤ 余量,合起来才超量。
            //    没有这条前置时,背包 10 份、请求 `(1,6)+(1,6)` 会双写成功。
            InventoryProjection proj = Project(new Row(1L, ItemOrdinal, 3, InitialQty, Actor));

            Assert.AreEqual(InventoryRejection.DuplicateInstanceInRequest,
                InventoryTransaction.ValidateConsumption(Lines((1L, 6), (1L, 6)), Actor, proj));
        }

        [Test]
        public void test_consume_instanceNotFound_rejected()
        {
            InventoryProjection proj = Project(new Row(1L, ItemOrdinal, 3, InitialQty, Actor));
            Assert.AreEqual(InventoryRejection.InstanceNotFound,
                InventoryTransaction.ValidateConsumption(Lines((99L, 1)), Actor, proj));
        }

        [Test]
        public void test_consume_notOwnedByActor_rejected_includingGroundInstance()
        {
            // 别人的实例
            InventoryProjection others = Project(new Row(1L, ItemOrdinal, 3, InitialQty, OtherActor));
            Assert.AreEqual(InventoryRejection.NotOwnedByActor,
                InventoryTransaction.ValidateConsumption(Lines((1L, 1)), Actor, others));

            // ⚠️ 地上的实例(`NoOwner`)同样拒 —— 「先捡再吃」是两步,不能合并成一步。
            InventoryProjection ground = Project(
                new Row(1L, ItemOrdinal, 3, InitialQty, InventoryProjection.NoOwner));
            Assert.AreEqual(InventoryRejection.NotOwnedByActor,
                InventoryTransaction.ValidateConsumption(Lines((1L, 1)), Actor, ground));
        }

        [Test]
        public void test_consume_quantityExceedsAvailable_rejected_boundaryIsInclusive()
        {
            InventoryProjection proj = Project(new Row(1L, ItemOrdinal, 3, 5, Actor));

            Assert.AreEqual(InventoryRejection.QuantityExceedsAvailable,
                InventoryTransaction.ValidateConsumption(Lines((1L, 6)), Actor, proj));

            // 边界:恰等余量是**合法**的(规则九不禁止扣光)
            Assert.AreEqual(InventoryRejection.None,
                InventoryTransaction.ValidateConsumption(Lines((1L, 5)), Actor, proj),
                "恰扣光余量合法 —— 上界是 `≤`,不是 `<`");
        }

        [Test]
        public void test_consume_nullArguments_throwInsteadOfBeingTreatedAsEmpty()
        {
            InventoryProjection proj = Project(new Row(1L, ItemOrdinal, 3, InitialQty, Actor));

            Assert.Throws<ArgumentNullException>(
                () => InventoryTransaction.ValidateConsumption(null, Actor, proj));
            Assert.Throws<ArgumentNullException>(
                () => InventoryTransaction.ValidateConsumption(Lines((1L, 1)), Actor, null));
        }

        [Test]
        public void test_consume_emptySet_isLegalNotAnError()
        {
            // 空集 = 「这次什么都不消耗」,是可表达的业务意图,不是编程错误。
            // (与 null 必须可分 —— 上一条夹具证明 null 会抛。)
            InventoryProjection proj = Project(new Row(1L, ItemOrdinal, 3, InitialQty, Actor));
            Assert.AreEqual(InventoryRejection.None,
                InventoryTransaction.ValidateConsumption(
                    Array.Empty<ConsumeLine>(), Actor, proj));
        }

        // ══════════════════════════════════════════════════════════════════
        // ⑤ 拾取前置 —— AC-20-06 整件拒绝(规则六原子单位表第一行)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_pickup_overCapacity_isRejectedWhole()
        {
            // cap = 10;当前载重 8;目标单件重 3、qty 1 ⇒ 8 + 3 = 11 > 10 ⇒ 装不下。
            InventoryProjection proj = Project(
                new Row(1L, ItemOrdinal, 3, 1, InventoryProjection.NoOwner));

            Assert.AreEqual(InventoryRejection.CapacityInsufficient,
                InventoryTransaction.ValidatePickup(
                    1L, Actor, currentCarryLoad: 8L, unitWeight: 3,
                    capacity: new CarryCapacity(10), projection: proj),
                "超容量 ⇒ 整件拒绝(不拆分、不留半件)");
        }

        [Test]
        public void test_pickup_exactlyAtCapacity_isAllowed()
        {
            // 边界:7 + 3 = 10 = cap ⇒ **恰等合法**(`CanCarry` 判据是 `≤`)。
            InventoryProjection proj = Project(
                new Row(1L, ItemOrdinal, 3, 1, InventoryProjection.NoOwner));

            Assert.AreEqual(InventoryRejection.None,
                InventoryTransaction.ValidatePickup(
                    1L, Actor, currentCarryLoad: 7L, unitWeight: 3,
                    capacity: new CarryCapacity(10), projection: proj),
                "恰等边界合法 —— 若这条转红,说明判据被写成了 `<`");
        }

        [Test]
        public void test_pickup_alreadyOwnedInstance_rejected()
        {
            // 已归属的实例再被「拾取」= 重复 claim,不是拾取。
            InventoryProjection proj = Project(new Row(1L, ItemOrdinal, 3, 1, OtherActor));

            Assert.AreEqual(InventoryRejection.NotOwnedByActor,
                InventoryTransaction.ValidatePickup(
                    1L, Actor, currentCarryLoad: 0L, unitWeight: 3,
                    capacity: new CarryCapacity(100), projection: proj));
        }

        [Test]
        public void test_pickup_missingInstance_rejected()
        {
            InventoryProjection proj = Project(
                new Row(1L, ItemOrdinal, 3, 1, InventoryProjection.NoOwner));
            Assert.AreEqual(InventoryRejection.InstanceNotFound,
                InventoryTransaction.ValidatePickup(
                    99L, Actor, currentCarryLoad: 0L, unitWeight: 3,
                    capacity: new CarryCapacity(100), projection: proj));
        }

        [Test]
        public void test_pickup_usesTheSingleCanCarryImplementation_verbatim()
        {
            // ⚠️ 这条不是「再测一遍 CanCarry」,而是钉**口径的一致性**:
            //    本类**不得**自己重写 `load + w×qty ≤ cap`。判据 = 对同一组输入,
            //    本类的裁决与 `CarryLoadSolver.CanCarry` **逐例相同**(含 qty > 1 的乘重)。
            //
            // 夹具:目标实例 qty = 4、单件重 3 ⇒ 候选重量 = 12。
            InventoryProjection proj = Project(
                new Row(1L, ItemOrdinal, 3, 4, InventoryProjection.NoOwner));
            var cap = new CarryCapacity(20);

            // 12 是「恰等」的锚点:8 + 12 = 20 ⇒ 可装;9 + 12 = 21 ⇒ 不可装。
            long[] loads = { 0L, 7L, 8L, 9L, 20L };
            foreach (long load in loads)
            {
                bool expected = CarryLoadSolver.CanCarry(load, 3, 4, cap);
                InventoryRejection actual = InventoryTransaction.ValidatePickup(
                    1L, Actor, load, 3, cap, proj);

                Assert.AreEqual(
                    expected ? InventoryRejection.None : InventoryRejection.CapacityInsufficient,
                    actual, $"load={load}:本类的容量裁决须与唯一实现点逐例相同");
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // ⑥ 落流前置 —— tick 单调(两阶段原子性的前提)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_ticksAreAppendable_monotonicBatch_passes()
        {
            var batch = new[] { Event(100L), Event(100L), Event(101L) };
            Assert.IsTrue(InventoryTransaction.TicksAreAppendable(batch, lastAppendedTick: 99L));
        }

        [Test]
        public void test_ticksAreAppendable_batchRegressingBelowStream_rejected()
        {
            // ⚠️ 本批 = **批内单调、但批首低于流基线** —— 这是本用例唯一能单独钉住
            // 「基线接续」那半判据的形态(2026-10-27 证伪轮 M11 实测教训):
            //   原稿写 `{100, 98}` + 基线 99 —— 它**自身内含回退**(98 < 100),
            //   于是把基线初值改成 `long.MinValue`(M11)后,仍被**批内比较**拦住返回 false,
            //   用例照绿 ⇒ 基线那半**零判别力**(S12-4 判定的假绿)。
            //   改成 `{98, 100}` 后:批内单调(100 ≥ 98),唯一能拒它的就是「98 < 基线 99」。
            var batch = new[] { Event(98L), Event(100L) };
            Assert.IsFalse(InventoryTransaction.TicksAreAppendable(batch, lastAppendedTick: 99L),
                "批首低于已入流 tick ⇒ 整批不可写(否则第 1 条进、第 2 条抛 = 半途)");
        }

        [Test]
        public void test_ticksAreAppendable_internalRegression_rejected()
        {
            var batch = new[] { Event(105L), Event(103L) };
            Assert.IsFalse(InventoryTransaction.TicksAreAppendable(batch, lastAppendedTick: 0L),
                "批**内**自身的回退同样要拒 —— 只看首条与流的关系会漏掉这种");
        }

        [Test]
        public void test_ticksAreAppendable_emptyBatch_passes()
        {
            Assert.IsTrue(InventoryTransaction.TicksAreAppendable(
                Array.Empty<SimEvent>(), lastAppendedTick: 99L),
                "空批无任何可违之序 ⇒ 真(与「空消耗集合法」同一口径)");
        }

        [Test]
        public void test_commit_emptyBatch_writesNothing()
        {
            InventoryTransaction.Commit(Array.Empty<SimEvent>(), _sink);
            Assert.AreEqual(0, _sink.Count, "空批 ⇒ 零写入(不是「写一条空事件」)");
        }

        [Test]
        public void test_commit_writesEveryEventInInputOrder()
        {
            var batch = new[] { Event(200L), Event(200L), Event(201L) };
            InventoryTransaction.Commit(batch, _sink);

            Assert.AreEqual(3, _sink.Count, "三条一条不少");
            Assert.AreEqual(200L, _sink.Events[0].Tick);
            Assert.AreEqual(201L, _sink.Events[2].Tick, "顺序 = 输入顺序(不重排)");
        }

        [Test]
        public void test_commit_nullArguments_throw()
        {
            Assert.Throws<ArgumentNullException>(
                () => InventoryTransaction.Commit(null, _sink));
            Assert.Throws<ArgumentNullException>(
                () => InventoryTransaction.Commit(Array.Empty<SimEvent>(), null));
        }

        // ══════════════════════════════════════════════════════════════════
        // ⑦ 拒绝路径零副作用(AC-20-25 的机制半边)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_rejectedPaths_writeNothing()
        {
            InventoryProjection proj = Project(new Row(1L, ItemOrdinal, 3, 5, Actor));
            int before = _sink.Count;

            // 逐个拒绝理由走一遍 —— 每一条都**不得**写任何事件(含「拒绝通知」类)。
            Assert.AreNotEqual(InventoryRejection.None, InventoryTransaction.ValidateConsumption(
                Lines((1L, 99)), Actor, proj));
            Assert.AreNotEqual(InventoryRejection.None, InventoryTransaction.ValidateConsumption(
                Lines((1L, 0)), Actor, proj));
            Assert.AreNotEqual(InventoryRejection.None, InventoryTransaction.ValidatePickup(
                1L, Actor, currentCarryLoad: 1000L, unitWeight: 1,
                capacity: new CarryCapacity(10), projection: proj));

            Assert.AreEqual(before, _sink.Count,
                "校验是纯函数 —— 拒绝路径零写入(AC-20-25:被拒意图世界流零新增)");
        }

        [Test]
        public void test_noNegativeSignalEventKindsExistInEnum()
        {
            // AC-20-25 的**枚举面**:三类否定信令 Kind 不得存在。
            // ⚠️ 用 `Enum.TryParse` 按名字查,而不是 grep 源码 —— 后者会被注释命中。
            string[] forbidden = { "PickupRejected", "IntentAck", "ConsumeFailed" };
            foreach (string name in forbidden)
            {
                Assert.IsFalse(Enum.TryParse(name, out EventKind _),
                    $"`EventKind.{name}` 不得存在 —— AC-20-25 禁否定信令事件");
            }
        }

        [Test]
        public void test_noConsumptionKindsExist()
        {
            // AC-20-17:扣减不由独立 Kind 表达,而是 `Craft.ActualConsumed` /
            // `StructurePlaced.cost(m)` / `DropDespawned` 的唯一推出(OQ-20-1)。
            string[] forbidden = { "ItemConsumed", "ItemTransferred" };
            foreach (string name in forbidden)
            {
                Assert.IsFalse(Enum.TryParse(name, out EventKind _),
                    $"`EventKind.{name}` 不得存在 —— AC-20-17 零追加 Kind");
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // 夹具构造
        // ══════════════════════════════════════════════════════════════════

        /// <summary>投影行 —— (实例 id, 物品 ordinal, 品级, 份数, 持有者)。</summary>
        private readonly struct Row
        {
            public readonly long Id;
            public readonly int Ordinal;
            public readonly int Quality;
            public readonly int Qty;
            public readonly int Owner;

            public Row(long id, int ordinal, int quality, int qty, int owner)
            {
                Id = id; Ordinal = ordinal; Quality = quality; Qty = qty; Owner = owner;
            }
        }

        private static WorldPos Cell(int x, int y, int z) => new WorldPos(x, y, z);

        private static ConsumeLine[] Lines(params (long Id, int Qty)[] rows)
        {
            var arr = new ConsumeLine[rows.Length];
            for (int i = 0; i < rows.Length; i++) arr[i] = new ConsumeLine(rows[i].Id, rows[i].Qty);
            return arr;
        }

        /// <summary>造一条只用于 tick 序断言的占位事件(载荷 `default`,不参与解码)。</summary>
        private static SimEvent Event(long tick)
            => new SimEvent(tick, PatientId.None, -1, EventKind.DropDespawned, default);

        /// <summary>
        /// 用**事件流 + 真 <see cref="InventoryFold"/>** 造投影 —— 不手搓投影对象:
        /// `InventoryProjection` 的构造器是 internal,且手搓的投影可能与 fold 的真实语义分叉
        /// (那样测的就是「我以为的投影」而不是「20 的投影」)。
        /// </summary>
        /// <remarks>
        /// 每行发两条事件:`DropSpawned`(建条目 + 定 qty/item_key)+ `ResourceHarvested`
        /// (绑品级 —— 缺它 fold 会报 `InstanceWithoutQualitySource` 缺口);
        /// 非地面行再补一条 `DropClaimed`(迁 owner)。三段都用真编码器入池,
        /// 保证 fold 走的是与生产同一条解码路径。
        /// </remarks>
        private InventoryProjection Project(params Row[] rows)
        {
            var events = new List<SimEvent>();
            long tick = 1L;      // 夹具 tick 基址;逐条自增,保持流内单调

            foreach (Row r in rows)
            {
                var spawn = new DropSpawnedPayload(r.Id, Cell(0, 0, 0), r.Ordinal, r.Qty);
                events.Add(new SimEvent(tick++, PatientId.None, -1, EventKind.DropSpawned,
                    _encoder.Encode(EventKind.DropSpawned, spawn)));

                var harvest = new ResourceHarvestedPayload(
                    r.Id, nodeId: 0, gatherSeq: 0, qty: r.Qty, outQuality: r.Quality);
                events.Add(new SimEvent(tick++, PatientId.None, -1, EventKind.ResourceHarvested,
                    _encoder.Encode(EventKind.ResourceHarvested, harvest)));

                if (r.Owner != InventoryProjection.NoOwner)
                {
                    long claimTick = tick++;
                    var claim = new DropClaimedPayload(r.Id, r.Owner, claimTick);
                    events.Add(new SimEvent(claimTick, PatientId.None, -1, EventKind.DropClaimed,
                        _encoder.Encode(EventKind.DropClaimed, claim)));
                }
            }

            return InventoryFold.Fold(events, _decoder).RequireComplete();
        }

        // ── 桩 ─────────────────────────────────────────────────────────────

        /// <summary>计数型 sink —— `Count` 是「写了几条」的直接观测。</summary>
        private sealed class CountingSink : IEventSink
        {
            private readonly List<SimEvent> _events = new List<SimEvent>();

            public int Count => _events.Count;
            public IReadOnlyList<SimEvent> Events => _events;

            /// <summary>按 `(Tick, Patient)` 组内自增发号(镜像 `EventStream` 的口径)。</summary>
            public void Append(in SimEvent e)
            {
                long seq = e.Seq;
                if (seq < 0)
                {
                    seq = 0;
                    for (int i = _events.Count - 1; i >= 0; i--)
                    {
                        if (_events[i].Tick == e.Tick &&
                            _events[i].Patient.Value == e.Patient.Value)
                        {
                            seq = _events[i].Seq + 1;
                            break;
                        }
                    }
                }
                _events.Add(new SimEvent(e.Tick, e.Patient, seq, e.Kind, e.Payload));
            }
        }

        /// <summary>计数型发号器 —— `ItemMints` / `LastMinted` 是铸造行为的直接观测。</summary>
        private sealed class CountingIdAuthority : IIdAuthority
        {
            private int _patients;
            private int _items;

            public int ItemMints => _items;
            public long LastMinted => _items - 1L;

            public PatientId NextPatientId() => new PatientId(_patients++);
            public ItemInstanceId NextItemInstanceId() => new ItemInstanceId(_items++);
        }
    }
}
