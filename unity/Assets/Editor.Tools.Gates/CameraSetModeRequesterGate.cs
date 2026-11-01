// 2 相机 —— **生产调用方必须用双参 `SetMode(mode, requesterId)`**的静态门(2026-11-02)
//
// ── 它防的是什么 ────────────────────────────────────────────────────────────
// `ICameraRig` 有两个重载:
//   `SetMode(CameraMode mode)`                 —— 兼容重载,内部记 `requesterId = -1`(无请求方)
//   `SetMode(CameraMode mode, int requesterId)` —— 甲案入口,**请求方号参与裁决**
//
// 甲案(2026-11-01 用户裁定,ADR-020 Amendment C ②)的判据是
// 「**同一请求方**可以把自己开的档降回去」:
//
//     sameRequesterDowngrade = best.Mode == Explore
//                           && best.RequesterId == CurrentRequesterId
//                           && Mode != Explore
//
// ⇒ 生产调用方若走**单参**重载(或显式传 `-1`),`CurrentRequesterId` 记成 `-1`
// ⇒ 该判据对它**恒假** ⇒「只能由请求方退出」**静默失效**。
// **不崩、不红、夹具也不会说话** —— 表现形式是「玩家复活后相机卡在倒下档」这种
// 只能在真机上玩出来的现象(sprint-24 的 `main_menu` 型偶发红同族)。
//
// `ICameraRig.SetMode` 的 XML 注释已明写「生产调用方必须用带 requesterId 的重载」——
// **注释不是执行体**(假绿形态 F6「自述顶替门」)。本门是那句话的执行体。
//
// ── 判据 ────────────────────────────────────────────────────────────────────
// 扫 `Assets/**/*.cs`,**排除 `/Tests/`**,逐处 `.SetMode(` 检查:
//
//   **R1(arity)** —— 顶层实参个数 < 2 ⇒ 红。
//   **R2(值)**    —— 顶层实参个数 ≥ 2,但**最后一个实参**是字面量 `-1`
//                    (含 `requesterId: -1` 具名形)⇒ 红。
//                    ⚠️ 传 `-1` 与走单参重载**语义完全等价** —— 只查 arity 会漏掉这条
//                    静默路径,所以 R2 必须单列。
//
// **唯一豁免 = 接收者为 `_modeMachine` 的调用。**
// 理由:`CameraModeMachine` 是**档位的唯一写入点**(AC-2-17),只有 `CameraRig` 碰它;
// `CameraRig.SetMode(mode)`(兼容重载)的实现体必须给机器层一个值,那里 `-1` 是**正确的**。
// 豁免按**接收者表达式文本**判定(`_modeMachine`),不按文件 / 行号 —— 后者会被无关改动撞碎。
//
// ── 判据的已知边界(如实登记,不假装覆盖) ─────────────────────────────────
// · **不查值是否「正确」**:`rig.SetMode(mode, 7)` 里 `7` 是不是真系统号,本门判不了
//   (那需要一张系统号表 + 调用点语义)。本门只保证「**没有**把请求方号抹成 `-1`」。
// · **不查 `SetModeForTest`**:它是测试缝,且本门已排除 `/Tests/`;生产码用它是另一回事
//   (若将来出现,应另立判据)。
// · **字符串 / 注释已剥离**:解释「为什么禁止单参」的注释不会把本门弄红。
//
// 权威:ADR-020 Amendment C ② · AC-20-16 · `control-manifest.md` 控制器与相机节
//       · `design/gdd/camera-and-viewpoint.md` 的 `R-2-5` / `O-12`

