// 52 运行期消费 —— 四个数据集值的执行体夹具(ADR-039 · V-1…V-6)。
//
// 权威来源:
//   ADR-039 §D1(四值各自的唯一消费面)· §D2(状态从流重构)· §D3(预算/抽数门在掷骰前)
//   GDD `random-events.md` 规则一(`:121` 子区间节奏)· F1 步骤①(`:860` WINDOW_SIZE)·
//   F2(`:979` `clamp(BASE + ΣMod, 0, BUDGET_MAX)`)· 规则六(超预算直接丢弃)
//
// ⚠️ **本文件用合成 key 集 + 合成格驱动**(与 `random_event_spawn_chain_test` 同纪律):
//    生产候选池为空 ⇒ 端到端恒 `NoPool`,照生产数据写夹具会把「没跑」与「跑对没料」
//    混成同一种绿(假绿)。合成 key 让预算 / 抽数 / 节奏三道门**真的可达**。
//
// ⚠️ 当日计数全部**从事件流**读(ADR-039 D2)⇒ 每种「已用额度」的夹具都是
//    **先往流里塞前件事件**,再调 `TrySpawnAtTick` —— 不是改内部计数器。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.RandomEvents
{
    /// <summary>ADR-039:ROLL_INTERVAL / WINDOW_SIZE / BASE / BUDGET_MAX 的运行期消费。</summary>
    public sealed class RandomEventsConsumptionTest
    {
        private const int Tier = 1;
        private const ulong WorldSeed = 0xC0FFEEUL;
        private static readonly int[] SyntheticKeys = { 9001, 9002 };
        private static readonly WorldPos SyntheticCell = new WorldPos(2, 0, 7);

        private const long RollInterval = 20L;
        private const long TicksPerDay = 100L;
        private const int WindowSize = 6;
        private const int BaseEvents = 2;     // 每日预算 = 2
        private const int BudgetMax = 6;

        private CapturingSink _sink;
        private PayloadEncoder _encoder;
        private PayloadDecoder _decoder;

        [SetUp]
        public void SetUp()
        {
            var pool = new InMemoryBlobPool();
            _encoder = new PayloadEncoder(pool);
            _decoder = new PayloadDecoder(pool);
            _sink = new CapturingSink();
        }

        // ══════════════ V-1 子区间节奏 ══════════════

        [Test]
        public void test_notAtBoundary_isNotEvaluated()
        {
            RandomEventSpawnChain chain = NewChain();

            SpawnChainOutcome outcome = chain.TrySpawnAtTick(tick: RollInterval + 1L);

            Assert.That(outcome, Is.EqualTo(SpawnChainOutcome.NotAtBoundary),
                "非 ROLL_INTERVAL 倍数 ⇒ 本 tick 不求值(GDD 规则一「隔多久看一眼」)");
            Assert.That(_sink.Events.Count, Is.EqualTo(0), "未到边界 ⇒ 零写入");
            Assert.That(chain.LastF1Input, Is.Null, "未到边界 ⇒ 不该走到 F1");
        }

        [Test]
        public void test_atBoundary_evaluatesAndSpawns()
        {
            RandomEventSpawnChain chain = NewChain();

            SpawnChainOutcome outcome = chain.TrySpawnAtTick(tick: RollInterval * 2);

            Assert.That(outcome, Is.EqualTo(SpawnChainOutcome.Spawned),
                "边界 + 有池 + 界内 ⇒ 生成(正控:没有它,上面那条可被「一律 NotAtBoundary」满足)");
            Assert.That(_sink.Events.Count, Is.EqualTo(2), "恰两条:先骰后降临");
        }

        // ══════════════ V-2 抽数上限(WINDOW_SIZE)══════════════

        [Test]
        public void test_drawCapReached_stopsDrawing()
        {
            // 同一日内先塞满 WINDOW_SIZE 条 `EventRolled`。
            SeedEvents(dayStart: 0L, rolled: WindowSize, arrived: 0);
            RandomEventSpawnChain chain = NewChain();

            SpawnChainOutcome outcome = chain.TrySpawnAtTick(tick: RollInterval * 2);

            Assert.That(outcome, Is.EqualTo(SpawnChainOutcome.DrawCapReached),
                "当日抽取数达 WINDOW_SIZE ⇒ 不再掷骰(F1 步骤①)");
            Assert.That(_sink.Events.Count, Is.EqualTo(WindowSize),
                "零新增写入(流里只有夹具塞的前件)");
        }

        // ══════════════ V-3 密度预算(BASE / BUDGET_MAX)══════════════

        [Test]
        public void test_overBudget_stopsSpawning()
        {
            // BASE = 2(无 mods)⇒ 当日预算 2;先塞两条 `EventArrived`。
            SeedEvents(dayStart: 0L, rolled: BaseEvents, arrived: BaseEvents);
            RandomEventSpawnChain chain = NewChain();

            SpawnChainOutcome outcome = chain.TrySpawnAtTick(tick: RollInterval * 2);

            Assert.That(outcome, Is.EqualTo(SpawnChainOutcome.OverBudget),
                "当日降临数达 clamp(BASE,∅,MAX) ⇒ 丢弃(规则六「不排队、不延后」)");
            Assert.That(_sink.Events.Count, Is.EqualTo(BaseEvents * 2),
                "零新增写入");
        }

        [Test]
        public void test_budgetMax_clampsBaseDown()
        {
            // BUDGET_MAX = 1 < BASE = 2 ⇒ 预算仍是 1(clamp 上界真的被消费)。
            SeedEvents(dayStart: 0L, rolled: 1, arrived: 1);
            RandomEventSpawnChain chain = NewChain(baseEvents: 2, budgetMax: 1);

            Assert.That(chain.TrySpawnAtTick(tick: RollInterval * 2),
                Is.EqualTo(SpawnChainOutcome.OverBudget),
                "BUDGET_MAX 钳住 BASE ⇒ 1 条之后即预算满");
        }

        [Test]
        public void test_baseZero_meansNoSpawning()
        {
            SeedEvents(dayStart: 0L, rolled: 0, arrived: 0);
            RandomEventSpawnChain chain = NewChain(baseEvents: 0, budgetMax: 6);

            Assert.That(chain.TrySpawnAtTick(tick: RollInterval * 2),
                Is.EqualTo(SpawnChainOutcome.OverBudget),
                "BASE = 0 ⇒ 当日预算 0(F2 下钳 0 的生产侧形态)");
        }

        // ══════════════ V-4 计数从流重构 · 换日归零 ══════════════

        [Test]
        public void test_newDay_resetsCounts()
        {
            SeedEvents(dayStart: 0L, rolled: BaseEvents, arrived: BaseEvents);
            RandomEventSpawnChain chain = NewChain();

            // 前一日已满 ⇒ 拒;换日 ⇒ 计数归零,可再来。
            Assert.That(chain.TrySpawnAtTick(tick: RollInterval * 2),
                Is.EqualTo(SpawnChainOutcome.OverBudget), "前一日满");

            Assert.That(chain.TrySpawnAtTick(tick: TicksPerDay + RollInterval),
                Is.EqualTo(SpawnChainOutcome.Spawned), "新日窗口 ⇒ 计数归零,可再生成");
        }

        // ══════════════ V-5 掷骰窗口 = 预算窗口起点 · ordinal 来自流 ══════════════

        [Test]
        public void test_rollRequest_usesDayWindow_andStreamOrdinal()
        {
            // 当日已有 1 条 EventRolled ⇒ 本次 ordinal 必须是 1(不是内存计数器的 0)。
            SeedEvents(dayStart: 0L, rolled: 1, arrived: 0);
            var counter = new CountingAuthority(new SeedEventAuthority((_, __) => Weighted(SyntheticKeys), WorldSeed));
            RandomEventSpawnChain chain = NewChain(authority: counter);

            chain.TrySpawnAtTick(tick: RollInterval * 2);   // tick 40,日窗口起点 0

            Assert.That(counter.Calls, Is.EqualTo(1), "边界 + 界内 ⇒ 恰掷一次");
            Assert.That(counter.LastRequest.Win, Is.EqualTo(0L),
                "`RollRequest.Win` = **预算窗口起点**(一天),不是 tick 本身(ADR-039 D2)");
            Assert.That(counter.LastRequest.Ordinal, Is.EqualTo(1),
                "ordinal = 当日已掷条数(从流重构),不是内存计数器");
        }

        // ══════════════ V-6 F1 的 WindowSize 取自数据集 ══════════════

        [Test]
        public void test_f1WindowSize_comesFromConfig_notTickWindow()
        {
            RandomEventSpawnChain chain = NewChain(windowSize: 6);

            chain.TrySpawnAtTick(tick: RollInterval * 2);

            Assert.That(chain.LastF1Input.HasValue, Is.True, "走到了 F1");
            Assert.That(chain.LastF1Input.Value.WindowSize, Is.EqualTo(6L),
                "F1 的 WindowSize = 数据集 window_size(条数),**不是** roll_interval(tick 数)");
            Assert.That(chain.LastF1Input.Value.Window, Is.EqualTo(0L),
                "F1 的窗口 = 预算窗口起点(一天),不是 tick 子窗口");
        }

        // ══════════════ V-8 该档 ΣW = 0 ⇒ 跳过本轮,不掷骰不报错 ══════════════

        [Test]
        public void test_zeroWeight_skipsWithoutRollingOrWriting()
        {
            var counter = new CountingAuthority(new SeedEventAuthority((_, __) => Weighted(SyntheticKeys), WorldSeed));
            RandomEventSpawnChain chain = NewChain(authority: counter,
                candidatesOfTier: (_, __) => ZeroWeighted(SyntheticKeys));

            SpawnChainOutcome outcome = chain.TrySpawnAtTick(tick: RollInterval * 2);

            Assert.That(outcome, Is.EqualTo(SpawnChainOutcome.ZeroWeight),
                "ΣW = 0 ⇒ 本档跳过本轮(GDD Edge Cases `:1065`:不掷骰、不报错)");
            Assert.That(counter.Calls, Is.EqualTo(0), "**不掷骰**(否则会留下孤儿 EventRolled)");
            Assert.That(_sink.Events.Count, Is.EqualTo(0), "零写入");
        }

        // ══════════════ 构造期 fail-closed ══════════════

        [Test]
        public void test_constructor_rejectsZeroRollInterval()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => NewChain(rollInterval: 0L), "ROLL_INTERVAL = 0 ⇒ 边界门除零");
        }

        [Test]
        public void test_constructor_rejectsZeroTicksPerDay()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => NewChain(ticksPerDay: 0L), "TICKS_PER_DAY = 0 ⇒ 预算窗口除零");
        }

        [Test]
        public void test_constructor_rejectsZeroWindowSize()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => NewChain(windowSize: 0), "WINDOW_SIZE = 0 ⇒ 永不抽取(功能死亡)");
        }

        // ══════════════ V-7 装配面:provider 缺省 ⇒ 不构造生成链 ══════════════

        [Test]
        public void test_assembly_withoutProvider_doesNotBuildChain()
        {
            using (CompositionRootServices bag = CompositionRoot.Assemble())
            {
                Assert.IsNotNull(bag.RandomEventsDirector,
                    "驱动恒在 —— 「未接线」的形态是**链为 null**,不是驱动缺席(否则两种失败不可分)");
                Assert.IsNull(bag.RandomEventsDirector.SpawnChain,
                    "ADR-039 D5:dataProvider == null ⇒ 不构造生成链,不回落默认配置");
            }
        }

        // ══════════════ 夹具 ══════════════

        private RandomEventSpawnChain NewChain(
            long rollInterval = RollInterval, long ticksPerDay = TicksPerDay,
            int windowSize = WindowSize, int baseEvents = BaseEvents, int budgetMax = BudgetMax,
            IEventAuthority authority = null,
            Func<int, long, IReadOnlyList<WeightedKey>> candidatesOfTier = null)
        {
            return new RandomEventSpawnChain(
                _sink, _encoder,
                authority ?? new SeedEventAuthority((_, __) => Weighted(SyntheticKeys), WorldSeed),
                candidatesOfTier: candidatesOfTier ?? ((_, __) => Weighted(SyntheticKeys)),
                presence: new FakePresence(),
                spawnAnchorOf: _ => SyntheticCell,
                spawnPatient: _ => new PatientId(1),
                contextOf: w => new F1PipelineInput(w, windowSize: 0,
                    isNight: false, isOnExpedition: true, isInClinic: false,
                    inCombatCooldown: false, seasonIndex: 0),
                events: () => _sink.Events,
                tier: Tier,
                config: new RandomEventsDataSet
                {
                    TicksPerDay = ticksPerDay,
                    RollInterval = rollInterval,
                    WindowSize = windowSize,
                    Base = baseEvents,
                    BudgetMax = budgetMax,
                });
        }

        /// <summary>等权候选(权重 1)⇒ 本件全部夹具只测**门**,不测权重分布(那归 `event_weighting_test`)。</summary>
        private static IReadOnlyList<WeightedKey> Weighted(IReadOnlyList<int> keys)
        {
            var list = new List<WeightedKey>(keys.Count);
            for (int i = 0; i < keys.Count; i++) list.Add(new WeightedKey(keys[i], 1));
            return list;
        }

        /// <summary>权重全 0 的候选(测 ΣW = 0 的退化路径)。</summary>
        private static IReadOnlyList<WeightedKey> ZeroWeighted(IReadOnlyList<int> keys)
        {
            var list = new List<WeightedKey>(keys.Count);
            for (int i = 0; i < keys.Count; i++) list.Add(new WeightedKey(keys[i], 0));
            return list;
        }

        /// <summary>往流里塞前件事件(同一天窗口内,按 `RollInterval` 间隔铺开)。</summary>
        private void SeedEvents(long dayStart, int rolled, int arrived)
        {
            for (int i = 0; i < rolled; i++)
                _sink.Events.Add(new SimEvent(dayStart + i, PatientId.None, -1,
                    EventKind.EventRolled, new PayloadRef(0, 0, 0)));
            for (int i = 0; i < arrived; i++)
                _sink.Events.Add(new SimEvent(dayStart + i, PatientId.None, -1,
                    EventKind.EventArrived, new PayloadRef(0, 0, 0)));
        }

        private sealed class CapturingSink : IEventSink
        {
            public readonly List<SimEvent> Events = new List<SimEvent>();
            public void Append(in SimEvent e) => Events.Add(e);
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

        private sealed class FakePresence : IPresenceQuery
        {
            public int PresentCount => 0;
            public bool IsPresent(PatientId patientId) => false;
            public bool IsPresentAt(WorldPos cell) => false;
            public IReadOnlyCollection<int> PresentPatientIds() => Array.Empty<int>();
        }
    }
}
