// 存档三条路径的**场景夹具**(2026-11-04 · 用户指令「试试退出 / 自动 / 手动三种情况的正常存档」)。
//
// ─────────────────────────────────────────────────────────────────────────────
// 与既有夹具的分工
// ─────────────────────────────────────────────────────────────────────────────
// 既有件各测一段:`save_exit_load_test`(退出写的可读性)· `checkpoint_fallback_test`
// (定时器计数)· `save_fixed_slots_test`(槽位契约)。**没有一件**把三条路径放进
// **同一份装配、同一个目录**里走一遍并回答「盘上到底有没有档、能不能读回来」。
// 本件补的就是这个整体问题 —— 用户问的是「正常存档」,不是「某个类的方法对不对」。
//
// 三条路径的**真实生产者**(不是手调下层 API):
//   ① 自动:`CheckpointFallback.OnTick()` → `Checkpoint(AutoSaveSlot)`
//      (生产接线 = `CompositionRoot.cs` 的定时兜底;间隔 6000 tick ≙ 5 min)
//   ② 手动:`SaveSlotCommandRouter.TrySave()` → `Checkpoint(Ledger.NextWritableSeq)`
//      (生产 = 存档位屏「留档」行)
//   ③ 退出:`SaveService.SaveOnExit()`
//      (生产 = `BootRoot` 的 wantsToQuit 处理器)
//
// 判据一律落在**可观测量**上:文件在不在、`Load` 抛不抛、`TryGetMostRecentSave` 说得出什么。
// ⚠️ 不断「返回值 true」—— 那只是「已受理」(`TrySave` 的文档明写),不是「已落盘」。
//
// 写盘边界:全部落 `Path.GetTempPath()`(FsWriteBoundaryGate 允许形态),TearDown 清目录。

