// 权威来源:ADR-014 §三/§五(两阶段烘焙入口 · ConfigVersion = 源数据集内容哈希派生)
//          · Story 004(仓根装载器 —— 菜单与测试共用同一台机器)
//
// ⚠️ 本件刻意**不碰 UnityEngine / Addressables**:纯文件 I/O + 绑定 + 编码,
//    故菜单(编辑器)与 EditMode 测试可**驱动同一台生产机器**(反空转)。

using System;
using System.Collections.Generic;
using System.IO;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>`diagnosis_negative_confidence.json` 两阶段烘焙的**仓根装载器**。
    /// <para>定表生成器调用点在 <see cref="DiagnosisNegativeConfidenceBinder.Bind"/> 内(唯一调用点)。</para>
    /// </summary>
    public static class DiagnosisNegativeConfidenceBaker
    {
        /// <summary>作者态源文件名(单一出处;测试与菜单共用)。</summary>
        public const string NegativeConfidenceFileName = "diagnosis_negative_confidence.json";

        /// <summary>源文件所在目录(相对仓根;承既有先例 `assets/data/`)。</summary>
        public const string DataDirName = "assets/data";

        /// <summary>烘焙成功产物(<c>diagnosis_negative_confidence.cooked.bytes</c> 内容)。</summary>
        public readonly struct BakeOutput
        {
            /// <summary>cooked 字节(含 20 字节头)。</summary>
            public readonly byte[] Cooked;

            /// <summary>源数据集内容哈希派生的 ConfigVersion(u32)。</summary>
            public readonly uint ConfigVersion;

            /// <summary>生成后的定表(供调用方核形状,不落盘)。</summary>
            public readonly IReadOnlyList<float> Table;

            public BakeOutput(byte[] cooked, uint configVersion, IReadOnlyList<float> table)
            {
                Cooked = cooked;
                ConfigVersion = configVersion;
                Table = table;
            }
        }

        /// <summary>从仓根烘焙(读源文件 → 绑定 + 校验 + 生成定表 → 编码)。</summary>
        /// <param name="repoRoot">仓根(含 `assets/data/`)。</param>
        /// <exception cref="BakeValidationException">词法 / 绑定 / 值域 / C-5 / C-6 任一失败(聚合)。</exception>
        public static BakeOutput BakeFromRepo(string repoRoot)
        {
            if (repoRoot == null) throw new ArgumentNullException(nameof(repoRoot));

            string path = Path.Combine(repoRoot, DataDirName, NegativeConfidenceFileName);
            if (!File.Exists(path))
                throw new BakeValidationException(new List<string> { $"源文件不存在:{path}" });

            string json = File.ReadAllText(path);
            DiagnosisNegativeConfidenceBinder.BindResult bound =
                DiagnosisNegativeConfidenceBinder.Bind(json);

            // ConfigVersion = 源文本内容哈希(ADR-014 §五;单文件集,按文件名排序)。
            uint configVersion = ConfigVersionUtility.DeriveConfigVersion(new[]
            {
                new KeyValuePair<string, string>(NegativeConfidenceFileName, json),
            });

            byte[] cooked = DiagnosisNegativeConfidenceCookedWriter.Write(bound, configVersion);
            return new BakeOutput(cooked, configVersion, bound.Table);
        }
    }
}
