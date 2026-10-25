// 24 匹配链的**端到端装配判据**(`TD-building-01` 读侧 · 2026-10-21)。
//
// 权威来源:
//   `TD-building-01`(docs/tech-debt-register.md:2097)「解除动作 · 读侧」的判据逐字:
//     「判据 = **真 `World` 端到端**:`Place → IsEffectivelyWalkable == false` /
//      `Remove → true`,且 `AC-24-11` 的布局输入来自真 `World` 而非 Fake」
//   GDD `design/gdd/clinic-machine.md` 规则二 · AC-24-03(ROOM_NONE 基例)
//   `Sim/Clinic/ClinicRoomMatcher.cs`(OQ-24-7 已裁 = 声明序首个匹配)
//
// ⚠️ **本文件补的是什么**:story-006 的 24 测全部把 `IPlacedStructureReadView` 喂成
//   **测试替身**(`FakeStructureReadView` 等)。替身能钉住「匹配算法对不对」,
//   **钉不住**「生产里能不能跑起来」—— 后者需要四个零件同时存在:
//     ① `StructureInstanceReadView`(23 注册表的只读视图,生产实现)
//     ② `BuildModulesCatalog`(`module_id` → `furn_type` 的跨层桥,生产实现)
//     ③ `CompositionRoot` 的装配段(把 ①② + 两张表接成 `ClinicRoomMatchingService`)
//     ④ 真 `StructureWriter` + 真 `StructureInstanceRegistry`(布局的真来源)
//   本文件把 ④ 经 ③ 接进 ①②,断言**真放置**后的房间类型。
//
// ⚠️ **④ 是 `StructureInstanceRegistry`,不是 `World`(2026-10-21 订正)**:
//   `TD-building-01` 的判据逐字要求「**真 `World`** 端到端:`Place → IsEffectivelyWalkable == false` /
//   `Remove → true`」。本文件**未**经 `World` —— 它直接持 `new StructureInstanceRegistry()`,
//   把同一个实例同时喂给 `StructureWriter` 与 `CompositionRoot.Assemble(structures:)`。
//   ⇒ **注册表侧已真,`World` 侧仍未**;`IsEffectivelyWalkable` 的端到端判据**不在本文件面内**,
//   **未验**。原文件头写「真 `World` + 真 `StructureWriter`」是**自述过宽**,已就地订正。
//   🔁 **2026-10-26 订正(S20-2 连带)**:上句「`World` 侧仍未 / 未验」**已过时** ——
//     S20-2a 的 `Tests/EditMode/ModularBuilding/world_end_to_end_test.cs` 已把**真 `World`**
//     接进 `StructureWriter` 的 `IWorldOccupancy` 位,覆盖 `Place → IsEffectivelyWalkable == false` /
//     `Remove → true`(含非锚点足迹格 + 真烘焙目录行);`TD-building-01` 两半已 closed
//     (债册三十五订)。⇒ 本条**不再是缺口**,只是「本文件自己不承担该判据」。
//     本文件与它的分工不变:本文件判**匹配链**(析出 + 房间类型),`World` 端到端判**占用合成**。
//
// ⚠️ **零 Fake**:`StructureInstanceRegistry` / `StructureWriter` /
//   `BuildModulesCatalog` / `ClinicFurnitureCatalogue` / `ClinicRoomTable` 全部是生产类型;
//   数据来自 `Assets/DataCooked/` 的**真烘焙产物**(经 `DataCookedDisk` + 生产 codec 解码)。
//   唯一的测试件是 `CapturingSink`(只收事件不落流 —— `StructureWriter` 要求 `IEventSink`,
//   而本测关心的不是事件流内容,是注册表 + 匹配结果)。
//
// ⚠️ **门 A 不受影响**:本文件住 `Tests/EditMode`(Sim.Contracts.Tests 装配),
//   引用 `Gameplay.Boot` / `Gameplay.Presentation` / `Sim` —— 与既有 `Boot/` 测试同款。

