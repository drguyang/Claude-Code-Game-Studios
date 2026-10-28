// `world_resources.cooked.bytes`(资源点)的**运行期解码器**。
//
// 权威来源:
//   ADR-014 §二/§五(运行期读烘焙产物)· `CookedFormat.cs`(20 字节头契约)
//   · ADR-022 §三(导出契约)· GDD `foraging.md`(17 采集消费资源点)
//   写方严格镜像:`Editor.Tools.Bake/WorldResourcesCookedWriter.cs`(字段序逐字对齐)
//
// ⚠️ 载荷布局(全部 little-endian;固定字段顺序):
//   world_resources.cooked
//     i32 res_count
//     res_count × { i32 resource_id; i32 x; i32 y; i32 z; i32 ecozone_id }   (每行 20 B)
//
// ⚠️ **本件为什么住在 `Gameplay.Presentation`**:与 `WorldPoiCookedCodec` 同因。
//
// ⚠️ **一格一条**:`WorldGeometry.ResourceGrid` 每格只有一个 `int`,故
//   **同一格不得有两行**(同格两行 ⇒ 填格时后写覆盖先写,且**无声**)。
//   工具侧已在 `ResourceSource` 构造期拒;本件在**读侧**再拒一次(纵深防御:
//   产物可以被手工改)。

using System;
using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.Sim.Contracts;   // WorldPos

namespace DaYiJingCheng.Gameplay.Presentation
{
    /// <summary>一个资源点(资源 id + 格 + 生态区倾向)。</summary>
    public readonly struct WorldResourceRow
    {
        /// <summary>资源 id(≥ 0)。</summary>
        public readonly int ResourceId;

        /// <summary>所在整数格。</summary>
        public readonly WorldPos Cell;

        /// <summary>生态区倾向(`-1` = 无倾向)。</summary>
        public readonly int EcozoneId;

        /// <summary>构造。</summary>
        public WorldResourceRow(int resourceId, WorldPos cell, int ecozoneId)
        {
            ResourceId = resourceId; Cell = cell; EcozoneId = ecozoneId;
        }
    }

    /// <summary>`world_resources.cooked.bytes` 的解码结果。</summary>
    public readonly struct WorldResourcesDataSet
    {
        /// <summary>cooked 产物名(Addressables 地址同此名)。</summary>
        public const string CookedFileName = "world_resources.cooked.bytes";

        /// <summary>资源点行集(行序 = `(resource_id, x, y, z, ecozone_id)` 升序)。</summary>
        public readonly IReadOnlyList<WorldResourceRow> Resources;

        /// <summary>产物头声明的 schema 版本(ADR-014 §五)。</summary>
        public readonly uint SchemaVersion;

        /// <summary>源内容哈希派生的 ConfigVersion(u32;ADR-014 §五)。</summary>
        public readonly uint ConfigVersion;

        /// <summary>构造。</summary>
        public WorldResourcesDataSet(IReadOnlyList<WorldResourceRow> resources,
                                     uint schemaVersion, uint configVersion)
        {
            Resources = resources; SchemaVersion = schemaVersion; ConfigVersion = configVersion;
        }
    }

    /// <summary>资源点(<c>world_resources.cooked.bytes</c>)的运行期解码器。</summary>
    public static class WorldResourcesCookedCodec
    {
        /// <summary>产物名(与写方 `WorldResourcesBake.CookedFileName` 同值)。</summary>
        public const string ProductName = "world_resources";

        /// <summary>本产物的作者态 schema_version 期望值(per-product 口径)。</summary>
        /// <remarks>⚠️ 与 `Editor.Tools.Level.LevelExporter.ResourcesSchemaVersion` **必须同值**。</remarks>
        public const uint ExpectedSchemaVersion = 1u;

        /// <summary>读资源点。</summary>
        /// <param name="bytes">cooked 字节(含 20 字节头)。</param>
        /// <exception cref="InvalidDataException">头部损坏 / 空表 / 长度不符 / 值域越界 / 同格两行 —— 启动期硬失败。</exception>
        public static WorldResourcesDataSet Read(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));

            using (BinaryReader r = CookedReadUtil.OpenChecked(bytes, ProductName, out uint configVersion))
            {
                uint schemaVersion = ReadU32Le(bytes, 8);
                if (schemaVersion != ExpectedSchemaVersion)
                    throw new InvalidDataException(
                        $"[cooked] {ProductName} schemaVersion = {schemaVersion},期望 " +
                        $"{ExpectedSchemaVersion} —— 布局演进后不得按旧契约读(E-13)。" +
                        "重烘:大医精诚/数据管线/烘焙 world_resources");

                int count = r.ReadInt32();
                if (count < 1)
                    throw new InvalidDataException(
                        $"[cooked] {ProductName} res_count = {count} —— 资源点集不得为空" +
                        "(空表与「未声明」不可分,且会让 `ResourceGrid` 全 -1;E-13)");

                var rows = new WorldResourceRow[count];
                var seenCells = new HashSet<(int, int, int)>();

                for (int i = 0; i < count; i++)
                {
                    int resourceId = r.ReadInt32();
                    int x = r.ReadInt32();
                    int y = r.ReadInt32();
                    int z = r.ReadInt32();
                    int ecozoneId = r.ReadInt32();

                    if (resourceId < 0)
                        throw new InvalidDataException(
                            $"[cooked] {ProductName} resources[{i}].resource_id = {resourceId} —— 须 ≥ 0" +
                            "(-1 是「本格无资源」哨兵)");
                    if (ecozoneId < -1)
                        throw new InvalidDataException(
                            $"[cooked] {ProductName} resources[{i}].ecozone_id = {ecozoneId} —— " +
                            "只接受 -1(无倾向)或 ≥ 0");
                    if (!seenCells.Add((x, y, z)))
                        throw new InvalidDataException(
                            $"[cooked] {ProductName} 格 ({x},{y},{z}) 出现两行 —— " +
                            "`ResourceGrid` 每格只有一个 int ⇒ 第二条会被静默丢弃。" +
                            "工具侧 `ResourceSource` 构造期即拒;此处是纵深防御(产物可被手改)");

                    rows[i] = new WorldResourceRow(resourceId, new WorldPos(x, y, z), ecozoneId);
                }

                CookedReadUtil.AssertFullyConsumed(r, ProductName);

                return new WorldResourcesDataSet(Array.AsReadOnly(rows), schemaVersion, configVersion);
            }
        }

        /// <summary>读头部第 3 个 u32(offset 8 = 作者态 schema_version 的回声)。</summary>
        private static uint ReadU32Le(byte[] b, int offset) =>
            (uint)(b[offset] | (b[offset + 1] << 8) | (b[offset + 2] << 16) | (b[offset + 3] << 24));
    }
}
