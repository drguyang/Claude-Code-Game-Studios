// S22-5 · `TD-processing-06` 结案件 —— **跨存档往返**的 `CompleteTick` 等值(真落盘 → 真读回)
//
// ▍债的原话(`docs/tech-debt-register.md` §TD-processing-06)
//   现有 `independentRebuilds_areBitIdentical` 是**换池重编码**(同进程两个 codec 实例),
//   **不等于**「存档落盘 → 读回」。真正的往返「依赖 7a 的读路径,今天物理上不存在」。
//
// ▍本件为什么今天能立(债的触发条件已满足)
//   读路径**已经落地**:`SaveService.Load` + `LoadSequencer` + ADR-034 的载荷段
//   (`save_replay_roundtrip_test.cs` 已用它做 Checkpoint → Load → Restore 全链)。
//   ⇒ 债里「`SaveReader` 类名全仓不存在」是**类名**层面的,不是**能力**层面的 ——
//   能力住 `SaveService.Load`,本件照 `save_replay_roundtrip_test` 同一装配法调它。
//
// ▍与 `save_replay_roundtrip_test` 的分野
//   那件钉的是**病史流折叠 + 高水位**(AC-7a-01 面);本件钉的是**世界流里的 `Craft`**
//   经过落盘/读回后 `CompleteTick` 是否仍与直算逐位相同(AC-18-17 ③ 的往返半边)。
//   两件共用同一装配法,**不共用断言**。
//
// ▍判据(可证伪)
//   A `craftRows_surviveRoundtrip`:**前置** —— 读回后的流里 `Craft` 行数 == 存前(否则下面的
//     等值断言会在「两边都空」上恒真,那是 F4 空洞正控)
//   B `completeTick_matchesDirectReplayAtEverySamplePoint`:24 个采样点 × 2 个 actor 逐点对比
//   C `completeTick_hitsLiteralFormula`:字面量哨兵 —— 不给被测常量留「两边一起动」的余地
//     (start `100` + duration `50` ⇒ `150`;若只写 `occ.CompleteTick == StartTick + Duration`
//      就成了自指判据,F3)
//   D `busyActorCount_matchesDirectReplay`:跨玩家并发计数同往返等值(AC-18-23 的往返半边)
//
// ⚠️ 本件**不测**:跨构建逐位一致(= `TD-ci-01` EXTERNAL,ADR-012 三格矩阵,凭证在用户侧);
//   折叠段对 `Craft` 的影响(世界流不折叠,ADR-008 §六)。
//
// 写盘边界:全部落 `Path.GetTempPath()`(`FsWriteBoundaryGate` 允许形态;零仓库锚点)。

using System;
using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.Processing;
using DaYiJingCheng.Tests.DiseaseSimulation;   // FakeIdAuthority / FakePresenceQuery
using NUnit.Framework;

namespace DaYiJingCheng.Tests.PersistenceService
{
    /// <summary>`Craft` 事件经存档落盘 → 读回后,炉状态仍与直算逐位相同(`TD-processing-06`)。</summary>
    public sealed class CraftSaveRoundtripTest
    {
        private string _testDir;
        private InMemoryBlobPool _pool;
        private PayloadEncoder _encoder;
        private PayloadDecoder _decoder;

        // ── 夹具常量(字面量哨兵依赖它们,故写成具名常量而非散落魔数)────────────
        private const int ActorA = 1;
        private const int ActorB = 2;
        private const long AStart = 100L, ADuration = 50L;    // ⇒ 完成 150
        private const long BStart = 120L, BDuration = 80L;    // ⇒ 完成 200
        private const long ASecondStart = 200L, ASecondDuration = 40L;   // ⇒ 完成 240

