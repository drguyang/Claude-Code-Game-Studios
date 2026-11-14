// N5 奇遇链的判据(ADR-045 · 2026-11-14)
//
// 权威:`docs/architecture/adr-045-quest-stream.md` §D2 / §D4 / §D5 / §D6
//      `design/levels/prologue-beat-script.md` §3.1(PA-01…PA-05)
//
// ⚠️ 本件钉的是**机制**:去重、区域归属、阈值、读档重扫。它**不**证明
//    「五条能在真关卡里完成」—— 那需要区域内真有采集点(见 ADR-045 §R2 与
//    关卡资源点的现实:今日只有 3 个止血草点,全在营地 x≥96)。
//
// ⚠️ 零写盘 / 零随机 / 零墙钟。

using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.PatientAI;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Boot
{
    /// <summary>奇遇链(接取 / 完成 / 掉落 / 区域 / 读档)的机制判据。</summary>
    public class adventure_chain_test
    {
        private const int Player = 7;

        /// <summary>废墟 A(教堂)内的格。</summary>
        private static readonly WorldPos InRectA = new WorldPos(8, 0, 20);

        /// <summary>区域外(营地)。</summary>
        private static readonly WorldPos Outside = new WorldPos(116, 0, 20);

        private EventStream _stream;
        private InMemoryBlobPool _pool;
        private PayloadEncoder _encoder;
        private PayloadDecoder _decoder;
        private AdventureWriter _writer;

        [SetUp]
        public void SetUp()
        {
            _stream = new EventStream(new TestDropWriter.RecordingIdAuthority(), new PresenceRegistry());
            _pool = new InMemoryBlobPool();
            _encoder = new PayloadEncoder(_pool);
            _decoder = new PayloadDecoder(_pool);
            _writer = new AdventureWriter(_stream, _encoder, () => _stream.Events, _decoder,
                                          TestDropWriter.Create(_stream, _pool));
        }

        // ── 写者:去重 ────────────────────────────────────────────────────

        [Test]
        public void test_complete_isIdempotent_andDropsOnce()
        {
            AdventureStep step = AdventureSteps.PrologueFive[0];

            Assert.IsTrue(_writer.Complete(in step, Player, InRectA, tick: 10));
            Assert.IsFalse(_writer.Complete(in step, Player, InRectA, tick: 11),
                "重复完成必须 no-op(§D5)—— 否则组件会被发两次");

            Assert.AreEqual(1, Count(EventKind.AdventureCompleted), "完成事件恰一条");
            Assert.AreEqual(1, Count(EventKind.DropSpawned), "组件掉落恰一次(§D6)");

            // V-6:掉落的 item_key / qty / 锚点必须与完成载荷**同源**,不是另算一份。
            SimEvent drop = First(EventKind.DropSpawned);
            var payload = _decoder.Decode<DropSpawnedPayload>(EventKind.DropSpawned, drop.Payload);
            Assert.AreEqual(step.ItemKey, payload.ItemKey, "掉的就是那件组件");
            Assert.AreEqual(1, payload.Qty, "qty 与 AdventureCompleted 载荷一致");
            Assert.AreEqual(InRectA, payload.SpawnAnchor, "落在完成那一刻的玩家格(§D6「掉在脚下」)");

            // V-7:任务流事件**不进**折叠视图 —— 它们 `Patient = None`,产不出病人折叠行。
            Assert.AreEqual(0, _stream.FoldedRows.Count, "奇遇不产生折叠行(§D8)");
            Assert.AreEqual(PatientId.None, First(EventKind.AdventureCompleted).Patient,
                "任务流的 Patient 恒 None(§D2)");
        }

        [Test]
        public void test_questKindRoutesToQuestStream_atPriorityAfterWorld()
        {
            // V-1:Kind → 流 的路由与枚举值(既有三流零位移)。
            Assert.AreEqual(StreamId.Quest, StreamRouting.Of(EventKind.AdventureAccepted));
            Assert.AreEqual(StreamId.Quest, StreamRouting.Of(EventKind.AdventureCompleted));
            Assert.AreEqual(0, (int)StreamId.History, "既有值零位移");
            Assert.AreEqual(1, (int)StreamId.Case);
            Assert.AreEqual(2, (int)StreamId.World);
            Assert.AreEqual(3, (int)StreamId.Quest, "任务流优先级 = 3,排在 World 之后(ADR-045 §D1)");
        }

        [Test]
        public void test_questEvents_doNotMovePatientIdHighWatermark()
        {
            // V-8:`max(patient_id)` 扫集含任务流行,但它们 `Patient = None` ⇒ 高水位不变。
            PatientId before = _stream.GetNextPatientId();
            AdventureStep step = AdventureSteps.PrologueFive[0];
            _writer.Accept(step.Id, Player, tick: 1);
            _writer.Complete(in step, Player, InRectA, tick: 2);

            Assert.AreEqual(before.Value, _stream.GetNextPatientId().Value,
                "任务流不得抬高 patient_id 高水位(ADR-006 Amendment B)");
        }

        [Test]
        public void test_accept_isIdempotent()
        {
            Assert.IsTrue(_writer.Accept(adventureId: 0, actorId: Player, tick: 5));
            Assert.IsFalse(_writer.Accept(adventureId: 0, actorId: Player, tick: 6));
            Assert.AreEqual(1, Count(EventKind.AdventureAccepted));
        }

        [Test]
        public void test_differentActor_isNotConsideredDuplicate()
        {
            _writer.Accept(adventureId: 0, actorId: Player, tick: 5);
            Assert.IsTrue(_writer.Accept(adventureId: 0, actorId: Player + 1, tick: 5),
                "去重键是 (actor, adventure) —— 别的玩家接同一条奇遇不算重复");
        }

        // ── 驱动:区域 / 阈值 ─────────────────────────────────────────────

        [Test]
        public void test_driver_completesAfterRequiredHarvestsInRect()
        {
            AdventureDriver driver = MakeDriver(cell: InRectA);
            AdventureStep step = AdventureSteps.PrologueFive[0];   // 门板:6 次

            for (int i = 1; i <= step.Required; i++)
            {
                Harvest(tick: i * 2, cell: InRectA);
                driver.OnTickEdge(i * 2);
                if (i < step.Required)
                    Assert.AreEqual(0, Count(EventKind.AdventureCompleted),
                        $"第 {i} 次采集还不够 {step.Required} 次,不得完成");
            }

            Assert.AreEqual(step.Required, driver.CountOf(step.Id));
            Assert.AreEqual(1, Count(EventKind.AdventureAccepted), "首次命中即接取");
            Assert.AreEqual(1, Count(EventKind.AdventureCompleted));
            Assert.AreEqual(1, Count(EventKind.DropSpawned), "完成时恰发一次组件掉落");
        }

        [Test]
        public void test_driver_ignoresHarvestOutsideRect()
        {
            AdventureDriver driver = MakeDriver(cell: Outside);
            for (int i = 1; i <= 10; i++)
            {
                Harvest(tick: i * 2, cell: Outside);
                driver.OnTickEdge(i * 2);
            }

            Assert.AreEqual(0, driver.CountOf(0), "区域外采集不得计入废墟 A 的奇遇");
            Assert.AreEqual(0, Count(EventKind.AdventureCompleted));
        }

        [Test]
        public void test_driver_tickRollback_rescansFromScratch()
        {
            AdventureDriver driver = MakeDriver(cell: InRectA);
            AdventureStep step = AdventureSteps.PrologueFive[0];

            // 读档前:采 3 次
            for (int i = 1; i <= 3; i++)
            {
                Harvest(tick: i, cell: InRectA);
                driver.OnTickEdge(i);
            }
            Assert.AreEqual(3, driver.CountOf(step.Id));

            // 读档:流变短 + tick 回退 ⇒ 计数清零重扫(不是「时间倒流即抛」)
            _stream = new EventStream(new TestDropWriter.RecordingIdAuthority(), new PresenceRegistry());
            driver = MakeDriver(cell: InRectA);
            Harvest(tick: 1, cell: InRectA);
            driver.OnTickEdge(1);

            Assert.AreEqual(1, driver.CountOf(step.Id), "读档后应按**当前流**重新计数");
            Assert.AreEqual(0, Count(EventKind.AdventureCompleted));
        }

        // ── 辅助 ─────────────────────────────────────────────────────────

        private WorldPos _cell;

        private AdventureDriver MakeDriver(WorldPos cell)
        {
            // 驱动读 `_playerCell()`;夹具用它模拟「玩家此刻站哪」。
            _cell = cell;
            return new AdventureDriver(() => _stream.Events, AdventureSteps.PrologueFive,
                                       _writer, () => _cell, Player, _decoder);
        }

        private void Harvest(long tick, WorldPos cell)
        {
            // 顺序 = 先站好,再采(采集是持续交互,玩家必须站在点旁)。
            _cell = cell;
            _stream.Append(new SimEvent(tick, PatientId.None, 0L, EventKind.ResourceHarvested,
                _encoder.Encode(EventKind.ResourceHarvested,
                    new ResourceHarvestedPayload(instanceId: 100 + tick, nodeId: 1, gatherSeq: 1,
                                                 qty: 1, outQuality: 1))));
        }

        private int Count(EventKind kind)
        {
            int n = 0;
            IReadOnlyList<SimEvent> events = _stream.Events;
            for (int i = 0; i < events.Count; i++)
                if (events[i].Kind == kind) n++;
            return n;
        }

        private SimEvent First(EventKind kind)
        {
            IReadOnlyList<SimEvent> events = _stream.Events;
            for (int i = 0; i < events.Count; i++)
                if (events[i].Kind == kind) return events[i];
            Assert.Fail($"流里没有 {kind}");
            return default;
        }
    }
}
