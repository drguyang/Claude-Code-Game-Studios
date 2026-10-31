// T1 · S3 —— 53 禁忌 / 过量代价表(assets/data/53_cost_table.json)的阶段 2 绑定器。
//
// 权威来源:
//   ADR-035 §Decision ②(表形状:效果一律**外键**、全整数域、**零 Fix**;rows 非空而
//     harm_kind_enabled 空 ⇒ 构建期拒收)
//   ADR-035 §Decision ③(**N-1** injury_id 所指行 lethal == false;/**N-3** 不得是结局轴符号;
//     **N-5** 不得指向终局型伤情(2026-10-31 新增 · S21-10 · TD-medcons-20);
//     **N-4′** canary —— 见 §Risks R2)
//   ADR-035 §Decision ⑥(P0 **只允许** contraindication;含 overdose ⇒ 构建期拒收)
//   ADR-035 §Risks R4(空表窗口:rows 填了而 enabled 没开 ⇒ TryLookup 恒 false = 静默跳过)
//   ADR-035 §Risks R5 + §Validation **V-10**(注册表零 lethal=false 伤情 ⇒ **告警**,点名 TD-sim-03)
//   ADR-014 §三(阶段 2 per-schema 绑定:未知键硬失败 · 校验聚合一次 throw · 禁数值经 double 中转)
//
// ⚠️ **本件今日没有任何读方**:S4 的 CostTable53 视图未建,亦无 CookedWriter / DataBakeMenu 入口
//    ⇒ 它**不改变任何运行期行为**。这是 T1「形状件」的预期形态,不是遗漏。
//
// ⚠️ **两段 JSON 入参(成本表 + 疾病注册表)**:N-1 / N-3 / V-10 都必须读注册表。
//    「注册表长什么样」的真源是 `DiseaseRegistryBinder` —— 本件**复用其绑定与全部校验**,
//    不另建第二份读取逻辑(否则 9 的 schema 一改,53 侧静默失真)。
//
// ⚠️ **canary 必须先于注册表绑定执行**:`DiseaseRegistryBinder` 对未知键是**硬失败**,
//    若先调它,`compounds` 会在那里被一条泛化的「未知键」报文拦下 —— 那就丢掉了
//    「**这件事意味着 ADR-035 N-4 该复查了**」这个唯一有用的信息。故本件先扫一遍注册表原文。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Consequences;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>`53_cost_table.json` 的阶段 2 绑定器(禁忌 / 过量代价表;**效果一律外键**)。</summary>
    /// <remarks>
    /// <para><b>为什么需要它</b>:`AC-5` 的 Then 句「致死行在 P0 表 = 构建期拒收」此前**没有宾语**
    /// (表从未存在)。本件是那个宾语的执行体(`TD-medcons-15` / `ADR-035`)。</para>
    /// <para><b>与 Sim 侧的关系</b>:本件 = **唯一构建期校验点**;S4 的 `CostTable53.Table` 构造器
    /// 复述同一拒收(纵深防御,防「绕过 Bake 直构表」),两侧口径须一致。</para>
    /// </remarks>
    public static class CostTableBinder
    {
        /// <summary>源文件名(报文前缀;与 `DataBakeMenu` / 证据件同一字面)。</summary>
        public const string FileName = "53_cost_table.json";

        /// <summary>伤害种类闭集(P0 只允许第一个;`overdose` 保留在闭集里但**被拒**,见 §Decision ⑥)。</summary>
        public const string KindContraindication = "contraindication";

        /// <summary>过量支 —— 结构上保留、**P0 禁止启用**(`ADR-035 §Decision ⑥`)。</summary>
        public const string KindOverdose = "overdose";

        private static readonly string[] RootKeys =
            { "schema_version", "harm_kind_enabled", "bands", "max_severity_band",
              "overdose_band_thresholds", "rows" };

        private static readonly string[] RowKeys =
            { "harm_kind", "band", "injury_id", "severity_band" };

        /// <summary>一行代价(键 = `(HarmKind, Band)`;效果 = **外键引用** 9 的伤情 id)。</summary>
        public readonly struct CostRow
        {
            /// <summary>伤害种类(今日恒 <see cref="KindContraindication"/>)。</summary>
            public readonly string HarmKind;

            /// <summary>档序数(同 kind 内单射)。</summary>
            public readonly int Band;

            /// <summary>外键 → 9 注册表的**伤情** id(N-1 已保证其 `lethal == false`)。</summary>
            public readonly string InjuryId;

            /// <summary>程度档(引用 9 域的档位,**不是**后果分值 —— `AC-53-01`)。</summary>
            public readonly int SeverityBand;

            public CostRow(string harmKind, int band, string injuryId, int severityBand)
            {
                HarmKind = harmKind; Band = band; InjuryId = injuryId; SeverityBand = severityBand;
            }
        }

        /// <summary>绑定结果(供写方落盘 / 测试直用 / S4 的表物化)。</summary>
        public readonly struct BindResult
        {
            /// <summary>代价行(今日恒空 —— 见 <see cref="Warnings"/> 的第一条)。</summary>
            public readonly List<CostRow> Rows;

            /// <summary>源 schema_version(= 写方头槽的唯一真值来源)。</summary>
            public readonly uint SchemaVersion;

            /// <summary>
            /// **告警**(非致命)。存在的理由:`ADR-035 §Validation V-10` 要求「`N-1` 的绿灯必须能
            /// 一眼看出是**无输入**」—— 否则读到一片绿的人会以为「药害不致死已被证明」。
            /// </summary>
            public readonly List<string> Warnings;

            /// <summary>
            /// 过量各档的**下界**(升序;`Fix`,与 `drug_potency` 同量纲)。
            /// 空 = 过量支未启用。2026-10-30 新增(承 `OverdoseEvaluator`)。
            /// </summary>
            public readonly List<Fix> OverdoseBandThresholds;

            public BindResult(List<CostRow> rows, uint schemaVersion, List<string> warnings,
                              List<Fix> overdoseBandThresholds = null)
            {
                Rows = rows; SchemaVersion = schemaVersion; Warnings = warnings;
                OverdoseBandThresholds = overdoseBandThresholds ?? new List<Fix>();
            }
        }

        /// <summary>绑定 + 全部构建期拒收(聚合为一次 <see cref="BakeValidationException"/>)。</summary>
        /// <param name="costJson">`53_cost_table.json` 源文本。</param>
        /// <param name="registryJson">`disease_registry.json` 源文本(N-1 / N-3 / V-10 的输入)。</param>
        /// <exception cref="BakeValidationException">词法失败 / canary 命中 / 任一条校验违例(聚合)。</exception>
        public static BindResult Bind(string costJson, string registryJson)
        {
            if (registryJson == null) throw new ArgumentNullException(nameof(registryJson));

            // ① canary 先行(理由见文件头):compounds 一出现即拦,且报文指向 ADR-035 N-4。
            AssertNoCompoundsCanary(registryJson);

            var errors = new List<string>();
            var warnings = new List<string>();

            JsonNode root = ParseRoot(costJson, errors, out uint schemaVersion);
            if (root == null) throw new BakeValidationException(errors); // 词法/根形态:行层不可达

            List<string> enabled = ReadEnabledKinds(root, errors);
            if (!root.TryGet("rows", out JsonNode rowsNode) || rowsNode.Kind != JsonNodeKind.Array)
            {
                errors.Add($"{FileName}:缺 rows 数组");
                throw new BakeValidationException(errors); // 早 throw 豁免点:行层结构不可达
            }

            RegistryView registry = LoadRegistry(registryJson, errors);
            int? maxSeverity = ReadMaxSeverityBand(root, errors);
            Dictionary<string, int> bandCounts = ReadBands(root, enabled, warnings, errors);
            List<Fix> overdoseThresholds = ReadOverdoseThresholds(root, enabled, bandCounts, errors);

            var rows = ParseRows(rowsNode, enabled, maxSeverity, bandCounts, registry, errors);

            // R4:rows 非空而 harm_kind_enabled 空 ⇒ 表里装了行却没人查 = 静默跳过(与 W-4 同型)。
            // ⚠️ 判据取**源行数**(`rowsNode.Items.Count`)而不是**已绑定行数**:逐行校验一旦开火,
            //    绑定集必为空 ⇒ 用后者写这条判据等于永远不触发(零输入可达的死门)。
            if (rowsNode.Items.Count > 0 && enabled.Count == 0)
                errors.Add($"{FileName}:rows 声明了 {rowsNode.Items.Count} 行,但 harm_kind_enabled 为空 —— " +
                           "该形态下 TryLookup 恒 false ⇒ 行永不生效(静默跳过,与 W-4 同型;ADR-035 §Risks R4)");

            if (rows.Count > 0 && maxSeverity == null)
                warnings.Add($"{FileName}:rows 非空而 max_severity_band 未声明 —— " +
                             "severity_band 无上界可校(档位越界不会被拦);值归内容轮");

            WarnIfNoNonLethalInjury(registry, warnings);

            if (errors.Count > 0) throw new BakeValidationException(errors);
            return new BindResult(rows, schemaVersion, warnings, overdoseThresholds);
        }

        // ═════════════════════ ③ 过量档阈值(2026-10-30)═════════════════════

        /// <summary>
        /// 读 `overdose_band_thresholds` —— 过量各档的**下界**(`Fix` 字符串数组,升序)。
        /// </summary>
        /// <remarks>
        /// <para><b>量纲</b>:与 `drug_potency` 相同(绝对值),**不是**比值。理由见
        /// `Sim.Contracts/OverdoseEvaluator.cs` 头注:`MAX_ACTIVE_DOSE` / `single_dose_max`
        /// 两个 F1 旋钮至今待定,拿它们归一化会让门槛依赖两个未裁定的数。</para>
        /// <para><b>为什么必须严格递增</b>:非递增会让某一档结构性不可达 —— 与 R1-37 的
        /// 「死词条」同型,且更隐蔽(档位只在运行期按 `≥` 比较,写重了不报错、只是永不命中)。
        /// ⇒ 构建期直接拒。</para>
        /// <para><b>启用面的联动</b>:`harm_kind_enabled` 含 `overdose` 而本键缺席 ⇒ 拒
        /// (启用过量却没有档位线 = 表里那几行永不生效,静默跳过同型);
        /// 不含 `overdose` 而本键非空 ⇒ 告警(声明了但没人查)。</para>
        /// </remarks>
        private static List<Fix> ReadOverdoseThresholds(
            JsonNode root, List<string> enabled, Dictionary<string, int> bandCounts, List<string> errors)
        {
            var result = new List<Fix>();
            bool present = root.TryGet("overdose_band_thresholds", out JsonNode node)
                           && node.Kind != JsonNodeKind.Null;
            bool overdoseEnabled = enabled.Contains(KindOverdose);

            if (!present)
            {
                if (overdoseEnabled)
                    errors.Add($"{FileName}:harm_kind_enabled 含 `{KindOverdose}` 但缺 " +
                               "overdose_band_thresholds —— 启用过量却没有档位线 ⇒ 该 kind 的行永不生效");
                return result;
            }

            if (node.Kind != JsonNodeKind.Array)
            {
                errors.Add($"{FileName}:overdose_band_thresholds 须为数组(实得 {node.Kind})");
                return result;
            }

            for (int i = 0; i < node.Items.Count; i++)
            {
                JsonNode item = node.Items[i];
                // ⚠️ Fix 必须写成**字符串**(ADR-014 §三:JSON 数字会经 double 中转,绕开 FixParse)。
                if (item.Kind != JsonNodeKind.String)
                {
                    errors.Add($"{FileName}:overdose_band_thresholds[{i}] 须为字符串(Fix 字面量," +
                               $"如 \"1/4\";实得 {item.Kind}={item.RawText})");
                    continue;
                }
                Fix v;
                try { v = FixParse.Parse(item.Str); }
                catch (Exception ex)
                {
                    errors.Add($"{FileName}:overdose_band_thresholds[{i}]「{item.Str}」不是合法 Fix" +
                               $"({ex.Message})");
                    continue;
                }
                if (v.Raw <= 0)
                {
                    errors.Add($"{FileName}:overdose_band_thresholds[{i}] 须 > 0(实得 {item.Str})");
                    continue;
                }
                if (result.Count > 0 && v.Raw <= result[result.Count - 1].Raw)
                {
                    errors.Add($"{FileName}:overdose_band_thresholds 须**严格递增** —— " +
                               $"[{i}]「{item.Str}」不大于上一档 ⇒ 该档结构性不可达");
                    continue;
                }
                result.Add(v);
            }

            if (result.Count > 0 && bandCounts.TryGetValue(KindOverdose, out int declared)
                && declared != result.Count)
            {
                errors.Add($"{FileName}:bands.{KindOverdose}={declared} 与 " +
                           $"overdose_band_thresholds 的项数 {result.Count} 不一致 —— " +
                           "档数与档位线必须同源,否则某档要么无下界、要么永不命中");
            }

            if (result.Count > 0 && !overdoseEnabled)
                errors.Add($"{FileName}:overdose_band_thresholds 非空但 harm_kind_enabled 不含 " +
                           $"`{KindOverdose}` —— 声明了档位线却没人查(静默跳过同型)");

            return result;
        }

        // ═════════════════════ ① canary(N-4′)═════════════════════

        /// <summary>
        /// **N-4′ canary**:注册表任一病种行出现 `compounds` 键 ⇒ 构建期 `throw`。
        /// </summary>
        /// <remarks>
        /// <para><b>它不是 N-4 本身</b>。`ADR-035 §Risks R2` 的 S1 普查结论:`compounds` 今日
        /// **在作者态与运行时 schema 两层都不存在** ⇒ 原式 N-4(在 compounds 闭包上查致死可达)
        /// **无对象可判**。本 canary 是它的**到期复查提醒**:今天恒绿,`compounds` 一落地就必红。</para>
        /// <para><b>出手步骤(报文里也写)</b>:① 复查 N-4 在本轮真实数据上是否成立
        /// (遍历 白名单 × compounds 可达闭包,看有无 `lethal == true` 的 target);
        /// ② 成立 ⇒ 把本 canary 换成原式 N-4;不成立 ⇒ 在 `ADR-035` 里销案后删除本 canary。</para>
        /// <para>⚠️ 本 canary **不得**被当作「N-4 已实现」—— 按 `ADR-035 §Risks R2` 的原话,
        /// 零输入可达的门**不记作已兑现守卫**。</para>
        /// </remarks>
        private static void AssertNoCompoundsCanary(string registryJson)
        {
            if (!JsonStage1Lexer.TryParse(registryJson, out JsonNode root, out string lexErr))
                throw new BakeValidationException(new List<string>
                    { $"disease_registry.json 阶段1 词法失败(canary 前置):{lexErr}" });
            if (root == null || root.Kind != JsonNodeKind.Object) return;

            if (!root.TryGet("diseases", out JsonNode list) || list.Kind != JsonNodeKind.Array) return;

            for (int i = 0; i < list.Items.Count; i++)
            {
                JsonNode row = list.Items[i];
                if (row.Kind != JsonNodeKind.Object) continue;
                foreach (string key in row.Keys)
                {
                    if (!string.Equals(key, "compounds", StringComparison.Ordinal)) continue;
                    throw new BakeValidationException(new List<string>
                    {
                        $"[N-4′ canary] 疾病注册表第 {i} 行出现 `compounds` 键 ⇒ **ADR-035 N-4 到期复查**。" +
                        "这是**复查提醒,不是拒绝**(compounds 本身是合法的 9 侧机制)。出手步骤:" +
                        "① 遍历 代价表白名单 × compounds 可达闭包,判有无 `lethal == true` 的 target;" +
                        "② 有 ⇒ 把本 canary 换成原式 N-4 并同批写夹具;无 ⇒ 在 ADR-035 §Risks R2 销案后删除本 canary。" +
                        "在此之前 `CostTableBinder` 拒绝烘焙 —— 因为「非致死伤 → 复合 → 致死病」这条绕行" +
                        "一旦成立,本表**永不致死**的保证就破了。",
                    });
                }
            }
        }

        // ═════════════════════ ② 根层 ═════════════════════

        /// <summary>词法 → 根对象 → 根键白名单 → schema_version。失败返回 null(错误已记)。</summary>
        private static JsonNode ParseRoot(string json, List<string> errors, out uint schemaVersion)
        {
            schemaVersion = 0;
            if (!JsonStage1Lexer.TryParse(json, out JsonNode root, out string lexErr))
            {
                errors.Add($"{FileName} 阶段1 词法失败:{lexErr}");
                return null;
            }
            if (root.Kind != JsonNodeKind.Object)
            {
                errors.Add($"{FileName} 根须为对象(实得 {root.Kind})");
                return null;
            }
            RejectUnknownKeys(root, RootKeys, "根", errors);
            schemaVersion = ReadSchemaVersion(root, errors);
            return root;
        }

        /// <summary>读 `harm_kind_enabled`(P0 只允许 contraindication;含 overdose ⇒ 拒)。</summary>
        private static List<string> ReadEnabledKinds(JsonNode root, List<string> errors)
        {
            var enabled = new List<string>();
            if (!root.TryGet("harm_kind_enabled", out JsonNode node))
            {
                errors.Add($"{FileName}:缺 harm_kind_enabled(必填 —— 空集须**显式**写 `[]`)");
                return enabled;
            }
            if (node.Kind != JsonNodeKind.Array)
            {
                errors.Add($"{FileName}:harm_kind_enabled 须为数组(实得 {node.Kind})");
                return enabled;
            }
            for (int i = 0; i < node.Items.Count; i++)
            {
                JsonNode item = node.Items[i];
                if (item.Kind != JsonNodeKind.String)
                {
                    errors.Add($"{FileName}:harm_kind_enabled[{i}] 须为字符串(实得 {item.Kind})");
                    continue;
                }
                string kind = item.Str;
                // 🔁 2026-10-30:`overdose` 的拒收分支已撤销 —— ADR-035 §Decision ⑥ 的解除条件
                //   「TD-sim-02 + TD-prescription-09 两条均结案」当日满足(前者由 9 侧
                //   `ProgressionEvaluator.ComputePeakDrugLoad` + `VitalsDto.PeakDrugLoad` 结清)。
                //   ⚠️ 但「能开启」≠「必须开启」:是否启用仍由作者态本数组决定;
                //   启用而未给 `overdose_band_thresholds` ⇒ 见 ReadOverdoseThresholds 的拒收。
                if (!string.Equals(kind, KindContraindication, StringComparison.Ordinal) &&
                    !string.Equals(kind, KindOverdose, StringComparison.Ordinal))
                {
                    errors.Add($"{FileName}:harm_kind_enabled[{i}]「{kind}」∉ 闭集 " +
                               $"{{{KindContraindication}, {KindOverdose}}}");
                    continue;
                }
                if (!enabled.Contains(kind)) enabled.Add(kind);
            }
            return enabled;
        }

        /// <summary>读 `max_severity_band`(null = 值归内容轮;整数须 ≥ 0)。</summary>
        private static int? ReadMaxSeverityBand(JsonNode root, List<string> errors)
        {
            if (!root.TryGet("max_severity_band", out JsonNode node) || node.Kind == JsonNodeKind.Null)
                return null;
            if (node.Kind != JsonNodeKind.Integer)
            {
                errors.Add($"{FileName}:max_severity_band 须为整数或 null(实得 {node.Kind}={node.RawText})");
                return null;
            }
            if (node.Int < 0)
            {
                errors.Add($"{FileName}:max_severity_band={node.Int} < 0 —— 档序数自 0 起");
                return null;
            }
            if (node.Int > int.MaxValue)
            {
                errors.Add($"{FileName}:max_severity_band={node.Int} 超出 int 域");
                return null;
            }
            return (int)node.Int;
        }

        /// <summary>校验 `bands` 块(kind → 档数 | null);未启用的 kind 只告警(ADR 未钉,不自裁为错)。</summary>
        /// <returns>已声明的档数(kind → 档数);未声明 / 写 <c>null</c> 的 kind 不在表内。</returns>
        /// <remarks>
        /// 🔁 <b>2026-10-30 起本块**参与校验**</b>(原为纯装饰:声明 3 档而写出 `band: 7` 的行也能过)。
        /// 行层用 <see cref="CheckBandInRange"/> 查 `band &lt; bands[kind]`。
        /// ⚠️ **未声明 ≠ 0 档**:`null` / 缺键表示「档数未定(值归内容轮)」,此时**不校**上界 ——
        /// 与 `max_severity_band` 的 `null` 语义同款。**不得**把它读成「0 档 ⇒ 任何行都拒」。
        /// </remarks>
        private static Dictionary<string, int> ReadBands(
            JsonNode root, List<string> enabled, List<string> warnings, List<string> errors)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            if (!root.TryGet("bands", out JsonNode bands) || bands.Kind == JsonNodeKind.Null) return counts;
            if (bands.Kind != JsonNodeKind.Object)
            {
                errors.Add($"{FileName}:bands 须为对象或 null(实得 {bands.Kind})");
                return counts;
            }
            foreach (string key in bands.Keys)
            {
                bool known = string.Equals(key, KindContraindication, StringComparison.Ordinal)
                             || string.Equals(key, KindOverdose, StringComparison.Ordinal);
                if (!known)
                {
                    errors.Add($"{FileName}:bands 出现未知 kind「{key}」");
                    continue;
                }
                if (!enabled.Contains(key))
                    warnings.Add($"{FileName}:bands 声明了未启用的 kind「{key}」的档数 —— 该 kind 的行会被拒");
                if (!bands.TryGet(key, out JsonNode v) || v.Kind == JsonNodeKind.Null) continue;
                if (v.Kind != JsonNodeKind.Integer || v.Int < 0)
                {
                    errors.Add($"{FileName}:bands.{key} 须为 ≥ 0 的整数或 null(实得 {v.Kind}={v.RawText})");
                    continue;
                }
                counts[key] = (int)v.Int;
            }
            return counts;
        }

        /// <summary>`band` 须落在声明的档数内(`0 ≤ band &lt; bands[kind]`);未声明档数 ⇒ 不校。</summary>
        /// <remarks>
        /// <para>⚠️ <b>这条门的存在理由</b>:没有它,`bands` 就是装饰字段 —— 作者声明「3 档」、
        /// 却写出 `band: 7` 的行,构建期零反馈,而运行期 `TryLookup` 永远 miss ⇒
        /// **行装了但永不生效**(与 `ADR-035 §Risks R4` 的「静默跳过」同型)。</para>
        /// <para>⚠️ 与 <see cref="ReadMaxSeverityBand"/> 的区别:那条管 **severity_band** 的上界
        /// (伤情程度档),本条管 **band** 的上界(禁忌档序数)。两者**不是同一把尺**,不共用。</para>
        /// </remarks>
        private static bool CheckBandInRange(
            int band, string harmKind, Dictionary<string, int> bandCounts, int i, List<string> errors)
        {
            if (!bandCounts.TryGetValue(harmKind, out int declared)) return true;
            if (band < declared) return true;
            errors.Add($"{FileName}:rows[{i}] band={band} ≥ bands.{harmKind}={declared} —— " +
                       "档序数须自 0 起连续且 < 声明档数(否则该行永不生效 = 静默跳过)");
            return false;
        }

        // ═════════════════════ ③ 行层 ═════════════════════

        /// <summary>逐行解析 + 行级拒收;返回合法行(重复键的行不入表)。</summary>
        private static List<CostRow> ParseRows(
            JsonNode rowsNode, List<string> enabled, int? maxSeverity, Dictionary<string, int> bandCounts,
            RegistryView registry, List<string> errors)
        {
            var rows = new List<CostRow>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < rowsNode.Items.Count; i++)
                ParseRow(rowsNode.Items[i], i, enabled, maxSeverity, bandCounts, registry, rows, seen, errors);
            return rows;
        }

        /// <summary>解析单行:字段读取与判据拆到 <see cref="TryReadRowFields"/>,本方法只管单射与入表。</summary>
        private static void ParseRow(
            JsonNode row, int i, List<string> enabled, int? maxSeverity, Dictionary<string, int> bandCounts,
            RegistryView registry, List<CostRow> rows, HashSet<string> seen, List<string> errors)
        {
            if (row.Kind != JsonNodeKind.Object)
            {
                errors.Add($"{FileName}:rows[{i}] 须为对象(实得 {row.Kind})");
                return;
            }
            RejectUnknownKeys(row, RowKeys, $"rows[{i}]", errors);

            if (!TryReadRowFields(row, i, enabled, maxSeverity, bandCounts, registry, errors,
                                  out CostRow parsed, out string dedup))
                return;

            if (!seen.Add(dedup))
            {
                errors.Add($"{FileName}:rows[{i}] 重复键 ({parsed.HarmKind}, {parsed.Band}) —— " +
                           "(harm_kind, band) 须单射(ADR-035 §Decision ②)");
                return;
            }
            rows.Add(parsed);
        }

        /// <summary>取一行四字段并逐条过判据(kind 闭集 / 启用 / 类型 / 上界 / N-3 / N-1)。</summary>
        /// <returns>true = 该行合法;<paramref name="dedup"/> 为单射键。</returns>
        private static bool TryReadRowFields(
            JsonNode row, int i, List<string> enabled, int? maxSeverity, Dictionary<string, int> bandCounts,
            RegistryView registry, List<string> errors, out CostRow parsed, out string dedup)
        {
            parsed = default;
            dedup = null;
            if (!TryReadString(row, "harm_kind", i, errors, out string harmKind)) return false;

            // ⚠️ 两条都**报错**,不得「静默跳过」—— 跳过就等于 R4 要防的那个形态
            //    (行装进去了、永远没人查)。本件第一版正是这么写的,自查时抓到。
            if (!IsKnownKind(harmKind))
            {
                errors.Add($"{FileName}:rows[{i}] harm_kind「{harmKind}」∉ 闭集 " +
                           $"{{{KindContraindication}, {KindOverdose}}}");
                return false;
            }
            if (!enabled.Contains(harmKind))
            {
                errors.Add($"{FileName}:rows[{i}] harm_kind「{harmKind}」**未启用** " +
                           "(不在 harm_kind_enabled 内 ⇒ 该行永远不会被查 = 静默跳过)");
                return false;
            }

            if (!TryReadInt(row, "band", i, errors, out int band)) return false;
            if (!CheckBandInRange(band, harmKind, bandCounts, i, errors)) return false;
            if (!TryReadString(row, "injury_id", i, errors, out string injuryId)) return false;
            if (!TryReadInt(row, "severity_band", i, errors, out int severity)) return false;

            if (maxSeverity != null && severity > maxSeverity.Value)
            {
                errors.Add($"{FileName}:rows[{i}] severity_band={severity} > max_severity_band={maxSeverity.Value}");
                return false;
            }

            // N-3 先于 N-1:先把「拿 53 自己的结局枚举当 injury_id」这种符号级错误摘出来,
            // 否则它会以一条泛化的「外键查无」报文出现,指向的地方不是坏掉的地方。
            if (IsOutcomeAxisSymbol(injuryId))
            {
                errors.Add($"{FileName}:rows[{i}] injury_id「{injuryId}」是 **Outcome 结局轴符号** —— " +
                           "N-3(ADR-035 §Decision ③):结局轴归 9 结算、53 只读,收进本表 = schema 错");
                return false;
            }

            if (!ValidateInjuryForeign(injuryId, i, registry, errors)) return false;

            parsed = new CostRow(harmKind, band, injuryId, severity);
            dedup = harmKind + "|" + band;
            return true;
        }

        /// <summary>N-1:外键存在 **且** 是伤情 **且** `lethal == false`。</summary>
        private static bool ValidateInjuryForeign(string key, int i, RegistryView registry, List<string> errors)
        {
            if (!registry.TryGet(key, out int kind, out int lethality))
            {
                errors.Add($"{FileName}:rows[{i}] injury_id「{key}」不在疾病注册表内 —— " +
                           "外键失效(ADR-035 §Decision ②:效果**一律外键**,禁内联属性副本)");
                return false;
            }
            if (kind != (int)DiseaseKind.Injury)
            {
                errors.Add($"{FileName}:rows[{i}] injury_id「{key}」的 kind ≠ injury —— " +
                           "本字段指向 9 的**伤情**注册表闭集;病种 id 不是合法落点");
                return false;
            }
            if (lethality != 0)
            {
                errors.Add($"{FileName}:rows[{i}] injury_id「{key}」的 lethal = true —— " +
                           "**N-1(永不致死)**:白名单只能指非致死伤情。" +
                           "「致死行在 P0 表 = 构建期拒收」正是 `AC-5` 的 Then 句");
                return false;
            }
            // ── N-5(2026-10-31 新增 · `S21-10` · `TD-medcons-20`)──
            //   「终局型」= 病人被**送入**的状态(25 的生命归零结算产物,如 `INJ_COMA`),
            //   不是可挂载的病理。53 的代价表是**离散查表**、没有伤害轨 ⇒ 它只能诱发病理,
            //   不能裁定状态终局。判据打在**行属性** `terminal_state` 上,不认名字。
            //   ⚠️ 位置在 N-1 之后:一个终局型伤情通常是 `lethal == false`(昏迷不是死亡),
            //      故 N-1 拦不住它 —— 这正是本条存在的理由。
            if (registry.IsTerminal(key))
            {
                errors.Add($"{FileName}:rows[{i}] injury_id「{key}」是**终局型伤情** —— " +
                           "**N-5(药害不裁定状态终局)**:代价表只能指「可挂载的病理」,不能指" +
                           "「病人被送入的状态」(如昏迷)。53 无伤害轨,终局归 9/25 结算");
                return false;
            }
            return true;
        }

        // ═════════════════════ ④ 注册表视图 ═════════════════════

        /// <summary>
        /// 注册表只读视图(键 → kind / lethality)。**由 <see cref="DiseaseRegistryBinder"/> 产出**,
        /// 本件不自己解析字段(否则 9 的 schema 一改,53 侧静默失真)。
        /// </summary>
        private readonly struct RegistryView
        {
            private readonly Dictionary<string, int> _kind;
            private readonly Dictionary<string, int> _lethality;
            private readonly HashSet<string> _terminal;

            private RegistryView(Dictionary<string, int> kind, Dictionary<string, int> lethality,
                                 HashSet<string> terminal)
            {
                _kind = kind; _lethality = lethality; _terminal = terminal;
            }

            /// <summary>该键是否被 9 标为**终局型**(`terminal_state == true`)。N-5 的唯一输入。</summary>
            public bool IsTerminal(string key)
                => key != null && _terminal != null && _terminal.Contains(key);

            public bool TryGet(string key, out int kind, out int lethality)
            {
                kind = 0; lethality = 0;
                if (key == null || _kind == null || !_kind.TryGetValue(key, out kind)) return false;
                _lethality.TryGetValue(key, out lethality);
                return true;
            }

            /// <summary>是否存在**伤情** ∧ `lethal == false` 的行(今日为 false —— 见 V-10 告警)。</summary>
            public bool HasNonLethalInjury()
            {
                if (_kind == null) return false;
                foreach (KeyValuePair<string, int> kv in _kind)
                    if (kv.Value == (int)DiseaseKind.Injury
                        && _lethality.TryGetValue(kv.Key, out int l) && l == 0)
                        return true;
                return false;
            }

            public static RegistryView Build(List<DiseaseRegistryEntry> entries,
                                             List<string> terminalKeys = null)
            {
                var kind = new Dictionary<string, int>(StringComparer.Ordinal);
                var lethal = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (DiseaseRegistryEntry e in entries)
                {
                    if (e == null || string.IsNullOrEmpty(e.DiseaseKey)) continue;
                    kind[e.DiseaseKey] = (int)e.Kind;
                    lethal[e.DiseaseKey] = e.Lethality;
                }
                var terminal = new HashSet<string>(StringComparer.Ordinal);
                if (terminalKeys != null)
                    foreach (string k in terminalKeys)
                        if (!string.IsNullOrEmpty(k)) terminal.Add(k);
                return new RegistryView(kind, lethal, terminal);
            }
        }

        /// <summary>注册表绑定(失败 ⇒ 记一条错误,视图退化为「空表」—— 后续外键检查会逐行报失效)。</summary>
        private static RegistryView LoadRegistry(string registryJson, List<string> errors)
        {
            try
            {
                DiseaseRegistryBinder.BindResult reg = DiseaseRegistryBinder.Bind(registryJson);
                // N-5 的输入走**侧信道**(`terminal_state` 不进 `DiseaseRegistryEntry`,见 9 侧注)。
                return RegistryView.Build(reg.Rows, reg.TerminalRowKeys);
            }
            catch (BakeValidationException ex)
            {
                errors.Add($"{FileName}:疾病注册表自身校验失败({ex.Errors.Count} 条)—— " +
                           "本表的外键无真源可比对:" + string.Join(" | ", ex.Errors));
                return RegistryView.Build(new List<DiseaseRegistryEntry>());
            }
        }

        /// <summary>
        /// **V-10 告警**:注册表零 `lethal == false` 伤情 ⇒ `N-1` 的可行性失效(真空真)。
        /// </summary>
        /// <remarks>
        /// 今日的实况(`TD-sim-03`):注册表 5 件伤情**全部** `lethal = true` ⇒ 本表 `injury_id`
        /// **一列无可填** ⇒ 「永不致死」会以「无行可填」的方式自动成立。这是**真空真**,不是门在工作。
        /// **告警而非拒收**:空表是合法惰性形态(T1),拒收会让骨架都建不了。
        /// </remarks>
        private static void WarnIfNoNonLethalInjury(RegistryView registry, List<string> warnings)
        {
            if (registry.HasNonLethalInjury()) return;
            warnings.Add("[V-10] 疾病注册表**零 `lethal == false` 伤情** ⇒ N-1 的绿灯是「**无输入**」, " +
                         "不是「药害不致死已获证明」。前置债:**TD-sim-03**" +
                         "(owner 9:注册表须先有 ≥1 件非致死伤情,最自然落点 = INJ_BLUNT)");
        }

        // ═════════════════════ ⑤ 小工具 ═════════════════════

        /// <summary>该名字是否属于 `harm_kind` 闭集(`contraindication` / `overdose`)。</summary>
        private static bool IsKnownKind(string kind)
            => string.Equals(kind, KindContraindication, StringComparison.Ordinal)
            || string.Equals(kind, KindOverdose, StringComparison.Ordinal);

        /// <summary>该名字是否是 `Outcome53.Outcome` 的**结局轴**成员(符号级,含 snake_case 形态)。</summary>
        private static bool IsOutcomeAxisSymbol(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return string.Equals(name, nameof(Outcome53.Outcome.PatientLives), StringComparison.Ordinal)
                || string.Equals(name, nameof(Outcome53.Outcome.PatientLoses), StringComparison.Ordinal)
                || string.Equals(name, "patient_lives", StringComparison.Ordinal)
                || string.Equals(name, "patient_loses", StringComparison.Ordinal);
        }

        private static bool TryReadString(JsonNode row, string key, int i, List<string> errors, out string value)
        {
            value = null;
            if (!row.TryGet(key, out JsonNode n) || n.Kind != JsonNodeKind.String)
            {
                errors.Add($"{FileName}:rows[{i}] 缺字符串 {key}(实得 " +
                           $"{(n == null ? "缺失" : n.Kind.ToString())})");
                return false;
            }
            value = n.Str;
            return true;
        }

        private static bool TryReadInt(JsonNode row, string key, int i, List<string> errors, out int value)
        {
            value = 0;
            if (!row.TryGet(key, out JsonNode n))
            {
                errors.Add($"{FileName}:rows[{i}] 缺 {key}");
                return false;
            }
            if (n.Kind != JsonNodeKind.Integer)
            {
                errors.Add($"{FileName}:rows[{i}] {key} 须为整数(实得 {n.Kind}={n.RawText})—— " +
                           "浮点字面量结构性硬失败(ADR-006 边界;档序数是整数)");
                return false;
            }
            if (n.Int < 0 || n.Int > int.MaxValue)
            {
                errors.Add($"{FileName}:rows[{i}] {key}={n.Int} 超出 [0, int.MaxValue]");
                return false;
            }
            value = (int)n.Int;
            return true;
        }

        private static uint ReadSchemaVersion(JsonNode root, List<string> errors)
        {
            if (!root.TryGet("schema_version", out JsonNode n))
            {
                errors.Add($"{FileName} 缺 schema_version");
                return 0;
            }
            if (n.Kind != JsonNodeKind.Integer || n.Int < 0 || n.Int > uint.MaxValue)
            {
                errors.Add($"schema_version 须为 u32 域整数(实得 {n.Kind}={n.RawText})");
                return 0;
            }
            uint v = (uint)n.Int;
            if (v != CookedFormat.SchemaVersion)
            {
                errors.Add($"schema_version={v} ≠ 期望 {CookedFormat.SchemaVersion}" +
                           "(源表 schema 与运行时头契约不一致 ⇒ 构建期硬失败)");
                return 0;
            }
            return v;
        }

        private static void RejectUnknownKeys(JsonNode obj, string[] allowed, string where, List<string> errors)
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
