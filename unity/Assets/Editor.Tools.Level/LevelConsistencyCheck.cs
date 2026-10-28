// ADR-022 §四 六项一致性检查 —— **槽位子集**的执行面。
//
// 权威来源:ADR-022 §四(C1–C6 表)· §②(工具的输出是**构建期失败通道**,失败显式 `throw`,
//          非 `Debug.Assert` —— 后者在非 Dev 构建被剥离)
//
// ═══════════════════════════════════════════════════════════════════════════
// 本件只实现「适用于槽位子集」的那几条;**其余逐条如实登记为未实现**
// ═══════════════════════════════════════════════════════════════════════════
// · C1 两层漂移(告警)        —— **未实现**:需要视觉层地形 / 摆件数据(待 S6 产出)。
// · C2 可走性同源(slopeLimit/stepOffset) —— **未实现**:需要 `world_terrain.json`(待 S6;s.t. 待 S2)。
// · C2' 模板 collider 足迹同源 —— **未实现**:需要模块模板 prefab(待 S6;素材归 23 的素材轮)。
// · C3 导航格 ↔ NavMesh(告警) —— **未实现**:需要 `world_nav_*.json` 与 NavMesh(待 S5 产出)。
// · C4 多边形合法性            —— **未实现**:需要 `world_ecozones.json`(待 S3 产出)。
// · C5 边界单一源              —— **未实现**:需要视觉层遮罩(待 S3 产出)。
// · C6 切片完整性               —— **未实现**:需要 `world_nav_{chunk}.json`(待 S5 产出)。
//
// 🔁 **2026-10-29(story-001 AC-4)措辞订正,原文不删**:上面七行原写作「**本批不产出**」——
//    那个「本批」指的是 S20-20 的最小关卡工具卡(它当时只做 `world_buildslots` 一类)。
//    `production/epics/level-tool/` 立件后,「本批」已不存在;继续用它会让读者以为
//    「这些检查永远不会有」,而事实是它们**各有归属 story**(S3 / S5 / S6)。
//    ⇒ 措辞改为「待 Sx 产出」;**条数与实现状态一个字都没变**(仍 7 条,仍全部未实现)。
//    夹具 `test_ac4_*` 用否定断言钉住「本批」不得回归。
//
// 本件实装(全部硬失败,`throw`):
//   ① 重复格           —— 同一 `(x,y,z)` 声明两次(会让「取哪一条」成为隐式规则)
//   ② 类型越界         —— `SlotType.None`(哨兵)/ 枚举未定义值
//   ③ 格超出声明范围   —— 格不在 `[0,W)×[0,H)×[0,D)`
//   ③之补 槽位落在**不可走**的格上(2026-10-29 · story-002;仅当关卡声明了可走性)
//   ④ `baked_initial = true` 却无 `module_id`(2026-10-28 · schema 2)
//   ⑤ `baked_initial = false` 却写了 `module_id`(同上,反方向)
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

        /// <summary>ADR-022 §四 里**尚未实现**的检查项(逐条如实登记,不是注释)。</summary>
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
            "C1 两层漂移(需视觉层数据 —— 待 S6 产出)",
            "C2 可走性同源(需 world_terrain.json —— 待 S6 产出;该数据集本身待 S2)",
            "C2' 模板 collider 足迹同源(需模块 prefab —— 待 S6 产出;素材归 23 素材轮)",
            "C3 导航格↔NavMesh(需 world_nav_*.json —— 待 S5 产出)",
            "C4 多边形合法性(需 world_ecozones.json —— 待 S3 产出)",
            "C5 边界单一源(需视觉层遮罩 —— 待 S3 产出)",
            "C6 切片完整性(需 world_nav_{chunk}.json —— 待 S5 产出)",
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

                // ③之补(2026-10-29 · story-002):槽位落在**不可走**的格上。
                //    ⚠️ 只在关卡声明了可走性时才可判(`Walkable == null` = 本关卡只声明槽位,
                //    几何归别的关卡 / 夹具)⇒ 未声明就**不产生这条判据**,而不是默认「全可走」。
                //    这条是 story-002 引入「外环不可走」时**必须**有的配套:本轮脚手架关卡真的
                //    踩了它(第四格 `(7,1,7)` 落在 8×2×8 的边界环上,已改 `(6,1,6)`)——
                //    没有这条判据,那个错误只能靠人眼在 128 个数里发现。
                if (src.Walkable != null && (s.X >= 0 && s.X < src.Width
                                             && s.Y >= 0 && s.Y < src.Height
                                             && s.Z >= 0 && s.Z < src.Depth))
                {
                    int idx = (s.X * src.Height + s.Y) * src.Depth + s.Z;
                    if (src.Walkable[idx] == 0)
                        violations.Add(
                            $"world_buildslots:{where} 格 ({s.X},{s.Y},{s.Z}) **不可通行**却声明了槽位 —— " +
                            "入口永远无法抵达该格 ⇒ 那条声明在运行期是死的(玩家造不到、也拆不到)");
                }

                // ④ `baked_initial = true` ⇒ 必须指明是哪件家具(2026-10-28 · schema 2)
                //    ⚠️ 这条**在导出期**就拦,而不是等绑定器 —— 导出器是唯一作者,
                //    作者的错在作者自己的工具里报,报文才指向「你少写了一个字段」。
                //    🔁 **2026-10-28 评审 S-1 修复(原文不删,就地加注)**:首版判
                //    `s.ModuleId == SlotSpec.NoModule`(即 `== -1`),而 ADR-022 `:176` 的契约是
                //    **`module_id ≥ 1`**。差集 = `module_id = 0` 从导出门**漏过去**,
                //    只有绑定器 / codec 才拒 —— 而导出器是「唯一作者」,作者的错就该在作者的
                //    工具里报。现按契约判 `≤ 0`(`NoModule = -1` 与 `0` 一并拒),与 ADR 逐字对齐。
                if (s.BakedInitial && s.ModuleId <= 0)
                    violations.Add(
                        $"world_buildslots:{where} 标了 baked_initial = true 却" +
                        (s.ModuleId == SlotSpec.NoModule ? "没有 module_id" : $"给了非法 module_id = {s.ModuleId}") +
                        " —— 开档既有家具必须指明是哪件家具,且 `module_id ≥ 1`(ADR-022);" +
                        "运行期要按它算足迹并发 structure_id(GDD 规则三);" +
                        "没写 / 写 0 = 那格开档「声明了占用但说不出被什么占用」");

                // ⑤ `baked_initial = false` 却写了 module_id ⇒ 语义矛盾
                //    ⚠️ 反方向的错同样要拦:它会让读者以为「这格有家具但没标初始占用」,
                //    而实际行为是「空地可建」—— 两个字段的取值互相打脸。
                if (!s.BakedInitial && s.ModuleId != SlotSpec.NoModule)
                    violations.Add(
                        $"world_buildslots:{where} 标了 baked_initial = false 却写了 " +
                        $"module_id = {s.ModuleId} —— 二者矛盾(非初始占用的槽位没有既有家具," +
                        "运行期会把它当空地);要声明开档家具请同时把 baked_initial 置 true");
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
