// S20-19 · 42 建造发起方接线 —— **建造编排器**夹具(AC B-1 / B-2 / B-3 / B-4 / B-6 / B-7 / B-9)
//
// 权威来源:
//   design/gdd/modular-building.md 规则二(五条件)· 规则七(:124 `cost(m)`）· 规则九(拆除返还)
//   design/gdd/inventory-and-items.md 规则六(建造扣料 = 一次 `StructurePlaced` 的 `cost(m)`）
//   design/gdd/skeuomorphic-ui.md §建造交互(建造 = **世界内操作态**,不占模态）
//   ADR-009 §七(意图 → 主机当下判定 → 效果进流)· ADR-013 §十(`ModalId` 闭集 7 员)
//   ADR-020 §四(玩家位移纯表现态;本层只读**已量化**整数格)
//   `production/sprints/sprint-20.md` §十 S20-19 的 AC B-1…B-9
//
// ─────────────────────────────────────────────────────────────────────────────
// 判据为什么这么写
// ─────────────────────────────────────────────────────────────────────────────
// · **零副作用**(B-2 / B-6)不能只看「结果状态没变」——「先调写者再判」在结果上
//   可能与「先判后写」完全一样。故本件用两个**调用计数代理**:
//     ① 世界流条数(`_stream.Count`);
//     ② `StructureWriter.PeekNextId` —— `Place` 每成功一次就递增 id 高水位,
//        它没动 ⇒ 写者**真的没被调用**,而不是「调了但被回滚」。
// · **B-4 的后半句**(卡面原文「消耗事件与 `StructurePlaced` 同一事务落流」)已作废:
//   `InventoryFold.ApplyBuildCost` 已按 `cost(m)` 从 `StructurePlaced` **反推**扣减
//   (2026-10-27 R16 甲案)⇒ 再写消耗事件就是**双重扣减**。现判据 = **恰一条**
//   `StructurePlaced`、**零** `DropDespawned`,且同一批事件喂给 fold 时材料**恰少 cost(m)**。
// · **B-3 的端到端是真的端到端**:`BuildController`(生产编排)→ `StructureWriter`
//   (生产编码)→ `SimEvent` 流 → `World`(生产占用表)。没有一处是测试手搓的字节或手搓的 op。
//
// ⚠️ 零随机 / 零时间依赖 / 零写盘;id、数量、格坐标全部写死。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Presentation.Building;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;
using NUnit.Framework;
using UnityEngine;

namespace DaYiJingCheng.Tests.ModularBuilding
{
    /// <summary>S20-19:建造编排器(判定 → 选料 → 落流)与它的生产装配点。</summary>
    [TestFixture]
    public class BuildControllerTest
    {
        // 夹具值(非 GDD 数值):owner 42 = 唯一玩家;模块 7 = 两格 L 形足迹,cost 4。
        private const int Owner = 42;
        private const int Module = 7;
        private const int BuildItemKey = 0;
        private const int Cost = 4;
        private const int Size = 5;

        private World _world;
        private InMemoryBlobPool _pool;
        private PayloadEncoder _encoder;
        private PayloadDecoder _decoder;
        private TestModuleCatalog _catalog;
        private BuildSlotCatalog _slots;
        private List<SimEvent> _inventoryEvents;
        private List<SimEvent> _stream;
        private StructureWriter _writer;
        private BuildOwnerSlot _ownerSlot;
        private BuildController _controller;
        private InventoryProjection _projection;

