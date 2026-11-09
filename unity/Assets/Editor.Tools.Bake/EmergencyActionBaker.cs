// `emergency_action.json` 两阶段烘焙的仓根装载器(T14 · 2026-11-09)
//
// 权威:ADR-014 §三/§五(两阶段烘焙入口 · ConfigVersion = 源数据集内容哈希派生)
//       · `design/gdd/emergency-procedures.md` `:843`
//
// ⚠️ 纯文件 I/O + 绑定 + 编码:不碰 UnityEngine / Addressables。

using System;
using System.Collections.Generic;
using System.IO;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>`emergency_action.json` 的仓根装载器(绑定 + 编码)。</summary>
    public static class EmergencyActionBaker
    {
        /// <summary>作者态源文件名。</summary>
        public const string SourceFileName = EmergencyActionBinder.SourceFileName;

        /// <summary>产物文件名(address 与此同值)。</summary>
        public const string CookedFileName =
            DaYiJingCheng.Gameplay.Presentation.EmergencyActionDataSet.CookedFileName;

        /// <summary>源文件所在目录(相对仓根)。</summary>
        public const string DataDirName = "assets/data";

        /// <summary>烘焙成功产物。</summary>
        public readonly struct BakeOutput
        {
            /// <summary>cooked 字节(含 20 字节头)。</summary>
            public readonly byte[] Cooked;

            /// <summary>内容哈希派生的 ConfigVersion。</summary>
            public readonly uint ConfigVersion;

            /// <summary>绑定结果。</summary>
            public readonly EmergencyActionBinder.BindResult Bound;

            public BakeOutput(byte[] cooked, uint configVersion, EmergencyActionBinder.BindResult bound)
            {
                Cooked = cooked;
                ConfigVersion = configVersion;
                Bound = bound;
            }
        }

        /// <summary>从仓根烘焙。</summary>
        /// <exception cref="BakeValidationException">源文件缺失 / 绑定违例。</exception>
        public static BakeOutput BakeFromRepo(string repoRoot)
        {
            if (repoRoot == null) throw new ArgumentNullException(nameof(repoRoot));

            string path = Path.Combine(repoRoot, DataDirName, SourceFileName);
            if (!File.Exists(path))
                throw new BakeValidationException(new List<string> { $"源文件不存在:{path}" });

            return BakeFromJson(File.ReadAllText(path));
        }

        /// <summary>从 JSON 文本烘焙(夹具入口 —— 负控要直接喂非法 JSON)。</summary>
        public static BakeOutput BakeFromJson(string json)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));

            EmergencyActionBinder.BindResult bound = EmergencyActionBinder.Bind(json);

            uint configVersion = ConfigVersionUtility.DeriveConfigVersion(new[]
            {
                new KeyValuePair<string, string>(SourceFileName, json),
            });

            byte[] cooked = EmergencyActionCookedWriter.Write(bound.Rows, bound.SchemaVersion, configVersion);
            return new BakeOutput(cooked, configVersion, bound);
        }
    }
}
