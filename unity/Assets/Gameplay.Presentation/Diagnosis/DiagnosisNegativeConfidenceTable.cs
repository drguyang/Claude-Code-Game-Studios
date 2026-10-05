// 权威来源:
//   GDD design/gdd/diagnosis-system.md —— §F-8.3(C_neg 曲线 · W_j 分支 · 把握度 clamp ·
//     构成排除阈值 · L*_j 导出)/ §F-8.4(无随机三理由)/ §F-8.5(不泄漏不变式)/
//     C-3(L*_j ≤ SKILL_CAP)/ C-4(两族正交)/ C-6(NEG_WEIGHT_j > 0)/ C-7(L*_j ≥ tier_named_j)/
//     AC-8-10/11/12/14/15/17/18 · AC-8-F1/F2/F4
//   ADR-006(舍入;线性项显式 Mul 防 FMA,承 G-4)· ADR-005(确定性纪律)· ADR-012(跨平台一致)
//   Story 004(Implementation Note 1/2/3/4:分表分文件 · 兜底值住全局旋钮 ·
//     clamp 在 float 门面 · L*_j 整数档单调扫描)
//
// ⚠️ 本文件 = F-8.3 的**运行期读侧**(story 004 的落点),**与 story 003 的
//    `DiagnosisReadFloorTable` 分表分文件**(C-4 正交性物化面 —— 阴性族与阳性族
//    各持一表、各走一条消费路径;改一族不触另一族)。
//
// ⚠️ 住前缀命名空间 `…Presentation.Diagnosis`(DiagnosisBoundaryGates 扫描键)——
//   受 D-TREF / D-PERSIST / D-FIX / D-CLK / D-G1 / D-11 源层与 IL 谓词约束:
//   · **运行期零幂运算**(G-1):C_neg 定表值由烘焙期算好,此处只有查表 + 整数/浮点比较;
//   · **不构造 `Fix`、不写 `FixParse`、不访问 `Fix` 成员**(D-FIX;旋钮以 raw `long` 回声承载,
//     运行期不做任何定点算术 —— raw → float 走本件私有 Q16 常量);
//   · **无 `System.IO` / 持久化 / 时钟 / RNG**(D-PERSIST · D-CLK · AC-8-16);
//   · **零 `double`**(G-4;标量一律 `System.Single`);
//   · 字段名零累积量语义 token(AC-8-3 反射扫描 —— 本类型为**只读值**,零跨调用累积量)。
//
// ⚠️ **纯函数形状**(AC-8-3):求值器静态、零字段;定表是**入参**(依赖注入),
//   8 不持任何跨调用状态。两独立进程喂同一定表 + 同一 (Skill, sign_id) ⇒ 逐位相同。

using System;

