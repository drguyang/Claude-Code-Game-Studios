// `world_poi.json`(POI **定义**)的 deterministic cooked 写入器。
//
// 权威来源:
//   ADR-014 §二(同源两次烘焙逐位一致:固定字段顺序 · 零字典迭代 · 零本地时间)
//   · `CookedFormat.cs`(20 字节头布局的唯一共参照)· `WorldPoiBinder.BindResult`(数据唯一来源)
//
// ⚠️ 载荷布局(全部 little-endian;固定字段顺序):
//   world_poi.cooked
//     i32 poi_count
//     poi_count × { i32 poi_id; i32 x; i32 y; i32 z; i32 type; i32 guard_ref }   (每行 24 B)
//
// ⚠️ **`level_size` 不进 cooked** —— 它只服务烘焙期的越界门。
//    运行期填格用的是**几何产物**声明的尺寸,两处再存一份 = 第二个真源。
//
// ⚠️ **POI 的格是三维三元组**(与生态区多边形的 `[x, z]` 二元组**不同**):
//    POI 是「世界里的一个点」,y 有意义(第二层的 POI 与第一层不是同一个);
//    生态区是**平面划区**,y 不参与(GDD F-6-3 §四)。两者形状不同是**语义**差异,不是笔误。
//
// ⚠️ 行序 = `poi_id` 升序(Binder 已排)—— cooked 的行序是产物的一部分(ADR-014 §二)。

using System;
using System.Collections.Generic;
using System.IO;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>POI 定义的 deterministic cooked 写入器。</summary>
    internal static class WorldPoiCookedWriter
    {
        /// <summary>编码 POI 定义(含 20 字节头)。</summary>
        /// <exception cref="InvalidOperationException">行集为空(Binder 应已拦)。</exception>
        public static byte[] Write(in WorldPoiBinder.BindResult bound, uint configVersion)
        {
            if (bound.Pois == null || bound.Pois.Count == 0)
                throw new InvalidOperationException(
                    "POI 绑定结果为空 ⇒ 拒绝落盘(空表与「未声明」不可分,且会让 `_poiIdSet` 零成员)");

            using (var payload = new MemoryStream())
            using (var w = new BinaryWriter(payload, System.Text.Encoding.UTF8))
            {
                w.Write(bound.Pois.Count);
                for (int i = 0; i < bound.Pois.Count; i++)
                {
                    DaYiJingCheng.EditorTools.Level.PoiSpec p = bound.Pois[i];
                    w.Write(p.PoiId);
                    w.Write(p.Cell.X);
                    w.Write(p.Cell.Y);
                    w.Write(p.Cell.Z);
                    w.Write(p.Type);
                    w.Write(p.GuardRef);
                }
                w.Flush();
                return ClinicMachineCookedWriter.FinishHeader(
                    payload.ToArray(), bound.SchemaVersion, configVersion);
            }
        }
    }
}
