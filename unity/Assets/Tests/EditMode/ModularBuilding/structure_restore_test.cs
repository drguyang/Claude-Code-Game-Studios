// S20-18 · World 生命周期轮 —— **Sim 侧**夹具(重置 / 重放 / `Modify` 足迹)。
//
// 权威来源:
//   `production/sprints/sprint-20.md` §十 S20-18 的 AC-1…AC-7、AC-10
//   ADR-009 §四(派生态:由事件流确定性重建)· ADR-015 §五(足迹 = 模块定义 + 朝向的纯函数)
//   同型先例:`PoiStateMachine.RebuildFromDecoded`(先清后重放、不自行解码)
//
// ⚠️ **零 Fake 生产件**:`World` / `WorldGeometry` / `BuildSlotCatalog` /
//   `StructureInstanceRegistry` / `StructureWriter` / `StructureRestorer` 全是生产类型。
//   测试件只两样:`CapturingSink`(`IEventSink` 的最小实现)与 `TestModuleCatalog`
//   (`placeable_checker_test.cs` 的既有序目替身,1 格与 2 格两种足迹)。
//
// ⚠️ **本文件的判据全部是行为面的**(占用表 / 实例表 / 可走性 / 抛出),没有一条是
//   「源码文本里某字面量恰 N 处」—— 故证伪轮的变异形态一律为**行为变异**,
//   不会重演 S20-17 首轮「行为变异打文本判据 ⇒ 零判别力」的错误。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.ModularBuilding
{
    /// <summary>世界重置与世界流重放的判据(读档后世界 = 存档时刻的准确世界)。</summary>
    [TestFixture]
    public class StructureRestoreTest
    {
        /// <summary>两格足迹(L 形):使「非锚点足迹格」与「旋转后换格」都可观测。</summary>
        private const int ModuleBunk = 7;

        /// <summary>非法 moduleId(目录里没有)。</summary>
        private const int ModuleMissing = 999;

        /// <summary>世界边长(5×1×5 —— 与 `world_end_to_end_test` 同形,自建数组,不经尺寸构造入口)。</summary>
        private const int Size = 5;

        private World _world;
        private InMemoryBlobPool _pool;
        private PayloadEncoder _encoder;
        private TestModuleCatalog _catalog;
        private StructureWriter _writer;
        private List<SimEvent> _events;

        [SetUp]
        public void Setup()
        {
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

            _world = new World(new WorldGeometry(terrain, poi, resource), new BuildSlotCatalog());

            _pool = new InMemoryBlobPool();
            _encoder = new PayloadEncoder(_pool);
            _events = new List<SimEvent>();
            _catalog = new TestModuleCatalog();
            // 朝向 0 → [(0,0),(1,0)];朝向 90 → [(0,0),(0,1)](整数四向旋转,F-23-2b)
            _catalog.RegisterModule(new ModuleDefinition(
                moduleId: ModuleBunk,
                slotType: SlotType.Decor,
                localOccupancy: new List<WorldPos> { new WorldPos(0, 0, 0), new WorldPos(1, 0, 0) },
                validOrientations: new[] { 0, 90, 180, 270 },
                cost: 4,
                buildItemKey: 0,
                refundable: 1));

            var sink = new CapturingSink(_events);
            _writer = new StructureWriter(
                sink, _world.Structures, _encoder, _catalog, _world,
                DaYiJingCheng.Tests.TestDropWriter.Create(sink));
        }

        private StructureRestorer NewRestorer() => new StructureRestorer(_world, _catalog);

        // ══════════ AC-1:重置清两样(占用表 + 实例表)══════════

        [Test]
        public void test_ac1_reset_clearsOccupancyAndRegistry()
        {
            // Arrange:经**真写者**放一个两格模块(前置 —— 重置前的非空必须是真的)
            long id = _writer.Place(new WorldPos(1, 0, 1), ModuleBunk, 0, 0, tick: 1);
            Assert.Greater(id, 0);
            Assert.AreEqual(2, _world.OccupiedCount, "前置:两格足迹须都被占");
            Assert.AreEqual(1, _world.Structures.Count, "前置:实例表须有 1 条");
            Assert.IsFalse(_world.IsEffectivelyWalkable(new WorldPos(2, 0, 1)), "前置:非锚点足迹格须不可走");

            // Act
            _world.Reset();

            // Assert:两样都清
            Assert.AreEqual(0, _world.OccupiedCount, "重置后占用表须全零");
            Assert.AreEqual(0, _world.Structures.Count, "重置后实例表须空");
            Assert.IsTrue(_world.IsEffectivelyWalkable(new WorldPos(2, 0, 1)),
                "重置后旧足迹格须恢复可走(否则 IsEffectivelyWalkable 仍被旧世界污染)");
        }

        // ══════════ AC-2:重置复位发号计数器 ══════════

        [Test]
        public void test_ac2_reset_resetsIdCounter()
        {
            // Arrange:让计数器走到 3
            Assert.AreEqual(1L, _world.Structures.Register(new WorldPos(0, 0, 0), ModuleBunk, 0, 0));
            Assert.AreEqual(2L, _world.Structures.Register(new WorldPos(1, 0, 0), ModuleBunk, 0, 0));
            Assert.AreEqual(3L, _world.Structures.Register(new WorldPos(2, 0, 0), ModuleBunk, 0, 0));

            // Act —— ⚠️ 用 Reset 而非 Clear:Clear **不复位**计数器(见该方法的注)
            _world.Structures.Reset();

            // Assert:复位到 1(若计数器停在 4,重放后新结构的号段就与存档错位)
            Assert.AreEqual(1L, _world.Structures.Register(new WorldPos(0, 0, 0), ModuleBunk, 0, 0),
                "Reset 后发号须从 1 重新开始");
        }

        // ══════════ AC-3:重放 `StructurePlaced` 还原实例 + 占用 + 可走性 ══════════

        [Test]
        public void test_ac3_replayPlaced_restoresFootprintAndWalkability()
        {
            // Arrange:先经真写者放一次,记下**真足迹**作为期望值(不手写字面量 ——
            // 足迹的权威是 PlaceableChecker.ComputeOccupiedCells,不是本测)
            var anchor = new WorldPos(1, 0, 1);
            long liveId = _writer.Place(anchor, ModuleBunk, 0, 0, tick: 1);
            WorldPos[] expected =
            {
                new WorldPos(1, 0, 1),
                new WorldPos(2, 0, 1),
            };
            Assert.AreEqual(2, _world.OccupiedCount, "前置:现场放置须占两格");

            // Act:整体重建 —— **先清后重放**(与现场放置完全无关的一条独立路径)
            int applied = NewRestorer().RebuildFromDecoded(new[]
            {
                StructureRestoreOp.Placed(liveId, anchor, ModuleBunk, 0, 0),
            });

            // Assert
            Assert.AreEqual(1, applied, "须恰应用 1 条");
            Assert.AreEqual(1, _world.Structures.Count);
            Assert.IsTrue(_world.Structures.TryGet(liveId, out StructureInstance inst),
                "重放须**按事件原号**落库");
            Assert.AreEqual(ModuleBunk, inst.ModuleId);
            Assert.AreEqual(0, inst.Orientation);
            CollectionAssert.AreEquivalent(expected, inst.OccupiedCells,
                "实例足迹须与 PlaceableChecker.ComputeOccupiedCells 同源");
            Assert.AreEqual(2, _world.OccupiedCount, "占用表须与现场放置一致");
            Assert.IsFalse(_world.IsEffectivelyWalkable(expected[0]), "锚点格须不可走");
            Assert.IsFalse(_world.IsEffectivelyWalkable(expected[1]), "非锚点足迹格须不可走");
        }

        // ══════════ AC-4:`Placed → Removed` 重放 ⇒ 净空 ══════════

        [Test]
        public void test_ac4_replayPlaceThenRemove_leavesWorldEmpty()
        {
            // Arrange
            var anchor = new WorldPos(1, 0, 1);
            long liveId = _writer.Place(anchor, ModuleBunk, 0, 0, tick: 1);
            _writer.Remove(liveId, tick: 2, demolishRefundRatio: DaYiJingCheng.Tests.TestDropWriter.P0RefundRatio);

            // Act:重放**同一条流的两端**(放置 → 拆除)
            int applied = NewRestorer().RebuildFromDecoded(new[]
            {
                StructureRestoreOp.Placed(liveId, anchor, ModuleBunk, 0, 0),
                StructureRestoreOp.Removed(liveId),
            });

            // Assert
            Assert.AreEqual(2, applied);
            Assert.AreEqual(0, _world.Structures.Count, "拆除后实例表须空");
            Assert.AreEqual(0, _world.OccupiedCount, "拆除后占用表须全零(足迹须被释放)");
            Assert.IsTrue(_world.IsEffectivelyWalkable(anchor), "拆除后锚点格须恢复可走");
            Assert.IsTrue(_world.IsEffectivelyWalkable(new WorldPos(2, 0, 1)), "拆除后非锚点足迹格须恢复可走");
        }

        // ══════════ AC-5:重放 `Modified` ⇒ 足迹随朝向搬家 ══════════

        [Test]
        public void test_ac5_replayRotate_movesFootprint()
        {
            // Arrange:朝向 0 的足迹 = [(1,0,1),(2,0,1)];朝向 90 应变成 [(1,0,1),(1,0,2)]
            var anchor = new WorldPos(1, 0, 1);
            var oldSecond = new WorldPos(2, 0, 1);
            var newSecond = new WorldPos(1, 0, 2);

            // Act
            int applied = NewRestorer().RebuildFromDecoded(new[]
            {
                StructureRestoreOp.Placed(1, anchor, ModuleBunk, 0, 0),
                StructureRestoreOp.Modified(1, newOrientation: 90, newVariant: 0),
            });

            // Assert
            Assert.AreEqual(2, applied);
            Assert.IsTrue(_world.Structures.TryGet(1, out StructureInstance inst));
            Assert.AreEqual(90, inst.Orientation, "实例朝向须是旋转后值");
            CollectionAssert.AreEquivalent(
                new[] { anchor, newSecond }, inst.OccupiedCells,
                "实例足迹须是**旋转后**的足迹(旧实现会留旧足迹 ⇒ 本断言红)");

            Assert.IsTrue(_world.IsEffectivelyWalkable(oldSecond), "旋转后**旧**第二格须恢复可走");
            Assert.IsFalse(_world.IsEffectivelyWalkable(newSecond), "旋转后**新**第二格须不可走");
            Assert.IsFalse(_world.IsEffectivelyWalkable(anchor), "锚点格转前转后都占,须仍不可走");
            Assert.AreEqual(2, _world.OccupiedCount, "总占用格数不变(仍是两格模块)");
        }

        // ══════════ AC-6:重放保号 + 高水位 = max(id) + 1 ══════════

        [Test]
        public void test_ac6_replayKeepsIdsAndBumpsHighWater()
        {
            // Arrange:流里出现过 id = 5(与 1、2 不连续 —— 保号才看得见)
            NewRestorer().RebuildFromDecoded(new[]
            {
                StructureRestoreOp.Placed(5, new WorldPos(1, 0, 1), ModuleBunk, 0, 0),
            });
            Assert.IsTrue(_world.Structures.TryGet(5, out _), "前置:id=5 须按原号落库");

            // Act:重放之后新放一个 —— 号必须是 6(而不是 1 ⇒ 撞号/号段错位)
            long next = _writer.Place(new WorldPos(3, 0, 3), ModuleBunk, 0, 0, tick: 2);

            // Assert
            Assert.AreEqual(6L, next, "重放须抬高高水位到 max(id)+1 ⇒ 新结构取 6");
            Assert.AreEqual(2, _world.Structures.Count);
            Assert.IsTrue(_world.Structures.TryGet(5, out _), "id=5 须仍在(未被新结构覆盖)");
        }

        // ══════════ AC-7:fail-closed —— 三种坏输入各抛,且抛后世界为空 ══════════

        [Test]
        public void test_ac7a_unknownModuleId_throwsAndLeavesWorldEmpty()
        {
            // Arrange:第一条好、第二条 moduleId 不在目录 ⇒ 抛在**中途**,正是原子性的判据面
            var ops = new[]
            {
                StructureRestoreOp.Placed(1, new WorldPos(1, 0, 1), ModuleBunk, 0, 0),
                StructureRestoreOp.Placed(2, new WorldPos(3, 0, 3), ModuleMissing, 0, 0),
            };

            // Act + Assert
            var ex = Assert.Throws<InvalidOperationException>(() => NewRestorer().RebuildFromDecoded(ops));
            StringAssert.Contains("不在模块目录内", ex.Message);

            Assert.AreEqual(0, _world.Structures.Count, "抛出后实例表须为空(不得留「重放了一半」)");
            Assert.AreEqual(0, _world.OccupiedCount, "抛出后占用表须全零");
        }

        [Test]
        public void test_ac7b_removedUnknownId_throwsAndLeavesWorldEmpty()
        {
            var ops = new[]
            {
                StructureRestoreOp.Placed(1, new WorldPos(1, 0, 1), ModuleBunk, 0, 0),
                StructureRestoreOp.Removed(2),   // id=2 从未放置 ⇒ 流损坏
            };

            var ex = Assert.Throws<InvalidOperationException>(() => NewRestorer().RebuildFromDecoded(ops));
            StringAssert.Contains("不在实例表内", ex.Message);
            Assert.AreEqual(0, _world.Structures.Count);
            Assert.AreEqual(0, _world.OccupiedCount);
        }

        [Test]
        public void test_ac7c_modifiedUnknownId_throwsAndLeavesWorldEmpty()
        {
            var ops = new[]
            {
                StructureRestoreOp.Placed(1, new WorldPos(1, 0, 1), ModuleBunk, 0, 0),
                StructureRestoreOp.Modified(2, newOrientation: 90, newVariant: 0),
            };

            var ex = Assert.Throws<InvalidOperationException>(() => NewRestorer().RebuildFromDecoded(ops));
            StringAssert.Contains("不在实例表内", ex.Message);
            Assert.AreEqual(0, _world.Structures.Count);
            Assert.AreEqual(0, _world.OccupiedCount);
        }

        [Test]
        public void test_ac7d_nullOps_throws()
        {
            Assert.Throws<System.ArgumentNullException>(
                () => NewRestorer().RebuildFromDecoded(null));
        }

        [Test]
        public void test_ac7e_nullCatalog_throwsAtConstruction()
        {
            Assert.Throws<System.ArgumentNullException>(
                () => new StructureRestorer(_world, null));
        }

        // ══════════ AC-10:`Modify` 必须改占用表(修复前的缺陷)══════════

        [Test]
        public void test_ac10_modifyAppliesFootprintChange()
        {
            // Arrange:朝向 0 放置
            var anchor = new WorldPos(1, 0, 1);
            var oldSecond = new WorldPos(2, 0, 1);
            var newSecond = new WorldPos(1, 0, 2);
            long id = _writer.Place(anchor, ModuleBunk, 0, 0, tick: 1);
            Assert.IsFalse(_world.IsEffectivelyWalkable(oldSecond), "前置:朝向 0 的第二格须不可走");

            // Act:旋转 90°
            Assert.IsTrue(_writer.Modify(id, newOrientation: 90, newVariant: null, tick: 2),
                "前提:朝向确实变化 ⇒ Modify 须成功");

            // Assert:旧格放行、新格挡住、注册表记新足迹
            Assert.IsTrue(_world.IsEffectivelyWalkable(oldSecond),
                "旋转后**旧**第二格须恢复可走(修复前:仍不可走 ⇒ 本断言红)");
            Assert.IsFalse(_world.IsEffectivelyWalkable(newSecond),
                "旋转后**新**第二格须不可走(修复前:仍可走 ⇒ 本断言红)");
            Assert.IsTrue(_world.Structures.TryGet(id, out StructureInstance inst));
            Assert.AreEqual(90, inst.Orientation);
            CollectionAssert.AreEquivalent(new[] { anchor, newSecond }, inst.OccupiedCells,
                "注册表须记旋转后的足迹(修复前记的是旧足迹)");
            Assert.AreEqual(2, _world.OccupiedCount, "总占用格数不变");
        }

        [Test]
        public void test_ac10b_modifyWithoutCatalog_throwsBeforeTouchingWorld()
        {
            // Arrange:写者不接目录 / 不接占用表(两条 fail-closed 的守卫)
            var sink = new CapturingSink(new List<SimEvent>());
            var bare = new StructureWriter(
                sink, _world.Structures, _encoder,
                moduleCatalog: null, occupancy: null,
                DaYiJingCheng.Tests.TestDropWriter.Create(sink));

            // 先用**好写者**落一个实例(裸写者自身放不了 —— 它没目录)
            long id = _writer.Place(new WorldPos(1, 0, 1), ModuleBunk, 0, 0, tick: 1);
            int occupiedBefore = _world.OccupiedCount;

            // Act + Assert:旋转须抛,且**抛前不改世界**
            var ex = Assert.Throws<System.InvalidOperationException>(
                () => bare.Modify(id, newOrientation: 90, newVariant: null, tick: 2));
            StringAssert.Contains("IModuleCatalog", ex.Message);
            Assert.AreEqual(occupiedBefore, _world.OccupiedCount, "抛前不得改占用表");
            Assert.IsTrue(_world.Structures.TryGet(id, out StructureInstance inst));
            Assert.AreEqual(0, inst.Orientation, "抛前不得改实例朝向");
        }

        // ══════════ 夹具 ══════════

        private sealed class CapturingSink : IEventSink
        {
            private readonly List<SimEvent> _sink;
            public CapturingSink(List<SimEvent> sink) { _sink = sink; }
            public void Append(in SimEvent e) => _sink.Add(e);
        }
    }
}
