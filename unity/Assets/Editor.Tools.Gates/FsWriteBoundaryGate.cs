// S16-4 · retro-14 行动项 A4 —— 「测试不得写仓库」机械门。
//
// 立规缘由(retro-14 §Action Items #4):本类已**三发**同名事故 ——
//   ① S14-6 `pipeline_gate_test.cs` 的 `Directory.CreateDirectory(repoRoot/assets/audio)`
//      + `File.WriteAllText`(目录 `assets/audio/` 之所以存在,唯一原因就是这个副作用);
//   ② S15-5 `statereport_walkthrough_probe.cs` 的 `WriteOnce` 往 `docs/audio/` 写日期命名副本;
//   ③ 更早的 `Directory.CreateDirectory` 同类。
// 规则一直写在 `.claude/rules/test-standards.md`(「Unit tests must not depend on external
// state (filesystem...)」),但**没有门** —— 规则与执行脱节三轮。
//
// 权威来源:
//   `.claude/rules/test-standards.md` 第 3 条(Unit tests must not depend on external state)。
//   先例:`MedConsFloatGate`(注释剥离后 token 扫描,偏安全口径)。
//
// ⚠️ **扫描面 = `Assets/Tests/**/*.cs`(递归),刻意不含工具程序集** ——
//   `Editor.Tools.Bake` 写 `*.cooked` 是 **ADR-014 烘焙管线的法定职责**,把它纳入只会逼出
//   一条「整目录白名单」,那正是 QA plan 明禁的「白名单 = 全部命中项」自我豁免。
//   ⇒ 本门只答「测试能不能写仓库」;「工具能不能写」不是 grep 能回答的(见 §盲区)。
//
// ⚠️ **两条规则(默认放行,命中才红)**:
//   R1 写盘调用的第一个实参**直接就是字符串字面量** ⇒ 红(相对路径落点 = 仓库)。
//   R2 文件含写盘调用 **且** 含仓库**代码锚点** ⇒ 必须在 `Declarations` 里具名声明。
//      声明不是豁免:它要求写清「这个锚点喂的是读还是写,写落哪儿」。缺声明 = 红。
//
// ⚠️ **为什么不走多跳变量解析(实测否决)**:原型两版,第二版对 115 个调用点做 BFS
//   数据流解析,得 **2 红且全是误报**(`_tempDir`),而最该抓的
//   `u1_scene_spikes_test` / `story007_phase_spike_playmode_test` **仍漏**
//   (它们的写目标是 foreach 变量,锚点在另一个方法的 `Candidates.Add(...)` 里)。
//   ⇒ 静态数据流在此不可靠;**文件级共存**结构上稳定:规模小、每条都是真审查问题。
//
// ⚠️ **已知盲区(登记,不借绿)**:
//   ① 锚点与写盘**分处两个文件**时漏(如 util 里建路径、测试里写);
//   ② **字面量锚点**(`"Assets/…"` / `"docs/…"`)不进 R2 的共存规则 —— 实测 14 个文件
//      含此类字面量且含写盘调用,但多为**读**路径;强行纳入会新增 9 条低信号声明。
//      仅当写目标**本身**是字面量时由 R1 捕获;
//   ③ `Path.Combine("..", "..", "docs")` 这类**无锚点**绕行看不见。
//   **触发条件** = 出现第四例事故,且形态属上述盲区 ⇒ 按 `EcozoneIntegerGates` 形状
//   升 Cecil IL 级(该门已用 `Mono.Cecil` 做 IL 级判据,是本仓的更强先例)。
//
// ⚠️ **声明的两个反向判据(防白名单腐烂)**:声明过期(文件已无锚点)或指向不存在的文件
//    ⇒ **红**,不是静默忽略。承 `WriterExistenceGate` 的「stale waiver is red」口径。

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace DaYiJingCheng.EditorTools.Gates
{
    /// <summary>写盘边界门:测试树内「写仓库」的机械判据。</summary>
    public static class FsWriteBoundaryGate
    {
        /// <summary>扫描目录(相对工程根 `unity/`;测试树单一出处)。</summary>
        public const string ScanDirRelative = "Assets/Tests";

        /// <summary>写盘 API 名单(单一真源 —— 测试读本数组,不再自带字面量)。</summary>
        public static readonly string[] WriteApiNames =
        {
            "File.WriteAllText",
            "File.AppendAllText",
            "File.WriteAllBytes",
            "File.WriteAllLines",
            "File.AppendAllLines",
            "File.Create",
            "File.Delete",
            "File.Move",
            "File.Copy",
            "File.Replace",
            "Directory.CreateDirectory",
            "Directory.Delete",
            "Directory.Move",
        };

        /// <summary>仓库**代码锚点**(R2 的判据面;字面量锚点刻意不收,见文件头注 ②)。</summary>
        public static readonly string[] RepoCodeAnchors =
        {
            "Application.dataPath",
            "Directory.GetCurrentDirectory",
            "Application.streamingAssetsPath",
        };

        /// <summary>具名声明:<c>(文件后缀, 理由)</c>。<b>每条必须带理由</b> —— 空理由 = 红。</summary>
        public sealed class Declaration
        {
            /// <summary>相对工程根的路径后缀(如 <c>PlayMode/u1_scene_spikes_test.cs</c>)。</summary>
            public string FileSuffix;

            /// <summary>为何「锚点存在但写目标不在仓库」,或「写目标限定的暂存区是哪个」。</summary>
            public string Reason;
        }

        /// <summary>
        /// 声明表。**每条都经过逐文件阅读**,不是「扫不到就豁免」:
        /// 前两条 = 锚点用于推导**指定暂存区** `unity/Logs/`(gitignored);
        /// 后五条 = 锚点只喂**读**路径,写落 temp / persistentDataPath。
        /// ⚠️ 第 6 条(`assembly_gate_b6_test`)是**真树测试抓出来的漏项** ——
        /// 起草时的抽样只列了「有代码锚点且无字面量锚点」的 4 个文件,漏掉了
        /// 「两种锚点都有」的这一个。抽样不是普查,故真树断言不可省。
        /// ⚠️ 第 7 条(`build_modules_bake_test`,`TD-itemdb-01` 兑现轮)是**同一形态的第二例**:
        /// 起草 `build_modules` 测试时没想起本门存在,真树断言 `test_realTestTree_zeroRed` 直接点名。
        /// 两次都是真树断言抓的,**不是**起草者自查抓的 —— 这条经验已入 `TD-itemdb-01` 的债条。
        /// </summary>
        public static readonly Declaration[] Declarations =
        {
            new Declaration
            {
                FileSuffix = "PlayMode/u1_scene_spikes_test.cs",
                Reason = "锚点仅用于推导项目根下的指定暂存区 unity/Logs/(gitignored 尖刺结果);" +
                         "另一候选 persistentDataPath;原 `Assets/` 候选已于 S16-4 删除(它会变成 Unity 资产)",
            },
            new Declaration
            {
                FileSuffix = "PlayMode/story007_phase_spike_playmode_test.cs",
                Reason = "同 u1:锚点仅用于 unity/Logs/(gitignored 尖刺结果);原 `Assets/` 候选已于 S16-4 删除",
            },
            new Declaration
            {
                FileSuffix = "EditMode/Audio/statereport_walkthrough_probe.cs",
                Reason = "`Application.dataPath` 仅用于**读** DataCooked / Audio;写目标走 Path.GetTempPath()",
            },
            new Declaration
            {
                FileSuffix = "EditMode/DiseaseSimulation/rule_nine_gate_test.cs",
                Reason = "`Application.dataPath` 仅用于**读**(定位 unity/ 根以扫 RuleNineGate.cs 源文本、" +
                         "取 ScriptAssemblies 下的 DLL 做 IL 半);写目标 = Path.GetTempPath() 下的" +
                         "一次性探针目录(`rulenine-probe-<guid>`),TearDown 即删 —— 不写仓库",
            },
            new Declaration
            {
                FileSuffix = "EditMode/PatientAI/presentation_projection_test.cs",
                Reason = "`Directory.GetCurrentDirectory` 在被测门的**读**搜索路径表里;写目标走局部 tmp",
            },
            new Declaration
            {
                FileSuffix = "PlayMode/determinism_golden_crossplatform_test.cs",
                Reason = "`Application.dataPath` 仅用于**读** golden;写目标走 persistentDataPath" +
                         "(环境变量 AC29_OUT 覆盖由操作者负责)",
            },
            new Declaration
            {
                FileSuffix = "EditMode/Sim/assembly_gate_b6_test.cs",
                Reason = "`Application.dataPath` 仅用于**读** Sim.Codec 源码(证明排除项非空设);" +
                         "写目标走 Path.GetTempPath()(tmp / tmp/Sub)",
            },
            new Declaration
            {
                FileSuffix = "EditMode/ClinicMachine/build_modules_bake_test.cs",
                Reason = "`Application.dataPath` 仅用于**读** —— 由它推出 `RepoRoot`," +
                         "再读 `assets/data/` 的两张真种子表与 `Assets/DataCooked/` 的产物;" +
                         "写目标一律落 `Path.GetTempPath()/djc-buildmodules-<guid>/`," +
                         "`finally` 里整棵删掉(跨表同源负向夹具造最小仓根用)",
            },
        };

        /// <summary>一处写盘调用点。</summary>
        public sealed class WriteSite
        {
            /// <summary>行号(1 基,按注释剥离后仍与原文件同行)。</summary>
            public int Line;

            /// <summary>命中的 API 名(取自 <see cref="WriteApiNames"/>)。</summary>
            public string Api;

            /// <summary>第一个实参的表达式文本(字符串内容已置空,引号保留)。</summary>
            public string Target;

            /// <summary>第一个实参的**原文本**(仅报错用 —— <see cref="Target"/> 的字符串内容已被置空)。</summary>
            public string RawTarget;
        }

        private static readonly Regex WriteCallRegex = BuildWriteCallRegex();

        private static Regex BuildWriteCallRegex()
        {
            var sb = new StringBuilder(@"\b(?:");
            for (int i = 0; i < WriteApiNames.Length; i++)
            {
                if (i > 0) sb.Append('|');
                sb.Append(Regex.Escape(WriteApiNames[i]));
            }
            sb.Append(@")\s*\(");
            return new Regex(sb.ToString(), RegexOptions.Compiled);
        }

        /// <summary>
        /// 全量求值:递归扫 <c>Assets/Tests</c>,返回失败列表(空 = 绿)。
        /// </summary>
        /// <param name="unityProjectRoot">工程根(含 `Assets/`;通常 = `Application.dataPath` 的上级)。</param>
        public static List<string> RunAll(string unityProjectRoot)
        {
            var errs = new List<string>();
            string dir = Path.Combine(unityProjectRoot, ScanDirRelative);
            if (!Directory.Exists(dir))
            {
                errs.Add($"[fs-write] 扫描目录不存在:{dir}(测试树被移位?门须同步改口径)");
                return errs;
            }

            var seen = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string file in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
            {
                string rel = ToGateRelative(file, unityProjectRoot);
                string source = File.ReadAllText(file);
                seen[rel] = source;
                errs.AddRange(Scan(rel, source));
            }

            errs.AddRange(ValidateDeclarations(seen));
            return errs;
        }

        /// <summary>
        /// 单文件扫描(R1 + R2)。**纯函数** —— 负向夹具直接喂源码,不碰文件系统。
        /// </summary>
        /// <param name="relPath">相对工程根的路径(仅用于报文与声明匹配)。</param>
        /// <param name="source">该文件源码。</param>
        public static List<string> Scan(string relPath, string source)
        {
            var errs = new List<string>();
            string stripped = StripComments(source);
            string structure = BlankStringLiterals(stripped);
            List<WriteSite> sites = FindWriteSitesIn(structure, stripped);
            if (sites.Count == 0) return errs;

            foreach (WriteSite s in sites)
            {
                if (!IsStringLiteral(s.Target)) continue;
                errs.Add($"[fs-write·R1] {relPath}:{s.Line} {s.Api} 的目标是**相对字面量** " +
                         $"`{s.RawTarget}` —— 相对路径按 cwd 解析 = 仓库落点;" +
                         "测试写盘须落 Path.GetTempPath() / persistentDataPath");
            }

            string[] anchors = FindRepoAnchorsIn(structure);
            if (anchors.Length == 0) return errs;

            Declaration decl = FindDeclaration(relPath);
            if (decl == null)
            {
                errs.Add($"[fs-write·R2] {relPath} 同时含写盘调用({sites.Count} 处)与仓库锚点" +
                         $"({string.Join(" / ", anchors)}) —— 须在 FsWriteBoundaryGate.Declarations " +
                         "里**具名声明**该锚点喂的是读还是写、写落哪儿(无声明 = 红)");
            }
            else if (string.IsNullOrWhiteSpace(decl.Reason))
            {
                errs.Add($"[fs-write·R2] {relPath} 的声明**无理由** —— 白名单必须说清为什么," +
                         "空理由等于自我豁免");
            }

            return errs;
        }

        /// <summary>
        /// 声明的反向判据:过期(文件已无锚点)或悬空(文件不存在)皆红。
        /// </summary>
        /// <param name="files">相对路径 → 源码(测试可直接构造,不碰文件系统)。</param>
        public static List<string> ValidateDeclarations(IReadOnlyDictionary<string, string> files)
        {
            var errs = new List<string>();
            foreach (Declaration d in Declarations)
            {
                if (string.IsNullOrWhiteSpace(d.FileSuffix))
                {
                    errs.Add("[fs-write·声明] 存在 FileSuffix 为空的声明项");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(d.Reason))
                {
                    errs.Add($"[fs-write·声明] `{d.FileSuffix}` 的声明无理由");
                    continue;
                }

                string source = FindBySuffix(files, d.FileSuffix);
                if (source == null)
                {
                    errs.Add($"[fs-write·声明] `{d.FileSuffix}` 声明悬空 —— 测试树里找不到该文件" +
                             "(文件改名/删除后声明未同步 = 白名单腐烂)");
                    continue;
                }

                string structure = BlankStringLiterals(StripComments(source));
                if (FindRepoAnchorsIn(structure).Length == 0)
                {
                    errs.Add($"[fs-write·声明] `{d.FileSuffix}` 声明**已过期** —— 该文件现无任何仓库锚点," +
                             "声明应删除(过期声明会让门对它永久失明)");
                }
            }
            return errs;
        }

        /// <summary>
        /// 找出源码里的全部写盘调用点(**注释与字符串字面量均已处理**:
        /// 注释里的 API 名不算,夹具字符串里的调用也不算)。
        /// </summary>
        public static List<WriteSite> FindWriteSites(string source)
        {
            string stripped = StripComments(source);
            return FindWriteSitesIn(BlankStringLiterals(stripped), stripped);
        }

        /// <summary>找出源码里的仓库代码锚点(注释剥离后;字面量锚点刻意不收)。</summary>
        public static string[] FindRepoAnchors(string source)
            => FindRepoAnchorsIn(BlankStringLiterals(StripComments(source)));

        /// <summary>注释剥离:与 <c>MedConsFloatGate.StripComments</c> 同款(保留换行与偏移)。</summary>
        public static string StripComments(string source)
        {
            if (string.IsNullOrEmpty(source)) return string.Empty;
            char[] buf = source.ToCharArray();
            int i = 0;
            char quote = '\0';
            while (i < buf.Length)
            {
                char c = buf[i];
                if (quote != '\0')
                {
                    if (c == '\\' && i + 1 < buf.Length) { i += 2; continue; }
                    if (c == quote) quote = '\0';
                    i++;
                    continue;
                }
                if (c == '"' || c == '\'') { quote = c; i++; continue; }
                if (c == '/' && i + 1 < buf.Length && buf[i + 1] == '/')
                {
                    while (i < buf.Length && buf[i] != '\n') { buf[i] = ' '; i++; }
                    continue;
                }
                if (c == '/' && i + 1 < buf.Length && buf[i + 1] == '*')
                {
                    buf[i] = ' '; buf[i + 1] = ' '; i += 2;
                    while (i < buf.Length && !(buf[i] == '*' && i + 1 < buf.Length && buf[i + 1] == '/'))
                    {
                        if (buf[i] != '\n') buf[i] = ' ';
                        i++;
                    }
                    if (i + 1 < buf.Length) { buf[i] = ' '; buf[i + 1] = ' '; i += 2; }
                    continue;
                }
                i++;
            }
            return new string(buf);
        }

        /// <summary>
        /// 字符串**内容**置空、引号保留(偏移不变)——
        /// 目的:夹具字符串里的 `File.WriteAllText(...)` 不算真调用;
        /// 同时「目标是字面量」仍可判(置空后形如 <c>"   "</c>)。
        /// </summary>
        public static string BlankStringLiterals(string source)
        {
            if (string.IsNullOrEmpty(source)) return string.Empty;
            char[] buf = source.ToCharArray();
            int i = 0;
            char quote = '\0';
            while (i < buf.Length)
            {
                char c = buf[i];
                if (quote != '\0')
                {
                    if (c == '\\' && i + 1 < buf.Length) { buf[i] = ' '; buf[i + 1] = ' '; i += 2; continue; }
                    if (c == quote) { quote = '\0'; i++; continue; }
                    buf[i] = ' ';
                    i++;
                    continue;
                }
                if (c == '"' || c == '\'') quote = c;
                i++;
            }
            return new string(buf);
        }

        // ── 内部 ─────────────────────────────────────────────────────────────

        private static List<WriteSite> FindWriteSitesIn(string structure, string stripped)
        {
            var sites = new List<WriteSite>();
            foreach (Match m in WriteCallRegex.Matches(structure))
            {
                int argStart = m.Index + m.Length;
                sites.Add(new WriteSite
                {
                    Line = LineOf(structure, m.Index),
                    Api = m.Value.Substring(0, m.Value.Length - 1).Trim(),
                    Target = FirstArgument(structure, argStart).Trim(),
                    RawTarget = FirstArgument(stripped, argStart).Trim(),
                });
            }
            return sites;
        }

        private static string[] FindRepoAnchorsIn(string structure)
        {
            var found = new List<string>();
            foreach (string anchor in RepoCodeAnchors)
                if (structure.IndexOf(anchor, StringComparison.Ordinal) >= 0)
                    found.Add(anchor);
            return found.ToArray();
        }

        private static int LineOf(string text, int index)
        {
            int line = 1;
            for (int i = 0; i < index && i < text.Length; i++)
                if (text[i] == '\n') line++;
            return line;
        }

        /// <summary>取第一个实参(按括号层级与顶层逗号切分)。</summary>
        private static string FirstArgument(string text, int start)
        {
            var sb = new StringBuilder();
            int depth = 0;
            for (int i = start; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '(' || c == '[' || c == '{') depth++;
                else if (c == ')' || c == ']' || c == '}')
                {
                    if (depth == 0) break;
                    depth--;
                }
                else if (c == ',' && depth == 0) break;
                sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>实参是否**只是一个字符串字面量**(置空后形如 <c>"   "</c> / <c>@"  "</c>)。</summary>
        private static bool IsStringLiteral(string target)
            => Regex.IsMatch(target, "^@?\"[^\"]*\"$");

        private static Declaration FindDeclaration(string relPath)
        {
            foreach (Declaration d in Declarations)
            {
                if (string.IsNullOrWhiteSpace(d.FileSuffix)) continue;
                if (relPath.EndsWith(d.FileSuffix, StringComparison.Ordinal)) return d;
            }
            return null;
        }

        private static string FindBySuffix(IReadOnlyDictionary<string, string> files, string suffix)
        {
            foreach (KeyValuePair<string, string> kv in files)
                if (kv.Key.EndsWith(suffix, StringComparison.Ordinal)) return kv.Value;
            return null;
        }

        private static string ToGateRelative(string absoluteOrRelative, string unityProjectRoot)
        {
            string full = Path.GetFullPath(absoluteOrRelative).Replace('\\', '/');
            string root = Path.GetFullPath(unityProjectRoot).Replace('\\', '/').TrimEnd('/') + "/";
            return full.StartsWith(root, StringComparison.Ordinal) ? full.Substring(root.Length) : full;
        }
    }
}
