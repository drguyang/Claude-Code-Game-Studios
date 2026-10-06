// 权威来源:AC-11-11①(零浮点)· ADR-006(Q16.16 整数域)· ADR-017 §二(引用集白名单机制)
//
// ⚠️ 本件是 11 侧**唯一**的零浮点静态扫描实现 —— story-002(DoseCalculator)与
//    story-003(HalfLifeCalculator)共享同一扫描面(Sim/Prescription/ 全目录)。
//    重复实现在两处会导致「修一处漏一处」(实测已发生:字符串剥离只加在一侧 ⇒ 另一侧误报)。
//
// 扫描面 = Sim/Prescription/ 下全部 *.cs(AC-11-11① 是**系统级**约束,非单文件约束)。

using System.Collections.Generic;
using System.IO;
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

            // 类型名 / 字面量 / 浮点函数调用三路;大小写不敏感;decimal 亦属浮点
            var floatPattern = new Regex(@"\b(float|double|decimal)\b", RegexOptions.IgnoreCase);
            var floatLiteralPattern = new Regex(@"\b\d+\.\d+[fdm]?\b|\b\d+[fdm]\b", RegexOptions.IgnoreCase);
            var floatCallPattern = new Regex(
                @"\bToFloat\s*\(|\bMath\.(Sqrt|Pow|Exp|Log|Log10|Sin|Cos|Tan|Atan|Atan2|Abs|Floor|Ceiling|Round)\s*\(");

            foreach (string file in Directory.GetFiles(simDir, "*.cs", SearchOption.AllDirectories))
            {
                string raw = File.ReadAllText(file);
                // 剥离字符串字面量:异常消息里的「F-11.2」这类版本号会被 float 字面量正则误报。
                // 字符串内容不参与浮点运算,剥离不损失判据强度。
                string content = Regex.Replace(raw, "\"(@?)(\\\\.|[^\"\\\\])*\"", "\"\"");

                Collect(floatPattern, content, file, "type", violations);
                Collect(floatLiteralPattern, content, file, "literal", violations);
                Collect(floatCallPattern, content, file, "float call", violations);
            }
            return violations;
        }

        private static void Collect(Regex pattern, string content, string file, string label, List<string> violations)
        {
            foreach (Match m in pattern.Matches(content))
            {
                int lineStart = content.LastIndexOf('\n', m.Index) + 1;
                string line = content.Substring(lineStart, m.Index - lineStart).Trim();
                if (line.StartsWith("//") || line.StartsWith("*") || line.StartsWith("/*"))
                    continue;  // 注释中的提及不算违例
                violations.Add($"{Path.GetFileName(file)}:{m.Index}: {label} '{m.Value}'");
            }
        }
    }
}