        [SetUp]
        public void Setup()
        {
            // ── 世界:全 walkable 的 Size×1×Size 平面 ──
            var terrain = new TerrainCell[Size, 1, Size];
            for (int x = 0; x < Size; x++)
                for (int z = 0; z < Size; z++)
                    terrain[x, 0, z] = new TerrainCell(true, 0);

            int total = Size * 1 * Size;
            var poi = new int[total];
            var resource = new int[total];
            for (int i = 0; i < total; i++)
            {
                poi[i] = -1;
                resource[i] = -1;
            }

            // ── 槽位:3×3 全 Decor(`IsInBuildSlotRegion` = 「有槽位即区域内」)──
            _slots = new BuildSlotCatalog();
            for (int x = 0; x < 3; x++)
                for (int z = 0; z < 3; z++)
                    _slots.RegisterSlot(new BuildSlot(new WorldPos(x, 0, z),
                        BitMask.Single(SlotType.Decor)));

            _world = new World(new WorldGeometry(terrain, poi, resource), _slots);

            _pool = new InMemoryBlobPool();      // 同一池既作编码写面,又作解码读面
            _encoder = new PayloadEncoder(_pool);
            _decoder = new PayloadDecoder(_pool);

            _catalog = new TestModuleCatalog();
            // 朝向 0 → [(0,0),(1,0)];朝向 90 → [(0,0),(0,1)](整数四向旋转,F-23-2b)
            _catalog.RegisterModule(new ModuleDefinition(
                moduleId: Module,
                slotType: SlotType.Decor,
                localOccupancy: new List<WorldPos> { new WorldPos(0, 0, 0), new WorldPos(1, 0, 0) },
                validOrientations: new[] { 0, 90, 180, 270 },
                cost: Cost,
                buildItemKey: BuildItemKey,
                refundable: 1));

            _stream = new List<SimEvent>();
            var sink = new CapturingSink(_stream);
            _writer = new StructureWriter(
                sink, _world.Structures, _encoder, _catalog, _world,
                DaYiJingCheng.Tests.TestDropWriter.Create(sink));

            _inventoryEvents = new List<SimEvent>();
            _projection = ProjectionFor(Owner, qty: 8);

            _ownerSlot = new BuildOwnerSlot();
            _ownerSlot.Bind(Owner);

            _controller = new BuildController(
                new PlaceableChecker(_slots, _catalog,
                    new BuildStockQuery(() => _projection, _catalog, _ownerSlot),
                    _world, new AlwaysEmptyPresence()),
                new DemolishChecker(_world.Structures, _world, new AlwaysEmptyPresence(), _catalog),
                new ModifiableChecker(_slots, _world, _catalog, _world.Structures),
                _writer,
                _world.Structures,
                _catalog,
                () => _projection,
                () => 100L,
                _ownerSlot);
        }

        // ══════════════════════════════════════════════════════════════════
        // 夹具工具
        // ══════════════════════════════════════════════════════════════════

        private SimEvent Ev<T>(EventKind kind, T payload, long tick, long seq = 0L) where T : struct
            => new SimEvent(tick, PatientId.None, seq, kind, _encoder.Encode(kind, payload));

        /// <summary>造一份 owner 名下的库存(真实 fold 产出,不是手搓投影)。</summary>
        private InventoryProjection ProjectionFor(int owner, int qty)
        {
            const long id = 1L;
            _inventoryEvents.Clear();
            _inventoryEvents.Add(Ev(EventKind.DropSpawned,
                new DropSpawnedPayload(id, new WorldPos(4, 0, 4), BuildItemKey, qty), 1, 0));
            _inventoryEvents.Add(Ev(EventKind.ResourceHarvested,
                new ResourceHarvestedPayload(id, nodeId: 100, gatherSeq: 1, qty: qty, outQuality: 1), 1, 1));
            _inventoryEvents.Add(Ev(EventKind.DropClaimed,
                new DropClaimedPayload(id, owner, 1), 2, 0));

            return InventoryFold.Fold(_inventoryEvents, _decoder, InventoryFoldContext.None)
                                .RequireComplete();
        }

        /// <summary>owner 名下的总份数(从投影现算 —— 不缓存第二真值)。</summary>
        private static int HeldQty(InventoryProjection projection, int owner)
        {
            int sum = 0;
            foreach (long id in projection.InstancesOf(owner))
                if (projection.TryGet(id, out InventoryRow row)) sum += row.Qty;
            return sum;
        }

        private int StreamCount => _stream.Count;

        private long WriterHighWater => _writer.PeekNextId;

