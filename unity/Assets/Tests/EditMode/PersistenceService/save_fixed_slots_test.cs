// 固定槽(1 = 退出保存 · 2 = 自动存档 · 3+ = 手动存档)的行为钉子。
//
// 权威(2026-11-03 用户裁定,`TD-persist-17`):
//   ① 自动存档(定时兜底 + 叙事事件锚点)**只占固定槽 2**,原地覆盖,不吃手动槽号;
//   ② 手动存档从槽 3 起,仍然只进不退;
//   ③ 「继续诊治」= 退出 / 自动 / 手动里 **tick 最新**的一份;
//   ④ 槽 1 / 2 的旧档不被失败的新写覆盖(原子写保证)。
//
// ⚠️ 本文件断的是**机制**,不是 `CompositionRoot` 的那两行接线 ——
//    接线由 `composition_root_test` / `checkpoint_fallback_test` 覆盖。
//
// 写盘边界:全部落 `Path.GetTempPath()`(FsWriteBoundaryGate 允许形态)。

using System;
using System.IO;
using DaYiJingCheng.Gameplay.Boot;   // SaveComposer
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Tests.DiseaseSimulation;   // FakeIdAuthority / FakePresenceQuery
using NUnit.Framework;

namespace DaYiJingCheng.Tests.PersistenceService
{
    [TestFixture]
    public class SaveFixedSlotsTest
    {
        private string _dir;
        private InMemoryBlobPool _pool;
        private PayloadEncoder _encoder;
        private PayloadDecoder _decoder;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "dyjq_fixed_slots_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _pool = new InMemoryBlobPool();
            _encoder = new PayloadEncoder(_pool);
            _decoder = new PayloadDecoder(_pool);
        }

        [TearDown]
        public void Teardown()
        {
            if (Directory.Exists(_dir))
            {
                try { Directory.Delete(_dir, true); } catch { }
            }
        }

