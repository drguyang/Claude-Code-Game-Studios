// Story 008 · 设备切换迟滞常量表的 ADR-014 两阶段绑定器(§Tuning Knobs 一之二 · AC-3-D4)
//
// 权威来源:ADR-014 §三(阶段1 仅词法 / 阶段2 自研绑定 + 白名单 + schema_version 硬失败)·
//   §四(Fix 字段 JSON 必须字符串,禁数值 token 中转;ADR-006 FixParse 唯一入口)·
//   GDD §一之二 Ⓑ.1(两列按设备类分别登记 —— 白名单键结构 = pointer / axis 两对象,
//   物理上不给「共用一行」留形状)· AC-3-A9 同型纪律(装载期断言 = 硬失败非警告)。
// 形状承 InputAxisTuningBinder 先例(单入口自聚合自抛;无 Baker 层 —— cooked 输出归后续轮,
//   与 Story 002 同一 Out-of-Scope 口径)。
//
// 数值纪律:本类**不拍值** —— 值住 assets/data/input_device_switch.json(临时种子,
//   标「待数值轮」);本类只执法 GDD 明写的安全范围(DeviceSwitchTuning.Validate)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Gameplay.Presentation;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>阶段1 词法 → 阶段2 绑定(两列 × 三旋钮)+ 装载期区间断言。
    /// 任何违例聚合为 <see cref="BakeValidationException"/> 硬失败(ADR-014 §四)。</summary>
    public static class InputDeviceSwitchBinder
    {
        public const string SourceFileName = "input_device_switch.json";

        private static readonly string[] AllowedTopKeys =
            { "schema_version", "_note", "pointer", "axis" };

        private static readonly string[] AllowedColumnKeys =
            { "device_switch_threshold", "drift_tolerance", "dwell_ticks" };

        /// <summary>词法 → 绑定 → 区间断言;合法返回两列表,违例抛
        /// <see cref="BakeValidationException"/>(Errors 聚合全部违例)。</summary>
        /// <param name="json">源 JSON 全文。</param>
        /// <param name="fileLabel">错误前缀,默认 <see cref="SourceFileName"/>。</param>
        public static DeviceSwitchTuning BindFromSourceText(string json, string fileLabel = SourceFileName)
        {
            var errors = new List<string>();

            if (!JsonStage1Lexer.TryParse(json, out JsonNode root, out string lexError))
            {
                errors.Add($"{fileLabel}:阶段1 词法失败:{lexError}");
                throw new BakeValidationException(errors);
            }

            CheckSchemaAndUnknownTop(root, fileLabel, errors);

            DeviceSwitchColumn? pointer = BindColumn(root, "pointer", fileLabel, errors);
            DeviceSwitchColumn? axis = BindColumn(root, "axis", fileLabel, errors);

            DeviceSwitchTuning tuning = default;
            if (pointer.HasValue && axis.HasValue)
            {
                tuning = new DeviceSwitchTuning(pointer.Value, axis.Value);
                errors.AddRange(tuning.Validate(fileLabel));   // 装载期区间断言(轴列带上界断言)
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
                    errors.Add($"{fileLabel}:未知顶层键 \"{key}\" —— ADR-014 §三 白名单外,烘焙期硬失败");

            // Ⓑ.1 两列各自成表:pointer 与 axis 两对象缺一不可(缺列 = 共用一行的温床)
            foreach (string col in new[] { "pointer", "axis" })
                if (!root.TryGet(col, out JsonNode n) || n.Kind != JsonNodeKind.Object)
                    errors.Add($"{fileLabel}:列 \"{col}\" 缺失或非标量对象 —— Ⓑ.1 两列结构(AC-3-D4)");
        }

        private static DeviceSwitchColumn? BindColumn(JsonNode root, string columnKey,
                                                      string fileLabel, List<string> errors)
        {
            if (!root.TryGet(columnKey, out JsonNode col) || col.Kind != JsonNodeKind.Object)
                return null;   // 结构性缺失已在 CheckSchemaAndUnknownTop 报,不重复

            var allowed = new HashSet<string>(AllowedColumnKeys, StringComparer.Ordinal);
            foreach (string key in col.Keys)
                if (!allowed.Contains(key))
                    errors.Add($"{fileLabel}:{columnKey} 未知键 \"{key}\" —— ADR-014 §三 白名单外");

            Fix? threshold = BindRequiredFix(col, columnKey, "device_switch_threshold", fileLabel, errors);
            Fix? tolerance = BindRequiredFix(col, columnKey, "drift_tolerance", fileLabel, errors);
            int? dwell = BindRequiredInt(col, columnKey, "dwell_ticks", fileLabel, errors);

            if (threshold.HasValue && tolerance.HasValue && dwell.HasValue)
                return new DeviceSwitchColumn(threshold.Value, tolerance.Value, dwell.Value);
            return null;
        }

        // 承 InputAxisTuningBinder.BindRequiredFix 同款口径(Fix 字段必须字符串;FormatException /
        // DivideByZeroException 聚合入错误列表 —— code review F3 先例)
        private static Fix? BindRequiredFix(JsonNode parent, string columnKey, string key,
                                            string fileLabel, List<string> errors)
        {
            if (!parent.TryGet(key, out JsonNode n))
            {
                errors.Add($"{fileLabel}:{columnKey} 缺失必填键 \"{key}\"");
                return null;
            }
            if (n.Kind == JsonNodeKind.Null)
            {
                errors.Add($"{fileLabel}:{columnKey}.\"{key}\" 为 null —— 必填(ADR-014 §四)");
                return null;
            }
            if (n.Kind != JsonNodeKind.String)
            {
                errors.Add($"{fileLabel}:{columnKey}.\"{key}\" 须为 JSON 字符串(token={n.Kind}) —— " +
                           "禁经数值 token 中转(ADR-014 §四 · ADR-006)");
                return null;
            }
            try
            {
                return FixParse.Parse(n.Str);
            }
            catch (FormatException ex)
            {
                errors.Add($"{fileLabel}:{columnKey}.\"{key}\" FixParse 失败:{ex.Message}");
                return null;
            }
            catch (DivideByZeroException ex)
            {
                errors.Add($"{fileLabel}:{columnKey}.\"{key}\" FixParse 失败:{ex.Message}");
                return null;
            }
        }

        // dwell_ticks = int 计数(ADR-006 D-21-17:int 计数移出 Fix 解析集,合法 JSON 整数 token)
        private static int? BindRequiredInt(JsonNode parent, string columnKey, string key,
                                            string fileLabel, List<string> errors)
        {
            if (!parent.TryGet(key, out JsonNode n))
            {
                errors.Add($"{fileLabel}:{columnKey} 缺失必填键 \"{key}\"");
                return null;
            }
            if (n.Kind != JsonNodeKind.Integer)
            {
                errors.Add($"{fileLabel}:{columnKey}.\"{key}\" 须为 JSON 整数 token(tick 计数是 int," +
                           $"非 Fix;当前 token={n.Kind};D-21-17)");
                return null;
            }
            if (n.Int > int.MaxValue || n.Int < int.MinValue)
            {
                errors.Add($"{fileLabel}:{columnKey}.\"{key}\" 超出 int 范围({n.Int})");
                return null;
            }
            return (int)n.Int;
        }
    }
}
