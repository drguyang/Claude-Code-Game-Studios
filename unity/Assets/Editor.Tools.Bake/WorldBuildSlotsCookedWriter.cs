// `world_buildslots.json`(建造槽位骨架)的 deterministic cooked 写入器。
//
// 权威来源:
//   ADR-014 §二(同源两次烘焙逐位一致:固定字段顺序 · 零字典迭代 · 零本地时间)
//   · `CookedFormat.cs`(20 字节头布局的唯一共参照)· `WorldBuildSlotsBinder.BindResult`(行集唯一来源)
//   · ADR-015 §三(整数格)
//
// ⚠️ 载荷布局(全部 little-endian;固定字段顺序):
//   world_buildslots.cooked
//     i32 world_size_x
//     i32 world_size_y
//     i32 world_size_z
//     i32 row_count
//     row_count × { i32 x; i32 y; i32 z; i32 allowed_mask; i32 baked_initial; i32 module_id }
//
// ⚠️ **`module_id` 是 schema 2 追加的行尾字段**(2026-10-28 · S20-20 挂标 #2)。
//    行尾追加而非插在中间 —— 承 `BuildModulesCookedWriter` 的 schema 演进纪律:
//    中间插入会让「旧读方按旧长度读」变成**错位**而非**拒收**。
//
// ⚠️ **行序 = (x, y, z) 升序**而非声明序 —— 声明序在作者重排 `slots[]` 时会静默改变字节流,
//    而槽位骨架是**集合**(重排零信息)。这条口径与 `BuildModulesCookedWriter` 的
//    「行序一律全序」同源。
//
// ⚠️ **`baked_initial` 写 i32(0/1)而非 u8** —— 与 `refundable` 同款(读方不必为布尔开特例)。
// ⚠️ **新字段一律追加在行尾**(承 `BuildModulesCookedWriter` 的 schema 演进纪律)。
// ⚠️ **schema_version 只住头槽**(offset 8,单一出处)。

using System;
using System.Collections.Generic;
using System.IO;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>建造槽位骨架的 deterministic cooked 写入器。</summary>
    internal static class WorldBuildSlotsCookedWriter
    {
        /// <summary>编码槽位骨架(含 20 字节头)。</summary>
        /// <exception cref="InvalidOperationException">行集为空(Binder 的空表校验应已拦)。</exception>
        public static byte[] Write(in WorldBuildSlotsBinder.BindResult bound, uint configVersion)
        {
            var rows = new List<WorldBuildSlotsBinder.SlotRow>(bound.Rows);
            if (rows.Count == 0)
                throw new InvalidOperationException(
                    "槽位骨架绑定行集为空 ⇒ 拒绝落盘(Binder 的空表校验应已拦;" +
                    "空骨架让 PlaceableChecker 恒 SlotNotFound = 建造系统静默失效)");

            rows.Sort(CompareCell);

            using (var payload = new MemoryStream())
            using (var w = new BinaryWriter(payload, System.Text.Encoding.UTF8))
            {
                w.Write(bound.Width);
                w.Write(bound.Height);
                w.Write(bound.Depth);
                w.Write(rows.Count);
                for (int i = 0; i < rows.Count; i++)
                {
                    WorldBuildSlotsBinder.SlotRow r = rows[i];
                    w.Write(r.X);
                    w.Write(r.Y);
                    w.Write(r.Z);
                    w.Write(r.AllowedMask);            // 1 << TypeOrdinal
                    w.Write(r.BakedInitial ? 1 : 0);
                    w.Write(r.ModuleId);               // schema 2 行尾追加;-1 = 无既有家具
                }
                w.Flush();
                return ClinicMachineCookedWriter.FinishHeader(
                    payload.ToArray(), bound.SchemaVersion, configVersion);
            }
        }

        /// <summary>按 `(x, y, z)` 升序(全序;Binder 已拒重复格)。</summary>
        private static int CompareCell(
            WorldBuildSlotsBinder.SlotRow a, WorldBuildSlotsBinder.SlotRow b)
        {
            int cmp = a.X.CompareTo(b.X);
            if (cmp != 0) return cmp;
            cmp = a.Y.CompareTo(b.Y);
            if (cmp != 0) return cmp;
            return a.Z.CompareTo(b.Z);
        }
    }
}
