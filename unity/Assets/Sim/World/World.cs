// 权威来源:modular-building.md 规则三(OccupancyOverlay 合成 · F-23-1)
//             ADR-015 §三(单一整数格 · Nav 与 Overlay 共用 WorldPos)
//             ADR-022 §三(world_buildslots.json 槽位骨架)
//
// 核心机制:
//   - World 类: 组合 Nav(静态) + Overlay(动态占用) → EffectiveWalkable
//   - 单一占用表 slot_occupied(23 唯一写者)
//   - Overlay.blocked = slot_occupied 的纯函数投影
//   - 合成点唯一: 23 产出, 27/13 只读

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.World
{
    /// <summary>世界状态(组合 Nav + Overlay)。</summary>
    public sealed class World
    {
        private readonly WorldGeometry _geometry;               // 静态几何(ADR-015)
        private readonly StructureInstanceRegistry _structures; // 结构实例表(派生态)
        private readonly BuildSlotCatalog _slotCatalog;         // 槽位骨架(派生态)
        private readonly bool[] _slotOccupied;                  // 占用表(单真相)

        public WorldGeometry Geometry => _geometry;
        public StructureInstanceRegistry Structures => _structures;
        public BuildSlotCatalog SlotCatalog => _slotCatalog;

        /// <summary>构造 World。</summary>
        public World(WorldGeometry geometry, BuildSlotCatalog slotCatalog)
        {
            _geometry = geometry;
            _slotCatalog = slotCatalog ?? throw new ArgumentNullException(nameof(slotCatalog));
            _structures = new StructureInstanceRegistry();

            // 初始化占用表(WorldGeometry 的 SizeX * SizeY * SizeZ)
            int total = geometry.SizeX * geometry.SizeY * geometry.SizeZ;
            _slotOccupied = new bool[total];
        }

        // ── 占用查询(Overlay.blocked = slot_occupied 投影) ──

        /// <summary>检查格是否被占用。</summary>
        public bool IsSlotOccupied(WorldPos cell)
        {
            int idx = _geometry.ToIndex(cell);
            if (idx < 0 || idx >= _slotOccupied.Length)
                return false;
            return _slotOccupied[idx];
        }

        /// <summary>获取占用表(用于合成 EffectiveWalkable)。</summary>
        public IReadOnlyList<bool> GetOccupancyArray()
        {
            return Array.AsReadOnly(_slotOccupied);
        }

        // ── 占用变更(23 唯一写者) ──

        /// <summary>标记格为占用。</summary>
        public void OccupyCell(WorldPos cell)
        {
            int idx = _geometry.ToIndex(cell);
            if (idx >= 0 && idx < _slotOccupied.Length)
                _slotOccupied[idx] = true;
        }

        /// <summary>清除格的占用。</summary>
        public void FreeCell(WorldPos cell)
        {
            int idx = _geometry.ToIndex(cell);
            if (idx >= 0 && idx < _slotOccupied.Length)
                _slotOccupied[idx] = false;
        }

        /// <summary>批量占用(模块放置时使用)。</summary>
        public void OccupyCells(IEnumerable<WorldPos> cells)
        {
            foreach (var cell in cells)
                OccupyCell(cell);
        }

        /// <summary>批量释放(模块拆除时使用)。</summary>
        public void FreeCells(IEnumerable<WorldPos> cells)
        {
            foreach (var cell in cells)
                FreeCell(cell);
        }

        // ── EffectiveWalkable 合成(F-23-1) ──

        /// <summary>
        /// EffectiveWalkable(cell) := Nav[cell].walkable ∧ ¬slot_occupied(cell)
        /// 27 / 13 只读此方法,不读 Nav 原件。
        /// </summary>
        public bool IsEffectivelyWalkable(WorldPos cell)
        {
            int idx = _geometry.ToIndex(cell);
            if (idx < 0 || idx >= _slotOccupied.Length)
                return false;

            TerrainCell terrain = _geometry.GetTerrain(cell);
            return terrain.Walkable && !_slotOccupied[idx];
        }

        /// <summary>获取格的 EffectiveWalkable(用于 27/13 查询接口)。</summary>
        public bool GetEffectiveWalkable(WorldPos cell)
        {
            return IsEffectivelyWalkable(cell);
        }

        // ── 结构查询 ──

        /// <summary>获取指定格的模块 id(无则返回 -1)。</summary>
        public int GetModuleAt(WorldPos cell)
        {
            int sid = _structures.GetInstanceAt(cell);
            if (sid < 0)
                return -1;

            if (_structures.TryGet(sid, out var inst))
                return inst.ModuleId;

            return -1;
        }

        /// <summary>获取总占用数。</summary>
        public int OccupiedCount
        {
            get
            {
                int count = 0;
                foreach (var b in _slotOccupied)
                    if (b) count++;
                return count;
            }
        }
    }
}
