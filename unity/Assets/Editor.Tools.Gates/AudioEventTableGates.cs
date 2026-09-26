// 权威来源:production/epics/audio-system/story-002-event-table-schema-gate.md(本 story 全部 AC 的执行体)
//   · AC-44-09(BLOCKING)—— 每行 whitelist_category ∈ 五类 且 trigger_source ∈ 合法集(禁止集任一 = 失败);
//                           三重负向(Sting 类别 / 类别合法 + VitalsCrossed / 空表 · 缺必需 cue)+ 配对混搭 + NOT-RUN 守卫
//   · AC-44-D9          —— 每行含 whitelist_category + trigger_source + schema_version(缺一 ⇒ 构建失败)
//   · schema 校验规则 1–9(design/gdd/audio-system.md §Event Table Schema :531-548,2026-09-25 二轮增补)
// GDD:design/gdd/audio-system.md
//   · §Event Table Schema :474-548(五类 · 合法/禁止集 · 规则 1–9)
//   · §Edge Cases 相位条 :576-584(2026-09-25 F5 重钉:相位原点 = 吸气起点;窗口 = 吸气段末 20%;
//     ±jitter 不得出吸气段;原 [0.6,0.95]×基础周期 落呼气段,医学反相,已废)
//   · §Formulas F-44.1 :300(tier_map 定型列集 6 列,唯一出处)· 规则二 :66-100(白名单 + 双重负向)
//   · AC-44-15 :1087-1092(subtitle_text = 语声/口述 cue 键集,[A] 半由本 story 承接)
// ADR-014 §三(两阶段工具链:本文件 = 阶段 2 的**校验纯函数**;绑定 / 烘焙接线归 Story 010)
// ADR-018 §六(无提示音铁律机械化:白名单断言 + 双负向夹具 + NOT-RUN 守卫)
// TR-audio-002(白名单断言 + 双负向)· TR-audio-010(事件表走 ADR-014 烘焙,零 JSON 解析器)
//
// ⚠️ 形态 = **错误列表(空 = 通过)**;本文件**零 throw** —— 聚合非空列表 ⇒ 构建失败的 throw 归
//    Story 010 烘焙管线(承 RecipeValidationGates / DrugProfileGates 同一形态,ADR-014 §三)。
// ⚠️ **枚举定义在代码**(闭集 C#:五类 / 合法集 / 禁止集 / 配对表 / 必需 cue / 族前缀全部是本文件常量),
//    数据只承载字面量 —— 否则「扩枚举 = 改数据即可过 CI」(GDD §Event Table Schema 2026-09-25 二轮纪律①)。
// ⚠️ **trigger_source 是申报,不是检测**:本门只查「字面量 ∈ 合法集」+「配对表」,上游谎报不可由本门识别
//    (GDD :515-525 降调注 + 重开触发条件 3)。措辞纪律:不称「堵住」阈值警报,只称
//    「依赖上游诚实申报 + 负向夹具查谎报形态」。
// ⚠️ **trigger_phase 编码决定(本 story 落码时钉死)**:字段值 = **相对吸气段的分数 ∈ [0,1]**
//    (原点 = 吸气起点,GDD「×吸气段时长」的等价落码)。门只断言 ∈ [0.8, 1.0];
//    **运行期**把它映射到绝对相位 = `INSPIRE_FRACTION × trigger_phase`(吸气占比旋钮,用户调)——
//    这样门不需要 INSPIRE_FRACTION(数值待调)即可判定窗口 ⊆ 吸气段。
//    旧编码「×基础周期 / ×全周期」([0.6,0.95] × 全周期,F5 重钉前)以**非数字字面量**出现时
//    由本门拒收(见 ValidateLoopClock)。
// ⚠️ 范围锁(Out of Scope,勿在本文件实现):
//    · 素材 `assets[]` ↔ 文件存在谓词(D6)与 Addressables 组门(D5)= Story 010 ——
//      **本门不查 assets[] 存在性(NOT-RUN,归 Story 010 D6)**,种子 / README 已同文标注
//    · `xfade_ms ≥ MUSIC_XFADE_MIN_MS` 数值断言(AC-44-18)= Story 012(本门只管存在性 / 类型)
//    · 世界语境呼吸行的 `bus` / min-max_distance / occlusion 断言(AC-44-16)= Story 008
//    · 语声 cue **全集**与上游扇出双投递([L] 半)= Story 011(本门只承接键集覆盖 [A] 半)
//    · 烘焙菜单 / 音频 Baker = Story 010 —— 但**键集白名单的单一出处 = 本文件**
//      (`TopLevelKeys` / `RowKeys` / `TierMapColumns` 三层,ADR-014 §三 未知键硬失败);
//      Story 010 绑定层**引用**这三份常量,**禁另抄一份**(两处执行 = 两处分叉,承 ADR-024 §⑥)
//
// ✅ 2026-09-26 顾问复核并入(编码 / 判据):
//    · **trigger_phase 判据**:QA 场景由「0.95×全周期」改判为 `0.5`(相对吸气段分数,出窗 [0.8,1.0])
//      —— 旧场景在分数编码下不可判(换算依赖用户旋钮 INSPIRE_FRACTION = 假红源)
//    · **编码防线四件之 ①** = 运行期映射收口唯一函数 `InspirePhaseMapping.ToAbsoluteOffsetSeconds`
//      (`Gameplay.Presentation/Audio/`,门不得复制该乘法);**②** = 该映射的 EditMode 测试
//      (本门测试真身内);**③** = 种子 `_note` 钉单位;**④** = 本门 `trigger_phase + jitter ≤ 1`
//      (分数域「不得越出吸气段」)
//    · **schema_version 三连**(缺失 / 非整数 / 不匹配)= 照抄 ItemDatabaseBinder 绑定入口先例;
//      顶层必填 + 行级同值**双写**以兑现 AC-44-D9「每行含」字面
//    · **配对表非恒等**:前四类同名自配,`MusicLayer` 只配 `EncounterMusicLayer`(恒等写法会拒合法乐层)

using System;
using System.Collections.Generic;
using System.Globalization;

namespace DaYiJingCheng.EditorTools.Gates
{
    /// <summary><c>adventitious_policy</c> 的**原文**承载(JSON 字面量未解析 —— 解析与拒收在门内完成,
    /// 以便把「非数字字面量 = 已废的 ×全周期 编码」与「数字越窗」区分为两种诊断)。</summary>
    public sealed class AdventitiousPolicyRaw
    {
        /// <summary><c>trigger_phase</c> 的 JSON 字面量原文(如 <c>0.9</c> 或 <c>"0.95×全周期"</c>);null = 字段缺失。</summary>
        public string TriggerPhaseRaw;

        /// <summary><c>jitter</c> 的 JSON 字面量原文;null = 字段缺失。</summary>
        public string JitterRaw;
    }

    /// <summary>音频事件表的一行(绑定前的 raw 承载 —— 与 BoundItem 先例同型:门读原文,不读已解析值)。</summary>
    public sealed class AudioEventTableRow
    {
        /// <summary>cue id(如 <c>Voice_Cough_Damp</c>);null = 字段缺失(NOT-RUN 守卫)。</summary>
        public string Cue;

        /// <summary>行级 <c>schema_version</c> 原文;null = 缺失(AC-44-D9)。</summary>
        public string SchemaVersionRaw;

        /// <summary>内容侧五类之一;null = 字段缺失(NOT-RUN 守卫 · 规则 1)。</summary>
        public string WhitelistCategory;

        /// <summary>时机侧闭枚举;null = 字段缺失(NOT-RUN 守卫 · 规则 2)。</summary>
        public string TriggerSource;

