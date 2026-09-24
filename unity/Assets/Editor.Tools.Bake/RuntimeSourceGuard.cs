// 权威来源:AC-21a-48(玩家构建零 JSON 解析器、零 FixParse —— ADR-014 结构性规避)
//          · Story 008 QA:扫描 src 无 assets/data 读取直连与 JSON 字面量解析路径;
//            Edge case:Editor 侧工具链含 Newtonsoft 词法(编辑期豁免,Editor.Tools 程序集白名单内,不进构建);
//            不与 Story 002 的代码侧重扫描合并不重复报(本守卫只扫 JSON 解析 / FixParse / assets/data 直读)

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>AC-48 运行期结构守卫:扫描运行期程序集源码,违例 token = 玩家构建禁物。
    /// <para>违例 token:<c>JsonConvert</c> · <c>JsonTextReader</c> · <c>JObject.Parse</c> ·
    /// <c>FixParse.Parse</c> · 字符串字面量 <c>assets/data</c>。</para>
    /// <para>豁免:路径含 <c>/Tests/</c> 段或任一 <c>Editor.*</c> 目录段(Editor.Tools.Bake 自身的
    /// JsonTextReader 不进构建 ⇒ 结构性豁免);文件名 <c>FixParse.cs</c>(类型定义自身)。</para>
    /// <para>剥离注释(<c>//</c> 与 <c>/* */</c>)后再扫 —— 运行期 doc-comment 里提及本 token 不计违例;
    /// <b>字符串字面量保留</b>(assets/data 直读是运行期字符串,必须命中)。</para>
    /// <example><code>var v = RuntimeSourceGuard.Scan(Path.Combine(projectRoot, "Assets"));</code></example>
    /// </summary>
    public static class RuntimeSourceGuard
    {
        private static readonly string[] ForbiddenTokens =
        {
            "JsonConvert",
            "JsonTextReader",
            "JObject.Parse",
            "FixParse.Parse",
            "assets/data",
        };

        /// <summary>扫描目录树(递归 *.cs)。</summary>
        /// <param name="assetsRoot">Assets 根(含 Sim / Sim.Contracts / Gameplay.* 等)。</param>
        /// <returns>违例列表(含 token 与相对路径);空 = 玩家构建禁物零命中(AC-48)。</returns>
        public static IReadOnlyList<string> Scan(string assetsRoot)
        {
            var violations = new List<string>();
            if (string.IsNullOrEmpty(assetsRoot) || !Directory.Exists(assetsRoot))
                return violations;

            foreach (string file in Directory.GetFiles(assetsRoot, "*.cs", SearchOption.AllDirectories))
            {
                string normalized = file.Replace('\\', '/');
                if (IsExemptPath(normalized))
                    continue;

                string text;
                try
                {
                    text = File.ReadAllText(file, Encoding.UTF8);
                }
                catch (IOException ex)
                {
                    violations.Add($"AC-48:无法读取 {normalized} —— {ex.Message}");
                    continue;
                }

                string stripped = StripCommentsKeepingStrings(text);
                foreach (string token in ForbiddenTokens)
                {
                    if (stripped.IndexOf(token, StringComparison.Ordinal) >= 0)
                    {
                        violations.Add(
                            $"AC-21a-48:运行期源码命中禁物 \"{token}\" —— 玩家构建零 JSON 解析器、零 FixParse、" +
                            $"零 assets/data 直读(ADR-014 结构性规避)[{normalized}]");
                    }
                }
            }
            return violations;
        }

        /// <summary>路径豁免:/Tests/ 段 · Editor.* 目录段 · FixParse.cs(定义自身)。</summary>
        private static bool IsExemptPath(string normalizedPath)
        {
            if (normalizedPath.EndsWith("/FixParse.cs", StringComparison.Ordinal))
                return true;

            string[] segments = normalizedPath.Split('/');
            for (int i = 0; i < segments.Length - 1; i++) // 最后一段 = 文件名,由上面单独判
            {
                string seg = segments[i];
                if (string.Equals(seg, "Tests", StringComparison.OrdinalIgnoreCase))
                    return true;
                if (seg.StartsWith("Editor.", StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        /// <summary>剥离 // 与 /* */ 注释(内容替换为空格,保留换行);字符串 / 字符字面量**内容保留**。</summary>
        internal static string StripCommentsKeepingStrings(string source)
        {
            if (string.IsNullOrEmpty(source))
                return string.Empty;

            var sb = new StringBuilder(source.Length);
            int i = 0;
            int n = source.Length;
            while (i < n)
            {
                char c = source[i];

                // 行注释
                if (c == '/' && i + 1 < n && source[i + 1] == '/')
                {
                    while (i < n && source[i] != '\n')
                    {
                        if (source[i] != '\r') sb.Append(' ');
                        i++;
                    }
                    continue;
                }

                // 块注释
                if (c == '/' && i + 1 < n && source[i + 1] == '*')
                {
                    sb.Append("  ");
                    i += 2;
                    while (i < n && !(source[i] == '*' && i + 1 < n && source[i + 1] == '/'))
                    {
                        if (source[i] != '\n') sb.Append(source[i] == '\r' ? '\r' : ' ');
                        else sb.Append('\n');
                        i++;
                    }
                    if (i < n)
                    {
                        sb.Append("  ");
                        i += 2;
                    }
                    continue;
                }

                // 字符串字面量(保留内容;处理转义)
                if (c == '"')
                {
                    sb.Append(c);
                    i++;
                    while (i < n)
                    {
                        char sc = source[i];
                        sb.Append(sc);
                        i++;
                        if (sc == '\\' && i < n)
                        {
                            sb.Append(source[i]);
                            i++;
                            continue;
                        }
                        if (sc == '"') break;
                    }
                    continue;
                }

                // 字符字面量
                if (c == '\'')
                {
                    sb.Append(c);
                    i++;
                    while (i < n)
                    {
                        char sc = source[i];
                        sb.Append(sc);
                        i++;
                        if (sc == '\\' && i < n)
                        {
                            sb.Append(source[i]);
                            i++;
                            continue;
                        }
                        if (sc == '\'') break;
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
