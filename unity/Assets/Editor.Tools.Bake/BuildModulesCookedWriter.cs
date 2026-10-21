// `build_modules.json`(23 模块目录)的 deterministic cooked 写入器。
//
// 权威来源:
//   ADR-014 §二(同源两次烘焙逐位一致:固定字段顺序 · 零字典迭代 · 零本地时间)
//   · CookedFormat.cs(20 字节头布局的唯一共参照)
//   · BuildModulesBinder.BindResult(行集唯一来源)
//
// ⚠️ 载荷布局(全部 little-endian;固定字段顺序;字符串 = i32 字节长 + UTF-8,长度 -1 = null):
//   build_modules.cooked
//     i32 row_count
//     row_count × { i32 module_id; i32 module_type; str module_type_name; str furn_type;
//                   i32 occupancy_count; occupancy_count × { i32 x; i32 y; i32 z };
//                   i32 cost; i32 refundable }
//
// ⚠️ **行序 = `module_id` 升序**而非声明序 —— 承 clinic 侧「行序一律全序」的教训:
//   声明序在作者重排 `modules[]` 时会静默改变字节流(而目录是**集合**,重排无信息)。
//   `module_type_name` 与 `furn_type` 都写进去:`module_type`(枚举值)是判据输入,
//   字符串名是**错误报文与跨侧对账**的输入 —— 只写枚举值会让「SlotType 重排」无迹可查。
//
// ⚠️ **`refundable` 写 i32(0/1)而非 i1** —— 与 `ModuleDefinition.Refundable` 的 `int` 同型,
//   避免读方为一个布尔开特例。
//
// ⚠️ **schema_version 只住头槽**(offset 8,单一出处)。

using System;
using System.Collections.Generic;
using System.IO;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>模块目录的 deterministic cooked 写入器。</summary>
    internal static class BuildModulesCookedWriter
    {
        /// <summary>编码模块目录(含 20 字节头)。</summary>
        /// <exception cref="InvalidOperationException">行集为空(Binder 的空表校验应已拦)。</exception>
        public static byte[] Write(in BuildModulesBinder.BindResult bound, uint configVersion)
        {
            var rows = new List<BuildModulesBinder.ModuleRow>(bound.Rows);
            if (rows.Count == 0)
                throw new InvalidOperationException(
                    "模块目录绑定行集为空 ⇒ 拒绝落盘(Binder 的空表校验应已拦;" +
                    "空目录让放置判定 ⑤ 恒失败 = 建造系统静默失效)");

            rows.Sort(CompareModuleId);

            using (var payload = new MemoryStream())
            using (var w = new BinaryWriter(payload, System.Text.Encoding.UTF8))
            {
                w.Write(rows.Count);
                for (int i = 0; i < rows.Count; i++)
                {
                    BuildModulesBinder.ModuleRow r = rows[i];
                    w.Write(r.ModuleId);
                    w.Write(r.ModuleType);
                    ClinicMachineCookedWriter.WriteString(w, r.ModuleTypeName);
                    ClinicMachineCookedWriter.WriteString(w, r.FurnType);   // null ⇒ -1
                    w.Write(r.OccupancyLocal.Count);
                    for (int c = 0; c < r.OccupancyLocal.Count; c++)
                    {
                        w.Write(r.OccupancyLocal[c].X);
                        w.Write(r.OccupancyLocal[c].Y);
                        w.Write(r.OccupancyLocal[c].Z);
                    }
                    w.Write(r.Cost);
                    w.Write(r.Refundable ? 1 : 0);
                }
                w.Flush();
                return ClinicMachineCookedWriter.FinishHeader(
                    payload.ToArray(), bound.SchemaVersion, configVersion);
            }
        }

        /// <summary>按 `module_id` 升序(全序;Binder 已拒重复 id)。</summary>
        private static int CompareModuleId(
            BuildModulesBinder.ModuleRow a, BuildModulesBinder.ModuleRow b)
            => a.ModuleId.CompareTo(b.ModuleId);
    }
}
