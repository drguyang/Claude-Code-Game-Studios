// 权威来源:ADR-014 §三/§五(两阶段烘焙入口 · ConfigVersion = 源数据集内容哈希派生)
//          · Story 001(仓根装载器 —— 菜单与测试共用同一台机器)
//
// ⚠️ 本件刻意**不碰 UnityEngine / Addressables**:纯文件 I/O + 绑定 + 编码,
//    故菜单(编辑器)与 EditMode 测试可**驱动同一台生产机器**(反空转)。

using System;
using System.Collections.Generic;
using System.IO;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>`prescription_actions.json` + `materia_lexicon.json` 两阶段烘焙的**仓根装载器**
    /// (阶段 1 词法 + 阶段 2 绑定 + 编码)。
    /// <para>校验器调用点在 <see cref="PrescriptionActionsBinder.Bind"/> 内(唯一调用点)。</para>
    /// </summary>
    public static class PrescriptionActionsBaker
    {
        /// <summary>作者态源文件名(单一出处;测试与菜单共用)。</summary>
        public const string ActionsFileName = "prescription_actions.json";

        /// <summary>本草词表源文件名。</summary>
        public const string LexiconFileName = "materia_lexicon.json";

        /// <summary>源文件所在目录(相对仓根;承既有先例 `assets/data/`)。</summary>
        public const string DataDirName = "assets/data";

        /// <summary>烘焙成功产物(<c>prescription_actions.cooked.bytes</c> 内容)。</summary>
        public readonly struct BakeOutput
        {
            /// <summary>cooked 字节(含 20 字节头)。</summary>
            public readonly byte[] Cooked;

            /// <summary>源数据集内容哈希派生的 ConfigVersion(u32)。</summary>
            public readonly uint ConfigVersion;

            /// <summary>校验通过后的行集(供调用方核数,不落盘)。</summary>
            public readonly IReadOnlyList<PrescriptionActionRow> Rows;

            public BakeOutput(byte[] cooked, uint configVersion, IReadOnlyList<PrescriptionActionRow> rows)
            {
                Cooked = cooked; ConfigVersion = configVersion; Rows = rows;
            }
        }

        /// <summary>从仓根烘焙(读三个源文件 → 绑定 + 校验 → 编码)。</summary>
        /// <param name="repoRoot">仓根(含 `assets/data/`)。</param>
        /// <exception cref="BakeValidationException">词法 / 绑定 / DC 校验任一失败(聚合)。</exception>
        public static BakeOutput BakeFromRepo(string repoRoot)
        {
            if (repoRoot == null) throw new ArgumentNullException(nameof(repoRoot));

            string dir = Path.Combine(repoRoot, DataDirName);
            string actionsPath = Path.Combine(dir, ActionsFileName);
            string lexiconPath = Path.Combine(dir, LexiconFileName);
            string itemsPath = Path.Combine(dir, "item_database_items.json");

            var errors = new List<string>();
            if (!File.Exists(actionsPath)) errors.Add($"源文件不存在:{actionsPath}");
            if (!File.Exists(lexiconPath)) errors.Add($"源文件不存在:{lexiconPath}");
            if (!File.Exists(itemsPath)) errors.Add($"源文件不存在:{itemsPath}");
            if (errors.Count > 0) throw new BakeValidationException(errors);

            string actionsJson = File.ReadAllText(actionsPath);
            string lexiconJson = File.ReadAllText(lexiconPath);
            string itemsJson = File.ReadAllText(itemsPath);

            PrescriptionActionsBinder.BindResult bound = PrescriptionActionsBinder.Bind(actionsJson, lexiconJson, itemsJson);

            // ConfigVersion = 源文本集内容哈希(排序按文件名,确定性;ADR-014 §五)。
            uint configVersion = ConfigVersionUtility.DeriveConfigVersion(new[]
            {
                new KeyValuePair<string, string>(ActionsFileName, actionsJson),
                new KeyValuePair<string, string>(LexiconFileName, lexiconJson),
            });

            byte[] cooked = PrescriptionActionsCookedWriter.Write(bound.Rows, bound.SchemaVersion, configVersion,
                bound.DoseBase, bound.MaxDoseDetents, bound.SingleDoseMaxRaw);
            return new BakeOutput(cooked, configVersion, bound.Rows);
        }
    }
}
