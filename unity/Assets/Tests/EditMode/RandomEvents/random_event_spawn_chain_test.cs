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
        // ── ADR-039 运行期消费的合成配置(量纲分开:节奏是 tick,抽数是条数)──
        private const long RollInterval = 20L;   // 边界门
        private const long TicksPerDay = 100L;   // 预算窗口
        private const int WindowSize = 20;       // 每日抽取条数上限 / F1 配额总数
        private const int BaseEvents = 6;        // 每日基础事件数
        private const int BudgetMax = 6;         // 每日硬上限
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
                candidatesOfTier: (_, __) => Array.Empty<WeightedKey>());

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
            chain.TrySpawnAtTick(tick: 40L);   // 20 的倍数 ⇒ 子区间边界;日窗口起点 0

            Assert.AreEqual(1, counter.Calls, "恰掷一次");
            Assert.AreEqual(0L, counter.LastRequest.Win,
                "win = **预算窗口起点**(一天)· ADR-039 D2;不是 tick 子窗口");
            Assert.AreEqual(Tier, counter.LastRequest.Tier, "tier = 本链的档");
            Assert.AreEqual(0, counter.LastRequest.Ordinal, "当日首次 ⇒ ordinal 0");

            // 同一日再一个边界 ⇒ ordinal = 当日已掷数(从流计数,读档可重建)
            chain.TrySpawnAtTick(tick: 60L);
            Assert.AreEqual(1, counter.LastRequest.Ordinal, "同日第二次 ⇒ ordinal = 1(流里已有 1 条 EventRolled)");

            // 换日 ⇒ 计数归零、窗口 = 新日起点
            chain.TrySpawnAtTick(tick: 120L);   // 日窗口起点 100
            Assert.AreEqual(100L, counter.LastRequest.Win);
            Assert.AreEqual(0, counter.LastRequest.Ordinal, "换日 ⇒ ordinal 归零");
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
            => new SeedEventAuthority((_, __) => Weighted(SyntheticKeys), WorldSeed);

        /// <summary>等权候选(权重 1)—— 本件测链路与门,不测权重分布(那归 `event_weighting_test`)。</summary>
        private static IReadOnlyList<WeightedKey> Weighted(IReadOnlyList<int> keys)
        {
            var list = new List<WeightedKey>(keys.Count);
            for (int i = 0; i < keys.Count; i++) list.Add(new WeightedKey(keys[i], 1));
            return list;
        }

        private RandomEventSpawnChain NewChain(
            CapturingSink sink, IPresenceQuery presence, Action onSpawn,
            IEventAuthority authority = null,
            Func<int, long, IReadOnlyList<WeightedKey>> candidatesOfTier = null,
            int windowSize = WindowSize,
            Func<int, bool> requiresPatient = null,
            Func<long, bool> canSpawnPatient = null)
        {
            return new RandomEventSpawnChain(
                sink, _encoder, authority ?? NewAuthority(),
                candidatesOfTier ?? new Func<int, long, IReadOnlyList<WeightedKey>>((_, __) => Weighted(SyntheticKeys)),
                presence,
                spawnAnchorOf: _ => SyntheticCell,
                spawnPatient: _ => { onSpawn(); return new PatientId(1); },
                // 合成上下文:**出诊路径上、不在医馆、非冷却** —— F1 上下文门的唯一通行组合
                // (WindowSize 由链按 config 覆盖,这里传 0)
                contextOf: w => new F1PipelineInput(w, windowSize: 0,
                    isNight: false, isOnExpedition: true, isInClinic: false,
                    inCombatCooldown: false, seasonIndex: 0),
                events: () => sink.Events,
                tier: Tier,
                config: new RandomEventsDataSet
                {
                    TicksPerDay = TicksPerDay,
                    RollInterval = RollInterval,
                    WindowSize = windowSize,
                    Base = BaseEvents,
                    BudgetMax = BudgetMax,
                },
                requiresPatient: requiresPatient,
                canSpawnPatient: canSpawnPatient);
        }

        // ══════════════════════════════════════════════════════════════════
        // E 病人通道(`TD-randomevents-05` 同批 · 2026-11-05)
        //
        // 为什么要这两条:上下文门改成**档级**之后,链路真的能走到 ⑦ 之后了。
        // 而旧序是「先 Append 两条、再在 `spawnPatient` 抛」⇒ 流里留孤儿行。
        // 这两条把「先判后写」与「遭遇条目不该产病人」两面各钉一处。
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_medicalEntry_patientUnavailable_writesNothing()
        {
            var sink = new CapturingSink();
            var presence = new FakePresence { PresentCount = 0 };
            int spawnCalls = 0;

            RandomEventSpawnChain chain = NewChain(
                sink, presence, () => spawnCalls++,
                requiresPatient: _ => true,          // 抽中的条目需要病人
                canSpawnPatient: _ => false);        // 而病人通道今日不可用

            SpawnChainOutcome outcome = chain.TrySpawnAtTick(tick: 40L);

            Assert.AreEqual(SpawnChainOutcome.PatientSpawnUnavailable, outcome,
                "需要病人而通道不可用 ⇒ 具名结果码(不是抛、不是静默 Spawned)");
            // 副作用判据:只断结果码会被「写了两条再抛」冒充 —— 必须断**流零写入**。
            Assert.AreEqual(0, sink.Events.Count,
                $"必须**零写入** —— 改前是先落 `EventRolled`+`EventArrived` 再抛," +
                $"流里会留下「掷过骰、降临了、没人」的孤儿行(实际写了 {sink.Events.Count} 条)");
            Assert.AreEqual(0, spawnCalls, "零病人创建");
        }

        [Test]
        public void test_encounterEntry_doesNotNeedPatient_stillSpawns()
        {
            var sink = new CapturingSink();
            var presence = new FakePresence { PresentCount = 0 };
            int spawnCalls = 0;

            RandomEventSpawnChain chain = NewChain(
                sink, presence, () => spawnCalls++,
                requiresPatient: _ => false,         // 遭遇条目(27):不产病人
                canSpawnPatient: _ => false);        // 病人通道不可用也不影响它

            SpawnChainOutcome outcome = chain.TrySpawnAtTick(tick: 40L);

            Assert.AreEqual(SpawnChainOutcome.Spawned, outcome,
                "遭遇条目不需要病人 ⇒ 病人通道不可用**不得**拖死它");
            Assert.AreEqual(2, sink.Events.Count, "仍恰两条(先骰后降临)");
            Assert.AreEqual(0, spawnCalls, "遭遇条目不得调用病人创建");
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
