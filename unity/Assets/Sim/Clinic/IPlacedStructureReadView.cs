// Story 002 · clinic-machine —— 24「房间析出」的**只读输入视图**(承卡面 AC 第 6 条 / GDD 规则一 / 规则五)。
//
// 权威来源:
//   GDD design/gdd/clinic-machine.md 规则一 —— 24 从 23 的 `structure_at(cell) → module_id + orientation`
//     读**只读布局**;不写占用、不持有任何占用真相(23 的所有权不动摇,规则五)。
//   ADR-015 §三 —— `WorldPos = (i32 x, i32 y, i32 z)` 单一整数格(住 Sim.Contracts,门 A 外/内共用)。
//   story production/epics/clinic-machine/story-002-room-extraction-connectivity.md
//     §Implementation Notes 7 —— 甲案代理判据 + 两条实测陷阱。
//
// 为什么本件**自定**视图而不直接吃 `StructureInstanceRegistry`(两条都是卡面实测的陷阱):
//   ① 注册表**无公开枚举器**(只有 `TryGet(id)` / `GetInstanceAt(anchor)` / `Count` / `Clear`)
//      ⇒ 24 在「不改 23」的前提下**无法**遍历它;
//   ② 本接口没有 Place / Remove / Modify / OccupyCells / FreeCells / Append 中的任何一员,
//      也不暴露 `IEventSink` / `IWorldOccupancy` ⇒ 「24 不调用 23 的写路径」在**类型层面**
//      成立(AC 第 6 条),而不是靠调用纪律。
//
// ⚠️ 本接口**只出格**,刻意不出现 `module_id` / `orientation` / `structure_id`:
//   · `module_id` + `orientation` 是 S17-2(类型匹配 / ROOM_TABLE 子句求值)的输入 ——
//     本 story 的 6 条 AC **不含**类型匹配,提前放进来 = 提前造接缝(卡面实施约束 ④);
//   · `structure_id` 是**写入顺序**的产物(`Register` 自增),不是布局的函数 ——
//     让它进入析出会使 room id 依赖建造历史而非布局,破坏「同一布局 ⇒ 同一 room id」(AC 第 5 条)。
//
// 门 A:`unity/Assets/Sim/Sim.asmdef` 的 `noEngineReferences: true` / 引用集恰 = {BCL, Sim.Contracts}
//   ⇒ 本文件零 `UnityEngine` 引用;`WorldPos` 只来自 `DaYiJingCheng.Sim.Contracts`。

using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.Clinic
{
    /// <summary>
    /// 系统 23(模块化建造)向系统 24(医馆即机器)提供的**只读结构视图**。
    /// <para>由调用方(装配层 / 测试)注入;析出器**只读不写**,且不持有本接口的任何引用
    /// (每次求值现取现用,见 <c>ClinicRoomExtraction.Extract</c> 的「无状态」注释)。</para>
    /// <para>⚠️ 实现方契约:<see cref="StructureCount"/> 须 ≥ 0;
    /// <see cref="GetOccupiedCells"/> 须返回**该实例的完整占用格**(含锚点与全部非锚点足迹格),
    /// 且不得返回 null。返回空集 = 该实例不贡献家具格(合法,例如纯装饰件在真判据下)。</para>
    /// </summary>
    public interface IPlacedStructureReadView
    {
        /// <summary>已放置结构实例数(只含 `StructurePlaced` 物化的实例 —— 外壳格不是实例)。</summary>
        int StructureCount { get; }

        /// <summary>第 <paramref name="structureIndex"/> 件结构实例的**完整占用格**。</summary>
        /// <param name="structureIndex">`[0, StructureCount)` 内的下标。</param>
        /// <returns>该实例占用的全部世界格;不得为 null。</returns>
        /// <remarks>
        /// ⚠️ **陷阱 ①(卡面实测)**:`StructureInstanceRegistry.GetInstanceAt(anchor)` **只比对
        /// `Anchor`,不比对 `OccupiedCells`** —— 多格结构的非锚点格在注册表里查不到,
        /// `World.GetModuleAt` 对它们返回 `-1`。本视图因此**按实例报完整足迹格**,
        /// 调用方不得退化成「逐格查 `GetInstanceAt` / `GetModuleAt`」。
        /// </remarks>
        IReadOnlyList<WorldPos> GetOccupiedCells(int structureIndex);
    }
}
