// `world_terrain.json`(地形表)的 deterministic cooked 写入器。
//
// 权威来源:
//   ADR-014 §二(同源两次烘焙逐位一致:固定字段顺序 · 零字典迭代 · 零本地时间;
//     `Fix` = raw `long`)
//   · `CookedFormat.cs`(20 字节头布局的唯一共参照)· `WorldTerrainBinder.BindResult`(数据唯一来源)
//   · ADR-015 §一之补(`slopeLimit` / `stepOffset` 由关卡工具量化后落盘)
//
// ⚠️ 载荷布局(全部 little-endian;固定字段顺序):
//   world_terrain.cooked
//     i32 row_count
//     row_count × { i32 terrain_id; i32 k_speed; i64 k_accel_raw; i64 k_decel_raw }
//     i32 world_size_x; i32 world_size_y; i32 world_size_z
//     i32 cell_count
//     cell_count × i32 speed_index          (指向上面行集的下标;Binder 已解析)
//     i32 slope_limit_deg_x100
//     i32 step_offset_mm
//
// ⚠️ **每格存的是「行下标」而不是 `terrain_id`** —— 解析在**烘焙期**做完(ADR-014 §二:
//    构建期校验、运行期零查表)。运行期 `TerrainCell.KSpeedIdx` 直接就是它,
//    少一次映射,也少一条「id→下标」的隐式规则。
//    ⚠️ 但行里**仍带 `terrain_id`**:它让读方可以断言「id 严格升序」「下标与 id 不漂移」,
//       并且在报文里能说「哪一行」而不是「第 0 行」。
//
// ⚠️ **`name` 不进 cooked** —— 它只服务作者可读性(JSON 里看得到)。把它烘进来需要一套
//    字符串编码,而运行期零消费者:那是为一个不存在的需求付格式演进的代价。
//
// ⚠️ 控制器参数**放在载荷末尾**:它们是「附在世界上的两个标量」,与表 / 每格数组是两类东西;
//    放末尾让「加第三个标量」= 行尾追加(承 `BuildModulesCookedWriter` 的 schema 演进纪律)。

using System;
using System.IO;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>地形表的 deterministic cooked 写入器。</summary>
    internal static class WorldTerrainCookedWriter
    {
        /// <summary>编码地形表(含 20 字节头)。</summary>
        /// <exception cref="InvalidOperationException">表为空 / 每格数组为空(Binder 的域门应已拦)。</exception>
        public static byte[] Write(in WorldTerrainBinder.BindResult bound, uint configVersion)
        {
            if (bound.Rows == null || bound.Rows.Count == 0)
                throw new InvalidOperationException(
                    "地形表绑定结果为空 ⇒ 拒绝落盘(F-6-2:max over 空集无定义;Binder 的空表门应已拦)");
            if (bound.CellSpeedIndex == null || bound.CellSpeedIndex.Count == 0)
                throw new InvalidOperationException(
                    "地形表绑定结果的每格数组为空 ⇒ 拒绝落盘(表没有消费者等于没有地形)");

            using (var payload = new MemoryStream())
            using (var w = new BinaryWriter(payload, System.Text.Encoding.UTF8))
            {
                w.Write(bound.Rows.Count);
                for (int i = 0; i < bound.Rows.Count; i++)
                {
                    WorldTerrainBinder.TerrainRow r = bound.Rows[i];
                    w.Write(r.TerrainId);
                    w.Write(r.KSpeed);
                    w.Write(r.KAccel.Raw);     // Q16.16 raw(Fix 在 cooked 里就是 long,ADR-014 §二)
                    w.Write(r.KDecel.Raw);
                }

                w.Write(bound.Width);
                w.Write(bound.Height);
                w.Write(bound.Depth);
                w.Write(bound.CellSpeedIndex.Count);
                for (int i = 0; i < bound.CellSpeedIndex.Count; i++)
                    w.Write(bound.CellSpeedIndex[i]);

                w.Write(bound.SlopeLimitDegX100);
                w.Write(bound.StepOffsetMm);

                w.Flush();
                return ClinicMachineCookedWriter.FinishHeader(
                    payload.ToArray(), bound.SchemaVersion, configVersion);
            }
        }
    }
}
