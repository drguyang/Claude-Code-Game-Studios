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
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;
using DaYiJingCheng.Tests.DiseaseSimulation;   // FakeIdAuthority / FakePresenceQuery
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

        // ══════════ 真存档字节往返(结清 `review-s20-20` §七-2)══════════

        /// <summary>
        /// **世界结构**经 `SaveService` 真落盘 → 新流读回 → `WorldLoadService` 重建 ⇒
        /// 世界形态与落盘前逐项相同。
        /// </summary>
        /// <remarks>
        /// <para><b>补的是什么(§七-2 原文)</b>:「`WorldLoadService` 的端到端只在夹具层验过,
        /// 真实存档字节流未过」。既有 `test_ac9_*` 系列的事件是**内存构造的 `SimEvent` 列表**,
        /// 从头到尾没有字节落过盘;`save_replay_roundtrip_test` 走真文件但只覆盖**病史流**
        /// (病人 onset / outcome / 折叠),**零建造事件**。本测把两半边接起来。</para>
        ///
        /// <para><b>为什么必须走真文件而不是内存往返</b>:存档字节流是**唯一真源**的载体。
        /// 内存往返测的是「对象引用还在」,真文件往返测的是「字节里真的写了足够重建的信息」——
        /// 后者才会暴露「codec 少写一个字段」「头部偏移错位」这类只有落盘才现形的缺陷。</para>
        ///
        /// <para>⚠️ <b>本测证明不了「跨进程」</b> —— 载荷 blob 池是**进程内存对象**
        /// (`InMemoryBlobPool`,生产装配根 `CompositionRoot.cs:132` 就是 `new InMemoryBlobPool()`),
        /// 而存档字节只携带 `PayloadRef` 三元组
        /// (`BlobId/Offset/Length`,ADR-006 Amendment G-2 的 blob 池寻址),
        /// **不携带载荷字节本身** —— `SaveComposer` / `SaveService` 全文零 blob 引用,
        /// ADR-010 §一 的 v1 布局(头部 + 事件段 + 折叠段)也**没有载荷段**。
        /// ⇒ 本测刻意让**同一个池实例**跨「重启」存活,模拟「池仍在内存」。
        /// 这是当前实现的真实边界:**跨进程读档须先裁「载荷字节存哪」**,
        /// 那不在本测面内 —— 登记为 **`TD-persist-09`**(归属 `persistence-service`,
        /// 不是 23:缺口在存档布局与池实现,不在建造投影)。</para>
        /// <para>本测**下方那段边界实测**把这条边界从「注释里的声明」变成「跑出来的断言」:
        /// 空池 + 同一份存档 ⇒ 抛,且报文点名 `BlobId`。</para>
        /// <para>🔁 <b>2026-10-28 ADR-034 就地加注(上面两段原文不删 —— 它们记录的是裁定前的真实边界)</b>:
        /// v2 起存档**带载荷段**,`SaveComposer` 组版时把存活引用的 blob 字节写进末段,
        /// `SaveService.Load` 装进注入的 `IBlobRestoreSink`。⇒ 主流程那两处 `blobRestoreSink: _pool`
        /// 依然成立(复用同一个池仍能读回),而**下方那段边界实测已就地翻转**为
        /// 「**全新空池** + 同一份存档 ⇒ 读档装回后 `Rebuild` 成功、朝向逐位保持」。
        /// 原段自陈「`TD-persist-09` 兑现时本段会转红,届时须改成本测的反面」—— 本段即那一反面。</para>
        ///
        /// <para><b>判别力(证伪轮实测,不是推演)</b>:</para>
        /// <para>· **M10**(`SimEventCodec.Encode` 把 `e.Payload` 写成 `default`)⇒ **恰 1 条红,正是本测**
        /// (报文 `StructurePlaced: 缺字段(已见 mask=0x0,应为 0x1F)`)。`test_ac9_*` 系列是**内存构造的
        /// `SimEvent`**,不经 codec ⇒ 它们照样绿。**这是本测的独占面:字节里真的带了载荷引用。**</para>
        /// <para>· **M9**(注掉 `RebuildFromDecoded` 开头的 `_world.Reset()`)⇒ 本测红,但**不独占**
        /// (同轮另有 `ac9` / `ac9b` / `ac9f` 三条红)。「残留被清」这面旧夹具也覆盖。</para>
        /// <para>· **M11**(`PayloadCodec.Encode(in StructurePlacedPayload)` 把 `p.Orientation` 写成 `0`)
        /// ⇒ 本测红,但**不独占**(`ac9` 同红,它走生产编码路径)。</para>
        /// <para>⇒ **诚实结论:本测的独占判别力 = M10 那一条。** 另两面它参与但不独占 ——
        /// 不得把「本测红了」当成「这两面只有它能抓」。</para>
        /// </remarks>
        [Test]
        public void test_ac9g_realSaveBytes_roundTripRebuildsWorld()
        {
            string dir = Path.Combine(Path.GetTempPath(),
                                      "dyjq_worldload_roundtrip_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                // ══ Arrange:落盘前 —— 两条建造事件进流(朝向 0 与 90,覆盖旋转分支)══
                long id1 = _writer.Place(new WorldPos(1, 0, 1), ModuleBunk, 0, 0, tick: 1);
                long id2 = _writer.Place(new WorldPos(1, 0, 3), ModuleBunk, 90, 0, tick: 2);

                Assert.AreEqual(2, _stream.Count, "前置:流里恰两条建造事件");
                Assert.AreEqual(2, _world.Structures.Count, "前置:世界里恰两条");
                Assert.AreEqual(4, _world.OccupiedCount, "前置:两条 × 两格 = 4");

                // ══ Act ①:Checkpoint 真落盘 ══
                var saveStream = new EventStream(new FakeIdAuthority(), new FakePresenceQuery());
                foreach (SimEvent e in _stream) saveStream.Append(e);

                var svc = new SaveService(
                    dir, new SaveCodec(),
                    folded: AlwaysFolded(),
                    narrativeContext: FuncNarrativeContextQuery.AlwaysIn,
                    replaySink: _ => { },
                    foldedRowSink: _ => { },
                    compose: _ => SaveComposer.Compose(
                        saveStream.Events, AlwaysFolded(), _decoder, new SaveCodec(),
                        worldSeed: 1UL, configVersion: 0U, tick: 2L,
                        existingFoldedRows: saveStream.FoldedRows, blobPool: _pool),
                    writeScheduler: action => action(), blobRestoreSink: _pool);
                svc.Checkpoint(new SaveSlot(0));

                string savePath = Path.Combine(dir, "save_0.bin");
                Assert.That(File.Exists(savePath), Is.True, "Checkpoint 须真的落出一个文件");
                long sizeOnDisk = new FileInfo(savePath).Length;
                Assert.That(sizeOnDisk, Is.GreaterThan(SaveHeaderCodec.HeaderSize),
                    $"存档须大于头部({sizeOnDisk} B)");

                // ══ Act ②:「重启」—— 新流 + 同目录 Load ══
                // ⚠️ blob 池**刻意复用同一实例**(见上方 remarks):存档只带 PayloadRef,
                //    不带载荷字节。跨进程边界不在本测面内。
                var fresh = new EventStream(new FakeIdAuthority(), new FakePresenceQuery());
                IReadOnlyList<SimEvent> pending = null;
                var svcLoad = new SaveService(
                    dir, new SaveCodec(),
                    folded: AlwaysFolded(),
                    narrativeContext: FuncNarrativeContextQuery.AlwaysIn,
                    replaySink: events => pending = events,
                    foldedRowSink: bytes => fresh.Restore(
                        pending ?? throw new InvalidOperationException("折叠段先于事件段到达"),
                        FoldRowCodec.DecodeSegment(bytes)),
                    compose: _ => SaveComposer.Compose(
                        fresh.Events, AlwaysFolded(), _decoder, new SaveCodec(),
                        worldSeed: 1UL, configVersion: 0U, tick: 2L,
                        existingFoldedRows: fresh.FoldedRows, blobPool: _pool),
                    writeScheduler: action => action(), blobRestoreSink: _pool);
                svcLoad.Load(new SaveSlot(0));

                Assert.AreEqual(2, fresh.Count, "读回的事件条数须 == 落盘的条数");

                // ══ Act ③:从读回的流重建**一个新世界** ══
                // ⚠️ **为什么必须换新 World 实例**(而不是复用 `_world`)——这是本测判别力的
                //    核心。`_world` 上已经有那两件结构(`Act ①` 里写者放进去的),
                //    若在它上面重建,「重建成功」与「重建根本没跑」在**实例表/占用表上
                //    完全同形** ⇒ 断言全绿而什么都没测(「靶点对但靶场里没有靶」)。
                //    换新世界后,那两件必须**由字节往返 + 重放**重新长出来。
                World rebuilt = NewFlatWorld(new BuildSlotCatalog());

                // 再往新世界里放几条**不在存档里**的残留 ⇒ 重建须把它们清掉。
                // (重建本体开头的 `Reset()` 是这些断言的唯一守卫。)
                //
                // ⚠️ **为什么先放两条 dummy**(2026-10-28 实跑抓到的自伤):
                //    新世界的注册表从 1 起号,而存档里的 id 也是 1、2 ⇒ 若残留直接拿
                //    `strayId = 1`,它**与重放出来的 id1 撞号**,`TryGet(strayId)` 会返回
                //    id1 的实例 ⇒ 「残留不得存活」这条断言变成**不可判**(红在撞号,不在残留)。
                //    两条 dummy 把水位推到 3,残留拿 4,与 {id1=1, id2=2} 不相交。
                var strayAnchor = new WorldPos(3, 0, 3);
                PlantOutsideStream(rebuilt, new WorldPos(3, 0, 0));   // dummy, id = 1
                PlantOutsideStream(rebuilt, new WorldPos(4, 0, 0));   // dummy, id = 2
                long strayId = PlantOutsideStream(rebuilt, strayAnchor); // 残留, id = 3
                Assert.Greater(strayId, id2,
                    $"前置:残留 id({strayId}) 须大于存档里的最大 id({id2}) —— " +
                    "否则下面「残留不得存活」会因撞号而不可判");
                Assert.AreEqual(3, rebuilt.Structures.Count, "前置:新世界里有三条残留");
                Assert.IsFalse(rebuilt.IsEffectivelyWalkable(strayAnchor), "前置:残留占着格");

                int applied = new WorldLoadService(() => fresh.Events, _decoder, rebuilt, _catalog)
                                  .Rebuild();

                // ══ Assert:新世界形态 == 落盘前的旧世界 ══
                Assert.AreEqual(2, applied, "须恰重放两条(与落盘前同数)");
                Assert.AreEqual(2, rebuilt.Structures.Count,
                    "实例表条数须由**字节往返**重建 —— 三条残留须被清掉");
                Assert.AreEqual(4, rebuilt.OccupiedCount, "占用格数须还原");
                Assert.IsFalse(rebuilt.Structures.TryGet(strayId, out _),
                    "存档外的残留不得存活(否则「重建」没发生)");
                Assert.IsTrue(rebuilt.IsEffectivelyWalkable(strayAnchor), "残留的足迹格须恢复可走");

                Assert.IsTrue(rebuilt.Structures.TryGet(id1, out StructureInstance r1),
                    $"id1={id1} 须按原号重建(发号经字节往返不变)");
                Assert.IsTrue(rebuilt.Structures.TryGet(id2, out StructureInstance r2),
                    $"id2={id2} 须按原号重建");

                Assert.AreEqual(0, r1.Orientation, "id1 朝向须为 0");
                Assert.AreEqual(90, r2.Orientation,
                    "id2 朝向须为 90 —— 若 codec 丢了 orientation,这里会变 0");
                Assert.AreEqual(new WorldPos(1, 0, 1), r1.Anchor, "id1 锚点须原样");
                Assert.AreEqual(new WorldPos(1, 0, 3), r2.Anchor, "id2 锚点须原样");
                CollectionAssert.AreEquivalent(
                    new[] { new WorldPos(1, 0, 1), new WorldPos(2, 0, 1) }, r1.OccupiedCells,
                    "id1 足迹须为旋转前形态");
                CollectionAssert.AreEquivalent(
                    new[] { new WorldPos(1, 0, 3), new WorldPos(1, 0, 4) }, r2.OccupiedCells,
                    "id2 足迹须为旋转后形态(朝向 90 ⇒ (0,1) 方向)");

                Assert.IsFalse(rebuilt.IsEffectivelyWalkable(new WorldPos(1, 0, 1)),
                    "重建后占用格须不可走");
                Assert.IsFalse(rebuilt.IsEffectivelyWalkable(new WorldPos(1, 0, 4)),
                    "重建后占用格须不可走(旋转腿)");
                Assert.IsTrue(rebuilt.IsEffectivelyWalkable(new WorldPos(2, 0, 3)),
                    "未占用的格须可走(否则「全不可走」也会让上面两条绿)");

                // 高水位须按重放抬回 ⇒ 下一个号 = max(id)+1
                Assert.AreEqual(id2 + 1, rebuilt.Structures.PeekNextId,
                    "读档后高水位须 == max(id)+1(ADR-006 Amendment B 同型)");

                // ══ 交叉校验:旧世界仍在原地(证明重建作用在新实例上,没偷改 `_world`)══
                Assert.AreEqual(2, _world.Structures.Count, "旧世界不受影响(本测不测它的重建)");

                // ══════════ 跨进程边界 —— **ADR-034 §②/§④ 已闭合**(2026-10-28 就地翻转)══════════
                //
                // 🔁 **原文(裁定前,保留作闭环记录)**:「生产装配根今日 = `CompositionRoot.cs:132`
                //    的 `new InMemoryBlobPool()` ⇒ 进程重启后池是空的,而存档只带 `PayloadRef`
                //    三元组、不带载荷字节 ⇒ 上面那段之所以能绿,靠的是『同一个池实例跨重启存活』
                //    这个人造前提。本段把该前提拿掉,证明它的确是前提:空池 + 同一份存档 ⇒ 抛,
                //    且报文点名池。**这不是借绿 —— 它是正向断言一个已知失败**:`TD-persist-09`
                //    兑现时本段会转红(届时须改成本测的反面)。」
                //
                // **本段即那一反面**:v2 存档带载荷段 ⇒ 上面那条「转红」的预言已兑现。
                // 判据 = 全新空池 + 同一份存档 ⇒ Load 装回 ⇒ Rebuild 成功,且**载荷内容**
                // (朝向 90)逐位保持 —— 只断「不抛」不足以排除「装错号 / 截断」。
                var restoredPool = new InMemoryBlobPool();
                Assert.AreNotSame(_pool, restoredPool, "前提:装回用的是**全新**池,不是复用");

                var freshForRestored = new EventStream(new FakeIdAuthority(), new FakePresenceQuery());
                IReadOnlyList<SimEvent> pendingRestored = null;
                var svcRestore = new SaveService(
                    dir, new SaveCodec(),
                    folded: AlwaysFolded(),
                    narrativeContext: FuncNarrativeContextQuery.AlwaysIn,
                    replaySink: events => pendingRestored = events,
                    foldedRowSink: bytes => freshForRestored.Restore(
                        pendingRestored ?? throw new InvalidOperationException("折叠段先于事件段到达"),
                        FoldRowCodec.DecodeSegment(bytes)),
                    writeScheduler: action => action(),
                    blobRestoreSink: restoredPool);
                svcRestore.Load(new SaveSlot(0));

                Assert.AreEqual(2, restoredPool.Count,
                    "载荷段须真的装回 2 条 blob(两条建造事件各一条)");

                World rebuiltFromDisk = NewFlatWorld(new BuildSlotCatalog());
                Assert.DoesNotThrow(
                    () => new WorldLoadService(() => freshForRestored.Events,
                                               new PayloadDecoder(restoredPool),
                                               rebuiltFromDisk, _catalog).Rebuild(),
                    "载荷已随存档落盘 ⇒ 全新空池装回后须能重建(ADR-034 §②/§④ 的端到端判据)");

                Assert.AreEqual(2, rebuiltFromDisk.Structures.Count, "装回后重建须长出两件结构");
                Assert.IsTrue(rebuiltFromDisk.Structures.TryGet(id2, out StructureInstance r2b),
                    $"id2={id2} 须由装回的载荷重建");
                Assert.AreEqual(90, r2b.Orientation,
                    "载荷**内容**须逐字节往返 —— 若装错号 / 截断,朝向会变 0(只断「不抛」抓不到)");
            }
            finally
            {
                try { Directory.Delete(dir, recursive: true); } catch { /* 清理失败不掩盖断言 */ }
            }
        }

        /// <summary>
        /// ADR-034 §④ 的 fail-closed:存档**含载荷条目**而读侧未注入 `IBlobRestoreSink` ⇒
        /// `Load` 抛,且报文点名 `blobRestoreSink`。
        /// <para>为什么要单独钉:静默跳过装载**不会红** —— 读档照常返回事件列表,只是每次解码
        /// 都得到「未驻留」。那正是 `TD-persist-09` 原本的症状,静默跳过等于把它藏回去。</para>
        /// </summary>
        [Test]
        public void test_load_payloadsPresentWithoutRestoreSink_isRejected()
        {
            string dir = Path.Combine(Path.GetTempPath(),
                                      "dyjq_worldload_norestore_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                _writer.Place(new WorldPos(1, 0, 1), ModuleBunk, 0, 0, tick: 1);

                var saveStream = new EventStream(new FakeIdAuthority(), new FakePresenceQuery());
                foreach (SimEvent e in _stream) saveStream.Append(e);

                var svcWrite = new SaveService(
                    dir, new SaveCodec(),
                    folded: AlwaysFolded(),
                    narrativeContext: FuncNarrativeContextQuery.AlwaysIn,
                    replaySink: _ => { },
                    foldedRowSink: _ => { },
                    compose: _ => SaveComposer.Compose(
                        saveStream.Events, AlwaysFolded(), _decoder, new SaveCodec(),
                        worldSeed: 1UL, configVersion: 0U, tick: 1L,
                        existingFoldedRows: saveStream.FoldedRows, blobPool: _pool),
                    writeScheduler: action => action());
                svcWrite.Checkpoint(new SaveSlot(0));

                // 读侧**刻意不注入** blobRestoreSink
                var svcLoad = new SaveService(
                    dir, new SaveCodec(),
                    folded: AlwaysFolded(),
                    narrativeContext: FuncNarrativeContextQuery.AlwaysIn,
                    replaySink: _ => { },
                    foldedRowSink: _ => { },
                    writeScheduler: action => action());

                var ex = Assert.Throws<InvalidOperationException>(
                    () => svcLoad.Load(new SaveSlot(0)),
                    "存档带载荷而未注入装载面 ⇒ 必须抛,不得静默跳过");
                StringAssert.Contains("blobRestoreSink", ex.Message,
                    "报文须点名缺的是哪一个形参 —— 否则读者会误读成「存档损坏」");
                StringAssert.Contains("未驻留", ex.Message,
                    "报文须点出静默跳过会造成的症状(解码得到「未驻留」)");
            }
            finally
            {
                try { Directory.Delete(dir, recursive: true); } catch { /* 清理失败不掩盖断言 */ }
            }
        }

        /// <summary>
        /// 建一个 `Size × 1 × Size` 全可走世界 —— 与 `Setup` 同形,供「重启后新世界」使用。
        /// </summary>
        private static World NewFlatWorld(BuildSlotCatalog slots)
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

            return new World(new WorldGeometry(terrain, poi, resource), slots);
        }

        /// <summary>往**指定**世界放一条不进流的结构(本文件的两个世界都要能塞)。</summary>
        private static long PlantOutsideStream(World world, WorldPos anchor)
        {
            var discard = new DiscardingSink();
            var stray = new StructureWriter(
                discard, world.Structures, new PayloadEncoder(new InMemoryBlobPool()), ModuleCatalog,
                world, DaYiJingCheng.Tests.TestDropWriter.Create(discard));
            return stray.Place(anchor, ModuleBunk, 0, 0, tick: 999);
        }

        /// <summary>与 `Setup` 逐字同款的模块目录(静态版,供静态 helper 用)。</summary>
        private static TestModuleCatalog ModuleCatalog
        {
            get
            {
                var catalog = new TestModuleCatalog();
                catalog.RegisterModule(new ModuleDefinition(
                    moduleId: ModuleBunk,
                    slotType: SlotType.Decor,
                    localOccupancy: new List<WorldPos> { new WorldPos(0, 0, 0), new WorldPos(1, 0, 0) },
                    validOrientations: new[] { 0, 90, 180, 270 },
                    cost: 4,
                    buildItemKey: 0,
                    refundable: 1));
                return catalog;
            }
        }

        /// <summary>「永不折叠」谓词 —— 世界流无病人,本测不涉及折叠,给个恒假即可。</summary>
        private static IFoldedPredicate AlwaysFolded() => new NeverFolded();

        private sealed class NeverFolded : IFoldedPredicate
        {
            public bool Folded(int patientId) => false;
        }
    }
}
