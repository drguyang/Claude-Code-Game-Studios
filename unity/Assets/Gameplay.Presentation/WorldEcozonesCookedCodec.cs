// `world_ecozones.cooked.bytes`(生态区多边形)的**运行期解码器**。
//
// 权威来源:
//   ADR-014 §二/§五(运行期读烘焙产物)· `CookedFormat.cs`(20 字节头契约)
//   · ADR-022 §三(导出契约)· §四 C4(几何合法性在**构建期**已校验,运行期只做入参卫生)
//   · GDD `world-and-ecozones.md` F-6-3(y 不参与 ⇒ 顶点只有 x / z)
//   写方严格镜像:`Editor.Tools.Bake/WorldEcozonesCookedWriter.cs`(字段序逐字对齐)
//
// ⚠️ 载荷布局(全部 little-endian;固定字段顺序):
//   world_ecozones.cooked
//     i32 zone_count
//     zone_count × { i32 ecozone_id; i32 vertex_count; vertex_count × { i32 x; i32 z } }
//
// ⚠️ **本件为什么住在 `Gameplay.Presentation`**:`EcozoneRegistry` / `EcozonePolygon` 住 `Sim`
//   (门 A 侧),而读 cooked 字节需要 `BinaryReader` + 头校验工具(本程序集)。`Sim` 引用集
//   恰 = {BCL, Sim.Contracts}(ADR-025 §①)⇒ codec 不能进 `Sim`。
//   本件只做「字节 → 行」,行到 `EcozoneRegistry` 的映射由装配侧(`CompositionRoot`)承担。

using System;
using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.Sim.Contracts;   // WorldPos

namespace DaYiJingCheng.Gameplay.Presentation
{
    /// <summary>一个生态区(id + 顶点;顶点 `Y` 恒 0 且不参与判定)。</summary>
    public readonly struct WorldEcozoneRow
    {
        /// <summary>`ecozone_id`(≥ 0;`-1` 是 `None` 哨兵)。</summary>
        public readonly int EcozoneId;

        /// <summary>顶点(闭合环;**首尾不重复**;`Y` 恒 0)。</summary>
        public readonly WorldPos[] Vertices;

        /// <summary>构造。</summary>
        public WorldEcozoneRow(int ecozoneId, WorldPos[] vertices)
        {
            EcozoneId = ecozoneId; Vertices = vertices;
        }
    }

    /// <summary>`world_ecozones.cooked.bytes` 的解码结果。</summary>
    public readonly struct WorldEcozonesDataSet
    {
        /// <summary>cooked 产物名(Addressables 地址同此名)。</summary>
        public const string CookedFileName = "world_ecozones.cooked.bytes";

        /// <summary>生态区行集(行序 = `ecozone_id` 升序)。</summary>
        public readonly IReadOnlyList<WorldEcozoneRow> Zones;

        /// <summary>产物头声明的 schema 版本(ADR-014 §五)。</summary>
        public readonly uint SchemaVersion;

        /// <summary>源内容哈希派生的 ConfigVersion(u32;ADR-014 §五)。</summary>
        public readonly uint ConfigVersion;

        /// <summary>构造。</summary>
        public WorldEcozonesDataSet(IReadOnlyList<WorldEcozoneRow> zones, uint schemaVersion, uint configVersion)
        {
            Zones = zones; SchemaVersion = schemaVersion; ConfigVersion = configVersion;
        }
    }

    /// <summary>生态区多边形(<c>world_ecozones.cooked.bytes</c>)的运行期解码器。</summary>
    public static class WorldEcozonesCookedCodec
    {
        /// <summary>产物名(与写方 `WorldEcozonesBake.CookedFileName` 同值,单一字面量)。</summary>
        public const string ProductName = "world_ecozones";

        /// <summary>本产物的作者态 schema_version 期望值(per-product 口径)。</summary>
        /// <remarks>⚠️ 与 `Editor.Tools.Level.LevelExporter.EcozonesSchemaVersion` **必须同值** ——
        /// 守护机制见 `WorldGeometryCookedCodec.ExpectedSchemaVersion` 的同款注。</remarks>
        public const uint ExpectedSchemaVersion = 1u;

        /// <summary>读生态区多边形。</summary>
        /// <param name="bytes">cooked 字节(含 20 字节头)。</param>
        /// <exception cref="InvalidDataException">头部损坏 / 空表 / 长度不符 / 值域越界 —— 启动期硬失败。</exception>
        public static WorldEcozonesDataSet Read(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));

            using (BinaryReader r = CookedReadUtil.OpenChecked(bytes, ProductName, out uint configVersion))
            {
                uint schemaVersion = ReadU32Le(bytes, 8);
                if (schemaVersion != ExpectedSchemaVersion)
                    throw new InvalidDataException(
                        $"[cooked] {ProductName} schemaVersion = {schemaVersion},期望 " +
                        $"{ExpectedSchemaVersion} —— 布局演进后不得按旧契约读(E-13)。" +
                        "重烘:大医精诚/数据管线/烘焙 world_ecozones");

                int zoneCount = r.ReadInt32();
                if (zoneCount < 1)
                    throw new InvalidDataException(
                        $"[cooked] {ProductName} zone_count = {zoneCount} —— 生态区集合不得为空" +
                        "(空表与「未声明」不可分,且会让 `EcozoneOf` 恒回 None,而那是 story-003 要消灭的状态;E-13)");

                var zones = new WorldEcozoneRow[zoneCount];
                int prevId = -1;
                for (int i = 0; i < zoneCount; i++)
                {
                    int id = r.ReadInt32();
                    int vertexCount = r.ReadInt32();

                    if (id < 0)
                        throw new InvalidDataException(
                            $"[cooked] {ProductName} zones[{i}].ecozone_id = {id} —— 须 ≥ 0" +
                            "(-1 是 None 哨兵,铸出同值即污染归属判定)");
                    if (id <= prevId)
                        throw new InvalidDataException(
                            $"[cooked] {ProductName} zones[{i}].ecozone_id = {id} 未严格升序(前行 {prevId})" +
                            " —— 写方按 id 升序排;乱序即字节损坏或契约漂移");
                    prevId = id;

                    if (vertexCount < 3)
                        throw new InvalidDataException(
                            $"[cooked] {ProductName} zones[{i}].vertex_count = {vertexCount} —— 须 ≥ 3" +
                            "(AC-6-19 ②:少于 3 个顶点没有内部)");

                    var vertices = new WorldPos[vertexCount];
                    for (int v = 0; v < vertexCount; v++)
                    {
                        int x = r.ReadInt32();
                        int z = r.ReadInt32();
                        vertices[v] = new WorldPos(x, 0, z);
                    }
                    zones[i] = new WorldEcozoneRow(id, vertices);
                }

                CookedReadUtil.AssertFullyConsumed(r, ProductName);

                return new WorldEcozonesDataSet(Array.AsReadOnly(zones), schemaVersion, configVersion);
            }
        }

        /// <summary>读头部第 3 个 u32(offset 8 = 作者态 schema_version 的回声)。</summary>
        private static uint ReadU32Le(byte[] b, int offset) =>
            (uint)(b[offset] | (b[offset + 1] << 8) | (b[offset + 2] << 16) | (b[offset + 3] << 24));
    }
}
