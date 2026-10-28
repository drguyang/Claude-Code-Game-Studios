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
//   🔁 **2026-10-28 就地订正(原文不删)**:上句「只两样」自 B-1' 轮起**已不准确** ——
//   本文件**自带的桩有两个**:`CapturingSink` 与 `RevocableModuleCatalog`
//   (B-1' 轮为造「基线中途抛」引入;本轮 §七-1 又给它加了 `RevokeAfterLookups` 查询配额)。
//   `TestModuleCatalog` 住在 `placeable_checker_test.cs`(别文件的既有序目替身),不算本文件的。
//   **现状 = 本文件自带 2 个桩 + 引用 1 个外来桩**,称「只两样」会让读者以为目录只有一个桩。
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

        /// <summary>
        /// `StructureWriter.PeekNextId` 须与**注册表**同一计数器,不得自己镜像一份。
        /// <para>🔁 <b>2026-10-28(S20-18 评审 · 新增)</b>:写者侧原有一个 `_nextStructureId`
        /// 镜像字段,两条路径上会与注册表**双向脱钩**,且都静默:① 基线在写者构造**之前**
        /// 跑(`CompositionRoot.cs:917` vs `:941`)⇒ 注册表已到 `N+1` 而镜像仍是 1;
        /// ② `World.Reset()` 复位注册表 ⇒ 镜像停在旧高水位。两方向都让 `PeekNextId`
        /// 报一个**不会真的发出**的号,而它唯一的消费方(`build_controller_test:189`)
        /// 正是拿它当「`Place` 有没有真被调用」的判据。</para>
        /// </summary>
        /// <remarks><b>判别力</b>:把 `PeekNextId` 改回读本类私有字段 ⇒ 本条红
        /// (基线已把注册表推到 2,镜像仍是 1)。</remarks>
        [Test]
        public void test_peekNextId_delegatesToRegistry_notLocalMirror()
        {
            // Arrange:带基线的世界 —— 基线落 1 件后,注册表的下一号必须是 2
            var slots = new BuildSlotCatalog();
            slots.RegisterSlot(new BuildSlot(
                new WorldPos(1, 0, 1), BitMask.Single(SlotType.Decor),
                bakedInitial: true, moduleId: ModuleBunk));

            var terrain = new TerrainCell[Size, 1, Size];
            for (int x = 0; x < Size; x++)
                for (int z = 0; z < Size; z++)
                    terrain[x, 0, z] = new TerrainCell(true, 0);
            int total = Size * 1 * Size;
            var poi = new int[total];
            var resource = new int[total];
            for (int i = 0; i < total; i++) { poi[i] = -1; resource[i] = -1; }

            var world = new World(new WorldGeometry(terrain, poi, resource), slots);

            // 关键:**写者在基线之后构造**(与生产序 CompositionRoot.cs:917 → :941 同序)
            world.ApplyBakedInitial(_catalog);
            Assert.AreEqual(1, world.BakedInitialStructureCount, "前置:基线须落 1 件");

            var sink = new CapturingSink(new List<SimEvent>());
            var writer = new StructureWriter(
                sink, world.Structures, _encoder, _catalog, world,
                DaYiJingCheng.Tests.TestDropWriter.Create(sink));

            // Assert:镜像字段此时会是 1(构造时注册表已到 2)⇒ 只有委托才给出 2
            Assert.AreEqual(2L, writer.PeekNextId,
                "`PeekNextId` 须读注册表 —— 基线已占 id=1,下一号是 2;"
                + "报 1 意味着它会预告一个已被占用的号");
            Assert.AreEqual(world.Structures.PeekNextId, writer.PeekNextId,
                "写者与注册表须是**同一个**计数器(两个计数器 = 两条静默脱钩路径)");
        }

        // ══════════ AC-7:fail-closed —— 三种坏输入各抛,且抛后世界为空 ══════════

        /// <summary>
        /// 重放 `Placed` **同号覆盖**已有实例 ⇒ 被覆盖那条的**旧足迹必须释放**。
        /// <para>🔁 <b>2026-10-28 评审 B-1 修复(新增)</b>:`Registry.Restore` 对同号是
        /// `_instances[id] = …` 静默覆盖,且它**从不碰占用表** ⇒ 旧足迹格永久不可走、无信号。
        /// **可达路径**:基线占 `1..N`(`World.ApplyBakedInitial`),而**基线诞生之前**写下的存档
        /// 里玩家放置从 `1` 起号 ⇒ 重放第一条 `Placed(id=1)` 就撞上基线第 1 件。</para>
        /// <para>⚠️ **措辞订正(2026-10-28)**:原文写「baseline 之前的存档(**schema 1**)」——
        /// 「schema 1」在仓库里专指 `world_buildslots.json` 的**作者态 JSON 格式**,
        /// 与**存档格式** `SaveVersion`(`CurrentSaveVersion = 1`)无关。真实类别是**时序**:
        /// 基线由 `d230eb36`(2026-10-28)引入。见债册 `TD-building-02`。</para>
        /// <para>⚠️ <b>夹具必须走「基线」这条腿</b>,不能用「先 Place 再 Rebuild」:后者里
        /// `RebuildFromDecoded` 内部先 `Reset()`,把预置实例与占用**都清光了** ⇒ 重放那条
        /// `Placed(1, …)` 是**新建**而非覆盖 ⇒ 断言全部恒真。基线之所以能存活,是因为
        /// `World.Reset()` 的语义是「回基线」而不是「回空」(`_moduleCatalogForBaseline` 仍在)。</para>
        /// </summary>
        /// <remarks><b>判别力</b>:删掉 `StructureRestorer.Apply` 的 `Placed` 支里那段
        /// `if (TryGet) FreeCells(existing.OccupiedCells)` ⇒ 本条红(旧格仍不可走)。</remarks>
        [Test]
        public void test_ac6b_replayPlacedOverBaselineId_releasesOldFootprint()
        {
            // Arrange:造一个**带基线**的世界 —— 槽位 (1,0,1) 的 baked_initial 家具 = ModuleBunk,
            //          足迹 = [(1,0,1),(2,0,1)],拿到 id=1(基线发号从 1 起)。
            var baselineSlots = new BuildSlotCatalog();
            baselineSlots.RegisterSlot(new BuildSlot(
                new WorldPos(1, 0, 1), BitMask.Single(SlotType.Decor),
                bakedInitial: true, moduleId: ModuleBunk));

            var terrain = new TerrainCell[Size, 1, Size];
            for (int x = 0; x < Size; x++)
                for (int z = 0; z < Size; z++)
                    terrain[x, 0, z] = new TerrainCell(true, 0);
            int total = Size * 1 * Size;
            var poi = new int[total];
            var resource = new int[total];
            for (int i = 0; i < total; i++) { poi[i] = -1; resource[i] = -1; }

            var baselineWorld = new World(new WorldGeometry(terrain, poi, resource), baselineSlots);
            baselineWorld.ApplyBakedInitial(_catalog);

            Assert.AreEqual(1, baselineWorld.BakedInitialStructureCount, "前置:基线须恰好 1 件");
            Assert.IsTrue(baselineWorld.Structures.TryGet(1, out _), "前置:基线须拿到 id=1");
            Assert.IsFalse(baselineWorld.IsEffectivelyWalkable(new WorldPos(1, 0, 1)),
                "前置:基线锚点格须不可走");
            Assert.IsFalse(baselineWorld.IsEffectivelyWalkable(new WorldPos(2, 0, 1)),
                "前置:基线非锚点格须不可走");

            // Act:重放一条**同号** id=1,锚点换到 (1,0,3) ⇒ 足迹 = [(1,0,3),(2,0,3)]
            //      `RebuildFromDecoded` 内部 `Reset()` 会把基线**重新应用**回 id=1 ⇒ 覆盖真发生。
            new StructureRestorer(baselineWorld, _catalog).RebuildFromDecoded(new[]
            {
                StructureRestoreOp.Placed(1, new WorldPos(1, 0, 3), ModuleBunk, 0, 0),
            });

            // Assert:新足迹被占、**旧足迹(基线那件)被释放**、实例表仍只有 1 条
            Assert.IsFalse(baselineWorld.IsEffectivelyWalkable(new WorldPos(1, 0, 3)), "新足迹锚点须不可走");
            Assert.IsFalse(baselineWorld.IsEffectivelyWalkable(new WorldPos(2, 0, 3)), "新足迹非锚点须不可走");
            Assert.IsTrue(baselineWorld.IsEffectivelyWalkable(new WorldPos(1, 0, 1)),
                "被覆盖实例的**旧锚点格**须恢复可走 —— 否则它永久阻塞且无任何信号");
            Assert.IsTrue(baselineWorld.IsEffectivelyWalkable(new WorldPos(2, 0, 1)),
                "被覆盖实例的**旧非锚点格**须恢复可走(同族:只放锚点会漏这一格)");
            Assert.AreEqual(1, baselineWorld.Structures.Count, "同号覆盖后实例表仍只有 1 条");
            Assert.AreEqual(2, baselineWorld.OccupiedCount, "占用总数须 = 新足迹 2 格(旧 2 格已释放)");
        }

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

        // ══════════ B-1'(`World.Reset()` 的回滚半边)—— 证伪轮 M5 的靶 ══════════

        /// <summary>
        /// `Reset()` 在基线**中途**抛 ⇒ 世界须停在**确定的空态**(实例表 0 / 占用表 0 / 计数 0),
        /// 而不是「清了一半 + 落了半个基线」。
        /// <para><b>为什么这条此前只标「阅读验证」</b>:`Reset()` 的 <c>catch</c> 看起来不可达 ——
        /// 基线抛 = 槽位骨架与模块目录不同源,而数据在开档后不该变。**但它可达**,路径是
        /// <b>目录桩可变</b>:生产实现注入的是接口 <see cref="IModuleCatalog"/>,任何注入方
        /// 都能在两次 `Reset()` 之间改变其返回。本夹具就把这个输入真造出来,而不是推演它。</para>
        /// <para><b>判别力</b>:删掉 `Reset()` 的 <c>catch</c> 里那三行清空 ⇒ 本例红
        /// (实例表残留第 1 件 / 占用表残留 2 格 / 计数残留 1)。</para>
        /// <para>⚠️ <b>前置探针不可省</b>:健康目录下同一基线必须**真的**落下 2 条 / 4 格 ——
        /// 否则「归零」断言会被「基线压根没跑」满足(空集借绿,S20-20 B-2 的同款教训)。</para>
        /// <para>⚠️ <b>本夹具证明不了</b>:① 真实存档往返;② 嵌套异常**不遮蔽原始异常**那一面
        /// (那要 `StructureRestorer` 的第二次 `Reset()` 也抛,本夹具不覆盖 —— 如实登记)。</para>
        /// </summary>
        [Test]
        public void test_b1p_reset_baselineThrowsMidway_leavesDefiniteEmptyState()
        {
            // Arrange:两件开档家具 —— 第一件 ModuleBunk(1,0,1)、第二件 ModuleBed(3,0,1)
            var catalog = new RevocableModuleCatalog();
            catalog.RegisterModule(BunkDefinition(ModuleBunk));
            catalog.RegisterModule(BunkDefinition(ModuleBed));

            var slots = new BuildSlotCatalog();
            slots.RegisterSlot(new BuildSlot(new WorldPos(1, 0, 1),
                BitMask.Single(SlotType.Decor), bakedInitial: true, moduleId: ModuleBunk));
            slots.RegisterSlot(new BuildSlot(new WorldPos(3, 0, 1),
                BitMask.Single(SlotType.Decor), bakedInitial: true, moduleId: ModuleBed));

            World world = NewFlatWorld(slots);
            world.ApplyBakedInitial(catalog);

            // ── 前置探针:健康目录下基线**真的**落下 2 条 / 4 格 ──
            Assert.AreEqual(2, world.Structures.Count,
                "前置:两件开档家具都须落库 —— 否则下面的「归零」断言是空集借绿");
            Assert.AreEqual(4, world.OccupiedCount, "前置:2 件 × 2 格 = 4 格须被占");
            Assert.AreEqual(2, world.BakedInitialStructureCount, "前置:计数须为 2");

            // ── Act:让第二件家具的模块"消失" ⇒ 基线第 1 件成功、第 2 件抛 ──
            catalog.RevokedModuleId = ModuleBed;
            var ex = Assert.Throws<InvalidOperationException>(() => world.Reset());
            StringAssert.Contains("不在模块目录内", ex.Message,
                "抛须来自基线本体的「不同源」判据,不是别处的同类型异常");

            // ── Assert:确定空态,不是「清了一半 + 落了半个基线」──
            Assert.AreEqual(0, world.Structures.Count,
                "基线中途抛后实例表须归零 —— 残留第 1 件 = 世界停在中间态且无任何信号");
            Assert.AreEqual(0, world.OccupiedCount, "基线中途抛后占用表须归零");
            Assert.AreEqual(0, world.BakedInitialStructureCount, "基线中途抛后计数须归零");
        }

        /// <summary>
        /// **回滚本身也抛**时,调用方看到的须是**原始异常**(重放失败的原因),
        /// 不是回滚失败抛的那条 —— 否则根因被嵌套异常顶掉,报文把读者指向错误的层。
        /// <para><b>这是 §七-1 明确登记为「仍未覆盖」的那一半</b>(2026-10-28 原文不删):
        /// 「嵌套异常不遮蔽原始异常」需 <see cref="StructureRestorer.RebuildFromDecoded"/> 的
        /// <b>第二次</b> <c>Reset()</c> 也抛才可观测 —— 上一个夹具
        /// (<see cref="test_b1p_reset_baselineThrowsMidway_leavesDefiniteEmptyState"/>)
        /// 只造了第一次 `Reset()` 抛,故不覆盖本面。</para>
        ///
        /// <para><b>怎么真造出这个输入(不是推演)</b>:两次 `Reset()` 之间目录必须**从好变坏**。
        /// `RevokedModuleId` 只能表达「全程坏」,所以用
        /// <see cref="RevocableModuleCatalog.RevokeAfterLookups"/> 给模块号一个**查询配额**:
        /// 首次 `Reset()` 的基线查它时配额还有 ⇒ 成功;第二次 `Reset()` 再查时配额已尽 ⇒ 抛。</para>
        ///
        /// <para><b>时序(逐拍,对应代码行;行号按 2026-10-28 修复后的树)</b>:</para>
        /// <para>① `RebuildFromDecoded` → `_world.Reset()`(`StructureRestore.cs:159`)⇒ 基线两件都落库 ✅</para>
        /// <para>② `Apply(ops[0])` → `FootprintOf(999)` ⇒ 抛「重放第 0 条:moduleId=999 不在模块目录内」</para>
        /// <para>③ `catch` → 回滚 `_world.Reset()`(`:195`)⇒ 基线第二件的配额已尽 ⇒ 抛「槽位 (3,0,1) …」</para>
        /// <para>④ `throw;`(`:202`)—— <b>修复前到不了</b>(③ 先抛)⇒ 调用方看到的是基线那条</para>
        ///
        /// <para><b>判别力(实测,不是推演)</b> —— 本夹具在修复前后各跑一次,结论是
        /// <b>它抓到了一个真缺陷</b>,不是判据过严:</para>
        /// <para>· <b>修复前</b>:实测红,报文实值
        /// <c>"槽位 (3,0,1) 的 `module_id = 8` 不在模块目录内 —— 槽位骨架与模块目录不同源…"</c>
        /// ⇒ 回滚失败那条把根因顶掉了。</para>
        /// <para>· <b>修复后</b>:17/17 全绿。</para>
        /// <para>· <b>M12</b>(把回滚退回裸 `_world.Reset(); throw;`)⇒ <b>恰 1 条红,正是本测</b>
        /// —— <b>独占</b>:同轮另 16 条全绿。「嵌套异常不遮蔽原始异常」这一面只有本夹具在守。</para>
        /// <para>· <b>M13</b>(回滚包住后把 `throw;` 改成 `return 0`)⇒ <b>4 条红</b>
        /// (`ac7a` / `ac7b` / `ac7c` / 本测),**不独占** —— 它打的是另一条属性
        /// 「原始异常必须真的被送出」,而 ac7 三条本来就断言「必须抛」。
        /// 按「一属性一条变异」,两条属性各留一条:M12 守「不被顶掉」,M13 守「真的送出」。</para>
        /// <para>⚠️ <b>为什么必须钉前缀而不是只断类型</b>:两条报文**都含**「不在模块目录内」、
        /// 同为 `InvalidOperationException` ⇒ 只断类型或短语包含**分不开**。
        /// 这正是 `feedback_fail_fast_needs_sideeffect_assert` 的同族纪律。</para>
        ///
        /// <para>⚠️ <b>前置探针不可省</b>:同一条重放、同一份基线,**配额放宽**时必须抛「重放第 0 条」
        /// —— 否则「回滚失败」这个前提本身没被证明存在(空集借绿)。</para>
        /// <para>⚠️ <b>配额须是 2,不是 1</b> —— 见本体段注释的逐拍复算。</para>
        /// </summary>
        [Test]
        public void test_b1q_rebuild_rollbackAlsoThrows_originalExceptionNotMasked()
        {
            // ══════ 前置探针:配额放宽 ⇒ 回滚成功,原始异常浮出 ══════
            {
                var catalog = new RevocableModuleCatalog();
                catalog.RegisterModule(BunkDefinition(ModuleBunk));
                catalog.RegisterModule(BunkDefinition(ModuleBed));
                catalog.RevokeAfterLookups(ModuleBed, 99);   // 两次 Reset 都够用

                World world = NewFlatWorld(BaselineSlots());
                world.ApplyBakedInitial(catalog);
                Assert.AreEqual(2, world.BakedInitialStructureCount, "前置:基线须落 2 件");

                var probe = Assert.Throws<InvalidOperationException>(
                    () => new StructureRestorer(world, catalog).RebuildFromDecoded(BrokenReplayOps()));
                StringAssert.Contains("重放第 0 条", probe.Message,
                    "前置:配额够时回滚成功 ⇒ 原始异常须浮出(否则本测的「遮蔽」面根本不存在)");
            }

            // ══════ 本体:配额卡在「只够第一次 Reset」 ⇒ 回滚也抛 ══════
            var catalog2 = new RevocableModuleCatalog();
            catalog2.RegisterModule(BunkDefinition(ModuleBunk));
            catalog2.RegisterModule(BunkDefinition(ModuleBed));
            // 基线按 (x,y,z) 升序发号 ⇒ 先查 ModuleBunk(1,0,1)、后查 ModuleBed(3,0,1)。
            // ⚠️ **配额须是 2,不是 1** —— 逐拍复算(ModuleBed 每应用一次基线被查一次):
            //   ① `ApplyBakedInitial` 应用基线      → 配额 2→1 ✅
            //   ② `RebuildFromDecoded` 首次 `Reset()` → 配额 1→0 ✅(正是这一步要成功)
            //   ③ `catch` 里第二次 `Reset()`          → 配额 0 ⇒ 抛(本测要的那一拍)
            //   配额写 1 会让 ② 就抛,回滚成功、原始异常浮出 ⇒ 本测退化成前置探针,
            //   「遮蔽」面根本没被构造出来(⚠️ 这是**写夹具时的推演复算**,不是实跑结论;
            //   首版确实写成了 1,在落盘前按上述时序自己算出来改掉)。
            catalog2.RevokeAfterLookups(ModuleBed, 2);

            World world2 = NewFlatWorld(BaselineSlots());
            world2.ApplyBakedInitial(catalog2);
            Assert.AreEqual(2, world2.BakedInitialStructureCount, "前置:基线须落 2 件");

            var ex = Assert.Throws<InvalidOperationException>(
                () => new StructureRestorer(world2, catalog2).RebuildFromDecoded(BrokenReplayOps()));

            // ── 判据:原始异常 vs 回滚异常,靠**前缀**分辨 ──
            StringAssert.Contains("重放第 0 条", ex.Message,
                "调用方须看到**原始**异常(重放失败的原因)。报文实值里没有「重放第 0 条」"
                + " ⇒ 回滚失败的那条把根因顶掉了(报错把读者指向基线/目录,而真正的病因是流里的 moduleId)");
            StringAssert.Contains("moduleId=999", ex.Message,
                "原始异常须带上真正的病因(流里那个不在目录内的模块号)");
            StringAssert.DoesNotContain("槽位 (", ex.Message,
                "报文不得是回滚侧的「槽位 (…)」—— 那是**回滚失败**,不是读档失败的原因;"
                + "两者同类型同短语,只能靠这处前缀分辨");

            // ── 世界仍须停在确定的空态(回滚虽失败,`World.Reset` 自己的 catch 清了)──
            Assert.AreEqual(0, world2.Structures.Count, "回滚失败后实例表仍须为空");
            Assert.AreEqual(0, world2.OccupiedCount, "回滚失败后占用表仍须全零");
            Assert.AreEqual(0, world2.BakedInitialStructureCount, "回滚失败后计数须归零");
        }

        /// <summary>B-1' / B-1q 共用的两件基线槽位 —— 第一件 <see cref="ModuleBunk"/>、第二件 <see cref="ModuleBed"/>。</summary>
        private static BuildSlotCatalog BaselineSlots()
        {
            var slots = new BuildSlotCatalog();
            slots.RegisterSlot(new BuildSlot(new WorldPos(1, 0, 1),
                BitMask.Single(SlotType.Decor), bakedInitial: true, moduleId: ModuleBunk));
            slots.RegisterSlot(new BuildSlot(new WorldPos(3, 0, 1),
                BitMask.Single(SlotType.Decor), bakedInitial: true, moduleId: ModuleBed));
            return slots;
        }

        /// <summary>一条**首条就坏**的重放序列(`moduleId` 不在目录内)⇒ `FootprintOf` 立刻抛。</summary>
        private static StructureRestoreOp[] BrokenReplayOps()
            => new[] { StructureRestoreOp.Placed(100, new WorldPos(2, 0, 2), ModuleMissing, 0, 0) };

        // ══════════ 夹具 ══════════

        /// <summary>第二件开档家具(与 <see cref="ModuleBunk"/> 不同号)—— 让基线有「第一件成功、第二件抛」的形态。</summary>
        private const int ModuleBed = 8;

        /// <summary>两格足迹的模块定义(与 `Setup` 里 <see cref="ModuleBunk"/> 同形,可换号)。</summary>
        private static ModuleDefinition BunkDefinition(int moduleId)
            => new ModuleDefinition(
                moduleId: moduleId,
                slotType: SlotType.Decor,
                localOccupancy: new List<WorldPos> { new WorldPos(0, 0, 0), new WorldPos(1, 0, 0) },
                validOrientations: new[] { 0, 90, 180, 270 },
                cost: 4,
                buildItemKey: 0,
                refundable: 1);

        /// <summary>全 walkable 的 5×1×5 平世界(无 POI / 无资源),槽位骨架由调用方给。</summary>
        private static World NewFlatWorld(BuildSlotCatalog slots)
        {
            var terrain = new TerrainCell[Size, 1, Size];
            for (int x = 0; x < Size; x++)
                for (int z = 0; z < Size; z++)
                    terrain[x, 0, z] = new TerrainCell(true, 0);
            int total = Size * 1 * Size;
            var poi = new int[total];
            var resource = new int[total];
            for (int i = 0; i < total; i++) { poi[i] = -1; resource[i] = -1; }
            return new World(new WorldGeometry(terrain, poi, resource), slots);
        }

        /// <summary>
        /// 可「撤掉」某个模块的目录桩 —— 这是让 <see cref="World.Reset"/> 的 <c>catch</c>
        /// **可达**的唯一手段:生产注入的是接口,任何注入方都能在两次 `Reset()` 之间改其返回。
        /// </summary>
        private sealed class RevocableModuleCatalog : IModuleCatalog
        {
            private readonly Dictionary<int, ModuleDefinition> _modules = new Dictionary<int, ModuleDefinition>();

            /// <summary>被撤掉的模块号;`-1` = 不撤。</summary>
            public int RevokedModuleId = -1;

            /// <summary>
            /// 「还允许被查到几次」—— 模块号 → 剩余次数。查一次减一,归零后视同撤掉。
            /// <para>用途:构造「**同一次调用内**目录前后不一致」的时序 —— `RevokedModuleId`
            /// 只能表达「全程坏」,而 <see cref="StructureRestorer.RebuildFromDecoded"/> 的
            /// <c>catch</c> 需要「首次 `Reset()` 时目录还好、第二次 `Reset()` 时已坏」。</para>
            /// </summary>
            private readonly Dictionary<int, int> _lookupsLeft = new Dictionary<int, int>();

            public void RegisterModule(ModuleDefinition def) => _modules[def.ModuleId] = def;

            /// <summary>让 <paramref name="moduleId"/> 只在被查 <paramref name="allowedLookups"/> 次之内可查。</summary>
            public void RevokeAfterLookups(int moduleId, int allowedLookups)
                => _lookupsLeft[moduleId] = allowedLookups;

            public ModuleDefinition? GetModuleDefinition(int moduleId)
            {
                if (moduleId == RevokedModuleId) return null;

                if (_lookupsLeft.TryGetValue(moduleId, out int left))
                {
                    if (left <= 0) return null;      // 配额用尽 ⇒ 视同撤掉
                    _lookupsLeft[moduleId] = left - 1;
                }

                return _modules.TryGetValue(moduleId, out ModuleDefinition def)
                    ? def
                    : (ModuleDefinition?)null;
            }

            public bool ContainsModule(int moduleId) => _modules.ContainsKey(moduleId);

            public IReadOnlyCollection<int> GetAllModuleIds() => _modules.Keys;
        }

        private sealed class CapturingSink : IEventSink
        {
            private readonly List<SimEvent> _sink;
            public CapturingSink(List<SimEvent> sink) { _sink = sink; }
            public void Append(in SimEvent e) => _sink.Add(e);
        }
    }
}
