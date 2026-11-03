// 2026-11-03 smoke 轮发现的两条**运行期**缺陷的夹具(7a 槽位台账)
//
// 病因(用户实测,`production/qa/smoke-2026-11-03.md`):
//   ① 「存了,什么都没发生」—— 台账高水位初值恒 0,而存档位屏只列 `{1,2,3}`
//      ⇒ 本次会话**第一次**写入落在槽 0,那号在屏上不存在。
//   ② 「槽位 slot=1 已锁 … 锁持续到下一次叙事事件 checkpoint」**永不自解** ——
//      置锁在 `SaveService.Load` 成功时,解锁唯一路径 `OnNarrativeAnchor()`
//      **全仓零生产调用点**。
//
// 本文件断的是**机制**:初值通道可达 + 锁可被叙事锚点解除。
// ⚠️ **不**断「CompositionRoot 那一行接线」—— 见文末 §诚实边界。
//
// 写盘边界:全部落 `Path.GetTempPath()`(FsWriteBoundaryGate 允许形态)。

using System;
using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Tests.DiseaseSimulation;   // FakeIdAuthority / FakePresenceQuery
using NUnit.Framework;

namespace DaYiJingCheng.Tests.PersistenceService
{
    [TestFixture]
    public class SaveSlotLockReleaseTest
    {
        private string _testDir;
        private InMemoryBlobPool _pool;
        private PayloadEncoder _encoder;
        private PayloadDecoder _decoder;

        [SetUp]
        public void SetUp()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "dyjq_slot_lock_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDir);
            _pool = new InMemoryBlobPool();
            _encoder = new PayloadEncoder(_pool);
            _decoder = new PayloadDecoder(_pool);
        }

        [TearDown]
        public void Teardown()
        {
            if (Directory.Exists(_testDir))
            {
                try { Directory.Delete(_testDir, true); } catch { }
            }
        }

        /// <summary>造一个能存能读的 `SaveService`。</summary>
        private SaveService MakeService(EventStream stream, uint seed)
            => new SaveService(
                _testDir, new SaveCodec(),
                folded: new FoldedPredicate(
                    historyStream: () => stream.Events,
                    caseStream: () => Array.Empty<SimEvent>(),
                    diseaseLookup: _ => null,
                    decoder: _decoder),
                narrativeContext: FuncNarrativeContextQuery.AlwaysIn,
                replaySink: ev => stream.Restore(ev, Array.Empty<FoldRow>()),
                foldedRowSink: _ => { },
                compose: _ => SaveComposer.Compose(
                    stream.Events,
                    new FoldedPredicate(() => stream.Events, () => Array.Empty<SimEvent>(),
                                        _ => null, _decoder),
                    _decoder, new SaveCodec(),
                    worldSeed: 1UL, configVersion: 0U, tick: 10L,
                    existingFoldedRows: stream.FoldedRows, blobPool: _pool),
                writeScheduler: action => action(),
                blobRestoreSink: _pool,
                initialNextWritableSeq: seed);

        private EventStream NewStream() => new EventStream(new FakeIdAuthority(), new FakePresenceQuery());

        // ══════════════════════════════════════════════════════════════════
        // ① 高水位初值通道
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_smoke_initialNextWritableSeq_isReachableThroughCtor()
        {
            SaveService svc = MakeService(NewStream(), seed: 1);
            Assert.That(svc.Ledger.NextWritableSeq, Is.EqualTo(1u),
                "装配层传 1 ⇒ 第一次写入落在槽 1(屏上的第一个槽);初值通道此前**不存在**");
        }

        [Test]
        public void test_smoke_defaultInitialNextWritableSeq_staysZero()
        {
            SaveService svc = new SaveService(_testDir, new SaveCodec(),
                narrativeContext: FuncNarrativeContextQuery.AlwaysIn);
            Assert.That(svc.Ledger.NextWritableSeq, Is.EqualTo(0u),
                "缺省仍为 0 —— 既有夹具与 AC-7a-19 的 0 起语义**不因本次修复而变**");
        }

        [Test]
        public void test_smoke_firstCheckpointLandsOnVisibleSlot()
        {
            EventStream stream = NewStream();
            SaveService svc = MakeService(stream, seed: 1);

            svc.Checkpoint(new SaveSlot(svc.Ledger.NextWritableSeq));

            Assert.That(File.Exists(Path.Combine(_testDir, "save_1.bin")), Is.True,
                "第一次存档必须落在 `save_1.bin` —— 落在 `save_0.bin` 就是玩家看到「什么都没发生」的那个缺陷");
        }

        // ══════════════════════════════════════════════════════════════════
        // ② 读档即锁 ⇒ 叙事锚点解锁
        // ══════════════════════════════════════════════════════════════════

        private void SaveOneSlot(SaveService svc)
        {
            svc.Checkpoint(new SaveSlot(svc.Ledger.NextWritableSeq));
        }

        [Test]
        public void test_smoke_loadLocksSlot_thenNarrativeAnchorUnlocks()
        {
            EventStream stream = NewStream();
            SaveService svc = MakeService(stream, seed: 1);
            SaveOneSlot(svc);

            svc.Load(new SaveSlot(1));                       // 首次读档成功
            Assert.That(svc.Ledger.IsLocked(new SaveSlot(1)), Is.True, "读档成功即置锁(AC-7a-19 (d))");

            Assert.Throws<InvalidOperationException>(() => svc.Load(new SaveSlot(1)),
                "同一会话内再读 = 已锁(这正是用户 smoke 看到的报错)");

            svc.OnNarrativeAnchor();                          // 「下一次叙事事件 checkpoint」

            Assert.That(svc.Ledger.IsLocked(new SaveSlot(1)), Is.False, "叙事锚点到达 ⇒ 锁解除");
            Assert.DoesNotThrow(() => svc.Load(new SaveSlot(1)),
                "解锁后同一槽位必须**重新可读** —— 这是本条修复的全部意义");
        }

        // ══════════════════════════════════════════════════════════════════
        // 诚实边界
        // ══════════════════════════════════════════════════════════════════
        //
        // ⚠️ **本文件不证「CompositionRoot 把 onFired 接到了 OnNarrativeAnchor」**。
        //    那行接线只能靠装配层夹具(需要整套 Assemble)或人工走查;
        //    此处只证**机制**两侧都通:初值可传、锁可解。
        //    该残余已登记在 smoke 报告的「未覆盖」节,不在这里假装已证。
    }
}
