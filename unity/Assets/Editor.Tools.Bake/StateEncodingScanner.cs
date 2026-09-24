// 权威来源:AC-21a-26(item_key 字段以 int 编码 processing_state ⇒ 装配期断言失败;D-21-13)
//          · Story 008 QA:扫描面 = 数据产物层 .json/.asset,不只查代码字段类型;
//            Edge case 明文「int 编码恰在旧 .asset 遗留文件(扫描全产物目录)」

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>AC-26 数据产物扫描器:content 内出现「processing_state : 数字」即违例。
    /// <para>两种形都扫:JSON <c>"processing_state": 3</c> 与 YAML/.asset <c>processing_state: 3</c>。
    /// 明文字符串 state(合法作者态)不命中。</para>
    /// <example><code>var v = StateEncodingScanner.ValidateNoIntEncodedState(File.ReadAllText(path));</code></example>
    /// </summary>
    public static class StateEncodingScanner
    {
        private static readonly Regex JsonIntState = new Regex(
            "\"processing_state\"\\s*:\\s*[-+]?\\d", RegexOptions.Compiled);

        private static readonly Regex YamlIntState = new Regex(
            "(?<![\"])processing_state\\s*:\\s*[-+]?\\d", RegexOptions.Compiled);

        /// <summary>扫描一段产物内容(JSON / YAML / 任意文本)。</summary>
        /// <param name="content">产物文本。</param>
        /// <returns>违例列表;空 = 合法(state 为明文字符串或不存在)。</returns>
        public static IReadOnlyList<string> ValidateNoIntEncodedState(string content)
        {
            var violations = new List<string>();
            if (string.IsNullOrEmpty(content))
                return violations;

            if (JsonIntState.IsMatch(content))
            {
                violations.Add(
                    "AC-21a-26:检出 processing_state 以 int 编码(JSON 数字 token)—— " +
                    "item_key 字段禁 int 编码,装配期硬失败(D-21-13;枚举序插入 P1a 值会静默重映射)");
            }
            if (YamlIntState.IsMatch(content))
            {
                violations.Add(
                    "AC-21a-26:检出 processing_state 以 int 编码(YAML/.asset 形)—— " +
                    "旧 .asset 遗留产物同样拒收;唯一合法作者态 = assets/data/*.json 明文(D-21-13)");
            }
            return violations;
        }

        /// <summary>递归扫描 <paramref name="assetsRoot"/> 下全部 *.asset(数据产物目录全扫 ——
        /// 「旧 .asset 遗留文件」Edge case);路径含 /Tests/ 段的跳过(测试合成夹具)。</summary>
        /// <param name="assetsRoot">Assets 根目录绝对路径。</param>
        /// <returns>违例列表(含文件路径)。</returns>
        public static IReadOnlyList<string> ScanAssetProducts(string assetsRoot)
        {
            var violations = new List<string>();
            if (string.IsNullOrEmpty(assetsRoot) || !Directory.Exists(assetsRoot))
                return violations;

            foreach (string file in Directory.GetFiles(assetsRoot, "*.asset", SearchOption.AllDirectories))
            {
                string normalized = file.Replace('\\', '/');
                if (normalized.IndexOf("/Tests/", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                string content;
                try
                {
                    content = File.ReadAllText(file);
                }
                catch (IOException ex)
                {
                    violations.Add($"AC-21a-26:无法读取产物 {file} —— {ex.Message}");
                    continue;
                }

                foreach (string v in ValidateNoIntEncodedState(content))
                    violations.Add($"{v} [文件: {normalized}]");
            }
            return violations;
        }
    }
}
