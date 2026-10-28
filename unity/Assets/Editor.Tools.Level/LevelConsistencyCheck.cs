// ADR-022 §四 六项一致性检查 —— **槽位子集**的执行面。
//
// 权威来源:ADR-022 §四(C1–C6 表)· §②(工具的输出是**构建期失败通道**,失败显式 `throw`,
//          非 `Debug.Assert` —— 后者在非 Dev 构建被剥离)
//
// ═══════════════════════════════════════════════════════════════════════════
// 本件只实现「适用于槽位子集」的那几条;**其余逐条如实登记为未实现**
// ═══════════════════════════════════════════════════════════════════════════
// · C1 两层漂移(告警)        —— ✅ **已实现(story-006 · 2026-10-29)**:`LayerDriftCheck`
//     (双向比对:逻辑有家具 ⇒ 视觉不能空;视觉有实体 ⇒ 逻辑不能是空地)。⚠️ 输入 =
//     作者侧 `VisualLayerSource`,今日全库零视觉层资产 ⇒ 生产上必然进 `NotRun`。
// · C2 可走性同源(slopeLimit/stepOffset) —— ✅ **已实现(story-006 · 2026-10-29)**:
//     `WalkabilityParityCheck`(两条独立判据;偏差公式 = `LatticeParamQuantization.IsWithinOneQuantum`)。
//     ✅ **今天唯一能在生产上真跑出结论的一条** —— 引擎侧 = 真 `CharacterController` 探针。
// · C2' 模板 collider 足迹同源 —— ✅ **已实现(story-006 · 2026-10-29)**:`ColliderFootprintCheck`
//     (取**乙路**:作者侧声明 + 检查逻辑;prefab 侧那一半**未跑**,素材归 23)。
// · C3 导航格 ↔ NavMesh(告警) —— ✅ **已实现(story-005 · 2026-10-29)**:`NavMeshConsistencyCheck`
//     (三态 Reachable / Unreachable / Unknown;`Unknown` 不告警,依据 = NavMesh spike)。
//     告警进 `CheckReport.Warnings`,**不进** `Violations` —— 合成一个列表会让「告警」与
//     「硬失败」在类型上不可分,而 C3 的全部语义正是「报出来但不使构建失败」。
// · C4 多边形合法性            —— ✅ **已实现(story-003 · 2026-10-29)**:`EcozonePolygonCheck`
//     (五项:内部重叠 / 顶点<3 / 自交 / 零面积 / 重合顶点),在 `Check` 里随生态区声明一并跑。
// · C5 边界单一源              —— ✅ **已实现(story-003 · 2026-10-29)**:`EcozoneMaskGenerator`(生成)
//     + `EcozoneMaskCheck.Verify`(比对)。⚠️ **乙路(比对已有视觉遮罩)无真实资产可验**,见该件文件头。
// · C6 切片完整性               —— ✅ **已实现(story-005 · 2026-10-29)**:`NavChunkSliceCheck`
//     (漏格 / 重复格 = **两条独立**硬失败)。⚠️ **不在本类里跑** —— 它需要切片集合,
//     而切片是 `LevelExporter.ExportNavChunks` 的产物,由该入口产出后**立即**校验(理由见 `Check` 的注)。
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
//   ⑥ **C4 生态区多边形五项**(story-003;见 `EcozonePolygonCheck`)
//
// ⚠️ **不假装实现**:上面七条未实装项在 `CheckReport.Unimplemented` 里逐条列出,
//    由调用方(导出器 / 测试)可读 —— 这不是注释里的自述,是返回值里的数据。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;   // WorldPos(C3 采样器的格形参)
using DaYiJingCheng.Sim.World;       // SlotType(唯一真源,2026-10-28 评审 A-21)

namespace DaYiJingCheng.EditorTools.Level
{
    /// <summary>一致性检查报告(空 = 全绿;`Violations` 非空 ⇒ 调用方须 `throw`)。</summary>
    public sealed class CheckReport
    {
        /// <summary>硬失败项(每条带判据关键词;空 = 绿)。</summary>
        public readonly IReadOnlyList<string> Violations;

        /// <summary>ADR-022 §四 里**尚未实现**的检查项(逐条如实登记,不是注释)。</summary>
        public readonly IReadOnlyList<string> Unimplemented;

