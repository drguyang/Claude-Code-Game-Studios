// O-24-1 · clinic-machine / 21a —— 家具目录(FURN_TABLE)的烘焙入口(ADR-014 两阶段)。
//
// 权威来源:
//   ADR-014 §二/§三(阶段1 词法 → 阶段2 绑定 → cooked;玩家构建零 JSON 解析器)
//   GDD design/gdd/clinic-machine.md `:297`(家具目录归 21a)
//   `TD-clinic-01` / 路线图 §三 G-8
//
// ⚠️ **本件刻意不碰 `UnityEngine` / Addressables**(承 `DelayTable53Bake` / `ClinicMachineTablesBake`
//   同款头注):EditMode 测试可直驱,菜单层才负责落盘与组注册。
//
// ⚠️ **闭集的消费方式**:24 侧(`ClinicMachineTablesBake` / `ClinicMachineRoomBinder`)只经
//   `IFurnTypeSource` 注入取 `FURN_TYPES`;`AsFurnTypeSource()` 是本件提供给生产接线的那一份实现。
//   24 侧**不得**自造类型清单 —— 否则两处真源。

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>家具目录的烘焙入口。</summary>
    public static class ItemDatabaseFurnitureBake
    {
        /// <summary>作者态源文件名(assets/data/ 下;ConfigVersion 哈希的命名键)。</summary>
        public const string FileName = ItemDatabaseFurnitureBinder.FileName;

        /// <summary>烘焙产物名(`Assets/DataCooked/` 下;Addressables 地址同此名)。</summary>
        public const string CookedFileName = "item_database_furniture.cooked.bytes";

        /// <summary>烘焙输出。</summary>
        public readonly struct BakeOutput
        {
            /// <summary>cooked 字节(含 20 字节头)。</summary>
            public readonly byte[] Cooked;

            /// <summary>绑定结果(行集 / tier_max / schema_version)。</summary>
            public readonly ItemDatabaseFurnitureBinder.BindResult Bound;

            /// <summary>源内容哈希派生的 ConfigVersion(u32;ADR-014 §五)。</summary>
            public readonly uint ConfigVersion;

            /// <summary>构造输出。</summary>
            public BakeOutput(byte[] cooked, ItemDatabaseFurnitureBinder.BindResult bound, uint configVersion)
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
            ItemDatabaseFurnitureBinder.BindResult bound = ItemDatabaseFurnitureBinder.Bind(json);
            uint configVersion = ConfigVersionUtility.DeriveConfigVersion(new[]
            {
                new KeyValuePair<string, string>(FileName, json ?? string.Empty),
            });
            byte[] cooked = ItemDatabaseFurnitureCookedWriter.Write(bound, configVersion);
            return new BakeOutput(cooked, bound, configVersion);
        }

        /// <summary>把绑定结果包成 24 侧要的闭集来源(<see cref="IFurnTypeSource"/>)。</summary>
        public static IFurnTypeSource AsFurnTypeSource(
            in ItemDatabaseFurnitureBinder.BindResult bound)
            => new BakedFurnTypeSource(bound.Types());

        private sealed class BakedFurnTypeSource : IFurnTypeSource
        {
            private readonly IReadOnlyCollection<string> _types;

            internal BakedFurnTypeSource(IReadOnlyCollection<string> types) { _types = types; }

            public bool TryGetTypes(out IReadOnlyCollection<string> types)
            {
                types = _types;
                return _types != null && _types.Count > 0;
            }
        }
    }
}
