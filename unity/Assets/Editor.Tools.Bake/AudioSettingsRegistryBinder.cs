// S14-5 · story-011 —— 设置暴露面注册表(assets/data/audio_settings_registry.json)的阶段 2 绑定器。
//
// 权威来源:
//   ADR-014 §三(阶段 2 per-schema 绑定:未知键白名单硬失败 · 校验聚合 throw)
//   GDD design/gdd/audio-system.md §UI Requirements 设置暴露面注册表(:896-909)
//     · 8 行 = 7 总线音量(含 Master)+ mono_enabled;每行:参数名 / 类型 / 区间 / 出厂默认 /
//       player_group / 题签语义源
//   AC-44-13(「出厂默认入烘焙分区」)· AC-44-19(出厂默认集 = 归源的真源)
//   OQ-SS-3=甲(数据层恒 7 路)· OQ-SS-5=甲(端本地 sidecar)· OQ-SS-7=甲(默认值真源 = 烘焙分区)
//
// ⚠️ 本件是**注册表的唯一构建期校验点**。它兑现三件事:
//   ① **名字对账** —— JSON 的 param 集 **恰 =** MixerRegistry.SettingsVisibleParameters(8 员)。
//      少一个 / 多一个 / 拼错 = 构建失败(注册表 AC①「壳消费集 ⊆ 闭枚举」的结构面;
//      名字半边单一出处在 MixerRegistry,本件**引用**它,不另抄 —— ADR-024 §⑥)。
//   ② **值对账** —— 出厂默认值与运行期回退值(AudioSettingsRegistry.FallbackRows)**逐键相等**。
//      这让「烘焙数据与回退静默漂移」不可能发生:改一处不改另一处 = 构建红。
//      ⚠️ 本对账是**双向**的:任一侧多 / 少 / 值不同都报。
//   ③ **域检查** —— float 行 ∈ [0,1] 且 min < max;bool 行不得带 min/max;type ∈ 闭集。
//
// ⚠️ 零浮点经 double 中转的纪律(ADR-006 / ADR-014 §二)在本件**不适用**:
//   设置值是**表现层设备偏好**,不入 sim 定点域、不进三流、不进存档头(OQ-SS-5=甲)。
//   本件用 JsonStage1Lexer 的 Decimal 形态直读(承 DelayTableBinder 同款),不经 float。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Presentation.Audio;
using DaYiJingCheng.Gameplay.Presentation.Settings;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary><c>audio_settings_registry.json</c> 的阶段 2 绑定器(名字/值双向对账 + 域检查,聚合 throw)。</summary>
    public static class AudioSettingsRegistryBinder
    {
        private static readonly string[] RootKeys = { "schema_version", "_note", "rows" };
        private static readonly string[] RowKeys =
        {
            "param", "type", "min", "max", "factory_default", "player_group", "label_source",
        };
        private static readonly string[] BoolRowForbiddenKeys = { "min", "max" };

        /// <summary>绑定结果(8 行注册表 + 源 schema_version)。</summary>
        public readonly struct BindResult
        {
            /// <summary>解析出的注册表行(顺序 = JSON 序)。</summary>
            public readonly IReadOnlyList<SettingsRegistryRow> Rows;

            /// <summary>源 schema_version。</summary>
            public readonly uint SchemaVersion;

            /// <summary>构造。</summary>
            public BindResult(IReadOnlyList<SettingsRegistryRow> rows, uint schemaVersion)
            {
                Rows = rows; SchemaVersion = schemaVersion;
            }
        }

        /// <summary>绑定 + 三类校验。任一违例 ⇒ 聚合 <see cref="BakeValidationException"/>。</summary>
        /// <exception cref="BakeValidationException">词法失败 / 未知键 / 名字或值对账失败 / 域违例。</exception>
        public static BindResult Bind(string json)
        {
            var errors = new List<string>();

            if (!JsonStage1Lexer.TryParse(json, out JsonNode root, out string lexErr))
                throw new BakeValidationException(
                    new List<string> { $"audio_settings_registry.json 阶段1 词法失败:{lexErr}" });

            if (root.Kind != JsonNodeKind.Object)
                throw new BakeValidationException(
                    new List<string> { $"audio_settings_registry.json 根须为对象(实得 {root.Kind})" });

            RejectUnknownKeys(root, RootKeys, "根", errors);

            uint schemaVersion = ReadSchemaVersion(root, errors);

            if (!root.TryGet("rows", out JsonNode rowsNode) || rowsNode.Kind != JsonNodeKind.Array)
            {
                errors.Add("audio_settings_registry.json 缺 rows 数组");
                throw new BakeValidationException(errors);   // 行层校验结构上不可达 ⇒ 早 throw
            }

            var rows = new List<SettingsRegistryRow>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < rowsNode.Items.Count; i++)
            {
                SettingsRegistryRow? row = ParseRow(rowsNode.Items[i], i, seen, errors);
                if (row.HasValue) rows.Add(row.Value);
            }

            ValidateNameReconciliation(rows, errors);
            ValidateValueReconciliation(rows, errors);

            if (errors.Count > 0) throw new BakeValidationException(errors);

            return new BindResult(rows, schemaVersion);
        }

        // ── ① 名字对账:JSON param 集 恰 = MixerRegistry.SettingsVisibleParameters ──

        private static void ValidateNameReconciliation(
            IReadOnlyList<SettingsRegistryRow> rows, List<string> errors)
        {
            IReadOnlyList<string> expected = MixerRegistry.SettingsVisibleParameters;

            var present = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < rows.Count; i++) present.Add(rows[i].Param);

            for (int i = 0; i < expected.Count; i++)
                if (!present.Contains(expected[i]))
                    errors.Add($"[注册表·名字对账] 烘焙注册表缺「{expected[i]}」行 —— " +
                               $"AC①:闭枚举恰 {expected.Count} 员(单一出处 = MixerRegistry.SettingsVisibleParameters)");

            foreach (string param in present)
            {
                bool known = false;
                for (int i = 0; i < expected.Count; i++)
                    if (string.Equals(expected[i], param, StringComparison.Ordinal)) known = true;
                if (!known)
                    errors.Add($"[注册表·名字对账] 烘焙注册表含闭枚举外条目「{param}」—— " +
                               "壳消费集 ⊆ 注册表(越集 = 断言失败);扩条目先改 MixerRegistry.SettingsVisibleParameters");
            }

            if (rows.Count != expected.Count)
                errors.Add($"[注册表·名字对账] 行数 {rows.Count} ≠ 闭枚举 {expected.Count} 员 —— " +
                           "注册表 AC①「恰 8 条(7 总线音量含 Master + mono)」");
        }

        // ── ② 值对账:烘焙出厂默认 == 运行期回退(FallbackRows),双向逐键 ──

        private static void ValidateValueReconciliation(
            IReadOnlyList<SettingsRegistryRow> rows, List<string> errors)
        {
            IReadOnlyList<SettingsRegistryRow> fallback = AudioSettingsRegistry.FallbackRows;

            var baked = new Dictionary<string, SettingsRegistryRow>(StringComparer.Ordinal);
            for (int i = 0; i < rows.Count; i++) baked[rows[i].Param] = rows[i];

            var fb = new Dictionary<string, SettingsRegistryRow>(StringComparer.Ordinal);
            for (int i = 0; i < fallback.Count; i++) fb[fallback[i].Param] = fallback[i];

            foreach (KeyValuePair<string, SettingsRegistryRow> pair in baked)
            {
                if (!fb.TryGetValue(pair.Key, out SettingsRegistryRow fbRow))
                {
                    errors.Add($"[注册表·值对账] 烘焙行「{pair.Key}」在运行期回退集中**不存在** —— " +
                               "回退与烘焙数据必须逐键对应(否则回退静默漏一个参数)");
                    continue;
                }
                if (!pair.Value.FactoryDefault.Equals(fbRow.FactoryDefault))
                    errors.Add($"[注册表·值对账] 「{pair.Key}」出厂默认不一致:烘焙 = " +
                               $"{pair.Value.FactoryDefault},回退 = {fbRow.FactoryDefault} —— " +
                               "AC-44-13「出厂默认 ∈ 烘焙分区」;改一处须同改另一处");
                if (pair.Value.Kind != fbRow.Kind)
                    errors.Add($"[注册表·值对账] 「{pair.Key}」类型不一致:烘焙 = {pair.Value.Kind}," +
                               $"回退 = {fbRow.Kind}");
            }

            foreach (KeyValuePair<string, SettingsRegistryRow> pair in fb)
                if (!baked.ContainsKey(pair.Key))
                    errors.Add($"[注册表·值对账] 运行期回退行「{pair.Key}」在烘焙数据中**不存在** —— " +
                               "回退多出的行 = 该参数无烘焙真源(AC-44-13 未兑现)");
        }

        // ── ③ 逐行解析 + 域检查 ──

        private static SettingsRegistryRow? ParseRow(
            JsonNode row, int i, HashSet<string> seen, List<string> errors)
        {
            if (row.Kind != JsonNodeKind.Object)
            {
                errors.Add($"rows[{i}] 须为对象(实得 {row.Kind})");
                return null;
            }
            RejectUnknownKeys(row, RowKeys, $"rows[{i}]", errors);

            if (!row.TryGet("param", out JsonNode paramNode) || paramNode.Kind != JsonNodeKind.String
                || string.IsNullOrEmpty(paramNode.Str))
            {
                errors.Add($"rows[{i}] 缺非空字符串 param(实得 " +
                           $"{(paramNode == null ? "缺失" : paramNode.Kind.ToString())})");
                return null;
            }
            string param = paramNode.Str;
            if (!seen.Add(param))
            {
                errors.Add($"rows[{i}]:param「{param}」重复(闭枚举内每参数恰一行)");
                return null;
            }

            if (!row.TryGet("type", out JsonNode typeNode) || typeNode.Kind != JsonNodeKind.String)
            {
                errors.Add($"rows[{i}]「{param}」缺字符串 type(闭集 = float / bool)");
                return null;
            }

            SettingsValueKind kind;
            if (string.Equals(typeNode.Str, "float", StringComparison.Ordinal))
                kind = SettingsValueKind.Float;
            else if (string.Equals(typeNode.Str, "bool", StringComparison.Ordinal))
                kind = SettingsValueKind.Bool;
            else
            {
                errors.Add($"rows[{i}]「{param}」type「{typeNode.Str}」∉ 闭集 {{float, bool}} —— " +
                           "GDD :896-909 只用两型(扩型先改 GDD 与本门)");
                return null;
            }

            double min = 0.0, max = 0.0;
            if (kind == SettingsValueKind.Float)
            {
                if (!TryReadNumber(row, "min", i, param, errors, out min)) return null;
                if (!TryReadNumber(row, "max", i, param, errors, out max)) return null;
                if (min >= max)
                    errors.Add($"rows[{i}]「{param}」区间非法:min={min} ≥ max={max}(须 min < max)");
                if (min < 0.0 || max > 1.0)
                    errors.Add($"rows[{i}]「{param}」区间 [{min}, {max}] 越出 GDD :896-909 的 0–1 域");
            }
            else
            {
                for (int k = 0; k < BoolRowForbiddenKeys.Length; k++)
                    if (row.TryGet(BoolRowForbiddenKeys[k], out _))
                        errors.Add($"rows[{i}]「{param}」bool 行不得带 {BoolRowForbiddenKeys[k]}" +
                                   "(GDD :908 mono 行只列类型 / 出厂默认 / 题签,无区间)");
            }

            if (!row.TryGet("factory_default", out JsonNode defNode))
            {
                errors.Add($"rows[{i}]「{param}」缺 factory_default(AC-44-13:出厂默认入烘焙分区)");
                return null;
            }

            SettingsValue factoryDefault;
            if (kind == SettingsValueKind.Float)
            {
                if (defNode.Kind != JsonNodeKind.Integer && defNode.Kind != JsonNodeKind.Float)
                {
                    errors.Add($"rows[{i}]「{param}」factory_default 须为数值(实得 {defNode.Kind})");
                    return null;
                }
                double v = ParseDecimal(defNode);
                if (v < min || v > max)
                    errors.Add($"rows[{i}]「{param}」factory_default={v} 越出区间 [{min}, {max}]");
                factoryDefault = SettingsValue.OfFloat(v);
            }
            else
            {
                if (defNode.Kind != JsonNodeKind.Boolean)
                {
                    errors.Add($"rows[{i}]「{param}」factory_default 须为布尔(实得 {defNode.Kind})");
                    return null;
                }
                factoryDefault = SettingsValue.OfBool(defNode.Bool);
            }

            if (!row.TryGet("player_group", out JsonNode groupNode) || groupNode.Kind != JsonNodeKind.String
                || string.IsNullOrEmpty(groupNode.Str))
            {
                errors.Add($"rows[{i}]「{param}」缺非空 player_group(OQ-SS-3=甲 的字段位)");
                return null;
            }

            if (!row.TryGet("label_source", out JsonNode labelNode) || labelNode.Kind != JsonNodeKind.String
                || string.IsNullOrEmpty(labelNode.Str))
            {
                errors.Add($"rows[{i}]「{param}」缺非空 label_source(GDD :896-909「题签 / 页背注语义源」)");
                return null;
            }

            return new SettingsRegistryRow(
                param, kind, min, max, factoryDefault, groupNode.Str, labelNode.Str);
        }

        private static bool TryReadNumber(
            JsonNode row, string key, int i, string param, List<string> errors, out double value)
        {
            value = 0.0;
            if (!row.TryGet(key, out JsonNode node))
            {
                errors.Add($"rows[{i}]「{param}」float 行缺 {key}");
                return false;
            }
            if (node.Kind != JsonNodeKind.Integer && node.Kind != JsonNodeKind.Float)
            {
                errors.Add($"rows[{i}]「{param}」{key} 须为数值(实得 {node.Kind})");
                return false;
            }
            value = ParseDecimal(node);
            return true;
        }

        /// <summary>整数 / 浮点 token → double(值来自词法层 Decimal 形态,不经 float)。
        /// <para>⚠️ 设置值是表现层偏好,不在 <c>Fix</c> 域内(见文件头),故此处用 double 合法。</para></summary>
        private static double ParseDecimal(JsonNode node)
        {
            if (node.Kind == JsonNodeKind.Integer) return node.Int;
            return double.Parse(node.RawText, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture);
        }

        private static uint ReadSchemaVersion(JsonNode root, List<string> errors)
        {
            if (!root.TryGet("schema_version", out JsonNode n))
            {
                errors.Add("audio_settings_registry.json 缺 schema_version");
                return 0;
            }
            if (n.Kind != JsonNodeKind.Integer || n.Int < 0 || n.Int > uint.MaxValue)
            {
                errors.Add($"schema_version 须为 u32 域整数(实得 {n.Kind}={n.RawText})");
                return 0;
            }
            return (uint)n.Int;
        }

        private static void RejectUnknownKeys(JsonNode node, string[] allowed, string where, List<string> errors)
        {
            foreach (string key in node.Keys)
            {
                bool ok = false;
                for (int i = 0; i < allowed.Length; i++)
                    if (string.Equals(allowed[i], key, StringComparison.Ordinal)) ok = true;
                if (!ok)
                    errors.Add($"{where} 未知键「{key}」∉ 白名单 {{{string.Join(", ", allowed)}}}" +
                               " —— ADR-014 §三:未知键 = 烘焙期硬失败(拼写错会静默丢字段)");
            }
        }
    }
}