        /// <summary>素材文件名数组;null = 字段缺失(素材存在性归 Story 010,本门不断言)。</summary>
        public IReadOnlyList<string> Assets;

        /// <summary>混音总线字面量;null = 缺失(总线断言归 AC-44-16 / 003,本门不断言)。</summary>
        public string Bus;

        /// <summary>是否循环 cue;字段缺失按 <c>false</c> 处理(GDD 示例行的可选字段)。</summary>
        public bool Loop;

        /// <summary>节拍时钟引用(指向基础层行的 cue);null = 字段缺失。</summary>
        public string ClockRef;

        /// <summary>字幕文本;null = **字段缺失**,空串 = 字段在但为空 —— 两者对语声族都是失败。</summary>
        public string SubtitleText;

        /// <summary><c>xfade_ms</c> 的 JSON 字面量原文;null = 字段缺失。</summary>
        public string XfadeRaw;

        /// <summary>行级 per-cue 覆盖参数(**嵌套 per-tier 形状**,GDD :487:
        /// <c>{"0": {列: 值}, "1": {...}, "2": {...}}</c>;**禁含滤波列**)—— null = 字段缺失。
        /// <para>2026-09-26 审查 BLOCKING 修:原扁平字典只扫顶层键,对 GDD 嵌套形状恒失效;
        /// 扁平数据(键 = 列名)由**档位键 ∉ {0,1,2}** 规则同样拒收。内层值 = 原文(未解析)。</para></summary>
        public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> TierParams;

        /// <summary>附加音层策略;null = 字段缺失。</summary>
        public AdventitiousPolicyRaw Policy;

        /// <summary>本行**实际出现过的全部键**(含未知键)—— 未知键白名单判定的输入;
        /// null = 抽取侧未提供(NOT-RUN 守卫)。</summary>
        public IReadOnlyList<string> PresentKeys;
    }

    /// <summary>音频事件表(顶层)—— <c>assets/data/audio_events.json</c> 的绑定前结构。</summary>
    public sealed class AudioEventTable
    {
        /// <summary>顶层 <c>schema_version</c> 原文;null = 缺失(ADR-014 §三:每个数据文件带 schema_version)。</summary>
        public string SchemaVersionRaw;

        /// <summary>设备级滤波表:档位键("0"/"1"/"2")→ 列名 → 原文;null = 字段缺失(规则 5)。</summary>
        public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> TierMap;

        /// <summary>cue 行数组;null = `rows` 字段缺失,0 元素 = 空表(规则 7 两者皆拒)。</summary>
        public IReadOnlyList<AudioEventTableRow> Rows;

        /// <summary>顶层**实际出现过的全部键**(含未知键)—— 未知键白名单判定的输入;
        /// null = 抽取侧未提供(NOT-RUN 守卫)。</summary>
        public IReadOnlyList<string> PresentKeys;
    }

    /// <summary>音频事件表的构建期校验纯函数(Story 002 · AC-44-09 / AC-44-D9 / schema 规则 1–9 的执行体)。
    /// <para>全部方法:纯函数 · 无 I/O · 无静态可变态 · **零 throw** · 返回错误列表(空 = 通过)——
    /// 供 Story 010 两阶段烘焙管线复用(聚合非空列表 ⇒ 构建失败,ADR-014 §三
    /// 「全量校验失败 = 构建失败」)。</para>
    /// <example>
    /// AudioEventTable table = /* 阶段 1 词法 + 阶段 2 绑定(Story 010) */;
    /// var errors = AudioEventTableGates.Validate(table);
    /// if (errors.Count &gt; 0) throw new BakeValidationException(string.Join("\n", errors));
    /// </example></summary>
    public static class AudioEventTableGates
    {
        // ══════════ 闭集常量(枚举定义在代码 —— 数据只承载字面量)══════════

        /// <summary>内容侧五类(<c>whitelist_category</c> 合法集;GDD §Event Table Schema :505-507 · 规则二第 5 类)。</summary>
        public static readonly IReadOnlyList<string> WhitelistCategories = new[]
        {
            "PlayerAction", "PlayerExamAction", "WorldPerceptible", "ContinuousPhysiology", CategoryMusicLayer,
        };

        /// <summary>时机侧合法集(<c>trigger_source</c>;GDD :513 左列 · ADR-018 §六 机械化 ②)。</summary>
        public static readonly IReadOnlyList<string> LegalTriggerSources = new[]
        {
            "PlayerAction", "PlayerExamAction", "WorldPerceptible", "ContinuousPhysiology", TriggerEncounterMusicLayer,
        };

        /// <summary>时机侧**禁止集**(状态播报;GDD :513 右列 —— 命中任一 = 构建失败)。</summary>
        public static readonly IReadOnlyList<string> ForbiddenTriggerSources = new[]
        {
            "VitalsCrossed", "ThresholdCrossed", "OutcomeResolved",
        };

        /// <summary>顶层 <c>tier_map</c> 的档位键集(规则 5;GDD :535-536)。</summary>
        public static readonly IReadOnlyList<string> TierMapKeys = new[] { "0", "1", "2" };

        /// <summary><c>tier_map</c> 定型列集(**恰 6 列**,F-44.1 唯一出处 :300;规则 5 的列集)。</summary>
        public static readonly IReadOnlyList<string> TierMapColumns = new[]
        {
            "passband_center_hz", "passband_width_hz", "noise_floor_db",
            "contact_noise_floor_db", "band_detail_count", "signal_db",
        };

        /// <summary>**顶层**已知键白名单(三层白名单的第一层;ADR-014 §三「未知键 = 烘焙期硬失败」)。
        /// <para>⚠️ **单一出处**:Story 010 绑定层直接引用本集合,**禁另抄一份**(两处执行 = 两处分叉,
        /// 承 ADR-024 §⑥ 同一纪律)。</para></summary>
        public static readonly IReadOnlyList<string> TopLevelKeys = new[]
        {
            "schema_version", "_note", "tier_map", "rows",
        };

        /// <summary>**行级**已知键白名单(三层白名单的第二层;第三层 = <c>tier_map</c> 恰 6 列,
        /// 见 <see cref="TierMapColumns"/>)。未知行键 ⇒ 构建失败(拼写错静默丢字段 = ADR-014 拒收面)。
        /// <para>⚠️ 单一出处同 <see cref="TopLevelKeys"/>;新增行键(如 008 的世界语境参数)先改本常量。</para></summary>
        public static readonly IReadOnlyList<string> RowKeys = new[]
        {
            "schema_version", "_note", "cue", "assets", "tier_params", "loop", "bus",
            "whitelist_category", "trigger_source", "clock_ref", "adventitious_policy",
            "xfade_ms", "subtitle_text",
            // 世界语境呼吸行参数(AC-44-16 的载体字段;断言归 Story 008,键在此预登记防假红)
            "min_distance", "max_distance", "occlusion_lowpass",
        };

        /// <summary>构建期**必需 cue 清单**(规则 7 · GDD :541-543)—— 语声族 / 呼吸基础层 /
        /// 呼吸附加层 / 世界语境呼吸四支最小集;命名与 <c>assets/data/audio_events.json</c> 种子行一致。
        /// <para>空表或缺任一 ⇒ 失败(堵「0 行全绿」假象)。扩集 = 改本常量(构建期常量,不是数据)。</para></summary>
        public static readonly IReadOnlyList<string> RequiredCues = new[]
        {
            "Voice_Cough_Damp",            // 语声族(GDD F-44.3 示例 cue id :361)
            "BreathLayer_Base_Calm",       // 呼吸两层 —— 基础气流层(规则四)
            "BreathLayer_Adventitious_Fine", // 呼吸两层 —— 附加音层(细湿啰音)
            "WorldBreath_Baseline",        // 世界语境呼吸(F-44.7 主通道,AC-44-16 的载体行)
        };

