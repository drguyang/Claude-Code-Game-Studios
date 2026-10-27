// S20-18 · World 生命周期轮 —— **Boot 侧**夹具(装配接线 + 端到端重建)。
//
// 权威来源:
//   · `production/sprints/sprint-20.md` §十 S20-18 的 AC-8 / AC-9
//   · ADR-009 §四(派生态:由事件流确定性重建,不持状态、不写流)
//   · ADR-025 §①(`Sim` 够不着 `Sim.Codec` ⇒ 解码留在 Boot 层,重放本体在 Sim)
//   · ADR-029 / ADR-031 甲案(写侧交已编码 `PayloadRef`,读侧收已解码字段)
//
// ── 判据为什么这么写 ────────────────────────────────────────────────────────
// · AC-9 的**判别力来源**是「世界先被弄脏」:若只在一个空世界上下重放,
//   「重建成功」与「什么都不做」observably 相同。故每个腿都先用**第二个写者**
//   (事件丢进 `DiscardingSink`,不进流)往同一个世界放一条**不在流里**的结构 ——
//   重放后它必须**消失**,否则 AC 未落地。
// · AC-9 的**端到端**是真的端到端:`StructureWriter`(生产编码路径)→ `SimEvent` 列表
//   → `PayloadDecoder`(生产解码路径)→ `StructureRestorer`(生产重放路径)→ `World`。
//   没有一处是测试手搓的字节或手搓的 op。
// · AC-8 的两条腿用**同一个** `Assemble` 生产入口,只差 `world` 传不传 ——
//   反向腿钉的是「既有 100+ 调用方零行为变化」。