        // ══════════════════════════════════════════════════════════════════
        // AC B-2 · 非法放置**零副作用**
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_acB2_placeOnNonSlotCell_returnsSlotNotFoundWithZeroSideEffects()
        {
            // Arrange:槽位只注册在 x,z ∈ [0,2];(4,0,4) 不是槽位
            long highWaterBefore = WriterHighWater;
            int streamBefore = StreamCount;

            // Act
            PlaceableResult r = _controller.TryPlace(Module, new WorldPos(4, 0, 4), 0);

            // Assert:判定不过 ⇒ 写者**零调用**(高水位没动),世界流**零新增**
            Assert.That(r, Is.EqualTo(PlaceableResult.SlotNotFound));
            Assert.That(StreamCount, Is.EqualTo(streamBefore), "拒绝路径不得落任何事件");
            Assert.That(WriterHighWater, Is.EqualTo(highWaterBefore),
                "`PeekNextId` 未动 ⇒ `StructureWriter.Place` 真的没被调用(而非「调了又回滚」)");
        }

        [Test]
        public void test_acB2_placeOnOccupiedCell_returnsOccupiedWithZeroSideEffects()
        {
            // Arrange:先合法放一个(占 (0,0,0) 与 (1,0,0))
            Assert.That(_controller.TryPlace(Module, new WorldPos(0, 0, 0), 0),
                Is.EqualTo(PlaceableResult.Success));

            long highWaterBefore = WriterHighWater;
            int streamBefore = StreamCount;

            // Act:在 (1,0,0) 上再放一个 ⇒ 足迹与已放置的重叠
            PlaceableResult r = _controller.TryPlace(Module, new WorldPos(1, 0, 0), 0);

            // Assert
            Assert.That(r, Is.EqualTo(PlaceableResult.Occupied));
            Assert.That(StreamCount, Is.EqualTo(streamBefore));
            Assert.That(WriterHighWater, Is.EqualTo(highWaterBefore));
        }

        [Test]
        public void test_acB2_placeWithUnboundOwner_isInsufficientWithZeroSideEffects()
        {
            // Arrange:归属槽未绑定(玩家 actorId 尚未铸造的启动窗口)
            var unbound = new BuildOwnerSlot();
            var controller = new BuildController(
                new PlaceableChecker(_slots, _catalog,
                    new BuildStockQuery(() => _projection, _catalog, unbound),
                    _world, new AlwaysEmptyPresence()),
                new DemolishChecker(_world.Structures, _world, new AlwaysEmptyPresence(), _catalog),
                new ModifiableChecker(_slots, _world, _catalog, _world.Structures),
                _writer, _world.Structures, _catalog, () => _projection, () => 100L, unbound);

            long highWaterBefore = WriterHighWater;
            int streamBefore = StreamCount;

            // Act
            PlaceableResult r = controller.TryPlace(Module, new WorldPos(0, 0, 0), 0);

            // Assert:fail-closed —— 不知道扣谁的 ⇒ 拒绝,不静默放行
            Assert.That(r, Is.EqualTo(PlaceableResult.InsufficientStock));
            Assert.That(StreamCount, Is.EqualTo(streamBefore));
            Assert.That(WriterHighWater, Is.EqualTo(highWaterBefore));
        }

        // ══════════════════════════════════════════════════════════════════
        // AC B-3 · 合法放置端到端
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_acB3_legalPlace_appendsOneStructurePlacedAndBlocksWalkability()
        {
            // Arrange:放置前四格全可走
            Assert.That(_world.IsEffectivelyWalkable(new WorldPos(0, 0, 0)), Is.True);
            Assert.That(_world.IsEffectivelyWalkable(new WorldPos(1, 0, 0)), Is.True);

            // Act
            PlaceableResult r = _controller.TryPlace(Module, new WorldPos(0, 0, 0), 0);

            // Assert ①:判定通过
            Assert.That(r, Is.EqualTo(PlaceableResult.Success));

            // Assert ②:世界流恰 +1,且是 StructurePlaced
            Assert.That(StreamCount, Is.EqualTo(1));
            Assert.That(_stream[0].Kind, Is.EqualTo(EventKind.StructurePlaced));

            // Assert ③:F-23-1 `EffectiveWalkable = Nav.walkable ∧ ¬slot_occupied`
            //           —— 足迹两格都不可走(非锚点格也要生效,否则足迹就是摆设)
            Assert.That(_world.IsEffectivelyWalkable(new WorldPos(0, 0, 0)), Is.False,
                "锚点格必须被占");
            Assert.That(_world.IsEffectivelyWalkable(new WorldPos(1, 0, 0)), Is.False,
                "**非锚点足迹格**也必须被占 —— 只占锚点 = 足迹未生效");
            Assert.That(_world.IsEffectivelyWalkable(new WorldPos(2, 0, 0)), Is.True,
                "足迹外的格不得被误占");

            // Assert ④:注册表按**锚点**反查(非锚点足迹格返回 -1 —— 这是 `GetInstanceAt`
            //           的既定语义,不是缺陷:足迹格的反查走占用表,不走实例表)
            long id = _controller.StructureIdAt(new WorldPos(0, 0, 0));
            Assert.That(id, Is.GreaterThan(0));
            Assert.That(_controller.StructureIdAt(new WorldPos(1, 0, 0)), Is.EqualTo(-1),
                "`StructureInstanceRegistry.GetInstanceAt` 以**锚点**为键 —— 非锚点足迹格不命中");
        }

