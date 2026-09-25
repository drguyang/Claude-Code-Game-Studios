// 权威来源:Story 002 AC-3-A9④(四组反例装载硬失败)· ADR-014 §三(阶段2 = 自研绑定 + 白名单)
//   · §四(Fix 字段必须 JSON 字符串,禁数值 token 中转)· ADR-006(唯一解析入口 FixParse)
//   · ADR-010 §七(schema_version 口径)—— 形状承 ItemDatabaseBinder / data_pipeline_bake_test 先例
//
// 与 ItemDatabaseBinder 的口径差:那边「本类不抛异常」,throw 归 Baker;本管线无 Baker 层
//   (Story 002 Out of Scope:cooked 输出 / DataBakeMenu 归后续)⇒ 单入口自聚合自抛。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Gameplay.Presentation;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>阶段1 词法(<see cref="JsonStage1Lexer"/>)→ 阶段2 绑定 + F-3.1 装载期断言(AC-3-A9④)。
    /// <para>任何违例(schema / 白名单 / 必填 / Fix 字符串 / 四条区间断言)聚合为
    /// <see cref="BakeValidationException"/> 硬失败 —— 非警告、非 clamp 后继续(ADR-014 §四)。</para>
    /// <para><b>「CURVE_POW 有限」在解析层承载</b>:"∞" / "NaN" 串经 <see cref="FixParse.Parse"/>
    /// 抛 FormatException ⇒ 进错误列表 —— Fix 域结构上无非有限值,与 ① 的运行期防护是独立两道门。</para></summary>
    public static class InputAxisTuningBinder
    {
        public const string SourceFileName = "input_axis_tuning.json";

        private static readonly string[] AllowedKeys =
            { "schema_version", "_note", "dz_inner", "dz_outer", "curve_pow" };

        private static readonly string[] RequiredKeys =
            { "dz_inner", "dz_outer", "curve_pow" };

        /// <summary>词法 → 绑定 → 四条装载期断言;合法返回常量表,违例抛
        /// <see cref="BakeValidationException"/>(Errors 聚合全部违例)。</summary>
        /// <param name="json">源 JSON 全文(种子文件 / 夹具 / 内联边界表)。</param>
        /// <param name="fileLabel">错误前缀,默认 <see cref="SourceFileName"/>。</param>
        public static AxisTuning BindFromSourceText(string json, string fileLabel = SourceFileName)
        {
            var errors = new List<string>();

            if (!JsonStage1Lexer.TryParse(json, out JsonNode root, out string lexError))
            {
                errors.Add($"{fileLabel}:阶段1 词法失败:{lexError}");
                throw new BakeValidationException(errors);
            }

            CheckSchemaAndUnknownTop(root, fileLabel, errors);

            foreach (string key in RequiredKeys)
            {
                if (!root.TryGet(key, out _))
                    errors.Add($"{fileLabel}:缺失必填键 \"{key}\"");
            }

            Fix? inner = BindRequiredFix(root, "dz_inner", fileLabel, errors);
            Fix? outer = BindRequiredFix(root, "dz_outer", fileLabel, errors);
            Fix? pow = BindRequiredFix(root, "curve_pow", fileLabel, errors);

            AxisTuning tuning = default;
            if (inner.HasValue && outer.HasValue && pow.HasValue)
            {
                tuning = new AxisTuning(inner.Value, outer.Value, pow.Value);
                errors.AddRange(tuning.Validate(fileLabel));   // AC-3-A9④ 四条区间断言
            }

            if (errors.Count > 0)
                throw new BakeValidationException(errors);
            return tuning;
        }

        // 承 ItemDatabaseBinder.CheckSchemaAndUnknownTop 同款口径(那边耦合 BindPackage,此处改 List 直聚合)
        private static void CheckSchemaAndUnknownTop(JsonNode root, string fileLabel, List<string> errors)
        {
            if (!root.TryGet("schema_version", out JsonNode sv))
            {
                errors.Add($"{fileLabel}:缺失必填键 \"schema_version\"");
            }
            else if (sv.Kind != JsonNodeKind.Integer)
            {
                errors.Add($"{fileLabel}:schema_version 须为 JSON 整数 token —— schema 不可读即烘焙硬失败(ADR-010 §七)");
            }
            else if ((ulong)sv.Int != CookedFormat.SchemaVersion)
            {
                errors.Add($"{fileLabel}:schema_version = {sv.Int},期望 {CookedFormat.SchemaVersion} —— " +
                           "schema 不可读/不匹配即烘焙硬失败(ADR-010 §七 · ADR-014 §三)");
            }

            var allowedSet = new HashSet<string>(AllowedKeys, StringComparer.Ordinal);
            foreach (string key in root.Keys)
            {
                if (!allowedSet.Contains(key))
                    errors.Add($"{fileLabel}:未知键 \"{key}\" —— ADR-014 §三 白名单外,烘焙期硬失败");
            }
        }

        // 承 ItemDatabaseBinder.BindRequiredFix 同款口径(缺失已由 RequiredKeys 登记 ⇒ 此处静默返回 null)
        private static Fix? BindRequiredFix(JsonNode parent, string key, string fileLabel, List<string> errors)
        {
            if (!parent.TryGet(key, out JsonNode n))
                return null;
            if (n.Kind == JsonNodeKind.Null)
            {
                errors.Add($"{fileLabel}:Fix 字段 \"{key}\" 为 null —— 必填(ADR-014 §四)");
                return null;
            }
            if (n.Kind != JsonNodeKind.String)
            {
                errors.Add($"{fileLabel}:Fix 字段 \"{key}\" 须为 JSON 字符串(token={n.Kind}) —— " +
                           "禁经数值 token 中转(ADR-014 §四 · ADR-006)");
                return null;
            }
            try
            {
                return FixParse.Parse(n.Str);
            }
            catch (FormatException ex)
            {
                errors.Add($"{fileLabel}:Fix 字段 \"{key}\" FixParse 失败:{ex.Message}" +
                           "(「CURVE_POW 有限」在此承载 —— 非整数字面量解析层即拒)");
                return null;
            }
            catch (DivideByZeroException ex)
            {
                // code review F3:"1/0" 类分母零原会以 DivideByZeroException 逃出聚合 ——
                // 仍是硬失败,但无字段定位、不入 Errors ⇒ 就地并入同一聚合口径。
                errors.Add($"{fileLabel}:Fix 字段 \"{key}\" FixParse 失败:{ex.Message}");
                return null;
            }
        }
    }
}
