// 24「布局 → 房间类型」匹配链的**生产装配面**(`TD-building-01` 读侧的接线落点)。
//
// 权威来源:
//   GDD `design/gdd/clinic-machine.md` 规则二(房间类型判定)· AC-24-03(ROOM_NONE 基例)
//   `Sim/Clinic/ClinicRoomExtraction.cs`(析出纯函数,两个重载)
//   `Sim/Clinic/ClinicRoomMatcher.cs`(子句求值纯函数,`OQ-24-7` 已裁 = 声明序首个匹配)
//   `Sim/Clinic/ClinicFurnitureTraitsSource.cs`(`type(c)` 的生产实现)
//   `Sim/Clinic/IPlacedStructureReadView.cs`(23 的只读视图契约)
//   `TD-building-01`(docs/tech-debt-register.md:2097)「解除动作 · 读侧」逐字:
//     「给 `IPlacedStructureReadView` 一个生产实现(**由 `World.Structures` 包一层**)
//      + 在装配根注入 `IModuleCatalog` 的生产实现(读 `build_modules.cooked`)」
//
// ⚠️ **本文件为什么住在 `Gameplay.Presentation`**:
//   链上五个零件里有**两个**只能住装配侧 —— `StructureInstanceReadView` 与
//   `BuildModulesCatalog`(二者都要读 cooked 字节 / 目录,`Sim` 引用集恰 = {BCL, Sim.Contracts},
//   ADR-025 §①)。⇒ 把链的**胶水**放在与它们同侧,`Sim` 侧保持纯逻辑(零文件依赖)。
//   本类**不含任何判定逻辑** —— 判据全部在 `Sim/Clinic/` 的两个纯函数里,本类只做
//   「把 23 的注册表 + 两张烘焙表接成那三个入参」。
//
// ⚠️ **无缓存 = 刻意的**:每次 `Match()` 都从**当下**的注册表重建视图与属性索引。
//   理由:`StructureInstanceReadView` / `ClinicFurnitureTraitsSource` 都是**快照**语义
//   (构造期摊平),而建造 / 拆除会改注册表 ⇒ 持有快照实例就等于持有**陈旧缓存**。
//   重算成本 = 一次 O(|实例| × |足迹|) 摊平,匹配本身也是同量级;为它引入失效机制
//   (AC-24-11)属 story-005 的面,本件**不提前造接缝**(承 story-006 实施约束 ⑥)。
//
// ⚠️ **本类不产生 `ClinicEnvDto`** —— 那是 story-004 的面。本类只回答「room_type 是谁」。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Clinic;
using DaYiJingCheng.Sim.World;

namespace DaYiJingCheng.Gameplay.Presentation
{
    /// <summary>一次「布局 → 房间类型」求值的完整结果(析出 + 匹配 + 诊断)。</summary>
    /// <remarks>
    /// <para>⚠️ <b>三段结果同时给出</b>,因为它们在诊断上是**不同的事**:
    /// <see cref="Extraction"/> 回答「有没有析出房间」,<see cref="Matched"/> /
    /// <see cref="RoomType"/> 回答「析出的房间是哪个类型」,<see cref="Diagnostics"/>
    /// 回答「为什么是这个类型 / 为什么没有」。</para>
    /// <para>⚠️ **未析出房间时 <see cref="Diagnostics"/> 恒为默认值(全零)** —— 这是
    /// `ClinicRoomMatcher` 的 `HasRoom == false` 短路契约,不是本类丢弃了信息。</para>
    /// </remarks>
    public readonly struct ClinicRoomMatchOutcome
    {
        /// <summary>析出结果(<see cref="ClinicRoomExtractionResult.HasRoom"/> = 是否成房间)。</summary>
        public readonly ClinicRoomExtractionResult Extraction;

        /// <summary>是否匹配到房间类型(= <c>TryMatchRoom</c> 的返回值)。</summary>
        public readonly bool Matched;

        /// <summary>匹配到的房间类型 id;`ROOM_NONE` ⇒ <c>null</c>。</summary>
        public readonly string RoomType;

        /// <summary>匹配诊断(多匹配个数 / 未知子句类型引用条数)。</summary>
        public readonly ClinicRoomMatchDiagnostics Diagnostics;

        /// <summary>构造一份结果。</summary>
        public ClinicRoomMatchOutcome(in ClinicRoomExtractionResult extraction, bool matched,
                                      string roomType, in ClinicRoomMatchDiagnostics diagnostics)
        {
            Extraction = extraction;
            Matched = matched;
            RoomType = roomType;
            Diagnostics = diagnostics;
        }
    }