        // ══════════════════════════════════════════════════════════════════
        // AC B-4 · 材料**先验后写**
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_acB4_insufficientMaterial_rejectsWithoutTouchingWriter()
        {
            // Arrange:只有 3 份,造一个要 4 份
            _projection = ProjectionFor(Owner, qty: Cost - 1);
            long highWaterBefore = WriterHighWater;
            int streamBefore = StreamCount;

            // Act
            PlaceableResult r = _controller.TryPlace(Module, new WorldPos(0, 0, 0), 0);

            // Assert:不足 ⇒ 整手拒绝,**零事件**、写者零调用
            Assert.That(r, Is.EqualTo(PlaceableResult.InsufficientStock));
            Assert.That(StreamCount, Is.EqualTo(streamBefore));
            Assert.That(WriterHighWater, Is.EqualTo(highWaterBefore),
                "材料不足时不得先写后判 —— 高水位未动证明 `Place` 未被调用");
            Assert.That(_world.IsEffectivelyWalkable(new WorldPos(0, 0, 0)), Is.True,
                "被拒的放置不得留下任何占用");
        }

        [Test]
        public void test_acB4_sufficientMaterial_appendsOnePlacedAndZeroDespawned()
        {
            // Arrange:8 份,够造两个
            long highWaterBefore = WriterHighWater;

            // Act
            PlaceableResult r = _controller.TryPlace(Module, new WorldPos(0, 0, 0), 0);

            // Assert:建造的唯一流事件 = 一条 `StructurePlaced`
            Assert.That(r, Is.EqualTo(PlaceableResult.Success));
            Assert.That(StreamCount, Is.EqualTo(1));
            Assert.That(_stream[0].Kind, Is.EqualTo(EventKind.StructurePlaced));
            Assert.That(_stream.Count(e => e.Kind == EventKind.DropDespawned), Is.EqualTo(0),
                "建造**不得**写 `DropDespawned` —— fold 的 `ApplyBuildCost` 已按 cost(m) 扣料,"
                + "再写一遍就是双重扣减(玩家建一个花两份料)");
            Assert.That(WriterHighWater, Is.EqualTo(highWaterBefore + 1),
                "成功路径上写者恰被调用一次");
        }

        [Test]
        public void test_acB4_foldDeductsCostFromSameStream()
        {
            // Arrange:8 份
            Assert.That(HeldQty(_projection, Owner), Is.EqualTo(8));

            // Act
            Assert.That(_controller.TryPlace(Module, new WorldPos(0, 0, 0), 0),
                Is.EqualTo(PlaceableResult.Success));

            // 把**同一批**事件(库存 + 世界流)喂回 fold —— 这是读档 / 投影重建的真实路径
            var all = new List<SimEvent>(_inventoryEvents);
            all.AddRange(_stream);
            var ctx = new InventoryFoldContext(default, default, _catalog, Owner);
            InventoryFoldResult folded = InventoryFold.Fold(all, _decoder, ctx);

            // Assert ①:fold 能归因(无缺口)—— 证明「材料充足性」这一手判据在 fold 侧同样成立
            Assert.That(folded.IsComplete, Is.True,
                "fold 报缺口 ⇒ 写前判定与重建判定不同源:" + string.Join(" | ", folded.Gaps));

            // Assert ②:材料恰少 cost(m),且**只**少这一份(双重扣减会让它少 2×cost)
            Assert.That(HeldQty(folded.Projection, Owner), Is.EqualTo(8 - Cost),
                "扣减量必须恰为 cost(m) —— 少 2×cost 就是 `StructurePlaced` 与 `DropDespawned` 双扣");
        }

