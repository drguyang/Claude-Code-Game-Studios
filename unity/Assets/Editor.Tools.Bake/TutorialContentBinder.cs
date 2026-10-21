// S17-3 · story-001 —— 48 教学内容(assets/data/48_tutorial_content.json)的阶段 2 绑定器。
// **本件 = 该数据集的唯一构建期校验点**,六项校验**全为硬失败**(聚合 throw,非告警)。
//
// 权威来源:
//   GDD design/gdd/tutorial-and-onboarding.md §规则八(六项校验逐条)
//   ADR-014 §三(阶段 2:已知键白名单 · 聚合 throw · 禁数值经 double 中转)
//   ADR-024 §①(predicateKind 白名单真源 = registry 的 35 支 Kind)
//   ADR-010 §六(archive 步 `Checkpoint` 例外)
//   docs/architecture/control-manifest.md:571-590(Cross-Cutting 之八 —— 本次实现的主要落点)
//
// ═══ 比较器纪律(Cross-Cutting 之八 · BLOCKING)════════════════════════════
// ① **承载判定语义的谓词一律显式比较器**:本文件的 `IndexOf` 必带
//    `StringComparison.OrdinalIgnoreCase`,`Equals` 必带 `StringComparison.Ordinal`,
//    **禁默认重载**(默认 = culture-sensitive,在 tr-TR 等文化下会改变判定)。
// ② **判定真源 = 结构化计数**:两条文本扫描(病名 / 显式指令)返回
//    <see cref="TutorialTextScanResult"/>,失败与否由 `HitCount &gt; 0` **派生**;
//    **不**按「命中串是否以某标记字符开头」判真假(S12-2 的真 bug 形态:emoji 前缀比较失效)。
// ③ 与 S12-2 的差异:那里是「前缀判定」,本例是「子串扫描 + 整数计数」——
//    字符串只作**人读定位**(Hits 里报条目 id / 字段 / 词条),不参与真假。
//
// ═══ 外部词表的降级路径(本 story 明写)════════════════════════════════
// 三项外部键空间(病名闭集 / 44 的 cueId 集 / 13 的 clipKey 集)都由**上游并行产出**:
//   · 病名闭集(9):`assets/data/` 下实测**不存在**独立词表文件 ⇒ 第 ④ 项 NOT-RUN
//   · cueId 集(44):`teach_*` 四个键在既有 `audio_events.json` 中**尚未登记** ⇒ 外部键半边 NOT-RUN
//   · clipKey 集(13):全库无示范片段表 ⇒ 外部键半边 NOT-RUN
// 故三者均经**注入接口**取表;**缺表 ⇒ 结构化「未就绪」哨兵,不崩、不抛、不用弱化词表凑绿**
// (本仓「自造断言顶替 AC 语义层」形态之一)。缺表状态下对应的 AC 子项记 **NOT-RUN**。
//
// ⚠️ 本 schema **无 `Fix` 字段**(48 是呈现系统,零数值 —— GDD §Formulas 首行),
//   故不经 `FixParse`;文本 / id / 枚举名全部走 String token,禁 Float token(结构性失败)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.EditorTools.Bake
{

    /// <summary>
    /// 外部媒体键空间的**来源接缝**(校验规则 2 的外部半边)。
    /// <para>cueId 集 = 44 的音频事件表(<c>audio_events</c> 烘成 <c>.cooked</c>);clipKey 集 = 13 的示范片段表。
    /// 两个集合都可缺席(生产路径当前**均**缺席 —— 见文件头降级路径段)。</para>
    /// <para>⚠️ 缺席时外部半边记 **NOT-RUN**(<see cref="TutorialContentBinder.BindResult.CueIdTableReady"/> /
    /// <see cref="TutorialContentBinder.BindResult.ClipKeyTableReady"/> = false),内部引用完整性照常执行。</para>
    /// </summary>
    public interface ITutorialMediaKeySource
    {
        /// <summary>尝试取得 44 的 cueId 集。</summary>
        /// <param name="cueIds">成功时为闭集;失败时为 null。</param>
        /// <returns>true = 表可用(false = 「依赖表未就绪」)。</returns>
        bool TryGetCueIds(out IReadOnlyCollection<string> cueIds);

        /// <summary>尝试取得 13 的示范 clipKey 集。</summary>
        /// <param name="clipKeys">成功时为闭集;失败时为 null。</param>
        /// <returns>true = 表可用(false = 「依赖表未就绪」)。</returns>
        bool TryGetClipKeys(out IReadOnlyCollection<string> clipKeys);
    }

    /// <summary>一条扫描命中(人读定位用;**不参与真假判定** —— 真假由计数派生)。</summary>
    public readonly struct TutorialScanHit
    {
        /// <summary>所属条目 id(step id / narration id / demo id / paper id)。</summary>
        public readonly string EntryId;

        /// <summary>字段路径(如 <c>narration[].text</c>)。</summary>
        public readonly string Field;

        /// <summary>命中的词条(词族表里的原文 —— 报告能指出「撞了哪个词」)。</summary>
        public readonly string Token;

        /// <summary>首个命中位置(字符下标)。</summary>
        public readonly int Index;

        public TutorialScanHit(string entryId, string field, string token, int index)
        {
            EntryId = entryId; Field = field; Token = token; Index = index;
        }

        /// <summary>可读形(错误消息 / 测试断言用)。</summary>
        public override string ToString() => $"{EntryId} · {Field} → 词条「{Token}」@ {Index}";
    }

    /// <summary>文本扫描结果(**结构化**;Cross-Cutting 之八 ② 的落地形态)。</summary>
    public readonly struct TutorialTextScanResult
    {
        /// <summary>词典是否就绪 + 命中与否(三态)。</summary>
        public readonly TutorialDiseaseScanOutcome Outcome;

        /// <summary>命中条目数(**判真的唯一依据**;0 = 通过)。</summary>
        public readonly int HitCount;

        /// <summary>命中明细(人读;顺序 = 扫描序,确定性)。</summary>
        public readonly IReadOnlyList<TutorialScanHit> Hits;

        public TutorialTextScanResult(TutorialDiseaseScanOutcome outcome, IReadOnlyList<TutorialScanHit> hits)
        {
            Outcome = outcome;
            Hits = hits ?? Array.Empty<TutorialScanHit>();
            HitCount = Hits.Count;
        }

        /// <summary>声明式构造(测试与生产共用)。</summary>
        public static TutorialTextScanResult Of(
            TutorialDiseaseScanOutcome outcome, IReadOnlyList<TutorialScanHit> hits)
            => new TutorialTextScanResult(outcome, hits);
    }

    /// <summary><c>steps[]</c> 的一行(绑定后)。</summary>
    public readonly struct TutorialStep
    {
        /// <summary>步 id ∈ 六步闭集。</summary>
        public readonly string Id;
        /// <summary>步名(仅文档,不渲染 —— 48 零 UI)。</summary>
        public readonly string Title;
        /// <summary>判据 Kind 名(或 archive 步的 <c>Checkpoint</c>)。</summary>
        public readonly string PredicateKind;
        /// <summary>口述条目 id 引用(可为空数组)。</summary>
        public readonly IReadOnlyList<string> NarrationIds;
        /// <summary>示范条目 id 引用。</summary>
        public readonly IReadOnlyList<string> DemonstrationIds;
        /// <summary>纸条目 id 引用。</summary>
        public readonly IReadOnlyList<string> PaperIds;
        /// <summary>实现注记(不渲染)。</summary>
        public readonly string Note;

        public TutorialStep(string id, string title, string predicateKind,
            IReadOnlyList<string> narrationIds, IReadOnlyList<string> demonstrationIds,
            IReadOnlyList<string> paperIds, string note)
        {
            Id = id; Title = title; PredicateKind = predicateKind;
            NarrationIds = narrationIds; DemonstrationIds = demonstrationIds;
            PaperIds = paperIds; Note = note;
        }
    }

    /// <summary><c>narration[]</c> 的一行(绑定后)。</summary>
    public readonly struct TutorialNarration
    {
        /// <summary>条目 id(step 引用它)。</summary>
        public readonly string Id;
        /// <summary>44 语音 cue 键(字幕语义归 44,承 AC-44-15)。</summary>
        public readonly string CueId;
        /// <summary>步内次序。</summary>
        public readonly int Order;
        /// <summary>口述文本本体(自含;≤170 字符)。</summary>
        public readonly string Text;

        public TutorialNarration(string id, string cueId, int order, string text)
        {
            Id = id; CueId = cueId; Order = order; Text = text;
        }
    }

    /// <summary><c>demonstrations[]</c> 的一行(绑定后)。</summary>
    public readonly struct TutorialDemonstration
    {
        /// <summary>条目 id。</summary>
        public readonly string Id;
        /// <summary>所属步 id。</summary>
        public readonly string StepId;
        /// <summary>示范者 ∈ {mentor}。</summary>
        public readonly string Actor;
        /// <summary>示范 clip 键(clip 语义归 13 / 10)。</summary>
        public readonly string ClipKey;

        public TutorialDemonstration(string id, string stepId, string actor, string clipKey)
        {
            Id = id; StepId = stepId; Actor = actor; ClipKey = clipKey;
        }
    }

    /// <summary><c>papers[]</c> 的一行(绑定后)。</summary>
    public readonly struct TutorialPaper
    {
        /// <summary>条目 id(6 侧布置引用它 ⇒ **跨系统契约**,纸道具 id 须稳定)。</summary>
        public readonly string Id;
        /// <summary>所属步 id。</summary>
        public readonly string StepId;
        /// <summary>纸名 / 图题(≤2 行)。</summary>
        public readonly string Title;
        /// <summary>一句话说明(≤170 字符)。</summary>
        public readonly string Body;
        /// <summary>出版纵横比(W:H)。</summary>
        public readonly string AspectRatio;
        /// <summary>插图素材键(归 art;48 零素材)。</summary>
        public readonly string IllustrationKey;

        public TutorialPaper(string id, string stepId, string title, string body,
            string aspectRatio, string illustrationKey)
        {
            Id = id; StepId = stepId; Title = title; Body = body;
            AspectRatio = aspectRatio; IllustrationKey = illustrationKey;
        }
    }

    /// <summary>48 教学内容 schema 的阶段 2 绑定器(六项校验,**聚合 throw**)。
    /// <example><code>
    /// var bound = TutorialContentBinder.Bind(json, diseaseNames: null, mediaKeys: null);
    /// if (bound.DiseaseScan.Outcome == TutorialDiseaseScanOutcome.TableNotReady)
    ///     /* AC 第 4 条的第 ④ 项记 NOT-RUN,不勾 */;
    /// </code></example>
    /// </summary>
    public static class TutorialContentBinder
    {
        /// <summary>绑定结果:内容 + 两项扫描的结构化结果 + 三张外部表的就绪状态(测试直用)。</summary>
        public readonly struct BindResult
        {
            /// <summary>源 <c>schemaVersion</c>。</summary>
            public readonly uint SchemaVersion;
            /// <summary>六步(源文件序;写方按 id 闭集序重排)。</summary>
            public readonly IReadOnlyList<TutorialStep> Steps;
            /// <summary>口述条目。</summary>
            public readonly IReadOnlyList<TutorialNarration> Narration;
            /// <summary>示范条目。</summary>
            public readonly IReadOnlyList<TutorialDemonstration> Demonstrations;
            /// <summary>纸条目。</summary>
            public readonly IReadOnlyList<TutorialPaper> Papers;
            /// <summary>第 ④ 项(病名闭集扫描)结果 —— 可能为 <c>TableNotReady</c>。</summary>
            public readonly TutorialTextScanResult DiseaseScan;
            /// <summary>第 ⑤ 项(显式指令扫描)结果。</summary>
            public readonly TutorialTextScanResult InstructionScan;
            /// <summary>44 cueId 表是否就绪(② 的外部半边;false ⇒ 该半边 NOT-RUN)。</summary>
            public readonly bool CueIdTableReady;
            /// <summary>13 clipKey 表是否就绪(② 的外部半边;false ⇒ 该半边 NOT-RUN)。</summary>
            public readonly bool ClipKeyTableReady;

            /// <summary>内容条目总数(6 步 + 4 口述 + 6 示范 + 1 纸 = 17;GDD 内容债口径)。</summary>
            public int ContentItemCount =>
                Steps.Count + Narration.Count + Demonstrations.Count + Papers.Count;

            public BindResult(uint schemaVersion,
                IReadOnlyList<TutorialStep> steps, IReadOnlyList<TutorialNarration> narration,
                IReadOnlyList<TutorialDemonstration> demonstrations, IReadOnlyList<TutorialPaper> papers,
                TutorialTextScanResult diseaseScan, TutorialTextScanResult instructionScan,
                bool cueIdTableReady, bool clipKeyTableReady)
            {
                SchemaVersion = schemaVersion; Steps = steps; Narration = narration;
                Demonstrations = demonstrations; Papers = papers;
                DiseaseScan = diseaseScan; InstructionScan = instructionScan;
                CueIdTableReady = cueIdTableReady; ClipKeyTableReady = clipKeyTableReady;
            }
        }

        /// <summary>六项校验的枚举名(测试报点名用 —— 与 GDD 规则八编号一一对应)。</summary>
        public static readonly string[] CheckNames =
        {
            "① JSON 合法", "② 引用完整性", "③ 长度约束", "④ 病名闭集扫描", "⑤ 显式指令扫描",
            "⑥ predicateKind 白名单",
        };

        /// <summary>绑定 + 六项校验(任一违例 ⇒ 聚合 <see cref="BakeValidationException"/>)。
        /// <para>⚠️ 三张**外部表缺席不是违例**(各自记 NOT-RUN);六项校验**内部**违例一律 throw。</para></summary>
        /// <param name="json">源文本。</param>
        /// <param name="diseaseNames">病名闭集来源;null = 未就绪(⇒ 第 ④ 项 NOT-RUN)。</param>
        /// <param name="mediaKeys">cueId / clipKey 表来源;null = 未就绪(⇒ ② 的外部半边 NOT-RUN)。</param>
        public static BindResult Bind(string json, ITutorialDiseaseNameSource diseaseNames = null,
            ITutorialMediaKeySource mediaKeys = null)
        {
            // ── ① JSON 合法 ────────────────────────────────────────────────
            if (!JsonStage1Lexer.TryParse(json, out JsonNode root, out string lexErr))
                throw new BakeValidationException(new List<string>
                {
                    $"[①] {TutorialContentSchema.SourceFileName} 阶段1 词法失败:{lexErr}",
                });

            if (root.Kind != JsonNodeKind.Object)
                throw new BakeValidationException(new List<string>
                {
                    $"[①] {TutorialContentSchema.SourceFileName} 根须为对象(实得 {root.Kind})",
                });

            var errors = new List<string>();
            RejectUnknownKeys(root, TutorialContentSchema.RootKeys, "根", errors);
            uint schemaVersion = ReadSchemaVersion(root, errors);
            ValidateMeta(root, errors);

            // 四个分区必须齐(缺区 ⇒ 引用完整性不可做;早 throw = 唯一豁免点,同 DelayTableBinder 口径)
            if (!TryRequireArray(root, "steps", errors, out JsonNode stepsNode) ||
                !TryRequireArray(root, "narration", errors, out JsonNode narrNode) ||
                !TryRequireArray(root, "demonstrations", errors, out JsonNode demoNode) ||
                !TryRequireArray(root, "papers", errors, out JsonNode paperNode))
                throw new BakeValidationException(errors);

            var steps = ParseSteps(stepsNode, errors);
            var narration = ParseNarration(narrNode, errors);
            var demos = ParseDemonstrations(demoNode, errors);
            var papers = ParsePapers(paperNode, errors);

            // ── ⑥ predicateKind 白名单(行内已做)+ 步 id 闭集恰 6 员 ─────────
            ValidateStepIdClosedSet(steps, errors);

            // ── ② 引用完整性(零孤儿 / 零悬垂;含外部键半边)────────────────
            ValidateReferences(steps, narration, demos, papers, errors);
            ValidateExternalMediaKeys(narration, demos, mediaKeys, errors,
                out bool cueReady, out bool clipReady);

            // ── ③ 长度约束(长度 + 自含)──────────────────────────────────
            ValidateLengths(narration, papers, errors);

            // ── ④ 病名闭集扫描(结构化计数;词表缺席 = NOT-RUN)────────────
            var diseaseScan = ScanDiseaseNames(narration, papers, diseaseNames, errors);

            // ── ⑤ 显式指令扫描(结构化计数)────────────────────────────────
            var instructionScan = ScanInstructionTokens(narration, papers, errors);

            if (errors.Count > 0) throw new BakeValidationException(errors);

            return new BindResult(schemaVersion, steps, narration, demos, papers,
                                  diseaseScan, instructionScan, cueReady, clipReady);
        }

        // ════════════════════════════════════════════════════════════════
        //  ① 顶层 / meta / schemaVersion
        // ════════════════════════════════════════════════════════════════

        private static uint ReadSchemaVersion(JsonNode root, List<string> errors)
        {
            if (!root.TryGet("schemaVersion", out JsonNode n))
            {
                errors.Add("[①] 缺 schemaVersion(int;GDD 规则八表首行)");
                return 0;
            }
            if (n.Kind != JsonNodeKind.Integer || n.Int < 0 || n.Int > uint.MaxValue)
            {
                errors.Add($"[①] schemaVersion 须为 u32 域整数(实得 {n.Kind}={n.RawText})");
                return 0;
            }
            uint v = (uint)n.Int;
            if (v != TutorialContentSchema.ExpectedSchemaVersion)
            {
                errors.Add($"[①] schemaVersion={v} ≠ 期望 {TutorialContentSchema.ExpectedSchemaVersion}" +
                           "(源 schema 与运行时头契约不一致 ⇒ 构建期硬失败;破坏性变更须版本化)");
                return 0;
            }
            return v;
        }

        private static void ValidateMeta(JsonNode root, List<string> errors)
        {
            if (!root.TryGet("meta", out JsonNode meta) || meta.Kind != JsonNodeKind.Object)
            {
                errors.Add($"[①] 根 meta 须为对象(实得 {(meta == null ? "缺失" : meta.Kind.ToString())})");
                return;
            }
            RejectUnknownKeys(meta, TutorialContentSchema.MetaKeys, "meta", errors);
            if (meta.TryGet("owners", out JsonNode owners) && owners.Kind == JsonNodeKind.Object)
                RejectUnknownKeys(owners, TutorialContentSchema.MetaOwnerKeys, "meta.owners", errors);
        }

        private static bool TryRequireArray(JsonNode root, string key, List<string> errors, out JsonNode node)
        {
            if (!root.TryGet(key, out node) || node.Kind != JsonNodeKind.Array)
            {
                errors.Add($"[①] 缺根数组 {key}[]" +
                           $"{(node == null ? "(缺失 —— 空引用须显式写 [])" : $"(实得 {node.Kind})")}");
                node = null;
                return false;
            }
            return true;
        }

        // ════════════════════════════════════════════════════════════════
        //  steps[]
        // ════════════════════════════════════════════════════════════════

        private static List<TutorialStep> ParseSteps(JsonNode node, List<string> errors)
        {
            var list = new List<TutorialStep>(node.Items.Count);
            for (int i = 0; i < node.Items.Count; i++)
            {
                string where = $"steps[{i}]";
                if (node.Items[i].Kind != JsonNodeKind.Object)
                {
                    errors.Add($"[①] {where} 须为对象(实得 {node.Items[i].Kind})");
                    continue;
                }
                JsonNode row = node.Items[i];
                RejectUnknownKeys(row, TutorialContentSchema.StepKeys, where, errors);

                if (!TryReadString(row, "id", where, errors, out string id)) continue;
                where = $"step「{id}」({where})";

                // AC 第 6 条:id ∈ 六步闭集(闭集外 = ⑥ 失败;定位到 id 本身)
                if (!TutorialContentSchema.IsStepId(id))
                {
                    errors.Add($"[⑥] {where} id「{id}」∉ 六步闭集 " +
                               "{" + string.Join(", ", TutorialContentSchema.StepIds) + "}");
                }

                TryReadString(row, "title", where, errors, out string title);
                TryReadString(row, "note", where, errors, out string note);
                ReadPredicateKind(row, id, where, errors, out string predicateKind);

                var narr = ReadStringArray(row, "narration", where, errors);
                var demoIds = ReadStringArray(row, "demonstrations", where, errors);
                var paperIds = ReadStringArray(row, "papers", where, errors);

                list.Add(new TutorialStep(id, title, predicateKind, narr, demoIds, paperIds, note));
            }
            return list;
        }

        /// <summary>⑥ predicateKind 白名单:registry 全集 ∪ {Checkpoint},且 Checkpoint 仅挂 archive 步。</summary>
        private static void ReadPredicateKind(JsonNode row, string stepId, string where,
            List<string> errors, out string predicateKind)
        {
            predicateKind = null;
            if (!row.TryGet("predicateKind", out JsonNode n) || n.Kind != JsonNodeKind.String)
            {
                errors.Add($"[⑥] {where} 缺字符串 predicateKind" +
                           $"{(n == null ? "(缺失)" : $"(实得 {n.Kind})")}");
                return;
            }
            predicateKind = n.Str;

            if (string.Equals(predicateKind, TutorialContentSchema.CheckpointPredicateLiteral,
                              StringComparison.Ordinal))
            {
                // ⚠️ 例外**只**给 archive 步:7a 存档头写入不是 SimEvent(ADR-010 §六 / GDD F-48.1 ⑤)。
                //    挂到其它步 = 该步「完成」却无任何可观察流事件 ⇒ 判据永不成立(静默失效)。
                if (!string.Equals(stepId, TutorialContentSchema.CheckpointOnlyStepId, StringComparison.Ordinal))
                {
                    errors.Add($"[⑥] {where} predicateKind = 字面「" +
                               TutorialContentSchema.CheckpointPredicateLiteral +
                               $"」只允许挂在 {TutorialContentSchema.CheckpointOnlyStepId} 步" +
                               "(ADR-010 §六:存档头 Checkpoint 不是 SimEvent.Kind,别步挂它 = 判据永不可达)");
                }
                return;
            }

            if (!TutorialContentSchema.IsRegistryKind(predicateKind))
            {
                errors.Add($"[⑥] {where} predicateKind「{predicateKind}」∉ registry 的 " +
                           $"{TutorialContentSchema.RegistryKindNames.Count} 支 SimEvent.Kind" +
                           $" ∪ {{\"{TutorialContentSchema.CheckpointPredicateLiteral}\"}}" +
                           "(ADR-024 §① 真源 = design/registry/entities.yaml)");
            }
        }

        private static void ValidateStepIdClosedSet(List<TutorialStep> steps, List<string> errors)
        {
            // AC 第 6 条:恰 6 员,多 / 少 = 失败(与「逐行 ∈ 闭集」互补:重复 id 会让多出的行蒙混)
            if (steps.Count != TutorialContentSchema.StepCount)
            {
                errors.Add($"[⑥] steps[] 须恰 {TutorialContentSchema.StepCount} 条(实得 {steps.Count})" +
                           " —— 观察步并入 ① 锚(observe 与 judge 共锚 CaseOpened),闭集固定六员");
            }

            var seen = new List<string>();
            for (int i = 0; i < steps.Count; i++)
            {
                bool dup = false;
                for (int j = 0; j < seen.Count; j++)
                    if (string.Equals(seen[j], steps[i].Id, StringComparison.Ordinal)) { dup = true; break; }
                if (dup) errors.Add($"[⑥] steps[] id「{steps[i].Id}」重复(闭集内每员恰一行)");
                else seen.Add(steps[i].Id);
            }

            var missing = new List<string>();
            for (int i = 0; i < TutorialContentSchema.StepIds.Count; i++)
            {
                bool found = false;
                for (int j = 0; j < seen.Count; j++)
                    if (string.Equals(seen[j], TutorialContentSchema.StepIds[i], StringComparison.Ordinal))
                    { found = true; break; }
                if (!found) missing.Add(TutorialContentSchema.StepIds[i]);
            }
            if (missing.Count > 0)
                errors.Add("[⑥] steps[] 缺闭集成员:" + string.Join(", ", missing) +
                           "(AC 第 6 条:闭集 = 恰好 6 员,多 / 少 = 校验失败)");
        }

        // ════════════════════════════════════════════════════════════════
        //  narration[] / demonstrations[] / papers[]
        // ════════════════════════════════════════════════════════════════

        private static List<TutorialNarration> ParseNarration(JsonNode node, List<string> errors)
        {
            var list = new List<TutorialNarration>(node.Items.Count);
            for (int i = 0; i < node.Items.Count; i++)
            {
                string where = $"narration[{i}]";
                if (node.Items[i].Kind != JsonNodeKind.Object)
                {
                    errors.Add($"[①] {where} 须为对象(实得 {node.Items[i].Kind})");
                    continue;
                }
                JsonNode row = node.Items[i];
                RejectUnknownKeys(row, TutorialContentSchema.NarrationKeys, where, errors);

                if (!TryReadString(row, "id", where, errors, out string id)) continue;
                where = $"口述「{id}」({where})";
                TryReadString(row, "cueId", where, errors, out string cueId);
                int order = ReadInt(row, "order", where, errors);
                TryReadString(row, "text", where, errors, out string text);
                list.Add(new TutorialNarration(id, cueId, order, text));
            }
            return list;
        }

        private static List<TutorialDemonstration> ParseDemonstrations(JsonNode node, List<string> errors)
        {
            var list = new List<TutorialDemonstration>(node.Items.Count);
            for (int i = 0; i < node.Items.Count; i++)
            {
                string where = $"demonstrations[{i}]";
                if (node.Items[i].Kind != JsonNodeKind.Object)
                {
                    errors.Add($"[①] {where} 须为对象(实得 {node.Items[i].Kind})");
                    continue;
                }
                JsonNode row = node.Items[i];
                RejectUnknownKeys(row, TutorialContentSchema.DemonstrationKeys, where, errors);

                if (!TryReadString(row, "id", where, errors, out string id)) continue;
                where = $"示范「{id}」({where})";
                TryReadString(row, "step", where, errors, out string stepId);
                TryReadString(row, "clipKey", where, errors, out string clipKey);
                // note = 作者注记:必填且非空(内容债以条目计数),**刻意不带入模型 / 不入 cooked**
                //   —— 48 零 UI,注记是作者态元数据。🔁 2026-10-21(评审 E2):原写法取到值后
                //   只剩一句空语句,是把「校验但不使用」伪装成「使用了」;改 `out _` 让意图成为语法。
                TryReadString(row, "note", where, errors, out _);

                if (!TryReadString(row, "actor", where, errors, out string actor)) continue;
                if (!TutorialContentSchema.IsDemonstrationActor(actor))
                    errors.Add($"[①] {where} actor「{actor}」∉ 示范者闭集 " +
                               "{" + string.Join(", ", TutorialContentSchema.DemonstrationActors) + "}");

                list.Add(new TutorialDemonstration(id, stepId, actor, clipKey));
            }
            return list;
        }

        private static List<TutorialPaper> ParsePapers(JsonNode node, List<string> errors)
        {
            var list = new List<TutorialPaper>(node.Items.Count);
            for (int i = 0; i < node.Items.Count; i++)
            {
                string where = $"papers[{i}]";
                if (node.Items[i].Kind != JsonNodeKind.Object)
                {
                    errors.Add($"[①] {where} 须为对象(实得 {node.Items[i].Kind})");
                    continue;
                }
                JsonNode row = node.Items[i];
                RejectUnknownKeys(row, TutorialContentSchema.PaperKeys, where, errors);

                if (!TryReadString(row, "id", where, errors, out string id)) continue;
                where = $"纸「{id}」({where})";
                TryReadString(row, "step", where, errors, out string stepId);
                TryReadString(row, "title", where, errors, out string title);
                TryReadString(row, "body", where, errors, out string body);
                TryReadString(row, "illustrationKey", where, errors, out string illustrationKey);
                // note:同 demonstrations —— 必填校验,刻意不入模型 / 不入 cooked(评审 E2)。
                TryReadString(row, "note", where, errors, out _);

                if (!TryReadString(row, "aspectRatio", where, errors, out string aspectRatio)) continue;
                if (!TutorialContentSchema.TryParseAspectRatio(aspectRatio, out _, out _))
                    errors.Add($"[①] {where} aspectRatio「{aspectRatio}」非法(须 W:H 形,两段正整数;" +
                               "近景纸与道具纸一致性判据归 6 布置轮 OQ-C2)");

                list.Add(new TutorialPaper(id, stepId, title, body, aspectRatio, illustrationKey));
            }
            return list;
        }

        // ════════════════════════════════════════════════════════════════
        //  ② 引用完整性(零孤儿 / 零悬垂)+ 外部键半边
        // ════════════════════════════════════════════════════════════════

        private static void ValidateReferences(
            List<TutorialStep> steps, List<TutorialNarration> narration,
            List<TutorialDemonstration> demos, List<TutorialPaper> papers, List<string> errors)
        {
            List<string> stepIds = CollectStepIds(steps);
            List<string> narrIds = CollectIds("口述", narration.Count, i => narration[i].Id, errors);
            List<string> demoIds = CollectIds("示范", demos.Count, i => demos[i].Id, errors);
            List<string> paperIds = CollectIds("纸", papers.Count, i => papers[i].Id, errors);

            var narRef = new bool[narration.Count];
            var demoRef = new bool[demos.Count];
            var paperRef = new bool[papers.Count];

            ValidateStepReferences(steps, narrIds, demoIds, paperIds, narRef, demoRef, paperRef, errors);
            ValidateReverseStepFields(steps, stepIds, demos, papers, demoRef, paperRef, errors);
            ValidateNoOrphans(narration, demos, papers, narRef, demoRef, paperRef, errors);
        }

        /// <summary>step 侧 id 去重集(保序;ordinal)。</summary>
        private static List<string> CollectStepIds(List<TutorialStep> steps)
        {
            var stepIds = new List<string>();
            for (int i = 0; i < steps.Count; i++)
                if (!ContainsOrdinal(stepIds, steps[i].Id)) stepIds.Add(steps[i].Id);
            return stepIds;
        }

        /// <summary>②-a 悬垂:step 引用的条目必须存在;顺带标出「被谁引用」。</summary>
        private static void ValidateStepReferences(
            List<TutorialStep> steps, List<string> narrIds, List<string> demoIds,
            List<string> paperIds, bool[] narRef, bool[] demoRef, bool[] paperRef, List<string> errors)
        {
            for (int s = 0; s < steps.Count; s++)
            {
                TutorialStep st = steps[s];
                string where = $"step「{st.Id}」";

                for (int i = 0; i < st.NarrationIds.Count; i++)
                {
                    int idx = IndexOfOrdinal(narrIds, st.NarrationIds[i]);
                    if (idx < 0)
                        errors.Add($"[②] {where}.narration 引用「{st.NarrationIds[i]}」不存在(悬垂 —— " +
                                   "GDD 规则八校验 2:零悬垂)");
                    else narRef[idx] = true;
                }
                MarkReferenced(st.DemonstrationIds, demoIds, demoRef, where, "demonstrations", errors);
                MarkReferenced(st.PaperIds, paperIds, paperRef, where, "papers", errors);
            }
        }

        /// <summary>把 <paramref name="refs"/> 里每个 id 在 <paramref name="ids"/> 中定位并标引用;
        /// 定位不到 ⇒ 悬垂错误。</summary>
        private static void MarkReferenced(
            IReadOnlyList<string> refs, List<string> ids, bool[] referenced,
            string where, string field, List<string> errors)
        {
            for (int i = 0; i < refs.Count; i++)
            {
                int idx = IndexOfOrdinal(ids, refs[i]);
                if (idx < 0)
                    errors.Add($"[②] {where}.{field} 引用「{refs[i]}」不存在(悬垂)");
                else referenced[idx] = true;
            }
        }

        /// <summary>②-b 反向一致性:示范 / 纸的 <c>step</c> 字段须与「引用它的 step」一致
        /// (两处声明同一关系 ⇒ 否则即第二真源)。</summary>
        private static void ValidateReverseStepFields(
            List<TutorialStep> steps, List<string> stepIds,
            List<TutorialDemonstration> demos, List<TutorialPaper> papers,
            bool[] demoRef, bool[] paperRef, List<string> errors)
        {
            for (int i = 0; i < demos.Count; i++)
            {
                if (string.IsNullOrEmpty(demos[i].StepId)) continue;   // 解析期已报缺字段
                if (IndexOfOrdinal(stepIds, demos[i].StepId) < 0)
                    errors.Add($"[②] 示范「{demos[i].Id}」.step「{demos[i].StepId}」不存在(悬垂)");
                else if (demoRef[i] && !StepLists(demos[i].StepId, steps, true, demos[i].Id))
                    errors.Add($"[②] 示范「{demos[i].Id}」.step = 「{demos[i].StepId}」," +
                               "但该 step 的 demonstrations[] 未列它(两处声明不一致 = 第二真源)");
            }
            for (int i = 0; i < papers.Count; i++)
            {
                if (string.IsNullOrEmpty(papers[i].StepId)) continue;
                if (IndexOfOrdinal(stepIds, papers[i].StepId) < 0)
                    errors.Add($"[②] 纸「{papers[i].Id}」.step「{papers[i].StepId}」不存在(悬垂)");
                else if (paperRef[i] && !StepLists(papers[i].StepId, steps, false, papers[i].Id))
                    errors.Add($"[②] 纸「{papers[i].Id}」.step = 「{papers[i].StepId}」," +
                               "但该 step 的 papers[] 未列它(两处声明不一致 = 第二真源)");
            }
        }

        /// <summary>②-c 孤儿:条目未被任何 step 引用。</summary>
        private static void ValidateNoOrphans(
            List<TutorialNarration> narration, List<TutorialDemonstration> demos,
            List<TutorialPaper> papers, bool[] narRef, bool[] demoRef, bool[] paperRef, List<string> errors)
        {
            ReportOrphans("口述", narRef, i => narration[i].Id, errors);
            ReportOrphans("示范", demoRef, i => demos[i].Id, errors);
            ReportOrphans("纸", paperRef, i => papers[i].Id, errors);
        }

        /// <summary>② 的外部半边:cueId ∈ 44 表;clipKey ∈ 13 表。**表缺席 ⇒ 该半边 NOT-RUN**。</summary>
        private static void ValidateExternalMediaKeys(
            List<TutorialNarration> narration, List<TutorialDemonstration> demos,
            ITutorialMediaKeySource source, List<string> errors,
            out bool cueReady, out bool clipReady)
        {
            cueReady = false;
            clipReady = false;

            if (source == null) return;   // 未就绪:不抛、不崩、不借绿

            if (source.TryGetCueIds(out IReadOnlyCollection<string> cues) && cues != null && cues.Count > 0)
            {
                cueReady = true;
                var table = ToList(cues);
                for (int i = 0; i < narration.Count; i++)
                {
                    string id = narration[i].CueId;
                    if (string.IsNullOrEmpty(id)) continue;   // 解析期已报缺字段
                    if (IndexOfOrdinal(table, id) < 0)
                        errors.Add($"[②] 口述「{narration[i].Id}」.cueId「{id}」∉ 44 音频事件表" +
                                   $"(表 {table.Count} 条)—— 悬垂键(规则八校验 2:cueId→44 表)");
                }
            }

            if (source.TryGetClipKeys(out IReadOnlyCollection<string> keys) && keys != null && keys.Count > 0)
            {
                clipReady = true;
                var table = ToList(keys);
                for (int i = 0; i < demos.Count; i++)
                {
                    string key = demos[i].ClipKey;
                    if (string.IsNullOrEmpty(key)) continue;
                    if (IndexOfOrdinal(table, key) < 0)
                        errors.Add($"[②] 示范「{demos[i].Id}」.clipKey「{key}」∉ 13 示范片段表" +
                                   $"(表 {table.Count} 条)—— 悬垂键(规则八校验 2:clipKey→13 表)");
                }
            }
        }

        private static bool StepLists(string stepId, List<TutorialStep> steps, bool isDemo, string entryId)
        {
            for (int i = 0; i < steps.Count; i++)
            {
                if (!string.Equals(steps[i].Id, stepId, StringComparison.Ordinal)) continue;
                var refs = isDemo ? steps[i].DemonstrationIds : steps[i].PaperIds;
                return IndexOfOrdinal(refs, entryId) >= 0;
            }
            return false;
        }

        private static void ReportOrphans(string kind, bool[] referenced,
            Func<int, string> idOf, List<string> errors)
        {
            for (int i = 0; i < referenced.Length; i++)
                if (!referenced[i])
                    errors.Add($"[②] {kind}「{idOf(i)}」未被任何 step 引用(孤儿 —— " +
                               "GDD 规则八校验 2:零孤儿;data-files「无孤儿条目」)");
        }

        private static List<string> CollectIds(string kind, int count, Func<int, string> idOf,
            List<string> errors)
        {
            var ids = new List<string>(count);
            for (int i = 0; i < count; i++)
            {
                string id = idOf(i);
                if (string.IsNullOrEmpty(id)) continue;
                if (IndexOfOrdinal(ids, id) >= 0)
                    errors.Add($"[②] {kind}条目 id「{id}」重复(条目 id 是 step 引用键,须唯一)");
                else ids.Add(id);
            }
            return ids;
        }

        // ════════════════════════════════════════════════════════════════
        //  ③ 长度约束(≤170 字符 / 标题 ≤2 行 / 自含)
        // ════════════════════════════════════════════════════════════════

        private static void ValidateLengths(
            List<TutorialNarration> narration, List<TutorialPaper> papers, List<string> errors)
        {
            for (int i = 0; i < narration.Count; i++)
            {
                int len = TutorialContentSchema.CharCount(narration[i].Text);
                if (len > TutorialContentSchema.MaxTextChars)
                    errors.Add($"[③] 口述「{narration[i].Id}」.text 长度 {len} > " +
                               $"{TutorialContentSchema.MaxTextChars}(规则八校验 3 · " +
                               "paper-closeup-48 §12:40% 本地化扩张后破版)");
                ReportSelfContainment("口述", narration[i].Id, "text", narration[i].Text, errors);
            }
            for (int i = 0; i < papers.Count; i++)
            {
                int bodyLen = TutorialContentSchema.CharCount(papers[i].Body);
                if (bodyLen > TutorialContentSchema.MaxTextChars)
                    errors.Add($"[③] 纸「{papers[i].Id}」.body 长度 {bodyLen} > " +
                               $"{TutorialContentSchema.MaxTextChars}(规则八校验 3)");

                int lines = TutorialContentSchema.LineCount(papers[i].Title);
                if (lines > TutorialContentSchema.MaxTitleLines)
                    errors.Add($"[③] 纸「{papers[i].Id}」.title 行数 {lines} > " +
                               $"{TutorialContentSchema.MaxTitleLines}(规则八校验 3 · " +
                               "paper-closeup-48 §12:40% 扩张后不破行)");

                ReportSelfContainment("纸", papers[i].Id, "body", papers[i].Body, errors);
            }
        }

        /// <summary>实现注记 4:文本须自含(不得有指代前文的标记)。命中 = 失败,由计数派生。</summary>
        private static void ReportSelfContainment(string kind, string entryId, string field,
            string text, List<string> errors)
        {
            var hits = ScanForTokens(text,
                TutorialContentSchema.SelfContainmentViolationTokens, entryId, field);
            if (hits.Count == 0) return;   // 真值 = 计数,不经字符串判断
            errors.Add($"[③] {kind}「{entryId}」.{field} 不自含:{hits.Count} 处指代标记 " +
                       $"({string.Join("; ", hits)}) —— 口述文本须自含(实现注记 4:" +
                       "读者不该为了听懂这句而回看前文)");
        }

        // ════════════════════════════════════════════════════════════════
        //  ④ 病名闭集扫描(词表缺席 ⇒ NOT-RUN)
        // ════════════════════════════════════════════════════════════════

        private static TutorialTextScanResult ScanDiseaseNames(
            List<TutorialNarration> narration, List<TutorialPaper> papers,
            ITutorialDiseaseNameSource source, List<string> errors)
        {
            // ⚠️ 词表缺席**不**记 error:本项记 NOT-RUN(AC 第 4 条第 ④ 项不勾、不借绿)。
            if (source == null ||
                !source.TryGetDiseaseNames(out IReadOnlyCollection<string> names) || names == null)
                return TutorialTextScanResult.Of(TutorialDiseaseScanOutcome.TableNotReady,
                                                 Array.Empty<TutorialScanHit>());

            var table = ToList(names);

            // 词表在但为空 = 依赖表未就绪的另一种形态(空字典会让扫描恒零命中 = 假绿)
            if (table.Count == 0)
                return TutorialTextScanResult.Of(TutorialDiseaseScanOutcome.TableNotReady,
                                                 Array.Empty<TutorialScanHit>());

            var hits = new List<TutorialScanHit>();
            for (int i = 0; i < narration.Count; i++)
                hits.AddRange(ScanForTokens(narration[i].Text, table,
                    narration[i].Id, "narration[].text"));
            for (int i = 0; i < papers.Count; i++)
            {
                hits.AddRange(ScanForTokens(papers[i].Title, table, papers[i].Id, "papers[].title"));
                hits.AddRange(ScanForTokens(papers[i].Body, table, papers[i].Id, "papers[].body"));
            }

            var result = TutorialTextScanResult.Of(
                hits.Count > 0 ? TutorialDiseaseScanOutcome.Hits : TutorialDiseaseScanOutcome.Clean, hits);

            // 失败与否由计数派生(Cross-Cutting 之八 ②)
            if (result.HitCount > 0)
            {
                errors.Add($"[④] 病名闭集扫描:{result.HitCount} 处命中(字典 {table.Count} 条) —— " +
                           "教学文本不得出现病名 / 诊断结论(AC-48-13);明细:" +
                           JoinHits(result.Hits));
            }
            return result;
        }

        // ════════════════════════════════════════════════════════════════
        //  ⑤ 显式指令扫描
        // ════════════════════════════════════════════════════════════════

        private static TutorialTextScanResult ScanInstructionTokens(
            List<TutorialNarration> narration, List<TutorialPaper> papers, List<string> errors)
        {
            var table = TutorialContentSchema.ExplicitInstructionTokens;
            var hits = new List<TutorialScanHit>();
            for (int i = 0; i < narration.Count; i++)
                hits.AddRange(ScanForTokens(narration[i].Text, table,
                    narration[i].Id, "narration[].text"));
            for (int i = 0; i < papers.Count; i++)
            {
                hits.AddRange(ScanForTokens(papers[i].Title, table, papers[i].Id, "papers[].title"));
                hits.AddRange(ScanForTokens(papers[i].Body, table, papers[i].Id, "papers[].body"));
            }

            var result = TutorialTextScanResult.Of(
                hits.Count > 0 ? TutorialDiseaseScanOutcome.Hits : TutorialDiseaseScanOutcome.Clean, hits);

            if (result.HitCount > 0)
            {
                errors.Add($"[⑤] 显式指令扫描:{result.HitCount} 处命中 —— 只教动作,不指路" +
                           $"(AC-48-06①);明细:{JoinHits(result.Hits)}");
            }
            return result;
        }

        // ════════════════════════════════════════════════════════════════
        //  扫描内核(唯一比较器出口 —— Cross-Cutting 之八 ①)
        // ════════════════════════════════════════════════════════════════

        /// <summary>在一段文本里扫描词表,**返回结构化命中列表**(真假由调用方按 Count 派生)。
        /// <para>⚠️ 比较器 = <see cref="StringComparison.OrdinalIgnoreCase"/>(**显式**)——
        /// 大小写不敏感是内容语义需要,但**不走 culture**(tr-TR 下 'I'/'i' 的默认比较会分叉)。</para></summary>
        /// <param name="text">被扫描文本(空 ⇒ 零命中)。</param>
        /// <param name="tokens">词表(空串项跳过)。</param>
        /// <param name="entryId">条目 id(仅用于人读定位)。</param>
        /// <param name="field">字段路径(仅用于人读定位)。</param>
        public static List<TutorialScanHit> ScanForTokens(
            string text, IReadOnlyList<string> tokens, string entryId, string field)
        {
            var hits = new List<TutorialScanHit>();
            if (string.IsNullOrEmpty(text) || tokens == null) return hits;

            for (int t = 0; t < tokens.Count; t++)
            {
                string token = tokens[t];
                if (string.IsNullOrEmpty(token)) continue;
                int idx = text.IndexOf(token, StringComparison.OrdinalIgnoreCase);
                if (idx >= 0) hits.Add(new TutorialScanHit(entryId, field, token, idx));
            }
            return hits;
        }

        private static string JoinHits(IReadOnlyList<TutorialScanHit> hits)
        {
            int n = Math.Min(hits.Count, 5);
            var parts = new List<string>(n);
            for (int i = 0; i < n; i++) parts.Add(hits[i].ToString());
            string tail = hits.Count > n ? $" …(+{hits.Count - n})" : string.Empty;
            return string.Join("; ", parts) + tail;
        }

        // ════════════════════════════════════════════════════════════════
        //  读值小件(全部显式比较器)
        // ════════════════════════════════════════════════════════════════

        private static bool TryReadString(JsonNode row, string key, string where,
            List<string> errors, out string value)
        {
            value = null;
            if (!row.TryGet(key, out JsonNode n) || n.Kind != JsonNodeKind.String)
            {
                errors.Add($"[①] {where} 缺字符串 {key}" +
                           $"{(n == null ? "(缺失 —— 拼写错误会静默丢字段 ⇒ 硬失败)" : $"(实得 {n.Kind}={n.RawText})")}");
                return false;
            }
            if (string.IsNullOrEmpty(n.Str))
            {
                errors.Add($"[①] {where}.{key} 为空串(内容债以条目计数,空串 = 静默缺内容)");
                return false;
            }
            value = n.Str;
            return true;
        }

        private static int ReadInt(JsonNode row, string key, string where, List<string> errors)
        {
            if (!row.TryGet(key, out JsonNode n) || n.Kind != JsonNodeKind.Integer)
            {
                errors.Add($"[①] {where} 缺整数 {key}" +
                           $"{(n == null ? "(缺失)" : $"(实得 {n.Kind}={n.RawText};浮点字面量结构性硬失败)")}");
                return 0;
            }
            if (n.Int < int.MinValue || n.Int > int.MaxValue)
            {
                errors.Add($"[①] {where}.{key}={n.Int} 越 int32 域");
                return 0;
            }
            return (int)n.Int;
        }

        private static List<string> ReadStringArray(JsonNode row, string key, string where,
            List<string> errors)
        {
            var list = new List<string>();
            if (!row.TryGet(key, out JsonNode n) || n.Kind != JsonNodeKind.Array)
            {
                errors.Add($"[①] {where} 缺数组 {key}" +
                           $"{(n == null ? "(缺失 —— 空引用须显式写 [])" : $"(实得 {n.Kind})")}");
                return list;
            }
            for (int i = 0; i < n.Items.Count; i++)
            {
                if (n.Items[i].Kind != JsonNodeKind.String)
                {
                    errors.Add($"[①] {where}.{key}[{i}] 须为字符串 id(实得 {n.Items[i].Kind})");
                    continue;
                }
                list.Add(n.Items[i].Str);
            }
            return list;
        }

        private static void RejectUnknownKeys(JsonNode obj, string[] allowed, string where,
            List<string> errors)
        {
            foreach (string key in obj.Keys)
            {
                if (!TutorialContentSchema.InWhitelist(allowed, key))
                    errors.Add($"[①] {where} 未知键「{key}」[ADR-014·unknown-key]" +
                               "(拼写错误会静默丢字段 ⇒ 构建期硬失败)");
            }
        }

        private static List<string> ToList(IReadOnlyCollection<string> source)
        {
            var list = new List<string>(source.Count);
            foreach (string s in source) if (!string.IsNullOrEmpty(s)) list.Add(s);
            return list;
        }

        private static bool ContainsOrdinal(List<string> list, string value) => IndexOfOrdinal(list, value) >= 0;

        private static int IndexOfOrdinal(IReadOnlyList<string> list, string value)
        {
            if (list == null || value == null) return -1;
            for (int i = 0; i < list.Count; i++)
                if (string.Equals(list[i], value, StringComparison.Ordinal)) return i;
            return -1;
        }
    }
}
