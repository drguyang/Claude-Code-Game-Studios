// 门的开 / 合消费面(4 → 23)· 2026-11-02 · `AC-4-22` 兑现轮。
//
// 权威来源:
//   GDD `interaction-system.md` 规则一(4 只选目标,不结算 —— 门改几何归 23)
//   · `modular-building.md` 规则四(`StructureModified` 三条 `Structure*` 事件之一)
//   · ADR-009 §七(三段式:意图 → 拥有方当下判定 → 效果进流)
//   · `World.IsEffectivelyWalkable`(门的开合**不另存状态**:现读 `StructureInstance.Variant`)
//
// ── 状态形状(本类的前提)──────────────────────────────────────────────────────
// 门 = 一个 `SlotType.Door` 的模块,开 / 合 = 实例的 `Variant`(**0 = 关,1 = 开**)。
// 切换走 `StructureWriter.Modify(id, null, newVariant, tick)`:
//   · 写出一条 `StructureModified`(进世界流 ⇒ 可重放 / 可存档 / 可联机);
//   · 实例表随之更新 ⇒ `World.IsEffectivelyWalkable` 的 `IsClosedDoorAt` 立刻反映新态;
//   · 占用表**不动**(变体不参与足迹 —— `PlaceableChecker.ComputeOccupiedCells` 的形参无 variant)。
//
// ⚠️ **不发明第二个"门状态"字段** —— 那是第二真源。门的开合只有 `Variant` 一个出处。
// ⚠️ **本类不做判定**:它不看玩家距离 / 不看是否有人正走进门(那归 4 与后续轮)。

using System;
using DaYiJingCheng.Sim.World;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>玩家的「开门 / 关门」意图 → 23 的 `StructureModified`。</summary>
    public sealed class DoorInteractionDriver
    {
        private readonly Func<StructureInstanceRegistry> _structuresOf;
        private readonly Func<IModuleCatalog> _modulesOf;
        private readonly Func<StructureWriter> _writerOf;
        private readonly Func<long> _tick;

        /// <summary>构造。</summary>
        /// <param name="structuresOf">结构实例表来源(读当前 `Variant`;可 null = 未接世界)。</param>
        /// <param name="modulesOf">模块目录(判「这个模块是不是门」;可 null = 不可判)。</param>
        /// <param name="writerOf">23 的写者(可 null = 未装配 ⇒ `Toggle` 返回 false)。</param>
        /// <param name="tick">当前逻辑 tick 来源(事件时点)。</param>
        public DoorInteractionDriver(
            Func<StructureInstanceRegistry> structuresOf,
            Func<IModuleCatalog> modulesOf,
            Func<StructureWriter> writerOf,
            Func<long> tick)
        {
            _structuresOf = structuresOf;
            _modulesOf = modulesOf;
            _writerOf = writerOf;
            _tick = tick ?? throw new ArgumentNullException(nameof(tick));
        }

        /// <summary>
        /// 切换一次门的开合。
        /// </summary>
        /// <param name="structureId">被选中的门的 `structure_id`(4 的 `StableId`)。</param>
        /// <returns>true = 已切换并写出 `StructureModified`;false = 不存在 / 不是门 / 未接线。</returns>
        public bool Toggle(long structureId)
        {
            StructureInstanceRegistry structures = _structuresOf?.Invoke();
            IModuleCatalog modules = _modulesOf?.Invoke();
            StructureWriter writer = _writerOf?.Invoke();
            if (structures == null || modules == null || writer == null) return false;

            if (!structures.TryGet(structureId, out StructureInstance inst)) return false;

            // ⚠️ **只接受门** —— 若 4 的候选分类错了(或载荷被伪造),不得把别的家具的
            //    variant 改掉(那会让 `StructureModified` 静默改一个无关结构)。
            ModuleDefinition? def = modules.GetModuleDefinition(inst.ModuleId);
            if (!def.HasValue || def.Value.SlotType != SlotType.Door) return false;

            if (inst.Variant != 0 && inst.Variant != 1) return false;   // 门的合法态只有两态

            int next = inst.Variant == 0 ? 1 : 0;
            return writer.Modify(structureId, newOrientation: null, newVariant: next, tick: _tick());
        }
    }
}
