// 门链**源文本解析器**(C-1 · `TD-gate-01` 的执行体 · 2026-10-21)
//
// 存在理由 —— 「零红」是单调判据:
//   `assembly_gate_runall_test.cs` 断的是 `errs.Count == 0`。**它有方向性** ——
//   只覆盖「多了不该有的红」,对「**少了一个门**」完全失明。实测:把
//   `AssemblyGates.cs:130`(`errs.AddRange(FsWriteBoundaryGate.RunAll("."))`)删掉,
//   17 条门测试**依然全绿**。⇒ 门从门链掉线时,现有 19 条相关判据一条都不红。
//
// 为什么本文件住在**测试装配**而不是 `Editor.Tools.Gates`:
//   若做成 `GateWiringGate` 并接进 `RunAll`,它就在检查「自己是否在链上」——
//   **一旦它自身被删,它也不再运行**。「谁监督监督者」的递归必须有底,
//   而底只能落在**独立入口**(本测试)。这也是 C-1 被记成「测试缺口」而非「门缺口」的原因。
//
// ⚠️ **本文件只做解析,不下判决** —— 判决(手写清单)住 `gate_wiring_manifest_test.cs`。
//   两者分家的理由:解析器若连清单一起生成,「源 ↔ 清单」的比对就变恒等式
//   (零判别力;`TD-gate-01` 的修法条文里逐字禁了这条)。
//
// ⚠️ **刻意不用 Roslyn**(承 `AssemblyGates` b4/b6 同款口径:不引 analyzer),
//   故为**源文本级**解析:剥注释 + 剥字符串字面量 + 花括号配平取方法体。
//   已知漏报面:宏(`#if` 内外的调用都会被数进来)与扩展方法式调用 —— 均属
//   **误报方向(偏安全)**:只会让判据更严,不会让门静默掉线。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace DaYiJingCheng.Tests.Unit.Sim
{
    /// <summary>
    /// 从 `AssemblyGates.cs` 源文本抽取「每个强制入口实际调用了哪些门」,
    /// 并从 `Editor.Tools.Gates/` 目录抽取「声明了哪些 public static class」。
    /// <para>纯源文本解析(不加载程序集、不用 Roslyn)—— 只读文件,不写文件。</para>
    /// </summary>
    public static class GateChainSource
    {
        /// <summary>被解析的**门链宿主**源文件(相对工程根;`Assets/` 起算)。</summary>
        public const string HostSourceRelativePath = "Assets/Editor.Tools.Gates/AssemblyGates.cs";

        /// <summary>门类目录(相对工程根)。</summary>
        public const string GateDirRelativePath = "Assets/Editor.Tools.Gates";

        /// <summary>
        /// 三个**强制入口**的签名(逐字取自源文件,用 `MethodBody` 定位方法体)。
        /// <para>三者是三处「门真被强制执行」的地方:菜单 · 域重载 · 构建前门。
        /// 任一入口少一根线 = 该门在那条路径上失效。</para>
        /// </summary>
        public static readonly string[] EntryPointSignatures =
        {
            "public static List<string> RunAll()",
            "private static void RunMenu()",
            "public void OnPreprocessBuild(BuildReport report)",
        };

        /// <summary>入口的**短名**(与 <see cref="EntryPointSignatures"/> 同序)。</summary>
        public static readonly string[] EntryPointNames =
        {
            "RunAll",        // 菜单 / 域重载 / 构建前门三处共用的合流入口
            "RunMenu",       // 菜单项「大医精诚/Validation/Run Assembly Gates」
            "BuildGate",     // IPreprocessBuildWithReport.OnPreprocessBuild
        };

        // ── 判定名形态(与 AssemblyGates 的命名惯例一致)────────────────────
        // 自调(无限定):Check* / Run*  —— 其余(if/foreach/return/errs.AddRange)不收。
        private static readonly Regex SelfCallRegex =
            new Regex(@"(?<![.\w])(Check[A-Z]\w*|Run[A-Za-z]*)\s*\(", RegexOptions.Compiled);

        // 限定调:X.Y( —— X 以 Gate / Gates 结尾才算门类;X == AssemblyGates 时归一为自调。
        private static readonly Regex QualifiedCallRegex =
            new Regex(@"\b([A-Za-z_]\w*)\.([A-Za-z_]\w*)\s*\(", RegexOptions.Compiled);

        /// <summary>「门类名」的判据 —— 以 `Gate` **或** `Gates` 结尾。</summary>
        /// <remarks>
        /// <para>⚠️ **两个后缀都要收** —— `"DiagnosisBoundaryGates".EndsWith("Gate")` 是 **false**
        /// (最后一个字符是 `s`)。首版只判 `"Gate"` ⇒ 全部门类(`*Gates`)被漏光,
        /// 只剩 `FsWriteBoundaryGate` / `MedConsFloatGate` 这类单数名可见;
        /// 该 bug 由 `test_extractor_actuallyExtractsKnownMembers` 当场抓出(2026-10-21),
        /// 正是「反空转守卫」要防的形态 —— 解析面看起来在工作,实际只抽到一小撮。</para>
        /// <para>这道判据同时排除了 `PodTypeScanner` / `MixerAssetGenerator` /
        /// `RoslynAnalyzerLabel` / `PresentationDtoGuard` 这四个**不是门**的类
        /// (扫描库 / 工具 / 反射守卫),它们名字里没有 Gate 不是疏漏 —— 本来就不该进构建前门。</para>
        /// </remarks>
        public static readonly string[] GateClassNameSuffixes = { "Gate", "Gates" };

        /// <summary>工程根(含 `Assets/` 的那一层)= `Application.dataPath` 的上级。</summary>
        public static string ProjectRoot(string dataPath)
        {
            string root = Path.GetFullPath(Path.Combine(dataPath, ".."));
            return root;
        }

        // ══════════════════════════════════════════════════════════════════════
        // 一、词法预处理
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 剥**行注释** / **块注释** / **普通字符串内容** / **逐字字符串内容**。
        /// <para>偏移与换行**保持不变**(长度不变)⇒ 报错仍能指回原行号。</para>
        /// <para>为何必须剥字符串:门体内有大量错误报文(如 `"[b6] …new PayloadRef(…"`),
        /// 不剥会把**报文里的门名**当成真的调用 —— 那正是假绿的来源。</para>
        /// </summary>
        public static string StripCommentsAndLiterals(string src)
        {
            if (string.IsNullOrEmpty(src)) return string.Empty;

            char[] buf = src.ToCharArray();
            int i = 0;
            while (i < buf.Length)
            {
                char c = buf[i];

                // ── 行注释 // ──
                if (c == '/' && i + 1 < buf.Length && buf[i + 1] == '/')
                {
                    while (i < buf.Length && buf[i] != '\n') { buf[i] = ' '; i++; }
                    continue;
                }

                // ── 块注释 /* */ ──
                if (c == '/' && i + 1 < buf.Length && buf[i + 1] == '*')
                {
                    buf[i] = ' '; buf[i + 1] = ' '; i += 2;
                    while (i < buf.Length &&
                           !(buf[i] == '*' && i + 1 < buf.Length && buf[i + 1] == '/'))
                    {
                        if (buf[i] != '\n') buf[i] = ' ';
                        i++;
                    }
                    if (i + 1 < buf.Length) { buf[i] = ' '; buf[i + 1] = ' '; i += 2; }
                    continue;
                }

                // ── 逐字字符串 @"…"(其中 "" 为转义的引号)──
                if (c == '@' && i + 1 < buf.Length && buf[i + 1] == '"')
                {
                    buf[i] = ' '; buf[i + 1] = ' '; i += 2;
                    while (i < buf.Length)
                    {
                        if (buf[i] == '"')
                        {
                            if (i + 1 < buf.Length && buf[i + 1] == '"')
                            {
                                buf[i] = ' '; buf[i + 1] = ' '; i += 2; continue;
                            }
                            i++; break;
                        }
                        if (buf[i] != '\n') buf[i] = ' ';
                        i++;
                    }
                    continue;
                }

                // ── 普通字符串 "…" ──
                if (c == '"')
                {
                    i++;
                    while (i < buf.Length)
                    {
                        if (buf[i] == '\\') { buf[i] = ' '; i++; if (i < buf.Length) { buf[i] = ' '; i++; } continue; }
                        if (buf[i] == '"') { i++; break; }
                        if (buf[i] != '\n') buf[i] = ' ';
                        i++;
                    }
                    continue;
                }

                // ── 字符字面量 '…'(存在 '{' / '}' 被误算的风险,必须剥)──
                if (c == '\'')
                {
                    i++;
                    while (i < buf.Length)
                    {
                        if (buf[i] == '\\') { buf[i] = ' '; i++; if (i < buf.Length) { buf[i] = ' '; i++; } continue; }
                        if (buf[i] == '\'') { i++; break; }
                        buf[i] = ' ';
                        i++;
                    }
                    continue;
                }

                i++;
            }
            return new string(buf);
        }

        // ══════════════════════════════════════════════════════════════════════
        // 二、方法体定位(花括号配平)
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 取 <paramref name="signature"/> 对应方法的方法体(**含花括号**;找不到返回 <c>null</c>)。
        /// </summary>
        /// <param name="cleaned">必须是 <see cref="StripCommentsAndLiterals"/> 的产物 —— 否则引号里的
        /// 花括号会让配平错位。</param>
        /// <param name="signature">逐字签名(见 <see cref="EntryPointSignatures"/>)。</param>
        public static string MethodBody(string cleaned, string signature)
        {
            if (string.IsNullOrEmpty(cleaned) || string.IsNullOrEmpty(signature)) return null;

            int sig = cleaned.IndexOf(signature, StringComparison.Ordinal);
            if (sig < 0) return null;

            int open = cleaned.IndexOf('{', sig);
            if (open < 0) return null;

            int depth = 0;
            for (int i = open; i < cleaned.Length; i++)
            {
                if (cleaned[i] == '{') depth++;
                else if (cleaned[i] == '}')
                {
                    depth--;
                    if (depth == 0) return cleaned.Substring(open, i - open + 1);
                }
            }
            return null;   // 花括号不配平 = 源文件结构异常,交由调用方断言
        }

        // ══════════════════════════════════════════════════════════════════════
        // 三、抽取「某入口实际调用了哪些门」
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 抽取某个入口方法体内**实际出现的门调用**(归一化名,去重排序)。
        /// <para>归一化:① `AssemblyGates.CheckXxx(…)` ⇒ `CheckXxx`;
        /// ② `XxxGates.RunAll(…)` ⇒ `XxxGates.RunAll`;
        /// ③ 无限定 `CheckXxx(…)` / `Run*` ⇒ 原样。</para>
        /// </summary>
        /// <param name="cleaned">已剥注释与字符串的源码。</param>
        /// <param name="signature">入口签名。</param>
        /// <param name="bodyOut">方法体(供调用方做空体检测);找不到签名时为 <c>null</c>。</param>
        public static string[] InvokedMembers(string cleaned, string signature, out string bodyOut)
        {
            bodyOut = MethodBody(cleaned, signature);
            if (bodyOut == null) return Array.Empty<string>();

            var found = new SortedSet<string>(StringComparer.Ordinal);

            // 限定调用
            foreach (Match m in QualifiedCallRegex.Matches(bodyOut))
            {
                string owner = m.Groups[1].Value;
                string member = m.Groups[2].Value;

                if (owner == "AssemblyGates")
                {
                    // 自身方法:只收 Check*/Run* 形态(排除 Debug.Log / errs.AddRange 等)
                    if (member.StartsWith("Check", StringComparison.Ordinal) ||
                        member.StartsWith("Run", StringComparison.Ordinal))
                        found.Add(member);
                    continue;
                }

                if (IsGateClassName(owner))
                    found.Add(owner + "." + member);
            }

            // 无限定自身调用
            foreach (Match m in SelfCallRegex.Matches(bodyOut))
                found.Add(m.Groups[1].Value);

            return found.ToArray();
        }

        /// <summary>是否为「门类名」—— 以 <c>Gate</c> 或 <c>Gates</c> 结尾。</summary>
        public static bool IsGateClassName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            foreach (string suffix in GateClassNameSuffixes)
                if (name.EndsWith(suffix, StringComparison.Ordinal)) return true;
            return false;
        }

        // ══════════════════════════════════════════════════════════════════════
        // 四、目录内声明了哪些类
        // ══════════════════════════════════════════════════════════════════════

        private static readonly Regex DeclaredStaticClassRegex =
            new Regex(@"^\s*public\s+static\s+class\s+([A-Za-z_]\w*)", RegexOptions.Compiled | RegexOptions.Multiline);

        /// <summary>
        /// 抽取 <paramref name="gateDir"/> 下**全部** `public static class` 的类名(去重排序)。
        /// <para>⚠️ 这是**全量**面(含非门类)—— 用来断言「每个类都有判决」,
        /// 而不是只断言「门类都在链上」。后者会漏掉「新加一个门类但没人接」。</para>
        /// </summary>
        public static string[] DeclaredStaticClasses(string gateDir)
        {
            if (!Directory.Exists(gateDir)) return Array.Empty<string>();

            var names = new SortedSet<string>(StringComparer.Ordinal);
            foreach (string file in Directory.GetFiles(gateDir, "*.cs", SearchOption.TopDirectoryOnly))
            {
                string src;
                try { src = File.ReadAllText(file); }
                catch (IOException) { continue; }

                string cleaned = StripCommentsAndLiterals(src);
                foreach (Match m in DeclaredStaticClassRegex.Matches(cleaned))
                    names.Add(m.Groups[1].Value);
            }
            return names.ToArray();
        }

        /// <summary>目录内的**门类**(名字以 `Gate` 结尾者)—— <see cref="DeclaredStaticClasses"/> 的子集。</summary>
        public static string[] DeclaredGateClasses(string gateDir)
            => DeclaredStaticClasses(gateDir).Where(IsGateClassName).ToArray();

        // ══════════════════════════════════════════════════════════════════════
        // 五、一次性解出三入口(测试主用)
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 读宿主源文件并解出**全部三个入口**的调用集。
        /// </summary>
        /// <param name="repoRoot">工程根(含 `Assets/`)。</param>
        /// <param name="errors">解析失败项(文件缺失 / 签名找不到 / 方法体不配平)—— 非空即应判红。</param>
        /// <returns>入口短名 → 实际调用集。<c>RunAll</c> 命名**不带**限定前缀(自身方法)。</returns>
        public static Dictionary<string, string[]> ResolveEntryPoints(
            string repoRoot, List<string> errors)
        {
            var result = new Dictionary<string, string[]>(StringComparer.Ordinal);
            if (errors == null) errors = new List<string>();

            string path = Path.Combine(repoRoot, HostSourceRelativePath);
            if (!File.Exists(path))
            {
                errors.Add($"[gate-wiring] 宿主源文件不存在:{path} —— 门链的解析面丢了,判据不可判");
                return result;
            }

            string cleaned = StripCommentsAndLiterals(File.ReadAllText(path));

            for (int i = 0; i < EntryPointNames.Length; i++)
            {
                string name = EntryPointNames[i];
                string sig = EntryPointSignatures[i];

                string body = MethodBody(cleaned, sig);
                if (body == null)
                {
                    errors.Add($"[gate-wiring] 入口「{name}」的签名未找到或方法体不配平 —— " +
                               $"签名变了?清单须同批订正。签名: `{sig}`");
                    continue;
                }

                if (body.Trim() == "{}")
                {
                    errors.Add($"[gate-wiring] 入口「{name}」方法体为空 —— 空体 = 该入口啥也不跑");
                    continue;
                }

                result[name] = InvokedMembers(cleaned, sig, out _);
            }
            return result;
        }
    }
}
