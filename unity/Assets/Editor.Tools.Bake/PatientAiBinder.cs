// 权威来源:ADR-014 §三(阶段 2 per-schema 绑定:白名单 / 闭集 / FixParse 换算出口)
//          · `docs/tech-debt-register.md` `TD-audio-032` ①(调参常量入生产)
//          · GDD design/gdd/patient-ai.md §Tuning 一 / 三(两族硬约束)
//
// ⚠️ 本件 = `assets/data/ai_patient.json` 的**绑定 + 唯一校验点**(阶段 2 的绑定面)。
//
// ⚠️ 两节各承一族硬约束,**校验逻辑不重实现** —— 直接复用运行期已有的
//    `CueIntervals.Validate` / `BehaviorBands.Validate`(它们是**同一个**约束表的
//    唯一实现;在此复写一份 = 第二真源,两处会漂移)。
//    本件只多做一件事:**把校验结果挂上构建期上下文**(哪个文件 / 哪一节),
//    让烘焙失败的报文指向源文件而不是运行期类型。
//
// ⚠️ 数值 = 现有代码常量的照抄(本轮 `TD-audio-032` ① 只搬「存放位置」,不改值)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.PatientAI;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>`ai_patient.json` 的阶段 2 绑定器:**唯一**产出 13 调参表的地方。</summary>
    internal static class PatientAiBinder
    {
        // ── 已知键白名单(ADR-014 §三:未知键 = 烘焙期硬失败,防拼写错误静默丢字段)──

        private static readonly string[] RootKeys =
        {
            "schema_version", "_note", "cue_intervals", "behavior_bands",
        };

        private static readonly string[] CueIntervalKeys =
        {
            "_note", "severe", "mild", "recover",
        };

        private static readonly string[] BehaviorBandKeys =
        {
            "_note", "mild_min", "seek_min", "collapse_min",
            "recover_max", "hyst", "death_band_min",
        };

        /// <summary>绑定结果(两族调参 + 源 schema 版本,供写方落盘)。</summary>
        public readonly struct BindResult
        {
            /// <summary>cue 间隔表(`CUE_INTERVAL[*]`,tick 域整数)。</summary>
            public readonly CueIntervals CueIntervals;

            /// <summary>行为分档阈值表(`BEHAVIOR_BAND_*`,Q16.16 raw 落盘)。</summary>
            public readonly BehaviorBands BehaviorBands;

            /// <summary>源 `schema_version`(u32 域;随产物头落盘)。</summary>
            public readonly uint SchemaVersion;

            public BindResult(CueIntervals cueIntervals, BehaviorBands behaviorBands, uint schemaVersion)
            {
                CueIntervals = cueIntervals;
                BehaviorBands = behaviorBands;
                SchemaVersion = schemaVersion;
            }
        }

        /// <summary>绑定 + 校验。任一违例 ⇒ 聚合 <see cref="BakeValidationException"/>。</summary>
        /// <param name="json">`ai_patient.json` 原文。</param>
        public static BindResult Bind(string json)
        {
            var errors = new List<string>();

            if (!JsonStage1Lexer.TryParse(json, out JsonNode root, out string lexErr))
            {
                errors.Add($"ai_patient.json 阶段1 词法失败:{lexErr}");
                throw new BakeValidationException(errors);
            }

            if (root.Kind != JsonNodeKind.Object)
            {
                errors.Add($"ai_patient.json 根须为对象(实得 {root.Kind})");
                throw new BakeValidationException(errors);
            }

            RejectUnknownKeys(root, RootKeys, "ai_patient.json 根", errors);
            uint schemaVersion = ReadSchemaVersion(root, errors);

            // ⚠️ 两节**各自独立**解析与校验:一节失败不得掩盖另一节(否则修一处再来一次)。
            CueIntervals? cueIntervals = ReadCueIntervals(root, errors);
            BehaviorBands? behaviorBands = ReadBehaviorBands(root, errors);

            if (errors.Count > 0)
                throw new BakeValidationException(errors);

            return new BindResult(cueIntervals.Value, behaviorBands.Value, schemaVersion);
        }

        // ── cue_intervals ──────────────────────────────────────────────

        private static CueIntervals? ReadCueIntervals(JsonNode root, List<string> errors)
        {
            const string Where = "ai_patient.json·cue_intervals";

            if (!root.TryGet("cue_intervals", out JsonNode obj))
            {
                errors.Add($"{Where} 缺失 —— 13 的 cue 间隔无来源");
                return null;
            }
            if (obj.Kind != JsonNodeKind.Object)
            {
                errors.Add($"{Where} 须为对象(实得 {obj.Kind})");
                return null;
            }

            RejectUnknownKeys(obj, CueIntervalKeys, Where, errors);

            bool okSevere = ReadInt32(obj, "severe", Where, errors, out int severe);
            bool okMild = ReadInt32(obj, "mild", Where, errors, out int mild);
            bool okRecover = ReadInt32(obj, "recover", Where, errors, out int recover);

            if (!okSevere || !okMild || !okRecover)
                return null;

            var intervals = new CueIntervals(severe, mild, recover);

            // 硬约束复用运行期实现(同一约束表,不在此复写)
            List<string> constraintErrors = CueIntervals.Validate(intervals);
            for (int i = 0; i < constraintErrors.Count; i++)
                errors.Add($"{Where}:{constraintErrors[i]}");

            return intervals;
        }

        // ── behavior_bands ─────────────────────────────────────────────

        private static BehaviorBands? ReadBehaviorBands(JsonNode root, List<string> errors)
        {
            const string Where = "ai_patient.json·behavior_bands";

            if (!root.TryGet("behavior_bands", out JsonNode obj))
            {
                errors.Add($"{Where} 缺失 —— 13 的行为分档无来源");
                return null;
            }
            if (obj.Kind != JsonNodeKind.Object)
            {
                errors.Add($"{Where} 须为对象(实得 {obj.Kind})");
                return null;
            }

            RejectUnknownKeys(obj, BehaviorBandKeys, Where, errors);

            bool okMild = ReadFixFloat(obj, "mild_min", Where, errors, out float mildMin);
            bool okSeek = ReadFixFloat(obj, "seek_min", Where, errors, out float seekMin);
            bool okCollapse = ReadFixFloat(obj, "collapse_min", Where, errors, out float collapseMin);
            bool okRecover = ReadFixFloat(obj, "recover_max", Where, errors, out float recoverMax);
            bool okHyst = ReadFixFloat(obj, "hyst", Where, errors, out float hyst);
            bool okDeath = ReadFixFloat(obj, "death_band_min", Where, errors, out float deathBandMin);

            if (!okMild || !okSeek || !okCollapse || !okRecover || !okHyst || !okDeath)
                return null;

            var bands = new BehaviorBands(mildMin, seekMin, collapseMin, recoverMax, hyst, deathBandMin);

            List<string> constraintErrors = BehaviorBands.Validate(bands);
            for (int i = 0; i < constraintErrors.Count; i++)
                errors.Add($"{Where}:{constraintErrors[i]}");

            return bands;
        }

        // ── 读件(严格:类型不符即记错,绝不隐式转换)───────────────────────────

        private static uint ReadSchemaVersion(JsonNode obj, List<string> errors)
        {
            if (!obj.TryGet("schema_version", out JsonNode n))
            {
                errors.Add("ai_patient.json 缺 schema_version");
                return 0;
            }
            if (n.Kind != JsonNodeKind.Integer || n.Int < 0 || n.Int > uint.MaxValue)
            {
                errors.Add($"schema_version 须为 u32 域整数(实得 {n.Kind}={n.RawText})");
                return 0;
            }
            return (uint)n.Int;
        }

        /// <summary>整数域字段(tick 计数):JSON **数字**整数(禁字符串 —— 它们是计数不是 Fix)。</summary>
        private static bool ReadInt32(JsonNode obj, string key, string where,
                                      List<string> errors, out int value)
        {
            value = 0;
            if (!obj.TryGet(key, out JsonNode n))
            {
                errors.Add($"{where} 缺 {key}");
                return false;
            }
            if (n.Kind != JsonNodeKind.Integer || n.Int < int.MinValue || n.Int > int.MaxValue)
            {
                errors.Add($"{where} 的 {key} 须为 i32 域整数(实得 {n.Kind}={n.RawText})");
                return false;
            }
            value = (int)n.Int;
            return true;
        }

        /// <summary>[0,1] 域阈值:JSON 里写**字符串** Fix 字面量 → `FixParse`(ADR-014 §三;
        /// 禁 JSON 数字 —— 那会经 double 中转,绕开 `FixParse` 的浮点泄漏防线)。
        /// <returns>true = 成功解析且换算为 float(值域校验可跑)。</returns></summary>
        private static bool ReadFixFloat(JsonNode obj, string key, string where,
                                         List<string> errors, out float value)
        {
            value = 0f;
            if (!obj.TryGet(key, out JsonNode n))
            {
                errors.Add($"{where} 缺 {key}");
                return false;
            }
            if (n.Kind != JsonNodeKind.String)
            {
                errors.Add($"{where} 的 {key} 须为字符串 Fix 字面量(实得 {n.Kind}={n.RawText}) " +
                           "[ADR-014·fix-string]");
                return false;
            }
            try
            {
                Fix fix = FixParse.Parse(n.Str);
                value = fix.ToFloat();
                return true;
            }
            catch (Exception ex)
            {
                errors.Add($"{where} 的 {key}=\"{n.Str}\" 解析失败:{ex.Message} [ADR-014·fix-string]");
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
