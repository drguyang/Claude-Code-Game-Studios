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
//   ⚠️ **T17 时本件有一处夹具自写的组装,已于 T18 乙收掉**:`isDiseaseAdmissible` 那段
//      在 `CompositionRoot` 里原是内联 λ(无落点可调),夹具只能自己组一遍并把钩子配额
//      用常量代替。T18 把它收成 `PatientAdmissionPolicy` ⇒ 本件现在**调的就是生产类**,
//      钩子配额也走真账本(`HookCaseLedger`),不再有常量替身。
//
//   ⚠️ **T18 丙**:投放资格**前移到候选集**(`EventWeighting.CandidatesOfTier` 的 `keyFilter`)。
//      ⇒ 本件的三组期望值与 T17 时**不同**:T17 的「首抽被拒 ⇒ 整日零产出」在结构上消失了,
//      第二测因此改成「配额用尽 ⇒ 该条目退池、日照常产出」。

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
            Harness h = Harness.Build(worldSeed: 0xA11CEUL);

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
        /// 钩子配额**已用掉**(流里先有一例钩子病例)⇒ 技法全锁的 2002 **不进抽池** ——
        /// 同一天照常产出两位病人,不再是「整日零产出」。
        /// </summary>
        /// <remarks>
        /// <para>这条钉的是 **T18 丙**的判据。改前(资格只在**抽中之后**判)的失败形态是:
        /// 被拒的那一眼不写事件(ADR-039 §D3 防孤儿行)⇒ `Ordinal` 不前进 ⇒ 当天每一眼都落在
        /// 同一个 `(win, tier, ordinal)` 上 ⇒ 抽到同一个 key、同样被拒 ⇒ **整日零产出**
        /// (T17 实测:种子 `0x5EED` 的 6 眼全是 `PatientSpawnUnavailable`)。
        /// 资格前移到候选集之后,该条目**根本不进抽池**,抽取序在剩下的条目上正常推进。</para>
        /// <para>与第三测对照读:同一世界种子、同一份数据,**唯一变量是章内钩子配额** ——
        /// 配额在 ⇒ 2002 以钩子病例身份进池(4 条候选);配额用完 ⇒ 它退池(3 条候选)。
        /// ⇒ 「死日」这个形态**在结构上消失**,而不是被别的机制掩盖掉。</para>
        /// <para>⚠️ 「配额已用掉」用生产写者造(`PatientSpawner.SpawnNext` 写一条病种 7 的
        /// `DiseaseOnset`):账本按「onset 那一刻该病种一门技法都没解锁」自然判它是一例钩子。
        /// **不伪造事件、不直接改账本计数**。</para>
        /// <para>⚠️ 本测换种子(`0xA`,不是第二测的 `0x5EED`),原因是**预置病例必须落在
        /// tick 0**,而若当日**第一眼**抽中的是出诊类条目(2003 / 2004 都是 `is_house_call`),
        /// 它的 `EventArrived` 也落在 tick 0 ⇒ 该 tick 有两条 `DiseaseOnset` ⇒
        /// `ActiveHouseCallQuery` 的「同 tick 恰一病人」判据**响亮抛**(规则十二 12.3)。
        /// 实测踩到过。⇒ 选一个首眼抽中 2001(遭遇条,不产病人)的种子。
        /// </para>
        /// </remarks>
        [Test]
        public void test_realData_whenHookQuotaIsSpent_theLockedKeyLeavesThePool()
        {
            Harness h = Harness.Build(worldSeed: 0xAUL, preSeedHookCase: true);

            Assert.That(h.OpportunityCandidateCount, Is.EqualTo(3),
                "**这就是丙的判据**:2002 因投放资格不过而退池 ⇒ 机会档只剩 2001 / 2003 / 2004");

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
                "候选集里没有不可产出的条目 ⇒ 前两眼都能产;预算 2 用尽后余四眼 OverBudget");

            Assert.That(day.OutcomeSequence, Has.No.Member(nameof(SpawnChainOutcome.PatientSpawnUnavailable)),
                "前移之后 `PatientSpawnUnavailable` 只是防御分支 —— 生产路径上不该再出现");

            Assert.That(h.Count(EventKind.EventRolled), Is.EqualTo(2));
            Assert.That(h.Count(EventKind.EventArrived), Is.EqualTo(2));
            // 1 = 预置的钩子病例(病种 7);+1 = 本日产出的那位(2004 心衰;2001 是遭遇条)。
            Assert.That(h.Count(EventKind.DiseaseOnset), Is.EqualTo(2));
            Assert.That(h.Presence.Added.Count, Is.EqualTo(2));

            var ids = new List<int>();
            foreach (TechniqueUnlockedPayload u in h.Unlocks()) ids.Add(u.TechniqueId);
            Assert.That(ids, Is.EquivalentTo(new[] { 0, 1, 11 }),
                "0/1 = 起步集;11 = 毛地黄(2004 触发)");
        }

        // ══════════════════════════════════════════════════════════════════════
        //  第三测:同一天 + 钩子配额还在 ⇒ 技法全锁的病种以**钩子病例**身份进池
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 种子 `0x5EED`、配额还在:2002(疟疾)准入为**钩子病例** ⇒ 病人照常出现,
        /// 而「降临了一个疟疾病人」又解锁了金鸡纳。
        /// </summary>
        /// <remarks>
        /// <para>这条钉的是**生产语义**:钩子放行的判据本体在 `PatientDrawEligibility.Decide` +
        /// `HookCasePolicy.AllowsInjection`,配额计数在 `HookCaseLedger`;**三者都是生产件**
        /// (T18 乙之后夹具不再自己组这一段)。</para>
        /// <para>与第二测对照读:同一世界种子、同一份数据,**唯一变量是配额**——
        /// 配额在 ⇒ 4 条候选、2002 以钩子身份被抽中;配额用完 ⇒ 3 条候选、2002 退池。
        /// 这正是 hook 规则 1「每章 1–3 例」在流层的可观测形态。</para>
        /// </remarks>
        [Test]
        public void test_realData_withHookQuotaAvailable_theLockedKeyEntersAsHookCase()
        {
            Harness h = Harness.Build(worldSeed: 0x5EEDUL);

            Assert.That(h.OpportunityCandidateCount, Is.EqualTo(4),
                "配额还在 ⇒ 2002 以钩子病例身份留在候选集里");

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
            /// <param name="worldSeed">世界种子。</param>
            /// <param name="preSeedHookCase">是否**先把一例钩子病例塞进流** —— 等价于
            /// 「本章的钩子配额已经用掉」。⚠️ 走生产写者(`PatientSpawner.SpawnNext` 写
            /// `DiseaseOnset`),不伪造事件;账本(`HookCaseLedger`)据此自然算出 count = 1。</param>
            public static Harness Build(ulong worldSeed, bool preSeedHookCase = false)
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

                // ── 起步集**必须在流为空时**写(ADR-043 §D9 的判据):先于任何预置事件 ──
                //    ⚠️ 放到后面(与预置的钩子病例同序)会让 `Ensure` 看到非空流 ⇒ no-op ⇒
                //    一门起步技法都没有 ⇒ 所有病种被技法门拒 ⇒ 候选集只剩「不产病人」的遭遇条目。
                //    (这处顺序在 T18 首跑实测中真的踩到了,行为是候选数 3 → 1。)
                TechniqueUnlockBootstrap.Ensure(stream.Events, unlockWriter, playerActorId, tick: 0);

                // ── ③ 投放资格 —— 🔁 **T18 乙后走生产件 `PatientAdmissionPolicy`** ──
                //   改前本文件在这里**自己组**一遍(钩子配额用常量代替),因为
                //   `CompositionRoot` 里那段是内联 λ、没有落点可调。T18 把它收成具名类 ⇒
                //   装配与夹具此后**是同一个对象**,分叉只可能发生在「有没有 new 它」这一点。
                Func<int, bool> isTechniqueUnlocked = techniqueId
                    => projection.IsUnlocked(playerActorId, techniqueId);

                var hookCases = new HookCaseLedger(() => stream.Events, decoder, drawIndex, registryById);
                var admission = new PatientAdmissionPolicy(
                    drawIndex, isTechniqueUnlocked, playerActor, hookCases, playerPrescriptionLevel: 0);

                Func<int, bool> isDiseaseAdmissible = admission.DiseaseAdmissible(registryById);

                // ── ④ 52 链(候选集 / 上下文 / 病人三条缝,全部走生产件)──
                // 🔁 **T18 丙**:投放资格**前移到候选集**(与 `CompositionRoot` 同一份组合)。
                Func<int, bool> requiresPatient = EventPoolPatientSpawner.RequiresPatient(pool);
                Func<int, bool> canSpawnPatient = EventPoolPatientSpawner.CanSpawnPatient(pool, isDiseaseAdmissible);
                Func<int, bool> canProduceKey = key => !requiresPatient(key) || canSpawnPatient(key);

                Func<int, long, IReadOnlyList<WeightedKey>> candidatesOfTier = (tier, tick) =>
                    EventWeighting.CandidatesOfTier(pool, tier,
                        isNight: TimeBase.IsNight(tick, time),
                        historyMult: null,
                        keyFilter: canProduceKey);

                int[] houseCallKeys = pool.HouseCallKeys();
                var clinicCells = new ClinicCellSource();   // 未绑定形态 ⇒ 恒 false(生产里玩家未出生前即此)
                var spawner = new PatientSpawner(ids, stream, encoder, worldSeed);

                // 「配额已用掉」的形态:先让 9 真写一条钩子病例的 `DiseaseOnset`(病种 7 疟疾,
                // 此刻只认技法 12 ⇒ 账本判它是一例钩子)。**不伪造事件、不改账本**。
                if (preSeedHookCase)
                {
                    PatientId hookPatient = spawner.SpawnNext(diseaseId: 7, tick: 0L);
                    presence.AddPresent(hookPatient, AnchorCell);
                }

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
                    requiresPatient: requiresPatient,
                    canSpawnPatient: canSpawnPatient);

                var director = new RandomEventsDirectorDriver(
                    () => stream.Events, stream, encoder, decoder, clinicCells,
                    houseCallKeys, chain, playerActor);
                director.BindPlayer(playerActorId);   // 与生产同序:装配后置绑定(玩家 id 原则上一刻才铸造)

                // ── ⑤ 解锁驱动(生产 `BootRoot` 4.4b;起步集已在上方写完)──
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
