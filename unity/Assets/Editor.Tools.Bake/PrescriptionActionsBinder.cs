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
using DaYiJingCheng.Sim.Contracts;   // FixParse(ADR-006 §一:JSON 里 Fix 字段写字符串)

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

            /// <summary>烘焙期派生常量 `single_dose_max`(raw Q16.16;AC-11-09 —— **零手填**)。
            /// <para>= max over(全部药 × `dose_range.hi`) of |`dose_potency`|,
            /// 由 <see cref="PrescriptionDerivedBaker"/> 在绑定末尾派生;
            /// 供 9 的 F1 clamp 上界 `MAX_ACTIVE_DOSE × single_dose_max` 消费。</para></summary>
            public readonly long SingleDoseMaxRaw;

            /// <summary>⚠️ 本次烘焙的 **DC-2 / DC-6 用了真源**(闭集 / 地板值均为真源)。
            /// <para>理由:两条判据的真源已落地 —— DC-2 的处置 id master 已登记(9 侧
            /// `disease_action_axis.json` 存在)· DC-6 的 `NOISE_BAND_9` 归 9 已立(BL-2 已闭)。
            /// **恒为 true 直至真源落地** —— 存在的意义是让「判据非真判」在构建日志里**可见**,
            /// 不静默(承 story-003 的「判据-文本背离」记账纪律)。</para></summary>
            public readonly bool RealRegistryUsed;

            /// <summary>真源期诊断(DC-2 / DC-6 的发现)—— **进 `errors`、硬失败**。
            /// <para>⚠️ 用有主门槛硬失败 = 正确行为。
            /// 真源落地后,调用方须把本列**显式升格**为 errors(禁借绿)。</para></summary>
            public readonly List<string> RealWarnings;

            public BindResult(List<PrescriptionActionRow> rows, int doseBase, int maxDoseDetents,
                              uint schemaVersion, long singleDoseMaxRaw, bool realRegistryUsed,
                              List<string> realWarnings = null)
            {
                Rows = rows; DoseBase = doseBase; MaxDoseDetents = maxDoseDetents;
                SchemaVersion = schemaVersion; SingleDoseMaxRaw = singleDoseMaxRaw;
                RealRegistryUsed = realRegistryUsed;
                RealWarnings = realWarnings ?? new List<string>();
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

            // ⚠️ DC-2 / DC-6 的真源期产出走本列,**进 errors**(硬失败)。
            //    理由:两条判据的真源已落地(处置 id master / NOISE_BAND_9),
            //    用有主门槛硬失败 = 正确行为。
            //    真源落地后,调用方须把本列**显式升格**为 errors(禁借绿)。
            // ⚠️ 2026-10-07 评审 M4:局部变量改名为 `diagnostics`,以区分于真正的 warnings。
            var diagnostics = new List<string>();

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
            var drugPotencyRaw = new Dictionary<string, long>(StringComparer.Ordinal);
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
                                int lo = (int)dr.Items[0].Int, hi = (int)dr.Items[1].Int;
                                // ⚠️ 逆序域是**非法输入**(非「整剂路径」)—— 静默放行会让 DC-6
                                //    在空档序列上恒通过(2026-10-07 评审缺陷 2)。与 42 侧
                                //    `DentchDoseSelector` 同口径显式拒。
                                if (lo > hi)
                                    errors.Add($"DC-7/DC-6 输入:药 '{drugId}' dose_range = [{lo}, {hi}] 逆序" +
                                               "(下界 > 上界 ⇒ 相邻档序列为空 ⇒ DC-6 静默恒通过)");
                                else
                                    doseRange = (lo, hi);
                            }
                            drugDoseRanges[drugId] = doseRange;

                            // DC-6 输入:药效幅值(ADR-006 §Decision 一:JSON 形 = 字符串,经 FixParse)
                            if (dp.Kind == JsonNodeKind.Object &&
                                dp.TryGet("drug_potency", out JsonNode dpn) &&
                                dpn.Kind == JsonNodeKind.String)
                            {
                                try { drugPotencyRaw[drugId] = FixParse.Parse(dpn.Str).Raw; }
                                catch (Exception ex)
                                {
                                    errors.Add($"DC-6 输入:药 '{drugId}' 的 drug_potency " +
                                               $"\"{dpn.Str}\" 非合法 Fix 字面量:{ex.Message}");
                                }
                            }
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

            // ── DC-6:逐药相邻档 dose_potency 差 ≥ 可感知地板 ──
            //    ⚠️ 2026-10-06 补:机制此前**只存在于 Sim 的比较器**,从未接进烘焙门。
            //    地板值现为**真源**(BL-2:`NOISE_BAND_9` 归 9,已立)⇒ 判据本体 RUN。
            //    求值经 DoseCalculator(F-11.1 唯一实现,AC-11-02)。
            int dc6Evaluated = 0, dc6SkippedNoRange = 0, dc6SkippedNoPotency = 0;
            foreach (var kvp in drugDoseRanges)
            {
                if (!kvp.Value.HasValue) { dc6SkippedNoRange++; continue; }   // 整剂路径 ⇒ 无相邻档
                if (!drugPotencyRaw.TryGetValue(kvp.Key, out long potencyRaw))
                { dc6SkippedNoPotency++; continue; }                          // 21a 可空 ⇒ 见下方记账

                var dr = new Sim.Contracts.DoseRange(kvp.Value.Value.lo, kvp.Value.Value.hi);
                errors.AddRange(PrescriptionActionIdRegistry.ValidatePerceptibleFloor(
                    potencyRaw, dr, doseBase));
                dc6Evaluated++;
            }

            // ⚠️ **覆盖率显式记账**(2026-10-07 评审缺陷 4):上面两条 `continue` 是**合法**跳过,
            //    但静默跳过 = 「看起来绿但没跑」。当前数据集 salicylic_acid 恰 `dose_range = null`
            //    ⇒ DC-6 **对当前数据集零求值**;不记账则读者会把「零覆盖」读成「已合规」。
            //    另:第 2 条跳过与 `single_dose_max` 对同一输入类判定**相反**(后者硬失败)——
            //    该背离须可见,故此处强制报出。
            diagnostics.Add($"[DC-6 覆盖] 求值 {dc6Evaluated} 味 · 跳过(无 dose_range/整剂路径){dc6SkippedNoRange} 味 · " +
                         $"跳过(无 drug_potency){dc6SkippedNoPotency} 味 —— " +
                         (dc6Evaluated == 0
                             ? "⚠️ **本次 DC-6 零求值**(判据未跑,禁读成绿)。"
                             : "仅上述被求值的药受判。") +
                         (dc6SkippedNoPotency > 0
                             ? $" ⚠️ 其中 {dc6SkippedNoPotency} 味缺 drug_potency 被跳过 —— " +
                               "同一输入类对 single_dose_max **硬失败**、对 DC-6 **静默通过**,背离须记账。"
                             : string.Empty));

            if (errors.Count > 0)
                throw new BakeValidationException(errors);

            // ── AC-11-09:single_dose_max 烘焙期派生(零手填;唯一派生点 = PrescriptionDerivedBaker)──
            //    改任一源字段(drug_potency / dose_range / DOSE_BASE)⇒ 派生值自动重算,
            //    并随 ConfigVersion(源内容哈希)一起变 —— 测试据此判「联动」。
            long singleDoseMaxRaw = PrescriptionDerivedBaker.DeriveSingleDoseMaxRaw(itemsRoot, doseBase);

            return new BindResult(rows, doseBase, maxDoseDetents, schemaVersion, singleDoseMaxRaw,
                                  realRegistryUsed: true, realWarnings: diagnostics);
        }

        // ── 处方表行集 ────────────────────────────────────────────────────

        private static void BindActionRows(JsonNode arr, List<PrescriptionActionRow> rows,
                                           List<string> errors)
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
                bool actionIdPresent = node.TryGet("action_id", out JsonNode actionIdNode) &&
                                       actionIdNode.Kind == JsonNodeKind.Integer;
                int actionId = ReadInt(node, "action_id", errors, where);
                string polarity = ReadString(node, "polarity", errors, where);

                // ── DC-3:polarity ∈ {causal, symptomatic} ──
                if (polarity != "causal" && polarity != "symptomatic")
                    errors.Add($"DC-3 违反:{where}.polarity=\"{polarity}\" ∉ {{causal, symptomatic}}");

                // ── DC-2:action_id ∈ 处置 id 注册表闭集 ──
                //    ⚠️ 2026-10-06 补:此前 action_id **读入后从不校验**(任何 int 放行)。
                //    现以**真源闭集**驱动 —— 判据本体 RUN(真源 = 9 的处置 id master,
                //    GDD `:748` 逐字登记「该枚举的 master 住哪一份文件未登记」)。
                //    ⚠️ **真源期落 errors 不落 warnings** —— 用有主的闭集硬失败 = 正确行为。
                //    字段**缺失**时不再报「0 ∉ 闭集」—— 那是失真(缺失已由 ReadInt 记账)。
                errors.AddRange(PrescriptionActionIdRegistry.ValidateActionId(actionId, null, actionIdPresent));

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
