// 权威来源:ADR-014 §三(阶段 2 per-schema 绑定:白名单 / 类型 / 值域)·
//          ADR-038 §D1(只放已裁定的三值)· §D2(52 的 $ref 由 52 的 Binder 解析,不在这里)
//          · GDD design/gdd/time-and-weather.md §Tuning Knobs(:515-519)
//
// ⚠️ 本件 = `assets/data/5_time.json` 的阶段 2 绑定器:白名单 + 类型 + 值域。
//    它是 AC-5-11「装载期校验器」的**烘焙期同构体** —— 同一组关系规则在
//    `Sim.TimeParams.Validate()` 里也有一份(运行期),两处都必须守,因为
//    烘焙期守的是「作者态写错了」,运行期守的是「产物/存档被改坏了」。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary><c>5_time.json</c> 的阶段 2 绑定器。</summary>
    /// <remarks>
    /// ⚠️ 刻意 <c>public</c>(与 `DiagnosisReadFloorBinder` 的 internal + Probe 双件不同):
    /// 负控夹具要**直接喂非法 JSON**给 <see cref="Bind"/>(V-3 的判据面),而
    /// `Editor.Tools.Bake` 的 IVT 只开了 `Sim.Contracts.Tests`。
    /// 多开一条 IVT 或加一个 Probe 薄壳都是**为测试改生产形态**;把纯函数转 public 更小。
    /// </remarks>
    public static class TimeParamsBinder
    {
        // ── 已知键白名单(ADR-014 §三:未知键 = 烘焙期硬失败,防拼写错误静默丢字段)──

        private static readonly string[] RootKeys =
        {
            "schema_version", "ticks_per_day", "night_start", "night_span",
        };

        /// <summary>
        /// 已知但**值未裁定**、因而**刻意不进 schema** 的键。
        /// <para>它们若出现在 JSON 里,报**专用**错误而不是笼统的「未知键」——
        /// 让下一个人立刻知道「不是拼错,是还没裁」。</para>
        /// </summary>
        private static readonly string[] UndecidedKeys =
        {
            "ticks_per_season", "seasons_per_year", "weather_block_ticks",
        };

        /// <summary>绑定结果。</summary>
        public readonly struct BindResult
        {
            /// <summary>日长(已过值域校验)。</summary>
            public readonly long TicksPerDay;

            /// <summary>夜起点(已过值域校验)。</summary>
            public readonly long NightStart;

            /// <summary>夜长(已过值域校验)。</summary>
            public readonly long NightSpan;

            /// <summary>源 <c>schema_version</c>(随产物头落盘)。</summary>
            public readonly uint SchemaVersion;

            public BindResult(long ticksPerDay, long nightStart, long nightSpan, uint schemaVersion)
            {
                TicksPerDay = ticksPerDay;
                NightStart = nightStart;
                NightSpan = nightSpan;
                SchemaVersion = schemaVersion;
            }
        }

        /// <summary>绑定 + 校验。任一违例 ⇒ 聚合 <see cref="BakeValidationException"/>。</summary>
        /// <param name="json"><c>5_time.json</c> 原文。</param>
        public static BindResult Bind(string json)
        {
            var errors = new List<string>();

            if (!JsonStage1Lexer.TryParse(json, out JsonNode root, out string lexErr))
            {
                errors.Add($"5_time.json 阶段1 词法失败:{lexErr}");
                throw new BakeValidationException(errors);
            }

            if (root.Kind != JsonNodeKind.Object)
            {
                errors.Add($"5_time.json 根须为对象(实得 {root.Kind})");
                throw new BakeValidationException(errors);
            }

            RejectUnknownKeys(root, errors);

            uint schemaVersion = ReadSchemaVersion(root, errors);
            bool okTpd = ReadLong(root, "ticks_per_day", errors, out long ticksPerDay);
            bool okStart = ReadLong(root, "night_start", errors, out long nightStart);
            bool okSpan = ReadLong(root, "night_span", errors, out long nightSpan);

            // ⚠️ 三个都解析成功才跑关系校验 —— 否则缺失的键以 0 参与,叠出
            //    「缺失 + 值域违反」两条错误,违「一处缺陷一条错」的排他口径。
            if (okTpd && okStart && okSpan)
            {
                // 复用运行期的同一组规则(单一判据出处):`TimeParams.Validate()`。
                try
                {
                    new TimeParams
                    {
                        TICKS_PER_DAY = ticksPerDay,
                        NIGHT_START = nightStart,
                        NIGHT_SPAN = nightSpan,
                    }.Validate();
                }
                catch (ArgumentException ex)
                {
                    errors.Add($"5_time.json 值域违反:{ex.Message}");
                }
            }

            if (errors.Count > 0)
                throw new BakeValidationException(errors);

            return new BindResult(ticksPerDay, nightStart, nightSpan, schemaVersion);
        }

        // ── 读件(严格:类型不符即记错,绝不隐式转换)───────────────────────────

        private static uint ReadSchemaVersion(JsonNode obj, List<string> errors)
        {
            if (!obj.TryGet("schema_version", out JsonNode n))
            {
                errors.Add("5_time.json 缺 schema_version");
                return 0;
            }
            if (n.Kind != JsonNodeKind.Integer || n.Int < 0 || n.Int > uint.MaxValue)
            {
                errors.Add($"schema_version 须为 u32 域整数(实得 {n.Kind}={n.RawText})");
                return 0;
            }
            return (uint)n.Int;
        }

        /// <summary>
        /// 读整数字段。**禁**字符串 / 浮点 —— 这里的三个量都不是 `Fix`,
        /// 按 ADR-014 §三 的「`Fix` 才写字符串」口径,它们就该是 JSON 整数。
        /// </summary>
        private static bool ReadLong(JsonNode obj, string key, List<string> errors, out long value)
        {
            value = 0L;
            if (!obj.TryGet(key, out JsonNode n))
            {
                errors.Add($"5_time.json 缺 {key}");
                return false;
            }
            if (n.Kind != JsonNodeKind.Integer)
            {
                errors.Add($"{key} 须为 JSON 整数(实得 {n.Kind}={n.RawText};" +
                           "非 Fix 字段禁写字符串)");
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
                if (allowed) continue;

                bool undecided = false;
                for (int i = 0; i < UndecidedKeys.Length; i++)
                    if (string.Equals(UndecidedKeys[i], key, StringComparison.Ordinal)) { undecided = true; break; }

                if (undecided)
                    errors.Add($"5_time.json 的「{key}」**值尚未裁定** ⇒ 刻意不进 schema。" +
                               "占位值与已裁定值在同形文件里不可分辨(ADR-038 §D1);" +
                               "先裁值,再连同 schema_version 一起升,不得先塞占位值");
                else
                    errors.Add($"5_time.json 未知键「{key}」[ADR-014·unknown-key]" +
                               "(拼写错误会静默丢字段 ⇒ 构建期硬失败)");
            }
        }
    }
}
