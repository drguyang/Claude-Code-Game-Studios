// 52 链**端到端一天**的实测夹具(T17 甲 · 2026-11-10)
//
// 权威来源:
//   ADR-039 §D1–§D3(四值的唯一消费面 / 状态从流重构 / 预算门在掷骰前)
//   ADR-038 §D2 / §D4(TICKS_PER_DAY 由 5 的 `$ref` 解析;52 四值 `6/6000/2/6`)
//   GDD `random-events.md` F1 / F2 / 规则六(超预算丢弃)· 规则十二(出诊类 key)
//   ADR-043 §D6 / §D7(解锁:写者去重 + 流折叠查询)· T4 的投放资格(`PatientDrawEligibility`)
//
// ─────────────────────────────────────────────────────────────────────────────
// 本件回答的问题(此前**全库无人回答**)
// ─────────────────────────────────────────────────────────────────────────────
//   「把**生产装配**的那几件拼起来、泵满一天,52 到底产出什么?」
//   此前对这条链的判据全是**分段**的:门各有一条夹具(合成 key),映射有一条夹具(真池),
//   解锁驱动有一条夹具(合成事件)。**没有任何一条**把它们接在一起跑一天 ⇒
//   段与段之间的接缝(谁给 `contextOf` 什么 / `canSpawnPatient` 用的是不是真资格 /
//   解锁驱动排在链之后还是之前)**零覆盖**。
//
//   本件的产出是**具体数字**:几个 `EventRolled` / 几个 `EventArrived` /
//   几个病人 / 解了几门技法 / 卡在哪个 `SpawnChainOutcome`。
//
// ─────────────────────────────────────────────────────────────────────────────
// 读哪些仓库文件(测试标准 §只读外部夹具的具名声明义务)
// ─────────────────────────────────────────────────────────────────────────────
//   读 `unity/Assets/DataCooked/` 下**已提交**的烘焙产物(经 `DataCookedDisk` +
//   生产 codec):`52_random_events` · `52_event_pool` · `5_time` · `disease_registry` ·
//   `disease_action_axis`。
//   为什么必须读真产物而不是内联一份假表:本件要量的是**这一份内容**跑出来的一天。
//   内联一份假池 / 假注册表,测的就只是夹具自己写的谓词(假绿形态 F1),而
//   「生产候选池是不是还是空的」「四条 Event 规则今天到底会不会触发」恰恰只能对真表问。
//   ⚠️ 本文件**零写盘**。
//
// ─────────────────────────────────────────────────────────────────────────────
// 生产件复用了哪些(逐条点名 —— 夹具**不**照抄任何判据)
// ─────────────────────────────────────────────────────────────────────────────
//   `EventWeighting.CandidatesOfTier`(权重的昼夜项)· `SeedEventAuthority`(加权掷骰)
//   `F1ContextEvaluator.Evaluate`(上下文三旗标)· `EventF1Pipeline`(经链)
//   `EventPoolPatientSpawner.{RequiresPatient,CanSpawnPatient,SpawnPatient}`(池→病人三判据)
//   `PatientSpawner.SpawnNext`(9 的 `DiseaseOnset` 写者)· `TechniqueDrawIndex` +
//   `PatientDrawEligibility.Decide`(T4 的投放资格)· `RandomEventSpawnChain` ·
//   `RandomEventsDirectorDriver` · `TechniqueUnlockBootstrap/Writer/Projection/Driver` ·
//   `ClinicCellSource`(未绑定形态 —— 生产里玩家未出生前就是它)。
//
//   ⚠️ **一处夹具自写的组装**:`isDiseaseAdmissible` 这个 λ 在 `CompositionRoot` 里是
//      内联的(没有具名落点),本件按它的**同一份**判据组了一遍(`Decide` + 钩子层**关**)。
//      这不是照抄判据(判据本体在 `PatientDrawEligibility`),但它确实是本件与生产装配的
//      唯一差异点,故**显式登记**:本件**不测钩子病例通道**(`hookAllowed ≡ false` ——
//      那是 T5 的层,另有用例),并断言「本日零 `PatientSpawnUnavailable`」使这处简化
//      对结论**不可观测**。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Presentation;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.Contracts.SkillSystem;
using DaYiJingCheng.Sim.DiseaseSimulation;
using DaYiJingCheng.Sim.World;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.RandomEvents
{
    /// <summary>52 生成链 + 技法解锁的**端到端一天**(生产装配,真产物,真 tick 泵)。</summary>
    [TestFixture]
    public sealed class RandomEventsEndToEndDayTest
    {
        /// <summary>机会档(急召出诊属此档,GDD `:400`;与生产 `CompositionRoot` 的 `tier: 1` 同值)。</summary>
        private const int OpportunityTier = 1;

        private static readonly WorldPos AnchorCell = new WorldPos(3, 0, 5);

        /// <summary>生产世界的日长(`5_time.json` 的 `ticks_per_day`)。</summary>
        /// <remarks>⚠️ 这是**数据值**,不是判据 —— 本件从产物读它,不在这里另写一份当真理;
        /// 下面的 `Assert` 只用来给「本日边界数」一个独立出处(见 `Range`)。</remarks>
        private const long TicksPerDay = 36000L;

        // ══════════════════════════════════════════════════════════════════════
        //  主测:一整天下来,链与解锁究竟产出了什么
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 世界种子 `0xA11CE`、真池、真注册表、真投放资格 —— 泵满第一天。
        /// </summary>
        /// <remarks>
        /// <para><b>为什么先钉这一段序</b>:<c>0xA11CE</c> 的当日抽取序(离线复算,见证据件)
        /// = 2001(机会遭遇)→ 2004(溃兵军医 / 心衰)。第一条不产病人、第二条产一个,
        /// 于是「预算 2」在第二个边界就被用尽 ⇒ 后四个边界全是 `OverBudget`。
        /// 这一段序**同时**覆盖了:遭遇支(无病人)、医疗支(有病人)、预算门的终止行为。</para>
        /// <para>⚠️ 种子是**夹具输入**,不是判据;换种子就必须回来重算数字(见断言旁的复算说明)。</para>
        /// </remarks>
        [Test]
        public void test_realData_oneDay_producesBudgetThenClosesTheGate()
        {
            Harness h = Harness.Build(worldSeed: 0xA11CEUL, hookAllowed: false);

            // ── 前置(反空洞正控):真池在这一档真的有候选,否则下面全是「没跑」 ──
            Assert.That(h.Pool.Entries.Count, Is.GreaterThan(0), "前置:池表须非空");
            Assert.That(h.OpportunityCandidateCount, Is.EqualTo(4),
                "机会档候选 = 2001 / 2002 / 2003 / 2004 —— 变更时须复核本件的期望序");

            DayResult day = h.RunOneDay();

            // ── ① 边界数 = 日长 / 子区间(两个值都来自真产物) ──
            Assert.That(day.Boundaries.Count, Is.EqualTo(6),
                $"一天应当只看 {TicksPerDay} / {h.Config.RollInterval} = 6 眼;" +
                "边界数不对 ⇒ `ROLL_INTERVAL` 或 `TICKS_PER_DAY` 没被真读到");
            Assert.That(day.Ticks, Is.EqualTo(new long[] { 0, 6000, 12000, 18000, 24000, 30000 }),
                "边界落在 `tick % ROLL_INTERVAL == 0` 上(ADR-039 §D1);tick 也钉住 `ROLL_INTERVAL` 的值");

            // ── ② 结果序:两次降临后,预算门把余下的边界全关掉 ──
            Assert.That(day.OutcomeSequence, Is.EqualTo(new[]
                {
                    nameof(SpawnChainOutcome.Spawned),
                    nameof(SpawnChainOutcome.Spawned),
                    nameof(SpawnChainOutcome.OverBudget),
                    nameof(SpawnChainOutcome.OverBudget),
                    nameof(SpawnChainOutcome.OverBudget),
                    nameof(SpawnChainOutcome.OverBudget),
                }),
                "预算 = clamp(BASE=2, 0, BUDGET_MAX=6) = 2 ⇒ 两次降临之后每个边界都应 OverBudget");

            Assert.That(day.OutcomeSequence, Has.No.Member(nameof(SpawnChainOutcome.PatientSpawnUnavailable)),
                "本日**零**投放被拒 ⇒ 本件关掉钩子通道(见文件头)对结论不可观测,简化是惰性的");

            // ── ③ 具体数字(这才是「实测」的意思) ──
            Assert.That(h.Count(EventKind.EventRolled), Is.EqualTo(2), "两次掷骰(每次降临恰一条)");
            Assert.That(h.Count(EventKind.EventArrived), Is.EqualTo(2), "两次降临");
            Assert.That(h.Count(EventKind.DiseaseOnset), Is.EqualTo(1),
                "两条降临里只有 2004 是医疗条目 ⇒ 恰一个病人(2001 是遭遇支,不产病人)");

            // ── ④ 病人真的落到在场视图里(否则链报 Spawned 而世界无变化) ──
            Assert.That(h.Presence.Added.Count, Is.EqualTo(1),
                "`presence.AddPresent` 未调用 ⇒ 13 看不见、9 不 Step(链「成功」而世界空的经典形态)");

            // ── ⑤ 解锁:起步集 2 门 + 链上学到的 1 门 ──
            List<TechniqueUnlockedPayload> unlocks = h.Unlocks();
            var unlockedIds = new List<int>();
            for (int i = 0; i < unlocks.Count; i++) unlockedIds.Add(unlocks[i].TechniqueId);

            Assert.That(unlockedIds, Is.EquivalentTo(new[] { 0, 1, 11 }),
                "0/1 = 起步集(序章手册);11 = 毛地黄 —— 由 `EventArrived(2004)` 那条规则解锁");

            // ── ⑥ 「是哪条规则解锁的」可归因:sourceId 必须是被命中的事件 key ──
            TechniqueUnlockedPayload digitalis = unlocks.Find(u => u.TechniqueId == 11);
            Assert.That(digitalis.SourceId, Is.EqualTo(2004),
                "毛地黄须由「溃兵军医」(2004) 解锁 —— 别的来源值说明接错规则了");
            Assert.That(unlocks.FindAll(u => u.TechniqueId == 11).Count, Is.EqualTo(1),
                "同一条 (actor, technique) 至多一条(ADR-043 §D6:写者去重)");

            // ── ⑦ 边沿序可判别:解锁写在**同一 tick** 上(链在前、解锁驱动在后)──
            Assert.That(h.UnlockTickOf(11), Is.EqualTo(6000L),
                "`EventArrived(2004)` 落在 6000;解锁若晚一个 tick 说明边沿序反了 —— " +
                "把顺序调过来,计数**一条都不会变**,只有本行看得见");
        }

        // ══════════════════════════════════════════════════════════════════════
        //  第二测:首个抽取被投放门拒掉 ⇒ **整日**零产出(当下行为,不是设计意图)
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 世界种子 `0x5EED`:当日首次抽取 = 2002(打摆子 / 间日疟),而疟疾只认技法 12
        /// (金鸡纳,未解锁)⇒ 投放门拒 ⇒ 掷骰不落流 ⇒ **序号不前进** ⇒ 后五个边界
        /// 抽到**同一个 key**、同样被拒。
        /// </summary>
        /// <remarks>
        /// <para>⚠️ <b>本测钉的是当下行为,不是设计意图。</b>它成立的两个前提各自都「对」:
        /// ① ADR-039 §D3 不许「先落 `EventRolled` 再被后半截拒绝」(孤儿行);
        /// ② `Ordinal` = 当日**已掷**数(从流重构)。两者相乘的后果是:被拒的那一眼
        /// **不消耗掷骰序**,于是同一天里每一眼都落在同一个 `(win, tier, ordinal)` 上
        /// ⇒ 同一个 key。跨日无此问题(`win = dayStart` 变)。
        /// 若判定这是缺陷,该改的是链的序号口径 —— 本测会**当场转红**,正好当作判据。</para>
        /// <para>种子的选取**不是**为了挑一个好看的结论:疟疾是池里两条「技法全锁」的
        /// 医疗条目之一(另一条是 3001 麻疹,它不在机会档),所以「首抽即锁」是一个
        /// 真实可达的日型,不构造它等于对这一整类日子零覆盖。</para>
        /// <para>⚠️ <b>本测关掉了钩子通道</b>(`hookAllowed: false`)——那是**章内配额已用完**
        /// 的生产形态。配额还在时生产会把它改判成钩子病例、当天照常出病人,见
        /// <see cref="test_realData_withHookQuotaAvailable_theSameDayYieldsTwoPatients"/>。
        /// 两条一起读才是完整的:「死日」不是链坏了,是**配额耗尽后**这个日型的真实后果。</para>
        /// </remarks>
        [Test]
        public void test_realData_whenFirstDrawIsLocked_theWholeDayIsDead()
        {
            Harness h = Harness.Build(worldSeed: 0x5EEDUL, hookAllowed: false);

            Assert.That(h.OpportunityCandidateCount, Is.EqualTo(4), "前置:同主测");

            DayResult day = h.RunOneDay();

            Assert.That(day.OutcomeSequence, Is.EqualTo(new[]
                {
                    nameof(SpawnChainOutcome.PatientSpawnUnavailable),
                    nameof(SpawnChainOutcome.PatientSpawnUnavailable),
                    nameof(SpawnChainOutcome.PatientSpawnUnavailable),
                    nameof(SpawnChainOutcome.PatientSpawnUnavailable),
                    nameof(SpawnChainOutcome.PatientSpawnUnavailable),
                    nameof(SpawnChainOutcome.PatientSpawnUnavailable),
                }),
                "首次抽中「技法全锁」的医疗条目 ⇒ 每一眼都因同一个 (win,tier,ordinal) 抽到同一个 key 而被拒");

            Assert.That(h.Count(EventKind.EventRolled), Is.EqualTo(0), "被拒的一眼零写入(ADR-039 §D3:防孤儿行)");
            Assert.That(h.Count(EventKind.EventArrived), Is.EqualTo(0));
            Assert.That(h.Count(EventKind.DiseaseOnset), Is.EqualTo(0));
            Assert.That(h.Presence.Added, Is.Empty);

            // 解锁只剩起步集 —— 证明「零降临」不是「解锁驱动没接上」:
            // 同一条驱动在主测里写出了第三条,这里不写是因为**没有来源事件**。
            var ids = new List<int>();
            foreach (TechniqueUnlockedPayload u in h.Unlocks()) ids.Add(u.TechniqueId);
            Assert.That(ids, Is.EquivalentTo(new[] { 0, 1 }),
                "本日无 `EventArrived` ⇒ 三条 Event 源规则全不命中(解锁驱动照常每 tick 跑)");
        }

        // ══════════════════════════════════════════════════════════════════════
        //  第三测:同一天 + 钩子配额还在(= 生产形态)⇒ 死日变活日
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 种子 `0x5EED` 不变、只把钩子通道打开:2002(疟疾)被改判为**钩子病例**放行 ——
        /// 病人照常出现,而「降临了一个疟疾病人」又解锁了金鸡纳。
        /// </summary>
        /// <remarks>
        /// <para>这条钉的是**生产语义**,不是另一份夹具:钩子放行的判据本体在
        /// `PatientDrawEligibility.Decide` + `HookCasePolicy.AllowsInjection`,本件只是把
        /// 「配额还在」这个**状态**用常量 `hookAllowed: true` 表达出来(真正的配额计数在
        /// `HookCaseLedger`,它的准入口径另有夹具)。</para>
        /// <para>与第二测对照读:同一个世界种子、同一份数据,**唯一变量是配额**——
        /// 配额在 ⇒ 两个病人 + 学会金鸡纳;配额用完 ⇒ 整日零产出。
        /// 这正是 hook 规则 1「每章 1–3 例」在流层的可观测形态。</para>
        /// </remarks>
        [Test]
        public void test_realData_withHookQuotaAvailable_theSameDayYieldsTwoPatients()
        {
            Harness h = Harness.Build(worldSeed: 0x5EEDUL, hookAllowed: true);

            DayResult day = h.RunOneDay();

            Assert.That(day.OutcomeSequence, Is.EqualTo(new[]
                {
                    nameof(SpawnChainOutcome.Spawned),
                    nameof(SpawnChainOutcome.Spawned),
                    nameof(SpawnChainOutcome.OverBudget),
                    nameof(SpawnChainOutcome.OverBudget),
                    nameof(SpawnChainOutcome.OverBudget),
                    nameof(SpawnChainOutcome.OverBudget),
                }),
                "钩子放行 ⇒ 首眼不再被投放门挡;第二眼抽中 2003(惊厥)⇒ 预算用尽后余四眼 OverBudget");

            Assert.That(h.Count(EventKind.EventArrived), Is.EqualTo(2));
            Assert.That(h.Count(EventKind.DiseaseOnset), Is.EqualTo(2), "两条都是医疗条目 ⇒ 两个病人");
            Assert.That(h.Presence.Added.Count, Is.EqualTo(2));

            var ids = new List<int>();
            foreach (TechniqueUnlockedPayload u in h.Unlocks()) ids.Add(u.TechniqueId);
            Assert.That(ids, Is.EquivalentTo(new[] { 0, 1, 12 }),
                "0/1 = 起步集;12 = 金鸡纳,由 `EventArrived(2002)` 解锁。" +
                "2003 那条规则指向技法 1(已在起步集)⇒ 写者去重后不新增");
        }

        // ══════════════════════════════════════════════════════════════════════
        //  装配 + 泵
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>一天的观测结果(边界 → 结果码,按 tick 升序)。</summary>
        private readonly struct DayResult
        {
            public readonly IReadOnlyList<(long Tick, SpawnChainOutcome Outcome)> Boundaries;

            public DayResult(IReadOnlyList<(long Tick, SpawnChainOutcome)> boundaries)
                => Boundaries = boundaries;

            /// <summary>结果码名(顺序与 `Boundaries` 一致)—— 便于断言写成可读的名字而非整数。</summary>
            public string[] OutcomeSequence
            {
                get
                {
                    var names = new string[Boundaries.Count];
                    for (int i = 0; i < Boundaries.Count; i++) names[i] = Boundaries[i].Outcome.ToString();
                    return names;
                }
            }

            /// <summary>边界所在的 tick(升序)。</summary>
            public long[] Ticks
            {
                get
                {
                    var ticks = new long[Boundaries.Count];
                    for (int i = 0; i < Boundaries.Count; i++) ticks[i] = Boundaries[i].Tick;
                    return ticks;
                }
            }
        }

        /// <summary>
        /// 生产件的装配面(与本文件头「复用了哪些」逐条对应)。
        /// </summary>
        private sealed class Harness
        {
            public readonly EventPoolDataSet Pool;
            public readonly RandomEventsDataSet Config;
            public readonly Sim.TimeParams Time;
            public readonly EventStream Stream;
            public readonly PayloadDecoder Decoder;
            public readonly RecordingPresence Presence;
            public readonly PlayerActorBinding PlayerActor;
            public readonly int PlayerActorId;
            public readonly RandomEventsDirectorDriver Director;
            public readonly TechniqueUnlockDriver UnlockDriver;
            public readonly int OpportunityCandidateCount;

            private Harness(EventPoolDataSet pool, RandomEventsDataSet config, Sim.TimeParams time,
                            EventStream stream, PayloadDecoder decoder, RecordingPresence presence,
                            RandomEventsDirectorDriver director, TechniqueUnlockDriver unlockDriver,
                            PlayerActorBinding playerActor, int playerActorId, int opportunityCandidateCount)
            {
                Pool = pool;
                Config = config;
                Time = time;
                Stream = stream;
                Decoder = decoder;
                Presence = presence;
                Director = director;
                UnlockDriver = unlockDriver;
                PlayerActor = playerActor;
                PlayerActorId = playerActorId;
                OpportunityCandidateCount = opportunityCandidateCount;
            }

            /// <summary>按生产装配的形状拼一台(见文件头「复用了哪些」)。</summary>
            public static Harness Build(ulong worldSeed, bool hookAllowed)
            {
                // ── ① 真产物 ──
                var provider = new RealTablesProvider();
                RandomEventsDataSet config = provider.Load<RandomEventsDataSet>();
                EventPoolDataSet pool = provider.Load<EventPoolDataSet>();
                Sim.TimeParams time = BootRoot.ResolveTimeParams(provider);   // 生产解析(含 Validate)
                DiseaseRegistryDataSet registry = DataCookedDisk.LoadDiseaseRegistry();
                DiseaseActionAxisDataSet axis = DiseaseActionAxisCookedCodec.Read(
                    DataCookedDisk.ReadAll(AddressablesDataProvider.DiseaseActionAxisAddress));

                var registryById = new Dictionary<int, DiseaseRegistryEntry>();
                for (int i = 0; i < registry.Rows.Count; i++) registryById[registry.Rows[i].DiseaseId] = registry.Rows[i];

                var knownTechniqueIds = new HashSet<int>();
                for (int i = 0; i < axis.Actions.Count; i++) knownTechniqueIds.Add(axis.Actions[i].ActionId);
                var drawIndex = new TechniqueDrawIndex(axis.TreatableBy);

                // ── ② 流的四件套(在场视图 = 链与 EventStream **同一个实例**)──
                var ids = new IdAuthority();
                // 玩家先占一个号(玩家与病人共用 id 空间,ADR-016 §二)—— 生产同样是先铸玩家。
                int playerActorId = ids.NextPatientId().Value;

                // ⚠️ `RecordingPresence` 必须是**同一个对象**同时给 `EventStream`(AC-15)与
                //    生成链(CAP 预检):分两个实例 = 链看见 0 在场、流看见 N 在场。
                var presence = new RecordingPresence();

                var blobPool = new InMemoryBlobPool();
                var encoder = new PayloadEncoder(blobPool);
                var decoder = new PayloadDecoder(blobPool);
                var stream = new EventStream(ids, presence);

                var playerActor = new PlayerActorBinding();

                var projection = new TechniqueUnlockProjection(() => stream.Events, decoder);
                var unlockWriter = new TechniqueUnlockWriter(stream, encoder, projection, knownTechniqueIds);

                // ── ③ 投放资格(T4)—— 与 `CompositionRoot` 同一份判据 ──
                Func<int, bool> isTechniqueUnlocked = techniqueId
                    => projection.IsUnlocked(playerActorId, techniqueId);

                Func<DiseaseRegistryEntry, PatientDrawDecision> admitEntry = row =>
                    PatientDrawEligibility.Decide(
                        row, drawIndex, playerPrescriptionLevel: 0, isTechniqueUnlocked,
                        playerBound: true, asHookCase: false, hookAllowed: hookAllowed);

                Func<int, bool> isDiseaseAdmissible = diseaseId =>
                    !registryById.TryGetValue(diseaseId, out DiseaseRegistryEntry candidate)
                    || admitEntry(candidate).Admitted;

                // ── ④ 52 链(候选集 / 上下文 / 病人三条缝,全部走生产件)──
                Func<int, long, IReadOnlyList<WeightedKey>> candidatesOfTier = (tier, tick) =>
                    EventWeighting.CandidatesOfTier(pool, tier, isNight: TimeBase.IsNight(tick, time));

                int[] houseCallKeys = pool.HouseCallKeys();
                var clinicCells = new ClinicCellSource();   // 未绑定形态 ⇒ 恒 false(生产里玩家未出生前即此)
                var spawner = new PatientSpawner(ids, stream, encoder, worldSeed);

                var chain = new RandomEventSpawnChain(
                    stream, encoder,
                    new SeedEventAuthority(candidatesOfTier, worldSeed),
                    candidatesOfTier: candidatesOfTier,
                    presence: presence,
                    spawnAnchorOf: _ => AnchorCell,
                    spawnPatient: EventPoolPatientSpawner.SpawnPatient(
                        pool, spawner.SpawnNext, presence, _ => AnchorCell),
                    contextOf: tick => F1ContextEvaluator.Evaluate(
                        tick, stream.Events, decoder, clinicCells,
                        playerActor.IsBound ? playerActor.ActorId : F1ContextEvaluator.NoPlayer,
                        houseCallKeys,
                        isNight: TimeBase.IsNight(tick, time)),
                    events: () => stream.Events,
                    tier: OpportunityTier,
                    config: config,
                    requiresPatient: EventPoolPatientSpawner.RequiresPatient(pool),
                    canSpawnPatient: EventPoolPatientSpawner.CanSpawnPatient(pool, isDiseaseAdmissible));

                var director = new RandomEventsDirectorDriver(
                    () => stream.Events, stream, encoder, decoder, clinicCells,
                    houseCallKeys, chain, playerActor);
                director.BindPlayer(playerActorId);   // 与生产同序:装配后置绑定(玩家 id 原则上一刻才铸造)

                // ── ⑤ 起步集 + 解锁驱动(生产 BootRoot 4.4a / 4.4b 的同序)──
                TechniqueUnlockBootstrap.Ensure(stream.Events, unlockWriter, playerActorId, tick: 0);
                var unlockDriver = new TechniqueUnlockDriver(
                    stream, decoder, TechniqueUnlockRules.Default, unlockWriter, playerActorId);

                int opportunityCandidates = candidatesOfTier(OpportunityTier, 0L).Count;

                return new Harness(pool, config, time, stream, decoder, presence,
                                   director, unlockDriver, playerActor, playerActorId, opportunityCandidates);
            }

            /// <summary>
            /// 泵满一天:**逐 tick** 驱动(与 `BootRoot.Update` 的边沿序列同粒度),
            /// 链只在 `tick % ROLL_INTERVAL == 0` 时真正求值(由链自己判)。
            /// </summary>
            public DayResult RunOneDay()
            {
                var boundaries = new List<(long, SpawnChainOutcome)>();
                for (long tick = 0L; tick < TicksPerDay; tick++)
                {
                    Director.OnTickEdge(tick);
                    if (Director.LastSpawnOutcome.HasValue
                        && Director.LastSpawnOutcome.Value != SpawnChainOutcome.NotAtBoundary)
                    {
                        boundaries.Add((tick, Director.LastSpawnOutcome.Value));
                    }

                    // 解锁扫描排在链**之后** —— 生产 `BootRoot` 的边沿序里 52 在第 4.4 段前,
                    // 30 的驱动在同一边沿后续;本处同一序:本 tick 刚写的 `EventArrived`
                    // 在**同一个边沿**就能被扫到(否则解锁会晚一整个 tick)。
                    UnlockDriver.OnTickEdge(tick);
                }
                return new DayResult(boundaries);
            }

            public int Count(EventKind kind)
            {
                int n = 0;
                IReadOnlyList<SimEvent> events = Stream.Events;
                for (int i = 0; i < events.Count; i++) if (events[i].Kind == kind) n++;
                return n;
            }

            public List<TechniqueUnlockedPayload> Unlocks()
            {
                var rows = new List<TechniqueUnlockedPayload>();
                IReadOnlyList<SimEvent> events = Stream.Events;
                for (int i = 0; i < events.Count; i++)
                {
                    if (events[i].Kind != EventKind.TechniqueUnlocked) continue;
                    rows.Add(Decoder.Decode<TechniqueUnlockedPayload>(
                        EventKind.TechniqueUnlocked, events[i].Payload));
                }
                return rows;
            }

            /// <summary>某技法被解锁时所在的 tick;未解锁 ⇒ <c>-1</c>。</summary>
            public long UnlockTickOf(int techniqueId)
            {
                IReadOnlyList<SimEvent> events = Stream.Events;
                for (int i = events.Count - 1; i >= 0; i--)
                {
                    if (events[i].Kind != EventKind.TechniqueUnlocked) continue;
                    TechniqueUnlockedPayload p = Decoder.Decode<TechniqueUnlockedPayload>(
                        EventKind.TechniqueUnlocked, events[i].Payload);
                    if (p.TechniqueId == techniqueId) return events[i].Tick;
                }
                return -1L;
            }
        }

        /// <summary>只装本件用到的三张表;问到别的 ⇒ 抛(接线错的信号,与既有夹具同款)。</summary>
        private sealed class RealTablesProvider : IDataProvider
        {
            public TDataSet Load<TDataSet>() where TDataSet : struct
            {
                if (typeof(TDataSet) == typeof(RandomEventsDataSet))
                    return (TDataSet)(object)DataCookedDisk.LoadRandomEvents();
                if (typeof(TDataSet) == typeof(EventPoolDataSet))
                    return (TDataSet)(object)DataCookedDisk.LoadEventPool();
                if (typeof(TDataSet) == typeof(TimeParamsDataSet))
                    return (TDataSet)(object)DataCookedDisk.LoadTimeParams();

                throw new InvalidOperationException(
                    $"RealTablesProvider 无 {typeof(TDataSet).Name} —— 本件不该问到它(真问 = 接线错)");
            }
        }

        /// <summary>真·在场写面 + 只读视图(链与 `EventStream` **共用同一个实例**)。</summary>
        private sealed class RecordingPresence : IPresenceQuery, IPresenceWriter
        {
            public readonly List<PatientId> Added = new List<PatientId>();
            private readonly HashSet<int> _ids = new HashSet<int>();

            public int PresentCount => _ids.Count;
            public bool IsPresent(PatientId patientId) => _ids.Contains(patientId.Value);
            public bool IsPresentAt(WorldPos cell) => false;
            public IReadOnlyCollection<int> PresentPatientIds() => new List<int>(_ids);

            public void AddPresent(PatientId patientId, WorldPos cell)
            {
                Added.Add(patientId);
                _ids.Add(patientId.Value);
            }

            public void RemovePresent(PatientId patientId) => _ids.Remove(patientId.Value);
            public void MovePresent(PatientId patientId, WorldPos toCell) { }
        }
    }
}
