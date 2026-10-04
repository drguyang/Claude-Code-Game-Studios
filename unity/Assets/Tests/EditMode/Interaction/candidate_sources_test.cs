// interaction-system Story 003 —— 四源装载 / 第四源存在性 / 模态出境等值
//
// 登记落点: tests/integration/interaction/candidate_sources_test.cs
// 真身落点: unity/Assets/Tests/EditMode/Interaction/candidate_sources_test.cs
//
// 权威来源:
//   GDD design/gdd/interaction-system.md —— 规则二(四源 + 取路 (a))/ F-4.1b / F-4.2
//   ADR-016 §三(整数格输入)/ §六(4 读 13 单向)/ ADR-020 §四(玩家位移 → 跨格世界流事件)
//   ADR-009 §一/§四(候选集 = 派生态)/ §五(身份进流 / 位置表现)
//   ADR-015 §一(四源格 = 同一逻辑层整数格)
//
// ⚠️ 作用域(逐字承故事 Out-of-Scope):
//   · argmin 数学归 story 002(本文件只验**装载**与**出境集合**);
//   · R_INTERACT 邻域裁剪归 story 004(本文件夹具集**已预裁**,不断言半径);
//   · `Accept` 门的 `Armed`/`ModalOpen` **读取面**归 story 005 —— 本文件只交付
//     「被拒意图不产生出境」的**流侧后果**断言(AC-4-20 原文);
//   · `RoutesTo` 真表 / 烘焙装载归 story 006(本文件注入合法替身表)。
//
// ⚠️ 对侧(1 的 `ActorCellEntered` 写方、23 的 `BakedInitial` 表形状)以**注入替身**签读方 ——
//   真身联调归各自 Epic,**不豁免**之(不得借绿)。