namespace DaYiJingCheng.Gameplay.Presentation.Diagnosis
{
    /// <summary>F-8.3 阴性把握度定表(<c>diagnosis_negative_confidence.cooked.bytes</c> 的解码形)。
    /// <para>只读值:旋钮以 raw Q16.16 <c>long</c> 承载(运行期不做定点算术,仅供端点复核 /
    /// 跨端一致性断言),<c>Curve[0..SkillCap]</c> 为 binary32 定表值(C_neg 曲线)。</para>
    /// </summary>
    public readonly struct DiagnosisNegativeConfidenceTable
    {
        /// <summary>Q16.16 的 1.0(raw → float 换算用)。
        /// <para>⚠️ D-FIX 源层谓词含 <c>\.OneRaw\b</c> ⇒ 前缀 ns 内**不得**引用 <c>Fix.OneRaw</c>,
        /// 故此处本地落值。<b>唯一性</b>:全前缀 ns 只有这一处 65536 —— 求值器经
        /// <see cref="RawToFloat"/> 复用,禁第二份(D-8-9 型漂移面)。</para>
        /// <para>⚠️ 该值的正确性由 <c>confidence_leak_test.test_q16one_matchesFixCanonical</c>
        /// **逐位锚定**到 <c>Fix.ToFloat()</c>(含 <c>Fix.FractionalBits == 16</c>)。</para></summary>
        private const float Q16One = 65536f;

        private readonly float[] _curve;

        /// <summary>等级上限(须 == 30 的 `SKILL_CAP`;构建期已断言,运行期只读)。</summary>
        public readonly int SkillCap;

        /// <summary>Lv0 缺席置信 raw Q16.16(`NEG_CONF_0`,= <c>C_neg(0)</c>)。</summary>
        public readonly long NegConf0Raw;

        /// <summary>满技能缺席置信 raw Q16.16(`NEG_CONF_CAP`,= <c>C_neg(SKILL_CAP)</c>)。</summary>
        public readonly long NegConfCapRaw;

        /// <summary>曲线陡度 raw Q16.16(∈ {整数, 整数+1/2})。</summary>
        public readonly long NegGammaRaw;

        /// <summary>阳性「读不出」兜底权重 raw Q16.16(`NEG_WEIGHT_FALLBACK`;> 0,C-6)。
        /// <para>⚠️ 只服务「把握不足」形态,**不参与任何排除判定**(F-8.3 修补 (c))。</para></summary>
        public readonly long NegWeightFallbackRaw;

        /// <summary>「这句话算数」阈值 raw Q16.16(`EXCLUDE_CONF_MIN`;∈ (0,1])。</summary>
        public readonly long ExcludeMinRaw;

        /// <summary>定表项数(= <see cref="SkillCap"/> + 1)。</summary>
        public int Count => _curve?.Length ?? 0;

        /// <summary>完整构造(codec / 测试用)。</summary>
        /// <exception cref="ArgumentException">表长 ≠ <paramref name="skillCap"/> + 1。</exception>
        public DiagnosisNegativeConfidenceTable(
            int skillCap, long negConf0Raw, long negConfCapRaw, long negGammaRaw,
            long negWeightFallbackRaw, long excludeMinRaw, float[] curve)
        {
            if (curve == null) throw new ArgumentNullException(nameof(curve));
            if (curve.Length != skillCap + 1)
                throw new ArgumentException(
                    $"定表长度 {curve.Length} ≠ skill_cap({skillCap}) + 1 —— 覆盖域须为 [0, SKILL_CAP]");
            SkillCap = skillCap;
            NegConf0Raw = negConf0Raw;
            NegConfCapRaw = negConfCapRaw;
            NegGammaRaw = negGammaRaw;
            NegWeightFallbackRaw = negWeightFallbackRaw;
            ExcludeMinRaw = excludeMinRaw;
            _curve = curve;
        }

        /// <summary><c>C_neg(Skill)</c> 查定表(`Skill` 越界即钳位 —— <c>s = clamp(...)</c>)。</summary>
        /// <param name="skill">诊断熟练度(整数等级)。</param>
        /// <returns>该等级的缺席置信(不乘权重;binary32)。</returns>
        /// <exception cref="InvalidOperationException">定表未装载(<c>default</c> 实例)。
        /// <para>⚠️ 退化表**不得静默返回 0**:<c>C_neg ≡ 0</c> 会让每条阴性都「永不构成排除」,
        /// 即 C-3 的死内容报警被伪装成正常值(静默降级 = E-13 口径所禁)。</para></exception>
        public float CurveAt(int skill)
        {
            if (_curve == null || _curve.Length == 0)
                throw new InvalidOperationException(
                    "[E-13] diagnosis_negative_confidence 定表未装载(default 实例) —— 不得消费");
            if (skill < 0) skill = 0;
            else if (skill > SkillCap) skill = SkillCap;
            return _curve[skill];
        }

        /// <summary>raw Q16.16 → binary32(与 <c>Fix.ToFloat()</c> 逐位等价 —— 见类头注)。
        /// <para>唯一换算出口:全前缀 ns 只有本处引用 <see cref="Q16One"/>。</para></summary>
        public static float RawToFloat(long raw) => raw / Q16One;

        /// <summary><c>NEG_WEIGHT_FALLBACK</c> 的 float 形(阳性「读不出」兜底权重)。</summary>
        public float FallbackWeight => RawToFloat(NegWeightFallbackRaw);

        /// <summary><c>EXCLUDE_CONF_MIN</c> 的 float 形(判定线)。</summary>
        public float ExcludeMin => RawToFloat(ExcludeMinRaw);
    }

    /// <summary>F-8.3 的运行期求值器(纯静态、零字段 —— AC-8-3)。
    /// <para>运行期零幂运算:C_neg 走定表查表(G-1)。<b>F-8.5 不泄漏</b>:本类型全部方法的实参表
    /// **不含任何 `Sign_j`**(AC-8-18 实现级)—— 把握度只是 <c>(Skill, sign_id)</c> 的函数。</para>
    /// </summary>
    public static class DiagnosisNegativeConfidenceEvaluator
    {
        /// <summary><c>L*_j</c> 的「永不构成排除」哨兵(C-3 报警面:满技能仍 &lt; 阈值)。</summary>
        public const int NeverExcludes = -1;

