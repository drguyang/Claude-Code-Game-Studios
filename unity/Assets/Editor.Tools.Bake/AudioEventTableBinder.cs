// S14-6 · story-010 —— 音频事件表(assets/data/audio_events.json)的**阶段 2 绑定器**。
//
// 权威来源:
//   ADR-014 §三(阶段 2 per-schema 绑定:未知键白名单硬失败 · 校验聚合 throw · 禁数值经 double 中转)
//   AC-44-D4(事件表走 ADR-014 烘焙管线;玩家构建零 JSON 解析器)
//   AC-44-D6(**素材缺失 = 编辑期烘焙门拒绝** —— 阶段 2 谓词集追加「素材文件存在」)
//   AudioEventTableGates(规则 1–9 / AC-44-09 / AC-44-D9 的**校验纯函数** —— 本件是它的唯一调用者)
//   AudioAssetExistence(D6 谓词单一出处)
//
// ⚠️ **本件把三层白名单「引用」而非「另抄」**(ADR-024 §⑥ 单一出处):
//   `TopLevelKeys` / `RowKeys` / `TierMapColumns` 全部直取 `AudioEventTableGates` 常量。
//   历史教训:`IEventSink.Append` 的路由白名单当年「从哪读」从未裁决 ⇒ 三处登记互不相同。
//   本件不重复那个错误 —— 白名单只有一处,门与绑定层共用同一对象。
//
// ⚠️ **阶段 1 = `JsonStage1Lexer`(自研 DOM)**,不是手写扫描器。
//   ADR-014 §三的两阶段工具链:阶段 1 = `JsonTextReader` **仅词法**(token 种类结构化记录,
//   Integer 与 String 永不混淆),阶段 2 = 本件(per-schema 绑定 + 校验)。
//   ⚠️ 既有 `event_table_gate_test.cs` 内**另有一份手写扫描器**(`:1231+`,含 `__truncated__`
//   哨兵语义),它是 story-002 的负向夹具载体(**测试自持**,不是生产抽取器)—— 两份并存
//   是**已登记的重复**(见 `production/qa/evidence/impl-s14-6-asset-pipeline-2026-10-16.md`
//   §登记项),不属本件裁决面;本件**不**去动它(动它 = 改 story-002 的截断夹具语义)。
//
// ⚠️ **cue ordinal 语义(本件钉死)**:`AudioCueDto.Cue` 是 `int` 序号
//   (`Sim.Contracts/PresentationDtos.cs` 自陈「解析在上游完成,44 只收结果 cue」),
//   而 `AudioEventTableGates.RowKeys` 白名单**无 ordinal 字段** ⇒ **ordinal = `rows[]` 行序**。
//   代价:重排 `rows[]` = 静默重编号。缓解 = 源内容哈希派生 `ConfigVersion`(ADR-014 §五)
//   ⇒ 跨版本存档 / 流被存档头比对拒收。该性质由测试钉住(行序即 ordinal + 同源两次烘焙逐位一致)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.EditorTools.Gates;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary><c>audio_events.json</c> 的阶段 2 绑定器(词法 → 模型 → 门 → 素材存在性,聚合 throw)。</summary>
    public static class AudioEventTableBinder
    {
        /// <summary>作者态源文件名(单一出处;测试与菜单共用)。</summary>
        public const string SourceFileName = "audio_events.json";

        /// <summary>源文件所在目录(相对仓根;承既有先例 <c>assets/data/</c>)。</summary>
        public const string DataDirName = "assets/data";

        /// <summary>绑定结果(表模型 + 源 schema_version)。</summary>
        public readonly struct BindResult
        {
            /// <summary>绑定前结构(与 <see cref="AudioEventTableGates"/> 同型;行序 = cue ordinal)。</summary>
            public readonly AudioEventTable Table;

            /// <summary>源 schema_version(= 写方头槽 offset 8 的唯一真值来源)。</summary>
            public readonly uint SchemaVersion;

            /// <summary>构造绑定结果(写方 / 测试直用)。</summary>
            public BindResult(AudioEventTable table, uint schemaVersion)
            {
                Table = table; SchemaVersion = schemaVersion;
            }
        }

        // ══════════════ 主入口 ══════════════

        /// <summary>绑定 + 门(规则 1–9 / AC-44-09 / AC-44-D9)+ **D6 素材存在性谓词**,聚合 throw。
        /// <para>⚠️ <paramref name="assetRoot"/> **必填** —— 刻意不提供「根可空 ⇒ 跳过存在性」的形态
        /// (2026-10-16 用户裁定 ①:门强制;可选门 = story-010 原稿 B1 的假绿翻版)。</para></summary>
        /// <param name="json">作者态 JSON 文本。</param>
        /// <param name="assetRoot">素材根绝对路径(见 <see cref="AudioAssetExistence.ResolveRoot"/>)。</param>
        /// <exception cref="BakeValidationException">词法失败 / 门违例 / 素材缺失(聚合)。</exception>
        public static BindResult Bind(string json, string assetRoot)
        {
            if (assetRoot == null) throw new ArgumentNullException(nameof(assetRoot));

            AudioEventTable table = ParseOnly(json);
            var errors = new List<string>(AudioEventTableGates.Validate(table));
            AppendMissingAssetErrors(table, assetRoot, errors);

            if (errors.Count > 0) throw new BakeValidationException(errors);

            return new BindResult(table, ReadSchemaVersionOf(table));
        }

        /// <summary>只做词法 + 模型构建(**不跑门、不查素材**)—— 负向夹具与 schema 负例的入口。
        /// <para>⚠️ 生产链路**只经 <see cref="Bind"/>**;本方法存在是为了让负例能逐条打门
        /// (「哪一条规则红了」在聚合 throw 下不可分辨)。</para></summary>
        /// <exception cref="BakeValidationException">阶段 1 词法失败 / 根非对象。</exception>
        public static AudioEventTable ParseOnly(string json)
        {
            if (!JsonStage1Lexer.TryParse(json, out JsonNode root, out string lexErr))
                throw new BakeValidationException(
                    new List<string> { $"{SourceFileName} 阶段1 词法失败:{lexErr}" });

            if (root.Kind != JsonNodeKind.Object)
                throw new BakeValidationException(
                    new List<string> { $"{SourceFileName} 根须为对象(实得 {root.Kind})" });

            return Build(root);
        }

        // ══════════════ D6:素材存在性谓词(阶段 2 谓词集追加项)══════════════

        /// <summary>逐行查 <c>assets[]</c>,缺任一项 ⇒ 一条聚合错误(点名 cue + 文件名 + 期望根)。</summary>
        private static void AppendMissingAssetErrors(
            AudioEventTable table, string assetRoot, List<string> errors)
        {
            if (table?.Rows == null) return;   // 行层缺失已由门报;此处不重复

            for (int i = 0; i < table.Rows.Count; i++)
            {
                AudioEventTableRow row = table.Rows[i];
                if (row == null) continue;

                IReadOnlyList<string> missing = AudioAssetExistence.Missing(row.Assets, assetRoot);
                if (missing.Count == 0) continue;

                errors.Add($"[D6·素材存在性] 行「{row.Cue ?? $"rows[{i}]"}」引用了缺失素材:" +
                           string.Join(", ", missing) +
                           $" —— 期望根 {AudioAssetExistence.LogicalRoot}(物理 {assetRoot})。" +
                           "AC-44-D6:素材缺失 = **编辑期烘焙门拒绝**(构建失败),不是运行期降级 / 静默播空 cue");
            }
        }

        // ══════════════ 阶段 2:JsonNode → AudioEventTable 模型 ══════════════

        /// <summary>阶段 1 DOM → 门可消费的表模型(字段缺失保持 null,供 NOT-RUN 守卫区分)。</summary>
        private static AudioEventTable Build(JsonNode root)
        {
            var table = new AudioEventTable
            {
                SchemaVersionRaw = RawOf(root, "schema_version"),
                PresentKeys = new List<string>(root.Keys),
            };

            if (root.TryGet("tier_map", out JsonNode tierMap) && tierMap != null)
            {
                var tiers = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
                if (tierMap.Kind == JsonNodeKind.Object && tierMap.Props != null)
                {
                    for (int i = 0; i < tierMap.Props.Count; i++)
                    {
                        string tierKey = tierMap.Props[i].Key;
                        JsonNode tierValue = tierMap.Props[i].Value;
                        tiers[tierKey] = MembersOf(tierValue, tierKey);
                    }
                }
                table.TierMap = tiers;
            }

            if (root.TryGet("rows", out JsonNode rowsNode) && rowsNode != null)
            {
                var rows = new List<AudioEventTableRow>();
                if (rowsNode.Kind == JsonNodeKind.Array && rowsNode.Items != null)
                    for (int i = 0; i < rowsNode.Items.Count; i++)
                        rows.Add(BuildRow(rowsNode.Items[i]));
                table.Rows = rows;
            }

            return table;
        }

        /// <summary>单个行对象 → <see cref="AudioEventTableRow"/>。</summary>
        private static AudioEventTableRow BuildRow(JsonNode obj)
        {
            var row = new AudioEventTableRow();

            if (obj == null || obj.Kind != JsonNodeKind.Object || obj.Props == null)
            {
                // 非对象行:PresentKeys = 空集(无键可查),字段全 null ⇒ 门按「字段缺失」逐条报。
                row.PresentKeys = new List<string>();
                return row;
            }

            var keys = new List<string>(obj.Props.Count);
            for (int i = 0; i < obj.Props.Count; i++) keys.Add(obj.Props[i].Key);
            row.PresentKeys = keys;

            row.Cue = RawOf(obj, "cue");
            row.SchemaVersionRaw = RawOf(obj, "schema_version");
            row.WhitelistCategory = RawOf(obj, "whitelist_category");
            row.TriggerSource = RawOf(obj, "trigger_source");
            row.Bus = RawOf(obj, "bus");
            row.ClockRef = RawOf(obj, "clock_ref");
            row.XfadeRaw = RawOf(obj, "xfade_ms");
            // story-020 重裁 ①:缺省 = 不是变体行(assets[] 是并列素材,非变体集)。
            row.VariantAxisRaw = RawOf(obj, "variant_axis");

            // ⚠️ subtitle_text 的三态:**缺失** = null / **在但空** = "" —— 两者对语声族都是失败,
            //    门据此区分诊断措辞(承 story-002 模型注)。
            if (obj.TryGet("subtitle_text", out JsonNode subNode))
                row.SubtitleText = subNode.Kind == JsonNodeKind.Null ? string.Empty : RawOf(subNode);

            if (obj.TryGet("loop", out JsonNode loopNode) && loopNode != null)
            {
                if (loopNode.Kind == JsonNodeKind.Boolean) row.Loop = loopNode.Bool;
                else if (loopNode.Kind == JsonNodeKind.String &&
                         bool.TryParse(loopNode.Str, out bool parsed)) row.Loop = parsed;
                else if (loopNode.Kind == JsonNodeKind.Integer) row.Loop = loopNode.Int != 0;
            }

            if (obj.TryGet("assets", out JsonNode assetsNode) && assetsNode != null)
            {
                var assets = new List<string>();
                if (assetsNode.Kind == JsonNodeKind.Array && assetsNode.Items != null)
                    for (int i = 0; i < assetsNode.Items.Count; i++)
                        assets.Add(RawOf(assetsNode.Items[i]));
                row.Assets = assets;
            }

            if (obj.TryGet("tier_params", out JsonNode tierParamsNode) && tierParamsNode != null)
            {
                var tierParams = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
                if (tierParamsNode.Kind == JsonNodeKind.Object && tierParamsNode.Props != null)
                {
                    for (int i = 0; i < tierParamsNode.Props.Count; i++)
                    {
                        string tierKey = tierParamsNode.Props[i].Key;
                        JsonNode tierValue = tierParamsNode.Props[i].Value;
                        // GDD :487 形状 = per-tier 嵌套;平坦残形(值非对象)以**自键占位**保留档位键,
                        // 交由门的「档位键 ∉ {0,1,2}」拒收 —— 不在此处私自补救(补救 = 静默吸收结构漂移)。
                        tierParams[tierKey] = tierValue != null && tierValue.Kind == JsonNodeKind.Object
                            ? MembersOf(tierValue, tierKey)
                            : new Dictionary<string, string>(StringComparer.Ordinal) { [tierKey] = RawOf(tierValue) };
                    }
                }
                row.TierParams = tierParams;
            }

            if (obj.TryGet("adventitious_policy", out JsonNode policyNode) && policyNode != null)
            {
                var policy = new AdventitiousPolicyRaw();
                if (policyNode.Kind == JsonNodeKind.Object && policyNode.Props != null)
                {
                    policy.TriggerPhaseRaw = RawOf(policyNode, "trigger_phase");
                    policy.JitterRaw = RawOf(policyNode, "jitter");
                }
                row.Policy = policy;
            }

            return row;
        }

        /// <summary>对象节点 → 成员字典(键 → 原文);非对象 ⇒ 自键占位(结构异常交由门报)。</summary>
        private static IReadOnlyDictionary<string, string> MembersOf(JsonNode node, string fallbackKey)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            if (node != null && node.Kind == JsonNodeKind.Object && node.Props != null)
            {
                for (int i = 0; i < node.Props.Count; i++)
                    map[node.Props[i].Key] = RawOf(node.Props[i].Value);
                return map;
            }
            map[fallbackKey] = RawOf(node);
            return map;
        }

        // ══════════════ 小件 ══════════════

        /// <summary>按名取成员原文;**字段缺失 / null 字面量 ⇒ null**(门据此判「缺失」)。
        /// <para>⚠️ 值形态:字符串取**去引号**正文(门按字面量判,如 <c>"0.95×全周期"</c>);
        /// 整数 / 浮点取 <c>RawText</c>(不经 double 中转 —— ADR-014 §三)。</para></summary>
        private static string RawOf(JsonNode obj, string key)
        {
            if (obj == null || obj.Kind != JsonNodeKind.Object) return null;
            if (!obj.TryGet(key, out JsonNode node)) return null;
            return RawOf(node);
        }

        /// <summary>任意节点 → 原文字符串(null 字面量 ⇒ null)。</summary>
        private static string RawOf(JsonNode node)
        {
            if (node == null || node.Kind == JsonNodeKind.Null) return null;
            if (node.Kind == JsonNodeKind.String) return node.Str;
            return node.RawText;
        }

        /// <summary>顶层 <c>schema_version</c> 原文 → u32(非 u32 域 ⇒ 0:门已就此报错,写方不再二次抛)。</summary>
        private static uint ReadSchemaVersionOf(AudioEventTable table)
        {
            string raw = table?.SchemaVersionRaw;
            if (string.IsNullOrEmpty(raw)) return 0;
            return uint.TryParse(raw, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out uint v) ? v : 0;
        }
    }
}