        // ══════════════════════════════════════════════════════════════════
        // AC B-6 · 拆除
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_acB6_remove_appendsRemovedAndRefundAndFreesCells()
        {
            // Arrange
            Assert.That(_controller.TryPlace(Module, new WorldPos(0, 0, 0), 0),
                Is.EqualTo(PlaceableResult.Success));
            long id = _controller.StructureIdAt(new WorldPos(0, 0, 0));
            Assert.That(_world.IsEffectivelyWalkable(new WorldPos(0, 0, 0)), Is.False);

            // Act
            DemolishResult r = _controller.TryRemove(id);

            // Assert ①:判定通过
            Assert.That(r, Is.EqualTo(DemolishResult.Success));

            // Assert ②:落流两条 —— `StructureRemoved` + 返还 `DropSpawned`(F-23-3)
            Assert.That(_stream.Count(e => e.Kind == EventKind.StructureRemoved), Is.EqualTo(1));
            Assert.That(_stream.Count(e => e.Kind == EventKind.DropSpawned), Is.EqualTo(1),
                "拆除必须发返还掉落(不静默吞返还,AC-23-16)");

            // Assert ③:占用格被释放
            Assert.That(_world.IsEffectivelyWalkable(new WorldPos(0, 0, 0)), Is.True);
            Assert.That(_world.IsEffectivelyWalkable(new WorldPos(1, 0, 0)), Is.True);
            Assert.That(_controller.StructureIdAt(new WorldPos(0, 0, 0)), Is.LessThanOrEqualTo(0));
        }

        [Test]
        public void test_acB6_removeUnknownId_returnsNotFoundWithZeroSideEffects()
        {
            int streamBefore = StreamCount;

            // Act
            DemolishResult r = _controller.TryRemove(structureId: 999);

            // Assert
            Assert.That(r, Is.EqualTo(DemolishResult.StructureNotFound));
            Assert.That(StreamCount, Is.EqualTo(streamBefore), "拒绝路径不得落任何事件");
        }

        /// <summary>
        /// B-6 的「判定器拒绝 ⇒ 零副作用」半边 —— **可证伪的那一半**。
        /// <para>⚠️ 为什么不能只靠 `removeUnknownId`:那条夹具挡在
        /// <see cref="StructureWriter.Remove"/> **自己的兜底**前面 —— 未知 id 时写者
        /// `return false` 且**不发事件** ⇒「判定器放行 + 写者兜底」与「判定器拒绝」
        /// 在返回值与副作用上**都不可分**,它对判定器零判别力。</para>
        /// <para>⇒ 必须让判定器挡在一条**存在且可拆**的结构前面。`DemolishChecker` 有两条
        /// 可达拒绝分支,本夹具走 `EntityOnCell`(另一条 `ShellNotRemovable` 需
        /// `SlotType.Shell` 模块,本夹具目录里没有)。</para>
        /// </summary>
        [Test]
        public void test_acB6_removeWithEntityOnCell_returnsEntityOnCellWithZeroSideEffects()
        {
            // Arrange ①:先合法放一个(占 (0,0,0) 与 (1,0,0))—— 用「无实体」存在性桩
            Assert.That(_controller.TryPlace(Module, new WorldPos(0, 0, 0), 0),
                Is.EqualTo(PlaceableResult.Success));
            long id = _controller.StructureIdAt(new WorldPos(0, 0, 0));
            int streamBefore = StreamCount;

            // Arrange ②:**只换 presence 一支** —— 结构真的存在,只是格上有人
            var blocked = new BuildController(
                new PlaceableChecker(_slots, _catalog,
                    new BuildStockQuery(() => _projection, _catalog, _ownerSlot),
                    _world, new AlwaysEmptyPresence()),
                new DemolishChecker(_world.Structures, _world, new PresenceOnCell(), _catalog),
                new ModifiableChecker(_slots, _world, _catalog, _world.Structures),
                _writer, _world.Structures, _catalog, () => _projection, () => 100L, _ownerSlot);

            // Act
            DemolishResult r = blocked.TryRemove(id);

            // Assert ①:判定器真的挡了(而不是写者兜底成 StructureNotFound)
            Assert.That(r, Is.EqualTo(DemolishResult.EntityOnCell));

            // Assert ②:写者**零调用** —— 流里既无 `StructureRemoved` 也无返还 `DropSpawned`
            Assert.That(StreamCount, Is.EqualTo(streamBefore),
                "拒绝路径不得落任何事件(尤其不得发返还 `DropSpawned`)");
            Assert.That(_stream.Count(e => e.Kind == EventKind.StructureRemoved), Is.EqualTo(0));

            // Assert ③:拒绝不得产生「半拆」状态 —— 实例仍在、占用格仍被占
            Assert.That(_world.Structures.TryGet(id, out _), Is.True,
                "被拒的拆除不得移除结构实例");
            Assert.That(_world.IsEffectivelyWalkable(new WorldPos(0, 0, 0)), Is.False,
                "被拒的拆除不得释放占用格");
        }

