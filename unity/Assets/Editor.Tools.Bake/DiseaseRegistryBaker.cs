// 权威来源:ADR-014 §三/§五(两阶段烘焙入口 · ConfigVersion = 源数据集内容哈希派生)
//          · `DiseaseActionAxisBaker`(同构先例)
//
// ⚠️ 本件 = 9 注册表的烘焙入口:读源文件 → 绑定 + 校验 → 编码。
//    在此之前 9 的注册表**没有任何烘焙入口** —— `disease_registry.cooked` 从未产出过。

using System;
using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.Sim;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>`assets/data/disease_registry.json` → `disease_registry.cooked` 的烘焙入口。</summary>
    public static class DiseaseRegistryBaker
    {
        /// <summary>源文件所在目录(相对仓根)。</summary>
        public const string DataDirName = "assets/data";

        /// <summary>源文件名。</summary>
        public const string SourceFileName = "disease_registry.json";

        /// <summary>产物文件名(⚠️ `.bytes` 扩展 —— ADR-014 的导入修正:
        /// 自定义扩展默认导入为 DefaultAsset,Addressables 无法按 TextAsset 载入二进制;
        /// `.bytes` 走内建 TextAsset 导入器)。</summary>
        public const string CookedFileName = "disease_registry.cooked.bytes";

        /// <summary>烘焙产物。</summary>
        public readonly struct BakeOutput
        {
            /// <summary>确定字节流(含 20 字节头)。</summary>
            public readonly byte[] Cooked;

            /// <summary>源数据集内容哈希派生的 ConfigVersion(u32)。</summary>
            public readonly uint ConfigVersion;

            /// <summary>绑定后的行集(供调用方断言)。</summary>
            public readonly List<DiseaseRegistryEntry> Rows;

            /// <summary>声明了但未绑定进行的键(透传 <see cref="DiseaseRegistryBinder.BindResult.UnboundKeys"/>)。
            /// <para>⚠️ 这是**丢失登记**,不是错误 —— 但它必须可见,否则「白名单内的键静默丢字段」
            /// 就又回到无人能看见的状态。测试断言其恰为预期集合。</para></summary>
            public readonly List<string> UnboundKeys;

            public BakeOutput(byte[] cooked, uint configVersion, List<DiseaseRegistryEntry> rows,
                              List<string> unboundKeys)
            {
                Cooked = cooked; ConfigVersion = configVersion; Rows = rows; UnboundKeys = unboundKeys;
            }
        }

        /// <summary>从源文本烘焙(不起 IO —— 供测试直接驱动)。</summary>
        public static BakeOutput BakeFromJson(string json)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));

            DiseaseRegistryBinder.BindResult bound = DiseaseRegistryBinder.Bind(json);

            // ConfigVersion = 源文本集内容哈希(排序按文件名,确定性;ADR-014 §五)。
            uint configVersion = ConfigVersionUtility.DeriveConfigVersion(new[]
            {
                new KeyValuePair<string, string>(SourceFileName, json),
            });

            byte[] cooked = DiseaseRegistryCookedWriter.Write(bound.Rows, bound.SchemaVersion, configVersion);
            return new BakeOutput(cooked, configVersion, bound.Rows, bound.UnboundKeys);
        }

        /// <summary>从仓根烘焙(读源文件 → 绑定 + 校验 → 编码)。</summary>
        /// <param name="repoRoot">仓根(含 `assets/data/`)。</param>
        /// <exception cref="BakeValidationException">词法 / 绑定 / R1 校验任一失败(聚合)。</exception>
        public static BakeOutput BakeFromRepo(string repoRoot)
        {
            if (repoRoot == null) throw new ArgumentNullException(nameof(repoRoot));

            string path = Path.Combine(repoRoot, DataDirName, SourceFileName);
            var errors = new List<string>();
            if (!File.Exists(path)) errors.Add($"源文件不存在:{path}");
            if (errors.Count > 0) throw new BakeValidationException(errors);

            return BakeFromJson(File.ReadAllText(path));
        }
    }
}

// ══════════════════════════════════════════════════════════════════════════
// §未实现登记(如实列出,禁借绿)
// ══════════════════════════════════════════════════════════════════════════
//   ① **无菜单入口** —— `DataBakeMenu` 里没有本件的菜单项。当前只能从测试 / CLI 驱动。
//      要「烘焙菜单能出产物」须另接(未做)。
//   ② **产物未落盘** —— 本件返回字节,不写文件。落盘须走既有 cooked 写盘路径
//      (未接)。⇒ `assets/data/disease_registry.cooked.bytes` **当前不存在**。
//   ③ **运行期不加载** —— `CompositionRoot` 仍用 `CreateFallbackRegistry()`,
//      不读本产物(读方 codec 亦不存在,见 CookedWriter 文件尾 ①)。
//   ⇒ 本件的实际兑现 = **「能烘焙出确定字节」**,不是「管线端到端已通」。
