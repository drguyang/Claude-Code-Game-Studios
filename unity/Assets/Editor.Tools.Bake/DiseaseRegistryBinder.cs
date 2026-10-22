// 权威来源:ADR-014 §三(阶段 2:per-schema 绑定 + 白名单/闭集/区间校验 + schema_version 版本化)
//          · GDD design/gdd/disease-simulation.md §R1 / §R1.3 / §R2 / §R3
//          · `assets/data/disease_registry.json`(作者态)
//
// ⚠️ 本件是 9 的注册表**阶段 2 绑定器** —— 与 `DiseaseActionAxisBinder` 同构。
//    在此之前 `RegistrySchemaValidator` 的唯一调用点是**测试**
//    (`curve_fixture_test` / `registry_bake_test` / `disease_registry_data_test`),
//    生产装载路径(`CompositionRoot.CreateFallbackRegistry`)不调它 ⇒ 校验器对真数据面
//    是**死代码**。本件把它接进装载路径(承「零调用方的门形同虚设」口径)。
//
// ⚠️ **Fix 字段在 JSON 里写字符串**(ADR-014 §四 硬规则 1):`"a_peak": "100"` 而非 `100`。
//    数字 token 会经 double/decimal 中转 = 浮点进内存,绕 `FixParse`。本件对 Fix 字段
//    **只接受 String**,收到 Integer 直接记错(不是「宽容地转」—— 宽容会让浮点泄漏静默通过)。
//
// ⚠️ 全部错误**聚合**后一次抛出(与 ItemDatabaseBaker / DiseaseActionAxisBinder 同纪律)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>`disease_registry.json` 的阶段 2 绑定器:**唯一**把真数据喂给
    /// <see cref="RegistrySchemaValidator"/> 的地方。</summary>
    public static class DiseaseRegistryBinder
    {
        // ── 已知键白名单(ADR-014 §三:未知键 = 烘焙期硬失败,防拼写错误静默丢字段)──
        //
        // ⚠️ 以 `_` 开头的键一律视为**作者态注记**(`_note` / `_rowNote` / `_source` …)——
        //    它们不参与绑定,但**必须**允许存在:它们是作者留给下一个读表人的话。
        //    这不是「宽松」:注记键仍走 RejectUnknownKeys 的 `_` 前缀规则,拼错成
        //    `note`(无下划线)会被当成未知键硬失败。
        private static readonly string[] RootKeys =
            { "schema_version", "diseases" };

        private static readonly string[] DiseaseRowKeys =
        {
            // ── 本件绑定的键 ──
            "id", "key", "kind", "polarity", "severity", "contagion",
            "lethal", "treatment_difficulty", "recovery_time", "relapse_chance",
            "comorbidity_factor", "seasonal_mod", "age_mod", "gender_mod",
            "occupation_mod", "region_mod", "climate_mod",
            "scale", "trauma_half_life", "natural_progress",
            // ── 声明合法但本件**不**绑定的键(见 BoundRowKeys 上方注)──
            "zh", "causes", "treatable_by", "handle", "signs",
        };

        /// <summary>
        /// 白名单内**真正被绑定进 <see cref="DiseaseRegistryEntry"/>** 的键。
        /// <para>⚠️ 本表必须与 <see cref="BindRow"/> 里实际发生的读取**逐键对应**。
        /// 它与 <see cref="DiseaseRowKeys"/> 的差集 = 允许声明但会被丢弃的键,
        /// 由 <see cref="Bind"/> 报进 <see cref="BindResult.UnboundKeys"/>。</para>
        /// <para>为什么要单列:ADR-014 §三 的未知键门只挡「拼错的键」。
        /// 一个**拼对的、白名单内的、却没人读的**键,丢字段是静默的 ——
        /// 门看不见它。本表让那种丢失变成一个可断言的量。</para>
        /// </summary>
        private static readonly string[] BoundRowKeys =
        {
            "id", "key", "kind", "polarity", "severity", "contagion",
            "lethal", "treatment_difficulty", "recovery_time", "relapse_chance",
            "comorbidity_factor", "seasonal_mod", "age_mod", "gender_mod",
            "occupation_mod", "region_mod", "climate_mod",
            "scale", "trauma_half_life", "natural_progress",
        };

        private static readonly string[] CurveKeys =
        {
            "incubation", "a_peak", "tau_rise", "tau_peak", "tau_fall",
            "plateau", "self_limit", "sigma", "boundary_mode",
        };

        /// <summary>绑定结果(行集 + schema_version)。</summary>
        public readonly struct BindResult
        {
            /// <summary>绑定后的注册表行(可直接交 <see cref="RegistrySchemaValidator.Validate"/>)。</summary>
            public readonly List<DiseaseRegistryEntry> Rows;

            /// <summary>源文件声明的 schema_version。</summary>
            public readonly uint SchemaVersion;

            /// <summary>
            /// **声明了、但本件没有绑定进 <see cref="DiseaseRegistryEntry"/> 的行键**(去重,Ordinal 排序)。
            /// <para>⚠️ 存在的理由:ADR-014 §三 的未知键门防的是「拼错 ⇒ 静默丢字段」,
            /// 但白名单内的键若**不被绑定**,丢字段就换个方式静默发生 —— 门看不见,
            /// 因为键是合法的。本字段把这种丢失**显式化**,由测试断言其恰等于
            /// <c>KnownUnboundRowKeys</c>:新增一个未绑定的键 ⇒ 断言红。</para>
            /// <para>空集 = 源文件声明的每个键都被绑定了(理想态,当前未达到)。</para>
            /// </summary>
            public readonly List<string> UnboundKeys;

            public BindResult(List<DiseaseRegistryEntry> rows, uint schemaVersion, List<string> unboundKeys)
            {
                Rows = rows; SchemaVersion = schemaVersion; UnboundKeys = unboundKeys;
            }
        }

        /// <summary>绑定 + 校验。任一违例 ⇒ 聚合 <see cref="BakeValidationException"/>。</summary>
        /// <param name="json">`disease_registry.json` 原文。</param>
        public static BindResult Bind(string json)
        {
            var errors = new List<string>();

            // ── 阶段 1:词法(复用仓库唯一入口,零 JsonConvert / JObject)──
            if (!JsonStage1Lexer.TryParse(json, out JsonNode root, out string lexErr))
            {
                errors.Add($"disease_registry.json 阶段1 词法失败:{lexErr}");
                throw new BakeValidationException(errors);
            }

            if (root.Kind != JsonNodeKind.Object)
            {
                errors.Add($"disease_registry.json 根须为对象(实得 {root.Kind})");
                throw new BakeValidationException(errors);
            }
            RejectUnknownKeys(root, RootKeys, "disease_registry.json 根", errors);

            uint schemaVersion = ReadSchemaVersion(root, errors);

            var rows = new List<DiseaseRegistryEntry>();
            var unbound = new SortedSet<string>(StringComparer.Ordinal);
            if (!root.TryGet("diseases", out JsonNode arr))
            {
                errors.Add("disease_registry.json 缺 diseases 数组");
            }
            else if (arr.Kind != JsonNodeKind.Array)
            {
                errors.Add($"disease_registry.json diseases 须为数组(实得 {arr.Kind})");
            }
            else
            {
                for (int i = 0; i < arr.Items.Count; i++)
                {
                    DiseaseRegistryEntry e = BindRow(arr.Items[i], $"diseases[{i}]", errors, unbound);
                    if (e != null) rows.Add(e);
                }
            }

            // ── ★ 校验器调用点(本件承重面)──
            //    仅当绑定本身无错时跑 —— 否则断言的是半成型行集,错误会重复且难读。
            if (errors.Count == 0)
            {
                try
                {
                    RegistrySchemaValidator.Validate(rows);
                }
                catch (RegistryValidationException ex)
                {
                    errors.Add("R1 校验:" + ex.Message);
                }
            }

            if (errors.Count > 0)
                throw new BakeValidationException(errors);

            return new BindResult(rows, schemaVersion, new List<string>(unbound));
        }

        // ── 单行 ────────────────────────────────────────────────────────────

        private static DiseaseRegistryEntry BindRow(JsonNode node, string where, List<string> errors,
                                                    SortedSet<string> unbound)
        {
            if (node.Kind != JsonNodeKind.Object)
            {
                errors.Add($"{where} 须为对象(实得 {node.Kind})");
                return null;
            }
            RejectUnknownKeys(node, DiseaseRowKeys, where, errors);
            CollectUnboundKeys(node, unbound);

            var e = new DiseaseRegistryEntry
            {
                DiseaseId = ReadInt(node, "id", errors, where),
                DiseaseKey = ReadString(node, "key", errors, where),
                Kind = ReadKind(node, errors, where),
                Polarity = ReadInt(node, "polarity", errors, where, 2),  // 缺省 = 平(伤情无寒热)
                Severity = ReadInt(node, "severity", errors, where),
                Contagion = ReadInt(node, "contagion", errors, where, 0),
                Lethality = ReadBool(node, "lethal", errors, where) ? 2 : 0,
                TreatmentDifficulty = ReadInt(node, "treatment_difficulty", errors, where, 5),
                RecoveryTime = ReadInt(node, "recovery_time", errors, where, 1),
                RelapseChance = ReadInt(node, "relapse_chance", errors, where, 0),
                ComorbidityFactor = ReadInt(node, "comorbidity_factor", errors, where, 0),
                SeasonalMod = ReadInt(node, "seasonal_mod", errors, where, 0),
                AgeMod = ReadInt(node, "age_mod", errors, where, 0),
                GenderMod = ReadInt(node, "gender_mod", errors, where, 0),
                OccupationMod = ReadInt(node, "occupation_mod", errors, where, 0),
                RegionMod = ReadInt(node, "region_mod", errors, where, 0),
                ClimateMod = ReadInt(node, "climate_mod", errors, where, 0),
                Scale = ReadFix(node, "scale", errors, where),
                TraumaHalfLife = ReadInt(node, "trauma_half_life", errors, where, 0),
                // 以下三项刻意留 null:
                //   TreatableBy —— 9 GDD R1.3 定义它在病种条目内,但已拆到
                //     `disease_action_axis.json`(story-007)。本表不填;填了会撞 R1-19 的
                //     NotImplementedException(那条校验本身尚未实现)。
                //   Relapse —— 复发块;八病种里的疟疾需要,但本表当前只含伤情行。
                //   Signs —— 体征词表归 8,当前为空。
                TreatableBy = null,
                Relapse = null,
                Signs = null,
                // handle 是**派生字段**(GDD R1.3:写入期不填,构建期从 treatable_by 极性算)。
                // 本表不填,留 null。⚠️ 派生逻辑当前**不存在** —— 见文件尾 §未实现登记。
                Handle = null,
            };

            // natural_progress 子块
            if (node.TryGet("natural_progress", out JsonNode np))
                e.Curve = BindCurve(np, $"{where}.natural_progress", errors);
            else
                errors.Add($"{where}.natural_progress 缺失(R1.2 铁律:必须存在且可序列化)");

            return e;
        }

        private static NaturalProgressCurve BindCurve(JsonNode node, string where, List<string> errors)
        {
            if (node.Kind != JsonNodeKind.Object)
            {
                errors.Add($"{where} 须为对象(实得 {node.Kind})");
                return null;
            }
            RejectUnknownKeys(node, CurveKeys, where, errors);

            var c = new NaturalProgressCurve
            {
                Incubation = ReadInt(node, "incubation", errors, where),
                APeak = ReadFix(node, "a_peak", errors, where),
                TauRise = ReadFix(node, "tau_rise", errors, where),
                TauPeak = ReadInt(node, "tau_peak", errors, where),
                TauFall = ReadFix(node, "tau_fall", errors, where, Fix.Zero),
                Plateau = ReadBool(node, "plateau", errors, where),
                SelfLimit = ReadBool(node, "self_limit", errors, where),
                Sigma = node.TryGet("sigma", out JsonNode s) && s.Kind != JsonNodeKind.Null
                            ? ReadFix(node, "sigma", errors, where)
                            : (Fix?)null,
                BoundaryMode = ReadString(node, "boundary_mode", errors, where),
            };
            return c;
        }

        // ── 读件(严格:类型不符即记错,绝不隐式转换)────────────────────────

        private static uint ReadSchemaVersion(JsonNode obj, List<string> errors)
        {
            if (!obj.TryGet("schema_version", out JsonNode n))
            {
                errors.Add("disease_registry.json 缺 schema_version");
                return 0;
            }
            if (n.Kind != JsonNodeKind.Integer || n.Int < 0 || n.Int > uint.MaxValue)
            {
                errors.Add($"disease_registry.json schema_version 须为 u32 域整数(实得 {n.Kind}={n.RawText})");
                return 0;
            }
            return (uint)n.Int;
        }

        private static int ReadInt(JsonNode obj, string key, List<string> errors, string where, int? fallback = null)
        {
            string label = $"{where}.{key}";
            if (!obj.TryGet(key, out JsonNode n))
            {
                if (fallback.HasValue) return fallback.Value;
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

        private static bool ReadBool(JsonNode obj, string key, List<string> errors, string where)
        {
            string label = $"{where}.{key}";
            if (!obj.TryGet(key, out JsonNode n))
            {
                errors.Add($"{label} 缺失");
                return false;
            }
            if (n.Kind != JsonNodeKind.Boolean)
            {
                errors.Add($"{label} 须为布尔(实得 {n.Kind})");
                return false;
            }
            return n.Bool;
        }

        /// <summary>
        /// 读 `Fix` 字段 —— **只接受字符串**(ADR-014 §四 硬规则 1),经 <see cref="FixParse.Parse"/>。
        /// <para>⚠️ 收到 JSON **数字**直接记错,**不**宽容转换:数字 token 已经过
        /// double/decimal 中转,宽容就等于把浮点泄漏放进定点域(那正是本规则要防的)。</para>
        /// </summary>
        private static Fix ReadFix(JsonNode obj, string key, List<string> errors, string where, Fix? fallback = null)
        {
            string label = $"{where}.{key}";
            if (!obj.TryGet(key, out JsonNode n))
            {
                if (fallback.HasValue) return fallback.Value;
                errors.Add($"{label} 缺失");
                return Fix.Zero;
            }
            if (n.Kind == JsonNodeKind.Integer || n.Kind == JsonNodeKind.Float)
            {
                errors.Add($"{label} 须为**字符串**(如 \"3/4\"),实得 JSON 数字 —— " +
                           "数字 token 经 double/decimal 中转 = 浮点泄漏,绕 FixParse(ADR-014 §四)");
                return Fix.Zero;
            }
            if (n.Kind != JsonNodeKind.String)
            {
                errors.Add($"{label} 须为字符串(实得 {n.Kind})");
                return Fix.Zero;
            }
            try
            {
                return FixParse.Parse(n.Str);
            }
            catch (FormatException ex)
            {
                errors.Add($"{label} 无法解析为 Fix:\"{n.Str}\" —— {ex.Message}");
                return Fix.Zero;
            }
        }

        /// <summary>读 `kind` —— 明文字符串映射(ADR-014 §三:禁 int 编码的 state)。</summary>
        private static DiseaseKind ReadKind(JsonNode obj, List<string> errors, string where)
        {
            string raw = ReadString(obj, "kind", errors, where);
            if (raw == null) return DiseaseKind.Unspecified;
            switch (raw)
            {
                case "injury": return DiseaseKind.Injury;
                case "disease": return DiseaseKind.Disease;
                case "poison": return DiseaseKind.Poison;
                default:
                    errors.Add($"{where}.kind=\"{raw}\" ∉ {{injury, disease, poison}}(GDD R1 表 :285)");
                    return DiseaseKind.Unspecified;
            }
        }

        /// <summary>收集**声明了但未绑定**的行键(见 <see cref="BoundRowKeys"/> 上方注)。
        /// <para>`_` 前缀注记不计入 —— 它们按设计就不参与绑定。</para></summary>
        private static void CollectUnboundKeys(JsonNode obj, SortedSet<string> unbound)
        {
            foreach (string key in obj.Keys)
            {
                if (key.Length > 0 && key[0] == '_') continue;
                bool bound = false;
                for (int i = 0; i < BoundRowKeys.Length; i++)
                    if (string.Equals(BoundRowKeys[i], key, StringComparison.Ordinal)) { bound = true; break; }
                if (!bound) unbound.Add(key);
            }
        }

        /// <summary>未知键 = 硬失败(ADR-014 §三,防拼写错误静默丢字段)。
        /// <para>⚠️ `_` 前缀键是作者态注记,一律放行(见 <c>RootKeys</c> 上方注)。</para></summary>
        private static void RejectUnknownKeys(JsonNode obj, string[] allowed, string where, List<string> errors)
        {
            foreach (string key in obj.Keys)
            {
                if (key.Length > 0 && key[0] == '_') continue;   // 作者态注记
                bool ok = false;
                for (int i = 0; i < allowed.Length; i++)
                    if (string.Equals(allowed[i], key, StringComparison.Ordinal)) { ok = true; break; }
                if (!ok)
                    errors.Add($"{where} 未知键「{key}」(拼写错误会静默丢字段 ⇒ 构建期硬失败)");
            }
        }
    }
}

// ══════════════════════════════════════════════════════════════════════════
// §未实现登记(如实列出,禁借绿)
// ══════════════════════════════════════════════════════════════════════════
//   ① `handle` 派生逻辑**不存在** —— GDD R1.3 要求它是派生字段(causal ⇔ ∃对因处置;
//      否则 {伤寒,痢疾,心衰} ⇒ care;DIS_TETANUS ⇒ none;其余 ⇒ symptomatic_only),
//      但仓库里**没有任何代码**算它。本 binder 留 null,不编。
//   ② `treatable_by` 已拆到 `disease_action_axis.json`(story-007),本表不填 ⇒
//      `handle` 的派生输入**在本表内不可得**,须跨两个文件才能算。
//   ③ `signs[]` 归 8 的体征词表,当前为空集。
//   ④ `relapse` 块 —— 八病种里的疟疾需要,但本表当前只含伤情行,故未绑定。
//   ⑤ `sigma` —— 全局默认值不存在(数值轮),逐病种覆盖亦无。
