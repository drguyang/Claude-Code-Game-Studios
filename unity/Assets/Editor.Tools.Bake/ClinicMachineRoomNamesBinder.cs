// 24 医馆 —— 房间名词表(assets/data/clinicmachine_room_names.json)的阶段 2 绑定器。
//
// 权威来源:
//   GDD design/gdd/clinic-machine.md :340「字段集 = roomName(词表索引)」
//   ClinicEnvDto.cs:12-16(2026-10-26「支 4-a」结案段)—— **本件以这段为准**
//   ⚠️ 原稿此处曾转述 `ClinicEnvDto.cs:20`「房间名字典 ordinal(词表索引;ROOM_NONE 亦占条目)」
//   —— 那句是**过时文本**(与本文件 :13「id = 0 不进本表」自相矛盾),且行号已漂移。
//   2026-10-26 评审 D2 订正:删转述,改引现行补注。
//   **2026-10-26 用户裁定 ①-C + ③-B**:显式词表文件;ROOM_NONE = **0**;真实房间从 **1** 起。
//
// 为什么要有这张表(而不是用 ROOM_TABLE 行序当下标):
//   ROOM_TABLE 的行序由烘焙按 `room_type` ordinal 排序决定 —— 它是**产物序**,不是**编号**。
//   用行序当编号 ⇒ 新增一个 `room_type` 会让其后所有编号**移位** ⇒ 旧存档里的 RoomNameId
//   指向错误的房间名(静默错读)。显式词表把编号**冻结在作者态**,新增只能追加。
//
// ⚠️ id = 0 **不进本表**:它是 ROOM_NONE 哨兵(与 ClinicRoomId.None = (0,0) 对齐)。
//   本表的每一行都必须是正整数 id —— `ReadRequiredPositiveInt` 天然拒 0 / 负。
//
// ⚠️ 本件是词表的**唯一构建期校验点**(id 唯一性 / 键唯一性 / 非空)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Clinic;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary><c>clinicmachine_room_names.json</c> 的阶段 2 绑定器(room_type ↔ 稳定 int 编号)。</summary>
    public static class ClinicMachineRoomNamesBinder
    {
        /// <summary>源文件名(单一出处;烘焙装载器 / 菜单 / 测试共用)。</summary>
        public const string FileName = "clinicmachine_room_names.json";

        /// <summary>`ROOM_NONE` 的词表编号(裁定 ③-B:0;真实房间从 1 起)。
        /// <para>⚠️ **唯一真源 = `ClinicRoomNameTable.NoneId`**(`Sim.Clinic` 侧)。
        /// 本常量只做**同值断言锚**,不独立取值 —— 2026-10-26 评审 M1:
        /// 原稿此处硬写 `= 0`,与 Sim 侧构成**同一哨兵的两个真源**,改一处不改另一处即静默漂移。
        /// 改为引用 Sim 侧常量后,漂移在编译期就不可能。</para></summary>
        public const int RoomNoneId = ClinicRoomNameTable.NoneId;

        /// <summary>一条词表条目。</summary>
        public readonly struct NameRow
        {
            /// <summary>房间类型 id(与 ROOM_TABLE / CONTEXT_TABLE 的 `room_type` 同一词空间)。</summary>
            public readonly string RoomType;

            /// <summary>稳定编号(≥ 1;0 保留给 `ROOM_NONE`)。</summary>
            public readonly int Id;

            /// <summary>构造一条(测试 / 物化用)。</summary>
            public NameRow(string roomType, int id)
            {
                RoomType = roomType; Id = id;
            }
        }

        /// <summary>绑定结果。</summary>
        public readonly struct BindResult
        {
            /// <summary>条目集(声明序;编码时另行 ordinal 排序)。</summary>
            public readonly IReadOnlyList<NameRow> Rows;

            /// <summary>源 schema_version(写方头槽 offset 8 的唯一真值来源)。</summary>
            public readonly uint SchemaVersion;

            /// <summary>构造绑定结果。</summary>
            public BindResult(IReadOnlyList<NameRow> rows, uint schemaVersion)
            {
                Rows = rows; SchemaVersion = schemaVersion;
            }
        }

        /// <summary>绑定 + 构建期拒收(任一违例 ⇒ 聚合 <see cref="BakeValidationException"/>)。</summary>
        /// <exception cref="BakeValidationException">词法失败 / 任一校验失败(聚合后一次 throw)。</exception>
        public static BindResult Bind(string json)
        {
            var errors = new List<string>();

            JsonNode root = ClinicMachineSchema.BeginTable(
                json, ClinicMachineSchema.RoomNamesRootKeys, FileName, errors, out uint schemaVersion);

            // ⚠️ 早 throw 豁免点:无 rooms 数组 ⇒ 行层校验结构上不可达。
            if (!root.TryGet("rooms", out JsonNode rowsNode) || rowsNode.Kind != JsonNodeKind.Array)
            {
                errors.Add($"{FileName}:缺 rooms 数组");
                throw new BakeValidationException(errors);
            }

            var rows = new List<NameRow>();
            var seenTypes = new HashSet<string>(StringComparer.Ordinal);
            var seenIds = new HashSet<int>();

            for (int i = 0; i < rowsNode.Items.Count; i++)
            {
                NameRow? row = ParseRow(rowsNode.Items[i], i, seenTypes, seenIds, errors);
                if (row.HasValue) rows.Add(row.Value);
            }

            if (rows.Count == 0 && errors.Count == 0)
                errors.Add($"{FileName}:rooms 为空 —— 空词表让所有房间都投成 ROOM_NONE(静默失效),拒");

            if (errors.Count > 0) throw new BakeValidationException(errors);

            return new BindResult(rows, schemaVersion);
        }

        /// <summary>逐行解析(白名单 + 键唯一 + id 唯一 + id 为正)。</summary>
        private static NameRow? ParseRow(
            JsonNode row, int i, HashSet<string> seenTypes, HashSet<int> seenIds, List<string> errors)
        {
            string where = $"rooms[{i}]";
            if (!ClinicMachineSchema.BeginRow(
                    row, where, ClinicMachineSchema.RoomNamesRowKeys, FileName, errors))
                return null;

            bool ok = ClinicMachineSchema.ReadRequiredString(
                row, "room_type", where, FileName, errors, out string roomType);

            // id 用 ReadRequiredPositiveInt:它天然拒 0 / 负 ⇒ 「0 保留给 ROOM_NONE」由类型机制守,
            // 不靠另设一条 if(单机制,与 AC-24-05 的档集纪律同构);失败路径亦赋 0(与拆分前等价)。
            long idValue = 0;
            ok &= ClinicMachineSchema.ReadRequiredPositiveInt(
                row, "id", where, FileName, errors, out idValue);

            if (!ok) return null;

            if (!ClinicMachineSchema.TryCastVocabularyId(
                    idValue, where, FileName, errors, out int id))
                return null;

            if (!seenTypes.Add(roomType))
            {
                errors.Add($"{FileName}:{where} room_type「{roomType}」重复 —— 一词一编号");
                return null;
            }

            if (!seenIds.Add(id))
            {
                errors.Add($"{FileName}:{where} id={id} 重复 —— 两个 room_type 争同一编号会让投影二义");
                return null;
            }

            return new NameRow(roomType, id);
        }
    }
}
