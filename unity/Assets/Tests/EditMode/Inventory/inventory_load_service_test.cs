// S20-10/11/12 修复轮 —— `InventoryLoadService` 的接线钉子(2026-10-27)。
//
// 权威来源:
//   · `production/qa/evidence/review-s20-10-inventory-001-2026-10-27.md` §2.1 B-1
//   · `production/qa/evidence/review-s20-11-inventory-003-2026-10-27.md` §2.1 B-1 / §2.2 W-2
//   · ADR-009 §四(派生态)· ADR-014 §五(装载硬失败)· ADR-025 §①(门 A 不受影响)
//
// 钉的安全属性(每条都对应一条原评审判定):
//   ① **Fold 真被调用**:Rebuild 后 `Instances` 非空,且内容出自事件流(不是空壳);
//   ② **BuildPartQualityGate 真被调用**:`build_part` 且 `quality != 1` ⇒ 装载抛
//      (接线前该门零生产调用点,一个违例实例可以自由穿过整个工程);
//   ③ **CarryLoadSolver 真被调用**:`UnclaimedLoad` = 按 weight 求和的非零值
//      (接线前 `FoldWeight` / `Sum` 零生产调用点);
//   ④ **读档后重建**:`LoadSequencer.Load` 走完 ⇒ `afterRestore` 钩子已执行、
//      `Current` 随之产出(用户裁定「在装配根加「读档后重建库存投影」」);
//   ⑤ **静态数据门**:重复 ordinal / 未知 ordinal / `weight < 1` 各自抛(禁静默按 0)。
//
// ⚠️ **禁借绿**:本文件不验「玩家看得到库存」(20b 呈现侧未接线),
//    只验「投影在装配层被重建出来、且三件孤儿函数确被调用」。
//
// ⚠️ 零随机 / 零时间依赖;物品种子表 = **合成夹具**(数值非 GDD 值,只为可判),
//    绝不读 `Assets/DataCooked` 真实产物(那会让夹具依赖烘焙状态)。

using System;
using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Tests.DiseaseSimulation;   // FakeIdAuthority / FakePresenceQuery
using NUnit.Framework;

namespace DaYiJingCheng.Tests.EditMode.Inventory
{
    /// <summary>`InventoryLoadService` —— 20 库存实体装载链的接线钉子。</summary>
    [TestFixture]
    public sealed class InventoryLoadServiceTest
    {
        // ── 合成物品表(ordinal 0/1/2;数值是夹具值,非 GDD 值)──
        private const int WillowOrdinal = 0;
        private const int PlankOrdinal = 1;
        private const int BarkOrdinal = 2;

        private InMemoryBlobPool _pool;
        private PayloadEncoder _encoder;
        private PayloadDecoder _decoder;
        private EventStream _stream;

        [SetUp]
        public void Setup()
        {
            _pool = new InMemoryBlobPool();
            _encoder = new PayloadEncoder(_pool);
            _decoder = new PayloadDecoder(_pool);
            _stream = new EventStream(new FakeIdAuthority(), new FakePresenceQuery());
        }

        // ══════════════════════════════════════════════════════════════════
        // 夹具
        // ══════════════════════════════════════════════════════════════════

        private static ItemDef Def(int ordinal, string baseId, ItemCategory cat, int stackMax, int weight)
            => new ItemDef
            {
                Ordinal = ordinal,
                BaseId = baseId,
                ProcessingState = ProcessingState.Raw,
                DisplayName = baseId,
                Category = cat,
                StackMax = stackMax,
                Weight = weight,
                Deprecated = false,
                LegalTransitions = Array.Empty<string>(),
            };

        /// <summary>三件物品:两件 material(可堆叠)+ 一件 build_part(stack_max = 1)。</summary>
        private static ItemDef[] MakeItems() => new[]
        {
            Def(WillowOrdinal, "willow_bark", ItemCategory.Material, stackMax: 9, weight: 2),
            Def(PlankOrdinal,  "plank",       ItemCategory.BuildPart, stackMax: 1, weight: 5),
            Def(BarkOrdinal,   "bark",        ItemCategory.Material, stackMax: 9, weight: 3),
        };

