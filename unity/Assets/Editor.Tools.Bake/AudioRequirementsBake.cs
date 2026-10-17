// S14-8 · story-016 —— R-2 归属需求表的**仓根装载器 + 绑定层 + 权威件对账**(C 与 B 两支)。
//
// 权威来源:design/assets/specs/audio-material-manifest.md §一 / §一之二(清单出处,= B)
//          · r2-asset-ownership-ruling-2026-10-03.md(归属真源)
//          · AudioEventTableGates.RequiredRequirements(**代码侧 A 锁** —— 本件不定义清单值)
//
// ⚠️ **三处结合的口径(2026-10-17 用户裁定「红,三处都要改」)**:
//   ① A = `AudioEventTableGates.RequiredRequirements` 常量(**锁**);
//   ② C = `assets/data/audio_requirements.json`(**值**,本件绑定它);
//   ③ B = `audio-material-manifest.md` 的冻结表(**出处**,本件 `ValidateManifestAgrees` 对账)。
//   三者任一不合 ⇒ 构建期 `throw`。数据件改了而常量没改 ⇒ A 锁差集红;
//   数据件与常量同步改了而文档没改 ⇒ B 对账红。**不存在只改两处的绿**。
//
// ⚠️ 本件刻意**不碰 UnityEngine / Addressables**(承 DelayTable53Bake / AudioSettingsRegistryBake 同纪律)
//   ⇒ EditMode 测试可直接驱动。本件**不落盘**;它不产出 cooked(见下方 NOT-RUN 登记)。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DaYiJingCheng.EditorTools.Gates;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>R-2 归属需求表的装载 / 绑定 / 权威件对账(Story 016)。</summary>
    public static class AudioRequirementsBake
    {
        /// <summary>作者态源文件名(单一出处)。</summary>
        public const string SourceFileName = "audio_requirements.json";

        /// <summary>源文件所在目录(相对仓根)。</summary>
        public const string DataDirName = "assets/data";

        /// <summary>权威件路径(相对仓根)。</summary>
        public const string ManifestRelative = "design/assets/specs/audio-material-manifest.md";

        /// <summary>顶层已知键白名单(ADR-014 §三:未知键 = 硬失败)。</summary>
        public static readonly IReadOnlyList<string> TopLevelKeys = new[]
        {
            "schema_version", "_note", "requirements", "world_cue_split",
        };

        /// <summary>需求行已知键白名单。</summary>
        public static readonly IReadOnlyList<string> RowKeys = new[] { "id", "name", "cue", "files" };

        /// <summary>当前 schema 版本(整数;与其它数据件同口径 —— 解析后比数值,不比起始字符串)。</summary>
        public const string CurrentSchemaVersion = "1";

        /// <summary>从仓根绑定(读源 → 阶段 1 词法 → 阶段 2 绑定 + 未知键 + schema)**不跑门**。</summary>
        /// <exception cref="BakeValidationException">源缺失 / 词法 / 未知键 / schema(聚合)。</exception>
        public static AudioRequirementsData BindFromRepo(string repoRoot)
            => Bind(ReadSource(repoRoot));

        /// <summary>从 JSON 串绑定(负向夹具入口;与 <see cref="BindFromRepo"/> 同一路径)。</summary>
        /// <exception cref="BakeValidationException">同 <see cref="BindFromRepo"/>。</exception>
        public static AudioRequirementsData BindFromJson(string json) => Bind(json);

        /// <summary>门本体:E1 / E4 —— A 锁 + 事件表覆盖 + 素材库覆盖。</summary>
        /// <param name="data">已绑定的数据件。</param>
        /// <param name="rows">事件表 cue 行。</param>
        /// <param name="assetRoot">素材根(绝对路径;见 <see cref="AudioAssetExistence.ResolveRoot"/>)。</param>
        public static IReadOnlyList<string> ValidateAtAssetRoot(AudioRequirementsData data,
            IReadOnlyList<AudioEventTableRow> rows, string assetRoot)
            => AudioEventTableGates.ValidateRequirements(data, rows, ListWavFiles(assetRoot));

        /// <summary>B 支:**权威件(Manifest)↔ 代码常量** 逐项对账 —— 清单改了文档没改 = 硬失败。</summary>
        /// <remarks>
        /// <para>⚠️ <b>这是 B 的判据,不是 A 的替代</b> —— A 锁比对的是「数据件 ↔ 常量」;
        /// 本方法比对「文档 ↔ 常量」。三处结合,任何一处漏改都红。</para>
        /// <para>⚠️ <b>解析面刻意收窄</b>:只读 <c>design/assets/specs/audio-material-manifest.md</c> 中
        /// 「## ✅ 冻结命名与 bus 裁定」起、至下一个二级标题前的那段,取其中含 <c>冻结) |</c> 表格的
        /// 反引号值。Markdown 重排(改列序 / 挪表)会让本方法**报缺**而非静默通过 ——
        /// 报缺是安全的失败方向。</para>
        /// </remarks>
        /// <param name="repoRoot">仓库根。</param>
        public static IReadOnlyList<string> ValidateManifestAgrees(string repoRoot)
        {
            if (repoRoot == null) throw new ArgumentNullException(nameof(repoRoot));
            string path = Path.Combine(repoRoot, ManifestRelative);
            if (!File.Exists(path))
                return new[] { $"[E1 · B 出处] 权威件不存在:{ManifestRelative}" };
            return ValidateManifestText(File.ReadAllText(path));
        }

        /// <summary>B 支的核心纯函数(夹具直驱,免落盘)。</summary>
        /// <param name="manifestText">权威件全文。</param>
        public static IReadOnlyList<string> ValidateManifestText(string manifestText)
        {
            if (manifestText == null)
                return new[] { "[NOT-RUN 守卫 · B 出处] 权威件文本为 null" };

            var errors = new List<string>();
            int section = manifestText.IndexOf("## ✅ 冻结命名与 bus 裁定", StringComparison.Ordinal);
            if (section < 0)
                return new[] { "[E1 · B 出处] 权威件内找不到「## ✅ 冻结命名与 bus 裁定」节 —— 结构被改?" };
            int next = manifestText.IndexOf("\n## ", section + 1, StringComparison.Ordinal);
            string body = next < 0 ? manifestText.Substring(section) : manifestText.Substring(section, next - section);

            var docCues = new HashSet<string>(StringComparer.Ordinal);
            var docFiles = new HashSet<string>(StringComparer.Ordinal);
            foreach (string line in body.Split('\n'))
            {
                bool isTableRow = line.TrimStart().StartsWith("|", StringComparison.Ordinal);
                // cue 只从**表格行**取(散文里的标识符可能不是 cue,如 `AddressableAssetSettings`);
                // 文件从**全文**取 —— 权威件把 `sfx_breath_*` 写在散文里(§三 :181),不是表格行。
                foreach (Match m in Regex.Matches(line, "`([^`]+)`"))
                {
                    string v = m.Groups[1].Value.Trim();
                    // ⚠️ 只有**看起来像文件名**的反引号值才进文件集 —— 散文里的复合写法
                    //    (「现 `vo_..._m1.wav` / `_f1.wav` 退役」)会给劈出 `_f1.wav` / `.wav` 这类碎片,
                    //    首版因此报了 2 条幽灵差异。判据 = 以字母开头的 `*.wav`。
                    if (Regex.IsMatch(v, "^[A-Za-z][A-Za-z0-9_{},.]*\\.wav$", RegexOptions.IgnoreCase))
                    {
                        foreach (string f in ExpandBraces(v)) docFiles.Add(f);
                    }
                    else if (isTableRow && Regex.IsMatch(v, "^[A-Za-z][A-Za-z0-9_]*$"))
                    {
                        docCues.Add(v);
                    }
                }
            }

            var codeCues = new HashSet<string>(
                AudioEventTableGates.RequiredRequirements.Select(r => r.Cue), StringComparer.Ordinal);
            codeCues.UnionWith(AudioEventTableGates.WorldSoundCues);
            var codeFiles = new HashSet<string>(
                AudioEventTableGates.RequiredRequirements.SelectMany(r => r.Files), StringComparer.Ordinal);

            // cue 侧只报「常量有、文档无」:文档可额外登记尚未落地的 cue(如草案表内的 14 行),不算错。
            foreach (string c in codeCues.Except(docCues).OrderBy(x => x, StringComparer.Ordinal))
                errors.Add($"[E1 · B 出处] 常量里的 cue「{c}」在权威件冻结表内找不到 —— 文档滞后于代码?");
            // 文件侧**双向**报 —— 文件是 E4 的比对量纲,多一个少一个都改变归属面。
            foreach (string f in codeFiles.Except(docFiles).OrderBy(x => x, StringComparer.Ordinal))
                errors.Add($"[E1 · B 出处] 常量文件「{f}」在权威件冻结表内找不到");
            foreach (string f in docFiles.Except(codeFiles).OrderBy(x => x, StringComparer.Ordinal))
                errors.Add($"[E1 · B 出处] 权威件文件「{f}」不在常量内 —— 归属变更须同步三处");
            return errors;
        }

        /// <summary>展开 <c>{a,b}</c> 花括号写法(单层;文档只用到单层)。</summary>
        /// <param name="pattern">如 <c>sfx_footstep_{stone,dirt}_01.wav</c>。</param>
        public static IReadOnlyList<string> ExpandBraces(string pattern)
        {
            if (string.IsNullOrEmpty(pattern)) return Array.Empty<string>();
            Match m = Regex.Match(pattern, "\\{([^{}]*)\\}");
            if (!m.Success) return new[] { pattern };
            var outp = new List<string>();
            foreach (string alt in m.Groups[1].Value.Split(','))
                outp.AddRange(ExpandBraces(pattern.Substring(0, m.Index) + alt + pattern.Substring(m.Index + m.Length)));
            return outp;
        }

        /// <summary>列出素材根下全部 <c>.wav</c>(裸名,序数排序;扩展名大小写不敏感,承 AC-44-D5b)。</summary>
        /// <param name="assetRoot">素材根(绝对路径)。</param>
        public static IReadOnlyList<string> ListWavFiles(string assetRoot)
        {
            if (string.IsNullOrEmpty(assetRoot) || !Directory.Exists(assetRoot))
                return null;   // NOT-RUN 守卫由门侧报
            var names = new List<string>();
            foreach (string p in Directory.GetFiles(assetRoot))
            {
                if (string.Equals(Path.GetExtension(p), ".wav", StringComparison.OrdinalIgnoreCase))
                    names.Add(Path.GetFileName(p));
            }
            names.Sort(StringComparer.Ordinal);
            return names;
        }

        // ────────────────────────── 内部 ──────────────────────────

        private static string ReadSource(string repoRoot)
        {
            if (repoRoot == null) throw new ArgumentNullException(nameof(repoRoot));
            string path = Path.Combine(repoRoot, DataDirName, SourceFileName);
            if (!File.Exists(path))
                throw new BakeValidationException(new List<string> { $"源文件不存在:{path}" });
            return File.ReadAllText(path);
        }

        private static AudioRequirementsData Bind(string json)
        {
            if (!JsonStage1Lexer.TryParse(json, out JsonNode root, out string lexErr))
                throw new BakeValidationException(new List<string> { $"[词法] {lexErr}" });
            if (root == null || root.Kind != JsonNodeKind.Object)
                throw new BakeValidationException(new List<string> { "[绑定] 顶层不是对象" });

            var errors = new List<string>();
            var data = new AudioRequirementsData { PresentKeys = new List<string>(root.Keys) };

            foreach (string k in root.Keys)
                if (!TopLevelKeys.Contains(k))
                    errors.Add($"[未知键] 顶层键「{k}」不在白名单 {{{string.Join(", ", TopLevelKeys)}}}");

            data.SchemaVersionRaw = RawOf(root, "schema_version");
            if (!string.Equals(data.SchemaVersionRaw?.Trim(), CurrentSchemaVersion, StringComparison.Ordinal))
                errors.Add($"[schema] schema_version = 「{data.SchemaVersionRaw ?? "<缺失>"}」,期望「{CurrentSchemaVersion}」");

            if (root.TryGet("requirements", out JsonNode reqNode) && reqNode != null &&
                reqNode.Kind == JsonNodeKind.Array && reqNode.Items != null)
            {
                var rows = new List<AudioRequirementRow>();
                for (int i = 0; i < reqNode.Items.Count; i++)
                {
                    JsonNode obj = reqNode.Items[i];
                    var row = new AudioRequirementRow();
                    if (obj == null || obj.Kind != JsonNodeKind.Object)
                    {
                        errors.Add($"[绑定] requirements[{i}] 不是对象");
                        rows.Add(row);
                        continue;
                    }
                    foreach (string k in obj.Keys)
                        if (!RowKeys.Contains(k))
                            errors.Add($"[未知键] requirements[{i}] 键「{k}」不在白名单");
                    row.Id = RawOf(obj, "id");
                    row.Name = RawOf(obj, "name");
                    row.Cue = RawOf(obj, "cue");
                    row.Files = StringsOf(obj, "files", i, errors);
                    rows.Add(row);
                }
                data.Requirements = rows;
            }
            else
            {
                errors.Add("[绑定] requirements 字段缺失或非数组");
            }

            if (root.TryGet("world_cue_split", out JsonNode wsNode) && wsNode != null &&
                wsNode.Kind == JsonNodeKind.Object && wsNode.Props != null)
            {
                var split = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
                for (int i = 0; i < wsNode.Props.Count; i++)
                {
                    string key = wsNode.Props[i].Key;
                    if (key.StartsWith("_", StringComparison.Ordinal)) continue;   // 注记键(非档位)
                    JsonNode v = wsNode.Props[i].Value;
                    if (v != null && v.Kind == JsonNodeKind.Array && v.Items != null)
                    {
                        var fs = new List<string>(v.Items.Count);
                        foreach (JsonNode it in v.Items) if (it != null && it.Str != null) fs.Add(it.Str);
                        split[key] = fs;
                    }
                    else
                    {
                        errors.Add($"[绑定] world_cue_split.{key} 不是数组");
                    }
                }
                data.WorldCueSplit = split;
            }
            else
            {
                errors.Add("[绑定] world_cue_split 字段缺失或非对象");
            }

            if (errors.Count > 0) throw new BakeValidationException(errors);
            return data;
        }

        /// <summary>取属性原文(缺失 / null ⇒ null;承 <c>AudioEventTableBinder.RawOf</c> 同口径)。</summary>
        private static string RawOf(JsonNode obj, string key)
        {
            if (obj == null || !obj.TryGet(key, out JsonNode n) || n == null || n.Kind == JsonNodeKind.Null)
                return null;
            return n.Str ?? n.RawText;
        }

        private static IReadOnlyList<string> StringsOf(JsonNode obj, string key, int rowIndex, List<string> errors)
        {
            if (!obj.TryGet(key, out JsonNode n) || n == null || n.Kind != JsonNodeKind.Array)
            {
                errors.Add($"[绑定] requirements[{rowIndex}].{key} 缺失或非数组");
                return null;
            }
            var outp = new List<string>();
            int i = 0;
            foreach (JsonNode it in n.Items ?? new List<JsonNode>())
            {
                if (it == null || it.Kind != JsonNodeKind.String)
                {
                    errors.Add($"[绑定] requirements[{rowIndex}].{key}[{i}] 不是字符串");
                }
                else outp.Add(it.Str);
                i++;
            }
            return outp;
        }
    }
}
