namespace DaYiJingCheng.Gameplay.UI.Skeuomorphic
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.RegularExpressions;

    /// <summary>主题变量引用验证器:维护变量登记集并提供 USS 完整性断言。</summary>
    public static class ThemeVariableReferenceValidator
    {
        private static readonly HashSet<string> _registered = new HashSet<string>();

        /// <summary>主题变量声明正则(匹配 <code>--skeuo-xxx:</code>)。</summary>
        private static readonly Regex DeclarationRegex =
            new Regex(@"(--skeuo-[a-z0-9-]+)\s*:", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>主题变量引用正则(匹配 <code>var(--skeuo-xxx)</code>)。</summary>
        private static readonly Regex ReferenceRegex =
            new Regex(@"var\((--skeuo-[a-z0-9-]+)\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>从主题定义文件中提取全部声明的变量名。</summary>
        public static IEnumerable<string> ExtractDeclaredNames(string themeUssText)
        {
            foreach (Match m in DeclarationRegex.Matches(themeUssText))
            {
                yield return m.Groups[1].Value;
            }
        }

        /// <summary>从 USS 样式文件中提取全部引用的变量名。</summary>
        public static IEnumerable<string> ExtractReferencedNames(string ussText)
        {
            foreach (Match m in ReferenceRegex.Matches(ussText))
            {
                yield return m.Groups[1].Value;
            }
        }

        /// <summary>登记一个主题变量名(构建期完成)。</summary>
        public static void Register(string variableName)
        {
            if (string.IsNullOrWhiteSpace(variableName))
                throw new ArgumentException("主题变量名不能为空。", nameof(variableName));
            _registered.Add(variableName);
        }

        /// <summary>USS 中引用了一个变量,做存在性检查。</summary>
        public static bool IsRegistered(string variableName)
            => _registered.Contains(variableName);

        /// <summary>构建期完整性断言:列出未登记的变量引用。</summary>
        public static IReadOnlyList<string> ValidateReferences(IEnumerable<string> usedVariables)
        {
            var errs = new List<string>();
            foreach (var v in usedVariables)
            {
                if (!IsRegistered(v))
                    errs.Add($"[C2]  USS 引用了未登记主题变量「{v}」。");
            }
            return errs;
        }
    }
}
