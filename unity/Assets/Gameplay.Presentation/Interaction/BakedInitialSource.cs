// 第四源的生产实装 —— 23 的 `BakedInitial`(开档自带的构建物 + 开档容器)。
//
// 权威来源:
//   GDD `interaction-system.md` 规则二(四源;第四源 = 23 `BakedInitial`)
//   · `AC-4-22`(漏第四源 ⇒ 开档门柜不可交互且**不报错**)
//   · ADR-009 §四(候选集 = 派生态:每次现算,不进流、不存档)
//   · ADR-016 §三(只出整数格,禁表现态位置)
//   · 2026-11-02 用户裁定 B:柜 = **容器物品实例**(`instance_id` 空间,不是 `structure_id`)
//
// ── 本类补的是什么(2026-11-02 · `AC-4-22` 兑现轮)────────────────────────────
// `IBakedInitialSource` 自 story-003 起只有**接口 + 测试替身**,生产装配里
// `CandidateSources.BakedInitial` 恒 `null` ⇒ 开档门柜**不在候选集里**。
// 本类把两份真实来源接上:
//   ① **结构基线** —— `StructureInstanceRegistry` 里 `structure_id ∈ [1, N]` 的实例
//      (`N = World.BakedInitialStructureCount`;ADR-033 平移后,基线恒占 `1..N`)。
//      按模块目录的 `SlotType` 分类:门 → `Doors`(P0 唯一有数据的一类)。
//   ② **容器物品实例** —— `world_containers.cooked` 的行(关卡烘焙,无流事件)。
//
// ── 为什么按 `structure_id ≤ N` 过滤,而不是「所有实例」────────────────────────
// 玩家**新建**的门不在第四源里 —— 第四源的定义是「开档既有、无流事件」。
// 玩家放置的结构有 `StructurePlaced` 事件(它们属于流侧),把它也算进第四源会让
// 「源」与「事件」重复计数(同一件目标进候选集两次,`argmin` 的第三键相同 ⇒ 不是错,
// 但是两套真源)。⇒ 只取基线号段。
//
// ⚠️ 本类**不持状态**:`Rows` 属性每次现算(候选集是派生态)。注册表 / 目录由调用方注入。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;   // WorldPos
using DaYiJingCheng.Sim.World;       // StructureInstance / StructureInstanceRegistry / SlotType / IModuleCatalog

namespace DaYiJingCheng.Gameplay.Interaction
{
    /// <summary>
    /// 第四源的生产实装:开档结构基线(按模块类型分类)+ 开档容器实例。
    /// </summary>
    public sealed class BakedInitialSource : IBakedInitialSource
    {
        private readonly Func<IEnumerable<StructureInstance>> _instancesOf;
        private readonly Func<IModuleCatalog> _modulesOf;
        private readonly Func<int> _baselineCountOf;
        private readonly Func<IReadOnlyList<ContainerDto>> _containersOf;

        /// <summary>构造第四源。</summary>
        /// <param name="instancesOf">结构实例的只读枚举来源(生产 = `world.Structures.EnumerateInstances`;
        /// <c>null</c> = 未接世界 ⇒ 结构切片为空)。</param>
        /// <param name="modulesOf">模块目录来源(分类依据 = `SlotType`;<c>null</c> = 无目录 ⇒ 结构切片为空,
        /// 因为「这个模块是不是门」不可判)。</param>
        /// <param name="baselineCountOf">基线实例数 `N`(`structure_id ∈ [1, N]` 才是开档既有;
        /// <c>null</c> = 不裁基线号段,全部实例都算 —— **生产不得这样用**)。</param>
        /// <param name="containersOf">开档容器实例来源(烘焙表;<c>null</c> 或空 ⇒ 容器切片为空)。</param>
        public BakedInitialSource(
            Func<IEnumerable<StructureInstance>> instancesOf = null,
            Func<IModuleCatalog> modulesOf = null,
            Func<int> baselineCountOf = null,
            Func<IReadOnlyList<ContainerDto>> containersOf = null)
        {
            _instancesOf = instancesOf;
            _modulesOf = modulesOf;
            _baselineCountOf = baselineCountOf;
            _containersOf = containersOf;
        }

        /// <inheritdoc/>
        /// <remarks>P0 无器具数据(`build_modules.json` 无对应 SlotType)⇒ 恒空。**不是漏接**:
        /// `Utensil` 的分类依据尚未在 23 的目录里存在。</remarks>
        public IReadOnlyList<UtensilDto> Utensils => Array.Empty<UtensilDto>();

        /// <inheritdoc/>
        /// <remarks>P0 无医馆面板数据 ⇒ 恒空(同上)。</remarks>
        public IReadOnlyList<ClinicPanelDto> ClinicPanels => Array.Empty<ClinicPanelDto>();

        /// <inheritdoc/>
        public IReadOnlyList<DoorDto> Doors
        {
            get
            {
                IModuleCatalog modules = _modulesOf?.Invoke();
                IEnumerable<StructureInstance> instances = _instancesOf?.Invoke();
                if (modules == null || instances == null)
                    return Array.Empty<DoorDto>();

                int baseline = _baselineCountOf?.Invoke() ?? int.MaxValue;

                List<DoorDto> rows = null;
                foreach (StructureInstance inst in instances)
                {
                    if (inst.StructureId > baseline) continue;          // 玩家新建的,不属第四源
                    if (!IsSlotType(modules, inst.ModuleId, SlotType.Door)) continue;
                    (rows ??= new List<DoorDto>()).Add(new DoorDto(inst.Anchor, inst.StructureId));
                }
                return rows ?? (IReadOnlyList<DoorDto>)Array.Empty<DoorDto>();
            }
        }

        /// <inheritdoc/>
        /// <remarks>P0 无开关数据 ⇒ 恒空(同上)。</remarks>
        public IReadOnlyList<SwitchDto> Switches => Array.Empty<SwitchDto>();

        /// <inheritdoc/>
        public IReadOnlyList<ContainerDto> Containers
            => _containersOf?.Invoke() ?? (IReadOnlyList<ContainerDto>)Array.Empty<ContainerDto>();

        /// <summary>模块是否属于某 `SlotType`(目录查无该模块 ⇒ false,不抛)。</summary>
        private static bool IsSlotType(IModuleCatalog modules, int moduleId, SlotType type)
        {
            ModuleDefinition? def = modules.GetModuleDefinition(moduleId);
            return def.HasValue && def.Value.SlotType == type;
        }
    }
}