        // ══════════════════════════════════════════════════════════════════
        // 旋转(幽灵循环的落点)—— `TryRotateBy` 的定义域
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_rotateBy90_flipsFootprintAndAppendsOneModified()
        {
            // Arrange
            Assert.That(_controller.TryPlace(Module, new WorldPos(0, 0, 0), 0),
                Is.EqualTo(PlaceableResult.Success));
            long id = _controller.StructureIdAt(new WorldPos(0, 0, 0));
            Assert.That(_world.IsEffectivelyWalkable(new WorldPos(1, 0, 0)), Is.False);

            // Act
            ModifiableResult r = _controller.TryRotateBy(id, 90);

            // Assert:足迹从 [(0,0),(1,0)] 变成 [(0,0),(0,1)]
            Assert.That(r, Is.EqualTo(ModifiableResult.Success));
            Assert.That(_stream.Count(e => e.Kind == EventKind.StructureModified), Is.EqualTo(1));
            Assert.That(_world.IsEffectivelyWalkable(new WorldPos(1, 0, 0)), Is.True,
                "旧足迹格必须释放");
            Assert.That(_world.IsEffectivelyWalkable(new WorldPos(0, 0, 1)), Is.False,
                "新足迹格必须占用");
        }

        [Test]
        public void test_rotateByIllegalDelta_throws()
        {
            // Arrange
            Assert.That(_controller.TryPlace(Module, new WorldPos(0, 0, 0), 0),
                Is.EqualTo(PlaceableResult.Success));
            long id = _controller.StructureIdAt(new WorldPos(0, 0, 0));

            // Act + Assert:四向整数旋转只接受 90/180/270(0 不是「增量」)
            Assert.Throws<ArgumentException>(() => _controller.TryRotateBy(id, 45));
            Assert.Throws<ArgumentException>(() => _controller.TryRotateBy(id, 0));
        }

        // ══════════════════════════════════════════════════════════════════
        // AC B-7 · 建造**不占模态**(`ModalId` 闭集 7 员里没有建造)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_acB7_buildPath_hasNoModalDependency()
        {
            // 判据面 = 建造链条的**全部**生产文件(编排 + 选料 + 归属 + 输入驱动/读取)
            string[] files =
            {
                "unity/Assets/Gameplay.Presentation/Building/BuildController.cs",
                "unity/Assets/Gameplay.Presentation/Building/BuildStockQuery.cs",
                "unity/Assets/Gameplay.Presentation/Building/BuildOwnerSlot.cs",
                "unity/Assets/Gameplay.Boot/BuildInputDriver.cs",
                "unity/Assets/Gameplay.Boot/BuildInputReader.cs",
            };

            string root = RepoRoot();
            var hits = new List<string>();
            foreach (string rel in files)
            {
                string path = Path.Combine(root, rel);
                Assert.That(File.Exists(path), Is.True, $"判据面文件不存在:{rel}(被搬走?)");

                string code = StripComments(File.ReadAllText(path));
                foreach (string token in new[] { "IModalState", "ModalId" })
                    if (code.Contains(token)) hits.Add($"{rel}: {token}");
            }

            Assert.That(hits, Is.Empty,
                "建造 = 世界内操作态,不占 `ModalId` 闭集(ADR-013 §十)⇒ 建造链条零模态依赖。"
                + "命中:" + string.Join(" | ", hits));
        }

