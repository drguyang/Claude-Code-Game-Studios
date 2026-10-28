// `world_nav_{chunk}.json` 的 deterministic cooked 写入器。
//
// 权威来源:
//   ADR-014 §二(同源两次烘焙逐位一致:固定字段顺序 · 零字典迭代 · 零本地时间)
//   · `CookedFormat.cs`(20 字节头布局的唯一共参照)· `WorldNavChunkBinder.BindResult`(数据唯一来源)
//
// ⚠️ 载荷布局(全部 little-endian;固定字段顺序):
//   world_nav_{chunk}.cooked
//     i32 world_size_x; i32 world_size_y; i32 world_size_z;
//     i32 chunk_size_x; i32 chunk_size_y; i32 chunk_size_z;
//     i32 chunk_index;
//     i32 chunk_cell_count;
//     chunk_cell_count × i32 walkable(0/1)
//
// ⚠️ **`chunk` 三元组不落盘** —— 它由 `(chunk_index, world_size, chunk_size)` 经
//    `ChunkTopology.IndexToChunk` **唯一确定**,存第二份就多一个可以和索引打架的字段
//    (而 `WorldNavChunkBinder` 在作者态已经把「两者是否自洽」判过一次)。
//    落盘只留索引 ⇒ 运行期读到的 chunk 坐标与「文件名里的 chunk 键」由同一函数算出。
//
// ⚠️ **`world_size` 与 `chunk_size` 冗余写进每一份** —— 与 `world_geometry` 同款理由:
//    读方可以「先断言这份切片属于哪个世界、按什么划分」再读格,把「产物配错了世界」
//    与「字节损坏」分成两条可区分的失败。

using System;
using System.IO;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>导航格切片的 deterministic cooked 写入器。</summary>
    internal static class WorldNavChunkCookedWriter
    {
        /// <summary>编码一份切片(含 20 字节头)。</summary>
        /// <exception cref="InvalidOperationException">可走性数组为空(Binder 的长度门应已拦)。</exception>
        public static byte[] Write(in WorldNavChunkBinder.BindResult bound, uint configVersion)
        {
            if (bound.Walkable == null || bound.Walkable.Count == 0)
                throw new InvalidOperationException(
                    "导航格切片绑定结果的可走性数组为空 ⇒ 拒绝落盘(Binder 的长度门应已拦)");

            using (var payload = new MemoryStream())
            using (var w = new BinaryWriter(payload, System.Text.Encoding.UTF8))
            {
                w.Write(bound.WorldSizeX);
                w.Write(bound.WorldSizeY);
                w.Write(bound.WorldSizeZ);
                w.Write(bound.ChunkSizeX);
                w.Write(bound.ChunkSizeY);
                w.Write(bound.ChunkSizeZ);
                w.Write(bound.ChunkIndex);
                w.Write(bound.Walkable.Count);
                for (int i = 0; i < bound.Walkable.Count; i++)
                    w.Write(bound.Walkable[i]);
                w.Flush();
                return ClinicMachineCookedWriter.FinishHeader(
                    payload.ToArray(), bound.SchemaVersion, configVersion);
            }
        }
    }
}
