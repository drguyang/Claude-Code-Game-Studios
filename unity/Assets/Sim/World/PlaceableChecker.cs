// 权威来源:modular-building.md 规则二(放置与合法性判定五条件)
//             ADR-009 §五(拾取判定同构:意图事件 + 当下判定)
//             ADR-015 §三(WorldPos 整数格)
//             ADR-020 §四(玩家跨格世界流事件 ActorCellEntered)
//             ADR-016 §一(敌人逻辑位姿 sim 原生)
//
// 核心机制:
//   - Placeable(m, anchor): 五条件判定,全整数域
//   - 判定结果 = 布尔(不 Append 事件,由调用方决定)
//   - 占用格集合由模块目录提供(相对锚点的整数偏移)

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;

namespace DaYiJingCheng.Sim.World
{
    /// <summary>放置判定结果(含拒绝原因)。</summary>
    public enum PlaceableResult
    {
        Success,                // 可以放置
        SlotNotFound,           // 锚点不是有效槽位
        TypeMismatch,           // 模块类型与槽位不匹配
        Occupied,               // 占用格已被占
        OutOfRegion,            // 占用格超出骨架区域
        EntityOnCell,           // 占用格上有实体(玩家/敌人)
        ModuleNotFound,         // module_id 不在目录
        InsufficientStock       // 库存不足(归 20 裁决,此处只检测)
    }

    /// <summary>放置合法性判定器。</summary>
    public sealed class PlaceableChecker
    {
        private readonly BuildSlotCatalog _catalog;
        private readonly IModuleCatalog _moduleCatalog;
        private readonly IStockQuery _stockQuery;
        private readonly IPresenceQuery _presenceQuery;

        /// <summary>构造放置判定器。</summary>
        public PlaceableChecker(
            BuildSlotCatalog catalog,
            IModuleCatalog moduleCatalog,
            IStockQuery stockQuery,
            IOccupancyQuery occupancyQuery,
            IPresenceQuery presenceQuery)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _moduleCatalog = moduleCatalog ?? throw new ArgumentNullException(nameof(moduleCatalog));
            _stockQuery = stockQuery ?? throw new ArgumentNullException(nameof(stockQuery));
            _presenceQuery = presenceQuery ?? throw new ArgumentNullException(nameof(presenceQuery));
            _occupancyQuery = occupancyQuery ?? throw new ArgumentNullException(nameof(occupancyQuery));
        }

        private readonly IOccupancyQuery _occupancyQuery;

        /// <summary>
        /// 五条件判定(F-23-2):
        /// ① 槽位合法 ∧ 类型匹配
        /// ② 占用为空
        /// ③ 占用格全部在骨架区域内
        /// ④ 骨架未改(烘焙数据,恒真 —— P0 无改骨架 API)
        /// ⑤ 数据可及 + 库存 ≥ 1
        /// ⑥ 占用格上无实体(玩家/敌人)
        /// </summary>
        public PlaceableResult Check(int moduleId, WorldPos anchor, int orientation = 0)
        {
            // 条件 ①: 槽位合法 + 类型匹配
            if (!_catalog.TryGetSlot(anchor, out var slot))
                return PlaceableResult.SlotNotFound;

            var moduleDefOpt = _moduleCatalog.GetModuleDefinition(moduleId);
            if (moduleDefOpt == null)
                return PlaceableResult.ModuleNotFound;
            var moduleDef = moduleDefOpt.Value;

            if (!slot.Allows(moduleDef.SlotType))
                return PlaceableResult.TypeMismatch;

            // 计算占用格集(F-23-2b 整数旋转)
            var occupiedCells = ComputeOccupiedCells(moduleDef, anchor, orientation);

            // 条件 ②: 占用为空 + ③ 区域包含 + ⑥ 实体空
            foreach (var cell in occupiedCells)
            {
                // ③ 区域包含
                if (!_catalog.IsInBuildSlotRegion(cell))
                    return PlaceableResult.OutOfRegion;

                // ② 占用为空
                if (_occupancyQuery.IsOccupied(cell))
                    return PlaceableResult.Occupied;

                // ⑥ 占用格上无实体(玩家/敌人)
                if (_presenceQuery.IsPresentAt(cell))
                    return PlaceableResult.EntityOnCell;
            }

            // 条件 ⑤: 库存
            if (!_stockQuery.HasStock(moduleId))
                return PlaceableResult.InsufficientStock;

            return PlaceableResult.Success;
        }

        /// <summary>
        /// 计算模块在世界格上的占用集(F-23-2b 整数旋转)。
        /// </summary>
        public static IReadOnlyList<WorldPos> ComputeOccupiedCells(
            ModuleDefinition moduleDef,
            WorldPos anchor,
            int orientation)
        {
            var result = new List<WorldPos>(moduleDef.LocalOccupancy.Count);

            foreach (var local in moduleDef.LocalOccupancy)
            {
                // 整数四向旋转(禁浮点)
                int rx = local.X;
                int rz = local.Z;
                switch (orientation)
                {
                    case 0:   break;              // 0°
                    case 90:  (rx, rz) = (-rz, rx);  break;  // 90° 绕 Y
                    case 180: (rx, rz) = (-rx, -rz); break;  // 180°
                    case 270: (rx, rz) = (rz, -rx);  break;  // 270°
                    default:  throw new ArgumentException($"非法朝向: {orientation}", nameof(orientation));
                }

                result.Add(new WorldPos(anchor.X + rx, anchor.Y, anchor.Z + rz));
            }

            return result;
        }
    }

    /// <summary>模块定义(从目录加载,数据归 21a)。</summary>
    public readonly struct ModuleDefinition
    {
        public readonly int ModuleId;
        public readonly SlotType SlotType;
        public readonly IReadOnlyList<WorldPos> LocalOccupancy;  // 相对锚点的占用格(本地坐标)
        public readonly int[] ValidOrientations;                  // 允许的朝向集合(度: 0/90/180/270)
        public readonly int Refundable;                           // 是否可返还(1=是, 0=否)

        public ModuleDefinition(
            int moduleId,
            SlotType slotType,
            IReadOnlyList<WorldPos> localOccupancy,
            int[] validOrientations,
            int refundable = 1)
        {
            ModuleId = moduleId;
            SlotType = slotType;
            LocalOccupancy = localOccupancy;
            ValidOrientations = validOrientations;
            Refundable = refundable;
        }

        /// <summary>锚点格是否在 LocalOccupancy 中(目录校验 AC-23-08)。</summary>
        public bool ContainsAnchor()
        {
            var anchor = new WorldPos(0, 0, 0);
            foreach (var cell in LocalOccupancy)
            {
                if (cell.X == 0 && cell.Y == 0 && cell.Z == 0)
                    return true;
            }
            return false;
        }
    }

    /// <summary>模块目录接口(数据归 21a,23 只读)。</summary>
    public interface IModuleCatalog
    {
        ModuleDefinition? GetModuleDefinition(int moduleId);
        bool ContainsModule(int moduleId);
        IReadOnlyCollection<int> GetAllModuleIds();
    }

    /// <summary>库存查询接口(归 20,23 只读)。</summary>
    public interface IStockQuery
    {
        bool HasStock(int moduleId);
    }

    /// <summary>占用查询接口(归 World,23 只读)。</summary>
    public interface IOccupancyQuery
    {
        bool IsOccupied(WorldPos cell);
    }
}
