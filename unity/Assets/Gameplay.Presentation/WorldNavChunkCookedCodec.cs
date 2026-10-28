// `world_nav_{chunk}.cooked.bytes`(导航格切片)的**运行期解码器**。
//
// 权威来源:
//   ADR-014 §二/§五(运行期读烘焙产物)· `CookedFormat.cs`(20 字节头布局契约)
//   · ADR-022 §五(导航格按 chunk 切片)· ADR-015 §四(chunk 是流式粒度,非坐标原点)
//   `production/epics/level-tool/story-005-world-nav-chunk-slicing.md` AC-1 / AC-5
//   写方严格镜像:`Editor.Tools.Bake/WorldNavChunkCookedWriter.cs`(字段序逐字对齐)
//
// ⚠️ 载荷布局(全部 little-endian;固定字段顺序):
//   world_nav_{chunk}.cooked
//     i32 world_size_x; i32 world_size_y; i32 world_size_z;
//     i32 chunk_size_x; i32 chunk_size_y; i32 chunk_size_z;
//     i32 chunk_index;
//     i32 chunk_cell_count;
//     chunk_cell_count × i32 walkable(0/1)
//
// ⚠️ **本文件为什么住在 `Gameplay.Presentation`**:与 `WorldGeometryCookedCodec` 同因 ——
//    读 cooked 字节要 `BinaryReader` + `CookedReadUtil`,而 `Sim` 的引用集恰 =
//    {BCL, Sim.Contracts}(ADR-025 §①),放不进 `Sim`。字节 → 数据的映射由装配侧承担。
//
// ⚠️ 任何字段缺失 / 魔数不符 / 长度不符 / 尺寸非法 / chunk 索引越界 ⇒ 显式 throw(启动期硬失败,E-13)。
//
// ⚠️ **`chunk` 三元组不落盘**(见写方注):本件用 `ChunkTopology.IndexToChunk` 从索引反推,
//    与「文件名里的 chunk 键」出自**同一函数** ⇒ 两者不可能分家。这正是 story-005 AC-2②
//    要的那条约束在**读侧**的落实。

using System;
using System.IO;
using DaYiJingCheng.Sim.Contracts;   // WorldPos(Chunk 属性)
using DaYiJingCheng.Sim.World;       // ChunkTopology(chunk 划分的唯一函数)

namespace DaYiJingCheng.Gameplay.Presentation
{
    /// <summary>`world_nav_{chunk}.cooked.bytes` 的解码结果(一块导航格切片)。</summary>
    public readonly struct WorldNavChunkDataSet
    {
        /// <summary>产物名主干(Addressables 地址 = 主干 + `_{chunk}` + `.cooked.bytes`)。</summary>
        public const string ProductName = "world_nav";

        /// <summary>`{chunk}` 后缀 + 扩展名;完整地址 = <see cref="ProductName"/> + 本值。</summary>
        public const string CookedSuffix = ".cooked.bytes";

        /// <summary>某 chunk 的 cooked 产物名(`world_nav_0.cooked.bytes` …)。</summary>
        public static string CookedFileNameFor(int chunkIndex) =>
            ProductName + "_" + chunkIndex + CookedSuffix;

        /// <summary>世界尺寸 X(格)。</summary>
        public readonly int WorldSizeX;
        /// <summary>世界尺寸 Y(格)。</summary>
        public readonly int WorldSizeY;
        /// <summary>世界尺寸 Z(格)。</summary>
        public readonly int WorldSizeZ;

        /// <summary>chunk 边长 X(格)。</summary>
        public readonly int ChunkSizeX;
        /// <summary>chunk 边长 Y(格)。</summary>
        public readonly int ChunkSizeY;
        /// <summary>chunk 边长 Z(格)。</summary>
        public readonly int ChunkSizeZ;

        /// <summary>chunk 一维索引。</summary>
        public readonly int ChunkIndex;

        /// <summary>稠密可走性(0/1;chunk 内局部索引)。</summary>
        public readonly int[] Walkable;

        /// <summary>产物头声明的 schema 版本(ADR-014 §五)。</summary>
        public readonly uint SchemaVersion;

        /// <summary>源内容哈希派生的 ConfigVersion(u32;ADR-014 §五)。</summary>
        public readonly uint ConfigVersion;

        /// <summary>构造。</summary>
        public WorldNavChunkDataSet(
            int worldSizeX, int worldSizeY, int worldSizeZ,
            int chunkSizeX, int chunkSizeY, int chunkSizeZ,
            int chunkIndex, int[] walkable, uint schemaVersion, uint configVersion)
        {
            WorldSizeX = worldSizeX; WorldSizeY = worldSizeY; WorldSizeZ = worldSizeZ;
            ChunkSizeX = chunkSizeX; ChunkSizeY = chunkSizeY; ChunkSizeZ = chunkSizeZ;
            ChunkIndex = chunkIndex; Walkable = walkable;
            SchemaVersion = schemaVersion; ConfigVersion = configVersion;
        }

        /// <summary>本片的世界 + chunk 划分(经 <see cref="ChunkTopology.ForWorld"/> —— 与几何侧同源)。</summary>
        public ChunkTopology Topology =>
            ChunkTopology.ForWorld(WorldSizeX, WorldSizeY, WorldSizeZ, ChunkSizeX, ChunkSizeY, ChunkSizeZ);

        /// <summary>本块的 chunk 坐标(<see cref="ChunkTopology.IndexToChunk"/> 反推,不落盘)。</summary>
        public WorldPos Chunk => Topology.IndexToChunk(ChunkIndex);
    }