        /// <summary>
        /// **告警**项(ADR-022 §四 里判定为「只影响表现,不退化为构建失败」的那几条 —— 今日 = C3)。
        /// <para>🔁 <b>2026-10-29(story-005 AC-4)新增</b>。与 <see cref="Violations"/> **刻意分家**:
        /// 合成一个列表会让「告警」与「硬失败」在类型上不可分,而 C3 的全部语义就是
        /// 「报出来但**不**使构建失败」。</para>
        /// </summary>
        public readonly IReadOnlyList<string> Warnings;

        /// <summary>
        /// **未跑**项(第三态:检查已实现,但这一次**没有输入可判**)。
        /// <para>🔁 <b>2026-10-29(story-006 AC-1/2/3)新增</b>。与另两态**刻意分家**:
        /// 写进 `Violations` 是**谎报**(把「没输入」说成「有缺陷」),
        /// 写进 `Warnings` 或干脆不写是**借绿**(把「没跑」读成「跑过且干净」)。
        /// 三态在类型上可分,是这个字段存在的全部理由。</para>
        /// <para>先例 = C3 的 `NavReachability.Unknown`(story-005):不知道就明说不知道。</para>
        /// </summary>
        public readonly IReadOnlyList<string> NotRun;

        /// <summary>构造报告。</summary>
        /// <param name="warnings">告警项;`null` = 无告警(向后兼容既有两参调用点)。</param>
        /// <param name="notRun">未跑项;`null` = 无(向后兼容)。</param>
        public CheckReport(
            IReadOnlyList<string> violations, IReadOnlyList<string> unimplemented,
            IReadOnlyList<string> warnings = null, IReadOnlyList<string> notRun = null)
        {
            Violations = violations ?? throw new ArgumentNullException(nameof(violations));
            Unimplemented = unimplemented ?? throw new ArgumentNullException(nameof(unimplemented));
            Warnings = warnings ?? Array.Empty<string>();
            NotRun = notRun ?? Array.Empty<string>();
        }

        /// <summary>是否全绿(**只看硬失败** —— 告警不影响本判据)。</summary>
        public bool IsGreen => Violations.Count == 0;
    }

    /// <summary>ADR-022 §四 的**槽位子集**一致性检查。</summary>
    public static class LevelConsistencyCheck
    {
        /// <summary>未实现项清单(单一出处;报告与测试共用)。</summary>
        /// <remarks>
        /// 🔁 <b>2026-10-29(story-006 AC-5)</b>:由 3 条 → <b>0 条</b>。C1 / C2 / C2' 三条
        /// 本轮全部落地(`LayerDriftCheck` / `WalkabilityParityCheck` / `ColliderFootprintCheck`)。
        /// <para>⚠️ <b>「实现」不等于「今天有结论」</b> —— 其中的 C1 / C2' 需要作者侧声明作输入,
        /// 而今日全库零视觉层资产、零模块 prefab ⇒ 它们在**生产上**必然进 `NotRun`。
        /// 把它读成「三项都验过了」是本仓反复记录的第五种假绿形态
        /// (用一句自述顶替一个从未存在的门)。**判据面如实登记在 `NotRun` 里,不进 `Warnings`。**</para>
        /// </remarks>
        public static readonly string[] UnimplementedChecks = Array.Empty<string>();