using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace DaYiJingCheng.EditorTools.Gates
{
    /// <summary>生产调用方必须用双参 `SetMode(mode, requesterId)` 的静态门。</summary>
    public static class CameraSetModeRequesterGate
    {
        /// <summary>扫描根(相对工程根 `unity/`)。</summary>
        public const string ScanRootRelative = "Assets";

        /// <summary>被判的调用标记(单一出处;判据与报文都读它)。</summary>
        public const string CallMarker = ".SetMode(";

        /// <summary>
        /// 唯一豁免的接收者 —— `CameraModeMachine` 的字段名(`CameraRig` 独占访问)。
        /// ⚠️ 豁免按**表达式文本**判定,不按文件 / 行号。
        /// </summary>
        public const string ExemptReceiver = "_modeMachine";

        /// <summary>
        /// 跑本门。
        /// </summary>
        /// <param name="projectRoot">工程根(`unity/`;`"."` = 当前目录)。</param>
        /// <returns>命中即一条错误(含 `file:line`);空 = 通过。</returns>
        public static List<string> RunAll(string projectRoot)
        {
            var errs = new List<string>();
            string root = Path.Combine(projectRoot, ScanRootRelative);
            if (!Directory.Exists(root))
            {
                errs.Add($"[相机·SetMode] 扫描根不存在:{root} —— 门无从执行");
                return errs;
            }

            foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string normalized = file.Replace('\\', '/');
                if (normalized.Contains("/Tests/")) continue;

                string text;
                try { text = File.ReadAllText(file); }
                catch (IOException ex)
                {
                    errs.Add($"[相机·SetMode] 读不了 {normalized}:{ex.Message}");
                    continue;
                }

                errs.AddRange(CheckSource(normalized, text));
            }

            return errs;
        }

        /// <summary>
        /// 单文件判据(纯函数;测试直接喂合成源码,无需造目录)。
        /// </summary>
        /// <param name="displayPath">报文里显示的文件名(相对路径)。</param>
        /// <param name="text">文件原文。</param>
        public static List<string> CheckSource(string displayPath, string text)
        {
            var errs = new List<string>();
            string code = StripCommentsAndStrings(text);

            int index = 0;
            while ((index = code.IndexOf(CallMarker, index, System.StringComparison.Ordinal)) >= 0)
            {
                int openParen = index + CallMarker.Length - 1;

                string receiver = ReadReceiver(code, index);
                if (receiver == ExemptReceiver)
                {
                    index = openParen + 1;
                    continue;
                }

                int closeParen = MatchCloseParen(code, openParen);
                if (closeParen < 0) break;   // 括号不平衡 —— 那是编译器的活,门不重复报

                var args = SplitTopLevelArgs(code, openParen + 1, closeParen);
                int line = LineOf(code, index);

                if (args.Count < 2)
                {
                    errs.Add(
                        $"[相机·SetMode] {displayPath}:{line} 单个实参 —— " +
                        "生产调用方**必须**用 `SetMode(mode, requesterId)`(ADR-020 Amendment C ② / AC-20-16)。" +
                        "单参重载把 `requesterId` 记成 `-1`,甲案的「同一请求方可降档」判据对它**恒假** ⇒ " +
                        "「只能由请求方退出」**静默失效**(不崩、不红,表现为相机卡在某个档出不来)。" +
                        "请传入真实系统号(10 急救 / 29 死亡过渡 / 39 脉案…)。");
                }
                else
                {
                    string last = args[args.Count - 1];
                    if (IsMinusOneLiteral(last))
                    {
                        errs.Add(
                            $"[相机·SetMode] {displayPath}:{line} `requesterId` 传了字面量 `-1` —— " +
                            "与走单参重载**语义等价**(同样让甲案判据恒假)。" +
                            "生产调用方必须传真实系统号(ADR-020 Amendment C ② / AC-20-16)。" +
                            "⚠️ 唯一合法处 = `CameraRig.SetMode(mode)` 兼容重载的实现体,它调的是 " +
                            $"`{ExemptReceiver}`(本门已豁免接收者 `{ExemptReceiver}`)。");
                    }
                }

                index = closeParen + 1;
            }

            return errs;
        }

        /// <summary>取 `.SetMode(` 之前那段接收者表达式(回溯到非标识符字符为止)。</summary>
        /// <remarks>
        /// ⚠️ **区间是闭区间** `[start+1, end]` ⇒ Substring 长度 = `end - start`。
        /// 首版写成 `end - start - 1`(少一个字符)⇒ 接收者恒缺尾字母 ⇒ `_modeMachine`
        /// 被读成 `_modeMachin` ⇒ 豁免失效 ⇒ **门的合法样本被自己判红**。
        /// 该 bug 是在**真树上预演判据**时抓到的(见证据件 §12),不是编译器抓的。
        /// </remarks>
        private static string ReadReceiver(string code, int markerIndex)
        {
            int end = markerIndex - 1;   // 接收者的**最后一个**字符
            if (end < 0) return string.Empty;
            int start = end - 1;
            while (start >= 0 && IsIdentChar(code[start])) start--;
            return code.Substring(start + 1, end - start);   // 闭区间 ⇒ 长度 = end - start
        }

        private static bool IsIdentChar(char c)
            => char.IsLetterOrDigit(c) || c == '_' || c == '.';

        private static int MatchCloseParen(string code, int openParen)
        {
            int depth = 0;
            for (int i = openParen; i < code.Length; i++)
            {
                if (code[i] == '(') depth++;
                else if (code[i] == ')')
                {
                    depth--;
                    if (depth == 0) return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// 按**顶层逗号**切实参(括号 / 方括号 / 花括号内的逗号不算)。
        /// ⚠️ **刻意不跟踪 `<` / `>`** —— 那会把 `a > b` 当成括号层,导致实参切错
        /// (切错 = 可能误报 = 门在正确代码上红)。实参里出现泛型实参的概率远低于比较运算。
        /// </summary>
        private static List<string> SplitTopLevelArgs(string code, int start, int endExclusive)
        {
            var args = new List<string>();
            int depth = 0;
            int argStart = start;
            for (int i = start; i < endExclusive; i++)
            {
                char c = code[i];
                if (c == '(' || c == '[' || c == '{') depth++;
                else if (c == ')' || c == ']' || c == '}') depth--;
                else if (c == ',' && depth == 0)
                {
                    args.Add(code.Substring(argStart, i - argStart));
                    argStart = i + 1;
                }
            }
            string tail = code.Substring(argStart, endExclusive - argStart);
            if (args.Count > 0 || tail.Trim().Length > 0) args.Add(tail);
            return args;
        }

        /// <summary>最后一个实参是否就是字面量 `-1`(允许 `requesterId: -1` 具名形)。</summary>
        private static bool IsMinusOneLiteral(string arg)
        {
            if (arg == null) return false;
            string a = Regex.Replace(arg, @"\s+", string.Empty);
            int colon = a.IndexOf(':');
            if (colon >= 0) a = a.Substring(colon + 1);   // 去掉 `requesterId:`
            return a == "-1";
        }

        private static int LineOf(string code, int index)
        {
            int line = 1;
            for (int i = 0; i < index && i < code.Length; i++)
                if (code[i] == '\n') line++;
            return line;
        }

        /// <summary>
        /// 把注释与字符串字面量替换成空格(保留换行,行号仍可对)。
        /// 与 `HarvestGateAllowGate` / `ForagingFloatGate` 同族的轻量剥离。
        /// </summary>
        private static string StripCommentsAndStrings(string src)
        {
            string noBlock = Regex.Replace(src, @"/\*.*?\*/", " ", RegexOptions.Singleline);
            string noLine = Regex.Replace(noBlock, @"//[^\n]*", " ");
            return Regex.Replace(noLine, @"""(\\.|[^""\\])*""", "\"\"");
        }
    }
}