    /// <summary>
    /// 24 匹配链的生产装配:23 注册表 + 21a 家具目录 + 24 ROOM_TABLE ⇒ 房间类型。
    /// </summary>
    /// <remarks>
    /// <para><b>链的形状</b>(每一步都是既有的纯函数 / 只读视图,本类零新增判据):</para>
    /// <list type="number">
    /// <item><c>StructureInstanceRegistry</c> → <see cref="StructureInstanceReadView"/>
    /// (经 <see cref="BuildModulesCatalog"/> 把 `module_id` 桥成 `furn_type`)</item>
    /// <item>视图 + <see cref="ClinicFurnitureCatalogue"/> → <see cref="ClinicFurnitureTraitsSource"/></item>
    /// <item>视图 + `FURN_TYPES` 闭集 → <see cref="ClinicRoomExtraction.Extract(IPlacedStructureReadView, IReadOnlyCollection{string})"/>
    /// (**真判据入口**,不是代理版 —— 见下)</item>
    /// <item>析出 + 属性源 + <see cref="ClinicRoomTable"/> → <see cref="ClinicRoomMatcher.TryMatchRoom(in ClinicRoomExtractionResult, IClinicFurnitureTraitsSource, ClinicRoomTable, out string, out ClinicRoomMatchDiagnostics)"/></item>
    /// </list>
    /// <para>⚠️ <b>第 3 步刻意选真判据重载</b>:代理版把「凡被 `StructurePlaced` 占用的格」
    /// 都算家具格(更宽),会让非家具结构桥接两个家具簇 ⇒ 把两个房间判成一个。
    /// 生产装配层选真判据是本类存在的**主要理由之一**(`ClinicRoomExtraction` 的
    /// `FurnTypeClosureEnforced` 哨兵因此仍为 `false` —— 它说的是「**默认入口**仍是代理」,
    /// 而本类不走默认入口)。</para>
    /// <para>⚠️ <b>无状态</b>:本类不持有 `StructureInstanceRegistry` 之外的任何东西,
    /// 不缓存求值结果,不读墙钟 / 表现态(ADR-016 §一 三源不变量)。</para>
    /// </remarks>
    public sealed class ClinicRoomMatchingService
    {
        private readonly StructureInstanceRegistry _registry;
        private readonly BuildModulesCatalog _modules;
        private readonly ClinicFurnitureCatalogue _furniture;
        private readonly ClinicRoomTable _rooms;

        /// <summary>构造:三份输入全部必填(fail-loud —— 缺任一件链都只能给出「恒 ROOM_NONE」)。</summary>
        /// <param name="registry">23 的结构实例表(`World.Structures`)。</param>
        /// <param name="modules">模块目录(`build_modules.cooked` 的生产实现)。</param>
        /// <param name="furniture">21a 家具目录(`FURN_TYPES` 闭集 + 逐件 tier / e_env)。</param>
        /// <param name="rooms">24 的 ROOM_TABLE。</param>
        /// <exception cref="ArgumentNullException">任一为 null(接线错误,非设计态)。</exception>
        public ClinicRoomMatchingService(StructureInstanceRegistry registry,
                                         BuildModulesCatalog modules,
                                         ClinicFurnitureCatalogue furniture,
                                         ClinicRoomTable rooms)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _modules = modules ?? throw new ArgumentNullException(nameof(modules));
            _furniture = furniture ?? throw new ArgumentNullException(nameof(furniture));
            _rooms = rooms ?? throw new ArgumentNullException(nameof(rooms));
        }

        /// <summary>ROOM_TABLE(只读;诊断 / 调试用)。</summary>
        public ClinicRoomTable Rooms => _rooms;

        /// <summary>
        /// 对**当下**的布局求值一次(无缓存 —— 见类备注)。
        /// </summary>
        /// <returns>析出 + 匹配 + 诊断三段结果(绝不为 null;结构体)。</returns>
        public ClinicRoomMatchOutcome Match()
        {
            // 每次重建快照:注册表可能因建造 / 拆除而变(AC-24-11 的「变更后重算」在此**天然成立**,
            // 因为它根本没有缓存可失效)。代价 = 一次摊平,与匹配同量级。
            var view = new StructureInstanceReadView(_registry, _modules);
            var traits = new ClinicFurnitureTraitsSource(view, _furniture);

            ClinicRoomExtractionResult extraction =
                ClinicRoomExtraction.Extract(view, _furniture.FurnTypes);

            bool matched = ClinicRoomMatcher.TryMatchRoom(
                extraction, traits, _rooms, out string roomType, out ClinicRoomMatchDiagnostics diagnostics);

            return new ClinicRoomMatchOutcome(extraction, matched, roomType, diagnostics);
        }

        /// <summary>
        /// `FURN_TYPES` 闭集(**只读**;供装配层 / 测试做跨侧对账,不参与判定)。
        /// </summary>
        public IReadOnlyCollection<string> FurnTypes => _furniture.FurnTypes;
    }
}
