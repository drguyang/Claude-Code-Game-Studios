// `world_resources.json`(资源点)的 deterministic cooked 写入器。
//
// 权威来源:
//   ADR-014 §二(同源两次烘焙逐位一致)· `CookedFormat.cs`(20 字节头)
//   · `WorldResourcesBinder.BindResult`(数据唯一来源)
//
// ⚠️ 载荷布局(全部 little-endian;固定字段顺序):
//   world_resources.cooked
//     i32 res_count
//     res_count × { i32 resource_id; i32 x; i32 y; i32 z; i32 ecozone_id }   (每行 20 B)
//
// ⚠️ **`level_size` 不进 cooked**(与 `world_poi` 同理:运行期用几何产物的尺寸)。
//
// ⚠️ 行序 = `(resource_id, x, y, z, ecozone_id)` 升序(Binder 已排)—— cooked 的行序
//    是产物的一部分(ADR-014 §二)。排序键含格坐标是**必须**的:同一 `resource_id`
//    会出现在多个格上(资源 id 是**种类**,不是实例),只按 id 排序时相等元素次序不定。

using System;
using System.Collections.Generic;
using System.IO;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>资源点的 deterministic cooked 写入器。</summary>
    internal static class WorldResourcesCookedWriter
    {
        /// <summary>编码资源点(含 20 字节头)。</summary>
        /// <exception cref="InvalidOperationException">行集为空(Binder 应已拦)。</exception>
        public static byte[] Write(in WorldResourcesBinder.BindResult bound, uint configVersion)
        {
            if (bound.Resources == null || bound.Resources.Count == 0)
                throw new InvalidOperationException(
                    "资源点绑定结果为空 ⇒ 拒绝落盘(空表与「未声明」不可分,且会让 ResourceGrid 全 -1)");

            using (var payload = new MemoryStream())
            using (var w = new BinaryWriter(payload, System.Text.Encoding.UTF8))
            {
                w.Write(bound.Resources.Count);
                for (int i = 0; i < bound.Resources.Count; i++)
                {
                    DaYiJingCheng.EditorTools.Level.ResourceSpec r = bound.Resources[i];
                    w.Write(r.ResourceId);
                    w.Write(r.Cell.X);
                    w.Write(r.Cell.Y);
                    w.Write(r.Cell.Z);
                    w.Write(r.EcozoneId);
                }
                w.Flush();
                return ClinicMachineCookedWriter.FinishHeader(
                    payload.ToArray(), bound.SchemaVersion, configVersion);
            }
        }
    }
}
