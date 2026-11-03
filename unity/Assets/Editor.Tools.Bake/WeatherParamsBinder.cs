// 权威来源:ADR-014 §三(阶段 2 per-schema 绑定:白名单 / 类型 / 值域)
//          · ADR-038 Amendment A(5 的天气参数外部化;2026-11-03 用户裁定 6000 / 100)
//          · GDD design/gdd/time-and-weather.md §F-5.3(`WEATHER_BLOCK` 块哈希)与 §Tuning Knobs
//
// ⚠️ 值域规则复用**运行期**的 `Sim.WeatherParams.Validate()`(单一判据出处);
//    本件只负责白名单 / 类型 / 读值,不复制规则。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary><c>5_weather.json</c> 的阶段 2 绑定器。</summary>
    /// <remarks>刻意 <c>public</c>:负控夹具要直接喂非法 JSON(与 `TimeParamsBinder` 同口径)。</remarks>
    public static class WeatherParamsBinder
    {
        private static readonly string[] RootKeys =
        {
            "schema_version", "weather_block_ticks", "intensity_max",
        };

        /// <summary>绑定结果(已过值域校验)。</summary>
        public readonly struct BindResult
        {
            /// <summary>天气相干块长(tick)。</summary>
            public readonly long WeatherBlockTicks;

            /// <summary>强度上界。</summary>
            public readonly long IntensityMax;

            /// <summary>源 <c>schema_version</c>。</summary>
            public readonly uint SchemaVersion;

            public BindResult(long weatherBlockTicks, long intensityMax, uint schemaVersion)
            {
                WeatherBlockTicks = weatherBlockTicks;
                IntensityMax = intensityMax;
                SchemaVersion = schemaVersion;
            }
        }

        /// <summary>绑定 + 校验。任一违例 ⇒ 聚合 <see cref="BakeValidationException"/>。</summary>
        public static BindResult Bind(string json)
        {
            var errors = new List<string>();

            if (!JsonStage1Lexer.TryParse(json, out JsonNode root, out string lexErr))
            {
                errors.Add($"5_weather.json 阶段1 词法失败:{lexErr}");
                throw new BakeValidationException(errors);
            }

            if (root.Kind != JsonNodeKind.Object)
            {
                errors.Add($"5_weather.json 根须为对象(实得 {root.Kind})");
                throw new BakeValidationException(errors);
            }

            RejectUnknownKeys(root, errors);

            uint schemaVersion = ReadSchemaVersion(root, errors);
            bool okBlock = ReadLong(root, "weather_block_ticks", errors, out long blockTicks);
            bool okMax = ReadLong(root, "intensity_max", errors, out long intensityMax);

            if (okBlock && okMax)
            {
                try
                {
                    new WeatherParams
                    {
                        WEATHER_BLOCK_TICKS = blockTicks,
                        INTENSITY_MAX = intensityMax,
                    }.Validate();
                }
                catch (ArgumentException ex)
                {
                    errors.Add($"5_weather.json 值域违反:{ex.Message}");
                }
            }

            if (errors.Count > 0)
                throw new BakeValidationException(errors);

            return new BindResult(blockTicks, intensityMax, schemaVersion);
        }

        private static uint ReadSchemaVersion(JsonNode obj, List<string> errors)
        {
            if (!obj.TryGet("schema_version", out JsonNode n))
            {
                errors.Add("5_weather.json 缺 schema_version");
                return 0;
            }
            if (n.Kind != JsonNodeKind.Integer || n.Int < 0 || n.Int > uint.MaxValue)
            {
                errors.Add($"schema_version 须为 u32 域整数(实得 {n.Kind}={n.RawText})");
                return 0;
            }
            return (uint)n.Int;
        }

        private static bool ReadLong(JsonNode obj, string key, List<string> errors, out long value)
        {
            value = 0L;
            if (!obj.TryGet(key, out JsonNode n))
            {
                errors.Add($"5_weather.json 缺 {key}");
                return false;
            }
            if (n.Kind != JsonNodeKind.Integer)
            {
                errors.Add($"{key} 须为 JSON 整数(实得 {n.Kind}={n.RawText};非 Fix 字段禁写字符串)");
                return false;
            }
            value = n.Int;
            return true;
        }

        private static void RejectUnknownKeys(JsonNode obj, List<string> errors)
        {
            foreach (string key in obj.Keys)
            {
                bool allowed = false;
                for (int i = 0; i < RootKeys.Length; i++)
                    if (string.Equals(RootKeys[i], key, StringComparison.Ordinal)) { allowed = true; break; }
                if (!allowed)
                    errors.Add($"5_weather.json 未知键「{key}」[ADR-014·unknown-key]" +
                               "(拼写错误会静默丢字段 ⇒ 构建期硬失败)");
            }
        }
    }
}
