// Story 005 · clinic-machine —— 24「纯函数重放与 Memoize 失效」的证据件(S20-2)。
//
// 权威来源(逐条):
//   GDD `design/gdd/clinic-machine.md`
//     · **AC-24-04** `:404` —— 「GIVEN 同一布局,WHEN 同进程两次求值(或缓存前 / 后),
//       THEN 乘子**逐位一致**(纯函数,States 节 / EC-24-05)。**测试形态 = EditMode 双求值对拍**
//       (明确**不**断言跨平台);跨平台逐位一致性**另**走 ADR-012 黄金夹具注册(字节级,
//       `golden-vN`),两者**分开**」
//     · **EC-24-05** `:291` —— 「缓存失效:以 `Structure*` 事件为失效键;失效后重导出,
//       **禁过期值混入**。缓存与否**不改结果**(AC:缓存前后逐位一致)」
//     · **States 注** `:123-127` —— 「24 没有自有状态机 —— 它是纯函数派生」;
//       「**缓存是允许的优化(= 表现层 Memoize),不是状态**」;「同一布局在会话内重放
//       (加载 / CatchUp)⇒ 乘子**逐位一致**(无内部缓存、无语境隐藏状态)」
//     · Dependencies `:149` / `:308` —— 45 → 24 的 `Structure*` 失效通知方向
//   ADR-005(确定性模拟)· ADR-016 §一(三源不变量:决策 / 派生量的全部输入 ∈
//     {事件流, 版本化烘焙数据, 二者纯函数};第四来源 = 静默不可重建通道)
//   ADR-025 §①(`Sim` 引用集恰 = {BCL, Sim.Contracts};Memoize 住 `Gameplay.Presentation`)
//   story `production/epics/clinic-machine/story-005-purity-replay-memoize.md`(2026-10-25 重写轮)
//
// ═══════════════════════════════════════════════════════════════════════════════════════
// 判据集(四条 —— 卡面重写轮订正后的口径;AC-24-10 / AC-24-11 不在本卡面,归 story-002)
// ═══════════════════════════════════════════════════════════════════════════════════════
//   ① **AC-24-04** 双求值逐位(独立实例化,**禁复用第一次的结果对象**;contexts[] 序确定)
//   ② **EC-24-05 行为半边** 布局经 23 变更(建造 / 拆除)后重算:旧条目失效、
//      新值 = 直接对新布局求值(**布局源走真 `World`** —— S20-2a 已落)
//   ③ **EC-24-05 归属半边 + 无状态** Memoize 在表现层;sim 程序集**反射扫描**断言
//      24 的类型无字段级可变状态、无 static
//   ④ **纯函数输入封闭** 求值函数签名仅收(布局快照, cooked 表句柄),不收时变量
//   ⑤ **重放等价** 「先变更后建馆」与「直接以终态布局求值」两路径输出相同(ADR-016 §一)
//
// ═══════════════════════════════════════════════════════════════════════════════════════
// ⚠️ 夹具数值纪律(**禁自指断言**)
// ═══════════════════════════════════════════════════════════════════════════════════════
// 本文件的期望值一律 = **字面量**,来源 = 作者态数据文件 + GDD 公式手算,且逐条写在用例注释里。
// **不**从被测类取常量当期望(`ClinicModifierEvaluator.EquipModCapRaw` 等一律不引)——
// 自指断言的失败模式是「变异两边一起动 ⇒ 零红逃逸」,本项目已抓多次。
//
//   来源:assets/data/item_database_furniture.json —— bed tier=1 e_env=1/16 ·
//         surgery_table tier=3 e_env=1/16 · sterile_cabinet tier=2 e_env=1/16 ·
//         chamber_pot tier=0 e_env=−1/8
//   来源:assets/data/clinicmachine_context.json —— ward(k_speed=1, base_env=0)·
//         surgery(k_speed=7/8, base_env=1/4) · herb_room(k_speed=3/4, base_env=1/8)
//   来源:assets/data/clinicmachine_adj.json —— w(surgery_table, sterile_cabinet) = 1/8
//   Q16.16 raw:1 = 65536 · 7/8 = 57344 · 3/4 = 49152 · 1/4 = 16384 · 1/8 = 8192 ·
//              1/16 = 4096 · −1/8 = −8192 · −1/4 = −16384
//
// ⚠️ **零 Fake 生产件**:`World` / `WorldGeometry` / `BuildSlotCatalog` / `StructureWriter` /
//   `StructureInstanceRegistry` / `BuildModulesCatalog` / `ClinicFurnitureCatalogue` /
//   `ClinicRoomTable` / `ClinicContextTable` / `ClinicAdjTable` 全是生产类型,
//   数据来自 `Assets/DataCooked/` 的真烘焙产物(经 `DataCookedDisk` + 生产 codec 解码)。
//   测试件只有两样:`FakeIdAuthority` / `NoPresence`(`EventStream` 的必需形参,
//   本测不判 CAP / 在场语义)。
//
// ⚠️ **门 A 不受影响**:本文件住 `Tests/EditMode`(`Sim.Contracts.Tests` 装配),
//   引用 `Gameplay.Presentation` / `Sim` / `Sim.Codec` —— 与既有 `ClinicMachine/` 测试同款。

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DaYiJingCheng.Gameplay.Presentation;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Clinic;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.ClinicMachine
{
    /// <summary>
    /// 24 的纯度 / 重放 / Memoize 失效判据(AC-24-04 · EC-24-05 · 输入封闭 · 重放等价)。
    /// </summary>
    [TestFixture]
    public class ClinicPurityReplayTest
    {
        // ══════════════════ 常量(字面量 —— 来源见文件头「夹具数值纪律」)══════════════════

        private const long OneRaw = 65536L;            // Q16.16 的 1.0
        private const long EquipCapRaw = 16384L;       // EQUIP_MOD_CAP = 1/4(GDD F-24-2 · 21a 常量)
        private const long EEnvOneSixteenthRaw = 4096L; // e_env = 1/16 —— 本目录全部正器同值(含 bed)
        private const long SurgeryBaseEnvRaw = 16384L;  // base_env(surgery) = 1/4
        private const long SurgeryAdjRaw = 8192L;       // w(surgery_table, sterile_cabinet) = 1/8

        // 真 `build_modules.cooked` 的 module_id(与 assets/data/build_modules.json 同源)
        private const int ModuleBed = 1;              // furn_type = bed,tier 1,足迹 1 格
        private const int ModuleSurgeryTable = 2;     // furn_type = surgery_table,tier 3,足迹 2 格
        private const int ModuleSterileCabinet = 3;   // furn_type = sterile_cabinet,tier 2,足迹 1 格
        private const int ModuleShell = 8;            // furn_type = null(非家具)

        /// <summary>世界边长(8×1×8 —— 与 `world_end_to_end_test` 同形:测试自建数组,不经尺寸构造入口)。</summary>
        private const int Size = 8;

        // ══════════════════ 夹具(真 World + 真烘焙产物)══════════════════

        private World _world;
        private InMemoryBlobPool _pool;
        private PayloadEncoder _encoder;
        private BuildModulesCatalog _modules;
        private ClinicFurnitureCatalogue _furniture;
        private ClinicRoomTable _rooms;
        private ClinicContextTable _contexts;
        private ClinicAdjTable _adj;
        private ClinicRoomNameTable _roomNames;
        private ClinicContextNameTable _contextNames;
        private EventStream _stream;
        private StructureWriter _writer;

        [SetUp]
        public void SetUp()
        {
            // ── 真 `WorldGeometry`(全格可走;POI / 资源网格填 -1 = 「无」)──
            var terrain = new TerrainCell[Size, 1, Size];
            for (int x = 0; x < Size; x++)
                for (int z = 0; z < Size; z++)
                    terrain[x, 0, z] = new TerrainCell(true, 0);

            int total = Size * 1 * Size;
            var poi = new int[total];
            var resource = new int[total];
            for (int i = 0; i < total; i++)
            {
                poi[i] = -1;
                resource[i] = -1;
            }

            _world = new World(new WorldGeometry(terrain, poi, resource), new BuildSlotCatalog());

            // ── 真烘焙产物(缺 ⇒ 抛 —— 不静默返回空表;E-13 同口径)──
            // ⚠️ **每份产物加非空断言**(评审 M-7):`Assets/DataCooked/*.cooked.bytes` 是
            // **文件系统外部状态** —— 数据缺失 / 漂移时本夹具 16 条会**全红**,归因粒度差
            // (看起来像「逻辑全错」,实际是「数据没烘」)。下列断言把两类失败在**归因上分开**:
            // 数据缺失 ⇒ 红在 SetUp 并直接点名缺哪份;数据正常 ⇒ 后续断言才可能是逻辑问题。
            _modules = new BuildModulesCatalog(DataCookedDisk.LoadBuildModules());
            _furniture = DataCookedDisk.LoadClinicFurniture().Catalogue;
            _rooms = DataCookedDisk.LoadClinicRoom().Table;
            _contexts = ClinicMachineCookedCodec.ReadContext(
                DataCookedDisk.ReadAll(AddressablesDataProvider.ClinicContextAddress)).Table;
            _adj = ClinicMachineCookedCodec.ReadAdj(
                DataCookedDisk.ReadAll(AddressablesDataProvider.ClinicAdjAddress)).Table;
            _roomNames = DataCookedDisk.LoadClinicRoomNames().Table;
            _contextNames = DataCookedDisk.LoadClinicContextNames().Table;

            Assert.That(_modules, Is.Not.Null, "烘焙产物缺失:BuildModules");
            Assert.That(_furniture, Is.Not.Null, "烘焙产物缺失:ClinicFurniture");
            Assert.That(_rooms, Is.Not.Null, "烘焙产物缺失:ClinicRoom");
            Assert.That(_contexts, Is.Not.Null, "烘焙产物缺失:ClinicContext");
            Assert.That(_adj, Is.Not.Null, "烘焙产物缺失:ClinicAdj");
            Assert.That(_roomNames, Is.Not.Null, "烘焙产物缺失:ClinicRoomNames");
            Assert.That(_contextNames, Is.Not.Null, "烘焙产物缺失:ClinicContextNames");
            Assert.That(_contexts.Rows.Count, Is.GreaterThan(0),
                "CONTEXT_TABLE 为空 ⇒ 后续「情境集」类断言全部失去意义(数据问题,非逻辑问题)");
            Assert.That(_rooms.Count, Is.GreaterThan(0),
                "ROOM_TABLE 为空 ⇒ 房间类型判定永不匹配(数据问题,非逻辑问题)");

            // ── 真写者 + 真事件流(occupancy 位 = 真 `World` 本身)──
            _pool = new InMemoryBlobPool();
            _encoder = new PayloadEncoder(_pool);
            _stream = new EventStream(new FakeIdAuthority(), new NoPresence());
            _writer = new StructureWriter(_stream, _world.Structures, _encoder, _modules, _world);
        }

        // ══════════════════ ① AC-24-04:双求值逐位(独立实例化)══════════════════

        /// <summary>
        /// 同一布局两次求值 —— **两次都独立实例化**(各自的视图 / 属性索引 / 求值调用),
        /// 不复用第一次的结果对象 ⇒ 输出逐位相同(AC-24-04 的「双求值对拍」形态)。
        /// <para><b>布局</b>:床 (0,0,0) + 床 (1,0,0) ⇒ `ward`(bed ≥ 2)。</para>
        /// <para><b>手算神谕</b>(GDD F-24-1 / F-24-2,全为字面量):
        /// `base_env(ward) = 0` · `Σ e_env = 2 × 1/16 = 8192` · `adj_sum = 0`(bed↔bed 无权重)
        /// ⇒ `EnvMod = 8192`;`equip_score = tier 1 × 2 = 2` ⇒ `2 × 65536 = 131072`
        /// ⇒ clamp 到 `EQUIP_MOD_CAP = 1/4 = 16384`;`K_CONTEXT_MAX = max(1, 7/8, 3/4) = 1`。</para>
        /// <para><b>判别力</b>:若求值链任一处残留跨调用状态(缓存 / 累加器 / 上次的房间类型),
        /// 第二次会与第一次分叉 ⇒ 本测红。</para>
        /// </summary>
        [Test]
        public void test_ac2404_doubleEvaluation_independentInstances_bitExact()
        {
            // Arrange:真 World 布局(两床相邻)
            _writer.Place(new WorldPos(0, 0, 0), ModuleBed, 0, 0, tick: 1);
            _writer.Place(new WorldPos(1, 0, 0), ModuleBed, 0, 0, tick: 2);

            // Act:两次求值,**各自独立建视图**(不复用第一次的任何对象)
            ClinicEnvEvaluation first = EvaluateFromFreshView();
            ClinicEnvEvaluation second = EvaluateFromFreshView();

            // Assert:三乘子逐位 + 房间标识 + 情境集全部相同
            Assert.That(second.EnvMod.Raw, Is.EqualTo(first.EnvMod.Raw), "EnvMod 双求值须逐位相同");
            Assert.That(second.EquipMod.Raw, Is.EqualTo(first.EquipMod.Raw), "EquipMod 双求值须逐位相同");
            Assert.That(second.KContextMax.Raw, Is.EqualTo(first.KContextMax.Raw), "K_CONTEXT_MAX 双求值须逐位相同");
            Assert.That(second.Extraction.RoomId, Is.EqualTo(first.Extraction.RoomId),
                "room 析出标识须相同(由规范排序格集派生,与实例报告序无关)");
            Assert.That(second.Extraction.CellCount, Is.EqualTo(first.Extraction.CellCount));
            Assert.That(second.ContextIds, Is.EqualTo(first.ContextIds), "contexts[] 序须一致");
            Assert.That(second.RoomType, Is.EqualTo(first.RoomType));

            // Assert:绝对值也对(防「两次都错成同一个值」)
            Assert.That(first.Matched, Is.True, "两床相邻 ⇒ 须匹配到房间类型");
            Assert.That(first.RoomType, Is.EqualTo("ward"), "bed ≥ 2 ⇒ ward(ROOM_TABLE 谓词)");
            Assert.That(first.EnvMod.Raw, Is.EqualTo(2 * EEnvOneSixteenthRaw),
                "EnvMod = base_env(ward)=0 + 2 × e_env(bed)=8192 + adj_sum=0 ⇒ 8192");
            Assert.That(first.EquipMod.Raw, Is.EqualTo(EquipCapRaw),
                "equip_score = 1+1 = 2 ⇒ 131072,clamp 到 EQUIP_MOD_CAP = 16384");
            Assert.That(first.KContextMax.Raw, Is.EqualTo(OneRaw), "max(k_speed) = 1 ⇒ 65536");
            Assert.That(first.ContextIds, Is.EqualTo(new[] { "ward_plain" }),
                "ward 的情境 = ward_plain(clinicmachine_context.json)");
        }

        /// <summary>
        /// 第二组双求值:非零 `adj_sum` 的布局(手术台 + 无菌柜),证明「双求值逐位」不只在
        /// 退化布局(adj = 0)上成立。
        /// <para><b>布局</b>:手术台 (2,0,0)(占 (2,0,0)+(3,0,0))+ 无菌柜 (4,0,0) ⇒ `surgery`。</para>
        /// <para><b>手算神谕</b>:`base_env(surgery) = 1/4 = 16384` ·
        /// `Σ e_env = 2 × 4096(手术台两格) + 4096(无菌柜) = 12288` ·
        /// `adj_sum = w(surgery_table, sterile_cabinet) = 1/8 = 8192`(边 = (3,0,0)→(4,0,0) 的 +x)
        /// ⇒ `EnvMod = 16384 + 12288 + 8192 = 36864`。</para>
        /// </summary>
        [Test]
        public void test_ac2404_doubleEvaluation_nonZeroAdjacency_bitExact()
        {
            // Arrange
            _writer.Place(new WorldPos(2, 0, 0), ModuleSurgeryTable, 0, 0, tick: 1); // 占 2,3
            _writer.Place(new WorldPos(4, 0, 0), ModuleSterileCabinet, 0, 0, tick: 2);

            // Act
            ClinicEnvEvaluation first = EvaluateFromFreshView();
            ClinicEnvEvaluation second = EvaluateFromFreshView();

            // Assert:逐位
            Assert.That(second.EnvMod.Raw, Is.EqualTo(first.EnvMod.Raw));
            Assert.That(second.EquipMod.Raw, Is.EqualTo(first.EquipMod.Raw));
            Assert.That(second.ContextIds, Is.EqualTo(first.ContextIds));

            // Assert:绝对值(含非零 adj_sum 项)
            Assert.That(first.RoomType, Is.EqualTo("surgery"), "手术台 + +x 侧无菌柜 ⇒ surgery");
            Assert.That(first.Extraction.CellCount, Is.EqualTo(3), "手术台 2 格 + 无菌柜 1 格");
            Assert.That(first.EnvMod.Raw,
                Is.EqualTo(SurgeryBaseEnvRaw + 3 * EEnvOneSixteenthRaw + SurgeryAdjRaw),
                "16384 + 12288 + 8192 = 36864(adj_sum 非零 ⇒ 本测覆盖 F-24-1 的邻接项)");
            Assert.That(first.EquipMod.Raw, Is.EqualTo(EquipCapRaw),
                "equip_score = 3×2 + 2×1 = 8 ⇒ 524288,clamp 到 16384");
            Assert.That(first.ContextIds, Is.EqualTo(new[] { "surgery_sterile" }));
        }

        /// <summary>
        /// 空布局的双求值:两次都出同一哨兵(`ROOM_NONE` + 中性 `(0, 0)`),`contexts[]` 为空数组
        /// 而非 null —— 卡面 QA plan 的 edge「空布局(ROOM_NONE)双求值同哨兵」。
        /// <para>⚠️ `K_CONTEXT_MAX` **不**归零:它是表派生量(F-24-3),与布局无关。</para>
        /// </summary>
        [Test]
        public void test_ac2404_doubleEvaluation_emptyLayout_sameNoneSentinel()
        {
            // Arrange:不放置任何东西(真 World 的空布局)
            // Act
            ClinicEnvEvaluation first = EvaluateFromFreshView();
            ClinicEnvEvaluation second = EvaluateFromFreshView();

            // Assert
            Assert.That(first.Extraction.RoomId, Is.EqualTo(ClinicRoomId.None), "空布局 ⇒ ROOM_NONE(EC-24-01)");
            Assert.That(first.Extraction.RoomId, Is.EqualTo(second.Extraction.RoomId));
            Assert.That(first.Matched, Is.False);
            Assert.That(first.RoomType, Is.Null);
            Assert.That(first.EnvMod.Raw, Is.EqualTo(0L), "ROOM_NONE ⇒ 中性 (0,0)");
            Assert.That(first.EquipMod.Raw, Is.EqualTo(0L));
            Assert.That(first.ContextIds, Is.Not.Null, "「可空集」= 空数组,不是 null");
            Assert.That(first.ContextIds.Count, Is.EqualTo(0));
            Assert.That(first.KContextMax.Raw, Is.EqualTo(OneRaw),
                "K_CONTEXT_MAX 是表派生量,不随布局归零(F-24-3)");
        }

        /// <summary>
        /// `contexts[]` 的序**与输入(表行序)无关**:两张行序相反的 `CONTEXT_TABLE` 对同一布局
        /// 求值,`contexts[]` 相同。
        /// <para>⚠️ **P0 判别力有限(如实登记)**:`ClinicContextTable` 的构造器拒 `room_type` 重复
        /// ⇒ P0 单房间单情境下 `contexts[]` 恰 1 元素,排序恒真。本测钉的是
        /// 「输出不依赖容器序」这一**结构性**性质(承 ADR-016 §一:判据只依赖内容),
        /// 多元素排序的判别力归 P1a(EC-24-06 / F-24-4)。</para>
        /// </summary>
        [Test]
        public void test_ac2404_contextsOrder_independentOfTableRowOrder()
        {
            // Arrange:同一布局(两床)
            _writer.Place(new WorldPos(0, 0, 0), ModuleBed, 0, 0, tick: 1);
            _writer.Place(new WorldPos(1, 0, 0), ModuleBed, 0, 0, tick: 2);

            // 两张表:行序相反(内容相同)
            var rowsForward = new List<ClinicContextRow>
            {
                new ClinicContextRow("ward", "ward_plain", new Fix(OneRaw), new Fix(0)),
                new ClinicContextRow("surgery", "surgery_sterile", new Fix(57344L), new Fix(SurgeryBaseEnvRaw)),
                new ClinicContextRow("herb_room", "herb_heavy", new Fix(49152L), new Fix(8192L)),
            };
            var rowsReverse = new List<ClinicContextRow>(rowsForward);
            rowsReverse.Reverse();
            var tableForward = new ClinicContextTable(rowsForward, new Fix(OneRaw));
            var tableReverse = new ClinicContextTable(rowsReverse, new Fix(OneRaw));

            // Act
            var view = new StructureInstanceReadView(_world.Structures, _modules);
            ClinicEnvEvaluation a = ClinicEnvironmentEvaluator.Evaluate(
                view, _furniture, _rooms, tableForward, _adj);
            ClinicEnvEvaluation b = ClinicEnvironmentEvaluator.Evaluate(
                view, _furniture, _rooms, tableReverse, _adj);

            // Assert:同内容不同行序 ⇒ 输出逐位相同
            Assert.That(b.ContextIds, Is.EqualTo(a.ContextIds), "contexts[] 不得依赖表行序");
            Assert.That(b.EnvMod.Raw, Is.EqualTo(a.EnvMod.Raw));
            Assert.That(b.RoomType, Is.EqualTo(a.RoomType));
            Assert.That(a.ContextIds, Is.EqualTo(new[] { "ward_plain" }));
        }

        // ══════════════════ ② EC-24-05 行为半边:变更后重算(真 World)══════════════════

        /// <summary>
        /// **建造后失效**:`StructurePlaced` 入流 ⇒ 缓存失效 ⇒ 重算值 = 对**新布局**直接求值
        /// (无中间态残留)。
        /// <para><b>布局演进</b>:L₁ = 两床(ward, EnvMod 8192)⇒ L₂ = L₁ + 一张床(三床,ward,
        /// EnvMod = 3 × 4096 = 12288)。</para>
        /// <para><b>判别力</b>:把 <see cref="ClinicEnvironmentMemo.Apply"/> 的失效改成 no-op
        /// ⇒ 第二次 `Get()` 返回 8192 ⇒ 本测红。</para>
        /// </summary>
        [Test]
        public void test_ec2405_afterPlace_memoInvalidatesAndEqualsDirectEvaluation()
        {
            // Arrange:L₁ = 两床
            _writer.Place(new WorldPos(0, 0, 0), ModuleBed, 0, 0, tick: 1);
            _writer.Place(new WorldPos(1, 0, 0), ModuleBed, 0, 0, tick: 2);

            var memo = NewMemo();
            ClinicEnvEvaluation l1 = memo.Get();
            Assert.That(l1.EnvMod.Raw, Is.EqualTo(2 * EEnvOneSixteenthRaw), "前提:L₁ 的 EnvMod = 8192");
            Assert.That(memo.HasCache, Is.True, "前提:缓存已建立");

            // Act:建造第三张床 —— 事件真入流,再投递给 memo
            _writer.Place(new WorldPos(2, 0, 0), ModuleBed, 0, 0, tick: 3);
            FeedLastEvent(memo);
            ClinicEnvEvaluation l2 = memo.Get();

            // Assert:失效发生
            Assert.That(memo.HasCache, Is.True, "Get 后缓存重建");

            // Assert:新值 = 直接对新布局求值(独立第二路径)
            ClinicEnvEvaluation direct = EvaluateFromFreshView();
            Assert.That(l2.EnvMod.Raw, Is.EqualTo(direct.EnvMod.Raw),
                "缓存路径与直接求值须逐位一致(EC-24-05「缓存与否不改结果」)");
            Assert.That(l2.Extraction.CellCount, Is.EqualTo(3), "三张床");
            Assert.That(l2.EnvMod.Raw, Is.EqualTo(3 * EEnvOneSixteenthRaw),
                "EnvMod = 3 × e_env(bed) = 12288;旧值 8192 若被返回 = 陈旧命中");
        }

        /// <summary>
        /// **拆除后失效**:`StructureRemoved` 入流 ⇒ 旧缓存条目失效 ⇒ 不得返回陈旧值。
        /// <para><b>布局演进</b>:L₁ = 两床(ward)⇒ 拆一张 ⇒ L₂ = 一床(无房间类型匹配 ⇒ ROOM_NONE)。
        /// ⇒ 新值必须是中性 `(0, 0)`,**不得**仍是 8192。</para>
        /// <para><b>判别力</b>:把 `IsStructureKind` 的 `StructureRemoved` 分支删掉
        /// ⇒ 拆除不失效 ⇒ 本测红(仍返回 8192)。</para>
        /// </summary>
        [Test]
        public void test_ec2405_afterRemove_staleEntryNotReturned()
        {
            // Arrange:L₁ = 两床 ⇒ ward
            _writer.Place(new WorldPos(0, 0, 0), ModuleBed, 0, 0, tick: 1);
            long second = _writer.Place(new WorldPos(1, 0, 0), ModuleBed, 0, 0, tick: 2);

            var memo = NewMemo();
            Assert.That(memo.Get().EnvMod.Raw, Is.EqualTo(2 * EEnvOneSixteenthRaw), "前提:L₁ = ward,EnvMod 8192");

            // Act:拆掉一张床
            Assert.That(_writer.Remove(second, tick: 3), Is.True, "前提:拆除须成功");
            FeedLastEvent(memo);
            ClinicEnvEvaluation after = memo.Get();

            // Assert:不得返回陈旧值
            Assert.That(after.EnvMod.Raw, Is.Not.EqualTo(2 * EEnvOneSixteenthRaw),
                "拆除后仍返回 8192 = 陈旧命中(EC-24-05「禁过期值混入」)");
            Assert.That(after.Matched, Is.False, "单床不满足任何房间类型 ⇒ ROOM_NONE");
            Assert.That(after.EnvMod.Raw, Is.EqualTo(0L), "ROOM_NONE ⇒ 中性 (0,0)");
            Assert.That(after.EnvMod.Raw, Is.EqualTo(EvaluateFromFreshView().EnvMod.Raw),
                "与直接求值逐位一致");
        }

        /// <summary>
        /// **修改后失效**:`StructureModified` 入流 ⇒ 缓存失效。
        /// <para><b>为什么需要这一条(2026-10-26 评审 W-3)</b>:`IsStructureKind` 是**三支闭集**
        /// (`StructurePlaced` / `StructureRemoved` / `StructureModified`),但评审实测原 15 条
        /// **只测了前两支** —— 从 switch 删掉 `StructureModified` 那一行,全套仍绿。
        /// 而该支有真实生产发点(`Sim/World/StructureKinds.cs:296-300` 的 `StructureWriter.Modify`),
        /// 不是死代码 ⇒ 这是一处**真实可证伪性缺口**,不是纸面遗漏。</para>
        /// <para><b>判别力设计(关键)</b>:24 的求值**不读** `Orientation` / `Variant`
        /// (`ClinicFurnitureTraitsSource` 只摊平 `module_id` + 占用格),所以 `Modify` 之后
        /// **`EnvMod` 的值不变**。⇒ 本条**不能**靠「值变了」判失效,必须直接观测
        /// <see cref="ClinicEnvironmentMemo.InvalidationCount"/>。
        /// 这恰好也把设计属性钉住了:memo 的失效是**保守的、不比较键新旧**
        /// (任何 `Structure*` 都清缓存,见 `ClinicEnvironmentMemo` 类头),
        /// 因此「值没变但仍失效」是**预期行为**,不是过度失效。</para>
        /// <para><b>判别力</b>:从 `IsStructureKind` 删掉 `case EventKind.StructureModified:`
        /// ⇒ `InvalidationCount` 停在 0 ⇒ 本测红。</para>
        /// </summary>
        [Test]
        public void test_ec2405_afterModify_invalidatesCache()
        {
            // Arrange:L₁ = 两床(ward),缓存已建立
            _writer.Place(new WorldPos(0, 0, 0), ModuleBed, 0, 0, tick: 1);
            long second = _writer.Place(new WorldPos(1, 0, 0), ModuleBed, 0, 0, tick: 2);

            var memo = NewMemo();
            ClinicEnvEvaluation l1 = memo.Get();
            Assert.That(l1.EnvMod.Raw, Is.EqualTo(2 * EEnvOneSixteenthRaw), "前提:L₁ = ward,EnvMod 8192");
            Assert.That(memo.HasCache, Is.True, "前提:缓存已建立");
            Assert.That(memo.InvalidationCount, Is.EqualTo(0), "前提:尚未投递任何 Structure* 事件");

            // Act:改第二张床的朝向 —— 真生产路径(StructureWriter.Modify 发 StructureModified)
            Assert.That(_writer.Modify(second, newOrientation: 1, newVariant: null, tick: 3), Is.True,
                "前提:Modify 须成功(朝向确实变化 ⇒ modified_fields 非 0)");
            FeedLastEvent(memo);
            ClinicEnvEvaluation l2 = memo.Get();

            // Assert 1:失效发生了(这是本测的主判据 —— 与值是否变化无关)
            Assert.That(memo.InvalidationCount, Is.EqualTo(1),
                "StructureModified 须使缓存失效;停在 0 = IsStructureKind 漏了这一支(评审 W-3)");

            // Assert 2:值不变(24 不读 Orientation / Variant —— 这是预期,不是缺陷)
            Assert.That(l2.EnvMod.Raw, Is.EqualTo(l1.EnvMod.Raw),
                "24 的求值不读朝向/变体 ⇒ 值不变是预期行为");
            Assert.That(l2.RoomType, Is.EqualTo(l1.RoomType), "房间类型不变");

            // Assert 3:重算真的跑了一次(不是「标脏但没重算」)
            Assert.That(memo.RecomputeCount, Is.EqualTo(2),
                "首次 Get + 失效后 Get = 2 次重算;若失效只标脏不重算,这里会是 1");

            // Assert 4:与直接求值逐位一致(EC-24-05「缓存与否不改结果」仍成立)
            Assert.That(l2.EnvMod.Raw, Is.EqualTo(EvaluateFromFreshView().EnvMod.Raw),
                "失效后重算值须等于直接求值");
        }

        // ══════════════════ ⑤ DTO 交付面(2026-10-26 裁定 ①-C / ②-C 的接线判据)══════════════════

        /// <summary>
        /// **Memo 缓存 DTO,且 DTO 与求值同源**(W-2 的真修):`GetDto()` 的编号来自词表,
        /// 乘子与 `Get()` 逐位相同,`Structure*` 事件同时使两者失效。
        /// <para><b>为什么需要这一条</b>:词表这条链最容易的半成品是「产物烘好了、Binder 也写了,
        /// 但**没人读它**」—— 那正是本项目反复抓过的「补烘 ≠ 接线」。本测是接线存在的证据。</para>
        /// <para><b>判别力</b>:① 把 `GetDto` 的 `_cachedDto = ...` 那行删掉 ⇒ 返回默认 DTO
        /// (RoomNameId = 0 / ContextIds = null)⇒ 本测红;② 把 `ToDto` 的
        /// `ResolveRoomNameId` 改成恒返回 0 ⇒ 本测红。</para>
        /// </summary>
        [Test]
        public void test_dto_memoCachesProjectedDto_inLockstepWithEvaluation()
        {
            // Arrange:手术台 + 无菌柜 ⇒ surgery(非零 adj,编号与乘子都不退化)
            _writer.Place(new WorldPos(2, 0, 0), ModuleSurgeryTable, 0, 0, tick: 1);
            _writer.Place(new WorldPos(4, 0, 0), ModuleSterileCabinet, 0, 0, tick: 2);

            var memo = NewMemo();
            ClinicEnvEvaluation evaluation = memo.Get();
            ClinicEnvDto dto = memo.GetDto();

            // Assert 1:编号来自词表(不是 0、不是哈希、不是行序)
            Assert.That(evaluation.RoomType, Is.EqualTo("surgery"), "前提:该布局判为 surgery");
            Assert.That(dto.RoomNameId, Is.EqualTo(2), "surgery 的词表编号 = 2(裁定 ①-C 的真源)");
            Assert.That(dto.ContextIds, Is.EqualTo(new[] { 2 }),
                "surgery_sterile 的词表编号 = 2(裁定 ②-C)");

            // Assert 2:乘子与求值逐位相同(DTO 不是二次计算)
            Assert.That(dto.EnvMod.Raw, Is.EqualTo(evaluation.EnvMod.Raw),
                "DTO 的 EnvMod 须与求值逐位相同(投影只换编号,不算数)");
            Assert.That(dto.EquipMod.Raw, Is.EqualTo(evaluation.EquipMod.Raw));

            // Assert 3:与独立投影路径一致(防「缓存里存的是旧快照」)
            ClinicEnvDto direct = ClinicEnvProjection.ToDto(
                EvaluateFromFreshView(), _roomNames, _contextNames);
            Assert.That(dto.RoomNameId, Is.EqualTo(direct.RoomNameId));
            Assert.That(dto.ContextIds, Is.EqualTo(direct.ContextIds));
            Assert.That(dto.EnvMod.Raw, Is.EqualTo(direct.EnvMod.Raw));
        }

        /// <summary>
        /// **`GetDto()` 的 `ContextIds` 是缓存内部的同一数组实例** —— 契约面钉住(评审 D3)。
        /// <para>⚠️ 为什么钉:数组是引用类型,`readonly` 只禁重绑**不禁元素写入**。
        /// 此前「既共享又没写进契约」= 最坏的一种:42 侧写一个元素就静默污染缓存,
        /// 而**没有任何测试会红**(`Is.EqualTo` 对数组是逐元素比较,别名与否都绿)。</para>
        /// <para>本条把**选型**钉死:若日后改成防御性拷贝,本测会红 —— 那时须同步订正
        /// `ClinicEnvDto.ContextIds` 与 `GetDto` 的契约注,是**有意变更**而非静默漂移。</para>
        /// </summary>
        /// <remarks><b>判别力</b>:把 `GetDto()` 命中分支改成返回深拷贝 ⇒ 本测红。</remarks>
        [Test]
        public void test_dto_contextIds_isSharedCacheInstance_contractPinned()
        {
            _writer.Place(new WorldPos(2, 0, 0), ModuleSurgeryTable, 0, 0, tick: 1);
            _writer.Place(new WorldPos(4, 0, 0), ModuleSterileCabinet, 0, 0, tick: 2);

            // 同一个 Memo 连续两次命中 ⇒ **同一实例**(这就是契约要写明的共享语义)
            var memo = NewMemo();
            ClinicEnvDto a = memo.GetDto();
            ClinicEnvDto b = memo.GetDto();
            Assert.That(a.ContextIds, Is.SameAs(b.ContextIds),
                "同一 Memo 的缓存命中须返回同一数组实例 —— 若此条红说明改成了防御性拷贝," +
                "须同步订正 ClinicEnvDto.ContextIds 与 GetDto 的契约注(有意变更,不是漂移)");

            // 两个独立 Memo 实例 ⇒ 各自独立的数组(排除「全局静态数组」这种更糟的形态)
            ClinicEnvDto other = NewMemo().GetDto();
            Assert.That(a.ContextIds, Is.Not.SameAs(other.ContextIds),
                "两个独立 Memo 实例不得共享数组实例");
        }

        /// <summary>
        /// **`Structure*` 事件同时使 DTO 失效**:拆除后 `GetDto()` 必须反映新布局
        /// (ROOM_NONE ⇒ `RoomNameId = 0`、`ContextIds` 空数组),不得返回旧编号。
        /// <para><b>判别力</b>:若 `GetDto` 自己持一份独立缓存而不随 `Apply` 失效 ⇒ 本测红
        /// (仍返回 2)。</para>
        /// </summary>
        [Test]
        public void test_dto_afterRemove_reflectsRoomNoneSentinel()
        {
            // Arrange:L₁ = 两床 ⇒ ward(编号 1)
            _writer.Place(new WorldPos(0, 0, 0), ModuleBed, 0, 0, tick: 1);
            long second = _writer.Place(new WorldPos(1, 0, 0), ModuleBed, 0, 0, tick: 2);

            var memo = NewMemo();
            Assert.That(memo.GetDto().RoomNameId, Is.EqualTo(1), "前提:L₁ = ward(编号 1)");

            // Act:拆一张 ⇒ 无房间类型匹配
            Assert.That(_writer.Remove(second, tick: 3), Is.True, "前提:拆除须成功");
            FeedLastEvent(memo);

            // Assert
            ClinicEnvDto after = memo.GetDto();
            Assert.That(after.RoomNameId, Is.EqualTo(ClinicRoomNameTable.NoneId),
                "未匹配 ⇒ ROOM_NONE 哨兵 0;停在 1 = DTO 缓存没随 Structure* 失效");
            Assert.That(after.ContextIds, Is.Not.Null, "空集须是空数组而非 null");
            Assert.That(after.ContextIds.Length, Is.EqualTo(0));
            Assert.That(after.EnvMod.Raw, Is.EqualTo(0L), "中性出口");
        }

        /// <summary>
        /// **同 tick 连两变更 ⇒ 只要求终态一致**(卡面 QA plan AC-2 的 edge)。
        /// <para>两条 `Structure*` 事件同 tick 先后投递 ⇒ 两次失效,`Get()` 只算一次,
        /// 结果 = 终态布局的直接求值。</para>
        /// </summary>
        [Test]
        public void test_ec2405_sameTickTwoChanges_terminalStateConsistent()
        {
            // Arrange:空布局起步
            var memo = NewMemo();
            Assert.That(memo.Get().Matched, Is.False, "前提:空布局 ROOM_NONE");

            // Act:同 tick(tick=5)连放两张床 —— 两条 StructurePlaced 都入流
            _writer.Place(new WorldPos(0, 0, 0), ModuleBed, 0, 0, tick: 5);
            _writer.Place(new WorldPos(1, 0, 0), ModuleBed, 0, 0, tick: 5);
            FeedAllPendingEvents(memo);
            ClinicEnvEvaluation after = memo.Get();

            // Assert:终态 = 两床 ⇒ ward
            // ⚠️ **不**断言 `InvalidationCount == 2`(评审 M-5):那是实现细节 ——
            // 若日后把「同 tick 批量失效」优化成幂等实现(只失效一次),行为完全正确却会误红。
            // 本测只判**终态一致性**(卡面 QA plan AC-2 的原话:「只要求终态一致(幂等)」)。
            Assert.That(memo.InvalidationCount, Is.GreaterThanOrEqualTo(1),
                "至少发生一次失效(0 = 缓存根本没被通知)");
            Assert.That(after.Matched, Is.True);
            Assert.That(after.RoomType, Is.EqualTo("ward"));
            Assert.That(after.EnvMod.Raw, Is.EqualTo(2 * EEnvOneSixteenthRaw));
            Assert.That(after.EnvMod.Raw, Is.EqualTo(EvaluateFromFreshView().EnvMod.Raw),
                "终态须与直接求值逐位一致(批量失效的幂等面)");
        }

        /// <summary>
        /// **缓存与否不改结果**(EC-24-05 的正面判据,AC-24-04 的「缓存前 / 后」形态):
        /// memo 命中值与「无条件丢弃缓存重算」的值逐位一致。
        /// <para><b>判别力</b>:若缓存路径与重算路径分叉(例如缓存的是 DTO 的旧快照而重算走新链)
        /// ⇒ 本测红。</para>
        /// </summary>
        [Test]
        public void test_ec2405_cachedValue_equalsUncachedRecompute_bitExact()
        {
            // Arrange:手术台 + 无菌柜(非零 adj,数值不退化)
            _writer.Place(new WorldPos(2, 0, 0), ModuleSurgeryTable, 0, 0, tick: 1);
            _writer.Place(new WorldPos(4, 0, 0), ModuleSterileCabinet, 0, 0, tick: 2);

            var memo = NewMemo();
            ClinicEnvEvaluation cached = memo.Get();
            Assert.That(memo.HasCache, Is.True);

            // Act:同一实例的第二次 Get(命中)与 Recompute(强制重算)
            ClinicEnvEvaluation hit = memo.Get();
            ClinicEnvEvaluation recomputed = memo.Recompute();

            // Assert:三条路径逐位相同
            Assert.That(memo.HitCount, Is.EqualTo(1), "第二次 Get 须命中");
            Assert.That(hit.EnvMod.Raw, Is.EqualTo(cached.EnvMod.Raw));
            Assert.That(hit.EquipMod.Raw, Is.EqualTo(cached.EquipMod.Raw));
            Assert.That(hit.ContextIds, Is.EqualTo(cached.ContextIds));
            Assert.That(recomputed.EnvMod.Raw, Is.EqualTo(cached.EnvMod.Raw),
                "缓存值 vs 重算值须逐位一致(EC-24-05)");
            Assert.That(recomputed.EquipMod.Raw, Is.EqualTo(cached.EquipMod.Raw));
            Assert.That(recomputed.Extraction.RoomId, Is.EqualTo(cached.Extraction.RoomId));
            Assert.That(cached.EnvMod.Raw,
                Is.EqualTo(SurgeryBaseEnvRaw + 3 * EEnvOneSixteenthRaw + SurgeryAdjRaw),
                "绝对值也对(36864)—— 防「缓存与重算一起错」");
        }

        /// <summary>
        /// **非 `Structure*` 事件不得触发失效**(EC-24-05 的失效键**闭集**):
        /// `ActorCellEntered` / `PoiStateChanged` 等不改布局的事件是 no-op。
        /// <para><b>为什么必须有</b>:若把「任何世界流事件」都当失效键,24 的重算频率
        /// 就被玩家移动速度 / POI 交互频率耦合 —— 那是 ADR-016 §三明令「事件率上界 = tick 频率,
        /// 与帧率 / 位移距离无关」的反面。</para>
        /// <para><b>判别力</b>:把 `IsStructureKind` 的 `default` 分支改成 `return true`
        /// ⇒ `InvalidationCount` 变 1 ⇒ 本测红。</para>
        /// </summary>
        [Test]
        public void test_ec2405_nonStructureEvents_doNotInvalidate()
        {
            // Arrange
            _writer.Place(new WorldPos(0, 0, 0), ModuleBed, 0, 0, tick: 1);
            var memo = NewMemo();
            memo.Get();
            int before = memo.InvalidationCount;

            // Act:投递一条 `ActorCellEntered`(玩家跨格,ADR-016 §三)
            var payload = _encoder.Encode(EventKind.ActorCellEntered,
                new ActorCellEnteredPayload(actorId: 7, cell: new WorldPos(3, 0, 3), tick: 2));
            memo.Apply(new SimEvent(2, PatientId.None, 0, EventKind.ActorCellEntered, payload));

            // Assert:缓存仍有效,失效计数不动
            Assert.That(memo.InvalidationCount, Is.EqualTo(before),
                "非 Structure* 事件不得失效 24 的缓存(失效键是闭集)");
            Assert.That(memo.HasCache, Is.True, "缓存须仍有效");
        }

        // ══════════════════ ③ EC-24-05 归属半边 + 无状态反射扫描 ══════════════════

        /// <summary>
        /// **Memoize 住表现层**:`ClinicEnvironmentMemo` 的装配 = `Gameplay.Presentation`;
        /// 且 `Sim` 程序集内**零**类型名含 `Memo` / `Cache`。
        /// <para><b>为什么是程序集面而非文件路径</b>:文件可以挪,程序集是编译期约束
        /// (ADR-025 §① 的清单封闭性)。</para>
        /// <para><b>判别力</b>:把 `ClinicEnvironmentMemo` 搬进 `Sim` ⇒ 本测第一条红;
        /// 在 `Sim` 里新立一个 `ClinicXxxCache` ⇒ 第二条红。</para>
        /// </summary>
        [Test]
        public void test_ec2405_memoLivesInPresentationAssembly_notInSim()
        {
            // Arrange
            Assembly memoAssembly = typeof(ClinicEnvironmentMemo).Assembly;
            Assembly simAssembly = typeof(ClinicModifierEvaluator).Assembly;

            // Assert 1:Memoize 的装配 = Gameplay.Presentation
            Assert.That(memoAssembly.GetName().Name, Is.EqualTo("Gameplay.Presentation"),
                "EC-24-05:缓存是表现层 Memoize,不得住 Sim");

            // Assert 2:Sim 程序集零 Memo/Cache 类型(扫描非空,防空集冒充绿)
            Type[] simTypes = simAssembly.GetTypes();
            Assert.That(simTypes.Length, Is.GreaterThan(0), "Sim 类型集为空 = 扫描面丢失,拒判");
            string[] offenders = simTypes
                .Where(t => t.Name.IndexOf("Memo", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            t.Name.IndexOf("Cache", StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(t => t.FullName)
                .ToArray();
            Assert.That(offenders, Is.Empty,
                "Sim 侧不得有任何缓存 / Memoize 类型(24 无自有状态);实际:" +
                string.Join(" | ", offenders));

            // Assert 3:正控制 —— 24 的求值类型确实住 Sim(证明上一条的扫描面覆盖它)
            Assert.That(simAssembly.GetName().Name, Is.EqualTo("Sim"),
                "被测求值器须住 Sim(正控制)");
            Assert.That(typeof(ClinicEnvironmentEvaluator).Namespace,
                Is.EqualTo("DaYiJingCheng.Sim.Clinic"));
        }

        /// <summary>
        /// **无状态 = 可断言**(卡面判据 3):反射扫描 `DaYiJingCheng.Sim.Clinic` 命名空间下
        /// **全部**类型 —— 零 static 可变字段、零实例可变字段。
        /// <para><b>合法豁免(逐类登记,见断言 3 的白名单)</b>:</para>
        /// <list type="bullet">
        /// <item>`readonly` 字段 = **不可重绑**(`IsInitOnly`)⇒ 不算可变状态。
        /// 卡面明写「readonly 结构体常量合法,不算状态」。</item>
        /// <item>`const`(`IsLiteral`)⇒ 编译期内联,零运行期存储。</item>
        /// <item>「表 / 快照类」(`ClinicFurnitureCatalogue` / `ClinicContextTable` /
        /// `ClinicAdjTable` / `ClinicRoomTable` / `ClinicFurnitureTraitsSource`)的 readonly
        /// 索引字段 = **版本化烘焙数据的运行期视图**(ADR-016 §一三源之一),
        /// 构造期一次填充、之后零公开写面 ⇒ 不是「可变状态」。</item>
        /// </list>
        /// <para>⚠️ **本条判的是「字段引用不可重绑」,不判「被引用对象的内部可变性」** ——
        /// 后者由断言 3 的白名单**双向差集归零**承担:任何新增的「有实例字段」的类型必须
        /// 显式登记,否则红。两个半边合起来才等于「无可变状态」。</para>
        /// <para><b>判别力</b>:给 `ClinicModifierEvaluator` 加一个
        /// `private static readonly Dictionary&lt;…&gt;` 之外的 **可变** static 字段
        /// (如 `private static int _calls;`)⇒ 断言 1 红。</para>
        /// </summary>
        [Test]
        public void test_ec2405_stateless_allClinicSimTypes_noMutableOrStaticState()
        {
            // Arrange:24 的全部 sim 类型(按命名空间取,不按文件名 —— 后者可被新文件绕过)
            Assembly sim = typeof(ClinicModifierEvaluator).Assembly;
            Type[] clinicTypes = sim.GetTypes()
                .Where(t => t.Namespace == "DaYiJingCheng.Sim.Clinic")
                .ToArray();

            Assert.That(clinicTypes.Length, Is.GreaterThanOrEqualTo(10),
                "命名空间内类型数异常偏少 = 扫描面丢失,拒判(实际 " + clinicTypes.Length + ")");

            // ── 断言 1:零 static 可变字段(非 const、非 readonly)──
            var staticMutable = new List<string>();
            foreach (Type t in clinicTypes)
            {
                foreach (FieldInfo f in t.GetFields(
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly))
                {
                    if (f.IsLiteral || f.IsInitOnly) continue;   // const / readonly ⇒ 合法
                    staticMutable.Add($"{t.Name}.{f.Name}");
                }
            }
            Assert.That(staticMutable, Is.Empty,
                "24 的 sim 类型出现 static 可变字段 = 跨调用状态(ADR-005 违例);实际:" +
                string.Join(" | ", staticMutable));

            // ── 断言 1b:`static readonly` 的**类型**必须不可变(评审 W-4)──
            // 为什么需要这一条:断言 1 把 `IsInitOnly` 一律豁免 —— 那只保证**引用不可重绑**,
            // 不保证**被引用对象的内容不可变**。`private static readonly Dictionary<K,V> _x`
            // 完全合法地通过断言 1,却是最典型的「跨调用共享可变状态」。
            // 而断言 3 只扫 `t.IsClass` 的**实例**字段 ⇒ static class 里的 readonly 容器
            // 两个断言都漏(评审支 B 实测指出的双向漏网)。
            //
            // 判据 = 字段**声明类型**必须是下列之一:
            //   ① 值类型(按值复制,结构上无法成为共享可变状态)
            //   ② 只读集合**接口**(IReadOnlyList / IReadOnlyCollection / IReadOnlyDictionary)
            //      —— 接口面零写方法 ⇒ 持有者拿不到写通道
            // 其余(裸 Dictionary / List / HashSet / 数组 / 自定义可变类)一律红。
            var staticReadonlyMutable = new List<string>();
            foreach (Type t in clinicTypes)
            {
                foreach (FieldInfo f in t.GetFields(
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly))
                {
                    if (!f.IsInitOnly || f.IsLiteral) continue;   // 只看 static readonly
                    Type ft = f.FieldType;
                    if (ft.IsValueType) continue;                 // ① 值类型 ⇒ 按值复制
                    if (IsReadOnlyCollectionInterface(ft)) continue; // ② 只读集合接口
                    staticReadonlyMutable.Add($"{t.Name}.{f.Name} : {ft.Name}");
                }
            }
            Assert.That(staticReadonlyMutable, Is.Empty,
                "`static readonly` 字段的类型必须是「值类型」或「只读集合接口」—— " +
                "裸 Dictionary/List/HashSet/数组 只锁引用、内容仍可变 = 跨调用共享状态(ADR-005)。实际:" +
                string.Join(" | ", staticReadonlyMutable));

            // ── 断言 2:零实例可变字段(非 readonly)──
            // ⚠️ 枚举除外:编译器为每个 enum 生成实例字段 `value__`(非 readonly、非 const)
            // —— 那是 CLR 的枚举表示,不是「状态」(枚举值是**值类型**,按值复制,
            // 结构上无法成为跨调用共享状态)。首轮实测在本条上误报三个 `…Kind.value__` /
            // `…Direction.value__` / `ClinicCellLookup.value__` —— 记为「枚举形态的假红」,
            // 处置 = 跳过 `IsEnum`,不改判据强度(值类型其余字段仍逐条判 readonly)。
            var instanceMutable = new List<string>();
            foreach (Type t in clinicTypes)
            {
                if (t.IsEnum) continue;
                foreach (FieldInfo f in t.GetFields(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly))
                {
                    if (f.IsInitOnly) continue;                  // readonly ⇒ 合法
                    instanceMutable.Add($"{t.Name}.{f.Name}");
                }
            }
            Assert.That(instanceMutable, Is.Empty,
                "24 的 sim 类型出现实例可变字段 = 隐藏状态(AC-24-04 违例);实际:" +
                string.Join(" | ", instanceMutable));

            // ── 断言 3:「带实例字段的**引用类型**」恰 = 白名单(双向差集归零)──
            // 为什么只扫引用类型:值类型(结构体 / 枚举)按值复制,**结构上**无法成为
            // 跨调用共享的可变状态 —— 它们即便有字段也伤不到无状态性(断言 1 / 2 已保证
            // 它们的字段是 readonly)。真正能承载共享状态的是类实例 ⇒ 类的字段面必须登记。
            // 白名单每条都给理由;新增类带实例字段必须在此登记,否则红。
            string[] classesWithInstanceFields = clinicTypes
                .Where(t => t.IsClass &&
                            t.GetFields(BindingFlags.Instance | BindingFlags.Public |
                                        BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Length > 0)
                .Select(t => t.Name)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToArray();

            string[] whitelist =
            {
                "ClinicAdjTable",             // 表视图:readonly 索引(构造期填充,零公开写面)
                "ClinicContextNameTable",     // 表视图:情境词表(裁定 ②-C),同上
                "ClinicContextTable",         // 表视图:同上
                "ClinicFurnitureCatalogue",   // 表视图:同上(21a 家具目录的运行期只读形态)
                "ClinicFurnitureTraitsSource",// 布局快照索引(构造期摊平,布局变更须重建)
                "ClinicNameTableCore",        // 表视图:两张词表的双向查找核(internal,同构造期填充)
                "ClinicRoomNameTable",        // 表视图:房间名词表(裁定 ①-C),同上
                "ClinicRoomTable",            // 表视图:同上
            };
            Assert.That(classesWithInstanceFields, Is.EqualTo(whitelist),
                "「带实例字段的 Sim.Clinic 引用类型」集合须恰等于已登记白名单(双向差集归零)—— " +
                "新增类带字段必须显式登记并说明其为「构造期填充的只读表 / 快照」");
        }

        /// <summary>该类型是否为「只读集合接口」—— 断言 1b 的合法白名单面。
        /// <para>只认 `IReadOnly*` 三族:它们**接口面零写方法**,持有者拿不到写通道
        /// (即便运行期实例是 `List&lt;T&gt;`,静态类型决定了调用点能做什么)。</para>
        /// </summary>
        private static bool IsReadOnlyCollectionInterface(Type t)
        {
            if (!t.IsInterface) return false;
            if (!t.IsGenericType) return false;
            Type def = t.GetGenericTypeDefinition();
            return def == typeof(IReadOnlyList<>) ||
                   def == typeof(IReadOnlyCollection<>) ||
                   def == typeof(IReadOnlyDictionary<,>);
        }

        // ══════════════════ ④ 纯函数输入封闭(三源,ADR-016 §一)══════════════════

        /// <summary>
        /// **求值函数签名仅收(布局快照, cooked 表句柄)** —— 类型面断言,不是口头约定。
        /// <para><b>白名单(恰 5 件)</b>:`IPlacedStructureReadView`(布局快照)+
        /// `ClinicFurnitureCatalogue` / `ClinicRoomTable` / `ClinicContextTable` / `ClinicAdjTable`
        /// (四份版本化烘焙表)。</para>
        /// <para><b>黑名单(禁出现)</b>:`long` / `int` / `SimEvent` / `EventOrderKey` /
        /// `DateTime` / `DateTimeOffset` / `TimeSpan` / `float` / `double` / `Random` ——
        /// 任一出现 = 引入了第四来源(墙钟 / tick 频率 / 随机),破坏 ADR-016 §一 三源不变量。</para>
        /// <para><b>判别力</b>:给 `Evaluate` 加一个 `long tick` 形参(哪怕不读它)
        /// ⇒ 本测红。这正是「24 不感知 tick 频率」(卡面 §Implementation Notes 5)的机械化。</para>
        /// </summary>
        [Test]
        public void test_inputClosure_evaluateSignature_takesOnlyLayoutAndTables()
        {
            // Arrange
            MethodInfo evaluate = typeof(ClinicEnvironmentEvaluator)
                .GetMethod("Evaluate", BindingFlags.Public | BindingFlags.Static);
            Assert.That(evaluate, Is.Not.Null, "求值入口须存在且为 public static(纯函数形态)");
            Assert.That(evaluate.ReturnType, Is.EqualTo(typeof(ClinicEnvEvaluation)));

            // Act
            ParameterInfo[] ps = evaluate.GetParameters();
            Type[] types = ps.Select(p => p.ParameterType).ToArray();

            // Assert 1:恰 5 件
            Assert.That(ps.Length, Is.EqualTo(5),
                "求值签名须恰 5 件(布局 + 四表);实际:" +
                string.Join(", ", ps.Select(p => p.ParameterType.Name)));

            // Assert 2:白名单(集合相等 —— 双向差集归零)
            Type[] whitelist =
            {
                typeof(IPlacedStructureReadView),
                typeof(ClinicFurnitureCatalogue),
                typeof(ClinicRoomTable),
                typeof(ClinicContextTable),
                typeof(ClinicAdjTable),
            };
            Assert.That(types.OrderBy(t => t.Name, StringComparer.Ordinal).ToArray(),
                Is.EqualTo(whitelist.OrderBy(t => t.Name, StringComparer.Ordinal).ToArray()),
                "求值签名须恰 = {布局快照, 四份 cooked 表}");

            // Assert 3:黑名单 —— 时变量 / 随机源一律不得出现
            // ⚠️ **只列「语义上是第四来源」的类型**(评审 M-6):`long` / `int` / `decimal` 等
            // **合法值类型**已从本表移除 —— 它们是「布局快照 / 表的句柄」可能的承载类型,
            // 靠类型名封它们属于「按名字而非语义判」,且白名单(断言 2)已做集合相等
            // ⇒ 任何新增形参都会红,黑名单本身无逃逸面可堵。保留的这几项则是**无歧义**的
            // 第四来源:事件 / 事件序键 / 墙钟 / 随机源。
            Type[] forbidden =
            {
                typeof(SimEvent), typeof(EventOrderKey),
                typeof(DateTime), typeof(DateTimeOffset), typeof(TimeSpan),
                typeof(Random),
                typeof(float), typeof(double),   // 浮点 = ADR-006 定点域边界外(非「值类型合法」)
            };
            foreach (Type bad in forbidden)
                Assert.That(types, Has.No.Member(bad),
                    $"求值签名出现时变量 / 随机源类型「{bad.Name}」—— 24 的输入须封闭于" +
                    "「布局快照 + 版本化烘焙表」(ADR-016 §一 三源不变量)");
        }

        /// <summary>
        /// 求值器本身是 **static class**(无实例 ⇒ 无实例状态)——
        /// 与「签名封闭」互补的另一半:即便签名干净,若求值器是带字段的实例类,
        /// 状态仍可藏在实例里。
        /// </summary>
        [Test]
        public void test_inputClosure_evaluatorIsStaticClass_noInstanceState()
        {
            // Arrange
            Type t = typeof(ClinicEnvironmentEvaluator);

            // Assert
            Assert.That(t.IsAbstract && t.IsSealed, Is.True,
                "求值器须为 static class(C# 中 static class = abstract + sealed)");
            Assert.That(t.GetFields(BindingFlags.Instance | BindingFlags.Public |
                                    BindingFlags.NonPublic | BindingFlags.DeclaredOnly),
                Is.Empty, "static class 不得有实例字段");
            Assert.That(t.GetFields(BindingFlags.Static | BindingFlags.Public |
                                    BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                         .Where(f => !f.IsLiteral && !f.IsInitOnly).ToArray(),
                Is.Empty, "求值器不得有 static 可变字段");
        }

        // ══════════════════ ⑤ 重放等价(派生态可重建,ADR-016 §一)══════════════════

        /// <summary>
        /// **「先变更后建馆」与「直接以终态布局求值」两路径输出相同**。
        /// <para>路径 A(重放):空医馆起步,逐条建造并逐条投递事件给 memo,最终 `Get()`。</para>
        /// <para>路径 B(直达):另起一个同终态布局的 `World`,新建 memo,**不投递任何事件**,
        /// 直接 `Get()`。</para>
        /// <para><b>为什么这条是 ADR-016 §一 的判据</b>:24 的输出是**派生态**(不进流、
        /// 不存档、重放期重建)。若两路径分叉,说明输出依赖了「到达顺序 / 中间态」这个
        /// 第四来源 —— 重放就不成立。</para>
        /// <para><b>判别力</b>:让 memo 的失效改成「在旧值上增量更新」⇒ 路径 A 会带上
        /// 中间态残留 ⇒ 本测红。</para>
        /// </summary>
        [Test]
        public void test_replayEquivalence_incrementalPath_equalsDirectTerminalLayout()
        {
            // ── 路径 A:逐条建造 + 逐条投递(重放形)──
            var memo = NewMemo();
            memo.Get();                                                        // 空医馆
            _writer.Place(new WorldPos(2, 0, 0), ModuleSurgeryTable, 0, 0, tick: 1);
            FeedLastEvent(memo); memo.Get();
            _writer.Place(new WorldPos(4, 0, 0), ModuleSterileCabinet, 0, 0, tick: 2);
            FeedLastEvent(memo);
            ClinicEnvEvaluation replayed = memo.Get();

            // ── 路径 B:另起同终态布局的 World,直接求值(零事件投递)──
            World terminal = NewEmptyWorld();
            var terminalWriter = new StructureWriter(
                new NullSink(), terminal.Structures, _encoder, _modules, terminal);
            terminalWriter.Place(new WorldPos(2, 0, 0), ModuleSurgeryTable, 0, 0, tick: 1);
            terminalWriter.Place(new WorldPos(4, 0, 0), ModuleSterileCabinet, 0, 0, tick: 2);

            var directMemo = new ClinicEnvironmentMemo(
                terminal.Structures, _modules, _furniture, _rooms, _contexts, _adj,
                _roomNames, _contextNames);
            ClinicEnvEvaluation direct = directMemo.Get();

            // Assert:两路径逐位相同
            Assert.That(replayed.EnvMod.Raw, Is.EqualTo(direct.EnvMod.Raw),
                "重放路径与直达路径的 EnvMod 须逐位相同(派生态可重建)");
            Assert.That(replayed.EquipMod.Raw, Is.EqualTo(direct.EquipMod.Raw));
            Assert.That(replayed.KContextMax.Raw, Is.EqualTo(direct.KContextMax.Raw));
            Assert.That(replayed.RoomType, Is.EqualTo(direct.RoomType));
            Assert.That(replayed.ContextIds, Is.EqualTo(direct.ContextIds));
            Assert.That(replayed.Extraction.RoomId, Is.EqualTo(direct.Extraction.RoomId),
                "room id 由布局派生 ⇒ 两路径须同 id");

            // Assert:绝对值(防两路径一起错)
            Assert.That(replayed.RoomType, Is.EqualTo("surgery"));
            Assert.That(replayed.EnvMod.Raw,
                Is.EqualTo(SurgeryBaseEnvRaw + 3 * EEnvOneSixteenthRaw + SurgeryAdjRaw));
            Assert.That(replayed.EnvMod.Raw, Is.EqualTo(36864L));
        }

        /// <summary>
        /// **重放等价的另一半:含拆除的路径**。建造 → 拆除 → 再建造的净终态,
        /// 与「直接构造该终态布局」输出相同。
        /// <para>这一条抓的是「拆除残留」—— `FreeCells` 只清一次 / 实例表留残影 / 缓存留旧条目
        /// 都会让两路径分叉。</para>
        /// </summary>
        [Test]
        public void test_replayEquivalence_withRemove_equalsDirectTerminalLayout()
        {
            // ── 路径 A:床(0) + 床(1) + 外壳(2) ⇒ 拆外壳 ⇒ 终态 = 两床 ──
            var memo = NewMemo();
            _writer.Place(new WorldPos(0, 0, 0), ModuleBed, 0, 0, tick: 1);
            FeedLastEvent(memo); memo.Get();
            _writer.Place(new WorldPos(1, 0, 0), ModuleBed, 0, 0, tick: 2);
            FeedLastEvent(memo); memo.Get();
            long shell = _writer.Place(new WorldPos(2, 0, 0), ModuleShell, 0, 0, tick: 3);
            FeedLastEvent(memo); memo.Get();
            Assert.That(_writer.Remove(shell, tick: 4), Is.True);
            FeedLastEvent(memo);
            ClinicEnvEvaluation replayed = memo.Get();

            // ── 路径 B:直接两床 ──
            World terminal = NewEmptyWorld();
            var terminalWriter = new StructureWriter(
                new NullSink(), terminal.Structures, _encoder, _modules, terminal);
            terminalWriter.Place(new WorldPos(0, 0, 0), ModuleBed, 0, 0, tick: 1);
            terminalWriter.Place(new WorldPos(1, 0, 0), ModuleBed, 0, 0, tick: 2);

            ClinicEnvEvaluation direct = new ClinicEnvironmentMemo(
                terminal.Structures, _modules, _furniture, _rooms, _contexts, _adj,
                _roomNames, _contextNames).Get();

            // Assert
            Assert.That(replayed.EnvMod.Raw, Is.EqualTo(direct.EnvMod.Raw),
                "含拆除的重放路径须与直达路径逐位相同(拆除残留会让两者分叉)");
            Assert.That(replayed.Extraction.CellCount, Is.EqualTo(2), "终态 = 两床(外壳已拆)");
            Assert.That(replayed.RoomType, Is.EqualTo("ward"));
            Assert.That(replayed.EnvMod.Raw, Is.EqualTo(2 * EEnvOneSixteenthRaw));
        }

        // ══════════════════ 辅助 ══════════════════

        /// <summary>用**全新视图**求值一次(不复用任何既有对象 —— 双求值对拍的前提)。</summary>
        private ClinicEnvEvaluation EvaluateFromFreshView()
        {
            var view = new StructureInstanceReadView(_world.Structures, _modules);
            return ClinicEnvironmentEvaluator.Evaluate(view, _furniture, _rooms, _contexts, _adj);
        }

        /// <summary>新建 memo(接本 fixture 的真 World 与四份真表)。</summary>
        private ClinicEnvironmentMemo NewMemo()
            => new ClinicEnvironmentMemo(
                _world.Structures, _modules, _furniture, _rooms, _contexts, _adj,
                _roomNames, _contextNames);

        /// <summary>把**最后一条**入流事件投递给 memo(单条变更的常规投递形)。</summary>
        private void FeedLastEvent(ClinicEnvironmentMemo memo)
            => memo.Apply(_stream.Events[_stream.Events.Count - 1]);

        /// <summary>把**尚未投递过**的事件全部投递给 memo(批量变更 / 同 tick 多事件)。
        /// <para>⚠️ 计数基线取自 memo 自己的 `InvalidationCount` —— 本测不另立游标状态。</para>
        /// </summary>
        private void FeedAllPendingEvents(ClinicEnvironmentMemo memo)
        {
            int start = memo.InvalidationCount;
            for (int i = start; i < _stream.Events.Count; i++)
                memo.Apply(_stream.Events[i]);
        }

        /// <summary>另起一个同尺寸的空 `World`(重放等价的路径 B 用)。</summary>
        private static World NewEmptyWorld()
        {
            var terrain = new TerrainCell[Size, 1, Size];
            for (int x = 0; x < Size; x++)
                for (int z = 0; z < Size; z++)
                    terrain[x, 0, z] = new TerrainCell(true, 0);

            int total = Size * 1 * Size;
            var poi = new int[total];
            var resource = new int[total];
            for (int i = 0; i < total; i++)
            {
                poi[i] = -1;
                resource[i] = -1;
            }
            return new World(new WorldGeometry(terrain, poi, resource), new BuildSlotCatalog());
        }

        /// <summary>`EventStream` 的必需形参;本测不发放号(只用 `NextItemInstanceId` 之外的路径)。</summary>
        private sealed class FakeIdAuthority : IIdAuthority
        {
            private int _patient;
            private long _item;
            public PatientId NextPatientId() => new PatientId(_patient++);
            public ItemInstanceId NextItemInstanceId() => new ItemInstanceId(_item++);
        }

        /// <summary>`EventStream` 的必需形参;本测的 `Structure*` 事件全带 `PatientId.None`
        /// ⇒ 不进 CAP 分支(见 `EventStream.AppendCore` 的 O-5 注)。</summary>
        private sealed class NoPresence : IPresenceQuery
        {
            public bool IsPresent(PatientId patientId) => false;
            public int PresentCount => 0;
            public bool IsPresentAt(WorldPos cell) => false;
            public IReadOnlyCollection<int> PresentPatientIds() => Array.Empty<int>();
        }

        /// <summary>丢弃事件的 `IEventSink`(路径 B 不判事件流内容)。</summary>
        private sealed class NullSink : IEventSink
        {
            public void Append(in SimEvent e) { }
        }
    }
}
