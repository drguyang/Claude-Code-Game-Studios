// Story 006 · clinic-machine —— 24「房间类型匹配」(ROOM_TABLE 子句求值)+ AC-24-03。
//
// 卡面:production/epics/clinic-machine/story-006-room-type-matching.md
// GDD:design/gdd/clinic-machine.md
//   规则二 —— 房间的**定义 = ROOM_TABLE**(24 拥有);房间类型 ⇒ 该房间承担的效果;
//     **未满足任何房间类型 ⇒ `ROOM_NONE`**(不产生效果乘子)—— 本件兑现的就是这一句(AC-24-03)。
//   规则二 2026-09-17 复核 —— 判据语言允许**邻接子句**,其方向确定性「同 `adj_sum` 的 +x/+y 单向查法」。
//   规则二 2026-10-21 补记 —— **多匹配未定义**,登记 `OQ-24-7`;暂定 = **声明序首个匹配**。
//   EC-24-01(空布局 ⇒ ROOM_NONE)· EC-24-02(有家具但谓词全不成立 ⇒ 中性)
//     · EC-24-07(≥ 2 个 4-不连通簇 ⇒ 整盘 ROOM_NONE)。
// ADR-015 §三(单一整数格)· §五(4 邻接判定,非物理 / 非 NavMesh)
// ADR-005(纯函数派生 / 无状态 / 可重放)· ADR-016 §一(三源不变量:决策只读事件流 + 版本化表)
// control-manifest §Core Layer Rules:
//   Required —— 匹配 = 纯函数(布局 + 表 → room_type 或 ROOM_NONE),无缓存、无状态;
//     字符串判定一律 `StringComparison.Ordinal`;邻接子句只查 +x / +y 单向。
//   Forbidden —— `UnityEngine` 几何 / 物理 / NavMesh 判邻接;culture-sensitive 比较;
//     sim 程序集内留匹配结果缓存。
//   Guardrail —— 匹配结果须可从 `ClinicRoomExtractionResult` + `ClinicRoomTable` 重建(派生态,不进流)。
//
// 门 A(ADR-017 §二 / ADR-025 §①):`Sim` 引用集恰 = {BCL, Sim.Contracts} ⇒ 本文件零 `UnityEngine`。
//   全部整数运算与字符串序数比较:无 float / double。
//
// ═══════════════════════════════════════════════════════════════════════════════════════
// ⚠️ 本件的三条边界(卡面 §Implementation Notes 2 / 5 / 6)
// ═══════════════════════════════════════════════════════════════════════════════════════
// ① **输出形 = `bool` + `out string`,不引入可空字符串类型、不引入 `"ROOM_NONE"` 字面哨兵**
//    —— 与 `ClinicContextTable.TryGetBaseEnv` / `TryGetKSpeed` 的 bool 形同款:
//    `false` **就是** ROOM_NONE,此时 `roomType` 置 `null`。**`base_env` 的解析仍归调用方**
//    (`ClinicContextTable.TryGetBaseEnv`),本件不改 `ClinicModifierEvaluator` 的任何签名。
//
// ② **邻接子句不复用 `adj_sum` 的权重查表** —— 邻接子句问的是「有没有那个类型的邻居」,
//    不是「权重和」。本件的公开签名**根本不接受 `ClinicAdjTable`**(类型层面不可表达),
//    由 `test_signature_takesNoAdjTableOrContextTable` 钉住。
//
// ③ **不提前造接缝**:`ClinicEnvDto` 交付归 story-004,Memoize / 重放等价归 story-005。
//    本件只做「布局 → room_type」,不碰这两处,也不留它们的入口。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.Clinic
{
    /// <summary>
    /// 一次房间类型匹配的**诊断面**(纯函数输出的一部分;无缓存、无隐藏状态)。
    /// <para>存在的理由:本件的两个关键裁决在「只看 bool」时都是**静默**的 ——
    /// 多匹配(OQ-24-7 暂定裁决)与未知子句类型(AC 第 6 条)若只从返回值看,
    /// 读者无法区分「恰好一个匹配」与「三个都匹配,取了第一个」,也分不清
    /// 「这条房间不成立」与「它引用的类型在布局里压根没有」。</para>
    /// <para>⚠️ <b>ROOM_NONE 时两项恒为 0</b> —— ROOM_NONE 短路**不进入子句求值**
    /// (与 <see cref="ClinicModifierEvaluator"/> 的 `HasRoom == false` 短路同形)。
    /// 想知道「为什么没有房间」,读 <see cref="ClinicRoomExtractionResult.ConnectedClusterCount"/> /
    /// <see cref="ClinicRoomExtractionResult.CellCount"/>,不要读本结构。</para>
    /// </summary>
    public readonly struct ClinicRoomMatchDiagnostics
    {
        /// <summary>
        /// 全子句成立的房间类型**个数**。
        /// <para>0 = 无匹配(ROOM_NONE);1 = 唯一匹配;**≥ 2 = 多匹配** —— 此时实际返回的是
        /// `ROOM_TABLE.Rooms` 中**声明序第一个**(`OQ-24-7` 的暂定裁决,见
        /// <see cref="ClinicRoomMatcher.MultiMatchPolicyIsProvisional"/>)。</para>
        /// </summary>
        public readonly int MatchedRoomCount;

        /// <summary>
        /// 子句引用中,**布局里没有任何一格承载**的 `type` / `neighbor_type` **引用条数**
        /// (逐房间定义、逐子句计;邻接子句的 `type` 与 `neighbor_type` 各算一条)。
        /// <para>⚠️ <b>本字段回答的是「布局里没有这个类型的格」,不是「该类型不在 `FURN_TYPES` 闭集里」</b>
        /// —— 后者是**烘焙期**判据(归 story-001 的成员资格门),匹配期**无从判定**,
        /// 本件也**不假装判定**(那需要 `FurnTypes` 闭集作输入,而本件的输入面刻意只有三件)。
        /// 在本件里「未知类型」的可观测形态**就是**这个计数:该引用按「不成立」处理,不抛。</para>
        /// </summary>
        public readonly int UnknownClauseTypeCount;

        /// <summary>构造一份诊断。</summary>
        public ClinicRoomMatchDiagnostics(int matchedRoomCount, int unknownClauseTypeCount)
        {
            MatchedRoomCount = matchedRoomCount;
            UnknownClauseTypeCount = unknownClauseTypeCount;
        }
    }

    /// <summary>
    /// 系统 24 的「布局 → 房间类型」匹配(ROOM_TABLE 子句求值)—— 一个**纯函数**。
    /// <para>无状态:同一布局 + 同一表两次求值 ⇒ 同一结果;不缓存、不持有任何引用、
    /// 不读墙钟 / 表现态(ADR-005 / ADR-016 §一)。</para>
    /// <para>谓词语义 = **子句合取**:一个房间类型的全部子句都成立才算匹配;
    /// 任一子句不成立 ⇒ 该房间类型不匹配(不存在「部分匹配」)。</para>
    /// </summary>
    public static class ClinicRoomMatcher
    {
        /// <summary>
        /// 缺口哨兵:**当前多匹配裁决是暂定的**(`OQ-24-7` 未裁)。
        /// <para>**✅ 现为 `true`** —— GDD 规则二只写「未满足**任何**房间类型 ⇒ `ROOM_NONE`」,
        /// **没写同时满足多个怎么办**。而种子表 `clinicmachine_room.json` 的 `ward`(bed ≥ 2)与
        /// `surgery`(手术台 + 邻接无菌柜)**谓词可同时成立**,`base_env` 又按 `room_type` 查
        /// ⇒ 选谁**直接改变 `EnvMod` 输出值**。用户 2026-10-21 裁定暂取**声明序首个匹配**
        /// (备选「多匹配 ⇒ ROOM_NONE」已否决:复合布局会静默归零且无诊断)。</para>
        /// <para>⚠️ **`OQ-24-7` 裁定后本属性须转 `false`,并同步改
        /// <see cref="TryMatchRoom(in ClinicRoomExtractionResult, IClinicFurnitureTraitsSource, ClinicRoomTable, out string, out ClinicRoomMatchDiagnostics)"/>
        /// 的遍历裁决** —— 届时哨兵测试 `test_multiMatch_policyIsProvisional_sentinel`
        /// **转红即提醒**,不得静默改绿(与 <see cref="ClinicModifierEvaluator.FurnTraitsProductionReady"/> /
        /// <see cref="ClinicRoomExtraction.FurnTypeClosureEnforced"/> 同款纪律)。</para>
        /// </summary>
        public static bool MultiMatchPolicyIsProvisional => true;

        /// <summary>匹配房间类型(不带诊断面的便捷重载)。</summary>
        /// <returns>存在匹配 ⇒ true 且 <paramref name="roomType"/> 为该房间类型 id;
        /// 否则 false 且 <paramref name="roomType"/> 为 <c>null</c>(= `ROOM_NONE`)。</returns>
        public static bool TryMatchRoom(
            in ClinicRoomExtractionResult room,
            IClinicFurnitureTraitsSource traits,
            ClinicRoomTable table,
            out string roomType)
        {
            return TryMatchRoom(room, traits, table, out roomType, out _);
        }

        /// <summary>匹配房间类型(带诊断面)。</summary>
        /// <param name="room">房间析出结果(story-002)。`HasRoom == false` ⇒ 直接 ROOM_NONE。</param>
        /// <param name="traits">格属性来源(`type(c)`);生产实现 = <see cref="ClinicFurnitureTraitsSource"/>。</param>
        /// <param name="table">ROOM_TABLE(story-001)。**按 `Rooms` 的声明序遍历**。</param>
        /// <param name="roomType">匹配到的房间类型 id;无匹配 ⇒ <c>null</c>(= `ROOM_NONE`)。</param>
        /// <param name="diagnostics">诊断面(多匹配个数 / 未知类型引用条数)。</param>
        /// <returns>存在匹配 ⇒ true;否则 false。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="traits"/> 或 <paramref name="table"/> 为 null
        /// (接线错误,非设计态)。</exception>
        /// <exception cref="InvalidOperationException">某格的属性查询返回
        /// <see cref="ClinicCellLookup.Unknown"/> 或「是家具但 `type` 为空」—— 属性源不完整 / 违约。
        /// **与 `ClinicModifierEvaluator` 同一判据**:静默跳过会让子句计数静默少算。</exception>
        public static bool TryMatchRoom(
            in ClinicRoomExtractionResult room,
            IClinicFurnitureTraitsSource traits,
            ClinicRoomTable table,
            out string roomType,
            out ClinicRoomMatchDiagnostics diagnostics)
        {
            if (traits == null) throw new ArgumentNullException(nameof(traits));
            if (table == null) throw new ArgumentNullException(nameof(table));

            roomType = null;
            diagnostics = default;

            // ── ROOM_NONE 短路(EC-24-01 / EC-24-07)────────────────────────────────
            // 未析出房间 ⇒ **不进入子句求值**:诊断保持全零(见 ClinicRoomMatchDiagnostics 的备注)。
            // ⚠️ 本条**不是**「为了返回 false」才存在 —— 空 `RoomCells` 下子句本来就全不成立;
            //   它的承重面是**诊断契约**(ROOM_NONE ⇒ 诊断全零),由
            //   test_roomNone_emptyLayout_returnsFalse / test_roomNone_twoClusters_returnsFalse 钉住。
            if (!room.HasRoom) return false;

            // ── 1. 解析 S 每格的 type(c) ───────────────────────────────────────────
            Dictionary<WorldPos, string> typeByCell = ResolveTypes(room, traits);
            IReadOnlyList<ClinicRoomDef> rooms = table.Rooms;

            // ── 2. 未知类型引用计数(与匹配裁决**无关**的独立一遍)──────────────────
            // 刻意放在匹配循环**之外**:若并入循环,提前 break / 遍历方向都会改变计数,
            // 诊断就变成「求值顺序的函数」而不是「布局与表的函数」(ADR-016 §一 三源不变量)。
            int unknownClauseTypeCount = CountUnknownClauseTypeRefs(rooms, typeByCell);

            // ── 3. 声明序遍历,取首个全子句成立者(OQ-24-7 暂定裁决)──────────────
            string firstMatch = null;
            int matchedRoomCount = 0;
            for (int i = 0; i < rooms.Count; i++)
            {
                ClinicRoomDef def = rooms[i];
                if (!RoomHolds(def, typeByCell)) continue;

                matchedRoomCount++;
                // ⚠️ **`OQ-24-7` 暂定实现**:多匹配时取**声明序第一个**。
                //    不排序、不按名字、不取最大 `base_env`。改判时本处与
                //    `MultiMatchPolicyIsProvisional` 及哨兵测试须同步改。
                if (firstMatch == null) firstMatch = def.RoomType;
            }

            roomType = firstMatch;
            diagnostics = new ClinicRoomMatchDiagnostics(matchedRoomCount, unknownClauseTypeCount);
            return firstMatch != null;
        }

        // ════════════════════════════════════════════════════════════════════
        // 内部实现(全部 private static —— 公有面刻意只有两个重载 + 缺口哨兵)
        // ════════════════════════════════════════════════════════════════════

        /// <summary>解析 S 每格的 `type(c)`;`NonFurniture` ⇒ 跳过(非格,不计入任何子句);`Unknown` ⇒ 硬失败。</summary>
        private static Dictionary<WorldPos, string> ResolveTypes(
            in ClinicRoomExtractionResult room,
            IClinicFurnitureTraitsSource traits)
        {
            IReadOnlyList<WorldPos> cells = room.RoomCells;
            var typeByCell = new Dictionary<WorldPos, string>(cells.Count);

            for (int i = 0; i < cells.Count; i++)
            {
                WorldPos cell = cells[i];
                ClinicCellLookup lookup = traits.Lookup(cell, out ClinicFurnitureTraits t);
                switch (lookup)
                {
                    case ClinicCellLookup.Furniture:
                        if (string.IsNullOrEmpty(t.Type))
                            throw new InvalidOperationException(
                                $"格 ({cell.X},{cell.Y},{cell.Z}) 标记为家具但 type 为空 —— 属性源违约");
                        typeByCell[cell] = t.Type;
                        break;

                    case ClinicCellLookup.NonFurniture:
                        break;                                  // 非格:不参与任何子句计数

                    default:
                        throw new InvalidOperationException(
                            $"格 ({cell.X},{cell.Y},{cell.Z}) 的家具属性查询返回 Unknown —— " +
                            "属性源不完整(与 ClinicModifierEvaluator 同一判据:静默跳过会让子句计数静默少算)");
                }
            }
            return typeByCell;
        }

        /// <summary>
        /// 数「布局里没有任何一格承载」的子句类型引用(逐房间定义、逐子句;邻接子句的两端各算一条)。
        /// <para>纯计数,**不参与匹配裁决** —— 它的存在只为让「未知类型按不成立处理」**可观测**。</para>
        /// </summary>
        private static int CountUnknownClauseTypeRefs(
            IReadOnlyList<ClinicRoomDef> rooms,
            Dictionary<WorldPos, string> typeByCell)
        {
            var present = new HashSet<string>(StringComparer.Ordinal);
            foreach (KeyValuePair<WorldPos, string> kv in typeByCell) present.Add(kv.Value);

            int count = 0;
            for (int i = 0; i < rooms.Count; i++)
            {
                IReadOnlyList<ClinicRoomClause> clauses = rooms[i].Clauses;
                for (int c = 0; c < clauses.Count; c++)
                {
                    ClinicRoomClause clause = clauses[c];
                    if (!present.Contains(clause.Type)) count++;
                    if (clause.NeighborType != null && !present.Contains(clause.NeighborType)) count++;
                }
            }
            return count;
        }

        /// <summary>一个房间类型是否匹配 = 其全部子句的**合取**。</summary>
        private static bool RoomHolds(ClinicRoomDef def, Dictionary<WorldPos, string> typeByCell)
        {
            IReadOnlyList<ClinicRoomClause> clauses = def.Clauses;
            for (int c = 0; c < clauses.Count; c++)
                if (!ClauseHolds(clauses[c], typeByCell)) return false;   // 合取:任一子句不成立 ⇒ 整条不成立
            return true;
        }

        /// <summary>单条子句的判据(三种子句类型各一支)。</summary>
        /// <remarks>
        /// ⚠️ `default` 分支按「不成立」处理而**不抛**:`ClinicRoomTable` 的构造器已拒闭集外的
        /// `kind`(产物损坏在构造期就红了),此处是纵深防御 —— 且与 AC 第 6 条
        /// 「未知类型不抛,按不成立处理」同一口径。
        /// </remarks>
        private static bool ClauseHolds(ClinicRoomClause clause, Dictionary<WorldPos, string> typeByCell)
        {
            switch (clause.Kind)
            {
                case ClinicRoomClauseKind.MinCount:
                    return CountOfType(typeByCell, clause.Type) >= clause.Min;

                case ClinicRoomClauseKind.HasType:
                    return CountOfType(typeByCell, clause.Type) >= 1;

                case ClinicRoomClauseKind.AdjacencyMinCount:
                    return CountAdjacent(typeByCell, clause) >= clause.Min;

                default:
                    return false;
            }
        }

        /// <summary>布局中该类型的家具格数(序数比较)。</summary>
        private static int CountOfType(Dictionary<WorldPos, string> typeByCell, string type)
        {
            int count = 0;
            foreach (KeyValuePair<WorldPos, string> kv in typeByCell)
                if (string.Equals(kv.Value, type, StringComparison.Ordinal)) count++;
            return count;
        }

        /// <summary>
        /// `type` 的格中,在子句指定方向上邻接 `neighbor_type` 的**格数**
        /// (一格最多贡献 1 —— 邻接子句问的是「有没有那个类型的邻居」,不是权重和)。
        /// </summary>
        private static int CountAdjacent(Dictionary<WorldPos, string> typeByCell, ClinicRoomClause clause)
        {
            int count = 0;
            foreach (KeyValuePair<WorldPos, string> kv in typeByCell)
            {
                if (!string.Equals(kv.Value, clause.Type, StringComparison.Ordinal)) continue;
                if (HasNeighbourOfType(typeByCell, kv.Key, clause.Direction, clause.NeighborType)) count++;
            }
            return count;
        }

        /// <summary>
        /// 该格在**子句指定的单一方向**上是否有 `neighbourType` 的邻居。
        /// <para>⚠️ **只查 `+x` / `+y` 单向**(ADR-015 §五,与 `ClinicModifierEvaluator.SumAdjacencyRaws`
        /// 同源):`PlusX` 只查 `(x+1, y, z)`,**不查** `(x-1, y, z)`;`PlusY` 同理。
        /// 双向查会让「邻居放在 −x 侧」也算成立 —— 那等于把方向判据废掉,
        /// 而方向正是 GDD 规则二 2026-09-17 复核点名要「确定性」的那一项。</para>
        /// </summary>
        private static bool HasNeighbourOfType(
            Dictionary<WorldPos, string> typeByCell,
            WorldPos cell,
            ClinicRoomClauseDirection direction,
            string neighbourType)
        {
            // 防 int 溢出制造「幻影邻格」(与 ClinicRoomExtraction.CountComponents 同款守卫)。
            if (direction == ClinicRoomClauseDirection.PlusX)
            {
                if (cell.X < int.MaxValue && HasTypeAt(typeByCell, new WorldPos(cell.X + 1, cell.Y, cell.Z), neighbourType)) return true;
            }
            else if (direction == ClinicRoomClauseDirection.PlusY)
            {
                if (cell.Y < int.MaxValue && HasTypeAt(typeByCell, new WorldPos(cell.X, cell.Y + 1, cell.Z), neighbourType)) return true;
            }
            return false;
        }

        /// <summary>该格是否是 `type`(序数比较)。</summary>
        private static bool HasTypeAt(Dictionary<WorldPos, string> typeByCell, WorldPos cell, string type)
        {
            return typeByCell.TryGetValue(cell, out string actual)
                && string.Equals(actual, type, StringComparison.Ordinal);
        }
    }
}