        /// <summary>需 <c>subtitle_text</c> 的 cue **族前缀**(AC-44-15 [A] 半 · GDD :502「语声 / 教学口述 cue」):
        /// <c>Voice_*</c>(病人语声变体库)+ <c>Narration_*</c>(48 教学口述;`tutorial-and-onboarding.md:253`
        /// 的 <c>narration[].cueId</c>)。族以**前缀常量**落在代码,数据不得自定义族。</summary>
        public static readonly IReadOnlyList<string> SubtitleCuePrefixes = new[] { "Voice_", "Narration_" };

        /// <summary>附加音层的 cue 前缀(规则 8 的**行判定器**):命中即必须带
        /// <c>clock_ref</c> + <c>adventitious_policy</c> + <c>loop: true</c>。
        /// <para>命名承素材族 <c>sfx_breath_adventitious_*</c>(GDD :786)。</para></summary>
        public static readonly IReadOnlyList<string> AdventitiousCuePrefixes = new[] { "BreathLayer_Adventitious" };

        /// <summary>附加音层**素材**前缀(规则 8 第四入口,2026-09-26 审查 REC):
        /// cue 改名后仍能凭 <c>assets[]</c> 的素材族命中(素材前缀 = GDD :784「sfx_breath_adventitious_*」
        /// 唯一客观载体;只做前缀匹配,**不查文件存在性**(归 Story 010 D6))。</summary>
        public static readonly IReadOnlyList<string> AdventitiousAssetPrefixes = new[] { "sfx_breath_adventitious_" };

        /// <summary>内容侧乐层类(第 5 类;GDD 规则二第 5 类 / ADR-018 §六「音乐层例外」)——
        /// <c>xfade_ms</c> 存在性判据用的就是这个字面量。</summary>
        public const string CategoryMusicLayer = "MusicLayer";

        /// <summary>时机侧乐层触发(G1–G3 护栏挂在本值上)。</summary>
        public const string TriggerEncounterMusicLayer = "EncounterMusicLayer";

        /// <summary>相位窗下界(相对吸气段的分数;GDD F5 重钉 :580「∈ [0.8,1.0] × 吸气段时长」)。</summary>
        public const double TriggerPhaseMin = 0.8;

        /// <summary>相位窗上界(同上;1.0 = 吸气段终点)。</summary>
        public const double TriggerPhaseMax = 1.0;

        // ══════════ 总门 ══════════

        /// <summary>逐条跑完规则 1–9 + AC-44-D9 + subtitle / xfade 存在性,聚合全部错误。
        /// <para>空 = 通过;非空 = 构建失败(throw 由调用方执行)。</para></summary>
        /// <param name="table">绑定前事件表(null ⇒ 一条 NOT-RUN 守卫错误)。</param>
        public static IReadOnlyList<string> Validate(AudioEventTable table)
        {
            if (table == null)
            {
                return new[]
                {
                    "[NOT-RUN 守卫 · 规则3] 事件表本体缺失(null)—— 全部规则不可执行,不得静默通过" +
                    "(「没检查」与「检查通过」必须可区分)",
                };
            }

            var errors = new List<string>();
            errors.AddRange(ValidateSchemaVersion(table));
            errors.AddRange(ValidateKnownKeys(table));
            errors.AddRange(ValidateRowCuePresence(table.Rows));
            errors.AddRange(ValidateTierMap(table));
            errors.AddRange(ValidateWhitelistCategories(table.Rows));
            errors.AddRange(ValidateTriggerSources(table.Rows));
            errors.AddRange(ValidatePairing(table.Rows));
            errors.AddRange(ValidateRowSchemaVersions(table.Rows, table.SchemaVersionRaw));
            errors.AddRange(ValidateRequiredCues(table.Rows));
            errors.AddRange(ValidateAdventitiousLayerPresence(table.Rows));
            errors.AddRange(ValidateTierParamsFilters(table.Rows));
            errors.AddRange(ValidateCueUniqueness(table.Rows));
            errors.AddRange(ValidateLoopClock(table.Rows));
            errors.AddRange(ValidateXfade(table.Rows));
            errors.AddRange(ValidateSubtitles(table.Rows));
            return errors;
        }

        // ══════════ 三层白名单:未知键 = 构建失败(ADR-014 §三)══════════

        /// <summary>顶层 + 行级**未知键**拒收(三层白名单的前两层;第三层 = tier_map 恰 6 列,
        /// 见 <see cref="ValidateTierMap"/>)。未知键 = 拼写错静默丢字段,ADR-014 §三 定为硬失败。
        /// <para>⚠️ 缺键集输入(<c>PresentKeys == null</c>)= NOT-RUN 守卫错误,**不得 continue 静默过**
        ///(「TryGet false → continue」正是假绿形态)。</para></summary>
        /// <param name="table">事件表(null ⇒ NOT-RUN)。</param>
        public static IReadOnlyList<string> ValidateKnownKeys(AudioEventTable table)
        {
            if (table == null)
                return new[] { "[NOT-RUN 守卫 · 规则3] 事件表缺失(null),键集白名单不可校验" };

            var errors = new List<string>();

            if (table.PresentKeys == null)
            {
                errors.Add(
                    "[NOT-RUN 守卫 · 规则3] 顶层键集不可枚举(PresentKeys 缺失)—— 未知键白名单" +
                    "(ADR-014 §三 未知键 = 硬失败)不可执行,不得静默通过");
            }
            else
            {
                foreach (string key in table.PresentKeys)
                {
                    if (!Contains(TopLevelKeys, key))
                        errors.Add($"[未知键] 顶层键「{key}」∉ 已知键白名单 {{{string.Join(", ", TopLevelKeys)}}}" +
                                   " —— ADR-014 §三:未知键 = 烘焙期硬失败(防拼写错静默丢字段)");
                }
            }

            if (table.Rows == null)
                return errors;

            for (int i = 0; i < table.Rows.Count; i++)
            {
                AudioEventTableRow row = table.Rows[i];
                if (row == null)
                    continue;
                if (row.PresentKeys == null)
                {
                    errors.Add(
                        $"[NOT-RUN 守卫 · 规则3] {RowLabel(row, i)} 键集不可枚举(PresentKeys 缺失)—— " +
                        "行级未知键白名单不可执行,不得静默通过");
                    continue;
                }

                foreach (string key in row.PresentKeys)
                {
                    if (!Contains(RowKeys, key))
                        errors.Add($"{RowLabel(row, i)} 未知行键「{key}」∉ 已知键白名单 —— " +
                                   "ADR-014 §三:未知键 = 烘焙期硬失败(新增行键先改 AudioEventTableGates.RowKeys)");
                }
            }

            return errors;
        }

        // ══════════ 顶层 schema_version(ADR-014 §三 数据 schema 版本化)══════════

