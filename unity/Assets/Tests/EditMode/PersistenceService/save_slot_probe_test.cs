// `SaveSlotProbe` 的夹具(2026-11-03 甲方案第一层)。
//
// 权威来源:
//   design/ux/save-slots-7b.md `OQ-SB-5`(「42 不知道哪个槽有存档」的那个缺口)
//   design/gdd/skeuomorphic-ui.md `AC-42-B5`(空位可聚焦但零文本)
//
// 断的四件事:
//   ① 路径契约与 `SaveService` **同源**(真存一份再探,不是比字符串);
//   ② 三种可区分状态:Empty / Occupied(带 tick)/ Corrupt;
//   ③ `Unknown` 与 `Empty` **不混**(「我不知道」不得冒充「这里没有」);
//   ④ `.bak` 回退与 `SaveService.LoadBytes` 同向(否则「屏上说有档、读档说没有」)。
//
// 写盘边界:全部落 `Path.GetTempPath()`(FsWriteBoundaryGate 允许形态)。

using System;
using System.IO;
using DaYiJingCheng.Gameplay.Boot;              // SaveComposer(组版闭包)
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Tests.DiseaseSimulation;   // FakeIdAuthority / FakePresenceQuery
using NUnit.Framework;

namespace DaYiJingCheng.Tests.PersistenceService
{
    [TestFixture]
    public class SaveSlotProbeTest
    {
        private string _dir;
        private InMemoryBlobPool _pool;
        private PayloadDecoder _decoder;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "dyjq_probe_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _pool = new InMemoryBlobPool();
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

        /// <summary>能真写盘的 `SaveService`(<paramref name="tick"/> 决定头部里的 tick)。</summary>
        private SaveService MakeService(EventStream stream, long tick)
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
                    timeParams: new SaveTimeParams(36000L, 27000L, 9000L, 252000L, 5L, 6000L, 100L), existingFoldedRows: stream.FoldedRows, blobPool: _pool),
                writeScheduler: action => action(),
                blobRestoreSink: _pool);

        private static EventStream NewStream()
            => new EventStream(new FakeIdAuthority(), new FakePresenceQuery());

        // ══════════════════════════════════════════════════════════════════
        // ① 路径契约与 SaveService 同源
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_probe_pathMatchesSaveService_realWriteThenProbe()
        {
            SaveService svc = MakeService(NewStream(), tick: 4242L);
            svc.Checkpoint(new SaveSlot(1));

            // 探针走的目录来源 = SaveService.BasePath(不是测试自己拼的路径)
            SaveSlotSummary s = SaveSlotProbe.Probe(svc.BasePath, 1);

            Assert.That(s.State, Is.EqualTo(SaveSlotState.Occupied),
                "真写一份之后,探同一个目录同一个槽必须判 Occupied —— " +
                "路径命名若与 SaveService 不同源,这里就是「屏上说没档、读档说有」");
            Assert.That(s.Tick, Is.EqualTo(4242L),
                "tick 须逐位等于组版时给的 tick(第二层「第 N 日」的唯一输入)");
            Assert.That(s.SaveVersion, Is.EqualTo(SaveHeader.CurrentSaveVersion));
        }

        // ══════════════════════════════════════════════════════════════════
        // ② 空 / 有档 / 坏档 三态可分
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_probe_neverWrittenSlot_isEmpty()
        {
            SaveSlotSummary s = SaveSlotProbe.Probe(_dir, 2);
            Assert.That(s.State, Is.EqualTo(SaveSlotState.Empty),
                "目录里没有这个槽的任何文件 ⇒ Empty(呈现侧画格线零字)");
        }

        [Test]
        public void test_probe_corruptFile_isNotReportedAsEmpty()
        {
            File.WriteAllBytes(Path.Combine(_dir, "save_2.bin"), new byte[] { 1, 2, 3 });

            SaveSlotSummary s = SaveSlotProbe.Probe(_dir, 2);

            Assert.That(s.State, Is.EqualTo(SaveSlotState.Corrupt),
                "文件在但解不出 ⇒ Corrupt。**不得**报 Empty:" +
                "那会让玩家以为这里能写新档,而 7a 的 Load 会拒");
        }

        [Test]
        public void test_probe_wrongMagic_isCorrupt()
        {
            byte[] junk = new byte[SaveHeaderCodec.HeaderSize];   // magic 全 0 ≠ "DYJQ"
            File.WriteAllBytes(Path.Combine(_dir, "save_3.bin"), junk);

            Assert.That(SaveSlotProbe.Probe(_dir, 3).State, Is.EqualTo(SaveSlotState.Corrupt));
        }

        // ══════════════════════════════════════════════════════════════════
        // ③ 「不知道」不得伪装成「没有」
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_probe_nullBasePath_isUnknownNot_empty()
        {
            Assert.That(SaveSlotProbe.Probe(null, 1).State, Is.EqualTo(SaveSlotState.Unknown),
                "未注入目录 = 不知道,不是空");
            Assert.That(SaveSlotProbe.Probe(string.Empty, 1).State,
                        Is.EqualTo(SaveSlotState.Unknown));
        }

        // ══════════════════════════════════════════════════════════════════
        // ④ `.bak` 回退与 SaveService.LoadBytes 同向
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_probe_bakOnly_isOccupied()
        {
            // 造一份真档,再手工搬成 .bak(模拟「final 丢了、上一好档还在」)
            SaveService svc = MakeService(NewStream(), tick: 77L);
            svc.Checkpoint(new SaveSlot(1));
            File.Move(Path.Combine(_dir, "save_1.bin"), Path.Combine(_dir, "save_1.bak"));

            SaveSlotSummary s = SaveSlotProbe.Probe(_dir, 1);

            Assert.That(s.State, Is.EqualTo(SaveSlotState.Occupied),
                "LoadBytes 会回退读 .bak ⇒ 探针必须同向,否则「屏上说没档、按下去却读出来了」");
            Assert.That(s.Tick, Is.EqualTo(77L));
        }

        [Test]
        public void test_probe_finalPreferredOverBak()
        {
            SaveService old = MakeService(NewStream(), tick: 5L);
            old.Checkpoint(new SaveSlot(1));                       // save_1.bin(tick 5)
            File.Copy(Path.Combine(_dir, "save_1.bin"), Path.Combine(_dir, "save_1.bak"));

            // 再写一次 ⇒ final = tick 9,bak = 旧 tick 5(双档机制)
            SaveService fresh = MakeService(NewStream(), tick: 9L);
            fresh.Checkpoint(new SaveSlot(1));

            Assert.That(SaveSlotProbe.Probe(_dir, 1).Tick, Is.EqualTo(9L),
                "final 可读时以 final 为准(与 LoadBytes 的次序一致)");
        }
    }
}
