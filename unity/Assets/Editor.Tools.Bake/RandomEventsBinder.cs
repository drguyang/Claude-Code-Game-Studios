// 权威来源:ADR-038 §D2(跨表引用 `{"$ref": "<artifact>.<field>"}` 的解析与同值断言)
//          · §D4(52 四值)· ADR-014 §三(阶段 2 白名单 / 类型 / 值域)
//          · GDD design/gdd/random-events.md §F1 / §规则六 / `:1075`(不整除 ⇒ 构建期报错)
//          · AC-5-05 —— 52 的数据集**零字面副本**,只许 `$ref`
//
// ⚠️ 本件是 AC-5-05 `THEN` 句里「**引用解析后与 5 同值**」的唯一执行体:
//    `$ref` 解析失败 / 目标不存在 / 值非法 ⇒ **烘焙失败**(不是警告、不是默认值)。
//
// ⚠️ 引用表由调用方注入(`IReadOnlyDictionary<string,long>`)—— 不在这里读文件:
//    ① 单测可直接喂假表(负控才写得出);② 解析源与「谁产出了 5 的值」解耦。

using System;
using System.Collections.Generic;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary><c>52_random_events.json</c> 的阶段 2 绑定器。</summary>
    /// <remarks>与 `TimeParamsBinder` 同款转 <c>public</c>:负控夹具要直接喂非法 JSON。</remarks>
    public static class RandomEventsBinder
    {
        private static readonly string[] RootKeys =
        {
            "schema_version", "ticks_per_day", "roll_interval", "window_size", "base", "budget_max",
        };

        /// <summary>`ticks_per_day` 的**唯一合法写法**是 `{"$ref": "<artifact>.<field>"}`。</summary>
        public const string RefKey = "$ref";

        /// <summary>绑定结果。</summary>
        public readonly struct BindResult
        {
            /// <summary>日长(**已解析**的引用值)。</summary>
            public readonly long TicksPerDay;

            /// <summary>子区间长度(tick)。</summary>
            public readonly long RollInterval;

            /// <summary>每预算窗口的抽取条数上限。</summary>
            public readonly int WindowSize;

            /// <summary>每日基础事件数。</summary>
            public readonly int Base;

            /// <summary>每日硬上限。</summary>
            public readonly int BudgetMax;

            /// <summary>源 <c>schema_version</c>。</summary>
            public readonly uint SchemaVersion;

            public BindResult(long ticksPerDay, long rollInterval, int windowSize,
                              int baseEvents, int budgetMax, uint schemaVersion)
            {
                TicksPerDay = ticksPerDay;
                RollInterval = rollInterval;
                WindowSize = windowSize;
                Base = baseEvents;
                BudgetMax = budgetMax;
                SchemaVersion = schemaVersion;
            }
        }

        /// <summary>绑定 + 校验 + `$ref` 解析。</summary>
        /// <param name="json"><c>52_random_events.json</c> 原文。</param>
        /// <param name="refs">可解析的引用表(键 = <c>"&lt;artifact&gt;.&lt;field&gt;"</c>)。</param>
        /// <exception cref="BakeValidationException">任一违例(聚合)。</exception>
        public static BindResult Bind(string json, IReadOnlyDictionary<string, long> refs)
        {
            var errors = new List<string>();

            if (!JsonStage1Lexer.TryParse(json, out JsonNode root, out string lexErr))
            {
                errors.Add($"52_random_events.json 阶段1 词法失败:{lexErr}");
                throw new BakeValidationException(errors);
            }

            if (root.Kind != JsonNodeKind.Object)
            {
                errors.Add($"52_random_events.json 根须为对象(实得 {root.Kind})");
                throw new BakeValidationException(errors);
            }

            RejectUnknownKeys(root, errors);

            uint schemaVersion = ReadSchemaVersion(root, errors);
            bool okTpd = ReadRefValue(root, refs, errors, out long ticksPerDay);
            bool okRoll = ReadLong(root, "roll_interval", errors, out long rollInterval);
            bool okWindow = ReadInt(root, "window_size", errors, out int windowSize);
            bool okBase = ReadInt(root, "base", errors, out int baseEvents);
            bool okMax = ReadInt(root, "budget_max", errors, out int budgetMax);

            if (okRoll && okTpd)
            {
                if (rollInterval <= 0)
                    errors.Add($"roll_interval={rollInterval} 须 > 0 —— 否则子区间不存在(除零)");
                else if (ticksPerDay % rollInterval != 0)
                    errors.Add(
                        $"ROLL_INTERVAL 不整除 TICKS_PER_DAY({rollInterval} ∤ {ticksPerDay})" +
                        " —— 日边界与子区间边界错位,预算重置点不唯一(GDD `:1075` 要求构建期报错)");
            }

            if (okWindow && windowSize < 1)
                errors.Add($"window_size={windowSize} 须 ≥ 1 —— =0 则永不抽取(功能死亡)");

            if (okBase && baseEvents < 0)
                errors.Add($"base={baseEvents} 须 ≥ 0");

            if (okMax && okBase && budgetMax < baseEvents)
                errors.Add($"budget_max={budgetMax} 须 ≥ base={baseEvents} —— 否则基础预算就被硬顶压死");

            if (errors.Count > 0)
                throw new BakeValidationException(errors);

            return new BindResult(ticksPerDay, rollInterval, windowSize, baseEvents, budgetMax, schemaVersion);
        }

        // ── 读件 ─────────────────────────────────────────────────────────

        /// <summary>
        /// 读一个 `{"$ref": "..."}` 字段并解析。
        /// <para>⚠️ 值是**数字/字符串**一律拒 —— 那正是 AC-5-05 说的「字面副本」。</para>
        /// </summary>
        private static bool ReadRefValue(
            JsonNode obj, IReadOnlyDictionary<string, long> refs, List<string> errors, out long value)
        {
            value = 0L;
            if (!obj.TryGet("ticks_per_day", out JsonNode n))
            {
                errors.Add("52_random_events.json 缺 ticks_per_day(DC-3 要求与 5 同值)");
                return false;
            }

            if (n.Kind != JsonNodeKind.Object)
            {
                errors.Add(
                    "ticks_per_day 必须写成 {\"$ref\": \"5_time.ticks_per_day\"},实际是 " +
                    $"{n.Kind}={n.RawText} —— **字面副本违反 AC-5-05**(52 只许引用,不许自带一份)");
                return false;
            }

            string refPath = null;
            int propCount = 0;
            foreach (KeyValuePair<string, JsonNode> kv in n.Props)
            {
                propCount++;
                if (string.Equals(kv.Key, RefKey, StringComparison.Ordinal)) refPath = kv.Value.Str;
            }

            if (propCount != 1 || refPath == null)
            {
                errors.Add($"ticks_per_day 的对象须**恰含**一个 \"{RefKey}\" 键(实得 {propCount} 个键)");
                return false;
            }

            if (refs == null || !refs.TryGetValue(refPath, out value))
            {
                errors.Add($"ticks_per_day 的引用「{refPath}」无法解析 —— " +
                           "目标产物未在同一烘焙批产出,或其字段名不在引用表内");
                return false;
            }

            if (value <= 0)
            {
                errors.Add($"ticks_per_day 的引用「{refPath}」解析出 {value} —— 须 > 0");
                return false;
            }

            return true;
        }

        private static uint ReadSchemaVersion(JsonNode obj, List<string> errors)
        {
            if (!obj.TryGet("schema_version", out JsonNode n))
            {
                errors.Add("52_random_events.json 缺 schema_version");
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
                errors.Add($"52_random_events.json 缺 {key}");
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

        private static bool ReadInt(JsonNode obj, string key, List<string> errors, out int value)
        {
            value = 0;
            if (!ReadLong(obj, key, errors, out long v)) return false;
            if (v < int.MinValue || v > int.MaxValue)
            {
                errors.Add($"{key}={v} 超出 int 域");
                return false;
            }
            value = (int)v;
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
                    errors.Add($"52_random_events.json 未知键「{key}」[ADR-014·unknown-key]" +
                               "(拼写错误会静默丢字段 ⇒ 构建期硬失败)");
            }
        }
    }
}
