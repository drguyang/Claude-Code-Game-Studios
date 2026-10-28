// ADR-022 §四 **C3 导航格 ↔ NavMesh 非矛盾**(= **告警**,不是硬失败)。
//
// 权威来源:
//   ADR-022 §四 C3 逐字:「逻辑可走与 NavMesh 可走**不矛盾**(不要求逐格相等)= 告警」
//   ADR-016 §四(NavMesh 仅驱动**表现态位移**;寻路走整数导航格)
//   `production/epics/level-tool/story-005-world-nav-chunk-slicing.md` AC-4
//   `production/qa/evidence/spike-navmesh-6.3-2026-10-29.md`(**本检查的三态设计依据**)
//
// ═══════════════════════════════════════════════════════════════════════════
// §为什么是**三态**而不是布尔(2026-10-29 spike 的结论)
// ═══════════════════════════════════════════════════════════════════════════
//   spike 实测:`NavMesh.CalculateTriangulation()` 在**没有 baked NavMesh** 时返回
//   `vertices.Length == 0`,既不抛也不假成功 ⇒ 它是「本场景到底有没有 NavMesh 数据」
//   的**可靠**存在性探针。据此分三态:
//     · Reachable   —— 有数据且采样命中
//     · Unreachable —— 有数据且采样不命中
//     · Unknown     —— **本场景根本没有 NavMesh 数据**
//
//   `Unknown` **不告警**。这不是完备性上的讲究,是**必需**:P0 至今没有任何 baked NavMesh
//   资产(P0 关卡是逻辑格 + 抽象表现),若把「无数据」当成「全不可达」,C3 会对**每一个**
//   逻辑可走的格报一次告警 —— 上千条噪声会把真告警淹掉,而那种「跑一次就满屏警告」的检查
//   在实践中等于不存在。
//
// ═══════════════════════════════════════════════════════════════════════════
// §两个方向都要检
// ═══════════════════════════════════════════════════════════════════════════
//   · 逻辑 **可走** / NavMesh **不可达** ⇒ 告警(AI 会绕过玩家走得通的地方 —— 玩家看得见,AI 看不见)
//   · 逻辑 **不可走** / NavMesh **可达** ⇒ 告警(AI 会走进玩家走不通的地方 —— 表现穿墙)
//   只检一个方向会让另一半漂移**静默**(story-005 §Implementation Notes 末段)。
//
// ⚠️ **本检查永不抛**(AC-4 ②「不使构建失败」)。一个「永远不报」的实现同样满足 ②,
//    所以 AC-4 ① 的判据是**必须验「报出」** —— 夹具注入矛盾并要求 `warnings` 非空。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;   // WorldPos

namespace DaYiJingCheng.EditorTools.Level
{
    /// <summary>某格在 NavMesh 上的可达性(三态;`Unknown` = 本场景无 NavMesh 数据)。</summary>
    public enum NavReachability
    {
        /// <summary>本场景**没有** NavMesh 数据 —— 不告警(spike 结论,见文件头)。</summary>
        Unknown = 0,

        /// <summary>有数据且采样命中。</summary>
        Reachable = 1,

        /// <summary>有数据且采样不命中。</summary>
        Unreachable = 2,
    }

    /// <summary>ADR-022 §四 **C3** 的实现面。</summary>
    public static class NavMeshConsistencyCheck
    {
        /// <summary>告警报出上限(防一整张世界全矛盾时刷屏;超出即截断,不静默 —— 报文末行会说明被截断)。</summary>
        public const int MaxReported = 20;

        /// <summary>
        /// 逐格比对「逻辑可走性」与「NavMesh 可达性」。
        /// </summary>
        /// <param name="src">关卡源(**须**声明了每格可走性 —— 否则本检查无事可做)。</param>
        /// <param name="sampler">格 → 三态的可达性采样(夹具注入合成采样器;生产见
        /// <see cref="NavMeshReachabilitySampler"/>)。</param>
        /// <param name="warnings">告警项(空 = 无矛盾 / 全 `Unknown`)。</param>
        /// <returns>是否无告警。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="src"/> / <paramref name="sampler"/> 为 null。</exception>
        public static bool TryCheck(
            LevelSource src, Func<WorldPos, NavReachability> sampler, out List<string> warnings)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));
            if (sampler == null) throw new ArgumentNullException(nameof(sampler));

            warnings = new List<string>();

            // 未声明可走性 ⇒ 本条检查没有判据面(不是「全绿」,是「无事可做」)。
            // ⚠️ 与 `ExportGeometry` 的口径一致:未声明 ≠ 全可走。
            if (src.Walkable == null) return true;

            int truncated = 0;
            for (int x = 0; x < src.Width; x++)
                for (int y = 0; y < src.Height; y++)
                    for (int z = 0; z < src.Depth; z++)
                    {
                        int idx = (x * src.Height + y) * src.Depth + z;
                        bool logicalWalkable = src.Walkable[idx] == 1;

                        NavReachability reach = sampler(new WorldPos(x, y, z));
                        if (reach == NavReachability.Unknown) continue;   // 无 NavMesh 数据 ⇒ 不告警

                        bool navReachable = reach == NavReachability.Reachable;

                        if (logicalWalkable && !navReachable)
                        {
                            if (warnings.Count >= MaxReported) { truncated++; continue; }
                            warnings.Add(
                                $"[关卡工具] C3 格 ({x},{y},{z}) **逻辑可走**但 NavMesh **不可达** —— " +
                                "AI 会绕过玩家走得通的地方(玩家看得见、AI 看不见);" +
                                "该矛盾只影响表现,不使构建失败(ADR-022 §四 C3)");
                        }
                        else if (!logicalWalkable && navReachable)
                        {
                            if (warnings.Count >= MaxReported) { truncated++; continue; }
                            warnings.Add(
                                $"[关卡工具] C3 格 ({x},{y},{z}) **逻辑不可走**但 NavMesh **可达** —— " +
                                "AI 会走进玩家走不通的地方(表现穿墙);" +
                                "该矛盾只影响表现,不使构建失败(ADR-022 §四 C3)");
                        }
                    }

            if (truncated > 0)
                warnings.Add(
                    $"[关卡工具] C3 另有 {truncated} 处矛盾未逐条列出(报出上限 {MaxReported}) —— " +
                    "截断是为了可读;总数不为 0 就说明两侧仍有漂移");

            return warnings.Count == 0;
        }
    }
}
