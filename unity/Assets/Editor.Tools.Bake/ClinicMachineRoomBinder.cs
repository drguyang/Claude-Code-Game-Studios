// Story 001 · clinic-machine —— ROOM_TABLE(assets/data/clinicmachine_room.json)的阶段 2 绑定器。
//
// 权威来源:
//   ADR-014 §三(阶段 2 绑定 + 聚合 throw)
//   GDD design/gdd/clinic-machine.md 规则二:
//     · ROOM_TABLE = 房间类型的判定谓词(24 拥有;非关卡工具产物,不登记 ADR-022 §三 导出契约)
//     · 判据语言允许「邻接子句」(2026-09-17 复核,阻塞 5 —— 幻想承重)
//     · 烘焙 schema 须校验邻接子句引用的 type ∈ FURN_TYPES 且方向确定性(同 adj_sum 的 +x/+y 单向查法)
//   AC-24-12 分两半(卡面「本体交 + 半边挂标」):
//     (a) **已实现半边** —— 子句类型闭集(<see cref="ClinicClauseKind"/>) + 方向闭集;未知 = 构建失败
//     (b) **挂标半边** —— type ∈ FURN_TYPES:闭集来源 = 21a 家具目录扩展(O-24-1),实测零落点
//         ⇒ BLOCKED-BY-O-24-1 / TD-clinic-01。本件**不伪造闭集**(协调方 2026-10-20 裁决 2):
//         闭集经 <see cref="IFurnTypeSource"/> 注入;缺席时
//         <c>BindResult.FurnMembershipEnforced == false</c> = **缺口哨兵**(缺席可观测,非静默)。
//
// ⚠️ 缺口哨兵口径:O-24-1 落地后生产路径注入真来源 ⇒ 哨兵翻 true ⇒ 相关断言**转红**是被要求的
//   行为,**不得当回归修掉**(承 S14-6 的哨兵口径)。

