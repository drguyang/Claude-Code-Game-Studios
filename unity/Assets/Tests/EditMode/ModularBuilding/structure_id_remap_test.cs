// ADR-033 §Validation V-1…V-7 —— 结构 id 撞号的载入期归一化(纯函数 + 端到端)。
//
// 权威来源:`docs/architecture/adr-033-structure-id-collision-on-load.md`
//
// ── 判据分两层 ──────────────────────────────────────────────────────────────
// · **纯函数层**(`StructureIdRemap`):判据必要性 / 充分性 / 单射 / Alt 3 反例。
//   这一层不需要世界、不需要流 —— 它只吃 `StructureRestoreOp` 序列。
// · **端到端层**(`WorldLoadService.Rebuild`):固化 / 幂等 / 混合存档不变量。
//   这一层必须走真实路径(`EventStream` + `PayloadEncoder` / `PayloadDecoder`),
//   因为「固化」写的正是**事件流**,而被测性质是「写出的存档不再判据命中」。
//
// ⚠️ 端到端层的**判别力来源**:前基线存档(含 `Placed(1)`,而基线占 `1..N`)必须
//   **同时**满足「世界里有基线家具」与「存档里有一条撞号的结构」。缺任一条,
//   「没平移」与「平移了但没观测到」不可分。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;
using DaYiJingCheng.Tests.DiseaseSimulation;   // FakeIdAuthority / FakePresenceQuery
using NUnit.Framework;

namespace DaYiJingCheng.Tests.ModularBuilding
{
    /// <summary>ADR-033 的 V-1…V-7 夹具。</summary>
    [TestFixture]
    public class structure_id_remap_test
    {
        private const int ModuleBunk = 7;
        private const int Size = 5;

        private InMemoryBlobPool _pool;
        private IPayloadEncoder _encoder;
        private IPayloadDecoder _decoder;
        private TestModuleCatalog _catalog;

        [SetUp]
        public void Setup()
        {
            _pool = new InMemoryBlobPool();
            _encoder = new PayloadEncoder(_pool);
            _decoder = new PayloadDecoder(_pool);

            _catalog = new TestModuleCatalog();
            _catalog.RegisterModule(new ModuleDefinition(
                moduleId: ModuleBunk,
                slotType: SlotType.Decor,
                localOccupancy: new List<WorldPos> { new WorldPos(0, 0, 0), new WorldPos(1, 0, 0) },
                validOrientations: new[] { 0, 90, 180, 270 },
                cost: 4,
                buildItemKey: 0,
                refundable: 1));
        }

        // ══════════════════ 构造件 ══════════════════