        // ══════════════════════════════════════════════════════════════════
        // AC B-1 · 量化同源(建造链条不自己做 float → 格)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_acB1_buildPath_doesNotReQuantizeCells()
        {
            // PRE-S19-C 约束 ②:格量化只有一条路 —— `CellTransitionDetector.CellFromPosition`。
            // 建造链条收的是**已量化**的 `WorldPos`(由 `BuildInputDriver` 的
            // `Func<WorldPos>` 供给),故本层出现 `Mathf.*ToInt` 就是第二套量化。
            string[] files =
            {
                "unity/Assets/Gameplay.Presentation/Building/BuildController.cs",
                "unity/Assets/Gameplay.Presentation/Building/BuildStockQuery.cs",
                "unity/Assets/Gameplay.Boot/BuildInputDriver.cs",
            };

            string root = RepoRoot();
            var hits = new List<string>();
            foreach (string rel in files)
            {
                string path = Path.Combine(root, rel);
                Assert.That(File.Exists(path), Is.True, $"判据面文件不存在:{rel}(被搬走?)");

                string code = StripComments(File.ReadAllText(path));
                foreach (string token in new[] { "Mathf.FloorToInt", "Mathf.RoundToInt", "Mathf.CeilToInt" })
                    if (code.Contains(token)) hits.Add($"{rel}: {token}");
            }

            Assert.That(hits, Is.Empty,
                "建造链条不得自建 float → 格量化(会与 `CellTransitionDetector` 分歧,"
                + "玩家看到 A 格、主机判定 B 格)。命中:" + string.Join(" | ", hits));
        }

        // ══════════════════════════════════════════════════════════════════
        // AC B-9 · 判定器的**生产构造点** 0 → 1
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_acB9_assembleWithWorld_yieldsBuildController()
        {
            // Act:走**生产装配入口**(不是测试手搭)
            CompositionRootServices services = CompositionRoot.Assemble(world: MinimalWorldFactory.Create());

            // Assert:建造链条在装配期成型 —— 此前三个判定器的生产构造点是 0
            Assert.That(services.BuildController, Is.Not.Null,
                "`world != null` 时装配必须产出 `BuildController`");
            Assert.That(services.BuildController.OwnerSlot.IsBound, Is.False,
                "装配期玩家尚未铸造 ⇒ 归属槽未绑定(由 `BootRoot` 后置绑定)");
        }

        [Test]
        public void test_acB9_assembleWithoutWorld_yieldsNullBuildController()
        {
            // Act:不给世界(既有 100+ 调用方的形态)
            CompositionRootServices services = CompositionRoot.Assemble();

            // Assert:建造链条整条不存在 —— 既有调用方行为零变化
            Assert.That(services.BuildController, Is.Null,
                "`world == null` 时不得凭空造一个没有世界可写的建造器");
        }

