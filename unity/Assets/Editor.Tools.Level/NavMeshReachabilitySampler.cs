// C3 的**生产采样器**(NavMesh → 三态)。与判据逻辑分家:
// `NavMeshConsistencyCheck` 只吃 `Func<WorldPos, NavReachability>`,不碰引擎 API,
// 所以判据可以在 EditMode 里用合成采样器直驱(AC-4 的两个方向都要可注入地验)。
//
// 权威来源:
//   `production/qa/evidence/spike-navmesh-6.3-2026-10-29.md`(实测三项,本文件的每一步都对应其一)
//   · ADR-022 §四 C3(告警级)· ADR-015 §三(格 → 毫米)
//
// ═══════════════════════════════════════════════════════════════════════════
// §实测事实(spike,2026-10-29 · Unity 6.3 · com.unity.ai.navigation 2.0.14)
// ═══════════════════════════════════════════════════════════════════════════
//   ① 无 baked NavMesh 时 `NavMesh.SamplePosition` 返回 **false**(不抛、不假成功)
//   ② `NavMesh.CalculateTriangulation()` 无数据时返回 **ok, vertices=0, indices=0**
//   ③ `maxDistance` 的口径(水平 / 三维)**NOT-RUN** —— 无真实资产,如实登记
//
//   ⇒ 存在性判据 = `CalculateTriangulation().vertices.Length == 0` ⇒ 全 `Unknown`。
//   ⇒ 采样半径 = **格边长**(见下),不依赖 ③ 的口径。
//
// ⚠️ **③ NOT-RUN 的后果**:真实 NavMesh 资产到位后,若 `maxDistance` 是三维距离,
//    那么「地板上一格」与「天花板上同 x/z 的一格」可能互相命中 —— 告警条数会变,
//    但**判据成立与否不变**(那是「报不报」的问题,不是「报得准不准」的问题)。
//    该口径在本 story 的 Out of Scope 之外(需真实资产),故如实留白。

using System;
using UnityEngine;
using UnityEngine.AI;
using DaYiJingCheng.Sim.Contracts;   // WorldPos

namespace DaYiJingCheng.EditorTools.Level
{
    /// <summary>C3 的生产采样器:把整数格映射到世界坐标后向 NavMesh 采样。</summary>
    public static class NavMeshReachabilitySampler
    {
        /// <summary>本场景是否有 NavMesh 数据(缓存;spike 判据 ② 的落地)。</summary>
        /// <remarks>⚠️ 缓存是**刻意的**:同一个进程里每导出一个关卡就调一次
        /// `CalculateTriangulation()` 会白白分配整张三角网。NavMesh 在编辑器会话内
        /// 只在烘焙 / 打开场景时变 —— 而导出是一条批处理,不需要实时语义。</remarks>
        private static bool? _hasNavMesh;

        /// <summary>清空存在性缓存(测试用;生产零调用点)。</summary>
        public static void ResetCache() => _hasNavMesh = null;

        /// <summary>本场景是否有 NavMesh 数据(`vertices.Length == 0` ⇒ false)。</summary>
        public static bool HasNavMeshData()
        {
            if (_hasNavMesh.HasValue) return _hasNavMesh.Value;
            bool value;
            try
            {
                NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
                value = tri.vertices != null && tri.vertices.Length > 0;
            }
            catch (Exception)
            {
                // 拿不到三角网 ⇒ 「无数据」是**安全**的那一侧:退化为 Unknown ⇒ 不告警,
                // 而不是把整个关卡报成不可达(那会淹没真告警,见 NavMeshConsistencyCheck 文件头)。
                value = false;
            }
            _hasNavMesh = value;
            return value;
        }

        /// <summary>
        /// 采样一格。<paramref name="latticeSizeMm"/> 用于把格坐标换成米制世界坐标
        /// (ADR-015 §三:格的物理尺寸以整数毫米承载)。
        /// </summary>
        /// <param name="cell">整数格。</param>
        /// <param name="latticeSizeMm">格边长(毫米;须 &gt; 0)。</param>
        public static NavReachability Sample(WorldPos cell, int latticeSizeMm)
        {
            if (!HasNavMeshData()) return NavReachability.Unknown;
            if (latticeSizeMm <= 0) return NavReachability.Unknown;

            float metresPerCell = latticeSizeMm / 1000f;
            // 取格中心,而非格角:角点落在四格交界,采样命中的是**邻格**的地面。
            var worldPoint = new Vector3(
                (cell.X + 0.5f) * metresPerCell,
                (cell.Y + 0.5f) * metresPerCell,
                (cell.Z + 0.5f) * metresPerCell);

            // 半径 = 一格:小于它,格中心的浮点误差会让本该命中的格采样落空;
            // 大于它,则会命中隔邻格(chunk 边界处整片误报成可达)。
            bool hit = NavMesh.SamplePosition(worldPoint, out _, metresPerCell, NavMesh.AllAreas);
            return hit ? NavReachability.Reachable : NavReachability.Unreachable;
        }

        /// <summary>按关卡源构造采样委托(<see cref="NavMeshConsistencyCheck.TryCheck"/> 的形参)。</summary>
        /// <param name="src">关卡源(取 <see cref="LevelSource.LatticeSizeMm"/>)。</param>
        /// <exception cref="ArgumentNullException"><paramref name="src"/> 为 null。</exception>
        public static Func<WorldPos, NavReachability> For(LevelSource src)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));
            int latticeSizeMm = src.LatticeSizeMm;
            return cell => Sample(cell, latticeSizeMm);
        }
    }
}
