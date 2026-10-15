// S12-9 · story-002 —— 53 判定表(assets/data/53_outcome_table.json)两阶段烘焙。
//
// 权威来源:
//   ADR-014 §三(阶段 2 per-schema 绑定:白名单 / 闭集 / FixParse 出口 · 校验聚合 throw)
//   卡面 story-002 Note 2 —— 构建期校验四条:
//     ① 组合全覆盖(P/C/D 笛卡尔积无空洞;空洞 ⇒ 显式 default 行)
//     ② 值域 ∈ Outcome 枚举闭集
//     ③ 零浮点(Float token 结构性硬失败)
//     ④ 极性列零(53 不重判极性 —— 真源 = 9 注册表;作者态出现极性键 = 硬失败)
//   键域裁定(2026-10-15 用户裁):P 3 × C 3 × D 2 = 18 组合;U 档列默认 1 档。
//   Outcome53.cs(表数据集 + Judge 求值器的运行期形状)
//
// ⚠️ 本件 = 判定表的**唯一校验点**(阶段 2 绑定面);Editor.Tools.Bake 引用 Sim
//   ⇒ Outcome 枚举名映射直用 Sim 符号(零字符串副本、零漂移)。
//
// ⚠️ 表值语义:行值 = Outcome 枚举名(字符串),非数值 —— 数值轮改表值不改形状;
//   U 档列 u_band ∈ [0, u_band_count);P0 默认 1 档 ⇒ 全行 u_band=0。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Consequences;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>`53_outcome_table.json` 的阶段 2 绑定器(唯一校验点;四条校验聚合 throw)。
    /// <para>⚠️ public:其 <see cref="OutcomeTableBinder.BindResult"/> 经
    /// <see cref="OutcomeTable53.BakeOutput"/> 公开承载 —— 容器可访问性须 ≥ 成员(CS0050/51/52)。</para></summary>
    public static class OutcomeTableBinder
    {
        // ── 已知键白名单(ADR-014 §三:未知键 = 烘焙期硬失败)──

        private static readonly string[] RootKeys = { "schema_version", "u_band_count", "rows" };
        private static readonly string[] RowKeys = { "key", "u_band", "outcome" };

        /// <summary>极性类禁名(校验 ④:53 不重判极性 —— 出现即硬失败,而非静默忽略)。</summary>
        private static readonly string[] PolarityForbiddenKeys = { "polarity", "pol", "极性" };

        /// <summary>闭集外 Outcome 名的硬失败口径(校验 ②)。</summary>
        private static readonly string[] AllowedOutcomeNames =
        {
            "residual", "recurrence", "trial_history", "no_echo",
        };

        /// <summary>键域轴取值闭集(2026-10-15 用户裁 3×3×2)。</summary>
        private static readonly string[] PLanes = { "empty", "suspected", "confirmed" };
        private static readonly string[] CLanes = { "none", "applied_unmatched", "applied_matched" };
        private static readonly string[] DLanes = { "recovered", "not_recovered" };

        /// <summary>绑定结果(表行 + 档数,供写方落盘 / 测试直用)。
        /// <para>⚠️ public:承载它的 <see cref="OutcomeTable53.BakeOutput"/> 是公开型
        /// (BakeFromRepo 出口),成员类型可访问性不得低于容器(CS0051/52/50)。</para></summary>
        public readonly struct BindResult
        {
            /// <summary>18 组合 → Outcome(+ default)。</summary>
            public readonly IReadOnlyDictionary<(Outcome53.JudgmentLane P, Outcome53.CareLane C,
                Outcome53.RecoveryLane D, int UBand), Outcome53.Outcome> Rows;

            /// <summary>U 档数(≥1)。</summary>
            public readonly int UBandCount;

            /// <summary>default 行出口(∈ 闭集)。</summary>
            public readonly Outcome53.Outcome DefaultOutcome;

            /// <summary>源 schema_version。</summary>
            public readonly uint SchemaVersion;

            public BindResult(
                IReadOnlyDictionary<(Outcome53.JudgmentLane, Outcome53.CareLane,
                    Outcome53.RecoveryLane, int), Outcome53.Outcome> rows,
                int uBandCount, Outcome53.Outcome defaultOutcome, uint schemaVersion)
            {
                Rows = rows; UBandCount = uBandCount;
                DefaultOutcome = defaultOutcome; SchemaVersion = schemaVersion;
            }
        }

        /// <summary>绑定 + 四条校验。任一违例 ⇒ 聚合 <see cref="BakeValidationException"/>。</summary>
        public static BindResult Bind(string json)
        {
            var errors = new List<string>();

            if (!JsonStage1Lexer.TryParse(json, out JsonNode root, out string lexErr))
                throw new BakeValidationException(
                    new List<string> { $"53_outcome_table.json 阶段1 词法失败:{lexErr}" });

            if (root.Kind != JsonNodeKind.Object)
                throw new BakeValidationException(
                    new List<string> { $"53_outcome_table.json 根须为对象(实得 {root.Kind})" });

            RejectUnknownKeys(root, RootKeys, "根", errors);
            RejectPolarityKeys(root, errors);

            uint schemaVersion = ReadSchemaVersion(root, errors);
            int uBandCount = ReadUBandCount(root, errors);

            if (!root.TryGet("rows", out JsonNode rowsNode) || rowsNode.Kind != JsonNodeKind.Array)
            {
                errors.Add("53_outcome_table.json 缺 rows 数组");
                throw new BakeValidationException(errors);
            }

            // ── 行解析(key 解析失败 = 硬失败 —— 行键缺失无法参与覆盖校验,不能聚合续跑)──
            var rows = new Dictionary<(Outcome53.JudgmentLane, Outcome53.CareLane,
                Outcome53.RecoveryLane, int), Outcome53.Outcome>();
            bool hasDefault = false;
            Outcome53.Outcome defaultOutcome = Outcome53.Outcome.Residual;

            for (int i = 0; i < rowsNode.Items.Count; i++)
            {
                JsonNode row = rowsNode.Items[i];
                if (row.Kind != JsonNodeKind.Object)
                {
                    errors.Add($"rows[{i}] 须为对象(实得 {row.Kind})");
                    continue;
                }
                RejectUnknownKeys(row, RowKeys, $"rows[{i}]", errors);
                RejectPolarityKeys(row, errors);

                if (!row.TryGet("key", out JsonNode keyNode) || keyNode.Kind != JsonNodeKind.String)
                {
                    errors.Add($"rows[{i}] 缺字符串 key(实得 {(keyNode == null ? "缺失" : keyNode.Kind.ToString())})");
                    continue;
                }
                string key = keyNode.Str;

                // default 行(key = "default")
                if (key == "default")
                {
                    if (!ReadOutcome(row, i, errors, out defaultOutcome)) continue;
                    if (hasDefault) errors.Add($"rows[{i}]:default 行重复");
                    hasDefault = true;
                    continue;
                }

                // 组合行:key = "P|C|D"
                string[] parts = key.Split('|');
                if (parts.Length != 3
                    || !TryParseLane(PLanes, parts[0], out var p)
                    || !TryParseLane(CLanes, parts[1], out var c)
                    || !TryParseLane(DLanes, parts[2], out var d))
                {
                    errors.Add($"rows[{i}] key「{key}」非法 —— 须为 P|C|D 组合键" +
                               "(轴取值见键域闭集;拼写错误会静默丢组合 ⇒ 硬失败)");
                    continue;
                }

                if (!row.TryGet("u_band", out JsonNode bandNode)) bandNode = default;
                int uBand = ReadUBand(row, i, bandNode, uBandCount, errors);
                if (uBand < 0) continue;

                if (!ReadOutcome(row, i, errors, out var outcome)) continue;
                var laneKey = ((Outcome53.JudgmentLane)p, (Outcome53.CareLane)c,
                               (Outcome53.RecoveryLane)d, uBand);
                if (rows.ContainsKey(laneKey))
                    errors.Add($"rows[{i}]:组合 ({p},{c},{d},u_band={uBand}) 重复");
                else
                    rows[laneKey] = outcome;
            }

            // ── 校验 ①:全覆盖(18 组合 × 每档无空洞;空洞处须有 default 行兜底)──
            if (uBandCount > 0 && errors.Count == 0)
            {
                var missing = new List<string>();
                for (int band = 0; band < uBandCount; band++)
                    foreach (var p in PLanes)
                        foreach (var c in CLanes)
                            foreach (var d in DLanes)
                            {
                                TryParseLane(PLanes, p, out var pv);
                                TryParseLane(CLanes, c, out var cv);
                                TryParseLane(DLanes, d, out var dv);
                                var laneKey = ((Outcome53.JudgmentLane)pv, (Outcome53.CareLane)cv,
                                               (Outcome53.RecoveryLane)dv, band);
                                if (!rows.ContainsKey(laneKey))
                                    missing.Add($"{p}|{c}|{d}|band{band}");
                            }
                if (missing.Count > 0)
                    errors.Add("覆盖校验失败:缺组合行 " + string.Join(", ", missing) +
                               " —— P/C/D 笛卡尔积无空洞(卡面 Note 2 ①;空洞 ⇒ 硬失败," +
                               "运行期 default 兜底仅防数据外版本漂移)");
            }

            if (!hasDefault)
                errors.Add("53_outcome_table.json 缺 default 行(显式兜底必备)");

            if (errors.Count > 0) throw new BakeValidationException(errors);

            return new BindResult(rows, uBandCount, defaultOutcome, schemaVersion);
        }

        // ── 行值读取(严格类型:非字符串 / 闭集外 = 记错)────────────────────

        /// <summary>Outcome 名 → 枚举(校验 ② 值域;结局轴两员 = 9 结算,不进表)。</summary>
        private static bool ReadOutcome(JsonNode row, int i, List<string> errors, out Outcome53.Outcome outcome)
        {
            outcome = Outcome53.Outcome.NoEcho;
            if (!row.TryGet("outcome", out JsonNode n) || n.Kind != JsonNodeKind.String)
            {
                errors.Add($"rows[{i}] 缺字符串 outcome(实得 {(n == null ? "缺失" : n.Kind.ToString())})");
                return false;
            }
            switch (n.Str)
            {
                case "residual": outcome = Outcome53.Outcome.Residual; return true;
                case "recurrence": outcome = Outcome53.Outcome.Recurrence; return true;
                case "trial_history": outcome = Outcome53.Outcome.TrialHistory; return true;
                case "no_echo": outcome = Outcome53.Outcome.NoEcho; return true;
                case "patient_lives":
                case "patient_loses":
                    errors.Add($"rows[{i}] outcome「{n.Str}」= 结局轴成员 —— 结局轴由 9 结算、" +
                               "53 只读,不进判定表(F-53.2 定义域收窄同构)");
                    return false;
                default:
                    errors.Add($"rows[{i}] outcome「{n.Str}」∉ 闭集(校验 ②;合法 = " +
                               string.Join("/", AllowedOutcomeNames) + ")");
                    return false;
            }
        }

        /// <summary>U 档读取(校验 ③ 的周边:非整数 / 负 / 超档 ⇒ 记错;Float token 结构性拒绝)。</summary>
        private static int ReadUBand(JsonNode row, int i, JsonNode node, int uBandCount, List<string> errors)
        {
            if (node.Kind != JsonNodeKind.Integer)
            {
                errors.Add($"rows[{i}] u_band 须为整数(实得 {node.Kind}={node.RawText})" +
                           " —— 浮点字面量结构性硬失败(校验 ③;ADR-006 边界)");
                return -1;
            }
            if (node.Int < 0 || (uBandCount > 0 && node.Int >= uBandCount))
            {
                errors.Add($"rows[{i}] u_band={node.Int} 超域 [0, {uBandCount - 1}]" +
                           $"(u_band_count={uBandCount})");
                return -1;
            }
            return (int)node.Int;
        }

        /// <summary>u_band_count 读取(≥1;P0 默认 1)。</summary>
        private static int ReadUBandCount(JsonNode root, List<string> errors)
        {
            if (!root.TryGet("u_band_count", out JsonNode n))
            {
                errors.Add("53_outcome_table.json 缺 u_band_count");
                return 0;
            }
            if (n.Kind != JsonNodeKind.Integer || n.Int < 1)
            {
                errors.Add($"u_band_count 须为 ≥1 整数(实得 {n.Kind}={n.RawText})");
                return 0;
            }
            return (int)n.Int;
        }

        private static uint ReadSchemaVersion(JsonNode root, List<string> errors)
        {
            if (!root.TryGet("schema_version", out JsonNode n))
            {
                errors.Add("53_outcome_table.json 缺 schema_version");
                return 0;
            }
            if (n.Kind != JsonNodeKind.Integer || n.Int < 0 || n.Int > uint.MaxValue)
            {
                errors.Add($"schema_version 须为 u32 域整数(实得 {n.Kind}={n.RawText})");
                return 0;
            }
            return (uint)n.Int;
        }

        /// <summary>轴取值解析(闭集外 ⇒ false;由调用方记错)。</summary>
        private static bool TryParseLane(string[] lanes, string s, out int index)
        {
            for (int i = 0; i < lanes.Length; i++)
                if (string.Equals(lanes[i], s, StringComparison.Ordinal))
                {
                    index = i; return true;
                }
            index = -1;
            return false;
        }

        /// <summary>校验 ④:极性类键出现即硬失败(53 不重判极性 —— 真源 = 9 注册表)。</summary>
        private static void RejectPolarityKeys(JsonNode obj, List<string> errors)
        {
            foreach (string key in obj.Keys)
                foreach (string tok in PolarityForbiddenKeys)
                    if (string.Equals(key, tok, StringComparison.OrdinalIgnoreCase))
                        errors.Add($"键「{key}」= 极性列 —— 53 不重判极性(校验 ④;" +
                                   "真源 = 9 注册表 treatable_by,作者态出现即硬失败)");
        }

        private static void RejectUnknownKeys(
            JsonNode obj, string[] allowed, string where, List<string> errors)
        {
            foreach (string key in obj.Keys)
            {
                bool ok = false;
                for (int i = 0; i < allowed.Length; i++)
                    if (string.Equals(allowed[i], key, StringComparison.Ordinal)) { ok = true; break; }
                if (!ok)
                    errors.Add($"{where} 未知键「{key}」[ADR-014·unknown-key]" +
                               "(拼写错误会静默丢字段 ⇒ 构建期硬失败)");
            }
        }
    }
}
