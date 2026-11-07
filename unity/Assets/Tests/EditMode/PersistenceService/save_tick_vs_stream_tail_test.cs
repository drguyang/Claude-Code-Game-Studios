// P0(2026-11-06)「档头 tick **不得早于**流尾 tick」—— 一变式两半的执行体。
//
// ▍病因(用户实测)
//   「继续诊治」之后玩家**走不动**,console 刷 1552 条
//   `InvalidOperationException: BCD-码-3: 事件 tick N 回退于已入流 tick 1554`。
//   机理:读档把三流换成存档内容(流尾 1554),而逻辑时钟被**档头 tick**定位 ——
//   若档头 tick 早于流尾,之后每一笔 `EventStream.Append` 都被 Tick 非降断言拒收:
//   世界形同停摆,且下一次写档会把更低的 tick 再写一遍(**坏档自我放大**)。
//
// ▍盘上实测(2026-11-06,用户存档目录)
//   `w2/save_exit.bin.bak`:档头 0     / 流尾 1554
//   `w2/save_3.bin`       :档头 179   / 流尾 1554   ← 由前一份坏档的会话写出
//
// ▍本件钉的两半(各一条不变式,各有正控/负控)
//   读侧:`SaveService.LastLoadedStreamTailTick` 交出流尾,`LoadSequencer` 按
//         **两者较大者**定位时钟(照档头定位会让世界停摆);
//   写侧:`SaveComposer.Compose` 的档头 tick 取 `max(调用方 tick, 流尾)`
//         —— 唯一组版口,坏档自今日起写不出来。
//
// ⚠️ 本件**不**判「用户那份档是谁写坏的」:那是历史,已不可复现;本件判的是
//   「今天起写不出坏档」+「已有坏档读回来不再停摆」。
// ⚠️ 落盘边界:全部落 `Path.GetTempPath()`;不读仓库内作者态文件。