        private void Spawn(long id, int itemOrdinal, int qty, long tick, long seq = 0L)
            => _stream.Append(new SimEvent(tick, PatientId.None, seq, EventKind.DropSpawned,
                _encoder.Encode(EventKind.DropSpawned,
                    new DropSpawnedPayload(id, new WorldPos(1, 2, 3), itemOrdinal, qty))));

        /// <summary>品级来源(<c>ResourceHarvested.out_quality</c>)—— 无它则 fold 报 R18 缺口。</summary>
        private void Harvest(long id, int nodeId, int qty, int outQuality, long tick, long seq = 0L)
            => _stream.Append(new SimEvent(tick, PatientId.None, seq, EventKind.ResourceHarvested,
                _encoder.Encode(EventKind.ResourceHarvested,
                    new ResourceHarvestedPayload(id, nodeId, 1, qty, outQuality))));

        private InventoryLoadService NewService(ItemDef[] items = null)
            => new InventoryLoadService(
                () => _stream.Events, _decoder, () => items ?? MakeItems());

        // ══════════════════════════════════════════════════════════════════
        // ① Fold 真被调用(评审 S20-10 B-1)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_rebuild_callsFold_producesInstancesFromTheStream()
        {
            Spawn(7, WillowOrdinal, qty: 3, tick: 1);
            Harvest(7, nodeId: 1, qty: 3, outQuality: 2, tick: 2);

            InventoryLoadResult r = NewService().Rebuild();

            Assert.That(r.Instances, Has.Count.EqualTo(1),
                "Rebuild 须从事件流产出一个实例 —— 零个说明 `InventoryFold.Fold` 没被调用" +
                "(接线前它的生产调用点恰为零,评审 S20-10 B-1)");

            ItemInstance inst = r.Instances[0];
            Assert.That(inst.InstanceId, Is.EqualTo(7L));
            Assert.That(inst.Key.BaseId, Is.EqualTo("willow_bark"),
                "ItemKey 由投影行的 ordinal 反查物品表得出(不是从流里另抄一份)");
            Assert.That(inst.Qty, Is.EqualTo(3));
            Assert.That(inst.Quality, Is.EqualTo(2), "品级来自 `ResourceHarvested.out_quality`");
            Assert.That(inst.Children, Is.Empty, "非容器实例的 Children = 空数组(Story 010 前无容器)");
        }

        [Test]
        public void test_rebuild_emptyStream_yieldsEmptyProjectionNotThrow()
        {
            // 空流 ≠ 装载失败:全新世界没有任何库存是**合法状态**。
            InventoryLoadResult r = NewService().Rebuild();
            Assert.That(r.Instances, Is.Empty);
            Assert.That(r.UnclaimedLoad, Is.EqualTo(0L));
            Assert.That(r.IsComplete, Is.True, "空流零缺口 ⇒ IsComplete = true");
        }

        [Test]
        public void test_rebuild_isPureRebuild_notIncremental()
        {
            // 派生态(ADR-009 §四):再调一次 = 从流**全量**重算,不是增量累加。
            Spawn(7, WillowOrdinal, qty: 2, tick: 1);
            Harvest(7, nodeId: 1, qty: 2, outQuality: 1, tick: 2);

            var svc = NewService();
            Assert.That(svc.Rebuild().Instances, Has.Count.EqualTo(1));
            Assert.That(svc.Rebuild().Instances, Has.Count.EqualTo(1),
                "两次 Rebuild 结果相同 ⇒ 零累积(缓存会让第二次翻倍)");

            // 流变了 ⇒ 结果跟着变(证明它读的是**当下**的流,不是快照)。
            Spawn(8, BarkOrdinal, qty: 1, tick: 3);
            Harvest(8, nodeId: 2, qty: 1, outQuality: 1, tick: 4);
            Assert.That(svc.Rebuild().Instances, Has.Count.EqualTo(2),
                "流追加后重算 ⇒ 2 个实例(读的是闭包取到的当下流)");
        }

        // ══════════════════════════════════════════════════════════════════
        // ② BuildPartQualityGate 真被调用(评审 S20-11 B-1)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_rebuild_buildPartWithQualityNotOne_throwsAtLoad()
        {
            // build_part(plank, ordinal 1)且 out_quality = 3 —— P0 范围门 ③ 的直接违例。
            Spawn(11, PlankOrdinal, qty: 1, tick: 1);
            Harvest(11, nodeId: 5, qty: 1, outQuality: 3, tick: 2);

            var ex = Assert.Throws<InvalidOperationException>(() => NewService().Rebuild(),
                "`build_part` 且 quality = 3 须在装载期抛 —— 接线前 `BuildPartQualityGate` " +
                "零生产调用点,该实例可自由穿过整个工程(评审 S20-11 B-1)");

            Assert.That(ex.Message, Does.Contain("P0 范围门"),
                "抛的须是范围门本体,不是别的异常冒充");
            Assert.That(ex.Message, Does.Contain("11"),
                "消息须点名违例实例 id(一次修完)");
        }

