// O-24-1 · clinic-machine / 21a —— 家具目录(FURN_TABLE)的 deterministic cooked 写入器。
//
// 权威来源:
//   ADR-014 §二(同源两次烘焙逐位一致:固定字段顺序 · 零字典迭代 · 零本地时间)
//   · CookedFormat.cs(20 字节头布局的唯一共参照)
//   · ItemDatabaseFurnitureBinder.BindResult(行集唯一来源)
//
// ⚠️ 载荷布局(全部 little-endian;固定字段顺序;字符串 = i32 字节长 + UTF-8,长度 -1 = null):
//   item_database_furniture.cooked
//     i64 tier_max                 ← `TIER_MAX` 的**声明值**(OQ-24-2 = 3;24 侧对账)
//     i32 row_count
//     row_count × { str type; i32 tier; i64 e_env_raw }
//
// ⚠️ **行序 = `type` 的序数(ordinal)升序**而非声明序 —— 承 clinic 侧「行序一律全序」的教训:
//   声明序在作者重排 `furn[]` 时会静默改变字节流(而 `FURN_TYPES` 是集合,重排无信息)。
//   闭集与逐件属性都不依赖行序 ⇒ 排序不改变任何语义,只让新源同源两次烘焙逐位一致。
//
// ⚠️ **schema_version 只住头槽**(offset 8,单一出处)。

using System;
using System.Collections.Generic;
using System.IO;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>家具目录的 deterministic cooked 写入器。</summary>
    internal static class ItemDatabaseFurnitureCookedWriter
    {
        /// <summary>编码家具目录(含 20 字节头)。</summary>
        /// <exception cref="InvalidOperationException">行集为空(Binder 的空表校验应已拦)。</exception>
        public static byte[] Write(
            in ItemDatabaseFurnitureBinder.BindResult bound, uint configVersion)
        {
            var rows = new List<ItemDatabaseFurnitureBinder.FurnRow>(bound.Rows);
            if (rows.Count == 0)
                throw new InvalidOperationException(
                    "FURN_TABLE 绑定行集为空 ⇒ 拒绝落盘(Binder 的空表校验应已拦;" +
                    "空 FURN_TYPES 让全部房间谓词恒不匹配 = 静默失败)");

            rows.Sort(CompareType);

            using (var payload = new MemoryStream())
            using (var w = new BinaryWriter(payload, System.Text.Encoding.UTF8))
            {
                w.Write(bound.TierMax);
                w.Write(rows.Count);
                for (int i = 0; i < rows.Count; i++)
                {
                    ClinicMachineCookedWriter.WriteString(w, rows[i].Type);
                    w.Write(rows[i].Tier);
                    w.Write(rows[i].EEnv.Raw);
                }
                w.Flush();
                return ClinicMachineCookedWriter.FinishHeader(
                    payload.ToArray(), bound.SchemaVersion, configVersion);
            }
        }

        /// <summary>按 <c>type</c> 的序数升序(固定全序;`List.Sort` 不稳定 ⇒ 必须给出全序)。</summary>
        private static int CompareType(
            ItemDatabaseFurnitureBinder.FurnRow a, ItemDatabaseFurnitureBinder.FurnRow b)
            => string.CompareOrdinal(a.Type, b.Type);
    }
}
