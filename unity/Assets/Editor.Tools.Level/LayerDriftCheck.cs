// 关卡工具 —— **C1 两层漂移**(**告警**,不是硬失败)。ADR-022 §四。
//
// 权威来源:
//   ADR-022 §四 C1(逻辑占用格 ↔ 视觉地形不矛盾;**告警**)
//   ADR-015 §Risks(漂移**只影响表现**,不改 sim 判定 —— 这是它是告警而不是硬失败的唯一理由)
//   ADR-022 §四 的 C1 依据格逐字引 `ADR-015:302`
//
// ═══════════════════════════════════════════════════════════════════════════
// 告警类检查的经典假绿:它不跑,也满足「不使构建失败」
// ═══════════════════════════════════════════════════════════════════════════
// 一条「永远不报」的 C1 同时满足 AC-3 的第②条(不使构建失败)。
// ⇒ 本件的判据必须**双向**:既要有「注入漂移 ⇒ 报出」的夹具,也要有
//   「无漂移 ⇒ 不报」的夹具。只有前者时,一个「无条件报」的实现也能过;
//   只有后者时,一个「永不报」的实现也能过。**两条合起来才钉住判据**。
//
// ⚠️ **不判「矛盾」的语义强弱**,只判「两侧说法是否一致」:
//    「逻辑层说这格开档有实体家具」vs「视觉层说这格有东西」——
//    两者必须同真同假。至于「视觉高度 30 mm 够不够看出是张桌子」,那是美术面,不是一致性面。

using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;   // WorldPos

namespace DaYiJingCheng.EditorTools.Level
{
    /// <summary>C1:逻辑层的「开档占用」↔ 视觉层的「这一格有东西」是否一致(**告警**)。</summary>
    public static class LayerDriftCheck
    {
        /// <summary>判据关键词(夹具按它点名)。</summary>
        public const string DriftKey = "C1 两层漂移";

        /// <summary>单次最多报几条(超出只报计数 —— 十万格的关卡逐格刷屏会让告警失去被读的机会)。</summary>
        public const int MaxReported = 20;

        /// <summary>
        /// 跑 C1(不抛;漂移进 `warnings`)。
        /// </summary>
        /// <param name="src">关卡源(逻辑层)。</param>
        /// <param name="visual">视觉层声明;`null` ⇒ 不判(返回 `false`,由调用方登记 NOT-RUN)。</param>
        /// <param name="warnings">输出:漂移告警(最多 <see cref="MaxReported"/> 条 + 一条计数)。</param>
        /// <returns>是否**真的比过**(`false` = 没输入,不是「比过且无漂移」)。</returns>
        /// <remarks>
        /// ⚠️ **返回值的语义是「跑没跑」,不是「绿没绿」** —— 这是本件最容易写错的一处:
        /// 一个返回「无告警」的 `bool` 会让「没输入」与「比过且干净」在类型上不可分,
        /// 而 C1 的全部意义就是区分这两者。
        /// </remarks>
        /// <remarks>🔁 <b>2026-10-29 拆分</b>(原 56 行 &gt; 40):两个方向各成一个私有方法,
        /// 共享的 `reported` / `total` 走 `ref`(顺序敏感:**方向 ① 必须先于方向 ②**,
        /// 否则「截断后先报哪几条」会变)。</remarks>
        public static bool TryCheck(LevelSource src, VisualLayerSource visual, out List<string> warnings)
        {
            warnings = new List<string>();
            if (src == null || visual == null) return false;

            HashSet<(int X, int Y, int Z)> logicSolid = CollectLogicSolid(src);
            int reported = 0, total = 0;

            ReportLogicSolidNotInVisual(src, visual, logicSolid, warnings, ref reported, ref total);
            ReportVisualSolidNotInLogic(src, visual, logicSolid, warnings, ref reported, ref total);

            if (total > MaxReported)
                warnings.Add($"{DriftKey}:另有 {total - MaxReported} 处未逐条列出(共 {total} 处)");

            return true;
        }

        /// <summary>逻辑侧「开档有实体」的格集。</summary>
        /// <remarks>只认 `BakedInitial` 的槽位。⚠️ 非 `BakedInitial` 的槽位是**空地可建** ——
        /// 视觉层上它就该是空的,把它也算成「有实体」会让每一块空地都变成漂移(而那正是正常状态)。</remarks>
        private static HashSet<(int X, int Y, int Z)> CollectLogicSolid(LevelSource src)
        {
            var logicSolid = new HashSet<(int X, int Y, int Z)>();
            for (int i = 0; i < src.Slots.Count; i++)
            {
                SlotSpec s = src.Slots[i];
                if (!s.BakedInitial) continue;
                if (s.X < 0 || s.X >= src.Width || s.Y < 0 || s.Y >= src.Height ||
                    s.Z < 0 || s.Z >= src.Depth) continue;   // 越界由 LevelConsistencyCheck 硬失败,这里不重复
                logicSolid.Add((s.X, s.Y, s.Z));
            }

            return logicSolid;
        }

        /// <summary>方向 ①:逻辑说「有家具」⇒ 视觉不能是空的。</summary>
        private static void ReportLogicSolidNotInVisual(LevelSource src, VisualLayerSource visual,
            HashSet<(int X, int Y, int Z)> logicSolid, List<string> warnings, ref int reported, ref int total)
        {
            foreach ((int x, int y, int z) in logicSolid)
            {
                int idx = (x * src.Height + y) * src.Depth + z;
                if (visual.HasSolidAt(idx)) continue;
                total++;
                if (reported++ < MaxReported)
                    warnings.Add(
                        $"{DriftKey}:格 ({x},{y},{z}) 逻辑层声明**开档有实体**," +
                        "视觉层该格高度为 0 —— 玩家会看见一张桌子凭空出现在空地上");
            }
        }

        /// <summary>方向 ②:视觉说「有东西」⇒ 逻辑层不能是空地。</summary>
        /// <remarks>⚠️ 没有这一半的话,一个「只扫逻辑侧」的实现会让「视觉层多出一堵墙」
        /// 这种漂移永远不被报出 —— 而那正是玩家最常看见的一种(`V-6-7`:未发现的 POI
        /// 不得被视觉提前暴露,反方向同理)。</remarks>
        private static void ReportVisualSolidNotInLogic(LevelSource src, VisualLayerSource visual,
            HashSet<(int X, int Y, int Z)> logicSolid, List<string> warnings, ref int reported, ref int total)
        {
            for (int x = 0; x < src.Width; x++)
                for (int y = 0; y < src.Height; y++)
                    for (int z = 0; z < src.Depth; z++)
                    {
                        int idx = (x * src.Height + y) * src.Depth + z;
                        if (!visual.HasSolidAt(idx)) continue;
                        if (logicSolid.Contains((x, y, z))) continue;
                        total++;
                        if (reported++ < MaxReported)
                            warnings.Add(
                                $"{DriftKey}:格 ({x},{y},{z}) 视觉层有实体(高 " +
                                $"{visual.CellHeightMm[idx]} mm),逻辑层该格**没有开档占用** —— " +
                                "玩家会看见一堵墙挡路,而判定说这里是空地(可建 / 可走)");
                    }
        }
    }
}
