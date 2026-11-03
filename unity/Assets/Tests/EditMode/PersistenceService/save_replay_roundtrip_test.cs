// Checkpoint → Load → Restore 全链往返 —— TD-persist-03 的真重放端到端钉。
//
// 权威来源:
//   docs/tech-debt-register.md TD-persist-03 —— 债描述「列表 → sim 状态」段的兑现判据
//   AC-7a-01(persistence-service.md:597)—— 重放后「id 高水位 == 直接重放所得」的形状
//   ADR-006 Amendment B —— 折叠行必留 patient_id,高水位重构依赖
//
// 与单测的分野:单测钉各部件(restore 三关 / codec 布局 / 组版段),
// 本文件只钉**全链**:存档字节 → 读档解出 → Restore 进 sim → 状态可比。
//
// 写盘边界:全部落 `Path.GetTempPath()`(FsWriteBoundaryGate 允许形态;零仓库锚点)。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Tests.DiseaseSimulation;   // FakeIdAuthority / FakePresenceQuery
using NUnit.Framework;

namespace DaYiJingCheng.Tests.PersistenceService
{
    public class SaveReplayRoundtripTest
    {
        private const int PatientA = 7;
        private const int DiseaseSelfLimit = 21;   // 非 plateau、非 lethal ⇒ 可折
        private const long Seed = 0x5EEDL;

        private string _testDir;
        private InMemoryBlobPool _pool;
        private PayloadEncoder _encoder;
        private PayloadDecoder _decoder;
        private Dictionary<int, DiseaseRegistryEntry> _registry;

        [SetUp]
        public void SetUp()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "dyjq_td_persist_03_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDir);
            _pool = new InMemoryBlobPool();
            _encoder = new PayloadEncoder(_pool);
            _decoder = new PayloadDecoder(_pool);
            _registry = new Dictionary<int, DiseaseRegistryEntry>
            {
                [DiseaseSelfLimit] = new DiseaseRegistryEntry
                {
                    DiseaseId = DiseaseSelfLimit,
                    DiseaseKey = "D" + DiseaseSelfLimit,
                    Kind = DiseaseKind.Disease,
                    Lethality = 1,
                    Curve = new NaturalProgressCurve { Plateau = false },
                },
            };
        }

        [TearDown]
        public void Teardown()
        {
            if (Directory.Exists(_testDir))
            {
                try { Directory.Delete(_testDir, true); } catch { }
            }
        }

        private SimEvent Onset(long tick, int patient, int disease)
            => new SimEvent(tick, new PatientId(patient), 0L, EventKind.DiseaseOnset,
                _encoder.Encode(EventKind.DiseaseOnset,
                    new DiseaseOnsetPayload(tick, disease, patient, Seed, 0L)));

        private SimEvent Outcome(long tick, int patient, int disease, PatientOutcome outcome)
            => new SimEvent(tick, new PatientId(patient), 0L, EventKind.PatientOutcomeChanged,
                _encoder.Encode(EventKind.PatientOutcomeChanged,
                    new PatientOutcomeChangedPayload(patient, disease, (int)outcome, 0L)));

        private FoldedPredicate PredicateOf(EventStream stream) => new FoldedPredicate(
            historyStream: () => SaveComposer.FilterByStream(stream.Events, StreamId.History),
            caseStream: () => SaveComposer.FilterByStream(stream.Events, StreamId.Case),
            diseaseLookup: id => _registry.TryGetValue(id, out var e) ? e : null,
            decoder: _decoder);

