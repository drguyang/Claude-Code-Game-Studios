// S22-4 · smoke `#14` 的载体 —— 52 急召生成链(抽池 → 掷骰 → F1 → `EventArrived`)
//
// ▍卡面的硬约束,逐条照办
//   > ⚠️ **必须用合成 key 集 + 合成格集驱动** —— 否则「双重空集 ⇒ 恒零写入」会让本条变成**假绿**
//   生产今日两个数据真空(52 池内容归 37 / 医馆格源占位)⇒ 生产调用**恒 `NoPool`**。
//   照生产数据写夹具,「零写入」既可能是「链路没跑」也可能是「跑对了没料」——
//   两者不可区分 = 假绿。故本件**全部**用合成 key 集(`7001/7002/7003`)与合成格(`(9,0,4)`)。
//
// ▍判据(可证伪)
//   A `inCap_writesRollThenArrival_withSyntheticKey`:界内 ⇒ **恰两条**(先 `EventRolled` 后
//     `EventArrived`)、key ∈ 合成集、锚点 == 合成格、病人创建恰一次
//   B `overCap_rejectsWithExplicitError_andWritesNothing`:满界 ⇒ **抛**且报文含 `AC-15` / `24`,
//     **流零写入**、**零掷骰**、**零病人创建**(副作用判据 —— 只断异常类型会被下层同类型异常冒充)
//   C `emptyPool_isFailSafe_noWritesNoThrow`:空池(生产形态)⇒ `NoPool`、零写入、零抛
//   D `roll_usesF1WindowAndTier`:掷骰请求的 `(win, tier, ordinal)` 恰是 F1 算出的那一组
//     —— 证明「掷骰真被调用且参数没被绕过」,而不是链里另有取法
//
// ⚠️ 本件**不测**:病人在场登记(归 9 / 15)、密度预算(未实现)、权重与历史标记乘子(未实现)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace DaYiJingCheng.Tests.RandomEvents
{
    /// <summary>52 急召生成链(合成输入驱动)。</summary>
    public sealed class RandomEventSpawnChainTest
    {
        // ── 合成输入(卡面要求:key 集与格集都必须合成)──────────────
        private static readonly int[] SyntheticKeys = { 7001, 7002, 7003 };
        private static readonly WorldPos SyntheticCell = new WorldPos(9, 0, 4);
        private const ulong WorldSeed = 0xA11CEUL;
        private const int Tier = 1;              // 机会档(急召出诊属此档)
        private const long WindowSize = 20L;
        private const int DiseaseId = 3;

        private InMemoryBlobPool _pool;
        private PayloadEncoder _encoder;
        private PayloadDecoder _decoder;

        [SetUp]
        public void SetUp()
        {
            _pool = new InMemoryBlobPool();
            _encoder = new PayloadEncoder(_pool);
            _decoder = new PayloadDecoder(_pool);
        }

        // ══════════════════════════════════════════════════════════════════
        // A 界内
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_inCap_writesRollThenArrival_withSyntheticKey()
        {
            var sink = new CapturingSink();
            var presence = new FakePresence { PresentCount = 3 };
            int spawnCalls = 0;

            RandomEventSpawnChain chain = NewChain(sink, presence, () => spawnCalls++);

            SpawnChainOutcome outcome = chain.TrySpawnAtTick(tick: 40L);

            Assert.AreEqual(SpawnChainOutcome.Spawned, outcome, "界内 + 有池 ⇒ 应生成");
            Assert.AreEqual(2, sink.Events.Count,
                $"一次降临 = `EventRolled` + `EventArrived` **恰两条**(实际 {sink.Events.Count})");

            Assert.AreEqual(EventKind.EventRolled, sink.Events[0].Kind, "先骰后降临(顺序固定)");
            Assert.AreEqual(EventKind.EventArrived, sink.Events[1].Kind);

            EventArrivedPayload arrived =
                _decoder.Decode<EventArrivedPayload>(EventKind.EventArrived, sink.Events[1].Payload);
            CollectionAssert.Contains(SyntheticKeys, arrived.EventKey,
                $"降临的 key 必须来自**合成候选集**(实际 {arrived.EventKey})—— " +
                "落在集合外说明抽池没用这份输入");
            Assert.AreEqual(SyntheticCell.X, arrived.SpawnAnchor.X, "锚点 = 合成格 X");
            Assert.AreEqual(SyntheticCell.Y, arrived.SpawnAnchor.Y, "锚点 = 合成格 Y");
            Assert.AreEqual(SyntheticCell.Z, arrived.SpawnAnchor.Z, "锚点 = 合成格 Z");
            Assert.AreEqual(Tier, arrived.Tier);

            Assert.AreEqual(1, spawnCalls, "病人出现恰一次(9 的写者)");
        }

        // ══════════════════════════════════════════════════════════════════
        // B 满界 —— 拒收 + 明确报错 + **零副作用**
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_overCap_rejectsWithExplicitError_andWritesNothing()
        {
            var sink = new CapturingSink();
            var presence = new FakePresence { PresentCount = EventStream.PATIENT_APPEARANCE_CAP };
            int spawnCalls = 0;
            var counter = new CountingAuthority(NewAuthority());

            RandomEventSpawnChain chain = NewChain(sink, presence, () => spawnCalls++, counter);

            var ex = Assert.Throws<InvalidOperationException>(
                () => chain.TrySpawnAtTick(tick: 40L),
                "满界 ⇒ 必须**抛**(不是静默返回 false:那是「没触发」与「满了」不可分)");

            StringAssert.Contains("AC-15", ex.Message, "报错须点出界名(AC-15)");
            StringAssert.Contains(EventStream.PATIENT_APPEARANCE_CAP.ToString(), ex.Message,
                "报错须带出界的数值 —— 「超上限」不说多少 = 排查时无从复算");

            Assert.AreEqual(0, sink.Events.Count,
                $"超界被拒 ⇒ **零写入** —— 实际写了 {sink.Events.Count} 条。" +
                "若 EventRolled 先落流,读档重放会凭空多一次掷骰(孤儿行)");
            Assert.AreEqual(0, counter.Calls, "被拒时**不得掷骰**(配额/CAP 都排在掷骰之前)");
            Assert.AreEqual(0, spawnCalls, "被拒时**不得创建病人**");
        }

        // ══════════════════════════════════════════════════════════════════
        // C 空池 —— 生产今日形态(fail-safe,不是缺陷)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_emptyPool_isFailSafe_noWritesNoThrow()
        {
            var sink = new CapturingSink();
            var presence = new FakePresence { PresentCount = 0 };
            int spawnCalls = 0;

            RandomEventSpawnChain chain = NewChain(sink, presence, () => spawnCalls++,
                keysOfTier: _ => Array.Empty<int>());

            SpawnChainOutcome outcome = SpawnChainOutcome.Spawned;   // 非 NoPool 的初值:若没被赋值,下面的断言语义仍成立
            Assert.DoesNotThrow(() => { outcome = chain.TrySpawnAtTick(tick: 40L); },
                "空池不得抛(那是生产今日的常态)");

            Assert.AreEqual(SpawnChainOutcome.NoPool, outcome,
                "空池 ⇒ **具名**的 NoPool(不是笼统 false:三种零写入形态必须可区分)");
            Assert.AreEqual(0, sink.Events.Count, "空池 ⇒ 零写入");
            Assert.AreEqual(0, spawnCalls, "空池 ⇒ 零病人创建");
        }

        // ══════════════════════════════════════════════════════════════════
        // D 掷骰参数 —— 证「真被调用且参数正确」
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_roll_usesF1WindowAndTier()
        {
            var sink = new CapturingSink();
            var presence = new FakePresence { PresentCount = 1 };
            var counter = new CountingAuthority(NewAuthority());

            RandomEventSpawnChain chain = NewChain(sink, presence, () => { }, counter);
            chain.TrySpawnAtTick(tick: 47L);   // 47 / 20 ⇒ 窗口起点 40

            Assert.AreEqual(1, counter.Calls, "恰掷一次");
            Assert.AreEqual(40L, counter.LastRequest.Win, "win = tick 对齐到窗口起点(47 → 40)");
            Assert.AreEqual(Tier, counter.LastRequest.Tier, "tier = 本链的档");
            Assert.AreEqual(0, counter.LastRequest.Ordinal, "本窗口首次 ⇒ ordinal 0");

            // 同窗口第二次 ⇒ ordinal 递增(否则同窗两次掷骰会掷出同一 key 且流里不可区分)
            chain.TrySpawnAtTick(tick: 48L);
            Assert.AreEqual(1, counter.LastRequest.Ordinal, "同窗口第二次 ⇒ ordinal = 1");

            // 换窗口 ⇒ ordinal 归零
            chain.TrySpawnAtTick(tick: 61L);   // 窗口 60
            Assert.AreEqual(60L, counter.LastRequest.Win);
            Assert.AreEqual(0, counter.LastRequest.Ordinal, "换窗口 ⇒ ordinal 归零");
        }

        // ══════════════════════════════════════════════════════════════════
        // E 生产调用点 —— 驱动真的每 tick 调生成链(卡面「生产调用点存在」)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_director_tickEdge_drivesSpawnChain()
        {
            var sink = new CapturingSink();
            var presence = new FakePresence { PresentCount = 0 };
            RandomEventSpawnChain chain = NewChain(sink, presence, () => { });

            // 玩家**不绑定** —— 生成链半边不应受影响(与出诊启动半边解耦)
            var driver = new RandomEventsDirectorDriver(
                () => new List<SimEvent>(), sink, _encoder, _decoder,
                new ClinicCellSource(), Array.Empty<int>(), chain);

            driver.OnTickEdge(40L);

            Assert.AreEqual(SpawnChainOutcome.Spawned, driver.LastSpawnOutcome,
                "驱动必须真的调到生成链 —— 否则「生产调用点存在」是纸面的");
            Assert.IsNull(driver.LastSpawnError, "本 tick 不应有报错");
            Assert.AreEqual(2, sink.Events.Count, "驱动一次边沿 ⇒ 链写两条");

            // 同 tick 二次边沿 ⇒ 不重复驱动(帧泵语义上不会发生,防御)
            driver.OnTickEdge(40L);
            Assert.AreEqual(2, sink.Events.Count, "同 tick 二次调用不得重复生成");
        }

        [Test]
        public void test_director_overCap_recordsError_withoutKillingFrame()
        {
            var sink = new CapturingSink();
            var presence = new FakePresence { PresentCount = EventStream.PATIENT_APPEARANCE_CAP };
            RandomEventSpawnChain chain = NewChain(sink, presence, () => { });

            var driver = new RandomEventsDirectorDriver(
                () => new List<SimEvent>(), sink, _encoder, _decoder,
                new ClinicCellSource(), Array.Empty<int>(), chain);

            // 边沿上抛 + 不接住 = 杀掉整个 Update 帧 ⇒ 驱动必须接住并留下记录
            LogAssert.ignoreFailingMessages = true;   // 本测**期望**一条 LogError
            Assert.DoesNotThrow(() => driver.OnTickEdge(40L),
                "超界在 tick 边沿上必须被接住(不杀帧)");
            LogAssert.ignoreFailingMessages = false;

            Assert.IsNotNull(driver.LastSpawnError, "接住 ≠ 吞掉:必须在观测面留记录");
            StringAssert.Contains("AC-15", driver.LastSpawnError.Message);
            Assert.AreEqual(0, sink.Events.Count, "被拒 ⇒ 零写入");
        }

        // ══════════════════════════════════════════════════════════════════

        private SeedEventAuthority NewAuthority()
            => new SeedEventAuthority(_ => SyntheticKeys, WorldSeed);

        private RandomEventSpawnChain NewChain(
            IEventSink sink, IPresenceQuery presence, Action onSpawn,
            IEventAuthority authority = null,
            Func<int, IReadOnlyList<int>> keysOfTier = null)
        {
            return new RandomEventSpawnChain(
                sink, _encoder, authority ?? NewAuthority(),
                keysOfTier ?? (_ => SyntheticKeys),
                presence,
                spawnAnchorOf: _ => SyntheticCell,
                spawnPatient: _ => { onSpawn(); return new PatientId(1); },
                // 合成上下文:**出诊路径上、不在医馆、非冷却** —— F1 上下文门的唯一通行组合
                contextOf: w => new F1PipelineInput(w, WindowSize,
                    isNight: false, isOnExpedition: true, isInClinic: false,
                    inCombatCooldown: false, seasonIndex: 0),
                tier: Tier,
                windowSize: WindowSize);
        }

        // ── 测试替身 ─────────────────────────────────────────────────────

        private sealed class CapturingSink : IEventSink
        {
            public readonly List<SimEvent> Events = new List<SimEvent>();
            public void Append(in SimEvent e) => Events.Add(e);
        }

        private sealed class FakePresence : IPresenceQuery
        {
            public int PresentCount { get; set; }
            public bool IsPresent(PatientId patientId) => false;
            public bool IsPresentAt(WorldPos cell) => false;
            public IReadOnlyCollection<int> PresentPatientIds() => Array.Empty<int>();
        }

        private sealed class CountingAuthority : IEventAuthority
        {
            private readonly IEventAuthority _inner;
            public int Calls { get; private set; }
            public RollRequest LastRequest { get; private set; }

            public CountingAuthority(IEventAuthority inner) { _inner = inner; }

            public bool IsAuthority => true;
            public bool IsHost => true;

            public EventRollResult Roll(in RollRequest r)
            {
                Calls++;
                LastRequest = r;
                return _inner.Roll(r);
            }
        }

    }
}
