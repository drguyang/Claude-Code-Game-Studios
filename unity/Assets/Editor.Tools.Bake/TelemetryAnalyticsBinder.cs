// TD-epic-01 第十二片 · 51 遥测 F4–F7 参数旋钮的 ADR-014 两阶段绑定器。
//
// 权威来源:ADR-014 §三(阶段1 仅词法 / 阶段2 自研绑定 + 白名单 + schema_version 硬失败)·
//   GDD `telemetry-analytics.md` §Tuning Knobs(六旋钮 · 「数值用户自己调」)·
//   AC-51-B14(`W < 1` / `S < 1` ⇒ **构建期**硬失败)· AC-51-E5(无开关 —— 白名单外键即拒收)
//   · AC-19-03(51 侧落点:加一个开关就是加一个出厂面)
// 形状承 InputDeviceSwitchBinder 先例(单入口自聚合自抛;无 Baker 层)。
//
// ⚠️ **AC-51-E5 在这里是结构性的而非另加一条检查**:白名单 = {schema_version, _note,
//    difficulty_curve, session_rhythm, skill_growth, sample};`enabled` / `sample_rate` /
//    `endpoint` / `experiment` / `consent` 全部落在白名单外 ⇒ 烘焙期硬失败。
//    禁止把「禁止字段」另写成黑名单 —— 黑名单只挡已知的,白名单挡其余的。
// ⚠️ 数值纪律:本类**不拍值**;只执法 GDD 明写的安全范围(TelemetryAnalyticsTuning.Validate)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Presentation.Telemetry;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>阶段1 词法 → 阶段2 绑定(四组 × 六旋钮)+ 安全范围断言。
    /// 任何违例聚合为 <see cref="BakeValidationException"/> 硬失败(ADR-014 §四)。</summary>
    public static class TelemetryAnalyticsBinder
    {
        /// <summary>作者态源文件名(单一出处)。</summary>
        public const string SourceFileName = "telemetry_analytics.json";

        private static readonly string[] AllowedTopKeys =
            { "schema_version", "_note", "difficulty_curve", "session_rhythm", "skill_growth", "sample" };

        private static readonly string[] DifficultyCurveKeys =
            { "window_ticks", "step_ticks", "first_window_tick" };

        private static readonly string[] SessionRhythmKeys = { "gap_threshold_ticks" };

        private static readonly string[] SkillGrowthKeys = { "growth_window_ticks" };

        private static readonly string[] SampleKeys = { "min_sample" };

        /// <summary>词法 → 绑定 → 范围断言;合法返回六旋钮,违例抛
        /// <see cref="BakeValidationException"/>(Errors 聚合全部违例)。</summary>
        /// <param name="json">源 JSON 全文。</param>
        /// <param name="fileLabel">错误前缀,默认 <see cref="SourceFileName"/>。</param>
        public static TelemetryAnalyticsTuning BindFromSourceText(string json, string fileLabel = SourceFileName)
        {
            var errors = new List<string>();

            if (!JsonStage1Lexer.TryParse(json, out JsonNode root, out string lexError))
            {
                errors.Add($"{fileLabel}:阶段1 词法失败:{lexError}");
                throw new BakeValidationException(errors);
            }

            CheckSchemaAndUnknownTop(root, fileLabel, errors);

            JsonNode diffCurve = RequireObject(root, "difficulty_curve", fileLabel, errors, DifficultyCurveKeys);
            JsonNode rhythm = RequireObject(root, "session_rhythm", fileLabel, errors, SessionRhythmKeys);
            JsonNode growth = RequireObject(root, "skill_growth", fileLabel, errors, SkillGrowthKeys);
            JsonNode sample = RequireObject(root, "sample", fileLabel, errors, SampleKeys);

            int? window = BindRequiredInt(diffCurve, "difficulty_curve", "window_ticks", fileLabel, errors);
            int? step = BindRequiredInt(diffCurve, "difficulty_curve", "step_ticks", fileLabel, errors);
            long? first = BindRequiredInt(diffCurve, "difficulty_curve", "first_window_tick", fileLabel, errors);
            int? gap = BindRequiredInt(rhythm, "session_rhythm", "gap_threshold_ticks", fileLabel, errors);
            int? growthWindow = BindRequiredInt(growth, "skill_growth", "growth_window_ticks", fileLabel, errors);
            int? minSample = BindRequiredInt(sample, "sample", "min_sample", fileLabel, errors);

            TelemetryAnalyticsTuning tuning = default;
            if (window.HasValue && step.HasValue && first.HasValue &&
                gap.HasValue && growthWindow.HasValue && minSample.HasValue)
            {
                tuning = new TelemetryAnalyticsTuning(
                    new DifficultyCurveTuning(window.Value, step.Value, first.Value),
                    gap.Value, growthWindow.Value, minSample.Value);
                errors.AddRange(tuning.Validate(fileLabel));   // 安全范围断言(W/S/t_0/…)
            }

            if (errors.Count > 0)
                throw new BakeValidationException(errors);
            return tuning;
        }

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

            var allowed = new HashSet<string>(AllowedTopKeys, StringComparer.Ordinal);
            foreach (string key in root.Keys)
                if (!allowed.Contains(key))
                    errors.Add($"{fileLabel}:未知顶层键 \"{key}\" —— ADR-014 §三 白名单外(AC-51-E5:51 无开关," +
                               "enabled / sample_rate / endpoint / experiment / consent 一律不进 schema),烘焙期硬失败");
        }

        /// <summary>取必填对象组并核对组内白名单(缺组 / 非对象 / 组内未知键均记错误,返回 null 占位)。</summary>
        private static JsonNode RequireObject(JsonNode root, string groupKey, string fileLabel,
                                              List<string> errors, string[] allowedKeys)
        {
            if (!root.TryGet(groupKey, out JsonNode node) || node.Kind != JsonNodeKind.Object)
            {
                errors.Add($"{fileLabel}:必填组 \"{groupKey}\" 缺失或非对象 —— GDD §Tuning Knobs 四组结构");
                return null;
            }

            var allowed = new HashSet<string>(allowedKeys, StringComparer.Ordinal);
            foreach (string key in node.Keys)
                if (!allowed.Contains(key))
                    errors.Add($"{fileLabel}:{groupKey} 未知键 \"{key}\" —— ADR-014 §三 白名单外");
            return node;
        }

        // 旋钮 = int / tick 计数(ADR-006 D-21-17:int 计数移出 Fix 解析集,合法 JSON 整数 token)
        private static int? BindRequiredInt(JsonNode parent, string groupKey, string key,
                                            string fileLabel, List<string> errors)
        {
            if (parent == null) return null;   // 组缺失已在 RequireObject 报,不重复
            if (!parent.TryGet(key, out JsonNode n))
            {
                errors.Add($"{fileLabel}:{groupKey} 缺失必填键 \"{key}\"");
                return null;
            }
            if (n.Kind != JsonNodeKind.Integer)
            {
                errors.Add($"{fileLabel}:{groupKey}.\"{key}\" 须为 JSON 整数 token(tick / 计数是 int,非 Fix;" +
                           $"当前 token={n.Kind};D-21-17)");
                return null;
            }
            if (n.Int > int.MaxValue || n.Int < int.MinValue)
            {
                errors.Add($"{fileLabel}:{groupKey}.\"{key}\" 超出 int 范围({n.Int})");
                return null;
            }
            return (int)n.Int;
        }
    }
}
