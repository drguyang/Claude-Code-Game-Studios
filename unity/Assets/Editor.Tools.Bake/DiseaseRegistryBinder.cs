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
using DaYiJingCheng.Gameplay.Presentation;   // DiseaseRegistryCookedCodec.ExpectedSchemaVersion(版本同源)
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
        // ⚠️ 2026-10-22 增 `trend_window_ticks` / `trend_eps` —— 趋势窗口是**全局**旋钮
        //    (GDD Tuning §四 :1743-1744;R1.3 的逐病种字段表里没有它们 ⇒ 不逐病种)。
        //    作者态缺省 = 两键可省(缺 = 未配置 = 0),因为数值轮尚未裁定。
        private static readonly string[] RootKeys =
            { "schema_version", "diseases", "trend_window_ticks", "trend_eps" };

        private static readonly string[] DiseaseRowKeys =
        {
            // ── 本件绑定的键 ──
            "id", "key", "kind", "polarity", "severity", "contagion",
            "lethal", "terminal_state", "treatment_difficulty", "recovery_time", "relapse_chance",
            "comorbidity_factor", "seasonal_mod", "age_mod", "gender_mod",
            "occupation_mod", "region_mod", "climate_mod",
            "scale", "trauma_half_life", "natural_progress",
            // ── 2026-10-23 起绑定 ──
            "signs", "relapse",
            // ── 声明合法但本件**不**绑定的键(见 BoundRowKeys 上方注)──
            //   handle      —— **派生**字段,作者态写了也**不读**(GDD `:329`
            //                  「派生而非手填,防表里写 causal、实际没有对因药」)。
            //                  ⚠️ 它与下面几个不同:它的值确实进了条目(见 BindRow 的
            //                  `DiseaseHandle.Derive`),只是**输入不是 JSON 里那个键**。
            //                  故它**不**进 BoundRowKeys —— 那张表记的是「从 JSON 读了哪些键」。
            "zh", "causes", "treatable_by", "handle",
        };

        /// <summary>`signs[]` 词条的键白名单(GDD R1.3 的 `signs[]` + `channel_mask`)。</summary>
        private static readonly string[] SignEntryKeys = { "sign_key", "channel_mask", "appear_at" };

        /// <summary>`relapse` 块的键白名单(GDD R1.3 三字段,GDD 校验 3「同生共死」)。</summary>
        private static readonly string[] RelapseKeys = { "a_rel", "interval", "tau_fall" };

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
            "signs", "relapse",
        };

        /// <summary>
        /// 白名单内、**读取但不进 <see cref="DiseaseRegistryEntry"/>** 的键 —— 走**侧信道**交给消费方。
        /// <para>⚠️ 2026-10-31(`S21-10` · `TD-medcons-20` 结案)新增。</para>
        /// <para><b>为什么不进 entry</b>:本字段的**唯一**消费方是 53 的构建期断言 N-5
        /// (`CostTableBinder`),运行期 9 不需要它。而进 entry ⇒ 进 cooked ⇒ 改载荷布局 +
        /// 升 `schema_version` + 改 codec 三件 —— 为一条**构建期**判据付**运行期**格式代价。
        /// 先例:`treatable_by` 同样「JSON 有、条目不携带」(轴是单一真源)。</para>
        /// <para><b>为什么不计入 <see cref="BindResult.UnboundKeys"/></b>:那张表记的是
        /// 「**被丢弃**的键」。本键没被丢弃 —— 它被读进了
        /// <see cref="BindResult.TerminalRowKeys"/>。把它算进「丢失」会让那张表说谎。</para>
        /// </summary>
        private static readonly string[] SideChannelRowKeys = { "terminal_state" };

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
            /// 全局 <c>W</c>(趋势差分窗口,tick;GDD Tuning §四 <c>:1743</c>)。
            /// <c>0</c> = 未配置(数值轮未裁)⇒ F4 的痊愈支 / 迁延支不求值。
            /// </summary>
            public readonly long TrendWindowTicks;

            /// <summary>
            /// 全局 <c>TREND_EPS</c>(趋势死区,Q16.16 raw;GDD Tuning §四 <c>:1744</c>)。
            /// <c>0</c> = 未配置。
            /// </summary>
            public readonly long TrendEpsRaw;

            /// <summary>
            /// **声明了、但本件没有绑定进 <see cref="DiseaseRegistryEntry"/> 的行键**(去重,Ordinal 排序)。
            /// <para>⚠️ 存在的理由:ADR-014 §三 的未知键门防的是「拼错 ⇒ 静默丢字段」,
            /// 但白名单内的键若**不被绑定**,丢字段就换个方式静默发生 —— 门看不见,
            /// 因为键是合法的。本字段把这种丢失**显式化**,由测试断言其恰等于
            /// <c>KnownUnboundRowKeys</c>:新增一个未绑定的键 ⇒ 断言红。</para>
            /// <para>空集 = 源文件声明的每个键都被绑定了(理想态,当前未达到)。</para>
            /// </summary>
            public readonly List<string> UnboundKeys;

            /// <summary>
            /// **`terminal_state == true` 的行键**(Ordinal 排序;2026-10-31 `S21-10` 新增)。
            /// <para>「终局型」= 病人被**送入**的状态,不是可挂载的病理(例如 `INJ_COMA`
            /// —— 25 的「生命归零」结算产物)。53 的构建期断言 **N-5** 据此拒绝
            /// 「代价表把药害直接指向终局状态」。</para>
            /// <para>⚠️ 本集合走**侧信道**:值不进 <see cref="DiseaseRegistryEntry"/>、
            /// 不进 cooked、运行期不读(唯一消费方 = `CostTableBinder`)。</para>
            /// </summary>
            public readonly List<string> TerminalRowKeys;

            public BindResult(List<DiseaseRegistryEntry> rows, uint schemaVersion,
                              List<string> unboundKeys, List<string> terminalRowKeys,
                              long trendWindowTicks, long trendEpsRaw)
            {
                Rows = rows; SchemaVersion = schemaVersion; UnboundKeys = unboundKeys;
                TerminalRowKeys = terminalRowKeys;
                TrendWindowTicks = trendWindowTicks; TrendEpsRaw = trendEpsRaw;
            }
        }

        /// <summary>绑定 + 校验。任一违例 ⇒ 聚合 <see cref="BakeValidationException"/>。</summary>
        /// <param name="json">`disease_registry.json` 原文。</param>
        /// <param name="causalTreatmentByDiseaseKey">
        /// 「该病种在**处置轴**里是否至少有一条 <c>polarity = causal</c> 的 <c>treatable_by</c> 行」
        /// —— <c>handle</c> 派生的唯一输入(GDD <c>:329</c> 的「∃ 对因处置 ∈ treatable_by」)。
        /// <para><b>null = 未接线</b> ⇒ <c>Handle</c> 留 <c>null</c>(「未声明」,不是「猜一个」)。
        /// 生产路径由 <see cref="DiseaseRegistryBaker.BakeFromRepo"/> 从
        /// <c>disease_action_axis.json</c> 算出来传入 —— 轴是极性**单一真源**(<c>D-9-K</c>)。</para>
        /// <para>⚠️ <b>为什么不从 <c>treatable_by</c> 那个 JSON 键读</b>:GDD <c>:329</c> 明写
        /// 「派生而非手填,防表里写 causal、实际没有对因药」。手填的值**不参与派生** ——
        /// 那个键仍然只被登记为「未绑定」(见 <see cref="BoundRowKeys"/> 上方注)。</para>
        /// </param>
        public static BindResult Bind(
            string json, IReadOnlyDictionary<string, bool> causalTreatmentByDiseaseKey = null)
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

            // ── 全局趋势窗口(W / TREND_EPS,GDD Tuning §四 :1743-1744)──
            //   两键**可省**:缺 = 未配置 = 0(数值轮未裁)。负值 / 奇数 W 由
            //   `ValidateTrendWindow`(R1-36)拒收 —— 与逐病种校验同批跑,同样聚合后抛。
            long trendWindowTicks = ReadInt(root, "trend_window_ticks", errors,
                                            "disease_registry.json 根", 0);
            long trendEpsRaw = 0;
            if (root.TryGet("trend_eps", out JsonNode epsNode) && epsNode.Kind != JsonNodeKind.Null)
            {
                // ⚠️ Fix 字段写**字符串**(ADR-014 §四硬规则 1):`"trend_eps": "0"`。
                //    收到 JSON 数字 = 浮点泄漏路径,ReadFix 会记错而不是宽容转换。
                trendEpsRaw = ReadFix(root, "trend_eps", errors, "disease_registry.json 根").Raw;
            }
            if (errors.Count == 0)
            {
                try
                {
                    RegistrySchemaValidator.ValidateTrendWindow(trendWindowTicks, trendEpsRaw);
                }
                catch (RegistryValidationException ex)
                {
                    errors.Add("R1-36 校验:" + ex.Message);
                }
            }

            var rows = new List<DiseaseRegistryEntry>();
            var unbound = new SortedSet<string>(StringComparer.Ordinal);
            // 2026-10-31(S21-10):侧信道集合 —— `terminal_state == true` 的行键(DiseaseRowKeys 有、
            // BoundRowKeys 无 ⇒ 不值进 entry,由 BindResult.TerminalRowKeys 交给 53 的 N-5)。
            var terminal = new SortedSet<string>(StringComparer.Ordinal);
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
                    DiseaseRegistryEntry e = BindRow(
                        arr.Items[i], $"diseases[{i}]", errors, unbound, terminal, causalTreatmentByDiseaseKey);
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

            return new BindResult(rows, schemaVersion, new List<string>(unbound), new List<string>(terminal),
                                  trendWindowTicks, trendEpsRaw);
        }

        // ── 单行 ────────────────────────────────────────────────────────────

        private static DiseaseRegistryEntry BindRow(JsonNode node, string where, List<string> errors,
                                                    SortedSet<string> unbound, SortedSet<string> terminal,
                                                    IReadOnlyDictionary<string, bool> causalByKey)
        {
            if (node.Kind != JsonNodeKind.Object)
            {
                errors.Add($"{where} 须为对象(实得 {node.Kind})");
                return null;
            }
            RejectUnknownKeys(node, DiseaseRowKeys, where, errors);
            CollectUnboundKeys(node, unbound);

            // ── `terminal_state`(2026-10-31 · `S21-10` · `TD-medcons-20`):终局型标记 ──
            //   ⚠️ **必填**(D-21-6「字段必须在」)。**不默认 false** ——
            //   默认会让「新增一件终局型伤情却忘了标」静默通过 53 的 N-5,
            //   而那正是本字段要堵的形态(承 `TD-sim-03` 的「真空真」教训)。
            //   值不进 entry(见 SideChannelRowKeys 的注),由 BindResult.TerminalRowKeys 侧信道交出。
            bool terminalState = ReadBool(node, "terminal_state", errors, where);

            // ── `handle` 不得手填(GDD `:329`「派生而非手填」的机械表达)──
            //   作者若写了它,**硬失败**而不是静默丢弃:
            //   静默丢弃 = 「表里写 causal、实际没有对因药」的作者**看不到自己的错**,
            //   而那正是 GDD 立这条派生规则要防的事。
            //   ⚠️ 这不是「未知键」—— 它在白名单内,故上面那条门放行它;本门是第二条。
            if (node.TryGet("handle", out JsonNode handFilled) && handFilled.Kind != JsonNodeKind.Null)
                errors.Add(
                    $"{where}.handle 不得手填(GDD :329)—— 它是**派生字段**:构建期由 " +
                    "`disease_action_axis.json` 的 causal 行 + 照护适用集算出;" +
                    "手填会与轴静默矛盾。请删除该键。");

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

                // ── treatable_by:**刻意不绑**(2026-10-23 口径不变)──
                //   9 GDD R1.3 把它定义在病种条目内,但 story-007 起已拆到
                //   `disease_action_axis.json` 作**单一真源**。若这里也填,轴与条目就构成
                //   第二真源、可分叉 —— 而分叉正是 `D-9-K` 要消灭的形态。
                //   ⇒ 条目的 `TreatableBy` 在真表上**恒 null**;运行期查轴(ITreatableByLookup)。
                TreatableBy = null,

                // ── 2026-10-23 起绑定 ──
                Relapse = BindRelapse(node, where, errors),
                Signs = BindSigns(node, where, errors),
            };

            // ── handle:派生(GDD `:329`),不读 JSON 里那个键 ──
            //   `causalByKey == null` = 轴未接线 ⇒ 留 null(「未声明」),**不猜**。
            //   ⚠️ 猜一个默认值会让「有对因药却报 symptomatic_only」静默通过 ——
            //      那正是 GDD 要防的「表里写 causal、实际没有对因药」的镜像错误。
            //   `DiseaseKey` 读失败时已记错(错误聚合后会抛),此处不再二次派生。
            if (causalByKey != null && e.DiseaseKey != null)
            {
                bool hasCausal = causalByKey.TryGetValue(e.DiseaseKey, out bool v) && v;
                e.Handle = DiseaseHandle.Derive(e.DiseaseKey, hasCausal);
            }

            // natural_progress 子块
            if (node.TryGet("natural_progress", out JsonNode np))
                e.Curve = BindCurve(np, $"{where}.natural_progress", errors);
            else
                errors.Add($"{where}.natural_progress 缺失(R1.2 铁律:必须存在且可序列化)");

            // ── R1-23(2026-10-31 新增):终局型只能是伤情 ──
            //   「终局状态」= 病人被**送入**的状态(如 `INJ_COMA` = 25 的生命归零结算产物),
            //   它没有自己的病程起点,故不可能是一个「病种」或「中毒」。
            //   ⚠️ 放在这里而不是 `RegistrySchemaValidator`:后者只看得见 `DiseaseRegistryEntry`,
            //      而 `terminal_state` 刻意不进 entry(见 SideChannelRowKeys 的注)。
            if (terminalState)
            {
                if (e.Kind != DiseaseKind.Injury)
                    errors.Add(
                        $"{where}.terminal_state = true 但 kind = {e.Kind} —— " +
                        "R1-23:终局型只能是伤情(`injury`);病种 / 中毒都不是「被送入的状态」");
                else
                    terminal.Add(e.DiseaseKey);
            }

            return e;
        }

        /// <summary>
        /// 绑 <c>signs[]</c>(2026-10-23 起)。**缺键 / <c>null</c> ⇒ 返回 <c>null</c>**。
        /// </summary>
        /// <remarks>
        /// <para><b>为什么「缺键」与「空数组」要区分</b>:GDD R1.3 写「可为空集」,
        /// 而空集是**豁免类**病种的合法形态(<c>:291</c>)。但「作者没写这一栏」与
        /// 「作者明确写了空」不是同一件事 —— 前者是未声明(<c>null</c>,R1-29 结构性跳过),
        /// 后者是「本病的体征不走 9」(空数组,过 R1-29)。本方法保持这个区分。</para>
        ///
        /// <para><b>⚠️ 空数组当前仍是全表现状</b>:GDD <c>:508-513</c> 的 B6 裁定说
        /// 「9 的 <c>signs[]</c> 不该全空」,而病种 → 体征的映射**在仓库里不存在**
        /// (归 8 的词表 + 数值轮)。本方法只提供**绑定能力**,不生成内容 ——
        /// 内容缺失是数据缺口,不是本件的缺陷。</para>
        /// </remarks>
        private static SignEntry[] BindSigns(JsonNode node, string where, List<string> errors)
        {
            if (!node.TryGet("signs", out JsonNode arr) || arr.Kind == JsonNodeKind.Null)
                return null;                     // 未声明 —— 与「显式空集」不同
            if (arr.Kind != JsonNodeKind.Array)
            {
                errors.Add($"{where}.signs 须为数组(实得 {arr.Kind})");
                return null;
            }

            var signs = new SignEntry[arr.Items.Count];
            for (int i = 0; i < arr.Items.Count; i++)
            {
                string w = $"{where}.signs[{i}]";
                JsonNode item = arr.Items[i];
                if (item.Kind != JsonNodeKind.Object)
                {
                    errors.Add($"{w} 须为对象(实得 {item.Kind})");
                    continue;
                }
                RejectUnknownKeys(item, SignEntryKeys, w, errors);

                string signKey = ReadString(item, "sign_key", errors, w);
                int channelMask = ReadInt(item, "channel_mask", errors, w);
                // 2026-10-30(F2 投影阈值):JSON 里写**字符串**(ADR-006 §一 —— 数字 token 会经
                //  double/decimal 中转 = 浮点泄漏),由 ReadFix 走 FixParse;域校验在 R1-37。
                Fix appearAt = ReadFix(item, "appear_at", errors, w);
                signs[i] = new SignEntry(signKey, channelMask, appearAt);
            }
            return signs;
        }

        /// <summary>
        /// 绑 <c>relapse</c> 块(2026-10-23 起)。**缺键 / <c>null</c> ⇒ 返回 <c>null</c>**(= 无复发)。
        /// </summary>
        /// <remarks>
        /// <para><b>GDD 校验 3「三字段同生共死」在这里的形态</b>:块存在 ⇒ 三个键都必须出现
        /// (<see cref="ReadFix"/> / <see cref="ReadInt"/> 无 fallback ⇒ 缺键即记错)。
        /// 值域(全 <c>&gt; 0</c>)由 R1-26 在 <c>Validate</c> 里查 —— 本方法只管**形状**,
        /// 不重复判值域(同一判据两份实现会分叉)。</para>
        ///
        /// <para><b>⚠️ 当前真表零 <c>relapse</c> 块</b>:疟疾需要它,而三字段
        /// (<c>a_rel</c> / <c>interval</c> / <c>tau_rel_fall</c>)的量级归**数值轮**
        /// (<c>assets/data/disease_registry.json</c> 的 <c>_relapseNote</c>)。
        /// 本方法只提供绑定能力;数据缺口见文件尾 §未实现登记。</para>
        /// </remarks>
        private static RelapseCurve BindRelapse(JsonNode node, string where, List<string> errors)
        {
            if (!node.TryGet("relapse", out JsonNode block) || block.Kind == JsonNodeKind.Null)
                return null;                     // 无复发块 —— 合法形态(R1.3)
            if (block.Kind != JsonNodeKind.Object)
            {
                errors.Add($"{where}.relapse 须为对象(实得 {block.Kind})");
                return null;
            }
            string w = $"{where}.relapse";
            RejectUnknownKeys(block, RelapseKeys, w, errors);

            return new RelapseCurve
            {
                ARel = ReadFix(block, "a_rel", errors, w),
                Interval = ReadInt(block, "interval", errors, w),
                TauFall = ReadFix(block, "tau_fall", errors, w),
            };
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
            // ⚠️ **与读方同源**(ADR-014 §五:版本不匹配硬失败)—— 2026-10-23 补。
            //    只判 u32 域会让源写 `1` 时烘焙**成功**、产物在**运行期装载**才抛 E-13 ——
            //    失败点从构建期漏到运行期。实测过:布局升到 2 之后,四份合成源仍写 1,
            //    `Materialize` 的真往返当场红在 `Materialize` 里,而**裸烘焙**(产物漂移门走的那条)
            //    一路绿 —— 也就是说没有本条时,盘上会出现一份**读方拒收**的产物而构建不报错。
            //    先例:`DiagnosisNegativeConfidenceBinder.cs:208` 同法。
            if ((uint)n.Int != DiseaseRegistryCookedCodec.ExpectedSchemaVersion)
            {
                errors.Add(
                    $"disease_registry.json schema_version={(uint)n.Int} ≠ 读方认领 " +
                    $"{DiseaseRegistryCookedCodec.ExpectedSchemaVersion}" +
                    " —— 版本不匹配,拒收(ADR-014 §五;版本史见 DiseaseRegistryCookedCodec)");
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
                // 2026-10-31:侧信道键**不算丢失** —— 它被读进了 BindResult.TerminalRowKeys。
                if (!bound)
                    for (int i = 0; i < SideChannelRowKeys.Length; i++)
                        if (string.Equals(SideChannelRowKeys[i], key, StringComparison.Ordinal)) { bound = true; break; }
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
//   ① ~~`handle` 派生逻辑**不存在**~~ —— **2026-10-23 已兑现**:派生落在
//      `Sim/DiseaseHandle.Derive`(闭集四值 + 判定序 `causal → care → none → symptomatic_only`,
//      其中 `care` 分支直接调 `CareLeverApplicability.AppliesToKey`,不另存第二份适用集)。
//      本 binder 在 `BindRow` 末尾调它,输入由 `Bind` 的 `causalTreatmentByDiseaseKey` 参数带进来。
//      ⚠️ **`causalTreatmentByDiseaseKey == null` ⇒ Handle 留 null**(「未接线」≠「猜一个」)——
//      生产路径 `BakeFromRepo` 恒传入,故真表恒有值;测试直调 `Bind(json)` 时才为 null。
//   ② ~~`treatable_by` 拆到轴文件 ⇒ `handle` 派生输入在本表内不可得~~ —— 仍在轴文件里,
//      但**跨文件不再是障碍**:`DiseaseRegistryBaker.BakeFromRepo` 同时读两个文件并算好映射传入。
//      ⚠️ **轴文件不进 `ConfigVersion`**(哈希只覆盖 `disease_registry.json`)——
//      轴改而 `handle` 变,产物字节会变但版本号不变;这一处由**产物漂移门**兜
//      (`cooked_drift_all_artifacts_test`),不是本件的事。
//   ③ ~~`signs[]` 归 8 的体征词表,当前为空集~~ —— **2026-10-23 已绑定**:真表 13 行全部
//      显式声明 `signs: []`(空数组 = 合法空集,GDD `:291`);binder 区分 null(未声明)
//      与 `[]`(显式空集)。⚠️ **数据侧的洞仍在**:GDD B6 裁定说「9 的 `signs[]` 不该全空」,
//      而 13 行全是 `[]` ⇒ 病种→体征映射**尚无内容**,归 8 的词表轮,不在本件判据面。
//   ④ ~~`relapse` 块 —— 本表当前只含伤情行,故未绑定~~ —— **2026-10-23 已绑定**:
//      binder 读 `a_rel` / `interval` / `tau_fall` 三字段(缺任一 ⇒ 硬失败,GDD 校验 3「同生共死」)。
//      ⚠️ **真表 13 行仍无一行带 `relapse`** —— 疟疾不在真表内(真表只含 8 伤情 + 5 其他伤情行),
//      且三个复发数值本身**未裁定**(`_relapseNote` 登记在案)⇒ 编码能力已就位,数据待裁。
//   ⑤ `sigma` —— 全局默认值不存在(数值轮),逐病种覆盖亦无。**状态未变**。
