// 权威来源:ADR-014 §三(阶段 2:per-schema 绑定 + 白名单/闭集/区间校验 + schema_version 版本化)
//          · GDD design/gdd/disease-simulation.md R1.3(treatable_by 字段)+ R3.2/R3.3(极性表 + 病种表)
//          · Story 007(校验器的**唯一调用点** —— 本件之前它是死代码)
//
// ⚠️ 本件是 story-007 的**生成器 + 唯一校验点**(阶段 2 的绑定面)。
//    `assets/data/disease_action_axis.json` → 绑定 + 校验 → 行集。
//
// ⚠️ 枚举一律**按明文字符串**读入并映射(ADR-014 §三:禁 int 编码的 state)。
// ⚠️ 全部错误**聚合**后一次抛出(与 ItemDatabaseBaker 同纪律):不首个错即停,
//    让一份表的所有问题一次可见(BakeValidationException.Errors)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>处置轴行(绑定后的契约行)。</summary>
    public readonly struct ActionAxisRow
    {
        /// <summary>处置 id(闭集成员)。</summary>
        public readonly int ActionId;

        /// <summary>符号名(诊断输出)。</summary>
        public readonly string Name;

        /// <summary>该处置的归属系统("10" / "11")。</summary>
        public readonly string Owner;

        public ActionAxisRow(int actionId, string name, string owner)
        {
            ActionId = actionId; Name = name; Owner = owner;
        }
    }

    /// <summary>treatable_by 关系行(绑定后的契约行)。</summary>
    public readonly struct TreatableByRow
    {
        /// <summary>病种 key(如 "DIS_MALARIA")。</summary>
        public readonly string DiseaseKey;

        /// <summary>处置 id(须 ∈ 处置轴闭集)。</summary>
        public readonly int ActionId;

        /// <summary>极性(causal / symptomatic)。</summary>
        public readonly string Polarity;

        public TreatableByRow(string diseaseKey, int actionId, string polarity)
        {
            DiseaseKey = diseaseKey; ActionId = actionId; Polarity = polarity;
        }
    }

    /// <summary>`disease_action_axis.json` 的阶段 2 绑定器:**唯一**调用
    /// 校验门的地方。</summary>
    public static class DiseaseActionAxisBinder
    {
        // ── 已知键白名单(ADR-014 §三:未知键 = 烘焙期硬失败,防拼写错误静默丢字段)──

        private static readonly string[] RootKeys = { "schema_version", "_note", "actions", "treatable_by" };
        private static readonly string[] ActionRowKeys = { "id", "name", "owner" };
        private static readonly string[] TreatableByEntryKeys = { "action", "polarity" };

        /// <summary>绑定结果(行集 + 校验通过后的源文本对,供 ConfigVersion 派生)。</summary>
        public readonly struct BindResult
        {
            public readonly List<ActionAxisRow> Actions;
            public readonly List<TreatableByRow> TreatableBy;
            public readonly uint SchemaVersion;

            public BindResult(List<ActionAxisRow> actions, List<TreatableByRow> treatableBy, uint schemaVersion)
            {
                Actions = actions; TreatableBy = treatableBy; SchemaVersion = schemaVersion;
            }
        }

        /// <summary>
        /// 绑定 + 校验。任一违例 ⇒ 聚合 <see cref="BakeValidationException"/>。
        /// </summary>
        /// <param name="json">`disease_action_axis.json` 原文。</param>
        public static BindResult Bind(string json)
        {
            var errors = new List<string>();

            // ── 阶段 1:词法(复用仓库唯一入口,零 JsonConvert / JObject)──
            if (!JsonStage1Lexer.TryParse(json, out JsonNode root, out string lexErr))
            {
                errors.Add($"disease_action_axis.json 阶段1 词法失败:{lexErr}");
                throw new BakeValidationException(errors);
            }

            // ── 根对象白名单 ──
            if (root.Kind != JsonNodeKind.Object)
            {
                errors.Add($"disease_action_axis.json 根须为对象(实得 {root.Kind})");
                throw new BakeValidationException(errors);
            }
            RejectUnknownKeys(root, RootKeys, "disease_action_axis.json 根", errors);

            // ── schema_version ──
            uint schemaVersion = ReadSchemaVersion(root, errors);

            // ── actions 数组 ──
            var actions = new List<ActionAxisRow>();
            if (!root.TryGet("actions", out JsonNode actionsArr))
            {
                errors.Add("disease_action_axis.json 缺 actions 数组");
            }
            else if (actionsArr.Kind != JsonNodeKind.Array)
            {
                errors.Add($"disease_action_axis.json actions 须为数组(实得 {actionsArr.Kind})");
            }
            else
            {
                BindActions(actionsArr, actions, errors);
            }

            // ── treatable_by 对象 ──
            var treatableBy = new List<TreatableByRow>();
            if (!root.TryGet("treatable_by", out JsonNode treatableByObj))
            {
                errors.Add("disease_action_axis.json 缺 treatable_by 对象");
            }
            else if (treatableByObj.Kind != JsonNodeKind.Object)
            {
                errors.Add($"disease_action_axis.json treatable_by 须为对象(实得 {treatableByObj.Kind})");
            }
            else
            {
                BindTreatableBy(treatableByObj, treatableBy, errors);
            }

            // ── ★ 校验器调用点(本故事承重面)──
            //    仅当绑定本身无错时跑 —— 否则断言的是半成型行集,错误会重复且难读。
            if (errors.Count == 0)
            {
                try
                {
                    DiseaseActionAxisValidator.Validate(actions, treatableBy);
                }
                catch (Exception ex)
                {
                    errors.Add("9-DC 校验:" + ex.Message);
                }
            }

            if (errors.Count > 0)
                throw new BakeValidationException(errors);

            return new BindResult(actions, treatableBy, schemaVersion);
        }

        // ── actions 行集 ────────────────────────────────────────────────────

        private static void BindActions(JsonNode arr, List<ActionAxisRow> rows, List<string> errors)
        {
            for (int i = 0; i < arr.Items.Count; i++)
            {
                JsonNode node = arr.Items[i];
                string where = $"actions[{i}]";
                if (node.Kind != JsonNodeKind.Object)
                {
                    errors.Add($"{where} 须为对象(实得 {node.Kind})");
                    continue;
                }
                RejectUnknownKeys(node, ActionRowKeys, where, errors);

                int id = ReadInt(node, "id", errors, where);
                string name = ReadString(node, "name", errors, where);
                string owner = ReadString(node, "owner", errors, where);

                // ── 9-DC-1:owner ∈ {"10", "11"} ──
                if (owner != "10" && owner != "11")
                    errors.Add($"9-DC-1 违反:{where}.owner=\"{owner}\" ∉ {{\"10\", \"11\"}}");

                rows.Add(new ActionAxisRow(id, name, owner));
            }
        }

        // ── treatable_by 行集 ───────────────────────────────────────────────

        private static void BindTreatableBy(JsonNode obj, List<TreatableByRow> rows, List<string> errors)
        {
            foreach (string diseaseKey in obj.Keys)
            {
                JsonNode entries = obj.TryGet(diseaseKey, out JsonNode e) ? e : null;
                if (entries == null)
                {
                    errors.Add($"treatable_by[{diseaseKey}] 缺失");
                    continue;
                }
                if (entries.Kind != JsonNodeKind.Array)
                {
                    errors.Add($"treatable_by[{diseaseKey}] 须为数组(实得 {entries.Kind})");
                    continue;
                }

                for (int i = 0; i < entries.Items.Count; i++)
                {
                    JsonNode entry = entries.Items[i];
                    string where = $"treatable_by[{diseaseKey}][{i}]";
                    if (entry.Kind != JsonNodeKind.Object)
                    {
                        errors.Add($"{where} 须为对象(实得 {entry.Kind})");
                        continue;
                    }
                    RejectUnknownKeys(entry, TreatableByEntryKeys, where, errors);

                    int action = ReadInt(entry, "action", errors, where);
                    string polarity = ReadString(entry, "polarity", errors, where);

                    // ── 9-DC-2:polarity ∈ {causal, symptomatic} ──
                    if (polarity != "causal" && polarity != "symptomatic")
                        errors.Add($"9-DC-2 违反:{where}.polarity=\"{polarity}\" ∉ {{causal, symptomatic}}");

                    rows.Add(new TreatableByRow(diseaseKey, action, polarity));
                }
            }
        }

        // ── 读件(严格:类型不符即记错,绝不隐式转换)────────────────────────

        private static uint ReadSchemaVersion(JsonNode obj, List<string> errors)
        {
            if (!obj.TryGet("schema_version", out JsonNode n))
            {
                errors.Add("disease_action_axis.json 缺 schema_version");
                return 0;
            }
            if (n.Kind != JsonNodeKind.Integer || n.Int < 0 || n.Int > uint.MaxValue)
            {
                errors.Add($"disease_action_axis.json schema_version 须为 u32 域整数(实得 {n.Kind}={n.RawText})");
                return 0;
            }
            return (uint)n.Int;
        }

        private static int ReadInt(JsonNode obj, string key, List<string> errors, string where)
        {
            string label = $"{where}.{key}";
            if (!obj.TryGet(key, out JsonNode n))
            {
                errors.Add($"{label} 缺失");
                return 0;
            }
            if (n.Kind != JsonNodeKind.Integer)
            {
                errors.Add($"{label} 须为整数(实得 {n.Kind};禁 float/字符串)");
                return 0;
            }
            return (int)n.Int;
        }

        private static string ReadString(JsonNode obj, string key, List<string> errors, string where)
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
            return n.Str;
        }

        /// <summary>未知键 = 硬失败(ADR-014 §三,防拼写错误静默丢字段)。</summary>
        private static void RejectUnknownKeys(JsonNode obj, string[] allowed, string where, List<string> errors)
        {
            foreach (string key in obj.Keys)
            {
                bool ok = false;
                for (int i = 0; i < allowed.Length; i++)
                    if (string.Equals(allowed[i], key, StringComparison.Ordinal)) { ok = true; break; }
                if (!ok)
                    errors.Add($"{where} 未知键「{key}」(拼写错误会静默丢字段 ⇒ 构建期硬失败)");
            }
        }
    }
}
