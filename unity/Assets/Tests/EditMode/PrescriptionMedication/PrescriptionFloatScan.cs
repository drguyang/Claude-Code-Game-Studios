// 权威来源:AC-11-11①(零浮点)· ADR-006(Q16.16 整数域)· ADR-017 §二(引用集白名单机制)
//          · ADR-005 Amendment G(禁 `System.Int128` / `BigInteger` —— 手工 hi/lo 唯一路径)
//
// ⚠️ 本件是 11 侧**唯一**的零浮点静态扫描实现 —— story-002(DoseCalculator)与
//    story-003(HalfLifeCalculator)共享同一扫描面(Sim/Prescription/ 全目录)。
//    重复实现在两处会导致「修一处漏一处」(实测已发生:字符串剥离只加在一侧 ⇒ 另一侧误报)。
//
// 扫描面 = Sim/Prescription/ 下全部 *.cs(AC-11-11① 是**系统级**约束,非单文件约束)。

using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace DaYiJingCheng.Tests.PrescriptionMedication
{
    /// <summary>11 侧零浮点静态扫描(AC-11-11①)。story-002 / story-003 共享。</summary>
    public static class PrescriptionFloatScan
    {
        /// <summary>
        /// 扫描 <paramref name="simDir"/> 下全部 *.cs,返回违例列表(空 = 通过)。
        /// </summary>
        /// <param name="simDir">Sim/Prescription/ 绝对路径。</param>
        /// <returns>违例描述列表;空列表 = 零浮点。</returns>
        public static List<string> Scan(string simDir)
        {
            var violations = new List<string>();
            if (!Directory.Exists(simDir)) return violations;

            // 四路判据:浮点类型名 / 大整数替身 / 浮点字面量 / 浮点函数调用
            // M-2(评审):ADR-005 AmG 禁 `System.Int128` 与 `BigInteger` —— 二者不进浮点正则,
            // 但同为「绕开手工 hi/lo」的替代路径(Int128 在 Unity 6.3 netstandard2.1/IL2CPP 不存在,
            // BigInteger 违 AC-4)⇒ 与浮点同等禁,扫到即违例。
            var floatPattern = new Regex(@"\b(float|double|decimal)\b", RegexOptions.IgnoreCase);
            var bigIntPattern = new Regex(@"\b(Int128|UInt128|BigInteger)\b");
            var floatLiteralPattern = new Regex(@"\b\d+\.\d+[fdm]?\b|\b\d+[fdm]\b", RegexOptions.IgnoreCase);
            var floatCallPattern = new Regex(
                @"\bToFloat\s*\(|\bMath\.(Sqrt|Pow|Exp|Log|Log10|Sin|Cos|Tan|Atan|Atan2|Abs|Floor|Ceiling|Round)\s*\(");

            foreach (string file in Directory.GetFiles(simDir, "*.cs", SearchOption.AllDirectories))
            {
                // m6(评审):原实现用「命中点前的整行前缀是否以 // 开头」判注释 —— 行尾注释
                // (`int x = 1; // 2.5`)与跨行块注释都会误判。现改为**单遍字符扫描器**
                // 精确剥离注释与字符串字面量:注释里的提及是**文档**(生产件注释常引述被禁类型名),
                // 字符串内容不参与算术 —— 两者都不构成违例,剥离不损失判据强度。
                string content = StripCommentsAndStrings(File.ReadAllText(file));

                Collect(floatPattern, content, file, "type", violations);
                Collect(bigIntPattern, content, file, "big-int type", violations);
                Collect(floatLiteralPattern, content, file, "literal", violations);
                Collect(floatCallPattern, content, file, "float call", violations);
            }
            return violations;
        }

        private static void Collect(Regex pattern, string content, string file, string label, List<string> violations)
        {
            foreach (Match m in pattern.Matches(content))
                violations.Add($"{Path.GetFileName(file)}:{m.Index}: {label} '{m.Value}'");
        }

        /// <summary>单遍扫描:把注释与字符串字面量的**内容**替换为等长空白,保留换行以维持行号可读。
        /// <para>处理 <c>//</c> 行注释 · <c>/* */</c> 块注释 · <c>"…"</c> 与 <c>@"…"</c> 字符串
        /// (含 <c>\"</c> 转义)。原字符数不变 ⇒ 违例报告的偏移量与原文件一致。</para></summary>
        private static string StripCommentsAndStrings(string source)
        {
            var sb = new StringBuilder(source.Length);
            int i = 0, n = source.Length;
            while (i < n)
            {
                char c = source[i];

                // 行注释
                if (c == '/' && i + 1 < n && source[i + 1] == '/')
                {
                    while (i < n && source[i] != '\n') { sb.Append(' '); i++; }
                    continue;
                }

                // 块注释
                if (c == '/' && i + 1 < n && source[i + 1] == '*')
                {
                    sb.Append("  "); i += 2;
                    while (i < n && !(source[i] == '*' && i + 1 < n && source[i + 1] == '/'))
                    {
                        sb.Append(source[i] == '\n' ? '\n' : ' ');
                        i++;
                    }
                    if (i < n) { sb.Append("  "); i += 2; }
                    continue;
                }

                // 字符串字面量(含逐字 @"" —— @ 本身是代码字符,原样保留)
                if (c == '"')
                {
                    sb.Append('"'); i++;
                    while (i < n)
                    {
                        if (source[i] == '\\' && i + 1 < n)
                        {
                            sb.Append("  "); i += 2;
                            continue;
                        }
                        if (source[i] == '"') { sb.Append('"'); i++; break; }
                        sb.Append(source[i] == '\n' ? '\n' : ' ');
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
