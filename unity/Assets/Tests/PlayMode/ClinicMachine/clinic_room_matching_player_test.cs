// 24 匹配链的 **Player 载体**(IL2CPP / Mono 双腿都能进)。
//
// 为什么必须有这份:
//   `Tests/EditMode/ClinicMachine/clinic_room_matching_e2e_test.cs` 已把链的**语义**钉死,
//   但 EditMode 装配 (`Sim.Contracts.Tests`) 的 `includePlatforms: ["Editor"]`
//   ⇒ 它**物理上进不了 player** ⇒ 整条链在 IL2CPP 腿上**零覆盖**(ADR-012 的三格矩阵
//   对 24 是空的)。
//   PlayMode 装配 (`Gameplay.Tests`) 的 `includePlatforms: []` ⇒ 能进 player。
//   本文件把 e2e 的**核心断言**在 PlayMode 装配里重述一遍,使 IL2CPP 腿有可跑载体。
//
// ⚠️ **本文件不是 e2e 的复制品** —— 它只带**跨平台敏感面**的断言:
//   · 字符串序数比较(`StringComparison.Ordinal` / `CompareOrdinal` 排序)在 IL2CPP 下的行为;
//   · 整数集合运算 / 字典桶序(析出 + 匹配都靠它);
//   · cooked 字节解码(BinaryReader 的 LE 读取);
//   · `Dictionary` 枚举序 → 规范排序的吸收。
//   逐条 AC 的细粒度反例(`+x` / `−x` 方向、多匹配的 2 vs 3、拆除后重算)仍归 EditMode 那份
//   —— 它们不依赖平台,**重复一遍只会把「两处都要改」变成新的维护面**。
//
// ⚠️ **数据来源 = 真烘焙产物,两条路**(2026-10-21 订正 —— 见下方「首跑翻车记录」)。
//   · **编辑器内**:`CompositionRoot.Assemble()` 不传 `dataProvider` ⇒ 走 `DiagnosisCookedDisk`
//     / `DataCookedDisk` 直读 `Assets/DataCooked/`(15+ 既有调用方的同款路径)。
//   · **player 内**:`Assets/DataCooked/` **不在** player 里(`Application.dataPath` 指向
//     `<build>_Data`,而 DataCooked 不是 Addressable 条目)**必须**传一个真
//     `AddressablesDataProvider` —— player 的 `StreamingAssets/aa/` 带着构建期 catalog,
//     `build_modules.cooked.bytes` 就在 `data-core` bundle 里(2026-10-21 实测:
//     `Library/com.unity.addressables/aa/Linux/StandaloneLinux64/data-core_assets_all_*.bundle`)。
//
// ⚠️ **首跑翻车记录(2026-10-21,如实登记)**:
//   首版文件头断言「不传 `structures` ⇒ 不触碰任何 cooked 产物」—— **该断言是假的**。
//   `CompositionRoot.AssembleCore` 在 `dataProvider == null` 时**无条件**装载
//   `diagnosis_signs` / `diagnosis_readfloor` / `judgment_lexicon` 等表(与 `structures` 无关)。
//   ⇒ IL2CPP player 首跑 `test_playerAssembly_assembleWithoutStructures_matchingIsNull` **红**:
//   `[E-13][disk] 烘焙产物不存在:.../PlayerWithTests_Data/DataCooked/diagnosis_signs.cooked.bytes`。
//   **这不是产品缺陷,是本文件的夹具设计错误** —— 已改为「player 里显式传 Addressables provider」。
//   ⚠️ 该红**只在 IL2CPP 腿暴露**,编辑器内一直绿 ⇒ 这正是「真跑」的价值。
//
// `[UnityPlatform]` 区分不了「在 player 里」与「在编辑器里」,故用
// `Application.isEditor` 在**运行期**分流。

using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Presentation;
using DaYiJingCheng.Sim.Clinic;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;
using NUnit.Framework;
using UnityEngine;