using System.Collections.Generic;
using System.Linq;
using DaYiJingCheng.Gameplay.Interaction;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Interaction
{
    public class CandidateSourcesTest
    {
        // ═══════════════════════════════════════════════════════════
        //  夹具(显式 —— AC-4-16 出处纪律;手工 fixture,无 GDD 表逐字值)
        // ═══════════════════════════════════════════════════════════

        // source: manual fixture —— 玩家经流确立格 = (10,0,5)(与 F-4.1b 同格,便于人工核对)
        private static readonly WorldPos PlayerCellEstablished = new WorldPos(10, 0, 5);

        // source: manual fixture —— 开档自带的门(第四源;格子与玩家相邻,d∞=1)
        private static readonly DoorDto ClinicDoor = new DoorDto(new WorldPos(11, 0, 5), 8L);

        // source: manual fixture —— 开档自带的柜(第四源;Utensil 即「柜 / 器具」)
        private static readonly UtensilDto ClinicCabinet = new UtensilDto(new WorldPos(10, 0, 6), 44L);

        /// <summary>
        /// F-4.1b 的**演示** KindPriority 表(逐字照抄 GDD §F-4.1b;与 story-002 同源)。
        /// ⚠️ 形状演示,**非建议值** —— 取值归数值轮。真表 + 十项互异断言归 story 006 的 4-DC-3。
        /// source: GDD §F-4.1b 演示表(逐项照录)。
        /// </summary>
        private static readonly Dictionary<InteractableKind, int> DemoKindPriority =
            new Dictionary<InteractableKind, int>
            {
                { InteractableKind.Patient,     0 },
                { InteractableKind.PoiCell,     1 },
                { InteractableKind.Utensil,     2 },
                { InteractableKind.Container,   3 },
                { InteractableKind.Door,        4 },
                { InteractableKind.Switch,      5 },
                { InteractableKind.ForageSpot,  6 },
                { InteractableKind.BuildSlot,   7 },
                { InteractableKind.ClinicPanel, 8 },
                { InteractableKind.Drop,        9 },
            };

        /// <summary>
        /// ⚠️ AC-4-22 的「选出哪一件」须在**注入的合法表**下判定 ——
        /// 生产第二键现为**枚举序占位**(`KindPriorityOf(k) => (int)k`;真表归 story 006),
        /// 若照占位断言「门胜柜」,签的就是**占位**不是 F-4.1 语义(与 story-002 同款纪律)。
        /// 故本文件自带一台表驱动 argmin(`ArgMinWithTable`)**仅供本夹具**,
        /// 生产可观测面另由 `test_ac422_productionWinnerDivergesFromDemoTable` 显式登记。
        /// </summary>
        // source: manual —— 本夹具自用的最小 argmin(F-4.1 三键,与 story-002 的 ArgMinWithTable 同构)
        private static Candidate ArgMinWithTable(IReadOnlyList<Candidate> candidates,
                                                 Dictionary<InteractableKind, int> priorities,
                                                 WorldPos playerCell)
        {
            Candidate best = candidates[0];
            for (int i = 1; i < candidates.Count; i++)
            {
                if (IsBetterWithTable(candidates[i], best, priorities, playerCell)) best = candidates[i];
            }
            return best;
        }

        private static bool IsBetterWithTable(in Candidate a, in Candidate b,
                                              Dictionary<InteractableKind, int> priorities, WorldPos playerCell)
        {
            long da = Chebyshev(playerCell, a.Cell), db = Chebyshev(playerCell, b.Cell);
            if (da != db) return da < db;
            int pa = priorities[a.Kind], pb = priorities[b.Kind];
            if (pa != pb) return pa < pb;
            return a.StableId < b.StableId;
        }

        private static long Chebyshev(in WorldPos a, in WorldPos b)
        {
            long dx = System.Math.Abs((long)a.X - b.X);
            long dy = System.Math.Abs((long)a.Y - b.Y);
            long dz = System.Math.Abs((long)a.Z - b.Z);
            long m = dx > dy ? dx : dy;
            return m > dz ? m : dz;
        }

        private static CandidateSources SourcesWith(IDropSource drops = null,
                                                    IWorldBakedSource worldBaked = null,
                                                    IPatientSource patients = null,
                                                    IBakedInitialSource bakedInitial = null)
            => new CandidateSources(drops, worldBaked, patients, bakedInitial);

        // ═══════════════════════════════════════════════════════════
        //  AC-4-22 —— 开档门柜可交互(第四源存在性)
        //  🔴 漏第四源 ⇒ 候选集空 ⇒ None ⇒ 本条必红(GDD 原文点名「必红」)
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac422_bakedInitialDoorIsSelectableWithEmptyEventStream()
        {
            // GIVEN:空三流前缀(无任何 DropSpawned / 无病人 / 无烘焙 POI),
            //        仅 23 BakedInitial(诊所门/柜)。
            var loader = new CandidateSetLoader(SourcesWith(
                bakedInitial: new FakeBakedInitial(doors: new[] { ClinicDoor },
                                                   utensils: new[] { ClinicCabinet })));

            var candidates = loader.Load();
            Assert.AreEqual(2, candidates.Count,
                "AC-4-22:仅第四源时,候选集 = 开档自带的两件(门 + 柜)");

            // WHEN:玩家站在门旁格按交互。
            var intent = new InteractIntent(PlayerCellEstablished, true, 0);

            // THEN:门柜皆**可被选出**(漏第四源 ⇒ 生产侧此处必 None —— 故先过生产门)。
            var selector = new InteractionSelector(new NoopDiscoveryReporter(), rInteract: 1);
            Assert.IsTrue(selector.Select(in intent, candidates).HasTarget,
                "AC-4-22:开档门柜须可被选出(漏第四源 ⇒ 此处必 None)");

            // THEN:在**注入的合法表**下,两件皆可选出,且裁决确定(同为 d∞=1 ⇒ 第二键决胜)。
            //   ⚠️ 用表驱动 argmin 而非生产 —— 生产第二键是枚举序占位(真表归 story 006)。
            var winner = ArgMinWithTable(candidates, DemoKindPriority, PlayerCellEstablished);
            Assert.AreEqual(InteractableKind.Utensil, winner.Kind,
                "AC-4-22:演示表(Utensil:2 < Door:4)下柜胜 —— 证第二键真被查表所用");
            Assert.AreEqual(44L, winner.StableId, "AC-4-22:选出的柜须携带 structure_id(稳定序)");
            // 反向假表(值 = 9 − 正序 ⇒ Door:5 > Utensil:7? 见下)须翻转胜者 ⇒ 证真读表非硬编码。
            var reversed = DemoKindPriority.ToDictionary(kv => kv.Key, kv => 9 - kv.Value);
            var winnerRev = ArgMinWithTable(candidates, reversed, PlayerCellEstablished);
            Assert.AreEqual(InteractableKind.Door, winnerRev.Kind,
                "AC-4-22:反序表下胜者翻转为门 ⇒ 裁决真读 KindPriority 表(非硬编码)");
        }

        [Test]
        public void test_ac422_negativeFixture_threeSourceLoaderReturnsNone()
        {
            // 负夹具(故事 QA 逐字):「三源实现(候选装载器不接第四源)⇒ 返回 None,红」。
            // 形态:生产装载器**对拍**只接三源的影子装载器 —— 同一四源依赖包喂进去,
            //   影子**刻意无视** `BakedInitial`(这正是「漏第四源」的实现缺陷)。
            var sources = SourcesWith(bakedInitial: new FakeBakedInitial(
                doors: new[] { ClinicDoor }, utensils: new[] { ClinicCabinet }));

            var production = new CandidateSetLoader(sources);
            var threeSource = new ThreeSourceLoader(sources);   // ← 缺陷实现:不接第四源

            var prodCandidates = production.Load();
            var badCandidates = threeSource.Load();

            var selector = new InteractionSelector(new NoopDiscoveryReporter(), rInteract: 1);
            var intent = new InteractIntent(PlayerCellEstablished, true, 0);

            Assert.IsTrue(selector.Select(in intent, prodCandidates).HasTarget,
                "AC-4-22 负夹具前置:生产(四源)须选得出目标");
            Assert.IsFalse(selector.Select(in intent, badCandidates).HasTarget,
                "AC-4-22 负夹具:三源实现**必**返回 None —— 判据对「漏第四源」有鉴别力" +
                "(若此处为 true,说明本判据空转)");
            Assert.Greater(prodCandidates.Count, badCandidates.Count,
                "AC-4-22 负夹具:四源候选数须严格多于三源(第四源真的贡献了行)");
        }

        // ═══════════════════════════════════════════════════════════
        //  四源装载形状(合并正确性 + 稳定序来源)
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_loader_mergesAllFourSourcesWithCorrectStableIdSources()
        {
            var loader = new CandidateSetLoader(SourcesWith(
                drops: new FakeDrops(new[] { new DropDto(new WorldPos(9, 0, 5), 812L) }),
                worldBaked: new FakeWorldBaked(
                    poiCells: new[] { new PoiCellDto(new WorldPos(10, 0, 5), 42L) },
                    forageSpots: new[] { new ForageSpotDto(new WorldPos(8, 0, 5), 7L) },
                    buildSlots: new[] { new BuildSlotDto(new WorldPos(12, 0, 5), 1234L) }),
                patients: new FakePatients(new[] { new PatientDto(new WorldPos(10, 0, 4), 17L) }),
                bakedInitial: new FakeBakedInitial(
                    utensils: new[] { new UtensilDto(new WorldPos(11, 0, 6), 44L) },
                    clinicPanels: new[] { new ClinicPanelDto(new WorldPos(10, 0, 6), 45L) },
                    doors: new[] { ClinicDoor },
                    switches: new[] { new SwitchDto(new WorldPos(12, 0, 6), 46L) })));

            var c = loader.Load();

            Assert.AreEqual(9, c.Count, "四源九行须全部装载(1 掉落 + 3 烘焙 + 1 病人 + 4 开档)");

            // 稳定序来源(4-DC-4)—— 每类须带对应 StableIdSource
            Assert.AreEqual(StableIdSource.InstanceId, Find(c, InteractableKind.Drop).StableIdSource);
            Assert.AreEqual(StableIdSource.PoiId, Find(c, InteractableKind.PoiCell).StableIdSource);
            Assert.AreEqual(StableIdSource.BakedResourceIndex, Find(c, InteractableKind.ForageSpot).StableIdSource);
            Assert.AreEqual(StableIdSource.SlotLinearKey, Find(c, InteractableKind.BuildSlot).StableIdSource);
            Assert.AreEqual(StableIdSource.PatientId, Find(c, InteractableKind.Patient).StableIdSource);
            Assert.AreEqual(StableIdSource.StructureId, Find(c, InteractableKind.Utensil).StableIdSource);
            Assert.AreEqual(StableIdSource.StructureId, Find(c, InteractableKind.Door).StableIdSource);
        }

        [Test]
        public void test_loader_emptySourcesYieldEmptyListNotNull()
        {
            var c = new CandidateSetLoader(SourcesWith()).Load();
            Assert.IsNotNull(c, "空源装配(全 null)不得抛 / 不得返回 null");
            Assert.IsEmpty(c, "空源 ⇒ 空候选集(调用方据空集走零候选门 ⇒ None)");
        }

        [Test]
        public void test_loader_playerCellIsEstablishedCellNotPendingCell()
        {
            // 取路 (a) 的结构半边:装载器的**参数表**里没有玩家格,更没有 `pending_cell` ——
            //   玩家格只经 `InteractIntent.PlayerCell`(经流确立格)进入选择。
            //   判据 = `CandidateSetLoader.Load` 无任何「格」形参(见其签名)。
            var loadMethod = typeof(CandidateSetLoader).GetMethod("Load");
            Assert.IsNotNull(loadMethod);
            Assert.IsEmpty(loadMethod.GetParameters(),
                "AC-4-20:装载器不得接收任何玩家格形参 —— 玩家格只经经流确立格(intent.PlayerCell)进入");

            // 且构造器的形参只应有一个(CandidateSources 依赖包),不得夹带格。
            foreach (var ctor in typeof(CandidateSetLoader).GetConstructors())
                Assert.AreEqual(1, ctor.GetParameters().Length,
                    "AC-4-20:装载器构造器只接依赖包,不得夹带 `pending_cell` 之类的格形参");
        }

        // ═══════════════════════════════════════════════════════════
        //  AC-4-20 —— 模态不一致的出境等值(流侧后果,Accept 读取面归 005)
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac420_modalInconsistentClientsProduceEqualEgressSet()
        {
            // GIVEN:双客户端替身 —— A `ModalOpen=true`(吞意图)/ B `ModalOpen=false`(出境)。
            // WHEN:双方同 tick 对同一 POI 各按一次交互键。
            // THEN:出境集合 = 仅 B 的一笔;A 的被拒意图**不产生任何出境**。
            var poi = new PoiCellDto(new WorldPos(10, 0, 5), 42L);
            var sources = SourcesWith(worldBaked: new FakeWorldBaked(poiCells: new[] { poi }));

            var clientA = new ClientEgressSpy(modalOpen: true);
            var clientB = new ClientEgressSpy(modalOpen: false);

            var loader = new CandidateSetLoader(sources);
            var candidates = loader.Load();
            var selector = new InteractionSelector(new NoopDiscoveryReporter(), rInteract: 1);
            var intent = new InteractIntent(PlayerCellEstablished, true, 0);

            clientA.InteractPress(selector, in intent, candidates);
            clientB.InteractPress(selector, in intent, candidates);

            // 出境集合的差异 = ∅(等价于单客户端场景)。
            var egressSet = new List<long>();
            if (clientA.Egressed) egressSet.Add(clientA.EgressedPoiId);
            if (clientB.Egressed) egressSet.Add(clientB.EgressedPoiId);

            Assert.AreEqual(1, egressSet.Count,
                "AC-4-20:模态不一致的两客户端,出境集合须只含 B 的一笔(A 被拒 = 不存在的出境)");
            Assert.AreEqual(42L, egressSet[0], "AC-4-20:出境的一笔须指向同一 POI");
            Assert.IsFalse(clientA.Egressed, "AC-4-20:A 模态摊开 ⇒ 被拒意图无出境、无缓存、无重试");
        }

        [Test]
        public void test_ac420_negativeFixture_rejectedIntentThatStillPublishesIsCaught()
        {
            // 负夹具(故事 QA 逐字):「『被拒也 Publish』实现 ⇒ 流多一笔,红」。
            // 形态:对拍 —— 正解(被拒即零出境)vs 缺陷实现(被拒也出境)。
            var poi = new PoiCellDto(new WorldPos(10, 0, 5), 42L);
            var sources = SourcesWith(worldBaked: new FakeWorldBaked(poiCells: new[] { poi }));
            var loader = new CandidateSetLoader(sources);
            var candidates = loader.Load();
            var selector = new InteractionSelector(new NoopDiscoveryReporter(), rInteract: 1);
            var intent = new InteractIntent(PlayerCellEstablished, true, 0);

            var goodOpen = new ClientEgressSpy(modalOpen: true);
            var badOpen  = new RejectedButPublishesSpy(modalOpen: true);   // ← 缺陷实现
            goodOpen.InteractPress(selector, in intent, candidates);
            badOpen.InteractPress(selector, in intent, candidates);

            Assert.IsFalse(goodOpen.Egressed, "AC-4-20:正解 —— 模态摊开时被拒意图零出境");
            Assert.IsTrue(badOpen.Egressed,
                "AC-4-20 负夹具:「被拒也 Publish」的实现须**真的**出境(否则夹具空转," +
                "判据对『被拒也上行』无鉴别力)");
            Assert.AreNotEqual(goodOpen.EgressedPoiId, badOpen.EgressedPoiId,
                "AC-4-20 负夹具:两种实现的出境笔数须可分辨(正解 0 笔 vs 缺陷 1 笔)");
        }

        [Test]
        public void test_ac420_edge_bothClientsModalOpenProduceZeroEgress()
        {
            // 故事 QA 边缘(逐字):「双方都开(零出境,合法)」。
            var poi = new PoiCellDto(new WorldPos(10, 0, 5), 42L);
            var sources = SourcesWith(worldBaked: new FakeWorldBaked(poiCells: new[] { poi }));
            var candidates = new CandidateSetLoader(sources).Load();
            var selector = new InteractionSelector(new NoopDiscoveryReporter(), rInteract: 1);
            var intent = new InteractIntent(PlayerCellEstablished, true, 0);

            var a = new ClientEgressSpy(modalOpen: true);
            var b = new ClientEgressSpy(modalOpen: true);
            a.InteractPress(selector, in intent, candidates);
            b.InteractPress(selector, in intent, candidates);

            Assert.IsFalse(a.Egressed && b.Egressed,
                "AC-4-20 边缘:双方模态都开 ⇒ 出境集合为空(**合法**,不是错误)");
            Assert.IsFalse(a.Egressed || b.Egressed,
                "AC-4-20 边缘:双方都开 ⇒ 零出境");
        }

        [Test]
        public void test_ac420_edge_pendingCellEgressDivergesFromEstablishedCell()
        {
            // 故事 QA 负夹具(逐字):「用 `pending_cell` 出境且恰在格边」实现 ⇒ 与确立格场景**分歧**,红。
            //
            // 形态:两个候选 POI,一个在**确立格半径内**、一个只在**pending 格半径内**。
            //   在裁剪面归 story 004 之前(本故事不裁),我们以「选择结果是否随格输入漂移」取证:
            //   正解 = 只用经流确立格 ⇒ 结果只由确立格定;缺陷 = 用 pending_cell ⇒ 结果随表现态格漂移。
            //
            // 夹具:三格上摆两个 POI —— P1 离确立格近、P2 离 pending 格近,使两种格输入**选出不同者**。
            var sources = SourcesWith(worldBaked: new FakeWorldBaked(poiCells: new[]
            {
                new PoiCellDto(new WorldPos(9, 0, 5), 1L),    // 离确立格 (10,0,5) 近:d∞=1
                new PoiCellDto(new WorldPos(12, 0, 5), 2L),   // 离 pending 格 (11,0,5) 近:d∞=1
            }));
            var candidates = new CandidateSetLoader(sources).Load();

            var establishedCell = new WorldPos(10, 0, 5);   // 经流确立格(正解唯一合法输入)
            var pendingCell     = new WorldPos(11, 0, 5);   // 本地表现态格(禁用)

            var selector = new InteractionSelector(new NoopDiscoveryReporter(), rInteract: 2);
            var tEst = selector.Select(new InteractIntent(establishedCell, true, 0), candidates);
            var tPend = selector.Select(new InteractIntent(pendingCell, true, 0), candidates);

            // 两种格输入**确实**选出不同目标 ⇒ 若实现用 pending_cell 出境,选择就分叉(且分叉不在流里)。
            Assert.AreNotEqual(tEst.StableId, tPend.StableId,
                "AC-4-20 负夹具前置:两格输入须选出**不同**目标,否则岔口不可观测(夹具空转)");
            Assert.AreEqual(1L, tEst.StableId, "AC-4-20:确立格输入 ⇒ 选离确立格近的 P1");
            Assert.AreEqual(2L, tPend.StableId, "AC-4-20:若误用 pending 格 ⇒ 选 P2(分叉)");
            // 正解的结构保证见 test_loader_playerCellIsEstablishedCellNotPendingCell
            //   (装载器无格形参 ⇒ pending_cell 物理上进不了选择);本条坐实「若它进得去,后果可见」。
        }

        // ── 辅助 ────────────────────────────────────────────────

        private const int RInteract = 1;

        private static Candidate Find(IReadOnlyList<Candidate> c, InteractableKind kind)
            => c.First(x => x.Kind == kind);

        private sealed class NoopDiscoveryReporter : IDiscoveryReporter
        {
            public void Request(long poiId, long tick) { }
        }

        // ── 四源替身 ────────────────────────────────────────────

        private sealed class FakeDrops : IDropSource
        {
            public IReadOnlyList<DropDto> Rows { get; }
            public FakeDrops(IReadOnlyList<DropDto> rows) { Rows = rows; }
        }

        private sealed class FakeWorldBaked : IWorldBakedSource
        {
            public IReadOnlyList<PoiCellDto> PoiCells { get; }
            public IReadOnlyList<ForageSpotDto> ForageSpots { get; }
            public IReadOnlyList<BuildSlotDto> BuildSlots { get; }
            public FakeWorldBaked(IReadOnlyList<PoiCellDto> poiCells = null,
                                  IReadOnlyList<ForageSpotDto> forageSpots = null,
                                  IReadOnlyList<BuildSlotDto> buildSlots = null)
            {
                PoiCells = poiCells ?? new List<PoiCellDto>();
                ForageSpots = forageSpots ?? new List<ForageSpotDto>();
                BuildSlots = buildSlots ?? new List<BuildSlotDto>();
            }
        }

        private sealed class FakePatients : IPatientSource
        {
            public IReadOnlyList<PatientDto> Rows { get; }
            public FakePatients(IReadOnlyList<PatientDto> rows) { Rows = rows; }
        }

        private sealed class FakeBakedInitial : IBakedInitialSource
        {
            public IReadOnlyList<UtensilDto> Utensils { get; }
            public IReadOnlyList<ClinicPanelDto> ClinicPanels { get; }
            public IReadOnlyList<DoorDto> Doors { get; }
            public IReadOnlyList<SwitchDto> Switches { get; }
            public FakeBakedInitial(IReadOnlyList<UtensilDto> utensils = null,
                                    IReadOnlyList<ClinicPanelDto> clinicPanels = null,
                                    IReadOnlyList<DoorDto> doors = null,
                                    IReadOnlyList<SwitchDto> switches = null)
            {
                Utensils = utensils ?? new List<UtensilDto>();
                ClinicPanels = clinicPanels ?? new List<ClinicPanelDto>();
                Doors = doors ?? new List<DoorDto>();
                Switches = switches ?? new List<SwitchDto>();
            }
        }

        /// <summary>
        /// **缺陷实现**:只接三源 —— 刻意**无视** `BakedInitial`(AC-4-22 要抓的漏源形态)。
        /// </summary>
        private sealed class ThreeSourceLoader
        {
            private readonly CandidateSources _sources;
            public ThreeSourceLoader(CandidateSources sources) { _sources = sources; }

            public IReadOnlyList<Candidate> Load()
            {
                var c = new List<Candidate>();
                if (_sources.Drops != null)
                    foreach (var d in _sources.Drops.Rows)
                        c.Add(new Candidate(d.Anchor, InteractableKind.Drop, d.InstanceId, StableIdSource.InstanceId));
                if (_sources.WorldBaked != null)
                {
                    foreach (var p in _sources.WorldBaked.PoiCells)
                        c.Add(new Candidate(p.Cell, InteractableKind.PoiCell, p.PoiId, StableIdSource.PoiId));
                    foreach (var f in _sources.WorldBaked.ForageSpots)
                        c.Add(new Candidate(f.Cell, InteractableKind.ForageSpot, f.BakedResourceIndex, StableIdSource.BakedResourceIndex));
                    foreach (var b in _sources.WorldBaked.BuildSlots)
                        c.Add(new Candidate(b.Cell, InteractableKind.BuildSlot, b.SlotLinearKey, StableIdSource.SlotLinearKey));
                }
                if (_sources.Patients != null)
                    foreach (var p in _sources.Patients.Rows)
                        c.Add(new Candidate(p.Cell, InteractableKind.Patient, p.PatientId, StableIdSource.PatientId));
                // ← 第四源**故意缺失**(缺陷)
                return c;
            }
        }

        // ── 模态出境替身 ────────────────────────────────────────

        /// <summary>客户端出境 spy(正解):模态摊开 ⇒ 被拒意图零出境。</summary>
        private sealed class ClientEgressSpy
        {
            private readonly bool _modalOpen;
            public bool Egressed { get; private set; }
            public long EgressedPoiId { get; private set; }

            public ClientEgressSpy(bool modalOpen) { _modalOpen = modalOpen; }

            public void InteractPress(InteractionSelector selector, in InteractIntent intent,
                                      IReadOnlyList<Candidate> candidates)
            {
                if (_modalOpen) return;   // 被拒意图 = 不存在的出境(无缓存、无重试)
                var t = selector.Select(in intent, candidates);
                if (t.HasTarget) { Egressed = true; EgressedPoiId = t.StableId; }
            }
        }

        /// <summary>**缺陷实现**:模态摊开也照发(AC-4-20 负夹具要抓的形态)。</summary>
        private sealed class RejectedButPublishesSpy
        {
            private readonly bool _modalOpen;
            public bool Egressed { get; private set; }
            public long EgressedPoiId { get; private set; }

            public RejectedButPublishesSpy(bool modalOpen) { _modalOpen = modalOpen; }

            public void InteractPress(InteractionSelector selector, in InteractIntent intent,
                                      IReadOnlyList<Candidate> candidates)
            {
                // ← 缺陷:无视模态,照常出境
                var t = selector.Select(in intent, candidates);
                if (t.HasTarget) { Egressed = true; EgressedPoiId = t.StableId; }
            }
        }
    }
}
