// 延迟威胁**生产接线**判据(AC-52-29 ② · GDD 规则六之三 · T22 批五 · 2026-11-11)
//
// 权威:
//   `design/gdd/random-events.md` 规则六之三(:284-305)—— 入槽 / 当日内离开医馆补发 /
//     跨日作废 / 补发时预算耗尽 ⇒ 丢弃;`DeferredThreatSlot` 当日有界 `DEFER_MAX`
//   AC-52-29(BLOCKING `[B][I]`)—— 压制(**已落**)与**延后**(本条)
//   `TD-randomevents-08` 的解除动作 ①–④
//   ADR-007 §三(槽是状态,必须进流)· ADR-039 §D2(状态全部从流重构)
//
// ── 本文件要证明的七件事 ─────────────────────────────────────────────────────
//   ① **入槽**:威胁档 ∧ 医馆内 ⇒ 抽一次、落 `EventRolled`、落 `ThreatDeferred`;
//      **不落 `EventArrived`**、不产病人(「压制之后去哪」= 槽,不是垃圾箱);
//   ② **补发**:当日内离开医馆 ⇒ `EventArrived` + `ThreatDeferralCleared(reason=补发)`;
//   ③ **跨日作废**:挂起日 < 当前日 ⇒ `ThreatDeferralCleared(reason=作废)`,无降临;
//   ④ **预算耗尽 ⇒ 丢弃**:当日预算已满时补发 = 清槽(作废),不降临;
//   ⑤ **空槽耗尽 ⇒ 零写入**:`DEFER_MAX` 满时**连 `EventRolled` 都不落**(防孤儿行);
//   ⑥ **未接线 ⇒ 旧语义**:不给延后三参 ⇒ 仍 `NoQuota` 且零写入(不做半接);
//   ⑦ **不误伤**:不在医馆时威胁档照常 `Spawned`(压制只在医馆内)。
//
// ⚠️ 本件**不读仓库文件**(纯合成件:池 / 权威 / 在场 / 医馆格全部内联),因为判据对象是
//    接线**逻辑**,不是内容表。真数据的端到端另由 `random_events_end_to_end_day_test` 覆盖。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.RandomEvents
{
    /// <summary>延迟威胁接线 —— 入槽 / 补发 / 作废 / 预算丢弃 / 空槽 / 未接线。</summary>
    [TestFixture]
    public sealed class deferred_threat_wiring_test
    {
        private const int PlayerId = 7;
        private const int ThreatTier = (int)EventTier.Threat;
        private const int DeferMax = 3;
        private const long TicksPerDay = 100;
        private const long RollInterval = 10;
        private const int PoolKey = 1001;

        private InMemoryBlobPool _pool;
        private PayloadEncoder _encoder;
        private PayloadDecoder _decoder;
        private CapturingSink _sink;
        private FakeClinicCells _clinic;
        private DeferredThreatSlotWriter _defer;

        [SetUp]
        public void SetUp()
        {
            _pool = new InMemoryBlobPool();
            _encoder = new PayloadEncoder(_pool);
            _decoder = new PayloadDecoder(_pool);
            _sink = new CapturingSink();
            _clinic = new FakeClinicCells(new WorldPos(0, 0, 0), new WorldPos(1, 0, 0));
            _defer = new DeferredThreatSlotWriter(_sink, _encoder);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  夹具
        // ══════════════════════════════════════════════════════════════════════

        private static RandomEventsDataSet Config() => new RandomEventsDataSet
        {
            TicksPerDay = TicksPerDay,
            RollInterval = RollInterval,
            WindowSize = 6,
            Base = 2,
            BudgetMax = 6,
        };

        private RandomEventSpawnChain Chain(int tier, bool inClinic, bool deferralWired = true)
            => Chain(tier, _ => inClinic, deferralWired);

        private RandomEventSpawnChain Chain(int tier, Func<long, bool> inClinicAt, bool deferralWired = true)
        {
            return new RandomEventSpawnChain(
                _sink, _encoder,
                new FixedAuthority(PoolKey),
                candidatesOfTier: (_, __) => new[] { new WeightedKey(PoolKey, 100) },
                presence: new FakePresence(),
                spawnAnchorOf: _ => new WorldPos(5, 0, 5),
                spawnPatient: (_, __) => new PatientId(1),
                contextOf: tick => new F1PipelineInput(0, 6, isNight: false, isOnExpedition: false,
                                                       isInClinic: inClinicAt(tick), inCombatCooldown: false,
                                                       seasonIndex: 0),
                events: () => _sink.Events,
                tier: tier,
                config: Config(),
                requiresPatient: _ => false,
                canSpawnPatient: _ => true,
                deferWriter: deferralWired ? _defer : null,
                deferDecoder: deferralWired ? _decoder : null,
                deferMax: deferralWired ? DeferMax : 0);
        }

        private DeferredThreatReleaseDriver Release(int baseEvents = 2, int budgetMax = 6,
                                                    bool bindPlayer = true)
        {
            var d = new DeferredThreatReleaseDriver(
                () => _sink.Events, _sink, _encoder, _decoder, _clinic, _defer,
                spawnAnchorOf: _ => new WorldPos(5, 0, 5),
                deferMax: DeferMax, ticksPerDay: TicksPerDay,
                baseEvents: baseEvents, budgetMax: budgetMax);
            if (bindPlayer) d.BindPlayer(PlayerId);   // 由导演绑的情形传 false(导演会转发)
            return d;
        }

        private void CellEntered(long tick, int x, int z)
        {
            _sink.Append(new SimEvent(tick, PatientId.None, -1, EventKind.ActorCellEntered,
                _encoder.Encode(EventKind.ActorCellEntered,
                    new ActorCellEnteredPayload(PlayerId, new WorldPos(x, 0, z), tick))));
        }

        private int Count(EventKind kind)
        {
            int n = 0;
            foreach (SimEvent e in _sink.Events) if (e.Kind == kind) n++;
            return n;
        }

        private ThreatDeferredPayload LastDeferred()
        {
            for (int i = _sink.Events.Count - 1; i >= 0; i--)
                if (_sink.Events[i].Kind == EventKind.ThreatDeferred)
                    return _decoder.Decode<ThreatDeferredPayload>(EventKind.ThreatDeferred,
                                                                  _sink.Events[i].Payload);
            Assert.Fail("流里没有 ThreatDeferred");
            return default;
        }

        private ThreatDeferralClearedPayload LastCleared()
        {
            for (int i = _sink.Events.Count - 1; i >= 0; i--)
                if (_sink.Events[i].Kind == EventKind.ThreatDeferralCleared)
                    return _decoder.Decode<ThreatDeferralClearedPayload>(EventKind.ThreatDeferralCleared,
                                                                        _sink.Events[i].Payload);
            Assert.Fail("流里没有 ThreatDeferralCleared");
            return default;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  ① 入槽 —— 「压制之后去哪」= 挂起槽(不是丢弃)
        // ══════════════════════════════════════════════════════════════════════
        [Test]
        public void test_inClinicThreat_deferred_notDropped_noArrived()
        {
            SpawnChainOutcome outcome = Chain(ThreatTier, inClinic: true).TrySpawnAtTick(RollInterval);

            Assert.That(outcome, Is.EqualTo(SpawnChainOutcome.ThreatDeferred),
                "医馆内威胁档 ⇒ 必须走延后支,而不是 NoQuota / Spawned");
            Assert.That(Count(EventKind.EventRolled), Is.EqualTo(1),
                "延后支**仍掷骰**(载荷要一个具体 key)⇒ 恰一条 EventRolled");
            Assert.That(Count(EventKind.ThreatDeferred), Is.EqualTo(1), "恰一条 ThreatDeferred");
            Assert.That(Count(EventKind.EventArrived), Is.EqualTo(0),
                "**不得降临** —— 这正是 AC-52-29 ①「被压制」那一半");

            ThreatDeferredPayload p = LastDeferred();
            Assert.That(p.SlotIndex, Is.EqualTo(0), "空槽集 ⇒ 取最小空闲槽 0");
            Assert.That(p.EventKey, Is.EqualTo(PoolKey), "记的 key = 本次抽中的 key");
        }

        // ══════════════════════════════════════════════════════════════════════
        //  ② 补发 —— 当日内离开医馆
        // ══════════════════════════════════════════════════════════════════════
        [Test]
        public void test_leavingClinicSameDay_reissuesWithBudget()
        {
            var chain = Chain(ThreatTier, inClinic: true);
            Assert.That(chain.TrySpawnAtTick(RollInterval), Is.EqualTo(SpawnChainOutcome.ThreatDeferred));

            // 玩家先在医馆内(格 (0,0,0)),后跨到医馆外(格 (9,0,9))—— 同一天(day 0)
            CellEntered(tick: 15, x: 0, z: 0);
            CellEntered(tick: 20, x: 9, z: 9);

            DeferredThreatReleaseReport report = Release().OnTickEdge(20);

            Assert.That(report.Reissued, Is.EqualTo(1), "同日内离开医馆 ⇒ 立即补发一条");
            Assert.That(Count(EventKind.EventArrived), Is.EqualTo(1), "补发 = 一条 EventArrived");
            Assert.That(LastCleared().Reason, Is.EqualTo(DeferredThreatSlotWriter.ReasonReissued),
                "清槽 reason = 补发(0)");
            Assert.That(LastCleared().SlotIndex, Is.EqualTo(0));
        }

        // ══════════════════════════════════════════════════════════════════════
        //  ③ 跨日作废 —— 不结转
        // ══════════════════════════════════════════════════════════════════════
        [Test]
        public void test_crossDay_voided_noReissue()
        {
            // 挂起在 day 0(tick 10 < 100),补发时刻在 day 1(tick 150)
            Assert.That(_defer.TryDefer(10, 0, PoolKey, DeferMax),
                        Is.EqualTo(DeferredThreatWriteResult.Applied));

            CellEntered(tick: 140, x: 0, z: 0);
            CellEntered(tick: 150, x: 9, z: 9);

            DeferredThreatReleaseReport report = Release().OnTickEdge(150);

            Assert.That(report.VoidedCrossDay, Is.EqualTo(1), "挂起日 < 当前日 ⇒ 作废");
            Assert.That(report.Reissued, Is.EqualTo(0), "**不得补发** —— 跨日的威胁一次性倾泻正是 F2 要防的");
            Assert.That(Count(EventKind.EventArrived), Is.EqualTo(0));
            Assert.That(LastCleared().Reason, Is.EqualTo(DeferredThreatSlotWriter.ReasonVoided));
        }

        // ══════════════════════════════════════════════════════════════════════
        //  ④ 预算耗尽 ⇒ 丢弃(GDD:「补发时若当日预算已耗尽,则丢弃」)
        // ══════════════════════════════════════════════════════════════════════
        [Test]
        public void test_budgetExhausted_dropsInsteadOfReissuing()
        {
            Assert.That(_defer.TryDefer(10, 0, PoolKey, DeferMax),
                        Is.EqualTo(DeferredThreatWriteResult.Applied));

            // 当日已有 2 条降临 = 预算上限(base 2 · mods ∅ · budgetMax 6 ⇒ clamp 2)
            for (int i = 0; i < 2; i++)
            {
                _sink.Append(new SimEvent(11 + i, PatientId.None, -1, EventKind.EventArrived,
                    _encoder.Encode(EventKind.EventArrived,
                        new EventArrivedPayload(PoolKey, ThreatTier, new WorldPos(5, 0, 5), 0))));
            }

            CellEntered(tick: 15, x: 0, z: 0);
            CellEntered(tick: 20, x: 9, z: 9);

            DeferredThreatReleaseReport report = Release(baseEvents: 2, budgetMax: 6).OnTickEdge(20);

            Assert.That(report.VoidedBudgetExhausted, Is.EqualTo(1), "预算满 ⇒ 丢弃该槽");
            Assert.That(report.Reissued, Is.EqualTo(0));
            Assert.That(Count(EventKind.EventArrived), Is.EqualTo(2), "降临数不增加");
            Assert.That(LastCleared().Reason, Is.EqualTo(DeferredThreatSlotWriter.ReasonVoided),
                "reason 闭集只有 {补发, 作废} —— 预算丢弃记作废(词表缺口已登记在驱动头注)");
        }

        // ══════════════════════════════════════════════════════════════════════
        //  ⑤ 空槽耗尽 ⇒ **零写入**(连骰都不落)
        // ══════════════════════════════════════════════════════════════════════
        [Test]
        public void test_deferSlotsFull_zeroWrites_noOrphanRoll()
        {
            for (int slot = 0; slot < DeferMax; slot++)
                Assert.That(_defer.TryDefer(1, slot, PoolKey, DeferMax),
                            Is.EqualTo(DeferredThreatWriteResult.Applied));

            int before = _sink.Events.Count;
            SpawnChainOutcome outcome = Chain(ThreatTier, inClinic: true).TrySpawnAtTick(RollInterval);

            Assert.That(outcome, Is.EqualTo(SpawnChainOutcome.DeferSlotsFull));
            Assert.That(_sink.Events.Count, Is.EqualTo(before),
                "槽满 ⇒ **零写入** —— 落一条 EventRolled 就成了「掷过骰、没入槽、也没降临」的孤儿行");
        }

        // ══════════════════════════════════════════════════════════════════════
        //  ⑥ 未接线 ⇒ 旧语义(不做半接)
        // ══════════════════════════════════════════════════════════════════════
        [Test]
        public void test_deferralNotWired_fallsBackToNoQuota_zeroWrites()
        {
            SpawnChainOutcome outcome =
                Chain(ThreatTier, inClinic: true, deferralWired: false).TrySpawnAtTick(RollInterval);

            Assert.That(outcome, Is.EqualTo(SpawnChainOutcome.NoQuota));
            Assert.That(_sink.Events.Count, Is.EqualTo(0), "未接线 ⇒ 与接线前逐位相同:零写入");
        }

        // ══════════════════════════════════════════════════════════════════════
        //  ⑦ 不误伤 —— 不在医馆时威胁档照常降临
        // ══════════════════════════════════════════════════════════════════════
        [Test]
        public void test_outsideClinic_threatSpawnsNormally()
        {
            SpawnChainOutcome outcome = Chain(ThreatTier, inClinic: false).TrySpawnAtTick(RollInterval);

            Assert.That(outcome, Is.EqualTo(SpawnChainOutcome.Spawned),
                "医馆外威胁档必须照常生成 —— 压制只在医馆内");
            Assert.That(Count(EventKind.EventArrived), Is.EqualTo(1));
            Assert.That(Count(EventKind.ThreatDeferred), Is.EqualTo(0));
        }

        // ══════════════════════════════════════════════════════════════════════
        //  ⑧ 导演主循环**真的驱动**这两条(接线支的运行时腿)
        // ══════════════════════════════════════════════════════════════════════
        // ⚠️ 单靠 `CompositionRoot` 里 `new` 了两件,证不了「有人调它们」——
        //    那正是 `TD-randomevents-08` 自己的病根(写者存在、零调用方)。本条钉调用点。
        [Test]
        public void test_director_drivesThreatChainAndRelease()
        {
            var director = new RandomEventsDirectorDriver(
                () => _sink.Events, _sink, _encoder, _decoder, _clinic,
                houseCallEventKeys: Array.Empty<int>(),
                spawnChain: null,
                playerActor: null,
                // 上下文按 tick 变:边界 10 在医馆内(⇒ 延后),边界 20 已在医馆外(⇒ 正常抽)
                threatSpawnChain: Chain(ThreatTier, tick => tick == RollInterval),
                deferredRelease: Release(bindPlayer: false));
            director.BindPlayer(PlayerId);

            director.OnTickEdge(RollInterval);

            Assert.That(director.LastThreatSpawnOutcome, Is.EqualTo(SpawnChainOutcome.ThreatDeferred),
                "导演边沿必须驱动威胁档链 —— 改前生产只有机会档一条链,威胁档整局不抽");
            Assert.That(director.LastDeferredRelease, Is.Not.Null, "导演边沿必须驱动延迟收尾");
            Assert.That(Count(EventKind.ThreatDeferred), Is.EqualTo(1));

            // 同一会话里再走到「离开医馆」⇒ 补发
            CellEntered(tick: RollInterval + 5, x: 0, z: 0);
            CellEntered(tick: 2 * RollInterval, x: 9, z: 9);
            director.OnTickEdge(2 * RollInterval);

            Assert.That(director.LastDeferredRelease?.Reissued, Is.EqualTo(1),
                "跨出医馆那一边沿 ⇒ 导演把补发跑起来(只补**已挂起**的那条)");
            // 该边沿两条降临:① 威胁档在医馆外正常抽中并降临 ② 挂起槽补发
            Assert.That(Count(EventKind.EventArrived), Is.EqualTo(2));
            Assert.That(Count(EventKind.ThreatDeferred), Is.EqualTo(1),
                "医馆外那一边沿不该再入槽");
        }

        // ══════════════════════════════════════════════════════════════════════
        //  夹具件
        // ══════════════════════════════════════════════════════════════════════

        private sealed class CapturingSink : IEventSink
        {
            public readonly List<SimEvent> Events = new List<SimEvent>();
            public void Append(in SimEvent e) => Events.Add(e);
        }

        private sealed class FixedAuthority : IEventAuthority
        {
            private readonly int _key;
            public FixedAuthority(int key) { _key = key; }
            public bool IsAuthority => true;
            public bool IsHost => true;
            public EventRollResult Roll(in RollRequest r)
                => new EventRollResult(0, r.Tier, r.Ordinal, _key);
        }

        private sealed class FakePresence : IPresenceQuery
        {
            public int PresentCount => 0;
            public bool IsPresent(PatientId patientId) => false;
            public bool IsPresentAt(WorldPos cell) => false;
            public IReadOnlyCollection<int> PresentPatientIds() => Array.Empty<int>();
        }

        private sealed class FakeClinicCells : IClinicCellSource
        {
            private readonly WorldPos[] _cells;
            public FakeClinicCells(params WorldPos[] cells) { _cells = cells; }
            public bool IsClinicCell(WorldPos cell)
            {
                foreach (WorldPos c in _cells)
                    if (c.X == cell.X && c.Y == cell.Y && c.Z == cell.Z) return true;
                return false;
            }
        }
    }
}
