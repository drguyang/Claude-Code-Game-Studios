// `IModuleCatalog` 的**生产实现**(`TD-building-01` 读侧的一半)。
//
// 权威来源:
//   `TD-building-01`(docs/tech-debt-register.md:2097)的「解除动作 · 读侧」逐字:
//     「在装配根注入 `IModuleCatalog` 的生产实现(读 `build_modules.cooked`)」
//   `Sim/World/PlaceableChecker.cs:174` 的接口定义(三成员)
//   `Sim/World/PlaceableChecker.cs:138` 的 `ModuleDefinition`(六字段,含 `Cost` ——
//   2026-10-25 TD-building-01 (b) 补)
//
// ⚠️ **本文件为什么住在 `Gameplay.Presentation`**:`IModuleCatalog` / `ModuleDefinition`
//   住 `Sim`(门 A 侧),而本实现的数据来源是 cooked 字节(codec 已在本装配)。
//   `Sim` 引用集恰 = {BCL, Sim.Contracts}(ADR-025 §①)⇒ **不能**在 `Sim` 里写「读文件」的实现。
//   本类是「字节 → `ModuleDefinition`」的装配层胶水,只此一件。
//
// ⚠️ **`Cost` 的映射**(🔁 2026-10-25,TD-building-01 (b) 落地):`row.Cost` →
//   `ModuleDefinition.Cost`(`int` 计数,D-21-17 同型 —— cost ∈ [1,99] 是计数不是定点量)。
//   原稿「Cost 刻意不映射进 ModuleDefinition」的限制**就此解除** —— 该限制的到期条件
//   (`DemolishChecker` 从目录读)已由 `DemolishChecker.CalculateRefund/ValidateRefundable`
//   的实例方法兑现。
//
// ⚠️ **`ValidOrientations` 的来源**:`build_modules.json` **无**该字段(作者态只有
//   `module_id / module_type / furn_type / occupancy_local / cost / refundable`)。
//   本实现按 **`{0}`(仅 0 度)** 填 —— 这是**如实登记的限制**,不是「四向都允许」的断言:
//   `PlaceableChecker` 用 `ValidOrientations` 校验朝向,填 `{0}` 意味着**只允许 0 度放置**。
//   ⇒ 生产若需要多朝向,须先给作者态补该字段(归 `TD-itemdb-01` 同族的数值轮)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;

namespace DaYiJingCheng.Gameplay.Presentation
{
    /// <summary>
    /// `IModuleCatalog` 的生产实现:输入 = <see cref="BuildModulesDataSet"/>(cooked 解码产物),
    /// 输出 = 23 的放置判定要的 `ModuleDefinition`。
    /// </summary>
    /// <remarks>
    /// <para>构造期一次性摊平索引,之后只读(与 `ClinicFurnitureTraitsSource` 同款快照语义)。</para>
    /// <para>⚠️ **本类不校验语义**(锚点格在足迹内 / `module_id` 唯一 / 足迹非空)——
    /// 那些是**烘焙期**门(`BuildModulesBinder`)的职责,此处重做只会产生第二真源。
    /// 本类只做**形状映射** + 「目录自洽」的防御性断言。</para>
    /// </remarks>
    public sealed class BuildModulesCatalog : IModuleCatalog
    {
        private readonly Dictionary<int, ModuleDefinition> _byId;
        private readonly Dictionary<int, string> _furnTypeById;
        private readonly int[] _allIds;

        /// <summary>目录行数。</summary>
        public int Count => _allIds.Length;