        [Test]
        public void test_rebuild_buildPartWithQualityOne_passes()
        {
            Spawn(12, PlankOrdinal, qty: 1, tick: 1);
            Harvest(12, nodeId: 5, qty: 1, outQuality: BuildPartQualityGate.P0BuildPartQuality, tick: 2);

            InventoryLoadResult r = NewService().Rebuild();
            Assert.That(r.Instances, Has.Count.EqualTo(1),
                "quality = 1 的 build_part 是唯一合法形态 ⇒ 不抛");
        }

        [Test]
        public void test_rebuild_enforceGatesFalse_skipsGate()
        {
            // 门可关(**仅供诊断 / 迁移期**):关掉后同一份流不抛 ——
            // 这条同时证明「上面那条抛」确实来自门,而不是 Fold 或载重折重。
            Spawn(13, PlankOrdinal, qty: 1, tick: 1);
            Harvest(13, nodeId: 5, qty: 1, outQuality: 3, tick: 2);

            Assert.DoesNotThrow(() => NewService().Rebuild(enforceGates: false),
                "enforceGates = false ⇒ 跳过范围门(门是可关闭的诊断面,不是 Fold 的副作用)");
        }

        [Test]
        public void test_rebuild_materialWithAnyQuality_isNotGated()
        {
            // 门的适用面 = 仅 build_part(AC-20-04 邻域):原料品级合法性归 21a,不在此重复。
            Spawn(14, WillowOrdinal, qty: 1, tick: 1);
            Harvest(14, nodeId: 1, qty: 1, outQuality: 7, tick: 2);

            Assert.DoesNotThrow(() => NewService().Rebuild(),
                "material 的任意品级**不得**被 P0 范围门拦 —— 那会把 21a 的职责越权接过来");
        }

        // ══════════════════════════════════════════════════════════════════
        // ③ CarryLoadSolver 真被调用(评审 S20-11 W-2)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_rebuild_computesUnclaimedLoadFromWeights()
        {
            // willow(qty 3, weight 2)= 6;bark(qty 2, weight 3)= 6 ⇒ 合计 12。
            // 未 claim ⇒ owner = NoOwner ⇒ 全部计入 UnclaimedLoad。
            Spawn(21, WillowOrdinal, qty: 3, tick: 1);
            Harvest(21, nodeId: 1, qty: 3, outQuality: 1, tick: 2);
            Spawn(22, BarkOrdinal, qty: 2, tick: 3);
            Harvest(22, nodeId: 2, qty: 2, outQuality: 1, tick: 4);

            InventoryLoadResult r = NewService().Rebuild();

            Assert.That(r.UnclaimedLoad, Is.EqualTo(12L),
                "3×2 + 2×3 = 12 —— 接线前 `CarryLoadSolver.Sum` 零生产调用点," +
                "此值恒 0(评审 S20-11 W-2)");
        }

        [Test]
        public void test_rebuild_weightBelowOne_throwsInsteadOfCountingZero()
        {
            // weight = 0 是静态数据缺陷 —— 静默按 0 计重会让载重凭空变轻(超载放行)。
            ItemDef[] items =
            {
                Def(WillowOrdinal, "willow_bark", ItemCategory.Material, stackMax: 9, weight: 0),
            };
            Spawn(31, WillowOrdinal, qty: 1, tick: 1);
            Harvest(31, nodeId: 1, qty: 1, outQuality: 1, tick: 2);

            var ex = Assert.Throws<InvalidOperationException>(() => NewService(items).Rebuild());
            Assert.That(ex.Message, Does.Contain("weight"),
                "须点名 weight —— 不是泛泛的装载失败");
        }

