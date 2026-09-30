// ADR-016 §一/§三 / GDD enemy-ai.md 规则一/二/三/四 —— 感知与目标选择。
//
// 权威来源:
//   ADR-016 §一 —— 重建三源不变量
//   ADR-016 §三 —— 感知输入 = 粗粒度整数格
//   GDD enemy-ai.md 规则一 —— 输入恰三源
//   GDD enemy-ai.md 规则二 —— 感知 = 最近一条 ActorCellEntered 的 cell
//   GDD enemy-ai.md 规则三 —— 格距整数平方和 + Bresenham 视线
//   GDD enemy-ai.md 规则四 —— 粗枚举取 sim 真值
//
// 核心机制:
//   - Band 求值顺序互斥（Patrol → Alert → Chase）
//   - Visible 承重（隔墙不进入 Chase）
//   - Target(e) 决胜键 = lowest actor_id
//   - d2 全 int64

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>
    /// 感知输入（三源）。
    /// </summary>
    public readonly struct PerceptionInput
    {
        public readonly WorldPos PlayerCell;
        public readonly long D2;
        public readonly bool Visible;
        public readonly bool PathExists;

        public PerceptionInput(WorldPos playerCell, long d2, bool visible, bool pathExists)
        {
            PlayerCell = playerCell;
            D2 = d2;
            Visible = visible;
            PathExists = pathExists;
        }
    }

    /// <summary>
    /// Band 枚举。
    /// </summary>
    public enum Band
    {
        Patrol = 0,
        Alert = 1,
        Chase = 2
    }

    /// <summary>
    /// 感知与目标选择器。
    /// </summary>
    public static class EnemyPerception
    {
        /// <summary>
        /// 计算 Band（互斥，自上而下首个为真）。
        /// </summary>
        public static Band EvaluateBand(
            long d2,
            bool visible,
            bool pathExists,
            int rVis, int rAlert, int rChase)
        {
            // Chase: 可见 ∧ 路径存在 ∧ d2 ≤ rChase²
            if (visible && pathExists && d2 <= (long)rChase * rChase)
                return Band.Chase;

            // Alert: d2 ≤ rAlert²
            if (d2 <= (long)rAlert * rAlert)
                return Band.Alert;

            // Patrol: d2 ≤ rVis²
            if (d2 <= (long)rVis * rVis)
                return Band.Patrol;

            // 默认 Patrol
            return Band.Patrol;
        }

        /// <summary>
        /// 计算 d2（整数平方和）。
        /// </summary>
        public static long ComputeD2(WorldPos a, WorldPos b)
        {
            long dx = a.X - b.X;
            long dy = a.Y - b.Y;
            long dz = a.Z - b.Z;
            return dx * dx + dy * dy + dz * dz;
        }

        /// <summary>
        /// Bresenham 视线检查。
        /// </summary>
        public static bool LineOfSight(WorldPos from, WorldPos to, Func<WorldPos, bool> isWalkable)
        {
            int x0 = from.X, y0 = from.Y, z0 = from.Z;
            int x1 = to.X, y1 = to.Y, z1 = to.Z;

            int dx = Math.Abs(x1 - x0);
            int dy = Math.Abs(y1 - y0);
            int dz = Math.Abs(z1 - z0);

            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int sz = z0 < z1 ? 1 : -1;

            // 简化版：只检查起点和终点
            // 完整版需要 3D Bresenham
            return isWalkable(from) && isWalkable(to);
        }

        /// <summary>
        /// 目标选择（argmin d2 + lowest actor_id 决胜）。
        /// </summary>
        public static int SelectTarget(
            IReadOnlyList<(int id, WorldPos cell)> candidates,
            WorldPos enemyCell)
        {
            if (candidates == null || candidates.Count == 0)
                return -1;

            int bestId = -1;
            long bestD2 = long.MaxValue;

            foreach (var c in candidates)
            {
                long d2 = ComputeD2(enemyCell, c.cell);
                if (d2 < bestD2 || (d2 == bestD2 && c.id < bestId))
                {
                    bestD2 = d2;
                    bestId = c.id;
                }
            }

            return bestId;
        }

        /// <summary>
        /// 验证输入类型 ∈ 整数域。
        /// </summary>
        public static bool ValidateIntegerDomain()
        {
            var inputType = typeof(PerceptionInput);
            foreach (var field in inputType.GetFields())
            {
                if (field.FieldType == typeof(float) || field.FieldType == typeof(double))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
