// ADR-016 §五 / GDD enemy-ai.md 规则十二/十三/十四/十五 —— 确定性移动与寻路。
//
// 权威来源:
//   ADR-016 §五 —— 整数导航格寻路
//   GDD enemy-ai.md 规则十二 —— 寻路输入 = EffectiveWalkable
//   GDD enemy-ai.md 规则十三 —— 轮询重规划
//   GDD enemy-ai.md 规则十四 —— sim 整数 A*
//   GDD enemy-ai.md 规则十五 —— LogiPose 禁反推
//
// 核心机制:
//   - 定点累加器步进
//   - 整数 A*（OCTILE 启发式）
//   - 路径重规划
//   - LogiPose 输出

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>
    /// 路径节点。
    /// </summary>
    public readonly struct PathNode
    {
        public readonly WorldPos Cell;
        public readonly long F;
        public readonly long H;
        public readonly int G;

        public PathNode(WorldPos cell, long f, long h, int g)
        {
            Cell = cell;
            F = f;
            H = h;
            G = g;
        }
    }

    /// <summary>
    /// A* 寻路结果。
    /// </summary>
    public readonly struct PathResult
    {
        public readonly bool Found;
        public readonly List<WorldPos> Path;
        public readonly int NodesExpanded;

        public PathResult(bool found, List<WorldPos> path, int nodesExpanded)
        {
            Found = found;
            Path = path;
            NodesExpanded = nodesExpanded;
        }
    }

    /// <summary>
    /// 确定性 A* 寻路器。
    /// </summary>
    public static class EnemyPathing
    {
        public const int STEP_COST = 1024;
        public const int NODE_BUDGET = 10000;

        /// <summary>
        /// 计算 OCTILE 启发式（整数近似）。
        /// </summary>
        public static long ComputeOctileHeuristic(WorldPos from, WorldPos to)
        {
            long dx = Math.Abs(from.X - to.X);
            long dy = Math.Abs(from.Y - to.Y);
            long dz = Math.Abs(from.Z - to.Z);

            // 排序
            if (dx > dy) { var t = dx; dx = dy; dy = t; }
            if (dy > dz) { var t = dy; dy = dz; dz = t; }
            if (dx > dy) { var t = dx; dx = dy; dy = t; }

            // OCTILE: (dx + dy + dz) + (sqrt(2) - 2) * min + (sqrt(3) - sqrt(2)) * mid
            // 整数近似: 1414/1000 ≈ sqrt(2), 1732/1000 ≈ sqrt(3)
            long min = dx;
            long mid = dy;
            long max = dz;

            return (min + mid + max) * STEP_COST
                + (1414 - 2000) * min * STEP_COST / 1000
                + (1732 - 1414) * mid * STEP_COST / 1000;
        }

        /// <summary>
        /// 执行 A* 寻路。
        /// </summary>
        public static PathResult FindPath(
            WorldPos start, WorldPos goal,
            Func<WorldPos, bool> isWalkable,
            int nodeBudget = NODE_BUDGET)
        {
            if (start.Equals(goal))
                return new PathResult(true, new List<WorldPos> { start }, 0);

            var openSet = new List<PathNode>();
            var closedSet = new HashSet<WorldPos>();
            var gScore = new Dictionary<WorldPos, long>();
            var cameFrom = new Dictionary<WorldPos, WorldPos>();

            gScore[start] = 0;
            openSet.Add(new PathNode(start, ComputeOctileHeuristic(start, goal), ComputeOctileHeuristic(start, goal), 0));

            int expanded = 0;

            while (openSet.Count > 0 && expanded < nodeBudget)
            {
                // 找最小 F
                int bestIdx = 0;
                for (int i = 1; i < openSet.Count; i++)
                {
                    if (openSet[i].F < openSet[bestIdx].F)
                        bestIdx = i;
                }

                var current = openSet[bestIdx];
                openSet.RemoveAt(bestIdx);
                expanded++;

                if (current.Cell.Equals(goal))
                {
                    // 重建路径
                    var path = new List<WorldPos>();
                    var node = goal;
                    while (!node.Equals(start))
                    {
                        path.Add(node);
                        node = cameFrom[node];
                    }
                    path.Add(start);
                    path.Reverse();
                    return new PathResult(true, path, expanded);
                }

                closedSet.Add(current.Cell);

                // 扩展邻居
                foreach (var neighbor in GetNeighbors(current.Cell))
                {
                    if (closedSet.Contains(neighbor)) continue;
                    if (!isWalkable(neighbor)) continue;

                    long tentativeG = current.G + STEP_COST;

                    if (!gScore.TryGetValue(neighbor, out long existingG) || tentativeG < existingG)
                    {
                        gScore[neighbor] = tentativeG;
                        cameFrom[neighbor] = current.Cell;
                        long h = ComputeOctileHeuristic(neighbor, goal);
                        openSet.Add(new PathNode(neighbor, tentativeG + h, h, (int)tentativeG));
                    }
                }
            }

            // 无路径或超预算
            return new PathResult(false, new List<WorldPos>(), expanded);
        }

        /// <summary>
        /// 获取邻居（6 方向）。
        /// </summary>
        private static IEnumerable<WorldPos> GetNeighbors(WorldPos cell)
        {
            yield return new WorldPos(cell.X + 1, cell.Y, cell.Z);
            yield return new WorldPos(cell.X - 1, cell.Y, cell.Z);
            yield return new WorldPos(cell.X, cell.Y + 1, cell.Z);
            yield return new WorldPos(cell.X, cell.Y - 1, cell.Z);
            yield return new WorldPos(cell.X, cell.Y, cell.Z + 1);
            yield return new WorldPos(cell.X, cell.Y, cell.Z - 1);
        }

        /// <summary>
        /// 定点累加器步进。
        /// </summary>
        public static WorldPos StepAccumulator(Fix speed, ref Fix acc, WorldPos current, WorldPos target)
        {
            acc += speed;
            if (acc.Raw >= Fix.OneRaw)
            {
                acc = Fix.Zero;
                // 向目标移动一步
                int dx = Math.Sign(target.X - current.X);
                int dy = Math.Sign(target.Y - current.Y);
                int dz = Math.Sign(target.Z - current.Z);
                return new WorldPos(current.X + dx, current.Y + dy, current.Z + dz);
            }
            return current;
        }

        /// <summary>
        /// 验证 0 ≤ acc < FIX_ONE。
        /// </summary>
        public static bool ValidateAccumulator(Fix acc)
        {
            return acc.Raw >= 0 && acc.Raw < Fix.OneRaw;
        }
    }
}
