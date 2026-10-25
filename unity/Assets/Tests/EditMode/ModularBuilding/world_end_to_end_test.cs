// `TD-building-01` 解除动作 · 读侧判据的 **(a) 真 `World` 端到端** 载体(2026-10-25 · S20-2a)。
//
// 权威来源:
//   `docs/tech-debt-register.md` §`TD-building-01`「解除动作 · 读侧」判据逐字:
//     「判据 = **真 `World` 端到端**:`Place → IsEffectivelyWalkable == false` /
//      `Remove → true`(承 `review-modular-building-round2-2026-10-03.md:240` 的原始要求)」
//   GDD `design/gdd/modular-building.md` F-23-1(`EffectiveWalkable` 合成)· F-23-2b(整数旋转足迹)
//
// ⚠️ **与既有 e2e 的差别(本文件存在的唯一理由)**:
//   `Tests/EditMode/ClinicMachine/clinic_room_matching_e2e_test.cs:19-24` 用的是**裸**
//   `StructureInstanceRegistry`,其文件头自陈「本文件**未**经 `World` ……`IsEffectivelyWalkable`
//   的端到端判据**不在本文件面内**,**未验**」。S17-6 的 e2e 因此**不满足**本债判据。
//   本文件把 `World` 实例**本身**接进 `StructureWriter` 的 `IWorldOccupancy` 位,
//   使「Place → 真足迹 → `World.OccupyCells` → `IsEffectivelyWalkable` 合成」在
//   **同一个真 `World` 实例**上闭环;`Remove` 反向同理。
//
// ⚠️ **零 Fake 生产件**:`World` / `WorldGeometry` / `BuildSlotCatalog` /
//   `StructureWriter` / `StructureInstanceRegistry` / `BuildModulesCatalog` 全是生产类型。
//   测试件只有两样:`CapturingSink`(`StructureWriter` 要求 `IEventSink`,本测不判事件内容)
//   与 `NullOccupancy`(对照组的**故意不接线**替身,见 §对照)。
//
// ⚠️ **本测不判放置合法性**(`PlaceableChecker` 五条件)—— 那是另一个判据面。
//   `BuildSlotCatalog` 因此留空:本测的观测对象是 **Overlay 合成**,不是槽位校验。

