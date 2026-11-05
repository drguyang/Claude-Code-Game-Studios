// 权威来源:ADR-014 §三(阶段 2 per-schema 绑定:白名单 / 类型 / 值域 / 拒收表)
//          · GDD random-events.md 规则二(池条目 schema)· 规则十一(P0 逐档内容)
//          · Tuning Knobs `:1142` / `:1157`(`TODMult` 与 `TOD_MULT_MAX` 的构建期约束)
//          · DC-2 / D-21-17(`W_base` 是 int 不是 Fix;乘子写分子/分母)
//
// ⚠️ 本件 = `assets/data/52_event_pool.json` 的阶段 2 绑定器:白名单 + 类型 + 值域 + 拒收。
//    与 `TimeParamsBinder` / `RandomEventsBinder` 同格(纯函数、零 UnityEngine、EditMode 可直驱)。
//
// ⚠️ **刻意 `public`**(同 `TimeParamsBinder` 的理由):负控夹具要直接喂非法 JSON 给
//    `Bind`,而 `Editor.Tools.Bake` 的 IVT 只开 `Sim.Contracts.Tests`。
//
// ⚠️ **本件负责「规范化」而不是「原样搬运」**:
//    ① 条目按 **key 升序**排序(重复 key ⇒ 拒收)—— 游走序是确定性判据的一部分(GDD `:687`);
//    ② `trigger_mode = script` 的条目**仍入表**(它们是注册表成员),但运行期抽池要排除;
//       本件只保证「表里有它们」,不替运行期做过滤(过滤在装配层,见 `CompositionRoot`)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary><c>52_event_pool.json</c> 的阶段 2 绑定器。</summary>
    public static class EventPoolBinder
    {
        // ── 已知键白名单(ADR-014 §三:未知键 = 烘焙期硬失败)──────────────────

        private static readonly string[] RootKeys =
        {
            "schema_version", "night_threat_mult", "tod_mult_max", "entries",
        };

        private static readonly string[] EntryKeys =
        {
            "key", "w_base", "tier", "spawn_anchor", "trigger_mode", "handler", "cause_flag",
            // 🔁 2026-11-05(`TD-randomevents-06`):急召出诊标记。**必填** ——
            //    缺字段不能默认 false:那会让「37 还没定内容」与「定了、说不是」不可分,
            //    而这两者一个该等、一个该停。
            "is_house_call",
        };

        /// <summary>档名 → id(闭集四员,规则三「属且只属一档」)。</summary>
        private static readonly string[] TierNames = { "threat", "opportunity", "reaction", "disaster" };

        /// <summary>锚点名 → id(P0 三员,规则十一 ③)。</summary>
        private static readonly string[] AnchorNames = { "clinic_front", "travel_path", "gather_point" };

        /// <summary>触发方式名 → id。</summary>
        private static readonly string[] TriggerNames = { "random", "script" };

        /// <summary>处理方名 → id。</summary>
        private static readonly string[] HandlerNames = { "encounter", "medical" };

        /// <summary>绑定结果。</summary>
        public readonly struct BindResult
        {
            /// <summary>规范化后的条目(按 key 升序)。</summary>
            public readonly IReadOnlyList<EventPoolEntryData> Entries;

            public readonly int NightThreatMultNum;
            public readonly int NightThreatMultDen;
            public readonly int TodMultMaxNum;
            public readonly int TodMultMaxDen;
            public readonly uint SchemaVersion;

            public BindResult(IReadOnlyList<EventPoolEntryData> entries,
                              int nightNum, int nightDen, int maxNum, int maxDen, uint schemaVersion)
            {
                Entries = entries;
                NightThreatMultNum = nightNum;
                NightThreatMultDen = nightDen;
                TodMultMaxNum = maxNum;
                TodMultMaxDen = maxDen;
                SchemaVersion = schemaVersion;
            }
        }

        /// <summary>绑定 + 校验 + 规范化。</summary>
        /// <param name="json"><c>52_event_pool.json</c> 原文。</param>
        /// <exception cref="BakeValidationException">任一违例(聚合)。</exception>
        public static BindResult Bind(string json)
        {
            var errors = new List<string>();

            if (!JsonStage1Lexer.TryParse(json, out JsonNode root, out string lexErr))
                throw new BakeValidationException(new List<string>
                {
                    $"52_event_pool.json 阶段1 词法失败:{lexErr}",
                });

            if (root.Kind != JsonNodeKind.Object)
                throw new BakeValidationException(new List<string>
                {
                    $"52_event_pool.json 根须为对象(实得 {root.Kind})",
                });

            RejectUnknownKeys(root, RootKeys, "52_event_pool.json", errors);

            uint schemaVersion = ReadSchemaVersion(root, errors);

            int nightNum = 0, nightDen = 1, maxNum = 0, maxDen = 1;
            bool okNight = ReadFraction(root, "night_threat_mult", errors, out nightNum, out nightDen);
            bool okMax = ReadFraction(root, "tod_mult_max", errors, out maxNum, out maxDen);

            // ── 乘子约束(GDD Tuning Knobs :1142 / :1157)────────────────────
            if (okNight)
            {
                if (nightNum < 0 || nightDen <= 0)
                    errors.Add($"night_threat_mult = {nightNum}/{nightDen} 非法 —— 须 num ≥ 0 且 den > 0");
                else if (nightNum < nightDen)
                    errors.Add($"night_threat_mult = {nightNum}/{nightDen} < 1 —— " +
                               "GDD `:1142` 要求 num ≥ den(TODMult 只抬不压)");
            }

            if (okMax)
            {
                if (maxNum < 0 || maxDen <= 0)
                    errors.Add($"tod_mult_max = {maxNum}/{maxDen} 非法 —— 须 num ≥ 0 且 den > 0");
                else if (maxNum < maxDen)
                    errors.Add($"tod_mult_max = {maxNum}/{maxDen} < 1 —— GDD `:1157` 要求 num ≥ den");
            }

            if (okNight && okMax && nightDen > 0 && maxDen > 0)
            {
                // 交叉相乘比较(整数域,禁浮点):nightNum/nightDen > maxNum/maxDen ⇒ 超上界
                long lhs = (long)nightNum * maxDen;
                long rhs = (long)maxNum * nightDen;
                if (lhs > rhs)
                    errors.Add($"night_threat_mult = {nightNum}/{nightDen} > tod_mult_max = {maxNum}/{maxDen} —— " +
                               "GDD `:1142` 要求 `TODMult ≤ TOD_MULT_MAX`(构建期拒绝超上界)");
            }

            // ── 条目 ────────────────────────────────────────────────────────
            var entries = new List<EventPoolEntryData>();
            if (!root.TryGet("entries", out JsonNode entriesNode))
            {
                errors.Add("52_event_pool.json 缺 entries");
            }
            else if (entriesNode.Kind != JsonNodeKind.Array)
            {
                errors.Add($"entries 须为数组(实得 {entriesNode.Kind})");
            }
            else
            {
                for (int i = 0; i < entriesNode.Items.Count; i++)
                {
                    EventPoolEntryData? parsed = ReadEntry(entriesNode.Items[i], i, errors);
                    if (parsed.HasValue) entries.Add(parsed.Value);
                }

                if (entries.Count == 0 && entriesNode.Items.Count > 0)
                    errors.Add("entries 全部不可解析 —— 见上方逐条错误");
                else if (entriesNode.Items.Count == 0)
                    errors.Add("entries 为空 —— P0 池不得为空(规则十一:四档内容不可缺席)");
            }

            // 重复 key ⇒ 拒收(游走序与「一个 key 一条」都是确定性判据)
            var seen = new HashSet<int>();
            for (int i = 0; i < entries.Count; i++)
                if (!seen.Add(entries[i].Key))
                    errors.Add($"entries 里 key {entries[i].Key} 重复 —— 一个 key 只能有一条");

            if (errors.Count > 0) throw new BakeValidationException(errors);

            // 规范化:按 key 升序(GDD `:687` 的游走序)
            entries.Sort((a, b) => a.Key.CompareTo(b.Key));

            return new BindResult(entries, nightNum, nightDen, maxNum, maxDen, schemaVersion);
        }

        // ── 逐条 ─────────────────────────────────────────────────────────

        private static EventPoolEntryData? ReadEntry(JsonNode node, int index, List<string> errors)
        {
            string at = $"entries[{index}]";
            if (node.Kind != JsonNodeKind.Object)
            {
                errors.Add($"{at} 须为对象(实得 {node.Kind})");
                return null;
            }

            int before = errors.Count;
            RejectUnknownKeys(node, EntryKeys, at, errors);

            if (!ReadInt(node, "key", at, errors, out int key)) return null;
            if (!ReadInt(node, "w_base", at, errors, out int wBase)) return null;

            if (!ReadEnum(node, "tier", TierNames, at, errors, out int tier)) return null;
            if (!ReadEnum(node, "spawn_anchor", AnchorNames, at, errors, out int anchor)) return null;
            if (!ReadEnum(node, "trigger_mode", TriggerNames, at, errors, out int trigger)) return null;
            if (!ReadEnum(node, "handler", HandlerNames, at, errors, out int handler)) return null;

            int causeFlagKey = 0;
            if (!node.TryGet("cause_flag", out JsonNode flagNode))
            {
                errors.Add($"{at} 缺 cause_flag(可为空串,但字段必须在 —— 缺字段会静默丢规则九的关联)");
                return null;
            }
            if (flagNode.Kind != JsonNodeKind.String)
            {
                errors.Add($"{at}.cause_flag 须为字符串(实得 {flagNode.Kind})");
                return null;
            }
            // 规则九:P0 无历史标记输入源 ⇒ cause_flag 只允许空串。
            // ⚠️ 非空 = 该条目声称受历史乘子影响,而 `HIST_W` 表在 P0 不存在 ⇒ 静默恒 1。
            //    与其静默,不如构建期拒收 —— 将来落 HIST_W 表时**同批**放宽本断言。
            if (!string.IsNullOrEmpty(flagNode.Str))
            {
                errors.Add($"{at}.cause_flag = \"{flagNode.Str}\" 非空 —— P0 无 `HIST_W` 表(规则九的" +
                           "输入源不存在)⇒ 该标记会静默恒乘 1。落 HIST_W 表时同批放宽本断言");
            }

            if (wBase < 0)
                errors.Add($"{at}.w_base = {wBase} 须 ≥ 0(W_base 是整数个数,负值无意义)");

            // 急召出诊标记(必填;类型须为 JSON 布尔 —— 整数 0/1 会让作者态不可读,
            // 与 `tier`/`handler` 用字符串枚举是同一条口径)。
            if (!node.TryGet("is_house_call", out JsonNode houseCallNode))
            {
                errors.Add($"{at} 缺 is_house_call(必填;`false` ≠ 「还没定」)");
                return null;
            }
            if (houseCallNode.Kind != JsonNodeKind.Boolean)
            {
                errors.Add($"{at}.is_house_call 须为 JSON 布尔(实得 {houseCallNode.Kind})");
                return null;
            }

            if (errors.Count != before) return null;

            return new EventPoolEntryData(key, wBase, tier, anchor, trigger,
                                          (PoolHandler)handler, causeFlagKey, houseCallNode.Bool);
        }

        private static bool ReadEnum(JsonNode node, string key, string[] names, string at,
                                     List<string> errors, out int value)
        {
            value = 0;
            if (!node.TryGet(key, out JsonNode n))
            {
                errors.Add($"{at} 缺 {key}");
                return false;
            }
            if (n.Kind != JsonNodeKind.String)
            {
                errors.Add($"{at}.{key} 须为字符串枚举(实得 {n.Kind};整数 id 会让作者态不可读)");
                return false;
            }
            for (int i = 0; i < names.Length; i++)
            {
                if (string.Equals(names[i], n.Str, StringComparison.Ordinal)) { value = i; return true; }
            }
            errors.Add($"{at}.{key} = \"{n.Str}\" 不在闭集 {{{string.Join(" / ", names)}}} 内");
            return false;
        }

        private static bool ReadInt(JsonNode node, string key, string at, List<string> errors, out int value)
        {
            value = 0;
            if (!node.TryGet(key, out JsonNode n))
            {
                errors.Add($"{at} 缺 {key}");
                return false;
            }
            if (n.Kind != JsonNodeKind.Integer)
            {
                errors.Add($"{at}.{key} 须为 JSON 整数(实得 {n.Kind}={n.RawText})");
                return false;
            }
            if (n.Int < int.MinValue || n.Int > int.MaxValue)
            {
                errors.Add($"{at}.{key} = {n.Int} 超出 int 域");
                return false;
            }
            value = (int)n.Int;
            return true;
        }

        /// <summary>读 <c>{"num":N,"den":D}</c>(ADR-006 禁浮点字面量,乘子一律写分子/分母)。</summary>
        private static bool ReadFraction(JsonNode obj, string key, List<string> errors,
                                         out int num, out int den)
        {
            num = 0; den = 1;
            if (!obj.TryGet(key, out JsonNode n))
            {
                errors.Add($"52_event_pool.json 缺 {key}");
                return false;
            }
            if (n.Kind != JsonNodeKind.Object)
            {
                errors.Add($"{key} 须写成 {{\"num\":…, \"den\":…}}(实得 {n.Kind})—— " +
                           "ADR-006 禁浮点字面量,乘子一律写分子/分母");
                return false;
            }

            int propCount = 0;
            foreach (KeyValuePair<string, JsonNode> kv in n.Props)
            {
                propCount++;
                if (!string.Equals(kv.Key, "num", StringComparison.Ordinal)
                    && !string.Equals(kv.Key, "den", StringComparison.Ordinal))
                {
                    errors.Add($"{key} 的未知键「{kv.Key}」(只允许 num / den)");
                    return false;
                }
            }
            if (propCount != 2)
            {
                errors.Add($"{key} 须**恰含** num 与 den 两键(实得 {propCount} 个)");
                return false;
            }

            if (!ReadInt(n, "num", key, errors, out num)) return false;
            if (!ReadInt(n, "den", key, errors, out den)) return false;
            return true;
        }

        private static uint ReadSchemaVersion(JsonNode obj, List<string> errors)
        {
            if (!obj.TryGet("schema_version", out JsonNode n))
            {
                errors.Add("52_event_pool.json 缺 schema_version");
                return 0;
            }
            if (n.Kind != JsonNodeKind.Integer || n.Int < 0 || n.Int > uint.MaxValue)
            {
                errors.Add($"schema_version 须为 u32 域整数(实得 {n.Kind}={n.RawText})");
                return 0;
            }
            return (uint)n.Int;
        }

        private static void RejectUnknownKeys(JsonNode obj, string[] allowed, string where, List<string> errors)
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
