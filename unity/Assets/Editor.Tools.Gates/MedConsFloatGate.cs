// S12-9 · story-002 —— 53 静态面门(AC-53-03 零浮点 + AC-53-12 31 零引用)。
//
// 权威来源:
//   AC-53-03(BLOCKING):53 源文件**零浮点字面量**(静态检查入构建;门 A / ADR-006)。
//   AC-53-12(BLOCKING):53 程序集引用集零 31(声誉与名声)符号 —— 53 不知 31 存在。
//   卡面 story-002 Control Manifest Forbidden:float/double 字面量;31 的任何符号引用。
//   先例:InputBoundaryGates(注释剥离后 token 扫描;偏安全,非可达性精确分析)。
//
// ⚠️ 扫描面 = `unity/Assets/Sim/Consequences/*.cs`(53 的实现落点 —— AC-53-12 载体 =
//   Sim 程序集内命名空间,无独立 asmdef 可断言 ⇒ 源文件面 + 引用集双断言)。
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

            foreach (string file in Directory.GetFiles(dir, "*.cs", SearchOption.TopDirectoryOnly))
            {
                string text = StripComments(File.ReadAllText(file));
                string rel = Path.GetFileName(file);

                foreach (string tok in FloatTokens)
                    if (Regex.IsMatch(text, $@"\b{Regex.Escape(tok)}\b"))
                        errs.Add($"[medcons·AC-53-03] {rel} 出现浮点 token「{tok}」—— " +
                                 "53 源零浮点字面量(静态检查入构建;U 以 Q16.16 raw long 承载)");

                foreach (string tok in ReputationTokens)
                    if (text.Contains(tok))
                        errs.Add($"[medcons·AC-53-12] {rel} 出现 31 类 token「{tok}」—— " +
                                 "53 不知 31 存在(声誉/信任类符号零命中)");

                foreach (string tok in ScoreTokens)
                    if (text.Contains(tok))
                        errs.Add($"[medcons·AC-53-12] {rel} 出现累积分 token「{tok}」—— " +
                                 "53 是水龙头,单向流只到世界流(AC-53-11 同源)");
            }
            return errs;
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
    }
}
