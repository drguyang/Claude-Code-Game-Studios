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
    /// <remarks>
    /// <para>🔁 <b>2026-10-28(S20-19)</b>:除写面 <see cref="IWorldOccupancy"/> 外,
    /// 另实现**读面** <see cref="IOccupancyQuery"/> —— 它此前**零生产实现**
    /// (只有测试替身),而 <see cref="PlaceableChecker"/> / <see cref="DemolishChecker"/> /
    /// <see cref="ModifiableChecker"/> 三个判定器都吃它 ⇒ 42 的建造发起方一接线就会撞上
    /// 「判定器无生产构造点」。本类已有 <see cref="IsSlotOccupied"/>(同一判据),
    /// 显式实现只是**把既有能力接到接口上**,零新增逻辑、零新增状态。</para>
    /// <para>⚠️ 刻意用**显式接口实现**:`IsOccupied` 这个名字对 `World` 的读者不如
    /// `IsSlotOccupied` 准确(它问的是「槽位占用表」,不是泛化的「占用」);
    /// 显式实现让两个名字并存而不互相冒充。</para>
    /// </remarks>
    public sealed class World : IWorldOccupancy, IOccupancyQuery
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

            // 初始化占用表(WorldGeometry 的 TotalCells)
            _slotOccupied = new bool[geometry.TotalCells];
        }

        /// <summary>
        /// 把世界重置为「空」—— **读档重建前的第一步**(S20-18 · 2026-10-28)。
        /// <para>清两样**派生态**:结构实例表(含发号计数器,<see cref="StructureInstanceRegistry.Reset"/>)
        /// + 占用表 <c>_slotOccupied</c> 全零。</para>
        /// <para><b>刻意不清的另两样</b>:<c>_geometry</c>(世界几何)与 <c>_slotCatalog</c>(槽位骨架)
        /// —— 它们是 ADR-015 的**烘焙静态层**,读档不改变;清零它们等于把静态数据当成状态,
        /// 且 `WorldGeometry` 根本没有「重建为空」的入口(`WorldLattice.cs:295-298` 明令)。</para>
        /// <para>⚠️ <b>唯一生产调用者 = <see cref="StructureRestorer"/> 的重放入口</b>:重置与
        /// 重放必须成对(只重置不重放 ⇒ 房子全消失;只重放不重置 ⇒ 旧世界残留并叠加)。
        /// 故本方法**不单独接进任何启动/读档路径**,只作为重放入口内部的第一步。</para>
        /// </summary>
        public void Reset()
        {
            _structures.Reset();
            Array.Clear(_slotOccupied, 0, _slotOccupied.Length);
        }

        // ── 占用查询(Overlay.blocked = slot_occupied 投影) ──

        /// <summary>检查格是否被占用。越界返回 false(fail-closed)。</summary>
        public bool IsSlotOccupied(WorldPos cell)
        {
            int idx = _geometry.ToIndex(cell);
            if (idx < 0 || idx >= _slotOccupied.Length)
                return false;
            return _slotOccupied[idx];
        }

        /// <summary>
        /// <see cref="IOccupancyQuery"/> 的实现 —— 与 <see cref="IsSlotOccupied"/> **同一判据**
        /// (S20-19 · 2026-10-28)。
        /// </summary>
        /// <remarks>
        /// ⚠️ 越界返回 false(与 <see cref="IsSlotOccupied"/> 一致,fail-closed)。
        /// 判定器侧的 `OutOfRegion` 条件会先拦掉越界格,本处不重复判。
        /// </remarks>
        bool IOccupancyQuery.IsOccupied(WorldPos cell) => IsSlotOccupied(cell);

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
            // ⚠️ 2026-10-03(C8/ID):structure id 全链升 `long` —— 局部变量随之。
            long sid = _structures.GetInstanceAt(cell);
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

    /// <summary>
    /// OccupancyOverlay —— slot_occupied 的纯函数投影。
    /// 27 / 13 只读此类型,不读 Nav 原件。
    /// </summary>
    public readonly struct OccupancyOverlay
    {
        private readonly IReadOnlyList<bool> _slotOccupied;

        public OccupancyOverlay(IReadOnlyList<bool> slotOccupied)
        {
            _slotOccupied = slotOccupied;
        }

        /// <summary>格是否被占用(blocked)。越界返回 true(fail-closed)。</summary>
        public bool IsBlocked(WorldPos cell, WorldGeometry geometry)
        {
            int idx = geometry.ToIndex(cell);
            if (idx < 0 || idx >= _slotOccupied.Count)
                return true;
            return _slotOccupied[idx];
        }
    }
}