        [Test]
        public void test_replay_roundtrip_restoredRowsAndHighWater_matchPreSaveState()
        {
            // ══ Arrange:可折病人(已终态)进流 ⇒ Checkpoint(折叠发生,行落盘)══
            var stream = new EventStream(new FakeIdAuthority(), new FakePresenceQuery());
            stream.Append(Onset(10L, PatientA, DiseaseSelfLimit));
            stream.Append(Outcome(20L, PatientA, DiseaseSelfLimit, PatientOutcome.Died));

            var svc = new SaveService(
                _testDir, new SaveCodec(),
                folded: PredicateOf(stream),
                narrativeContext: FuncNarrativeContextQuery.AlwaysIn,
                replaySink: _ => { },
                foldedRowSink: _ => { },
                // 🔁 2026-10-25 AC-7a-07:`compose` 契约 = **返回未签名**,校验和由 worker 补。
                // ⚠️ 此处**不得**再包 `AddChecksum` —— 那会二次签名 ⇒ 头错位
                // (`Load` 报「存档版本 1508257137 > 当前 1」,回归实证)。
                compose: _ => SaveComposer.Compose(
                    stream.Events, PredicateOf(stream), _decoder, new SaveCodec(),
                    worldSeed: 1UL, configVersion: 0U, tick: 20L,
                    timeParams: new SaveTimeParams(36000L, 27000L, 9000L, 252000L, 5L, 6000L, 100L), existingFoldedRows: stream.FoldedRows, blobPool: _pool),
                writeScheduler: action => action(), blobRestoreSink: _pool);
            svc.Checkpoint(new SaveSlot(3));

            // ══ Act:「进程重启」⇒ 新流 + 同目录 Load,恢复闭包 = Restore ══
            var fresh = new EventStream(new FakeIdAuthority(), new FakePresenceQuery());
            IReadOnlyList<SimEvent> pending = null;
            var svcLoad = new SaveService(
                _testDir, new SaveCodec(),
                folded: PredicateOf(fresh),
                narrativeContext: FuncNarrativeContextQuery.AlwaysIn,
                replaySink: events => pending = events,
                foldedRowSink: bytes => fresh.Restore(
                    pending ?? throw new InvalidOperationException("折叠段先于事件段到达"),
                    FoldRowCodec.DecodeSegment(bytes)),
                // 同上:`compose` 返回未签名,worker 补校验和。
                compose: _ => SaveComposer.Compose(
                    fresh.Events, PredicateOf(fresh), _decoder, new SaveCodec(),
                    worldSeed: 1UL, configVersion: 0U, tick: 20L,
                    timeParams: new SaveTimeParams(36000L, 27000L, 9000L, 252000L, 5L, 6000L, 100L), existingFoldedRows: fresh.FoldedRows, blobPool: _pool),
                writeScheduler: action => action(), blobRestoreSink: _pool);
            svcLoad.Load(new SaveSlot(3));

            // ══ Assert:恢复态 == 直接重放所得(AC-7a-01 形状的三点)══
            Assert.AreEqual(0, fresh.Count,
                "被折病人病史行已抹:恢复后事件段不含其任何行");
            Assert.AreEqual(1, fresh.FoldedRows.Count, "折叠行经折叠段字节回到 sim");
            Assert.AreEqual(PatientA, fresh.FoldedRows[0].PatientId,
                "patient_id 必留(ADR-006 Amendment B)");
            Assert.AreEqual(Seed, fresh.FoldedRows[0].PatientSeed,
                "patient_seed 从 onset 载荷往返,不得重算");

            // 高水位:被折病人 id 只活在折叠行 ⇒ 漏掉它 = 新病人复用已折叠 id。
            Assert.AreEqual(PatientA + 1, fresh.GetNextPatientId().Value,
                "恢复后高水位须从折叠行重构(== 直接重放世界流所得)");

            // 二次 Checkpoint 不丢折叠行(回写义务):
            svcLoad.Checkpoint(new SaveSlot(4));
            byte[] secondSave = File.ReadAllBytes(Path.Combine(_testDir, "save_4.bin"));
            // ADR-037:正文起点 = 头部 + 配置版本段
            // ⚠️ ADR-038 §D3:v4 起头部多 24 B ⇒ 头部长度**按版本取**,不得硬编码 68。
            int headerSize = SaveHeaderCodec.HeaderSizeFor(SaveHeaderCodec.ReadSaveVersion(secondSave));
            ConfigVersionSegment.Decode(secondSave, headerSize, out int cfgBytes);
            var r = new CodecReader(secondSave.AsSpan(headerSize + cfgBytes));
            SimEventListCodec.ReadWithinBody(ref r, new SaveCodec());
            // 🔁 2026-10-28 ADR-034 §③:改按 count 精确读(原 `DecodeSegment(r.ReadBytes(r.Remaining))`
            //    在 v2 下会把其后的载荷段一起吞进折叠段 ⇒ 报「未读尽」)。
            IReadOnlyList<FoldRow> carried = FoldRowCodec.ReadSegment(ref r);
            Assert.AreEqual(1, carried.Count,
                "读档后的下一次 Checkpoint:折叠行经 existingFoldedRows 回写,不静默消失");
            Assert.AreEqual(PatientA, carried[0].PatientId);
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-7a-01 / AC-7a-02 —— 2026-11-03(S24-2)补的**判决性**载体
        //
        // ⚠️ **两条 AC 的前件今日不成立,必须如实分开记**:
        //   存档布局里**没有快照段**(`SaveComposer.cs:38/:56`:`SnapshotOffset` 恒写 0 哨兵,
        //   「快照段 / 分册态段留待后续」)⇒ 「快照损坏」与「快照与流不一致」都**没有宾语**。
        //   直接照 AC 措辞写夹具 = 造一条零输入可达的门(本项目已登记的假绿形态)。
        //
        // ⇒ 本测断的是**前件本身**(`SnapshotOffset == 0`,机械可判)+ AC 真正保护的
        //   **实质性质**:读档后的游戏事实**只**来自三逻辑流,且与「直接重放同一流」所得相等。
        //   快照段一旦引入,本测会先在 `SnapshotOffset == 0` 上转红,提醒补真快照夹具。
        // ══════════════════════════════════════════════════════════════════

        private SimEvent Drop(long tick, long instanceId, int itemKey, int qty)
            => new SimEvent(tick, PatientId.None, 0L, EventKind.DropSpawned,
                _encoder.Encode(EventKind.DropSpawned,
                    new DropSpawnedPayload(instanceId, new WorldPos(1, 0, 2), itemKey, qty)));

        [Test]
        public void test_ac7a01_noSnapshotSegment_andRestoredFactsEqualDirectReplay()
        {
            // ══ Arrange:世界流两条掉落 + 一条可折病史(已完成)→ Checkpoint ══
            var stream = new EventStream(new FakeIdAuthority(), new FakePresenceQuery());
            stream.Append(Onset(10L, PatientA, DiseaseSelfLimit));
            stream.Append(Drop(11L, instanceId: 5001L, itemKey: 2, qty: 3));
            stream.Append(Drop(12L, instanceId: 5002L, itemKey: 4, qty: 1));
            stream.Append(Outcome(20L, PatientA, DiseaseSelfLimit, PatientOutcome.Died));

            var svc = new SaveService(
                _testDir, new SaveCodec(),
                folded: PredicateOf(stream),
                narrativeContext: FuncNarrativeContextQuery.AlwaysIn,
                replaySink: _ => { },
                foldedRowSink: _ => { },
                compose: _ => SaveComposer.Compose(
                    stream.Events, PredicateOf(stream), _decoder, new SaveCodec(),
                    worldSeed: 1UL, configVersion: 0U, tick: 20L,
                    timeParams: new SaveTimeParams(36000L, 27000L, 9000L, 252000L, 5L, 6000L, 100L), existingFoldedRows: stream.FoldedRows, blobPool: _pool),
                writeScheduler: action => action(), blobRestoreSink: _pool);
            svc.Checkpoint(new SaveSlot(3));

            // ══ Assert ①:布局里**没有**快照段(AC-7a-01/02 的前件)══
            byte[] saved = File.ReadAllBytes(Path.Combine(_testDir, "save_3.bin"));
            SaveHeader header = SaveHeaderCodec.Read(saved);
            Assert.AreEqual(0UL, header.SnapshotOffset,
                "存档头写了非 0 的 SnapshotOffset ⇒ 快照段已引入 —— " +
                "AC-7a-01/02 必须补「快照损坏 / 与流不一致」的真夹具,不得继续沿用本测");

            // ══ Act:进程重启 ⇒ 新流 + 同目录 Load ══
            var fresh = new EventStream(new FakeIdAuthority(), new FakePresenceQuery());
            IReadOnlyList<SimEvent> pending = null;
            var svcLoad = new SaveService(
                _testDir, new SaveCodec(),
                folded: PredicateOf(fresh),
                narrativeContext: FuncNarrativeContextQuery.AlwaysIn,
                replaySink: events => pending = events,
                foldedRowSink: bytes => fresh.Restore(
                    pending ?? throw new InvalidOperationException("折叠段先于事件段到达"),
                    FoldRowCodec.DecodeSegment(bytes)),
                compose: _ => SaveComposer.Compose(
                    fresh.Events, PredicateOf(fresh), _decoder, new SaveCodec(),
                    worldSeed: 1UL, configVersion: 0U, tick: 20L,
                    timeParams: new SaveTimeParams(36000L, 27000L, 9000L, 252000L, 5L, 6000L, 100L),
                    existingFoldedRows: fresh.FoldedRows, blobPool: _pool),
                writeScheduler: action => action(), blobRestoreSink: _pool);
            svcLoad.Load(new SaveSlot(3));

            // ══ Assert ②:掉落清单 == 直接重放世界流所得 ══
            string dropFromRestored = InventoryFold
                .Fold(fresh.Events, _decoder, InventoryFoldContext.None).Projection.CanonicalForm();
            string dropFromDirect = InventoryFold
                .Fold(snapshotOfOriginal(stream), _decoder, InventoryFoldContext.None)
                .Projection.CanonicalForm();

            Assert.AreEqual(dropFromDirect, dropFromRestored,
                "读档后的掉落清单须逐位等于「直接重放存档前世界流」所得(AC-7a-01 的掉落半边)");
            Assert.AreEqual(2, fresh.Events.Count(e => e.Kind == EventKind.DropSpawned),
                "两条掉落必须都在 —— 少一条即「静默吞」");

            // ══ Assert ③:id 高水位 == 直接重放所得 ══
            Assert.AreEqual(PatientA + 1, fresh.GetNextPatientId().Value,
                "高水位须从三流并集(含折叠行)重构(AC-7a-01 的高水位半边)");

            // ══ Assert ④:折叠行 == 直接重放所得(终态事实)══
            Assert.AreEqual(1, fresh.FoldedRows.Count);
            Assert.AreEqual(PatientA, fresh.FoldedRows[0].PatientId);
        }

        /// <summary>把原流的**事件快照**取出来(直接重放用的对照面)。
        /// <para>⚠️ 复制一份是必须的:`fresh` 在 `Load` 之后是新流,而对照面必须仍是
        /// 「存档前那条流」的内容 —— 直接引用 `stream.Events` 会让后续 `Checkpoint`
        /// 改动悄悄改变对照面(夹具自指)。</para>
        /// </summary>
        private static IReadOnlyList<SimEvent> snapshotOfOriginal(EventStream stream)
            => new List<SimEvent>(stream.Events);
    }
}
