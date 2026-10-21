// TD-testasm-01 · PlayMode 装配的 `UnityEditor` 裸用 —— 机械门。
//
// 立门缘由(2026-10-21 · S17-6 接线轮):IL2CPP 腿**首跑构建失败**,
// 48 errors / 15 条 unique 全是同一个:
//
//     error CS0234: The type or namespace name 'AssetDatabase' does not exist
//                   in the namespace 'UnityEditor'
//
// 根因**不在当轮改动**,在 4 个**既有** PlayMode 测试文件:
//   · SkeuomorphicUI/sdf_font_render_path_probe_test.cs   (2026-10-13 入库)
//   · SkeuomorphicUI/skeuo_stylesheet_mount_probe_test.cs (2026-10-13)
//   · SkeuomorphicUI/sdf_font_coverage_probe_test.cs      (2026-10-13)
//   · SkeuomorphicUI/casebook_reading_inline_test.cs      (2026-10-14)
// 它们裸用 `UnityEditor.AssetDatabase` 且**零 `#if UNITY_EDITOR` 守卫**。
//
// **机制**:`Assets/Tests/PlayMode/PlayMode.asmdef` 的 `includePlatforms: []`
// ⇒ 该装配**进 player**;编辑器里 `UnityEditor` 存在、编得过;
// **player 构建时该命名空间不存在** ⇒ 装配编不过 ⇒ 整个 player 出不来。
//
// **为什么必须立门**:这 4 件自 2026-10-13 起让 IL2CPP 腿**结构性坏**了 **8 天**,
// 而期间**每一次「全量 EditMode 绿」都不矛盾** —— `Sim.Contracts.Tests` 的
// `includePlatforms: ["Editor"]`,**EditMode 树根本不含这些文件**。
// 唯一的发现途径是「跑一次 IL2CPP 构建」= 数十分钟。⇒ 靠构建失败发现 = 结构性代价。
//
// ⚠️ **扫描面 = `Assets/Tests/PlayMode/**/*.cs`(递归)**,刻意只扫这一棵树:
//   · `Assets/Tests/EditMode/` 的装配 `includePlatforms: ["Editor"]` ⇒ **不进 player**,
//     裸用 `UnityEditor` 完全合法(全仓 100+ 处),纳入只会逼出「整目录豁免」;
//   · `Assets/Editor.Tools.*/` 同理(ADR-025 §①:Editor.Tools 族 `includePlatforms: ["Editor"]`)。
//   ⇒ 本门只答「PlayMode 测试树里的 UnityEditor 引用会不会打死 player 构建」。
//
// ⚠️ **判据是「代码引用」不是「文本出现」(实测教训)**:
//   首轮普查 `grep -rln 'UnityEditor' Assets/Tests/PlayMode/` 得 **6** 个文件,
//   但其中 **2 个从不致命** ——
//     · `Audio/stethoscope_preload_loadstate_test.cs` 本来就有 2 处守卫;
//     · `direct_read_channel_phase_test.cs` 的唯一命中在**注释**里(`:56`),
//       不是代码引用。
//   ⇒ 若判据写成「扫到 UnityEditor 就报」,会**误报 2 条**。本门因此:
//     ① 先剥注释(`StripComments`,与 `FsWriteBoundaryGate` 同款);
//     ② 只认**限定引用形态**(`UnityEditor.` 后接标识符 / `using UnityEditor`);
//     ③ 逐行判「该行是否落在 `#if UNITY_EDITOR` 的活跃区内」。
//
// ⚠️ **守卫判据 = 条件编译的活跃区**,不是「文件里出现过 `#if UNITY_EDITOR`」:
//   文件级「有守卫」会让「守卫写在一处、裸用在另一处」逃逸 —— 正是那 4 件的形态
//   (`casebook_reading_inline_test.cs` 修前有 0 处,但同目录的兄弟件有 2–3 处)。
//   ⇒ 本门按 `#if/#elif/#else/#endif` 维护**行级活跃栈**,只对「活跃区内」的引用报红。
//
// ⚠️ **已知盲区(登记,不借绿)**:
//   ① `#if !UNITY_EDITOR ... #else <裸用> #endif` 的 `#else` 分支 = 编辑器侧活跃,
//      本门**正确放行** —— 那不是 player 路径,是对的;
//   ② 反射字符串形态(`Type.GetType("UnityEditor.X, UnityEditor")`)看不见;
//      但那种写法在 player 里是**运行期**返回 null 而非编译失败,**不阻断构建**,不在本门面内;
//   ③ 只认 `UnityEditor` 命名空间;`UNITY_EDITOR` 之外的编辑器专有 API
//      (如 `[MenuItem]` 属性的裸用)不在本门面内 —— 它们同样编不过,但形态不同,
//      **待下一例出现再扩**,不预判。
//
// 权威来源:
//   · `ADR-025 §①`(装配清单:PlayMode 装配 `includePlatforms: []` ⇒ 进 player)
//   · `ADR-012 §Validation`(IL2CPP 三格矩阵 —— 本门是它的**构建期前置**)
//   · 先例:`FsWriteBoundaryGate`(注释剥离 + 纯函数 `Scan` + 真树断言)

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace DaYiJingCheng.EditorTools.Gates
{
    /// <summary>PlayMode 装配的 <c>UnityEditor</c> 裸用门(IL2CPP 构建期前置)。</summary>
    public static class PlayerAssemblyGate
    {
        /// <summary>扫描目录(相对工程根 `unity/`)。**只此一棵树** —— 见文件头注。</summary>
        public const string ScanDirRelative = "Assets/Tests/PlayMode";

        /// <summary>守卫宏名。活跃区判据只认它。</summary>
        public const string GuardSymbol = "UNITY_EDITOR";

        /// <summary>
        /// 编辑器专有引用形态。**限定名 + using** 两种 ——
        /// 刻意不匹配裸词 `UnityEditor`(那会把注释 / 字符串 / 变量名一起收进来)。
        /// </summary>
        private static readonly Regex EditorRefRegex = new Regex(
            @"(?<![\w.])UnityEditor\s*\.|(?<![\w.])using\s+UnityEditor\s*;",
            RegexOptions.Compiled);

        /// <summary>一处违规引用。</summary>
        public sealed class Violation
        {
            /// <summary>相对工程根的路径。</summary>
            public string RelPath;

            /// <summary>行号(1 基,与源文件同行)。</summary>
            public int Line;

            /// <summary>该行原文(去首尾空白)。</summary>
            public string Text;
        }

        /// <summary>
        /// 全量求值:递归扫 <c>Assets/Tests/PlayMode</c>,返回失败列表(空 = 绿)。
        /// </summary>
        /// <param name="unityProjectRoot">工程根(含 `Assets/`)。</param>
        public static List<string> RunAll(string unityProjectRoot)
        {
            var errs = new List<string>();
            string dir = Path.Combine(unityProjectRoot, ScanDirRelative);
            if (!Directory.Exists(dir))
            {
                errs.Add($"[playasm] 扫描目录不存在:{dir}(PlayMode 测试树被移位?门须同步改口径)");
                return errs;
            }

            int scanned = 0;
            foreach (string file in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
            {
                scanned++;
                string rel = ToGateRelative(file, unityProjectRoot);
                foreach (Violation v in Scan(rel, File.ReadAllText(file)))
                {
                    errs.Add($"[playasm] {v.RelPath}:{v.Line} 裸用 `UnityEditor` —— " +
                             $"`{v.Text}`\n" +
                             "        PlayMode 装配(includePlatforms: []) 会进 player," +
                             "而 player 里没有 `UnityEditor` 命名空间 ⇒ CS0234 ⇒ **整个 player 构建失败**。\n" +
                             "        修法:把该调用包进 `#if UNITY_EDITOR … #else … #endif`" +
                             "(player 侧走 `Assert.Inconclusive` 或等效短路)。");
                }
            }

            // 空树守卫:扫到 0 个文件 ⇒ 判据空洞为真(承 FsWriteBoundaryGate / D5 的非空守卫口径)。
            if (scanned == 0)
                errs.Add($"[playasm] `{ScanDirRelative}` 下零个 .cs —— 扫描面为空时本门恒真," +
                         "那是假绿而非通过(测试树被清空 / 路径写错?)");

            return errs;
        }

        /// <summary>
        /// 单文件扫描。**纯函数** —— 负向夹具直接喂源码,不碰文件系统。
        /// </summary>
        /// <param name="relPath">相对工程根的路径(仅用于报文)。</param>
        /// <param name="source">该文件源码。</param>
        public static List<Violation> Scan(string relPath, string source)
        {
            var found = new List<Violation>();
            string stripped = FsWriteBoundaryGate.StripComments(source);
            string[] lines = stripped.Split('\n');
            string[] rawLines = source.Split('\n');

            // 行级活跃栈:true = 该行在 UNITY_EDITOR 活跃区内。
            var inEditor = new bool[lines.Length];
            var stack = new Stack<Frame>();
            bool active = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string t = lines[i].TrimStart();

                if (t.StartsWith("#if", StringComparison.Ordinal))
                {
                    // 只认直接含 UNITY_EDITOR 的条件。
                    //  `#if UNITY_EDITOR`  ⇒ 本区在编辑器里**活跃**;其 `#else` **不**活跃。
                    //  `#if !UNITY_EDITOR` ⇒ 本区**不**活跃;其 `#else` **活跃**(编辑器侧)。
                    bool mentions = ContainsGuard(t);
                    bool negated = t.StartsWith("#if !", StringComparison.Ordinal) && mentions;
                    bool positive = mentions && !negated;

                    stack.Push(new Frame { ParentActive = active, Positive = positive, Negative = mentions && negated });
                    active = active || positive;
                    inEditor[i] = active;
                    continue;
                }
                if (t.StartsWith("#elif", StringComparison.Ordinal))
                {
                    Frame f = stack.Count > 0 ? stack.Peek() : default(Frame);
                    bool mentions = ContainsGuard(t);
                    bool negated = t.StartsWith("#elif !", StringComparison.Ordinal) && mentions;
                    bool positive = mentions && !negated;
                    // 保守:`#elif` 只看自己那一支,并把配对的 Positive/Negative 一并记上
                    // (多支链上只要有一支是编辑器侧,就不该把它当 player 路径报红)。
                    f.Positive |= positive;
                    f.Negative |= mentions && negated;
                    if (stack.Count > 0) { stack.Pop(); stack.Push(f); }
                    active = f.ParentActive || positive;
                    inEditor[i] = active;
                    continue;
                }
                if (t.StartsWith("#else", StringComparison.Ordinal))
                {
                    // ⚠️ **`#else` 的活跃性取决于配对 `#if` 的极性** —— 这是本门最容易写错的一处:
                    //   `#if UNITY_EDITOR`  的 `#else` = player 侧 ⇒ **不**活跃 ⇒ 裸用**要报**;
                    //   `#if !UNITY_EDITOR` 的 `#else` = 编辑器侧 ⇒ **活跃** ⇒ 裸用**放行**。
                    // 写反的后果:把合法的 `#if !UNITY_EDITOR … #else <AssetDatabase> #endif`
                    // 判红(误报),而那正是修复那 4 件时的推荐形态之一。
                    Frame f = stack.Count > 0 ? stack.Peek() : default(Frame);
                    active = f.ParentActive || f.Negative;
                    inEditor[i] = active;
                    continue;
                }
                if (t.StartsWith("#endif", StringComparison.Ordinal))
                {
                    active = stack.Count > 0 ? stack.Pop().ParentActive : false;
                    inEditor[i] = active;
                    continue;
                }

                inEditor[i] = active;
            }

            // ⚠️ **字符串字面量也要置空** —— 实测(2026-10-21):修好那 4 件后,
            //   `Assert.Inconclusive("…依赖 UnityEditor.AssetDatabase…")` 这类**说明串**
            //   会被裸词判据当成引用,产生 **4 条误报**。串里的 `UnityEditor.X` 不是代码,
            //   在 player 里也不会 CS0234。
            //   复用 `FsWriteBoundaryGate.BlankStringLiterals` 会**抹掉引号** ⇒ 破坏
            //   `using UnityEditor;` 之外的行结构,故此处只抹串内容、保留引号(与 R1 的
            //   `IsStringLiteral` 同款取舍)。
            string[] structure = BlankStringContents(stripped).Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                if (inEditor[i]) continue;                     // 已在编辑器守卫内 ⇒ 合法
                if (!EditorRefRegex.IsMatch(structure[i])) continue;
                found.Add(new Violation
                {
                    RelPath = relPath,
                    Line = i + 1,
                    Text = (i < rawLines.Length ? rawLines[i] : lines[i]).Trim(),
                });
            }

            return found;
        }

        /// <summary>条件编译栈帧:父级活跃性 + 该 <c>#if</c> 的极性。</summary>
        private struct Frame
        {
            /// <summary>进入本 <c>#if</c> 之前的活跃性。</summary>
            public bool ParentActive;

            /// <summary>条件形如 <c>UNITY_EDITOR</c>(或含它且非否定)⇒ 正文在编辑器里活跃。</summary>
            public bool Positive;

            /// <summary>条件形如 <c>!UNITY_EDITOR</c> ⇒ 正文不活跃,而其 <c>#else</c> 活跃。</summary>
            public bool Negative;
        }

        private static bool ContainsGuard(string directive)
            => directive.IndexOf(GuardSymbol, StringComparison.Ordinal) >= 0;

        /// <summary>
        /// 把**字符串字面量的内容**置空,保留引号与换行(偏移不变)。
        /// <para>与 <c>FsWriteBoundaryGate.BlankStringLiterals</c> 的差别:后者连引号一起抹,
        /// 本门需要保留引号以便 `using UnityEditor;` 这类行仍可被正则识别。</para>
        /// </summary>
        private static string BlankStringContents(string source)
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
                    if (c == '\\' && i + 1 < buf.Length)
                    {
                        buf[i] = ' ';
                        if (buf[i + 1] != '\n') buf[i + 1] = ' ';
                        i += 2;
                        continue;
                    }
                    if (c == quote) { quote = '\0'; i++; continue; }
                    if (c != '\n') buf[i] = ' ';
                    i++;
                    continue;
                }
                if (c == '"' || c == '\'') { quote = c; i++; continue; }
                i++;
            }
            return new string(buf);
        }

        private static string ToGateRelative(string fullPath, string unityProjectRoot)
        {
            string root = Path.GetFullPath(unityProjectRoot);
            string full = Path.GetFullPath(fullPath);
            if (full.StartsWith(root, StringComparison.Ordinal))
            {
                string rel = full.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar,
                                                                   Path.AltDirectorySeparatorChar);
                return rel.Replace('\\', '/');
            }
            return fullPath.Replace('\\', '/');
        }
    }
}
