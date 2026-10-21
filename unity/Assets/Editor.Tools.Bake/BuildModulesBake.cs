// 23 模块化建造 —— 模块目录(`build_modules.json`)的烘焙入口(ADR-014 两阶段)。
//
// 权威来源:
//   ADR-014 §二/§三(阶段1 词法 → 阶段2 绑定 → cooked;玩家构建零 JSON 解析器)
//   GDD design/gdd/modular-building.md `:88`(模块目录归 21a)· `:90`(锚点校验)· `:507`(cost 域)
//   `TD-itemdb-01` / `O-24-1` 的「与 `cost(m)` 同源」半边
//
// ⚠️ **本件刻意不碰 `UnityEngine` / Addressables**(承 `ItemDatabaseFurnitureBake` / `DelayTable53Bake`
//   同款头注):EditMode 测试可直驱,菜单层才负责落盘与组注册。

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>模块目录的烘焙入口。</summary>
    public static class BuildModulesBake
    {
        /// <summary>作者态源文件名(assets/data/ 下;ConfigVersion 哈希的命名键)。</summary>
        public const string FileName = BuildModulesBinder.FileName;

        /// <summary>烘焙产物名(`Assets/DataCooked/` 下;Addressables 地址同此名)。</summary>
        public const string CookedFileName = "build_modules.cooked.bytes";

        /// <summary>烘焙输出。</summary>
        public readonly struct BakeOutput
        {
            /// <summary>cooked 字节(含 20 字节头)。</summary>
            public readonly byte[] Cooked;

            /// <summary>绑定结果(行集 / schema_version)。</summary>
            public readonly BuildModulesBinder.BindResult Bound;

            /// <summary>源内容哈希派生的 ConfigVersion(u32;ADR-014 §五)。</summary>
            public readonly uint ConfigVersion;

            /// <summary>构造输出。</summary>
            public BakeOutput(
                byte[] cooked, BuildModulesBinder.BindResult bound, uint configVersion)
            {
                Cooked = cooked; Bound = bound; ConfigVersion = configVersion;
            }
        }

        /// <summary>从仓根烘焙(菜单 / 测试共用)。</summary>
        /// <param name="repoRoot">仓库根(含 <c>assets/</c> 的目录)。</param>
        /// <exception cref="BakeValidationException">词法 / 绑定 / 任一域门失败(聚合)。</exception>
        public static BakeOutput BakeFromRepo(string repoRoot)
        {
            if (repoRoot == null) throw new ArgumentNullException(nameof(repoRoot));
            string path = Path.Combine(repoRoot, "assets", "data", FileName);
            return BakeFromSourceText(File.ReadAllText(path, Encoding.UTF8));
        }

        /// <summary>从源文本烘焙(测试负例入口;不落盘)。</summary>
        /// <exception cref="BakeValidationException">词法 / 绑定 / 任一域门失败(聚合)。</exception>
        public static BakeOutput BakeFromSourceText(string json)
        {
            BuildModulesBinder.BindResult bound = BuildModulesBinder.Bind(json);
            uint configVersion = ConfigVersionUtility.DeriveConfigVersion(new[]
            {
                new KeyValuePair<string, string>(FileName, json ?? string.Empty),
            });
            byte[] cooked = BuildModulesCookedWriter.Write(bound, configVersion);
            return new BakeOutput(cooked, bound, configVersion);
        }

        /// <summary>把绑定结果包成 24 侧要的闭集来源(<see cref="IFurnTypeSource"/>)。
        /// <para>⚠️ 本件的闭集 = **`furn_type` 非 null 的那些行的字符串** —— 它不是 `FURN_TYPES` 的真源
        /// (真源是 `item_database_furniture.json`),而是**跨表一致性检查的输入**:同一 `furn_type`
        /// 若在家具目录里不存在,就是两表漂移。对账在 `DataBakeMenu` 的生产链路里做。</para>
        /// </summary>
        public static IFurnTypeSource AsFurnTypeSource(in BuildModulesBinder.BindResult bound)
            => new ModuleFurnTypeSource(bound.Rows);

        /// <summary>本表声明的 `furn_type` 集合(去重;**不含 null**)。</summary>
        public static IReadOnlyCollection<string> DeclaredFurnTypes(
            in BuildModulesBinder.BindResult bound)
        {
            var set = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < bound.Rows.Count; i++)
            {
                string t = bound.Rows[i].FurnType;
                if (t != null && seen.Add(t)) set.Add(t);
            }
            return set;
        }

        private sealed class ModuleFurnTypeSource : IFurnTypeSource
        {
            private readonly IReadOnlyCollection<string> _types;

            internal ModuleFurnTypeSource(IReadOnlyList<BuildModulesBinder.ModuleRow> rows)
            {
                var set = new List<string>();
                var seen = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < rows.Count; i++)
                {
                    string t = rows[i].FurnType;
                    if (t != null && seen.Add(t)) set.Add(t);
                }
                _types = set;
            }

            public bool TryGetTypes(out IReadOnlyCollection<string> types)
            {
                types = _types;
                return _types != null && _types.Count > 0;
            }
        }
    }
}
