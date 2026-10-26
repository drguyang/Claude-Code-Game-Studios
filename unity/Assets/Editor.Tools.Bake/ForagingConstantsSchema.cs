// S20-3 · story-001 —— `foraging_constants.json` 的**键白名单与读取辅助**(阶段 2 绑定器的词表半边)。
//
// 权威来源:
//   ADR-014 §三(阶段 2:已知键白名单 · FixParse · schema_version · 聚合 throw)· §四(Fix 字段必须字符串)
//   GDD design/gdd/foraging.md §Tuning Knobs `:541-548`(全部数值 2026-10-06 已裁)
//   story production/epics/foraging/story-001-variety-closure-and-data-load-validation.md
//     AC-17-08b(非法值硬失败)· AC-17-14(CapTable 四性质)· AC-17-16(SEASON_MULT 表长)
//
// ⚠️ **白名单是「本表能写哪些键」的唯一出处**。新增旋钮 = 改本文件的数组 + Binder 的读取行
//   + Sim 侧 `ForagingConstants` 的字段,**三处同时改**(少一处 = 静默丢字段)。
//
// ⚠️ 键名一律 `snake_case`(承既有 `assets/data/*.json` 先例:clinicmachine_context.json /
//   item_database_items.json 均 snake_case;`.claude/rules/data-files.md` 的「camelCase」一条
//   在本仓已被既有先例覆盖,以先例为准)。
//
// ⚠️ `quality_distribution` **刻意不在本白名单里** —— 它住 21a 的 `gather_profile`
//   (GDD 六处一致;2026-10-26 用户裁定「按 GDD:住 21a」)。写进本表 = 造第二真源 ⇒ 拒收。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>`foraging_constants.json` 的键白名单 + 读取辅助(单一出处)。</summary>
    public static class ForagingConstantsSchema
    {
        /// <summary>源文件名(单一出处;烘焙装载器 / 菜单 / 测试共用)。</summary>
        public const string FileName = "foraging_constants.json";

        /// <summary>顶层键白名单(全部必填 —— 本表无「可选旋钮」,缺任一即 F-17 求值缺输入)。</summary>
        internal static readonly string[] RootKeys =
        {
            "schema_version", "_note",
            "seasons_per_year",
            "cap_table",
            "season_mult",
            "regrow_window_ticks",
            "base_capacity",
            "decay_rate",
            "decay_floor",
            "gather_mul_cap",
        };

        /// <summary>除 <c>schema_version</c> / <c>_note</c> 外全部必填。</summary>
        internal static readonly string[] RequiredKeys =
        {
            "seasons_per_year",
            "cap_table",
            "season_mult",
            "regrow_window_ticks",
            "base_capacity",
            "decay_rate",
            "decay_floor",
            "gather_mul_cap",
        };

        /// <summary>`CapTable` 的规定长度 = `SKILL_CAP + 1`(GDD `:300`)。</summary>
        public const int CapTableLength = 61;

        /// <summary>读取必填 JSON 整数(int 计数,非 Fix —— 承 D-21-17 口径)。</summary>
        internal static bool ReadRequiredInt(
            JsonNode obj, string key, string fileName, List<string> errors, out int value)
        {
            value = 0;
            if (!obj.TryGet(key, out JsonNode n))
            {
                // 缺失已由 Binder 的 RequiredKeys 循环一次性登记(承 InputAxisTuningBinder.BindRequiredFix
                // `:93` 同款口径)⇒ 此处**静默**返回 false,避免同一缺键报两遍(评审 M-4)。
                // ⚠️ 调用方**必须**先跑 RequiredKeys 检查,否则缺失会静默丢失。
                return false;
            }
            if (n.Kind != JsonNodeKind.Integer)
            {
                errors.Add(
                    $"{fileName}:{key} 须为 JSON 整数 token(实得 {n.Kind}={n.RawText}) —— " +
                    "计数字段非 Fix(D-21-17:weight / 计数移出 Fix 解析集)");
                return false;
            }
            if (n.Int < int.MinValue || n.Int > int.MaxValue)
            {
                errors.Add($"{fileName}:{key} = {n.Int} 越出 int 域");
                return false;
            }
            value = (int)n.Int;
            return true;
        }

        /// <summary>读取必填 JSON 整数(long 计数,用于 tick 数)。</summary>
        internal static bool ReadRequiredLong(
            JsonNode obj, string key, string fileName, List<string> errors, out long value)
        {
            value = 0;
            if (!obj.TryGet(key, out JsonNode n))
            {
                // 缺失已由 Binder 的 RequiredKeys 循环一次性登记(承 InputAxisTuningBinder.BindRequiredFix
                // `:93` 同款口径)⇒ 此处**静默**返回 false,避免同一缺键报两遍(评审 M-4)。
                // ⚠️ 调用方**必须**先跑 RequiredKeys 检查,否则缺失会静默丢失。
                return false;
            }
            if (n.Kind != JsonNodeKind.Integer)
            {
                errors.Add(
                    $"{fileName}:{key} 须为 JSON 整数 token(实得 {n.Kind}={n.RawText}) —— " +
                    "tick 计数非 Fix(D-21-17)");
                return false;
            }
            value = n.Int;
            return true;
        }

        /// <summary>读取必填 Fix(**只接受 JSON 字符串**;数值 token 结构性硬拒,ADR-014 §四)。</summary>
        internal static bool ReadRequiredFix(
            JsonNode obj, string key, string fileName, List<string> errors, out Fix value)
        {
            value = Fix.Zero;
            if (!obj.TryGet(key, out JsonNode n))
            {
                // 缺失已由 Binder 的 RequiredKeys 循环一次性登记(承 InputAxisTuningBinder.BindRequiredFix
                // `:93` 同款口径)⇒ 此处**静默**返回 false,避免同一缺键报两遍(评审 M-4)。
                // ⚠️ 调用方**必须**先跑 RequiredKeys 检查,否则缺失会静默丢失。
                return false;
            }
            if (n.Kind != JsonNodeKind.String)
            {
                errors.Add(
                    $"{fileName}:{key} 须为 JSON 字符串 Fix 字面量(实得 {n.Kind}={n.RawText}) —— " +
                    "数值 token 会被词法器转成 CLR 基元,浮点即已在内存(ADR-014 §四 / ADR-006)");
                return false;
            }
            try
            {
                value = FixParse.Parse(n.Str);
                return true;
            }
            catch (FormatException ex)
            {
                errors.Add($"{fileName}:{key}=\"{n.Str}\" FixParse 失败:{ex.Message}");
                return false;
            }
            catch (DivideByZeroException ex)
            {
                errors.Add($"{fileName}:{key}=\"{n.Str}\" FixParse 失败:{ex.Message}");
                return false;
            }
        }

        /// <summary>读取必填的 int 数组(<c>cap_table</c>;元素须全为 JSON 整数)。</summary>
        internal static bool ReadRequiredIntArray(
            JsonNode obj, string key, string fileName, List<string> errors, out int[] values)
        {
            values = null;
            if (!obj.TryGet(key, out JsonNode n))
            {
                // 缺失已由 Binder 的 RequiredKeys 循环一次性登记(承 InputAxisTuningBinder.BindRequiredFix
                // `:93` 同款口径)⇒ 此处**静默**返回 false,避免同一缺键报两遍(评审 M-4)。
                // ⚠️ 调用方**必须**先跑 RequiredKeys 检查,否则缺失会静默丢失。
                return false;
            }
            if (n.Kind != JsonNodeKind.Array)
            {
                errors.Add($"{fileName}:{key} 须为 JSON 数组(实得 {n.Kind})");
                return false;
            }
            var buf = new int[n.Items.Count];
            for (int i = 0; i < n.Items.Count; i++)
            {
                JsonNode el = n.Items[i];
                if (el.Kind != JsonNodeKind.Integer)
                {
                    errors.Add(
                        $"{fileName}:{key}[{i}] 须为 JSON 整数(实得 {el.Kind}={el.RawText}) —— " +
                        "CapTable 是 int 档位表,非 Fix(D-21-17)");
                    return false;
                }
                if (el.Int < int.MinValue || el.Int > int.MaxValue)
                {
                    errors.Add($"{fileName}:{key}[{i}] = {el.Int} 越出 int 域");
                    return false;
                }
                buf[i] = (int)el.Int;
            }
            values = buf;
            return true;
        }

        /// <summary>读取必填的 Fix 数组(<c>season_mult</c>;元素须全为 JSON 字符串)。</summary>
        internal static bool ReadRequiredFixArray(
            JsonNode obj, string key, string fileName, List<string> errors, out Fix[] values)
        {
            values = null;
            if (!obj.TryGet(key, out JsonNode n))
            {
                // 缺失已由 Binder 的 RequiredKeys 循环一次性登记(承 InputAxisTuningBinder.BindRequiredFix
                // `:93` 同款口径)⇒ 此处**静默**返回 false,避免同一缺键报两遍(评审 M-4)。
                // ⚠️ 调用方**必须**先跑 RequiredKeys 检查,否则缺失会静默丢失。
                return false;
            }
            if (n.Kind != JsonNodeKind.Array)
            {
                errors.Add($"{fileName}:{key} 须为 JSON 数组(实得 {n.Kind})");
                return false;
            }
            var buf = new Fix[n.Items.Count];
            for (int i = 0; i < n.Items.Count; i++)
            {
                JsonNode el = n.Items[i];
                if (el.Kind != JsonNodeKind.String)
                {
                    errors.Add(
                        $"{fileName}:{key}[{i}] 须为 JSON 字符串 Fix 字面量(实得 {el.Kind}={el.RawText}) —— " +
                        "ADR-014 §四");
                    return false;
                }
                try
                {
                    buf[i] = FixParse.Parse(el.Str);
                }
                catch (Exception ex) when (ex is FormatException || ex is DivideByZeroException)
                {
                    errors.Add($"{fileName}:{key}[{i}]=\"{el.Str}\" FixParse 失败:{ex.Message}");
                    return false;
                }
            }
            values = buf;
            return true;
        }

        /// <summary>拒收白名单外的键(与 <c>ClinicMachineSchema.RejectUnknownKeys</c> 同口径)。</summary>
        internal static void RejectUnknownKeys(
            JsonNode obj, string[] allowed, string where, string fileName, List<string> errors)
        {
            var allowedSet = new HashSet<string>(allowed, StringComparer.Ordinal);
            foreach (string key in obj.Keys)
            {
                if (!allowedSet.Contains(key))
                    errors.Add(
                        $"{fileName}:{where} 未登记键 \"{key}\" —— ADR-014 §三 白名单外,烘焙期硬失败");
            }
        }
    }
}