        // ══════════════════════════════════════════════════════════════════
        // ④ 静态数据门(禁静默降级)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_items_duplicateOrdinal_throws()
        {
            ItemDef[] items =
            {
                Def(0, "a", ItemCategory.Material, stackMax: 9, weight: 1),
                Def(0, "b", ItemCategory.Material, stackMax: 9, weight: 1),
            };
            var ex = Assert.Throws<InvalidOperationException>(() => NewService(items).Rebuild());
            Assert.That(ex.Message, Does.Contain("ordinal"),
                "ordinal 是不可变主键(ADR-006 D-21-13)· 重复 = 静态数据缺陷,当场抛");
        }

        [Test]
        public void test_items_duplicateItemKey_throws()
        {
            // (base_id, processing_state) 是复合主键(AC-21a-21);不同 ordinal 同键 = 表坏了。
            ItemDef[] items =
            {
                Def(0, "same", ItemCategory.Material, stackMax: 9, weight: 1),
                Def(1, "same", ItemCategory.Material, stackMax: 9, weight: 1),
            };
            var ex = Assert.Throws<InvalidOperationException>(() => NewService(items).Rebuild());
            Assert.That(ex.Message, Does.Contain("item_key"),
                "复合主键重复须抛 —— 否则 `defOf` 会静默取最后一个条目");
        }

        [Test]
        public void test_projectionOrdinalNotInSeedTable_throws()
        {
            Spawn(41, itemOrdinal: 99, qty: 1, tick: 1);   // 种子表里没有 99
            Harvest(41, nodeId: 1, qty: 1, outQuality: 1, tick: 2);

            var ex = Assert.Throws<InvalidOperationException>(() => NewService().Rebuild());
            Assert.That(ex.Message, Does.Contain("99"),
                "投影行引用不存在的物品 ⇒ 硬失败(ADR-014 §五),不静默跳过");
        }

        [Test]
        public void test_ctor_nullGuards()
        {
            Func<IReadOnlyList<SimEvent>> ev = () => _stream.Events;
            Func<IReadOnlyList<ItemDef>> items = MakeItems;

            Assert.Throws<ArgumentNullException>(() => new InventoryLoadService(null, _decoder, items));
            Assert.Throws<ArgumentNullException>(() => new InventoryLoadService(ev, null, items));
            Assert.Throws<ArgumentNullException>(() => new InventoryLoadService(ev, _decoder, null));
        }

        [Test]
        public void test_items_nullResult_throwsInsteadOfEmptyProjection()
        {
            // 取数返回 null ⇒ 抛(不把 null 当空表 —— 那是「库存全空」的静默失败)。
            var svc = new InventoryLoadService(() => _stream.Events, _decoder, () => null);
            var ex = Assert.Throws<InvalidOperationException>(() => svc.Rebuild());
            Assert.That(ex.Message, Does.Contain("物品种子表"));
        }

        [Test]
        public void test_beforeFirstRebuild_currentIsNull()
        {
            Assert.That(NewService().Current, Is.Null,
                "「装没装」看服务袋属性,「重建没重建」看 Current —— 首次 Rebuild 前为 null");
        }

        // ══════════════════════════════════════════════════════════════════
        // ⑤ 读档后重建(用户裁定「在装配根加「读档后重建库存投影」」)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_loadSequencer_afterRestoreHookRunsAndRebuildsProjection()
        {
            var dir = Path.Combine(Path.GetTempPath(), "dyjq_invload_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                Spawn(51, WillowOrdinal, qty: 4, tick: 1);
                Harvest(51, nodeId: 1, qty: 4, outQuality: 1, tick: 2);
                WriteSlot(dir, 1);

                var svc = NewService();
                var sequencer = new LoadSequencer(
                    NewLoadSideService(dir), NewTrigger(), fallback: null,
                    afterRestore: () => svc.Rebuild());

                sequencer.Load(new SaveSlot(1U));

                Assert.That(svc.Current, Is.Not.Null,
                    "`LoadSequencer.Load` 走完 ⇒ `afterRestore` 已执行(接线前该钩子不存在)");
                Assert.That(svc.Current.Instances, Has.Count.EqualTo(1),
                    "重建出的实例须来自**读回来的**流(qty = 4)");
                Assert.That(svc.Current.Instances[0].Qty, Is.EqualTo(4));
                Assert.That(svc.Current.UnclaimedLoad, Is.EqualTo(8L), "4 × weight 2 = 8");
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch { }
            }
        }