using System;
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
    public sealed class SaveThreePathsTest
    {
        private string _dir;
        private InMemoryBlobPool _pool;
        private PayloadDecoder _decoder;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "dyjq_threepaths_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _pool = new InMemoryBlobPool();
            _decoder = new PayloadDecoder(_pool);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir))
            {
                try { Directory.Delete(_dir, true); } catch { }
            }
        }

        /// <summary>能真写、真读的 `SaveService`(与 `save_exit_load_test` 同一装配形状)。</summary>
        private SaveService MakeService(EventStream stream, long tick = 10L)
            => new SaveService(
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
                blobRestoreSink: _pool);

        private static EventStream NewStream()
            => new EventStream(new FakeIdAuthority(), new FakePresenceQuery());

        private string[] FilesOnDisk()
        {
            string[] files = Directory.GetFiles(_dir);
            for (int i = 0; i < files.Length; i++) files[i] = Path.GetFileName(files[i]);
            Array.Sort(files, StringComparer.Ordinal);
            return files;
        }

        // ══════════════════════════════════════════════════════════════════
        // ① 自动存档:定时兜底到点 ⇒ 盘上有一份,且读得回来
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_autoSave_firesOnInterval_andReloads()
        {
            SaveService svc = MakeService(NewStream());
            var timer = new CheckpointFallback(
                () => svc.Checkpoint(SaveService.AutoSaveSlot),
                FuncNarrativeContextQuery.AlwaysIn,
                intervalTicks: 3);

            timer.OnTick();
            timer.OnTick();
            Assert.That(File.Exists(Path.Combine(_dir, SaveService.AutoSaveFileName)), Is.False,
                "未到间隔 ⇒ 不该写(正控前置:否则下面的断言在「每 tick 都写」时也成立)");

            timer.OnTick();

            Assert.That(File.Exists(Path.Combine(_dir, SaveService.AutoSaveFileName)), Is.True,
                $"自动存档到点须落盘({SaveService.AutoSaveFileName})");
            Assert.DoesNotThrow(() => svc.Load(SaveService.AutoSaveSlot),
                "自动存档必须能读回来 —— 写得进读不出 = 玩家「继续诊治」时才发现");
        }

        // ══════════════════════════════════════════════════════════════════
        // ② 手动存档:走「留档」的真实入口,写到下一可写槽
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_manualSave_writesNextWritableSlot_andReloads()
        {
            SaveService svc = MakeService(NewStream());
            var router = new SaveSlotCommandRouter(sequencer: null, failureSink: null, saveService: svc);

            Assert.That(router.IsSaveWired, Is.True, "[前置] 落笔半边须已接线");
            uint expected = svc.Ledger.NextWritableSeq;

            Assert.That(router.TrySave(), Is.True, "「留档」须受理");

            string file = Path.Combine(_dir, $"save_{expected}.bin");
            Assert.That(File.Exists(file), Is.True,
                $"手动存档须落 `save_{expected}.bin`;盘上实有:[{string.Join(", ", FilesOnDisk())}]");

            Assert.DoesNotThrow(() => svc.Load(new SaveSlot(expected)),
                "手动档必须能读回来");
        }

        // ══════════════════════════════════════════════════════════════════
        // ③ 退出存档:SaveOnExit ⇒ 独立文件名,且读得回来
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_exitSave_writesDedicatedFile_andReloads()
        {
            SaveService svc = MakeService(NewStream());

            svc.SaveOnExit();

            Assert.That(File.Exists(Path.Combine(_dir, SaveService.ExitSaveFileName)), Is.True,
                $"退出保存须落 {SaveService.ExitSaveFileName}");
            Assert.DoesNotThrow(() => svc.Load(SaveService.ExitSaveSlot),
                "退出保存必须能读回来(主菜单「继续诊治」的唯一来源之一)");
        }

        // ══════════════════════════════════════════════════════════════════
        // ④ 三条并存:同一会话里都发生过 ⇒ 三份文件互不覆盖、都能读
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_allThreePaths_coexist_withoutOverwritingEachOther()
        {
            SaveService svc = MakeService(NewStream());

            var timer = new CheckpointFallback(
                () => svc.Checkpoint(SaveService.AutoSaveSlot),
                FuncNarrativeContextQuery.AlwaysIn,
                intervalTicks: 1);
            timer.OnTick();                                  // 自动

            var router = new SaveSlotCommandRouter(sequencer: null, failureSink: null, saveService: svc);
            Assert.That(router.TrySave(), Is.True);
            uint manualSeq = router.LastSavedSlot.Value;     // 手动

            svc.SaveOnExit();                                // 退出

            Assert.That(File.Exists(Path.Combine(_dir, SaveService.AutoSaveFileName)), Is.True,
                $"自动档缺失;盘上:[{string.Join(", ", FilesOnDisk())}]");
            Assert.That(File.Exists(Path.Combine(_dir, $"save_{manualSeq}.bin")), Is.True,
                $"手动档缺失;盘上:[{string.Join(", ", FilesOnDisk())}]");
            Assert.That(File.Exists(Path.Combine(_dir, SaveService.ExitSaveFileName)), Is.True,
                $"退出档缺失;盘上:[{string.Join(", ", FilesOnDisk())}]");

            Assert.DoesNotThrow(() => svc.Load(SaveService.AutoSaveSlot), "自动档须仍可读");
            Assert.DoesNotThrow(() => svc.Load(new SaveSlot(manualSeq)), "手动档须仍可读");
            Assert.DoesNotThrow(() => svc.Load(SaveService.ExitSaveSlot), "退出档须仍可读");

            Assert.That(svc.TryGetMostRecentSave(out SaveSlot recent), Is.True,
                "三份都在 ⇒ 「最近档」必须说得出一个");
            Assert.That(recent.SlotSeq, Is.EqualTo(SaveService.ExitSaveSlot.SlotSeq).Or
                                              .EqualTo(manualSeq).Or
                                              .EqualTo(SaveService.AutoSaveSlot.SlotSeq),
                $"「最近档」返回了盘上不存在的槽 {recent.SlotSeq}(说有档却读不出是最坏的形态)");
        }
    }
}