        /// <summary>顶层 <c>schema_version</c> 必须存在(缺失 ⇒ NOT-RUN 守卫;版本号的分派校验归 Story 010 绑定层)。</summary>
        public static IReadOnlyList<string> ValidateSchemaVersion(AudioEventTable table)
        {
            if (table == null)
                return new[] { "[NOT-RUN 守卫 · 规则3] 事件表缺失(null),顶层 schema_version 不可校验" };

            if (string.IsNullOrEmpty(table.SchemaVersionRaw))
            {
                return new[]
                {
                    "[NOT-RUN 守卫 · 规则3] 顶层 schema_version 缺失 —— ADR-014 §三「每个数据文件带 " +
                    "schema_version」;未版本化的表不得进入烘焙(AC-44-D9 同族)",
                };
            }

            // 2026-09-26 审查 REC(双审同点名):顶层非整数原被静默放行,且使行级「不匹配」
            // 比较整体跳过(topLevelParsed=false)=「没检查 ≠ 检查通过」—— 照 AC-44-D9 三连补类型面。
            if (!int.TryParse(table.SchemaVersionRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            {
                return new[]
                {
                    $"[规则3] 顶层 schema_version = {table.SchemaVersionRaw} 非整数字面量 —— " +
                    "AC-44-D9 三连(顶层类型面;行级不匹配比较依赖本值可解析)",
                };
            }

            return Array.Empty<string>();
        }

        // ══════════ 规则 3(NOT-RUN 守卫):行必须有 cue —— 行身份缺失 ══════════

        /// <summary>规则 3 补(2026-09-26 复审 REC):<c>rows</c> 里每行必须有非空 <c>cue</c>。
        /// <para>为何归 NOT-RUN 守卫而非内容规则:<c>cue</c> 是行的身份 —— 缺它时规则 7(必需 cue)
        /// 与规则 9(同名唯一)**无从判定**、字幕族前缀判据失效、<see cref="RowLabel"/> 退化为
        /// 下标 ⇒ 校验「跑过了」但对这行什么都没查,正是「没检查」与「检查通过」不可分的形态。
        /// 复审前全案无任何规则拒收此类行,它能过完 13 段校验直接进烘焙。</para>
        /// <para>字段本身在 GDD §Event Table Schema 的 schema 示例里,但未写必填 —— 同批补注。</para></summary>
        /// <param name="rows">cue 行;null = <c>rows</c> 字段缺失(NOT-RUN)。</param>
        public static IReadOnlyList<string> ValidateRowCuePresence(IReadOnlyList<AudioEventTableRow> rows)
        {
            if (rows == null)
                return new[] { "[NOT-RUN 守卫 · 规则3] rows 字段缺失 —— 行 cue 存在性不可执行" };

            var errors = new List<string>();
            for (int i = 0; i < rows.Count; i++)
            {
                AudioEventTableRow row = rows[i];
                if (row == null)
                {
                    // 规则 1 / 2 / AC-44-D9 也会报(null 被 `row == null || 缺字段` 合并条件吞成
                    // 「缺 whitelist_category」等),但那三条的措辞对 null 行是错的 ——
                    // 这里给出身份级的准确原因,并保证键集白名单(对 null 行 `continue`)也有覆盖。
                    errors.Add(
                        $"行[{i}] 本体为 null —— [NOT-RUN 守卫 · 规则3]:行身份缺失," +
                        "全部规则对本行不可执行,不得静默通过");
                    continue;
                }
                if (string.IsNullOrEmpty(row.Cue))
                    errors.Add(
                        $"行[{i}] 缺 cue(字段缺失或空串)—— [NOT-RUN 守卫 · 规则3]:" +
                        "行身份不可用,规则 7 / 规则 9 / 字幕族判据对本行失效,不得静默通过");
            }

            return errors;
        }

        // ══════════ 规则 1:whitelist_category ∈ 五类 ══════════

        /// <summary>规则 1:每行 <c>whitelist_category</c> 必填且 ∈ 五类(缺失 ⇒ NOT-RUN 守卫)。</summary>
        /// <param name="rows">cue 行;null = <c>rows</c> 字段缺失(NOT-RUN)。</param>
        public static IReadOnlyList<string> ValidateWhitelistCategories(IReadOnlyList<AudioEventTableRow> rows)
        {
            if (rows == null)
                return new[] { "[NOT-RUN 守卫 · 规则3] rows 字段缺失 —— 规则 1(whitelist_category)不可执行" };

            var errors = new List<string>();
            for (int i = 0; i < rows.Count; i++)
            {
                AudioEventTableRow row = rows[i];
                string label = RowLabel(row, i);

                if (row == null || string.IsNullOrEmpty(row.WhitelistCategory))
                {
                    errors.Add(
                        $"[NOT-RUN 守卫 · 规则3] {label} 缺 whitelist_category 字段 —— 断言不可执行," +
                        "不得静默通过(AC-44-09 NOT-RUN 守卫)");
                    continue;
                }

                if (!Contains(WhitelistCategories, row.WhitelistCategory))
                {
                    errors.Add(
                        $"{label} whitelist_category = 「{row.WhitelistCategory}」∉ 五类白名单 " +
                        $"{{{string.Join(", ", WhitelistCategories)}}} —— AC-44-09 ① / 规则 1" +
                        "(sting / 提示音类别在此拒收)");
                }
            }

            return errors;
        }

        // ══════════ 规则 2:trigger_source ∈ 合法集(禁止集任一 = 失败)══════════

        /// <summary>规则 2:每行 <c>trigger_source</c> 必填 ∈ 合法集;命中**禁止集**给出专门诊断(缺失 ⇒ NOT-RUN)。</summary>
        /// <param name="rows">cue 行;null = <c>rows</c> 字段缺失(NOT-RUN)。</param>
        public static IReadOnlyList<string> ValidateTriggerSources(IReadOnlyList<AudioEventTableRow> rows)
        {
            if (rows == null)
                return new[] { "[NOT-RUN 守卫 · 规则3] rows 字段缺失 —— 规则 2(trigger_source)不可执行" };

            var errors = new List<string>();
            for (int i = 0; i < rows.Count; i++)
            {
                AudioEventTableRow row = rows[i];
                string label = RowLabel(row, i);

                if (row == null || string.IsNullOrEmpty(row.TriggerSource))
                {
                    errors.Add(
                        $"[NOT-RUN 守卫 · 规则3] {label} 缺 trigger_source 字段 —— 断言不可执行," +
                        "不得静默通过(AC-44-09 NOT-RUN 守卫)");
                    continue;
                }

                if (Contains(ForbiddenTriggerSources, row.TriggerSource))
                {
                    errors.Add(
                        $"{label} trigger_source = 「{row.TriggerSource}」 ∈ **禁止集(状态播报)** " +
                        $"{{{string.Join(", ", ForbiddenTriggerSources)}}} —— AC-44-09 ② / 规则 2:" +
                        "阈值 / 结果类时机申报 = 构建失败(申报非检测,见 GDD :515-525 降调注)");
                    continue;
                }

                if (!Contains(LegalTriggerSources, row.TriggerSource))
                {
                    errors.Add(
                        $"{label} trigger_source = 「{row.TriggerSource}」 ∉ 合法集 " +
                        $"{{{string.Join(", ", LegalTriggerSources)}}} —— AC-44-09 ② / 规则 2");
                }
            }

            return errors;
        }

        // ══════════ 规则 6:category ↔ trigger 显式配对表 ══════════

        /// <summary>规则 6:<c>whitelist_category</c> 与 <c>trigger_source</c> 须落在**显式配对表**内;
        /// 单查各自 ∈ 集合不够 —— 混搭(如 <c>PlayerAction</c> × <c>ContinuousPhysiology</c>)= 失败。
        /// <para>只在**两侧各自都已通过规则 1 / 2**时执行(否则规则 1/2 已报,避免重复诊断)。</para></summary>
        /// <param name="rows">cue 行;null = <c>rows</c> 字段缺失(NOT-RUN)。</param>
        public static IReadOnlyList<string> ValidatePairing(IReadOnlyList<AudioEventTableRow> rows)
        {
            if (rows == null)
                return new[] { "[NOT-RUN 守卫 · 规则3] rows 字段缺失 —— 规则 6(配对表)不可执行" };

            var errors = new List<string>();
            for (int i = 0; i < rows.Count; i++)
            {
                AudioEventTableRow row = rows[i];
                if (row == null)
                    continue;
                if (!Contains(WhitelistCategories, row.WhitelistCategory) ||
                    !Contains(LegalTriggerSources, row.TriggerSource))
                    continue; // 规则 1 / 2 已报,不在本规则重复

                if (!IsLegalCategoryTriggerPair(row.WhitelistCategory, row.TriggerSource))
                {
                    errors.Add(
                        $"{RowLabel(row, i)} 配对违例:whitelist_category = 「{row.WhitelistCategory}」 × " +
                        $"trigger_source = 「{row.TriggerSource}」 不在显式配对表内 —— AC-44-09 / 规则 6" +
                        "(单查各自 ∈ 集合**不够**;MusicLayer 只能配 EncounterMusicLayer)");
                }
            }

            return errors;
        }

        /// <summary>显式 5×5 配对表(规则 6;GDD :537-540)。前四类 = 同名自配;乐层类内容侧
        /// <c>MusicLayer</c> 只能配时机侧 <c>EncounterMusicLayer</c>(G1–G3 护栏挂在这条配对上)。
        /// <para>其余 20 种组合全部非法 —— 配对表是**闭集,定义在代码**,数据无法自行扩对。</para></summary>
        public static bool IsLegalCategoryTriggerPair(string whitelistCategory, string triggerSource)
        {
            if (whitelistCategory == CategoryMusicLayer)
                return triggerSource == TriggerEncounterMusicLayer;

            // 前四类:内容侧与时机侧**同名**才合法
            return whitelistCategory == triggerSource &&
                   Contains(WhitelistCategories, whitelistCategory);
        }

        // ══════════ AC-44-D9:每行 schema_version 齐 ══════════

        /// <summary>AC-44-D9:每行必带 <c>schema_version</c>,且**与顶层同值**。
        /// <para>三连 = **缺失 / 非整数 / 不匹配**(照抄 <c>ItemDatabaseBinder</c> 绑定入口先例;
        /// 顶层必填由 <see cref="ValidateSchemaVersion"/> 把关 —— 顶层 + 行级**双写**以兑现
        /// AC-44-D9「每行含」的字面要求,同时与 item 数据文件「顶层一个版本号」的先例调和)。
        /// 缺失走 NOT-RUN 守卫(缺键必须**加错误**,不得 <c>continue</c> 静默过)。</para></summary>
        /// <param name="rows">cue 行;null = <c>rows</c> 字段缺失(NOT-RUN)。</param>
        /// <param name="topLevelVersionRaw">顶层 <c>schema_version</c> 原文(null = 顶层缺失,
        /// 由顶层判据负责,此处不做不匹配比较)。</param>
        public static IReadOnlyList<string> ValidateRowSchemaVersions(
            IReadOnlyList<AudioEventTableRow> rows, string topLevelVersionRaw = null)
        {
            if (rows == null)
                return new[] { "[NOT-RUN 守卫 · 规则3] rows 字段缺失 —— AC-44-D9(schema_version)不可执行" };

            bool topLevelParsed = int.TryParse(topLevelVersionRaw, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out int topVersion);

            var errors = new List<string>();
            for (int i = 0; i < rows.Count; i++)
            {
                AudioEventTableRow row = rows[i];
                string label = RowLabel(row, i);

                if (row == null || string.IsNullOrEmpty(row.SchemaVersionRaw))
                {
                    errors.Add(
                        $"[NOT-RUN 守卫 · 规则3] {label} 缺 schema_version 字段 —— AC-44-D9:" +
                        "每行须含 whitelist_category + trigger_source + schema_version(缺一 ⇒ 构建失败)");
                    continue;
                }

                if (!int.TryParse(row.SchemaVersionRaw, NumberStyles.Integer, CultureInfo.InvariantCulture,
                        out int rowVersion))
                {
                    errors.Add(
                        $"{label} schema_version = {row.SchemaVersionRaw} 非整数字面量 —— AC-44-D9(类型)");
                    continue;
                }

                if (topLevelParsed && rowVersion != topVersion)
                {
                    errors.Add(
                        $"{label} schema_version = {rowVersion} ≠ 顶层 schema_version = {topVersion} —— " +
                        "AC-44-D9(不匹配):行级版本须与文件级同值(绑定入口三连:缺失 / 非整数 / 不匹配)");
                }
            }

            return errors;
        }

        // ══════════ 规则 5:tier_map 三键 + 六列集 ══════════

        /// <summary>规则 5 上半:顶层 <c>tier_map</c> 必须存在、键集恰 = {"0","1","2"}、每档列集恰 = F-44.1 的 6 列。</summary>
        /// <param name="table">事件表(null ⇒ NOT-RUN)。</param>
        public static IReadOnlyList<string> ValidateTierMap(AudioEventTable table)
        {
            if (table == null)
                return new[] { "[NOT-RUN 守卫 · 规则3] 事件表缺失(null),tier_map 不可校验" };

            if (table.TierMap == null)
            {
                return new[]
                {
                    "[NOT-RUN 守卫 · 规则3] 顶层 tier_map 字段缺失 —— 规则 5(设备级滤波表,F4=甲「一张」)" +
                    "不可执行;per-cue 不得替代设备级滤波",
                };
            }

            var errors = new List<string>();

            foreach (string key in TierMapKeys)
            {
                if (!table.TierMap.TryGetValue(key, out IReadOnlyDictionary<string, string> columns) ||
                    columns == null)
                {
                    errors.Add($"[规则5] tier_map 缺档位键「{key}」—— 键集须恰 = {{{string.Join(", ", TierMapKeys)}}}");
                    continue;
                }

                foreach (string required in TierMapColumns)
                {
                    if (!columns.ContainsKey(required))
                        errors.Add($"[规则5] tier_map[\"{key}\"].{required} 缺失 —— F-44.1 定型列集恰 6 列(唯一出处)");
                }

                foreach (string present in columns.Keys)
                {
                    if (!Contains(TierMapColumns, present))
                        errors.Add(
                            $"[规则5] tier_map[\"{key}\"].{present} ∉ F-44.1 定型列集 " +
                            $"{{{string.Join(", ", TierMapColumns)}}}");
                }
            }

            foreach (string present in table.TierMap.Keys)
            {
                if (!Contains(TierMapKeys, present))
                    errors.Add($"[规则5] tier_map 含档位键「{present}」∉ {{{string.Join(", ", TierMapKeys)}}}");
            }

            return errors;
        }

        // ══════════ 规则 5 下半:per-row tier_params 禁滤波列 ══════════

        /// <summary>规则 5 下半:行级 <c>tier_params</c> 只容非滤波的 per-cue 覆盖 ——
        /// **禁含 F-44.1 的任何滤波列**(滤波只住设备级 <c>tier_map</c>,F4=甲 一张表)。
        /// <para>⚠️ **GAP(登记,勿当已闭)**:此处按 6 列名做**黑名单精确匹配** —— GDD 只给了
        /// 排除法(「禁含滤波列」),**未枚举 tier_params 的允许键集**;正向未知键白名单待 GDD 枚举
        /// 后再收(提前收紧 = 对合法 per-cue 覆盖假红)。</para></summary>
        /// <param name="rows">cue 行;null = <c>rows</c> 字段缺失(NOT-RUN)。</param>
        public static IReadOnlyList<string> ValidateTierParamsFilters(IReadOnlyList<AudioEventTableRow> rows)
        {
            if (rows == null)
                return new[] { "[NOT-RUN 守卫 · 规则3] rows 字段缺失 —— 规则 5(tier_params 滤波列)不可执行" };

            var errors = new List<string>();
            for (int i = 0; i < rows.Count; i++)
            {
                AudioEventTableRow row = rows[i];
                if (row?.TierParams == null)
                    continue;
                string label = RowLabel(row, i);

                foreach (KeyValuePair<string, IReadOnlyDictionary<string, string>> tier in row.TierParams)
                {
                    // 档位键检查(2026-09-26 审查 BLOCKING 修):键必须 ∈ {0,1,2} ——
                    // 扁平形状(键 = 列名)在此拒收(原扁平夹具绕开 GDD 嵌套形状 = 假绿源)。
                    if (!Contains(TierMapKeys, tier.Key))
                    {
                        errors.Add(
                            $"{label} tier_params 档位键「{tier.Key}」∉ {{{string.Join(", ", TierMapKeys)}}}" +
                            " —— 规则 5 / GDD :487 行级形状 = per-tier 嵌套(扁平形状在此拒收)");
                        continue;
                    }

                    if (tier.Value == null)
                    {
                        errors.Add(
                            $"[NOT-RUN 守卫 · 规则3] {label} tier_params[\"{tier.Key}\"] 缺对象体 —— " +
                            "规则 5(列集)不可执行,不得静默通过");
                        continue;
                    }

                    foreach (KeyValuePair<string, string> column in tier.Value)
                    {
                        if (Contains(TierMapColumns, column.Key))
                        {
                            errors.Add(
                                $"{label} tier_params[\"{tier.Key}\"] 含滤波列「{column.Key}」—— 规则 5 / F4=甲:" +
                                "滤波参数只住设备级 tier_map,行级只容非滤波的 per-cue 覆盖(构建失败)");
                        }
                    }
                }
            }

            return errors;
        }

        // ══════════ 规则 7:空表 / 必需 cue 缺失 ══════════

        /// <summary>规则 7:<c>rows</c> 缺失 / 0 行 / 缺任一**必需 cue** ⇒ 失败(堵「0 行全绿」假象)。</summary>
        /// <param name="rows">cue 行;null = <c>rows</c> 字段缺失(NOT-RUN)。</param>
        public static IReadOnlyList<string> ValidateRequiredCues(IReadOnlyList<AudioEventTableRow> rows)
        {
            if (rows == null)
            {
                return new[]
                {
                    "[NOT-RUN 守卫 · 规则3] rows 字段缺失 —— 规则 7(空表 / 必需 cue)不可执行;" +
                    "字段缺失 = 报错退出,不得静默通过",
                };
            }

            if (rows.Count == 0)
            {
                return new[]
                {
                    "[AC-44-09 · 规则7] 空表(0 行)—— 0 行全绿假象拒收;" +
                    $"必需 cue 清单 = 构建期常量 {{{string.Join(", ", RequiredCues)}}}",
                };
            }

            var errors = new List<string>();
            var present = new HashSet<string>(StringComparer.Ordinal);
            foreach (AudioEventTableRow row in rows)
            {
                if (row != null && !string.IsNullOrEmpty(row.Cue))
                    present.Add(row.Cue);
            }

            foreach (string required in RequiredCues)
            {
                if (!present.Contains(required))
                {
                    errors.Add(
                        $"[AC-44-09 · 规则7] 缺必需 cue 行「{required}」—— 语声 / 呼吸 / 世界语境族最小集 " +
                        "(构建期常量 RequiredCues;第三负向)");
                }
            }

            return errors;
        }

        // ══════════ 规则 9:cue 唯一(2026-09-26 审查 REC;GDD 校验规则同批补条)══════════

        /// <summary>同名 cue 行唯一:<c>clock_ref</c> 经 <c>byCue</c> **首配**解析、必需集用
        /// HashSet 聚合 —— 重复行在两处都静默(歧义 + 掩盖),且运行期同样首配 ⇒ 构建期拒。</summary>
        /// <param name="rows">cue 行;null = <c>rows</c> 字段缺失(NOT-RUN)。</param>
        public static IReadOnlyList<string> ValidateCueUniqueness(IReadOnlyList<AudioEventTableRow> rows)
        {
            if (rows == null)
                return new[] { "[NOT-RUN 守卫 · 规则3] rows 字段缺失 —— 规则 9(cue 唯一)不可执行" };

            var errors = new List<string>();
            var seen = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < rows.Count; i++)
            {
                AudioEventTableRow row = rows[i];
                if (row == null || string.IsNullOrEmpty(row.Cue))
                    continue;
                if (seen.TryGetValue(row.Cue, out int first))
                {
                    errors.Add(
                        $"{RowLabel(row, i)} cue「{row.Cue}」重复(首现于行 {first})—— 规则 9:" +
                        "clock_ref 首配歧义 + 必需集聚合掩盖(2026-09-26 审查 REC;GDD 校验规则 9)");
                }
                else seen[row.Cue] = i;
            }
            return errors;
        }

        // ══════════ 规则 8:循环字段闭合(clock_ref + adventitious_policy + 相位窗)══════════

        /// <summary>规则 8(AC-44-14 / AC-44-08 [A]):附加音层行必带 <c>loop: true</c> +
        /// <c>clock_ref</c>(指向存在的基础层行,悬空 = 失败)+ <c>adventitious_policy{trigger_phase, jitter}</c>;
        /// <c>trigger_phase ∈ [0.8, 1.0]</c>(**相对吸气段的分数**,见文件头编码决定),±jitter 不得出吸气段。</summary>
        /// <param name="rows">cue 行;null = <c>rows</c> 字段缺失(NOT-RUN)。</param>
        public static IReadOnlyList<string> ValidateLoopClock(IReadOnlyList<AudioEventTableRow> rows)
        {
            if (rows == null)
                return new[] { "[NOT-RUN 守卫 · 规则3] rows 字段缺失 —— 规则 8(clock_ref / 相位窗)不可执行" };

            var errors = new List<string>();
            var byCue = new Dictionary<string, AudioEventTableRow>(StringComparer.Ordinal);
            foreach (AudioEventTableRow row in rows)
            {
                if (row != null && !string.IsNullOrEmpty(row.Cue) && !byCue.ContainsKey(row.Cue))
                    byCue[row.Cue] = row;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                AudioEventTableRow row = rows[i];
                if (row == null)
                    continue;

                // 行判定器:抽为 IsAdventitiousRow 单一出处(规则 8 与 AC-44-01 ① 存在性共用)
                if (!IsAdventitiousRow(row))
                    continue;

                string label = RowLabel(row, i);

                if (!row.Loop)
                    errors.Add($"{label} 附加音层行 loop ≠ true —— 规则 8(附加层随基础循环,非独立循环)");

                if (string.IsNullOrEmpty(row.ClockRef))
                {
                    errors.Add(
                        $"[NOT-RUN 守卫 · 规则3] {label} 附加音层行缺 clock_ref —— 规则 8 / AC-44-14:" +
                        "相位锁定不可执行,不得静默通过");
                }
                else if (!byCue.TryGetValue(row.ClockRef, out AudioEventTableRow target))
                {
                    errors.Add(
                        $"{label} clock_ref = 「{row.ClockRef}」**悬空**(表内无此 cue 行)—— " +
                        "规则 8 / AC-44-14:必须指向存在的基础层行(构建失败)");
                }
                else if (!target.Loop ||
                         !string.IsNullOrEmpty(target.ClockRef) ||
                         target.WhitelistCategory != "ContinuousPhysiology")
                {
                    errors.Add(
                        $"{label} clock_ref = 「{row.ClockRef}」指向的行不是基础气流层行" +
                        $"(target.loop = {target.Loop}, target.clock_ref = {(target.ClockRef ?? "缺失")}, " +
                        $"target.whitelist_category = {target.WhitelistCategory ?? "缺失"})" +
                        " —— 规则 8 / AC-44-14:基础层 = 持续生理声的自循环行,且自身不再引用别的时钟");
                }

                if (row.Policy == null)
                {
                    errors.Add(
                        $"[NOT-RUN 守卫 · 规则3] {label} 附加音层行缺 adventitious_policy —— " +
                        "规则 8 / AC-44-08 [A] 的相位窗载体缺失,不得静默通过");
                    continue;
                }

                errors.AddRange(ValidatePhaseWindow(label, row.Policy));
            }

            return errors;
        }

        /// <summary>相位窗子判据(规则 8):<c>trigger_phase</c> 必须是**数字字面量**且 ∈ [0.8, 1.0];
        /// <c>jitter</c> 必须是数字字面量且 ≥ 0;±jitter 后的窗口须仍 ⊆ 吸气段 [0, 1]。
        /// <para>**编码决定**:值 = 相对吸气段的分数(GDD「×吸气段时长、原点 = 吸气起点」的等价落码)。
        /// 旧规则「×基础周期 / ×全周期」写成的**非数字字面量**(如 <c>"0.95×全周期"</c>)在此拒收 ——
        /// 那是 F5 重钉前的编码,原窗口整段落呼气段(医学反相,GDD :576-584)。</para></summary>
        private static IEnumerable<string> ValidatePhaseWindow(string label, AdventitiousPolicyRaw policy)
        {
            if (string.IsNullOrEmpty(policy.TriggerPhaseRaw))
            {
                yield return
                    $"[NOT-RUN 守卫 · 规则3] {label} adventitious_policy 缺 trigger_phase —— 规则 8 / AC-44-14 ②";
            }
            else if (policy.TriggerPhaseRaw.StartsWith("\"", StringComparison.Ordinal))
            {
                yield return
                    $"{label} trigger_phase = {policy.TriggerPhaseRaw} 非数字字面量 —— 规则 8:" +
                    "trigger_phase 编码 = 相对吸气段的分数(数字 ∈ [0.8,1.0]);该形态是 F5 重钉前的 " +
                    "「×全周期 / ×基础周期」旧编码(落呼气段,已废,GDD :576-584)";
            }
            else if (!TryParseDouble(policy.TriggerPhaseRaw, out double phase))
            {
                yield return $"{label} trigger_phase = {policy.TriggerPhaseRaw} 不可解析为数字 —— 规则 8";
            }
            else if (phase < TriggerPhaseMin || phase > TriggerPhaseMax)
            {
                yield return
                    $"{label} trigger_phase = {Fmt(phase)} ∉ [{Fmt(TriggerPhaseMin)}, {Fmt(TriggerPhaseMax)}]" +
                    "(相对吸气段的分数)—— 规则 8 / F5 重钉:窗口 = 吸气段末 20%";
            }

            if (string.IsNullOrEmpty(policy.JitterRaw))
            {
                yield return
                    $"[NOT-RUN 守卫 · 规则3] {label} adventitious_policy 缺 jitter —— 规则 8(相位抖动载体)";
                yield break;
            }

            if (policy.JitterRaw.StartsWith("\"", StringComparison.Ordinal) ||
                !TryParseDouble(policy.JitterRaw, out double jitter))
            {
                yield return $"{label} jitter = {policy.JitterRaw} 不可解析为数字 —— 规则 8";
                yield break;
            }

            if (jitter < 0)
            {
                yield return $"{label} jitter = {Fmt(jitter)} < 0 —— 规则 8(抖动须为非负)";
                yield break;
            }

            // ±jitter 仍适用,但**不得越出吸气段**(GDD §Edge Cases 相位条 :581)
            if (string.IsNullOrEmpty(policy.TriggerPhaseRaw) ||
                policy.TriggerPhaseRaw.StartsWith("\"", StringComparison.Ordinal) ||
                !TryParseDouble(policy.TriggerPhaseRaw, out double center))
                yield break; // 相位本身已报错,不在本判据重复

            if (center - jitter < 0 || center + jitter > 1)
            {
                yield return
                    $"{label} 相位窗 [{Fmt(center - jitter)}, {Fmt(center + jitter)}] 越出吸气段 [0,1] —— " +
                    "规则 8 / GDD §Edge Cases:±jitter 不得出吸气段";
            }
        }

        // ══════════ AC-44-01 ①:附加音层行存在性(数据级结构前提)══════════

        /// <summary>附加音层行判定器(**单一出处**;规则 8 与 AC-44-01 ① 共用):四入口任一命中 ——
        /// cue 前缀(<see cref="AdventitiousCuePrefixes"/>)/ 素材前缀(<see cref="AdventitiousAssetPrefixes"/>)/
        /// <c>adventitious_policy</c> 在场 / <c>clock_ref</c> 在场。</summary>
        private static bool IsAdventitiousRow(AudioEventTableRow row)
        {
            return row != null &&
                   (HasAdventitiousPrefix(row.Cue) ||
                    row.Policy != null ||
                    !string.IsNullOrEmpty(row.ClockRef) ||
                    HasAdventitiousAsset(row.Assets));
        }

        /// <summary>AC-44-01 ①(存在半):<c>rows</c> 非空且**无任何附加音层行** ⇒ 失败 ——
        /// 附加层行缺失 = 该档附加音层为零(GDD AC-44-01 ① :990「附加音层行存在」的执行体;
        /// 2026-09-26 换载体后 ① 只是**结构前提**,「非静音」强判据在 ②③,不冒充增益判据)。
        /// <para><b>重复性说明</b>:六列齐已由 <see cref="ValidateTierMap"/> 覆盖(002 规则 5)、
        /// <c>loop ≠ true</c> 已由 <see cref="ValidateLoopClock"/> 覆盖(002 规则 8)—— 两者**不在此重复**;
        /// 本方法只补「一行都没有」时规则 8 的空转面(判定器无行可判 ⇒ 静默通过)。
        /// **空表(0 行)不在此报** —— 规则 7 已接管,同场景双报会打乱 002 现有
        /// <c>errors.Count</c> 断言。</para></summary>
        /// <param name="rows">cue 行;null = <c>rows</c> 字段缺失(NOT-RUN)。</param>
        public static IReadOnlyList<string> ValidateAdventitiousLayerPresence(IReadOnlyList<AudioEventTableRow> rows)
        {
            if (rows == null)
                return new[] { "[NOT-RUN 守卫 · 规则3] rows 字段缺失 —— AC-44-01 ①(附加音层行存在性)不可执行" };

            if (rows.Count == 0)
                return new string[0];   // 规则 7(空表)已报,不重复

            for (int i = 0; i < rows.Count; i++)
            {
                if (IsAdventitiousRow(rows[i]))
                    return new string[0];
            }

            return new[]
            {
                "[AC-44-01 ①] rows 非空但**无任何附加音层行**(cue 前缀 / 素材前缀 / " +
                "adventitious_policy / clock_ref 四入口全不命中)—— 附加层行缺失 = 该档附加音层为零" +
                "(GDD AC-44-01 ①「附加音层行存在」;结构前提,非增益判据)",
            };
        }

        /// <summary>**AC-44-01 ① 的单一聚合入口**(数据级结构前提,2026-09-26 换载体后口径):
        /// ① 三档六列齐 —— **委托** <see cref="ValidateTierMap"/>(002 规则 5 已实现,不造第二份);
        /// ② 附加音层行存在 —— <see cref="ValidateAdventitiousLayerPresence"/>;
        /// ③ 附加层行 <c>loop: true</c> + <c>clock_ref</c> / 相位窗完整 —— **委托**
        /// <see cref="ValidateLoopClock"/>(002 规则 8,含 AC-44-14 schema 半 / AC-44-08 [A] 窗口)。
        /// <para>⚠️ 本入口**不新增判据**、不替代 <see cref="Validate"/> 总门 —— 只为 AC 提供可直接
        /// 引用的聚合调用点(Story 004 测试 / Story 010 烘焙)。「非静音」的**强判据在
        /// AC-44-01 ②③**(.mixer 静音扫描见 <c>MixerTopologyGates.ValidateNoTierMuteFlags</c> /
        /// RMS 探针),① 只是结构前提(GDD :995-1001),**不冒充增益判据**。</para>
        /// <example>
        /// var errs = AudioEventTableGates.ValidateAc4401DataLevel(table);
        /// if (errs.Count &gt; 0) throw new Exception(string.Join("\n", errs));
        /// </example></summary>
        /// <param name="table">事件表(null ⇒ NOT-RUN)。</param>
        public static IReadOnlyList<string> ValidateAc4401DataLevel(AudioEventTable table)
        {
            if (table == null)
                return new[] { "[NOT-RUN 守卫 · 规则3] 事件表缺失(null)—— AC-44-01 ① 不可执行" };

            var errors = new List<string>();
            errors.AddRange(ValidateTierMap(table));
            errors.AddRange(ValidateAdventitiousLayerPresence(table.Rows));
            errors.AddRange(ValidateLoopClock(table.Rows));
            return errors;
        }

        // ══════════ xfade_ms 存在性 / 类型(数值断言归 Story 012)══════════

        /// <summary><c>xfade_ms</c>:**乐层行(<c>whitelist_category = MusicLayer</c>)必有且为正整数**;
        /// 其他行可选,但若在场必须是整数字面量。<para>
        /// **不做** <c>≥ MUSIC_XFADE_MIN_MS</c> 的数值断言(AC-44-18,Story 012)。</para></summary>
        /// <param name="rows">cue 行;null = <c>rows</c> 字段缺失(NOT-RUN)。</param>
        public static IReadOnlyList<string> ValidateXfade(IReadOnlyList<AudioEventTableRow> rows)
        {
            if (rows == null)
                return new[] { "[NOT-RUN 守卫 · 规则3] rows 字段缺失 —— xfade_ms 存在性校验不可执行" };

            var errors = new List<string>();
            for (int i = 0; i < rows.Count; i++)
            {
                AudioEventTableRow row = rows[i];
                if (row == null)
                    continue;

                bool music = row.WhitelistCategory == CategoryMusicLayer;
                string label = RowLabel(row, i);

                if (string.IsNullOrEmpty(row.XfadeRaw))
                {
                    if (music)
                    {
                        errors.Add(
                            $"{label} 乐层行缺 xfade_ms —— 交叉淡化 / 乐层切换时长必填" +
                            "(AC-44-09 · 存在性;数值下界归 Story 012 AC-44-18)");
                    }

                    continue;
                }

                if (!int.TryParse(row.XfadeRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int xfade))
                {
                    errors.Add($"{label} xfade_ms = {row.XfadeRaw} 非整数 int —— 类型校验(数值断言归 Story 012)");
                    continue;
                }

                if (music && xfade <= 0)
                    errors.Add($"{label} 乐层行 xfade_ms = {xfade} ≤ 0 —— 须为正 int(存在性 / 类型;下界归 Story 012)");
            }

            return errors;
        }

        // ══════════ subtitle_text:语声 / 口述族键集覆盖(AC-44-15 [A] 半)══════════

        /// <summary>语声 / 教学口述族 cue(前缀 ∈ <see cref="SubtitleCuePrefixes"/>)必须带**非空**
        /// <c>subtitle_text</c> —— 事件表 = 文本本体唯一出处(AC-44-15 [A];缺失字段 = NOT-RUN 守卫)。</summary>
        /// <param name="rows">cue 行;null = <c>rows</c> 字段缺失(NOT-RUN)。</param>
        public static IReadOnlyList<string> ValidateSubtitles(IReadOnlyList<AudioEventTableRow> rows)
        {
            if (rows == null)
                return new[] { "[NOT-RUN 守卫 · 规则3] rows 字段缺失 —— subtitle_text 键集覆盖不可执行" };

            var errors = new List<string>();
            for (int i = 0; i < rows.Count; i++)
            {
                AudioEventTableRow row = rows[i];
                if (row == null || !IsSubtitleCue(row.Cue))
                    continue;

                string label = RowLabel(row, i);
                if (row.SubtitleText == null)
                {
                    errors.Add(
                        $"[NOT-RUN 守卫 · 规则3] {label} 语声 / 口述族 cue 缺 subtitle_text 字段 —— " +
                        "AC-44-15 [A]:表键集须 ⊇ 语声 cue 集(缺失 = 构建失败)");
                }
                else if (string.IsNullOrWhiteSpace(row.SubtitleText))
                {
                    errors.Add($"{label} subtitle_text 为空 —— AC-44-15 [A](键在值空仍不可呈现)");
                }
            }

            return errors;
        }

        /// <summary>该 cue 是否属于需字幕的**语声 / 教学口述族**(前缀 ∈ <see cref="SubtitleCuePrefixes"/>,
        /// 序数比较)。</summary>
        public static bool IsSubtitleCue(string cue)
        {
            if (string.IsNullOrEmpty(cue))
                return false;

            foreach (string prefix in SubtitleCuePrefixes)
            {
                if (cue.StartsWith(prefix, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        // ══════════ 共用小件 ══════════

        /// <summary>附加音层行判定器(规则 8):cue ∈ <see cref="AdventitiousCuePrefixes"/> 前缀族。</summary>
        private static bool HasAdventitiousPrefix(string cue)
        {
            if (string.IsNullOrEmpty(cue))
                return false;

            foreach (string prefix in AdventitiousCuePrefixes)
            {
                if (cue.StartsWith(prefix, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        /// <summary>附加音层行判定器第四入口(2026-09-26 审查 REC):assets[] 含素材族前缀。</summary>
        private static bool HasAdventitiousAsset(IReadOnlyList<string> assets)
        {
            if (assets == null) return false;
            foreach (string asset in assets)
            {
                if (string.IsNullOrEmpty(asset)) continue;
                foreach (string prefix in AdventitiousAssetPrefixes)
                {
                    if (asset.StartsWith(prefix, StringComparison.Ordinal))
                        return true;
                }
            }
            return false;
        }

        /// <summary>行诊断标签(优先 cue,退回下标)。</summary>
        private static string RowLabel(AudioEventTableRow row, int index)
            => row != null && !string.IsNullOrEmpty(row.Cue) ? $"记录({row.Cue})" : $"行[{index}]";

        private static bool Contains(IReadOnlyList<string> set, string value)
        {
            if (value == null)
                return false;
            for (int i = 0; i < set.Count; i++)
            {
                if (string.Equals(set[i], value, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        /// <summary>恒等文化的 double 解析(禁当前区域敏感的默认重载)。</summary>
        private static bool TryParseDouble(string raw, out double value)
            => double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

        /// <summary>恒等文化的 double 格式化(诊断文本跨机器稳定;CI 日志不随区域变逗号小数点)。</summary>
        private static string Fmt(double value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
