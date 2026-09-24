// ═══════════════════════════════════════════════════════════════════════════
// ItemValidationGates.cs — 物品表写入期校验套件(Story 007)
//
// 权威件:
//   Story   : production/epics/item-database/story-007-item-validation-fixtures.md
//             (Acceptance Criteria + QA Test Cases 为唯一验收口径)
//   GDD     : design/gdd/item-database.md
//             夹具表      :1049–1074(七件夹具文件名与 AC 对照)
//             写入期校验族:755–830(各条规则原文)
//             Schema A    :283–303(legal_transitions / tcm_profile / inflicts_injury 字段定义)
//             States      :415–445(「枚举只给词汇不给通路」)
//   ADR     : ADR-014 数据管线与 JSON 解析器(全量校验失败 = 构建失败)
//   Control : docs/architecture/control-manifest.md Foundation 层
//
// 各 AC 覆盖:
//   AC-21a-13 : MAX_QUALITY < 2 或非整数拒绝(GDD :795 / :1061)—— 夹具 invalid_max_quality.json
//   AC-21a-14 : quality_distribution 支撑 ⊄ [1, MAX_QUALITY] 拒绝(D-21-7 双向耦合;
//               GDD :648–651 / :796 / :1062)—— 夹具 invalid_quality_dist.json
//   AC-21a-15 : stack_max < 1 或 weight ≤ 0 拒绝(规则六;GDD :185–191 / :797 / :1063)
//               —— 夹具 invalid_stack_weight.json(须覆盖 Fix 形式 "1/2" 拒 —— D-21-17)
//   AC-21a-23 : P0 期出现 P1a 值(honey_fried / dry_fried / 非空 tcm_profile)拒绝
//               (GDD :805 / :1071;对齐 8 的 AC-8-33 同型纪律)—— 夹具 invalid_p1a_leak.json
//   AC-21a-24 : 配方 state 对不在源物品声明的 legal_transitions 内拒绝
//               (GDD :415–445 / :806 / :1072)—— 夹具 invalid_transition.json
//   AC-21a-25 : 两个门 ——
//               (a) 类别门 + 外键门:category=weapon 无 inflicts_injury / 非 weapon 带该字段 /
//                   injury_id 悬空(9 的枚举),均拒(GDD :193–197 / :807 / :1073)
//                   —— 夹具 invalid_injury_fk.json
//               (b) 集合漂移门:21a 的 inflicts_injury ⊇ 该线全部动作的 maps_to_injury,
//                   违例 = 硬失败(承 25 的 A20;GDD :303 / :1073)—— 夹具 injury_set_drift.json
//
// ⚠️ 形态纪律(承 Story 006 RecipeValidationGates 模板):
//   - 全部方法:纯函数 · 无 I/O · 无静态可变态 · 零 throw · 返回错误列表(空 = 通过)
//   - 「显式 throw」的执法落点归 Story 008 管线(汇总错误列表后 throw)—— 本文件不 throw
//   - 单一实现委托:processing_state / category 字面量解析走 ItemDbValidation 的
//     TryParseProcessingState / TryParseItemCategory(Story 002 产物,禁另写 switch)
//   - 零调参字面量:规则阈值(MAX_QUALITY ≥ 2、stack_max ≥ 1、weight > 0)是规则本身,
//     可写整数字面量;平衡值(MAX_QUALITY 具体档数、SKILL_CAP 等)一律注入参数,禁硬编码
//
// ⚠️ 范围锁(本文件明确不覆盖,勿在此扩):
//   - 枚举闭合本身(AC-21 / AC-22,Story 002)—— AC-23 只判 honey_fried / dry_fried
//     两个已知 P1a 字面量,其他非法字面量(如 "banana")归 AC-22,不属本门
//   - 轴数组长度与地板(AC-50 / 50b / 60 / 61 / 62,Story 004)
//   - 配方表侧夹具(AC-7 ~ AC-20 / AC-66,Story 006)
//   - 管线执行本套校验的落点(Story 008)—— 本文件只提供纯函数,不接线
//   - 伤情语义(「哪次命中造成哪个伤」归 25 的 maps_to_injury 判定逻辑)—— 只断集合 ⊇ 关系
// ═══════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.EditorTools.Gates
{
    /// <summary>
    /// 物品表写入期校验套件(Story 007):针对 ItemDef 关键字段的六类硬失败规则
    /// (AC-13 / 14 / 15 / 23 / 24 / 25)。
    /// 全部方法为纯函数,无 I/O,无静态可变态,零 throw —— 聚合与 throw-at-caller 归 Story 008 管线。
    /// </summary>
    /// <example>
    /// <code>
    /// var errors = ItemValidationGates.ValidateMaxQuality("3.5", "max_quality 常量");
    /// if (errors.Count &gt; 0)
    /// {
    ///     // 示意(非可执行):Story 008 管线在此处汇总全部错误后显式 throw。
    ///     // 本类自身不 throw,只返回错误列表。
    ///     throw new InvalidOperationException(string.Join("\n", errors));
    /// }
    /// </code>
    /// </example>
    public static class ItemValidationGates
    {
        // ══════════ AC-21a-13:MAX_QUALITY ≥ 2 且为整数 ══════════

        /// <summary>
        /// 校验 MAX_QUALITY 常量:必须能解析为整数,且值 ≥ 2(品级维度必须存在,GDD :795)。
        /// </summary>
        /// <remarks>
        /// <para>
        /// 输入为作者态原始字面量而非 <c>int</c>,因为本 AC 必须能拒「非整数」形态
        /// (如 <c>"3.5"</c>、<c>"1/2"</c>)—— 光传 <c>int</c> 则绑定层已把这类拒掉,
        /// 门无法独立验证。字符串 <c>"4"</c>(合法整数的字符串写法)必须能解析成功,
        /// 不因「是字符串」被拒(QA Edge case)。
        /// </para>
        /// <para>
        /// D-21-7 双向耦合:MAX_QUALITY 变更须触发全分布重验(AC-14)—— 本方法只校验
        /// MAX_QUALITY 自身,重跑机制归 Story 008 管线编排,不在此处触发。
        /// </para>
        /// </remarks>
        /// <param name="rawMaxQualityLiteral">MAX_QUALITY 的原始作者态字面量(如 "5"、"3.5"、"1")。</param>
        /// <param name="recordLabel">错误信息中的记录标签(如 "max_quality 常量");null/空则用默认"记录"。</param>
        /// <returns>错误列表;空 = 通过。</returns>
        /// <example>
        /// 示意(非可执行):
        /// <code>
        /// ValidateMaxQuality("3.5", "max_quality 常量")  // =&gt; 1 条错误:非整数
        /// ValidateMaxQuality("1", "max_quality 常量")    // =&gt; 1 条错误:小于 2
        /// ValidateMaxQuality("4", "max_quality 常量")    // =&gt; 空(字符串形式的合法整数)
        /// </code>
        /// </example>
        public static IReadOnlyList<string> ValidateMaxQuality(
            string rawMaxQualityLiteral,
            string recordLabel)
        {
            var errors = new List<string>();
            string label = RecordPrefix(recordLabel);

            if (string.IsNullOrEmpty(rawMaxQualityLiteral) ||
                !int.TryParse(rawMaxQualityLiteral, System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out int parsed))
            {
                errors.Add(
                    $"{label} MAX_QUALITY = \"{rawMaxQualityLiteral}\" 不是合法整数 —— " +
                    "品级维度必须存在,非整数拒绝;构建期硬失败(AC-21a-13)");
                return errors;
            }

            if (parsed < 2)
            {
                errors.Add(
                    $"{label} MAX_QUALITY = {parsed} 小于 2 —— " +
                    "品级维度必须存在(至少两档);构建期硬失败(AC-21a-13)");
            }

            return errors;
        }

        // ══════════ AC-21a-14:quality_distribution 支撑 ⊆ [1, MAX_QUALITY] ══════════

        /// <summary>
        /// 校验 quality_distribution 的支撑点集合必须全部落在 <c>[1, maxQuality]</c> 闭区间内
        /// (D-21-7 双向耦合,GDD :648–651 / :796)。
        /// </summary>
        /// <remarks>
        /// <para>
        /// 只接收支撑点(档位索引),不接收权重 —— 因为 <c>GatherProfile.QualityDistribution</c>
        /// 的形状由系统 17 拥有(P0 是空占位结构,见 GDD :365),本门无从得知权重字段。
        /// QA Edge case「权重为 0 的档仍计支撑」由**调用方**保证:任何档位(含权重为 0 的档)
        /// 都须把其索引传入 <paramref name="supportPoints"/> —— 本门不豁免任何传入的点。
        /// </para>
        /// <para>
        /// D-21-7 的「MAX_QUALITY 后下调须重跑本校验」不在此方法内触发 —— 重跑编排归
        /// Story 008 管线;本方法只提供「给定当前 maxQuality 判一次」的纯函数。
        /// </para>
        /// </remarks>
        /// <param name="supportPoints">质量分布的支撑点(档位索引)列表;null 视为空列表(通过)。</param>
        /// <param name="maxQuality">当前 MAX_QUALITY 值(注入,禁硬编码具体档数)。</param>
        /// <param name="recordLabel">错误信息中的记录标签;null/空则用默认"记录"。</param>
        /// <returns>错误列表;空 = 通过。</returns>
        /// <example>
        /// 示意(非可执行):
        /// <code>
        /// ValidateQualityDistribution(new[] {1, 2, 5}, 5, "gather")  // =&gt; 空(全在界内)
        /// ValidateQualityDistribution(new[] {1, 0, 3}, 5, "gather")  // =&gt; 1 条:点 0 越界
        /// </code>
        /// </example>
        public static IReadOnlyList<string> ValidateQualityDistribution(
            IReadOnlyList<int> supportPoints,
            int maxQuality,
            string recordLabel)
        {
            var errors = new List<string>();
            string label = RecordPrefix(recordLabel);

            if (supportPoints == null)
            {
                return errors;
            }

            for (int i = 0; i < supportPoints.Count; i++)
            {
                int point = supportPoints[i];
                if (point < 1 || point > maxQuality)
                {
                    errors.Add(
                        $"{label} quality_distribution 支撑点 {point} 越出 [1, {maxQuality}] —— " +
                        "支撑必须 ⊆ [1, MAX_QUALITY](D-21-7);构建期硬失败(AC-21a-14)");
                }
            }

            return errors;
        }

        // ══════════ AC-21a-15:stack_max ≥ 1 且 weight ≥ 1(int 非 Fix) ══════════

        /// <summary>
        /// 校验 stack_max ≥ 1 且 weight ≥ 1,且两者必须是合法整数(规则六;D-21-17)。
        /// </summary>
        /// <remarks>
        /// <para>
        /// 输入为原始字面量而非 <c>int</c>,因为本 AC 必须能拒两类「类型错误」形态:
        /// Fix 形式 <c>"1/2"</c>(weight 是 int 计数,非 Fix,D-21-17,Control Manifest
        /// Guardrail)与浮点形式 <c>"1.5"</c> —— 传 <c>int</c> 则绑定层已拒,门无法独立验证。
        /// </para>
        /// <para>
        /// 「stack_max = 1」合法(不可堆叠),不因 &lt; 1 被拒;「weight = 1」合法(最小正单位)。
        /// </para>
        /// </remarks>
        /// <param name="rawStackMax">stack_max 的原始作者态字面量(如 "0"、"1"、"1/2")。</param>
        /// <param name="rawWeight">weight 的原始作者态字面量(如 "0"、"-3"、"1/2"、"1.5")。</param>
        /// <param name="recordLabel">错误信息中的记录标签;null/空则用默认"记录"。</param>
        /// <returns>错误列表;空 = 通过。</returns>
        /// <example>
        /// 示意(非可执行):
        /// <code>
        /// ValidateStackWeight("1", "1", "herb")      // =&gt; 空
        /// ValidateStackWeight("0", "1", "herb")      // =&gt; 1 条:stack_max &lt; 1
        /// ValidateStackWeight("1", "1/2", "herb")    // =&gt; 1 条:weight 非整数(Fix 形式拒)
        /// </code>
        /// </example>
        public static IReadOnlyList<string> ValidateStackWeight(
            string rawStackMax,
            string rawWeight,
            string recordLabel)
        {
            var errors = new List<string>();
            string label = RecordPrefix(recordLabel);

            if (string.IsNullOrEmpty(rawStackMax) ||
                !int.TryParse(rawStackMax, System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out int stackMax))
            {
                errors.Add(
                    $"{label} stack_max = \"{rawStackMax}\" 不是合法整数 —— " +
                    "stack_max 是 int 计数非 Fix(D-21-17);构建期硬失败(AC-21a-15)");
            }
            else if (stackMax < 1)
            {
                errors.Add(
                    $"{label} stack_max = {stackMax} 小于 1 —— " +
                    "堆叠基数至少为 1(规则六);构建期硬失败(AC-21a-15)");
            }

            if (string.IsNullOrEmpty(rawWeight) ||
                !int.TryParse(rawWeight, System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out int weight))
            {
                errors.Add(
                    $"{label} weight = \"{rawWeight}\" 不是合法整数 —— " +
                    "weight 是 int 计数非 Fix(D-21-17),Fix 形式与浮点形式均拒;构建期硬失败(AC-21a-15)");
            }
            else if (weight <= 0)
            {
                errors.Add(
                    $"{label} weight = {weight} 不大于 0 —— " +
                    "weight 必须为正整数(规则六);构建期硬失败(AC-21a-15)");
            }

            return errors;
        }

        // ══════════ AC-21a-23:P0 收窄 —— 拒 honey_fried / dry_fried / 非空 tcm_profile ══════════

        /// <summary>
        /// 校验 P0 期不出现 P1a 值:processing_state 不得是 honey_fried / dry_fried,
        /// tcm_profile 块不得含任何非 null 值(GDD :805,对齐 8 的 AC-8-33 同型纪律)。
        /// </summary>
        /// <remarks>
        /// <para>
        /// 输入为原始字面量 / 原始 JSON 块体而非已绑定类型,因为 <c>TcmProfile</c> 是空
        /// 占位结构(GDD :369「字段表由 P1a 撰写时补,本节此处仅占位」)—— 绑定后无法区分
        /// 「块存在但全为 null」(占位,D-21-6,合法)与「块含任一非 null 值」(P1a 值泄漏)。
        /// </para>
        /// <para>
        /// <paramref name="rawProcessingState"/> 只判两个已知 P1a 字面量(honey_fried / dry_fried),
        /// **不**做枚举闭合 —— 其他非法字面量(如 "banana")归 AC-22(Story 002),
        /// 两条门不互相替代(QA Edge case:honey_fried 先被 AC-22 拒,本门仍须独立能拒它)。
        /// </para>
        /// <para>
        /// <paramref name="rawTcmProfileBody"/> 语义:null 或空串 = 字段缺省 / JSON null → 通过;
        /// 仅含 null 值的占位块(如 <c>{"p1a_field":null}</c>)→ 通过(D-21-6);
        /// 含任一非 null 值 → 拒绝。
        /// </para>
        /// <para>P1a 解锁本值时须修改本校验(登记于 GDD :805)。</para>
        /// </remarks>
        /// <param name="rawProcessingState">processing_state 的原始作者态字面量(如 "honey_fried"、"raw")。</param>
        /// <param name="rawTcmProfileBody">tcm_profile 块的原始 JSON 体(不含外层花括号);
        /// null / 空串表示字段缺省或 JSON null。</param>
        /// <param name="recordLabel">错误信息中的记录标签;null/空则用默认"记录"。</param>
        /// <returns>错误列表;空 = 通过。</returns>
        /// <example>
        /// 示意(非可执行):
        /// <code>
        /// ValidateP0Narrowing("honey_fried", null, "herb")              // =&gt; 1 条
        /// ValidateP0Narrowing("raw", "p1a_field\":null", "herb")        // =&gt; 空(全 null 占位)
        /// ValidateP0Narrowing("raw", "p1a_field\":\"nan\"", "herb")    // =&gt; 1 条:非空 tcm_profile
        /// </code>
        /// </example>
        public static IReadOnlyList<string> ValidateP0Narrowing(
            string rawProcessingState,
            string rawTcmProfileBody,
            string recordLabel)
        {
            var errors = new List<string>();
            string label = RecordPrefix(recordLabel);

            if (string.Equals(rawProcessingState, "honey_fried", StringComparison.Ordinal) ||
                string.Equals(rawProcessingState, "dry_fried", StringComparison.Ordinal))
            {
                errors.Add(
                    $"{label} processing_state = \"{rawProcessingState}\" 是 P1a 值 —— " +
                    "P0 期只允许 5 值枚举;构建期硬失败(AC-21a-23)");
            }

            if (TcmProfileBodyHasNonNullValue(rawTcmProfileBody))
            {
                errors.Add(
                    $"{label} tcm_profile 含非 null 值 —— " +
                    "P0 恒空,占位块字段须全为 null(D-21-6);构建期硬失败(AC-21a-23)");
            }

            return errors;
        }

        // ══════════ AC-21a-24:配方 state 对 ∈ 源物品 legal_transitions ══════════

        /// <summary>
        /// 校验配方的 boundary_state 每一对 (From, To) 都在源物品声明的 legal_transitions 内
        /// (「枚举只给词汇不给通路」,GDD :415–445 / :806)。
        /// </summary>
        /// <remarks>
        /// <para>
        /// <paramref name="sourceItemLegalTransitions"/> 是**源 / 输入侧**物品的声明(调用方负责
        /// 只传入该物品的列表,不传输出侧物品的)—— 本门从签名上只接收一个列表,
        /// 结构性杜绝「拿输出侧物品的声明凑数」(QA Edge case:跨 base 配方查输入侧声明)。
        /// </para>
        /// <para>
        /// 声明的作者态字符串格式由本门**单点定义**(GDD 只定 <c>string[]</c> 类型,未定字面格式):
        /// 接受 <c>"raw>dried"</c> 与 <c>"raw→dried"</c> 两种箭头(U+003E 或 U+2192),
        /// 两侧状态字面量经 <see cref="ItemDbValidation.TryParseProcessingState"/> 解析
        /// (单一实现,不另写 switch)。解析失败的声明条目跳过(不匹配任何对)——
        /// 字面量本身的枚举闭合归 AC-22,不属本门。
        /// </para>
        /// <para>
        /// 方向单遍:只迭代 boundary_state → 声明,不迭代声明 → boundary_state。
        /// 「声明了但配方未用」因此天然通过(声明是超集许可,QA Edge case)。
        /// 「声明为空 + 任意 boundary」因此拒(空集不含任何对)。
        /// </para>
        /// </remarks>
        /// <param name="sourceItemLegalTransitions">源 / 输入侧物品声明的 legal_transitions 原始字符串列表;null 视为空列表。</param>
        /// <param name="recipeBoundaryState">配方解析后的 boundary_state 状态对;null 视为空列表(通过)。</param>
        /// <param name="recordLabel">错误信息中的记录标签;null/空则用默认"记录"。</param>
        /// <returns>错误列表;空 = 通过。</returns>
        /// <example>
        /// 示意(非可执行):
        /// <code>
        /// ValidateTransition(
        ///     new[] {"raw>dried", "dried>extracted"},
        ///     new[] { new ProcessingTransition(ProcessingState.Raw, ProcessingState.Dried) },
        ///     "recipe_x");                                                     // =&gt; 空
        /// ValidateTransition(
        ///     new[] {"raw>dried"},
        ///     new[] { new ProcessingTransition(ProcessingState.Raw, ProcessingState.Extracted) },
        ///     "recipe_x");                                                     // =&gt; 1 条:未声明
        /// </code>
        /// </example>
        public static IReadOnlyList<string> ValidateTransition(
            IReadOnlyList<string> sourceItemLegalTransitions,
            IReadOnlyList<ProcessingTransition> recipeBoundaryState,
            string recordLabel)
        {
            var errors = new List<string>();
            string label = RecordPrefix(recordLabel);

            if (recipeBoundaryState == null || recipeBoundaryState.Count == 0)
            {
                return errors;
            }

            var declared = ParseDeclaredTransitions(sourceItemLegalTransitions);

            for (int i = 0; i < recipeBoundaryState.Count; i++)
            {
                ProcessingTransition pair = recipeBoundaryState[i];
                if (!declared.Contains(pair))
                {
                    errors.Add(
                        $"{label} 配方 state 对 {pair} 不在源物品声明的 legal_transitions 内 —— " +
                        "「枚举只给词汇不给通路」;构建期硬失败(AC-21a-24)");
                }
            }

            return errors;
        }

        // ══════════ AC-21a-25(a):weapon 类别门 + injury_id 外键门 ══════════

        /// <summary>
        /// 校验 inflicts_injury 字段的存在性(类别门)与 injury_id 外键有效性(外键门)。
        /// </summary>
        /// <remarks>
        /// <para>
        /// 只校验存在性与类别门,**不校验语义**(「哪次命中造成哪个伤」归 25 的
        /// <c>maps_to_injury</c> 判定逻辑,GDD :303 / :1073)—— 本门从不读取、也不要求
        /// 传入伤害语义数据,结构性防止测试过度断言。
        /// </para>
        /// <para>
        /// <paramref name="knownInjuryIds"/> 是 9 的伤情枚举(注入,禁在本门硬编码枚举值)。
        /// null 视为空集 ⇒ 任何引用都悬空(与 Story 006 <c>ValidateRecipeForeignKeys</c>
        /// null→空的口径一致)。
        /// </para>
        /// <para>
        /// 单值 vs 数组的作者态形式差异由**调用方**归一为列表后传入(形态归 OQ / 数据轮,
        /// GDD :303),本门只接收 <see cref="IReadOnlyList{T}"/>。
        /// </para>
        /// </remarks>
        /// <param name="category">物品类别(已解析的枚举;字面量闭合归 AC-22,调用方负责解析)。</param>
        /// <param name="inflictsInjury">inflictsInjury 字段值列表;null / 空列表 = 字段缺省或空。</param>
        /// <param name="knownInjuryIds">9 拥有的合法 injury_id 集合(注入);null 视为空集。</param>
        /// <param name="recordLabel">错误信息中的记录标签;null/空则用默认"记录"。</param>
        /// <returns>错误列表;空 = 通过。</returns>
        /// <example>
        /// 示意(非可执行):
        /// <code>
        /// ValidateInjuryBinding(ItemCategory.Weapon, null, known, "sword");            // =&gt; 1 条:缺字段
        /// ValidateInjuryBinding(ItemCategory.Material, new[] {"cut"}, known, "herb");  // =&gt; 1 条:非 weapon 带字段
        /// ValidateInjuryBinding(ItemCategory.Weapon, new[] {"ghost"}, known, "sword"); // =&gt; 1 条:外键悬空
        /// </code>
        /// </example>
        public static IReadOnlyList<string> ValidateInjuryBinding(
            ItemCategory category,
            IReadOnlyList<string> inflictsInjury,
            ISet<string> knownInjuryIds,
            string recordLabel)
        {
            var errors = new List<string>();
            string label = RecordPrefix(recordLabel);
            bool hasField = inflictsInjury != null && inflictsInjury.Count > 0;

            if (category == ItemCategory.Weapon && !hasField)
            {
                errors.Add(
                    $"{label} category = weapon 却无 inflicts_injury —— " +
                    "规则四:武器须声明可产生伤情集合;构建期硬失败(AC-21a-25)");
                return errors;
            }

            if (category != ItemCategory.Weapon && hasField)
            {
                errors.Add(
                    $"{label} category = {category} 非 weapon 却带 inflicts_injury —— " +
                    "规则四 / 规则七反向校验:仅 weapon 可声明;构建期硬失败(AC-21a-25)");
                return errors;
            }

            if (hasField && knownInjuryIds != null)
            {
                for (int i = 0; i < inflictsInjury.Count; i++)
                {
                    string injuryId = inflictsInjury[i];
                    if (!knownInjuryIds.Contains(injuryId))
                    {
                        errors.Add(
                            $"{label} inflicts_injury 引用的 injury_id \"{injuryId}\" 不在 9 的枚举内 —— " +
                            "外键悬空;构建期硬失败(AC-21a-25)");
                    }
                }
            }

            return errors;
        }

        // ══════════ AC-21a-25(b):集合漂移门 —— inflicts_injury ⊇ maps_to_injury ══════════

        /// <summary>
        /// 校验 21a 的 inflicts_injury 集合必须包含该线全部动作的 maps_to_injury(⊇ 含等号),
        /// 违例 = 构建期硬失败(承 25 的 A20,GDD :303 / :1073)。
        /// </summary>
        /// <remarks>
        /// <para>
        /// 方向单遍:只查 maps_to_injury ⊆ inflicts_injury,不查反向 —— 「25 侧删动作后
        /// inflicts_injury 悬空」明确不在本 AC(QA Edge case,注记归 25),本门不实现、
        /// 也禁止调用方期待它实现。
        /// </para>
        /// <para>
        /// 集合恰好相等 ⇒ 通过(⊇ 取等)。null 集合视为空集 ⇒ <paramref name="mapsToInjury"/>
        /// 中任一值都违反;<paramref name="mapsToInjury"/> 为 null ⇒ 平凡通过。
        /// </para>
        /// <para>
        /// <paramref name="mapsToInjury"/> 是 25 侧数据(注入,本门不读取 25 的内部结构)。
        /// </para>
        /// </remarks>
        /// <param name="inflictsInjury">21a 物品声明的 inflicts_injury 集合;null 视为空集。</param>
        /// <param name="mapsToInjury">该线全部动作的 maps_to_injury 并集(注入);null 视为空集(通过)。</param>
        /// <param name="recordLabel">错误信息中的记录标签;null/空则用默认"记录"。</param>
        /// <returns>错误列表;空 = 通过。</returns>
        /// <example>
        /// 示意(非可执行):
        /// <code>
        /// ValidateInjurySetSuperset(
        ///     new HashSet&lt;string&gt; {"cut", "contusion"},
        ///     new HashSet&lt;string&gt; {"cut"}, "line");                            // =&gt; 空(⊇ 含等)
        /// ValidateInjurySetSuperset(
        ///     new HashSet&lt;string&gt; {"cut"},
        ///     new HashSet&lt;string&gt; {"cut", "puncture"}, "line");               // =&gt; 1 条:漂移
        /// </code>
        /// </example>
        public static IReadOnlyList<string> ValidateInjurySetSuperset(
            ISet<string> inflictsInjury,
            ISet<string> mapsToInjury,
            string recordLabel)
        {
            var errors = new List<string>();
            string label = RecordPrefix(recordLabel);

            if (mapsToInjury == null || mapsToInjury.Count == 0)
            {
                return errors;
            }

            foreach (string injuryId in mapsToInjury)
            {
                if (inflictsInjury == null || !inflictsInjury.Contains(injuryId))
                {
                    errors.Add(
                        $"{label} maps_to_injury 值 \"{injuryId}\" 不在 21a 的 inflicts_injury 集合内 —— " +
                        "集合漂移(inflicts_injury 须 ⊇ maps_to_injury);构建期硬失败(AC-21a-25)");
                }
            }

            return errors;
        }

        // ══════════ 私有辅助 ══════════

        /// <summary>
        /// 判定 tcm_profile 原始块体是否含任一非 null 值(AC-23)。
        /// null / 空白体 = 无内容(缺省 / JSON null);仅含 null 值的键 = 占位(D-21-6,通过);
        /// 含任一非 null 值 = P1a 值泄漏(拒)。单层花括号嵌套够用(TcmProfile P0 无嵌套块)。
        /// </summary>
        private static bool TcmProfileBodyHasNonNullValue(string rawBody)
        {
            if (string.IsNullOrWhiteSpace(rawBody))
            {
                return false;
            }

            // 匹配 "key" : value —— value 取到下一个逗号 / 右花括号 / 行尾为止。
            var matches = System.Text.RegularExpressions.Regex.Matches(
                rawBody,
                "\"[^\"]+\"\\s*:\\s*([^,}\\r\\n]+)");

            if (matches.Count == 0)
            {
                // 无 "key":value 对(如空块 "{}")—— 视为无内容。
                return false;
            }

            foreach (System.Text.RegularExpressions.Match m in matches)
            {
                string value = m.Groups[1].Value.Trim().Trim('"');
                if (!string.Equals(value, "null", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 解析 legal_transitions 声明字符串列表为已解析状态对集合(AC-24)。
        /// 每条接受 <c>"from&gt;to"</c> 或 <c>"from→to"</c>(两侧允许空白);解析失败的条目跳过
        /// (字面量闭合归 AC-22,不属本门)。null / 空列表返回空集。
        /// </summary>
        private static HashSet<ProcessingTransition> ParseDeclaredTransitions(
            IReadOnlyList<string> rawDeclarations)
        {
            var declared = new HashSet<ProcessingTransition>();

            if (rawDeclarations == null)
            {
                return declared;
            }

            for (int i = 0; i < rawDeclarations.Count; i++)
            {
                string raw = rawDeclarations[i];
                if (string.IsNullOrEmpty(raw))
                {
                    continue;
                }

                int arrowIndex = raw.IndexOf('>');
                if (arrowIndex < 0)
                {
                    arrowIndex = raw.IndexOf('→'); // U+2192
                }

                if (arrowIndex < 0)
                {
                    continue;
                }

                string fromPart = raw.Substring(0, arrowIndex).Trim();
                string toPart = raw.Substring(arrowIndex + 1).Trim();

                if (ItemDbValidation.TryParseProcessingState(fromPart, out ProcessingState from) &&
                    ItemDbValidation.TryParseProcessingState(toPart, out ProcessingState to))
                {
                    declared.Add(new ProcessingTransition(from, to));
                }
            }

            return declared;
        }

        private static string RecordPrefix(string recordLabel)
        {
            return string.IsNullOrEmpty(recordLabel) ? "记录" : $"记录({recordLabel})";
        }
    }
}
