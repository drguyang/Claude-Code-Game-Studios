// 权威来源:ADR-014 §三(阶段2 = 自研 per-schema 绑定 + FixParse + 白名单)· §四(Fix 字段必须 JSON 字符串)
//          · Story 008(绑定规则;门的调用在 ItemDatabaseBaker,本文件只做结构/类型/白名单)

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.EditorTools.Gates;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>阶段2 绑定完成的物品行(raw 串保留供门读取)。</summary>
    internal sealed class BoundItem
    {
        public ItemDef Def;
        public string RawCategory;
        public string RawState;
        public string RawStackMax;
        public string RawWeight;
        public string RawTcmBody;
        public string Label;
    }

    /// <summary>阶段2 绑定完成的配方行。</summary>
    internal sealed class BoundRecipe
    {
        public Recipe Rec;
        public string RawOwner;
        public string Label;
    }

    /// <summary>阶段2 绑定完成的常量表(含门所需 raw / 地板)。</summary>
    internal sealed class BoundConstants
    {
        public RecipeSettlementConstants Constants;
        public string RawMaxQuality;
        public Fix PerceptibleFloor;
    }

    /// <summary>三文件绑定总包(错误聚合 + 绑定产物)。</summary>
    internal sealed class BindPackage
    {
        public readonly List<string> Errors = new List<string>();
        public readonly List<BoundItem> Items = new List<BoundItem>();
        public readonly List<BoundRecipe> Recipes = new List<BoundRecipe>();
        public readonly List<string> RecipeRawOwners = new List<string>();
        public BoundConstants Constants;
        public HashSet<string> KnownInjuryIds; // null = 源文件未提供(门跳过外键侧)
    }

    /// <summary>阶段2:JSON DOM → 领域 struct 的 per-schema 绑定。
    /// 白名单外键 = 错误;Fix 只经 <see cref="FixParse.Parse"/>;float/int token 按字段规则结构性拒收。
    /// <para><b>本类不抛异常</b>(错误进列表,聚合与 throw 归 ItemDatabaseBaker —— 既有门零 throw 纪律同口径)。</para>
    /// <example><code>BindPackage pkg = ItemDatabaseBinder.BindAll(itemsJson, recipesJson, constantsJson);</code></example>
    /// </summary>
    internal static class ItemDatabaseBinder
    {
        private static readonly string[] TopItemsKeys = { "schema_version", "_note", "items", "known_injury_ids" };
        private static readonly string[] TopRecipesKeys = { "schema_version", "_note", "recipes" };
        private static readonly string[] ItemRecordKeys =
            { "base_id", "processing_state", "display_name", "category", "stack_max", "weight", "deprecated", "legal_transitions",
              "drug_profile", "gather_profile", "tcm_profile", "inflicts_injury" };
        private static readonly string[] ItemRequiredKeys =
            { "base_id", "processing_state", "display_name", "category", "stack_max", "weight", "deprecated", "legal_transitions" };
        private static readonly string[] DrugProfileKeys =
            { "indications", "contraindications", "dose_range", "drug_potency", "onset", "peak", "half_life", "elimination",
              "quality_axis", "axis_offset_by_quality", "drug_quality_character" };
        private static readonly string[] GatherKeys = { "ecosystem", "parts", "qty_per_node", "quality_character", "quality_distribution" };
        private static readonly string[] GatherRequiredKeys = { "ecosystem", "parts", "qty_per_node" };
        private static readonly string[] RecipeRecordKeys =
            { "recipe_id", "owner", "inputs", "outputs", "duration_ticks", "skill_gate", "min_quality", "boundary_state" };
        private static readonly string[] RecipeRequiredKeys = RecipeRecordKeys;
        private static readonly string[] ConstantsKeys =
            { "schema_version", "_note", "qty_mult_min", "qty_mult_max", "skill_mod_cap", "qual_mod_cap", "equip_mod_cap",
              "env_mod_min", "env_mod_max", "retain_min", "retain_max", "eff_min", "eff_max", "max_quality", "skill_cap",
              "perceptible_floor" };
        private static readonly string[] ConstantsRequiredKeys =
            { "qty_mult_min", "qty_mult_max", "skill_mod_cap", "qual_mod_cap", "equip_mod_cap", "env_mod_min", "env_mod_max",
              "retain_min", "retain_max", "eff_min", "eff_max", "max_quality", "skill_cap", "perceptible_floor" };

        /// <summary>绑定三个已词法通过的 DOM。</summary>
        public static BindPackage BindAll(JsonNode itemsRoot, JsonNode recipesRoot, JsonNode constantsRoot)
        {
            var pkg = new BindPackage();
            BindConstants(constantsRoot, pkg);
            BindItems(itemsRoot, pkg);
            BindRecipes(recipesRoot, pkg);
            return pkg;
        }

        // ══════════ 顶层与 schema_version ══════════

        private static void CheckSchemaAndUnknownTop(JsonNode root, string[] allowed, string fileLabel, BindPackage pkg)
        {
            if (!root.TryGet("schema_version", out JsonNode sv))
            {
                pkg.Errors.Add($"{fileLabel}:缺失必填键 \"schema_version\"");
            }
            else if (sv.Kind != JsonNodeKind.Integer)
            {
                pkg.Errors.Add($"{fileLabel}:schema_version 须为 JSON 整数 token —— schema 不可读即烘焙硬失败(ADR-010 §七)");
            }
            else if ((ulong)sv.Int != CookedFormat.SchemaVersion)
            {
                pkg.Errors.Add($"{fileLabel}:schema_version = {sv.Int},期望 {CookedFormat.SchemaVersion} —— " +
                               "schema 不可读/不匹配即烘焙硬失败(ADR-010 §七 · ADR-014 §三)");
            }

            var allowedSet = new HashSet<string>(allowed, StringComparer.Ordinal);
            foreach (string key in root.Keys)
            {
                if (!allowedSet.Contains(key))
                    pkg.Errors.Add($"{fileLabel}:未知键 \"{key}\" —— ADR-014 §三 白名单外,烘焙期硬失败");
            }
        }

        private static void AddMissingRequired(JsonNode obj, string[] required, string label, BindPackage pkg)
        {
            foreach (string key in required)
            {
                if (!obj.TryGet(key, out _))
                    pkg.Errors.Add($"{label}:缺失必填键 \"{key}\"");
            }
        }

        private static void AddUnknown(JsonNode obj, HashSet<string> allowed, string label, BindPackage pkg)
        {
            foreach (string key in obj.Keys)
            {
                if (!allowed.Contains(key))
                    pkg.Errors.Add($"{label}:未知键 \"{key}\" —— ADR-014 §三 白名单外,烘焙期硬失败");
            }
        }

        // ══════════ 常量表 ══════════

        private static void BindConstants(JsonNode root, BindPackage pkg)
        {
            const string fileLabel = "item_database_constants.json";
            CheckSchemaAndUnknownTop(root, ConstantsKeys, fileLabel, pkg);
            AddMissingRequired(root, ConstantsRequiredKeys, fileLabel, pkg);

            Fix? qmMin = BindRequiredFix(root, "qty_mult_min", fileLabel, pkg);
            Fix? qmMax = BindRequiredFix(root, "qty_mult_max", fileLabel, pkg);
            Fix? skillCapMod = BindRequiredFix(root, "skill_mod_cap", fileLabel, pkg);
            Fix? qualMod = BindRequiredFix(root, "qual_mod_cap", fileLabel, pkg);
            Fix? equipMod = BindRequiredFix(root, "equip_mod_cap", fileLabel, pkg);
            Fix? envMin = BindRequiredFix(root, "env_mod_min", fileLabel, pkg);
            Fix? envMax = BindRequiredFix(root, "env_mod_max", fileLabel, pkg);
            Fix? retainMin = BindRequiredFix(root, "retain_min", fileLabel, pkg);
            Fix? retainMax = BindRequiredFix(root, "retain_max", fileLabel, pkg);
            Fix? effMin = BindRequiredFix(root, "eff_min", fileLabel, pkg);
            Fix? effMax = BindRequiredFix(root, "eff_max", fileLabel, pkg);
            Fix? floor = BindRequiredFix(root, "perceptible_floor", fileLabel, pkg);

            int maxQuality = 0;
            string rawMaxQuality = "0";
            if (root.TryGet("max_quality", out JsonNode mq))
            {
                if (mq.Kind == JsonNodeKind.Integer)
                {
                    rawMaxQuality = mq.RawText;
                    if (mq.Int >= int.MinValue && mq.Int <= int.MaxValue) maxQuality = (int)mq.Int;
                    else pkg.Errors.Add($"{fileLabel}:max_quality 超出 int 域");
                }
                else if (mq.Kind == JsonNodeKind.String)
                {
                    // 字面形 "4" 合法(AC-13 夹具口径)—— raw 交 ValidateMaxQuality 门。
                    rawMaxQuality = mq.Str;
                    int.TryParse(mq.Str, System.Globalization.NumberStyles.Integer,
                        System.Globalization.CultureInfo.InvariantCulture, out maxQuality);
                }
                else
                {
                    pkg.Errors.Add($"{fileLabel}:\"max_quality\" 须为整数或整数字符串 token —— " +
                                   "构建期硬失败(AC-21a-13)");
                }
            }

            int skillCapNum = root.TryGet("skill_cap", out JsonNode sc) && sc.Kind == JsonNodeKind.Integer
                ? (int)sc.Int
                : 0;
            if (root.TryGet("skill_cap", out JsonNode scChk) && scChk.Kind != JsonNodeKind.Integer)
                pkg.Errors.Add($"{fileLabel}:\"skill_cap\" 须为 JSON 整数 token —— SKILL_CAP 引用自 30(AC-21a-48 零硬编码)");

            if (qmMin.HasValue && qmMax.HasValue && skillCapMod.HasValue && qualMod.HasValue && equipMod.HasValue &&
                envMin.HasValue && envMax.HasValue && retainMin.HasValue && retainMax.HasValue &&
                effMin.HasValue && effMax.HasValue && floor.HasValue)
            {
                pkg.Constants = new BoundConstants
                {
                    Constants = new RecipeSettlementConstants(
                        qmMin.Value, qmMax.Value, skillCapMod.Value, qualMod.Value, equipMod.Value,
                        envMin.Value, envMax.Value, retainMin.Value, retainMax.Value,
                        effMin.Value, effMax.Value, maxQuality, skillCapNum),
                    RawMaxQuality = rawMaxQuality,
                    PerceptibleFloor = floor.Value,
                };
            }
        }

        // ══════════ 物品表 ══════════

        private static void BindItems(JsonNode root, BindPackage pkg)
        {
            const string fileLabel = "item_database_items.json";
            CheckSchemaAndUnknownTop(root, TopItemsKeys, fileLabel, pkg);

            if (root.TryGet("known_injury_ids", out JsonNode kid))
            {
                if (kid.Kind == JsonNodeKind.Null) { /* 未提供:门跳过外键侧 */ }
                else if (TryBindStringArray(kid, fileLabel + ".known_injury_ids", pkg, out string[] ids) && ids != null)
                    pkg.KnownInjuryIds = new HashSet<string>(ids, StringComparer.Ordinal);
            }

            if (!root.TryGet("items", out JsonNode arr) || arr.Kind != JsonNodeKind.Array)
            {
                pkg.Errors.Add($"{fileLabel}:缺失必填键 \"items\" 或其非数组");
                return;
            }

            for (int i = 0; i < arr.Items.Count; i++)
            {
                JsonNode rec = arr.Items[i];
                if (rec.Kind != JsonNodeKind.Object)
                {
                    pkg.Errors.Add($"{fileLabel}:items[{i}] 非对象");
                    continue;
                }
                BindItemRecord(rec, $"{fileLabel}:items[{i}]", pkg);
            }
        }

        private static void BindItemRecord(JsonNode rec, string label, BindPackage pkg)
        {
            // stackable 拒收 = 门(AC-21a-59)在绑定层执行 —— 必须拿原始键集(两级缝隙,ItemDef 注)。
            var rawKeys = new List<string>(rec.Keys);
            foreach (string err in ItemDbValidation.FindStoredStackableKeys(rawKeys, label))
                pkg.Errors.Add(err);

            var allowed = new HashSet<string>(ItemRecordKeys, StringComparer.Ordinal);
            AddUnknown(rec, allowed, label, pkg);
            AddMissingRequired(rec, ItemRequiredKeys, label, pkg);

            var def = new ItemDef();

            if (rec.TryGet("base_id", out JsonNode baseId) && baseId.Kind == JsonNodeKind.String)
                def.BaseId = baseId.Str;
            else if (rec.TryGet("base_id", out _))
                pkg.Errors.Add($"{label}:\"base_id\" 须为 JSON 字符串");

            // processing_state:int/float 编码 ⇒ AC-21a-26 结构性硬失败;字面量闭合交门(AC-22)。
            string rawState = null;
            if (rec.TryGet("processing_state", out JsonNode st))
            {
                if (st.Kind == JsonNodeKind.Integer || st.Kind == JsonNodeKind.Float)
                {
                    pkg.Errors.Add($"{label}:processing_state 以 int 编码(token={st.Kind}, 值={st.RawText}) —— " +
                                   "item_key 字段禁 int 编码;装配期硬失败(AC-21a-26 · D-21-13;唯一合法作者态 = assets/data/*.json 明文)");
                }
                else if (st.Kind == JsonNodeKind.String)
                {
                    rawState = st.Str;
                    def.ProcessingState = ItemDbValidation.TryParseProcessingState(rawState, out ProcessingState parsedState)
                        ? parsedState
                        : default; // 闭合失败交 ValidateEnumClosure(AC-22)
                }
                else
                {
                    pkg.Errors.Add($"{label}:\"processing_state\" 须为 JSON 字符串字面量(AC-21a-22)");
                }
            }

            if (rec.TryGet("display_name", out JsonNode dn) && dn.Kind == JsonNodeKind.String)
                def.DisplayName = dn.Str;
            else if (rec.TryGet("display_name", out _))
                pkg.Errors.Add($"{label}:\"display_name\" 须为 JSON 字符串");

            string rawCategory = null;
            if (rec.TryGet("category", out JsonNode cat))
            {
                if (cat.Kind == JsonNodeKind.Object || cat.Kind == JsonNodeKind.Array || cat.Kind == JsonNodeKind.Null)
                    pkg.Errors.Add($"{label}:\"category\" 须为标量字面量(AC-21a-22)");
                else
                {
                    rawCategory = cat.RawText;
                    def.Category = ItemDbValidation.TryParseItemCategory(rawCategory, out ItemCategory parsedCat)
                        ? parsedCat
                        : default;
                }
            }

            def.StackMax = BindGatedInt(rec, "stack_max", label, pkg, "构建期硬失败(AC-21a-15)", out string rawStack);
            def.Weight = BindGatedInt(rec, "weight", label, pkg, "构建期硬失败(AC-21a-15)", out string rawW);

            if (rec.TryGet("deprecated", out JsonNode dep))
            {
                if (dep.Kind == JsonNodeKind.Boolean) def.Deprecated = dep.Bool;
                else pkg.Errors.Add($"{label}:\"deprecated\" 须为 JSON 布尔 token");
            }

            if (rec.TryGet("legal_transitions", out JsonNode lt))
            {
                if (lt.Kind == JsonNodeKind.Array)
                {
                    var decls = new string[lt.Items.Count];
                    bool ok = true;
                    for (int i = 0; i < lt.Items.Count; i++)
                    {
                        if (lt.Items[i].Kind != JsonNodeKind.String)
                        {
                            pkg.Errors.Add($"{label}:legal_transitions[{i}] 须为字符串(明文 from>to)");
                            ok = false;
                            break;
                        }
                        decls[i] = lt.Items[i].Str;
                    }
                    if (ok) def.LegalTransitions = decls;
                }
                else
                {
                    pkg.Errors.Add($"{label}:\"legal_transitions\" 须为数组");
                }
            }

            string rawTcmBody = null;
            if (rec.TryGet("tcm_profile", out JsonNode tcm))
            {
                if (tcm.Kind == JsonNodeKind.Null) rawTcmBody = null;
                else if (tcm.Kind == JsonNodeKind.Object)
                {
                    // P0 恒空:键可出现(占位),非 null 值交 ValidateP0Narrowing 判 AC-23。
                    rawTcmBody = BuildTcmBody(tcm);
                    def.TcmProfile = new TcmProfile();
                }
                else pkg.Errors.Add($"{label}:\"tcm_profile\" 须为对象或 null");
            }

            if (rec.TryGet("drug_profile", out JsonNode drug))
            {
                if (drug.Kind == JsonNodeKind.Null) { /* 可空 */ }
                else if (drug.Kind == JsonNodeKind.Object) def.DrugProfile = BindDrugProfile(drug, label, pkg);
                else pkg.Errors.Add($"{label}:\"drug_profile\" 须为对象或 null");
            }

            if (rec.TryGet("gather_profile", out JsonNode gather))
            {
                if (gather.Kind == JsonNodeKind.Null) { /* 可空 */ }
                else if (gather.Kind == JsonNodeKind.Object) def.GatherProfile = BindGatherProfile(gather, label, pkg);
                else pkg.Errors.Add($"{label}:\"gather_profile\" 须为对象或 null");
            }

            if (rec.TryGet("inflicts_injury", out JsonNode inf))
            {
                if (inf.Kind == JsonNodeKind.Null) { /* 可空 */ }
                else if (inf.Kind == JsonNodeKind.String) def.InflictsInjury = new[] { inf.Str };
                else if (TryBindStringArray(inf, label + ".inflicts_injury", pkg, out string[] inj) && inj != null)
                    def.InflictsInjury = inj;
                else pkg.Errors.Add($"{label}:\"inflicts_injury\" 须为字符串或字符串数组");
            }

            pkg.Items.Add(new BoundItem
            {
                Def = def,
                RawCategory = rawCategory,
                RawState = rawState,
                RawStackMax = rawStack,
                RawWeight = rawW,
                RawTcmBody = rawTcmBody,
                Label = $"{def.BaseId}/{rawState ?? "?"}",
            });
        }

        private static DrugProfile BindDrugProfile(JsonNode drug, string label, BindPackage pkg)
        {
            const string drugLabelSuffix = ".drug_profile";
            var allowed = new HashSet<string>(DrugProfileKeys, StringComparer.Ordinal);
            AddUnknown(drug, allowed, label + drugLabelSuffix, pkg);
            // D-21-6:字段必须在(值可 null)—— 白名单即绑定期义务。
            AddMissingRequired(drug, DrugProfileKeys, label + drugLabelSuffix, pkg);

            var profile = new DrugProfile();

            if (drug.TryGet("indications", out JsonNode ind))
                profile.Indications = BindOptionalStringArray(ind, label + drugLabelSuffix + ".indications", pkg);
            if (drug.TryGet("contraindications", out JsonNode contra))
                profile.Contraindications = BindOptionalStringArray(contra, label + drugLabelSuffix + ".contraindications", pkg);

            if (drug.TryGet("dose_range", out JsonNode dose))
            {
                if (dose.Kind == JsonNodeKind.Null) { /* 可空 */ }
                else if (dose.Kind == JsonNodeKind.Object)
                {
                    int min = BindIntInObject(dose, "min", label + drugLabelSuffix + ".dose_range", pkg);
                    int max = BindIntInObject(dose, "max", label + drugLabelSuffix + ".dose_range", pkg);
                    var allowedDose = new HashSet<string>(StringComparer.Ordinal) { "min", "max" };
                    AddUnknown(dose, allowedDose, label + drugLabelSuffix + ".dose_range", pkg);
                    AddMissingRequired(dose, new[] { "min", "max" }, label + drugLabelSuffix + ".dose_range", pkg);
                    profile.DoseRange = new DoseRange(min, max);
                }
                else pkg.Errors.Add(label + drugLabelSuffix + ":\"dose_range\" 须为对象或 null");
            }

            profile.DrugPotency = BindOptionalFix(drug, "drug_potency", label + drugLabelSuffix, pkg);
            profile.Onset = BindOptionalFix(drug, "onset", label + drugLabelSuffix, pkg);
            profile.Peak = BindOptionalFix(drug, "peak", label + drugLabelSuffix, pkg);
            profile.HalfLife = BindOptionalFix(drug, "half_life", label + drugLabelSuffix, pkg);
            profile.Elimination = BindOptionalFix(drug, "elimination", label + drugLabelSuffix, pkg);

            if (drug.TryGet("quality_axis", out JsonNode axis))
            {
                if (axis.Kind == JsonNodeKind.Null) { /* 可空 */ }
                else if (axis.Kind == JsonNodeKind.String)
                {
                    // 轴字面量解析 = drug_profile 绑定(Story 008,QualityAxis.cs 注授权);P0 收窄归 AC-60 门。
                    if (TryParseQualityAxis(axis.Str, out QualityAxis parsedAxis))
                        profile.QualityAxis = parsedAxis;
                    else
                        pkg.Errors.Add($"{label}{drugLabelSuffix}:quality_axis 字面量 \"{axis.Str}\" ∉ 闭集 " +
                                       "half_life|onset|peak|elimination(AC-21a-22 口径)");
                }
                else pkg.Errors.Add($"{label}{drugLabelSuffix}:\"quality_axis\" 须为字符串或 null");
            }

            if (drug.TryGet("axis_offset_by_quality", out JsonNode offsets))
            {
                if (offsets.Kind == JsonNodeKind.Null) { /* 可空 */ }
                else if (offsets.Kind == JsonNodeKind.Array)
                {
                    var arr = new Fix[offsets.Items.Count];
                    bool ok = true;
                    for (int i = 0; i < offsets.Items.Count; i++)
                    {
                        JsonNode el = offsets.Items[i];
                        if (el.Kind != JsonNodeKind.String)
                        {
                            pkg.Errors.Add($"{label}{drugLabelSuffix}:axis_offset_by_quality[{i}] 须为 Fix 字符串" +
                                           "(ADR-014 §四);构建期硬失败(AC-21a-41)");
                            ok = false;
                            continue;
                        }
                        try { arr[i] = FixParse.Parse(el.Str); }
                        catch (FormatException ex)
                        {
                            pkg.Errors.Add($"{label}{drugLabelSuffix}:axis_offset_by_quality[{i}] FixParse 失败:{ex.Message}" +
                                           "(AC-21a-41)");
                            ok = false;
                        }
                    }
                    if (ok) profile.AxisOffsetByQuality = arr;
                }
                else pkg.Errors.Add($"{label}{drugLabelSuffix}:\"axis_offset_by_quality\" 须为数组或 null");
            }

            profile.DrugQualityCharacter = drug.TryGet("drug_quality_character", out JsonNode dqc)
                ? BindOptionalStringArray(dqc, label + drugLabelSuffix + ".drug_quality_character", pkg)
                : null;

            return profile;
        }

        private static GatherProfile BindGatherProfile(JsonNode gather, string label, BindPackage pkg)
        {
            const string suffix = ".gather_profile";
            var allowed = new HashSet<string>(GatherKeys, StringComparer.Ordinal);
            AddUnknown(gather, allowed, label + suffix, pkg);
            AddMissingRequired(gather, GatherRequiredKeys, label + suffix, pkg);

            var profile = new GatherProfile();
            if (gather.TryGet("ecosystem", out JsonNode eco) && eco.Kind == JsonNodeKind.String)
                profile.Ecosystem = eco.Str;
            else if (gather.TryGet("ecosystem", out _))
                pkg.Errors.Add($"{label}{suffix}:\"ecosystem\" 须为 JSON 字符串");

            if (gather.TryGet("parts", out JsonNode parts))
            {
                if (TryBindStringArray(parts, label + suffix + ".parts", pkg, out string[] partArr) && partArr != null)
                    profile.Parts = partArr;
                else pkg.Errors.Add($"{label}{suffix}:\"parts\" 须为字符串数组");
            }

            profile.QtyPerNode = gather.TryGet("qty_per_node", out JsonNode qpn) && qpn.Kind == JsonNodeKind.Integer
                ? (int)qpn.Int
                : 0;
            if (gather.TryGet("qty_per_node", out JsonNode qpnChk) && qpnChk.Kind != JsonNodeKind.Integer)
                pkg.Errors.Add($"{label}{suffix}:\"qty_per_node\" 须为 JSON 整数 token(D-21-17 计数非 Fix)");

            if (gather.TryGet("quality_character", out JsonNode qc))
                profile.QualityCharacter = BindOptionalStringArray(qc, label + suffix + ".quality_character", pkg);

            if (gather.TryGet("quality_distribution", out JsonNode qd))
            {
                if (qd.Kind != JsonNodeKind.Object)
                    pkg.Errors.Add($"{label}{suffix}:\"quality_distribution\" 须为对象(形状归 17)");
                else
                {
                    // 形状由 17 定 —— P0 不得携带任何字段(GDD §Schema C「本节之外出现的字段一律视为未定义」)。
                    foreach (string key in qd.Keys)
                        pkg.Errors.Add($"{label}{suffix}.quality_distribution:未登记字段 \"{key}\" —— " +
                                       "形状归系统 17 的 GDD,P0 不得携带(AC-21a-14 判据归 17)");
                }
            }

            return profile;
        }

        // ══════════ 配方表 ══════════

        private static void BindRecipes(JsonNode root, BindPackage pkg)
        {
            const string fileLabel = "item_database_recipes.json";
            CheckSchemaAndUnknownTop(root, TopRecipesKeys, fileLabel, pkg);

            if (!root.TryGet("recipes", out JsonNode arr) || arr.Kind != JsonNodeKind.Array)
            {
                pkg.Errors.Add($"{fileLabel}:缺失必填键 \"recipes\" 或其非数组");
                return;
            }

            for (int i = 0; i < arr.Items.Count; i++)
            {
                JsonNode rec = arr.Items[i];
                string label = $"{fileLabel}:recipes[{i}]";
                if (rec.Kind != JsonNodeKind.Object)
                {
                    pkg.Errors.Add($"{label}:非对象");
                    pkg.RecipeRawOwners.Add(null);
                    continue;
                }
                pkg.RecipeRawOwners.Add(BindRecipeRecord(rec, label, pkg));
            }
        }

        private static string BindRecipeRecord(JsonNode rec, string label, BindPackage pkg)
        {
            var allowed = new HashSet<string>(RecipeRecordKeys, StringComparer.Ordinal);
            AddUnknown(rec, allowed, label, pkg);
            AddMissingRequired(rec, RecipeRequiredKeys, label, pkg);

            var recipe = new Recipe();

            if (rec.TryGet("recipe_id", out JsonNode rid) && rid.Kind == JsonNodeKind.String)
                recipe.RecipeId = rid.Str;
            else if (rec.TryGet("recipe_id", out _))
                pkg.Errors.Add($"{label}:\"recipe_id\" 须为 JSON 字符串");

            string rawOwner = null;
            if (rec.TryGet("owner", out JsonNode owner))
            {
                if (owner.Kind == JsonNodeKind.Object || owner.Kind == JsonNodeKind.Array)
                    pkg.Errors.Add($"{label}:\"owner\" 须为标量字面量 —— 缺/null/类型错 = 装载硬失败(AC-21a-66)");
                else
                {
                    rawOwner = owner.Kind == JsonNodeKind.Null ? null : owner.RawText;
                    if (rawOwner != null && ItemDbValidation.TryParseRecipeOwner(rawOwner, out RecipeOwner parsedOwner))
                        recipe.Owner = parsedOwner;
                }
            }

            recipe.Inputs = BindRecipeEntries(rec, "inputs", "AC-21a-16", label, pkg);
            recipe.Outputs = BindRecipeEntries(rec, "outputs", "AC-21a-7", label, pkg);

            recipe.DurationTicks = BindRequiredInt(rec, "duration_ticks", label, pkg, "工时须为正整数 tick(D-21-17);构建期硬失败(AC-21a-18)");
            recipe.SkillGate = BindRequiredInt(rec, "skill_gate", label, pkg, "技能门槛须为整数;构建期硬失败(AC-21a-19)");
            recipe.MinQuality = BindRequiredInt(rec, "min_quality", label, pkg, "品级下限须为整数;构建期硬失败(AC-21a-20)");

            if (rec.TryGet("boundary_state", out JsonNode boundary))
            {
                if (boundary.Kind != JsonNodeKind.Array)
                {
                    pkg.Errors.Add($"{label}:\"boundary_state\" 须为数组(明文 from>to)");
                }
                else
                {
                    var pairs = new List<ProcessingTransition>(boundary.Items.Count);
                    bool ok = true;
                    for (int i = 0; i < boundary.Items.Count; i++)
                    {
                        JsonNode el = boundary.Items[i];
                        if (el.Kind != JsonNodeKind.String || !TryParseBoundary(el.Str, out ProcessingTransition tr))
                        {
                            pkg.Errors.Add($"{label}:boundary_state[{i}] = {(el.Kind == JsonNodeKind.String ? el.Str : el.Kind.ToString())} " +
                                           "无法解析 —— 明文 from>to(或 →),两侧须 ∈ P0 闭集;构建期硬失败(AC-21a-24)");
                            ok = false;
                            continue;
                        }
                        pairs.Add(tr);
                    }
                    if (ok) recipe.BoundaryState = pairs.ToArray();
                }
            }

            pkg.Recipes.Add(new BoundRecipe
            {
                Rec = recipe,
                RawOwner = rawOwner,
                Label = recipe.RecipeId ?? label,
            });
            return rawOwner;
        }

        private static RecipeEntry[] BindRecipeEntries(JsonNode rec, string key, string acHint, string label, BindPackage pkg)
        {
            if (!rec.TryGet(key, out JsonNode arr))
                return null;
            if (arr.Kind != JsonNodeKind.Array)
            {
                pkg.Errors.Add($"{label}:\"{key}\" 须为数组");
                return null;
            }

            var entries = new RecipeEntry[arr.Items.Count];
            for (int i = 0; i < arr.Items.Count; i++)
            {
                JsonNode entry = arr.Items[i];
                string entryLabel = $"{label}.{key}[{i}]";
                if (entry.Kind != JsonNodeKind.Object)
                {
                    pkg.Errors.Add($"{entryLabel}:须为对象 {{item_key, qty}}");
                    continue;
                }
                var allowed = new HashSet<string>(StringComparer.Ordinal) { "item_key", "qty" };
                AddUnknown(entry, allowed, entryLabel, pkg);
                AddMissingRequired(entry, new[] { "item_key", "qty" }, entryLabel, pkg);

                ItemKey itemKey = default;
                if (entry.TryGet("item_key", out JsonNode ik))
                {
                    if (ik.Kind != JsonNodeKind.Object)
                    {
                        pkg.Errors.Add($"{entryLabel}:\"item_key\" 须为对象 {{base_id, processing_state}}");
                    }
                    else
                    {
                        var allowedIk = new HashSet<string>(StringComparer.Ordinal) { "base_id", "processing_state" };
                        AddUnknown(ik, allowedIk, entryLabel + ".item_key", pkg);
                        AddMissingRequired(ik, new[] { "base_id", "processing_state" }, entryLabel + ".item_key", pkg);

                        string ikBase = ik.TryGet("base_id", out JsonNode ikb) && ikb.Kind == JsonNodeKind.String ? ikb.Str : null;
                        ProcessingState ikState = default;
                        if (ik.TryGet("processing_state", out JsonNode iks))
                        {
                            if (iks.Kind == JsonNodeKind.Integer || iks.Kind == JsonNodeKind.Float)
                            {
                                pkg.Errors.Add($"{entryLabel}.item_key:processing_state 以 int 编码(token={iks.Kind}) —— " +
                                               "item_key 字段禁 int 编码;装配期硬失败(AC-21a-26 · D-21-13)");
                            }
                            else if (iks.Kind == JsonNodeKind.String)
                            {
                                if (!ItemDbValidation.TryParseProcessingState(iks.Str, out ikState))
                                    pkg.Errors.Add($"{entryLabel}.item_key:processing_state 字面量 \"{iks.Str}\" ∉ P0 闭集" +
                                                   "(AC-21a-22 口径)");
                            }
                            else
                            {
                                pkg.Errors.Add($"{entryLabel}.item_key:\"processing_state\" 须为字符串字面量(AC-21a-22)");
                            }
                        }
                        itemKey = new ItemKey(ikBase, ikState);
                    }
                }

                int qty = 0;
                if (entry.TryGet("qty", out JsonNode q))
                {
                    if (q.Kind == JsonNodeKind.Integer && q.Int >= int.MinValue && q.Int <= int.MaxValue)
                        qty = (int)q.Int;
                    else
                        pkg.Errors.Add($"{entryLabel}:\"qty\" 须为 JSON 整数 token(基数,非实耗 —— D-21-15);" +
                                       "类型错由绑定层拒;构建期硬失败({acHint})".Replace("{acHint}", acHint));
                }

                entries[i] = new RecipeEntry(itemKey, qty);
            }
            return entries;
        }

        // ══════════ 共用绑定小件 ══════════

        private static int BindRequiredInt(JsonNode parent, string key, string label, BindPackage pkg, string acHint)
        {
            if (!parent.TryGet(key, out JsonNode n))
                return 0; // 缺失已在 required 检查登记
            if (n.Kind != JsonNodeKind.Integer)
            {
                pkg.Errors.Add($"{label}:\"{key}\" 须为 JSON 整数 token —— {acHint}");
                return 0;
            }
            if (n.Int < int.MinValue || n.Int > int.MaxValue)
            {
                pkg.Errors.Add($"{label}:\"{key}\" = {n.Int} 超出 int 域");
                return 0;
            }
            return (int)n.Int;
        }

        private static int BindIntInObject(JsonNode obj, string key, string label, BindPackage pkg)
        {
            if (!obj.TryGet(key, out JsonNode n))
                return 0;
            if (n.Kind != JsonNodeKind.Integer)
            {
                pkg.Errors.Add($"{label}:\"{key}\" 须为 JSON 整数 token(D-21-17 计数非 Fix)");
                return 0;
            }
            return (int)n.Int;
        }

        /// <summary>带 raw 回传的 int 绑定:Integer 取值;String/Boolean 结构放行(raw 交 ValidateStackWeight 门判);
        /// Float/Null/复合 token 结构性拒收(AC-21a-15)。</summary>
        private static int BindGatedInt(JsonNode parent, string key, string label, BindPackage pkg, string acHint, out string rawText)
        {
            rawText = null;
            if (!parent.TryGet(key, out JsonNode n))
                return 0;

            switch (n.Kind)
            {
                case JsonNodeKind.Integer:
                    rawText = n.RawText;
                    if (n.Int < int.MinValue || n.Int > int.MaxValue)
                    {
                        pkg.Errors.Add($"{label}:\"{key}\" = {n.Int} 超出 int 域 —— {acHint}");
                        return 0;
                    }
                    return (int)n.Int;
                case JsonNodeKind.String:
                    rawText = n.Str;
                    return int.TryParse(n.Str, System.Globalization.NumberStyles.Integer,
                        System.Globalization.CultureInfo.InvariantCulture, out int parsed) ? parsed : 0;
                case JsonNodeKind.Boolean:
                    rawText = n.RawText; // "true"/"false" —— 门以非法整数拒
                    return 0;
                default:
                    pkg.Errors.Add($"{label}:\"{key}\" token={n.Kind} —— 须为 JSON 整数(非 Fix、非浮点;" +
                                   $"D-21-17);{acHint}");
                    return 0;
            }
        }

        private static Fix? BindRequiredFix(JsonNode parent, string key, string label, BindPackage pkg)
        {
            if (!parent.TryGet(key, out JsonNode n))
                return null; // 缺失已在 required 检查登记
            if (n.Kind == JsonNodeKind.Null)
            {
                pkg.Errors.Add($"{label}:Fix 字段 \"{key}\" 为 null —— 必填(ADR-014 §四)");
                return null;
            }
            if (n.Kind != JsonNodeKind.String)
            {
                pkg.Errors.Add($"{label}:Fix 字段 \"{key}\" 须为 JSON 字符串(token={n.Kind}) —— " +
                               "禁经数值 token 中转(ADR-014 §四 · ADR-006)");
                return null;
            }
            try
            {
                return FixParse.Parse(n.Str);
            }
            catch (FormatException ex)
            {
                pkg.Errors.Add($"{label}:Fix 字段 \"{key}\" FixParse 失败:{ex.Message}(AC-21a-41)");
                return null;
            }
        }

        private static Fix? BindOptionalFix(JsonNode parent, string key, string label, BindPackage pkg)
        {
            if (!parent.TryGet(key, out JsonNode n))
                return null;
            if (n.Kind == JsonNodeKind.Null)
                return null;
            if (n.Kind != JsonNodeKind.String)
            {
                pkg.Errors.Add($"{label}:Fix 字段 \"{key}\" 须为 JSON 字符串(token={n.Kind}) —— " +
                               "禁经数值 token 中转(ADR-014 §四);构建期硬失败(AC-21a-41)");
                return null;
            }
            try
            {
                return FixParse.Parse(n.Str);
            }
            catch (FormatException ex)
            {
                pkg.Errors.Add($"{label}:Fix 字段 \"{key}\" FixParse 失败:{ex.Message}(AC-21a-41)");
                return null;
            }
        }

        private static string[] BindOptionalStringArray(JsonNode n, string label, BindPackage pkg)
        {
            if (n.Kind == JsonNodeKind.Null)
                return null;
            if (!TryBindStringArray(n, label, pkg, out string[] result))
                return null;
            return result;
        }

        private static bool TryBindStringArray(JsonNode n, string label, BindPackage pkg, out string[] result)
        {
            result = null;
            if (n.Kind != JsonNodeKind.Array)
                return false;
            var arr = new string[n.Items.Count];
            for (int i = 0; i < n.Items.Count; i++)
            {
                if (n.Items[i].Kind != JsonNodeKind.String)
                {
                    pkg.Errors.Add($"{label}[{i}]:须为 JSON 字符串元素");
                    return false;
                }
                arr[i] = n.Items[i].Str;
            }
            result = arr;
            return true;
        }

        /// <summary>tcm_profile 块体(不含外层花括号)—— 供 ValidateP0Narrowing 正则判非 null 值。</summary>
        private static string BuildTcmBody(JsonNode tcm)
        {
            if (tcm.Props == null || tcm.Props.Count == 0)
                return string.Empty;
            var parts = new List<string>(tcm.Props.Count);
            foreach (KeyValuePair<string, JsonNode> p in tcm.Props)
            {
                string valueText;
                switch (p.Value.Kind)
                {
                    case JsonNodeKind.Null: valueText = "null"; break;
                    case JsonNodeKind.String: valueText = "\"" + p.Value.Str + "\""; break;
                    case JsonNodeKind.Object: valueText = "{"; break; // 非 null 标记
                    case JsonNodeKind.Array: valueText = "["; break;
                    default: valueText = p.Value.RawText; break;
                }
                parts.Add($"\"{p.Key}\":{valueText}");
            }
            return string.Join(",", parts);
        }

        /// <summary>quality_axis 字面量解析(轴解析 = Story 008 绑定职责,QualityAxis.cs 注授权)。</summary>
        private static bool TryParseQualityAxis(string literal, out QualityAxis axis)
        {
            switch (literal)
            {
                case "half_life": axis = QualityAxis.HalfLife; return true;
                case "onset": axis = QualityAxis.Onset; return true;
                case "peak": axis = QualityAxis.Peak; return true;
                case "elimination": axis = QualityAxis.Elimination; return true;
                default: axis = default; return false;
            }
        }

        /// <summary>boundary_state 明文对解析('>' / '→' 两种箭头;镜像 ItemValidationGates.ParseDeclaredTransitions 口径)。</summary>
        private static bool TryParseBoundary(string raw, out ProcessingTransition transition)
        {
            transition = default;
            if (string.IsNullOrEmpty(raw))
                return false;
            int arrow = raw.IndexOf('>');
            if (arrow < 0) arrow = raw.IndexOf('→'); // U+2192
            if (arrow < 0) return false;
            string fromPart = raw.Substring(0, arrow).Trim();
            string toPart = raw.Substring(arrow + 1).Trim();
            if (!ItemDbValidation.TryParseProcessingState(fromPart, out ProcessingState from))
                return false;
            if (!ItemDbValidation.TryParseProcessingState(toPart, out ProcessingState to))
                return false;
            transition = new ProcessingTransition(from, to);
            return true;
        }
    }
}
