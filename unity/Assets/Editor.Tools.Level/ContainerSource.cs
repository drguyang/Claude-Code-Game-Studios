// 关卡工具 —— **开档自带的容器物品实例声明**(ADR-022 §三 `world_containers.json`)。
//
// 权威来源:
//   ADR-022 §二(唯一作者 = 关卡工具)· §三(导出契约;本数据集为**加性第 8 类**)
//   ADR-015 §三(逻辑层只有整数格;禁浮点)
//   GDD `interaction-system.md` 规则二(`Container` 的 `StableId = instance_id`,「容器本身是一件掉落」)
//   GDD `inventory-and-items.md` 规则十(BL-3 容器契约)
//
// ── 为什么是**物品实例**而不是结构(2026-11-02 · 用户裁定 B)────────────────────
// 4 的 `Container` kind 的稳定 id 在 GDD 里逐字定为 **`instance_id`**(不是 `structure_id`),
// 且明写「容器本身是一件掉落」。⇒ 柜子必须在**物品实例**空间里,不能复用 23 的
// `StructureInstance`(那是 `structure_id` 空间,与后者是两个 id 空间)。
// 本表因此是**物品实例的基线**(与 `BakedInitial` 结构基线同构:开档既有、无流事件)。
//
// ⚠️ **P0 只有「存在与位置」** —— 容器里装什么(contents)归 20 的容器契约,
//    而 20 今日**没有**「这是容器吗」的判据载体(`ItemCategory` 无 `container` 值,
//    `inventory-and-items.md:595-597`),搜箱时长亦未登记(`OQ-4-4` / `OQ-20-11`)。
//    ⇒ 本表**刻意不含** `contents` 字段:写了就是一个无人消费的第二真源。
//
// ⚠️ **instance_id 的号段与高水位**:本表的 id 会经
//    `IdAuthority.ReconstructItemHighWater` 抬升计数器(装配根接线),
//    使开档之后的掉落不会发到同号(ADR-006 Amendment B「计数器 + 高水位可重构」)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;   // WorldPos

namespace DaYiJingCheng.EditorTools.Level
{
    /// <summary>一个开档自带的容器物品实例(整数格 + 实例 id)。</summary>
    public readonly struct ContainerSpec
    {
        /// <summary>物品实例 id(≥ 0;与掉落共用 `IIdAuthority` 空间)。</summary>
        public readonly long InstanceId;

        /// <summary>所在整数格。</summary>
        public readonly WorldPos Cell;

        /// <summary>构造一个容器实例声明。</summary>
        /// <exception cref="ArgumentOutOfRangeException">`instanceId` 为负。</exception>
        public ContainerSpec(long instanceId, WorldPos cell)
        {
            if (instanceId < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(instanceId), instanceId,
                    "容器实例 id 须 ≥ 0 —— 负值是事件侧的「无实例」哨兵,不得占用");
            InstanceId = instanceId; Cell = cell;
        }
    }

    /// <summary>开档容器实例集合(声明式源;`null` = 本关卡未声明)。</summary>
    public sealed class ContainerSource
    {
        /// <summary>容器实例集(声明序;导出时按 `(instance_id)` 升序重排)。</summary>
        public readonly IReadOnlyList<ContainerSpec> Containers;

        /// <summary>构造容器实例集。</summary>
        /// <exception cref="ArgumentNullException">`containers` 为 null。</exception>
        /// <exception cref="ArgumentException">id 重复 / 同格两条。</exception>
        public ContainerSource(IReadOnlyList<ContainerSpec> containers)
        {
            Containers = containers ?? throw new ArgumentNullException(nameof(containers));

            var seenIds = new HashSet<long>();
            var seenCells = new HashSet<(int, int, int)>();

            for (int i = 0; i < containers.Count; i++)
            {
                ContainerSpec c = containers[i];
                if (!seenIds.Add(c.InstanceId))
                    throw new ArgumentException(
                        $"containers[{i}] 的 instance_id = {c.InstanceId} 重复 —— " +
                        "`StableId` 要求唯一(容器 id 是 F-4.1 的第三键)", nameof(containers));
                if (!seenCells.Add((c.Cell.X, c.Cell.Y, c.Cell.Z)))
                    throw new ArgumentException(
                        $"containers[{i}] 的格 ({c.Cell.X},{c.Cell.Y},{c.Cell.Z}) 出现两条 —— " +
                        "同格两个容器会让「这一格是哪一个」成为隐式规则", nameof(containers));
            }
        }
    }
}
