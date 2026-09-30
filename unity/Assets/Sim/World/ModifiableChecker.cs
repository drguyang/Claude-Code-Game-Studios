// 权威来源:modular-building.md 规则十(StructureModified 写者分界 + 改朝向判定)
//             F-23-2b(整数旋转) · F-23-2(五条件判定)
//
// 核心机制:
//   - ModifiableChecker: 改朝向/变体时的占用检查
//   - 新占用格集 ∖ 旧格 全空 ∧ 新朝向 TypeOK
//   - 整数四向旋转(零浮点)

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;

namespace DaYiJingCheng.Sim.World
{
    /// <summary>改朝向/变体的判定结果。</summary>
    public enum ModifiableResult
    {
        Success,           // 可以修改
        StructureNotFound, // structure_id 不存在
        NoChange,          // 新旧值相同
        NewCellsOccupied,  // 新占用格被占
        TypeMismatch,      // 新朝向不满足槽位类型约束
        OutOfRegion        // 新占用格超出骨架区域
    }

    /// <summary>改朝向/变体判定器。</summary>
    public sealed class ModifiableChecker
    {
        private readonly BuildSlotCatalog _catalog;
        private readonly IOccupancyQuery _occupancyQuery;
        private readonly IModuleCatalog _moduleCatalog;
        private readonly StructureInstanceRegistry _registry;

        /// <summary>构造 ModifiableChecker。</summary>
        public ModifiableChecker(
            BuildSlotCatalog catalog,
            IOccupancyQuery occupancyQuery,
            IModuleCatalog moduleCatalog,
            StructureInstanceRegistry registry)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _occupancyQuery = occupancyQuery ?? throw new ArgumentNullException(nameof(occupancyQuery));
            _moduleCatalog = moduleCatalog ?? throw new ArgumentNullException(nameof(moduleCatalog));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        /// <summary>
        /// Modifiable(m, anchor, new_orientation, new_variant):
        /// 新占用格集 ∖ 旧格 全空 ∧ 新朝向 TypeOK
        /// </summary>
        public ModifiableResult Check(int structureId, int newOrientation, int newVariant)
        {
            // 查询实例
            if (!_registry.TryGet(structureId, out var inst))
                return ModifiableResult.StructureNotFound;

            // 无变更
            if (newOrientation == inst.Orientation && newVariant == inst.Variant)
                return ModifiableResult.NoChange;

            // 获取模块定义
            var moduleDef = _moduleCatalog.GetModuleDefinition(inst.ModuleId);
            if (moduleDef == null)
                return ModifiableResult.StructureNotFound;

            // 检查新朝向是否在模块允许集合内
            bool orientationAllowed = false;
            foreach (var valid in moduleDef.ValidOrientations)
            {
                if (valid == newOrientation)
                {
                    orientationAllowed = true;
                    break;
                }
            }
            if (!orientationAllowed)
                return ModifiableResult.TypeMismatch;

            // 计算新占用格集
            var newCells = PlaceableChecker.ComputeOccupiedCells(
                moduleDef, inst.Anchor, newOrientation);

            // 检查: 新格 ∖ 旧格 全空
            var oldCells = PlaceableChecker.ComputeOccupiedCells(
                moduleDef, inst.Anchor, inst.Orientation);

            var oldSet = new HashSet<WorldPos>(oldCells);

            foreach (var cell in newCells)
            {
                // 区域包含检查
                if (!_catalog.IsInBuildSlotRegion(cell))
                    return ModifiableResult.OutOfRegion;

                // 旧格本身不检查(自己的格永远空)
                if (oldSet.Contains(cell))
                    continue;

                // 新格须为空
                if (_occupancyQuery.IsOccupied(cell))
                    return ModifiableResult.NewCellsOccupied;
            }

            return ModifiableResult.Success;
        }
    }
}
