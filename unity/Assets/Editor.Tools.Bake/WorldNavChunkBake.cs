// 导航格切片(`world_nav_{chunk}.json`)的烘焙入口(ADR-014 两阶段)。
//
// 权威来源:
//   ADR-014 §二/§三/§五(阶段1 词法 → 阶段2 绑定 → cooked;ConfigVersion = 源内容哈希;
//     玩家构建零 JSON 解析器)· ADR-022 §五(按 chunk 切片)
//
// ⚠️ **本件刻意不碰 `UnityEngine` / Addressables**(承 `WorldGeometryBake` 同款头注):
//    EditMode 测试可直驱,菜单层才负责落盘与组注册。
//
// ⚠️ **ConfigVersion 的命名键含 chunk 号**(`world_nav_{i}.json`):
//    若四份切片共用一个键,单改第 2 片的内容不会改第 0 片的 ConfigVersion ——
//    而存档头比对是**整组**比一次,局部漂移会被掩盖。

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>导航格切片的烘焙入口。</summary>
    public static class WorldNavChunkBake
    {
        /// <summary>某 chunk 的作者态源文件名。</summary>
        public static string FileNameFor(int chunkIndex) => WorldNavChunkBinder.FileNameFor(chunkIndex);

        // ⚠️ **此处曾有一个 `CookedFileNameFor(int)`**(转调 `WorldNavChunkDataSet.CookedFileNameFor`),
        //    本轮删除。理由(承 2026-10-28 评审 A-21「零消费方死 API」):
        //      ① 落盘处(`DataBakeMenu.BakeWorldNavChunks`)改从**登记表**
        //         `DataBakeMenu.WorldNavChunkCookedAssetNames` 取名 ⇒ 本方法零消费方;
        //      ② 留着它 = 给「产物名」留第二条算出路径,而两条路径只在「怎么数 chunk」上分家
        //         (静默:名字对得上时看不出差别,改 chunk 尺寸时才暴露)。
        //    参数化的**读侧**名字仍在 `WorldNavChunkDataSet.CookedFileNameFor`(它有真消费方
        //    `DataCookedDisk.LoadWorldNavChunk`),两者是否一致由夹具
        //    `nav_chunk_slicing_test::test_ac5_registeredNames_matchParameterizedName` 钉住。

        /// <summary>烘焙输出。</summary>
        public readonly struct BakeOutput
        {
            /// <summary>cooked 字节(含 20 字节头)。</summary>
            public readonly byte[] Cooked;
            /// <summary>绑定结果。</summary>
            public readonly WorldNavChunkBinder.BindResult Bound;
            /// <summary>源内容哈希派生的 ConfigVersion(u32;ADR-014 §五)。</summary>
            public readonly uint ConfigVersion;

            /// <summary>构造输出。</summary>
            public BakeOutput(byte[] cooked, WorldNavChunkBinder.BindResult bound, uint configVersion)
            {
                Cooked = cooked; Bound = bound; ConfigVersion = configVersion;
            }
        }

        /// <summary>从仓根烘焙某 chunk。</summary>
        /// <param name="repoRoot">仓库根(含 <c>assets/</c> 的目录)。</param>
        /// <param name="chunkIndex">chunk 一维索引。</param>
        /// <exception cref="BakeValidationException">词法 / 绑定 / 任一域门失败(聚合)。</exception>
        public static BakeOutput BakeFromRepo(string repoRoot, int chunkIndex)
        {
            if (repoRoot == null) throw new ArgumentNullException(nameof(repoRoot));
            string fileName = FileNameFor(chunkIndex);
            string path = Path.Combine(repoRoot, "assets", "data", fileName);
            if (!File.Exists(path))
                throw new BakeValidationException(new List<string>
                {
                    $"作者态源不存在:{path} —— 先跑「大医精诚/关卡工具/导出 p0_clinic 到 assets/data」" +
                    "(关卡工具是唯一作者,ADR-022 §二;不得手写该 JSON)",
                });
            return BakeFromSourceText(File.ReadAllText(path, Encoding.UTF8), chunkIndex);
        }

        /// <summary>从源文本烘焙(测试负例入口;不落盘)。</summary>
        /// <param name="json">作者态 JSON 文本。</param>
        /// <param name="chunkIndex">文件名声明的 chunk 号(`-1` = 不校验,负例用)。</param>
        /// <exception cref="BakeValidationException">词法 / 绑定 / 任一域门失败(聚合)。</exception>
        public static BakeOutput BakeFromSourceText(string json, int chunkIndex = -1)
        {
            WorldNavChunkBinder.BindResult bound = WorldNavChunkBinder.Bind(json, chunkIndex);
            uint configVersion = ConfigVersionUtility.DeriveConfigVersion(new[]
            {
                new KeyValuePair<string, string>(
                    chunkIndex >= 0 ? FileNameFor(chunkIndex) : WorldNavChunkBinder.FileNameFor(bound.ChunkIndex),
                    json ?? string.Empty),
            });
            byte[] cooked = WorldNavChunkCookedWriter.Write(bound, configVersion);
            return new BakeOutput(cooked, bound, configVersion);
        }
    }
}
