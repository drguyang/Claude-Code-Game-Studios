// 权威来源:modular-building.md 规则四(Structure* 世界流事件)
//             ADR-009 §三(Structure 三 Kind 骨架 · 载荷归本 GDD)
//             ADR-015 §五(模块化网格 · 朝向整数枚举)
//             ADR-029(载荷编码唯一路径 = IPayloadEncoder)
//
// 核心机制:
//   - StructurePlaced / Removed / Modified 世界流事件
//   - StructureInstanceRegistry: 实例表(派生态,不存事件流)
//   - 主机唯一 Append(通过构造时传入的 IEventSink)
//   - 全部整数载荷,无 float
//   - **载荷编码经 IPayloadEncoder**(ADR-029 §③:零手搓 PayloadRef)
//
// ⚠️ 2026-10-02(ADR-029 接线支 Story 007)删除本文件内的三支 payload struct 副本:
//   StructurePlacedPayload / StructureRemovedPayload / StructureModifiedPayload
//   曾**重复定义**于本文件(DaYiJingCheng.Sim.World)与
//   Sim.Contracts/Payloads/WorldPayloads.cs(DaYiJingCheng.Sim.Contracts)。
//   两份字段同序同义,**但 `StructureId` 类型不同**(本文件 = int,契约版 = long)。
//   契约版才是权威 —— PayloadCodec 与全部测试用 long;本文件那三支**零引用**(死代码)。
//   ⇒ 已删,统一用契约版(本文件经 using DaYiJingCheng.Sim.Contracts 可见)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.World
{
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
        private readonly IPayloadEncoder _encoder;
        private int _nextStructureId = 1;

        /// <param name="eventSink">事件写入通道(主机唯一)。</param>
        /// <param name="registry">结构实例表。</param>
        /// <param name="encoder">
        /// 载荷编码器(ADR-029 §③)—— **唯一合法的载荷构造路径**。
        /// 本类内不得出现 <c>new PayloadRef(</c>(ADR-029 §③ 的门判据)。
        /// </param>
        public StructureWriter(IEventSink eventSink, StructureInstanceRegistry registry,
                               IPayloadEncoder encoder)
        {
            _eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _encoder = encoder ?? throw new ArgumentNullException(nameof(encoder));
        }

        /// <summary>下一个可用 structure_id(不递增,仅预览)。</summary>
        public int PeekNextId => _nextStructureId;

        /// <summary>放置模块(Append StructurePlaced 事件)。</summary>
        public int Place(WorldPos anchor, int moduleId, int orientation, int variant, long tick)
        {
            int structureId = _registry.Register(anchor, moduleId, orientation, variant);
            if (structureId >= _nextStructureId)
                _nextStructureId = structureId + 1;

            // ADR-029 §③:载荷经 IPayloadEncoder —— 五字段全载,零 bit-packing
            var payload = _encoder.Encode(EventKind.StructurePlaced,
                new StructurePlacedPayload(structureId, anchor, moduleId, orientation, variant));

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

            // ADR-029 §③:载荷经 IPayloadEncoder(三字段:structure_id / cell / module_id)
            var payload = _encoder.Encode(EventKind.StructureRemoved,
                new StructureRemovedPayload(structureId, inst.Anchor, inst.ModuleId));

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

            // ADR-029 §③:载荷经 IPayloadEncoder —— 六字段全载(含 ModifiedFields 位掩码)。
            // ⚠️ 2026-10-02 修复:原实现把 orientation / variant **算出却未放进载荷**
            //    (只写 offset: 0),两字段被静默丢弃。本行起真正编码。
            var payload = _encoder.Encode(EventKind.StructureModified,
                new StructureModifiedPayload(structureId, inst.Anchor, inst.ModuleId,
                                             orientation, variant, modifiedFields));

            var evt = new SimEvent(tick, PatientId.None, 0, EventKind.StructureModified, payload);
            _eventSink.Append(evt);

            return true;
        }
    }

    #endregion
}
