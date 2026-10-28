// `world_poi.cooked.bytes`(POI **定义**)的**运行期解码器**。
//
// 权威来源:
//   ADR-014 §二/§五(运行期读烘焙产物)· `CookedFormat.cs`(20 字节头契约)
//   · ADR-022 §三(导出契约)· ADR-021 §①(定义 = 派生态;**状态不在此处**)
//   写方严格镜像:`Editor.Tools.Bake/WorldPoiCookedWriter.cs`(字段序逐字对齐)
//
// ⚠️ 载荷布局(全部 little-endian;固定字段顺序):
//   world_poi.cooked
//     i32 poi_count
//     poi_count × { i32 poi_id; i32 x; i32 y; i32 z; i32 type; i32 guard_ref }   (每行 24 B)
//
// ⚠️ **本件只解「定义」**,不含任何状态字段 —— ADR-021 把 POI 一分为二:
//   定义 = 派生态(本产物)· 状态 = 模拟态(世界流,唯一写者 = 系统 6)。
//   在这个 DTO 里加一个 `state` 字段 = 造出第二个真源,那是 ADR 级改动。
//
// ⚠️ **本件为什么住在 `Gameplay.Presentation`**:与 `WorldEcozonesCookedCodec` 同因 ——
//   `Sim` 的引用集恰 = {BCL, Sim.Contracts}(ADR-025 §①),而读 cooked 字节需要
//   `BinaryReader` + 头校验工具(本程序集)。行到 `WorldGeometry.PoiGrid` 的填充由装配侧承担。

using System;
using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.Sim.Contracts;   // WorldPos

namespace DaYiJingCheng.Gameplay.Presentation
{
    /// <summary>一条 POI **定义**(id + 格 + 类型 + 守卫档位)。</summary>
    public readonly struct WorldPoiRow
    {
        /// <summary>`poi_id`(≥ 0;`-1` 是「本格无 POI」的哨兵)。</summary>
        public readonly int PoiId;

        /// <summary>所在整数格(三维 —— POI 是空间里的一个点)。</summary>
        public readonly WorldPos Cell;

        /// <summary>POI 类型(取值闭集归 6 的 GDD)。</summary>
        public readonly int Type;

        /// <summary>守卫强度**档位**(`0` = 无守卫;具体敌人由 52 抽池时决定)。</summary>
        public readonly int GuardRef;

        /// <summary>构造。</summary>
        public WorldPoiRow(int poiId, WorldPos cell, int type, int guardRef)
        {
            PoiId = poiId; Cell = cell; Type = type; GuardRef = guardRef;
        }
    }

    /// <summary>`world_poi.cooked.bytes` 的解码结果。</summary>
    public readonly struct WorldPoiDataSet
    {
        /// <summary>cooked 产物名(Addressables 地址同此名)。</summary>
        public const string CookedFileName = "world_poi.cooked.bytes";

        /// <summary>POI 定义行集(行序 = `poi_id` 升序)。</summary>
        public readonly IReadOnlyList<WorldPoiRow> Pois;

        /// <summary>产物头声明的 schema 版本(ADR-014 §五)。</summary>
        public readonly uint SchemaVersion;

        /// <summary>源内容哈希派生的 ConfigVersion(u32;ADR-014 §五)。</summary>
        public readonly uint ConfigVersion;

        /// <summary>构造。</summary>
        public WorldPoiDataSet(IReadOnlyList<WorldPoiRow> pois, uint schemaVersion, uint configVersion)
        {
            Pois = pois; SchemaVersion = schemaVersion; ConfigVersion = configVersion;
        }
    }

    /// <summary>POI 定义(<c>world_poi.cooked.bytes</c>)的运行期解码器。</summary>
    public static class WorldPoiCookedCodec
    {
        /// <summary>产物名(与写方 `WorldPoiBake.CookedFileName` 同值)。</summary>
        public const string ProductName = "world_poi";

        /// <summary>本产物的作者态 schema_version 期望值(per-product 口径)。</summary>
        /// <remarks>⚠️ 与 `Editor.Tools.Level.LevelExporter.PoiSchemaVersion` **必须同值**
        /// (绑定器的 `ReadSchemaVersion` 交叉校验)。</remarks>
        public const uint ExpectedSchemaVersion = 1u;

        /// <summary>读 POI 定义。</summary>
        /// <param name="bytes">cooked 字节(含 20 字节头)。</param>
        /// <exception cref="InvalidDataException">头部损坏 / 空表 / 长度不符 / 值域越界 —— 启动期硬失败。</exception>
        public static WorldPoiDataSet Read(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));

            using (BinaryReader r = CookedReadUtil.OpenChecked(bytes, ProductName, out uint configVersion))
            {
                uint schemaVersion = ReadU32Le(bytes, 8);
                if (schemaVersion != ExpectedSchemaVersion)
                    throw new InvalidDataException(
                        $"[cooked] {ProductName} schemaVersion = {schemaVersion},期望 " +
                        $"{ExpectedSchemaVersion} —— 布局演进后不得按旧契约读(E-13)。" +
                        "重烘:大医精诚/数据管线/烘焙 world_poi");

                int count = r.ReadInt32();
                if (count < 1)
                    throw new InvalidDataException(
                        $"[cooked] {ProductName} poi_count = {count} —— POI 定义集不得为空" +
                        "(空表与「未声明」不可分,且会让 `_poiIdSet` 零成员、`PoiStateChanged` 永不发出;E-13)");

                var rows = new WorldPoiRow[count];
                int prevId = -1;
                for (int i = 0; i < count; i++)
                {
                    int id = r.ReadInt32();
                    int x = r.ReadInt32();
                    int y = r.ReadInt32();
                    int z = r.ReadInt32();
                    int type = r.ReadInt32();
                    int guardRef = r.ReadInt32();

                    if (id < 0)
                        throw new InvalidDataException(
                            $"[cooked] {ProductName} pois[{i}].poi_id = {id} —— 须 ≥ 0" +
                            "(-1 是「本格无 POI」哨兵,铸出同值即污染「无」与「有」的分界)");
                    if (id <= prevId)
                        throw new InvalidDataException(
                            $"[cooked] {ProductName} pois[{i}].poi_id = {id} 未严格升序(前行 {prevId})" +
                            " —— 写方按 id 升序排;乱序即字节损坏或契约漂移");
                    prevId = id;

                    rows[i] = new WorldPoiRow(id, new WorldPos(x, y, z), type, guardRef);
                }

                CookedReadUtil.AssertFullyConsumed(r, ProductName);

                return new WorldPoiDataSet(Array.AsReadOnly(rows), schemaVersion, configVersion);
            }
        }

        /// <summary>读头部第 3 个 u32(offset 8 = 作者态 schema_version 的回声)。</summary>
        private static uint ReadU32Le(byte[] b, int offset) =>
            (uint)(b[offset] | (b[offset + 1] << 8) | (b[offset + 2] << 16) | (b[offset + 3] << 24));
    }
}