        [Test]
        public void test_loadSequencer_afterRestoreThrows_loadFailsLoud()
        {
            var dir = Path.Combine(Path.GetTempPath(), "dyjq_invload_bad_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                // 存档里带一个违例 build_part ⇒ 读档时的重建会抛。
                Spawn(61, PlankOrdinal, qty: 1, tick: 1);
                Harvest(61, nodeId: 5, qty: 1, outQuality: 3, tick: 2);
                WriteSlot(dir, 1);

                var svc = NewService();
                var sequencer = new LoadSequencer(
                    NewLoadSideService(dir), NewTrigger(), fallback: null,
                    afterRestore: () => svc.Rebuild());

                Assert.Throws<InvalidOperationException>(() => sequencer.Load(new SaveSlot(1U)),
                    "重建抛 ⇒ 读档整体失败(不 catch —— 静默吞掉会让下游拿到旧投影," +
                    "那是更坏的失败;见 LoadSequencer 第 4 步的注)");
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch { }
            }
        }

        [Test]
        public void test_loadSequencer_noHook_loadStillWorks()
        {
            // 钩子可 null(既有装配点不传)⇒ 前三步照常,零行为变化。
            var dir = Path.Combine(Path.GetTempPath(), "dyjq_invload_nohook_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                Spawn(71, WillowOrdinal, qty: 1, tick: 1);
                Harvest(71, nodeId: 1, qty: 1, outQuality: 1, tick: 2);
                WriteSlot(dir, 1);

                var sequencer = new LoadSequencer(NewLoadSideService(dir), NewTrigger());
                Assert.DoesNotThrow(() => sequencer.Load(new SaveSlot(1U)),
                    "afterRestore 缺省 null ⇒ 读档行为与接线前逐位一致");
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch { }
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // 读档夹具(双 SaveService 先例:写槽即锁,读侧须独立实例)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>锚点触发器(白名单非空 —— 该类对空表 fail-loud:「事件触发半边恒哑」
        /// 是配置错误,不是本夹具要验的东西)。本文件的流里没有该 Kind ⇒ 恒不触发。</summary>
        private AnchorCheckpointTrigger NewTrigger()
            => new AnchorCheckpointTrigger(() => true, FuncNarrativeContextQuery.AlwaysIn,
                new[] { EventKind.DiseaseOnset }, onFired: () => { });

        private FoldedPredicate PredicateOf() => new FoldedPredicate(
            historyStream: () => SaveComposer.FilterByStream(_stream.Events, StreamId.History),
            caseStream: () => SaveComposer.FilterByStream(_stream.Events, StreamId.Case),
            diseaseLookup: _ => null,
            decoder: _decoder);

        private void WriteSlot(string dir, int slot)
        {
            var svc = new SaveService(
                dir, new SaveCodec(),
                folded: PredicateOf(),
                narrativeContext: FuncNarrativeContextQuery.AlwaysIn,
                replaySink: _ => { },
                foldedRowSink: _ => { },
                compose: _ => SaveComposer.Compose(
                    _stream.Events, PredicateOf(), _decoder, new SaveCodec(),
                    worldSeed: 1UL, configVersion: 0U, tick: 20L,
                    existingFoldedRows: _stream.FoldedRows, blobPool: _pool),
                writeScheduler: action => action(), blobRestoreSink: _pool);   // 夹具与生产的唯一差别:同步内联
            svc.Checkpoint(new SaveSlot((uint)slot));
        }

        private SaveService NewLoadSideService(string dir)
        {
            IReadOnlyList<SimEvent> pending = null;
            return new SaveService(
                dir, new SaveCodec(),
                folded: PredicateOf(),
                narrativeContext: FuncNarrativeContextQuery.AlwaysIn,
                replaySink: events => pending = events,
                foldedRowSink: bytes => _stream.Restore(
                    pending ?? throw new InvalidOperationException("折叠段先于事件段到达"),
                    FoldRowCodec.DecodeSegment(bytes)),
                compose: _ => SaveComposer.Compose(
                    _stream.Events, PredicateOf(), _decoder, new SaveCodec(),
                    worldSeed: 1UL, configVersion: 0U, tick: 20L,
                    existingFoldedRows: _stream.FoldedRows, blobPool: _pool),
                writeScheduler: action => action(), blobRestoreSink: _pool);
        }
    }
}
