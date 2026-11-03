// AC-5-05 —— `TICKS_PER_DAY` 在代码中**只有一个定义点**。
//
// 权威来源:
//   design/gdd/time-and-weather.md §规则四(「`TICKS_PER_DAY` 的值 = **5**;52 **引用**,不重复定义」)
//   design/gdd/time-and-weather.md §Detailed Rules 的 AC-5-05(「`TICKS_PER_DAY` 在代码中
//     **只有一个定义点**」)
//   DC-3 —— 52 的预算窗口与 5 的日边界是**同一个量**
//
// ─────────────────────────────────────────────────────────────────────────────
// 为什么需要这个门(2026-11-03 立的**直接**缘由)
// ─────────────────────────────────────────────────────────────────────────────
// 本轮我在 `Sim/Time/` 新造了一份 `TicksPerDay = 36000`,而 `Sim/World/TimeBase.cs` 里
// **早就**有一份 `TICKS_PER_DAY = 480L`(占位值)。全仓于是同时存在:
//   · **两个定义点**、**两套昼夜公式**、**两个互相矛盾的日长**(480 vs 36000);
//   · 而**没有任何东西会红** —— 两份实现各自有绿灯夹具。
// 这正是 GDD 说的「各写一份就会静默漂移,症状是事件预算在跨日时错位一格」。
// ⇒ 该门就是「不再允许第二次」的机械执行体。
//
// ─────────────────────────────────────────────────────────────────────────────
// 判据(三条,**缺一即空洞**)
// ─────────────────────────────────────────────────────────────────────────────
// ① **正控**:全仓必须**恰好存在**一条 `TICKS_PER_DAY` / `TicksPerDay` 的**声明**;
//    零条 ⇒ 红。没有这一条,「把常量删光」也能让门绿(空洞正控 · F4 形态)。
// ② **位置**:那一条必须落在 `Assets/Sim/World/TimeBase.cs`;
//    出现在别的文件 ⇒ 红。
// ③ **唯一**:允许文件内**也只能有一条**;写两条(改值后忘删旧的)⇒ 红。
//
// ─────────────────────────────────────────────────────────────────────────────
// 判据面与已知盲区(如实登记,不借绿)
// ─────────────────────────────────────────────────────────────────────────────
// **抓得到**:任何 `const` / `static readonly` / 裸类型声明,名为 `TICKS_PER_DAY`
//   或 `TicksPerDay`,出现在任意 `.cs` 的**代码**里。
// **不误报**:注释(`//` / `/* */`)、字符串字面量(`"…"` / `@"…"` / `$"…"`)、字符字面量
//   (`'…'`)里的同名文本 —— 先经 `StripCommentsAndLiterals` 抹成空格再匹配。
//   ⚠️ 这条是**首跑实测逼出来的**:不剥离时,本文件自己的头注释
//   (`// const int TICKS_PER_DAY = 480;` 那行)与 `time_constant_gate_test.cs` 里
//   作为**字符串实参**的 `"public const long TICKS_PER_DAY = 36000L;"` 双双被判成第二定义点
//   ⇒ 门在真树上**结构性恒红**。恒红的门等于没有门(与恒绿同为空洞)。
// **抓不到**(具名残余,承 `coding-standards.md` §「某件有门」的诚实登记义务):
//   · **换名同值**:`const int DayTicks = 36000;` —— 名字不同,本门看不见。
//     它是「第二份日长」的真实风险面,但按名字匹配的判据结构上无法覆盖;
//     要抓它得比对「值 = 36000 的常量」,那会把一切恰好等于 36000 的常量误判成红
//     (误报方向安全但噪音大),**本门不采用**,登记为缺口。
//   · **大小写变体**:`ticksPerDay` / `TicksPerday` —— 判据是 `Ordinal`(区分大小写)。
//     现存一例:`Tests/EditMode/RandomEvents/event_budget_defer_test.cs:99`
//     `const long ticksPerDay = 1200;`(夹具值,注释自陈「权威值归 52 配置轮」)。
//     本门**刻意不管**它:夹具值本就该能与生产值不同,把它判红会迫使夹具去读生产常量
//     (= 自指判据,见 F3)。
//   · **非 `.cs` 的定义点**(`.json` / `.asset` 里的同名键)—— 代码侧只扫 `.cs`;
//     数据侧另由 §数据侧 覆盖(见下)。
//
// ─────────────────────────────────────────────────────────────────────────────
// 数据侧(AC-5-05 的**第二半**:52 的数据集零字面副本)
// ─────────────────────────────────────────────────────────────────────────────
// AC-5-05 的 `THEN` 句有两半:① 定义点唯一在 5;② 「构建脚本读取 52 的数据集,
// 断言其**引用解析后与 5 同值**且**52 数据集中零字面副本**」。
// 本门只机械化得了 ②的**字面副本**那一侧 —— 凡 `assets/data/**/*.json` 出现
// `ticks_per_day` / `ticksPerDay` / `TICKS_PER_DAY`(大小写与 `_`/`-`/空 分隔皆算)⇒ 红。
//
// ⚠️ **今日是空真,不是已验证**:`assets/data/` 有 47 个已入库 JSON,对 `TICKS_PER_DAY`
//   与 `36000` **零命中**(复算见下)⇒ 数据侧今天**没有可判对象**。它的价值是「52 一写出副本就红」。
// ⚠️ **②的另一侧(引用解析后同值)本门不做** —— 52 今天没有数据集,「引用」形式
//   (跨表引用 Kind)在 ADR-014 管线里尚未定义 ⇒ 无可解析之物。登记为缺口,不借绿。
// ⚠️ **盲区**:若 52 将来把日长写成 `{"day_length": 36000}`,名字不同,本门看不见。
//   按值匹配会把一切恰好等于 36000 的 JSON 数字判红,噪音不可接受 —— 不采用。
//
// 复算(代码侧命中应为 1 条;数据侧应为 0 条):
//   grep -rnE "\b(const|static[[:space:]]+readonly)[[:space:]]+(int|long|uint|ulong)[[:space:]]+(TICKS_PER_DAY|TicksPerDay)\b" \
//     unity/Assets --include=*.cs
//   # 门实际判据,期望 0:
//   grep -rniE "ticks[_-]?per[_-]?day" assets/data --include=*.json | wc -l
//   # 只作事实记录(门**不**判值 —— 见上「盲区」),期望 0:
//   grep -rn "36000" assets/data --include=*.json | wc -l

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace DaYiJingCheng.EditorTools.Gates
{
    /// <summary>
    /// AC-5-05 门 —— `TICKS_PER_DAY` 全仓只有一个定义点,且必须在 `TimeBase.cs`。
    /// </summary>
    public static class TimeConstantSingleDefinitionGate
    {
        /// <summary>允许的定义点(工程内相对路径,正斜杠)。</summary>
        public const string AllowedRelativePath = "Assets/Sim/World/TimeBase.cs";

        /// <summary>被守的常量名(两种写法都算同一个量 —— GDD 用 `TICKS_PER_DAY`,代码用 `TicksPerDay`)。</summary>
        public static readonly string[] GuardedNames = { "TICKS_PER_DAY", "TicksPerDay" };

        /// <summary>
        /// 声明形:可选修饰符 + 整数类型 + 被守名字 + `=`。
        /// <para>⚠️ 不含「赋值目标在左侧带点」的形态(`x.TicksPerDay = …` 不可能作为常量声明出现),
        /// 故同一行若只有 `TimeBase.TICKS_PER_DAY` 这类**引用**,不会命中。</para>
        /// </summary>
        private static readonly Regex DefinitionPattern = new Regex(
            @"\b(?:(?:const|static\s+readonly)\s+)?(?:int|long|uint|ulong)\s+" +
            @"(?<name>TICKS_PER_DAY|TicksPerDay)\b\s*=",
            RegexOptions.Compiled);

        /// <summary>
        /// 数据侧判据:JSON 里出现「日长」标识(任意大小写 / `_` / `-` / 空分隔)⇒ 红。
        /// <para>AC-5-05 要求 52 的数据集**零字面副本**。此处按**名字**判,不按值判(见文件头「盲区」)。</para>
        /// </summary>
        private static readonly Regex DataSidePattern = new Regex(
            @"ticks[_\-\s]?per[_\-\s]?day",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>一条命中的声明。</summary>
        public readonly struct Declaration
        {
            /// <summary>工程内相对路径(正斜杠)。</summary>
            public readonly string RelativePath;
            /// <summary>1 起的行号。</summary>
            public readonly int Line;
            /// <summary>命中的名字。</summary>
            public readonly string Name;

            /// <summary>构造。</summary>
            public Declaration(string relativePath, int line, string name)
            {
                RelativePath = relativePath;
                Line = line;
                Name = name;
            }
        }

        /// <summary>
        /// 门链入口(供 <c>AssemblyGates.RunAll</c> / 构建前门调用)。
        /// </summary>
        /// <param name="root">工程根(<c>unity/</c>;门链各调用方传 <c>"."</c>)。</param>
        /// <returns>违例清单(空 = 门绿)。</returns>
        public static List<string> RunAll(string root = ".")
        {
            // 相对路径以**绝对化后的根**为基准 —— 传 "." 时 `GetRelativePath` 的基准是 CWD,
            // 不同调用方 CWD 不同会把相对路径算歪(症状是「明明在允许文件里却判成第二定义点」)。
            string rootFull = Path.GetFullPath(root ?? ".");
            var errs = new List<string>();

            // ── ① 代码侧:定义点唯一 ────────────────────────────────────────
            string assets = Path.Combine(rootFull, "Assets");
            if (!Directory.Exists(assets))
            {
                // 面守卫:扫不到 ⇒ 红。静默返回空会让「路径写错」与「门绿」不可分(空洞正控)。
                errs.Add($"[AC-5-05] 扫描面不存在:{assets} —— 门不可判(路径错 ≠ 判据通过)。");
            }
            else
            {
                var files = new List<(string relPath, string text)>();
                foreach (string f in Directory.GetFiles(assets, "*.cs", SearchOption.AllDirectories))
                {
                    string rel = Path.GetRelativePath(rootFull, Path.GetFullPath(f)).Replace('\\', '/');
                    files.Add((rel, File.ReadAllText(f)));
                }

                errs.AddRange(Evaluate(files));
            }

            // ── ② 数据侧:52 数据集零字面副本(AC-5-05 的第二半)────────────
            // 仓库布局:`<repo>/unity/`(projFull)与 `<repo>/assets/data/` 是兄弟目录。
            string dataRoot = Path.GetFullPath(Path.Combine(rootFull, "..", "assets", "data"));
            errs.AddRange(ScanDataSide(dataRoot));

            return errs;
        }

        /// <summary>
        /// 数据侧入口:取 <c>assets/data/**/*.json</c> 并判。
        /// </summary>
        /// <param name="dataRoot">数据目录绝对路径。</param>
        /// <returns>违例清单(空 = 绿)。</returns>
        public static List<string> ScanDataSide(string dataRoot)
        {
            if (!Directory.Exists(dataRoot))
            {
                // 面守卫:目录不在 ⇒ 红。`assets/data/` 已入库 47 件(git tracked),
                // 它消失 = 仓库结构损坏,不是「没有可判对象」。
                return new List<string>
                {
                    $"[AC-5-05] 数据集目录不存在:{dataRoot} —— AC-5-05 的数据集半边无从检查(缺面 ≠ 通过)。",
                };
            }

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
        /// 数据侧判据本体(**纯函数**,零 I/O)。
        /// </summary>
        /// <param name="files">(仓库内相对路径, 文件全文)序列。</param>
        /// <returns>违例清单(空 = 绿)。</returns>
        public static List<string> EvaluateData(IEnumerable<(string relPath, string text)> files)
        {
            var errs = new List<string>();

            if (files == null)
                return new List<string> { "[AC-5-05] 数据集扫描面为空(未传入任何文件)—— 门不可判。" };

            foreach ((string relPath, string text) in files)
            {
                if (text == null) continue;
                string[] lines = text.Replace("\r\n", "\n").Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    if (!DataSidePattern.IsMatch(lines[i])) continue;
                    errs.Add($"[AC-5-05] {relPath}:{i + 1} 出现「日长」标识的字面副本 —— " +
                             "AC-5-05 要求 52 的数据集**零字面副本**、只经引用取 5 的同值(DC-3)。");
                }
            }

            return errs;
        }

        /// <summary>
        /// 判据本体(**纯函数**,零 I/O)—— 夹具直驱它,门链只负责取文件。
        /// </summary>
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
                // 先剥注释与字面量 —— 否则本门的头注释与夹具里的**字符串实参**会被判成第二定义点
                // (首跑实测:真树上一片假红,门恒红)。
                string[] lines = StripCommentsAndLiterals(text).Replace("\r\n", "\n").Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    Match m = DefinitionPattern.Match(lines[i]);
                    if (m.Success)
                        hits.Add(new Declaration(relPath, i + 1, m.Groups["name"].Value));
                }
            }

            // ① 正控:一条都没有 = 常量被删光 ⇒ 红(否则「全删」也能让门绿)。
            if (hits.Count == 0)
            {
                errs.Add("[AC-5-05] 全仓**零条** `TICKS_PER_DAY` 声明 —— 常量被删光了。" +
                         "门必须能证明常量存在,否则「删掉即可变绿」。期望唯一落点:" +
                         AllowedRelativePath);
                return errs;
            }

            // ② 位置:任何不在允许文件里的声明 = 第二定义点。
            foreach (Declaration d in hits)
            {
                if (!string.Equals(d.RelativePath, AllowedRelativePath, StringComparison.Ordinal))
                {
                    errs.Add($"[AC-5-05] {d.RelativePath}:{d.Line} 声明了 `{d.Name}` —— " +
                             $"第二定义点。唯一允许的落点是 {AllowedRelativePath}。" +
                             "52 的日边界与预算窗口是同一个量(DC-3),各写一份会静默漂移。");
                }
            }

            // ③ 唯一:允许文件内也只能有一条。
            int allowedCount = 0;
            foreach (Declaration d in hits)
                if (string.Equals(d.RelativePath, AllowedRelativePath, StringComparison.Ordinal))
                    allowedCount++;

            if (allowedCount > 1)
            {
                errs.Add($"[AC-5-05] {AllowedRelativePath} 内有 **{allowedCount}** 条 `TICKS_PER_DAY` 声明 —— " +
                         "改值后忘删旧的一条,同样是第二定义点(两条会各自被不同调用点读到)。");
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
        /// 不处理 C# 11 原始字符串 <c>"""…"""</c> —— 本项目语言级别为 C# 9(见
        /// <c>docs/engine-reference/unity/VERSION.md</c>),该语法不可用。
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

                // 行注释:抹到行尾,换行留给主循环
                if (c == '/' && next == '/')
                {
                    while (i < n && text[i] != '\n') { sb.Append(' '); i++; }
                    continue;
                }

                // 块注释:抹到 */,换行保留
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

                // 逐字字符串 @"…":"" 是转义引号,反斜杠无特殊含义
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

                // 普通字符串(含 $"…";`$` 会先作为普通字符落下,不影响判据)
                if (c == '"')
                {
                    sb.Append(' ');
                    i++;
                    while (i < n)
                    {
                        if (text[i] == '\\')
                        {
                            bool hasNext = i + 1 < n;
                            // 转义换行仍要保住换行(否则行号会错),其余一律抹成空格
                            sb.Append(hasNext && text[i + 1] == '\n' ? '\n' : ' ');
                            sb.Append(' ');
                            i += hasNext ? 2 : 1;
                            continue;
                        }
                        if (text[i] == '"') { sb.Append(' '); i++; break; }
                        if (text[i] == '\n') { sb.Append('\n'); i++; break; } // 未闭合:换行即终止
                        sb.Append(' ');
                        i++;
                    }
                    continue;
                }

                // 字符字面量
                if (c == '\'')
                {
                    sb.Append(' ');
                    i++;
                    while (i < n)
                    {
                        if (text[i] == '\\')
                        {
                            sb.Append("  ");
                            i += 2;
                            continue;
                        }
                        if (text[i] == '\'') { sb.Append(' '); i++; break; }
                        if (text[i] == '\n') { sb.Append('\n'); i++; break; } // 未闭合(如撇号):换行即终止
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