namespace DaYiJingCheng.Tests.PlayMode.ClinicMachine
{
    /// <summary>
    /// 24 匹配链的 player 可跑载体。
    /// <para>⚠️ 本文件在 **player** 里只跑「不依赖 cooked 产物」的第一段;第二段在 player 里
    /// 记 `Inconclusive`(见文件头「诚实边界」)。**禁**把第二段的 Inconclusive 读成「通过」。</para>
    /// </summary>
    [TestFixture]
    public class ClinicRoomMatchingPlayerTest
    {
        private const int ModuleBed = 1;
        private const int ModuleSurgeryTable = 2;
        private const int ModuleSterileCabinet = 3;

        private static bool InPlayer => !Application.isEditor;

        // ══════════════════ 第一段:不依赖 cooked 产物(player 里必过)══════════════════

        /// <summary>四个生产类型在本装配里**可达**(编译期证据 —— 若 asmdef 引用断了,本测编不过)。
        /// <para>本测的真正价值不在断言,而在**编译**:`Gameplay.Tests` 若能编译并跑到这里,
        /// 就证明 `ClinicRoomMatchingService` / `StructureInstanceReadView` /
        /// `BuildModulesCatalog` / `DataCookedDisk` 四个新件在 **player 目标**下可链接。</para></summary>
        [Test]
        public void test_playerAssembly_canReferenceMatchingChainTypes()
        {
            Assert.That(typeof(ClinicRoomMatchingService).FullName,
                Is.EqualTo("DaYiJingCheng.Gameplay.Presentation.ClinicRoomMatchingService"));
            Assert.That(typeof(StructureInstanceReadView).FullName, Is.Not.Null);
            Assert.That(typeof(BuildModulesCatalog).FullName, Is.Not.Null);
            Assert.That(typeof(DataCookedDisk).FullName, Is.Not.Null);
            Assert.That(typeof(ClinicRoomMatchOutcome).IsValueType, Is.True,
                "Outcome 是 struct(零堆分配 —— 匹配每 tick 可能被调,不得引入 GC)");
        }

        /// <summary>装配签名可用:`Assemble(structures: null)` ⇒ `ClinicRoomMatching` 为 null
        /// (**未接线**的可观测形态)。
        /// <para>⚠️ **本测必须显式传 provider**(2026-10-21 订正):`AssembleCore` 在
        /// `dataProvider == null` 时**无条件**从 `Assets/DataCooked/` 直读若干表(与
        /// `structures` 无关)⇒ player 里没有该目录 ⇒ 首跑红(`[E-13][disk]`)。
        /// player 里传真 `AddressablesDataProvider`(走 `StreamingAssets/aa/` 的构建期 catalog);
        /// 编辑器里**照旧不传**(走磁盘兜底,与 15+ 既有调用方同款)。</para>
        /// <para><b>判别力</b>:把 `CompositionRoot` 的 `if (structures != null)` 改成无条件装配
        /// ⇒ `ClinicRoomMatching` 非 null ⇒ 本测红。</para></summary>
        [Test]
        public void test_playerAssembly_assembleWithoutStructures_matchingIsNull()
        {
            CompositionRootServices bag = InPlayer
                ? CompositionRoot.Assemble(dataProvider: new AddressablesDataProvider())
                : CompositionRoot.Assemble();

            Assert.That(bag, Is.Not.Null);
            Assert.That(bag.ClinicRoomMatching, Is.Null,
                "未注入 23 注册表 ⇒ 匹配服务为 null(接线状态可观测)");
        }

        // ══════════════════ 第二段:依赖 cooked 产物(player 里也真跑)══════════════════

        /// <summary>空注册表 + 真表 ⇒ ROOM_NONE。</summary>
        [Test]
        public void test_emptyRegistry_roomNone()
        {
            var registry = new StructureInstanceRegistry();
            CompositionRootServices bag = AssembleWith(registry);
            Assert.That(bag.ClinicRoomMatching, Is.Not.Null);

            ClinicRoomMatchOutcome outcome = bag.ClinicRoomMatching.Match();
            Assert.That(outcome.Extraction.HasRoom, Is.False, "空布局 ⇒ 未析出房间");
            Assert.That(outcome.Matched, Is.False);
            Assert.That(outcome.RoomType, Is.Null);
        }