        /// <summary>
        /// <b>§F-8.3 <c>W_j</c>(该体征的排除权重)</b> —— 阴性取词表 <c>NEG_WEIGHT_j</c>,
        /// 阳性取全局兜底 <c>NEG_WEIGHT_FALLBACK</c>(F-8.3 修补 (a))。
        /// <para>⚠️ 分支依 <see cref="SignLexemeRow.Polarity"/>(**词表字段**,与病人病情无关),
        /// **不引入任何 `Sign_j` 依赖**(F-8.3 修补 (b) / F-8.5)。</para>
        /// </summary>
        /// <param name="table">F-8.3 定表(兜底权重来源)。</param>
        /// <param name="row">词条行(极性 + 阴性权重)。</param>
        public static float WeightOf(in DiagnosisNegativeConfidenceTable table, SignLexemeRow row)
        {
            if (row.Polarity == SignPolarity.Negative && row.NegWeightRaw.HasValue)
                return DiagnosisNegativeConfidenceTable.RawToFloat(row.NegWeightRaw.Value);
            return table.FallbackWeight;
        }

        /// <summary>
        /// <b>§F-8.3 把握度</b> —— <c>clamp( C_neg(Skill) × W_j , 0 , 1 )</c>。
        /// <para>⚠️ 显式 Mul + 显式 clamp(禁依赖 FMA 收缩;G-4 位宽钉 <c>System.Single</c>)。</para>
        /// <para>⚠️ 本方法实参表**不含 `Sign_j`**(F-8.5 / AC-8-18):同一 <c>(Skill, sign_id)</c>
        /// 下,任何两个病人的把握度**逐位相同**。</para>
        /// </summary>
        /// <param name="table">F-8.3 定表(依赖注入 —— 8 不持状态)。</param>
        /// <param name="row">词条行。</param>
        /// <param name="skill">诊断熟练度(整数等级)。</param>
        /// <returns>把握度 ∈ [0,1](binary32)。</returns>
        public static float Confidence(
            in DiagnosisNegativeConfidenceTable table, SignLexemeRow row, int skill)
        {
            float product = table.CurveAt(skill) * WeightOf(table, row);
            if (product < 0f) return 0f;
            if (product > 1f) return 1f;
            return product;
        }

        /// <summary>
        /// <b>§F-8.3 构成排除证据</b> —— <c>把握度_j(Skill) ≥ EXCLUDE_CONF_MIN</c>。
        /// <para>⚠️ <b>仅阴性体征</b>有排除语义(F-8.3 修补 (c)):阳性体征的兜底权重只服务
        /// 「把握不足」形态,**本方法对阳性行恒返回 <c>false</c>**(37 / #53 只看
        /// <c>polarity = 阴性</c> 的行;阳性不得被当作排除证据消费)。</para>
        /// </summary>
        public static bool ConstitutesExclusion(
            in DiagnosisNegativeConfidenceTable table, SignLexemeRow row, int skill)
        {
            if (row.Polarity != SignPolarity.Negative) return false;
            return Confidence(table, row, skill) >= table.ExcludeMin;
        }

        /// <summary>
        /// <b>§F-8.3 导出 <c>L*_j</c></b> —— 阴性体征 j **第一次构成排除**的等级:
        /// <c>min { Skill ∈ ℤ : 把握度_j(Skill) ≥ EXCLUDE_CONF_MIN }</c>。
        /// <para>⚠️ **整数档单调扫描**(禁浮点求根;Implementation Note 4):自 Lv0 起逐档查,
        /// 首个达标即返回。全表不达标(死内容)⇒ 返回 <see cref="NeverExcludes"/>(C-3 报警面)。</para>
        /// <para>⚠️ 仅对 <c>polarity = 阴性</c> 有意义(F-8.3 修补 (c));阳性行返回
        /// <see cref="NeverExcludes"/>。</para>
        /// </summary>
        public static int ExclusionLevel(in DiagnosisNegativeConfidenceTable table, SignLexemeRow row)
        {
            if (row.Polarity != SignPolarity.Negative) return NeverExcludes;
            float threshold = table.ExcludeMin;
            for (int skill = 0; skill <= table.SkillCap; skill++)
                if (Confidence(table, row, skill) >= threshold)
                    return skill;
            return NeverExcludes;
        }
    }
}