using System;
using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.ModularBuilding
{
    /// <summary>
    /// S20-18:`WorldLoadService` 的装配接线(AC-8)与世界流端到端重建(AC-9)。
    /// </summary>
    [TestFixture]
    public class world_load_service_test
    {
        /// <summary>两格足迹(L 形):使「非锚点足迹格」与「旋转换格」都可观测。</summary>
        private const int ModuleBunk = 7;

        private const int Size = 5;

        private World _world;
        private InMemoryBlobPool _pool;
        private IPayloadEncoder _encoder;
        private IPayloadDecoder _decoder;
        private TestModuleCatalog _catalog;
        private List<SimEvent> _stream;
        private StructureWriter _writer;

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

            _pool = new InMemoryBlobPool();          // 同一池既作编码写面,又作解码读面
            _encoder = new PayloadEncoder(_pool);
            _decoder = new PayloadDecoder(_pool);

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

            _stream = new List<SimEvent>();
            var sink = new CapturingSink(_stream);
            _writer = new StructureWriter(
                sink, _world.Structures, _encoder, _catalog, _world,
                DaYiJingCheng.Tests.TestDropWriter.Create(sink));
        }

        private WorldLoadService NewService(IReadOnlyList<SimEvent> events = null)
            => new WorldLoadService(() => events ?? _stream, _decoder, _world, _catalog);

        /// <summary>
        /// 往世界放一条**不在流里**的结构(模拟「上一局的残留」)。
        /// 事件被丢弃 ⇒ 重放后它必须消失,否则「重置」没发生。
        /// </summary>
        private long PlantStructureOutsideStream(WorldPos anchor, int orientation = 0)
        {
            var discard = new DiscardingSink();
            var stray = new StructureWriter(
                discard, _world.Structures, _encoder, _catalog, _world,
                DaYiJingCheng.Tests.TestDropWriter.Create(discard));
            return stray.Place(anchor, ModuleBunk, orientation, 0, tick: 999);
        }

        // ══════════ AC-8:装配接线(给 world ⇒ 非 null;不给 ⇒ null)══════════

        [Test]
        public void test_ac8_assembleWithWorldAndSavePath_yieldsWorldLoad()
        {
            string dir = Path.Combine(Path.GetTempPath(), "s20_18_ac8_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                World world = MinimalWorldFactory.Create();

                CompositionRootServices services =
                    CompositionRoot.Assemble(world: world, saveBasePath: dir);

                Assert.IsNotNull(services.WorldLoad,
                    "给了 world ⇒ 世界读档重建服务须被构造(否则读档后旧世界仍留在原地)");
                Assert.AreSame(world, services.World, "服务袋须交回同一个世界实例");
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }

        [Test]
        public void test_ac8b_assembleWithoutWorld_yieldsNullWorldLoad()
        {
            CompositionRootServices services = CompositionRoot.Assemble();

            Assert.IsNull(services.WorldLoad,
                "未注入 world ⇒ 世界重建服务须为 null(既有 100+ 调用方零行为变化)");
        }

        /// <summary>
        /// 行为记录(非 AC):`world` 有、`saveBasePath` 无 ⇒ 服务**仍**被构造。
        /// <para>钉的是「服务的存在性只取决于 world」这一条 —— 若将来改成「有存档目录才造」,
        /// 本测转红,届时须同步改卡里的「挂标不记绿 #3(`RequestLoad` 仍零调用方)」叙事。</para>
        /// </summary>
        [Test]
        public void test_ac8c_assembleWithWorldButNoSavePath_stillYieldsWorldLoad()
        {
            World world = MinimalWorldFactory.Create();

            CompositionRootServices services = CompositionRoot.Assemble(world: world);

            Assert.IsNotNull(services.WorldLoad,
                "服务存在性只取决于 world;读档触发点(BootRoot.RequestLoad)另归接线轮");
            Assert.IsNull(services.SaveService, "未给 saveBasePath ⇒ 存档服务仍为 null(既有行为)");
        }

        [Test]
        public void test_ac8d_noDecoder_throws()
        {
            var ex = Assert.Throws<ArgumentNullException>(
                () => new WorldLoadService(() => new List<SimEvent>(), null, _world, _catalog));

            Assert.AreEqual("decoder", ex.ParamName);
            StringAssert.Contains("IPayloadDecoder", ex.Message,
                "异常须点名缺的是解码器 —— 空投影会伪装成「世界为空」");
        }

        [Test]
        public void test_ac8e_noEventsSource_throws()
        {
            var ex = Assert.Throws<ArgumentNullException>(
                () => new WorldLoadService(null, _decoder, _world, _catalog));

            Assert.AreEqual("events", ex.ParamName);
        }

        [Test]
        public void test_ac8f_noWorld_throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => new WorldLoadService(() => new List<SimEvent>(), _decoder, null, _catalog));
        }

        [Test]
        public void test_ac8g_noModuleCatalog_throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => new WorldLoadService(() => new List<SimEvent>(), _decoder, _world, null));
        }

        // ══════════ AC-9:端到端重建 ══════════

        [Test]
        public void test_ac9_streamWithPlaced_rebuildsWorldToStreamShape()
        {
            // ── Arrange:流里两条(一条朝向 0、一条朝向 90),世界另有一条**不进流**的残留 ──
            long id1 = _writer.Place(new WorldPos(1, 0, 1), ModuleBunk, 0, 0, tick: 1);
            long id2 = _writer.Place(new WorldPos(1, 0, 3), ModuleBunk, 90, 0, tick: 2);
            var strayAnchor = new WorldPos(3, 0, 3);
            long strayId = PlantStructureOutsideStream(strayAnchor);

            Assert.AreEqual(2, _stream.Count, "前置:流里恰两条 StructurePlaced");
            Assert.AreEqual(3, _world.Structures.Count, "前置:世界里三条(含残留)");
            Assert.AreEqual(6, _world.OccupiedCount, "前置:三条 × 两格 = 6");
            Assert.IsFalse(_world.IsEffectivelyWalkable(strayAnchor), "前置:残留结构占着格");

            // ── Act ──
            int applied = NewService().Rebuild();

            // ── Assert:世界恢复到流所述形态(残留必须消失)──
            Assert.AreEqual(2, applied, "须恰重放流里的两条");
            Assert.AreEqual(2, _world.Structures.Count, "残留结构须被重置掉(否则「重置」没发生)");
            Assert.IsTrue(_world.Structures.TryGet(id1, out StructureInstance inst1), "id1 须按原号重建");
            Assert.IsTrue(_world.Structures.TryGet(id2, out StructureInstance inst2), "id2 须按原号重建");
            Assert.IsFalse(_world.Structures.TryGet(strayId, out _), "残留 id 不得留在实例表内");
            Assert.IsTrue(_world.IsEffectivelyWalkable(strayAnchor), "残留的足迹格须恢复可走");

            Assert.AreEqual(0, inst1.Orientation);
            Assert.AreEqual(90, inst2.Orientation, "朝向须按载荷还原(不是一律 0)");
            CollectionAssert.AreEquivalent(
                new[] { new WorldPos(1, 0, 1), new WorldPos(2, 0, 1) }, inst1.OccupiedCells);
            CollectionAssert.AreEquivalent(
                new[] { new WorldPos(1, 0, 3), new WorldPos(1, 0, 4) }, inst2.OccupiedCells,
                "朝向 90 的足迹须是旋转后的(载荷 + 目录重算,不是抄流)");
            Assert.AreEqual(4, _world.OccupiedCount, "四条足迹格");

            Assert.IsFalse(_world.IsEffectivelyWalkable(new WorldPos(2, 0, 1)));
            Assert.IsFalse(_world.IsEffectivelyWalkable(new WorldPos(1, 0, 4)));
        }

        [Test]
        public void test_ac9b_emptyStream_clearsWorld()
        {
            // ── Arrange:世界里有东西,流是空的 ──
            PlantStructureOutsideStream(new WorldPos(1, 0, 1));
            Assert.AreEqual(1, _world.Structures.Count, "前置:世界非空");
            Assert.AreEqual(2, _world.OccupiedCount, "前置:占用表非空");

            // ── Act ──
            int applied = NewService(new List<SimEvent>()).Rebuild();

            // ── Assert ──
            Assert.AreEqual(0, applied, "空流 ⇒ 零条重放");
            Assert.AreEqual(0, _world.Structures.Count,
                "空流 ⇒ 世界被**清空**(不是保持原样 —— 旧状态冒充新存档正是本件要消灭的失败模式)");
            Assert.AreEqual(0, _world.OccupiedCount);
            Assert.IsTrue(_world.IsEffectivelyWalkable(new WorldPos(1, 0, 1)));
        }

        [Test]
        public void test_ac9c_streamWithModified_appliesRotatedFootprint()
        {
            // ── Arrange:流 = Placed + Modified(写者把绝对新朝向放进载荷)──
            var anchor = new WorldPos(1, 0, 1);
            long id = _writer.Place(anchor, ModuleBunk, 0, 0, tick: 1);
            Assert.IsTrue(_writer.Modify(id, newOrientation: 90, newVariant: null, tick: 2),
                "前置:朝向变化 ⇒ Modify 须发事件");
            Assert.AreEqual(2, _stream.Count, "前置:流 = 放置 + 修改");

            // ── Act ──
            int applied = NewService().Rebuild();

            // ── Assert ──
            Assert.AreEqual(2, applied);
            Assert.IsTrue(_world.Structures.TryGet(id, out StructureInstance inst));
            Assert.AreEqual(90, inst.Orientation, "重放须读载荷里的**绝对**新朝向");
            CollectionAssert.AreEquivalent(
                new[] { anchor, new WorldPos(1, 0, 2) }, inst.OccupiedCells,
                "足迹须按旋转后朝向重算(旧足迹须已释放)");
            Assert.IsTrue(_world.IsEffectivelyWalkable(new WorldPos(2, 0, 1)),
                "旋转前的第二格须恢复可走");
            Assert.IsFalse(_world.IsEffectivelyWalkable(new WorldPos(1, 0, 2)),
                "旋转后的第二格须不可走");
            Assert.AreEqual(2, _world.OccupiedCount);
        }

        [Test]
        public void test_ac9d_streamWithPlaceThenRemove_leavesNothingBehind()
        {
            // ── Arrange ──
            var anchor = new WorldPos(1, 0, 1);
            long id = _writer.Place(anchor, ModuleBunk, 0, 0, tick: 1);
            Assert.IsTrue(_writer.Remove(id, tick: 2,
                demolishRefundRatio: DaYiJingCheng.Tests.TestDropWriter.P0RefundRatio),
                "前置:拆除须成功");

            // ── Act ──
            int applied = NewService().Rebuild();

            // ── Assert ──
            Assert.AreEqual(2, applied, "流里三条事件中与建造投影有关的是两条(放置 + 拆除)");
            Assert.AreEqual(0, _world.Structures.Count);
            Assert.AreEqual(0, _world.OccupiedCount);
            Assert.IsTrue(_world.IsEffectivelyWalkable(anchor));
            Assert.IsTrue(_world.IsEffectivelyWalkable(new WorldPos(2, 0, 1)));
        }

        [Test]
        public void test_ac9e_streamWithForeignKinds_skippedWithoutSideEffects()
        {
            // ── Arrange:流里混入一条**非建造** Kind(`DropSpawned` 由拆除返还产生)──
            var anchor = new WorldPos(1, 0, 1);
            long id = _writer.Place(anchor, ModuleBunk, 0, 0, tick: 1);
            _writer.Remove(id, tick: 2,
                demolishRefundRatio: DaYiJingCheng.Tests.TestDropWriter.P0RefundRatio);

            int kinds = _stream.Count;
            int buildKinds = 0;
            foreach (SimEvent e in _stream)
                if (e.Kind == EventKind.StructurePlaced
                    || e.Kind == EventKind.StructureRemoved
                    || e.Kind == EventKind.StructureModified) buildKinds++;

            Assert.Greater(kinds, buildKinds,
                "前置:流里须有至少一条非建造 Kind(否则本条判不出「跳过」)");

            // ── Act ──
            int applied = NewService().Rebuild();

            // ── Assert:重放条数 = 建造 Kind 数(其它支被显式跳过,不落状态也不抛)──
            Assert.AreEqual(buildKinds, applied,
                "非建造 Kind 须被跳过(不得计进 applied,也不得抛)");
            Assert.AreEqual(0, _world.Structures.Count);
            Assert.AreEqual(0, _world.OccupiedCount);
        }

        [Test]
        public void test_ac9f_secondRebuild_startsFromCleanSlate()
        {
            // ── Arrange:流里一条;先重建一次(世界 = 流所述形态)──
            _writer.Place(new WorldPos(1, 0, 1), ModuleBunk, 0, 0, tick: 1);
            WorldLoadService service = NewService();
            service.Rebuild();
            int countAfterFirst = _world.Structures.Count;
            int occupiedAfterFirst = _world.OccupiedCount;

            // 再往世界里塞一条**不在流里**的结构(=「上次读档之后的玩法痕迹」)
            var strayAnchor = new WorldPos(3, 0, 3);
            PlantStructureOutsideStream(strayAnchor);
            Assert.AreEqual(countAfterFirst + 1, _world.Structures.Count,
                "前置:世界已被弄脏(否则本条的判据与「什么都不做」observably 相同)");

            // ── Act:第二次重建 ──
            service.Rebuild();

            // ── Assert:第二次重建**从零开始**(不叠在第一次的结果上,也不留脏结构)──
            // ⚠️ 判别力设计(2026-10-28 订正):首版本条只「连跑两次比数值」—— 那对
            //    **任何**流的确定性函数都恒成立 ⇒ 零判别力(证伪轮抓不到它,是假绿的)。
            //    现改为「第一次重建后弄脏、再重建」,钉的是 `RebuildFromDecoded` 开头那次
            //    `_world.Reset()`:没有它,脏结构会跨过第二次重建存活(读档两次 = 叠加)。
            Assert.AreEqual(countAfterFirst, _world.Structures.Count,
                "第二次重建须先清世界 —— 否则两次读档会叠加,脏结构还会冒充存档内容");
            Assert.AreEqual(occupiedAfterFirst, _world.OccupiedCount);
            Assert.IsTrue(_world.IsEffectivelyWalkable(strayAnchor),
                "脏结构的足迹须被清掉(否则它继续挡路)");
        }

        // ══════════ 夹具 ══════════

        /// <summary>记事件的 sink(`StructureWriter` 要求 `IEventSink`)。</summary>
        private sealed class CapturingSink : IEventSink
        {
            private readonly List<SimEvent> _sink;
            public CapturingSink(List<SimEvent> sink) { _sink = sink; }
            public void Append(in SimEvent e) { _sink.Add(e); }
        }

        /// <summary>
        /// 丢弃事件的 sink —— 用来制造「世界里有、流里没有」的残留。
        /// <para>⚠️ 这不是 Fake 生产件,而是**测试对「上一局的流已经不在了」这一情形的建模**:
        /// 写者照常工作、事件照常发出,只是不落进本测喂给重放器的那条流。</para>
        /// </summary>
        private sealed class DiscardingSink : IEventSink
        {
            public void Append(in SimEvent e) { }
        }
    }
}
