// `world_geometry.json`(世界几何)的 deterministic cooked 写入器。
//
// 权威来源:
//   ADR-014 §二(同源两次烘焙逐位一致:固定字段顺序 · 零字典迭代 · 零本地时间)
//   · `CookedFormat.cs`(20 字节头布局的唯一共参照)· `WorldGeometryBinder.BindResult`(数据唯一来源)
//   · ADR-015 §三(格边长以整数毫米表达)
//
// ⚠️ 载荷布局(全部 little-endian;固定字段顺序):
//   world_geometry.cooked
//     i32 world_size_x
//     i32 world_size_y
//     i32 world_size_z
//     i32 lattice_size_mm
//     i32 cell_count
//     cell_count × i32 walkable          (0/1)
//
// ⚠️ **`cell_count` 与 `world_size` 冗余** —— 刻意保留:它让读方可以「先按长度分配、再断言
//    `cell_count == W×H×D`」,把「长度字段被改坏」与「尺寸字段被改坏」分成两条可区分的失败,
//    而不是让一条越界读或一条 `EndOfStreamException` 吞掉两者(承 `WorldBuildSlotsCookedCodec`
//    的 schema 门教训:报文必须直指真因)。
//
// ⚠️ **行序 = `(x*H + y)*D + z` 的索引序**(= 作者数组序),不是声明序以外的东西 ——
//    可走性数组本身就是按格的**位置**组织的,没有「重排零信息」的余地。
//
// ⚠️ **`walkable` 写 i32 而非 u8** —— 与 `baked_initial` / `refundable` 同款
//    (读方不必为布尔开特例)。代价是 4 B/格:P0 的 256 格 = 1 KB,可忽略。

using System;
using System.IO;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>世界几何的 deterministic cooked 写入器。</summary>
    internal static class WorldGeometryCookedWriter
    {
        /// <summary>编码世界几何(含 20 字节头)。</summary>
        /// <exception cref="InvalidOperationException">可走性数组为空(Binder 的域门应已拦)。</exception>
        public static byte[] Write(in WorldGeometryBinder.BindResult bound, uint configVersion)
        {
            if (bound.Walkable == null || bound.Walkable.Count == 0)
                throw new InvalidOperationException(
                    "世界几何绑定结果的可走性数组为空 ⇒ 拒绝落盘" +
                    "(Binder 的长度门 / 全不可走门应已拦)");

            using (var payload = new MemoryStream())
            using (var w = new BinaryWriter(payload, System.Text.Encoding.UTF8))
            {
                w.Write(bound.Width);
                w.Write(bound.Height);
                w.Write(bound.Depth);
                w.Write(bound.LatticeSizeMm);
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