        /// <summary>
        /// B-9 反例半边的**可证伪路径**:给了注册表、但**没给世界**。
        /// <para>⚠️ 为什么不靠 `assembleWithoutWorld` 一条:`buildController` 的赋值在
        /// **两层守卫的合取**之内 —— `:847 structures != null` ∧ `:874 world != null`。
        /// 裸 `Assemble()` 时两层**同时为假**,单独停用任一层都不改变可观察量
        /// ⇒ 那条夹具对**任何单层守卫**都零判别力(不是它错,是合取使然)。</para>
        /// <para>本条让第一层为真、第二层为假 ⇒ `world` 那一层**单独**成为决定项,
        /// 停用它就会让装配段在 `world == null` 下执行并抛。</para>
        /// </summary>
        [Test]
        public void test_acB9_assembleWithStructuresButNoWorld_yieldsNullBuildController()
        {
            // Arrange:只给注册表(既有 77 处调用方里「接了世界注册表但没接 World」的形态)
            StructureInstanceRegistry structures = MinimalWorldFactory.Create().Structures;

            // Act
            CompositionRootServices services = CompositionRoot.Assemble(structures: structures);

            // Assert:无世界 ⇒ 建造器不得存在 —— 写者的 occupancy 与 registry 必须同指一个世界
            Assert.That(services.BuildController, Is.Null,
                "只给注册表不给世界时不得造建造器(否则占用表与世界注册表分叉 = C1/C2 复现)");
        }

        [Test]
        public void test_moduleIds_projectsCatalog()
        {
            Assert.That(_controller.ModuleIds, Is.EquivalentTo(new[] { Module }),
                "`ModuleIds` 是目录投影 —— 供驱动侧选默认幽灵模块");
        }

        // ══════════════════════════════════════════════════════════════════
        // 源码面扫描工具
        // ══════════════════════════════════════════════════════════════════

        private static string RepoRoot()
            => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        /// <summary>剥除 `//` 与 `/* */` 注释(字符串字面量内容保留)。</summary>
        /// <remarks>
        /// ⚠️ 必须剥 —— 两处判据面文件的**文件头注释里就写着**被禁的标识符
        /// (例如 `BuildController.cs` 的「本类零 `IModalState` 依赖」)。
        /// 注释不是执行体;不剥就会把「解释了这条规则」判成「违反了这条规则」。
        /// </remarks>
        private static string StripComments(string src)
        {
            var sb = new System.Text.StringBuilder(src.Length);
            bool inLine = false, inBlock = false, inStr = false, inChar = false, esc = false;

            for (int i = 0; i < src.Length; i++)
            {
                char c = src[i];
                char n = i + 1 < src.Length ? src[i + 1] : '\0';

                if (inLine) { if (c == '\n') { inLine = false; sb.Append(c); } continue; }
                if (inBlock) { if (c == '*' && n == '/') { inBlock = false; i++; } continue; }
                if (inStr)
                {
                    sb.Append(c);
                    if (esc) { esc = false; continue; }
                    if (c == '\\') { esc = true; continue; }
                    if (c == '"') inStr = false;
                    continue;
                }
                if (inChar)
                {
                    sb.Append(c);
                    if (esc) { esc = false; continue; }
                    if (c == '\\') { esc = true; continue; }
                    if (c == '\'') inChar = false;
                    continue;
                }

                if (c == '/' && n == '/') { inLine = true; i++; continue; }
                if (c == '/' && n == '*') { inBlock = true; i++; continue; }
                if (c == '"') { inStr = true; sb.Append(c); continue; }
                if (c == '\'') { inChar = true; sb.Append(c); continue; }

                sb.Append(c);
            }

            return sb.ToString();
        }

        // ══════════════════════════════════════════════════════════════════
        // 测试替身(局部;共享替身见 `placeable_checker_test.cs`)
        // ══════════════════════════════════════════════════════════════════

        private sealed class CapturingSink : IEventSink
        {
            private readonly List<SimEvent> _sink;
            public CapturingSink(List<SimEvent> sink) => _sink = sink;
            public void Append(in SimEvent e) => _sink.Add(e);
        }

        /// <summary>
        /// 存在性替身:**每个格上都有人**。
        /// <para>用于走 `DemolishChecker.Check` 的 `EntityOnCell` 分支 —— 共享替身
        /// (`AlwaysEmptyPresence`)恒 false,走不到这条分支。</para>
        /// </summary>
        private sealed class PresenceOnCell : IPresenceQuery
        {
            public bool IsPresent(PatientId patientId) => true;
            public int PresentCount => 1;
            public bool IsPresentAt(WorldPos cell) => true;
            public IReadOnlyCollection<int> PresentPatientIds() => new[] { 0 };
        }
    }
}
