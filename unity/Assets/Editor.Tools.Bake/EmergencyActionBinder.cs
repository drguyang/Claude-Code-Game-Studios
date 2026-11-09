// `emergency_action.json` 的阶段 2 绑定器(T14 · 2026-11-09)
//
// 权威:`design/gdd/emergency-procedures.md` `:843`(10-DC 数据契约:动作表的路径 / 主键 / 列)
//       · `:639` / `:856` / AC-10-16(`result_mul` 三档 = {65536, 32768, 16384})
//       · `OQ-10-1`(2026-09-18 用户裁定 Missed 档**非零**——「改回 0 须重开该 OQ」)
//       · ADR-014 §三(阶段 2:白名单 / 类型 / 值域)· §二(Fix 字段在 JSON 里写**字符串**)
//
// ⚠️ 值域规则复用 `Sim.EmergencyProcedures.EmergencyActionSchema` 的 DC 校验器(**单一判据出处**);
//    本件只负责白名单 / 类型 / 读值,不复制规则。
// ⚠️ `result_mul` 的**承载**归数据表(GDD `:1049` 的 remarks 明写「不得改从代码直读常量」),
//    但三档是 AC-10-16 的**已裁定值** ⇒ 本件在构建期把数据值逐档比对裁定常量,
//    漂移即拒收。数据承载 + 构建期钉死,两者都要。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.EmergencyProcedures;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary><c>emergency_action.json</c> 的阶段 2 绑定器。</summary>
    /// <remarks>刻意 <c>public</c>:负控夹具要直接喂非法 JSON(与 `WeatherParamsBinder` 同口径)。</remarks>
    public static class EmergencyActionBinder
    {
        /// <summary>作者态源文件名。</summary>
        public const string SourceFileName = "emergency_action.json";

        private static readonly string[] RootKeys =
        {
            "schema_version", "_note", "_note_result_mul", "_note_missing_rows", "rows",
        };

        private static readonly string[] RowKeys =
        {
            "action_id", "polarity", "base_potency", "mag_threshold",
            "min_edges", "min_hold_ticks", "half_life_ticks", "duration_ticks", "result_mul",
        };

        /// <summary>绑定结果。</summary>
        public readonly struct BindResult
        {
            /// <summary>动作行(按 `action_id` 升序)。</summary>
            public readonly IReadOnlyList<EmergencyActionRow> Rows;

            /// <summary>源 <c>schema_version</c>。</summary>
            public readonly uint SchemaVersion;

            public BindResult(IReadOnlyList<EmergencyActionRow> rows, uint schemaVersion)
            {
                Rows = rows;
                SchemaVersion = schemaVersion;
            }
        }

        /// <summary>绑定 + 校验。任一违例 ⇒ 聚合 <see cref="BakeValidationException"/>。</summary>
        public static BindResult Bind(string json)
        {
            var errors = new List<string>();

            if (!JsonStage1Lexer.TryParse(json, out JsonNode root, out string lexErr))
                throw new BakeValidationException(new List<string> { $"{SourceFileName} 阶段1 词法失败:{lexErr}" });

            if (root.Kind != JsonNodeKind.Object)
                throw new BakeValidationException(new List<string>
                    { $"{SourceFileName} 根须为对象(实得 {root.Kind})" });

            RejectUnknownKeys(root, RootKeys, SourceFileName, errors);

            uint schemaVersion = ReadSchemaVersion(root, errors);
            var rows = new List<EmergencyActionRow>();
            var seen = new HashSet<int>();

            if (!root.TryGet("rows", out JsonNode rowsNode))
            {
                errors.Add($"{SourceFileName} 缺 rows");
            }
            else if (rowsNode.Kind != JsonNodeKind.Array)
            {
                errors.Add($"{SourceFileName}.rows 须为数组(实得 {rowsNode.Kind})");
            }
            else if (rowsNode.Items == null || rowsNode.Items.Count == 0)
            {
                // 空表 = 「一个动作都没有」 ⇒ 生产第一笔急救就抛。拒收,不让它以「还没配」的形态存在。
                errors.Add($"{SourceFileName}.rows 为空 —— 至少须有动作 0(止血包扎);" +
                           "空表会让生产第一笔急救 fail-loud");
            }
            else
            {
                for (int i = 0; i < rowsNode.Items.Count; i++)
                    BindRow(rowsNode.Items[i], i, rows, seen, errors);
            }

            if (errors.Count > 0) throw new BakeValidationException(errors);

            rows.Sort((a, b) => a.ActionId.CompareTo(b.ActionId));
            return new BindResult(rows, schemaVersion);
        }

        private static void BindRow(JsonNode node, int index, List<EmergencyActionRow> rows,
                                    HashSet<int> seen, List<string> errors)
        {
            string where = $"{SourceFileName}.rows[{index}]";
            if (node.Kind != JsonNodeKind.Object)
            {
                errors.Add($"{where} 须为对象(实得 {node.Kind})");
                return;
            }

            RejectUnknownKeys(node, RowKeys, where, errors);

            var row = new EmergencyActionRow
            {
                ActionId = ReadInt(node, "action_id", where, errors, int.MinValue),
                Polarity = ReadInt(node, "polarity", where, errors, int.MinValue),
                BasePotency = ReadFixRaw(node, "base_potency", where, errors),
                MagThreshold = ReadInt(node, "mag_threshold", where, errors, int.MinValue),
                MinEdges = ReadInt(node, "min_edges", where, errors, int.MinValue),
                MinHoldTicks = ReadInt(node, "min_hold_ticks", where, errors, int.MinValue),
                HalfLifeTicks = ReadInt(node, "half_life_ticks", where, errors, int.MinValue),
                DurationTicks = ReadInt(node, "duration_ticks", where, errors, int.MinValue),
                ResultMul = ReadResultMul(node, where, errors),
            };

            // ── DC 校验(复用 schema,**不复制规则**)──
            if (!seen.Add(row.ActionId))
                errors.Add($"{where}.action_id = {row.ActionId} 出现两次 —— 重复会让「用哪一行」取决于迭代序");
            if (!EmergencyActionSchema.ValidateActionId(row.ActionId))
                errors.Add($"{where}.action_id = {row.ActionId} 不在 `EmergencyAction` 闭集内(DC-4)");
            if (!EmergencyActionSchema.ValidateHalfLifeTicks(row))
                errors.Add($"{where}.half_life_ticks = {row.HalfLifeTicks} 须 ≥ 1(DC-1;0 会让 9 的 Decay 除零)");
            if (!EmergencyActionSchema.ValidateMagThreshold(row, FeelLatencyEvaluator.MAG_MAX))
                errors.Add($"{where}.mag_threshold = {row.MagThreshold} 须 ∈ [1, {FeelLatencyEvaluator.MAG_MAX}](DC-2)");
            if (!EmergencyActionSchema.ValidateResultMul(row))
                errors.Add($"{where}.result_mul 须恰三档(DC-6;缺档 ⇒ 索引越界或静默取错档)");
            if (row.Polarity < 0)
                errors.Add($"{where}.polarity = {row.Polarity} 不得为负(枚举序数)");
            if (row.BasePotency <= 0)
                errors.Add($"{where}.base_potency 须 > 0(实得 raw {row.BasePotency})");
            if (row.MinEdges < 0)
                errors.Add($"{where}.min_edges = {row.MinEdges} 不得为负");
            if (row.MinHoldTicks < 0)
                errors.Add($"{where}.min_hold_ticks = {row.MinHoldTicks} 不得为负");
            if (row.DurationTicks < 0)
                errors.Add($"{where}.duration_ticks = {row.DurationTicks} 不得为负");

            // ── result_mul 逐档比对 AC-10-16 裁定值 ──
            long[] ruled = EmergencyActionSchema.GetResultMulTiers();
            if (row.ResultMul != null && row.ResultMul.Length == ruled.Length)
            {
                for (int t = 0; t < ruled.Length; t++)
                {
                    if (row.ResultMul[t] != ruled[t])
                        errors.Add($"{where}.result_mul[{t}] = {row.ResultMul[t]} ≠ AC-10-16 裁定值 {ruled[t]}" +
                                   "(OQ-10-1 已裁 Missed 档非零;改回 0 须重开该 OQ)");
                }
            }

            rows.Add(row);
        }

        private static long[] ReadResultMul(JsonNode obj, string where, List<string> errors)
        {
            if (!obj.TryGet("result_mul", out JsonNode n))
            {
                errors.Add($"{where} 缺 result_mul");
                return null;
            }
            if (n.Kind != JsonNodeKind.Array || n.Items == null)
            {
                errors.Add($"{where}.result_mul 须为数组(实得 {n.Kind})");
                return null;
            }

            var result = new long[n.Items.Count];
            for (int i = 0; i < n.Items.Count; i++)
            {
                JsonNode item = n.Items[i];
                if (item.Kind != JsonNodeKind.String)
                {
                    errors.Add($"{where}.result_mul[{i}] 须为**字符串**(Fix 字段禁写 JSON 数字,ADR-014 §二)");
                    continue;
                }
                if (!TryParseFix(item.Str, out long raw))
                {
                    errors.Add($"{where}.result_mul[{i}] 不是合法定点字面量:「{item.Str}」");
                    continue;
                }
                result[i] = raw;
            }
            return result;
        }

        private static long ReadFixRaw(JsonNode obj, string key, string where, List<string> errors)
        {
            if (!obj.TryGet(key, out JsonNode n))
            {
                errors.Add($"{where} 缺 {key}");
                return 0;
            }
            if (n.Kind != JsonNodeKind.String)
            {
                errors.Add($"{where}.{key} 须为**字符串**(Fix 字段禁写 JSON 数字 —— 数字会经 double 中转绕开 FixParse)");
                return 0;
            }
            if (!TryParseFix(n.Str, out long raw))
            {
                errors.Add($"{where}.{key} 不是合法定点字面量:「{n.Str}」");
                return 0;
            }
            return raw;
        }

        /// <summary>`FixParse.Parse` 的抛形包一层(bool):绑定器要把「坏字面量」聚合成一条错误,
        /// 不能在第一条就中断(ADR-014 阶段 2 的聚合口径)。</summary>
        private static bool TryParseFix(string literal, out long raw)
        {
            try
            {
                raw = FixParse.Parse(literal).Raw;
                return true;
            }
            catch (Exception)
            {
                raw = 0;
                return false;
            }
        }

        private static int ReadInt(JsonNode obj, string key, string where, List<string> errors, int fallback)
        {
            if (!obj.TryGet(key, out JsonNode n))
            {
                errors.Add($"{where} 缺 {key}");
                return fallback;
            }
            if (n.Kind != JsonNodeKind.Integer)
            {
                errors.Add($"{where}.{key} 须为 JSON 整数(实得 {n.Kind}={n.RawText})");
                return fallback;
            }
            if (n.Int < int.MinValue || n.Int > int.MaxValue)
            {
                errors.Add($"{where}.{key} = {n.Int} 超出 i32 域");
                return fallback;
            }
            return (int)n.Int;
        }

        private static uint ReadSchemaVersion(JsonNode obj, List<string> errors)
        {
            if (!obj.TryGet("schema_version", out JsonNode n))
            {
                errors.Add($"{SourceFileName} 缺 schema_version");
                return 0;
            }
            if (n.Kind != JsonNodeKind.Integer || n.Int < 0 || n.Int > uint.MaxValue)
            {
                errors.Add($"schema_version 须为 u32 域整数(实得 {n.Kind}={n.RawText})");
                return 0;
            }
            return (uint)n.Int;
        }

        private static void RejectUnknownKeys(JsonNode obj, string[] allowed, string where, List<string> errors)
        {
            foreach (string key in obj.Keys)
            {
                bool ok = false;
                for (int i = 0; i < allowed.Length; i++)
                    if (string.Equals(allowed[i], key, StringComparison.Ordinal)) { ok = true; break; }
                if (!ok)
                    errors.Add($"{where} 未知键「{key}」[ADR-014·unknown-key](拼写错误会静默丢字段 ⇒ 构建期硬失败)");
            }
        }
    }
}
