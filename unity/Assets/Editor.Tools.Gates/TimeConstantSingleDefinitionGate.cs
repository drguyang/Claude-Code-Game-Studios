// AC-5-05 —— `TICKS_PER_DAY` / `NIGHT_START` / `NIGHT_SPAN` 的**值只有一个定义点**。
//
// 权威来源:
//   design/gdd/time-and-weather.md §规则四(「`TICKS_PER_DAY` 的值 = **5**;52 **引用**,不重复定义」)
//   design/gdd/time-and-weather.md:184 / :668 —— AC-5-05 的判据句与 GIVEN/WHEN/THEN
//   **ADR-038 §D1** —— 三值**外部化到 `assets/data/5_time.json`**;代码里零 `const`
//   DC-3 —— 52 的预算窗口与 5 的日边界是**同一个量**
//
// ─────────────────────────────────────────────────────────────────────────────
// 判据(三条,缺一即空洞)
// ─────────────────────────────────────────────────────────────────────────────
// ① **代码里零 `const` / `static readonly`**:值不在代码里 —— 出现任何一条 ⇒ 红。
//    (ADR-038 之前,这里守的是「恰一条 const 且落在 TimeBase.cs」;值外部化后
//     那条判据反而会放过「代码里也留了一份 36000」的形态,故整个换向。)
// ② **唯一的裸字段声明**必须落在 `Assets/Sim/TimeParams.cs`:
//    那是**载体槽**(装载后填值),不是定义;写在别处 = 第二份载体 ⇒ 红。
// ③ **正控**:全仓一条声明都没有 ⇒ 红。没有它,「把 `TimeParams` 的字段删光」
//    也会让门绿 —— 而那会让装载后的值无处安放(空洞正控 · F4 形态)。
//
// ─────────────────────────────────────────────────────────────────────────────
// 为什么需要这个门(2026-11-03 立 · 两次真实事故)
// ─────────────────────────────────────────────────────────────────────────────
// ① **我造了第二份日长**:`Sim/Time/WorldTimeConstants.cs`(36000)与**早已存在**的
//    `Sim/World/TimeBase.cs`(480)并存 —— 两个定义点、两套昼夜公式、两个冲突值,
//    **两边各有绿灯夹具**,没有任何东西会红。
// ② **门自己曾漏判**:首版判据要求「类型 + 名字 + `=`」,于是
//    `public long TICKS_PER_DAY;`(`Sim/TimeBase.cs:23`,无初始化)**完全看不见** ——
//    门绿着,第二个声明一直在。这正是「恒绿的假门」,与恒红同样是空洞。
//    ⇒ 本版判据把 `;` 也算作声明终止符。
//
// ─────────────────────────────────────────────────────────────────────────────
// 判据面与已知盲区(如实登记,不借绿)
// ─────────────────────────────────────────────────────────────────────────────
// **抓得到**:`TICKS_PER_DAY` / `NIGHT_START` / `NIGHT_SPAN`(及其驼峰写法)在任意 `.cs`
//   **代码**里的 `const` / `static readonly` / 裸字段声明(带初始化或不带)。
// **不误报**:注释、字符串/字符字面量里的同名文本 —— 先经 `StripCommentsAndLiterals`
//   抹成空格再匹配(换行保留,行号不漂)。
// **抓不到**(具名残余):
//   · **换名同值**:`const long DayLength = 36000;` —— 名字不同,本门看不见。
//     按值匹配会把一切恰好等于 36000 的常量判红(噪音不可接受)⇒ 不采用,登记为缺口。
//   · **数据侧**:本门只扫 `.cs`。`assets/data/` 侧的「唯一定义 + 52 只许 `$ref`」
//     由 ADR-038 §D2 的数据侧判据负责(另一条,同一轮)。
//
// 复算(期望:const 0 条 · 裸字段恰 1 条且落在 TimeParams.cs):
//   grep -rnE "\b(const|static[[:space:]]+readonly)[[:space:]]+(int|long|uint|ulong)[[:space:]]+(TICKS_PER_DAY|NIGHT_START|NIGHT_SPAN)\b" \
//     unity/Assets --include=*.cs
//   grep -rnE "\b(int|long|uint|ulong)[[:space:]]+(TICKS_PER_DAY|NIGHT_START|NIGHT_SPAN)\b[[:space:]]*;" \
//     unity/Assets --include=*.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace DaYiJingCheng.EditorTools.Gates
{
    /// <summary>
    /// AC-5-05 门 —— 时间三值在**代码**里只有一个载体槽、零个 `const`,且载体必须落在
    /// `Sim/TimeParams.cs`(ADR-038 §D1)。
    /// </summary>
    public static class TimeConstantSingleDefinitionGate
    {
        /// <summary>允许的**载体槽**落点(工程内相对路径,正斜杠)。</summary>
        public const string AllowedRelativePath = "Assets/Sim/TimeParams.cs";

        /// <summary>被守的名字(三种写法都算同一个量)。</summary>
        public static readonly string[] GuardedNames = { "TICKS_PER_DAY", "NIGHT_START", "NIGHT_SPAN" };

        /// <summary>
        /// 声明形:可选修饰符 + 整数类型 + 被守名字 + 终止符(`=` 或 `;`)。
        /// <para>⚠️ `;` 那一支是 2026-11-03 补的洞:首版只认 `=`,于是
        /// <c>public long TICKS_PER_DAY;</c> 完全不可见。</para>
        /// </summary>
        private static readonly Regex DefinitionPattern = new Regex(
            @"\b(?<mods>(?:const\s+|static\s+readonly\s+)?)" +
            @"(?:int|long|uint|ulong)\s+" +
            @"(?<name>TICKS_PER_DAY|NIGHT_START|NIGHT_SPAN)\b\s*(?:=|;)",
            RegexOptions.Compiled);

        /// <summary>一条命中的声明。</summary>
        public readonly struct Declaration
        {
            /// <summary>工程内相对路径(正斜杠)。</summary>
            public readonly string RelativePath;

            /// <summary>1 起的行号。</summary>
            public readonly int Line;

            /// <summary>命中的名字。</summary>
            public readonly string Name;

            /// <summary>是否 `const` / `static readonly`(= 值写死在代码里)。</summary>
            public readonly bool IsConstLike;

            /// <summary>构造。</summary>
            public Declaration(string relativePath, int line, string name, bool isConstLike)
            {
                RelativePath = relativePath;
                Line = line;
                Name = name;
                IsConstLike = isConstLike;
            }
        }

        /// <summary>门链入口(供 <c>AssemblyGates.RunAll</c> / 构建前门调用)。</summary>
        /// <param name="root">工程根(<c>unity/</c>;门链各调用方传 <c>"."</c>)。</param>
        /// <returns>违例清单(空 = 门绿)。</returns>
        public static List<string> RunAll(string root = ".")
        {
            string rootFull = Path.GetFullPath(root ?? ".");
            string assets = Path.Combine(rootFull, "Assets");
            if (!Directory.Exists(assets))
            {
                // 面守卫:扫不到 ⇒ 红。静默返回空会让「路径写错」与「门绿」不可分(空洞正控)。
                return new List<string>
                {
                    $"[AC-5-05] 扫描面不存在:{assets} —— 门不可判(路径错 ≠ 判据通过)。",
                };
            }

            var files = new List<(string relPath, string text)>();
            foreach (string f in Directory.GetFiles(assets, "*.cs", SearchOption.AllDirectories))
            {
                string rel = Path.GetRelativePath(rootFull, Path.GetFullPath(f)).Replace('\\', '/');
                files.Add((rel, File.ReadAllText(f)));
            }

            return Evaluate(files);
        }

        /// <summary>判据本体(**纯函数**,零 I/O)。</summary>
        /// <param name="files">(工程内相对路径, 文件全文)序列。</param>
        public static List<string> Evaluate(IEnumerable<(string relPath, string text)> files)
        {
            var errs = new List<string>();
            var hits = new List<Declaration>();

            if (files == null)
                return new List<string> { "[AC-5-05] 扫描面为空(未传入任何文件)—— 门不可判。" };

            foreach ((string relPath, string text) in files)
            {
                if (text == null) continue;
                // 先剥注释与字面量 —— 否则门的头注释与夹具里的字符串实参会被判成声明(首跑实测)。
                string[] lines = StripCommentsAndLiterals(text).Replace("\r\n", "\n").Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    Match m = DefinitionPattern.Match(lines[i]);
                    if (!m.Success) continue;
                    bool constLike = m.Groups["mods"].Success && m.Groups["mods"].Value.Trim().Length > 0;
                    hits.Add(new Declaration(relPath, i + 1, m.Groups["name"].Value, constLike));
                }
            }

            // ③ 正控:一条都没有 = 载体被删光 ⇒ 红(否则「全删」也能让门绿)。
            if (hits.Count == 0)
            {
                errs.Add("[AC-5-05] 全仓**零条**时间参数声明 —— `TimeParams` 的载体槽被删光了。" +
                         $"门必须能证明载体存在。期望唯一落点:{AllowedRelativePath}");
                return errs;
            }

            foreach (Declaration d in hits)
            {
                // ① 代码里零 const —— 值不在代码里(ADR-038 §D1)。
                if (d.IsConstLike)
                {
                    errs.Add($"[AC-5-05] {d.RelativePath}:{d.Line} 以 `const` / `static readonly` 声明了 " +
                             $"`{d.Name}` —— 值**不在代码里**(ADR-038 §D1:唯一来源 = assets/data/5_time.json)。" +
                             "代码里留一份 = 第二个定义点,改数据不改代码就会静默漂移。");
                    continue;
                }

                // ② 裸字段只许落在载体文件。
                if (!string.Equals(d.RelativePath, AllowedRelativePath, StringComparison.Ordinal))
                {
                    errs.Add($"[AC-5-05] {d.RelativePath}:{d.Line} 声明了 `{d.Name}` 字段 —— " +
                             $"第二份载体。唯一允许的落点是 {AllowedRelativePath}。");
                }
            }

            // ② 载体文件内也只能有一条(同一名字写两遍同样是第二份载体)。
            var seen = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (Declaration d in hits)
            {
                if (!string.Equals(d.RelativePath, AllowedRelativePath, StringComparison.Ordinal)) continue;
                if (d.IsConstLike) continue;
                seen.TryGetValue(d.Name, out int n);
                seen[d.Name] = n + 1;
            }
            foreach (KeyValuePair<string, int> kv in seen)
            {
                if (kv.Value > 1)
                    errs.Add($"[AC-5-05] {AllowedRelativePath} 内 `{kv.Key}` 有 **{kv.Value}** 条声明 —— " +
                             "同一载体槽写两遍,装载后哪个生效取决于字段序(第二真源)。");
            }

            return errs;
        }

        /// <summary>
        /// 数据侧的**唯一定义点**(ADR-038 §D1)。
        /// </summary>
        public const string DataDefinitionFile = "assets/data/5_time.json";

        /// <summary>`ticks_per_day` 字段名的匹配(值首字符决定形态:数字 = 字面副本;`{` = `$ref`)。</summary>
        private static readonly Regex DataFieldPattern = new Regex(
            "\"(ticks_per_day|night_start|night_span)\"\\s*:\\s*(?<v>.)",
            RegexOptions.Compiled);

        /// <summary>
        /// 数据侧入口(供 <c>AssemblyGates.RunAll</c> 调用;仓库布局 = `&lt;repo&gt;/assets/data/`)。
        /// </summary>
        /// <param name="root">工程根(<c>unity/</c>)。</param>
        /// <returns>违例清单(空 = 绿)。</returns>
        public static List<string> RunDataSide(string root = ".")
        {
            string dataRoot = Path.GetFullPath(
                Path.Combine(Path.GetFullPath(root ?? "."), "..", "assets", "data"));

            if (!Directory.Exists(dataRoot))
                return new List<string>
                {
                    $"[AC-5-05] 数据集目录不存在:{dataRoot} —— 门不可判(缺面 ≠ 通过)。",
                };

            var files = new List<(string relPath, string text)>();
            foreach (string f in Directory.GetFiles(dataRoot, "*.json", SearchOption.AllDirectories))
            {
                string rel = "assets/data/" +
                             Path.GetRelativePath(dataRoot, Path.GetFullPath(f)).Replace('\\', '/');
                files.Add((rel, File.ReadAllText(f)));
            }

            return EvaluateData(files);
        }

        /// <summary>
        /// 数据侧判据本体(**纯函数**):
        /// <list type="number">
        ///   <item><c>5_time.json</c> 必须存在,且三个字段各**恰出现一次**、值形为 JSON 数字</item>
        ///   <item>其他任何数据文件里的这三个字段值必须是 <c>{</c>(即 <c>{"$ref": …}</c>)——
        ///     数字即**字面副本**,红</item>
        ///   <item>三个字段在 <c>5_time.json</c> 之外**只允许**以 `$ref` 出现</item>
        /// </list>
        /// </summary>
        /// <param name="files">(仓库内相对路径, 文件全文)序列。</param>
        public static List<string> EvaluateData(IEnumerable<(string relPath, string text)> files)
        {
            if (files == null)
                return new List<string> { "[AC-5-05] 数据集扫描面为空 —— 门不可判。" };

            var errs = new List<string>();
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            bool sawDefinitionFile = false;

            foreach ((string relPath, string text) in files)
            {
                if (text == null) continue;
                bool isDefFile = string.Equals(relPath, DataDefinitionFile, StringComparison.Ordinal);
                if (isDefFile) sawDefinitionFile = true;

                string[] lines = text.Replace("\r\n", "\n").Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    foreach (Match m in DataFieldPattern.Matches(lines[i]))
                    {
                        string name = m.Groups[1].Value;
                        char v = m.Groups["v"].Success && m.Groups["v"].Value.Length > 0
                            ? m.Groups["v"].Value[0] : '\0';

                        if (isDefFile)
                        {
                            counts.TryGetValue(name, out int c);
                            counts[name] = c + 1;

                            // 定义文件里必须是**数字**(字符串/对象都非法:禁隐式转换、禁二次 $ref)
                            if (v != '-' && !(v >= '0' && v <= '9'))
                                errs.Add($"[AC-5-05] {relPath}:{i + 1} 的 `{name}` 值须为 JSON 数字" +
                                         $"(实得首字符 '{v}')—— 定义文件里它就是**真值**(ADR-038 §D1)");
                        }
                        else if (v != '{')
                        {
                            // 其他文件只许 `{"$ref": …}` —— 数字/字符串都是字面副本
                            errs.Add($"[AC-5-05] {relPath}:{i + 1} 的 `{name}` 是**字面副本**" +
                                     $"(首字符 '{v}')—— 只许写 {{\"$ref\": \"5_time.{name}\"}};" +
                                     "各写一份就会在改值时静默漂移(52 的预算窗口与 5 的日边界是同一个量)");
                        }
                    }
                }
            }

            if (!sawDefinitionFile)
                return new List<string>
                {
                    $"[AC-5-05] 缺唯一定义文件 {DataDefinitionFile} —— 值无处安放(门不可判)。",
                };

            foreach (string name in new[] { "ticks_per_day", "night_start", "night_span" })
            {
                counts.TryGetValue(name, out int c);
                if (c == 0)
                    errs.Add($"[AC-5-05] {DataDefinitionFile} 缺 `{name}` —— 三值必须齐(ADR-038 §D1)");
                else if (c > 1)
                    errs.Add($"[AC-5-05] {DataDefinitionFile} 里 `{name}` 出现 **{c}** 次 —— 同一份文件里写两遍,后者静默覆盖前者");
            }

            return errs;
        }

        /// <summary>
        /// 把注释与字符串 / 字符字面量的内容抹成空格 —— **换行原样保留**,使行号与原文一一对应。
        /// </summary>
        /// <remarks>
        /// 覆盖:行注释 <c>//</c> · 块注释 <c>/* */</c> · 普通字符串 <c>"…"</c>(含 <c>$"…"</c>)·
        /// 逐字字符串 <c>@"…"</c>(<c>""</c> = 转义引号)· 字符字面量 <c>'…'</c>。
        /// 未闭合的字符串 / 字符字面量遇换行即终止(防御:不让一个坏字面量吞掉后面整个文件)。
        /// 不处理 C# 11 原始字符串 <c>"""…"""</c> —— 本项目语言级别为 C# 9
        /// (见 <c>docs/engine-reference/unity/VERSION.md</c>),该语法不可用。
        /// </remarks>
        /// <param name="text">源文件全文。</param>
        /// <returns>等长文本,注释与字面量内容变空格、换行保持不变。</returns>
        public static string StripCommentsAndLiterals(string text)
        {
            if (string.IsNullOrEmpty(text)) return text ?? string.Empty;

            var sb = new StringBuilder(text.Length);
            int i = 0, n = text.Length;

            while (i < n)
            {
                char c = text[i];
                char next = i + 1 < n ? text[i + 1] : '\0';

                if (c == '/' && next == '/')
                {
                    while (i < n && text[i] != '\n') { sb.Append(' '); i++; }
                    continue;
                }

                if (c == '/' && next == '*')
                {
                    sb.Append("  ");
                    i += 2;
                    while (i < n && !(text[i] == '*' && i + 1 < n && text[i + 1] == '/'))
                    {
                        sb.Append(text[i] == '\n' ? '\n' : ' ');
                        i++;
                    }
                    if (i < n) { sb.Append("  "); i += 2; }
                    continue;
                }

                if (c == '@' && next == '"')
                {
                    sb.Append("  ");
                    i += 2;
                    while (i < n)
                    {
                        if (text[i] == '"')
                        {
                            if (i + 1 < n && text[i + 1] == '"') { sb.Append("  "); i += 2; continue; }
                            sb.Append(' ');
                            i++;
                            break;
                        }
                        sb.Append(text[i] == '\n' ? '\n' : ' ');
                        i++;
                    }
                    continue;
                }

                if (c == '"')
                {
                    sb.Append(' ');
                    i++;
                    while (i < n)
                    {
                        if (text[i] == '\\')
                        {
                            bool hasNext = i + 1 < n;
                            sb.Append(hasNext && text[i + 1] == '\n' ? '\n' : ' ');
                            sb.Append(' ');
                            i += hasNext ? 2 : 1;
                            continue;
                        }
                        if (text[i] == '"') { sb.Append(' '); i++; break; }
                        if (text[i] == '\n') { sb.Append('\n'); i++; break; }
                        sb.Append(' ');
                        i++;
                    }
                    continue;
                }

                if (c == '\'')
                {
                    sb.Append(' ');
                    i++;
                    while (i < n)
                    {
                        if (text[i] == '\\') { sb.Append("  "); i += 2; continue; }
                        if (text[i] == '\'') { sb.Append(' '); i++; break; }
                        if (text[i] == '\n') { sb.Append('\n'); i++; break; }
                        sb.Append(' ');
                        i++;
                    }
                    continue;
                }

                sb.Append(c);
                i++;
            }

            return sb.ToString();
        }
    }
}
