// sprint-08 S1.0 · 病名册词表仓根烘焙入口(阶段 1 词法 + 阶段 2 绑定 + 编码)
//
// ⚠️ 不碰 UnityEngine / Addressables —— 菜单与 EditMode 测试驱动同一台机器。

using System;
using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.Gameplay.Presentation.Casebook;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>`case_judgment_lexicon.json` 两阶段烘焙的仓根装载器。</summary>
    public static class JudgmentLexiconBaker
    {
        /// <summary>作者态源文件名(单一出处)。</summary>
        public const string FileName = "case_judgment_lexicon.json";

        /// <summary>源文件所在目录(相对仓根)。</summary>
        public const string DataDirName = "assets/data";

        public readonly struct BakeOutput
        {
            public readonly byte[] Cooked;
            public readonly uint ConfigVersion;
            public readonly IReadOnlyList<JudgmentLexemeRow> Rows;

            public BakeOutput(byte[] cooked, uint configVersion, IReadOnlyList<JudgmentLexemeRow> rows)
            {
                Cooked = cooked;
                ConfigVersion = configVersion;
                Rows = rows;
            }
        }

        /// <exception cref="BakeValidationException">词法 / 绑定 / 校验任一失败(聚合)。</exception>
        public static BakeOutput BakeFromRepo(string repoRoot)
        {
            if (repoRoot == null) throw new ArgumentNullException(nameof(repoRoot));

            string path = Path.Combine(repoRoot, DataDirName, FileName);
            if (!File.Exists(path))
                throw new BakeValidationException(new List<string> { $"源文件不存在:{path}" });

            string json = File.ReadAllText(path);
            JudgmentLexiconBinder.BindResult bound = JudgmentLexiconBinder.Bind(json);

            uint configVersion = ConfigVersionUtility.DeriveConfigVersion(new[]
            {
                new KeyValuePair<string, string>(FileName, json),
            });

            byte[] cooked = JudgmentLexiconCookedWriter.Write(bound.Rows, bound.SchemaVersion, configVersion);
            return new BakeOutput(cooked, configVersion, bound.Rows);
        }
    }
}
