// 2026-11-03 smoke 轮发现的两条**运行期**缺陷的夹具(7a 槽位台账)
//
// 病因(用户实测,`production/qa/smoke-2026-11-03.md`):
//   ① 「存了,什么都没发生」—— 台账高水位初值恒 0,而存档位屏只列 `{1,2,3}`
//      ⇒ 本次会话**第一次**写入落在槽 0,那号在屏上不存在。
//   ② 「槽位 slot=1 已锁 … 锁持续到下一次叙事事件 checkpoint」**永不自解** ——
//      置锁在 `SaveService.Load` 成功时,解锁唯一路径 `OnNarrativeAnchor()`
//      **全仓零生产调用点**。
//
// 本文件断的是**机制**:初值通道可达 + 锁可被叙事锚点解除 + **跨会话高水位不被重置**
// (第三项 = `TD-persist-15`,见 ①-b;前两项是 smoke 轮那两条)。
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
                    timeParams: new SaveTimeParams(36000L, 27000L, 9000L, 252000L, 5L, 6000L, 100L), existingFoldedRows: stream.FoldedRows, blobPool: _pool),
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
            SaveService svc = MakeService(NewStream(), seed: SaveService.MinManualSlotSeq);
            Assert.That(svc.Ledger.NextWritableSeq, Is.EqualTo(SaveService.MinManualSlotSeq),
                "装配层传 3(手动槽第一个号)⇒ 第一次手动写入落在槽 3;初值通道此前**不存在**");
        }

        [Test]
        public void test_smoke_defaultInitialNextWritableSeq_clampsToFirstManualSlot()
        {
            SaveService svc = new SaveService(_testDir, new SaveCodec(),
                narrativeContext: FuncNarrativeContextQuery.AlwaysIn);
            Assert.That(svc.Ledger.NextWritableSeq, Is.EqualTo(3u),
                "缺省 0 被**夹到 3** —— 0 不是合法槽号,1/2 是保留槽(退出保存 / 自动存档)");
        }

        [Test]
        public void test_smoke_firstCheckpointLandsOnVisibleSlot()
        {
            EventStream stream = NewStream();
            SaveService svc = MakeService(stream, seed: SaveService.MinManualSlotSeq);

            svc.Checkpoint(new SaveSlot(svc.Ledger.NextWritableSeq));

            Assert.That(File.Exists(Path.Combine(_testDir, "save_3.bin")), Is.True,
                "第一次手动存档必须落在 `save_3.bin`(手动槽的第一个号)");
        }

        // ══════════════════════════════════════════════════════════════════
        // ①-b 跨会话:磁盘上已有槽位 ⇒ 高水位必须越过它们(TD-persist-15)
        // ══════════════════════════════════════════════════════════════════
        //
        // 病因:`initialNextWritableSeq` 曾是**唯一**来源 ⇒ 每个新会话内存里都从 floor 起
        //   ⇒ `EnsureWritable(floor)` 判 `floor < floor` = false ⇒ 放行 ⇒ 顶掉磁盘上同号那份。
        //   用户机器上已实测发生:`save_1.bak` 是旧会话的 3868B / `save_1.bin` 是新会话的 1868B。
        // 修法:`SaveService` 构造期扫**文件名**取 `max + 1`,与该 floor 取大。
        //
        // ⚠️ 扫的是**文件名**,不是存档体内容 ⇒ 不触碰 ADR-010 §六「不进存档体」。
        // ⚠️ 三件事**分开断**(合起来才叫「没覆盖」):高水位真被抬高 / 旧槽拒写且字节不变 /
        //    新写落在下一空槽。只断第一条时,「放行了但恰好没写坏」也能绿。
        // 🔁 2026-11-03(`TD-persist-17`):手动槽从 **3** 起(1 = 退出保存 · 2 = 自动存档)。

        private static void WriteSentinel(string dir, uint seq, string content)
            => File.WriteAllText(Path.Combine(dir, $"save_{seq}.bin"), content);

        [Test]
        public void test_crossSession_existingSlots_raiseHighWaterPastThem()
        {
            WriteSentinel(_testDir, 3, "SESSION-1-A");
            WriteSentinel(_testDir, 4, "SESSION-1-B");
            WriteSentinel(_testDir, 5, "SESSION-1-C");

            SaveService svc = MakeService(NewStream(), seed: SaveService.MinManualSlotSeq);

            Assert.That(svc.Ledger.NextWritableSeq, Is.EqualTo(6u),
                "磁盘上有 save_3..save_5 ⇒ 本会话下一可写槽必须是 6;从 3 起就是跨会话覆盖");
        }

        /// <summary>
        /// 🔁 **2026-11-04 改判(ADR-041 §D2/§D4)**:跨会话**显式覆写**既有槽位现在**允许**,
        /// 且旧版必须留进 `.bak`(上一版可抢救)。
        /// <para>⚠️ 原判据是「跨会话覆盖被拒」—— 它守的是 `TD-persist-15` 那条**意外**覆盖
        /// (高水位每会话复位);那一半仍由 `test_crossSession_nextSaveLandsOnFirstFreeSlot`
        /// 守着(**追加**仍落第一个空槽)。本条改成断「显式覆写是有意为之且有留底」。</para>
        /// </summary>
        [Test]
        public void test_crossSession_explicitOverwrite_isAllowed_andKeepsPrevious()
        {
            WriteSentinel(_testDir, 3, "SESSION-1-A");
            SaveService svc = MakeService(NewStream(), seed: SaveService.MinManualSlotSeq);

            Assert.DoesNotThrow(() => svc.Checkpoint(new SaveSlot(3)),
                "显式覆写既有槽位自 ADR-041 起合法");

            Assert.That(File.ReadAllText(Path.Combine(_testDir, "save_3.bak")), Is.EqualTo("SESSION-1-A"),
                "被覆写的那一版必须留在 `.bak`(ADR-041 §D4:上一版可回退)");
            Assert.That(File.ReadAllText(Path.Combine(_testDir, "save_3.bin")),
                Is.Not.EqualTo("SESSION-1-A"), "final 已被新内容顶替");
        }

        [Test]
        public void test_crossSession_nextSaveLandsOnFirstFreeSlot()
        {
            WriteSentinel(_testDir, 3, "SESSION-1-A");
            WriteSentinel(_testDir, 4, "SESSION-1-B");

            SaveService svc = MakeService(NewStream(), seed: SaveService.MinManualSlotSeq);
            svc.Checkpoint(new SaveSlot(svc.Ledger.NextWritableSeq));

            Assert.That(File.Exists(Path.Combine(_testDir, "save_5.bin")), Is.True,
                "新会话的第一次写入必须落在 save_5(下一空槽)");
            Assert.That(File.ReadAllText(Path.Combine(_testDir, "save_3.bin")), Is.EqualTo("SESSION-1-A"),
                "旧槽 save_3 不得被这次写入碰到");
        }

        [Test]
        public void test_crossSession_emptyDirectory_floorAppliesUnchanged()
        {
            SaveService svc = MakeService(NewStream(), seed: SaveService.MinManualSlotSeq);
            Assert.That(svc.Ledger.NextWritableSeq, Is.EqualTo(3u),
                "目录里一张槽都没有 ⇒ 退回 floor(空目录没有可覆盖的东西)");
        }

        [Test]
        public void test_crossSession_nonSlotFiles_doNotRaiseHighWater()
        {
            // 两个保留槽 + 原子写的中间/备份文件 + 无关文件:一个都不算**手动**槽。
            File.WriteAllText(Path.Combine(_testDir, "save_exit.bin"), "EXIT");
            File.WriteAllText(Path.Combine(_testDir, "save_auto.bin"), "AUTO");
            File.WriteAllText(Path.Combine(_testDir, "save_9.bak"), "BAK");
            File.WriteAllText(Path.Combine(_testDir, "save_9.tmp"), "TMP");
            File.WriteAllText(Path.Combine(_testDir, "save_notes.txt"), "TXT");

            SaveService svc = MakeService(NewStream(), seed: SaveService.MinManualSlotSeq);

            Assert.That(svc.Ledger.NextWritableSeq, Is.EqualTo(3u),
                "`save_exit.bin` / `save_auto.bin` / `.bak` / `.tmp` / 非 .bin 一律不算手动槽");
        }

        [Test]
        public void test_crossSession_legacyLowNumberedFiles_areIgnored()
        {
            // 🔁 2026-11-03(`TD-persist-17`):槽号语义改过 —— `save_0.bin` 现在是**非法槽号**,
            //    `save_1.bin` / `save_2.bin` 是与保留槽撞号的**旧命名孤儿**。
            //    三者都**不得**参与高水位(否则手动槽会被推到它们后面)。
            File.WriteAllText(Path.Combine(_testDir, "save_0.bin"), "LEGACY-0");
            File.WriteAllText(Path.Combine(_testDir, "save_1.bin"), "LEGACY-1");
            File.WriteAllText(Path.Combine(_testDir, "save_2.bin"), "LEGACY-2");

            SaveService svc = new SaveService(_testDir, new SaveCodec(),
                narrativeContext: FuncNarrativeContextQuery.AlwaysIn);

            Assert.That(svc.Ledger.NextWritableSeq, Is.EqualTo(3u),
                "低号孤儿文件不抬高水位:1/2 是保留槽的号,0 非法 —— 都不能把第一个手动槽推走");
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

            svc.Load(new SaveSlot(3));                       // 首次读档成功
            Assert.That(svc.Ledger.IsLocked(new SaveSlot(3)), Is.True, "读档成功即置锁(AC-7a-19 (d))");

            // 🔁 **2026-11-06(用户裁定「锁搬到写侧」)**:已锁挡的是**写**。
            //    旧形态在此断「再读被拒」—— 那条已作废(读可以反复);现断覆写被拒。
            Assert.DoesNotThrow(() => svc.Load(new SaveSlot(3)), "已锁仍可读(读可反复)");
            Assert.Throws<InvalidOperationException>(() => svc.Checkpoint(new SaveSlot(3)),
                "同一会话内覆写该槽 = 已锁(锁在写侧)");

            svc.OnNarrativeAnchor();                          // 「下一次叙事事件 checkpoint」

            Assert.That(svc.Ledger.IsLocked(new SaveSlot(3)), Is.False, "叙事锚点到达 ⇒ 锁解除");
            Assert.DoesNotThrow(() => svc.Checkpoint(new SaveSlot(3)),
                "解锁后同一槽位必须**重新可写** —— 这是本条修复的全部意义(现落点在写侧)");
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
