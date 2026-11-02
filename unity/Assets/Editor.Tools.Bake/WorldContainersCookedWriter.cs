// `world_containers.json`(开档容器物品实例)的 deterministic cooked 写入器。
//
// 权威来源:
//   ADR-014 §二(同源两次烘焙逐位一致:固定字段顺序 · 零字典迭代 · 零本地时间)
//   · `CookedFormat.cs`(20 字节头布局的唯一共参照)
//   · `WorldContainersBinder.BindResult`(行集唯一来源)
//
// ⚠️ 载荷布局(全部 little-endian;固定字段顺序):
//   world_containers.cooked
//     i32 world_size_x
//     i32 world_size_y
//     i32 world_size_z
//     i32 row_count
//     row_count × { i64 instance_id; i32 x; i32 y; i32 z }   (每行 20 B)
//
// ⚠️ **行序 = `instance_id` 升序**(不是声明序)—— 声明序无关语义,重排零信息。
// ⚠️ **`instance_id` 写 i64**(与 `StructurePlacedPayload.structure_id` / 掉落 `instance_id` 同宽);
//    写 i32 会与 item instance 的 `long` 空间在 21 亿处静默截断。
// ⚠️ **schema_version 只住头槽**(offset 8,单一出处)。

using System;
using System.Collections.Generic;
using System.IO;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>开档容器实例的 deterministic cooked 写入器。</summary>
    internal static class WorldContainersCookedWriter
    {
        /// <summary>编码容器实例集(含 20 字节头)。</summary>
        /// <exception cref="InvalidOperationException">行集为空(Binder 的空表校验应已拦)。</exception>
        public static byte[] Write(in WorldContainersBinder.BindResult bound, uint configVersion)
        {
            var rows = new List<WorldContainersBinder.ContainerRow>(bound.Rows);
            if (rows.Count == 0)
                throw new InvalidOperationException(
                    "容器实例绑定行集为空 ⇒ 拒绝落盘(Binder 的空表校验应已拦)");

            rows.Sort((a, b) => a.InstanceId.CompareTo(b.InstanceId));

            using (var payload = new MemoryStream())
            using (var w = new BinaryWriter(payload, System.Text.Encoding.UTF8))
            {
                w.Write(bound.Width);
                w.Write(bound.Height);
                w.Write(bound.Depth);
                w.Write(rows.Count);
                for (int i = 0; i < rows.Count; i++)
                {
                    WorldContainersBinder.ContainerRow r = rows[i];
                    w.Write(r.InstanceId);   // i64
                    w.Write(r.X);
                    w.Write(r.Y);
                    w.Write(r.Z);
                }
                w.Flush();
                return ClinicMachineCookedWriter.FinishHeader(
                    payload.ToArray(), bound.SchemaVersion, configVersion);
            }
        }
    }
}
