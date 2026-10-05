// 权威来源:ADR-014 §三(阶段 2 per-schema 绑定:白名单 / 闭集 / FixParse 换算出口)
//          · GDD design/gdd/diagnosis-system.md §F-8.3(C_neg 曲线 + 旋钮值域)/ §F-8.4(无随机)
//          · C-4(两族正交)/ C-5(NEG_GAMMA ∈ {整数, 1/2})/ C-6(NEG_WEIGHT_FALLBACK > 0)
//          · ADR-026(FixPow 唯一整数幂;8 侧等价物 = 预计算定表)
//          · Story 004 Implementation Note 1/2(阴性族**分表分文件**;兜底值住全局旋钮)
//
// ⚠️ 本件 = F-8.3 阴性定表的**生成器 + 唯一校验点**(阶段 2 的绑定面)。
//    `assets/data/diagnosis_negative_confidence.json`(5 合成旋钮)→ 逐档 Fix 求值 → float 定表[0..60]。
//
// ⚠️ **与 story 003 的 `DiagnosisReadFloorBinder` 分表分文件**(C-4 正交性物化面):
//    阴性族 `{NEG_*}` 只进 F-8.3,阳性族 `{READ_*, READ_GAMMA}` 只进 F-8.1 —— 两张定表、
//    两条消费路径、两处哈希,改一族不影响另一族(测试物化,非代码审查)。
//
// ⚠️ 运行期 8 **不做任何幂运算**(G-1):本件在编辑期把 s^NEG_GAMMA 算完,出货的只有 float 定表值。
//
// ⚠️ 旋钮是**合成值**(Guardrail:阈值族数值归用户数值轮)—— 本件只签**形状**
//    (值域 / 序关系 / 指数闭集 / C-6),不签数值。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Presentation.Diagnosis;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.Contracts.SkillSystem;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>`diagnosis_negative_confidence.json` 的阶段 2 绑定器:**唯一**产出 F-8.3 定表的地方。
    /// <para>生成期在整数定点域内求值(ADR-005 / ADR-006),运行期只见 float 定表值。</para>
    /// </summary>
    internal static class DiagnosisNegativeConfidenceBinder
    {
        // ── 已知键白名单(ADR-014 §三:未知键 = 烘焙期硬失败,防拼写错误静默丢字段)──

        private static readonly string[] RootKeys =
        {
            "schema_version", "skill_cap", "neg_conf_0", "neg_conf_cap", "neg_gamma",
            "neg_weight_fallback", "exclude_conf_min",
        };

        /// <summary>0.5 的 Q16.16 小数位(C-5 的闭集判据之一)。</summary>
        private const ushort HalfFractional = 0x8000;

        /// <summary>绑定结果(定表 + 旋钮回声,供写方落盘)。</summary>
        public readonly struct BindResult
        {
            /// <summary>等级上限(须 == 30 的 `SKILL_CAP`,构建期断言)。</summary>
            public readonly int SkillCap;

            /// <summary>Lv0 缺席置信 raw Q16.16(`NEG_CONF_0`)。</summary>
            public readonly long NegConf0Raw;

            /// <summary>满技能缺席置信 raw Q16.16(`NEG_CONF_CAP`)。</summary>
            public readonly long NegConfCapRaw;

            /// <summary>曲线陡度 raw Q16.16(∈ {整数, 整数+1/2})。</summary>
            public readonly long NegGammaRaw;

            /// <summary>阳性「读不出」兜底权重 raw Q16.16(`NEG_WEIGHT_FALLBACK`;> 0,C-6)。</summary>
            public readonly long NegWeightFallbackRaw;

            /// <summary>「这句话算数」阈值 raw Q16.16(`EXCLUDE_CONF_MIN`;∈ (0,1] 且 ≤ CAP)。</summary>
            public readonly long ExcludeMinRaw;

            /// <summary>定表 `[0..SkillCap]`(生成期 Fix 求值 → float;Q16.16 → binary32 **精确**)。
            /// <para>精确性论证:值域 [0,1] 且小数位 ≤ 16 ⇒ 有效位 ≤ 17 &lt; float 尾数 24 位。</para></summary>
            public readonly float[] Table;

            /// <summary>源 `schema_version`(u32 域;随产物头落盘)。</summary>
            public readonly uint SchemaVersion;

            public BindResult(
                int skillCap, long negConf0Raw, long negConfCapRaw, long negGammaRaw,
                long negWeightFallbackRaw, long excludeMinRaw, float[] table, uint schemaVersion)
            {
                SkillCap = skillCap;
                NegConf0Raw = negConf0Raw;
                NegConfCapRaw = negConfCapRaw;
                NegGammaRaw = negGammaRaw;
                NegWeightFallbackRaw = negWeightFallbackRaw;
                ExcludeMinRaw = excludeMinRaw;
                Table = table;
                SchemaVersion = schemaVersion;
            }
        }

        /// <summary>绑定 + 校验 + 生成定表。任一违例 ⇒ 聚合 <see cref="BakeValidationException"/>。</summary>
        /// <param name="json">`diagnosis_negative_confidence.json` 原文。</param>
        public static BindResult Bind(string json)
        {
            var errors = new List<string>();

            if (!JsonStage1Lexer.TryParse(json, out JsonNode root, out string lexErr))
            {
                errors.Add($"diagnosis_negative_confidence.json 阶段1 词法失败:{lexErr}");
                throw new BakeValidationException(errors);
            }

            if (root.Kind != JsonNodeKind.Object)
            {
                errors.Add($"diagnosis_negative_confidence.json 根须为对象(实得 {root.Kind})");
                throw new BakeValidationException(errors);
            }

            RejectUnknownKeys(root, RootKeys, "diagnosis_negative_confidence.json 根", errors);

            uint schemaVersion = ReadSchemaVersion(root, errors);
            int skillCap = ReadSkillCap(root, errors);
            bool ok0 = ReadFix(root, "neg_conf_0", errors, out Fix negConf0);
            bool okCap = ReadFix(root, "neg_conf_cap", errors, out Fix negConfCap);
            bool okGamma = ReadFix(root, "neg_gamma", errors, out Fix negGamma);
            bool okFallback = ReadFix(root, "neg_weight_fallback", errors, out Fix negFallback);
            bool okExclude = ReadFix(root, "exclude_conf_min", errors, out Fix excludeMin);

            // ⚠️ 仅当**全部**旋钮成功解析时才跑值域校验 —— 否则解析失败的 key 会以 Fix.Zero 参与,
            //    叠出「解析失败 + 值域违反」两条错误(违「恰一条错误」排他)。
            if (ok0 && okCap && okGamma && okFallback && okExclude)
            {
                // ── 值域:NEG_CONF_0 / NEG_CONF_CAP ∈ [0,1](GDD Tuning 表)──
                // ⚠️ `Fix` 只定义 == / != 与算术运算符(无关系运算符)—— 比较走 `.Raw` 整数域。
                if (negConf0.Raw < Fix.ZeroRaw || negConf0.Raw > Fix.OneRaw)
                    errors.Add($"F-8.3·neg_conf_0={negConf0.ToFloat()} 须 ∈ [0,1](Lv0 缺席置信)");
                if (negConfCap.Raw < Fix.ZeroRaw || negConfCap.Raw > Fix.OneRaw)
                    errors.Add($"F-8.3·neg_conf_cap={negConfCap.ToFloat()} 须 ∈ [0,1](满技能缺席置信)");

                // ── 序关系:NEG_CONF_CAP ≥ NEG_CONF_0(否则曲线反向)──
                if (negConfCap.Raw < negConf0.Raw)
                    errors.Add(
                        $"F-8.3·neg_conf_cap={negConfCap.ToFloat()} < neg_conf_0={negConf0.ToFloat()}" +
                        " —— 曲线反向(技能越高越不敢说「没有」,与规则四相悖)");

                // ── C-5:NEG_GAMMA ∈ {整数, 整数+1/2}(G-1 闭集;ADR-026)──
                if (negGamma.Raw <= 0)
                    errors.Add($"C-5·neg_gamma={negGamma.ToFloat()} 须 > 0(陡度非正 ⇒ 曲线退化或反号)");
                else
                {
                    ushort frac = (ushort)(negGamma.Raw & 0xFFFF);
                    if (frac != 0 && frac != HalfFractional)
                        errors.Add(
                            $"C-5·neg_gamma={negGamma.ToFloat()} 的小数部分须 ∈ {{0, 1/2}}" +
                            $"(G-1 指数闭集;实得 frac=0x{frac:X4})—— 其余指数须 libm 超越函数,已禁");
                }

                // ── C-6:NEG_WEIGHT_FALLBACK > 0(含兜底;取 0 ⇒ 把握度恒 0)──
                if (negFallback.Raw <= Fix.ZeroRaw)
                    errors.Add(
                        $"C-6·neg_weight_fallback={negFallback.ToFloat()} 须 > 0 —— 取 0 会让" +
                        "把握度恒 0(L* 成空集,该阴性被静默抹出鉴别路径)");

                // ── EXCLUDE_CONF_MIN ∈ (0,1] 且 ≤ NEG_CONF_CAP(否则全表死:满技能也不能排除)──
                //    C_neg(SKILL_CAP) = NEG_CONF_0 + (CAP−0)×1^gamma = NEG_CONF_CAP(s = 1 时 s^γ = 1)。
                if (excludeMin.Raw <= Fix.ZeroRaw || excludeMin.Raw > Fix.OneRaw)
                    errors.Add($"F-8.3·exclude_conf_min={excludeMin.ToFloat()} 须 ∈ (0,1](判定线)");
                else if (excludeMin.Raw > negConfCap.Raw)
                    errors.Add(
                        $"F-8.3·exclude_conf_min={excludeMin.ToFloat()} > neg_conf_cap=" +
                        $"{negConfCap.ToFloat()} —— 满技能也不能排除(全表死;GDD Tuning 表硬约束)");
            }

            if (errors.Count > 0)
                throw new BakeValidationException(errors);

            float[] table = BuildTable(skillCap, negConf0, negConfCap, negGamma);

            return new BindResult(
                skillCap, negConf0.Raw, negConfCap.Raw, negGamma.Raw,
                negFallback.Raw, excludeMin.Raw, table, schemaVersion);
        }

        // ── 定表生成(生成期 Fix 整数域;运行期零幂)────────────────────────────

        /// <summary>
        /// `C_neg(Skill) = NEG_CONF_0 + (NEG_CONF_CAP − NEG_CONF_0) × s^NEG_GAMMA`
        /// (`s = clamp(Skill,0,SKILL_CAP)/SKILL_CAP`)—— 逐档求值,全部在 Q16.16 定点域内完成。
        /// <para>⚠️ 与 F-8.1 的差别:底数是 **s**(技能比),不是 `(1−s)` —— 阴性把握度随技能**升**。</para>
        /// </summary>
        private static float[] BuildTable(int skillCap, Fix negConf0, Fix negConfCap, Fix negGamma)
        {
            var table = new float[skillCap + 1];
            Fix span = negConfCap - negConf0;

            for (int skill = 0; skill <= skillCap; skill++)
            {
                Fix s = Fix.FromRational(skill, skillCap);   // ∈ [0,1],本循环内天然钳位
                Fix pow = Fix.Pow(s, negGamma);
                Fix c = negConf0 + span * pow;
                table[skill] = c.ToFloat();
            }
            return table;
        }

        // ── 读件(严格:类型不符即记错,绝不隐式转换)───────────────────────────

        private static uint ReadSchemaVersion(JsonNode obj, List<string> errors)
        {
            if (!obj.TryGet("schema_version", out JsonNode n))
            {
                errors.Add("diagnosis_negative_confidence.json 缺 schema_version");
                return 0;
            }
            if (n.Kind != JsonNodeKind.Integer || n.Int < 0 || n.Int > uint.MaxValue)
            {
                errors.Add($"schema_version 须为 u32 域整数(实得 {n.Kind}={n.RawText})");
                return 0;
            }
            // ⚠️ **与读方同源**(ADR-014 §五:版本不匹配硬失败)。只判 u32 域会让源写 `2` 时
            //    烘焙**成功**、产物在**运行期装载**才抛 E-13 —— 失败点从构建期漏到运行期。
            //    (同形缺口在 `DiagnosisReadFloorBinder` 侧亦存,归 story-003 后续轮登记。)
            if ((uint)n.Int != DiagnosisNegativeConfidenceCookedCodec.ExpectedSchemaVersion)
            {
                errors.Add(
                    $"schema_version={(uint)n.Int} ≠ 读方认领 " +
                    $"{DiagnosisNegativeConfidenceCookedCodec.ExpectedSchemaVersion}" +
                    " —— 版本不匹配,拒收(ADR-014 §五)");
                return 0;
            }
            return (uint)n.Int;
        }

        private static int ReadSkillCap(JsonNode obj, List<string> errors)
        {
            if (!obj.TryGet("skill_cap", out JsonNode n))
            {
                errors.Add("diagnosis_negative_confidence.json 缺 skill_cap [F-8.3·skill_cap]");
                return 0;
            }
            if (n.Kind != JsonNodeKind.Integer)
            {
                errors.Add($"skill_cap 须为整数(实得 {n.Kind};禁 float/字符串) [F-8.3·skill_cap]");
                return 0;
            }
            if (n.Int != SkillRegistry.SKILL_CAP)
            {
                errors.Add(
                    $"F-8.3·skill_cap={n.Int} ≠ 30 侧 SKILL_CAP={SkillRegistry.SKILL_CAP}" +
                    " —— 定表覆盖域须与技能上限同源(否则两端档位分叉,静默漂移)");
                return 0;
            }
            return (int)n.Int;
        }

        /// <summary>Fix 字段:JSON 里写**字符串**字面量 → FixParse(ADR-014 §三;禁 JSON 数字)。</summary>
        /// <returns>true = 成功解析(值域校验可跑);false = 缺失 / 非字符串 / 解析失败(已记错)。</returns>
        private static bool ReadFix(JsonNode obj, string key, List<string> errors, out Fix value)
        {
            value = Fix.Zero;
            if (!obj.TryGet(key, out JsonNode n))
            {
                errors.Add($"diagnosis_negative_confidence.json 缺 {key} [F-8.3·{key}]");
                return false;
            }
            if (n.Kind != JsonNodeKind.String)
            {
                errors.Add(
                    $"{key} 须为字符串 Fix 字面量(实得 {n.Kind}={n.RawText}) [ADR-014·fix-string]");
                return false;
            }
            try
            {
                value = FixParse.Parse(n.Str);
                return true;
            }
            catch (Exception ex)
            {
                errors.Add($"{key}=\"{n.Str}\" 解析失败:{ex.Message} [ADR-014·fix-string]");
                return false;
            }
        }

        private static void RejectUnknownKeys(
            JsonNode obj, string[] allowed, string where, List<string> errors)
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
