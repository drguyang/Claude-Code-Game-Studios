// `com.unity.ai.navigation` 6.3(2.0.14)的 **NavMesh 采样 API 实测尖桩**(关卡工具 story-005 AC-4 / C3)。
//
// 权威来源:
//   `production/epics/level-tool/story-005-world-nav-chunk-slicing.md`(AC-4 · Test Evidence:
//     「C3 的 NavMesh 实测证据:若 `com.unity.ai.navigation` 行为与假设不符,须落 spike 报告」)
//   ADR-022 §四 C3(导航格 ↔ NavMesh **非矛盾** = 告警)· Engine Risk **MEDIUM**(post-cutoff)
//
// ⚠️ **本件只在 batch / 菜单下跑**(`[MenuItem]` + `-executeMethod`),不在构建产物里
//    (`Editor.Tools.Level` 是 Editor-only 程序集,ADR-022 §①)。
//
// ⚠️ **本件回答三个二选一,不回答「C3 该怎么实现」**:
//   ① `NavMesh.SamplePosition` 在**没有 baked NavMesh** 时是返回 false,还是抛 / 静默成功?
//   ② 采样半径的口径:`maxDistance` 是**水平**还是**三维**距离?
//   ③ `NavMesh.CalculatePolygonArea` / `NavMeshBuilder` 在无场景数据的 batch 里可跑吗?
//
// ⚠️ 结论落 `production/qa/evidence/spike-navmesh-6.3-2026-10-29.md`;
//    **本件本身不是判据**,它是勘察工具。

using System;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace DaYiJingCheng.EditorTools.Level
{
    /// <summary>NavMesh 采样 API 的运行时行为探针(batch 用)。</summary>
    public static class NavMeshProbe
    {
        /// <summary>跑三项探针并打出结论行(每行以 `[SPIKE-NAV]` 开头,便于 grep)。</summary>
        /// <remarks>🔁 <b>2026-10-29 拆分</b>(原 51 行 &gt; 40):四项探针各成一个私有方法,
        /// **当场执行**的次序与每行原文逐字不变。</remarks>
        [MenuItem("大医精诚/关卡工具/[spike] NavMesh 采样 API 实测")]
        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[SPIKE-NAV] ==== com.unity.ai.navigation 采样 API 实测 ====");
            sb.AppendLine($"[SPIKE-NAV] unity = {Application.unityVersion}");

            bool sampled = ProbeSampleWithoutNavMesh(sb);
            ProbeMaxDistanceSemantics(sb);
            ProbeTriangulation(sb);
            AppendConclusion(sb, sampled);

            Debug.Log(sb.ToString());
        }

        /// <summary>① 无 baked NavMesh 时 `SamplePosition` 的行为。</summary>
        /// <returns>是否命中(抛异常按「未命中」计,并把异常写进日志)。</returns>
        private static bool ProbeSampleWithoutNavMesh(StringBuilder sb)
        {
            sb.AppendLine($"[SPIKE-NAV] NavMesh.GetSettingsCount = {NavMesh.GetSettingsCount()}");

            bool sampled;
            string sampleOutcome;
            try
            {
                // 原点附近采样,半径 1。空场景 ⇒ 期望「没有 NavMesh」。
                sampled = NavMesh.SamplePosition(Vector3.zero, out NavMeshHit hit, 1.0f, NavMesh.AllAreas);
                sampleOutcome = sampled
                    ? $"true(position={hit.position}, distance={hit.distance}, mask={(int)hit.mask})"
                    : "false(无命中 —— 没有 baked NavMesh)";
            }
            catch (Exception ex)
            {
                sampled = false;
                sampleOutcome = $"抛 {ex.GetType().Name}: {ex.Message}";
            }

            sb.AppendLine($"[SPIKE-NAV] ① SamplePosition(无 NavMesh) => {sampleOutcome}");
            return sampled;
        }

        /// <summary>② `maxDistance` 的口径(水平 vs 三维)。</summary>
        private static void ProbeMaxDistanceSemantics(StringBuilder sb)
        {
            // 空场景里没法测口径(无 NavMesh 时恒 false)⇒ 如实登记「本项在无场景数据时不可判」。
            sb.AppendLine("[SPIKE-NAV] ② maxDistance 口径 => 无 baked NavMesh ⇒ 本项判定不了" +
                          "(须有真实 NavMesh 资产才能测;本 spike 如实登记为 NOT-RUN)");
        }

        /// <summary>③ `NavMeshBuilder` / 三角化在 batch 里是否可跑。</summary>
        private static void ProbeTriangulation(StringBuilder sb)
        {
            try
            {
                NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
                sb.AppendLine($"[SPIKE-NAV] ③ CalculateTriangulation => ok(vertices={tri.vertices?.Length ?? 0}, " +
                              $"indices={tri.indices?.Length ?? 0})");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"[SPIKE-NAV] ③ CalculateTriangulation => 抛 {ex.GetType().Name}: {ex.Message}");
            }
        }

        /// <summary>④ 结论建议(由 ① 的观测值决定,不是常量)。</summary>
        private static void AppendConclusion(StringBuilder sb, bool sampled)
        {
            sb.AppendLine("[SPIKE-NAV] ==== 结论 ====");
            sb.AppendLine(sampled
                ? "[SPIKE-NAV] 空场景也命中 ⇒ C3 **不能**把 SamplePosition 当唯一判据(须先确认有 baked 数据)"
                : "[SPIKE-NAV] 空场景不命中 ⇒ C3 可在「无 NavMesh 数据」时退化为「无法判定」,不误报告警");
        }
    }
}