    /// <summary>导航格切片(<c>world_nav_{chunk}.cooked.bytes</c>)的运行期解码器。</summary>
    public static class WorldNavChunkCookedCodec
    {
        /// <summary>本产物的作者态 schema_version 期望值(per-product 口径)。</summary>
        /// <remarks>⚠️ 与 `Editor.Tools.Level.LevelExporter.NavChunkSchemaVersion` **必须同值** ——
        /// 两者不可互相引用(编辑期程序集不进构建),但绑定器的 `ReadSchemaVersion` 以**本常量**
        /// 为期望值 ⇒ 导出侧写成 2 而这里还是 1 时,烘焙**当场**报 `schema_version ≠ 期望`。</remarks>
        public const uint ExpectedSchemaVersion = 1u;

        /// <summary>读一块导航格切片。</summary>
        /// <param name="bytes">cooked 字节(含 20 字节头)。</param>
        /// <param name="expectedChunkIndex">文件名声明的 chunk 号(`-1` = 不校验,测试负例用)。</param>
        /// <exception cref="InvalidDataException">头部损坏 / 长度不符 / 尺寸非法 / chunk 索引越界 / 与文件名不符。</exception>
        public static WorldNavChunkDataSet Read(byte[] bytes, int expectedChunkIndex = -1)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));

            using (BinaryReader r = CookedReadUtil.OpenChecked(
                       bytes, WorldNavChunkDataSet.ProductName, out uint configVersion))
            {
                // ⚠️ 版本门排在读载荷**之前**(承 WorldGeometryCookedCodec 的教训):
                //    否则旧布局产物会先在某个 ReadInt32 撞 EndOfStreamException。
                uint schemaVersion = ReadU32Le(bytes, 8);
                if (schemaVersion != ExpectedSchemaVersion)
                    throw new InvalidDataException(
                        $"[cooked] {WorldNavChunkDataSet.ProductName} schemaVersion = {schemaVersion},期望 " +
                        $"{ExpectedSchemaVersion} —— 布局演进后不得按旧契约读(E-13)。" +
                        "重烘:大医精诚/数据管线/烘焙 world_nav");

                int wsX = r.ReadInt32();
                int wsY = r.ReadInt32();
                int wsZ = r.ReadInt32();
                if (wsX < 1 || wsY < 1 || wsZ < 1)
                    throw new InvalidDataException(
                        $"[cooked] {WorldNavChunkDataSet.ProductName} world_size = ({wsX},{wsY},{wsZ}) —— 三维须 ≥ 1");

                int csX = r.ReadInt32();
                int csY = r.ReadInt32();
                int csZ = r.ReadInt32();
                if (csX < 1 || csY < 1 || csZ < 1)
                    throw new InvalidDataException(
                        $"[cooked] {WorldNavChunkDataSet.ProductName} chunk_size = ({csX},{csY},{csZ}) —— 三维须 ≥ 1" +
                        "(为 0 时 ChunkTopology.ForWorld 必抛,报文会指向「数值非法」而不是「产物损坏」)");

                int chunkIndex = r.ReadInt32();

                long total = (long)csX * csY * csZ;
                if (total > int.MaxValue)
                    throw new InvalidDataException(
                        $"[cooked] {WorldNavChunkDataSet.ProductName} chunk 格数 {total} 超出 int 域");

                ChunkTopology topo = ChunkTopology.ForWorld(wsX, wsY, wsZ, csX, csY, csZ);
                if (chunkIndex < 0 || chunkIndex >= topo.ChunkCount)
                    throw new InvalidDataException(
                        $"[cooked] {WorldNavChunkDataSet.ProductName} chunk_index = {chunkIndex} 越界 —— " +
                        $"世界 ({wsX},{wsY},{wsZ}) / chunk ({csX},{csY},{csZ}) 共 {topo.ChunkCount} 块");

                if (expectedChunkIndex >= 0 && expectedChunkIndex != chunkIndex)
                    throw new InvalidDataException(
                        $"[cooked] {WorldNavChunkDataSet.ProductName} 文件名声明的 chunk 号 = {expectedChunkIndex}," +
                        $"而载荷里是 {chunkIndex} —— 把 A 片的字节装到 B 片的地址上," +
                        "运行期会静默读到邻块的导航格");

                int cellCount = r.ReadInt32();
                if (cellCount != (int)total)
                    throw new InvalidDataException(
                        $"[cooked] {WorldNavChunkDataSet.ProductName} chunk_cell_count = {cellCount}," +
                        $"与 chunk_size 的格数 {total} 不符 —— 长度字段与尺寸字段对不上 = 字节损坏或写读契约漂移");

                var walkable = new int[cellCount];
                for (int i = 0; i < cellCount; i++)
                {
                    int v = r.ReadInt32();
                    if (v != 0 && v != 1)
                        throw new InvalidDataException(
                            $"[cooked] {WorldNavChunkDataSet.ProductName} walkable[{i}] = {v} —— " +
                            "写方契约只写 0/1;非 0/1 必然是损坏或 bool→int 编码漂移");
                    walkable[i] = v;
                }

                // ⚠️ **不设「全 0」门** —— 与绑定期同一口径:一片全不可走是**合法**的
                //    (一整块实心区域),判据面归 C3 / C6,不归本 codec。
                CookedReadUtil.AssertFullyConsumed(r, WorldNavChunkDataSet.ProductName);

                return new WorldNavChunkDataSet(
                    wsX, wsY, wsZ, csX, csY, csZ, chunkIndex, walkable, schemaVersion, configVersion);
            }
        }

        /// <summary>读头部第 3 个 u32(offset 8 = 作者态 schema_version 的回声)。</summary>
        private static uint ReadU32Le(byte[] b, int offset) =>
            (uint)(b[offset] | (b[offset + 1] << 8) | (b[offset + 2] << 16) | (b[offset + 3] << 24));
    }
}
