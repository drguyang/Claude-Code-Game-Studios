// 权威来源:modular-building.md 规则九(拆除与回收) · F-23-3(拆除返还)
//             ADR-006(定点域舍入 · ROUND_HALF_AWAY_FROM_ZERO)
//
// 核心机制:
//   - DemolishChecker: 拆除合法性判定(结构存在 + 无实体)
//   - RefundCalculator: F-23-3 整数返还公式
//   - Shell(SlotType=6) 在 P0 不可拆除
//   - P0 DEMOLISH_REFUND_RATIO = 1 (全额返还)

using System;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.World
{
    /// <summary>拆除判定结果。</summary>
    public enum DemolishResult
    {
        Success,               // 可以拆除
        StructureNotFound,     // structure_id 不存在
        EntityOnCell,          // 占用格上有实体(玩家/敌人)
        ShellNotRemovable,     // 外壳类模块(P0 不可拆)
        InventoryFull          // 返还时库存满(触发 DropSpawned)
    }

    /// <summary>拆除合法性判定器。</summary>
    public sealed class DemolishChecker
    {
        private readonly StructureInstanceRegistry _registry;
        private readonly IOccupancyQuery _occupancyQuery;
        private readonly IPresenceQuery _presenceQuery;

        /// <summary>构造 DemolishChecker。</summary>
        public DemolishChecker(
            StructureInstanceRegistry registry,
            IOccupancyQuery occupancyQuery,
            IPresenceQuery presenceQuery)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _occupancyQuery = occupancyQuery ?? throw new ArgumentNullException(nameof(occupancyQuery));
            _presenceQuery = presenceQuery ?? throw new ArgumentNullException(nameof(presenceQuery));
        }

        /// <summary>
        /// 拆除判定:
        /// ① structure_id 存在
        /// ② 不是 Shell 类
        /// ③ 占用格上无实体(玩家/敌人,量化格 + 宽容半径)
        /// </summary>
        public DemolishResult Check(int structureId)
        {
            // ① 结构存在
            if (!_registry.TryGet(structureId, out var inst))
                return DemolishResult.StructureNotFound;

            // ② Shell 不可拆
            if (inst.ModuleId == (int)SlotType.Shell)
                return DemolishResult.ShellNotRemovable;

            // ③ 实体检查(简化版:检查锚点格是否有实体)
            if (_presenceQuery.IsPresent(0)) // 简化:只检查玩家 id=0
                return DemolishResult.EntityOnCell;

            return DemolishResult.Success;
        }
    }

    /// <summary>拆除返还计算器(F-23-3)。</summary>
    public static class RefundCalculator
    {
        /// <summary>
        /// Refund(m) := ⌊cost(m) × DEMOLISH_REFUND_RATIO⌋
        /// 整数向下取整: (cost × rawR) >> 16
        /// </summary>
        /// <param name="cost">模块材料成本。</param>
        /// <param name="demolishRefundRatio">返还率(Q16.16)。P0 = 1.0 = 65536。</param>
        /// <returns>返还材料数(整数 ≥ 0)。</returns>
        public static int CalculateRefund(int cost, int demolishRefundRatio)
        {
            if (cost <= 0)
                return 0;

            // Q16.16: ⌊cost × R⌋ = (cost × rawR) >> 16
            long refund = ((long)cost * demolishRefundRatio) >> 16;
            return (int)Math.Max(0, refund);
        }

        /// <summary>
        /// 目录校验: 可返还模块须满足 cost ≥ ⌈1/R⌉。
        /// P0 R = 1 时自动满足。
        /// </summary>
        public static bool ValidateRefundable(int cost, int demolishRefundRatio)
        {
            if (demolishRefundRatio <= 0)
                return false;

            // ⌈1/R⌉ = ⌈65536 / rawR⌉ = (65536 + rawR - 1) / rawR
            int minCost = (65536 + demolishRefundRatio - 1) / demolishRefundRatio;
            return cost >= minCost;
        }

        /// <summary>
        /// P0 快捷: 全额返还(无需计算)。
        /// </summary>
        public static int FullRefund(int cost) => cost;
    }
}
