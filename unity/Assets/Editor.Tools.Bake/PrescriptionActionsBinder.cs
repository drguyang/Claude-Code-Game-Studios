// 权威来源:ADR-014 §三(阶段 2:per-schema 绑定 + 白名单/闭集/区间校验 + schema_version 版本化)
//          · GDD design/gdd/prescription-medication.md §F-11.1…F-11.5(剂量/半衰期/药效路径/检定/成长门)
//          · Story 001(校验器的**唯一调用点** —— 本件之前它是死代码)
//
// ⚠️ 本件是 story-001 双表 polarity 硬门的**生成器 + 唯一校验点**(阶段 2 的绑定面)。
//    `assets/data/prescription_actions.json` + `materia_lexicon.json` → 绑定 + 校验 → 行集。
//
// ⚠️ 枚举一律**按明文字符串**读入并映射(ADR-014 §三:禁 int 编码的 state)。
// ⚠️ 全部错误**聚合**后一次抛出(与 ItemDatabaseBaker 同纪律):不首个错即停,
//    让一份表的所有问题一次可见(BakeValidationException.Errors)。

using System;
using System.Collections.Generic;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>处方表行(绑定后的契约行)。</summary>
    public readonly struct PrescriptionActionRow
    {
        /// <summary>物品 key(须 ∈ ItemDef 闭集)。</summary>
        public readonly string ItemKey;

        /// <summary>处置 id(枚举序数;闭集校验取决于 OQ-11-2)。</summary>
        public readonly int ActionId;

        /// <summary>极性(causal / symptomatic)。</summary>
        public readonly string Polarity;

        public PrescriptionActionRow(string itemKey, int actionId, string polarity)
        {
            ItemKey = itemKey; ActionId = actionId; Polarity = polarity;
        }
    }

    /// <summary>`prescription_actions.json`(+本草词表)的阶段 2 绑定器:**唯一**调用
    /// 校验门的地方。</summary>
    public static class PrescriptionActionsBinder
    {
        // ── 已知键白名单(ADR-014 §三:未知键 = 烘焙期硬失败,防拼写错误静默丢字段)──

        private static readonly string[] ActionsRootKeys = { "schema_version", "_note", "dose_const", "actions" };
        private static readonly string[] DoseConstKeys = { "DOSE_BASE", "MAX_DOSE_DETENTS" };
        private static readonly string[] ActionRowKeys = { "item_key", "action_id", "polarity" };
        private static readonly string[] LexiconRootKeys = { "schema_version", "_note", "lexicon" };
        private static readonly string[] LexiconRowKeys = { "item_key", "功效词", "体征轴" };

        /// <summary>绑定结果(行集 + 常量回声,供写方落盘)。</summary>
        public readonly struct BindResult
        {
            public readonly List<PrescriptionActionRow> Rows;
            public readonly int DoseBase;
            public readonly int MaxDoseDetents;
            public readonly uint SchemaVersion;

            public BindResult(List<PrescriptionActionRow> rows, int doseBase, int maxDoseDetents, uint schemaVersion)
            {
                Rows = rows; DoseBase = doseBase; MaxDoseDetents = maxDoseDetents; SchemaVersion = schemaVersion;
            }
        }

        /// <summary>
        /// 绑定 + 校验。任一违例 ⇒ 聚合 <see cref="BakeValidationException"/>。
        /// </summary>
        /// <param name="actionsJson">`prescription_actions.json` 原文。</param>
        /// <param name="lexiconJson">`materia_lexicon.json` 原文。</param>
        /// <param name="itemsJson">`item_database_items.json` 原文(DC-1/DC-5 覆盖/DC-7 上界/AC-11-20 的输入)。</param>
        public static BindResult Bind(string actionsJson, string lexiconJson, string itemsJson)
        {
            var errors = new List<string>();

            // ── 阶段 1:词法(复用仓库唯一入口,零 JsonConvert / JObject)──
            if (!JsonStage1Lexer.TryParse(actionsJson, out JsonNode actionsRoot, out string lexErr1))
            {
                errors.Add($"prescription_actions.json 阶段1 词法失败:{lexErr1}");
                throw new BakeValidationException(errors);
            }
            if (!JsonStage1Lexer.TryParse(lexiconJson, out JsonNode lexiconRoot, out string lexErr2))
            {
                errors.Add($"materia_lexicon.json 阶段1 词法失败:{lexErr2}");
                throw new BakeValidationException(errors);
            }

            // ── 处方表根对象 ──
            if (actionsRoot.Kind != JsonNodeKind.Object)
            {
                errors.Add($"prescription_actions.json 根须为对象(实得 {actionsRoot.Kind})");
                throw new BakeValidationException(errors);
            }
            RejectUnknownKeys(actionsRoot, ActionsRootKeys, "prescription_actions.json 根", errors);

            // ── 本草词表根对象 ──
            if (lexiconRoot.Kind != JsonNodeKind.Object)
            {
                errors.Add($"materia_lexicon.json 根须为对象(实得 {lexiconRoot.Kind})");
                throw new BakeValidationException(errors);
            }
            RejectUnknownKeys(lexiconRoot, LexiconRootKeys, "materia_lexicon.json 根", errors);

            // ── schema_version(两文件须一致 —— 版本不匹配 = 硬失败,ADR-014 §三)──
            uint schemaVersion = ReadSchemaVersion(actionsRoot, "prescription_actions.json", errors);
            uint lexiconSchemaVersion = ReadSchemaVersion(lexiconRoot, "materia_lexicon.json", errors);
            if (schemaVersion != 0 && lexiconSchemaVersion != 0 && schemaVersion != lexiconSchemaVersion)
                errors.Add($"schema_version 不一致:actions={schemaVersion}, lexicon={lexiconSchemaVersion}");

            // ── dose_const ──
            int doseBase = 0, maxDoseDetents = 0;
            if (!actionsRoot.TryGet("dose_const", out JsonNode doseConst))
            {
                errors.Add("prescription_actions.json 缺 dose_const");
            }
            else if (doseConst.Kind != JsonNodeKind.Object)
            {
                errors.Add($"prescription_actions.json dose_const 须为对象(实得 {doseConst.Kind})");
            }
            else
            {
                RejectUnknownKeys(doseConst, DoseConstKeys, "dose_const", errors);
                doseBase = ReadInt(doseConst, "DOSE_BASE", errors, "dose_const");
                maxDoseDetents = ReadInt(doseConst, "MAX_DOSE_DETENTS", errors, "dose_const");
            }

            // ── DC-7:0 < DOSE_BASE ≤ 65536 × hi ──
            //    (hi 来自 item_database_items.json 的 drug_profile.dose_range[1];
            //     当前数据集 salicylic_acid 的 dose_range = null ⇒ 无 hi 约束,只校验 > 0)
            if (doseBase <= 0)
                errors.Add($"DC-7 违反:DOSE_BASE = {doseBase},必须 > 0");

            // ── actions 行集 ──
            var rows = new List<PrescriptionActionRow>();
            if (!actionsRoot.TryGet("actions", out JsonNode actionsArr))
            {
                errors.Add("prescription_actions.json 缺 actions 数组");
            }
            else if (actionsArr.Kind != JsonNodeKind.Array)
            {
                errors.Add($"prescription_actions.json actions 须为数组(实得 {actionsArr.Kind})");
            }
            else
            {
                BindActionRows(actionsArr, rows, errors);
            }

            // ── 本草词表行集 ──
            var lexiconKeys = new List<string>();
            if (!lexiconRoot.TryGet("lexicon", out JsonNode lexiconArr))
            {
                errors.Add("materia_lexicon.json 缺 lexicon 数组");
            }
            else if (lexiconArr.Kind != JsonNodeKind.Array)
            {
                errors.Add($"materia_lexicon.json lexicon 须为数组(实得 {lexiconArr.Kind})");
            }
            else
            {
                BindLexiconRows(lexiconArr, lexiconKeys, errors);
            }

            // ── DC-5:词表每药恰一条(重复检测)──
            var seenLexicon = new HashSet<string>(StringComparer.Ordinal);
            foreach (string key in lexiconKeys)
            {
                if (!seenLexicon.Add(key))
                    errors.Add($"DC-5 违反:药 '{key}' 在词表中出现多次");
            }

            // ── AC-11-02:零重定义(11 表字段集 ∌ drug_potency/half_life/axis_offset)──
            //    由 RejectUnknownKeys 隐式满足 —— 白名单不含这些字段,出现即被拒。

            // ── 跨文件校验(需 item_database_items.json)──
            if (!JsonStage1Lexer.TryParse(itemsJson, out JsonNode itemsRoot, out string lexErr3))
            {
                errors.Add($"item_database_items.json 阶段1 词法失败:{lexErr3}");
                throw new BakeValidationException(errors);
            }

            // 收集 ItemDef 闭集(base_id)和药品的 dose_range
            var itemDefIds = new HashSet<string>(StringComparer.Ordinal);
            var drugDoseRanges = new Dictionary<string, (int lo, int hi)?>(StringComparer.Ordinal);
            if (itemsRoot.Kind == JsonNodeKind.Object && itemsRoot.TryGet("items", out JsonNode itemsArr) && itemsArr.Kind == JsonNodeKind.Array)
            {
                for (int i = 0; i < itemsArr.Items.Count; i++)
                {
                    JsonNode item = itemsArr.Items[i];
                    if (item.Kind != JsonNodeKind.Object) continue;
                    if (item.TryGet("base_id", out JsonNode baseIdNode) && baseIdNode.Kind == JsonNodeKind.String)
                        itemDefIds.Add(baseIdNode.Str);
                    if (item.TryGet("category", out JsonNode catNode) && catNode.Kind == JsonNodeKind.String && catNode.Str == "drug")
                    {
                        string drugId = item.TryGet("base_id", out JsonNode bid) ? bid.Str : null;
                        if (drugId != null)
                        {
                            (int lo, int hi)? doseRange = null;
                            if (item.TryGet("drug_profile", out JsonNode dp) && dp.Kind == JsonNodeKind.Object &&
                                dp.TryGet("dose_range", out JsonNode dr) && dr.Kind == JsonNodeKind.Array && dr.Items.Count == 2)
                            {
                                doseRange = ((int)dr.Items[0].Int, (int)dr.Items[1].Int);
                            }
                            drugDoseRanges[drugId] = doseRange;
                        }
                    }
                }
            }

            // ── DC-1:处方表 item_key ⊂ ItemDef 闭集 ──
            foreach (var row in rows)
            {
                if (!itemDefIds.Contains(row.ItemKey))
                    errors.Add($"DC-1 违反:处方表 item_key '{row.ItemKey}' 不在 ItemDef 闭集中");
            }

            // ── DC-5(覆盖)+ AC-11-20:词表覆盖所有药 ──
            foreach (var drugId in drugDoseRanges.Keys)
            {
                if (!seenLexicon.Contains(drugId))
                    errors.Add($"AC-11-20 违反:药 '{drugId}' 无本草功效词");
            }

            // ── DC-7(上界):DOSE_BASE ≤ 65536 × hi(对每味有非空 dose_range 的药)──
            foreach (var kvp in drugDoseRanges)
            {
                if (kvp.Value.HasValue)
                {
                    int hi = kvp.Value.Value.hi;
                    long bound = 65536L * hi;
                    if (doseBase > bound)
                        errors.Add($"DC-7 违反:药 '{kvp.Key}' DOSE_BASE = {doseBase} > 65536 × {hi} = {bound}");
                }
            }

            if (errors.Count > 0)
                throw new BakeValidationException(errors);

            return new BindResult(rows, doseBase, maxDoseDetents, schemaVersion);
        }

        // ── 处方表行集 ────────────────────────────────────────────────────

        private static void BindActionRows(JsonNode arr, List<PrescriptionActionRow> rows, List<string> errors)
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

                string itemKey = ReadString(node, "item_key", errors, where);
                int actionId = ReadInt(node, "action_id", errors, where);
                string polarity = ReadString(node, "polarity", errors, where);

                // ── DC-3:polarity ∈ {causal, symptomatic} ──
                if (polarity != "causal" && polarity != "symptomatic")
                    errors.Add($"DC-3 违反:{where}.polarity=\"{polarity}\" ∉ {{causal, symptomatic}}");

                rows.Add(new PrescriptionActionRow(itemKey, actionId, polarity));
            }
        }

        // ── 本草词表行集 ──────────────────────────────────────────────────

        private static void BindLexiconRows(JsonNode arr, List<string> lexiconKeys, List<string> errors)
        {
            for (int i = 0; i < arr.Items.Count; i++)
            {
                JsonNode node = arr.Items[i];
                string where = $"lexicon[{i}]";
                if (node.Kind != JsonNodeKind.Object)
                {
                    errors.Add($"{where} 须为对象(实得 {node.Kind})");
                    continue;
                }
                RejectUnknownKeys(node, LexiconRowKeys, where, errors);

                string itemKey = ReadString(node, "item_key", errors, where);
                ReadString(node, "功效词", errors, where);
                // 体征轴:字符串数组(可选字段,当前数据集有)
                if (node.TryGet("体征轴", out JsonNode axes))
                {
                    if (axes.Kind != JsonNodeKind.Array)
                        errors.Add($"{where}.体征轴 须为数组(实得 {axes.Kind})");
                }

                lexiconKeys.Add(itemKey);
            }
        }

        // ── 读件(严格:类型不符即记错,绝不隐式转换)────────────────────────

        private static uint ReadSchemaVersion(JsonNode obj, string fileName, List<string> errors)
        {
            if (!obj.TryGet("schema_version", out JsonNode n))
            {
                errors.Add($"{fileName} 缺 schema_version");
                return 0;
            }
            if (n.Kind != JsonNodeKind.Integer || n.Int < 0 || n.Int > uint.MaxValue)
            {
                errors.Add($"{fileName} schema_version 须为 u32 域整数(实得 {n.Kind}={n.RawText})");
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