using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Presentation;
using DaYiJingCheng.Sim.Clinic;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.ClinicMachine
{
    /// <summary>
    /// 24 匹配链的端到端判据:真 `World` → 真 `StructureWriter.Place` → 真装配的匹配服务。
    /// </summary>
    [TestFixture]
    public class clinic_room_matching_e2e_test
    {
        // ── 真 `build_modules.cooked` 的 module_id(与 assets/data/build_modules.json 同源)──
        // ⚠️ 这些是**产物里的 id**,不是本测发明的:改 `build_modules.json` 后重烘,
        //    若 id 漂移,`test_catalogue_idsMatchBuildModulesArtifact` 会先红(提醒改这里)。
        private const int ModuleBed = 1;
        private const int ModuleSurgeryTable = 2;      // 足迹 = {(0,0,0),(1,0,0)}
        private const int ModuleSterileCabinet = 3;
        private const int ModuleHerbCabinet = 4;
        private const int ModuleChamberPot = 7;
        private const int ModuleShell = 8;             // furn_type = null(非家具)

        private StructureInstanceRegistry _registry;
        private InMemoryBlobPool _pool;
        private PayloadEncoder _encoder;
        private BuildModulesCatalog _catalogue;
        private ClinicFurnitureCatalogue _furniture;
        private ClinicRoomTable _rooms;
        private StructureWriter _writer;
        private List<SimEvent> _events;

        [SetUp]
        public void SetUp()
        {
            _registry = new StructureInstanceRegistry();
            _pool = new InMemoryBlobPool();
            _encoder = new PayloadEncoder(_pool);
            _events = new List<SimEvent>();

            // 真烘焙产物(缺 ⇒ 抛 —— 不静默返回空表;E-13 同口径)
            _catalogue = new BuildModulesCatalog(DataCookedDisk.LoadBuildModules());
            _furniture = DataCookedDisk.LoadClinicFurniture().Catalogue;
            _rooms = DataCookedDisk.LoadClinicRoom().Table;

            // 真写者:真目录 + 真注册表(occupancy 传 null ⇒ 只测注册表侧;
            // 本测不判 Overlay —— `IsEffectivelyWalkable` 另有一测,见下)
            _writer = new StructureWriter(new CapturingSink(_events), _registry, _encoder,
                                          _catalogue, occupancy: new NoopOccupancy());
        }

        /// <summary>经**真装配**构造匹配服务(不是 `new ClinicRoomMatchingService(...)` 手接)。</summary>
        private CompositionRootServices AssembleWithRealRegistry()
            => CompositionRoot.Assemble(structures: _registry);

        // ══════════════════ 0. 前提:产物与常量对得上 ══════════════════

        /// <summary>本测写死的 `module_id` 必须真的在产物里,且 `furn_type` 如注释所述。
        /// <para><b>判别力</b>:改 `build_modules.json` 的 id / 重排 ⇒ 本测红 ⇒ 提醒同步。</para></summary>
        [Test]
        public void test_catalogue_idsMatchBuildModulesArtifact()
        {
            Assert.That(_catalogue.Count, Is.EqualTo(9), "build_modules 行数漂移 ⇒ 同步本测常量");
            Assert.That(_catalogue.GetFurnType(ModuleBed), Is.EqualTo("bed"));
            Assert.That(_catalogue.GetFurnType(ModuleSurgeryTable), Is.EqualTo("surgery_table"));
            Assert.That(_catalogue.GetFurnType(ModuleSterileCabinet), Is.EqualTo("sterile_cabinet"));
            Assert.That(_catalogue.GetFurnType(ModuleHerbCabinet), Is.EqualTo("herb_cabinet"));
            Assert.That(_catalogue.GetFurnType(ModuleChamberPot), Is.EqualTo("chamber_pot"));
            Assert.That(_catalogue.GetFurnType(ModuleShell), Is.Null,
                "module 8 是 Shell 且 furn_type = null ⇒ 桥须给 null(不是 id.ToString())");
        }

        // ══════════════════ 1. 装配面:null 与真注册表的可观测差别 ══════════════════

        /// <summary>不传 `structures` ⇒ `ClinicRoomMatching` 为 null(**不静默造一个空世界**)。
        /// <para><b>为什么必须有</b>:若装配层自建注册表,「24 求值了但世界是空的」
        /// 与「24 没接线」在运行期**无法区分**;而自建注册表还会与将来的 `World` 实例分叉。</para>
        /// <para><b>判别力</b>:把 `CompositionRoot` 里 `if (structures != null)` 改成无条件构造
        /// (自建空注册表)⇒ 本测红。</para></summary>
        [Test]
        public void test_assemble_withoutStructures_matchingIsNull()
        {
            CompositionRootServices bag = CompositionRoot.Assemble();

            Assert.That(bag.ClinicRoomMatching, Is.Null,
                "未注入 23 注册表 ⇒ 匹配服务须为 null(可观测的「未接线」,不是空世界)");
        }

        /// <summary>传真注册表 ⇒ 服务存在,且三份烘焙数据都解出了(非空表)。</summary>
        [Test]
        public void test_assemble_withStructures_matchingIsReady()
        {
            CompositionRootServices bag = AssembleWithRealRegistry();

            Assert.That(bag.ClinicRoomMatching, Is.Not.Null, "注入真注册表 ⇒ 匹配服务须装配");
            Assert.That(bag.ClinicRoomMatching.Rooms.Count, Is.GreaterThanOrEqualTo(1),
                "ROOM_TABLE 解出 0 个房间类型 —— 装载 / 解码异常");
            Assert.That(bag.ClinicRoomMatching.FurnTypes.Count, Is.GreaterThanOrEqualTo(1),
                "FURN_TYPES 解出 0 个类型 —— 装载 / 解码异常");
        }

        // ══════════════════ 2. ROOM_NONE:空世界 ══════════════════

        /// <summary>空注册表 ⇒ 未析出房间 + 未匹配(AC-24-03 基例,EC-24-01)。
        /// <para>⚠️ 本测**不是** AC-24-03 的唯一载体(story-006 已有双向载体);
        /// 它钉的是「真装配链在空世界下不炸、且走的是短路分支」。</para></summary>
        [Test]
        public void test_emptyWorld_roomNone()
        {
            CompositionRootServices bag = AssembleWithRealRegistry();

            ClinicRoomMatchOutcome outcome = bag.ClinicRoomMatching.Match();

            Assert.That(outcome.Extraction.HasRoom, Is.False, "空布局 ⇒ 未析出房间(EC-24-01)");
            Assert.That(outcome.Extraction.CellCount, Is.EqualTo(0));
            Assert.That(outcome.Matched, Is.False, "未析出 ⇒ 恒 ROOM_NONE");
            Assert.That(outcome.RoomType, Is.Null);
            Assert.That(outcome.Diagnostics.MatchedRoomCount, Is.EqualTo(0),
                "未析出房间 ⇒ 诊断全零(ClinicRoomMatcher 的短路契约)");
        }

        // ══════════════════ 3. ward:两张床 ══════════════════

        /// <summary>两床相邻 ⇒ `ward`(min_count bed ≥ 2)。
        /// <para><b>判别力</b>:`BuildModulesCatalog.GetFurnType` 若返回 `null`(桥断)⇒
        /// 真判据析出的成员集为空 ⇒ `HasRoom == false` ⇒ 本测红。</para></summary>
        [Test]
        public void test_twoAdjacentBeds_matchWard()
        {
            _writer.Place(new WorldPos(0, 0, 0), ModuleBed, 0, 0, tick: 1);
            _writer.Place(new WorldPos(1, 0, 0), ModuleBed, 0, 0, tick: 2);

            CompositionRootServices bag = AssembleWithRealRegistry();
            ClinicRoomMatchOutcome outcome = bag.ClinicRoomMatching.Match();

            Assert.That(outcome.Extraction.HasRoom, Is.True,
                "两张相邻床 = 一个连通簇 ⇒ 须析出房间(桥断 / 判据错都会让它 false)");
            Assert.That(outcome.Extraction.CellCount, Is.EqualTo(2));
            Assert.That(outcome.Matched, Is.True);
            Assert.That(outcome.RoomType, Is.EqualTo("ward"));
        }

        // ══════════════════ 4. surgery:手术台 + 无菌柜(+x 邻接)══════════════════

        /// <summary>`surgery_table`(2 格足迹)与 `sterile_cabinet` 相邻 ⇒ `surgery`。
        /// <para>⚠️ 本测同时钉**多格足迹**的落实:module 2 的 `occupancy_local` 是
        /// `[(0,0,0),(1,0,0)]` ⇒ 放在 (2,0,0) 时占 (2,0,0) 与 (3,0,0)。
        /// 若 `StructureInstanceReadView` 只报锚点格(陷阱 ① 复现)⇒ 簇仍是 1 个但
        /// `CellCount` 会少 1 ⇒ 本测的 `CellCount` 断言红。</para></summary>
        [Test]
        public void test_surgeryTableWithCabinet_matchSurgery()
        {
            // 手术台在 (2,0,0) ⇒ 占 (2,0,0) + (3,0,0)
            _writer.Place(new WorldPos(2, 0, 0), ModuleSurgeryTable, 0, 0, tick: 1);
            // 无菌柜在 (4,0,0) ⇒ 与 (3,0,0) 在 +x 侧相邻
            _writer.Place(new WorldPos(4, 0, 0), ModuleSterileCabinet, 0, 0, tick: 2);

            CompositionRootServices bag = AssembleWithRealRegistry();
            ClinicRoomMatchOutcome outcome = bag.ClinicRoomMatching.Match();

            Assert.That(outcome.Extraction.CellCount, Is.EqualTo(3),
                "手术台 2 格 + 无菌柜 1 格 = 3 格;少于 3 ⇒ 视图丢了非锚点足迹(陷阱 ① 复现)");
            Assert.That(outcome.Extraction.HasRoom, Is.True, "三格连通 ⇒ 一个簇");
            Assert.That(outcome.Matched, Is.True);
            Assert.That(outcome.RoomType, Is.EqualTo("surgery"),
                "手术台 + +x 侧无菌柜 ⇒ surgery;方向反了(柜在 −x)⇒ 见下一测");
        }

        /// <summary>方向反例:无菌柜在**−x** 侧 ⇒ 不满足 `+x` 邻接 ⇒ 不是 surgery。
        /// <para>⚠️ 本测的期望是 **ROOM_NONE 或别的房间类型**(不是「必须 false」)——
        /// 因为 2 格手术台 + 柜子若恰好满足别的类型,那是对的。本测钉的是
        /// 「**不得**判成 surgery」这条方向判据。</para></summary>
        [Test]
        public void test_surgeryTableWithCabinetOnMinusX_isNotSurgery()
        {
            // 无菌柜在 (0,0,0),手术台在 (2,0,0) ⇒ 占 (2,0,0)+(3,0,0)
            // 柜子在 (0,0,0) 与手术台的 (2,0,0) **不相邻**(中间隔了 (1,0,0))——
            // 故把柜子挪到 (1,0,0):它与 (2,0,0) 相邻,但在**−x** 侧。
            _writer.Place(new WorldPos(1, 0, 0), ModuleSterileCabinet, 0, 0, tick: 1);
            _writer.Place(new WorldPos(2, 0, 0), ModuleSurgeryTable, 0, 0, tick: 2);

            CompositionRootServices bag = AssembleWithRealRegistry();
            ClinicRoomMatchOutcome outcome = bag.ClinicRoomMatching.Match();

            Assert.That(outcome.Extraction.HasRoom, Is.True, "前提:三格仍连通");
            Assert.That(outcome.RoomType, Is.Not.EqualTo("surgery"),
                "无菌柜在 −x 侧 ⇒ 不满足 `+x` 邻接子句 ⇒ 不得判成 surgery(方向判据)");
        }

        // ══════════════════ 5. herb_room:+y 邻接 ══════════════════

        /// <summary>`herb_cabinet` 与其 **+y** 侧的 `chamber_pot` ⇒ `herb_room`。
        /// <para>⚠️ 这是唯一覆盖 `+y` 方向的**端到端**载体(story-006 的 `+y` 载体是单元级)。</para></summary>
        [Test]
        public void test_herbCabinetWithPotOnPlusY_matchHerbRoom()
        {
            _writer.Place(new WorldPos(0, 0, 0), ModuleHerbCabinet, 0, 0, tick: 1);
            _writer.Place(new WorldPos(0, 1, 0), ModuleChamberPot, 0, 0, tick: 2);

            CompositionRootServices bag = AssembleWithRealRegistry();
            ClinicRoomMatchOutcome outcome = bag.ClinicRoomMatching.Match();

            Assert.That(outcome.Extraction.HasRoom, Is.True);
            Assert.That(outcome.Matched, Is.True);
            Assert.That(outcome.RoomType, Is.EqualTo("herb_room"));
        }

        // ══════════════════ 6. 非家具不进 S(真判据 vs 代理版)══════════════════

        /// <summary>`Shell` 模块(`furn_type = null`)**不进**成员集 ⇒ 它不桥接两个家具簇。
        /// <para><b>为什么这条是本文件的核心之一</b>:代理版(`Extract(view)`)把「凡被
        /// `StructurePlaced` 占用的格」都算家具格 ⇒ 夹在中间的外壳会把左右两簇桥成一个
        /// 房间(两床 + 一个外壳 = 一簇 ⇒ 误判为 ward)。真判据不数外壳 ⇒ 仍是两簇 ⇒ ROOM_NONE。
        /// 本测断言的是**真判据入口**被用上了。</para>
        /// <para><b>判别力</b>:把 `ClinicRoomMatchingService.Match()` 里的
        /// `ClinicRoomExtraction.Extract(view, _furniture.FurnTypes)` 换成单参重载
        /// ⇒ 三格连通成一簇 ⇒ `HasRoom` 由 false 变 true ⇒ 本测红。</para></summary>
        [Test]
        public void test_nonFurnitureShell_doesNotJoinMembership_twoClusters()
        {
            // 床 (0,0,0) —— 外壳 (1,0,0) —— 床 (2,0,0):三格连通,但只有两张床是家具
            _writer.Place(new WorldPos(0, 0, 0), ModuleBed, 0, 0, tick: 1);
            _writer.Place(new WorldPos(1, 0, 0), ModuleShell, 0, 0, tick: 2);
            _writer.Place(new WorldPos(2, 0, 0), ModuleBed, 0, 0, tick: 3);

            CompositionRootServices bag = AssembleWithRealRegistry();
            ClinicRoomMatchOutcome outcome = bag.ClinicRoomMatching.Match();

            Assert.That(outcome.Extraction.CellCount, Is.EqualTo(2),
                "外壳 furn_type = null ⇒ 不进 S ⇒ 成员格只有 2 张床(代理版会给 3)");
            Assert.That(outcome.Extraction.ConnectedClusterCount, Is.EqualTo(2),
                "两床被外壳隔开 ⇒ 2 个连通簇(代理版会因外壳桥接成 1)");
            Assert.That(outcome.Extraction.HasRoom, Is.False, "≥ 2 簇 ⇒ 整盘 ROOM_NONE(EC-24-07)");
            Assert.That(outcome.Matched, Is.False);
        }

        // ══════════════════ 7. 无缓存:拆掉后重算即变 ══════════════════

        /// <summary>同一服务实例:先两床 ⇒ ward;拆一张 ⇒ 不再 ward(无陈旧缓存)。
        /// <para><b>为什么必须有</b>:`StructureInstanceReadView` / `ClinicFurnitureTraitsSource`
        /// 都是**快照**语义。若 `Match()` 缓存了视图实例,拆除后仍会返回旧结果 ——
        /// 这是「静默错误」,不抛、不警告。</para>
        /// <para><b>判别力</b>:把 `Match()` 里新建视图的那两行提到构造期(存字段)⇒ 本测红。</para>
        /// <para>⚠️ **本测不覆盖 AC-24-11 的 Memoize 面**(那归 story-005)——
        /// 它只钉「本实现没有陈旧缓存」这一条。</para>
        /// <para>🔁 **2026-10-26 订正(S20-2 连带,原文不删)**:上句的 `AC-24-11` 应读作
        /// **`EC-24-05`** —— GDD `:411` 的 AC-24-11 是「外壳格 / 空地板格不计分、不参与连通」
        /// (归 story-002),缓存失效的权威出处是 EC-24-05 `:291` + States 注 `:123-127`
        /// (story-005 卡面 2026-10-25 重写轮订正表 ①)。Memoize 的实现载体现为
        /// `Gameplay.Presentation/ClinicEnvironmentMemo.cs`,其判据在
        /// `Tests/EditMode/ClinicMachine/clinic_purity_replay_test.cs`。</para></summary>
        [Test]
        public void test_afterRemove_recomputedWithoutStaleCache()
        {
            long a = _writer.Place(new WorldPos(0, 0, 0), ModuleBed, 0, 0, tick: 1);
            _writer.Place(new WorldPos(1, 0, 0), ModuleBed, 0, 0, tick: 2);

            CompositionRootServices bag = AssembleWithRealRegistry();
            Assert.That(bag.ClinicRoomMatching.Match().RoomType, Is.EqualTo("ward"), "前提:两床 ⇒ ward");

            Assert.That(_writer.Remove(a, tick: 3), Is.True);
            ClinicRoomMatchOutcome after = bag.ClinicRoomMatching.Match();

            Assert.That(after.RoomType, Is.Not.EqualTo("ward"),
                "拆掉一张床后不得仍判 ward —— 陈旧缓存会让这里静默返回旧值");
            Assert.That(after.Extraction.CellCount, Is.EqualTo(1), "剩一张床");
        }

        // ══════════════════ 8. 多匹配:声明序首个(经真表)══════════════════

        /// <summary>同时满足 `ward`(bed ≥ 2)与 `surgery` 的布局 ⇒ 取 `Rooms` 里**序最靠前**者。
        /// <para>⚠️ <b>本测连带揭示的一条生产事实(`OQ-24-7` 裁定的落地形态,2026-10-21 实测)</b>:
        /// 「声明序」在**生产环境里 = 产物行序**,而产物行序**不是**作者态 JSON 的手写序 ——
        /// `ClinicMachineCookedWriter.cs:123` 对房间行做了
        /// <c>rooms.Sort((x, y) =&gt; string.CompareOrdinal(x.RoomType, y.RoomType))</c>
        /// (写方头注 `:22-26` 自陈「行序一律全序」,为的是消除 `List.Sort` 不稳定带来的
        /// 跨平台漂移)。⇒ 作者态 JSON 写的是 `ward, surgery, herb_room`,而
        /// **产物与运行期 `Rooms` 的顺序是 `herb_room, surgery, ward`**(序数升序)。
        /// ⇒ 多匹配时胜出者 = **`room_type` 序数最小**的那个,**不是** JSON 里排第一的那个。
        /// 本测因此**从表里派生期望值**(取 `ward` / `surgery` 中索引较小者),不硬编码 `"ward"`。</para>
        /// <para>⚠️ 这条不改变 `OQ-24-7` 的裁决内容(仍是「取 `Rooms` 序首个」),
        /// 但改变读者对「声明序」的理解 —— 已在 `ClinicRoomMatcher` 的哨兵注释里加注。</para>
        /// <para><b>判别力</b>:把 `ClinicRoomMatcher` 的 `if (firstMatch == null)` 改成
        /// 无条件覆盖(`firstMatch = def.RoomType`)⇒ 取到**最后一个** ⇒ 本测红。</para></summary>
        [Test]
        public void test_multiMatch_takesFirstInRoomsOrder_fromRealTable()
        {
            // 构造一个**恰好**满足 ward 与 surgery 的布局(不含 herb_cabinet ⇒ herb_room 不成立):
            //   床 (0,0,0)/(1,0,0)/(2,0,0)         ⇒ ward(bed ≥ 2)成立
            //   手术台 (3,0,0) 占 (3,0,0)+(4,0,0)  ⇒ has_type surgery_table 成立
            //   无菌柜 (5,0,0) 在 (4,0,0) 的 +x 侧 ⇒ adjacency +x 成立
            // 且 (0..5,0,0) 连续 ⇒ 单簇(否则整盘 ROOM_NONE,EC-24-07)。
            _writer.Place(new WorldPos(0, 0, 0), ModuleBed, 0, 0, tick: 1);
            _writer.Place(new WorldPos(1, 0, 0), ModuleBed, 0, 0, tick: 2);
            _writer.Place(new WorldPos(2, 0, 0), ModuleBed, 0, 0, tick: 3);
            _writer.Place(new WorldPos(3, 0, 0), ModuleSurgeryTable, 0, 0, tick: 4); // 占 3,4
            _writer.Place(new WorldPos(5, 0, 0), ModuleSterileCabinet, 0, 0, tick: 5);

            CompositionRootServices bag = AssembleWithRealRegistry();
            ClinicRoomMatchOutcome outcome = bag.ClinicRoomMatching.Match();

            // 从**表本身**派生期望:ward 与 surgery 中 `Rooms` 索引较小者。
            IReadOnlyList<ClinicRoomDef> rooms = bag.ClinicRoomMatching.Rooms.Rooms;
            int wardIdx = -1, surgeryIdx = -1;
            for (int i = 0; i < rooms.Count; i++)
            {
                if (rooms[i].RoomType == "ward") wardIdx = i;
                if (rooms[i].RoomType == "surgery") surgeryIdx = i;
            }
            Assert.That(wardIdx, Is.GreaterThanOrEqualTo(0), "前提:真表含 ward");
            Assert.That(surgeryIdx, Is.GreaterThanOrEqualTo(0), "前提:真表含 surgery");

            string expected = wardIdx < surgeryIdx ? "ward" : "surgery";

            Assert.That(outcome.Extraction.HasRoom, Is.True, "前提:全部格子连通成一簇");
            Assert.That(outcome.Matched, Is.True);
            Assert.That(outcome.Diagnostics.MatchedRoomCount, Is.EqualTo(2),
                "本布局应**恰好**满足 ward + surgery(不是 1 个 ⇒ 测不到多匹配;不是 3 个 ⇒ herb_room 意外成立)");
            Assert.That(outcome.RoomType, Is.EqualTo(expected),
                $"多匹配 ⇒ 取 Rooms 序首个(本例 = {expected});取最后一个 / 按名字重排都会红");
        }

        // ══════════════════ 测试替身(只两件,且都不含判据)══════════════════

        /// <summary>只收事件、不落流的 `IEventSink`(`StructureWriter` 的必需形参;
        /// 本测不判事件流内容 —— 那是 23 的世界流测试面)。</summary>
        private sealed class CapturingSink : IEventSink
        {
            private readonly List<SimEvent> _sink;
            public CapturingSink(List<SimEvent> sink) { _sink = sink; }
            public void Append(in SimEvent e) { _sink.Add(e); }
        }

        /// <summary>不做任何事的占用表 —— 本测只走注册表侧(`StructureWriter` 的 C2 面
        /// 由 `structure_occupancy_wiring_test` 覆盖)。</summary>
        private sealed class NoopOccupancy : IWorldOccupancy
        {
            public void OccupyCells(IEnumerable<WorldPos> cells) { }
            public void FreeCells(IEnumerable<WorldPos> cells) { }
        }
    }
}
