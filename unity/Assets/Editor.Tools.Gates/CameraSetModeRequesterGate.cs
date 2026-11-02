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
// 权威:ADR-020 Amendment C ② · AC-ADR020-16 · `control-manifest.md` 控制器与相机节
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
                        "生产调用方**必须**用 `SetMode(mode, requesterId)`(ADR-020 Amendment C ② / AC-ADR020-16)。" +
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
                            "生产调用方必须传真实系统号(ADR-020 Amendment C ② / AC-ADR020-16)。" +
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

        // ═══════════════════════════════════════════════════════════════════════
        // 档位请求**覆盖**判据(camera-viewpoint/story-007 · 2026-11-02)
        // ───────────────────────────────────────────────────────────────────────
        // ⚠️ **本段只防「绕过」,不防「没发」** —— 这是设计上的已知边界,不许读成更强。
        //   `EmergencyChannelDriver` 的调用是 `rig.SetMode(mode, RequesterId)`,**mode 是形参**,
        //   字面量在调用方 `RequestCameraMode(CameraMode.Treatment)` 处 ⇒ 静态扫描要么抓不到、
        //   要么依赖脆弱的局部数据流(改个辅助方法就失配)。
        //   ⇒ B2 只能证明「**有人声明要发它**」,证明不了「**它真的会被发**」。
        //   真正的「该发没发」判据 = 动态矩阵夹具(camera_mode_request_matrix_test.cs)。
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>档位表真源(相对**仓库根**;门会从工程根向上找)。</summary>
        public const string ModeTableRelative = "docs/architecture/camera-mode-requests.yaml";

        /// <summary>表里一行的最小形状(`status: excluded` 的行只有 id/requester/trigger)。</summary>
        public sealed class ModeTableRow
        {
            public string Id = "";
            public int Requester = -1;
            public string Type = "";
            public string Mode = "";
            public string Evidence = "";
            public string Status = "";
        }

        /// <summary>
        /// **B1 + B2**:表 ↔ 代码的覆盖判据。
        /// <list type="bullet">
        ///   <item>B1:每个 active 行的请求方类型必须存在,且声明 `const int RequesterId = &lt;系统号&gt;`;</item>
        ///   <item>B2:该行的 `mode` 名必须出现在该类型的源码里(**声明级**判据,见段首边界说明);</item>
        ///   <item>B3:`evidence` 文件必须存在,且**文件内出现该行的 id**(弱链接 —— 防「随便挂个文件」)。</item>
        /// </list>
        /// </summary>
        /// <param name="projectRoot">工程根(`unity/`;`"."` = 当前目录)。</param>
        public static List<string> RunModeRequestTable(string projectRoot)
        {
            var errs = new List<string>();

            string repoRoot = FindRepoRoot(projectRoot);
            if (repoRoot == null)
            {
                errs.Add($"[相机·档位表] 未能定位仓库根(向上找不到 {ModeTableRelative})—— 门无从执行");
                return errs;
            }

            string tablePath = Path.Combine(repoRoot, ModeTableRelative.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(tablePath))
            {
                errs.Add($"[相机·档位表] 真源不存在:{ModeTableRelative} —— 表没了,覆盖判据就没有宾语");
                return errs;
            }

            List<ModeTableRow> rows = ParseModeTable(File.ReadAllLines(tablePath), errs);
            if (errs.Count > 0) return errs;

            int active = 0;
            foreach (ModeTableRow row in rows)
            {
                if (row.Status == "excluded") continue;
                if (row.Status != "active")
                {
                    errs.Add($"[相机·档位表] {row.Id}:`status` 必须是 active 或 excluded(实得 '{row.Status}')");
                    continue;
                }
                active++;

                // ── B1:请求方类型存在 + `const int RequesterId` 对得上 ──────────────
                string carrier = FindProductionFile(projectRoot, row.Type + ".cs");
                if (carrier == null)
                {
                    errs.Add($"[相机·档位表] {row.Id}:请求方类型 `{row.Type}` 在 `Assets/**` 下找不到 " +
                             "(测试除外)—— 表里有、代码里没有");
                    continue;
                }
                string carrierName = Path.GetFileNameWithoutExtension(carrier);
                string carrierText = File.ReadAllText(carrier);
                string expected = $"const int RequesterId = {row.Requester};";

                if (carrierName != row.Type)
                {
                    errs.Add($"[相机·档位表] {row.Id}:命中文件 `{carrierName}.cs` 与表内 `type: {row.Type}` 不符 " +
                             "(一对多或同名文件歧义)");
                }
                else if (!Regex.IsMatch(carrierText, @"const\s+int\s+RequesterId\s*=\s*" + row.Requester + @"\s*;"))
                {
                    errs.Add($"[相机·档位表] {row.Id}:`{row.Type}` 未声明 `{expected}` —— " +
                             "系统号不是常量 ⇒ 甲案「同一请求方可降档」无从判定,且表与代码对不上");
                }

                // ── B2:mode 名在该类型源码里出现(声明级)──────────────────────────
                if (!Regex.IsMatch(carrierText, @"CameraMode\." + Regex.Escape(row.Mode) + @"\b"))
                {
                    errs.Add($"[相机·档位表] {row.Id}:`{row.Type}` 的源码里找不到 `CameraMode.{row.Mode}` —— " +
                             "表声明要发这个档,代码里连声明都没有。\n" +
                             "⚠️ 反向不成立:找到了也不代表**真的会发**(见本段边界说明)");
                }

                // ── B3:evidence 存在且点名本行 id ─────────────────────────────────
                if (string.IsNullOrEmpty(row.Evidence))
                {
                    errs.Add($"[相机·档位表] {row.Id}:缺 `evidence` —— 没有载体就没有判别力");
                }
                else
                {
                    string evPath = Path.Combine(repoRoot, row.Evidence.Replace('/', Path.DirectorySeparatorChar));
                    if (!File.Exists(evPath))
                    {
                        errs.Add($"[相机·档位表] {row.Id}:`evidence` 指向的文件不存在:{row.Evidence}");
                    }
                    else if (!File.ReadAllText(evPath).Contains(row.Id))
                    {
                        errs.Add($"[相机·档位表] {row.Id}:`{row.Evidence}` 里**没有出现 `{row.Id}`** —— " +
                                 "载体文件存在但没点名这一行(弱链接失效)");
                    }
                }
            }

            if (active != 6)
            {
                errs.Add($"[相机·档位表] active 行数 = {active},期望 **6** —— " +
                         "改表行数须同步 `R-2-5`、本门的期望值与矩阵夹具(三处缺一即漂移)");
            }

            return errs;
        }

        /// <summary>极简行解析(只认 `- id:` 起行 + `key: value`;足够本表,不引入 YAML 依赖)。</summary>
        private static List<ModeTableRow> ParseModeTable(string[] lines, List<string> errs)
        {
            var rows = new List<ModeTableRow>();
            ModeTableRow current = null;

            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#") || line == "rows:") continue;

                if (line.StartsWith("- id:"))
                {
                    current = new ModeTableRow { Id = TrimValue(line.Substring(5)) };
                    rows.Add(current);
                    continue;
                }
                if (current == null) continue;

                int colon = line.IndexOf(':');
                if (colon <= 0) continue;
                string key = line.Substring(0, colon).Trim();
                string value = TrimValue(line.Substring(colon + 1));

                switch (key)
                {
                    case "requester":
                        if (!int.TryParse(value, out int req))
                            errs.Add($"[相机·档位表] {current.Id}:`requester` 不是整数('{value}')");
                        else current.Requester = req;
                        break;
                    case "type": current.Type = value; break;
                    case "mode": current.Mode = value; break;
                    case "evidence": current.Evidence = value; break;
                    case "status": current.Status = value; break;
                }
            }

            if (rows.Count == 0)
                errs.Add("[相机·档位表] 表里一行都没解析出来 —— 格式坏了,或解析器与表格式脱节");
            return rows;
        }

        private static string TrimValue(string s)
        {
            s = s.Trim();
            if (s.Length >= 2 && s[0] == '"' && s[s.Length - 1] == '"') s = s.Substring(1, s.Length - 2);
            return s.Trim();
        }

        private static string FindRepoRoot(string projectRoot)
        {
            var dir = new DirectoryInfo(Path.GetFullPath(projectRoot));
            for (int i = 0; i < 6 && dir != null; i++, dir = dir.Parent)
            {
                if (File.Exists(Path.Combine(dir.FullName,
                        ModeTableRelative.Replace('/', Path.DirectorySeparatorChar))))
                    return dir.FullName;
            }
            return null;
        }

        /// <summary>在 `Assets/**` 下按文件名找生产源码(测试除外)。</summary>
        private static string FindProductionFile(string projectRoot, string fileName)
        {
            string root = Path.Combine(projectRoot, ScanRootRelative);
            if (!Directory.Exists(root)) return null;

            foreach (string file in Directory.GetFiles(root, fileName, SearchOption.AllDirectories))
            {
                if (file.Replace('\\', '/').Contains("/Tests/")) continue;
                return file;
            }
            return null;
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