using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Presentation;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.ModularBuilding
{
    /// <summary>
    /// 真 `World` 端到端:`Place → IsEffectivelyWalkable == false` / `Remove → true`。
    /// </summary>
    [TestFixture]
    public class WorldEndToEndTest
    {
        /// <summary>测试目录行 —— **两格足迹**(L 形),使「非锚点足迹格」可观测(C1 同型)。</summary>
        private const int ModuleLShape = 7;

        /// <summary>真 `build_modules.json` 行:2 = `surgery_table`,足迹 = `[(0,0,0),(1,0,0)]`。</summary>
        private const int ModuleSurgeryTableReal = 2;

        /// <summary>世界边长(5×1×5 —— 与 `world_test.cs` 同形,由测试自建数组,不经「尺寸构造」入口)。</summary>
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
            // ── 真 `WorldGeometry`(全格可走;POI / 资源网格填 -1 = 「无」,承 WorldLattice 语义)──
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

            var geometry = new WorldGeometry(terrain, poi, resource);
            _world = new World(geometry, new BuildSlotCatalog());

            // ── 真写者,occupancy 位 = 真 `World` 本身(判据的全部意义所在)──
            _pool = new InMemoryBlobPool();
            _encoder = new PayloadEncoder(_pool);
            _events = new List<SimEvent>();
            _catalog = new TestModuleCatalog();
            _catalog.RegisterModule(new ModuleDefinition(
                moduleId: ModuleLShape,
                slotType: SlotType.Decor,
                localOccupancy: new List<WorldPos> { new WorldPos(0, 0, 0), new WorldPos(1, 0, 0) },
                validOrientations: new[] { 0 },
                cost: 4,
                refundable: 1));

            _writer = new StructureWriter(
                new CapturingSink(_events), _world.Structures, _encoder, _catalog, _world);
        }

        // ══════════ 0. 前置:未放置时全可走(防「恒 false」冒充判据)══════════

        [Test]
        public void test_world_e2e_beforePlace_anchorAndFootprintWalkable()
        {
            // Arrange: 尚未放置任何结构
            var anchor = new WorldPos(2, 0, 2);
            var second = new WorldPos(3, 0, 2);

            // Assert: 两格均可走 —— 否则 §1 的 false 断言可能是「本来就 false」
            Assert.IsTrue(_world.IsEffectivelyWalkable(anchor), "未放置 ⇒ 锚点须可走(前置)");
            Assert.IsTrue(_world.IsEffectivelyWalkable(second), "未放置 ⇒ 非锚点足迹格须可走(前置)");
            Assert.AreEqual(0, _world.OccupiedCount, "未放置 ⇒ 占用表须空");
        }

        // ══════════ 1. (a) 判据正腿:Place ⇒ IsEffectivelyWalkable == false ══════════

        [Test]
        public void test_world_e2e_place_blocksAnchorAndNonAnchorFootprint()
        {
            // Arrange
            var anchor = new WorldPos(2, 0, 2);
            var second = new WorldPos(3, 0, 2);

            // Act
            long sid = _writer.Place(anchor, ModuleLShape, orientation: 0, variant: 0, tick: 1);

            // Assert
            Assert.Greater(sid, 0, "Place 须返回有效 structure_id");
            Assert.IsFalse(_world.IsEffectivelyWalkable(anchor),
                "🔴 (a):Place 后**锚点**须不可走 —— 未接线 World 时本断言绿(false 预期落空)");
            Assert.IsFalse(_world.IsEffectivelyWalkable(second),
                "🔴 (a):Place 后**非锚点足迹格**也须不可走(只写锚点 = C2 复现)");
            Assert.AreEqual(2, _world.OccupiedCount,
                "占用表格数须 == 真足迹格数(2);1 ⇒ 足迹退化;>2 ⇒ 越界写入");
        }

        [Test]
        public void test_world_e2e_place_leavesUnrelatedCellsWalkable()
        {
            // Arrange: 让 (0,0,0) 远离放置点
            var untouched = new WorldPos(0, 0, 0);

            // Act
            _writer.Place(new WorldPos(2, 0, 2), ModuleLShape, 0, 0, tick: 1);

            // Assert: 防「一次 Place 把全图 block」的退化
            Assert.IsTrue(_world.IsEffectivelyWalkable(untouched),
                "未被足迹覆盖的格须仍可走(否则 OccupiedCount 断言会被整体 block 骗过)");
        }

        // ══════════ 2. (a) 判据反腿:Remove ⇒ IsEffectivelyWalkable == true ══════════

        [Test]
        public void test_world_e2e_remove_restoresAnchorAndNonAnchorFootprint()
        {
            // Arrange
            var anchor = new WorldPos(2, 0, 2);
            var second = new WorldPos(3, 0, 2);
            long sid = _writer.Place(anchor, ModuleLShape, 0, 0, tick: 1);
            Assert.IsFalse(_world.IsEffectivelyWalkable(anchor), "前置:放置后须不可走");

            // Act
            bool removed = _writer.Remove(sid, tick: 2);

            // Assert
            Assert.IsTrue(removed, "Remove 须返回 true");
            Assert.IsTrue(_world.IsEffectivelyWalkable(anchor),
                "🔴 (a):Remove 后**锚点**须恢复可走");
            Assert.IsTrue(_world.IsEffectivelyWalkable(second),
                "🔴 (a):Remove 后**非锚点足迹格**也须恢复(只放锚点 = FreeCells 未接全足迹)");
            Assert.AreEqual(0, _world.OccupiedCount, "Remove 后占用表须回到空");
        }

        [Test]
        public void test_world_e2e_placeRemovePlace_roundTripIsStable()
        {
            // Arrange: 同一格放 → 拆 → 再放(防「FreeCells 只清一次」的残留)
            var anchor = new WorldPos(2, 0, 2);

            // Act / Assert
            long first = _writer.Place(anchor, ModuleLShape, 0, 0, tick: 1);
            Assert.IsFalse(_world.IsEffectivelyWalkable(anchor));
            _writer.Remove(first, tick: 2);
            Assert.IsTrue(_world.IsEffectivelyWalkable(anchor), "第一轮拆除后须恢复");

            long second = _writer.Place(anchor, ModuleLShape, 0, 0, tick: 3);
            Assert.AreNotEqual(first, second, "structure_id 须发新号(不复用)");
            Assert.IsFalse(_world.IsEffectivelyWalkable(anchor), "第二轮放置后须再次不可走");
            Assert.AreEqual(2, _world.OccupiedCount, "第二轮占用格数须仍 == 2(无累加残留)");
        }

        // ══════════ 3. 结构视图经 `World` 可见(同一条链的旁证)══════════

        [Test]
        public void test_world_e2e_getModuleAt_reflectsPlaceAndRemove()
        {
            // Arrange
            var anchor = new WorldPos(2, 0, 2);

            // Act / Assert
            Assert.AreEqual(-1, _world.GetModuleAt(anchor), "未放置 ⇒ -1");

            long sid = _writer.Place(anchor, ModuleLShape, 0, 0, tick: 1);
            Assert.AreEqual(ModuleLShape, _world.GetModuleAt(anchor),
                "放置后 `World.GetModuleAt` 须经真注册表报出 module_id");

            _writer.Remove(sid, tick: 2);
            Assert.AreEqual(-1, _world.GetModuleAt(anchor), "拆除后须回到 -1");
        }

        // ══════════ 4. 对照组:证明观测点**依赖** World 接线(判别力自检)══════════

        /// <summary>
        /// 同一 `World` + 同一目录,但写者的 occupancy 位换成**不接线**替身
        /// ⇒ `Place` 后 `IsEffectivelyWalkable` **仍为 true**。
        /// <para><b>为什么必须有</b>:若本测的 false 断言来自别的东西(如目录 / 注册表副作用),
        /// 这条对照会**同样变 false**;它保持 true 才证明 §1 的 false 确实由
        /// `World.OccupyCells` 产生。</para>
        /// </summary>
        [Test]
        public void test_world_e2e_withoutOccupancyWiring_walkableUnchanged()
        {
            // Arrange: 写者不接 `World`(结构照进注册表,Overlay 不动)
            var detached = new StructureWriter(
                new CapturingSink(_events), _world.Structures, _encoder, _catalog, new NullOccupancy());
            var anchor = new WorldPos(2, 0, 2);

            // Act
            long sid = detached.Place(anchor, ModuleLShape, 0, 0, tick: 1);

            // Assert
            Assert.Greater(sid, 0, "前置:结构须真的进了注册表");
            Assert.IsTrue(_world.IsEffectivelyWalkable(anchor),
                "未接 Overlay ⇒ 须仍可走(本断言绿 = §1 的 false 确由 World 接线产生)");
            Assert.AreEqual(0, _world.OccupiedCount, "未接 Overlay ⇒ 占用表须仍空");
        }

        // ══════════ 5. 真烘焙目录经 `World`(生产数据全链)══════════

        /// <summary>
        /// `build_modules.cooked` 的 module 2(`surgery_table`,两格足迹)经真目录 → 真写者 → 真 `World`。
        /// <para>⚠️ 本测钉的是**生产目录行的足迹**能被 `World` 正确吸收;若 `build_modules.json`
        /// 的 `occupancy_local` 改形,本测先红 ⇒ 提醒同步(与
        /// `clinic_room_matching_e2e_test.test_catalogue_idsMatchBuildModulesArtifact` 同款判别力)。</para>
        /// </summary>
        [Test]
        public void test_world_e2e_realBakedCatalog_surgeryTableBlocksBothCells()
        {
            // Arrange: 真烘焙产物(缺 ⇒ 抛,不静默降级 —— DataCookedDisk 的 E-13 口径)
            var realCatalog = new BuildModulesCatalog(DataCookedDisk.LoadBuildModules());
            var realWriter = new StructureWriter(
                new CapturingSink(_events), _world.Structures, _encoder, realCatalog, _world);
            var anchor = new WorldPos(2, 0, 2);
            var second = new WorldPos(3, 0, 2);

            // Act
            long sid = realWriter.Place(anchor, ModuleSurgeryTableReal, orientation: 0, variant: 0, tick: 1);

            // Assert
            Assert.IsFalse(_world.IsEffectivelyWalkable(anchor), "真目录两格模块:锚点须不可走");
            Assert.IsFalse(_world.IsEffectivelyWalkable(second),
                "真目录两格模块:非锚点足迹格须不可走(只挡锚点 ⇒ 生产数据未真吸收)");
            Assert.AreEqual(2, _world.OccupiedCount);

            realWriter.Remove(sid, tick: 2);
            Assert.IsTrue(_world.IsEffectivelyWalkable(anchor), "真目录:拆除后锚点须恢复");
            Assert.IsTrue(_world.IsEffectivelyWalkable(second), "真目录:拆除后非锚点格须恢复");
        }

        // ══════════ 辅助 ══════════

        private sealed class CapturingSink : IEventSink
        {
            private readonly List<SimEvent> _sink;
            public CapturingSink(List<SimEvent> sink) { _sink = sink; }
            public void Append(in SimEvent e) => _sink.Add(e);
        }

        /// <summary>对照组替身:**故意不接线**(不写 Overlay)。</summary>
        private sealed class NullOccupancy : IWorldOccupancy
        {
            public void OccupyCells(IEnumerable<WorldPos> cells) { }
            public void FreeCells(IEnumerable<WorldPos> cells) { }
        }
    }
}
