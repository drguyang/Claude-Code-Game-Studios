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
        /// <summary>
        /// 结构身份 —— **`long`(i64)**,非 `int`。
        /// ⚠️ **2026-10-03 修复(评审 C8/ID)**:原为 `int`,与
        /// `entities.yaml:2068` 登记的 `structure_id: i64` **类型不符**,
        /// 也与契约侧 `Sim.Contracts.StructurePlacedPayload.StructureId`(**long**)
        /// 及 codec(`PayloadCodec.World.cs:269` `WriteFieldInt64`)**不一致** ——
        /// 同一 id 在两处不同宽度,是**静默截断**风险(`int` 溢出即失真,
        /// 且与 `ItemInstanceId` 同模式的「计数器 + 高水位可重构」口径不匹配)。
        /// 现全链升 `long`,与契约 / codec / ADR-010 §五 的 `IIdAuthority` 机制 A 对齐。
        /// </summary>
        public readonly long StructureId;
        public readonly WorldPos Anchor;
        public readonly int ModuleId;
        public readonly int Orientation;
        public readonly int Variant;
        public readonly IReadOnlyList<WorldPos> OccupiedCells;

        public StructureInstance(long structureId, WorldPos anchor, int moduleId, int orientation, int variant, IReadOnlyList<WorldPos> occupiedCells = null)
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
        private readonly Dictionary<long, StructureInstance> _instances = new Dictionary<long, StructureInstance>();
        private long _nextStructureId = 1;

        /// <summary>
        /// 注册新实例(返回新 id)。
        /// </summary>
        /// <param name="occupiedCells">
        /// **完整占用格集**(F-23-2b 整数旋转后的世界格)。
        /// ⚠️ **2026-10-03 修复(评审 C1)**:原实现自造 `new List&lt;WorldPos&gt; { anchor }`
        /// (注释自陈「简化:仅锚点格」)⇒ **多格模块的非锚点足迹格不进实例表**,
        /// 使 `DemolishChecker` 的逐格实体检查**只查锚点** ⇒ **抵消 B2 的修复**。
        /// 现由调用方传入**真足迹**(源 = `PlaceableChecker.ComputeOccupiedCells`,
        /// 即放置判定所用的同一函数 —— 两处自此同源)。
        /// </param>
        public long Register(WorldPos anchor, int moduleId, int orientation, int variant,
                             IReadOnlyList<WorldPos> occupiedCells = null)
        {
            long id = _nextStructureId++;
            // ⚠️ **2026-10-03(N-r1)**:兜底仍 = `{anchor}`,但**生产路径已 fail-closed**
            //    (`StructureWriter.Place` 无目录即抛)⇒ 该兜底**只服务既有测试的直调**。
            //    新调用方**须传真足迹**;若生产代码出现直调 `Register` 而不传足迹,
            //    须在评审中判为 C1 复现。
            var cells = occupiedCells ?? new List<WorldPos> { anchor };
            _instances[id] = new StructureInstance(id, anchor, moduleId, orientation, variant, cells);
            return id;
        }

        /// <summary>移除实例(返回 true 如果存在并移除)。</summary>
        public bool Remove(long structureId)
        {
            return _instances.Remove(structureId);
        }

        /// <summary>更新实例(朝向/变体)。</summary>
        public bool Update(long structureId, int? newOrientation, int? newVariant)
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
        public bool TryGet(long structureId, out StructureInstance instance)
        {
            return _instances.TryGetValue(structureId, out instance);
        }

        /// <summary>获取锚点格上的实例 id(无则返回 -1)。</summary>
        public long GetInstanceAt(WorldPos anchor)
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
        private readonly IModuleCatalog _moduleCatalog;
        private readonly IWorldOccupancy _occupancy;
        private long _nextStructureId = 1;

        /// <param name="eventSink">事件写入通道(主机唯一)。</param>
        /// <param name="registry">结构实例表。</param>
        /// <param name="encoder">
        /// 载荷编码器(ADR-029 §③)—— **唯一合法的载荷构造路径**。
        /// 本类内不得出现 <c>new PayloadRef(</c>(ADR-029 §③ 的门判据)。
        /// </param>
        /// <param name="moduleCatalog">
        /// 模块目录 —— 用于取 `LocalOccupancy` 算**真足迹**(C1)。
        /// </param>
        /// <param name="occupancy">
        /// 世界占用表写面 —— **放置/拆除须同步 Overlay**(C2)。
        /// 可传 null 表示「不接线」(向后兼容;但 F-23-1 对已放置结构将不生效)。
        /// </param>
        public StructureWriter(IEventSink eventSink, StructureInstanceRegistry registry,
                               IPayloadEncoder encoder,
                               IModuleCatalog moduleCatalog = null,
                               IWorldOccupancy occupancy = null)
        {
            _eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _encoder = encoder ?? throw new ArgumentNullException(nameof(encoder));
            _moduleCatalog = moduleCatalog;
            _occupancy = occupancy;
        }

        /// <summary>下一个可用 structure_id(不递增,仅预览)。</summary>
        public long PeekNextId => _nextStructureId;

        /// <summary>放置模块(Append StructurePlaced 事件)。</summary>
        public long Place(WorldPos anchor, int moduleId, int orientation, int variant, long tick)
        {
            // ── C1:真足迹(与放置判定同源)─────────────────────────────
            // ⚠️ **2026-10-03 fail-closed(第二轮评审 N-r1)**:
            //    初版把 `moduleCatalog` / `occupancy` 设为**可选**(默认 null)以求向后兼容 ——
            //    结果 `moduleCatalog == null` 时 `occupied` 保持 null ⇒ `Register` 兜底 `{anchor}`
            //    (**C1 复现**)且 `OccupyCells` 不调(**C2 复现**),**全程静默**。
            //    更糟:`test_c2_noCatalog_noOccupancy_noThrow` 把该退化路径**断言为正确行为**。
            //    ⇒ 现改为 **fail-closed**:
            //      · 未注入 `moduleCatalog` ⇒ **抛**(结构写者无目录即不可用);
            //      · `moduleId` 不在目录 ⇒ **抛**(不许拿 `{anchor}` 冒充真足迹)。
            if (_moduleCatalog == null)
                throw new InvalidOperationException(
                    "StructureWriter 未注入 IModuleCatalog —— 无法算真足迹(F-23-2b)。" +
                    "静默兜底 {{anchor}} 会复现 C1(占用集退化)与 C2(Overlay 不写),故 fail-closed。");

            var defOpt = _moduleCatalog.GetModuleDefinition(moduleId);
            if (!defOpt.HasValue)
                throw new InvalidOperationException(
                    $"moduleId={moduleId} 不在模块目录内 —— 无法算真足迹。" +
                    "静默兜底 {{anchor}} 会复现 C1/C2,故 fail-closed。");

            IReadOnlyList<WorldPos> occupied =
                PlaceableChecker.ComputeOccupiedCells(defOpt.Value, anchor, orientation);

            long structureId = _registry.Register(anchor, moduleId, orientation, variant, occupied);
            if (structureId >= _nextStructureId)
                _nextStructureId = structureId + 1;

            // ── C2:同步世界占用表(F-23-1 EffectiveWalkable 的写路径)──────
            // ⚠️ **fail-closed(N-r1)**:未注入 `occupancy` ⇒ 抛。
            //    静默跳过会让 F-23-1 对已放置结构**不生效**(C2),且**无任何信号**。
            if (_occupancy == null)
                throw new InvalidOperationException(
                    "StructureWriter 未注入 IWorldOccupancy —— 放置不会写 Overlay ⇒ " +
                    "F-23-1 EffectiveWalkable 对已放置结构不生效(C2)。故 fail-closed。");
            _occupancy.OccupyCells(occupied);

            // ADR-029 §③:载荷经 IPayloadEncoder —— 五字段全载,零 bit-packing
            var payload = _encoder.Encode(EventKind.StructurePlaced,
                new StructurePlacedPayload(structureId, anchor, moduleId, orientation, variant));

            var evt = new SimEvent(tick, PatientId.None, 0, EventKind.StructurePlaced, payload);
            _eventSink.Append(evt);

            return structureId;
        }

        /// <summary>拆除模块(Append StructureRemoved 事件)。</summary>
        public bool Remove(long structureId, long tick)
        {
            if (!_registry.TryGet(structureId, out var inst))
                return false;

            // ── C2:先释放占用格,再移除实例 ──────────────────────────
            if (_occupancy != null)
                _occupancy.FreeCells(inst.OccupiedCells);

            _registry.Remove(structureId);

            // ADR-029 §③:载荷经 IPayloadEncoder(三字段:structure_id / cell / module_id)
            var payload = _encoder.Encode(EventKind.StructureRemoved,
                new StructureRemovedPayload(structureId, inst.Anchor, inst.ModuleId));

            var evt = new SimEvent(tick, PatientId.None, 0, EventKind.StructureRemoved, payload);
            _eventSink.Append(evt);

            return true;
        }

        /// <summary>修改模块(朝向/变体,Append StructureModified 事件)。</summary>
        public bool Modify(long structureId, int? newOrientation, int? newVariant, long tick)
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