        /// <summary>造一个**带基线**的世界:基线占 id <c>1..baselineCount</c>(ADR-033 §①)。</summary>
        private World NewWorldWithBaseline(int baselineCount)
        {
            var slots = new BuildSlotCatalog();
            for (int i = 0; i < baselineCount; i++)
                slots.RegisterSlot(new BuildSlot(
                    new WorldPos(i * 2, 0, 0), BitMask.Single(SlotType.Decor),
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
            world.ApplyBakedInitial(_catalog);
            Assert.AreEqual(baselineCount, world.BakedInitialStructureCount,
                "前置:基线件数须落成 N —— 否则判据区间 [1,N] 退化,整组夹具失去判别力");
            return world;
        }

        /// <summary>直接把估值编成世界流事件(**不经写者** —— 前基线存档本就没有写者约束)。</summary>
        private SimEvent Placed(long tick, long structureId, int x, int z, int moduleId = ModuleBunk)
            => new SimEvent(tick, PatientId.None, -1, EventKind.StructurePlaced,
                            _encoder.Encode(EventKind.StructurePlaced,
                                new StructurePlacedPayload(structureId, new WorldPos(x, 0, z),
                                                           moduleId, 0, 0)));

        private SimEvent Removed(long tick, long structureId)
            => new SimEvent(tick, PatientId.None, -1, EventKind.StructureRemoved,
                            _encoder.Encode(EventKind.StructureRemoved,
                                new StructureRemovedPayload(structureId, default, -1)));

        private EventStream NewStream(params SimEvent[] events)
        {
            var stream = new EventStream(new FakeIdAuthority(), new FakePresenceQuery());
            foreach (SimEvent e in events) stream.Append(e);
            return stream;
        }

        /// <summary>从流里解出建造 op 序列(与 `WorldLoadService.Rebuild` 同一解码口径)。</summary>
        private List<StructureRestoreOp> OpsOf(EventStream stream)
        {
            var ops = new List<StructureRestoreOp>();
            foreach (SimEvent e in stream.Events)
            {
                switch (e.Kind)
                {
                    case EventKind.StructurePlaced:
                    {
                        var p = _decoder.Decode<StructurePlacedPayload>(e.Kind, e.Payload);
                        ops.Add(StructureRestoreOp.Placed(p.StructureId, p.Cell, p.ModuleId,
                                                          p.Orientation, p.Variant));
                        break;
                    }
                    case EventKind.StructureRemoved:
                    {
                        var p = _decoder.Decode<StructureRemovedPayload>(e.Kind, e.Payload);
                        ops.Add(StructureRestoreOp.Removed(p.StructureId, p.Cell, p.ModuleId));
                        break;
                    }
                }
            }
            return ops;
        }

        /// <summary>流里所有 `Placed` 的 id(升序不变,按流序)。</summary>
        private List<long> PlacedIdsOf(EventStream stream)
        {
            var ids = new List<long>();
            foreach (SimEvent e in stream.Events)
                if (e.Kind == EventKind.StructurePlaced)
                    ids.Add(_decoder.Decode<StructurePlacedPayload>(e.Kind, e.Payload).StructureId);
            return ids;
        }

        // ══════════════════ 纯函数层 ══════════════════

        [Test]
        public void test_remap_collisionCriterion_onlyPlacedWithinBaseline()
        {
            // **V-1 必要性 + V-2 充分性**,一次钉住判据的**两侧**与两条边界。
            const int N = 3;

            // ── V-1:后基线存档 —— `Placed` 全部 > N,`Removed` 合法引用基线号 ⇒ **不触发** ──
            var postBaseline = new List<StructureRestoreOp>
            {
                StructureRestoreOp.Placed(N + 1, new WorldPos(0, 0, 2), ModuleBunk, 0, 0),
                StructureRestoreOp.Removed(1),                       // 拆掉基线家具 1 —— 合法
                StructureRestoreOp.Modified(2, 90, 0),               // 旋转基线家具 2 —— 合法
            };
            Assert.IsFalse(StructureIdRemap.Collides(postBaseline, N),
                "V-1:`Removed` / `Modified` 引用基线号是**正常的后基线行为**(GDD :150-151)," +
                "放宽成「任一操作命中号段」会让每一份拆过初始家具的正常存档被整体平移");

            // ── V-2:前基线存档 —— 存在 `Placed(1)` ⇒ **触发** ──
            var preBaseline = new List<StructureRestoreOp>
            {
                StructureRestoreOp.Placed(1, new WorldPos(0, 0, 2), ModuleBunk, 0, 0),
            };
            Assert.IsTrue(StructureIdRemap.Collides(preBaseline, N),
                "V-2:基线诞生前的 `Placed` 从 1 起号 ⇒ 必与基线撞号 ⇒ 须触发");

            // ── 两条边界:上界闭、越界不触发 ──
            Assert.IsTrue(StructureIdRemap.Collides(
                    new[] { StructureRestoreOp.Placed(N, default, ModuleBunk, 0, 0) }, N),
                $"边界:Placed.id == N({N}) 仍在基线号段内 ⇒ 触发(闭区间上界)");
            Assert.IsFalse(StructureIdRemap.Collides(
                    new[] { StructureRestoreOp.Placed(N + 1, default, ModuleBunk, 0, 0) }, N),
                $"边界:Placed.id == N+1({N + 1}) 已在基线之外 ⇒ 不触发");

            // ── 空基线 ⇒ 区间 [1,0] 为空 ⇒ 任何 id 都不撞 ──
            Assert.IsFalse(StructureIdRemap.Collides(preBaseline, 0),
                "基线件数为 0 ⇒ 不存在可撞的号 ⇒ 不得触发(否则空世界读档会无故跳号)");
        }

        [Test]
        public void test_remap_apply_shiftsAllThreeKinds_keepsOtherFields()
        {
            const int N = 3;
            var ops = new List<StructureRestoreOp>
            {
                StructureRestoreOp.Placed(1, new WorldPos(2, 0, 2), ModuleBunk, 90, 1),
                StructureRestoreOp.Removed(2, new WorldPos(3, 0, 3), ModuleBunk),
                StructureRestoreOp.Modified(3, 270, 2),
            };

            IReadOnlyList<StructureRestoreOp> shifted = StructureIdRemap.Apply(ops, N);

            CollectionAssert.AreEqual(new long[] { 1 + N, 2 + N, 3 + N },
                new[] { shifted[0].StructureId, shifted[1].StructureId, shifted[2].StructureId },
                "ADR-033 §③:**全部**结构操作共用同一映射 —— Removed / Modified 必须跟着走," +
                "否则它们指向的就不是同一条结构了");

            // 其余字段一律原样(只换 id)
            Assert.AreEqual(new WorldPos(2, 0, 2), shifted[0].Anchor);
            Assert.AreEqual(ModuleBunk, shifted[0].ModuleId);
            Assert.AreEqual(90, shifted[0].Orientation);
            Assert.AreEqual(1, shifted[0].Variant);
            Assert.AreEqual(270, shifted[2].Orientation);
            Assert.AreEqual(2, shifted[2].Variant);
        }

        [Test]
        public void test_remap_shiftedRange_isDisjointFromBaselineAndInjective()
        {
            // **V-3**(按可判定口径实现,见证据件对该条措辞的说明):
            // ① 平移后号段与基线号段 `1..N` **不交**;② 映射**单射**。
            const int N = 3;
            var ops = new List<StructureRestoreOp>();
            for (long id = 1; id <= 4; id++)
                ops.Add(StructureRestoreOp.Placed(id, default, ModuleBunk, 0, 0));

            IReadOnlyList<StructureRestoreOp> shifted = StructureIdRemap.Apply(ops, N);

            long min = long.MaxValue;
            var seen = new HashSet<long>();
            foreach (StructureRestoreOp op in shifted)
            {
                min = Math.Min(min, op.StructureId);
                Assert.IsTrue(seen.Add(op.StructureId),
                    $"单射被破坏:id={op.StructureId} 出现两次 ⇒ 两条不同结构会指向同一实例");
            }

            Assert.Greater(min, N,
                "① 平移后最小号须 > N ⇒ 与基线号段 1..N **不交**(1 + N > N)");
            Assert.AreEqual(ops.Count, seen.Count, "④ 条目数不变(整体平移是双射,不增不减)");
        }

        [Test]
        public void test_remap_wholeShift_remainsInjectiveWhenMaxIdExceedsBaseline()
        {
            // **V-5 第一半 = Alt 3 的反例面**(ADR-033 §Alt 3):
            // `M > N` 时「只平移 id ≤ N」会把 `1,2,3` 推到 `4,5,6`,**与未平移的 4,5,6 撞**。
            // 整体平移在同一输入上仍单射 ⇒ 本条同时钉住「选整体平移」这个决定。
            const int N = 3;
            var ops = new List<StructureRestoreOp>();
            for (long id = 1; id <= 6; id++)        // M = 6 > N = 3
                ops.Add(StructureRestoreOp.Placed(id, default, ModuleBunk, 0, 0));

            IReadOnlyList<StructureRestoreOp> shifted = StructureIdRemap.Apply(ops, N);
            var ids = new HashSet<long>();
            foreach (StructureRestoreOp op in shifted) ids.Add(op.StructureId);

            Assert.AreEqual(6, ids.Count,
                "整体平移须在 M > N 时仍单射;若这里退化,说明走的是 Alt 3(只移 id ≤ N)" +
                "—— 那会把 1,2,3 → 4,5,6 与未平移的 4,5,6 撞成 3 个号");

            // 对照:Alt 3 的算法在同一输入上的号集合(仅 3 个不同值)
            var alt3 = new HashSet<long>();
            for (long id = 1; id <= 6; id++) alt3.Add(id <= N ? id + N : id);
            Assert.AreEqual(3, alt3.Count,
                "对照:Alt 3 在同一输入上只剩 3 个不同号 ⇒ 碰撞确实发生(反例可复算)");
        }

        [Test]
        public void test_remap_withoutCure_driftsEachReload_alt5Counterexample()
        {
            // **V-5 第二半 = Alt 5 的反例面**(ADR-033 §Alt 5:不固化 ⇒ 漂移)。
            // 反例的机制是:`Apply` 是**内容上的变换**,不是幂等变换 —— 喂同一个输入,
            // 每跑一次再涨 N。⇒ 「下次自然固化」不但不够,而且根本不是固化:
            // 它会逐轮把 id 推高(1 → 1+N → 1+2N …)。
            //
            // ⚠️ 生产路径上这条路已被**结构性关掉**(未注入 `cureStream` 而命中判据 ⇒ 抛,
            //    见 `test_load_collisionWithoutCureCapability_throwsAndLeavesStreamIntact`)
            //    —— 本条钉的是「为什么必须关掉」,不是「生产会走这里」。
            const int N = 3;
            var ops = new List<StructureRestoreOp>
            {
                StructureRestoreOp.Placed(1, new WorldPos(0, 0, 2), ModuleBunk, 0, 0),
            };

            IReadOnlyList<StructureRestoreOp> once =
                StructureIdRemap.Apply(StructureIdRemap.Apply(ops, N), N);

            Assert.AreEqual(1 + 2 * N, once[0].StructureId,
                $"Alt 5 反例:同一份内容连跑两次 ⇒ 1 → {1 + 2 * N}(每轮涨 N)。"
                + "若这里只得到 1+N,说明 Apply 意外变成了幂等 —— 那 V-4 的固化必要性就失去了论据");
        }

        // ══════════════════ 端到端层 ══════════════════

        [Test]
        public void test_load_preBaselineSave_shiftsIdsAndCuresStream()
        {
            // **V-2 端到端 + V-4 幂等 + V-7 混合存档不变量**
            const int N = 2;
            World world = NewWorldWithBaseline(N);
            EventStream stream = NewStream(Placed(tick: 10, structureId: 1, x: 0, z: 2));

            var svc = new WorldLoadService(
                () => stream.Events, _decoder, world, _catalog,
                encoder: _encoder,
                cureStream: cured => stream.Restore(cured, stream.FoldedRows));

            int applied = svc.Rebuild();

            Assert.AreEqual(1, applied, "前置:流里恰一条建造事件");
            Assert.AreEqual(N + 1, PlacedIdsOf(stream)[0],
                "**固化**:流里的 Placed.id 须被改写成 N+1 —— 不写回流,本会话之后写出的存档就是混合态");
            Assert.AreEqual(1L, PlacedIdsOf(stream)[0] - N, "平移量恰为 N(不是 maxId)");

            // 世界:基线 N 件 + 平移后的那件
            Assert.AreEqual(N + 1, world.Structures.Count,
                "两边都留:基线不被覆盖、存档结构不被丢弃");
            Assert.IsTrue(world.Structures.TryGet(N + 1, out StructureInstance shifted),
                "平移后的结构须以 id = N+1 落库");
            Assert.AreEqual(new WorldPos(0, 0, 2), shifted.Anchor, "锚点原样");

            // **V-7**:固化后的流其 `Placed` 全部 > N ⇒ 判据不再触发
            Assert.IsFalse(StructureIdRemap.Collides(OpsOf(stream), N),
                "V-7 混合存档不变量:固化后写出的存档,结构 id 空间**全在新空间** ⇒ 判据永不复发");

            // **V-4 幂等**:再载入一次(读到的是已固化的流)⇒ 恒等
            List<long> afterFirst = PlacedIdsOf(stream);
            svc.Rebuild();
            CollectionAssert.AreEqual(afterFirst, PlacedIdsOf(stream),
                "V-4:二次载入须是**恒等变换** —— 再涨一次就是 ADR-033 Alt 5 的漂移反例");
            Assert.AreEqual(N + 1, world.Structures.Count, "二次载入后世界仍恰好 N+1 件(不叠加)");
        }

        [Test]
        public void test_load_postBaselineSave_isUntouched()
        {
            // **V-1 端到端**:后基线存档(Placed 全 > N,外加一条拆基线家具的 Removed)
            // 必须**零改动** —— 流不重写、id 不跳号、基线家具真的被拆掉。
            const int N = 2;
            World world = NewWorldWithBaseline(N);
            EventStream stream = NewStream(
                Placed(tick: 10, structureId: N + 1, x: 0, z: 2),
                Removed(tick: 20, structureId: 1));          // 拆掉基线家具 1

            var svc = new WorldLoadService(
                () => stream.Events, _decoder, world, _catalog,
                encoder: _encoder,
                cureStream: cured => stream.Restore(cured, stream.FoldedRows));

            var before = new List<SimEvent>(stream.Events);
            svc.Rebuild();
            var after = new List<SimEvent>(stream.Events);

            CollectionAssert.AreEqual(before, after,
                "后基线存档不得被改写 —— 一旦改写,玩家建造的 id 会无故跳号(可用性级错误)");
            Assert.AreEqual(N + 1, PlacedIdsOf(stream)[0], "id 保持 N+1,不得平移");

            Assert.AreEqual(N, world.Structures.Count, "基线拆掉一件 ⇒ 世界剩 N-1 件 + 存档 1 件");
            Assert.IsFalse(world.Structures.TryGet(1, out _), "基线家具 1 须真的被拆掉");
            Assert.IsTrue(world.Structures.TryGet(N + 1, out _), "存档结构按原号落库");
        }

        [Test]
        public void test_load_collisionWithoutCureCapability_throwsAndLeavesStreamIntact()
        {
            // **fail-closed**:命中判据却没有固化能力 ⇒ 抛,且**流原样、世界未被改**。
            // 「只平移不固化」= Alt 5(漂移 + 混合态),ADR-033 §④之补 明令不得静默走这条路。
            const int N = 2;
            World world = NewWorldWithBaseline(N);
            EventStream stream = NewStream(Placed(tick: 10, structureId: 1, x: 0, z: 2));

            var svc = new WorldLoadService(() => stream.Events, _decoder, world, _catalog);

            var ex = Assert.Throws<InvalidOperationException>(() => svc.Rebuild());
            StringAssert.Contains("混合态", ex.Message,
                "报文须点名后果 —— 只说「未注入」会让读者以为是个可选的依赖");
            StringAssert.Contains("ADR-033", ex.Message, "报文须给出出处");

            CollectionAssert.AreEqual(new List<long> { 1 }, PlacedIdsOf(stream),
                "抛出时流必须原样 —— 固化了却没重放成功 = 改写了没通过校验的那一版");

            // 世界未被重放(异常发生在 RebuildFromDecoded 之前)⇒ 基线仍在
            Assert.AreEqual(N, world.Structures.Count, "抛出时世界须保持基线原样");
        }

        [Test]
        public void test_load_replayFailsAfterCollision_leavesStreamUncured()
        {
            // **固化时序**:`WorldLoadService.cs:168-171` 自称「重放成功**之后**才固化」。
            // 本夹具把那条自述变成可证伪的 —— 构造一份**撞号且重放必抛**的存档
            // (Placed 的 moduleId = 999 不在目录内 ⇒ `StructureRestorer.FootprintOf` 抛),
            // 并**正常注入** encoder + cureStream。
            //
            // 若固化被提到重放之前(或写在 try 里),流里的 id 会**已经**变成 N+1
            // ⇒ 下次载入读的是「改写过的、且没通过校验的那一版」。
            const int N = 2;
            const int MissingModule = 999;
            World world = NewWorldWithBaseline(N);
            EventStream stream = NewStream(Placed(tick: 10, structureId: 1, x: 0, z: 2,
                                                  moduleId: MissingModule));

            var svc = new WorldLoadService(
                () => stream.Events, _decoder, world, _catalog,
                encoder: _encoder,
                cureStream: cured => stream.Restore(cured, stream.FoldedRows));

            Assert.Throws<InvalidOperationException>(() => svc.Rebuild());

            CollectionAssert.AreEqual(new List<long> { 1 }, PlacedIdsOf(stream),
                "重放抛 ⇒ 流**必须原样**(id 仍是 1)。固化若发生在重放之前,这里会看到 N+1 = "
                + $"{N + 1} —— 那等于把「读档失败」变成「流已被改写过」,且改写的是没通过校验的版本");
        }

        [Test]
        public void test_load_curePreservesFoldedRows()
        {
            // **固化的整表替换语义**:折叠行必须原样带走。
            //
            // ⚠️ 本夹具钉的是一处**实测踩到过**的静默抹除(2026-10-28):`FoldedRows => _foldedRows`
            //   返回的是内部列表实例,而 `EventStream.Restore` 先 `Clear()`、末尾才
            //   `AddRange(foldedRows)` ⇒ **把流自己的列表传回去**就得到「清空后加回自己」= 全丢,
            //   零异常。生产装配根正是这么写的。
            //   后果不是少一条记录:`GetNextPatientId` 高水位依赖折叠行(ADR-006 Amendment B)。
            //
            // 判别力来源:本夹具**必须**同时满足「固化确实发生了」与「固化前折叠行非空」,
            //   否则「没丢」与「本来就空」不可分。
            const int N = 2;
            World world = NewWorldWithBaseline(N);

            var stream = new EventStream(new FakeIdAuthority(), new FakePresenceQuery());
            stream.Restore(
                new List<SimEvent> { Placed(tick: 10, structureId: 1, x: 0, z: 2) },
                new[]
                {
                    new FoldRow(onset: 5L, diseaseId: 3, patientId: 42, patientSeed: 77L,
                                outcome: (int)PatientOutcome.Died, tEnd: 9L),
                });

            Assert.AreEqual(1, stream.FoldedRows.Count, "前置:折叠行须已登记 —— 否则本夹具无判别力");

            // **生产写法**(传流自己的列表):依赖 `EventStream.Restore` 的自引用守卫。
            var svc = new WorldLoadService(
                () => stream.Events, _decoder, world, _catalog,
                encoder: _encoder,
                cureStream: cured => stream.Restore(cured, stream.FoldedRows));

            svc.Rebuild();

            Assert.AreEqual(N + 1, PlacedIdsOf(stream)[0], "前置:固化须确实发生过(id 已变 N+1)");
            Assert.AreEqual(1, stream.FoldedRows.Count,
                "固化后折叠行须原样保留 —— 若为 0,说明自引用守卫失效(AddRange 把已清空的列表加回自己)");
            Assert.AreEqual(42, stream.FoldedRows[0].PatientId, "内容亦须一致,不只是条数");
        }
    }
}