        /// <summary>跑槽位子集的硬检查(不抛;失败项进 <see cref="CheckReport.Violations"/>)。
        /// <para>⚠️ C6 不在这里:**它需要切片集合**,而切片是 `ExportNavChunks` 的产物 ——
        /// 由 `LevelExporter` 在产出后立即跑 <see cref="NavChunkSliceCheck"/>。
        /// 把 C6 塞进本方法只能靠「本方法自己再切一遍」,那会让「校验的」与「导出的」变成
        /// 两份可能不同的切片(story AC-3 的判据正是「注入的坏切片要被拦」)。</para>
        /// </summary>
        /// <param name="navSampler">
        /// C3 的采样器;**`null` = 不跑 C3**(本关卡没有 NavMesh 输入 —— 等价于全 `Unknown`,
        /// 即「无告警」,而不是「全部矛盾」)。传 `null` 与传一个恒 `Unknown` 的采样器行为等价,
        /// 但前者不付出逐格调用的代价。
        /// </param>
        /// <param name="engineParams">
        /// 引擎侧的格点参数(C2 的另一半);**`null` = 没有引擎侧输入 ⇒ C2 进 `NotRun`**。
        /// 取法见 `CharacterControllerParamProbe.TryRead`。
        /// </param>
        /// <param name="moduleCatalog">
        /// 模块目录(C2' 的占用格真源);**`null` ⇒ C2' 只跑高度判据**,足迹那一半进 `NotRun`。
        /// </param>
        public static CheckReport Check(LevelSource src, Func<WorldPos, NavReachability> navSampler = null,
            LatticeEngineParams? engineParams = null, IModuleCatalog moduleCatalog = null)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));

            var violations = new List<string>();
            var notRun = new List<string>();
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

            // ── C4 多边形合法性(story-003 起**已实现**;关卡声明了生态区才产生判据)────
            // ⚠️ 「声明了才判」而不是「没声明就判失败」:只声明槽位的关卡 / 夹具是合法用法,
            //    而 `ExportEcozones` 自己会对「未声明」硬失败 —— 两处职责不重复。
            if (src.Ecozones != null
                && EcozonePolygonCheck.TryCheck(src.Ecozones, out List<string> ecoViolations))
                violations.AddRange(ecoViolations);

            // ── C3 导航格 ↔ NavMesh(**告警**,2026-10-29 story-005 起已实现)───────────
            // ⚠️ 告警**不进** `violations` —— C3 的语义就是「报出来但不使构建失败」
            //    (ADR-022 §四)。合成一个列表会让「告警」与「硬失败」在类型上不可分。
            List<string> warnings = null;
            if (navSampler != null)
                NavMeshConsistencyCheck.TryCheck(src, navSampler, out warnings);

            // ── C2 可走性同源(**硬失败**,2026-10-29 story-006 起已实现)────────────────
            // ⚠️ 没有引擎侧输入 ⇒ `NotRun`,**不冒充绿**:C2 的整个语义是「两侧比一比」,
            //    只有一侧时「通过」这个结论根本不存在。
            if (engineParams.HasValue)
            {
                // ⚠️ `TryCheck` 的返回值是「**无违例**」;这里**不看**它 —— 只看它填出来的列表。
                //    (首版写成 `if (TryCheck(...)) AddRange(...)`,即「无违例时把空表加进去」
                //     ⇒ 有违例时一条都不加 ⇒ C2 恒绿。夹具当场抓到,记于此。)
                WalkabilityParityCheck.TryCheck(src.Terrain, engineParams.Value, out List<string> c2);
                violations.AddRange(c2);
            }
            else
            {
                notRun.Add("C2 可走性同源 —— 未提供引擎侧 `CharacterController` 参数" +
                           "(编辑期无 prefab / 场景实例;玩家对象由 BootRoot 运行期创建)");
            }

            // ── C2' 模板 collider 足迹同源(**硬失败**,同上)──────────────────────────
            if (src.ModuleFootprints != null && src.ModuleFootprints.Count > 0)
            {
                int stepMm = src.Terrain != null ? src.Terrain.StepOffsetMm : 0;
                bool ok = ColliderFootprintCheck.TryCheck(
                    src.ModuleFootprints, stepMm, moduleCatalog, out List<string> c2p, out bool fpSkipped);
                if (!ok) violations.AddRange(c2p);
                if (fpSkipped)
                    notRun.Add("C2' 足迹一致性 —— 未提供模块目录(`IModuleCatalog`),只跑了「高 collider」半边");
            }
            else
            {
                notRun.Add("C2' 模板 collider 足迹同源 —— 本关卡未声明模块足迹(模块 prefab 归 23 素材轮)");
            }

            // ── C1 两层漂移(**告警**,同上)──────────────────────────────────────────
            // ⚠️ 返回值是「跑没跑」不是「绿没绿」—— 没跑就没跑,不许落成一条空告警。
            if (LayerDriftCheck.TryCheck(src, src.VisualLayer, out List<string> c1))
            {
                if (c1.Count > 0)
                {
                    if (warnings == null) warnings = new List<string>();
                    warnings.AddRange(c1);
                }
            }
            else
            {
                notRun.Add("C1 两层漂移 —— 本关卡未声明视觉层(今日全库零视觉层资产)");
            }

            return new CheckReport(violations, UnimplementedChecks, warnings, notRun);
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
