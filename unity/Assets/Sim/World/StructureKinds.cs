// 权威来源:modular-building.md 规则四(Structure* 世界流事件)
//             ADR-009 §三(Structure 三 Kind 骨架 · 载荷归本 GDD)
//             ADR-015 §五(模块化网格 · 朝向整数枚举)
//
// 核心机制:
//   - StructurePlaced / Removed / Modified 世界流事件
//   - StructureInstanceRegistry: 实例表(派生态,不存事件流)
//   - 主机唯一 Append(通过构造时传入的 IEventSink)
//   - 全部整数载荷,无 float

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.World
{
    #region 事件载荷

    /// <summary>StructurePlaced 载荷(全整数)。</summary>
    public readonly struct StructurePlacedPayload
    {
        public readonly int StructureId;      // 结构实例 id
        public readonly WorldPos Cell;        // 锚点格
        public readonly int ModuleId;         // 模块 id
        public readonly int Orientation;      // 朝向(度: 0/90/180/270)
        public readonly int Variant;          // 变体索引

        public StructurePlacedPayload(int structureId, WorldPos cell, int moduleId, int orientation, int variant)
        {
            StructureId = structureId;
            Cell = cell;
            ModuleId = moduleId;
            Orientation = orientation;
            Variant = variant;
        }
    }

    /// <summary>StructureRemoved 载荷(无 orientation/variant)。</summary>
    public readonly struct StructureRemovedPayload
    {
        public readonly int StructureId;      // 结构实例 id
        public readonly WorldPos Cell;        // 锚点格
        public readonly int ModuleId;         // 模块 id

        public StructureRemovedPayload(int structureId, WorldPos cell, int moduleId)
        {
            StructureId = structureId;
            Cell = cell;
            ModuleId = moduleId;
        }
    }

    /// <summary>StructureModified 载荷(物理形态变更)。</summary>
    public readonly struct StructureModifiedPayload
    {
        public readonly int StructureId;      // 结构实例 id
        public readonly WorldPos Cell;        // 锚点格
        public readonly int ModuleId;         // 模块 id(不变,校验用)
        public readonly int NewOrientation;   // 新朝向
        public readonly int NewVariant;       // 新变体
        public readonly int ModifiedFields;   // 位掩码: 1=朝向, 2=变体

        public StructureModifiedPayload(
            int structureId, WorldPos cell, int moduleId,
            int newOrientation, int newVariant, int modifiedFields)
        {
            StructureId = structureId;
            Cell = cell;
            ModuleId = moduleId;
            NewOrientation = newOrientation;
            NewVariant = newVariant;
            ModifiedFields = modifiedFields;
        }
    }

    #endregion

    #region 结构实例表(第三份派生态)

    /// <summary>结构实例数据。</summary>
    public readonly struct StructureInstance
    {
        public readonly int StructureId;
        public readonly WorldPos Anchor;
        public readonly int ModuleId;
        public readonly int Orientation;
        public readonly int Variant;
        public readonly IReadOnlyList<WorldPos> OccupiedCells;

        public StructureInstance(int structureId, WorldPos anchor, int moduleId, int orientation, int variant, IReadOnlyList<WorldPos> occupiedCells = null)
        {
            StructureId = structureId;
            Anchor = anchor;
            ModuleId = moduleId;
            Orientation = orientation;
            Variant = variant;
            OccupiedCells = occupiedCells ?? new List<WorldPos> { anchor };
        }
    }

    /// <summary>结构实例表(派生态,不存事件流,由 Structure* 事件重建)。</summary>
    public sealed class StructureInstanceRegistry
    {
        private readonly Dictionary<int, StructureInstance> _instances = new Dictionary<int, StructureInstance>();
        private int _nextStructureId = 1;

        /// <summary>注册新实例(返回新 id)。</summary>
        public int Register(WorldPos anchor, int moduleId, int orientation, int variant)
        {
            int id = _nextStructureId++;
            // 计算占用格(简化:仅锚点格;完整实现需查模块目录)
            var occupiedCells = new List<WorldPos> { anchor };
            _instances[id] = new StructureInstance(id, anchor, moduleId, orientation, variant, occupiedCells);
            return id;
        }

        /// <summary>移除实例(返回 true 如果存在并移除)。</summary>
        public bool Remove(int structureId)
        {
            return _instances.Remove(structureId);
        }

        /// <summary>更新实例(朝向/变体)。</summary>
        public bool Update(int structureId, int? newOrientation, int? newVariant)
        {
            if (!_instances.TryGetValue(structureId, out var inst))
                return false;

            int orientation = newOrientation ?? inst.Orientation;
            int variant = newVariant ?? inst.Variant;

            _instances[structureId] = new StructureInstance(
                inst.StructureId, inst.Anchor, inst.ModuleId, orientation, variant, inst.OccupiedCells);
            return true;
        }

        /// <summary>查询实例。</summary>
        public bool TryGet(int structureId, out StructureInstance instance)
        {
            return _instances.TryGetValue(structureId, out instance);
        }

        /// <summary>获取锚点格上的实例 id(无则返回 -1)。</summary>
        public int GetInstanceAt(WorldPos anchor)
        {
            foreach (var kv in _instances)
            {
                if (kv.Value.Anchor.X == anchor.X &&
                    kv.Value.Anchor.Y == anchor.Y &&
                    kv.Value.Anchor.Z == anchor.Z)
                    return kv.Key;
            }
            return -1;
        }

        /// <summary>获取实例总数。</summary>
        public int Count => _instances.Count;

        /// <summary>清空(重建用)。</summary>
        public void Clear()
        {
            _instances.Clear();
        }
    }

    #endregion

    #region 结构写者

    /// <summary>结构世界流事件写者(主机唯一)。</summary>
    public sealed class StructureWriter
    {
        private readonly IEventSink _eventSink;
        private readonly StructureInstanceRegistry _registry;
        private int _nextStructureId = 1;

        public StructureWriter(IEventSink eventSink, StructureInstanceRegistry registry)
        {
            _eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        /// <summary>下一个可用 structure_id(不递增,仅预览)。</summary>
        public int PeekNextId => _nextStructureId;

        /// <summary>放置模块(Append StructurePlaced 事件)。</summary>
        public int Place(WorldPos anchor, int moduleId, int orientation, int variant, long tick)
        {
            int structureId = _registry.Register(anchor, moduleId, orientation, variant);
            if (structureId >= _nextStructureId)
                _nextStructureId = structureId + 1;

            // 构造载荷
            var payload = new PayloadRef(blobId: structureId, offset: moduleId, length: 8);
            // 额外数据: orientation + variant 编码到 offset 高位
            payload = new PayloadRef(
                blobId: structureId,
                offset: moduleId | (orientation << 16) | (variant << 24),
                length: 8);

            var evt = new SimEvent(tick, PatientId.None, 0, EventKind.StructurePlaced, payload);
            _eventSink.Append(evt);

            return structureId;
        }

        /// <summary>拆除模块(Append StructureRemoved 事件)。</summary>
        public bool Remove(int structureId, long tick)
        {
            if (!_registry.TryGet(structureId, out var inst))
                return false;

            _registry.Remove(structureId);

            // 载荷: BlobId = structure_id, Offset = module_id, Length = 8
            var payload = new PayloadRef(blobId: structureId, offset: inst.ModuleId, length: 8);
            var evt = new SimEvent(tick, PatientId.None, 0, EventKind.StructureRemoved, payload);
            _eventSink.Append(evt);

            return true;
        }

        /// <summary>修改模块(朝向/变体,Append StructureModified 事件)。</summary>
        public bool Modify(int structureId, int? newOrientation, int? newVariant, long tick)
        {
            if (!_registry.TryGet(structureId, out var inst))
                return false;

            // 计算 modified_fields 位掩码
            int modifiedFields = 0;
            if (newOrientation.HasValue && newOrientation.Value != inst.Orientation)
                modifiedFields |= 1; // 朝向位
            if (newVariant.HasValue && newVariant.Value != inst.Variant)
                modifiedFields |= 2; // 变体位

            if (modifiedFields == 0)
                return false; // 无变更

            _registry.Update(structureId, newOrientation, newVariant);

            int orientation = newOrientation ?? inst.Orientation;
            int variant = newVariant ?? inst.Variant;

            // 载荷: 真实 payload 类型(ADR-024: 非 bit-packing)
            var payload = new PayloadRef(blobId: structureId, offset: 0, length: 8);
            // TODO: 接入 Sim.Codec 编码 StructureModifiedPayload(需 IBlobPool 支持)

            var evt = new SimEvent(tick, PatientId.None, 0, EventKind.StructureModified, payload);
            _eventSink.Append(evt);

            return true;
        }
    }

    #endregion
}