        /// <summary>构造(一次性映射)。</summary>
        /// <param name="data">cooked 解码产物。</param>
        /// <exception cref="ArgumentNullException"><paramref name="data"/> 的 `Rows` 为 null。</exception>
        /// <exception cref="InvalidOperationException">行集为空 / `module_id` 重复 ——
        /// 二者都是**上游门漏拦**的信号,此处 fail-loud 而非静默择一。</exception>
        public BuildModulesCatalog(in BuildModulesDataSet data)
        {
            IReadOnlyList<BuildModuleRow> rows = data.Rows;
            if (rows == null)
                throw new ArgumentNullException(nameof(data), "BuildModulesDataSet.Rows 为 null");
            if (rows.Count == 0)
                throw new InvalidOperationException(
                    "模块目录行集为空 —— 空目录让放置判定恒失败(建造系统静默失效);" +
                    "烘焙期 Binder 的空表校验应已拦,此处是纵深防御");

            _byId = new Dictionary<int, ModuleDefinition>(rows.Count);
            _furnTypeById = new Dictionary<int, string>(rows.Count);
            _allIds = new int[rows.Count];
            for (int i = 0; i < rows.Count; i++)
            {
                BuildModuleRow row = rows[i];
                if (_byId.ContainsKey(row.ModuleId))
                    throw new InvalidOperationException(
                        $"module_id={row.ModuleId} 在目录中重复 —— 择一会让判定依赖行序(不确定);" +
                        "烘焙期应已拒重复 id");

                _byId.Add(row.ModuleId, ToDefinition(row));
                // ⚠️ 只收非 null 的 furn_type:null = 非家具,查表缺失即返回 null,
                //    不往字典里塞 null 值(那样 `GetFurnType` 的「不在目录」与
                //    「目录里就是 null」会走两条路径,而二者语义相同)。
                if (row.FurnType != null) _furnTypeById.Add(row.ModuleId, row.FurnType);
                _allIds[i] = row.ModuleId;
            }
        }

        /// <summary>按 id 取定义(不在目录 ⇒ null)。</summary>
        public ModuleDefinition? GetModuleDefinition(int moduleId)
        {
            return _byId.TryGetValue(moduleId, out ModuleDefinition def) ? def : (ModuleDefinition?)null;
        }

        /// <summary>该 id 是否在目录内。</summary>
        public bool ContainsModule(int moduleId) => _byId.ContainsKey(moduleId);

        /// <summary>全部 id(只读快照;顺序 = 行序 = `module_id` 升序)。</summary>
        public IReadOnlyCollection<int> GetAllModuleIds() => _allIds;

        /// <summary>
        /// 该模块的 `furn_type`(24 词汇);不在目录 / `furn_type == null` ⇒ <c>null</c>。
        /// <para>⚠️ <b>这不是 `IModuleCatalog` 的成员</b> —— 接口只有三成员(23 的放置判定面)。
        /// 本方法是给 24 侧只读视图(`StructureInstanceReadView`)用的**跨层桥**:
        /// `module_id`(i32)→ `furn_type`(string)。详见该类的文件头注。</para>
        /// <para>⚠️ 返回 <c>null</c> 有**两个**来源(不在目录 / 目录里就是 null),
        /// 二者对 24 侧语义相同(「无模块标识」),故不区分。</para>
        /// </summary>
        public string GetFurnType(int moduleId)
            => _furnTypeById.TryGetValue(moduleId, out string t) ? t : null;

        /// <summary>`BuildModuleRow` → `ModuleDefinition`(形状映射;零语义校验)。</summary>
        /// <remarks>
        /// ⚠️ `ValidOrientations` 填 `{0}` —— 作者态无该字段(见文件头注)。
        /// ⚠️ `Refundable` 传 `int`(0/1),与 `ModuleDefinition.Refundable` 同型,不开布尔特例。
        /// ⚠️ `Cost` 传 `row.Cost`(`int` 计数;2026-10-25 TD-building-01 (b) 起)。
        /// </remarks>
        private static ModuleDefinition ToDefinition(in BuildModuleRow row)
        {
            return new ModuleDefinition(
                moduleId: row.ModuleId,
                slotType: (SlotType)row.ModuleType,
                localOccupancy: row.OccupancyLocal,
                validOrientations: OnlyZeroDegrees,
                cost: row.Cost,
                refundable: row.Refundable);
        }

        /// <summary>唯一的合法朝向集(作者态无该字段 ⇒ 只允许 0 度;见文件头注)。</summary>
        private static readonly int[] OnlyZeroDegrees = { 0 };
    }
}
