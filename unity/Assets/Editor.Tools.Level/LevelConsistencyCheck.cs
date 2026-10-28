// ADR-022 §四 六项一致性检查 —— **槽位子集**的执行面。
//
// 权威来源:ADR-022 §四(C1–C6 表)· §②(工具的输出是**构建期失败通道**,失败显式 `throw`,
//          非 `Debug.Assert` —— 后者在非 Dev 构建被剥离)
//
// ═══════════════════════════════════════════════════════════════════════════
// 本件只实现「适用于槽位子集」的那几条;**其余逐条如实登记为未实现**
// ═══════════════════════════════════════════════════════════════════════════
// · C1 两层漂移(告警)        —— **未实现**:需要视觉层地形 / 摆件数据(本批不产出)。
// · C2 可走性同源(slopeLimit/stepOffset) —— **未实现**:需要 `world_terrain.json`(本批不产出)。
// · C2' 模板 collider 足迹同源 —— **未实现**:需要模块模板 prefab(归 23 的素材轮)。
// · C3 导航格 ↔ NavMesh(告警) —— **未实现**:需要 `world_nav_*.json` 与 NavMesh(本批不产出)。
// · C4 多边形合法性            —— **未实现**:需要 `world_ecozones.json`(本批不产出)。
// · C5 边界单一源              —— **未实现**:需要视觉层遮罩(本批不产出)。
// · C6 切片完整性               —— **未实现**:需要 `world_nav_{chunk}.json`(本批不产出)。
//
// 本件实装(全部硬失败,`throw`):
//   ① 重复格           —— 同一 `(x,y,z)` 声明两次(会让「取哪一条」成为隐式规则)
//   ② 类型越界         —— `SlotType.None`(哨兵)/ 枚举未定义值
//   ③ 格超出声明范围   —— 格不在 `[0,W)×[0,H)×[0,D)`
//
// ⚠️ **不假装实现**:上面七条未实装项在 `CheckReport.Unimplemented` 里逐条列出,
//    由调用方(导出器 / 测试)可读 —— 这不是注释里的自述,是返回值里的数据。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.World;   // SlotType(唯一真源,2026-10-28 评审 A-21)

namespace DaYiJingCheng.EditorTools.Level
{
    /// <summary>一致性检查报告(空 = 全绿;`Violations` 非空 ⇒ 调用方须 `throw`)。</summary>
    public sealed class CheckReport
    {
        /// <summary>硬失败项(每条带判据关键词;空 = 绿)。</summary>
        public readonly IReadOnlyList<string> Violations;

        /// <summary>ADR-022 §四 里**本批未实现**的检查项(逐条如实登记,不是注释)。</summary>
        public readonly IReadOnlyList<string> Unimplemented;

        /// <summary>构造报告。</summary>
        public CheckReport(IReadOnlyList<string> violations, IReadOnlyList<string> unimplemented)
        {
            Violations = violations ?? throw new ArgumentNullException(nameof(violations));
            Unimplemented = unimplemented ?? throw new ArgumentNullException(nameof(unimplemented));
        }

        /// <summary>是否全绿。</summary>
        public bool IsGreen => Violations.Count == 0;
    }

    /// <summary>ADR-022 §四 的**槽位子集**一致性检查。</summary>
    public static class LevelConsistencyCheck
    {
        /// <summary>未实现项清单(单一出处;报告与测试共用)。</summary>
        public static readonly string[] UnimplementedChecks =
        {
            "C1 两层漂移(需视觉层数据 —— 本批不产出)",
            "C2 可走性同源(需 world_terrain.json —— 本批不产出)",
            "C2' 模板 collider 足迹同源(需模块 prefab —— 归 23 素材轮)",
            "C3 导航格↔NavMesh(需 world_nav_*.json —— 本批不产出)",
            "C4 多边形合法性(需 world_ecozones.json —— 本批不产出)",
            "C5 边界单一源(需视觉层遮罩 —— 本批不产出)",
            "C6 切片完整性(需 world_nav_{chunk}.json —— 本批不产出)",
        };

        /// <summary>跑槽位子集的三条硬检查(不抛;失败项进 <see cref="CheckReport.Violations"/>)。</summary>
        public static CheckReport Check(LevelSource src)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));

            var violations = new List<string>();
            var seen = new HashSet<(int, int, int)>();

            for (int i = 0; i < src.Slots.Count; i++)
            {
                SlotSpec s = src.Slots[i];
                string where = $"slots[{i}]";

                // ① 重复格
                if (!seen.Add((s.X, s.Y, s.Z)))
                    violations.Add(
                        $"world_buildslots:{where} 格 ({s.X},{s.Y},{s.Z}) 重复 —— " +
                        "同一格两条槽位声明会让「取哪一条」成为隐式规则");

                // ② 类型越界(None = 哨兵;未定义枚举值同样拒)
                if (s.Type == SlotType.None)
                    violations.Add(
                        $"world_buildslots:{where} slot_type = None —— " +
                        "None 是「未分配」哨兵,不是合法槽位类型");
                else if (!Enum.IsDefined(typeof(SlotType), s.Type))
                    violations.Add(
                        $"world_buildslots:{where} slot_type = {(int)s.Type} 不在 SlotType 闭集内");

                // ③ 格超出声明范围
                if (s.X < 0 || s.X >= src.Width || s.Y < 0 || s.Y >= src.Height ||
                    s.Z < 0 || s.Z >= src.Depth)
                    violations.Add(
                        $"world_buildslots:{where} 格 ({s.X},{s.Y},{s.Z}) 超出声明范围 " +
                        $"[0,{src.Width})×[0,{src.Height})×[0,{src.Depth}) —— 越界格在运行期永远不可达");
            }

            return new CheckReport(violations, UnimplementedChecks);
        }

        /// <summary>跑检查并在任一硬失败时 `throw`(ADR-022 §②:构建期失败通道,非 `Debug.Assert`)。</summary>
        /// <exception cref="InvalidOperationException">任一硬失败。</exception>
        public static void CheckOrThrow(LevelSource src)
        {
            CheckReport report = Check(src);
            if (!report.IsGreen)
                throw new InvalidOperationException(
                    "[关卡工具] world_buildslots 一致性检查失败(ADR-022 §四):\n  "
                    + string.Join("\n  ", report.Violations));
        }
    }
}
