// S12-9 · story-002 —— 53 静态面门(AC-53-03 零浮点 + AC-53-12 31 零引用)。
//
// 权威来源:
//   AC-53-03(BLOCKING):53 源文件**零浮点字面量**(静态检查入构建;门 A / ADR-006)。
//   AC-53-12(BLOCKING):53 程序集引用集零 31(声誉与名声)符号 —— 53 不知 31 存在。
//   卡面 story-002 Control Manifest Forbidden:float/double 字面量;31 的任何符号引用。
//   先例:InputBoundaryGates(注释剥离后 token 扫描;偏安全,非可达性精确分析)。
//
// ⚠️ 扫描面 = `unity/Assets/Sim/Consequences/**/*.cs`(53 的实现落点 —— AC-53-12 载体 =
//   Sim 程序集内命名空间,无独立 asmdef 可断言 ⇒ 源文件面 + 引用集双断言)。
//
// ⚠️ 2026-10-15 返工轮(`/code-review` 裁定 CHANGES REQUIRED · 原件
//   production/qa/evidence/review-medcons-story-002-2026-10-15.md):
//   F 改递归扫描(原 TopDirectoryOnly —— 53 实现面若分子目录即漏扫)·
//   R-3 补**浮点字面量**正则(原只扫标识符 token ⇒ `1.5`/`0.5f`/`3e10` 一个都抓不到,
//     而 AC-53-03 原文判的就是「字面量」)·
//   H token 单一真源留本门(测试改读本数组,不再自带字面量;匹配口径统一
//     `\b{tok}\b` + IgnoreCase)。
//   ⚠️ H 的**测试侧引用无需改 asmdef** —— EditMode.asmdef 早已以 GUID
//     `09b3130c3cce749c78793250d6d191f4` 引用 Editor.Tools.Gates(返工期实测:按名补一条
//     反而触发 "Assembly has duplicate references" 编译红)。此前侦察只读了名称列表、
//     漏看 GUID 形引用,是**侦察错误**,不是缺口。
//
// ⚠️ **R-3 残余缺口(如实登记,不借绿)**:源级判据看不见 IL 层的 `conv.r4` /
//   `conv.r8` —— 常量折叠或算术产生的非字面量浮点不经源码文本。本仓的更强先例是
//   `EcozoneIntegerGates`(AC-6-22)走 Mono.Cecil IL 级
//   (`ForbiddenFloatOpcodes = {Conv_R4, Conv_R8, Ldc_R4, Ldc_R8}` + float/double 类型面)。
//   **本批刻意不引入 Cecil 扫描面** —— 53 当前零浮点算术(U 是 Q16.16 raw long 整数比较),
//   源级已覆盖其全部数值路径;升级触发条件 = 53 数值轮引入任何可能产生浮点的算术时,
//   届时按 `EcozoneIntegerGates` 形状扩列。
//
// ⚠️ token 扫描偏安全口径:注释剥离后命中禁名 token 即红(含字符串字面量 ——
//   53 面合法地不出现这些词;误报面 = 极小,漏报面 = 零)。

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace DaYiJingCheng.EditorTools.Gates
{
    /// <summary>53(医疗后果)静态面门:零浮点 token + 零 31 符号。</summary>
    public static class MedConsFloatGate
    {
        /// <summary>扫描目录(相对工程根 `unity/`;53 实现落点,单一出处)。</summary>
        public const string ConsequencesDirRelative = "Assets/Sim/Consequences";

        /// <summary>浮点类禁名 token(AC-53-03;词边界 —— `ToFloat` 这类成员名不误伤是
        /// **刻意不追求**的:53 面连 `ToFloat` 都不该有,偏安全口径)。</summary>
        public static readonly string[] FloatTokens =
        {
            "float", "double", "Single", "Double", "decimal", "Decimal",
        };

        /// <summary>浮点**字面量**形态(R-3 返工:AC-53-03 原文判的就是字面量,标识符 token
        /// 抓不到 `1.5` / `0.5f` / `3e10` / `2m`)。三类形态:
        /// <list type="number">
        /// <item>定点小数 `1.5` / `.5`(可选后缀 f/F/d/D/m/M)</item>
        /// <item>带后缀无点小数 `0.5f`(已由 ① 覆盖)/ `2m` / `3d`(**无小数点**的纯后缀形)</item>
        /// <item>科学计数 `3e10` / `1.5E-4` / `2f` 组合</item>
        /// </list>
        /// <para>⚠️ `(?:^|[^\w.])` 前视 + `(?=$|[^\w.])` 后视:不切断标识符
        /// (如 `a1.5` 不是合法 C#,但 `x.5` 是成员访问 ⇒ 后视的 `.` 排除项防误判),
        /// 也不把版本号 `1.5.2` 的小数点当字面量起点。</para>
        /// <para>⚠️ N-6(2026-10-16 重评返工):`\d+(?:_\d+)*` 吃 C# 7.0+ **数字分隔符** ——
        /// 原 `\d+` 漏 `1_000f` / `1_000.5` 两种合法浮点字面量(源级内、可零成本补)。</para>
        /// <para>⚠️ N-7(2026-10-16 重评返工·**登记不修**):<see cref="StripStrings"/> 只认
        /// `"` 起止,不识 `@"…"`(verbatim,其中 `""` 是转义引号)/ `$"…"`(插值)/ `'…'`(char)。
        /// 若 53 源出现 verbatim 字符串,扫描器会把第一个 `"` 当结束 ⇒ 后续真实浮点字面量
        /// 可能被剥掉(漏报)。当前 3 个文件无此形态 ⇒ INFO,与 R-3 已登记的 IL 级缺口同族,
        /// 一并归入「源级判据边界」清单,不借绿。</para></summary>
        public static readonly Regex[] FloatLiteralPatterns =
        {
            // ① 定点小数:1.5 / .5 / 0.5 / 12.0(可选后缀)
            new Regex(@"(?:^|[^\w.])\d*(?:_\d+)*\.\d+(?:_\d+)*(?:[fFdDmM])?(?=$|[^\w.])",
                      RegexOptions.Compiled),
            // ② 无小数点纯后缀:2m / 3d / 5f / 7F(整数面浮点后缀 —— C# 合法浮点字面量)
            new Regex(@"(?:^|[^\w.])\d+(?:_\d+)*[fFdDmM](?=$|[^\w.])",
                      RegexOptions.Compiled),
            // ③ 科学计数:3e10 / 1.5E-4 / 2e+5(可选后缀)
            new Regex(@"(?:^|[^\w.])(?:\d+(?:_\d+)*\.?\d*|\.\d+)[eE][+-]?\d+(?:[fFdDmM])?(?=$|[^\w.])",
                      RegexOptions.Compiled),
        };

        /// <summary>31(声誉与名声)类禁名 token(AC-53-12;声誉/信任/累积分语义族)。</summary>
        public static readonly string[] ReputationTokens =
        {
            "Reputation", "Trust", "Favor", "Renown", "Standing", "Prestige",
        };

        /// <summary>累积分类禁名 token(AC-53-11 同源:零声誉/信任值/累积分呈现)。</summary>
        public static readonly string[] ScoreTokens = { "ScoreAccum", "ScorePool", "CreditScore" };

        /// <summary>
        /// 跑门(聚合返回违例行;空集 = 绿)。
        /// </summary>
        /// <param name="unityProjectRoot">工程根(含 `Assets/`;通常 = Application.dataPath 的上级)。</param>
        public static List<string> RunAll(string unityProjectRoot)
        {
            var errs = new List<string>();
            string dir = Path.Combine(unityProjectRoot, ConsequencesDirRelative);
            if (!Directory.Exists(dir))
            {
                errs.Add($"[medcons] 扫描目录不存在:{dir}(53 实现落点被移?门须同步改口径)");
                return errs;
            }

            // F(2026-10-15 返工):递归扫描。原 TopDirectoryOnly —— 53 实现面若分子目录
            // (如 Consequences/Data/、Consequences/Judge/)即整支漏扫,门静默变绿。
            // 2026-10-16 重评返工:逐文件扫描拆出 ScanFile —— RunAll 本体 49 行超 40 行标准。
            foreach (string file in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
                errs.AddRange(ScanFile(file));
            return errs;
        }

        /// <summary>单文件扫描(两份扫描面 + 三类禁项;空集 = 该文件绿)。</summary>
        private static List<string> ScanFile(string file)
        {
            var errs = new List<string>();
            string source = File.ReadAllText(file);
            string rel = Path.GetFileName(file);

            // 两份扫描面(刻意不同,各有判据):
            //  · token 面 = 仅剥注释、**保留字符串**(偏安全 —— 53 面连字符串里出现
            //    "Reputation" / "float" 都可疑;误报面极小,漏报面零)。
            //  · 字面量面 = 注释 + 字符串**全剥**(`"F-53.2"` 这类文档引用不是 C# 浮点
            //    字面量;若保留字符串,本门的异常消息自身就会被 ① 号正则判红 ——
            //    返工期实测命中一次,已修)。
            string tokenText = StripComments(source);

            foreach (string tok in FloatTokens)
                if (Regex.IsMatch(tokenText, $@"\b{Regex.Escape(tok)}\b"))
                    errs.Add($"[medcons·AC-53-03] {rel} 出现浮点 token「{tok}」—— " +
                             "53 源零浮点字面量(静态检查入构建;U 以 Q16.16 raw long 承载)");

            // R-3:浮点**字面量**形态(AC-53-03 原文判据面)。
            foreach (string hit in FindFloatLiterals(source))
                errs.Add($"[medcons·AC-53-03] {rel} 出现浮点**字面量**「{hit}」—— " +
                         "AC-53-03 原文判「零浮点字面量」;U 以 Q16.16 raw long 承载," +
                         "档切分走整数比较(ADR-006 边界)");

            foreach (string tok in ReputationTokens)
                if (ContainsToken(tokenText, tok))
                    errs.Add($"[medcons·AC-53-12] {rel} 出现 31 类 token「{tok}」—— " +
                             "53 不知 31 存在(声誉/信任类符号零命中)");

            foreach (string tok in ScoreTokens)
                if (ContainsToken(tokenText, tok))
                    errs.Add($"[medcons·AC-53-12] {rel} 出现累积分 token「{tok}」—— " +
                             "53 是水龙头,单向流只到世界流(AC-53-11 同源)");
            return errs;
        }

        /// <summary>token 命中判定(H 返工:单一匹配口径 —— 词边界 + 大小写不敏感)。</summary>
        /// <remarks>
        /// <para>原实现用 <c>text.Contains(tok)</c>(序数、大小写敏感):① 会把
        /// <c>some_Reputation_x</c> 这类**子串**命中判红(过宽);② 同时漏掉
        /// <c>reputation</c> 小写形(过窄)。两侧都不对。</para>
        /// <para>本方法是**单一真源** —— 测试的 AC-53-12 断言读
        /// <see cref="ReputationTokens"/> / <see cref="ScoreTokens"/> 而非自带字面量
        ///(原双真源:门集与测试集互不为超集,可各绿而语义分叉)。</para>
        /// </remarks>
        public static bool ContainsToken(string text, string token)
            => Regex.IsMatch(text, $@"\b{Regex.Escape(token)}\b", RegexOptions.IgnoreCase);

        /// <summary>
        /// 源文本中的浮点**字面量**命中清单(R-3;纯函数 —— 供测试直驱,反空转)。
        /// </summary>
        /// <remarks>
        /// <para>与 <see cref="RunAll"/> 走**同一条管线**:剥注释 → 剥字符串 → 三形态正则。
        /// 空清单 = 该源文本无浮点字面量。</para>
        /// <para>⚠️ 剥字符串是必需的:`"F-53.2"` / `"ADR-006.3"` / `"1.5"` 这类文档引用与
        /// 版本串不是 C# 浮点字面量。返工期实测:不剥时,本门自身的异常消息就被 ① 号正则
        /// 判红(自触门)。</para>
        /// </remarks>
        public static List<string> FindFloatLiterals(string source)
        {
            var hits = new List<string>();
            if (string.IsNullOrEmpty(source)) return hits;
            string literalText = StripStrings(StripComments(source));
            foreach (Regex rx in FloatLiteralPatterns)
                // 2026-10-16 重评返工:取**全部**匹配而非首个 —— 原用 rx.Match,
                // 同形态多个字面量时命中清单不完整(不影响门的布尔结论,损失诊断完备性)。
                foreach (Match m in rx.Matches(literalText))
                    hits.Add(m.Value.Trim());
            return hits;
        }

        /// <summary>注释剥离(// 与 /* */;字符串字面量保留 —— 偏安全)。</summary>
        private static string StripComments(string source)
        {
            var sb = new StringBuilder(source.Length);
            bool inLine = false, inBlock = false, inString = false;
            for (int i = 0; i < source.Length; i++)
            {
                char c = source[i];
                char next = i + 1 < source.Length ? source[i + 1] : '\0';

                if (inLine)
                {
                    if (c == '\n') { inLine = false; sb.Append(c); }
                    continue;
                }
                if (inBlock)
                {
                    if (c == '*' && next == '/') { inBlock = false; i++; }
                    continue;
                }
                if (inString)
                {
                    sb.Append(c);
                    if (c == '\\') { sb.Append(next); i++; }
                    else if (c == '"') inString = false;
                    continue;
                }
                if (c == '/' && next == '/') { inLine = true; i++; continue; }
                if (c == '/' && next == '*') { inBlock = true; i++; continue; }
                if (c == '"') inString = true;
                sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>字符串字面量剥离(内容替换为单空格,保位不保长 —— 字面量扫描面前置)。</summary>
        /// <remarks>
        /// <para>⚠️ 只用于**浮点字面量**扫描面:`"F-53.2"` / `"ADR-006.3"` / `"1.5"` 这类
        /// 文档引用与版本串不是 C# 浮点字面量。返工期实测:不剥字符串时,本门自身的异常
        /// 消息就被 ① 号正则判红(自触门)。</para>
        /// <para>⚠️ 字符串**内容**不参与 token 扫描面的判定(该面刻意保留字符串,偏安全)。</para>
        /// </remarks>
        private static string StripStrings(string commentFreeSource)
        {
            var sb = new StringBuilder(commentFreeSource.Length);
            bool inString = false;
            for (int i = 0; i < commentFreeSource.Length; i++)
            {
                char c = commentFreeSource[i];
                if (inString)
                {
                    if (c == '\\') { sb.Append(' '); i++; }
                    else if (c == '"') { inString = false; sb.Append(c); }
                    else sb.Append(' ');
                    continue;
                }
                if (c == '"') inString = true;
                sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
