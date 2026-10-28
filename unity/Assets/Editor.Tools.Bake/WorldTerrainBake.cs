// 地形表(`world_terrain.json`)的烘焙入口(ADR-014 两阶段)。
//
// 权威来源:
//   ADR-014 §二/§三/§五(阶段1 词法 → 阶段2 绑定 → cooked;ConfigVersion = 源内容哈希;
//     玩家构建零 JSON 解析器)· ADR-022 §三(导出契约)· GDD F-6-2 / AC-6-08
//
// ⚠️ **本件刻意不碰 `UnityEngine` / Addressables**(承 `WorldBuildSlotsBake` 同款头注):
//    EditMode 测试可直驱,菜单层才负责落盘与组注册。

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>地形表的烘焙入口。</summary>
    public static class WorldTerrainBake
    {
        /// <summary>作者态源文件名(assets/data/ 下;ConfigVersion 哈希的命名键)。</summary>
        public const string FileName = WorldTerrainBinder.FileName;

        /// <summary>烘焙产物名(`Assets/DataCooked/` 下;Addressables 地址同此名)。</summary>
        public const string CookedFileName = "world_terrain.cooked.bytes";

        /// <summary>烘焙输出。</summary>
        public readonly struct BakeOutput
        {
            /// <summary>cooked 字节(含 20 字节头)。</summary>
            public readonly byte[] Cooked;

            /// <summary>绑定结果(表 / 每格下标 / 控制器参数)。</summary>
            public readonly WorldTerrainBinder.BindResult Bound;

            /// <summary>源内容哈希派生的 ConfigVersion(u32;ADR-014 §五)。</summary>
            public readonly uint ConfigVersion;

            /// <summary>构造输出。</summary>
            public BakeOutput(byte[] cooked, WorldTerrainBinder.BindResult bound, uint configVersion)
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
            if (!File.Exists(path))
                throw new BakeValidationException(new List<string>
                {
                    $"作者态源不存在:{path} —— 先跑「大医精诚/关卡工具/导出 p0_clinic 到 assets/data」" +
                    "(关卡工具是唯一作者,ADR-022 §二;不得手写该 JSON)",
                });
            return BakeFromSourceText(File.ReadAllText(path, Encoding.UTF8));
        }

        /// <summary>从源文本烘焙(测试负例入口;不落盘)。</summary>
        /// <exception cref="BakeValidationException">词法 / 绑定 / 任一域门失败(聚合)。</exception>
        public static BakeOutput BakeFromSourceText(string json)
        {
            WorldTerrainBinder.BindResult bound = WorldTerrainBinder.Bind(json);
            uint configVersion = ConfigVersionUtility.DeriveConfigVersion(new[]
            {
                new KeyValuePair<string, string>(FileName, json ?? string.Empty),
            });
            byte[] cooked = WorldTerrainCookedWriter.Write(bound, configVersion);
            return new BakeOutput(cooked, bound, configVersion);
        }
    }
}
