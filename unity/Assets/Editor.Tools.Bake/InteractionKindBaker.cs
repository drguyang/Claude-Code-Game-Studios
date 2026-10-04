// 权威来源:ADR-014 §三/§五(两阶段烘焙入口 · ConfigVersion = 源数据集内容哈希派生)
//          · Story 007(NR-1:装载路径的**仓根装载器** —— 与菜单 / 测试共用同一台机器)
//
// ⚠️ 本件刻意**不碰 UnityEngine / Addressables**:纯文件 I/O + 绑定 + 编码,
//    故菜单(编辑器)与 EditMode 测试可**驱动同一台生产机器**(反空转:
//    测试断言的必须是生产接缝,不是测试侧重实现)。

using System;
using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.Gameplay.Interaction;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>`interaction_kinds.json` 两阶段烘焙的**仓根装载器**(阶段 1 词法 + 阶段 2 绑定 + 编码)。
    /// <para>校验器调用点在 <see cref="InteractionKindBinder.Bind"/> 内(唯一调用点)。</para>
    /// </summary>
    public static class InteractionKindBaker
    {
        /// <summary>作者态源文件名(单一出处;测试与菜单共用)。</summary>
        public const string KindsFileName = "interaction_kinds.json";

        /// <summary>世界维度源文件名(4-DC-4 ② 的 W/H/D + r_interact)。</summary>
        public const string DimensionsFileName = "interaction_kinds_dimensions.json";

        /// <summary>源文件所在目录(相对仓根;承既有先例 `assets/data/`)。</summary>
        public const string DataDirName = "assets/data";

        /// <summary>烘焙成功产物(<c>interaction_kinds.cooked.bytes</c> 内容)。</summary>
        public readonly struct BakeOutput
        {
            /// <summary>cooked 字节(含 20 字节头)。</summary>
            public readonly byte[] Cooked;

            /// <summary>源数据集内容哈希派生的 ConfigVersion(u32)。</summary>
            public readonly uint ConfigVersion;

            /// <summary>校验通过后的行集(供调用方核数,不落盘)。</summary>
            public readonly IReadOnlyList<KindContractRow> Rows;

            public BakeOutput(byte[] cooked, uint configVersion, IReadOnlyList<KindContractRow> rows)
            {
                Cooked = cooked; ConfigVersion = configVersion; Rows = rows;
            }
        }

        /// <summary>从仓根烘焙(读两个源文件 → 绑定 + 校验 → 编码)。</summary>
        /// <param name="repoRoot">仓根(含 `assets/data/`)。</param>
        /// <exception cref="BakeValidationException">词法 / 绑定 / 4-DC 校验任一失败(聚合)。</exception>
        public static BakeOutput BakeFromRepo(string repoRoot)
        {
            if (repoRoot == null) throw new ArgumentNullException(nameof(repoRoot));

            string dir = Path.Combine(repoRoot, DataDirName);
            string kindsPath = Path.Combine(dir, KindsFileName);
            string dimsPath = Path.Combine(dir, DimensionsFileName);

            var errors = new List<string>();
            if (!File.Exists(kindsPath)) errors.Add($"源文件不存在:{kindsPath}");
            if (!File.Exists(dimsPath)) errors.Add($"源文件不存在:{dimsPath}");
            if (errors.Count > 0) throw new BakeValidationException(errors);

            string kindsJson = File.ReadAllText(kindsPath);
            string dimsJson = File.ReadAllText(dimsPath);

            InteractionKindBinder.BindResult bound = InteractionKindBinder.Bind(kindsJson, dimsJson);

            // ConfigVersion = 源文本集内容哈希(排序按文件名,确定性;ADR-014 §五)。
            uint configVersion = ConfigVersionUtility.DeriveConfigVersion(new[]
            {
                new KeyValuePair<string, string>(KindsFileName, kindsJson),
                new KeyValuePair<string, string>(DimensionsFileName, dimsJson),
            });

            byte[] cooked = InteractionKindCookedWriter.Write(bound.Rows, bound.SchemaVersion, configVersion,
                bound.RInteract, bound.WorldW, bound.WorldH, bound.WorldD);
            return new BakeOutput(cooked, configVersion, bound.Rows);
        }
    }
}