        [SetUp]
        public void SetUp()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "dyjq_s22_5_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDir);
            _pool = new InMemoryBlobPool();
            _encoder = new PayloadEncoder(_pool);
            _decoder = new PayloadDecoder(_pool);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_testDir))
            {
                try { Directory.Delete(_testDir, true); } catch { /* 清场失败不影响判定 */ }
            }
        }

        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_craftEvents_afterSaveAndLoad_keepCompleteTickAndBusyCount()
        {
            // ══ Arrange:两 actor 三炉,全在**世界流** ══
            EventStream original = NewStream();
            original.Append(Craft(ActorA, AStart, ADuration, seq: 0L));
            original.Append(Craft(ActorB, BStart, BDuration, seq: 1L));
            original.Append(Craft(ActorA, ASecondStart, ASecondDuration, seq: 2L));

            SaveTo(original, slotSeq: 0);

            // ══ Act:「进程重启」⇒ 新流 + 同目录 Load(读路径 = SaveService.Load)══
            EventStream reloaded = NewStream();
            LoadInto(reloaded, slotSeq: 0);

            // ══ Assert A(前置):世界事件真的回来了 ══
            Assert.AreEqual(original.Events.Count, reloaded.Events.Count,
                "读回的事件条数必须与存前相同 —— 否则下面的等值断言会在「两边都空」上恒真(空洞正控)");
            Assert.AreEqual(3, CountCraft(reloaded.Events),
                $"读回后 `Craft` 行数必须仍是 3(实际 {CountCraft(reloaded.Events)})—— " +
                "少一行说明世界流在往返里被丢,不是「状态还没变」");

            // ══ Assert B:24 个采样点 × 2 actor 逐点等值 ══
            for (long t = 90L; t <= 260L; t += 10L)
            {
                foreach (int actor in new[] { ActorA, ActorB })
                {
                    bool directOk = FurnaceState.TryCompleteTickOf(
                        original.Events, _decoder, actor, t, out long direct);
                    bool reloadedOk = FurnaceState.TryCompleteTickOf(
                        reloaded.Events, _decoder, actor, t, out long reloadedTick);

                    Assert.AreEqual(directOk, reloadedOk,
                        $"tick {t} · actor {actor}:「点过火没有」在往返后必须同判");
                    if (directOk)
                    {
                        Assert.AreEqual(direct, reloadedTick,
                            $"tick {t} · actor {actor}:完成时点逐位相同(直算 {direct} / 读回 {reloadedTick})");
                    }
                }
            }

            // ══ Assert C:字面量哨兵(防自指判据)══
            Assert.IsTrue(FurnaceState.TryCompleteTickOf(
                    reloaded.Events, _decoder, ActorA, 100L, out long firstOfA),
                "actor 1 在 tick 100 已点过火");
            Assert.AreEqual(150L, firstOfA,
                $"start {AStart} + duration {ADuration} ⇒ 完成 150(**字面量**,不是 `Start + Duration` 的自指重算)");

            Assert.IsTrue(FurnaceState.TryCompleteTickOf(
                    reloaded.Events, _decoder, ActorA, 200L, out long secondOfA),
                "actor 1 的第二炉已开始");
            Assert.AreEqual(240L, secondOfA,
                $"第二炉 start {ASecondStart} + duration {ASecondDuration} ⇒ 完成 240");

            // ══ Assert D:跨玩家并发的往返等值(AC-18-23 的往返半边)══
            for (long t = 90L; t <= 260L; t += 10L)
            {
                Assert.AreEqual(FurnaceState.BusyActorCount(original.Events, _decoder, t),
                                FurnaceState.BusyActorCount(reloaded.Events, _decoder, t),
                                $"tick {t}:在烧人数在往返后必须相同");
            }

            // 两炉交叠的确定点:150..200 之间 A 的第二炉未开始、第一炉已结束 ⇒ 只剩 B
            Assert.AreEqual(1, FurnaceState.BusyActorCount(reloaded.Events, _decoder, 160L),
                "tick 160:只有 B 在烧(字面量)");
            Assert.AreEqual(2, FurnaceState.BusyActorCount(reloaded.Events, _decoder, 120L),
                "tick 120:A 第一炉与 B 同时在烧(字面量)");
        }

        // ══════════════════════════════════════════════════════════════════
        // 装配(照 `save_replay_roundtrip_test` 同一形态;两件各自独立,不共享 helper)
        // ══════════════════════════════════════════════════════════════════

        private static EventStream NewStream()
            => new EventStream(new FakeIdAuthority(), new FakePresenceQuery());

        private SimEvent Craft(int actorId, long startTick, long durationTicks, long seq)
        {
            var payload = new CraftPayload(
                actorId, recipeId: 1, startTick, durationTicks,
                inputInstanceIds: new long[] { 101 },
                actualConsumed: new int[] { 1 },
                outputQty: new int[] { 1 },
                outputQuality: new int[] { 3 },
                outputInstanceIds: new long[] { 201 },
                toolCell: new WorldPos(4, 0, 7));
            return new SimEvent(startTick, PatientId.None, seq, EventKind.Craft,
                                _encoder.Encode(EventKind.Craft, payload));
        }

        private static int CountCraft(IReadOnlyList<SimEvent> events)
        {
            int n = 0;
            for (int i = 0; i < events.Count; i++)
                if (events[i].Kind == EventKind.Craft) n++;
            return n;
        }

        /// <summary>空注册表 + 世界中立谓词:本件没有病人,折叠段恒空。</summary>
        private FoldedPredicate PredicateOf(EventStream stream) => new FoldedPredicate(
            historyStream: () => SaveComposer.FilterByStream(stream.Events, StreamId.History),
            caseStream: () => SaveComposer.FilterByStream(stream.Events, StreamId.Case),
            diseaseLookup: _ => null,
            decoder: _decoder);

        private SaveService NewService(EventStream stream,
                                       Action<IReadOnlyList<SimEvent>> replaySink,
                                       EventStream foldTarget = null)
            => new SaveService(
                _testDir, new SaveCodec(),
                folded: PredicateOf(stream),
                narrativeContext: FuncNarrativeContextQuery.AlwaysIn,
                replaySink: replaySink,
                foldedRowSink: bytes => (foldTarget ?? stream).Restore(
                    LastReplayed ?? throw new InvalidOperationException("折叠段先于事件段到达"),
                    FoldRowCodec.DecodeSegment(bytes)),
                // `compose` 契约 = 返回未签名(校验和由写 worker 补);此处不得再包 AddChecksum。
                compose: _ => SaveComposer.Compose(
                    stream.Events, PredicateOf(stream), _decoder, new SaveCodec(),
                    worldSeed: 1UL, configVersion: 0U, tick: 260L,
                    existingFoldedRows: stream.FoldedRows, blobPool: _pool),
                writeScheduler: action => action(), blobRestoreSink: _pool);

        /// <summary>最近一次回放的事件段(读档时先到;折叠段紧随其后)。</summary>
        private IReadOnlyList<SimEvent> LastReplayed;

        private void SaveTo(EventStream stream, uint slotSeq)
        {
            var svc = NewService(stream, _ => { });
            svc.Checkpoint(new SaveSlot(slotSeq));
            Assert.IsTrue(File.Exists(Path.Combine(_testDir, $"save_{slotSeq}.bin")),
                $"前置:checkpoint 应写出 save_{slotSeq}.bin");
        }

        private void LoadInto(EventStream target, uint slotSeq)
        {
            LastReplayed = null;
            var svc = NewService(target, events => LastReplayed = events, foldTarget: target);
            svc.Load(new SaveSlot(slotSeq));
            Assert.IsNotNull(LastReplayed, "前置:读档必须回调事件段(否则 restore 从未发生)");
        }
    }
}
