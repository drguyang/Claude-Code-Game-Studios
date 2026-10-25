// 24 医馆 —— 情境词表(assets/data/clinicmachine_context_names.json)的阶段 2 绑定器。
//
// 权威来源:
//   GDD design/gdd/clinic-machine.md :340「contexts[](情境词表索引,可空集)」
//   ClinicEnvDto.cs:9「支 4-a:情境词表 size 全库未定 ⇒ 不造 bitmask」—— **本表即该 size 的答案**
//   **2026-10-26 用户裁定 ②-C**:显式词表文件。
//
// 为什么要有这张表:
//   `ClinicEnvDto.ContextIds` 是 `int[]`,而 `CONTEXT_TABLE` 里存的是 `string ContextId`。
//   用表行序当下标 = 让 DTO 编号成为「产物序」的函数 ⇒ 新增一个情境会让其后编号移位
//   (静默错读)。显式词表把编号冻结在作者态,新增只能追加。
//
// ⚠️ **空集语义**:`ClinicEnvDto.ContextIds` 为空数组表示「印卸 = 不成立」(tutorial-48 面板),
//   **不是**本表里的某个 id —— 本表不设「空」条目。
//
// ⚠️ 本件是词表的**唯一构建期校验点**(id 唯一性 / 键唯一性 / 非空)。

using System;
using System.Collections.Generic;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary><c>clinicmachine_context_names.json</c> 的阶段 2 绑定器(context_id ↔ 稳定 int 编号)。</summary>
    public static class ClinicMachineContextNamesBinder
    {
        /// <summary>源文件名(单一出处;烘焙装载器 / 菜单 / 测试共用)。</summary>
        public const string FileName = "clinicmachine_context_names.json";

        /// <summary>一条词表条目。</summary>
        public readonly struct NameRow
        {
            /// <summary>情境 id(与 CONTEXT_TABLE 的 `context_id` 同一词空间)。</summary>
            public readonly string ContextId;

            /// <summary>稳定编号(≥ 1)。</summary>
            public readonly int Id;

            /// <summary>构造一条(测试 / 物化用)。</summary>
            public NameRow(string contextId, int id)
            {
                ContextId = contextId; Id = id;
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

            if (!JsonStage1Lexer.TryParse(json, out JsonNode root, out string lexErr))
                throw new BakeValidationException(
                    new List<string> { $"{FileName} 阶段1 词法失败:{lexErr}" });

            if (root.Kind != JsonNodeKind.Object)
                throw new BakeValidationException(
                    new List<string> { $"{FileName} 根须为对象(实得 {root.Kind})" });

            ClinicMachineSchema.RejectUnknownKeys(
                root, ClinicMachineSchema.ContextNamesRootKeys, "根", FileName, errors);

            uint schemaVersion = ClinicMachineSchema.ReadSchemaVersion(root, FileName, errors);

            // ⚠️ 早 throw 豁免点:无 contexts 数组 ⇒ 行层校验结构上不可达。
            if (!root.TryGet("contexts", out JsonNode rowsNode) || rowsNode.Kind != JsonNodeKind.Array)
            {
                errors.Add($"{FileName}:缺 contexts 数组");
                throw new BakeValidationException(errors);
            }

            var rows = new List<NameRow>();
            var seenIds2 = new HashSet<string>(StringComparer.Ordinal);
            var seenIds = new HashSet<int>();

            for (int i = 0; i < rowsNode.Items.Count; i++)
            {
                NameRow? row = ParseRow(rowsNode.Items[i], i, seenIds2, seenIds, errors);
                if (row.HasValue) rows.Add(row.Value);
            }

            if (rows.Count == 0 && errors.Count == 0)
                errors.Add($"{FileName}:contexts 为空 —— 空词表让所有情境都投不出来(静默失效),拒");

            if (errors.Count > 0) throw new BakeValidationException(errors);

            return new BindResult(rows, schemaVersion);
        }

        /// <summary>逐行解析(白名单 + 键唯一 + id 唯一 + id 为正)。</summary>
        private static NameRow? ParseRow(
            JsonNode row, int i, HashSet<string> seenContextIds, HashSet<int> seenIds, List<string> errors)
        {
            string where = $"contexts[{i}]";
            if (row.Kind != JsonNodeKind.Object)
            {
                errors.Add($"{FileName}:{where} 须为对象(实得 {row.Kind})");
                return null;
            }

            ClinicMachineSchema.RejectUnknownKeys(
                row, ClinicMachineSchema.ContextNamesRowKeys, where, FileName, errors);

            bool ok = ClinicMachineSchema.ReadRequiredString(
                row, "context_id", where, FileName, errors, out string contextId);

            // id 用 ReadRequiredPositiveInt:天然拒 0 / 负(与 room 词表同一纪律 —— 单机制)。
            long idValue = 0;
            if (ClinicMachineSchema.ReadRequiredPositiveInt(
                    row, "id", where, FileName, errors, out long parsedId))
                idValue = parsedId;
            else
                ok = false;

            if (!ok) return null;

            if (idValue > int.MaxValue)
            {
                errors.Add($"{FileName}:{where}.id={idValue} 越 int 上界 —— 词表编号须在 int 域内");
                return null;
            }

            int id = (int)idValue;

            if (!seenContextIds.Add(contextId))
            {
                errors.Add($"{FileName}:{where} context_id「{contextId}」重复 —— 一词一编号");
                return null;
            }

            if (!seenIds.Add(id))
            {
                errors.Add($"{FileName}:{where} id={id} 重复 —— 两个 context_id 争同一编号会让投影二义");
                return null;
            }

            return new NameRow(contextId, id);
        }
    }
}
