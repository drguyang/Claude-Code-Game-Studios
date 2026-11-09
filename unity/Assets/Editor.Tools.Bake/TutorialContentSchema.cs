// S17-3 · story-001(tutorial-onboarding)—— 48 教学内容的作者态 schema 共享面
// (键白名单 / 机制常数 / 闭集 / 外置词族表)。
//
// 权威来源:
//   GDD design/gdd/tutorial-and-onboarding.md §规则八(`:236` 起)
//     · 六步 id 闭集 {observe, judge, treat, close, archive, manage}(规则三)
//     · 长度:narration[].text / papers[].body ≤ 170 字符;papers[].title ≤ 2 行
//     · 校验 1..6(JSON 合法 / 引用完整性 / 长度 / 病名闭集 / 显式指令 / predicateKind 白名单)
//   design/ux/paper-closeup-48.md §12(长度上限 = 作者态约束,防 40% 本地化扩张破版)
//   ADR-014 §三(per-schema 绑定:已知键白名单 · 聚合 throw · 禁数值经 double 中转)
//   ADR-024 §①(predicateKind 白名单真源 = design/registry/entities.yaml 的 36 支 Kind;2026-10-22 ADR-032 起 35→36)
//   ADR-010 §六(archive 步判据 = 7a 存档头 Checkpoint 写入 —— schema 白名单的**唯一**例外)
//   docs/architecture/control-manifest.md:571-590(Cross-Cutting 之八:判据不得用文化敏感字符串比较)
//
// ⚠️ **机制值冻结**:170 / 2 行 / 六步闭集 是 GDD 已裁的机制值(本 story Implementation Note 5),
//   **不得**为让夹具通过而放宽。文本**内容**归叙事轮,本 story 只交种子与校验器。
//
// ⚠️ **词族表外置**(本 story Implementation Note 3):显式指令词族与「自含判据」标记词住本文件
//   的**静态只读表**,不散落在校验逻辑里 —— 扩充归文案轮,改表即改判据。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>
    /// 病名闭集词表的**来源接缝**(校验规则 4 的扫描字典 = 9 的病种名表)。
    /// <para>⚠️ 本 story 实施期实测:`assets/data/` 下**不存在**独立的病名闭集词表文件
    /// (只有 <c>disease_action_axis.json</c>,其 <c>DIS_*</c> 是病种 id 而非病名,且它自陈
    /// 「只承载动作轴子集」)。故本接口在**生产路径当前无实现者**。</para>
    /// <para>⚠️ **缺席必须是可观测的**:校验器对缺表报「依赖表未就绪」并把第 ④ 项标
    /// <see cref="TutorialDiseaseScanOutcome.TableNotReady"/>,**不得崩、更不得用弱化词表凑绿**
    /// (本仓「自造断言顶替 AC 语义层」形态之一)。与 <c>IFurnTypeSource</c>(O-24-1)同构。</para>
    /// </summary>
    public interface ITutorialDiseaseNameSource
    {
        /// <summary>尝试取得病名闭集。</summary>
        /// <param name="names">成功时为闭集;失败时为 null。</param>
        /// <returns>true = 词表可用(第 ④ 项须执行);false = 词表缺席(第 ④ 项记 NOT-RUN)。</returns>
        bool TryGetDiseaseNames(out IReadOnlyCollection<string> names);
    }

    /// <summary>
    /// 技法 id 闭集的生产来源(T8 · 2026-11-09)—— <c>papers[].teachTechnique</c> 的外部键半边。
    /// </summary>
    /// <remarks>
    /// <para><b>为什么要跨表</b>:纸「教哪一门技法」是**引用** 9 的处置轴(<c>DiseaseHandle</c> /
    /// ADR-043 §D5:技法 id 空间 = <c>disease_action_axis.json</c> 的 action id)。悬空 id 会让
    /// 「这一页缺」永远判不出来(技法解锁查询对它恒 false)⇒ 缺页态**静默全缺**或**静默不缺**,
    /// 两种都是假绿。承 `LexiconRegistryCrossRefGate` 的同款理由(悬空外键静默烘进产物)。</para>
    /// <para><b>缺席语义(承 <see cref="ITutorialDiseaseNameSource"/> 的同款纪律)</b>:
    /// 文件不存在 ⇒ 返回 false(② 的这条半边记 NOT-RUN,**不阻断烘焙**);
    /// 文件存在却损坏 / 无 <c>actions[]</c> / 零条 ⇒ 抛 <c>BakeValidationException</c>
    /// —— 损坏不得静默降级成「未就绪」。</para>
    /// </remarks>
    public interface ITutorialTechniqueIdSource
    {
        /// <summary>尝试取得技法 id 闭集。</summary>
        /// <param name="ids">成功时为闭集;失败时为 null。</param>
        /// <returns>true = 轴可用(须逐条校验 <c>teachTechnique</c>);false = 轴缺席(NOT-RUN)。</returns>
        bool TryGetTechniqueIds(out IReadOnlyCollection<int> ids);
    }

    /// <summary>第 ④ 项(病名闭集扫描)的结果三态 —— **结构化**,不靠字符串判真假。</summary>
    public enum TutorialDiseaseScanOutcome
    {
        /// <summary>执行且零命中(通过)。</summary>
        Clean = 0,

        /// <summary>执行且命中 ≥1 条(硬失败,失败与否由 <see cref="TutorialContentBinder.DiseaseScanResult.HitCount"/> 派生)。</summary>
        Hits = 1,

        /// <summary>依赖词表未就绪 ⇒ 本项 **NOT-RUN**(不勾、不借绿)。</summary>
        TableNotReady = 2,
    }

    /// <summary>48 教学内容的 schema 常量与闭集(单一出处;绑定器与测试共用)。</summary>
    public static class TutorialContentSchema
    {
        // ── 文件身份 ────────────────────────────────────────────────────────

        /// <summary>作者态源文件名(单一出处;菜单 / 测试 / 烘焙共用)。</summary>
        public const string SourceFileName = "48_tutorial_content.json";

        /// <summary>cooked 产物名(落 <c>unity/Assets/DataCooked/</c>,与既有 15 件同目录约定)。</summary>
        public const string CookedFileName = "48_tutorial_content.cooked.bytes";

        /// <summary>源目录名(相对仓根;承既有先例 <c>assets/data/</c>)。</summary>
        public const string DataDirName = "assets/data";

        /// <summary>期望的 <c>schemaVersion</c>。</summary>
        /// <remarks>
        /// <para>🔁 **2026-11-09(T8):1 → 2,并与 <see cref="CookedFormat.SchemaVersion"/> 解耦。**</para>
        /// <para>原写法 = <c>= CookedFormat.SchemaVersion</c>(全局基线 1)。T8 给 <c>papers[]</c> 行尾
        /// 追加了 <c>teach_technique:i32</c>,48 的载荷布局因此**领先**全局基线一格;
        /// 若仍绑全局常量,要么 48 谎报版本(布局变了版本没变 ⇒ 旧产物被当新布局解),
        /// 要么为了 48 一件事把**全部 16 件产物**的版本抬上去。</para>
        /// <para>⇒ 48 自带版本号。**这不会破坏单一出处**:<c>CookedFormat.SchemaVersion</c> 是
        /// 「格式基线」,各产物的 <c>schemaVersion</c> 是「该产物自己的布局代数」——
        /// 二者本就是两个量,原写法只是把默认值当成了等号。</para>
        /// </remarks>
        public const uint ExpectedSchemaVersion = 2;

        // ── 机制值(GDD 已裁,冻结)────────────────────────────────────────

        /// <summary>口述文本 / 纸正文的字符上限(规则八校验 3 · paper-closeup-48 §12)。
        /// <para>计数单位 = UTF-16 码元(<see cref="string.Length"/>);中日韩常字各占 1 ——
        /// 与「170 字符」的本地化估算口径一致。</para></summary>
        public const int MaxTextChars = 170;

        /// <summary>
        /// <c>papers[].teachTechnique</c> 的「不教任何技法」哨兵(T8 · 2026-11-09)。
        /// <para>⚠️ 取 <c>-1</c> 而不是 <c>0</c>:技法 id <c>0</c> 是**合法**值(止血包扎),
        /// 用 0 当哨兵会让「不教」与「教止血包扎」在数据层不可分 —— 与 ADR-007 §四 用
        /// <c>PatientId.None = -1</c> 的理由同型。</para>
        /// </summary>
        public const int NoTechnique = -1;

        /// <summary>纸标题的行数上限(规则八校验 3 · 2 行过 / 3 行拒)。</summary>
        public const int MaxTitleLines = 2;

        /// <summary>六步 id 闭集(GDD 规则三;AC 第 6 条「恰 6 员,多/少 = 失败」)。</summary>
        public static readonly IReadOnlyList<string> StepIds = new[]
        {
            "observe", "judge", "treat", "close", "archive", "manage",
        };

        /// <summary>闭集基数(= 6;AC 第 6 条把「数量」也变成判据)。</summary>
        public const int StepCount = 6;

        /// <summary>示范者闭集(GDD 规则八:`actor` = <c>mentor</c>,师父,13 实体;闭集枚举)。</summary>
        public static readonly IReadOnlyList<string> DemonstrationActors = new[] { "mentor" };

        /// <summary>`archive` 步的判据字面量 —— **7a 存档头 Checkpoint 写入,不是 SimEvent.Kind**。
        /// <para>⚠️ 这是 predicateKind 白名单的**唯一例外**(GDD 规则八「archive 步判据例外」·
        /// ADR-010 §六:存档头不落流,故 ADR-024 的 36 支 Kind 里没有它;2026-10-22 ADR-032 起 35→36)。</para>
        /// <para>⚠️ **防后人当 bug 修**:本例外的存在是 **ADR-010 §六 的裁决**,不是漏登记;
        /// 若把 `Checkpoint` 删除/改成某个 Kind,<c>archive</c> 步的完成判据即失去语义
        /// (7a 存档头写入 <b>不产生</b> SimEvent)。改动须先改 ADR-010 §六 与 GDD F-48.1 ⑤。</para>
        /// </summary>
        public const string CheckpointPredicateLiteral = "Checkpoint";

        /// <summary>唯一允许挂 <see cref="CheckpointPredicateLiteral"/> 的步 id(其余五步挂它 = 校验 6 失败)。</summary>
        public const string CheckpointOnlyStepId = "archive";

        // ── 键白名单(ADR-014 §三:未知键 = 烘焙期硬失败)────────────────────

        /// <summary>根键白名单。</summary>
        public static readonly string[] RootKeys =
        {
            "schemaVersion", "meta", "steps", "narration", "demonstrations", "papers",
        };

        /// <summary><c>meta</c> 键白名单(内容状态 / 属主 / 校验说明 —— 规则八表层)。</summary>
        public static readonly string[] MetaKeys =
        {
            "contentStatus", "description", "owners", "validation",
        };

        /// <summary><c>meta.owners</c> 键白名单(归属声明;不参与校验,但不许塞私货)。</summary>
        public static readonly string[] MetaOwnerKeys =
        {
            "narrationText", "demonstrationConfig", "paperContent", "scheduleSemantics",
        };

        /// <summary><c>steps[]</c> 行键白名单。</summary>
        public static readonly string[] StepKeys =
        {
            "id", "title", "predicateKind", "note", "narration", "demonstrations", "papers",
        };

        /// <summary><c>narration[]</c> 行键白名单。</summary>
        public static readonly string[] NarrationKeys = { "id", "cueId", "order", "text" };

        /// <summary><c>demonstrations[]</c> 行键白名单。</summary>
        public static readonly string[] DemonstrationKeys = { "id", "step", "actor", "clipKey", "note" };

        /// <summary><c>papers[]</c> 行键白名单。</summary>
        public static readonly string[] PaperKeys =
        {
            "id", "step", "title", "body", "aspectRatio", "illustrationKey", "note",
            // 🔁 2026-11-09(T8):纸 ↔ 技法绑定。缺省 = 不教任何技法(如「望诊四要」是诊断侧,不是技法)。
            "teachTechnique",
        };

        // ── 外置词族表(Implementation Note 3 / 4 —— 扩充归文案轮)──────────

        /// <summary>**显式指引指令**词族(校验 5 的扫描字典;AC-48-06①「只教动作,不指路」)。
        /// <para>⚠️ 本表是**校验夹具常量**,不是设计真相表 —— 它只承载「哪些词等于指路」的现行口径;
        /// 扩充/收窄须走文案轮 + 本表 diff,禁在校验逻辑里就地加词。</para>
        /// <para>⚠️ 匹配方式 = **子串、OrdinalIgnoreCase**(显式选择,见
        /// <see cref="TutorialContentBinder"/> 文件头「比较器纪律」)。</para></summary>
        public static readonly IReadOnlyList<string> ExplicitInstructionTokens = new[]
        {
            "按",        // 「按 E」「按左键」
            "去",        // 「去药圃」「去 X 处」
            "走到",      // 「走到柜台前」
            "前往",      // 「前往医馆」
            "点击",      // 显式指针指令
            "press",     // 英文:press E
            "click",     // 英文:click the paper
            "go to",     // 英文:go to the clinic
        };

        /// <summary>**自含判据**标记词(Implementation Note 4:口述文本不得指代前文才能读)。
        /// <para>命中 = 该条 text **不自含** ⇒ 校验 3 失败(与长度同一条规则的两个半边)。</para></summary>
        public static readonly IReadOnlyList<string> SelfContainmentViolationTokens = new[]
        {
            "如前所述",
            "上文",
            "前面提到",
            "刚才说",
            "上面说的",
            "as mentioned above",
        };

        // ── predicateKind 白名单(registry 全集 ∪ {Checkpoint})────────────────

        /// <summary>registry 的 Kind 名全集(36 支;2026-10-22 ADR-032 起 35→36)。
        /// <para>⚠️ **真源 = <c>design/registry/entities.yaml</c>**(ADR-024 §①);此处读编译期镜像
        /// <see cref="EventKind"/> —— 二者由 ADR-024 断言 **A5(双向差集归零)** 锁死,
        /// 故本处不构成第二真源(与 <c>OutcomeTableBinder</c> 「以枚举符号直用」同纪律)。
        /// 校验期不解析 yaml ⇒ 烘焙零额外 I/O。</para></summary>
        public static readonly IReadOnlyList<string> RegistryKindNames = Enum.GetNames(typeof(EventKind));

        // ── 闭集判定(全部显式 StringComparison.Ordinal)────────────────────

        /// <summary>判断 <paramref name="id"/> 是否 ∈ 六步闭集。</summary>
        public static bool IsStepId(string id)
        {
            for (int i = 0; i < StepIds.Count; i++)
                if (string.Equals(StepIds[i], id, StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>判断 <paramref name="actor"/> 是否 ∈ 示范者闭集。</summary>
        public static bool IsDemonstrationActor(string actor)
        {
            for (int i = 0; i < DemonstrationActors.Count; i++)
                if (string.Equals(DemonstrationActors[i], actor, StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>判断 predicateKind 是否 ∈ registry 全集(<b>不含</b> Checkpoint 例外 —— 例外另判)。</summary>
        public static bool IsRegistryKind(string kind)
        {
            for (int i = 0; i < RegistryKindNames.Count; i++)
                if (string.Equals(RegistryKindNames[i], kind, StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>判断字符串是否 ∈ 给定白名单(显式 Ordinal;空白名单恒 false)。</summary>
        public static bool InWhitelist(string[] allowed, string candidate)
        {
            if (allowed == null || candidate == null) return false;
            for (int i = 0; i < allowed.Length; i++)
                if (string.Equals(allowed[i], candidate, StringComparison.Ordinal)) return true;
            return false;
        }

        // ── 长度判据 ────────────────────────────────────────────────────────

        /// <summary>字符数(<see cref="MaxTextChars"/> 判据)。null 记 0。</summary>
        public static int CharCount(string text) => text == null ? 0 : text.Length;

        /// <summary>行数(按 <c>'\n'</c> 切;空串 = 1 行);<see cref="MaxTitleLines"/> 判据。</summary>
        public static int LineCount(string text)
        {
            if (string.IsNullOrEmpty(text)) return 1;
            int lines = 1;
            for (int i = 0; i < text.Length; i++)
                if (text[i] == '\n') lines++;
            return lines;
        }

        /// <summary>校验 <c>aspectRatio</c> 形状 = <c>W:H</c>(两段正整数;如 4:3)。
        /// <para>⚠️ 只校验**形状**;「近景纸与道具纸纵横比一致」属 6 布置轮(OQ-C2),不在本 story。</para></summary>
        public static bool TryParseAspectRatio(string text, out int width, out int height)
        {
            width = 0; height = 0;
            if (string.IsNullOrEmpty(text)) return false;
            int colon = text.IndexOf(':', StringComparison.Ordinal);
            if (colon <= 0 || colon == text.Length - 1) return false;
            if (text.IndexOf(':', colon + 1) >= 0) return false;   // 只允许一个分隔符

            string left = text.Substring(0, colon);
            string right = text.Substring(colon + 1);
            if (!TryParsePositiveInt(left, out width)) return false;
            if (!TryParsePositiveInt(right, out height)) return false;
            return true;
        }

        private static bool TryParsePositiveInt(string s, out int value)
        {
            value = 0;
            if (string.IsNullOrEmpty(s)) return false;
            for (int i = 0; i < s.Length; i++)
                if (s[i] < '0' || s[i] > '9') return false;
            if (!int.TryParse(s, System.Globalization.NumberStyles.None,
                              System.Globalization.CultureInfo.InvariantCulture, out value))
                return false;
            return value > 0;
        }
    }
}
