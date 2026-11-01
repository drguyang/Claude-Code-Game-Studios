// `inventory_constants.json` 的**烘焙入口**(阶段 1 词法 + 阶段 2 绑定 + 编码)。
//
// 权威来源:ADR-014 §二/§三/§五 · `InventoryConstantsBinder`(AC-20-13 的构建期执行体)
//
// ⚠️ **本 Baker 与 `ForagingConstantsBaker` 同构**,差异只有一处:本表**无跨表依赖**
//   (`carry_cap` 是 20 自己的量),故不读第二份文件。
//
// ⚠️ **不做写盘** —— 落盘与 Addressables 组注册归菜单层(`DataBakeMenu`),
//   与全部既有 Baker 一致;EditMode 测试可直驱 `BakeFromRepo` 而不触盘。

using System;
using System.Collections.Generic;
using System.IO;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>`inventory_constants.json` 两阶段烘焙的仓根装载器。</summary>
    public static class InventoryConstantsBake
    {
        /// <summary>作者态源文件名(单一出处;与 Binder 同源)。</summary>
        public const string FileName = InventoryConstantsSchema.FileName;

        /// <summary>源文件所在目录(相对仓根)。</summary>
        public const string DataDirName = "assets/data";

        /// <summary>烘焙产物名(`Assets/DataCooked/` 下;Addressables 地址同此名)。</summary>
        public const string CookedFileName = "inventory_constants.cooked.bytes";

        /// <summary>烘焙输出。</summary>
        public readonly struct BakeOutput
        {
            /// <summary>cooked 字节(含 20 字节头)。</summary>
            public readonly byte[] Cooked;

            /// <summary>源内容哈希派生的 ConfigVersion(u32;ADR-014 §五)。</summary>
            public readonly uint ConfigVersion;

            /// <summary>绑定结果(已校验;测试可据此断形状而不必反解字节)。</summary>
            public readonly InventoryConstantsBinder.BindResult Bound;

            /// <summary>构造输出。</summary>
            public BakeOutput(byte[] cooked, uint configVersion,
                              InventoryConstantsBinder.BindResult bound)
            {
                Cooked = cooked; ConfigVersion = configVersion; Bound = bound;
            }
        }

        /// <summary>从仓根烘焙 <c>assets/data/inventory_constants.json</c>。</summary>
        /// <exception cref="ArgumentNullException"><paramref name="repoRoot"/> 为 null。</exception>
        /// <exception cref="BakeValidationException">源文件缺失 / 词法 / 绑定失败(聚合)。</exception>
        public static BakeOutput BakeFromRepo(string repoRoot)
        {
            if (repoRoot == null) throw new ArgumentNullException(nameof(repoRoot));

            string path = Path.Combine(repoRoot, DataDirName, FileName);
            if (!File.Exists(path))
                throw new BakeValidationException(new List<string> { $"源文件不存在:{path}" });

            return BakeFromSourceText(File.ReadAllText(path));
        }

        /// <summary>从源文本烘焙(测试负例入口;不落盘)。</summary>
        /// <exception cref="BakeValidationException">词法 / 绑定失败(聚合)。</exception>
        public static BakeOutput BakeFromSourceText(string json)
        {
            InventoryConstantsBinder.BindResult bound = InventoryConstantsBinder.Bind(json);

            // ⚠️ ConfigVersion 一律走**全仓唯一的派生器**(`ConfigVersionUtility`),
            //    不在这里自算哈希 —— 自算 = 第二套算法,产物版本号与其余件不可比。
            uint configVersion = ConfigVersionUtility.DeriveConfigVersion(new[]
            {
                new KeyValuePair<string, string>(FileName, json),
            });

            byte[] cooked = InventoryConstantsCookedWriter.Write(bound, bound.SchemaVersion, configVersion);
            return new BakeOutput(cooked, configVersion, bound);
        }
    }
}
