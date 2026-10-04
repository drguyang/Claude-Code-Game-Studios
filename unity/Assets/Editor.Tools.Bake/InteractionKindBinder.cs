// 权威来源:ADR-014 §三(阶段 2:per-schema 绑定 + 白名单/区间/闭集校验 + schema_version 版本化)
//          · GDD design/gdd/interaction-system.md §数据契约(4-DC-1…6)+ 规则一(路由表)
//          · Story 007(NR-1:校验器的**唯一调用点** —— 本件之前它是死代码)
//
// ⚠️ 本件是 story-006 F-1「死代码」判据的反面:删掉 Bind() 里的 Validate(...) 调用,
//    违例夹具会**静默烘出**产物 ⇒ 负夹具(只断言「烘焙期抛」,自己不判 4-DC)必须转红。
//
// ⚠️ 枚举一律**按明文字符串**读入并映射(ADR-014 §三 / 21a AC-21a-26 先例「禁 int 编码的 state」)。
//    `kind` 例外 —— GDD §数据契约表明写主键 `kind : int`(InteractableKind 枚举序数),
//    按 int 读,但绑定后仍走 4-DC-2 闭集校验(序数超界 / 缺项即红)。
//
// ⚠️ 全部错误**聚合**后一次抛出(与 ItemDatabaseBaker 同纪律):不首个错即停,
//    让一份表的所有问题一次可见(BakeValidationException.Errors)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Interaction;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>`interaction_kinds.json`(+维度表)的阶段 2 绑定器:**唯一**调用
    /// <see cref="InteractionKindTableValidator"/> 的地方。</summary>
    internal static class InteractionKindBinder
    {
        // ── 已知键白名单(ADR-014 §三:未知键 = 烘焙期硬失败,防拼写错误静默丢字段)──

        private static readonly string[] RootKeys = { "schema_version", "r_interact", "kinds" };
        private static readonly string[] KindRowKeys =
        {
            "kind", "kind_priority", "stable_id_source", "routes_to",
            "suppresses_motor", "duration_owner", "duration_owner_system_id", "intent_uplink",
            "world_w", "world_h", "world_d",   // 可选:仅 SlotLinearKey 行(P4-4 ② 的行内载体)
        };
        private static readonly string[] DimensionKeys = { "schema_version", "world_w", "world_h", "world_d", "r_interact" };

        /// <summary>绑定结果(行集 + 校验通过后的源文本对,供 ConfigVersion 派生)。</summary>
        public readonly struct BindResult
        {
            public readonly List<KindContractRow> Rows;
            public readonly int RInteract;
            public readonly int WorldW;
            public readonly int WorldH;
            public readonly int WorldD;
            public readonly uint SchemaVersion;

            public BindResult(List<KindContractRow> rows, int rInteract,
                              int worldW, int worldH, int worldD, uint schemaVersion)
            {
                Rows = rows; RInteract = rInteract;
                WorldW = worldW; WorldH = worldH; WorldD = worldD;
                SchemaVersion = schemaVersion;
            }
        }

        /// <summary>
        /// 绑定 + 校验 + 注入世界维度。任一违例 ⇒ 聚合 <see cref="BakeValidationException"/>。
        /// </summary>
        /// <param name="kindsJson">`interaction_kinds.json` 原文。</param>
        /// <param name="dimensionsJson">`interaction_kinds_dimensions.json` 原文(4-DC-4 ② 的 W/H/D + r_interact)。</param>
        public static BindResult Bind(string kindsJson, string dimensionsJson)
        {
            var errors = new List<string>();

            // ── 阶段 1:词法(复用仓库唯一入口,零 JsonConvert / JObject)──
            if (!JsonStage1Lexer.TryParse(kindsJson, out JsonNode kindsRoot, out string lexErr1))
            {
                errors.Add($"interaction_kinds.json 阶段1 词法失败:{lexErr1}");
                throw new BakeValidationException(errors);
            }
            if (!JsonStage1Lexer.TryParse(dimensionsJson, out JsonNode dimRoot, out string lexErr2))
            {
                errors.Add($"interaction_kinds_dimensions.json 阶段1 词法失败:{lexErr2}");
                throw new BakeValidationException(errors);
            }

            // ── 维度表(先读:r_interact 与 W/H/D 是 4-DC-1 的输入)──
            uint schemaVersion = 0;
            int worldW = 0, worldH = 0, worldD = 0, rInteract = 0;
            BindDimensions(dimRoot, errors, ref schemaVersion, ref worldW, ref worldH, ref worldD, ref rInteract);

            // ── 根对象白名单 + 行集 ──
            var rows = new List<KindContractRow>();
            if (kindsRoot.Kind != JsonNodeKind.Object)
            {
                errors.Add($"interaction_kinds.json 根须为对象(实得 {kindsRoot.Kind})");
            }
            else
            {
                RejectUnknownKeys(kindsRoot, RootKeys, "interaction_kinds.json 根", errors);

                // schema_version(两文件须一致 —— 版本不匹配 = 硬失败,ADR-014 §三)
                if (kindsRoot.TryGet("schema_version", out JsonNode sv))
                {
                    if (sv.Kind != JsonNodeKind.Integer)
                        errors.Add("interaction_kinds.json schema_version 须为整数");
                    else if (schemaVersion != 0 && (uint)sv.Int != schemaVersion)
                        errors.Add($"schema_version 不一致:kinds={sv.Int}, dimensions={schemaVersion}");
                }
                else
                {
                    errors.Add("interaction_kinds.json 缺 schema_version");
                }

                // ⚠️ r_interact 也出现在根里(GDD §数据契约表「半径 = 内联于 interaction_kinds.json」)——
                //    若两处都有且不等,取维度表值并记错(单源纪律:一个值只能有一个真源)。
                if (kindsRoot.TryGet("r_interact", out JsonNode rInRoot))
                {
                    if (rInRoot.Kind != JsonNodeKind.Integer)
                        errors.Add("interaction_kinds.json r_interact 须为整数(非 float/字符串)");
                    else if ((int)rInRoot.Int != rInteract)
                        errors.Add($"r_interact 两处不等:根={rInRoot.Int}, 维度表={rInteract} —— 单源纪律(AC-4-17)");
                }

                if (!kindsRoot.TryGet("kinds", out JsonNode kindsArr))
                    errors.Add("interaction_kinds.json 缺 kinds 数组");
                else if (kindsArr.Kind != JsonNodeKind.Array)
                    errors.Add($"interaction_kinds.json kinds 须为数组(实得 {kindsArr.Kind})");
                else
                    BindRows(kindsArr, worldW, worldH, worldD, rows, errors);
            }

            // ── ★ 校验器调用点(本故事承重面;story-006 F-1 的「死代码」由此消解)──
            //    仅当绑定本身无错时跑 —— 否则断言的是半成型行集,错误会重复且难读。
            if (errors.Count == 0)
            {
                try
                {
                    InteractionKindTableValidator.Validate(rows, rInteract, worldW, worldH, worldD);
                }
                catch (Exception ex)
                {
                    errors.Add("4-DC 校验:" + ex.Message);
                }
            }

            if (errors.Count > 0)
                throw new BakeValidationException(errors);

            return new BindResult(rows, rInteract, worldW, worldH, worldD, schemaVersion);
        }

        // ── 维度表 ──────────────────────────────────────────────────────────

        private static void BindDimensions(JsonNode root, List<string> errors,
            ref uint schemaVersion, ref int w, ref int h, ref int d, ref int rInteract)
        {
            if (root.Kind != JsonNodeKind.Object)
            {
                errors.Add($"interaction_kinds_dimensions.json 根须为对象(实得 {root.Kind})");
                return;
            }
            RejectUnknownKeys(root, DimensionKeys, "dimensions 根", errors);

            schemaVersion = ReadU32(root, "schema_version", errors);
            w = ReadInt(root, "world_w", errors);
            h = ReadInt(root, "world_h", errors);
            d = ReadInt(root, "world_d", errors);
            rInteract = ReadInt(root, "r_interact", errors);
        }

        // ── 行集 ────────────────────────────────────────────────────────────

        private static void BindRows(JsonNode arr, int w, int h, int d,
                                     List<KindContractRow> rows, List<string> errors)
        {
            for (int i = 0; i < arr.Items.Count; i++)
            {
                JsonNode node = arr.Items[i];
                string where = $"kinds[{i}]";
                if (node.Kind != JsonNodeKind.Object)
                {
                    errors.Add($"{where} 须为对象(实得 {node.Kind})");
                    continue;
                }
                RejectUnknownKeys(node, KindRowKeys, where, errors);

                InteractableKind kind = ReadKind(node, where, errors);
                int priority = ReadInt(node, "kind_priority", errors, where);
                StableIdSource source = ReadEnum<StableIdSource>(node, "stable_id_source", where, errors,
                    nameof(StableIdSource.InstanceId), nameof(StableIdSource.BakedResourceIndex),
                    nameof(StableIdSource.PatientId), nameof(StableIdSource.PoiId),
                    nameof(StableIdSource.SlotLinearKey), nameof(StableIdSource.StructureId));
                int routesTo = ReadInt(node, "routes_to", errors, where);
                bool suppresses = ReadBool(node, "suppresses_motor", errors, where);
                DurationOwnerKind durOwner = ReadEnum<DurationOwnerKind>(node, "duration_owner", where, errors,
                    nameof(DurationOwnerKind.None), nameof(DurationOwnerKind.RoutedSystem));
                int durOwnerId = ReadInt(node, "duration_owner_system_id", errors, where);
                IntentUplink uplink = ReadEnum<IntentUplink>(node, "intent_uplink", where, errors,
                    nameof(IntentUplink.None), nameof(IntentUplink.HostOnlyIntent));

                // ⚠️ W/H/D 只注入 **SlotLinearKey** 行 —— GDD §数据契约(`:1006`)把 W/H/D 列为
                //    `SlotLinearKey` 项的字段,4-DC-4 ② 的字面是「`SlotLinearKey` 项须同表登记 W/H/D」。
                //    其余行写 0:它们的 W/H/D **无意义** —— 若也填世界维度,则「未登记」与
                //    「登记为某值」在产物里不可区分(静默),且 4-DC-4 ② 在装载路径上**结构性不可达**
                //    (评审 F-2:注入全行 = 把一条有真值的检查加宽到恒真)。
                int rw = 0, rh = 0, rd = 0;
                if (source == StableIdSource.SlotLinearKey)
                {
                    // GDD §数据契约(`:1006`):W/H/D 是 **SlotLinearKey 行自己的字段**。
                    // 行内声明优先;缺省则回落到维度表(4-DC-4 ② 的输入可在**行**上被违反 ——
                    // 这是让该判据在装载路径上真正可达的唯一途径:维度表取 0 会被 4-DC-1 先拦)。
                    rw = node.TryGet("world_w", out JsonNode nw) ? ReadInt(node, "world_w", errors, where) : w;
                    rh = node.TryGet("world_h", out JsonNode nh) ? ReadInt(node, "world_h", errors, where) : h;
                    rd = node.TryGet("world_d", out JsonNode nd) ? ReadInt(node, "world_d", errors, where) : d;
                }

                rows.Add(new KindContractRow(kind, priority, source, routesTo, suppresses,
                                             durOwner, durOwnerId, rw, rh, rd, uplink));
            }
        }

        // ── 读件(严格:类型不符即记错,绝不隐式转换)────────────────────────

        /// <summary>`kind` 按 int 读(GDD 主键 = 枚举序数),超界由 4-DC-2 闭集校验兜底。</summary>
        private static InteractableKind ReadKind(JsonNode node, string where, List<string> errors)
        {
            if (!node.TryGet("kind", out JsonNode n))
            {
                errors.Add($"{where} 缺 kind");
                return default;
            }
            if (n.Kind != JsonNodeKind.Integer)
            {
                errors.Add($"{where}.kind 须为整数(枚举序数,实得 {n.Kind})");
                return default;
            }
            return (InteractableKind)n.Int;
        }

        private static int ReadInt(JsonNode obj, string key, List<string> errors, string where = null)
        {
            string label = where == null ? key : $"{where}.{key}";
            if (!obj.TryGet(key, out JsonNode n))
            {
                errors.Add($"{label} 缺失");
                return 0;
            }
            if (n.Kind != JsonNodeKind.Integer)
            {
                // ⚠️ 结构化拒收:Float token 在此即硬失败(不靠数值转换;见 Implementation Notes ②)。
                errors.Add($"{label} 须为整数(实得 {n.Kind};禁 float/字符串)");
                return 0;
            }
            if (n.Int < int.MinValue || n.Int > int.MaxValue)
            {
                errors.Add($"{label}={n.Int} 超出 int 域");
                return 0;
            }
            return (int)n.Int;
        }

        private static uint ReadU32(JsonNode obj, string key, List<string> errors)
        {
            if (!obj.TryGet(key, out JsonNode n))
            {
                errors.Add($"{key} 缺失");
                return 0;
            }
            if (n.Kind != JsonNodeKind.Integer || n.Int < 0 || n.Int > uint.MaxValue)
            {
                errors.Add($"{key} 须为 u32 域整数(实得 {n.Kind}={n.RawText})");
                return 0;
            }
            return (uint)n.Int;
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

        /// <summary>枚举按**明文字符串**读入并映射(ADR-014 §三:禁 int 编码的 state)。</summary>
        private static TEnum ReadEnum<TEnum>(JsonNode obj, string key, string where,
                                             List<string> errors, params string[] allowed)
            where TEnum : struct
        {
            string label = $"{where}.{key}";
            if (!obj.TryGet(key, out JsonNode n))
            {
                errors.Add($"{label} 缺失");
                return default;
            }
            if (n.Kind != JsonNodeKind.String)
            {
                errors.Add($"{label} 须为明文字符串枚举(实得 {n.Kind};禁 int 编码)");
                return default;
            }
            for (int i = 0; i < allowed.Length; i++)
            {
                if (string.Equals(allowed[i], n.Str, StringComparison.Ordinal))
                    return (TEnum)Enum.Parse(typeof(TEnum), n.Str);
            }
            errors.Add($"{label}=\"{n.Str}\" ∉ {{{string.Join(", ", allowed)}}}");
            return default;
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