        /// <summary>两床 ⇒ ward(跨平台敏感面:序数比较 + 字典桶序 + 规范排序)。</summary>
        [Test]
        public void test_twoBeds_matchWard()
        {
            var registry = new StructureInstanceRegistry();
            var writer = NewWriter(registry);

            writer.Place(new WorldPos(0, 0, 0), ModuleBed, 0, 0, tick: 1);
            writer.Place(new WorldPos(1, 0, 0), ModuleBed, 0, 0, tick: 2);

            ClinicRoomMatchOutcome outcome = AssembleWith(registry).ClinicRoomMatching.Match();

            Assert.That(outcome.Extraction.HasRoom, Is.True);
            Assert.That(outcome.RoomType, Is.EqualTo("ward"),
                "序数比较 / 字典桶序 / 规范排序三者任一的跨平台差异都会让这里红");
        }

        /// <summary>手术台(2 格足迹)+ 无菌柜 ⇒ surgery。</summary>
        /// <para>这条同时钉「多格足迹经视图完整传出」—— 视图若只报锚点格,IL2CPP 与 Mono 都会少一格。</para>
        [Test]
        public void test_surgeryWithCabinet_matchSurgery()
        {
            var registry = new StructureInstanceRegistry();
            var writer = NewWriter(registry);

            writer.Place(new WorldPos(2, 0, 0), ModuleSurgeryTable, 0, 0, tick: 1);
            writer.Place(new WorldPos(4, 0, 0), ModuleSterileCabinet, 0, 0, tick: 2);

            ClinicRoomMatchOutcome outcome = AssembleWith(registry).ClinicRoomMatching.Match();

            Assert.That(outcome.Extraction.CellCount, Is.EqualTo(3), "手术台 2 格 + 柜 1 格");
            Assert.That(outcome.RoomType, Is.EqualTo("surgery"));
        }

        // ══════════════════ 装配 / 夹具(编辑器 ↔ player 同一判据,仅数据源不同)══════════════════

        /// <summary>编辑器 ⇒ 磁盘兜底(不传 provider);player ⇒ 真 Addressables。
        /// <para>⚠️ **两条路读同一份 baked 字节 + 同一 codec** ⇒ 无第二真源(承 `CompositionRoot`
        /// 的装载策略注释)。本方法把「哪条路」收在一处,使三条测**在两种宿主里跑同一判据**。</para></summary>
        private static CompositionRootServices AssembleWith(StructureInstanceRegistry registry)
            => InPlayer
                ? CompositionRoot.Assemble(structures: registry, dataProvider: new AddressablesDataProvider())
                : CompositionRoot.Assemble(structures: registry);

        /// <summary>真写者:真模块目录 + 真注册表。</summary>
        private static StructureWriter NewWriter(StructureInstanceRegistry registry)
        {
            BuildModulesCatalog catalogue = InPlayer
                ? new BuildModulesCatalog(new AddressablesDataProvider().Load<BuildModulesDataSet>())
                : new BuildModulesCatalog(DataCookedDisk.LoadBuildModules());

            return new StructureWriter(new NullSink(), registry, new PayloadEncoder(new InMemoryBlobPool()),
                                       catalogue, new NoopOccupancy());
        }

        // ══════════════════ 测试替身(无判据)══════════════════

        private sealed class NullSink : IEventSink
        {
            public void Append(in SimEvent e) { }
        }

        private sealed class NoopOccupancy : IWorldOccupancy
        {
            public void OccupyCells(IEnumerable<WorldPos> cells) { }
            public void FreeCells(IEnumerable<WorldPos> cells) { }
        }
    }
}