using System;
using System.Collections.Generic;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary><c>clinicmachine_room.json</c> 的阶段 2 绑定器(AC-24-12 的已实现半边 + 缺口哨兵)。</summary>
    public static class ClinicMachineRoomBinder
    {
        /// <summary>源文件名(单一出处)。</summary>
        public const string FileName = "clinicmachine_room.json";

        /// <summary>一条房间判据子句。</summary>
        public readonly struct RoomClause
        {
            /// <summary>子句类型(闭集;闭集外不可能出现在结果里 —— 绑定期已拒)。</summary>
            public readonly ClinicClauseKind Kind;

            /// <summary>主语家具类型 id。</summary>
            public readonly string Type;

            /// <summary>邻接子句的邻居类型(非邻接子句 = null)。</summary>
            public readonly string NeighborType;

            /// <summary>邻接子句的方向(非邻接子句 = <see cref="ClinicClauseDirection.None"/>)。</summary>
            public readonly ClinicClauseDirection Direction;

            /// <summary>计数阈值(非计数子句 = 0)。</summary>
            public readonly int Min;

            /// <summary>构造一条。</summary>
            public RoomClause(
                ClinicClauseKind kind, string type, string neighborType,
                ClinicClauseDirection direction, int min)
            {
                Kind = kind; Type = type; NeighborType = neighborType; Direction = direction; Min = min;
            }
        }

        /// <summary>一个房间类型定义(谓词 = 子句的合取)。</summary>
        public readonly struct RoomDef
        {
            /// <summary>房间类型 id。</summary>
            public readonly string RoomType;

            /// <summary>子句集(按合取组合;编码时另行全序排序)。</summary>
            public readonly IReadOnlyList<RoomClause> Clauses;

            /// <summary>构造一个。</summary>
            public RoomDef(string roomType, IReadOnlyList<RoomClause> clauses)
            {
                RoomType = roomType; Clauses = clauses;
            }
        }

        /// <summary>绑定结果。</summary>
        public readonly struct BindResult
        {
            /// <summary>房间定义集(声明序;编码时另行全序排序)。</summary>
            public readonly IReadOnlyList<RoomDef> Rooms;

            /// <summary>**缺口哨兵**:true = 本次烘焙对子句引用的类型执行了 <c>∈ FURN_TYPES</c> 校验;
            /// false = 闭集来源缺席(<c>O-24-1</c> 未兑现)⇒ 该半边**未强制**。
            /// <para>⚠️ 生产路径当前**恒 false**(21a 家具目录扩展实测零落点)。</para></summary>
            public readonly bool FurnMembershipEnforced;

            /// <summary>源 schema_version。</summary>
            public readonly uint SchemaVersion;

            /// <summary>构造绑定结果。</summary>
            public BindResult(IReadOnlyList<RoomDef> rooms, bool furnMembershipEnforced, uint schemaVersion)
            {
                Rooms = rooms; FurnMembershipEnforced = furnMembershipEnforced; SchemaVersion = schemaVersion;
            }
        }

        /// <summary>绑定 + 子句类型闭集 + 方向确定性 + (可用时)家具类型成员资格。</summary>
        /// <param name="json">源文本。</param>
        /// <param name="furnTypeSource">家具类型闭集来源;null = 缺席(生产态,<c>O-24-1</c> 未兑现)。</param>
        /// <exception cref="BakeValidationException">任一违例(聚合后一次 throw)。</exception>
        public static BindResult Bind(string json, IFurnTypeSource furnTypeSource)
        {
            var errors = new List<string>();
            IReadOnlyCollection<string> furnTypes = null;
            bool membershipEnforced =
                furnTypeSource != null && furnTypeSource.TryGetTypes(out furnTypes) && furnTypes != null;

            JsonNode root = ClinicMachineSchema.BeginTable(
                json, ClinicMachineSchema.RoomRootKeys, FileName, errors, out uint schemaVersion);

            // ⚠️ 早 throw 豁免点:无 rooms 数组 ⇒ 行层校验结构上不可达。
            if (!root.TryGet("rooms", out JsonNode rowsNode) || rowsNode.Kind != JsonNodeKind.Array)
            {
                errors.Add($"{FileName}:缺 rooms 数组");
                throw new BakeValidationException(errors);
            }

            // 🔁 2026-10-29(债册 `TD-clinic-07` / 评审 R-19):`FurnMembershipEnforced` 原先是
            //    **独立算出**的(只问「来源在不在」),与 `AssertFurnMember` **有没有真的跑**无关
            //    ⇒ 哨兵会撒谎:删掉成员资格调用,标志仍为 true,而正面夹具照样绿。
            //    改为**计数实际执行次数**:0 次 ⇒ 门没跑过 ⇒ 哨兵为 false。
            List<RoomDef> rooms = ParseRooms(
                rowsNode, membershipEnforced, furnTypes, errors, out int memberChecks);

            if (rooms.Count == 0 && errors.Count == 0)
                errors.Add($"{FileName}:rooms 为空 —— 空表让所有布局恒 ROOM_NONE(整表静默失效),拒");

            if (errors.Count > 0) throw new BakeValidationException(errors);

            // 哨兵口径(2026-10-29 改):**来源可用 且 至少真的执行过一次成员资格门**。
            return new BindResult(rooms, membershipEnforced && memberChecks > 0, schemaVersion);
        }

        /// <summary>解析单个房间类型。</summary>
        private static RoomDef? ParseRoom(
            JsonNode row, int i, HashSet<string> seenRoomTypes,
            bool membershipEnforced, IReadOnlyCollection<string> furnTypes, List<string> errors,
            ref int memberChecks)
        {
            string where = $"rooms[{i}]";
            if (!ClinicMachineSchema.BeginRow(
                    row, where, ClinicMachineSchema.RoomRowKeys, FileName, errors))
                return null;

            if (!ClinicMachineSchema.ReadRequiredString(
                    row, "room_type", where, FileName, errors, out string roomType))
                return null;

            List<RoomClause> clauses = ParseClauses(
                row, where, roomType, membershipEnforced, furnTypes, errors, ref memberChecks);

            if (clauses == null || clauses.Count == 0) return null;

            if (!seenRoomTypes.Add(roomType))
            {
                errors.Add($"{FileName}:{where} room_type「{roomType}」重复 —— 每房间类型恰一谓词");
                return null;
            }

            return new RoomDef(roomType, clauses);
        }

        /// <summary>逐房间解析(声明序;<c>room_type</c> 去重跨行共享)。</summary>
        /// <remarks>🔁 <b>2026-10-29 拆分</b>(原 <see cref="Bind"/> 52 行 &gt; 40;债册 `TD-clinic-14` / 评审 S16)。
        /// <paramref name="memberChecks"/> 见 <see cref="Bind"/> 里哨兵口径的注释。</remarks>
        private static List<RoomDef> ParseRooms(
            JsonNode rowsNode, bool membershipEnforced, IReadOnlyCollection<string> furnTypes,
            List<string> errors, out int memberChecks)
        {
            var rooms = new List<RoomDef>();
            var seenRoomTypes = new HashSet<string>(StringComparer.Ordinal);
            memberChecks = 0;

            for (int i = 0; i < rowsNode.Items.Count; i++)
            {
                RoomDef? room = ParseRoom(
                    rowsNode.Items[i], i, seenRoomTypes, membershipEnforced, furnTypes, errors,
                    ref memberChecks);
                if (room.HasValue) rooms.Add(room.Value);
            }

            return rooms;
        }

        /// <summary>子句数组:存在性 / 非空 / 逐条解析;任一结构失败返回 <c>null</c>。</summary>
        /// <remarks>🔁 <b>2026-10-29 拆分</b>(原 <see cref="ParseRoom"/> 52 行 &gt; 40;债册 `TD-clinic-14` / 评审 S16)。
        /// **报文原文与判定次序逐字未变**。</remarks>
        private static List<RoomClause> ParseClauses(
            JsonNode row, string where, string roomType, bool membershipEnforced,
            IReadOnlyCollection<string> furnTypes, List<string> errors, ref int memberChecks)
        {
            if (!row.TryGet("clauses", out JsonNode clausesNode) || clausesNode.Kind != JsonNodeKind.Array)
            {
                errors.Add($"{FileName}:{where}(room_type=\"{roomType}\") 缺 clauses 数组");
                return null;
            }

            if (clausesNode.Items.Count == 0)
            {
                errors.Add(
                    $"{FileName}:{where}(room_type=\"{roomType}\") clauses 为空 —— " +
                    "空谓词会匹配任一布局(房间身份退化为恒真),拒");
                return null;
            }

            var clauses = new List<RoomClause>(clausesNode.Items.Count);
            for (int c = 0; c < clausesNode.Items.Count; c++)
            {
                RoomClause? clause = ParseClause(
                    clausesNode.Items[c], $"{where}.clauses[{c}]", membershipEnforced, furnTypes, errors,
                    ref memberChecks);
                if (clause.HasValue) clauses.Add(clause.Value);
            }

            return clauses;
        }

        /// <summary>解析单条子句(AC-24-12 已实现半边)。</summary>
        /// <remarks>🔁 <b>2026-10-29 拆分</b>(原 82 行 &gt; 40;债册 `TD-clinic-14` / 评审 S16):
        /// 头部 / 邻接支 / 计数支各成一个私有方法,**校验次序与报文原文逐字未变**。
        /// <para>🔁 <b>2026-10-29 同批</b>(债册 `TD-clinic-07` / 评审 R-19):新增
        /// <paramref name="memberChecks"/> —— 让「成员资格门真的执行过」可被观测,
        /// 见 <see cref="Bind"/> 里哨兵口径的改动。</para></remarks>
        private static RoomClause? ParseClause(
            JsonNode clause, string where,
            bool membershipEnforced, IReadOnlyCollection<string> furnTypes, List<string> errors,
            ref int memberChecks)
        {
            if (!TryReadClauseHead(clause, where, errors, out ClinicClauseKind kind, out string type))
                return null;

            // 挂标半边(成员资格):仅在来源可用时执行;缺席由哨兵自陈。
            if (membershipEnforced)
            {
                AssertFurnMember(type, where, furnTypes, errors);
                memberChecks++;
            }

            string neighborType = null;
            ClinicClauseDirection direction = ClinicClauseDirection.None;

            if (kind == ClinicClauseKind.AdjacencyMinCount)
            {
                if (!TryReadAdjacency(clause, where, membershipEnforced, furnTypes, errors,
                        ref memberChecks, out neighborType, out direction))
                    return null;
            }

            int min = 0;
            if ((kind == ClinicClauseKind.MinCount || kind == ClinicClauseKind.AdjacencyMinCount)
                && !TryReadMin(clause, where, errors, out min))
                return null;

            return new RoomClause(kind, type, neighborType, direction, min);
        }

        /// <summary>子句头:须对象 · `kind` 必填且 ∈ 闭集 · 未知键白名单 · `type` 必填。</summary>
        private static bool TryReadClauseHead(JsonNode clause, string where, List<string> errors,
            out ClinicClauseKind kind, out string type)
        {
            kind = default;
            type = null;

            if (clause.Kind != JsonNodeKind.Object)
            {
                errors.Add($"{FileName}:{where} 须为对象(实得 {clause.Kind})");
                return false;
            }

            if (!clause.TryGet("kind", out JsonNode kindNode) || kindNode.Kind != JsonNodeKind.String)
            {
                errors.Add($"{FileName}:{where} 缺字符串 kind(子句类型必填)");
                return false;
            }

            // AC-24-12 已实现半边:未知子句类型 = 构建失败(单机制,不另设分支)。
            if (!ClinicMachineSchema.TryClauseKind(kindNode.Str, out kind))
            {
                errors.Add(
                    $"{FileName}:{where} 未知子句类型 kind=\"{kindNode.Str}\" —— AC-24-12 / 规则二:" +
                    "子句类型是闭集 {" + string.Join(" / ", ClinicMachineSchema.ClauseKindLiterals) + "};" +
                    "闭集外一律 schema 错(收进表即错误,非「给它一个行为」)");
                return false;
            }

            ClinicMachineSchema.RejectUnknownKeys(
                clause, ClinicMachineSchema.ClauseKeysByKind[(int)kind], where, FileName, errors);

            return ClinicMachineSchema.ReadRequiredString(clause, "type", where, FileName, errors, out type);
        }

        /// <summary>邻接支:`neighbor_type` 必填 · 成员资格 · 方向闭集与确定性。</summary>
        private static bool TryReadAdjacency(JsonNode clause, string where,
            bool membershipEnforced, IReadOnlyCollection<string> furnTypes, List<string> errors,
            ref int memberChecks, out string neighborType, out ClinicClauseDirection direction)
        {
            neighborType = null;
            direction = ClinicClauseDirection.None;

            if (!ClinicMachineSchema.ReadRequiredString(
                    clause, "neighbor_type", where, FileName, errors, out neighborType))
                return false;

            if (membershipEnforced)
            {
                AssertFurnMember(neighborType, where, furnTypes, errors);
                memberChecks++;
            }

            // 方向确定性(GDD 规则二 2026-09-17 复核;同 adj_sum 的 +x/+y 单向查法)
            if (!clause.TryGet("direction", out JsonNode dirNode) || dirNode.Kind != JsonNodeKind.String)
            {
                errors.Add(
                    $"{FileName}:{where} 邻接子句缺字符串 direction —— 规则二:" +
                    "方向确定性是承重判据(同 adj_sum 的 +x/+y 单向查法)");
                return false;
            }

            if (!ClinicMachineSchema.TryClauseDirection(dirNode.Str, out direction))
            {
                errors.Add(
                    $"{FileName}:{where} 未知方向 direction=\"{dirNode.Str}\" —— 规则二:" +
                    "方向闭集 = {+x, +y}(ADR-015 §五 的基向量 (1,0) / (0,1))");
                return false;
            }

            return true;
        }

        /// <summary>计数支:`min` 必填且为正整数,且不超 `int` 域。</summary>
        private static bool TryReadMin(JsonNode clause, string where, List<string> errors, out int min)
        {
            min = 0;
            if (!ClinicMachineSchema.ReadRequiredPositiveInt(
                    clause, "min", where, FileName, errors, out long minValue))
                return false;

            if (minValue > int.MaxValue)
            {
                errors.Add($"{FileName}:{where}.min={minValue} 超出 int 域");
                return false;
            }

            min = (int)minValue;
            return true;
        }

        /// <summary>AC-24-12 挂标半边:子句引用类型须 ∈ 注入的闭集。</summary>
        private static void AssertFurnMember(
            string type, string where, IReadOnlyCollection<string> furnTypes, List<string> errors)
        {
            foreach (string allowed in furnTypes)
                if (string.Equals(allowed, type, StringComparison.Ordinal))
                    return;

            errors.Add(
                $"{FileName}:{where}.type「{type}」∉ 家具类型闭集(FURN_TYPES) —— AC-24-12 / 规则二:" +
                "邻接子句引用的 type 须在闭集内(闭集来源 = 21a 家具目录扩展,O-24-1)");
        }
    }
}