using System;
using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Tests.DiseaseSimulation;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.PersistenceService
{
    /// <summary>档头 tick ↔ 流尾 tick 的不变式(2026-11-06 P0)。</summary>
    [TestFixture]
    public sealed class SaveTickVsStreamTailTest
    {
        /// <summary>夹具里的流尾 —— 与档头 tick 的三种关系各用一个**字面量**锚住。</summary>
        private const long StreamTail = 500L;

        private string _testDir;
        private InMemoryBlobPool _pool;
        private PayloadDecoder _decoder;
        private EventStream _stream;
        private FoldedPredicate _predicate;

        [SetUp]
        public void SetUp()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "dyjq_ticktail_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDir);
            _pool = new InMemoryBlobPool();
            _decoder = new PayloadDecoder(_pool);
            _stream = new EventStream(new FakeIdAuthority(), new FakePresenceQuery());
            _predicate = new FoldedPredicate(
                historyStream: () => SaveComposer.FilterByStream(_stream.Events, StreamId.History),
                caseStream: () => SaveComposer.FilterByStream(_stream.Events, StreamId.Case),
                diseaseLookup: _ => null,
                decoder: _decoder);

            // 流里**只有一条**事件,现场景唯一:tick 500(世界流,不参与折叠)。
            _stream.Append(new SimEvent(
                StreamTail, PatientId.None, seq: 0L, EventKind.ActorCellEntered, default));
        }

        [TearDown]
        public void Teardown()
        {
            if (Directory.Exists(_testDir))
            {
                try { Directory.Delete(_testDir, true); } catch { }
            }
        }

        // ══════════════ 读侧:交出流尾 + 按较大者定位 ══════════════

        [Test]
        public void test_load_reportsStreamTail_apartFromHeaderTick()
        {
            // 造一份「档头 0 / 流尾 500」的旧档(写侧已钉住 ⇒ 只能手改字节造出来)。
            WriteSave(PatchHeaderTick(ComposeUnsigned(tick: 900L), 0L));

            SaveService load = NewLoadSide();
            load.Load(new SaveSlot(4));

            Assert.That(load.LastLoadedTick, Is.EqualTo(0L),
                "LastLoadedTick 仍如实交出**档头**原值 —— 不在这里悄悄改口径");
            Assert.That(load.LastLoadedStreamTailTick, Is.EqualTo(StreamTail),
                "流尾必须作为一个**独立事实**交出来,否则消费侧无从发现档头落在了历史之前");
        }

        [Test]
        public void test_load_exposesZeroTail_whenEventSegmentEmpty()
        {
            _stream.Clear();
            WriteSave(ComposeUnsigned(tick: 900L));   // 空流 ⇒ 档头 900 原样

            SaveService load = NewLoadSide();
            load.Load(new SaveSlot(4));

            Assert.That(load.LastLoadedStreamTailTick, Is.EqualTo(0L),
                "空事件段 ⇒ 流尾 0(没有可保护的历史),不得沿用上一次读档的值");
            Assert.That(load.LastLoadedTick, Is.EqualTo(900L));
        }

        [Test]
        public void test_loadSequencer_restoresToStreamTail_whenHeaderTickBehind()
        {
            WriteSave(PatchHeaderTick(ComposeUnsigned(tick: 900L), 0L));

            long? restored = null;
            NewSequencer(t => restored = t).Load(new SaveSlot(4));

            Assert.That(restored, Is.EqualTo(StreamTail),
                "档头落在流尾之前 ⇒ 时钟必须按**流尾**定位;按档头(0)定位会让之后每一笔写入" +
                "被 BCD-码-3 拒收(用户实测:走不动 + 1552 条异常)");
        }

        [Test]
        public void test_loadSequencer_restoresToHeader_whenHeaderTickAhead()
        {
            // 负控:档头**在流尾之后**是正常形态(时钟在自己走,事件是稀疏的)⇒ 原样恢复。
            WriteSave(ComposeUnsigned(tick: 900L));

            long? restored = null;
            NewSequencer(t => restored = t).Load(new SaveSlot(4));

            Assert.That(restored, Is.EqualTo(900L),
                "档头在流尾之后 ⇒ 必须用档头(用流尾会让时钟倒退到 500)");
        }

        // ══════════════ 写侧:组版口不许写出坏档 ══════════════

        [Test]
        public void test_compose_headerTick_neverBehindStreamTail()
        {
            byte[] unsigned = ComposeUnsigned(tick: 0L);   // 时钟落后流尾 500

            long headerTick = ReadHeaderTick(unsigned);
            Assert.That(headerTick, Is.EqualTo(StreamTail),
                "组版口必须取流尾 —— 写进 0 的档读回来会把时钟放到历史之前(坏档自我放大)");
        }

        [Test]
        public void test_compose_headerTick_keptWhenAheadOfStreamTail()
        {
            // 负控:时钟正常领先(900 > 500)⇒ 不得被改写成流尾。
            byte[] unsigned = ComposeUnsigned(tick: 900L);

            Assert.That(ReadHeaderTick(unsigned), Is.EqualTo(900L),
                "时钟领先流尾时档头必须原样 —— 「永远写流尾」会把世界的时间线压回去");
        }

        [Test]
        public void test_composeThenLoad_roundTripsWithoutBeingBehind()
        {
            // 端到端:时钟落后时写档 ⇒ 读回来时钟**不落后于流尾**(P0 的直接判据)。
            WriteSave(ComposeUnsigned(tick: 0L));

            long? restored = null;
            NewSequencer(t => restored = t).Load(new SaveSlot(4));

            Assert.That(restored, Is.EqualTo(StreamTail));
        }

        // ══════════════ 夹具工具 ══════════════

        private byte[] ComposeUnsigned(long tick)
            => SaveComposer.Compose(
                _stream.Events, _predicate, _decoder, new SaveCodec(),
                worldSeed: 0x1122334455667788UL, configVersion: 7u, tick: tick,
                timeParams: new SaveTimeParams(36000L, 27000L, 9000L, 252000L, 5L, 6000L, 100L),
                existingFoldedRows: _stream.FoldedRows, blobPool: _pool);

        /// <summary>把档头 tick 改坏(**不含校验和**形态下的偏移 20 = magic 4 + 版本 4 + 种子 8 + 配置版本 4)。</summary>
        private static byte[] PatchHeaderTick(byte[] unsigned, long tick)
        {
            const int at = 4 + 4 + 8 + 4;
            for (int i = 0; i < 8; i++) unsigned[at + i] = (byte)(tick >> (8 * i));
            return unsigned;
        }

        /// <summary>读**不含校验和**形态里的档头 tick(与 <see cref="PatchHeaderTick"/> 同一偏移)。</summary>
        private static long ReadHeaderTick(byte[] unsigned)
        {
            const int at = 4 + 4 + 8 + 4;
            long v = 0L;
            for (int i = 0; i < 8; i++) v |= (long)unsigned[at + i] << (8 * i);
            return v;
        }

        private void WriteSave(byte[] unsigned)
            => File.WriteAllBytes(Path.Combine(_testDir, "save_4.bin"),
                                  SaveService.AddChecksum(unsigned));

        private SaveService NewLoadSide()
        {
            IReadOnlyList<SimEvent> pending = null;
            return new SaveService(
                _testDir, new SaveCodec(),
                folded: _predicate,
                narrativeContext: FuncNarrativeContextQuery.AlwaysIn,
                replaySink: events => pending = events,
                foldedRowSink: bytes => _stream.Restore(
                    pending ?? throw new InvalidOperationException("折叠段先于事件段到达"),
                    FoldRowCodec.DecodeSegment(bytes)),
                compose: _ => ComposeUnsigned(900L),
                migrations: new ISaveMigration[]
                {
                    new SaveMigrationV2ToV3(),
                    new SaveMigrationV3ToV4(),
                    new SaveMigrationV4ToV5(),
                    new SaveMigrationV5ToV6(),
                    // ADR-010 义务 13(2026-11-08):v6 → v7 插空分册态段;不补则旧档在链里断掉
                    new SaveMigrationV6ToV7(),
                },
                writeScheduler: action => action(), blobRestoreSink: _pool);
        }

        /// <summary>只关心「时钟被定位到哪」—— 其余缝合钩子给空实现(它们不是本件判据面)。</summary>
        /// <remarks>锚点触发器白名单非空(该类对空表 fail-loud,而「事件触发半边恒哑」
        /// 与本件无关);本文件的流里没有该 Kind ⇒ 恒不触发。</remarks>
        private LoadSequencer NewSequencer(Action<long> onRestore)
            => new LoadSequencer(
                NewLoadSide(),
                new AnchorCheckpointTrigger(() => true, FuncNarrativeContextQuery.AlwaysIn,
                    new[] { EventKind.DiseaseOnset }, onFired: () => { }),
                restoreTick: onRestore);
    }
}
