// 权威来源:ADR-014 §五「D-21-13 收窄的承接」承接条 ③(ordinal 映射表 append-only 硬门)
//          · assets/data/_ordinal_baseline.json(格式见该文件 `_note`)
//
// ⚠️ **为什么解析住 Bake 而不住 Gates**:本文件吃 <c>JsonNode</c>(阶段1 词法的产物),
//    而 `JsonNode` / `JsonStage1Lexer` 是 `Editor.Tools.Bake` 的 internal。反向依赖
//    (Gates → Bake)不存在、**也不该建**(依赖方向恒为 Bake → Gates,见两装配的 asmdef)。
//    ⇒ 分工:**形状解析 + 读文件住本文件(有 I/O)**,「比对」住
//    `Editor.Tools.Gates.OrdinalBaselineGate`(纯函数,可判伪)。
//
// ⚠️ **基线文件是判据、不是数据** ⇒ 不进 ConfigVersion 覆盖集(理由见该文件 `_note`)。
//    覆盖集登记属 ADR-014 承接条 ①,与承接条 ③ 分列。

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DaYiJingCheng.EditorTools.Gates;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>一个注册表在基线里的条目集(已过形状校验)。</summary>
    public sealed class OrdinalBaselineRegistry
    {
        /// <summary>注册表标识(如 <c>item_database</c>)。</summary>
        public readonly string Id;

        /// <summary>作者态源文件路径(仓库相对;仅报文与人工核对用,本类不读它)。</summary>
        public readonly string Source;

        /// <summary>承载 ordinals 的字段名(如 <c>ordinal</c>;仅报文用)。</summary>
        public readonly string Field;

        /// <summary>拼键所用的字段名(如 <c>base_id</c> / <c>processing_state</c>;仅报文用)。</summary>
        public readonly IReadOnlyList<string> KeyFields;

        /// <summary>冻结条目。</summary>
        public readonly IReadOnlyList<OrdinalBaselineGate.Entry> Entries;

        /// <summary>构造。</summary>
        public OrdinalBaselineRegistry(
            string id, string source, string field,
            IReadOnlyList<string> keyFields, IReadOnlyList<OrdinalBaselineGate.Entry> entries)
        {
            Id = id; Source = source; Field = field; KeyFields = keyFields; Entries = entries;
        }
    }

    /// <summary><c>_ordinal_baseline.json</c> 的读入 + 形状校验(纯函数式接口,但**有 I/O 入口**)。
    /// <para>错误进列表由调用方聚合 throw —— 与本仓门/绑定层同一纪律(不首个错即停)。</para>
    /// </summary>
    /// <example>
    /// <code>
    /// var regs = OrdinalBaselineReader.ReadFromRepo(repoRoot, errors);
    /// if (errors.Count &gt; 0) throw new BakeValidationException(errors);
    /// </code>
    /// </example>
    public static class OrdinalBaselineReader
    {
        /// <summary>基线文件名(位于 <c>assets/data/</c>)。</summary>
        public const string FileName = "_ordinal_baseline.json";

        /// <summary>key_fields 各段之间的连接符(ASCII Unit Separator)。
        /// <para>取它是因为**不可能**出现在任何 ID 里(ID 均为 snake_case)⇒ 拼接无歧义。
        /// 改动它 = 让既有基线的键全部对不上 ⇒ 构建全红,故与本文件同批冻结。</para></summary>
        public const char KeySeparator = '\u001F';

        private static readonly string[] TopKeys = { "schema_version", "_note", "registries" };
        private static readonly string[] RegistryKeys =
            { "id", "source", "field", "key_fields", "note", "entries" };

        /// <summary>从仓库读并解析。</summary>
        /// <param name="repoRoot">仓库根(含 <c>assets/</c> 的目录)。</param>
        /// <param name="errors">错误聚合。</param>
        /// <returns>已解析的注册表(有错时可能不完整;调用方先看 errors)。</returns>
        public static List<OrdinalBaselineRegistry> ReadFromRepo(string repoRoot, List<string> errors)
        {
            if (repoRoot == null) throw new ArgumentNullException(nameof(repoRoot));
            if (errors == null) throw new ArgumentNullException(nameof(errors));

            string path = Path.Combine(repoRoot, "assets", "data", FileName);
            if (!File.Exists(path))
            {
                // 缺失 = 硬失败(不是「跳过门」)—— ADR-014 承接条③ 要求这张表存在;
                // 「文件不在 ⇒ 门静默不跑」正是本条要消灭的形态。
                errors.Add($"{FileName}:文件不存在({path}) —— ADR-014 §五 承接条③ 要求该基线存在。" +
                           "缺它 ⇒ append-only 门无从比对,任何改号都会静默通过。");
                return new List<OrdinalBaselineRegistry>();
            }

            string json = File.ReadAllText(path, Encoding.UTF8);
            return Parse(json, errors);
        }

        /// <summary>解析基线源文本(测试可直接注入)。</summary>
        /// <param name="json">基线 JSON 全文。</param>
        /// <param name="errors">错误聚合。</param>
        /// <returns>已解析的注册表。</returns>
        public static List<OrdinalBaselineRegistry> Parse(string json, List<string> errors)
        {
            if (errors == null) throw new ArgumentNullException(nameof(errors));
            var result = new List<OrdinalBaselineRegistry>();

            if (!JsonStage1Lexer.TryParse(json, out JsonNode root, out string lexErr))
            {
                errors.Add($"{FileName} 阶段1 词法失败:{lexErr}");
                return result;
            }
            if (root.Kind != JsonNodeKind.Object)
            {
                errors.Add($"{FileName} 根须为对象(实得 {root.Kind})");
                return result;
            }

            RejectUnknown(root, TopKeys, FileName, errors);

            if (!root.TryGet("registries", out JsonNode regs) || regs.Kind != JsonNodeKind.Array)
            {
                errors.Add($"{FileName}:缺失必填键 \"registries\" 或其非数组");
                return result;
            }

            for (int r = 0; r < regs.Items.Count; r++)
            {
                JsonNode rr = regs.Items[r];
                string label = $"{FileName}:registries[{r}]";
                if (rr.Kind != JsonNodeKind.Object) { errors.Add($"{label} 非对象"); continue; }

                RejectUnknown(rr, RegistryKeys, label, errors);

                string id = ReadString(rr, "id", label, errors);
                string source = ReadString(rr, "source", label, errors);
                string field = ReadString(rr, "field", label, errors);

                var keyFields = new List<string>();
                if (!rr.TryGet("key_fields", out JsonNode kf) || kf.Kind != JsonNodeKind.Array)
                {
                    errors.Add($"{label}:缺失必填键 \"key_fields\" 或其非数组");
                }
                else
                {
                    for (int k = 0; k < kf.Items.Count; k++)
                    {
                        if (kf.Items[k].Kind != JsonNodeKind.String)
                        {
                            errors.Add($"{label}.key_fields[{k}] 非字符串");
                            continue;
                        }
                        keyFields.Add(kf.Items[k].Str);
                    }
                    if (keyFields.Count == 0)
                        errors.Add($"{label}.key_fields 为空 —— 拼不出键就无法比对");
                }

                var entries = new List<OrdinalBaselineGate.Entry>();
                if (!rr.TryGet("entries", out JsonNode ens) || ens.Kind != JsonNodeKind.Array)
                    errors.Add($"{label}:缺失必填键 \"entries\" 或其非数组");
                else
                    ReadEntries(ens, keyFields, label, errors, entries);

                result.Add(new OrdinalBaselineRegistry(
                    id ?? string.Empty, source ?? string.Empty, field ?? string.Empty, keyFields, entries));
            }

            // 注册表 id 唯一(调用方按 id 取基线,重复即歧义)
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < result.Count; i++)
                if (!seen.Add(result[i].Id))
                    errors.Add($"{FileName}:注册表 id \"{result[i].Id}\" 重复 —— 按 id 取基线会歧义");

            return result;
        }

        /// <summary>按 id 找注册表;找不到返回 null(调用方须报错,不得静默跳过)。</summary>
        /// <param name="registries">已解析列表。</param>
        /// <param name="id">目标 id。</param>
        public static OrdinalBaselineRegistry Find(
            IReadOnlyList<OrdinalBaselineRegistry> registries, string id)
        {
            if (registries == null) return null;
            for (int i = 0; i < registries.Count; i++)
                if (string.Equals(registries[i].Id, id, StringComparison.Ordinal))
                    return registries[i];
            return null;
        }

        /// <summary>按 <c>key_fields</c> 顺序把各段拼成可比较键。</summary>
        /// <param name="parts">各段(顺序 = key_fields 顺序)。</param>
        public static string JoinKey(IReadOnlyList<string> parts)
        {
            if (parts == null) throw new ArgumentNullException(nameof(parts));
            if (parts.Count == 0) return string.Empty;
            if (parts.Count == 1) return parts[0];
            var sb = new StringBuilder();
            for (int i = 0; i < parts.Count; i++)
            {
                if (i > 0) sb.Append(KeySeparator);
                sb.Append(parts[i]);
            }
            return sb.ToString();
        }

        // ══════════ 内部 ══════════

        private static void ReadEntries(
            JsonNode ens, IReadOnlyList<string> keyFields, string label,
            List<string> errors, List<OrdinalBaselineGate.Entry> entries)
        {
            for (int e = 0; e < ens.Items.Count; e++)
            {
                JsonNode er = ens.Items[e];
                string elabel = $"{label}.entries[{e}]";
                if (er.Kind != JsonNodeKind.Object) { errors.Add($"{elabel} 非对象"); continue; }

                // 条目的键集 = {ordinal} ∪ key_fields,别无其他(拼写错 = 静默拼出错误键)
                foreach (string key in er.Keys)
                {
                    if (string.Equals(key, "ordinal", StringComparison.Ordinal)) continue;
                    bool inKeyFields = false;
                    for (int k = 0; k < keyFields.Count; k++)
                        if (string.Equals(keyFields[k], key, StringComparison.Ordinal)) { inKeyFields = true; break; }
                    if (!inKeyFields)
                        errors.Add($"{elabel}:未知键 \"{key}\" —— 只容 \"ordinal\" 与 key_fields 内的字段");
                }

                if (!er.TryGet("ordinal", out JsonNode on) || on.Kind != JsonNodeKind.Integer)
                {
                    errors.Add($"{elabel}:缺失 \"ordinal\" 或非 JSON 整数 token");
                    continue;
                }
                if (on.Int < int.MinValue || on.Int > int.MaxValue)
                {
                    errors.Add($"{elabel}:ordinal = {on.RawText} 超出 int 域(载荷内 item_key 是 int 位宽)");
                    continue;
                }

                var parts = new List<string>(keyFields.Count);
                bool ok = true;
                for (int k = 0; k < keyFields.Count; k++)
                {
                    if (!er.TryGet(keyFields[k], out JsonNode kv) || kv.Kind != JsonNodeKind.String)
                    {
                        errors.Add($"{elabel}:缺失 key_fields 内的 \"{keyFields[k]}\" 或非字符串");
                        ok = false;
                        break;
                    }
                    parts.Add(kv.Str);
                }
                if (!ok) continue;

                entries.Add(new OrdinalBaselineGate.Entry((int)on.Int, JoinKey(parts)));
            }
        }

        private static void RejectUnknown(JsonNode obj, string[] allowed, string label, List<string> errors)
        {
            foreach (string key in obj.Keys)
                if (Array.IndexOf(allowed, key) < 0)
                    errors.Add($"{label}:未知键 \"{key}\" —— ADR-014 §三 白名单外,硬失败");
        }

        private static string ReadString(JsonNode obj, string key, string label, List<string> errors)
        {
            if (!obj.TryGet(key, out JsonNode n) || n.Kind != JsonNodeKind.String)
            {
                errors.Add($"{label}:缺失必填键 \"{key}\" 或非字符串");
                return null;
            }
            return n.Str;
        }
    }
}
