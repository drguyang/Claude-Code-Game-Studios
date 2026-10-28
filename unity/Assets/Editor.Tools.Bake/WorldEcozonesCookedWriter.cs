// `world_ecozones.json`(生态区多边形)的 deterministic cooked 写入器。
//
// 权威来源:
//   ADR-014 §二(同源两次烘焙逐位一致:固定字段顺序 · 零字典迭代 · 零本地时间)
//   · `CookedFormat.cs`(20 字节头布局的唯一共参照)· `WorldEcozonesBinder.BindResult`(数据唯一来源)
//   · GDD F-6-3 §四(y 不参与 ⇒ 顶点只写 x / z)
//
// ⚠️ 载荷布局(全部 little-endian;固定字段顺序):
//   world_ecozones.cooked
//     i32 zone_count
//     zone_count × { i32 ecozone_id; i32 vertex_count; vertex_count × { i32 x; i32 z } }
//
// ⚠️ **顶点只写 `x` / `z`,不写 `y`** —— GDD F-6-3 §四 逐字「横轴 = 与射线方向垂直的那个轴,
//    不是 y」,而 runtime `EcozonePolygon` 的 `Contains` / `IsBoundary` / `Cross` 从不读 `Y`。
//    把恒 0 的 y 写进产物 = 每区多 `顶点数 × 4` 字节换一个**永远不能变的常量**。
//    ⚠️ 这与「行尾追加」的 schema 演进纪律不冲突:本产物 schema 1 起就**没有** y,
//    将来若要分层(GDD 说须先扩 F-6-3 + AC-6-19),那次是**升 schema 号**而不是就地加字段。
//
// ⚠️ **`name` 不进 cooked** —— 只服务作者可读性,运行期零消费者(与地形表同理)。
// ⚠️ 行序 = `ecozone_id` 升序(Binder 已排);顶点序 = **声明序**(它对多边形是**有意义的**:
//    顺/逆时针与起点都影响射线法的边遍历,但结果不变 —— 故不重排,重排会让字节随作者
//    的「从哪个顶点开始写」而变,那是无谓的不确定性)。

using System;
using System.Collections.Generic;
using System.IO;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>生态区多边形的 deterministic cooked 写入器。</summary>
    internal static class WorldEcozonesCookedWriter
    {
        /// <summary>编码生态区多边形(含 20 字节头)。</summary>
        /// <exception cref="InvalidOperationException">行集为空(Binder 的 C4 应已拦)。</exception>
        public static byte[] Write(in WorldEcozonesBinder.BindResult bound, uint configVersion)
        {
            if (bound.Zones == null || bound.Zones.Count == 0)
                throw new InvalidOperationException(
                    "生态区绑定结果为空 ⇒ 拒绝落盘(空表与「未声明」不可分,且会让 EcozoneOf 恒回 None)");

            using (var payload = new MemoryStream())
            using (var w = new BinaryWriter(payload, System.Text.Encoding.UTF8))
            {
                w.Write(bound.Zones.Count);
                for (int i = 0; i < bound.Zones.Count; i++)
                {
                    DaYiJingCheng.EditorTools.Level.EcozoneZoneSpec z = bound.Zones[i];
                    w.Write(z.Id);
                    w.Write(z.Vertices.Length);
                    for (int v = 0; v < z.Vertices.Length; v++)
                    {
                        w.Write(z.Vertices[v].X);
                        w.Write(z.Vertices[v].Z);
                    }
                }
                w.Flush();
                return ClinicMachineCookedWriter.FinishHeader(
                    payload.ToArray(), bound.SchemaVersion, configVersion);
            }
        }
    }
}