        private SaveService MakeService(long tick)
        {
            var stream = new EventStream(new FakeIdAuthority(), new FakePresenceQuery());
            return new SaveService(
                _dir, new SaveCodec(),
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
                    worldSeed: 1UL, configVersion: 0U, tick: tick,
                    timeParams: new SaveTimeParams(36000L, 27000L, 9000L, 252000L, 5L, 6000L, 100L),
                    existingFoldedRows: stream.FoldedRows, blobPool: _pool),
                writeScheduler: action => action(),
                blobRestoreSink: _pool,
                initialNextWritableSeq: SaveService.MinManualSlotSeq);
        }

        // ══════════════════════════════════════════════════════════════════
        // ① 自动存档 = 固定槽 2,原地覆盖
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_autoSlot_isRewritable_secondWriteDoesNotThrow()
        {
            SaveService svc = MakeService(tick: 10L);
            svc.Checkpoint(SaveService.AutoSaveSlot);
            Assert.DoesNotThrow(() => svc.Checkpoint(SaveService.AutoSaveSlot),
                "自动存档必须**原地覆盖** —— 它不是玩家的回滚点,不需要「只进不退」");
        }

        [Test]
        public void test_autoSlot_writesToOwnFile_notToNumberedSlot()
        {
            SaveService svc = MakeService(tick: 10L);
            svc.Checkpoint(SaveService.AutoSaveSlot);

            Assert.IsTrue(File.Exists(Path.Combine(_dir, "save_auto.bin")),
                "自动存档须落 `save_auto.bin`(独立文件名,与退出保存对称)");
            Assert.IsFalse(File.Exists(Path.Combine(_dir, "save_2.bin")),
                "**不得**再写出一份 `save_2.bin` —— 那是「槽号 = 文件名」的旧形态,会和保留槽语义打架");
        }

        [Test]
        public void test_autoSlot_doesNotAdvanceManualHighWater()
        {
            SaveService svc = MakeService(tick: 10L);
            svc.Checkpoint(SaveService.AutoSaveSlot);
            svc.Checkpoint(SaveService.AutoSaveSlot);
            svc.Checkpoint(SaveService.AutoSaveSlot);

            Assert.That(svc.Ledger.NextWritableSeq, Is.EqualTo(SaveService.MinManualSlotSeq),
                "自动存档写多少次都不许动手动槽高水位 —— 动了就是「自动存档吃掉手动槽号」" +
                "(TD-persist-17 的原病灶)");
        }

        [Test]
        public void test_autoSlot_secondWriteProducesBak()
        {
            SaveService svc = MakeService(tick: 10L);
            svc.Checkpoint(SaveService.AutoSaveSlot);
            svc.Checkpoint(SaveService.AutoSaveSlot);

            Assert.IsTrue(File.Exists(Path.Combine(_dir, "save_auto.bin.bak")),
                "自动槽被反复重写 ⇒ final→bak 分支**可达** ⇒ AC-7a-15 (a) 的「回退上一好档」" +
                "对自动槽成立(对玩家槽仍不成立,TD-persist-16 的口径不变)");
        }

        [Test]
        public void test_ledger_reservedSlotCommit_neverAdvancesHighWater()
        {
            // ⚠️ 这条**故意绕开 `SaveService`**:后者的构造器会把 floor 夹到 3,
            //    于是「保留槽推进高水位」在生产 floor 下**不可观测**(2+1 = 3,推不动)。
            //    台账是公开类型,它自己的契约必须能被单独钉住 —— 否则那道守卫
            //    在证伪轮里是**等价变异**,永远抓不到(2026-11-03 实测 M2 零红)。
            var ledger = new SaveSlotLedger(FuncNarrativeContextQuery.AlwaysIn, initialNextWritableSeq: 0);

            ledger.OnCheckpointCommitted(SaveService.AutoSaveSlot);

            Assert.That(ledger.NextWritableSeq, Is.EqualTo(0u),
                "保留槽(1/2)提交后**不得**推进高水位 —— 它们不在递增段里");
        }

        [Test]
        public void test_slotZero_isRejected()
        {
            SaveService svc = MakeService(tick: 10L);
            Assert.Throws<InvalidDataException>(() => svc.Checkpoint(new SaveSlot(0)),
                "槽 0 不是合法槽号(1 = 退出 · 2 = 自动 · 3+ = 手动)");
        }

        [Test]
        public void test_manualSlotBelowThree_isTreatedAsReservedNotRejected()
        {
            SaveService svc = MakeService(tick: 10L);
            // 槽 1 是退出保存的身份 —— 写它不该抛(它是保留槽),但**也不该**写出手动槽文件。
            Assert.DoesNotThrow(() => svc.Checkpoint(SaveService.ExitSaveSlot));
            Assert.IsTrue(File.Exists(Path.Combine(_dir, "save_exit.bin")),
                "槽 1 = 退出保存 ⇒ 落 `save_exit.bin`");
            Assert.IsFalse(File.Exists(Path.Combine(_dir, "save_1.bin")),
                "**不得**出现 `save_1.bin`");
        }

        // ══════════════════════════════════════════════════════════════════
        // ② 「最近档」= tick 最新(不是槽号最大)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_mostRecent_autoBeatsManual_whenAutoTickIsNewer()
        {
            // 手动槽 3 写 tick 10;自动槽写 tick 99(更近)。
            MakeService(tick: 10L).Checkpoint(new SaveSlot(3));
            MakeService(tick: 99L).Checkpoint(SaveService.AutoSaveSlot);

            SaveService reader = MakeService(tick: 0L);
            Assert.That(reader.TryGetMostRecentSave(out SaveSlot recent), Is.True);
            Assert.That(recent.SlotSeq, Is.EqualTo(SaveService.AutoSaveSlot.SlotSeq),
                "自动存档更近 ⇒ 它就是「继续诊治」的目标 —— " +
                "若按旧的「槽号最大」判据,这里会错选槽 3(崩溃损失远超一个间隔)");
        }

        [Test]
        public void test_mostRecent_manualBeatsAuto_whenManualTickIsNewer()
        {
            MakeService(tick: 5L).Checkpoint(SaveService.AutoSaveSlot);
            MakeService(tick: 50L).Checkpoint(new SaveSlot(3));

            SaveService reader = MakeService(tick: 0L);
            Assert.That(reader.TryGetMostRecentSave(out SaveSlot recent), Is.True);
            Assert.That(recent.SlotSeq, Is.EqualTo(3u),
                "手动存档更近 ⇒ 它是目标(判据是 tick,不是「自动优先」)");
        }

        [Test]
        public void test_mostRecent_exitSaveParticipatesByTick()
        {
            MakeService(tick: 7L).Checkpoint(new SaveSlot(3));
            SaveService writer = MakeService(tick: 70L);
            writer.SaveOnExit();

            SaveService reader = MakeService(tick: 0L);
            Assert.That(reader.TryGetMostRecentSave(out SaveSlot recent), Is.True);
            Assert.That(recent.SlotSeq, Is.EqualTo(SaveService.ExitSaveSlot.SlotSeq),
                "退出保存 tick 更近 ⇒ 它是目标(它同样靠 tick 参与比较,不再「无条件优先」)");
        }
    }
}
