// sprint-08 S1.0 · 病名册词表阶段 2 绑定器(唯一调用 JudgmentLexiconValidator 处)
//
// ⚠️ 全部错误聚合后一次抛(承 DiagnosisSignBinder 纪律);未知键硬失败(ADR-014 §三)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Presentation.Casebook;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>`case_judgment_lexicon.json` 的阶段 2 绑定器。</summary>
    internal static class JudgmentLexiconBinder
    {
        private static readonly string[] RootKeys = { "schema_version", "entries" };
        private static readonly string[] RowKeys = { "lexicon_id", "text", "disease_ids" };

        public readonly struct BindResult
        {
            public readonly List<JudgmentLexemeRow> Rows;
            public readonly uint SchemaVersion;

            public BindResult(List<JudgmentLexemeRow> rows, uint schemaVersion)
            {
                Rows = rows;
                SchemaVersion = schemaVersion;
            }
        }

        /// <param name="json">`case_judgment_lexicon.json` 原文。</param>
        public static BindResult Bind(string json)
        {
            var errors = new List<string>();

            if (!JsonStage1Lexer.TryParse(json, out JsonNode root, out string lexErr))
            {
                errors.Add($"case_judgment_lexicon.json 阶段1 词法失败:{lexErr}");
                throw new BakeValidationException(errors);
            }

            uint schemaVersion = 0;
            var rows = new List<JudgmentLexemeRow>();

            if (root.Kind != JsonNodeKind.Object)
            {
                errors.Add($"根须为对象(实得 {root.Kind})");
            }
            else
            {
                RejectUnknownKeys(root, RootKeys, "根", errors);

                if (!root.TryGet("schema_version", out JsonNode sv))
                    errors.Add("缺 schema_version");
                else if (sv.Kind != JsonNodeKind.Integer || sv.Int < 0 || sv.Int > uint.MaxValue)
                    errors.Add($"schema_version 须为 u32 域整数(实得 {sv.RawText})");
                else
                    schemaVersion = (uint)sv.Int;

                if (!root.TryGet("entries", out JsonNode arr))
                    errors.Add("缺 entries 数组");
                else if (arr.Kind != JsonNodeKind.Array)
                    errors.Add($"entries 须为数组(实得 {arr.Kind})");
                else
                    BindRows(arr, rows, errors);
            }

            if (errors.Count == 0)
            {
                try
                {
                    JudgmentLexiconValidator.Validate(rows);
                }
                catch (Exception ex)
                {
                    errors.Add("词表校验:" + ex.Message);
                }
            }

            if (errors.Count > 0)
                throw new BakeValidationException(errors);

            return new BindResult(rows, schemaVersion);
        }

        private static void BindRows(JsonNode arr, List<JudgmentLexemeRow> rows, List<string> errors)
        {
            for (int i = 0; i < arr.Items.Count; i++)
            {
                JsonNode node = arr.Items[i];
                string where = $"entries[{i}]";
                if (node.Kind != JsonNodeKind.Object)
                {
                    errors.Add($"{where} 须为对象(实得 {node.Kind})");
                    continue;
                }
                RejectUnknownKeys(node, RowKeys, where, errors);

                int lexiconId = ReadInt(node, "lexicon_id", where, errors, required: true);
                string text = ReadString(node, "text", where, errors);
                int[] diseaseIds = ReadDiseaseIds(node, where, errors);

                rows.Add(new JudgmentLexemeRow(lexiconId, text, diseaseIds));
            }
        }

        private static int ReadInt(JsonNode obj, string key, string where, List<string> errors, bool required)
        {
            string label = $"{where}.{key}";
            if (!obj.TryGet(key, out JsonNode n))
            {
                if (required) errors.Add($"{label} 缺失");
                return 0;
            }
            if (n.Kind != JsonNodeKind.Integer)
            {
                errors.Add($"{label} 须为整数(实得 {n.Kind})");
                return 0;
            }
            return (int)n.Int;
        }

        private static string ReadString(JsonNode obj, string key, string where, List<string> errors)
        {
            string label = $"{where}.{key}";
            if (!obj.TryGet(key, out JsonNode n))
            {
                errors.Add($"{label} 缺失");
                return null;
            }
            if (n.Kind != JsonNodeKind.String)
            {
                errors.Add($"{label} 须为字符串(实得 {n.Kind})");
                return null;
            }
            // 空串**不**在此拒 —— 归校验器 [lexicon-text](域语义判据,禁与绑定层重复致其不可达)。
            return n.Str;
        }

        private static int[] ReadDiseaseIds(JsonNode obj, string where, List<string> errors)
        {
            string label = $"{where}.disease_ids";
            if (!obj.TryGet("disease_ids", out JsonNode n))
            {
                errors.Add($"{label} 缺失(孤儿条目须显式 [] —— 缺字段与空数组语义不同)");
                return null;
            }
            if (n.Kind != JsonNodeKind.Array)
            {
                errors.Add($"{label} 须为数组(实得 {n.Kind})");
                return null;
            }
            var ids = new int[n.Items.Count];
            for (int i = 0; i < n.Items.Count; i++)
            {
                JsonNode item = n.Items[i];
                if (item.Kind != JsonNodeKind.Integer)
                {
                    errors.Add($"{label}[{i}] 须为整数(实得 {item.Kind})");
                    return null;
                }
                ids[i] = (int)item.Int;
            }
            return ids;
        }

        private static void RejectUnknownKeys(JsonNode obj, string[] allowed, string where, List<string> errors)
        {
            foreach (string key in obj.Keys)
            {
                bool ok = false;
                for (int i = 0; i < allowed.Length; i++)
                    if (string.Equals(allowed[i], key, StringComparison.Ordinal)) { ok = true; break; }
                if (!ok)
                    errors.Add($"{where} 未知键「{key}」[ADR-014·unknown-key]");
            }
        }
    }
}
