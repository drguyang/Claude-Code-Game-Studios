// 权威来源:ADR-014 §三(阶段 2 per-schema 绑定:白名单 / 闭集 / FixParse 换算出口)
//          · GDD design/gdd/diagnosis-system.md §F-8.1(READ_FLOOR 曲线 + 三锚点 + C-1/C-5)
//          · ADR-026(FixPow 唯一整数幂;8 侧等价物 = 预计算定表)
//          · Story 003 Implementation Note 1/2(定表 = 烘焙产物;比值运算在**生成期**以 Fix 完成)
//
// ⚠️ 本件 = F-8.1 定表的**生成器 + 唯一校验点**(阶段 2 的绑定面)。
//    `assets/data/diagnosis_read_floor.json`(3 合成系数)→ 逐档 Fix 求值 → float 定表[0..60]。
//
// ⚠️ 运行期 8 **不做任何幂运算**(G-1):本件在编辑期把 (1−s)^READ_GAMMA 算完,
//    出货的只有 float 定表值。运行期只有查表 + 整数比较。
//
// ⚠️ 系数是**合成值**(Guardrail:曲线系数值归用户数值轮)—— 本件只签**形状**
//    (C-1 端点序 / C-5 指数闭集 / 单调),不签数值。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.Contracts.SkillSystem;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>`diagnosis_read_floor.json` 的阶段 2 绑定器:**唯一**产出 F-8.1 定表的地方。
    /// <para>生成期在整数定点域内求值(ADR-005 / ADR-006),运行期只见 float 定表值。</para>
    /// </summary>
    internal static class DiagnosisReadFloorBinder
    {
        // ── 已知键白名单(ADR-014 §三:未知键 = 烘焙期硬失败,防拼写错误静默丢字段)──

        private static readonly string[] RootKeys =
        {
            "schema_version", "skill_cap", "base_read", "read_floor_min", "read_gamma",
        };

        /// <summary>0.5 的 Q16.16 小数位(C-5 的闭集判据之一)。</summary>
        private const ushort HalfFractional = 0x8000;

        /// <summary>绑定结果(定表 + 系数回声,供写方落盘)。</summary>
        public readonly struct BindResult
        {
            /// <summary>等级上限(须 == 30 的 `SKILL_CAP`,构建期断言)。</summary>
            public readonly int SkillCap;

            /// <summary>门槛上端锚 raw Q16.16(`READ_FLOOR(0)`)。</summary>
            public readonly long BaseReadRaw;

            /// <summary>门槛下端锚 raw Q16.16(`READ_FLOOR(SKILL_CAP)`)。</summary>
            public readonly long ReadFloorMinRaw;

            /// <summary>曲线陡度 raw Q16.16(∈ {整数, 整数+1/2})。</summary>
            public readonly long ReadGammaRaw;

            /// <summary>定表 `[0..SkillCap]`(生成期 Fix 求值 → float;Q16.16 → binary32 **精确**)。
            /// <para>精确性论证:值域 [0,1] 且小数位 ≤ 16 ⇒ 有效位 ≤ 17 &lt; float 尾数 24 位。</para></summary>
            public readonly float[] Table;

            /// <summary>源 `schema_version`(u32 域;随产物头落盘)。</summary>
            public readonly uint SchemaVersion;

            public BindResult(
                int skillCap, long baseReadRaw, long readFloorMinRaw, long readGammaRaw,
                float[] table, uint schemaVersion)
            {
                SkillCap = skillCap;
                BaseReadRaw = baseReadRaw;
                ReadFloorMinRaw = readFloorMinRaw;
                ReadGammaRaw = readGammaRaw;
                Table = table;
                SchemaVersion = schemaVersion;
            }
        }

        /// <summary>绑定 + 校验 + 生成定表。任一违例 ⇒ 聚合 <see cref="BakeValidationException"/>。</summary>
        /// <param name="json">`diagnosis_read_floor.json` 原文。</param>
        public static BindResult Bind(string json)
        {
            var errors = new List<string>();

            if (!JsonStage1Lexer.TryParse(json, out JsonNode root, out string lexErr))
            {
                errors.Add($"diagnosis_read_floor.json 阶段1 词法失败:{lexErr}");
                throw new BakeValidationException(errors);
            }

            if (root.Kind != JsonNodeKind.Object)
            {
                errors.Add($"diagnosis_read_floor.json 根须为对象(实得 {root.Kind})");
                throw new BakeValidationException(errors);
            }

            RejectUnknownKeys(root, RootKeys, "diagnosis_read_floor.json 根", errors);

            uint schemaVersion = ReadSchemaVersion(root, errors);
            int skillCap = ReadSkillCap(root, errors);
            bool okBase = ReadFix(root, "base_read", errors, out Fix baseRead);
            bool okMin = ReadFix(root, "read_floor_min", errors, out Fix readFloorMin);
            bool okGamma = ReadFix(root, "read_gamma", errors, out Fix readGamma);

            // ⚠️ 仅当三个系数**全部**成功解析时才跑值域校验 —— 否则解析失败的 key 会以
            //    Fix.Zero 参与,叠出「解析失败 + C-1/C-5 违反」两条错误(违「恰一条错误」排他)。
            if (okBase && okMin && okGamma)
            {
                // ── C-1:BASE_READ > READ_FLOOR_MIN > 0(GDD §F-8.1 硬约束 + 三锚点)──
                // ⚠️ `Fix` 只定义 == / != 与算术运算符(无关系运算符)—— 比较走 `.Raw` 整数域。
                if (readFloorMin.Raw <= Fix.ZeroRaw)
                    errors.Add(
                        $"C-1·read_floor_min={readFloorMin.ToFloat()} 须 > 0 —— 门槛永不为零" +
                        "(满技能的医生也不是全知;GDD §F-8.1 硬约束)");
                if (baseRead.Raw <= readFloorMin.Raw)
                    errors.Add(
                        $"C-1·base_read={baseRead.ToFloat()} 须 > read_floor_min={readFloorMin.ToFloat()}" +
                        " —— 否则门槛是常数,曲线不存在(READ_FLOOR(0) = READ_FLOOR(SKILL_CAP))");

                // ── C-5:READ_GAMMA ∈ {整数, 整数+1/2}(G-1 闭集;ADR-026)──
                if (readGamma.Raw <= 0)
                    errors.Add($"C-5·read_gamma={readGamma.ToFloat()} 须 > 0(陡度非正 ⇒ 曲线退化为常数或反号)");
                else
                {
                    ushort frac = (ushort)(readGamma.Raw & 0xFFFF);
                    if (frac != 0 && frac != HalfFractional)
                        errors.Add(
                            $"C-5·read_gamma={readGamma.ToFloat()} 的小数部分须 ∈ {{0, 1/2}}" +
                            $"(G-1 指数闭集;实得 frac=0x{frac:X4})—— 其余指数须 libm 超越函数,已禁");
                }
            }

            if (errors.Count > 0)
                throw new BakeValidationException(errors);

            float[] table = BuildTable(skillCap, baseRead, readFloorMin, readGamma);

            return new BindResult(
                skillCap, baseRead.Raw, readFloorMin.Raw, readGamma.Raw, table, schemaVersion);
        }

        // ── 定表生成(生成期 Fix 整数域;运行期零幂)────────────────────────────

        /// <summary>
        /// `READ_FLOOR(Skill) = READ_FLOOR_MIN + (BASE_READ − READ_FLOOR_MIN) × (1 − s)^READ_GAMMA`
        /// (`s = clamp(Skill,0,SKILL_CAP)/SKILL_CAP`)—— 逐档求值,全部在 Q16.16 定点域内完成。
        /// <para>Fix 的四则 / 幂 / 舍入均为 ROUND_HALF_AWAY_FROM_ZERO(ADR-006 §三),故生成期
        /// 舍入模式即定表钉死模式,跨平台逐位确定(ADR-012)。</para>
        /// </summary>
        private static float[] BuildTable(int skillCap, Fix baseRead, Fix readFloorMin, Fix readGamma)
        {
            var table = new float[skillCap + 1];
            Fix span = baseRead - readFloorMin;

            for (int skill = 0; skill <= skillCap; skill++)
            {
                // s = clamp(Skill,0,CAP)/CAP —— 本循环内 skill ∈ [0,cap] 天然钳位
                Fix s = Fix.FromRational(skill, skillCap);
                Fix oneMinusS = Fix.One - s;
                Fix pow = Fix.Pow(oneMinusS, readGamma);
                Fix floor = readFloorMin + span * pow;
                table[skill] = floor.ToFloat();
            }
            return table;
        }

        // ── 读件(严格:类型不符即记错,绝不隐式转换)───────────────────────────

        private static uint ReadSchemaVersion(JsonNode obj, List<string> errors)
        {
            if (!obj.TryGet("schema_version", out JsonNode n))
            {
                errors.Add("diagnosis_read_floor.json 缺 schema_version");
                return 0;
            }
            if (n.Kind != JsonNodeKind.Integer || n.Int < 0 || n.Int > uint.MaxValue)
            {
                errors.Add($"schema_version 须为 u32 域整数(实得 {n.Kind}={n.RawText})");
                return 0;
            }
            return (uint)n.Int;
        }

        private static int ReadSkillCap(JsonNode obj, List<string> errors)
        {
            if (!obj.TryGet("skill_cap", out JsonNode n))
            {
                errors.Add("diagnosis_read_floor.json 缺 skill_cap [F-8.1·skill_cap]");
                return 0;
            }
            if (n.Kind != JsonNodeKind.Integer)
            {
                errors.Add($"skill_cap 须为整数(实得 {n.Kind};禁 float/字符串) [F-8.1·skill_cap]");
                return 0;
            }
            if (n.Int != SkillRegistry.SKILL_CAP)
            {
                errors.Add(
                    $"F-8.1·skill_cap={n.Int} ≠ 30 侧 SKILL_CAP={SkillRegistry.SKILL_CAP}" +
                    " —— 定表覆盖域须与技能上限同源(否则两端档位分叉,静默漂移)");
                return 0;
            }
            return (int)n.Int;
        }

        /// <summary>Fix 字段:JSON 里写**字符串**字面量 → FixParse(ADR-014 §三;禁 JSON 数字)。
        /// <returns>true = 成功解析(值域校验可跑);false = 缺失 / 非字符串 / 解析失败(已记错)。</returns></summary>
        private static bool ReadFix(JsonNode obj, string key, List<string> errors, out Fix value)
        {
            value = Fix.Zero;
            if (!obj.TryGet(key, out JsonNode n))
            {
                errors.Add($"diagnosis_read_floor.json 缺 {key} [F-8.1·{key}]");
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
                // catch 整型 Exception:分子超域抛 FormatException、分母 0 抛 DivideByZeroException ——
                // 任一都须聚合成 ADR-014·fix-string(承 story-002 S-M1 同纪律)。
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
